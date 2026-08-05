using Forge.Domain.Spatial;

namespace Forge.Application.Sequences;

public static class TransformOverrideRuntime
{
    public static TransformOverrideState Start(TransformOverrideComponent component) =>
        new(component.Id, 0, component.From, true, false, false);

    public static TransformOverrideState Advance(TransformOverrideComponent component, TransformOverrideState state,
        int deltaMilliseconds, bool hasAuthority)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(state);
        if (deltaMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaMilliseconds));
        if (!state.Active || state.ComponentId != component.Id) return state;
        if (component.Authority == TransformAuthority.Server && !hasAuthority) return state;
        var elapsed = Math.Min(component.DurationMilliseconds, checked(state.ElapsedMilliseconds + deltaMilliseconds));
        var transform = TransformInterpolator.Sample(component.From, component.To,
            elapsed / (double)component.DurationMilliseconds, component.Easing);
        var complete = elapsed == component.DurationMilliseconds;
        return new TransformOverrideState(component.Id, elapsed, transform, !complete, false, complete);
    }

    public static TransformOverrideState Cancel(TransformOverrideState state) =>
        state with { Active = false, Cancelled = true, Completed = false };
}
