using Forge.Domain.Spatial;

namespace Forge.Application.Sequences;

public static class AnimationTweenRuntime
{
    public static AnimationTweenState Start(AnimationTweenDefinition definition) => new(definition.Id, 0,
        definition.FromTransform, definition.FromValue, true, false, false);

    public static AnimationTweenState Advance(AnimationTweenDefinition definition, AnimationTweenState state,
        int deltaMilliseconds, bool hasAuthority)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(state);
        if (deltaMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaMilliseconds));
        if (!state.Active || state.DefinitionId != definition.Id ||
            definition.Authority == TransformAuthority.Server && !hasAuthority) return state;
        var elapsed = Math.Min(definition.DurationMilliseconds, checked(state.ElapsedMilliseconds + deltaMilliseconds));
        var progress = elapsed / (double)definition.DurationMilliseconds;
        var eased = TransformInterpolator.Ease(progress, definition.Easing);
        var transform = definition.Channel == AnimationChannelKind.Transform
            ? TransformInterpolator.Sample(definition.FromTransform!, definition.ToTransform!, progress,
                definition.Easing)
            : null;
        var value = definition.Channel switch
        {
            AnimationChannelKind.MaterialParameter => new AnimationTweenValue(
                Lerp(definition.FromValue.X, definition.ToValue.X, eased),
                Lerp(definition.FromValue.Y, definition.ToValue.Y, eased),
                Lerp(definition.FromValue.Z, definition.ToValue.Z, eased),
                Lerp(definition.FromValue.W, definition.ToValue.W, eased), string.Empty),
            AnimationChannelKind.State => elapsed == definition.DurationMilliseconds
                ? definition.ToValue : definition.FromValue,
            _ => definition.FromValue,
        };
        var complete = elapsed == definition.DurationMilliseconds;
        return new AnimationTweenState(definition.Id, elapsed, transform, value, !complete, false, complete);
    }

    public static AnimationTweenState Cancel(AnimationTweenState state) =>
        state with { Active = false, Cancelled = true, Completed = false };

    private static float Lerp(float from, float to, double progress) =>
        (float)(from + ((to - from) * progress));
}
