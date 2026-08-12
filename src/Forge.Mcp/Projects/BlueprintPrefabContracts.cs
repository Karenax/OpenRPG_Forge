using Forge.Application.Commands;

namespace Forge.Mcp.Projects;

public sealed record BlueprintPrefabResource(
    int SchemaVersion,
    long Revision,
    string Query,
    IReadOnlyList<BlueprintPrefabResourceItem> Items);

public sealed record BlueprintPrefabResourceItem(
    string BlueprintId,
    string Slug,
    IReadOnlyList<string> AuthorCollections,
    string? VariantBaseBlueprintId,
    IReadOnlyList<AgentPrefabVariantOverride> VariantOverrides,
    bool VariantRequired,
    IReadOnlyList<AgentPrefabCompositionNode> CompositionNodes,
    IReadOnlyList<string> AssemblyRules,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<string> AssemblyScripts,
    bool CompositionRequired,
    AgentAssetBudgetMetrics? AssetMetrics,
    IReadOnlyList<AgentAssetPlatformBudget> PlatformBudgets,
    bool AssetBudgetRequired);

public sealed record AgentPrefabVariantOverride(string Kind, string Key, string Value);

public sealed record AgentPrefabCompositionNode(
    string NodeId,
    string BlueprintId,
    string? ParentNodeId,
    string? Socket,
    float X,
    float Y,
    float Z);

public sealed record AgentAssetBudgetMetrics(
    int TriangleCount,
    int MaterialCount,
    int TextureCount,
    long MemoryBytes,
    int AnimationCount,
    int LodLevelCount,
    int CollisionShapeCount);

public sealed record AgentAssetPlatformBudget(
    string Platform,
    int MaximumTriangles,
    int MaximumMaterials,
    int MaximumTextures,
    long MaximumMemoryBytes,
    int MaximumAnimations,
    int MaximumLodLevels,
    int MaximumCollisionShapes,
    bool GateBuild);

public sealed record BlueprintPrefabPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string BlueprintId,
    string? VariantBaseBlueprintId,
    IReadOnlyList<AgentPrefabVariantOverride> VariantOverrides,
    bool VariantRequired,
    IReadOnlyList<AgentPrefabCompositionNode> CompositionNodes,
    IReadOnlyList<string> AssemblyRules,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<string> AssemblyScripts,
    bool CompositionRequired,
    AgentAssetBudgetMetrics? AssetMetrics,
    IReadOnlyList<AgentAssetPlatformBudget> PlatformBudgets,
    bool AssetBudgetRequired,
    IReadOnlyList<string> AuthorCollections);

public sealed record BlueprintPrefabPlanResult(
    bool Succeeded,
    string? PlanId,
    long BaseRevision,
    BlueprintPrefabResourceItem? Preview,
    ProjectPatch? Diff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied,
    bool Reverted,
    IReadOnlyList<BlueprintPrefabAuditEntry> Audit);

public sealed record BlueprintPrefabAuditEntry(
    string PlanId,
    string Action,
    long Revision,
    DateTimeOffset OccurredAt,
    bool Succeeded);
