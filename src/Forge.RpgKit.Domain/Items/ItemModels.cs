namespace Forge.RpgKit.Domain.Items;

public sealed record LocalizedItemText(string Locale, string Name, string ShortDescription, string LongDescription, string UnknownText, string TypeLabel, string Notes);
public sealed record ItemType(Guid Id, int SchemaVersion, string Provenance, string Slug, IReadOnlySet<string> AllowedSlots, IReadOnlySet<string> AllowedPropertyKinds, string RequiredRig);
public sealed record ItemProperty(Guid Id, string Kind, double Value, string EffectId, string? ScriptId);
public sealed record ItemDefinition(Guid Id, int SchemaVersion, string Provenance, string Slug, Guid TypeId, string Archetype, string AppearanceAssetId, string LicenseSpdx, string ContentSha256, int Quality, int Level, IReadOnlySet<string> Slots, IReadOnlyList<ItemProperty> Properties, IReadOnlyList<LocalizedItemText> Texts, decimal BaseValue);
public sealed record ItemInstance(Guid Id, Guid DefinitionId, int DefinitionVersion, int Durability, IReadOnlyDictionary<string, string> Overrides);
public sealed record ItemDiagnostic(string Code, string Path, string Message);
public sealed record ItemTypeMigrationPreview(ItemDefinition Definition, Guid TargetTypeId, IReadOnlyList<ItemProperty> IncompatibleProperties, IReadOnlySet<string> IncompatibleSlots, bool RequiresRigChange);

public static class ItemRules
{
    public static IReadOnlyList<ItemDiagnostic> Validate(ItemDefinition item, IReadOnlyDictionary<Guid, ItemType> types)
    {
        var diagnostics = new List<ItemDiagnostic>(); var root = $"items/{item.Id}";
        if (item.Id == Guid.Empty || item.SchemaVersion != 1 || string.IsNullOrWhiteSpace(item.Provenance) || string.IsNullOrWhiteSpace(item.Slug)) diagnostics.Add(new("RPG_ITEM_IDENTITY_INVALID", root, "Item identity is invalid."));
        if (!types.TryGetValue(item.TypeId, out var type)) { diagnostics.Add(new("RPG_ITEM_TYPE_MISSING", $"{root}/type", "Item type does not exist.")); return diagnostics; }
        if (item.Slots.Any(slot => !type.AllowedSlots.Contains(slot))) diagnostics.Add(new("RPG_ITEM_SLOT_INVALID", $"{root}/slots", "A slot is incompatible with the item type."));
        if (item.Properties.Any(property => !type.AllowedPropertyKinds.Contains(property.Kind))) diagnostics.Add(new("RPG_ITEM_PROPERTY_INVALID", $"{root}/properties", "A property is incompatible with the item type."));
        if (item.Properties.Any(property => !double.IsFinite(property.Value) || string.IsNullOrWhiteSpace(property.EffectId))) diagnostics.Add(new("RPG_ITEM_EFFECT_INVALID", $"{root}/properties", "Effect values and references must be valid."));
        if (item.Texts.Count == 0 || item.Texts.Any(text => string.IsNullOrWhiteSpace(text.Locale) || string.IsNullOrWhiteSpace(text.Name))) diagnostics.Add(new("RPG_ITEM_LOCALIZATION_INVALID", $"{root}/texts", "At least one valid localization is required."));
        if (string.IsNullOrWhiteSpace(item.AppearanceAssetId) || string.IsNullOrWhiteSpace(item.LicenseSpdx) || item.ContentSha256.Length != 64) diagnostics.Add(new("RPG_ITEM_ASSET_INVALID", $"{root}/asset", "Asset, license and SHA-256 are required."));
        if (item.BaseValue < 0 || item.Level < 0 || item.Quality is < 0 or > 5) diagnostics.Add(new("RPG_ITEM_ECONOMY_INVALID", $"{root}/economy", "Economy, level or quality is outside its range."));
        return diagnostics;
    }

    public static ItemTypeMigrationPreview PreviewTypeMigration(ItemDefinition item, ItemType target, string currentRig)
        => new(item, target.Id, item.Properties.Where(property => !target.AllowedPropertyKinds.Contains(property.Kind)).ToArray(), item.Slots.Where(slot => !target.AllowedSlots.Contains(slot)).ToHashSet(StringComparer.Ordinal), !string.Equals(currentRig, target.RequiredRig, StringComparison.Ordinal));
}
