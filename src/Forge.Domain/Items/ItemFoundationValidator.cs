using System.Text.RegularExpressions;

namespace Forge.Domain.Items;

public static partial class ItemFoundationValidator
{
    public const string MissingRequiredCode = "item.foundation.required_missing";
    public const string CreationMissingRequiredCode = "item.creation.required_missing";
    public const string GameplayMissingRequiredCode = "item.gameplay.required_missing";
    public const string OperationsMissingRequiredCode = "item.operations.required_missing";

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
        ValidateCreation(profile, path, diagnostics);
        ValidateGameplay(profile, path, diagnostics);
        ValidateOperations(profile, path, diagnostics);
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

    public static ItemCreationRuntimeResult ResolveCreation(ItemFoundationProfile? foundation, bool required = false)
    {
        var profile = foundation?.Creation;
        var effectiveRequired = required || profile?.RequiredAtRuntime == true;
        if (profile is null)
            return new(null, !effectiveRequired, effectiveRequired
                ? [new(CreationMissingRequiredCode, "itemFoundation/creation",
                    "Required item creation capabilities are missing.")]
                : []);
        var diagnostics = new List<ItemFoundationDiagnostic>();
        ValidateCreation(foundation!, "itemFoundation", diagnostics);
        return new(diagnostics.Count == 0 ? profile : null, false, diagnostics);
    }

    public static ItemGameplayRuntimeResult ResolveGameplay(ItemFoundationProfile? foundation, bool required = false)
    {
        var profile = foundation?.Gameplay;
        var effectiveRequired = required || profile?.RequiredAtRuntime == true;
        if (profile is null)
            return new(null, !effectiveRequired, effectiveRequired
                ? [new(GameplayMissingRequiredCode, "itemFoundation/gameplay",
                    "Required item gameplay capabilities are missing.")]
                : []);
        var diagnostics = new List<ItemFoundationDiagnostic>();
        ValidateGameplay(foundation!, "itemFoundation", diagnostics);
        return new(diagnostics.Count == 0 ? profile : null, false, diagnostics);
    }

    public static ItemOperationsRuntimeResult ResolveOperations(ItemFoundationProfile? foundation, bool required = false)
    {
        var profile = foundation?.Operations;
        var effectiveRequired = required || profile?.RequiredAtRuntime == true;
        if (profile is null)
            return new(null, !effectiveRequired, effectiveRequired
                ? [new(OperationsMissingRequiredCode, "itemFoundation/operations",
                    "Required item operations capabilities are missing.")]
                : []);
        var diagnostics = new List<ItemFoundationDiagnostic>();
        ValidateOperations(foundation!, "itemFoundation", diagnostics);
        return new(diagnostics.Count == 0 ? profile : null, false, diagnostics);
    }

