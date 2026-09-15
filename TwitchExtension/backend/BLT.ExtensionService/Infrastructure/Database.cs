using System.Text.Json;
using BLT.ExtensionService.Models;
using Npgsql;

namespace BLT.ExtensionService.Infrastructure;

public sealed class Database(NpgsqlDataSource dataSource)
{
    public async Task InitializeAsync(CancellationToken token)
    {
        const string sql = """
        CREATE TABLE IF NOT EXISTS pairing_codes (
          code text PRIMARY KEY, channel_id text NOT NULL, expires_at timestamptz NOT NULL, consumed_at timestamptz NULL
        );
        CREATE TABLE IF NOT EXISTS installations (
          installation_id uuid PRIMARY KEY, channel_id text NOT NULL, credential_hash text NOT NULL UNIQUE,
          created_at timestamptz NOT NULL, revoked_at timestamptz NULL, last_seen_at timestamptz NULL
        );
        CREATE INDEX IF NOT EXISTS installations_channel_idx ON installations(channel_id);
        CREATE TABLE IF NOT EXISTS pairing_requests (
          request_id uuid PRIMARY KEY, channel_id text NOT NULL, request_token_hash text NOT NULL UNIQUE,
          credential_hash text NOT NULL UNIQUE, mod_version text NOT NULL, platform_label text NOT NULL,
          fingerprint text NOT NULL, status text NOT NULL DEFAULT 'pending', created_at timestamptz NOT NULL,
          expires_at timestamptz NOT NULL, decided_at timestamptz NULL, installation_id uuid NULL
        );
        CREATE INDEX IF NOT EXISTS pairing_requests_channel_idx ON pairing_requests(channel_id,created_at DESC);
        CREATE TABLE IF NOT EXISTS channel_configurations (
          channel_id text PRIMARY KEY, document jsonb NOT NULL, revision bigint NOT NULL DEFAULT 0, updated_at timestamptz NOT NULL
        );
        ALTER TABLE channel_configurations ADD COLUMN IF NOT EXISTS revision bigint NOT NULL DEFAULT 0;
        CREATE TABLE IF NOT EXISTS action_audit (
          audit_id bigserial PRIMARY KEY, request_id uuid NOT NULL, channel_id text NOT NULL, user_id text NOT NULL,
          action_id text NOT NULL, status text NOT NULL, detail text NULL, created_at timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS action_audit_request_idx ON action_audit(request_id);
        """;
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync(token);
        await MigratePredictionConfigurationsAsync(token);
    }

    private async Task MigratePredictionConfigurationsAsync(CancellationToken token)
    {
        var pending = new List<(string Channel, ChannelConfiguration Configuration)>();
        await using (var query = dataSource.CreateCommand("SELECT channel_id,document::text FROM channel_configurations WHERE document::text LIKE '%command.bltbet%'"))
        await using (var reader = await query.ExecuteReaderAsync(token))
            while (await reader.ReadAsync(token)) pending.Add((reader.GetString(0), JsonSerializer.Deserialize<ChannelConfiguration>(reader.GetString(1))!));
        foreach (var item in pending) await SaveConfigurationAsync(item.Channel, item.Configuration, token);
    }

    public async Task SavePairingCodeAsync(string code, string channel, DateTimeOffset expiresAt, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand("WITH invalidated AS (UPDATE pairing_codes SET consumed_at=now() WHERE channel_id=$2 AND consumed_at IS NULL) INSERT INTO pairing_codes(code,channel_id,expires_at) VALUES($1,$2,$3) ON CONFLICT(code) DO UPDATE SET channel_id=$2,expires_at=$3,consumed_at=NULL");
        command.Parameters.AddWithValue(code); command.Parameters.AddWithValue(channel); command.Parameters.AddWithValue(expiresAt);
        await command.ExecuteNonQueryAsync(token);
    }

    public async Task<string?> ConsumePairingCodeAsync(string code, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand("UPDATE pairing_codes SET consumed_at=now() WHERE code=$1 AND consumed_at IS NULL AND expires_at>now() RETURNING channel_id");
        command.Parameters.AddWithValue(code);
        return (string?)await command.ExecuteScalarAsync(token);
    }

