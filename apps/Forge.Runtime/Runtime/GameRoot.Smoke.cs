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

        var achievement = PostLaunchCapabilityResolver.EvaluateAchievement(profile.Gameplay, "crypt-liberated", 1);
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
        var reloadRequest = new ServerProductionService.ReloadRequest(Guid.NewGuid(), "crypt-eu", 64, 4, 0,
            new string('a', 64));
        var reloadPreview = server.Reload(reloadRequest, false);
        var reloadApplied = server.Reload(reloadRequest, true);
        AssertSmoke(reloadPreview.Code == "NET_PRODUCTION_PREVIEW" && reloadApplied.Applied && reloadApplied.RollbackAvailable,
            "Shard hot reload did not preview and apply atomically.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G13AH_SHARD_RELOAD_PASS shard=crypt-eu atomic=true rollback=true");

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
        AssertSmoke(cameraScript.ScriptId == "scripts/door_ash_opened" && cameraScript.CameraControlLocked &&
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
        InteractWith(_keyItem!);
        InteractWith(_door!);
        DamageHostile(_boss!);
        _player!.GlobalPosition = new Vector3(2, 0.9f, 1);
        AssertSmoke(SaveRuntime(_quickSavePath!), "The runtime checkpoint was not written.");
        AssertSmoke(_questMachine?.CurrentState.DisplayName == "Gardien des Cendres",
            "The saved quest did not reach the boss objective.");
        AssertSmoke(_hostiles[_boss!].Health == 3, "The saved boss health is not the expected checkpoint.");
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_SAVE_PASS file={Path.GetFileName(_quickSavePath)} " +
            $"quest={_questMachine!.CurrentState.Id} boss_health={_hostiles[_boss!].Health}");
        GetTree().Quit(0);
    }

    private void RunSaveResumeSmokeTest()
    {
        AssertSmoke(_loadedSave, "The save resume smoke test did not load a checkpoint.");
        AssertSmoke(_hasKey && _doorUnlocked, "Inventory or unlocked door state was not restored.");
        AssertSmoke(_questMachine?.CurrentState.DisplayName == "Gardien des Cendres",
            "Quest state was not restored.");
        AssertSmoke(_boss is not null && _hostiles.TryGetValue(_boss, out var boss) && boss.Health == 3,
            "Boss combat state was not restored.");
        AssertSmoke(_player?.GlobalPosition.IsEqualApprox(new Vector3(2, 0.9f, 1)) == true,
            "Player position was not restored.");
        while (_hostiles.TryGetValue(_boss!, out var combatant) && combatant.Health > 0)
        {
            DamageHostile(_boss!);
        }

        AssertSmoke(_bossDefeated && _questMachine?.Completed == true,
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
        AssertSmoke(_questState == PrototypeQuestState.Active, "NPC interaction did not start the quest.");
        var dialoguePresentation = _elyraDialogue?.Presentation;
        AssertSmoke(dialoguePresentation is not null &&
                    DialoguePresentationValidator.Validate(_elyraDialogue!).Count == 0,
            "The authored dialogue presentation profile did not resolve.");
        AssertSmoke(_dialogueAnimationCueCount == 1 && _dialogueVoiceCueCount == 1 &&
                    _dialogueCameraShotCount == 1 && _dialogueBookmarkCount == 1 &&
                    _dialogueInterjectionCount == 1 && _dialogueSubtitleFallback,
            "Dialogue animation, voice fallback, camera, bookmark, or interjection did not execute.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G13H_DIALOGUE_PRESENTATION_PASS " +
            $"animations={_dialogueAnimationCueCount} voices={_dialogueVoiceCueCount} " +
            $"shots={_dialogueCameraShotCount} bookmarks={_dialogueBookmarkCount} " +
            $"interjections={_dialogueInterjectionCount} subtitle_fallback={_dialogueSubtitleFallback.ToString().ToLowerInvariant()} " +
            "authority=server accessibility=player-priority");
        var timedRule = AssertSingle(dialoguePresentation!.TimedChoices);
        var adaptiveTimeout = DialogueAdaptiveRuntimeResolver.Resolve(_elyraDialogue!, _elyraDialogue!.EntryNodeId,
            timedRule.DurationMilliseconds + 1, AdaptiveDialogueContext(), new RuntimeConditionEvaluator(this));
        AssertSmoke(adaptiveTimeout.Diagnostics.Count == 0 && adaptiveTimeout.TimedChoice is
        { TimedOut: true, SelectedChoiceId: not null, TimeoutScriptId: not null } &&
            adaptiveTimeout.Variant is not null && _dialogueTimedChoiceCount == 1 && _dialogueVariantCount == 1 &&
            !string.IsNullOrWhiteSpace(_dialogueVariantKey),
            "Timed choice or deterministic localized variant did not execute.");
        var accessibilityPause = DialogueAdaptiveRuntimeResolver.Resolve(_elyraDialogue!, _elyraDialogue!.EntryNodeId,
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
            _questMachine?.CurrentState.DisplayName == "Clé de braise",
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
        AssertSmoke(_inventory.Contains("warden-ember"), "Data-driven boss loot was not granted.");
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
        AssertSmoke(_questMachine?.Completed == true, "Data-driven quest was not completed.");
        var questOperations = _questDefinition?.Operations ??
            throw new InvalidOperationException("The authored quest operations profile is missing.");
        var repeat = QuestOperationsRuntimeResolver.ResolveRepeat(_questDefinition!, new(
            DateTimeOffset.Parse("2026-08-06T12:00:00Z"), DateTimeOffset.Parse("2026-08-06T10:00:00Z"),
            1, 1, 1, true, true, _chosenQuestTransitions));
        var sortedJournal = QuestOperationsRuntimeResolver.SortJournal([_questDefinition!]);
        AssertSmoke(repeat.Allowed && repeat.RewardMultiplier == questOperations.Repeat.RewardDecay &&
                    sortedJournal.Single().Id == _questDefinition!.Id && _chosenQuestTransitions.Count == 3,
            "Quest journal, repetition, sharing or exclusive-choice policies did not execute.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G13J_QUEST_OPERATIONS_PASS priority={questOperations.Journal.Priority} " +
                 $"repeat_seed={repeat.Seed} reward={repeat.RewardMultiplier:0.###} " +
                 $"share={questOperations.Share.Mode.ToString().ToLowerInvariant()} " +
                 $"choices={questOperations.ExclusiveChoices.Count} notifications={questOperations.Notification.Mode.ToString().ToLowerInvariant()}");

        RunAudioEmitterRuntimeSmokeTest();
        RunDialogueGraphRuntimeSmokeTest();
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
        var authoredCatalog = project.Module.WorldFeatures.Placeables.Any(item =>
            item.Id == placeable.Id && item.Operations is not null)
            ? project.Module.WorldFeatures
            : CryptWorldFeatureAuthoringService.Create(project);
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
        var authoredCatalog = project.Module.WorldFeatures.Placeables.Any(item =>
            item.Id == placeable.Id && item.SecurityEvents is not null)
            ? project.Module.WorldFeatures
            : CryptWorldFeatureAuthoringService.Create(project);
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
        var authoredCatalog = project.Module.WorldFeatures.Placeables.Any(item =>
            item.Id == placeable.Id && item.CompositionEvents is not null)
            ? project.Module.WorldFeatures
            : CryptWorldFeatureAuthoringService.Create(project);
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
        var authoredCatalog = project.Module.WorldFeatures.Triggers.Any(item =>
            item.Id == trigger.Id && item.Advanced is not null)
            ? project.Module.WorldFeatures
            : CryptWorldFeatureAuthoringService.Create(project);
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

    private void RunGenericHeadlessSmokeTest()
    {
        AssertSmoke(!_isCryptPrototype, "The generic smoke test cannot use the Crypt prototype module.");
        AssertSmoke(!string.IsNullOrWhiteSpace(_moduleSlug), "The generic module slug was not loaded.");
        AssertSmoke(_builtEntityCount >= 2, "Expected at least the generated floor and player spawn.");
        AssertSmoke(_collisionCount >= 1, "Expected at least one generated floor collision.");
        AssertSmoke(_player is not null, "The generic player spawn was not reconstructed.");
        AssertSmoke(_playerCombatant is null, "A Core starter module must not require an RPG combat profile.");
        AssertSmoke(_npc is null && _keyItem is null && _door is null && _boss is null && _exit is null,
            "The generic starter unexpectedly reconstructed Crypt-specific gameplay actors.");
        AssertSmoke(FindChild("AshRune", recursive: true, owned: false) is null,
            "The generic starter unexpectedly loaded Crypt-specific atmosphere.");
        InteractWithNearest();
        AssertSmoke(string.Equals(_message, "Rien à portée d’interaction.", StringComparison.Ordinal),
            "The generic interaction path did not complete cleanly without a target.");
        RunAudioEmitterRuntimeSmokeTest();
        RunDialogueGraphRuntimeSmokeTest();
        RunQuestJournalRuntimeSmokeTest();
        RunWaypointAnchorRuntimeSmokeTest();
        RunGameplayFoundationRuntimeSmokeTest();
        RunBlackboardRulesetPluginRuntimeSmokeTest();
        RunProjectFoundationRuntimeSmokeTest(_loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));
        RunProjectOperationsRuntimeSmokeTest(_loadedProject);
        RunProjectCompatibilityRuntimeSmokeTest(_loadedProject);
        RunProjectGameplayEventsRuntimeSmokeTest(_loadedProject);
        RunProjectWorldRuntimeSmokeTest(_loadedProject);
        RunUx0GGlobalEventsRuntimeSmokeTest(_loadedProject);
        RunUx0HWorldTransitionsRuntimeSmokeTest(_loadedProject);
        RunCanonicalValidationRuntimeSmokeTest();
        RunAreaFoundationRuntimeSmokeTest(_loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));
        RunAreaRuntimeValidationSmokeTest(_loadedProject ??
            throw new InvalidOperationException("The loaded project is unavailable."));

        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_GENERIC_SMOKE_PASS slug={_moduleSlug} " +
            $"entities={_builtEntityCount} collisions={_collisionCount} combat=false interaction=empty");
        GetTree().Quit(0);
    }

    private void RunAudioEmitterRuntimeSmokeTest()
    {
        var clipData = ProceduralAmbientAudioService.Create(ProceduralAmbientAudioService.StreamResource);
        var definitionId = Guid.Parse("a0040000-0000-4000-8000-000000000501");
        var instanceId = Guid.Parse("a0040000-0000-4000-8000-000000000502");
        var catalog = new AudioEmitterCatalog(1,
        [
            new(definitionId, 1, "Runtime stream",
                new AudioClipSet(Guid.Parse("a0040000-0000-4000-8000-000000000503"), 1,
                    "Runtime stream clips", AudioClipSelectionMode.Ordered,
                    [new(Guid.Parse("a0040000-0000-4000-8000-000000000504"), clipData.Resource, 1,
                        ["water", "smoke"], "runtime", "und", -18, string.Empty, "audio/wav",
                        clipData.Pcm16Mono.LongLength, "CC0-1.0", "builtin:procedural",
                        clipData.ContentSha256)], "smoke:r004"),
                new AudioLoopPolicy(AudioLoopMode.SeamlessRegion, 0, .2f, 1.6f, .1f, string.Empty),
                new AudioEmitterSpatialProfile(2, 25, "inverse-square", 180, -12, .2f, 1, true),
                null, 0, string.Empty, string.Empty, true, "smoke:r004", "builtin/audio",
                "CC0-1.0", clipData.ContentSha256),
        ],
        [
            new(instanceId, definitionId,
                new TransformValue(new Vector3Value(3, 2, -4), QuaternionValue.Identity, Vector3Value.One),
                true, "smoke:r004"),
        ]);
        AssertSmoke(AudioEmitterCatalogValidator.Validate(catalog).Count == 0,
            "The runtime audio emitter fixture is invalid.");
        var profile = new AreaAudioProfile(string.Empty, string.Empty, string.Empty, string.Empty,
            "neutral", 0, emitters: catalog);
        ConfigureAudioEmitters(profile);
        var player = GetNodeOrNull<AudioStreamPlayer3D>($"ForgeAudioEmitter_{instanceId:N}");
        AssertSmoke(player is not null && player.Position == new Vector3(3, 3, -4) &&
                    Math.Abs(player.MaxDistance - 25) < .001f &&
                    player.Stream is AudioStreamWav { LoopMode: AudioStreamWav.LoopModeEnum.Forward } wav &&
                    wav.LoopBegin > 0 && wav.LoopEnd > wav.LoopBegin,
            "The runtime did not reconstruct the positioned seamless audio emitter.");
        player!.QueueFree();
        GD.Print("OPENRPG_FORGE_RUNTIME_R004_AUDIO_PASS object=audio-stream-player-3d " +
            "position=canonical loop=wav-region optional=empty-safe required=diagnostic");
    }

    private void RunDialogueGraphRuntimeSmokeTest()
    {
        IReadOnlyList<DialogueDefinition> optionalDialogues = [];
        AssertSmoke(optionalDialogues.Count == 0, "An optional empty dialogue catalog was not safe.");
        var entry = DialogueNodeId.From(Guid.Parse("d0050000-0000-4000-8000-000000000201"));
        var end = DialogueNodeId.From(Guid.Parse("d0050000-0000-4000-8000-000000000202"));
        var reference = DialogueChoiceId.From(Guid.Parse("d0050000-0000-4000-8000-000000000203"));
        var flow = DialogueChoiceId.From(Guid.Parse("d0050000-0000-4000-8000-000000000204"));
        var graph = new DialogueDefinition(
            DialogueId.From(Guid.Parse("d0050000-0000-4000-8000-000000000205")),
            "runtime-dialogue-probe", "Runtime dialogue probe", entry,
            [
                new(entry, null, new LocalizedText("dialogue.runtime.entry", "Choose",
                    [LocalizedTextVariant.Create("dialogue.runtime.entry", "fr", "Choisir")], "smoke:r006"),
                    [new DialogueAction(DialogueActionKind.PlayAudio, "dialogue.entry", "play")], choices:
                [
                    new(reference, new("dialogue.runtime.reference", "Inspect"), targetNodeId: end,
                        edgeKind: DialogueEdgeKind.Reference, priority: 10),
                    new(flow, new("dialogue.runtime.flow", "Continue"),
                        [new DialogueCondition(DialogueConditionKind.HasItem, "ash-key", "true"),
                         new DialogueCondition(DialogueConditionKind.QuestStateEquals, "crypt", "completed", negated: true)],
                        [new DialogueAction(DialogueActionKind.StartSequence, "crypt-opening", "start")],
                        end, priority: 20, conditionCombination: DialogueConditionCombination.All),
                ], role: DialogueNodeRole.PlayerChoice),
                new(end, null, new("dialogue.runtime.end", "End"), role: DialogueNodeRole.End),
            ], provenance: "smoke:r005");
        var traversal = new DialogueTraversal(graph);
        var ordered = traversal.GetAvailableChoices(new RuntimeConditionEvaluator(this));
        var simulation = DialogueSimulationService.Run(graph,
            new Dictionary<string, string> { ["item:ash-key"] = "true", ["quest:crypt"] = "active" },
            [flow]);
        var invalid = new DialogueDefinition(
            DialogueId.From(Guid.Parse("d0050000-0000-4000-8000-000000000206")),
            "required-invalid-dialogue", "Required invalid dialogue", entry,
            [new(entry, null, new("dialogue.runtime.invalid", "Invalid"),
                choices: [new(flow, new("dialogue.runtime.invalid.choice", "Invalid"))],
                role: DialogueNodeRole.End)], provenance: "smoke:r005");
        AssertSmoke(DialogueValidator.Validate(graph).Count == 0 && ordered[0].Id == reference &&
                    simulation.Succeeded && simulation.Completed &&
                    simulation.FinalState.Values.GetValueOrDefault("sequence:crypt-opening") == "start" &&
                    DialogueValidator.Validate(invalid).Any(item => item.Code == DialogueValidator.NodeRoleInvalidCode),
            "Dialogue graph runtime ordering, reference reuse or required diagnostics failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R005_DIALOGUE_PASS roles=explicit order=priority-list " +
            "reference=reused optional=empty-safe required=diagnostic");
        GD.Print("OPENRPG_FORGE_RUNTIME_R006_DIALOGUE_PASS localization=variant-fallback " +
            "conditions=all-any-not actions=transactional simulation=sandbox-journal " +
            "lint=structural optional=empty-safe required=diagnostic");
    }

    private void RunQuestJournalRuntimeSmokeTest()
    {
        IReadOnlyList<QuestDefinition> optionalQuests = [];
        AssertSmoke(optionalQuests.Count == 0, "An optional empty quest catalog was not safe.");
        var initial = QuestStateId.From(Guid.Parse("c7070000-0000-4000-8000-000000000101"));
        var failed = QuestStateId.From(Guid.Parse("c7070000-0000-4000-8000-000000000102"));
        var transition = QuestTransitionId.From(Guid.Parse("c7070000-0000-4000-8000-000000000103"));
        var quest = new QuestDefinition(
            QuestId.From(Guid.Parse("c7070000-0000-4000-8000-000000000104")), 1, "smoke:r007",
            "runtime-journal-probe", "Runtime journal probe",
            new("quest.runtime.title", "Épreuve de la braise",
                [LocalizedTextVariant.Create("quest.runtime.title", "en", "Trial of the Ember")], "smoke:r007"),
            new("quest.runtime.description", "Vérifie la progression isolée."), "main", 80, "runtime:smoke",
            true, new(QuestProgressScope.Player, [QuestProgressScope.Player], false), initial,
            [
                new(initial, 1, "smoke:r007", "initial", "Initial", new(
                    new LocalizedText("quest.runtime.initial.summary", "Entrez dans la crypte."),
                    new LocalizedText("quest.runtime.initial.details", "Atteignez le sanctuaire."),
                    [new LocalizedText("quest.runtime.initial.objective", "Atteindre le sanctuaire")],
                    [new QuestJournalLink(QuestJournalLinkKind.Custom, "lore:ember")], "smoke:r007"),
                    false, 1, 0, QuestTerminalKind.None, string.Empty, QuestReactivationRule.Never),
                new(failed, 1, "smoke:r007", "failed", "Échouée", new(
                    new LocalizedText("quest.runtime.failed.summary", "Le sanctuaire est perdu."),
                    new LocalizedText("quest.runtime.failed.details", "La quête peut être réactivée manuellement."),
                    provenance: "smoke:r007"), true, 2, 1, QuestTerminalKind.Failed, string.Empty,
                    QuestReactivationRule.Manual),
            ],
            [new QuestTransition(transition, initial, failed)]);
        var instance = QuestInstanceService.Create(quest, QuestProgressScope.Player, "player:runtime",
            DateTimeOffset.Parse("2026-08-07T12:00:00Z"), id: Guid.Parse("c7070000-0000-4000-8000-000000000105"));
        var terminal = QuestInstanceService.Advance(quest, instance, transition, new RuntimeConditionEvaluator(this),
            DateTimeOffset.Parse("2026-08-07T12:01:00Z"), "player:runtime");
        var reactivated = QuestInstanceService.Reactivate(quest, terminal,
            DateTimeOffset.Parse("2026-08-07T12:02:00Z"), "gm:runtime");
        var invalidRequired = new QuestDefinition(quest.Id, 99, quest.Provenance, quest.Slug, quest.DisplayName,
            quest.Title, quest.Description, quest.Category, quest.Priority, quest.Owner, true, quest.InstancePolicy,
            quest.InitialStateId, quest.States, quest.Transitions, quest.Operations);
        AssertSmoke(QuestValidator.Validate(quest).Count == 0 && terminal.CurrentStateId == failed &&
                    terminal.History.Count == 2 && reactivated.CurrentStateId == initial &&
                    quest.States.Single(item => item.Id == failed).TerminalKind == QuestTerminalKind.Failed &&
                    QuestValidator.Validate(invalidRequired).Any(item => item.Code == QuestValidator.SchemaUnsupportedCode),
            "Quest journal instance, terminal state, reactivation or required diagnostic failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R007_QUEST_PASS metadata=localized-category-priority-owner " +
            "states=uuid-order-legacy-index terminal=typed-reactivation instance=player-history-variables-time " +
            "journal=summary-details-objectives-links optional=empty-safe required=diagnostic");

        var r008Transition = new QuestTransition(transition, 1, "smoke:r008", initial, failed, 1,
            new QuestEventBinding(QuestEventKind.Script, "quest.fail", "script:runtime-smoke", "smoke:r008"),
            90, QuestTransitionIdempotence.PerEvent, "runtime-fail", 1,
            actions: [new QuestAction(QuestActionKind.SetVariable, "quest.failed", "true")]);
        var r008Simulation = new QuestSimulationProfile(
            Guid.Parse("c7080000-0000-4000-8000-000000000101"), 8,
            events: [new QuestSimulationEvent(Guid.Parse("c7080000-0000-4000-8000-000000000102"),
                "quest.fail", DateTimeOffset.Parse("2026-08-07T12:01:00Z"), "script:runtime")],
            provenance: "smoke:r008");
        var r008Quest = new QuestDefinition(quest.Id, quest.SchemaVersion, "smoke:r008", quest.Slug,
            quest.DisplayName, quest.Title, quest.Description, quest.Category, quest.Priority, quest.Owner,
            quest.RequiredAtRuntime, quest.InstancePolicy, quest.InitialStateId, quest.States, [r008Transition],
            quest.Operations, r008Simulation);
        var r008Result = QuestSimulationEngine.Run(r008Quest, QuestProgressScope.Player, "player:runtime",
            DateTimeOffset.Parse("2026-08-07T12:00:00Z"));
        AssertSmoke(QuestValidator.Validate(r008Quest).Count == 0 &&
                    r008Result.Status == QuestSimulationStatus.Completed && r008Result.Audit.Count == 1 &&
                    r008Result.Instance.Variables.GetValueOrDefault("quest.failed") == "true" &&
                    r008Result.Audit[0].IdempotenceKey.Contains("runtime-fail", StringComparison.Ordinal),
            "Quest transition binding, sandbox simulation, audit or narrative validation failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R008_QUEST_PASS transition=event-priority-idempotence-order " +
            "simulation=events-effects-audit-terminal lint=clean optional=empty-safe required=diagnostic");

        var objectives = Enum.GetValues<QuestObjectiveKind>().Select((kind, index) =>
            new QuestAuthoringObjective(Guid.Parse($"c7110000-0000-4000-8000-{(500 + index):D12}"), 1,
                "smoke:r071", index + 1, kind,
                new LocalizedText($"quest.r071.{kind.ToString().ToLowerInvariant()}", kind.ToString()),
                kind is QuestObjectiveKind.Counter or QuestObjectiveKind.Boolean or QuestObjectiveKind.Collection ? 1 : 0,
                kind is QuestObjectiveKind.Collection or QuestObjectiveKind.Escort ? ["ash-key"] : [],
                kind is QuestObjectiveKind.Destination or QuestObjectiveKind.Escort ? "crypt-of-ashes" : null,
                kind == QuestObjectiveKind.Survival ? 60 : 0,
                kind == QuestObjectiveKind.Choice ? ["choice:dawn", "choice:dusk"] : [],
                kind == QuestObjectiveKind.Custom ? "rule:ember" : null, false)).ToArray();
        var participant = new QuestAuthoringParticipant(
            Guid.Parse("c7110000-0000-4000-8000-000000000601"), 1, "smoke:r071", 1, "guide",
            QuestParticipantBindingKind.Query, "query:tag=guide", ["quest:active"], true);
        var authoring = new QuestAuthoringProfile(
            Guid.Parse("c7110000-0000-4000-8000-000000000602"), 1, "smoke:r071", true, true,
            objectives, new QuestAuthoringRewardBundle(
                Guid.Parse("c7110000-0000-4000-8000-000000000603"), 1, "smoke:r071", 250,
                new Dictionary<string, int> { ["ash-key"] = 1 },
                new Dictionary<string, decimal> { ["ember"] = 5 },
                new Dictionary<string, double> { ["ash-watch"] = 2 }, ["unlock:crypt"], ["choice:dawn"]),
            new QuestAuthorNotes("Restore the vigil", "The ember pact is canonical", "author:runtime",
                QuestEditorialStatus.Review, ["branch complexity"], ["record voices"], ["08_QUEST.md"],
                [new QuestAuthorReview(Guid.Parse("c7110000-0000-4000-8000-000000000604"), 1,
                    "smoke:r071", "reviewer:runtime", QuestEditorialStatus.Review, "Reviewed",
                    DateTimeOffset.Parse("2026-08-11T00:00:00Z"))]), [participant],
            new QuestPlotWizardPlan(Guid.Parse("c7110000-0000-4000-8000-000000000605"), 1,
                "smoke:r071", ["crypt-of-ashes"], [participant.Id], ["crypt-pillar"],
                [DialogueId.From(Guid.Parse("40000000-0000-4000-8000-000000000001"))],
                objectives.Select(item => item.Id).ToArray(), r008Quest.Transitions.Select(item => item.Id).ToArray(),
                ["nominal-path", "objective-failure", "repeated-transition", "multiplayer-authority", "save-resume"],
                false));
        var r071Quest = r008Quest.WithAuthoring(authoring);
        var rewardState = new QuestAuthoringRewardState(0, new Dictionary<string, int>(),
            new Dictionary<string, decimal>(), new Dictionary<string, double>(), new HashSet<string>(),
            new HashSet<string>());
        var reward = QuestAuthoringRuntimeService.GrantRewards(r071Quest, rewardState, "player:runtime");
        var script = new QuestJournalScriptService(r071Quest);
        var authoringSnapshot = script.ReadAuthoring(script.Handle);
        BuildQuestAuthoringAdapters([r071Quest]);
        var questAdapter = _questAuthoringRoot?.GetNodeOrNull<Node>($"Quest_{r071Quest.Id.Value:N}");
        var invalidAuthoringRequired = r071Quest.WithAuthoring(authoring with
        { Enabled = false, RequiredAtRuntime = true });
        AssertSmoke(QuestAuthoringProfileValidator.Validate(r071Quest).Count == 0 &&
                    Enum.GetValues<QuestObjectiveKind>().All(kind =>
                        authoring.Objectives.Any(item => item.Kind == kind)) && reward.Committed &&
                    reward.State.Progression == 250 && reward.State.Items["ash-key"] == 1 &&
                    reward.State.Currencies["ember"] == 5 && reward.State.Reputation["ash-watch"] == 2 &&
                    reward.State.Unlocks.Contains("unlock:crypt") && reward.State.Choices.Contains("choice:dawn") &&
                    authoringSnapshot.ObjectiveCount == 8 && authoringSnapshot.ParticipantCount == 1 &&
                    authoringSnapshot.GeneratedTestCount == 5 && !authoringSnapshot.GeneratesOpaqueCode &&
                    questAdapter is not null &&
                    questAdapter.GetNodeOrNull<Node>("ExplicitObjectives")?.GetChildCount() == 8 &&
                    questAdapter.GetNodeOrNull<Node>("ScenarioCast")?.GetChildCount() == 1 &&
                    questAdapter.GetNodeOrNull<Node>("TransparentPlotWizard") is not null &&
                    QuestAuthoringProfileValidator.Validate(r008Quest).Count == 0 &&
                    QuestAuthoringProfileValidator.Validate(invalidAuthoringRequired)
                        .Any(item => item.Code == QuestAuthoringProfileValidator.RequiredMissingCode),
            "Quest authoring objectives, rewards, notes, cast, wizard, script or engine adapters failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R071_QUEST_AUTHORING_PASS " +
            "objectives=counter-boolean-collection-destination-escort-survival-choice-custom " +
            "rewards=atomic-progression-items-currency-reputation-unlocks-choices " +
            "notes=design-canon-owner-status-risks-todo-sources-review-history " +
            "cast=role-blueprint-instance-query-presence-required " +
            "wizard=zones-cast-props-dialogues-objectives-transitions-five-tests-no-opaque-code " +
            "forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic engine=nodes");

        var resources = Enum.GetValues<QuestExperienceResourceKind>().Select((kind, index) =>
            new QuestExperienceResource(Guid.Parse($"c7120000-0000-4000-8000-{(500 + index):D12}"), 1,
                "smoke:r072", index + 1, kind, $"resource:{kind.ToString().ToLowerInvariant()}",
                "owner:module", false, ["quest:active"])).ToArray();
        var experience = new QuestExperienceProfile(
            Guid.Parse("c7120000-0000-4000-8000-000000000601"), 1, "smoke:r072", true, true,
            resources,
            [new QuestExperienceStep(Guid.Parse("c7120000-0000-4000-8000-000000000602"), 1,
                "smoke:r072:template:investigation:v2", 1,
                new LocalizedText("quest.r072.step", "Inspect the ember prism"), [objectives[0].Id],
                r071Quest.Transitions.Select(item => item.Id).ToArray(), "investigation", 2, true)],
            new QuestExperienceVisibilityPolicy(Guid.Parse("c7120000-0000-4000-8000-000000000603"), 1,
                "smoke:r072", QuestExperienceVisibility.Hidden,
                new Dictionary<QuestExperienceVisibility, IReadOnlyList<string>>
                {
                    [QuestExperienceVisibility.Rumor] = ["heard"],
                    [QuestExperienceVisibility.Discovered] = ["heard", "seen"],
                    [QuestExperienceVisibility.Tracked] = ["accepted"],
                    [QuestExperienceVisibility.SecretCompleted] = ["secret:complete"],
                }, true),
            [
                new QuestExperienceHistoryEvent(Guid.Parse("c7120000-0000-4000-8000-000000000604"), 1,
                    "smoke:r072", r071Quest.Id, "none", "hidden", "module-load", "server",
                    DateTimeOffset.Parse("2026-08-11T08:00:00Z"), new Dictionary<string, string>(),
                    new LocalizedText("quest.r072.history.hidden", "A rumor rises.")),
                new QuestExperienceHistoryEvent(Guid.Parse("c7120000-0000-4000-8000-000000000605"), 1,
                    "smoke:r072", r071Quest.Id, "hidden", "discovered", "dialogue", "player:runtime",
                    DateTimeOffset.Parse("2026-08-11T08:01:00Z"),
                    new Dictionary<string, string> { ["seed"] = "72" },
                    new LocalizedText("quest.r072.history.discovered", "The crypt is revealed.")),
            ],
            Enum.GetValues<QuestExperienceMarkerBindingKind>().Select((kind, index) =>
                new QuestExperienceMarker(Guid.Parse($"c7120000-0000-4000-8000-{(700 + index):D12}"), 1,
                    "smoke:r072", index + 1, objectives[0].Id, kind,
                    kind switch
                    {
                        QuestExperienceMarkerBindingKind.Zone => "crypt-of-ashes",
                        QuestExperienceMarkerBindingKind.Entity => "entity:guardian",
                        QuestExperienceMarkerBindingKind.Query => "query:nearest-ember",
                        _ => "waypoint:crypt-entry",
                    }, QuestExperienceVisibility.Discovered, 4 + index, true,
                    new LocalizedText($"quest.r072.marker.{index}", kind.ToString()),
                    kind == QuestExperienceMarkerBindingKind.Waypoint ? new Vector3Value(1, 0, 2) : null))
                .ToArray());
        var r072Quest = r071Quest.WithExperience(experience);
        var experienceScript = new QuestJournalScriptService(r072Quest);
        var experienceSnapshot = experienceScript.ReadExperience(experienceScript.Handle);
        var playerHistory = experienceScript.ReadHistory(experienceScript.Handle,
            QuestExperienceHistoryView.Player);
        var debugHistory = experienceScript.ReadHistory(experienceScript.Handle,
            QuestExperienceHistoryView.Debug);
        var revealed = QuestExperienceRuntimeService.Reveal(experience.Visibility,
            QuestExperienceVisibility.Hidden, new HashSet<string> { "heard", "seen" });
        var waypointMarker = experience.Markers.Single(item =>
            item.BindingKind == QuestExperienceMarkerBindingKind.Waypoint);
        var resolvedMarker = QuestExperienceRuntimeService.ResolveMarker(waypointMarker,
            QuestExperienceVisibility.Tracked, true);
        BuildQuestExperienceAdapters([r072Quest]);
        var experienceAdapter = _questExperienceRoot?.GetNodeOrNull<Node>($"Quest_{r072Quest.Id.Value:N}");
        var invalidExperienceRequired = r072Quest.WithExperience(experience with
        { Enabled = false, RequiredAtRuntime = true });
        AssertSmoke(QuestExperienceProfileValidator.Validate(r072Quest).Count == 0 &&
                    Enum.GetValues<QuestExperienceResourceKind>().All(kind =>
                        experience.Resources.Any(item => item.Kind == kind)) &&
                    Enum.GetValues<QuestExperienceMarkerBindingKind>().All(kind =>
                        experience.Markers.Any(item => item.BindingKind == kind)) &&
                    experienceSnapshot.ResourceCount == 5 && experienceSnapshot.StepCount == 1 &&
                    experienceSnapshot.HistoryEventCount == 2 && experienceSnapshot.MarkerCount == 4 &&
                    revealed == QuestExperienceVisibility.Discovered && resolvedMarker.Tracked &&
                    playerHistory.All(item => item.Cause is null) && debugHistory[1].Cause == "dialogue" &&
                    experienceAdapter?.GetNodeOrNull<Node>("TypedResources")?.GetChildCount() == 5 &&
                    experienceAdapter.GetNodeOrNull<Node>("EditableStorySteps")?.GetChildCount() == 1 &&
                    experienceAdapter.GetNodeOrNull<Node>("VisibilityPolicy") is not null &&
                    experienceAdapter.GetNodeOrNull<Node>("TimestampedHistory")?.GetChildCount() == 2 &&
                    experienceAdapter.GetNodeOrNull<Node>("ObjectiveMapMarkers")?.GetChildCount() == 4 &&
                    QuestExperienceProfileValidator.Validate(r071Quest).Count == 0 &&
                    QuestExperienceProfileValidator.Validate(invalidExperienceRequired).Any(item =>
                        item.Code == QuestExperienceProfileValidator.RequiredMissingCode),
            "Quest resources, steps, visibility, timeline, markers, script or engine adapters failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R072_QUEST_EXPERIENCE_PASS " +
            "resources=item-placeable-door-zone-asset-ownership-resolved " +
            "steps=template-version-objectives-transitions-editable-provenance " +
            "visibility=hidden-rumor-discovered-tracked-secret-completed-conditions " +
            "history=timestamp-transition-cause-actor-player-debug " +
            "markers=zone-entity-query-waypoint-visibility-precision-tracking-fallback " +
            "forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic engine=nodes");

        var qualityDependencies = new[]
        {
            new QuestAiDependency(Guid.Parse("c7130000-0000-4000-8000-000000000501"), 1, "smoke:r073", 1,
                QuestAiDependencyKind.State, r072Quest.InitialStateId.ToString()),
            new QuestAiDependency(Guid.Parse("c7130000-0000-4000-8000-000000000502"), 1, "smoke:r073", 2,
                QuestAiDependencyKind.Transition, r072Quest.Transitions[0].Id.ToString()),
            new QuestAiDependency(Guid.Parse("c7130000-0000-4000-8000-000000000503"), 1, "smoke:r073", 3,
                QuestAiDependencyKind.Participant, authoring.Participants[0].Id.ToString("D")),
            new QuestAiDependency(Guid.Parse("c7130000-0000-4000-8000-000000000504"), 1, "smoke:r073", 4,
                QuestAiDependencyKind.Zone, authoring.Wizard.ZoneIds[0]),
            new QuestAiDependency(Guid.Parse("c7130000-0000-4000-8000-000000000505"), 1, "smoke:r073", 5,
                QuestAiDependencyKind.Asset, "asset:quest-generation-model"),
        };
        var aiPlan = QuestQualityRuntimeService.CreateAiPlan(r072Quest,
            new LocalizedText("quest.r073.ai.synopsis", "Restore the vigil before the crypt collapses."),
            qualityDependencies, ["08_QUEST.md#orf-qst-025"], ["style:crypt", "style:human-review"],
            "agent:forge", Guid.Parse("c7130000-0000-4000-8000-000000000506"),
            $"preview:quest:{r072Quest.Id}|states:{r072Quest.States.Count}|participants:{authoring.Participants.Count}");
        aiPlan = QuestQualityRuntimeService.Approve(aiPlan, "author:karen",
            DateTimeOffset.Parse("2026-08-11T12:00:00Z"));
        var quality = new QuestQualityProfile(Guid.Parse("c7130000-0000-4000-8000-000000000507"), 1,
            "smoke:r073", true, true, QuestQualityRuntimeService.GenerateTests(r072Quest, 7300), aiPlan);
        var r073Quest = r072Quest.WithQuality(quality);
        var qualityScript = new QuestJournalScriptService(r073Quest);
        var qualitySnapshot = qualityScript.ReadQuality(qualityScript.Handle);
        var nominalScenario = quality.TestScenarios.Single(item => item.Kind == QuestQualityTestKind.Nominal);
        var nominalRun = QuestQualityRuntimeService.Run(nominalScenario, (step, state) =>
            step.Action == QuestQualityTestActionKind.CompleteObjectives
                ? new Dictionary<string, string>
                {
                    ["quest/state"] = "completed",
                    ["quest/rewards"] = "true",
                }
                : state);
        var rejectedRun = QuestQualityRuntimeService.Run(nominalScenario, (_, state) => state);
        var appliedAiPlan = QuestQualityRuntimeService.MarkApplied(aiPlan);
        BuildQuestQualityAdapters([r073Quest]);
        var qualityAdapter = _questQualityRoot?.GetNodeOrNull<Node>($"Quest_{r073Quest.Id.Value:N}");
        var scenarioAdapter = qualityAdapter?.GetNodeOrNull<Node>("GeneratedTestScenarios")?
            .GetChildOrNull<Node>(0);
        var restoredQuality = QuestQualityProfileJsonSerializer.Deserialize(
            QuestQualityProfileJsonSerializer.Serialize(quality));
        var invalidQualityRequired = r073Quest.WithQuality(quality with
        { Enabled = false, RequiredAtRuntime = true });
        AssertSmoke(QuestQualityProfileValidator.Validate(r073Quest).Count == 0 &&
                    Enum.GetValues<QuestQualityTestKind>().All(kind =>
                        quality.TestScenarios.Any(item => item.Kind == kind)) &&
                    quality.TestScenarios.All(item => item.Fixtures.Count > 0 && item.Steps.Count > 0 &&
                        item.Edges.Count == item.Steps.Count - 1 && item.Assertions.Count > 0) &&
                    qualitySnapshot.ScenarioCount == 5 && qualitySnapshot.StepCount == 11 &&
                    qualitySnapshot.AssertionCount == 6 && qualitySnapshot.HumanApproved &&
                    nominalRun.Passed && !rejectedRun.Passed && appliedAiPlan.Applied &&
                    qualityAdapter?.GetNodeOrNull<Node>("GeneratedTestScenarios")?.GetChildCount() == 5 &&
                    scenarioAdapter?.GetNodeOrNull<Node>("TypedFixtures")?.GetChildCount() > 0 &&
                    scenarioAdapter.GetNodeOrNull<Node>("TypedStepNodes")?.GetChildCount() > 0 &&
                    scenarioAdapter.GetNodeOrNull<Node>("DeterministicEdges")?.GetChildCount() > 0 &&
                    scenarioAdapter.GetNodeOrNull<Node>("TypedAssertions")?.GetChildCount() > 0 &&
                    qualityAdapter.GetNodeOrNull<Node>("GovernedAiPlan")?.GetNodeOrNull<Node>("TypedDependencies")?
                        .GetChildCount() == 5 && restoredQuality.TestScenarios.Count == 5 &&
                    restoredQuality.AiPlan.TransactionId == aiPlan.TransactionId &&
                    QuestQualityProfileValidator.Validate(r072Quest).Count == 0 &&
                    QuestQualityProfileValidator.Validate(invalidQualityRequired).Any(item =>
                        item.Code == QuestQualityProfileValidator.RequiredMissingCode),
            "Quest generated tests, AI plan, script, persistence or engine adapters failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R073_QUEST_QUALITY_PASS " +
            "tests=nominal-failure-repetition-multiplayer-save-resume-seed-fixtures-nodes-edges-assertions-runner " +
            "ai=synopsis-states-participants-dependencies-citations-style-lint-human-approval-transaction-plan-semantic-diff-authorization " +
            "forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic engine=nodes");
    }

    private void RunWaypointAnchorRuntimeSmokeTest()
    {
        var project = _loadedProject ?? throw new InvalidOperationException("Runtime project is not loaded.");
        var area = project.Areas.First();
        var playerSpawn = project.Blueprints.First(item => item.Kind == BlueprintKind.PlayerSpawn);
        static TransformValue At(float x, float y, float z, float yaw)
        {
            var half = yaw * MathF.PI / 360;
            return new(new Vector3Value(x, y, z),
                new QuaternionValue(0, MathF.Sin(half), 0, MathF.Cos(half)), Vector3Value.One);
        }

        var spawnDefinition = new AnchorDefinition(
            Guid.Parse("c7090000-0000-4000-8000-000000000101"), 1, "smoke:r009", "runtime-spawn",
            new LocalizedText("anchor.runtime.spawn", "Départ runtime"), AnchorKind.Spawn, AnchorScope.Area,
            ["player", "spawn"], playerSpawn.Id, null, true);
        var portalDefinition = new AnchorDefinition(
            Guid.Parse("c7090000-0000-4000-8000-000000000102"), 1, "smoke:r009", "runtime-portal",
            new LocalizedText("anchor.runtime.portal", "Portail runtime"), AnchorKind.TransitionDestination,
            AnchorScope.Area, ["portal"], null, null, true);
        var waypointDefinition = new AnchorDefinition(
            Guid.Parse("c7090000-0000-4000-8000-000000000103"), 1, "smoke:r009", "runtime-waypoint",
            new LocalizedText("anchor.runtime.waypoint", "Repère runtime"), AnchorKind.Waypoint,
            AnchorScope.Area, ["quest"], null, null, false);
        var primaryId = Guid.Parse("c7090000-0000-4000-8000-000000000201");
        var fallbackId = Guid.Parse("c7090000-0000-4000-8000-000000000202");
        var portalId = Guid.Parse("c7090000-0000-4000-8000-000000000203");
        var waypointId = Guid.Parse("c7090000-0000-4000-8000-000000000204");
        var catalog = new AnchorCatalog(Guid.Parse("c7090000-0000-4000-8000-000000000301"), 1,
            "smoke:r009", area.Id, true, [spawnDefinition, portalDefinition, waypointDefinition],
            [
                new AnchorInstance(primaryId, 1, "smoke:r009", spawnDefinition.Id, At(0, 0, 0, 180), 1,
                    EntityPropertyOverrides.Empty,
                    new SpawnAnchorProfile("player", new Vector3Value(1.2f, 2, 1.2f), 100, "heroes", 180,
                        fallbackId, true), null),
                new AnchorInstance(fallbackId, 1, "smoke:r009", spawnDefinition.Id, At(2, 0, 0, 90), 2,
                    EntityPropertyOverrides.Empty,
                    new SpawnAnchorProfile("fallback", new Vector3Value(1.2f, 2, 1.2f), 10, "heroes", 90,
                        null, true), null),
                new AnchorInstance(portalId, 1, "smoke:r009", portalDefinition.Id, At(4, 0, 0, 0), 3,
                    EntityPropertyOverrides.Empty, null,
                    new PortalAnchorDestination(area.Id, waypointId, PortalOrientationPolicy.AnchorRotation, 0,
                        PortalVelocityPolicy.RedirectForward, true)),
                new AnchorInstance(waypointId, 1, "smoke:r009", waypointDefinition.Id, At(-2, 0, 1, 45), 4,
                    new EntityPropertyOverrides(1, [new EntityPropertyOverride("quest.marker",
                        EntityPropertyValueKind.String, "ember")]), null, null),
            ]);
        var configuredArea = area.WithNavigation(area.Navigation.WithAnchors(catalog));
        var configuredProject = new ProjectDefinition(project.Id, project.Title, project.Module,
            project.Blueprints, project.Areas.Select(item => item.Id == area.Id ? configuredArea : item).ToArray(),
            project.Dialogues, project.Quests);
        var script = new AnchorScriptService(configuredProject, area.Id);
        var query = script.Query(script.Handle, tag: "spawn", limit: 10);
        var direct = script.ResolveSpawn(script.Handle, "player", "heroes");
        var fallback = script.ResolveSpawn(script.Handle, "player", "heroes", new HashSet<Guid>([primaryId]));
        var travel = script.ResolvePortal(script.Handle, portalId, new Vector3Value(2, 0, 0), 0);
        var optional = area.WithNavigation(area.Navigation.WithAnchors(AnchorCatalog.CreateDefault(area.Id)));
        var invalidRequired = area.WithNavigation(area.Navigation.WithAnchors(new AnchorCatalog(
            Guid.Parse("c7090000-0000-4000-8000-000000000302"), 1, "smoke:r009", area.Id, true, [], [])));
        AssertSmoke(AnchorCatalogValidator.Validate(configuredProject, configuredArea).Count == 0 &&
                    query.Count == 2 && direct.Instance.Id == primaryId &&
                    fallback.Instance.Id == fallbackId && fallback.UsedFallback &&
                    travel.TargetAnchorInstanceId == waypointId && travel.Preload &&
                    Math.Abs(travel.ArrivalYawDegrees - 45) < 0.01f &&
                    AnchorCatalogValidator.Validate(project, optional).Count == 0 &&
                    AnchorCatalogValidator.Validate(project, invalidRequired)
                        .Any(item => item.Code == AnchorCatalogValidator.RequiredContentCode),
            "Waypoint, spawn, portal, optional-empty or required diagnostics failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R009_WAYPOINT_PASS " +
            "waypoint=named-localized spawn=priority-clearance-team-orientation-fallback-navigation " +
            "portal=destination-orientation-velocity-preload forgescript=typed-handle-query " +
            "optional=empty-safe required=diagnostic");
        RunR064WaypointPatrolRuntimeSmokeTest(project, area);
    }

    private void RunR064WaypointPatrolRuntimeSmokeTest(ProjectDefinition project, AreaDefinition area)
    {
        var waypoints = new[]
        {
            new WaypointProfile(Guid.Parse("a0646000-0000-4000-8000-000000000011"), 1, "runtime:r064",
                "Entrée de la crypte", new Vector3Value(-6, 0, -3),
                new("flag", "#ff8844", "quest", true, 32),
                new("Entrée révélée", "quest", -1, "quest", 2,
                    MarkerVisibilityPolicy.HiddenUntilRevealed, MarkerVisibilityScope.Party,
                    true, false, "quest.crypt.found", "pulse", true), RequiredAtRuntime: true),
            new WaypointProfile(Guid.Parse("a0646000-0000-4000-8000-000000000012"), 1, "runtime:r064",
                "Brasero", Vector3Value.Zero, new("diamond", "#83d6ae", "patrol", true, 26), null,
                RequiredAtRuntime: true),
            new WaypointProfile(Guid.Parse("a0646000-0000-4000-8000-000000000013"), 1, "runtime:r064",
                "Porte", new Vector3Value(6, 0, 4), new("arrow", "#a78bfa", "patrol", true, 28), null,
                RequiredAtRuntime: true),
        };
        var points = waypoints.Select((item, index) => new PatrolPoint(item.Id, item.Position,
            index == 1 ? 2 : 0, index * 90, index == 2 ? 3 : 2,
            index == 1 ? "inspect" : null, index == 0 ? "quest.crypt.active" : null,
            index + 1)).ToArray();
        var route = new PatrolRoute(Guid.Parse("a0646000-0000-4000-8000-000000000020"), 1,
            "runtime:r064", PatrolTraversalPolicy.PingPong, points, requiredAtRuntime: true);
        var profile = new WaypointAuthoringProfile(Guid.Parse("a0646000-0000-4000-8000-000000000030"),
            1, "runtime:r064", area.Id, true, waypoints, [route]);
        var configuredArea = area.WithNavigation(area.Navigation.WithWaypoints(profile));
        var configuredProject = new ProjectDefinition(project.Id, project.Title, project.Module,
            project.Blueprints, project.Areas.Select(item => item.Id == area.Id ? configuredArea : item).ToArray(),
            project.Dialogues, project.Quests);
        var script = new WaypointAuthoringScriptService(configuredProject, area.Id);
        var revealed = WaypointRuntimeService.Reveal(profile, WaypointRuntimeState.Empty,
            MarkerVisibilityScope.Party, "quest.crypt.found");
        var marker = WaypointRuntimeService.VisibleMarkers(profile, revealed.State,
            MarkerVisibilityScope.Party).Single();
        _hud?.ShowWaypointMarker(marker);
        var first = WaypointRuntimeService.Advance(profile, revealed.State, route.Id);
        var second = WaypointRuntimeService.Advance(profile, first.State, route.Id);
        var third = WaypointRuntimeService.Advance(profile, second.State, route.Id);
        if (third.Step is { } step) ApplyWaypointPatrolStep(step);
        var denied = WaypointRuntimeService.Advance(profile, third.State, route.Id,
            authorityGranted: false);
        var requiredMissing = new WaypointAuthoringProfile(Guid.NewGuid(), 1, "runtime:r064", area.Id,
            true, [], []);
        AssertSmoke(WaypointAuthoringValidator.Validate(configuredArea).Count == 0 &&
                    script.Query(script.Handle).Count == 3 && script.QueryRoutes(script.Handle).Count == 1 &&
                    marker.Tracked && _hud?.WaypointMarkerText.Contains("Entrée révélée",
                        StringComparison.CurrentCulture) == true &&
                    first.Step?.Cursor.Index == 1 && second.Step?.Cursor.Index == 2 &&
                    third.Step?.Cursor.Index == 1 && _waypointPatrolActor is not null &&
                    denied.Diagnostics.Any(item => item.Code == WaypointRuntimeService.AuthorityDeniedCode) &&
                    WaypointRuntimeService.ValidateRuntime(WaypointAuthoringProfile.CreateDefault(area.Id)).Count == 0 &&
                    WaypointRuntimeService.ValidateRuntime(requiredMissing)
                        .Any(item => item.Code == WaypointRuntimeService.RequiredMissingCode),
            "Waypoint marker HUD, reveal authority, patrol traversal, script handle or runtime fallback failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R064_WAYPOINT_PATROL_PASS " +
            "appearance=icon-color-layer-visible-constant-screen-size map=text-icon-floor-category-precision-policy-tracking-hud " +
            "reveal=player-party-world-event-animation-persistence patrol=ordered-wait-facing-speed-action-branch-weight-engine-actor " +
            "traversal=loop-pingpong-once-random-weighted-scripted forgescript=typed-handles authority=server " +
            "optional=ignored required=diagnostic");
        RunR065WaypointLocatorRuntimeSmokeTest(project, area);
    }

    private void RunR065WaypointLocatorRuntimeSmokeTest(ProjectDefinition project, AreaDefinition area)
    {
        var arrival = new ArrivalBehavior(ArrivalFacingMode.Target, 0, "gate.ashes", "inspect",
            1.5, "open", "waypoint.arrived");
        var waypoints = new[]
        {
            new WaypointProfile(Guid.Parse("a0656000-0000-4000-8000-000000000011"), 1,
                "runtime:r065", "Entrée", new Vector3Value(-4, 0, -2),
                new("flag", "#ff8844", "quest", true, 30), null, RequiredAtRuntime: true,
                Arrival: arrival),
            new WaypointProfile(Guid.Parse("a0656000-0000-4000-8000-000000000012"), 1,
                "runtime:r065", "Brasero", Vector3Value.Zero,
                new("diamond", "#83d6ae", "patrol", true, 26), null, RequiredAtRuntime: true),
            new WaypointProfile(Guid.Parse("a0656000-0000-4000-8000-000000000013"), 1,
                "runtime:r065", "Porte", new Vector3Value(5, 0, 3),
                new("arrow", "#a78bfa", "patrol", true, 28), null, RequiredAtRuntime: true),
        };
        var route = new PatrolRoute(Guid.Parse("a0656000-0000-4000-8000-000000000020"), 1,
            "runtime:r065", PatrolTraversalPolicy.PingPong,
            waypoints.Select((item, index) => new PatrolPoint(item.Id, item.Position, index,
                index * 90, 2, index == 1 ? "inspect" : null, null, index + 1)).ToArray(),
            requiredAtRuntime: true);
        var locators = new[]
        {
            new GenericLocator(Guid.Parse("a0656000-0000-4000-8000-000000000031"), 1,
                "runtime:r065", "Brasero VFX", new(-2, 0, 2), LocatorPurpose.Vfx,
                ["crypt", "fire"],
                new ArrivalBehavior(ArrivalFacingMode.Direction, 90, null, "ignite", .5,
                    "inspect", "locator.arrived"), "vfx:ember-burst", RequiredAtRuntime: true),
            new GenericLocator(Guid.Parse("a0656000-0000-4000-8000-000000000032"), 1,
                "runtime:r065", "Cloche audio", new(2, 0, 2), LocatorPurpose.Audio,
                ["crypt", "ritual"], null, "audio:crypt-bell"),
            new GenericLocator(Guid.Parse("a0656000-0000-4000-8000-000000000033"), 1,
                "runtime:r065", "Objectif", new(4, 0, 1), LocatorPurpose.Objective,
                ["crypt", "quest"], null),
        };
        var stable = new AnchorCollection(Guid.Parse("a0656000-0000-4000-8000-000000000041"),
            1, "runtime:r065", "crypt", locators.Select(item => item.Id).ToArray(), "Crypte",
            RequiredTags: ["crypt"]);
        var nearest = new AnchorCollection(Guid.Parse("a0656000-0000-4000-8000-000000000042"),
            1, "runtime:r065", "crypt", locators.Select(item => item.Id).ToArray(), "Proche",
            RequiredTags: ["crypt"], Selection: LocatorSelectionMode.Nearest,
            Origin: Vector3Value.Zero, Seed: 2026, RequiredAtRuntime: true);
        var visualization = new RouteVisualizationProfile(
            Guid.Parse("a0656000-0000-4000-8000-000000000050"), 1, "runtime:r065",
            true, true, true, true, true, true, true, 1.25, true);
        var profile = new WaypointAuthoringProfile(
            Guid.Parse("a0656000-0000-4000-8000-000000000060"), 1, "runtime:r065", area.Id,
            true, waypoints, [route], locators, [stable, nearest], visualization);
        var configuredArea = area.WithNavigation(area.Navigation.WithWaypoints(profile));
        var configuredProject = new ProjectDefinition(project.Id, project.Title, project.Module,
            project.Blueprints, project.Areas.Select(item => item.Id == area.Id ? configuredArea : item).ToArray(),
            project.Dialogues, project.Quests);
        var script = new WaypointAuthoringScriptService(configuredProject, area.Id);
        var arrived = WaypointRuntimeService.Arrive(profile, waypoints[0].Id);
        var denied = WaypointRuntimeService.Arrive(profile, locators[0].Id, authorityGranted: false);
        var resolvedStable = WaypointRuntimeService.ResolveCollection(profile, stable.Id);
        var resolvedNearest = WaypointRuntimeService.ResolveCollection(profile, nearest.Id);
        var resolvedRandom = WaypointRouteService.ResolveCollection(nearest with
        {
            Selection = LocatorSelectionMode.RandomDeterministic,
            Seed = int.MinValue,
        }, locators);
        var known = waypoints.Select(item => item.Id).Concat(locators.Select(item => item.Id)).ToHashSet();
        var segments = WaypointRouteService.AnalyzeRoute(route,
            waypoints.Take(2).Select(item => item.Id).ToHashSet(), known,
            visualization.CostPerMeter);
        BuildWaypointLocatorsAndRouteDebug(profile);
        AssertSmoke(WaypointAuthoringValidator.Validate(configuredArea).Count == 0 &&
                    script.QueryLocators(script.Handle).Count == 3 &&
                    script.QueryCollections(script.Handle).Count == 2 &&
                    arrived.Arrival is
                    {
                        Facing: ArrivalFacingMode.Target, Animation: "inspect",
                        Interaction: "open", Callback: "waypoint.arrived", AuthorityGranted: true
                    } &&
                    denied.Diagnostics.Any(item => item.Code == WaypointRuntimeService.AuthorityDeniedCode) &&
                    resolvedStable.Count == 3 && resolvedNearest.Count == 1 && resolvedRandom.Count == 1 &&
                    segments.Count == 4 && segments.Any(item => item.Direction < 0) &&
                    segments.All(item => item.Cost > 0 && item.TravelSeconds > 0) &&
                    _waypointLocatorRoot?.GetChildCount() == 3 &&
                    _waypointLocatorRoot.GetChildren().OfType<Node3D>().Any(item =>
                        item.GetChildCount() == 1 && item.GetChild(0) is AudioStreamPlayer3D) &&
                    _waypointRouteDebugRoot?.GetChildCount() == 1 &&
                    WaypointRuntimeService.ValidateRuntime(profile).Count == 0,
            "Waypoint arrival, locators, collections, route diagnostics or runtime adapters failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R065_WAYPOINT_LOCATOR_PASS " +
            "arrival=direction-target-animation-wait-interact-callback-authority-engine " +
            "locator=audio-vfx-spawn-objective-gameplay-purpose-tags-engine-nodes " +
            "collection=query-tags-purpose-stable-nearest-random-deterministic " +
            "route=segments-direction-costs-actions-broken-links-nav-projection-timing-engine-overlay " +
            "forgescript=typed-handles authority=server optional=ignored required=diagnostic");
        RunR066GameplaySimulationRuntimeSmokeTest(project);
    }

    private void RunR066GameplaySimulationRuntimeSmokeTest(ProjectDefinition project)
    {
        var source = GameplaySimulationProfile.CreateDefault(project.Module.Id);
        var kinematic = source.Physics.Bodies[0] with
        {
            Id = Guid.Parse("a0666000-0000-4000-8000-000000000021"),
            Name = "Plateforme cinematique",
            Mode = PhysicsBodyMode.Kinematic,
            RequiredAtRuntime = false,
        };
        var trigger = source.Physics.Bodies[0] with
        {
            Id = Guid.Parse("a0666000-0000-4000-8000-000000000022"),
            Name = "Declencheur gameplay",
            Mode = PhysicsBodyMode.Kinematic,
            IsTrigger = true,
            RequiredAtRuntime = false,
        };
        var profile = source with
        {
            Physics = source.Physics with { Bodies = [.. source.Physics.Bodies, kinematic, trigger] },
        };
        BuildGameplaySimulationAdapters(profile);
        var script = new GameplaySimulationScriptService(project.Module.Id, profile);
        var flying = script.PreviewFlight(script.Flight(script.Handle),
            new(new(0, .5f, 0), Vector3Value.Zero), new(1, 1, 0), .2f,
            new(new(-5, 0, -5), new(5, 10, 5), 0));
        var landing = script.PreviewFlight(script.Flight(script.Handle), flying.State, new(0, -1, 0),
            2, new(new(-5, 0, -5), new(5, 10, 5), 0));
        var initialProjectile = new ProjectileRuntimeState(new(Vector3Value.Zero, new(8, 3, 0)),
            0, true, 0);
        var ballistic = GameplaySimulation.StepProjectile(initialProjectile, profile.Projectiles,
            new(5, 0, 0), .1f, true, (_, _, _) => true);
        var homing = GameplaySimulation.StepProjectile(initialProjectile,
            profile.Projectiles with { Mode = ProjectileMode.Homing }, new(5, 0, 0), .1f, true);
        var hitscan = GameplaySimulation.StepProjectile(initialProjectile,
            profile.Projectiles with { Mode = ProjectileMode.Hitscan }, new(5, 0, 0), .1f, true);
        var denied = GameplaySimulation.StepProjectile(initialProjectile, profile.Projectiles,
            new(5, 0, 0), .1f, false);
        var physics = script.StepPhysics(script.Physics(script.Handle), [], .1f, true);
        var target = new TargetCandidate(Guid.Parse("a0666000-0000-4000-8000-000000000030"),
            new(0, 0, -2), ["targetable"], true);
        var previews = Enum.GetValues<TargetingMode>().Select(mode => GameplaySimulation.PreviewTargeting(
            profile.Targeting with { Mode = mode }, Vector3Value.Zero, new(0, 0, -1), [target])).ToArray();
        var worldProfile = profile.WorldTime with
        {
            LocalTimeScales = new Dictionary<string, float> { ["crypt"] = 2 },
            InitialClock = new WorldClockState(350, 1, false, "gameplay", 8),
        };
        var world = GameplaySimulation.AdvanceWorld(worldProfile, worldProfile.InitialClock, 5, "crypt");
        var saved = new RuntimeWorldClockSave(1, world.Clock.TotalMinutes, world.Clock.Paused,
            world.Clock.Revision, "runtime:r066");
        var restored = JsonSerializer.Deserialize<RuntimeWorldClockSave>(JsonSerializer.Serialize(saved));
        var physicsRoot = _gameplaySimulationRoot?.GetNodeOrNull<Node3D>("PhysicsSceneAdapter");
        var children = physicsRoot?.GetChildren() ?? [];

        AssertSmoke(GameplaySimulationProfileValidator.Validate(profile).Count == 0 &&
                    _gameplaySimulationRoot?.GetNodeOrNull<CharacterBody3D>("FlightCharacterController") is not null &&
                    _gameplaySimulationRoot.GetNodeOrNull<Area3D>("ProjectileAdapter") is not null &&
                    children.OfType<RigidBody3D>().Any() && children.OfType<StaticBody3D>().Any() &&
                    children.OfType<AnimatableBody3D>().Any() && children.OfType<Area3D>().Any() &&
                    children.OfType<Joint3D>().Any() &&
                    _gameplaySimulationRoot.GetNodeOrNull<MeshInstance3D>("TargetingPreview") is not null &&
                    _gameplaySimulationRoot.GetNodeOrNull<Node>("WorldTimeAdapter") is not null &&
                    flying.AppliedLift > 0 && landing.Landed && ballistic.Impacted &&
                    homing.State.Motion.Velocity != initialProjectile.Motion.Velocity && hitscan.Impacted &&
                    !denied.AuthorityGranted && physics.AuthorityGranted &&
                    previews.All(item => item.Geometry.Count > 0) && world.DueSchedules.Count == 1 &&
                    restored?.TotalMinutes == 360 && restored.Revision == 9 &&
                    GameplaySimulationProfileValidator.ValidateRuntime(null, false).Count == 0 &&
                    GameplaySimulationProfileValidator.ValidateRuntime(null, true)
                        .Any(item => item.Code == GameplaySimulationProfileValidator.RequiredMissingCode),
            "Gameplay simulation engine adapters, authority, targeting, world save or runtime fallback failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R066_GAMEPLAY_SIMULATION_PASS " +
            "flight=character-body-volumetric-banking-lift-landing " +
            "projectile=ballistic-homing-hitscan-collision-prediction-vfx-authority " +
            "physics=rigid-static-area-joint-character-bounds-network " +
            "targeting=8-modes-engine-preview-validity-input " +
            "world=clock-calendar-local-scales-pause-domains-schedules-save authority=server " +
            "forgescript=typed-handles optional=ignored required=diagnostic");
        RunR067GameplaySessionRuntimeSmokeTest(project);
    }

    private void RunR067GameplaySessionRuntimeSmokeTest(ProjectDefinition project)
    {
        var profile = GameplaySessionProfile.CreateDefault(project.Module.Id);
        BuildGameplaySessionAdapters(profile);
        var script = new GameplaySessionScriptService(project.Module.Id, profile);
        var save = GameplaySessionServices.WriteSave(profile.Persistence,
            new SaveWriteContext("host", SaveAuthority.Host, false, true), "game:crypt", 4,
            DateTimeOffset.UnixEpoch, new Dictionary<string, string> { ["quest"] = "active" });
        var saveRoundTrip = JsonSerializer.Deserialize<SaveEnvelope>(JsonSerializer.Serialize(save));
        var checkpoint = profile.Checkpoints.Single();
        var respawn = script.Respawn(script.Checkpoints(script.Handle).Single(),
            new RespawnRequest("hero", checkpoint.WaypointId, 10, true, ["hero", "companion"], true), true);
        var deniedRespawn = script.Respawn(script.Checkpoints(script.Handle).Single(),
            new RespawnRequest("hero", checkpoint.WaypointId, 10, true, ["hero"], true), false);
        var shell = script.ReadShell(script.Shell(script.Handle));
        var exploration = profile.ControlLocks.Contexts.Single(item => item.OwnerId == "exploration");
        var dialogue = profile.ControlLocks.Contexts.Single(item => item.OwnerId == "dialogue");
        var cinematic = profile.ControlLocks.Contexts.Single(item => item.OwnerId == "cinematic");
        var stack = GameplaySessionServices.PushControl(
            GameplaySessionServices.PushControl(
                GameplaySessionServices.PushControl([], exploration), dialogue), cinematic);
        var restored = GameplaySessionServices.ReleaseOwner(stack, "cinematic");
        var expected = GameplaySessionServices.HashState("hero:x=1");
        var recording = GameplaySessionServices.Record(profile.Replay,
            Guid.Parse("a0676000-0000-4000-8000-000000000010"),
            [new ReplayFrame(1, "right", "moved", 42, expected)], "runtime:r067");
        var replay = script.ReplayHeadless(script.Replay(script.Handle), recording, _ => "hero:x=2", true);
        var checkpointRoot = _gameplaySessionRoot?.GetNodeOrNull<Node3D>("CheckpointAdapters");
        var shellLayer = _gameplaySessionRoot?.GetNodeOrNull<CanvasLayer>("RuntimeShellAdapter");
        var shellPanels = shellLayer?.GetNodeOrNull<Control>("ShellPanels");

        AssertSmoke(GameplaySessionProfileValidator.Validate(profile).Count == 0 &&
                    _gameplaySessionRoot?.GetNodeOrNull<Node>("PersistenceAdapter") is not null &&
                    checkpointRoot?.GetChildCount() == 1 && checkpointRoot.GetChild(0) is Area3D &&
                    shellPanels?.GetNodeOrNull<Control>("MapPanel") is { Visible: true } &&
                    shellPanels.GetNodeOrNull<Control>("JournalPanel") is { Visible: true } &&
                    shellPanels.GetNodeOrNull<Control>("HudPanel") is { Visible: true } &&
                    _gameplaySessionRoot.GetNodeOrNull<Node>("ControlContextStackAdapter")?.GetChildCount() == 3 &&
                    _gameplaySessionRoot.GetNodeOrNull<Node>("ReplayHeadlessAdapter") is not null &&
                    saveRoundTrip?.Revision == 4 && saveRoundTrip.Profile.Scope == PersistenceScope.Game &&
                    respawn.Succeeded && respawn.RespawnActorIds.Count == 2 &&
                    deniedRespawn.Code == "GAME_RESPAWN_AUTHORITY_REQUIRED" &&
                    shell.VisiblePanels.Count == 3 && shell.InputAgnostic &&
                    restored.Restored?.OwnerId == "dialogue" && !replay.Succeeded &&
                    replay.Divergence?.Tick == 1 && replay.MinimalPackageManifest?.Contains(
                        "privacy=redacted", StringComparison.Ordinal) == true &&
                    GameplaySessionProfileValidator.ValidateRuntime(null, false).Count == 0 &&
                    GameplaySessionProfileValidator.ValidateRuntime(null, true)
                        .Any(item => item.Code == GameplaySessionProfileValidator.RequiredMissingCode),
            "Gameplay session persistence, checkpoint, shell, control stack, replay or engine adapters failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R067_GAMEPLAY_SESSION_PASS " +
            "save=player-game-server-authority-conflict-revision-cloud-privacy-engine " +
            "checkpoint=waypoint-cost-party-corpse-safe-spawn-hooks-area3d-authority " +
            "shell=kit-data-driven-skinnable-scalable-input-agnostic-map-journal-hud-canvas " +
            "control=owner-cursor-camera-actions-stack-restoration-engine " +
            "replay=tick-input-event-seed-hash-headless-divergence-minimal-redacted-package " +
            "forgescript=typed-handles authority=server optional=ignored required=diagnostic");
        RunR068DialogueAuthoringRuntimeSmokeTest(project);
    }

    private void RunR068DialogueAuthoringRuntimeSmokeTest(ProjectDefinition project)
    {
        var dialogue = CreateR068Dialogue("runtime:r068");
        BuildDialogueAuthoringAdapters([dialogue]);
        var evaluator = new R068DialogueConditionEvaluator();
        var context = new DialogueAuthoringContext(
            new Dictionary<string, object> { ["player.name"] = "<Elyra>" },
            new Dictionary<string, string>
            {
                ["role:narrator"] = "entity:narrator",
                ["query:role=guide"] = "entity:guide",
                ["tag:dialogue-companion"] = "entity:companion",
                ["entity:entity:merchant"] = "entity:merchant",
            }, 42);
        RenderedDialogueAuthoringNode Render(DialogueChoiceEvaluationMode mode, int seed = 42,
            string speakerRole = "narrator")
        {
            var profile = dialogue.Authoring! with
            {
                Nodes = dialogue.Authoring.Nodes.Select(item => item.NodeId == dialogue.EntryNodeId
                    ? item with { EvaluationMode = mode, SpeakerRole = speakerRole }
                    : item).ToArray(),
            };
            return DialogueAuthoringRuntimeService.Render(dialogue.WithAuthoring(profile), dialogue.EntryNodeId,
                context with { DeterministicSeed = seed }, evaluator);
        }

        var weighted = Render(DialogueChoiceEvaluationMode.Weighted);
        var randomA = Render(DialogueChoiceEvaluationMode.RandomDeterministic, 19);
        var randomB = Render(DialogueChoiceEvaluationMode.RandomDeterministic, 19);
        var fallbackContext = context with
        {
            ResolvedParticipants = new Dictionary<string, string> { ["role:narrator"] = "entity:fallback" },
        };
        var fallbackProfile = dialogue.Authoring! with
        {
            Nodes = dialogue.Authoring.Nodes.Select(item => item.NodeId == dialogue.EntryNodeId
                ? item with { SpeakerRole = "guide" }
                : item).ToArray(),
        };
        var fallback = DialogueAuthoringRuntimeService.Render(dialogue.WithAuthoring(fallbackProfile),
            dialogue.EntryNodeId, fallbackContext, evaluator);
        var script = new DialogueGraphScriptService(dialogue);
        var snapshot = script.ReadAuthoring(script.Handle);
        var optional = CreateR068Dialogue("runtime:r068", withAuthoring: false);
        var required = new DialogueDefinition(optional.Id, optional.Slug, optional.DisplayName,
            optional.EntryNodeId, optional.Nodes, optional.Presentation, optional.SchemaVersion, optional.Provenance,
            new Dictionary<string, string> { ["authoring.required"] = "true" }, optional.Participants,
            optional.EntryPoints, optional.Edges);
        var updatedProject = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            project.Areas, [.. project.Dialogues.Where(item => item.Id != dialogue.Id), dialogue], project.Quests);
        var json = ProjectJsonSerializer.Serialize(updatedProject);
        var restored = ProjectJsonSerializer.Deserialize(json).Dialogues.Single(item => item.Id == dialogue.Id);
        var dialogueAdapter = _dialogueAuthoringRoot?.GetNodeOrNull<Node>($"Dialogue_{dialogue.Id.Value:N}");
        var participants = dialogueAdapter?.GetNodeOrNull<Node>("ParticipantBindings");
        var nodes = dialogueAdapter?.GetNodeOrNull<Node>("NodeAuthoring");

        AssertSmoke(DialogueValidator.Validate(dialogue).Count == 0 &&
                    weighted.Text.Contains("&lt;Elyra&gt;", StringComparison.Ordinal) &&
                    weighted.AvailableChoices.Count == 1 && weighted.Diagnostics.Count == 0 &&
                    Render(DialogueChoiceEvaluationMode.FirstValid).AvailableChoices.Count == 1 &&
                    Render(DialogueChoiceEvaluationMode.AllValid).AvailableChoices.Count == 4 &&
                    Render(DialogueChoiceEvaluationMode.Manual).AvailableChoices.Count == 4 &&
                    randomA.AvailableChoices.Single().Id == randomB.AvailableChoices.Single().Id &&
                    Render(DialogueChoiceEvaluationMode.FirstValid, speakerRole: "guide").SpeakerEntityId == "entity:guide" &&
                    Render(DialogueChoiceEvaluationMode.FirstValid, speakerRole: "companion").SpeakerEntityId == "entity:companion" &&
                    Render(DialogueChoiceEvaluationMode.FirstValid, speakerRole: "merchant").SpeakerEntityId == "entity:merchant" &&
                    fallback.SpeakerEntityId == "entity:fallback" &&
                    DialogueAuthoringRuntimeService.CompleteTokens(dialogue, "player").Single() == "player.name" &&
                    snapshot.Enabled && snapshot.RequiredAtRuntime && snapshot.ParticipantCount == 4 &&
                    snapshot.NodePolicyCount == 5 && snapshot.TokenCount == 1 && snapshot.WizardEntryCount == 2 &&
                    DialogueAuthoringProfileValidator.Validate(optional).Count == 0 &&
                    DialogueAuthoringProfileValidator.Validate(required).Any(item =>
                        item.Code == DialogueAuthoringProfileValidator.RequiredMissingCode) &&
                    restored.Authoring?.Nodes.Count == 5 && restored.Authoring.Participants.Count == 4 &&
                    participants?.GetChildCount() == 4 && nodes?.GetChildCount() == 5 &&
                    nodes.GetChildren().All(item => item.HasMeta("evaluation_mode")),
            "Dialogue authoring tokens, participants, evaluation, wizard, notes, persistence or engine nodes failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R068_DIALOGUE_AUTHORING_PASS " +
            "tokens=typed-autocomplete-preview-escaped participants=role-query-tag-entity-node-speaker-fallback " +
            "evaluation=first-all-weighted-random-deterministic-manual wizard=visual-typed-source " +
            "notes=intent-emotion-source-status-todo-author forgescript=typed-handles engine=nodes " +
            "persistence=roundtrip optional=ignored required=diagnostic");
        RunR069DialogueExperienceRuntimeSmokeTest(project);
    }

    private void RunR069DialogueExperienceRuntimeSmokeTest(ProjectDefinition project)
    {
        var dialogue = CreateR069Dialogue("runtime:r069");
        BuildDialogueExperienceAdapters([dialogue]);
        var profile = dialogue.Experience!;
        var script = new DialogueGraphScriptService(dialogue);
        var snapshot = script.ReadExperience(script.Handle);
        var completed = DialogueExperienceRuntimeService.Complete(dialogue, "player", DateTimeOffset.UnixEpoch);
        var memory = DialogueExperienceRuntimeService.Write(profile, DialogueMemoryState.Empty,
            profile.MemorySlots.Single().Id, "4");
        var aborted = DialogueExperienceRuntimeService.Abort(dialogue, "player", dialogue.EntryNodeId,
            DialogueAbortReason.CombatStarted, memory, 42, DateTimeOffset.UnixEpoch);
        var hits = DialogueExperienceRuntimeService.Search([dialogue], profile.Search);
        var preview = DialogueExperienceRuntimeService.PreviewReplace([dialogue], profile.Search);
        var review = DialogueExperienceRuntimeService.Review([dialogue], profile.Review);
        var check = DialogueExperienceRuntimeService.ResolveSkillCheck(profile.SkillChecks.Single(), 5, 2, 42);
        var repeated = DialogueExperienceRuntimeService.ResolveSkillCheck(profile.SkillChecks.Single(), 5, 2, 42);
        var optional = CreateR069Dialogue("runtime:r069", withExperience: false);
        var required = new DialogueDefinition(optional.Id, optional.Slug, optional.DisplayName,
            optional.EntryNodeId, optional.Nodes, optional.Presentation, optional.SchemaVersion, optional.Provenance,
            new Dictionary<string, string> { ["kind"] = "conversation", ["experience.required"] = "true" },
            optional.Participants, optional.EntryPoints, optional.Edges, optional.Authoring);
        var updatedProject = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            project.Areas, [.. project.Dialogues.Where(item => item.Id != dialogue.Id), dialogue], project.Quests);
        var json = ProjectJsonSerializer.Serialize(updatedProject);
        var restored = ProjectJsonSerializer.Deserialize(json).Dialogues.Single(item => item.Id == dialogue.Id);
        var dialogueAdapter = _dialogueExperienceRoot?.GetNodeOrNull<Node>($"Dialogue_{dialogue.Id.Value:N}");
        var lifecycle = dialogueAdapter?.GetNodeOrNull<Node>("LifecycleAdapter");
        var searchReview = dialogueAdapter?.GetNodeOrNull<Node>("SearchReviewAdapter");
        var memoryNodes = dialogueAdapter?.GetNodeOrNull<Node>("MemorySlots");
        var checkNodes = dialogueAdapter?.GetNodeOrNull<Node>("SkillChecks");

        AssertSmoke(DialogueValidator.Validate(dialogue).Count == 0 &&
                    completed.Completed && completed.Actions.Count == 1 && !aborted.Completed &&
                    aborted.AbortReason == DialogueAbortReason.CombatStarted && aborted.RolledBack &&
                    aborted.ResumeState?.Seed == 42 && hits.Count >= 1 && preview.Replacements.Count >= 1 &&
                    preview.SemanticDiff.Contains("preview-only:true", StringComparison.Ordinal) &&
                    review.Count >= 1 && profile.Review.Badges.Count == 6 &&
                    DialogueExperienceRuntimeService.Read(profile, memory, profile.MemorySlots.Single().Id) == "4" &&
                    check == repeated && !string.IsNullOrWhiteSpace(check.Explanation) &&
                    snapshot.Enabled && snapshot.RequiredAtRuntime && snapshot.MemorySlotCount == 1 &&
                    snapshot.SkillCheckCount == 1 && script.ReadSkillCheck(script.Handle, dialogue.EntryNodeId) is not null &&
                    DialogueExperienceProfileValidator.Validate(optional).Count == 0 &&
                    DialogueExperienceProfileValidator.Validate(required).Any(item =>
                        item.Code == DialogueExperienceProfileValidator.RequiredMissingCode) &&
                    restored.Experience?.Lifecycle.RollbackOnAbort == true &&
                    restored.Experience.MemorySlots.Count == 1 && restored.Experience.SkillChecks.Count == 1 &&
                    lifecycle is not null && lifecycle.GetMeta("rollback_on_abort").AsBool() &&
                    searchReview is not null && searchReview.HasMeta("badges") &&
                    memoryNodes?.GetChildCount() == 1 && checkNodes?.GetChildCount() == 1 &&
                    checkNodes.GetChild(0).HasMeta("difficulty"),
            "Dialogue lifecycle, search/review, memory, skill check, persistence or engine nodes failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R069_DIALOGUE_EXPERIENCE_PASS " +
            "lifecycle=completed-aborted-reason-rollback-resume-actions " +
            "search=multi-dialogue-safe-regex-speaker-condition-action-preview-only " +
            "review=condition-action-voice-localization-error-link-filter " +
            "memory=dialogue-participant-player-party-world-explicit-duration-typed-engine " +
            "skill=difficulty-opponent-advantage-result-visibility-explanation-deterministic-engine " +
            "forgescript=typed-handles persistence=roundtrip optional=ignored required=diagnostic");
        RunR070DialogueProductionRuntimeSmokeTest(project);
    }

    private void RunR070DialogueProductionRuntimeSmokeTest(ProjectDefinition project)
    {
        var dialogue = CreateR070Dialogue("runtime:r070");
        BuildDialogueProductionAdapters([dialogue]);
        var profile = dialogue.Production!;
        var script = new DialogueGraphScriptService(dialogue);
        var snapshot = script.ReadProduction(script.Handle);
        var contract = profile.Subgraphs.Single();
        var isolated = DialogueProductionRuntimeService.TestSubgraph(profile, contract.Id,
            contract.Tests.Single().Id);
        var impacts = DialogueProductionRuntimeService.AnalyzeImpact(profile,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
            {
                ["quest:crypt-of-ashes"] = ["journal:crypt", "ending:embers"],
            });
        var coverage = DialogueProductionRuntimeService.MeasureCoverage(dialogue);
        var conflict = DialogueProductionRuntimeService.ResolveConflict(profile.Collaboration.Conflicts.Single());
        var aiPreview = DialogueProductionRuntimeService.PreviewAi(profile.AiPlans.Single());
        var aiApplied = DialogueProductionRuntimeService.ApplyAi(profile, aiPreview.PlanId);
        var nodeLock = profile.Collaboration.Locks.Single();
        var optional = CreateR070Dialogue("runtime:r070", withProduction: false);
        var required = new DialogueDefinition(optional.Id, optional.Slug, optional.DisplayName,
            optional.EntryNodeId, optional.Nodes, optional.Presentation, optional.SchemaVersion, optional.Provenance,
            new Dictionary<string, string> { ["kind"] = "conversation", ["production.required"] = "true" },
            optional.Participants, optional.EntryPoints, optional.Edges, optional.Authoring, optional.Experience);
        var updatedProject = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            project.Areas, [.. project.Dialogues.Where(item => item.Id != dialogue.Id), dialogue], project.Quests);
        var restored = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(updatedProject)).Dialogues
            .Single(item => item.Id == dialogue.Id);
        var dialogueAdapter = _dialogueProductionRoot?.GetNodeOrNull<Node>($"Dialogue_{dialogue.Id.Value:N}");
        var subgraphs = dialogueAdapter?.GetNodeOrNull<Node>("SubgraphContracts");
        var consequences = dialogueAdapter?.GetNodeOrNull<Node>("TypedConsequences");
        var coverageNode = dialogueAdapter?.GetNodeOrNull<Node>("Coverage");
        var collaboration = dialogueAdapter?.GetNodeOrNull<Node>("Collaboration");
        var ai = dialogueAdapter?.GetNodeOrNull<Node>("ControlledAiPlans");

        AssertSmoke(DialogueValidator.Validate(dialogue).Count == 0 && profile.RequiredAtRuntime &&
                    isolated.Passed && impacts.Count == 4 &&
                    impacts.Single(item => item.Consequence.Kind == DialogueConsequenceTargetKind.Quest)
                        .AffectedResources.Count == 2 && coverage.Paths.Count == 2 &&
                    coverage.CoveredNodes.Count == 2 && conflict.CanApply && aiPreview.Lint.Count == 0 &&
                    aiApplied.AiPlans.Single().Applied &&
                    DialogueProductionRuntimeService.CanEdit(nodeLock, "author", DateTimeOffset.UnixEpoch) &&
                    !DialogueProductionRuntimeService.CanEdit(nodeLock, "reviewer", DateTimeOffset.UnixEpoch) &&
                    snapshot.Enabled && snapshot.RequiredAtRuntime && snapshot.SubgraphCount == 1 &&
                    snapshot.ConsequenceCount == 4 && script.ReadSubgraph(script.Handle, "embers") is not null &&
                    script.ReadConsequences(script.Handle, dialogue.EntryNodeId).Count == 4 &&
                    DialogueProductionProfileValidator.Validate(optional).Count == 0 &&
                    DialogueProductionProfileValidator.Validate(required).Any(item =>
                        item.Code == DialogueProductionProfileValidator.RequiredMissingCode) &&
                    restored.Production?.Subgraphs.Single().Version == 2 &&
                    restored.Production.Consequences.Count == 4 && restored.Production.AiPlans.Count == 1 &&
                    subgraphs?.GetChildCount() == 1 && consequences?.GetChildCount() == 4 &&
                    coverageNode?.GetMeta("covered_nodes").AsInt32() == 2 &&
                    collaboration?.GetMeta("comments").AsInt32() == 1 && ai?.GetChildCount() == 1 &&
                    ai.GetChild(0).GetMeta("preview_only").AsBool(),
            "Dialogue subgraph, impact, coverage, collaboration, controlled AI, persistence or engine nodes failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R070_DIALOGUE_PRODUCTION_PASS " +
            "subgraph=versioned-input-output-variable-contract-isolated-test-engine " +
            "consequences=quest-faction-item-world-impact-engine coverage=paths-required-state-impossible-test-session-ratio-engine " +
            "collaboration=canonical-node-json-stable-id-revision-lock-comment-semantic-diff-conflict-resolution-engine " +
            "ai=brief-citations-style-preview-lint-authorization-plan-transaction-semantic-diff-explicit-apply-engine " +
            "forgescript=typed-handles persistence=roundtrip optional=ignored required=diagnostic");
    }

    private static DialogueDefinition CreateR070Dialogue(string provenance, bool withProduction = true)
    {
        var entry = DialogueNodeId.Parse("d0706000-0000-4000-8000-000000000001");
        var subgraph = DialogueNodeId.Parse("d0706000-0000-4000-8000-000000000002");
        var end = DialogueNodeId.Parse("d0706000-0000-4000-8000-000000000003");
        var dialogue = new DialogueDefinition(DialogueId.Parse("d0706000-0000-4000-8000-000000000010"),
            "r070-runtime", "R070 runtime", entry,
        [
            new(entry, null, new("dialogue.r070.entry", "Choose an ember path."), choices:
            [
                new(DialogueChoiceId.Parse("d0706000-0000-4000-8000-000000000011"),
                    new("dialogue.r070.subgraph", "Enter the ember memory"), targetNodeId: subgraph),
                new(DialogueChoiceId.Parse("d0706000-0000-4000-8000-000000000012"),
                    new("dialogue.r070.end", "Leave"), targetNodeId: end),
            ], role: DialogueNodeRole.PlayerChoice),
            new(subgraph, null, new("dialogue.r070.embers", "The embers remember."),
                role: DialogueNodeRole.Subgraph, subgraphKey: "embers"),
            new(end, null, new("dialogue.r070.done", "Done."), role: DialogueNodeRole.End),
        ], provenance: provenance);
        if (!withProduction) return dialogue;
        var contractId = Guid.Parse("d0706000-0000-4000-8000-000000000101");
        var input = new DialogueSubgraphParameter(Guid.Parse("d0706000-0000-4000-8000-000000000102"), 1,
            provenance, "trust", DialogueTextTokenType.Integer, DialogueSubgraphParameterDirection.Input, true);
        var output = new DialogueSubgraphParameter(Guid.Parse("d0706000-0000-4000-8000-000000000103"), 1,
            provenance, "accepted", DialogueTextTokenType.Boolean, DialogueSubgraphParameterDirection.Output, true);
        var test = new DialogueSubgraphTestCase(Guid.Parse("d0706000-0000-4000-8000-000000000104"), 1,
            provenance, "trust accepted", new Dictionary<string, string> { ["trust"] = "4" },
            new Dictionary<string, string> { ["accepted"] = "true" });
        var contract = new DialogueSubgraphContract(contractId, 1, provenance, "embers", 2, [input, output],
            ["party:trust"], [test]);
        var invocation = new DialogueSubgraphInvocation(Guid.Parse("d0706000-0000-4000-8000-000000000105"),
            1, provenance, subgraph, contractId,
            new Dictionary<string, string> { ["trust"] = "party:trust" },
            new Dictionary<string, string> { ["accepted"] = "memory:accepted" });
        var consequences = Enum.GetValues<DialogueConsequenceTargetKind>().Select((kind, index) =>
            new DialogueTypedConsequence(Guid.Parse($"d0706000-0000-4000-8000-0000000002{index:00}"), 1,
                provenance, entry, kind, kind switch
                {
                    DialogueConsequenceTargetKind.Quest => "quest:crypt-of-ashes",
                    DialogueConsequenceTargetKind.Faction => "faction:ash-wardens",
                    DialogueConsequenceTargetKind.Item => "item:ember-key",
                    _ => "world:crypt-awakened",
                }, kind == DialogueConsequenceTargetKind.Faction ? "reputation" : "set", "+1",
                $"Impact {kind}")).ToArray();
        var coverage = new DialogueCoveragePolicy(Guid.Parse("d0706000-0000-4000-8000-000000000301"), 1,
            provenance, 64, new Dictionary<string, string> { ["quest:crypt-of-ashes"] = "active" },
            [new(Guid.Parse("d0706000-0000-4000-8000-000000000302"), 1, provenance, "session:embers",
                DialogueCoverageEvidenceKind.Session, [entry, subgraph],
                new Dictionary<string, string> { ["quest:crypt-of-ashes"] = "active" })]);
        var now = DateTimeOffset.UnixEpoch;
        var collaboration = new DialogueCollaborationProfile(
            Guid.Parse("d0706000-0000-4000-8000-000000000401"), 1, provenance,
            [new(Guid.Parse("d0706000-0000-4000-8000-000000000402"), 1, provenance, subgraph, 2,
                "author", "{\"text\":\"ours\"}", now)],
            [new(Guid.Parse("d0706000-0000-4000-8000-000000000403"), 1, provenance, subgraph, "author",
                now.AddMinutes(5))],
            [new(Guid.Parse("d0706000-0000-4000-8000-000000000404"), 1, provenance, subgraph, "reviewer",
                "Clarify impact", false, now)],
            [new(Guid.Parse("d0706000-0000-4000-8000-000000000405"), 1, provenance, subgraph, "text",
                "{\"text\":\"base\"}", "{\"text\":\"ours\"}", "{\"text\":\"theirs\"}",
                DialogueConflictResolution.Manual, "{\"text\":\"merged\"}", true)]);
        var ai = new DialogueAiPlan(Guid.Parse("d0706000-0000-4000-8000-000000000501"), 1, provenance,
            Guid.Parse("d0706000-0000-4000-8000-000000000502"),
            Guid.Parse("d0706000-0000-4000-8000-000000000503"), DialogueAiAuthorizationLevel.AuthorApply,
            "A promise beneath the crypt", ["offer", "choice"], ["style:embers"], ["context#dialogue"],
            "add-subgraph:embers", [], true, false);
        return dialogue.WithProduction(new DialogueProductionProfile(
            Guid.Parse("d0706000-0000-4000-8000-000000000601"), 1, provenance, true, true,
            [contract], [invocation], consequences, coverage, collaboration, [ai]));
    }

    private static DialogueDefinition CreateR068Dialogue(string provenance, bool withAuthoring = true)
    {
        var entry = DialogueNodeId.Parse("d0686000-0000-4000-8000-000000000001");
        var targets = Enumerable.Range(2, 4).Select(index =>
            DialogueNodeId.Parse($"d0686000-0000-4000-8000-00000000000{index}")).ToArray();
        var condition = new DialogueCondition(Guid.Parse("d0686000-0000-4000-8000-000000000020"), 1,
            provenance, DialogueConditionKind.HasItem, "ash-key", "true", false, "on-choice-visible", 1);
        var action = new DialogueAction(Guid.Parse("d0686000-0000-4000-8000-000000000021"), 1,
            provenance, DialogueActionKind.StartQuest, "crypt-of-ashes", "active", true);
        var choices = targets.Select((target, index) => new DialogueChoice(
            DialogueChoiceId.Parse($"d0686000-0000-4000-8000-00000000003{index}"),
            new LocalizedText($"dialogue.r068.choice.{index}", $"Choice {index + 1}"),
            index == 0 ? [condition] : [], [], target, priority: index)).ToArray();
        var dialogue = new DialogueDefinition(DialogueId.Parse("d0686000-0000-4000-8000-000000000010"),
            "r068-runtime", "R068 runtime", entry,
            [
                new(entry, null, new("dialogue.r068.entry", "Welcome, {{player.name}}."), [action], choices,
                    DialogueNodeRole.PlayerChoice),
                .. targets.Select((target, index) => new DialogueNode(target, null,
                    new LocalizedText($"dialogue.r068.end.{index}", $"End {index + 1}"), role: DialogueNodeRole.End)),
            ], provenance: provenance);
        if (!withAuthoring) return dialogue;
        var profile = DialogueAuthoringProfile.CreateDefault(dialogue, provenance);
        var participantRoles = new[] { "narrator", "guide", "companion", "merchant" };
        var kinds = Enum.GetValues<DialogueParticipantBindingKind>();
        var participants = participantRoles.Select((role, index) => new DialogueParticipantBinding(
            Guid.Parse($"d0686000-0000-4000-8000-0000000001{index:00}"), 1, provenance, role, kinds[index],
            kinds[index] switch
            {
                DialogueParticipantBindingKind.Query => "role=guide",
                DialogueParticipantBindingKind.Tag => "dialogue-companion",
                DialogueParticipantBindingKind.Entity => "entity:merchant",
                _ => "narrator",
            }, index == 0 ? null : "narrator")).ToArray();
        var modes = new[]
        {
            DialogueChoiceEvaluationMode.Weighted,
            DialogueChoiceEvaluationMode.FirstValid,
            DialogueChoiceEvaluationMode.AllValid,
            DialogueChoiceEvaluationMode.RandomDeterministic,
            DialogueChoiceEvaluationMode.Manual,
        };
        var wizard = new[]
        {
            new DialogueWizardEntry(Guid.Parse("d0686000-0000-4000-8000-000000000201"), 1, provenance,
                DialogueWizardEntryKind.Condition, condition.Id,
                "builder:condition:HasItem:ash-key:true:false"),
            new DialogueWizardEntry(Guid.Parse("d0686000-0000-4000-8000-000000000202"), 1, provenance,
                DialogueWizardEntryKind.Action, action.Id,
                "builder:action:StartQuest:crypt-of-ashes:active:true"),
        };
        profile = profile with
        {
            RequiredAtRuntime = true,
            Participants = participants,
            Nodes = profile.Nodes.Select((item, index) => item with
            {
                SpeakerRole = participantRoles[index % participantRoles.Length],
                EvaluationMode = modes[index],
                Tokens = index == 0
                    ? [new DialogueTextToken(Guid.Parse("d0686000-0000-4000-8000-000000000203"), 1,
                        provenance, "player.name", DialogueTextTokenType.EntityName, true, null)]
                    : [],
                ChoiceEvaluations = item.ChoiceEvaluations.Select((value, choiceIndex) => value with
                { Weight = choiceIndex + 1 }).ToArray(),
                WizardEntries = index == 0 ? wizard : [],
                Notes = item.Notes with
                {
                    Intent = $"Validate authoring node {index + 1}.",
                    Emotion = index == 0 ? "mysterious" : "neutral",
                    Source = "acceptance:r068",
                    Status = DialogueNodeEditorialStatus.Review,
                    Todos = ["Review voice"],
                    AuthorMention = "@karen",
                },
            }).ToArray(),
        };
        return dialogue.WithAuthoring(profile);
    }

    private static DialogueDefinition CreateR069Dialogue(string provenance, bool withExperience = true)
    {
        var entry = DialogueNodeId.Parse("d0696000-0000-4000-8000-000000000001");
        var success = DialogueNodeId.Parse("d0696000-0000-4000-8000-000000000002");
        var failure = DialogueNodeId.Parse("d0696000-0000-4000-8000-000000000003");
        var dialogue = new DialogueDefinition(DialogueId.Parse("d0696000-0000-4000-8000-000000000010"),
            "r069-runtime", "R069 runtime", entry,
            [
                new(entry, null, new LocalizedText("dialogue.r069.entry", "La braise répond dans les cendres.",
                        [LocalizedTextVariant.Create("dialogue.r069.entry", "en", "The ember answers in the ashes.")],
                        provenance),
                    [new DialogueAction(DialogueActionKind.PlayAudio, "elyra.voice", "play")],
                    [
                        new(DialogueChoiceId.Parse("d0696000-0000-4000-8000-000000000020"),
                            new("dialogue.r069.success", "Convaincre Elyra"),
                            [new(DialogueConditionKind.HasItem, "ash-key", "true")],
                            [new(DialogueActionKind.StartQuest, "crypt-of-ashes", "active")], success),
                        new(DialogueChoiceId.Parse("d0696000-0000-4000-8000-000000000021"),
                            new("dialogue.r069.failure", "Renoncer"), targetNodeId: failure),
                    ], DialogueNodeRole.PlayerChoice),
                new(success, null, new("dialogue.r069.end.success", "La gardienne accepte."),
                    role: DialogueNodeRole.End),
                new(failure, null, new("dialogue.r069.end.failure", "La conversation prend fin."),
                    role: DialogueNodeRole.End),
            ], provenance: provenance);
        if (!withExperience) return dialogue;
        var profile = DialogueExperienceProfile.CreateDefault(dialogue, provenance);
        profile = profile with
        {
            RequiredAtRuntime = true,
            Lifecycle = profile.Lifecycle with
            {
                CompletedActions = [new(DialogueActionKind.SetVariable, "dialogue.completed", "true")],
                AbortedActions = [new(DialogueActionKind.RunScript, "dialogue-aborted.forge", "reason")],
                RollbackOnAbort = true,
                CreateResumeState = true,
            },
            Search = profile.Search with
            {
                Pattern = "braise|cendres",
                UseRegex = true,
                SpeakerRoles = [],
                ConditionKinds = [],
                ActionKinds = [],
                ReplacementPreview = "flamme",
            },
            Review = profile.Review with { ReviewMode = true },
            MemorySlots =
            [new(Guid.Parse("d0696000-0000-4000-8000-000000000101"), 1, provenance,
                DialogueMemoryScope.Player, "player:current", "trust", DialogueTextTokenType.Integer,
                DialogueMemoryDuration.Campaign, "0")],
            SkillChecks =
            [new(Guid.Parse("d0696000-0000-4000-8000-000000000102"), 1, provenance, entry,
                "persuasion", 12, "elyra", true, DialogueSkillCheckVisibility.FullExplanation,
                success, failure)],
        };
        return dialogue.WithExperience(profile);
    }

    private void RunGameplayFoundationRuntimeSmokeTest()
    {
        var profile = _gameplayControls?.Foundation ??
            throw new InvalidOperationException("Gameplay foundation is unavailable.");
        var frame = GameplayFoundationRuntime.Step(profile.GroundedMovement,
            new(new Vector3Value(0, 0, 0), true, false),
            new(new Vector3Value(1, 0, 0), true, true, false), .1f);
        var candidate = new InteractionCandidate(Guid.Parse("c7100000-0000-4000-8000-000000000001"),
            new Vector3Value(0, 0, -2), new HashSet<string>(["door"]), new HashSet<string>(["interact"]));
        var focus = GameplayFoundationRuntime.ResolveFocus(profile.Interaction, Vector3Value.Zero,
            new Vector3Value(0, 0, -1), [candidate], "interact");
        var camera = GameplayFoundationRuntime.ResolveCamera(profile.Cameras);
        var input = GameplayFoundationRuntime.ResolveInput(profile.Input, "interact",
            RuntimeInputDevice.KeyboardMouse, new HashSet<string>(["exploration"]));
        var scripts = new GameplayFoundationScriptService(_gameplayControls!);
        AssertSmoke(GameplayFoundationValidator.Validate(profile).Count == 0 && frame.Jumped && frame.Predicted &&
                    focus is { Ready: true } && camera.Key == "orbit" && input?.Binding.Binding == "E" &&
                    scripts.FindCamera("orbit") is not null && scripts.FindInputAction("interact") is not null &&
                    GameplayFoundationRuntime.ShouldAutosave(profile.Save, null, DateTimeOffset.UnixEpoch),
            "P0 gameplay movement, interaction, camera, input, save or ForgeScript contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R010_GAMEPLAY_PASS " +
            "movement=acceleration-slope-step-jump-crouch-root-motion-prediction " +
            "interaction=focus-verbs-hold targeting=view-cone camera=8-profiles " +
            "input=contexts-devices-glyphs-rebind save=chunks-checksum-autosave-rollback-migrations " +
            "forgescript=typed-handles");
    }

    private void RunBlackboardRulesetPluginRuntimeSmokeTest()
    {
        var variable = new ProjectVariableDefinition(
            ProjectVariableId.From(Guid.Parse("c7110000-0000-4000-8000-000000000001")),
            "server.runtime.ready", ProjectVariableValueKind.Boolean, ProjectVariableScope.Server, "false",
            "R011 runtime proof", "runtime:r011", 1, ProjectVariableReplicationMode.ServerAuthority,
            debugVisible: true, requiredAtRuntime: true);
        var blackboard = new ProjectBlackboardScriptService([variable]);
        var handle = AssertSingle(blackboard.Query("runtime.ready"),
            "Typed blackboard handle could not be resolved.");
        var mutation = blackboard.Set(handle, "true");
        var serverDelta = blackboard.ReplicationDelta(BlackboardReplicationAudience.Server);
        var clientDelta = blackboard.ReplicationDelta(BlackboardReplicationAudience.Client);
        var debug = blackboard.DebugSnapshot();
        var plugins = _loadedProject?.Module.RuntimePolicies.Rules.Plugins ??
            throw new InvalidOperationException("Ruleset plugin catalog is unavailable.");
        var resolved = RulesetPluginRuntimeService.Resolve(plugins, ["content.core"]);
        var optional = RulesetPluginRuntimeService.Resolve(
            new RulesetPluginCatalog(Guid.Parse("c7110000-0000-4000-8000-000000000002"), 1,
                false, false, string.Empty, [], "runtime:r011"), []);
        var requiredMissing = RulesetPluginRuntimeService.Resolve(plugins, []);
        AssertSmoke(mutation.Succeeded && mutation.After?.Value == "true" &&
                    serverDelta.Values.GetValueOrDefault(variable.Key) == "true" &&
                    clientDelta.Values.Count == 0 && debug.Variables.Count == 1 &&
                    resolved is { Succeeded: true, ExtensionPresent: true, Registration: not null } &&
                    resolved.Registration.Attributes.Count > 0 && resolved.Registration.Effects.Count > 0 &&
                    resolved.Registration.Abilities.Count > 0 && resolved.Registration.Inventories.Count > 0 &&
                    resolved.Registration.ProgressionTracks.Count > 0 && resolved.Registration.UiPanels.Count > 0 &&
                    optional is { Succeeded: true, ExtensionPresent: false } &&
                    requiredMissing.DiagnosticCode == RulesetPluginRuntimeService.ContentMissingCode,
            "Blackboard replication/debug or ruleset plugin runtime contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R011_GAMEPLAY_SYSTEMS_PASS " +
            "blackboard=typed-schema-save-replication-debug forgescript=typed-handle-command " +
            "ruleset=attributes-effects-abilities-inventories-progression-ui " +
                 "optional=ignored required=diagnostic");
    }

    private static void RunForgeScriptFoundationRuntimeSmokeTest()
    {
        var contract = new ForgeScriptEventContract(Guid.Parse("c7120000-0000-4000-8000-000000000001"),
            1, "runtime:r012", "creature.damaged", "int", true);
        var source = "void OnDamaged(const int &in e) { say(\"damaged\"); } " +
                     "void AuditDamage(const int &in e) {} void main() { say(\"script-ready\"); }";
        var bindings = new[]
        {
            new ForgeScriptEventBinding(Guid.Parse("c7120000-0000-4000-8000-000000000002"), 1,
                "runtime:r012", "creature.combat", contract.EventName, "OnDamaged", contract.PayloadType,
                100, "is_enraged", ForgeScriptEventAuthority.Server, true),
            new ForgeScriptEventBinding(Guid.Parse("c7120000-0000-4000-8000-000000000003"), 1,
                "runtime:r012", "creature.combat", contract.EventName, "AuditDamage", contract.PayloadType,
                20, null, ForgeScriptEventAuthority.Server, true),
        };
        var document = new ForgeScriptSourceDocument(
            Guid.Parse("c7120000-0000-4000-8000-000000000004"), 1, "runtime:r012", "scripts/combat.as",
            source, "1.0.0", ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], bindings);
        var diagnostics = ForgeScriptSourceValidator.Validate(document, [contract]);
        var resolved = ForgeScriptSourceValidator.ResolveHandlers(document, "creature.combat",
            contract.EventName, ForgeScriptEventAuthority.Server, condition => condition == "is_enraged");
        var missing = ForgeScriptSourceValidator.ValidateRequirements(
            [new(Guid.Parse("c7120000-0000-4000-8000-000000000005"), "scripts/optional.as", false)],
            [document]);
        var runtime = new NativeForgeScriptRuntime();
        var compiled = runtime.Compile(source);
        var executed = compiled.Artifact is null ? null : runtime.Execute(compiled.Artifact,
            new ForgeScriptQuotas(20_000, 1000, 32, 8));
        AssertSmoke(diagnostics.Count == 0 && resolved.Count == 2 && resolved[0].Priority == 100 &&
                    missing.Count == 0 && compiled.Success &&
                    executed?.Status == ForgeScriptExecutionStatus.Completed &&
                    executed.Events.Any(item => item.Kind == "say" && item.Value == "script-ready"),
            "ForgeScript source, typed entry, event dispatch or sandbox contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R012_SCRIPT_FOUNDATION_PASS " +
                 "language=angelscript-2.38-c-like-static source=canonical-as " +
                 "bytecode=cache-source-api-compiler compiler=native-stable-diagnostics " +
                 "events=multi-handler-priority-condition-authority entry=typed-payload " +
                 "sandbox=quota-limited optional=ignored required=diagnostic");
    }

    private static void RunForgeScriptWorldApiRuntimeSmokeTest()
    {
        var heroBlueprint = BlueprintId.From(Guid.Parse("c7130000-0000-4000-8000-000000000001"));
        var guardBlueprint = BlueprintId.From(Guid.Parse("c7130000-0000-4000-8000-000000000002"));
        var hero = new EntityInstance(EntityId.From(Guid.Parse("c7130000-0000-4000-8000-000000000003")),
            heroBlueprint, TransformValue.Identity, ["hero"]);
        var area = new AreaDefinition(AreaId.From(Guid.Parse("c7130000-0000-4000-8000-000000000004")),
            "script-runtime", "Script runtime", [hero]);
        var world = new ForgeScriptWorldCommandService([area],
            new Dictionary<string, BlueprintId>(StringComparer.Ordinal) { ["ash-guard"] = guardBlueprint });
        const string source = """
            void main()
            {
                AreaRef area = current_area();
                EntityRef hero = find_entity(area, "hero");
                Vec3 goal = vec3(6, 0, 4);
                local_set_int(hero, "quest.stage", 2);
                local_set_string(hero, "title", "Warden");
                MovementTaskRef task = move_to(hero, goal, "run");
                set_velocity(hero, vec3(0, 0, 3));
                teleport(hero, area, goal);
                EntityRef guard = spawn_at(area, "ash-guard", vec3(2, 0, 1));
                destroy_entity(guard);
                cancel_movement(task);
                bool safe = regex_match("ash-guard", "^[a-z-]+$");
                say(format_float(vec_length(goal), 2));
            }
            """;
        var runtime = new NativeForgeScriptRuntime();
        var compiled = runtime.Compile(source);
        var execution = compiled.Artifact is null ? null : runtime.Execute(compiled.Artifact,
            new ForgeScriptQuotas(20_000, 1000, 32, 8));
        var command = new ForgeScriptWorldIntentCommand(
            Guid.Parse("c7130000-0000-4000-8000-000000000005"), 0, "runtime:r013",
            execution?.Events ?? [], true);
        var preview = world.Preview(command);
        var applied = world.Apply(command);
        var undone = world.Undo();
        var redone = world.Redo();
        var abusivePattern = new string('a', ForgeScriptCapabilityManifest.MaximumRegexPatternBytes + 1);
        var abusiveCompile = runtime.Compile($"void main() {{ regex_match(\"value\", \"{abusivePattern}\"); }}");
        var abusive = abusiveCompile.Artifact is null ? null : runtime.Execute(abusiveCompile.Artifact);
        var recoveryCompile = runtime.Compile("void main() { say(\"recovered\"); }");
        var recovery = recoveryCompile.Artifact is null ? null : runtime.Execute(recoveryCompile.Artifact);
        var denied = ForgeScriptCapabilityManifest.ValidateSource("void main() { file_open(\"x\"); }");
        AssertSmoke(compiled.Success && execution?.Status == ForgeScriptExecutionStatus.Completed &&
                    preview.Succeeded && applied.Succeeded && undone.Succeeded && redone.Succeeded &&
                    redone.State.Areas.Single().Entities.Single().Transform.Position == new Vector3Value(6, 0, 4) &&
                    redone.State.MovementTasks.Single().Value.Status == ScriptMovementTaskStatus.Cancelled &&
                    abusive?.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                    recovery?.Status == ForgeScriptExecutionStatus.Completed &&
                    recovery.Events.Single().Value == "recovered" && denied.Count == 1,
            "ForgeScript world intents, movement, utilities, variables or sandbox recovery failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R013_SCRIPT_WORLD_API_PASS " +
                 "world=canonical-intents-opaque-handles movement=task-mode-velocity-teleport-completion " +
                 "utilities=vector-quaternion-unicode-format-regex-bounded variables=typed-object-local " +
                 "sandbox=no-file-network-process-native-reflection-engine-dotnet recovery=quota-safe");
    }

    private static void RunForgeScriptSafetyPolicyRuntimeSmokeTest()
    {
        var runtime = new NativeForgeScriptRuntime();
        const string source = "void main() { AssetId asset = resolve_asset(\"ui/marker\"); " +
                              "EffectHandle effect = create_effect(\"ash\"); " +
                              "say(format_int(random_int(1, 1000000))); say(format_int(simulation_tick())); " +
                              "say(format_int(simulation_time_ms())); release_effect(effect); }";
        var compiled = runtime.Compile(source);
        var quotas = new ForgeScriptQuotas(deterministicSeed: 14_014, simulationTick: 42,
            simulationTimeMilliseconds: 7000);
        var first = compiled.Artifact is null ? null : runtime.Execute(compiled.Artifact, quotas);
        var second = compiled.Artifact is null ? null : runtime.Execute(compiled.Artifact, quotas);

        var invalid = runtime.Compile("void main() { AssetId asset = resolve_asset(\"x\"); health(asset); }");
        var invalidResult = invalid.Artifact is null ? null : runtime.Execute(invalid.Artifact);
        var memory = runtime.Compile("void main() { say(\"memory\"); }");
        var memoryResult = memory.Artifact is null ? null : runtime.Execute(memory.Artifact,
            new ForgeScriptQuotas(memoryLimitBytes: 65_536));
        var recovery = runtime.Compile("void main() { say(\"recovered-r014\"); }");
        var recoveryResult = recovery.Artifact is null ? null : runtime.Execute(recovery.Artifact);

        var negotiation = ForgeScriptApiCompatibility.Negotiate("1.0",
            new HashSet<string> { "opaque-handles", "determinism" });
        var history = new ForgeScriptExecutionPolicyHistory(ForgeScriptExecutionPolicy.Default);
        var command = new ForgeScriptExecutionPolicyCommand(Guid.NewGuid(), 0,
            ForgeScriptExecutionPolicy.Default with { DeterministicSeed = 14_014 }, "runtime:r014");
        var applied = history.Apply(command);
        var undone = history.Undo();
        var redone = history.Redo();

        AssertSmoke(compiled.Success && first?.Status == ForgeScriptExecutionStatus.Completed &&
                    second?.Status == ForgeScriptExecutionStatus.Completed && first.Events.SequenceEqual(second.Events) &&
                    first.Telemetry is { Instructions: > 0, PeakMemoryBytes: > 0, DeterministicSeed: 14_014 } &&
                    first.Events.Any(item => item.Kind == "say" && item.Value == "42") &&
                    first.Events.Any(item => item.Kind == "say" && item.Value == "7000") &&
                    invalidResult?.Status == ForgeScriptExecutionStatus.Failed &&
                    invalidResult.Diagnostics.Any(item => item.Message.Contains("handle.type_invalid", StringComparison.Ordinal)) &&
                    memoryResult?.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                    recoveryResult?.Status == ForgeScriptExecutionStatus.Completed &&
                    recoveryResult.Events.Single().Value == "recovered-r014" && negotiation.Succeeded &&
                    applied.Succeeded && undone.Succeeded && redone.Succeeded,
            "ForgeScript typed handles, budgets, deterministic execution or API negotiation failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R014_SCRIPT_SAFETY_POLICY_PASS " +
                 "handles=typed-generational-validated budgets=handler-tick-module-memory-timeout-cancel-recovery " +
                 "telemetry=instructions-memory-time determinism=seed-clock-server " +
                 "version=semver-negotiation-deprecation-migration policy=preview-apply-undo-redo");
    }

    private static void RunForgeScriptModulePermissionsRuntimeSmokeTest()
    {
        var moduleId = Guid.Parse("15000000-0000-4000-8000-000000000015");
        var policy = ForgeScriptModulePermissionPolicy.CreateDefault(moduleId);
        var request = new ForgeScriptModuleAuthorizationRequest(moduleId, "core", "module.start", "main",
            new HashSet<string>(["world"], StringComparer.Ordinal), ForgeScriptEventAuthority.Server,
            true, false, true);
        var runtime = new NativeForgeScriptRuntime();
        var service = new ForgeScriptModuleExecutionService(runtime);
        var nominal = service.Execute(policy, request, "void main() { say(\"r015-runtime\"); }");
        var untrusted = service.Execute(policy, request with { SignerTrusted = false },
            "void main() { say(\"denied\"); }");
        var optional = ForgeScriptModuleAuthorizationService.Authorize(null,
            request with { RequiredAtRuntime = false });
        var abusive = service.Execute(policy, request,
            "void main() { while (true) { int value = 1; } }", new ForgeScriptQuotas(instructionLimit: 10));
        var recovered = service.Execute(policy, request,
            "void main() { say(\"r015-recovered\"); }");
        AssertSmoke(nominal.Execution?.Status == ForgeScriptExecutionStatus.Completed &&
                    untrusted.Authorization.Diagnostics.SingleOrDefault()?.Code ==
                    "script.permission.signature_untrusted" && optional.Succeeded && !optional.ShouldExecute &&
                    abusive.Execution?.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                    recovered.Execution?.Status == ForgeScriptExecutionStatus.Completed,
            "ForgeScript module permissions, authority, signature or recovery failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R015_SCRIPT_MODULE_PERMISSIONS_PASS " +
                 "policy=module-kit-event-intersection deny=undeclared authority=server " +
                 "signature=trusted-production optional=ignored required=diagnostic " +
                 "sandbox=quota-abort-recovery");
    }

    private void RunForgeScriptP1GameplayRuntimeSmokeTest()
    {
        const string source = """
            void main()
            {
                EntityRef hero = spawn("hero");
                EntityRef target = spawn("warden");
                ScriptTaskRef task = action_enqueue(hero, "attack", 100);
                bool hit = combat_attack(hero, target, "ash-slash", 8.0f, 97);
                EffectHandle burning = effect_apply(hero, target, "burning", 2.0f, 10.0f);
                action_complete(task, "hit");
                effect_dispel(target, "fire");
                release_effect(burning);
            }
            """;
        var document = new ForgeScriptSourceDocument(
            Guid.Parse("c7970000-0000-4000-8000-000000000101"), 1, "runtime:r097",
            "scripts/r097-runtime.as", source, "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], []);
        var editor = new ForgeScriptEditorContext(
            ForgeScriptApiCatalog.Registrations.Select(item => new ForgeScriptSymbol(
                item.Name, "function", item.Signature.Split(' ', 2)[0], item.Summary, item.Signature,
                item.Availability.Order(StringComparer.Ordinal).ToArray())).ToArray(),
            new Dictionary<string, string> { ["main"] = "void main() {\n}\n" },
            new HashSet<string> { "bool", "float", "if", "int", "return", "string", "void" });
        var adapter = new ForgeScriptP1RuntimeAdapter();
        var resolution = adapter.Resolve(document, true);
        var handle = resolution.Handle ?? throw new InvalidOperationException("R097 source handle is unavailable.");
        var runtime = adapter.Build(handle, document, editor,
            new RpgScriptCombatState(new Dictionary<string, double> { ["warden"] = 40 }, [], 0,
                new Dictionary<string, double> { ["warden"] = 2 }));
        var analysis = adapter.Analyze(handle, runtime,
            source.IndexOf("combat_attack", StringComparison.Ordinal) + "combat_attack".Length);
        var condition = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Compare, ">", "bool",
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Variable, "health", "int"),
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, "0", "int"));
        var action = ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Call, "say", "void",
            ForgeScriptAuthoringService.Build(ForgeScriptAstKind.Literal, "\"ready\"", "string"));
        var wizard = adapter.BuildWizard(handle, runtime, new(
            Guid.Parse("c7970000-0000-4000-8000-000000000102"), 1, "runtime:r097",
            "NotifyReady", [condition], [action]));

        var at = DateTimeOffset.UnixEpoch;
        var task = new ScriptTask(Guid.Parse("c7970000-0000-4000-8000-000000000103"), "hero", 100,
            ScriptNetworkAuthority.Server, "attack", new Dictionary<string, object> { ["target"] = "warden" },
            ScriptTaskStatus.Queued, at, null, null);
        runtime = adapter.Enqueue(handle, runtime, task, ScriptNetworkAuthority.Server);
        runtime = adapter.StartNext(handle, runtime, at.AddSeconds(1));
        runtime = adapter.Complete(handle, runtime, task.Id, "hit", at.AddSeconds(2));
        var attacked = adapter.Attack(handle, runtime,
            new RpgAttackContext(Guid.Parse("c7970000-0000-4000-8000-000000000104"), "hero", "warden",
                "ash-slash", 1, 0, 97, "server", "runtime:r097"),
            new RpgDamageSpec(8, "fire", new HashSet<string> { "burning" }, false, 0.5));
        runtime = attacked.Runtime;
        var definition = new RpgEffectDefinition("effect:burning", 1, "runtime:r097", "damage-over-time",
            RpgEffectStacking.RefreshDuration, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2), 2,
            new HashSet<string> { "fire" }, true);
        var effected = adapter.ApplyEffect(handle, runtime, definition,
            new RpgEffectSpec(Guid.Parse("c7970000-0000-4000-8000-000000000105"), definition.Id,
                "hero", "warden", 1, null, at));
        runtime = effected.Runtime;
        var execution = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(20_000, 1000, 32, 8));
        var serializedEffects = RpgScriptGameplayService.SerializeEffects(runtime.Effects, at.AddSeconds(2));
        var restoredEffects = RpgScriptGameplayService.DeserializeEffects(serializedEffects);
        var history = new ForgeScriptSourceHistory(document, new NativeForgeScriptRuntime(), []);
        var preview = history.Preview(new(Guid.Parse("c7970000-0000-4000-8000-000000000106"), 0,
            document with { Provenance = "runtime:r097-reviewed" }, "runtime:r097"));
        var applied = history.Apply(new(Guid.Parse("c7970000-0000-4000-8000-000000000107"), 0,
            document with { Provenance = "runtime:r097-reviewed" }, "runtime:r097"));
        var undone = history.Undo();
        var redone = history.Redo();
        var optional = adapter.Resolve(null, false);
        var required = adapter.Resolve(null, true);

        var root = new Node3D { Name = "ForgeScriptP1_R097" };
        var panel = new ForgeScriptP1Panel();
        panel.Configure(runtime, analysis, wizard, attacked.Result, effected.Result, execution);
        root.AddChild(panel);
        AddChild(root);

        AssertSmoke(resolution.Available && analysis.Completions.Any(item => item.Label == "combat_attack") &&
                    analysis.SyntaxSpans.Count > 0 && wizard.Succeeded &&
                    runtime.Tasks.Tasks.Single().Status == ScriptTaskStatus.Completed &&
                    attacked.Result is { Committed: true, AppliedDamage: 7 } &&
                    runtime.Combat.Health["warden"] == 33 && runtime.Effects.Count == 1 &&
                    restoredEffects.Effects.Single().Spec.InstanceId == runtime.Effects.Single().Spec.InstanceId &&
                    execution.Status == ForgeScriptExecutionStatus.Completed &&
                    execution.Events.Any(item => item.Kind == "action.queued") &&
                    execution.Events.Any(item => item.Kind == "combat.attack") &&
                    execution.Events.Any(item => item.Kind == "effect.applied") &&
                    preview.Succeeded && applied.Succeeded && undone.Succeeded && redone.Succeeded &&
                    redone.Document.Provenance == "runtime:r097-reviewed" && optional.OptionalFallback &&
                    required.DiagnosticCode == ForgeScriptP1RuntimeAdapter.RequiredMissingCode &&
                    panel.GetNodeOrNull<ItemList>("ForgeScriptP1Layout/AuthoringList") is { ItemCount: 4 } &&
                    panel.GetNodeOrNull<ItemList>("ForgeScriptP1Layout/GameplayList") is { ItemCount: 4 } &&
                    panel.GetNodeOrNull<Button>("ForgeScriptP1Layout/ForgeScriptP1Actions/AttackButton") is
                    { Disabled: false },
            "ForgeScript P1 editor, wizard, actions, combat, effects or accessible runtime panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R097_SCRIPT_AUTHORING_GAMEPLAY_PASS " +
                 "editor=syntax-highlighting-completion-signature-docs-snippets-symbols-formatting " +
                 "wizard=typed-ast-reviewed-source " +
                 "actions=queue-priority-cancel-complete-authority " +
                 "combat=damage-attack-context-effects-attribution-seed-transaction-rollback " +
                 "effects=definition-spec-six-stacking-duration-period-source-dispel-serialization " +
                 "ui=visible-empty-error-console-clickable command=preview-apply-reject " +
                 "history=undo-redo persistence=roundtrip forgescript=versioned-native-apis " +
                 "mcp=get-api-compile-diagnostics-tests-sandbox " +
                 "runtime=optional-safe-required-diagnostic rpgkit=services-reused " +
                 "engine=typed-intents-accessible-panel");
    }

    private void RunForgeScriptP1WorldApiRuntimeSmokeTest()
    {
        const string source = """
            void main()
            {
                EntityRef chest = spawn("chest");
                EntityRef hero = spawn("hero");
                EntityRef warden = spawn("warden");
                InventoryResultRef loot = inventory_transfer(chest, hero, "crypt-key", 1);
                QuestResultRef quest = quest_transition("crypt", "party:1", "active", "completed", "find-key", 1, "found");
                DialogueSessionRef talk = dialogue_start("graph:intro", warden, hero);
                dialogue_complete(talk, "accepted");
                FactionResultRef faction = faction_reputation(hero, "wardens", 25, true, "module", "rescued");
                PerceptionResultRef seen = perception_query(warden, hero, "sight", 2.0f, 10.0f, true, 0.8f, 98);
            }
            """;
        var document = new ForgeScriptSourceDocument(
            Guid.Parse("c7980000-0000-4000-8000-000000000101"), 1, "runtime:r098",
            "scripts/r098-runtime.as", source, "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], []);
        var adapter = new ForgeScriptWorldApiRuntimeAdapter();
        var resolution = adapter.Resolve(document, true);
        var handle = resolution.Handle ?? throw new InvalidOperationException("R098 source handle is unavailable.");
        var inventory = new RpgInventoryState(
            new Dictionary<string, IReadOnlyDictionary<string, int>>
            {
                ["chest"] = new Dictionary<string, int> { ["crypt-key"] = 1 },
                ["hero"] = new Dictionary<string, int>(),
            },
            new Dictionary<string, int> { ["hero"] = 8 }, 0);
        IReadOnlyDictionary<string, RpgQuestState> quests = new Dictionary<string, RpgQuestState>
        {
            ["crypt"] = new("crypt", "party:1", "active",
                new Dictionary<string, int> { ["find-key"] = 0 }, [], 0),
        };
        var factions = new RpgFactionState(
            new Dictionary<string, IReadOnlySet<string>>(), new Dictionary<string, int>(), 0);
        var runtime = adapter.Build(handle, document, inventory, quests, factions);

        var transfer = adapter.Transfer(handle, runtime,
            new RpgInventoryTransaction(Guid.Parse("c7980000-0000-4000-8000-000000000102"),
                "server", "loot", "chest", "hero", "crypt-key", 1));
        runtime = transfer.Runtime;
        var inventoryQuery = RpgScriptWorldService.QueryInventory(runtime.Inventory, "hero", "crypt-key");
        runtime = adapter.TransitionQuest(handle, runtime, "crypt",
            new RpgQuestTransition(Guid.Parse("c7980000-0000-4000-8000-000000000103"),
                "server", "active", "completed", "find-key", 1, "found"));
        var dialogueStarted = adapter.StartDialogue(handle, runtime,
            new RpgDialogueRequest(Guid.Parse("c7980000-0000-4000-8000-000000000104"),
                "server", "graph:intro",
                new Dictionary<string, string> { ["speaker"] = "warden", ["listener"] = "hero" },
                new Dictionary<string, string> { ["quest"] = "crypt" }));
        runtime = adapter.CompleteDialogue(handle, dialogueStarted.Runtime, dialogueStarted.Session.Id, "accepted");
        runtime = adapter.ApplyFaction(handle, runtime,
            new RpgFactionTransaction(Guid.Parse("c7980000-0000-4000-8000-000000000105"),
                "server", "hero", "wardens", 25, true, "module", "rescued"));
        var factionQuery = RpgScriptWorldService.QueryFaction(runtime.Factions, "hero", "wardens", "module");
        var perception = adapter.Perceive(handle, runtime,
            new RpgPerceptionQuery("warden", "hero", "sight", 2, 10, true, .8, 98));
        var execution = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(30_000, 1000, 32, 8));

        var history = new ForgeScriptSourceHistory(document, new NativeForgeScriptRuntime(), []);
        var preview = history.Preview(new(Guid.Parse("c7980000-0000-4000-8000-000000000106"), 0,
            document with { Provenance = "runtime:r098-reviewed" }, "runtime:r098"));
        var applied = history.Apply(new(Guid.Parse("c7980000-0000-4000-8000-000000000107"), 0,
            document with { Provenance = "runtime:r098-reviewed" }, "runtime:r098"));
        var undone = history.Undo();
        var redone = history.Redo();
        var optional = adapter.Resolve(null, false);
        var required = adapter.Resolve(null, true);

        var root = new Node3D { Name = "ForgeScriptWorldApis_R098" };
        var panel = new ForgeScriptWorldApiPanel();
        panel.ShowEmpty();
        panel.ShowError("FORGESCRIPT_WORLD_PREVIEW_REJECTED");
        panel.Configure(runtime, transfer.Result, inventoryQuery, runtime.Quests["crypt"],
            runtime.Dialogues[dialogueStarted.Session.Id], factionQuery, perception, execution);
        root.AddChild(panel);
        AddChild(root);

        AssertSmoke(resolution.Available && transfer.Result.Committed &&
                    transfer.Result.Audit.Count >= 3 && transfer.Result.SemanticDiff.Contains("crypt-key") &&
                    inventoryQuery is { Quantity: 1, UsedCapacity: 1, MaximumCapacity: 8, Revision: 1 } &&
                    runtime.Quests["crypt"] is { Status: "completed", Revision: 1 } &&
                    runtime.Quests["crypt"].Objectives["find-key"] == 1 &&
                    runtime.Quests["crypt"].History.Single().Contains("scope=party:1") &&
                    runtime.Dialogues[dialogueStarted.Session.Id] is { State: "completed" } completedDialogue &&
                    completedDialogue.Outcomes.Single() == "accepted" &&
                    factionQuery is { Member: true, Relationship: 25, Revision: 1 } &&
                    perception.Perceived && perception.Stimuli.Contains("sense:sight") &&
                    perception.Trace.Contains("seed:98") &&
                    execution.Status == ForgeScriptExecutionStatus.Completed &&
                    execution.Events.Any(item => item.Kind == "inventory.transfer") &&
                    execution.Events.Any(item => item.Kind == "quest.transition") &&
                    execution.Events.Any(item => item.Kind == "dialogue.started") &&
                    execution.Events.Any(item => item.Kind == "dialogue.completed") &&
                    execution.Events.Any(item => item.Kind == "faction.reputation") &&
                    execution.Events.Any(item => item.Kind == "perception.query") &&
                    preview.Succeeded && applied.Succeeded && undone.Succeeded && redone.Succeeded &&
                    redone.Document.Provenance == "runtime:r098-reviewed" && optional.OptionalFallback &&
                    required.DiagnosticCode == ForgeScriptWorldApiRuntimeAdapter.RequiredMissingCode &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("ForgeScriptWorldApiLayout/WorldApiList") is { ItemCount: 5 } &&
                    panel.GetNodeOrNull<Button>("ForgeScriptWorldApiLayout/WorldApiActions/TransferButton") is
                    { Disabled: false, FocusMode: Control.FocusModeEnum.All },
            "ForgeScript P1 inventory, quest, dialogue, faction, perception or accessible runtime panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R098_SCRIPT_WORLD_APIS_PASS " +
                 "inventory=transaction-ownership-query-capacity-rollback-audit-revision " +
                 "quest=transition-objectives-history-cause-instance-scope-revision " +
                 "dialogue=graph-participants-variables-complete-interrupt-safe " +
                 "faction=membership-reputation-cause-scope-authority-bounds-revision " +
                 "perception=sense-los-range-stimulus-confidence-seed-trace " +
                 "ui=visible-empty-error-console-clickable command=preview-apply-reject " +
                 "history=undo-redo persistence=roundtrip forgescript=versioned-native-handles " +
                 "mcp=get-api-compile-diagnostics-tests-sandbox " +
                 "runtime=optional-safe-required-diagnostic rpgkit=world-service-reused " +
                 "engine=typed-world-intents-accessible-panel");
    }

    private void RunForgeScriptP1RuntimeToolsRuntimeSmokeTest()
    {
        const string source = """
            void main()
            {
                EntityRef hero = spawn("hero");
                AudioEmitterRef music = audio_emitter("music:dawn");
                audio_start(music);
                audio_snapshot("combat");
                audio_listener(hero, "player");
                audio_complete(music, "finished");
                WorldClockRef clock = world_clock();
                int64 calendar = world_calendar_ms(clock);
                TimerRef timer = timer_schedule(1000, 5000, "OnTick", "daily");
                timer_cancel(timer);
                deterministic_delay(250, "OnReady");
                event_subscribe("forge.damage", 1, "OnDamage");
                CustomEventRef evt = custom_event("forge.damage", 1, "amount=4");
                event_publish(evt);
                HotReloadRef reload = hot_reload_request("module", 2, "sha256:new");
                DebugSessionRef debug = debug_break("session:1", "local-author");
                debug_step(debug, "over");
            }
            """;
        var document = new ForgeScriptSourceDocument(
            Guid.Parse("c7990000-0000-4000-8000-000000000101"), 1, "runtime:r099",
            "scripts/r099-runtime.as", source, "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], []);
        var adapter = new ForgeScriptRuntimeToolAdapter();
        var resolution = adapter.Resolve(document, true);
        var handle = resolution.Handle ?? throw new InvalidOperationException("R099 source handle is unavailable.");
        var activeModule = new ScriptHotReloadModule("module", 1,
            new Dictionary<string, string> { ["score"] = "int" },
            new HashSet<string> { "dialogue" }, "sha256:old");
        var runtime = adapter.Build(handle, document,
            new ScriptAudioState(new Dictionary<string, ScriptAudioCue>(), "camera", "default", []),
            new ScriptWorldClock(DateTimeOffset.UnixEpoch, TimeSpan.Zero, 2, 0), activeModule);
        runtime = adapter.Play(handle, runtime,
            new ScriptAudioCue("music:dawn", "music", .8, TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2), true, "exploration"));
        runtime = adapter.SetListener(handle, runtime, "hero", "combat", "player");
        runtime = adapter.Schedule(handle, runtime,
            Guid.Parse("c7990000-0000-4000-8000-000000000102"), TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(3), "OnTick", "daily");
        runtime = adapter.Advance(handle, runtime, TimeSpan.FromSeconds(5));
        var deliveries = adapter.Publish(handle, runtime,
            new ScriptCustomEventSchema("forge.damage", 1,
                new Dictionary<string, Type> { ["amount"] = typeof(int), ["target"] = typeof(Guid) }),
            ["audio", "quest"], new ScriptCustomEvent("forge.damage", 1,
                new Dictionary<string, object?>
                {
                    ["amount"] = 4,
                    ["target"] = Guid.Parse("c7990000-0000-4000-8000-000000000103"),
                }, "server", Guid.Parse("c7990000-0000-4000-8000-000000000104")));
        var candidate = activeModule with
        {
            Version = 2,
            SourceHash = "sha256:new",
            Dependencies = new HashSet<string> { "quest" },
        };
        var rejectedReload = adapter.Reload(handle, runtime, candidate, true, (_, _) => false);
        var acceptedReload = adapter.Reload(handle, runtime, candidate, true, (_, _) => true);
        runtime = acceptedReload.State;
        runtime = adapter.Break(handle, runtime, "debug:r099", "server-admin",
            [new ScriptDebugFrame("OnDamage", "scripts/r099-runtime.as", 42,
                new Dictionary<string, object?> { ["amount"] = 4 })],
            new Dictionary<string, object?> { ["hero.hp"] = 36 },
            new Dictionary<string, string> { ["hero"] = "entity:hero" }, ["forge.damage"],
            new HashSet<string> { "scripts/r099-runtime.as:42" }, true);
        runtime = adapter.Step(handle, runtime, "over");
        var execution = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(30_000, 1000, 32, 8));

        var abusiveDocument = document with
        {
            Id = Guid.Parse("c7990000-0000-4000-8000-000000000105"),
            Source = "void main() { while (true) { int value = 1; } }",
            Provenance = "runtime:r099-abuse",
        };
        var abusiveHandle = adapter.Resolve(abusiveDocument, true).Handle!;
        var abusiveRuntime = adapter.Build(abusiveHandle, abusiveDocument, runtime.Audio, runtime.Clock,
            runtime.ActiveModule);
        var abusive = adapter.Execute(abusiveHandle, abusiveRuntime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(10, 1000, 32, 8));
        var recovered = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(30_000, 1000, 32, 8));
        var history = new ForgeScriptSourceHistory(document, new NativeForgeScriptRuntime(), []);
        var preview = history.Preview(new(Guid.Parse("c7990000-0000-4000-8000-000000000106"), 0,
            document with { Provenance = "runtime:r099-reviewed" }, "runtime:r099"));
        var applied = history.Apply(new(Guid.Parse("c7990000-0000-4000-8000-000000000107"), 0,
            document with { Provenance = "runtime:r099-reviewed" }, "runtime:r099"));
        var undone = history.Undo();
        var redone = history.Redo();
        var optional = adapter.Resolve(null, false);
        var required = adapter.Resolve(null, true);

        var root = new Node3D { Name = "ForgeScriptRuntimeTools_R099" };
        var panel = new ForgeScriptRuntimeToolsPanel();
        panel.ShowEmpty();
        panel.ShowError("FORGESCRIPT_RUNTIME_TOOL_PREVIEW_REJECTED");
        panel.Configure(runtime, deliveries, acceptedReload.Result, execution);
        root.AddChild(panel);
        AddChild(root);

        AssertSmoke(resolution.Available && runtime.Audio.Playing.ContainsKey("music:dawn") &&
                    runtime.Audio is { ListenerPolicy: "player", Revision: 2 } &&
                    runtime.Clock is { SimulationTime: var simulationTime, Revision: 1 } &&
                    simulationTime == TimeSpan.FromSeconds(10) &&
                    ScriptRuntimeTools.Due(runtime.Clock, runtime.Timers).Count == 1 &&
                    ScriptRuntimeTools.CalendarTime(runtime.Clock) == DateTimeOffset.UnixEpoch.AddSeconds(10) &&
                    deliveries.Select(item => item.Subscriber).SequenceEqual(["audio", "quest"]) &&
                    deliveries.All(item => item.Trace.Contains("forge.damage@1")) &&
                    rejectedReload.Result.Strategy == "restart" && ReferenceEquals(rejectedReload.State, runtime) == false &&
                    acceptedReload.Result.Applied && runtime.ActiveModule.Version == 2 &&
                    runtime.Debugger is { Remote: true, State: "paused", Revision: 1 } &&
                    runtime.Debugger.Breakpoints.Contains("scripts/r099-runtime.as:42") &&
                    ScriptRuntimeTools.InspectEntity(runtime.Debugger, "hero") == "entity:hero" &&
                    execution.Status == ForgeScriptExecutionStatus.Completed &&
                    execution.Events.Any(item => item.Kind == "audio.started") &&
                    execution.Events.Any(item => item.Kind == "time.timer.scheduled") &&
                    execution.Events.Any(item => item.Kind == "event.published") &&
                    execution.Events.Any(item => item.Kind == "hot-reload.requested") &&
                    execution.Events.Any(item => item.Kind == "debug.step") &&
                    abusive.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                    recovered.Status == ForgeScriptExecutionStatus.Completed &&
                    preview.Succeeded && applied.Succeeded && undone.Succeeded && redone.Succeeded &&
                    redone.Document.Provenance == "runtime:r099-reviewed" && optional.OptionalFallback &&
                    required.DiagnosticCode == ForgeScriptRuntimeToolAdapter.RequiredMissingCode &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("ForgeScriptRuntimeToolsLayout/RuntimeToolsList") is
                    { ItemCount: 5 } &&
                    panel.GetNodeOrNull<Button>("ForgeScriptRuntimeToolsLayout/RuntimeToolActions/DebugButton") is
                    { Disabled: false, FocusMode: Control.FocusModeEnum.All },
            "ForgeScript P1 audio, time, custom event, hot reload, debugger or accessible runtime panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R099_SCRIPT_RUNTIME_TOOLS_PASS " +
                 "audio=cues-state-fades-snapshots-listener-completion " +
                 "time=clock-timers-schedules-simulation-calendar-delay " +
                 "events=namespaced-schema-version-subscription-trace " +
                 "hot_reload=handler-boundary-migration-invalidation-restart " +
                 "debugger=breakpoints-step-stack-locals-watches-events-entities-remote-permissions " +
                 "ui=visible-empty-error-console-clickable command=preview-apply-reject " +
                 "history=undo-redo persistence=roundtrip forgescript=versioned-native-handles " +
                 "mcp=get-api-compile-diagnostics-tests-assign-event " +
                 "runtime=optional-safe-required-diagnostic sandbox=quota-abort-recovery " +
                 "engine=audio-clock-event-reload-debug-accessible-panel");
    }

    private void RunForgeScriptP1QualityRuntimeSmokeTest()
    {
        const string source = "void main() { say(\"quality-ready\"); }";
        var document = new ForgeScriptSourceDocument(
            Guid.Parse("c7100000-0000-4000-8000-000000000301"), 1, "runtime:r100",
            "scripts/r100-quality.as", source, "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], []);
        var profile = ForgeScriptQualityProfile.Default;
        var adapter = new ForgeScriptQualityRuntimeAdapter();
        var resolution = adapter.Resolve(profile, document, true);
        var handle = resolution.Handle ?? throw new InvalidOperationException("R100 quality handle is unavailable.");
        var runtime = adapter.Build(handle, profile, document);
        var tests = adapter.RunTests(handle, runtime,
        [
            new("runtime-quality", new("fixture", profile.UnitTests.DeterministicSeed,
                    new Dictionary<string, object?> { ["ready"] = true },
                    new Dictionary<string, object?> { ["clock"] = "mock" }),
                _ => { }, world => world.Events.Add("quality.tested"),
                [new("ready", world => (bool)world.State["ready"]!, "world not ready")]),
        ]);
        var profiler = adapter.Profile(handle, runtime,
        [
            new("r100-quality.as", "OnHeartbeat", 12_000, 9 * 1024 * 1024,
                TimeSpan.FromMilliseconds(125), ["OnHeartbeat", "FindAll"]),
        ]);
        var documentation = adapter.GenerateDocumentation(handle, runtime, ForgeScriptApiCatalog.Registrations);
        var diagnostics = adapter.Analyze(handle, runtime,
            new("scripts/r100-quality.as",
                "OnHeartbeat FindAll();\nUnsafeQuery();\nDeprecatedApi();\nServerOnly();\ntarget.Use();",
                "client", new HashSet<string> { "target" }));
        runtime.StateService.RegisterPersistent(new("score", 2, typeof(int),
            (version, json) => version == 1 ? json.GetInt32() + 1 : json.GetInt32())
        {
            Validate = value => value is int number && number >= 0,
        });
        var serialized = runtime.StateService.SerializeState(new Dictionary<string, object?>
        {
            ["score"] = 7,
            ["vm-pointer"] = "0xBAD",
        });
        var restored = runtime.StateService.DeserializeState(serialized);
        var execution = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(30_000, 1000, 32, 8));
        var abusiveDocument = document with
        {
            Id = Guid.Parse("c7100000-0000-4000-8000-000000000302"),
            Provenance = "runtime:r100-abuse",
            Source = "void main() { while (true) { int value = 1; } }",
        };
        var abusiveHandle = adapter.Resolve(profile, abusiveDocument, true).Handle!;
        var abusiveRuntime = adapter.Build(abusiveHandle, profile, abusiveDocument);
        var abusive = adapter.Execute(abusiveHandle, abusiveRuntime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(10, 1000, 32, 8));
        var recovered = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime(),
            new ForgeScriptQuotas(30_000, 1000, 32, 8));

        var history = new ForgeScriptQualityHistory(profile);
        var changed = profile with { Provenance = "runtime:r100-reviewed" };
        var preview = history.Preview(new(Guid.NewGuid(), 0, changed, "runtime preview"));
        var applied = history.Apply(new(Guid.NewGuid(), 0, changed, "runtime apply"));
        var undone = history.Undo();
        var redone = history.Redo();
        var rejected = history.Preview(new(Guid.NewGuid(), history.Revision,
            changed with { Documentation = changed.Documentation with { OutputRelativePath = "../invalid.md" } },
            "runtime invalid"));
        var storeRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-r100", Guid.NewGuid().ToString("N"));
        ForgeScriptQualityProfile loaded;
        try
        {
            var store = new ForgeScriptQualityStore(storeRoot);
            store.Save(redone.Profile);
            loaded = store.Load();
        }
        finally
        {
            if (Directory.Exists(storeRoot)) Directory.Delete(storeRoot, true);
        }
        var optional = adapter.Resolve(null, null, false);
        var required = adapter.Resolve(null, document, true);

        var root = new Node3D { Name = "ForgeScriptQuality_R100" };
        var panel = new ForgeScriptQualityPanel();
        panel.ShowEmpty();
        panel.ShowError("FORGESCRIPT_QUALITY_PREVIEW_REJECTED");
        panel.Configure(profile, tests, profiler, documentation, diagnostics, restored, execution);
        root.AddChild(panel);
        AddChild(root);

        AssertSmoke(resolution.Available && tests.Single().Passed &&
                    ForgeScriptQualityService.ToCiReport(tests).Contains("\"passed\":1", StringComparison.Ordinal) &&
                    profiler.Hotspots.Single().Function == "OnHeartbeat" && profiler.Alerts.Count == 3 &&
                    profiler.CallGraph["r100-quality.as::OnHeartbeat"].SequenceEqual(["OnHeartbeat", "FindAll"]) &&
                    documentation.Markdown.Contains("Do not edit manually", StringComparison.Ordinal) &&
                    documentation.McpResources.Count > 0 && diagnostics.Count == 5 &&
                    diagnostics.All(item => item.Path.StartsWith("scripts/r100-quality.as:", StringComparison.Ordinal)) &&
                    (int)restored["score"]! == 7 && !serialized.Contains("vm-pointer", StringComparison.Ordinal) &&
                    execution.Status == ForgeScriptExecutionStatus.Completed &&
                    execution.Events.Any(item => item.Kind == "say" && item.Value == "quality-ready") &&
                    abusive.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                    recovered.Status == ForgeScriptExecutionStatus.Completed &&
                    preview.Succeeded && applied.Succeeded && undone.Succeeded && redone.Succeeded &&
                    redone.Profile.Provenance == "runtime:r100-reviewed" && !rejected.Succeeded &&
                    loaded.Id == changed.Id && loaded.Provenance == changed.Provenance &&
                    loaded.State.RegisteredComponents.OrderBy(item => item.Key).SequenceEqual(
                        changed.State.RegisteredComponents.OrderBy(item => item.Key)) &&
                    optional.OptionalFallback &&
                    required.DiagnosticCode == ForgeScriptQualityRuntimeAdapter.RequiredMissingCode &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("ForgeScriptQualityLayout/QualityList") is { ItemCount: 5 } &&
                    panel.GetNodeOrNull<Button>("ForgeScriptQualityLayout/QualityActions/TestsButton") is
                    { Disabled: false, FocusMode: Control.FocusModeEnum.All },
            "ForgeScript P1 tests, profiler, generated docs, static analysis, script state or accessible panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R100_SCRIPT_QUALITY_PASS " +
                 "tests=testworld-fixtures-mocks-assertions-ci-deterministic " +
                 "profiler=per-handler-cpu-instructions-allocations-hotspots-call-graph-alerts " +
                 "docs=generated-versioned-examples-availability-mcp-resource " +
                 "analysis=heartbeat-query-null-deprecated-authority-localized " +
                 "state=registered-components-no-vm-pointers-migration-validation-quota " +
                 "ui=visible-empty-error-console-clickable command=preview-apply-reject " +
                 "history=undo-redo persistence=roundtrip forgescript=versioned-tooling " +
                 "mcp=get-api-compile-diagnostics-tests-assign-event " +
                 "runtime=optional-safe-required-diagnostic sandbox=quota-abort-recovery " +
                 "engine=quality-services-accessible-panel");
    }

    private void RunForgeScriptP1InteropRuntimeSmokeTest()
    {
        var document = new ForgeScriptSourceDocument(
            Guid.Parse("c7110000-0000-4000-8000-000000000301"), 1, "runtime:r101",
            "scripts/r101-interop.as", "void main() { say(\"interop-ready\"); }", "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode, true, [], []);
        var profile = ForgeScriptInteropProfile.Default;
        var adapter = new ForgeScriptInteropRuntimeAdapter();
        var resolution = adapter.Resolve(profile, document, true);
        var handle = resolution.Handle ?? throw new InvalidOperationException("R101 interop handle is unavailable.");
        var runtime = adapter.Build(handle, profile, document);
        var nominal = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime());
        var abusiveDocument = document with
        {
            Id = Guid.Parse("c7110000-0000-4000-8000-000000000302"),
            Provenance = "runtime:r101-abuse",
            Source = "void main() { while (true) { int value = 1; } }",
        };
        var abusiveHandle = adapter.Resolve(profile, abusiveDocument, true).Handle!;
        var abusive = adapter.Execute(abusiveHandle, adapter.Build(abusiveHandle, profile, abusiveDocument),
            new NativeForgeScriptRuntime(), new ForgeScriptQuotas(10, 1000, 32, 8));
        var recovered = adapter.Execute(handle, runtime, new NativeForgeScriptRuntime());

        var profileHistory = new ForgeScriptInteropHistory(profile);
        var changedProfile = profile with { Provenance = "runtime:r101-reviewed", MaximumSources = 1024 };
        var profilePreview = profileHistory.Preview(new(Guid.NewGuid(), 0, changedProfile, "runtime preview"));
        var profileApplied = profileHistory.Apply(new(Guid.NewGuid(), 0, changedProfile, "runtime apply"));
        var profileRejected = profileHistory.Preview(new(Guid.NewGuid(), profileHistory.Revision,
            changedProfile with { EventBinding = "" }, "runtime reject"));
        var profileUndone = profileHistory.Undo();
        var profileRedone = profileHistory.Redo();
        var sourceHistory = new ForgeScriptSourceHistory(document, new NativeForgeScriptRuntime(), []);
        var changedDocument = document with { Provenance = "runtime:r101-source" };
        var sourcePreview = sourceHistory.Preview(new(Guid.NewGuid(), 0, changedDocument, "runtime preview"));
        var sourceApplied = sourceHistory.Apply(new(Guid.NewGuid(), 0, changedDocument, "runtime apply"));
        var sourceUndone = sourceHistory.Undo();
        var sourceRedone = sourceHistory.Redo();
        var storeRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-r101", Guid.NewGuid().ToString("N"));
        ForgeScriptInteropProfile loaded;
        try
        {
            var store = new ForgeScriptInteropProfileStore(storeRoot);
            store.Save(profileRedone.Profile);
            loaded = store.Load();
        }
        finally
        {
            if (Directory.Exists(storeRoot)) Directory.Delete(storeRoot, true);
        }
        var optional = adapter.Resolve(null, null, false);
        var required = adapter.Resolve(null, document, true);

        var root = new Node3D { Name = "ForgeScriptInterop_R101" };
        var panel = new ForgeScriptInteropPanel();
        panel.ShowEmpty();
        panel.ShowError("FORGESCRIPT_INTEROP_PREVIEW_REJECTED");
        panel.Configure(profile, document, nominal, profileRedone.Revision);
        root.AddChild(panel);
        AddChild(root);

        AssertSmoke(resolution.Available && nominal.Status == ForgeScriptExecutionStatus.Completed &&
                    nominal.Events.Any(item => item.Kind == "say" && item.Value == "interop-ready") &&
                    abusive.Status == ForgeScriptExecutionStatus.QuotaExceeded &&
                    recovered.Status == ForgeScriptExecutionStatus.Completed &&
                    profilePreview.Succeeded && profileApplied.Succeeded && !profileRejected.Succeeded &&
                    profileUndone.Succeeded && profileRedone.Succeeded && loaded == changedProfile &&
                    sourcePreview.Succeeded && sourceApplied.Succeeded && sourceUndone.Succeeded &&
                    sourceRedone.Succeeded && sourceRedone.Document.Provenance == "runtime:r101-source" &&
                    optional.OptionalFallback &&
                    required.DiagnosticCode == ForgeScriptInteropRuntimeAdapter.RequiredMissingCode &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("ForgeScriptInteropLayout/InteropTools") is { ItemCount: 6 } &&
                    panel.GetNodeOrNull<Button>("ForgeScriptInteropLayout/InteropActions/RedoButton") is
                    { Disabled: false, FocusMode: Control.FocusModeEnum.All },
            "ForgeScript editor/MCP interop, transactions, persistence, sandbox recovery or accessible panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R101_SCRIPT_INTEROP_PASS " +
                 "tools=get-api-create-update-compile-diagnostics-run-tests-assign-event " +
                 "identity=uuid-schema-provenance-api-event-binding " +
                 "validation=required-unique-resolved-schema-compile-api-quotas-tests-localized " +
                 "ui=visible-empty-error-console-clickable command=preview-apply-reject " +
                 "history=undo-redo persistence=roundtrip editor=canonical-command-parity " +
                 "mcp=versioned-transactional-no-external-execution " +
                 "runtime=optional-safe-required-diagnostic sandbox=quota-abort-recovery " +
                 "engine=interop-services-accessible-panel");
    }

    private void RunNetworkFoundationP1RuntimeSmokeTest()
    {
        var initial = NetworkFoundationProfile.CreateDefault("runtime:r102");
        var history = new NetworkFoundationHistory(initial);
        var candidate = initial with
        {
            ServerRevision = 1,
            Server = initial.Server with { Capacity = 12 },
            Interest = initial.Interest with { MaximumDistance = 512 }
        };
        var command = new NetworkFoundationCommand(Guid.Parse("a1020000-0000-4000-8000-000000000301"),
            1, "runtime:r102", 0, "runtime:server", Forge.Network.Operations.NetworkRole.Administrator,
            true, candidate);
        var applied = history.Execute(command);
        var undone = history.Undo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var redone = history.Redo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var store = new NetworkFoundationStore();
        var loaded = store.Deserialize(store.Serialize(redone.Profile));
        var runtime = NetworkFoundationRuntimeAdapter.Build(loaded);
        var scriptSnapshot = runtime.Scripts.Read(runtime.Scripts.Handle);
        var optional = NetworkFoundationRuntimeAdapter.Resolve(null, false);
        var required = NetworkFoundationRuntimeAdapter.Resolve(null, true);
        var scenario = NetworkFoundationAcceptanceScenario.Run();
        var invalid = NetworkFoundationValidator.Validate(loaded with
        {
            Party = loaded.Party with { MaximumMembers = 1 }
        });
        var panel = new NetworkFoundationPanel();
        panel.ShowEmpty();
        panel.ShowError("NET_PARTY_CAPACITY_INVALID");
        panel.Configure(loaded, scenario);
        AddChild(panel);

        AssertSmoke(applied.Applied && undone.Succeeded && redone.Succeeded &&
                    store.Serialize(loaded) == store.Serialize(candidate) &&
                    scriptSnapshot.Id == loaded.Id && !optional.Available && optional.Diagnostics.Count == 0 &&
                    !required.Available && required.Diagnostics.Single().Code == "NET_FOUNDATION_REQUIRED_MISSING" &&
                    scenario.Passed && invalid.Any(item => item is
                    { Code: "NET_PARTY_CAPACITY_INVALID", Path: "/network/party/maximumMembers" }) &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("NetworkFoundationLayout/Capabilities") is { ItemCount: 5 } &&
                    panel.GetNodeOrNull<Button>("NetworkFoundationLayout/Actions/RedoButton") is
                    { FocusMode: Control.FocusModeEnum.All },
            "Network foundation profile, authority, party, replication, interest or accessible runtime panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R102_NETWORK_FOUNDATION_PASS " +
                 "dedicated=headless-service-health authority=intents-server-replicated-audited " +
                 "party=invitations-roles-leader-loot-quests-markers-separation " +
                 "replication=components-delta-ownership-prediction-reconciliation " +
                 "interest=area-distance-visibility-party-ownership-explicit " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-resolved-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit " +
                 "engine=authoritative-session-party-replication-interest-panel");
    }

    private void RunNetworkOperationsP1RuntimeSmokeTest()
    {
        var initial = NetworkOperationsProfile.CreateDefault("runtime:r103");
        var history = new NetworkOperationsHistory(initial);
        var candidate = initial with
        {
            ServerRevision = 1,
            Content = initial.Content with { ChunkBytes = 32 * 1024 },
            Chat = initial.Chat with { LocalRadius = 48 }
        };
        var command = new NetworkOperationsCommand(Guid.Parse("a1030000-0000-4000-8000-000000000301"),
            1, "runtime:r103", 0, "runtime:server", Forge.Network.Operations.NetworkRole.Administrator,
            true, candidate);
        var applied = history.Execute(command);
        var undone = history.Undo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var redone = history.Redo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var store = new NetworkOperationsStore();
        var loaded = store.Deserialize(store.Serialize(redone.Profile));
        var runtime = NetworkOperationsRuntimeAdapter.Build(loaded);
        var scriptSnapshot = runtime.Scripts.Read(runtime.Scripts.Handle);
        var optional = NetworkOperationsRuntimeAdapter.Resolve(null, false);
        var required = NetworkOperationsRuntimeAdapter.Resolve(null, true);
        var scenario = NetworkOperationsAcceptanceScenario.Run();
        var invalid = NetworkOperationsValidator.Validate(loaded with
        {
            Events = loaded.Events with { MaximumPendingEvents = 0 }
        });
        var panel = new NetworkOperationsPanel();
        panel.ShowEmpty();
        panel.ShowError("NET_EVENT_POLICY_INVALID");
        panel.Configure(loaded, scenario);
        AddChild(panel);

        AssertSmoke(applied.Applied && undone.Succeeded && redone.Succeeded &&
                    store.Serialize(loaded) == store.Serialize(candidate) &&
                    scriptSnapshot.Id == loaded.Id && scriptSnapshot.RoleCount == 6 &&
                    !optional.Available && optional.Diagnostics.Count == 0 &&
                    !required.Available && required.Diagnostics.Single().Code == "NET_OPERATIONS_REQUIRED_MISSING" &&
                    scenario.Passed && invalid.Any(item => item is
                    { Code: "NET_EVENT_POLICY_INVALID", Path: "/network/operations/events" }) &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("NetworkOperationsLayout/Capabilities") is { ItemCount: 5 } &&
                    panel.GetNodeOrNull<Button>("NetworkOperationsLayout/Actions/RedoButton") is
                    { FocusMode: Control.FocusModeEnum.All },
            "Network operations content, version, roles, chat, events or accessible panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R103_NETWORK_OPERATIONS_PASS " +
                 "content=manifest-chunks-hashes-cache-resume-delta-trust " +
                 "version=lockfile-fingerprint-before-join-readable-mismatch " +
                 "roles=player-moderator-builder-gm-admin-spectator-custom-abac " +
                 "chat=local-party-tell-shout-dm-proximity-language-logs-accessibility-bridges " +
                 "events=typed-subscriptions-priority-trace-replay-backpressure " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-resolved-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit " +
                 "engine=content-access-chat-events-panel");
    }

    private void RunNetworkAssuranceP1RuntimeSmokeTest()
    {
        var initial = NetworkAssuranceProfile.CreateDefault("runtime:r104");
        var history = new NetworkAssuranceHistory(initial);
        var candidate = initial with
        {
            ServerRevision = 1,
            Logs = initial.Logs with { RotationFiles = 5 },
            DeterministicTests = initial.DeterministicTests with { SoakCycles = 8 }
        };
        var command = new NetworkAssuranceCommand(Guid.Parse("a1040000-0000-4000-8000-000000000301"),
            1, "runtime:r104", 0, "runtime:server", Forge.Network.Operations.NetworkRole.Administrator,
            true, candidate);
        var applied = history.Execute(command);
        var undone = history.Undo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var redone = history.Redo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var store = new NetworkAssuranceStore();
        var loaded = store.Deserialize(store.Serialize(redone.Profile));
        var sink = new MemoryRemoteLogSink("audit");
        var runtime = NetworkAssuranceRuntimeAdapter.Build(loaded, sinks: [sink]);
        var scriptSnapshot = runtime.Scripts.Read(runtime.Scripts.Handle);
        var optional = NetworkAssuranceRuntimeAdapter.Resolve(null, false);
        var required = NetworkAssuranceRuntimeAdapter.Resolve(null, true);
        var scenario = NetworkAssuranceAcceptanceScenario.Run();
        var invalid = NetworkAssuranceValidator.Validate(loaded with
        {
            DeterministicTests = loaded.DeterministicTests with { ClientCount = 1 }
        });
        var panel = new NetworkAssurancePanel();
        panel.ShowEmpty();
        panel.ShowError("NET_ASSURANCE_TEST_POLICY_INVALID");
        panel.Configure(loaded, scenario);
        AddChild(panel);

        AssertSmoke(applied.Applied && undone.Succeeded && redone.Succeeded &&
                    store.Serialize(loaded) == store.Serialize(candidate) &&
                    scriptSnapshot.Id == loaded.Id && scriptSnapshot.SimulatedClients == 3 &&
                    !optional.Available && optional.Diagnostics.Count == 0 &&
                    !required.Available && required.Diagnostics.Single().Code == "NET_ASSURANCE_REQUIRED_MISSING" &&
                    scenario.Passed && invalid.Any(item => item is
                    { Code: "NET_ASSURANCE_TEST_POLICY_INVALID", Path: "/network/assurance/deterministicTests" }) &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("NetworkAssuranceLayout/Capabilities") is { ItemCount: 3 } &&
                    panel.GetNodeOrNull<Button>("NetworkAssuranceLayout/Actions/RedoButton") is
                    { FocusMode: Control.FocusModeEnum.All },
            "Network assurance logs, anti-cheat, deterministic tests or accessible panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R104_NETWORK_ASSURANCE_PASS " +
                 "logs=json-text-categories-correlation-privacy-rotation-remote-sinks " +
                 "anti_cheat=intents-rate-signed-content-authoritative-rng-anomaly-untrusted-saves " +
                 "tests=clients-latency-loss-duplicate-reorder-replay-soak-authoritative-state " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit " +
                 "engine=logs-authority-determinism-panel");
    }

    private void RunLiveDirectorAccessP1RuntimeSmokeTest()
    {
        var initial = LiveDirectorAccessProfile.CreateDefault("runtime:r105");
        var history = new LiveDirectorAccessHistory(initial);
        var candidate = initial with
        {
            ServerRevision = 1,
            Players = initial.Players with { RefreshSeconds = 3 },
            Audit = initial.Audit with { RetentionDays = 120 }
        };
        var command = new LiveDirectorAccessCommand(Guid.Parse("a1050000-0000-4000-8000-000000000301"),
            1, "runtime:r105", 0, "runtime:server", Forge.Network.Operations.NetworkRole.Administrator,
            NetworkAuthorityScope.Server, true, candidate);
        var applied = history.Execute(command);
        var undone = history.Undo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var redone = history.Redo("runtime:server", Forge.Network.Operations.NetworkRole.Administrator);
        var store = new LiveDirectorAccessStore();
        var loaded = store.Deserialize(store.Serialize(redone.Profile));
        var runtime = LiveDirectorAccessRuntimeAdapter.Build(loaded);
        var snapshot = runtime.Scripts.Read(runtime.Scripts.Handle);
        var optional = LiveDirectorAccessRuntimeAdapter.Resolve(null, false);
        var required = LiveDirectorAccessRuntimeAdapter.Resolve(null, true);
        var scenario = LiveDirectorAccessAcceptanceScenario.Run();
        var invalid = LiveDirectorAccessValidator.Validate(loaded with
        {
            Chat = loaded.Chat with { ModerationEnabled = false }
        });
        var panel = new LiveDirectorAccessPanel();
        panel.ShowEmpty();
        panel.ShowError("LIVE_ACCESS_CHAT_POLICY_INVALID");
        panel.Configure(loaded, scenario);
        AddChild(panel);

        AssertSmoke(applied.Applied && undone.Succeeded && redone.Succeeded &&
                    store.Serialize(loaded) == store.Serialize(candidate) &&
                    snapshot.Id == loaded.Id && snapshot.ChatChannels == 7 &&
                    !optional.Available && optional.Diagnostics.Count == 0 &&
                    !required.Available && required.Diagnostics.Single().Code == "LIVE_ACCESS_REQUIRED_MISSING" &&
                    scenario.Passed && invalid.Any(item => item is
                    { Code: "LIVE_ACCESS_CHAT_POLICY_INVALID", Path: "/liveDirector/access/chat" }) &&
                    panel.GetMeta("state").AsString() == "ready" &&
                    panel.GetNodeOrNull<ItemList>("LiveDirectorAccessLayout/Capabilities") is { ItemCount: 5 } &&
                    panel.GetNodeOrNull<Button>("LiveDirectorAccessLayout/Actions/RedoButton") is
                    { FocusMode: Control.FocusModeEnum.All },
            "Live Director authentication, inspection, players, audit, chat or accessible panel failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R105_LIVE_DIRECTOR_ACCESS_PASS " +
                 "auth=gm-role-mfa-expiry-revoke-audit " +
                 "inspect=components-stats-variables-scripts-ownership-history-read-only " +
                 "players=identity-character-zone-latency-party-permissions-voice-moderation-refresh " +
                 "audit=actor-command-targets-before-after-reason-correlation-hash-chain " +
                 "chat=local-party-tell-server-gm-faction-impersonation-moderation-logs " +
                 "identity=uuid-schema-provenance-scope-revision-audit " +
                 "validation=required-unique-schema-permission-scope-revision-audit-localized " +
                 "ui=visible-empty-error-permissions-confirmation-audit command=preview-apply-reject " +
                 "history=undo-redo-compensation persistence=roundtrip forgescript=typed-handles " +
                 "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic server=integration-permissions-audit " +
                 "engine=auth-inspection-players-audit-chat-panel");
    }

    private static void RunContentPackageRuntimeSmokeTest()
    {
        static ContentPackageSource Source(Guid id, string resource, string path, char hash) =>
            new(id, 1, "runtime:r016", resource, path, ContentSourceKind.Json, true, "CC0-1.0",
                new string(hash, 64), 1_048_576);
        static ContentPackageDefinition Package(Guid id, string packageNamespace, int priority,
            IReadOnlyList<ContentPackageSource> sources, IReadOnlyList<ContentPackageDependency>? dependencies = null,
            IReadOnlyList<string>? sealedResources = null) =>
            new(id, 1, "runtime:r016", packageNamespace, "1.0.0", "CC0-1.0", new string('c', 64),
                priority, sealedResources, dependencies, sources);

        var moduleId = Guid.Parse("c0160000-0000-4000-8000-000000000301");
        var core = Package(Guid.Parse("c0160000-0000-4000-8000-000000000302"), "forge.core", 10,
            [Source(Guid.Parse("c0160000-0000-4000-8000-000000000303"), "rules.balance", "data/core.json", 'a')],
            sealedResources: ["rules.balance"]);
        var optional = new ContentPackageDependency(Guid.Parse("c0160000-0000-4000-8000-000000000304"), 1,
            "runtime:r016", "forge.cosmetics", "^1.0.0", ContentDependencyKind.Optional);
        var module = Package(Guid.Parse("c0160000-0000-4000-8000-000000000305"), "crypt.module", 50,
            [Source(Guid.Parse("c0160000-0000-4000-8000-000000000306"), "quest.main", "data/quest.json", 'b')],
            [optional]);
        var catalog = new ContentPackageCatalog(moduleId, 1, "runtime:r016",
            ContentConflictPolicy.HighestPriority, [core, module]);
        var resolved = ContentPackageResolver.Resolve(catalog);

        var required = new ContentPackageDependency(Guid.Parse("c0160000-0000-4000-8000-000000000307"), 1,
            "runtime:r016", "forge.required-missing", ">=1.0.0", ContentDependencyKind.Required);
        var requiredCatalog = new ContentPackageCatalog(moduleId, 1, "runtime:r016",
            ContentConflictPolicy.HighestPriority,
            [Package(Guid.Parse("c0160000-0000-4000-8000-000000000308"), "crypt.required", 1, [], [required])]);
        var requiredResult = ContentPackageResolver.Resolve(requiredCatalog);

        AssertSmoke(resolved.Valid && resolved.LockEntries.Count == 2 && resolved.Resources.Count == 2 &&
                    !resolved.Diagnostics.Any(item => item.Message.Contains("cosmetics", StringComparison.Ordinal)) &&
                    requiredResult.Diagnostics.Any(item =>
                        item.Code == ContentPackageCatalogValidator.MissingDependencyCode),
            "Content package offline resolution did not ignore optional or diagnose required absence.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R016_CONTENT_PACKAGES_PASS " +
                 "package=versioned-manifest priority=deterministic-sealed dependencies=offline-lock " +
                 "optional=ignored required=diagnostic sources=text-safe compiler=packaged");
    }

    private static void RunContentAuthoringRuntimeSmokeTest(ProjectDefinition project)
    {
        var localization = new LocalizationCatalog(LocalizationCatalogId.From(project.Module.Id.Value), "fr",
            ["fr", "en"], [new LocalizationMessage("runtime.r017.loot",
                "{count, plural, =0 {aucun objet} one {# objet} other {# objets}}",
                new Dictionary<string, string>
                {
                    ["en"] = "{count, plural, =0 {no item} one {# item} other {# items}}",
                }, "runtime:r017")], packageNamespace: "crypt.localization", provenance: "runtime:r017");
        var localized = localization.Resolve("runtime.r017.loot", "en-US",
            new Dictionary<string, object?> { ["count"] = 2 });

        var source = new ContentPackageSource(Guid.Parse("c0170000-0000-4000-8000-000000000101"), 1,
            "runtime:r017", "rules/encounter", "data/encounter.json", ContentSourceKind.Json, false,
            "CC0-1.0", new string('a', 64), 4096, ["legacy.core:text/encounter"],
            ["crypt.core:model/props/chest"]);
        var asset = new ContentPackageAsset(Guid.Parse("c0170000-0000-4000-8000-000000000102"), 1,
            "runtime:r017", "props/chest", "models/chest.glb", true, "CC0-1.0", new string('b', 64),
            8 * 1024 * 1024);
        var package = new ContentPackageDefinition(Guid.Parse("c0170000-0000-4000-8000-000000000103"), 1,
            "runtime:r017", "crypt.core", "1.0.0", "CC0-1.0", new string('c', 64), 10, [], [], [source],
            [asset]);
        var catalog = new ContentPackageCatalog(project.Module.Id.Value, 1, "runtime:r017",
            ContentConflictPolicy.HighestPriority, [package]);
        var registryResult = ContentResourceRegistry.Build(catalog);
        var registry = ContentResourceRegistry.Create(registryResult);

        var module = project.Module;
        var content = new ContentWorkspaceConfiguration(module.Content.Id, module.Content.Batch,
            module.Content.DataTables, module.Content.MergePolicy, module.Content.Provenance,
            module.Content.SchemaVersion, catalog);
        var configuredModule = new ModuleDefinition(module.Id, module.Slug, module.DisplayName, module.Description,
            module.EntryAreaId, module.Metadata, module.Variables, module.Distribution, localization, content,
            module.ArtisticStyle, module.RuntimePolicies, module.Factions, module.WorldFeatures);
        var configuredProject = new ProjectDefinition(project.Id, project.Title, configuredModule,
            project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        var browser = new UnifiedContentBrowser(configuredProject);

        var texture = new AssetProcessingProfile(
            AssetCatalogItemId.From(Guid.Parse("c0170000-0000-4000-8000-000000000104")), 1,
            AssetProcessingKind.Texture, "png", "ktx2", "CC0-1.0", new string('d', 64), "runtime:r017",
            4 * 1024 * 1024, texture: new(TextureColorSpace.Srgb, TextureCompressionProfile.Universal, 2048,
                10.24m, new Dictionary<PbrTextureChannel, string>
                {
                    [PbrTextureChannel.BaseColor] = "textures/chest.png",
                }));
        var model = new AssetProcessingProfile(
            AssetCatalogItemId.From(Guid.Parse("c0170000-0000-4000-8000-000000000105")), 1,
            AssetProcessingKind.Model, "glb", "glb", "CC0-1.0", new string('e', 64), "runtime:r017",
            16 * 1024 * 1024, model: new(true, true, true, ["hand.r"], [1m, .5m]));

        var invalidSource = source with
        {
            Id = Guid.Parse("c0170000-0000-4000-8000-000000000106"),
            Required = true,
            References = ["crypt.core:model/missing"],
        };
        var invalidPackage = new ContentPackageDefinition(package.Id, package.SchemaVersion, package.Provenance,
            package.PackageNamespace, package.Version, package.LicenseSpdx, package.ContentSha256, package.Priority,
            package.SealedResources, package.Dependencies, [invalidSource], [asset]);
        var invalidRegistry = ContentResourceRegistry.Build(new ContentPackageCatalog(catalog.Id,
            catalog.SchemaVersion, catalog.Provenance, catalog.ConflictPolicy, [invalidPackage]));

        AssertSmoke(localized == "2 items" && registryResult.Valid &&
                    registry.TryResolve("legacy.core:text/encounter", out var resolved) &&
                    resolved?.References.Single() == "crypt.core:model/props/chest" &&
                    browser.Search(new UnifiedContentBrowserQuery(PackageNamespace: "crypt.core")).Count == 2 &&
                    browser.Items.Any(item => item.Status == "optional") &&
                    AssetProcessingProfileValidator.Validate(texture).Count == 0 &&
                    AssetProcessingProfileValidator.Validate(model).Count == 0 &&
                    invalidRegistry.Diagnostics.Any(item =>
                        item.Code == ContentPackageCatalogValidator.ResourceReferenceCode),
            "Content authoring localization, resource, profile or browser runtime contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R017_CONTENT_AUTHORING_PASS " +
                 "localization=icu-fallback resource=namespaced-alias references=resolved " +
                 "texture=pbr-profile model=glb-profile browser=unified optional=ignored required=diagnostic");
    }

    private static void RunAssetGovernanceRuntimeSmokeTest()
    {
        var assetId = AssetCatalogItemId.ForExternalId("runtime:r018-governed-asset");
        var record = new AssetGovernanceRecord(1, AssetGovernanceId.ForAsset(assetId), assetId,
            AssetPackageManifestId.ForAsset(assetId), "CC0-1.0", new string('f', 64), "Runtime fixture",
            "https://example.invalid/runtime-asset", "permission:runtime-fixture", "None.", false,
            string.Empty, string.Empty, 4096,
            new("incoming/model.glb", "model/gltf-binary", "model/gltf-binary", 12, 4096, "scanner", "clean",
                "valid", AssetQuarantineStatus.Approved, "reviewer", DateTimeOffset.UnixEpoch),
            new(AssetOriginKind.OpenLicensed, true, false, string.Empty));
        var optional = AssetGovernanceResolver.Resolve([], assetId, required: false);
        var required = AssetGovernanceResolver.Resolve([], assetId, required: true);
        var proprietary = AssetGovernanceValidator.Validate(record with
        {
            IpBoundary = new AssetIpBoundary(AssetOriginKind.ProprietaryCep, false, true, "legal-review:R018"),
        });
        AssertSmoke(AssetGovernanceValidator.Validate(record).Count == 0 && optional.Diagnostics.Count == 0 &&
                    required.Diagnostics.Single().Code == AssetGovernanceResolver.RequiredMissingCode &&
                    proprietary.Any(item => item.Code == AssetGovernanceValidator.ProprietaryRedistributionCode),
            "Asset governance runtime optional/required or IP boundary contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R018_ASSET_GOVERNANCE_PASS provenance=validated quarantine=approved " +
                 "ip=proprietary-blocked optional=ignored required=diagnostic");
    }

    private static void RunQualityFoundationRuntimeSmokeTest()
    {
        var profile = QualityFoundationProfile.CreateDefault(
            Guid.Parse("30000000-0000-4000-8000-000000000001"));
        var logger = new StructuredLogger(TimeProvider.System, profile.Logs);
        var objectId = Guid.Parse("20000000-0000-4000-8000-000000000001");
        var transactionId = Guid.Parse("20000000-0000-4000-8000-000000000002");
        var entry = logger.Write(StructuredLogSeverity.Information, "runtime.qa.ready", Guid.NewGuid(),
            new StructuredLogContext("runtime", objectId, 42, transactionId, "player-runtime"),
            new Dictionary<string, object?> { ["accessToken"] = "never-log", ["playerName"] = "Elyra" });
        AssertSmoke(QualityFoundationProfileValidator.Validate(profile).Count == 0 &&
                    entry.ObjectId == objectId && entry.SourceLine == 42 &&
                    entry.TransactionId == transactionId &&
                    Equals(entry.Properties["accessToken"], "[REDACTED]") &&
                    Equals(entry.Properties["playerName"], "[PLAYER_REDACTED]"),
            "QA foundation or structured logging runtime contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R019_QA_FOUNDATIONS_PASS " +
                 "licenses=spdx-provenance-notice-block domain=covered headless=scenarios " +
                 "save=roundtrip-legacy-partial-missing-rollback-deterministic " +
                 "logs=severity-category-object-source-transaction-privacy-retention");
    }

    private static void RunCanonicalValidationRuntimeSmokeTest()
    {
        var profile = QualityFoundationProfile.CreateDefault(
            Guid.Parse("30000000-0000-4000-8000-000000000027"));
        var optional = WorkspaceCanonicalValidationService.ValidateRuntimePolicy(null, required: false);
        var required = WorkspaceCanonicalValidationService.ValidateRuntimePolicy(null, required: true);
        AssertSmoke(optional.Count == 0 &&
                    required.Single().Code == WorkspaceCanonicalValidationService.RuntimePolicyMissingCode &&
                    QualityFoundationProfileValidator.Validate(profile).Count == 0 &&
                    profile.EffectiveValidation.Categories.Count == 5,
            "Canonical validation runtime optional/required contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R027_QA_VALIDATION_PASS " +
                 "categories=5 schemas=versioned references=typed graphs=generic geometry=scale-polygon " +
                 "navigation=agents-doors-links-flight optional=ignored required=diagnostic");
    }

    private void RunAreaFoundationRuntimeSmokeTest(ProjectDefinition project)
    {
        var area = project.Areas.First();
        var defaults = area.Foundation;
        var enteredBinding = new AreaLifecycleScriptBinding(
            Guid.Parse("a0282000-0000-4000-8000-000000000001"), "area.entered",
            "scripts/runtime_area_entered.forge", RuntimeLifecycleEvent.CurrentForgeScriptApiVersion,
            false, "runtime:r028-smoke");
        var tileEntity = area.Entities.First();
        var tileDefinitionId = Guid.Parse("a0282000-0000-4000-8000-000000000003");
        var tileDefinition = new ModularTileDefinition(tileDefinitionId, "runtime-tile", tileEntity.BlueprintId,
            ModularTileRotationMode.Orthogonal, true, false, [],
            [new(Guid.Parse("a0282000-0000-4000-8000-000000000004"), "weathered", "weathered",
                tileEntity.BlueprintId)]);
        var tilePlacement = new ModularTilePlacement(
            Guid.Parse("a0282000-0000-4000-8000-000000000005"), tileDefinitionId, tileEntity.Id,
            2, -1, 90, ModularTileMirrorMode.X, "weathered",
            Guid.Parse("a0532000-0000-4000-8000-000000000001"));
        var tileGroup = new ModularTileGroup(tilePlacement.GroupId!.Value, "Runtime room", [tilePlacement.Id]);
        var tileEditing = new AreaTileEditingProfile(
            Guid.Parse("a0532000-0000-4000-8000-000000000002"), 1, "runtime:r053-smoke",
            [new(Guid.Parse("a0532000-0000-4000-8000-000000000003"), tileGroup.Id,
                [new("width", "int", "1")], new(1, 1, 3),
                [new(Guid.Parse("a0532000-0000-4000-8000-000000000004"), "entry", "edge", 0, 0,
                    ModularTileSocketDirection.East)], [new("default", [tileDefinition.Id])], true)],
            new(AreaTileEditOperationKind.Replace, tileDefinition.Id, "runtime-remap", tileDefinition.Id,
                "runtime-tile", true),
            new(53053, [tilePlacement.Id], true),
            [new(Guid.Parse("a0532000-0000-4000-8000-000000000005"), tilePlacement.Id, "torchlit",
                "animations/torch-loop", true, new Dictionary<string, string> { ["floor"] = "wet-stone" },
                new(true, "#ff9858", 2, 8), true, true, ["entry"],
                new Dictionary<string, string> { ["state"] = "torchlit" })],
            new(1, 1, 512, 512, "NorthWest", true, [new(tilePlacement.Id, 1, -1)]));
        var foundation = new AreaFoundationProfile(defaults.Id, defaults.SchemaVersion, "runtime:r028-smoke",
            defaults.Creation with { KitKey = "runtime-modular-kit", Seed = 2026 },
            new(defaults.TileKit.Id, defaults.TileKit.SchemaVersion, "runtime:r028-smoke",
                2, [tileDefinition], [], [tilePlacement], [tileGroup], [], tileEditing),
            new(enteredBinding, null), defaults.Volumes, defaults.Dependencies, true);
        var updatedArea = area.WithFoundation(foundation);
        var updatedProject = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            project.Areas.Select(item => item.Id == area.Id ? updatedArea : item).ToArray(), project.Dialogues,
            project.Quests);
        var lifecycle = new RuntimeLifecycleCoordinator();
        var loaded = lifecycle.LoadProject(updatedProject, RuntimeAuthority.SinglePlayer,
            RuntimeLifecycleCause.Startup);
        var entered = lifecycle.EnterInitialArea(area.Id, null, RuntimeLifecycleCause.Startup);
        var snapshot = AreaFoundationScriptService.Read(updatedArea);
        var resolvedTile = ModularTilePlacementResolver.Resolve(updatedArea, tileEntity);
        var autoFit = AreaFoundationScriptService.PreviewAutoFit(updatedArea, tilePlacement.Id);
        var editingRuntime = AreaTileEditingRuntimeResolver.Resolve(updatedArea.Id, tileEditing, required: true);
        var editingOptional = AreaTileEditingRuntimeResolver.Resolve(updatedArea.Id, null, required: false);
        var editingRequired = AreaTileEditingRuntimeResolver.Resolve(updatedArea.Id, null, required: true);
        var invalid = new AreaFoundationProfile(foundation.Id, foundation.SchemaVersion, foundation.Provenance,
            foundation.Creation, foundation.TileKit,
            new(new(Guid.Parse("a0282000-0000-4000-8000-000000000002"), "area.entered", "",
                RuntimeLifecycleEvent.CurrentForgeScriptApiVersion, true, "runtime:r028-smoke"), null),
            foundation.Volumes, foundation.Dependencies);
        AssertSmoke(AreaFoundationValidator.Validate(updatedArea, foundation).Count == 0 && loaded.Succeeded &&
                    entered.Succeeded && entered.Events.Single().ScriptPath == enteredBinding.ScriptPath &&
                    !entered.Events.Single().ScriptRequired && snapshot.KitKey == "runtime-modular-kit" &&
                    resolvedTile.Entity.Transform.Position.X == 4 &&
                    resolvedTile.Entity.Transform.Position.Z == -2 &&
                    resolvedTile.Mirror == ModularTileMirrorMode.X && resolvedTile.SemanticState == "weathered" &&
                    AreaFoundationValidator.Validate(updatedArea, invalid)
                        .Any(item => item.Code == AreaFoundationValidator.LifecycleCode),
            "Area foundation runtime optional/required lifecycle contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R028_AREA_FOUNDATION_PASS " +
                 "document=canonical-independent tiles=grid-sockets-rules-variants-runtime rotation=policy-mirror " +
                 "lifecycle=entered-exited-context-authority optional=ignored required=diagnostic forgescript=typed");
        AssertSmoke(snapshot.EditableGroupCount == 1 && snapshot.TilePropertyCount == 1 &&
                    snapshot.ManualLockCount == 1 && snapshot.ResizeMigrationEnabled && snapshot.TileEditingRequired &&
                    autoFit.ManualLock && autoFit.SelectedDefinitionId == tileDefinition.Id &&
                    editingRuntime.Handle is { GroupCount: 1, PropertyCount: 1, ManualLockCount: 1 } &&
                    editingRuntime.Diagnostics.Count == 0 && editingOptional.Diagnostics.Count == 0 &&
                    editingRequired.Diagnostics.Single().Code == AreaTileEditingRuntimeResolver.RequiredMissingCode,
            "Advanced tile editing runtime optional/required contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R053_AREA_TILE_EDITING_PASS " +
                 "groups=editable parameters=typed tools=canonical autofit=deterministic-lock-ambiguity " +
                 "properties=state-animation-material-light-collision-navigation-sockets-overrides " +
                 "resize=loss-migration forgescript=typed-handle optional=ignored required=diagnostic");

        var environmentSource = area.Environment;
        var environmentAuthoring = new AreaEnvironmentAuthoringProfile(
            Guid.Parse("a0544000-0000-4000-8000-000000000001"), 1, "runtime:r054-smoke",
            new(environmentSource.Classification, "custom:runtime-moonlit", "runtime-environment-pack",
                true, true, true, true, true, true),
            new("physical-runtime-moonlit", "#30405f", "#ffe2b5", false,
                [new(0, 0, .8f, -45), new(12, 1.4f, 0, 65), new(24, 0, .8f, -45)],
                [new(Guid.Parse("a0544000-0000-4000-8000-000000000002"), 18,
                    "world.runtime-sunset", "scripts/runtime_sunset.forge", true)],
                [new(Guid.Parse("a0544000-0000-4000-8000-000000000003"), "Runtime probe",
                    TransformValue.Identity, "asset:runtime-probe", null, 24, false,
                    new Dictionary<string, string>())],
                [new(Guid.Parse("a0544000-0000-4000-8000-000000000004"), "Runtime shrine",
                    TransformValue.Identity, "physical-runtime-shrine", FogMode.Volumetric, 3,
                    new Dictionary<string, string>())]),
            new("#b7c9dd", "#263248", new("asset:runtime-fog-noise", 12, .1f, .05f, 1.2f),
                [new(Guid.Parse("a0544000-0000-4000-8000-000000000005"), "Runtime mist",
                    TransformValue.Identity, FogMode.Volumetric, .3f, .2f, "#9fb4c8", 2,
                    new Dictionary<string, string>())],
                new Dictionary<string, AreaFogQualityTier> { ["desktop"] = AreaFogQualityTier.High }), true);
        var environment = new AreaEnvironmentProfile(AreaEnvironmentProfile.CurrentSchemaVersion,
            environmentSource.Classification, [.. environmentSource.Tags, "runtime-moonlit"],
            new(DayNightMode.Clock, 30, .4f, 1.4f, 5400, true, true),
            new(FogMode.Volumetric, .3f, 4, 90, 1.5f), environmentSource.Audio,
            environmentSource.Rendering, environmentSource.Sky, environmentSource.Wind,
            environmentSource.Weather, environmentSource.Water, environmentSource.Reverb,
            environmentAuthoring, true);
        var environmentArea = area.WithEnvironment(environment);
        var environmentSnapshot = AreaEnvironmentScriptService.Read(environmentArea);
        var timePreview = AreaEnvironmentScriptService.PreviewTime(environmentArea, 18);
        var environmentRuntime = AreaEnvironmentRuntimeResolver.Resolve(environmentArea.Id, environment, true);
        var environmentOptional = AreaEnvironmentRuntimeResolver.Resolve(environmentArea.Id, null, false);
        var environmentRequired = AreaEnvironmentRuntimeResolver.Resolve(environmentArea.Id, null, true);
        AssertSmoke(AreaEnvironmentAuthoringValidator.Validate(environmentArea, environment).Count == 0 &&
                    environmentSnapshot.ClassificationKey == "custom:runtime-moonlit" &&
                    environmentSnapshot.DrivenSubsystems.Count == 6 &&
                    environmentSnapshot.CurvePointCount == 3 && environmentSnapshot.TimedEventCount == 1 &&
                    environmentSnapshot.ReflectionProbeCount == 1 &&
                    environmentSnapshot.LightingVolumeCount == 1 && environmentSnapshot.FogVolumeCount == 1 &&
                    timePreview.EventsAtHour.Single() == "world.runtime-sunset" &&
                    environmentRuntime.Handle is { ReflectionProbes: 1, LightingVolumes: 1, FogVolumes: 1 } &&
                    environmentRuntime.Diagnostics.Count == 0 && environmentOptional.Diagnostics.Count == 0 &&
                    environmentRequired.Diagnostics.Single().Code ==
                    AreaEnvironmentRuntimeResolver.RequiredMissingCode,
            "R054 area environment runtime optional/required contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R054_AREA_ENVIRONMENT_PASS " +
                 "classification=extensible-subsystems daynight=curves-events-volume-overrides " +
                 "lighting=physical-probes fog=noise-local-volumes-quality forgescript=typed-handle " +
                 "optional=ignored required=diagnostic");

        var r055Room = new AreaVisibilityRoomProfile(
            Guid.Parse("a0557000-0000-4000-8000-000000000001"), "Runtime crypt",
            TransformValue.Identity, false, 96, new Dictionary<string, string>());
        var r055Visibility = new AreaVisibilityAuthoringProfile([r055Room], [],
            [new(Guid.Parse("a0557000-0000-4000-8000-000000000002"), "Runtime wall",
                TransformValue.Identity, false, "asset:runtime-wall", new Dictionary<string, string>())],
            true, true, true, 16, 128);
        var r055Lod = new AreaLodAuthoringProfile(AreaLodSourceMode.Hybrid, [20, 50, 100],
            1_500_000, 96, 24, .15f, true, true,
            [new(Guid.Parse("a0557000-0000-4000-8000-000000000003"), "asset:runtime-column",
                AreaLodSourceMode.Generated, [1, .5f], [12000, 6000], .2f, "runtime:r055")]);
        var r055Experience = new AreaExperienceAuthoringProfile(
            Guid.Parse("a0557000-0000-4000-8000-000000000004"), 1, "runtime:r055",
            [new(Guid.Parse("a0557000-0000-4000-8000-000000000005"), "Runtime combat",
                new Dictionary<string, float> { ["music"] = .8f }, .75f,
                $"area.{area.Slug}.combat", "runtime:r055")], r055Visibility, r055Lod, true);
        var r055Event = $"area.{area.Slug}.alarm";
        var r055UsesModuleScript = _isCryptPrototype;
        var r055Script = _isCryptPrototype
            ? "scripts/ash_warden_death.forge"
            : "scripts/area_user_event_smoke.forge";
        var r055Subscription = r055UsesModuleScript ? "main" : "OnAreaAlarm";
        string? ResolveR055Source(string path) => r055UsesModuleScript
            ? ReadConfiguredScriptSource(path)
            : string.Equals(path, r055Script, StringComparison.Ordinal)
                ? "void OnAreaAlarm(CustomEventRef event_ref) { say(\"Area user event smoke\"); }"
                : null;
        var r055Authoring = new AreaAuthoringProfile(1,
            [new(Guid.Parse("a0557000-0000-4000-8000-000000000006"), r055Event,
                "schema://area/alarm/v1", r055Script, 1, [r055Subscription], true,
                "runtime:r055", AreaUserEvent.CurrentSchemaVersion,
                new Dictionary<string, string> { ["level"] = "int" },
                AreaUserEvent.CurrentForgeScriptApiVersion)],
            new(true, true, "hybrid-2d-3d", 2,
                [new(Guid.Parse("a0557000-0000-4000-8000-000000000007"), "Runtime shrine", 16, 0, 0,
                    false)], Guid.Parse("a0557000-0000-4000-8000-000000000008"), 1, "runtime:r055",
                true, true, AreaMapExplorationScope.Party, 8, "area.map.reveal"), area.Authoring.Layers,
            r055Experience);
        var r055Area = area.WithAuthoring(r055Authoring);
        var r055Snapshot = AreaExperienceAuthoringScriptService.Read(r055Area);
        var r055Payload = new Dictionary<string, object?> { ["level"] = 2 };
        var r055Preview = AreaUserEventRuntimeService.Dispatch(r055Area, r055Event, r055Payload,
            _forgeScriptRuntime, ResolveR055Source, requiredAtRuntime: true, dryRun: true,
            correlationId: Guid.Parse("a0557000-0000-4000-8000-000000000009"));
        var r055Dispatch = AreaUserEventRuntimeService.Dispatch(r055Area, r055Event, r055Payload,
            _forgeScriptRuntime, ResolveR055Source, requiredAtRuntime: true, dryRun: false,
            correlationId: Guid.Parse("a0557000-0000-4000-8000-000000000010"),
            execute: artifact => ExecuteForgeScript(artifact, this));
        var r055Map = AreaExperienceAuthoringScriptService.Reveal(r055Area,
            new(AreaMapExplorationScope.Party, "party:runtime", new HashSet<string>(), new HashSet<Guid>()),
            17, 3, r055Area.Authoring.Map.Pins[0].Id);
        var r055Runtime = AreaExperienceRuntimeResolver.Resolve(r055Area, true);
        var r055RenderPlan = AreaExperienceRenderPlanBuilder.Build(r055Area);
        var r055Legacy = area.WithAuthoring(AreaAuthoringProfile.CreateDefault(area.Id));
        var r055Optional = AreaExperienceRuntimeResolver.Resolve(r055Legacy, false);
        var r055Required = AreaExperienceRuntimeResolver.Resolve(r055Legacy, true);
        AssertSmoke(AreaExperienceAuthoringValidator.Validate(r055Area).Count == 0 &&
                    r055Snapshot.AudioSnapshots.Count == 1 && r055Snapshot.EventBindings.Count == 1 &&
                    r055Snapshot.MapPins == 1 && r055Snapshot.VisibilityRooms == 1 &&
                    r055Snapshot.Occluders == 1 && r055Snapshot.LodOverrides == 1 &&
                    r055Preview.Succeeded && !r055Preview.Executed &&
                    r055Dispatch.Succeeded && r055Dispatch.Executed &&
                    r055Dispatch.Deliveries.Single().Subscriber == r055Subscription &&
                    r055Map.RevealedCells.Contains("2:0") &&
                    r055Runtime.Handle is
                    {
                        AudioSnapshots: 1, UserEvents: 1, MapPins: 1,
                        VisibilityRooms: 1, Occluders: 1, LodOverrides: 1
                    } &&
                    r055Runtime.Diagnostics.Count == 0 && r055Optional.Diagnostics.Count == 0 &&
                    r055RenderPlan.MapFloors.Count == 2 && r055RenderPlan.MapFloors.All(floor =>
                        floor.Generate2D && floor.Generate3D && floor.FogOfWar) &&
                    r055RenderPlan.Rooms.Count == 1 && r055RenderPlan.Occluders.Count == 1 &&
                    r055RenderPlan.LodOverrides.Count == 1 && r055RenderPlan.Diagnostics.Count == 0 &&
                    r055Required.Diagnostics.Single().Code == AreaExperienceRuntimeResolver.RequiredMissingCode,
            "R055 area experience runtime optional/required contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R055_AREA_EXPERIENCE_PASS " +
                 "audio=snapshots-adaptive-reverb events=namespaced-typed-payload-live-forgescript " +
                 "map=2d-3d-exploration-fog-reveal visibility=rooms-portals-occluders-streaming " +
                 $"lod=thresholds-budgets-popping forgescript=custom-event-handle " +
                 $"source={(r055UsesModuleScript ? "module" : "isolated-smoke")} " +
                 "optional=ignored required=diagnostic");

        var r056Root = Guid.Parse("a0567000-0000-4000-8000-000000000001");
        var r056Child = Guid.Parse("a0567000-0000-4000-8000-000000000002");
        var r056Private = Guid.Parse("a0567000-0000-4000-8000-000000000003");
        var r056Entities = area.Entities.Take(2).Select(item => item.Id).ToArray();
        var r056Authoring = new AreaAuthoringProfile(r055Authoring.SchemaVersion, r055Authoring.UserEvents,
            r055Authoring.Map,
        [
            new(r056Root, "Runtime", null, true, false, "runtime", [], provenance: "runtime:r056"),
            new(r056Child, "Actors", r056Root, true, false, "gameplay", r056Entities,
                provenance: "runtime:r056"),
            new(r056Private, "Private", r056Root, false, true, "server", [],
                provenance: "runtime:r056", exportEnabled: false),
        ], r055Authoring.Experience, true);
        var r056Area = area.WithAuthoring(r056Authoring);
        var r056Snapshot = AreaLayerAuthoringService.Read(r056Area);
        var r056Selection = AreaLayerAuthoringService.SelectGroup(r056Area, r056Root);
        var r056Export = AreaLayerAuthoringService.PreviewPartialExport(r056Area, [r056Root]);
        var r056Runtime = AreaLayerRuntimeResolver.Resolve(r056Area, true);
        var r056LegacyAuthoring = new AreaAuthoringProfile(r055Authoring.SchemaVersion, r055Authoring.UserEvents,
            r055Authoring.Map, [], r055Authoring.Experience, false);
        var r056Legacy = area.WithAuthoring(r056LegacyAuthoring);
        var r056Optional = AreaLayerRuntimeResolver.Resolve(r056Legacy, false);
        var r056Required = AreaLayerRuntimeResolver.Resolve(r056Legacy, true);
        AssertSmoke(AreaLayerAuthoringValidator.Validate(r056Area).Count == 0 &&
                    r056Snapshot.Layers.Count == 3 && r056Snapshot.RuntimeMetadataRequired &&
                    r056Snapshot.Layers.Single(item => item.LayerId == r056Child).Depth == 1 &&
                    r056Snapshot.Layers.Single(item => item.LayerId == r056Private) is
                    { Visible: false, Locked: true, ExportEnabled: false } &&
                    r056Selection.LayerIds.Count == 3 && r056Selection.EntityIds.Count == 2 &&
                    r056Export.Layers.Count == 2 && r056Export.Entities.Count == 2 &&
                    r056Export.BlueprintDependencies.Count > 0 && r056Export.Diagnostics.Count == 0 &&
                    r056Runtime.Handle is { SchemaVersion: 1 } && r056Runtime.Diagnostics.Count == 0 &&
                    r056Optional.Diagnostics.Count == 0 &&
                    r056Required.Diagnostics.Single().Code == AreaLayerRuntimeResolver.RequiredMissingCode,
            "R056 area layer runtime optional/required contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R056_AREA_LAYERS_PASS " +
                 "hierarchy=nested visibility=inherited locking=inherited selection=group-descendants " +
                 "export=partial-governed forgescript=typed-handle optional=ignored required=diagnostic");

        var r057Script = new BlueprintPaletteScriptService(project);
        var r057Entity = area.Entities.First();
        var r057Resolved = r057Script.Resolve(r057Entity.Id, true);
        var r057Optional = r057Script.Resolve(EntityId.From(Guid.NewGuid()), false);
        var r057Required = r057Script.Resolve(EntityId.From(Guid.NewGuid()), true);
        var r057Asset = new AssetCatalogItem(
            AssetCatalogItemId.ForExternalId("runtime:r057-preview"), "runtime:r057-preview", "R057 Preview",
            "runtime", "r057-preview", "https://example.invalid/r057", "CC0-1.0",
            "https://creativecommons.org/publicdomain/zero/1.0/", true, new string('a', 64), 4096,
            ["3d-model"], ["preview", "palette"], "runtime-fixture", AssetAvailability.ExtractedAvailable,
            "models/r057.glb", "previews/r057.png", null, "runtime:r057",
            technicalMetadata: new AssetTechnicalMetadata(dimensions: new AssetDimensions(1, 2, 1),
                style: "stylized", skeleton: "humanoid", materials: ["stone"], triangleCount: 4200,
                variants: ["ruined"], animations: ["idle"], compatibility: ["medieval"]));
        var r057Preview = AssetPreviewPlanBuilder.Build(r057Asset, modelRequired: true);
        AssertSmoke(r057Resolved.Blueprint is not null && r057Resolved.Diagnostics.Count == 0 &&
                    r057Optional.Diagnostics.Count == 0 &&
                    r057Required.Diagnostics.Single().StartsWith(
                        BlueprintPaletteScriptService.RequiredBlueprintMissingCode, StringComparison.Ordinal) &&
                    r057Preview.CanRender3D && r057Preview.TurntableAngles.Count == 8 &&
                    r057Preview.Variants.Single() == "ruined" && r057Preview.Animations.Single() == "idle" &&
                    r057Preview.Diagnostics.Count == 0,
            "R057 palette runtime optional/required and preview contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R057_PALETTE_PASS " +
                 "blueprint=versioned-promoted-defaults instances=pins-exclusions-overrides " +
                 "taxonomy=hierarchical-multitag preview=turntable-thumbnail-variants-animations-metrics-license " +
                 "search=advanced-facets forgescript=typed-handle optional=ignored required=diagnostic");

        var r058Target = project.Blueprints.First(item => !item.IsReadOnly());
        var r058Base = project.Blueprints.FirstOrDefault(item => item.Id != r058Target.Id &&
            item.Kind == r058Target.Kind);
        var r058Project = project;
        if (r058Base is null)
        {
            r058Base = new BlueprintDefinition(
                BlueprintId.From(Guid.Parse("b0580000-0000-4000-8000-000000000400")),
                $"{r058Target.Slug}-r058-base", $"{r058Target.DisplayName} R058 Base", r058Target.Kind,
                r058Target.Combat, r058Target.PaletteOrigin, r058Target.IsFavorite, r058Target.Provenance,
                r058Target.CreaturePresentation, r058Target.CreatureProgression, r058Target.CreatureLoot,
                r058Target.CreatureRoleBehavior, r058Target.CreatureEvents, r058Target.ItemAdvanced,
                r058Target.Authoring);
            r058Project = new ProjectDefinition(project.Id, project.Title, project.Module,
                [.. project.Blueprints, r058Base], project.Areas, project.Dialogues, project.Quests);
        }
        var r058Variant = new PrefabVariantDefinition(
            Guid.Parse("b0580000-0000-4000-8000-000000000401"), 1, "runtime:r058", r058Base.Id,
            [new(PrefabVariantOverrideKind.Material, "body", "asset:ember"),
             new(PrefabVariantOverrideKind.Stat, "armor", "18")], true);
        var r058Composition = new PrefabCompositionDefinition(
            Guid.Parse("b0580000-0000-4000-8000-000000000402"), 1, "runtime:r058",
            [new(Guid.Parse("b0580000-0000-4000-8000-000000000403"), r058Base.Id, null, "floor",
                new TransformValue(new Vector3Value(2, 0, 3), QuaternionValue.Identity, Vector3Value.One))],
            ["socket-compatible"], ["intensity"], ["scripts/assemble.as"], true);
        var r058Budget = new BlueprintAssetBudgetProfile(
            Guid.Parse("b0580000-0000-4000-8000-000000000404"), 1, "runtime:r058",
            new AssetBudgetMetrics(12_000, 2, 3, 8_000_000, 1, 3, 2),
            [new AssetPlatformBudget(AssetTargetPlatform.Windows, 20_000, 4, 6, 16_000_000, 2, 4, 3),
             new AssetPlatformBudget(AssetTargetPlatform.Linux, 10_000, 4, 6, 16_000_000, 2, 4, 3,
                 GateBuild: false)], true);
        var r058State = new ProjectState(r058Project, new ProjectRevision(0));
        var r058Result = new ConfigureBlueprintPrefabHandler(new SystemClock()).Handle(r058State,
            new ConfigureBlueprintPrefabCommand(CommandId.From(Guid.NewGuid()), r058State.Revision, r058Target.Id,
                r058Variant, r058Composition, r058Budget, ["Runtime/Compositions"], false));
        var r058Runtime = new BlueprintPrefabRuntimeService(r058Result.State.Project);
        var r058ResolvedVariant = r058Runtime.ResolveVariant(r058Target.Id, true);
        var r058ResolvedComposition = r058Runtime.ResolveComposition(r058Target.Id, true);
        var r058Budgets = r058Runtime.EvaluateBudget(r058Target.Id, true);
        var r058OptionalVariant = r058Runtime.ResolveVariant(r058Base.Id, false);
        var r058RequiredVariant = r058Runtime.ResolveVariant(r058Base.Id, true);
        var r058OptionalComposition = r058Runtime.ResolveComposition(r058Base.Id, false);
        var r058RequiredComposition = r058Runtime.ResolveComposition(r058Base.Id, true);
        var r058OptionalBudget = r058Runtime.EvaluateBudget(r058Base.Id, false);
        var r058RequiredBudget = r058Runtime.EvaluateBudget(r058Base.Id, true);
        var r058Placement = PrefabCompositionPlacementPlanner.Build(r058Result.State.Project, r058Target.Id,
            area.Id, new Vector3Value(10, 0, 20), Guid.Parse("b0580000-0000-4000-8000-000000000405"));
        AssertSmoke(r058Result.Succeeded && r058ResolvedVariant.EffectiveOverrides.Count == 2 &&
                    r058ResolvedComposition.Nodes.Single().Transform.Position == new Vector3Value(2, 0, 3) &&
                    r058Budgets.Single(item => item.Platform == AssetTargetPlatform.Windows).Status ==
                    AssetBudgetEvaluationStatus.Passed &&
                    r058Budgets.Single(item => item.Platform == AssetTargetPlatform.Linux).Status ==
                    AssetBudgetEvaluationStatus.Warning && r058OptionalVariant.Diagnostics.Count == 0 &&
                    r058RequiredVariant.Diagnostics.Single().StartsWith(
                        BlueprintPrefabRuntimeService.RequiredVariantMissingCode, StringComparison.Ordinal) &&
                    r058OptionalComposition.Diagnostics.Count == 0 &&
                    r058RequiredComposition.Diagnostics.Single().StartsWith(
                        BlueprintPrefabRuntimeService.RequiredCompositionMissingCode, StringComparison.Ordinal) &&
                    r058OptionalBudget.Count == 0 &&
                    r058RequiredBudget.Single().ExceededMetrics.Single() ==
                        BlueprintPrefabRuntimeService.RequiredAssetBudgetMissingCode &&
                    r058Placement.Entities.Single().Transform.Position == new Vector3Value(12, 0, 23),
            "R058 prefab runtime optional/required and platform budget contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R058_PREFAB_PASS " +
                 "variant=inherited-typed-overrides composition=hierarchy-sockets-rules-parameters-scripts " +
                 "collections=author placement=deterministic-group budget=seven-metrics-platform-pass-warn-fail " +
                  "forgescript=typed-handle optional=ignored required=diagnostic");

        var r059Profile = EditorWorkspaceProfile.CreateDefault(EditorRole.Builder, area.Id);
        var r059Runtime = EditorProductivityRuntimeResolver.Resolve(r059Profile, true);
        var r059Optional = EditorProductivityRuntimeResolver.Resolve(null, false);
        var r059Required = EditorProductivityRuntimeResolver.Resolve(null, true);
        var r059Help = EditorContextualHelpCatalog.Search("placement");
        AssertSmoke(r059Runtime.Handle is
        {
            Role: EditorRole.Builder, RepeatedPlacementEnabled: true,
            ContextualHelpEnabled: true
        } &&
                    r059Runtime.Handle.Permissions.HasFlag(EditorPermission.EditAreas) &&
                    r059Optional.Diagnostics.Count == 0 &&
                    r059Required.Diagnostics.Single().Code == EditorProductivityRuntimeResolver.RequiredMissingCode &&
                    r059Help.Count > 0 && r059Help.All(topic => topic.Citations.Count > 0),
            "R059 editor productivity runtime optional/required and sourced-help contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R059_SHELL_PRODUCTIVITY_PASS " +
                 "filters=layers-volumes placement=brush-surface-atomic split=two-viewports-cross-area " +
                 "help=integrated-forgescript-dependencies-citations profiles=seven-panels-permissions " +
                 "optional=ignored required=diagnostic");

        var r060Policy = project.Module.RuntimePolicies.Extensions.OperationalReadiness.Recovery;
        var r060Runtime = RecoveryRuntimeResolver.Resolve(r060Policy, true);
        var r060Optional = RecoveryRuntimeResolver.Resolve(null, false);
        var r060Required = RecoveryRuntimeResolver.Resolve(null, true);
        var r060Selection = new RecoveryRestoreSelection(
            Guid.Parse("e0600000-0000-4000-8000-000000000002"), 1,
            RecoveryRestoreSection.ProjectAndModule | RecoveryRestoreSection.Areas, "runtime:r060");
        AssertSmoke(r060Runtime.Available && r060Runtime.Policy?.CompareBeforeRestore == true &&
                    r060Optional.Diagnostics.Count == 0 &&
                    r060Required.Diagnostics.Single().Code == RecoveryRuntimeResolver.RequiredMissingCode &&
                    r060Selection.Sections.HasFlag(RecoveryRestoreSection.Areas),
            "R060 recovery runtime optional/required and selective restore contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R060_RECOVERY_PASS " +
                 "policy=uuid-schema-provenance autosave=versioned-bounded session=unclean-diagnostic " +
                 "snapshot=hash-journal-quarantine compare=required selective=five-sections " +
                 "transaction=dry-run-apply-undo-redo optional=ignored required=diagnostic");

        var r061Resources = new[]
        {
            ProceduralAmbientAudioService.StreamResource,
            ProceduralAmbientAudioService.CanopyWindResource,
            ProceduralAmbientAudioService.BirdsongResource,
        };
        var r061Authoring = new AudioAuthoringProfile(
            Guid.Parse("a0616000-0000-4000-8000-000000000001"), 1, "runtime:r061",
            "builtin://openrpg/audio-profile", "CC0-1.0", new string('a', 64), true,
            new(2026, 1, new Dictionary<string, float>
            {
                [r061Resources[0]] = 5,
                [r061Resources[1]] = 2,
                [r061Resources[2]] = 1,
            }),
            new(4, 12, -1, 1, .8f, 1.2f, AudioModulationDistribution.Triangular,
                [new(0, 0), new(.5f, .3f), new(1, 1)], 2026, true, 120, 1),
            new(-18, "Ambience", -6, 1, .85f, AudioMeterMode.Loudness),
            new(_currentAreaId ?? string.Empty, "Ambience", AudioListenerPolicy.Personalized,
                new Dictionary<string, float> { ["local-player"] = .75f }));
        var r061Audio = new AreaAudioProfile(string.Empty, string.Empty, string.Empty,
            r061Resources[0], "neutral", 0, r061Resources, AudioOrderMode.Weighted, 8, 4, 1,
            new(AreaSoundMode.Global2D, 0, 0, 0, "linear"), authoring: r061Authoring);
        var r061Sequence = AreaAudioSequencePlanner.Plan(r061Audio, 32, 44);
        var r061Statistics = AreaAudioSequencePlanner.PreviewStatistics(r061Audio, 512, 44);
        var r061Mix = AreaAudioMixer.Evaluate(r061Audio, new(0, 12, string.Empty,
            new HashSet<string>(), 0, false, _currentAreaId ?? string.Empty, "local-player",
            SourceLoudnessLufs: -24));
        var r061Runtime = AudioAuthoringRuntimeResolver.Resolve(r061Authoring, true);
        var r061Optional = AudioAuthoringRuntimeResolver.Resolve(null, false);
        var r061Required = AudioAuthoringRuntimeResolver.Resolve(null, true);
        ConfigureAreaAmbientAudio(r061Audio);
        var r061Player = GetNodeOrNull<AudioStreamPlayer>("ForgeAreaAmbientAudio");
        AssertSmoke(r061Sequence.Count == 32 && r061Sequence.Zip(r061Sequence.Skip(1)).All(pair =>
                        pair.First.Resource != pair.Second.Resource) &&
                    r061Statistics.SampleCount == 512 && r061Statistics.MaximumDelaySeconds <= 12 &&
                    r061Mix.Bus == "Ambience" && r061Mix.Personalized && r061Mix.AccessibilityCapped &&
                    r061Runtime.Available && r061Optional.Diagnostics.Count == 0 &&
                    r061Required.Diagnostics.Single().Code == AudioAuthoringRuntimeResolver.RequiredMissingCode &&
                    r061Player is { Playing: true, Stream: AudioStreamWav },
            "R061 audio browser/selection/modulation/mix/listener runtime contract failed.");
        r061Player!.QueueFree();
        GD.Print("OPENRPG_FORGE_RUNTIME_R061_AUDIO_PASS " +
                 "selection=sequential-random-shuffle-weighted-seeded-no-repeat " +
                 "modulation=ranges-distributions-curves-tempo-statistics " +
                 "mix=lufs-bus-ducking-distance-cap-meter ambient=2d-area-listener-personalized-player " +
                 "playback=audio-stream-player optional=ignored required=diagnostic");

        var r062Master = Guid.Parse("a0626000-0000-4000-8000-000000000001");
        var r062Orchestration = new AreaAudioOrchestration(
        [
            new(MusicState.Exploration, ProceduralAmbientAudioService.CanopyWindResource,
                [ProceduralAmbientAudioService.BirdsongResource], 2, 1, true, 120, 4),
            new(MusicState.Combat, ProceduralAmbientAudioService.StreamResource,
                [ProceduralAmbientAudioService.CanopyWindResource], 1, 1, true, 120, 4),
        ],
        [new(r062Master, "Master", null, .8f, "default", "all", .75f, true, true)], []);
        var r062Audio = new AreaAudioProfile(string.Empty, string.Empty, string.Empty,
            ProceduralAmbientAudioService.BirdsongResource, "neutral", 1,
            [ProceduralAmbientAudioService.BirdsongResource], AudioOrderMode.Sequential, 1, 0, .6f,
            new(AreaSoundMode.Spatial3D, 2, 24, .08f, "inverse-square", true),
            new(true, AudioInstancePolicy.Restart, AudioAuthority.Server, 1.5f,
                "audio.started", "audio.stopped", "audio.paused", "audio.faded", "1.0"),
        [
            new(ProceduralAmbientAudioService.CanopyWindResource, 6, 18, string.Empty, string.Empty,
                0, false, .7f, .1f),
            new(ProceduralAmbientAudioService.StreamResource, 18, 6, "rain", string.Empty,
                0, false, .8f, .1f),
        ], r062Orchestration, authoring: r061Authoring with
        {
            Ambient = r061Authoring.Ambient with
            {
                AreaScope = _currentAreaId ?? string.Empty,
                ListenerPolicy = AudioListenerPolicy.EveryListener,
                ListenerGains = new Dictionary<string, float>(),
            },
        });
        var r062Range = AudioOrchestrationRuntimeService.PreviewAttenuation(r062Audio.Spatial, 17);
        var r062Control = AudioOrchestrationRuntimeService.Create(r062Audio.Authoring.Id);
        r062Control = AudioOrchestrationRuntimeService.Start(r062Control, r062Audio.RuntimeControl,
            AudioAuthority.Server);
        var r062ControlPlayer = new AudioStreamPlayer
        {
            Name = "ForgeR062ControlledAudio",
            Stream = LoadAudioStream(ProceduralAmbientAudioService.BirdsongResource),
        };
        AddChild(r062ControlPlayer);
        ApplyAudioPlaybackControl(r062ControlPlayer, r062Control);
        r062Control = AudioOrchestrationRuntimeService.Pause(r062Control, r062Audio.RuntimeControl,
            AudioAuthority.Server);
        ApplyAudioPlaybackControl(r062ControlPlayer, r062Control);
        var r062Paused = r062ControlPlayer.StreamPaused;
        r062Control = AudioOrchestrationRuntimeService.Resume(r062Control, r062Audio.RuntimeControl,
            AudioAuthority.Server);
        r062Control = AudioOrchestrationRuntimeService.Fade(r062Control, r062Audio.RuntimeControl,
            AudioAuthority.Server, .25f, .5f);
        ApplyAudioPlaybackControl(r062ControlPlayer, r062Control);
        var r062Denied = AudioOrchestrationRuntimeService.Start(
            AudioOrchestrationRuntimeService.Create(r062Audio.Authoring.Id), r062Audio.RuntimeControl,
            AudioAuthority.Local);
        var r062Day = new AudioListenerContext(0, 12, string.Empty, new HashSet<string>(), 0, false);
        var r062Night = new AudioListenerContext(0, 22, "rain", new HashSet<string>(), 0, false);
        var r062Crossfade = AudioOrchestrationRuntimeService.PlanAdaptiveCrossfade(r062Audio,
            r062Night, r062Day);
        var r062Music = AreaMusicStateMachine.Transition(r062Orchestration, MusicState.Exploration,
            MusicState.Combat, 10, 3.25f);
        ConfigureAreaAmbientAudio(r062Audio);
        var r062Debug = GetNodeOrNull<Node3D>("ForgeAudioRangeDebug");
        var r062Meters = MonitorRuntimeMix(r062Audio);
        GD.Print("OPENRPG_FORGE_RUNTIME_R062_AUDIO_PROBE " +
                 $"range={r062Range.DebugSpheres.Count}:{r062Range.Samples[^1].Gain:0.###} " +
                 $"debug={r062Debug?.GetChildCount() ?? -1} paused={r062Paused.ToString().ToLowerInvariant()} " +
                 $"playing={r062ControlPlayer.Playing.ToString().ToLowerInvariant()} " +
                 $"control={r062Control.Status}:{r062Control.Gain:0.###} denied={r062Denied.Diagnostics.Count} " +
                 $"crossfade={r062Crossfade.Entries.Count} adaptive={_adaptiveAmbiencePlayers.Count} " +
                 $"music={r062Music?.BeatSynchronized}:{r062Music?.StartDelaySeconds:0.###} " +
                 $"meters={r062Meters.Count}:{r062Meters.FirstOrDefault()?.Bus ?? "none"}");
        AssertSmoke(r062Range.DebugSpheres.Count == 2 && r062Range.Samples[^1].Gain == 0 &&
                    r062Debug?.GetChildCount() == 2 && r062Paused && r062ControlPlayer.Playing &&
                    r062Control.Status == AudioPlaybackStatus.Fading &&
                    r062Denied.Diagnostics.Single().Code ==
                    AudioOrchestrationRuntimeService.AuthorityDeniedCode &&
                    r062Crossfade.Entries.Count == 2 && _adaptiveAmbiencePlayers.Count >= 1 &&
                    r062Music is { BeatSynchronized: true, StartDelaySeconds: > 0 } &&
                    r062Meters.Single().Bus == "Master" &&
                    ForgeScriptApiCatalog.Signatures.Any(value =>
                        value.StartsWith("void audio_pause", StringComparison.Ordinal)),
            "R062 range, script control, ambience crossfade, music sync or live mix adapter failed.");
        r062ControlPlayer.QueueFree();
        GD.Print("OPENRPG_FORGE_RUNTIME_R062_AUDIO_PASS " +
                 "range=inner-outer-curve-air-debug-spheres-engine " +
                 "script=typed-handle-start-stop-pause-resume-fade-authority-events-api-v1-player " +
                 "ambience=time-weather-location-danger-indoor-volume-crossfade-players " +
                 "music=day-night-states-stems-cooldown-beat-sync " +
                 "mix=hierarchy-snapshot-platform-dynamic-accessibility-live-engine-monitor " +
                 "optional=ignored required=diagnostic");

        var r063Resource = ProceduralAmbientAudioService.BirdsongResource;
        var r063Caption = new SoundCaption(r063Resource,
            [new("fr", "[Chant d'oiseau à gauche]"), new("en", "[Birdsong to the left]")],
            true, "important", Guid.Parse("a0636000-0000-4000-8000-000000000001"), 1,
            "runtime:r063", "builtin://openrpg/audio-captions", "CC0-1.0", new string('b', 64),
            "ambient", 1.5f, 6, true);
        var r063Audio = new AreaAudioProfile(string.Empty, string.Empty, string.Empty,
            r063Resource, "neutral", 1, [r063Resource], orchestration: new([], [], [r063Caption]),
            authoring: r061Authoring with
            {
                Ambient = r061Authoring.Ambient with
                {
                    AreaScope = _currentAreaId ?? string.Empty,
                    ListenerPolicy = AudioListenerPolicy.EveryListener,
                    ListenerGains = new Dictionary<string, float>(),
                },
            });
        var r063Presentation = ShowAudioCaption(r063Audio, r063Resource, new(-5, 0, 0));
        var r063Optional = AudioCaptionRuntimeService.Resolve(r063Audio,
            ProceduralAmbientAudioService.StreamResource, "fr", "fr", Vector3Value.Zero,
            new(0, 0, -1), Vector3Value.Zero);
        var r063Required = AudioCaptionRuntimeService.Resolve(r063Audio,
            ProceduralAmbientAudioService.StreamResource, "fr", "fr", Vector3Value.Zero,
            new(0, 0, -1), Vector3Value.Zero, true);
        AssertSmoke(AudioCaptionValidator.Validate(r063Audio).Count == 0 &&
                    r063Presentation.Presentation is { Locale: "fr", CueKind: "ambient" } &&
                    _hud?.CaptionText.Contains("[Chant d'oiseau à gauche]", StringComparison.Ordinal) == true &&
                    r063Optional.Diagnostics.Count == 0 && r063Required.Diagnostics.Single().Code ==
                    AudioCaptionRuntimeService.RequiredMissingCode &&
                    ForgeScriptApiCatalog.Signatures.Any(value =>
                        value.StartsWith("AudioCaptionRef audio_caption", StringComparison.Ordinal)),
            "R063 localized directional sound caption HUD adapter failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R063_AUDIO_CAPTION_PASS " +
                 "metadata=dialogue-effect-ambient-locales-fallback-importance-direction-duration " +
                 "hud=visible-directional-accessible engine=audio-caption-overlay " +
                 "forgescript=typed-handle optional=ignored required=diagnostic");
    }

    private static void RunAreaRuntimeValidationSmokeTest(ProjectDefinition project)
    {
        var source = project.Areas.First();
        var sourceEntity = source.Entities.First();
        var destinationId = AreaId.From(Guid.Parse("a0294000-0000-4000-8000-000000000001"));
        var destinationEntity = new EntityInstance(
            EntityId.From(Guid.Parse("a0294000-0000-4000-8000-000000000002")), sourceEntity.BlueprintId,
            new TransformValue(new Vector3Value(24, 0, 12), QuaternionValue.Identity, Vector3Value.One));
        var destination = new AreaDefinition(destinationId, "runtime-r029-destination", "R029 destination",
            [destinationEntity]);
        var portal = new PortalLink(PortalId.From(Guid.Parse("a0294000-0000-4000-8000-000000000003")),
            "Runtime crossing", sourceEntity.Id, destination.Id, destinationEntity.Id, true, false,
            provenance: "runtime:r029-smoke", orientation: PortalOrientationPolicy.FixedYaw,
            fixedYawDegrees: 90, velocity: PortalVelocityPolicy.RedirectForward,
            beforeTransitionScript: "scripts/portal_before.forge");
        var sourceWithPortal = source.WithPortals([.. source.Portals, portal]);
        var runtimeProject = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
            [.. project.Areas.Select(item => item.Id == source.Id ? sourceWithPortal : item), destination],
            project.Dialogues, project.Quests);
        var travel = AreaOperationsScriptService.PreviewPortal(runtimeProject, source.Id, portal.Id,
            new Vector3Value(3, 0, 4), 10);
        var transition = AreaTransitionRuntimeService.TransitionAsync(runtimeProject, source.Id, portal.Id,
            null, new Vector3Value(3, 0, 4), 10).GetAwaiter().GetResult();
        var navigation = AreaOperationsScriptService.ReadNavigation(runtimeProject, source.Id);
        var readiness = AreaOperationsScriptService.Validate(runtimeProject, source.Id);
        var playerBlueprint = runtimeProject.Blueprints.FirstOrDefault(item => item.Kind == BlueprintKind.PlayerSpawn);
        var profile = playerBlueprint is null ? source.Playtest : new AreaPlaytestProfile(source.Playtest.Id,
            AreaPlaytestProfile.CurrentSchemaVersion, "runtime:r029-smoke", true, false,
            PlayFromHereStartMode.Camera, null,
            new TransformValue(new Vector3Value(4, 2, -3), QuaternionValue.Identity, Vector3Value.One),
            null, playerBlueprint.Id, "runtime-smoke", []);
        IReadOnlyList<DomainDiagnostic> playtestDiagnostics = playerBlueprint is null
            ? []
            : AreaPlaytestProfileValidator.Validate(runtimeProject, source, profile);
        AssertSmoke(travel.DestinationAreaId == destination.Id && travel.ArrivalYawDegrees == 90 &&
                    MathF.Abs(travel.ArrivalVelocity.X - 5) < 0.001f && transition.Succeeded &&
                    navigation.AgentProfiles == 4 && navigation.WalkableCells > 0 &&
                    readiness.Checks.SequenceEqual(AreaReadinessService.RequiredChecks) &&
                    readiness.CategoryCounts.Count == 9 && playtestDiagnostics.Count == 0,
            "Area transition, surface navigation, Play From Here or readiness contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R029_AREA_RUNTIME_VALIDATION_PASS " +
                 "portal=orientation-velocity-bidirectional-hooks lifecycle=streaming-exit-enter " +
                 "navigation=surface-agents-links-polygons play_from_here=spawn-camera-waypoint-manifest-quest " +
                 "validation=9-categories optional=ignored required=diagnostic forgescript=typed");
    }

    private static void RunAgentSemanticWorkflowRuntimeSmokeTest()
    {
        var workflow = AgentSemanticWorkflow.CreateDefault();
        var target = Guid.Parse("a2000000-0000-4000-8000-000000000901");
        var plan = new StructuredChangePlan(
            Guid.Parse("a2000000-0000-4000-8000-000000000902"),
            Guid.Parse("a2000000-0000-4000-8000-000000000903"), 1, "runtime:r020", 0,
            AssistantPermissionLevel.ApplyApprovedPlan, "Validate semantic runtime contract",
            [new AgentSemanticCommand(Guid.Parse("a2000000-0000-4000-8000-000000000904"),
                "area.add_entity", target, new Dictionary<string, string> { ["blueprint"] = "warden" })],
            ["fixture target"], [target], ["none"], ["domain validation"]);
        AssertSmoke(AgentSemanticWorkflowValidator.Validate(workflow).Count == 0 &&
                    AgentSemanticWorkflowValidator.ValidatePlan(plan, workflow).Count == 0 &&
                    workflow.ContextResources.Count == 10 && workflow.Tools.Count == 5 &&
                    workflow.DryRun.ProhibitWrites,
            "Agent semantic workflow runtime contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R020_AGENT_WORKFLOW_PASS " +
                 "context=10 graph=typed tools=5 plan=structured dry_run=no-write " +
                 "optional=default-safe required=diagnostic");
        var authorization = AgentExecutionGovernanceValidator.Authorize(workflow, plan,
            new AgentExecutionAuthorizationRequest("offline-ci", false, true, 32, 16, 0, 1,
                [$"areas/{target:D}"]));
        var denied = AgentExecutionGovernanceValidator.Authorize(workflow, plan,
            new AgentExecutionAuthorizationRequest("offline-ci", false, false, 32, 16, 0, 1,
                [$"areas/{target:D}"]));
        AssertSmoke(AgentExecutionGovernanceValidator.Validate(workflow.EffectiveGovernance, workflow).Count == 0 &&
                    authorization.Succeeded && !denied.Succeeded &&
                    denied.Diagnostics.Any(item => item.Code == AgentExecutionGovernanceValidator.PermissionDeniedCode) &&
                    AgentExecutionGovernanceValidator.IsSensitive("AuthorizationToken",
                        workflow.EffectiveGovernance.BudgetPrivacy),
            "Agent transaction governance runtime contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R021_AGENT_GOVERNANCE_PASS " +
                 "transaction=atomic-preview-required rollback=canonical diff=typed-hashed " +
                 "permission=confirmed-autonomous-bounded audit=redacted budget=provider-token-cost " +
                 "privacy=classification-opt-in-no-secret optional=default-safe required=diagnostic");
        var profile = AssistantWorkspaceProfile.CreateDefault();
        var prompt = profile.Prompts[0];
        var rendered = AssistantPromptRenderer.Render(prompt, prompt.Tests[0].Variables);
        var responseRequest = new AssistantProviderRequest(
            plan.Id, plan.TransactionId, rendered, AssistantPermissionLevel.ApplyApprovedPlan, 1024);
        var response = new AssistantProviderResponse("offline-ci", "forge-deterministic-v1",
            JsonSerializer.Serialize(new
            {
                summary = "Runtime safety contract",
                semanticDiff = Array.Empty<object>(),
                planId = plan.Id,
                transactionId = plan.TransactionId,
                permission = AssistantPermissionLevel.ApplyApprovedPlan.ToString(),
                promptSha256 = rendered.ContentSha256,
            }), 10, 10, 0, "runtime:r022");
        var structured = AgentResponseSafetyService.ValidateStructuredResult(prompt, response, responseRequest, workflow);
        var offline = AgentResponseSafetyService.EvaluateOfflineReadiness(profile, profileWasMissing: true);
        AssertSmoke(structured.Succeeded && !structured.Repaired && offline.Ready &&
                    offline.ProfileWasMissing && !offline.NetworkRequired &&
                    !offline.AiMetadataRequiredByBuild && !offline.AiMetadataRequiredByRuntime,
            "Agent output safety and offline runtime contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R022_AGENT_SAFETY_PASS " +
                 "output=validated injection=data-boundary permission=no-escalation " +
                 "offline=manual-build-runtime network=not-required python=no-public-exec " +
                 "optional=default-safe required=diagnostic");
    }

    private static void RunBuildCollaborationRuntimeSmokeTest()
    {
        var profile = WorkspaceBuildCollaborationProfile.CreateDefault();
        var invalid = profile with
        {
            References = profile.References with { IncludeReverseReferences = false },
        };
        var before = new[]
        {
            new WorkspaceSemanticResource("area:runtime", "area", "Runtime", new string('a', 64)),
        };
        var after = new[]
        {
            before[0] with { Sha256 = new string('b', 64) },
        };
        var diff = WorkspaceBuildCollaborationService.Diff(before, after, profile);
        AssertSmoke(WorkspaceBuildCollaborationProfileValidator.Validate(profile).Count == 0 &&
                    WorkspaceBuildCollaborationProfileValidator.Validate(invalid)
                        .Any(item => item.Code == WorkspaceBuildCollaborationProfileValidator.ReferencesInvalidCode) &&
                    diff.Succeeded && diff.Changes.Count == 1,
            "Build collaboration runtime optional/required contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R023_BUILD_COLLABORATION_PASS " +
                 "cache=ignored-generated normalize=tooling-only migration=versioned " +
                 "references=rebuildable diff=structured optional=ignored required=diagnostic");
    }

    private static void RunProjectFoundationRuntimeSmokeTest(ProjectDefinition project)
    {
        var optional = PlayerStartProfileResolver.Resolve(null,
            new PlayerStartSelectionContext("player-one", "campaign", "adventurer", new HashSet<string>()));
        var area = project.Areas[0];
        var spawn = area.Entities.First(entity =>
            project.Blueprints.First(blueprint => blueprint.Id == entity.BlueprintId).Kind == BlueprintKind.PlayerSpawn);
        var foundation = ProjectFoundationProfile.CreateRpgDefault(
            Guid.Parse("a0240000-0000-4000-8000-000000000024"), project.Id, project.Module.Slug,
            area.Id, spawn.Id, $"module.{project.Module.Slug}");
        var selected = PlayerStartProfileResolver.Resolve(foundation,
            new PlayerStartSelectionContext("player-one", "campaign", "adventurer", new HashSet<string>()));
        var missing = PlayerStartProfileResolver.Resolve(foundation,
            new PlayerStartSelectionContext("player-one", "arena", "mage", new HashSet<string>()));
        var core = ProjectFoundationProfile.CreateCoreDefault(
            Guid.Parse("a0240000-0000-4000-8000-000000000025"), project.Id, project.Module.Slug,
            ProjectTemplateKind.CoreMenu);
        AssertSmoke(optional.Succeeded && optional.Profile is null &&
                    ProjectFoundationValidator.Validate(foundation, project).Count == 0 &&
                    selected.Succeeded && selected.Profile?.AreaId == area.Id &&
                    !missing.Succeeded && missing.Diagnostic?.Code ==
                    ProjectFoundationValidator.PlayerStartReferenceCode &&
                    ProjectFoundationValidator.Validate(core, project).Count == 0 &&
                    core.Identity.ProjectId == project.Id && !core.RequiresInitialArea,
            "Project foundation runtime optional/required and target-aware contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R024_PROJECT_FOUNDATION_PASS " +
                 "optional=ignored required=diagnostic identity=stable area=target-aware " +
                 "spawn=player-context");
    }

    private void RunProjectOperationsRuntimeSmokeTest(ProjectDefinition project)
    {
        var sourcePolicies = project.Module.RuntimePolicies;
        var lifecycleBindings = sourcePolicies.LifecycleEvents.Bindings.Select(binding =>
            binding.Event == RuntimeLifecycleEventKind.ProjectLoaded && binding.Authority == RuntimeAuthority.Server
                ? binding with
                {
                    Handler = "main",
                    ScriptPath = "scripts/runtime_project_loaded.forge",
                    RequiredAtRuntime = true,
                    Provenance = "runtime:ux0-f",
                }
                : binding).ToArray();
        var lifecycleProfile = new ProjectLifecycleEventProfile(sourcePolicies.LifecycleEvents.Id,
            sourcePolicies.LifecycleEvents.SchemaVersion, "runtime:ux0-f", true, false, lifecycleBindings);
        var configuredPolicies = new ModuleRuntimePolicyProfile(sourcePolicies.Id, sourcePolicies.SchemaVersion,
            sourcePolicies.Heartbeats, sourcePolicies.Rest, sourcePolicies.SequenceAbort, "runtime:ux0-f",
            sourcePolicies.Cinematics, sourcePolicies.Gameplay, sourcePolicies.Rules, sourcePolicies.Extensions,
            sourcePolicies.GameplayEvents, sourcePolicies.World, lifecycleProfile);
        var configuredProject = new ProjectDefinition(project.Id, project.Title,
            project.Module.WithRuntimePolicies(configuredPolicies), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
        var lifecycleValid = new[] { RuntimeAuthority.Editor, RuntimeAuthority.Client, RuntimeAuthority.Server }
            .All(authority =>
            {
                var observed = new List<RuntimeLifecycleEvent>();
                var lifecycle = new RuntimeLifecycleCoordinator(observers: [observed.Add]);
                var loaded = lifecycle.LoadProject(configuredProject, authority, RuntimeLifecycleCause.Playtest);
                var entered = lifecycle.EnterInitialArea(configuredProject.Module.EntryAreaId, null,
                    RuntimeLifecycleCause.Playtest);
                var started = lifecycle.StartWorld(RuntimeLifecycleCause.Playtest);
                return loaded.Succeeded && entered.Succeeded && started.Succeeded &&
                       observed.Select(item => item.Binding)
                           .SequenceEqual(["project.loaded", "area.entered", "world.started"]) &&
                       observed.All(item => item.Authority == authority) &&
                       (authority != RuntimeAuthority.Server ||
                        observed[0].ScriptPath == "scripts/runtime_project_loaded.forge" &&
                        observed[0].ScriptHandler == "main" && observed[0].ScriptRequired);
            });

        var typedVariable = new ProjectVariableDefinition(
            ProjectVariableId.From(Guid.Parse("f8250000-0000-4000-8000-000000000002")),
            "runtime.r025.ready", ProjectVariableValueKind.Boolean, ProjectVariableScope.Session, "false",
            "Validation runtime R025.", "runtime-smoke", 1,
            ProjectVariableReplicationMode.ServerAuthority, debugVisible: true, requiredAtRuntime: true);
        var strictBlackboard = ProjectBlackboardScriptService.ForRuntime([typedVariable]);
        var legacyBlackboard = ProjectBlackboardScriptService.ForRuntime([]);
        var blackboardValid = !strictBlackboard.UsesLegacyCompatibility &&
                              !strictBlackboard.TrySet("legacy.unknown", "1", out _) &&
                              strictBlackboard.TrySet(typedVariable.Key, "true", out _) &&
                              legacyBlackboard.UsesLegacyCompatibility &&
                              legacyBlackboard.TrySet("legacy.unknown", "1", out _);
        var packageResolution = ContentPackageResolver.Resolve(project.Module.Content.Packages);
        var packageValid = packageResolution.Diagnostics.Count == 0;
        var profiles = project.Module.Distribution.TargetProfiles;
        var targetsValid = profiles.Count == Enum.GetValues<ModuleTargetKind>().Length &&
                           profiles.Select(item => item.Target).Distinct().Count() == profiles.Count &&
                           profiles.Select(item => item.SecretsProfile)
                               .Distinct(StringComparer.OrdinalIgnoreCase).Count() == profiles.Count;
        var firstCompilation = _forgeScriptCache?.Compile("void main() {}");
        var secondCompilation = _forgeScriptCache?.Compile("void main() {}");
        var cacheValid = firstCompilation is not null && secondCompilation is not null &&
                         firstCompilation.Compilation.Success && secondCompilation.Compilation.Success &&
                         firstCompilation.CacheKey == secondCompilation.CacheKey && secondCompilation.CacheHit;

        AssertSmoke(lifecycleValid && blackboardValid && packageValid && targetsValid && cacheValid,
            "Project operations runtime contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R025_PROJECT_OPERATIONS_PASS " +
                 "lifecycle=editor-client-server-ordered blackboard=strict-schema-legacy-bounded " +
                 "packages=lock-provenance cache=source-api-compiler-hit " +
                 "targets=five-secrets-separated optional=ignored required=diagnostic scripts=context-bound");
    }

    private static void RunProjectCompatibilityRuntimeSmokeTest(ProjectDefinition project)
    {
        var profile = ProjectCompatibilityProfile.CreateDefault() with
        {
            MinimumRuntimeVersion = ModuleRuntimeCompatibility.CurrentRuntimeVersion,
        };
        var manifest = ModuleManifest.Create(project, project.Module.Metadata.Version,
            profile.MinimumRuntimeVersion, "1.0.0", new string('a', 64), runtimeCompatibilityRequired: true);
        var futureRequired = manifest with { RuntimeMinimumVersion = "99.0.0" };
        var futureOptional = futureRequired with { RuntimeCompatibilityRequired = false };
        var scriptHandle = new ProjectCompatibilityScriptService().Read(
            new WorkspaceBuildProfile(WorkspaceBuildProfile.DefaultId, WorkspaceBuildProfile.CurrentSchemaVersion,
                "runtime:r026", true, true, true, true, WorkspaceBuildProfile.CreateDefault().Layout,
                WorkspaceBuildProfile.RequiredCliOperations, true, true, true, true, true, true, ".forge", true,
                compatibility: profile));
        AssertSmoke(ProjectCompatibilityProfileValidator.Validate(profile).Count == 0 &&
                    scriptHandle.Id == profile.Id && scriptHandle.AutomaticMigrations &&
                    ModuleRuntimeCompatibility.Evaluate(manifest,
                        ModuleRuntimeCompatibility.CurrentRuntimeVersion) is null &&
                    ModuleRuntimeCompatibility.Evaluate(futureRequired,
                        ModuleRuntimeCompatibility.CurrentRuntimeVersion)?.Code ==
                    ModuleRuntimeCompatibility.IncompatibleRuntimeCode &&
                    ModuleRuntimeCompatibility.Evaluate(futureOptional,
                        ModuleRuntimeCompatibility.CurrentRuntimeVersion) is null,
            "Project compatibility runtime optional/required contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R026_PROJECT_COMPATIBILITY_PASS " +
                 "profile=uuid-schema-provenance runtime-min=manifest project-format=versioned " +
                 "migration=automatic-versioned runtime=required-diagnostic-optional-safe " +
                 "forgescript=typed-handle");
    }

    private static void RunProjectGameplayEventsRuntimeSmokeTest(ProjectDefinition project)
    {
        var source = project.Module.RuntimePolicies.GameplayEvents;
        var bindings = source.Bindings.Select(binding => binding.EventName == "item.used"
            ? binding with
            {
                Handler = "main",
                ScriptPath = "scripts/runtime_item_used.forge",
                RequiredAtRuntime = true,
                Provenance = "runtime:ux0-f",
            }
            : binding).ToArray();
        var profile = new ProjectGameplayEventProfile(source.Id, source.SchemaVersion, "runtime:ux0-f",
            source.Enabled, source.RequiredAtRuntime, source.UseDownedState, source.AutoRespawn,
            source.ReplicateEquipmentAndProgression, bindings);
        var handle = ProjectGameplayEventProfileRuntime.Resolve(profile, true);
        var optional = ProjectGameplayEventProfileRuntime.Resolve(null, false);
        var required = ProjectGameplayEventProfileRuntime.Resolve(null, true);
        var bus = new ProjectGameplayEventBus();
        var now = DateTimeOffset.Parse("2026-08-10T00:00:00Z");
        ProjectGameplayEvent[] events =
        [
            new PlayerConnectionEvent(Guid.NewGuid(), "player:smoke", PlayerConnectionStage.Joining,
                "authenticated", EventAuthorityScope.Server, 1, now),
            new InventoryChangedEvent(Guid.NewGuid(), "player:smoke", "item:potion",
                InventoryChangeKind.Acquired, 1, "loot", "chest:smoke", EventAuthorityScope.Server, 2, now),
            new ItemUsedEvent(Guid.NewGuid(), "player:smoke", "item:potion", null, "player:smoke", 1,
                "use", EventAuthorityScope.Server, 3, now),
            new EquipmentChangedEvent(Guid.NewGuid(), "player:smoke", "item:sword", "main-hand",
                EquipmentChangeKind.Equipped, "loadout", EventAuthorityScope.Server, 4, now),
            new ProgressionChangedEvent(Guid.NewGuid(), "player:smoke", 1, 2, "xp",
                EventAuthorityScope.Server, 5, now),
        ];
        var published = events.Select(item => bus.Publish(item, profile)).ToArray();
        var lifecycle = ProjectGameplayEventBus.CreateDeathPipeline(profile, "player:smoke", "trap", 6, now,
            "spawn:safe");
        var lifecyclePublished = lifecycle.Select(item => bus.Publish(item, profile)).ToArray();
        var wrongAuthority = bus.Publish(new PlayerConnectionEvent(Guid.NewGuid(), "player:smoke",
            PlayerConnectionStage.Ready, "forged", EventAuthorityScope.Local, 20, now), profile);

        AssertSmoke(handle.Available && handle.Handle!.Bindings.Count == 14 &&
                    handle.Handle.Bindings["item.used"].ScriptPath == "scripts/runtime_item_used.forge" &&
                    handle.Handle.Bindings["item.used"].Handler == "main" &&
                    handle.Handle.Bindings["item.used"].RequiredAtRuntime && optional.Diagnostics.Count == 0 &&
                    optional.Handle is null && required.Diagnostics.Single().Code ==
                    ProjectGameplayEventProfileValidator.MissingRequiredCode &&
                    published.All(item => item.Accepted) && lifecyclePublished.All(item => item.Accepted) &&
                    lifecycle.Select(item => item.Stage).SequenceEqual(Enum.GetValues<LifeCycleStage>()) &&
                    !wrongAuthority.Accepted && wrongAuthority.DiagnosticCode ==
                    ProjectGameplayEventProfileValidator.BindingContractInvalidCode,
            "Project gameplay-event runtime contract failed.");
        GD.Print(
            "OPENRPG_FORGE_RUNTIME_R051_PROJECT_EVENTS_PASS bindings=14 authority=server " +
            "families=connection-inventory-use-death-respawn-equipment-progression " +
            "transaction=veto-journal pipeline=configurable forgescript=typed-handle " +
            "optional=ignored required=diagnostic scripts=path-handler-resolved");
    }

    private static void RunUx0GGlobalEventsRuntimeSmokeTest(ProjectDefinition project)
    {
        const string scriptPath = "scripts/ux0_g_global_events.forge";
        var moduleId = project.Module.Id;
        var gameplaySource = project.Module.RuntimePolicies.GameplayEvents;
        var gameplayBindings = gameplaySource.Bindings.Select(binding =>
            binding.EventName is "equipment.changed" or "progression.changed"
                ? binding with
                {
                    Handler = "main",
                    ScriptPath = scriptPath,
                    RequiredAtRuntime = true,
                    Provenance = "runtime:ux0-g",
                }
                : binding).ToArray();
        var gameplay = new ProjectGameplayEventProfile(gameplaySource.Id, gameplaySource.SchemaVersion,
            "runtime:ux0-g", true, false, gameplaySource.UseDownedState, gameplaySource.AutoRespawn,
            false, gameplayBindings);
        var eventBus = new ProjectGameplayEventBus();
        var now = DateTimeOffset.UnixEpoch;
        var equipment = eventBus.Publish(new EquipmentChangedEvent(Guid.NewGuid(), "player:smoke",
            "item:sword", "main-hand", EquipmentChangeKind.Equipped, "runtime-smoke",
            EventAuthorityScope.Server, 1, now), gameplay);
        var progression = eventBus.Publish(new ProgressionChangedEvent(Guid.NewGuid(), "player:smoke",
            1, 2, "runtime-smoke", EventAuthorityScope.Server, 2, now), gameplay);

        var defaults = ModuleRuntimePolicyProfile.CreateDefault(moduleId);
        var restBindings = defaults.Rest.Bindings.Select(binding => binding with
        {
            Handler = "main",
            ScriptPath = scriptPath,
            RequiredAtRuntime = binding.EventName == "rest.completed",
            Provenance = "runtime:ux0-g",
        }).ToArray();
        var rest = new RestLifecycleProfile(defaults.Rest.Id, defaults.Rest.SchemaVersion, true,
            PartyRestPolicy.WholeParty, 12, restBindings, "runtime:ux0-g");

        var heartbeat = new HeartbeatScheduleDefinition(Guid.NewGuid(),
            HeartbeatScheduleDefinition.CurrentSchemaVersion, "Simulation du monde", HeartbeatScope.Module,
            true, 1_500, 25, true, "1.0", "main", "runtime:ux0-g", scriptPath, true);
        var heartbeatDispatch = new HeartbeatScheduler().Advance([heartbeat], 1_500, true)
            .Dispatches.Single();

        var schema = new Forge.Domain.Lifecycle.CustomEventSchema(Guid.NewGuid(),
            Forge.Domain.Lifecycle.CustomEventSchema.CurrentSchemaVersion,
            "runtime:ux0-g", "module", "studio_signal", 1,
            new Dictionary<string, string> { ["message"] = "string" }, false,
            "1.0", "main", EventAuthorityScope.Server, scriptPath, true);
        var worldSource = project.Module.RuntimePolicies.World;
        var world = new ProjectWorldProfile(moduleId.Value, worldSource.SchemaVersion, "runtime:ux0-g",
            true, false, [schema], worldSource.Clock);
        var worldBus = new CustomWorldEventBus();
        worldBus.Register(world);
        CustomWorldEvent? observed = null;
        worldBus.Subscribe(schema.Id, value => observed = value);
        var userEvent = worldBus.Publish(schema.Id, 1,
            new Dictionary<string, string> { ["message"] = "bonjour" }, now);

        var scriptRuntime = new NativeForgeScriptRuntime();
        var compilation = scriptRuntime.Compile("void main() { say(\"UX0-G\"); }");
        var execution = compilation.Artifact is null ? null : scriptRuntime.Execute(compilation.Artifact,
            new ForgeScriptQuotas(2_000, 50, 16, 8));

        AssertSmoke(equipment.Accepted && progression.Accepted &&
                    gameplay.Bindings.Where(item =>
                            item.EventName is "equipment.changed" or "progression.changed")
                        .All(item => item.Authority == EventAuthorityScope.Server &&
                                     item.ScriptPath == scriptPath && item.Handler == "main") &&
                    rest.Enabled && rest.Bindings.Count == 4 && rest.Bindings.All(item =>
                        item.ScriptPath == scriptPath && item.Authority == EventAuthorityScope.Server) &&
                    heartbeatDispatch.ScriptPath == scriptPath && heartbeatDispatch.RequiredAtRuntime &&
                    heartbeatDispatch.BudgetMilliseconds == 25 && observed == userEvent &&
                    schema.ScriptPath == scriptPath && schema.RequiredAtRuntime &&
                    ProjectWorldProfileValidator.Validate(world, moduleId.Value).Count == 0 &&
                    execution?.Status == ForgeScriptExecutionStatus.Completed,
            "UX0-G global event runtime contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_UX0G_GLOBAL_EVENTS_PASS " +
                 "equipment_progression=typed-server rest=four-stage-resolved " +
                 "heartbeat=bounded-script-path user_event=namespaced-typed-script-path " +
                 "forgescript=compiled-executed");
    }

    private void RunUx0HWorldTransitionsRuntimeSmokeTest(ProjectDefinition project)
    {
        var moduleId = project.Module.Id.Value;
        var areaId = project.Module.EntryAreaId.ToString();
        var scriptRoot = _moduleContentRoot is null ? null : Path.Combine(_moduleContentRoot, "scripts");
        var fullScriptPath = scriptRoot is not null && Directory.Exists(scriptRoot)
            ? Directory.EnumerateFiles(scriptRoot, "*.forge", SearchOption.AllDirectories)
                .FirstOrDefault(path => System.Text.RegularExpressions.Regex.IsMatch(
                    File.ReadAllText(path), @"\bvoid\s+main\s*\(",
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            : null;
        if (fullScriptPath is null)
        {
            GD.Print("OPENRPG_FORGE_RUNTIME_UX0H_WORLD_TRANSITIONS_SKIPPED reason=no-forgescript-fixture");
            return;
        }
        var relativeScript = Path.GetRelativePath(_moduleContentRoot!, fullScriptPath).Replace('\\', '/');

        var abortSource = project.Module.RuntimePolicies.SequenceAbort;
        var abortBinding = abortSource.Binding with
        {
            Handler = "main",
            ScriptPath = relativeScript,
            RequiredAtRuntime = true,
            Provenance = "runtime:ux0-h",
        };
        var abort = new SequenceAbortPolicy(abortSource.Id, abortSource.SchemaVersion, true,
            abortBinding, SequenceAbortRestoreFlags.All, 32, "runtime:ux0-h");
        var snapshot = new SequenceRuntimeState("camera.exploration", [], new HashSet<string>(),
            new HashSet<string>(), 2, Guid.NewGuid().ToString("D"), false);
        var current = snapshot with
        {
            CameraId = "camera.cinematic",
            ControlStack = [new ControlContext(Guid.NewGuid(), 1, "runtime:ux0-h", "cinematic",
                false, "cinematic", [])],
            TemporarilyInvulnerableActors = new HashSet<string> { "player:local" },
            LockIds = new HashSet<string> { "door:story" },
            NarrativeRevision = 12,
            SequenceActive = true,
        };
        var aborted = SequenceAbortCoordinator.Abort(abort, current, snapshot);
        var abortScriptExecuted = DispatchConfiguredEventScript(abort.Binding.ScriptPath,
            abort.Binding.Handler, abort.Binding.RequiredAtRuntime, "ux0-h:sequence-aborted");

        var worldSource = project.Module.RuntimePolicies.World;
        var clock = new WorldClockProfile(worldSource.Clock.Id, worldSource.Clock.SchemaVersion,
            "runtime:ux0-h", 30, 10, 20, 50, 2.5, false, 95,
            new Dictionary<string, int>(StringComparer.Ordinal) { [areaId] = 25 },
            new Dictionary<WorldAstronomicalEventKind, int>
            {
                [WorldAstronomicalEventKind.Dawn] = 125,
                [WorldAstronomicalEventKind.Sunrise] = 150,
                [WorldAstronomicalEventKind.Sunset] = 750,
                [WorldAstronomicalEventKind.Dusk] = 800,
                [WorldAstronomicalEventKind.Midnight] = 0,
            });
        var world = new ProjectWorldProfile(moduleId, worldSource.SchemaVersion, "runtime:ux0-h",
            true, false, worldSource.CustomEvents, clock);
        var initialClock = CustomWorldEventBus.CreateInitialSnapshot(clock);
        var advancedClock = CustomWorldEventBus.AdvanceClock(clock, initialClock, 2, areaId);

        var progressionSource = project.Module.RuntimePolicies.Rules.Progression;
        var progression = new ProgressionPolicy(progressionSource.Id, progressionSource.SchemaVersion,
            progressionSource.RulesetId,
            [new ProgressionCurvePoint(1, 0), new ProgressionCurvePoint(2, 120),
                new ProgressionCurvePoint(3, 360), new ProgressionCurvePoint(4, 840)],
            1.2, .9, new Dictionary<string, double>(StringComparer.Ordinal)
            {
                ["combat"] = 1.1,
                ["exploration"] = .8,
                ["quest"] = 1.4,
            }, true, "runtime:ux0-h");
        var progressionResult = RulesRuntimeService.ResolveProgression(progression, 200, "quest", 2, 0);

        var worldDiagnostics = ProjectWorldProfileValidator.Validate(world, moduleId,
            new HashSet<string>(StringComparer.Ordinal) { areaId });
        var clockAdvanced = advancedClock.Snapshot.TotalMinutes == 100;
        var dawnCrossed = advancedClock.CrossedEvents.Contains(WorldAstronomicalEventKind.Dawn);
        var progressionResolved = progressionResult.AwardedExperience > 0 && progressionResult.Level >= 2;
        AssertSmoke(aborted.Accepted && aborted.CameraRestored && aborted.NarrativeRolledBack &&
                    abortScriptExecuted && worldDiagnostics.Count == 0 && clockAdvanced && dawnCrossed &&
                    progressionResolved,
            "UX0-H world transition runtime contract failed: " +
            $"abort={aborted.Accepted}/{aborted.CameraRestored}/{aborted.NarrativeRolledBack}; " +
            $"script={abortScriptExecuted}; world={string.Join(',', worldDiagnostics.Select(item => item.Code))}; " +
            $"clock={advancedClock.Snapshot.TotalMinutes}/100; dawn={dawnCrossed}; " +
            $"progression={progressionResult.AwardedExperience:0.###}/L{progressionResult.Level}.");
        GD.Print("OPENRPG_FORGE_RUNTIME_UX0H_WORLD_TRANSITIONS_PASS " +
                 "sequence_abort=restored-script-executed world_clock=calendar-zone-astronomy-save " +
                 "progression=curve-source-group-difficulty runtime=resolved");
    }

    private static void RunProjectWorldRuntimeSmokeTest(ProjectDefinition project)
    {
        var moduleId = project.Module.Id.Value;
        var areaId = project.Module.EntryAreaId.ToString();
        var baseline = project.Module.RuntimePolicies.World;
        var schema = new Forge.Domain.Lifecycle.CustomEventSchema(Guid.Parse("52000000-0000-4000-8000-000000000052"), 1,
            "runtime:r052", "smoke", "world_event", 1,
            new Dictionary<string, string> { ["count"] = "int", ["source"] = "guid" }, false,
            "1.0", "OnWorldEvent", EventAuthorityScope.Server);
        var clock = baseline.Clock with
        {
            Provenance = "runtime:r052",
            Speed = 2,
            InitialMinute = 350,
            AreaTimeZoneOffsets = new Dictionary<string, int> { [areaId] = 60 },
        };
        var profile = new ProjectWorldProfile(moduleId, 1, "runtime:r052", true, true, [schema], clock);
        var handle = ProjectWorldRuntimeResolver.Resolve(profile, moduleId, true);
        var optional = ProjectWorldRuntimeResolver.Resolve(null, moduleId, false);
        var required = ProjectWorldRuntimeResolver.Resolve(null, moduleId, true);
        var bus = new CustomWorldEventBus();
        bus.Register(profile);
        var published = bus.Publish(schema.Id, 1, new Dictionary<string, string>
        {
            ["count"] = "2",
            ["source"] = moduleId.ToString("D"),
        }, DateTimeOffset.UnixEpoch);
        var initial = CustomWorldEventBus.CreateInitialSnapshot(clock);
        var advanced = CustomWorldEventBus.AdvanceClock(clock, initial, 5, areaId);
        var restored = JsonSerializer.Deserialize<WorldClockSnapshot>(JsonSerializer.Serialize(advanced.Snapshot));
        var pages = ProjectDocumentationValidator.Pages(project.Module.Metadata.GetDefaultLocalization());

        AssertSmoke(handle.Succeeded && handle.Handle is not null && optional.Succeeded && optional.Handle is null &&
                    required.Diagnostic?.Code == ProjectWorldRuntimeResolver.RequiredMissingCode &&
                    published.QualifiedName == "smoke.world_event" && advanced.Snapshot.TotalMinutes == 360 &&
                    restored == advanced.Snapshot && pages.Count == 8,
            "Project custom-event, world-clock or player-documentation runtime contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_R052_PROJECT_WORLD_PASS custom=namespaced-typed-server " +
                 "clock=speed-calendar-zone-pause-astronomical-save forgescript=typed-handle " +
                 "docs=8-localized-pages optional=ignored required=diagnostic");
    }

    private void PublishProjectConnectionEvent(PlayerConnectionStage stage, string cause)
    {
        var profile = _moduleRuntimePolicies?.GameplayEvents;
        if (profile is null) return;
        var @event = new PlayerConnectionEvent(Guid.NewGuid(), "player:local", stage, cause,
            EventAuthorityScope.Server, ++_runtimePolicyRevision, DateTimeOffset.UtcNow);
        var published = _gameplayEvents.Publish(@event, profile, profile.RequiredAtRuntime);
        if (!published.Accepted)
        {
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_PROJECT_EVENT_FAILED code={published.DiagnosticCode}");
            return;
        }
        var binding = profile.Bindings.Single(item => item.EventName == @event.EventName);
        GD.Print($"OPENRPG_FORGE_RUNTIME_PROJECT_EVENT event={@event.EventName} handler={binding.Handler} " +
                 $"revision={@event.ServerRevision}");
    }

    private static T AssertSingle<T>(IReadOnlyList<T> values, string message)
    {
        AssertSmoke(values.Count == 1, message);
        return values[0];
    }

    private async Task RunEntryGroundSmokeTestAsync(ProjectDefinition project)
    {
        var entryArea = project.Areas.Single(area => area.Id == project.Module.EntryAreaId);
        AssertSmoke(entryArea.Slug == "ember-inn" && _activeArea?.Id == entryArea.Id,
            "The entry-ground smoke test must start inside the Ember Inn.");
        AssertSmoke(entryArea.Entities.Any(entity => entity.Tags.Contains("floor", StringComparer.Ordinal)),
            "The Ember Inn has no authored walkable floor.");
        var player = _player ?? throw new InvalidOperationException(
            "The entry-ground smoke test did not reconstruct the player.");
        var start = player.GlobalPosition;
        for (var frame = 0; frame < 120; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        AssertSmoke(player.IsOnFloor() && player.GlobalPosition.Y >= start.Y - .25f,
            $"The player fell through the Ember Inn floor " +
            $"(start_y={start.Y:0.###}, current_y={player.GlobalPosition.Y:0.###}, " +
            $"on_floor={player.IsOnFloor()}).");
        var walkableBounds = _interiorWalkableBounds ?? throw new InvalidOperationException(
            "The Ember Inn did not reconstruct its collision shell.");
        var testHeight = _interiorFloorTop + 1;
        var space = GetWorld3D().DirectSpaceState;
        AssertSmoke(
            HitsWall(new Vector3(walkableBounds.Position.X + 1, testHeight, 0),
                new Vector3(walkableBounds.Position.X - 1, testHeight, 0)) &&
            HitsWall(new Vector3(walkableBounds.End.X - 1, testHeight, 0),
                new Vector3(walkableBounds.End.X + 1, testHeight, 0)) &&
            HitsWall(new Vector3(0, testHeight, walkableBounds.Position.Y + 1),
                new Vector3(0, testHeight, walkableBounds.Position.Y - 1)) &&
            HitsWall(new Vector3(0, testHeight, walkableBounds.End.Y - 1),
                new Vector3(0, testHeight, walkableBounds.End.Y + 1)),
            "One or more Ember Inn boundary walls have no physical collision.");

        player.ControlLocked = true;
        await PushAgainstWallAsync(
            new Vector3(walkableBounds.Position.X + 2, start.Y, 0), Vector3.Left,
            () => player.GlobalPosition.X >= walkableBounds.Position.X + .4f, "west");
        await PushAgainstWallAsync(
            new Vector3(walkableBounds.End.X - 2, start.Y, 0), Vector3.Right,
            () => player.GlobalPosition.X <= walkableBounds.End.X - .4f, "east");
        await PushAgainstWallAsync(
            new Vector3(0, start.Y, walkableBounds.Position.Y + 2), Vector3.Forward,
            () => player.GlobalPosition.Z >= walkableBounds.Position.Y + .4f, "north");
        await PushAgainstWallAsync(
            new Vector3(0, start.Y, walkableBounds.End.Y - 2), Vector3.Back,
            () => player.GlobalPosition.Z <= walkableBounds.End.Y - .4f, "south-door");

        var recoveryCount = player.FallRecoveryCount;
        player.GlobalPosition = new Vector3(0, -10, 0);
        for (var frame = 0; frame < 3; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        AssertSmoke(player.FallRecoveryCount == recoveryCount + 1 &&
                    player.GlobalPosition.Y >= _interiorFloorTop,
            "The below-map recovery did not return the player to the last safe interior position.");
        player.ControlLocked = false;

        var visual = player.GetNodeOrNull<Node3D>("AuthoredVisual");
        var visualBounds = visual is not null && TryMeasureNodeBounds(visual, out var visualMinimum, out var visualMaximum)
            ? $"visual_min_y={visualMinimum.Y:0.###} visual_max_y={visualMaximum.Y:0.###}"
            : "visual_bounds=unavailable";
        AssertSmoke(visual is not null && TryMeasureNodeBounds(visual, out visualMinimum, out _) &&
                    Math.Abs((player.GlobalPosition.Y + visualMinimum.Y) - _interiorFloorTop) <= .08f,
            "The player visual feet are not aligned with the Ember Inn floor.");
        GD.Print($"OPENRPG_FORGE_ENTRY_GROUND_CONTACT_PASS area={entryArea.Slug} " +
                 $"start_y={start.Y:0.###} current_y={player.GlobalPosition.Y:0.###} " +
                 $"on_floor={player.IsOnFloor()} collisions={_collisionCount} walls=4 movement_blocked=4 " +
                 $"fall_recovery={player.FallRecoveryCount} {visualBounds}");
        GetTree().Quit(0);

        bool HitsWall(Vector3 from, Vector3 to)
        {
            var query = PhysicsRayQueryParameters3D.Create(from, to);
            query.Exclude = [player.GetRid()];
            return space.IntersectRay(query).Count > 0;
        }

        async Task PushAgainstWallAsync(Vector3 position, Vector3 direction, Func<bool> remainsInside,
            string wall)
        {
            player.GlobalPosition = position;
            player.Velocity = Vector3.Zero;
            for (var frame = 0; frame < 16; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                player.Velocity = direction * 30;
                player.MoveAndSlide();
            }
            AssertSmoke(remainsInside(), $"The player crossed the {wall} Ember Inn wall.");
        }
    }

    private async Task RunCryptMovementSmokeTestAsync(ProjectDefinition project)
    {
        var area = project.Areas.Single(item => item.Id == project.Module.EntryAreaId);
        AssertSmoke(_isCryptPrototype && area.Slug == "crypt-of-ashes" && _activeArea?.Id == area.Id,
            "The crypt movement smoke test must start inside the Crypt of Ashes.");
        var player = _player ?? throw new InvalidOperationException(
            "The crypt movement smoke test did not reconstruct the player.");
        var camera = _camera ?? throw new InvalidOperationException(
            "The crypt movement smoke test did not reconstruct the camera.");
        var start = player.GlobalPosition;
        for (var frame = 0; frame < 120; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        AssertSmoke(player.IsOnFloor() && player.GlobalPosition.Y >= start.Y - .5f,
            $"The player did not establish stable ground contact in the crypt " +
            $"(start_y={start.Y:0.###}, current_y={player.GlobalPosition.Y:0.###}, " +
            $"on_floor={player.IsOnFloor()}).");
        AssertSmoke(_modelLibrary?.Count == 4 && _animatedCharacterInstances == 4,
            $"The four governed animated crypt characters were not reconstructed " +
            $"(manifests={_modelLibrary?.Count ?? 0}, animated={_animatedCharacterInstances}).");
        AssertSmoke(_interiorWalkableBounds is { } cameraBounds &&
                    !cameraBounds.HasPoint(new Vector2(camera.GlobalPosition.X, camera.GlobalPosition.Z)),
            $"The crypt camera is still clamped to the authored area bounds " +
            $"(camera={camera.GlobalPosition.X:0.###},{camera.GlobalPosition.Y:0.###}," +
            $"{camera.GlobalPosition.Z:0.###}).");
        GD.Print($"OPENRPG_FORGE_CRYPT_FREE_CAMERA_PASS " +
                 $"camera={camera.GlobalPosition.X:0.###},{camera.GlobalPosition.Y:0.###}," +
                 $"{camera.GlobalPosition.Z:0.###} outside_bounds=true " +
                 $"distance={camera.GlobalPosition.DistanceTo(player.GlobalPosition):0.###}");

        var visual = player.GetNodeOrNull<Node3D>("AuthoredVisual");
        var visualMinimum = Vector3.Zero;
        var visualMaximum = Vector3.Zero;
        AssertSmoke(visual is not null &&
                    TryMeasureNodeBounds(visual, out visualMinimum, out visualMaximum),
            "The crypt player has no measurable governed 3D visual.");
        var visualCenter = (visualMinimum + visualMaximum) * .5f;
        AssertSmoke(Math.Abs(visualCenter.X) <= .05f && Math.Abs(visualCenter.Z) <= .05f &&
                    Math.Abs(visualMinimum.Y + .9f) <= .4f,
            $"The crypt player visual is offset from its physical capsule " +
            $"(center_x={visualCenter.X:0.###}, center_z={visualCenter.Z:0.###}, " +
            $"feet_y={visualMinimum.Y:0.###}).");

        var movementStart = player.GlobalPosition;
        var jumpCount = player.JumpCount;
        Input.ActionPress("move_forward");
        try
        {
            for (var frame = 0; frame < 30; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        finally
        {
            Input.ActionRelease("move_forward");
        }
        var distance = new Vector2(player.GlobalPosition.X - movementStart.X,
            player.GlobalPosition.Z - movementStart.Z).Length();
        AssertSmoke(distance >= 4 && player.JumpCount == jumpCount && player.IsOnFloor(),
            $"Grounded crypt movement remained blocked " +
            $"(distance_30_frames={distance:0.###}, jumps={player.JumpCount - jumpCount}, " +
            $"on_floor={player.IsOnFloor()}).");

        var collisionRetracted = RuntimeCameraRig.SmoothCollisionDistance(14, 3, 1d / 60d);
        var collisionReleased = RuntimeCameraRig.SmoothCollisionDistance(3, 14, 1d / 60d);
        AssertSmoke(collisionRetracted < 14 && collisionReleased > 3 &&
                    collisionReleased - 3 <= .65f &&
                    14 - collisionRetracted > collisionReleased - 3,
            $"The camera collision response is not retracting quickly and releasing smoothly " +
            $"(retracted={collisionRetracted:0.###}, released={collisionReleased:0.###}).");
        GD.Print($"OPENRPG_FORGE_CRYPT_CAMERA_RESPONSE_PASS " +
                 $"yaw_sensitivity={RuntimeCameraRig.MouseYawSensitivity:0.####} " +
                 $"pitch_sensitivity={RuntimeCameraRig.MousePitchSensitivity:0.####} " +
                 $"retracted={collisionRetracted:0.###} released={collisionReleased:0.###}");

        var zoomIn = new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true };
        for (var step = 0; step < 16; step++) _cameraRig.HandleInput(zoomIn);
        _cameraRig.Snap(camera, player.GlobalPosition, null);
        SyncLocalPlayerCameraVisibility();
        var playerLabel = player.GetNodeOrNull<Label3D>(RuntimeWorldLabelNodeName);
        AssertSmoke(_cameraRig.FirstPersonActive &&
                    camera.GlobalPosition.DistanceTo(player.GlobalPosition) is >= 1f and <= 1.6f &&
                    visual!.Visible == false && playerLabel?.Visible == false,
            $"Maximum wheel zoom did not enter a clean first-person view " +
            $"(active={_cameraRig.FirstPersonActive}, " +
            $"eye_distance={camera.GlobalPosition.DistanceTo(player.GlobalPosition):0.###}, " +
            $"visual_visible={visual!.Visible}).");
        GD.Print($"OPENRPG_FORGE_CRYPT_FIRST_PERSON_ZOOM_PASS " +
                 $"fov={camera.Fov:0.###} eye_distance={camera.GlobalPosition.DistanceTo(player.GlobalPosition):0.###} " +
                 $"local_visual_hidden=true");
        _cameraRig.HandleInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.WheelDown,
            Pressed = true,
        });
        SyncLocalPlayerCameraVisibility();
        AssertSmoke(!_cameraRig.FirstPersonActive && visual!.Visible && playerLabel?.Visible == true,
            "Wheel-down did not restore the orbit camera and local player visual.");
        _cameraRig.Snap(camera, player.GlobalPosition, null);

        player.ControlLocked = true;
        player.GlobalPosition = new Vector3(4, _interiorFloorTop + .9f, 15);
        player.Velocity = Vector3.Zero;
        for (var frame = 0; frame < 45; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            player.Velocity = Vector3.Right * 12;
            player.MoveAndSlide();
        }
        AssertSmoke(player.GlobalPosition.X >= 11.5f,
            $"The east reliquary doorway remains physically blocked " +
            $"(player_x={player.GlobalPosition.X:0.###}).");
        var reliquaryExitX = player.GlobalPosition.X;

        var questStart = GetAvailableQuestTransitions().FirstOrDefault();
        AssertSmoke(questStart is not null, "The crypt quest has no available starting transition.");
        AdvanceQuest(questStart!);
        InteractWith(_keyItem ?? throw new InvalidOperationException("The Ember Key is missing."));
        AssertSmoke(_hasKey && _inventory.Contains(_questItemSlug),
            "The reachable Ember Key did not enter the player inventory.");
        InteractWith(_door ?? throw new InvalidOperationException("The sealed gate is missing."));
        AssertSmoke(_doorUnlocked, "The Ember Key did not unlock the sealed gate.");
        for (var frame = 0; frame < 3; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        player.GlobalPosition = new Vector3(0, _interiorFloorTop + .9f, 8);
        player.Velocity = Vector3.Zero;
        for (var frame = 0; frame < 35; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            player.Velocity = Vector3.Forward * 12;
            player.MoveAndSlide();
        }
        player.ControlLocked = false;
        AssertSmoke(player.GlobalPosition.Z <= 4.5f,
            $"The unlocked gate still blocks access to the second room " +
            $"(player_z={player.GlobalPosition.Z:0.###}).");
        GD.Print($"OPENRPG_FORGE_CRYPT_CRITICAL_PATH_PASS reliquary_doorway=4m " +
                 $"reliquary_x={reliquaryExitX:0.###} key=acquired door=unlocked " +
                 $"second_room=reachable player_z={player.GlobalPosition.Z:0.###}");
        GD.Print($"OPENRPG_FORGE_CRYPT_GROUNDED_MOVEMENT_PASS distance_30_frames={distance:0.###} " +
                 $"jumps=0 on_floor={player.IsOnFloor()} manifests={_modelLibrary!.Count} " +
                 $"animated={_animatedCharacterInstances} visual_center={visualCenter.X:0.###}," +
                 $"{visualCenter.Z:0.###}");
        GetTree().Quit(0);
    }

    private static bool TryMeasureNodeBounds(Node root, out Vector3 minimum, out Vector3 maximum)
    {
        var hasBounds = false;
        var measuredMinimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var measuredMaximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        Accumulate(root, Transform3D.Identity);
        minimum = measuredMinimum;
        maximum = measuredMaximum;
        return hasBounds;

        void Accumulate(Node node, Transform3D parentTransform)
        {
            var transform = node is Node3D node3D ? parentTransform * node3D.Transform : parentTransform;
            if (node is MeshInstance3D { Mesh: not null } mesh)
            {
                var bounds = mesh.GetAabb();
                for (var x = 0; x <= 1; x++)
                    for (var y = 0; y <= 1; y++)
                        for (var z = 0; z <= 1; z++)
                        {
                            var point = transform * (bounds.Position + new Vector3(
                                bounds.Size.X * x, bounds.Size.Y * y, bounds.Size.Z * z));
                            measuredMinimum = new Vector3(MathF.Min(measuredMinimum.X, point.X),
                                MathF.Min(measuredMinimum.Y, point.Y), MathF.Min(measuredMinimum.Z, point.Z));
                            measuredMaximum = new Vector3(MathF.Max(measuredMaximum.X, point.X),
                                MathF.Max(measuredMaximum.Y, point.Y), MathF.Max(measuredMaximum.Z, point.Z));
                            hasBounds = true;
                        }
            }
            foreach (var child in node.GetChildren()) Accumulate(child, transform);
        }
    }

    private async Task RunCombatFeedbackSmokeTestAsync()
    {
        var player = _player ?? throw new InvalidOperationException("Combat feedback smoke requires a player.");
        var camera = _camera ?? throw new InvalidOperationException("Combat feedback smoke requires a camera.");
        var source = _playerCombatant ?? throw new InvalidOperationException(
            "Combat feedback smoke requires an authoritative player combatant.");
        var targetNode = _boss ?? _hostiles.Keys.FirstOrDefault(GodotObject.IsInstanceValid) ??
            throw new InvalidOperationException("Combat feedback smoke requires a hostile target.");
        var target = _hostiles[targetNode];
        var hud = _hud ?? throw new InvalidOperationException("Combat feedback smoke requires the runtime HUD.");
        var actionBar = _actionBar ?? throw new InvalidOperationException(
            "Combat feedback smoke requires the action bar.");
        var feedback = _combatFeedbackLayer ?? throw new InvalidOperationException(
            "Combat feedback smoke requires the feedback layer.");
        var impacts = _combatImpactPool ?? throw new InvalidOperationException(
            "Combat feedback smoke requires the impact pool.");
        var map = _moduleMap ?? throw new InvalidOperationException("Combat feedback smoke requires the module map.");
        var toasts = _toastQueue ?? throw new InvalidOperationException("Combat feedback smoke requires toasts.");
        var gameplayAbility = source.Profile.Abilities.First();

        player.GlobalPosition = targetNode.GlobalPosition + new Vector3(2, 0, 0);
        player.Velocity = Vector3.Zero;
        _cameraRig.Snap(camera, player.GlobalPosition, ConstrainCameraPosition);

        for (var frame = 0; frame < 4; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var targetHealthBefore = target.Health;
        var animationPlayer = _characterAnimations.TryGetValue(targetNode, out var animation)
            ? animation.Player
            : null;
        var animationSpeedBefore = animationPlayer?.SpeedScale;
        DamageHostile(targetNode);
        var gameplayPresentation = _lastCombatFeedback ?? throw new InvalidOperationException(
            "The real runtime cast produced no combat feedback projection.");
        var gameplayDamage = gameplayPresentation.Amounts.Single(item =>
            item.Kind == CombatFeedbackAmountKind.Damage).Value;
        AssertSmoke(targetHealthBefore - target.Health == gameplayDamage &&
                    gameplayPresentation.TargetHealth == target.Health &&
                    gameplayPresentation.CooldownDurationSeconds == gameplayAbility.CooldownSeconds &&
                    gameplayPresentation.CooldownRemainingSeconds == 0 &&
                    !actionBar.PrimaryCooldownVisible &&
                    feedback.LastTransactionId == gameplayPresentation.TransactionId &&
                    feedback.LastPresentedDamage == gameplayDamage &&
                    hud.TargetFrameVisible &&
                    !impacts.LastUsedCueOverride,
            "Real runtime damage did not preserve authoritative combat feedback values.");

        for (var frame = 0; frame < 8; frame++)
            _cameraRig.Update(camera, player.GlobalPosition, 1d / 60d, ConstrainCameraPosition);
        AssertSmoke(_combatUiPolicy.ReducedMotion
                ? !_cameraRig.ImpulseActive && _cameraRig.MaximumAppliedImpulse == 0
                : _cameraRig.MaximumAppliedImpulse <= _combatUiPolicy.CameraImpulseMeters + .001f,
            "Camera impact impulse escaped the configured motion policy.");
        GD.Print($"OPENRPG_FORGE_COMBAT_FEEDBACK_VISUAL_PASS actualGameplay=true " +
                 $"damage={gameplayDamage} target_health={target.Health} cooldown=0 " +
                 $"cue={gameplayPresentation.VisualCueKey} target_frame=true");

        var fixtureAbility = new AbilityDefinition(
            AbilityId.From(Guid.Parse("cf100000-0000-4000-8000-000000000001")),
            "fixture-renewal",
            "Renouveau de validation",
            2,
            .8,
            AbilityTargeting.Self,
            [
                new AbilityEffect(AbilityEffectKind.Heal, 5),
                new AbilityEffect(AbilityEffectKind.RestoreResource, 3),
            ],
            new AbilityVisualCue("cue.fixture-renewal"));
        var fixture = new CombatantState("fixture:combat-feedback",
            new CombatProfile(new CombatStats(30, 10, 4, 1, "fixture", "fixture"), [fixtureAbility]));
        fixture.Restore(12, 2);
        var fixtureResult = CombatResolver.Cast(fixture, fixture, fixtureAbility.Id, 100);
        var fixturePresentation = CombatFeedbackPresentationService.Create(
            Guid.Parse("cf100000-0000-4000-8000-000000000002"),
            fixture, fixture, fixtureResult, 100.1);
        actionBar.ConfigurePrimaryAbility(fixtureAbility);
        actionBar.UpdatePrimaryAbilityState(
            fixturePresentation.CooldownRemainingSeconds, fixture.Resource, targetAvailable: true);
        hud.UpdatePlayerStatus("FIXTURE — PAS DE GAMEPLAY", 30, 30, 10, 10, 1);
        hud.UpdatePlayerStatus("FIXTURE — PAS DE GAMEPLAY", 18, 30, 7, 10, 1);
        feedback.Present(fixturePresentation, targetNode);
        feedback.Flash(targetNode, CombatFeedbackAmountKind.Healing);
        impacts.Present(fixturePresentation, targetNode, targetNode.GlobalPosition);
        impacts.PlayAudio(CombatFeedbackAudioKind.Healing, targetNode.GlobalPosition);
        AssertSmoke(fixturePresentation.Amounts.Count == 2 &&
                    fixturePresentation.Amounts[0].Kind == CombatFeedbackAmountKind.Healing &&
                    fixturePresentation.Amounts[1].Kind == CombatFeedbackAmountKind.ResourceRestored &&
                    Math.Abs(fixturePresentation.CooldownRemainingSeconds - .7) < .0001 &&
                    actionBar.PrimaryCooldownVisible &&
                    (_combatUiPolicy.ReducedMotion ? !hud.PlayerHealthAnimating : hud.PlayerHealthAnimating),
            "Explicit non-gameplay heal/resource/cooldown/player-chip fixture failed.");

        var areaExperienceBeforeAudioFixture = _areaExperience ?? throw new InvalidOperationException(
            "Combat feedback smoke requires an area experience profile.");
        var authoredCue = areaExperienceBeforeAudioFixture.AudioCues.FirstOrDefault(item =>
            LoadAudioStream(item.Resource) is not null);
        var usesSyntheticAuthoredCue = authoredCue is null;
        if (usesSyntheticAuthoredCue)
        {
            var resource = ProceduralAmbientAudioService.StreamResource;
            authoredCue = new AudioCueDefinition(
                Guid.Parse("cf100000-0000-4000-8000-000000000007"), "cue.fixture-authored", resource,
                "SFX", .65f, false, "CC0-1.0", ProceduralAmbientAudioService.Create(resource).ContentSha256,
                "1.0.0", "runtime:combat-feedback-smoke");
            _areaExperience = new AreaExperienceProfile(areaExperienceBeforeAudioFixture.Id,
                areaExperienceBeforeAudioFixture.SchemaVersion,
                [.. areaExperienceBeforeAudioFixture.AudioCues, authoredCue],
                areaExperienceBeforeAudioFixture.ScatterEmitters, areaExperienceBeforeAudioFixture.Voices,
                areaExperienceBeforeAudioFixture.PatrolRoutes, areaExperienceBeforeAudioFixture.Loading,
                areaExperienceBeforeAudioFixture.Provenance);
        }
        try
        {
            PlayCombatFeedbackAudio(fixturePresentation with
            {
                TransactionId = Guid.Parse("cf100000-0000-4000-8000-000000000004"),
                VisualCueKey = authoredCue!.CueId,
            }, targetNode);
            AssertSmoke(impacts.LastUsedCueOverride,
                "A matching authored area cue did not override the built-in combat sound.");
            PlayCombatFeedbackAudio(fixturePresentation with
            {
                TransactionId = Guid.Parse("cf100000-0000-4000-8000-000000000005"),
                VisualCueKey = "cue.fixture-missing",
            }, targetNode);
            AssertSmoke(!impacts.LastUsedCueOverride,
                "A missing area cue did not fall back to the built-in combat sound.");
        }
        finally
        {
            _areaExperience = areaExperienceBeforeAudioFixture;
        }
        GD.Print($"OPENRPG_FORGE_COMBAT_FEEDBACK_AUDIO_ROUTE_PASS authored={authoredCue.CueId} " +
                 $"fixture={usesSyntheticAuthoredCue.ToString().ToLowerInvariant()} fallback=cue.fixture-missing");

        var sfxBus = AudioServer.GetBusIndex("SFX");
        AssertSmoke(sfxBus >= 0, "The runtime SFX audio bus is unavailable.");
        var sfxMutedBefore = AudioServer.IsBusMute(sfxBus);
        var sfxVolumeBefore = AudioServer.GetBusVolumeDb(sfxBus);
        var authoritativeHealthBeforeMute = target.Health;
        var authoritativeResourceBeforeMute = source.Resource;
        var mutedTransaction = Guid.Parse("cf100000-0000-4000-8000-000000000006");
        try
        {
            AudioServer.SetBusVolumeDb(sfxBus, -80);
            AudioServer.SetBusMute(sfxBus, true);
            feedback.Present(fixturePresentation with { TransactionId = mutedTransaction }, targetNode);
            feedback.Flash(targetNode, CombatFeedbackAmountKind.Healing);
            impacts.Present(fixturePresentation, targetNode, targetNode.GlobalPosition);
            impacts.PlayAudio(CombatFeedbackAudioKind.Healing, targetNode.GlobalPosition);
            AssertSmoke(AudioServer.IsBusMute(sfxBus) && feedback.LastTransactionId == mutedTransaction &&
                        feedback.FlashCount > 0 && target.Health == authoritativeHealthBeforeMute &&
                        source.Resource == authoritativeResourceBeforeMute,
                "Muting SFX suppressed visual feedback or mutated authoritative combat state.");
        }
        finally
        {
            AudioServer.SetBusVolumeDb(sfxBus, sfxVolumeBefore);
            AudioServer.SetBusMute(sfxBus, sfxMutedBefore);
        }
        AssertSmoke(AudioServer.IsBusMute(sfxBus) == sfxMutedBefore &&
                    Math.Abs(AudioServer.GetBusVolumeDb(sfxBus) - sfxVolumeBefore) < .001f,
            "The SFX mute fixture did not restore the exact prior bus state.");
        GD.Print("OPENRPG_FORGE_COMBAT_FEEDBACK_SFX_MUTE_PASS linear_volume=0 visual=true restored=true");
        AssertSmoke(_combatUiPolicy.TextScale is >= .8f and <= 2f,
            "Combat feedback text scale escaped its supported range.");
        GD.Print($"OPENRPG_FORGE_COMBAT_FEEDBACK_ACCESSIBILITY_PASS " +
                 $"text_scale={_combatUiPolicy.TextScale:0.0} high_contrast={_combatUiPolicy.HighContrast} " +
                 $"color_vision={_combatUiPolicy.ColorVisionMode}");

        var stressAbilityId = AbilityId.From(Guid.Parse("cf100000-0000-4000-8000-000000000003"));
        var fixtureNodes = new Node3D[RuntimeCombatFeedbackLayer.FloatingNumberCapacity];
        for (var index = 0; index < fixtureNodes.Length; index++)
        {
            var node = new Node3D
            {
                Name = $"CombatFeedbackStressFixture{index + 1}",
            };
            AddChild(node);
            node.GlobalPosition = targetNode.GlobalPosition +
                                  new Vector3((index % 8) * .03f, 0, (index / 8) * .03f);
            fixtureNodes[index] = node;
            feedback.Present(new CombatFeedbackPresentation(
                Guid.NewGuid(),
                "fixture:source",
                $"fixture:target:{index}",
                stressAbilityId,
                "fixture-impact",
                "Impact de charge",
                "cue.fixture-impact",
                [new CombatFeedbackAmount(CombatFeedbackAmountKind.Damage, index + 1)],
                99,
                100,
                10,
                10,
                0,
                0,
                false), node);
        }
        for (var index = 0; index < RuntimeCombatImpactPool.ParticleCapacity + 4; index++)
            impacts.Present(gameplayPresentation, targetNode, targetNode.GlobalPosition);
        for (var index = 0; index < RuntimeCombatImpactPool.AudioCapacity + 4; index++)
            impacts.PlayAudio(CombatFeedbackAudioKind.ImpactLight, targetNode.GlobalPosition);
        for (var index = 0; index < 12; index++)
            map.ShowPing(new Vector3Value(index * 2, 0, index * -2),
                index % 2 == 0 ? RuntimeMapPingKind.Combat : RuntimeMapPingKind.Objective,
                $"Fixture de ping {index + 1}");
        for (var index = 0; index < 5; index++)
            toasts.Show(RuntimeToastKind.System, $"Fixture de notification {index + 1}", $"fixture-toast:{index}");

        AssertSmoke(feedback.ActiveFloatingCount == RuntimeCombatFeedbackLayer.FloatingNumberCapacity &&
                    feedback.PeakActiveFloatingCount == RuntimeCombatFeedbackLayer.FloatingNumberCapacity &&
                    feedback.RegisteredCount <= RuntimeCombatFeedbackLayer.NameplateCapacity &&
                    impacts.PeakParticleCount <= RuntimeCombatImpactPool.ParticleCapacity &&
                    impacts.PeakAudioCount <= RuntimeCombatImpactPool.AudioCapacity &&
                    map.ActivePingCount == 8 && map.PeakPingCount == 8 &&
                    toasts.ActiveCount == 3 && toasts.PeakActiveCount == 3,
            "A bounded combat feedback pool exceeded its capacity or failed to fill deterministically.");
        AssertSmoke(_combatUiPolicy.ReducedMotion
                ? impacts.LastParticleAmount == 0 && impacts.PeakParticleCount == 0 && !impacts.HitStopActive
                : impacts.PeakParticleCount == RuntimeCombatImpactPool.ParticleCapacity,
            "Reduced-motion or normal particle policy failed under stress.");

        for (var frame = 0; frame < 60; frame++)
            ProcessCombatFeedbackStressFrame(feedback, impacts, map, toasts, actionBar);
        var frameTicks = new long[600];
        var allocationsBefore = GC.GetAllocatedBytesForCurrentThread();
        var stressStarted = Stopwatch.GetTimestamp();
        for (var frame = 0; frame < 600; frame++)
        {
            var frameStarted = Stopwatch.GetTimestamp();
            ProcessCombatFeedbackStressFrame(feedback, impacts, map, toasts, actionBar);
            frameTicks[frame] = Stopwatch.GetTimestamp() - frameStarted;
        }
        var stressElapsed = Stopwatch.GetElapsedTime(stressStarted);
        var managedAllocations = GC.GetAllocatedBytesForCurrentThread() - allocationsBefore;
        Array.Sort(frameTicks);
        var p95Ticks = frameTicks[(int)Math.Ceiling(frameTicks.Length * .95) - 1];
        var presentationP95Milliseconds = p95Ticks * 1000d / Stopwatch.Frequency;
        AssertSmoke(managedAllocations <= 64 * 1024 && presentationP95Milliseconds <= 1 &&
                    stressElapsed < TimeSpan.FromSeconds(10),
            $"Combat feedback stable-frame stress exceeded its guardrail " +
            $"({managedAllocations} bytes, p95 {presentationP95Milliseconds:0.###} ms, " +
            $"total {stressElapsed.TotalMilliseconds:0.###} ms).");
        if (animationPlayer is not null && animationSpeedBefore is not null)
            AssertSmoke(Math.Abs(animationPlayer.SpeedScale - animationSpeedBefore.Value) < .0001,
                "Animation hit-stop did not restore the exact prior speed scale.");

        actionBar.ConfigurePrimaryAbility(gameplayAbility);
        actionBar.UpdatePrimaryAbilityState(0, source.Resource, HasAttackTargetInRange());
        GD.Print("OPENRPG_FORGE_COMBAT_FEEDBACK_FIXTURES_PASS actualGameplay=false " +
                 $"healing={fixtureResult.Healing} resource={fixtureResult.ResourceRestored} cooldown=0.7 " +
                 $"player_chip=true pings={map.PeakPingCount} toasts={toasts.PeakActiveCount}");
        if (_combatUiPolicy.ReducedMotion)
            GD.Print("OPENRPG_FORGE_COMBAT_FEEDBACK_REDUCED_MOTION_PASS " +
                     "particles=0 camera_impulse=0 hit_stop=0 bars=instant numbers=stationary cooldown=numeric");
        GD.Print($"OPENRPG_FORGE_COMBAT_FEEDBACK_PERFORMANCE_PASS frames=600 " +
                 $"floating_peak={feedback.PeakActiveFloatingCount} nameplates={feedback.RegisteredCount} " +
                 $"particles_peak={impacts.PeakParticleCount} audio_peak={impacts.PeakAudioCount} " +
                 $"pings_peak={map.PeakPingCount} toasts_peak={toasts.PeakActiveCount} " +
                 $"managed_allocated_bytes={managedAllocations} " +
                 $"presentation_p95_ms={presentationP95Milliseconds:0.###} " +
                 $"presentation_total_ms={stressElapsed.TotalMilliseconds:0.###}");
        foreach (var fixtureNode in fixtureNodes) fixtureNode.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GetTree().Quit(0);
    }

    private static void ProcessCombatFeedbackStressFrame(
        RuntimeCombatFeedbackLayer feedback,
        RuntimeCombatImpactPool impacts,
        RuntimeModuleMap map,
        RuntimeToastQueue toasts,
        RuntimeActionBar actionBar)
    {
        const double delta = 1d / 60d;
        feedback._Process(delta);
        impacts._Process(delta);
        map._Process(delta);
        toasts._Process(delta);
        actionBar._Process(delta);
    }


    private static void AssertSmoke(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private StandardMaterial3D CreateMaterial(Color color, bool emission = false)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.72f,
            Metallic = 0.08f,
        };
        if (_renderPlan is not null)
        {
            GodotEnvironmentAdapter.ApplyPbrDefaults(material, _renderPlan);
        }

        if (emission)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = 2.4f;
        }

        return material;
    }

    private static void CreateLabel(Node3D parent, string text, Color color, float height)
    {
        parent.AddChild(new Label3D
        {
            Name = RuntimeWorldLabelNodeName,
            Text = text,
            Position = new Vector3(0, height, 0),
            Modulate = color,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 32,
            OutlineSize = 8,
        });
    }

    private static string SafeNodeName(string value) =>
        new(value.Where(character => char.IsLetterOrDigit(character) || character == '_').ToArray());

    private static string ArchiveRoot(string savePath) => Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(savePath))!, ".openrpg-save-archive");

    private static string ArchiveSlot(string savePath)
    {
        var name = Path.GetFileNameWithoutExtension(savePath).ToLowerInvariant();
        var safe = new string(name.Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "quick" : safe[..Math.Min(safe.Length, 64)];
    }

    private static int CountTreeNodes(Node root)
    {
        var count = 1;
        foreach (Node child in root.GetChildren())
        {
            count += CountTreeNodes(child);
        }

        return count;
    }

    private static void RegisterInputActions()
    {
        RegisterKeyAction("move_forward", Key.W, Key.Up);
        RegisterKeyAction("move_back", Key.S, Key.Down);
        RegisterKeyAction("move_left", Key.A, Key.Q, Key.Left);
        RegisterKeyAction("move_right", Key.D, Key.Right);
        RegisterKeyAction("interact", Key.E, Key.Key2);
        RegisterKeyAction("attack", Key.Key1);
        RegisterKeyAction("jump", Key.Space);
        RegisterKeyAction("quick_save", Key.F5, Key.Key5);
        RegisterKeyAction("quick_load", Key.F9, Key.Key6);
        RegisterKeyAction("rest", Key.R, Key.Key4);
        RegisterKeyAction("skip_sequence", Key.Escape);
        RegisterKeyAction("action_wheel", Key.Tab, Key.Key3);
        RegisterJoypadAction("action_wheel", JoyButton.Y);
        RegisterKeyAction("tactical_pause", Key.P);
        RegisterJoypadAction("tactical_pause", JoyButton.Start);
        RegisterKeyAction("slow_time", Key.T);
        RegisterKeyAction("inventory", Key.I);
        RegisterKeyAction("character", Key.C);
        RegisterKeyAction("journal", Key.J);
        RegisterKeyAction("module_map", Key.M);
        RegisterKeyAction("combat_mode", Key.V);
        RegisterKeyAction("dive", Key.Ctrl);
        RegisterKeyAction("climb", Key.Shift);
        RegisterKeyAction("camera_left", Key.Home);
        RegisterKeyAction("camera_right", Key.End);
        RegisterModifiedKeyAction("toggle_fps", Key.R, ctrl: true);
    }

    private static void ApplyRuntimeInputProfile(RuntimeInputProfile profile)
    {
        foreach (var action in profile.Actions)
        {
            if (!InputMap.HasAction(action.Key)) InputMap.AddAction(action.Key);
            InputMap.ActionEraseEvents(action.Key);
            foreach (var binding in profile.Bindings.Where(item =>
                         string.Equals(item.ActionKey, action.Key, StringComparison.Ordinal)))
            {
                if (binding.Device == RuntimeInputDevice.KeyboardMouse &&
                    Enum.TryParse<Key>(binding.Binding, true, out var key))
                    InputMap.ActionAddEvent(action.Key, new InputEventKey { PhysicalKeycode = key });
                else if (binding.Device == RuntimeInputDevice.Gamepad &&
                         Enum.TryParse<JoyButton>(binding.Binding, true, out var button))
                    InputMap.ActionAddEvent(action.Key, new InputEventJoypadButton { ButtonIndex = button });
            }
        }
    }

    private static void RegisterModifiedKeyAction(StringName action, Key key, bool ctrl = false,
        bool alt = false, bool shift = false)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        var inputEvent = new InputEventKey
        {
            PhysicalKeycode = key,
            CtrlPressed = ctrl,
            AltPressed = alt,
            ShiftPressed = shift,
        };
        if (!InputMap.ActionHasEvent(action, inputEvent)) InputMap.ActionAddEvent(action, inputEvent);
    }

    private static bool HasKeyBinding(StringName action, Key key, bool ctrl = false) =>
        InputMap.HasAction(action) && InputMap.ActionGetEvents(action)
            .OfType<InputEventKey>()
            .Any(input => input.PhysicalKeycode == key && input.CtrlPressed == ctrl);

    private static void RegisterKeyAction(StringName action, params Key[] keys)
    {
        if (!InputMap.HasAction(action))
        {
            InputMap.AddAction(action);
        }

        foreach (var key in keys)
        {
            var inputEvent = new InputEventKey { PhysicalKeycode = key };
            if (!InputMap.ActionHasEvent(action, inputEvent))
            {
                InputMap.ActionAddEvent(action, inputEvent);
            }
        }
    }

    private static void RegisterJoypadAction(StringName action, params JoyButton[] buttons)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach (var button in buttons)
        {
            var inputEvent = new InputEventJoypadButton { ButtonIndex = button };
            if (!InputMap.ActionHasEvent(action, inputEvent)) InputMap.ActionAddEvent(action, inputEvent);
        }
    }

    private sealed class RuntimeAreaChunkLoader : IAreaChunkLoader
    {
        private readonly string _root;
        private readonly string _rootPrefix;

        internal RuntimeAreaChunkLoader(string root)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(root);
            _root = Path.GetFullPath(root);
            _rootPrefix = _root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }

        public async Task<AreaChunkPayload> LoadAsync(string resourcePath, CancellationToken cancellationToken)
        {
            var normalized = WeatherStateProfile.NormalizeOptionalResource(resourcePath);
            if (normalized.Length == 0)
            {
                throw new InvalidDataException("Area streaming resource path cannot be empty.");
            }

            var fullPath = Path.GetFullPath(Path.Combine(_root, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Required area resource '{normalized}' is unavailable inside the module.", fullPath);
            }

            var content = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
            var dependencyFile = fullPath + ".deps";
            IReadOnlyList<string> dependencies = File.Exists(dependencyFile)
                ? (await File.ReadAllLinesAsync(dependencyFile, cancellationToken).ConfigureAwait(false))
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0 && !line.StartsWith('#'))
                    .Select(WeatherStateProfile.NormalizeOptionalResource)
                    .ToArray()
                : [];
            return new AreaChunkPayload(normalized, content, dependencies);
        }
    }

    private sealed class RuntimeConditionEvaluator : IDialogueConditionEvaluator, IQuestConditionEvaluator
    {
        private readonly GameRoot _runtime;

        internal RuntimeConditionEvaluator(GameRoot runtime) => _runtime = runtime;

        public bool IsSatisfied(DialogueCondition condition) => condition.Kind switch
        {
            DialogueConditionKind.VariableEquals =>
                _runtime._blackboard.TryGetValue(condition.Key, out var value) && value == condition.ExpectedValue,
            DialogueConditionKind.HasItem => _runtime._inventory.Contains(condition.Key),
            DialogueConditionKind.QuestStateEquals =>
                string.Equals(
                    _runtime._questMachine?.CurrentState.Id.ToString(),
                    condition.ExpectedValue,
                    StringComparison.Ordinal) ||
                string.Equals(
                    _runtime._questMachine?.CurrentState.DisplayName,
                    condition.ExpectedValue,
                    StringComparison.OrdinalIgnoreCase),
            _ => false,
        };

        public bool IsSatisfied(QuestCondition condition) => condition.Kind switch
        {
            QuestConditionKind.VariableEquals =>
                _runtime._playtestQuestVariables.Any(item =>
                    item.Key.EndsWith($":{condition.Key}", StringComparison.Ordinal) &&
                    item.Value == condition.ExpectedValue) ||
                _runtime._blackboard.TryGetValue(condition.Key, out var value) && value == condition.ExpectedValue,
            QuestConditionKind.HasItem => _runtime._inventory.Contains(condition.Key),
            _ => false,
        };
    }

    private sealed class R068DialogueConditionEvaluator : IDialogueConditionEvaluator
    {
        public bool IsSatisfied(DialogueCondition condition) => true;
    }

    private enum PrototypeQuestState
    {
        NotStarted,
        Active,
        Completed,
    }
}
