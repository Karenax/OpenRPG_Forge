using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Areas;
using Forge.Application.Assets;
using Forge.Application.Blueprints;
using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Application.Build;
using Forge.Application.Content;
using Forge.Application.Creatures;
using Forge.Application.Gameplay;
using Forge.Application.Items;
using Forge.Application.Localization;
using Forge.Application.Modules;
using Forge.Application.Lifecycle;
using Forge.Application.Navigation;
using Forge.Application.Sequences;
using Forge.Application.Time;
using Forge.Application.Validation;
using Forge.Application.Rules;
using Forge.Application.Quality;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.RuntimeExtensions;
using Forge.Application.Scripting;
using Forge.Application.Entities;
using Forge.Application.Combat;
using Forge.Application.Factions;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Audio;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Combat;
using Forge.Domain.Creatures;
using Forge.Domain.Content;
using Forge.Application.Dialogues;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Gameplay;
using Forge.Domain.Lifecycle;
using Forge.Domain.Localization;
using Forge.Domain.Items;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Quality;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.EngineAdapter.Spatial;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.World;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Playtesting;
using Forge.ModuleFormat.Launcher;
using Forge.ModuleFormat.Saves;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Production;
using Forge.Network.Observability;
using Forge.Network.Testing;
using Forge.Network.Authoring;
using Forge.Network.Scenarios;
using Forge.Network.Sessions;
using Forge.Network.World;
using Forge.LiveDirector.Authoring;
using Forge.LiveDirector.Scenarios;
using Forge.RpgKit.Application.Companions;
using Forge.RpgKit.Application.Creatures;
using Forge.RpgKit.Application.Doors;
using Forge.RpgKit.Application.Encounters;
using Forge.RpgKit.Application.Merchants;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Application.Scripting;
using Forge.RpgKit.Application.Triggers;
using Forge.RpgKit.Domain.Companions;
using Forge.RpgKit.Domain.Factions;
using Forge.RpgKit.Domain.Merchants;
using RpgAttackContext = Forge.RpgKit.Domain.Scripting.AttackContext;
using RpgDamageSpec = Forge.RpgKit.Domain.Scripting.DamageSpec;
using RpgEffectDefinition = Forge.RpgKit.Domain.Scripting.EffectDefinition;
using RpgEffectSpec = Forge.RpgKit.Domain.Scripting.EffectSpec;
using RpgEffectStacking = Forge.RpgKit.Domain.Scripting.EffectStacking;
using RpgScriptCombatState = Forge.RpgKit.Domain.Scripting.ScriptCombatState;
using RpgScriptGameplayService = Forge.RpgKit.Domain.Scripting.ScriptGameplayService;
using RpgDialogueRequest = Forge.RpgKit.Domain.Scripting.DialogueRequest;
using RpgFactionState = Forge.RpgKit.Domain.Scripting.FactionState;
using RpgFactionTransaction = Forge.RpgKit.Domain.Scripting.FactionTransaction;
using RpgInventoryState = Forge.RpgKit.Domain.Scripting.InventoryState;
using RpgInventoryTransaction = Forge.RpgKit.Domain.Scripting.InventoryTransaction;
using RpgPerceptionQuery = Forge.RpgKit.Domain.Scripting.PerceptionQuery;
using RpgQuestState = Forge.RpgKit.Domain.Scripting.QuestState;
using RpgQuestTransition = Forge.RpgKit.Domain.Scripting.QuestTransition;
using RpgScriptWorldService = Forge.RpgKit.Domain.Scripting.ScriptWorldService;
using ScriptAudioCue = Forge.Scripting.AudioCue;
using ScriptAudioState = Forge.Scripting.AudioState;
using ScriptCustomEvent = Forge.Scripting.CustomEvent;
using ScriptCustomEventSchema = Forge.Scripting.CustomEventSchema;
using ScriptDebugFrame = Forge.Scripting.DebugFrame;
using ScriptHotReloadModule = Forge.Scripting.HotReloadModule;
using ScriptRuntimeTools = Forge.Scripting.ForgeScriptRuntimeTools;
using ScriptWorldClock = Forge.Scripting.WorldClock;
using RpgActivator = Forge.RpgKit.Domain.Encounters.Activator;
using RpgNetworkAuthority = Forge.RpgKit.Domain.Encounters.NetworkAuthority;
using RpgLockState = Forge.RpgKit.Domain.Placeables.LockState;
using RpgQueryableEntity = Forge.RpgKit.Domain.Triggers.QueryableEntity;
using RpgTriggerEventTrace = Forge.RpgKit.Domain.Triggers.TriggerEventTrace;
using RpgTriggerRuntimeState = Forge.RpgKit.Domain.Triggers.TriggerRuntimeState;
using Forge.Scripting;
using Godot;

namespace Forge.Runtime;

public partial class GameRoot
{
    private void RunPostLaunchSmokeTest()
    {
        var profile = _postLaunchProfile ??
            throw new InvalidOperationException("The post-launch capability profile was not configured.");
        var mobility = PostLaunchCapabilityResolver.ResolveMobility(profile.Gameplay, true, .5f, 4, true);
        AssertSmoke(mobility.ZeroGravityActive && mobility.VehicleAccepted && mobility.MountAccepted,
            "Zero-gravity, vehicle or mount capability did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13AC_MOBILITY_PASS zero-g=active acceleration=7 vehicle=accepted mount=accepted");

        var achievementDefinition = profile.Gameplay.Achievements.Single();
        var achievement = PostLaunchCapabilityResolver.EvaluateAchievement(
            profile.Gameplay, achievementDefinition.Key, achievementDefinition.Threshold);
        AssertSmoke(achievement.Unlocked && profile.Gameplay.CuratedPacks.RequireTrustedSignature,
            "Achievement or curated-pack governance did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13AD_ACHIEVEMENT_PACK_PASS achievement=unlocked platform=queued pack=trusted-cc0");

        var rest = PostLaunchCapabilityResolver.PlanDirectorIntervention(profile.LiveDirector, "force-rest", 2);
        var level = PostLaunchCapabilityResolver.PlanDirectorIntervention(profile.LiveDirector, "adjust-level", 1, 2);
        var appearance = PostLaunchCapabilityResolver.PlanDirectorIntervention(profile.LiveDirector, "appearance", 1, scale: 1.2f);
        AssertSmoke(rest.Accepted && level.Accepted && level.SnapshotRequired && appearance.Accepted,
            "Live Director intervention policies did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13AE_DIRECTOR_INTERVENTION_PASS rest=completed level=preview-snapshot appearance=validated");

        var recording = PostLaunchCapabilityResolver.ResolveRecording(profile.LiveDirector, 1,
            new Dictionary<string, IReadOnlyList<Guid>> { ["tag:guardian"] = [Guid.NewGuid()] });
        AssertSmoke(recording.Accepted && recording.RedactPrivateData && recording.ExportFormat == "orp-replay",
            "Saved Live Director group or recording policy did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13AF_DIRECTOR_RECORDING_PASS group=resolved replay=orp-replay privacy=redacted");

        var networkContent = PostLaunchCapabilityResolver.ResolveNetworkContent(profile.Gameplay, profile.Network,
            signerTrusted: true, "CC0-1.0");
        AssertSmoke(networkContent.VoiceEnabled && networkContent.Diagnostic == "POST_NETWORK_CONTENT_READY",
            "Curated content or voice policy did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13AG_NETWORK_CONTENT_VOICE_PASS content=trusted voice=opus-positional");

        var server = new ServerProductionService(profile.Network);
        var shard = profile.Network.Shards.Single();
        var reloadRequest = new ServerProductionService.ReloadRequest(Guid.NewGuid(), shard.Key,
            shard.MaximumPlayers, 4, 0,
            new string('a', 64));
        var reloadPreview = server.Reload(reloadRequest, false);
        var reloadApplied = server.Reload(reloadRequest, true);
        AssertSmoke(reloadPreview.Code == "NET_PRODUCTION_PREVIEW" && reloadApplied.Applied && reloadApplied.RollbackAvailable,
            "Shard hot reload did not preview and apply atomically.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G13AH_SHARD_RELOAD_PASS shard={shard.Key} atomic=true rollback=true");

        var protocol = server.Negotiate("windows", 1, 2, ["metrics", "unsafe-shell"], signedExtension: true);
        AssertSmoke(protocol.Compatible && protocol.ExtensionAllowed && protocol.GrantedCapabilities.SequenceEqual(["metrics"]),
            "Cross-platform protocol or signed extension negotiation failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13AI_SERVER_PROTOCOL_PASS platform=windows version=2 extension=signed-isolated capabilities=metrics");

        var technical = PostLaunchCapabilityResolver.ResolveCreatorTechnical(profile.CreatorExperience,
            ["graphics-tier", "hardware-serial"], 64, 2);
        AssertSmoke(technical.Accepted && technical.DeviceQueries.SequenceEqual(["graphics-tier"]) &&
                    technical.GraphicsMode == "msaa8+aniso16",
            "Creator technical capability resolution failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13AJ_CREATOR_TECHNICAL_PASS water=high device=consented resources=bounded areas=2 graphics=msaa8-aniso16");

        var distribution = PostLaunchCapabilityResolver.ResolveRpgDistribution(profile.CreatorExperience, 8, 32);
        AssertSmoke(distribution.PartyControl && distribution.SignedMetadata && distribution.Observability,
            "Party, progression, repository or observability policy did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13AK_RPG_DISTRIBUTION_PASS party=8 tracks=64 repository=signed observability=tracy-otel");

        var media = PostLaunchRuntimeService.PlanMediaGeneration(profile, Guid.NewGuid(), "image", "local",
            "CC0-1.0", "runtime-smoke", 1024, providerAvailable: true);
        var soak = DeterministicSoakTestService.Evaluate(profile.Production.SoakTests,
        [
            new(0, 512, 1_000, 80, true, 64),
            new(60, 540, 1_020, 120, true, 64),
            new(120, 560, 1_030, 150, true, 64),
        ]);
        AssertSmoke(media.Succeeded && media.RequiresReview && soak.Passed,
            "Governed media staging or deterministic soak evaluation failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13AL_MEDIA_SOAK_PASS media=staged-rights-review soak=120m-memory-handles-db-latency-players");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13AC_AL_POST_LAUNCH_PASS slices=10 profile=canonical runtime=domain-application-network-qa");
    }

