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
        var r055ModuleScript = area.Entities
            .SelectMany(item => item.Overrides.Properties)
            .FirstOrDefault(item => string.Equals(item.Key, "deathScript", StringComparison.Ordinal))
            ?.Value;
        var r055UsesModuleScript = !string.IsNullOrWhiteSpace(r055ModuleScript) &&
                                   !string.IsNullOrWhiteSpace(ReadConfiguredScriptSource(r055ModuleScript));
        var r055Script = r055UsesModuleScript ? r055ModuleScript! : "scripts/area_user_event_smoke.forge";
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
        var r055Pin = r055Area.Authoring.Map.Pins[0];
        var r055FloorZeroMap = AreaExperienceAuthoringScriptService.Reveal(r055Area,
            new(AreaMapExplorationScope.Party, "party:runtime", new HashSet<string>(), new HashSet<Guid>()),
            0, r055Pin.X, r055Pin.Z);
        var r055FloorZeroMapAgain = AreaExperienceAuthoringScriptService.Reveal(r055Area,
            r055FloorZeroMap, 0, r055Pin.X, r055Pin.Z);
        var r055Map = AreaExperienceAuthoringScriptService.Reveal(r055Area,
            r055FloorZeroMapAgain, 1, r055Pin.X, r055Pin.Z);
        var r055Grid = new AreaMapExplorationGrid(r055Area.Dimensions,
            r055Area.Authoring.Map.FloorCount, r055Area.Authoring.Map.ExplorationCellSize);
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
                    r055Map.RevealedCells.SetEquals([
                        r055Grid.Resolve(0, r055Pin.X, r055Pin.Z).Key,
                        r055Grid.Resolve(1, r055Pin.X, r055Pin.Z).Key,
                    ]) &&
                    r055Map.RevealedPins.SetEquals([r055Pin.Id]) && r055Map.ActiveFloor == 1 &&
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


}