    private static void ValidateCreation(
        ItemFoundationProfile profile,
        string path,
        List<ItemFoundationDiagnostic> diagnostics)
    {
        var creation = profile.Creation;
        if (creation is null) return;
        var root = $"{path}/creation";
        Add(creation.Id == Guid.Empty || creation.Id == profile.Id || creation.Appearance.Id == Guid.Empty ||
            creation.Appearance.Id == creation.Id || creation.Appearance.Id == profile.Id,
            "item.creation.uuid_invalid", root,
            "Creation-profile and appearance UUIDs must be non-empty and distinct.");
        Add(creation.SchemaVersion != ItemCreationProfile.CurrentSchemaVersion ||
            creation.Appearance.SchemaVersion != ItemCreationProfile.CurrentSchemaVersion,
            "item.creation.schema_unsupported", root,
            "Creation-profile and appearance schemas must be supported.");
        Add(string.IsNullOrWhiteSpace(creation.Provenance) ||
            string.IsNullOrWhiteSpace(creation.Appearance.Provenance) ||
            string.IsNullOrWhiteSpace(creation.PackageManifestId),
            "item.creation.provenance_invalid", $"{root}/provenance",
            "Creation, appearance and package provenance are required.");
        Add(string.IsNullOrWhiteSpace(creation.LicenseSpdx) || !IsSha256(creation.ContentSha256),
            "item.creation.content_governance_invalid", $"{root}/content",
            "Creation capabilities require an SPDX license and lowercase SHA-256 hash.");

        var requiredSections = new[]
        {
            "archetype", "appearance", "slots", "quality", "level", "property", "provenance",
            "generation-rules",
        };
        Add(creation.Wizard.Sections.Count == 0 ||
            creation.Wizard.Sections.Distinct(StringComparer.Ordinal).Count() != creation.Wizard.Sections.Count ||
            creation.Wizard.Sections.Any(value => !IdentifierPattern().IsMatch(value)) ||
            requiredSections.Any(section => !creation.Wizard.Sections.Contains(section, StringComparer.Ordinal)) ||
            creation.Wizard.GenerationRules.Count == 0 ||
            creation.Wizard.GenerationRules.Distinct(StringComparer.Ordinal).Count() !=
            creation.Wizard.GenerationRules.Count ||
            creation.Wizard.GenerationRules.Any(value => !IdentifierPattern().IsMatch(value)),
            "item.creation.wizard_invalid", $"{root}/wizard",
            "The item wizard requires every canonical section and at least one generation rule.");

        var appearance = creation.Appearance;
        Add(appearance.MeshAssetIds.Count == 0 || appearance.MeshAssetIds.Any(string.IsNullOrWhiteSpace) ||
            appearance.MeshAssetIds.Distinct(StringComparer.Ordinal).Count() != appearance.MeshAssetIds.Count ||
            appearance.MaterialAssetIds.Any(string.IsNullOrWhiteSpace) ||
            appearance.MaterialAssetIds.Distinct(StringComparer.Ordinal).Count() != appearance.MaterialAssetIds.Count ||
            appearance.Sockets.Any(value => !IdentifierPattern().IsMatch(value)) ||
            appearance.Sockets.Distinct(StringComparer.Ordinal).Count() != appearance.Sockets.Count ||
            string.IsNullOrWhiteSpace(appearance.IconAssetId) ||
            appearance.VisualEffectAssetIds.Any(string.IsNullOrWhiteSpace) ||
            appearance.VisualEffectAssetIds.Distinct(StringComparer.Ordinal).Count() !=
            appearance.VisualEffectAssetIds.Count || string.IsNullOrWhiteSpace(appearance.EquippedPreviewAssetId),
            "item.creation.appearance_invalid", $"{root}/appearance",
            "Appearance requires unique meshes, materials, sockets, icon, VFX and equipped preview bindings.");

        var inventory = creation.Inventory;
        Add(!double.IsFinite(inventory.Mass) || !double.IsFinite(inventory.Volume) || inventory.Mass < 0 ||
            inventory.Volume < 0 || inventory.GridWidth < 1 || inventory.GridHeight < 1 ||
            inventory.StackMassFactor is < 0 or > 1 || inventory.StackVolumeFactor is < 0 or > 1,
            "item.creation.inventory_policy_invalid", $"{root}/inventory",
            "Mass, volume, footprint and stack contributions must be inside supported ranges.");

        Add(creation.Stack.Maximum is < 1 or > 1_000_000 ||
            !IdentifierPattern().IsMatch(creation.Stack.CompatibilityKey),
            "item.creation.stack_policy_invalid", $"{root}/stack",
            "Stack maximum and compatibility key are invalid.");

        var usage = creation.Usage;
        Add(usage.TotalCharges < 0 || usage.CurrentCharges < 0 || usage.CurrentCharges > usage.TotalCharges ||
            usage.UsesPerRest < 0 || usage.UsesRemaining < 0 || usage.UsesRemaining > usage.UsesPerRest ||
            usage.CooldownMilliseconds < 0 || usage.Durability is < 0 or > 100 ||
            !IdentifierPattern().IsMatch(usage.RechargeRule),
            "item.creation.usage_policy_invalid", $"{root}/usage",
            "Charges, rest uses, cooldown, durability and recharge rules are invalid.");
        return;

        void Add(bool condition, string code, string diagnosticPath, string message)
        {
            if (condition) diagnostics.Add(new(code, diagnosticPath, message));
        }
    }

