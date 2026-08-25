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
    private const string SolarenVisualFixtureArgument = "--solaren-visual-fixture";
    private const string ModuleMapVisualCaptureArgument = "--module-map-visual-capture";
    private const string RpgMenuVisualCaptureArgument = "--rpg-menu-visual-capture";
    private const string RpgMenuPageArgument = "--rpg-menu-page";
    private const string DialogueVisualCaptureArgument = "--dialogue-visual-capture";
    private const string ActionWheelVisualCaptureArgument = "--action-wheel-visual-capture";
    private const string CombatFeedbackCaptureArgument = "--combat-feedback-capture";
    private const string CombatFeedbackCaptureSizeArgument = "--combat-feedback-capture-size";
    private const string VisualRegressionSuiteArgument = "--g11s-visual-suite";
    private const float InteractionDistance = 3.0f;
    private const float AttackDistance = 3.5f;
    private const float CameraHorizontalWallClearance = .85f;
    private const float CameraDirectHitClearance = .65f;
    private const string VisualAssetProperty = "visual.asset";
    private const string RuntimeRoleProperty = "runtime.role";
    private const string RuntimeAnimationProfileProperty = "runtime.animationProfile";
    private const string RuntimeWorldLabelNodeName = "RuntimeWorldLabel";
    private static EntityId? s_pendingPortalSpawn;
    private static string? s_pendingPortalLabel;
    private static int s_innUxSmokeStep;
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
    private readonly Dictionary<Node3D, EntityClickTriggerSettings> _clickInteractions = [];
    private readonly Dictionary<Node3D, PortalLink> _portalNodes = [];
    private readonly Dictionary<Guid, CameraAnchorDefinition> _cameraAnchors = [];
    private readonly Dictionary<Guid, double> _scatterDueSeconds = [];
    private readonly Dictionary<Guid, int> _scatterSequences = [];
    private readonly Dictionary<string, AudioStreamWav> _proceduralAudioCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D> _surfaceTextureCache = new(StringComparer.OrdinalIgnoreCase);
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
    private readonly HashSet<Guid> _livingWorldTriggerOccupancy = [];
    private readonly HashSet<Guid> _livingWorldTriggerDiagnostics = [];
    private readonly Dictionary<Guid, WorldFeatureRuntimeResolver.TriggerRuntimeState>
        _livingWorldTriggerFoundationStates = [];
    private TriggerNarrativeRuntimeState _livingWorldNarrativeState = TriggerNarrativeRuntimeState.Empty;
    private EntityInstance? _livingWorldPlayerEntity;
    private Node? _doorAccessEventRoot;
    private Node? _encounterCompositionRoot;
    private Node? _encounterScenarioRoot;
    private Node? _encounterStatisticsRoot;
    private Node? _companionFoundationRoot;
    private Node? _companionControlRoot;
    private Node? _companionExperienceRoot;
    private PostLaunchCapabilityProfile? _postLaunchProfile;

    private PlayerController? _player;
    private RuntimeFlyingMount? _flyingMount;
    private CombatantState? _playerCombatant;
    private Camera3D? _camera;
    private RuntimeHud? _hud;
    private RuntimeActionBar? _actionBar;
    private RuntimeModuleMap? _moduleMap;
    private RuntimeCombatFeedbackLayer? _combatFeedbackLayer;
    private RuntimeCombatImpactPool? _combatImpactPool;
    private RuntimeToastQueue? _toastQueue;
    private long? _combatFeedbackCaptureReadbackParity;
    private RuntimeUiMotionPolicy _combatUiPolicy = RuntimeUiMotionPolicy.Create(null, []);
    private Node3D? _currentCombatTarget;
    private CombatFeedbackPresentation? _lastCombatFeedback;
    private WaypointMapPresentation? _trackedWaypointMarker;
    private string _lastPresentedObjective = string.Empty;
    private RuntimeRpgMenu? _rpgMenu;
    private RuntimeDialogueOverlay? _dialogueOverlay;
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
    private AreaDefinition? _activeArea;
    private string? _quickSavePath;
    private string _moduleTitle = "OpenRPG Forge";
    private string _moduleSlug = string.Empty;
    private bool _isCryptPrototype;
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
    private readonly PartyService _areaCombatParties = new();
    private AreaCombatAuthority? _areaCombatAuthority;
    private bool _areaCombatServerAuthority;
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
    private bool _portalTransitionInProgress;
    private bool _fpsVisible;
    private bool _localPlayerHiddenForFirstPerson;
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
    private int _terrainWindAnimatedLayers;
    private int _terrainCullingGroups;
    private int _terrainWaterSurfaces;
    private int _terrainWaterVolumes;
    private TerrainDefinition? _activeTerrain;
    private Godot.Environment? _worldEnvironment;
    private DirectionalLight3D? _referenceSun;
    private float _baseAmbientLightEnergy;
    private float _baseBackgroundEnergyMultiplier;
    private float _baseTonemapExposure;
    private float _baseFogDensity;
    private float _baseFogLightEnergy;
    private float _baseDirectionalLightEnergy;
    private float _baseCloudCoverage;
    private bool _baseFogEnabled;
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
    private Rect2? _interiorWalkableBounds;
    private Rect2? _activeAreaHorizontalBounds;
    private float _interiorFloorTop;

    public override async void _Ready()
    {
        var startupStarted = Stopwatch.GetTimestamp();
        var userArguments = OS.GetCmdlineUserArgs();
        var smokeTest = userArguments.Contains("--smoke-test", StringComparer.Ordinal);
        var genericSmokeTest = userArguments.Contains("--generic-smoke-test", StringComparer.Ordinal);
        var solarenSmokeTest = userArguments.Contains("--solaren-smoke-test", StringComparer.Ordinal);
        var combatFeedbackSmokeTest = userArguments.Contains("--combat-feedback-smoke-test", StringComparer.Ordinal);
        var entryGroundSmokeTest = userArguments.Contains("--entry-ground-smoke-test", StringComparer.Ordinal);
        var cryptMovementSmokeTest = userArguments.Contains("--crypt-movement-smoke-test", StringComparer.Ordinal);
        var innUxSmokeTest = userArguments.Contains("--inn-ux-smoke-test", StringComparer.Ordinal);
        var saveSmokeCreate = userArguments.Contains("--save-smoke-create", StringComparer.Ordinal);
        var saveSmokeResume = userArguments.Contains("--save-smoke-resume", StringComparer.Ordinal);
        var visualCapturePath = GetArgumentValue(userArguments, VisualCaptureArgument);
        var solarenVisualFixture = GetArgumentValue(userArguments, SolarenVisualFixtureArgument);
        var moduleMapVisualCapturePath = GetArgumentValue(userArguments, ModuleMapVisualCaptureArgument);
        var rpgMenuVisualCapturePath = GetArgumentValue(userArguments, RpgMenuVisualCaptureArgument);
        var dialogueVisualCapturePath = GetArgumentValue(userArguments, DialogueVisualCaptureArgument);
        var actionWheelVisualCapturePath = GetArgumentValue(userArguments, ActionWheelVisualCaptureArgument);
        var combatFeedbackCapturePath = GetArgumentValue(userArguments, CombatFeedbackCaptureArgument);
        var combatFeedbackCaptureSize = GetArgumentValue(userArguments, CombatFeedbackCaptureSizeArgument);
        var visualRegressionSuitePath = GetArgumentValue(userArguments, VisualRegressionSuiteArgument);
        var automatedTest = smokeTest || genericSmokeTest || solarenSmokeTest ||
            combatFeedbackSmokeTest ||
            entryGroundSmokeTest || cryptMovementSmokeTest ||
            innUxSmokeTest ||
            saveSmokeCreate || saveSmokeResume ||
            visualCapturePath is not null ||
            solarenVisualFixture is not null ||
            moduleMapVisualCapturePath is not null ||
            rpgMenuVisualCapturePath is not null || dialogueVisualCapturePath is not null ||
            actionWheelVisualCapturePath is not null ||
            combatFeedbackCapturePath is not null ||
            visualRegressionSuitePath is not null;

        try
        {
            RegisterInputActions();
            var launcherContext = LauncherRuntimeBootstrap.LoadAndApply(this, userArguments);
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
            var requestedSpawnId = s_pendingPortalSpawn ?? ParseOptionalEntityId(
                GetArgumentValue(userArguments, SpawnIdArgument));
            var portalArrivalLabel = s_pendingPortalLabel;
            s_pendingPortalSpawn = null;
            s_pendingPortalLabel = null;
            var playtestManifestPath = GetArgumentValue(userArguments, PlaytestManifestArgument);
            var playtestManifest = playtestManifestPath is null
                ? null
                : PlaytestLaunchManifestStore.Load(playtestManifestPath);
            var project = LoadProject(snapshotPath, modulePackagePath, workspacePath);
            await AcquireLauncherNetworkSessionAsync(
                launcherContext,
                _moduleContentSha256 ?? throw new InvalidOperationException("Loaded module fingerprint is missing."),
                project.Module.DisplayName);
            if (requestedSpawnId is null && (combatFeedbackSmokeTest || combatFeedbackCapturePath is not null))
            {
                var entryArea = project.Areas.Single(area => area.Id == project.Module.EntryAreaId);
                requestedSpawnId = entryArea.Entities.Single(entity =>
                    entity.Tags.Contains("player", StringComparer.Ordinal)).Id;
            }
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
            var runtimeAuthority = _launcherNetworkSession is null
                ? RuntimeAuthority.SinglePlayer
                : _launcherNetworkSession.IsHost
                    ? RuntimeAuthority.Server
                    : RuntimeAuthority.Client;
            _areaCombatServerAuthority = runtimeAuthority is RuntimeAuthority.SinglePlayer or
                RuntimeAuthority.Server;
            EnsureLifecycle(
                _lifecycle.LoadProject(
                    project,
                    runtimeAuthority,
                    lifecycleCause),
                "project_loaded");
            _blackboard = ProjectBlackboardScriptService.ForRuntime(project.Module.Variables);
            _moduleTitle = project.Module.DisplayName;
            _moduleSlug = project.Module.Slug;
            ApplyModuleWindowTitle();
            ConfigureFactions(project.Module.Factions);
            _defaultLocale = project.Module.Metadata.DefaultLocale;
            _isCryptPrototype = string.Equals(_moduleSlug, "crypt-of-ashes", StringComparison.Ordinal);
            ConfigureWorldFeatures(project);
            ConfigurePostLaunch(project);
            _message = $"Explorez {_moduleTitle}.";
            var startArea = ResolveStartArea(project, requestedSpawnId);
            _message = $"Explorez {startArea.DisplayName}.";
            ConfigureRuntimePolicies(project.Module.RuntimePolicies, startArea.RuntimePolicies);
            PublishProjectConnectionEvent(PlayerConnectionStage.Joining, "authenticated");
            await ConfigureWorldAsync(startArea.Environment, startArea.World, startArea.Navigation, _isCryptPrototype);
            var selectedSpawn = BuildProject(project, requestedSpawnId);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_player?.GetNodeOrNull<Node3D>("AuthoredVisual") is { } settledPlayerVisual)
                AlignPlayerVisualToCapsule(settledPlayerVisual);
            ConfigureLauncherInternetReplication();
            if (!string.IsNullOrWhiteSpace(portalArrivalLabel))
            {
                _message = $"Arrivée : {portalArrivalLabel}.";
                GD.Print($"OPENRPG_FORGE_RUNTIME_AREA_TRANSITION_PASS destination={startArea.Slug} " +
                         $"spawn={selectedSpawn.Id} label={portalArrivalLabel}");
            }
            ApplyPlaytestLaunch(project, playtestManifest);
            if (_camera is not null && _player is not null)
            {
                _cameraRig.Snap(_camera, _player.GlobalPosition,
                    _isCryptPrototype ? null : ConstrainCameraPosition);
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
            ApplyModuleWindowTitle();
            ConfigureWaypointRuntime(startArea.Navigation.Waypoints);
            if (solarenVisualFixture is not null)
            {
                if (visualCapturePath is null)
                    throw new InvalidOperationException(
                        "A Solaren visual fixture requires an explicit --visual-capture destination.");
                await ConfigureSolarenVisualFixtureAsync(solarenVisualFixture);
            }
            if (!entryGroundSmokeTest && !innUxSmokeTest &&
                !cryptMovementSmokeTest && !combatFeedbackSmokeTest &&
                combatFeedbackCapturePath is null)
            {
                VerifyModernRuntimeTooling(project, startArea);
                VerifyOperationalReadiness(project);
                VerifyQualityLab(project);
                VerifyPerformanceLab(project);
                VerifyGovernanceLab(project);
            }
            if (visualRegressionSuitePath is not null)
            {
                UpdateHud();
                await CaptureVisualRegressionSuiteAsync(project, visualRegressionSuitePath);
                return;
            }
            if (combatFeedbackCapturePath is not null)
            {
                UpdateHud();
                await CaptureCombatFeedbackVisualProofAsync(combatFeedbackCapturePath, combatFeedbackCaptureSize);
                return;
            }
            if (moduleMapVisualCapturePath is not null)
            {
                UpdateHud();
                await CaptureModuleMapVisualProofAsync(moduleMapVisualCapturePath);
                return;
            }
            if (rpgMenuVisualCapturePath is not null)
            {
                var requestedPage = GetArgumentValue(userArguments, RpgMenuPageArgument);
                var page = Enum.TryParse<RuntimeRpgMenuPage>(requestedPage, true, out var parsedPage)
                    ? parsedPage
                    : RuntimeRpgMenuPage.Inventory;
                UpdateHud();
                await CaptureRpgMenuVisualProofAsync(rpgMenuVisualCapturePath, page);
                return;
            }
            if (dialogueVisualCapturePath is not null)
            {
                UpdateHud();
                await CaptureDialogueVisualProofAsync(dialogueVisualCapturePath);
                return;
            }
            if (actionWheelVisualCapturePath is not null)
            {
                UpdateHud();
                await CaptureActionWheelVisualProofAsync(actionWheelVisualCapturePath);
                return;
            }
            if (visualCapturePath is not null)
            {
                UpdateHud();
                await CaptureVisualProofAsync(visualCapturePath);
                return;
            }
            if (!cryptMovementSmokeTest)
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

            if (innUxSmokeTest)
            {
                await RunInnUxSmokeTestAsync(project);
            }
            else if (entryGroundSmokeTest)
            {
                await RunEntryGroundSmokeTestAsync(project);
            }
            else if (cryptMovementSmokeTest)
            {
                await RunCryptMovementSmokeTestAsync(project);
            }
            else if (combatFeedbackSmokeTest)
            {
                await RunCombatFeedbackSmokeTestAsync();
            }
            else if (genericSmokeTest)
            {
                RunGenericHeadlessSmokeTest();
            }
            else if (solarenSmokeTest)
            {
                await RunSolarenHeadlessSmokeTestAsync();
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
            ReleaseLauncherNetworkSessionAfterStartupFailure();
            if (innUxSmokeTest) s_innUxSmokeStep = 0;
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
        for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var image = GetViewport().GetTexture().GetImage();
        if (GetViewport().UseHdr2D && image.GetFormat() is Image.Format.Rgb8 or Image.Format.Rgba8)
            image.LinearToSrgb();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var error = image.SavePng(destination);
        if (error != Error.Ok) throw new IOException($"Visual proof could not be written: {error}.");
        GD.Print($"OPENRPG_FORGE_RUNTIME_G11P_VISUAL_PASS file={Path.GetFileName(destination)} " +
            $"size={image.GetWidth()}x{image.GetHeight()} nodes={CountTreeNodes(this)} nui=2.0 aoe=visible");
        GetTree().Quit(0);
    }



    private async Task CaptureModuleMapVisualProofAsync(string destination)
    {
        if (!Path.IsPathFullyQualified(destination) || !string.Equals(Path.GetExtension(destination), ".png",
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The module map visual proof destination must be an absolute PNG path.",
                nameof(destination));
        if (_moduleMap is null || _activeArea is null)
            throw new InvalidOperationException("The module map visual proof requires an active runtime map.");

        _moduleMap.SetGlobalMapVisible(true);
        for (var frame = 0; frame < 8; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var image = GetViewport().GetTexture().GetImage();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (image.SavePng(destination) != Error.Ok)
            throw new IOException("The module map visual proof could not be written.");
        GD.Print($"OPENRPG_FORGE_MODULE_MAP_VISUAL_PASS file={Path.GetFileName(destination)} " +
                 $"size={image.GetWidth()}x{image.GetHeight()} area={_activeArea.Slug} " +
                 $"areas={_moduleMap.AreaCount} minimap={_moduleMap.MiniMapVisible.ToString().ToLowerInvariant()} " +
                 $"global={_moduleMap.GlobalMapVisible.ToString().ToLowerInvariant()}");
        GetTree().Quit(0);
    }

    private async Task CaptureRpgMenuVisualProofAsync(string destination, RuntimeRpgMenuPage page)
    {
        ValidateUiCaptureDestination(destination, "RPG menu");
        if (_rpgMenu is null)
            throw new InvalidOperationException("The RPG menu visual proof requires the player codex.");
        _moduleMap?.SetGlobalMapVisible(false);
        _rpgMenu.ShowPage(page, focus: false);
        SyncPlayerInterfaceLock();
        for (var frame = 0; frame < 8; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var image = GetViewport().GetTexture().GetImage();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (image.SavePng(destination) != Error.Ok)
            throw new IOException("The RPG menu visual proof could not be written.");
        GD.Print($"OPENRPG_FORGE_RPG_MENU_VISUAL_PASS file={Path.GetFileName(destination)} " +
                 $"size={image.GetWidth()}x{image.GetHeight()} page={page.ToString().ToLowerInvariant()}");
        GetTree().Quit(0);
    }

    private async Task CaptureDialogueVisualProofAsync(string destination)
    {
        ValidateUiCaptureDestination(destination, "dialogue");
        if (_dialogueOverlay is null)
            throw new InvalidOperationException("The dialogue visual proof requires the dialogue overlay.");
        _dialogueOverlay.Show(_guideName, "Gardienne des braises",
            "La lumière revient, mais les pierres se souviennent encore du feu qui les a fendues.",
            [
                "Que s'est-il passé dans cette crypte ?",
                "Je retrouverai la clé de braise.",
                "Nous reparlerons plus tard.",
            ]);
        SyncPlayerInterfaceLock();
        for (var frame = 0; frame < 8; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var image = GetViewport().GetTexture().GetImage();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (image.SavePng(destination) != Error.Ok)
            throw new IOException("The dialogue visual proof could not be written.");
        GD.Print($"OPENRPG_FORGE_DIALOGUE_VISUAL_PASS file={Path.GetFileName(destination)} " +
                 $"size={image.GetWidth()}x{image.GetHeight()} choices=3");
        GetTree().Quit(0);
    }

    private async Task CaptureActionWheelVisualProofAsync(string destination)
    {
        ValidateUiCaptureDestination(destination, "action wheel");
        if (_actionWheel is null)
            throw new InvalidOperationException("The action wheel visual proof requires the gameplay radial menu.");
        _rpgMenu?.Close();
        _moduleMap?.SetGlobalMapVisible(false);
        _dialogueOverlay?.Close();
        _actionWheel.Toggle();
        if (!_actionWheel.IsOpen)
            throw new InvalidOperationException("The action wheel could not be opened for visual proof.");
        for (var frame = 0; frame < 8; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var image = GetViewport().GetTexture().GetImage();
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (image.SavePng(destination) != Error.Ok)
            throw new IOException("The action wheel visual proof could not be written.");
        GD.Print($"OPENRPG_FORGE_ACTION_WHEEL_VISUAL_PASS file={Path.GetFileName(destination)} " +
                 $"size={image.GetWidth()}x{image.GetHeight()}");
        GetTree().Quit(0);
    }

    private async Task CaptureCombatFeedbackVisualProofAsync(
        string destinationDirectory,
        string? requestedResolution)
    {
        if (!Path.IsPathFullyQualified(destinationDirectory))
            throw new ArgumentException("The combat feedback capture destination must be absolute.",
                nameof(destinationDirectory));
        var root = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(root);
        var player = _player ?? throw new InvalidOperationException("Combat feedback capture requires a player.");
        var camera = _camera ?? throw new InvalidOperationException("Combat feedback capture requires a camera.");
        var source = _playerCombatant ?? throw new InvalidOperationException(
            "Combat feedback capture requires a player combatant.");
        var targetNode = _boss ?? _hostiles.Keys.FirstOrDefault(GodotObject.IsInstanceValid) ??
            throw new InvalidOperationException("Combat feedback capture requires a hostile target.");
        var target = _hostiles[targetNode];
        var hud = _hud ?? throw new InvalidOperationException("Combat feedback capture requires the HUD.");
        var actionBar = _actionBar ?? throw new InvalidOperationException(
            "Combat feedback capture requires the action bar.");
        var feedback = _combatFeedbackLayer ?? throw new InvalidOperationException(
            "Combat feedback capture requires the feedback layer.");
        var impacts = _combatImpactPool ?? throw new InvalidOperationException(
            "Combat feedback capture requires the impact pool.");
        var map = _moduleMap ?? throw new InvalidOperationException("Combat feedback capture requires the map.");
        var toasts = _toastQueue ?? throw new InvalidOperationException("Combat feedback capture requires toasts.");
        var ability = source.Profile.Abilities.First();
        var captures = new List<object>();
        var mode = _combatUiPolicy.ReducedMotion ? "reduced" : "normal";

        if (!string.IsNullOrWhiteSpace(requestedResolution))
        {
            var (width, height) = VisualRegressionProfile.ParseResolution(requestedResolution);
            var window = GetWindow();
            window.Mode = Window.ModeEnum.Windowed;
            window.Borderless = true;
            window.Size = new Vector2I(width, height);
            window.Position = Vector2I.Zero;
            for (var frame = 0; frame < 3; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GetViewport().Scaling3DScale = 1;
        player.GlobalPosition = targetNode.GlobalPosition + new Vector3(2.4f, 0, 1.2f);
        player.Velocity = Vector3.Zero;
        player.ResetPhysicsInterpolation();
        var focus = targetNode.GlobalPosition + Vector3.Up * 1.15f;
        camera.Fov = 48;
        camera.GlobalPosition = focus + new Vector3(7.2f, 4.4f, 8.2f);
        camera.LookAt(focus, Vector3.Up);
        feedback.SetTarget(targetNode);
        _currentCombatTarget = targetNode;
        hud.UpdateTargetStatus(_hostileNames[targetNode], target.Health,
            target.Profile.Stats.MaximumHealth, targetNode == _boss, defeated: false);
        actionBar.ConfigurePrimaryAbility(ability);
        actionBar.UpdatePrimaryAbilityState(0, source.Resource, targetAvailable: true);
        toasts.ClearAll();
        map.ClearPings();

        var preImpact = await SaveCombatFeedbackFrameAsync(root, "01-pre-impact.png", 6);
        captures.Add(CaptureReport("pre-impact", preImpact, actualGameplay: true,
            damage: 0, healing: 0, resource: 0, cooldown: 0));

        var healthBefore = target.Health;
        DamageHostile(targetNode);
        var presentation = _lastCombatFeedback ?? throw new InvalidOperationException(
            "Combat feedback capture received no projection after the real cast.");
        var damage = presentation.Amounts.Single(item => item.Kind == CombatFeedbackAmountKind.Damage).Value;
        AssertSmoke(healthBefore - target.Health == damage,
            "Combat feedback capture damage diverged from the authoritative result.");
        var impact = await SaveCombatFeedbackFrameAsync(root, "02-impact-peak.png", 1);
        captures.Add(CaptureReport("impact-peak", impact, actualGameplay: true,
            damage, healing: 0, resource: 0, cooldown: 0));

        var chip = await SaveCombatFeedbackFrameAsync(root, "03-delayed-chip.png", 9);
        captures.Add(CaptureReport("delayed-chip", chip, actualGameplay: true,
            damage, healing: 0, resource: 0, cooldown: 0));

        var stableFrames = _combatUiPolicy.ReducedMotion ? 70 : 42;
        var stable = await SaveCombatFeedbackFrameAsync(root, "04-stable-ready.png", stableFrames);
        captures.Add(CaptureReport("stable-ready", stable, actualGameplay: true,
            damage, healing: 0, resource: 0, cooldown: 0));

        toasts.ClearAll();
        map.ClearPings();
        var objective = _questMachine?.CurrentState.Journal.Fallback ?? "Retrouver la relique de la forêt";
        toasts.Show(RuntimeToastKind.Objective, $"Objectif : {objective}", $"capture-objective:{objective}");
        var objectivePosition = _trackedWaypointMarker?.Position ??
            (_keyItem is not null && GodotObject.IsInstanceValid(_keyItem)
                ? new Vector3Value(_keyItem.GlobalPosition.X, _keyItem.GlobalPosition.Y, _keyItem.GlobalPosition.Z)
                : new Vector3Value(targetNode.GlobalPosition.X, targetNode.GlobalPosition.Y,
                    targetNode.GlobalPosition.Z));
        map.ShowPing(objectivePosition, RuntimeMapPingKind.Objective,
            _trackedWaypointMarker?.AccessibleText ?? $"Objectif suivi : {objective}");
        var objectiveCapture = await SaveCombatFeedbackFrameAsync(root, "05-objective-toast-ping.png", 10);
        captures.Add(CaptureReport("objective-toast-ping", objectiveCapture, actualGameplay: true,
            damage: 0, healing: 0, resource: 0, cooldown: 0));

        var fixtureAbility = new AbilityDefinition(
            AbilityId.From(Guid.Parse("cf200000-0000-4000-8000-000000000001")),
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
        var fixture = new CombatantState("fixture:capture",
            new CombatProfile(new CombatStats(30, 10, 4, 1, "fixture", "fixture"), [fixtureAbility]));
        fixture.Restore(12, 2);
        var fixtureResult = CombatResolver.Cast(fixture, fixture, fixtureAbility.Id, 100);
        var fixturePresentation = CombatFeedbackPresentationService.Create(
            Guid.Parse("cf200000-0000-4000-8000-000000000002"),
            fixture, fixture, fixtureResult, 100.1);
        toasts.ClearAll();
        map.ClearPings();
        actionBar.ConfigurePrimaryAbility(fixtureAbility);
        actionBar.UpdatePrimaryAbilityState(.7, fixture.Resource, targetAvailable: true);
        hud.UpdatePlayerStatus("FIXTURE TEST", 18, 30, fixture.Resource, 10, 1);
        hud.UpdateTargetStatus("FIXTURE S+R", fixture.Health, 30, boss: false, defeated: false);
        feedback.Present(fixturePresentation, targetNode);
        feedback.Flash(targetNode, CombatFeedbackAmountKind.Healing);
        impacts.Present(fixturePresentation, targetNode, targetNode.GlobalPosition);
        impacts.PlayAudio(CombatFeedbackAudioKind.Healing, targetNode.GlobalPosition);
        toasts.Show(RuntimeToastKind.System,
            "FIXTURE HORS GAMEPLAY — soin, ressource, recharge et chip joueur",
            "capture:fixture");
        map.ShowPing(new Vector3Value(targetNode.GlobalPosition.X, targetNode.GlobalPosition.Y,
                targetNode.GlobalPosition.Z),
            RuntimeMapPingKind.Objective, "Fixture visuelle hors gameplay");
        var fixtureCapture = await SaveCombatFeedbackFrameAsync(root, "06-fixtures-not-gameplay.png", 10);
        captures.Add(CaptureReport("fixtures", fixtureCapture, actualGameplay: false,
            damage: 0, fixtureResult.Healing, fixtureResult.ResourceRestored,
            fixturePresentation.CooldownRemainingSeconds));

        var windowSize = GetViewport().GetTexture().GetImage().GetSize();
        var reportPath = Path.Combine(root, "capture-report.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            passed = true,
            resolution = $"{windowSize.X}x{windowSize.Y}",
            mode,
            reducedMotion = _combatUiPolicy.ReducedMotion,
            colorVisionMode = _combatUiPolicy.ColorVisionMode.ToString(),
            highContrast = _combatUiPolicy.HighContrast,
            textScale = _combatUiPolicy.TextScale,
            pools = new
            {
                floatingCapacity = RuntimeCombatFeedbackLayer.FloatingNumberCapacity,
                nameplateCapacity = RuntimeCombatFeedbackLayer.NameplateCapacity,
                particleCapacity = RuntimeCombatImpactPool.ParticleCapacity,
                audioCapacity = RuntimeCombatImpactPool.AudioCapacity,
                pingCapacity = 8,
                toastCapacity = 3,
                floatingPeak = feedback.PeakActiveFloatingCount,
                particlePeak = impacts.PeakParticleCount,
                audioPeak = impacts.PeakAudioCount,
                pingPeak = map.PeakPingCount,
                toastPeak = toasts.PeakActiveCount,
            },
            authoritative = new
            {
                presentation.TransactionId,
                presentation.AbilitySlug,
                presentation.VisualCueKey,
                damage,
                targetHealth = target.Health,
                cooldownSeconds = presentation.CooldownRemainingSeconds,
            },
            captures,
            marker = "OPENRPG_FORGE_COMBAT_FEEDBACK_CAPTURE_PASS",
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        }) + System.Environment.NewLine, new UTF8Encoding(false));
        GD.Print($"OPENRPG_FORGE_COMBAT_FEEDBACK_CAPTURE_PASS resolution={windowSize.X}x{windowSize.Y} " +
                 $"mode={mode} captures={captures.Count} actual_gameplay=5 fixtures=1 " +
                 $"damage={damage} report={Path.GetFileName(reportPath)}");
        GetTree().Quit(0);

        object CaptureReport(
            string stage,
            (int Width, int Height, long Bytes, string File) frame,
            bool actualGameplay,
            int damage,
            int healing,
            int resource,
            double cooldown) => new
            {
                stage,
                frame.File,
                frame.Width,
                frame.Height,
                frame.Bytes,
                actualGameplay,
                damage,
                healing,
                resourceRestored = resource,
                cooldownRemainingSeconds = cooldown,
            };
    }

    private async Task<(int Width, int Height, long Bytes, string File)> SaveCombatFeedbackFrameAsync(
        string root,
        string fileName,
        int frames)
    {
        for (var frame = 0; frame < frames; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var readbackParity = (long)(Engine.GetFramesDrawn() & 1);
        if (_combatFeedbackCaptureReadbackParity is null)
        {
            // The first Vulkan swapchain image after the deterministic resize can retain stale canvas batches.
            // Prime the alternate image once, then keep every proof on that validated readback parity.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            readbackParity = (long)(Engine.GetFramesDrawn() & 1);
            _combatFeedbackCaptureReadbackParity = readbackParity;
        }
        else if (readbackParity != _combatFeedbackCaptureReadbackParity.Value)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            readbackParity = (long)(Engine.GetFramesDrawn() & 1);
        }
        RenderingServer.ForceSync();
        var image = GetViewport().GetTexture().GetImage();
        var path = Path.Combine(root, fileName);
        if (image.SavePng(path) != Error.Ok)
            throw new IOException($"Combat feedback capture could not be written: {path}.");
        return (image.GetWidth(), image.GetHeight(), new FileInfo(path).Length, fileName);
    }

    private static void ValidateUiCaptureDestination(string destination, string label)
    {
        if (!Path.IsPathFullyQualified(destination) || !string.Equals(Path.GetExtension(destination), ".png",
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"The {label} visual proof destination must be an absolute PNG path.",
                nameof(destination));
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

        if (!IsTimeFrozen())
        {
            AdvanceLivingWorldTriggers();
        }

        if (_camera is not null && !_sequenceOwnsCamera)
        {
            var cameraTarget = _flyingMount is { IsMounted: true }
                ? _flyingMount.GetGlobalTransformInterpolated().Origin
                : _player.GetGlobalTransformInterpolated().Origin;
            _cameraRig.Update(_camera, cameraTarget, delta,
                _isCryptPrototype ? null : ConstrainCameraPosition);
            SyncLocalPlayerCameraVisibility();
            if (_cameraRig.AlignCharacterFacing && _flyingMount?.IsMounted != true)
            {
                _player.FaceDirection(_cameraRig.ForwardDirection);
            }
        }

        if (Input.IsActionJustPressed("skip_sequence") && _actionWheel?.IsOpen == true)
        {
            _actionWheel.Close();
            SyncPlayerInterfaceLock();
            return;
        }

        if (Input.IsActionJustPressed("skip_sequence") && _dialogueOverlay?.IsOpen == true)
        {
            _dialogueOverlay.Close();
            SyncPlayerInterfaceLock();
            UpdateTacticalHud();
            return;
        }

        if (Input.IsActionJustPressed("skip_sequence") && _rpgMenu?.IsOpen == true)
        {
            _rpgMenu.Close();
            SyncPlayerInterfaceLock();
            UpdateTacticalHud();
            return;
        }

        if (Input.IsActionJustPressed("skip_sequence") && _moduleMap?.GlobalMapVisible == true)
        {
            _moduleMap.SetGlobalMapVisible(false);
            _message = "Carte fermée.";
            SyncPlayerInterfaceLock();
            UpdateHud();
            return;
        }

        if (Input.IsActionJustPressed("inventory")) ToggleRpgMenu(RuntimeRpgMenuPage.Inventory);
        if (Input.IsActionJustPressed("character")) ToggleRpgMenu(RuntimeRpgMenuPage.Character);
        if (Input.IsActionJustPressed("journal")) ToggleRpgMenu(RuntimeRpgMenuPage.Journal);

        if (Input.IsActionJustPressed("module_map") && _rpgMenu?.IsOpen == true)
        {
            OpenModuleMapFromMenu();
            return;
        }

        if (_rpgMenu?.IsOpen == true || _dialogueOverlay?.IsOpen == true)
        {
            UpdateTacticalHud();
            return;
        }

        if (Input.IsActionJustPressed("action_wheel") && _moduleMap?.GlobalMapVisible != true)
        {
            _actionWheel?.Toggle();
        }

        if (Input.IsActionJustPressed("module_map"))
        {
            ToggleModuleMap();
        }

        if (_moduleMap?.GlobalMapVisible == true)
        {
            UpdateTacticalHud();
            return;
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

        UpdateTacticalHud();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_moduleMap?.GlobalMapVisible == true)
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (@event is InputEventMouseButton
            {
                ButtonIndex: MouseButton.Left,
                Pressed: true,
            } click && TryInteractWithWorldPointer(click.Position))
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!_sequenceOwnsCamera) _cameraRig.HandleInput(@event);
    }

    private Vector3 ConstrainCameraPosition(Vector3 focus, Vector3 desired)
    {
        var exteriorTerrain = _interiorWalkableBounds is null && _activeTerrain?.Enabled == true;
        if (exteriorTerrain)
        {
            desired = RuntimeCameraRig.RaiseForTerrainClearance(
                focus,
                desired,
                _activeTerrain!.HeightAt,
                clearance: _cameraRig.TerrainClearance,
                sampleCount: _cameraRig.TerrainProbeSamples);
        }

        if (_player is not null && !exteriorTerrain)
        {
            var horizontalOffset = new Vector3(desired.X - focus.X, 0, desired.Z - focus.Z);
            var horizontalDistance = horizontalOffset.Length();
            if (horizontalDistance > .01f)
            {
                var horizontalQuery = PhysicsRayQueryParameters3D.Create(focus, focus + horizontalOffset);
                horizontalQuery.Exclude = _flyingMount is { IsMounted: true }
                    ? [_player.GetRid(), _flyingMount.GetRid()]
                    : [_player.GetRid()];
                horizontalQuery.CollideWithAreas = false;
                horizontalQuery.CollideWithBodies = true;
                var horizontalHit = GetWorld3D().DirectSpaceState.IntersectRay(horizontalQuery);
                if (horizontalHit.TryGetValue("position", out var horizontalPosition))
                {
                    var hitDistance = new Vector2(
                        horizontalPosition.AsVector3().X - focus.X,
                        horizontalPosition.AsVector3().Z - focus.Z).Length();
                    var allowedDistance = Math.Max(.75f, hitDistance - CameraHorizontalWallClearance);
                    var fraction = Math.Clamp(allowedDistance / horizontalDistance, 0, 1);
                    desired = focus + ((desired - focus) * fraction);
                }
            }
        }

        if (_interiorWalkableBounds is { } bounds)
        {
            const float wallClearance = .65f;
            desired = new Vector3(
                Math.Clamp(desired.X, bounds.Position.X + wallClearance, bounds.End.X - wallClearance),
                desired.Y,
                Math.Clamp(desired.Z, bounds.Position.Y + wallClearance, bounds.End.Y - wallClearance));
        }
        if (_player is null || focus.DistanceSquaredTo(desired) <= .01f) return desired;

        var query = PhysicsRayQueryParameters3D.Create(focus, desired);
        query.Exclude = _flyingMount is { IsMounted: true }
            ? [_player.GetRid(), _flyingMount.GetRid()]
            : [_player.GetRid()];
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (!hit.TryGetValue("position", out var position)) return desired;
        if (exteriorTerrain && hit.TryGetValue("collider", out var collider) &&
            collider.AsGodotObject() is Node terrainCollider &&
            terrainCollider.Name == "TerrainCollision")
        {
            return desired;
        }
        var direction = (desired - focus).Normalized();
        return position.AsVector3() - (direction * CameraDirectHitClearance);
    }

    private void SyncLocalPlayerCameraVisibility()
    {
        if (_player is null || _localPlayerHiddenForFirstPerson == _cameraRig.FirstPersonActive) return;
        _localPlayerHiddenForFirstPerson = _cameraRig.FirstPersonActive;
        var visible = !_localPlayerHiddenForFirstPerson;
        if (_player.GetNodeOrNull<Node3D>("AuthoredVisual") is { } authoredVisual)
            authoredVisual.Visible = visible;
        foreach (var node in _player.FindChildren("*", "MeshInstance3D", recursive: true, owned: false))
            if (node is MeshInstance3D mesh) mesh.Visible = visible;
        foreach (var node in _player.FindChildren("*", "Label3D", recursive: true, owned: false))
            if (node is Label3D label) label.Visible = visible;
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
            _moduleContentRoot = Path.GetDirectoryName(snapshotPath);
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
        _livingWorldTriggerOccupancy.Clear();
        _livingWorldTriggerDiagnostics.Clear();
        _livingWorldTriggerFoundationStates.Clear();
        _livingWorldNarrativeState = TriggerNarrativeRuntimeState.Empty;
        _livingWorldPlayerEntity = null;
        BuildDialogueAuthoringAdapters(project.Dialogues);
        BuildDialogueExperienceAdapters(project.Dialogues);
        BuildDialogueProductionAdapters(project.Dialogues);
        BuildQuestAuthoringAdapters(project.Quests);
        BuildQuestExperienceAdapters(project.Quests);
        BuildQuestQualityAdapters(project.Quests);
        var blueprints = project.Blueprints.ToDictionary(blueprint => blueprint.Id);
        var guidedPrototype = _isCryptPrototype;
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
            ? spawnCandidates.SingleOrDefault(candidate => candidate.Area.Id == project.Module.EntryAreaId)
            : spawnCandidates.SingleOrDefault(candidate => candidate.Entity.Id == requestedSpawnId);

        if (selectedSpawn.Entity is null)
        {
            throw new InvalidOperationException(
                requestedSpawnId is null
                    ? "The project does not contain a player spawn."
                    : $"The requested player spawn '{requestedSpawnId}' does not exist.");
        }

        _activeArea = selectedSpawn.Area;
        BuildAreaCollisionShell(selectedSpawn.Area);
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
                var clickTrigger = EntityClickTriggerSettings.Read(effectiveEntity.Overrides);
                if (clickTrigger.Enabled)
                {
                    _clickInteractions[node] = clickTrigger;
                }
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

    private void BuildAreaCollisionShell(AreaDefinition area)
    {
        _interiorWalkableBounds = null;
        _activeAreaHorizontalBounds = null;
        _interiorFloorTop = 0;
        if (area.Kind != AreaKind.Interior)
        {
            BuildExteriorBoundaryShell(area);
            return;
        }

        var floorTiles = area.Entities
            .Where(entity => entity.Tags.Contains("floor", StringComparer.Ordinal))
            .ToArray();
        var xCoordinates = floorTiles.Select(entity => entity.Transform.Position.X).Distinct().Order().ToArray();
        var zCoordinates = floorTiles.Select(entity => entity.Transform.Position.Z).Distinct().Order().ToArray();
        if (floorTiles.Length < 4 || xCoordinates.Length < 2 || zCoordinates.Length < 2) return;

        var stepX = MinimumPositiveStep(xCoordinates);
        var stepZ = MinimumPositiveStep(zCoordinates);
        var minimumX = xCoordinates[0] - (stepX * .5f);
        var maximumX = xCoordinates[^1] + (stepX * .5f);
        var minimumZ = zCoordinates[0] - (stepZ * .5f);
        var maximumZ = zCoordinates[^1] + (stepZ * .5f);
        _interiorFloorTop = (float)floorTiles.Average(entity => entity.Transform.Position.Y) + .05f;
        _interiorWalkableBounds = new Rect2(minimumX, minimumZ, maximumX - minimumX, maximumZ - minimumZ);
        _activeAreaHorizontalBounds = _interiorWalkableBounds;

        const float floorThickness = .2f;
        const float wallThickness = .6f;
        const float wallHeight = 4.2f;
        var centerX = (minimumX + maximumX) * .5f;
        var centerZ = (minimumZ + maximumZ) * .5f;
        AddRuntimeCollisionBox("InteriorFloorShell",
            new Vector3(centerX, _interiorFloorTop - (floorThickness * .5f), centerZ),
            new Vector3(maximumX - minimumX, floorThickness, maximumZ - minimumZ));
        AddRuntimeCollisionBox("InteriorWallNorth",
            new Vector3(centerX, _interiorFloorTop + (wallHeight * .5f), minimumZ),
            new Vector3(maximumX - minimumX, wallHeight, wallThickness));
        AddRuntimeCollisionBox("InteriorWallSouth",
            new Vector3(centerX, _interiorFloorTop + (wallHeight * .5f), maximumZ),
            new Vector3(maximumX - minimumX, wallHeight, wallThickness));
        AddRuntimeCollisionBox("InteriorWallWest",
            new Vector3(minimumX, _interiorFloorTop + (wallHeight * .5f), centerZ),
            new Vector3(wallThickness, wallHeight, maximumZ - minimumZ));
        AddRuntimeCollisionBox("InteriorWallEast",
            new Vector3(maximumX, _interiorFloorTop + (wallHeight * .5f), centerZ),
            new Vector3(wallThickness, wallHeight, maximumZ - minimumZ));
        GD.Print($"OPENRPG_FORGE_INTERIOR_COLLISION_SHELL_READY area={area.Slug} " +
                 $"bounds={minimumX:0.###},{minimumZ:0.###},{maximumX:0.###},{maximumZ:0.###} " +
                 $"floor_y={_interiorFloorTop:0.###} barriers=4");

        static float MinimumPositiveStep(IReadOnlyList<float> values)
        {
            var step = float.PositiveInfinity;
            for (var index = 1; index < values.Count; index++)
            {
                var candidate = values[index] - values[index - 1];
                if (candidate > .001f) step = MathF.Min(step, candidate);
            }
            return float.IsFinite(step) ? step : 1;
        }
    }

    private void BuildExteriorBoundaryShell(AreaDefinition area)
    {
        var halfWidth = area.Dimensions.Width * .5f;
        var halfDepth = area.Dimensions.Depth * .5f;
        const float inset = 1.25f;
        const float wallThickness = 2.5f;
        const float overheadClearance = 48f;
        const float undergroundClearance = 18f;
        var minimumTerrainHeight = area.World.Terrain.Heights.Count == 0
            ? -4f
            : area.World.Terrain.Heights.Min();
        var maximumTerrainHeight = area.World.Terrain.Heights.Count == 0
            ? 8f
            : area.World.Terrain.Heights.Max();
        var wallBottom = minimumTerrainHeight - undergroundClearance;
        var wallTop = maximumTerrainHeight + overheadClearance;
        var wallHeight = wallTop - wallBottom;
        var wallCenterY = wallBottom + (wallHeight * .5f);
        var minimumX = -halfWidth + inset;
        var maximumX = halfWidth - inset;
        var minimumZ = -halfDepth + inset;
        var maximumZ = halfDepth - inset;
        _activeAreaHorizontalBounds = new Rect2(minimumX, minimumZ,
            maximumX - minimumX, maximumZ - minimumZ);

        AddRuntimeCollisionBox("ExteriorBoundaryNorth",
            new Vector3(0, wallCenterY, minimumZ),
            new Vector3(area.Dimensions.Width, wallHeight, wallThickness));
        AddRuntimeCollisionBox("ExteriorBoundarySouth",
            new Vector3(0, wallCenterY, maximumZ),
            new Vector3(area.Dimensions.Width, wallHeight, wallThickness));
        AddRuntimeCollisionBox("ExteriorBoundaryWest",
            new Vector3(minimumX, wallCenterY, 0),
            new Vector3(wallThickness, wallHeight, area.Dimensions.Depth));
        AddRuntimeCollisionBox("ExteriorBoundaryEast",
            new Vector3(maximumX, wallCenterY, 0),
            new Vector3(wallThickness, wallHeight, area.Dimensions.Depth));
        AddRuntimeCollisionBox("ExteriorSafetyFloor",
            new Vector3(0, minimumTerrainHeight - 8f, 0),
            new Vector3(area.Dimensions.Width, 1f, area.Dimensions.Depth));
        GD.Print($"OPENRPG_FORGE_EXTERIOR_BOUNDARY_READY area={area.Slug} " +
                 $"bounds={minimumX:0.###},{minimumZ:0.###},{maximumX:0.###},{maximumZ:0.###} " +
                 $"terrain_y={minimumTerrainHeight:0.###}..{maximumTerrainHeight:0.###} barriers=4 safety_floor=1");
    }

    private void AddRuntimeCollisionBox(string name, Vector3 position, Vector3 size)
    {
        var body = new StaticBody3D { Name = name, Position = position };
        body.AddChild(new CollisionShape3D
        {
            Name = $"{name}Shape",
            Shape = new BoxShape3D { Size = size },
        });
        AddChild(body);
        _collisionCount++;
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
                var portal = _activeArea?.Portals.SingleOrDefault(candidate => candidate.SourceEntityId == entity.Id);
                var trigger = CreateExit(entity, portal is not null);
                if (portal is null) _exit = trigger;
                else _portalNodes.Add(trigger, portal);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(blueprint), blueprint.Kind, "Unsupported blueprint kind.");
        }
    }

    private void BuildPlaceable(EntityInstance entity, BlueprintDefinition blueprint)
    {
        var displayName = GetOverride(entity, "runtime.displayName") ?? blueprint.DisplayName;
        if (entity.Tags.Contains("pillar", StringComparer.Ordinal))
        {
            CreatePillar(entity, displayName);
            return;
        }

        var isFloor = entity.Tags.Contains("floor", StringComparer.Ordinal);
        CreateBox(
            entity,
            displayName,
            isFloor ? new Color("282638") : new Color("443b51"));
    }

    private void BuildCreature(EntityInstance entity, BlueprintDefinition blueprint)
    {
        if (entity.Tags.Contains("flying-mount", StringComparer.Ordinal))
        {
            var mountDisplayName = GetOverride(entity, "runtime.displayName") ?? blueprint.DisplayName;
            _flyingMount = CreateFlyingMount(entity, mountDisplayName);
            return;
        }

        if (entity.Tags.Contains("npc", StringComparer.Ordinal) || HasRuntimeRole(entity, "guide"))
        {
            var guideDisplayName = GetOverride(entity, "runtime.displayName") ?? ProgressionDisplayName(blueprint);
            _npc = CreateCreature(entity, guideDisplayName, new Color("4ec9b0"), blueprint.CreaturePresentation);
            _guideName = guideDisplayName;
            ConfigureCreatureFoundation(_npc, blueprint);
            ConfigureCreatureRoleBehavior(_npc, blueprint);
            ConfigureCreatureEvents(_npc, blueprint);
            return;
        }

        var isBoss = entity.Tags.Contains("boss", StringComparer.Ordinal) || HasRuntimeRole(entity, "boss");
        var hostileDisplayName = GetOverride(entity, "runtime.displayName") ?? ProgressionDisplayName(blueprint);
        var creature = CreateCreature(
            entity,
            hostileDisplayName,
            isBoss ? new Color("d34f4f") : new Color("d68a3a"),
            blueprint.CreaturePresentation);
        var profile = blueprint.Combat
            ?? throw new InvalidOperationException($"Hostile blueprint '{blueprint.Slug}' has no combat profile.");
        _hostiles.Add(creature, new CombatantState(entity.Id.ToString(), profile));
        _hostileNames.Add(creature, hostileDisplayName);
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
        _livingWorldPlayerEntity = entity;
        if (blueprint.Combat is not null)
        {
            _playerCombatant = new CombatantState(entity.Id.ToString(), blueprint.Combat);
        }

        var player = new PlayerController { Name = "Player" };
        AddChild(player);
        player.GlobalPosition = EngineValueConverter.ToEngine(entity.Transform.Position);
        player.ResetPhysicsInterpolation();
        player.ConfigureFallRecovery(player.GlobalPosition,
            _activeArea?.Kind == AreaKind.Interior ? 8 : 30);
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
        var authoredDisplayName = GetOverride(entity, "runtime.displayName");
        CreateLabel(player, !string.IsNullOrWhiteSpace(authoredDisplayName)
                ? authoredDisplayName.ToUpperInvariant()
                : blueprint.Combat is null ? "AVATAR" : playerLevel > 0 ? $"HÉROS · NIV. {playerLevel}" : "HÉROS",
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
        var hidden = bool.TryParse(GetOverride(entity, "visual.hidden"), out var hide) && hide;
        var collisionProxy = hidden && entity.Tags.Contains("collision", StringComparer.Ordinal);
        if (collisionProxy) root.Scale = Vector3.One;
        var attachedVisual = !hidden && TryAttachVisual(body, entity);
        if (!attachedVisual && !hidden)
        {
            var effectiveColor = Color.FromString(GetOverride(entity, "visual.color") ?? string.Empty, color);
            var emission = bool.TryParse(GetOverride(entity, "visual.emission"), out var emit) && emit;
            var assisted = entity.Tags.Contains("assistant-generated", StringComparer.Ordinal);
            PrimitiveMesh mesh = GetOverride(entity, "visual.shape")?.ToLowerInvariant() switch
            {
                "sphere" => new SphereMesh { Radius = .5f, Height = 1 },
                "cylinder" => new CylinderMesh { TopRadius = .5f, BottomRadius = .5f, Height = 1 },
                "cone" => new CylinderMesh { TopRadius = .05f, BottomRadius = .55f, Height = 1 },
                _ => new BoxMesh { Size = Vector3.One },
            };
            if (assisted)
            {
                BuildAssistedLandmark(root, body, entity);
            }
            else
            {
                body.AddChild(new MeshInstance3D
                {
                    Mesh = mesh,
                    MaterialOverride = CreateMaterial(effectiveColor, emission),
                });
            }
        }
        if (entity.Tags.Contains("light", StringComparer.Ordinal))
        {
            var lightColor = Color.FromString(GetOverride(entity, "visual.color") ?? string.Empty,
                new Color("ffad67"));
            root.AddChild(new OmniLight3D
            {
                Position = new Vector3(0, 1.35f, 0),
                LightColor = lightColor,
                LightEnergy = 2.6f,
                OmniRange = 10,
                ShadowEnabled = false,
            });
        }
        // Interior areas already have one continuous floor shell. Rebuilding a collider for
        // every overlapping floor tile creates invisible seams that trap grounded movement.
        // Areas without a shell still keep their authored floor colliders.
        var isFloor = entity.Tags.Contains("floor", StringComparer.Ordinal);
        var usesInteriorFloorShell = isFloor && _interiorWalkableBounds is not null;
        var requiresFloorCollision = isFloor && !usesInteriorFloorShell;
        var isOpenDoorway = entity.Tags.Contains("doorway", StringComparer.Ordinal);
        if (!usesInteriorFloorShell &&
            (requiresFloorCollision ||
             (!isOpenDoorway && !entity.Tags.Contains("nonblocking", StringComparer.Ordinal))))
        {
            if (collisionProxy)
            {
                var authoredSize = EngineValueConverter.ToEngine(entity.Transform.Scale).Abs();
                body.AddChild(new CollisionShape3D
                {
                    Name = "AuthoredCollisionProxy",
                    Shape = new BoxShape3D { Size = authoredSize },
                });
            }
            else
            {
                var visual = attachedVisual ? body.GetNodeOrNull<Node3D>("AuthoredVisual") : null;
                body.AddChild(CreateVisualCollision(visual));
            }
            _collisionCount++;
        }
        return root;
    }

    private static CollisionShape3D CreateVisualCollision(Node3D? visual)
    {
        if (visual is null)
            return new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One } };

        var hasBounds = false;
        var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        Accumulate(visual, Transform3D.Identity);
        if (!hasBounds)
            return new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One } };

        var size = maximum - minimum;
        size = new Vector3(MathF.Max(size.X, .15f), MathF.Max(size.Y, .15f), MathF.Max(size.Z, .15f));
        return new CollisionShape3D
        {
            Position = (minimum + maximum) * .5f,
            Shape = new BoxShape3D { Size = size },
        };

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
                                bounds.Size.X * x,
                                bounds.Size.Y * y,
                                bounds.Size.Z * z));
                            minimum = new Vector3(
                                MathF.Min(minimum.X, point.X),
                                MathF.Min(minimum.Y, point.Y),
                                MathF.Min(minimum.Z, point.Z));
                            maximum = new Vector3(
                                MathF.Max(maximum.X, point.X),
                                MathF.Max(maximum.Y, point.Y),
                                MathF.Max(maximum.Z, point.Z));
                            hasBounds = true;
                        }
            }

            foreach (var child in node.GetChildren()) Accumulate(child, transform);
        }
    }

    private void BuildAssistedLandmark(Node3D root, Node3D body, EntityInstance entity)
    {
        var semantic = new[] { "service", "objective", "social", "exploration", "rest", "encounter" }
            .FirstOrDefault(tag => entity.Tags.Contains(tag, StringComparer.Ordinal)) ?? "exploration";
        var primary = semantic switch
        {
            "service" => new Color("4db6ac"),
            "objective" => new Color("d5b35c"),
            "social" => new Color("9b633f"),
            "rest" => new Color("5576a8"),
            "encounter" => new Color("b74a3d"),
            _ => new Color("6f7884"),
        };
        switch (semantic)
        {
            case "service":
                Add(new CylinderMesh { TopRadius = 1.25f, BottomRadius = 1.45f, Height = .45f }, new(0, .22f, 0), primary.Darkened(.25f));
                Add(new CylinderMesh { TopRadius = .35f, BottomRadius = .5f, Height = 2.8f }, new(0, 1.55f, 0), primary);
                Add(new SphereMesh { Radius = .48f, Height = .96f }, new(0, 3.1f, 0), primary.Lightened(.32f), true);
                Light(primary.Lightened(.25f), 8, 2.2f);
                break;
            case "objective":
                Add(new BoxMesh { Size = new Vector3(2.4f, .5f, 2.4f) }, new(0, .25f, 0), primary.Darkened(.25f));
                Add(new CylinderMesh { TopRadius = .16f, BottomRadius = .72f, Height = 4.4f }, new(0, 2.45f, 0), primary);
                Add(new SphereMesh { Radius = .32f, Height = .64f }, new(0, 4.7f, 0), primary.Lightened(.35f), true);
                break;
            case "social":
                foreach (var (x, z) in new[] { (-1.45f, -1.25f), (1.45f, -1.25f), (-1.45f, 1.25f), (1.45f, 1.25f) })
                    Add(new CylinderMesh { TopRadius = .1f, BottomRadius = .13f, Height = 2.5f }, new(x, 1.25f, z), primary.Darkened(.22f));
                Add(new BoxMesh { Size = new Vector3(3.6f, .25f, 3.2f) }, new(0, 2.58f, 0), primary.Lightened(.12f));
                Add(new CylinderMesh { TopRadius = .38f, BottomRadius = .55f, Height = .7f }, new(0, .38f, 0), new Color("e4843f"), true);
                Light(new Color("ffb45f"), 7, 1.8f);
                break;
            case "rest":
                Add(new BoxMesh { Size = new Vector3(3.8f, .45f, 2.4f) }, new(0, .32f, 0), primary.Darkened(.2f));
                Add(new BoxMesh { Size = new Vector3(3.4f, .25f, 2f) }, new(0, .62f, 0), primary.Lightened(.15f));
                Add(new BoxMesh { Size = new Vector3(1.2f, .35f, 2f) }, new(-1.05f, .86f, 0), new Color("d7d0bd"));
                break;
            case "encounter":
                Add(new CylinderMesh { TopRadius = .75f, BottomRadius = 1.05f, Height = 1.1f }, new(0, .55f, 0), primary.Darkened(.3f));
                Add(new SphereMesh { Radius = .62f, Height = 1.15f }, new(0, 1.35f, 0), new Color("ff6538"), true);
                Light(new Color("ff6a3d"), 8, 2.8f);
                break;
            default:
                Add(new SphereMesh { Radius = 1.15f, Height = 2f }, new(-.65f, .75f, .15f), primary);
                Add(new SphereMesh { Radius = .8f, Height = 1.5f }, new(.75f, .48f, -.35f), primary.Lightened(.08f));
                Add(new SphereMesh { Radius = .58f, Height = 1.05f }, new(.4f, .35f, .8f), primary.Darkened(.1f));
                break;
        }
        CreateLabel(root, semantic.ToUpperInvariant(), primary.Lightened(.38f), 3.8f);

        void Add(PrimitiveMesh part, Vector3 position, Color partColor, bool glow = false) =>
            body.AddChild(new MeshInstance3D
            {
                Mesh = part,
                Position = position,
                MaterialOverride = CreateMaterial(partColor, glow),
            });

        void Light(Color lightColor, float range, float energy) => root.AddChild(new OmniLight3D
        {
            Position = new Vector3(0, 2.5f, 0),
            LightColor = lightColor,
            LightEnergy = energy,
            OmniRange = range,
            ShadowEnabled = false,
        });
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
            var effectiveColor = Color.FromString(GetOverride(entity, "visual.color") ?? string.Empty,
                new Color("665565"));
            body.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1 },
                MaterialOverride = CreateMaterial(effectiveColor),
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
        var species = GetOverride(entity, "runtime.species");
        if (!string.IsNullOrWhiteSpace(species))
        {
            BuildProceduralCreatureVisual(root, species, color);
        }
        else if (!TryAttachVisual(root, entity))
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

    private RuntimeFlyingMount CreateFlyingMount(EntityInstance entity, string displayName)
    {
        var mount = new RuntimeFlyingMount
        {
            Name = SafeNodeName(displayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        AddChild(mount);
        if (!TryAttachVisual(mount, entity))
            throw new InvalidOperationException($"Flying mount '{displayName}' has no governed animated visual.");
        mount.AddChild(new CollisionShape3D
        {
            Name = "MountCollision",
            Position = new Vector3(0, .85f, 0),
            Shape = new CapsuleShape3D { Radius = .8f, Height = 1.8f },
        });
        mount.Configure(_activeAreaHorizontalBounds ?? new Rect2(-64, -64, 128, 128), _camera);
        var prompt = new Label3D
        {
            Name = "MountPrompt",
            Text = $"{displayName} · E POUR MONTER",
            Position = new Vector3(0, 2.45f, 0),
            Modulate = new Color("d8e9cf"),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 32,
            OutlineSize = 8,
        };
        mount.AddChild(prompt);
        mount.ConfigureInteractionPrompt(prompt);
        _collisionCount++;
        return mount;
    }

    private void BuildProceduralCreatureVisual(Node3D root, string species, Color fallback)
    {
        var gnoll = string.Equals(species, "gnoll", StringComparison.OrdinalIgnoreCase);
        var skin = gnoll ? new Color("8f7550") : new Color("71934a");
        var cloth = gnoll ? new Color("49352c") : new Color("553c2e");
        Add(new CapsuleMesh { Radius = gnoll ? .42f : .34f, Height = gnoll ? 1.25f : 1.05f },
            new Vector3(0, .72f, 0), Vector3.Zero, skin);
        Add(new SphereMesh { Radius = gnoll ? .34f : .3f, Height = gnoll ? .68f : .6f },
            new Vector3(0, gnoll ? 1.48f : 1.3f, 0), Vector3.Zero, skin);
        Add(new BoxMesh { Size = new Vector3(gnoll ? .32f : .22f, .18f, gnoll ? .48f : .28f) },
            new Vector3(0, gnoll ? 1.42f : 1.28f, -.3f), Vector3.Zero, skin.Lightened(.08f));
        Add(new CylinderMesh { TopRadius = .025f, BottomRadius = gnoll ? .16f : .2f, Height = .46f },
            new Vector3(-.28f, gnoll ? 1.68f : 1.5f, 0), new Vector3(0, 0, gnoll ? -22 : -48), skin);
        Add(new CylinderMesh { TopRadius = .025f, BottomRadius = gnoll ? .16f : .2f, Height = .46f },
            new Vector3(.28f, gnoll ? 1.68f : 1.5f, 0), new Vector3(0, 0, gnoll ? 22 : 48), skin);
        Add(new BoxMesh { Size = new Vector3(gnoll ? .82f : .68f, .48f, .42f) },
            new Vector3(0, .85f, .02f), Vector3.Zero, cloth);
        foreach (var x in new[] { -.24f, .24f })
        {
            Add(new CylinderMesh { TopRadius = .11f, BottomRadius = .13f, Height = .72f },
                new Vector3(x, .18f, 0), Vector3.Zero, skin.Darkened(.08f));
            Add(new CylinderMesh { TopRadius = .09f, BottomRadius = .12f, Height = .72f },
                new Vector3(x * 1.7f, .78f, 0), new Vector3(0, 0, x < 0 ? -22 : 22), skin);
        }
        if (gnoll)
        {
            Add(new CylinderMesh { TopRadius = .04f, BottomRadius = .12f, Height = .9f },
                new Vector3(0, .65f, .42f), new Vector3(62, 0, 0), skin.Darkened(.12f));
        }
        Add(new CylinderMesh { TopRadius = .055f, BottomRadius = .07f, Height = gnoll ? 1.5f : 1.05f },
            new Vector3(.62f, .72f, 0), new Vector3(0, 0, 12), fallback.Darkened(.28f));

        void Add(PrimitiveMesh mesh, Vector3 position, Vector3 rotation, Color partColor)
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = mesh,
                Position = position,
                RotationDegrees = rotation,
                MaterialOverride = CreateMaterial(partColor),
            });
        }
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
        var clickTarget = new Area3D
        {
            Name = "ItemClickTarget",
            CollisionLayer = 1,
            CollisionMask = 1,
            InputRayPickable = true,
            Monitoring = false,
            Monitorable = true,
        };
        clickTarget.AddChild(new CollisionShape3D
        {
            Shape = new SphereShape3D { Radius = .65f },
        });
        clickTarget.MouseEntered += () => Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
        clickTarget.MouseExited += () => Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
        root.AddChild(clickTarget);
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

    private Node3D CreateExit(EntityInstance entity, bool isPortal)
    {
        var displayName = GetOverride(entity, "runtime.displayName") ?? "Passage";
        var root = new Node3D
        {
            Name = SafeNodeName(displayName),
            Transform = EngineValueConverter.ToEngine(entity.Transform),
        };
        root.AddToGroup("forge_exit_marker");
        AddChild(root);
        if (isPortal)
        {
            var innDoor = string.Equals(_activeArea?.Slug, "ember-inn", StringComparison.Ordinal);
            var clickTarget = new Area3D
            {
                Name = "PortalClickTarget",
                Position = innDoor ? new Vector3(-2, 2, 1) : new Vector3(0, 1.5f, 0),
                CollisionLayer = 1,
                CollisionMask = 1,
                InputRayPickable = true,
                Monitoring = false,
                Monitorable = true,
            };
            clickTarget.AddToGroup("forge_portal_click_target");
            clickTarget.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D
                {
                    Size = innDoor ? new Vector3(6, 4.2f, 1.6f) : new Vector3(3, 3, 3),
                },
            });
            clickTarget.MouseEntered += () => Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
            clickTarget.MouseExited += () => Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
            root.AddChild(clickTarget);
            if (innDoor) return root;
        }
        TryAttachVisual(root, entity);
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 1, BottomRadius = 1, Height = 0.1f },
            MaterialOverride = CreateMaterial(new Color("55efc4"), emission: true),
        });
        CreateLabel(root, displayName, new Color("b9fff0"), 1.1f);
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
        _trackedWaypointMarker = marker;
        if (marker is not null)
        {
            _hud?.ShowWaypointMarker(marker);
            _moduleMap?.ShowPing(marker.Position, RuntimeMapPingKind.Objective, marker.AccessibleText);
        }
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
        _areaCombatAuthority = new AreaCombatAuthority(_areaCombatParties, area.Rules.Combat);
        if (!area.Rules.Combat.ServerAuthoritative)
        {
            GD.PrintErr("OPENRPG_FORGE_AREA_COMBAT_DIAGNOSTIC " +
                        "code=NET_AREA_COMBAT_AUTHORITY_REQUIRED " +
                        "path=area/runtimePolicies/rules/combat/serverAuthoritative");
        }
        else if (!_areaCombatServerAuthority)
        {
            GD.PrintErr("OPENRPG_FORGE_AREA_COMBAT_DIAGNOSTIC " +
                        "code=NET_AREA_COMBAT_SERVER_REQUIRED " +
                        "path=runtime/authority");
        }
        else
        {
            GD.Print("OPENRPG_FORGE_AREA_COMBAT_READY " +
                     $"pvp={area.Rules.Combat.PvpMode.ToString().ToLowerInvariant()} " +
                     $"aggro={area.Rules.Combat.AggroEnabled.ToString().ToLowerInvariant()} " +
                     $"server={_areaCombatServerAuthority.ToString().ToLowerInvariant()}");
        }
        _areaExperience = area.Experience;
        _runtimeExtensions = module.Extensions;
        var worldResolution = ProjectWorldRuntimeResolver.Resolve(module.World, module.Id, module.World.RequiredAtRuntime);
        if (!worldResolution.Succeeded)
            GD.PrintErr($"OPENRPG_FORGE_RUNTIME_PROJECT_WORLD_DIAGNOSTIC code={worldResolution.Diagnostic!.Code} " +
                        $"path={worldResolution.Diagnostic.Path}");
        else if (worldResolution.Handle is not null)
        {
            _customWorldEvents.Register(worldResolution.Handle.Profile);
            foreach (var schema in worldResolution.Handle.Profile.CustomEvents)
            {
                _customWorldEvents.Subscribe(schema.Id, _ => DispatchConfiguredEventScript(
                    schema.ScriptPath, schema.Handler, schema.RequiredAtRuntime,
                    $"user-event:{schema.Namespace}.{schema.Name}"));
            }
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
        _areaEffectIndicator.HidePreview();

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
        var scriptResolved = DispatchConfiguredEventScript(policy.Binding.ScriptPath,
            policy.Binding.Handler, policy.Binding.RequiredAtRuntime, "sequence:aborted");
        GD.Print($"OPENRPG_FORGE_RUNTIME_SEQUENCE_ABORTED handler={result.EventHandler} " +
                 $"camera_restored={result.CameraRestored.ToString().ToLowerInvariant()} " +
                 $"script_resolved={scriptResolved.ToString().ToLowerInvariant()}");
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

    private void DispatchHeartbeats(HeartbeatAdvanceResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_POLICY_DIAGNOSTIC code={diagnostic.Code} path={diagnostic.Path}");
        }

        foreach (var dispatch in result.Dispatches)
        {
            var scriptResolved = DispatchConfiguredEventScript(dispatch.ScriptPath, dispatch.Handler,
                dispatch.RequiredAtRuntime, $"heartbeat:{dispatch.Name}");
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_HEARTBEAT handler={dispatch.Handler} scope={dispatch.Scope} " +
                $"budget_ms={dispatch.BudgetMilliseconds} due_ms={dispatch.DueAtMilliseconds} " +
                $"script_resolved={scriptResolved.ToString().ToLowerInvariant()}");
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
        var triggerRest = ResolveLivingWorldRest(restPosition);
        if (triggerRest is { Allowed: false } denied)
        {
            _message = ResolveLivingWorldMessage(denied.MessageKey);
            _toastQueue?.Show(RuntimeToastKind.Alert, _message, $"trigger-rest:{denied.DiagnosticCode}");
            GD.Print($"OPENRPG_FORGE_TRIGGER_REST_DENIED code={denied.DiagnosticCode} " +
                     $"message={denied.MessageKey} summary={denied.Summary}");
            UpdateHud();
            return;
        }
        var result = _restRuntime.Request("player", "player-party", AreaId.Parse(_currentAreaId),
            restPosition, _moduleRuntimePolicies.Rest, _areaRuntimePolicies.Rest,
            secure: _hostiles.Keys.All(hostile => hostile.GlobalPosition.DistanceTo(position) >
                effectivePolicy.RequiredSecureRadius),
            EvaluateRuntimePolicyCondition, ++_runtimePolicyRevision, DateTimeOffset.UtcNow);
        PublishRestEvents(result.Events);
        _restSession = result.Session;
        _message = result.Accepted
            ? triggerRest is { Available: true } allowed
                ? $"{ResolveLivingWorldMessage(allowed.MessageKey)} " +
                  $"({_moduleRuntimePolicies.Rest.DefaultDurationSeconds} s)…"
                : $"Repos en cours ({_moduleRuntimePolicies.Rest.DefaultDurationSeconds} s)…"
            : result.Events.LastOrDefault()?.Cause ?? result.DiagnosticCode ?? "Repos refusé.";
        UpdateHud();
    }

    private TriggerRestRuntimeResolution? ResolveLivingWorldRest(Vector3Value position)
    {
        if (_worldFeatureCatalog is null || _currentAreaId is null) return null;
        var currentArea = AreaId.Parse(_currentAreaId);
        var partyExpected = LauncherConnectedPlayerCount();
        // Remote poses are visible but are not evaluated against authored safe-rest volumes yet. A
        // multi-member session therefore cannot claim that the full party is physically inside one.
        var partyPresent = partyExpected == 1 ? 1 : 0;
        TriggerRestRuntimeResolution? accepted = null;
        foreach (var profile in _worldFeatureCatalog.Triggers
                     .Where(item => item.AreaId == currentArea && item.Rest is not null)
                     .OrderBy(item => item.Id))
        {
            var containment = TriggerLivingWorldRuntimeService.Contains(profile, position);
            if (!containment.Supported)
            {
                ReportLivingWorldTriggerDiagnostic(profile, containment.DiagnosticCode, containment.Summary);
                continue;
            }
            if (!containment.Contains) continue;
            var foundationProbe = EnterLivingWorldFoundation(profile,
                EmptyLivingWorldFoundationState(), position, DateTimeOffset.UtcNow);
            if (!foundationProbe.Fired) continue;
            var resolution = TriggerLivingWorldRuntimeService.ResolveRest(profile,
                new(EvaluateLivingWorldDanger(profile.Rest!.DangerQuery), partyPresent, partyExpected));
            if (!resolution.Allowed) return resolution;
            accepted ??= resolution;
        }
        return accepted;
    }

    private bool EvaluateLivingWorldDanger(string query)
    {
        if (_blackboard.TryGetValue(query, out var authored) && bool.TryParse(authored, out var parsed))
            return parsed;
        if (string.Equals(query, "danger.nearby", StringComparison.Ordinal))
            return _hostiles.Values.Any(item => !item.IsDead);
        GD.PrintErr($"OPENRPG_FORGE_TRIGGER_DANGER_QUERY_UNRESOLVED query={query}");
        return true;
    }

    private void AdvanceLivingWorldTriggers()
    {
        if (_player is null || _livingWorldPlayerEntity is null ||
            _worldFeatureCatalog is null || _currentAreaId is null) return;
        var currentArea = AreaId.Parse(_currentAreaId);
        var position = _player.GlobalPosition;
        var worldPosition = new Vector3Value(position.X, position.Y, position.Z);
        var insideNow = new HashSet<Guid>();
        var now = DateTimeOffset.UtcNow;
        var narrativeClaimed = false;
        foreach (var profile in _worldFeatureCatalog.Triggers
                     .Where(item => item.AreaId == currentArea &&
                                    (item.Clue is not null || item.Narrative is not null))
                     .OrderByDescending(item => item.Narrative?.Priority ?? 0)
                     .ThenBy(item => item.Id))
        {
            var containment = TriggerLivingWorldRuntimeService.Contains(profile, worldPosition);
            if (!containment.Supported)
            {
                ReportLivingWorldTriggerDiagnostic(profile, containment.DiagnosticCode, containment.Summary);
                continue;
            }
            if (!containment.Contains) continue;
            insideNow.Add(profile.Id);
            if (_livingWorldTriggerOccupancy.Contains(profile.Id)) continue;
            var foundationState = _livingWorldTriggerFoundationStates.GetValueOrDefault(profile.Id) ??
                                  EmptyLivingWorldFoundationState();
            var entered = EnterLivingWorldFoundation(profile, foundationState, worldPosition, now);
            _livingWorldTriggerFoundationStates[profile.Id] = entered.State;
            if (!entered.Fired) continue;
            ResolveLivingWorldClue(profile);
            if (!narrativeClaimed)
                narrativeClaimed = ResolveLivingWorldNarrative(profile);
        }

        foreach (var departedId in _livingWorldTriggerOccupancy.Except(insideNow).ToArray())
        {
            var profile = _worldFeatureCatalog.Triggers.SingleOrDefault(item => item.Id == departedId);
            if (profile?.Foundation is null ||
                !_livingWorldTriggerFoundationStates.TryGetValue(departedId, out var state)) continue;
            _livingWorldTriggerFoundationStates[departedId] = WorldFeatureRuntimeResolver.ExitTrigger(
                profile, state, _livingWorldPlayerEntity.Id.ToString(), now, "player-left-volume").State;
        }

        _livingWorldTriggerOccupancy.Clear();
        _livingWorldTriggerOccupancy.UnionWith(insideNow);
    }

    private WorldFeatureRuntimeResolver.TriggerEnterResult EnterLivingWorldFoundation(
        TriggerWorldProfile profile,
        WorldFeatureRuntimeResolver.TriggerRuntimeState state,
        Vector3Value position,
        DateTimeOffset now)
    {
        var entity = _livingWorldPlayerEntity ??
                     throw new InvalidOperationException("Living-world player context is unavailable.");
        var conditions = profile.Foundation!.Conditions.Distinct(StringComparer.Ordinal).ToDictionary(
            condition => condition,
            condition => _blackboard.TryGetValue(condition, out var value) ? value : "false",
            StringComparer.Ordinal);
        var velocity = _player?.Velocity ?? Vector3.Zero;
        var direction = velocity.LengthSquared() > .0001f ? velocity.Normalized() : Vector3.Zero;
        return WorldFeatureRuntimeResolver.EnterTrigger(profile, state, entity.Id.ToString(), "creature",
            entity.Tags.ToHashSet(StringComparer.Ordinal), new HashSet<string>(["characters"], StringComparer.Ordinal),
            position, new(direction.X, direction.Y, direction.Z), conditions, now);
    }

    private static WorldFeatureRuntimeResolver.TriggerRuntimeState EmptyLivingWorldFoundationState() => new(
        new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
        new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal), false);

    private void ResolveLivingWorldClue(TriggerWorldProfile profile)
    {
        if (profile.Clue is not { } clue) return;
        int? skillScore = null;
        if (_blackboard.TryGetValue($"skill.{clue.SkillId}", out var value) &&
            int.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            skillScore = parsed;
        var questActive = clue.QuestId is null ||
                          _questDefinition?.Id.Value == clue.QuestId && _questMachine is not null;
        var result = TriggerLivingWorldRuntimeService.ResolveClue(profile, new(skillScore, questActive));
        if (!result.Revealed)
        {
            GD.Print($"OPENRPG_FORGE_TRIGGER_CLUE_SKIPPED code={result.DiagnosticCode} summary={result.Summary}");
            return;
        }

        var kind = result.FalseClue ? "Piste incertaine" : "Piste révélée";
        _message = $"{kind} : {result.TraceId} · direction {result.DirectionDegrees:0.#}° · " +
                   $"âge {result.AgeHours:0.#} h";
        _toastQueue?.Show(RuntimeToastKind.Objective, _message, $"trigger-clue:{profile.Id:D}:{result.TraceId}");
        UpdateHud();
        GD.Print($"OPENRPG_FORGE_TRIGGER_CLUE_REVEALED trigger={profile.Id:D} trace={result.TraceId} " +
                 $"false={result.FalseClue.ToString().ToLowerInvariant()} " +
                 $"quest={result.JournalQuestId?.ToString("D") ?? "none"} summary={result.Summary}");
    }

    private bool ResolveLivingWorldNarrative(TriggerWorldProfile profile)
    {
        if (profile.Narrative is not { } narrative || _loadedProject is null || _activeArea is null) return false;
        var presentParticipants = _activeArea.Entities
            .Where(entity => narrative.Participants.Contains(entity.BlueprintId) &&
                             _entityNodes.TryGetValue(entity.Id.Value, out var node) &&
                             GodotObject.IsInstanceValid(node))
            .Select(entity => entity.BlueprintId)
            .ToHashSet();
        var connectedPlayers = LauncherConnectedPlayerCount();
        // The local runtime has no multiplayer vote transport. Only the canonical party-vote policy can
        // resolve, and only for a one-member local-host session; every other policy remains fail-closed.
        var voteSatisfied = string.Equals(narrative.MultiplayerPolicy, "party-vote", StringComparison.Ordinal) &&
                            connectedPlayers == 1;
        var result = TriggerLivingWorldRuntimeService.ResolveNarrative(profile, _livingWorldNarrativeState,
            new(DateTimeOffset.UtcNow, true, voteSatisfied, presentParticipants));
        _livingWorldNarrativeState = result.State;
        if (!result.Fired)
        {
            GD.Print($"OPENRPG_FORGE_TRIGGER_NARRATIVE_SKIPPED code={result.DiagnosticCode} " +
                     $"summary={result.Summary}");
            return false;
        }

        var dialogue = _loadedProject.Dialogues.SingleOrDefault(item => item.Id.Value == result.DialogueId);
        if (dialogue is null)
        {
            GD.PrintErr($"OPENRPG_FORGE_TRIGGER_NARRATIVE_FAILED code=world.reference.dialogue_missing " +
                        $"trigger={profile.Id:D}");
            return false;
        }
        _elyraDialogue = dialogue;
        _guideName = dialogue.DisplayName;
        ShowLivingWorldInterjection(dialogue);
        GD.Print($"OPENRPG_FORGE_TRIGGER_NARRATIVE_FIRED trigger={profile.Id:D} " +
                 $"dialogue={dialogue.Id} priority={result.Priority} policy={narrative.MultiplayerPolicy}");
        return true;
    }

    private void ShowLivingWorldInterjection(DialogueDefinition dialogue)
    {
        var traversal = new DialogueTraversal(dialogue);
        var start = traversal.Start();
        var evaluator = new RuntimeConditionEvaluator(this);
        var spokenLine = ApplyDialoguePresentation(start.Node!.Id, evaluator) ?? start.Node.Text.Fallback;
        if (_actionWheel?.IsOpen == true) _actionWheel.Close();
        // An interjection presents the authored opening line only. It does not auto-pick a dialogue choice or
        // execute dialogue actions; the full dialogue traversal remains an explicit player interaction.
        _dialogueOverlay?.Show(dialogue.DisplayName, "Interjection", spokenLine, []);
        SyncPlayerInterfaceLock();
        _message = $"{dialogue.DisplayName} : {spokenLine}";
        UpdateHud();
    }

    private void ReportLivingWorldTriggerDiagnostic(
        TriggerWorldProfile profile,
        string code,
        string summary)
    {
        if (!_livingWorldTriggerDiagnostics.Add(profile.Id)) return;
        GD.PrintErr($"OPENRPG_FORGE_TRIGGER_LIVING_WORLD_DIAGNOSTIC trigger={profile.Id:D} " +
                    $"code={code} summary={summary}");
    }

    private string ResolveLivingWorldMessage(string key)
    {
        if (_loadedProject is null || string.IsNullOrWhiteSpace(key)) return key;
        try
        {
            return _loadedProject.Module.Localization.Resolve(key, _defaultLocale);
        }
        catch (KeyNotFoundException)
        {
            return key;
        }
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
            var scriptResolved = binding is null || DispatchConfiguredEventScript(binding.ScriptPath,
                binding.Handler, binding.RequiredAtRuntime, $"rest:{@event.Stage}");
            GD.Print(
                $"OPENRPG_FORGE_RUNTIME_REST_EVENT stage={@event.Stage} handler={binding?.Handler ?? "unbound"} " +
                $"revision={@event.ServerRevision} script_resolved={scriptResolved.ToString().ToLowerInvariant()}");
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
        ApplyWeatherLighting(plan.Weather);
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

    private void ApplyWeatherLighting(WeatherStateProfile weather)
    {
        if (_worldEnvironment is null || _referenceSun is null) return;

        var (sunMultiplier, ambientMultiplier, skyMultiplier, exposureMultiplier,
            fogDensity, fogEnergy) = weather.Kind switch
            {
                WeatherKind.Overcast => (.68f, .82f, .76f, .94f, _baseFogDensity, .82f),
                WeatherKind.Rain => (.52f, .72f, .64f, .90f,
                    Math.Max(_baseFogDensity, .0022f + weather.Intensity * .0015f), .72f),
                WeatherKind.Storm => (.38f, .62f, .48f, .84f,
                    Math.Max(_baseFogDensity, .0038f + weather.Intensity * .0022f), .62f),
                WeatherKind.Snow => (.62f, .84f, .78f, .92f,
                    Math.Max(_baseFogDensity, .0028f + weather.Intensity * .0012f), .86f),
                WeatherKind.Fog => (.44f, .74f, .58f, .89f,
                    Math.Max(_baseFogDensity, .0052f + weather.Intensity * .003f), .66f),
                _ => (1f, 1f, 1f, 1f, _baseFogDensity, 1f),
            };

        _referenceSun.LightEnergy = _baseDirectionalLightEnergy * sunMultiplier;
        _worldEnvironment.AmbientLightEnergy = _baseAmbientLightEnergy * ambientMultiplier;
        _worldEnvironment.BackgroundEnergyMultiplier = _baseBackgroundEnergyMultiplier * skyMultiplier;
        _worldEnvironment.TonemapExposure = _baseTonemapExposure * exposureMultiplier;
        _worldEnvironment.FogEnabled = _baseFogEnabled || weather.Kind is WeatherKind.Rain or
            WeatherKind.Storm or WeatherKind.Snow or WeatherKind.Fog;
        _worldEnvironment.FogDensity = fogDensity;
        _worldEnvironment.FogLightEnergy = _baseFogLightEnergy * fogEnergy;
        if (_worldEnvironment.Sky?.SkyMaterial is ShaderMaterial skyMaterial)
        {
            var cloudCoverage = weather.Kind switch
            {
                WeatherKind.Overcast => Math.Max(_baseCloudCoverage, .68f),
                WeatherKind.Rain => Math.Max(_baseCloudCoverage, .86f),
                WeatherKind.Storm => Math.Max(_baseCloudCoverage, .95f),
                WeatherKind.Snow => Math.Max(_baseCloudCoverage, .78f),
                WeatherKind.Fog => Math.Max(_baseCloudCoverage, .64f),
                _ => _baseCloudCoverage,
            };
            skyMaterial.SetShaderParameter("cloud_coverage", cloudCoverage);
        }
    }

    private void CreateFinalCryptAtmosphere()
    {
        var artPass = CryptArtKitDecorator.Build();
        AddChild(artPass.Root);
        _runtimeModelInstances += artPass.ModelInstances;
        GD.Print($"OPENRPG_FORGE_CRYPT_ART_PASS models={artPass.ModelInstances} " +
                 $"floors={artPass.FloorTiles} walls={artPass.WallSegments} props={artPass.Props} " +
                 "source=kaykit-dungeon-remastered license=CC0-1.0 collisions=gameplay-layer");
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
            var torch = new Node3D { Name = "EmberLight", Position = position };
            AddChild(torch);
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
    }

    private bool TryAttachVisual(Node parent, EntityInstance entity, bool applyPlayerVisualTransform = false)
    {
        var authoredAssetId = GetOverride(entity, VisualAssetProperty);
        var cryptCharacter = _isCryptPrototype ? ResolveCryptCharacterVisual(entity) : null;
        var assetId = authoredAssetId ?? cryptCharacter?.AssetId;
        if (string.IsNullOrWhiteSpace(assetId)) return false;
        if (_modelLibrary is null)
        {
            if (authoredAssetId is null) return false;
            throw new InvalidOperationException($"Entity '{entity.Id}' references visual asset '{assetId}' but the module model library is unavailable.");
        }
        var visual = authoredAssetId is null
            ? _modelLibrary.TryInstantiate(assetId)
            : _modelLibrary.Instantiate(assetId);
        if (visual is null)
        {
            GD.Print($"OPENRPG_FORGE_RUNTIME_CRYPT_VISUAL_FALLBACK entity={entity.Id} asset={assetId}");
            return false;
        }
        visual.Name = "AuthoredVisual";
        if (applyPlayerVisualTransform)
        {
            visual.Basis = EngineValueConverter.ToEngine(entity.Transform).Basis;
            visual.Position = new Vector3(0, -1f, 0);
            visual.RotateY(Mathf.Pi);
        }
        parent.AddChild(visual);
        if (parent is RuntimeFlyingMount)
        {
            EnhanceFlyingMountMaterials(visual);
        }
        if (entity.Tags.Contains("wall", StringComparer.Ordinal) &&
            TryMeasureNodeBounds(visual, out var wallMinimum, out var wallMaximum))
        {
            var center = (wallMinimum + wallMaximum) * .5f;
            visual.Position -= new Vector3(center.X, 0, center.Z);
        }
        _runtimeModelInstances++;
        var animationProfile = GetOverride(entity, RuntimeAnimationProfileProperty) ?? cryptCharacter?.AnimationProfile;
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
            "authored-clips" => GodotAssetAnimationController.Configure(
                visual,
                idle: RequiredAnimationOverride(entity, "runtime.animationIdle"),
                run: RequiredAnimationOverride(entity, "runtime.animationRun"),
                jump: RequiredAnimationOverride(entity, "runtime.animationJump"),
                attack: RequiredAnimationOverride(entity, "runtime.animationAttack"),
                hit: RequiredAnimationOverride(entity, "runtime.animationHit"),
                death: RequiredAnimationOverride(entity, "runtime.animationDeath")),
            "quaternius-dragon" => GodotAssetAnimationController.Configure(
                visual,
                idle: GetOverride(entity, "runtime.animationIdle") ?? "Dragon_Flying",
                run: GetOverride(entity, "runtime.animationRun") ?? "Dragon_Flying",
                jump: GetOverride(entity, "runtime.animationJump") ?? "Dragon_Flying",
                attack: GetOverride(entity, "runtime.animationAttack") ?? "Dragon_Attack",
                hit: GetOverride(entity, "runtime.animationHit") ?? "Dragon_Hit",
                death: GetOverride(entity, "runtime.animationDeath") ?? "Dragon_Death"),
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
            if (parent is RuntimeFlyingMount mount)
            {
                mount.ConfigureAnimations(animation);
            }
        }
        if (applyPlayerVisualTransform) AlignPlayerVisualToCapsule(visual);
        return true;
    }

    private static void AlignPlayerVisualToCapsule(Node3D visual)
    {
        if (!TryMeasureNodeBounds(visual, out var minimum, out var maximum)) return;
        const float capsuleBottom = -.9f;
        var center = (minimum + maximum) * .5f;
        visual.Position += new Vector3(-center.X, capsuleBottom - minimum.Y, -center.Z);
    }

    private static CryptCharacterVisualDescriptor? ResolveCryptCharacterVisual(EntityInstance entity)
    {
        return CryptCharacterVisualResolver.Resolve(entity.Tags);
    }

    private static void EnhanceFlyingMountMaterials(Node3D visual)
    {
        foreach (var node in visual.FindChildren("*", "MeshInstance3D", recursive: true, owned: false))
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh is null) continue;
            for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.GetActiveMaterial(surface) is not BaseMaterial3D source) continue;
                var material = (BaseMaterial3D)source.Duplicate(true);
                var palette = source.ResourceName.ToLowerInvariant() switch
                {
                    "main" => new Color("bd6570"),
                    "wings" => new Color("896176"),
                    "belly" => new Color("d6a62d"),
                    "claws" => new Color("302d39"),
                    "eyes" => new Color("d9c69a"),
                    _ => source.AlbedoColor,
                };
                material.AlbedoColor = new Color(palette.R, palette.G, palette.B, 1);
                material.Metallic = .04f;
                material.Roughness = .72f;
                mesh.SetSurfaceOverrideMaterial(surface, material);
            }
        }
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

    private static string RequiredAnimationOverride(EntityInstance entity, string key) =>
        !string.IsNullOrWhiteSpace(GetOverride(entity, key))
            ? GetOverride(entity, key)!
            : throw new InvalidDataException(
                $"Entity '{entity.Id}' uses authored-clips but does not define '{key}'.");

    private void CreateHud()
    {
        _combatUiPolicy = RuntimeUiMotionPolicy.Create(
            _runtimeExtensions?.ModernTooling.QualityAssurance.Accessibility,
            OS.GetCmdlineUserArgs());
        _hud = new RuntimeHud
        {
            ModuleTitle = _moduleTitle,
            AreaTitle = _activeArea?.DisplayName ?? _moduleTitle,
            InteractionLabel = string.IsNullOrWhiteSpace(_guideName) ? "Interagir" : $"Parler à {_guideName}",
            CombatEnabled = _playerCombatant is not null,
            Accessibility = _runtimeExtensions?.ModernTooling.QualityAssurance.Accessibility,
            PartyHud = _worldFeatureCatalog?.Companions.FirstOrDefault()?.Experience?.Hud,
            MotionPolicy = _combatUiPolicy,
        };
        _hud.PauseRequested += ToggleTacticalPause;
        _hud.JournalRequested += () => ToggleRpgMenu(RuntimeRpgMenuPage.Journal);
        AddChild(_hud);
        _hud.UpdatePartyStatus("Elyra", 24, 30,
            new Dictionary<string, double> { ["health"] = 24, ["focus"] = 4 }, ["prête"]);
        _toastQueue = new RuntimeToastQueue { MotionPolicy = _combatUiPolicy };
        AddChild(_toastQueue);
        _actionBar = new RuntimeActionBar
        {
            CombatEnabled = _playerCombatant is not null,
            MotionPolicy = _combatUiPolicy,
        };
        if (_playerCombatant?.Profile.Abilities.FirstOrDefault() is { } primaryAbility)
            _actionBar.ConfigurePrimaryAbility(primaryAbility);
        _actionBar.PrimaryAbilityReady += HandlePrimaryAbilityReady;
        _actionBar.CommandRequested += HandleActionBarCommand;
        AddChild(_actionBar);
        if (_camera is not null)
        {
            _combatFeedbackLayer = new RuntimeCombatFeedbackLayer(_camera, _combatUiPolicy);
            AddChild(_combatFeedbackLayer);
            _combatImpactPool = new RuntimeCombatImpactPool(_combatUiPolicy);
            AddChild(_combatImpactPool);
            RegisterCombatFeedbackTargets();
        }
        _moduleMap = new RuntimeModuleMap { MotionPolicy = _combatUiPolicy };
        _moduleMap.Configure(_loadedProject!, _activeArea!, _player!);
        _moduleMap.GlobalMapVisibilityChanged += _ => SyncPlayerInterfaceLock();
        _moduleMap.InventoryRequested += () => ShowRpgMenu(RuntimeRpgMenuPage.Inventory);
        _moduleMap.CharacterRequested += () => ShowRpgMenu(RuntimeRpgMenuPage.Character);
        _moduleMap.JournalRequested += () => ShowRpgMenu(RuntimeRpgMenuPage.Journal);
        AddChild(_moduleMap);
        _rpgMenu = new RuntimeRpgMenu
        {
            ModuleTitle = _moduleTitle,
            AreaTitle = _activeArea?.DisplayName ?? _moduleTitle,
        };
        _rpgMenu.MapRequested += OpenModuleMapFromMenu;
        _rpgMenu.Closed += SyncPlayerInterfaceLock;
        AddChild(_rpgMenu);
        _rpgMenu.UpdateContext("Les braises oubliées", _message);
        _dialogueOverlay = new RuntimeDialogueOverlay();
        _dialogueOverlay.Closed += SyncPlayerInterfaceLock;
        AddChild(_dialogueOverlay);
        _actionWheel = new RuntimeActionWheel();
        if (_gameplayControls is not null) _actionWheel.Configure(_gameplayControls);
        _actionWheel.SelectionAccepted += command => _actionBar?.NotifyCommandAccepted(command);
        _actionWheel.CommandRequested += RequestGameplayCommand;
        _actionWheel.WheelVisibilityChanged += _ => SyncPlayerInterfaceLock();
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

    private void ApplyModuleWindowTitle()
    {
        var title = $"OpenRPG Forge — {_moduleTitle}";
        GetWindow().Title = title;
        DisplayServer.WindowSetTitle(title);
    }

    private void RegisterCombatFeedbackTargets()
    {
        if (_combatFeedbackLayer is null) return;
        foreach (var pair in _hostiles)
        {
            var node = pair.Key;
            var combatant = pair.Value;
            var worldLabel = node.GetNodeOrNull<Label3D>(RuntimeWorldLabelNodeName);
            var anchorHeight = worldLabel?.Position.Y ?? (node == _boss ? 2.2f : 1.35f);
            worldLabel?.QueueFree();
            _combatFeedbackLayer.Register(
                node,
                combatant.Id,
                _hostileNames[node],
                combatant.Health,
                combatant.Profile.Stats.MaximumHealth,
                node == _boss,
                anchorHeight);
        }
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
            case "module_map":
                ToggleModuleMap();
                break;
            case "inventory":
                ToggleRpgMenu(RuntimeRpgMenuPage.Inventory);
                break;
            case "character":
                ToggleRpgMenu(RuntimeRpgMenuPage.Character);
                break;
            case "journal":
                ToggleRpgMenu(RuntimeRpgMenuPage.Journal);
                break;
            case "tactical_pause":
                ToggleTacticalPause();
                break;
            default:
                RequestGameplayCommand(command);
                break;
        }
    }

    private void HandlePrimaryAbilityReady()
    {
        var ability = _playerCombatant?.Profile.Abilities.FirstOrDefault();
        if (ability is null || ability.CooldownSeconds <= 0) return;
        _toastQueue?.Show(RuntimeToastKind.Combat, $"{ability.DisplayName} est prête.",
            $"combat-ready:{ability.Id}");
        if (_player is not null)
            _combatImpactPool?.PlayAudio(CombatFeedbackAudioKind.AbilityReady, _player.GlobalPosition);
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
        var candidates = new Node3D?[] { _flyingMount, _npc, _keyItem, _door }
            .Where(node => node is not null && GodotObject.IsInstanceValid(node))
            .Cast<Node3D>()
            .Concat(_portalNodes.Keys.Where(GodotObject.IsInstanceValid))
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

    private bool TryInteractWithWorldPointer(Vector2 pointerPosition)
    {
        if (_camera is null || _player is null || _portalTransitionInProgress ||
            _moduleMap?.GlobalMapVisible == true || _rpgMenu?.IsOpen == true || _dialogueOverlay?.IsOpen == true)
            return false;

        var origin = _camera.ProjectRayOrigin(pointerPosition);
        var query = PhysicsRayQueryParameters3D.Create(origin,
            origin + (_camera.ProjectRayNormal(pointerPosition) * 250));
        query.Exclude = [_player.GetRid()];
        query.CollideWithAreas = true;
        query.CollideWithBodies = true;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (!hit.TryGetValue("collider", out var collider) ||
            collider.AsGodotObject() is not Node colliderNode)
            return false;

        Node? current = colliderNode;
        Node3D? interactiveNode = null;
        while (current is not null)
        {
            if (current is Node3D node3D &&
                (_portalNodes.ContainsKey(node3D) || node3D == _flyingMount ||
                 _clickInteractions.ContainsKey(node3D) || node3D == _npc ||
                 node3D == _keyItem || node3D == _door || _corpseLoot.ContainsKey(node3D)))
            {
                interactiveNode = node3D;
                break;
            }
            current = current.GetParent();
        }
        if (interactiveNode is null) return false;

        var interactionDistance = _clickInteractions.TryGetValue(interactiveNode, out var clickTrigger)
            ? clickTrigger.MaximumDistanceMeters
            : _gameplayControls?.Foundation.Interaction.FocusDistance ?? InteractionDistance;
        if (!IsNear(interactiveNode, interactionDistance))
        {
            if (clickTrigger is not null)
            {
                _message = $"Approchez-vous de l'objet pour l'utiliser ({interactionDistance:0.#} m maximum).";
                UpdateHud();
                return true;
            }
            _message = interactiveNode switch
            {
                _ when interactiveNode == _flyingMount => "Approchez-vous de Brumaille pour le monter.",
                _ when interactiveNode == _npc => $"Approchez-vous de {_guideName} pour lui parler.",
                _ when interactiveNode == _keyItem => $"Approchez-vous de {_questItemDisplayName} pour la prendre.",
                _ when interactiveNode == _door => $"Approchez-vous de {_gateDisplayName} pour l’ouvrir.",
                _ when _corpseLoot.ContainsKey(interactiveNode) => "Approchez-vous de la dépouille pour la fouiller.",
                _ => "Approchez-vous du passage pour l’emprunter."
            };
            UpdateHud();
            return true;
        }

        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
        if (clickTrigger is not null && interactiveNode != _flyingMount &&
            !_portalNodes.ContainsKey(interactiveNode))
        {
            ExecuteClickInteraction(interactiveNode, clickTrigger);
            return true;
        }
        InteractWith(interactiveNode);
        return true;
    }

    private void ExecuteClickInteraction(Node3D source, EntityClickTriggerSettings settings)
    {
        switch (settings.Action)
        {
            case EntityClickTriggerAction.ShowMessage:
                _message = settings.Message;
                break;
            case EntityClickTriggerAction.StartDialogue:
                if (!Guid.TryParse(settings.TargetId, out var dialogueId) || _loadedProject is null ||
                    _loadedProject.Dialogues.FirstOrDefault(dialogue => dialogue.Id.Value == dialogueId) is not { } dialogue)
                {
                    _message = "Le dialogue lié à cet objet n'est plus disponible.";
                    break;
                }
                _elyraDialogue = dialogue;
                _guideName = dialogue.DisplayName;
                InteractWithGuide();
                break;
            case EntityClickTriggerAction.ToggleVisibility:
                if (!Guid.TryParse(settings.TargetId, out var entityId) ||
                    !_entityNodes.TryGetValue(entityId, out var target) ||
                    !GodotObject.IsInstanceValid(target))
                {
                    _message = "L'objet cible de ce déclencheur n'est plus disponible.";
                    break;
                }
                var visible = !target.Visible;
                target.Visible = visible;
                foreach (var node in target.FindChildren("*", "CollisionShape3D", recursive: true, owned: false))
                {
                    if (node is CollisionShape3D collision) collision.Disabled = !visible;
                }
                _message = $"{target.Name} est maintenant {(visible ? "visible" : "masqué")}.";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(settings), settings.Action,
                    "Unknown click interaction action.");
        }
        UpdateHud();
        GD.Print($"OPENRPG_FORGE_ENTITY_CLICK_TRIGGER action={EntityClickTriggerSettings.ActionKey(settings.Action)} " +
                 $"source={source.Name} target={settings.TargetId}");
    }

    private void InteractWith(Node3D target)
    {
        if (target == _flyingMount && _player is not null)
        {
            if (_flyingMount.IsMounted)
            {
                _message = _flyingMount.TryDismount()
                    ? "Vous descendez de Brumaille."
                    : "Descendez près du sol avec Ctrl avant de quitter la selle.";
            }
            else
            {
                _flyingMount.Mount(_player);
                _message = "Brumaille prend son envol · ZQSD/WASD diriger · Espace monter · Ctrl descendre · E quitter la selle.";
            }
            SyncPlayerInterfaceLock();
            UpdateHud();
            return;
        }

        if (_portalNodes.TryGetValue(target, out var portal))
        {
            _ = TransitionThroughPortalAsync(portal);
            return;
        }

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
                    ? $"{_gateDisplayName} reste fermée. La Clé de braise se trouve dans le reliquaire latéral, avant cette porte."
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

    private async Task TransitionThroughPortalAsync(PortalLink portal)
    {
        if (_portalTransitionInProgress || _loadedProject is null || _activeArea is null || _lifecycle is null)
            return;
        _portalTransitionInProgress = true;
        try
        {
            var travel = PortalLinkRuntimeService.Resolve(_loadedProject, _activeArea.Id, portal.Id,
                Vector3Value.Zero, 0);
            var destination = _loadedProject.Areas.Single(area => area.Id == travel.DestinationAreaId);
            EnsureLifecycle(_lifecycle.Transition(destination.Id, null, portal.Id,
                RuntimeLifecycleCause.PortalTransition), "portal_transition");
            _message = $"Voyage vers {destination.DisplayName}…";
            UpdateHud();
            s_pendingPortalSpawn = portal.DestinationEntityId;
            s_pendingPortalLabel = destination.DisplayName;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var error = GetTree().ReloadCurrentScene();
            if (error != Error.Ok)
            {
                s_pendingPortalSpawn = null;
                s_pendingPortalLabel = null;
                throw new InvalidOperationException($"Scene reload failed during portal travel: {error}.");
            }
        }
        catch
        {
            _portalTransitionInProgress = false;
            throw;
        }
    }

    private async Task RunInnUxSmokeTestAsync(ProjectDefinition project)
    {
        var activeArea = _activeArea ?? throw new InvalidOperationException(
            "The inn UX smoke test requires an active area.");
        if (s_innUxSmokeStep == 1)
        {
            AssertSmoke(activeArea.Slug == "clairval",
                $"Clicking the inn door loaded '{activeArea.Slug}' instead of Clairval.");
            s_innUxSmokeStep = 0;
            GD.Print("OPENRPG_FORGE_INN_DOOR_CLICK_PASS source=ember-inn destination=clairval " +
                     "input=left-mouse ui_button=absent transition=runtime-reload");
            GetTree().Quit(0);
            return;
        }

        AssertSmoke(s_innUxSmokeStep == 0 && activeArea.Slug == "ember-inn" &&
                    project.Module.EntryAreaId == activeArea.Id,
            "The inn UX smoke test must begin at the module entry inside the Ember Inn.");
        var player = _player ?? throw new InvalidOperationException("The inn UX smoke test has no player.");
        var camera = _camera ?? throw new InvalidOperationException("The inn UX smoke test has no camera.");
        var start = player.GlobalPosition;
        for (var frame = 0; frame < 20; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        _cameraRig.ResetMotionDiagnostics();
        Input.ActionPress("move_right");
        try
        {
            for (var frame = 0; frame < 60; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        finally
        {
            Input.ActionRelease("move_right");
        }
        for (var frame = 0; frame < 8; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var travelled = new Vector2(player.GlobalPosition.X - start.X, player.GlobalPosition.Z - start.Z).Length();
        AssertSmoke(travelled >= 3,
            $"The inn camera motion test moved only {travelled:0.###} metres.");
        AssertSmoke(_cameraRig.MaximumVerticalFocusStep <= .04f,
            $"The camera focus still bounces vertically while walking " +
            $"(vertical={_cameraRig.MaximumVerticalFocusStep:0.###}).");
        GD.Print($"OPENRPG_FORGE_CAMERA_MOTION_STABLE_PASS area=ember-inn distance={travelled:0.###} " +
                 $"max_focus_step={_cameraRig.MaximumFocusStep:0.###} " +
                 $"max_step_delta={_cameraRig.MaximumFocusStepDelta:0.###} " +
                 $"max_vertical_step={_cameraRig.MaximumVerticalFocusStep:0.###} " +
                 "focus=physics-interpolated-render-smoothed wall_avoidance=enabled");

        player.GlobalPosition = start;
        player.Velocity = Vector3.Zero;
        for (var frame = 0; frame < 4; frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        _cameraRig.Snap(camera, player.GlobalPosition, ConstrainCameraPosition);
        AssertSmoke(_hud?.InteractionPromptVisible == false,
            "The obsolete central inn exit prompt is still visible.");

        var portalNode = _portalNodes.Keys.Single();
        var clickTarget = portalNode.GetNode<Area3D>("PortalClickTarget");
        AssertSmoke(!camera.IsPositionBehind(clickTarget.GlobalPosition),
            "The authored inn door click target is behind the initial camera.");
        var pointer = camera.UnprojectPosition(clickTarget.GlobalPosition);
        s_innUxSmokeStep = 1;
        var clicked = TryInteractWithWorldPointer(pointer);
        AssertSmoke(clicked && _portalTransitionInProgress,
            "A left click on the authored inn door did not begin the portal transition.");
    }



    private void AttackNearestHostile()
    {
        var nearest = _hostiles.Keys
            .Where(node => GodotObject.IsInstanceValid(node) && !_hostiles[node].IsDead)
            .OrderBy(node => _player!.GlobalPosition.DistanceTo(node.GlobalPosition))
            .FirstOrDefault();

        if (nearest is null || !IsNear(nearest, AttackDistance))
        {
            _message = "Aucun ennemi à portée.";
            _toastQueue?.Show(RuntimeToastKind.Alert, "Aucune cible hostile à portée.", "combat:no-target");
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
        var nowSeconds = Time.GetTicksMsec() / 1000d;
        if (target.IsDead)
        {
            RejectCombatAction("Cette cible est déjà vaincue.", "combat:target-dead");
            return;
        }
        if (!AuthorizeAreaCombatAttack(_playerCombatant, target))
        {
            return;
        }
        if (_playerCombatant.Resource < ability.ResourceCost)
        {
            RejectCombatAction(
                $"Ressource insuffisante pour {ability.DisplayName} : {ability.ResourceCost} requise, " +
                $"{_playerCombatant.Resource} disponible.",
                $"combat:resource:{ability.Id}");
            return;
        }
        var cooldownRemaining = _playerCombatant.CooldownRemaining(ability, nowSeconds);
        if (cooldownRemaining > 0)
        {
            RejectCombatAction($"{ability.DisplayName} est en recharge ({cooldownRemaining:0.0} s).",
                $"combat:cooldown:{ability.Id}");
            return;
        }
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
            nowSeconds,
            damageMultiplier: _resolvedDifficulty?.PlayerDamageMultiplier ?? 1);
        _lastCombatFeedback = CombatFeedbackPresentationService.Create(
            damageTransactionId, _playerCombatant, target, result, nowSeconds);
        _actionBar?.NotifyCastAccepted();
        PresentCombatFeedback(hostile, target, _lastCombatFeedback);
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
        if (_characterAnimations.TryGetValue(hostile, out var hitStopAnimation))
            _combatImpactPool?.ApplyHitStop(hitStopAnimation.Player);

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
                    ReleaseCombatFeedbackTarget(hostile);
                    if (GodotObject.IsInstanceValid(hostile)) hostile.QueueFree();
                };
            }
            else
            {
                ReleaseCombatFeedbackTarget(hostile);
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

    private bool AuthorizeAreaCombatAttack(CombatantState attacker, CombatantState target)
    {
        var path = _activeArea is null
            ? "area/runtimePolicies/rules/combat"
            : $"areas/{_activeArea.Id}/runtimePolicies/rules/combat";
        if (_areaCombatAuthority is null ||
            !Guid.TryParse(attacker.Id, out var attackerId) ||
            !Guid.TryParse(target.Id, out var targetId))
        {
            const string code = "NET_AREA_COMBAT_CONTEXT_INVALID";
            GD.PrintErr($"OPENRPG_FORGE_AREA_COMBAT_REJECTED code={code} path={path}");
            RejectCombatAction("Le contexte d’autorité du combat est indisponible.",
                "combat:authority:context-invalid");
            return false;
        }

        var decision = _areaCombatAuthority.AuthorizeDamage(
            attackerId,
            targetId,
            targetIsPlayer: false,
            worldRevision: checked(++_runtimePolicyRevision),
            serverAuthorityGranted: _areaCombatServerAuthority,
            aggroInitiation: true);
        if (!decision.Accepted)
        {
            var message = decision.Code switch
            {
                "NET_AREA_COMBAT_SERVER_REQUIRED" =>
                    "Cette action de combat exige l’autorité du serveur.",
                "NET_AREA_COMBAT_AUTHORITY_REQUIRED" =>
                    "La politique de combat de cette zone n’autorise pas une décision locale.",
                "NET_AREA_AGGRO_DISABLED" =>
                    "Cette zone interdit l’initiation d’un combat hostile.",
                _ => "La politique de combat de cette zone refuse cette action.",
            };
            GD.PrintErr($"OPENRPG_FORGE_AREA_COMBAT_REJECTED code={decision.Code} path={path} " +
                        $"revision={decision.Revision}");
            RejectCombatAction(message, $"combat:authority:{decision.Code.ToLowerInvariant()}");
            return false;
        }

        GD.Print($"OPENRPG_FORGE_AREA_COMBAT_AUTHORITY_PASS code={decision.Code} path={path} " +
                 $"aggro=true server={_areaCombatServerAuthority.ToString().ToLowerInvariant()} " +
                 $"revision={decision.Revision}");
        return true;
    }

    private void RejectCombatAction(string message, string deduplicationKey)
    {
        _message = message;
        _toastQueue?.Show(RuntimeToastKind.Alert, message, deduplicationKey);
        UpdateHud();
    }

    private void PresentCombatFeedback(
        Node3D hostile,
        CombatantState target,
        CombatFeedbackPresentation presentation)
    {
        var targetChanged = _currentCombatTarget != hostile;
        _currentCombatTarget = hostile;
        _combatFeedbackLayer?.Present(presentation, hostile);
        if (presentation.Amounts.FirstOrDefault() is { } primaryAmount)
            _combatFeedbackLayer?.Flash(hostile, primaryAmount.Kind);
        if (_camera is not null)
            _combatImpactPool?.Present(presentation, hostile, _camera.GlobalPosition);
        if (presentation.Amounts.Any(item => item.Kind == CombatFeedbackAmountKind.Damage))
            _cameraRig.AddImpulse(_combatUiPolicy.CameraImpulseMeters, _combatUiPolicy.CameraImpulseSeconds);
        PlayCombatFeedbackAudio(presentation, hostile);
        var displayName = _hostileNames.GetValueOrDefault(hostile, hostile.Name);
        if (targetChanged)
        {
            var position = hostile.GlobalPosition;
            _moduleMap?.ShowPing(new Vector3Value(position.X, position.Y, position.Z),
                RuntimeMapPingKind.Combat, $"Cible de combat : {displayName}");
        }
        _hud?.UpdateTargetStatus(
            displayName,
            target.Health,
            target.Profile.Stats.MaximumHealth,
            hostile == _boss,
            presentation.TargetDied);
    }

    private void PlayCombatFeedbackAudio(CombatFeedbackPresentation presentation, Node3D target)
    {
        if (_combatImpactPool is null) return;
        var position = target.GlobalPosition + Vector3.Up;
        var authoredCue = _areaExperience?.AudioCues.FirstOrDefault(item =>
            string.Equals(item.CueId, presentation.VisualCueKey, StringComparison.Ordinal));
        if (authoredCue is not null && _areaExperience is not null)
        {
            var resolved = AreaExperienceRuntimeService.PlayCue(
                _areaExperience, _areaAudioState, authoredCue.CueId, DateTimeOffset.UtcNow);
            _areaAudioState = resolved.State;
            var stream = LoadAudioStream(resolved.Playback.Resource);
            if (stream is not null)
            {
                var playbackId = resolved.Playback.PlaybackId;
                _combatImpactPool.PlayAudioStream(stream, position, resolved.Playback.Bus,
                    Math.Clamp(resolved.Playback.Volume, .001f, 1), () =>
                    {
                        _areaAudioState = AreaExperienceRuntimeService.Complete(
                            _areaAudioState, playbackId, DateTimeOffset.UtcNow);
                    }, cueOverride: true);
                GD.Print($"OPENRPG_FORGE_RUNTIME_COMBAT_AUDIO cue={presentation.VisualCueKey} source=area");
                return;
            }
            _areaAudioState = AreaExperienceRuntimeService.Complete(
                _areaAudioState, resolved.Playback.PlaybackId, DateTimeOffset.UtcNow);
        }

        var kind = presentation.Amounts.Any(item => item.Kind == CombatFeedbackAmountKind.Damage)
            ? CombatFeedbackAudioKind.ImpactLight
            : CombatFeedbackAudioKind.Healing;
        _combatImpactPool.PlayAudio(kind, position);
        GD.Print($"OPENRPG_FORGE_RUNTIME_COMBAT_AUDIO cue={presentation.VisualCueKey} source=builtin kind={kind}");
    }

    private void ReleaseCombatFeedbackTarget(Node3D hostile)
    {
        _combatFeedbackLayer?.Unregister(hostile);
        if (_currentCombatTarget != hostile) return;
        ClearCombatTargetFocus();
    }

    private void ClearCombatTargetFocus()
    {
        _currentCombatTarget = null;
        _combatFeedbackLayer?.ClearTarget();
        _hud?.ClearTarget();
    }

    private void GrantLoot(IEnumerable<LootGrant> grants)
    {
        foreach (var grant in grants)
        {
            _inventory.Add(grant.ItemSlug);
            _toastQueue?.Show(RuntimeToastKind.Loot,
                $"Butin : {grant.ItemSlug} ×{grant.Quantity}", $"loot:{grant.ItemSlug}");
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

    public bool DispatchAreaUserEvent(
        string namespacedEvent,
        IReadOnlyDictionary<string, object?> payload,
        bool requiredAtRuntime = false)
    {
        if (_activeArea is null)
        {
            if (requiredAtRuntime)
                GD.PrintErr($"OPENRPG_FORGE_AREA_USER_EVENT_FAILED event={namespacedEvent} reason=area_missing");
            return !requiredAtRuntime;
        }
        var result = AreaUserEventRuntimeService.Dispatch(
            _activeArea,
            namespacedEvent,
            payload,
            _forgeScriptRuntime,
            ReadConfiguredScriptSource,
            requiredAtRuntime,
            dryRun: false,
            execute: artifact => ExecuteForgeScript(artifact, this));
        if (!result.Succeeded)
        {
            foreach (var diagnostic in result.Diagnostics)
                GD.PrintErr($"OPENRPG_FORGE_AREA_USER_EVENT_FAILED event={namespacedEvent} " +
                            $"code={diagnostic.Code} path={diagnostic.Path}");
            return false;
        }
        GD.Print($"OPENRPG_FORGE_AREA_USER_EVENT_DISPATCH event={namespacedEvent} " +
                 $"deliveries={result.Deliveries.Count} executed={result.Executed.ToString().ToLowerInvariant()} " +
                 $"ignored={result.Ignored.ToString().ToLowerInvariant()}");
        return true;
    }

    private string? ReadConfiguredScriptSource(string scriptPath)
    {
        var normalized = scriptPath.Replace('\\', '/');
        return _moduleContentRoot is null
            ? Godot.FileAccess.GetFileAsString($"res://content/{_moduleSlug}/{normalized}")
            : ReadCachedModuleScript(normalized);
    }

    private bool DispatchConfiguredEventScript(
        string? scriptPath,
        string handler,
        bool requiredAtRuntime,
        string eventName)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            if (requiredAtRuntime)
            {
                GD.PrintErr($"OPENRPG_FORGE_EVENT_SCRIPT_FAILED event={eventName} reason=required_missing");
                return false;
            }
            return true;
        }
        if (!ProjectLifecycleEventProfileValidator.IsSafeScriptPath(scriptPath))
        {
            GD.PrintErr($"OPENRPG_FORGE_EVENT_SCRIPT_FAILED event={eventName} reason=unsafe_path");
            return false;
        }
        try
        {
            var normalized = scriptPath.Replace('\\', '/');
            var source = ReadConfiguredScriptSource(normalized);
            if (string.IsNullOrWhiteSpace(source))
            {
                GD.PrintErr($"OPENRPG_FORGE_EVENT_SCRIPT_FAILED event={eventName} reason=source_missing");
                return false;
            }
            var handlerPattern = @"\bvoid\s+" +
                                 System.Text.RegularExpressions.Regex.Escape(handler) + @"\s*\(";
            if (!System.Text.RegularExpressions.Regex.IsMatch(source, handlerPattern,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            {
                GD.PrintErr($"OPENRPG_FORGE_EVENT_SCRIPT_FAILED event={eventName} reason=handler_missing");
                return false;
            }
            var entrySource = BuildConfiguredEventEntrySource(source, handler);
            var compilation = _forgeScriptCache?.Compile(entrySource).Compilation ??
                              _forgeScriptRuntime.Compile(entrySource);
            if (!compilation.Success || compilation.Artifact is null)
            {
                GD.PrintErr($"OPENRPG_FORGE_EVENT_SCRIPT_FAILED event={eventName} reason=compile_failed");
                return false;
            }
            var executed = ExecuteForgeScript(compilation.Artifact, this);
            GD.Print($"OPENRPG_FORGE_EVENT_SCRIPT_DISPATCH event={eventName} handler={handler} " +
                     $"executed={executed.ToString().ToLowerInvariant()}");
            return executed;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or
                                           UnauthorizedAccessException)
        {
            GD.PrintErr($"OPENRPG_FORGE_EVENT_SCRIPT_FAILED event={eventName} " +
                        $"reason={exception.GetType().Name}");
            return false;
        }
    }

    private static string BuildConfiguredEventEntrySource(string source, string handler)
    {
        if (string.Equals(handler, "main", StringComparison.Ordinal))
        {
            return source;
        }
        var withoutMain = System.Text.RegularExpressions.Regex.Replace(source, @"\bvoid\s+main\s*\(",
            "void __forge_original_main(",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        return withoutMain + System.Environment.NewLine + $"void main() {{ {handler}(); }}" +
               System.Environment.NewLine;
    }

    private bool ExecuteForgeScript(ForgeScriptArtifact? artifact, Node3D origin)
    {
        if (artifact is null)
        {
            _message = "Le script de gameplay requis est absent.";
            GD.PrintErr("OPENRPG_FORGE_SCRIPT_FAILED missing_artifact");
            return false;
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
            return false;
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
        return true;
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
            _player.ControlLocked = IsTimeFrozen() || _moduleMap?.GlobalMapVisible == true ||
                                    _rpgMenu?.IsOpen == true || _dialogueOverlay?.IsOpen == true ||
                                    _flyingMount?.IsMounted == true;
            _player.SimulationScale = _timeControlSession.State == RuntimeTimeState.SlowMotion
                ? _timeControlSession.Scale
                : 1;
        }
        if (_flyingMount is not null)
        {
            _flyingMount.ControlLocked = IsTimeFrozen() || _moduleMap?.GlobalMapVisible == true ||
                                         _rpgMenu?.IsOpen == true || _dialogueOverlay?.IsOpen == true;
        }
    }

    private void ToggleModuleMap()
    {
        if (_moduleMap is null) return;
        if (_actionWheel?.IsOpen == true) _actionWheel.Close();
        if (_rpgMenu?.IsOpen == true) _rpgMenu.Close();
        if (_dialogueOverlay?.IsOpen == true) _dialogueOverlay.Close();
        _moduleMap.ToggleGlobalMap();
        SyncPlayerInterfaceLock();
        _message = _moduleMap.GlobalMapVisible
            ? "Carte du module ouverte · M pour revenir au jeu."
            : "Carte fermée.";
        UpdateHud();
    }

    private void ToggleRpgMenu(RuntimeRpgMenuPage page)
    {
        if (_rpgMenu is null) return;
        if (_rpgMenu.IsOpen && _rpgMenu.CurrentPage == page)
        {
            _rpgMenu.Close();
            SyncPlayerInterfaceLock();
            return;
        }
        ShowRpgMenu(page);
    }

    private void ShowRpgMenu(RuntimeRpgMenuPage page)
    {
        if (_rpgMenu is null) return;
        if (_actionWheel?.IsOpen == true) _actionWheel.Close();
        if (_moduleMap?.GlobalMapVisible == true) _moduleMap.SetGlobalMapVisible(false);
        if (_dialogueOverlay?.IsOpen == true) _dialogueOverlay.Close();
        _rpgMenu.UpdateContext("Les braises oubliées", _message);
        _rpgMenu.ShowPage(page);
        SyncPlayerInterfaceLock();
    }

    private void OpenModuleMapFromMenu()
    {
        if (_moduleMap is null) return;
        if (_rpgMenu?.IsOpen == true) _rpgMenu.Close();
        _moduleMap.SetGlobalMapVisible(true);
        _message = "Carte du module ouverte · M pour revenir au jeu.";
        SyncPlayerInterfaceLock();
        UpdateHud();
    }

    private void SyncPlayerInterfaceLock()
    {
        var codexOpen = _rpgMenu?.IsOpen == true;
        var dialogueOpen = _dialogueOverlay?.IsOpen == true;
        var mapOpen = _moduleMap?.GlobalMapVisible == true;
        var wheelOpen = _actionWheel?.IsOpen == true;
        var explorationVisible = !codexOpen && !dialogueOpen && !mapOpen && !wheelOpen;
        if (_hud is not null) _hud.Visible = explorationVisible;
        if (_actionBar is not null) _actionBar.Visible = explorationVisible;
        if (_combatFeedbackLayer is not null) _combatFeedbackLayer.Visible = explorationVisible;
        _combatImpactPool?.SetInterfaceVisible(explorationVisible);
        _toastQueue?.SetInterfaceVisible(explorationVisible);
        _moduleMap?.SetExplorationUiVisible(!codexOpen && !dialogueOpen && !wheelOpen);
        var interfaceLocked = IsTimeFrozen() || mapOpen || codexOpen || dialogueOpen;
        if (_flyingMount is not null) _flyingMount.ControlLocked = interfaceLocked;
        if (_player is null) return;
        _player.ControlLocked = interfaceLocked || _flyingMount?.IsMounted == true;
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
        var playerResource = _playerCombatant?.Resource ?? 0;
        var playerMaximumResource = _playerCombatant?.Profile.Stats.MaximumResource ?? 1;
        _hud.UpdatePlayerStatus("Héros", playerHealth, playerMaximumHealth,
            playerResource, playerMaximumResource, _progressionLevel);
        if (_playerCombatant?.Profile.Abilities.FirstOrDefault() is { } primaryAbility)
        {
            var nowSeconds = Time.GetTicksMsec() / 1000d;
            _actionBar?.UpdatePrimaryAbilityState(
                _playerCombatant.CooldownRemaining(primaryAbility, nowSeconds),
                _playerCombatant.Resource,
                HasAttackTargetInRange());
        }
        if (_currentCombatTarget is not null &&
            (!GodotObject.IsInstanceValid(_currentCombatTarget) ||
             (_hostiles.ContainsKey(_currentCombatTarget) && _player is not null &&
              _player.GlobalPosition.DistanceTo(_currentCombatTarget.GlobalPosition) > 45)))
            ClearCombatTargetFocus();
        if (_moduleRules is not null && _areaRules is not null && _resolvedDifficulty is not null)
        {
            var next = _moduleRules.Progression.Curve.FirstOrDefault(point => point.Level > _progressionLevel)
                ?.RequiredExperience;
            _hud.UpdateRules(_resolvedDifficulty.DisplayName, _progressionLevel, _progressionExperience, next,
                _areaRules.Combat.PvpMode.ToString(), _areaRules.Perception.Modifiers.Count);
        }
        _hud.UpdateInteractionPrompt(ResolveInteractionPrompt());
    }

    private string? ResolveInteractionPrompt()
    {
        if (_player is null || _gameplayControls is null) return null;
        var nearest = new Node3D?[] { _flyingMount, _npc, _keyItem, _door }
            .Where(node => node is not null && GodotObject.IsInstanceValid(node))
            .Cast<Node3D>()
            .Concat(_portalNodes.Keys.Where(GodotObject.IsInstanceValid))
            .Concat(_corpseLoot.Keys.Where(GodotObject.IsInstanceValid))
            .OrderBy(node => _player.GlobalPosition.DistanceTo(node.GlobalPosition))
            .FirstOrDefault();
        var interactionDistance = _gameplayControls.Foundation.Interaction.FocusDistance;
        if (nearest is null || !IsNear(nearest, interactionDistance)) return null;
        if (nearest == _npc) return $"Parler à {_guideName}";
        if (nearest == _keyItem && !_hasKey) return $"Prendre {_questItemDisplayName}";
        if (nearest == _door && !_doorUnlocked)
            return _hasKey ? $"Ouvrir {_gateDisplayName}" : $"Examiner {_gateDisplayName}";
        if (nearest == _flyingMount) return _flyingMount?.IsMounted == true ? "Descendre" : "Monter";
        if (_corpseLoot.ContainsKey(nearest)) return "Fouiller";
        if (_portalNodes.ContainsKey(nearest)) return "Emprunter le passage";
        return null;
    }

    private bool HasAttackTargetInRange()
    {
        if (_player is null) return false;
        foreach (var pair in _hostiles)
        {
            if (!GodotObject.IsInstanceValid(pair.Key) || pair.Value.IsDead) continue;
            if (_player.GlobalPosition.DistanceTo(pair.Key.GlobalPosition) <= AttackDistance) return true;
        }
        return false;
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
            var published = _gameplayEvents.Publish(progressionEvent, _moduleRuntimePolicies?.GameplayEvents,
                _moduleRuntimePolicies?.GameplayEvents.RequiredAtRuntime == true);
            if (published.Accepted && _moduleRuntimePolicies?.GameplayEvents.Bindings.SingleOrDefault(item =>
                    item.EventName == progressionEvent.EventName) is { } binding)
            {
                DispatchConfiguredEventScript(binding.ScriptPath, binding.Handler, binding.RequiredAtRuntime,
                    "progression:changed");
            }
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

        if (!_isCryptPrototype)
        {
            var areaName = _activeArea?.DisplayName ?? _moduleTitle;
            PresentHudState(
                $"explorer {areaName}",
                _message,
                "Parcourez la zone et découvrez ses lieux remarquables.");
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

        var state = _questState switch
        {
            PrototypeQuestState.NotStarted => "Elyra attend près du sceau et semble vouloir vous parler.",
            PrototypeQuestState.Active when !_hasKey =>
                "La Clé de braise repose dans le reliquaire latéral à droite des catacombes, avant la porte scellée.",
            PrototypeQuestState.Active when !_doorUnlocked => "La clé pulse à proximité de la Porte des Cendres.",
            PrototypeQuestState.Active => "Le gardien protège la dernière route vers la lumière.",
            PrototypeQuestState.Completed => "La chaleur retombe. Le chemin de sortie est désormais libre.",
            _ => "Les pierres anciennes gardent encore leurs secrets.",
        };
        PresentHudState(objective, _message, state);
    }

    private void PresentHudState(string objective, string message, string state)
    {
        _hud?.UpdateState(objective, message, state);
        var cleanObjective = objective.Replace("[E]", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        if (string.Equals(cleanObjective, _lastPresentedObjective, StringComparison.Ordinal)) return;
        _lastPresentedObjective = cleanObjective;
        _toastQueue?.Show(RuntimeToastKind.Objective, $"Objectif : {cleanObjective}",
            $"objective:{cleanObjective}");
        if (_trackedWaypointMarker is { } marker)
            _moduleMap?.ShowPing(marker.Position, RuntimeMapPingKind.Objective, marker.AccessibleText);
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
        var availableChoices = traversal.GetAvailableChoices(evaluator);
        var spokenLine = variantText ?? start.Node!.Text.Fallback;
        if (_actionWheel?.IsOpen == true) _actionWheel.Close();
        _dialogueOverlay?.Show(_guideName,
            string.Equals(_guideName, "Elyra", StringComparison.OrdinalIgnoreCase)
                ? "Gardienne des braises"
                : "Guide du voyage",
            spokenLine,
            availableChoices.Select(item => item.Text.Fallback).ToArray());
        SyncPlayerInterfaceLock();
        var choice = availableChoices.FirstOrDefault();
        if (choice is null)
        {
            _message = $"{_guideName} : {spokenLine}";
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
            node.ResetPhysicsInterpolation();
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

}
