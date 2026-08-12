namespace Forge.Domain.Items;

public sealed record ItemCreationWizardPolicy(
    IReadOnlyList<string> Sections,
    IReadOnlyList<string> GenerationRules);

public sealed record ItemAppearanceDefinition(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<string> MeshAssetIds,
    IReadOnlyList<string> MaterialAssetIds,
    IReadOnlyList<string> Sockets,
    string IconAssetId,
    IReadOnlyList<string> VisualEffectAssetIds,
    string EquippedPreviewAssetId);

public sealed record ItemInventoryPolicy(
    double Mass,
    double Volume,
    int GridWidth,
    int GridHeight,
    double StackMassFactor,
    double StackVolumeFactor);

public sealed record ItemStackPolicy(
    int Maximum,
    string CompatibilityKey,
    bool Replicated);

public sealed record ItemUsagePolicy(
    int TotalCharges,
    int CurrentCharges,
    int UsesPerRest,
    int UsesRemaining,
    long CooldownMilliseconds,
    int Durability,
    bool Consumable,
    string RechargeRule);

public sealed record ItemCreationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool RequiredAtRuntime,
    string PackageManifestId,
    string LicenseSpdx,
    string ContentSha256,
    ItemCreationWizardPolicy Wizard,
    ItemAppearanceDefinition Appearance,
    ItemInventoryPolicy Inventory,
    ItemStackPolicy Stack,
    ItemUsagePolicy Usage)
{
    public const int CurrentSchemaVersion = 1;

    public static ItemCreationProfile CreateDefault(Guid id, Guid appearanceId, string provenance) => new(
        id, CurrentSchemaVersion, provenance, false, "project-content", "CC0-1.0", new string('0', 64),
        new(["archetype", "appearance", "slots", "quality", "level", "property", "provenance",
            "generation-rules"], ["respect-type", "preserve-provenance"]),
        new(appearanceId, CurrentSchemaVersion, provenance, ["asset.item.placeholder.mesh"], [],
            ["hand-r"], "asset.item.placeholder.icon", [], "asset.item.placeholder.equipped"),
        new(0, 0, 1, 1, 1, 1), new(1, "unique-item", false),
        new(0, 0, 0, 0, 0, 100, false, "none"));
}

public sealed record ItemCreationRuntimeResult(
    ItemCreationProfile? Profile,
    bool UsedFallback,
    IReadOnlyList<ItemFoundationDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
