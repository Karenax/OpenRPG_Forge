using Forge.Network.Scenarios;

namespace Forge.Network.Tests;

public sealed class NetworkBetaScenarioTests
{
    [Fact]
    public void Two_clients_share_progress_reconnect_and_cannot_forge_authority()
    {
        var result = NetworkBetaScenario.Run();
        Assert.True(result.Passed);
        Assert.Equal(result.ServerRevision, result.ClientARevision);
        Assert.Equal(result.ClientARevision, result.ClientBRevision);
        Assert.Equal("NET_INTENT_FORBIDDEN", result.ForgedMutationCode);
        Assert.Equal("NET_RECONNECT_ACCEPTED", result.ReconnectionCode);
        Assert.Contains(result.Audit, x => x.StartsWith("rejected:NET_INTENT_FORBIDDEN", StringComparison.Ordinal));
    }

    [Fact]
    public void Scenario_digest_is_deterministic()
    {
        Assert.Equal(NetworkBetaScenario.Run().SharedDigest, NetworkBetaScenario.Run().SharedDigest);
    }
}
