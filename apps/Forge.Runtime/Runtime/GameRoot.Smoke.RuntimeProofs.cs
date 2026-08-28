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

    private async Task RunModuleMovementSmokeTestAsync(ProjectDefinition project)
    {
        var area = project.Areas.Single(item => item.Id == project.Module.EntryAreaId);
        AssertSmoke(_activeArea?.Id == area.Id,
            "The module movement smoke test must start inside the module entry area.");
        var player = _player ?? throw new InvalidOperationException(
            "The module movement smoke test did not reconstruct the player.");
        var camera = _camera ?? throw new InvalidOperationException(
            "The module movement smoke test did not reconstruct the camera.");
        var start = player.GlobalPosition;
        for (var frame = 0; frame < 120; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);

        AssertSmoke(player.IsOnFloor() && player.GlobalPosition.Y >= start.Y - .5f,
            $"The player did not establish stable ground contact in the module entry area " +
            $"(start_y={start.Y:0.###}, current_y={player.GlobalPosition.Y:0.###}, " +
            $"on_floor={player.IsOnFloor()}).");
        AssertSmoke(_modelLibrary is { Count: > 0 } && _animatedCharacterInstances == _modelLibrary.Count,
            $"The governed animated module characters were not reconstructed " +
            $"(manifests={_modelLibrary?.Count ?? 0}, animated={_animatedCharacterInstances}).");
        var cameraBounds = _interiorWalkableBounds ??
                           throw new InvalidOperationException("The module camera bounds are unavailable.");
        var outsideProbe = new Vector3(cameraBounds.End.X + 10, camera.GlobalPosition.Y,
            cameraBounds.End.Y + 10);
        var resolvedProbe = _moduleExperience.ConstrainCamera
            ? ConstrainCameraPosition(player.GlobalPosition, outsideProbe)
            : outsideProbe;
        AssertSmoke(!_moduleExperience.ConstrainCamera && resolvedProbe.IsEqualApprox(outsideProbe) &&
                    !ConstrainCameraPosition(player.GlobalPosition, outsideProbe).IsEqualApprox(outsideProbe),
            "The authored camera:unbounded contract did not bypass the generic area constraint.");
        GD.Print($"OPENRPG_FORGE_CRYPT_FREE_CAMERA_PASS " +
                 $"camera={camera.GlobalPosition.X:0.###},{camera.GlobalPosition.Y:0.###}," +
                 $"{camera.GlobalPosition.Z:0.###} outside_bounds=true " +
                 $"distance={camera.GlobalPosition.DistanceTo(player.GlobalPosition):0.###}");

        var visual = player.GetNodeOrNull<Node3D>("AuthoredVisual");
        var visualMinimum = Vector3.Zero;
        var visualMaximum = Vector3.Zero;
        AssertSmoke(visual is not null &&
                    TryMeasureNodeBounds(visual, out visualMinimum, out visualMaximum),
            "The module player has no measurable governed 3D visual.");
        var visualCenter = (visualMinimum + visualMaximum) * .5f;
        AssertSmoke(Math.Abs(visualCenter.X) <= .05f && Math.Abs(visualCenter.Z) <= .05f &&
                    Math.Abs(visualMinimum.Y + .9f) <= .4f,
            $"The module player visual is offset from its physical capsule " +
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
            $"Grounded module movement remained blocked " +
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

        var smokeQuest = project.Quests.Single();
        if (_questTracker?.IsAccepted(smokeQuest.Id) != true)
        {
            AssertSmoke(_questTracker?.Accept(smokeQuest.Slug, track: true) == true,
                "The module movement smoke could not accept its authored quest.");
            SyncTrackedQuestDefinition();
        }
        var questStart = GetAvailableQuestTransitions().FirstOrDefault();
        AssertSmoke(questStart is not null, "The module quest has no available starting transition.");
        AdvanceQuest(questStart!);
        InteractWith(_keyItem ?? throw new InvalidOperationException("The Ember Key is missing."));
        AssertSmoke(_hasKey && HasInventoryItem(_questItemSlug),
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
        hud.UpdatePlayerStatus("FIXTURE — PAS DE GAMEPLAY",
            new RuntimeModulePresentation.Resource("health", 30, 30),
            new RuntimeModulePresentation.Resource("resource", 10, 10), 1);
        hud.UpdatePlayerStatus("FIXTURE — PAS DE GAMEPLAY",
            new RuntimeModulePresentation.Resource("health", 18, 30),
            new RuntimeModulePresentation.Resource("resource", 7, 10), 1);
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



}
