using Forge.Application.Areas;
using Forge.Domain.Areas;

namespace Forge.Application.Tests.Areas;

public sealed class WeatherRuntimeStateMachineTests
{
    [Fact]
    public void AdvancesDeterministicallyWithAuthorityAndProducesPortableSnapshot()
    {
        var profile = WeatherProfile.CreateDefault(AreaEnvironmentClass.Exterior);
        var initial = WeatherRuntimeStateMachine.CreateInitial(profile);
        var current = WeatherRuntimeStateMachine.Resolve(profile, initial);

        var denied = WeatherRuntimeStateMachine.Advance(profile, initial, current.MaximumDurationSeconds, false);
        var first = WeatherRuntimeStateMachine.Advance(profile, initial, current.MaximumDurationSeconds, true);
        var second = WeatherRuntimeStateMachine.Advance(profile, initial, current.MaximumDurationSeconds, true);

        Assert.True(denied.AuthorityDenied);
        Assert.Equal(initial, denied.State);
        Assert.NotNull(first.Transition);
        Assert.Equal(first, second);
        Assert.Equal(1, first.State.Revision);
        Assert.Equal(1, first.State.ServerRevision);
        Assert.True(first.Transition!.Persist);
        Assert.True(first.Transition.Replicate);
        Assert.Equal("surface.wet", first.Transition.GameplayEffect);
    }
}
