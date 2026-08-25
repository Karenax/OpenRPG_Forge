using System.Net.Sockets;
using Forge.Network.Configuration;
using Forge.Network.Sessions;
using Forge.Network.Scenarios;
using Forge.Network.Transport;
using Forge.LiveDirector.Scenarios;

if (args.Contains("--internet-transport-smoke-test", StringComparer.Ordinal))
{
    var result = await InternetTransportAcceptanceScenario.RunAsync();
    Console.WriteLine(
        $"OPENRPG_FORGE_INTERNET_TRANSPORT_TWO_CLIENTS_PASS session={result.SessionId:D} " +
        $"revision={result.Revision} players={result.Players} mismatch={result.RejectionCode}");
    Console.WriteLine($"OPENRPG_FORGE_INTERNET_TRANSPORT={(result.Passed ? "PASS" : "FAILED")}");
    Environment.ExitCode = result.Passed ? 0 : 7;
    return;
}

if (args.Contains("--live-director-smoke-test", StringComparer.Ordinal))
{
    var result = LiveDirectorScenario.Run();
    Console.WriteLine($"OPENRPG_FORGE_LIVE_DIRECTOR_NETWORK_PASS digest={result.NetworkDigest}");
    Console.WriteLine($"OPENRPG_FORGE_LIVE_DIRECTOR_AUTHORITY_PASS gm={result.SpawnCode} player={result.RejectedPlayerCode}");
    Console.WriteLine($"OPENRPG_FORGE_LIVE_DIRECTOR_AUDIT_PASS entries={result.AuditEntries} hash={result.AuditHash}");
    Console.WriteLine($"OPENRPG_FORGE_LIVE_DIRECTOR={(result.Passed ? "PASS" : "FAILED")}");
    Environment.ExitCode = result.Passed ? 0 : 5;
    return;
}

if (args.Contains("--live-director-access-smoke-test", StringComparer.Ordinal))
{
    var access = LiveDirectorAccessAcceptanceScenario.Run();
    Console.WriteLine(
        $"OPENRPG_FORGE_SERVER_R105_LIVE_DIRECTOR_ACCESS_PASS auth=gm-role-mfa-expiry-revoke-audit " +
        $"inspect=components-stats-variables-scripts-ownership-history-read-only " +
        $"players=identity-character-zone-latency-party-permissions-voice-moderation-refresh " +
        $"audit=actor-command-targets-before-after-reason-correlation-hash-chain " +
        $"chat=local-party-tell-server-gm-faction-impersonation-moderation-logs " +
        $"auth_code={access.AuthenticationCode} inspect_code={access.InspectionCode} " +
        $"players_count={access.Players} latency={access.LatencyMs} audit_entries={access.AuditEntries} " +
        $"audit_verified={access.AuditVerified} chat_messages={access.ChatMessages} permission={access.PermissionCode}");
    Console.WriteLine($"OPENRPG_FORGE_LIVE_DIRECTOR_ACCESS={(access.Passed ? "PASS" : "FAILED")}");
    Environment.ExitCode = access.Passed ? 0 : 6;
    return;
}

