using Forge.RpgKit.Application.Items;

namespace Forge.RpgKit.Domain.Tests.Items;

public sealed class InventoryAuthoringServiceTests
{
    [Fact]
    public void Supports_drag_drop_validation_preview_and_multi_edit()
    {
        var first = Placement("backpack", 0, "quest"); var second = Placement("backpack", 1, "consumable"); var document = new InventoryDocument(8, [first, second]);
        var preview = InventoryAuthoringService.Move(document, new([first.ItemInstanceId, second.ItemInstanceId], "vault", null, 3, 2, true), 8);
        Assert.Equal(8, preview.Document.Revision); Assert.Empty(preview.Diagnostics); Assert.Equal(2, preview.After.Count); Assert.Equal("asset:preview", preview.After[0].PreviewAssetId);
        var applied = InventoryAuthoringService.Move(document, new([first.ItemInstanceId, second.ItemInstanceId], "vault", null, 3, 2, false), 8);
        Assert.Equal(9, applied.Document.Revision); Assert.Equal(2, InventoryAuthoringService.Compare(applied.Before, applied.After).Count);
    }

    [Fact]
    public void Filters_and_rejects_conflicting_slot_drop()
    {
        var first = Placement("backpack", 0, "quest"); var second = Placement("backpack", 1, "consumable"); var document = new InventoryDocument(0, [first, second]);
        Assert.Equal(first.ItemInstanceId, Assert.Single(InventoryAuthoringService.Filter(document, "backpack", "quest", "ques")).ItemInstanceId);
        var invalid = InventoryAuthoringService.Move(document, new([first.ItemInstanceId, second.ItemInstanceId], "equipment", "hand.r", 0, 0, false), 0);
        Assert.Contains("RPG_INVENTORY_SLOT_MULTI_EDIT_CONFLICT", invalid.Diagnostics); Assert.Equal(0, invalid.Document.Revision);
    }

    [Fact]
    public void Emits_every_canonical_item_lifecycle_event()
    {
        var itemId = Guid.NewGuid(); var at = DateTimeOffset.UnixEpoch;
        var events = Enum.GetValues<ItemLifecycleEventKind>().Select(kind => InventoryAuthoringService.Emit(kind, itemId, "runtime:test", "source", "target", at)).ToArray();
        Assert.Equal(9, events.Length); Assert.Equal(9, events.Select(item => item.Kind).Distinct().Count()); Assert.All(events, item => Assert.Equal(1, item.SchemaVersion));
    }

    private static InventoryPlacement Placement(string container, int x, string tag) => new(Guid.NewGuid(), container, null, x, 0, 1, new HashSet<string> { tag }, "asset:preview");
}
