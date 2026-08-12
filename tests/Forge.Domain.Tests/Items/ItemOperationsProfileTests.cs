using Forge.Domain.Items;

namespace Forge.Domain.Tests.Items;

public sealed class ItemOperationsProfileTests
{
    [Fact]
    public void ValidatesCanonicalLootInspectorAndLifecycleBindings()
    {
        var foundation = Foundation();

        Assert.Empty(ItemFoundationValidator.Validate(foundation));
        Assert.Equal(9, foundation.Operations!.Events.Bindings.Count);
        Assert.Equal(2, foundation.Operations.Inspector.Placements.Count);
    }

    [Fact]
    public void RejectsInvalidLootAndIncompleteLifecycleBindingsWithLocalizedPaths()
    {
        var foundation = Foundation();
        var operations = foundation.Operations!;
        var invalid = foundation with
        {
            Operations = operations with
            {
                Loot = operations.Loot with
                {
                    Entries = [operations.Loot.Entries[0] with { Weight = 0 }],
                },
                Events = operations.Events with { Bindings = operations.Events.Bindings.Take(8).ToArray() },
            },
        };
        var diagnostics = ItemFoundationValidator.Validate(invalid);

        Assert.Contains(diagnostics, item => item.Code == "item.operations.loot_entry_invalid" &&
                                             item.Path.Contains("/loot/entries/", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == "item.operations.events_invalid" &&
                                             item.Path.EndsWith("/operations/events", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolvesOptionalAndRequiredOperationsProfiles()
    {
        Assert.True(ItemFoundationValidator.ResolveOperations(null).UsedFallback);
        Assert.Equal(ItemFoundationValidator.OperationsMissingRequiredCode,
            Assert.Single(ItemFoundationValidator.ResolveOperations(null, true).Diagnostics).Code);
    }

    internal static ItemFoundationProfile Foundation()
    {
        var foundation = ItemFoundationProfile.CreateDefault(
            Guid.Parse("c2900000-0000-4000-8000-000000000001"), "ash-key", "Clé de braise", "test:r090");
        var placements = new[]
        {
            new ItemInventoryPlacementPolicy(Guid.Parse("c2900000-0000-4000-8000-000000000011"),
                "backpack", null, 0, 0, 1, ["quest"], "asset.items.ash-key.preview"),
            new ItemInventoryPlacementPolicy(Guid.Parse("c2900000-0000-4000-8000-000000000012"),
                "backpack", null, 1, 0, 2, ["consumable"], "asset.items.ember.preview"),
        };
        var bindings = Enum.GetValues<ItemLifecycleEventType>().Select((kind, index) =>
            new ItemLifecycleBindingPolicy(
                Guid.Parse($"c2900000-0000-4000-8000-{(100 + index):000000000000}"), 1,
                "test:r090", kind, $"On{kind}", "server")).ToArray();
        return foundation with
        {
            Operations = new(Guid.Parse("c2900000-0000-4000-8000-000000000002"), 1, "test:r090", true,
                "crypt-of-ashes", "CC0-1.0", new string('a', 64),
                new(Guid.Parse("c2900000-0000-4000-8000-000000000003"), 1, "test:r090",
                [
                    new(Guid.Parse("c2900000-0000-4000-8000-000000000021"), "relic", 1, 1, 1, 2,
                        "crypt-open", true),
                    new(Guid.Parse("c2900000-0000-4000-8000-000000000022"), "relic", 3, 1, 3, 1,
                        "crypt-open", false),
                ]),
                new(Guid.Parse("c2900000-0000-4000-8000-000000000004"), 1, "test:r090", 8,
                    placements, true, true, true),
                new("1.0", bindings)),
        };
    }
}
