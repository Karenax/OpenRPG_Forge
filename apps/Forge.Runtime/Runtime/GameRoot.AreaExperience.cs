using System.Diagnostics;
using System.Globalization;
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
    private void ConfigureAudioEmitters(AreaAudioProfile profile)
    {
        var playbacks = AudioEmitterRuntimeService.CreateInitialPlaybacks(
            profile.Emitters, 2026, EvaluateRuntimePolicyCondition);
        foreach (var playback in playbacks)
        {
            var definition = profile.Emitters.Definitions.Single(item => item.Id == playback.DefinitionId);
            var stream = LoadAudioStream(playback.ResourcePath);
            if (stream is null)
            {
                var level = definition.Required ? "required" : "optional";
                GD.PrintErr($"OPENRPG_FORGE_RUNTIME_AUDIO_EMITTER_DIAGNOSTIC code=audio.resource.unavailable " +
                    $"path=environment/audio/emitters/{definition.Id}/clips/{playback.ClipId} " +
                    $"uuid={definition.Id} level={level}");
                continue;
            }
            stream = ConfigureLoopStream(stream, playback.Loop);

            var bus = playback.MixBusId is { } busId
                ? profile.Orchestration.MixBuses.FirstOrDefault(item => item.Id == busId)?.Name ?? "Master"
                : "Master";
            if (AudioServer.GetBusIndex(bus) < 0) bus = "Master";
            var player = new AudioStreamPlayer3D
            {
                Name = $"ForgeAudioEmitter_{playback.Handle.Id:N}",
                Stream = stream,
                Position = EngineValueConverter.ToEngine(playback.Transform.Position) +
                    new Vector3(0, playback.Spatial.HeightOffset, 0),
                UnitSize = Math.Max(.01f, playback.Spatial.InnerRadius),
                MaxDistance = playback.Spatial.OuterRadius,
                EmissionAngleEnabled = playback.Spatial.ConeAngleDegrees < 360,
                EmissionAngleDegrees = Math.Clamp(playback.Spatial.ConeAngleDegrees / 2, 0, 90),
                EmissionAngleFilterAttenuationDb = playback.Spatial.ConeAttenuationDb,
                VolumeDb = Mathf.LinearToDb(Math.Max(.0001f, playback.Volume)) -
                    (24 * playback.Spatial.Occlusion),
                Bus = bus,
                Autoplay = playback.Autoplay && playback.StartDelaySeconds <= 0,
            };
            var remaining = playback.Loop.Mode == AudioLoopMode.FiniteCount
                ? Math.Max(1, playback.Loop.RepeatCount)
                : 1;
            player.Finished += () =>
            {
                if (playback.Loop.Mode is AudioLoopMode.Loop or AudioLoopMode.SeamlessRegion)
                {
                    player.Play();
                    return;
                }
                if (playback.Loop.Mode == AudioLoopMode.FiniteCount && --remaining > 0)
                {
                    player.Play();
                    return;
                }
                player.QueueFree();
                GD.Print($"OPENRPG_FORGE_RUNTIME_AUDIO_EMITTER uuid={playback.Handle.Id} event=completed");
            };
            AddChild(player);
            if (playback.Autoplay && playback.StartDelaySeconds > 0)
            {
                var timer = new Godot.Timer
                {
                    Name = $"ForgeAudioDelay_{playback.Handle.Id:N}",
                    WaitTime = playback.StartDelaySeconds,
                    OneShot = true,
                    Autostart = true,
                };
                timer.Timeout += () =>
                {
                    if (GodotObject.IsInstanceValid(player)) player.Play();
                    timer.QueueFree();
                };
                AddChild(timer);
            }
            GD.Print($"OPENRPG_FORGE_RUNTIME_AUDIO_EMITTER uuid={playback.Handle.Id} " +
                $"clip={playback.ClipId} spatial=true loop={playback.Loop.Mode.ToString().ToLowerInvariant()} " +
                $"autoplay={playback.Autoplay.ToString().ToLowerInvariant()}");
        }
    }

    private void ConfigureAreaAmbientAudio(AreaAudioProfile profile)
    {
        var resolution = AudioAuthoringRuntimeResolver.Resolve(profile.Authoring,
            profile.Authoring.RequiredAtRuntime);
        foreach (var diagnostic in resolution.Diagnostics)
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_AUDIO_DIAGNOSTIC code={diagnostic.Code} " +
                        $"path={diagnostic.Path}");
        ConfigureAudioRangeDebug(profile);
        if (!resolution.Available || !profile.RuntimeControl.InitiallyActive) return;

        var resources = profile.Playlist.Count > 0
            ? profile.Playlist
            : string.IsNullOrWhiteSpace(profile.Ambience)
                ? []
                : [profile.Ambience];
        if (resources.Count == 0) return;
        var listener = new AudioListenerContext(0, 12, string.Empty,
            new HashSet<string>(StringComparer.Ordinal), 0, false,
            _currentAreaId ?? string.Empty, "local-player", true,
            "local-party", "local-party", -18);
        var mix = AreaAudioMixer.Evaluate(profile, listener);
        if (!mix.ListenerAllowed || mix.NormalizedGain <= 0) return;

        var player = new AudioStreamPlayer
        {
            Name = "ForgeAreaAmbientAudio",
            Bus = AudioServer.GetBusIndex(mix.Bus) >= 0 ? mix.Bus : "Master",
            VolumeDb = Mathf.LinearToDb(Math.Max(.0001f, mix.NormalizedGain)) + mix.DuckingDb,
        };
        AddChild(player);
        var cues = profile.Playlist.Count > 0
            ? AreaAudioSequencePlanner.Plan(profile, Math.Max(64, profile.Playlist.Count * 4), 2026)
            : [new PlannedAudioCue(profile.Ambience, 0, profile.RelativeVolume, 0, mix.Bus)];
        var index = 0;
        Action playNext = null!;
        playNext = () =>
        {
            if (!GodotObject.IsInstanceValid(player) || index >= cues.Count) return;
            var cue = cues[index++];
            var stream = LoadAudioStream(cue.Resource);
            if (stream is null)
            {
                var level = profile.Authoring.RequiredAtRuntime ? "required" : "optional";
                GD.PrintErr("OPENRPG_FORGE_RUNTIME_AUDIO_DIAGNOSTIC " +
                            $"code=runtime.audio.resource_unavailable path=environment/audio/playlist/{index - 1} " +
                            $"uuid={profile.Authoring.Id:D} level={level}");
                playNext();
                return;
            }
            player.Stream = profile.Playlist.Count == 0
                ? ConfigureLoopStream(stream, new AudioLoopPolicy(AudioLoopMode.Loop, 0, 0, 0, 0, string.Empty))
                : stream;
            player.PitchScale = MathF.Pow(2, cue.PitchSemitones / 12f);
            player.VolumeDb = Mathf.LinearToDb(Math.Max(.0001f,
                Math.Min(mix.NormalizedGain * cue.RelativeVolume, profile.Authoring.Mix.AccessibilityVolumeCap))) +
                              mix.DuckingDb;
            player.Play();
            ShowAudioCaption(profile, cue.Resource, Vector3Value.Zero);
        };
        player.Finished += () =>
        {
            if (profile.Playlist.Count == 0)
            {
                player.Play();
                return;
            }
            if (index >= cues.Count) return;
            var timer = new Godot.Timer
            {
                WaitTime = Math.Max(.001, cues[index].DelaySeconds),
                OneShot = true,
                Autostart = true,
            };
            timer.Timeout += () =>
            {
                timer.QueueFree();
                playNext();
            };
            AddChild(timer);
        };
        playNext();
        var previousContext = listener with { Hour = listener.Hour < 12 ? 22 : 2, Weather = "rain" };
        ConfigureAdaptiveAmbienceAudio(profile, previousContext, listener);
        var monitored = MonitorRuntimeMix(profile);
        GD.Print("OPENRPG_FORGE_RUNTIME_AREA_AUDIO " +
                 $"uuid={profile.Authoring.Id:D} mode={profile.OrderMode.ToString().ToLowerInvariant()} " +
                 $"bus={player.Bus} listener={profile.Authoring.Ambient.ListenerPolicy.ToString().ToLowerInvariant()} " +
                 $"lufs={profile.Authoring.Mix.TargetLoudnessLufs:0.0} meter={mix.MeterLufs:0.0} " +
                 $"adaptive={_adaptiveAmbiencePlayers.Count} monitored={monitored.Count}");
    }

    private AudioCaptionRuntimeResolution ShowAudioCaption(AreaAudioProfile profile, string resource,
        Vector3Value sourcePosition)
    {
        var listenerPosition = _player is null
            ? Vector3Value.Zero
            : new Vector3Value(_player.GlobalPosition.X, _player.GlobalPosition.Y, _player.GlobalPosition.Z);
        var engineForward = _camera is null ? Vector3.Forward : -_camera.GlobalTransform.Basis.Z;
        var listenerForward = new Vector3Value(engineForward.X, engineForward.Y, engineForward.Z);
        var required = profile.Orchestration.Captions.FirstOrDefault(item =>
            item.Resource.Equals(resource, StringComparison.Ordinal))?.RequiredAtRuntime ?? false;
        var resolution = AudioCaptionRuntimeService.Resolve(profile, resource, _defaultLocale, "fr",
            listenerPosition, listenerForward, sourcePosition, required);
        foreach (var diagnostic in resolution.Diagnostics)
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_AUDIO_CAPTION_DIAGNOSTIC code={diagnostic.Code} " +
                        $"path={diagnostic.Path}");
        if (resolution.Presentation is not { } presentation || _hud is null) return resolution;
        _hud.ShowSoundCaption(presentation);
        GetTree().CreateTimer(presentation.DisplaySeconds).Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(_hud)) _hud.ClearSoundCaption();
        };
        GD.Print($"OPENRPG_FORGE_RUNTIME_AUDIO_CAPTION uuid={presentation.Id:D} " +
                 $"locale={presentation.Locale} kind={presentation.CueKind} " +
                 $"importance={presentation.Importance} direction={presentation.Direction.ToString().ToLowerInvariant()}");
        return resolution;
    }

    private void ConfigureWaypointRuntime(WaypointAuthoringProfile profile)
    {
        foreach (var diagnostic in WaypointRuntimeService.ValidateRuntime(profile))
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_WAYPOINT_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        var marker = WaypointRuntimeService.VisibleMarkers(profile, _waypointRuntimeState,
            MarkerVisibilityScope.Party).FirstOrDefault();
        _trackedWaypointMarker = marker;
        if (marker is not null)
        {
            _hud?.ShowWaypointMarker(marker);
            _moduleMap?.ShowPing(marker.Position, RuntimeMapPingKind.Objective, marker.AccessibleText);
        }
        BuildWaypointLocatorsAndRouteDebug(profile);
        ConfigureEntityWaypointPatrols(profile);
        var route = profile.PatrolRoutes.FirstOrDefault();
        if (route is null) return;
        var resolution = WaypointRuntimeService.Advance(profile, _waypointRuntimeState, route.Id);
        _waypointRuntimeState = resolution.State;
        if (resolution.Step is { } step) ApplyWaypointPatrolStep(step);
    }

    private void BuildGameplaySimulationAdapters(GameplaySimulationProfile profile)
    {
        foreach (var diagnostic in GameplaySimulationProfileValidator.ValidateRuntime(profile,
                     profile.RequiredAtRuntime))
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_GAMEPLAY_SIMULATION_DIAGNOSTIC code={diagnostic.Code} " +
                        $"path={diagnostic.Path}");
        if (_gameplaySimulationRoot is not null && GodotObject.IsInstanceValid(_gameplaySimulationRoot))
            _gameplaySimulationRoot.QueueFree();
        _gameplaySimulationRoot = new Node3D { Name = "GameplaySimulationAdapters" };
        AddChild(_gameplaySimulationRoot);
        if (!profile.Enabled) return;

        if (profile.Flight.Enabled)
        {
            var flight = new CharacterBody3D { Name = "FlightCharacterController" };
            flight.SetMeta("profile_id", profile.Flight.Id.ToString("D"));
            flight.SetMeta("maximum_altitude", profile.Flight.MaximumAltitude);
            flight.SetMeta("banking_degrees", profile.Flight.BankingDegrees);
            flight.SetMeta("lift", profile.Flight.Lift);
            flight.SetMeta("volumetric_navigation", profile.Flight.VolumetricNavigation);
            flight.AddChild(new CollisionShape3D
            {
                Name = "FlightCollision",
                Shape = new CapsuleShape3D { Radius = .35f, Height = 1.2f },
            });
            _gameplaySimulationRoot.AddChild(flight);
        }

        if (profile.Projectiles.Enabled)
        {
            var projectile = new Area3D { Name = "ProjectileAdapter" };
            projectile.SetMeta("profile_id", profile.Projectiles.Id.ToString("D"));
            projectile.SetMeta("mode", profile.Projectiles.Mode.ToString());
            projectile.SetMeta("prediction", profile.Projectiles.Prediction);
            projectile.SetMeta("server_authoritative", profile.Projectiles.ServerAuthoritative);
            projectile.SetMeta("vfx_asset", profile.Projectiles.VfxAssetId);
            projectile.AddChild(new CollisionShape3D
            {
                Shape = new SphereShape3D { Radius = profile.Projectiles.CollisionRadius },
            });
            projectile.AddChild(new MeshInstance3D
            {
                Name = "ProjectileVfx",
                Mesh = new SphereMesh { Radius = .12f, Height = .24f },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color("ff875f"),
                    EmissionEnabled = true,
                    Emission = new Color("ff6b42"),
                },
            });
            _gameplaySimulationRoot.AddChild(projectile);
        }

        var physicsRoot = new Node3D { Name = "PhysicsSceneAdapter" };
        physicsRoot.SetMeta("fixed_step_seconds", profile.Physics.FixedStepSeconds);
        _gameplaySimulationRoot.AddChild(physicsRoot);
        var bodies = new Dictionary<Guid, Node3D>();
        foreach (var body in profile.Physics.Bodies)
        {
            Node3D node = body.IsTrigger ? new Area3D() : body.Mode switch
            {
                PhysicsBodyMode.Static => new StaticBody3D(),
                PhysicsBodyMode.Kinematic => new AnimatableBody3D(),
                _ => new RigidBody3D { Mass = body.Mass, LinearDamp = body.LinearDamping },
            };
            node.Name = $"PhysicsBody_{body.Id:N}";
            node.SetMeta("body_id", body.Id.ToString("D"));
            node.SetMeta("mode", body.Mode.ToString());
            node.SetMeta("network_policy", body.NetworkPolicy);
            node.SetMeta("character_interactions", body.InteractsWithCharacters);
            node.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(.8f, .8f, .8f) },
            });
            physicsRoot.AddChild(node);
            bodies[body.Id] = node;
        }
        foreach (var jointProfile in profile.Physics.Joints)
        {
            Joint3D joint = jointProfile.Mode switch
            {
                PhysicsJointMode.Hinge => new HingeJoint3D(),
                PhysicsJointMode.Slider => new SliderJoint3D(),
                PhysicsJointMode.Spring => new Generic6DofJoint3D(),
                _ => new PinJoint3D(),
            };
            joint.Name = $"PhysicsJoint_{jointProfile.Id:N}";
            physicsRoot.AddChild(joint);
            joint.NodeA = joint.GetPathTo(bodies[jointProfile.BodyAId]);
            joint.NodeB = joint.GetPathTo(bodies[jointProfile.BodyBId]);
            joint.ExcludeNodesFromCollision = jointProfile.CollisionExcluded;
            joint.SetMeta("break_force", jointProfile.BreakForce);
        }

        if (profile.Targeting.Enabled)
        {
            var preview = GameplaySimulation.PreviewTargeting(profile.Targeting, Vector3Value.Zero,
                new(0, 0, -1), []);
            var immediate = new ImmediateMesh();
            immediate.SurfaceBegin(Mesh.PrimitiveType.LineStrip);
            foreach (var point in preview.Geometry)
                immediate.SurfaceAddVertex(new Vector3(point.X, point.Y + .03f, point.Z));
            immediate.SurfaceEnd();
            var target = new MeshInstance3D { Name = "TargetingPreview", Mesh = immediate };
            target.SetMeta("profile_id", profile.Targeting.Id.ToString("D"));
            target.SetMeta("mode", profile.Targeting.Mode.ToString());
            target.SetMeta("input_action", profile.Targeting.InputAction);
            _gameplaySimulationRoot.AddChild(target);
        }

        var world = new Node { Name = "WorldTimeAdapter" };
        world.SetMeta("profile_id", profile.WorldTime.Id.ToString("D"));
        world.SetMeta("calendar_days_per_month", profile.WorldTime.Calendar.DaysPerMonth);
        world.SetMeta("pause_domains", string.Join(",", profile.WorldTime.PauseDomains));
        world.SetMeta("schedule_count", profile.WorldTime.Schedules.Count);
        _gameplaySimulationRoot.AddChild(world);
    }

    private void BuildGameplaySessionAdapters(GameplaySessionProfile profile)
    {
        foreach (var diagnostic in GameplaySessionProfileValidator.ValidateRuntime(profile,
                     profile.RequiredAtRuntime))
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_GAMEPLAY_SESSION_DIAGNOSTIC code={diagnostic.Code} " +
                        $"path={diagnostic.Path}");
        if (_gameplaySessionRoot is not null && GodotObject.IsInstanceValid(_gameplaySessionRoot))
            _gameplaySessionRoot.QueueFree();
        _gameplaySessionRoot = new Node { Name = "GameplaySessionAdapters" };
        AddChild(_gameplaySessionRoot);
        if (!profile.Enabled) return;

        var persistence = new Node { Name = "PersistenceAdapter" };
        persistence.SetMeta("profile_id", profile.Persistence.Id.ToString("D"));
        persistence.SetMeta("scope", profile.Persistence.Scope.ToString());
        persistence.SetMeta("authority", profile.Persistence.Authority.ToString());
        persistence.SetMeta("conflict", profile.Persistence.ConflictPolicy.ToString());
        persistence.SetMeta("cloud_optional", profile.Persistence.CloudAdapterEnabled);
        persistence.SetMeta("privacy", profile.Persistence.PrivacyClassification);
        persistence.SetMeta("encrypted", profile.Persistence.EncryptAtRest);
        _gameplaySessionRoot.AddChild(persistence);

        var checkpoints = new Node3D { Name = "CheckpointAdapters" };
        foreach (var checkpoint in profile.Checkpoints.Where(item => item.Enabled))
        {
            var marker = new Area3D { Name = $"Checkpoint_{checkpoint.Id:N}" };
            marker.SetMeta("checkpoint_id", checkpoint.Id.ToString("D"));
            marker.SetMeta("waypoint", checkpoint.WaypointId);
            marker.SetMeta("party_policy", checkpoint.PartyPolicy.ToString());
            marker.SetMeta("corpse_policy", checkpoint.CorpsePolicy.ToString());
            marker.SetMeta("before_event", checkpoint.BeforeRespawnEvent);
            marker.SetMeta("after_event", checkpoint.AfterRespawnEvent);
            marker.AddChild(new CollisionShape3D
            {
                Shape = new SphereShape3D { Radius = (float)checkpoint.SafeRadius },
            });
            marker.AddChild(new OmniLight3D
            {
                LightColor = new Color("7bb7ff"),
                OmniRange = (float)Math.Max(1, checkpoint.SafeRadius),
                LightEnergy = .45f,
            });
            checkpoints.AddChild(marker);
        }
        _gameplaySessionRoot.AddChild(checkpoints);

        var shellLayer = new CanvasLayer { Name = "RuntimeShellAdapter", Layer = 7 };
        shellLayer.SetMeta("kit", profile.Shell.KitId);
        shellLayer.SetMeta("skin", profile.Shell.SkinId);
        shellLayer.SetMeta("scale", profile.Shell.Scale);
        shellLayer.SetMeta("input_agnostic", profile.Shell.InputAgnostic);
        var shellRoot = new Control { Name = "ShellPanels", MouseFilter = Control.MouseFilterEnum.Ignore };
        shellRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var panel in new[]
                 {
                     ("MapPanel", profile.Shell.MapVisible),
                     ("JournalPanel", profile.Shell.JournalVisible),
                     ("HudPanel", profile.Shell.HudVisible),
                 })
        {
            var control = new Control { Name = panel.Item1, Visible = panel.Item2 };
            control.SetMeta("provided_by_kit", profile.Shell.KitId);
            shellRoot.AddChild(control);
        }
        shellLayer.AddChild(shellRoot);
        _gameplaySessionRoot.AddChild(shellLayer);

        var controls = new Node { Name = "ControlContextStackAdapter" };
        foreach (var context in profile.ControlLocks.Contexts.OrderBy(item => item.Priority))
        {
            var node = new Node { Name = $"Control_{context.OwnerId}" };
            node.SetMeta("context_id", context.Id.ToString("D"));
            node.SetMeta("cursor_visible", context.CursorVisible);
            node.SetMeta("camera_mode", context.CameraMode);
            node.SetMeta("allowed_actions", string.Join(",", context.AllowedInputActions.Order(StringComparer.Ordinal)));
            node.SetMeta("restore_on_release", context.RestoreOnRelease);
            node.SetMeta("priority", context.Priority);
            controls.AddChild(node);
        }
        _gameplaySessionRoot.AddChild(controls);

        var replay = new Node { Name = "ReplayHeadlessAdapter" };
        replay.SetMeta("profile_id", profile.Replay.Id.ToString("D"));
        replay.SetMeta("maximum_frames", profile.Replay.MaximumFrames);
        replay.SetMeta("divergence_report", profile.Replay.EmitDivergenceReport);
        replay.SetMeta("minimal_package", profile.Replay.EmitMinimalPackage);
        replay.SetMeta("privacy", profile.Replay.IncludePersonalData ? "included" : "redacted");
        _gameplaySessionRoot.AddChild(replay);
    }

    private void BuildDialogueAuthoringAdapters(IReadOnlyList<DialogueDefinition> dialogues)
    {
        ArgumentNullException.ThrowIfNull(dialogues);
        if (_dialogueAuthoringRoot is not null && GodotObject.IsInstanceValid(_dialogueAuthoringRoot))
            _dialogueAuthoringRoot.QueueFree();
        _dialogueAuthoringRoot = new Node { Name = "DialogueAuthoringAdapters" };
        AddChild(_dialogueAuthoringRoot);
        foreach (var dialogue in dialogues)
        {
            foreach (var diagnostic in DialogueAuthoringProfileValidator.Validate(dialogue))
                GD.PrintErr($"OPENRPG_FORGE_RUNTIME_DIALOGUE_AUTHORING_DIAGNOSTIC code={diagnostic.Code} " +
                            $"path={diagnostic.Path}");
            var profile = dialogue.Authoring;
            if (profile is null || !profile.Enabled) continue;
            var dialogueNode = new Node { Name = $"Dialogue_{dialogue.Id.Value:N}" };
            dialogueNode.SetMeta("dialogue_id", dialogue.Id.ToString());
            dialogueNode.SetMeta("profile_id", profile.Id.ToString("D"));
            dialogueNode.SetMeta("schema_version", profile.SchemaVersion);
            dialogueNode.SetMeta("provenance", profile.Provenance);
            dialogueNode.SetMeta("required_at_runtime", profile.RequiredAtRuntime);
            var participantRoot = new Node { Name = "ParticipantBindings" };
            foreach (var participant in profile.Participants)
            {
                var binding = new Node { Name = $"Participant_{participant.Id:N}" };
                binding.SetMeta("role", participant.Role);
                binding.SetMeta("kind", participant.Kind.ToString());
                binding.SetMeta("value", participant.Value);
                binding.SetMeta("fallback_role", participant.FallbackRole ?? string.Empty);
                participantRoot.AddChild(binding);
            }
            dialogueNode.AddChild(participantRoot);
            var nodeRoot = new Node { Name = "NodeAuthoring" };
            foreach (var authoring in profile.Nodes)
            {
                var adapter = new Node { Name = $"Node_{authoring.NodeId.Value:N}" };
                adapter.SetMeta("authoring_id", authoring.Id.ToString("D"));
                adapter.SetMeta("speaker_role", authoring.SpeakerRole);
                adapter.SetMeta("evaluation_mode", authoring.EvaluationMode.ToString());
                adapter.SetMeta("token_count", authoring.Tokens.Count);
                adapter.SetMeta("wizard_count", authoring.WizardEntries.Count);
                adapter.SetMeta("editorial_status", authoring.Notes.Status.ToString());
                adapter.SetMeta("editorial_intent", authoring.Notes.Intent);
                nodeRoot.AddChild(adapter);
            }
            dialogueNode.AddChild(nodeRoot);
            _dialogueAuthoringRoot.AddChild(dialogueNode);
        }
    }

    private void BuildDialogueExperienceAdapters(IReadOnlyList<DialogueDefinition> dialogues)
    {
        ArgumentNullException.ThrowIfNull(dialogues);
        if (_dialogueExperienceRoot is not null && GodotObject.IsInstanceValid(_dialogueExperienceRoot))
            _dialogueExperienceRoot.QueueFree();
        _dialogueExperienceRoot = new Node { Name = "DialogueExperienceAdapters" };
        AddChild(_dialogueExperienceRoot);
        foreach (var dialogue in dialogues)
        {
            foreach (var diagnostic in DialogueExperienceProfileValidator.Validate(dialogue))
                GD.PrintErr($"OPENRPG_FORGE_RUNTIME_DIALOGUE_EXPERIENCE_DIAGNOSTIC code={diagnostic.Code} " +
                            $"path={diagnostic.Path}");
            var profile = dialogue.Experience;
            if (profile is null || !profile.Enabled) continue;
            var dialogueNode = new Node { Name = $"Dialogue_{dialogue.Id.Value:N}" };
            dialogueNode.SetMeta("dialogue_id", dialogue.Id.ToString());
            dialogueNode.SetMeta("profile_id", profile.Id.ToString("D"));
            dialogueNode.SetMeta("schema_version", profile.SchemaVersion);
            dialogueNode.SetMeta("provenance", profile.Provenance);
            dialogueNode.SetMeta("required_at_runtime", profile.RequiredAtRuntime);
            var lifecycle = new Node { Name = "LifecycleAdapter" };
            lifecycle.SetMeta("completed_actions", profile.Lifecycle.CompletedActions.Count);
            lifecycle.SetMeta("aborted_actions", profile.Lifecycle.AbortedActions.Count);
            lifecycle.SetMeta("rollback_on_abort", profile.Lifecycle.RollbackOnAbort);
            lifecycle.SetMeta("create_resume_state", profile.Lifecycle.CreateResumeState);
            dialogueNode.AddChild(lifecycle);
            var searchReview = new Node { Name = "SearchReviewAdapter" };
            searchReview.SetMeta("search_pattern", profile.Search.Pattern);
            searchReview.SetMeta("safe_regex", profile.Search.UseRegex);
            searchReview.SetMeta("review_mode", profile.Review.ReviewMode);
            searchReview.SetMeta("badges", string.Join(",", profile.Review.Badges.Order()));
            dialogueNode.AddChild(searchReview);
            var memory = new Node { Name = "MemorySlots" };
            foreach (var slot in profile.MemorySlots)
            {
                var adapter = new Node { Name = $"Memory_{slot.Id:N}" };
                adapter.SetMeta("scope", slot.Scope.ToString());
                adapter.SetMeta("scope_id", slot.ScopeId);
                adapter.SetMeta("key", slot.Name);
                adapter.SetMeta("type", slot.Type.ToString());
                adapter.SetMeta("duration", slot.Duration.ToString());
                memory.AddChild(adapter);
            }
            dialogueNode.AddChild(memory);
            var checks = new Node { Name = "SkillChecks" };
            foreach (var check in profile.SkillChecks)
            {
                var adapter = new Node { Name = $"Check_{check.Id:N}" };
                adapter.SetMeta("node_id", check.NodeId.ToString());
                adapter.SetMeta("skill_id", check.SkillId);
                adapter.SetMeta("difficulty", check.Difficulty);
                adapter.SetMeta("opponent_id", check.OpponentId ?? string.Empty);
                adapter.SetMeta("advantage", check.Advantage);
                adapter.SetMeta("visibility", check.Visibility.ToString());
                adapter.SetMeta("success_node_id", check.SuccessNodeId.ToString());
                adapter.SetMeta("failure_node_id", check.FailureNodeId.ToString());
                checks.AddChild(adapter);
            }
            dialogueNode.AddChild(checks);
            _dialogueExperienceRoot.AddChild(dialogueNode);
        }
    }

    private void BuildDialogueProductionAdapters(IReadOnlyList<DialogueDefinition> dialogues)
    {
        ArgumentNullException.ThrowIfNull(dialogues);
        if (_dialogueProductionRoot is not null && GodotObject.IsInstanceValid(_dialogueProductionRoot))
            _dialogueProductionRoot.QueueFree();
        _dialogueProductionRoot = new Node { Name = "DialogueProductionAdapters" };
        AddChild(_dialogueProductionRoot);
        foreach (var dialogue in dialogues)
        {
            foreach (var diagnostic in DialogueProductionProfileValidator.Validate(dialogue))
                GD.PrintErr($"OPENRPG_FORGE_RUNTIME_DIALOGUE_PRODUCTION_DIAGNOSTIC code={diagnostic.Code} " +
                            $"path={diagnostic.Path}");
            var profile = dialogue.Production;
            if (profile is null || !profile.Enabled) continue;
            var dialogueNode = new Node { Name = $"Dialogue_{dialogue.Id.Value:N}" };
            dialogueNode.SetMeta("dialogue_id", dialogue.Id.ToString());
            dialogueNode.SetMeta("profile_id", profile.Id.ToString("D"));
            dialogueNode.SetMeta("schema_version", profile.SchemaVersion);
            dialogueNode.SetMeta("provenance", profile.Provenance);
            dialogueNode.SetMeta("required_at_runtime", profile.RequiredAtRuntime);
            var subgraphs = new Node { Name = "SubgraphContracts" };
            foreach (var contract in profile.Subgraphs)
            {
                var adapter = new Node { Name = $"Subgraph_{contract.Id:N}" };
                adapter.SetMeta("key", contract.Key);
                adapter.SetMeta("version", contract.Version);
                adapter.SetMeta("input_count", contract.Parameters.Count(item =>
                    item.Direction == DialogueSubgraphParameterDirection.Input));
                adapter.SetMeta("output_count", contract.Parameters.Count(item =>
                    item.Direction == DialogueSubgraphParameterDirection.Output));
                adapter.SetMeta("variable_contract", string.Join(',', contract.RequiredVariables));
                adapter.SetMeta("isolated_tests", contract.Tests.Count);
                subgraphs.AddChild(adapter);
            }
            dialogueNode.AddChild(subgraphs);
            var consequences = new Node { Name = "TypedConsequences" };
            foreach (var consequence in profile.Consequences)
            {
                var adapter = new Node { Name = $"Consequence_{consequence.Id:N}" };
                adapter.SetMeta("node_id", consequence.NodeId.ToString());
                adapter.SetMeta("kind", consequence.Kind.ToString());
                adapter.SetMeta("target_id", consequence.TargetId);
                adapter.SetMeta("operation", consequence.Operation);
                adapter.SetMeta("value", consequence.Value);
                consequences.AddChild(adapter);
            }
            dialogueNode.AddChild(consequences);
            var coverageReport = DialogueProductionRuntimeService.MeasureCoverage(dialogue);
            var coverage = new Node { Name = "Coverage" };
            coverage.SetMeta("path_count", coverageReport.Paths.Count);
            coverage.SetMeta("covered_nodes", coverageReport.CoveredNodes.Count);
            coverage.SetMeta("uncovered_nodes", coverageReport.UncoveredNodes.Count);
            coverage.SetMeta("impossible_paths", coverageReport.ImpossiblePaths.Count);
            coverage.SetMeta("ratio", coverageReport.Ratio);
            coverage.SetMeta("evidence_count", profile.Coverage.Evidence.Count);
            dialogueNode.AddChild(coverage);
            var collaboration = new Node { Name = "Collaboration" };
            collaboration.SetMeta("revisions", profile.Collaboration.Revisions.Count);
            collaboration.SetMeta("locks", profile.Collaboration.Locks.Count);
            collaboration.SetMeta("comments", profile.Collaboration.Comments.Count);
            collaboration.SetMeta("conflicts", profile.Collaboration.Conflicts.Count);
            dialogueNode.AddChild(collaboration);
            var ai = new Node { Name = "ControlledAiPlans" };
            foreach (var plan in profile.AiPlans)
            {
                var adapter = new Node { Name = $"Plan_{plan.PlanId:N}" };
                adapter.SetMeta("transaction_id", plan.TransactionId.ToString("D"));
                adapter.SetMeta("authorization", plan.Authorization.ToString());
                adapter.SetMeta("preview_only", plan.PreviewOnly);
                adapter.SetMeta("applied", plan.Applied);
                adapter.SetMeta("citation_count", plan.ContextCitations.Count);
                adapter.SetMeta("lint_count", plan.Lint.Count);
                ai.AddChild(adapter);
            }
            dialogueNode.AddChild(ai);
            _dialogueProductionRoot.AddChild(dialogueNode);
        }
    }

    private void BuildQuestAuthoringAdapters(IReadOnlyList<QuestDefinition> quests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        if (_questAuthoringRoot is not null && GodotObject.IsInstanceValid(_questAuthoringRoot))
            _questAuthoringRoot.QueueFree();
        _questAuthoringRoot = new Node { Name = "QuestAuthoringAdapters" };
        AddChild(_questAuthoringRoot);
        foreach (var quest in quests)
        {
            foreach (var diagnostic in QuestAuthoringProfileValidator.Validate(quest))
                GD.PrintErr($"OPENRPG_FORGE_RUNTIME_QUEST_AUTHORING_DIAGNOSTIC code={diagnostic.Code} " +
                            $"path={diagnostic.Path}");
            var profile = quest.Authoring;
            if (profile is null || !profile.Enabled) continue;
            var questNode = new Node { Name = $"Quest_{quest.Id.Value:N}" };
            questNode.SetMeta("quest_id", quest.Id.ToString());
            questNode.SetMeta("profile_id", profile.Id.ToString("D"));
            questNode.SetMeta("schema_version", profile.SchemaVersion);
            questNode.SetMeta("provenance", profile.Provenance);
            questNode.SetMeta("required_at_runtime", profile.RequiredAtRuntime);
            var objectives = new Node { Name = "ExplicitObjectives" };
            foreach (var objective in profile.Objectives.OrderBy(item => item.DisplayOrder))
            {
                var adapter = new Node { Name = $"Objective_{objective.Id:N}" };
                adapter.SetMeta("kind", objective.Kind.ToString());
                adapter.SetMeta("label_key", objective.Label.Key);
                adapter.SetMeta("target", objective.Target);
                adapter.SetMeta("target_ids", string.Join(',', objective.TargetIds));
                adapter.SetMeta("destination", objective.DestinationId ?? string.Empty);
                adapter.SetMeta("duration_seconds", objective.DurationSeconds);
                adapter.SetMeta("choice_ids", string.Join(',', objective.ChoiceIds));
                adapter.SetMeta("custom_rule", objective.CustomRuleId ?? string.Empty);
                objectives.AddChild(adapter);
            }
            questNode.AddChild(objectives);
            var rewards = new Node { Name = "AtomicRewards" };
            rewards.SetMeta("progression", profile.Rewards.Progression);
            rewards.SetMeta("items", profile.Rewards.Items.Count);
            rewards.SetMeta("currencies", profile.Rewards.Currencies.Count);
            rewards.SetMeta("reputation", profile.Rewards.Reputation.Count);
            rewards.SetMeta("unlocks", profile.Rewards.Unlocks.Count);
            rewards.SetMeta("choices", profile.Rewards.ChoiceIds.Count);
            questNode.AddChild(rewards);
            var notes = new Node { Name = "AuthorNotes" };
            notes.SetMeta("owner", profile.Notes.OwnerId);
            notes.SetMeta("status", profile.Notes.Status.ToString());
            notes.SetMeta("risks", profile.Notes.Risks.Count);
            notes.SetMeta("todos", profile.Notes.Todos.Count);
            notes.SetMeta("sources", profile.Notes.Sources.Count);
            notes.SetMeta("reviews", profile.Notes.ReviewHistory.Count);
            questNode.AddChild(notes);
            var cast = new Node { Name = "ScenarioCast" };
            foreach (var participant in profile.Participants.OrderBy(item => item.DisplayOrder))
            {
                var adapter = new Node { Name = $"Participant_{participant.Id:N}" };
                adapter.SetMeta("role", participant.Role);
                adapter.SetMeta("binding_kind", participant.BindingKind.ToString());
                adapter.SetMeta("binding_value", participant.BindingValue);
                adapter.SetMeta("presence_conditions", string.Join(',', participant.PresenceConditions));
                adapter.SetMeta("required", participant.Required);
                cast.AddChild(adapter);
            }
            questNode.AddChild(cast);
            var wizard = new Node { Name = "TransparentPlotWizard" };
            wizard.SetMeta("zones", profile.Wizard.ZoneIds.Count);
            wizard.SetMeta("participants", profile.Wizard.ParticipantIds.Count);
            wizard.SetMeta("props", profile.Wizard.PropIds.Count);
            wizard.SetMeta("dialogues", profile.Wizard.DialogueIds.Count);
            wizard.SetMeta("objectives", profile.Wizard.ObjectiveIds.Count);
            wizard.SetMeta("transitions", profile.Wizard.TransitionIds.Count);
            wizard.SetMeta("tests", profile.Wizard.GeneratedTestScenarios.Count);
            wizard.SetMeta("opaque_code", profile.Wizard.GeneratesOpaqueCode);
            questNode.AddChild(wizard);
            _questAuthoringRoot.AddChild(questNode);
        }
    }

    private void BuildQuestExperienceAdapters(IReadOnlyList<QuestDefinition> quests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        if (_questExperienceRoot is not null && GodotObject.IsInstanceValid(_questExperienceRoot))
            _questExperienceRoot.QueueFree();
        _questExperienceRoot = new Node { Name = "QuestExperienceAdapters" };
        AddChild(_questExperienceRoot);
        foreach (var quest in quests)
        {
            foreach (var diagnostic in QuestExperienceProfileValidator.Validate(quest))
                GD.PrintErr($"OPENRPG_FORGE_RUNTIME_QUEST_EXPERIENCE_DIAGNOSTIC code={diagnostic.Code} " +
                            $"path={diagnostic.Path}");
            var profile = quest.Experience;
            if (profile is null || !profile.Enabled) continue;
            var questNode = new Node { Name = $"Quest_{quest.Id.Value:N}" };
            questNode.SetMeta("quest_id", quest.Id.ToString());
            questNode.SetMeta("profile_id", profile.Id.ToString("D"));
            questNode.SetMeta("schema_version", profile.SchemaVersion);
            questNode.SetMeta("provenance", profile.Provenance);
            questNode.SetMeta("required_at_runtime", profile.RequiredAtRuntime);
            var resources = new Node { Name = "TypedResources" };
            foreach (var resource in profile.Resources.OrderBy(item => item.DisplayOrder))
            {
                var adapter = new Node { Name = $"Resource_{resource.Id:N}" };
                adapter.SetMeta("kind", resource.Kind.ToString());
                adapter.SetMeta("reference_id", resource.ReferenceId);
                adapter.SetMeta("owner_id", resource.OwnerId);
                adapter.SetMeta("required", resource.Required);
                adapter.SetMeta("conditions", string.Join(',', resource.Conditions));
                resources.AddChild(adapter);
            }
            questNode.AddChild(resources);
            var steps = new Node { Name = "EditableStorySteps" };
            foreach (var step in profile.Steps.OrderBy(item => item.DisplayOrder))
            {
                var adapter = new Node { Name = $"Step_{step.Id:N}" };
                adapter.SetMeta("template_id", step.TemplateId);
                adapter.SetMeta("template_version", step.TemplateVersion);
                adapter.SetMeta("editable", step.Editable);
                adapter.SetMeta("objectives", step.ObjectiveIds.Count);
                adapter.SetMeta("transitions", step.TransitionIds.Count);
                adapter.SetMeta("provenance", step.Provenance);
                steps.AddChild(adapter);
            }
            questNode.AddChild(steps);
            var visibility = new Node { Name = "VisibilityPolicy" };
            visibility.SetMeta("initial", profile.Visibility.Initial.ToString());
            visibility.SetMeta("reveal_states", profile.Visibility.RevealConditions.Count);
            visibility.SetMeta("manual_tracking", profile.Visibility.AllowManualTracking);
            questNode.AddChild(visibility);
            var history = new Node { Name = "TimestampedHistory" };
            foreach (var historyEvent in profile.History.OrderBy(item => item.At).ThenBy(item => item.Id))
            {
                var adapter = new Node { Name = $"Event_{historyEvent.Id:N}" };
                adapter.SetMeta("from", historyEvent.FromState);
                adapter.SetMeta("to", historyEvent.ToState);
                adapter.SetMeta("cause", historyEvent.Cause);
                adapter.SetMeta("actor", historyEvent.ActorId);
                adapter.SetMeta("at", historyEvent.At.ToString("O"));
                adapter.SetMeta("player_summary_key", historyEvent.PlayerSummary.Key);
                history.AddChild(adapter);
            }
            questNode.AddChild(history);
            var markers = new Node { Name = "ObjectiveMapMarkers" };
            foreach (var marker in profile.Markers.OrderBy(item => item.DisplayOrder))
            {
                var adapter = new Node3D { Name = $"Marker_{marker.Id:N}" };
                adapter.SetMeta("objective_id", marker.ObjectiveId.ToString("D"));
                adapter.SetMeta("binding_kind", marker.BindingKind.ToString());
                adapter.SetMeta("binding_value", marker.BindingValue);
                adapter.SetMeta("visibility", marker.Visibility.ToString());
                adapter.SetMeta("precision_radius", marker.PrecisionRadius);
                adapter.SetMeta("tracked", marker.Tracked);
                adapter.SetMeta("label_key", marker.Label.Key);
                if (marker.FallbackPosition is { } fallback)
                    adapter.Position = new Vector3(fallback.X, fallback.Y, fallback.Z);
                markers.AddChild(adapter);
            }
            questNode.AddChild(markers);
            _questExperienceRoot.AddChild(questNode);
        }
    }

    private void BuildQuestQualityAdapters(IReadOnlyList<QuestDefinition> quests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        if (_questQualityRoot is not null && GodotObject.IsInstanceValid(_questQualityRoot))
            _questQualityRoot.QueueFree();
        _questQualityRoot = new Node { Name = "QuestQualityAdapters" };
        AddChild(_questQualityRoot);
        foreach (var quest in quests)
        {
            foreach (var diagnostic in QuestQualityProfileValidator.Validate(quest))
                GD.PrintErr($"OPENRPG_FORGE_RUNTIME_QUEST_QUALITY_DIAGNOSTIC code={diagnostic.Code} " +
                            $"path={diagnostic.Path}");
            var profile = quest.Quality;
            if (profile is null || !profile.Enabled) continue;
            var questNode = new Node { Name = $"Quest_{quest.Id.Value:N}" };
            questNode.SetMeta("quest_id", quest.Id.ToString());
            questNode.SetMeta("profile_id", profile.Id.ToString("D"));
            questNode.SetMeta("schema_version", profile.SchemaVersion);
            questNode.SetMeta("provenance", profile.Provenance);
            questNode.SetMeta("required_at_runtime", profile.RequiredAtRuntime);
            var scenarios = new Node { Name = "GeneratedTestScenarios" };
            foreach (var scenario in profile.TestScenarios.OrderBy(item => item.DisplayOrder))
            {
                var scenarioNode = new Node { Name = $"Scenario_{scenario.Id:N}" };
                scenarioNode.SetMeta("kind", scenario.Kind.ToString());
                scenarioNode.SetMeta("seed", scenario.Seed);
                scenarioNode.SetMeta("label_key", scenario.Label.Key);
                var fixtures = new Node { Name = "TypedFixtures" };
                foreach (var fixture in scenario.Fixtures.OrderBy(item => item.DisplayOrder))
                {
                    var fixtureNode = new Node { Name = $"Fixture_{fixture.Id:N}" };
                    fixtureNode.SetMeta("kind", fixture.Kind.ToString());
                    fixtureNode.SetMeta("reference_id", fixture.ReferenceId);
                    fixtureNode.SetMeta("required", fixture.Required);
                    fixtures.AddChild(fixtureNode);
                }
                scenarioNode.AddChild(fixtures);
                var steps = new Node { Name = "TypedStepNodes" };
                foreach (var step in scenario.Steps.OrderBy(item => item.DisplayOrder))
                {
                    var stepNode = new Node { Name = $"Step_{step.Id:N}" };
                    stepNode.SetMeta("action", step.Action.ToString());
                    stepNode.SetMeta("parameters", string.Join(';', step.Parameters.Select(item => $"{item.Key}={item.Value}")));
                    steps.AddChild(stepNode);
                }
                scenarioNode.AddChild(steps);
                var edges = new Node { Name = "DeterministicEdges" };
                foreach (var edge in scenario.Edges.OrderBy(item => item.DisplayOrder))
                {
                    var edgeNode = new Node { Name = $"Edge_{edge.Id:N}" };
                    edgeNode.SetMeta("from", edge.FromStepId.ToString("D"));
                    edgeNode.SetMeta("to", edge.ToStepId.ToString("D"));
                    edges.AddChild(edgeNode);
                }
                scenarioNode.AddChild(edges);
                var assertions = new Node { Name = "TypedAssertions" };
                foreach (var assertion in scenario.Assertions.OrderBy(item => item.DisplayOrder))
                {
                    var assertionNode = new Node { Name = $"Assertion_{assertion.Id:N}" };
                    assertionNode.SetMeta("kind", assertion.Kind.ToString());
                    assertionNode.SetMeta("path", assertion.Path);
                    assertionNode.SetMeta("expected", assertion.Expected);
                    assertions.AddChild(assertionNode);
                }
                scenarioNode.AddChild(assertions);
                scenarios.AddChild(scenarioNode);
            }
            questNode.AddChild(scenarios);
            var aiPlan = new Node { Name = "GovernedAiPlan" };
            aiPlan.SetMeta("plan_id", profile.AiPlan.Id.ToString("D"));
            aiPlan.SetMeta("transaction_id", profile.AiPlan.TransactionId.ToString("D"));
            aiPlan.SetMeta("synopsis_key", profile.AiPlan.Synopsis.Key);
            aiPlan.SetMeta("states", profile.AiPlan.ProposedStateIds.Count);
            aiPlan.SetMeta("participants", profile.AiPlan.ParticipantIds.Count);
            aiPlan.SetMeta("citations", profile.AiPlan.ContextCitations.Count);
            aiPlan.SetMeta("style_rules", profile.AiPlan.StyleRules.Count);
            aiPlan.SetMeta("lint", profile.AiPlan.Lint.Count);
            aiPlan.SetMeta("semantic_diff", profile.AiPlan.SemanticDiff);
            aiPlan.SetMeta("authorization", profile.AiPlan.Authorization.ToString());
            aiPlan.SetMeta("approved_by", profile.AiPlan.ApprovedBy);
            aiPlan.SetMeta("applied", profile.AiPlan.Applied);
            var dependencies = new Node { Name = "TypedDependencies" };
            foreach (var dependency in profile.AiPlan.Dependencies.OrderBy(item => item.DisplayOrder))
            {
                var dependencyNode = new Node { Name = $"Dependency_{dependency.Id:N}" };
                dependencyNode.SetMeta("kind", dependency.Kind.ToString());
                dependencyNode.SetMeta("reference_id", dependency.ReferenceId);
                dependencies.AddChild(dependencyNode);
            }
            aiPlan.AddChild(dependencies);
            questNode.AddChild(aiPlan);
            _questQualityRoot.AddChild(questNode);
        }
    }

    private void ApplyWaypointPatrolStep(WaypointPatrolStep step)
    {
        if (_waypointPatrolActor is null || !GodotObject.IsInstanceValid(_waypointPatrolActor))
        {
            _waypointPatrolActor = new Node3D { Name = "WaypointPatrolActor" };
            var mesh = new MeshInstance3D
            {
                Mesh = new CapsuleMesh { Radius = .22f, Height = .7f },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color("83d6ae"),
                    EmissionEnabled = true,
                    Emission = new Color("2f7d62"),
                },
            };
            _waypointPatrolActor.AddChild(mesh);
            AddChild(_waypointPatrolActor);
        }
        _waypointPatrolActor.Position = new Vector3(step.Position.X, step.Position.Y + .35f,
            step.Position.Z);
        _waypointPatrolActor.RotationDegrees = new Vector3(0, (float)step.FacingDegrees, 0);
        _waypointPatrolActor.SetMeta("patrol_speed", step.Speed);
        _waypointPatrolActor.SetMeta("patrol_wait", step.WaitSeconds);
        GD.Print($"OPENRPG_FORGE_RUNTIME_WAYPOINT_PATROL route={step.RouteId:D} " +
                 $"from={step.FromPointId:D} to={step.ToPointId:D} speed={step.Speed:0.##} " +
                 $"wait={step.WaitSeconds:0.##}");
    }

    private void ConfigureEntityWaypointPatrols(WaypointAuthoringProfile profile)
    {
        _entityWaypointPatrols.Clear();
        if (_activeArea is not { } area) return;
        foreach (var entity in area.Entities)
        {
            var routeReference = GetOverride(entity, "runtime.patrolRoute");
            if (!Guid.TryParse(routeReference, out var routeId)) continue;
            var route = profile.PatrolRoutes.FirstOrDefault(candidate => candidate.Id == routeId);
            if (route is null || route.Points.Count < 2 ||
                !_entityNodes.TryGetValue(entity.Id.Value, out var actor) ||
                !GodotObject.IsInstanceValid(actor))
            {
                GD.PrintErr($"OPENRPG_FORGE_RUNTIME_ENTITY_PATROL_INVALID entity={entity.Id} route={routeReference}");
                continue;
            }

            var state = new EntityWaypointPatrolState(
                route,
                actor.Position,
                targetIndex: 1,
                waitRemainingSeconds: Math.Max(0, route.Points[0].WaitSeconds));
            _entityWaypointPatrols.Add(actor, state);
            GD.Print($"OPENRPG_FORGE_RUNTIME_ENTITY_PATROL_READY entity={entity.Id} route={route.Id:D} " +
                     $"points={route.Points.Count}");
        }
    }

    private void AdvanceEntityWaypointPatrols(double delta)
    {
        foreach (var (actor, state) in _entityWaypointPatrols.ToArray())
        {
            if (!GodotObject.IsInstanceValid(actor))
            {
                _entityWaypointPatrols.Remove(actor);
                continue;
            }
            if (ReferenceEquals(actor, _activeDialogueParticipant) &&
                (_dialogueOverlay?.IsOpen == true || _activeDialogueTraversal is not null))
            {
                actor.Position = state.Position;
                PlayEntityPatrolAnimation(actor, moving: false);
                continue;
            }

            var remainingSeconds = Math.Max(0, delta);
            if (state.WaitRemainingSeconds > 0)
            {
                var consumed = Math.Min(state.WaitRemainingSeconds, remainingSeconds);
                state.WaitRemainingSeconds -= consumed;
                remainingSeconds -= consumed;
                if (state.WaitRemainingSeconds > 0 || remainingSeconds <= 0)
                {
                    PlayEntityPatrolAnimation(actor, moving: false);
                    continue;
                }
            }

            var target = state.Route.Points[state.TargetIndex];
            var targetPosition = new Vector3(target.Position.X, target.Position.Y, target.Position.Z);
            var displacement = targetPosition - state.Position;
            var distance = displacement.Length();
            if (distance <= .02f)
            {
                CompleteEntityPatrolPoint(actor, state, target, targetPosition);
                continue;
            }

            var direction = displacement / distance;
            var distanceStep = Math.Min(distance, Math.Max(.1, target.Speed) * remainingSeconds);
            state.Position += direction * (float)distanceStep;
            actor.Position = state.Position;
            var rotation = actor.Rotation;
            rotation.Y = Mathf.Atan2(direction.X, direction.Z);
            actor.Rotation = rotation;
            if (distanceStep + .02 >= distance)
                CompleteEntityPatrolPoint(actor, state, target, targetPosition);
            else
                PlayEntityPatrolAnimation(actor, moving: true);
        }
    }

    private void CompleteEntityPatrolPoint(
        Node3D actor,
        EntityWaypointPatrolState state,
        PatrolPoint point,
        Vector3 position)
    {
        state.Position = position;
        actor.Position = position;
        var rotation = actor.Rotation;
        rotation.Y = Mathf.DegToRad((float)point.FacingDegrees);
        actor.Rotation = rotation;
        state.WaitRemainingSeconds = Math.Max(0, point.WaitSeconds);
        state.TargetIndex = (state.TargetIndex + 1) % state.Route.Points.Count;
        PlayEntityPatrolAnimation(actor, moving: false);
    }

    private void PlayEntityPatrolAnimation(Node3D actor, bool moving)
    {
        if (!_characterAnimations.TryGetValue(actor, out var animation)) return;
        var clip = moving ? animation.RunClip : animation.IdleClip;
        if (!string.Equals(animation.CurrentClip, clip, StringComparison.Ordinal))
            GodotAssetAnimationController.Play(animation, clip, .12);
    }

    private sealed class EntityWaypointPatrolState(
        PatrolRoute route,
        Vector3 position,
        int targetIndex,
        double waitRemainingSeconds)
    {
        public PatrolRoute Route { get; } = route;
        public Vector3 Position { get; set; } = position;
        public int TargetIndex { get; set; } = targetIndex;
        public double WaitRemainingSeconds { get; set; } = waitRemainingSeconds;
    }

    private void BuildWaypointLocatorsAndRouteDebug(WaypointAuthoringProfile profile)
    {
        if (_waypointLocatorRoot is not null && GodotObject.IsInstanceValid(_waypointLocatorRoot))
            _waypointLocatorRoot.QueueFree();
        if (_waypointRouteDebugRoot is not null && GodotObject.IsInstanceValid(_waypointRouteDebugRoot))
            _waypointRouteDebugRoot.QueueFree();
        _waypointLocatorRoot = new Node3D { Name = "WaypointLocators" };
        foreach (var locator in profile.Locators)
        {
            var node = new Node3D
            {
                Name = $"Locator_{locator.Purpose}_{locator.Name}",
                Position = new Vector3(locator.Position.X, locator.Position.Y, locator.Position.Z),
            };
            node.SetMeta("locator_id", locator.Id.ToString("D"));
            node.SetMeta("locator_purpose", locator.Purpose.ToString());
            node.SetMeta("locator_tags", string.Join(",", locator.Tags.Order(StringComparer.Ordinal)));
            if (!string.IsNullOrWhiteSpace(locator.AssetId)) node.SetMeta("asset_id", locator.AssetId);
            if (locator.Purpose == LocatorPurpose.Audio)
                node.AddChild(new AudioStreamPlayer3D { Name = "AudioAnchor" });
            else if (locator.Purpose == LocatorPurpose.Vfx)
                node.AddChild(new OmniLight3D
                {
                    Name = "VfxAnchor",
                    LightColor = new Color("ff9858"),
                    OmniRange = 2.5f,
                    LightEnergy = .8f,
                });
            else
                node.AddChild(new MeshInstance3D
                {
                    Name = "LocatorMarker",
                    Mesh = new SphereMesh { Radius = .16f, Height = .32f },
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = locator.Purpose == LocatorPurpose.Objective
                            ? new Color("ffd166") : new Color("83d6ae"),
                        EmissionEnabled = true,
                        Emission = locator.Purpose == LocatorPurpose.Objective
                            ? new Color("6f541d") : new Color("285f4c"),
                    },
                });
            _waypointLocatorRoot.AddChild(node);
        }
        AddChild(_waypointLocatorRoot);

        _waypointRouteDebugRoot = new Node3D { Name = "WaypointRouteDiagnostics" };
        var known = profile.Waypoints.Select(item => item.Id)
            .Concat(profile.Locators.Select(item => item.Id)).ToHashSet();
        foreach (var route in profile.PatrolRoutes)
        {
            var segments = WaypointRouteService.AnalyzeRoute(route, knownPointIds: known,
                costPerMeter: profile.RouteVisualization.CostPerMeter);
            var mesh = new ImmediateMesh();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
            foreach (var segment in segments)
            {
                var from = route.Points.Single(item => item.Id == segment.FromId);
                var to = route.Points.Single(item => item.Id == segment.ToId);
                mesh.SurfaceAddVertex(new Vector3(from.Position.X, from.Position.Y + .08f,
                    from.Position.Z));
                mesh.SurfaceAddVertex(new Vector3(to.Position.X, to.Position.Y + .08f,
                    to.Position.Z));
            }
            mesh.SurfaceEnd();
            var display = new MeshInstance3D
            {
                Name = $"Route_{route.Id:D}",
                Mesh = mesh,
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    AlbedoColor = new Color("8b5cf6"),
                },
            };
            display.SetMeta("route_segments", segments.Count);
            display.SetMeta("route_cost", segments.Sum(item => item.Cost));
            display.SetMeta("route_timing", segments.Sum(item => item.TravelSeconds));
            _waypointRouteDebugRoot.AddChild(display);
        }
        AddChild(_waypointRouteDebugRoot);
    }

    private void ConfigureAudioRangeDebug(AreaAudioProfile profile)
    {
        if (_audioRangeDebugRoot is not null && GodotObject.IsInstanceValid(_audioRangeDebugRoot))
            _audioRangeDebugRoot.QueueFree();
        _audioRangeDebugRoot = null;
        var preview = AudioOrchestrationRuntimeService.PreviewAttenuation(profile.Spatial);
        if (preview.DebugSpheres.Count == 0) return;
        var root = new Node3D { Name = "ForgeAudioRangeDebug" };
        foreach (var sphere in preview.DebugSpheres)
        {
            var color = sphere.Kind == "inner" ? new Color(0.3f, 0.9f, 0.65f, .16f) :
                new Color(1f, .5f, .25f, .1f);
            var material = new StandardMaterial3D
            {
                AlbedoColor = color,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Front,
            };
            root.AddChild(new MeshInstance3D
            {
                Name = $"{sphere.Kind}Range",
                Mesh = new SphereMesh
                {
                    Radius = Math.Max(.01f, sphere.Radius),
                    Height = Math.Max(.02f, sphere.Radius * 2),
                    RadialSegments = 32,
                    Rings = 16,
                    Material = material,
                },
            });
        }
        AddChild(root);
        _audioRangeDebugRoot = root;
    }

    private void ConfigureAdaptiveAmbienceAudio(AreaAudioProfile profile, AudioListenerContext previous,
        AudioListenerContext next)
    {
        var plan = AudioOrchestrationRuntimeService.PlanAdaptiveCrossfade(profile, previous, next);
        foreach (var entry in plan.Entries)
        {
            if (!_adaptiveAmbiencePlayers.TryGetValue(entry.Resource, out var player) ||
                !GodotObject.IsInstanceValid(player))
            {
                var stream = LoadAudioStream(entry.Resource);
                if (stream is null) continue;
                player = new AudioStreamPlayer
                {
                    Name = $"ForgeAdaptiveAudio_{Convert.ToHexString(SHA256.HashData(
                        Encoding.UTF8.GetBytes(entry.Resource)))[..12]}",
                    Stream = ConfigureLoopStream(stream,
                        new AudioLoopPolicy(AudioLoopMode.Loop, 0, 0, 0, 0, string.Empty)),
                    Bus = AudioServer.GetBusIndex(profile.Authoring.Ambient.Bus) >= 0
                        ? profile.Authoring.Ambient.Bus
                        : "Master",
                    VolumeDb = Mathf.LinearToDb(Math.Max(.0001f, entry.FromGain)),
                };
                AddChild(player);
                player.Play();
                _adaptiveAmbiencePlayers[entry.Resource] = player;
            }
            var targetDb = Mathf.LinearToDb(Math.Max(.0001f, entry.ToGain));
            if (entry.CrossfadeSeconds <= 0)
                player.VolumeDb = targetDb;
            else
                CreateTween().TweenProperty(player, "volume_db", targetDb, entry.CrossfadeSeconds);
            if (entry.ToGain <= 0)
            {
                var retiring = player;
                GetTree().CreateTimer(Math.Max(.01, entry.CrossfadeSeconds)).Timeout += () =>
                {
                    if (!GodotObject.IsInstanceValid(retiring)) return;
                    retiring.Stop();
                    retiring.QueueFree();
                    _adaptiveAmbiencePlayers.Remove(entry.Resource);
                };
            }
        }
    }

    private static void ApplyAudioPlaybackControl(AudioStreamPlayer player,
        AudioPlaybackControlState state)
    {
        player.StreamPaused = state.Status == AudioPlaybackStatus.Paused;
        player.VolumeDb = Mathf.LinearToDb(Math.Max(.0001f, state.Gain));
        if (state.Status == AudioPlaybackStatus.Stopped) player.Stop();
        else if (!player.Playing && state.Status is AudioPlaybackStatus.Playing or AudioPlaybackStatus.Fading)
            player.Play();
    }

    private static IReadOnlyList<AudioBusMeterSample> MonitorRuntimeMix(AreaAudioProfile profile)
    {
        var readings = new Dictionary<string, (float, float, float)>(StringComparer.Ordinal);
        foreach (var bus in profile.Orchestration.MixBuses.Where(item => item.MonitorEnabled))
        {
            var index = AudioServer.GetBusIndex(bus.Name);
            var peak = index >= 0 ? AudioServer.GetBusPeakVolumeLeftDb(index, 0) : -80;
            readings[bus.Name] = (peak, peak - 6, peak - 12);
        }
        return AudioOrchestrationRuntimeService.MonitorMix(profile.Orchestration, readings,
            OS.GetName().Equals("Windows", StringComparison.OrdinalIgnoreCase) ? "windows" : "all", "default");
    }

    private static AudioStream ConfigureLoopStream(AudioStream source, AudioLoopPolicy loop)
    {
        var stream = source.Duplicate(deep: true) as AudioStream ?? source;
        var loopsContinuously = loop.Mode is AudioLoopMode.Loop or AudioLoopMode.SeamlessRegion;
        if (stream is AudioStreamOggVorbis ogg)
        {
            ogg.Loop = loopsContinuously;
            ogg.LoopOffset = loop.Mode == AudioLoopMode.SeamlessRegion ? loop.RegionStartSeconds : 0;
        }
        else if (stream is AudioStreamWav wav)
        {
            wav.LoopMode = loopsContinuously
                ? AudioStreamWav.LoopModeEnum.Forward
                : AudioStreamWav.LoopModeEnum.Disabled;
            if (loop.Mode == AudioLoopMode.SeamlessRegion)
            {
                wav.LoopBegin = Math.Max(0, (int)Math.Round(loop.RegionStartSeconds * wav.MixRate));
                wav.LoopEnd = Math.Max(wav.LoopBegin + 1,
                    (int)Math.Round(loop.RegionEndSeconds * wav.MixRate));
            }
        }
        return stream;
    }


}
