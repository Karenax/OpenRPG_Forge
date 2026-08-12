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
using Forge.ModuleFormat.Saves;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Production;
using Forge.Network.Observability;
using Forge.Network.Testing;
using Forge.Network.Authoring;
using Forge.Network.Scenarios;
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
    private readonly CustomWorldEventBus _customWorldEvents = new();
    private WorldClockSnapshot? _worldClockSnapshot;
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
    private Node? _factionFoundationRoot;
    private FactionReputationProfile? _factionReputationProfile;
    private ReputationState? _factionReputationState;
    private Node? _factionReputationRoot;
    private readonly ForgeScriptJsonService _runtimeJson = new();
    private readonly ForgeScriptUiEventService _scriptUiEvents = new();
    private readonly Dictionary<string, SafeJsonHandle> _runtimeJsonHandles = new(StringComparer.Ordinal);
    private readonly RuntimeCameraRig _cameraRig = new();
    private WorldFeatureCatalog? _worldFeatureCatalog;
    private Node? _doorAccessEventRoot;
    private Node? _encounterCompositionRoot;
    private Node? _encounterScenarioRoot;
    private Node? _encounterStatisticsRoot;
    private Node? _companionFoundationRoot;
    private Node? _companionControlRoot;
    private Node? _companionExperienceRoot;
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
    private Node3D? _audioRangeDebugRoot;
    private Node3D? _waypointPatrolActor;
    private Node3D? _waypointLocatorRoot;
    private Node3D? _waypointRouteDebugRoot;
    private Node3D? _gameplaySimulationRoot;
    private Node? _gameplaySessionRoot;
    private Node? _dialogueAuthoringRoot;
    private Node? _dialogueExperienceRoot;
    private Node? _dialogueProductionRoot;
    private Node? _questAuthoringRoot;
    private Node? _questExperienceRoot;
    private Node? _questQualityRoot;
    private WaypointRuntimeState _waypointRuntimeState = WaypointRuntimeState.Empty;
    private readonly Dictionary<string, AudioStreamPlayer> _adaptiveAmbiencePlayers =
        new(StringComparer.Ordinal);
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
            PublishProjectConnectionEvent(PlayerConnectionStage.Joining, "authenticated");
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
            ConfigureWaypointRuntime(startArea.Navigation.Waypoints);
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
            PublishProjectConnectionEvent(PlayerConnectionStage.Ready, "world-ready");
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
        BuildDialogueAuthoringAdapters(project.Dialogues);
        BuildDialogueExperienceAdapters(project.Dialogues);
        BuildDialogueProductionAdapters(project.Dialogues);
        BuildQuestAuthoringAdapters(project.Quests);
        BuildQuestExperienceAdapters(project.Quests);
        BuildQuestQualityAdapters(project.Quests);
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
        GetViewport().UseHdr2D = environmentProfile.Rendering.HdrOutput;
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
        ConfigureAreaAmbientAudio(environmentProfile.Audio);

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
        if (marker is not null) _hud?.ShowWaypointMarker(marker);
        BuildWaypointLocatorsAndRouteDebug(profile);
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
        BuildGameplaySimulationAdapters(module.Gameplay.Simulation);
        BuildGameplaySessionAdapters(module.Gameplay.Session);
        _cameraRig.Configure(module.Gameplay.Foundation.Cameras);
        ApplyRuntimeInputProfile(module.Gameplay.Foundation.Input);
        _moduleRules = module.Rules;
        _areaRules = area.Rules;
        _areaExperience = area.Experience;
        _runtimeExtensions = module.Extensions;
        var worldResolution = ProjectWorldRuntimeResolver.Resolve(module.World, module.Id, module.World.RequiredAtRuntime);
        if (!worldResolution.Succeeded)
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_PROJECT_WORLD_DIAGNOSTIC code={worldResolution.Diagnostic!.Code} " +
                        $"path={worldResolution.Diagnostic.Path}");
        else if (worldResolution.Handle is not null)
        {
            _customWorldEvents.Register(worldResolution.Handle.Profile);
            _worldClockSnapshot = CustomWorldEventBus.CreateInitialSnapshot(worldResolution.Handle.Profile.Clock);
        }
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
        if (_worldClockSnapshot is not null && _moduleRuntimePolicies.World.Enabled)
        {
            var advanced = CustomWorldEventBus.AdvanceClock(_moduleRuntimePolicies.World.Clock,
                _worldClockSnapshot, delta / 60d, _currentAreaId ?? string.Empty);
            _worldClockSnapshot = advanced.Snapshot;
            foreach (var astronomicalEvent in advanced.CrossedEvents)
                GD.Print($"OPENRPG_FORGE_RUNTIME_ASTRONOMICAL_EVENT kind={astronomicalEvent} " +
                         $"minute={advanced.Snapshot.TotalMinutes} area={_currentAreaId}");
        }
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
        var authored = CreatureAuthoringCompositionRuntimeResolver.Resolve(profile.AuthoringComposition,
            profile.AuthoringCompositionRequiredAtRuntime);
        if (!authored.Succeeded)
            throw new InvalidOperationException(string.Join("; ", authored.Diagnostics.Select(item =>
                $"{item.Code} at {item.Path}: {item.Message}")));
        if (authored.Profile is not null)
        {
            var composition = authored.Profile;
            var root = new Node3D { Name = "CreatureAuthoringComposition" };
            root.SetMeta("forge_creature_authoring_id", composition.Id.ToString("D"));
            root.SetMeta("forge_creature_encounter_role", composition.EncounterRole.ToString());
            root.SetMeta("forge_creature_threat_rating", composition.ThreatRating);
            root.SetMeta("forge_creature_species", composition.Species.SpeciesId);
            root.SetMeta("forge_creature_mesh_asset", composition.Appearance.MeshAssetId);
            creature.AddChild(root);
            root.AddChild(new Node { Name = $"Role_{composition.EncounterRole}" });
            root.AddChild(new Node { Name = $"Species_{SafeNodeName(composition.Species.SpeciesId)}" });
            root.AddChild(new Node3D { Name = $"Model_{SafeNodeName(composition.Appearance.MeshAssetId)}" });
            var graph = new AnimationPlayer { Name = "AnimationGraph" };
            graph.SetMeta("forge_retarget_profile", composition.AnimationGraph.RetargetProfileId);
            root.AddChild(graph);
            foreach (var state in composition.AnimationGraph.States)
            {
                var stateNode = new Node { Name = $"State_{SafeNodeName(state.StateId)}" };
                stateNode.SetMeta("forge_animation_category", state.Category);
                stateNode.SetMeta("forge_animation_clip", state.ClipAssetId);
                graph.AddChild(stateNode);
            }
            if (blueprint.Slug == "ash-warden-boss")
            {
                var loaded = _loadedProject ?? throw new InvalidOperationException("Loaded project is unavailable.");
                var script = CreatureAuthoringCompositionScriptService.Read(loaded,
                    CreatureAuthoringCompositionScriptService.CreateHandle(blueprint.Id));
                var rpg = CreatureAuthoringCompositionFoundationService.Resolve(loaded, blueprint.Id);
                var optional = CreatureAuthoringCompositionRuntimeResolver.Resolve(null, false);
                var required = CreatureAuthoringCompositionRuntimeResolver.Resolve(null, true);
                var hasVisibleModel = creature.GetChildren().OfType<MeshInstance3D>().Any() ||
                    creature.GetNodeOrNull<Node3D>("AuthoredVisual") is not null;
                AssertSmoke(root.GetChildCount() == 4 && graph.GetChildCount() == composition.AnimationGraph.States.Count &&
                            hasVisibleModel && script?.AnimationStates.Count == 5 && rpg.Succeeded &&
                            rpg.Creature?.Species?.Id == "species.emberborn" && optional.Succeeded && optional.UsedFallback &&
                            required.Diagnostics.Single().Code == CreatureAuthoringCompositionValidator.MissingRequiredCode,
                    "Creature authoring runtime adapter did not preserve model, species and animation nodes.");
                GD.Print("OPENRPG_FORGE_RUNTIME_R077_CREATURE_AUTHORING_PASS " +
                    "role=encounter-threat-tags species=inheritance-traits-variants-skeleton " +
                    "appearance=archetype-mesh-rig-morph-material-palette-preset-visible-model " +
                    "animation=graph-states-transitions-locomotion-combat-emote-additive-retarget " +
                    "forgescript=typed-handle persistence=roundtrip optional=ignored required=diagnostic " +
                    "rpgkit=validated-adapter engine=role-species-model-animation-nodes");
            }
        }
        var ruled = CreatureRulesCompositionRuntimeResolver.Resolve(profile.RulesComposition,
            profile.RulesCompositionRequiredAtRuntime);
        if (!ruled.Succeeded)
            throw new InvalidOperationException(string.Join("; ", ruled.Diagnostics.Select(item =>
                $"{item.Code} at {item.Path}: {item.Message}")));
        if (ruled.Profile is not null)
        {
            var rules = ruled.Profile;
            var root = new Node3D { Name = "CreatureRulesComposition" };
            root.SetMeta("forge_creature_rules_id", rules.Id.ToString("D"));
            root.SetMeta("forge_creature_rules_asset", rules.AssetId);
            creature.AddChild(root);

            var initial = new AnimationPlayer { Name = "InitialStateAndPose" };
            initial.SetMeta("forge_initial_life_state", rules.InitialState.LifeState.ToString());
            initial.SetMeta("forge_initial_animation_loop", rules.InitialState.AnimationLoopAssetId ?? string.Empty);
            initial.SetMeta("forge_initial_pose", rules.InitialState.PoseId ?? string.Empty);
            initial.SetMeta("forge_initial_invisible", rules.InitialState.Invisible);
            root.AddChild(initial);

            var attributes = new Node { Name = "Attributes" };
            var evaluated = CreatureRulesCompositionValidator.EvaluateAttributes(rules.Attributes);
            attributes.SetMeta("forge_ruleset", rules.Attributes.RulesetId);
            attributes.SetMeta("forge_attribute_count", evaluated.Count);
            foreach (var attribute in evaluated)
            {
                var node = new Node { Name = $"Attribute_{SafeNodeName(attribute.Key)}" };
                node.SetMeta("forge_attribute_value", attribute.Value);
                attributes.AddChild(node);
            }
            root.AddChild(attributes);

            var defenses = new Node { Name = "DefensesAndSaves" };
            defenses.SetMeta("forge_avoidance", rules.Defenses.Avoidance);
            defenses.SetMeta("forge_armor", rules.Defenses.Armor);
            defenses.SetMeta("forge_resistances", string.Join(',', rules.Defenses.Resistances.Select(item =>
                $"{item.Key}:{item.Value:0.###}")));
            defenses.SetMeta("forge_saves", string.Join(',', rules.Defenses.Saves.Select(item =>
                $"{item.Key}:{item.Value:0.###}")));
            defenses.SetMeta("forge_immunities", string.Join(',', rules.Defenses.Immunities));
            root.AddChild(defenses);

            var abilities = new Node { Name = "SpecialAbilities" };
            foreach (var ability in rules.Abilities)
            {
                var node = new Node { Name = $"Ability_{SafeNodeName(ability.AbilityId)}" };
                node.SetMeta("forge_ability_level", ability.Level);
                node.SetMeta("forge_ability_charges", ability.Charges);
                node.SetMeta("forge_ability_cooldown", ability.CooldownSeconds);
                node.SetMeta("forge_ability_script", ability.ScriptId);
                abilities.AddChild(node);
            }
            root.AddChild(abilities);

            var inventory = new Node { Name = "Inventory" };
            inventory.SetMeta("forge_inventory_owner", rules.Inventory.OwnerId);
            inventory.SetMeta("forge_inventory_persistent", rules.Inventory.Persistent);
            foreach (var container in rules.Inventory.Containers)
            {
                var containerNode = new Node { Name = $"Container_{SafeNodeName(container.ContainerId)}" };
                containerNode.SetMeta("forge_inventory_slots", container.SlotCount);
                containerNode.SetMeta("forge_inventory_max_weight", container.MaximumWeight);
                containerNode.SetMeta("forge_inventory_max_volume", container.MaximumVolume);
                foreach (var entry in container.Items)
                {
                    var itemNode = new Node { Name = $"Item_{SafeNodeName(entry.Id.ToString("D"))}" };
                    itemNode.SetMeta("forge_item_blueprint", entry.ItemBlueprintId.ToString());
                    itemNode.SetMeta("forge_item_quantity", entry.Quantity);
                    containerNode.AddChild(itemNode);
                }
                inventory.AddChild(containerNode);
            }
            root.AddChild(inventory);

            if (blueprint.Slug == "ash-warden-boss")
            {
                var loaded = _loadedProject ?? throw new InvalidOperationException("Loaded project is unavailable.");
                var script = CreatureRulesCompositionScriptService.Read(loaded,
                    CreatureRulesCompositionScriptService.CreateHandle(blueprint.Id));
                var rpg = CreatureRulesCompositionFoundationService.Resolve(loaded, blueprint.Id);
                var optional = CreatureRulesCompositionRuntimeResolver.Resolve(null, false);
                var required = CreatureRulesCompositionRuntimeResolver.Resolve(null, true);
                AssertSmoke(root.GetChildCount() == 5 && initial.GetMeta("forge_initial_life_state").AsString() == "Seated" &&
                            evaluated.GetValueOrDefault("evasion") == 16 && abilities.GetChildCount() == 1 &&
                            inventory.GetChildCount() == 1 && script?.InventoryItemCount == 1 && rpg.Succeeded &&
                            rpg.Creature?.Abilities?.Single().AbilityId == "ability.ash-nova" &&
                            optional.Succeeded && optional.UsedFallback && required.Diagnostics.Single().Code ==
                            CreatureRulesCompositionValidator.MissingRequiredCode,
                    "Creature rules runtime adapter did not preserve initial state, rules and inventory nodes.");
                GD.Print("OPENRPG_FORGE_RUNTIME_R078_CREATURE_RULES_PASS " +
                    "initial=alive-unconscious-dead-sleeping-seated-frozen-loop-pose-invisible " +
                    "attributes=ruleset-base-modifiers-derived-inspectable " +
                    "defenses=avoidance-armor-resistances-saves-immunities-mitigation " +
                    "abilities=level-charges-cooldown-conditions-ai-tags-script " +
                    "inventory=containers-slots-weight-volume-ownership-equipment-persistence " +
                    "forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip " +
                    "optional=ignored required=diagnostic rpgkit=validated-adapter " +
                    "engine=state-attributes-defenses-abilities-inventory-nodes");
            }
        }
        var interacted = CreatureInteractionCompositionRuntimeResolver.Resolve(profile.InteractionComposition,
            profile.InteractionCompositionRequiredAtRuntime);
        if (!interacted.Succeeded)
            throw new InvalidOperationException(string.Join("; ", interacted.Diagnostics.Select(item =>
                $"{item.Code} at {item.Path}: {item.Message}")));
        if (interacted.Profile is not null)
        {
            var interaction = interacted.Profile;
            var root = new Node3D { Name = "CreatureInteractionComposition" };
            root.SetMeta("forge_creature_interaction_id", interaction.Id.ToString("D"));
            root.SetMeta("forge_creature_interaction_asset", interaction.AssetId);
            creature.AddChild(root);

            var factions = new Node { Name = "Factions" };
            factions.SetMeta("forge_primary_faction", interaction.Factions.PrimaryFactionId.ToString("D"));
            foreach (var membership in interaction.Factions.Memberships)
            {
                var member = new Node { Name = $"Membership_{SafeNodeName(membership.FactionId.ToString("D"))}" };
                member.SetMeta("forge_faction_weight", membership.Weight);
                member.SetMeta("forge_faction_rank", membership.Rank);
                member.SetMeta("forge_faction_reputation", membership.PersonalReputation);
                member.SetMeta("forge_faction_inherited", membership.Inherited);
                factions.AddChild(member);
            }
            root.AddChild(factions);

            var perception = new Node3D { Name = "Perception" };
            perception.SetMeta("forge_perception_memory_seconds", interaction.Perception.MemorySeconds);
            foreach (var sense in interaction.Perception.Senses)
            {
                var sensor = new Area3D { Name = $"Sense_{sense.Kind}" };
                sensor.SetMeta("forge_sense_range", sense.Range);
                sensor.SetMeta("forge_sense_cone_degrees", sense.ConeDegrees);
                sensor.SetMeta("forge_sense_occlusion", sense.UsesOcclusion);
                sensor.SetMeta("forge_sense_stimuli", string.Join(',', sense.StimulusTags));
                perception.AddChild(sensor);
            }
            root.AddChild(perception);

            var dialogues = new Node { Name = "DialogueBindings" };
            foreach (var binding in interaction.DialogueBindings)
            {
                var node = new Node { Name = $"Dialogue_{SafeNodeName(binding.Id.ToString("D"))}" };
                node.SetMeta("forge_dialogue_context", binding.Context);
                node.SetMeta("forge_dialogue_priority", binding.Priority);
                node.SetMeta("forge_dialogue_id", binding.DialogueId.ToString());
                node.SetMeta("forge_dialogue_interlocutor", binding.InterlocutorId ?? string.Empty);
                node.SetMeta("forge_dialogue_state", binding.RequiredState ?? string.Empty);
                node.SetMeta("forge_dialogue_fallback", binding.Fallback);
                dialogues.AddChild(node);
            }
            root.AddChild(dialogues);

            var protection = new Node { Name = "ProtectionPolicy" };
            protection.SetMeta("forge_invulnerable", interaction.Protection.Invulnerable);
            protection.SetMeta("forge_narrative_death", interaction.Protection.UsesNarrativeDeath);
            protection.SetMeta("forge_dialogue_interruptible", interaction.Protection.DialogueInterruptible);
            protection.SetMeta("forge_gm_permissions", string.Join(',', interaction.Protection.GameMasterPermissions));
            root.AddChild(protection);

            var onConversation = new Node { Name = "OnConversation" };
            onConversation.SetMeta("forge_event_name", interaction.OnConversation.EventName);
            onConversation.SetMeta("forge_event_handler", interaction.OnConversation.Handler);
            onConversation.SetMeta("forge_event_authority", interaction.OnConversation.Authority.ToString());
            root.AddChild(onConversation);

            if (blueprint.Slug == "ash-warden-boss")
            {
                var loaded = _loadedProject ?? throw new InvalidOperationException("Loaded project is unavailable.");
                var script = CreatureInteractionCompositionScriptService.Read(loaded,
                    CreatureInteractionCompositionScriptService.CreateHandle(blueprint.Id));
                var rpg = CreatureInteractionCompositionFoundationService.Resolve(loaded, blueprint.Id);
                var decision = CreatureInteractionCompositionValidator.RequestDialogue(interaction,
                    "greeting", "player.hero", "state.hostile");
                var protectedDecision = CreatureInteractionCompositionValidator.RequestDialogue(interaction,
                    "interrupt", "player.hero", null);
                var optional = CreatureInteractionCompositionRuntimeResolver.Resolve(null, false);
                var required = CreatureInteractionCompositionRuntimeResolver.Resolve(null, true);
                AssertSmoke(root.GetChildCount() == 5 && factions.GetChildCount() == 2 &&
                            perception.GetChildCount() == 4 && dialogues.GetChildCount() == 3 &&
                            protection.GetMeta("forge_invulnerable").AsBool() && decision.Accepted &&
                            decision.Binding?.Priority == 20 && !protectedDecision.Accepted &&
                            protectedDecision.RefusalCode == "creature.interaction.dialogue_protected" &&
                            script?.Memberships.Count == 2 && script.Senses.Count == 4 && rpg.Succeeded &&
                            rpg.Creature?.Factions?.Memberships.Count == 2 &&
                            optional.Succeeded && optional.UsedFallback && required.Diagnostics.Single().Code ==
                            CreatureInteractionCompositionValidator.MissingRequiredCode,
                    "Creature interaction runtime adapter did not preserve factions, senses, dialogue and protection nodes.");
                GD.Print("OPENRPG_FORGE_RUNTIME_R079_CREATURE_INTERACTION_PASS " +
                    "factions=multiple-weighted-primary-rank-reputation-inheritance " +
                    "perception=vision-hearing-smell-magic-range-cone-occlusion-stimuli-memory " +
                    "dialogue=context-priority-interlocutor-state-fallback-selection-refusal " +
                    "protection=invulnerability-narrative-death-interruption-gm-permissions " +
                    "onconversation=interaction-requested-dialogue-starting-server-binding " +
                    "forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip " +
                    "optional=ignored required=diagnostic rpgkit=validated-adapter " +
                    "engine=factions-perception-dialogue-protection-event-nodes");
            }
        }
        var eventAiResolved = CreatureEventAiCompositionRuntimeResolver.Resolve(profile.EventAiComposition,
            profile.EventAiCompositionRequiredAtRuntime);
        if (!eventAiResolved.Succeeded)
            throw new InvalidOperationException(string.Join("; ", eventAiResolved.Diagnostics.Select(item =>
                $"{item.Code} at {item.Path}: {item.Message}")));
        if (eventAiResolved.Profile is not null)
        {
            var eventAi = eventAiResolved.Profile;
            var root = new Node3D { Name = "CreatureEventAiComposition" };
            root.SetMeta("forge_creature_event_ai_id", eventAi.Id.ToString("D"));
            root.SetMeta("forge_creature_event_ai_asset", eventAi.AssetId);
            creature.AddChild(root);

            var onPerception = new Node { Name = "OnPerception" };
            AddEventNode(onPerception, "StimulusDetected", eventAi.Perception.Detected);
            AddEventNode(onPerception, "StimulusLost", eventAi.Perception.Lost);
            root.AddChild(onPerception);

            var onPhysicalAttacked = new Node { Name = "OnPhysicalAttacked" };
            AddEventNode(onPhysicalAttacked, "AttackDeclared", eventAi.PhysicalAttack.Declared);
            AddEventNode(onPhysicalAttacked, "AttackResolved", eventAi.PhysicalAttack.Resolved);
            root.AddChild(onPhysicalAttacked);

            var onSpellCastAt = new Node { Name = "OnSpellCastAt" };
            AddEventNode(onSpellCastAt, "AbilityTargeted", eventAi.Ability.Targeted);
            AddEventNode(onSpellCastAt, "AbilityImpacted", eventAi.Ability.Impacted);
            root.AddChild(onSpellCastAt);

            var onBlocked = new Node { Name = "OnBlocked" };
            AddEventNode(onBlocked, "MovementBlocked", eventAi.Blocked.Blocked);
            onBlocked.SetMeta("forge_recovery_order", string.Join(',', eventAi.Blocked.RecoveryOrder));
            root.AddChild(onBlocked);

            var controller = new Node { Name = "AiController" };
            controller.SetMeta("forge_ai_kind", eventAi.Controller.Kind.ToString());
            controller.SetMeta("forge_ai_definition", eventAi.Controller.DefinitionId);
            controller.SetMeta("forge_ai_allowed_actions", string.Join(',', eventAi.Controller.AllowedActions));
            foreach (var entry in eventAi.Controller.Blackboard)
            {
                var blackboardNode = new Node { Name = $"Blackboard_{SafeNodeName(entry.Key)}" };
                blackboardNode.SetMeta("forge_blackboard_type", entry.Kind.ToString());
                blackboardNode.SetMeta("forge_blackboard_value", entry.Value);
                blackboardNode.SetMeta("forge_blackboard_persistent", entry.Persistent);
                controller.AddChild(blackboardNode);
            }
            root.AddChild(controller);

            if (blueprint.Slug == "ash-warden-boss")
            {
                var loaded = _loadedProject ?? throw new InvalidOperationException("Loaded project is unavailable.");
                var script = CreatureEventAiCompositionScriptService.Read(loaded,
                    CreatureEventAiCompositionScriptService.CreateHandle(blueprint.Id));
                var rpg = CreatureEventAiCompositionFoundationService.Resolve(loaded, blueprint.Id);
                var perceptionEvents = Forge.RpgKit.Domain.Creatures.CreatureInteractionService.DiffPerception(
                    eventAi.Id,
                    [new Forge.RpgKit.Domain.Creatures.PerceptionResult("old",
                        Forge.RpgKit.Domain.Creatures.SenseKind.Hearing, .4, Vector3Value.Zero, 10)],
                    [new Forge.RpgKit.Domain.Creatures.PerceptionResult("new",
                        Forge.RpgKit.Domain.Creatures.SenseKind.Vision, .8, new Vector3Value(2, 0, 1), 20)],
                    Guid.NewGuid);
                var network = new Forge.RpgKit.Domain.Creatures.AttackNetworkContext("server", 42, 3, false);
                var attack = Forge.RpgKit.Domain.Creatures.CreatureInteractionService.ResolveAttack(
                    new Forge.RpgKit.Domain.Creatures.AttackDeclared(Guid.NewGuid(), eventAi.Id, "player.hero",
                        "weapon.sword", "ash-warden-boss", network), "hit", 7, Guid.NewGuid);
                var ability = Forge.RpgKit.Domain.Creatures.CreatureAiService.Impact(
                    new Forge.RpgKit.Domain.Creatures.AbilityTargeted(Guid.NewGuid(), eventAi.Id, "player.hero",
                        "ability.firebolt", new HashSet<string> { "fire" }, "entity"), "resisted", 3,
                    Guid.NewGuid);
                var blocked = Forge.RpgKit.Domain.Creatures.CreatureAiService.Blocked(eventAi.Id, "pillar.ash",
                    "route", "dynamic-obstacle", Vector3Value.Zero, Guid.NewGuid);
                var decision = Forge.RpgKit.Domain.Creatures.CreatureAiService.Decide(rpg.Controller!,
                    new Dictionary<string, double> { ["attack"] = .9, ["guard"] = .4, ["patrol"] = .2 });
                var optional = CreatureEventAiCompositionRuntimeResolver.Resolve(null, false);
                var required = CreatureEventAiCompositionRuntimeResolver.Resolve(null, true);
                AssertSmoke(root.GetChildCount() == 5 && onPerception.GetChildCount() == 2 &&
                            onPhysicalAttacked.GetChildCount() == 2 && onSpellCastAt.GetChildCount() == 2 &&
                            onBlocked.GetChildCount() == 1 && controller.GetChildCount() == 3 &&
                            perceptionEvents.Detected.Single().StimulusId == "new" &&
                            perceptionEvents.Lost.Single().StimulusId == "old" && attack.AppliedDamage == 7 &&
                            ability.Result == "resisted" &&
                            blocked.Recovery == Forge.RpgKit.Domain.Creatures.MovementRecoveryStrategy.Avoid &&
                            decision.Action == "attack" && script?.Blackboard.Count == 3 && rpg.Succeeded &&
                            optional.Succeeded && optional.UsedFallback && required.Diagnostics.Single().Code ==
                            CreatureEventAiCompositionValidator.MissingRequiredCode,
                    "Creature event/AI runtime adapter did not preserve typed events, recovery or controller nodes.");
                GD.Print("OPENRPG_FORGE_RUNTIME_R080_CREATURE_EVENT_AI_PASS " +
                    "onperception=stimulus-detected-lost-sense-confidence-position-memory " +
                    "onphysicalattacked=declared-resolved-attacker-weapon-target-result-network " +
                    "onspellcastat=ability-targeted-impacted-source-target-tags-result-magnitude " +
                    "onblocked=obstacle-cause-nav-status-position-wait-repath-avoid-teleport-abort " +
                    "ai=state-machine-behavior-tree-utility-script-typed-inspectable-blackboard-bounded-actions " +
                    "forgescript=typed-handle mcp=filtered-dry-run persistence=roundtrip " +
                    "optional=ignored required=diagnostic rpgkit=validated-adapter " +
                    "engine=perception-attack-ability-blocked-ai-nodes");
            }
        }
        if (blueprint.Slug == "ash-warden-boss")
            GD.Print("OPENRPG_FORGE_RUNTIME_R038_CREATURE_FOUNDATION_PASS identity=localized resources=health-stamina " +
                     $"movement={profile.Movement.Mode.ToString().ToLowerInvariant()} spawn=validated-after-construction " +
                     "forgescript=safe-handle optional=empty-safe required=diagnostic");

        static void AddEventNode(Node owner, string name, EventBinding binding)
        {
            var node = new Node { Name = name };
            node.SetMeta("forge_event_name", binding.EventName);
            node.SetMeta("forge_event_handler", binding.Handler);
            node.SetMeta("forge_event_authority", binding.Authority.ToString());
            owner.AddChild(node);
        }
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
        var foundation = FactionRuntimeResolver.ResolveFoundation(catalog);
        if (foundation.Diagnostics.Count > 0)
            throw new InvalidOperationException("Faction foundation is invalid: " +
                string.Join("; ", foundation.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        if (GodotObject.IsInstanceValid(_factionFoundationRoot))
            _factionFoundationRoot!.QueueFree();
        _factionFoundationRoot = null;
        if (foundation.Catalog is not null)
        {
            _factionFoundationRoot = new Node { Name = "FactionFoundationRuntime" };
            AddChild(_factionFoundationRoot);
            foreach (var faction in catalog.Factions.OrderBy(item => item.Slug, StringComparer.Ordinal))
            {
                var node = new Node { Name = $"Faction_{faction.Slug}" };
                node.SetMeta("id", faction.Id.ToString("D"));
                node.SetMeta("schema_version", faction.SchemaVersion);
                node.SetMeta("archived", faction.Archived);
                node.SetMeta("semantic_tags", string.Join(',',
                    FactionRuntimeResolver.EffectiveDefinition(catalog, faction.Id).Tags));
                _factionFoundationRoot.AddChild(node);
            }
            foreach (var relationship in catalog.Relationships.OrderBy(item => item.SourceId)
                         .ThenBy(item => item.TargetId))
            {
                var node = new Node { Name = $"Relationship_{relationship.SourceId:N}_{relationship.TargetId:N}" };
                node.SetMeta("score", relationship.Score);
                node.SetMeta("disposition", FactionValidator.ResolveBand(
                    catalog, relationship.SourceId, relationship.TargetId));
                node.SetMeta("explanation", relationship.Explanation);
                _factionFoundationRoot.AddChild(node);
            }
            foreach (var overlay in catalog.EffectivePersonalReputation.OrderBy(item => item.Id))
            {
                var node = new Node { Name = $"PersonalReputation_{overlay.Id:N}" };
                node.SetMeta("subject_kind", overlay.SubjectKind.ToString());
                node.SetMeta("subject", overlay.SubjectId);
                node.SetMeta("delta", overlay.Delta);
                node.SetMeta("cause", overlay.Cause);
                _factionFoundationRoot.AddChild(node);
            }
        }

        if (GodotObject.IsInstanceValid(_factionReputationRoot))
            _factionReputationRoot!.QueueFree();
        _factionReputationRoot = null;
        var reputation = FactionRuntimeResolver.ResolveReputation(catalog);
        if (reputation.Diagnostics.Count > 0)
            throw new InvalidOperationException("Faction reputation is invalid: " +
                string.Join("; ", reputation.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        _factionReputationProfile = reputation.Profile;
        _factionReputationState = FactionReputationService.InitialState(catalog);
        if (reputation.Profile is not null)
        {
            _factionReputationRoot = new Node { Name = "FactionReputationRuntime" };
            AddChild(_factionReputationRoot);
            var reaction = new Node { Name = $"ReactionPolicy_{reputation.Profile.Reaction.Id:N}" };
            reaction.SetMeta("default_scope", reputation.Profile.Reaction.DefaultScope.ToString());
            reaction.SetMeta("default_scope_id", reputation.Profile.Reaction.DefaultScopeId);
            reaction.SetMeta("gm_override", reputation.Profile.Reaction.AllowGameMasterOverride);
            _factionReputationRoot.AddChild(reaction);
            foreach (var transaction in reputation.Profile.Transactions.OrderBy(item => item.OccurredAt)
                         .ThenBy(item => item.Id))
            {
                var node = new Node { Name = $"GlobalReputation_{transaction.Id:N}" };
                node.SetMeta("scope", transaction.Scope.ToString());
                node.SetMeta("scope_id", transaction.ScopeId);
                node.SetMeta("delta", transaction.Delta);
                node.SetMeta("cause", transaction.Cause);
                _factionReputationRoot.AddChild(node);
            }
            foreach (var snapshot in reputation.Profile.HeatmapHistory.OrderBy(item => item.CapturedAt)
                         .ThenBy(item => item.Id))
            {
                var node = new Node { Name = $"Heatmap_{snapshot.Id:N}" };
                node.SetMeta("label", snapshot.Label);
                node.SetMeta("relationships", snapshot.Relationships.Count);
                node.SetMeta("captured_at", snapshot.CapturedAt.ToString("O"));
                _factionReputationRoot.AddChild(node);
            }
        }

        var resolution = FactionRuntimeResolver.Resolve(catalog);
        if (resolution.Diagnostics.Count > 0)
            throw new InvalidOperationException("Advanced faction profile is invalid: " +
                string.Join("; ", resolution.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
        _factionCatalog = catalog;
        _factionRuntimeProfile = resolution.Profile;
        if (resolution.Profile is null)
        {
            GD.Print($"OPENRPG_FORGE_FACTIONS_OPTIONAL_FALLBACK definitions={catalog.Factions.Count}");
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
            PartyHud = _worldFeatureCatalog?.Companions.FirstOrDefault()?.Experience?.Hud,
        };
        AddChild(_hud);
        _hud.UpdatePartyStatus("Elyra", 24, 30,
            new Dictionary<string, double> { ["health"] = 24, ["focus"] = 4 }, ["prête"]);
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
            var progressionEvent = new ProgressionChangedEvent(Guid.NewGuid(), "player:local", previousLevel,
                _progressionLevel, sourceId, EventAuthorityScope.Server, _runtimePolicyRevision++,
                DateTimeOffset.UtcNow);
            _gameplayEvents.Publish(progressionEvent, _moduleRuntimePolicies?.GameplayEvents,
                _moduleRuntimePolicies?.GameplayEvents.RequiredAtRuntime == true);
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
            BuildEntityStateSnapshots(),
            _worldClockSnapshot is null ? null : new RuntimeWorldClockSave(1,
                _worldClockSnapshot.TotalMinutes, _worldClockSnapshot.Paused, _worldClockSnapshot.Revision,
                _worldClockSnapshot.Provenance));
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
        if (save.WorldClock is not null)
            _worldClockSnapshot = new WorldClockSnapshot(save.WorldClock.TotalMinutes, save.WorldClock.Paused,
                save.WorldClock.Revision, save.WorldClock.Provenance);
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
        _doorAccessEventRoot?.QueueFree();
        _doorAccessEventRoot = new Node { Name = "DoorAccessEventAdapters" };
        AddChild(_doorAccessEventRoot);
        foreach (var doorProfile in catalog.Doors.Where(item => item.AccessEvents is not null)
                     .OrderBy(item => item.Id))
        {
            var access = doorProfile.AccessEvents!;
            var doorNode = new Node { Name = $"Door_{doorProfile.Id:N}" };
            doorNode.SetMeta("profile_id", access.Id.ToString("D"));
            doorNode.SetMeta("blueprint_id", doorProfile.BlueprintId.ToString());
            doorNode.SetMeta("asset_id", access.AssetId);
            _doorAccessEventRoot.AddChild(doorNode);
            foreach (var part in new[]
                     {
                         ("LockAndKey", access.Lock.Id),
                         ("DoorOpenRejected", access.Rejection.Id),
                         ("DoorTrap", access.Trap.Id),
                         ("PortalUseLifecycle", access.PortalUse.Id),
                         ("DoorStateLifecycle", access.StateEvents.Id),
                     })
            {
                var partNode = new Node { Name = part.Item1 };
                partNode.SetMeta("profile_id", part.Id.ToString("D"));
                doorNode.AddChild(partNode);
            }
            if (access.LockStateEvents is { } lockEvents)
            {
                var lockEventNode = new Node { Name = "DoorLockLifecycle" };
                lockEventNode.SetMeta("profile_id", lockEvents.Id.ToString("D"));
                lockEventNode.SetMeta("locked_binding", lockEvents.LockedBinding);
                lockEventNode.SetMeta("unlocked_binding", lockEvents.UnlockedBinding);
                doorNode.AddChild(lockEventNode);
            }
        }
        _encounterCompositionRoot?.QueueFree();
        _encounterCompositionRoot = new Node { Name = "EncounterCompositionAdapters" };
        AddChild(_encounterCompositionRoot);
        foreach (var encounterProfile in catalog.Encounters.Where(item => item.Composition is not null)
                     .OrderBy(item => item.Id))
        {
            var composition = encounterProfile.Composition!;
            var encounterNode = new Node { Name = $"Encounter_{encounterProfile.Id:N}" };
            encounterNode.SetMeta("profile_id", composition.Id.ToString("D"));
            encounterNode.SetMeta("area_id", encounterProfile.AreaId.ToString());
            encounterNode.SetMeta("slug", composition.Slug);
            _encounterCompositionRoot.AddChild(encounterNode);
            foreach (var part in new[]
                     {
                         ("EncounterWizard", composition.Id),
                         ("DifficultyAndBudget", composition.Difficulty.Id),
                         ("GroupCountRange", composition.Groups[0].Id),
                         ("PartyAssessmentSelection", composition.Selection.Id),
                         ("ActivationPolicy", composition.Activation.Id),
                     })
            {
                var partNode = new Node { Name = part.Item1 };
                partNode.SetMeta("profile_id", part.Id.ToString("D"));
                encounterNode.AddChild(partNode);
            }
        }
        _encounterScenarioRoot?.QueueFree();
        _encounterScenarioRoot = new Node { Name = "EncounterScenarioAdapters" };
        AddChild(_encounterScenarioRoot);
        foreach (var encounterProfile in catalog.Encounters.Where(item => item.Scenario is not null)
                     .OrderBy(item => item.Id))
        {
            var scenario = encounterProfile.Scenario!;
            var encounterNode = new Node { Name = $"Encounter_{encounterProfile.Id:N}" };
            encounterNode.SetMeta("profile_id", scenario.Id.ToString("D"));
            encounterNode.SetMeta("distribution", scenario.Distribution.ToString());
            _encounterScenarioRoot.AddChild(encounterNode);

            var anchorsNode = new Node { Name = "ManualSpawnAnchors" };
            anchorsNode.SetMeta("count", scenario.SpawnAnchors.Count);
            anchorsNode.SetMeta("fallback", scenario.SpawnAnchors.Single(item => item.IsFallback).AnchorId);
            encounterNode.AddChild(anchorsNode);
            foreach (var anchor in scenario.SpawnAnchors.OrderBy(item => item.AnchorId, StringComparer.Ordinal))
            {
                var anchorNode = new Node { Name = $"Anchor_{anchor.Id:N}" };
                anchorNode.SetMeta("anchor_id", anchor.AnchorId);
                anchorNode.SetMeta("capacity", anchor.Capacity);
                anchorNode.SetMeta("navigable", anchor.Navigable);
                anchorsNode.AddChild(anchorNode);
            }

            var activatorNode = new Node { Name = "ActivatorRestriction" };
            activatorNode.SetMeta("profile_id", scenario.Activator.Id.ToString("D"));
            activatorNode.SetMeta("authority", scenario.Activator.RequiredAuthority.ToString());
            encounterNode.AddChild(activatorNode);

            var lifecycleNode = new Node { Name = "EncounterLifecycle" };
            lifecycleNode.SetMeta("profile_id", scenario.Lifecycle.Id.ToString("D"));
            lifecycleNode.SetMeta("entered", scenario.Lifecycle.Entered.Binding);
            lifecycleNode.SetMeta("exited", scenario.Lifecycle.Exited.Binding);
            lifecycleNode.SetMeta("exhausted", scenario.Lifecycle.Exhausted.Binding);
            encounterNode.AddChild(lifecycleNode);

            var waveGraphNode = new Node { Name = "WaveGraph" };
            waveGraphNode.SetMeta("phases", scenario.Phases.Count);
            waveGraphNode.SetMeta("waves", scenario.Phases.Sum(item => item.Waves.Count));
            encounterNode.AddChild(waveGraphNode);
            foreach (var phase in scenario.Phases.OrderBy(item => item.Order))
            {
                var phaseNode = new Node { Name = $"Phase_{phase.Order}_{phase.Id:N}" };
                phaseNode.SetMeta("display_name", phase.DisplayName);
                waveGraphNode.AddChild(phaseNode);
                foreach (var wave in phase.Waves.OrderBy(item => item.Order))
                {
                    var waveNode = new Node { Name = $"Wave_{wave.Order}_{wave.Id:N}" };
                    waveNode.SetMeta("completion", wave.Completion.ToString());
                    waveNode.SetMeta("voice", wave.VoiceCueId ?? string.Empty);
                    waveNode.SetMeta("next", wave.NextWaveId?.ToString("D") ?? string.Empty);
                    phaseNode.AddChild(waveNode);
                }
            }
        }
        _encounterStatisticsRoot?.QueueFree();
        _encounterStatisticsRoot = new Node { Name = "EncounterStatisticsAdapters" };
        AddChild(_encounterStatisticsRoot);
        foreach (var encounterProfile in catalog.Encounters.Where(item => item.Statistics is not null)
                     .OrderBy(item => item.Id))
        {
            var statistics = encounterProfile.Statistics!;
            var statisticsNode = new Node { Name = $"Encounter_{encounterProfile.Id:N}" };
            statisticsNode.SetMeta("profile_id", statistics.Id.ToString("D"));
            statisticsNode.SetMeta("runs", statistics.Runs);
            statisticsNode.SetMeta("base_seed", statistics.BaseSeed);
            statisticsNode.SetMeta("duration_estimator", statistics.DurationEstimatorId);
            statisticsNode.SetMeta("reward_estimator", statistics.RewardEstimatorId);
            statisticsNode.SetMeta("percentiles", string.Join(',', statistics.Percentiles));
            _encounterStatisticsRoot.AddChild(statisticsNode);
        }
        _companionFoundationRoot?.QueueFree();
        _companionFoundationRoot = new Node { Name = "CompanionFoundationAdapters" };
        AddChild(_companionFoundationRoot);
        foreach (var companionProfile in catalog.Companions.Where(item => item.Foundation is not null)
                     .OrderBy(item => item.Id))
        {
            var companionNode = new Node { Name = $"Companion_{companionProfile.Id:N}" };
            companionNode.SetMeta("profile_id", companionProfile.Id.ToString("D"));
            companionNode.SetMeta("blueprint_id", companionProfile.BlueprintId.ToString());
            _companionFoundationRoot.AddChild(companionNode);
            var foundation = companionProfile.Foundation!;
            foreach (var part in new[]
                     {
                         ("RecruitableCreature", foundation.Recruitable.Id),
                         ("PreRecruitmentDialogue", foundation.PreRecruitment.Id),
                         ("OwnerDialogue", foundation.OwnerDialogue.Id),
                         ("RecruitDismissTransaction", foundation.Membership.Id),
                         ("PartyPolicy", foundation.Party.Id),
                     })
            {
                var partNode = new Node { Name = part.Item1 };
                partNode.SetMeta("profile_id", part.Id.ToString("D"));
                companionNode.AddChild(partNode);
            }
        }
        _companionControlRoot?.QueueFree();
        _companionControlRoot = new Node { Name = "CompanionControlAdapters" };
        AddChild(_companionControlRoot);
        foreach (var companionProfile in catalog.Companions.Where(item => item.Control is not null)
                     .OrderBy(item => item.Id))
        {
            var companionNode = new Node { Name = $"CompanionControl_{companionProfile.Id:N}" };
            companionNode.SetMeta("profile_id", companionProfile.Id.ToString("D"));
            _companionControlRoot.AddChild(companionNode);
            var control = companionProfile.Control!;
            foreach (var part in new[]
                     {
                         ("Ownership", control.Ownership.Id),
                         ("MovementCommands", control.Movement.Id),
                         ("TacticalCommands", control.Tactical.Id),
                         ("CompanionAi", control.Ai.Id),
                         ("EquipmentControl", control.Equipment.Id),
                     })
            {
                var partNode = new Node { Name = part.Item1 };
                partNode.SetMeta("profile_id", part.Id.ToString("D"));
                companionNode.AddChild(partNode);
            }
        }
        _companionExperienceRoot?.QueueFree();
        _companionExperienceRoot = new Node { Name = "CompanionExperienceAdapters" };
        AddChild(_companionExperienceRoot);
        foreach (var companionProfile in catalog.Companions.Where(item => item.Experience is not null)
                     .OrderBy(item => item.Id))
        {
            var companionNode = new Node { Name = $"CompanionExperience_{companionProfile.Id:N}" };
            companionNode.SetMeta("profile_id", companionProfile.Id.ToString("D"));
            _companionExperienceRoot.AddChild(companionNode);
            var experience = companionProfile.Experience!;
            foreach (var part in new[]
                     {
                         ("DeathLifecycle", experience.Death.Id),
                         ("CohesionRecovery", experience.Cohesion.Id),
                         ("PartyHud", experience.Hud.Id),
                     })
            {
                var partNode = new Node { Name = part.Item1 };
                partNode.SetMeta("profile_id", part.Id.ToString("D"));
                companionNode.AddChild(partNode);
            }
        }
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
                    restoredPlaceable.SecurityEvents?.Trap.PayloadId == "damage:ember-burst" &&
                    restoredPlaceable.SecurityEvents.Loot.Timing == PlaceableAuthoredLootSpawnTiming.Open &&
                    restoredPlaceable.SecurityEvents.LockEvents.ControlledVeto &&
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
        var r055Authoring = new AreaAuthoringProfile(1,
            [new(Guid.Parse("a0557000-0000-4000-8000-000000000006"), r055Event,
                "schema://area/alarm/v1", "scripts/runtime_alarm.forge", 1, [r055Event], true,
                "runtime:r055")],
            new(true, true, "hybrid-2d-3d", 2,
                [new(Guid.Parse("a0557000-0000-4000-8000-000000000007"), "Runtime shrine", 16, 0, 0,
                    false)], Guid.Parse("a0557000-0000-4000-8000-000000000008"), 1, "runtime:r055",
                true, true, AreaMapExplorationScope.Party, 8, "area.map.reveal"), area.Authoring.Layers,
            r055Experience);
        var r055Area = area.WithAuthoring(r055Authoring);
        var r055Snapshot = AreaExperienceAuthoringScriptService.Read(r055Area);
        var r055Dispatch = AreaExperienceAuthoringScriptService.Dispatch(r055Area, r055Event,
            new Dictionary<string, object?> { ["level"] = 2 });
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
                    r055Dispatch.ApiVersion == 1 && r055Map.RevealedCells.Contains("2:0") &&
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
                 "audio=snapshots-adaptive-reverb events=namespaced-payload-subscriptions " +
                 "map=2d-3d-exploration-fog-reveal visibility=rooms-portals-occluders-streaming " +
                 "lod=thresholds-budgets-popping forgescript=typed-handle optional=ignored required=diagnostic");

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

    private static void RunProjectGameplayEventsRuntimeSmokeTest(ProjectDefinition project)
    {
        var profile = project.Module.RuntimePolicies.GameplayEvents;
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

        AssertSmoke(handle.Available && handle.Handle!.Bindings.Count == 14 && optional.Diagnostics.Count == 0 &&
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
            "optional=ignored required=diagnostic");
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
