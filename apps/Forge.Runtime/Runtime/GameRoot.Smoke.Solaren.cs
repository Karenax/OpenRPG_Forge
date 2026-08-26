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
    private async Task RunSolarenHeadlessSmokeTestAsync()
    {
        var project = _loadedProject ?? throw new InvalidOperationException("The loaded project is unavailable.");
        var area = project.Areas.Single();
        var modelLibrary = _modelLibrary ?? throw new InvalidOperationException("The model library is unavailable.");
        var solarenPlayer = _player ?? throw new InvalidOperationException("The Solaren player is unavailable.");
        var gameplayFoundation = _gameplayControls?.Foundation ??
            throw new InvalidOperationException("The Solaren gameplay foundation is unavailable.");
        var groundedMovement = gameplayFoundation.GroundedMovement ??
            throw new InvalidOperationException("The Solaren movement profile is unavailable.");
        AssertSmoke(string.Equals(_moduleSlug, "solaren-les-vallees-libres", StringComparison.Ordinal),
            "The Solaren smoke test loaded another module.");
        AssertSmoke(!_isCryptPrototype && FindChild("AshRune", recursive: true, owned: false) is null,
            "Solaren unexpectedly reconstructed Crypt-specific atmosphere.");
        AssertSmoke(area.Kind == AreaKind.Exterior && area.Dimensions.Width == 512 && area.Dimensions.Depth == 512,
            "Solaren did not reconstruct its bounded 512 metre exterior.");
        AssertSmoke(area.World.Terrain.SamplesX == 129 && area.World.Terrain.SamplesZ == 129 &&
                    area.World.Features.Count(item => item.Kind == TerrainFeatureKind.Road) == 1,
            "Solaren terrain samples or village road are missing.");
        AssertSmoke(_terrainVertices == 16_641 && _terrainTriangles == 32_768 && _terrainSectors == 64,
            "Solaren terrain did not reach the expected deterministic render plan.");
        AssertSmoke(_terrainScatterLayers >= 15 && _terrainScatterInstances >= 30_000 &&
                    _terrainWindAnimatedLayers == 13 &&
                    _terrainCullingGroups > 0,
            "Solaren biome scatter or its thirteen wind-animated layers did not reconstruct the dressed valley.");
        AssertSmoke(_surfaceTextureCache.Count >= 2,
            "Solaren meadow and cobblestone surfaces did not resolve their governed textures.");
        AssertSmoke(modelLibrary.Count >= 42 && _runtimeModelInstances >= 365,
            "Solaren governed medieval assets were not all available at runtime.");
        var normalizedCharacterClipNames = _animatedCharacterClipNames
            .Select(NormalizeAnimationImportHint)
            .ToHashSet(StringComparer.Ordinal);
        AssertSmoke(_animatedCharacterInstances == 4 && normalizedCharacterClipNames.SetEquals(
            ["death01", "hit_chest", "idle", "jog_fwd", "jump_start", "sword_attack"]),
            "Solaren villagers did not expose the six governed locomotion and action clips.");
        AssertSmoke(_characterAnimations.TryGetValue(solarenPlayer, out var playerAnimation) &&
                    NormalizeAnimationImportHint(playerAnimation.IdleClip) == "idle" &&
                    NormalizeAnimationImportHint(playerAnimation.RunClip) == "jog_fwd" &&
                    NormalizeAnimationImportHint(playerAnimation.JumpClip) == "jump_start" &&
                    playerAnimation.Player.GetAnimation(playerAnimation.IdleClip)?.LoopMode == Animation.LoopModeEnum.Linear &&
                    playerAnimation.Player.GetAnimation(playerAnimation.RunClip)?.LoopMode == Animation.LoopModeEnum.Linear &&
                    GodotAssetAnimationController.Play(playerAnimation, playerAnimation.RunClip, 0, restart: true) &&
                    playerAnimation.CurrentClip == playerAnimation.RunClip,
            "Solaren player locomotion did not map idle and forward jog to looping clips.");
        GodotAssetAnimationController.Play(playerAnimation!, playerAnimation!.IdleClip, 0, restart: true);
        AssertSmoke(_entityWaypointPatrols.Count == 3,
            "Solaren named residents are not bound to their three authored local patrol routes.");
        var patrolPositions = _entityWaypointPatrols.ToDictionary(item => item.Key, item => item.Value.Position);
        AdvanceEntityWaypointPatrols(3);
        AssertSmoke(patrolPositions.All(item =>
                _entityWaypointPatrols[item.Key].Position.DistanceTo(item.Value) > .5f),
            "Solaren named resident patrol routines did not advance along their authored points.");
        GD.Print("OPENRPG_FORGE_SOLAREN_RESIDENT_PATROLS_PASS residents=3 routes=3 moving=true");
        AssertSmoke(Math.Abs(groundedMovement.WalkSpeed - 1.2f) <= .001f &&
                    Math.Abs(groundedMovement.RunSpeed - 2.64f) <= .001f &&
                    _npc is not null && _builtEntityCount == area.Entities.Count,
            "Solaren player, residents or village entities are incomplete.");
        var solarenPresentation = _runtimePresentation ??
            throw new InvalidOperationException("The Solaren runtime presentation is unavailable.");
        AssertSmoke(solarenPresentation.Journal.ActiveObjective is null &&
                    _hud is { ObjectiveTrackerVisible: false },
            "Solaren renders an interactive objective tracker for an honest empty journal state.");
        var solarenDialogue = project.Dialogues.Single(dialogue =>
            string.Equals(dialogue.Slug, "maelis-beacon", StringComparison.Ordinal));
        var narrativeMatch = _narrativeParticipants
            .Where(candidate => solarenDialogue.Participants.Any(participant =>
                participant.BlueprintId == candidate.Value.BlueprintId))
            .ToArray();
        AssertSmoke(narrativeMatch.Length > 0,
            "The Solaren authored dialogue has no reconstructed participant.");
        var (narrativeNode, narrativeParticipant) = narrativeMatch
            .OrderBy(candidate => solarenPlayer.GlobalPosition.DistanceTo(candidate.Key.GlobalPosition))
            .First();
        AssertSmoke(TryActivateNarrativeDialogue(narrativeNode, narrativeParticipant),
            "The Solaren dialogue did not resolve through its authored participant and entry point.");
        var speakingPatrolPosition = _entityWaypointPatrols[narrativeNode].Position;
        AdvanceEntityWaypointPatrols(3);
        AssertSmoke(_entityWaypointPatrols[narrativeNode].Position.IsEqualApprox(speakingPatrolPosition),
            "A Solaren resident continued its patrol while participating in an active dialogue.");
        AssertSmoke(_activeDialogue?.Id == solarenDialogue.Id &&
                    _activeDialogueEntryNodeId == solarenDialogue.EntryPoints.Single(entry =>
                        string.Equals(entry.Key, "default", StringComparison.Ordinal)).NodeId,
            "The Solaren interaction selected a different dialogue document or entry point.");
        InteractWithGuide();
        CommitDialogueChoice(0);
        AssertSmoke(_message.Contains(narrativeParticipant.DisplayName, StringComparison.Ordinal) &&
                    _activeDialogue?.Participants.Any(participant =>
                        participant.BlueprintId == narrativeParticipant.BlueprintId) == true,
            "The Solaren authored dialogue did not execute through the participant interaction.");
        InteractWithGuide();
        CommitDialogueChoice(0);
        UpdateHud();
        var solarenQuest = AssertSingle(project.Quests,
            "The Solaren narrative regression requires one authored quest.");
        var solarenQuestTracker = _questTracker ??
            throw new InvalidOperationException("The Solaren quest tracker is unavailable.");
        AssertSmoke(solarenQuestTracker.IsAccepted(solarenQuest.Id) &&
                    solarenQuestTracker.TrackedQuestId == solarenQuest.Id &&
                    solarenQuestTracker.GetCurrentState(solarenQuest.Id)?.Id != solarenQuest.InitialStateId &&
                    _runtimePresentation?.Journal.ActiveEntry?.Id == solarenQuest.Id.ToString() &&
                    _hud is { ObjectiveTrackerVisible: true },
            "The authored Solaren dialogue choice did not accept, advance and track its quest.");
        var acceptedStateId = solarenQuestTracker.GetCurrentState(solarenQuest.Id)?.Id;
        InteractWithGuide();
        CommitDialogueChoice(0);
        InteractWithGuide();
        CommitDialogueChoice(0);
        AssertSmoke(TryActivateNarrativeDialogue(narrativeNode, narrativeParticipant),
            "The Solaren dialogue could not be replayed for its idempotence regression.");
        InteractWithGuide();
        CommitDialogueChoice(0);
        InteractWithGuide();
        CommitDialogueChoice(0);
        UpdateHud();
        AssertSmoke(solarenQuestTracker.GetCurrentState(solarenQuest.Id)?.Id == acceptedStateId &&
                    solarenQuestTracker.TrackedQuestId == solarenQuest.Id,
            "Replaying StartQuest advanced or untracked an already accepted Solaren quest.");
        var patrolPositionBeforeAbort = _entityWaypointPatrols[narrativeNode].Position;
        _dialogueOverlay?.Close();
        SyncPlayerInterfaceLock();
        AdvanceEntityWaypointPatrols(3);
        AssertSmoke(_activeDialogueParticipant is null &&
                    _entityWaypointPatrols[narrativeNode].Position.DistanceTo(patrolPositionBeforeAbort) > .5f,
            "Closing an unfinished Solaren dialogue did not release its resident patrol.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_SOLAREN_NARRATIVE_PASS dialogue={solarenDialogue.Id} " +
                 $"entry={_activeDialogueEntryNodeId} participant={narrativeParticipant.BlueprintId} " +
                 $"quest={solarenQuest.Id} state={solarenQuestTracker.GetCurrentState(solarenQuest.Id)?.Id} crypt=false");

        var beaconEntity = area.Entities.Single(entity =>
            entity.Tags.Contains("quest-beacon", StringComparer.Ordinal));
        AssertSmoke(_entityNodes.TryGetValue(beaconEntity.Id.Value, out var beaconNode),
            "The authored Solaren quest beacon has no reconstructed runtime node.");
        var beaconInteraction = EntityClickTriggerSettings.Read(beaconEntity.Overrides);
        AssertSmoke(beaconInteraction.Enabled &&
                    beaconInteraction.Action == EntityClickTriggerAction.StartDialogue,
            "The authored Solaren quest beacon is not linked to its interaction dialogue.");
        ExecuteClickInteraction(beaconNode!, beaconInteraction);
        CommitDialogueChoice(0);
        UpdateHud();
        var beaconState = solarenQuestTracker.GetCurrentState(solarenQuest.Id);
        AssertSmoke(_blackboard.TryGetValue("quest.objectives", out var beaconVariable) &&
                    string.Equals(beaconVariable, "complete", StringComparison.Ordinal) &&
                    beaconState?.IsTerminal == true &&
                    solarenQuestTracker.IsAccepted(solarenQuest.Id) &&
                    _hud is { ObjectiveTrackerVisible: false },
            "Lighting the authored Solaren beacon did not complete the active quest once.");
        _dialogueOverlay?.Close();
        SyncPlayerInterfaceLock();
        GD.Print($"OPENRPG_FORGE_RUNTIME_SOLAREN_BEACON_PASS beacon={beaconEntity.Id} " +
                 $"dialogue={beaconInteraction.TargetId} quest={solarenQuest.Id} state={beaconState?.Id} " +
                 "variable=quest.objectives:complete");
        AssertSmoke(solarenPresentation.Map.Markers.Count == 8 &&
                    _moduleMap is
                    {
                        GlobalMapUsesLocalAtlas: true,
                        GlobalRenderedMarkerCount: 8,
                        GlobalMapLabelOverlapCount: 0,
                    },
            "The one-area Solaren atlas counts authored pins without rendering its local markers.");

        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var doorwayEntities = area.Entities.Where(item => item.Tags.Contains("doorway", StringComparer.Ordinal)).ToArray();
        var openDoorEntities = area.Entities.Where(item => item.Tags.Contains("open-door", StringComparer.Ordinal)).ToArray();
        AssertSmoke(doorwayEntities.Length == 4 && openDoorEntities.Length == 4,
            "Solaren houses do not expose one open entrance per building.");
        var physics = GetWorld3D().DirectSpaceState;
        var playerPositionBeforeDoorChecks = solarenPlayer.GlobalPosition;
        foreach (var doorway in doorwayEntities)
        {
            var houseTag = doorway.Tags.Single(tag => tag.StartsWith("house-", StringComparison.Ordinal));
            var stairs = area.Entities.Single(item => item.Tags.Contains(houseTag, StringComparer.Ordinal) &&
                item.Tags.Contains("entrance", StringComparer.Ordinal) &&
                item.Tags.Contains("nonblocking", StringComparer.Ordinal) &&
                !item.Tags.Contains("open-door", StringComparer.Ordinal));
            AssertSmoke(_entityNodes.TryGetValue(doorway.Id.Value, out var doorwayNode) &&
                        doorwayNode.FindChildren("*", "CollisionShape3D", recursive: true, owned: false).Count == 0,
                $"Solaren entrance '{houseTag}' retained a full-wall collision across the opening.");
            var outward = doorwayNode!.GlobalTransform.Basis * Vector3.Back;
            outward.Y = 0;
            outward = outward.Normalized();
            var authoredOutside = new Vector3(
                stairs.Transform.Position.X - doorway.Transform.Position.X,
                0,
                stairs.Transform.Position.Z - doorway.Transform.Position.Z);
            if (outward.Dot(authoredOutside) < 0) outward = -outward;
            var threshold = doorwayNode.GlobalPosition + (Vector3.Up * 1.05f);
            var query = PhysicsRayQueryParameters3D.Create(
                threshold + (outward * 1.25f),
                threshold - (outward * 1.5f));
            query.Exclude = [solarenPlayer.GetRid()];
            AssertSmoke(physics.IntersectRay(query).Count == 0,
                $"Solaren entrance '{houseTag}' is blocked by an authored collision.");

            var entranceOutside = doorwayNode.GlobalPosition + (outward * 1.2f);
            var entranceInside = doorwayNode.GlobalPosition - (outward * 1.2f);
            var maximumGround = Enumerable.Range(0, 13)
                .Select(index => entranceOutside.Lerp(entranceInside, index / 12f))
                .Max(point => _activeTerrain?.HeightAt(point.X, point.Z) ?? doorwayNode.GlobalPosition.Y);
            solarenPlayer.GlobalPosition = new Vector3(
                entranceOutside.X,
                maximumGround + 1.05f,
                entranceOutside.Z);
            solarenPlayer.Velocity = Vector3.Zero;
            solarenPlayer.ResetPhysicsInterpolation();
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var entranceStart = solarenPlayer.GlobalPosition;
            var entranceCollision = solarenPlayer.MoveAndCollide(-outward * 2.4f);
            var enteredDistance = (solarenPlayer.GlobalPosition - entranceStart).Dot(-outward);
            var entranceCollider = entranceCollision?.GetCollider() as Node;
            AssertSmoke(entranceCollision is null && enteredDistance >= 2.3f,
                $"Solaren player could not physically enter '{houseTag}' " +
                $"(distance={enteredDistance:0.###}, collider={entranceCollider?.Name ?? "unknown"}, " +
                $"parent={entranceCollider?.GetParent()?.Name ?? "unknown"}, " +
                $"position={entranceCollision?.GetPosition()}).");
        }
        solarenPlayer.GlobalPosition = playerPositionBeforeDoorChecks;
        solarenPlayer.Velocity = Vector3.Zero;
        solarenPlayer.ResetPhysicsInterpolation();

        var raisedCamera = RuntimeCameraRig.RaiseForTerrainClearance(
            new Vector3(0, 2, 0),
            new Vector3(0, 7, 12),
            (_, z) => z * .6f,
            .9f,
            32);
        AssertSmoke(raisedCamera.Y > 8 &&
                    Math.Abs(new Vector2(raisedCamera.X, raisedCamera.Z).Length() - 12) <= .001f,
            "Solaren slope clearance retracted the camera horizontally instead of raising it above terrain.");

        var roadPoints = area.World.Features.Single(item => item.Kind == TerrainFeatureKind.Road).ControlPoints;
        var steepestRoadSegment = roadPoints.Zip(roadPoints.Skip(1), (first, second) => (First: first, Second: second))
            .OrderByDescending(segment => Math.Abs(segment.Second.Y - segment.First.Y))
            .First();
        var lower = steepestRoadSegment.First.Y <= steepestRoadSegment.Second.Y
            ? steepestRoadSegment.First
            : steepestRoadSegment.Second;
        var higher = steepestRoadSegment.First.Y <= steepestRoadSegment.Second.Y
            ? steepestRoadSegment.Second
            : steepestRoadSegment.First;
        var uphill = new Vector2(higher.X - lower.X, higher.Z - lower.Z).Normalized();
        var slopeFocus = new Vector3(lower.X, lower.Y + 1.25f, lower.Z);
        var slopeDesired = slopeFocus + new Vector3(uphill.X * 14, 5.7f, uphill.Y * 14);
        var slopeConstrained = ConstrainCameraPosition(slopeFocus, slopeDesired);
        var constrainedHorizontalDistance = new Vector2(
            slopeConstrained.X - slopeFocus.X,
            slopeConstrained.Z - slopeFocus.Z).Length();
        AssertSmoke(constrainedHorizontalDistance >= 13.5f,
            $"Solaren's live terrain collision collapsed the slope camera " +
            $"(horizontal={constrainedHorizontalDistance:0.###}).");

        var activeCameraProfile = GameplayFoundationRuntime.ResolveCamera(gameplayFoundation.Cameras);
        var occlusionFocus = solarenPlayer.GlobalPosition + (Vector3.Up * 20);
        var occlusionDesired = occlusionFocus + (Vector3.Back * 14);
        var syntheticOccluder = new StaticBody3D
        {
            Name = "ForgeCameraOcclusionSmokeObstacle",
            Position = occlusionFocus + (Vector3.Back * 4),
        };
        syntheticOccluder.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(2.5f, 2.5f, 1.2f) },
        });
        AddChild(syntheticOccluder);
        Vector3 occlusionBlocked;
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            occlusionBlocked = ConstrainCameraPosition(occlusionFocus, occlusionDesired);
        }
        finally
        {
            RemoveChild(syntheticOccluder);
            syntheticOccluder.Free();
        }
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        var occlusionReleased = ConstrainCameraPosition(occlusionFocus, occlusionDesired);
        var blockedDistance = occlusionFocus.DistanceTo(occlusionBlocked);
        var releasedDistance = occlusionFocus.DistanceTo(occlusionReleased);
        var activeOcclusion = _cameraRig.ResolveOcclusion(14, 3);
        var localVisual = solarenPlayer.GetNodeOrNull<Node3D>("AuthoredVisual");
        AssertSmoke(activeCameraProfile.CollisionAvoidance &&
                    (activeCameraProfile.OcclusionStrategy != CameraOcclusionStrategy.Retract ||
                     blockedDistance < 8) &&
                    releasedDistance >= 13.5f &&
                    (activeOcclusion.OccluderTreatment switch
                    {
                        CameraOccluderTreatment.None => activeOcclusion.CameraDistance < 14,
                        CameraOccluderTreatment.Fade or CameraOccluderTreatment.Hide =>
                            Math.Abs(activeOcclusion.CameraDistance - 14) <= .001f,
                        _ => false,
                    }) &&
                    solarenPlayer.Visible && localVisual?.Visible != false,
            $"The generic camera occlusion policy did not release cleanly " +
            $"(strategy={activeCameraProfile.OcclusionStrategy}, blocked={blockedDistance:0.###}, " +
            $"released={releasedDistance:0.###}, player_visible={solarenPlayer.Visible}).");
        GD.Print($"OPENRPG_FORGE_SOLAREN_CAMERA_OCCLUSION_PASS " +
                 $"strategy={activeCameraProfile.OcclusionStrategy.ToString().ToLowerInvariant()} " +
                 $"blocked={blockedDistance:0.###} released={releasedDistance:0.###} " +
                 "player_visible=true transient=true");

        var solarenCamera = _camera ?? throw new InvalidOperationException("The Solaren camera is unavailable.");
        var orbitButton = new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true };
        _cameraRig.HandleInput(orbitButton);
        try
        {
            _cameraRig.HandleInput(new InputEventMouseMotion { Relative = new Vector2(0, -100_000) });
        }
        finally
        {
            _cameraRig.HandleInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Right,
                Pressed = false,
            });
        }
        var upwardCameraTarget = solarenPlayer.GlobalPosition;
        await MutateCameraInPhysicsAsync(() =>
            _cameraRig.Snap(solarenCamera, upwardCameraTarget, (focus, desired) =>
                RuntimeCameraRig.RaiseForTerrainClearance(focus, desired, _activeTerrain!.HeightAt,
                    _cameraRig.TerrainClearance, _cameraRig.TerrainProbeSamples)));
        var groundHeightBelowCamera = _activeTerrain!.HeightAt(
            solarenCamera.GlobalPosition.X, solarenCamera.GlobalPosition.Z);
        var upwardView = -solarenCamera.GlobalTransform.Basis.Z;
        AssertSmoke(_cameraRig.MinimumPitchDegrees <= -84.9f &&
                    Math.Abs(_cameraRig.PitchDegrees - _cameraRig.MinimumPitchDegrees) <= .01f &&
                    solarenCamera.GlobalPosition.Y >= groundHeightBelowCamera + _cameraRig.TerrainClearance - .02f &&
                    upwardView.Y >= .35f,
            $"Solaren's ground-level orbit camera still cannot look into the sky " +
            $"(pitch={_cameraRig.PitchDegrees:0.###}, minimum={_cameraRig.MinimumPitchDegrees:0.###}, " +
            $"view_y={upwardView.Y:0.###}, clearance=" +
            $"{solarenCamera.GlobalPosition.Y - groundHeightBelowCamera:0.###}).");
        GD.Print($"OPENRPG_FORGE_SOLAREN_CAMERA_LOOK_UP_PASS " +
                 $"pitch={_cameraRig.PitchDegrees:0.###} view_y={upwardView.Y:0.###} " +
                 $"clearance={solarenCamera.GlobalPosition.Y - groundHeightBelowCamera:0.###}");
        _cameraRig.Configure(gameplayFoundation.Cameras);
        var restoredCameraTarget = solarenPlayer.GlobalPosition;
        await MutateCameraInPhysicsAsync(() =>
            _cameraRig.Snap(solarenCamera, restoredCameraTarget, ConstrainCameraPosition));

        var readableNpcProbe = new Node3D { Name = "ForgeWorldLabelSmokeNpc" };
        AddChild(readableNpcProbe);
        readableNpcProbe.GlobalPosition = solarenCamera.GlobalPosition + (Vector3.Right * 8);
        CreateLabel(readableNpcProbe, "NPC", Colors.White, .5f);
        var readableNpc = readableNpcProbe.GetNode<Label3D>(RuntimeWorldLabelNodeName);
        UpdateWorldLabels();
        var localHeroLabel = _worldLabels.Single(pair => pair.Value.IsLocalHero);
        var localHeroDistance = solarenCamera.GlobalPosition.DistanceTo(localHeroLabel.Key.GlobalPosition);
        var localHeroPresentation = _cameraRig.ResolveWorldLabel(localHeroDistance, isLocalHero: true);
        AssertSmoke(!localHeroLabel.Key.FixedSize && localHeroLabel.Key.Visible &&
                    Math.Abs(localHeroLabel.Key.Scale.X - localHeroPresentation.UniformScale) <= .001f &&
                    readableNpc.Visible,
            "The runtime world-label policy was not applied to the local hero and a readable NPC.");
        var zoomToFirstPerson = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.WheelUp,
            Pressed = true,
        };
        for (var step = 0; step < 32 && !_cameraRig.FirstPersonActive; step++)
            _cameraRig.HandleInput(zoomToFirstPerson);
        SyncLocalPlayerCameraVisibility();
        UpdateWorldLabels();
        AssertSmoke(_cameraRig.FirstPersonActive && !localHeroLabel.Key.Visible &&
                    readableNpc.Visible,
            "First person did not hide only the local hero world label while preserving a readable NPC label.");
        GD.Print($"OPENRPG_FORGE_SOLAREN_WORLD_LABEL_PASS " +
                 $"near_scale={localHeroPresentation.UniformScale:0.###} " +
                 $"npc_scale={readableNpc.Scale.X:0.###} " +
                 "local_first_person_hidden=true npc_readable=true fixed_size=false");
        _cameraRig.Configure(gameplayFoundation.Cameras);
        SyncLocalPlayerCameraVisibility();
        await MutateCameraInPhysicsAsync(() =>
            _cameraRig.Snap(solarenCamera, restoredCameraTarget, ConstrainCameraPosition));
        UpdateWorldLabels();
        _worldLabels.Remove(readableNpc);
        RemoveChild(readableNpcProbe);
        readableNpcProbe.Free();

        var initialWeather = _weatherState ??
            throw new InvalidOperationException("Solaren weather state is unavailable.");
        var initialWeatherProfile = WeatherRuntimeStateMachine.Resolve(area.Environment.Weather, initialWeather);
        var afterTwoMinutes = WeatherRuntimeStateMachine.Advance(
            area.Environment.Weather,
            initialWeather,
            120,
            hasAuthority: true);
        AssertSmoke(afterTwoMinutes.Transition is null && afterTwoMinutes.State.Revision == 0 &&
                    area.Environment.Weather.States.All(item => item.MinimumDurationSeconds >= 420),
            "Solaren weather still changes within two minutes.");
        var rainProfile = area.Environment.Weather.States.Single(item => item.Kind == WeatherKind.Rain);
        var rainPlan = AreaClimatePlanBuilder.Build(area.Environment, rainProfile.Id,
            new(solarenPlayer.GlobalPosition.X, solarenPlayer.GlobalPosition.Y, solarenPlayer.GlobalPosition.Z),
            "desktop");
        var rainEffects = GodotClimateAdapter.Build(rainPlan, configureRuntimeAudio: false);
        var rainParticles = rainEffects.Root.GetChildren().OfType<GpuParticles3D>().Single();
        var rainMaterial = rainParticles.ProcessMaterial as ParticleProcessMaterial;
        var authoredRainMotion = GodotClimateAdapter.DescribePrecipitationMotion(
            rainProfile.Kind,
            rainPlan.Wind,
            rainProfile.Precipitation);
        var authoredRainAmount = Math.Max(
            rainProfile.Precipitation.MinimumParticles,
            (int)(rainProfile.Precipitation.ParticlesPerIntensity * rainProfile.Intensity));
        AssertSmoke(rainMaterial is not null &&
                    rainMaterial.Direction.DistanceTo(authoredRainMotion.Direction) <= .001f &&
                    Math.Abs(rainMaterial.Spread - authoredRainMotion.SpreadDegrees) <= .001f &&
                    rainMaterial.Gravity.DistanceTo(authoredRainMotion.Gravity) <= .001f &&
                    Math.Abs(rainMaterial.InitialVelocityMin - authoredRainMotion.MinimumVelocity) <= .001f &&
                    Math.Abs(rainMaterial.InitialVelocityMax - authoredRainMotion.MaximumVelocity) <= .001f &&
                    rainParticles.Amount == authoredRainAmount &&
                    Math.Abs(rainParticles.Preprocess - rainProfile.Precipitation.LifetimeSeconds) <= .001f,
            $"Solaren procedural rain diverged from its authored generic precipitation profile " +
            $"(direction={rainMaterial?.Direction}, spread={rainMaterial?.Spread:0.###}, " +
            $"gravity={rainMaterial?.Gravity}, amount={rainParticles.Amount}, " +
            $"preprocess={rainParticles.Preprocess:0.###}).");
        GD.Print($"OPENRPG_FORGE_SOLAREN_RAIN_PROFILE_PASS " +
                 $"direction={rainMaterial!.Direction.X:0.###},{rainMaterial.Direction.Y:0.###}," +
                 $"{rainMaterial.Direction.Z:0.###} spread={rainMaterial.Spread:0.###} " +
                 $"amount={rainParticles.Amount} preprocess={rainParticles.Preprocess:0.###} authored=true");
        rainEffects.Root.Free();

        _surfaceWetnessCoefficients = rainProfile.WetnessMaterial;
        _surfaceWetnessValue = 0;
        _surfaceWetnessTransition = SurfaceWetnessTransition.Start(0, rainProfile.SurfaceWetness, 2);
        var dryProjection = _surfaceWetnessMaterials.Apply(0, rainProfile.WetnessMaterial);
        _surfaceWetnessResult = dryProjection;
        AdvanceSurfaceWetness(1);
        var wettingProjection = _surfaceWetnessResult ??
            throw new InvalidOperationException("The wetness transition produced no midpoint projection.");
        AdvanceSurfaceWetness(1);
        var wetProjection = _surfaceWetnessResult ??
            throw new InvalidOperationException("The wetness transition produced no rain projection.");
        _surfaceWetnessTransition = SurfaceWetnessTransition.Start(_surfaceWetnessValue, 0, 2);
        AdvanceSurfaceWetness(1);
        var dryingProjection = _surfaceWetnessResult ??
            throw new InvalidOperationException("The drying transition produced no midpoint projection.");
        AdvanceSurfaceWetness(1);
        var restoredProjection = _surfaceWetnessResult ??
            throw new InvalidOperationException("The drying transition produced no restored projection.");
        var drySample = dryProjection.PbrSample ??
            throw new InvalidOperationException("The Solaren world exposes no governed PBR wetness sample.");
        var wetSample = wetProjection.PbrSample ??
            throw new InvalidOperationException("The Solaren rain exposes no governed PBR wetness sample.");
        var restoredSample = restoredProjection.PbrSample ??
            throw new InvalidOperationException("The Solaren drying pass exposes no governed PBR wetness sample.");
        AssertSmoke(dryProjection.ShaderMaterials + dryProjection.PbrMaterials > 0 &&
                    wettingProjection.SurfaceWetness > 0 &&
                    wettingProjection.SurfaceWetness < rainProfile.SurfaceWetness &&
                    Math.Abs(wetProjection.SurfaceWetness - rainProfile.SurfaceWetness) <= .001f &&
                    dryingProjection.SurfaceWetness > 0 &&
                    dryingProjection.SurfaceWetness < rainProfile.SurfaceWetness &&
                    restoredProjection.SurfaceWetness == 0 &&
                    wetSample.Projected.AlbedoR <= drySample.Dry.AlbedoR &&
                    wetSample.Projected.AlbedoG <= drySample.Dry.AlbedoG &&
                    wetSample.Projected.AlbedoB <= drySample.Dry.AlbedoB &&
                    wetSample.Projected.Roughness <= drySample.Dry.Roughness &&
                    wetSample.Projected.Specular >= drySample.Dry.Specular &&
                    wetSample.Projected.Alpha == drySample.Dry.Alpha &&
                    restoredSample.Projected == drySample.Dry &&
                    restoredProjection.ShaderMaterials == dryProjection.ShaderMaterials &&
                    restoredProjection.PbrMaterials == dryProjection.PbrMaterials,
            "The runtime wetness projection is not bounded, transitional and exactly reversible.");
        GD.Print($"OPENRPG_FORGE_SOLAREN_WETNESS_TRANSITION_PASS " +
                 $"dry={dryProjection.SurfaceWetness:0.###} " +
                 $"mid={wettingProjection.SurfaceWetness:0.###} " +
                 $"rain={wetProjection.SurfaceWetness:0.###} " +
                 $"drying={dryingProjection.SurfaceWetness:0.###} " +
                 $"restored={restoredProjection.SurfaceWetness:0.###} " +
                 $"shader_materials={wetProjection.ShaderMaterials} " +
                 $"pbr_materials={wetProjection.PbrMaterials} reversible=true");

        var advancedWeather = WeatherRuntimeStateMachine.Advance(
            area.Environment.Weather,
            initialWeather,
            initialWeatherProfile.MaximumDurationSeconds + 1,
            hasAuthority: true);
        AssertSmoke(advancedWeather.Transition is not null && advancedWeather.State.Revision == 1 &&
                    advancedWeather.State.StateId != initialWeather.StateId,
            "Solaren random weather did not produce its first authoritative transition.");
        _weatherState = advancedWeather.State;
        ApplyClimateEffects(force: true);
        var advancedWeatherProfile = WeatherRuntimeStateMachine.Resolve(
            area.Environment.Weather,
            advancedWeather.State);
        AdvanceSurfaceWetness(Math.Max(1, advancedWeatherProfile.TransitionSeconds));
        AssertSmoke(_referenceSun is not null && _worldEnvironment is not null &&
                    _referenceSun.LightEnergy < _baseDirectionalLightEnergy &&
                    _worldEnvironment.BackgroundEnergyMultiplier < _baseBackgroundEnergyMultiplier &&
                    Math.Abs(_surfaceWetnessValue - advancedWeatherProfile.SurfaceWetness) <= .001f &&
                    _surfaceWetnessResult is { ShaderMaterials: > 0 },
            "Solaren weather transition did not darken the sun and sky.");
        GD.Print("OPENRPG_FORGE_SOLAREN_SMOKE_PASS " +
                 $"area={area.Slug} terrain={_terrainVertices}/{_terrainTriangles}/{_terrainSectors} " +
                 $"scatter={_terrainScatterLayers}/{_terrainScatterInstances}/{_terrainCullingGroups} " +
                 $"wind={_terrainWindAnimatedLayers} " +
                 $"models={modelLibrary.Count}/{_runtimeModelInstances} animated={_animatedCharacterInstances}/6 " +
                 $"entities={_builtEntityCount} movement={groundedMovement.WalkSpeed:0.##}/{groundedMovement.RunSpeed:0.##} " +
                 $"doors={doorwayEntities.Length}/entered rain=authored/prewarmed weather_min=420 " +
                 $"weather_revision={_weatherState.Revision} wetness={_surfaceWetnessValue:0.###}/reversible " +
                 "terrain_camera=raised/look-up/occlusion labels=adaptive crypt=false");
        GetTree().Quit(0);
    }

}
