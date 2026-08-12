using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.RpgKit.Application.Creatures;
using Forge.RpgKit.Domain.Creatures;

namespace Forge.RpgKit.Domain.Tests.Creatures;

public sealed class CreatureRulesCompositionFoundationServiceTests
{
    [Fact]
    public void CanonicalRulesBuildValidatedRpgKitCreatureWithoutSecondStorage()
    {
        var creatureId = BlueprintId.From(Guid.Parse("a1781000-0000-4000-8000-000000000001"));
        var itemId = BlueprintId.From(Guid.Parse("a1781000-0000-4000-8000-000000000002"));
        var project = Project(creatureId, itemId, CompleteFoundation(itemId));

        var result = CreatureRulesCompositionFoundationService.Resolve(project, creatureId);

        Assert.True(result.Succeeded);
        Assert.False(result.UsedFallback);
        Assert.Equal(CreatureInitialLifeState.Seated, result.Creature!.InitialState!.LifeState);
        Assert.Equal(16, CreatureCompositionService.EvaluateAttributes(result.Creature.Attributes!)["evasion"]);
        Assert.Equal(5, CreatureRulesService.Mitigate(result.Creature.Defenses!, "glass", 10));
        Assert.Equal("ability.ash-nova", Assert.Single(result.Creature.Abilities!).AbilityId);
        Assert.Equal(itemId.ToString(), Assert.Single(Assert.Single(result.Creature.Inventory!.Containers).ItemIds));
    }

    [Fact]
    public void MissingOptionalRulesAreIgnoredAndRequiredRulesAreDiagnosed()
    {
        var creatureId = BlueprintId.From(Guid.NewGuid());
        var itemId = BlueprintId.From(Guid.NewGuid());
        var source = CreatureFoundationProfile.CreateDefault(Guid.NewGuid(), "warden", "Gardien", "author:r078");
        var authoring = CreatureAuthoringCompositionProfile.CreateDefault(Guid.NewGuid(), "author:r078");
        var optional = new CreatureFoundationProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RequiredAtRuntime, source.Identity, source.DefaultTransform, source.Resources, source.Movement,
            source.References, source.Spawned, source.Overrides, authoring, true);
        Assert.True(CreatureRulesCompositionFoundationService.Resolve(Project(creatureId, itemId, optional), creatureId)
            .Succeeded);
        Assert.True(CreatureRulesCompositionFoundationService.Resolve(Project(creatureId, itemId, optional), creatureId)
            .UsedFallback);

        var required = new CreatureFoundationProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RequiredAtRuntime, source.Identity, source.DefaultTransform, source.Resources, source.Movement,
            source.References, source.Spawned, source.Overrides, authoring, true, null, true);
        var result = CreatureRulesCompositionFoundationService.Resolve(Project(creatureId, itemId, required), creatureId);
        Assert.False(result.Succeeded);
        Assert.Equal(CreatureRulesCompositionValidator.MissingRequiredCode, Assert.Single(result.Diagnostics).Code);
    }

    private static CreatureFoundationProfile CompleteFoundation(BlueprintId itemId)
    {
        var source = CreatureFoundationProfile.CreateDefault(Guid.NewGuid(), "warden", "Gardien", "author:r078");
        return new(source.Id, source.SchemaVersion, source.Provenance, true, source.Identity,
            source.DefaultTransform, source.Resources, source.Movement, source.References, source.Spawned,
            source.Overrides, CreatureAuthoringCompositionProfile.CreateDefault(Guid.NewGuid(), "author:r078"), true,
            CompleteRules(itemId), true);
    }

    internal static CreatureRulesCompositionProfile CompleteRules(BlueprintId itemId) => new(
        Guid.Parse("a1781000-0000-4000-8000-000000000010"), 1, "author:r078", "asset.rules.warden",
        TransformValue.Identity,
        new(Guid.Parse("a1781000-0000-4000-8000-000000000011"), CreatureRulesInitialLifeState.Seated,
            "clip.warden.seated", "pose.warden-throne", false),
        new(Guid.Parse("a1781000-0000-4000-8000-000000000012"), "ruleset.crypt",
            new Dictionary<string, double> { ["strength"] = 8, ["agility"] = 6 },
            [new(Guid.Parse("a1781000-0000-4000-8000-000000000013"), "agility", "species.emberborn", 1)],
            [new(Guid.Parse("a1781000-0000-4000-8000-000000000014"), "evasion",
                CreatureRulesAttributeFormulaOperation.Sum, ["agility", "strength"], 1)]),
        new(Guid.Parse("a1781000-0000-4000-8000-000000000015"), 4, 2,
            new Dictionary<string, double> { ["glass"] = .2 },
            new Dictionary<string, double> { ["fortitude"] = 6 }, ["radiant"], 1),
        [new(Guid.Parse("a1781000-0000-4000-8000-000000000016"), "ability.ash-nova", 3, 2, 12,
            ["state.alive"], ["boss"], "script.ash-nova")],
        new(Guid.Parse("a1781000-0000-4000-8000-000000000017"),
            [new(Guid.Parse("a1781000-0000-4000-8000-000000000018"), "container.backpack", 8, 100, 100,
                [new(Guid.Parse("a1781000-0000-4000-8000-000000000019"), itemId, 1, 3, 2)])],
            new Dictionary<string, Guid>
            {
                ["main-hand"] = Guid.Parse("a1781000-0000-4000-8000-000000000019"),
            }, "creature.warden", true), new Dictionary<string, string>());

    private static ProjectDefinition Project(BlueprintId creatureId, BlueprintId itemId,
        CreatureFoundationProfile foundation) => new(ProjectId.From(Guid.NewGuid()), "R078",
        [
            new BlueprintDefinition(creatureId, "warden", "Gardien", BlueprintKind.Creature,
                creatureFoundation: foundation),
            new BlueprintDefinition(itemId, "ember-blade", "Lame", BlueprintKind.Item),
        ], [new AreaDefinition(AreaId.From(Guid.NewGuid()), "lab", "Lab", [])]);
}