    private static void ValidateGameplay(
        ItemFoundationProfile foundation,
        string path,
        List<ItemFoundationDiagnostic> diagnostics)
    {
        var profile = foundation.Gameplay;
        if (profile is null) return;
        var root = $"{path}/gameplay";
        Add(profile.Id == Guid.Empty || profile.Id == foundation.Id || profile.Id == foundation.Creation?.Id,
            "item.gameplay.uuid_invalid", root, "The item gameplay UUID must be non-empty and unique.");
        Add(profile.SchemaVersion != ItemGameplayProfile.CurrentSchemaVersion,
            "item.gameplay.schema_unsupported", root, "The item gameplay schema is unsupported.");
        Add(string.IsNullOrWhiteSpace(profile.Provenance) || string.IsNullOrWhiteSpace(profile.PackageManifestId),
            "item.gameplay.provenance_invalid", $"{root}/provenance",
            "Gameplay provenance and package manifest are required.");
        Add(string.IsNullOrWhiteSpace(profile.LicenseSpdx) || !IsSha256(profile.ContentSha256),
            "item.gameplay.content_governance_invalid", $"{root}/content",
            "Gameplay capabilities require an SPDX license and lowercase SHA-256 hash.");

        var destinations = Enum.GetValues<ItemTransferDestination>();
        Add(profile.Transfer.AllowedDestinations.Count != destinations.Length ||
            destinations.Any(value => !profile.Transfer.AllowedDestinations.ContainsKey(value)) ||
            profile.Transfer.RequiredPermissions.Distinct(StringComparer.Ordinal).Count() !=
            profile.Transfer.RequiredPermissions.Count ||
            profile.Transfer.RequiredPermissions.Any(value => !IdentifierPattern().IsMatch(value)),
            "item.gameplay.transfer_policy_invalid", $"{root}/transfer",
            "Every transfer destination and canonical required permission must be configured.");

        var modifierIds = new HashSet<Guid>();
        foreach (var modifier in profile.Modifiers)
        {
            var modifierPath = $"{root}/modifiers/{modifier.Id}";
            Add(modifier.Id == Guid.Empty || !modifierIds.Add(modifier.Id) ||
                modifier.SchemaVersion != ItemGameplayProfile.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(modifier.Provenance) || !IdentifierPattern().IsMatch(modifier.Kind) ||
                modifier.Parameters.Count == 0 || modifier.Parameters.Any(item =>
                    !IdentifierPattern().IsMatch(item.Key) || !double.IsFinite(item.Value)) ||
                modifier.StackingRule is not ("sum" or "highest" or "lowest") ||
                modifier.Condition is { Length: > 0 } condition && !IdentifierPattern().IsMatch(condition) ||
                string.IsNullOrWhiteSpace(modifier.UiSummary) ||
                modifier.BeforeHook is { Length: > 0 } before && !HandlerPattern().IsMatch(before) ||
                modifier.AfterHook is { Length: > 0 } after && !HandlerPattern().IsMatch(after),
                "item.gameplay.modifier_invalid", modifierPath,
                "Modifiers require unique identity, finite parameters, stacking, summary and safe bindings.");
        }

        if (profile.Ability is { } ability)
            Add(ability.AbilityDefinitionId == Guid.Empty || !IdentifierPattern().IsMatch(ability.Targeting) ||
                ability.Costs.Any(item => !IdentifierPattern().IsMatch(item.Key) ||
                    !double.IsFinite(item.Value) || item.Value < 0) || ability.ChargeCost < 1 ||
                ability.CooldownMilliseconds < 0 || !HandlerPattern().IsMatch(ability.RequestedEvent) ||
                !HandlerPattern().IsMatch(ability.ResolvedEvent),
                "item.gameplay.ability_invalid", $"{root}/ability",
                "Item ability, targeting, costs, charges, cooldown and events are invalid.");

        var slots = profile.Equipment.SlotDefinitions;
        var slotIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in slots)
            Add(!IdentifierPattern().IsMatch(slot.Id) || !slotIds.Add(slot.Id) ||
                !IdentifierPattern().IsMatch(slot.Ruleset) || !IdentifierPattern().IsMatch(slot.Rig) ||
                slot.Layer is < 0 or > 100 || slot.Conflicts.Distinct(StringComparer.Ordinal).Count() !=
                slot.Conflicts.Count || slot.Conflicts.Any(value => !IdentifierPattern().IsMatch(value)),
                "item.gameplay.equipment_slot_invalid", $"{root}/equipment/slots/{slot.Id}",
                "Equipment slots require unique IDs, compatible ruleset/rig, layer and conflicts.");
        Add(slots.Count == 0 || profile.Equipment.RequiredSlots.Count == 0 ||
            profile.Equipment.RequiredSlots.Distinct(StringComparer.Ordinal).Count() !=
            profile.Equipment.RequiredSlots.Count ||
            profile.Equipment.RequiredSlots.Any(value => !slotIds.Contains(value)),
            "item.gameplay.equipment_policy_invalid", $"{root}/equipment/requiredSlots",
            "Equipment requires one or more resolved slots for multi-slot preview.");
        return;

