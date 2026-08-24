using Forge.Domain.Blueprints;
using Forge.RpgKit.Application.Creatures;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.RpgKit.Domain.Tests.Creatures;

public sealed class CreatureCombatAndAuthoringTests
{
    [Fact]
    public void Wizard_DryRunAndApplyProduceSameCreatureWithRevisionAndInverse()
    {
        var request = new CreatureWizardRequest(Guid.NewGuid(), 0, Guid.NewGuid(), BlueprintId.From(Guid.NewGuid()), "ilyne-eclaireuse", "Ilyne",
            new("archetype.scout", "ruleset.sample", "rig.humanoid", "faction.village", EncounterRole.Support, 3, "visual.coast", new HashSet<string> { "companion" }),
            "appearance.ilyne", "perception.scout", "combat.scout", "inventory.ilyne", false);
        var initial = new CreatureCatalogState(0, []);
        var preview = CreatureAuthoringService.CreateFromWizard(initial, request with { DryRun = true });
        var applied = CreatureAuthoringService.CreateFromWizard(initial, request);
        Assert.True(preview.Succeeded); Assert.False(preview.Applied); Assert.Equal(0, preview.State.Revision);
        Assert.True(applied.Applied); Assert.Equal(1, applied.NewRevision); Assert.Single(applied.State.Creatures);
        Assert.Equal(preview.Patch, applied.Patch); Assert.Contains(request.CreatureId.ToString(), applied.Inverse);
    }

    [Fact]
    public void DamageHooksAreBoundedAndDeathIsEmittedAfterTransaction()
    {
        var definition = CreatureFactoryTestsDefinition.Create();
        var ids = new Queue<Guid>([Guid.NewGuid(), Guid.NewGuid()]);
        var result = CreatureCombatService.ApplyDamage(definition, new Dictionary<string, double> { ["health"] = 12 },
            new(Guid.NewGuid(), definition.Id, "ilyne", 10, "radiant", "lantern"), [(_, amount) => amount * 2], ["ilyne", "player"], ["glass-shard"], "drop", () => ids.Dequeue());
        Assert.Equal(0, result.Resources["health"]); Assert.Equal(12, result.Applied.Applied); Assert.NotNull(result.Died);
        Assert.Equal("ilyne", result.Died!.KillerId); Assert.Equal(2, result.Died.Contributors.Count);
    }

    [Fact]
    public void ValidatorReportsMissingRigFactionScriptAndBudgetsWithPaths()
    {
        var definition = CreatureFactoryTestsDefinition.Create();
        var context = new CreatureValidationContext(new HashSet<string>(), new HashSet<string> { definition.AppearanceId }, new HashSet<string> { definition.CombatProfileId },
            new HashSet<string> { definition.InventoryProfileId }, new HashSet<string>(), new HashSet<string>(), 1, 1);
        var diagnostics = CreatureFactory.Diagnose(definition, context);
        Assert.Contains(diagnostics, item => item.Code == "RPG_CREATURE_RIG_MISSING" && item.Path.Contains(definition.Id.ToString()));
        Assert.Contains(diagnostics, item => item.Code == "RPG_CREATURE_FACTION_MISSING");
        Assert.Contains(diagnostics, item => item.Code == "RPG_CREATURE_SCRIPT_MISSING");
        Assert.Contains(diagnostics, item => item.Code == "RPG_CREATURE_RESOURCE_BUDGET_EXCEEDED");
        Assert.Contains(diagnostics, item => item.Code == "RPG_CREATURE_THREAT_BUDGET_EXCEEDED");
    }
}

internal static class CreatureFactoryTestsDefinition
{
    public static CreatureDefinition Create() => new(Guid.NewGuid(), 1, "author:test", BlueprintId.From(Guid.NewGuid()),
        new("glass-guard", new("Glass Guard", new Dictionary<string, string>()), [], "it", new("", new Dictionary<string, string>()), "", new HashSet<string>()),
        new("guard", "sample", "rig.guard", "glass", EncounterRole.Defender, 5, "glass", new HashSet<string>()), Forge.Domain.Spatial.TransformValue.Identity,
        [new("health", 20, 20, 0, true), new("morale", 10, 10, 0, true)], new(CreatureMovementMode.Grounded, 4, 8, 6, 0, 0, 45),
        "appearance.guard", "perception.guard", "combat.guard", null, "inventory.guard", new("spawn.guard", new Dictionary<string, string> { ["damaged"] = "damage.guard" }));
}
