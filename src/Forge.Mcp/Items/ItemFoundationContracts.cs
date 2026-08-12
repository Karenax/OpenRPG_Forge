using Forge.Application.Commands;
using Forge.Domain.Items;

namespace Forge.Mcp.Items;

public sealed record ItemFoundationResource(
    int SchemaVersion,
    long ProjectRevision,
    string? Filter,
    IReadOnlyList<ItemFoundationResourceItem> Items);

public sealed record ItemFoundationResourceItem(
    string BlueprintId,
    string BlueprintSlug,
    string DisplayName,
    string ProfileId,
    int ProfileSchemaVersion,
    string Provenance,
    bool RequiredAtRuntime,
    string TypeId,
    string TypeSlug,
    int DefinitionVersion,
    IReadOnlyList<string> Locales,
    IReadOnlyList<string> Slots,
    IReadOnlyList<string> AllowedInstanceOverrides,
    ItemCreationResource? Creation,
    ItemGameplayResource? Gameplay,
    ItemOperationsResource? Operations);

public sealed record ItemCreationResource(
    string ProfileId,
    bool RequiredAtRuntime,
    IReadOnlyList<string> GenerationRules,
    IReadOnlyList<string> MeshAssetIds,
    IReadOnlyList<string> MaterialAssetIds,
    IReadOnlyList<string> Sockets,
    string IconAssetId,
    IReadOnlyList<string> VisualEffectAssetIds,
    string EquippedPreviewAssetId,
    double Mass,
    double Volume,
    int GridWidth,
    int GridHeight,
    double StackMassFactor,
    double StackVolumeFactor,
    int StackMaximum,
    string StackCompatibilityKey,
    bool StackReplicated,
    int TotalCharges,
    int CurrentCharges,
    int UsesPerRest,
    int UsesRemaining,
    long CooldownMilliseconds,
    int Durability,
    bool Consumable,
    string RechargeRule);

public sealed record ItemGameplayResource(
    string ProfileId,
    bool RequiredAtRuntime,
    IReadOnlyDictionary<string, bool> CriticalProtections,
    bool AllowAuditedGameMasterOverride,
    IReadOnlyDictionary<string, bool> TransferDestinations,
    IReadOnlyList<string> RequiredPermissions,
    IReadOnlyList<string> ModifierKinds,
    string? AbilityDefinitionId,
    IReadOnlyList<string> SlotDefinitions,
    IReadOnlyList<string> RequiredSlots);

public sealed record ItemOperationsResource(
    string ProfileId,
    bool RequiredAtRuntime,
    string LootTableId,
    int LootEntryCount,
    long InventoryRevision,
    int InventoryPlacementCount,
    IReadOnlyList<string> LifecycleEvents);

public sealed record ItemFoundationPlanRequest(
    int SchemaVersion,
    long BaseRevision,
    string BlueprintId,
    ItemFoundationProfile Profile,
    bool AcceptTypeMigration = false);

public sealed record ItemFoundationPlanResult(
    bool Succeeded,
    long BaseRevision,
    ProjectPatch? SemanticDiff,
    ProjectPatch? Inverse,
    IReadOnlyList<AgentDiagnostic> Diagnostics);
