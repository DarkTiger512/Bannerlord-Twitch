using System.Net.WebSockets;
using System.Text.Json;
using BLT.ExtensionService.Infrastructure;
using BLT.ExtensionService.Models;
using BLT.ExtensionService.Security;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
var connectionString = Environment.GetEnvironmentVariable("BLT_DATABASE") ?? builder.Configuration["BLT:Database"] ?? throw new InvalidOperationException("BLT database is not configured");
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<Database>();
builder.Services.AddSingleton<ChannelStateCache>();
builder.Services.AddSingleton<ChannelRouter>();
builder.Services.AddSingleton<RequestGuard>();
builder.Services.AddSingleton<TwitchExtensionTokenValidator>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins((builder.Configuration["BLT:AllowedOrigins"] ?? "http://127.0.0.1:5173;http://127.0.0.1:5174").Split(';', StringSplitOptions.RemoveEmptyEntries))
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
await app.Services.GetRequiredService<Database>().InitializeAsync(CancellationToken.None);

static IResult Unauthorized(string error) => Results.Problem(error, statusCode: StatusCodes.Status401Unauthorized);
static bool Authorized(HttpContext context, TwitchExtensionTokenValidator validator, string channel, out TwitchPrincipal? principal, out IResult? failure)
{
    if (validator.TryValidate(context.Request.Headers.Authorization, channel, out principal, out var error)) { failure = null; return true; }
    failure = Unauthorized(error); return false;
}

app.MapGet("/health", (ChannelRouter router) => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }));

app.MapGet("/api/channels/{channel}/health", (string channel, HttpContext context, TwitchExtensionTokenValidator validator, ChannelRouter router) =>
{
    if (!Authorized(context, validator, channel, out _, out var failure)) return failure!;
    return Results.Ok(new { status = "ok", channelId = channel, gameConnected = router.IsGameConnected(channel), lastStateAt = router.LastStateAt(channel) });
});

app.MapPost("/api/channels/{channel}/pairing", async (string channel, HttpContext context, TwitchExtensionTokenValidator validator, Database database, IConfiguration config, CancellationToken token) =>
{
    if (!Authorized(context, validator, channel, out var principal, out var failure)) return failure!;
    if (principal!.Role != "broadcaster") return Results.Forbid();
    var code = $"BLT-{Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(2))}-{Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(2))}";
    var expiry = DateTimeOffset.UtcNow.AddMinutes(config.GetValue("BLT:PairingLifetimeMinutes", 10));
    await database.SavePairingCodeAsync(code, channel, expiry, token);
    return Results.Ok(new PairingCodeResponse(code, expiry));
});

app.MapPost("/api/pairing/requests", async (PairingRequestSubmission request, Database database, CancellationToken token) =>
{
    var code = request.Code?.Trim().ToUpperInvariant();
    if (string.IsNullOrWhiteSpace(code) || code.Length > 32 || string.IsNullOrWhiteSpace(request.ModVersion) || request.ModVersion.Length > 40 || string.IsNullOrWhiteSpace(request.PlatformLabel) || request.PlatformLabel.Length > 80 || string.IsNullOrWhiteSpace(request.Fingerprint) || request.Fingerprint.Length > 32)
        return Results.Problem("The pairing request is malformed.", statusCode: 400);
    var requestId = Guid.NewGuid(); var requestToken = InstallationCredentialService.Create(); var candidateCredential = InstallationCredentialService.Create();
    var created = await database.CreatePairingRequestAsync(code, requestId, InstallationCredentialService.Hash(requestToken), InstallationCredentialService.Hash(candidateCredential), request.ModVersion.Trim(), request.PlatformLabel.Trim(), request.Fingerprint.Trim(), token);
    if (created is null) return Results.Problem("The pairing code is invalid, expired, or already used.", statusCode: 400);
    return Results.Ok(new PairingRequestReceipt(requestId, requestToken, candidateCredential, created.Value.ExpiresAt, "pending"));
});

app.MapGet("/api/pairing/requests/{requestId:guid}/status", async (Guid requestId, HttpContext context, Database database, CancellationToken token) =>
{
    var bearer = context.Request.Headers.Authorization.ToString(); var requestToken = bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? bearer[7..].Trim() : null;
    if (string.IsNullOrWhiteSpace(requestToken)) return Unauthorized("A pairing request token is required.");
    var status = await database.GetPairingRequestStatusAsync(requestId, InstallationCredentialService.Hash(requestToken), token);
    return status is null ? Results.NotFound() : Results.Ok(status);
});