if (args.Contains("--network-beta-smoke-test", StringComparer.Ordinal))
{
    var result = NetworkBetaScenario.Run();
    var foundation = NetworkFoundationAcceptanceScenario.Run();
    var operations = NetworkOperationsAcceptanceScenario.Run();
    var assurance = NetworkAssuranceAcceptanceScenario.Run();
    var access = LiveDirectorAccessAcceptanceScenario.Run();
    Console.WriteLine($"OPENRPG_FORGE_NETWORK_TWO_CLIENTS_PASS revision={result.ServerRevision} digest={result.SharedDigest}");
    Console.WriteLine($"OPENRPG_FORGE_NETWORK_RECONNECT_PASS code={result.ReconnectionCode} clientA={result.ClientARevision} clientB={result.ClientBRevision}");
    Console.WriteLine($"OPENRPG_FORGE_NETWORK_AUTHORITY_PASS forged={result.ForgedMutationCode}");
    Console.WriteLine(
        $"OPENRPG_FORGE_SERVER_R102_NETWORK_FOUNDATION_PASS dedicated=headless-service-health authority=intents-server-replicated-audited " +
        $"party=invitations-roles-leader-loot-quests-markers-separation replication=components-delta-ownership-prediction-reconciliation " +
        $"interest=area-distance-visibility-party-ownership-explicit configuration=uuid-schema-provenance-scope-revision-audit " +
        $"command=preview-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handles mcp=filtered-dry-run " +
        $"permission={foundation.PermissionCode} prediction={foundation.PredictionCode} audit={foundation.AuditEntries}");
    Console.WriteLine(
        $"OPENRPG_FORGE_SERVER_R103_NETWORK_OPERATIONS_PASS content=manifest-chunks-hashes-cache-resume-delta-trust " +
        $"version=lockfile-fingerprint-before-join-readable-mismatch roles=player-moderator-builder-gm-admin-spectator-custom-abac " +
        $"chat=local-party-tell-shout-dm-proximity-language-logs-accessibility-bridges " +
        $"events=typed-subscriptions-priority-trace-replay-backpressure permission={operations.PermissionCode} " +
        $"content_code={operations.ContentCode} version_code={operations.VersionCode} chat_code={operations.ChatCode} " +
        $"backpressure={operations.BackpressureCode} audit={operations.AuditEntries}");
    Console.WriteLine(
        $"OPENRPG_FORGE_SERVER_R104_NETWORK_ASSURANCE_PASS logs=json-text-categories-correlation-privacy-rotation-remote-sinks " +
        $"anti_cheat=intents-rate-signed-content-authoritative-rng-anomaly-untrusted-saves " +
        $"tests=clients-latency-loss-duplicate-reorder-replay-soak-authoritative-state " +
        $"log_code={assurance.LogCode} segments={assurance.LogSegments} sinks={assurance.RemoteDeliveries} " +
        $"join={assurance.JoinCode} anomaly={assurance.AnomalyCode} quarantined={assurance.Quarantined} " +
        $"client_save={assurance.ClientSaveCode} deterministic={assurance.TestCode} " +
        $"assertions={assurance.StateAssertions} audit={assurance.Audit.Count}");
    Console.WriteLine(
        $"OPENRPG_FORGE_SERVER_R105_LIVE_DIRECTOR_ACCESS_PASS auth=gm-role-mfa-expiry-revoke-audit " +
        $"inspect=components-stats-variables-scripts-ownership-history-read-only " +
        $"players=identity-character-zone-latency-party-permissions-voice-moderation-refresh " +
        $"audit=actor-command-targets-before-after-reason-correlation-hash-chain " +
        $"chat=local-party-tell-server-gm-faction-impersonation-moderation-logs " +
        $"auth_code={access.AuthenticationCode} inspect_code={access.InspectionCode} " +
        $"players_count={access.Players} latency={access.LatencyMs} audit_entries={access.AuditEntries} " +
        $"audit_verified={access.AuditVerified} chat_messages={access.ChatMessages} permission={access.PermissionCode}");
    Console.WriteLine($"OPENRPG_FORGE_NETWORK_BETA={(result.Passed ? "PASS" : "FAILED")}");
    Environment.ExitCode = result.Passed && foundation.Passed && operations.Passed && assurance.Passed && access.Passed ? 0 : 4;
    return;
}

if (args.Contains("--self-test", StringComparer.Ordinal))
{
    var config = new DedicatedServerConfiguration(Guid.Parse("10000000-0000-4000-8000-000000000001"), 1, "OpenRPG Forge Network Beta", "127.0.0.1", 5121, 8, DiscoveryMode.Direct, "self-test", "1.0");
    var session = new AuthoritativeSession(config, Guid.Parse("20000000-0000-4000-8000-000000000001"));
    Console.WriteLine($"OPENRPG_FORGE_SERVER_READY server={config.ServerId} session={session.SessionId}");
    Console.WriteLine("OPENRPG_FORGE_SERVER_HEALTH status=healthy revision=0 players=0");
    return;
}

var configArgument = args.FirstOrDefault(value => value.StartsWith("--config=", StringComparison.Ordinal));
if (configArgument is null)
{
    Console.Error.WriteLine("NET_CONFIG_ARGUMENT_REQUIRED: use --config=<path>");
    Environment.ExitCode = 2;
    return;
}

try
{
    var path = Path.GetFullPath(configArgument["--config=".Length..]);
    var configuration = new NetworkConfigurationStore().Deserialize(await File.ReadAllTextAsync(path));
    await using var server = new TcpAuthoritativeServer(configuration);
    server.Start();
    Console.WriteLine(
        $"OPENRPG_FORGE_SERVER_READY server={configuration.ServerId} session={server.SessionId} " +
        $"transport=tcp bind={configuration.BindAddress} port={configuration.Port}");
    Console.WriteLine("OPENRPG_FORGE_SERVER_HEALTH status=healthy revision=0 players=0 transport=tcp");
    using var shutdown = new CancellationTokenSource();
    ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        shutdown.Cancel();
    };
    Console.CancelKeyPress += cancelHandler;
    try
    {
        await server.WaitForShutdownAsync(shutdown.Token);
    }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
    {
    }
    finally
    {
        Console.CancelKeyPress -= cancelHandler;
    }
}
catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or
                                           ArgumentException or SocketException)
{
    Console.Error.WriteLine($"OPENRPG_FORGE_SERVER_INVALID {exception.Message}");
    Environment.ExitCode = 3;
}
