using Forge.Domain.RuntimeExtensions;
using Forge.Network.Testing;

namespace Forge.Network.Tests;

public sealed class AdverseNetworkQualityServiceTests
{
    [Fact]
    public void Adverse_suite_converges_and_rejects_mismatch_and_authority_attacks_deterministically()
    {
        var profile = QualityAssuranceProfile.CreateDefault(
            Guid.Parse("2f790000-0000-4000-8000-000000000002")).Network;

        var first = AdverseNetworkQualityService.Run(profile);
        var second = AdverseNetworkQualityService.Run(profile);

        Assert.True(first.Passed);
        Assert.Equal(9, first.Scenarios.Count);
        Assert.All(first.Scenarios, scenario => Assert.True(scenario.Converged));
        Assert.Contains(first.Scenarios, scenario => scenario.LossPercentage > 0 && scenario.DroppedPackets > 0);
        Assert.True(first.ReconnectPassed);
        Assert.True(first.ContentMismatchPassed);
        Assert.True(first.AuthorityAttackPassed);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.Scenarios.Select(item => item.Digest), second.Scenarios.Select(item => item.Digest));
    }
}
