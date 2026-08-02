using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Quests;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Serialization;

public sealed class CryptOfAshesSampleTests
{
    [Fact]
    public void SampleProject_IsValidAndContainsPrototypeActors()
    {
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "crypt-of-ashes");
        var opened = ProjectSession.Open(fixtureDirectory);
        Assert.Empty(opened.Diagnostics);
        var project = Assert.IsType<ProjectSession>(opened.Session).Project;

        Assert.Equal("Crypte des Cendres — Tranche verticale", project.Title);
        Assert.Equal("crypt-of-ashes", project.Module.Slug);
        Assert.Equal(10, project.Blueprints.Count);
        var area = Assert.Single(project.Areas);
        Assert.Equal(area.Id, project.Module.EntryAreaId);
        Assert.Equal(16, area.Entities.Count);
        Assert.Contains(project.Blueprints, blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn);
        Assert.Contains(project.Blueprints, blueprint => blueprint.Slug == "ash-warden-boss");
        Assert.Contains(project.Blueprints, blueprint => blueprint.Slug == "ash-key");
        var pillar = Assert.Single(project.Blueprints, blueprint => blueprint.Slug == "crypt-pillar");
        Assert.Equal(4, area.Entities.Count(entity =>
            entity.BlueprintId == pillar.Id && entity.Tags.Contains("pillar", StringComparer.Ordinal)));
        var boss = Assert.Single(area.Entities, entity =>
            project.Blueprints.Single(blueprint => blueprint.Id == entity.BlueprintId).Slug == "ash-warden-boss");
        Assert.Equal(5, boss.Overrides.Properties.Count);
        Assert.Equal("180", boss.Overrides.Properties.Single(property => property.Key == "hitPoints").Value);
        Assert.Equal(
            "scripts/ash_warden_phase_two.forge",
            boss.Overrides.Properties.Single(property => property.Key == "phaseTwoScript").Value);
        var dialogue = Assert.Single(project.Dialogues);
        Assert.Equal("elyra-crypt-of-ashes", dialogue.Slug);
        Assert.Equal(4, dialogue.Nodes.Count);
        Assert.Contains(
            dialogue.Nodes.SelectMany(node => node.Choices).SelectMany(choice => choice.Conditions),
            condition => condition.Kind == DialogueConditionKind.HasItem && condition.Key == "ash-key");
        var quest = Assert.Single(project.Quests);
        Assert.Equal("crypt-of-ashes", quest.Slug);
        Assert.Equal(4, quest.States.Count);
        Assert.Equal(3, quest.Transitions.Count);
        Assert.Contains(
            quest.Transitions.SelectMany(transition => transition.Actions),
            action => action.Kind == QuestActionKind.UnlockEntity);
        var playerBlueprint = Assert.Single(project.Blueprints, blueprint => blueprint.Slug == "player-spawn");
        Assert.NotNull(playerBlueprint.Combat);
        Assert.Equal(12, playerBlueprint.Combat.Stats.MaximumHealth);
        Assert.Equal("heroes", playerBlueprint.Combat.Stats.Team);
        Assert.Equal("cue.hero-strike", Assert.Single(playerBlueprint.Combat.Abilities).VisualCue.CueKey);
        var bossBlueprint = Assert.Single(project.Blueprints, blueprint => blueprint.Slug == "ash-warden-boss");
        Assert.Equal(5, bossBlueprint.Combat!.Stats.MaximumHealth);
        Assert.Equal("hostile", bossBlueprint.Combat.Stats.Team);
        Assert.Equal("warden-ember", Assert.Single(bossBlueprint.Combat.Loot).ItemSlug);
    }
}
