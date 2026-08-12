using Forge.Domain.Quality;
using Forge.Network.Authoring;
using Forge.Network.Observability;
using Forge.Network.Operations;
using Forge.Network.Replication;
using Forge.Network.Security;
using Forge.Network.Testing;

namespace Forge.Network.Scenarios;

public sealed record NetworkAssuranceAcceptanceResult(bool Passed, string LogCode, int LogSegments,
    int RemoteDeliveries, string JoinCode, string AnomalyCode, bool Quarantined,
    string ClientSaveCode, int RandomValue, string TestCode, int StateAssertions,
    string Fingerprint, IReadOnlyList<string> Audit);

public static class NetworkAssuranceAcceptanceScenario
{
    public static NetworkAssuranceAcceptanceResult Run()
    {
        var profile = NetworkAssuranceProfile.CreateDefault("scenario:r104");
        var sink = new MemoryRemoteLogSink("audit");
        var runtime = NetworkAssuranceRuntimeAdapter.Build(profile, TimeProvider.System, [sink]);
        var context = new StructuredLogContext("security", profile.AntiCheat.Id, 104,
            Guid.Parse("a1040000-0000-4000-8000-000000000200"), "player-elyra");
        runtime.Logs.Write(StructuredLogSeverity.Information, "network.assurance.started",
            Guid.Parse("a1040000-0000-4000-8000-000000000203"), context,
            new Dictionary<string, object?> { ["phase"] = "preflight" });
        var log = runtime.Logs.Write(StructuredLogSeverity.Warning, "network.intent.rejected",
            Guid.Parse("a1040000-0000-4000-8000-000000000201"), context,
            new Dictionary<string, object?> { ["accessToken"] = "never-log", ["detail"] = new string('x', 5000) });

        var connection = Guid.Parse("a1040000-0000-4000-8000-000000000202");
        var join = runtime.AntiCheat.ValidateJoin(connection,
            new(new string('b', 64), new string('a', 64), true));
        AntiCheatDecision anomaly = new(true, "not-run", 0, false, string.Empty);
        for (var sequence = 1; sequence <= 4; sequence++)
            anomaly = runtime.AntiCheat.ValidateIntent(new(Guid.NewGuid(), connection, Guid.NewGuid(), sequence,
                42, new(0, 0, 0), new(100, 0, 0), TimeSpan.FromSeconds(1)), 42,
                DateTimeOffset.UnixEpoch.AddMilliseconds(sequence));
        var clientSave = runtime.AntiCheat.ValidateClientSave(connection, "client-save"u8);
        var random = runtime.AntiCheat.NextAuthoritativeRandom(1, 21);
        var repeatRandom = new AntiCheatService(profile.AntiCheat).NextAuthoritativeRandom(1, 21);
        var deterministic = DeterministicNetworkTestRunner.Run(runtime.DeterministicTests);

        var history = new NetworkAssuranceHistory(profile);
        var candidate = profile with { ServerRevision = 1, Logs = profile.Logs with { RotationFiles = 5 } };
        var preview = history.Execute(new(Guid.NewGuid(), 1, "scenario:r104", 0, "server:qa",
            NetworkRole.Administrator, false, candidate));
        var applied = history.Execute(new(Guid.NewGuid(), 1, "scenario:r104", 0, "server:qa",
            NetworkRole.Administrator, true, candidate));
        var denied = history.Execute(new(Guid.NewGuid(), 1, "scenario:r104", 1, "client:player",
            NetworkRole.Player, true, candidate with { ServerRevision = 2 }));
        var audit = new[] { preview.AuditEntry, applied.AuditEntry, denied.AuditEntry,
            join.AuditEntry, anomaly.AuditEntry, clientSave.AuditEntry };

        var secretProtected = log.Json is not null && !log.Json.Contains("never-log", StringComparison.Ordinal) &&
            log.Json.Contains("[REDACTED]", StringComparison.Ordinal);
        var passed = secretProtected && log.Text is not null && runtime.Logs.SegmentCount >= 2 &&
            sink.Entries.Count == 2 && join.Allowed && anomaly.Quarantined && !clientSave.Allowed &&
            random == repeatRandom && deterministic.Passed && preview.Succeeded && !preview.Applied &&
            applied.Applied && denied.Diagnostics.Any(item => item.Code == "NET_ASSURANCE_PERMISSION_DENIED") &&
            audit.All(item => !string.IsNullOrWhiteSpace(item));
        return new(passed, secretProtected ? "NET_LOG_ROUTED" : "NET_LOG_PRIVACY_FAILED",
            runtime.Logs.SegmentCount, sink.Entries.Count, join.Code, anomaly.Code, anomaly.Quarantined,
            clientSave.Code, random, deterministic.Code, deterministic.StateAssertions,
            deterministic.Fingerprint, audit);
    }
}
