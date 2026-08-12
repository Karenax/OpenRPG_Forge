namespace Forge.Domain.Items;

public sealed record ItemTypeDefinition(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Slug,
    IReadOnlyList<string> AllowedSlots,
    IReadOnlyList<string> AllowedPropertyKinds,
    string RequiredRig);

public sealed record ItemFoundationText(
    string Locale,
    string Name,
    string ShortDescription,
    string LongDescription,
    string UnknownText,
    string TypeLabel,
    string Notes);

public sealed record ItemFoundationProperty(
    Guid Id,
    string Kind,
    double Value,
    string EffectId,
    string? ScriptId);

public sealed record ItemFoundationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool RequiredAtRuntime,
    string PackageManifestId,
    string LicenseSpdx,
    string ContentSha256,
    Guid TypeId,
    ItemTypeDefinition Type,
    string Archetype,
    string AppearanceAssetId,
    string AppearanceRig,
    int DefinitionVersion,
    int Quality,
    int Level,
    decimal BaseValue,
    IReadOnlyList<string> Slots,
    IReadOnlyList<ItemFoundationProperty> Properties,
    IReadOnlyList<ItemFoundationText> Texts,
    IReadOnlyList<string> AllowedInstanceOverrides,
    ItemCreationProfile? Creation = null,
    ItemGameplayProfile? Gameplay = null,
    ItemOperationsProfile? Operations = null)
{
    public const int CurrentSchemaVersion = 1;

    public static ItemFoundationProfile CreateDefault(
        Guid id,
        string slug,
        string displayName,
        string provenance)
    {
        var typeId = Guid.NewGuid();
        return new(id, CurrentSchemaVersion, provenance, false, "project-content", "CC0-1.0",
            new string('0', 64), typeId,
            new(typeId, CurrentSchemaVersion, provenance, "generic-item",
                ["hand", "inventory"], ["utility"], "none"),
            slug, "asset.item.placeholder", "none", 1, 0, 0, 0,
            ["inventory"], [],
            [new("fr", displayName, displayName, displayName, "Objet inconnu", "Objet", string.Empty)],
            ["identified", "durability", "owner"]);
    }
}

public sealed record ItemFoundationInstance(
    Guid Id,
    Guid DefinitionId,
    int DefinitionVersion,
    int Durability,
    IReadOnlyDictionary<string, string> Overrides);

public sealed record ItemTypeMigrationPreview(
    Guid SourceTypeId,
    Guid TargetTypeId,
    IReadOnlyList<Guid> IncompatiblePropertyIds,
    IReadOnlySet<string> IncompatibleSlots,
    bool RequiresRigChange)
{
    public bool RequiresAcceptance => IncompatiblePropertyIds.Count > 0 || IncompatibleSlots.Count > 0 ||
                                      RequiresRigChange;
}

public sealed record ItemFoundationDiagnostic(string Code, string Path, string Message);

public sealed record ItemFoundationRuntimeResult(
    ItemFoundationProfile? Profile,
    bool UsedFallback,
    IReadOnlyList<ItemFoundationDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
