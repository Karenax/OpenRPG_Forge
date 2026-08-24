using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Application.Tests.WorldFeatures;

public sealed class TriggerLivingWorldWorkflowTests
{
    [Fact]
    public void Ux2LwE_CommandProjectsPoliciesAndPreservesAllWorldFeatureFamilies()
    {
        var fixture = Fixture();
        var state = new ProjectState(fixture.Project, new ProjectRevision(4));
        var handler = new SetWorldFeatureCatalogHandler(new FixedClock(DateTimeOffset.UnixEpoch));
        var command = new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), state.Revision,
            fixture.Project.Module.WorldFeatures, true);

        var preview = handler.Handle(state, command);
        var applied = handler.Handle(state, command with { DryRun = false });
        var history = new ProjectCommandHistory(state);
        history.Record(applied);
        var handle = Assert.Single(new WorldFeatureScriptService().Query(
            history.State, "trigger", fixture.Trigger.Id.ToString("D"), 1));

        Assert.True(preview.Succeeded);
        Assert.Same(state, preview.State);
        Assert.True(applied.Succeeded);
        Assert.Equal("True", handle.Properties["rest"]);
        Assert.Equal("danger.nearby", handle.Properties["restDangerQuery"]);
        Assert.Equal("tracking", handle.Properties["clueSkill"]);
        Assert.Equal("trail.ash", handle.Properties["clueTrace"]);
        Assert.Equal("party-vote", handle.Properties["narrativeMultiplayer"]);
        Assert.Equal(fixture.DialogueId.ToString("D"), handle.Properties["narrativeDialogue"]);
        Assert.Same(fixture.Project.Module.WorldFeatures.Placeables,
            applied.State.Project.Module.WorldFeatures.Placeables);
        Assert.Same(fixture.Project.Module.WorldFeatures.Doors,
            applied.State.Project.Module.WorldFeatures.Doors);
        Assert.Same(fixture.Project.Module.WorldFeatures.Encounters,
            applied.State.Project.Module.WorldFeatures.Encounters);
        Assert.Same(fixture.Project.Module.WorldFeatures.VehicleRoutes,
            applied.State.Project.Module.WorldFeatures.VehicleRoutes);
        Assert.Same(fixture.Project.Module.WorldFeatures.Merchants,
            applied.State.Project.Module.WorldFeatures.Merchants);
        Assert.Same(fixture.Project.Module.WorldFeatures.Companions,
            applied.State.Project.Module.WorldFeatures.Companions);
        Assert.Same(fixture.Project.Module.WorldFeatures.Scripts,
            applied.State.Project.Module.WorldFeatures.Scripts);
        Assert.True(history.Undo().Succeeded);
        Assert.True(history.Redo().Succeeded);
    }

    [Fact]
    public void Ux2LwE_ValidatorLocalizesRestClueNarrativeAndParticipantFailures()
    {
        var fixture = Fixture();
        var invalid = fixture.Trigger with
        {
            Rest = fixture.Trigger.Rest! with { DangerQuery = string.Empty, AllowedMessageKey = "bad key" },
            Clue = fixture.Trigger.Clue! with
            {
                SkillId = string.Empty,
                Difficulty = -1,
                DirectionDegrees = 360,
                AgeHours = -1,
                FalseClueId = fixture.Trigger.Clue.TraceId,
            },
            Narrative = fixture.Trigger.Narrative! with
            {
                Participants = [BlueprintId.From(Guid.Parse("e0160000-0000-4000-8000-000000000099"))],
                Priority = -1,
                CooldownSeconds = -1,
                MultiplayerPolicy = string.Empty,
            },
        };
        var source = fixture.Project.Module.WorldFeatures;
        var catalog = new WorldFeatureCatalog(source.Id, source.SchemaVersion, source.Provenance,
            source.Placeables, source.Doors, [invalid], source.Encounters, source.VehicleRoutes,
            source.Merchants, source.Companions, source.Scripts);
        var project = new ProjectDefinition(fixture.Project.Id, fixture.Project.Title,
            fixture.Project.Module.WithWorldFeatures(catalog), fixture.Project.Blueprints,
            fixture.Project.Areas, fixture.Project.Dialogues, fixture.Project.Quests);

        var diagnostics = WorldFeatureCatalogValidator.Validate(project);

        Assert.Contains(diagnostics, item => item.Code == "world.trigger.rest_invalid" &&
            item.Path.EndsWith("/rest", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == "world.trigger.clue_invalid" &&
            item.Path.EndsWith("/clue", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == "world.trigger.narrative_invalid" &&
            item.Path.EndsWith("/narrative", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == "world.trigger.narrative_participant_missing" &&
            item.Path.Contains("/narrative/participants/", StringComparison.Ordinal));
    }

    private static LivingWorldFixture Fixture()
    {
        var narrativeProject = OriginalModuleAuthoringService.CreateVeilleeDesEclats().State.Project;
        var dialogue = narrativeProject.Dialogues.Single();
        var participant = dialogue.Nodes.Select(item => item.SpeakerBlueprintId).First(item => item is not null)!;
        var triggerBlueprint = new BlueprintDefinition(
            BlueprintId.From(Guid.Parse("e0160000-0000-4000-8000-000000000001")),
            "living-world-trigger",
            "Living world trigger",
            BlueprintKind.Trigger);
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("e0160000-0000-4000-8000-000000000002")),
            "living-world",
            "Living world",
            []);
        var shell = new ProjectDefinition(
            ProjectId.From(Guid.Parse("e0160000-0000-4000-8000-000000000003")),
            "UX2-LW-E",
            [triggerBlueprint, .. narrativeProject.Blueprints],
            [area]);
        var trigger = new TriggerWorldProfile(
            Guid.Parse("e0160000-0000-4000-8000-000000000004"),
            1,
            "test:ux2-lw-e",
            triggerBlueprint.Id,
            area.Id,
            null,
            null,
            null,
            "trigger.living-world",
            null,
            new(true, "danger.nearby", true, true, "rest.allowed", "rest.denied"),
            new("tracking", 8, "trail.ash", 45, 2, "trail.decoy", null),
            new([participant], 4, true, 60, "party-vote", dialogue.Id.Value),
            null);
        var catalog = new WorldFeatureCatalog(shell.Module.Id.Value, 1, "test:ux2-lw-e",
            triggers: [trigger]);
        var project = new ProjectDefinition(shell.Id, shell.Title,
            shell.Module.WithWorldFeatures(catalog), shell.Blueprints, shell.Areas,
            [dialogue], shell.Quests);
        return new(project, trigger, dialogue.Id.Value);
    }

    private sealed record LivingWorldFixture(
        ProjectDefinition Project,
        TriggerWorldProfile Trigger,
        Guid DialogueId);
}