app.MapDelete("/api/pairing/requests/{requestId:guid}", async (Guid requestId, HttpContext context, Database database, CancellationToken token) =>
{
    var bearer = context.Request.Headers.Authorization.ToString(); var requestToken = bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? bearer[7..].Trim() : null;
    if (string.IsNullOrWhiteSpace(requestToken)) return Unauthorized("A pairing request token is required.");
    return await database.CancelPairingRequestAsync(requestId, InstallationCredentialService.Hash(requestToken), token) ? Results.NoContent() : Results.NotFound();
});

app.MapPost("/api/pairing/exchange", async (PairingExchangeRequest request, Database database, CancellationToken token) =>
{
    var channel = await database.ConsumePairingCodeAsync(request.Code.Trim().ToUpperInvariant(), token);
    if (channel is null) return Results.Problem("The pairing code is invalid, expired, or already used.", statusCode: 400);
    var credential = InstallationCredentialService.Create();
    var id = await database.CreateInstallationAsync(channel, InstallationCredentialService.Hash(credential), token);
    return Results.Ok(new PairingExchangeResponse(channel, id.ToString(), credential, DateTimeOffset.UtcNow));
});

app.MapGet("/api/channels/{channel}/configuration", async (string channel, HttpContext context, TwitchExtensionTokenValidator validator, Database database, CancellationToken token) =>
{
    if (!Authorized(context, validator, channel, out var principal, out var failure)) return failure!;
    if (principal!.Role != "broadcaster") return Results.Forbid();
    return Results.Ok(await database.GetConfigurationAsync(channel, token));
});

app.MapGet("/api/channels/{channel}/configuration/context", async (string channel, HttpContext context, TwitchExtensionTokenValidator validator, Database database, ChannelRouter router, CancellationToken token) =>
{
    if (!Authorized(context, validator, channel, out var principal, out var failure)) return failure!;
    if (principal!.Role != "broadcaster") return Results.Forbid();
    return Results.Ok(new { configuration = await database.GetConfigurationAsync(channel, token), gameConnected = router.IsGameConnected(channel), lastStateAt = router.LastStateAt(channel), installations = await database.ListInstallationsAsync(channel, token), pairingRequests = await database.ListPairingRequestsAsync(channel, token), runtimeCommands = router.RuntimeCommands(channel) });
});

app.MapPut("/api/channels/{channel}/configuration/apply", async (string channel, ConfigurationApplyRequest request, HttpContext context, TwitchExtensionTokenValidator validator, Database database, ChannelRouter router, CancellationToken token) =>
{
    if (!Authorized(context, validator, channel, out var principal, out var failure)) return failure!;
    if (principal!.Role != "broadcaster") return Results.Forbid();
    try
    {
        var saved = await database.ApplyConfigurationAsync(channel, request.Configuration, request.PairingDecisions, token);
        if (saved is null) return Results.Conflict(new { detail = "Configuration was changed by another session." });
        await router.BroadcastConfigurationAsync(channel, saved, token); return Results.Ok(saved);
    }
    catch (InvalidOperationException ex) { return Results.Conflict(new { detail = ex.Message }); }
});

app.MapPost("/api/channels/{channel}/pairing/decisions", async (string channel, PairingDecisionRequest request, HttpContext context, TwitchExtensionTokenValidator validator, Database database, CancellationToken token) =>
{
    if (!Authorized(context, validator, channel, out var principal, out var failure)) return failure!;
    if (principal!.Role != "broadcaster") return Results.Forbid();
    try { await database.ApplyPairingDecisionsAsync(channel, request.PairingDecisions, token); return Results.NoContent(); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { detail = ex.Message }); }
});

app.MapPut("/api/channels/{channel}/configuration", async (string channel, ChannelConfiguration configuration, HttpContext context, TwitchExtensionTokenValidator validator, Database database, ChannelRouter router, CancellationToken token) =>
{
    if (!Authorized(context, validator, channel, out var principal, out var failure)) return failure!;
    if (principal!.Role != "broadcaster") return Results.Forbid();
    var saved = await database.SaveConfigurationAsync(channel, configuration, token);
    if (saved is null) return Results.Conflict(new { detail = "Configuration was changed by another session." });
    await router.BroadcastConfigurationAsync(channel, saved, token);
    return Results.Ok(saved);
});

app.MapDelete("/api/channels/{channel}/installations/{installationId:guid}", async (string channel, Guid installationId, HttpContext context, TwitchExtensionTokenValidator validator, Database database, ChannelRouter router, CancellationToken token) =>
{
    if (!Authorized(context, validator, channel, out var principal, out var failure)) return failure!;
    if (principal!.Role != "broadcaster") return Results.Forbid();
    if (!await database.RevokeInstallationAsync(channel, installationId, token)) return Results.NotFound();
    await router.DisconnectInstallationAsync(channel, installationId, token);
    return Results.NoContent();
});

