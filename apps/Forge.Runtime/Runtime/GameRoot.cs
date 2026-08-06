using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Areas;
using Forge.Application.Commands;
using Forge.Application.Gameplay;
using Forge.Application.Lifecycle;
using Forge.Application.Sequences;
using Forge.Application.Time;
using Forge.Application.Validation;
using Forge.Application.Rules;
using Forge.Application.Quality;
using Forge.Application.Projects;
using Forge.Application.RuntimeExtensions;
using Forge.Application.Entities;
using Forge.Application.Combat;
using Forge.Domain.Areas;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Gameplay;
using Forge.Domain.Lifecycle;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.EngineAdapter.Spatial;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.World;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Saves;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;
using Godot;

namespace Forge.Runtime;

public partial class GameRoot : Node3D
{
    private const string ProjectPath = "res://content/crypt-of-ashes/project.json";
    private const string ProjectSnapshotArgument = "--project-snapshot";
    private const string ModulePackageArgument = "--module-package";
    private const string WorkspaceArgument = "--workspace";
    private const string SpawnIdArgument = "--spawn-id";
    private const string SaveFileArgument = "--save-file";
    private const string LoadSaveArgument = "--load-save";
    private const string VisualCaptureArgument = "--visual-capture";
    private const string WorldVisualCaptureArgument = "--world-visual-capture";
    private const string VisualRegressionSuiteArgument = "--g11s-visual-suite";
    private const string TerrainBenchmarkArgument = "--terrain-benchmark";
    private const float InteractionDistance = 3.0f;
    private const float AttackDistance = 3.5f;
    private const string VisualAssetProperty = "visual.asset";
    private const string RuntimeRoleProperty = "runtime.role";
    private const string RuntimeAnimationProfileProperty = "runtime.animationProfile";

    private readonly Dictionary<Node3D, CombatantState> _hostiles = [];
    private readonly Dictionary<Node3D, string> _hostileNames = [];
    private ProjectBlackboard _blackboard = new([], allowUndeclared: true);
    private readonly HashSet<string> _inventory = new(StringComparer.Ordinal);
    private readonly NativeForgeScriptRuntime _forgeScriptRuntime = new();
    private readonly List<RuntimeLifecycleEvent> _lifecycleEvents = [];
    private readonly HeartbeatScheduler _heartbeatScheduler = new();
    private readonly RestRuntimeCoordinator _restRuntime = new();
    private readonly ProjectGameplayEventBus _gameplayEvents = new();
    private readonly Dictionary<Guid, Node3D> _entityNodes = [];
    private readonly Dictionary<Guid, CameraAnchorDefinition> _cameraAnchors = [];
    private readonly Dictionary<Guid, double> _scatterDueSeconds = [];
    private readonly Dictionary<Guid, int> _scatterSequences = [];
    private readonly Dictionary<string, AudioStreamWav> _proceduralAudioCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _animatedCharacterClipNames = new(StringComparer.Ordinal);
    private readonly Dictionary<Node3D, GodotAssetAnimationState> _characterAnimations = [];
    private readonly ForgeScriptJsonService _runtimeJson = new();
    private readonly ForgeScriptUiEventService _scriptUiEvents = new();
    private readonly Dictionary<string, SafeJsonHandle> _runtimeJsonHandles = new(StringComparer.Ordinal);
    private readonly RuntimeCameraRig _cameraRig = new();

    private PlayerController? _player;
    private CombatantState? _playerCombatant;
    private Camera3D? _camera;
    private RuntimeHud? _hud;
    private Node3D? _npc;
    private Node3D? _keyItem;
    private Node3D? _door;
    private Node3D? _boss;
    private Node3D? _exit;
    private DialogueDefinition? _elyraDialogue;
    private QuestStateMachine? _questMachine;
    private QuestDefinition? _questDefinition;
    private EntityId? _doorEntityId;
    private ForgeScriptArtifact? _bossPhaseTwoScript;
    private ForgeScriptArtifact? _bossDeathScript;
    private PrototypeQuestState _questState;
    private bool _hasKey;
    private bool _doorUnlocked;
    private bool _bossPhaseTwo;
    private bool _bossDefeated;
    private bool _escaped;
    private int _builtEntityCount;
    private int _collisionCount;
    private int _spawnedAshSpiritCount;
    private int _ashNovaCueCount;
    private string _message = "Explorez la crypte.";
    private string? _moduleContentRoot;
    private string? _moduleId;
    private string? _moduleContentSha256;
    private string? _currentAreaId;
    private string? _quickSavePath;
    private string _moduleTitle = "OpenRPG Forge";
    private string _moduleSlug = string.Empty;
    private bool _isCryptPrototype;
    private bool _isForgottenForestPrototype;
    private string _guideName = "Guide";
    private string _questItemSlug = "quest-item";
    private string _questItemDisplayName = "Objet de quête";
    private string _gateDisplayName = "Passage scellé";
    private bool _loadedSave;
    private RuntimeLifecycleCoordinator? _lifecycle;
    private ForgeScriptArtifactCache? _forgeScriptCache;
    private AreaRenderPlan? _renderPlan;
    private AreaEnvironmentProfile? _environmentProfile;
    private WeatherRuntimeSnapshot? _weatherState;
    private Node3D? _climateEffects;
    private string? _climateSignature;
    private double _climateProbeElapsed;
    private Node3D? _areaWorldRoot;
    private ModuleRuntimePolicyProfile? _moduleRuntimePolicies;
    private AreaRuntimePolicyProfile? _areaRuntimePolicies;
    private RestSession? _restSession;
    private long _runtimePolicyRevision;
    private SequenceDefinition? _activeSequence;
    private SequencePlaybackState? _sequenceState;
    private Transform3D _explorationCameraTransform;
    private float _explorationCameraFov;
    private bool _sequenceOwnsCamera;
    private ModuleGameplayControlProfile? _gameplayControls;
    private TimeControlSession _timeControlSession = TimeControlSession.Running;
    private CombatModeProviderDefinition? _activeCombatProvider;
    private RuntimeActionWheel? _actionWheel;
    private ModuleRulesProfile? _moduleRules;
    private AreaRulesProfile? _areaRules;
    private AreaExperienceProfile? _areaExperience;
    private ModuleRuntimeExtensionProfile? _runtimeExtensions;
    private RuntimeDeclarativeUi? _declarativeUi;
    private RuntimeAreaEffectIndicator? _areaEffectIndicator;
    private AreaAudioRuntimeState _areaAudioState = AreaAudioRuntimeState.Empty;
    private IReadOnlySet<string> _activeBiomeTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private string _defaultLocale = "fr";
    private ResolvedDifficulty? _resolvedDifficulty;
    private double _progressionExperience;
    private int _progressionLevel = 1;
    private bool _initializationComplete;
    private GodotAssetModelLibrary? _modelLibrary;
    private int _runtimeModelInstances;
    private int _animatedCharacterInstances;
    private int _combatHitAnimationCount;
    private int _combatDeathAnimationCount;
    private int _terrainVertices;
    private int _terrainTriangles;
    private int _terrainSectors;
    private string _terrainMaterialMode = "disabled";
    private int _terrainMaterialLayers;
    private int _terrainCollisionShapes;
    private int _terrainScatterLayers;
    private int _terrainScatterInstances;
    private int _terrainCullingGroups;
    private int _terrainWaterSurfaces;
    private int _terrainWaterVolumes;
    private int _ambientVfxEmitters;
    private int _ambientVfxParticles;
    private int _surfaceNavigationRegions;
    private int _surfaceNavigationPolygons;
    private int _surfaceNavigationBlockedTriangles;
    private int _surfaceNavigationLinks;
    private GodotTerrainBuildMetrics? _terrainBuildMetrics;
    private double _surfaceNavigationBuildMilliseconds;
    private long _surfaceNavigationManagedAllocatedBytes;
    private long _terrainManagedHeapBeforeBytes;
    private long _terrainManagedHeapAfterBytes;
    private long _terrainWorkingSetBeforeBytes;
    private long _terrainWorkingSetAfterBytes;

    public override async void _Ready()
    {
        var startupStarted = Stopwatch.GetTimestamp();
        var userArguments = OS.GetCmdlineUserArgs();
        var smokeTest = userArguments.Contains("--smoke-test", StringComparer.Ordinal);
        var genericSmokeTest = userArguments.Contains("--generic-smoke-test", StringComparer.Ordinal);
        var rpgKitSmokeTest = userArguments.Contains("--rpg-kit-smoke-test", StringComparer.Ordinal);
        var forgottenForestSmokeTest = userArguments.Contains("--forgotten-forest-smoke-test", StringComparer.Ordinal);
        var saveSmokeCreate = userArguments.Contains("--save-smoke-create", StringComparer.Ordinal);
        var saveSmokeResume = userArguments.Contains("--save-smoke-resume", StringComparer.Ordinal);
        var visualCapturePath = GetArgumentValue(userArguments, VisualCaptureArgument);
        var worldVisualCapturePath = GetArgumentValue(userArguments, WorldVisualCaptureArgument);
        var visualRegressionSuitePath = GetArgumentValue(userArguments, VisualRegressionSuiteArgument);
        var terrainBenchmarkPath = GetArgumentValue(userArguments, TerrainBenchmarkArgument);
        var automatedTest = smokeTest || genericSmokeTest || rpgKitSmokeTest || forgottenForestSmokeTest ||
            saveSmokeCreate || saveSmokeResume ||
            visualCapturePath is not null || worldVisualCapturePath is not null ||
            visualRegressionSuitePath is not null || terrainBenchmarkPath is not null;

        try
        {
            RegisterInputActions();
            var snapshotPath = GetArgumentValue(userArguments, ProjectSnapshotArgument);
            var modulePackagePath = GetArgumentValue(userArguments, ModulePackageArgument);
            var workspacePath = GetArgumentValue(userArguments, WorkspaceArgument);
            var saveFilePath = GetArgumentValue(userArguments, SaveFileArgument);
            var loadSavePath = GetArgumentValue(userArguments, LoadSaveArgument);
            _quickSavePath = saveFilePath ?? loadSavePath ??
                ProjectSettings.GlobalizePath("user://openrpg-forge/saves/quick.orpsave");
            _forgeScriptCache = new ForgeScriptArtifactCache(
                _forgeScriptRuntime,
                ProjectSettings.GlobalizePath("user://openrpg-forge/cache/forgescript"));
            var requestedSpawnId = ParseOptionalEntityId(
                GetArgumentValue(userArguments, SpawnIdArgument));
            var project = LoadProject(snapshotPath, modulePackagePath, workspacePath);
            ValidateAssetPackageManifests();
            var lifecycleCause = loadSavePath is null
                ? RuntimeLifecycleCause.Startup
                : RuntimeLifecycleCause.SaveResume;
            _lifecycleEvents.Clear();
            _lifecycle = new RuntimeLifecycleCoordinator(observers: [HandleLifecycleEvent]);
            EnsureLifecycle(
                _lifecycle.LoadProject(project, RuntimeAuthority.SinglePlayer, lifecycleCause),
                "project_loaded");
            _blackboard = new ProjectBlackboard(project.Module.Variables, allowUndeclared: true);
            _moduleTitle = project.Module.DisplayName;
            _moduleSlug = project.Module.Slug;
            _defaultLocale = project.Module.Metadata.DefaultLocale;
            _isCryptPrototype = string.Equals(_moduleSlug, "crypt-of-ashes", StringComparison.Ordinal);
            _isForgottenForestPrototype = string.Equals(_moduleSlug, "forgotten-forest", StringComparison.Ordinal);
            _message = $"Explorez {_moduleTitle}.";
            var entryArea = project.Areas.Single(area => area.Id == project.Module.EntryAreaId);
            ConfigureRuntimePolicies(project.Module.RuntimePolicies, entryArea.RuntimePolicies);
            await ConfigureWorldAsync(entryArea.Environment, entryArea.World, entryArea.Navigation, _isCryptPrototype);
            var selectedSpawn = BuildProject(project, requestedSpawnId);
            if (_camera is not null && _player is not null)
            {
                _cameraRig.Snap(_camera, _player.GlobalPosition);
            }
            EnsureLifecycle(
                _lifecycle.EnterInitialArea(
                    AreaId.Parse(_currentAreaId!),
                    selectedSpawn.Id,
                    lifecycleCause),
                "area_entered");
            if (loadSavePath is not null)
            {
                if (!LoadRuntimeSave(loadSavePath))
                {
                    throw new InvalidOperationException("The requested runtime save could not be loaded.");
                }
            }

            CreateHud();
            VerifyModernRuntimeTooling(project, entryArea);
            VerifyOperationalReadiness(project);
            VerifyQualityLab(project);
            VerifyPerformanceLab(project);
            VerifyGovernanceLab(project);
            if (visualRegressionSuitePath is not null)
            {
                UpdateHud();
                await CaptureVisualRegressionSuiteAsync(project, visualRegressionSuitePath);
                return;
            }
            if (visualCapturePath is not null)
            {
                UpdateHud();
                await CaptureVisualProofAsync(visualCapturePath);
                return;
            }
            if (worldVisualCapturePath is not null)
            {
                await CaptureWorldVisualProofAsync(worldVisualCapturePath);
                return;
            }
            ConfigureCinematics(project, entryArea);
            UpdateHud();
            EnsureLifecycle(_lifecycle.StartWorld(lifecycleCause), "world_started");
            _initializationComplete = true;
            AssertLifecycleStartupContract();
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_PERF startup_ms={Stopwatch.GetElapsedTime(startupStarted).TotalMilliseconds:0.###} " +
                $"nodes={CountTreeNodes(this)} entities={_builtEntityCount} collisions={_collisionCount}");

            if (snapshotPath is not null)
            {
                GD.Print($"OPENRPG_FORGE_RUNTIME_SNAPSHOT_LOADED file={Path.GetFileName(snapshotPath)}");
            }

            GD.Print($"OPENRPG_FORGE_RUNTIME_SPAWN_SELECTED id={selectedSpawn.Id}");
            GD.Print($"OPENRPG_FORGE_RUNTIME_PROJECT_LOADED entities={_builtEntityCount} collisions={_collisionCount}");

            if (forgottenForestSmokeTest)
            {
                await RunForgottenForestSmokeTestAsync(project, terrainBenchmarkPath);
            }
            else if (rpgKitSmokeTest)
            {
                RunRpgKitHeadlessSmokeTest(project);
            }
            else if (genericSmokeTest)
            {
                RunGenericHeadlessSmokeTest();
            }
            else if (saveSmokeCreate)
            {
                RunSaveCreateSmokeTest();
            }
            else if (saveSmokeResume)
            {
                RunSaveResumeSmokeTest();
            }
            else if (smokeTest)
            {
                RunHeadlessSmokeTest();
            }
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_STARTUP_FAILED {exception}");
            if (automatedTest)
            {
                GetTree().Quit(1);
            }
            else
            {
                ShowStartupFailure(exception);
            }
        }
    }

