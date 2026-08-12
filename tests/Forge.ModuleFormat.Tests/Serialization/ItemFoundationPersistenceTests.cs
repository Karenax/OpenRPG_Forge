using Forge.Domain.Blueprints;
using Forge.Domain.Items;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class ItemFoundationPersistenceTests
{
    [Fact]
    public void ItemFoundationRoundTripsWithTypeLocalizationAndOverridePolicy()
    {
        var project = TestProjectFactory.CreateValid();
        var original = project.Blueprints.First();
        var typeId = Guid.Parse("c2450000-0000-4000-8000-000000000001");
        var profile = new ItemFoundationProfile(Guid.Parse("c2450000-0000-4000-8000-000000000002"), 1,
            "test:r044", true, "crypt-of-ashes", "CC0-1.0", new string('c', 64), typeId,
            new(typeId, 1, "test:r044", "ritual-key", ["hand"], ["utility"], "rig.hand"),
            original.Slug, "asset.items.ash-key", "rig.hand", 2, 1, 3, 40, ["hand"],
            [new(Guid.Parse("c2450000-0000-4000-8000-000000000003"), "utility", 1, "effect.key", "OnUse")],
            [new("fr", "Clé", "Courte", "Longue", "Inconnu", "Clé", "Notes")],
            ["identified"],
            new(Guid.Parse("c2880000-0000-4000-8000-000000000031"), 1, "test:r088", true,
                "crypt-of-ashes", "CC0-1.0", new string('8', 64),
                new(["archetype", "appearance", "slots", "quality", "level", "property", "provenance",
                    "generation-rules"], ["fixed-seed", "preserve-provenance"]),
                new(Guid.Parse("c2880000-0000-4000-8000-000000000032"), 1, "test:r088",
                    ["asset.items.ash-key.mesh"], ["asset.materials.iron"], ["hand-r"],
                    "asset.items.ash-key.icon", ["asset.vfx.ash"], "asset.items.ash-key.equipped"),
                new(1.25, .4, 1, 2, .5, .25), new(10, "ash-key", true),
                new(3, 3, 2, 2, 5_000, 100, false, "rest-or-refill")));
        profile = profile with { Gameplay = Gameplay(), Operations = Operations() };
        var item = new BlueprintDefinition(original.Id, original.Slug, original.DisplayName, BlueprintKind.Item,
            original.Combat, original.PaletteOrigin, original.IsFavorite, original.Provenance,
            itemFoundation: profile);
        var aggregate = new Forge.Domain.Projects.ProjectDefinition(project.Id, project.Title, project.Module,
            [item, .. project.Blueprints.Skip(1)], project.Areas, project.Dialogues, project.Quests);

        var json = ProjectJsonSerializer.Serialize(aggregate);
        var restored = ProjectJsonSerializer.Deserialize(json);
        var roundTrip = restored.Blueprints.First().ItemFoundation!;

        Assert.Equal(json, ProjectJsonSerializer.Serialize(restored));
        Assert.Equal(profile.TypeId, roundTrip.TypeId);
        Assert.Equal("Clé", Assert.Single(roundTrip.Texts).Name);
        Assert.Contains("identified", roundTrip.AllowedInstanceOverrides);
        Assert.Equal("asset.items.ash-key.equipped", roundTrip.Creation!.Appearance.EquippedPreviewAssetId);
        Assert.Equal("ash-key", roundTrip.Creation.Stack.CompatibilityKey);
        Assert.Equal(5_000, roundTrip.Creation.Usage.CooldownMilliseconds);
        Assert.True(roundTrip.Gameplay!.Critical.ProtectDestruction);
        Assert.Equal(8, roundTrip.Gameplay.Transfer.AllowedDestinations.Count);
        Assert.Equal("ward", Assert.Single(roundTrip.Gameplay.Modifiers).Kind);
        Assert.Equal(2, roundTrip.Gameplay.Equipment.RequiredSlots.Count);
        Assert.Equal(profile.Operations!.Loot.Id, roundTrip.Operations!.Loot.Id);
        Assert.Equal(9, roundTrip.Operations.Events.Bindings.Count);
        Assert.Equal("asset.items.ash-key.preview",
            Assert.Single(roundTrip.Operations.Inspector.Placements).PreviewAssetId);
    }

    private static ItemGameplayProfile Gameplay() => new(
        Guid.Parse("c2890000-0000-4000-8000-000000000031"), 1, "test:r089", true,
        "crypt-of-ashes", "CC0-1.0", new string('9', 64), new(true, true, true, true, true, true, true),
        new(Enum.GetValues<ItemTransferDestination>().ToDictionary(item => item,
            item => item is ItemTransferDestination.Container or ItemTransferDestination.GameMaster),
            ["item.transfer.quest"]),
        [new(Guid.Parse("c2890000-0000-4000-8000-000000000032"), 1, "test:r089", "ward",
            new Dictionary<string, double> { ["value"] = 8 }, "highest", "night", "+8 ward",
            "BeforeItemModifier", "AfterItemModifier")],
        new(Guid.Parse("c2890000-0000-4000-8000-000000000033"), "point",
            new Dictionary<string, double> { ["focus"] = 1 }, 1, 12_000,
            "ItemAbilityRequested", "ItemAbilityResolved"),
        new([new("hand-r", "forge", "humanoid", 2, ["hands-two"]),
            new("hand-l", "forge", "humanoid", 2, ["hands-two"]),
            new("hands-two", "forge", "humanoid", 3, ["hand-r", "hand-l"])], ["hand-r", "hand-l"]));

    private static ItemOperationsProfile Operations() => new(Guid.NewGuid(), 1, "test:r090", true,
        "crypt-of-ashes", "CC0-1.0", new string('7', 64),
        new(Guid.NewGuid(), 1, "test:r090",
        [new(Guid.NewGuid(), "relic", 1, 1, 1, 2, "crypt-open", true)]),
        new(Guid.NewGuid(), 1, "test:r090", 0,
        [new(Guid.NewGuid(), "backpack", null, 0, 0, 1, ["quest"], "asset.items.ash-key.preview")],
            true, true, true),
        new("1.0", Enum.GetValues<ItemLifecycleEventType>().Select(kind =>
            new ItemLifecycleBindingPolicy(Guid.NewGuid(), 1, "test:r090", kind, $"On{kind}", "server")).ToArray()));
}
