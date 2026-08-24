using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.RpgKit.Domain.Tests.Creatures;

public sealed class CreatureFactoryTests
{
    [Fact]
    public void Creature_IsComposableValidatedAndSpawnedOnlyAfterCompleteConstruction()
    {
        var definition = CreateDefinition(CreatureMovementMode.Flying);
        Assert.Same(definition, CreatureFactory.Validate(definition));
        var spawn = CreatureFactory.Spawn(definition,
            new CreatureSpawnContext(Guid.NewGuid(), "village-des-eclats", TransformValue.Identity, 4201, CreatureSpawnAuthority.Server, "encounter.wave.1"),
            () => Guid.Parse("20000000-0000-4000-8000-000000000001"));
        Assert.Equal(definition.Id, spawn.CreatureDefinitionId);
        Assert.Equal(4201, spawn.Context.Seed);
        Assert.Equal(CreatureSpawnAuthority.Server, spawn.Context.Authority);
        Assert.Equal(48, spawn.ResourceState["health"]);
        Assert.Equal("creature.spawned", definition.Scripts.OnSpawn);
    }

    [Theory]
    [InlineData(CreatureMovementMode.Grounded)]
    [InlineData(CreatureMovementMode.Flying)]
    [InlineData(CreatureMovementMode.Swimming)]
    [InlineData(CreatureMovementMode.Climbing)]
    [InlineData(CreatureMovementMode.Vehicle)]
    [InlineData(CreatureMovementMode.ZeroG)]
    public void AllMovementModes_AreCanonical(CreatureMovementMode mode) => Assert.Equal(mode, CreatureFactory.Validate(CreateDefinition(mode)).Movement.Mode);

    [Fact]
    public void InvalidResource_ReturnsStableDiagnosticCode()
    {
        var invalid = CreateDefinition(CreatureMovementMode.Grounded) with { Resources = [new ResourcePool("health", 10, 11, 0, true)] };
        Assert.Equal("RPG_CREATURE_RESOURCES_INVALID", Assert.Throws<InvalidOperationException>(() => CreatureFactory.Validate(invalid)).Message);
    }

    private static CreatureDefinition CreateDefinition(CreatureMovementMode mode) => new(
        Guid.Parse("10000000-0000-4000-8000-000000000001"), 1, "author:sample",
        BlueprintId.From(Guid.Parse("10000000-0000-4000-8000-000000000002")),
        new CreatureIdentity("sentinelle-de-verre", new("Sentinelle de verre", new Dictionary<string, string> { ["en"] = "Glass Sentinel" }),
            ["sentinelle"], "elle", new("Une gardienne née de la tempête.", new Dictionary<string, string>()), "Antagoniste du phare", new HashSet<string> { "phare", "verre" }),
        new("archetype.sentinel", "ruleset.sample", "rig.humanoid", "faction.glass", EncounterRole.Skirmisher, 4.8, "visual.glass", new HashSet<string> { "ranged" }),
        TransformValue.Identity,
        [new("health", 60, 48, 0.5, true), new("morale", 20, 20, 0.1, true)],
        new(mode, 6, 12, 8, 0, mode == CreatureMovementMode.Flying ? 30 : 0, 50),
        "appearance.glass-sentinel", "perception.guard", "combat.skirmisher", null, "inventory.shards",
        new("creature.spawned", new Dictionary<string, string>()));
}
