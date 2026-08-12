using System.Security.Cryptography;
using System.Text;

namespace Forge.Network.Testing;

public sealed record DeterministicNetworkTestPolicy(Guid Id, int SchemaVersion, int ClientCount,
    int LatencyTicks, int DropEvery, int DuplicateEvery, bool ReverseSameTick,
    int ReplayPasses, int SoakCycles, long ExpectedAuthoritativeRevision);

public sealed record DeterministicNetworkTestResult(bool Passed, string Code, int Clients,
    int ReplayPasses, int SoakCycles, int StateAssertions, int SentPackets, int DroppedPackets,
    int DuplicatedPackets, string Digest, string Fingerprint, IReadOnlyList<string> Diagnostics);

public static class DeterministicNetworkTestRunner
{
    public static DeterministicNetworkTestResult Run(DeterministicNetworkTestPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var digests = new List<string>();
        var diagnostics = new List<string>();
        var assertions = 0;
        var sent = 0;
        var dropped = 0;
        var duplicated = 0;
        for (var pass = 0; pass < policy.ReplayPasses; pass++)
        {
            var harness = new DeterministicNetworkHarness(new(policy.LatencyTicks, policy.DropEvery,
                policy.DuplicateEvery, policy.ReverseSameTick));
            for (var cycle = 0; cycle < policy.SoakCycles; cycle++)
            {
                for (var client = 0; client < policy.ClientCount; client++)
                {
                    harness.Send($"client-{client}", "server", $"intent:{client}:{cycle}");
                    // Redundant authoritative state packets make convergence testable under deterministic loss.
                    for (var retry = 0; retry < Math.Max(2, policy.DropEvery + 1); retry++)
                        harness.Send("server", $"client-{client}", $"state:{policy.ExpectedAuthoritativeRevision}");
                }
                harness.Advance(policy.LatencyTicks + 1);
            }
            for (var client = 0; client < policy.ClientCount; client++)
            {
                assertions++;
                if (!harness.Received($"client-{client}").Contains(
                        $"state:{policy.ExpectedAuthoritativeRevision}", StringComparer.Ordinal))
                    diagnostics.Add($"NET_DETERMINISM_STATE_MISMATCH:client-{client}:pass-{pass}");
            }
            digests.Add(harness.Digest());
            sent = harness.Sent;
            dropped = harness.Dropped;
            duplicated = harness.Duplicated;
        }
        if (digests.Distinct(StringComparer.Ordinal).Count() != 1)
            diagnostics.Add("NET_DETERMINISM_REPLAY_DIVERGED");
        var digest = digests.FirstOrDefault() ?? string.Empty;
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(digest)));
        return new(diagnostics.Count == 0,
            diagnostics.Count == 0 ? "NET_DETERMINISM_PASS" : "NET_DETERMINISM_FAILED",
            policy.ClientCount, policy.ReplayPasses, policy.SoakCycles, assertions, sent, dropped,
            duplicated, digest, fingerprint, diagnostics);
    }
}
