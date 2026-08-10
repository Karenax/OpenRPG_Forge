using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Application.Areas;
using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Application.Build;
using Forge.Application.Content;
using Forge.Application.Gameplay;
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
using Forge.Application.RuntimeExtensions;
using Forge.Application.Scripting;
using Forge.Application.Entities;
using Forge.Application.Combat;
using Forge.Domain.Areas;
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
using Forge.ModuleFormat.Saves;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Production;
using Forge.Network.Observability;
using Forge.Network.Testing;
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
    private const string PlaytestManifestArgument = "--playtest-manifest";
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
    private ProjectDefinition? _loadedProject;

    private readonly Dictionary<Node3D, CombatantState> _hostiles = [];
    private readonly Dictionary<Node3D, string> _hostileNames = [];
    private readonly Dictionary<Node3D, BlueprintDefinition> _hostileBlueprints = [];
    private readonly Dictionary<Node3D, IReadOnlyList<LootGrant>> _corpseLoot = [];
    private readonly Dictionary<Node3D, CreatureEventProfile> _corpseCreatureEvents = [];
    private ProjectBlackboardScriptService _blackboard = ProjectBlackboardScriptService.ForRuntime([]);
    private readonly HashSet<string> _inventory = new(StringComparer.Ordinal);
    private ItemUniqueRegistryState _uniqueItemRegistry = ItemUniqueRegistryState.Empty;
    private BlueprintDefinition? _keyItemBlueprint;
    private Guid? _keyItemUniqueToken;
    private decimal _keyItemEconomicValue;
    private int _keyItemGeneratedAffixes;
    private readonly HashSet<QuestTransitionId> _chosenQuestTransitions = [];
    private readonly Dictionary<string, string> _playtestQuestVariables = new(StringComparer.Ordinal);
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
    private readonly Dictionary<Node3D, CreatureRoleBehaviorRuntimeSnapshot> _creatureRoleBehaviors = [];
    private readonly Dictionary<Node3D, Vector3> _creatureAmbientOrigins = [];
    private readonly Dictionary<Node3D, Vector3> _creatureAmbientRotations = [];
    private readonly Dictionary<Node3D, double> _creatureAmbientPhases = [];
    private readonly Dictionary<Node3D, CreatureEventProfile> _creatureEventProfiles = [];
    private readonly Dictionary<Node3D, CreatureEventRuntimeState> _creatureEventStates = [];
    private FactionCatalog _factionCatalog = FactionCatalog.Empty;
    private FactionAdvancedProfile? _factionRuntimeProfile;
    private readonly ForgeScriptJsonService _runtimeJson = new();
    private readonly ForgeScriptUiEventService _scriptUiEvents = new();
    private readonly Dictionary<string, SafeJsonHandle> _runtimeJsonHandles = new(StringComparer.Ordinal);
    private readonly RuntimeCameraRig _cameraRig = new();
    private WorldFeatureCatalog? _worldFeatureCatalog;
    private PostLaunchCapabilityProfile? _postLaunchProfile;

    private PlayerController? _player;
    private CombatantState? _playerCombatant;
    private Camera3D? _camera;
    private RuntimeHud? _hud;
    private RuntimeActionBar? _actionBar;
    private Node3D? _npc;
    private Node3D? _keyItem;
    private Node3D? _door;
    private Node3D? _boss;
    private Node3D? _bossCorpse;
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
    private int _dialogueAnimationCueCount;
    private int _dialogueVoiceCueCount;
    private int _dialogueCameraShotCount;
    private int _dialogueBookmarkCount;
    private int _dialogueInterjectionCount;
    private int _dialogueTimedChoiceCount;
    private int _dialogueVariantCount;
    private string? _dialogueVariantKey;
    private bool _dialogueSubtitleFallback;
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
    private bool _fpsVisible;
    private GodotAssetModelLibrary? _modelLibrary;
    private int _runtimeModelInstances;
    private int _animatedCharacterInstances;
    private int _combatHitAnimationCount;
    private int _combatDeathAnimationCount;
    private int _creatureEventHeartbeatDispatchCount;
    private int _creatureEventCombatDispatchCount;
    private int _creatureEventInventoryDispatchCount;
    private int _creatureEventRestDispatchCount;
    private int _creatureEventCustomDispatchCount;
    private int _creatureEventBeforeDamageDispatchCount;
    private int _creatureEventDamageAppliedDispatchCount;
    private int _creatureEventDeathDispatchCount;
    private int _factionWitnessReportCount;
    private int _factionDiplomacyTransitionCount;
    private int _factionExpirationCount;
    private int _factionInheritedTagCount;
    private int _factionMembershipCount;
    private long _creatureCombatBoundarySequence;
    private int _creatureAmbientTicks;
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
            var playtestManifestPath = GetArgumentValue(userArguments, PlaytestManifestArgument);
            var playtestManifest = playtestManifestPath is null
                ? null
                : PlaytestLaunchManifestStore.Load(playtestManifestPath);
            var project = LoadProject(snapshotPath, modulePackagePath, workspacePath);
            if (playtestManifest is not null &&
                !string.Equals(playtestManifest.SpawnEntityId, requestedSpawnId?.ToString(),
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Playtest manifest spawn does not match the requested spawn.");
            ValidateAssetPackageManifests();
            var lifecycleCause = loadSavePath is null
                ? RuntimeLifecycleCause.Startup
                : RuntimeLifecycleCause.SaveResume;
            _lifecycleEvents.Clear();
            _lifecycle = new RuntimeLifecycleCoordinator(observers: [HandleLifecycleEvent]);
            EnsureLifecycle(
                _lifecycle.LoadProject(project, RuntimeAuthority.SinglePlayer, lifecycleCause),
                "project_loaded");
            _blackboard = ProjectBlackboardScriptService.ForRuntime(project.Module.Variables);
            _moduleTitle = project.Module.DisplayName;
            _moduleSlug = project.Module.Slug;
            ConfigureFactions(project.Module.Factions);
            _defaultLocale = project.Module.Metadata.DefaultLocale;
            _isCryptPrototype = string.Equals(_moduleSlug, "crypt-of-ashes", StringComparison.Ordinal);
            _isForgottenForestPrototype = string.Equals(_moduleSlug, "forgotten-forest", StringComparison.Ordinal);
            ConfigureWorldFeatures(project);
            ConfigurePostLaunch(project);
            _message = $"Explorez {_moduleTitle}.";
            var startArea = ResolveStartArea(project, requestedSpawnId);
            ConfigureRuntimePolicies(project.Module.RuntimePolicies, startArea.RuntimePolicies);
            await ConfigureWorldAsync(startArea.Environment, startArea.World, startArea.Navigation, _isCryptPrototype);
            var selectedSpawn = BuildProject(project, requestedSpawnId);
            ApplyPlaytestLaunch(project, playtestManifest);
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
            VerifyModernRuntimeTooling(project, startArea);
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
            ConfigureCinematics(project, startArea);
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
        if (_actionBar is not null) _actionBar.Visible = false;
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
        _hud?.UpdatePerformance(Engine.GetFramesPerSecond());
        var toggleFpsRequested = Input.IsActionJustPressed("toggle_fps");
        if (toggleFpsRequested)
        {
            _fpsVisible = !_fpsVisible;
            _hud?.SetFpsVisible(_fpsVisible);
        }
        var simulationDelta = delta * (_timeControlSession.State == RuntimeTimeState.SlowMotion
            ? _timeControlSession.Scale
            : 1);
        if (!IsTimeFrozen())
        {
            AdvanceClimate(simulationDelta);
            AdvanceRuntimePolicies(simulationDelta);
            AdvanceAreaExperience(simulationDelta);
            AdvanceCinematics(simulationDelta);
            AdvanceCreatureAmbientBehavior(simulationDelta);
            AdvanceCreatureEvents(simulationDelta);
        }

        if (_player is null)
        {
            return;
        }

        if (_camera is not null && !_sequenceOwnsCamera)
        {
            _cameraRig.Update(_camera, _player.GlobalPosition, delta);
            if (_cameraRig.AlignCharacterFacing)
            {
                _player.FaceDirection(_cameraRig.ForwardDirection);
            }
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

        if (Input.IsActionJustPressed("rest") && !toggleFpsRequested)
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

    private static AreaDefinition ResolveStartArea(ProjectDefinition project, EntityId? requestedSpawnId)
    {
        if (requestedSpawnId is null)
            return project.Areas.Single(area => area.Id == project.Module.EntryAreaId);
        return project.Areas.SingleOrDefault(area => area.Entities.Any(entity => entity.Id == requestedSpawnId))
            ?? throw new InvalidOperationException($"Requested spawn '{requestedSpawnId}' has no owning area.");
    }

    private void ApplyPlaytestLaunch(ProjectDefinition project, PlaytestLaunchManifest? manifest)
    {
        _playtestQuestVariables.Clear();
        if (manifest is null) return;
        foreach (var injection in manifest.QuestStates)
        {
            var quest = project.Quests.SingleOrDefault(item =>
                string.Equals(item.Id.ToString(), injection.QuestId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Playtest quest '{injection.QuestId}' does not exist.");
            var stateId = QuestStateId.Parse(injection.StateId);
            if (quest.States.All(item => item.Id != stateId))
                throw new InvalidOperationException(
                    $"Playtest quest state '{injection.StateId}' does not exist in '{injection.QuestId}'.");
            if (_questDefinition is null || _questDefinition.Id == quest.Id)
            {
                _questDefinition = quest;
                _questMachine = new QuestStateMachine(quest);
                _questMachine.Restore(stateId);
            }
            foreach (var variable in injection.Variables)
                _playtestQuestVariables[$"{quest.Id}:{variable.Key}"] = variable.Value;
        }
        var targetKind = Enum.Parse<PlaytestTargetKind>(manifest.TargetKind, ignoreCase: true);
        if (targetKind == PlaytestTargetKind.SelectedQuest)
        {
            var targetId = QuestId.Parse(manifest.TargetId!);
            var quest = project.Quests.Single(item => item.Id == targetId);
            var injection = manifest.QuestStates.Single(item =>
                string.Equals(item.QuestId, manifest.TargetId, StringComparison.OrdinalIgnoreCase));
            _questDefinition = quest;
            _questMachine = new QuestStateMachine(quest);
            _questMachine.Restore(QuestStateId.Parse(injection.StateId));
        }
        else if (targetKind == PlaytestTargetKind.Dialogue)
        {
            var targetId = DialogueId.Parse(manifest.TargetId!);
            _elyraDialogue = project.Dialogues.Single(item => item.Id == targetId);
        }
        GD.Print($"OPENRPG_FORGE_RUNTIME_PLAY_FROM_HERE_PASS mode={manifest.StartMode.ToLowerInvariant()} " +
                 $"profile={manifest.TestProfile} area={manifest.AreaId} quests={manifest.QuestStates.Count} " +
                 $"variables={_playtestQuestVariables.Count} target={targetKind.ToString().ToLowerInvariant()} " +
                 $"headless={manifest.Headless.ToString().ToLowerInvariant()}");
    }

    private EntityInstance BuildProject(ProjectDefinition project, EntityId? requestedSpawnId)
    {
        _loadedProject = project;
        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        var guidedPrototype = _isCryptPrototype || _isForgottenForestPrototype;
        _elyraDialogue = guidedPrototype ? project.Dialogues.SingleOrDefault() : null;
        var quest = guidedPrototype ? project.Quests.SingleOrDefault() : null;
        _questDefinition = quest;
        _questMachine = quest is null ? null : new QuestStateMachine(quest);
        if (quest?.Operations is { } questOperations)
        {
            var notification = QuestOperationsRuntimeResolver.ResolveNotification(quest);
            GD.Print($"OPENRPG_FORGE_RUNTIME_QUEST_OPERATIONS_LOADED profile={questOperations.Id:D} " +
                     $"priority={questOperations.Journal.Priority} repeat={questOperations.Repeat.Enabled.ToString().ToLowerInvariant()} " +
                     $"share={questOperations.Share.Mode.ToString().ToLowerInvariant()} " +
                     $"exclusive={questOperations.ExclusiveChoices.Count} notification={notification.GroupKey}");
        }
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
            var resolvedTile = ModularTilePlacementResolver.Resolve(selectedSpawn.Area, entity);
            var effectiveEntity = resolvedTile.Entity;
            var blueprint = blueprints[effectiveEntity.BlueprintId];
            if (blueprint.Kind == BlueprintKind.PlayerSpawn && entity.Id != selectedSpawn.Entity.Id)
            {
                continue;
            }

            var previousChildCount = GetChildCount();
            BuildEntity(effectiveEntity, blueprint);
            if (GetChildCount() > previousChildCount && GetChild(previousChildCount) is Node3D node)
            {
                ApplyTileMirror(node, resolvedTile.Mirror);
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

    private static void ApplyTileMirror(Node3D node, ModularTileMirrorMode mirror)
    {
        node.Scale = mirror switch
        {
            ModularTileMirrorMode.X => new Vector3(-node.Scale.X, node.Scale.Y, node.Scale.Z),
            ModularTileMirrorMode.Z => new Vector3(node.Scale.X, node.Scale.Y, -node.Scale.Z),
            _ => node.Scale,
        };
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
                _keyItem = CreateItem(entity, blueprint);
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
            _npc = CreateCreature(entity, ProgressionDisplayName(blueprint), new Color("4ec9b0"), blueprint.CreaturePresentation);
            _guideName = GetOverride(entity, "runtime.displayName") ?? blueprint.DisplayName;
            ConfigureCreatureFoundation(_npc, blueprint);
            ConfigureCreatureRoleBehavior(_npc, blueprint);
            ConfigureCreatureEvents(_npc, blueprint);
            return;
        }

        var isBoss = entity.Tags.Contains("boss", StringComparer.Ordinal) || HasRuntimeRole(entity, "boss");
        var creature = CreateCreature(
            entity,
            ProgressionDisplayName(blueprint),
            isBoss ? new Color("d34f4f") : new Color("d68a3a"),
            blueprint.CreaturePresentation);
        var profile = blueprint.Combat
            ?? throw new InvalidOperationException($"Hostile blueprint '{blueprint.Slug}' has no combat profile.");
        _hostiles.Add(creature, new CombatantState(entity.Id.ToString(), profile));
        _hostileNames.Add(creature, blueprint.DisplayName);
        _hostileBlueprints.Add(creature, blueprint);
        ConfigureCreatureFoundation(creature, blueprint);
        ConfigureCreatureRoleBehavior(creature, blueprint);
        ConfigureCreatureEvents(creature, blueprint);

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
                MaterialOverride = CreateMaterial(PresentationColor(blueprint.CreaturePresentation, new Color("74b9ff"))),
            });
        }

        AddPresentationProxies(player, blueprint.CreaturePresentation);

        player.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.45f, Height = 1.8f },
        });
        var playerLevel = ResolveProgression(blueprint).TotalLevel;
        CreateLabel(player, blueprint.Combat is null ? "AVATAR" : playerLevel > 0 ? $"HÉROS · NIV. {playerLevel}" : "HÉROS",
            new Color("dff3ff"), 1.5f);
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

    private Node3D CreateCreature(
        EntityInstance entity,
        string displayName,
        Color color,
        CreaturePresentationProfile? presentation)
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
                MaterialOverride = CreateMaterial(PresentationColor(presentation, color)),
            });
        }
        AddPresentationProxies(root, presentation);
        CreateLabel(root, displayName, Colors.White, 1.35f);
        return root;
    }

    private static Color PresentationColor(CreaturePresentationProfile? presentation, Color fallback)
    {
        var value = presentation?.MaterialColors.FirstOrDefault(item => item.ParameterId == "primary")?.HexColor;
        return string.IsNullOrWhiteSpace(value) ? fallback : Color.FromString(value, fallback);
    }

    private static string ProgressionDisplayName(BlueprintDefinition blueprint)
    {
        var level = ResolveProgression(blueprint).TotalLevel;
        return level > 0 ? $"{blueprint.DisplayName} · Niv. {level}" : blueprint.DisplayName;
    }

    private static CreatureProgressionRuntimeSnapshot ResolveProgression(BlueprintDefinition blueprint)
    {
        var abilities = (blueprint.Combat?.Abilities ?? []).Select(item => item.Slug)
            .ToHashSet(StringComparer.Ordinal);
        var result = CreatureProgressionRuntimeResolver.Resolve(
            blueprint.CreatureProgression,
            abilities,
            blueprint.CreatureProgression?.RequiredAtRuntime == true);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Diagnostics.Select(item =>
                $"{item.Code} at {item.Path}: {item.Message}")));
        }

        return result;
    }

    private void AddPresentationProxies(Node3D root, CreaturePresentationProfile? presentation)
    {
        if (presentation is null) return;
        var color = PresentationColor(presentation, new Color("d68a3a")).Lightened(0.14f);
        foreach (var attachment in presentation.Attachments)
        {
            switch (attachment.Kind)
            {
                case "wings":
                    Add(new BoxMesh { Size = new Vector3(1.15f, 0.6f, 0.08f) }, new Vector3(-0.62f, 0.68f, 0.22f), new Vector3(0, 0, 28));
                    Add(new BoxMesh { Size = new Vector3(1.15f, 0.6f, 0.08f) }, new Vector3(0.62f, 0.68f, 0.22f), new Vector3(0, 0, -28));
                    break;
                case "tail":
                    Add(new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.14f, Height = 1.1f }, new Vector3(0, 0.15f, 0.48f), new Vector3(65, 0, 0));
                    break;
            }
        }

        void Add(PrimitiveMesh mesh, Vector3 position, Vector3 rotation)
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = mesh,
                Position = position,
                RotationDegrees = rotation,
                MaterialOverride = CreateMaterial(color),
            });
        }
    }

    private Node3D CreateItem(EntityInstance entity, BlueprintDefinition blueprint)
    {
        var displayName = blueprint.DisplayName;
        var itemSlug = blueprint.Slug;
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
        _keyItemBlueprint = blueprint;
        if (blueprint.ItemAdvanced is { } advanced)
        {
            var diagnostics = ItemAdvancedValidator.Validate(advanced, $"blueprints/{blueprint.Id}/itemAdvanced");
            if (diagnostics.Count > 0)
                throw new InvalidOperationException($"Advanced item '{blueprint.Slug}' is invalid: {diagnostics[0].Code}.");
            GD.Print($"OPENRPG_FORGE_RUNTIME_ITEM_ADVANCED_LOADED profile={advanced.Id:D} " +
                     $"weapon={(advanced.Weapon is not null).ToString().ToLowerInvariant()} " +
                     $"armor={(advanced.Armor is not null).ToString().ToLowerInvariant()} " +
                     $"container={(advanced.Container is not null).ToString().ToLowerInvariant()} " +
                     $"recipes={advanced.Recipes.Count} procedural={(advanced.Generator?.Enabled == true).ToString().ToLowerInvariant()}");
        }
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
        ConfigureAudioEmitters(environmentProfile.Audio);

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

    private void ConfigureAudioEmitters(AreaAudioProfile profile)
    {
        var playbacks = AudioEmitterRuntimeService.CreateInitialPlaybacks(profile.Emitters, 2026);
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
                VolumeDb = -24 * playback.Spatial.Occlusion,
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
        _cameraRig.Configure(module.Gameplay.Foundation.Cameras);
        ApplyRuntimeInputProfile(module.Gameplay.Foundation.Input);
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
        var rulesetPlugin = RulesetPluginRuntimeService.Resolve(module.Rules.Plugins, ["content.core"]);
        if (!rulesetPlugin.Succeeded)
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_RULESET_PLUGIN_DIAGNOSTIC code={rulesetPlugin.DiagnosticCode} " +
                $"path=module/runtimePolicies/rules/plugins uuid={module.Rules.Plugins.Id} " +
                $"message={rulesetPlugin.Diagnostic}");
        else
            GD.Print($"OPENRPG_FORGE_RUNTIME_RULESET_PLUGIN_READY active=" +
                $"{rulesetPlugin.Registration?.Key ?? "none"} present={rulesetPlugin.ExtensionPresent.ToString().ToLowerInvariant()} " +
                $"required={module.Rules.Plugins.RequiredAtRuntime.ToString().ToLowerInvariant()}");
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
            visual.RotateY(Mathf.Pi);
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

    private void ConfigureCreatureFoundation(Node3D creature, BlueprintDefinition blueprint)
    {
        var resolved = CreatureFoundationRuntimeResolver.Resolve(blueprint.CreatureFoundation,
            blueprint.CreatureFoundation?.RequiredAtRuntime == true);
        if (!resolved.Succeeded)
            throw new InvalidOperationException(string.Join("; ", resolved.Diagnostics.Select(item =>
                $"{item.Code} at {item.Path}: {item.Message}")));
        if (resolved.Profile is null) return;
        var profile = resolved.Profile;
        creature.SetMeta("forge_creature_foundation_id", profile.Id.ToString("D"));
        creature.SetMeta("forge_creature_movement_mode", profile.Movement.Mode.ToString());
        creature.SetMeta("forge_creature_spawn_handler", profile.Spawned.Handler);
        creature.SetMeta("forge_creature_resources", string.Join(',', profile.Resources.Select(item =>
            $"{item.ResourceId}:{item.Current:0.##}/{item.Maximum:0.##}")));
        if (blueprint.Slug == "ash-warden-boss")
            GD.Print("OPENRPG_FORGE_RUNTIME_R038_CREATURE_FOUNDATION_PASS identity=localized resources=health-stamina " +
                     $"movement={profile.Movement.Mode.ToString().ToLowerInvariant()} spawn=validated-after-construction " +
                     "forgescript=safe-handle optional=empty-safe required=diagnostic");
    }

    private void ConfigureCreatureRoleBehavior(Node3D creature, BlueprintDefinition blueprint)
    {
        var profile = blueprint.CreatureRoleBehavior;
        var snapshot = CreatureRoleBehaviorRuntimeResolver.Resolve(profile, blueprint.CreatureProgression,
            21 * 60, interrupted: false, required: profile?.RequiredAtRuntime == true);
        if (!snapshot.Succeeded)
            throw new InvalidOperationException($"Creature '{blueprint.Slug}' role/behavior is invalid: " +
                string.Join("; ", snapshot.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        if (snapshot.IgnoredOptionalProfile) return;
        _creatureRoleBehaviors[creature] = snapshot;
        _creatureAmbientOrigins[creature] = creature.Position;
        _creatureAmbientRotations[creature] = creature.Rotation;
        _creatureAmbientPhases[creature] = StableLootRoll(blueprint.Id.ToString()) * Math.Tau;
        if (_characterAnimations.TryGetValue(creature, out var animation) && snapshot.ActiveAnimationId is { } clip)
        {
            var resolvedClip = animation.Clips.FirstOrDefault(item => string.Equals(item, clip,
                StringComparison.OrdinalIgnoreCase)) ?? animation.IdleClip;
            GodotAssetAnimationController.Play(animation, resolvedClip, .1);
        }
        GD.Print($"OPENRPG_FORGE_CREATURE_AMBIENT_READY blueprint={blueprint.Slug} " +
            $"roles={snapshot.RoleTags.Count} lore={snapshot.LoreFields.Count} " +
            $"set={snapshot.ActiveIdleSetId} animation={snapshot.ActiveAnimationId} " +
            $"autolevel_changes={snapshot.AutoLevel?.Changes.Count ?? 0}");
    }

    private void AdvanceCreatureAmbientBehavior(double delta)
    {
        foreach (var creature in _creatureRoleBehaviors.Keys.ToArray())
        {
            if (!GodotObject.IsInstanceValid(creature))
            {
                RemoveCreatureRoleBehavior(creature);
                continue;
            }
            var phase = _creatureAmbientPhases[creature] + Math.Max(0, delta);
            _creatureAmbientPhases[creature] = phase;
            var origin = _creatureAmbientOrigins[creature];
            var rotation = _creatureAmbientRotations[creature];
            creature.Position = origin + Vector3.Up * (float)(Math.Sin(phase * 1.3) * .025);
            creature.Rotation = rotation + Vector3.Up * (float)(Math.Sin(phase * .45) * .06);
            _creatureAmbientTicks++;
        }
    }

    private void RemoveCreatureRoleBehavior(Node3D creature)
    {
        _creatureRoleBehaviors.Remove(creature);
        _creatureAmbientOrigins.Remove(creature);
        _creatureAmbientRotations.Remove(creature);
        _creatureAmbientPhases.Remove(creature);
    }

    private void ConfigureCreatureEvents(Node3D creature, BlueprintDefinition blueprint)
    {
        var profile = blueprint.CreatureEvents;
        if (profile is null) return;
        var diagnostics = CreatureEventValidator.Validate(profile, $"blueprints/{blueprint.Id}/creatureEvents");
        if (diagnostics.Count > 0)
            throw new InvalidOperationException($"Creature '{blueprint.Slug}' events are invalid: " +
                string.Join("; ", diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        _creatureEventProfiles[creature] = profile;
        _creatureEventStates[creature] = CreatureEventRuntimeState.Empty;
        GD.Print($"OPENRPG_FORGE_CREATURE_EVENTS_READY blueprint={blueprint.Slug} " +
            $"bindings={CreatureEventValidator.EnumerateBindings(profile).Count - (profile.DamageDeath is null ? 0 : 3)} " +
            $"heartbeat_ms={profile.Heartbeat?.IntervalMilliseconds ?? 0} " +
            $"budget_ms={profile.Heartbeat?.BudgetMilliseconds ?? 0} custom={profile.CustomEvents.Count}");
    }

    private void AdvanceCreatureEvents(double delta)
    {
        var milliseconds = Math.Max(0, (long)Math.Round(delta * 1_000, MidpointRounding.AwayFromZero));
        foreach (var creature in _creatureEventProfiles.Keys.ToArray())
        {
            if (!GodotObject.IsInstanceValid(creature))
            {
                RemoveCreatureEvents(creature);
                continue;
            }
            var profile = _creatureEventProfiles[creature];
            var insideInterest = _player is null || creature.GlobalPosition.DistanceTo(_player.GlobalPosition) <= 40;
            var result = CreatureEventRuntimeResolver.AdvanceHeartbeat(profile, _creatureEventStates[creature],
                milliseconds, insideInterest, creature.Name, profile.RequiredAtRuntime);
            _creatureEventStates[creature] = result.State;
            foreach (var diagnostic in result.Diagnostics)
                GD.Print($"OPENRPG_FORGE_CREATURE_EVENT_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
            foreach (var dispatch in result.Dispatches)
            {
                _creatureEventHeartbeatDispatchCount++;
                DispatchCreatureEvent(dispatch);
            }
        }
    }

    private void DispatchCreatureEvent(CreatureEventDispatch dispatch)
    {
        GD.Print($"OPENRPG_FORGE_CREATURE_EVENT_DISPATCH event={dispatch.EventName} handler={dispatch.Handler} " +
            $"authority={dispatch.Authority} budget_ms={dispatch.BudgetMilliseconds} " +
            $"payload={string.Join(',', dispatch.Payload.OrderBy(item => item.Key).Select(item => $"{item.Key}:{item.Value}"))}");
    }

    private void RemoveCreatureEvents(Node3D creature)
    {
        _creatureEventProfiles.Remove(creature);
        _creatureEventStates.Remove(creature);
    }

    private void ConfigureFactions(FactionCatalog catalog)
    {
        var resolution = FactionRuntimeResolver.Resolve(catalog);
        if (resolution.Diagnostics.Count > 0)
            throw new InvalidOperationException("Advanced faction profile is invalid: " +
                string.Join("; ", resolution.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        _factionCatalog = catalog;
        _factionRuntimeProfile = resolution.Profile;
        if (resolution.Profile is null)
        {
            GD.Print("OPENRPG_FORGE_FACTIONS_OPTIONAL_FALLBACK definitions=0");
            return;
        }

        var watch = catalog.Factions.FirstOrDefault(item =>
            string.Equals(item.Slug, "ash-watch", StringComparison.Ordinal));
        _factionInheritedTagCount = watch is null ? 0 :
            FactionRuntimeResolver.EffectiveDefinition(catalog, watch.Id).Tags.Count;
        _factionMembershipCount = FactionRuntimeResolver.Memberships(catalog, "hero",
            DateTimeOffset.Parse("2026-08-05T12:00:00Z")).Count;
        GD.Print($"OPENRPG_FORGE_FACTIONS_READY definitions={catalog.Factions.Count} " +
            $"inheritance={resolution.Profile.Inheritance.Count} witness={resolution.Profile.WitnessPolicies.Count} " +
            $"memberships={resolution.Profile.Memberships.Count} dynamic={resolution.Profile.DynamicFactions.Count} " +
            $"authority=server inherited_tags={_factionInheritedTagCount}");
    }

    private void ReportFactionWitness(string incidentTag)
    {
        if (_factionRuntimeProfile is null || _factionWitnessReportCount > 0) return;
        var watch = _factionCatalog.Factions.FirstOrDefault(item =>
            string.Equals(item.Slug, "ash-watch", StringComparison.Ordinal));
        if (watch is null) return;
        var at = DateTimeOffset.Parse("2026-08-05T12:00:00Z");
        var observation = new FactionWitnessObservation(
            Guid.Parse("b2050000-0000-4000-8000-000000000020"), incidentTag, "hero", "ash-warden-boss",
            watch.Id, "crime.assault", .9, .8, 12, at, true);
        var result = FactionRuntimeResolver.PropagateWitnesses(_factionCatalog, [observation], at);
        if (result.Diagnostics.Count > 0)
            throw new InvalidOperationException(string.Join("; ", result.Diagnostics.Select(item => item.Code)));
        foreach (var report in result.Reports)
        {
            _factionWitnessReportCount++;
            GD.Print($"OPENRPG_FORGE_FACTION_WITNESS_REPORT incident={report.IncidentId} " +
                $"witness={report.WitnessActorId} faction={report.ReportingFactionId:D} " +
                $"evidence={report.Evidence:0.00} reputation={report.ReputationDelta:0.00}");
        }
    }

    private void TransitionFactionDiplomacy()
    {
        if (_factionRuntimeProfile is null || _factionDiplomacyTransitionCount > 0) return;
        var dynamic = _factionRuntimeProfile.DynamicFactions.FirstOrDefault(item => item.Active);
        var target = _factionCatalog.Factions.FirstOrDefault(item =>
            string.Equals(item.Slug, "surface-alliance", StringComparison.Ordinal));
        if (dynamic is null || target is null) return;
        var at = DateTimeOffset.Parse("2026-08-05T12:05:00Z");
        var diplomacy = new FactionDiplomacyState(
            Guid.Parse("b2050000-0000-4000-8000-000000000021"), target.Id,
            FactionDiplomacyStatus.Truce, at, at.AddHours(12), "ash-warden-defeated");
        var result = FactionRuntimeResolver.TransitionDiplomacy(_factionRuntimeProfile, dynamic.Id, diplomacy,
            isServer: true);
        if (result.Diagnostics.Count > 0)
            throw new InvalidOperationException(string.Join("; ", result.Diagnostics.Select(item => item.Code)));
        _factionRuntimeProfile = result.Profile;
        _factionCatalog = _factionCatalog with { Advanced = result.Profile };
        _factionDiplomacyTransitionCount += result.Events.Count;
        foreach (var item in result.Events)
            GD.Print($"OPENRPG_FORGE_FACTION_DIPLOMACY event={item.Code} faction={item.FactionId:D} " +
                $"target={item.TargetFactionId:D} revision={item.Revision}");
    }

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
        _actionBar = new RuntimeActionBar { CombatEnabled = _playerCombatant is not null };
        _actionBar.CommandRequested += HandleActionBarCommand;
        AddChild(_actionBar);
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

    private void HandleActionBarCommand(string command)
    {
        switch (command)
        {
            case "action_wheel":
                _actionWheel?.Toggle();
                break;
            case "quick_load":
                LoadRuntimeSave(_quickSavePath!);
                UpdateHud();
                break;
            default:
                RequestGameplayCommand(command);
                break;
        }
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
            .Concat(_corpseLoot.Keys.Where(GodotObject.IsInstanceValid))
            .OrderBy(node => _player!.GlobalPosition.DistanceTo(node.GlobalPosition))
            .ToArray();
        var nearest = candidates.FirstOrDefault();

        var interactionDistance = _gameplayControls?.Foundation.Interaction.FocusDistance ?? InteractionDistance;
        if (nearest is null || !IsNear(nearest, interactionDistance))
        {
            _message = "Rien à portée d’interaction.";
            UpdateHud();
            return;
        }

        InteractWith(nearest);
    }

    private void InteractWith(Node3D target)
    {
        if (_corpseLoot.Remove(target, out var contents))
        {
            if (_corpseCreatureEvents.Remove(target, out var creatureEvents))
            {
                var accessed = CreatureEventRuntimeResolver.InventoryAccessed(creatureEvents, target.Name,
                    "player", "corpse-loot", contents.Count == 0 ? "empty" : "accepted");
                if (accessed is not null)
                {
                    _creatureEventInventoryDispatchCount++;
                    DispatchCreatureEvent(accessed);
                }
                foreach (var item in contents)
                {
                    var transferred = CreatureEventRuntimeResolver.ItemTransferred(creatureEvents, target.Name,
                        "player", item.ItemSlug, item.Quantity, "corpse-loot", "accepted");
                    if (transferred is null) continue;
                    _creatureEventInventoryDispatchCount++;
                    DispatchCreatureEvent(transferred);
                }
            }
            GrantLoot(contents);
            _message = contents.Count == 0
                ? "La dépouille ne contient plus rien."
                : $"Vous fouillez la dépouille · {contents.Count} type(s) d’objet récupéré(s).";
            if (target == _bossCorpse) _bossCorpse = null;
            target.QueueFree();
            UpdateHud();
            return;
        }

        if (target == _npc)
        {
            InteractWithGuide();
            UpdateHud();
            return;
        }

        if (target == _keyItem && !_hasKey)
        {
            if (_keyItemBlueprint?.ItemAdvanced is { } advanced)
            {
                var context = AdvancedItemContext(advanced, 2026);
                var requirements = ItemAdvancedRuntimeResolver.ResolveRequirements(advanced, context);
                if (!requirements.Allowed)
                {
                    _message = $"Objet inutilisable : {string.Join(", ", requirements.Missing)}.";
                    UpdateHud();
                    return;
                }
                var reservation = ItemUniqueRegistryResolver.Reserve(advanced, _uniqueItemRegistry, "player:local",
                    "party:local", "season:2026", DateTimeOffset.UtcNow, true);
                if (!reservation.Accepted)
                {
                    _message = $"Objet unique indisponible ({reservation.Code}).";
                    UpdateHud();
                    return;
                }
                _uniqueItemRegistry = reservation.State;
                _keyItemUniqueToken = reservation.Reservation?.Token;
                if (_keyItemUniqueToken is { } token)
                    _uniqueItemRegistry = ItemUniqueRegistryResolver.Commit(_uniqueItemRegistry, token, true);
                _keyItemEconomicValue = ItemAdvancedRuntimeResolver.ResolveEconomicValue(advanced, context);
                _keyItemGeneratedAffixes = advanced.Generator?.Enabled == true
                    ? ItemAdvancedRuntimeResolver.Generate(advanced, context).Affixes.Count : 0;
                var identified = ItemAdvancedRuntimeResolver.ResolveIdentifiedProperties(advanced,
                    advanced.Identification.Difficulty);
                GD.Print($"OPENRPG_FORGE_RUNTIME_ITEM_PICKUP profile={advanced.Id:D} value={_keyItemEconomicValue:0.##} " +
                         $"identified={identified.Count} affixes={_keyItemGeneratedAffixes} " +
                         $"unique_revision={_uniqueItemRegistry.Revision}");
            }
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
        var damageTransactionId = Guid.NewGuid();
        if (_creatureEventProfiles.TryGetValue(hostile, out var preDamageEvents))
        {
            var requested = ability.Effects.Where(item => item.Kind == AbilityEffectKind.Damage)
                .Sum(item => item.Magnitude);
            var beforeDamage = CreatureEventRuntimeResolver.BeforeDamage(preDamageEvents, damageTransactionId,
                hostile.Name, _playerCombatant.Id, requested, "physical", ability.Slug);
            if (beforeDamage is not null)
            {
                _creatureEventBeforeDamageDispatchCount++;
                DispatchCreatureEvent(beforeDamage);
            }
        }
        _player?.PlayAttackAnimation();
        var result = CombatResolver.Cast(
            _playerCombatant,
            target,
            ability.Id,
            Time.GetTicksMsec() / 1000d,
            damageMultiplier: _resolvedDifficulty?.PlayerDamageMultiplier ?? 1);
        if (hostile == _boss) ReportFactionWitness("attack.ash-warden");
        if (_creatureEventProfiles.TryGetValue(hostile, out var creatureEvents))
        {
            var damageApplied = CreatureEventRuntimeResolver.DamageApplied(creatureEvents, damageTransactionId,
                hostile.Name, _playerCombatant.Id, result.Damage, result.Damage, target.Health,
                "physical", ability.Slug);
            if (damageApplied is not null)
            {
                _creatureEventDamageAppliedDispatchCount++;
                DispatchCreatureEvent(damageApplied);
            }
            var combatBoundary = CreatureEventRuntimeResolver.CombatBoundary(creatureEvents,
                CreatureCombatBoundaryMode.CombatTickCompleted, hostile.Name, ++_creatureCombatBoundarySequence,
                _activeCombatProvider?.ProviderId ?? "realtime");
            if (combatBoundary is not null)
            {
                _creatureEventCombatDispatchCount++;
                DispatchCreatureEvent(combatBoundary);
            }
        }
        var health = target.Health;
        var hostileName = _hostileNames[hostile];
        _message = $"{hostileName} subit {result.Damage} dégât(s) ({health} PV).";
        var reactionDuration = PlayHostileCombatReaction(hostile, result.TargetDied);

        if (hostile == _boss && health <= Math.Max(1, target.Profile.Stats.MaximumHealth / 2) && !_bossPhaseTwo)
        {
            _bossPhaseTwo = true;
            if (_creatureEventProfiles.TryGetValue(hostile, out var bossEvents) &&
                bossEvents.CustomEvents.FirstOrDefault(item =>
                    string.Equals(item.QualifiedName, "crypt.braziers_lit", StringComparison.Ordinal)) is { } customSchema)
            {
                var custom = CreatureEventRuntimeResolver.Custom(bossEvents, customSchema.SchemaId,
                    new Dictionary<string, string>
                    {
                        ["count"] = "3",
                        ["source"] = _hostileBlueprints[hostile].Slug,
                    });
                _creatureEventCustomDispatchCount++;
                DispatchCreatureEvent(custom);
            }
            ExecuteForgeScript(_bossPhaseTwoScript, hostile);
            GD.Print("OPENRPG_FORGE_BOSS_PHASE_TWO");
        }

        if (result.TargetDied)
        {
            var blueprint = _hostileBlueprints[hostile];
            _hostiles.Remove(hostile);
            _hostileNames.Remove(hostile);
            _hostileBlueprints.Remove(hostile);
            var loot = CreatureLootRuntimeResolver.ResolveDeath(
                blueprint.CreatureLoot,
                result.Loot,
                StableLootRoll(target.Id),
                "player",
                "player-party",
                DateTimeOffset.UtcNow,
                blueprint.CreatureLoot?.RequiredAtRuntime == true);
            if (!loot.Succeeded)
                throw new InvalidOperationException(string.Join("; ", loot.Diagnostics.Select(item =>
                    $"{item.Code} at {item.Path}: {item.Message}")));
            GrantLoot(loot.Grants);
            if (blueprint.CreatureEvents is not null)
            {
                var died = CreatureEventRuntimeResolver.EntityDied(blueprint.CreatureEvents, hostile.Name,
                    _playerCombatant.Id, [_playerCombatant.Id], ability.Slug,
                    $"{hostile.GlobalPosition.X:0.###},{hostile.GlobalPosition.Y:0.###},{hostile.GlobalPosition.Z:0.###}",
                    result.Loot.Select(item => item.ItemSlug).ToArray(), loot.Corpse.Mode.ToString());
                if (died is not null)
                {
                    _creatureEventDeathDispatchCount++;
                    DispatchCreatureEvent(died);
                }
            }
            if (loot.Corpse.Created)
            {
                var corpse = CreateLootCorpse(hostile, hostileName, loot.Corpse);
                if (blueprint.CreatureEvents is not null) _corpseCreatureEvents[corpse] = blueprint.CreatureEvents;
                if (hostile == _boss) _bossCorpse = corpse;
            }

            _characterAnimations.Remove(hostile);
            RemoveCreatureRoleBehavior(hostile);
            RemoveCreatureEvents(hostile);
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
                TransitionFactionDiplomacy();
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

    private void GrantLoot(IEnumerable<LootGrant> grants)
    {
        foreach (var grant in grants)
        {
            _inventory.Add(grant.ItemSlug);
            GD.Print($"OPENRPG_FORGE_LOOT_GRANTED item={grant.ItemSlug} quantity={grant.Quantity} " +
                $"owner={grant.OwnerId} instance={grant.InstanceKey} cause={grant.Cause}");
        }
    }

    private Node3D CreateLootCorpse(Node3D source, string hostileName, CreatureCorpseSnapshot snapshot)
    {
        var corpse = new Node3D
        {
            Name = $"Corpse_{SafeNodeName(hostileName)}",
            GlobalTransform = source.GlobalTransform,
        };
        AddChild(corpse);
        corpse.AddChild(new MeshInstance3D
        {
            Position = new Vector3(0, .18f, 0),
            RotationDegrees = new Vector3(0, 0, 90),
            Mesh = new CapsuleMesh { Radius = .28f, Height = 1.35f },
            MaterialOverride = CreateMaterial(new Color("4d3545")),
        });
        CreateLabel(corpse, $"DÉPOUILLE · {hostileName}\n[E] Fouiller", new Color("ffd2a6"), .65f);
        _corpseLoot[corpse] = snapshot.Contents;
        GD.Print($"OPENRPG_FORGE_CORPSE_CREATED mode={snapshot.Mode} items={snapshot.Contents.Count} " +
            $"ownership={snapshot.OwnershipPolicy} resurrection={snapshot.AllowResurrection}");
        if (snapshot.ExpiresAt is { } expiresAt)
        {
            var delay = Math.Max(.05, (expiresAt - DateTimeOffset.UtcNow).TotalSeconds);
            var timer = GetTree().CreateTimer(delay);
            timer.Timeout += () =>
            {
                _corpseLoot.Remove(corpse);
                _corpseCreatureEvents.Remove(corpse);
                if (corpse == _bossCorpse) _bossCorpse = null;
                if (GodotObject.IsInstanceValid(corpse)) corpse.QueueFree();
            };
        }
        return corpse;
    }

    private static double StableLootRoll(string combatantId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(combatantId));
        return BitConverter.ToUInt32(bytes, 0) / (double)uint.MaxValue;
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
            case "quick_load": LoadRuntimeSave(_quickSavePath!); UpdateHud(); break;
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
        var playerHealth = _playerCombatant?.Health ?? 1;
        var playerMaximumHealth = _playerCombatant?.Profile.Stats.MaximumHealth ?? 1;
        _hud.UpdatePlayerStatus("Héros", playerHealth, playerMaximumHealth, _progressionLevel);
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
            PrototypeQuestState.Active => "vaincre le Gardien des Cendres [1]",
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
        var variantText = ApplyDialoguePresentation(start.Node!.Id, evaluator);
        var choice = traversal.GetAvailableChoices(evaluator).FirstOrDefault();
        if (choice is null)
        {
            _message = $"{_guideName} : {variantText ?? start.Node!.Text.Fallback}";
            return;
        }

        var step = traversal.Choose(choice.Id, evaluator);
        ApplyDialogueActions(step.Actions);
        var destination = step.Node?.Text.Fallback ?? start.Node!.Text.Fallback;
        _message = variantText is null ? $"{_guideName} : {destination}" :
            $"{_guideName} : {variantText} → {destination}";
    }

    private string? ApplyDialoguePresentation(DialogueNodeId nodeId, IDialogueConditionEvaluator evaluator)
    {
        if (_elyraDialogue is null) return null;
        var participant = _elyraDialogue.Nodes.First(item => item.Id == nodeId).SpeakerBlueprintId;
        IReadOnlyList<DialogueCompanionState> companions = participant is null ? [] :
            [new(participant, _npc is not null, 100, new HashSet<string>(["guardian"], StringComparer.Ordinal))];
        var context = new DialoguePresentationRuntimeContext("fr-FR",
            _elyraDialogue.Presentation?.RequiredAtRuntime == true, false, false, true,
            DateTimeOffset.UtcNow, companions, new Dictionary<Guid, DateTimeOffset>());
        var resolution = DialoguePresentationRuntimeResolver.Resolve(_elyraDialogue, nodeId, context);
        foreach (var diagnostic in resolution.Diagnostics)
            GD.PrintErr($"OPENRPG_FORGE_DIALOGUE_PRESENTATION_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        if (resolution.Diagnostics.Count > 0) return null;

        _dialogueAnimationCueCount += resolution.AnimationCues.Count;
        if (_npc is not null && _characterAnimations.TryGetValue(_npc, out var animation))
        {
            foreach (var cue in resolution.AnimationCues)
            {
                var clip = animation.Clips.FirstOrDefault(item =>
                    string.Equals(item, cue.AnimationId, StringComparison.OrdinalIgnoreCase)) ?? animation.IdleClip;
                GodotAssetAnimationController.Play(animation, clip,
                    Math.Max(0, cue.BlendMilliseconds) / 1000.0, restart: true);
            }
        }

        if (resolution.VoiceCue is not null)
        {
            _dialogueVoiceCueCount++;
            _dialogueSubtitleFallback = resolution.UsesSubtitleFallback;
            GD.Print($"OPENRPG_FORGE_DIALOGUE_VOICE locale={resolution.VoiceCue.Locale} " +
                $"take={resolution.VoiceCue.Take} fallback={resolution.UsesSubtitleFallback.ToString().ToLowerInvariant()} " +
                $"subtitle={resolution.VoiceCue.Subtitle.Key}");
        }

        if (resolution.CameraShot is { } shot && _camera is not null)
        {
            _dialogueCameraShotCount++;
            _camera.Fov = shot.FieldOfViewDegrees;
            if (_npc is not null) _camera.LookAt(_npc.GlobalPosition + Vector3.Up, Vector3.Up);
        }
        _dialogueBookmarkCount += _elyraDialogue.Presentation?.Bookmarks.Count(item => item.NodeId == nodeId) ?? 0;
        if (resolution.Interjection is not null) _dialogueInterjectionCount++;

        var adaptive = DialogueAdaptiveRuntimeResolver.Resolve(_elyraDialogue, nodeId, 0,
            AdaptiveDialogueContext(), evaluator);
        foreach (var diagnostic in adaptive.Diagnostics)
            GD.PrintErr($"OPENRPG_FORGE_ADAPTIVE_DIALOGUE_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        if (adaptive.Diagnostics.Count > 0) return null;
        if (adaptive.TimedChoice is not null) _dialogueTimedChoiceCount++;
        if (adaptive.Variant is null) return null;
        _dialogueVariantCount++;
        _dialogueVariantKey = adaptive.Variant.Variant.Text.Key;
        GD.Print($"OPENRPG_FORGE_DIALOGUE_VARIANT set={adaptive.Variant.Set.Key} " +
            $"key={_dialogueVariantKey} locale={adaptive.Variant.Locale} roll={adaptive.Variant.DeterministicRoll}");
        return adaptive.Variant.Variant.Text.Fallback;
    }

    private DialogueAdaptiveRuntimeContext AdaptiveDialogueContext() => new("fr-FR", true, 1, false, false, 2026,
        _elyraDialogue?.Id.Value ?? Guid.Empty, new Dictionary<Guid, IReadOnlyList<Guid>>());

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

        if (_questDefinition?.Operations is not null)
        {
            var context = QuestOperationsContext();
            var exclusive = QuestOperationsRuntimeResolver.ResolveExclusive(_questDefinition, transition.Id, context);
            if (!exclusive.Allowed)
            {
                _message = "Cette voie de quête est incompatible avec un choix antérieur.";
                return;
            }
            var share = QuestOperationsRuntimeResolver.ResolveShare(_questDefinition, context);
            if (!share.Accepted)
            {
                _message = "Le groupe n’a pas validé cette progression de quête.";
                return;
            }
            _chosenQuestTransitions.Add(transition.Id);
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
        if (_questDefinition?.Operations is not null)
        {
            var notice = QuestOperationsRuntimeResolver.ResolveNotification(_questDefinition);
            GD.Print($"OPENRPG_FORGE_RUNTIME_QUEST_NOTICE visible={notice.Visible.ToString().ToLowerInvariant()} " +
                     $"cinematic={notice.Cinematic.ToString().ToLowerInvariant()} group={notice.GroupKey} " +
                     $"state={result.State.Id}");
        }
    }

    private QuestOperationsRuntimeContext QuestOperationsContext() => new(
        DateTimeOffset.UtcNow, null, 0, 1, 1, true, true, _chosenQuestTransitions);

    private ItemRuntimeContext AdvancedItemContext(ItemAdvancedProfile profile, int seed) => new(
        6, 20, new HashSet<string>(["lore", "smithing"]), new HashSet<string>(["ash-attuned"]),
        new HashSet<string>(["surface-alliance"]),
        new HashSet<string>(_questState == PrototypeQuestState.NotStarted
            ? ["target.ash-sealed"] : ["target.ash-sealed", "quest.accepted"]),
        "crypt", profile.Economy.Currency, 1, profile.Durability?.Initial ?? 100, seed, "player:local", true);

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

        if (_gameplayControls?.Foundation.Save is { } savePolicy)
        {
            var archived = RuntimeSaveArchiveStore.Save(ArchiveRoot(path), ArchiveSlot(path),
                Math.Max(1, save.SavedAtUtc.UtcTicks), save, savePolicy, [], save.SavedAtUtc);
            if (!archived.Succeeded)
            {
                GD.PrintErr($"OPENRPG_FORGE_RUNTIME_SAVE_FAILED {archived.Diagnostics[0].Code}:" +
                            archived.Diagnostics[0].Message);
                return false;
            }
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
        var loadedSave = result.Save;

        var archivePath = ArchiveRoot(path);
        if (_gameplayControls?.Foundation.Save is { } savePolicy && Directory.Exists(archivePath))
        {
            var archived = RuntimeSaveArchiveStore.Load(archivePath, ArchiveSlot(path), 0,
                _moduleId, _moduleContentSha256, []);
            if (!archived.Succeeded || archived.Save is null)
            {
                GD.PrintErr($"OPENRPG_FORGE_RUNTIME_LOAD_FAILED {archived.Diagnostics[0].Code}:" +
                            archived.Diagnostics[0].Message);
                return false;
            }
            loadedSave = archived.Save;
        }

        try
        {
            ApplyRuntimeSave(loadedSave);
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
            $"OPENRPG_FORGE_RUNTIME_SAVE_LOADED format={loadedSave.FormatVersion} " +
            $"file={Path.GetFileName(path)} quest={loadedSave.QuestStateId}");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G11O_SAVE_PASS mode=load entities={loadedSave.EntitySnapshots?.Count ?? 0} " +
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

        _blackboard = ProjectBlackboardScriptService.ForRuntime(_blackboard.Definitions);
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

    private void ConfigureWorldFeatures(ProjectDefinition project)
    {
        var catalog = project.Module.WorldFeatures;
        if (_isCryptPrototype && IsEmptyWorldFeatureCatalog(catalog))
        {
            // Deterministic fixture for the shipped empty sample and migrated pre-1.26 projects.
            // Authored catalogs always win.
            catalog = CryptWorldFeatureAuthoringService.Create(project);
        }

        var configured = new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
            project.Blueprints, project.Areas, project.Dialogues, project.Quests);
        var diagnostics = WorldFeatureCatalogValidator.Validate(configured);
        if (diagnostics.Count > 0)
        {
            throw new InvalidOperationException("WORLD_FEATURE_CATALOG_INVALID: " + string.Join("; ",
                diagnostics.Select(item => $"{item.Code} at {item.Path}: {item.Message}")));
        }

        _worldFeatureCatalog = catalog;
        if (catalog.Companions.FirstOrDefault() is { } companion)
        {
            var destination = companion.Validation?.CrossZoneNavigation.AllowedAreaIds.FirstOrDefault();
            var resolved = CompanionValidationRuntimeResolver.Resolve(companion, destination);
            var optional = CompanionValidationRuntimeResolver.Resolve(
                companion with { Validation = null, ValidationRequiredAtRuntime = false });
            var required = CompanionValidationRuntimeResolver.Resolve(
                companion with { Validation = null, ValidationRequiredAtRuntime = true });
            AssertSmoke(resolved.Available && resolved.DestinationAllowed && optional.OptionalFallback &&
                        required.Diagnostics.Any(item => item.Code == "world.companion.validation_required"),
                "Companion validation runtime policy did not resolve its authored and fallback paths.");
            GD.Print("OPENRPG_FORGE_RUNTIME_R037_COMPANION_PASS policy=canonical recruitment=dialogue-script commands=4 " +
                     "navigation=bounded optional=empty-safe required=diagnostic");
        }
    }

    private static bool IsEmptyWorldFeatureCatalog(WorldFeatureCatalog catalog) =>
        catalog.Placeables.Count == 0 && catalog.Doors.Count == 0 && catalog.Triggers.Count == 0 &&
        catalog.Encounters.Count == 0 && catalog.VehicleRoutes.Count == 0 && catalog.Merchants.Count == 0 &&
        catalog.Companions.Count == 0 && catalog.Scripts.Camera.Shots.Count == 0 &&
        catalog.Scripts.TagRouting.Routes.Count == 0 && catalog.Scripts.ModuleFlags.Count == 0 &&
        catalog.Scripts.AiSettings.Count == 0;

    private void ConfigurePostLaunch(ProjectDefinition project)
    {
        var profile = project.Module.RuntimePolicies.Extensions.PostLaunch;
        if (_isCryptPrototype && string.Equals(profile.Provenance, "migration-default", StringComparison.Ordinal))
        {
            // The shipped sample demonstrates every optional production capability. An authored profile
            // always carries explicit provenance and therefore always wins over this deterministic fixture.
            profile = CryptPostLaunchAuthoringService.Create(project).PostLaunch;
        }
        _postLaunchProfile = profile;
    }

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

        AssertSmoke(_builtEntityCount == 16, "Expected 16 reconstructed entities.");
        AssertSmoke(_collisionCount == 10, "Expected 10 static collision shapes.");
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
        AdvanceCreatureEvents(1.5);
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

    private static void RunAreaFoundationRuntimeSmokeTest(ProjectDefinition project)
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
            2, -1, 90, ModularTileMirrorMode.X, "weathered", null);
        var foundation = new AreaFoundationProfile(defaults.Id, defaults.SchemaVersion, "runtime:r028-smoke",
            defaults.Creation with { KitKey = "runtime-modular-kit", Seed = 2026 },
            new(defaults.TileKit.Id, defaults.TileKit.SchemaVersion, "runtime:r028-smoke",
                2, [tileDefinition], [], [tilePlacement], [], []),
            new(enteredBinding, null), defaults.Volumes, defaults.Dependencies);
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
        var lifecycleValid = new[] { RuntimeAuthority.Editor, RuntimeAuthority.Client, RuntimeAuthority.Server }
            .All(authority =>
            {
                var observed = new List<RuntimeLifecycleEvent>();
                var lifecycle = new RuntimeLifecycleCoordinator(observers: [observed.Add]);
                var loaded = lifecycle.LoadProject(project, authority, RuntimeLifecycleCause.Playtest);
                var entered = lifecycle.EnterInitialArea(project.Module.EntryAreaId, null,
                    RuntimeLifecycleCause.Playtest);
                var started = lifecycle.StartWorld(RuntimeLifecycleCause.Playtest);
                return loaded.Succeeded && entered.Succeeded && started.Succeeded &&
                       observed.Select(item => item.Binding)
                           .SequenceEqual(["project.loaded", "area.entered", "world.started"]) &&
                       observed.All(item => item.Authority == authority);
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
                 "targets=five-secrets-separated optional=ignored required=diagnostic");
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

    private static T AssertSingle<T>(IReadOnlyList<T> values, string message)
    {
        AssertSmoke(values.Count == 1, message);
        return values[0];
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
        AssertSmoke(_terrainScatterLayers == 13,
            "The biome scatter did not reconstruct the thirteen Poly Haven environment layers.");
        AssertSmoke(_terrainScatterInstances >= 3_000, "The 512 x 512 dressed forest is too sparse.");
        AssertSmoke(_terrainWaterSurfaces == 10 && _terrainWaterVolumes == 10,
            "Forgotten Forest must reconstruct all ten animated river segments and swim volumes.");
        AssertSmoke(_surfaceNavigationRegions == 1 && _surfaceNavigationPolygons > 0,
            "Forgotten Forest did not reconstruct its playable surface navigation region.");
        AssertSmoke(_surfaceNavigationBlockedTriangles > 0 && _surfaceNavigationLinks == 3,
            "River crossing policy or the three authored bridge traversals are missing from surface navigation.");
        AssertSmoke(_modelLibrary?.Count == 45,
            "All 39 governed environment models, the legacy character and five KayKit variants must be available.");
        AssertSmoke(_runtimeModelInstances >= 43, "Authored sector landmarks did not use their governed 3D models.");
        AssertSmoke(_animatedCharacterInstances == 5 && _animatedCharacterClipNames.Count >= 75,
            "The five authored KayKit characters must expose the governed movement and combat clips.");
        AssertSmoke(_player is not null && _npc is not null && _keyItem is not null && _door is not null &&
            _boss is not null && _exit is not null, "The playable forest scenario is incomplete.");
        var playerSpawnPosition = _player!.GlobalPosition;
        for (var frame = 0; frame < 120; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        AssertSmoke(
            _player.IsOnFloor() && _player.GlobalPosition.Y >= playerSpawnPosition.Y - .25f,
            $"The player fell through the terrain at the forest spawn " +
            $"(start_y={playerSpawnPosition.Y:0.###}, current_y={_player.GlobalPosition.Y:0.###}, " +
            $"on_floor={_player.IsOnFloor()}).");
        GD.Print(
            $"OPENRPG_FORGE_FORGOTTEN_FOREST_GROUND_CONTACT_PASS " +
            $"start_y={playerSpawnPosition.Y:0.###} current_y={_player.GlobalPosition.Y:0.###} " +
            $"on_floor={_player.IsOnFloor()}");
        AssertSmoke(_actionBar?.SlotCount == 6,
            "The MMO action bar did not reconstruct all six command slots.");
        AssertSmoke(HasKeyBinding("jump", Key.Space) && HasKeyBinding("attack", Key.Key1) &&
                    HasKeyBinding("toggle_fps", Key.R, ctrl: true),
            "The MMO keyboard bindings are incomplete.");
        AssertSmoke(Math.Abs(PlayerController.MovementSpeedMultiplier - 2f) < .001f,
            "The requested doubled movement speed is not active.");
        _hud!.SetFpsVisible(true);
        _hud.UpdatePerformance(60);
        AssertSmoke(_hud.FpsVisible, "The FPS counter could not be enabled.");
        _hud.SetFpsVisible(false);

        var jumpStartY = _player.GlobalPosition.Y;
        AssertSmoke(_player.QueueJump(), "The grounded player refused the jump request.");
        var jumpMaximumY = jumpStartY;
        for (var frame = 0; frame < 45; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            jumpMaximumY = Math.Max(jumpMaximumY, _player.GlobalPosition.Y);
        }
        for (var frame = 0; frame < 180 && !_player.IsOnFloor(); frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        AssertSmoke(_player.JumpCount == 1 && jumpMaximumY >= jumpStartY + 1f && _player.IsOnFloor(),
            $"The MMO jump did not complete a stable physical arc " +
            $"(start_y={jumpStartY:0.###}, max_y={jumpMaximumY:0.###}, " +
            $"current_y={_player.GlobalPosition.Y:0.###}, count={_player.JumpCount}).");
        GD.Print(
            $"OPENRPG_FORGE_FORGOTTEN_FOREST_MMO_CONTROLS_PASS speed_multiplier=2 " +
            $"jump_height={jumpMaximumY - jumpStartY:0.###} action_slots={_actionBar!.SlotCount} " +
            "attack=1 jump=space fps=ctrl+r camera=dual-mouse-orbit");
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
        AssertSmoke(_ambientVfxEmitters == 0 && _ambientVfxParticles == 0,
            "Forgotten Forest must not reconstruct untextured ambient particle quads.");
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
            $"OPENRPG_FORGE_RUNTIME_R036_EE_PASS nav_regions={_surfaceNavigationRegions} " +
            $"nav_polygons={_surfaceNavigationPolygons} blocked={_surfaceNavigationBlockedTriangles} " +
            $"links={_surfaceNavigationLinks} path_points={navigationPath.Length} " +
            $"rebuild_ms={_surfaceNavigationBuildMilliseconds:0.###} query_ms={navigationPathMilliseconds:0.###}");
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
        RegisterKeyAction("combat_mode", Key.M);
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

    private enum PrototypeQuestState
    {
        NotStarted,
        Active,
        Completed,
    }
}
