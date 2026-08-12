using Forge.Domain.Assets;
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

public sealed record PrefabCompositionNode(
    Guid Id,
    BlueprintId BlueprintId,
    Guid? ParentNodeId,
    string? Socket,
    TransformValue Transform);

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
