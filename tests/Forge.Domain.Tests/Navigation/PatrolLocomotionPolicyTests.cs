using Forge.Domain.Navigation;

namespace Forge.Domain.Tests.Navigation;

public sealed class PatrolLocomotionPolicyTests
{
    [Theory]
    [InlineData(1.4, true)]
    [InlineData(2.39, true)]
    [InlineData(2.4, false)]
    [InlineData(3.2, false)]
    public void DistinguishesWalkingPaceFromRunPace(double speed, bool expected)
    {
        Assert.Equal(expected, PatrolLocomotionPolicy.IsWalkingPace(speed));
    }
}
