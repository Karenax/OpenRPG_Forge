using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.RpgKit.Domain.Tests.Creatures;

public sealed class CreatureCompositionServiceTests
{
    [Fact]
    public void CompleteComposition_ValidatesAndEvaluatesInspectableAttributes()
    {
        var definition = Complete();
        Assert.Empty(CreatureCompositionService.ValidateComposition(definition));
        var values = CreatureCompositionService.EvaluateAttributes(definition.Attributes!);
        Assert.Equal(7, values["agility"]);
        Assert.Equal(13, values["evasion"]);
        Assert.Equal("rig.humanoid", definition.Species!.RequiredSkeletonId);
        Assert.Contains("wave", definition.Animations!.Emotes);
        Assert.Equal(CreatureInitialLifeState.Seated, definition.InitialState!.LifeState);
    }

    [Fact]
    public void BrokenComposition_ReportsEverySpecializedPath()
    {
        var broken = Complete() with
        {
            Species = new("human", ["human"], [new("a", new HashSet<string>()), new("a", new HashSet<string>())], [], ""),
            Visual = null,
            Animations = null,
            InitialState = new(CreatureInitialLifeState.Dead, null, null, false),
            Attributes = null
        };
        var diagnostics = CreatureCompositionService.ValidateComposition(broken);
        Assert.Equal(5, diagnostics.Count);
        Assert.All(diagnostics, diagnostic => Assert.StartsWith($"creatures/{broken.Id}/", diagnostic.Path));
    }

    private static CreatureDefinition Complete() => new(Guid.NewGuid(), 1, "author:sample", BlueprintId.From(Guid.NewGuid()),
        new("ilyne", new("Ilyne", new Dictionary<string, string>()), [], "elle", new("Éclaireuse", new Dictionary<string, string>()), "", new HashSet<string>()),
        new("scout", "sample", "rig.humanoid", "village", EncounterRole.Support, 3, "coast", new HashSet<string>()), TransformValue.Identity,
        [new("health", 30, 30, .2, true)], new(CreatureMovementMode.Grounded, 6, 10, 8, 0, 0, 50), "appearance.ilyne", "perception.scout", "combat.scout", "dialogue.ilyne", "inventory.ilyne", new("spawn.ilyne", new Dictionary<string, string>()),
        new("human.coast", ["human"], [new("coastal", new HashSet<string> { "salt-resistant" })], ["islander"], "rig.humanoid"),
        new("visual.ilyne", "mesh.ilyne", "rig.humanoid", new Dictionary<string, double> { ["height"] = .1 }, new Dictionary<string, string> { ["head"] = "hood" }, ["cloth"], ["coast"], ["lantern"], "ilyne.default"),
        new("anim.ilyne", ["idle", "walk", "run"], ["ranged"], ["wave"], ["breathing"], "retarget.humanoid"),
        new(CreatureInitialLifeState.Seated, "idle.seated", "pose.stool", false),
        new(new Dictionary<string, double> { ["agility"] = 6, ["awareness"] = 5 }, new Dictionary<string, IReadOnlyList<AttributeModifier>> { ["agility"] = [new("species", 1)] }, [new("evasion", AttributeFormulaOperation.Sum, ["agility", "awareness"], 1)]));
}