app.MapPost("/api/channels/{channel}/actions", async (string channel, ActionSubmission submission, HttpContext context, TwitchExtensionTokenValidator validator, Database database, ChannelRouter router, RequestGuard guard, CancellationToken token) =>
{
    if (!Authorized(context, validator, channel, out var principal, out var failure)) return failure!;
    if (!principal!.IsLinked) return Results.Problem("Share Twitch identity before triggering actions.", statusCode: 403);
    if (!Guid.TryParse(submission.RequestId, out var requestId)) return Results.Problem("requestId must be a UUID.", statusCode: 400);
    if (!submission.ActionId.StartsWith("command.", StringComparison.Ordinal)) return Results.Problem("Unknown action namespace.", statusCode: 400);
    if (!await database.IsActionEnabledAsync(channel, submission.ActionId, token)) return Results.Problem("This action is disabled by the broadcaster.", statusCode: 403);
    if (HtmlInputGuard.ContainsHtml(submission.Args))
        return Results.Problem("Command input cannot contain HTML.", statusCode: 400);
    if (!guard.Accept(requestId, $"{channel}:{principal.UserId}:{submission.ActionId}", submission.Timestamp, out var guardError))
        return Results.Problem(guardError, statusCode: 429);
    var envelope = new
    {
        v = ProtocolKinds.Version, id = requestId, kind = "action.request", channelId = channel, timestamp = submission.Timestamp,
        user = new IntegrationUser(principal.UserId, principal.DisplayName, principal.Roles),
        data = new { actionId = submission.ActionId, args = submission.Args }
    };
    router.RegisterPrivateRequest(requestId, channel, principal.UserId);
    if (!await router.SendGameAsync(channel, envelope, token)) { router.ForgetPrivateRequest(requestId); return Results.Problem("The streamer's game is offline.", statusCode: 409); }
    await database.AuditAsync(requestId, channel, principal.UserId, submission.ActionId, "accepted", null, token);
    return Results.Accepted(value: new { requestId, status = "accepted" });
});

app.MapPost("/api/channels/{channel}/commands", async (string channel, CommandSubmission submission, HttpContext context, TwitchExtensionTokenValidator validator, Database database, ChannelRouter router, RequestGuard guard, CancellationToken token) =>
{
    if (!Authorized(context, validator, channel, out var principal, out var failure)) return failure!;
    if (!principal!.IsLinked) return Results.Problem("Share Twitch identity before running commands.", statusCode: 403);
    if (!Guid.TryParse(submission.RequestId, out var requestId)) return Results.Problem("requestId must be a UUID.", statusCode: 400);
    var commandLine = submission.CommandLine?.Trim();
    if (string.IsNullOrWhiteSpace(commandLine) || commandLine.Length > 512 || commandLine.Any(char.IsControl))
        return Results.Problem("The command line is invalid.", statusCode: 400);
    if (HtmlInputGuard.ContainsHtml(commandLine))
        return Results.Problem("Command input cannot contain HTML.", statusCode: 400);
    if (commandLine.StartsWith('!')) commandLine = commandLine[1..].TrimStart();
    var commandName = commandLine.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0];
    if (!await database.IsActionEnabledAsync(channel, $"command.{commandName.ToLowerInvariant()}", token)) return Results.Problem("This command is disabled by the broadcaster.", statusCode: 403);
    if (!guard.Accept(requestId, $"{channel}:{principal.UserId}:command.{commandName.ToLowerInvariant()}", submission.Timestamp, out var guardError))
        return Results.Problem(guardError, statusCode: 429);
    var envelope = new
    {
        v = ProtocolKinds.Version, id = requestId, kind = "command.request", channelId = channel, timestamp = submission.Timestamp,
        user = new IntegrationUser(principal.UserId, principal.DisplayName, principal.Roles), data = new { commandLine }
    };
    router.RegisterPrivateRequest(requestId, channel, principal.UserId);
    if (!await router.SendGameAsync(channel, envelope, token)) { router.ForgetPrivateRequest(requestId); return Results.Problem("The streamer's game is offline.", statusCode: 409); }
    await database.AuditAsync(requestId, channel, principal.UserId, $"command.{commandName.ToLowerInvariant()}", "accepted", null, token);
    return Results.Accepted(value: new { requestId, status = "accepted" });
});

