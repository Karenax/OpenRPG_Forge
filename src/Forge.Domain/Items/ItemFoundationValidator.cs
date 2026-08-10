using System.Text.RegularExpressions;

namespace Forge.Domain.Items;

public static partial class ItemFoundationValidator
{
    public const string MissingRequiredCode = "item.foundation.required_missing";

    public static IReadOnlyList<ItemFoundationDiagnostic> Validate(
        ItemFoundationProfile profile,
        string path = "itemFoundation")
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<ItemFoundationDiagnostic>();
        Add(profile.Id == Guid.Empty, "item.foundation.uuid_required", path, "A stable item UUID is required.");
        Add(profile.SchemaVersion != ItemFoundationProfile.CurrentSchemaVersion,
            "item.foundation.schema_unsupported", path, "The item foundation schema is unsupported.");
        Add(string.IsNullOrWhiteSpace(profile.Provenance) || string.IsNullOrWhiteSpace(profile.PackageManifestId),
            "item.foundation.provenance_invalid", $"{path}/provenance",
            "Item provenance and package manifest are required.");
        Add(string.IsNullOrWhiteSpace(profile.LicenseSpdx) || !IsSha256(profile.ContentSha256),
            "item.foundation.content_governance_invalid", $"{path}/content",
            "An SPDX license and lowercase SHA-256 content hash are required.");

        var type = profile.Type;
        Add(profile.TypeId == Guid.Empty || type.Id == Guid.Empty || profile.TypeId != type.Id ||
            type.SchemaVersion != ItemFoundationProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(type.Provenance) || !IdentifierPattern().IsMatch(type.Slug) ||
            string.IsNullOrWhiteSpace(type.RequiredRig),
            "item.foundation.type_invalid", $"{path}/typeId",
            "The immutable item type reference must resolve to a versioned type definition.");
        Add(type.AllowedSlots.Distinct(StringComparer.Ordinal).Count() != type.AllowedSlots.Count ||
            type.AllowedSlots.Any(value => !IdentifierPattern().IsMatch(value)) ||
            type.AllowedPropertyKinds.Distinct(StringComparer.Ordinal).Count() != type.AllowedPropertyKinds.Count ||
            type.AllowedPropertyKinds.Any(value => !IdentifierPattern().IsMatch(value)),
            "item.foundation.type_rules_invalid", $"{path}/type/rules",
            "Item-type slots and property kinds must be canonical identifiers.");

        Add(!IdentifierPattern().IsMatch(profile.Archetype) || string.IsNullOrWhiteSpace(profile.AppearanceAssetId) ||
            string.IsNullOrWhiteSpace(profile.AppearanceRig) || profile.DefinitionVersion < 1,
            "item.foundation.blueprint_invalid", path,
            "The item blueprint requires an archetype, appearance, rig and positive definition version.");
        Add(profile.Quality is < 0 or > 5 || profile.Level < 0 || profile.BaseValue < 0,
            "item.foundation.values_invalid", $"{path}/values",
            "Quality, level and base value must be inside supported ranges.");
        Add(profile.Slots.Distinct(StringComparer.Ordinal).Count() != profile.Slots.Count ||
            profile.Slots.Any(slot => !type.AllowedSlots.Contains(slot)),
            "item.foundation.slot_invalid", $"{path}/slots",
            "Every item slot must be allowed by the immutable item type.");

        var propertyIds = new HashSet<Guid>();
        foreach (var property in profile.Properties)
        {
            var propertyPath = $"{path}/properties/{property.Id}";
            Add(property.Id == Guid.Empty || !propertyIds.Add(property.Id), "item.foundation.property_uuid_invalid",
                propertyPath, "Item-property UUIDs must be non-empty and unique.");
            Add(!type.AllowedPropertyKinds.Contains(property.Kind) || !double.IsFinite(property.Value) ||
                string.IsNullOrWhiteSpace(property.EffectId) ||
                property.ScriptId is not null && !HandlerPattern().IsMatch(property.ScriptId),
                "item.foundation.property_invalid", propertyPath,
                "Item properties require a compatible kind, finite value, effect and optional script binding.");
        }

