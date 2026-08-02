using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;

namespace Forge.Domain.Entities;

public sealed record EntityInstance
{
    public EntityId Id { get; }

    public BlueprintId BlueprintId { get; }

    public TransformValue Transform { get; }

    public IReadOnlyList<string> Tags { get; }

    public EntityPropertyOverrides Overrides { get; }

    public EntityInstance(
        EntityId id,
        BlueprintId blueprintId,
        TransformValue transform,
        IReadOnlyList<string>? tags = null,
        EntityPropertyOverrides? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(blueprintId);
        ArgumentNullException.ThrowIfNull(transform);

        Id = id;
        BlueprintId = blueprintId;
        Transform = transform;
        Tags = (tags ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Overrides = overrides ?? EntityPropertyOverrides.Empty;
    }
}
