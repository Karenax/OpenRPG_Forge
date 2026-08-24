using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Creatures;
using Forge.Domain.Navigation;
using Forge.Domain.Quests;
using Forge.Application.Projects;
using Forge.Domain.Projects;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
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
        Assert.Equal(38, area.Entities.Count);
        Assert.Contains(project.Blueprints, blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn);
        Assert.Contains(project.Blueprints, blueprint => blueprint.Slug == "ash-warden-boss");
        Assert.Contains(project.Blueprints, blueprint => blueprint.Slug == "ash-key");
        var pillar = Assert.Single(project.Blueprints, blueprint => blueprint.Slug == "crypt-pillar");
        Assert.Equal(4, area.Entities.Count(entity =>
            entity.BlueprintId == pillar.Id && entity.Tags.Contains("pillar", StringComparer.Ordinal)));
        Assert.Equal(2, area.Entities.Count(entity =>
            entity.Tags.Contains("wall", StringComparer.Ordinal) &&
            entity.Tags.Contains("cover", StringComparer.Ordinal) &&
            Math.Abs(Math.Abs(entity.Transform.Position.X) - 8) < .01 &&
            Math.Abs(entity.Transform.Position.Z - 15) < .01));
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
        var eventAi = Assert.IsType<CreatureEventAiCompositionProfile>(
            bossBlueprint.CreatureFoundation!.EventAiComposition);
        Assert.True(bossBlueprint.CreatureFoundation.EventAiCompositionRequiredAtRuntime);
        Assert.Equal(CreatureEventAiControllerKind.Utility, eventAi.Controller.Kind);
        Assert.Equal(3, eventAi.Controller.Blackboard.Count);
        Assert.Equal("OnPerceptionDetected", eventAi.Perception.Detected.Handler);
        Assert.Equal("OnPhysicalAttacked", eventAi.PhysicalAttack.Resolved.Handler);
        Assert.Equal("OnSpellCastAt", eventAi.Ability.Impacted.Handler);
        Assert.Equal("OnBlocked", eventAi.Blocked.Blocked.Handler);
    }

    [Fact]
    public void SampleProject_WorldFeatureFixtureIsCanonicalAndRuntimeResolvable()
    {
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "crypt-of-ashes");
        var project = ProjectSession.Open(fixtureDirectory).Session!.Project;
        var catalog = CryptWorldFeatureAuthoringService.Create(project);
        var configured = new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
            project.Blueprints, project.Areas, project.Dialogues, project.Quests);

        Assert.Empty(ProjectValidator.Validate(configured));
        Assert.Single(catalog.Placeables);
        Assert.Single(catalog.Doors);
        Assert.Single(catalog.Triggers);
        Assert.Single(catalog.Encounters);
        Assert.Single(catalog.VehicleRoutes);
        Assert.Single(catalog.Merchants);
        Assert.Single(catalog.Companions);
        var encounter = catalog.Encounters[0];
        Assert.True(encounter.CompositionRequiredAtRuntime);
        Assert.Equal(EncounterDifficultyBand.Hard, encounter.Composition!.Difficulty.Band);
        Assert.Equal(36, encounter.Composition.Difficulty.EffectiveBudget);
        Assert.Equal((2, 4), (encounter.Composition.Groups[0].Minimum,
            encounter.Composition.Groups[0].Maximum));
        Assert.Equal("party:forge-generic", encounter.Composition.Selection.PartyAssessmentProviderId);
        Assert.Equal(EncounterActivationMode.PopulationMaintained, encounter.Composition.Activation.Mode);
        Assert.True(encounter.ScenarioRequiredAtRuntime);
        Assert.Equal("anchor:crypt-north", Assert.Single(encounter.Scenario!.SpawnAnchors).AnchorId);
        Assert.Equal(EncounterNetworkAuthority.Server, encounter.Scenario.Activator.RequiredAuthority);
        Assert.Equal("EncounterEntered", encounter.Scenario.Lifecycle.Entered.Binding);
        Assert.Equal("EncounterExited", encounter.Scenario.Lifecycle.Exited.Binding);
        Assert.Equal("EncounterExhausted", encounter.Scenario.Lifecycle.Exhausted.Binding);
        Assert.Equal(2, Assert.Single(encounter.Scenario.Phases).Waves.Count);
        var roundTrip = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(configured));
        Assert.Equal("challenge-rating",
            roundTrip.Module.WorldFeatures.Encounters[0].Composition!.Selection.Strategy);
        Assert.Equal("audio:encounter-warden-fallen",
            roundTrip.Module.WorldFeatures.Encounters[0].Scenario!.Phases[0].Waves[1].VoiceCueId);
        Assert.Equal(project.Quests.Single().Id.Value,
            roundTrip.Module.WorldFeatures.Encounters[0].Scenario!.Phases[0].Waves[1].QuestId);
        var compositionEvents = Assert.IsType<PlaceableCompositionEventsProfile>(
            catalog.Placeables[0].CompositionEvents);
        Assert.True(catalog.Placeables[0].CompositionEventsRequiredAtRuntime);
        Assert.Equal("DamageApplied", compositionEvents.DamageEvents.DamagedBinding);
        Assert.Equal("EntityDestroyed", compositionEvents.DamageEvents.DestroyedBinding);
        Assert.Equal("lever", Assert.Single(compositionEvents.Sockets).SocketId);
        Assert.Equal("prefab:crypt-pillar-lever", Assert.Single(compositionEvents.Attachments).PrefabId);
        Assert.Equal(8, compositionEvents.MultiVerb.Verbs.Count);
        Assert.Equal("custom-ring", compositionEvents.MultiVerb.Verbs[^1].VerbId);
        Assert.Equal("prefab:crypt-pillar-lever", roundTrip.Module.WorldFeatures.Placeables[0]
            .CompositionEvents!.Attachments.Single().PrefabId);
        Assert.Equal("RingRequested", roundTrip.Module.WorldFeatures.Placeables[0]
            .CompositionEvents!.MultiVerb.Verbs.Single(item => item.VerbId == "custom-ring").EventBinding);
        var advancedTrigger = Assert.IsType<TriggerAdvancedProfile>(catalog.Triggers[0].Advanced);
        var ashWatchFaction = project.Module.Factions.Factions.Single(item => item.Slug == "ash-watch");
        Assert.True(catalog.Triggers[0].AdvancedRequiredAtRuntime);
        Assert.Equal("damage:ember-burst", advancedTrigger.Trap.PayloadId);
        Assert.True(advancedTrigger.TargetFilter.PlayersOnly);
        Assert.Equal(ashWatchFaction.Id, Assert.Single(advancedTrigger.TargetFilter.FactionIds));
        Assert.Equal(10, advancedTrigger.GameplayVolumes.Count);
        Assert.Equal(10, advancedTrigger.GameplayVolumes.Select(item => item.Kind).Distinct().Count());
        Assert.Equal("TrapTriggered", advancedTrigger.Trap.TriggeredBinding);
        Assert.Equal(100, advancedTrigger.OccupancyDebug.MaximumTraceEntries);
        Assert.Equal("DamageVolumeEntered", roundTrip.Module.WorldFeatures.Triggers[0].Advanced!
            .GameplayVolumes.Single(item => item.Kind == TriggerGameplayVolumeKind.Damage).EnterBinding);
        Assert.Equal(ashWatchFaction.Id, Assert.Single(roundTrip.Module.WorldFeatures.Triggers[0].Advanced!
            .TargetFilter.FactionIds));
        Assert.True(encounter.StatisticsRequiredAtRuntime);
        Assert.Equal(64, encounter.Statistics!.Runs);
        Assert.Equal([.5, .9, .99], encounter.Statistics.Percentiles);
        Assert.Equal("duration:actors-linear",
            roundTrip.Module.WorldFeatures.Encounters[0].Statistics!.DurationEstimatorId);
        Assert.Equal(840, roundTrip.Module.WorldFeatures.Encounters[0].Statistics!.BaseSeed);
        var companion = catalog.Companions[0];
        Assert.NotNull(companion.Validation);
        Assert.True(companion.ValidationRequiredAtRuntime);
        Assert.Equal(
            ["move", "follow", "hold", "rally", "guard", "interact", "attack", "defend", "focus-fire", "protect", "disengage", "use-ability"],
            companion.Validation!.CommandIds);
        Assert.Equal(2, companion.Validation.DialogueContexts.Count);
        Assert.Equal("recoverable-corpse", companion.Validation.Death.Mode);
        var resolved = CompanionValidationRuntimeResolver.Resolve(companion, destinationAreaId: null);
        Assert.True(resolved.Available);
        Assert.True(resolved.DestinationAllowed);
        Assert.Equal("party", resolved.OwnershipKind);
        var optional = CompanionValidationRuntimeResolver.Resolve(
            companion with { Validation = null, ValidationRequiredAtRuntime = false });
        var required = CompanionValidationRuntimeResolver.Resolve(
            companion with { Validation = null, ValidationRequiredAtRuntime = true });
        Assert.True(optional.OptionalFallback);
        Assert.Contains(required.Diagnostics, item => item.Code == "world.companion.validation_required");
        Assert.True(WorldFeatureRuntimeResolver.ResolvePlaceableAccess(catalog.Placeables[0],
            new HashSet<string> { "key:ember" }, new HashSet<string>(), 3).Allowed);
        Assert.True(WorldFeatureRuntimeResolver.ApplyDoorDamage(catalog.Doors[0], 40, 100).Breached);
        var doorAccess = DoorAccessEventProfileValidator.Resolve(catalog.Doors[0]);
        Assert.True(doorAccess.Available);
        Assert.Equal("key:ember", doorAccess.Profile!.Lock.CredentialIds.Single());
        Assert.Equal("DoorOpenRejected", doorAccess.Profile.Rejection.EventBinding);
        Assert.Equal("damage:ember-burst", doorAccess.Profile.Trap.PayloadId);
        Assert.Equal("PortalUseRequested", doorAccess.Profile.PortalUse.RequestedBinding);
        Assert.Equal("DoorClosed", doorAccess.Profile.StateEvents.ClosedBinding);
        Assert.True(doorAccess.Profile.LockStateEventsRequiredAtRuntime);
        Assert.Equal("DoorLocked", doorAccess.Profile.LockStateEvents!.LockedBinding);
        Assert.Equal("DoorUnlocked", doorAccess.Profile.LockStateEvents.UnlockedBinding);
        Assert.NotNull(WorldFeatureRuntimeResolver.ResolveScript(catalog.Scripts, "door.ash", "opened").ScriptId);
    }

    [Fact]
    public void Companion_validation_rejects_unsafe_commands_and_unknown_cross_zone_references_before_build()
    {
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "crypt-of-ashes");
        var project = ProjectSession.Open(fixtureDirectory).Session!.Project;
        var catalog = CryptWorldFeatureAuthoringService.Create(project);
        var companion = catalog.Companions[0];
        var invalid = companion with
        {
            Validation = companion.Validation! with
            {
                CommandIds = ["follow", "follow"],
                CrossZoneNavigation = companion.Validation.CrossZoneNavigation with
                {
                    Enabled = true,
                    AllowedAreaIds = [project.Areas[0].Id, Forge.Domain.Areas.AreaId.From(Guid.NewGuid())],
                },
            },
        };
        var invalidCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
            catalog.Placeables, catalog.Doors, catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes,
            catalog.Merchants, [invalid], catalog.Scripts);
        var configured = new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(invalidCatalog),
            project.Blueprints, project.Areas, project.Dialogues, project.Quests);

        var diagnostics = WorldFeatureCatalogValidator.Validate(configured);

        Assert.Contains(diagnostics, item => item.Code == "world.companion.commands_invalid" &&
                                             item.Path.EndsWith("/validation/commandIds", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == "world.companion.cross_zone_navigation_invalid" &&
                                             item.Path.EndsWith("/validation/crossZoneNavigation", StringComparison.Ordinal));
    }

    [Fact]
    public void Companion_cross_zone_policy_accepts_an_authored_connected_portal_graph()
    {
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "crypt-of-ashes");
        var project = ProjectSession.Open(fixtureDirectory).Session!.Project;
        var destination = new AreaDefinition(
            AreaId.From(Guid.Parse("15400000-0000-4000-8000-000000000001")), "camp", "Camp", []);
        var source = project.Areas[0].WithPortals([
            new PortalLink(PortalId.From(Guid.Parse("15400000-0000-4000-8000-000000000002")), "Camp gate",
                project.Areas[0].Entities[0].Id, destination.Id,
                EntityId.From(Guid.Parse("15400000-0000-4000-8000-000000000003")), true, true,
                provenance: "test:r037"),
        ]);
        var projectWithPortal = new ProjectDefinition(project.Id, project.Title, project.Module,
            project.Blueprints, [source, destination], project.Dialogues, project.Quests);
        var catalog = CryptWorldFeatureAuthoringService.Create(projectWithPortal);
        var companion = catalog.Companions[0];
        companion = companion with
        {
            Validation = companion.Validation! with
            {
                CrossZoneNavigation = companion.Validation.CrossZoneNavigation with
                {
                    Enabled = true,
                    AllowedAreaIds = [source.Id, destination.Id],
                },
            },
        };
        var connectedCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
            catalog.Placeables, catalog.Doors, catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes,
            catalog.Merchants, [companion], catalog.Scripts);
        var configured = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(connectedCatalog), project.Blueprints, [source, destination],
            project.Dialogues, project.Quests);

        var diagnostics = WorldFeatureCatalogValidator.Validate(configured);

        Assert.DoesNotContain(diagnostics, item => item.Code.StartsWith("world.companion.cross_zone", StringComparison.Ordinal));
        Assert.True(CompanionValidationRuntimeResolver.Resolve(companion, destination.Id).DestinationAllowed);
    }
}
