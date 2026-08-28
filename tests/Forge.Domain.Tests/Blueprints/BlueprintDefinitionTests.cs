using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;

namespace Forge.Domain.Tests.Blueprints;

public sealed class BlueprintDefinitionTests
{
    [Fact]
    public void WithCreatureEvents_PreservesLaterBlueprintExtensions()
    {
        var id = BlueprintId.From(Guid.Parse("14d1fdd0-dae7-4d78-a4d5-03c0ef73b6d6"));
        var foundation = CreatureFoundationProfile.CreateDefault(
            Guid.Parse("d708ed99-bad8-481b-a966-08d9f7e31be1"),
            "ember-warden",
            "Ember Warden",
            "test:blueprint-mutation");
        var source = new BlueprintDefinition(
            id,
            "ember-warden",
            "Ember Warden",
            BlueprintKind.Creature,
            creatureFoundation: foundation);
        var events = CreatureEventProfile.CreateDefault(
            Guid.Parse("1f9ef8d7-d846-4c2e-a6f3-7aec67409f0e"),
            "test:blueprint-mutation");

        var updated = source.WithCreatureEvents(events);

        Assert.Same(events, updated.CreatureEvents);
        Assert.Same(foundation, updated.CreatureFoundation);
        Assert.Same(source.Authoring, updated.Authoring);
        Assert.Equal(source.Provenance, updated.Provenance);
    }
}