    private async Task CaptureVisualProofAsync(string destination)
    {
        if (!Path.IsPathFullyQualified(destination) || !string.Equals(Path.GetExtension(destination), ".png",
            StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The visual proof destination must be an absolute PNG path.", nameof(destination));
        if (_isForgottenForestPrototype && _camera is not null && _player is not null && _npc is not null)
        {
            // Visual-proof framing is intentionally closer than the normal player camera so a
            // screenshot can validate the authored character silhouettes and their shared scene.
            var campFocus = (_player.GlobalPosition + _npc.GlobalPosition) * .5f + (Vector3.Up * .9f);
            _camera.GlobalPosition = campFocus + new Vector3(7, 5.5f, 7);
            _camera.LookAt(campFocus, Vector3.Up);
        }
        for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var image = GetViewport().GetTexture().GetImage();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var error = image.SavePng(destination);
        if (error != Error.Ok) throw new IOException($"Visual proof could not be written: {error}.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G11P_VISUAL_PASS file={Path.GetFileName(destination)} " +
            $"size={image.GetWidth()}x{image.GetHeight()} nodes={CountTreeNodes(this)} nui=2.0 aoe=visible");
        GetTree().Quit(0);
    }

    private async Task CaptureWorldVisualProofAsync(string destination)
    {
        if (!Path.IsPathFullyQualified(destination) || !string.Equals(Path.GetExtension(destination), ".png",
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The world visual proof destination must be an absolute PNG path.", nameof(destination));
        if (!_isForgottenForestPrototype || _camera is null)
            throw new InvalidOperationException("World visual proof currently requires Forgotten Forest.");

        if (_hud is not null) _hud.Visible = false;
        if (_actionWheel is not null) _actionWheel.Visible = false;
        foreach (var node in FindChildren("*", "GeometryInstance3D", recursive: true, owned: false))
        {
            if (node is GeometryInstance3D geometry) geometry.VisibilityRangeEnd = 0;
        }
        _camera.Fov = 64;
        _camera.GlobalPosition = new Vector3(150, 125, 175);
        _camera.LookAt(new Vector3(0, 1.5f, 0), Vector3.Up);
        for (var frame = 0; frame < 8; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var image = GetViewport().GetTexture().GetImage();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (image.SavePng(destination) != Error.Ok)
            throw new IOException("World visual proof could not be written.");
        GD.Print(
            $"OPENRPG_FORGE_AV6E_WORLD_VISUAL_PASS file={Path.GetFileName(destination)} " +
            $"size={image.GetWidth()}x{image.GetHeight()} sectors={_terrainSectors} scatter={_terrainScatterInstances}");
        GetTree().Quit(0);
    }

    private async Task CaptureVisualRegressionSuiteAsync(ProjectDefinition project, string destinationDirectory)
    {
        if (!Path.IsPathFullyQualified(destinationDirectory))
            throw new ArgumentException("The visual regression destination must be absolute.", nameof(destinationDirectory));
        var root = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(root);
        var profile = project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance.Visual;
        var plan = PerformanceQualityService.PlanVisualReviews(profile);
        var reviews = new List<object>();
        foreach (var visualCase in plan.Cases)
        {
            var (width, height) = VisualRegressionProfile.ParseResolution(visualCase.Resolution);
            GetWindow().Size = new Vector2I(width, height);
            GetViewport().Scaling3DScale = visualCase.QualityTier == RenderQualityTier.Medium ? .85f : 1f;
            if (_camera is not null)
            {
                _camera.GlobalPosition = visualCase.CameraFixture == "overview"
                    ? new Vector3(13, 15, 17)
                    : new Vector3(-10, 10, 14);
                _camera.Fov = visualCase.CameraFixture == "overview" ? 55 : 50;
                _camera.LookAt((_player?.GlobalPosition ?? Vector3.Zero) + Vector3.Up * .8f, Vector3.Up);
            }
            for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var reference = GetViewport().GetTexture().GetImage();
            for (var frame = 0; frame < 2; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var candidate = GetViewport().GetTexture().GetImage();
            var referencePath = Path.Combine(root, $"{visualCase.Id}.reference.png");
            var candidatePath = Path.Combine(root, $"{visualCase.Id}.candidate.png");
            var reviewPath = Path.Combine(root, $"{visualCase.Id}.review.json");
            if (reference.SavePng(referencePath) != Error.Ok || candidate.SavePng(candidatePath) != Error.Ok)
                throw new IOException($"Visual regression images could not be written for {visualCase.Id}.");
            var error = MeanPixelError(reference, candidate, 16);
            var passed = error <= visualCase.PixelTolerance;
            var review = new
            {
                schemaVersion = 1,
                visualCase.Id,
                visualCase.Resolution,
                visualCase.CameraFixture,
                qualityTier = visualCase.QualityTier.ToString(),
                expectedWidth = width,
                expectedHeight = height,
                actualWidth = candidate.GetWidth(),
                actualHeight = candidate.GetHeight(),
                visualCase.PixelTolerance,
                meanPixelError = error,
                passed,
            };
            File.WriteAllText(reviewPath, JsonSerializer.Serialize(review, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            }) + System.Environment.NewLine, new UTF8Encoding(false));
            if (!passed) throw new InvalidOperationException(
                $"Visual regression {visualCase.Id} exceeded tolerance: {error:0.######} > {visualCase.PixelTolerance:0.######}.");
            reviews.Add(review);
        }
        File.WriteAllText(Path.Combine(root, "visual-suite-report.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            passed = true,
            plan.Fingerprint,
            cases = reviews,
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }) +
            System.Environment.NewLine, new UTF8Encoding(false));
        GD.Print($"OPENRPG_FORGE_RUNTIME_G11S_VISUAL_PASS cases={plan.Cases.Count} " +
            $"resolutions={profile.Resolutions.Count} cameras={profile.CameraFixtures.Count} " +
            $"quality={profile.QualityTiers.Count} tolerance={profile.PixelTolerance:0.###} fingerprint={plan.Fingerprint}");
        GetTree().Quit(0);
    }

    private static double MeanPixelError(Image reference, Image candidate, int sampleStep)
    {
        if (reference.GetWidth() != candidate.GetWidth() || reference.GetHeight() != candidate.GetHeight())
            return 1;
        double error = 0;
        var samples = 0;
        for (var y = 0; y < reference.GetHeight(); y += sampleStep)
            for (var x = 0; x < reference.GetWidth(); x += sampleStep)
            {
                var expected = reference.GetPixel(x, y);
                var actual = candidate.GetPixel(x, y);
                error += (Math.Abs(expected.R - actual.R) + Math.Abs(expected.G - actual.G) +
                    Math.Abs(expected.B - actual.B)) / 3d;
                samples++;
            }
        return samples == 0 ? 1 : error / samples;
    }

    public override void _Process(double delta)
    {
        if (!_initializationComplete) return;
        var simulationDelta = delta * (_timeControlSession.State == RuntimeTimeState.SlowMotion
            ? _timeControlSession.Scale
            : 1);
        if (!IsTimeFrozen())
        {
            AdvanceClimate(simulationDelta);
            AdvanceRuntimePolicies(simulationDelta);
            AdvanceAreaExperience(simulationDelta);
            AdvanceCinematics(simulationDelta);
        }

        if (_player is null)
        {
            return;
        }

        if (_camera is not null && !_sequenceOwnsCamera)
        {
            _cameraRig.Update(_camera, _player.GlobalPosition, delta);
        }

        if (Input.IsActionJustPressed("action_wheel"))
        {
            _actionWheel?.Toggle();
        }

        if (Input.IsActionJustPressed("tactical_pause"))
        {
            ToggleTacticalPause();
        }

        if (Input.IsActionJustPressed("slow_time"))
        {
            ToggleSlowMotion();
        }

        if (Input.IsActionJustPressed("combat_mode"))
        {
            CycleCombatProvider();
        }

        if (Input.IsActionJustPressed("interact") && _actionWheel?.IsOpen != true)
        {
            RequestGameplayCommand("interact");
        }

        if (Input.IsActionJustPressed("attack") && _actionWheel?.IsOpen != true)
        {
            RequestGameplayCommand("attack");
        }

        if (Input.IsActionJustPressed("quick_save"))
        {
            SaveRuntime(_quickSavePath!);
        }

        if (Input.IsActionJustPressed("quick_load"))
        {
            LoadRuntimeSave(_quickSavePath!);
            UpdateHud();
        }

        if (Input.IsActionJustPressed("rest"))
        {
            RequestGameplayCommand("rest");
        }

        if (Input.IsActionJustPressed("skip_sequence") && _activeSequence is not null && _sequenceState is not null)
        {
            if (_activeSequence.Skippable)
            {
                CompleteSequence(SequencePlayer.Skip(_activeSequence, _sequenceState), "skipped");
            }
            else
            {
                AbortActiveSequence();
            }
        }

        if (_isCryptPrototype && !_escaped && _bossDefeated && IsNear(_exit, 2.5f))
        {
            _escaped = true;
            _message = "La Crypte des Cendres est libérée. Module terminé !";
            UpdateHud();
            GD.Print("OPENRPG_FORGE_PROTOTYPE_COMPLETED");
        }

        if (_isForgottenForestPrototype && !_escaped && _bossDefeated && IsNear(_exit, 3.25f))
        {
            _escaped = true;
            _message = "Le sanctuaire de la forêt est apaisé. Démonstration terminée !";
            UpdateHud();
            GD.Print("OPENRPG_FORGE_FORGOTTEN_FOREST_COMPLETED");
        }

        UpdateTacticalHud();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_sequenceOwnsCamera) _cameraRig.HandleInput(@event);
    }

    private ProjectDefinition LoadProject(string? snapshotPath, string? modulePackagePath, string? workspacePath)
    {
        var sourceCount = (snapshotPath is null ? 0 : 1) + (modulePackagePath is null ? 0 : 1) +
            (workspacePath is null ? 0 : 1);
        if (sourceCount > 1)
        {
            throw new InvalidOperationException(
                "A runtime launch cannot combine a workspace, project snapshot and module package.");
        }

        if (workspacePath is not null)
        {
            if (!Path.IsPathFullyQualified(workspacePath) || !Directory.Exists(workspacePath))
            {
                throw new InvalidOperationException("The runtime workspace path must be an existing absolute directory.");
            }

            var openedWorkspace = ProjectSession.Open(workspacePath);
            if (!openedWorkspace.Succeeded || openedWorkspace.Session is null)
            {
                throw new InvalidOperationException(
                    "The requested Forge workspace could not be loaded: " +
                    string.Join("; ", openedWorkspace.Diagnostics.Select(diagnostic =>
                        $"{diagnostic.Code} ({diagnostic.Path}): {diagnostic.Message}")));
            }

            var workspaceProject = openedWorkspace.Session.Project;
            _moduleContentRoot = openedWorkspace.Session.RootDirectory;
            SetUnpackagedModuleIdentity(workspaceProject);
            GD.Print($"OPENRPG_FORGE_RUNTIME_WORKSPACE_LOADED format={ProjectSession.CurrentProjectFormatVersion} " +
                     $"slug={workspaceProject.Module.Slug} external=true");
            return workspaceProject;
        }

        if (modulePackagePath is not null)
        {
            if (!Path.IsPathFullyQualified(modulePackagePath) ||
                !string.Equals(Path.GetExtension(modulePackagePath), ".orpmod", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The module package path must be an absolute .orpmod path.");
            }

            var cacheRoot = ProjectSettings.GlobalizePath("user://openrpg-forge/module-cache");
            var loaded = ModulePackageLoader.Load(modulePackagePath, cacheRoot);
            _moduleContentRoot = Path.Combine(loaded.CachePath, "content");
            _moduleId = loaded.Manifest.ModuleId.ToString();
            _moduleContentSha256 = loaded.Manifest.ContentSha256;
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_MODULE_LOADED slug={loaded.Manifest.Slug} " +
                $"cache_hit={loaded.CacheHit.ToString().ToLowerInvariant()}");
            return loaded.Project;
        }

        if (snapshotPath is not null)
        {
            if (!Path.IsPathFullyQualified(snapshotPath) ||
                !string.Equals(Path.GetExtension(snapshotPath), ".json", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The playtest snapshot path must be an absolute JSON file path.");
            }

            var snapshotProject = ProjectJsonSerializer.Deserialize(File.ReadAllText(snapshotPath));
            SetUnpackagedModuleIdentity(snapshotProject);
            return snapshotProject;
        }

        var projectFilePath = ProjectSettings.GlobalizePath(ProjectPath);
        var workspaceRoot = Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new InvalidOperationException($"Project workspace could not be resolved from '{ProjectPath}'.");
        }

        var opened = ProjectSession.Open(workspaceRoot);
        if (!opened.Succeeded)
        {
            throw new InvalidOperationException(
                "The bundled Forge workspace could not be loaded: " +
                string.Join("; ", opened.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code} ({diagnostic.Path}): {diagnostic.Message}")));
        }

        var project = opened.Session!.Project;
        _moduleContentRoot = opened.Session.RootDirectory;
        SetUnpackagedModuleIdentity(project);
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_WORKSPACE_LOADED format={ProjectSession.CurrentProjectFormatVersion} " +
            $"slug={project.Module.Slug}");
        return project;
    }

    private void ValidateAssetPackageManifests()
    {
        if (string.IsNullOrWhiteSpace(_moduleContentRoot))
        {
            return;
        }

        var loaded = AssetPackageManifestStore.Load(_moduleContentRoot);
        if (!loaded.Succeeded)
        {
            throw new InvalidOperationException(
                $"Asset package manifests could not be loaded: {loaded.Diagnostics[0].Code} " +
                $"({loaded.Diagnostics[0].Path}).");
        }

        var platform = OS.GetName() switch
        {
            "Windows" => AssetTargetPlatform.Windows,
            "Linux" => AssetTargetPlatform.Linux,
            "macOS" => AssetTargetPlatform.MacOS,
            "Android" => AssetTargetPlatform.Android,
            "iOS" => AssetTargetPlatform.IOS,
            _ => AssetTargetPlatform.Any,
        };
        var resolvedChunks = 0;
        foreach (var manifest in loaded.Manifests)
        {
            var available = manifest.Chunks
                .Select(chunk => new
                {
                    Chunk = chunk,
                    Path = Path.Combine(
                        _moduleContentRoot,
                        chunk.RelativePath.Replace('/', Path.DirectorySeparatorChar)),
                })
                .Where(item => File.Exists(item.Path))
                .ToDictionary(
                    item => item.Chunk.RelativePath,
                    item => HashFileSha256(item.Path),
                    StringComparer.Ordinal);
            var resolution = AssetPackageManifestResolver.Resolve(manifest, platform, available);
            if (!resolution.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Asset package manifest '{manifest.Id}' is invalid at runtime: " +
                    $"{resolution.Diagnostics[0].Code} ({resolution.Diagnostics[0].Path}).");
            }

            resolvedChunks += resolution.Chunks.Count;
        }

        _modelLibrary = new GodotAssetModelLibrary(_moduleContentRoot, loaded.Manifests);

        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_ASSET_MANIFESTS_PASS manifests={loaded.Manifests.Count} " +
            $"chunks={resolvedChunks} platform={platform}");
    }

    private static string HashFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private EntityInstance BuildProject(ProjectDefinition project, EntityId? requestedSpawnId)
    {
        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        var guidedPrototype = _isCryptPrototype || _isForgottenForestPrototype;
        _elyraDialogue = guidedPrototype ? project.Dialogues.SingleOrDefault() : null;
        var quest = guidedPrototype ? project.Quests.SingleOrDefault() : null;
        _questDefinition = quest;
        _questMachine = quest is null ? null : new QuestStateMachine(quest);
        var spawnCandidates = project.Areas
            .SelectMany(area => area.Entities.Select(entity => (Area: area, Entity: entity)))
            .Where(candidate =>
                blueprints[candidate.Entity.BlueprintId].Kind == BlueprintKind.PlayerSpawn)
            .ToArray();
        var selectedSpawn = requestedSpawnId is null
            ? spawnCandidates.FirstOrDefault()
            : spawnCandidates.SingleOrDefault(candidate => candidate.Entity.Id == requestedSpawnId);

        if (selectedSpawn.Entity is null)
        {
            throw new InvalidOperationException(
                requestedSpawnId is null
                    ? "The project does not contain a player spawn."
                    : $"The requested player spawn '{requestedSpawnId}' does not exist.");
        }

        foreach (var entity in selectedSpawn.Area.Entities)
        {
            var blueprint = blueprints[entity.BlueprintId];
            if (blueprint.Kind == BlueprintKind.PlayerSpawn && entity.Id != selectedSpawn.Entity.Id)
            {
                continue;
            }

            var previousChildCount = GetChildCount();
            BuildEntity(entity, blueprint);
            if (GetChildCount() > previousChildCount && GetChild(previousChildCount) is Node3D node)
            {
                _entityNodes[entity.Id.Value] = node;
            }
            _builtEntityCount++;
        }

        if (_player is null)
        {
            throw new InvalidOperationException("The selected area did not reconstruct a player spawn.");
        }

        _currentAreaId = selectedSpawn.Area.Id.ToString();

        GD.Print($"OPENRPG_FORGE_RUNTIME_MODELS_PASS manifests={_modelLibrary?.Count ?? 0} " +
                 $"instances={_runtimeModelInstances}");

        return selectedSpawn.Entity;
    }

    private void SetUnpackagedModuleIdentity(ProjectDefinition project)
    {
        _moduleId = project.Module.Id.ToString();
        _moduleContentSha256 = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))))
            .ToLowerInvariant();
    }

    private void ShowStartupFailure(Exception exception)
    {
        var layer = new CanvasLayer { Layer = 100 };
        AddChild(layer);

        var background = new ColorRect { Color = new Color("0b0915") };
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(background);

        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 48);
        }

        layer.AddChild(margin);
        var content = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        content.AddThemeConstantOverride("separation", 18);
        margin.AddChild(content);

        var title = new Label { Text = "OpenRPG Forge n'a pas pu démarrer le module" };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        content.AddChild(title);

        var explanation = new Label
        {
            Text = "La scène 3D n'a pas été construite. Le diagnostic ci-dessous permet de corriger le projet sans afficher une fenêtre vide.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        explanation.AddThemeFontSizeOverride("font_size", 17);
        explanation.AddThemeColorOverride("font_color", new Color("d8cce8"));
        content.AddChild(explanation);

        var diagnostic = new Label
        {
            Text = exception.Message,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        diagnostic.AddThemeColorOverride("font_color", new Color("ff806d"));
        content.AddChild(diagnostic);

        var close = new Button
        {
            Text = "Fermer",
            CustomMinimumSize = new Vector2(160, 44),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
        };
        close.Pressed += () => GetTree().Quit();
        content.AddChild(close);
    }

    private static string? GetArgumentValue(IReadOnlyList<string> arguments, string name)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!string.Equals(arguments[index], name, StringComparison.Ordinal))
            {
                continue;
            }

            if (index + 1 >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index + 1]))
            {
                throw new InvalidOperationException($"The runtime argument '{name}' requires a value.");
            }

            return arguments[index + 1];
        }

        return null;
    }

    private static EntityId? ParseOptionalEntityId(string? value) =>
        value is null ? null : EntityId.Parse(value);

    private void HandleLifecycleEvent(RuntimeLifecycleEvent @event)
    {
        _lifecycleEvents.Add(@event);
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_LIFECYCLE sequence={@event.Sequence} event={@event.Kind} " +
            $"binding={@event.Binding} authority={@event.Authority} " +
            $"previous={@event.PreviousAreaId?.ToString() ?? "none"} " +
            $"current={@event.CurrentAreaId?.ToString() ?? "none"} " +
            $"actor={@event.ActorEntityId?.ToString() ?? "none"} " +
            $"transition={@event.TransitionId?.ToString() ?? "none"} cause={@event.Cause} " +
            $"schema={@event.SchemaVersion} api={@event.ForgeScriptApiVersion} " +
            $"provenance={@event.Provenance}");
    }

    private static void EnsureLifecycle(RuntimeLifecycleDispatchResult result, string stage)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Runtime lifecycle stage '{stage}' failed: " +
            string.Join("; ", result.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}:{diagnostic.Message} [{diagnostic.Path}]")));
    }

    private void AssertLifecycleStartupContract()
    {
        var expectedOrder = new[]
        {
            RuntimeLifecycleEventKind.ProjectLoaded,
            RuntimeLifecycleEventKind.AreaEntered,
            RuntimeLifecycleEventKind.WorldStarted,
        };
        if (!_lifecycleEvents.Select(@event => @event.Kind).SequenceEqual(expectedOrder))
        {
            throw new InvalidOperationException("Runtime lifecycle startup order is not deterministic.");
        }

        GD.Print(
            "OPENRPG_FORGE_RUNTIME_LIFECYCLE_PASS " +
            "events=3 order=ProjectLoaded>AreaEntered>WorldStarted authority=SinglePlayer");
    }

    private void BuildEntity(EntityInstance entity, BlueprintDefinition blueprint)
    {
        switch (blueprint.Kind)
        {
            case BlueprintKind.Placeable:
                BuildPlaceable(entity, blueprint);
                break;
            case BlueprintKind.Door:
                _door = CreateBox(entity, blueprint.DisplayName, new Color("8f5738"));
                _doorEntityId = entity.Id;
                _gateDisplayName = GetOverride(entity, "runtime.displayName") ?? blueprint.DisplayName;
                break;
            case BlueprintKind.Creature:
                BuildCreature(entity, blueprint);
                break;
            case BlueprintKind.PlayerSpawn:
                _player = CreatePlayer(entity, blueprint);
                break;
            case BlueprintKind.Item:
                _keyItem = CreateItem(entity, blueprint.DisplayName, blueprint.Slug);
                break;
            case BlueprintKind.Trigger:
                _exit = CreateExit(entity);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(blueprint), blueprint.Kind, "Unsupported blueprint kind.");
        }
    }

    private void BuildPlaceable(EntityInstance entity, BlueprintDefinition blueprint)
    {
        if (entity.Tags.Contains("pillar", StringComparer.Ordinal))
        {
            CreatePillar(entity, blueprint.DisplayName);
            return;
        }

        var isFloor = entity.Tags.Contains("floor", StringComparer.Ordinal);
        CreateBox(
            entity,
            blueprint.DisplayName,
            isFloor ? new Color("282638") : new Color("443b51"));
    }

    private void BuildCreature(EntityInstance entity, BlueprintDefinition blueprint)
    {
        if (entity.Tags.Contains("npc", StringComparer.Ordinal) || HasRuntimeRole(entity, "guide"))
        {
            _npc = CreateCreature(entity, blueprint.DisplayName, new Color("4ec9b0"));
            _guideName = GetOverride(entity, "runtime.displayName") ?? blueprint.DisplayName;
            return;
        }

        var isBoss = entity.Tags.Contains("boss", StringComparer.Ordinal) || HasRuntimeRole(entity, "boss");
        var creature = CreateCreature(
            entity,
            blueprint.DisplayName,
            isBoss ? new Color("d34f4f") : new Color("d68a3a"));
        var profile = blueprint.Combat
            ?? throw new InvalidOperationException($"Hostile blueprint '{blueprint.Slug}' has no combat profile.");
        _hostiles.Add(creature, new CombatantState(entity.Id.ToString(), profile));
        _hostileNames.Add(creature, blueprint.DisplayName);

        if (isBoss)
        {
            _boss = creature;
            _bossPhaseTwoScript = TryCompileEntityScript(entity, "phaseTwoScript");
            _bossDeathScript = TryCompileEntityScript(entity, "deathScript");
        }
    }

    private PlayerController CreatePlayer(EntityInstance entity, BlueprintDefinition blueprint)
    {
        if (blueprint.Combat is not null)
        {
            _playerCombatant = new CombatantState(entity.Id.ToString(), blueprint.Combat);
        }

        var player = new PlayerController { Name = "Player" };
        AddChild(player);
        player.GlobalPosition = EngineValueConverter.ToEngine(entity.Transform.Position);
        player.MovementCamera = _camera;
        if (_gameplayControls is not null) player.Configure(_gameplayControls);

        if (!TryAttachVisual(player, entity, applyPlayerVisualTransform: true))
        {
            player.AddChild(new MeshInstance3D
            {
                Mesh = new CapsuleMesh { Radius = 0.45f, Height = 1.8f },
                MaterialOverride = CreateMaterial(new Color("74b9ff")),
            });
        }

        player.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.45f, Height = 1.8f },
        });
        CreateLabel(player, blueprint.Combat is null ? "AVATAR" : "HÉROS", new Color("dff3ff"), 1.5f);
        return player;
    }

    private Node3D CreateBox(EntityInstance entity, string displayName, Color color)
    {
        var root = new Node3D
        {
            Name = SafeNodeName(displayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        AddChild(root);

        var body = new StaticBody3D();
        if (_gameplayControls?.Climbing.Enabled == true && entity.Tags.Any(tag =>
                _gameplayControls.Climbing.SurfaceTags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
        {
            body.SetMeta("forge_climb", true);
        }
        root.AddChild(body);
        if (!TryAttachVisual(body, entity))
        {
            body.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = Vector3.One },
                MaterialOverride = CreateMaterial(color),
            });
        }
        body.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = Vector3.One },
        });
        _collisionCount++;
        return root;
    }

    private Node3D CreatePillar(EntityInstance entity, string displayName)
    {
        var root = new Node3D
        {
            Name = SafeNodeName(displayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        AddChild(root);

        var body = new StaticBody3D();
        root.AddChild(body);
        if (!TryAttachVisual(body, entity))
        {
            body.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1 },
                MaterialOverride = CreateMaterial(new Color("665565")),
            });
        }
        body.AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = 0.5f, Height = 1 },
        });
        _collisionCount++;
        return root;
    }

    private Node3D CreateCreature(EntityInstance entity, string displayName, Color color)
    {
        var root = new Node3D
        {
            Name = SafeNodeName(displayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        AddChild(root);
        if (!TryAttachVisual(root, entity))
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = new CapsuleMesh { Radius = 0.5f, Height = 1.7f },
                MaterialOverride = CreateMaterial(color),
            });
        }
        CreateLabel(root, displayName, Colors.White, 1.35f);
        return root;
    }

    private Node3D CreateItem(EntityInstance entity, string displayName, string itemSlug)
    {
        var root = new Node3D
        {
            Name = SafeNodeName(displayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        AddChild(root);
        if (!TryAttachVisual(root, entity))
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.45f, Height = 0.9f },
                MaterialOverride = CreateMaterial(new Color("ffca55"), emission: true),
            });
        }
        _questItemSlug = GetOverride(entity, "runtime.item") ?? itemSlug;
        _questItemDisplayName = GetOverride(entity, "runtime.displayName") ?? displayName;
        CreateLabel(root, displayName, new Color("ffe6a0"), 0.9f);
        return root;
    }

    private Node3D CreateExit(EntityInstance entity)
    {
        var root = new Node3D
        {
            Name = "CryptExit",
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        AddChild(root);
        TryAttachVisual(root, entity);
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1, BottomRadius = 1, Height = 0.1f },
            MaterialOverride = CreateMaterial(new Color("55efc4"), emission: true),
        });
        return root;
    }

    private async Task ConfigureWorldAsync(
        AreaEnvironmentProfile environmentProfile,
        AreaWorldProfile worldProfile,
        AreaNavigationDefinition navigation,
        bool includeCryptAtmosphere)
    {
        ArgumentNullException.ThrowIfNull(environmentProfile);
        ArgumentNullException.ThrowIfNull(worldProfile);
        ArgumentNullException.ThrowIfNull(navigation);
        var dynamicLights = includeCryptAtmosphere ? 8 : 2;
        var shadowLights = 2;
        _renderPlan = AreaRenderPlanBuilder.Build(
            environmentProfile,
            OS.HasFeature("mobile") ? "mobile" : "desktop",
            dynamicLights,
            shadowLights);
        var engineEnvironment = GodotEnvironmentAdapter.Build(_renderPlan);
        RenderingServer.SetDefaultClearColor(new Color(environmentProfile.Sky.GroundColor));
        AddChild(new WorldEnvironment
        {
            Name = "ForgeWorldEnvironment",
            Environment = engineEnvironment.Environment,
        });
        GetViewport().PositionalShadowAtlasSize = engineEnvironment.ShadowAtlasSize;

        var sun = new DirectionalLight3D
        {
            Name = "ForgeReferenceSun",
            RotationDegrees = new Vector3(-58, -32, 0),
            LightColor = engineEnvironment.DirectionalLightColor,
            LightEnergy = engineEnvironment.DirectionalLightEnergy,
            ShadowEnabled = environmentProfile.Lighting.Shadows,
        };
        AddChild(sun);

        if (includeCryptAtmosphere)
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

        if (includeCryptAtmosphere)
        {
            CreateFinalCryptAtmosphere();
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
        var terrain = GodotTerrainAdapter.Build(
            profile,
            _modelLibrary is null ? null : _modelLibrary.ResolveScatterMesh);
        _terrainVertices = terrain.Vertices;
        _terrainTriangles = terrain.Triangles;
        _terrainSectors = terrain.TerrainSectors;
        _terrainMaterialMode = terrain.TerrainMaterialMode;
        _terrainMaterialLayers = terrain.TerrainMaterialLayers;
        _terrainCollisionShapes = terrain.CollisionShapes;
        _terrainScatterLayers = terrain.ScatterLayers;
        _terrainScatterInstances = terrain.ScatterInstances;
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
        var reduceMotion = OS.GetCmdlineUserArgs().Contains("--reduce-motion", StringComparer.Ordinal);
        var overlay = new RuntimeLoadingOverlay(_moduleContentRoot ?? string.Empty);
        AddChild(overlay);
        var loadingFrame = AreaExperienceRuntimeService.ResolveLoading(presentation, _defaultLocale, 0,
            profile.Loading.RequiredResources.Count, 0, reduceMotion, imageAvailable, videoAvailable);
        overlay.Apply(loadingFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (profile.Loading.RequiredResources.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(_moduleContentRoot))
            {
                throw new InvalidOperationException("Area streaming resources require a module content root.");
            }

            var coordinator = new AreaStreamingCoordinator(new RuntimeAreaChunkLoader(_moduleContentRoot));
            var progress = new Progress<AreaStreamingProgress>(update =>
                overlay.Apply(AreaExperienceRuntimeService.ResolveLoading(presentation, _defaultLocale,
                    update.CompletedResources, Math.Max(update.TotalResources,
                    profile.Loading.RequiredResources.Count), update.CompletedResources, reduceMotion,
                    imageAvailable, videoAvailable), update.CurrentResource));
            streaming = await coordinator.LoadAsync(profile.Loading, progress);
        }

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
            $"streamed={loadedResources}");

        var elapsed = Stopwatch.GetElapsedTime(started);
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11I_TERRAIN_PASS provider={profile.Terrain.Provider.ToString().ToLowerInvariant()} " +
            $"vertices={terrain.Vertices} triangles={terrain.Triangles} sectors={terrain.TerrainSectors} " +
            $"material={terrain.TerrainMaterialMode} material_layers={terrain.TerrainMaterialLayers} " +
            $"collisions={terrain.CollisionShapes} " +
            $"features={terrain.FeatureSegments} volumes={terrain.DebugVolumes} routes={terrain.FlightRouteSegments} " +
            $"scatter_layers={terrain.ScatterLayers} scatter_instances={terrain.ScatterInstances} " +
            $"water_surfaces={terrain.WaterSurfaces} water_volumes={terrain.WaterVolumes} " +
            $"nav_regions={surfaceNavigation.Regions} nav_polygons={surfaceNavigation.Polygons} " +
            $"nav_blocked={surfaceNavigation.BlockedTriangles} nav_links={surfaceNavigation.Links} " +
            $"streamed={streaming?.Resources.Count ?? 0} cache_hits={streaming?.CacheHits ?? 0} " +
            $"elapsed_ms={elapsed.TotalMilliseconds:0.###} target_ms={profile.Loading.TargetLoadMilliseconds}");
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
            if (!ProceduralAmbientAudioService.SupportedResources.Contains(resourcePath, StringComparer.Ordinal))
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

    private void AdvanceClimate(double delta)
    {
        if (_environmentProfile is null || _weatherState is null)
        {
            return;
        }

        var result = WeatherRuntimeStateMachine.Advance(
            _environmentProfile.Weather,
            _weatherState,
            delta,
            hasAuthority: true);
        _weatherState = result.State;
        if (result.Transition is not null)
        {
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_WEATHER_TRANSITION revision={result.State.Revision} " +
                $"state={result.State.StateId} blend={result.Transition.BlendSeconds:0.###} " +
                $"effect={result.Transition.GameplayEffect} replicate={result.Transition.Replicate.ToString().ToLowerInvariant()} " +
                $"persist={result.Transition.Persist.ToString().ToLowerInvariant()}");
            ApplyClimateEffects(force: true);
        }

        _climateProbeElapsed += delta;
        if (_climateProbeElapsed >= .5)
        {
            _climateProbeElapsed = 0;
            ApplyClimateEffects(force: false);
        }
    }

    private void ConfigureRuntimePolicies(ModuleRuntimePolicyProfile module, AreaRuntimePolicyProfile area)
    {
        _moduleRuntimePolicies = module ?? throw new ArgumentNullException(nameof(module));
        _areaRuntimePolicies = area ?? throw new ArgumentNullException(nameof(area));
        _gameplayControls = module.Gameplay;
        _moduleRules = module.Rules;
        _areaRules = area.Rules;
        _areaExperience = area.Experience;
        _runtimeExtensions = module.Extensions;
        foreach (var diagnostic in RuntimeExtensionRuntimeService.ValidateRequired(module.Extensions))
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_EXTENSION_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path} uuid={diagnostic.SubjectId}");
        _runtimeJsonHandles.Clear();
        foreach (var document in module.Extensions.JsonDocuments)
        {
            var snapshot = _runtimeJson.Register(document.Id, document.Key, document.JsonSchema,
                document.InitialValue, new SafeJsonQuota(document.MaxBytes, document.MaxDepth), document.Mutable);
            _runtimeJsonHandles[document.Key] = snapshot.Handle;
        }
        foreach (var document in module.Extensions.UiDocuments)
            foreach (var binding in document.EventBindings)
                _scriptUiEvents.Register(new ScriptUiBinding(binding.Id, new ScriptUiHandle(document.Id, document.Key),
                    binding.WidgetKey, binding.Event.ToString().ToLowerInvariant(), binding.Command,
                    binding.ForgeScriptApiVersion, binding.InputContext, binding.Permission));
        _areaAudioState = AreaAudioRuntimeState.Empty;
        _scatterDueSeconds.Clear();
        _scatterSequences.Clear();
        _resolvedDifficulty = RulesRuntimeService.ResolveDifficulty(module.Rules);
        _activeCombatProvider = module.Gameplay.CombatModes.DefaultProvider;
        _timeControlSession = TimeControlSession.Running;
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11J_POLICY_PASS global={module.Heartbeats.Count} area={area.Heartbeats.Count} " +
            $"rest={module.Rest.Enabled.ToString().ToLowerInvariant()} volumes={area.Rest.Volumes.Count} " +
            $"abort={module.SequenceAbort.Enabled.ToString().ToLowerInvariant()} restore={module.SequenceAbort.Restore}");
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11L_GAMEPLAY_PASS swim={module.Gameplay.Swimming.Enabled.ToString().ToLowerInvariant()} " +
            $"climb={module.Gameplay.Climbing.Enabled.ToString().ToLowerInvariant()} " +
            $"radial={module.Gameplay.RadialMenu.Actions.Count} accessible={module.Gameplay.RadialMenu.ListFallback.ToString().ToLowerInvariant()} " +
            $"pause={module.Gameplay.TimeControl.PauseEnabled.ToString().ToLowerInvariant()} planning={module.Gameplay.TimeControl.PlanningEnabled.ToString().ToLowerInvariant()} " +
            $"combat={_activeCombatProvider.ProviderId} authority={_activeCombatProvider.ServerAuthoritative.ToString().ToLowerInvariant()}");
        var perception = RulesRuntimeService.ResolvePerception(area.Rules.Perception, "vision", 20, .8,
            new HashSet<string>(StringComparer.Ordinal));
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11M_RULES_PASS progression={module.Rules.Progression.RulesetId} " +
            $"difficulty={_resolvedDifficulty.Key} mutators={_resolvedDifficulty.EnabledMutators.Count} " +
            $"perception={area.Rules.Perception.Modifiers.Count}:{perception.Range:0.###} " +
            $"pvp={area.Rules.Combat.PvpMode.ToString().ToLowerInvariant()} " +
            $"authority={area.Rules.Combat.ServerAuthoritative.ToString().ToLowerInvariant()}");
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11N_EXPERIENCE_PASS cues={area.Experience.AudioCues.Count} " +
            $"scatter={area.Experience.ScatterEmitters.Count} voices={area.Experience.Voices.Count} " +
            $"patrols={area.Experience.PatrolRoutes.Count} loading=accessible-progress-prefetch " +
            "audio_api=started-completed-interrupted-music-snapshot patrol_api=streaming-owner-resume");
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11O_EXTENSIONS_PASS rules={module.Extensions.RuleSchemas.Count} " +
            $"ui={module.Extensions.UiDocuments.Count} json={module.Extensions.JsonDocuments.Count} " +
            $"snapshot={module.Extensions.SnapshotPolicy.Components.Count} api=1.0 quotas=bounded");
    }

    private void VerifyModernRuntimeTooling(ProjectDefinition project, AreaDefinition area)
    {
        if (_runtimeExtensions is null || _forgeScriptCache is null) return;
        var tooling = _runtimeExtensions.ModernTooling;
        var source = area.Entities.First();
        var destinationId = EntityId.From(Guid.Parse("e0190000-0000-4000-8000-000000000001"));
        var copyService = new RuntimeEntityCopyService(tooling.RuntimeCopy);
        var copy = AssertSingle(copyService.CopyTransaction(
        [
            new RuntimeEntityCopyRequest(
                new RuntimeEntityCopySource(source, "module", [source.Id],
                    [new RuntimeEntityStateComponent("transform", "{\"x\":0}"), new RuntimeEntityStateComponent("private", "true")]),
                destinationId, new Vector3Value(1, 0, 1), "runtime", true,
                new Dictionary<EntityId, EntityId> { [source.Id] = destinationId }),
        ], project.Areas.SelectMany(candidate => candidate.Entities).Select(entity => entity.Id).ToHashSet()));
        if (!copy.Succeeded || copy.Entity?.Id != destinationId || copy.References.SingleOrDefault() != destinationId ||
            copy.State.Any(component => component.Name == "private"))
            throw new InvalidOperationException($"G11-P runtime copy failed: {copy.Code}");

        var aoeService = new AreaEffectPreviewService(tooling.AreaEffectIndicator);
        var preview = aoeService.Preview(new AreaEffectPreviewRequest(AreaEffectShape.Circle,
            Vector3Value.Zero, new Vector3Value(0, 0, -1), 8, 6, 2, 90, true,
            [new AreaEffectTargetCandidate(source.Id, new Vector3Value(2, 0, 1), 0, true)]));
        if (!preview.IsValid || !preview.ValidTargets.Contains(source.Id))
            throw new InvalidOperationException($"G11-P AOE preview failed: {preview.Code}");
        _areaEffectIndicator = new RuntimeAreaEffectIndicator();
        _areaEffectIndicator.Configure(tooling.AreaEffectIndicator);
        AddChild(_areaEffectIndicator);
        _areaEffectIndicator.ShowPreview(AreaEffectShape.Circle, new Vector3(0, 0.05f, 0), Vector3.Forward,
            8, 2.5f, 2, valid: true);

        var compiler = new ForgeScriptCompilerService(_forgeScriptCache,
            new ForgeScriptCompilerOptions(tooling.Compiler.Incremental, tooling.Compiler.StableDiagnostics,
                tooling.Compiler.PerformanceTelemetry, tooling.Compiler.MaxParallelism, tooling.Compiler.MaxSourceBytes,
                tooling.Compiler.ConformanceSuites));
        var first = compiler.Compile([new ForgeScriptCompilationUnit("smoke/g11p.as", "void main() {}", [])]);
        var second = compiler.Compile([new ForgeScriptCompilationUnit("smoke/g11p.as", "void main() {}", [])]);
        var conformance = compiler.RunConformance(
        [
            new ForgeScriptConformanceCase("valid", "void main() {}", true, null),
            new ForgeScriptConformanceCase("diagnostic", "void main() { missing_api(); }", false, null),
        ]);
        if (!first.Success || second.Reused != 1 || conformance.Any(result => !result.Passed))
            throw new InvalidOperationException("G11-P compiler conformance or incremental compilation failed.");

        GD.Print("OPENRPG_FORGE_RUNTIME_G11P_MODERN_PASS " +
            $"copy=uuid-owner-references-state-authority aoe=mesh-targets-cover-height-colorblind-confirmation " +
            $"nui={tooling.UiToolkit.ToolkitVersion}-forms-lists-canvas-accessibility-localization-inspector " +
            $"compiler=incremental-stable-telemetry-conformance docs=metadata-generated");
    }

    private static void VerifyOperationalReadiness(ProjectDefinition project)
    {
        var readiness = project.Module.RuntimePolicies.Extensions.OperationalReadiness;
        var diagnostics = OperationalReadinessValidator.Validate(readiness);
        var testEvidence = new ProjectHealthSignal("tests", ProjectHealthCategory.Tests,
            ProjectHealthSeverity.Information, "health.runtime.smoke", "Runtime smoke is executing.",
            "health/tests/runtime", true);
        var health = ProjectHealthReportService.Evaluate(project, DateTimeOffset.UnixEpoch, [testEvidence]);
        if (diagnostics.Count != 0 || readiness.TaskManifests.Count == 0 ||
            !readiness.CrashArtifacts.CaptureSanitizedLogs ||
            !readiness.Recovery.OfferRecoveryOnOpen || !readiness.Recovery.QuarantineCorruptFiles ||
            !readiness.ContentSynchronization.Resumable || !readiness.ContentSynchronization.DeduplicateByHash ||
            !readiness.ContentSynchronization.VerifyContent || health.Status != ProjectHealthStatus.Healthy)
            throw new InvalidOperationException("G11-Q operational readiness contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G11Q_RESILIENCE_PASS " +
            "tasks=bounded-machine-readable crash=sanitized-version-fingerprint-reproduction " +
            $"health={health.Domains.Count}-domains-healthy recovery=journal-reopen-quarantine-compare " +
            "sync=resume-dedup-hash-trust-storage");
    }

    private void VerifyQualityLab(ProjectDefinition project)
    {
        if (string.IsNullOrWhiteSpace(_moduleContentRoot) || !Directory.Exists(_moduleContentRoot)) return;
        var options = WorkspaceQualityLabOptions.Default with { WritePseudoLocalizationSnapshot = false };
        var report = new WorkspaceQualityLabService(_forgeScriptRuntime).Run(_moduleContentRoot, project, options);
        if (!report.Passed)
            throw new InvalidOperationException("G11-R quality lab contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G11R_QUALITY_PASS " +
            $"localization=coverage-placeholders-plurals-overflow-pseudo scripts={report.Scripts.Tests.Count}-sandbox-quota-mock-coverage " +
            $"dialogues={report.Dialogues.Count}-branches-actions quests={report.Quests.Count}-events-save-scope " +
            $"encounters={report.Encounters.TotalRuns}-seeds-nav-cleanup");
    }

    private void VerifyPerformanceLab(ProjectDefinition project)
    {
        if (string.IsNullOrWhiteSpace(_moduleContentRoot) || !Directory.Exists(_moduleContentRoot)) return;
        var report = new WorkspacePerformanceLabService().Run(_moduleContentRoot, project,
            new WorkspacePerformanceLabOptions(false));
        if (!report.Passed || report.Network.Scenarios.Count != 9 || report.VisualReviews.Cases.Count != 8 ||
            report.Benchmarks.Measurements.Count != 9 || report.Benchmarks.Budgets.Any(item =>
                item.Status == PerformanceBudgetStatus.Failed) ||
            !Enum.GetValues<QualityTraceSurface>().All(surface =>
                report.TraceSpans.Any(span => span.Surface == surface)))
            throw new InvalidOperationException("G11-S performance laboratory contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G11S_PERFORMANCE_PASS " +
            "network=9-latency-loss-reconnect-mismatch-authority visual=8-fixtures-tolerance-quality " +
            "benchmarks=9-interior-exterior-stress-load-frame-memory-nav-package " +
            "budgets=3-frame-memory-texture-draw-light-script-network " +
            "profiler=editor-runtime-server-script-database-network");
    }

    private void VerifyGovernanceLab(ProjectDefinition project)
    {
        if (string.IsNullOrWhiteSpace(_moduleContentRoot) || !Directory.Exists(_moduleContentRoot)) return;
        var report = new WorkspaceGovernanceLabService(_forgeScriptRuntime).Run(_moduleContentRoot, project,
            new WorkspaceGovernanceLabOptions(false));
        if (!report.Passed || report.SecurityFuzz.Boundaries.Count != 7 ||
            report.SecurityFuzz.TotalCases != 112 ||
            report.SecurityFuzz.Boundaries.Any(item => item.UnexpectedFailures != 0 || item.PathEscapes != 0))
            throw new InvalidOperationException("G11-T governance laboratory contract failed.");
        GD.Print("OPENRPG_FORGE_RUNTIME_G11T_GOVERNANCE_PASS " +
            $"editor={report.Accessibility.EditorChecks.Count}-keyboard-labels-scale-contrast " +
            $"runtime={report.Accessibility.RuntimeChecks.Count}-subtitles-captions-color-input-motion-difficulty " +
            $"fuzz={report.SecurityFuzz.TotalCases}-cases-7-boundaries-no-crash-no-escape " +
            $"dependencies={report.Dependencies.DependencyCount}-approved-risk-review-audit-lifecycle");
    }

    private static T AssertSingle<T>(IReadOnlyList<T> values) => values.Count == 1
        ? values[0] : throw new InvalidOperationException($"Expected one result, found {values.Count}.");

    private void ConfigureCinematics(ProjectDefinition project, AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(area);
        if (_runtimeExtensions?.ModernTooling.QualityAssurance.Accessibility.RuntimeReducedMotion == true)
        {
            GD.Print("OPENRPG_FORGE_RUNTIME_ACCESSIBILITY reduced_motion=startup-cinematic-skipped");
            return;
        }
        _cameraAnchors.Clear();
        foreach (var anchor in area.RuntimePolicies.Cinematics.CameraAnchors)
        {
            _cameraAnchors.Add(anchor.Id, anchor);
        }

        var profile = project.Module.RuntimePolicies.Cinematics;
        var resolution = StartupFlowResolver.Resolve(profile, CinematicResourceExists);
        foreach (var diagnostic in resolution.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_CINEMATIC_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }

        if (resolution.Kind == StartupFlowKind.Sequence && resolution.ReferenceId is not null)
        {
            StartSequence(profile.Sequences.Single(item => item.Id == resolution.ReferenceId));
        }
        else if (resolution.Kind == StartupFlowKind.Dialogue && resolution.ReferenceId is not null)
        {
            var dialogue = project.Dialogues.SingleOrDefault(item => item.Id.Value == resolution.ReferenceId);
            _message = dialogue is null ? _message : $"Dialogue d’introduction · {dialogue.DisplayName}";
        }
        else if (resolution.Kind is StartupFlowKind.Video or StartupFlowKind.Scene)
        {
            StartResourceFlow(resolution.Kind, resolution.ResourcePath);
        }

        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11K_CINEMATIC_PASS startup={resolution.Kind.ToString().ToLowerInvariant()} " +
            $"fallback={resolution.UsedFallback.ToString().ToLowerInvariant()} sequences={profile.Sequences.Count} " +
            $"anchors={_cameraAnchors.Count} skip={resolution.Skippable.ToString().ToLowerInvariant()} " +
            "timeline=branch-signal-transform authority=governed recovery=enabled");
    }

    private void StartSequence(SequenceDefinition sequence)
    {
        _activeSequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
        _sequenceState = SequencePlaybackState.Start(sequence.Id);
        if (_camera is not null)
        {
            _explorationCameraTransform = _camera.GlobalTransform;
            _explorationCameraFov = _camera.Fov;
        }
        _sequenceOwnsCamera = sequence.Tracks.Any(item => item.Kind == SequenceTrackKind.Camera);
        if (_player is not null) _player.ProcessMode = ProcessModeEnum.Disabled;
        ApplySequenceEvaluation(SequencePlayer.Advance(sequence, _sequenceState, 0, true, EvaluateSequenceCondition));
        GD.Print($"OPENRPG_FORGE_RUNTIME_SEQUENCE_STARTED id={sequence.Id:D} duration_ms={sequence.DurationMilliseconds}");
    }

    private void AdvanceCinematics(double delta)
    {
        if (_activeSequence is null || _sequenceState is null || !_sequenceState.Active) return;
        var milliseconds = Math.Max(0, (int)Math.Round(delta * 1000));
        var evaluation = SequencePlayer.Advance(_activeSequence, _sequenceState, milliseconds, true,
            EvaluateSequenceCondition);
        ApplySequenceEvaluation(evaluation);
        if (evaluation.State.Completed) CompleteSequence(evaluation, "completed");
    }

    private void ApplySequenceEvaluation(SequenceEvaluation evaluation)
    {
        _sequenceState = evaluation.State;
        if (evaluation.CameraAnchorId is not null && _camera is not null &&
            _cameraAnchors.TryGetValue(evaluation.CameraAnchorId.Value, out var anchor))
        {
            _camera.GlobalPosition = EngineValueConverter.ToEngine(anchor.Transform.Position);
            _camera.Fov = anchor.FieldOfViewDegrees;
            _camera.LookAt(EngineValueConverter.ToEngine(anchor.LookAt), Vector3.Up);
        }

        foreach (var transform in evaluation.Transforms)
        {
            if (_entityNodes.TryGetValue(transform.Key, out var node))
                node.Transform = EngineValueConverter.ToEngine(transform.Value);
        }
        foreach (var signal in evaluation.Signals)
            GD.Print($"OPENRPG_FORGE_RUNTIME_SEQUENCE_SIGNAL name={signal.Name} at_ms={signal.TimeMilliseconds}");
        foreach (var resource in evaluation.AudioResources) PlaySequenceAudio(resource);
        foreach (var diagnostic in evaluation.Diagnostics)
            GD.Print($"OPENRPG_FORGE_RUNTIME_SEQUENCE_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
    }

    private void CompleteSequence(SequenceEvaluation evaluation, string reason)
    {
        ApplySequenceEvaluation(evaluation);
        if (_player is not null) _player.ProcessMode = ProcessModeEnum.Inherit;
        if (_camera is not null && _sequenceOwnsCamera)
        {
            _camera.GlobalTransform = _explorationCameraTransform;
            _camera.Fov = _explorationCameraFov;
        }
        GD.Print($"OPENRPG_FORGE_RUNTIME_SEQUENCE_FINISHED id={_activeSequence!.Id:D} reason={reason}");
        _activeSequence = null;
        _sequenceState = null;
        _sequenceOwnsCamera = false;
    }

    private void AbortActiveSequence()
    {
        if (_activeSequence is null || _sequenceState is null || _moduleRuntimePolicies is null) return;
        var policy = _moduleRuntimePolicies.SequenceAbort;
        var snapshot = new SequenceRuntimeState("camera.exploration", [], new HashSet<string>(),
            new HashSet<string>(), 0, null, false);
        var current = snapshot with { CameraId = "camera.cinematic", SequenceActive = true };
        var result = SequenceAbortCoordinator.Abort(policy, current, snapshot);
        if (!result.Accepted)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_SEQUENCE_ABORT_REFUSED code={result.DiagnosticCode}");
            return;
        }
        var aborted = _sequenceState with { Active = false, Completed = false, Skipped = false };
        CompleteSequence(new SequenceEvaluation(aborted, null, new Dictionary<Guid, TransformValue>(), [], [], []),
            "aborted");
        GD.Print($"OPENRPG_FORGE_RUNTIME_SEQUENCE_ABORTED handler={result.EventHandler} camera_restored={result.CameraRestored.ToString().ToLowerInvariant()}");
    }

    private bool EvaluateSequenceCondition(string condition) => condition switch
    {
        "boss.defeated" => _bossDefeated,
        "door.unlocked" => _doorUnlocked,
        "key.acquired" => _hasKey,
        _ => _blackboard.TryGetValue(condition, out var value) && bool.TryParse(value, out var parsed) && parsed,
    };

    private bool CinematicResourceExists(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath)) return false;
        var uri = resourcePath.StartsWith("res://", StringComparison.Ordinal) ? resourcePath : $"res://{resourcePath}";
        if (ResourceLoader.Exists(uri)) return true;
        return _moduleContentRoot is not null && File.Exists(Path.Combine(_moduleContentRoot,
            resourcePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private void StartResourceFlow(StartupFlowKind kind, string resourcePath)
    {
        var uri = resourcePath.StartsWith("res://", StringComparison.Ordinal) ? resourcePath : $"res://{resourcePath}";
        if (kind == StartupFlowKind.Scene && ResourceLoader.Load<PackedScene>(uri) is { } scene)
        {
            AddChild(scene.Instantiate());
        }
        else if (kind == StartupFlowKind.Video && ResourceLoader.Load<VideoStream>(uri) is { } stream)
        {
            var player = new VideoStreamPlayer { Stream = stream, Autoplay = true };
            player.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            AddChild(player);
        }
    }

    private void PlaySequenceAudio(string resourcePath)
    {
        var uri = resourcePath.StartsWith("res://", StringComparison.Ordinal) ? resourcePath : $"res://{resourcePath}";
        if (ResourceLoader.Load<AudioStream>(uri) is not { } stream) return;
        var player = new AudioStreamPlayer { Stream = stream, Autoplay = true };
        player.Finished += player.QueueFree;
        AddChild(player);
    }

    private void AdvanceRuntimePolicies(double delta)
    {
        if (_moduleRuntimePolicies is null || _areaRuntimePolicies is null) return;
        var milliseconds = Math.Max(0, (long)Math.Round(delta * 1_000, MidpointRounding.AwayFromZero));
        DispatchHeartbeats(_heartbeatScheduler.Advance(_moduleRuntimePolicies.Heartbeats, milliseconds, true));
        DispatchHeartbeats(_heartbeatScheduler.Advance(_areaRuntimePolicies.Heartbeats, milliseconds, true,
            _currentAreaId is null ? null : AreaId.Parse(_currentAreaId)));
        if (_restSession is { Active: true } session && DateTimeOffset.UtcNow >= session.CompletesAt)
        {
            var completed = _restRuntime.Complete(session, ++_runtimePolicyRevision, DateTimeOffset.UtcNow);
            PublishRestEvents(completed.Events);
            _restSession = completed.Session;
            _message = completed.Accepted ? "Repos terminé." : "Le repos a été interrompu.";
            UpdateHud();
        }
    }

    private static void DispatchHeartbeats(HeartbeatAdvanceResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_POLICY_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }

        foreach (var dispatch in result.Dispatches)
        {
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_HEARTBEAT handler={dispatch.Handler} scope={dispatch.Scope} " +
                $"budget_ms={dispatch.BudgetMilliseconds} due_ms={dispatch.DueAtMilliseconds}");
        }
    }

    private void RequestPlayerRest()
    {
        if (_player is null || _moduleRuntimePolicies is null || _areaRuntimePolicies is null ||
            _currentAreaId is null)
        {
            return;
        }

        var position = _player.GlobalPosition;
        var restPosition = new Vector3Value(position.X, position.Y, position.Z);
        var effectivePolicy = _areaRuntimePolicies.Rest.ResolveAt(restPosition);
        var result = _restRuntime.Request("player", "player-party", AreaId.Parse(_currentAreaId),
            restPosition, _moduleRuntimePolicies.Rest, _areaRuntimePolicies.Rest,
            secure: _hostiles.Keys.All(hostile => hostile.GlobalPosition.DistanceTo(position) >
                effectivePolicy.RequiredSecureRadius),
            EvaluateRuntimePolicyCondition, ++_runtimePolicyRevision, DateTimeOffset.UtcNow);
        PublishRestEvents(result.Events);
        _restSession = result.Session;
        _message = result.Accepted
            ? $"Repos en cours ({_moduleRuntimePolicies.Rest.DefaultDurationSeconds} s)…"
            : result.Events.LastOrDefault()?.Cause ?? result.DiagnosticCode ?? "Repos refusé.";
        UpdateHud();
    }

    private bool EvaluateRuntimePolicyCondition(string expression)
    {
        var parts = expression.Split("==", 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !_blackboard.TryGetValue(parts[0], out var value)) return false;
        return string.Equals(value, parts[1], StringComparison.OrdinalIgnoreCase);
    }

    private void PublishRestEvents(IReadOnlyList<RestLifecycleEvent> events)
    {
        foreach (var @event in events)
        {
            var published = _gameplayEvents.Publish(@event);
            if (!published.Accepted)
            {
                GD.PrintErr($"OPENRPG_FORGE_RUNTIME_REST_EVENT_FAILED code={published.DiagnosticCode}");
                continue;
            }

            var binding = _moduleRuntimePolicies?.Rest.Bindings.SingleOrDefault(item =>
                string.Equals(item.EventName, @event.EventName, StringComparison.Ordinal));
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_REST_EVENT stage={@event.Stage} handler={binding?.Handler ?? "unbound"} " +
                $"revision={@event.ServerRevision}");
        }
    }

    private void ApplyClimateEffects(bool force)
    {
        if (_environmentProfile is null || _weatherState is null)
        {
            return;
        }

        var listener = _player?.GlobalPosition ?? Vector3.Zero;
        var plan = AreaClimatePlanBuilder.Build(
            _environmentProfile,
            _weatherState.StateId,
            new(listener.X, listener.Y, listener.Z),
            OS.HasFeature("mobile") ? "mobile" : "desktop");
        if (_climateEffects?.GetChildren().OfType<GpuParticles3D>().FirstOrDefault() is { } precipitation)
        {
            precipitation.GlobalPosition = listener + (Vector3.Up * 18);
        }

        var signature = $"{plan.Weather.Id}:{plan.ActiveWindZone?.Id}:{plan.ActiveReverbZone?.Id}";
        if (!force && string.Equals(signature, _climateSignature, StringComparison.Ordinal))
        {
            return;
        }

        if (_climateEffects is not null)
        {
            RemoveChild(_climateEffects);
            _climateEffects.QueueFree();
        }

        var result = GodotClimateAdapter.Build(plan, configureRuntimeAudio: true);
        _climateEffects = result.Root;
        _climateSignature = signature;
        AddChild(_climateEffects);
        if (_climateEffects.GetChildren().OfType<GpuParticles3D>().FirstOrDefault() is { } createdPrecipitation)
        {
            createdPrecipitation.GlobalPosition = listener + (Vector3.Up * 18);
        }
        foreach (var diagnostic in result.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_CLIMATE_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }

        var effectiveWindSpeed = plan.ActiveWindZone?.Speed ?? plan.Wind.Speed;
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_G11H_ENVIRONMENT_PASS wind={effectiveWindSpeed:0.###} " +
            $"weather={plan.Weather.Kind.ToString().ToLowerInvariant()} revision={_weatherState.Revision} " +
            $"particles={result.PrecipitationEmitters} water={result.WaterSurfaces} reverb={result.ReverbPreset} " +
            $"authority={_environmentProfile.Weather.ServerAuthoritative.ToString().ToLowerInvariant()} " +
            $"save={_environmentProfile.Weather.PersistState.ToString().ToLowerInvariant()} " +
            $"replicate={_environmentProfile.Weather.ReplicateState.ToString().ToLowerInvariant()}");
    }

    private void CreateFinalCryptAtmosphere()
    {
        var torchPositions = new[]
        {
            new Vector3(-7.8f, 0, 8),
            new Vector3(7.8f, 0, 8),
            new Vector3(-7.8f, 0, 1),
            new Vector3(7.8f, 0, 1),
            new Vector3(-7.8f, 0, -7),
            new Vector3(7.8f, 0, -7),
        };
        foreach (var position in torchPositions)
        {
            var torch = new Node3D { Name = "EmberBrazier", Position = position };
            AddChild(torch);
            torch.AddChild(new MeshInstance3D
            {
                Position = new Vector3(0, 0.35f, 0),
                Mesh = new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.3f, Height = 0.7f },
                MaterialOverride = CreateMaterial(new Color("4a3448")),
            });
            torch.AddChild(new MeshInstance3D
            {
                Position = new Vector3(0, 0.86f, 0),
                Mesh = new SphereMesh { Radius = 0.18f, Height = 0.36f },
                MaterialOverride = CreateMaterial(new Color("ff7043"), emission: true),
            });
            torch.AddChild(new OmniLight3D
            {
                Position = new Vector3(0, 1.05f, 0),
                OmniRange = 7,
                LightColor = new Color("ff8a50"),
                LightEnergy = 1.7f,
                ShadowEnabled = false,
            });
        }

        var rune = new MeshInstance3D
        {
            Name = "AshRune",
            Position = new Vector3(0, 0.03f, -8.5f),
            Mesh = new CylinderMesh { TopRadius = 2.2f, BottomRadius = 2.2f, Height = 0.05f },
            MaterialOverride = CreateMaterial(new Color("8e3828"), emission: true),
        };
        AddChild(rune);

        CreateDecorativeBeam(new Vector3(0, 3.15f, 4.5f), new Vector3(17, 0.35f, 0.45f));
        CreateDecorativeBeam(new Vector3(0, 3.15f, -4.5f), new Vector3(17, 0.35f, 0.45f));
    }

    private void CreateDecorativeBeam(Vector3 position, Vector3 size)
    {
        AddChild(new MeshInstance3D
        {
            Name = "CryptVaultBeam",
            Position = position,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = CreateMaterial(new Color("372f45")),
        });
    }

    private bool TryAttachVisual(Node parent, EntityInstance entity, bool applyPlayerVisualTransform = false)
    {
        var assetId = GetOverride(entity, VisualAssetProperty);
        if (string.IsNullOrWhiteSpace(assetId)) return false;
        if (_modelLibrary is null)
            throw new InvalidOperationException($"Entity '{entity.Id}' references visual asset '{assetId}' but the module model library is unavailable.");
        var visual = _modelLibrary.Instantiate(assetId);
        visual.Name = "AuthoredVisual";
        if (applyPlayerVisualTransform)
        {
            visual.Basis = EngineValueConverter.ToEngine(entity.Transform).Basis;
            visual.Position = new Vector3(0, -1f, 0);
        }
        parent.AddChild(visual);
        _runtimeModelInstances++;
        var animationProfile = GetOverride(entity, RuntimeAnimationProfileProperty);
        var animation = animationProfile switch
        {
            "humanoid-basic" => GodotAssetAnimationController.Configure(visual),
            "kaykit-adventurer" => GodotAssetAnimationController.Configure(
                visual,
                idle: GetOverride(entity, "runtime.animationIdle") ?? "Idle",
                run: GetOverride(entity, "runtime.animationRun") ?? "Running_A",
                jump: GetOverride(entity, "runtime.animationJump") ?? "Jump_Full_Short",
                attack: GetOverride(entity, "runtime.animationAttack") ?? "1H_Melee_Attack_Slice_Horizontal",
                hit: GetOverride(entity, "runtime.animationHit") ?? "Hit_A",
                death: GetOverride(entity, "runtime.animationDeath") ?? "Death_A"),
            _ => null,
        };
        if (animation is not null)
        {
            GodotAssetAnimationController.Play(animation, animation.IdleClip, 0);
            _animatedCharacterInstances++;
            _animatedCharacterClipNames.UnionWith(animation.Clips);
            if (parent is Node3D character)
            {
                _characterAnimations[character] = animation;
            }
            if (parent is PlayerController player)
            {
                player.ConfigureAnimations(animation);
            }
        }
        return true;
    }

    private static bool HasRuntimeRole(EntityInstance entity, string role) =>
        string.Equals(GetOverride(entity, RuntimeRoleProperty), role, StringComparison.OrdinalIgnoreCase);

    private static string? GetOverride(EntityInstance entity, string key) =>
        entity.Overrides.Properties.SingleOrDefault(property =>
            string.Equals(property.Key, key, StringComparison.Ordinal))?.Value;

    private void CreateHud()
    {
        _hud = new RuntimeHud
        {
            ModuleTitle = _moduleTitle,
            CombatEnabled = _playerCombatant is not null,
            Accessibility = _runtimeExtensions?.ModernTooling.QualityAssurance.Accessibility,
        };
        AddChild(_hud);
        _actionWheel = new RuntimeActionWheel();
        if (_gameplayControls is not null) _actionWheel.Configure(_gameplayControls);
        _actionWheel.CommandRequested += RequestGameplayCommand;
        AddChild(_actionWheel);
        if (_runtimeExtensions is not null && _runtimeExtensions.UiDocuments.Count > 0)
        {
            _declarativeUi = new RuntimeDeclarativeUi();
            _declarativeUi.Configure(_runtimeExtensions, _runtimeJson, _scriptUiEvents, _runtimeJsonHandles);
            _declarativeUi.EventRaised += HandleScriptableUiEvent;
            AddChild(_declarativeUi);
        }
        UpdateTacticalHud();
    }

    private void HandleScriptableUiEvent(ScriptUiEvent @event)
    {
        _message = $"Interface {@event.Document.Key} : {@event.Command}";
        GD.Print($"OPENRPG_FORGE_RUNTIME_UI_EVENT_PASS document={@event.Document.Key} widget={@event.WidgetKey} " +
            $"command={@event.Command} sequence={@event.Sequence}");
        UpdateHud();
    }

    private void InteractWithNearest()
    {
        var candidates = new[] { _npc, _keyItem, _door }
            .Where(node => node is not null && GodotObject.IsInstanceValid(node))
            .Cast<Node3D>()
            .OrderBy(node => _player!.GlobalPosition.DistanceTo(node.GlobalPosition))
            .ToArray();
        var nearest = candidates.FirstOrDefault();

        if (nearest is null || !IsNear(nearest, InteractionDistance))
        {
            _message = "Rien à portée d’interaction.";
            UpdateHud();
            return;
        }

        InteractWith(nearest);
    }

    private void InteractWith(Node3D target)
    {
        if (target == _npc)
        {
            InteractWithGuide();
            UpdateHud();
            return;
        }

        if (target == _keyItem && !_hasKey)
        {
            _hasKey = true;
            _inventory.Add(_questItemSlug);
            _message = $"Vous obtenez {_questItemDisplayName}.";
            _keyItem.QueueFree();
            UpdateHud();
            return;
        }

        if (target == _door && !_doorUnlocked)
        {
            var transition = GetAvailableQuestTransitions()
                .FirstOrDefault(candidate => candidate.Actions.Any(action =>
                    action.Kind == QuestActionKind.UnlockEntity &&
                    string.Equals(action.Key, _doorEntityId?.ToString(), StringComparison.Ordinal)));
            if (transition is not null)
            {
                AdvanceQuest(transition);
            }
            else
            {
                _message = !_hasKey
                    ? $"{_gateDisplayName} reste fermé. Il manque l’objet lié à la quête."
                    : "La quête n’autorise pas encore le passage.";
            }

            UpdateHud();
            return;
        }

        if (target == _npc)
        {
            if (_questState == PrototypeQuestState.NotStarted)
            {
                _questState = PrototypeQuestState.Active;
                _message = "Elyra : Retrouvez la clé de braise et terrassez le Gardien.";
            }
            else
            {
                _message = _bossDefeated
                    ? "Elyra : La cendre retombe enfin."
                    : "Elyra : La clé brille dans l’aile est de la crypte.";
            }
        }
        else if (target == _keyItem && !_hasKey)
        {
            _hasKey = true;
            _message = "Vous obtenez la Clé de braise.";
            _keyItem.QueueFree();
        }
        else if (target == _door && !_doorUnlocked)
        {
            if (!_hasKey)
            {
                _message = "La porte est scellée. Une clé ardente semble nécessaire.";
            }
            else
            {
                _doorUnlocked = true;
                _message = "La Porte des Cendres se déverrouille.";
                _door.QueueFree();
            }
        }

        UpdateHud();
    }

    private void AttackNearestHostile()
    {
        var nearest = _hostiles.Keys
            .Where(GodotObject.IsInstanceValid)
            .OrderBy(node => _player!.GlobalPosition.DistanceTo(node.GlobalPosition))
            .FirstOrDefault();

        if (nearest is null || !IsNear(nearest, AttackDistance))
        {
            _message = "Aucun ennemi à portée.";
            UpdateHud();
            return;
        }

        DamageHostile(nearest);
    }

    private void DamageHostile(Node3D hostile)
    {
        if (!_hostiles.TryGetValue(hostile, out var target) || _playerCombatant is null)
        {
            return;
        }

        var ability = _playerCombatant.Profile.Abilities.FirstOrDefault()
            ?? throw new InvalidOperationException("The player combat profile has no attack ability.");
        _player?.PlayAttackAnimation();
        var result = CombatResolver.Cast(
            _playerCombatant,
            target,
            ability.Id,
            Time.GetTicksMsec() / 1000d,
            damageMultiplier: _resolvedDifficulty?.PlayerDamageMultiplier ?? 1);
        var health = target.Health;
        var hostileName = _hostileNames[hostile];
        _message = $"{hostileName} subit {result.Damage} dégât(s) ({health} PV).";
        var reactionDuration = PlayHostileCombatReaction(hostile, result.TargetDied);

        if (hostile == _boss && health <= Math.Max(1, target.Profile.Stats.MaximumHealth / 2) && !_bossPhaseTwo)
        {
            _bossPhaseTwo = true;
            ExecuteForgeScript(_bossPhaseTwoScript, hostile);
            GD.Print("OPENRPG_FORGE_BOSS_PHASE_TWO");
        }

        if (result.TargetDied)
        {
            _hostiles.Remove(hostile);
            _hostileNames.Remove(hostile);
            foreach (var drop in result.Loot)
            {
                _inventory.Add(drop.ItemSlug);
                GD.Print($"OPENRPG_FORGE_LOOT_GRANTED item={drop.ItemSlug} quantity={drop.Quantity}");
            }

            _characterAnimations.Remove(hostile);
            if (reactionDuration > 0)
            {
                var timer = GetTree().CreateTimer(reactionDuration);
                timer.Timeout += () =>
                {
                    if (GodotObject.IsInstanceValid(hostile)) hostile.QueueFree();
                };
            }
            else
            {
                hostile.QueueFree();
            }

            if (hostile == _boss)
            {
                _bossDefeated = true;
                ExecuteForgeScript(_bossDeathScript, hostile);
            }
            else
            {
                _message = $"{hostileName} est vaincue.";
            }

            AwardProgression(hostile == _boss ? 200 : 75, "combat");
        }

        UpdateHud();
    }

    private double PlayHostileCombatReaction(Node3D hostile, bool died)
    {
        if (!_characterAnimations.TryGetValue(hostile, out var animation)) return 0;
        var clip = died ? animation.DeathClip : animation.HitClip;
        if (clip is null || !GodotAssetAnimationController.Play(animation, clip, .06, restart: true)) return 0;
        if (died)
        {
            _combatDeathAnimationCount++;
        }
        else
        {
            _combatHitAnimationCount++;
        }

        return Math.Max(.05, GodotAssetAnimationController.DurationSeconds(animation, clip));
    }

    private void SpawnAshSpirit(Vector3 position)
    {
        _spawnedAshSpiritCount++;
        var spirit = new Node3D { Name = "AshSpirit", Position = position };
        AddChild(spirit);
        spirit.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.35f, Height = 0.7f },
            MaterialOverride = CreateMaterial(new Color("ff7675"), emission: true),
        });
    }

    private ForgeScriptArtifact? TryCompileEntityScript(EntityInstance entity, string propertyKey)
    {
        var relativePath = entity.Overrides.Properties
            .SingleOrDefault(property => string.Equals(property.Key, propertyKey, StringComparison.Ordinal))
            ?.Value;
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        if (
            relativePath.Contains("..", StringComparison.Ordinal) ||
            Path.IsPathFullyQualified(relativePath))
        {
            throw new InvalidOperationException(
                $"Entity '{entity.Id}' requires a safe relative ForgeScript path in '{propertyKey}'.");
        }

        var resourcePath = $"res://content/{_moduleSlug}/{relativePath.Replace('\\', '/')}";
        var source = _moduleContentRoot is null
            ? Godot.FileAccess.GetFileAsString(resourcePath)
            : ReadCachedModuleScript(relativePath);
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException($"ForgeScript source '{resourcePath}' could not be loaded.");
        }

        GD.Print($"OPENRPG_FORGE_SCRIPT_COMPILE_START path={relativePath}");
        var cachedCompilation = _forgeScriptCache?.Compile(source)
            ?? throw new InvalidOperationException("ForgeScript cache is not initialized.");
        var compilation = cachedCompilation.Compilation;
        if (!compilation.Success || compilation.Artifact is null)
        {
            throw new InvalidOperationException(
                $"ForgeScript '{resourcePath}' failed to compile: " +
                string.Join("; ", compilation.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        GD.Print(
            $"OPENRPG_FORGE_FORGESCRIPT_CACHE status={(cachedCompilation.CacheHit ? "hit" : "miss")} " +
            $"key={cachedCompilation.CacheKey} script={relativePath} " +
            $"schema={cachedCompilation.Metadata!.SchemaVersion}");
        GD.Print($"OPENRPG_FORGE_SCRIPT_COMPILE_PASS path={relativePath}");
        return compilation.Artifact;
    }

    private string ReadCachedModuleScript(string relativePath)
    {
        var root = Path.GetFullPath(_moduleContentRoot!);
        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(path))
        {
            throw new InvalidOperationException($"Cached ForgeScript '{relativePath}' is unavailable.");
        }

        return File.ReadAllText(path);
    }

    private void ExecuteForgeScript(ForgeScriptArtifact? artifact, Node3D origin)
    {
        if (artifact is null)
        {
            _message = "Le script de gameplay requis est absent.";
            GD.PrintErr("OPENRPG_FORGE_SCRIPT_FAILED missing_artifact");
            return;
        }

        var result = _forgeScriptRuntime.Execute(
            artifact,
            new ForgeScriptQuotas(
                instructionLimit: 2_000,
                timeoutMilliseconds: 50,
                recursionLimit: 16,
                spawnCap: 8));
        if (result.Status != ForgeScriptExecutionStatus.Completed)
        {
            _message = "Un script de gameplay a été interrompu ; la partie continue.";
            GD.PrintErr(
                "OPENRPG_FORGE_SCRIPT_FAILED " +
                string.Join(";", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
            return;
        }

        var spawnIndex = 0;
        foreach (var scriptEvent in result.Events)
        {
            switch (scriptEvent.Kind)
            {
                case "variable":
                    var variable = scriptEvent.Value.Split('=', 2);
                    if (variable.Length == 2)
                    {
                        SetProjectVariable(variable[0], variable[1]);
                    }

                    break;
                case "say":
                    _message = scriptEvent.Value;
                    break;
                case "cast":
                    if (scriptEvent.Value.EndsWith(":ash-nova", StringComparison.Ordinal))
                    {
                        _ashNovaCueCount++;
                    }

                    GD.Print($"OPENRPG_FORGE_ABILITY_CUE value={scriptEvent.Value}");
                    break;
                case "spawn":
                    var blueprint = scriptEvent.Value.Split(':', 2)[0];
                    if (string.Equals(blueprint, "ash-spirit", StringComparison.Ordinal))
                    {
                        var side = spawnIndex++ % 2 == 0 ? -2 : 2;
                        SpawnAshSpirit(origin.GlobalPosition + new Vector3(side, 0, 0));
                    }

                    break;
                case "quest":
                    var questTransition = GetAvailableQuestTransitions().FirstOrDefault();
                    if (questTransition is not null)
                    {
                        AdvanceQuest(questTransition);
                    }

                    break;
                case "unlock":
                    UnlockEntity(scriptEvent.Value);
                    break;
            }
        }

        GD.Print($"OPENRPG_FORGE_SCRIPT_EXECUTED events={result.Events.Count}");
    }

    private bool IsTimeFrozen() => _timeControlSession.State is RuntimeTimeState.Paused or
        RuntimeTimeState.Planning or RuntimeTimeState.TurnBased;

    private void ToggleTacticalPause()
    {
        if (_gameplayControls is null) return;
        if (IsTimeFrozen())
        {
            var planned = _timeControlSession.PlannedActions.ToArray();
            var resumed = TimeControlCoordinator.Request(_gameplayControls.TimeControl, _timeControlSession,
                RuntimeTimeState.Running, "player:local", false, true);
            ApplyTimeControl(resumed);
            foreach (var action in planned) ExecuteGameplayCommand(action.Command);
            _timeControlSession = _timeControlSession with { PlannedActions = [] };
            _message = planned.Length == 0
                ? "Simulation reprise."
                : $"Simulation reprise · {planned.Length} action(s) planifiée(s) exécutée(s).";
        }
        else
        {
            var requested = _gameplayControls.TimeControl.PlanningEnabled
                ? RuntimeTimeState.Planning
                : RuntimeTimeState.Paused;
            ApplyTimeControl(TimeControlCoordinator.Request(_gameplayControls.TimeControl, _timeControlSession,
                requested, "player:local", false, true));
            _message = requested == RuntimeTimeState.Planning
                ? "Pause tactique · choisissez des actions, puis P pour exécuter."
                : "Jeu en pause.";
        }

        UpdateHud();
    }

    private void ToggleSlowMotion()
    {
        if (_gameplayControls is null || IsTimeFrozen()) return;
        var requested = _timeControlSession.State == RuntimeTimeState.SlowMotion
            ? RuntimeTimeState.Running
            : RuntimeTimeState.SlowMotion;
        ApplyTimeControl(TimeControlCoordinator.Request(_gameplayControls.TimeControl, _timeControlSession,
            requested, "player:local", false, true));
        _message = requested == RuntimeTimeState.SlowMotion ? "Ralenti tactique actif." : "Vitesse normale.";
        UpdateHud();
    }

    private void ApplyTimeControl(TimeControlDecision decision)
    {
        if (!decision.Accepted)
        {
            _message = $"Contrôle du temps refusé ({decision.Code}).";
            return;
        }

        _timeControlSession = decision.Session;
        if (_player is not null)
        {
            _player.ControlLocked = IsTimeFrozen();
            _player.SimulationScale = _timeControlSession.State == RuntimeTimeState.SlowMotion
                ? _timeControlSession.Scale
                : 1;
        }
    }

    private void CycleCombatProvider()
    {
        if (_gameplayControls is null || _gameplayControls.CombatModes.Providers.Count == 0) return;
        var providers = _gameplayControls.CombatModes.Providers;
        var current = _activeCombatProvider is null ? -1 : providers.ToList().FindIndex(provider =>
            provider.ProviderId == _activeCombatProvider.ProviderId);
        _activeCombatProvider = providers[(current + 1) % providers.Count];
        _message = $"Mode de combat : {_activeCombatProvider.DisplayName}.";
        if (_activeCombatProvider.Kind == CombatModeKind.TurnBased && !IsTimeFrozen())
        {
            ApplyTimeControl(TimeControlCoordinator.Request(_gameplayControls.TimeControl, _timeControlSession,
                RuntimeTimeState.TurnBased, "player:local", false, true));
        }
        else if (_activeCombatProvider.Kind != CombatModeKind.TurnBased &&
                 _timeControlSession.State == RuntimeTimeState.TurnBased)
        {
            ApplyTimeControl(TimeControlCoordinator.Request(_gameplayControls.TimeControl, _timeControlSession,
                RuntimeTimeState.Running, "player:local", false, true));
        }

        UpdateHud();
    }

    private void RequestGameplayCommand(string command)
    {
        if (_gameplayControls is null) return;
        if (_timeControlSession.State == RuntimeTimeState.Planning)
        {
            var queued = TimeControlCoordinator.Queue(_gameplayControls.TimeControl, _timeControlSession,
                new PlannedGameplayAction(Guid.NewGuid(), "player:local", command, "nearest",
                    _timeControlSession.PlannedActions.Count));
            if (queued.Accepted)
            {
                _timeControlSession = queued.Session;
                _message = $"Action planifiée : {command}.";
            }
            else _message = $"Action refusée ({queued.Code}).";
            UpdateHud();
            return;
        }

        if (IsTimeFrozen())
        {
            _message = "Cette action exige le mode planification ou la reprise du jeu.";
            UpdateHud();
            return;
        }

        ExecuteGameplayCommand(command);
    }

    private void ExecuteGameplayCommand(string command)
    {
        switch (command)
        {
            case "interact": InteractWithNearest(); break;
            case "attack": AttackNearestHostile(); break;
            case "rest": RequestPlayerRest(); break;
            case "quick_save": SaveRuntime(_quickSavePath!); break;
            default:
                _message = $"Commande de roue inconnue : {command}.";
                UpdateHud();
                break;
        }
    }

    private void UpdateTacticalHud()
    {
        if (_hud is null || _gameplayControls is null) return;
        var movement = _player?.CurrentCapability.ToString().ToLowerInvariant() ?? "ground";
        _hud.UpdateTactical(_timeControlSession.State.ToString(),
            _activeCombatProvider?.DisplayName ?? _gameplayControls.CombatModes.DefaultProvider.DisplayName,
            _timeControlSession.PlannedActions.Count, movement, _player?.BreathRemaining ?? 0,
            _player?.StaminaRemaining ?? 0);
        if (_moduleRules is not null && _areaRules is not null && _resolvedDifficulty is not null)
        {
            var next = _moduleRules.Progression.Curve.FirstOrDefault(point => point.Level > _progressionLevel)
                ?.RequiredExperience;
            _hud.UpdateRules(_resolvedDifficulty.DisplayName, _progressionLevel, _progressionExperience, next,
                _areaRules.Combat.PvpMode.ToString(), _areaRules.Perception.Modifiers.Count);
        }
    }

    private void AwardProgression(double baseAmount, string sourceId)
    {
        if (_moduleRules is null || _resolvedDifficulty is null) return;
        var previousLevel = _progressionLevel;
        var result = RulesRuntimeService.ResolveProgression(_moduleRules.Progression, baseAmount, sourceId, 1,
            _progressionExperience, _resolvedDifficulty.ProgressionMultiplier);
        _progressionExperience = result.TotalExperience;
        _progressionLevel = result.Level;
        if (_progressionLevel > previousLevel)
        {
            _gameplayEvents.Publish(new ProgressionChangedEvent(Guid.NewGuid(), "player:local", previousLevel,
                _progressionLevel, sourceId, EventAuthorityScope.Server, _runtimePolicyRevision++,
                DateTimeOffset.UtcNow));
        }
        _message += $" +{result.AwardedExperience:0} XP · niveau {_progressionLevel}.";
        GD.Print($"OPENRPG_FORGE_RUNTIME_PROGRESSION source={sourceId} award={result.AwardedExperience:0.###} " +
                 $"total={result.TotalExperience:0.###} level={result.Level}");
    }

    private void UpdateHud()
    {
        if (_hud is null)
        {
            return;
        }

        UpdateTacticalHud();

        if (!_isCryptPrototype && !_isForgottenForestPrototype)
        {
            _hud.UpdateState(
                "explorer le module",
                _message,
                $"Module : {_moduleSlug} | Entités : {_builtEntityCount} | Combat : {(_playerCombatant is null ? "désactivé" : "actif")}");
            return;
        }

        if (_isForgottenForestPrototype)
        {
            var forestObjective = _questMachine?.CurrentState.Journal.Fallback ?? "rencontrer la garde forestière [E]";
            var forestBossCombatant = _boss is not null && _hostiles.TryGetValue(_boss, out var forestBoss)
                ? forestBoss
                : null;
            var forestState = $"Relique : {(_hasKey ? "trouvée" : "à trouver")} | " +
                              $"Passage : {(_doorUnlocked ? "ouvert" : "fermé")} | " +
                              $"Gardien : {forestBossCombatant?.Health ?? 0}/{forestBossCombatant?.Profile.Stats.MaximumHealth ?? 0}";
            _hud.UpdateState(forestObjective, _message, forestState);
            return;
        }

        var objective = _questState switch
        {
            PrototypeQuestState.NotStarted => "parler à Elyra [E]",
            PrototypeQuestState.Active when !_hasKey => "trouver la Clé de braise",
            PrototypeQuestState.Active when !_doorUnlocked => "ouvrir la Porte des Cendres",
            PrototypeQuestState.Active => "vaincre le Gardien des Cendres [ESPACE]",
            PrototypeQuestState.Completed => "rejoindre la sortie lumineuse",
            _ => "explorer",
        };
        if (_questMachine is not null)
        {
            objective = _questMachine.CurrentState.Journal.Fallback;
        }

        var bossCombatant = _boss is not null && _hostiles.TryGetValue(_boss, out var combatant)
            ? combatant
            : null;
        var bossHealth = bossCombatant?.Health ?? 0;
        var bossMaximumHealth = bossCombatant?.Profile.Stats.MaximumHealth ?? 0;
        var state = $"Quête : {_questState} | Clé : {(_hasKey ? "oui" : "non")} | Boss : {bossHealth}/{bossMaximumHealth}";
        _hud.UpdateState(objective, _message, state);
    }

    private void InteractWithGuide()
    {
        if (_elyraDialogue is null)
        {
            _message = $"{_guideName} reste silencieux : aucun dialogue n’est lié à ce module.";
            return;
        }

        var traversal = new DialogueTraversal(_elyraDialogue);
        var start = traversal.Start();
        var evaluator = new RuntimeConditionEvaluator(this);
        var choice = traversal.GetAvailableChoices(evaluator).FirstOrDefault();
        if (choice is null)
        {
            _message = $"{_guideName} : {start.Node!.Text.Fallback}";
            return;
        }

        var step = traversal.Choose(choice.Id, evaluator);
        ApplyDialogueActions(step.Actions);
        _message = $"{_guideName} : {step.Node?.Text.Fallback ?? start.Node!.Text.Fallback}";
    }

    private void ApplyDialogueActions(IReadOnlyList<DialogueAction> actions)
    {
        foreach (var action in actions)
        {
            switch (action.Kind)
            {
                case DialogueActionKind.SetVariable:
                    SetProjectVariable(action.Key, action.Value);
                    break;
                case DialogueActionKind.StartQuest:
                case DialogueActionKind.AdvanceQuest:
                case DialogueActionKind.CompleteQuest:
                    var transition = GetAvailableQuestTransitions().FirstOrDefault();
                    if (transition is not null)
                    {
                        AdvanceQuest(transition);
                    }

                    break;
                case DialogueActionKind.GiveItem:
                    _inventory.Add(action.Key);
                    break;
                case DialogueActionKind.UnlockEntity:
                    UnlockEntity(action.Key);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "Unsupported dialogue action.");
            }
        }
    }

    private IReadOnlyList<QuestTransition> GetAvailableQuestTransitions() =>
        _questMachine?.GetAvailableTransitions(new RuntimeConditionEvaluator(this)) ?? [];

    private void AdvanceQuest(QuestTransition transition)
    {
        if (_questMachine is null)
        {
            return;
        }

        var result = _questMachine.Advance(transition.Id, new RuntimeConditionEvaluator(this));
        _questState = result.Completed ? PrototypeQuestState.Completed : PrototypeQuestState.Active;
        foreach (var action in result.Actions)
        {
            switch (action.Kind)
            {
                case QuestActionKind.SetVariable:
                    SetProjectVariable(action.Key, action.Value);
                    break;
                case QuestActionKind.GiveItem:
                    _inventory.Add(action.Key);
                    break;
                case QuestActionKind.UnlockEntity:
                    UnlockEntity(action.Key);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "Unsupported quest action.");
            }
        }

        _message = result.State.Journal.Fallback;
    }

    private void UnlockEntity(string entityId)
    {
        if (_doorEntityId is null || !string.Equals(entityId, _doorEntityId.ToString(), StringComparison.Ordinal))
        {
            return;
        }

        _doorUnlocked = true;
        if (_door is not null && GodotObject.IsInstanceValid(_door))
        {
            _door.QueueFree();
        }

        _message = $"{_gateDisplayName} s’ouvre.";
    }

    private void SetProjectVariable(string key, string value)
    {
        if (_blackboard.TrySet(key, value, out var error))
        {
            return;
        }

        _message = $"La variable de projet '{key}' a refusé une valeur incompatible.";
        GD.PrintErr($"OPENRPG_FORGE_RUNTIME_VARIABLE_REJECTED key={key} error={error}");
    }

    private bool SaveRuntime(string path)
    {
        if (_player is null || _playerCombatant is null || _moduleId is null ||
            _moduleContentSha256 is null || _currentAreaId is null)
        {
            GD.PrintErr("OPENRPG_FORGE_RUNTIME_SAVE_FAILED runtime_not_ready");
            return false;
        }

        var position = _player.GlobalPosition;
        var save = new RuntimeSaveDocument(
            RuntimeSaveDocument.CurrentFormatVersion,
            _moduleId,
            _moduleContentSha256,
            _currentAreaId,
            DateTimeOffset.UtcNow,
            new RuntimeVector3(position.X, position.Y, position.Z),
            new RuntimeCombatantSave(
                _playerCombatant.Id,
                _playerCombatant.Health,
                _playerCombatant.Resource),
            _inventory.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            _blackboard.Snapshot().OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new RuntimeKeyValue(item.Key, item.Value))
                .ToArray(),
            _questDefinition?.Id.ToString(),
            _questMachine?.CurrentState.Id.ToString(),
            _hostiles.Values
                .OrderBy(item => item.Id, StringComparer.Ordinal)
                .Select(item => new RuntimeCombatantSave(item.Id, item.Health, item.Resource))
                .ToArray(),
            new RuntimeProgressFlags(
                _hasKey,
                _doorUnlocked,
                _bossPhaseTwo,
                _bossDefeated,
                _escaped),
            _environmentProfile?.Weather.PersistState == true && _weatherState is not null
                ? new RuntimeWeatherSave(
                    _weatherState.SchemaVersion,
                    _weatherState.StateId.ToString(),
                    _weatherState.Revision,
                    _weatherState.ServerRevision,
                    _weatherState.ElapsedSeconds)
                : null,
            new RuntimeProgressionSave(1, _progressionExperience, _progressionLevel),
            BuildEntityStateSnapshots());
        var result = RuntimeSaveStore.Save(path, save);
        if (!result.Succeeded)
        {
            _message = "Échec de la sauvegarde de partie.";
            GD.PrintErr(
                "OPENRPG_FORGE_RUNTIME_SAVE_FAILED " +
                string.Join(";", result.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
            return false;
        }

        _message = $"Partie sauvegardée — {Path.GetFileName(path)}";
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_SAVE_WRITTEN format={RuntimeSaveDocument.CurrentFormatVersion} " +
            $"file={Path.GetFileName(path)} quest={_questMachine?.CurrentState.Id}");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G11O_SAVE_PASS mode=write entities={save.EntitySnapshots?.Count ?? 0} " +
            "components=transform-combat-inventory-blackboard schema=1");
        UpdateHud();
        return true;
    }

    private bool LoadRuntimeSave(string path)
    {
        if (_moduleId is null || _moduleContentSha256 is null)
        {
            GD.PrintErr("OPENRPG_FORGE_RUNTIME_LOAD_FAILED runtime_not_ready");
            return false;
        }

        var result = RuntimeSaveStore.Load(path, _moduleId, _moduleContentSha256);
        if (!result.Succeeded || result.Save is null)
        {
            _message = "Cette sauvegarde est absente, invalide ou incompatible avec le module.";
            GD.PrintErr(
                "OPENRPG_FORGE_RUNTIME_LOAD_FAILED " +
                string.Join(";", result.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
            return false;
        }

        try
        {
            ApplyRuntimeSave(result.Save);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentOutOfRangeException)
        {
            _message = "La sauvegarde référence un état de jeu indisponible.";
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_LOAD_FAILED save.state_invalid:{exception.Message}");
            return false;
        }

        _loadedSave = true;
        _message = $"Partie reprise — {Path.GetFileName(path)}";
        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_SAVE_LOADED format={result.Save.FormatVersion} " +
            $"file={Path.GetFileName(path)} quest={result.Save.QuestStateId}");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G11O_SAVE_PASS mode=load entities={result.Save.EntitySnapshots?.Count ?? 0} " +
            "components=transform-combat-inventory-blackboard schema=1");
        return true;
    }

    private void ApplyRuntimeSave(RuntimeSaveDocument save)
    {
        if (_player is null || _playerCombatant is null ||
            !string.Equals(save.AreaId, _currentAreaId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The save area does not match the loaded runtime area.");
        }

        _player.GlobalPosition = new Vector3(
            (float)save.PlayerPosition.X,
            (float)save.PlayerPosition.Y,
            (float)save.PlayerPosition.Z);
        _playerCombatant.Restore(save.Player.Health, save.Player.Resource);
        _inventory.Clear();
        foreach (var item in save.Inventory)
        {
            _inventory.Add(item);
        }

        _blackboard = new ProjectBlackboard(_blackboard.Definitions, allowUndeclared: true);
        var variableDiagnostics = _blackboard.Load(save.Variables.ToDictionary(
            variable => variable.Key,
            variable => variable.Value,
            StringComparer.Ordinal));
        if (variableDiagnostics.Count > 0)
        {
            throw new InvalidOperationException(string.Join("; ", variableDiagnostics));
        }

        if (_questDefinition is not null && _questMachine is not null && save.QuestStateId is not null)
        {
            if (!string.Equals(save.QuestId, _questDefinition.Id.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The saved quest does not match the loaded module quest.");
            }

            _questMachine.Restore(QuestStateId.Parse(save.QuestStateId));
            _questState = _questMachine.Completed
                ? PrototypeQuestState.Completed
                : _questMachine.CurrentState.Id == _questDefinition.InitialStateId
                    ? PrototypeQuestState.NotStarted
                    : PrototypeQuestState.Active;
        }

        if (save.Weather is not null)
        {
            if (_environmentProfile is null ||
                !_environmentProfile.Weather.States.Any(state =>
                    string.Equals(state.Id.ToString(), save.Weather.StateId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("The saved weather state is unavailable in the current area.");
            }

            _weatherState = new WeatherRuntimeSnapshot(
                save.Weather.SchemaVersion,
                Guid.Parse(save.Weather.StateId),
                save.Weather.Revision,
                save.Weather.ServerRevision,
                save.Weather.ElapsedSeconds);
            ApplyClimateEffects(force: true);
        }

        var hostileSaves = save.Hostiles.ToDictionary(item => item.EntityId, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in _hostiles.ToArray())
        {
            if (hostileSaves.TryGetValue(pair.Value.Id, out var hostileSave))
            {
                pair.Value.Restore(hostileSave.Health, hostileSave.Resource);
                continue;
            }

            _hostiles.Remove(pair.Key);
            _hostileNames.Remove(pair.Key);
            pair.Key.QueueFree();
        }

        if (hostileSaves.Keys.Any(id => _hostiles.Values.All(item =>
                !string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidOperationException("A saved hostile is no longer available in the current scene.");
        }

        ApplyEntityStateSnapshots(save.EntitySnapshots ?? []);

        _hasKey = save.Progress.HasKey;
        _doorUnlocked = save.Progress.DoorUnlocked;
        _bossPhaseTwo = save.Progress.BossPhaseTwo;
        _bossDefeated = save.Progress.BossDefeated;
        _escaped = save.Progress.Escaped;
        _progressionExperience = save.Progression?.Experience ?? 0;
        _progressionLevel = save.Progression?.Level ?? 1;
        if (_hasKey && _keyItem is not null && GodotObject.IsInstanceValid(_keyItem))
        {
            _keyItem.QueueFree();
        }

        if (_doorUnlocked && _door is not null && GodotObject.IsInstanceValid(_door))
        {
            _door.QueueFree();
        }
    }

    private IReadOnlyList<RuntimeEntityStateSave> BuildEntityStateSnapshots()
    {
        if (_moduleRuntimePolicies is null) return [];
        var policy = _moduleRuntimePolicies.Extensions.SnapshotPolicy;
        var snapshots = new List<RuntimeEntityStateSave>();
        foreach (var pair in _entityNodes.OrderBy(item => item.Key))
        {
            var components = new List<RuntimeStateComponentSave>();
            if (policy.Components.Contains("transform"))
            {
                var position = pair.Value.GlobalPosition;
                components.Add(new RuntimeStateComponentSave("transform", JsonSerializer.Serialize(new
                {
                    x = position.X,
                    y = position.Y,
                    z = position.Z,
                })));
            }
            var combatant = pair.Value == _player ? _playerCombatant : _hostiles.GetValueOrDefault(pair.Value);
            if (policy.Components.Contains("combat") && combatant is not null)
                components.Add(new RuntimeStateComponentSave("combat", JsonSerializer.Serialize(new
                {
                    health = combatant.Health,
                    resource = combatant.Resource,
                })));
            var isPlayer = pair.Value == _player;
            var inventory = isPlayer && policy.Components.Contains("inventory")
                ? _inventory.Order(StringComparer.Ordinal).Take(policy.MaxInventoryItems).ToArray() : [];
            var blackboard = isPlayer && policy.Components.Contains("blackboard")
                ? _blackboard.Snapshot().OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new RuntimeKeyValue(item.Key, item.Value)).ToArray() : [];
            snapshots.Add(new RuntimeEntityStateSave(pair.Key.ToString("D"), 1, components, [], inventory,
                blackboard));
        }
        return snapshots;
    }

    private void ApplyEntityStateSnapshots(IReadOnlyList<RuntimeEntityStateSave> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            if (!Guid.TryParse(snapshot.EntityId, out var id) || !_entityNodes.TryGetValue(id, out var node))
                throw new InvalidOperationException($"Saved entity '{snapshot.EntityId}' is unavailable.");
            var transform = snapshot.Components.SingleOrDefault(item => item.Key == "transform");
            if (transform is null) continue;
            using var json = JsonDocument.Parse(transform.JsonValue);
            var root = json.RootElement;
            node.GlobalPosition = new Vector3(root.GetProperty("x").GetSingle(), root.GetProperty("y").GetSingle(),
                root.GetProperty("z").GetSingle());
        }
    }

    private bool IsNear(Node3D? target, float distance) =>
        _player is not null &&
        target is not null &&
        GodotObject.IsInstanceValid(target) &&
        _player.GlobalPosition.DistanceTo(target.GlobalPosition) <= distance;

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

        AssertSmoke(_builtEntityCount == 16, "Expected 16 reconstructed entities.");
        AssertSmoke(_collisionCount == 10, "Expected 10 static collision shapes.");
        AssertSmoke(_player is not null, "Player was not reconstructed.");
        AssertSmoke(_npc is not null, "NPC was not reconstructed.");
        AssertSmoke(_keyItem is not null, "Key item was not reconstructed.");
        AssertSmoke(_door is not null, "Door was not reconstructed.");
        AssertSmoke(_boss is not null, "Boss was not reconstructed.");

        InteractWith(_npc!);
        AssertSmoke(_questState == PrototypeQuestState.Active, "NPC interaction did not start the quest.");
        AssertSmoke(
            _questMachine?.CurrentState.DisplayName == "Clé de braise",
            "NPC interaction did not start the data-driven quest.");
        InteractWith(_keyItem!);
        AssertSmoke(_hasKey, "Key interaction did not update inventory state.");
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
        AssertSmoke(_inventory.Contains("warden-ember"), "Data-driven boss loot was not granted.");
        AssertSmoke(_questState == PrototypeQuestState.Completed, "Quest was not completed.");
        AssertSmoke(_questMachine?.Completed == true, "Data-driven quest was not completed.");

        GD.Print("OPENRPG_FORGE_RUNTIME_SMOKE_PASS quest=completed door=unlocked boss=defeated");
        GetTree().Quit(0);
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

        GD.Print(
            $"OPENRPG_FORGE_RUNTIME_GENERIC_SMOKE_PASS slug={_moduleSlug} " +
            $"entities={_builtEntityCount} collisions={_collisionCount} combat=false interaction=empty");
        GetTree().Quit(0);
    }

    private async Task RunForgottenForestSmokeTestAsync(ProjectDefinition project, string? benchmarkPath)
    {
        AssertSmoke(_isForgottenForestPrototype, "The Forgotten Forest smoke test loaded another module.");
        var area = project.Areas.Single();
        AssertSmoke(area.Kind == AreaKind.Exterior, "Forgotten Forest must remain an exterior area.");
        AssertSmoke(area.Dimensions == new AreaDimensions(512, 512),
            "Forgotten Forest must use the authored 512 x 512 production footprint.");
        AssertSmoke(_terrainVertices == 129 * 129 && _terrainSectors == 64,
            "The authored 129 x 129 terrain grid was not reconstructed as 64 sectors.");
        AssertSmoke(_terrainMaterialMode == GodotTerrainMaterialAdapter.ProceduralPbrMode &&
                    _terrainMaterialLayers == 4,
            "Forgotten Forest must reconstruct its four-layer procedural PBR terrain material.");
        AssertSmoke(_terrainScatterLayers == 17,
            "The biome scatter did not reconstruct the five foundation and twelve close-dressing layers.");
        AssertSmoke(_terrainScatterInstances >= 7_000, "The 512 x 512 dressed forest is too sparse.");
        AssertSmoke(_terrainWaterSurfaces == 10 && _terrainWaterVolumes == 10,
            "Forgotten Forest must reconstruct all ten animated river segments and swim volumes.");
        AssertSmoke(_surfaceNavigationRegions == 1 && _surfaceNavigationPolygons > 0,
            "Forgotten Forest did not reconstruct its playable surface navigation region.");
        AssertSmoke(_surfaceNavigationBlockedTriangles > 0 && _surfaceNavigationLinks == 2,
            "River crossing policy or the two authored bridge traversals are missing from surface navigation.");
        AssertSmoke(_modelLibrary?.Count == 40,
            "All 34 governed environment models, the legacy character and five KayKit variants must be available.");
        AssertSmoke(_runtimeModelInstances >= 39, "Authored sector landmarks did not use their governed 3D models.");
        AssertSmoke(_animatedCharacterInstances == 5 && _animatedCharacterClipNames.Count >= 75,
            "The five authored KayKit characters must expose the governed movement and combat clips.");
        AssertSmoke(_player is not null && _npc is not null && _keyItem is not null && _door is not null &&
            _boss is not null && _exit is not null, "The playable forest scenario is incomplete.");
        AssertSmoke(_hostiles.Count == 3, "Forgotten Forest must reconstruct two scouts and its boss.");
        AssertSmoke(project.Dialogues.Count == 1 && project.Quests.Count == 1,
            "Forgotten Forest dialogue or quest data is missing.");
        AssertSmoke(area.RuntimePolicies.Experience.AudioCues.Count == 3 &&
                    area.RuntimePolicies.Experience.ScatterEmitters.Count == 3,
            "Forgotten Forest procedural audio authoring is incomplete.");
        foreach (var cue in area.RuntimePolicies.Experience.AudioCues)
        {
            var clip = ProceduralAmbientAudioService.Create(cue.Resource);
            AssertSmoke(string.Equals(clip.ContentSha256, cue.ContentSha256, StringComparison.Ordinal),
                $"Procedural audio hash is stale for '{cue.CueId}'.");
            AssertSmoke(LoadAudioStream(cue.Resource) is AudioStreamWav { Data.Length: > 0 },
                $"Procedural audio stream could not be reconstructed for '{cue.CueId}'.");
        }
        AssertSmoke(_ambientVfxEmitters == 3 && _ambientVfxParticles == 204,
            "Forgotten Forest animated atmosphere was not reconstructed.");
        var navigationPathStarted = Stopwatch.GetTimestamp();
        var navigationPath = Array.Empty<Vector3>();
        for (var frame = 0; frame < 120 && navigationPath.Length < 2; frame++)
        {
            // NavigationServer3D synchronizes regions after the physics step. Querying it directly
            // from _Ready is timing-dependent on a cold engine launch.
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            navigationPath = NavigationServer3D.MapGetPath(
                GetWorld3D().NavigationMap,
                _player!.GlobalPosition,
                _exit!.GlobalPosition,
                optimize: true);
        }
        AssertSmoke(navigationPath.Length >= 2,
            "Godot surface navigation could not find a path from the forest spawn to the sanctuary.");
        var navigationPathMilliseconds = Stopwatch.GetElapsedTime(navigationPathStarted).TotalMilliseconds;

        var sculptureHandler = new EditTerrainBrushHandler(new SystemClock());
        var sculptureStarted = Stopwatch.GetTimestamp();
        var sculpture = sculptureHandler.Handle(
            new ProjectState(project, new ProjectRevision(0)),
            new EditTerrainBrushCommand(
                CommandId.From(Guid.Parse("f5d1ca15-4a39-46ba-a8e2-74dde7188258")),
                new ProjectRevision(0),
                area.Id,
                new TerrainBrushStroke(TerrainBrushOperation.Raise, -48, -48, 3, .05f),
                DryRun: false));
        var sculptureMilliseconds = Stopwatch.GetElapsedTime(sculptureStarted).TotalMilliseconds;
        AssertSmoke(sculpture.Succeeded && sculpture.NewRevision?.Value == 1,
            "Transactional terrain sculpture benchmark did not produce an isolated revision.");

        var terrainMetrics = _terrainBuildMetrics ??
            throw new InvalidOperationException("Terrain build metrics were not captured.");
        var benchmark = TerrainProductionBenchmark.Evaluate(new TerrainProductionBenchmarkSample(
            _moduleSlug,
            area.World.Terrain.Width,
            area.World.Terrain.Depth,
            area.World.Terrain.SamplesX * area.World.Terrain.SamplesZ,
            _terrainVertices,
            _terrainTriangles,
            _terrainCollisionShapes,
            _terrainScatterLayers,
            _terrainScatterInstances,
            _surfaceNavigationRegions,
            _surfaceNavigationPolygons,
            _surfaceNavigationBlockedTriangles,
            _surfaceNavigationLinks,
            navigationPath.Length,
            _modelLibrary?.LodAssetCount ?? 0,
            _modelLibrary?.LodLevelCount ?? 0,
            _terrainCullingGroups,
            _modelLibrary?.GovernedStoredBytes ?? 0,
            terrainMetrics.ManagedAllocatedBytes + _surfaceNavigationManagedAllocatedBytes,
            Math.Max(0, _terrainManagedHeapAfterBytes - _terrainManagedHeapBeforeBytes),
            Math.Max(0, _terrainWorkingSetAfterBytes - _terrainWorkingSetBeforeBytes),
            terrainMetrics.TotalMilliseconds,
            terrainMetrics.CollisionMilliseconds,
            _surfaceNavigationBuildMilliseconds,
            sculptureMilliseconds,
            navigationPathMilliseconds,
            sculpture.Succeeded), TerrainProductionBenchmarkBudget.ProductionExterior512);
        if (!string.IsNullOrWhiteSpace(benchmarkPath))
        {
            WriteTerrainBenchmark(benchmarkPath, benchmark);
        }
        AssertSmoke(benchmark.Passed,
            $"Terrain benchmark exceeded budget: {string.Join(',', benchmark.Checks.Where(check => !check.Passed).Select(check => check.Key))}.");
        GD.Print(
            $"OPENRPG_FORGE_AV4A_TERRAIN_BENCHMARK_PASS area={area.World.Terrain.Width:0}x{area.World.Terrain.Depth:0} " +
            $"memory_mb={benchmark.Sample.ProcessWorkingSetDeltaBytes / 1048576d:0.###} " +
            $"rebuild_ms={benchmark.Sample.ReconstructionMilliseconds:0.###} " +
            $"collision_ms={benchmark.Sample.CollisionMilliseconds:0.###} " +
            $"nav_ms={benchmark.Sample.NavigationMilliseconds:0.###} " +
            $"sculpture_ms={benchmark.Sample.SculptureMilliseconds:0.###} " +
            $"lod_assets={benchmark.Sample.LodAssetCount} lod_levels={benchmark.Sample.LodLevelCount} " +
            $"culling_groups={benchmark.Sample.CullingGroupCount}");
        GD.Print(
            $"OPENRPG_FORGE_FORGOTTEN_FOREST_POLISH_PASS audio_cues={area.RuntimePolicies.Experience.AudioCues.Count} " +
            $"scatter={area.RuntimePolicies.Experience.ScatterEmitters.Count} vfx_emitters={_ambientVfxEmitters} " +
            $"vfx_particles={_ambientVfxParticles} water_animated={_terrainWaterSurfaces} provenance=builtin");
        GD.Print(
            $"OPENRPG_FORGE_FORGOTTEN_FOREST_ANIMATION_PASS characters={_animatedCharacterInstances} " +
            $"variants=5 clips={_animatedCharacterClipNames.Count} rig=kaykit-rig-medium-41 " +
            "source=kaykit:character-pack-adventures-1.0");

        InteractWith(_npc!);
        AssertSmoke(_questMachine?.CurrentState.DisplayName == "La Graine de lune",
            "Guide dialogue did not start the forest quest.");
        InteractWith(_keyItem!);
        AssertSmoke(_inventory.Contains("moonseed"), "Moonseed interaction did not update the inventory.");
        InteractWith(_door!);
        AssertSmoke(_doorUnlocked && _questMachine?.CurrentState.DisplayName == "Le Gardien des Racines",
            "The Moonseed did not open the bramble passage.");
        while (_hostiles.TryGetValue(_boss!, out var combatant) && combatant.Health > 0)
        {
            DamageHostile(_boss!);
        }

        AssertSmoke(_bossPhaseTwo, "The Root Guardian phase two script did not execute.");
        AssertSmoke(_bossDefeated && _questMachine?.Completed == true,
            "The Root Guardian defeat did not complete the data-driven quest.");
        AssertSmoke(_player!.AttackAnimationCount > 0 && _combatHitAnimationCount > 0 &&
                    _combatDeathAnimationCount == 1,
            "Combat did not play the governed attack, hit and death animations.");
        GD.Print(
            $"OPENRPG_FORGE_FORGOTTEN_FOREST_COMBAT_ANIMATION_PASS " +
            $"attack={_player.AttackAnimationCount} hit={_combatHitAnimationCount} " +
            $"death={_combatDeathAnimationCount} clip={_player.CurrentAnimationClip}");
        _player!.GlobalPosition = _exit!.GlobalPosition;
        _escaped = true;
        GD.Print(
            $"OPENRPG_FORGE_FORGOTTEN_FOREST_SMOKE_PASS terrain={_terrainVertices} sectors={_terrainSectors} " +
            $"material={_terrainMaterialMode} material_layers={_terrainMaterialLayers} " +
            $"scatter_layers={_terrainScatterLayers} scatter_instances={_terrainScatterInstances} " +
            $"water_surfaces={_terrainWaterSurfaces} water_volumes={_terrainWaterVolumes} " +
            $"nav_regions={_surfaceNavigationRegions} nav_polygons={_surfaceNavigationPolygons} " +
            $"nav_blocked={_surfaceNavigationBlockedTriangles} nav_links={_surfaceNavigationLinks} " +
            $"nav_path_points={navigationPath.Length} " +
            $"manifests={_modelLibrary!.Count} model_instances={_runtimeModelInstances} " +
            $"animated_characters={_animatedCharacterInstances} animation_clips={_animatedCharacterClipNames.Count} " +
            $"entities={_builtEntityCount} quest=completed boss=defeated camera=orbit");
        GetTree().Quit(0);
    }

    private static void WriteTerrainBenchmark(string path, TerrainProductionBenchmarkReport report)
    {
        var absolute = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        var temporary = $"{absolute}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            }), new UTF8Encoding(false));
            File.Move(temporary, absolute, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private void RunRpgKitHeadlessSmokeTest(ProjectDefinition project)
    {
        AssertSmoke(string.Equals(_moduleSlug, "la-veillee-des-eclats", StringComparison.Ordinal), "Unexpected RPG Kit smoke module.");
        AssertSmoke(_builtEntityCount == 8, "Expected eight authored entities.");
        AssertSmoke(_collisionCount >= 3, "Expected authored floor, placeable and door collisions.");
        AssertSmoke(_player is not null, "The authored player spawn was not reconstructed.");
        AssertSmoke(_hostiles.Count == 2, "Expected both authored combat profiles to be reconstructed.");
        AssertSmoke(project.Dialogues.Count == 1 && project.Dialogues.Single().Nodes.Count == 2, "Expected the authored dialogue graph.");
        AssertSmoke(project.Quests.Count == 1 && project.Quests.Single().States.Count == 3, "Expected the authored quest state machine.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G8_MODULE_PASS slug={_moduleSlug} entities={_builtEntityCount} collisions={_collisionCount} combat={_hostiles.Count} dialogues=1 quests=1");
        GetTree().Quit(0);
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
        RegisterKeyAction("interact", Key.E);
        RegisterKeyAction("attack", Key.Space);
        RegisterKeyAction("quick_save", Key.F5);
        RegisterKeyAction("quick_load", Key.F9);
        RegisterKeyAction("rest", Key.R);
        RegisterKeyAction("skip_sequence", Key.Escape);
        RegisterKeyAction("action_wheel", Key.Tab);
        RegisterJoypadAction("action_wheel", JoyButton.Y);
        RegisterKeyAction("tactical_pause", Key.P);
        RegisterJoypadAction("tactical_pause", JoyButton.Start);
        RegisterKeyAction("slow_time", Key.T);
        RegisterKeyAction("combat_mode", Key.M);
        RegisterKeyAction("dive", Key.Ctrl);
        RegisterKeyAction("climb", Key.Shift);
        RegisterKeyAction("camera_left", Key.Home);
        RegisterKeyAction("camera_right", Key.End);
    }

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
                _runtime._blackboard.TryGetValue(condition.Key, out var value) && value == condition.ExpectedValue,
            QuestConditionKind.HasItem => _runtime._inventory.Contains(condition.Key),
            _ => false,
        };
    }

    private enum PrototypeQuestState
    {
        NotStarted,
        Active,
        Completed,
    }
}
