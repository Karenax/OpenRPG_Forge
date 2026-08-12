using Forge.Domain.Blueprints;
using Forge.Domain.Items;
using Forge.Domain.Projects;

namespace Forge.Application.Items;

public readonly record struct ItemFoundationHandle(Guid Value);

public sealed record ItemFoundationScriptView(
    ItemFoundationHandle Handle,
    string BlueprintId,
    string Slug,
    string Type,
    int DefinitionVersion,
    IReadOnlyDictionary<string, string> Names,
    IReadOnlyList<string> Slots,
    IReadOnlyList<string> AllowedOverrides,
    ItemCreationScriptView? Creation,
    ItemGameplayScriptView? Gameplay,
    ItemOperationsScriptView? Operations);

public sealed record ItemCreationScriptView(
    Guid ProfileId,
    IReadOnlyList<string> GenerationRules,
    IReadOnlyList<string> MeshAssetIds,
    string IconAssetId,
    double Mass,
    double Volume,
    int GridWidth,
    int GridHeight,
    int StackMaximum,
    string StackCompatibilityKey,
    int CurrentCharges,
    int UsesRemaining,
    long CooldownMilliseconds,
    int Durability,
    string RechargeRule);

public sealed record ItemGameplayScriptView(
    Guid ProfileId,
    bool RequiredAtRuntime,
    IReadOnlyDictionary<string, bool> CriticalProtections,
    IReadOnlyDictionary<string, bool> TransferDestinations,
    IReadOnlyList<string> RequiredPermissions,
    IReadOnlyList<string> ModifierKinds,
    Guid? AbilityDefinitionId,
    IReadOnlyList<string> EquipmentSlots,
    IReadOnlyList<string> RequiredEquipmentSlots);

public sealed record ItemOperationsScriptView(
    Guid ProfileId,
    bool RequiredAtRuntime,
    Guid LootTableId,
    int LootEntryCount,
    long InventoryRevision,
    int InventoryPlacementCount,
    IReadOnlyList<string> LifecycleEvents);

public static class ItemFoundationScriptService
{
    public static ItemFoundationScriptView? Read(ProjectDefinition project, BlueprintId blueprintId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(blueprintId);
        var blueprint = project.Blueprints.SingleOrDefault(item => item.Id == blueprintId);
        var profile = blueprint?.ItemFoundation;
        if (blueprint is null || profile is null) return null;
        var resolved = ItemFoundationValidator.Resolve(profile, profile.RequiredAtRuntime);
        if (!resolved.Succeeded || resolved.Profile is null) return null;
        return new(new(profile.Id), blueprint.Id.ToString(), blueprint.Slug, profile.Type.Slug,
            profile.DefinitionVersion,
            profile.Texts.ToDictionary(item => item.Locale, item => item.Name, StringComparer.Ordinal),
            profile.Slots, profile.AllowedInstanceOverrides,
            profile.Creation is null ? null : new(profile.Creation.Id, profile.Creation.Wizard.GenerationRules,
                profile.Creation.Appearance.MeshAssetIds, profile.Creation.Appearance.IconAssetId,
                profile.Creation.Inventory.Mass, profile.Creation.Inventory.Volume,
                profile.Creation.Inventory.GridWidth, profile.Creation.Inventory.GridHeight,
                profile.Creation.Stack.Maximum, profile.Creation.Stack.CompatibilityKey,
                profile.Creation.Usage.CurrentCharges, profile.Creation.Usage.UsesRemaining,
                profile.Creation.Usage.CooldownMilliseconds, profile.Creation.Usage.Durability,
                profile.Creation.Usage.RechargeRule),
            profile.Gameplay is null ? null : new(profile.Gameplay.Id, profile.Gameplay.RequiredAtRuntime,
                new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    ["destruction"] = profile.Gameplay.Critical.ProtectDestruction,
                    ["sale"] = profile.Gameplay.Critical.ProtectSale,
                    ["drop"] = profile.Gameplay.Critical.ProtectDrop,
                    ["theft"] = profile.Gameplay.Critical.ProtectTheft,
                    ["consumption"] = profile.Gameplay.Critical.ProtectConsumption,
                    ["cleanup"] = profile.Gameplay.Critical.ProtectCleanup,
                },
                profile.Gameplay.Transfer.AllowedDestinations.ToDictionary(item => item.Key.ToString(),
                    item => item.Value, StringComparer.Ordinal), profile.Gameplay.Transfer.RequiredPermissions,
                profile.Gameplay.Modifiers.Select(item => item.Kind).Order(StringComparer.Ordinal).ToArray(),
                profile.Gameplay.Ability?.AbilityDefinitionId,
                profile.Gameplay.Equipment.SlotDefinitions.Select(item => item.Id).Order(StringComparer.Ordinal).ToArray(),
                profile.Gameplay.Equipment.RequiredSlots),
            profile.Operations is null ? null : new(profile.Operations.Id,
                profile.Operations.RequiredAtRuntime, profile.Operations.Loot.Id,
                profile.Operations.Loot.Entries.Count, profile.Operations.Inspector.Revision,
                profile.Operations.Inspector.Placements.Count,
                profile.Operations.Events.Bindings.Select(item => item.Kind.ToString())
                    .Order(StringComparer.Ordinal).ToArray()));
    }

    public static IReadOnlyList<ItemFoundationDiagnostic> ValidateInstance(
        ItemFoundationHandle handle,
        ItemFoundationProfile profile,
        ItemFoundationInstance instance)
    {
        if (handle.Value != profile.Id)
            return [new("item.foundation.handle_stale", "itemInstance", "The typed item handle is stale.")];
        return ItemFoundationValidator.ValidateInstance(profile, instance);
    }
}
