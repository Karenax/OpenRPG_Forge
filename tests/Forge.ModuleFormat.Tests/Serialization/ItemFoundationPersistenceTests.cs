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
            ["identified"]);
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
    }
}
