using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Reflection;
using System.Linq;
using BannerlordTwitch.Util;

namespace BannerlordTwitch.Integration
{
    public sealed class IntegrationInventoryItem
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public bool Equipped { get; set; }
    }

    public sealed class IntegrationEquipmentSlot
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Accepts { get; set; }
        public string ItemName { get; set; }
        public int? CustomItemIndex { get; set; }
    }

    public sealed class IntegrationInventorySnapshot
    {
        public string HeroName { get; set; }
        public int Limit { get; set; }
        public string Error { get; set; }
        public IntegrationInventoryItem[] Items { get; set; } = Array.Empty<IntegrationInventoryItem>();
        public IntegrationEquipmentSlot[] Slots { get; set; } = Array.Empty<IntegrationEquipmentSlot>();
    }

    public static class IntegrationInventoryProvider
    {
        public static Func<string, IntegrationInventorySnapshot> Get { private get; set; }
        public static IntegrationInventorySnapshot For(string userName) => Get?.Invoke(userName) ?? new IntegrationInventorySnapshot { Error = "Inventory is unavailable in this game." };
    }

    public sealed class IntegrationRetinueTroop
    {
        public int Slot { get; set; }
        public string Name { get; set; }
        public int Tier { get; set; }
        public string Culture { get; set; }
    }

    public sealed class IntegrationRetinueSnapshot
    {
        public string HeroName { get; set; }
        public string Error { get; set; }
        public IntegrationRetinueTroop[] Retinue { get; set; } = Array.Empty<IntegrationRetinueTroop>();
        public IntegrationRetinueTroop[] EliteRetinue { get; set; } = Array.Empty<IntegrationRetinueTroop>();
    }

    public static class IntegrationRetinueProvider
    {
        public static Func<string, IntegrationRetinueSnapshot> Get { private get; set; }
        public static IntegrationRetinueSnapshot For(string userName) => Get?.Invoke(userName) ?? new IntegrationRetinueSnapshot { Error = "Retinue data is unavailable in this game." };
    }

    public sealed class IntegrationSelectorSnapshot
    {
        public string[] Cultures { get; set; } = Array.Empty<string>();
        public string[] Heroes { get; set; } = Array.Empty<string>();
        public string[] Clans { get; set; } = Array.Empty<string>();
        public string[] Kingdoms { get; set; } = Array.Empty<string>();
        public string[] Settlements { get; set; } = Array.Empty<string>();
        public string[] Skills { get; set; } = Array.Empty<string>();
    }

    public static class IntegrationSelectorProvider
    {
        private static IntegrationSelectorSnapshot current = new IntegrationSelectorSnapshot();
        private static string[] Values(IEnumerable<string> values) => values == null ? Array.Empty<string>() : new List<string>(values).ToArray();
        public static void Set(
            IEnumerable<string> cultures,
            IEnumerable<string> heroes,
            IEnumerable<string> clans,
            IEnumerable<string> kingdoms,
            IEnumerable<string> settlements,
            IEnumerable<string> skills) => current = new IntegrationSelectorSnapshot
        {
            Cultures = Values(cultures), Heroes = Values(heroes), Clans = Values(clans),
            Kingdoms = Values(kingdoms), Settlements = Values(settlements), Skills = Values(skills)
        };
        public static void Clear() => current = new IntegrationSelectorSnapshot();
        public static IntegrationSelectorSnapshot Current() => current;
    }

    public sealed class IntegrationViewerSnapshot
    {
        public bool Adopted { get; set; }
        public string HeroId { get; set; }
        public string HeroName { get; set; }
        public int? Gold { get; set; }
        public IntegrationPrestigeSnapshot Prestige { get; set; }
        public IntegrationViewerBalance BattleBalance { get; set; }
    }

    public sealed class IntegrationViewerBalance
    {
        public string MissionId { get; set; }
        public double LockedBonus { get; set; }
    }
    public sealed class IntegrationBalanceSnapshot
    {
        public string MissionId { get; set; }
        public int Summoners { get; set; }
        public int Attackers { get; set; }
        public double SummonOffer { get; set; }
        public double AttackOffer { get; set; }
    }

    public sealed class IntegrationPrestigeSnapshot
    {
        public int Count { get; set; }
        public int Maximum { get; set; }
        public int RunKills { get; set; }
        public long RequiredKills { get; set; }
        public long RequiredGold { get; set; }
        public bool Eligible { get; set; }
        public string BlockingReason { get; set; }
        public string ResetSummary { get; set; }
        public IntegrationPrestigePerk[] Perks { get; set; }
    }
    public sealed class IntegrationPrestigePerk
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Rank { get; set; }
        public int Cap { get; set; }
    }

    public sealed class IntegrationRuntimeCommand
    {
        public string Name { get; set; }
        public string Handler { get; set; }
        public string Help { get; set; }
        public string HelpKey { get; set; }
        public bool ModeratorOnly { get; set; }
        public bool HideHelp { get; set; }
    }

    public static class IntegrationViewerStateProvider
    {
        public static Func<string, IntegrationViewerSnapshot> Get { private get; set; }
        public static void Set(Func<string, IntegrationViewerSnapshot> provider) => Get = provider;
        public static void Clear() => Get = null;
        public static IntegrationViewerSnapshot For(string userName) => Get?.Invoke(userName) ?? new IntegrationViewerSnapshot();
    }

    public static class IntegrationIdentityProvider
    {
        public static Action<string, string> Reconcile { private get; set; }
        public static void Apply(string userId, string displayName) => Reconcile?.Invoke(userId, displayName);
        public static void Clear() => Reconcile = null;
    }

    public sealed class IntegrationBattleCombatant
    {
        [JsonPropertyName("id")] public string Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("hp")] public float HP { get; set; }
        [JsonPropertyName("maxHp")] public float MaxHP { get; set; }
        [JsonPropertyName("state")] public string State { get; set; }
        [JsonPropertyName("isPlayerSide")] public bool IsPlayerSide { get; set; }
        [JsonPropertyName("tournamentTeam")] public int TournamentTeam { get; set; }
        [JsonPropertyName("cooldownFractionRemaining")] public float CooldownFractionRemaining { get; set; }
        [JsonPropertyName("cooldownSecondsRemaining")] public float CooldownSecondsRemaining { get; set; }
        [JsonPropertyName("activePowerFractionRemaining")] public float ActivePowerFractionRemaining { get; set; }
        [JsonPropertyName("activePowerName")] public string ActivePowerName { get; set; }
        [JsonPropertyName("activePowerActive")] public bool ActivePowerActive { get; set; }
        [JsonPropertyName("kills")] public int Kills { get; set; }
        [JsonPropertyName("retinue")] public int Retinue { get; set; }
        [JsonPropertyName("deadRetinue")] public int DeadRetinue { get; set; }
        [JsonPropertyName("eliteRetinue")] public int EliteRetinue { get; set; }
        [JsonPropertyName("deadEliteRetinue")] public int DeadEliteRetinue { get; set; }
        [JsonPropertyName("retinueKills")] public int RetinueKills { get; set; }
        [JsonPropertyName("goldEarned")] public int GoldEarned { get; set; }
        [JsonPropertyName("xpEarned")] public int XPEarned { get; set; }
        [JsonPropertyName("ammoCurrent")] public int AmmoCurrent { get; set; }
        [JsonPropertyName("ammoMaximum")] public int AmmoMaximum { get; set; }
    }

    public sealed class IntegrationBattleSnapshot
    {
        [JsonPropertyName("active")] public bool Active { get; set; }
        [JsonPropertyName("kind")] public string Kind { get; set; } = "inactive";
        [JsonPropertyName("revision")] public long Revision { get; set; }
        [JsonPropertyName("deploymentFinished")] public bool DeploymentFinished { get; set; }
        [JsonPropertyName("combatants")] public IntegrationBattleCombatant[] Combatants { get; set; } = Array.Empty<IntegrationBattleCombatant>();
        [JsonPropertyName("battleBalance")] public IntegrationBalanceSnapshot BattleBalance { get; set; }
        [JsonPropertyName("actionAvailability")] public Dictionary<string, string> ActionAvailability { get; set; } = new();
    }

    public static class IntegrationBattleProvider
    {
        private static readonly object sync = new();
        private static IntegrationBattleSnapshot current = new();
        private static string signature = "inactive";
        public static void Update(string kind, bool deploymentFinished, IEnumerable<IntegrationBattleCombatant> combatants, IntegrationBalanceSnapshot battleBalance = null)
        {
            var nextCombatants = new List<IntegrationBattleCombatant>(combatants ?? Array.Empty<IntegrationBattleCombatant>()).ToArray();
            var nextSignature = kind + "|" + deploymentFinished + "|" + JsonSerializer.Serialize(nextCombatants) + "|" + JsonSerializer.Serialize(battleBalance);
            lock (sync)
            {
                if (signature == nextSignature) return;
                signature = nextSignature;
                current = new IntegrationBattleSnapshot
                {
                    Active = kind == "battle" || kind == "tournament", Kind = kind, Revision = current.Revision + 1,
                    DeploymentFinished = deploymentFinished, Combatants = nextCombatants,
                    ActionAvailability = MissionActions(kind, deploymentFinished), BattleBalance = battleBalance
                };
            }
        }
        public static void Clear() => Update("inactive", false, Array.Empty<IntegrationBattleCombatant>());
        public static void Touch() { lock (sync) current.Revision++; }
        public static IntegrationBattleSnapshot Current() { lock (sync) return current; }
        private static Dictionary<string, string> MissionActions(string kind, bool deploymentFinished)
        {
            if (kind == "inactive") return new Dictionary<string, string>();
            return new Dictionary<string, string>
            {
                ["command.summon"] = deploymentFinished ? null : "Available when deployment finishes.",
                ["command.attack"] = deploymentFinished ? null : "Available when deployment finishes.",
                ["command.heal"] = null,
                ["command.power"] = null,
                ["command.formation"] = null,
            };
        }
    }

    public static class IntegrationRuntimeState
    {
        private static int saving;
        public static bool IsSaving => Volatile.Read(ref saving) != 0;
        public static void SetSaving(bool value) => Interlocked.Exchange(ref saving, value ? 1 : 0);
    }

    public sealed class ManagedIntegrationClient : IDisposable
    {
        private readonly AuthSettings auth;
        private readonly string channelId;
        private readonly IntegrationActionCatalog catalog;
        private readonly HttpClient http = new();
        private readonly CancellationTokenSource lifetime = new();
        private readonly CancellationToken lifetimeToken;
        private ClientWebSocket socket;
        private volatile bool disposed;
        private readonly ConcurrentDictionary<Guid, byte> receivedRequests = new();
        private readonly IntegrationRequestLifecycle requestLifecycle = new();
        private readonly ConcurrentDictionary<string, IntegrationUser> subscribedViewers = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, string> lastViewerStates = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, string> twitchDisplayNames = new(StringComparer.Ordinal);
        private readonly SemaphoreSlim sendLock = new(1, 1);
        private static readonly JsonSerializerOptions WireJson = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        private readonly IntegrationRuntimeCommand[] runtimeCommands;
        private readonly Dictionary<string, Command> configuredCommands;

        public event Action<IntegrationActionRequest> ActionRequested;
        public event Action<IntegrationCommandRequest> CommandRequested;
        public bool IsConnected => socket?.State == WebSocketState.Open;

        public ManagedIntegrationClient(AuthSettings auth, string channelId, IEnumerable<Command> commands)
        {
            lifetimeToken = lifetime.Token;
            this.auth = auth;
            this.channelId = channelId;
            catalog = IntegrationActionCatalog.Load();
            var commandList = commands.ToArray();
            configuredCommands = commandList.ToDictionary(command => command.Name.ToString(), StringComparer.CurrentCultureIgnoreCase);
            runtimeCommands = commandList.Select(command => new IntegrationRuntimeCommand
            {
                Name = command.Name.ToString(), Handler = command.Handler, Help = command.Help.ToString(),
                HelpKey = $"command.{command.Handler.ToLowerInvariant()}.help",
                ModeratorOnly = command.ModeratorOnly, HideHelp = command.HideHelp
            }).ToArray();
            _ = RunAsync();
        }

        private void PostIfActive(Action action)
        {
            if (disposed) return;
            MainThreadSync.Post(() => { if (!disposed) action(); });
        }

        public bool IsRequestPending(Guid requestId) => !disposed && requestLifecycle.IsPending(requestId);

        private async Task RunAsync()
        {
            var delay = TimeSpan.FromSeconds(2);
            while (!disposed)
            {
                try
                {
                    if (!auth.IntegrationConfigured) return;
                    socket?.Dispose();
                    socket = new ClientWebSocket();
                    socket.Options.SetRequestHeader("Authorization", $"Bearer {auth.IntegrationCredential}");
                    socket.Options.AddSubProtocol("blt.integration.v1");
                    await socket.ConnectAsync(GameSocketUri(), lifetimeToken);
                    delay = TimeSpan.FromSeconds(2);
                    Log.LogFeedSystem("[Integration] Connected to managed Twitch Extension service");
                    await SendAsync("hello", new { modVersion = typeof(ManagedIntegrationClient).Assembly.GetName().Version?.ToString(), protocolVersion = IntegrationProtocol.Version }, lifetimeToken);
                    await SendRawAsync(JsonSerializer.Serialize(new { v = IntegrationProtocol.Version, id = Guid.NewGuid(), kind = "manifest", channelId, timestamp = DateTimeOffset.UtcNow, data = JsonSerializer.Deserialize<JsonElement>(catalog.ManifestJson) }), lifetimeToken);
                    var battle = IntegrationBattleProvider.Current();
                    var gameStarted = Settings.GameStarted;
                    await SendAsync("state.snapshot", new { connected = true, gameStarted, unavailable = new { }, cooldowns = new { }, selectors = IntegrationSelectorProvider.Current(), commands = runtimeCommands, mission = battle }, lifetimeToken);
                    await Task.WhenAll(ReceiveAsync(lifetimeToken), PublishBattleStateAsync(battle.Revision, gameStarted, lifetimeToken), PublishViewerStatesAsync(lifetimeToken));
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) { if (disposed) return; Log.Error($"[Integration] Connection failed: {ex.Message}"); }
                if (disposed) return;
                try { await Task.Delay(delay, lifetimeToken); } catch (OperationCanceledException) { return; }
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 60));
            }
        }

        private async Task PublishBattleStateAsync(long lastRevision, bool lastGameStarted, CancellationToken token)
        {
            while (socket?.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                await Task.Delay(250, token);
                var battle = IntegrationBattleProvider.Current();
                var gameStarted = Settings.GameStarted;
                if (battle.Revision == lastRevision && gameStarted == lastGameStarted) continue;
                lastRevision = battle.Revision;
                lastGameStarted = gameStarted;
                await SendAsync("state.patch", new { gameStarted, mission = battle }, token);
            }
        }

        private async Task PublishViewerStatesAsync(CancellationToken token)
        {
            while (socket?.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                await Task.Delay(500, token);
                if (IntegrationRuntimeState.IsSaving) continue;
                PostIfActive(() =>
                {
                    if (IntegrationRuntimeState.IsSaving) return;
                    foreach (var viewer in subscribedViewers.Values)
                    {
                        IntegrationIdentityProvider.Apply(viewer.Id, viewer.Name);
                        var snapshot = IntegrationViewerStateProvider.For(viewer.Name);
                        var serialized = JsonSerializer.Serialize(snapshot);
                        if (lastViewerStates.TryGetValue(viewer.Id, out var previous) && previous == serialized) continue;
                        lastViewerStates[viewer.Id] = serialized;
                        _ = SendAsync("viewer.state", new { userId = viewer.Id, snapshot.Adopted, snapshot.HeroId, snapshot.HeroName, snapshot.Gold, snapshot.Prestige, snapshot.BattleBalance }, lifetimeToken);
                    }
                });
            }
        }

        private async Task ExchangePairingCodeAsync(CancellationToken token)
        {
            var endpoint = new Uri(new Uri(EnsureTrailingSlash(auth.IntegrationServiceUrl)), "api/pairing/exchange");
            var payload = JsonSerializer.Serialize(new { code = auth.IntegrationPairingCode.Trim().ToUpperInvariant() });
            using var response = await http.PostAsync(endpoint, new StringContent(payload, Encoding.UTF8, "application/json"), token);
            response.EnsureSuccessStatusCode();
            var exchange = JsonSerializer.Deserialize<PairingExchangeResponse>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (exchange == null || !string.Equals(exchange.ChannelId, channelId, StringComparison.Ordinal)) throw new InvalidDataException("Pairing response did not match this Twitch channel");
            auth.IntegrationChannelId = exchange.ChannelId;
            auth.IntegrationInstallationId = exchange.InstallationId;
            auth.IntegrationCredential = exchange.InstallationCredential;
            auth.IntegrationPairingCode = null;
            AuthSettings.Save(auth);
            Log.LogFeedSystem("[Integration] Twitch Extension pairing completed");
        }

        private async Task ReceiveAsync(CancellationToken token)
        {
            var buffer = new byte[64 * 1024];
            while (socket.State == WebSocketState.Open)
            {
                using var stream = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    stream.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
                if (result.MessageType == WebSocketMessageType.Text) await HandleMessageAsync(Encoding.UTF8.GetString(stream.ToArray()), token);
            }
        }

        private async Task<string> ResolveTwitchDisplayNameAsync(JsonElement user, CancellationToken token)
        {
            var userId = user.GetProperty("id").GetString();
            var fallback = user.GetProperty("name").GetString();
            if (string.IsNullOrWhiteSpace(userId)) return fallback;
            if (twitchDisplayNames.TryGetValue(userId, out var cached)) return cached;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.twitch.tv/helix/users?id={Uri.EscapeDataString(userId)}");
                request.Headers.TryAddWithoutValidation("Client-Id", auth.ClientID);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
                using var lookupTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                lookupTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                using var response = await http.SendAsync(request, lookupTimeout.Token);
                response.EnsureSuccessStatusCode();
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var users = document.RootElement.GetProperty("data");
                if (users.GetArrayLength() > 0)
                {
                    var resolved = users[0].TryGetProperty("display_name", out var displayName)
                        ? displayName.GetString()
                        : users[0].GetProperty("login").GetString();
                    if (!string.IsNullOrWhiteSpace(resolved))
                    {
                        twitchDisplayNames[userId] = resolved;
                        return resolved;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Integration] Could not resolve Twitch user {userId}: {ex.Message}");
            }

            return fallback;
        }

        private async Task HandleMessageAsync(string json, CancellationToken token)
        {
            Guid? requestIdForError = null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.GetProperty("v").GetInt32() != IntegrationProtocol.Version) return;
                var kind = root.GetProperty("kind").GetString();
                var data = root.GetProperty("data");
                if (kind == "configuration.updated")
                {
                    var configuration = data.Clone();
                    PostIfActive(() => ApplyConfiguration(configuration));
                    return;
                }
                if (kind == "command.request" || kind == "action.request")
                {
                    requestIdForError = root.GetProperty("id").GetGuid();
                    Log.Info($"[Integration] Received {kind} {requestIdForError}");
                    // Start the deadline before identity lookup or main-thread queuing.
                    await SendActionAcceptedAsync(requestIdForError.Value);
                }
                var user = root.GetProperty("user");
                if (kind == "viewer.subscribe")
                {
                    var viewer = new IntegrationUser { Id = user.GetProperty("id").GetString(), Name = await ResolveTwitchDisplayNameAsync(user, token), Roles = JsonSerializer.Deserialize<string[]>(user.GetProperty("roles").GetRawText()) };
                    subscribedViewers[viewer.Id] = viewer;
                    // A reconnected backend needs the identity mapping even if gold/name did not change.
                    lastViewerStates.TryRemove(viewer.Id, out _);
                    lastViewerStates.TryRemove(viewer.Id, out _);
                    if (!IntegrationRuntimeState.IsSaving)
                        PostIfActive(() => { if (!IntegrationRuntimeState.IsSaving) IntegrationIdentityProvider.Apply(viewer.Id, viewer.Name); });
                    return;
                }
                if (kind == "viewer.unsubscribe")
                {
                    var userId = user.GetProperty("id").GetString();
                    subscribedViewers.TryRemove(userId, out _);
                    lastViewerStates.TryRemove(userId, out _);
                    return;
                }
                if (kind == "inventory.request")
                {
                    var requestId = root.GetProperty("id").GetGuid();
                    if (IntegrationRuntimeState.IsSaving) { await SendAsync("inventory.error", new { error = "Inventory is temporarily unavailable while the campaign is saving." }, token, requestId); return; }
                    var userId = user.GetProperty("id").GetString();
                    var userName = await ResolveTwitchDisplayNameAsync(user, token);
                    PostIfActive(() =>
                    {
                        if (IntegrationRuntimeState.IsSaving) { _ = SendAsync("inventory.error", new { error = "Inventory is temporarily unavailable while the campaign is saving." }, lifetimeToken, requestId); return; }
                        IntegrationIdentityProvider.Apply(userId, userName);
                        var inventory = IntegrationInventoryProvider.For(userName);
                        _ = string.IsNullOrEmpty(inventory.Error)
                            ? SendAsync("inventory.snapshot", inventory, lifetimeToken, requestId)
                            : SendAsync("inventory.error", new { error = inventory.Error }, lifetimeToken, requestId);
                    });
                    return;
                }
                if (kind == "retinue.request")
                {
                    var requestId = root.GetProperty("id").GetGuid();
                    if (IntegrationRuntimeState.IsSaving) { await SendAsync("retinue.error", new { error = "Retinue is temporarily unavailable while the campaign is saving." }, token, requestId); return; }
                    var userId = user.GetProperty("id").GetString();
                    var userName = await ResolveTwitchDisplayNameAsync(user, token);
                    PostIfActive(() =>
                    {
                        if (IntegrationRuntimeState.IsSaving) { _ = SendAsync("retinue.error", new { error = "Retinue is temporarily unavailable while the campaign is saving." }, lifetimeToken, requestId); return; }
                        IntegrationIdentityProvider.Apply(userId, userName);
                        var retinue = IntegrationRetinueProvider.For(userName);
                        _ = string.IsNullOrEmpty(retinue.Error)
                            ? SendAsync("retinue.snapshot", retinue, lifetimeToken, requestId)
                            : SendAsync("retinue.error", new { error = retinue.Error }, lifetimeToken, requestId);
                    });
                    return;
                }
                if (kind == "command.request")
                {
                    requestIdForError = root.GetProperty("id").GetGuid();
                    var commandRequest = new IntegrationCommandRequest
                    {
                        RequestId = requestIdForError.Value, ChannelId = root.GetProperty("channelId").GetString(), Timestamp = root.GetProperty("timestamp").GetDateTimeOffset(),
                        CommandLine = data.GetProperty("commandLine").GetString(),
                        User = new IntegrationUser { Id = user.GetProperty("id").GetString(), Name = await ResolveTwitchDisplayNameAsync(user, token), Roles = JsonSerializer.Deserialize<string[]>(user.GetProperty("roles").GetRawText()) }
                    };
                    if (!string.Equals(commandRequest.ChannelId, channelId, StringComparison.Ordinal)) return;
                    if (Math.Abs((DateTimeOffset.UtcNow - commandRequest.Timestamp).TotalSeconds) > 30) { await SendActionErrorAsync(commandRequest.RequestId, "The command expired before Bannerlord could execute it. Please retry."); return; }
                    if (!receivedRequests.TryAdd(commandRequest.RequestId, 0) || !IsRequestPending(commandRequest.RequestId)) return;
                    CommandRequested?.Invoke(commandRequest);
                    return;
                }
                if (kind != "action.request") return;
                requestIdForError = root.GetProperty("id").GetGuid();
                var actionRequest = new IntegrationActionRequest
                {
                    RequestId = requestIdForError.Value, ChannelId = root.GetProperty("channelId").GetString(), Timestamp = root.GetProperty("timestamp").GetDateTimeOffset(),
                    ActionId = data.GetProperty("actionId").GetString(), Args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(data.GetProperty("args").GetRawText()),
                    User = new IntegrationUser { Id = user.GetProperty("id").GetString(), Name = await ResolveTwitchDisplayNameAsync(user, token), Roles = JsonSerializer.Deserialize<string[]>(user.GetProperty("roles").GetRawText()) }
                };
                if (!string.Equals(actionRequest.ChannelId, channelId, StringComparison.Ordinal)) return;
                if (Math.Abs((DateTimeOffset.UtcNow - actionRequest.Timestamp).TotalSeconds) > 30) { await SendActionErrorAsync(actionRequest.RequestId, "The action expired before Bannerlord could execute it. Please retry."); return; }
                if (!receivedRequests.TryAdd(actionRequest.RequestId, 0) || !IsRequestPending(actionRequest.RequestId)) return;
                ActionRequested?.Invoke(actionRequest);
            }
            catch (Exception ex)
            {
                Log.Error($"[Integration] Rejected malformed action request: {ex.Message}");
                if (requestIdForError.HasValue)
                    _ = SendActionErrorAsync(requestIdForError.Value, "The game rejected a malformed action request.");
            }
        }

        private void ApplyConfiguration(JsonElement data)
        {
            if (!data.TryGetProperty("commands", out var commands) || commands.ValueKind != JsonValueKind.Array) return;
            foreach (var preference in commands.EnumerateArray())
            {
                var actionId = preference.GetProperty("actionId").GetString();
                if (actionId == "command.bltbet") actionId = "command.predict";
                var commandName = actionId?.StartsWith("command.", StringComparison.Ordinal) == true ? actionId.Substring(8) : actionId;
                if (string.IsNullOrWhiteSpace(commandName) || !configuredCommands.TryGetValue(commandName, out var command)) continue;
                if (preference.TryGetProperty("enabled", out var enabled)) command.Enabled = enabled.GetBoolean();
                if (!preference.TryGetProperty("settings", out var settings) || settings.ValueKind != JsonValueKind.Object) continue;
                foreach (var setting in settings.EnumerateObject())
                {
                    // Public prediction metadata comes from the corrected module, never stale saved profiles.
                    if (commandName == "predict" && new[] { "Name", "Help", "Documentation", "Handler" }.Contains(setting.Name, StringComparer.OrdinalIgnoreCase)) continue;
                    object target = command; var propertyName = setting.Name;
                    if (propertyName.StartsWith("HandlerConfig.", StringComparison.Ordinal)) { target = command.HandlerConfig; propertyName = propertyName.Substring(14); }
                    if (target == null) continue;
                    var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                    if (property == null || !property.CanWrite) continue;
                    try
                    {
                        object value = property.PropertyType == typeof(bool) ? setting.Value.GetBoolean()
                            : property.PropertyType == typeof(int) ? setting.Value.GetInt32()
                            : property.PropertyType == typeof(float) ? setting.Value.GetSingle()
                            : property.PropertyType == typeof(double) ? setting.Value.GetDouble()
                            : property.PropertyType == typeof(string) ? setting.Value.GetString()
                            : JsonSerializer.Deserialize(setting.Value.GetRawText(), property.PropertyType);
                        property.SetValue(target, value);
                    }
                    catch (Exception ex) { Log.Error($"[Integration] Could not apply {commandName}.{setting.Name}: {ex.Message}"); }
                }
            }
        }

        public bool TryResolve(IntegrationActionRequest request, out IntegrationActionDefinition definition, out string legacyArgs, out string error)
        {
            definition = null; legacyArgs = null; error = null;
            try
            {
                if (!catalog.TryGet(request.ActionId, out definition)) { error = "Unknown action."; return false; }
                legacyArgs = catalog.BuildLegacyArguments(definition, request.Args);
                return true;
            }
            catch (ArgumentException ex) { error = ex.Message; return false; }
        }

        public Task SendActionAcceptedAsync(Guid requestId)
        {
            if (requestLifecycle.TryAccept(requestId, out var timeoutToken))
                _ = ExpireRequestAsync(requestId, timeoutToken);
            return SendAsync("action.accepted", new { requestId }, lifetimeToken, requestId);
        }

        public Task SendActionResultAsync(Guid requestId, string[] messages) =>
            SendTerminalAsync("action.result", requestId, new { requestId, messages });

        public Task SendActionErrorAsync(Guid requestId, string error) =>
            SendTerminalAsync("action.error", requestId, new { requestId, error });

        private async Task SendTerminalAsync(string kind, Guid requestId, object data)
        {
            if (disposed || !requestLifecycle.TryComplete(requestId)) return;
            Log.Info($"[Integration] Completed {requestId}: {kind}");
            await SendAsync(kind, data, lifetimeToken, requestId);
            _ = ForgetTerminalAsync(requestId);
        }

        private async Task ExpireRequestAsync(Guid requestId, CancellationToken token)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(30), token); }
            catch (OperationCanceledException) { return; }
            if (!requestLifecycle.TryExpire(requestId)) return;
            await SendAsync("action.error", new { requestId, error = "Bannerlord did not return a result within 30 seconds." }, lifetimeToken, requestId);
            _ = ForgetTerminalAsync(requestId);
        }

        private async Task ForgetTerminalAsync(Guid requestId)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(5), lifetimeToken); }
            catch (OperationCanceledException) { return; }
            requestLifecycle.Forget(requestId);
        }

        private Task SendAsync(string kind, object data, CancellationToken token, Guid? id = null) =>
            SendRawAsync(JsonSerializer.Serialize(new { v = IntegrationProtocol.Version, id = id ?? Guid.NewGuid(), kind, channelId, timestamp = DateTimeOffset.UtcNow, data }, WireJson), token);

        private async Task SendRawAsync(string json, CancellationToken token)
        {
            if (disposed || token.IsCancellationRequested || socket?.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(json);
            await sendLock.WaitAsync(token);
            try { if (socket?.State == WebSocketState.Open) await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token); }
            finally { sendLock.Release(); }
        }

        private Uri GameSocketUri()
        {
            var builder = new UriBuilder(new Uri(new Uri(EnsureTrailingSlash(auth.IntegrationServiceUrl)), $"ws/game/{Uri.EscapeDataString(channelId)}"))
            { Scheme = auth.IntegrationServiceUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws" };
            return builder.Uri;
        }

        private static string EnsureTrailingSlash(string value) => value.EndsWith("/") ? value : value + "/";

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; lifetime.Cancel();
            requestLifecycle.Dispose(); socket?.Dispose(); http.Dispose(); lifetime.Dispose();
        }
    }
}
