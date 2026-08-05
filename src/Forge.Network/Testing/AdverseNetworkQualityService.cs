using System.Security.Cryptography;
using System.Text.Json;
using Forge.Domain.RuntimeExtensions;
using Forge.Network.Sessions;

namespace Forge.Network.Testing;

public sealed record AdverseNetworkScenarioResult(
    int LatencyTicks,
    int LossPercentage,
    int Clients,
    int SentPackets,
    int DroppedPackets,
    int DuplicatePackets,
    bool Converged,
    string Digest);

public sealed record AdverseNetworkQualityReport(
    bool Passed,
    string Fingerprint,
    IReadOnlyList<AdverseNetworkScenarioResult> Scenarios,
    bool ReconnectPassed,
    bool ContentMismatchPassed,
    bool AuthorityAttackPassed,
    IReadOnlyList<string> Diagnostics);

public static class AdverseNetworkQualityService
{
    private static readonly Guid ServerId = Guid.Parse("5a140000-0000-4000-8000-000000000001");
    private static readonly Guid SessionId = Guid.Parse("5a140000-0000-4000-8000-000000000002");
    private const string Fingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public static AdverseNetworkQualityReport Run(NetworkTestSuiteProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var scenarios = new List<AdverseNetworkScenarioResult>();
        foreach (var latency in profile.LatencyTicks)
            foreach (var loss in profile.LossPercentages)
            {
                var dropEvery = loss == 0 ? 0 : Math.Max(2, (int)Math.Round(100d / loss));
                var harness = new DeterministicNetworkHarness(new NetworkFaultProfile(latency, dropEvery, 7, true));
                for (var sequence = 0; sequence < 12; sequence++)
                    for (var client = 0; client < profile.ClientCount; client++)
                        harness.Send($"client-{client}", "server", $"intent:{client}:{sequence}");

                for (var client = 0; client < profile.ClientCount; client++)
                    for (var retry = 0; retry < Math.Max(4, dropEvery + 1); retry++)
                        harness.Send("server", $"client-{client}", "state:final");

                harness.Advance(latency + 1);
                var converged = Enumerable.Range(0, profile.ClientCount).All(client =>
                    harness.Received($"client-{client}").Contains("state:final", StringComparer.Ordinal));
                scenarios.Add(new(latency, loss, profile.ClientCount, harness.Sent, harness.Dropped,
                    harness.Duplicated, converged && harness.Pending == 0, harness.Digest()));
            }

        var reconnect = !profile.TestReconnect || VerifyReconnect();
        var mismatch = !profile.TestContentMismatch || VerifyContentMismatch();
        var authority = !profile.TestAuthorityAttacks || VerifyAuthorityAttack();
        var diagnostics = new List<string>();
        if (scenarios.Any(item => !item.Converged)) diagnostics.Add("NET_QA_CONVERGENCE_FAILED");
        if (!reconnect) diagnostics.Add("NET_QA_RECONNECT_FAILED");
        if (!mismatch) diagnostics.Add("NET_QA_CONTENT_MISMATCH_FAILED");
        if (!authority) diagnostics.Add("NET_QA_AUTHORITY_ATTACK_FAILED");
        var passed = diagnostics.Count == 0;
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            passed,
            scenarios = scenarios.Select(item => new
            {
                item.LatencyTicks,
                item.LossPercentage,
                item.Clients,
                item.SentPackets,
                item.DroppedPackets,
                item.DuplicatePackets,
                item.Converged,
                item.Digest,
            }),
            reconnect,
            mismatch,
            authority,
            diagnostics,
        })));
        return new(passed, fingerprint, scenarios, reconnect, mismatch, authority, diagnostics);
    }

    private static bool VerifyReconnect()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 5, 20, 0, 0, TimeSpan.Zero));
        var reconnect = new ReconnectionService(clock, TimeSpan.FromMinutes(2));
        var account = Guid.Parse("5a140000-0000-4000-8000-000000000010");
        var ticket = reconnect.Issue(account, Guid.Parse("5a140000-0000-4000-8000-000000000011"), SessionId, 42);
        var accepted = reconnect.Redeem(ticket.Token, account, SessionId);
        return accepted.Accepted && accepted.Ticket?.LastAcknowledgedRevision == 42;
    }

    private static bool VerifyContentMismatch()
    {
        var session = CreateSession();
        var decision = session.Connect(Guid.Parse("5a140000-0000-4000-8000-000000000020"),
            new SessionIdentity(Guid.Parse("5a140000-0000-4000-8000-000000000021"), "Mismatch", SessionRole.Player),
            new string('b', 64), "1");
        return !decision.Accepted && decision.Code == "NET_CONTENT_MISMATCH" && decision.Revision == 0;
    }

    private static bool VerifyAuthorityAttack()
    {
        var session = CreateSession();
        var connection = Guid.Parse("5a140000-0000-4000-8000-000000000030");
        var connected = session.Connect(connection,
            new SessionIdentity(Guid.Parse("5a140000-0000-4000-8000-000000000031"), "Attacker", SessionRole.Player),
            Fingerprint, "1");
        var attack = session.Apply(new ClientIntent(Guid.Parse("5a140000-0000-4000-8000-000000000032"),
            connection, connected.Revision, "world.set-state", new Dictionary<string, string>
            {
                ["gold"] = "999999",
                ["quest"] = "completed",
            }));
        return connected.Accepted && !attack.Accepted && attack.Code == "NET_INTENT_FORBIDDEN";
    }

    private static AuthoritativeSession CreateSession() => new(new DedicatedServerConfiguration(ServerId, 1,
        "OpenRPG Forge QA", "127.0.0.1", 5121, 64, DiscoveryMode.Direct, Fingerprint, "1"), SessionId);

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private readonly DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