        var locales = new HashSet<string>(StringComparer.Ordinal);
        Add(profile.Texts.Count == 0, "item.foundation.localization_required", $"{path}/texts",
            "At least one localized item identity is required.");
        foreach (var text in profile.Texts)
        {
            var textPath = $"{path}/texts/{text.Locale}";
            Add(!LocalePattern().IsMatch(text.Locale) || !locales.Add(text.Locale) ||
                string.IsNullOrWhiteSpace(text.Name) || string.IsNullOrWhiteSpace(text.ShortDescription) ||
                string.IsNullOrWhiteSpace(text.LongDescription) || string.IsNullOrWhiteSpace(text.UnknownText) ||
                string.IsNullOrWhiteSpace(text.TypeLabel),
                "item.foundation.localization_invalid", textPath,
                "Localized name, descriptions, unknown text and type label are required per unique locale.");
        }

        Add(profile.AllowedInstanceOverrides.Distinct(StringComparer.Ordinal).Count() != profile.AllowedInstanceOverrides.Count ||
            profile.AllowedInstanceOverrides.Any(value => !IdentifierPattern().IsMatch(value)),
            "item.foundation.override_allowlist_invalid", $"{path}/allowedInstanceOverrides",
            "Instance override keys must be canonical identifiers.");
        return diagnostics;

        void Add(bool condition, string code, string diagnosticPath, string message)
        {
            if (condition) diagnostics.Add(new(code, diagnosticPath, message));
        }
    }

    public static IReadOnlyList<ItemFoundationDiagnostic> ValidateInstance(
        ItemFoundationProfile profile,
        ItemFoundationInstance instance,
        string path = "itemInstance")
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(instance);
        var diagnostics = new List<ItemFoundationDiagnostic>();
        if (instance.Id == Guid.Empty || instance.DefinitionId != profile.Id ||
            instance.DefinitionVersion != profile.DefinitionVersion || instance.Durability < 0)
            diagnostics.Add(new("item.foundation.instance_invalid", path,
                "Item instance identity, definition version or durability is invalid."));
        foreach (var key in instance.Overrides.Keys.Where(key => !profile.AllowedInstanceOverrides.Contains(key)))
            diagnostics.Add(new("item.foundation.instance_override_forbidden", $"{path}/overrides/{key}",
                "The mutable instance override is not allowed by the item blueprint."));
        return diagnostics;
    }

    public static ItemTypeMigrationPreview PreviewTypeMigration(ItemFoundationProfile profile, ItemTypeDefinition target)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(target);
        return new(profile.TypeId, target.Id,
            profile.Properties.Where(property => !target.AllowedPropertyKinds.Contains(property.Kind))
                .Select(property => property.Id).Order().ToArray(),
            profile.Slots.Where(slot => !target.AllowedSlots.Contains(slot)).ToHashSet(StringComparer.Ordinal),
            !string.Equals(profile.AppearanceRig, target.RequiredRig, StringComparison.Ordinal));
    }

    public static ItemFoundationProfile MigrateType(
        ItemFoundationProfile profile,
        ItemTypeDefinition target,
        bool acceptIncompatibilities)
    {
        var preview = PreviewTypeMigration(profile, target);
        if (preview.RequiresAcceptance && !acceptIncompatibilities)
            throw new InvalidOperationException("item.foundation.type_migration_preview_required");
        return profile with
        {
            TypeId = target.Id,
            Type = target,
            AppearanceRig = target.RequiredRig,
            Slots = profile.Slots.Except(preview.IncompatibleSlots).ToArray(),
            Properties = profile.Properties.Where(item => !preview.IncompatiblePropertyIds.Contains(item.Id)).ToArray(),
            DefinitionVersion = checked(profile.DefinitionVersion + 1),
        };
    }

    public static ItemFoundationRuntimeResult Resolve(ItemFoundationProfile? profile, bool required)
    {
        if (profile is null)
            return new(null, !required, required
                ? [new(MissingRequiredCode, "itemFoundation", "A required item foundation is missing.")]
                : []);
        var diagnostics = Validate(profile);
        return new(diagnostics.Count == 0 ? profile : null, false, diagnostics);
    }

    private static bool IsSha256(string? value) => value is { Length: 64 } &&
                                                   value.All(character => Uri.IsHexDigit(character) &&
                                                       !char.IsUpper(character));

    [GeneratedRegex("^[a-z][a-z0-9]*(?:[._-][a-z0-9]+)*$")]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex("^[a-z]{2,3}(?:-[a-z0-9]{2,8})?$")]
    private static partial Regex LocalePattern();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,63}$")]
    private static partial Regex HandlerPattern();
}