    public async Task<(string Channel, DateTimeOffset ExpiresAt)?> CreatePairingRequestAsync(string code, Guid requestId, string requestTokenHash, string credentialHash, string modVersion, string platformLabel, string fingerprint, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await using var claim = new NpgsqlCommand("UPDATE pairing_codes SET consumed_at=now() WHERE code=$1 AND consumed_at IS NULL AND expires_at>now() RETURNING channel_id,expires_at", connection, transaction);
        claim.Parameters.AddWithValue(code);
        await using var reader = await claim.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) { await reader.DisposeAsync(); await transaction.RollbackAsync(token); return null; }
        var channel = reader.GetString(0); var expiresAt = reader.GetFieldValue<DateTimeOffset>(1);
        await reader.DisposeAsync();
        await using var insert = new NpgsqlCommand("INSERT INTO pairing_requests(request_id,channel_id,request_token_hash,credential_hash,mod_version,platform_label,fingerprint,created_at,expires_at) VALUES($1,$2,$3,$4,$5,$6,$7,now(),$8)", connection, transaction);
        insert.Parameters.AddWithValue(requestId); insert.Parameters.AddWithValue(channel); insert.Parameters.AddWithValue(requestTokenHash); insert.Parameters.AddWithValue(credentialHash);
        insert.Parameters.AddWithValue(modVersion); insert.Parameters.AddWithValue(platformLabel); insert.Parameters.AddWithValue(fingerprint); insert.Parameters.AddWithValue(expiresAt);
        await insert.ExecuteNonQueryAsync(token); await transaction.CommitAsync(token);
        return (channel, expiresAt);
    }

    public async Task<PairingRequestStatus?> GetPairingRequestStatusAsync(Guid requestId, string requestTokenHash, CancellationToken token)
    {
        await using var expire = dataSource.CreateCommand("UPDATE pairing_requests SET status='expired',decided_at=now() WHERE request_id=$1 AND request_token_hash=$2 AND status='pending' AND expires_at<=now()");
        expire.Parameters.AddWithValue(requestId); expire.Parameters.AddWithValue(requestTokenHash); await expire.ExecuteNonQueryAsync(token);
        await using var command = dataSource.CreateCommand("SELECT status,channel_id,installation_id,expires_at FROM pairing_requests WHERE request_id=$1 AND request_token_hash=$2");
        command.Parameters.AddWithValue(requestId); command.Parameters.AddWithValue(requestTokenHash);
        await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) return null;
        return new(requestId, reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetGuid(2).ToString(), reader.GetFieldValue<DateTimeOffset>(3));
    }

    public async Task<bool> CancelPairingRequestAsync(Guid requestId, string requestTokenHash, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand("UPDATE pairing_requests SET status='cancelled',decided_at=now() WHERE request_id=$1 AND request_token_hash=$2 AND status='pending'");
        command.Parameters.AddWithValue(requestId); command.Parameters.AddWithValue(requestTokenHash);
        return await command.ExecuteNonQueryAsync(token) == 1;
    }

    public async Task<IReadOnlyList<PairingRequestSummary>> ListPairingRequestsAsync(string channel, CancellationToken token)
    {
        await using var expire = dataSource.CreateCommand("UPDATE pairing_requests SET status='expired',decided_at=now() WHERE channel_id=$1 AND status='pending' AND expires_at<=now()");
        expire.Parameters.AddWithValue(channel); await expire.ExecuteNonQueryAsync(token);
        await using var command = dataSource.CreateCommand("SELECT request_id,mod_version,platform_label,fingerprint,created_at,expires_at,status FROM pairing_requests WHERE channel_id=$1 AND created_at>now()-interval '24 hours' ORDER BY created_at DESC");
        command.Parameters.AddWithValue(channel); await using var reader = await command.ExecuteReaderAsync(token); var result = new List<PairingRequestSummary>();
        while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4), reader.GetFieldValue<DateTimeOffset>(5), reader.GetString(6)));
        return result;
    }

    public async Task<ChannelConfiguration?> ApplyConfigurationAsync(string channel, ChannelConfiguration configuration, IReadOnlyList<PairingDecision> decisions, CancellationToken token)
    {
        var normalized = Normalize(configuration); var updated = normalized with { SchemaVersion = 2, Revision = configuration.Revision + 1, UpdatedAt = DateTimeOffset.UtcNow }; var json = JsonSerializer.Serialize(updated);
        await using var connection = await dataSource.OpenConnectionAsync(token); await using var transaction = await connection.BeginTransactionAsync(token);
        await using var save = new NpgsqlCommand("INSERT INTO channel_configurations(channel_id,document,revision,updated_at) SELECT $1,$2::jsonb,$3,$4 WHERE $5=0 OR EXISTS (SELECT 1 FROM channel_configurations WHERE channel_id=$1) ON CONFLICT(channel_id) DO UPDATE SET document=$2::jsonb,revision=$3,updated_at=$4 WHERE channel_configurations.revision=$5 RETURNING revision", connection, transaction);
        save.Parameters.AddWithValue(channel); save.Parameters.AddWithValue(json); save.Parameters.AddWithValue(updated.Revision); save.Parameters.AddWithValue(updated.UpdatedAt); save.Parameters.AddWithValue(configuration.Revision);
        if (await save.ExecuteScalarAsync(token) is not long) { await transaction.RollbackAsync(token); return null; }
        await ApplyPairingDecisionsAsync(connection, transaction, channel, decisions, token);
        await transaction.CommitAsync(token); return updated;
    }

    public async Task ApplyPairingDecisionsAsync(string channel, IReadOnlyList<PairingDecision> decisions, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await ApplyPairingDecisionsAsync(connection, transaction, channel, decisions, token);
        await transaction.CommitAsync(token);
    }

    private static async Task ApplyPairingDecisionsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string channel, IReadOnlyList<PairingDecision> decisions, CancellationToken token)
    {
        foreach (var decision in decisions)
        {
            if (decision.Decision is not ("approved" or "denied")) throw new InvalidOperationException("Pairing decisions must be approved or denied.");
            if (decision.Decision == "approved")
            {
                var installationId = Guid.NewGuid();
                await using var approve = new NpgsqlCommand("WITH chosen AS (UPDATE pairing_requests SET status='approved',decided_at=now(),installation_id=$3 WHERE request_id=$1 AND channel_id=$2 AND status='pending' AND expires_at>now() RETURNING credential_hash) INSERT INTO installations(installation_id,channel_id,credential_hash,created_at) SELECT $3,$2,credential_hash,now() FROM chosen", connection, transaction);
                approve.Parameters.AddWithValue(decision.RequestId); approve.Parameters.AddWithValue(channel); approve.Parameters.AddWithValue(installationId);
                if (await approve.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("A pairing request changed or expired before it could be approved.");
            }
            else
            {
                await using var deny = new NpgsqlCommand("UPDATE pairing_requests SET status='denied',decided_at=now() WHERE request_id=$1 AND channel_id=$2 AND status='pending' AND expires_at>now()", connection, transaction);
                deny.Parameters.AddWithValue(decision.RequestId); deny.Parameters.AddWithValue(channel);
                if (await deny.ExecuteNonQueryAsync(token) != 1) throw new InvalidOperationException("A pairing request changed or expired before it could be denied.");
            }
        }
    }

    public async Task<Guid> CreateInstallationAsync(string channel, string credentialHash, CancellationToken token)
    {
        var id = Guid.NewGuid();
        await using var command = dataSource.CreateCommand("INSERT INTO installations(installation_id,channel_id,credential_hash,created_at) VALUES($1,$2,$3,now())");
        command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(channel); command.Parameters.AddWithValue(credentialHash);
        await command.ExecuteNonQueryAsync(token);
        return id;
    }

    public async Task<Guid?> ValidateInstallationAsync(string channel, string credentialHash, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand("UPDATE installations SET last_seen_at=now() WHERE channel_id=$1 AND credential_hash=$2 AND revoked_at IS NULL RETURNING installation_id");
        command.Parameters.AddWithValue(channel); command.Parameters.AddWithValue(credentialHash);
        return await command.ExecuteScalarAsync(token) is Guid installationId ? installationId : null;
    }

    public async Task<ChannelConfiguration?> SaveConfigurationAsync(string channel, ChannelConfiguration configuration, CancellationToken token)
    {
        var normalized = Normalize(configuration);
        var updated = normalized with { SchemaVersion = 2, Revision = configuration.Revision + 1, UpdatedAt = DateTimeOffset.UtcNow };
        var json = JsonSerializer.Serialize(updated);
        await using var command = dataSource.CreateCommand("""
          INSERT INTO channel_configurations(channel_id,document,revision,updated_at)
          SELECT $1,$2::jsonb,$3,$4 WHERE $5=0 OR EXISTS (SELECT 1 FROM channel_configurations WHERE channel_id=$1)
          ON CONFLICT(channel_id) DO UPDATE SET document=$2::jsonb,revision=$3,updated_at=$4
          WHERE channel_configurations.revision=$5 RETURNING revision
          """);
        command.Parameters.AddWithValue(channel); command.Parameters.AddWithValue(json); command.Parameters.AddWithValue(updated.Revision); command.Parameters.AddWithValue(updated.UpdatedAt); command.Parameters.AddWithValue(configuration.Revision);
        return await command.ExecuteScalarAsync(token) is long ? updated : null;
    }

    public async Task<ChannelConfiguration> GetConfigurationAsync(string channel, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand("SELECT document::text FROM channel_configurations WHERE channel_id=$1");
        command.Parameters.AddWithValue(channel);
        var json = (string?)await command.ExecuteScalarAsync(token);
        if (json is null) return Normalize(new ChannelConfiguration(2, true, [], 0, DateTimeOffset.UtcNow));
        var stored = JsonSerializer.Deserialize<ChannelConfiguration>(json)!;
        return Normalize(stored);
    }

    public async Task<bool> IsActionEnabledAsync(string channel, string actionId, CancellationToken token)
    {
        var configuration = await GetConfigurationAsync(channel, token);
        var profile = configuration.Profiles!.First(item => item.ProfileId == configuration.ActiveProfile);
        var preference = profile.Commands.FirstOrDefault(item => string.Equals(item.ActionId, PredictionCompatibility.ActionId(actionId), StringComparison.Ordinal));
        return profile.ExtensionEnabled && (preference?.Enabled ?? true);
    }

    private static ChannelConfiguration Normalize(ChannelConfiguration configuration)
    {
        var active = configuration.ActiveProfile is >= 1 and <= 3 ? configuration.ActiveProfile : 1;
        var profiles = configuration.Profiles?.ToList() ?? [];
        for (var id = 1; id <= 3; id++) if (profiles.All(profile => profile.ProfileId != id)) profiles.Add(new(id, id == active ? configuration.ExtensionEnabled : true, id == active ? configuration.Commands : []));
        profiles = profiles.Select(profile => profile with { Commands = PredictionCompatibility.Commands(profile.Commands) }).ToList();
        var selected = profiles.First(profile => profile.ProfileId == active);
        return configuration with { SchemaVersion = 2, ActiveProfile = active, Profiles = profiles.OrderBy(profile => profile.ProfileId).ToArray(), ExtensionEnabled = selected.ExtensionEnabled, Commands = selected.Commands };
    }

    public async Task<IReadOnlyList<InstallationSummary>> ListInstallationsAsync(string channel, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand("SELECT installation_id,created_at,last_seen_at,revoked_at FROM installations WHERE channel_id=$1 ORDER BY created_at DESC"); command.Parameters.AddWithValue(channel);
        await using var reader = await command.ExecuteReaderAsync(token); var result = new List<InstallationSummary>();
        while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetFieldValue<DateTimeOffset>(1), reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2), reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3)));
        return result;
    }

    public async Task<bool> RevokeInstallationAsync(string channel, Guid installationId, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand("UPDATE installations SET revoked_at=now() WHERE channel_id=$1 AND installation_id=$2 AND revoked_at IS NULL"); command.Parameters.AddWithValue(channel); command.Parameters.AddWithValue(installationId);
        return await command.ExecuteNonQueryAsync(token) == 1;
    }

    public async Task AuditAsync(Guid requestId, string channel, string user, string action, string status, string? detail, CancellationToken token)
    {
        await using var command = dataSource.CreateCommand("INSERT INTO action_audit(request_id,channel_id,user_id,action_id,status,detail,created_at) VALUES($1,$2,$3,$4,$5,$6,now()) ON CONFLICT(request_id) DO NOTHING");
        command.Parameters.AddWithValue(requestId); command.Parameters.AddWithValue(channel); command.Parameters.AddWithValue(user);
        command.Parameters.AddWithValue(action); command.Parameters.AddWithValue(status); command.Parameters.AddWithValue((object?)detail ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(token);
    }
}
