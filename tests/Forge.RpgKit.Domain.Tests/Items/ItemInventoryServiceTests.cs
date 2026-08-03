using Forge.RpgKit.Domain.Items;

namespace Forge.RpgKit.Domain.Tests.Items;

public sealed class ItemInventoryServiceTests
{
    [Fact]
    public void Validates_visual_inventory_stack_and_usage_policies()
    {
        var id = Guid.NewGuid(); var bundle = Bundle(id);
        Assert.Empty(ItemInventoryService.Validate(bundle));
        var contribution = ItemInventoryService.Contribution(bundle.Inventory, 4);
        Assert.Equal(2.5, contribution.Mass, 6); Assert.Equal(1.2, contribution.Volume, 6);
        Assert.Equal("asset:lantern-equipped", bundle.Visual.EquippedPreviewAssetId);
    }

    [Fact]
    public void Merges_and_splits_stacks_atomically_with_replication_event()
    {
        var definition = Guid.NewGuid(); var policy = new StackPolicy(10, "lantern-oil", true);
        var merged = ItemInventoryService.Merge(new(Guid.NewGuid(), definition, "lantern-oil", 7, 3), new(Guid.NewGuid(), definition, "lantern-oil", 6, 3), policy, 3);
        Assert.Equal(10, merged.Primary.Quantity); Assert.Equal(3, merged.Secondary!.Quantity); Assert.Equal("ItemStackMergedReplicated", merged.EventName);
        var split = ItemInventoryService.Split(merged.Primary, 4, 4); Assert.Equal(6, split.Primary.Quantity); Assert.Equal(4, split.Secondary!.Quantity);
    }

    [Fact]
    public void Consumes_generic_usage_and_audits_critical_gm_override()
    {
        var now = DateTimeOffset.Parse("2026-08-03T12:00:00Z"); var usage = Bundle(Guid.NewGuid()).Usage;
        var consumed = ItemInventoryService.Consume(usage, now); Assert.Equal(2, consumed.CurrentCharges); Assert.Equal(now.AddSeconds(5), consumed.CooldownUntil);
        Assert.Throws<InvalidOperationException>(() => ItemInventoryService.Consume(consumed, now.AddSeconds(1)));
        var policy = Bundle(Guid.NewGuid()).Critical;
        Assert.False(ItemInventoryService.AuthorizeCriticalAction(policy, "sell", false, "player", "").Allowed);
        var allowed = ItemInventoryService.AuthorizeCriticalAction(policy, "sell", true, "gm:karen", "module recovery"); Assert.True(allowed.Allowed); Assert.Contains("module recovery", allowed.AuditEntry);
    }

    private static ItemPolicyBundle Bundle(Guid id) => new(id,
        new(id, ["asset:lantern-mesh"], ["asset:brass"], new HashSet<string> { "hand.r" }, "asset:lantern-icon", ["vfx:glow"], "asset:lantern-equipped"),
        new(1, 0.75, 1, 2, 0.5, 0.2), new(10, "lantern-oil", true),
        new(3, 3, 2, 2, TimeSpan.FromSeconds(5), null, 100, false, "rest-or-refill"), new(true, true, true, true, true, true));
}
