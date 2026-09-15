using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BLT.ExtensionService.Models;

namespace BLT.ExtensionService.Infrastructure;

public sealed class ChannelRouter(ChannelStateCache stateCache, Database database)
{
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);
    private sealed record GameConnection(Guid InstallationId, WebSocket Socket);
    private readonly ConcurrentDictionary<string, GameConnection> games = new(StringComparer.Ordinal);
    private sealed record ViewerConnection(string UserId, string DisplayName, IReadOnlyList<string> Roles, WebSocket Socket, bool NativeHeroHud);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, ViewerConnection>> viewers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, (string Channel, string UserId)> privateRequests = new();
    private readonly ConcurrentDictionary<(string Channel, string UserId), string> viewerHeroes = new();

    private void ClearViewerHeroes(string channel)
    {
        foreach (var key in viewerHeroes.Keys.Where(key => key.Channel == channel)) viewerHeroes.TryRemove(key, out _);
    }

    private string ViewerMessage(string channel, string userId, string message, bool nativeHeroHud = false) =>
        PersonalBattleHud.Prepare(message, viewerHeroes.TryGetValue((channel, userId), out var heroId) ? heroId : null, nativeHeroHud);

    public bool IsGameConnected(string channel) => games.TryGetValue(channel, out var game) && game.Socket.State == WebSocketState.Open;
    public DateTimeOffset? LastStateAt(string channel) => stateCache.LastStateAt(channel);
    public JsonElement RuntimeCommands(string channel)
    {
        if (stateCache.TryGet(channel, out var state))
        {
            using var document = JsonDocument.Parse(state);
            if (document.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty("commands", out var commands)) return commands.Clone();
        }
        return JsonSerializer.SerializeToElement(Array.Empty<object>());
    }
    public async Task BroadcastConfigurationAsync(string channel, ChannelConfiguration configuration, CancellationToken token)
    {
        var envelope = Envelope("configuration.updated", channel, new { configuration.SchemaVersion, configuration.ExtensionEnabled, configuration.Commands, configuration.Revision, configuration.UpdatedAt });
        await BroadcastViewerAsync(channel, envelope, token);
        await SendGameAsync(channel, JsonSerializer.Deserialize<JsonElement>(envelope), token);
    }

    public async Task AttachGameAsync(string channel, Guid installationId, WebSocket socket, CancellationToken token)
    {
        if (games.TryGetValue(channel, out var previous) && previous.Socket.State == WebSocketState.Open)
            await previous.Socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Replaced by a new game connection", token);
        stateCache.Clear(channel);
        ClearViewerHeroes(channel);
        var connection = new GameConnection(installationId, socket);
        games[channel] = connection;
        var configuration = await database.GetConfigurationAsync(channel, token);
        await SendAsync(socket, Envelope("configuration.updated", channel, new { configuration.SchemaVersion, configuration.ExtensionEnabled, configuration.Commands, configuration.Revision, configuration.UpdatedAt }), token);
        await BroadcastViewerAsync(channel, Envelope("connection.status", channel, new { connected = true, gameStarted = false }), token);
        // A restarted game has no subscriptions; replay each authenticated viewer once.
        if (viewers.TryGetValue(channel, out var connectedViewers))
            foreach (var viewer in connectedViewers.Values.DistinctBy(viewer => viewer.UserId))
                await SendGameAsync(channel, new { v = ProtocolKinds.Version, id = Guid.NewGuid(), kind = "viewer.subscribe", channelId = channel, timestamp = DateTimeOffset.UtcNow, user = new IntegrationUser(viewer.UserId, viewer.DisplayName, viewer.Roles), data = new { } }, token);
        await PumpAsync(socket, async message => await RouteGameMessageAsync(channel, message, token), token);
        if (games.TryRemove(new KeyValuePair<string, GameConnection>(channel, connection)))
        {
            stateCache.Clear(channel);
            await BroadcastViewerAsync(channel, Envelope("connection.status", channel, new { connected = false, gameStarted = false }), CancellationToken.None);
        }
    }

    public async Task DisconnectInstallationAsync(string channel, Guid installationId, CancellationToken token)
    {
        if (!games.TryGetValue(channel, out var game) || game.InstallationId != installationId) return;
        if (!games.TryRemove(new KeyValuePair<string, GameConnection>(channel, game))) return;
        stateCache.Clear(channel);
        if (game.Socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            await game.Socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Installation revoked", token);
        await BroadcastViewerAsync(channel, Envelope("connection.status", channel, new { connected = false, gameStarted = false }), CancellationToken.None);
    }

    public async Task AttachViewerAsync(string channel, TwitchPrincipal principal, WebSocket socket, CancellationToken token, bool nativeHeroHud = false)
    {
        var id = Guid.NewGuid();
        viewers.GetOrAdd(channel, _ => new ConcurrentDictionary<Guid, ViewerConnection>())[id] = new(principal.UserId, principal.DisplayName, principal.Roles, socket, nativeHeroHud);
        await SendAsync(socket, Envelope("connection.status", channel, new { connected = IsGameConnected(channel), gameStarted = false }), token);
        if (stateCache.TryGet(channel, out var state)) await SendAsync(socket, ViewerMessage(channel, principal.UserId, ViewerStateEnvelope.Prepare(state), nativeHeroHud), token);
        await SendGameAsync(channel, new { v = ProtocolKinds.Version, id = Guid.NewGuid(), kind = "viewer.subscribe", channelId = channel, timestamp = DateTimeOffset.UtcNow, user = new IntegrationUser(principal.UserId, principal.DisplayName, principal.Roles), data = new { } }, token);
        await PumpAsync(socket, _ => Task.CompletedTask, token);
        if (viewers.TryGetValue(channel, out var channelViewers)) channelViewers.TryRemove(id, out _);
        if (!viewers.TryGetValue(channel, out channelViewers) || !channelViewers.Values.Any(viewer => viewer.UserId == principal.UserId))
            await SendGameAsync(channel, new { v = ProtocolKinds.Version, id = Guid.NewGuid(), kind = "viewer.unsubscribe", channelId = channel, timestamp = DateTimeOffset.UtcNow, user = new IntegrationUser(principal.UserId, principal.DisplayName, principal.Roles), data = new { } }, CancellationToken.None);
    }

    public async Task<bool> SendGameAsync(string channel, object payload, CancellationToken token)
    {
        if (!games.TryGetValue(channel, out var game) || game.Socket.State != WebSocketState.Open) return false;
        await SendAsync(game.Socket, JsonSerializer.Serialize(payload, WireJson), token);
        return true;
    }

    public void RegisterPrivateRequest(Guid requestId, string channel, string userId) => privateRequests[requestId] = (channel, userId);
    public void ForgetPrivateRequest(Guid requestId) => privateRequests.TryRemove(requestId, out _);

    private async Task RouteGameMessageAsync(string channel, string message, CancellationToken token)
    {
        try
        {
            message = PredictionCompatibility.PublicMessage(message);
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            var kind = root.GetProperty("kind").GetString();
            if (kind is "state.snapshot" or "state.patch")
            {
                if (!stateCache.TryAccept(channel, message, out var normalized)) return;
                message = ViewerStateEnvelope.Prepare(normalized);
            }
            if (kind is "action.accepted" or "action.result" or "action.error" or "inventory.snapshot" or "inventory.error" or "retinue.snapshot" or "retinue.error")
            {
                var requestId = root.GetProperty("id").GetGuid();
                if (privateRequests.TryGetValue(requestId, out var target) && target.Channel == channel)
                {
                    await SendViewerAsync(channel, target.UserId, message, token);
                    if (kind is not "action.accepted") ForgetPrivateRequest(requestId);
                }
                return;
            }
            if (kind == "viewer.state")
            {
                var targetUserId = root.GetProperty("data").GetProperty("userId").GetString();
                if (!string.IsNullOrWhiteSpace(targetUserId))
                {
                    var data = root.GetProperty("data");
                    var heroId = data.TryGetProperty("adopted", out var adopted) && adopted.ValueKind == JsonValueKind.True
                        && data.TryGetProperty("heroId", out var hero) && hero.ValueKind == JsonValueKind.String ? hero.GetString() : null;
                    var key = (channel, targetUserId);
                    viewerHeroes.TryGetValue(key, out var previousHeroId);
                    if (string.IsNullOrEmpty(heroId)) viewerHeroes.TryRemove(key, out _);
                    else viewerHeroes[key] = heroId;
                    await SendViewerAsync(channel, targetUserId, message, token);
                    if (previousHeroId != heroId && stateCache.TryGet(channel, out var state))
                        await SendViewerAsync(channel, targetUserId, ViewerStateEnvelope.Prepare(state), token);
                }
                return;
            }
        }
        catch (JsonException) { return; }
        await BroadcastViewerAsync(channel, message, token);
    }

    private async Task SendViewerAsync(string channel, string userId, string message, CancellationToken token)
    {
        if (!viewers.TryGetValue(channel, out var sockets)) return;
        foreach (var pair in sockets.Where(pair => pair.Value.UserId == userId).ToArray())
        {
            if (pair.Value.Socket.State != WebSocketState.Open) { sockets.TryRemove(pair.Key, out _); continue; }
            try { await SendAsync(pair.Value.Socket, ViewerMessage(channel, pair.Value.UserId, message, pair.Value.NativeHeroHud), token); } catch (WebSocketException) { sockets.TryRemove(pair.Key, out _); }
        }
    }

    private async Task BroadcastViewerAsync(string channel, string message, CancellationToken token)
    {
        if (!viewers.TryGetValue(channel, out var sockets)) return;
        foreach (var pair in sockets.ToArray())
        {
            if (pair.Value.Socket.State != WebSocketState.Open) { sockets.TryRemove(pair.Key, out _); continue; }
            try { await SendAsync(pair.Value.Socket, ViewerMessage(channel, pair.Value.UserId, message, pair.Value.NativeHeroHud), token); } catch (WebSocketException) { sockets.TryRemove(pair.Key, out _); }
        }
    }

    private static async Task PumpAsync(WebSocket socket, Func<string, Task> onMessage, CancellationToken token)
    {
        var buffer = new byte[64 * 1024];
        try
        {
            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                using var stream = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, token);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    stream.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
                if (result.MessageType == WebSocketMessageType.Text) await onMessage(Encoding.UTF8.GetString(stream.ToArray()));
            }
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private static Task SendAsync(WebSocket socket, string message, CancellationToken token) =>
        socket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, token);

    private static string Envelope(string kind, string channel, object data) => JsonSerializer.Serialize(new
    {
        v = ProtocolKinds.Version, id = Guid.NewGuid(), kind, channelId = channel, timestamp = DateTimeOffset.UtcNow, data
    });
}
