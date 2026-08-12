using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Creatures;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Tests.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Factions;
using Forge.Domain.Lifecycle;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Blueprints;

public sealed class SetCreatureFoundationHandlerTests
{
    private static readonly BlueprintId CreatureId = BlueprintId.From(Guid.Parse("a1610000-0000-4000-8000-000000000001"));
    private readonly SetCreatureFoundationHandler _handler = new(new FixedClock(DateTimeOffset.UnixEpoch));

    [Fact]
    public void DryRunApplyUndoRedoAndScriptReadUseOneCanonicalMutation()
    {
        var history = new ProjectCommandHistory(CreateState());
        var profile = Complete();
        var command = new SetCreatureFoundationCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            CreatureId, profile, true);
        var preview = _handler.Handle(history.State, command);
        Assert.True(preview.Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single().CreatureFoundation);
        Assert.Contains("2 resource pools", preview.Patch!.Summary);

        var applied = _handler.Handle(history.State, command with { DryRun = false });
        history.Record(applied);
        Assert.Equal(profile.Id, history.State.Project.Blueprints.Single().CreatureFoundation!.Id);
        var scriptView = CreatureFoundationScriptService.Read(history.State.Project, CreatureId);
        Assert.Equal("Flying", scriptView!.MovementMode);
        Assert.Equal("OnSpawned", scriptView.SpawnHandler);
        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single().CreatureFoundation);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(profile.Id, history.State.Project.Blueprints.Single().CreatureFoundation!.Id);
    }

    [Fact]
    public void InvalidSlugIsRejectedBeforeDocumentMutation()
    {
        var state = CreateState();
        var source = Complete();
        var invalid = new Forge.Domain.Creatures.CreatureFoundationProfile(source.Id, source.SchemaVersion,
            source.Provenance, source.RequiredAtRuntime,
            source.Identity with { Slug = "another-creature" }, source.DefaultTransform, source.Resources,
            source.Movement, source.References, source.Spawned, source.Overrides);
        var result = _handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision, CreatureId, invalid, false));
        Assert.Equal("creature.foundation.slug_mismatch", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(state.Revision, result.State.Revision);
    }

    [Fact]
    public void AuthoringCompositionUsesSameDryRunApplyUndoRedoAndTypedScriptHandle()
    {
        var history = new ProjectCommandHistory(CreateState());
        var source = Complete();
        var profile = new CreatureFoundationProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RequiredAtRuntime, source.Identity, source.DefaultTransform, source.Resources, source.Movement,
            source.References, source.Spawned, source.Overrides,
            CreatureAuthoringCompositionProfile.CreateDefault(Guid.NewGuid(), "author:r077"), true);
        var command = new SetCreatureFoundationCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            CreatureId, profile, true);

        var preview = _handler.Handle(history.State, command);
        var applied = _handler.Handle(history.State, command with { DryRun = false });
        history.Record(applied);
        var view = CreatureAuthoringCompositionScriptService.Read(history.State.Project,
            CreatureAuthoringCompositionScriptService.CreateHandle(CreatureId));

        Assert.True(preview.Succeeded);
        Assert.True(applied.Succeeded);
        Assert.Equal(preview.Patch, applied.Patch);
        Assert.Equal("species.human", view!.SpeciesId);
        Assert.Equal("mesh.guardian", view.MeshAssetId);
        Assert.Contains("guard", view.AnimationStates);
        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single().CreatureFoundation);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(profile.AuthoringComposition!.Id,
            history.State.Project.Blueprints.Single().CreatureFoundation!.AuthoringComposition!.Id);
    }

    [Fact]
    public void RulesCompositionUsesSameDryRunApplyUndoRedoAndTypedScriptHandle()
    {
        var itemId = BlueprintId.From(Guid.Parse("a1782000-0000-4000-8000-000000000001"));
        var history = new ProjectCommandHistory(CreateRulesState(itemId));
        var source = Complete();
        var rules = CompleteRules(itemId);
        var profile = new CreatureFoundationProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RequiredAtRuntime, source.Identity, source.DefaultTransform, source.Resources, source.Movement,
            source.References, source.Spawned, source.Overrides,
            CreatureAuthoringCompositionProfile.CreateDefault(Guid.NewGuid(), "author:r078"), true, rules, true);
        var command = new SetCreatureFoundationCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            CreatureId, profile, true);

        var preview = _handler.Handle(history.State, command);
        var applied = _handler.Handle(history.State, command with { DryRun = false });
        history.Record(applied);
        var view = CreatureRulesCompositionScriptService.Read(history.State.Project,
            CreatureRulesCompositionScriptService.CreateHandle(CreatureId));

        Assert.True(preview.Succeeded);
        Assert.True(applied.Succeeded);
        Assert.Equal(preview.Patch, applied.Patch);
        Assert.Equal("Seated", view!.LifeState);
        Assert.Equal(16, view.Attributes["evasion"]);
        Assert.Equal("ability.ash-nova", Assert.Single(view.AbilityIds));
        Assert.Equal(1, view.InventoryItemCount);
        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single(item => item.Id == CreatureId).CreatureFoundation);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(rules.Id, history.State.Project.Blueprints.Single(item => item.Id == CreatureId)
            .CreatureFoundation!.RulesComposition!.Id);
    }

    [Fact]
    public void RulesCompositionRejectsMissingInventoryBlueprintBeforeMutation()
    {
        var state = CreateRulesState(BlueprintId.From(Guid.NewGuid()));
        var source = Complete();
        var rules = CompleteRules(BlueprintId.From(Guid.NewGuid()));
        var profile = new CreatureFoundationProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RequiredAtRuntime, source.Identity, source.DefaultTransform, source.Resources, source.Movement,
            source.References, source.Spawned, source.Overrides,
            CreatureAuthoringCompositionProfile.CreateDefault(Guid.NewGuid(), "author:r078"), true, rules, true);

        var result = _handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision,
            CreatureId, profile, false));

        Assert.False(result.Succeeded);
        Assert.Equal("creature.rules.inventory_item_missing", Assert.Single(result.Diagnostics).Code);
        Assert.Equal(state.Revision, result.State.Revision);
    }

    [Fact]
    public void InteractionCompositionUsesSameDryRunApplyUndoRedoAndTypedScriptHandle()
    {
        var itemId = BlueprintId.From(Guid.Parse("a1793000-0000-4000-8000-000000000001"));
        var factionId = Guid.Parse("a1793000-0000-4000-8000-000000000002");
        var dialogueId = DialogueId.Parse("a1793000-0000-4000-8000-000000000003");
        var history = new ProjectCommandHistory(CreateInteractionState(itemId, factionId, dialogueId));
        var source = Complete();
        var interaction = CompleteInteraction(factionId, dialogueId);
        var profile = new CreatureFoundationProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RequiredAtRuntime, source.Identity, source.DefaultTransform, source.Resources, source.Movement,
            source.References, source.Spawned, source.Overrides,
            CreatureAuthoringCompositionProfile.CreateDefault(Guid.NewGuid(), "author:r079"), true,
            CompleteRules(itemId), true, interaction, true);
        var command = new SetCreatureFoundationCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            CreatureId, profile, true);

        var preview = _handler.Handle(history.State, command);
        var applied = _handler.Handle(history.State, command with { DryRun = false });
        history.Record(applied);
        var view = CreatureInteractionCompositionScriptService.Read(history.State.Project,
            CreatureInteractionCompositionScriptService.CreateHandle(CreatureId));

        Assert.True(preview.Succeeded);
        Assert.True(applied.Succeeded);
        Assert.Equal(preview.Patch, applied.Patch);
        Assert.Equal(factionId.ToString("D"), view!.PrimaryFactionId);
        Assert.Equal(4, view.Senses.Count);
        Assert.Equal(dialogueId.ToString(), Assert.Single(view.DialogueIds));
        Assert.True(view.Invulnerable);
        Assert.Equal("OnConversation", view.OnConversationHandler);
        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single(item => item.Id == CreatureId).CreatureFoundation);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(interaction.Id, history.State.Project.Blueprints.Single(item => item.Id == CreatureId)
            .CreatureFoundation!.InteractionComposition!.Id);
    }

    [Fact]
    public void InteractionCompositionRejectsMissingFactionAndDialogueBeforeMutation()
    {
        var itemId = BlueprintId.From(Guid.NewGuid());
        var factionId = Guid.NewGuid();
        var dialogueId = DialogueId.From(Guid.NewGuid());
        var state = CreateInteractionState(itemId, factionId, dialogueId);
        var source = Complete();
        CreatureFoundationProfile Foundation(CreatureInteractionCompositionProfile interaction) => new(
            source.Id, source.SchemaVersion, source.Provenance, source.RequiredAtRuntime, source.Identity,
            source.DefaultTransform, source.Resources, source.Movement, source.References, source.Spawned,
            source.Overrides, CreatureAuthoringCompositionProfile.CreateDefault(Guid.NewGuid(), "author:r079"),
            true, CompleteRules(itemId), true, interaction, true);

        var missingFaction = _handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision, CreatureId,
            Foundation(CompleteInteraction(Guid.NewGuid(), dialogueId)), false));
        var missingDialogue = _handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision, CreatureId,
            Foundation(CompleteInteraction(factionId, DialogueId.From(Guid.NewGuid()))), false));

        Assert.Equal("creature.interaction.faction_missing", Assert.Single(missingFaction.Diagnostics).Code);
        Assert.Equal("creature.interaction.dialogue_missing", Assert.Single(missingDialogue.Diagnostics).Code);
        Assert.Equal(state.Revision, missingFaction.State.Revision);
        Assert.Equal(state.Revision, missingDialogue.State.Revision);
    }

    [Fact]
    public void EventAiCompositionUsesSameDryRunApplyUndoRedoAndTypedScriptHandle()
    {
        var history = new ProjectCommandHistory(CreateState());
        var source = Complete();
        var eventAi = CreatureEventAiCompositionProfile.CreateDefault(
            Guid.Parse("a1803000-0000-4000-8000-000000000001"), "author:r080");
        var profile = new CreatureFoundationProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RequiredAtRuntime, source.Identity, source.DefaultTransform, source.Resources, source.Movement,
            source.References, source.Spawned, source.Overrides, eventAiComposition: eventAi,
            eventAiCompositionRequiredAtRuntime: true);
        var command = new SetCreatureFoundationCommand(CommandId.From(Guid.NewGuid()), history.State.Revision,
            CreatureId, profile, true);

        var preview = _handler.Handle(history.State, command);
        var applied = _handler.Handle(history.State, command with { DryRun = false });
        history.Record(applied);
        var view = CreatureEventAiCompositionScriptService.Read(history.State.Project,
            CreatureEventAiCompositionScriptService.CreateHandle(CreatureId));

        Assert.True(preview.Succeeded);
        Assert.True(applied.Succeeded);
        Assert.Equal(preview.Patch, applied.Patch);
        Assert.Equal("StateMachine", view!.ControllerKind);
        Assert.Equal(2, view.Blackboard.Count);
        Assert.Equal("OnPhysicalAttacked", view.OnPhysicalAttackedHandler);
        Assert.Equal("OnSpellCastAt", view.OnSpellCastAtHandler);
        Assert.True(history.Undo().Succeeded);
        Assert.Null(history.State.Project.Blueprints.Single().CreatureFoundation);
        Assert.True(history.Redo().Succeeded);
        Assert.Equal(eventAi.Id, history.State.Project.Blueprints.Single().CreatureFoundation!
            .EventAiComposition!.Id);
    }

    [Fact]
    public void EventAiCompositionRejectsInvalidBindingAndDuplicateUuidBeforeMutation()
    {
        var state = CreateState();
        var source = Complete();
        var eventAi = CreatureEventAiCompositionProfile.CreateDefault(Guid.NewGuid(), "author:r080");
        var invalidEventAi = new CreatureEventAiCompositionProfile(eventAi.Id, eventAi.SchemaVersion,
            eventAi.Provenance, eventAi.AssetId, eventAi.PreviewTransform,
            eventAi.Perception with { Lost = eventAi.Perception.Detected }, eventAi.PhysicalAttack,
            eventAi.Ability, eventAi.Blocked, eventAi.Controller, eventAi.Overrides);
        var profile = new CreatureFoundationProfile(source.Id, source.SchemaVersion, source.Provenance,
            source.RequiredAtRuntime, source.Identity, source.DefaultTransform, source.Resources, source.Movement,
            source.References, source.Spawned, source.Overrides, eventAiComposition: invalidEventAi,
            eventAiCompositionRequiredAtRuntime: true);

        var result = _handler.Handle(state, new(CommandId.From(Guid.NewGuid()), state.Revision,
            CreatureId, profile, false));

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, item => item.Code == "creature.event_ai.component_uuid_invalid");
        Assert.Contains(result.Diagnostics, item => item.Code == "creature.event_ai.binding_invalid");
        Assert.Equal(state.Revision, result.State.Revision);
    }

    private static ProjectState CreateState()
    {
        var area = new AreaDefinition(AreaId.From(Guid.Parse("a1610000-0000-4000-8000-000000000002")),
            "crypt", "Crypte", []);
        var blueprint = new BlueprintDefinition(CreatureId, "ash-warden", "Gardien des cendres", BlueprintKind.Creature);
        return new(new ProjectDefinition(ProjectId.From(Guid.Parse("a1610000-0000-4000-8000-000000000003")),
            "R038", [blueprint], [area]), new ProjectRevision(0));
    }

    private static ProjectState CreateRulesState(BlueprintId itemId)
    {
        var state = CreateState();
        var creature = state.Project.Blueprints.Single();
        var item = new BlueprintDefinition(itemId, "ember-blade", "Lame de braise", BlueprintKind.Item);
        return new(new ProjectDefinition(state.Project.Id, state.Project.Title, [creature, item], state.Project.Areas),
            state.Revision);
    }

    private static ProjectState CreateInteractionState(BlueprintId itemId, Guid factionId, DialogueId dialogueId)
    {
        var state = CreateRulesState(itemId);
        var faction = new FactionDefinition(factionId, 1, "test:r079", "ash-watch", "Veilleurs", false, [],
            ["lawful"]);
        var module = state.Project.Module.WithFactions(new FactionCatalog([faction], [], [], []));
        var nodeId = DialogueNodeId.From(Guid.Parse("a1793000-0000-4000-8000-000000000004"));
        var dialogue = new DialogueDefinition(dialogueId, "ash-greeting", "Accueil", nodeId,
            [new DialogueNode(nodeId, CreatureId, new LocalizedText("dialogue.ash-greeting", "Halte."))]);
        return new(new ProjectDefinition(state.Project.Id, state.Project.Title, module, state.Project.Blueprints,
            state.Project.Areas, [dialogue]), state.Revision);
    }

    private static CreatureRulesCompositionProfile CompleteRules(BlueprintId itemId) => new(
        Guid.Parse("a1782000-0000-4000-8000-000000000010"), 1, "author:r078", "asset.rules.warden",
        Forge.Domain.Spatial.TransformValue.Identity,
        new(Guid.Parse("a1782000-0000-4000-8000-000000000011"), CreatureRulesInitialLifeState.Seated,
            "clip.warden.seated", "pose.warden-throne", false),
        new(Guid.Parse("a1782000-0000-4000-8000-000000000012"), "ruleset.crypt",
            new Dictionary<string, double> { ["strength"] = 8, ["agility"] = 6 },
            [new(Guid.Parse("a1782000-0000-4000-8000-000000000013"), "agility", "species.emberborn", 1)],
            [new(Guid.Parse("a1782000-0000-4000-8000-000000000014"), "evasion",
                CreatureRulesAttributeFormulaOperation.Sum, ["agility", "strength"], 1)]),
        new(Guid.Parse("a1782000-0000-4000-8000-000000000015"), 4, 2,
            new Dictionary<string, double> { ["glass"] = .2 },
            new Dictionary<string, double> { ["fortitude"] = 6 }, ["radiant"], 1),
        [new(Guid.Parse("a1782000-0000-4000-8000-000000000016"), "ability.ash-nova", 3, 2, 12,
            ["state.alive"], ["boss"], "script.ash-nova")],
        new(Guid.Parse("a1782000-0000-4000-8000-000000000017"),
            [new(Guid.Parse("a1782000-0000-4000-8000-000000000018"), "container.backpack", 8, 100, 100,
                [new(Guid.Parse("a1782000-0000-4000-8000-000000000019"), itemId, 1, 3, 2)])],
            new Dictionary<string, Guid>
            {
                ["main-hand"] = Guid.Parse("a1782000-0000-4000-8000-000000000019"),
            }, "creature.warden", true), new Dictionary<string, string>());

    private static CreatureInteractionCompositionProfile CompleteInteraction(Guid factionId, DialogueId dialogueId) =>
        new(Guid.Parse("a1793000-0000-4000-8000-000000000010"), 1, "author:r079",
            "asset.interaction.warden", TransformValue.Identity,
            new(Guid.Parse("a1793000-0000-4000-8000-000000000011"), factionId,
                [new(Guid.Parse("a1793000-0000-4000-8000-000000000012"), factionId, 1, "rank.captain", 25,
                    true)]),
            new(Guid.Parse("a1793000-0000-4000-8000-000000000013"),
                [new(Guid.Parse("a1793000-0000-4000-8000-000000000014"),
                    CreatureInteractionSenseKind.Vision, 30, 120, true, ["stimulus.visual"]),
                 new(Guid.Parse("a1793000-0000-4000-8000-000000000015"),
                    CreatureInteractionSenseKind.Hearing, 20, 360, true, ["stimulus.sound"]),
                 new(Guid.Parse("a1793000-0000-4000-8000-000000000016"),
                    CreatureInteractionSenseKind.Smell, 8, 360, false, ["stimulus.scent"]),
                 new(Guid.Parse("a1793000-0000-4000-8000-000000000017"),
                    CreatureInteractionSenseKind.Magic, 15, 360, false, ["stimulus.magic"])], 12),
            [new(Guid.Parse("a1793000-0000-4000-8000-000000000018"), "greeting", 10, dialogueId,
                null, null, true)],
            new(Guid.Parse("a1793000-0000-4000-8000-000000000019"), true, true, false,
                ["creature.plot.override"]),
            new(Guid.Parse("a1793000-0000-4000-8000-000000000020"), 1, "author:r079", "1.0",
                "creature.interaction_requested", "OnConversation", EventAuthorityScope.Server),
            new Dictionary<string, string>());

    internal static CreatureFoundationProfile Complete()
    {
        var profile = CreatureFoundationProfile.CreateDefault(
            Guid.Parse("a1600000-0000-4000-8000-000000000001"), "ash-warden", "Gardien des cendres", "author:r038");
        return new(profile.Id, profile.SchemaVersion, profile.Provenance, true,
            profile.Identity, profile.DefaultTransform,
            [new(Guid.Parse("a1600000-0000-4000-8000-000000000002"), "health", 100, 84, .5, true),
             new(Guid.Parse("a1600000-0000-4000-8000-000000000003"), "stamina", 40, 40, 2, false)],
            new(CreatureFoundationMovementMode.Flying, 6, 12, 8, 0, 30, 50), profile.References,
            new(Guid.Parse("a1600000-0000-4000-8000-000000000004"), 1, "author:r038", "1.0",
                "creature.spawned", "OnSpawned", EventAuthorityScope.Server), profile.Overrides);
    }
}
