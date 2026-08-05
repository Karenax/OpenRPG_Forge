using Forge.Domain.Spatial;

namespace Forge.Domain.Sequences;

public sealed record SequenceKeyframe
{
    public Guid Id { get; }
    public int TimeMilliseconds { get; }
    public TransformValue? Transform { get; }
    public string Value { get; }
    public SequenceEasing Easing { get; }

    public SequenceKeyframe(Guid id, int timeMilliseconds, TransformValue? transform, string value,
        SequenceEasing easing)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (id == Guid.Empty || timeMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Sequence keyframe is invalid.");
        }

        Id = id;
        TimeMilliseconds = timeMilliseconds;
        Transform = transform;
        Value = value.Trim();
        Easing = easing;
    }
}