        void Add(bool condition, string code, string diagnosticPath, string message)
        {
            if (condition) diagnostics.Add(new(code, diagnosticPath, message));
        }
    }

    private static void ValidateOperations(
        ItemFoundationProfile foundation,
        string path,
        List<ItemFoundationDiagnostic> diagnostics)
    {
        var profile = foundation.Operations;
        if (profile is null) return;
        var root = $"{path}/operations";
        var reservedIds = new HashSet<Guid>
        {
            foundation.Id,
            foundation.Creation?.Id ?? Guid.Empty,
            foundation.Creation?.Appearance.Id ?? Guid.Empty,
            foundation.Gameplay?.Id ?? Guid.Empty,
        };
        Add(profile.Id == Guid.Empty || reservedIds.Contains(profile.Id),
            "item.operations.uuid_invalid", root,
            "The item operations UUID must be non-empty and distinct from other item profiles.");
        Add(profile.SchemaVersion != ItemOperationsProfile.CurrentSchemaVersion,
            "item.operations.schema_unsupported", root, "The item operations schema is unsupported.");
        Add(string.IsNullOrWhiteSpace(profile.Provenance) || string.IsNullOrWhiteSpace(profile.PackageManifestId),
            "item.operations.provenance_invalid", $"{root}/provenance",
            "Operations provenance and package manifest are required.");
        Add(string.IsNullOrWhiteSpace(profile.LicenseSpdx) || !IsSha256(profile.ContentSha256),
            "item.operations.content_governance_invalid", $"{root}/content",
            "Item operations require an SPDX license and lowercase SHA-256 hash.");

        var loot = profile.Loot;
        Add(loot.Id == Guid.Empty || loot.Id == profile.Id || reservedIds.Contains(loot.Id) ||
            loot.SchemaVersion != ItemOperationsProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(loot.Provenance) || loot.Entries.Count == 0,
            "item.operations.loot_invalid", $"{root}/loot",
            "The loot table requires a distinct UUID, supported schema, provenance and entries.");
        var lootDefinitions = new HashSet<Guid>();
        foreach (var entry in loot.Entries)
            Add(entry.ItemDefinitionId == Guid.Empty || !lootDefinitions.Add(entry.ItemDefinitionId) ||
                !IdentifierPattern().IsMatch(entry.Group) || !double.IsFinite(entry.Weight) || entry.Weight <= 0 ||
                entry.MinimumQuantity < 1 || entry.MaximumQuantity < entry.MinimumQuantity ||
                entry.MinimumQuality < 0 || entry.Condition is { Length: > 0 } condition &&
                !IdentifierPattern().IsMatch(condition),
                "item.operations.loot_entry_invalid", $"{root}/loot/entries/{entry.ItemDefinitionId}",
                "Loot entries require resolved unique definitions, group, weight, quantity, quality and condition.");

        var inspector = profile.Inspector;
        Add(inspector.Id == Guid.Empty || inspector.Id == profile.Id || inspector.Id == loot.Id ||
            reservedIds.Contains(inspector.Id) ||
            inspector.SchemaVersion != ItemOperationsProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(inspector.Provenance) || inspector.Revision < 0 ||
            inspector.Placements.Count == 0 || !inspector.DragDropEnabled || !inspector.MultiEditEnabled ||
            !inspector.ComparisonEnabled,
            "item.operations.inspector_invalid", $"{root}/inspector",
            "The inventory inspector requires identity, revision, placements and authoring capabilities.");
        var placementIds = new HashSet<Guid>();
        foreach (var placement in inspector.Placements)
            Add(placement.ItemInstanceId == Guid.Empty || !placementIds.Add(placement.ItemInstanceId) ||
                !IdentifierPattern().IsMatch(placement.ContainerId) ||
                placement.SlotId is { Length: > 0 } slot && !IdentifierPattern().IsMatch(slot) ||
                placement.GridX < 0 || placement.GridY < 0 || placement.Quantity < 1 ||
                placement.Tags.Distinct(StringComparer.Ordinal).Count() != placement.Tags.Count ||
                placement.Tags.Any(tag => !IdentifierPattern().IsMatch(tag)) ||
                string.IsNullOrWhiteSpace(placement.PreviewAssetId),
                "item.operations.inventory_placement_invalid",
                $"{root}/inspector/placements/{placement.ItemInstanceId}",
                "Inventory placements require unique identity, container, grid, quantity, tags and preview.");

        var expectedEvents = Enum.GetValues<ItemLifecycleEventType>();
        var bindingIds = new HashSet<Guid>();
        var bindingKinds = new HashSet<ItemLifecycleEventType>();
        Add(profile.Events.ForgeScriptApiVersion != "1.0" || profile.Events.Bindings.Count != expectedEvents.Length,
            "item.operations.events_invalid", $"{root}/events",
            "Item lifecycle bindings require ForgeScript API 1.0 and every canonical event.");
        foreach (var binding in profile.Events.Bindings)
            Add(binding.Id == Guid.Empty || !bindingIds.Add(binding.Id) || !bindingKinds.Add(binding.Kind) ||
                binding.SchemaVersion != ItemOperationsProfile.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(binding.Provenance) || !HandlerPattern().IsMatch(binding.HandlerId) ||
                binding.Authority is not ("server" or "owner" or "game-master"),
                "item.operations.event_binding_invalid", $"{root}/events/{binding.Kind}",
                "Lifecycle bindings require unique identity, kind, handler and supported authority.");
        Add(expectedEvents.Any(kind => !bindingKinds.Contains(kind)),
            "item.operations.events_incomplete", $"{root}/events",
            "Every canonical item lifecycle event must be bound exactly once.");
        return;

        void Add(bool condition, string code, string diagnosticPath, string message)
        {
            if (condition) diagnostics.Add(new(code, diagnosticPath, message));
        }
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
