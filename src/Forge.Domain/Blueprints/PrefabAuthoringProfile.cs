using Forge.Domain.Assets;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Domain.Blueprints;

public enum PrefabVariantOverrideKind
{
    Component,
    Material,
    Stat,
    Script,
}

public sealed record PrefabVariantOverride(PrefabVariantOverrideKind Kind, string Key, string Value);

public sealed record PrefabVariantDefinition(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    BlueprintId BaseBlueprintId,
    IReadOnlyList<PrefabVariantOverride> Overrides,
    bool RequiredAtRuntime = false)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed class PrefabCompositionNode : IEquatable<PrefabCompositionNode>
{
    public Guid Id { get; }
    public BlueprintId BlueprintId { get; }
    public Guid? ParentNodeId { get; }
    public string? Socket { get; }
    public TransformValue Transform { get; }
    public IReadOnlyList<string> Tags { get; }
    public EntityPropertyOverrides Overrides { get; }

    public PrefabCompositionNode(
        Guid id,
        BlueprintId blueprintId,
        Guid? parentNodeId,
        string? socket,
        TransformValue transform,
        IReadOnlyList<string>? tags = null,
        EntityPropertyOverrides? overrides = null)
    {
        ArgumentNullException.ThrowIfNull(blueprintId);
        ArgumentNullException.ThrowIfNull(transform);

        Id = id;
        BlueprintId = blueprintId;
        ParentNodeId = parentNodeId;
        Socket = string.IsNullOrWhiteSpace(socket) ? null : socket.Trim();
        Transform = transform;
        Tags = (tags ?? [])
            .Select(item => (item ?? string.Empty).Trim().ToLowerInvariant())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Overrides = overrides ?? EntityPropertyOverrides.Empty;
    }

    public bool Equals(PrefabCompositionNode? other) => other is not null &&
        Id == other.Id &&
        BlueprintId == other.BlueprintId &&
        ParentNodeId == other.ParentNodeId &&
        string.Equals(Socket, other.Socket, StringComparison.Ordinal) &&
        Transform == other.Transform &&
        Tags.SequenceEqual(other.Tags, StringComparer.Ordinal) &&
        Overrides.ContentEquals(other.Overrides);

    public override bool Equals(object? obj) => Equals(obj as PrefabCompositionNode);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(BlueprintId);
        hash.Add(ParentNodeId);
        hash.Add(Socket, StringComparer.Ordinal);
        hash.Add(Transform);
        foreach (var tag in Tags) hash.Add(tag, StringComparer.Ordinal);
        foreach (var property in Overrides.Properties) hash.Add(property);
        return hash.ToHashCode();
    }
}

public sealed record PrefabCompositionDefinition(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<PrefabCompositionNode> Nodes,
    IReadOnlyList<string> AssemblyRules,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<string> AssemblyScripts,
    bool RequiredAtRuntime = false)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record AssetBudgetMetrics(
    int TriangleCount,
    int MaterialCount,
    int TextureCount,
    long MemoryBytes,
    int AnimationCount,
    int LodLevelCount,
    int CollisionShapeCount);

public sealed record AssetPlatformBudget(
    AssetTargetPlatform Platform,
    int MaximumTriangles,
    int MaximumMaterials,
    int MaximumTextures,
    long MaximumMemoryBytes,
    int MaximumAnimations,
    int MaximumLodLevels,
    int MaximumCollisionShapes,
    bool GateBuild = true);

public sealed record BlueprintAssetBudgetProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    AssetBudgetMetrics Metrics,
    IReadOnlyList<AssetPlatformBudget> Platforms,
    bool RequiredAtRuntime = false)
{
    public const int CurrentSchemaVersion = 1;
}
