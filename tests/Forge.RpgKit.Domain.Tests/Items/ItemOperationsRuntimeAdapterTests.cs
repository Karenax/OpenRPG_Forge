using Forge.Domain.Items;
using Forge.RpgKit.Application.Items;
using Forge.RpgKit.Domain.Items;

namespace Forge.RpgKit.Domain.Tests.Items;

public sealed class ItemOperationsRuntimeAdapterTests
{
    [Fact]
    public void CanonicalOperationsReuseLootAndInventoryAuthoringServices()
    {
        var foundation = Foundation();
        var resolved = ItemOperationsRuntimeAdapter.Resolve(foundation);
        var conditions = new HashSet<string> { "crypt-open" };
        var first = ItemGameplayService.Roll(resolved.Loot!, 42, conditions);
        var second = ItemGameplayService.Roll(resolved.Loot!, 42, conditions);
        var preview = ItemGameplayService.Preview(resolved.Loot!, conditions);
        var placements = resolved.Inventory!.Placements;
        var move = InventoryAuthoringService.Move(resolved.Inventory,
            new(placements.Select(item => item.ItemInstanceId).ToArray(), "vault", null, 3, 2, true), 8);

        Assert.Equal(first, second);
        Assert.Single(first);
        Assert.Equal(2, preview.Count);
        Assert.Empty(move.Diagnostics);
        Assert.Equal(8, move.Document.Revision);
        Assert.Equal(2, InventoryAuthoringService.Compare(move.Before, move.After).Count);
        Assert.Equal(2, InventoryAuthoringService.Filter(resolved.Inventory, "backpack", null, null).Count);
    }

    [Fact]
    public void AdapterPreservesNineEventsAndOptionalRequiredResolution()
    {
        var foundation = Foundation();
        var resolved = ItemOperationsRuntimeAdapter.Resolve(foundation);
        var events = Enum.GetValues<ItemLifecycleEventKind>().Select(kind => ItemOperationsRuntimeAdapter.Emit(
            foundation.Operations!, kind, Guid.NewGuid(), "source", "target", DateTimeOffset.UnixEpoch)).ToArray();

        Assert.Equal(9, resolved.EventBindings.Count);
        Assert.Equal(9, events.Select(item => item.Kind).Distinct().Count());
        Assert.All(events, item => Assert.Contains("On", item.Provenance, StringComparison.Ordinal));
        Assert.True(ItemOperationsRuntimeAdapter.Resolve(null).OptionalFallback);
        Assert.Equal(ItemFoundationValidator.OperationsMissingRequiredCode,
            Assert.Single(ItemOperationsRuntimeAdapter.Resolve(null, true).Diagnostics).Code);
    }

    private static ItemFoundationProfile Foundation()
    {
        var foundation = ItemFoundationProfile.CreateDefault(
            Guid.Parse("c2900000-0000-4000-8000-000000000031"), "ash-key", "Clé de braise", "test:r090");
        var placements = new[]
        {
            new ItemInventoryPlacementPolicy(Guid.Parse("c2900000-0000-4000-8000-000000000041"),
                "backpack", null, 0, 0, 1, ["quest"], "asset.items.ash-key.preview"),
            new ItemInventoryPlacementPolicy(Guid.Parse("c2900000-0000-4000-8000-000000000042"),
                "backpack", null, 1, 0, 2, ["consumable"], "asset.items.ember.preview"),
        };
        var bindings = Enum.GetValues<ItemLifecycleEventType>().Select((kind, index) =>
            new ItemLifecycleBindingPolicy(
                Guid.Parse($"c2900000-0000-4000-8000-{(200 + index):000000000000}"), 1,
                "test:r090", kind, $"On{kind}", "server")).ToArray();
        return foundation with
        {
            Operations = new(Guid.Parse("c2900000-0000-4000-8000-000000000032"), 1, "test:r090", true,
                "crypt-of-ashes", "CC0-1.0", new string('a', 64),
                new(Guid.Parse("c2900000-0000-4000-8000-000000000033"), 1, "test:r090",
                [
                    new(Guid.Parse("c2900000-0000-4000-8000-000000000051"), "relic", 1, 1, 1, 2,
                        "crypt-open", true),
                    new(Guid.Parse("c2900000-0000-4000-8000-000000000052"), "relic", 3, 1, 3, 1,
                        "crypt-open", false),
                ]),
                new(Guid.Parse("c2900000-0000-4000-8000-000000000034"), 1, "test:r090", 8,
                    placements, true, true, true),
                new("1.0", bindings)),
        };
    }
}