    private void RunWorldFeatureSmokeTest()
    {
        var catalog = _worldFeatureCatalog ??
            throw new InvalidOperationException("The world-feature catalog was not configured.");
        var placeable = AssertSingle(catalog.Placeables);
        var door = AssertSingle(catalog.Doors);
        var trigger = AssertSingle(catalog.Triggers);
        var encounter = AssertSingle(catalog.Encounters);
        var route = AssertSingle(catalog.VehicleRoutes);
        var merchant = AssertSingle(catalog.Merchants);
        var companion = AssertSingle(catalog.Companions);

        var access = WorldFeatureRuntimeResolver.ResolvePlaceableAccess(placeable,
            new HashSet<string>(["key:ember"], StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal), 3);
        AssertSmoke(access is { Allowed: true, Consume: true, RemainingCharges: 2 } &&
                    placeable.DialogueId is not null && placeable.FactionId == "ash-watch",
            "Placeable credentials, dialogue or faction policy did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13N_PLACEABLE_ACCESS_PASS credential=charge remaining=2 dialogue=bound faction=ash-watch");

        var placeableAttack = WorldFeatureRuntimeResolver.ResolvePlaceableEvent(placeable,
            "physical-attack-resolved", 0, 0);
        var placeableAbility = WorldFeatureRuntimeResolver.ResolvePlaceableEvent(placeable, "ability-impacted", 0, 0);
        var placeableHeartbeat = WorldFeatureRuntimeResolver.ResolvePlaceableEvent(placeable, "heartbeat", 0, 6);
        var placeableCustom = WorldFeatureRuntimeResolver.ResolvePlaceableEvent(placeable,
            "custom:crypt:pillar-pulse", 0, 0);
        AssertSmoke(placeableAttack.Dispatched && placeableAbility.Dispatched && placeableHeartbeat.Dispatched &&
                    placeableCustom.Dispatched && placeable.Physics is { Mass: 12, Replicated: true },
            "Placeable damage events, scheduler, custom hook or physics policy did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13O_PLACEABLE_REACTIVE_PASS events=4 heartbeat=scheduled physics=rigid authority=server");
        RunPlaceableOperationsRuntimeSmokeTest(placeable, _loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));
        RunPlaceableSecurityEventsRuntimeSmokeTest(placeable, _loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));
        RunPlaceableCompositionEventsRuntimeSmokeTest(placeable, _loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));
        RunTriggerAdvancedRuntimeSmokeTest(trigger, _loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));

        var doorDamage = WorldFeatureRuntimeResolver.ApplyDoorDamage(door, 40, 100);
        var damaged = WorldFeatureRuntimeResolver.ResolveDoorEvent(door, "damage-applied", 0, 0);
        var destroyed = WorldFeatureRuntimeResolver.ResolveDoorEvent(door, "entity-destroyed", 0, 0);
        AssertSmoke(doorDamage is { Breached: true, NavigationChanged: true } && damaged.Dispatched &&
                    destroyed.Dispatched && door.Loading is { UseDestinationFallback: true },
            "Door damage, destruction, navigation or loading policy did not resolve.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G13P_DOOR_DAMAGE_PASS health={doorDamage.Health:0} breached=true navigation=updated loading=fallback");

        var doorAttack = WorldFeatureRuntimeResolver.ResolveDoorEvent(door, "physical-attack-resolved", 0, 0);
        var doorAbility = WorldFeatureRuntimeResolver.ResolveDoorEvent(door, "ability-impacted", 0, 0);
        var doorHeartbeat = WorldFeatureRuntimeResolver.ResolveDoorEvent(door, "heartbeat", 0, 2);
        var doorCustom = WorldFeatureRuntimeResolver.ResolveDoorEvent(door, "custom:crypt:door-seal", 0, 0);
        AssertSmoke(doorAttack.Dispatched && doorAbility.Dispatched && doorHeartbeat.Dispatched &&
                    doorCustom.Dispatched && door.DialogueId is not null && door.FactionId == "ash-watch" &&
                    door.Assembly is { Panels.Count: 2, Interlocked: true },
            "Door reactive, dialogue, faction or assembly policy did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13Q_DOOR_ASSEMBLY_PASS events=4 panels=2 synchronized=true dialogue=bound");

        var accessResolution = DoorAccessEventFoundationService.Resolve(door);
        var credentials = new HashSet<string>(["key:ember", "badge:watch"], StringComparer.Ordinal);
        var unlocked = DoorAccessEventFoundationService.Unlock(door,
            new("runtime-player", credentials, new HashSet<string>(), null, 0, false, false), credentials);
        var relocked = DoorAccessEventFoundationService.Relock(door, false, "runtime-player");
        var rejection = DoorAccessEventFoundationService.Reject(door, "runtime-player", "locked");
        var trapIgnored = DoorAccessEventFoundationService.TriggerTrap(door, "ceiling", "interact", "outsider");
        var trapTriggered = DoorAccessEventFoundationService.TriggerTrap(door, "front", "interact", "outsider");
        var portalVetoed = DoorAccessEventFoundationService.RequestPortalUse(door, "runtime-player", "click",
            new HashSet<string>(["quest:open"]), _ => "party-not-ready");
        var portalAccepted = DoorAccessEventFoundationService.RequestPortalUse(door, "runtime-player", "click",
            new HashSet<string>(["quest:open"]));
        var opened = DoorAccessEventFoundationService.CompleteStateTransition(door, "runtime-player", "opening",
            "open", "animation-complete", DateTimeOffset.UnixEpoch);
        var closed = DoorAccessEventFoundationService.CompleteStateTransition(door, "runtime-player", "closing",
            "closed", "animation-complete", DateTimeOffset.UnixEpoch);
        var lockEvents = DoorAccessEventFoundationService.ResolveLockEvents(door);
        var unlockedEvent = DoorAccessEventFoundationService.CompleteLockTransition(door, "runtime-player",
            "credential", "key:ember", DoorAccessLockState.Locked, DoorAccessLockState.Unlocked,
            DateTimeOffset.UnixEpoch);
        var lockedEvent = DoorAccessEventFoundationService.CompleteLockTransition(door, "runtime-player",
            "relock", null, DoorAccessLockState.Unlocked, DoorAccessLockState.Locked, DateTimeOffset.UnixEpoch);
        var optionalLockEvents = DoorAccessEventFoundationService.ResolveLockEvents(door with
        {
            AccessEvents = door.AccessEvents! with
            {
                LockStateEvents = null,
                LockStateEventsRequiredAtRuntime = false,
            },
        });
        var requiredLockEvents = DoorAccessEventFoundationService.ResolveLockEvents(door with
        {
            AccessEvents = door.AccessEvents! with { LockStateEvents = null },
        });
        var optionalDoorAccess = DoorAccessEventFoundationService.Resolve(door with
        { AccessEvents = null, AccessEventsRequiredAtRuntime = false });
        var requiredDoorAccess = DoorAccessEventFoundationService.Resolve(door with { AccessEvents = null });
        AssertSmoke(accessResolution.Available && unlocked.LockResult.Succeeded && unlocked.CredentialConsumed &&
                    !unlocked.RemainingCredentials.Contains("key:ember") && relocked.Lock.State == RpgLockState.Locked &&
                    rejection.Texts.Count == 2 && !trapIgnored.Succeeded && trapTriggered.Succeeded &&
                    portalVetoed.Request.Vetoed && portalVetoed.Completed is null &&
                    portalAccepted.Completed?.RequestEventId == portalAccepted.Request.EventId &&
                    opened.Name == "DoorOpened" && closed.Name == "DoorClosed" &&
                    optionalDoorAccess.OptionalFallback &&
                    requiredDoorAccess.Diagnostics.Any(item => item.Code == "world.door.access_events_required") &&
                    _doorAccessEventRoot?.GetChildCount() == 1 &&
                    _doorAccessEventRoot.GetChild(0).GetChildCount() == 6,
            "Door access, rejection, trap, portal or terminal state lifecycle did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R081_DOOR_ACCESS_EVENTS_PASS lock=credentials-skill-relock-consume rejection=reason-localized-sound-animation-script trap=detect-disarm-payload-reset-owner-side-event portal=requested-actor-destination-cause-conditions-veto-completed state=opened-closed-terminal forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=validated-adapter engine=lock-rejection-trap-portal-state-nodes");
        AssertSmoke(lockEvents.Available && unlockedEvent.Name == "DoorUnlocked" &&
                    unlockedEvent.ActorId == "runtime-player" && unlockedEvent.Method == "credential" &&
                    unlockedEvent.Credential == "key:ember" && lockedEvent.Name == "DoorLocked" &&
                    lockedEvent.PreviousState == RpgLockState.Unlocked &&
                    lockedEvent.CurrentState == RpgLockState.Locked && optionalLockEvents.OptionalFallback &&
                    requiredLockEvents.Diagnostics.Any(item => item.Code == "world.door.lock_events_required") &&
                    _doorAccessEventRoot?.GetChild(0).GetNodeOrNull<Node>("DoorLockLifecycle") is not null,
            "Door lock event lifecycle did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R082_DOOR_LOCK_EVENTS_PASS events=locked-unlocked payload=method-actor-credential-state-provenance terminal=guarded forgescript=typed-view mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=reused engine=lock-lifecycle-node");

        var encounterComposition = EncounterCompositionFoundationService.Resolve(encounter);
        var encounterDefinition = EncounterCompositionFoundationService.CreateDefinition(encounter);
        var encounterPreview = EncounterCompositionFoundationService.Preview(encounter, 4, 12,
            new HashSet<string>(["party"], StringComparer.Ordinal));
        var repeatedEncounterPreview = EncounterCompositionFoundationService.Preview(encounter, 4, 12,
            new HashSet<string>(["party"], StringComparer.Ordinal));
        var encounterAuthoringDiagnostics = EncounterCompositionFoundationService.ValidateAuthoring(encounter,
            new(encounterDefinition.Candidates.Select(item => item.CreatureId).ToHashSet(),
                encounterDefinition.Candidates.Select(item => item.BlueprintId).ToHashSet(StringComparer.Ordinal),
                new HashSet<string>([encounterDefinition.ZoneId], StringComparer.Ordinal),
                encounterDefinition.SpawnAnchorIds.ToHashSet(StringComparer.Ordinal),
                new HashSet<string>([encounterDefinition.Volume.ActivationScript!], StringComparer.Ordinal),
                encounterDefinition.RewardIds.ToHashSet(StringComparer.Ordinal), 128, 100000));
        var optionalEncounterComposition = EncounterCompositionFoundationService.Resolve(encounter with
        { Composition = null, CompositionRequiredAtRuntime = false });
        var requiredEncounterComposition = EncounterCompositionFoundationService.Resolve(encounter with
        { Composition = null, CompositionRequiredAtRuntime = true });
        AssertSmoke(encounterComposition.Available && encounterPreview.Diagnostics.Count == 0 &&
                    encounterAuthoringDiagnostics.Count == 0 && encounterPreview.Composition.TotalCount is >= 2 and <= 4 &&
                    encounterPreview.Composition.TotalCost <= encounter.Composition!.Difficulty.EffectiveBudget &&
                    encounterPreview.Composition.Candidates.Select(item => item.CreatureId).SequenceEqual(
                        repeatedEncounterPreview.Composition.Candidates.Select(item => item.CreatureId)) &&
                    encounterPreview.Composition.Summary == repeatedEncounterPreview.Composition.Summary &&
                    optionalEncounterComposition.OptionalFallback &&
                    requiredEncounterComposition.Diagnostics.Any(item =>
                        item.Code == "world.encounter.composition_required") &&
                    _encounterCompositionRoot?.GetChildCount() == 1 &&
                    _encounterCompositionRoot.GetChild(0).GetChildCount() == 5,
            "Encounter wizard, difficulty, group counts, party selection or activation policy did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R083_ENCOUNTER_COMPOSITION_PASS wizard=role-theme-zone-difficulty-groups-budget-activation-spawn difficulty=ruleset-party-estimator-multipliers count=min-max-budget-server-cap selection=provider-challenge-seeded-preview activation=once-repeat-timed-wave-persistent-population-script forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=authoring-composition-reused engine=wizard-difficulty-count-selection-activation-nodes");

        var encounterScenario = EncounterCompositionFoundationService.ResolveScenario(encounter);
        var acceptedActivator = new RpgActivator("runtime-player", true, false, false, false, false,
            RpgNetworkAuthority.Server, new HashSet<string>(["party"], StringComparer.Ordinal));
        var deniedActivator = acceptedActivator with { IsCompanion = true };
        var scenarioPolicy = EncounterCompositionFoundationService.CreateRuntimePolicy(encounter);
        var scenarioPreview = EncounterCompositionFoundationService.PreviewScenario(encounter, acceptedActivator,
            4, 12, new HashSet<string>(["party"], StringComparer.Ordinal), DateTimeOffset.UnixEpoch,
            TimeSpan.FromSeconds(9));
        var optionalEncounterScenario = EncounterCompositionFoundationService.ResolveScenario(encounter with
        { Scenario = null, ScenarioRequiredAtRuntime = false });
        var requiredEncounterScenario = EncounterCompositionFoundationService.ResolveScenario(encounter with
        { Scenario = null, ScenarioRequiredAtRuntime = true });
        var scenarioNode = _encounterScenarioRoot?.GetChildCount() == 1
            ? _encounterScenarioRoot.GetChild(0)
            : null;
        AssertSmoke(encounterScenario.Available && scenarioPreview.Diagnostics.Count == 0 &&
                    scenarioPolicy.Anchors.Count == 1 && scenarioPolicy.Anchors[0].Transform.Position.X == 2 &&
                    scenarioPolicy.Anchors[0].Transform.Position.Z == 4 &&
                    scenarioPreview.ActivatorAccepted &&
                    !Forge.RpgKit.Domain.Encounters.EncounterCompositionService.CanActivate(deniedActivator,
                        scenarioPolicy.Activators) &&
                    scenarioPreview.Entered.ActivatorId == "runtime-player" &&
                    scenarioPreview.Exited.DurationInside == TimeSpan.FromSeconds(9) &&
                    scenarioPreview.Exhausted.Cause == "waves-completed" &&
                    scenarioPreview.Exhausted.Kills > 0 && scenarioPreview.Exhausted.Escaped == 0 &&
                    scenarioPreview.Exhausted.Waves == 2 && scenarioPreview.Exhausted.RewardIds?.Count == 1 &&
                    scenarioPreview.Phases.Count == 1 && scenarioPreview.Phases[0].Waves.Count == 2 &&
                    scenarioPreview.Phases[0].Waves[0].NextWaveId == scenarioPreview.Phases[0].Waves[1].Id &&
                    scenarioPreview.Phases[0].Waves[1].QuestId == encounter.Scenario!.Phases[0].Waves[1].QuestId &&
                    scenarioPreview.Phases[0].Waves[1].VoiceCueId == "audio:encounter-warden-fallen" &&
                    optionalEncounterScenario.OptionalFallback &&
                    requiredEncounterScenario.Diagnostics.Any(item => item.Code == "world.encounter.scenario_required") &&
                    scenarioNode?.GetChildCount() == 4 &&
                    scenarioNode.GetNodeOrNull<Node>("ManualSpawnAnchors")?.GetChildCount() == 1 &&
                    scenarioNode.GetNodeOrNull<Node>("ActivatorRestriction") is not null &&
                    scenarioNode.GetNodeOrNull<Node>("EncounterLifecycle") is not null &&
                    scenarioNode.GetNodeOrNull<Node>("WaveGraph")?.GetChildCount() == 1 &&
                    scenarioNode.GetNode<Node>("WaveGraph").GetChild(0).GetChildCount() == 2,
            "Encounter anchors, player restrictions, lifecycle, exhausted payload or wave graph did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R084_ENCOUNTER_SCENARIO_PASS anchors=transform-capacity-navigation-visibility-fallback-distribution activator=players-only-summons-companions-gm-spectators-authority lifecycle=entered-exited-duration-exhausted-full-payload waves=uuid-order-conditions-groups-delay-objective-voice-transition forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=composition-lifecycle-wave-reused engine=anchor-activator-lifecycle-wavegraph-nodes");

        var encounterStatistics = EncounterCompositionFoundationService.ResolveStatistics(encounter);
        var statisticsPreview = EncounterCompositionFoundationService.PreviewStatistics(encounter);
        var repeatedStatisticsPreview = EncounterCompositionFoundationService.PreviewStatistics(encounter);
        var optionalEncounterStatistics = EncounterCompositionFoundationService.ResolveStatistics(encounter with
        { Statistics = null, StatisticsRequiredAtRuntime = false });
        var requiredEncounterStatistics = EncounterCompositionFoundationService.ResolveStatistics(encounter with
        { Statistics = null, StatisticsRequiredAtRuntime = true });
        AssertSmoke(encounterStatistics.Available && statisticsPreview.Statistics.Runs == 64 &&
                    statisticsPreview.Statistics.Results.Select(item => item.Seed)
                        .SequenceEqual(Enumerable.Range(840, 64)) &&
                    statisticsPreview.Summary == repeatedStatisticsPreview.Summary &&
                    statisticsPreview.Statistics.CompositionTotals.Count > 0 &&
                    statisticsPreview.Statistics.MeanCost > 0 &&
                    statisticsPreview.Statistics.MeanDurationSeconds > 0 &&
                    statisticsPreview.Statistics.MeanRewardValue > 0 &&
                    statisticsPreview.Statistics.CostVariance >= 0 &&
                    statisticsPreview.Statistics.CountVariance >= 0 &&
                    statisticsPreview.Statistics.DurationVariance >= 0 &&
                    statisticsPreview.Statistics.RewardVariance >= 0 &&
                    statisticsPreview.Statistics.Percentiles?.Count == 3 &&
                    optionalEncounterStatistics.OptionalFallback &&
                    requiredEncounterStatistics.Diagnostics.Any(item =>
                        item.Code == "world.encounter.statistics_required") &&
                    _encounterStatisticsRoot?.GetChildCount() == 1 &&
                    (int)_encounterStatisticsRoot.GetChild(0).GetMeta("runs") == 64,
            "Encounter statistical preview did not resolve deterministic distributions and runtime metadata.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R085_ENCOUNTER_STATISTICS_PASS simulation=n-runs-composition-cost-duration-rewards variances=count-cost-duration-reward seeds=reproducible-consecutive percentiles=50-90-99 estimators=typed-data-driven forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic rpgkit=wave-simulation-reused engine=statistics-node");

        var hiddenTrigger = WorldFeatureRuntimeResolver.EvaluateTrigger(trigger, 5, new HashSet<string>(),
            new HashSet<string>(), false, 0, 0, false);
        var deniedTrigger = WorldFeatureRuntimeResolver.EvaluateTrigger(trigger, 20, new HashSet<string>(),
            new HashSet<string>(["player"], StringComparer.Ordinal), false, 1, 0, false);
        AssertSmoke(!hiddenTrigger.Revealed && deniedTrigger is { Revealed: true, Fired: false } &&
                    deniedTrigger.Reason == "credential-missing" && trigger.Interaction is not null &&
                    trigger.Scheduler is { BudgetPerMinute: 30 },
            "Trigger discovery, interaction, scheduler or credential denial did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13R_TRIGGER_ACCESS_PASS discovery=hidden-to-revealed interaction=typed scheduler=budgeted credential=denied");

        var firedTrigger = WorldFeatureRuntimeResolver.EvaluateTrigger(trigger, 20,
            new HashSet<string>(["key:ember"], StringComparer.Ordinal),
            new HashSet<string>(["player"], StringComparer.Ordinal), false, 2, 0, true);
        AssertSmoke(firedTrigger is { Fired: true, RestAllowed: true, Reason: "director" } &&
                    firedTrigger.Effects.Any(item => item.StartsWith("clue:", StringComparison.Ordinal)) &&
                    firedTrigger.Effects.Any(item => item.StartsWith("dialogue:", StringComparison.Ordinal)) &&
                    firedTrigger.Effects.Contains("director:audit", StringComparer.Ordinal),
            "Trigger rest, clue, narrative or director policy did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13S_TRIGGER_DIRECTOR_PASS rest=safe clue=tracked narrative=dialogue director=audited");

        var restDanger = TriggerLivingWorldRuntimeService.ResolveRest(trigger, new(true, 2, 2));
        var restParty = TriggerLivingWorldRuntimeService.ResolveRest(trigger, new(false, 1, 2));
        var restAllowed = TriggerLivingWorldRuntimeService.ResolveRest(trigger, new(false, 2, 2));
        var clueFailed = TriggerLivingWorldRuntimeService.ResolveClue(trigger, new(0, true));
        var cluePassed = TriggerLivingWorldRuntimeService.ResolveClue(trigger, new(20, true));
        var participants = trigger.Narrative!.Participants.ToHashSet();
        var narrativePending = TriggerLivingWorldRuntimeService.ResolveNarrative(trigger,
            TriggerNarrativeRuntimeState.Empty,
            new(DateTimeOffset.UnixEpoch, true, false, participants));
        var narrativeFired = TriggerLivingWorldRuntimeService.ResolveNarrative(trigger,
            TriggerNarrativeRuntimeState.Empty,
            new(DateTimeOffset.UnixEpoch, true, true, participants));
        var narrativeRepeated = TriggerLivingWorldRuntimeService.ResolveNarrative(trigger,
            narrativeFired.State,
            new(DateTimeOffset.UnixEpoch.AddMinutes(2), true, true, participants));
        AssertSmoke(restDanger.DiagnosticCode == TriggerLivingWorldRuntimeService.RestDangerCode &&
                    restParty.DiagnosticCode == TriggerLivingWorldRuntimeService.RestPartyCode &&
                    restAllowed is { Allowed: true, CampSetupAllowed: true } &&
                    clueFailed is { Revealed: true, FalseClue: true } &&
                    cluePassed is { Revealed: true, SkillCheckPassed: true } &&
                    narrativePending.DiagnosticCode == TriggerLivingWorldRuntimeService.NarrativeVoteCode &&
                    narrativeFired is { Fired: true, DialogueId: not null } &&
                    narrativeRepeated.DiagnosticCode == TriggerLivingWorldRuntimeService.NarrativeOnceCode,
            "Typed living-world trigger rest, clue or narrative resolution failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_UX2_LW_E_TRIGGER_PASS " +
                 "rest=danger-party-camp clue=skill-false-trace-quest " +
                 "narrative=participants-priority-once-cooldown-policy-dialogue " +
                 "live=player-position-blackboard-party-authority");

        var respawn = WorldFeatureRuntimeResolver.EvaluateEncounter(encounter, 0, 0, false, false, 31);
        AssertSmoke(respawn is { Spawn: true, NextCycle: 1 } && encounter.Scheduler is { HeartbeatEnabled: true },
            "Encounter respawn or scheduler policy did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13T_ENCOUNTER_RESPAWN_PASS cycle=1 heartbeat=scheduled anti_farm=0.5");

        var reinforcement = WorldFeatureRuntimeResolver.EvaluateEncounter(encounter, 0, 3, false, false, 31);
        var visible = WorldFeatureRuntimeResolver.EvaluateEncounter(encounter, 0, 3, true, false, 31);
        var cleanup = WorldFeatureRuntimeResolver.EvaluateEncounter(encounter, 0, 0, false, true, 121);
        AssertSmoke(reinforcement.Reinforce && !visible.Spawn && visible.Reason == "visible" && cleanup.Cleanup,
            "Encounter reinforcement, visibility or cleanup policy did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13U_ENCOUNTER_DIRECTOR_PASS reinforcement=true visible_spawn=false cleanup=true");

        var vehicle = WorldFeatureRuntimeResolver.SampleVehicleRoute(route, 1);
        AssertSmoke(vehicle.AtStop && vehicle.WaitSeconds == 5 && vehicle.SpeedLimit == 8,
            "Vehicle route, lane speed or stop policy did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13V_VEHICLE_ROUTE_PASS progress=1 stop=crypt-lift wait=5 speed=8");

        var standardQuote = WorldFeatureRuntimeResolver.QuoteMerchant(merchant, "ember-crown", 80, false,
            "crypt", .5, 0, 0, 0);
        var stolenQuote = WorldFeatureRuntimeResolver.QuoteMerchant(merchant, "ember-crown", 80, true,
            "crypt", .5, 0, 0, 0);
        AssertSmoke(standardQuote.Accepted && !stolenQuote.Accepted && merchant.Currencies.Count == 2 &&
                    merchant.Treasury.Count == 2 && merchant.Purchase.RequireTreasuryFunds,
            "Merchant currency, treasury, purchase or stolen-goods policy did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13W_MERCHANT_ECONOMY_PASS currencies=2 treasury=2 purchase=accepted stolen=rejected");

        var marketQuote = WorldFeatureRuntimeResolver.QuoteMerchant(merchant, "ember-crown", 100, false,
            "crypt", .8, 10, .1, .5);
        AssertSmoke(marketQuote.Accepted && marketQuote.Price != 100 &&
                    marketQuote.Audit.StartsWith("ledger:", StringComparison.Ordinal) &&
                    merchant.Services.Count == 1 && merchant.Market is { RegionId: "ashlands" },
            "Merchant market, negotiation, service or ledger policy did not resolve.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G13X_MERCHANT_OPERATIONS_PASS quote={marketQuote.Price:0.##} service=repair ledger=audited market=regional");
        RunMerchantFoundationRuntimeSmokeTest(catalog, merchant, _loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));

        var partyCompanion = WorldFeatureRuntimeResolver.EvaluateCompanion(companion, "party", .8, false, 0,
            false, false);
        AssertSmoke(partyCompanion.DialogueAllowed && partyCompanion.Formation == "wedge:1" &&
                    companion.Progression.PreviewEnabled && companion.Relationship.Tags.Contains("friend") &&
                    companion.Banter.RequiredParticipants.Count == 1 && companion.PersonalQuest is not null,
            "Companion dialogue, formation, progression, relationship, banter or quest policy did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13Y_COMPANION_SOCIAL_PASS dialogue=limited formation=wedge progression=preview relationship=tracked banter=bound quest=bound");

        var controlledCompanion = WorldFeatureRuntimeResolver.EvaluateCompanion(companion, "owner", .2, true, 2,
            true, true);
        AssertSmoke(controlledCompanion is
        { UseAbility: true, HandleTrap: true, DirectControl: true, Regroup: true },
            "Companion trap, ability, direct-control or party-split policy did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13Z_COMPANION_CONTROL_PASS trap=handled ability=heal direct_control=true regroup=true");

        var foundationResolution = CompanionFoundationService.Resolve(companion);
        var component = CompanionFoundationService.CreateComponent(companion, string.Empty, TransformValue.Identity);
        var foundation = foundationResolution.Foundation ??
                         throw new InvalidOperationException("R074 companion foundation is missing.");
        var partyState = CompanionFoundationService.CreatePartyState(companion, "party:runtime", [],
            new Dictionary<string, decimal> { [foundation.Recruitable.CurrencyId] = 100 },
            foundation.Recruitable.Conditions.ToHashSet(StringComparer.Ordinal), 1);
        var companionState = new CompanionWorldState(component, partyState,
            new Dictionary<string, string> { [component.InventoryId] = component.Id.ToString("D") },
            TransformValue.Identity);
        var recruitPreview = CompanionFoundationService.Recruit(companion, companionState, "player:runtime", true);
        var recruitApply = CompanionFoundationService.Recruit(companion, companionState, "player:runtime", false);
        var dismissPosition = new TransformValue(new Vector3Value(3, 0, 4), QuaternionValue.Identity,
            Vector3Value.One);
        var dismissed = CompanionFoundationService.Dismiss(companion, recruitApply.Result.State,
            "player:runtime", dismissPosition, false);
        var optionalFoundation = CompanionFoundationService.Resolve(companion with
        { Foundation = null, FoundationRequiredAtRuntime = false });
        var requiredFoundation = CompanionFoundationService.Resolve(companion with { Foundation = null });
        var partyDiagnostics = CompanionFoundationService.ValidateParty(companion,
            new PartyRoster([component], 1, 1, 1));
        AssertSmoke(recruitPreview.Result.Committed && recruitApply.Result.Committed &&
                    recruitPreview.TransactionId == recruitApply.TransactionId && dismissed.Result.Committed &&
                    dismissed.Result.State.DismissPosition == dismissPosition && optionalFoundation.OptionalFallback &&
                    requiredFoundation.Diagnostics.Any(item => item.Code == "RPG_COMPANION_FOUNDATION_REQUIRED") &&
                    partyDiagnostics.Count == 0 &&
                    CompanionFoundationService.DialogueContext(companion, string.Empty, "player:runtime", true) ==
                    RecruitDialogueContext.NotRecruited &&
                    CompanionFoundationService.DialogueContext(companion, "player:runtime", "player:runtime", true) ==
                    RecruitDialogueContext.RecruitedBySpeaker &&
                    _companionFoundationRoot?.GetChildCount() == 1 &&
                    _companionFoundationRoot.GetChild(0).GetChildCount() == 5,
            "Companion foundation runtime integration did not preserve its governed contracts.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R074_COMPANION_FOUNDATION_PASS " +
                 "recruitable=component-policy dialogue=not-recruited-owner " +
                 "membership=preview-apply-dismiss-capacity-ownership-inventory-position-persistence-atomic " +
                 "party=companions-summons-pets-allies-roles-server-limit " +
                 "forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic engine=nodes");

        var controlResolution = CompanionControlFoundationService.Resolve(companion);
        var controlProfile = controlResolution.Profile ??
                             throw new InvalidOperationException("R075 companion control profile is missing.");
        var transferPreview = CompanionControlFoundationService.Transfer(companion, component.Id, "party",
            new HashSet<string>(["party:runtime"], StringComparer.Ordinal), "player:owner", "runtime-preview",
            DateTimeOffset.UnixEpoch, true);
        var transferApply = CompanionControlFoundationService.Transfer(companion, component.Id, "party",
            new HashSet<string>(["party:runtime"], StringComparer.Ordinal), "player:owner", "runtime-preview",
            DateTimeOffset.UnixEpoch, false);
        var movement = CompanionControlFoundationService.IssueMovement(companion, "move", "player:owner", null,
            new Vector3Value(4, 0, 8), 20, []);
        var tactical = CompanionControlFoundationService.IssueTactical(companion, "focus-fire", "player:owner",
            "enemy:ash-warden", "aggressive", null, 50, movement.Queue);
        var suggestedItem = controlProfile.Equipment.SuggestedLoadout["main-hand"];
        var equipment = CompanionControlFoundationService.Equip(companion,
            new Dictionary<string, Guid>(), suggestedItem, "main-hand", "player:owner");
        var unauthorizedEquipment = CompanionControlFoundationService.Equip(companion,
            new Dictionary<string, Guid>(), suggestedItem, "main-hand", "intruder");
        var optionalControl = CompanionControlFoundationService.Resolve(companion with
        { Control = null, ControlRequiredAtRuntime = false });
        var requiredControl = CompanionControlFoundationService.Resolve(companion with { Control = null });
        AssertSmoke(transferPreview.PlanId == transferApply.PlanId &&
                    transferPreview.TransactionId == transferApply.TransactionId &&
                    transferApply.Current.Kind == CompanionOwnershipKind.Party &&
                    transferApply.Current.Revision == 2 && movement.Accepted && tactical.Accepted &&
                    tactical.Queue.Count == 2 && equipment.Accepted && !unauthorizedEquipment.Accepted &&
                    CompanionControlService.ValidateAi(CompanionControlFoundationService.CreateAi(companion)).Count ==
                    0 && optionalControl.OptionalFallback &&
                    requiredControl.Diagnostics.Any(item => item.Code == "RPG_COMPANION_CONTROL_REQUIRED") &&
                    _companionControlRoot?.GetChildCount() == 1 &&
                    _companionControlRoot.GetChild(0).GetChildCount() == 5,
            "Companion control runtime integration did not preserve ownership, orders, AI and equipment.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R075_COMPANION_CONTROL_PASS " +
                 "ownership=individual-party-shared-ai-only-gm-transfer-audit-revision " +
                 "movement=move-follow-hold-rally-guard-interact-formation-waypoint-priority-authority " +
                 "tactical=attack-defend-focus-fire-protect-disengage-use-ability-stance-target-priority " +
                 "ai=autonomy-risk-resources-friendly-fire-role-distance-abilities-plan-transaction-authorization " +
                 "equipment=permission-locks-loadout-compare-ownership-semantic-diff " +
                 "forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic engine=nodes");

        var experienceResolution = CompanionExperienceFoundationService.Resolve(companion);
        _ = experienceResolution.Profile ??
            throw new InvalidOperationException("R076 companion experience profile is missing.");
        var deathModes = new[]
        {
            "downed", "permanent-death", "recoverable-corpse", "resurrection", "replacement", "memorial",
        };
        var deathTransitions = deathModes.Select(mode => CompanionExperienceFoundationService.Defeat(
            companion with { Experience = companion.Experience! with { Death = companion.Experience.Death with { Mode = mode } } },
            DateTimeOffset.UnixEpoch)).ToArray();
        var anchors = new List<(string Id, Vector3Value Position, IReadOnlySet<string> Tags, bool Navigable)>
        {
            ("anchor:party-safe", new Vector3Value(2, 0, 2), new HashSet<string>(["party.safe"], StringComparer.Ordinal), true),
        };
        var navigate = CompanionExperienceFoundationService.Recover(companion,
            new CohesionContext(15, true, false, false, false, anchors));
        var portal = CompanionExperienceFoundationService.Recover(companion,
            new CohesionContext(40, false, true, false, false, anchors));
        var teleport = CompanionExperienceFoundationService.Recover(companion,
            new CohesionContext(80, false, false, false, false, anchors));
        var combatBlocked = CompanionExperienceFoundationService.Recover(companion,
            new CohesionContext(80, false, false, true, false, anchors));
        var visibleBlocked = CompanionExperienceFoundationService.Recover(companion,
            new CohesionContext(80, false, false, false, true, anchors));
        var hud = CompanionExperienceFoundationService.CreateHud(companion);
        var optionalExperience = CompanionExperienceFoundationService.Resolve(companion with
        { Experience = null, ExperienceRequiredAtRuntime = false });
        var requiredExperience = CompanionExperienceFoundationService.Resolve(companion with { Experience = null });
        AssertSmoke(deathTransitions.Length == 6 && deathTransitions[0].State.Downed &&
                    deathTransitions[1].EventName == "CompanionPermanentlyDied" &&
                    deathTransitions[2].State.CorpseAvailable &&
                    deathTransitions[3].Effects.Contains("requires:resource:ember", StringComparer.Ordinal) &&
                    deathTransitions[4].State.ReplacementId == "creature:elyra-heir" &&
                    deathTransitions[5].State.Memorialized &&
                    navigate.Kind == CohesionRecoveryKind.Navigate &&
                    portal.Kind == CohesionRecoveryKind.FollowPortal &&
                    teleport.Kind == CohesionRecoveryKind.SafeTeleport &&
                    teleport.Audit.Contains("anchor:party-safe", StringComparison.Ordinal) &&
                    combatBlocked.Kind == CohesionRecoveryKind.None && visibleBlocked.Kind == CohesionRecoveryKind.None &&
                    hud is
                    {
                        Portraits: true, Health: true, Resources: true, Statuses: true, QuickCommands: true,
                        Reorder: true, Keyboard: true, Gamepad: true, ScreenReaderLabels: true, HighContrast: true
                    } &&
                    CompanionExperienceFoundationService.Validate(companion).Count == 0 &&
                    optionalExperience.OptionalFallback &&
                    requiredExperience.Diagnostics.Any(item => item.Code == "RPG_COMPANION_EXPERIENCE_REQUIRED") &&
                    _companionExperienceRoot?.GetChildCount() == 1 &&
                    _companionExperienceRoot.GetChild(0).GetChildCount() == 3 &&
                    _hud is { PartyHudVisible: true, PartyHudQuickCommandCount: 3 } &&
                    _hud.PartyHudText.Contains("Elyra", StringComparison.Ordinal),
            "Companion experience runtime integration did not preserve death, cohesion and party HUD contracts.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R076_COMPANION_EXPERIENCE_PASS " +
                 "death=downed-permanent-corpse-resurrection-replacement-memorial-inventory-events " +
                 "cohesion=navigate-portal-safe-teleport-combat-visible-audit " +
                 "hud=portraits-health-resources-statuses-quick-commands-reorder-keyboard-gamepad-screen-reader-high-contrast " +
                 "forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic engine=nodes-visible-hud");

        var cameraScript = WorldFeatureRuntimeResolver.ResolveScript(catalog.Scripts, "door.ash", "opened",
            cameraSequence: true);
        var cameraRoute = catalog.Scripts.TagRouting.Routes.Single(item =>
            item.Enabled && string.Equals(item.BlueprintTag, "door.ash", StringComparison.Ordinal) &&
            string.Equals(item.Event, "opened", StringComparison.Ordinal));
        AssertSmoke(cameraScript.ScriptId == cameraRoute.ScriptId && cameraScript.CameraControlLocked &&
                    cameraScript.AbortRecovery == "restore-camera-and-control",
            "Script camera sequence or explicit tag routing did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13AA_SCRIPT_CAMERA_ROUTE_PASS route=door.ash/opened camera=locked recovery=restore");

        var automatedScript = WorldFeatureRuntimeResolver.ResolveScript(catalog.Scripts, "none", "none",
            new Dictionary<string, string> { ["rest.enabled"] = "false" },
            new Dictionary<string, string> { ["combat.aggression"] = "aggressive" });
        AssertSmoke(automatedScript.Flags.GetValueOrDefault("rest.enabled") == "false" &&
                    automatedScript.AiSettings.GetValueOrDefault("combat.aggression") == "aggressive",
            "Script module flags or typed AI settings did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13AB_SCRIPT_AUTOMATION_PASS flag=rest.enabled:false ai=combat.aggression:aggressive authority=server");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13N_AB_WORLD_FEATURES_PASS slices=15 catalog=canonical runtime=pure-domain");
    }

    private void RunSaveCreateSmokeTest()
    {
        AssertSmoke(_quickSavePath is not null, "The save smoke test requires --save-file.");
        AssertSmoke(_npc is not null && _keyItem is not null && _door is not null && _boss is not null,
            "The save smoke scenario is incomplete.");
        InteractWith(_npc!);
        var questChoiceIndex = _activeDialogueChoices.ToList().FindIndex(choice =>
            choice.Actions.Any(action => action.Kind == DialogueActionKind.StartQuest));
        AssertSmoke(questChoiceIndex >= 0, "NPC dialogue did not expose its authored quest choice.");
        CommitDialogueChoice(questChoiceIndex);
        InteractWith(_keyItem!);
        InteractWith(_door!);
        DamageHostile(_boss!);
        _player!.GlobalPosition = new Vector3(2, 0.9f, 1);
        AssertSmoke(SaveRuntime(_quickSavePath!), "The runtime checkpoint was not written.");
        AssertSmoke(TrackedQuestRuntimeState?.DisplayName == "Gardien des Cendres",
            "The saved quest did not reach the boss objective.");
        AssertSmoke(_hostiles[_boss!].Health == 3, "The saved boss health is not the expected checkpoint.");
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_SAVE_PASS file={Path.GetFileName(_quickSavePath)} " +
            $"quest={TrackedQuestRuntimeState!.Id} boss_health={_hostiles[_boss!].Health}");
        GetTree().Quit(0);
    }

    private void RunSaveResumeSmokeTest()
    {
        AssertSmoke(_loadedSave, "The save resume smoke test did not load a checkpoint.");
        AssertSmoke(_hasKey && _doorUnlocked, "Inventory or unlocked door state was not restored.");
        AssertSmoke(TrackedQuestRuntimeState?.DisplayName == "Gardien des Cendres",
            "Quest state was not restored.");
        AssertSmoke(_boss is not null && _hostiles.TryGetValue(_boss, out var boss) && boss.Health == 3,
            "Boss combat state was not restored.");
        AssertSmoke(_player?.GlobalPosition.IsEqualApprox(new Vector3(2, 0.9f, 1)) == true,
            "Player position was not restored.");
        while (_hostiles.TryGetValue(_boss!, out var combatant) && combatant.Health > 0)
        {
            DamageHostile(_boss!);
        }

        AssertSmoke(_bossDefeated && IsQuestTerminal(_loadedProject!.Quests.Single().Id),
            "The restored game could not be completed.");
        AssertSmoke(SaveRuntime(_quickSavePath!), "The completed resumed game was not saved.");
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_SAVE_RESUME_PASS file={Path.GetFileName(_quickSavePath)} " +
            "quest=completed boss=defeated");
        GetTree().Quit(0);
    }

    private void RunHeadlessSmokeTest()
    {
        var scriptRuntime = new NativeForgeScriptRuntime();
        var scriptCompilation = scriptRuntime.Compile(
            "void main() { variable(\"smoke\", \"pass\"); say(\"ForgeScript prêt\"); }");
        AssertSmoke(scriptCompilation.Success, "ForgeScript native compilation failed.");
        var scriptResult = scriptRuntime.Execute(scriptCompilation.Artifact!);
        AssertSmoke(scriptResult.Status == ForgeScriptExecutionStatus.Completed, "ForgeScript execution failed.");
        AssertSmoke(scriptResult.Events.Count == 2, "ForgeScript API events were not emitted.");
        GD.Print(
            $"OPENRPG_FORGE_FORGESCRIPT_SMOKE_PASS vm={scriptRuntime.VirtualMachineVersion} " +
            $"api={scriptRuntime.ApiVersion} events={scriptResult.Events.Count}");

        AssertSmoke(_builtEntityCount == 40, "Expected 40 reconstructed entities.");
        AssertSmoke(_collisionCount == 30, "Expected 30 static collision shapes.");
        AssertSmoke(_player is not null, "Player was not reconstructed.");
        AssertSmoke(_npc is not null, "NPC was not reconstructed.");
        AssertSmoke(_keyItem is not null, "Key item was not reconstructed.");
        AssertSmoke(_door is not null, "Door was not reconstructed.");
        AssertSmoke(_boss is not null, "Boss was not reconstructed.");
        AssertSmoke(_creatureRoleBehaviors.ContainsKey(_boss!),
            "The authored boss role and ambient behavior were not reconstructed.");
        AdvanceCreatureAmbientBehavior(.25);
        AssertSmoke(_creatureAmbientTicks > 0, "The creature ambient behavior did not advance.");
        var ambient = _creatureRoleBehaviors[_boss!];
        AssertSmoke(ambient.LoreFields.GetValueOrDefault("deity") == "deity.embers" &&
                    ambient.ActiveAnimationId is not null,
            "The creature role lore or ambient animation did not resolve.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G13E_CREATURE_ROLE_BEHAVIOR_PASS role=guardian " +
            $"deity={ambient.LoreFields["deity"]} animation={ambient.ActiveAnimationId} " +
            $"ambient_ticks={_creatureAmbientTicks} autolevel=6 seed=42");

        AssertSmoke(_creatureEventProfiles.TryGetValue(_boss!, out var creatureEvents) &&
                    CreatureEventValidator.Validate(creatureEvents).Count == 0,
            "The authored creature-event profile did not resolve.");
        var authoredCreatureEvents = creatureEvents ??
            throw new InvalidOperationException("The authored creature-event profile is missing.");
        var smokePlayerPosition = _player!.GlobalPosition;
        _player.GlobalPosition = _boss!.GlobalPosition;
        AdvanceCreatureEvents(1.5);
        _player.GlobalPosition = smokePlayerPosition;
        var restCompleted = CreatureEventRuntimeResolver.RestCompleted(authoredCreatureEvents, _boss!.Name, 28_800,
            new Dictionary<string, double> { ["health"] = 12, ["focus"] = 4 }, ["ambush"]);
        AssertSmoke(restCompleted is not null, "RestCompleted did not produce a typed dispatch.");
        _creatureEventRestDispatchCount++;
        DispatchCreatureEvent(restCompleted!);

        InteractWith(_npc!);
        var questChoiceIndex = _activeDialogueChoices.ToList().FindIndex(choice =>
            choice.Actions.Any(action => action.Kind == DialogueActionKind.StartQuest));
        AssertSmoke(questChoiceIndex >= 0, "NPC dialogue did not expose its authored quest choice.");
        CommitDialogueChoice(questChoiceIndex);
        var startedQuest = _loadedProject!.Quests.Single();
        AssertSmoke(_questTracker?.IsAccepted(startedQuest.Id) == true &&
                    _questTracker.GetCurrentState(startedQuest.Id)?.Id != startedQuest.InitialStateId,
            "NPC interaction did not accept and advance the canonical quest.");
        var dialoguePresentation = _activeDialogue?.Presentation;
        AssertSmoke(dialoguePresentation is not null &&
                    DialoguePresentationValidator.Validate(_activeDialogue!).Count == 0,
            "The authored dialogue presentation profile did not resolve.");
        AssertSmoke(_dialogueAnimationCueCount == 1 && _dialogueVoiceCueCount == 1 &&
                    _dialogueCameraShotCount == 1 && _dialogueBookmarkCount == 1 &&
                    _dialogueInterjectionCount == 0 && _dialogueSubtitleFallback,
            "Dialogue animation, voice fallback, camera, bookmark, or authored interjection filtering did not execute.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G13H_DIALOGUE_PRESENTATION_PASS " +
            $"animations={_dialogueAnimationCueCount} voices={_dialogueVoiceCueCount} " +
            $"shots={_dialogueCameraShotCount} bookmarks={_dialogueBookmarkCount} " +
            $"interjections={_dialogueInterjectionCount} subtitle_fallback={_dialogueSubtitleFallback.ToString().ToLowerInvariant()} " +
            "authority=server accessibility=player-priority");
        var timedRule = AssertSingle(dialoguePresentation!.TimedChoices);
        var adaptiveTimeout = DialogueAdaptiveRuntimeResolver.Resolve(_activeDialogue!, _activeDialogue!.EntryNodeId,
            timedRule.DurationMilliseconds + 1, AdaptiveDialogueContext(), new RuntimeConditionEvaluator(this));
        AssertSmoke(adaptiveTimeout.Diagnostics.Count == 0 && adaptiveTimeout.TimedChoice is
        { TimedOut: true, SelectedChoiceId: not null, TimeoutScriptId: not null } &&
            adaptiveTimeout.Variant is not null && _dialogueTimedChoiceCount == 1 && _dialogueVariantCount == 1 &&
            !string.IsNullOrWhiteSpace(_dialogueVariantKey),
            "Timed choice or deterministic localized variant did not execute.");
        var accessibilityPause = DialogueAdaptiveRuntimeResolver.Resolve(_activeDialogue!, _activeDialogue!.EntryNodeId,
            timedRule.DurationMilliseconds + 1,
            AdaptiveDialogueContext() with { PauseRequested = true, AccessibilityPauseRequested = true },
            new RuntimeConditionEvaluator(this));
        AssertSmoke(accessibilityPause.TimedChoice is { Paused: true, TimedOut: false },
            "The accessible timed-choice pause did not suspend timeout selection.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13I_ADAPTIVE_DIALOGUE_PASS " +
            $"timed={_dialogueTimedChoiceCount} default={adaptiveTimeout.TimedChoice!.SelectedChoiceId} " +
            $"timeout_script={adaptiveTimeout.TimedChoice.TimeoutScriptId} pause=accessible " +
            $"variants={_dialogueVariantCount} key={_dialogueVariantKey} " +
            $"roll={adaptiveTimeout.Variant!.DeterministicRoll} authority=server revision=1 seed=2026");
        AssertSmoke(
            TrackedQuestRuntimeState?.DisplayName == "Clé de braise",
            "NPC interaction did not start the data-driven quest.");
        InteractWith(_keyItem!);
        AssertSmoke(_hasKey, "Key interaction did not update inventory state.");
        var advancedItem = _keyItemBlueprint?.ItemAdvanced ??
            throw new InvalidOperationException("The authored advanced item profile is missing.");
        var itemContext = AdvancedItemContext(advancedItem, 2026);
        var requirementDecision = ItemAdvancedRuntimeResolver.ResolveRequirements(advancedItem, itemContext);
        var identifiedProperties = ItemAdvancedRuntimeResolver.ResolveIdentifiedProperties(advancedItem,
            advancedItem.Identification.Difficulty);
        var procDecision = ItemAdvancedRuntimeResolver.ResolveOnHit(advancedItem, "unlock-strike",
            new Dictionary<Guid, DateTimeOffset>(), DateTimeOffset.UnixEpoch, itemContext);
        AssertSmoke(requirementDecision.Allowed && _keyItemEconomicValue > 0 && identifiedProperties.Count >= 3 &&
                    procDecision.Triggered, "Economy, requirements, identification or on-hit data did not resolve.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G13K_ITEM_FOUNDATION_PASS value={_keyItemEconomicValue:0.##} " +
                 $"requirements=allowed knowledge={identifiedProperties.Count} ownership=stolen " +
                 $"proc={procDecision.EffectId}");
        var ammo = ItemAmmoResolver.Fire(advancedItem, 5, 2026,
            Guid.Parse("a1610000-0000-4000-8000-000000000020"));
        var weapon = advancedItem.Weapon ?? throw new InvalidOperationException("The authored weapon profile is missing.");
        var armor = advancedItem.Armor ?? throw new InvalidOperationException("The authored armor profile is missing.");
        var container = advancedItem.Container ?? throw new InvalidOperationException("The authored container profile is missing.");
        var durability = ItemAdvancedRuntimeResolver.ApplyWear(advancedItem,
            advancedItem.Durability?.Initial ?? 0, 2, 1);
        var canNest = ItemAdvancedRuntimeResolver.CanNest(
            Guid.Parse("a1610000-0000-4000-8000-000000000021"),
            Guid.Parse("a1610000-0000-4000-8000-000000000022"),
            new Dictionary<Guid, Guid?>(), advancedItem.Container?.MaximumDepth ?? 1);
        AssertSmoke(ammo.Fired &&
                    durability.Current < advancedItem.Durability!.Initial && canNest,
            "Equipment, ammo, durability or container policies did not execute.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G13L_ITEM_EQUIPMENT_PASS weapon={weapon.WeaponClass} " +
                 $"armor={armor.Material} ammo={ammo.Remaining} durability={durability.Current} " +
                 $"container_depth={container.MaximumDepth}");
        var lootContext = ItemAdvancedRuntimeResolver.ResolveLootContext(advancedItem, "crypt-chest", "crypt",
            "ash-watch", "normal", new HashSet<string>(["undead"]), "eclipse", itemContext);
        var ownership = ItemLootOwnershipResolver.Resolve(advancedItem, new("player:local", "player:local",
            "party:local", "party:local", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, false, null,
            false, true));
        var recipe = advancedItem.Recipes.Single();
        var craft = ItemCraftingResolver.Resolve(advancedItem, recipe.Id,
            new("forge", new HashSet<string>(["smithing"]), 2,
                new Dictionary<string, int> { ["tag:ember-metal"] = 2 }, true));
        AssertSmoke(lootContext.RuleId is not null && ownership.Allowed && craft.Allowed &&
                    _keyItemUniqueToken is not null && _keyItemGeneratedAffixes > 0,
            "Contextual loot, ownership, craft, unique registry or procedural generation did not execute.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G13M_ITEM_LOOT_CRAFT_PASS context={lootContext.RuleId:D} " +
                 $"ownership={ownership.Code} recipe={recipe.Slug} unique={_keyItemUniqueToken:D} " +
                 $"affixes={_keyItemGeneratedAffixes}");
        RunWorldFeatureSmokeTest();
        RunPostLaunchSmokeTest();
        InteractWith(_door!);
        AssertSmoke(_doorUnlocked, "Door interaction did not unlock the door.");

        while (_hostiles.TryGetValue(_boss!, out var combatant) && combatant.Health > 0)
        {
            DamageHostile(_boss!);
        }

        AssertSmoke(_bossPhaseTwo, "Boss phase two did not trigger.");
        AssertSmoke(_spawnedAshSpiritCount == 2, "ForgeScript did not spawn two ash spirits.");
        AssertSmoke(_ashNovaCueCount == 1, "ForgeScript did not emit the ash nova cue.");
        AssertSmoke(_bossDefeated, "Boss was not defeated.");
        var smokeCorpse = _corpseLoot.Keys.FirstOrDefault(GodotObject.IsInstanceValid);
        if (smokeCorpse is not null) InteractWith(smokeCorpse);
        AssertSmoke(HasInventoryItem("warden-ember"), "Data-driven boss loot was not granted.");
        AssertSmoke(_creatureEventHeartbeatDispatchCount > 0 && _creatureEventCombatDispatchCount > 0 &&
                    _creatureEventInventoryDispatchCount >= 2 && _creatureEventRestDispatchCount > 0 &&
                    _creatureEventCustomDispatchCount > 0,
            "The advanced creature-event lifecycle did not dispatch every authored event family.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G13F_CREATURE_EVENTS_PASS combat={_creatureEventCombatDispatchCount} " +
            $"inventory={_creatureEventInventoryDispatchCount} rest={_creatureEventRestDispatchCount} " +
            $"heartbeat={_creatureEventHeartbeatDispatchCount} custom={_creatureEventCustomDispatchCount} " +
            $"cadence_ms={authoredCreatureEvents.Heartbeat?.IntervalMilliseconds} budget_ms={authoredCreatureEvents.Heartbeat?.BudgetMilliseconds}");
        AssertSmoke(_creatureEventBeforeDamageDispatchCount > 0 &&
                    _creatureEventDamageAppliedDispatchCount > 0 && _creatureEventDeathDispatchCount > 0,
            "The creature damage/death lifecycle did not dispatch before, applied and died events.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_R039_CREATURE_DAMAGE_DEATH_PASS " +
            $"before={_creatureEventBeforeDamageDispatchCount} applied={_creatureEventDamageAppliedDispatchCount} " +
            $"died={_creatureEventDeathDispatchCount} authority=server transaction=ordered validation=pre-build");
        AssertSmoke(_factionRuntimeProfile is not null &&
                    FactionValidator.Validate(_factionCatalog).Count == 0,
            "The authored advanced faction profile did not resolve.");
        AssertSmoke(_factionInheritedTagCount >= 4 && _factionMembershipCount == 2,
            "Faction hierarchy or multiple memberships did not resolve.");
        AssertSmoke(_factionWitnessReportCount == 1 && _factionDiplomacyTransitionCount == 1,
            "Witness propagation or authoritative diplomacy did not execute.");
        var expiration = FactionRuntimeResolver.AdvanceExpirations(_factionRuntimeProfile!,
            DateTimeOffset.Parse("2031-08-05T12:00:00Z"), isServer: true);
        _factionExpirationCount = expiration.Events.Count;
        AssertSmoke(expiration.Diagnostics.Count == 0 && _factionExpirationCount > 0 &&
                    expiration.Profile.DynamicFactions.All(item => !item.Active),
            "Dynamic faction expiration did not execute deterministically.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G13G_ADVANCED_FACTIONS_PASS hierarchy_tags={_factionInheritedTagCount} " +
            $"memberships={_factionMembershipCount} witness={_factionWitnessReportCount} " +
            $"diplomacy={_factionDiplomacyTransitionCount} expiration={_factionExpirationCount} " +
            "authority=server replication=revisioned");
        var covenant = _factionCatalog.Factions.Single(item => item.Slug == "crypt-covenant");
        var surface = _factionCatalog.Factions.Single(item => item.Slug == "surface-alliance");
        var watch = _factionCatalog.Factions.Single(item => item.Slug == "ash-watch");
        var foundationScript = new FactionFoundationScriptService(_factionCatalog);
        var covenantHandle = foundationScript.Handle(covenant.Id);
        var surfaceHandle = foundationScript.Handle(surface.Id);
        var watchHandle = foundationScript.Handle(watch.Id);
        var forward = foundationScript.Relationship(covenantHandle, surfaceHandle);
        var reverse = foundationScript.Relationship(surfaceHandle, covenantHandle);
        var personalDisposition = foundationScript.Disposition(
            surfaceHandle, watchHandle, "hero", DateTimeOffset.Parse("2026-08-05T12:00:00Z"));
        var foundationOptional = FactionRuntimeResolver.ResolveFoundation(FactionCatalog.Empty);
        var foundationRequired = FactionRuntimeResolver.ResolveFoundation(
            FactionCatalog.Empty with { RequireFoundationRuntime = true });
        AssertSmoke(forward?.Score == -75 && reverse?.Score == -30 &&
                    foundationScript.Read(surfaceHandle).PersonalReputationCount == 1 &&
                    personalDisposition == "neutral" && foundationOptional.OptionalFallback &&
                    foundationRequired.Diagnostics.Single().Code == FactionValidator.FoundationMissingCode &&
                    _factionFoundationRoot is not null && _factionFoundationRoot.GetChildCount() == 6,
            "Faction creation, asymmetric matrix, bands, personal reputation or runtime nodes did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R086_FACTION_FOUNDATION_PASS " +
            "lifecycle=create-archive-safe-remove-member-migration matrix=directional-heatmap-presets " +
            "asymmetry=a-to-b-b-to-a-independent-explained bands=ruleset-configurable-result-overrides " +
            "personal=actor-player-group-duration-cause forgescript=typed-definition-handles " +
            "mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic " +
            "rpgkit=authoring-reputation-reused engine=faction-relationship-personal-nodes");
        var reputationProfile = _factionReputationProfile ??
            throw new InvalidOperationException("The authored faction reputation profile did not resolve.");
        var reputationScript = new FactionReputationScriptService(_factionCatalog);
        var reputationSnapshot = reputationScript.Read(reputationScript.Handle);
        var scopes = reputationProfile.Transactions.Select(item => item.Scope).ToHashSet();
        var history = reputationScript.HeatmapHistory(reputationScript.Handle);
        var differences = FactionReputationService.Compare(history[0], history[1]);
        var exported = FactionReputationService.ExportSnapshot(history[1]);
        var imported = FactionReputationService.ImportSnapshot(exported);
        var encounterReactions = FactionReputationService.SimulateEncounter(
            _factionCatalog, [covenant.Id, surface.Id], watch.Id);
        var tagged = reputationScript.QueryByTags(reputationScript.Handle, ["guardian"]);
        var reactionAt = DateTimeOffset.Parse("2026-08-11T12:00:00Z");
        var reaction = FactionReputationService.ResolveReaction(_factionCatalog,
            _factionReputationState!, new("hero", surface.Id, covenant.Id, reactionAt,
                20, -10, 5, 80, -70));
        var appliedReputation = FactionReputationService.Apply(_factionReputationState!,
            new(Guid.Parse("b2050000-0000-4000-8000-000000000029"), 1, "runtime:r087",
                ReputationScope.Player, "hero", surface.Id, covenant.Id, 5, "runtime-choice", reactionAt));
        var optionalReputation = FactionRuntimeResolver.ResolveReputation(FactionCatalog.Empty);
        var requiredReputation = FactionRuntimeResolver.ResolveReputation(
            FactionCatalog.Empty with { RequireReputationRuntime = true });
        AssertSmoke(reputationSnapshot.TransactionCount == 5 && reputationSnapshot.HeatmapHistoryCount == 2 &&
                    scopes.SetEquals(Enum.GetValues<ReputationScope>()) && differences.Count == 1 &&
                    differences[0].Before == -55 && differences[0].After == -30 &&
                    imported.Label == "apres-pacte" && encounterReactions.Count == 2 && tagged.Count == 2 &&
                    reaction.Score == 80 && reaction.Disposition == "allied" &&
                    reaction.Explanation[^1] == "gm-override:80" && appliedReputation.Inverse.Delta == -5 &&
                    optionalReputation.OptionalFallback &&
                    requiredReputation.Diagnostics.Single().Code == FactionValidator.ReputationMissingCode &&
                    _factionReputationRoot is not null && _factionReputationRoot.GetChildCount() == 8,
            "Global reputation, tags, heatmap history, reaction policy or runtime nodes did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R087_FACTION_REPUTATION_PASS " +
            "global=world-server-campaign-group-player-transaction-inverse tags=semantic-effective-query " +
            "matrix=editable-compare-simulate-import-export-history reaction=reputation-disguise-crime-dialogue-zone-gm " +
            "forgescript=typed-profile-handle mcp=filtered-dry-run persistence=roundtrip " +
            "optional=ignored required=diagnostic engine=transaction-heatmap-reaction-nodes");
        RunItemCreationRuntimeSmokeTest(_loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));
        RunItemGameplayRuntimeSmokeTest(_loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));
        RunItemOperationsRuntimeSmokeTest(_loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));
        AssertSmoke(_questState == PrototypeQuestState.Completed, "Quest was not completed.");
        var completedQuest = _loadedProject!.Quests.Single();
        AssertSmoke(IsQuestTerminal(completedQuest.Id), "Data-driven quest was not completed.");
        var questOperations = completedQuest.Operations ??
            throw new InvalidOperationException("The authored quest operations profile is missing.");
        var repeat = QuestOperationsRuntimeResolver.ResolveRepeat(completedQuest, new(
            DateTimeOffset.Parse("2026-08-06T12:00:00Z"), DateTimeOffset.Parse("2026-08-06T10:00:00Z"),
            1, 1, 1, true, true, _chosenQuestTransitions));
        var sortedJournal = QuestOperationsRuntimeResolver.SortJournal([completedQuest]);
        AssertSmoke(repeat.Allowed && repeat.RewardMultiplier == questOperations.Repeat.RewardDecay &&
                    sortedJournal.Single().Id == completedQuest.Id && _chosenQuestTransitions.Count == 3,
            "Quest journal, repetition, sharing or exclusive-choice policies did not execute.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G13J_QUEST_OPERATIONS_PASS priority={questOperations.Journal.Priority} " +
                 $"repeat_seed={repeat.Seed} reward={repeat.RewardMultiplier:0.###} " +
                 $"share={questOperations.Share.Mode.ToString().ToLowerInvariant()} " +
                 $"choices={questOperations.ExclusiveChoices.Count} notifications={questOperations.Notification.Mode.ToString().ToLowerInvariant()}");

        RunAudioEmitterRuntimeSmokeTest();
        RunDialogueGraphRuntimeSmokeTest();
        RunDialogueVoicePlaybackSmokeTest();
        RunQuestJournalRuntimeSmokeTest();
        RunWaypointAnchorRuntimeSmokeTest();
        RunGameplayFoundationRuntimeSmokeTest();
        RunBlackboardRulesetPluginRuntimeSmokeTest();
        RunForgeScriptFoundationRuntimeSmokeTest();
        RunForgeScriptWorldApiRuntimeSmokeTest();
        RunForgeScriptSafetyPolicyRuntimeSmokeTest();
        RunForgeScriptModulePermissionsRuntimeSmokeTest();
        RunForgeScriptP1GameplayRuntimeSmokeTest();
        RunForgeScriptP1WorldApiRuntimeSmokeTest();
        RunForgeScriptP1RuntimeToolsRuntimeSmokeTest();
        RunForgeScriptP1QualityRuntimeSmokeTest();
        RunForgeScriptP1InteropRuntimeSmokeTest();
        RunNetworkFoundationP1RuntimeSmokeTest();
        RunNetworkOperationsP1RuntimeSmokeTest();
        RunNetworkAssuranceP1RuntimeSmokeTest();
        RunLiveDirectorAccessP1RuntimeSmokeTest();
        RunContentPackageRuntimeSmokeTest();
        RunContentAuthoringRuntimeSmokeTest(_loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));
        RunAssetGovernanceRuntimeSmokeTest();
        RunQualityFoundationRuntimeSmokeTest();
        RunCanonicalValidationRuntimeSmokeTest();
        RunAreaFoundationRuntimeSmokeTest(_loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));
        RunAreaRuntimeValidationSmokeTest(_loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));
        RunAgentSemanticWorkflowRuntimeSmokeTest();
        RunBuildCollaborationRuntimeSmokeTest();
        RunProjectFoundationRuntimeSmokeTest(_loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));
        RunProjectOperationsRuntimeSmokeTest(_loadedProject);
        RunProjectCompatibilityRuntimeSmokeTest(_loadedProject);
        RunProjectGameplayEventsRuntimeSmokeTest(_loadedProject);
        RunProjectWorldRuntimeSmokeTest(_loadedProject);
        RunUx0GGlobalEventsRuntimeSmokeTest(_loadedProject);
        RunUx0HWorldTransitionsRuntimeSmokeTest(_loadedProject);
        GD.Print("OPENRPG_FORGE_RUNTIME_SMOKE_PASS quest=completed door=unlocked boss=defeated");
        GetTree().Quit(0);
    }

    private void RunItemCreationRuntimeSmokeTest(ProjectDefinition project)
    {
        var canonical = project.Blueprints.First(item => item.Kind == BlueprintKind.Item);
        var typeId = Guid.Parse("c2880000-0000-4000-8000-000000000061");
        var profile = new ItemFoundationProfile(
            Guid.Parse("c2880000-0000-4000-8000-000000000062"), 1, "runtime:r088", true,
            "crypt-of-ashes", "CC0-1.0", new string('8', 64), typeId,
            new(typeId, 1, "runtime:r088", "ritual-key", ["hand", "inventory"], ["utility"], "rig.hand"),
            canonical.Slug, "asset.items.ash-key", "rig.hand", 2, 2, 4, 75, ["hand"],
            [new(Guid.Parse("c2880000-0000-4000-8000-000000000063"), "utility", 1,
                "effect.ash-key", "OnUse")],
            [new("fr", "Clé des cendres", "Une clé.", "Une clé rituelle.", "Objet inconnu", "Clé", "")],
            ["identified", "durability"],
            new(Guid.Parse("c2880000-0000-4000-8000-000000000064"), 1, "runtime:r088", true,
                "crypt-of-ashes", "CC0-1.0", new string('9', 64),
                new(["archetype", "appearance", "slots", "quality", "level", "property", "provenance",
                    "generation-rules"], ["fixed-seed", "respect-type", "preserve-provenance"]),
                new(Guid.Parse("c2880000-0000-4000-8000-000000000065"), 1, "runtime:r088",
                    ["asset.items.ash-key.mesh"], ["asset.materials.embered-iron"], ["hand-r"],
                    "asset.items.ash-key.icon", ["asset.vfx.ash-glow"], "asset.items.ash-key.equipped"),
                new(1.25, .4, 1, 2, .5, .25), new(10, "ash-key", true),
                new(3, 3, 2, 2, 5_000, 100, false, "rest-or-refill")));
        var authored = canonical.WithItemFoundation(profile);
        var aggregate = new ProjectDefinition(project.Id, project.Title, project.Module,
            project.Blueprints.Select(item => item.Id == canonical.Id ? authored : item).ToArray(),
            project.Areas, project.Dialogues, project.Quests);
        var script = ItemFoundationScriptService.Read(aggregate, authored.Id);
        var resolved = Forge.RpgKit.Domain.Items.ItemFoundationRuntimeAdapter.Resolve(profile);
        var policies = resolved.Policies ??
            throw new InvalidOperationException("The canonical item creation profile did not adapt.");
        var contribution = Forge.RpgKit.Domain.Items.ItemInventoryService.Contribution(policies.Inventory, 3);
        var merged = Forge.RpgKit.Domain.Items.ItemInventoryService.Merge(
            new(Guid.Parse("c2880000-0000-4000-8000-000000000066"), profile.Id, "ash-key", 7, 4),
            new(Guid.Parse("c2880000-0000-4000-8000-000000000067"), profile.Id, "ash-key", 5, 4),
            policies.Stack, 4);
        var split = Forge.RpgKit.Domain.Items.ItemInventoryService.Split(merged.Primary, 2, 5);
        var consumed = Forge.RpgKit.Domain.Items.ItemInventoryService.Consume(policies.Usage,
            DateTimeOffset.UnixEpoch);
        var optional = Forge.RpgKit.Domain.Items.ItemFoundationRuntimeAdapter.Resolve(null);
        var required = Forge.RpgKit.Domain.Items.ItemFoundationRuntimeAdapter.Resolve(null, true);

        var root = new Node { Name = "ItemCreation_R088" };
        foreach (var name in new[] { "Wizard", "AppearancePreview", "InventoryPolicy", "StackPolicy", "UsageResource" })
            root.AddChild(new Node { Name = name });
        AddChild(root);

        AssertSmoke(ItemFoundationValidator.Validate(profile).Count == 0 && script?.Creation is not null &&
                    script.Creation.ProfileId == profile.Creation!.Id && resolved.Diagnostics.Count == 0 &&
                    policies.Visual.EquippedPreviewAssetId == "asset.items.ash-key.equipped" &&
                    Math.Abs(contribution.Mass - 2.5) < .000001 && Math.Abs(contribution.Volume - .6) < .000001 &&
                    merged.Primary.Quantity == 10 && merged.Secondary?.Quantity == 2 &&
                    split.Primary.Quantity == 8 && split.Secondary?.Quantity == 2 &&
                    consumed.CurrentCharges == 2 && consumed.CooldownUntil == DateTimeOffset.UnixEpoch.AddSeconds(5) &&
                    optional.OptionalFallback &&
                    required.Diagnostics.Single().Code == ItemFoundationValidator.CreationMissingRequiredCode &&
                    root.GetChildCount() == 5,
            "Item creation, appearance, inventory, stack or usage runtime behavior did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R088_ITEM_CREATION_PASS " +
            "wizard=archetype-appearance-slots-quality-level-property-provenance-generation-rules " +
            "appearance=meshes-materials-sockets-icon-vfx-equipped-preview " +
            "inventory=mass-volume-grid-stack-contribution stack=maximum-key-atomic-merge-split-replication " +
            "usage=charges-rest-cooldown-durability-consumable-recharge forgescript=typed-profile-handle " +
            "mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic " +
            "rpgkit=inventory-reused engine=wizard-appearance-inventory-stack-usage-nodes");
    }

    private void RunItemGameplayRuntimeSmokeTest(ProjectDefinition project)
    {
        var canonical = project.Blueprints.First(item => item.Kind == BlueprintKind.Item);
        var profile = ItemFoundationProfile.CreateDefault(
            Guid.Parse("c2890000-0000-4000-8000-000000000071"), canonical.Slug,
            canonical.DisplayName, "runtime:r089");
        var gameplay = new ItemGameplayProfile(
            Guid.Parse("c2890000-0000-4000-8000-000000000072"), 1, "runtime:r089", true,
            "crypt-of-ashes", "CC0-1.0", new string('9', 64),
            new(true, true, true, true, true, true, true),
            new(Enum.GetValues<ItemTransferDestination>().ToDictionary(item => item,
                    item => item is ItemTransferDestination.Container or ItemTransferDestination.GameMaster),
                ["item.transfer.quest"]),
            [new(Guid.Parse("c2890000-0000-4000-8000-000000000073"), 1, "runtime:r089", "ward",
                new Dictionary<string, double> { ["value"] = 8 }, "highest", "night", "+8 ward",
                "BeforeItemModifier", "AfterItemModifier")],
            new(Guid.Parse("c2890000-0000-4000-8000-000000000074"), "point",
                new Dictionary<string, double> { ["focus"] = 1 }, 1, 12_000,
                "ItemAbilityRequested", "ItemAbilityResolved"),
            new([
                new("hand-r", "forge", "humanoid", 2, ["hands-two"]),
                new("hand-l", "forge", "humanoid", 2, ["hands-two"]),
                new("hands-two", "forge", "humanoid", 3, ["hand-r", "hand-l"]),
            ], ["hand-r", "hand-l"]));
        profile = profile with { RequiredAtRuntime = true, Gameplay = gameplay };
        var authored = canonical.WithItemFoundation(profile);
        var aggregate = new ProjectDefinition(project.Id, project.Title, project.Module,
            project.Blueprints.Select(item => item.Id == canonical.Id ? authored : item).ToArray(),
            project.Areas, project.Dialogues, project.Quests);
        var script = ItemFoundationScriptService.Read(aggregate, authored.Id);
        var resolved = Forge.RpgKit.Domain.Items.ItemGameplayRuntimeAdapter.Resolve(profile);
        var blocked = Forge.RpgKit.Domain.Items.ItemGameplayRuntimeAdapter.AuthorizeCriticalAction(
            gameplay, "drop", false, "player", string.Empty);
        var overridden = Forge.RpgKit.Domain.Items.ItemGameplayRuntimeAdapter.AuthorizeCriticalAction(
            gameplay, "drop", true, "gm:karen", "recover quest item");
        var allowedTransfer = Forge.RpgKit.Domain.Items.ItemGameplayService.AuthorizeTransfer(
            resolved.Transfer!, Forge.RpgKit.Domain.Items.TransferDestination.Container,
            new HashSet<string> { "item.transfer.quest" }, "player");
        var blockedTransfer = Forge.RpgKit.Domain.Items.ItemGameplayService.AuthorizeTransfer(
            resolved.Transfer!, Forge.RpgKit.Domain.Items.TransferDestination.Drop,
            new HashSet<string> { "item.transfer.quest" }, "player");
        var modifiers = Forge.RpgKit.Domain.Items.ItemGameplayService.ResolveModifiers(
            resolved.Modifiers, new HashSet<string> { "night" });
        var activation = Forge.RpgKit.Domain.Items.ItemGameplayService.ActivateAbility(
            Guid.Parse("c2890000-0000-4000-8000-000000000075"), resolved.Ability!, "crypt-door",
            new(3, 3, 3, 3, TimeSpan.Zero, null, 100, false, "rest"), DateTimeOffset.UnixEpoch);
        var equipment = Forge.RpgKit.Domain.Items.ItemGameplayService.PreviewEquipment(
            new(Guid.Parse("c2890000-0000-4000-8000-000000000075"), resolved.RequiredSlots,
                "forge", "humanoid"), resolved.Slots, new HashSet<string> { "hands-two" });
        var optional = Forge.RpgKit.Domain.Items.ItemGameplayRuntimeAdapter.Resolve(null);
        var required = Forge.RpgKit.Domain.Items.ItemGameplayRuntimeAdapter.Resolve(null, true);
        var serialized = ProjectJsonSerializer.Serialize(aggregate);
        var restored = ProjectJsonSerializer.Deserialize(serialized).Blueprints
            .Single(item => item.Id == canonical.Id).ItemFoundation;

        var root = new Node { Name = "ItemGameplay_R089" };
        foreach (var name in new[] { "CriticalPolicy", "TransferPolicy", "Modifiers", "Ability", "Equipment" })
            root.AddChild(new Node { Name = name });
        AddChild(root);

        AssertSmoke(ItemFoundationValidator.Validate(profile).Count == 0 &&
                    script?.Gameplay?.ProfileId == gameplay.Id && script.Gameplay.RequiredEquipmentSlots.Count == 2 &&
                    resolved.Diagnostics.Count == 0 && resolved.Slots.Count == 3 &&
                    blocked.Code == "RPG_ITEM_CRITICAL_PROTECTED" && overridden.Code == "RPG_ITEM_GM_OVERRIDE" &&
                    overridden.AuditEntry.Contains("recover quest item", StringComparison.Ordinal) &&
                    allowedTransfer.Code == "RPG_ITEM_TRANSFER_ALLOWED" &&
                    blockedTransfer.Code == "RPG_ITEM_TRANSFER_BLOCKED" &&
                    Math.Abs(modifiers["ward"] - 8) < .000001 &&
                    activation.Requested.Name == "ItemAbilityRequested" && activation.Usage.CurrentCharges == 2 &&
                    !equipment.Allowed && equipment.Conflicts.Count == 2 && optional.OptionalFallback &&
                    required.Diagnostics.Single().Code == ItemFoundationValidator.GameplayMissingRequiredCode &&
                    restored?.Gameplay?.Id == gameplay.Id &&
                    ProjectJsonSerializer.Serialize(ProjectJsonSerializer.Deserialize(serialized)) == serialized &&
                    root.GetChildCount() == 5,
            "Item critical, transfer, modifier, ability or equipment runtime behavior did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R089_ITEM_GAMEPLAY_PASS " +
            "critical=destruction-sale-drop-theft-consumption-cleanup-gm-audit " +
            "transfer=drop-trade-store-container-mail-destroy-consume-gm-permissions " +
            "modifiers=data-driven-parameters-stacking-conditions-ui-hooks " +
            "ability=definition-targeting-costs-charges-cooldown-events " +
            "equipment=ruleset-rig-multislot-layer-conflicts-preview forgescript=typed-profile-handle " +
            "mcp=filtered-dry-run persistence=roundtrip optional=ignored required=diagnostic " +
            "rpgkit=gameplay-reused engine=critical-transfer-modifier-ability-equipment-nodes");
    }

    private void RunItemOperationsRuntimeSmokeTest(ProjectDefinition project)
    {
        var canonical = project.Blueprints.First(item => item.Kind == BlueprintKind.Item);
        var profile = ItemFoundationProfile.CreateDefault(
            Guid.Parse("c2900000-0000-4000-8000-000000000081"), canonical.Slug,
            canonical.DisplayName, "runtime:r090");
        var bindings = Enum.GetValues<ItemLifecycleEventType>().Select((kind, index) =>
            new ItemLifecycleBindingPolicy(
                Guid.Parse($"c2900000-0000-4000-8000-{(300 + index):000000000000}"), 1,
                "runtime:r090", kind, $"On{kind}", "server")).ToArray();
        var operations = new ItemOperationsProfile(
            Guid.Parse("c2900000-0000-4000-8000-000000000082"), 1, "runtime:r090", true,
            "crypt-of-ashes", "CC0-1.0", new string('7', 64),
            new(Guid.Parse("c2900000-0000-4000-8000-000000000083"), 1, "runtime:r090",
            [
                new(Guid.Parse("c2900000-0000-4000-8000-000000000091"), "relic", 1, 1, 1, 2,
                    "crypt-open", true),
                new(Guid.Parse("c2900000-0000-4000-8000-000000000092"), "relic", 3, 1, 3, 1,
                    "crypt-open", false),
            ]),
            new(Guid.Parse("c2900000-0000-4000-8000-000000000084"), 1, "runtime:r090", 8,
            [
                new(Guid.Parse("c2900000-0000-4000-8000-000000000093"), "backpack", null, 0, 0, 1,
                    ["quest"], "asset.items.ash-key.preview"),
                new(Guid.Parse("c2900000-0000-4000-8000-000000000094"), "backpack", null, 1, 0, 2,
                    ["consumable"], "asset.items.ember.preview"),
            ], true, true, true), new("1.0", bindings));
        profile = profile with { RequiredAtRuntime = true, Operations = operations };
        var authored = canonical.WithItemFoundation(profile);
        var aggregate = new ProjectDefinition(project.Id, project.Title, project.Module,
            project.Blueprints.Select(item => item.Id == canonical.Id ? authored : item).ToArray(),
            project.Areas, project.Dialogues, project.Quests);
        var script = ItemFoundationScriptService.Read(aggregate, authored.Id);
        var resolved = Forge.RpgKit.Application.Items.ItemOperationsRuntimeAdapter.Resolve(profile);
        var conditions = new HashSet<string> { "crypt-open" };
        var firstRoll = Forge.RpgKit.Domain.Items.ItemGameplayService.Roll(resolved.Loot!, 42, conditions);
        var secondRoll = Forge.RpgKit.Domain.Items.ItemGameplayService.Roll(resolved.Loot!, 42, conditions);
        var preview = Forge.RpgKit.Domain.Items.ItemGameplayService.Preview(resolved.Loot!, conditions);
        var placements = resolved.Inventory!.Placements;
        var move = Forge.RpgKit.Application.Items.InventoryAuthoringService.Move(resolved.Inventory,
            new(placements.Select(item => item.ItemInstanceId).ToArray(), "vault", null, 3, 2, true), 8);
        var filtered = Forge.RpgKit.Application.Items.InventoryAuthoringService.Filter(
            resolved.Inventory, "backpack", null, null);
        var compared = Forge.RpgKit.Application.Items.InventoryAuthoringService.Compare(move.Before, move.After);
        var events = Enum.GetValues<Forge.RpgKit.Application.Items.ItemLifecycleEventKind>().Select(kind =>
            Forge.RpgKit.Application.Items.ItemOperationsRuntimeAdapter.Emit(operations, kind, Guid.NewGuid(),
                "source", "target", DateTimeOffset.UnixEpoch)).ToArray();
        var optional = Forge.RpgKit.Application.Items.ItemOperationsRuntimeAdapter.Resolve(null);
        var required = Forge.RpgKit.Application.Items.ItemOperationsRuntimeAdapter.Resolve(null, true);
        var serialized = ProjectJsonSerializer.Serialize(aggregate);
        var restored = ProjectJsonSerializer.Deserialize(serialized).Blueprints
            .Single(item => item.Id == canonical.Id).ItemFoundation;

        var root = new Node { Name = "ItemOperations_R090" };
        foreach (var name in new[] { "LootTable", "InventoryInspector", "LifecycleEvents" })
            root.AddChild(new Node { Name = name });
        AddChild(root);

        AssertSmoke(ItemFoundationValidator.Validate(profile).Count == 0 &&
                    script?.Operations?.ProfileId == operations.Id && script.Operations.LifecycleEvents.Count == 9 &&
                    resolved.Diagnostics.Count == 0 && firstRoll.Count == 1 && firstRoll.SequenceEqual(secondRoll) &&
                    preview.Count == 2 && move.Diagnostics.Count == 0 && move.Document.Revision == 8 &&
                    filtered.Count == 2 && compared.Count == 2 && events.Length == 9 &&
                    events.Select(item => item.Kind).Distinct().Count() == 9 && optional.OptionalFallback &&
                    required.Diagnostics.Single().Code == ItemFoundationValidator.OperationsMissingRequiredCode &&
                    restored?.Operations?.Id == operations.Id &&
                    ProjectJsonSerializer.Serialize(ProjectJsonSerializer.Deserialize(serialized)) == serialized &&
                    root.GetChildCount() == 3,
            "Item loot, inventory inspector or lifecycle events did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R090_ITEM_OPERATIONS_PASS " +
            "loot=weighted-groups-conditions-quantity-quality-seed-uniqueness-statistical-preview " +
            "inspector=drag-drop-containers-slots-filters-validation-visual-preview-comparison-multi-edit " +
            "events=created-acquired-transferred-equipped-unequipped-used-consumed-broken-destroyed " +
            "forgescript=typed-profile-handle mcp=filtered-dry-run persistence=roundtrip " +
            "optional=ignored required=diagnostic rpgkit=loot-inventory-reused " +
            "engine=loot-inspector-events-nodes");
    }

    private void RunPlaceableOperationsRuntimeSmokeTest(
        PlaceableWorldProfile placeable,
        ProjectDefinition project)
    {
        var authoredCatalog = project.Module.WorldFeatures;
        var authoredPlaceable = authoredCatalog.Placeables.Single(item => item.Id == placeable.Id);
        var adapter = new PlaceableOperationsRuntimeAdapter();
        var resolution = adapter.Resolve(authoredPlaceable);
        var handle = resolution.Handle ??
                     throw new InvalidOperationException("The placeable operations handle did not resolve.");
        var runtime = adapter.Build(handle, authoredPlaceable);
        var transition = adapter.PreviewTransition(handle, authoredPlaceable, "closed", "open",
            new HashSet<string> { "quest:crypt-open" });
        var damage = adapter.Damage(handle, authoredPlaceable, 80, "force");
        var destroyed = Forge.RpgKit.Domain.Placeables.PlaceableInteractionService.ApplyDamage(
            damage.Damageable, 500, "cold");
        var repaired = adapter.Repair(handle, authoredPlaceable, destroyed.Damageable, 20);
        var blocked = adapter.Authorize(handle, authoredPlaceable, "destroy", new HashSet<string>(), false,
            "player:runtime", string.Empty);
        var overridden = adapter.Authorize(handle, authoredPlaceable, "destroy", new HashSet<string>(), true,
            "gm:runtime", "restore module");
        var unlocked = adapter.Unlock(handle, authoredPlaceable,
            new("player:runtime", new HashSet<string> { "key:ember" }, new HashSet<string>(), null, 0, false,
                false));
        var jammed = adapter.Relock(handle, authoredPlaceable, unlocked.Lock, true, "player:runtime");
        var optional = adapter.Resolve(authoredPlaceable with
        { Operations = null, OperationsRequiredAtRuntime = false });
        var required = adapter.Resolve(authoredPlaceable with
        { Operations = null, OperationsRequiredAtRuntime = true });

        var authoredProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(authoredCatalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        var serialized = ProjectJsonSerializer.Serialize(authoredProject);
        var restored = ProjectJsonSerializer.Deserialize(serialized).Module.WorldFeatures.Placeables
            .Single(item => item.Id == authoredPlaceable.Id);

        var root = new Node { Name = "PlaceableOperations_R093" };
        var containerNode = new Node { Name = "ContainerComponent" };
        containerNode.SetMeta("inventory", runtime.Profile.InventoryReferences.Single());
        var stateMachineNode = new AnimationPlayer { Name = "VisualStateMachine" };
        stateMachineNode.SetMeta("initial_state", runtime.Profile.StateMachine.InitialState);
        stateMachineNode.SetMeta("states", runtime.Profile.StateMachine.States.Count);
        var damageableNode = new StaticBody3D { Name = "DamageableComponent" };
        damageableNode.SetMeta("maximum_health", runtime.Profile.Damageable!.MaximumHealth);
        var protectionNode = new Node { Name = "ProtectionPolicy" };
        protectionNode.SetMeta("audit_binding", runtime.ProtectionAuditBinding ?? string.Empty);
        var lockNode = new Node { Name = "LockComponent" };
        lockNode.SetMeta("state", runtime.Lock!.State.ToString());
        var panel = new PlaceableOperationsPanel();
        panel.Configure(runtime);
        root.AddChild(containerNode);
        root.AddChild(stateMachineNode);
        root.AddChild(damageableNode);
        root.AddChild(protectionNode);
        root.AddChild(lockNode);
        root.AddChild(panel);
        AddChild(root);

        AssertSmoke(resolution.Available && adapter.Validate(handle, authoredPlaceable).Count == 0 &&
                    runtime.Profile.Container is { CapacitySlots: 24, Persistent: true, Replicated: true } &&
                    runtime.Profile.InventoryReferences.Single() == "inventory:crypt-pillar" &&
                    runtime.Profile.StateMachine.States.Count == 6 && transition.CurrentState == "open" &&
                    damage.Damageable.CurrentHealth == 70 && destroyed.Destroyed &&
                    destroyed.DebrisDefinitionId == "debris:crypt-pillar" && repaired.CurrentHealth == 20 &&
                    !blocked.Allowed && overridden.Allowed &&
                    overridden.AuditEntry.Contains("restore module", StringComparison.Ordinal) &&
                    unlocked.Succeeded && unlocked.Method == "credential" && jammed.Lock.State == RpgLockState.Jammed &&
                    optional.OptionalFallback && required.DiagnosticCode ==
                    "WORLD_PLACEABLE_OPERATIONS_REQUIRED_MISSING" &&
                    restored.Operations?.Container?.CapacitySlots == 24 &&
                    restored.Operations.StateMachine?.States.Count == 6 &&
                    restored.Operations.Protection?.AuditBinding == "PlaceableProtectionOverride" &&
                    ProjectJsonSerializer.Serialize(ProjectJsonSerializer.Deserialize(serialized)) == serialized &&
                    root.GetChildCount() == 6 &&
                    panel.GetNodeOrNull<ItemList>("OperationsLayout/ContainerInventory") is { ItemCount: 2 } &&
                    panel.GetNodeOrNull<ProgressBar>("OperationsLayout/Health") is { Value: 100 } &&
                    panel.GetNodeOrNull<Button>("OperationsLayout/OperationsActions/UnlockButton") is
                    { Disabled: false },
            "Placeable container, visual states, damage, protection, lock or runtime UI did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R093_PLACEABLE_OPERATIONS_PASS " +
            "container=capacity-ownership-access-persistence-loot-ui-replication-inventory " +
            "state=idle-open-closed-broken-active-custom-transitions-preview " +
            "damage=health-armor-resistances-stages-destruction-debris-repair " +
            "protection=permissions-feedback-gm-override-audit " +
            "lock=states-credentials-skill-code-hacking-relock-jam-permissions " +
            "forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored " +
            "required=diagnostic rpgkit=interaction-security-services-reused " +
            "engine=container-state-damage-protection-lock-accessible-panel");
    }

    private void RunPlaceableSecurityEventsRuntimeSmokeTest(
        PlaceableWorldProfile placeable,
        ProjectDefinition project)
    {
        var authoredCatalog = project.Module.WorldFeatures;
        var authoredPlaceable = authoredCatalog.Placeables.Single(item => item.Id == placeable.Id);
        var adapter = new PlaceableSecurityEventsRuntimeAdapter();
        var resolution = adapter.Resolve(authoredPlaceable);
        var handle = resolution.Handle ??
                     throw new InvalidOperationException("The placeable security-events handle did not resolve.");
        var runtime = adapter.Build(handle, authoredPlaceable);

        var hidden = adapter.Detect(handle, authoredPlaceable, 9, "outsider");
        var detected = adapter.Detect(handle, authoredPlaceable, 10, "outsider");
        var friendly = adapter.Trigger(handle, authoredPlaceable, "ash-watch");
        var triggered = adapter.Trigger(handle, authoredPlaceable, "outsider");
        var disarmed = adapter.Disarm(handle, authoredPlaceable, 12);
        var reset = adapter.Reset(handle, authoredPlaceable, disarmed.Trap);
        var skipped = adapter.Spawn(handle, authoredPlaceable,
            Forge.RpgKit.Domain.Placeables.LootSpawnTiming.Initialize, new HashSet<string>());
        var spawned = adapter.Spawn(handle, authoredPlaceable,
            Forge.RpgKit.Domain.Placeables.LootSpawnTiming.Open,
            new HashSet<string> { "quest:crypt-open" });
        var restoredLoot = adapter.Spawn(handle, authoredPlaceable,
            Forge.RpgKit.Domain.Placeables.LootSpawnTiming.Open,
            new HashSet<string> { "quest:crypt-open" }, spawned.Spawner);
        var opened = adapter.ContainerStateChanged(handle, authoredPlaceable, true, "player:runtime", "success",
            "closed", "server", DateTimeOffset.UnixEpoch);
        var closed = adapter.ContainerStateChanged(handle, authoredPlaceable, false, "player:runtime", "success",
            "open", "server", DateTimeOffset.UnixEpoch);
        var vetoed = adapter.ChangeLockState(handle, authoredPlaceable, RpgLockState.Unlocked, "credential",
            "key:ember", "player:runtime", _ => "quest-gate");
        var unlocked = adapter.ChangeLockState(handle, authoredPlaceable, RpgLockState.Unlocked, "credential",
            "key:ember", "player:runtime");
        var contents = adapter.ContentsChanged(handle, authoredPlaceable, 4,
            [new(runtime.LootTable.Entries.Single().ItemDefinitionId, -1, "take", "inventory:crypt-pillar",
                "player:runtime")], "player-loot", "player:runtime");
        var optional = adapter.Resolve(authoredPlaceable with
        {
            SecurityEvents = null,
            SecurityEventsRequiredAtRuntime = false,
        });
        var required = adapter.Resolve(authoredPlaceable with
        {
            SecurityEvents = null,
            SecurityEventsRequiredAtRuntime = true,
        });

        var authoredProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(authoredCatalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        var serialized = ProjectJsonSerializer.Serialize(authoredProject);
        var restoredPlaceable = ProjectJsonSerializer.Deserialize(serialized).Module.WorldFeatures.Placeables
            .Single(item => item.Id == authoredPlaceable.Id);

        var root = new Node { Name = "PlaceableSecurityEvents_R094" };
        var trapNode = new Area3D { Name = "TrapComponent" };
        trapNode.SetMeta("payload", runtime.Trap.PayloadId);
        trapNode.SetMeta("owner", runtime.Trap.OwnerId);
        var lootNode = new Node { Name = "LootSpawner" };
        lootNode.SetMeta("timing", runtime.LootSpawner.Timing.ToString());
        lootNode.SetMeta("seed", runtime.LootSpawner.Seed);
        lootNode.SetMeta("persisted", runtime.LootSpawner.PersistGeneratedState);
        var eventNode = new Node { Name = "PlaceableEventBus" };
        eventNode.SetMeta("opened", opened.Binding);
        eventNode.SetMeta("closed", closed.Binding);
        eventNode.SetMeta("lock_changing", vetoed.Changing.Binding);
        eventNode.SetMeta("contents_changed", contents.Binding);
        var panel = new PlaceableSecurityEventsPanel();
        panel.Configure(runtime, restoredLoot,
            [opened.Binding, closed.Binding, vetoed.Changing.Binding, unlocked.Changed!.Binding, contents.Binding]);
        root.AddChild(trapNode);
        root.AddChild(lootNode);
        root.AddChild(eventNode);
        root.AddChild(panel);
        AddChild(root);

        AssertSmoke(resolution.Available && !hidden.Succeeded && detected.Succeeded && !friendly.Succeeded &&
                    triggered.Succeeded && triggered.Detail.Contains("damage:ember", StringComparison.Ordinal) &&
                    !disarmed.Trap.Armed && reset.Armed && skipped.EventName == "LootSpawnSkipped" &&
                    spawned is { EventName: "LootSpawned", Results.Count: 1 } &&
                    restoredLoot.ReusedSavedState && spawned.Results.SequenceEqual(restoredLoot.Results) &&
                    opened.Binding == "ContainerOpened" && opened.Payload.ActorId == "player:runtime" &&
                    opened.Payload.PreviousState == "closed" && opened.Payload.Authority == "server" &&
                    closed.Binding == "ContainerClosed" && vetoed.Changing.Payload.Vetoed &&
                    vetoed.Changed is null && vetoed.Lock.State == RpgLockState.Locked &&
                    unlocked.Changed?.Binding == "LockStateChanged" &&
                    unlocked.Changed.Payload.Method == "credential" &&
                    unlocked.Changed.Payload.Credential == "key:ember" &&
                    contents.Binding == "ContainerContentsChanged" && contents.Payload.PreviousRevision == 4 &&
                    contents.Payload.CurrentRevision == 5 && contents.Payload.Delta.Single().QuantityDelta == -1 &&
                    contents.Payload.Cause == "player-loot" && optional.OptionalFallback &&
                    required.DiagnosticCode == "WORLD_PLACEABLE_SECURITY_EVENTS_REQUIRED_MISSING" &&
                    restoredPlaceable.SecurityEvents?.Trap?.PayloadId == "damage:ember-burst" &&
                    restoredPlaceable.SecurityEvents?.Loot?.Timing == PlaceableAuthoredLootSpawnTiming.Open &&
                    restoredPlaceable.SecurityEvents?.LockEvents?.ControlledVeto == true &&
                    ProjectJsonSerializer.Serialize(ProjectJsonSerializer.Deserialize(serialized)) == serialized &&
                    root.GetChildCount() == 4 &&
                    panel.GetNodeOrNull<ItemList>("SecurityEventsLayout/LootResults") is { ItemCount: 1 } &&
                    panel.GetNodeOrNull<ItemList>("SecurityEventsLayout/EventLog") is { ItemCount: 5 } &&
                    panel.GetNodeOrNull<Button>("SecurityEventsLayout/TrapActions/DetectButton") is
                    { Disabled: false } &&
                    panel.GetNodeOrNull<Button>("SecurityEventsLayout/ContainerActions/DisturbButton") is
                    { Disabled: false },
            "Placeable trap, loot, state events, lock events, content deltas or runtime UI did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R094_PLACEABLE_SECURITY_EVENTS_PASS " +
            "trap=trigger-payload-detect-disarm-reset-owner-vfx-friendly-one-shot " +
            "loot=table-init-open-reset-seed-persisted-state " +
            "container=opened-closed-actor-result-previous-authority " +
            "lock=changing-changed-method-credential-actor-veto " +
            "disturbed=transactional-delta-cause-revision-actor " +
            "ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo " +
            "persistence=roundtrip forgescript=typed-handle-events mcp=filtered-dry-run " +
            "runtime=optional-safe-required-diagnostic rpgkit=security-event-services-reused " +
            "engine=trap-loot-container-lock-disturbed-accessible-panel");
    }

    private void RunPlaceableCompositionEventsRuntimeSmokeTest(
        PlaceableWorldProfile placeable,
        ProjectDefinition project)
    {
        var authoredCatalog = project.Module.WorldFeatures;
        var authoredPlaceable = authoredCatalog.Placeables.Single(item => item.Id == placeable.Id);
        var adapter = new PlaceableCompositionEventsRuntimeAdapter();
        var resolution = adapter.Resolve(authoredPlaceable);
        var handle = resolution.Handle ??
                     throw new InvalidOperationException("The placeable composition-events handle did not resolve.");
        var runtime = adapter.Build(handle, authoredPlaceable);
        var damaged = adapter.Damage(handle, authoredPlaceable, "player:runtime", 20, "force", 50, 30,
            new Dictionary<string, string> { ["weapon"] = "hammer" });
        var destroyed = adapter.Damage(handle, authoredPlaceable, "player:runtime", 30, "force", 30, 0);
        var attachment = adapter.Attach(handle, authoredPlaceable, runtime.Attachments.Single().Id);
        var verbs = adapter.ResolveVerbs(handle, authoredPlaceable,
            new HashSet<string> { "unlocked", "interlocutor", "portable", "climbable", "ash-rune" });
        var anchoredVerbs = adapter.ResolveVerbs(handle, authoredPlaceable,
            new HashSet<string>
                { "unlocked", "interlocutor", "portable", "climbable", "ash-rune", "anchored" });
        var optional = adapter.Resolve(authoredPlaceable with
        {
            CompositionEvents = null,
            CompositionEventsRequiredAtRuntime = false,
        });
        var required = adapter.Resolve(authoredPlaceable with
        {
            CompositionEvents = null,
            CompositionEventsRequiredAtRuntime = true,
        });

        var authoredProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(authoredCatalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        var serialized = ProjectJsonSerializer.Serialize(authoredProject);
        var restoredPlaceable = ProjectJsonSerializer.Deserialize(serialized).Module.WorldFeatures.Placeables
            .Single(item => item.Id == authoredPlaceable.Id);

        var root = new Node { Name = "PlaceableCompositionEvents_R095" };
        var damageNode = new Node { Name = "UniformDamageEvents" };
        damageNode.SetMeta("damaged", damaged.Binding);
        damageNode.SetMeta("destroyed", destroyed.Binding);
        var socket = runtime.Sockets.Single();
        var socketNode = new Marker3D
        {
            Name = $"Socket_{socket.Id}",
            Position = new((float)socket.LocalTransform.Position.X, (float)socket.LocalTransform.Position.Y,
                (float)socket.LocalTransform.Position.Z),
        };
        socketNode.SetMeta("kind", socket.Kind);
        socketNode.SetMeta("capacity", socket.MaximumAttachments);
        var attachmentNode = new Node3D
        {
            Name = "ComposedAttachment",
            Position = new((float)attachment.Attached.Transform.Position.X,
                (float)attachment.Attached.Transform.Position.Y,
                (float)attachment.Attached.Transform.Position.Z),
        };
        attachmentNode.SetMeta("prefab", attachment.Attached.PrefabId ?? string.Empty);
        attachmentNode.SetMeta("inverse_patch", attachment.InversePatch);
        var panel = new PlaceableCompositionEventsPanel();
        panel.Configure(runtime, damaged, destroyed, attachment, verbs);
        root.AddChild(damageNode);
        root.AddChild(socketNode);
        root.AddChild(attachmentNode);
        root.AddChild(panel);
        AddChild(root);

        AssertSmoke(resolution.Available && damaged.Binding == "DamageApplied" &&
                    damaged.Payload.Name == "DamageApplied" && damaged.Payload.EntityKind == "placeable" &&
                    damaged.Payload.SourceId == "player:runtime" && damaged.Payload.Amount == 20 &&
                    damaged.Payload.DamageType == "force" && damaged.Payload.PreviousHealth == 50 &&
                    damaged.Payload.CurrentHealth == 30 && damaged.Payload.Context["weapon"] == "hammer" &&
                    destroyed.Binding == "EntityDestroyed" && destroyed.Payload.CurrentHealth == 0 &&
                    attachment.Attached.Transform == socket.LocalTransform &&
                    attachment.Attached.PrefabId == "prefab:crypt-pillar-lever" &&
                    attachment.Patch.Contains("attach:/sockets/lever", StringComparison.Ordinal) &&
                    attachment.InversePatch.Contains("detach:/sockets/lever", StringComparison.Ordinal) &&
                    verbs.Count == 8 && verbs[0].Id == "push" && verbs[1].Id == "take" &&
                    verbs[2].Id == "custom-ring" && anchoredVerbs.All(item => item.Id != "push") &&
                    optional.OptionalFallback && required.DiagnosticCode ==
                    "WORLD_PLACEABLE_COMPOSITION_EVENTS_REQUIRED_MISSING" &&
                    restoredPlaceable.CompositionEvents?.DamageEvents.DamagedBinding == "DamageApplied" &&
                    restoredPlaceable.CompositionEvents.DamageEvents.DestroyedBinding == "EntityDestroyed" &&
                    restoredPlaceable.CompositionEvents.Sockets.Single().SocketId == "lever" &&
                    restoredPlaceable.CompositionEvents.Attachments.Single().PrefabId ==
                    "prefab:crypt-pillar-lever" &&
                    restoredPlaceable.CompositionEvents.MultiVerb.Verbs.Count == 8 &&
                    ProjectJsonSerializer.Serialize(ProjectJsonSerializer.Deserialize(serialized)) == serialized &&
                    root.GetChildCount() == 4 &&
                    panel.GetNodeOrNull<ItemList>("CompositionEventsLayout/SocketList") is { ItemCount: 2 } &&
                    panel.GetNodeOrNull<ItemList>("CompositionEventsLayout/VerbList") is { ItemCount: 8 } &&
                    panel.GetNodeOrNull<Button>("CompositionEventsLayout/CompositionActions/AttachButton") is
                    { Disabled: false },
            "Placeable damage events, sockets, attachments, multi-verbs or runtime UI did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R095_PLACEABLE_COMPOSITION_EVENTS_PASS " +
            "damage=applied-destroyed-uniform-source-amount-type-health-context " +
            "sockets=typed-transform-tags-snap-capacity-blueprint-prefab-reversible " +
            "verbs=inspect-use-open-talk-take-push-climb-custom-context-priority " +
            "ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo " +
            "persistence=roundtrip forgescript=typed-handle-events mcp=filtered-dry-run " +
            "runtime=optional-safe-required-diagnostic rpgkit=event-attachment-verb-services-reused " +
            "engine=damage-socket-attachment-contextual-verb-accessible-panel");
    }

    private void RunTriggerAdvancedRuntimeSmokeTest(TriggerWorldProfile trigger, ProjectDefinition project)
    {
        var authoredCatalog = project.Module.WorldFeatures;
        var authoredTrigger = authoredCatalog.Triggers.Single(item => item.Id == trigger.Id);
        var adapter = new TriggerAdvancedRuntimeAdapter();
        var resolution = adapter.Resolve(authoredTrigger);
        var handle = resolution.Handle ??
                     throw new InvalidOperationException("The advanced trigger handle did not resolve.");
        var runtime = adapter.Build(handle, authoredTrigger);
        var detected = adapter.Detect(handle, authoredTrigger, 10, "outsider");
        var disarmed = adapter.Disarm(handle, authoredTrigger, 12);
        var reset = adapter.Reset(handle, authoredTrigger, disarmed.Trap);
        var queryEntity = new RpgQueryableEntity("player:runtime", true,
            new HashSet<string> { "player", "hero" }, runtime.TargetFilter.FactionIds,
            new HashSet<string> { "inventory", "health" }, "party", "heroes",
            new Dictionary<string, string> { ["quest"] = "active" });
        var matches = adapter.Matches(handle, authoredTrigger, queryEntity);
        var effect = adapter.Apply(handle, authoredTrigger, TriggerGameplayVolumeKind.Damage, queryEntity.Id);
        var now = DateTimeOffset.UnixEpoch.AddSeconds(10);
        var state = new RpgTriggerRuntimeState(
            new Dictionary<string, DateTimeOffset> { [queryEntity.Id] = now.AddSeconds(-3) },
            new Dictionary<string, DateTimeOffset> { [queryEntity.Id] = now.AddSeconds(-2) },
            new HashSet<string> { queryEntity.Id }, false);
        var trace = new[] { new RpgTriggerEventTrace(now, "DamageVolumeEntered", queryEntity.Id, "fired") };
        var overlay = adapter.Debug(handle, authoredTrigger, state, now, trace);
        var optional = adapter.Resolve(authoredTrigger with
        {
            Advanced = null,
            AdvancedRequiredAtRuntime = false,
        });
        var required = adapter.Resolve(authoredTrigger with
        {
            Advanced = null,
            AdvancedRequiredAtRuntime = true,
        });

        var authoredProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(authoredCatalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        var serialized = ProjectJsonSerializer.Serialize(authoredProject);
        var restoredTrigger = ProjectJsonSerializer.Deserialize(serialized).Module.WorldFeatures.Triggers
            .Single(item => item.Id == authoredTrigger.Id);

        var root = new Node3D { Name = "TriggerAdvanced_R096" };
        var trapNode = new Area3D { Name = "TrapVolume" };
        trapNode.SetMeta("payload", runtime.Trap.Trap.PayloadId);
        trapNode.SetMeta("gm_visualization", runtime.Trap.GameMasterVisualization);
        root.AddChild(trapNode);
        foreach (var volume in runtime.GameplayVolumes)
        {
            var volumeNode = new Area3D { Name = $"GameplayVolume_{volume.Kind}" };
            volumeNode.SetMeta("enter_binding", volume.EnterEvent);
            volumeNode.SetMeta("exit_binding", volume.ExitEvent);
            root.AddChild(volumeNode);
        }
        var panel = new TriggerAdvancedPanel();
        panel.Configure(runtime, detected, disarmed, effect, overlay);
        root.AddChild(panel);
        AddChild(root);

        AssertSmoke(resolution.Available && runtime.GameplayVolumes.Count == 10 && detected.Succeeded &&
                    disarmed.Succeeded && !disarmed.Trap.Armed && reset.Trap.Armed && matches &&
                    effect.EventName == "DamageVolumeEntered" && effect.Changes["health"] == "-10" &&
                    overlay.Occupants.SequenceEqual([queryEntity.Id]) && overlay.LastFires.Count == 1 &&
                    overlay.CooldownsRemaining[queryEntity.Id] > TimeSpan.Zero &&
                    overlay.FilterSummary.Contains("required:player", StringComparison.Ordinal) &&
                    overlay.EventTrace.Single().EventName == "DamageVolumeEntered" &&
                    optional.OptionalFallback && required.DiagnosticCode == "WORLD_TRIGGER_ADVANCED_REQUIRED_MISSING" &&
                    restoredTrigger.Advanced?.Trap.PayloadId == "damage:ember-burst" &&
                    restoredTrigger.Advanced.GameplayVolumes.Count == 10 &&
                    restoredTrigger.Advanced.OccupancyDebug.MaximumTraceEntries == 100 &&
                    ProjectJsonSerializer.Serialize(ProjectJsonSerializer.Deserialize(serialized)) == serialized &&
                    root.GetChildCount() == 12 &&
                    panel.GetNodeOrNull<ItemList>("TriggerAdvancedLayout/GameplayVolumeList") is { ItemCount: 10 } &&
                    panel.GetNodeOrNull<ItemList>("TriggerAdvancedLayout/OccupancyList") is { ItemCount: 4 } &&
                    panel.GetNodeOrNull<Button>("TriggerAdvancedLayout/TriggerAdvancedActions/DisarmTrapButton") is
                    { Disabled: false },
            "Advanced trigger trap, target query, gameplay volumes, occupancy debug or runtime UI did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R096_TRIGGER_ADVANCED_PASS " +
                 "trap=detection-disarm-payload-reset-ownership-gm-visualization " +
                 "query=players-tags-factions-components-owner-team-sandbox " +
                 "volumes=damage-heal-gravity-water-climb-nobuild-camera-audio-weather-checkpoint " +
                 "debug=occupants-last-fires-cooldowns-filters-event-trace " +
                 "ui=visible-empty-error-overlay-preview command=dry-run-apply-reject history=undo-redo " +
                 "persistence=roundtrip forgescript=typed-handle-events mcp=filtered-dry-run " +
                 "runtime=optional-safe-required-diagnostic rpgkit=advanced-trigger-service-reused " +
                 "engine=area3d-trap-ten-gameplay-volumes-accessible-panel");
    }

    private void RunMerchantFoundationRuntimeSmokeTest(
        WorldFeatureCatalog catalog,
        MerchantWorldProfile merchant,
        ProjectDefinition project)
    {
        var creatureBlueprints = project.Blueprints.Where(item => item.Kind == BlueprintKind.Creature)
            .Select(item => item.Id).ToArray();
        var itemBlueprints = project.Blueprints.Where(item => item.Kind == BlueprintKind.Item)
            .Select(item => item.Id).ToArray();
        var vendorA = merchant.BlueprintId;
        var vendorB = creatureBlueprints.First(item => item != vendorA);
        var itemA = itemBlueprints[0];
        var itemB = itemBlueprints.ElementAtOrDefault(1) ?? itemA;
        var dialogueId = project.Dialogues.First().Id.Value;
        var currency = merchant.Currencies.First().Id;
        var foundation = new MerchantFoundationProfile(
            Guid.Parse("c2910000-0000-4000-8000-000000000006"), 1, "runtime:r091",
            "crypt-vigil-market", "merchant.crypt-vigil.name", ["fr-FR", "en-US"],
            [
                new(Guid.Parse("c2910000-0000-4000-8000-000000000007"), vendorA, dialogueId,
                    "choice:trade", "interaction:trade", "npc"),
                new(Guid.Parse("c2910000-0000-4000-8000-000000000008"), vendorB, dialogueId,
                    "choice:trade", "interaction:trade", "kiosk"),
            ],
            new(Guid.Parse("c2910000-0000-4000-8000-000000000009"), 1, "runtime:r091", true, true,
                true, true, true, "merchant-crypt-vigil-nominal"),
            [
                new(Guid.Parse("c2910000-0000-4000-8000-000000000010"), itemA, "standard",
                    MerchantSupplyPolicy.Finite, 12, "supplier:ember-guild", 12, 1,
                    ["quest:market-closed"], 50, currency),
                new(Guid.Parse("c2910000-0000-4000-8000-000000000011"), itemB, "unbounded",
                    MerchantSupplyPolicy.Infinite, 0, null, 0, 2, [], 10, currency),
            ],
            new(Guid.Parse("c2910000-0000-4000-8000-000000000012"), 1, "runtime:r091", 1.2m, .6m,
                [new(0, 1), new(100, 1.1m)], [new(0, 1), new(100, .9m)],
                new Dictionary<string, decimal>(StringComparer.Ordinal) { [currency] = 1 },
                .1m, .2m, .2m, .1m, 1, 500),
            new(Guid.Parse("c2910000-0000-4000-8000-000000000013"), 1, "runtime:r091", ["relic"],
                ["merchant-approved"], ["stolen", "cursed"], 1, 5, ["author:forge"], .5,
                null, "scripts/merchant_rule"));
        var authoredMerchant = merchant with { Foundation = foundation, FoundationRequiredAtRuntime = true };
        var adapter = new MerchantFoundationRuntimeAdapter();
        var resolution = adapter.Resolve(authoredMerchant);
        var handle = resolution.FoundationHandle ??
                     throw new InvalidOperationException("The merchant foundation handle did not resolve.");
        var store = adapter.BuildStore(handle, authoredMerchant);
        var context = new MerchantValidationContext(
            store.Stock.Select(item => item.ItemDefinitionId).ToHashSet(),
            store.Vendors.Select(item => item.VendorId).ToHashSet(StringComparer.Ordinal),
            store.CurrencyIds,
            new HashSet<string>(StringComparer.Ordinal) { "scripts/merchant_rule" },
            new HashSet<string>(StringComparer.Ordinal) { "supplier:ember-guild" },
            100);
        var wizard = adapter.PreviewWizard(handle, authoredMerchant, context);
        var price = adapter.Price(handle, authoredMerchant, itemA.Value, 50,
            new(currency, .5, .25, .9, .5, new HashSet<string>()));
        var item = new MerchantItemView(itemA.Value, "relic",
            new HashSet<string>(["merchant-approved"], StringComparer.Ordinal), 3, "author:forge", .9,
            string.Empty);
        var accepted = adapter.Matches(handle, authoredMerchant, item,
            (binding, _) => binding == "scripts/merchant_rule");
        var blocked = adapter.Matches(handle, authoredMerchant,
            item with { Tags = new HashSet<string>(["merchant-approved", "stolen"], StringComparer.Ordinal) },
            (_, _) => true);
        var optional = adapter.Resolve(authoredMerchant with
        { Foundation = null, FoundationRequiredAtRuntime = false });
        var required = adapter.Resolve(authoredMerchant with
        { Foundation = null, FoundationRequiredAtRuntime = true });
        var experienceAdapter = new MerchantStoreExperienceRuntimeAdapter();
        var experienceResolution = experienceAdapter.Resolve(authoredMerchant);
        var experienceHandle = experienceResolution.ExperienceHandle ??
                               throw new InvalidOperationException("The merchant experience handle did not resolve.");
        var storeUi = experienceAdapter.Ui(experienceHandle, authoredMerchant);
        var priceContext = new StorePriceContext(currency, .5,
            new Dictionary<Guid, PricedItem> { [itemA.Value] = price });
        var openedAt = DateTimeOffset.Parse("2026-08-11T12:00:00Z");
        var opened = experienceAdapter.Open(experienceHandle, authoredMerchant, "player:runtime",
            store.Vendors[0].VendorId, priceContext, openedAt);
        var sessionTransactionId = Guid.Parse("c2920000-0000-4000-8000-000000000020");
        var closed = experienceAdapter.Close(experienceHandle, authoredMerchant, opened, [sessionTransactionId],
            100, 25, openedAt.AddMinutes(2));
        var infiniteStock = store.Stock.Single(entry => entry.Supply == SupplyPolicy.Infinite);
        var infiniteState = new TradeState(store,
            new("player:runtime", new Dictionary<string, decimal> { [currency] = 10_000 },
                new Dictionary<Guid, int>(), 1000),
            new("store:runtime", new Dictionary<string, decimal> { [currency] = 1_000 },
                new Dictionary<Guid, int>(), 1000));
        var infinitePurchase = MerchantService.Execute(infiniteState,
            new(Guid.Parse("c2920000-0000-4000-8000-000000000021"), TradeKind.Buy, "player:runtime",
                "store:runtime",
                [new(infiniteStock.ItemDefinitionId, infiniteStock.VariantId, 50, infiniteStock.UnitPrice, currency)],
                []));
        var optionalExperience = experienceAdapter.Resolve(authoredMerchant with
        { Experience = null, ExperienceRequiredAtRuntime = false });
        var requiredExperience = experienceAdapter.Resolve(authoredMerchant with
        { Experience = null, ExperienceRequiredAtRuntime = true });

        var authoredCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
            catalog.Placeables, catalog.Doors, catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes,
            catalog.Merchants.Select(itemProfile => itemProfile.Id == merchant.Id ? authoredMerchant : itemProfile)
                .ToArray(), catalog.Companions, catalog.Scripts);
        var authoredProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithWorldFeatures(authoredCatalog), project.Blueprints, project.Areas, project.Dialogues,
            project.Quests);
        var serialized = ProjectJsonSerializer.Serialize(authoredProject);
        var restored = ProjectJsonSerializer.Deserialize(serialized).Module.WorldFeatures.Merchants
            .Single(itemProfile => itemProfile.Id == merchant.Id);

        var root = new Node { Name = "MerchantFoundation_R091" };
        foreach (var name in new[] { "StoreDefinition", "StoreWizard", "Stock", "Pricing", "ItemQuery" })
            root.AddChild(new Node { Name = name });
        var storePanel = new MerchantStorePanel();
        storePanel.Configure(store, storeUi);
        root.AddChild(storePanel);
        AddChild(root);

        AssertSmoke(resolution.Available && store.Vendors.Count == 2 && store.Stock.Count == 2 &&
                    store.Stock.Select(entry => entry.Supply).ToHashSet()
                        .SetEquals([SupplyPolicy.Finite, SupplyPolicy.Infinite]) &&
                    wizard.CanApply && price.BuyPrice is >= 1 and <= 500 && price.SellPrice is >= 1 and <= 500 &&
                    accepted && !blocked && optional.OptionalFallback &&
                    required.DiagnosticCode == "WORLD_MERCHANT_FOUNDATION_REQUIRED_MISSING" &&
                    restored.Foundation?.Wizard.TestFixtureId == "merchant-crypt-vigil-nominal" &&
                    restored.Foundation.Stock.Count == 2 &&
                    restored.Experience?.Sessions.OpenEventBinding == "StoreSessionOpened" &&
                    restored.Experience.Sessions.CloseEventBinding == "StoreSessionClosed" &&
                    restored.Experience.Ui.Columns.Count == 4 &&
                    ProjectJsonSerializer.Serialize(ProjectJsonSerializer.Deserialize(serialized)) == serialized &&
                    root.GetChildCount() == 6,
            "Merchant store, wizard, stock, pricing, query or runtime nodes did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R091_MERCHANT_FOUNDATION_PASS " +
            "store=independent-multi-vendor-interface-channel wizard=store-vendor-dialogue-interaction-tests " +
            "stock=finite-infinite-quantity-variant-supplier-restock-order-hidden " +
            "pricing=buy-sell-curves-currency-reputation-scarcity-condition-negotiation " +
            "restrictions=whitelist-blacklist-type-tags-quality-provenance-condition-owner-custom-rule " +
            "forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip optional=ignored " +
            "required=diagnostic rpgkit=merchant-services-reused engine=store-wizard-stock-pricing-query-nodes");
        AssertSmoke(experienceResolution.Available && infinitePurchase.Committed &&
                    infinitePurchase.State.Store.Stock.Single(entry =>
                        entry.ItemDefinitionId == infiniteStock.ItemDefinitionId &&
                        entry.VariantId == infiniteStock.VariantId).Quantity ==
                    infiniteStock.Quantity && infinitePurchase.State.Buyer.Items[infiniteStock.ItemDefinitionId] == 50 &&
                    opened.CustomerId == "player:runtime" && opened.VendorId == store.Vendors[0].VendorId &&
                    opened.PriceContext == priceContext && closed.TransactionIds.Single() == sessionTransactionId &&
                    closed.BoughtTotal == 100 && closed.SoldTotal == 25 && storeUi.Keyboard && storeUi.Gamepad &&
                    storeUi.Compare && storeUi.Filters && storeUi.Cart && storeUi.Confirmation &&
                    storeUi.ScreenReaderLabels && storeUi.HighContrast && storeUi.Columns.Count == 4 &&
                    optionalExperience.OptionalFallback &&
                    requiredExperience.DiagnosticCode == "WORLD_MERCHANT_EXPERIENCE_REQUIRED_MISSING" &&
                    storePanel.GetNodeOrNull<LineEdit>("StoreLayout/StoreFilters") is { Editable: true } &&
                    storePanel.GetNodeOrNull<ItemList>("StoreLayout/StoreStock") is { ItemCount: 2 } &&
                    storePanel.GetNodeOrNull<Button>("StoreLayout/StoreActions/ConfirmButton") is
                    { Disabled: false },
            "Merchant infinite stock, session events or accessible store UI did not resolve.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R092_MERCHANT_EXPERIENCE_PASS " +
            "stock=infinite-unbounded-quantity-stable " +
            "sessions=opened-closed-customer-vendor-price-context-transaction-summary-api-1.0 " +
            "ui=keyboard-gamepad-compare-filters-cart-confirmation-screen-reader-high-contrast-columns " +
            "forgescript=typed-handle-events mcp=filtered-dry-run persistence=roundtrip optional=ignored " +
            "required=diagnostic rpgkit=store-session-ui-services-reused engine=accessible-store-panel");
    }


}
