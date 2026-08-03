using Forge.RpgKit.Domain.Items;

namespace Forge.RpgKit.Domain.Tests.Items;

public sealed class ItemGameplayServiceTests
{
    [Fact]
    public void Enforces_transfer_destination_and_permissions()
    {
        var policy = new TransferPolicy(new Dictionary<TransferDestination, bool> { [TransferDestination.Trade] = true, [TransferDestination.Drop] = false }, new HashSet<string> { "item.transfer.quest" });
        Assert.False(ItemGameplayService.AuthorizeTransfer(policy, TransferDestination.Drop, new HashSet<string>(), "player").Allowed);
        Assert.False(ItemGameplayService.AuthorizeTransfer(policy, TransferDestination.Trade, new HashSet<string>(), "player").Allowed);
        Assert.True(ItemGameplayService.AuthorizeTransfer(policy, TransferDestination.Trade, new HashSet<string> { "item.transfer.quest" }, "player").Allowed);
    }

    [Fact]
    public void Resolves_data_driven_modifier_stacking_and_ability_events()
    {
        var modifiers = new[] { Modifier("light", 2, "sum", "night"), Modifier("light", 3, "sum", "night"), Modifier("ward", 8, "highest", "") };
        var resolved = ItemGameplayService.ResolveModifiers(modifiers, new HashSet<string> { "night" }); Assert.Equal(5, resolved["light"]); Assert.Equal(8, resolved["ward"]);
        var ability = new ItemAbilityBinding(Guid.NewGuid(), "point", new Dictionary<string, double> { ["focus"] = 1 }, 1, TimeSpan.FromSeconds(12), "ItemAbilityRequested", "ItemAbilityResolved");
        var usage = new UsageResource(3, 3, 3, 3, TimeSpan.Zero, null, 100, false, "rest");
        var activation = ItemGameplayService.ActivateAbility(Guid.NewGuid(), ability, "lighthouse", usage, DateTimeOffset.UnixEpoch);
        Assert.Equal("ItemAbilityRequested", activation.Requested.Name); Assert.Equal(2, activation.Usage.CurrentCharges); Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(12), activation.Usage.CooldownUntil);
    }

    [Fact]
    public void Previews_multislot_conflicts_and_rolls_seeded_weighted_loot()
    {
        var slots = new Dictionary<string, EquipmentSlotDefinition> { ["hand.r"] = new("hand.r", "forge", "humanoid", 2, new HashSet<string> { "hands.two" }), ["hand.l"] = new("hand.l", "forge", "humanoid", 2, new HashSet<string> { "hands.two" }) };
        var request = new EquipmentRequest(Guid.NewGuid(), new HashSet<string> { "hand.r", "hand.l" }, "forge", "humanoid");
        var preview = ItemGameplayService.PreviewEquipment(request, slots, new HashSet<string> { "hands.two" }); Assert.False(preview.Allowed); Assert.Equal(2, preview.Conflicts.Count);
        var lantern = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"); var shard = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var table = new LootTable(Guid.NewGuid(), 1, "author:test", [new(lantern, "relic", 1, 1, 1, 2, "lighthouse-open", true), new(shard, "relic", 3, 1, 3, 1, "lighthouse-open", false)]);
        var conditions = new HashSet<string> { "lighthouse-open" }; var first = ItemGameplayService.Roll(table, 42, conditions); var second = ItemGameplayService.Roll(table, 42, conditions);
        Assert.Equal(first, second); Assert.Equal(2, ItemGameplayService.Preview(table, conditions).Count); Assert.Single(first);
    }

    private static ItemModifierDefinition Modifier(string kind, double value, string stacking, string condition) => new(Guid.NewGuid(), 1, "author:test", kind, new Dictionary<string, double> { ["value"] = value }, stacking, condition, $"{kind} {value}", "before_item_modifier", "after_item_modifier");
}
