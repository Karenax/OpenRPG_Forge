namespace Forge.RpgKit.Application.Items;

public enum ItemLifecycleEventKind { ItemCreated, Acquired, Transferred, Equipped, Unequipped, Used, Consumed, Broken, Destroyed }
public sealed record InventoryPlacement(Guid ItemInstanceId, string ContainerId, string? SlotId, int GridX, int GridY, int Quantity, IReadOnlySet<string> Tags, string PreviewAssetId);
public sealed record InventoryDocument(long Revision, IReadOnlyList<InventoryPlacement> Placements);
public sealed record InventoryEditCommand(IReadOnlyList<Guid> ItemInstanceIds, string TargetContainerId, string? TargetSlotId, int GridX, int GridY, bool DryRun);
public sealed record InventoryEditResult(InventoryDocument Document, IReadOnlyList<InventoryPlacement> Before, IReadOnlyList<InventoryPlacement> After, IReadOnlyList<string> Diagnostics, string Patch, string InversePatch);
public sealed record InventoryComparison(Guid ItemInstanceId, string Field, string Before, string After);
public sealed record ItemLifecycleEvent(Guid EventId, int SchemaVersion, string Provenance, ItemLifecycleEventKind Kind, Guid ItemInstanceId, string? SourceId, string? TargetId, DateTimeOffset OccurredAt, IReadOnlyDictionary<string, string> Context);

public static class InventoryAuthoringService
{
    public static InventoryEditResult Move(InventoryDocument document, InventoryEditCommand command, long expectedRevision)
    {
        if (document.Revision != expectedRevision) throw new InvalidOperationException("RPG_INVENTORY_REVISION_CONFLICT");
        if (command.ItemInstanceIds.Count == 0 || string.IsNullOrWhiteSpace(command.TargetContainerId)) throw new InvalidOperationException("RPG_INVENTORY_COMMAND_INVALID");
        var selected = document.Placements.Where(item => command.ItemInstanceIds.Contains(item.ItemInstanceId)).ToArray();
        if (selected.Length != command.ItemInstanceIds.Distinct().Count()) throw new InvalidOperationException("RPG_INVENTORY_ITEM_NOT_FOUND");
        var diagnostics = ValidateDrop(document, selected, command);
        if (diagnostics.Count > 0) return new(document, selected, selected, diagnostics, "", "");
        var updated = selected.Select((item, index) => item with { ContainerId = command.TargetContainerId, SlotId = command.TargetSlotId, GridX = command.GridX + index, GridY = command.GridY }).ToArray();
        var placements = document.Placements.Select(item => updated.FirstOrDefault(candidate => candidate.ItemInstanceId == item.ItemInstanceId) ?? item).ToArray();
        var next = command.DryRun ? document : new InventoryDocument(document.Revision + 1, placements);
        return new(next, selected, updated, [], $"move:{string.Join(',', command.ItemInstanceIds)}:{command.TargetContainerId}:{command.TargetSlotId}", $"restore:{string.Join(',', selected.Select(item => $"{item.ItemInstanceId}@{item.ContainerId}/{item.SlotId}"))}");
    }

    public static IReadOnlyList<InventoryPlacement> Filter(InventoryDocument document, string? containerId, string? tag, string? text)
        => document.Placements.Where(item => containerId is null || item.ContainerId == containerId).Where(item => tag is null || item.Tags.Contains(tag)).Where(item => string.IsNullOrWhiteSpace(text) || item.ItemInstanceId.ToString().Contains(text, StringComparison.OrdinalIgnoreCase) || item.Tags.Any(value => value.Contains(text, StringComparison.OrdinalIgnoreCase))).OrderBy(item => item.ContainerId, StringComparer.Ordinal).ThenBy(item => item.SlotId, StringComparer.Ordinal).ThenBy(item => item.GridY).ThenBy(item => item.GridX).ToArray();

    public static IReadOnlyList<InventoryComparison> Compare(IReadOnlyList<InventoryPlacement> before, IReadOnlyList<InventoryPlacement> after)
    {
        var result = new List<InventoryComparison>();
        foreach (var old in before)
        {
            var current = after.FirstOrDefault(item => item.ItemInstanceId == old.ItemInstanceId); if (current is null) { result.Add(new(old.ItemInstanceId, "existence", "present", "missing")); continue; }
            if (old.ContainerId != current.ContainerId) result.Add(new(old.ItemInstanceId, "container", old.ContainerId, current.ContainerId));
            if (old.SlotId != current.SlotId) result.Add(new(old.ItemInstanceId, "slot", old.SlotId ?? "", current.SlotId ?? ""));
            if (old.Quantity != current.Quantity) result.Add(new(old.ItemInstanceId, "quantity", old.Quantity.ToString(), current.Quantity.ToString()));
        }
        return result;
    }

    public static ItemLifecycleEvent Emit(ItemLifecycleEventKind kind, Guid itemInstanceId, string provenance, string? sourceId, string? targetId, DateTimeOffset occurredAt, IReadOnlyDictionary<string, string>? context = null)
    {
        if (itemInstanceId == Guid.Empty || string.IsNullOrWhiteSpace(provenance)) throw new InvalidOperationException("RPG_ITEM_EVENT_INVALID");
        return new(Guid.NewGuid(), 1, provenance, kind, itemInstanceId, sourceId, targetId, occurredAt, context ?? new Dictionary<string, string>());
    }

    private static IReadOnlyList<string> ValidateDrop(InventoryDocument document, IReadOnlyList<InventoryPlacement> selected, InventoryEditCommand command)
    {
        var diagnostics = new List<string>();
        if (command.GridX < 0 || command.GridY < 0) diagnostics.Add("RPG_INVENTORY_GRID_INVALID");
        if (command.TargetSlotId is not null && selected.Count > 1) diagnostics.Add("RPG_INVENTORY_SLOT_MULTI_EDIT_CONFLICT");
        var occupied = document.Placements.Any(item => !command.ItemInstanceIds.Contains(item.ItemInstanceId) && item.ContainerId == command.TargetContainerId && item.SlotId == command.TargetSlotId && item.GridX == command.GridX && item.GridY == command.GridY);
        if (occupied) diagnostics.Add("RPG_INVENTORY_TARGET_OCCUPIED");
        return diagnostics;
    }
}
