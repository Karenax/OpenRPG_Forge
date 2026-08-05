using Forge.Domain.Entities;
using Forge.Domain.Sequences;

namespace Forge.Domain.Spatial;

public sealed record TransformOverrideComponent
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public EntityId EntityId { get; }
    public EntityId? ParentEntityId { get; }
    public TransformValue From { get; }
    public TransformValue To { get; }
    public int DurationMilliseconds { get; }
    public SequenceEasing Easing { get; }
    public TransformAuthority Authority { get; }
    public TransformSaveBehavior SaveBehavior { get; }
    public bool Replicate { get; }
    public string Provenance { get; }

    public TransformOverrideComponent(Guid id, int schemaVersion, EntityId entityId, EntityId? parentEntityId,
        TransformValue from, TransformValue to, int durationMilliseconds, SequenceEasing easing,
        TransformAuthority authority, TransformSaveBehavior saveBehavior, bool replicate, string provenance)
    {
        ArgumentNullException.ThrowIfNull(entityId);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || parentEntityId == entityId ||
            durationMilliseconds is < 1 or > 86_400_000 || replicate && authority == TransformAuthority.Local)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Transform override component is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        EntityId = entityId;
        ParentEntityId = parentEntityId;
        From = from;
        To = to;
        DurationMilliseconds = durationMilliseconds;
        Easing = easing;
        Authority = authority;
        SaveBehavior = saveBehavior;
        Replicate = replicate;
        Provenance = provenance.Trim();
    }
}