app.MapPost("/api/channels/{channel}/inventory", async (string channel, InventorySubmission submission, HttpContext context, TwitchExtensionTokenValidator validator, ChannelRouter router, RequestGuard guard, CancellationToken token) =>
{
    if (!Authorized(context, validator, channel, out var principal, out var failure)) return failure!;
    if (!principal!.IsLinked) return Results.Problem("Share Twitch identity before viewing your inventory.", statusCode: 403);
    if (!Guid.TryParse(submission.RequestId, out var requestId)) return Results.Problem("requestId must be a UUID.", statusCode: 400);
    if (!guard.Accept(requestId, $"{channel}:{principal.UserId}:inventory", submission.Timestamp, out var guardError)) return Results.Problem(guardError, statusCode: 429);
    var envelope = new { v = ProtocolKinds.Version, id = requestId, kind = "inventory.request", channelId = channel, timestamp = submission.Timestamp, user = new IntegrationUser(principal.UserId, principal.DisplayName, principal.Roles), data = new { } };
    router.RegisterPrivateRequest(requestId, channel, principal.UserId);
    if (!await router.SendGameAsync(channel, envelope, token)) { router.ForgetPrivateRequest(requestId); return Results.Problem("The streamer's game is offline.", statusCode: 409); }
    return Results.Accepted(value: new { requestId, status = "accepted" });
});

app.MapPost("/api/channels/{channel}/retinue", async (string channel, RetinueSubmission submission, HttpContext context, TwitchExtensionTokenValidator validator, ChannelRouter router, RequestGuard guard, CancellationToken token) =>
{
    if (!Authorized(context, validator, channel, out var principal, out var failure)) return failure!;
    if (!principal!.IsLinked) return Results.Problem("Share Twitch identity before viewing your retinue.", statusCode: 403);
    if (!Guid.TryParse(submission.RequestId, out var requestId)) return Results.Problem("requestId must be a UUID.", statusCode: 400);
    if (!guard.Accept(requestId, $"{channel}:{principal.UserId}:retinue", submission.Timestamp, out var guardError)) return Results.Problem(guardError, statusCode: 429);
    var envelope = new { v = ProtocolKinds.Version, id = requestId, kind = "retinue.request", channelId = channel, timestamp = submission.Timestamp, user = new IntegrationUser(principal.UserId, principal.DisplayName, principal.Roles), data = new { } };
    router.RegisterPrivateRequest(requestId, channel, principal.UserId);
    if (!await router.SendGameAsync(channel, envelope, token)) { router.ForgetPrivateRequest(requestId); return Results.Problem("The streamer's game is offline.", statusCode: 409); }
    return Results.Accepted(value: new { requestId, status = "accepted" });
});

app.Map("/ws/game/{channel}", async (string channel, HttpContext context, Database database, ChannelRouter router, CancellationToken token) =>
{
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
    var authorization = context.Request.Headers.Authorization.ToString();
    if (!authorization.StartsWith("Bearer ")) { context.Response.StatusCode = 401; return; }
    var installationId = await database.ValidateInstallationAsync(channel, InstallationCredentialService.Hash(authorization[7..].Trim()), token);
    if (installationId is null) { context.Response.StatusCode = 401; return; }
    using var socket = await context.WebSockets.AcceptWebSocketAsync("blt.integration.v1");
    await router.AttachGameAsync(channel, installationId.Value, socket, token);
});

app.Map("/ws/viewer/{channel}", async (string channel, HttpContext context, TwitchExtensionTokenValidator validator, ChannelRouter router, CancellationToken token) =>
{
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
    var tokenValue = context.Request.Query["token"].ToString();
    if (!validator.TryValidate($"Bearer {tokenValue}", channel, out var principal, out _)) { context.Response.StatusCode = 401; return; }
    using var socket = await context.WebSockets.AcceptWebSocketAsync("blt.viewer.v1");
    await router.AttachViewerAsync(channel, principal!, socket, token, context.Request.Query["hud"] == "hero-id-v1");
});

app.Run();

public partial class Program;

public static class HtmlInputGuard
{
    public static bool ContainsHtml(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        var lower = value.ToLowerInvariant();
        return value.Contains('<') || value.Contains('>') || lower.Contains("&lt;") || lower.Contains("&gt;");
    }

    public static bool ContainsHtml(IReadOnlyDictionary<string, JsonElement>? values)
    {
        if (values is null) return false;
        return values.Any(pair => ContainsHtml(pair.Key) || ContainsHtml(pair.Value));
    }

    public static bool ContainsHtml(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => ContainsHtml(value.GetString()),
            JsonValueKind.Array => value.EnumerateArray().Any(ContainsHtml),
            JsonValueKind.Object => value.EnumerateObject().Any(property => ContainsHtml(property.Name) || ContainsHtml(property.Value)),
            _ => false
        };
    }
}
