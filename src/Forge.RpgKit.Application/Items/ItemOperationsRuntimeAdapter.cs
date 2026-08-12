using Forge.Domain.Items;
using Forge.RpgKit.Domain.Items;

namespace Forge.RpgKit.Application.Items;

public sealed record ItemOperationsPolicyResolution(
    LootTable? Loot,
    InventoryDocument? Inventory,
    IReadOnlyDictionary<ItemLifecycleEventKind, ItemLifecycleBindingPolicy> EventBindings,
    bool OptionalFallback,
    IReadOnlyList<ItemFoundationDiagnostic> Diagnostics);

public static class ItemOperationsRuntimeAdapter
{
    public static ItemOperationsPolicyResolution Resolve(ItemFoundationProfile? foundation, bool required = false)
    {
        var resolved = ItemFoundationValidator.ResolveOperations(foundation, required);
        if (!resolved.Succeeded || resolved.Profile is null)
            return new(null, null, new Dictionary<ItemLifecycleEventKind, ItemLifecycleBindingPolicy>(),
                resolved.UsedFallback, resolved.Diagnostics);
        var profile = resolved.Profile;
        var loot = new LootTable(profile.Loot.Id, profile.Loot.SchemaVersion, profile.Loot.Provenance,
            profile.Loot.Entries.Select(item => new LootEntry(item.ItemDefinitionId, item.Group, item.Weight,
                item.MinimumQuantity, item.MaximumQuantity, item.MinimumQuality, item.Condition, item.Unique)).ToArray());
        var inventory = new InventoryDocument(profile.Inspector.Revision,
            profile.Inspector.Placements.Select(item => new InventoryPlacement(item.ItemInstanceId,
                item.ContainerId, item.SlotId, item.GridX, item.GridY, item.Quantity,
                item.Tags.ToHashSet(StringComparer.Ordinal), item.PreviewAssetId)).ToArray());
        var events = profile.Events.Bindings.ToDictionary(
            item => Enum.Parse<ItemLifecycleEventKind>(item.Kind.ToString()), item => item);
        return new(loot, inventory, events, false, []);
    }

    public static ItemLifecycleEvent Emit(
        ItemOperationsProfile profile,
        ItemLifecycleEventKind kind,
        Guid itemInstanceId,
        string? sourceId,
        string? targetId,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, string>? context = null)
    {
        var binding = profile.Events.Bindings.Single(item => item.Kind.ToString() == kind.ToString());
        return InventoryAuthoringService.Emit(kind, itemInstanceId,
            $"{binding.Provenance}:{binding.HandlerId}:{binding.Authority}", sourceId, targetId, occurredAt, context);
    }
}
