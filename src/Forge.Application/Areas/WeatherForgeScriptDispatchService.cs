using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed record WeatherForgeScriptEvent(
    string EventName,
    string ApiVersion,
    Guid PreviousStateId,
    Guid CurrentStateId,
    string WeatherKind,
    float Intensity,
    float Humidity,
    float SurfaceWetness,
    bool Lightning,
    string GameplayEffect,
    long ServerRevision);

public sealed record WeatherForgeScriptDispatchResult(bool Dispatched, string Code,
    WeatherForgeScriptEvent Event);

public interface IWeatherForgeScriptRuntime
{
    WeatherForgeScriptDispatchResult Dispatch(WeatherForgeScriptEvent @event);
}

public static class WeatherForgeScriptDispatchService
{
    public static WeatherForgeScriptEvent Build(WeatherProfile profile, WeatherRuntimeSnapshot snapshot,
        WeatherRuntimeTransition transition)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(transition);
        if (snapshot.StateId != transition.CurrentStateId || snapshot.ServerRevision < 1)
            throw new InvalidOperationException("WEATHER_SCRIPT_TRANSITION_MISMATCH");
        var state = WeatherRuntimeStateMachine.Resolve(profile, snapshot);
        return new("weather.changed", "1.0", transition.PreviousStateId, transition.CurrentStateId,
            state.Kind.ToString(), state.Intensity, state.Humidity, state.SurfaceWetness,
            state.Lightning, transition.GameplayEffect, snapshot.ServerRevision);
    }

    public static WeatherForgeScriptDispatchResult Dispatch(WeatherProfile profile,
        WeatherRuntimeSnapshot snapshot, WeatherRuntimeTransition transition,
        IWeatherForgeScriptRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return runtime.Dispatch(Build(profile, snapshot, transition));
    }
}
