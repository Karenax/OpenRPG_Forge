using Forge.Application.Areas;
using Forge.Domain.Areas;

namespace Forge.Application.Tests.Areas;

public sealed class WeatherForgeScriptDispatchServiceTests
{
    [Fact]
    public void DispatchesTypedWeatherTransitionToRuntime()
    {
        var profile = WeatherProfile.CreateDefault(AreaEnvironmentClass.Exterior);
        var initial = WeatherRuntimeStateMachine.CreateInitial(profile);
        var current = WeatherRuntimeStateMachine.Resolve(profile, initial);
        var advanced = WeatherRuntimeStateMachine.Advance(profile, initial, current.MaximumDurationSeconds, true);
        var runtime = new CapturingRuntime();

        var result = WeatherForgeScriptDispatchService.Dispatch(
            profile, advanced.State, advanced.Transition!, runtime);

        Assert.True(result.Dispatched);
        Assert.Equal("weather.changed", result.Event.EventName);
        Assert.Equal(advanced.State.ServerRevision, result.Event.ServerRevision);
        Assert.Equal(advanced.Transition!.GameplayEffect, result.Event.GameplayEffect);
        Assert.Equal(result.Event, runtime.Event);
    }

    private sealed class CapturingRuntime : IWeatherForgeScriptRuntime
    {
        public WeatherForgeScriptEvent? Event { get; private set; }

        public WeatherForgeScriptDispatchResult Dispatch(WeatherForgeScriptEvent @event)
        {
            Event = @event;
            return new(true, "WEATHER_SCRIPT_DISPATCHED", @event);
        }
    }
}
