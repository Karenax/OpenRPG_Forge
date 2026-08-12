using Forge.Domain.Items;
using Forge.RpgKit.Domain.Items;

namespace Forge.RpgKit.Domain.Tests.Items;

public sealed class ItemFoundationRuntimeAdapterTests
{
    [Fact]
    public void CanonicalProfileAdaptsToExistingInventoryStackAndUsageRuntime()
    {
        var profile = Profile();

        var resolved = ItemFoundationRuntimeAdapter.Resolve(profile);
        var policies = Assert.IsType<ItemPolicyBundle>(resolved.Policies);
        var contribution = ItemInventoryService.Contribution(policies.Inventory, 3);
        var merged = ItemInventoryService.Merge(
            new(Guid.NewGuid(), profile.Id, "ash-key", 7, 4),
            new(Guid.NewGuid(), profile.Id, "ash-key", 5, 4), policies.Stack, 4);
        var split = ItemInventoryService.Split(merged.Primary, 2, 5);
        var consumed = ItemInventoryService.Consume(policies.Usage, DateTimeOffset.UnixEpoch);

        Assert.Empty(resolved.Diagnostics);
        Assert.Equal(2.5, contribution.Mass, 6);
        Assert.Equal(10, merged.Primary.Quantity);
        Assert.Equal(8, split.Primary.Quantity);
        Assert.Equal(2, consumed.CurrentCharges);
        Assert.Equal(TimeSpan.FromSeconds(5), policies.Usage.Cooldown);
        Assert.Equal("asset.items.ash-key.equipped", policies.Visual.EquippedPreviewAssetId);
    }

    [Fact]
    public void AdapterPreservesOptionalAndRequiredMissingBehavior()
    {
        var optional = ItemFoundationRuntimeAdapter.Resolve(null);
        var required = ItemFoundationRuntimeAdapter.Resolve(null, required: true);

        Assert.True(optional.OptionalFallback);
        Assert.Equal(ItemFoundationValidator.CreationMissingRequiredCode,
            Assert.Single(required.Diagnostics).Code);
    }

    private static ItemFoundationProfile Profile()
    {
        var typeId = Guid.Parse("c2880000-0000-4000-8000-000000000011");
        return new(Guid.Parse("c2880000-0000-4000-8000-000000000012"), 1, "test:r088", true,
            "crypt-of-ashes", "CC0-1.0", new string('8', 64), typeId,
            new(typeId, 1, "test:r088", "ritual-key", ["hand"], ["utility"], "rig.hand"),
            "ash-key", "asset.items.ash-key", "rig.hand", 1, 2, 4, 75, ["hand"],
            [new(Guid.Parse("c2880000-0000-4000-8000-000000000013"), "utility", 1, "effect.ash-key", "OnUse")],
            [new("fr", "Clé", "Courte", "Longue", "Inconnu", "Clé", "")], ["identified"],
            new(Guid.Parse("c2880000-0000-4000-8000-000000000014"), 1, "test:r088", true,
                "crypt-of-ashes", "CC0-1.0", new string('9', 64),
                new(["archetype", "appearance", "slots", "quality", "level", "property", "provenance",
                    "generation-rules"], ["fixed-seed"]),
                new(Guid.Parse("c2880000-0000-4000-8000-000000000015"), 1, "test:r088",
                    ["asset.items.ash-key.mesh"], ["asset.materials.iron"], ["hand-r"],
                    "asset.items.ash-key.icon", ["asset.vfx.ash"], "asset.items.ash-key.equipped"),
                new(1.25, .4, 1, 2, .5, .25), new(10, "ash-key", true),
                new(3, 3, 2, 2, 5_000, 100, false, "rest-or-refill")));
    }
}
