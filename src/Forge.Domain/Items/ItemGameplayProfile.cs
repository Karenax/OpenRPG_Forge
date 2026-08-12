namespace Forge.Domain.Items;

public enum ItemTransferDestination
{
    Drop,
    Trade,
    Store,
    Container,
    Mail,
    Destroy,
    Consume,
    GameMaster,
}

public sealed record ItemCriticalPolicy(
    bool ProtectDestruction,
    bool ProtectSale,
    bool ProtectDrop,
    bool ProtectTheft,
    bool ProtectConsumption,
    bool ProtectCleanup,
    bool AllowAuditedGameMasterOverride);

public sealed record ItemTransferPolicy(
    IReadOnlyDictionary<ItemTransferDestination, bool> AllowedDestinations,
    IReadOnlyList<string> RequiredPermissions);

public sealed record ItemModifierPolicy(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Kind,
    IReadOnlyDictionary<string, double> Parameters,
    string StackingRule,
    string? Condition,
    string UiSummary,
    string? BeforeHook,
    string? AfterHook);

public sealed record ItemAbilityPolicy(
    Guid AbilityDefinitionId,
    string Targeting,
    IReadOnlyDictionary<string, double> Costs,
    int ChargeCost,
    long CooldownMilliseconds,
    string RequestedEvent,
    string ResolvedEvent);

public sealed record ItemEquipmentSlotPolicy(
    string Id,
    string Ruleset,
    string Rig,
    int Layer,
    IReadOnlyList<string> Conflicts);

public sealed record ItemEquipmentPolicy(
    IReadOnlyList<ItemEquipmentSlotPolicy> SlotDefinitions,
    IReadOnlyList<string> RequiredSlots);

public sealed record ItemGameplayProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool RequiredAtRuntime,
    string PackageManifestId,
    string LicenseSpdx,
    string ContentSha256,
    ItemCriticalPolicy Critical,
    ItemTransferPolicy Transfer,
    IReadOnlyList<ItemModifierPolicy> Modifiers,
    ItemAbilityPolicy? Ability,
    ItemEquipmentPolicy Equipment)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record ItemGameplayRuntimeResult(
    ItemGameplayProfile? Profile,
    bool UsedFallback,
    IReadOnlyList<ItemFoundationDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
