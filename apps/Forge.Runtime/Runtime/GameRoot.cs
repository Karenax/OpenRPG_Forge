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
    private const string VisualCaptureResolution = "1600x900";
    private const string SolarenVisualFixtureArgument = "--solaren-visual-fixture";
    private const string ModuleMapVisualCaptureArgument = "--module-map-visual-capture";
    private const string ModuleMapFloorArgument = "--module-map-floor";
    private const string RpgMenuVisualCaptureArgument = "--rpg-menu-visual-capture";
    private const string RpgMenuPageArgument = "--rpg-menu-page";
    private const string DialogueVisualCaptureArgument = "--dialogue-visual-capture";
    private const string ActionWheelVisualCaptureArgument = "--action-wheel-visual-capture";
    private const string CombatFeedbackCaptureArgument = "--combat-feedback-capture";
    private const string CombatFeedbackCaptureSizeArgument = "--combat-feedback-capture-size";
    private const string VisualRegressionSuiteArgument = "--g11s-visual-suite";
    private const string MapQualityAuditArgument = "--map-quality-audit";
    private const string MapQualityMinimumPatrolActorsArgument = "--map-quality-min-patrols";
    private const string MapQualityMinimumBuildingGroupsArgument = "--map-quality-min-building-groups";
    private const string MapQualityRequiredVegetationStrataArgument =
        "--map-quality-required-vegetation-strata";
    private const string MapQualityMinimumGroundCoverDensityArgument =
        "--map-quality-min-ground-cover-density";
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
    private static RuntimeSaveDocument? s_pendingPortalRuntimeState;
    private static int s_innUxSmokeStep;
    private static int s_areaStateSmokeStep;
    private ProjectDefinition? _loadedProject;
    private readonly Dictionary<Node3D, CombatantState> _hostiles = [];
    private readonly Dictionary<Node3D, string> _hostileNames = [];
    private readonly Dictionary<Node3D, BlueprintDefinition> _hostileBlueprints = [];
    private readonly Dictionary<Node3D, IReadOnlyList<LootGrant>> _corpseLoot = [];
    private readonly Dictionary<Node3D, CreatureEventProfile> _corpseCreatureEvents = [];
    private ProjectBlackboardScriptService _blackboard = ProjectBlackboardScriptService.ForRuntime([]);
    private readonly RuntimeInventoryState _inventory = new();
    private readonly Dictionary<string, RuntimeAreaSaveState> _runtimeAreaStates =
        new(StringComparer.OrdinalIgnoreCase);
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
    private readonly Queue<(long Id, Action Mutation)> _pendingCameraMutations = [];
    private long _nextCameraMutationId;
    private long _completedCameraMutationId;
    private readonly Dictionary<GeometryInstance3D, (bool Visible, float Transparency)> _cameraOccluders = [];
    private readonly Dictionary<Label3D, (Color BaseColor, bool IsLocalHero)> _worldLabels = [];
    private SequenceEvaluation? _pendingSequenceCompletion;
    private string? _pendingSequenceCompletionReason;
    private float? _pendingCameraFieldOfView;
    private Vector3? _pendingCameraPosition;
    private Vector3? _pendingCameraLookAt;
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
    private RuntimeModulePresentation? _runtimePresentation;
    private AreaMapExplorationState? _mapExploration;
    private string? _mapExplorationAreaId;
    private string? _mapExplorationCell;
    private RuntimeDialogueOverlay? _dialogueOverlay;
    private RuntimeDialogueVoicePlayer? _dialogueVoicePlayer;
    private Node3D? _npc;
    private readonly Dictionary<Node3D, (BlueprintId BlueprintId, string DisplayName)> _narrativeParticipants = [];
    private Node3D? _activeDialogueParticipant;
    private Node3D? _keyItem;
    private Node3D? _door;
    private Node3D? _boss;
    private Node3D? _bossCorpse;
    private Node3D? _exit;
    private DialogueDefinition? _activeDialogue;
    private DialogueNodeId? _activeDialogueEntryNodeId;
    private DialogueTraversal? _activeDialogueTraversal;
    private DialogueId? _activeDialogueTraversalDialogueId;
    private IReadOnlyList<DialogueChoice> _activeDialogueChoices = [];
    private bool _dialogueChoiceCommitted;
    private RuntimeQuestStateTracker? _questTracker;
    private QuestDefinition? _trackedQuestDefinition;
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
    private RuntimeModuleExperienceProfile _moduleExperience = RuntimeModuleExperienceProfile.Default;
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
    private readonly GodotSurfaceWetnessAdapter _surfaceWetnessMaterials = new();
    private SurfaceWetnessTransitionState? _surfaceWetnessTransition;
    private SurfaceWetnessMaterialProfile? _surfaceWetnessCoefficients;
    private GodotSurfaceWetnessResult? _surfaceWetnessResult;
    private float _surfaceWetnessValue;
    private bool _surfaceWetnessInitialized;
    private bool _surfaceWetnessBound;
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
    private readonly Dictionary<Node3D, EntityWaypointPatrolState> _entityWaypointPatrols = [];
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
        var dialogueVoiceCompletionSmokeTest = userArguments.Contains(
            "--dialogue-voice-completion-smoke-test", StringComparer.Ordinal);
        var combatFeedbackSmokeTest = userArguments.Contains("--combat-feedback-smoke-test", StringComparer.Ordinal);
        var entryGroundSmokeTest = userArguments.Contains("--entry-ground-smoke-test", StringComparer.Ordinal);
        var moduleMovementSmokeTest = userArguments.Contains("--module-movement-smoke-test", StringComparer.Ordinal) ||
                                      userArguments.Contains("--crypt-movement-smoke-test", StringComparer.Ordinal);
        var innUxSmokeTest = userArguments.Contains("--inn-ux-smoke-test", StringComparer.Ordinal);
        var areaStateSmokeTest = userArguments.Contains("--area-state-smoke-test", StringComparer.Ordinal);
        var areaSaveResumeSmokeTest = userArguments.Contains("--area-save-resume-smoke-test", StringComparer.Ordinal);
        var secureInternetRuntimeSmokeTest = userArguments.Contains(
            "--secure-internet-runtime-smoke-test", StringComparer.Ordinal);
        var saveSmokeCreate = userArguments.Contains("--save-smoke-create", StringComparer.Ordinal);
        var saveSmokeResume = userArguments.Contains("--save-smoke-resume", StringComparer.Ordinal);
        var visualCapturePath = GetArgumentValue(userArguments, VisualCaptureArgument);
        var solarenVisualFixture = GetArgumentValue(userArguments, SolarenVisualFixtureArgument);
        var moduleMapVisualCapturePath = GetArgumentValue(userArguments, ModuleMapVisualCaptureArgument);
        var moduleMapFloorValue = GetArgumentValue(userArguments, ModuleMapFloorArgument);
        int? moduleMapFloor = null;
        if (moduleMapFloorValue is not null)
        {
            if (!int.TryParse(moduleMapFloorValue, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var parsedFloor))
                throw new InvalidOperationException("The requested module map floor must be an integer.");
            moduleMapFloor = parsedFloor;
        }
        var rpgMenuVisualCapturePath = GetArgumentValue(userArguments, RpgMenuVisualCaptureArgument);
        var dialogueVisualCapturePath = GetArgumentValue(userArguments, DialogueVisualCaptureArgument);
        var actionWheelVisualCapturePath = GetArgumentValue(userArguments, ActionWheelVisualCaptureArgument);
        var combatFeedbackCapturePath = GetArgumentValue(userArguments, CombatFeedbackCaptureArgument);
        var combatFeedbackCaptureSize = GetArgumentValue(userArguments, CombatFeedbackCaptureSizeArgument);
        var visualRegressionSuitePath = GetArgumentValue(userArguments, VisualRegressionSuiteArgument);
        var mapQualityAuditPath = GetArgumentValue(userArguments, MapQualityAuditArgument);
        var mapQualityMinimumPatrolActors = ParseMapQualityNonNegativeInteger(
            userArguments, MapQualityMinimumPatrolActorsArgument, 0);
        var mapQualityMinimumBuildingGroups = ParseMapQualityNonNegativeInteger(
            userArguments, MapQualityMinimumBuildingGroupsArgument, 0);
        var mapQualityRequiredVegetationStrata = ParseMapQualityOptionalNonNegativeInteger(
            userArguments, MapQualityRequiredVegetationStrataArgument);
        var mapQualityMinimumGroundCoverDensity = ParseMapQualityOptionalNonNegativeDouble(
            userArguments, MapQualityMinimumGroundCoverDensityArgument);
        var automatedTest = smokeTest || genericSmokeTest || solarenSmokeTest || dialogueVoiceCompletionSmokeTest ||
            combatFeedbackSmokeTest ||
            entryGroundSmokeTest || moduleMovementSmokeTest ||
            innUxSmokeTest || areaStateSmokeTest || areaSaveResumeSmokeTest ||
            secureInternetRuntimeSmokeTest ||
            saveSmokeCreate || saveSmokeResume ||
            visualCapturePath is not null ||
            solarenVisualFixture is not null ||
            moduleMapVisualCapturePath is not null ||
            rpgMenuVisualCapturePath is not null || dialogueVisualCapturePath is not null ||
            actionWheelVisualCapturePath is not null ||
            combatFeedbackCapturePath is not null ||
            visualRegressionSuitePath is not null ||
            mapQualityAuditPath is not null;

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
            var portalRuntimeState = s_pendingPortalRuntimeState;
            s_pendingPortalSpawn = null;
            s_pendingPortalLabel = null;
            s_pendingPortalRuntimeState = null;
            var playtestManifestPath = GetArgumentValue(userArguments, PlaytestManifestArgument);
            var playtestManifest = playtestManifestPath is null
                ? null
                : PlaytestLaunchManifestStore.Load(playtestManifestPath);
            var project = LoadProject(snapshotPath, modulePackagePath, workspacePath);
            if (loadSavePath is not null && requestedSpawnId is null)
            {
                var startupSave = RuntimeSaveStore.Load(
                    loadSavePath,
                    _moduleId ?? throw new InvalidOperationException("Loaded module identity is missing."),
                    _moduleContentSha256 ?? throw new InvalidOperationException("Loaded module fingerprint is missing."));
                if (!startupSave.Succeeded || startupSave.Save is null)
                {
                    throw new InvalidOperationException(
                        "The requested runtime save could not select its owning area: " +
                        string.Join(";", startupSave.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
                }

                requestedSpawnId = ResolvePlayerSpawnId(project, startupSave.Save.AreaId);
            }
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
            ConfigureWorldFeatures(project);
            ConfigurePostLaunch(project);
            _message = $"Explorez {_moduleTitle}.";
            var startArea = ResolveStartArea(project, requestedSpawnId);
            // World configuration may resolve area-owned streamed resources (including
            // promoted WorldGeneration scenes), so the active-area identity must be
            // available before the world is assembled rather than only when entities
            // are materialized by BuildProject.
            _activeArea = startArea;
            _moduleExperience = RuntimeModuleExperienceProfile.From(startArea);
            _message = $"Explorez {startArea.DisplayName}.";
            ConfigureRuntimePolicies(project.Module.RuntimePolicies, startArea.RuntimePolicies);
            PublishProjectConnectionEvent(PlayerConnectionStage.Joining, "authenticated");
            await ConfigureWorldAsync(startArea.Environment, startArea.World, startArea.Navigation);
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
                var camera = _camera;
                var target = _player.GlobalPosition;
                await MutateCameraInPhysicsAsync(() =>
                    _cameraRig.Snap(camera, target,
                        _moduleExperience.ConstrainCamera ? ConstrainCameraPosition : null));
            }
            EnsureLifecycle(
                _lifecycle.EnterInitialArea(
                    AreaId.Parse(_currentAreaId!),
                    selectedSpawn.Id,
                    lifecycleCause),
                "area_entered");
            if (portalRuntimeState is not null)
            {
                ApplyRuntimeSave(portalRuntimeState);
                GD.Print($"OPENRPG_FORGE_RUNTIME_AREA_STATE_TRANSFER_PASS destination={startArea.Slug} " +
                         $"areas={_runtimeAreaStates.Count} inventory={_inventory.Snapshot().Count} " +
                         $"quests={portalRuntimeState.QuestStates?.Count ?? 0}");
            }
            if (loadSavePath is not null)
            {
                if (!LoadRuntimeSave(loadSavePath))
                {
                    throw new InvalidOperationException("The requested runtime save could not be loaded.");
                }
            }

            CreateHud();
            if (moduleMapFloor is { } requestedMapFloor) SetActiveMapFloor(requestedMapFloor);
            ApplyModuleWindowTitle();
            ConfigureWaypointRuntime(startArea.Navigation.Waypoints);
            if (solarenVisualFixture is not null)
            {
                if (visualCapturePath is null)
                    throw new InvalidOperationException(
                        "A Solaren visual fixture requires an explicit --visual-capture destination.");
                await ConfigureSolarenVisualFixtureAsync(solarenVisualFixture);
            }
            BindSurfaceWetnessMaterials();
            if (RuntimeStartupVerificationPolicy.ShouldRun(
                    automatedTest,
                    entryGroundSmokeTest,
                    innUxSmokeTest,
                    moduleMovementSmokeTest,
                    combatFeedbackSmokeTest,
                    combatFeedbackCapturePath is not null,
                    mapQualityAuditPath is not null))
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
            if (!moduleMovementSmokeTest)
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

            if (mapQualityAuditPath is not null)
            {
                await RunMapQualityAuditAsync(
                    mapQualityAuditPath,
                    mapQualityMinimumPatrolActors,
                    mapQualityMinimumBuildingGroups,
                    mapQualityRequiredVegetationStrata,
                    mapQualityMinimumGroundCoverDensity);
            }
            else if (secureInternetRuntimeSmokeTest)
            {
                await RunSecureInternetRuntimeSmokeTestAsync();
            }
            else if (areaSaveResumeSmokeTest)
            {
                RunAreaSaveResumeSmokeTest(project);
            }
            else if (areaStateSmokeTest)
            {
                await RunAreaStateTransitionSmokeTestAsync(project);
            }
            else if (innUxSmokeTest)
            {
                await RunInnUxSmokeTestAsync(project);
            }
            else if (entryGroundSmokeTest)
            {
                await RunEntryGroundSmokeTestAsync(project);
            }
            else if (moduleMovementSmokeTest)
            {
                await RunModuleMovementSmokeTestAsync(project);
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
            else if (dialogueVoiceCompletionSmokeTest)
            {
                await RunDialogueVoiceCompletionSmokeTestAsync();
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
            if (areaStateSmokeTest) s_areaStateSmokeStep = 0;
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

    public override void _Process(double delta)
    {
        var framePlan = RuntimeFramePhasePlanner.Plan(RuntimeFramePhase.Process);
        if (!framePlan.ProcessInputAndUi) return;
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
            AdvanceCreatureAmbientBehavior(simulationDelta);
            AdvanceEntityWaypointPatrols(simulationDelta);
            AdvanceCreatureEvents(simulationDelta);
        }

        if (_player is null)
        {
            return;
        }

        if (!IsTimeFrozen())
        {
            AdvanceLivingWorldTriggers();
            AdvanceMapExploration();
        }

        if (_camera is not null && !_sequenceOwnsCamera)
        {
            SyncLocalPlayerCameraVisibility();
        }
        UpdateWorldLabels();

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
                QueueSequenceCompletion(SequencePlayer.Skip(_activeSequence, _sequenceState), "skipped");
            }
            else
            {
                AbortActiveSequence();
            }
        }

        if (_moduleExperience.CompleteOnBossExit && !_escaped && _bossDefeated && IsNear(_exit, 2.5f))
        {
            _escaped = true;
            _message = $"{_moduleTitle} est terminé !";
            UpdateHud();
            GD.Print("OPENRPG_FORGE_PROTOTYPE_COMPLETED");
        }

        UpdateTacticalHud();
    }

    public override void _PhysicsProcess(double delta)
    {
        var framePlan = RuntimeFramePhasePlanner.Plan(RuntimeFramePhase.PhysicsProcess);
        while (_pendingCameraMutations.TryDequeue(out var pendingMutation))
        {
            pendingMutation.Mutation();
            _completedCameraMutationId = pendingMutation.Id;
        }
        if (!_initializationComplete || !framePlan.MutateInterpolatedCamera) return;
        RestoreCameraOccluders();

        if (_pendingSequenceCompletion is { } completion)
        {
            var reason = _pendingSequenceCompletionReason ?? "completed";
            _pendingSequenceCompletion = null;
            _pendingSequenceCompletionReason = null;
            CompleteSequence(completion, reason);
        }

        var simulationDelta = delta * (_timeControlSession.State == RuntimeTimeState.SlowMotion
            ? _timeControlSession.Scale
            : 1);
        if (!IsTimeFrozen() && framePlan.AdvanceCameraCinematics)
            AdvanceCinematics(simulationDelta);

        if (_camera is null) return;
        if (_player is not null && !_sequenceOwnsCamera)
        {
            var cameraTarget = _flyingMount is { IsMounted: true }
                ? _flyingMount.GlobalPosition
                : _player.GlobalPosition;
            _cameraRig.Update(_camera, cameraTarget, delta,
                _moduleExperience.ConstrainCamera ? ConstrainCameraPosition : null);
            if (_cameraRig.AlignCharacterFacing && _flyingMount?.IsMounted != true)
                _player.FaceDirection(_cameraRig.ForwardDirection);
        }
        if (_pendingCameraFieldOfView is { } fieldOfView)
        {
            _camera.Fov = fieldOfView;
            _pendingCameraFieldOfView = null;
        }
        if (_pendingCameraPosition is { } position)
        {
            _camera.GlobalPosition = position;
            _pendingCameraPosition = null;
        }
        if (_pendingCameraLookAt is { } lookAt)
        {
            _camera.LookAt(lookAt, Vector3.Up);
            _pendingCameraLookAt = null;
        }
    }

    private async Task MutateCameraInPhysicsAsync(Action mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        var id = ++_nextCameraMutationId;
        _pendingCameraMutations.Enqueue((id, mutation));
        while (_completedCameraMutationId < id)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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
        var exteriorTerrain = TryGetExteriorGroundHeight(out var groundHeightAt);
        if (exteriorTerrain)
        {
            desired = RuntimeCameraRig.RaiseForTerrainClearance(
                focus,
                desired,
                groundHeightAt,
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
                    var horizontalResolution = _cameraRig.ResolveOcclusion(horizontalDistance, allowedDistance);
                    if (horizontalResolution.OccluderTreatment == CameraOccluderTreatment.None)
                    {
                        var fraction = Math.Clamp(horizontalResolution.CameraDistance / horizontalDistance, 0, 1);
                        desired = focus + ((desired - focus) * fraction);
                    }
                    else
                    {
                        ApplyCameraOccluderTreatment(horizontalHit, horizontalResolution.OccluderTreatment);
                    }
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
            IsTerrainCollision(collider.AsGodotObject()))
        {
            return desired;
        }
        var direction = (desired - focus).Normalized();
        var idealDistance = focus.DistanceTo(desired);
        var obstacleDistance = Math.Max(0,
            focus.DistanceTo(position.AsVector3()) - CameraDirectHitClearance);
        var resolution = _cameraRig.ResolveOcclusion(idealDistance, obstacleDistance);
        if (resolution.OccluderTreatment != CameraOccluderTreatment.None)
        {
            ApplyCameraOccluderTreatment(hit, resolution.OccluderTreatment);
            return desired;
        }
        return focus + (direction * resolution.CameraDistance);
    }

    private bool TryGetExteriorGroundHeight(out Func<float, float, float> heightAt)
    {
        if (_interiorWalkableBounds is null && _activeArea?.Kind == AreaKind.Exterior)
        {
            if (_activeArea.World.PlacementSurface is { } placementSurface)
            {
                heightAt = placementSurface.HeightAt;
                return true;
            }
            if (_activeTerrain?.Enabled == true)
            {
                heightAt = _activeTerrain.HeightAt;
                return true;
            }
        }

        heightAt = static (_, _) => 0;
        return false;
    }

    private static bool IsTerrainCollision(GodotObject? collider) => collider is Node node &&
        (node.Name == "TerrainCollision" ||
         node.HasMeta("openrpg_terrain_collision") && node.GetMeta("openrpg_terrain_collision").AsBool());

    private void ApplyCameraOccluderTreatment(
        Godot.Collections.Dictionary hit,
        CameraOccluderTreatment treatment)
    {
        if (treatment == CameraOccluderTreatment.None ||
            !hit.TryGetValue("collider", out var collider) ||
            collider.AsGodotObject() is not Node colliderNode ||
            FindCameraOccluderVisual(colliderNode) is not { } visual)
            return;
        if (!_cameraOccluders.ContainsKey(visual))
            _cameraOccluders.Add(visual, (visual.Visible, visual.Transparency));
        if (treatment == CameraOccluderTreatment.Hide)
            visual.Visible = false;
        else
            visual.Transparency = Math.Max(visual.Transparency, .65f);
    }

    private GeometryInstance3D? FindCameraOccluderVisual(Node collider)
    {
        Node? current = collider;
        for (var depth = 0; current is not null && depth < 4; depth++, current = current.GetParent())
        {
            if (current == this) break;
            if (current is GeometryInstance3D geometry && !IsLocalPlayerVisual(geometry))
                return geometry;
            foreach (var candidate in current.FindChildren("*", "GeometryInstance3D", recursive: true, owned: false))
                if (candidate is GeometryInstance3D child && !IsLocalPlayerVisual(child))
                    return child;
        }
        return null;
    }

    private bool IsLocalPlayerVisual(Node node) =>
        _player is not null && (node == _player || node.IsAncestorOf(_player) || _player.IsAncestorOf(node));

    private void RestoreCameraOccluders()
    {
        foreach (var pair in _cameraOccluders)
        {
            if (!GodotObject.IsInstanceValid(pair.Key)) continue;
            pair.Key.Visible = pair.Value.Visible;
            pair.Key.Transparency = pair.Value.Transparency;
        }
        _cameraOccluders.Clear();
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

    private void UpdateWorldLabels()
    {
        if (_camera is null) return;
        foreach (var pair in _worldLabels.ToArray())
        {
            if (!GodotObject.IsInstanceValid(pair.Key) || !pair.Key.IsInsideTree())
            {
                _worldLabels.Remove(pair.Key);
                continue;
            }
            var distance = _camera.GlobalPosition.DistanceTo(pair.Key.GlobalPosition);
            var presentation = _cameraRig.ResolveWorldLabel(distance, pair.Value.IsLocalHero);
            pair.Key.Visible = presentation.Visible;
            pair.Key.Scale = Vector3.One * presentation.UniformScale;
            var color = pair.Value.BaseColor;
            pair.Key.Modulate = new Color(color.R, color.G, color.B, color.A * presentation.Opacity);
        }
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
        => ReadModuleContentFile(relativePath);

    private string ReadModuleContentFile(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (_moduleContentRoot is null || Path.IsPathFullyQualified(relativePath) ||
            relativePath.Contains('\\', StringComparison.Ordinal))
            throw new InvalidOperationException($"Module content path '{relativePath}' is unsafe.");
        var root = Path.GetFullPath(_moduleContentRoot);
        var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(path))
        {
            throw new InvalidOperationException($"Module content file '{relativePath}' is unavailable.");
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
                    var eventQuest = _questTracker?.ResolveQuest(scriptEvent.Value);
                    if (eventQuest is not null && _questTracker!.IsAccepted(eventQuest.Id) &&
                        !TryAdvanceQuestByEvent(eventQuest, QuestEventKind.Script, "script", scriptEvent.Value))
                        TryAdvanceQuestByEvent(eventQuest, QuestEventKind.Manual, "manual", sourceReference: null);

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
        RefreshRuntimePresentation();
        _rpgMenu.UpdateContext(_runtimePresentation?.Journal.ActiveObjective,
            _runtimePresentation?.Journal.ActiveEntry?.Details);
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
        var hero = _runtimePresentation?.Hero;
        var healthResource = hero?.Resources.FirstOrDefault(resource =>
            string.Equals(resource.Id, "health", StringComparison.OrdinalIgnoreCase));
        var primaryResource = hero is null
            ? null
            : RuntimePrimaryResourceSelector.Select(hero.Resources);
        _hud.UpdatePlayerStatus(hero?.DisplayName ?? "Héros", healthResource, primaryResource, hero?.Level);
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
        var progression = _runtimePresentation?.Progression;
        if (_moduleRules is not null && _areaRules is not null && _resolvedDifficulty is not null &&
            progression?.Level is { } liveLevel && progression.Experience is { } liveExperience)
        {
            var next = _moduleRules.Progression.Curve.FirstOrDefault(point => point.Level > liveLevel)
                ?.RequiredExperience;
            _hud.UpdateRules(_resolvedDifficulty.DisplayName, liveLevel, liveExperience, next,
                _areaRules.Combat.PvpMode.ToString(), _areaRules.Perception.Modifiers.Count);
        }
        _hud.UpdateInteractionPrompt(ResolveInteractionPrompt());
    }

    private string? ResolveInteractionPrompt()
    {
        if (_player is null || _gameplayControls is null) return null;
        var nearest = new Node3D?[] { _flyingMount, _keyItem, _door }
            .Where(node => node is not null && GodotObject.IsInstanceValid(node))
            .Cast<Node3D>()
            .Concat(_narrativeParticipants.Keys.Where(GodotObject.IsInstanceValid))
            .Concat(_portalNodes.Keys.Where(GodotObject.IsInstanceValid))
            .Concat(_corpseLoot.Keys.Where(GodotObject.IsInstanceValid))
            .Distinct()
            .OrderBy(node => _player.GlobalPosition.DistanceTo(node.GlobalPosition))
            .FirstOrDefault();
        var interactionDistance = _gameplayControls.Foundation.Interaction.FocusDistance;
        if (nearest is null || !IsNear(nearest, interactionDistance)) return null;
        if (_narrativeParticipants.TryGetValue(nearest, out var participant))
            return $"Parler à {participant.DisplayName}";
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

        RefreshRuntimePresentation();
        UpdateTacticalHud();
        var activeEntry = _runtimePresentation?.Journal.ActiveEntry;
        PresentHudState(
            _runtimePresentation?.Journal.ActiveObjective ?? string.Empty,
            _message,
            activeEntry?.Summary ?? "Aucune quête active dans ce module.");
    }

    private void PresentHudState(string objective, string message, string state)
    {
        _hud?.UpdateState(objective, message, state);
        var cleanObjective = objective.Replace("[E]", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        if (string.IsNullOrWhiteSpace(cleanObjective))
        {
            _lastPresentedObjective = string.Empty;
            return;
        }
        if (string.Equals(cleanObjective, _lastPresentedObjective, StringComparison.Ordinal)) return;
        _lastPresentedObjective = cleanObjective;
        _toastQueue?.Show(RuntimeToastKind.Objective, $"Objectif : {cleanObjective}",
            $"objective:{cleanObjective}");
        if (_trackedWaypointMarker is { } marker)
            _moduleMap?.ShowPing(marker.Position, RuntimeMapPingKind.Objective, marker.AccessibleText);
    }

}
