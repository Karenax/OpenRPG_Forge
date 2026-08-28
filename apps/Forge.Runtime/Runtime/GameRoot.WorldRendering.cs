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
    private async Task ConfigureWorldAsync(
        AreaEnvironmentProfile environmentProfile,
        AreaWorldProfile worldProfile,
        AreaNavigationDefinition navigation)
    {
        ArgumentNullException.ThrowIfNull(environmentProfile);
        ArgumentNullException.ThrowIfNull(worldProfile);
        ArgumentNullException.ThrowIfNull(navigation);
        var dynamicLights = _moduleExperience.ArtKitId is not null ? 8 : 2;
        var shadowLights = 2;
        _renderPlan = AreaRenderPlanBuilder.Build(
            environmentProfile,
            OS.HasFeature("mobile") ? "mobile" : "desktop",
            dynamicLights,
            shadowLights);
        var engineEnvironment = GodotEnvironmentAdapter.Build(_renderPlan);
        _worldEnvironment = engineEnvironment.Environment;
        _baseAmbientLightEnergy = _worldEnvironment.AmbientLightEnergy;
        _baseBackgroundEnergyMultiplier = _worldEnvironment.BackgroundEnergyMultiplier;
        _baseTonemapExposure = _worldEnvironment.TonemapExposure;
        _baseFogEnabled = _worldEnvironment.FogEnabled;
        _baseFogDensity = _worldEnvironment.FogDensity;
        _baseFogLightEnergy = _worldEnvironment.FogLightEnergy;
        _baseCloudCoverage = (float)environmentProfile.Sky.CloudCoverage;
        RenderingServer.SetDefaultClearColor(new Color(environmentProfile.Sky.GroundColor));
        AddChild(new WorldEnvironment
        {
            Name = "ForgeWorldEnvironment",
            Environment = engineEnvironment.Environment,
        });
        GetViewport().UseHdr2D = environmentProfile.Rendering.HdrOutput;
        GetViewport().PositionalShadowAtlasSize = engineEnvironment.ShadowAtlasSize;

        var sun = new DirectionalLight3D
        {
            Name = "ForgeReferenceSun",
            RotationDegrees = new Vector3(
                (float)environmentProfile.Rendering.VisualTuning.DirectionalLightPitchDegrees,
                (float)environmentProfile.Rendering.VisualTuning.DirectionalLightYawDegrees,
                0),
            LightColor = engineEnvironment.DirectionalLightColor,
            LightEnergy = engineEnvironment.DirectionalLightEnergy,
            LightVolumetricFogEnergy = 1.25f,
            LightAngularDistance = (float)environmentProfile.Rendering.VisualTuning.DirectionalLightAngularDistance,
            ShadowEnabled = environmentProfile.Lighting.Shadows,
            ShadowBlur = (float)environmentProfile.Rendering.VisualTuning.ShadowBlur,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits,
            DirectionalShadowMaxDistance = 320,
            DirectionalShadowFadeStart = .82f,
        };
        _referenceSun = sun;
        _baseDirectionalLightEnergy = sun.LightEnergy;
        AddChild(sun);

        if (environmentProfile.Tags.Contains("indoor", StringComparer.OrdinalIgnoreCase))
        {
            var interiorLights = new[]
            {
                (Position: new Vector3(-6, 5.2f, -4), Color: new Color("ffe0b8"), Energy: 2.35f, Range: 23f, Shadows: true),
                (Position: new Vector3(7, 5.8f, 5), Color: new Color("b5d8ff"), Energy: 2.15f, Range: 30f, Shadows: false),
            };
            foreach (var light in interiorLights)
            {
                AddChild(new OmniLight3D
                {
                    Position = light.Position,
                    OmniRange = light.Range,
                    LightColor = light.Color,
                    LightEnergy = light.Energy,
                    LightVolumetricFogEnergy = .58f,
                    ShadowEnabled = light.Shadows,
                    ShadowBlur = 1.8f,
                });
            }
        }

        if (_moduleExperience.EmberAtmosphereEnabled)
        {
            AddChild(new OmniLight3D
            {
                Position = new Vector3(0, 5, -7),
                OmniRange = 18,
                LightColor = new Color("ff7043"),
                LightEnergy = 4,
                ShadowEnabled = true,
            });
        }

        foreach (var diagnostic in engineEnvironment.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_RENDER_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }

        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_RENDER_PROFILE_PASS pbr={environmentProfile.Rendering.MaterialWorkflow.ToString().ToLowerInvariant()} " +
            $"tone={environmentProfile.Rendering.ToneMapping.ToString().ToLowerInvariant()} hdr={environmentProfile.Rendering.HdrOutput.ToString().ToLowerInvariant()} " +
            $"perpixel={environmentProfile.Rendering.PerPixelLighting.ToString().ToLowerInvariant()} sky={environmentProfile.Sky.Mode.ToString().ToLowerInvariant()} " +
            $"lights={dynamicLights}/{_renderPlan.LightBudget.MaximumDynamicLights} shadows={shadowLights}/{_renderPlan.LightBudget.MaximumShadowCastingLights}");
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_RENDER_BACKEND method={RenderingServer.GetCurrentRenderingMethod()} " +
            $"driver={RenderingServer.GetCurrentRenderingDriverName()}");
        _environmentProfile = environmentProfile;
        _weatherState = WeatherRuntimeStateMachine.CreateInitial(environmentProfile.Weather);
        ApplyClimateEffects(force: true);
        await ConfigureAreaWorldAsync(worldProfile, navigation);
        ConfigureAudioEmitters(environmentProfile.Audio);
        ConfigureAreaAmbientAudio(environmentProfile.Audio);

        if (_moduleExperience.ArtKitId is not null)
        {
            CreateAuthoredArtKitAtmosphere(_moduleExperience.ArtKitId);
        }

        if (environmentProfile.Tags.Contains("vfx:forest-ambient", StringComparer.Ordinal))
        {
            var atmosphere = GodotForestAtmosphereAdapter.Build();
            _ambientVfxEmitters = atmosphere.AnimatedEmitters;
            _ambientVfxParticles = atmosphere.Particles;
            AddChild(atmosphere.Root);
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_FOREST_ATMOSPHERE_PASS emitters={atmosphere.AnimatedEmitters} " +
                $"particles={atmosphere.Particles} provenance={atmosphere.Provenance}");
        }

        _camera = new Camera3D
        {
            Position = new Vector3(13, 15, 17),
            Current = true,
            Fov = 55,
        };
        AddChild(_camera);
        _camera.LookAt(Vector3.Zero, Vector3.Up);
    }

    private async Task ConfigureAreaWorldAsync(AreaWorldProfile profile, AreaNavigationDefinition navigation)
    {
        var started = Stopwatch.GetTimestamp();
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        _terrainWorkingSetBeforeBytes = process.WorkingSet64;
        _terrainManagedHeapBeforeBytes = GC.GetTotalMemory(forceFullCollection: false);
        _activeBiomeTags = profile.Biomes.SelectMany(item => item.Tags)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _activeTerrain = profile.Terrain;
        var terrain = GodotTerrainAdapter.Build(
            profile,
            _modelLibrary is null ? null : _modelLibrary.ResolveScatterMesh,
            _environmentProfile?.Water.Bodies,
            ResolveContentTexture);
        _terrainVertices = terrain.Vertices;
        _terrainTriangles = terrain.Triangles;
        _terrainSectors = terrain.TerrainSectors;
        _terrainMaterialMode = terrain.TerrainMaterialMode;
        _terrainMaterialLayers = terrain.TerrainMaterialLayers;
        _terrainCollisionShapes = terrain.CollisionShapes;
        _terrainScatterLayers = terrain.ScatterLayers;
        _terrainScatterInstances = terrain.ScatterInstances;
        _terrainWindAnimatedLayers = terrain.WindAnimatedScatterLayers;
        _terrainCullingGroups = terrain.CullingGroups;
        _terrainBuildMetrics = terrain.Metrics;
        _terrainWaterSurfaces = terrain.WaterSurfaces;
        _terrainWaterVolumes = terrain.WaterVolumes;
        var surfaceNavigation = GodotSurfaceNavigationAdapter.Build(profile, navigation);
        _surfaceNavigationRegions = surfaceNavigation.Regions;
        _surfaceNavigationPolygons = surfaceNavigation.Polygons;
        _surfaceNavigationBlockedTriangles = surfaceNavigation.BlockedTriangles;
        _surfaceNavigationLinks = surfaceNavigation.Links;
        _surfaceNavigationBuildMilliseconds = surfaceNavigation.BuildMilliseconds;
        _surfaceNavigationManagedAllocatedBytes = surfaceNavigation.ManagedAllocatedBytes;
        _areaWorldRoot = terrain.Root;
        _areaWorldRoot.AddChild(surfaceNavigation.Root);
        AddChild(_areaWorldRoot);
        _terrainManagedHeapAfterBytes = GC.GetTotalMemory(forceFullCollection: false);
        process.Refresh();
        _terrainWorkingSetAfterBytes = process.WorkingSet64;
        foreach (var diagnostic in terrain.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_TERRAIN_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }
        foreach (var diagnostic in surfaceNavigation.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_NAVIGATION_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }

        AreaStreamingResult? streaming = null;
        var presentation = _areaExperience?.Loading ??
            throw new InvalidOperationException("Area experience must be configured before streaming.");
        var imageAvailable = LoadingResourceExists(presentation.ImageResource);
        var videoAvailable = LoadingResourceExists(presentation.VideoResource);
        var requiredResourceCount = presentation.PrefetchRequiredResources
            ? profile.Loading.RequiredResources.Count
            : 0;
        var reduceMotion = OS.GetCmdlineUserArgs().Contains("--reduce-motion", StringComparer.Ordinal);
        var overlay = new RuntimeLoadingOverlay(_moduleContentRoot ?? string.Empty);
        AddChild(overlay);
        var loadingFrame = AreaExperienceRuntimeService.ResolveLoading(presentation, _defaultLocale, 0,
            requiredResourceCount, 0, reduceMotion, imageAvailable, videoAvailable);
        overlay.Apply(loadingFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (requiredResourceCount > 0)
        {
            if (string.IsNullOrWhiteSpace(_moduleContentRoot))
            {
                throw new InvalidOperationException("Area streaming resources require a module content root.");
            }

            var coordinator = new AreaStreamingCoordinator(new RuntimeAreaChunkLoader(_moduleContentRoot));
            var progress = new Progress<AreaStreamingProgress>(update =>
                overlay.Apply(AreaExperienceRuntimeService.ResolveLoading(presentation, _defaultLocale,
                    update.CompletedResources, Math.Max(update.TotalResources,
                    requiredResourceCount), update.CompletedResources, reduceMotion,
                    imageAvailable, videoAvailable), update.CurrentResource));
            streaming = await coordinator.LoadAsync(profile.Loading, progress);
        }

        LoadPromotedGeneratedWorld(profile.Loading);

        var loadedResources = streaming?.Resources.Count ?? 0;
        loadingFrame = AreaExperienceRuntimeService.ResolveLoading(presentation, _defaultLocale,
            loadedResources, loadedResources, loadedResources, reduceMotion, imageAvailable, videoAvailable);
        overlay.Apply(loadingFrame);

        var remaining = presentation.MinimumVisibleMilliseconds -
                        (int)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (remaining > 0)
            await ToSignal(GetTree().CreateTimer(remaining / 1000.0), SceneTreeTimer.SignalName.Timeout);
        overlay.Complete();
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11N_LOADING_PASS media={loadingFrame.MediaKind.ToString().ToLowerInvariant()} " +
            $"progress={loadingFrame.Progress:0.###} accessible={!string.IsNullOrWhiteSpace(loadingFrame.AccessibleText)} " +
            $"prefetch={presentation.PrefetchRequiredResources.ToString().ToLowerInvariant()} streamed={loadedResources}");

        var elapsed = Stopwatch.GetElapsedTime(started);
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11I_TERRAIN_PASS provider={profile.Terrain.Provider.ToString().ToLowerInvariant()} " +
            $"vertices={terrain.Vertices} triangles={terrain.Triangles} sectors={terrain.TerrainSectors} " +
            $"material={terrain.TerrainMaterialMode} material_layers={terrain.TerrainMaterialLayers} " +
            $"collisions={terrain.CollisionShapes} " +
            $"features={terrain.FeatureSegments} volumes={terrain.DebugVolumes} routes={terrain.FlightRouteSegments} " +
            $"scatter_layers={terrain.ScatterLayers} scatter_instances={terrain.ScatterInstances} " +
            $"wind_layers={terrain.WindAnimatedScatterLayers} " +
            $"water_surfaces={terrain.WaterSurfaces} water_volumes={terrain.WaterVolumes} " +
            $"nav_regions={surfaceNavigation.Regions} nav_polygons={surfaceNavigation.Polygons} " +
            $"nav_blocked={surfaceNavigation.BlockedTriangles} nav_links={surfaceNavigation.Links} " +
            $"streamed={streaming?.Resources.Count ?? 0} cache_hits={streaming?.CacheHits ?? 0} " +
            $"plan_ms={terrain.Metrics.PlanMilliseconds:0.###} mesh_ms={terrain.Metrics.MeshMilliseconds:0.###} " +
            $"collision_ms={terrain.Metrics.CollisionMilliseconds:0.###} " +
            $"features_ms={terrain.Metrics.FeatureMilliseconds:0.###} " +
            $"scatter_ms={terrain.Metrics.ScatterMilliseconds:0.###} " +
            $"scatter_mesh_resolve_ms={terrain.Metrics.ScatterMeshResolutionMilliseconds:0.###} " +
            $"scatter_mesh_prepare_ms={terrain.Metrics.ScatterMeshPreparationMilliseconds:0.###} " +
            $"scatter_placement_ms={terrain.Metrics.ScatterPlacementMilliseconds:0.###} " +
            $"scatter_cells_ms={terrain.Metrics.ScatterCellPlanningMilliseconds:0.###} " +
            $"scatter_upload_ms={terrain.Metrics.ScatterInstanceUploadMilliseconds:0.###} " +
            $"managed_allocated={terrain.Metrics.ManagedAllocatedBytes} " +
            $"elapsed_ms={elapsed.TotalMilliseconds:0.###} target_ms={profile.Loading.TargetLoadMilliseconds}");
    }

    private Texture2D? ResolveContentTexture(string relativePath)
    {
        if (_surfaceTextureCache.TryGetValue(relativePath, out var cached)) return cached;
        if (string.IsNullOrWhiteSpace(_moduleContentRoot) ||
            Path.IsPathFullyQualified(relativePath) ||
            relativePath.Split('/', '\\').Any(segment => segment is "" or "." or ".."))
        {
            return null;
        }

        var root = Path.GetFullPath(_moduleContentRoot);
        var absolute = Path.GetFullPath(Path.Combine(root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(absolute))
            return null;
        var image = Image.LoadFromFile(absolute);
        if (image.IsEmpty()) return null;
        var texture = ImageTexture.CreateFromImage(image);
        _surfaceTextureCache[relativePath] = texture;
        return texture;
    }

    private bool LoadingResourceExists(string resourcePath) => CinematicResourceExists(resourcePath);

    private void AdvanceAreaExperience(double delta)
    {
        if (_areaExperience is null || _areaExperience.ScatterEmitters.Count == 0) return;
        foreach (var emitter in _areaExperience.ScatterEmitters)
        {
            var due = _scatterDueSeconds.GetValueOrDefault(emitter.Id) - delta;
            if (due > 0)
            {
                _scatterDueSeconds[emitter.Id] = due;
                continue;
            }

            var sequence = _scatterSequences.GetValueOrDefault(emitter.Id);
            var emission = AreaExperienceRuntimeService.ResolveScatter(_areaExperience, emitter.Id, sequence,
                _activeBiomeTags, new HashSet<string>(StringComparer.OrdinalIgnoreCase), navigable: true);
            _scatterSequences[emitter.Id] = sequence + 1;
            if (emission is null)
            {
                _scatterDueSeconds[emitter.Id] = Math.Max(1, emitter.MinimumIntervalSeconds);
                continue;
            }

            _scatterDueSeconds[emitter.Id] = Math.Max(.05, emission.DelaySeconds);
            var result = AreaExperienceRuntimeService.PlayCue(_areaExperience, _areaAudioState, emission.CueId,
                DateTimeOffset.UtcNow);
            _areaAudioState = result.State;
            PlaySpatialCue(result.Playback, emission.Position, emission.Volume);
        }
    }

    private void PlaySpatialCue(AudioCuePlayback playback, Vector3Value position, float scatterVolume)
    {
        var stream = LoadAudioStream(playback.Resource);
        if (stream is null)
        {
            _areaAudioState = AreaExperienceRuntimeService.Complete(_areaAudioState, playback.PlaybackId,
                DateTimeOffset.UtcNow);
            GD.Print($"OPENRPG_FORGE_RUNTIME_AUDIO_CUE cue={playback.CueKey} event=completed resource=unavailable");
            return;
        }

        var player = new AudioStreamPlayer3D
        {
            Name = $"ForgeAudio_{playback.CueKey.Replace('.', '_')}",
            Stream = stream,
            Position = EngineValueConverter.ToEngine(position),
            VolumeDb = Mathf.LinearToDb(Math.Max(.001f, playback.Volume * scatterVolume)),
            Bus = AudioServer.GetBusIndex(playback.Bus) >= 0 ? playback.Bus : "Master",
            Autoplay = true,
        };
        player.Finished += () =>
        {
            _areaAudioState = AreaExperienceRuntimeService.Complete(_areaAudioState, playback.PlaybackId,
                DateTimeOffset.UtcNow);
            player.QueueFree();
        };
        AddChild(player);
        GD.Print($"OPENRPG_FORGE_RUNTIME_AUDIO_CUE cue={playback.CueKey} event=started spatial=true");
    }

    private AudioStream? LoadAudioStream(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath)) return null;
        if (resourcePath.StartsWith("builtin://", StringComparison.Ordinal))
        {
            if (!ProceduralAmbientAudioService.IsSupported(resourcePath))
                return null;
            if (!_proceduralAudioCache.TryGetValue(resourcePath, out var procedural))
            {
                var clip = ProceduralAmbientAudioService.Create(resourcePath);
                procedural = new AudioStreamWav
                {
                    Format = AudioStreamWav.FormatEnum.Format16Bits,
                    MixRate = clip.SampleRate,
                    Stereo = false,
                    LoopMode = AudioStreamWav.LoopModeEnum.Disabled,
                    Data = clip.Pcm16Mono,
                };
                _proceduralAudioCache.Add(resourcePath, procedural);
            }

            return procedural;
        }

        var direct = resourcePath.StartsWith("res://", StringComparison.Ordinal)
            ? resourcePath
            : _moduleContentRoot is null
                ? $"res://{resourcePath}"
                : ProjectSettings.LocalizePath(Path.Combine(_moduleContentRoot,
                    resourcePath.Replace('/', Path.DirectorySeparatorChar)));
        return direct.StartsWith("res://", StringComparison.Ordinal) && ResourceLoader.Exists(direct)
            ? ResourceLoader.Load<AudioStream>(direct)
            : null;
    }


}
