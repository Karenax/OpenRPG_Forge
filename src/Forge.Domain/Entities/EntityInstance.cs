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

    public string BlueprintVersionRequirement { get; }

    public EntityInstance(
        EntityId id,
        BlueprintId blueprintId,
        TransformValue transform,
        IReadOnlyList<string>? tags = null,
        EntityPropertyOverrides? overrides = null,
        string blueprintVersionRequirement = "*")
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(blueprintId);
        ArgumentNullException.ThrowIfNull(transform);

        Id = id;
        BlueprintId = blueprintId;
        Transform = transform;
        Tags = (tags ?? []).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Overrides = overrides ?? EntityPropertyOverrides.Empty;
        if (!BlueprintVersionConstraint.IsValid(blueprintVersionRequirement))
            throw new ArgumentException("Blueprint version requirement must be '*', an exact semantic version or a supported range.", nameof(blueprintVersionRequirement));
        BlueprintVersionRequirement = blueprintVersionRequirement.Trim();
    }
}
