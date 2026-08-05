using Forge.Domain.Entities;
using Forge.Domain.Sequences;

namespace Forge.Domain.Spatial;

public sealed record AnimationTweenDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public EntityId EntityId { get; }
    public AnimationChannelKind Channel { get; }
    public string Property { get; }
    public TransformValue? FromTransform { get; }
    public TransformValue? ToTransform { get; }
    public AnimationTweenValue FromValue { get; }
    public AnimationTweenValue ToValue { get; }
    public int DurationMilliseconds { get; }
    public SequenceEasing Easing { get; }
    public TransformAuthority Authority { get; }
    public TransformSaveBehavior SaveBehavior { get; }
    public bool Replicate { get; }
    public string Provenance { get; }

    public AnimationTweenDefinition(Guid id, int schemaVersion, EntityId entityId, AnimationChannelKind channel,
        string property, TransformValue? fromTransform, TransformValue? toTransform,
        AnimationTweenValue fromValue, AnimationTweenValue toValue, int durationMilliseconds,
        SequenceEasing easing, TransformAuthority authority, TransformSaveBehavior saveBehavior, bool replicate,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(entityId);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(fromValue);
        ArgumentNullException.ThrowIfNull(toValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || durationMilliseconds is < 1 or > 86_400_000 ||
            replicate && authority == TransformAuthority.Local ||
            channel == AnimationChannelKind.Transform && (fromTransform is null || toTransform is null) ||
            channel != AnimationChannelKind.Transform && string.IsNullOrWhiteSpace(property) ||
            channel == AnimationChannelKind.State &&
            (string.IsNullOrWhiteSpace(fromValue.Text) || string.IsNullOrWhiteSpace(toValue.Text)))
            throw new ArgumentException("Animation tween definition is invalid.", nameof(channel));
        Id = id; SchemaVersion = schemaVersion; EntityId = entityId; Channel = channel;
        Property = property.Trim(); FromTransform = fromTransform; ToTransform = toTransform;
        FromValue = fromValue; ToValue = toValue; DurationMilliseconds = durationMilliseconds; Easing = easing;
        Authority = authority; SaveBehavior = saveBehavior; Replicate = replicate; Provenance = provenance.Trim();
    }
}
