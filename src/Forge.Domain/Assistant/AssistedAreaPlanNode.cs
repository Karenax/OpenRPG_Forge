using Forge.Domain.Spatial;

namespace Forge.Domain.Assistant;

public enum AssistedAreaPlacementMode
{
    TerrainSurface,
    ExplicitTransform,
}

public sealed record AssistedAreaPlanNode
{
    public Guid Id { get; }
    public int SchemaVersion { get; }
    public AssistedAreaPlanNodeKind Kind { get; }
    public string Label { get; }
    public float X { get; }
    public float Z { get; }
    public float Width { get; }
    public float Depth { get; }
    public IReadOnlyList<Guid> ContentBlueprintIds { get; }
    public IReadOnlyList<string> Tags { get; }
    public AssistedAreaPlacementMode PlacementMode { get; }
    public TransformValue PlacementTransform { get; }
    public float TerrainOffset { get; }

    public AssistedAreaPlanNode(
        Guid id,
        int schemaVersion,
        AssistedAreaPlanNodeKind kind,
        string label,
        float x,
        float z,
        float width,
        float depth,
        IReadOnlyList<Guid>? contentBlueprintIds,
        IReadOnlyList<string>? tags,
        AssistedAreaPlacementMode placementMode = AssistedAreaPlacementMode.TerrainSurface,
        TransformValue? placementTransform = null,
        float terrainOffset = 0)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Kind = kind;
        Label = (label ?? string.Empty).Trim();
        X = x;
        Z = z;
        Width = width;
        Depth = depth;
        ContentBlueprintIds = (contentBlueprintIds ?? []).Distinct().Order().ToArray();
        Tags = (tags ?? []).Select(item => item.Trim().ToLowerInvariant())
            .Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        PlacementMode = placementMode;
        PlacementTransform = placementTransform ?? new TransformValue(
            new Vector3Value(x, 0, z), QuaternionValue.Identity, Vector3Value.One);
        TerrainOffset = terrainOffset;
    }
}
