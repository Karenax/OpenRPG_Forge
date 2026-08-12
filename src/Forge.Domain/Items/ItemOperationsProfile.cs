namespace Forge.Domain.Items;

public enum ItemLifecycleEventType
{
    ItemCreated,
    Acquired,
    Transferred,
    Equipped,
    Unequipped,
    Used,
    Consumed,
    Broken,
    Destroyed,
}

public sealed record ItemLootEntryPolicy(
    Guid ItemDefinitionId,
    string Group,
    double Weight,
    int MinimumQuantity,
    int MaximumQuantity,
    int MinimumQuality,
    string Condition,
    bool Unique);

public sealed record ItemLootTablePolicy(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<ItemLootEntryPolicy> Entries);

public sealed record ItemInventoryPlacementPolicy(
    Guid ItemInstanceId,
    string ContainerId,
    string? SlotId,
    int GridX,
    int GridY,
    int Quantity,
    IReadOnlyList<string> Tags,
    string PreviewAssetId);

public sealed record ItemInventoryInspectorPolicy(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    long Revision,
    IReadOnlyList<ItemInventoryPlacementPolicy> Placements,
    bool DragDropEnabled,
    bool MultiEditEnabled,
    bool ComparisonEnabled);

public sealed record ItemLifecycleBindingPolicy(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    ItemLifecycleEventType Kind,
    string HandlerId,
    string Authority);

public sealed record ItemLifecycleEventPolicy(
    string ForgeScriptApiVersion,
    IReadOnlyList<ItemLifecycleBindingPolicy> Bindings);

public sealed record ItemOperationsProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool RequiredAtRuntime,
    string PackageManifestId,
    string LicenseSpdx,
    string ContentSha256,
    ItemLootTablePolicy Loot,
    ItemInventoryInspectorPolicy Inspector,
    ItemLifecycleEventPolicy Events)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record ItemOperationsRuntimeResult(
    ItemOperationsProfile? Profile,
    bool UsedFallback,
    IReadOnlyList<ItemFoundationDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
