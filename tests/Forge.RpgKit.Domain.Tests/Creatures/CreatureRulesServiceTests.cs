using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.RpgKit.Domain.Tests.Creatures;

public sealed class CreatureRulesServiceTests
{
    [Fact]
    public void RulesProfile_ValidatesMitigatesAndPerceivesDeterministically()
    {
        var definition = RulesCreature();
        Assert.Empty(CreatureRulesService.Validate(definition));
        Assert.Equal(5, CreatureRulesService.Mitigate(definition.Defenses!, "glass", 10));
        Assert.Equal(0, CreatureRulesService.Mitigate(definition.Defenses!, "radiant", 10));
        var detected = CreatureRulesService.Detect(definition.Perception!, Vector3Value.Zero,
            [new("lantern", "light", new(2, 0, 0), 1), new("silent", "sound", new(3, 0, 0), .5)], 20);
        Assert.Equal(2, detected.Count); Assert.Equal("lantern", detected[0].StimulusId); Assert.Equal(50, detected[0].RememberUntilSeconds);
    }

    [Fact]
    public void InvalidComponents_ReportFiveStablePaths()
    {
        var invalid = RulesCreature() with { Defenses = null, Abilities = null, Inventory = null, Factions = null, Perception = null };
        var diagnostics = CreatureRulesService.Validate(invalid);
        Assert.Equal(5, diagnostics.Count); Assert.All(diagnostics, item => Assert.StartsWith($"creatures/{invalid.Id}/", item.Path));
    }

    private static CreatureDefinition RulesCreature() => new(Guid.NewGuid(), 1, "author:sample", BlueprintId.From(Guid.NewGuid()),
        new("ilyne", new("Ilyne", new Dictionary<string, string>()), [], "elle", new("", new Dictionary<string, string>()), "", new HashSet<string>()),
        new("scout", "sample", "rig.humanoid", "village", EncounterRole.Support, 3, "coast", new HashSet<string>()), TransformValue.Identity,
        [new("health", 30, 30, 0, true)], new(CreatureMovementMode.Grounded, 5, 10, 8, 0, 0, 45), "appearance.ilyne", "perception.ilyne", "combat.ilyne", null, "inventory.ilyne", new("spawn.ilyne", new Dictionary<string, string>()),
        Defenses: new(2, 1, new Dictionary<string, double> { ["glass"] = .4 }, new Dictionary<string, double> { ["reflex"] = 5 }, new HashSet<string> { "radiant" }, 0),
        Abilities: [new("lantern-flare", 1, 3, 6, ["has-lantern"], new HashSet<string> { "ranged", "support" }, "ability.lantern-flare")],
        Inventory: new([new("backpack", 8, 20, 30, ["lantern"])], new Dictionary<string, string> { ["main-hand"] = "lantern" }, "ilyne", true),
        Factions: new("village", [new("village", 1, "scout", 20, false), new("tide", .25, "friend", 5, true)]),
        Perception: new([new(SenseKind.Vision, 10, 120, true, new HashSet<string> { "light" }), new(SenseKind.Hearing, 8, 360, true, new HashSet<string> { "sound" })], 30));
}
