using Forge.Application.Commands;
using Forge.Domain.Items;

namespace Forge.Mcp.Items;

public sealed record ItemAdvancedResource(
    int SchemaVersion,
    long ProjectRevision,
    string? Filter,
    IReadOnlyList<ItemAdvancedResourceItem> Items);

public sealed record ItemAdvancedResourceItem(
    string BlueprintId,
    string BlueprintSlug,
    string DisplayName,
    string ProfileId,
    decimal BaseCost,
    int RequirementCount,
    int KnowledgeLevelCount,
    int OnHitProcCount,
    bool Weapon,
    bool Armor,
    bool Ammo,
    bool Durability,
    bool Container,
    int LootContextCount,
    bool Unique,
    int RecipeCount,
    bool Procedural,
    bool RequiredAtRuntime);

public sealed record ItemAdvancedPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string BlueprintId,
    ItemAdvancedProfile Profile);

public sealed record ItemAdvancedPlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? SemanticDiff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
