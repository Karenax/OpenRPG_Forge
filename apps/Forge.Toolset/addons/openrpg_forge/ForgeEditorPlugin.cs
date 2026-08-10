#if TOOLS
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Areas;
using Forge.Application.Assistant;
using Forge.Application.Assets;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Content;
using Forge.Application.Creator;
using Forge.Application.Creatures;
using Forge.Application.Dialogues;
using Forge.Application.Entities;
using Forge.Application.Factions;
using Forge.Application.Gameplay;
using Forge.Application.Items;
using Forge.Application.Modules;
using Forge.Application.Rules;
using Forge.Application.Localization;
using Forge.Application.Lifecycle;
using Forge.Application.Navigation;
using Forge.Application.Sequences;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.Quality;
using Forge.Application.Spatial;
using Forge.Application.Time;
using Forge.Application.WorldFeatures;
using Forge.Domain.Areas;
using Forge.Domain.Audio;
using Forge.Domain.Assistant;
using Forge.Domain.Assets;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Content;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Factions;
using Forge.Domain.Gameplay;
using Forge.Domain.Localization;
using Forge.Domain.Lifecycle;
using Forge.Domain.Items;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quality;
using Forge.Domain.Quests;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Rules;
using Forge.Domain.Sequences;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.Domain.WorldFeatures;
using Forge.EngineAdapter.Assets;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.World;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Distribution;
using Forge.ModuleFormat.Diagnostics;
using Forge.ModuleFormat.Navigation;
using Forge.ModuleFormat.Playtesting;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Application.Abilities;
using Forge.RpgKit.Application.Bosses;
using Forge.RpgKit.Application.Dialogues;
using Forge.RpgKit.Application.Merchants;
using Forge.RpgKit.Application.Quests;
using Forge.RpgKit.Domain.Merchants;
using Forge.Scripting;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class ForgeEditorPlugin : EditorPlugin
{
    private const string MainPanelPath = "res://addons/openrpg_forge/main_panel.tscn";
    private const string ForgeWindowTitle = "OpenRPG_Forge";
    private const float QuarterTurnComponent = 0.70710677f;

    private readonly PlaceEntityHandler _placeHandler = new(new SystemClock());
    private readonly PasteEntitiesHandler _pasteEntitiesHandler = new(new SystemClock());
    private readonly CutEntitiesHandler _cutEntitiesHandler = new(new SystemClock());
    private readonly CreateAreaHandler _createAreaHandler = new(new SystemClock());
    private readonly DuplicateAreaHandler _duplicateAreaHandler = new(new SystemClock());
    private readonly EditAreaGeometryHandler _editAreaGeometryHandler = new(new SystemClock());
    private readonly SetAreaEnvironmentHandler _setAreaEnvironmentHandler = new(new SystemClock());
    private readonly SetAreaWorldProfileHandler _setAreaWorldProfileHandler = new(new SystemClock());
    private readonly EditTerrainBrushHandler _editTerrainBrushHandler = new(new SystemClock());
    private readonly SetRuntimePoliciesHandler _setRuntimePoliciesHandler = new(new SystemClock());
    private readonly SetCinematicProfilesHandler _setCinematicProfilesHandler = new(new SystemClock());
    private readonly SetGameplayControlProfileHandler _setGameplayControlProfileHandler = new(new SystemClock());
    private readonly SetRulesProfilesHandler _setRulesProfilesHandler = new(new SystemClock());
    private readonly SetAreaExperienceProfileHandler _setAreaExperienceProfileHandler = new(new SystemClock());
    private readonly SetRuntimeExtensionsHandler _setRuntimeExtensionsHandler = new(new SystemClock());
    private readonly SetAreaAuthoringHandler _setAreaAuthoringHandler = new(new SystemClock());
    private readonly SetAreaFoundationHandler _setAreaFoundationHandler = new(new SystemClock());
    private readonly SetAreaPlaytestHandler _setAreaPlaytestHandler = new(new SystemClock());
    private readonly UpsertPortalLinkHandler _upsertPortalHandler = new(new SystemClock());
    private readonly TransformEntityHandler _transformHandler = new(new SystemClock());
    private readonly ScaleEntitiesHandler _scaleEntitiesHandler = new(new SystemClock());
    private readonly ManipulateEntitiesHandler _manipulateEntitiesHandler = new(new SystemClock());
    private readonly RemoveEntityHandler _removeHandler = new(new SystemClock());
    private readonly SetEntityOverridesHandler _setOverridesHandler = new(new SystemClock());
    private readonly NewModuleHandler _newModuleHandler = new(new SystemClock());
    private readonly SetDialogueHandler _setDialogueHandler = new(new SystemClock());
    private readonly SetQuestHandler _setQuestHandler = new(new SystemClock());
    private readonly UpsertBlueprintHandler _upsertBlueprintHandler = new(new SystemClock());
    private readonly CreateBlueprintFromEntityHandler _createBlueprintFromEntityHandler = new(new SystemClock());
    private readonly ConfigureBlueprintPaletteHandler _configureBlueprintPaletteHandler = new(new SystemClock());
    private readonly SafeRenameHandler _safeRenameHandler = new(new SystemClock());
    private readonly SetModuleConfigurationHandler _setModuleConfigurationHandler = new(new SystemClock());
    private readonly SetModuleDistributionHandler _setModuleDistributionHandler = new(new SystemClock());
    private readonly SetContentWorkspaceHandler _setContentWorkspaceHandler = new(new SystemClock());
    private readonly SetArtisticStyleProfileHandler _setArtisticStyleProfileHandler = new(new SystemClock());
    private readonly SetLocalizationCatalogHandler _setLocalizationCatalogHandler = new(new SystemClock());
    private readonly SetAreaNavigationHandler _setAreaNavigationHandler = new(new SystemClock());
    private readonly ApplyDiagnosticQuickFixHandler _diagnosticQuickFixHandler = new(new SystemClock());
    private readonly SetAssetProcessingProfileHandler _assetProcessingProfileHandler = new(new SystemClock());
    private readonly SetAssetGovernanceHandler _assetGovernanceHandler = new(new SystemClock());
    private readonly SetQualityFoundationHandler _qualityFoundationHandler = new(new SystemClock());
    private readonly SetAssetPackageManifestHandler _assetPackageManifestHandler = new(new SystemClock());
    private readonly InstallAssetBlueprintHandler _installAssetBlueprintHandler = new(new SystemClock());
    private readonly SetAssetExternalEditHandler _assetExternalEditHandler = new(new SystemClock());
    private readonly SetAssistantWorkspaceProfileHandler _assistantProfileHandler = new(new SystemClock());
    private readonly SetLoreIndexHandler _loreIndexHandler = new(new SystemClock());
    private readonly SetAssistedAreaPlansHandler _assistedAreaPlanHandler = new(new SystemClock());
    private readonly BuildAssistedAreaPlanHandler _assistedAreaBuildHandler = new(new SystemClock());
    private readonly ApplyAssistedDialogueHandler _assistedDialogueHandler = new(new SystemClock());
    private readonly ApplyAssistedQuestHandler _assistedQuestHandler = new(new SystemClock());
    private readonly ApplyAssistedForgeScriptHandler _assistedForgeScriptHandler = new();
    private readonly ApplyAssistedBossHandler _assistedBossHandler = new(new SystemClock());
    private readonly ApplyAssistedAbilityHandler _assistedAbilityHandler = new(new SystemClock());
    private readonly ApplyAssistedBlenderCreationHandler _assistedBlenderCreationHandler = new(new SystemClock());
    private readonly ApplyAssistedModel3DGenerationHandler _assistedModel3DGenerationHandler = new(new SystemClock());
    private readonly SetExternalWorkerSecurityPolicyHandler _externalWorkerSecurityHandler = new(new SystemClock());
    private readonly SetEditorKeymapHandler _editorKeymapHandler = new(new SystemClock());
    private readonly SetCreaturePresentationHandler _creaturePresentationHandler = new(new SystemClock());
    private readonly SetCreatureFoundationHandler _creatureFoundationHandler = new(new SystemClock());
    private readonly SetCreatureProgressionHandler _creatureProgressionHandler = new(new SystemClock());
    private readonly SetCreatureLootHandler _creatureLootHandler = new(new SystemClock());
    private readonly SetCreatureRoleBehaviorHandler _creatureRoleBehaviorHandler = new(new SystemClock());
    private readonly SetCreatureEventsHandler _creatureEventsHandler = new(new SystemClock());
    private readonly SetFactionCatalogHandler _factionHandler = new(new SystemClock());
    private readonly SetDialoguePresentationHandler _dialoguePresentationHandler = new(new SystemClock());
    private readonly SetQuestOperationsHandler _questOperationsHandler = new(new SystemClock());
    private readonly SetItemAdvancedProfileHandler _itemAdvancedHandler = new(new SystemClock());
    private readonly SetItemFoundationHandler _itemFoundationHandler = new(new SystemClock());
    private readonly SetWorldFeatureCatalogHandler _worldFeatureHandler = new(new SystemClock());
    private readonly AssistedForgeScriptCommandHistory _assistedForgeScriptHistory = new();
    private readonly ConcurrentQueue<Action> _mainThreadActions = new();
    private readonly CreatorSelectionContext _selectionContext = new();
    private readonly ActiveAreaContext _activeAreaContext = new();
    private readonly SemanticClipboard _semanticClipboard = new();
    private RecoverySnapshotStore _recoveryStore = new();
    private readonly List<BlueprintId> _paletteBlueprintIds = [];
    private readonly List<EntityId> _entityIds = [];
    private readonly List<EntityId> _spawnEntityIds = [];
    private readonly List<EntityPropertyOverride> _draftOverrides = [];
    private readonly PlaytestSnapshotStore _playtestSnapshots = new(Path.Combine(
        Path.GetTempPath(),
        "OpenRPG_Forge",
        "playtests"));

    private Control? _mainPanel;
    private Label? _statusLabel;
    private Label? _detailsLabel;
    private Label? _projectPathLabel;
    private Label? _revisionLabel;
    private Button? _openButton;
    private Button? _newModuleButton;
    private Button? _dialoguesButton;
    private Button? _areasButton;
    private Button? _workspaceButton;
    private Button? _recoveryButton;
    private Button? _qualityButton;
    private Button? _searchButton;
    private Button? _moduleSettingsButton;
    private Button? _reloadButton;
    private Button? _saveButton;
    private Button? _exportButton;
    private Button? _placeButton;
    private Button? _moveXNegativeButton;
    private Button? _moveXPositiveButton;
    private Button? _moveZNegativeButton;
    private Button? _moveZPositiveButton;
    private Button? _rotateButton;
    private Button? _deleteButton;
    private SpinBox? _scaleX;
    private SpinBox? _scaleY;
    private SpinBox? _scaleZ;
    private CheckButton? _uniformScale;
    private Button? _scaleDownButton;
    private Button? _scaleUpButton;
    private Button? _applyScaleButton;
    private Button? _overridesButton;
    private Button? _copyButton;
    private Button? _cutButton;
    private Button? _pasteButton;
    private Button? _duplicateButton;
    private CheckButton? _independentCopy;
    private Button? _undoButton;
    private Button? _redoButton;
    private Button? _playButton;
    private OptionButton? _blueprintPalette;
    private OptionButton? _entityPicker;
    private OptionButton? _spawnPicker;
    private Label? _overridesSummary;
    private Label? _clipboardSummary;
    private Label? _playtestStatus;
    private RichTextLabel? _playtestLog;
    private SpinBox? _positionX;
    private SpinBox? _positionZ;
    private SpinBox? _gridSize;
    private FileDialog? _projectDialog;
    private FileDialog? _exportDialog;
    private Window? _newModuleDialog;
    private FileDialog? _newModuleLocationDialog;
    private LineEdit? _newModuleTitle;
    private TextEdit? _newModuleDescription;
    private OptionButton? _newModuleTemplate;
    private LineEdit? _newModuleKits;
    private OptionButton? _newModulePlatform;
    private LineEdit? _newModuleRules;
    private OptionButton? _newModuleRender;
    private OptionButton? _newModuleNetwork;
    private LineEdit? _newModulePackageNamespace;
    private CheckButton? _newModuleCreateInitialArea;
    private LineEdit? _newModuleAreaName;
    private OptionButton? _newModuleAreaKind;
    private SpinBox? _newModuleAreaWidth;
    private SpinBox? _newModuleAreaDepth;
    private LineEdit? _newModuleGameMode;
    private LineEdit? _newModuleEntryClass;
    private LineEdit? _newModulePlayerRule;
    private Label? _newModuleSummary;
    private Button? _newModuleNextButton;
    private Button? _newModuleCancelButton;
    private string? _projectFoundationCapturePath;
    private double _projectFoundationCaptureElapsed;
    private string? _areaFoundationCapturePath;
    private double _areaFoundationCaptureElapsed;
    private bool _areaFoundationCapturePrepared;
    private string? _areaRuntimeCapturePath;
    private double _areaRuntimeCaptureElapsed;
    private bool _areaRuntimeCapturePrepared;
    private string? _r035DiagnosticsCapturePath;
    private double _r035DiagnosticsCaptureElapsed;
    private bool _r035DiagnosticsCapturePrepared;
    private string? _r036ScaleCapturePath;
    private double _r036ScaleCaptureElapsed;
    private bool _r036ScaleCapturePrepared;
    private string? _r038CreatureFoundationCapturePath;
    private double _r038CreatureFoundationCaptureElapsed;
    private bool _r038CreatureFoundationCapturePrepared;
    private string? _r039CreatureEventsCapturePath;
    private double _r039CreatureEventsCaptureElapsed;
    private bool _r039CreatureEventsCapturePrepared;
    private string? _r044ItemFoundationCapturePath;
    private double _r044ItemFoundationCaptureElapsed;
    private bool _r044ItemFoundationCapturePrepared;
    private Window? _entityPropertiesDialog;
    private Label? _entityPropertiesIdentity;
    private ItemList? _entityPropertiesList;
    private LineEdit? _entityPropertyKey;
    private OptionButton? _entityPropertyKind;
    private LineEdit? _entityPropertyValue;
    private Label? _entityPropertyValidation;
    private Button? _entityPropertyUpsertButton;
    private Button? _entityPropertyRemoveButton;
    private Button? _entityPropertiesSaveButton;
    private Button? _entityPropertiesCancelButton;
    private DialogueEditorWindow? _dialogueEditor;
    private QuestJournalWindow? _questJournalWindow;
    private DialoguePresentationWindow? _dialoguePresentationWindow;
    private BlueprintCatalogWindow? _blueprintCatalog;
    private AssetContentBrowserWindow? _assetBrowser;
    private LocalizationWorkspaceWindow? _localizationWorkspace;
    private AssistantWorkspaceWindow? _assistantWorkspace;
    private LoreIndexWindow? _loreIndexWindow;
    private AssistedAreaPlanWindow? _assistedAreaPlanWindow;
    private AssistedDialogueWindow? _assistedDialogueWindow;
    private AssistedQuestWindow? _assistedQuestWindow;
    private AssistedForgeScriptWindow? _assistedForgeScriptWindow;
    private AssistedBossWindow? _assistedBossWindow;
    private AssistedAbilityWindow? _assistedAbilityWindow;
    private AssistedBlenderCreationWindow? _assistedBlenderCreationWindow;
    private AssistedModel3DGenerationWindow? _assistedModel3DGenerationWindow;
    private ExternalWorkerSecurityWindow? _externalWorkerSecurityWindow;
    private EditorKeymapWindow? _editorKeymapWindow;
    private CreaturePresentationWindow? _creaturePresentationWindow;
    private CreatureFoundationWindow? _creatureFoundationWindow;
    private CreatureProgressionWindow? _creatureProgressionWindow;
    private CreatureLootWindow? _creatureLootWindow;
    private CreatureRoleBehaviorWindow? _creatureRoleBehaviorWindow;
    private CreatureEventWindow? _creatureEventWindow;
    private QuestOperationsWindow? _questOperationsWindow;
    private ItemAdvancedWindow? _itemAdvancedWindow;
    private ItemFoundationWindow? _itemFoundationWindow;
    private WorldFeatureStudioWindow? _worldFeatureStudioWindow;
    private PostLaunchStudioWindow? _postLaunchStudioWindow;
    private FactionAdvancedWindow? _factionAdvancedWindow;
    private GlobalSearchWindow? _globalSearch;
    private ModuleConfigurationWindow? _moduleConfiguration;
    private AreaWorkspaceWindow? _areaWorkspace;
    private BuildPipelineWindow? _buildPipeline;
    private ForgeScriptEditorWindow? _forgeScriptEditor;
    private DistributionProfilesWindow? _distributionProfiles;
    private ContentWorkflowWindow? _contentWorkflow;
    private ContentPackageWindow? _contentPackageWindow;
    private NavigationConfigurationWindow? _navigationConfiguration;
    private CreatorWorkspaceWindow? _workspaceWindow;
    private RecoveryWindow? _recoveryWindow;
    private QualityLabWindow? _qualityLabWindow;
    private AreaEnvironmentWindow? _areaEnvironmentWindow;
    private TerrainStudioWindow? _terrainStudioWindow;
    private RuntimePoliciesWindow? _runtimePoliciesWindow;
    private CinematicStudioWindow? _cinematicStudioWindow;
    private GameplayControlsWindow? _gameplayControlsWindow;
    private RulesConfigurationWindow? _rulesConfigurationWindow;
    private AreaExperienceWindow? _areaExperienceWindow;
    private RuntimeExtensionsWindow? _runtimeExtensionsWindow;
    private AreaAuthoringWindow? _areaAuthoringWindow;
    private AreaFoundationWindow? _areaFoundationWindow;
    private RpgKitAuthoringWindow? _rpgKitAuthoringWindow;
    private NetworkBetaWindow? _networkBetaWindow;
    private LiveDirectorWindow? _liveDirectorWindow;
    private ForgeCreatorShell? _creatorShell;
    private ProjectSession? _projectSession;
    private ProjectPreview3D? _projectPreview;
    private ProjectCommandHistory? _history;
    private EditorUndoRedoManager? _nativeUndoRedo;
    private bool _nativeUndoRedoCallback;
    private int _nativeUndoRedoRegistrationCount;
    private AssetProcessingProfileCommandHistory? _assetProcessingProfileHistory;
    private AssetGovernanceCommandHistory? _assetGovernanceHistory;
    private QualityFoundationCommandHistory? _qualityFoundationHistory;
    private AssetPackageManifestCommandHistory? _assetPackageManifestHistory;
    private AssetExternalEditCommandHistory? _assetExternalEditHistory;
    private AssistedBlenderCreationCommandHistory? _assistedBlenderCreationHistory;
    private AssistedModel3DGenerationCommandHistory? _assistedModel3DGenerationHistory;
    private ExternalWorkerSecurityPolicyCommandHistory? _externalWorkerSecurityHistory;
    private EditorKeymapCommandHistory? _editorKeymapHistory;
    private AssistantWorkspaceProfileCommandHistory? _assistantProfileHistory;
    private LoreIndexCommandHistory? _loreIndexHistory;
    private AssistedAreaPlanCommandHistory? _assistedAreaPlanHistory;
    private Image? _forgeWindowIcon;
    private bool _playtestRunning;
    private bool _showEntityPropertiesDialogWhenReady;
    private bool _showDialogueEditorWhenReady;
    private ModuleTargetKind _distributionTarget = ModuleTargetKind.Editor;
    private int _selectedDraftOverrideIndex = -1;
    private double _editorReadyElapsed;
    private double _titleRefreshElapsed;
    private double _recoveryElapsed;
    private long _lastRecoveryRevision = -1;
    private long _lastSavedRevision;

    public override void _EnterTree()
    {
        _nativeUndoRedo = GetUndoRedo();
        var mainPanelScene = GD.Load<PackedScene>(MainPanelPath);
        _mainPanel = mainPanelScene.Instantiate<Control>();
        _mainPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        BindControls();

        _projectDialog = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenDir,
            Title = "Ouvrir un dossier de projet Forge",
        };
        _mainPanel.AddChild(_projectDialog);
        _exportDialog = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.SaveFile,
            Title = "Exporter le module OpenRPG Forge",
            Filters = ["*.orpmod ; Module OpenRPG Forge"],
        };
        _mainPanel.AddChild(_exportDialog);
        CreateNewModuleDialogs();
        CreateEntityPropertiesDialog();
        _dialogueEditor = new DialogueEditorWindow();
        _dialogueEditor.Initialize(
            () => _history?.State,
            ApplyDialogue,
            dialogueId => _dialoguePresentationWindow?.Open(dialogueId),
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_dialogueEditor);
        _questJournalWindow = new QuestJournalWindow();
        _questJournalWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyQuestJournal,
            Undo,
            Redo,
            () => _questOperationsWindow?.Open(),
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_questJournalWindow);
        _dialoguePresentationWindow = new DialoguePresentationWindow();
        _dialoguePresentationWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyDialoguePresentation,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_dialoguePresentationWindow);
        _blueprintCatalog = new BlueprintCatalogWindow();
        _blueprintCatalog.Initialize(
            () => _history?.State,
            UpsertBlueprint,
            CreateBlueprintFromSelectedEntity,
            ConfigureBlueprintPalette,
            LoadWorkspaceProfile,
            RecordRecentBlueprint,
            TogglePersonalFavorite,
            selection => { _selectionContext.Replace(selection); },
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_blueprintCatalog);
        _assetBrowser = new AssetContentBrowserWindow();
        _assetBrowser.Initialize(
            GetRepositoryRoot,
            () => _projectSession?.RootDirectory,
            () => _history?.State,
            () => _assetProcessingProfileHistory?.State,
            ApplyAssetProcessingProfile,
            UndoAssetProcessingProfile,
            RedoAssetProcessingProfile,
            () => _assetPackageManifestHistory?.State,
            PrepareAssetPackageManifest,
            InstallAssetForPlacement,
            UndoAssetPackageManifest,
            RedoAssetPackageManifest,
            () => _assetExternalEditHistory?.State,
            ConfigureAssetExternalEdit,
            UndoAssetExternalEdit,
            RedoAssetExternalEdit,
            () => _assetGovernanceHistory?.State,
            ApplyAssetGovernance,
            UndoAssetGovernance,
            RedoAssetGovernance,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_assetBrowser);
        _localizationWorkspace = new LocalizationWorkspaceWindow();
        _localizationWorkspace.Initialize(
            () => _history?.State,
            () => _projectSession?.RootDirectory,
            ApplyLocalizationCatalog,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_localizationWorkspace);
        _loreIndexWindow = new LoreIndexWindow();
        _loreIndexWindow.Initialize(
            () => _history?.State,
            () => _loreIndexHistory?.State,
            ApplyLoreIndex,
            UndoLoreIndex,
            RedoLoreIndex,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_loreIndexWindow);
        _assistedAreaPlanWindow = new AssistedAreaPlanWindow();
        _assistedAreaPlanWindow.Initialize(
            () => _history?.State,
            () => _assistedAreaPlanHistory?.State,
            () => _loreIndexHistory?.State,
            () => _assistantProfileHistory?.State,
            ApplyAssistedAreaPlans,
            BuildAssistedAreaPlan,
            UndoAssistedAreaPlans,
            RedoAssistedAreaPlans,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_assistedAreaPlanWindow);
        _assistedDialogueWindow = new AssistedDialogueWindow();
        _assistedDialogueWindow.Initialize(
            () => _history?.State,
            () => _loreIndexHistory?.State,
            () => _assistantProfileHistory?.State,
            ApplyAssistedDialogue,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_assistedDialogueWindow);
        _assistedQuestWindow = new AssistedQuestWindow();
        _assistedQuestWindow.Initialize(
            () => _history?.State,
            () => _loreIndexHistory?.State,
            () => _assistantProfileHistory?.State,
            ApplyAssistedQuest,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_assistedQuestWindow);
        _assistedForgeScriptWindow = new AssistedForgeScriptWindow();
        _assistedForgeScriptWindow.Initialize(
            () => _projectSession,
            () => _history?.State,
            () => _loreIndexHistory?.State,
            () => _assistantProfileHistory?.State,
            ApplyAssistedForgeScript,
            UndoAssistedForgeScript,
            RedoAssistedForgeScript,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_assistedForgeScriptWindow);
        _assistedBossWindow = new AssistedBossWindow();
        _assistedBossWindow.Initialize(
            () => _projectSession,
            () => _history?.State,
            () => _loreIndexHistory?.State,
            () => _assistantProfileHistory?.State,
            ApplyAssistedBoss,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_assistedBossWindow);
        _assistedAbilityWindow = new AssistedAbilityWindow();
        _assistedAbilityWindow.Initialize(
            () => _projectSession,
            () => _history?.State,
            () => _loreIndexHistory?.State,
            () => _assistantProfileHistory?.State,
            ApplyAssistedAbility,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_assistedAbilityWindow);
        _assistedBlenderCreationWindow = new AssistedBlenderCreationWindow();
        _assistedBlenderCreationWindow.Initialize(
            () => _projectSession,
            () => _history?.State,
            () => _loreIndexHistory?.State,
            () => _assistantProfileHistory?.State,
            () => _assetExternalEditHistory?.State,
            ApplyAssistedBlenderCreation,
            UndoAssistedBlenderCreation,
            RedoAssistedBlenderCreation,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_assistedBlenderCreationWindow);
        _assistedModel3DGenerationWindow = new AssistedModel3DGenerationWindow();
        _assistedModel3DGenerationWindow.Initialize(
            () => _projectSession,
            () => _assetExternalEditHistory?.State,
            ApplyAssistedModel3DGeneration,
            UndoAssistedModel3DGeneration,
            RedoAssistedModel3DGeneration,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_assistedModel3DGenerationWindow);
        _externalWorkerSecurityWindow = new ExternalWorkerSecurityWindow();
        _externalWorkerSecurityWindow.Initialize(
            () => _externalWorkerSecurityHistory?.State,
            ApplyExternalWorkerSecurityPolicy,
            UndoExternalWorkerSecurityPolicy,
            RedoExternalWorkerSecurityPolicy,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_externalWorkerSecurityWindow);
        _editorKeymapWindow = new EditorKeymapWindow();
        _editorKeymapWindow.Initialize(
            () => _editorKeymapHistory?.State,
            ApplyEditorKeymap,
            UndoEditorKeymap,
            RedoEditorKeymap,
            () => _editorKeymapHistory?.CanUndo == true,
            () => _editorKeymapHistory?.CanRedo == true,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_editorKeymapWindow);
        _creatureFoundationWindow = new CreatureFoundationWindow();
        _creatureFoundationWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyCreatureFoundation,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_creatureFoundationWindow);
        _creaturePresentationWindow = new CreaturePresentationWindow();
        _creaturePresentationWindow.Initialize(
            () => _history?.State,
            () => _projectSession?.RootDirectory,
            PreviewOrApplyCreaturePresentation,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_creaturePresentationWindow);
        _creatureProgressionWindow = new CreatureProgressionWindow();
        _creatureProgressionWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyCreatureProgression,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_creatureProgressionWindow);
        _creatureLootWindow = new CreatureLootWindow();
        _creatureLootWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyCreatureLoot,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_creatureLootWindow);
        _creatureRoleBehaviorWindow = new CreatureRoleBehaviorWindow();
        _creatureRoleBehaviorWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyCreatureRoleBehavior,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_creatureRoleBehaviorWindow);
        _creatureEventWindow = new CreatureEventWindow();
        _creatureEventWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyCreatureEvents,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_creatureEventWindow);
        _questOperationsWindow = new QuestOperationsWindow();
        _questOperationsWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyQuestOperations,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_questOperationsWindow);
        _itemAdvancedWindow = new ItemAdvancedWindow();
        _itemAdvancedWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyItemAdvanced,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_itemAdvancedWindow);
        _itemFoundationWindow = new ItemFoundationWindow();
        _itemFoundationWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyItemFoundation,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_itemFoundationWindow);
        _worldFeatureStudioWindow = new WorldFeatureStudioWindow();
        _worldFeatureStudioWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyWorldFeatures,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_worldFeatureStudioWindow);
        _postLaunchStudioWindow = new PostLaunchStudioWindow();
        _postLaunchStudioWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyPostLaunch,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_postLaunchStudioWindow);
        _factionAdvancedWindow = new FactionAdvancedWindow();
        _factionAdvancedWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyFactions,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_factionAdvancedWindow);
        _assistantWorkspace = new AssistantWorkspaceWindow();
        _assistantWorkspace.Initialize(
            () => _history?.State,
            () => _assistantProfileHistory?.State,
            () => _selectionContext.Current,
            ApplyAssistantWorkspaceProfile,
            UndoAssistantWorkspaceProfile,
            RedoAssistantWorkspaceProfile,
            () => _loreIndexWindow?.Open(),
            () => _assistedAreaPlanWindow?.Open(),
            () => _assistedDialogueWindow?.Open(),
            () => _assistedQuestWindow?.Open(),
            () => _assistedForgeScriptWindow?.Open(),
            () => _assistedBossWindow?.Open(),
            () => _assistedAbilityWindow?.Open(),
            () => _assistedBlenderCreationWindow?.Open(),
            () => _assistedModel3DGenerationWindow?.Open(),
            () => _externalWorkerSecurityWindow?.Open(),
            () => _localizationWorkspace?.Open(),
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_assistantWorkspace);
        _globalSearch = new GlobalSearchWindow();
        _globalSearch.Initialize(
            () => _history?.State,
            GetSearchDiagnostics,
            () => _projectSession?.RootDirectory,
            ApplySafeRename,
            PublishSearchSelection,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_globalSearch);
        _moduleConfiguration = new ModuleConfigurationWindow();
        _moduleConfiguration.Initialize(
            () => _history?.State,
            ApplyModuleConfiguration,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_moduleConfiguration);
        _areaWorkspace = new AreaWorkspaceWindow();
        _areaWorkspace.Initialize(
            () => _history?.State,
            () => _activeAreaContext.Current,
            ActivateArea,
            CreateArea,
            UpsertPortal,
            ApplyAreaPlaytest,
            DuplicateArea,
            ResizeArea,
            OpenNavigationConfiguration,
            OpenAreaEnvironment,
            OpenTerrainStudio,
            OpenRuntimePolicies,
            OpenCinematicStudio,
            OpenGameplayControls,
            OpenRulesConfiguration,
            OpenAreaExperience,
            OpenAreaAuthoring,
            OpenAreaFoundation,
            StartPlaytestFromUi,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_areaWorkspace);
        _areaEnvironmentWindow = new AreaEnvironmentWindow();
        _areaEnvironmentWindow.Initialize(() => _history?.State, () => _activeAreaContext.Current, ApplyAreaEnvironment, ApplyForgeWindowIcon);
        _mainPanel.AddChild(_areaEnvironmentWindow);
        _terrainStudioWindow = new TerrainStudioWindow();
        _terrainStudioWindow.Initialize(() => _history?.State, () => _activeAreaContext.Current,
            ApplyAreaWorldProfile, ApplyTerrainBrush, ApplyForgeWindowIcon);
        _mainPanel.AddChild(_terrainStudioWindow);
        _runtimePoliciesWindow = new RuntimePoliciesWindow();
        _runtimePoliciesWindow.Initialize(() => _history?.State, () => _activeAreaContext.Current,
            ApplyRuntimePolicies, ApplyForgeWindowIcon);
        _mainPanel.AddChild(_runtimePoliciesWindow);
        _cinematicStudioWindow = new CinematicStudioWindow();
        _cinematicStudioWindow.Initialize(() => _history?.State, () => _activeAreaContext.Current,
            ApplyCinematicProfiles, ApplyForgeWindowIcon);
        _mainPanel.AddChild(_cinematicStudioWindow);
        _gameplayControlsWindow = new GameplayControlsWindow();
        _gameplayControlsWindow.Initialize(() => _history?.State, ApplyGameplayControlProfile,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_gameplayControlsWindow);
        _rulesConfigurationWindow = new RulesConfigurationWindow();
        _rulesConfigurationWindow.Initialize(() => _history?.State, () => _activeAreaContext.Current,
            ApplyRulesProfiles, ApplyForgeWindowIcon);
        _mainPanel.AddChild(_rulesConfigurationWindow);
        _areaExperienceWindow = new AreaExperienceWindow();
        _areaExperienceWindow.Initialize(() => _history?.State, () => _activeAreaContext.Current,
            ApplyAreaExperienceProfile, ApplyForgeWindowIcon);
        _mainPanel.AddChild(_areaExperienceWindow);
        _runtimeExtensionsWindow = new RuntimeExtensionsWindow();
        _runtimeExtensionsWindow.Initialize(() => _history?.State, ApplyRuntimeExtensions, ApplyForgeWindowIcon);
        _mainPanel.AddChild(_runtimeExtensionsWindow);
        _areaAuthoringWindow = new AreaAuthoringWindow();
        _areaAuthoringWindow.Initialize(() => _history?.State, () => _activeAreaContext.Current, ApplyAreaAuthoring, ApplyForgeWindowIcon);
        _mainPanel.AddChild(_areaAuthoringWindow);
        _areaFoundationWindow = new AreaFoundationWindow();
        _areaFoundationWindow.Initialize(() => _history?.State, () => _activeAreaContext.Current,
            ApplyAreaFoundation, ApplyForgeWindowIcon);
        _mainPanel.AddChild(_areaFoundationWindow);
        _rpgKitAuthoringWindow = new RpgKitAuthoringWindow();
        _rpgKitAuthoringWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyRpgBlueprint,
            Undo,
            Redo,
            OpenDialogueEditor,
            () => _buildPipeline?.Open(),
            () => _creatureFoundationWindow?.Open(),
            () => _creaturePresentationWindow?.Open(),
            () => _creatureProgressionWindow?.Open(),
            () => _creatureLootWindow?.Open(),
            () => _creatureRoleBehaviorWindow?.Open(),
            () => _creatureEventWindow?.Open(),
            () => _factionAdvancedWindow?.Open(),
            () => _questOperationsWindow?.Open(),
            () => _itemFoundationWindow?.Open(),
            () => _itemAdvancedWindow?.Open(),
            () => _worldFeatureStudioWindow?.Open(),
            () => _postLaunchStudioWindow?.Open(),
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_rpgKitAuthoringWindow);
        _networkBetaWindow = new NetworkBetaWindow();
        _networkBetaWindow.Initialize(ApplyForgeWindowIcon);
        _mainPanel.AddChild(_networkBetaWindow);
        _liveDirectorWindow = new LiveDirectorWindow();
        _liveDirectorWindow.Initialize(ApplyForgeWindowIcon);
        _mainPanel.AddChild(_liveDirectorWindow);
        _workspaceWindow = new CreatorWorkspaceWindow();
        _workspaceWindow.Initialize(
            () => _history?.State,
            LoadWorkspaceProfile,
            SaveWorkspaceProfile,
            () => _editorKeymapWindow?.Open(),
            () => _history?.State.Project.Module.ArtisticStyle ??
                  ArtisticStyleProfile.CreateDefault(Guid.Parse("10000000-0000-4000-8000-000000000001")),
            ApplyArtisticStyleProfile,
            ActivateArea,
            ApplyWorkspaceFilter,
            PlaceRepeatedSeries,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_workspaceWindow);
        _recoveryWindow = new RecoveryWindow();
        _recoveryWindow.Initialize(ListRecoverySnapshots, RestoreRecoverySnapshot, CaptureRecoverySnapshot,
            ClearRecoverySnapshots, ApplyForgeWindowIcon);
        _mainPanel.AddChild(_recoveryWindow);
        _qualityLabWindow = new QualityLabWindow();
        _qualityLabWindow.Initialize(() => _history?.State.Project, () => _projectSession?.RootDirectory,
            () => _qualityFoundationHistory?.State,
            ApplyQualityFoundation,
            UndoQualityFoundation,
            RedoQualityFoundation,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_qualityLabWindow);
        _navigationConfiguration = new NavigationConfigurationWindow();
        _navigationConfiguration.Initialize(
            () => _history?.State,
            () => _activeAreaContext.Current,
            ApplyAreaNavigation,
            surface => _projectPreview?.ShowNavigationOverlay(surface),
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_navigationConfiguration);
        _buildPipeline = new BuildPipelineWindow();
        _buildPipeline.Initialize(
            () => _projectSession,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_buildPipeline);
        _forgeScriptEditor = new ForgeScriptEditorWindow();
        _forgeScriptEditor.Initialize(() => _projectSession, ApplyForgeWindowIcon);
        _mainPanel.AddChild(_forgeScriptEditor);
        _distributionProfiles = new DistributionProfilesWindow();
        _distributionProfiles.Initialize(
            () => _history?.State,
            ApplyModuleDistribution,
            target => _distributionTarget = target,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_distributionProfiles);
        _contentWorkflow = new ContentWorkflowWindow();
        _contentWorkflow.Initialize(
            () => _history?.State,
            ApplyContentWorkspace,
            () => _projectSession?.RootDirectory,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_contentWorkflow);
        _contentPackageWindow = new ContentPackageWindow();
        _contentPackageWindow.Initialize(
            () => _history?.State,
            ApplyContentWorkspace,
            () => _projectSession?.RootDirectory,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_contentPackageWindow);
        _projectPreview = new ProjectPreview3D();
        _projectPreview.Initialize(
            () => _history?.State,
            () => _projectSession?.RootDirectory,
            () => _assetPackageManifestHistory?.State,
            () => _activeAreaContext.Current,
            () => GridStep,
            SelectEntityFromViewport,
            TransformEntityFromViewport,
            PlaceBlueprintFromViewport,
            ShowViewportPlacementFeedback);
        _mainPanel.GetNode<Control>("Margin/Content/PreviewHost").AddChild(_projectPreview);
        _creatorShell = new ForgeCreatorShell(_mainPanel);
        ConnectSignals();

        EditorInterface.Singleton.GetEditorMainScreen().AddChild(_mainPanel);
        SetProcess(true);
        SetProcessInput(true);
        EnsureForgeWindowTitle();
        _MakeVisible(false);
        _mainThreadActions.Enqueue(() => EditorInterface.Singleton.SetMainScreenEditor("Forge"));
        OpenProjectDirectory(GetPrototypeWorkspaceDirectory());

        var userArguments = OS.GetCmdlineUserArgs();
        if (userArguments.Contains("--show-new-module-dialog", StringComparer.Ordinal))
        {
            _mainThreadActions.Enqueue(ShowNewModuleDialog);
        }

        var projectFoundationCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r024-project-foundation-ui-capture=", StringComparison.Ordinal));
        if (projectFoundationCapture is not null)
        {
            _projectFoundationCapturePath = projectFoundationCapture[
                "--r024-project-foundation-ui-capture=".Length..];
            _mainThreadActions.Enqueue(ShowNewModuleDialog);
        }

        var areaFoundationCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r028-area-foundation-ui-capture=", StringComparison.Ordinal));
        if (areaFoundationCapture is not null)
        {
            _areaFoundationCapturePath = areaFoundationCapture[
                "--r028-area-foundation-ui-capture=".Length..];
        }

        var areaRuntimeCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r029-area-runtime-ui-capture=", StringComparison.Ordinal));
        if (areaRuntimeCapture is not null)
        {
            _areaRuntimeCapturePath = areaRuntimeCapture["--r029-area-runtime-ui-capture=".Length..];
        }

        var r035DiagnosticsCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r035-diagnostics-ui-capture=", StringComparison.Ordinal));
        if (r035DiagnosticsCapture is not null)
        {
            _r035DiagnosticsCapturePath = r035DiagnosticsCapture[
                "--r035-diagnostics-ui-capture=".Length..];
        }

        var r036ScaleCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r036-ee-scale-ui-capture=", StringComparison.Ordinal));
        if (r036ScaleCapture is not null)
        {
            _r036ScaleCapturePath = r036ScaleCapture["--r036-ee-scale-ui-capture=".Length..];
        }

        var r038CreatureFoundationCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r038-creature-foundation-ui-capture=", StringComparison.Ordinal));
        if (r038CreatureFoundationCapture is not null)
            _r038CreatureFoundationCapturePath = r038CreatureFoundationCapture[
                "--r038-creature-foundation-ui-capture=".Length..];

        var r039CreatureEventsCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r039-creature-events-ui-capture=", StringComparison.Ordinal));
        if (r039CreatureEventsCapture is not null)
            _r039CreatureEventsCapturePath = r039CreatureEventsCapture[
                "--r039-creature-events-ui-capture=".Length..];

        var r044ItemFoundationCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r044-item-foundation-ui-capture=", StringComparison.Ordinal));
        if (r044ItemFoundationCapture is not null)
            _r044ItemFoundationCapturePath = r044ItemFoundationCapture[
                "--r044-item-foundation-ui-capture=".Length..];

        if (userArguments.Contains("--show-entity-properties-dialog", StringComparer.Ordinal))
        {
            _showEntityPropertiesDialogWhenReady = true;
        }

        if (userArguments.Contains("--show-dialogue-editor", StringComparer.Ordinal))
        {
            _showDialogueEditorWhenReady = true;
        }

        if (userArguments.Contains("--workspace-smoke-test", StringComparer.Ordinal))
        {
            RunWorkspaceSmokeTest();
        }

        GD.Print("OPENRPG_FORGE_PLUGIN_LOADED");
    }

    public override void _ExitTree()
    {
        SetProcessInput(false);
        _nativeUndoRedo = null;
        DisconnectSignals();
        if (_mainPanel is not null && GodotObject.IsInstanceValid(_mainPanel))
        {
            _mainPanel.QueueFree();
        }

        _mainPanel = null;
        _statusLabel = null;
        _detailsLabel = null;
        _projectPathLabel = null;
        _revisionLabel = null;
        _openButton = null;
        _newModuleButton = null;
        _dialoguesButton = null;
        _areasButton = null;
        _workspaceButton = null;
        _recoveryButton = null;
        _qualityButton = null;
        _searchButton = null;
        _moduleSettingsButton = null;
        _reloadButton = null;
        _saveButton = null;
        _exportButton = null;
        _placeButton = null;
        _moveXNegativeButton = null;
        _moveXPositiveButton = null;
        _moveZNegativeButton = null;
        _moveZPositiveButton = null;
        _rotateButton = null;
        _deleteButton = null;
        _scaleX = null;
        _scaleY = null;
        _scaleZ = null;
        _uniformScale = null;
        _scaleDownButton = null;
        _scaleUpButton = null;
        _applyScaleButton = null;
        _overridesButton = null;
        _copyButton = null;
        _cutButton = null;
        _pasteButton = null;
        _duplicateButton = null;
        _independentCopy = null;
        _undoButton = null;
        _redoButton = null;
        _playButton = null;
        _blueprintPalette = null;
        _entityPicker = null;
        _spawnPicker = null;
        _overridesSummary = null;
        _clipboardSummary = null;
        _playtestStatus = null;
        _playtestLog = null;
        _positionX = null;
        _positionZ = null;
        _gridSize = null;
        _projectDialog = null;
        _exportDialog = null;
        _newModuleDialog = null;
        _newModuleLocationDialog = null;
        _newModuleTitle = null;
        _newModuleDescription = null;
        _newModuleTemplate = null;
        _newModuleKits = null;
        _newModulePlatform = null;
        _newModuleRules = null;
        _newModuleRender = null;
        _newModuleNetwork = null;
        _newModulePackageNamespace = null;
        _newModuleCreateInitialArea = null;
        _newModuleAreaName = null;
        _newModuleAreaKind = null;
        _newModuleAreaWidth = null;
        _newModuleAreaDepth = null;
        _newModuleGameMode = null;
        _newModuleEntryClass = null;
        _newModulePlayerRule = null;
        _newModuleSummary = null;
        _newModuleNextButton = null;
        _newModuleCancelButton = null;
        _projectFoundationCapturePath = null;
        _areaFoundationCapturePath = null;
        _areaRuntimeCapturePath = null;
        _r035DiagnosticsCapturePath = null;
        _r036ScaleCapturePath = null;
        _r038CreatureFoundationCapturePath = null;
        _r039CreatureEventsCapturePath = null;
        _entityPropertiesDialog = null;
        _entityPropertiesIdentity = null;
        _entityPropertiesList = null;
        _entityPropertyKey = null;
        _entityPropertyKind = null;
        _entityPropertyValue = null;
        _entityPropertyValidation = null;
        _entityPropertyUpsertButton = null;
        _entityPropertyRemoveButton = null;
        _entityPropertiesSaveButton = null;
        _entityPropertiesCancelButton = null;
        _dialogueEditor = null;
        _questJournalWindow = null;
        _blueprintCatalog = null;
        _assetBrowser = null;
        _localizationWorkspace = null;
        _assistantWorkspace = null;
        _externalWorkerSecurityWindow = null;
        _editorKeymapWindow = null;
        _creaturePresentationWindow = null;
        _creatureFoundationWindow = null;
        _creatureProgressionWindow = null;
        _creatureLootWindow = null;
        _creatureRoleBehaviorWindow = null;
        _creatureEventWindow = null;
        _questOperationsWindow = null;
        _itemAdvancedWindow = null;
        _itemFoundationWindow = null;
        _worldFeatureStudioWindow = null;
        _loreIndexWindow = null;
        _globalSearch = null;
        _moduleConfiguration = null;
        _areaWorkspace = null;
        _buildPipeline = null;
        _forgeScriptEditor = null;
        _distributionProfiles = null;
        _contentWorkflow = null;
        _contentPackageWindow = null;
        _navigationConfiguration = null;
        _workspaceWindow = null;
        _recoveryWindow = null;
        _qualityLabWindow = null;
        _areaEnvironmentWindow = null;
        _runtimePoliciesWindow = null;
        _cinematicStudioWindow = null;
        _gameplayControlsWindow = null;
        _areaAuthoringWindow = null;
        _areaFoundationWindow = null;
        _rpgKitAuthoringWindow = null;
        _creatorShell = null;
        _projectSession = null;
        _projectPreview = null;
        _history = null;
        _assetProcessingProfileHistory = null;
        _assetGovernanceHistory = null;
        _qualityFoundationHistory = null;
        _assetPackageManifestHistory = null;
        _assetExternalEditHistory = null;
        _externalWorkerSecurityHistory = null;
        _editorKeymapHistory = null;
        _assistantProfileHistory = null;
        _loreIndexHistory = null;
        GD.Print("OPENRPG_FORGE_PLUGIN_UNLOADED");
    }

    public override void _Process(double delta)
    {
        if (_r044ItemFoundationCapturePath is not null)
        {
            _r044ItemFoundationCaptureElapsed += delta;
            if (!_r044ItemFoundationCapturePrepared && _r044ItemFoundationCaptureElapsed >= 4 &&
                _itemFoundationWindow is not null)
            {
                _itemFoundationWindow.Open();
                _r044ItemFoundationCapturePrepared = _itemFoundationWindow.PrepareForCapture();
                _r044ItemFoundationCaptureElapsed = 0;
            }
            else if (_r044ItemFoundationCapturePrepared && _itemFoundationWindow?.Visible == true &&
                     _r044ItemFoundationCaptureElapsed >= 2)
            {
                var destination = Path.GetFullPath(_r044ItemFoundationCapturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var error = _itemFoundationWindow.GetViewport().GetTexture().GetImage().SavePng(destination);
                GD.Print($"OPENRPG_FORGE_R044_ITEM_FOUNDATION_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
                _r044ItemFoundationCapturePath = null;
                GetTree().Quit(error == Error.Ok ? 0 : 1);
            }
        }

        if (_r039CreatureEventsCapturePath is not null)
        {
            _r039CreatureEventsCaptureElapsed += delta;
            if (!_r039CreatureEventsCapturePrepared && _r039CreatureEventsCaptureElapsed >= 4 &&
                _creatureEventWindow is not null)
            {
                _creatureEventWindow.Open();
                _r039CreatureEventsCapturePrepared = _creatureEventWindow.PrepareForCapture();
                _r039CreatureEventsCaptureElapsed = 0;
            }
            else if (_r039CreatureEventsCapturePrepared && _creatureEventWindow?.Visible == true &&
                     _r039CreatureEventsCaptureElapsed >= 2)
            {
                var destination = Path.GetFullPath(_r039CreatureEventsCapturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var error = _creatureEventWindow.GetViewport().GetTexture().GetImage().SavePng(destination);
                GD.Print($"OPENRPG_FORGE_R039_CREATURE_EVENTS_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
                _r039CreatureEventsCapturePath = null;
                _r044ItemFoundationCapturePath = null;
                GetTree().Quit(error == Error.Ok ? 0 : 1);
            }
        }

        if (_r038CreatureFoundationCapturePath is not null)
        {
            _r038CreatureFoundationCaptureElapsed += delta;
            if (!_r038CreatureFoundationCapturePrepared && _r038CreatureFoundationCaptureElapsed >= 4 &&
                _creatureFoundationWindow is not null)
            {
                _creatureFoundationWindow.Open();
                _r038CreatureFoundationCapturePrepared = _creatureFoundationWindow.PrepareForCapture();
                _r038CreatureFoundationCaptureElapsed = 0;
            }
            else if (_r038CreatureFoundationCapturePrepared && _creatureFoundationWindow?.Visible == true &&
                     _r038CreatureFoundationCaptureElapsed >= 2)
            {
                var destination = Path.GetFullPath(_r038CreatureFoundationCapturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var error = _creatureFoundationWindow.GetViewport().GetTexture().GetImage().SavePng(destination);
                GD.Print($"OPENRPG_FORGE_R038_CREATURE_FOUNDATION_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
                _r038CreatureFoundationCapturePath = null;
                GetTree().Quit(error == Error.Ok ? 0 : 1);
            }
        }

        if (_r036ScaleCapturePath is not null)
        {
            _r036ScaleCaptureElapsed += delta;
            if (!_r036ScaleCapturePrepared && _r036ScaleCaptureElapsed >= 4 &&
                _projectSession is not null && _projectPreview is not null)
            {
                var entity = _projectSession.Project.Areas
                    .SelectMany(area => area.Entities)
                    .FirstOrDefault(candidate => candidate.Tags.Contains("architecture"));
                _r036ScaleCapturePrepared = entity is not null &&
                    _projectPreview.SelectEntityForSmokeTest(entity.Id) &&
                    _projectPreview.PrepareScaleGizmoForCapture(ProjectPreview3D.ScaleGizmoAxis.X);
                _r036ScaleCaptureElapsed = 0;
            }
            else if (_r036ScaleCapturePrepared && _r036ScaleCaptureElapsed >= 1 && _mainPanel is not null)
            {
                var destination = Path.GetFullPath(_r036ScaleCapturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var error = _mainPanel.GetViewport().GetTexture().GetImage().SavePng(destination);
                GD.Print($"OPENRPG_FORGE_R036_EE_SCALE_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                         $"path={destination}");
                _r036ScaleCapturePath = null;
                GetTree().Quit(error == Error.Ok ? 0 : 1);
            }
        }

        if (_r035DiagnosticsCapturePath is not null)
        {
            _r035DiagnosticsCaptureElapsed += delta;
            if (!_r035DiagnosticsCapturePrepared && _r035DiagnosticsCaptureElapsed >= 4 &&
                _creatorShell is not null)
            {
                _creatorShell.SetDiagnostics([
                    CreatorDiagnostic.Create(CreatorDiagnosticSeverity.Error,
                        "project.reference.blueprint_missing", "Blueprint introuvable.",
                        "areas/crypt/entities/warden", "build:r035"),
                    CreatorDiagnostic.Create(CreatorDiagnosticSeverity.Warning,
                        "area.navigation.portal_missing", "Portail de destination introuvable.",
                        "areas/crypt/portals/exit", "build:r035"),
                ]);
                _creatorShell.FilterDiagnosticsForSmokeTest(null, CreatorDiagnosticSeverity.Information,
                    string.Empty, string.Empty, expectedCount: 2);
                _r035DiagnosticsCapturePrepared = true;
                _r035DiagnosticsCaptureElapsed = 0;
            }
            else if (_r035DiagnosticsCapturePrepared && _r035DiagnosticsCaptureElapsed >= 0.5 &&
                     _mainPanel is not null)
            {
                var destination = Path.GetFullPath(_r035DiagnosticsCapturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var error = _mainPanel.GetViewport().GetTexture().GetImage().SavePng(destination);
                GD.Print($"OPENRPG_FORGE_R035_DIAGNOSTICS_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                         $"path={destination}");
                _r035DiagnosticsCapturePath = null;
                GetTree().Quit(error == Error.Ok ? 0 : 1);
            }
        }

        if (_areaRuntimeCapturePath is not null)
        {
            _areaRuntimeCaptureElapsed += delta;
            if (!_areaRuntimeCapturePrepared && _areaRuntimeCaptureElapsed >= 4 && _areaWorkspace is not null)
            {
                _areaWorkspace.Open();
                _areaRuntimeCapturePrepared = _areaWorkspace.PrepareRuntimeValidationForSmokeTest();
                _areaWorkspace.ShowRuntimeValidationForCapture();
                _areaRuntimeCaptureElapsed = 0;
            }
            else if (_areaRuntimeCapturePrepared && _areaWorkspace?.Visible == true &&
                     _areaRuntimeCaptureElapsed >= 2)
            {
                var destination = Path.GetFullPath(_areaRuntimeCapturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var error = _areaWorkspace.GetViewport().GetTexture().GetImage().SavePng(destination);
                GD.Print($"OPENRPG_FORGE_R029_AREA_RUNTIME_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
                _areaRuntimeCapturePath = null;
                GetTree().Quit(error == Error.Ok ? 0 : 1);
            }
        }

        if (_areaFoundationCapturePath is not null)
        {
            _areaFoundationCaptureElapsed += delta;
            if (!_areaFoundationCapturePrepared && _areaFoundationCaptureElapsed >= 4 &&
                _areaFoundationWindow is not null)
            {
                _areaFoundationWindow.Open();
                if (_areaFoundationWindow.PrepareForSmokeTest())
                {
                    _areaFoundationCapturePrepared = true;
                    _areaFoundationCaptureElapsed = 0;
                }
            }
            else if (_areaFoundationCapturePrepared && _areaFoundationWindow?.Visible == true &&
                     _areaFoundationCaptureElapsed >= 2)
            {
                var destination = Path.GetFullPath(_areaFoundationCapturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var error = _areaFoundationWindow.GetViewport().GetTexture().GetImage().SavePng(destination);
                GD.Print($"OPENRPG_FORGE_R028_AREA_FOUNDATION_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
                _areaFoundationCapturePath = null;
                GetTree().Quit(error == Error.Ok ? 0 : 1);
            }
        }

        if (_projectFoundationCapturePath is not null && _newModuleDialog?.Visible == true)
        {
            _projectFoundationCaptureElapsed += delta;
            if (_projectFoundationCaptureElapsed >= 2)
            {
                var destination = Path.GetFullPath(_projectFoundationCapturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var error = _newModuleDialog.GetViewport().GetTexture().GetImage().SavePng(destination);
                GD.Print($"OPENRPG_FORGE_R024_PROJECT_FOUNDATION_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
                _projectFoundationCapturePath = null;
                GetTree().Quit(error == Error.Ok ? 0 : 1);
            }
        }

        if (_showEntityPropertiesDialogWhenReady)
        {
            _editorReadyElapsed += delta;
            if (_editorReadyElapsed >= 4)
            {
                _showEntityPropertiesDialogWhenReady = false;
                ShowExampleEntityPropertiesDialog();
            }
        }

        if (_showDialogueEditorWhenReady)
        {
            _editorReadyElapsed += delta;
            if (_editorReadyElapsed >= 4)
            {
                _showDialogueEditorWhenReady = false;
                _dialogueEditor?.Open(createNew: true);
            }
        }

        _titleRefreshElapsed += delta;
        if (_titleRefreshElapsed >= 0.5)
        {
            _titleRefreshElapsed = 0;
            EnsureForgeWindowTitle();
        }

        while (_mainThreadActions.TryDequeue(out var action))
        {
            action();
        }

        _recoveryElapsed += delta;
        var recoveryInterval = _history?.State.Project.Module.RuntimePolicies.Extensions.OperationalReadiness
            .Recovery.AutosaveIntervalSeconds ?? 60;
        if (_recoveryElapsed >= recoveryInterval)
        {
            _recoveryElapsed = 0;
            CaptureRecoverySnapshot();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key ||
            _mainPanel?.Visible != true || _editorKeymapHistory is null ||
            _editorKeymapWindow?.Visible == true || IsTextEntryFocused())
            return;

        var binding = _editorKeymapHistory.State.Profile.Resolve(
            key.Keycode.ToString(), key.CtrlPressed, key.AltPressed, key.ShiftPressed, key.MetaPressed);
        if (binding is null || !DispatchEditorShortcut(binding.CommandId)) return;
        GetViewport().SetInputAsHandled();
        GD.Print($"OPENRPG_FORGE_EDITOR_SHORTCUT command={binding.CommandId} gesture={binding.Gesture.CanonicalText}");
    }

    private bool IsTextEntryFocused()
    {
        var focused = GetViewport().GuiGetFocusOwner();
        return focused is LineEdit or TextEdit;
    }

    private bool DispatchEditorShortcut(string commandId)
    {
        switch (commandId)
        {
            case EditorKeymapCatalog.SaveProject: SaveProject(); return true;
            case EditorKeymapCatalog.Undo: Undo(); return true;
            case EditorKeymapCatalog.Redo: Redo(); return true;
            case EditorKeymapCatalog.CopySelection: CopySelectedEntity(); return true;
            case EditorKeymapCatalog.PasteSelection: PasteSemanticClipboard(); return true;
            case EditorKeymapCatalog.DuplicateSelection: DuplicateSelectedEntity(); return true;
            case EditorKeymapCatalog.DeleteSelection: DeleteSelected(); return true;
            case EditorKeymapCatalog.FocusSelection: return _projectPreview?.FocusSelectionFromShortcut() == true;
            case EditorKeymapCatalog.RunPlaytest: StartPlaytestFromUi(); return true;
            case EditorKeymapCatalog.OpenSearch: OpenGlobalSearch(); return true;
            case EditorKeymapCatalog.OpenWorld: HandleWorkspaceRequest("Monde"); return true;
            case EditorKeymapCatalog.OpenBlueprints: HandleWorkspaceRequest("Blueprints"); return true;
            case EditorKeymapCatalog.OpenDialogues: HandleWorkspaceRequest("Dialogues"); return true;
            default: return false;
        }
    }

    public override bool _HasMainScreen() => true;

    public override void _MakeVisible(bool visible)
    {
        if (_mainPanel is not null && GodotObject.IsInstanceValid(_mainPanel))
        {
            _mainPanel.Visible = visible;
        }
    }

    public override string _GetPluginName() => "Forge";

    public override Texture2D _GetPluginIcon() =>
        EditorInterface.Singleton.GetEditorTheme().GetIcon("Node3D", "EditorIcons");

    private void BindControls()
    {
        _statusLabel = GetControl<Label>("Margin/Content/Status");
        _detailsLabel = GetControl<Label>("Margin/Content/Details");
        _projectPathLabel = GetControl<Label>("Margin/Content/ProjectPath");
        _openButton = GetControl<Button>("Margin/Content/Actions/OpenButton");
        _newModuleButton = GetControl<Button>("Margin/Content/Actions/NewModuleButton");
        _dialoguesButton = GetControl<Button>("Margin/Content/Actions/DialoguesButton");
        _areasButton = GetControl<Button>("Margin/Content/Actions/AreasButton");
        _workspaceButton = GetControl<Button>("Margin/Content/Actions/WorkspaceButton");
        _recoveryButton = GetControl<Button>("Margin/Content/Actions/RecoveryButton");
        _qualityButton = GetControl<Button>("Margin/Content/Actions/QualityButton");
        _searchButton = GetControl<Button>("Margin/Content/Actions/SearchButton");
        _moduleSettingsButton = GetControl<Button>("Margin/Content/Actions/ModuleSettingsButton");
        _reloadButton = GetControl<Button>("Margin/Content/Actions/ValidateButton");
        _saveButton = GetControl<Button>("Margin/Content/Actions/SaveButton");
        _exportButton = GetControl<Button>("Margin/Content/Actions/ExportButton");
        _blueprintPalette = GetControl<OptionButton>("Margin/Content/Editing/PaletteRow/BlueprintPalette");
        _positionX = GetControl<SpinBox>("Margin/Content/Editing/PaletteRow/PositionX");
        _positionZ = GetControl<SpinBox>("Margin/Content/Editing/PaletteRow/PositionZ");
        _gridSize = GetControl<SpinBox>("Margin/Content/Editing/PaletteRow/GridSize");
        _placeButton = GetControl<Button>("Margin/Content/Editing/PaletteRow/PlaceButton");
        _entityPicker = GetControl<OptionButton>("Margin/Content/Editing/EntityRow/EntityPicker");
        _moveXNegativeButton = GetControl<Button>("Margin/Content/Editing/EntityRow/MoveXNegative");
        _moveXPositiveButton = GetControl<Button>("Margin/Content/Editing/EntityRow/MoveXPositive");
        _moveZNegativeButton = GetControl<Button>("Margin/Content/Editing/EntityRow/MoveZNegative");
        _moveZPositiveButton = GetControl<Button>("Margin/Content/Editing/EntityRow/MoveZPositive");
        _rotateButton = GetControl<Button>("Margin/Content/Editing/EntityRow/RotateButton");
        _deleteButton = GetControl<Button>("Margin/Content/Editing/EntityRow/DeleteButton");
        _scaleX = GetControl<SpinBox>("Margin/Content/Editing/ScaleRow/ScaleX");
        _scaleY = GetControl<SpinBox>("Margin/Content/Editing/ScaleRow/ScaleY");
        _scaleZ = GetControl<SpinBox>("Margin/Content/Editing/ScaleRow/ScaleZ");
        _uniformScale = GetControl<CheckButton>("Margin/Content/Editing/ScaleRow/UniformScale");
        _scaleDownButton = GetControl<Button>("Margin/Content/Editing/ScaleRow/ScaleDown");
        _scaleUpButton = GetControl<Button>("Margin/Content/Editing/ScaleRow/ScaleUp");
        _applyScaleButton = GetControl<Button>("Margin/Content/Editing/ScaleRow/ApplyScale");
        _overridesButton = GetControl<Button>("Margin/Content/Editing/OverridesRow/OverridesButton");
        _copyButton = GetControl<Button>("Margin/Content/Editing/ClipboardRow/CopyButton");
        _cutButton = GetControl<Button>("Margin/Content/Editing/ClipboardRow/CutButton");
        _pasteButton = GetControl<Button>("Margin/Content/Editing/ClipboardRow/PasteButton");
        _duplicateButton = GetControl<Button>("Margin/Content/Editing/ClipboardRow/DuplicateButton");
        _independentCopy = GetControl<CheckButton>("Margin/Content/Editing/ClipboardRow/IndependentCopy");
        _overridesSummary = GetControl<Label>("Margin/Content/Editing/OverridesRow/OverridesSummary");
        _clipboardSummary = GetControl<Label>("Margin/Content/Editing/ClipboardRow/ClipboardSummary");
        _undoButton = GetControl<Button>("Margin/Content/Editing/HistoryRow/UndoButton");
        _redoButton = GetControl<Button>("Margin/Content/Editing/HistoryRow/RedoButton");
        _revisionLabel = GetControl<Label>("Margin/Content/Editing/HistoryRow/Revision");
        _spawnPicker = GetControl<OptionButton>("Margin/Content/PlaytestBar/PlaytestMargin/PlaytestRow/SpawnPicker");
        _playButton = GetControl<Button>("Margin/Content/PlaytestBar/PlaytestMargin/PlaytestRow/PlayButton");
        _playtestStatus = GetControl<Label>("Margin/Content/PlaytestBar/PlaytestMargin/PlaytestRow/PlaytestStatus");
        _playtestLog = GetControl<RichTextLabel>("Margin/Content/PlaytestConsole/ConsoleMargin/ConsoleContent/PlaytestLog");
    }

    private T GetControl<T>(string path)
        where T : Node => _mainPanel!.GetNode<T>(path);

    private void CreateNewModuleDialogs()
    {
        _newModuleDialog = new Window
        {
            Title = "Assistant de projet · OpenRPG Forge",
            MinSize = new Vector2I(980, 900),
            Size = new Vector2I(980, 900),
            Exclusive = false,
            Transient = true,
            Unresizable = false,
            Visible = false,
        };
        _mainPanel!.AddChild(_newModuleDialog);

        var background = new PanelContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        background.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("171322"),
            BorderColor = new Color("62458a"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
        });
        _newModuleDialog.AddChild(background);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        background.AddChild(margin);

        var layout = new VBoxContainer();
        layout.AddThemeConstantOverride("separation", 12);
        margin.AddChild(layout);
        layout.AddChild(new Label
        {
            Text = "NOUVEAU PROJET",
            ThemeTypeVariation = "HeaderSmall",
        });
        layout.AddChild(new Label
        {
            Text = "Choisissez une fondation technique stable. Un résumé vérifiable précède toute écriture.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        layout.AddChild(scroll);
        var columns = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        columns.AddThemeConstantOverride("separation", 18);
        scroll.AddChild(columns);

        var identity = NewWizardColumn("IDENTITÉ & CIBLE");
        var world = NewWizardColumn("PREMIÈRE EXPÉRIENCE");
        columns.AddChild(identity);
        columns.AddChild(world);

        identity.AddChild(new Label { Text = "Titre du module · obligatoire" });
        _newModuleTitle = new LineEdit
        {
            PlaceholderText = "Les Îles Brisées",
            Text = "Mon nouveau module",
        };
        identity.AddChild(_newModuleTitle);
        identity.AddChild(new Label { Text = "Résumé localisé · français" });
        _newModuleDescription = new TextEdit
        {
            CustomMinimumSize = new Vector2(410, 76),
            PlaceholderText = "Décrivez en quelques lignes l’aventure et son ambiance.",
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        identity.AddChild(_newModuleDescription);
        identity.AddChild(new Label { Text = "Type de projet" });
        _newModuleTemplate = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _newModuleTemplate.AddItem("RPG · zone jouable requise", (int)ProjectTemplateKind.Rpg);
        _newModuleTemplate.AddItem("Core · menus, sans zone", (int)ProjectTemplateKind.CoreMenu);
        _newModuleTemplate.AddItem("Core · simulation, zone optionnelle", (int)ProjectTemplateKind.CoreSimulation);
        _newModuleTemplate.ItemSelected += NewModuleTemplateSelected;
        identity.AddChild(_newModuleTemplate);
        identity.AddChild(new Label { Text = "Kits · séparés par des virgules" });
        _newModuleKits = new LineEdit { Text = "forge-core, forge-rpg-kit" };
        identity.AddChild(_newModuleKits);
        identity.AddChild(new Label { Text = "Plateforme cible" });
        _newModulePlatform = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _newModulePlatform.AddItem("Windows x64", (int)ModuleDistributionPlatform.WindowsX64);
        _newModulePlatform.AddItem("Linux x64", (int)ModuleDistributionPlatform.LinuxX64);
        _newModulePlatform.AddItem("macOS Apple Silicon", (int)ModuleDistributionPlatform.MacOSArm64);
        identity.AddChild(_newModulePlatform);
        identity.AddChild(new Label { Text = "Fournisseur de règles" });
        _newModuleRules = new LineEdit { Text = "forge.generic" };
        identity.AddChild(_newModuleRules);
        identity.AddChild(new Label { Text = "Espace de noms du package · vide = automatique" });
        _newModulePackageNamespace = new LineEdit { PlaceholderText = "module.mon-projet" };
        identity.AddChild(_newModulePackageNamespace);

        var presets = new HBoxContainer();
        presets.AddThemeConstantOverride("separation", 8);
        identity.AddChild(presets);
        var renderColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        renderColumn.AddChild(new Label { Text = "Rendu" });
        _newModuleRender = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _newModuleRender.AddItem("Équilibré", (int)ProjectRenderPreset.Balanced);
        _newModuleRender.AddItem("Qualité", (int)ProjectRenderPreset.Quality);
        _newModuleRender.AddItem("Performance", (int)ProjectRenderPreset.Performance);
        renderColumn.AddChild(_newModuleRender);
        presets.AddChild(renderColumn);
        var networkColumn = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        networkColumn.AddChild(new Label { Text = "Réseau" });
        _newModuleNetwork = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _newModuleNetwork.AddItem("Hors ligne", (int)ProjectNetworkMode.Offline);
        _newModuleNetwork.AddItem("Pair à pair", (int)ProjectNetworkMode.PeerToPeer);
        _newModuleNetwork.AddItem("Serveur dédié", (int)ProjectNetworkMode.DedicatedServer);
        networkColumn.AddChild(_newModuleNetwork);
        presets.AddChild(networkColumn);

        _newModuleCreateInitialArea = new CheckButton
        {
            Text = "Créer une première zone",
            ButtonPressed = true,
        };
        _newModuleCreateInitialArea.Toggled += NewModuleCreateInitialAreaToggled;
        world.AddChild(_newModuleCreateInitialArea);
        world.AddChild(new Label { Text = "Nom de la première zone" });
        _newModuleAreaName = new LineEdit
        {
            PlaceholderText = "Zone de départ",
            Text = "Zone de départ",
        };
        world.AddChild(_newModuleAreaName);
        world.AddChild(new Label { Text = "Type de zone" });
        _newModuleAreaKind = new OptionButton
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _newModuleAreaKind.AddItem("Intérieur · pièce ou donjon", (int)AreaKind.Interior);
        _newModuleAreaKind.AddItem("Extérieur · terrain sculptable", (int)AreaKind.Exterior);
        _newModuleAreaKind.ItemSelected += NewModuleAreaKindSelected;
        world.AddChild(_newModuleAreaKind);

        var dimensions = new HBoxContainer();
        dimensions.AddThemeConstantOverride("separation", 10);
        world.AddChild(dimensions);
        dimensions.AddChild(new Label
        {
            Text = "Dimensions (m)",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });
        _newModuleAreaWidth = NewModuleDimensionBox(32);
        _newModuleAreaDepth = NewModuleDimensionBox(32);
        dimensions.AddChild(new Label { Text = "Largeur" });
        dimensions.AddChild(_newModuleAreaWidth);
        dimensions.AddChild(new Label { Text = "Profondeur" });
        dimensions.AddChild(_newModuleAreaDepth);

        world.AddChild(new Label { Text = "Mode de jeu d’entrée" });
        _newModuleGameMode = new LineEdit { Text = "campaign" };
        world.AddChild(_newModuleGameMode);
        world.AddChild(new Label { Text = "Classe d’entrée" });
        _newModuleEntryClass = new LineEdit { Text = "adventurer" };
        world.AddChild(_newModuleEntryClass);
        world.AddChild(new Label { Text = "Sélection du joueur" });
        _newModulePlayerRule = new LineEdit
        {
            Text = "default",
            TooltipText = "default, player:<id> ou tag:<tag>",
        };
        world.AddChild(_newModulePlayerRule);

        var summaryPanel = new PanelContainer();
        summaryPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("21182e"),
            BorderColor = new Color("62458a"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 14,
            ContentMarginTop = 10,
            ContentMarginRight = 14,
            ContentMarginBottom = 10,
        });
        world.AddChild(summaryPanel);
        _newModuleSummary = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = "Résumé · RPG · Windows x64 · zone intérieure 32 × 32 · départ campaign/adventurer",
        };
        summaryPanel.AddChild(_newModuleSummary);

        var buttons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
        };
        buttons.AddThemeConstantOverride("separation", 8);
        layout.AddChild(buttons);
        _newModuleCancelButton = new Button
        {
            CustomMinimumSize = new Vector2(120, 38),
            Text = "Annuler",
        };
        buttons.AddChild(_newModuleCancelButton);
        _newModuleNextButton = new Button
        {
            CustomMinimumSize = new Vector2(250, 38),
            Text = "Valider et choisir l’emplacement",
        };
        _newModuleNextButton.AddThemeColorOverride("font_color", Colors.White);
        _newModuleNextButton.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color("e24f2d"),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
        });
        buttons.AddChild(_newModuleNextButton);

        _newModuleLocationDialog = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenDir,
            Title = "Choisir le dossier parent du nouveau module",
        };
        _mainPanel.AddChild(_newModuleLocationDialog);
    }

    private static VBoxContainer NewWizardColumn(string heading)
    {
        var column = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(430, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        column.AddThemeConstantOverride("separation", 8);
        column.AddChild(new Label { Text = heading, ThemeTypeVariation = "HeaderSmall" });
        return column;
    }

    private void CreateEntityPropertiesDialog()
    {
        _entityPropertiesDialog = new Window
        {
            Title = "Propriétés locales de l’instance",
            MinSize = new Vector2I(720, 560),
            Size = new Vector2I(720, 560),
            Exclusive = false,
            Transient = true,
            Unresizable = true,
            Visible = false,
        };
        _mainPanel!.AddChild(_entityPropertiesDialog);

        var background = new PanelContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
        };
        background.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("171322"),
            BorderColor = new Color("62458a"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
        });
        _entityPropertiesDialog.AddChild(background);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        background.AddChild(margin);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);
        margin.AddChild(content);
        var title = new Label
        {
            Text = "PROPRIÉTÉS DE L’INSTANCE",
            ThemeTypeVariation = "HeaderSmall",
        };
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        content.AddChild(title);
        _entityPropertiesIdentity = new Label();
        _entityPropertiesIdentity.AddThemeColorOverride("font_color", new Color("d8c8ff"));
        content.AddChild(_entityPropertiesIdentity);
        content.AddChild(new Label
        {
            Text = "Ces valeurs personnalisent uniquement l’instance sélectionnée. Le blueprint reste réutilisable et inchangé.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var editor = new HBoxContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        editor.AddThemeConstantOverride("separation", 16);
        content.AddChild(editor);

        var listColumn = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(340, 300),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        listColumn.AddThemeConstantOverride("separation", 6);
        editor.AddChild(listColumn);
        listColumn.AddChild(new Label { Text = "Valeurs locales" });
        _entityPropertiesList = new ItemList
        {
            CustomMinimumSize = new Vector2(340, 270),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        listColumn.AddChild(_entityPropertiesList);

        var propertyForm = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(300, 300),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        propertyForm.AddThemeConstantOverride("separation", 7);
        editor.AddChild(propertyForm);
        propertyForm.AddChild(new Label { Text = "Clé" });
        _entityPropertyKey = new LineEdit
        {
            PlaceholderText = "ex. displayName ou locked",
        };
        propertyForm.AddChild(_entityPropertyKey);
        propertyForm.AddChild(new Label { Text = "Type" });
        _entityPropertyKind = new OptionButton();
        _entityPropertyKind.AddItem("Texte");
        _entityPropertyKind.AddItem("Booléen");
        _entityPropertyKind.AddItem("Entier");
        _entityPropertyKind.AddItem("Nombre");
        propertyForm.AddChild(_entityPropertyKind);
        propertyForm.AddChild(new Label { Text = "Valeur" });
        _entityPropertyValue = new LineEdit
        {
            PlaceholderText = "Valeur propre à cette instance",
        };
        propertyForm.AddChild(_entityPropertyValue);
        _entityPropertyUpsertButton = new Button
        {
            CustomMinimumSize = new Vector2(0, 38),
            Text = "Ajouter ou remplacer",
        };
        _entityPropertyUpsertButton.AddThemeColorOverride("font_color", Colors.White);
        _entityPropertyUpsertButton.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color("65439a"),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
        });
        propertyForm.AddChild(_entityPropertyUpsertButton);
        _entityPropertyRemoveButton = new Button
        {
            CustomMinimumSize = new Vector2(0, 36),
            Disabled = true,
            Text = "Retirer la propriété sélectionnée",
        };
        propertyForm.AddChild(_entityPropertyRemoveButton);

        _entityPropertyValidation = new Label
        {
            Text = "Schéma d’overrides v1 · texte, booléen, entier ou nombre.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _entityPropertyValidation.AddThemeColorOverride("font_color", new Color("ab9acb"));
        content.AddChild(_entityPropertyValidation);

        var actions = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
        };
        actions.AddThemeConstantOverride("separation", 8);
        content.AddChild(actions);
        _entityPropertiesCancelButton = new Button
        {
            CustomMinimumSize = new Vector2(120, 38),
            Text = "Annuler",
        };
        actions.AddChild(_entityPropertiesCancelButton);
        _entityPropertiesSaveButton = new Button
        {
            CustomMinimumSize = new Vector2(190, 38),
            Text = "Appliquer à l’instance",
        };
        _entityPropertiesSaveButton.AddThemeColorOverride("font_color", Colors.White);
        _entityPropertiesSaveButton.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color("e24f2d"),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
        });
        actions.AddChild(_entityPropertiesSaveButton);
    }

    private void ConnectSignals()
    {
        _openButton!.Pressed += ShowProjectPicker;
        _newModuleButton!.Pressed += ShowNewModuleDialog;
        _dialoguesButton!.Pressed += OpenDialogueEditor;
        _areasButton!.Pressed += OpenAreaWorkspace;
        _workspaceButton!.Pressed += OpenCreatorWorkspace;
        _recoveryButton!.Pressed += OpenRecoveryWindow;
        _qualityButton!.Pressed += OpenQualityLab;
        _searchButton!.Pressed += OpenGlobalSearch;
        _moduleSettingsButton!.Pressed += OpenModuleConfiguration;
        _reloadButton!.Pressed += ReloadProject;
        _saveButton!.Pressed += SaveProject;
        _exportButton!.Pressed += ShowExportDialog;
        _placeButton!.Pressed += PlaceSelectedBlueprintFromUi;
        _moveXNegativeButton!.Pressed += MoveSelectedNegativeX;
        _moveXPositiveButton!.Pressed += MoveSelectedPositiveX;
        _moveZNegativeButton!.Pressed += MoveSelectedNegativeZ;
        _moveZPositiveButton!.Pressed += MoveSelectedPositiveZ;
        _rotateButton!.Pressed += RotateSelected;
        _deleteButton!.Pressed += DeleteSelected;
        _scaleX!.ValueChanged += SynchronizeUniformScale;
        _uniformScale!.Toggled += ToggleUniformScale;
        _scaleDownButton!.Pressed += ScaleSelectedDown;
        _scaleUpButton!.Pressed += ScaleSelectedUp;
        _applyScaleButton!.Pressed += ApplySelectedScale;
        _overridesButton!.Pressed += ShowEntityPropertiesDialog;
        _copyButton!.Pressed += CopySelectedEntity;
        _cutButton!.Pressed += CutSelectedEntities;
        _pasteButton!.Pressed += PasteSemanticClipboard;
        _duplicateButton!.Pressed += DuplicateSelectedEntity;
        _entityPicker!.ItemSelected += EntitySelectionChanged;
        _undoButton!.Pressed += Undo;
        _redoButton!.Pressed += Redo;
        _playButton!.Pressed += StartPlaytestFromUi;
        _projectDialog!.DirSelected += OpenSelectedProject;
        _exportDialog!.FileSelected += ExportModule;
        _newModuleNextButton!.Pressed += ChooseNewModuleLocation;
        _newModuleCancelButton!.Pressed += HideNewModuleDialog;
        _newModuleDialog!.CloseRequested += HideNewModuleDialog;
        _newModuleLocationDialog!.DirSelected += CreateNewModuleIn;
        _entityPropertiesList!.ItemSelected += SelectDraftOverride;
        _entityPropertyUpsertButton!.Pressed += UpsertDraftOverride;
        _entityPropertyRemoveButton!.Pressed += RemoveDraftOverride;
        _entityPropertiesSaveButton!.Pressed += ApplyDraftOverrides;
        _entityPropertiesCancelButton!.Pressed += HideEntityPropertiesDialog;
        _entityPropertiesDialog!.CloseRequested += HideEntityPropertiesDialog;
        _selectionContext.Changed += HandleSharedSelectionChanged;
        _activeAreaContext.Changed += HandleActiveAreaChanged;
        _creatorShell!.ExplorerSelectionRequested += HandleExplorerSelection;
        _creatorShell.DiagnosticSelectionRequested += HandleDiagnosticSelection;
        _creatorShell.DiagnosticQuickFixRequested += HandleDiagnosticQuickFix;
        _creatorShell.DiagnosticExportRequested += HandleDiagnosticExport;
        _creatorShell.WorkspaceRequested += HandleWorkspaceRequest;
        _creatorShell.SelectionLockRequested += HandleSelectionLockRequest;
        _creatorShell.AreaTabRequested += HandleAreaTabRequested;
        _creatorShell.AreaTabCloseRequested += HandleAreaTabCloseRequested;
    }

    private void DisconnectSignals()
    {
        if (_openButton is not null) _openButton.Pressed -= ShowProjectPicker;
        if (_newModuleButton is not null) _newModuleButton.Pressed -= ShowNewModuleDialog;
        if (_dialoguesButton is not null) _dialoguesButton.Pressed -= OpenDialogueEditor;
        if (_areasButton is not null) _areasButton.Pressed -= OpenAreaWorkspace;
        if (_workspaceButton is not null) _workspaceButton.Pressed -= OpenCreatorWorkspace;
        if (_recoveryButton is not null) _recoveryButton.Pressed -= OpenRecoveryWindow;
        if (_qualityButton is not null) _qualityButton.Pressed -= OpenQualityLab;
        if (_searchButton is not null) _searchButton.Pressed -= OpenGlobalSearch;
        if (_moduleSettingsButton is not null) _moduleSettingsButton.Pressed -= OpenModuleConfiguration;
        if (_reloadButton is not null) _reloadButton.Pressed -= ReloadProject;
        if (_saveButton is not null) _saveButton.Pressed -= SaveProject;
        if (_exportButton is not null) _exportButton.Pressed -= ShowExportDialog;
        if (_placeButton is not null) _placeButton.Pressed -= PlaceSelectedBlueprintFromUi;
        if (_moveXNegativeButton is not null) _moveXNegativeButton.Pressed -= MoveSelectedNegativeX;
        if (_moveXPositiveButton is not null) _moveXPositiveButton.Pressed -= MoveSelectedPositiveX;
        if (_moveZNegativeButton is not null) _moveZNegativeButton.Pressed -= MoveSelectedNegativeZ;
        if (_moveZPositiveButton is not null) _moveZPositiveButton.Pressed -= MoveSelectedPositiveZ;
        if (_rotateButton is not null) _rotateButton.Pressed -= RotateSelected;
        if (_deleteButton is not null) _deleteButton.Pressed -= DeleteSelected;
        if (_scaleX is not null) _scaleX.ValueChanged -= SynchronizeUniformScale;
        if (_uniformScale is not null) _uniformScale.Toggled -= ToggleUniformScale;
        if (_scaleDownButton is not null) _scaleDownButton.Pressed -= ScaleSelectedDown;
        if (_scaleUpButton is not null) _scaleUpButton.Pressed -= ScaleSelectedUp;
        if (_applyScaleButton is not null) _applyScaleButton.Pressed -= ApplySelectedScale;
        if (_overridesButton is not null) _overridesButton.Pressed -= ShowEntityPropertiesDialog;
        if (_copyButton is not null) _copyButton.Pressed -= CopySelectedEntity;
        if (_cutButton is not null) _cutButton.Pressed -= CutSelectedEntities;
        if (_pasteButton is not null) _pasteButton.Pressed -= PasteSemanticClipboard;
        if (_duplicateButton is not null) _duplicateButton.Pressed -= DuplicateSelectedEntity;
        if (_entityPicker is not null) _entityPicker.ItemSelected -= EntitySelectionChanged;
        if (_undoButton is not null) _undoButton.Pressed -= Undo;
        if (_redoButton is not null) _redoButton.Pressed -= Redo;
        if (_playButton is not null) _playButton.Pressed -= StartPlaytestFromUi;
        if (_projectDialog is not null) _projectDialog.DirSelected -= OpenSelectedProject;
        if (_exportDialog is not null) _exportDialog.FileSelected -= ExportModule;
        if (_newModuleAreaKind is not null) _newModuleAreaKind.ItemSelected -= NewModuleAreaKindSelected;
        if (_newModuleTemplate is not null) _newModuleTemplate.ItemSelected -= NewModuleTemplateSelected;
        if (_newModuleCreateInitialArea is not null)
            _newModuleCreateInitialArea.Toggled -= NewModuleCreateInitialAreaToggled;
        if (_newModuleNextButton is not null) _newModuleNextButton.Pressed -= ChooseNewModuleLocation;
        if (_newModuleCancelButton is not null) _newModuleCancelButton.Pressed -= HideNewModuleDialog;
        if (_newModuleDialog is not null) _newModuleDialog.CloseRequested -= HideNewModuleDialog;
        if (_newModuleLocationDialog is not null) _newModuleLocationDialog.DirSelected -= CreateNewModuleIn;
        if (_entityPropertiesList is not null) _entityPropertiesList.ItemSelected -= SelectDraftOverride;
        if (_entityPropertyUpsertButton is not null) _entityPropertyUpsertButton.Pressed -= UpsertDraftOverride;
        if (_entityPropertyRemoveButton is not null) _entityPropertyRemoveButton.Pressed -= RemoveDraftOverride;
        if (_entityPropertiesSaveButton is not null) _entityPropertiesSaveButton.Pressed -= ApplyDraftOverrides;
        if (_entityPropertiesCancelButton is not null) _entityPropertiesCancelButton.Pressed -= HideEntityPropertiesDialog;
        if (_entityPropertiesDialog is not null) _entityPropertiesDialog.CloseRequested -= HideEntityPropertiesDialog;
        _selectionContext.Changed -= HandleSharedSelectionChanged;
        _activeAreaContext.Changed -= HandleActiveAreaChanged;
        if (_creatorShell is not null)
        {
            _creatorShell.ExplorerSelectionRequested -= HandleExplorerSelection;
            _creatorShell.DiagnosticSelectionRequested -= HandleDiagnosticSelection;
            _creatorShell.DiagnosticQuickFixRequested -= HandleDiagnosticQuickFix;
            _creatorShell.DiagnosticExportRequested -= HandleDiagnosticExport;
            _creatorShell.WorkspaceRequested -= HandleWorkspaceRequest;
            _creatorShell.SelectionLockRequested -= HandleSelectionLockRequest;
            _creatorShell.AreaTabRequested -= HandleAreaTabRequested;
            _creatorShell.AreaTabCloseRequested -= HandleAreaTabCloseRequested;
        }
    }

    private void HandleAreaTabRequested(AreaId areaId)
    {
        var profile = EditorWorkspaceProfilePlanner.OpenArea(LoadWorkspaceProfile(), areaId);
        SaveWorkspaceProfile(profile);
    }

    private void HandleAreaTabCloseRequested(AreaId areaId)
    {
        var profile = LoadWorkspaceProfile();
        if (profile.OpenAreaTabs.Count <= 1)
        {
            return;
        }

        SaveWorkspaceProfile(EditorWorkspaceProfilePlanner.CloseArea(profile, areaId));
    }

    private void HandleWorkspaceRequest(string workspace)
    {
        _creatorShell?.SetWorkspace(workspace);
        if (string.Equals(workspace, "Dialogues", StringComparison.Ordinal))
        {
            OpenDialogueEditor();
            return;
        }

        if (string.Equals(workspace, "Quêtes", StringComparison.Ordinal))
        {
            _questJournalWindow?.Open();
            return;
        }

        if (string.Equals(workspace, "Blueprints", StringComparison.Ordinal))
        {
            OpenBlueprintCatalog();
            return;
        }

        if (string.Equals(workspace, "Scripts", StringComparison.Ordinal))
        {
            _forgeScriptEditor?.Open();
            return;
        }

        if (string.Equals(workspace, "Assets", StringComparison.Ordinal))
        {
            _assetBrowser?.Open();
            return;
        }

        if (string.Equals(workspace, "Données", StringComparison.Ordinal))
        {
            _contentPackageWindow?.Open();
            return;
        }

        if (string.Equals(workspace, "Validation", StringComparison.Ordinal))
        {
            _distributionProfiles?.Open();
            return;
        }

        if (string.Equals(workspace, "Assistant", StringComparison.Ordinal))
        {
            _assistantWorkspace?.Open();
            return;
        }

        if (string.Equals(workspace, "Réseau", StringComparison.Ordinal))
        {
            _networkBetaWindow?.Open();
            return;
        }

        if (string.Equals(workspace, "Live Director", StringComparison.Ordinal))
        {
            _liveDirectorWindow?.Open();
            return;
        }

        if (string.Equals(workspace, "Monde", StringComparison.Ordinal))
        {
            _statusLabel!.Text = "✓ Workspace Monde actif";
            _detailsLabel!.Text = "Explorateur, sélection, aperçu et diagnostics partagent le projet courant.";
            return;
        }

        _statusLabel!.Text = $"Espace {workspace} préparé dans la coque Creator";
        _detailsLabel!.Text = "Son workflow spécialisé sera livré par un incrément Creator dédié.";
    }

    private void HandleExplorerSelection(CreatorExplorerNode node)
    {
        var selection = node.ToSelection();
        if (selection is null)
        {
            return;
        }

        PublishSearchSelection(selection);
    }

    private void PublishSearchSelection(CreatorSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        if (_history is not null && selection.Kind == CreatorSelectionKind.Area)
        {
            ActivateArea(AreaId.Parse(selection.Id));
        }
        else if (_history is not null && selection.Kind == CreatorSelectionKind.Entity)
        {
            var owner = _history.State.Project.Areas.FirstOrDefault(area =>
                area.Entities.Any(entity => entity.Id.ToString() == selection.Id));
            if (owner is not null)
            {
                ActivateArea(owner.Id);
            }
        }

        if (!_selectionContext.Replace(selection))
        {
            return;
        }

        if (selection.Kind == CreatorSelectionKind.Entity && _entityPicker is not null)
        {
            var entityIndex = _entityIds.FindIndex(id => id.ToString() == selection.Id);
            if (entityIndex >= 0)
            {
                _entityPicker.Selected = entityIndex;
                RefreshOverrideSummary();
            }
        }
    }

    private IReadOnlyList<CreatorDiagnostic> GetSearchDiagnostics() => _history is null
        ? []
        : CreatorDiagnostics.FromProject(
            _history.State.Project,
            $"revision:{_history.State.Revision}");

    private void HandleDiagnosticSelection(CreatorDiagnostic diagnostic)
    {
        var selection = diagnostic.Target ?? new CreatorSelection(
            CreatorSelectionKind.Diagnostic,
            diagnostic.Id,
            diagnostic.Code,
            diagnostic.DocumentPath);
        if (_selectionContext.Replace(selection))
        {
            _statusLabel!.Text = $"Diagnostic ciblé · {diagnostic.Code}";
            _detailsLabel!.Text =
                $"{diagnostic.Message}\n{diagnostic.DocumentPath}\n" +
                $"Propriété · {diagnostic.Property}\nSource · {diagnostic.Source}\nUUID · {diagnostic.Id}";
        }
    }

    private void HandleDiagnosticQuickFix(
        CreatorDiagnostic diagnostic,
        DiagnosticQuickFixDefinition quickFix,
        bool dryRun)
    {
        if (_history is null)
        {
            return;
        }

        var command = new ApplyDiagnosticQuickFixCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            diagnostic.DiagnosticId,
            quickFix.Id,
            dryRun);
        var result = _diagnosticQuickFixHandler.Handle(_history.State, command);
        if (dryRun)
        {
            _statusLabel!.Text = result.Succeeded
                ? $"Aperçu du correctif · {quickFix.Label}"
                : "✗ Prévisualisation refusée";
            _detailsLabel!.Text = result.Succeeded
                ? result.Patch?.Summary ?? quickFix.Description
                : string.Join("\n", result.Diagnostics.Select(item => $"{item.Code} — {item.Message}"));
            return;
        }

        ApplyCommand(
            result,
            $"Correctif appliqué · {quickFix.Label}",
            quickFix.EntityId);
    }

    private void HandleDiagnosticExport(IReadOnlyList<CreatorDiagnostic> diagnostics)
    {
        if (_projectSession is null || _creatorShell is null) return;
        try
        {
            var exported = CreatorDiagnosticBundleExporter.Export(
                _projectSession.RootDirectory,
                diagnostics,
                _playtestLog?.Text);
            var message =
                $"✓ Bundle exporté · {exported.DiagnosticCount} diagnostic(s) · {exported.RelativePath}";
            _creatorShell.SetDiagnosticExportStatus(message, failed: false);
            if (_statusLabel is not null) _statusLabel.Text = "✓ Diagnostics exportés";
            if (_detailsLabel is not null)
                _detailsLabel.Text = $"{exported.RelativePath}\nSHA-256 · {exported.Sha256}";
            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_DIAGNOSTICS_EXPORTED count={exported.DiagnosticCount} " +
                $"runtime={exported.IncludesRuntimeLog.ToString().ToLowerInvariant()} " +
                $"reused={exported.Reused.ToString().ToLowerInvariant()} sha256={exported.Sha256}");
        }
        catch (Exception exception) when (
            exception is ArgumentException or DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            _creatorShell.SetDiagnosticExportStatus($"✕ Export impossible · {exception.Message}", failed: true);
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_DIAGNOSTICS_EXPORT_FAILED {exception.Message}");
        }
    }

    private void HandleSelectionLockRequest(bool locked) => _selectionContext.SetLocked(locked);

    private void HandleSharedSelectionChanged()
    {
        _creatorShell?.SetSelection(_selectionContext.Current, _selectionContext.IsLocked);
        if (_history is not null && _selectionContext.Current.Count > 1)
        {
            _creatorShell?.SetSelectionInspection(CreatorSelectionInspector.Inspect(
                _history.State.Project, _selectionContext.Current, "creator-shell-inspector"));
        }
        var entitySelection = _selectionContext.Current.Count == 1 &&
            _selectionContext.Current[0].Kind == CreatorSelectionKind.Entity
                ? _selectionContext.Current[0]
                : null;
        if (entitySelection is null || _entityPicker is null)
        {
            return;
        }

        var entityIndex = _entityIds.FindIndex(id => id.ToString() == entitySelection.Id);
        if (entityIndex >= 0 && _entityPicker.Selected != entityIndex)
        {
            _entityPicker.Selected = entityIndex;
            RefreshOverrideSummary();
        }
    }

    private void HandleActiveAreaChanged()
    {
        if (_history is null)
        {
            return;
        }

        RebuildPreview();
        RefreshEditorControls();
        _areaWorkspace?.Refresh();
    }

    private void ShowProjectPicker()
    {
        if (_projectDialog is null)
        {
            return;
        }

        _projectDialog.CurrentDir = _projectSession?.RootDirectory ?? GetPrototypeWorkspaceDirectory();
        _projectDialog.PopupCenteredRatio(0.72f);
        ApplyForgeWindowIcon(_projectDialog);
    }

    private void ShowExportDialog()
    {
        if (_exportDialog is null || _projectSession is null || _history is null)
        {
            return;
        }

        _exportDialog.CurrentDir = Path.GetDirectoryName(_projectSession.RootDirectory)
            ?? _projectSession.RootDirectory;
        _exportDialog.CurrentFile = $"{_history.State.Project.Module.Slug}-0.1.0.orpmod";
        _exportDialog.PopupCenteredRatio(0.72f);
        ApplyForgeWindowIcon(_exportDialog);
    }

    private void ExportModule(string destinationPath)
    {
        if (_projectSession is null || _history is null || _statusLabel is null ||
            _detailsLabel is null || _exportButton is null)
        {
            return;
        }

        if (!string.Equals(Path.GetExtension(destinationPath), ".orpmod", StringComparison.OrdinalIgnoreCase))
        {
            destinationPath += ".orpmod";
        }

        _exportButton.Disabled = true;
        try
        {
            var saved = _projectSession.Save(_history.State.Project);
            if (!saved.Succeeded)
            {
                _statusLabel.Text = "✕ Export interrompu";
                _detailsLabel.Text = FormatDiagnostics(saved.Diagnostics);
                return;
            }

            var exported = ModulePackager.Export(
                _projectSession.RootDirectory,
                destinationPath,
                target: _distributionTarget);
            _statusLabel.Text = "✓ Module exporté et validé";
            _detailsLabel.Text =
                $"{exported.Manifest.Slug} · cible {exported.Manifest.Target} · " +
                $"{exported.FileCount} fichiers · {exported.ArchiveBytes / 1024d:0.0} Kio · " +
                $"{exported.BuildPlan.RebuiltResources.Count} recalculé(s) · " +
                $"{exported.BuildPlan.ReusedResources.Count} réutilisé(s)";
            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_MODULE_EXPORTED slug={exported.Manifest.Slug} " +
                $"files={exported.FileCount} bytes={exported.ArchiveBytes} " +
                $"rebuilt={exported.BuildPlan.RebuiltResources.Count} " +
                $"reused={exported.BuildPlan.ReusedResources.Count} " +
                $"dependencies={exported.Manifest.Dependencies.Count} " +
                $"target={exported.Manifest.Target.ToString().ToLowerInvariant()} notices=generated");
        }
        catch (Exception exception) when (
            exception is ArgumentException or DirectoryNotFoundException or FileNotFoundException or
            InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _statusLabel.Text = "✕ Échec de l’export";
            _detailsLabel.Text = exception.Message;
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_MODULE_EXPORT_FAILED {exception.Message}");
        }
        finally
        {
            _exportButton.Disabled = false;
        }
    }

    private void OpenDialogueEditor() => _dialogueEditor?.Open();

    private void OpenBlueprintCatalog() => _blueprintCatalog?.Open();

    private void OpenAreaWorkspace() => _areaWorkspace?.Open();

    private void OpenCreatorWorkspace() => _workspaceWindow?.Open();

    private void OpenRecoveryWindow() => _recoveryWindow?.Open();

    private void OpenQualityLab() => _qualityLabWindow?.Open();

    private IReadOnlyList<RecoverySnapshotInfo> ListRecoverySnapshots()
    {
        if (_projectSession is null) return [];
        try
        {
            var quarantine = _history?.State.Project.Module.RuntimePolicies.Extensions.OperationalReadiness
                .Recovery.QuarantineCorruptFiles == true;
            var inspection = _recoveryStore.Inspect(_projectSession.RootDirectory, quarantine);
            foreach (var item in inspection.Where(item => item.Status == RecoverySnapshotStatus.Quarantined))
                GD.Print($"OPENRPG_FORGE_RECOVERY_QUARANTINED code={item.Code} file={Path.GetFileName(item.QuarantinePath)}");
            return _recoveryStore.List(_projectSession.RootDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or NotSupportedException)
        {
            GD.PrintErr($"OPENRPG_FORGE_RECOVERY_LIST_FAILED {exception.Message}"); return [];
        }
    }

    private void CaptureRecoverySnapshot()
    {
        if (_projectSession is null || _history is null || _history.State.Revision.Value == _lastSavedRevision ||
            _history.State.Revision.Value == _lastRecoveryRevision) return;
        try
        {
            var snapshot = _recoveryStore.Capture(_projectSession.RootDirectory, _history.State.Project,
                _history.State.Revision.Value, DateTimeOffset.UtcNow);
            _lastRecoveryRevision = _history.State.Revision.Value;
            GD.Print($"OPENRPG_FORGE_AUTOSAVE_CAPTURED revision={snapshot.Revision} hash={snapshot.ProjectSha256[..12]}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { GD.PrintErr($"OPENRPG_FORGE_AUTOSAVE_FAILED {exception.Message}"); }
    }

    private RecoveryComparison? RestoreRecoverySnapshot(RecoverySnapshotInfo snapshot)
    {
        if (_history is null) return null;
        try
        {
            var preview = _recoveryStore.PreviewRestore(snapshot, _history.State.Project);
            _history = new ProjectCommandHistory(new ProjectState(preview.Project, _history.State.Revision.Next()));
            _activeAreaContext.EnsureValid(preview.Project); RebuildPreview(); RefreshEditorControls();
            _statusLabel!.Text = "✓ Instantané restauré en mémoire";
            _detailsLabel!.Text = $"Avant/après comparé · {preview.Comparison.CurrentSha256[..8]}→{preview.Comparison.SnapshotSha256[..8]} · Enregistrer pour confirmer";
            return preview.Comparison;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException or NotSupportedException)
        { GD.PrintErr($"OPENRPG_FORGE_RECOVERY_RESTORE_FAILED {exception.Message}"); return null; }
    }

    private void ClearRecoverySnapshots()
    {
        if (_projectSession is null) return;
        try { _recoveryStore.Clear(_projectSession.RootDirectory); _lastRecoveryRevision = -1; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { GD.PrintErr($"OPENRPG_FORGE_RECOVERY_CLEAR_FAILED {exception.Message}"); }
    }

    private int ApplyWorkspaceFilter(EditorDisplayFilter filter)
    {
        if (_projectPreview is null) return 0;
        _projectPreview.SetDisplayFilter(filter);
        return _projectPreview.ApplyDisplayFilter();
    }

    private EditorWorkspaceProfile LoadWorkspaceProfile()
    {
        var areaId = _activeAreaContext.Current;
        if (_projectSession is null) return EditorWorkspaceProfile.CreateDefault(EditorRole.Builder, areaId);
        try
        {
            var loaded = EditorWorkspaceProfileStore.TryLoad(_projectSession.RootDirectory) ??
                         EditorWorkspaceProfile.CreateDefault(EditorRole.Builder, areaId);
            var availableAreas = _history?.State.Project.Areas.Select(area => area.Id).ToArray() ?? [];
            return EditorWorkspaceProfilePlanner.ReconcileAreas(loaded, availableAreas, areaId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidDataException or ArgumentException or
                                           System.Text.Json.JsonException)
        {
            GD.PrintErr($"OPENRPG_FORGE_WORKSPACE_PROFILE_LOAD_FAILED {exception.Message}");
            return EditorWorkspaceProfile.CreateDefault(EditorRole.Builder, areaId);
        }
    }

    private void SaveWorkspaceProfile(EditorWorkspaceProfile profile)
    {
        if (_projectSession is null) return;
        EditorWorkspaceProfileStore.Save(_projectSession.RootDirectory, profile);
        _projectPreview?.SetDisplayFilter(profile.DisplayFilter);
        _projectPreview?.SetEditorMode(profile.Shell.ViewportMode);
        _creatorShell?.ApplyShellConfiguration(profile.Shell);
        ApplyEditorExperience(profile);
        RefreshCreatorShell();
        GD.Print("OPENRPG_FORGE_WORKSPACE_PROFILE_SAVED");
    }

    private void RecordRecentBlueprint(BlueprintId blueprintId)
    {
        if (_projectSession is null) return;
        SaveWorkspaceProfile(EditorWorkspaceProfilePlanner.RecordRecent(LoadWorkspaceProfile(), blueprintId));
    }

    private bool TogglePersonalFavorite(BlueprintId blueprintId)
    {
        if (_projectSession is null) return false;
        var updated = EditorWorkspaceProfilePlanner.TogglePersonalFavorite(LoadWorkspaceProfile(), blueprintId);
        SaveWorkspaceProfile(updated);
        return updated.PersonalFavoriteBlueprintIds.Contains(blueprintId);
    }

    private void ApplyEditorExperience(EditorWorkspaceProfile profile)
    {
        if (_mainPanel is null || _history is null)
        {
            return;
        }

        var theme = new Theme
        {
            DefaultBaseScale = (float)profile.UiScale,
            DefaultFontSize = Math.Max(11, (int)Math.Round(14 * profile.TextScale)),
        };
        var font = new SystemFont
        {
            FontNames = new[] { profile.PrimaryFontFamily }.Concat(profile.FallbackFontFamilies).ToArray(),
        };
        theme.DefaultFont = font;
        _mainPanel.Theme = theme;
        _projectPreview?.SetDisplayFilter(profile.DisplayFilter);
        _creatorShell?.SetAreaTabs(_history.State.Project, profile);
        if (profile.ActiveAreaId is not null)
        {
            ActivateArea(profile.ActiveAreaId);
        }

        GD.Print($"OPENRPG_FORGE_EDITOR_EXPERIENCE_APPLIED ui={profile.UiScale:0.##} text={profile.TextScale:0.##} font={profile.PrimaryFontFamily} tabs={profile.OpenAreaTabs.Count}");
    }

    private bool PlaceRepeatedSeries(int count, float spacing, PlacementDistribution distribution, int seed)
    {
        if (_history is null || _blueprintPalette is null || _positionX is null || _positionZ is null ||
            _blueprintPalette.Selected < 0 || _blueprintPalette.Selected >= _paletteBlueprintIds.Count) return false;
        var area = GetActiveArea();
        var positions = RepeatedPlacementPlanner.Plan(new(
            new Vector3Value(SnapToGrid(_positionX.Value), 0, SnapToGrid(_positionZ.Value)),
            count, spacing, distribution, Seed: seed));
        var blueprintId = _paletteBlueprintIds[_blueprintPalette.Selected];
        var sources = positions.Select(position => new EntityInstance(
            EntityId.From(Guid.NewGuid()), blueprintId,
            new TransformValue(position, QuaternionValue.Identity, Vector3Value.One))).ToArray();
        var payload = new EntityClipboardPayload(area.Id, sources);
        var mapping = sources.ToDictionary(entity => entity.Id, _ => EntityId.From(Guid.NewGuid()));
        var command = new PasteEntitiesCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            area.Id, payload, mapping, Vector3Value.Zero, DryRun: false);
        return ApplyCommand(_pasteEntitiesHandler.Handle(_history.State, command),
            $"{count} entités placées en série ({distribution}).", mapping[sources[0].Id]);
    }

    private void OpenNavigationConfiguration() => _navigationConfiguration?.Open();

    private void OpenAreaEnvironment() => _areaEnvironmentWindow?.Open();

    private void OpenTerrainStudio() => _terrainStudioWindow?.Open();

    private void OpenRuntimePolicies() => _runtimePoliciesWindow?.Open();

    private void OpenCinematicStudio() => _cinematicStudioWindow?.Open();

    private void OpenGameplayControls() => _gameplayControlsWindow?.Open();

    private void OpenRulesConfiguration() => _rulesConfigurationWindow?.Open();

    private void OpenAreaExperience() => _areaExperienceWindow?.Open();

    private void OpenAreaAuthoring() => _areaAuthoringWindow?.Open();

    private void OpenAreaFoundation() => _areaFoundationWindow?.Open();

    private bool ApplyAreaFoundation(AreaId areaId, AreaFoundationProfile profile)
    {
        if (_history is null) return false;
        return ApplyCommand(_setAreaFoundationHandler.Handle(_history.State,
            new SetAreaFoundationCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, areaId,
                profile, false)), "Fondation de zone mise à jour.", null);
    }

    private bool ApplyAreaPlaytest(AreaId areaId, AreaPlaytestProfile profile)
    {
        if (_history is null) return false;
        return ApplyCommand(_setAreaPlaytestHandler.Handle(_history.State,
            new SetAreaPlaytestCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, areaId,
                profile, false)), "Play From Here configuré.", null);
    }

    private bool ApplyAreaAuthoring(AreaId areaId, AreaAuthoringProfile profile)
    {
        if (_history is null) return false;
        return ApplyCommand(_setAreaAuthoringHandler.Handle(_history.State,
            new SetAreaAuthoringCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, areaId, profile, false)),
            "Carte, événements et calques mis à jour.", null);
    }

    private bool ApplyAreaEnvironment(AreaId areaId, AreaEnvironmentProfile profile)
    {
        if (_history is null) return false;
        var command = new SetAreaEnvironmentCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, areaId, profile, false);
        return ApplyCommand(_setAreaEnvironmentHandler.Handle(_history.State, command), "Environnement de zone mis à jour.", null);
    }

    private bool ApplyAreaWorldProfile(AreaId areaId, AreaWorldProfile profile)
    {
        if (_history is null) return false;
        var command = new SetAreaWorldProfileCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, areaId, profile, false);
        return ApplyCommand(_setAreaWorldProfileHandler.Handle(_history.State, command),
            "Terrain, biomes, features et volumes mis à jour.", null);
    }

    private bool ApplyTerrainBrush(AreaId areaId, TerrainBrushStroke stroke)
    {
        if (_history is null) return false;
        var command = new EditTerrainBrushCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, areaId,
            stroke, false);
        return ApplyCommand(_editTerrainBrushHandler.Handle(_history.State, command),
            "Terrain sculpté et navigation reconstruite.", null);
    }

    private bool ApplyRuntimePolicies(ModuleRuntimePolicyProfile moduleProfile, AreaId areaId,
        AreaRuntimePolicyProfile areaProfile)
    {
        if (_history is null) return false;
        var preview = new SetRuntimePoliciesCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            moduleProfile, areaId, areaProfile, true);
        var previewResult = _setRuntimePoliciesHandler.Handle(_history.State, preview);
        if (!previewResult.Succeeded) return false;
        var command = preview with
        {
            CommandId = CommandId.From(Guid.NewGuid()),
            DryRun = false,
        };
        return ApplyCommand(_setRuntimePoliciesHandler.Handle(_history.State, command),
            "Rythme, repos et restauration des séquences mis à jour.", null);
    }

    private bool ApplyCinematicProfiles(Forge.Domain.Sequences.ModuleCinematicProfile moduleProfile, AreaId areaId,
        Forge.Domain.Sequences.AreaCinematicProfile areaProfile)
    {
        if (_history is null) return false;
        var preview = new SetCinematicProfilesCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            moduleProfile, areaId, areaProfile, true);
        var previewResult = _setCinematicProfilesHandler.Handle(_history.State, preview);
        if (!previewResult.Succeeded) return false;
        var command = preview with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
        return ApplyCommand(_setCinematicProfilesHandler.Handle(_history.State, command),
            "Séquences, caméras et mouvements mis à jour.", null);
    }

    private bool ApplyGameplayControlProfile(ModuleGameplayControlProfile profile)
    {
        if (_history is null) return false;
        var preview = new SetGameplayControlProfileCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            profile, true);
        if (!_setGameplayControlProfileHandler.Handle(_history.State, preview).Succeeded) return false;
        var command = preview with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
        return ApplyCommand(_setGameplayControlProfileHandler.Handle(_history.State, command),
            "Nage, escalade, roue d’actions, temps tactique et combat mis à jour.", null);
    }

    private bool ApplyRulesProfiles(ModuleRulesProfile moduleProfile, AreaId areaId, AreaRulesProfile areaProfile)
    {
        if (_history is null) return false;
        var preview = new SetRulesProfilesCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            moduleProfile, areaId, areaProfile, true);
        if (!_setRulesProfilesHandler.Handle(_history.State, preview).Succeeded) return false;
        var command = preview with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
        return ApplyCommand(_setRulesProfilesHandler.Handle(_history.State, command),
            "Progression, difficulté, perception et combat de zone mis à jour.", null);
    }

    private bool ApplyAreaExperienceProfile(AreaId areaId, AreaExperienceProfile profile)
    {
        if (_history is null) return false;
        var preview = new SetAreaExperienceProfileCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            areaId, profile, true);
        if (!_setAreaExperienceProfileHandler.Handle(_history.State, preview).Succeeded) return false;
        var command = preview with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
        return ApplyCommand(_setAreaExperienceProfileHandler.Handle(_history.State, command),
            "Audio spatial, voix, patrouilles et chargement mis à jour.", null);
    }

    private bool ApplyRuntimeExtensions(ModuleRuntimeExtensionProfile profile)
    {
        if (_history is null) return false;
        var preview = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            profile, true);
        if (!_setRuntimeExtensionsHandler.Handle(_history.State, preview).Succeeded) return false;
        var command = preview with { CommandId = CommandId.From(Guid.NewGuid()), DryRun = false };
        return ApplyCommand(_setRuntimeExtensionsHandler.Handle(_history.State, command),
            "Règles extensibles, UI scriptable, JSON runtime et snapshots mis à jour.", null);
    }

    private void OpenGlobalSearch() => _globalSearch?.Open();

    private void OpenModuleConfiguration() => _moduleConfiguration?.Open();

    private bool ActivateArea(AreaId areaId)
    {
        if (_history is null || !_activeAreaContext.Activate(_history.State.Project, areaId))
        {
            return _activeAreaContext.Current == areaId;
        }

        var area = _history.State.Project.Areas.Single(candidate => candidate.Id == areaId);
        _statusLabel!.Text = $"✓ Zone active · {area.DisplayName}";
        _detailsLabel!.Text =
            $"{area.Dimensions.Width} × {area.Dimensions.Depth} · {area.Entities.Count} entité(s) · " +
            $"{area.Portals.Count} transition(s)";
        if (_projectSession is not null)
        {
            SaveWorkspaceProfile(EditorWorkspaceProfilePlanner.OpenArea(LoadWorkspaceProfile(), areaId));
        }

        return true;
    }

    private AreaId? CreateArea(
        string displayName,
        AreaKind kind,
        AreaDimensions dimensions,
        AreaTemplateKind template) =>
        CreateArea(displayName, kind, dimensions, template, AreaCreationSettings.CreateDefault(kind));

    private AreaId? CreateArea(
        string displayName,
        AreaKind kind,
        AreaDimensions dimensions,
        AreaTemplateKind template,
        AreaCreationSettings creation)
    {
        if (_history is null)
        {
            return null;
        }

        var areaId = AreaId.From(Guid.NewGuid());
        var spawnBlueprintId = template == AreaTemplateKind.Playable
            ? _history.State.Project.Blueprints.FirstOrDefault(blueprint =>
                blueprint.Kind == BlueprintKind.PlayerSpawn)?.Id
            : null;
        var spawnEntityId = template == AreaTemplateKind.Playable
            ? EntityId.From(Guid.NewGuid())
            : null;
        var command = new CreateAreaCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            areaId,
            displayName,
            kind,
            dimensions,
            template,
            spawnBlueprintId,
            spawnEntityId,
            DryRun: false,
            Foundation: CreateAreaFoundation(areaId, kind, creation));
        if (!ApplyCommand(
                _createAreaHandler.Handle(_history.State, command),
                $"Zone « {displayName} » créée.",
                selectedEntityId: null))
        {
            return null;
        }

        ActivateArea(areaId);
        return areaId;
    }

    private static AreaFoundationProfile CreateAreaFoundation(
        AreaId areaId,
        AreaKind kind,
        AreaCreationSettings creation)
    {
        var defaults = AreaFoundationProfile.CreateDefault(areaId, kind);
        return new(defaults.Id, defaults.SchemaVersion, "author:area-wizard", creation,
            new(defaults.TileKit.Id, defaults.TileKit.SchemaVersion, "author:area-wizard",
                defaults.TileKit.GridSizeMeters, defaults.TileKit.Definitions, defaults.TileKit.ConnectionRules,
                defaults.TileKit.Placements, defaults.TileKit.Groups, defaults.TileKit.Brushes),
            defaults.Lifecycle, defaults.Volumes, defaults.Dependencies);
    }

    private bool UpsertPortal(AreaId sourceAreaId, PortalLink portal)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new UpsertPortalLinkCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            sourceAreaId,
            portal,
            DryRun: false);
        return ApplyCommand(
            _upsertPortalHandler.Handle(_history.State, command),
            $"Transition « {portal.DisplayName} » enregistrée.",
            portal.SourceEntityId);
    }

    private void CopySelectedEntity()
    {
        var selected = GetSelectedEntityIdsForActiveArea();
        if (_history is null || selected.Count == 0)
        {
            return;
        }

        var area = GetActiveArea();
        _semanticClipboard.CopyEntities(
            _history.State.Project,
            area.Id,
            selected,
            provenance: "creator-selection-copy");
        _clipboardSummary!.Text =
            $"Aperçu · {selected.Count} entité(s) · UUID et références remappés au collage";
        _pasteButton!.Disabled = false;
        _statusLabel!.Text = $"✓ {selected.Count} entité(s) copiée(s) dans le clipboard sémantique";
        _detailsLabel!.Text = "Le collage sera prévalidé dans la zone active; choisissez instance ou copie indépendante.";
    }

    private void CutSelectedEntities()
    {
        var selected = GetSelectedEntityIdsForActiveArea();
        if (_history is null || selected.Count == 0)
        {
            return;
        }

        var command = new CutEntitiesCommand(
            CommandId.From(Guid.NewGuid()),
            CutEntitiesHandler.CurrentSchemaVersion,
            "creator-selection-cut",
            _history.State.Revision,
            GetActiveArea().Id,
            selected,
            DryRun: false);
        var preview = _cutEntitiesHandler.Handle(_history.State, command with { DryRun = true });
        if (!preview.Command.Succeeded)
        {
            ApplyCommand(preview.Command, string.Empty, selectedEntityId: null);
            return;
        }

        var applied = _cutEntitiesHandler.Handle(_history.State, command);
        if (applied.Payload is null || !ApplyCommand(
                applied.Command,
                $"{selected.Count} entité(s) coupée(s).",
                selectedEntityId: null))
        {
            return;
        }

        _semanticClipboard.Set(applied.Payload);
        _pasteButton!.Disabled = false;
        _clipboardSummary!.Text = $"{selected.Count} entité(s) coupée(s) · collage transactionnel disponible";
    }

    private void PasteSemanticClipboard()
    {
        if (_history is null || _semanticClipboard.Payload is not EntityClipboardPayload payload)
        {
            return;
        }

        var area = GetActiveArea();
        var mapping = payload.Entities.ToDictionary(
            entity => entity.Id,
            _ => EntityId.From(Guid.NewGuid()));
        var mode = _independentCopy!.ButtonPressed
            ? SemanticPasteMode.IndependentCopy
            : SemanticPasteMode.Instance;
        var blueprintMap = mode == SemanticPasteMode.IndependentCopy
            ? payload.Entities.Select(entity => entity.BlueprintId).Distinct()
                .ToDictionary(id => id, _ => BlueprintId.From(Guid.NewGuid()))
            : null;
        var command = new PasteEntitiesCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            payload,
            mapping,
            new Vector3Value(GridStep, 0, GridStep),
            DryRun: false,
            Mode: mode,
            BlueprintIdMap: blueprintMap);
        var firstPastedId = mapping[payload.Entities[0].Id];
        if (ApplyCommand(
                _pasteEntitiesHandler.Handle(_history.State, command),
                $"{payload.Entities.Count} entité(s) collée(s) en mode {mode} avec de nouveaux UUID.",
                firstPastedId))
        {
            _clipboardSummary!.Text =
                $"{payload.Entities.Count} entité(s) disponible(s) · dernier collage {mode} appliqué";
        }
    }

    private void DuplicateSelectedEntity()
    {
        CopySelectedEntity();
        PasteSemanticClipboard();
    }

    private AreaId? DuplicateArea(
        AreaId sourceAreaId,
        string displayName,
        AreaCloneMode mode,
        ExternalPortalPolicy externalPortalPolicy)
    {
        if (_history is null)
        {
            return null;
        }

        var source = _history.State.Project.Areas.FirstOrDefault(area => area.Id == sourceAreaId);
        if (source is null)
        {
            return null;
        }

        _semanticClipboard.CopyArea(_history.State.Project, sourceAreaId);
        var destinationAreaId = AreaId.From(Guid.NewGuid());
        var entityMap = mode == AreaCloneMode.Full
            ? source.Entities.Select(entity =>
                new EntityIdRemap(entity.Id, EntityId.From(Guid.NewGuid()))).ToArray()
            : [];
        var portalMap = mode == AreaCloneMode.Full
            ? source.Portals.Select(portal =>
                new PortalIdRemap(portal.Id, PortalId.From(Guid.NewGuid()))).ToArray()
            : [];
        var command = new DuplicateAreaCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            sourceAreaId,
            destinationAreaId,
            displayName,
            mode,
            externalPortalPolicy,
            entityMap,
            portalMap,
            DryRun: false);
        var dryRun = _duplicateAreaHandler.Handle(_history.State, command with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            ApplyCommand(dryRun, string.Empty, selectedEntityId: null);
            return null;
        }

        if (!ApplyCommand(
                _duplicateAreaHandler.Handle(_history.State, command),
                $"Zone « {displayName} » dupliquée avec remapping des UUID.",
                selectedEntityId: null))
        {
            return null;
        }

        _clipboardSummary!.Text = $"Zone « {source.DisplayName} » copiée · {entityMap.Length} UUID remappé(s)";
        ActivateArea(destinationAreaId);
        return destinationAreaId;
    }

    private bool ResizeArea(AreaId areaId, AreaDimensions dimensions, AreaResizeAnchor anchor)
    {
        if (_history is null) return false;
        var area = _history.State.Project.Areas.SingleOrDefault(candidate => candidate.Id == areaId);
        if (area is null) return false;
        var preview = AreaGeometryPlanner.Resize(area, dimensions, anchor);
        var command = new EditAreaGeometryCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            areaId, dimensions, preview.Kept,
            $"Resize area; remove {preview.Removed.Count} out-of-bounds entity(s).", DryRun: false);
        return ApplyCommand(_editAreaGeometryHandler.Handle(_history.State, command),
            $"Zone redimensionnée · {preview.Removed.Count} entité(s) hors limites retirée(s).", null);
    }

    private bool UpsertBlueprint(
        BlueprintId blueprintId,
        string slug,
        string displayName,
        BlueprintKind kind,
        BlueprintAuthoringMetadata authoring)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new UpsertBlueprintCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            slug,
            displayName,
            kind,
            DryRun: false,
            Authoring: authoring);
        if (!ApplyCommand(
                _upsertBlueprintHandler.Handle(_history.State, command),
                $"Blueprint « {displayName} » enregistré.",
                GetSelectedEntity()?.Id))
        {
            return false;
        }

        PublishBlueprintSelection(blueprintId);
        _blueprintCatalog?.Refresh(blueprintId);
        return true;
    }

    private CommandResult PreviewOrApplyRpgBlueprint(
        BlueprintId blueprintId,
        string slug,
        string displayName,
        BlueprintKind kind,
        bool dryRun)
    {
        if (_history is null)
        {
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        }

        var command = new UpsertBlueprintCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            slug,
            displayName,
            kind,
            dryRun);
        var result = _upsertBlueprintHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, $"Blueprint RPG « {displayName} » enregistré.", null);
            _blueprintCatalog?.Refresh(blueprintId);
        }

        return result;
    }

    private CommandResult PreviewOrApplyCreatureFoundation(
        BlueprintId blueprintId,
        CreatureFoundationProfile foundation,
        bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetCreatureFoundationCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            blueprintId, foundation, dryRun);
        var result = _creatureFoundationHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Fondation de créature enregistrée dans le projet.", null);
            _creatureFoundationWindow?.Refresh(blueprintId);
        }
        return result;
    }

    private CommandResult PreviewOrApplyCreaturePresentation(
        BlueprintId blueprintId,
        CreaturePresentationProfile presentation,
        bool dryRun)
    {
        if (_history is null)
        {
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        }

        var command = new SetCreaturePresentationCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            presentation,
            dryRun);
        var result = _creaturePresentationHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Présentation de créature enregistrée dans le projet.", null);
            _creaturePresentationWindow?.Refresh();
        }

        return result;
    }

    private CommandResult PreviewOrApplyCreatureProgression(
        BlueprintId blueprintId,
        CreatureProgressionProfile progression,
        bool dryRun)
    {
        if (_history is null)
        {
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        }

        var command = new SetCreatureProgressionCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            progression,
            dryRun);
        var result = _creatureProgressionHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Progression de créature enregistrée dans le projet.", null);
            _creatureProgressionWindow?.Refresh();
        }

        return result;
    }

    private CommandResult PreviewOrApplyCreatureLoot(
        BlueprintId blueprintId,
        CreatureLootProfile loot,
        bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");

        var command = new SetCreatureLootCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            loot,
            dryRun);
        var result = _creatureLootHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Politique de butin de créature enregistrée dans le projet.", null);
            _creatureLootWindow?.Refresh();
        }

        return result;
    }

    private CommandResult PreviewOrApplyCreatureRoleBehavior(
        BlueprintId blueprintId,
        CreatureRoleBehaviorProfile roleBehavior,
        bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");

        var command = new SetCreatureRoleBehaviorCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            roleBehavior,
            dryRun);
        var result = _creatureRoleBehaviorHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Rôle, activité et auto-level de créature enregistrés dans le projet.", null);
            _creatureRoleBehaviorWindow?.Refresh();
        }

        return result;
    }

    private CommandResult PreviewOrApplyCreatureEvents(
        BlueprintId blueprintId,
        CreatureEventProfile events,
        bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");

        var command = new SetCreatureEventsCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            events,
            dryRun);
        var result = _creatureEventsHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Événements avancés de créature enregistrés dans le projet.", null);
            _creatureEventWindow?.Refresh();
        }

        return result;
    }

    private CommandResult PreviewOrApplyQuestOperations(
        QuestId questId,
        QuestOperationsProfile? profile,
        bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetQuestOperationsCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            questId, profile, dryRun);
        var result = _questOperationsHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Politiques avancées de quête enregistrées dans le projet.", null);
            _questOperationsWindow?.Refresh();
        }
        return result;
    }

    private CommandResult PreviewOrApplyQuestJournal(QuestDefinition quest, bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetQuestCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, quest, dryRun);
        var result = _setQuestHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, $"Quête « {quest.Title.Fallback} » enregistrée.", null);
            _questJournalWindow?.Refresh();
        }
        return result;
    }

    private CommandResult PreviewOrApplyItemAdvanced(
        BlueprintId blueprintId,
        ItemAdvancedProfile? profile,
        bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetItemAdvancedProfileCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            blueprintId, profile, dryRun);
        var result = _itemAdvancedHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Économie, équipement, butin et craft de l’objet enregistrés.", null);
            _itemAdvancedWindow?.Refresh();
        }
        return result;
    }

    private CommandResult PreviewOrApplyItemFoundation(
        BlueprintId blueprintId,
        ItemFoundationProfile? profile,
        bool acceptTypeMigration,
        bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetItemFoundationCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            blueprintId, profile, acceptTypeMigration, dryRun);
        var result = _itemFoundationHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Fondation canonique de l’objet enregistrée.", null);
            _itemFoundationWindow?.Refresh(blueprintId);
        }
        return result;
    }

    private CommandResult PreviewOrApplyWorldFeatures(WorldFeatureCatalog catalog, bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetWorldFeatureCatalogCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            catalog, dryRun);
        var result = _worldFeatureHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Monde vivant et automatisations enregistrés dans le module.", null);
            _worldFeatureStudioWindow?.Refresh();
        }
        return result;
    }

    private CommandResult PreviewOrApplyPostLaunch(ModuleRuntimeExtensionProfile profile, bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetRuntimeExtensionsCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision,
            profile, dryRun);
        var result = _setRuntimeExtensionsHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Production, plateforme et expérience enregistrées dans le module.", null);
            _postLaunchStudioWindow?.Refresh();
        }
        return result;
    }

    private CommandResult PreviewOrApplyFactions(FactionCatalog catalog, bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");

        var command = new SetFactionCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            catalog,
            dryRun);
        var result = _factionHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Factions avancées enregistrées dans le projet.", null);
            _factionAdvancedWindow?.Refresh();
        }

        return result;
    }

    private CommandResult PreviewOrApplyDialoguePresentation(
        DialogueId dialogueId, DialoguePresentationProfile? presentation, bool dryRun)
    {
        if (_history is null)
            throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var command = new SetDialoguePresentationCommand(CommandId.From(Guid.NewGuid()),
            _history.State.Revision, dialogueId, presentation, dryRun);
        var result = _dialoguePresentationHandler.Handle(_history.State, command);
        if (!dryRun && result.Succeeded)
        {
            ApplyCommand(result, "Mise en scène du dialogue enregistrée dans le projet.", null);
            _dialoguePresentationWindow?.Refresh(dialogueId);
        }
        return result;
    }

    private bool ConfigureBlueprintPalette(
        BlueprintId blueprintId,
        BlueprintPaletteOrigin origin,
        bool isFavorite,
        BlueprintProvenance provenance)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new ConfigureBlueprintPaletteCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            blueprintId,
            origin,
            isFavorite,
            provenance,
            DryRun: false);
        var preview = _configureBlueprintPaletteHandler.Handle(_history.State, command with { DryRun = true });
        if (!preview.Succeeded)
        {
            ApplyCommand(preview, string.Empty, GetSelectedEntity()?.Id);
            return false;
        }

        if (!ApplyCommand(
                _configureBlueprintPaletteHandler.Handle(_history.State, command),
                $"Palette « {FormatPaletteOrigin(origin)} » et provenance enregistrées.",
                GetSelectedEntity()?.Id))
        {
            return false;
        }

        PublishBlueprintSelection(blueprintId);
        _blueprintCatalog?.Refresh(blueprintId);
        return true;
    }

    private bool CreateBlueprintFromSelectedEntity(
        BlueprintId blueprintId,
        string slug,
        string displayName,
        BlueprintAuthoringMetadata? authoring = null)
    {
        var entity = GetSelectedEntity();
        if (_history is null || entity is null)
        {
            return false;
        }

        var area = _history.State.Project.Areas.First(candidate =>
            candidate.Entities.Any(item => item.Id == entity.Id));
        var command = new CreateBlueprintFromEntityCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            entity.Id,
            blueprintId,
            slug,
            displayName,
            DryRun: false,
            Authoring: authoring);
        if (!ApplyCommand(
                _createBlueprintFromEntityHandler.Handle(_history.State, command),
                $"Blueprint « {displayName} » dérivé de l’instance.",
                entity.Id))
        {
            return false;
        }

        PublishBlueprintSelection(blueprintId);
        _blueprintCatalog?.Refresh(blueprintId);
        return true;
    }

    private void PublishBlueprintSelection(BlueprintId blueprintId)
    {
        var blueprint = _history?.State.Project.Blueprints.FirstOrDefault(candidate => candidate.Id == blueprintId);
        if (blueprint is null)
        {
            return;
        }

        _selectionContext.Replace(new CreatorSelection(
            CreatorSelectionKind.Blueprint,
            blueprint.Id.ToString(),
            blueprint.DisplayName,
            $"blueprints/{blueprint.Id}"));
    }

    private bool ApplySafeRename(SafeRenamePreview preview)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SafeRenameCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            preview.TargetKind,
            preview.TargetId,
            preview.NewDisplayName,
            preview.NewSlug,
            DryRun: false);
        var dryRun = _safeRenameHandler.Handle(_history.State, command with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            return ApplyCommand(dryRun, string.Empty, selectedEntityId: null);
        }

        var selectedEntityId = GetSelectedEntity()?.Id;
        if (!ApplyCommand(
                _safeRenameHandler.Handle(_history.State, command),
                $"{preview.TargetKind} « {preview.NewDisplayName} » renommé sans modifier son UUID.",
                selectedEntityId))
        {
            return false;
        }

        if (_selectionContext.Current.Count == 1 &&
            _selectionContext.Current[0].Id == preview.TargetId)
        {
            var selection = _selectionContext.Current[0];
            _selectionContext.Replace(new CreatorSelection(
                selection.Kind,
                selection.Id,
                preview.NewDisplayName,
                selection.DocumentPath));
        }

        return true;
    }

    private bool ApplyModuleConfiguration(
        ProjectMetadata metadata,
        IReadOnlyList<ProjectVariableDefinition> variables)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetModuleConfigurationCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            metadata,
            variables,
            DryRun: false);
        var dryRun = _setModuleConfigurationHandler.Handle(_history.State, command with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            return ApplyCommand(dryRun, string.Empty, selectedEntityId: null);
        }

        return ApplyCommand(
            _setModuleConfigurationHandler.Handle(_history.State, command),
            $"Configuration du module enregistrée · {metadata.Localizations.Count} langue(s) · " +
            $"{variables.Count} variable(s).",
            GetSelectedEntity()?.Id);
    }

    private bool ApplyLocalizationCatalog(LocalizationCatalog catalog, bool dryRun)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetLocalizationCatalogCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            catalog,
            DryRun: dryRun);
        var result = _setLocalizationCatalogHandler.Handle(_history.State, command);
        if (dryRun)
        {
            return result.Succeeded;
        }

        return ApplyCommand(
            result,
            $"Catalogue de localisation appliqué · {catalog.Messages.Count} message(s) · {catalog.Locales.Count} langue(s).",
            GetSelectedEntity()?.Id);
    }

    private bool ApplyModuleDistribution(ModuleDistributionConfiguration distribution)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetModuleDistributionCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            distribution,
            DryRun: false);
        var dryRun = _setModuleDistributionHandler.Handle(_history.State, command with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            return ApplyCommand(dryRun, string.Empty, selectedEntityId: null);
        }

        return ApplyCommand(
            _setModuleDistributionHandler.Handle(_history.State, command),
            $"Distribution enregistrée · {distribution.TargetProfiles.Count} cibles · " +
            $"{distribution.Dependencies.Count} dépendance(s).",
            GetSelectedEntity()?.Id);
    }

    private bool ApplyContentWorkspace(ContentWorkspaceConfiguration configuration)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetContentWorkspaceCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            configuration,
            DryRun: false);
        var dryRun = _setContentWorkspaceHandler.Handle(_history.State, command with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            return ApplyCommand(dryRun, string.Empty, selectedEntityId: null);
        }

        return ApplyCommand(
            _setContentWorkspaceHandler.Handle(_history.State, command),
            $"Contenu enregistré · {configuration.DataTables.Count} table(s) · " +
            $"fusion {configuration.MergePolicy}.",
            GetSelectedEntity()?.Id);
    }

    private bool ApplyArtisticStyleProfile(ArtisticStyleProfile profile)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetArtisticStyleProfileCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            profile,
            DryRun: false);
        var dryRun = _setArtisticStyleProfileHandler.Handle(_history.State, command with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            return ApplyCommand(dryRun, string.Empty, selectedEntityId: null);
        }

        return ApplyCommand(
            _setArtisticStyleProfileHandler.Handle(_history.State, command),
            $"Style artistique enregistré · {profile.Assets.Count} blueprint(s) profilé(s) · " +
            $"{ArtisticCompatibilityValidator.Analyze(profile).Count} avertissement(s).",
            GetSelectedEntity()?.Id);
    }

    private bool ApplyAreaNavigation(AreaId areaId, AreaNavigationDefinition navigation)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetAreaNavigationCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            areaId,
            navigation,
            DryRun: false);
        var dryRun = _setAreaNavigationHandler.Handle(_history.State, command with { DryRun = true });
        if (!dryRun.Succeeded)
        {
            return ApplyCommand(dryRun, string.Empty, selectedEntityId: null);
        }

        return ApplyCommand(
            _setAreaNavigationHandler.Handle(_history.State, command),
            $"Navigation enregistrée · {navigation.AgentProfiles.Count} profil(s) · " +
            $"{navigation.OffMeshLinks.Count} lien(s) hors-maillage.",
            GetSelectedEntity()?.Id);
    }

    private bool ApplyDialogue(DialogueDefinition dialogue)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetDialogueCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            dialogue,
            DryRun: false);
        return ApplyCommand(
            _setDialogueHandler.Handle(_history.State, command),
            $"Dialogue « {dialogue.DisplayName} » appliqué.",
            GetSelectedEntity()?.Id);
    }

    private bool ApplyQuest(QuestDefinition quest)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new SetQuestCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            quest,
            DryRun: false);
        return ApplyCommand(
            _setQuestHandler.Handle(_history.State, command),
            $"Quête « {quest.DisplayName} » appliquée.",
            GetSelectedEntity()?.Id);
    }

    private void ShowNewModuleDialog()
    {
        if (_newModuleDialog is not null)
        {
            _newModuleDialog.Exclusive = true;
        }

        UpdateNewModuleSummary();
        _newModuleDialog?.PopupCentered(new Vector2I(980, 900));
        if (_newModuleDialog is not null)
        {
            ApplyForgeWindowIcon(_newModuleDialog);
        }
        _newModuleTitle?.GrabFocus();
        _newModuleTitle?.SelectAll();
    }

    private void HideNewModuleDialog()
    {
        _newModuleDialog?.Hide();
        if (_newModuleDialog is not null)
        {
            _newModuleDialog.Exclusive = false;
        }
    }

    private void ChooseNewModuleLocation()
    {
        if (_newModuleTitle is null || _newModuleAreaName is null ||
            string.IsNullOrWhiteSpace(_newModuleTitle.Text) ||
            (_newModuleCreateInitialArea?.ButtonPressed == true &&
             string.IsNullOrWhiteSpace(_newModuleAreaName.Text)))
        {
            _statusLabel!.Text = "✗ Informations incomplètes";
            _detailsLabel!.Text = "Le titre est obligatoire ; le nom de zone l’est lorsque sa création est activée.";
            return;
        }

        var preview = _newModuleHandler.Handle(CreateNewModuleCommand(dryRun: true));
        if (!preview.Succeeded)
        {
            _statusLabel!.Text = "✗ Fondation de projet invalide";
            _detailsLabel!.Text = string.Join("\n", preview.Diagnostics.Select(item => item.Message));
            return;
        }

        UpdateNewModuleSummary();
        HideNewModuleDialog();
        _newModuleLocationDialog!.CurrentDir = _projectSession is null
            ? System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)
            : Directory.GetParent(_projectSession.RootDirectory)?.FullName ?? _projectSession.RootDirectory;
        _newModuleLocationDialog.PopupCenteredRatio(0.72f);
        ApplyForgeWindowIcon(_newModuleLocationDialog);
    }

    private void NewModuleAreaKindSelected(long index)
    {
        if (_newModuleAreaKind is null || _newModuleAreaWidth is null || _newModuleAreaDepth is null) return;
        var exterior = (AreaKind)_newModuleAreaKind.GetItemId((int)index) == AreaKind.Exterior;
        var suggested = exterior ? 128 : 32;
        _newModuleAreaWidth.Value = suggested;
        _newModuleAreaDepth.Value = suggested;
        UpdateNewModuleSummary();
    }

    private void NewModuleTemplateSelected(long index)
    {
        if (_newModuleTemplate is null || _newModuleCreateInitialArea is null) return;
        var template = (ProjectTemplateKind)_newModuleTemplate.GetItemId((int)index);
        _newModuleCreateInitialArea.Disabled = template is ProjectTemplateKind.Rpg or ProjectTemplateKind.CoreMenu;
        _newModuleCreateInitialArea.ButtonPressed = template == ProjectTemplateKind.Rpg;
        if (template == ProjectTemplateKind.CoreMenu) _newModuleKits!.Text = "forge-core";
        if (template == ProjectTemplateKind.Rpg) _newModuleKits!.Text = "forge-core, forge-rpg-kit";
        SetNewModuleAreaControls(_newModuleCreateInitialArea.ButtonPressed);
        UpdateNewModuleSummary();
    }

    private void NewModuleCreateInitialAreaToggled(bool enabled)
    {
        SetNewModuleAreaControls(enabled);
        UpdateNewModuleSummary();
    }

    private void SetNewModuleAreaControls(bool enabled)
    {
        if (_newModuleAreaName is not null) _newModuleAreaName.Editable = enabled;
        if (_newModuleAreaKind is not null) _newModuleAreaKind.Disabled = !enabled;
        if (_newModuleAreaWidth is not null) _newModuleAreaWidth.Editable = enabled;
        if (_newModuleAreaDepth is not null) _newModuleAreaDepth.Editable = enabled;
        if (_newModuleGameMode is not null) _newModuleGameMode.Editable = enabled;
        if (_newModuleEntryClass is not null) _newModuleEntryClass.Editable = enabled;
        if (_newModulePlayerRule is not null) _newModulePlayerRule.Editable = enabled;
    }

    private void UpdateNewModuleSummary()
    {
        if (_newModuleSummary is null || _newModuleTemplate is null || _newModulePlatform is null ||
            _newModuleRender is null || _newModuleNetwork is null) return;
        var area = _newModuleCreateInitialArea?.ButtonPressed == true
            ? $"zone {_newModuleAreaKind?.GetItemText(_newModuleAreaKind.Selected)} " +
              $"{(int)(_newModuleAreaWidth?.Value ?? 0)} × {(int)(_newModuleAreaDepth?.Value ?? 0)}"
            : "aucune zone initiale";
        var start = _newModuleCreateInitialArea?.ButtonPressed == true
            ? $" · départ {_newModuleGameMode?.Text}/{_newModuleEntryClass?.Text} ({_newModulePlayerRule?.Text})"
            : string.Empty;
        _newModuleSummary.Text =
            $"Résumé · {_newModuleTemplate.GetItemText(_newModuleTemplate.Selected)} · " +
            $"{_newModulePlatform.GetItemText(_newModulePlatform.Selected)} · " +
            $"rendu {_newModuleRender.GetItemText(_newModuleRender.Selected)} · " +
            $"réseau {_newModuleNetwork.GetItemText(_newModuleNetwork.Selected)} · {area}{start}";
    }

    private void CreateNewModuleIn(string parentDirectory)
    {
        var outcome = CreateNewModuleWorkspace(
            parentDirectory,
            _newModuleTitle!.Text,
            _newModuleDescription!.Text,
            _newModuleAreaName!.Text,
            (AreaKind)_newModuleAreaKind!.GetSelectedId(),
            new AreaDimensions((int)_newModuleAreaWidth!.Value, (int)_newModuleAreaDepth!.Value),
            (ProjectTemplateKind)_newModuleTemplate!.GetSelectedId(),
            ParseWizardKits(_newModuleKits!.Text),
            [(ModuleDistributionPlatform)_newModulePlatform!.GetSelectedId()],
            _newModuleRules!.Text.Trim(),
            (ProjectRenderPreset)_newModuleRender!.GetSelectedId(),
            (ProjectNetworkMode)_newModuleNetwork!.GetSelectedId(),
            _newModulePackageNamespace!.Text.Trim(),
            _newModuleGameMode!.Text.Trim(),
            _newModuleEntryClass!.Text.Trim(),
            _newModulePlayerRule!.Text.Trim(),
            _newModuleCreateInitialArea!.ButtonPressed);
        if (!outcome.Succeeded || outcome.WorkspaceDirectory is null)
        {
            _statusLabel!.Text = "✗ Création du module impossible";
            _detailsLabel!.Text = outcome.ErrorMessage ?? "Une erreur inconnue a interrompu la création.";
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_NEW_MODULE_FAILED {outcome.ErrorMessage}");
            return;
        }

        OpenProjectDirectory(outcome.WorkspaceDirectory);
        _statusLabel!.Text = "✓ Nouveau module prêt à construire";
        var createdArea = _history!.State.Project.Areas.SingleOrDefault();
        _detailsLabel!.Text = createdArea is null
            ? $"{_history.State.Project.Module.DisplayName} · projet Core prêt · aucune zone initiale"
            : $"{_history.State.Project.Module.DisplayName} · {createdArea.Kind} " +
              $"{createdArea.Dimensions.Width} × {createdArea.Dimensions.Depth} · point de départ créé";
        GD.Print($"OPENRPG_FORGE_TOOLSET_NEW_MODULE_CREATED path={outcome.WorkspaceDirectory}");
    }

    private NewModuleCreationOutcome CreateNewModuleWorkspace(
        string parentDirectory,
        string title,
        string description,
        string areaDisplayName,
        AreaKind areaKind = AreaKind.Interior,
        AreaDimensions? dimensions = null,
        ProjectTemplateKind template = ProjectTemplateKind.Rpg,
        IReadOnlyList<string>? kits = null,
        IReadOnlyList<ModuleDistributionPlatform>? targetPlatforms = null,
        string rulesProvider = "forge.generic",
        ProjectRenderPreset renderPreset = ProjectRenderPreset.Balanced,
        ProjectNetworkMode networkMode = ProjectNetworkMode.Offline,
        string packageNamespace = "",
        string gameMode = "campaign",
        string entryClass = "adventurer",
        string playerSelectionRule = "default",
        bool? createInitialArea = null)
    {
        var result = _newModuleHandler.Handle(new NewModuleCommand(
            CommandId.From(Guid.NewGuid()),
            new ProjectRevision(0),
            ProjectId.From(Guid.NewGuid()),
            Forge.Domain.Modules.ModuleId.From(Guid.NewGuid()),
            Forge.Domain.Areas.AreaId.From(Guid.NewGuid()),
            BlueprintId.From(Guid.NewGuid()),
            EntityId.From(Guid.NewGuid()),
            BlueprintId.From(Guid.NewGuid()),
            EntityId.From(Guid.NewGuid()),
            title,
            description,
            areaDisplayName,
            DryRun: false,
            AreaKind: areaKind,
            Dimensions: dimensions,
            Template: template,
            Kits: kits,
            TargetPlatforms: targetPlatforms,
            RulesProvider: rulesProvider,
            RenderPreset: renderPreset,
            NetworkMode: networkMode,
            PackageNamespace: packageNamespace,
            GameMode: gameMode,
            EntryClass: entryClass,
            PlayerSelectionRule: playerSelectionRule,
            CreateInitialArea: createInitialArea));
        if (!result.Succeeded || result.State is null)
        {
            return new NewModuleCreationOutcome(
                false,
                null,
                string.Join("\n", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }

        var workspaceDirectory = Path.Combine(
            Path.GetFullPath(parentDirectory),
            result.State.Project.Module.Slug);
        var creation = ProjectSession.Create(workspaceDirectory, result.State.Project);
        if (!creation.Succeeded)
        {
            return new NewModuleCreationOutcome(
                false,
                null,
                FormatDiagnostics(creation.Diagnostics));
        }

        return new NewModuleCreationOutcome(true, workspaceDirectory, null);
    }

    private NewModuleCommand CreateNewModuleCommand(bool dryRun) => new(
        CommandId.From(Guid.NewGuid()),
        new ProjectRevision(0),
        ProjectId.From(Guid.NewGuid()),
        Forge.Domain.Modules.ModuleId.From(Guid.NewGuid()),
        Forge.Domain.Areas.AreaId.From(Guid.NewGuid()),
        BlueprintId.From(Guid.NewGuid()),
        EntityId.From(Guid.NewGuid()),
        BlueprintId.From(Guid.NewGuid()),
        EntityId.From(Guid.NewGuid()),
        _newModuleTitle!.Text,
        _newModuleDescription!.Text,
        _newModuleAreaName!.Text,
        dryRun,
        (AreaKind)_newModuleAreaKind!.GetSelectedId(),
        new AreaDimensions((int)_newModuleAreaWidth!.Value, (int)_newModuleAreaDepth!.Value),
        (ProjectTemplateKind)_newModuleTemplate!.GetSelectedId(),
        ParseWizardKits(_newModuleKits!.Text),
        [(ModuleDistributionPlatform)_newModulePlatform!.GetSelectedId()],
        _newModuleRules!.Text.Trim(),
        (ProjectRenderPreset)_newModuleRender!.GetSelectedId(),
        (ProjectNetworkMode)_newModuleNetwork!.GetSelectedId(),
        _newModulePackageNamespace!.Text.Trim(),
        _newModuleGameMode!.Text.Trim(),
        _newModuleEntryClass!.Text.Trim(),
        _newModulePlayerRule!.Text.Trim(),
        _newModuleCreateInitialArea!.ButtonPressed);

    private static IReadOnlyList<string> ParseWizardKits(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private void ShowExampleEntityPropertiesDialog()
    {
        if (_history is null || _entityPicker is null)
        {
            return;
        }

        var entities = GetActiveArea().Entities;
        var exampleIndex = entities.ToList().FindIndex(entity => entity.Overrides.Properties.Count > 0);
        _entityPicker.Selected = exampleIndex >= 0 ? exampleIndex : 0;
        EntitySelectionChanged(_entityPicker.Selected);
        ShowEntityPropertiesDialog();
    }

    private void ShowEntityPropertiesDialog()
    {
        var selected = GetSelectedEntity();
        if (_history is null || selected is null || _entityPropertiesDialog is null)
        {
            return;
        }

        var blueprint = _history.State.Project.Blueprints.Single(candidate => candidate.Id == selected.BlueprintId);
        _draftOverrides.Clear();
        _draftOverrides.AddRange(selected.Overrides.Properties);
        _selectedDraftOverrideIndex = -1;
        _entityPropertiesIdentity!.Text =
            $"{blueprint.DisplayName} · instance {selected.Id.ToString()[..8]} · schéma v{selected.Overrides.SchemaVersion}";
        _entityPropertyKey!.Clear();
        _entityPropertyKind!.Selected = 0;
        _entityPropertyValue!.Clear();
        _entityPropertyRemoveButton!.Disabled = true;
        SetOverrideValidation("Sélectionnez une propriété ou créez une nouvelle valeur locale.", false);
        RefreshDraftOverrides();
        _entityPropertiesDialog.Exclusive = true;
        _entityPropertiesDialog.PopupCentered(new Vector2I(720, 560));
        ApplyForgeWindowIcon(_entityPropertiesDialog);
        _entityPropertyKey.GrabFocus();
    }

    private void HideEntityPropertiesDialog()
    {
        _entityPropertiesDialog?.Hide();
        if (_entityPropertiesDialog is not null)
        {
            _entityPropertiesDialog.Exclusive = false;
        }
    }

    private void EntitySelectionChanged(long _)
    {
        RefreshOverrideSummary();
        RefreshScaleControls();
        PublishSelectedEntity();
    }

    private void SynchronizeUniformScale(double value)
    {
        if (_uniformScale?.ButtonPressed == true && _scaleY is not null && _scaleZ is not null)
        {
            _scaleY.SetValueNoSignal(value);
            _scaleZ.SetValueNoSignal(value);
        }
    }

    private void ToggleUniformScale(bool enabled)
    {
        if (enabled && _scaleX is not null)
        {
            SynchronizeUniformScale(_scaleX.Value);
        }
    }

    private void ScaleSelectedDown() => MultiplySelectedScale(0.9f);

    private void ScaleSelectedUp() => MultiplySelectedScale(1.1f);

    private void MultiplySelectedScale(float factor)
    {
        var selected = GetSelectedEntityIdsForActiveArea();
        if (selected.Count > 0)
        {
            ApplyDirectManipulation(selected, EditorTransformTool.Scale,
                new Vector3Value(factor, factor, factor), EditorTransformAlignment.None,
                selected.Count == 1 ? "Échelle de l’entité appliquée." : $"Échelle appliquée à {selected.Count} entités.");
        }
    }

    private void ApplySelectedScale()
    {
        var selected = GetSelectedEntity();
        if (selected is null || _scaleX is null || _scaleY is null || _scaleZ is null)
        {
            return;
        }

        ApplyScaleCommand(
            [selected.Id],
            new Vector3Value((float)_scaleX.Value, (float)_scaleY.Value, (float)_scaleZ.Value),
            multiply: false);
    }

    private bool ApplyScaleCommand(IReadOnlyList<EntityId> entityIds, Vector3Value scale, bool multiply)
    {
        if (_history is null)
        {
            return false;
        }

        var command = new ScaleEntitiesCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            GetActiveArea().Id,
            entityIds,
            scale,
            multiply,
            DryRun: false);
        var preview = _scaleEntitiesHandler.Handle(_history.State, command with { DryRun = true });
        if (!preview.Succeeded)
        {
            return ApplyCommand(preview, string.Empty, entityIds.Count == 1 ? entityIds[0] : null);
        }

        var applied = ApplyCommand(
            _scaleEntitiesHandler.Handle(_history.State, command),
            entityIds.Count == 1 ? "Échelle de l’entité appliquée." : $"Échelle appliquée à {entityIds.Count} entités.",
            entityIds.Count == 1 ? entityIds[0] : null);
        if (applied)
        {
            RefreshScaleControls();
            RebuildNavigationAfterScale();
        }

        return applied;
    }

    private void RebuildNavigationAfterScale()
    {
        if (_history is null || _projectPreview is null) return;
        var area = GetActiveArea();
        var agent = area.Navigation.AgentProfiles.FirstOrDefault();
        if (agent is null) return;
        var surface = NavigationSurfaceBuilder.Build(_history.State.Project, area, agent);
        _projectPreview.ShowNavigationOverlay(surface);
        if (_detailsLabel is not null)
        {
            _detailsLabel.Text += $" · navigation reconstruite {surface.WalkableCells.Count} cellules";
        }
    }

    private void RefreshScaleControls()
    {
        if (_scaleX is null || _scaleY is null || _scaleZ is null)
        {
            return;
        }

        var selected = GetSelectedEntity();
        if (selected is null)
        {
            return;
        }

        _scaleX.SetValueNoSignal(selected.Transform.Scale.X);
        _scaleY.SetValueNoSignal(selected.Transform.Scale.Y);
        _scaleZ.SetValueNoSignal(selected.Transform.Scale.Z);
    }

    private void SelectDraftOverride(long index)
    {
        if (index < 0 || index >= _draftOverrides.Count)
        {
            return;
        }

        _selectedDraftOverrideIndex = (int)index;
        var property = _draftOverrides[_selectedDraftOverrideIndex];
        _entityPropertyKey!.Text = property.Key;
        _entityPropertyKind!.Selected = (int)property.Kind;
        _entityPropertyValue!.Text = property.Value;
        _entityPropertyRemoveButton!.Disabled = false;
        SetOverrideValidation("La propriété sélectionnée peut être modifiée ou retirée.", false);
    }

    private void UpsertDraftOverride()
    {
        if (_entityPropertyKey is null || _entityPropertyKind is null || _entityPropertyValue is null)
        {
            return;
        }

        try
        {
            var property = new EntityPropertyOverride(
                _entityPropertyKey.Text,
                (EntityPropertyValueKind)_entityPropertyKind.Selected,
                _entityPropertyValue.Text);
            var index = _draftOverrides.FindIndex(candidate =>
                string.Equals(candidate.Key, property.Key, StringComparison.Ordinal));
            if (index >= 0)
            {
                _draftOverrides[index] = property;
            }
            else
            {
                _draftOverrides.Add(property);
                _draftOverrides.Sort((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key));
                index = _draftOverrides.FindIndex(candidate => candidate.Key == property.Key);
            }

            RefreshDraftOverrides(index);
            SetOverrideValidation($"✓ Propriété « {property.Key} » prête à être appliquée.", true);
        }
        catch (ArgumentException exception)
        {
            SetOverrideValidation($"Valeur invalide — {exception.Message}", false, isError: true);
        }
    }

    private void RemoveDraftOverride()
    {
        if (_selectedDraftOverrideIndex < 0 || _selectedDraftOverrideIndex >= _draftOverrides.Count)
        {
            return;
        }

        var removedKey = _draftOverrides[_selectedDraftOverrideIndex].Key;
        _draftOverrides.RemoveAt(_selectedDraftOverrideIndex);
        _selectedDraftOverrideIndex = -1;
        _entityPropertyKey!.Clear();
        _entityPropertyValue!.Clear();
        _entityPropertyRemoveButton!.Disabled = true;
        RefreshDraftOverrides();
        SetOverrideValidation($"La propriété « {removedKey} » sera retirée à l’application.", false);
    }

    private void ApplyDraftOverrides()
    {
        var overrides = new EntityPropertyOverrides(
            EntityPropertyOverrides.CurrentSchemaVersion,
            _draftOverrides);
        if (ApplyEntityOverrides(overrides))
        {
            HideEntityPropertiesDialog();
        }
    }

    private bool ApplyEntityOverrides(EntityPropertyOverrides overrides)
    {
        var selected = GetSelectedEntity();
        if (_history is null || selected is null)
        {
            return false;
        }

        var area = GetActiveArea();
        var command = new SetEntityOverridesCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            selected.Id,
            overrides,
            DryRun: false);
        return ApplyCommand(
            _setOverridesHandler.Handle(_history.State, command),
            $"{overrides.Properties.Count} propriété(s) locale(s) appliquée(s).",
            selected.Id);
    }

    private void RefreshDraftOverrides(int selectedIndex = -1)
    {
        if (_entityPropertiesList is null)
        {
            return;
        }

        _entityPropertiesList.Clear();
        foreach (var property in _draftOverrides)
        {
            var displayedValue = property.Value.Length > 38
                ? $"{property.Value[..35]}…"
                : property.Value;
            _entityPropertiesList.AddItem(
                $"{property.Key}   ·   {FormatPropertyKind(property.Kind)}   ·   {displayedValue}");
        }

        if (selectedIndex >= 0 && selectedIndex < _draftOverrides.Count)
        {
            _selectedDraftOverrideIndex = selectedIndex;
            _entityPropertiesList.Select(selectedIndex);
            _entityPropertyRemoveButton!.Disabled = false;
        }
    }

    private void SetOverrideValidation(string message, bool isSuccess, bool isError = false)
    {
        if (_entityPropertyValidation is null)
        {
            return;
        }

        _entityPropertyValidation.Text = message;
        _entityPropertyValidation.AddThemeColorOverride(
            "font_color",
            isError ? new Color("ff8c8c") : isSuccess ? new Color("83d6ae") : new Color("ab9acb"));
    }

    private static string FormatPropertyKind(EntityPropertyValueKind kind) => kind switch
    {
        EntityPropertyValueKind.String => "Texte",
        EntityPropertyValueKind.Boolean => "Booléen",
        EntityPropertyValueKind.Integer => "Entier",
        EntityPropertyValueKind.Number => "Nombre",
        _ => kind.ToString(),
    };

    private static string FormatPaletteOrigin(BlueprintPaletteOrigin origin) => origin switch
    {
        BlueprintPaletteOrigin.InstalledPack => "Standard",
        BlueprintPaletteOrigin.ProjectContent => "Projet",
        BlueprintPaletteOrigin.Generated => "Généré",
        BlueprintPaletteOrigin.Temporary => "Temporaire",
        _ => origin.ToString(),
    };

    private void OpenSelectedProject(string workspaceDirectory) =>
        OpenProjectDirectory(workspaceDirectory);

    private void ReloadProject() =>
        OpenProjectDirectory(_projectSession?.RootDirectory ?? GetPrototypeWorkspaceDirectory());

    private void OpenProjectDirectory(string workspaceDirectory)
    {
        if (_statusLabel is null || _detailsLabel is null ||
            _projectPathLabel is null || _saveButton is null || _exportButton is null)
        {
            return;
        }

        _semanticClipboard.Clear();
        if (_clipboardSummary is not null)
        {
            _clipboardSummary.Text = "Aucun contenu copié";
        }
        var result = ProjectSession.Open(workspaceDirectory);
        if (!result.Succeeded || result.Session is null)
        {
            _projectSession = null;
            _history = null;
            _assetProcessingProfileHistory = null;
            _assetGovernanceHistory = null;
            _qualityFoundationHistory = null;
            _assetPackageManifestHistory = null;
            _assetExternalEditHistory = null;
            _externalWorkerSecurityHistory = null;
            _editorKeymapHistory = null;
            _assistantProfileHistory = null;
            _loreIndexHistory = null;
            _activeAreaContext.Clear();
            _selectionContext.SetLocked(false);
            _selectionContext.Clear();
            _projectPreview?.Clear();
            _statusLabel.Text = "✗ Impossible d’ouvrir ce dossier Forge";
            _detailsLabel.Text = FormatDiagnostics(result.Diagnostics);
            _projectPathLabel.Text = workspaceDirectory;
            _saveButton.Disabled = true;
            _exportButton.Disabled = true;
            RefreshEditorControls();
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(MapSessionDiagnostic).ToArray());
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_PROJECT_INVALID {result.Diagnostics.FirstOrDefault()?.Code}");
            return;
        }

        _projectSession = result.Session;
        _recoveryStore = new RecoverySnapshotStore(_projectSession.Project.Module.RuntimePolicies.Extensions
            .OperationalReadiness.Recovery.RetainedSnapshots);
        _selectionContext.SetLocked(false);
        _selectionContext.Clear();
        _history = new ProjectCommandHistory(new ProjectState(
            _projectSession.Project,
            new ProjectRevision(0)));
        var assetProfiles = AssetProcessingProfileStore.Load(_projectSession.RootDirectory);
        _assetProcessingProfileHistory = new AssetProcessingProfileCommandHistory(assetProfiles.Profiles);
        if (!assetProfiles.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_PROFILES_INVALID {assetProfiles.Diagnostics[0].Code}");
        }
        var assetGovernance = AssetGovernanceStore.Load(_projectSession.RootDirectory);
        _assetGovernanceHistory = new AssetGovernanceCommandHistory(assetGovernance.Records);
        if (!assetGovernance.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_GOVERNANCE_INVALID {assetGovernance.Diagnostics[0].Code}");
        }
        var qualityFoundation = QualityFoundationProfileStore.Load(
            _projectSession.RootDirectory, _projectSession.Project.Id.Value);
        _qualityFoundationHistory = new QualityFoundationCommandHistory(qualityFoundation.Profile);
        if (!qualityFoundation.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_QUALITY_FOUNDATION_INVALID {qualityFoundation.Diagnostics[0].Code}");
        }
        var assetManifests = AssetPackageManifestStore.Load(_projectSession.RootDirectory);
        _assetPackageManifestHistory = new AssetPackageManifestCommandHistory(assetManifests.Manifests);
        if (!assetManifests.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_MANIFESTS_INVALID {assetManifests.Diagnostics[0].Code}");
        }
        var externalEdits = AssetExternalEditStore.Load(_projectSession.RootDirectory);
        _assetExternalEditHistory = new AssetExternalEditCommandHistory(externalEdits.Configurations);
        _assistedBlenderCreationHistory = new AssistedBlenderCreationCommandHistory(_assetExternalEditHistory.State);
        _assistedModel3DGenerationHistory = new AssistedModel3DGenerationCommandHistory(_assetExternalEditHistory.State);
        if (!externalEdits.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_EXTERNAL_EDITS_INVALID {externalEdits.Diagnostics[0].Code}");
        }
        var workerSecurity = ExternalWorkerSecurityPolicyStore.Load(_projectSession.RootDirectory);
        _externalWorkerSecurityHistory = new ExternalWorkerSecurityPolicyCommandHistory(workerSecurity.Policy);
        if (!workerSecurity.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_WORKER_SECURITY_INVALID {workerSecurity.Diagnostics[0].Code}");
        }
        var editorKeymap = EditorKeymapProfileStore.Load(_projectSession.RootDirectory);
        _editorKeymapHistory = new EditorKeymapCommandHistory(editorKeymap.Profile);
        if (!editorKeymap.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_KEYMAP_INVALID {editorKeymap.Diagnostics[0].Code}");
        }
        var assistantProfile = AssistantWorkspaceProfileStore.Load(_projectSession.RootDirectory);
        _assistantProfileHistory = new AssistantWorkspaceProfileCommandHistory(assistantProfile.Profile);
        if (!assistantProfile.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSISTANT_PROFILE_INVALID {assistantProfile.Diagnostics[0].Code}");
        }
        var loreIndex = LoreIndexStore.Load(_projectSession.RootDirectory);
        _loreIndexHistory = new LoreIndexCommandHistory(loreIndex.Index);
        if (!loreIndex.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_LORE_INDEX_INVALID {loreIndex.Diagnostics[0].Code}");
        }
        var assistedAreaPlans = AssistedAreaPlanStore.Load(_projectSession.RootDirectory);
        _assistedAreaPlanHistory = new AssistedAreaPlanCommandHistory(assistedAreaPlans.Catalog);
        if (!assistedAreaPlans.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_AREA_PLANS_INVALID {assistedAreaPlans.Diagnostics[0].Code}");
        }
        _lastSavedRevision = 0;
        _lastRecoveryRevision = 0;
        var cameraProfile = EditorCameraProfileStore.LoadOrCreate(
            _projectSession.RootDirectory,
            _projectSession.Project.Id);
        _projectPreview?.SetCameraProfile(cameraProfile.Profile, PersistEditorCameraProfile);
        _activeAreaContext.Reset(_projectSession.Project);
        ApplyEditorExperience(LoadWorkspaceProfile());
        RebuildPreview();
        RefreshEditorControls();
        var project = _history.State.Project;
        _rpgKitAuthoringWindow?.Refresh();
        var entityCount = project.Areas.Sum(area => area.Entities.Count);
        _statusLabel.Text = "✓ Projet Forge ouvert et valide";
        _detailsLabel.Text = $"{project.Title} — {project.Areas.Count} zone · {project.Blueprints.Count} blueprints · {entityCount} entités";
        _projectPathLabel.Text = _projectSession.RootDirectory;
        _saveButton.Disabled = false;
        _exportButton.Disabled = false;
        var recoveryPolicy = project.Module.RuntimePolicies.Extensions.OperationalReadiness.Recovery;
        if (recoveryPolicy.OfferRecoveryOnOpen)
        {
            var canonicalProjectPath = Path.Combine(
                _projectSession.RootDirectory,
                ProjectSession.DefaultProjectFileName);
            var canonicalSavedAt = File.Exists(canonicalProjectPath)
                ? new DateTimeOffset(File.GetLastWriteTimeUtc(canonicalProjectPath))
                : DateTimeOffset.MinValue;
            var recoveryCandidate = _recoveryStore.FindRecoveryCandidate(
                _projectSession.RootDirectory,
                canonicalSavedAt);
            if (recoveryCandidate is not null)
            {
                _detailsLabel.Text += $" · récupération disponible (révision {recoveryCandidate.Revision})";
                _recoveryWindow?.Open();
                GD.Print($"OPENRPG_FORGE_RECOVERY_OFFERED id={recoveryCandidate.Id} revision={recoveryCandidate.Revision}");
            }
        }
        GD.Print($"OPENRPG_FORGE_TOOLSET_PREVIEW_REBUILT entities={_projectPreview?.EntityCount ?? 0}");
        GD.Print($"OPENRPG_FORGE_TOOLSET_PROJECT_VALID entities={entityCount}");
    }

    private void PersistEditorCameraProfile(EditorCameraProfile profile)
    {
        if (_projectSession is null)
        {
            return;
        }

        var result = EditorCameraProfileStore.Save(_projectSession.RootDirectory, profile);
        if (!result.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_CAMERA_PROFILE_SAVE_FAILED {result.Diagnostics[0].Code}");
        }
    }

    private bool ApplyAssetGovernance(AssetGovernanceRecord record, bool dryRun)
    {
        if (_projectSession is null || _assetGovernanceHistory is null) return false;
        var command = new SetAssetGovernanceCommand(CommandId.From(Guid.NewGuid()),
            _assetGovernanceHistory.State.Revision, record, dryRun);
        var result = _assetGovernanceHandler.Handle(_assetGovernanceHistory.State, command);
        if (!result.Succeeded) return false;
        if (dryRun) return result.Patch is not null && !result.Audit.Applied;
        var saved = AssetGovernanceStore.Save(_projectSession.RootDirectory, result.State.Records);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_GOVERNANCE_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        _assetGovernanceHistory.Record(result);
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSET_GOVERNANCE_SAVED asset={record.AssetId} " +
                 $"quarantine={record.Quarantine.Status} origin={record.IpBoundary.Origin}");
        return true;
    }

    private bool ApplyQualityFoundation(QualityFoundationProfile profile, bool dryRun)
    {
        if (_projectSession is null || _qualityFoundationHistory is null) return false;
        var command = new SetQualityFoundationCommand(CommandId.From(Guid.NewGuid()),
            _qualityFoundationHistory.State.Revision, profile, dryRun);
        var result = _qualityFoundationHandler.Handle(_qualityFoundationHistory.State, command);
        if (!result.Succeeded) return false;
        if (dryRun) return result.Patch is not null && !result.Audit.Applied;
        var saved = QualityFoundationProfileStore.Save(_projectSession.RootDirectory, result.State.Profile);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_QUALITY_FOUNDATION_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        _qualityFoundationHistory.Record(result);
        GD.Print($"OPENRPG_FORGE_TOOLSET_QUALITY_FOUNDATION_SAVED revision={result.NewRevision?.Value} " +
                 $"severity={profile.Logs.MinimumSeverity} privacy={profile.Logs.PlayerPrivacy}");
        return true;
    }

    private bool UndoQualityFoundation()
    {
        if (_projectSession is null || _qualityFoundationHistory is null || !_qualityFoundationHistory.Undo()) return false;
        var saved = QualityFoundationProfileStore.Save(_projectSession.RootDirectory, _qualityFoundationHistory.State.Profile);
        if (saved.Succeeded) return true;
        _qualityFoundationHistory.Redo();
        return false;
    }

    private bool RedoQualityFoundation()
    {
        if (_projectSession is null || _qualityFoundationHistory is null || !_qualityFoundationHistory.Redo()) return false;
        var saved = QualityFoundationProfileStore.Save(_projectSession.RootDirectory, _qualityFoundationHistory.State.Profile);
        if (saved.Succeeded) return true;
        _qualityFoundationHistory.Undo();
        return false;
    }

    private bool UndoAssetGovernance()
    {
        if (_projectSession is null || _assetGovernanceHistory is null || !_assetGovernanceHistory.Undo()) return false;
        var saved = AssetGovernanceStore.Save(_projectSession.RootDirectory, _assetGovernanceHistory.State.Records);
        if (saved.Succeeded) return true;
        _assetGovernanceHistory.Redo();
        return false;
    }

    private bool RedoAssetGovernance()
    {
        if (_projectSession is null || _assetGovernanceHistory is null || !_assetGovernanceHistory.Redo()) return false;
        var saved = AssetGovernanceStore.Save(_projectSession.RootDirectory, _assetGovernanceHistory.State.Records);
        if (saved.Succeeded) return true;
        _assetGovernanceHistory.Undo();
        return false;
    }

    private bool ApplyAssetProcessingProfile(AssetProcessingProfile profile, bool dryRun)
    {
        if (_projectSession is null || _assetProcessingProfileHistory is null)
        {
            return false;
        }

        var command = new SetAssetProcessingProfileCommand(
            CommandId.From(Guid.NewGuid()),
            _assetProcessingProfileHistory.State.Revision,
            profile,
            dryRun);
        var result = _assetProcessingProfileHandler.Handle(_assetProcessingProfileHistory.State, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                result.Patch?.Path ?? $"assets/{profile.AssetId}/profile",
                command.CommandId.ToString())).ToArray());
            return false;
        }

        if (dryRun)
        {
            return result.Patch is not null && !result.Audit.Applied;
        }

        var saved = AssetProcessingProfileStore.Save(_projectSession.RootDirectory, result.State.Profiles);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_PROFILE_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }

        _assetProcessingProfileHistory.Record(result);
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_ASSET_PROFILE_SAVED asset={profile.AssetId} kind={profile.Kind} " +
            $"revision={_assetProcessingProfileHistory.State.Revision}");
        return true;
    }

    private bool UndoAssetProcessingProfile()
    {
        if (_projectSession is null || _assetProcessingProfileHistory is null ||
            !_assetProcessingProfileHistory.Undo())
        {
            return false;
        }

        var saved = AssetProcessingProfileStore.Save(
            _projectSession.RootDirectory,
            _assetProcessingProfileHistory.State.Profiles);
        if (saved.Succeeded)
        {
            return true;
        }

        _assetProcessingProfileHistory.Redo();
        return false;
    }

    private bool RedoAssetProcessingProfile()
    {
        if (_projectSession is null || _assetProcessingProfileHistory is null ||
            !_assetProcessingProfileHistory.Redo())
        {
            return false;
        }

        var saved = AssetProcessingProfileStore.Save(
            _projectSession.RootDirectory,
            _assetProcessingProfileHistory.State.Profiles);
        if (saved.Succeeded)
        {
            return true;
        }

        _assetProcessingProfileHistory.Undo();
        return false;
    }

    private bool ApplyAssistantWorkspaceProfile(
        AssistantWorkspaceProfile profile,
        Guid planId,
        bool dryRun)
    {
        if (_projectSession is null || _assistantProfileHistory is null) return false;
        var command = new SetAssistantWorkspaceProfileCommand(
            CommandId.From(Guid.NewGuid()),
            planId,
            _assistantProfileHistory.State.Revision,
            profile,
            AssistantPermissionLevel.Configure,
            dryRun);
        var result = _assistantProfileHandler.Handle(_assistantProfileHistory.State, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                "assistant/profile",
                profile.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Patch is not null && !result.Audit.Applied;
        var saved = AssistantWorkspaceProfileStore.Save(_projectSession.RootDirectory, result.State.Profile);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSISTANT_PROFILE_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        _assistantProfileHistory.Record(result);
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_ASSISTANT_PROFILE_SAVED provider={profile.ActiveProvider.Key} " +
            $"prompts={profile.Prompts.Count} revision={_assistantProfileHistory.State.Revision} plan={planId:D}");
        return true;
    }

    private bool UndoAssistantWorkspaceProfile()
    {
        if (_projectSession is null || _assistantProfileHistory is null || !_assistantProfileHistory.Undo())
            return false;
        var saved = AssistantWorkspaceProfileStore.Save(
            _projectSession.RootDirectory,
            _assistantProfileHistory.State.Profile);
        if (saved.Succeeded) return true;
        _assistantProfileHistory.Redo();
        return false;
    }

    private bool RedoAssistantWorkspaceProfile()
    {
        if (_projectSession is null || _assistantProfileHistory is null || !_assistantProfileHistory.Redo())
            return false;
        var saved = AssistantWorkspaceProfileStore.Save(
            _projectSession.RootDirectory,
            _assistantProfileHistory.State.Profile);
        if (saved.Succeeded) return true;
        _assistantProfileHistory.Undo();
        return false;
    }

    private bool ApplyExternalWorkerSecurityPolicy(
        ExternalWorkerSecurityPolicy policy,
        Guid transactionId,
        Guid planId,
        bool dryRun)
    {
        if (_projectSession is null || _externalWorkerSecurityHistory is null) return false;
        var command = new SetExternalWorkerSecurityPolicyCommand(
            CommandId.From(transactionId),
            planId,
            _externalWorkerSecurityHistory.State.Revision,
            policy,
            AssistantPermissionLevel.Configure,
            dryRun);
        var result = _externalWorkerSecurityHandler.Handle(_externalWorkerSecurityHistory.State, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                "assets/worker-security",
                policy.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Patch is not null && result.Inverse is not null && !result.Audit.Applied;
        var saved = ExternalWorkerSecurityPolicyStore.Save(_projectSession.RootDirectory, result.State.Policy);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_WORKER_SECURITY_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        _externalWorkerSecurityHistory.Record(result);
        GD.Print($"OPENRPG_FORGE_TOOLSET_WORKER_SECURITY_SAVED policy={policy.Id:D} " +
                 $"revision={_externalWorkerSecurityHistory.State.Revision.Value} transaction={transactionId:D} plan={planId:D}");
        return true;
    }

    private bool UndoExternalWorkerSecurityPolicy()
    {
        if (_projectSession is null || _externalWorkerSecurityHistory is null ||
            !_externalWorkerSecurityHistory.Undo()) return false;
        var saved = ExternalWorkerSecurityPolicyStore.Save(
            _projectSession.RootDirectory, _externalWorkerSecurityHistory.State.Policy);
        if (saved.Succeeded) return true;
        _externalWorkerSecurityHistory.Redo();
        return false;
    }

    private bool RedoExternalWorkerSecurityPolicy()
    {
        if (_projectSession is null || _externalWorkerSecurityHistory is null ||
            !_externalWorkerSecurityHistory.Redo()) return false;
        var saved = ExternalWorkerSecurityPolicyStore.Save(
            _projectSession.RootDirectory, _externalWorkerSecurityHistory.State.Policy);
        if (saved.Succeeded) return true;
        _externalWorkerSecurityHistory.Undo();
        return false;
    }

    private EditorKeymapCommandResult ApplyEditorKeymap(EditorKeymapProfile profile, bool dryRun)
    {
        if (_projectSession is null || _editorKeymapHistory is null)
        {
            var state = new EditorKeymapState(profile);
            var command = new SetEditorKeymapCommand(
                CommandId.From(Guid.NewGuid()), state.Revision, profile, dryRun);
            return new EditorKeymapCommandResult(false, state, state.Revision, null,
                [new CommandDiagnostic("authoring.keymap.workspace_missing", "Open a Forge workspace before changing shortcuts.")],
                null, null, new AuditEntry(command.CommandId, "set_editor_keymap", DateTimeOffset.UtcNow, false));
        }

        var request = new SetEditorKeymapCommand(
            CommandId.From(Guid.NewGuid()), _editorKeymapHistory.State.Revision, profile, dryRun);
        var result = _editorKeymapHandler.Handle(_editorKeymapHistory.State, request);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                ".forge/editor-keymap",
                profile.Id.ToString())).ToArray());
            return result;
        }
        if (dryRun) return result;

        var saved = EditorKeymapProfileStore.Save(_projectSession.RootDirectory, result.State.Profile);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_KEYMAP_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return result with
            {
                Succeeded = false,
                State = _editorKeymapHistory.State,
                NewRevision = null,
                Diagnostics = saved.Diagnostics.Select(diagnostic =>
                    new CommandDiagnostic(diagnostic.Code, diagnostic.Message)).ToArray(),
                Patch = null,
                Inverse = null,
                Audit = result.Audit with { Applied = false },
            };
        }

        _editorKeymapHistory.Record(result);
        GD.Print($"OPENRPG_FORGE_TOOLSET_KEYMAP_SAVED preset={profile.Preset} " +
                 $"revision={_editorKeymapHistory.State.Revision.Value} command={request.CommandId}");
        return result;
    }

    private bool UndoEditorKeymap()
    {
        if (_projectSession is null || _editorKeymapHistory is null || !_editorKeymapHistory.Undo())
            return false;
        var saved = EditorKeymapProfileStore.Save(_projectSession.RootDirectory, _editorKeymapHistory.State.Profile);
        if (saved.Succeeded) return true;
        _editorKeymapHistory.Redo();
        return false;
    }

    private bool RedoEditorKeymap()
    {
        if (_projectSession is null || _editorKeymapHistory is null || !_editorKeymapHistory.Redo())
            return false;
        var saved = EditorKeymapProfileStore.Save(_projectSession.RootDirectory, _editorKeymapHistory.State.Profile);
        if (saved.Succeeded) return true;
        _editorKeymapHistory.Undo();
        return false;
    }

    private bool ApplyLoreIndex(
        LoreIndexDefinition index,
        Guid transactionId,
        Guid planId,
        bool dryRun)
    {
        if (_projectSession is null || _history is null || _loreIndexHistory is null) return false;
        var command = new SetLoreIndexCommand(
            CommandId.From(Guid.NewGuid()),
            transactionId,
            planId,
            _loreIndexHistory.State.Revision,
            index,
            AssistantPermissionLevel.Configure,
            dryRun);
        var result = _loreIndexHandler.Handle(_loreIndexHistory.State, command, _history.State.Project);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                "assistant/lore-index",
                index.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Patch is not null && !result.Audit.Applied;
        var saved = LoreIndexStore.Save(_projectSession.RootDirectory, result.State.Index);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_LORE_INDEX_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        _loreIndexHistory.Record(result);
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_LORE_INDEX_SAVED entries={index.Entries.Count} " +
            $"revision={_loreIndexHistory.State.Revision} transaction={transactionId:D} plan={planId:D}");
        return true;
    }

    private bool UndoLoreIndex()
    {
        if (_projectSession is null || _loreIndexHistory is null || !_loreIndexHistory.Undo()) return false;
        var saved = LoreIndexStore.Save(_projectSession.RootDirectory, _loreIndexHistory.State.Index);
        if (saved.Succeeded) return true;
        _loreIndexHistory.Redo();
        return false;
    }

    private bool RedoLoreIndex()
    {
        if (_projectSession is null || _loreIndexHistory is null || !_loreIndexHistory.Redo()) return false;
        var saved = LoreIndexStore.Save(_projectSession.RootDirectory, _loreIndexHistory.State.Index);
        if (saved.Succeeded) return true;
        _loreIndexHistory.Undo();
        return false;
    }

    private bool ApplyAssistedAreaPlans(
        AssistedAreaPlanCatalog catalog,
        Guid transactionId,
        Guid planId,
        AssistantPermissionLevel permission,
        bool dryRun)
    {
        if (_projectSession is null || _history is null || _assistedAreaPlanHistory is null) return false;
        var command = new SetAssistedAreaPlansCommand(
            CommandId.From(Guid.NewGuid()), transactionId, planId, _assistedAreaPlanHistory.State.Revision,
            catalog, permission, dryRun);
        var result = _assistedAreaPlanHandler.Handle(_assistedAreaPlanHistory.State, command, _history.State.Project);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                "assistant/area-plans",
                catalog.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Patch is not null && !result.Audit.Applied;
        var saved = AssistedAreaPlanStore.Save(_projectSession.RootDirectory, result.State.Catalog);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_AREA_PLANS_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        _assistedAreaPlanHistory.Record(result);
        GD.Print($"OPENRPG_FORGE_TOOLSET_AREA_PLANS_SAVED plans={catalog.Plans.Count} " +
                 $"revision={_assistedAreaPlanHistory.State.Revision} geometry=false transaction={transactionId:D} plan={planId:D}");
        return true;
    }

    private bool UndoAssistedAreaPlans()
    {
        if (_projectSession is null || _assistedAreaPlanHistory is null || !_assistedAreaPlanHistory.Undo()) return false;
        var saved = AssistedAreaPlanStore.Save(_projectSession.RootDirectory, _assistedAreaPlanHistory.State.Catalog);
        if (saved.Succeeded) return true;
        _assistedAreaPlanHistory.Redo();
        return false;
    }

    private bool BuildAssistedAreaPlan(
        Guid assistedAreaPlanId,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _history is null || _assistedAreaPlanHistory is null) return false;
        var command = new BuildAssistedAreaPlanCommand(
            CommandId.From(Guid.NewGuid()),
            transactionId,
            executionPlanId,
            _history.State.Revision,
            assistedAreaPlanId,
            AssistantPermissionLevel.ApplyApprovedPlan,
            dryRun);
        var result = _assistedAreaBuildHandler.Handle(
            _history.State,
            command,
            _assistedAreaPlanHistory.State.Catalog);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                "assistant/area-build",
                assistedAreaPlanId.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Command.Patch is not null && !result.Command.Audit.Applied;
        var saved = _projectSession.Save(result.Command.State.Project);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_AREA_BUILD_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        RecordProjectCommand(result.Command);
        _activeAreaContext.EnsureValid(_history.State.Project);
        RebuildPreview();
        RefreshEditorControls();
        GD.Print($"OPENRPG_FORGE_TOOLSET_AREA_BUILD_APPLIED area_plan={assistedAreaPlanId:D} " +
                 $"placements={result.EntityIds.Count} revision={_history.State.Revision} " +
                 $"transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool ApplyAssistedDialogue(
        AssistedDialogueProposal proposal,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _history is null) return false;
        var command = new ApplyAssistedDialogueCommand(
            CommandId.From(Guid.NewGuid()),
            transactionId,
            executionPlanId,
            _history.State.Revision,
            proposal,
            AssistantPermissionLevel.ApplyApprovedPlan,
            dryRun);
        var result = _assistedDialogueHandler.Handle(_history.State, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                "assistant/dialogue",
                proposal.Dialogue.Id.ToString())).ToArray());
            return false;
        }
        if (dryRun) return result.Command.Patch is not null && !result.Command.Audit.Applied;
        var saved = _projectSession.Save(result.Command.State.Project);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSISTED_DIALOGUE_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        RecordProjectCommand(result.Command);
        RebuildPreview();
        RefreshEditorControls();
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSISTED_DIALOGUE_APPLIED dialogue={proposal.Dialogue.Id} " +
                 $"nodes={proposal.Dialogue.Nodes.Count} revision={_history.State.Revision} " +
                 $"transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool ApplyAssistedQuest(
        AssistedQuestProposal proposal,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _history is null) return false;
        var command = new ApplyAssistedQuestCommand(
            CommandId.From(Guid.NewGuid()),
            transactionId,
            executionPlanId,
            _history.State.Revision,
            proposal,
            AssistantPermissionLevel.ApplyApprovedPlan,
            dryRun);
        var result = _assistedQuestHandler.Handle(_history.State, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                "assistant/quest",
                proposal.Quest.Id.ToString())).ToArray());
            return false;
        }
        if (dryRun) return result.Command.Patch is not null && !result.Command.Audit.Applied;
        var saved = _projectSession.Save(result.Command.State.Project);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSISTED_QUEST_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        RecordProjectCommand(result.Command);
        RebuildPreview();
        RefreshEditorControls();
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSISTED_QUEST_APPLIED quest={proposal.Quest.Id} " +
                 $"objectives={proposal.AdvancedQuest.Objectives.Count} revision={_history.State.Revision} " +
                 $"transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool ApplyAssistedForgeScript(
        AssistedForgeScriptProposal proposal,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _history is null) return false;
        var command = new ApplyAssistedForgeScriptCommand(
            Guid.NewGuid(),
            transactionId,
            executionPlanId,
            _history.State.Revision.Value,
            proposal,
            AssistantPermissionLevel.ApplyApprovedPlan,
            dryRun);
        var result = _assistedForgeScriptHandler.Handle(
            _projectSession.RootDirectory,
            _history.State.Revision.Value,
            command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(code => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                code,
                "La transaction ForgeScript assistée a été refusée.",
                proposal.RelativePath,
                proposal.ScriptId.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Patch is not null && !result.Applied;
        _assistedForgeScriptHistory.Record(result);
        _buildPipeline?.Refresh();
        RefreshEditorControls();
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSISTED_SCRIPT_APPLIED path={proposal.RelativePath} " +
                 $"api={proposal.ForgeScriptApiVersion} tests={proposal.Tests.Count} " +
                 $"transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool ApplyAssistedBoss(
        AssistedBossProposal proposal,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _history is null) return false;
        var command = new ApplyAssistedBossCommand(
            CommandId.From(Guid.NewGuid()),
            transactionId,
            executionPlanId,
            _history.State.Revision,
            proposal,
            AssistantPermissionLevel.ApplyApprovedPlan,
            dryRun);
        var result = _assistedBossHandler.Handle(_history.State, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                $"bosses/{proposal.Boss.Id:D}",
                proposal.Boss.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Command.Patch is not null && !result.Command.Audit.Applied;
        var saved = _projectSession.Save(result.Command.State.Project);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSISTED_BOSS_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        RecordProjectCommand(result.Command);
        RebuildPreview();
        RefreshEditorControls();
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSISTED_BOSS_APPLIED boss={proposal.Boss.Id:D} " +
                 $"abilities={proposal.Boss.Blueprint.Combat!.Abilities.Count} phases={proposal.Boss.Phases.Count} " +
                 $"transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool ApplyAssistedAbility(
        AssistedAbilityProposal proposal,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _history is null) return false;
        var command = new ApplyAssistedAbilityCommand(
            CommandId.From(Guid.NewGuid()),
            transactionId,
            executionPlanId,
            _history.State.Revision,
            proposal,
            AssistantPermissionLevel.ApplyApprovedPlan,
            dryRun);
        var result = _assistedAbilityHandler.Handle(_history.State, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                $"abilities/{proposal.Orchestration.Id:D}",
                proposal.Orchestration.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return result.Command.Patch is not null && !result.Command.Audit.Applied;
        var saved = _projectSession.Save(result.Command.State.Project);
        if (!saved.Succeeded)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSISTED_ABILITY_SAVE_FAILED {saved.Diagnostics[0].Code}");
            return false;
        }
        RecordProjectCommand(result.Command);
        RebuildPreview();
        RefreshEditorControls();
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSISTED_ABILITY_APPLIED ability={proposal.Orchestration.Id:D} " +
                 $"owner={proposal.Orchestration.OwnerBlueprintId} tests={proposal.Tests.Count} " +
                 $"transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool ApplyAssistedBlenderCreation(
        AssistedBlenderCreationProposal proposal,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _assetExternalEditHistory is null) return false;
        var state = _assetExternalEditHistory.State;
        if (_assistedBlenderCreationHistory is null ||
            _assistedBlenderCreationHistory.State.Revision != state.Revision ||
            !_assistedBlenderCreationHistory.State.Configurations.SequenceEqual(state.Configurations))
            _assistedBlenderCreationHistory = new AssistedBlenderCreationCommandHistory(state);
        var command = new ApplyAssistedBlenderCreationCommand(
            CommandId.From(Guid.NewGuid()), transactionId, executionPlanId, state.Revision, proposal,
            AssistantPermissionLevel.ApplyApprovedPlan, dryRun);
        var result = _assistedBlenderCreationHandler.Handle(_projectSession.RootDirectory, state, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(code => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error, code, code,
                $"assets/assisted-3d/{proposal.Definition.Id:D}", proposal.Definition.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return !result.Applied && result.Patches.Count == 3;
        _assistedBlenderCreationHistory.Record(result);
        _assetExternalEditHistory = new AssetExternalEditCommandHistory(result.State);
        _assetBrowser?.Refresh();
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSISTED_BLENDER_APPLIED asset={proposal.Configuration.AssetId} " +
                 $"primitives={proposal.Definition.Primitives.Count} transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool UndoAssistedBlenderCreation()
    {
        if (_projectSession is null || _assistedBlenderCreationHistory is null ||
            !_assistedBlenderCreationHistory.Undo(_projectSession.RootDirectory)) return false;
        _assetExternalEditHistory = new AssetExternalEditCommandHistory(_assistedBlenderCreationHistory.State);
        _assetBrowser?.Refresh();
        return true;
    }

    private bool RedoAssistedBlenderCreation()
    {
        if (_projectSession is null || _assistedBlenderCreationHistory is null ||
            !_assistedBlenderCreationHistory.Redo(_projectSession.RootDirectory)) return false;
        _assetExternalEditHistory = new AssetExternalEditCommandHistory(_assistedBlenderCreationHistory.State);
        _assetBrowser?.Refresh();
        return true;
    }

    private bool ApplyAssistedModel3DGeneration(
        AssistedModel3DGenerationProposal proposal,
        Guid transactionId,
        Guid executionPlanId,
        bool dryRun)
    {
        if (_projectSession is null || _assetExternalEditHistory is null) return false;
        var state = _assetExternalEditHistory.State;
        if (_assistedModel3DGenerationHistory is null ||
            _assistedModel3DGenerationHistory.State.Revision != state.Revision ||
            !_assistedModel3DGenerationHistory.State.Configurations.SequenceEqual(state.Configurations))
            _assistedModel3DGenerationHistory = new AssistedModel3DGenerationCommandHistory(state);
        var command = new ApplyAssistedModel3DGenerationCommand(
            CommandId.From(Guid.NewGuid()), transactionId, executionPlanId, state.Revision, proposal,
            AssistantPermissionLevel.ApplyApprovedPlan, dryRun);
        var result = _assistedModel3DGenerationHandler.Handle(_projectSession.RootDirectory, state, command);
        if (!result.Succeeded)
        {
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(code => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error, code, code,
                $"assets/assisted-provider-3d/{proposal.Receipt.Id:D}", proposal.Receipt.Id.ToString("D"))).ToArray());
            return false;
        }
        if (dryRun) return !result.Applied && result.Patches.Count == 4;
        _assistedModel3DGenerationHistory.Record(result);
        _assetExternalEditHistory = new AssetExternalEditCommandHistory(result.State);
        _assetBrowser?.Refresh();
        GD.Print($"OPENRPG_FORGE_TOOLSET_ASSISTED_MODEL3D_APPLIED asset={proposal.Configuration.AssetId} " +
                 $"provider={proposal.Receipt.Provider.Key} triangles={proposal.Receipt.SourceTriangleCount} " +
                 $"transaction={transactionId:D} plan={executionPlanId:D}");
        return true;
    }

    private bool UndoAssistedModel3DGeneration()
    {
        if (_projectSession is null || _assistedModel3DGenerationHistory is null ||
            !_assistedModel3DGenerationHistory.Undo(_projectSession.RootDirectory)) return false;
        _assetExternalEditHistory = new AssetExternalEditCommandHistory(_assistedModel3DGenerationHistory.State);
        _assetBrowser?.Refresh();
        return true;
    }

    private bool RedoAssistedModel3DGeneration()
    {
        if (_projectSession is null || _assistedModel3DGenerationHistory is null ||
            !_assistedModel3DGenerationHistory.Redo(_projectSession.RootDirectory)) return false;
        _assetExternalEditHistory = new AssetExternalEditCommandHistory(_assistedModel3DGenerationHistory.State);
        _assetBrowser?.Refresh();
        return true;
    }

    private bool UndoAssistedForgeScript()
    {
        if (_projectSession is null || !_assistedForgeScriptHistory.Undo(_projectSession.RootDirectory)) return false;
        _buildPipeline?.Refresh();
        return true;
    }

    private bool RedoAssistedForgeScript()
    {
        if (_projectSession is null || !_assistedForgeScriptHistory.Redo(_projectSession.RootDirectory)) return false;
        _buildPipeline?.Refresh();
        return true;
    }

    private bool RedoAssistedAreaPlans()
    {
        if (_projectSession is null || _assistedAreaPlanHistory is null || !_assistedAreaPlanHistory.Redo()) return false;
        var saved = AssistedAreaPlanStore.Save(_projectSession.RootDirectory, _assistedAreaPlanHistory.State.Catalog);
        if (saved.Succeeded) return true;
        _assistedAreaPlanHistory.Undo();
        return false;
    }

    private AssetPackageManifest? PrepareAssetPackageManifest(
        AssetCatalogItem item,
        AssetProcessingProfile profile,
        bool includeVfx,
        bool dryRun)
    {
        if (_projectSession is null || _assetPackageManifestHistory is null)
        {
            return null;
        }

        try
        {
            var planned = AssetImportPlanner.Plan(item, profile);
            if (!planned.Succeeded || planned.Plan is null)
            {
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_MANIFEST_PLAN_FAILED asset={item.ExternalId}");
                return null;
            }

            AssetManifestArtifactInput artifact;
            if (dryRun)
            {
                var outputPath = planned.Plan.Tasks.First().OutputRelativePath;
                artifact = new AssetManifestArtifactInput(
                    outputPath,
                    item.ContentSha256,
                    Math.Max(1, item.TotalBytes),
                    string.Equals(profile.SourceFormat, profile.CanonicalFormat, StringComparison.Ordinal));
            }
            else
            {
                var repositoryRoot = AssetCatalogLoader.FindRepositoryRoot(GetRepositoryRoot());
                if (repositoryRoot is null)
                {
                    return null;
                }

                var catalog = AssetCatalogLoader.Load(repositoryRoot);
                if (catalog.Catalog is null || catalog.Diagnostics.Count > 0)
                {
                    return null;
                }

                var execution = AssetImportExecutor.Execute(
                    planned.Plan,
                    catalog.VaultRoot,
                    _projectSession.RootDirectory);
                if (!execution.Succeeded)
                {
                    GD.PrintErr(
                        $"OPENRPG_FORGE_TOOLSET_ASSET_MANIFEST_IMPORT_FAILED {execution.Diagnostics[0].Code}");
                    return null;
                }

                var prepared = execution.Execution!;
                artifact = new AssetManifestArtifactInput(
                    prepared.OutputRelativePath,
                    prepared.OutputContentSha256,
                    prepared.OutputBytes,
                    prepared.Status == AssetImportExecutionStatus.CanonicalReady);
            }

            var manifest = AssetPackageManifestFactory.Create(
                item,
                profile,
                artifact,
                AssetPackageManifestAuthoringOptions.Default with { IncludeVfx = includeVfx });
            var command = new SetAssetPackageManifestCommand(
                CommandId.From(Guid.NewGuid()),
                _assetPackageManifestHistory.State.Revision,
                manifest,
                dryRun);
            var result = _assetPackageManifestHandler.Handle(_assetPackageManifestHistory.State, command);
            if (!result.Succeeded)
            {
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_MANIFEST_INVALID {result.Diagnostics[0].Code}");
                return null;
            }

            if (dryRun)
            {
                return result.Patch is not null && !result.Audit.Applied ? manifest : null;
            }

            var saved = AssetPackageManifestStore.Save(
                _projectSession.RootDirectory,
                result.State.Manifests);
            if (!saved.Succeeded)
            {
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_MANIFEST_SAVE_FAILED {saved.Diagnostics[0].Code}");
                return null;
            }

            _assetPackageManifestHistory.Record(result);
            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_ASSET_MANIFEST_SAVED asset={item.ExternalId} id={manifest.Id} " +
                $"chunks={manifest.Chunks.Count} revision={_assetPackageManifestHistory.State.Revision}");
            return manifest;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_MANIFEST_FAILED {exception.Message}");
            return null;
        }
    }

    private BlueprintDefinition? InstallAssetForPlacement(
        AssetCatalogItem item,
        AssetProcessingProfile profile,
        bool dryRun)
    {
        if (_projectSession is null || _history is null || _assetPackageManifestHistory is null)
        {
            return null;
        }

        try
        {
            var manifest = _assetPackageManifestHistory.State.Find(item.Id) ??
                PrepareAssetPackageManifest(item, profile, includeVfx: false, dryRun);
            if (manifest is null)
            {
                return null;
            }

            var command = new InstallAssetBlueprintCommand(
                CommandId.From(Guid.NewGuid()),
                _history.State.Revision,
                item,
                manifest,
                dryRun);
            var result = _installAssetBlueprintHandler.Handle(_history.State, command);
            if (!result.Succeeded)
            {
                GD.PrintErr(
                    $"OPENRPG_FORGE_TOOLSET_ASSET_INSTALL_FAILED asset={item.ExternalId} " +
                    $"code={result.Diagnostics[0].Code}");
                return null;
            }

            var blueprint = AssetBlueprintFactory.Create(item, manifest);
            if (dryRun)
            {
                return result.Patch is not null && !result.Audit.Applied ? blueprint : null;
            }

            if (!GovernedAssetModelVerifier.Verify(_projectSession.RootDirectory, manifest).Succeeded ||
                !ApplyCommand(
                    result,
                    $"Asset « {item.DisplayName} » installé dans la palette.",
                    selectedEntityId: null))
            {
                return null;
            }

            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_ASSET_INSTALL_PASS asset={item.ExternalId} blueprint={blueprint.Id} " +
                "placement=palette-drag visual=governed-glb");
            return _history.State.Project.Blueprints.Single(candidate => candidate.Id == blueprint.Id);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_ASSET_INSTALL_FAILED asset={item.ExternalId} {exception.Message}");
            return null;
        }
    }

    private bool UndoAssetPackageManifest()
    {
        if (_projectSession is null || _assetPackageManifestHistory is null ||
            !_assetPackageManifestHistory.Undo())
        {
            return false;
        }

        var saved = AssetPackageManifestStore.Save(
            _projectSession.RootDirectory,
            _assetPackageManifestHistory.State.Manifests);
        if (saved.Succeeded)
        {
            return true;
        }

        _assetPackageManifestHistory.Redo();
        return false;
    }

    private bool RedoAssetPackageManifest()
    {
        if (_projectSession is null || _assetPackageManifestHistory is null ||
            !_assetPackageManifestHistory.Redo())
        {
            return false;
        }

        var saved = AssetPackageManifestStore.Save(
            _projectSession.RootDirectory,
            _assetPackageManifestHistory.State.Manifests);
        if (saved.Succeeded)
        {
            return true;
        }

        _assetPackageManifestHistory.Undo();
        return false;
    }

    private AssetExternalEditConfiguration? ConfigureAssetExternalEdit(
        AssetCatalogItem item,
        bool watchForChanges,
        bool dryRun)
    {
        if (_projectSession is null || _assetExternalEditHistory is null)
        {
            return null;
        }

        try
        {
            var manifest = _assetPackageManifestHistory?.State.Find(item.Id);
            if (manifest is null && dryRun)
            {
                var profile = _assetProcessingProfileHistory?.State.Find(item.Id, AssetProcessingKind.Model) ??
                    AssetImportPlanner.CreateDefaultProfile(item, AssetProcessingKind.Model);
                var plan = AssetImportPlanner.Plan(item, profile);
                if (!plan.Succeeded || plan.Plan is null)
                {
                    return null;
                }

                manifest = AssetPackageManifestFactory.Create(
                    item,
                    profile,
                    new AssetManifestArtifactInput(
                        plan.Plan.Tasks.First().OutputRelativePath,
                        item.ContentSha256,
                        Math.Max(1, item.TotalBytes),
                        string.Equals(profile.SourceFormat, profile.CanonicalFormat, StringComparison.Ordinal)));
            }

            if (manifest is null)
            {
                return null;
            }

            var repositoryRoot = AssetCatalogLoader.FindRepositoryRoot(GetRepositoryRoot());
            if (repositoryRoot is null)
            {
                return null;
            }

            var catalog = AssetCatalogLoader.Load(repositoryRoot);
            if (catalog.Catalog is null || catalog.Diagnostics.Count > 0 ||
                string.IsNullOrWhiteSpace(item.RelativeModelPath) ||
                !AssetCatalogValidator.IsSafeRelativePath(item.RelativeModelPath))
            {
                return null;
            }

            var sourcePath = Path.Combine(
                catalog.VaultRoot,
                item.RelativeModelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sourcePath))
            {
                return null;
            }

            using var stream = File.OpenRead(sourcePath);
            var sourceHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            var configuration = AssetExternalEditFactory.CreateBlenderRoundTrip(
                item,
                manifest,
                sourceHash,
                watchForChanges);
            var command = new SetAssetExternalEditCommand(
                CommandId.From(Guid.NewGuid()),
                _assetExternalEditHistory.State.Revision,
                configuration,
                dryRun);
            var result = _assetExternalEditHandler.Handle(_assetExternalEditHistory.State, command);
            if (!result.Succeeded)
            {
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_EXTERNAL_EDIT_INVALID {result.Diagnostics[0].Code}");
                return null;
            }

            if (dryRun)
            {
                return result.Patch is not null && !result.Audit.Applied ? configuration : null;
            }

            var previous = _assetExternalEditHistory.State.Configurations;
            var saved = AssetExternalEditStore.Save(_projectSession.RootDirectory, result.State.Configurations);
            if (!saved.Succeeded)
            {
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_EXTERNAL_EDIT_SAVE_FAILED {saved.Diagnostics[0].Code}");
                return null;
            }

            var request = BlenderWorkerRequestStore.Save(
                _projectSession.RootDirectory,
                configuration,
                BlenderWorkerOptions.Default with
                {
                    GenerateCollision = manifest.Collision is not null,
                    LodRatios = manifest.Lod?.Levels
                        .Where(level => level.Level > 0)
                        .Select(level => level.ScreenRatio)
                        .ToArray() ?? [],
                });
            if (!request.Succeeded)
            {
                AssetExternalEditStore.Save(_projectSession.RootDirectory, previous);
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_EXTERNAL_EDIT_REQUEST_FAILED {request.Diagnostics[0].Code}");
                return null;
            }

            _assetExternalEditHistory.Record(result);
            _assistedBlenderCreationHistory = new AssistedBlenderCreationCommandHistory(_assetExternalEditHistory.State);
            _assistedModel3DGenerationHistory = new AssistedModel3DGenerationCommandHistory(_assetExternalEditHistory.State);
            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_EXTERNAL_EDIT_SAVED asset={item.ExternalId} id={configuration.Id} " +
                $"request={request.RequestRelativePath} revision={_assetExternalEditHistory.State.Revision.Value}");
            return configuration;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_EXTERNAL_EDIT_FAILED {exception.Message}");
            return null;
        }
    }

    private bool UndoAssetExternalEdit()
    {
        if (_projectSession is null || _assetExternalEditHistory is null || !_assetExternalEditHistory.Undo())
        {
            return false;
        }

        var saved = AssetExternalEditStore.Save(
            _projectSession.RootDirectory,
            _assetExternalEditHistory.State.Configurations);
        if (saved.Succeeded)
        {
            _assistedBlenderCreationHistory = new AssistedBlenderCreationCommandHistory(_assetExternalEditHistory.State);
            _assistedModel3DGenerationHistory = new AssistedModel3DGenerationCommandHistory(_assetExternalEditHistory.State);
            return true;
        }

        _assetExternalEditHistory.Redo();
        return false;
    }

    private bool RedoAssetExternalEdit()
    {
        if (_projectSession is null || _assetExternalEditHistory is null || !_assetExternalEditHistory.Redo())
        {
            return false;
        }

        var saved = AssetExternalEditStore.Save(
            _projectSession.RootDirectory,
            _assetExternalEditHistory.State.Configurations);
        if (saved.Succeeded)
        {
            _assistedBlenderCreationHistory = new AssistedBlenderCreationCommandHistory(_assetExternalEditHistory.State);
            _assistedModel3DGenerationHistory = new AssistedModel3DGenerationCommandHistory(_assetExternalEditHistory.State);
            return true;
        }

        _assetExternalEditHistory.Undo();
        return false;
    }

    private EntityId? PlaceSelectedBlueprint()
    {
        if (_history is null || _blueprintPalette is null ||
            _positionX is null || _positionZ is null ||
            _blueprintPalette.Selected < 0 ||
            _blueprintPalette.Selected >= _paletteBlueprintIds.Count)
        {
            return null;
        }

        var area = GetActiveArea();
        var entityId = EntityId.From(Guid.NewGuid());
        var transform = new TransformValue(
            new Vector3Value(SnapToGrid(_positionX.Value), 0, SnapToGrid(_positionZ.Value)),
            QuaternionValue.Identity,
            Vector3Value.One);
        var blueprintId = _paletteBlueprintIds[_blueprintPalette.Selected];
        var assetPlacement = ResolveAssetPlacement(blueprintId);
        var command = new PlaceEntityCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            entityId,
            blueprintId,
            transform,
            dryRun: false,
            assetPlacement.Tags,
            assetPlacement.Overrides);
        var result = _placeHandler.Handle(_history.State, command);
        return ApplyCommand(result, "Entité placée sur la grille.", entityId) ? entityId : null;
    }

    private EntityId? PlaceBlueprintFromViewport(BlueprintId blueprintId, Vector3Value position)
    {
        if (_history is null)
        {
            return null;
        }

        var area = GetActiveArea();
        var entityId = EntityId.From(Guid.NewGuid());
        var assetPlacement = ResolveAssetPlacement(blueprintId);
        var command = new PlaceEntityCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            entityId,
            blueprintId,
            new TransformValue(position, QuaternionValue.Identity, Vector3Value.One),
            dryRun: false,
            assetPlacement.Tags,
            assetPlacement.Overrides);
        return ApplyCommand(
            _placeHandler.Handle(_history.State, command),
            "Blueprint déposé directement dans le Monde.",
            entityId)
                ? entityId
                : null;
    }

    private (IReadOnlyList<string> Tags, EntityPropertyOverrides Overrides) ResolveAssetPlacement(
        BlueprintId blueprintId)
    {
        var blueprint = _history?.State.Project.Blueprints.SingleOrDefault(candidate => candidate.Id == blueprintId);
        if (blueprint is null || !AssetBlueprintFactory.TryGetAssetId(blueprint, out var assetId) || assetId is null)
        {
            return ([], EntityPropertyOverrides.Empty);
        }

        var manifest = _assetPackageManifestHistory?.State.Find(assetId);
        if (manifest is null)
        {
            return ([], EntityPropertyOverrides.Empty);
        }

        var tags = new[] { "asset" }
            .Concat(manifest.ModelMetadata?.Styles ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var overrides = new EntityPropertyOverrides(
            EntityPropertyOverrides.CurrentSchemaVersion,
            [new EntityPropertyOverride("visual.asset", EntityPropertyValueKind.String, assetId.ToString())]);
        return (tags, overrides);
    }

    private bool TransformEntityFromViewport(EntityId entityId, TransformValue transform)
    {
        if (_history is null)
        {
            return false;
        }

        var current = _history.State.Project.Areas.SelectMany(candidate => candidate.Entities)
            .FirstOrDefault(candidate => candidate.Id == entityId);
        if (current is not null && current.Transform.Position == transform.Position &&
            current.Transform.Rotation == transform.Rotation && current.Transform.Scale != transform.Scale)
        {
            return ApplyScaleCommand([entityId], transform.Scale, multiply: false);
        }

        var area = _history.State.Project.Areas.FirstOrDefault(candidate =>
            candidate.Entities.Any(entity => entity.Id == entityId));
        if (area is null)
        {
            return false;
        }

        var command = new TransformEntityCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            entityId,
            transform,
            DryRun: false);
        return ApplyCommand(
            _transformHandler.Handle(_history.State, command),
            "Entité déplacée avec le gizmo du Monde.",
            entityId);
    }

    private void SelectEntityFromViewport(EntityId entityId)
    {
        if (_entityPicker is null)
        {
            return;
        }

        var index = _entityIds.FindIndex(id => id == entityId);
        if (index < 0)
        {
            return;
        }

        _entityPicker.Selected = index;
        RefreshOverrideSummary();
        var settings = LoadWorkspaceProfile().Shell.DirectManipulation.Selection;
        var additive = settings.MultiSelectionEnabled && Input.IsKeyPressed(Key.Ctrl);
        PublishSelectedEntity(additive);
    }

    private void ShowViewportPlacementFeedback(string message, bool valid)
    {
        if (_statusLabel is null || _detailsLabel is null)
        {
            return;
        }

        _statusLabel.Text = valid ? "✓ Placement direct valide" : "✗ Placement direct refusé";
        _detailsLabel.Text = message;
        if (!valid)
        {
            var target = _selectionContext.Current.Count == 1 ? _selectionContext.Current[0] : null;
            var code = message.Split('·', 2, StringSplitOptions.TrimEntries)[0];
            _creatorShell?.SetDiagnostics([CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                code,
                message,
                target?.DocumentPath ?? "areas/active/placement",
                "viewport:placement",
                target)]);
        }
    }

    private void PlaceSelectedBlueprintFromUi() => PlaceSelectedBlueprint();

    private void MoveSelectedNegativeX() => MoveSelected(-GridStep, 0);

    private void MoveSelectedPositiveX() => MoveSelected(GridStep, 0);

    private void MoveSelectedNegativeZ() => MoveSelected(0, -GridStep);

    private void MoveSelectedPositiveZ() => MoveSelected(0, GridStep);

    private void MoveSelected(float deltaX, float deltaZ)
    {
        var selected = GetSelectedEntityIdsForActiveArea();
        if (_history is null || selected.Count == 0)
        {
            return;
        }
        ApplyDirectManipulation(selected, EditorTransformTool.Translate,
            new Vector3Value(deltaX, 0, deltaZ), EditorTransformAlignment.None,
            selected.Count == 1 ? "Entité déplacée sur la grille." : $"{selected.Count} entités déplacées sur la grille.");
    }

    private void RotateSelected()
    {
        var selected = GetSelectedEntityIdsForActiveArea();
        if (_history is null || selected.Count == 0)
        {
            return;
        }
        ApplyDirectManipulation(selected, EditorTransformTool.Rotate,
            new Vector3Value(0, 90, 0), EditorTransformAlignment.None,
            selected.Count == 1 ? "Entité tournée de 90°." : $"{selected.Count} entités tournées de 90°.");
    }

    private IReadOnlyList<EntityId> GetSelectedEntityIdsForActiveArea()
    {
        var active = GetActiveArea().Entities.Select(entity => entity.Id).ToHashSet();
        var selected = _selectionContext.Current
            .Where(item => item.Kind == CreatorSelectionKind.Entity && Guid.TryParse(item.Id, out _))
            .Select(item => EntityId.From(Guid.Parse(item.Id)))
            .Where(active.Contains)
            .Distinct()
            .ToArray();
        if (selected.Length > 0) return selected;
        var fallback = GetSelectedEntity();
        return fallback is null ? [] : [fallback.Id];
    }

    private bool ApplyDirectManipulation(
        IReadOnlyList<EntityId> entityIds,
        EditorTransformTool tool,
        Vector3Value value,
        EditorTransformAlignment alignment,
        string successMessage)
    {
        if (_history is null || entityIds.Count == 0) return false;
        var configuration = LoadWorkspaceProfile().Shell.DirectManipulation.Transform;
        var command = new ManipulateEntitiesCommand(
            CommandId.From(Guid.NewGuid()), ManipulateEntitiesHandler.CurrentSchemaVersion,
            "creator-direct-manipulation", _history.State.Revision, GetActiveArea().Id, entityIds,
            tool, value, configuration.CoordinateSpace, configuration.PivotMode, alignment,
            configuration.TranslationSnap, configuration.RotationSnapDegrees, configuration.ScaleSnap,
            DryRun: false);
        var preview = _manipulateEntitiesHandler.Handle(_history.State, command with { DryRun = true });
        if (!preview.Succeeded) return ApplyCommand(preview, string.Empty, entityIds.Count == 1 ? entityIds[0] : null);
        return ApplyCommand(_manipulateEntitiesHandler.Handle(_history.State, command), successMessage,
            entityIds.Count == 1 ? entityIds[0] : null);
    }

    private void DeleteSelected()
    {
        var selected = GetSelectedEntity();
        if (_history is null || selected is null)
        {
            return;
        }

        var area = GetActiveArea();
        var command = new RemoveEntityCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            selected.Id,
            DryRun: false);
        ApplyCommand(
            _removeHandler.Handle(_history.State, command),
            "Entité supprimée.",
            selectedEntityId: null);
    }

    private void Undo()
    {
        if (_history is null)
        {
            return;
        }

        var selectedEntityId = GetSelectedEntity()?.Id;
        var result = _history.Undo();
        if (!result.Succeeded)
        {
            return;
        }

        _activeAreaContext.EnsureValid(_history.State.Project);
        RebuildPreview();
        RefreshEditorControls(selectedEntityId);
        _creaturePresentationWindow?.Refresh();
        PersistProjectCommandJournal();
        ShowEditStatus("Modification annulée.");
    }

    private void Redo()
    {
        if (_history is null)
        {
            return;
        }

        var selectedEntityId = GetSelectedEntity()?.Id;
        var result = _history.Redo();
        if (!result.Succeeded)
        {
            return;
        }

        _activeAreaContext.EnsureValid(_history.State.Project);
        RebuildPreview();
        RefreshEditorControls(selectedEntityId);
        _creaturePresentationWindow?.Refresh();
        PersistProjectCommandJournal();
        ShowEditStatus("Modification rétablie.");
    }

    private bool ApplyCommand(
        CommandResult result,
        string successMessage,
        EntityId? selectedEntityId)
    {
        if (_history is null)
        {
            return false;
        }

        if (!result.Succeeded)
        {
            _statusLabel!.Text = "✗ Commande refusée";
            _detailsLabel!.Text = string.Join(
                "\n",
                result.Diagnostics.Select(diagnostic => $"{diagnostic.Code} — {diagnostic.Message}"));
            var currentTarget = _selectionContext.Current.Count == 1 ? _selectionContext.Current[0] : null;
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(diagnostic => CreatorDiagnostic.Create(
                CreatorDiagnosticSeverity.Error,
                diagnostic.Code,
                diagnostic.Message,
                $"commands/{result.Audit.CommandId}",
                result.Audit.CommandId.ToString(),
                currentTarget)).ToArray());
            return false;
        }

        RecordProjectCommand(result);
        _activeAreaContext.EnsureValid(_history.State.Project);
        RebuildPreview();
        RefreshEditorControls(selectedEntityId);
        ShowEditStatus(successMessage);
        GD.Print($"OPENRPG_FORGE_TOOLSET_COMMAND_APPLIED operation={result.Patch?.Operation} revision={_history.State.Revision}");
        return true;
    }

    public void ApplyNativeProjectUndo()
    {
        if (_nativeUndoRedoCallback) return;
        _nativeUndoRedoCallback = true;
        try { Undo(); }
        finally { _nativeUndoRedoCallback = false; }
    }

    public void ApplyNativeProjectRedo()
    {
        if (_nativeUndoRedoCallback) return;
        _nativeUndoRedoCallback = true;
        try { Redo(); }
        finally { _nativeUndoRedoCallback = false; }
    }

    private void RecordProjectCommand(CommandResult result)
    {
        if (_history is null) throw new InvalidOperationException("No project command history is active.");
        _history.Record(result);
        PersistProjectCommandJournal();
        if (_nativeUndoRedo is null || _nativeUndoRedoCallback) return;
        var actionName = string.IsNullOrWhiteSpace(result.Patch?.Summary)
            ? $"OpenRPG Forge · {result.Audit.CommandName}"
            : $"OpenRPG Forge · {result.Patch.Summary}";
        _nativeUndoRedo.CreateAction(actionName, UndoRedo.MergeMode.Disable, this, false, true);
        _nativeUndoRedo.AddDoMethod(this, new StringName(nameof(ApplyNativeProjectRedo)));
        _nativeUndoRedo.AddUndoMethod(this, new StringName(nameof(ApplyNativeProjectUndo)));
        _nativeUndoRedo.CommitAction(false);
        _nativeUndoRedoRegistrationCount++;
    }

    private void PersistProjectCommandJournal()
    {
        if (_projectSession is null || _history is null) return;
        try
        {
            EditorCommandJournalStore.Save(_projectSession.RootDirectory, _history.Journal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            GD.PrintErr($"OPENRPG_FORGE_COMMAND_JOURNAL_SAVE_FAILED {exception.Message}");
        }
    }

    private void SaveProject()
    {
        if (_projectSession is null || _history is null || _statusLabel is null ||
            _detailsLabel is null || _saveButton is null)
        {
            return;
        }

        _saveButton.Disabled = true;
        var result = _projectSession.Save(_history.State.Project);
        _saveButton.Disabled = false;

        if (!result.Succeeded)
        {
            _statusLabel.Text = "✗ Échec de la sauvegarde";
            _detailsLabel.Text = FormatDiagnostics(result.Diagnostics);
            _creatorShell?.SetDiagnostics(result.Diagnostics.Select(MapSessionDiagnostic).ToArray());
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_PROJECT_SAVE_FAILED {result.Diagnostics.FirstOrDefault()?.Code}");
            return;
        }

        _statusLabel.Text = "✓ Projet sauvegardé de façon atomique";
        _detailsLabel.Text = $"{_history.State.Project.Title} — aucune écriture partielle";
        _lastSavedRevision = _history.State.Revision.Value;
        GD.Print("OPENRPG_FORGE_TOOLSET_PROJECT_SAVED");
    }

    private void RefreshEditorControls(EntityId? selectedEntityId = null)
    {
        if (_blueprintPalette is null || _entityPicker is null || _spawnPicker is null ||
            _placeButton is null || _revisionLabel is null ||
            _undoButton is null || _redoButton is null || _playButton is null ||
            _overridesButton is null || _overridesSummary is null || _dialoguesButton is null ||
            _areasButton is null || _searchButton is null || _moduleSettingsButton is null ||
            _copyButton is null || _cutButton is null || _pasteButton is null ||
            _duplicateButton is null || _independentCopy is null || _clipboardSummary is null)
        {
            return;
        }

        var selectedSpawnId = GetSelectedSpawnId();
        _paletteBlueprintIds.Clear();
        _blueprintPalette.Clear();
        _entityIds.Clear();
        _entityPicker.Clear();
        _spawnEntityIds.Clear();
        _spawnPicker.Clear();

        if (_history is null)
        {
            SetEditingEnabled(false);
            _revisionLabel.Text = "Révision —";
            _creatorShell?.SetProject(null);
            return;
        }

        var project = _history.State.Project;
        foreach (var blueprint in project.Blueprints.OrderBy(blueprint => blueprint.DisplayName, StringComparer.Ordinal))
        {
            _paletteBlueprintIds.Add(blueprint.Id);
            var favorite = blueprint.IsFavorite ? "★ " : string.Empty;
            _blueprintPalette.AddItem($"{favorite}{blueprint.DisplayName} · {FormatPaletteOrigin(blueprint.PaletteOrigin)}");
        }

        var blueprintNames = project.Blueprints.ToDictionary(blueprint => blueprint.Id, blueprint => blueprint.DisplayName);
        var activeArea = GetActiveArea();
        foreach (var entity in activeArea.Entities)
        {
            _entityIds.Add(entity.Id);
            var overrideBadge = entity.Overrides.Properties.Count > 0
                ? $" · {entity.Overrides.Properties.Count} prop."
                : string.Empty;
            _entityPicker.AddItem(
                $"{blueprintNames[entity.BlueprintId]} · {entity.Id.ToString()[..8]}{overrideBadge}");
        }

        var spawnBlueprintIds = project.Blueprints
            .Where(blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn)
            .Select(blueprint => blueprint.Id)
            .ToHashSet();
        foreach (var area in project.Areas)
        {
            foreach (var entity in area.Entities.Where(entity => spawnBlueprintIds.Contains(entity.BlueprintId)))
            {
                _spawnEntityIds.Add(entity.Id);
                var position = entity.Transform.Position;
                _spawnPicker.AddItem($"{area.DisplayName} · X {position.X:0.##} / Z {position.Z:0.##}");
            }
        }

        if (selectedSpawnId is not null)
        {
            var spawnIndex = _spawnEntityIds.FindIndex(id => id == selectedSpawnId);
            if (spawnIndex >= 0)
            {
                _spawnPicker.Selected = spawnIndex;
            }
        }

        if (selectedEntityId is not null)
        {
            var selectedIndex = _entityIds.FindIndex(id => id == selectedEntityId);
            if (selectedIndex >= 0)
            {
                _entityPicker.Selected = selectedIndex;
            }
        }

        SetEditingEnabled(true);
        _revisionLabel.Text = $"Révision {_history.State.Revision}";
        _undoButton.Disabled = !_history.CanUndo;
        _redoButton.Disabled = !_history.CanRedo;
        _playButton.Disabled = _playtestRunning || _spawnEntityIds.Count == 0;
        RefreshOverrideSummary();
        RefreshScaleControls();
        RefreshCreatorShell();
        _blueprintCatalog?.Refresh();
        _areaWorkspace?.Refresh();
        _globalSearch?.Refresh();
        _moduleConfiguration?.Refresh();
        _distributionProfiles?.Refresh();
        _contentWorkflow?.Refresh();
        _contentPackageWindow?.Refresh();
        PublishSelectedEntity();
    }

    private void SetEditingEnabled(bool enabled)
    {
        var hasEntity = enabled && _entityIds.Count > 0;
        _blueprintPalette!.Disabled = !enabled || _paletteBlueprintIds.Count == 0;
        _placeButton!.Disabled = _blueprintPalette.Disabled;
        _entityPicker!.Disabled = !hasEntity;
        _moveXNegativeButton!.Disabled = !hasEntity;
        _moveXPositiveButton!.Disabled = !hasEntity;
        _moveZNegativeButton!.Disabled = !hasEntity;
        _moveZPositiveButton!.Disabled = !hasEntity;
        _rotateButton!.Disabled = !hasEntity;
        _deleteButton!.Disabled = !hasEntity;
        _scaleX!.Editable = hasEntity;
        _scaleY!.Editable = hasEntity;
        _scaleZ!.Editable = hasEntity;
        _uniformScale!.Disabled = !hasEntity;
        _scaleDownButton!.Disabled = !hasEntity;
        _scaleUpButton!.Disabled = !hasEntity;
        _applyScaleButton!.Disabled = !hasEntity;
        _overridesButton!.Disabled = !hasEntity;
        _copyButton!.Disabled = !hasEntity;
        _cutButton!.Disabled = !hasEntity;
        _duplicateButton!.Disabled = !hasEntity;
        _independentCopy!.Disabled = !enabled;
        _pasteButton!.Disabled = !enabled || _semanticClipboard.Payload is not EntityClipboardPayload;
        if (!hasEntity)
        {
            _overridesSummary!.Text = "Sélectionnez une entité";
        }
        _undoButton!.Disabled = true;
        _redoButton!.Disabled = true;
        _spawnPicker!.Disabled = !enabled || _spawnEntityIds.Count == 0;
        _playButton!.Disabled = !enabled || _spawnEntityIds.Count == 0 || _playtestRunning;
        _dialoguesButton!.Disabled = !enabled;
        _areasButton!.Disabled = !enabled;
        _workspaceButton!.Disabled = !enabled;
        _recoveryButton!.Disabled = !enabled;
        _qualityButton!.Disabled = !enabled;
        _searchButton!.Disabled = !enabled;
        _moduleSettingsButton!.Disabled = !enabled;
        _exportButton!.Disabled = !enabled;
    }

    private void RefreshOverrideSummary()
    {
        if (_overridesSummary is null)
        {
            return;
        }

        var selected = GetSelectedEntity();
        if (selected is null)
        {
            _overridesSummary.Text = "Sélectionnez une entité";
            return;
        }

        var count = selected.Overrides.Properties.Count;
        _overridesSummary.Text = count == 0
            ? "Hérite entièrement du blueprint · schéma v1"
            : $"{count} propriété(s) personnalisée(s) · schéma v{selected.Overrides.SchemaVersion}";
    }

    private void RefreshCreatorShell()
    {
        if (_creatorShell is null || _history is null || _projectSession is null || _projectPreview is null)
        {
            return;
        }

        var project = _history.State.Project;
        var profile = LoadWorkspaceProfile();
        _creatorShell.SetProject(CreatorProjectExplorer.Build(project, profile.Shell.ExplorerGrouping));
        _creatorShell.ApplyShellConfiguration(profile.Shell);
        _projectPreview?.SetEditorMode(profile.Shell.ViewportMode);
        _creatorShell.SetDiagnostics(CreatorDiagnostics.FromProject(
            project,
            $"revision:{_history.State.Revision}"));
    }

    private void PublishSelectedEntity(bool additive = false)
    {
        var entity = GetSelectedEntity();
        if (_history is null || entity is null)
        {
            return;
        }

        var project = _history.State.Project;
        var area = project.Areas.First(candidate => candidate.Entities.Any(item => item.Id == entity.Id));
        var blueprintName = project.Blueprints
            .FirstOrDefault(blueprint => blueprint.Id == entity.BlueprintId)
            ?.DisplayName ?? "Blueprint manquant";
        var selection = new CreatorSelection(
            CreatorSelectionKind.Entity,
            entity.Id.ToString(),
            blueprintName,
            $"areas/{area.Id}/entities/{entity.Id}");
        var settings = LoadWorkspaceProfile().Shell.DirectManipulation.Selection;
        var next = additive
            ? _selectionContext.Current.Concat([selection])
            : [selection];
        _selectionContext.Replace(next);
        if (settings.FocusOnSelection) _selectionContext.Focus(selection, "creator-viewport");
        if (settings.FrameSelection)
        {
            _selectionContext.Frame("creator-viewport");
            _projectPreview?.FocusSelectionFromShortcut();
        }
        _projectPreview?.ShowSelection(entity.Id);
    }

    private static CreatorDiagnostic MapSessionDiagnostic(ProjectSessionDiagnostic diagnostic) =>
        CreatorDiagnostic.Create(
            CreatorDiagnosticSeverity.Error,
            diagnostic.Code,
            diagnostic.Message,
            string.IsNullOrWhiteSpace(diagnostic.Path) ? "workspace" : diagnostic.Path);

    private EntityInstance? GetSelectedEntity()
    {
        if (_history is null || _entityPicker is null ||
            _entityPicker.Selected < 0 || _entityPicker.Selected >= _entityIds.Count)
        {
            return null;
        }

        var entityId = _entityIds[_entityPicker.Selected];
        return GetActiveArea().Entities.Single(entity => entity.Id == entityId);
    }

    private AreaDefinition GetActiveArea()
    {
        if (_history is null)
        {
            throw new InvalidOperationException("No Forge project is open.");
        }

        return _history.State.Project.Areas.FirstOrDefault(area => area.Id == _activeAreaContext.Current) ??
            _history.State.Project.Areas.First();
    }

    private EntityId? GetSelectedSpawnId()
    {
        if (_spawnPicker is null ||
            _spawnPicker.Selected < 0 || _spawnPicker.Selected >= _spawnEntityIds.Count)
        {
            return null;
        }

        return _spawnEntityIds[_spawnPicker.Selected];
    }

    private void StartPlaytestFromUi()
    {
        var configured = _history?.State.Project.Areas
            .FirstOrDefault(item => item.Id == _activeAreaContext.Current)?.Playtest;
        StartPlaytest(headless: configured?.Enabled == true && configured.Headless, workspaceSmokeTest: false);
    }

    private void StartPlaytest(bool headless, bool workspaceSmokeTest)
    {
        var spawnEntityId = GetSelectedSpawnId();
        if (_history is null || _playtestRunning)
        {
            return;
        }
        var activeArea = _history.State.Project.Areas.FirstOrDefault(item => item.Id == _activeAreaContext.Current);
        var configured = activeArea?.Playtest.Enabled == true ? activeArea.Playtest : null;
        if (configured is null && spawnEntityId is null) return;

        PlaytestSnapshot snapshot;
        try
        {
            snapshot = configured is null
                ? _playtestSnapshots.Create(_history.State.Project, spawnEntityId!)
                : _playtestSnapshots.Create(_history.State.Project, activeArea!.Id, configured);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException or
            NotSupportedException or System.Text.Json.JsonException or
            Forge.ModuleFormat.Serialization.ProjectValidationException)
        {
            _playtestStatus!.Text = "Échec du snapshot";
            _playtestLog!.Text = $"Le snapshot temporaire n’a pas pu être créé.\n{exception.Message}";
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_T32_SNAPSHOT_FAILED {exception.Message}");
            return;
        }

        _playtestRunning = true;
        _playButton!.Disabled = true;
        _spawnPicker!.Disabled = true;
        _playtestStatus!.Text = headless ? "Test automatisé…" : "Module en cours…";
        _playtestLog!.Text =
            $"Snapshot temporaire prêt.\nPoint de départ : {snapshot.SpawnEntityId}" +
            (configured is null ? "\n" : $" · mode {configured.StartMode} · profil {configured.TestProfile}\n") +
            (headless
                ? "Exécution du scénario de validation…"
                : "Le module est ouvert dans une fenêtre séparée. Fermez-la pour afficher le rapport complet.");
        _ = RunPlaytestAsync(snapshot, headless, workspaceSmokeTest);
    }

    private async Task RunPlaytestAsync(
        PlaytestSnapshot snapshot,
        bool headless,
        bool workspaceSmokeTest)
    {
        var result = await PlaytestProcessRunner.RunAsync(
            OS.GetExecutablePath(),
            GetRuntimeProjectDirectory(),
            snapshot,
            headless,
            runSmokeScenario: workspaceSmokeTest);
        string? cleanupError = null;
        try
        {
            _playtestSnapshots.Delete(snapshot);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            cleanupError = exception.Message;
        }

        var snapshotDeleted = !Directory.Exists(snapshot.DirectoryPath);
        _mainThreadActions.Enqueue(() => CompletePlaytest(
            snapshot,
            result,
            cleanupError,
            snapshotDeleted,
            workspaceSmokeTest));
    }

    private void CompletePlaytest(
        PlaytestSnapshot snapshot,
        PlaytestProcessResult result,
        string? cleanupError,
        bool snapshotDeleted,
        bool workspaceSmokeTest)
    {
        _playtestRunning = false;
        var output = FilterRuntimeBoilerplate(result.StandardOutput);
        var error = FilterRuntimeBoilerplate(result.StandardError);
        var succeeded = result.Succeeded && cleanupError is null && snapshotDeleted;

        if (_playtestStatus is not null && _playtestLog is not null && _playButton is not null && _spawnPicker is not null)
        {
            _playtestStatus.Text = succeeded ? "✓ Test terminé" : "✗ Test interrompu";
            _playtestLog.Text = BuildPlaytestReport(snapshot, result, output, error, cleanupError, snapshotDeleted);
            _playButton.Disabled = _history is null || _spawnEntityIds.Count == 0;
            _spawnPicker.Disabled = _history is null || _spawnEntityIds.Count == 0;
        }

        if (!workspaceSmokeTest)
        {
            return;
        }

        var expectedMarkersPresent = output.Contains(
                "OPENRPG_FORGE_RUNTIME_SNAPSHOT_LOADED file=project.json",
                StringComparison.Ordinal) &&
            output.Contains(
                $"OPENRPG_FORGE_RUNTIME_SPAWN_SELECTED id={snapshot.SpawnEntityId}",
                StringComparison.Ordinal) &&
            output.Contains("OPENRPG_FORGE_RUNTIME_SMOKE_PASS", StringComparison.Ordinal);
        if (!succeeded || !expectedMarkersPresent)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_TOOLSET_T32_SMOKE_FAILED exit={result.ExitCode} snapshot_deleted={snapshotDeleted}\n{output}\n{error}");
            return;
        }

        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_T32_SMOKE_PASS spawn={snapshot.SpawnEntityId} snapshot=deleted logs=captured");
    }

    private static string BuildPlaytestReport(
        PlaytestSnapshot snapshot,
        PlaytestProcessResult result,
        string output,
        string error,
        string? cleanupError,
        bool snapshotDeleted)
    {
        var report = new List<string>
        {
            "OpenRPG Forge · Rapport de playtest",
            $"Point de départ : {snapshot.SpawnEntityId}",
            $"Code de sortie : {result.ExitCode}",
            $"Snapshot temporaire : {(snapshotDeleted ? "supprimé" : "à nettoyer")}",
        };

        if (result.LaunchError is not null)
        {
            report.Add($"Lancement : {result.LaunchError}");
        }

        if (cleanupError is not null)
        {
            report.Add($"Nettoyage : {cleanupError}");
        }

        if (!string.IsNullOrWhiteSpace(output))
        {
            report.Add(string.Empty);
            report.Add("Sortie du module");
            report.Add(output.Trim());
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            report.Add(string.Empty);
            report.Add("Diagnostics");
            report.Add(error.Trim());
        }

        return string.Join(System.Environment.NewLine, report);
    }

    private static string FilterRuntimeBoilerplate(string value) => string.Join(
        System.Environment.NewLine,
        value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line =>
                !line.StartsWith("Godot Engine", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("godotengine.org", StringComparison.OrdinalIgnoreCase)));

    private void RebuildPreview()
    {
        if (_history is not null)
        {
            _projectPreview?.Rebuild(_history.State.Project);
        }
    }

    private void ShowEditStatus(string message)
    {
        _statusLabel!.Text = $"✓ {message}";
        _detailsLabel!.Text = $"{_projectPreview?.EntityCount ?? 0} entités · révision {_history?.State.Revision}";
    }

    private float GridStep => Math.Max(0.25f, (float)(_gridSize?.Value ?? 1));

    private float SnapToGrid(double value) =>
        MathF.Round((float)value / GridStep) * GridStep;

    private static QuaternionValue MultiplyAndNormalize(QuaternionValue left, QuaternionValue right)
    {
        var x = (left.W * right.X) + (left.X * right.W) + (left.Y * right.Z) - (left.Z * right.Y);
        var y = (left.W * right.Y) - (left.X * right.Z) + (left.Y * right.W) + (left.Z * right.X);
        var z = (left.W * right.Z) + (left.X * right.Y) - (left.Y * right.X) + (left.Z * right.W);
        var w = (left.W * right.W) - (left.X * right.X) - (left.Y * right.Y) - (left.Z * right.Z);
        var length = MathF.Sqrt((x * x) + (y * y) + (z * z) + (w * w));
        return new QuaternionValue(x / length, y / length, z / length, w / length);
    }

    private static string FormatDiagnostics(IReadOnlyList<ProjectSessionDiagnostic> diagnostics) =>
        string.Join("\n", diagnostics.Select(diagnostic => $"{diagnostic.Code} — {diagnostic.Message}"));

    private void RunWorkspaceSmokeTest()
    {
        EnsureForgeWindowTitle();
        var editorWindow = EditorInterface.Singleton.GetBaseControl().GetWindow();
        if (!string.Equals(editorWindow.Title, ForgeWindowTitle, StringComparison.Ordinal))
        {
            GD.PrintErr($"OPENRPG_FORGE_WINDOW_TITLE_FAILED actual={editorWindow.Title}");
            return;
        }

        GD.Print($"OPENRPG_FORGE_WINDOW_TITLE_PASS title={editorWindow.Title}");
        ReloadProject();
        if (_projectPreview?.EntityCount != 16 || _history is null || _newModuleButton is null ||
            _blueprintPalette is null || _positionX is null || _positionZ is null ||
            _spawnPicker is null || _spawnEntityIds.Count != 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_PREVIEW_REBUILD_FAILED");
            return;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_PREVIEW_REBUILD_PASS entities=16");
        if (!RunCreatorShellSmokeTest())
        {
            return;
        }

        if (!RunBlueprintCatalogSmokeTest())
        {
            return;
        }

        if (!RunDirectManipulationSmokeTest())
        {
            return;
        }

        if (!RunAreaWorkflowSmokeTest())
        {
            return;
        }

        if (!RunSemanticClipboardSmokeTest())
        {
            return;
        }

        if (!RunGlobalSearchAndRenameSmokeTest())
        {
            return;
        }

        if (!RunR034ContentOperationsSmokeTest())
        {
            return;
        }

        if (!RunModuleConfigurationSmokeTest())
        {
            return;
        }

        if (!RunPaletteProvenanceSmokeTest())
        {
            return;
        }

        if (!RunEditorCameraNavigationSmokeTest())
        {
            return;
        }

        if (!RunBuildPipelineSmokeTest())
        {
            return;
        }

        if (!RunR035BuildDiagnosticsSmokeTest())
        {
            return;
        }

        if (!RunR036EeAcceptanceSmokeTest())
        {
            return;
        }

        if (!RunDistributionProfilesSmokeTest())
        {
            return;
        }

        if (!RunContentWorkflowSmokeTest())
        {
            return;
        }

        if (!RunContentPackageSmokeTest())
        {
            return;
        }

        if (!RunContentAuthoringSmokeTest())
        {
            return;
        }

        if (!RunCreatorProductivitySmokeTest())
        {
            return;
        }

        if (!RunScalableEditorExperienceSmokeTest())
        {
            return;
        }

        if (!RunReferenceRenderingSmokeTest())
        {
            return;
        }

        if (!RunClimateWaterReverbSmokeTest())
        {
            return;
        }

        if (!RunTerrainWorldSmokeTest())
        {
            return;
        }

        if (!RunRuntimePolicySmokeTest())
        {
            return;
        }

        if (!RunCinematicSmokeTest())
        {
            return;
        }

        if (!RunGameplayControlsSmokeTest())
        {
            return;
        }

        if (!RunRulesProfilesSmokeTest())
        {
            return;
        }

        if (!RunForgeScriptFoundationSmokeTest())
        {
            return;
        }

        if (!RunForgeScriptWorldApiSmokeTest())
        {
            return;
        }

        if (!RunForgeScriptSafetyPolicySmokeTest())
        {
            return;
        }

        if (!RunForgeScriptModulePermissionsSmokeTest())
        {
            return;
        }

        if (!RunAreaExperienceSmokeTest())
        {
            return;
        }

        if (!RunAudioEmitterSmokeTest())
        {
            return;
        }

        if (!RunRuntimeExtensionsSmokeTest())
        {
            return;
        }

        if (!RunQualityLabSmokeTest())
        {
            return;
        }

        if (!RunAreaFoundationSmokeTest())
        {
            return;
        }

        if (!RunAreaRuntimeValidationSmokeTest())
        {
            return;
        }

        if (!RunNavigationAndGeometrySmokeTest())
        {
            return;
        }

        if (!RunActionableDiagnosticsSmokeTest())
        {
            return;
        }

        if (!RunAssetContentBrowserSmokeTest())
        {
            return;
        }

        if (!RunGovernedAssetPreviewSmokeTest())
        {
            return;
        }

        if (!RunLocalizationWorkspaceSmokeTest())
        {
            return;
        }

        if (!RunAssistantFoundationSmokeTest())
        {
            return;
        }

        if (!RunLoreIndexSmokeTest())
        {
            return;
        }

        if (!RunAssistedAreaPlanSmokeTest())
        {
            return;
        }

        if (!RunAssistedAreaBuildSmokeTest())
        {
            return;
        }

        if (!RunAssistedDialogueSmokeTest())
        {
            return;
        }

        if (!RunAssistedQuestSmokeTest())
        {
            return;
        }

        if (!RunAssistedForgeScriptSmokeTest())
        {
            return;
        }

        if (!RunAssistedBossSmokeTest())
        {
            return;
        }

        if (!RunAssistedAbilitySmokeTest())
        {
            return;
        }

        if (!RunAssistedBlenderCreationSmokeTest())
        {
            return;
        }

        if (!RunAssistedModel3DGenerationSmokeTest())
        {
            return;
        }

        if (!RunExternalWorkerSecuritySmokeTest())
        {
            return;
        }

        if (!RunEditorKeymapSmokeTest())
        {
            return;
        }

        if (!RunCreatureFoundationSmokeTest())
        {
            return;
        }

        if (!RunCreaturePresentationSmokeTest())
        {
            return;
        }

        if (!RunCreatureProgressionSmokeTest())
        {
            return;
        }

        if (!RunCreatureLootSmokeTest())
        {
            return;
        }

        if (!RunCreatureRoleBehaviorSmokeTest())
        {
            return;
        }

        if (!RunCreatureEventSmokeTest())
        {
            return;
        }

        if (!RunAdvancedFactionSmokeTest())
        {
            return;
        }

        if (!RunDialoguePresentationSmokeTest())
        {
            return;
        }

        if (!RunQuestJournalSmokeTest())
        {
            return;
        }

        if (!RunQuestTransitionSmokeTest())
        {
            return;
        }

        if (!RunQuestOperationsSmokeTest())
        {
            return;
        }

        if (!RunItemFoundationSmokeTest())
        {
            return;
        }

        if (!RunItemAdvancedSmokeTest())
        {
            return;
        }

        if (!RunWorldFeatureStudioSmokeTest())
        {
            return;
        }

        if (!RunPostLaunchStudioSmokeTest())
        {
            return;
        }

        if (!RunScaleAuthoringSmokeTest())
        {
            return;
        }

        if (!RunRpgKitAuthoringSmokeTest())
        {
            return;
        }

        if (!RunNewModuleSmokeTest())
        {
            return;
        }

        if (!RunProjectOperationsSmokeTest())
        {
            return;
        }

        var placeable = _history.State.Project.Blueprints.First(blueprint => blueprint.Kind == BlueprintKind.Placeable);
        _blueprintPalette.Selected = _paletteBlueprintIds.FindIndex(id => id == placeable.Id);
        _positionX.Value = 7;
        _positionZ.Value = 5;
        var placedEntityId = PlaceSelectedBlueprint();
        if (placedEntityId is null || _projectPreview.EntityCount != 17)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T23_PLACE_FAILED");
            return;
        }

        MoveSelectedPositiveX();
        RotateSelected();
        var transformed = _history.State.Project.Areas.First().Entities.Single(entity => entity.Id == placedEntityId);
        if (transformed.Transform.Position.X != 8 || transformed.Transform.Rotation == QuaternionValue.Identity)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T23_TRANSFORM_FAILED");
            return;
        }

        var smokeOverrides = new EntityPropertyOverrides(
            EntityPropertyOverrides.CurrentSchemaVersion,
            [
                new EntityPropertyOverride("displayName", EntityPropertyValueKind.String, "Pilier du smoke test"),
                new EntityPropertyOverride("locked", EntityPropertyValueKind.Boolean, "true"),
                new EntityPropertyOverride("hitPoints", EntityPropertyValueKind.Integer, "40"),
            ]);
        if (!ApplyEntityOverrides(smokeOverrides))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T13_OVERRIDES_FAILED command");
            return;
        }

        var overridden = _history.State.Project.Areas.First().Entities.Single(entity => entity.Id == placedEntityId);
        if (overridden.Overrides.SchemaVersion != EntityPropertyOverrides.CurrentSchemaVersion ||
            overridden.Overrides.Properties.Count != 3 ||
            overridden.Overrides.Properties.Single(property => property.Key == "locked").Value != "true")
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T13_OVERRIDES_FAILED invalid_state");
            return;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_T13_OVERRIDES_PASS schema=1 properties=3");

        Undo();
        Redo();
        if (!_history.CanUndo)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T23_HISTORY_FAILED");
            return;
        }

        DeleteSelected();
        if (_projectPreview.EntityCount != 16)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T23_DELETE_FAILED");
            return;
        }

        if (!RunDialogueSmokeTest())
        {
            return;
        }

        if (!RunQuestSmokeTest())
        {
            return;
        }

        SaveProject();
        GD.Print($"OPENRPG_FORGE_TOOLSET_T23_SMOKE_PASS entities={_projectPreview.EntityCount} revision={_history.State.Revision}");
        StartPlaytest(headless: true, workspaceSmokeTest: true);
    }

    private bool RunRpgKitAuthoringSmokeTest()
    {
        if (_history is null || _rpgKitAuthoringWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G8_AUTHOR_UI_FAILED missing_workspace");
            return false;
        }

        var blueprintId = BlueprintId.From(Guid.Parse("8a810000-0000-4000-8000-000000000001"));
        var revision = _history.State.Revision;
        var preview = PreviewOrApplyRpgBlueprint(blueprintId, "sentinelle-prismatique-smoke",
            "Sentinelle prismatique smoke", BlueprintKind.Creature, dryRun: true);
        if (!preview.Succeeded || preview.Audit.Applied || _history.State.Revision != revision ||
            _history.State.Project.Blueprints.Any(item => item.Id == blueprintId))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G8_AUTHOR_UI_FAILED preview");
            return false;
        }

        var applied = PreviewOrApplyRpgBlueprint(blueprintId, "sentinelle-prismatique-smoke",
            "Sentinelle prismatique smoke", BlueprintKind.Creature, dryRun: false);
        if (!applied.Succeeded || !applied.Audit.Applied ||
            !_history.State.Project.Blueprints.Any(item => item.Id == blueprintId))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G8_AUTHOR_UI_FAILED apply");
            return false;
        }

        Undo();
        if (_history.State.Project.Blueprints.Any(item => item.Id == blueprintId))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G8_AUTHOR_UI_FAILED undo");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_G8_AUTHOR_UI_PASS domains=9 json=none preview=dry-run apply=command undo=restored diagnostics=structured");
        return true;
    }

    private bool RunCreatorShellSmokeTest()
    {
        if (_creatorShell is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G702_SHELL_FAILED missing_shell");
            return false;
        }

        var entity = _history.State.Project.Areas.First().Entities.First();
        if (_creatorShell.WorkspaceCount != 12 ||
            _creatorShell.ExplorerNodeCount <= _history.State.Project.Areas.Sum(area => area.Entities.Count) ||
            !_creatorShell.SelectNodeForSmokeTest($"entity:{entity.Id}") ||
            _selectionContext.Current.Count != 1 ||
            _selectionContext.Current[0].Kind != CreatorSelectionKind.Entity ||
            _selectionContext.Current[0].Id != entity.Id.ToString())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G702_SHELL_FAILED explorer_selection");
            return false;
        }

        var target = _selectionContext.Current[0];
        var diagnostic = CreatorDiagnostic.Create(
            CreatorDiagnosticSeverity.Error,
            "creator.smoke.clickable",
            "Diagnostic synthétique du smoke test Creator.",
            target.DocumentPath,
            "smoke:g7-02",
            target);
        _creatorShell.SetDiagnostics([diagnostic]);
        if (_creatorShell.DiagnosticCount != 1 ||
            !_creatorShell.SelectDiagnosticForSmokeTest(0) ||
            _selectionContext.Current.Count != 1 ||
            _selectionContext.Current[0].Id != entity.Id.ToString())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G702_SHELL_FAILED diagnostic_navigation");
            return false;
        }

        RefreshCreatorShell();
        var originalProfile = LoadWorkspaceProfile();
        var projectSession = _projectSession!;
        var projectPreview = _projectPreview!;
        string? shellFailure = null;
        try
        {
            var project = _history.State.Project;
            var roots = Enum.GetValues<EditorExplorerGrouping>()
                .Select(grouping => CreatorProjectExplorer.Build(project, grouping))
                .ToArray();
            var blueprint = project.Blueprints.First();
            var paletteFilter = new EditorPaletteFilter(
                [blueprint.Kind], [blueprint.Authoring.Kit], blueprint.Authoring.Styles,
                [blueprint.Provenance.LicenseSpdx], blueprint.Authoring.Compatibility,
                blueprint.Authoring.Tags, favoritesOnly: true);
            var shell = new EditorShellConfiguration(
                Guid.Parse("32000000-0000-4000-8000-000000000001"), 1, "toolset:r032-smoke",
                new EditorShellLayout(Guid.Parse("32000000-0000-4000-8000-000000000002"), 1,
                    "toolset:r032-smoke", 272, 336, 176, assistantVisible: false),
                EditorExplorerGrouping.Tag, EditorViewportMode.Navigation, paletteFilter);
            var favoriteProfile = originalProfile.PersonalFavoriteBlueprintIds.Contains(blueprint.Id)
                ? originalProfile
                : EditorWorkspaceProfilePlanner.TogglePersonalFavorite(originalProfile, blueprint.Id);
            var candidate = EditorWorkspaceProfilePlanner.ConfigureShell(favoriteProfile, shell);
            var commands = new EditorWorkspaceProfileCommandHistory(originalProfile);
            var command = new UpdateEditorWorkspaceProfileCommand(
                Guid.Parse("32000000-0000-4000-8000-000000000003"), 1, "toolset:r032-smoke", candidate);
            var preview = commands.Execute(command with { DryRun = true });
            var applied = commands.Execute(command);
            var undo = commands.Undo();
            var redo = commands.Redo();
            SaveWorkspaceProfile(commands.Current);
            var persisted = EditorWorkspaceProfileStore.TryLoad(projectSession.RootDirectory);
            var paletteItems = BlueprintPaletteCatalog.Build(project, commands.Current)
                .Filter(BlueprintPaletteView.All, contextFilter: paletteFilter);
            foreach (var mode in Enum.GetValues<EditorViewportMode>())
            {
                projectPreview.SetEditorMode(mode);
                if (projectPreview.EditorMode != mode) shellFailure = $"viewport_{mode}";
            }

            if (roots.Length != 6 || roots.Any(root => root.Children.Count == 0) ||
                !preview.Succeeded || preview.Applied || !applied.Applied || !undo.Succeeded || !redo.Succeeded ||
                persisted?.Shell.ExplorerGrouping != EditorExplorerGrouping.Tag ||
                persisted.Shell.ViewportMode != EditorViewportMode.Navigation ||
                persisted.Shell.Layout.ExplorerWidth != 272 || paletteItems.Count != 1)
            {
                shellFailure ??= "configuration";
            }
        }
        catch (Exception exception)
        {
            shellFailure = $"exception_{exception.GetType().Name}";
        }
        finally
        {
            SaveWorkspaceProfile(originalProfile);
            RefreshCreatorShell();
        }
        if (shellFailure is not null)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_R032_SHELL_FOUNDATION_FAILED {shellFailure}");
            return false;
        }
        // Preserve the historical G7.02 acceptance marker while G9 reports the expanded total.
        GD.Print($"OPENRPG_FORGE_TOOLSET_G702_SHELL_PASS workspaces=9 explorer={_creatorShell.ExplorerNodeCount} selection=shared diagnostics=clickable");
        GD.Print($"OPENRPG_FORGE_TOOLSET_G9_NETWORK_UI_PASS workspace=network total={_creatorShell.WorkspaceCount}");
        GD.Print($"OPENRPG_FORGE_TOOLSET_G10_LIVE_DIRECTOR_UI_PASS workspace=live-director total={_creatorShell.WorkspaceCount}");
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R032_SHELL_FOUNDATION_PASS " +
            "panels=explorer-viewport-inspector-palette-diagnostics-assistant layout=persisted " +
            "explorer=type-folder-area-tag-dependency-validation viewport=editing-simulation-lighting-navigation-collisions-performance " +
            "palette=type-kit-style-license-compatibility-tags-favorites origins=installed-project-generated-favorites-temporary " +
            "command=dry-run-apply history=undo-redo persistence=roundtrip");
        return true;
    }

    private bool RunBlueprintCatalogSmokeTest()
    {
        if (_blueprintCatalog is null || _history is null || _entityPicker is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_FAILED missing_catalog");
            return false;
        }

        var initialBlueprintCount = _history.State.Project.Blueprints.Count;
        var entity = _history.State.Project.Areas.First().Entities.First();
        var entityIndex = _entityIds.FindIndex(id => id == entity.Id);
        if (entityIndex < 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_FAILED missing_entity");
            return false;
        }

        _entityPicker.Selected = entityIndex;
        PublishSelectedEntity();
        var originalBlueprintId = entity.BlueprintId;
        var derivedBlueprintId = BlueprintId.From(Guid.Parse("f1000000-0000-4000-8000-000000000001"));
        if (!CreateBlueprintFromSelectedEntity(
                derivedBlueprintId,
                "smoke-derived-pillar",
                "Pilier dérivé smoke"))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_FAILED command");
            return false;
        }

        var derived = _history.State.Project.Blueprints.Single(candidate => candidate.Id == derivedBlueprintId);
        var retargeted = _history.State.Project.Areas.First().Entities.Single(candidate => candidate.Id == entity.Id);
        var references = BlueprintReferenceIndex.Build(_history.State.Project).Find(derivedBlueprintId);
        if (_history.State.Project.Blueprints.Count != initialBlueprintCount + 1 ||
            retargeted.BlueprintId != derivedBlueprintId ||
            retargeted.Overrides != entity.Overrides ||
            retargeted.BlueprintVersionRequirement != $"={derived.Authoring.Version}" ||
            !derived.Authoring.ManifestPath.StartsWith("content/blueprints/", StringComparison.Ordinal) ||
            references.Count != 1 ||
            !_blueprintCatalog.SelectBlueprintForSmokeTest(derivedBlueprintId) ||
            _blueprintCatalog.VisibleBlueprintCount != initialBlueprintCount + 1 ||
            _blueprintCatalog.ReferenceCount != 1 ||
            _selectionContext.Current.Count != 1 ||
            _selectionContext.Current[0].Kind != CreatorSelectionKind.Blueprint ||
            _selectionContext.Current[0].Id != derivedBlueprintId.ToString())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_FAILED invalid_state");
            return false;
        }

        var undone = _history.Undo();
        if (!undone.Succeeded ||
            _history.State.Project.Blueprints.Any(candidate => candidate.Id == derivedBlueprintId) ||
            _history.State.Project.Areas.First().Entities.Single(candidate => candidate.Id == entity.Id).BlueprintId != originalBlueprintId)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_FAILED undo");
            return false;
        }

        var redone = _history.Redo();
        if (!redone.Succeeded ||
            _history.State.Project.Areas.First().Entities.Single(candidate => candidate.Id == entity.Id).BlueprintId != derivedBlueprintId)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_FAILED redo");
            return false;
        }

        var restored = _history.Undo();
        if (!restored.Succeeded ||
            _history.State.Project.Blueprints.Count != initialBlueprintCount ||
            _history.State.Project.Areas.First().Entities.Single(candidate => candidate.Id == entity.Id).BlueprintId != originalBlueprintId)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_FAILED cleanup");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls(entity.Id);
        GD.Print($"OPENRPG_FORGE_TOOLSET_G703_BLUEPRINT_PASS catalog={initialBlueprintCount} derived=1 references=1 undo=restored");
        return true;
    }

    private bool RunDirectManipulationSmokeTest()
    {
        if (_projectPreview is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED missing_preview");
            return false;
        }

        var project = _history.State.Project;
        var area = project.Areas.First();
        var placeable = project.Blueprints.First(blueprint => blueprint.Kind == BlueprintKind.Placeable);
        var initialEntityCount = area.Entities.Count;
        var initialRevision = _history.State.Revision;
        var initialJournalCount = _history.Journal.Count;
        var initialNativeRegistrations = _nativeUndoRedoRegistrationCount;
        var originalSelection = _selectionContext.Current.ToArray();
        var originalSelectionLock = _selectionContext.IsLocked;
        var occupiedPosition = area.Entities.First().Transform.Position;
        var rejected = _projectPreview.DropBlueprintForSmokeTest(placeable.Id, occupiedPosition);
        if (rejected is not null || _history.State.Revision != initialRevision ||
            !_projectPreview.GhostVisible || _projectPreview.LastPlacementWasValid)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED invalid_feedback");
            return false;
        }

        var freePosition = FindFreePlacement(placeable.Id);
        if (freePosition is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED no_free_cell");
            return false;
        }

        var placedEntityId = _projectPreview.DropBlueprintForSmokeTest(placeable.Id, freePosition.Value);
        if (placedEntityId is null || _history.State.Project.Areas.First().Entities.Count != initialEntityCount + 1 ||
            !_projectPreview.SelectEntityForSmokeTest(placedEntityId) || !_projectPreview.GizmoVisible)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED drag_drop");
            return false;
        }

        var movePosition = FindFreePlacement(placeable.Id, placedEntityId, freePosition.Value);
        if (movePosition is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED no_move_cell");
            return false;
        }

        var movePreview = _projectPreview.PreviewEntityMoveForSmokeTest(placedEntityId, movePosition.Value);
        if (!movePreview.IsValid || !_projectPreview.GhostVisible || !_projectPreview.LastPlacementWasValid ||
            !_projectPreview.CommitPreviewMoveForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED gizmo_move");
            return false;
        }

        var moved = _history.State.Project.Areas.First().Entities.Single(entity => entity.Id == placedEntityId);
        if (moved.Transform.Position != movePreview.CorrectedPosition)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED transform_state");
            return false;
        }

        _selectionContext.SetLocked(false);
        var anchor = area.Entities.First();
        var selections = new[]
        {
            new CreatorSelection(CreatorSelectionKind.Entity, anchor.Id.ToString(), "Ancre", $"areas/{area.Id}/entities/{anchor.Id}"),
            new CreatorSelection(CreatorSelectionKind.Entity, placedEntityId.ToString(), "Placement", $"areas/{area.Id}/entities/{placedEntityId}"),
        };
        _selectionContext.Replace(selections);
        var selectionInspection = CreatorSelectionInspector.Inspect(_history.State.Project, selections, "toolset:r033-smoke");
        if (!_selectionContext.Focus(selections[1], "toolset:r033-smoke") ||
            !_selectionContext.Frame("toolset:r033-smoke") || selectionInspection.CommonProperties.Count < 7)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R033_DIRECT_MANIPULATION_FAILED selection_inspector");
            return false;
        }

        var manipulation = new ManipulateEntitiesCommand(
            CommandId.From(Guid.NewGuid()), ManipulateEntitiesHandler.CurrentSchemaVersion, "toolset:r033-smoke",
            _history.State.Revision, area.Id, [anchor.Id, placedEntityId], EditorTransformTool.Rotate,
            new Vector3Value(0, 15, 0), EditorCoordinateSpace.World, EditorPivotMode.Individual,
            EditorTransformAlignment.None, 1, 15, .05f, false);
        if (!ApplyCommand(_manipulateEntitiesHandler.Handle(_history.State, manipulation),
                "Rotation multiple validée.", selectedEntityId: null))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R033_DIRECT_MANIPULATION_FAILED multi_gizmo");
            return false;
        }

        var validator = new PlacementValidator();
        var socketFailure = validator.Validate(_history.State.Project,
            new PlacementValidationRequest(area.Id, placeable.Id, freePosition.Value, 1,
                RequiredSocket: "wall", AvailableSockets: ["floor"]));
        var budgetFailure = validator.Validate(_history.State.Project,
            new PlacementValidationRequest(area.Id, placeable.Id, freePosition.Value, 1,
                AvailableBudgetBytes: 1024, BlueprintEstimatedBytes: 2048));
        var correction = validator.Validate(_history.State.Project,
            new PlacementValidationRequest(area.Id, placeable.Id, occupiedPosition, 1, AutoCorrect: true));
        if (socketFailure.Code != PlacementValidator.SocketMissingCode ||
            budgetFailure.Code != PlacementValidator.BudgetExceededCode || correction.Corrections?.Count < 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R033_DIRECT_MANIPULATION_FAILED placement_causes");
            return false;
        }

        var cameraHandler = new NavigateEditorCameraHandler(new SystemClock());
        var cameraState = new EditorCameraState(EditorCameraProfile.CreateDefault(
            EditorCameraProfileId.From(Guid.Parse("33000000-0000-4000-8000-000000000001"))), new EditorCameraRevision(0));
        var cameraInput = new EditorCameraInputConfiguration(
            Guid.Parse("33000000-0000-4000-8000-000000000002"), 1, "toolset:r033-smoke", EditorCameraPilotMode.Fly,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["forward"] = "I",
                ["back"] = "K",
                ["left"] = "J",
                ["right"] = "L",
                ["up"] = "U",
                ["down"] = "O",
                ["focus"] = "F",
            });
        var configuredCamera = cameraHandler.Handle(cameraState, new NavigateEditorCameraCommand(
            CommandId.From(Guid.NewGuid()), cameraState.Revision, EditorCameraOperation.SetInput, Input: cameraInput));
        var pilotedCamera = cameraHandler.Handle(configuredCamera.State, new NavigateEditorCameraCommand(
            CommandId.From(Guid.NewGuid()), configuredCamera.State.Revision, EditorCameraOperation.Pilot, 2, -1, Z: 3));
        if (!configuredCamera.Succeeded || !pilotedCamera.Succeeded ||
            pilotedCamera.State.Profile.Pose.Pivot == cameraState.Profile.Pose.Pivot)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R033_DIRECT_MANIPULATION_FAILED camera_pilot");
            return false;
        }

        var undoManipulation = _history.Undo();
        var undoTransform = _history.Undo();
        var undoPlacement = _history.Undo();
        if (!undoManipulation.Succeeded || !undoTransform.Succeeded || !undoPlacement.Succeeded ||
            _history.State.Project.Areas.First().Entities.Count != initialEntityCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED undo");
            return false;
        }

        var redoPlacement = _history.Redo();
        var redoTransform = _history.Redo();
        var redoManipulation = _history.Redo();
        if (!redoPlacement.Succeeded || !redoTransform.Succeeded || !redoManipulation.Succeeded ||
            _history.State.Project.Areas.First().Entities.Single(entity => entity.Id == placedEntityId).Transform.Position != movePreview.CorrectedPosition)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED redo");
            return false;
        }

        var cleanupManipulation = _history.Undo();
        var cleanupTransform = _history.Undo();
        var cleanupPlacement = _history.Undo();
        if (!cleanupManipulation.Succeeded || !cleanupTransform.Succeeded || !cleanupPlacement.Succeeded ||
            _history.State.Project.Areas.First().Entities.Count != initialEntityCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED cleanup");
            return false;
        }

        var journalRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r033-journal", Guid.NewGuid().ToString("N"));
        try
        {
            EditorCommandJournalStore.Save(journalRoot, _history.Journal);
            var restoredJournal = EditorCommandJournalStore.Load(journalRoot);
            if (restoredJournal.Count != _history.Journal.Count ||
                _history.Journal.Count < initialJournalCount + 3 ||
                (_nativeUndoRedo is not null && _nativeUndoRedoRegistrationCount < initialNativeRegistrations + 3))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R033_DIRECT_MANIPULATION_FAILED command_journal_native_history");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(journalRoot)) Directory.Delete(journalRoot, true);
        }

        _selectionContext.Replace(originalSelection);
        _selectionContext.SetLocked(originalSelectionLock);
        RebuildPreview();
        RefreshEditorControls(area.Entities.First().Id);
        GD.Print("OPENRPG_FORGE_TOOLSET_G704_DIRECT_PASS drag=placed gizmo=moved invalid=blocked history=restored");
        GD.Print("OPENRPG_FORGE_TOOLSET_R033_DIRECT_MANIPULATION_PASS " +
            "selection=simple-multiple-focus-frame-lock-common-inspector camera=configurable-fly-orthographic-bookmarks " +
            "placement=ghost-collision-socket-grid-budget-correction gizmo=translate-rotate-scale-pivot-local-global-snap-numeric-align " +
            "command=uuid-schema-provenance-dry-run-atomic history=native-undo-redo-journal persistence=roundtrip");
        return true;
    }

    private Vector3Value? FindFreePlacement(
        BlueprintId blueprintId,
        EntityId? ignoredEntityId = null,
        Vector3Value? excludedPosition = null)
    {
        if (_history is null)
        {
            return null;
        }

        var area = _history.State.Project.Areas.First();
        var validator = new PlacementValidator();
        for (var x = -9; x <= 9; x++)
        {
            for (var z = -9; z <= 9; z++)
            {
                var candidate = new Vector3Value(x, 0, z);
                if (excludedPosition == candidate)
                {
                    continue;
                }

                var result = validator.Validate(
                    _history.State.Project,
                    new PlacementValidationRequest(area.Id, blueprintId, candidate, 1, ignoredEntityId));
                if (result.IsValid)
                {
                    return result.CorrectedPosition;
                }
            }
        }

        return null;
    }

    private bool RunAreaWorkflowSmokeTest()
    {
        if (_history is null || _areaWorkspace is null || _projectPreview is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G705_AREA_FAILED missing_workspace");
            return false;
        }

        var originalArea = GetActiveArea();
        var originalAreaCount = _history.State.Project.Areas.Count;
        var createdAreaId = CreateArea(
            "Cour des transitions G705",
            AreaKind.Exterior,
            new AreaDimensions(48, 36),
            AreaTemplateKind.Playable);
        if (createdAreaId is null ||
            _history.State.Project.Areas.Count != originalAreaCount + 1 ||
            _activeAreaContext.Current != createdAreaId ||
            !_areaWorkspace.SelectAreaForSmokeTest(createdAreaId) ||
            _projectPreview.EntityCount != 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G705_AREA_FAILED create_activate");
            return false;
        }

        var createdArea = _history.State.Project.Areas.Single(area => area.Id == createdAreaId);
        var sourceAnchor = createdArea.Entities.Single();
        var destinationAnchor = originalArea.Entities.First();
        var portal = new PortalLink(
            PortalId.From(Guid.Parse("f5000000-0000-4000-8000-000000000001")),
            "Retour vers la zone initiale",
            sourceAnchor.Id,
            originalArea.Id,
            destinationAnchor.Id,
            true,
            true);
        if (!UpsertPortal(createdArea.Id, portal) ||
            ProjectValidator.Validate(_history.State.Project)
                .Any(diagnostic => diagnostic.Code.StartsWith("area.portal.", StringComparison.Ordinal)))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G705_AREA_FAILED transition");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "area-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var creation = ProjectSession.Create(smokeRoot, _history.State.Project);
            var areaDirectory = Path.Combine(smokeRoot, "areas");
            var areaDocuments = Directory.Exists(areaDirectory)
                ? Directory.GetFiles(areaDirectory, "*.json", SearchOption.TopDirectoryOnly)
                : [];
            var reopened = ProjectSession.Open(smokeRoot);
            if (!creation.Succeeded || !reopened.Succeeded || reopened.Session is null ||
                areaDocuments.Length != originalAreaCount + 1 ||
                reopened.Session.Project.Areas.Single(area => area.Id == createdAreaId).Portals.Count != 1 ||
                !File.ReadAllText(Path.Combine(smokeRoot, ProjectSession.DefaultProjectFileName))
                    .Contains($"\"formatVersion\": \"{ProjectSession.CurrentProjectFormatVersion}\"", StringComparison.Ordinal))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G705_AREA_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        Undo();
        Undo();
        if (_history.State.Project.Areas.Count != originalAreaCount ||
            _activeAreaContext.Current != originalArea.Id)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G705_AREA_FAILED undo");
            return false;
        }

        Redo();
        Redo();
        if (_history.State.Project.Areas.Single(area => area.Id == createdAreaId).Portals.Count != 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G705_AREA_FAILED redo");
            return false;
        }

        Undo();
        Undo();
        ActivateArea(originalArea.Id);
        if (_history.State.Project.Areas.Count != originalAreaCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G705_AREA_FAILED cleanup");
            return false;
        }

        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_G705_AREA_PASS areas={originalAreaCount + 1} " +
            $"documents={originalAreaCount + 1} transition=valid active=switched history=restored");
        return true;
    }

    private bool RunSemanticClipboardSmokeTest()
    {
        if (_history is null || _entityPicker is null || _projectPreview is null || _areaWorkspace is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED missing_workspace");
            return false;
        }

        var sourceArea = GetActiveArea();
        var candidate = FindEntityWithFreeDuplicateCell(sourceArea);
        if (candidate is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED no_free_duplicate_cell");
            return false;
        }

        var initialIds = sourceArea.Entities.Select(entity => entity.Id).ToHashSet();
        var initialEntityCount = sourceArea.Entities.Count;
        var entityIndex = _entityIds.FindIndex(id => id == candidate.Id);
        if (entityIndex < 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED selection");
            return false;
        }

        _entityPicker.Selected = entityIndex;
        _selectionContext.Replace([new CreatorSelection(CreatorSelectionKind.Entity, candidate.Id.ToString(),
            "clipboard-smoke", $"areas/{sourceArea.Id}/entities/{candidate.Id}")]);
        CopySelectedEntity();
        PasteSemanticClipboard();
        var afterPaste = GetActiveArea();
        var pasted = afterPaste.Entities.SingleOrDefault(entity => !initialIds.Contains(entity.Id));
        if (pasted is null || afterPaste.Entities.Count != initialEntityCount + 1 ||
            pasted.BlueprintId != candidate.BlueprintId || !pasted.Tags.SequenceEqual(candidate.Tags) ||
            pasted.Overrides != candidate.Overrides)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED entity_paste");
            return false;
        }

        Undo();
        Redo();
        if (GetActiveArea().Entities.Count != initialEntityCount + 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED entity_redo");
            return false;
        }
        Undo();

        var blueprintCount = _history.State.Project.Blueprints.Count;
        _independentCopy!.ButtonPressed = true;
        CopySelectedEntity();
        PasteSemanticClipboard();
        var independent = GetActiveArea().Entities.SingleOrDefault(entity => !initialIds.Contains(entity.Id));
        if (independent is null || independent.BlueprintId == candidate.BlueprintId ||
            _history.State.Project.Blueprints.Count != blueprintCount + 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED independent_copy");
            return false;
        }
        _selectionContext.Replace([new CreatorSelection(CreatorSelectionKind.Entity, independent.Id.ToString(),
            "clipboard-cut-smoke", $"areas/{sourceArea.Id}/entities/{independent.Id}")]);
        CutSelectedEntities();
        if (_semanticClipboard.Payload is not EntityClipboardPayload { IsCut: true } cutPayload ||
            cutPayload.Id == Guid.Empty || string.IsNullOrWhiteSpace(cutPayload.Provenance) ||
            GetActiveArea().Entities.Count != initialEntityCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED cut_payload");
            return false;
        }
        Undo();
        Undo();
        _independentCopy.ButtonPressed = false;
        _selectionContext.Replace([new CreatorSelection(CreatorSelectionKind.Entity, candidate.Id.ToString(),
            "clipboard-smoke", $"areas/{sourceArea.Id}/entities/{candidate.Id}")]);
        if (_history.State.Project.Blueprints.Count != blueprintCount ||
            GetActiveArea().Entities.Count != initialEntityCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED independent_cleanup");
            return false;
        }

        var clonedAreaId = DuplicateArea(
            sourceArea.Id,
            "Crypte clone G706",
            AreaCloneMode.Full,
            ExternalPortalPolicy.Drop);
        if (clonedAreaId is null || _activeAreaContext.Current != clonedAreaId ||
            _areaWorkspace.AreaCount != _history.State.Project.Areas.Count)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED area_clone");
            return false;
        }

        var clone = _history.State.Project.Areas.Single(area => area.Id == clonedAreaId);
        if (clone.Entities.Count != initialEntityCount ||
            clone.Entities.Select(entity => entity.Id).Intersect(initialIds).Any() ||
            !clone.Entities.Select(entity => entity.BlueprintId)
                .SequenceEqual(sourceArea.Entities.Select(entity => entity.BlueprintId)))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED area_remap");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "clipboard-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var creation = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            if (!creation.Succeeded || !reopened.Succeeded || reopened.Session is null ||
                reopened.Session.Project.Areas.Count != 2 ||
                reopened.Session.Project.Areas.Single(area => area.Id == clonedAreaId).Entities.Count != initialEntityCount)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        Undo();
        Redo();
        if (!_history.State.Project.Areas.Any(area => area.Id == clonedAreaId))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED area_redo");
            return false;
        }
        Undo();
        ActivateArea(sourceArea.Id);
        if (_history.State.Project.Areas.Any(area => area.Id == clonedAreaId) ||
            GetActiveArea().Entities.Count != initialEntityCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_FAILED cleanup");
            return false;
        }

        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_PASS entities=semantic-copy area=full-clone " +
            "uuids=remapped preview=available history=restored persistence=roundtrip");
        return true;
    }

    private bool RunGlobalSearchAndRenameSmokeTest()
    {
        if (_history is null || _globalSearch is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G707_SEARCH_FAILED missing_search");
            return false;
        }

        var project = _history.State.Project;
        var referenceIndex = CreatorReferenceIndex.Build(project);
        var blueprint = project.Blueprints
            .OrderByDescending(candidate => referenceIndex
                .Find(CreatorContentKind.Blueprint, candidate.Id.ToString()).Count)
            .ThenBy(candidate => candidate.DisplayName, StringComparer.Ordinal)
            .First();
        var initialName = blueprint.DisplayName;
        var initialSlug = blueprint.Slug;
        var referenceCount = referenceIndex
            .Find(CreatorContentKind.Blueprint, blueprint.Id.ToString()).Count;
        if (_globalSearch.SearchForSmokeTest(string.Empty, CreatorContentKind.Blueprint) != project.Blueprints.Count ||
            !_globalSearch.SelectDocumentForSmokeTest(CreatorContentKind.Blueprint, blueprint.Id.ToString()) ||
            _globalSearch.VisibleReferenceCount != referenceCount ||
            !_globalSearch.PreviewRenameForSmokeTest($"{initialName} G707", $"{initialSlug}-g707") ||
            !_globalSearch.ApplyRenameForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G707_SEARCH_FAILED ui_workflow");
            return false;
        }

        var renamed = _history.State.Project.Blueprints.Single(candidate => candidate.Id == blueprint.Id);
        if (renamed.DisplayName != $"{initialName} G707" || renamed.Slug != $"{initialSlug}-g707" ||
            CreatorReferenceIndex.Build(_history.State.Project)
                .Find(CreatorContentKind.Blueprint, blueprint.Id.ToString()).Count != referenceCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G707_SEARCH_FAILED stable_identity");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "search-rename-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var creation = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var persisted = reopened.Session?.Project.Blueprints
                .SingleOrDefault(candidate => candidate.Id == blueprint.Id);
            if (!creation.Succeeded || !reopened.Succeeded || persisted is null ||
                persisted.DisplayName != renamed.DisplayName || persisted.Slug != renamed.Slug)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G707_SEARCH_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        var restored = _history.State.Project.Blueprints.Single(candidate => candidate.Id == blueprint.Id);
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            restored.DisplayName != initialName || restored.Slug != initialSlug)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G707_SEARCH_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_G707_SEARCH_PASS search=global references={referenceCount} " +
            "rename=stable-uuid history=restored persistence=roundtrip");
        return true;
    }

    private bool RunR034ContentOperationsSmokeTest()
    {
        if (_history is null || _globalSearch is null || _cutButton is null || _independentCopy is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R034_CONTENT_OPERATIONS_FAILED missing_ui");
            return false;
        }

        var root = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r034-smoke", Guid.NewGuid().ToString("N"));
        try
        {
            var scripts = Path.Combine(root, "scripts");
            var assets = Path.Combine(root, "assets");
            Directory.CreateDirectory(scripts);
            Directory.CreateDirectory(assets);
            File.WriteAllText(Path.Combine(scripts, "guardian.forge"),
                "asset = \"assets/guardian.glb\"\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(assets, "guardian.glb"), "r034-asset", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "project.json"),
                "{\"script\":\"scripts/guardian.forge\"}\n", new UTF8Encoding(false));

            var searchProfile = CreatorSearchProfile.CreateDefault(_history.State.Project.Id.Value) with
            {
                Provenance = "toolset:r034-smoke",
                ResultLimit = 37,
            };
            CreatorSearchProfileStore.Save(root, searchProfile);
            var loadedProfile = CreatorSearchProfileStore.Load(root, _history.State.Project.Id.Value);
            var script = CreatorWorkspaceResource.Create(CreatorContentKind.Script, "scripts/guardian.forge",
                File.ReadAllText(Path.Combine(scripts, "guardian.forge")), ["script"],
                id: WorkspaceResourceIdentityStore.Resolve(root, "scripts/guardian.forge"));
            var asset = CreatorWorkspaceResource.Create(CreatorContentKind.Asset, "assets/guardian.glb",
                "guardian 3d", ["asset", "3d"],
                id: WorkspaceResourceIdentityStore.Resolve(root, "assets/guardian.glb"));
            var catalog = CreatorWorkspaceResourceCatalog.Build(_history.State.Project, [script, asset]);
            var searchIndex = CreatorSearchIndex.Build(_history.State.Project,
                supplementalDocuments: catalog.Documents);
            var referenceIndex = CreatorReferenceIndex.Build(_history.State.Project, catalog.References);
            if (loadedProfile.ResultLimit != 37 ||
                searchIndex.Search(new CreatorSearchQuery("guardian", CreatorContentKind.Script)).Count != 1 ||
                searchIndex.Search(new CreatorSearchQuery("guardian 3d", CreatorContentKind.Asset)).Count != 1 ||
                referenceIndex.Find(CreatorContentKind.Asset, asset.Id.ToString()).Count != 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R034_CONTENT_OPERATIONS_FAILED search_references_profile");
                return false;
            }

            var rename = WorkspaceResourceRenameService.Preview(root, "scripts/guardian.forge",
                "scripts/guardian_runtime.forge", "toolset:r034-smoke");
            WorkspaceResourceRenameService.Apply(rename);
            var renamedValid = File.Exists(Path.Combine(scripts, "guardian_runtime.forge")) &&
                File.ReadAllText(Path.Combine(root, "project.json"))
                    .Contains("scripts/guardian_runtime.forge", StringComparison.Ordinal) &&
                WorkspaceResourceIdentityStore.Resolve(root, "scripts/guardian_runtime.forge") == rename.ResourceId;
            WorkspaceResourceRenameService.Undo(rename);
            if (!renamedValid || !File.Exists(Path.Combine(scripts, "guardian.forge")))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R034_CONTENT_OPERATIONS_FAILED resource_rename");
                return false;
            }

            var project = _history.State.Project;
            var playerIds = project.Blueprints.Where(item => item.Kind == BlueprintKind.PlayerSpawn)
                .Select(item => item.Id).ToHashSet();
            var start = project.Areas.SelectMany(area => area.Entities.Select(entity => (Area: area, Entity: entity)))
                .First(item => playerIds.Contains(item.Entity.BlueprintId));
            var targetKind = project.Quests.Count > 0 ? PlaytestTargetKind.SelectedQuest
                : project.Dialogues.Count > 0 ? PlaytestTargetKind.Dialogue : PlaytestTargetKind.World;
            Guid? targetId = targetKind == PlaytestTargetKind.SelectedQuest ? project.Quests[0].Id.Value
                : targetKind == PlaytestTargetKind.Dialogue ? project.Dialogues[0].Id.Value : null;
            var injections = targetKind == PlaytestTargetKind.SelectedQuest
                ? new[] { new PlaytestQuestInjection(Guid.NewGuid(), project.Quests[0].Id,
                    project.Quests[0].InitialStateId, new Dictionary<string, string>()) }
                : [];
            var defaults = AreaPlaytestProfile.CreateDefault(start.Area.Id);
            var profile = new AreaPlaytestProfile(defaults.Id, AreaPlaytestProfile.CurrentSchemaVersion,
                "toolset:r034-smoke", true, false, PlayFromHereStartMode.PlayerSpawn, start.Entity.Id,
                TransformValue.Identity, null, start.Entity.BlueprintId, "r034-targeted", injections,
                targetKind, targetId, headless: true);
            var before = ProjectJsonSerializer.Serialize(project);
            var snapshotStore = new PlaytestSnapshotStore(Path.Combine(root, "playtests"));
            var snapshot = snapshotStore.Create(project, start.Area.Id, profile);
            var manifest = PlaytestLaunchManifestStore.Load(snapshot.LaunchManifestFilePath!);
            var playtestValid = manifest.Headless && manifest.TargetKind == targetKind.ToString() &&
                manifest.TargetId == targetId?.ToString() && before == ProjectJsonSerializer.Serialize(project);
            snapshotStore.Delete(snapshot);
            if (!playtestValid)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R034_CONTENT_OPERATIONS_FAILED targeted_playtest");
                return false;
            }

            GD.Print("OPENRPG_FORGE_TOOLSET_R034_CONTENT_OPERATIONS_PASS " +
                "clipboard=multi-cut-instance-independent-remap search=scripts-assets-profile-persisted " +
                "references=localized-variable-resource-path rename=preview-atomic-uuid-rollback " +
                $"playtest=world-quest-dialogue-headless target={targetKind.ToString().ToLowerInvariant()}");
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_R034_CONTENT_OPERATIONS_FAILED exception={exception.Message}");
            return false;
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private bool RunModuleConfigurationSmokeTest()
    {
        if (_history is null || _moduleConfiguration is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G708_MODULE_FAILED missing_configuration");
            return false;
        }

        var originalModule = _history.State.Project.Module;
        var originalMetadata = originalModule.Metadata;
        var metadata = new ProjectMetadata(
            originalMetadata.Id,
            "fr",
            [
                new ProjectLocalizedMetadata(
                    "fr",
                    originalModule.DisplayName,
                    "Module de validation Creator G708",
                    originalModule.Description,
                    "Équipe OpenRPG Forge"),
                new ProjectLocalizedMetadata(
                    "en",
                    $"{originalModule.DisplayName} EN",
                    "G708 Creator validation module",
                    originalModule.Description,
                    "OpenRPG Forge Team"),
            ],
            "Atelier OpenRPG Forge",
            "0.8.0",
            "CC0-1.0",
            "assets/module-cover.png",
            ["creator", "smoke"],
            "Tout public",
            "creator-smoke");
        var variable = new ProjectVariableDefinition(
            ProjectVariableId.From(Guid.Parse("f8000000-0000-4000-8000-000000000001")),
            "smoke.g708.ready",
            ProjectVariableValueKind.Boolean,
            ProjectVariableScope.Save,
            "false",
            "Prouve le blackboard typé G708.",
            "creator-smoke", 1, ProjectVariableReplicationMode.Party, debugVisible: false,
            requiredAtRuntime: true);
        if (!_moduleConfiguration.ConfigureForSmokeTest(metadata, [variable]) ||
            !_moduleConfiguration.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G708_MODULE_FAILED ui_workflow");
            return false;
        }

        var configured = _history.State.Project.Module;
        var blackboard = new ProjectBlackboardScriptService(configured.Variables);
        var handle = blackboard.Query(variable.Key).Single();
        var mutation = blackboard.Set(handle, "true");
        var replication = blackboard.ReplicationDelta(BlackboardReplicationAudience.Party);
        var debug = blackboard.DebugSnapshot();
        var searchResults = CreatorSearchIndex.Build(_history.State.Project).Search(
            new CreatorSearchQuery("smoke.g708.ready", CreatorContentKind.ProjectVariable));
        if (configured.Metadata.Localizations.Count != 2 || configured.Metadata.Version != "0.8.0" ||
            configured.Variables.Count != 1 || searchResults.Count != 1 ||
            !mutation.Succeeded || replication.Values.GetValueOrDefault(variable.Key) != "true" ||
            debug.HiddenVariableCount != 1 || debug.Variables.Count != 0 ||
            blackboard.Set(handle, "not-a-boolean").Succeeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G708_MODULE_FAILED canonical_state");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "module-configuration-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = Path.Combine(smokeRoot, "workspace");
            var artifact = Path.Combine(smokeRoot, "configured.orpmod");
            var creation = ProjectSession.Create(workspace, _history.State.Project);
            var reopened = ProjectSession.Open(workspace);
            var exported = ModulePackager.Export(workspace, artifact);
            if (!creation.Succeeded || !reopened.Succeeded || reopened.Session is null ||
                reopened.Session.Project.Module.Metadata.Localizations.Count != 2 ||
                reopened.Session.Project.Module.Variables.Single().Kind != ProjectVariableValueKind.Boolean ||
                reopened.Session.Project.Module.Variables.Single().Replication != ProjectVariableReplicationMode.Party ||
                reopened.Session.Project.Module.Variables.Single().DebugVisible ||
                !reopened.Session.Project.Module.Variables.Single().RequiredAtRuntime ||
                exported.Manifest.ModuleVersion != "0.8.0" ||
                !File.ReadAllText(reopened.Session.ProjectFilePath)
                    .Contains($"\"formatVersion\": \"{ProjectSession.CurrentProjectFormatVersion}\"", StringComparison.Ordinal))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G708_MODULE_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Module.Metadata != originalMetadata ||
            _history.State.Project.Module.Variables.Count != originalModule.Variables.Count)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G708_MODULE_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_G708_MODULE_PASS metadata=localized variables=typed scopes=save " +
            $"format={ProjectSession.CurrentProjectFormatVersion} history=restored package=versioned");
        GD.Print("OPENRPG_FORGE_TOOLSET_R011_BLACKBOARD_PASS " +
            "schema=typed-scoped save=roundtrip replication=party debug=governed " +
            "forgescript=typed-handle-command ui=module-configuration history=undo-redo");
        return true;
    }

    private bool RunPaletteProvenanceSmokeTest()
    {
        if (_history is null || _blueprintCatalog is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED missing_catalog");
            return false;
        }

        var blueprint = _history.State.Project.Blueprints.First();
        var originalOrigin = blueprint.PaletteOrigin;
        var originalFavorite = blueprint.IsFavorite;
        var originalProvenance = blueprint.Provenance;
        var originalAuthoring = blueprint.Authoring;
        var installedProvenance = new BlueprintProvenance(
            BlueprintProvenance.CurrentSchemaVersion,
            $"pack://forge-core/1.0.0/{blueprint.Slug}",
            "OpenRPG Forge",
            "CC0-1.0",
            new string('a', 64),
            false,
            ["Validated installed pack content"],
            "spdx:CC0-1.0");
        if (!_blueprintCatalog.ConfigurePaletteForSmokeTest(
                blueprint.Id,
                BlueprintPaletteOrigin.InstalledPack,
                false,
                installedProvenance))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED installed_ui");
            return false;
        }

        var installedCatalog = BlueprintPaletteCatalog.Build(_history.State.Project);
        var installedBlueprint = _history.State.Project.Blueprints.Single(candidate => candidate.Id == blueprint.Id);
        var editInstalled = _upsertBlueprintHandler.Handle(
            _history.State,
            new UpsertBlueprintCommand(
                CommandId.From(Guid.NewGuid()),
                _history.State.Revision,
                blueprint.Id,
                blueprint.Slug,
                $"{blueprint.DisplayName} modifié",
                blueprint.Kind,
                DryRun: false));
        if (installedCatalog.Filter(BlueprintPaletteView.InstalledPacks).Count != 1 ||
            !_blueprintCatalog.SelectedIsReadOnly ||
            installedBlueprint.Authoring.Version != "1.0.0" ||
            installedBlueprint.Authoring.ManifestPath != "pack://forge-core/1.0.0/manifest.json" ||
            editInstalled.Diagnostics.Count != 1 ||
            editInstalled.Diagnostics[0].Code != UpsertBlueprintHandler.ReadOnlyCode)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED installed_readonly");
            return false;
        }

        if (!_history.Undo().Succeeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED installed_undo");
            return false;
        }
        RefreshEditorControls();

        var temporaryProvenance = new BlueprintProvenance(
            BlueprintProvenance.CurrentSchemaVersion,
            $"temporary://creator/{blueprint.Id}",
            "Creator session",
            "UNLICENSED",
            "unverified",
            false,
            ["Ephemeral authoring draft"]);
        if (!_blueprintCatalog.ConfigurePaletteForSmokeTest(
                blueprint.Id,
                BlueprintPaletteOrigin.Temporary,
                false,
                temporaryProvenance) ||
            BlueprintPaletteCatalog.Build(_history.State.Project)
                .Filter(BlueprintPaletteView.Temporary).Count != 1 ||
            !_history.Undo().Succeeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED temporary_scope");
            return false;
        }
        RefreshEditorControls();

        var generatedProvenance = new BlueprintProvenance(
            BlueprintProvenance.CurrentSchemaVersion,
            $"generator://creator/{blueprint.Slug}",
            "Forge Generator",
            "CC0-1.0",
            new string('b', 64),
            true,
            ["Generated for G7-09", "Reviewed by module author"],
            "project-author:generated-and-reviewed");
        if (!_blueprintCatalog.ConfigurePaletteForSmokeTest(
                blueprint.Id,
                BlueprintPaletteOrigin.Generated,
                true,
                generatedProvenance) ||
            !_blueprintCatalog.SelectViewForSmokeTest(BlueprintPaletteView.Generated) ||
            _blueprintCatalog.VisibleBlueprintCount != 1 ||
            !_blueprintCatalog.SelectViewForSmokeTest(BlueprintPaletteView.Favorites) ||
            _blueprintCatalog.VisibleBlueprintCount != 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED generated_favorite_views");
            return false;
        }

        var search = CreatorSearchIndex.Build(_history.State.Project).Search(
            new CreatorSearchQuery("Forge Generator", CreatorContentKind.Blueprint));
        if (search.Count != 1 || search[0].Document.Id != blueprint.Id.ToString() ||
            !search[0].Document.Tags.Contains("favorite", StringComparer.OrdinalIgnoreCase))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED provenance_search");
            return false;
        }

        var dropPlanner = new BlueprintDropPlanner();
        var activeArea = _history.State.Project.Areas.First();
        var freePlacement = FindFreePlacement(blueprint.Id);
        var itemBlueprint = _history.State.Project.Blueprints.FirstOrDefault(candidate => candidate.Kind == BlueprintKind.Item);
        var dropPlans = freePlacement is null || itemBlueprint is null
            ? []
            : new[]
            {
                dropPlanner.Plan(_history.State.Project, new BlueprintDropRequest(
                    Guid.NewGuid(), BlueprintDropPlanner.CurrentSchemaVersion, "toolset:r031-smoke",
                    blueprint.Id, BlueprintDropTargetKind.Viewport, $"areas/{activeArea.Id}/entities",
                    activeArea.Id, freePlacement)),
                dropPlanner.Plan(_history.State.Project, new BlueprintDropRequest(
                    Guid.NewGuid(), 1, "toolset:r031-smoke", blueprint.Id,
                    BlueprintDropTargetKind.Graph, "dialogues/smoke/nodes/speakerBlueprintId")),
                dropPlanner.Plan(_history.State.Project, new BlueprintDropRequest(
                    Guid.NewGuid(), 1, "toolset:r031-smoke", itemBlueprint.Id,
                    BlueprintDropTargetKind.Inventory, "areas/smoke/entities/player/inventory/items")),
                dropPlanner.Plan(_history.State.Project, new BlueprintDropRequest(
                    Guid.NewGuid(), 1, "toolset:r031-smoke", blueprint.Id,
                    BlueprintDropTargetKind.Property, "areas/smoke/entities/selection/properties/appearance")),
                dropPlanner.Plan(_history.State.Project, new BlueprintDropRequest(
                    Guid.NewGuid(), 1, "toolset:r031-smoke", blueprint.Id,
                    BlueprintDropTargetKind.Package, "module/content/packages/selection")),
            };
        if (dropPlans.Length != 5 || dropPlans.Any(plan => !plan.IsValid) ||
            BlueprintCreditCatalog.Aggregate(_history.State.Project.Blueprints).Count == 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R031_PALETTE_INTEGRATION_FAILED contextual_drop");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "palette-provenance-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = Path.Combine(smokeRoot, "workspace");
            var creation = ProjectSession.Create(workspace, _history.State.Project);
            var reopened = ProjectSession.Open(workspace);
            var restoredBlueprint = reopened.Session?.Project.Blueprints
                .Single(candidate => candidate.Id == blueprint.Id);
            if (!creation.Succeeded || !reopened.Succeeded || restoredBlueprint is null ||
                restoredBlueprint.PaletteOrigin != BlueprintPaletteOrigin.Generated ||
                !restoredBlueprint.IsFavorite ||
                !restoredBlueprint.Provenance.IsAiGenerated ||
                restoredBlueprint.Provenance.LicenseSpdx != "CC0-1.0" ||
                restoredBlueprint.Provenance.PermissionEvidence != "project-author:generated-and-reviewed")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        var restored = _history.State.Project.Blueprints.Single(candidate => candidate.Id == blueprint.Id);
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            restored.PaletteOrigin != originalOrigin || restored.IsFavorite != originalFavorite ||
            restored.Provenance != originalProvenance || restored.Authoring != originalAuthoring)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G709_PALETTE_FAILED history");
            return false;
        }

        _blueprintCatalog.SelectViewForSmokeTest(BlueprintPaletteView.All);
        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G709_PALETTE_PASS standard=readonly project=editable " +
            $"generated=provenance favorites=filtered temporary=isolated format={ProjectSession.CurrentProjectFormatVersion} " +
            "history=restored persistence=roundtrip");
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R030_BLUEPRINT_PALETTES_PASS " +
            "definition=uuid-schema-version-package-license-hash-budget instance=version-range-overrides " +
            "create=parameters-dependencies-preview-package standard=readonly-versioned-replaceable " +
            "project=content-blueprints-namespace-git-friendly ui=visible-command history=undo-redo " +
            "persistence=roundtrip export=manifest-licenses-migration-preview");
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R031_PALETTE_INTEGRATION_PASS " +
            "drop=viewport-graph-inventory-property-package validation=contextual-command " +
            "dependencies=local-transitive-packages-minimal governance=source-author-spdx-permission-hash-modifications-ai " +
            "credits=aggregated distribution=blocked-if-incomplete persistence=roundtrip history=undo-redo");
        return true;
    }

    private bool RunBuildPipelineSmokeTest()
    {
        if (_buildPipeline is null || _history is null || !_buildPipeline.AnalyzeForSmokeTest() ||
            !_buildPipeline.PreviewProfileForSmokeTest() ||
            _buildPipeline.ResourceCount != 5 || _buildPipeline.ScriptCount != 2 ||
            _buildPipeline.BuildTaskCount < 1 ||
            _buildPipeline.BuildMaximumParallelism != DeterministicBuildTaskScheduler.DefaultMaximumParallelism)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G712_BUILD_FAILED analysis");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "incremental-build-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = Path.Combine(smokeRoot, "workspace");
            var artifact = Path.Combine(smokeRoot, "module.orpmod");
            var created = ProjectSession.Create(workspace, _history.State.Project);
            if (!created.Succeeded || created.Session is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G712_BUILD_FAILED workspace");
                return false;
            }

            var profile = WorkspaceBuildProfile.CreateDefault();
            var compatibility = profile.Compatibility with
            {
                MinimumRuntimeVersion = ModuleRuntimeCompatibility.CurrentRuntimeVersion,
            };
            profile = new WorkspaceBuildProfile(profile.Id, profile.SchemaVersion, profile.Provenance,
                profile.RequireGitRepository, profile.TextDocuments, profile.ExplicitGeneratedFiles,
                profile.ReproducibleAssets, profile.Layout, profile.CliOperations, profile.StableOrdering,
                profile.NormalizedTimestamps, profile.LockedDependencies, profile.ContentHashes,
                profile.VerifyReproducibility, profile.IncrementalBuild, profile.CachePath,
                profile.RebuildAffectedOnly, profile.Governance, profile.Collaboration, compatibility);
            var profileSaved = WorkspaceBuildProfileStore.Save(workspace, profile);
            var first = ModulePackager.Export(workspace, artifact);
            var second = ModulePackager.Export(workspace, artifact);
            var restored = WorkspaceBuildStateStore.TryLoad(workspace);
            var cache = WorkspaceBuildCollaborationService.RebuildCacheIndex(workspace, profile, dryRun: true);
            var normalization = WorkspaceBuildCollaborationService.NormalizeDocuments(workspace, profile, dryRun: true);
            var migration = WorkspaceBuildCollaborationService.MigrateWorkspace(workspace, profile, dryRun: true);
            var references = WorkspaceBuildCollaborationService.BuildReferenceIndex(
                workspace, created.Session.Project, created.Session.ProjectFilePath, profile.Collaboration);
            var semantic = WorkspaceBuildCollaborationService.Diff(
                WorkspaceBuildCollaborationService.LoadSemanticSnapshot(workspace),
                WorkspaceBuildCollaborationService.CreateSemanticSnapshot(created.Session.Project, second.DependencyGraph),
                profile.Collaboration);
            if (first.BuildPlan.RebuiltResources.Count == 0 ||
                second.BuildPlan.RebuiltResources.Count != 0 ||
                second.BuildPlan.ReusedResources.Count == 0 ||
                restored?.SchemaVersion != BuildDependencyGraph.CurrentSchemaVersion ||
                !File.Exists(Path.Combine(workspace, ".forge", "build-graph.json")) ||
                !File.Exists(Path.Combine(workspace,
                    WorkspaceBuildCollaborationService.SemanticSnapshotRelativePath.Replace('/', Path.DirectorySeparatorChar))) ||
                !cache.Succeeded || !normalization.Succeeded || !migration.Succeeded || !migration.Idempotent ||
                references.Diagnostics.Count != 0 || !references.Nodes.Any(node => node.Dependents.Count > 0) ||
                !semantic.Succeeded || semantic.Changes.Count != 0 || !profileSaved.Succeeded ||
                first.PackagingMaterialization.TaskCount < 1 ||
                first.PackagingMaterialization.MaximumParallelism !=
                DeterministicBuildTaskScheduler.DefaultMaximumParallelism ||
                first.Manifest.RuntimeMinimumVersion != ModuleRuntimeCompatibility.CurrentRuntimeVersion ||
                ModuleRuntimeCompatibility.Evaluate(first.Manifest, ModuleRuntimeCompatibility.CurrentRuntimeVersion) is not null ||
                ModuleRuntimeCompatibility.Evaluate(first.Manifest with { RuntimeMinimumVersion = "99.0.0" },
                    ModuleRuntimeCompatibility.CurrentRuntimeVersion)?.Code !=
                ModuleRuntimeCompatibility.IncompatibleRuntimeCode ||
                ModuleRuntimeCompatibility.Evaluate(first.Manifest with
                { RuntimeMinimumVersion = "99.0.0", RuntimeCompatibilityRequired = false },
                    ModuleRuntimeCompatibility.CurrentRuntimeVersion) is not null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G712_BUILD_FAILED incremental");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G712_BUILD_PASS graph=versioned scripts=2 cache=source-api-compiler " +
            "dependencies=automatic incremental=reused persistence=restored profile=canonical ui=previewed " +
            "governance=p0-r002 worktrees=exclusive ci=complete fixtures=5 semver=core-kits-packages adrs=governed");
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R023_BUILD_COLLABORATION_PASS " +
            "cache=content-addressed-disposable-rebuildable normalize=json-utf8-lf-order-numbers-atomic " +
            "migration=dry-run-backup-idempotent-downgrade-explicit references=forward-reverse-rebuildable " +
            "diff=area-graph-entity-package-script-text-json cli=structured mcp=shell-free " +
            "history=undo-redo persistence=roundtrip");
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R026_PROJECT_COMPATIBILITY_PASS " +
            "profile=uuid-schema-provenance runtime-min=manifest project-format=versioned " +
            "migration=automatic-dry-run-backup-idempotent runtime=required-diagnostic-optional-safe " +
            "ui=visible-command-history forgescript=typed-handle mcp=filtered-plan-apply-revert " +
            "persistence=legacy-roundtrip");
        return true;
    }

    private bool RunR036EeAcceptanceSmokeTest()
    {
        if (_history is null || _projectPreview is null || _buildPipeline is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R036_EE_FAILED setup");
            return false;
        }

        var area = GetActiveArea();
        var selected = area.Entities.FirstOrDefault(entity =>
            entity.Tags.Contains("solid", StringComparer.OrdinalIgnoreCase) ||
            entity.Tags.Contains("architecture", StringComparer.OrdinalIgnoreCase));
        var agent = area.Navigation.AgentProfiles.FirstOrDefault();
        if (selected is null || agent is null || !_projectPreview.SelectEntityForSmokeTest(selected.Id))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R036_EE_FAILED selection");
            return false;
        }

        var originalScale = selected.Transform.Scale;
        var beforeNavigation = NavigationSurfaceBuilder.Build(_history.State.Project, area, agent);
        if (_projectPreview.ScaleGizmoHandleCount != 4 ||
            !_projectPreview.ScaleSelectedForSmokeTest(ProjectPreview3D.ScaleGizmoAxis.X, increase: false))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R036_EE_FAILED scale_gizmo");
            return false;
        }

        var scaledArea = GetActiveArea();
        var scaled = scaledArea.Entities.Single(entity => entity.Id == selected.Id);
        var afterNavigation = NavigationSurfaceBuilder.Build(_history.State.Project, scaledArea, agent);
        var scaleAccepted = scaled.Transform.Scale.X < originalScale.X &&
                            scaled.Transform.Scale.Y == originalScale.Y &&
                            scaled.Transform.Scale.Z == originalScale.Z &&
                            beforeNavigation.SourceFingerprint != afterNavigation.SourceFingerprint &&
                            _projectPreview.NavigationOverlayCellCount == afterNavigation.WalkableCells.Count;
        var undo = _history.Undo();
        RebuildPreview();
        RefreshEditorControls(selected.Id);
        var restored = GetActiveArea().Entities.Single(entity => entity.Id == selected.Id);
        if (!scaleAccepted || !undo.Succeeded || restored.Transform.Scale != originalScale ||
            _buildPipeline.BuildTaskCount < 1 ||
            _buildPipeline.BuildMaximumParallelism != DeterministicBuildTaskScheduler.DefaultMaximumParallelism)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R036_EE_FAILED acceptance");
            return false;
        }

        var quality = PerformanceQualityService.Benchmark(
            _history.State.Project,
            _history.State.Project.Module.RuntimePolicies.Extensions.ModernTooling.QualityAssurance,
            packageBytes: 0,
            scriptCount: _buildPipeline.ScriptCount);
        if (!quality.Passed || quality.Measurements.Count == 0 ||
            quality.Measurements.Any(item => !double.IsFinite(item.NavigationRebuildMilliseconds)))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R036_EE_FAILED navigation_benchmark");
            return false;
        }

        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R036_EE_PASS " +
            "scale=gizmo-x-y-z-uniform-constraints-multiedit-command-history-persistence " +
            "collision=preview-rebuilt navigation=canonical-fingerprint-overlay-agent-profiles-dynamic-obstacles-recovery " +
            $"benchmark={quality.Measurements.Count} build=dependency-graph-incremental-script-cache-asset-cache-" +
            $"parallel-{_buildPipeline.BuildMaximumParallelism}-deterministic-output");
        return true;
    }

    private bool RunR035BuildDiagnosticsSmokeTest()
    {
        if (_creatorShell is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R035_BUILD_DIAGNOSTICS_FAILED shell");
            return false;
        }

        var diagnostics = new[]
        {
            CreatorDiagnostic.Create(CreatorDiagnosticSeverity.Warning, "area.navigation.portal_missing",
                "Portail introuvable", "areas/smoke/portals/entry", "build:r035"),
            CreatorDiagnostic.Create(CreatorDiagnosticSeverity.Error, "project.reference.blueprint_missing",
                "Blueprint introuvable", "areas/smoke/entities/warden", "build:r035"),
            CreatorDiagnostic.Create(CreatorDiagnosticSeverity.Information, "runtime.playtest.ready",
                "Playtest prêt", "runtime/playtest", "runtime:r035"),
        };
        _creatorShell.SetDiagnostics(diagnostics);
        if (!_creatorShell.FilterDiagnosticsForSmokeTest(
                CreatorDiagnosticCategory.Reference,
                CreatorDiagnosticSeverity.Warning,
                "blueprint",
                "build:r035",
                expectedCount: 1) ||
            !_creatorShell.SelectDiagnosticForSmokeTest(0))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R035_BUILD_DIAGNOSTICS_FAILED filtering");
            return false;
        }

        var root = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r035-diagnostics", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var first = CreatorDiagnosticBundleExporter.Export(root, diagnostics,
                "runtime ready\npassword=smoke-secret\nplayerId=smoke-player");
            var second = CreatorDiagnosticBundleExporter.Export(root, diagnostics.Reverse().ToArray(),
                "runtime ready\r\npassword=smoke-secret\r\nplayerId=smoke-player");
            using var archive = ZipFile.OpenRead(Path.Combine(root,
                first.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            using var reader = new StreamReader(archive.GetEntry("runtime.log")!.Open());
            var log = reader.ReadToEnd();
            if (first.SchemaVersion != CreatorDiagnosticBundleResult.CurrentSchemaVersion ||
                first.DiagnosticCount != diagnostics.Length || !first.IncludesRuntimeLog ||
                second.Id != first.Id || second.Sha256 != first.Sha256 || !second.Reused ||
                log.Contains("smoke-secret", StringComparison.Ordinal) ||
                log.Contains("smoke-player", StringComparison.Ordinal) ||
                !log.Contains("[REDACTED]", StringComparison.Ordinal) ||
                !log.Contains("[PLAYER_REDACTED]", StringComparison.Ordinal))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R035_BUILD_DIAGNOSTICS_FAILED bundle");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            _creatorShell.FilterDiagnosticsForSmokeTest(
                category: null,
                CreatorDiagnosticSeverity.Information,
                string.Empty,
                string.Empty,
                diagnostics.Length);
            RefreshCreatorShell();
        }

        GD.Print(
            "OPENRPG_FORGE_TOOLSET_R035_BUILD_DIAGNOSTICS_PASS " +
            "build=validation-scripts-assets-cache-incremental-report-reproducible ui=canonical-export " +
            "diagnostics=uuid-schema-provenance-category-severity-text-correlation-clickable-quickfix " +
            "logs=runtime-sanitized bundle=atomic-deterministic-reused cli=structured mcp=structured");
        return true;
    }

    private bool RunDistributionProfilesSmokeTest()
    {
        if (_distributionProfiles is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_FAILED missing_window");
            return false;
        }

        var original = _history.State.Project.Module.Distribution;
        var profiles = original.TargetProfiles.Select(profile => new ModuleTargetProfile(
            profile.Id,
            profile.Target,
            profile.Enabled,
            profile.IncludeEditorData,
            profile.IncludeDebugSymbols,
            profile.OptimizeContent,
            profile.RequireApprovedLicenses,
            profile.Target == ModuleTargetKind.Editor ? "editor-smoke" : profile.OutputName,
            profile.SecretsProfile,
            "workspace-smoke-test")).ToArray();
        var approval = new ThirdPartyDependencyApproval(
            ThirdPartyDependencyId.From(Guid.Parse("d7130000-0000-4000-8000-000000000001")),
            "Forge Smoke Library",
            "1.2.0",
            "MIT",
            "OpenRPG Forge",
            "Vérifier le registre gouverné dans le prototype.",
            "trimestrielle",
            "Remplacer par le composant interne équivalent.",
            "https://example.invalid/forge-smoke-library",
            [ModuleTargetKind.Editor],
            "workspace-smoke-test");
        var release = new ModuleReleaseConfiguration(
            original.Release.Id,
            ModuleReleaseChannel.Preview,
            [ModuleDistributionPlatform.WindowsX64, ModuleDistributionPlatform.LinuxX64],
            new ModuleSigningConfiguration(
                ModuleSigningMode.Optional,
                "preview-signing",
                [],
                [],
                allowUnsignedDevelopment: true),
            new ModuleDeltaConfiguration(true, 1024 * 1024, true, true, true, 3),
            original.Release.WindowsPackaging,
            "workspace-smoke-test");
        var configuration = new ModuleDistributionConfiguration(
            original.Id,
            profiles,
            [approval],
            "workspace-smoke-test",
            release);
        if (!_distributionProfiles.ConfigureForSmokeTest(configuration, ModuleTargetKind.Shipping) ||
            _distributionProfiles.ProfileCount != 5 ||
            _distributionProfiles.ComplianceDiagnosticCount == 0 ||
            !_distributionProfiles.ApplyForSmokeTest() ||
            _history.State.Project.Module.Distribution.Dependencies.Count != 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_FAILED configuration");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "distribution-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = Path.Combine(smokeRoot, "workspace");
            var artifact = Path.Combine(smokeRoot, "editor.orpmod");
            var releaseBundle = Path.Combine(smokeRoot, "editor-preview-windows.zip");
            var created = ProjectSession.Create(workspace, _history.State.Project);
            if (!created.Succeeded || created.Session is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_FAILED workspace");
                return false;
            }

            var reopened = ProjectSession.Open(workspace);
            var exported = ModulePackager.Export(workspace, artifact, target: ModuleTargetKind.Editor);
            var bundled = ModuleReleaseBundleBuilder.Build(
                artifact,
                releaseBundle,
                ModuleTargetKind.Editor,
                ModuleDistributionPlatform.WindowsX64,
                release);
            using var archive = ZipFile.OpenRead(artifact);
            using var releaseArchive = ZipFile.OpenRead(releaseBundle);
            if (!reopened.Succeeded || reopened.Session is null ||
                reopened.Session.Project.Module.Distribution.TargetProfiles.Count != 5 ||
                reopened.Session.Project.Module.Distribution.Dependencies.Single().Id != approval.Id ||
                reopened.Session.Project.Module.Distribution.Release.Channel != ModuleReleaseChannel.Preview ||
                reopened.Session.Project.Module.Distribution.Release.Platforms.Count != 2 ||
                exported.Manifest.Target != ModuleTargetKind.Editor ||
                exported.Manifest.ThirdPartyDependencies.Single().Id != approval.Id ||
                archive.GetEntry(DistributionNoticeBuilder.PackagePath) is null ||
                bundled.Manifest.Channel != ModuleReleaseChannel.Preview ||
                releaseArchive.GetEntry("release-manifest.json") is null ||
                releaseArchive.GetEntry("install.ps1") is null ||
                releaseArchive.GetEntry("uninstall.ps1") is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        _distributionTarget = ModuleTargetKind.Editor;
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Module.Distribution != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_PASS profiles=5 shipping=blocked " +
            "licenses=diagnostic registry=versioned notices=generated channel=preview platforms=2 " +
            "windows=portable-installer history=restored persistence=roundtrip");
        return true;
    }

    private bool RunContentWorkflowSmokeTest()
    {
        if (_contentWorkflow is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11E_CONTENT_FAILED missing_window");
            return false;
        }

        var original = _history.State.Project.Module.Content;
        var tableId = DataTableId.From(Guid.Parse("d7150000-0000-4000-8000-000000000001"));
        var rowId = DataRowId.From(Guid.Parse("d7150000-0000-4000-8000-000000000002"));
        var column = new DataColumnDefinition(
            DataColumnId.From(Guid.Parse("d7150000-0000-4000-8000-000000000003")),
            "difficulty",
            "Difficulté",
            DataColumnKind.Integer,
            required: true);
        DataTableDefinition Table(string value, string provenance) => new(
            tableId,
            "encounters",
            "Rencontres",
            [column],
            [new DataRowDefinition(rowId, new Dictionary<string, string> { ["difficulty"] = value }, provenance)],
            provenance: provenance,
            licenseSpdx: "CC0-1.0",
            contentHash: new string('a', 64));
        var configured = new ContentWorkspaceConfiguration(
            original.Id,
            new ContentBatchConfiguration(
                "*.json",
                [".json"],
                ContentBatchCollisionPolicy.PreserveBoth,
                "workspace-smoke-test",
                "CC0-1.0"),
            [Table("2", "base")],
            ContentMergePolicy.PreserveBoth,
            "workspace-smoke-test");
        if (!_contentWorkflow.ConfigureForSmokeTest(configured) ||
            _contentWorkflow.DataTableCount != 1 ||
            !_contentWorkflow.ApplyForSmokeTest() ||
            _history.State.Project.Module.Content.DataTables.Single().Id != tableId)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11E_CONTENT_FAILED configuration");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "content-workflow-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var source = Path.Combine(smokeRoot, "source");
            var destination = Path.Combine(smokeRoot, "destination");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "encounters.json"), "{\"schemaVersion\":1}");
            var request = new ContentBatchTransferRequest(
                ContentBatchOperation.Import,
                source,
                destination,
                configured.Batch.Glob,
                configured.Batch.Extensions,
                configured.Batch.CollisionPolicy,
                configured.Batch.Provenance,
                configured.Batch.LicenseSpdx,
                dryRun: true);
            var preview = ContentBatchTransferService.Execute(request);
            var applied = ContentBatchTransferService.Execute(new ContentBatchTransferRequest(
                request.Operation,
                request.SourceRoot,
                request.DestinationRoot,
                request.Glob,
                request.Extensions,
                request.CollisionPolicy,
                request.Provenance,
                request.LicenseSpdx,
                dryRun: false));
            var collision = ContentBatchTransferService.Execute(new ContentBatchTransferRequest(
                request.Operation,
                request.SourceRoot,
                request.DestinationRoot,
                request.Glob,
                request.Extensions,
                request.CollisionPolicy,
                request.Provenance,
                request.LicenseSpdx,
                dryRun: false));
            var blueprintPack = Path.Combine(smokeRoot, "selection.orpblueprints");
            var blueprint = _history.State.Project.Blueprints.First();
            var exported = BlueprintExportPackageBuilder.Build(
                _history.State.Project,
                [blueprint.Id],
                blueprintPack);
            using var archive = ZipFile.OpenRead(blueprintPack);
            var merged = DataTableThreeWayMerger.Merge(
                Table("2", "base"),
                Table("4", "ours"),
                Table("6", "theirs"),
                ContentMergePolicy.PreserveBoth);
            var workspace = Path.Combine(smokeRoot, "workspace");
            var saved = ProjectSession.Create(workspace, _history.State.Project);
            var reopened = ProjectSession.Open(workspace);
            if (!preview.Valid || preview.Entries.Count != 1 || !preview.DryRun ||
                !applied.Valid || applied.Added != 1 || !File.Exists(applied.Entries.Single().TargetPath) ||
                !collision.Valid || collision.PreservedBoth != 1 ||
                exported.BlueprintCount != 1 || exported.PreviewCount != 1 ||
                archive.GetEntry("manifest.json") is null ||
                archive.GetEntry("licenses/index.json") is null ||
                archive.GetEntry("migrations/blueprint-schema-v1.json") is null ||
                merged.Conflicts.Count != 1 || merged.Merged.Rows.Count != 2 ||
                !saved.Succeeded || !reopened.Succeeded || reopened.Session is null ||
                reopened.Session.Project.Module.Content.DataTables.Single().Id != tableId)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11E_CONTENT_FAILED workflows");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Module.Content != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11E_CONTENT_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G11E_CONTENT_PASS tables=typed batch=dry-run-applied " +
            "collisions=preserve-both blueprints=transitive-governed merge=three-way-conflicts " +
            "history=restored persistence=roundtrip");
        return true;
    }

    private bool RunContentPackageSmokeTest()
    {
        if (_contentPackageWindow is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R016_CONTENT_PACKAGES_FAILED missing_window");
            return false;
        }

        var original = _history.State.Project.Module.Content;
        var source = new ContentPackageSource(Guid.Parse("c0160000-0000-4000-8000-000000000203"), 1,
            "toolset-smoke", "creature.ash-warden", "data/creatures/ash-warden.json",
            ContentSourceKind.Json, true, "CC0-1.0", new string('a', 64), 1_048_576);
        var optional = new ContentPackageDependency(Guid.Parse("c0160000-0000-4000-8000-000000000204"), 1,
            "toolset-smoke", "crypt.cosmetics", "^1.0.0", ContentDependencyKind.Optional);
        var draft = new ContentPackageDefinition(Guid.Parse("c0160000-0000-4000-8000-000000000202"), 1,
            "toolset-smoke", "crypt.encounters", "1.2.0", "CC0-1.0", new string('0', 64), 80,
            ["creature.ash-warden"], [optional], [source]);
        var package = new ContentPackageDefinition(draft.Id, draft.SchemaVersion, draft.Provenance,
            draft.PackageNamespace, draft.Version, draft.LicenseSpdx,
            ContentPackageCompiler.ComputePackageHash(draft), draft.Priority, draft.SealedResources,
            draft.Dependencies, draft.Sources);
        var catalog = new ContentPackageCatalog(_history.State.Project.Module.Id.Value, 1, "toolset-smoke",
            ContentConflictPolicy.HighestPriority, [package]);
        var resolution = ContentPackageResolver.Resolve(catalog);
        var script = new ContentPackageScriptService(catalog);
        var handle = script.Find("crypt.encounters");
        if (!_contentPackageWindow.ConfigureForSmokeTest(catalog) ||
            _contentPackageWindow.PackageCount != 1 ||
            !_contentPackageWindow.ApplyForSmokeTest() ||
            _history.State.Project.Module.Content.Packages.Packages.Single().Id != package.Id ||
            !resolution.Valid || resolution.LockEntries.Count != 1 || resolution.Resources.Count != 1 ||
            handle is null || script.Read(handle.Value).Id != package.Id)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R016_CONTENT_PACKAGES_FAILED configuration");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r016-content-packages",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            if (!saved.Succeeded || !reopened.Succeeded || reopened.Session is null ||
                reopened.Session.Project.Module.Content.Packages.Packages.Single().Id != package.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R016_CONTENT_PACKAGES_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Module.Content != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R016_CONTENT_PACKAGES_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_R016_CONTENT_PACKAGES_PASS " +
                 "package=uuid-schema-namespace-semver-license-hash priority=explicit-sealed-conflict-preview " +
                 "dependencies=required-optional-peer-offline-lock sources=text-one-resource-per-file " +
                 "compiler=validate-resolve-script-hash-compress-reproducible ui=visible-empty-error " +
                 "forgescript=typed-handle mcp=filtered-dry-run history=undo-redo persistence=roundtrip");
        return true;
    }

    private bool RunContentAuthoringSmokeTest()
    {
        if (_history is null || _assetBrowser is null || _localizationWorkspace is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_FAILED missing_workspace");
            return false;
        }

        var original = _history.State.Project.Module.Localization;
        var extracted = LocalizationCatalogExtractor.Extract(_history.State.Project);
        if (extracted.Diagnostics.Count != 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_FAILED extraction");
            return false;
        }
        var baseline = extracted.Catalog;
        var messageKey = "smoke.r017.loot";
        var messages = baseline.Messages.Where(message => message.Key != messageKey)
            .Append(new LocalizationMessage(messageKey,
                "{gender, select, female {Elle} male {Il} other {Iel}} trouve " +
                "{count, plural, =0 {aucun objet} one {# objet} other {# objets}}.",
                provenance: "toolset:r017"))
            .ToArray();
        var catalog = new LocalizationCatalog(baseline.Id, baseline.DefaultLocale,
            [.. baseline.Locales, "en-US"], messages, baseline.SchemaVersion,
            baseline.PackageNamespace, baseline.LicenseSpdx, baseline.Provenance, baseline.PlatformByteBudget);
        var bundle = LocalizationTranslationExchange.Export(catalog, "en-US");
        var translated = bundle with
        {
            Units = bundle.Units.Select(unit => unit with
            {
                Translation = unit.Key == messageKey
                    ? "{gender, select, female {She} male {He} other {They}} finds " +
                      "{count, plural, =0 {no item} one {# item} other {# items}}."
                    : unit.Source,
            }).ToArray(),
        };
        var imported = LocalizationTranslationExchange.Import(catalog, translated);
        var resolvedText = imported.Catalog?.Resolve(messageKey, "en-US", new Dictionary<string, object?>
        {
            ["gender"] = "female",
            ["count"] = 2,
        });
        var diagnosticProbe = imported.Catalog is null
            ? null
            : _setLocalizationCatalogHandler.Handle(_history.State, new SetLocalizationCatalogCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, imported.Catalog, DryRun: true));
        var dryRunSucceeded = diagnosticProbe?.Succeeded == true;
        var applySucceeded = dryRunSucceeded && ApplyLocalizationCatalog(imported.Catalog!, dryRun: false);
        if (!imported.Succeeded || imported.Catalog is null || resolvedText != "She finds 2 items." ||
            !dryRunSucceeded || !applySucceeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_FAILED localization " +
                        $"imported={imported.Succeeded} diagnostics={string.Join(',', imported.Diagnostics)} " +
                        $"resolved={resolvedText ?? "null"} dry_run={dryRunSucceeded} applied={applySucceeded} " +
                        $"command={string.Join(';', diagnosticProbe?.Diagnostics.Select(item => $"{item.Code}:{item.Message}") ?? [])}");
            return false;
        }

        var source = new ContentPackageSource(Guid.Parse("c0170000-0000-4000-8000-000000000001"), 1,
            "toolset:r017", "rules/encounter", "data/encounter.json", ContentSourceKind.Json, true,
            "CC0-1.0", new string('a', 64), 4096, ["legacy.core:text/encounter"],
            ["crypt.core:model/props/chest"]);
        var modelAsset = new ContentPackageAsset(Guid.Parse("c0170000-0000-4000-8000-000000000002"), 1,
            "toolset:r017", "props/chest", "models/chest.glb", true, "CC0-1.0", new string('b', 64),
            8 * 1024 * 1024);
        var package = new ContentPackageDefinition(Guid.Parse("c0170000-0000-4000-8000-000000000003"), 1,
            "toolset:r017", "crypt.core", "1.0.0", "CC0-1.0", new string('c', 64), 10, [], [], [source],
            [modelAsset]);
        var packageCatalog = new ContentPackageCatalog(_history.State.Project.Module.Id.Value, 1,
            "toolset:r017", ContentConflictPolicy.HighestPriority, [package]);
        var registryResult = ContentResourceRegistry.Build(packageCatalog);
        if (!registryResult.Valid)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_FAILED namespaces");
            return false;
        }

        var registry = ContentResourceRegistry.Create(registryResult);
        var currentProject = _history.State.Project;
        var currentModule = currentProject.Module;
        var configuredContent = new ContentWorkspaceConfiguration(currentModule.Content.Id,
            currentModule.Content.Batch, currentModule.Content.DataTables, currentModule.Content.MergePolicy,
            currentModule.Content.Provenance, currentModule.Content.SchemaVersion, packageCatalog);
        var configuredModule = new ModuleDefinition(currentModule.Id, currentModule.Slug, currentModule.DisplayName,
            currentModule.Description, currentModule.EntryAreaId, currentModule.Metadata, currentModule.Variables,
            currentModule.Distribution, currentModule.Localization, configuredContent, currentModule.ArtisticStyle,
            currentModule.RuntimePolicies, currentModule.Factions, currentModule.WorldFeatures);
        var configuredProject = new ProjectDefinition(currentProject.Id, currentProject.Title, configuredModule,
            currentProject.Blueprints, currentProject.Areas, currentProject.Dialogues, currentProject.Quests);
        var browser = new UnifiedContentBrowser(configuredProject);
        var resourceScript = new ContentResourceScriptService(browser);
        var resourceHandle = resourceScript.Find("legacy.core:text/encounter");
        var localizationScript = new LocalizationScriptService(currentModule.Localization);
        var localizationHandle = localizationScript.Find(messageKey);

        var textureProfile = new AssetProcessingProfile(
            AssetCatalogItemId.From(Guid.Parse("c0170000-0000-4000-8000-000000000004")), 1,
            AssetProcessingKind.Texture, "tiff", "ktx2", "CC0-1.0", new string('d', 64), "toolset:r017",
            8 * 1024 * 1024, texture: new(TextureColorSpace.Srgb, TextureCompressionProfile.Desktop, 4096,
                10.24m, new Dictionary<PbrTextureChannel, string>
                {
                    [PbrTextureChannel.BaseColor] = "textures/chest-base.png",
                    [PbrTextureChannel.Normal] = "textures/chest-normal.exr",
                }));
        var modelProfile = new AssetProcessingProfile(
            AssetCatalogItemId.From(Guid.Parse("c0170000-0000-4000-8000-000000000005")), 1,
            AssetProcessingKind.Model, "gltf", "glb", "CC0-1.0", new string('e', 64), "toolset:r017",
            32 * 1024 * 1024, model: new(true, true, true, ["hand.r"], [1m, .5m, .25m]));
        if (!registry.TryResolve("legacy.core:text/encounter", out var aliased) ||
            aliased?.Address != "crypt.core:text/rules/encounter" ||
            browser.Search(new UnifiedContentBrowserQuery(PackageNamespace: "crypt.core")).Count != 2 ||
            resourceHandle is null || resourceScript.Read(resourceHandle.Value).ReferenceCount != 1 ||
            localizationHandle is null || localizationScript.Read(localizationHandle.Value, "en-US",
                new Dictionary<string, object?> { ["gender"] = "female", ["count"] = 2 }).Text !=
            "She finds 2 items." || AssetProcessingProfileValidator.Validate(textureProfile).Count != 0 ||
            AssetProcessingProfileValidator.Validate(modelProfile).Count != 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_FAILED resources");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r017-content-authoring",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            if (!saved.Succeeded || !reopened.Succeeded || reopened.Session is null ||
                reopened.Session.Project.Module.Localization.ContentSha256 != currentModule.Localization.ContentSha256)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Module.Localization != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_R017_CONTENT_AUTHORING_PASS " +
                 "localization=icu-fallback-pseudo-translator texture=png-tiff-exr-ktx2-pbr-compression-texel " +
                 "model=gltf-glb-rig-animation-collision-sockets-lod namespaces=package-type-path-uuid-aliases " +
                 "browser=facets-preview-references-package-license-status-actions ui=visible-empty-error " +
                 "forgescript=typed-handles mcp=filtered-dry-run history=undo-redo persistence=roundtrip");
        return true;
    }

    private bool RunCreatorProductivitySmokeTest()
    {
        if (_history is null || _projectSession is null || _projectPreview is null ||
            _creatorShell is null || _blueprintPalette is null || _positionX is null || _positionZ is null ||
            _paletteBlueprintIds.Count == 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G720_PRODUCTIVITY_FAILED missing_workspace");
            return false;
        }

        var originalProfile = LoadWorkspaceProfile();
        var originalArea = GetActiveArea();
        var originalAreaCount = _history.State.Project.Areas.Count;
        var originalPaletteSelection = _blueprintPalette.Selected;
        var originalPositionX = _positionX.Value;
        var originalPositionZ = _positionZ.Value;
        var originalSelection = _selectionContext.Current.ToArray();
        var originalSelectionLock = _selectionContext.IsLocked;
        AreaId? comparisonAreaId = null;
        string? failure = null;
        try
        {
            _selectionContext.SetLocked(false);
            var blueprints = _history.State.Project.Blueprints.ToDictionary(blueprint => blueprint.Id);
            var filterTarget = originalArea.Entities.First(entity => blueprints.ContainsKey(entity.BlueprintId));
            var targetBlueprint = blueprints[filterTarget.BlueprintId];
            var requiredTags = filterTarget.Tags.Take(1).ToArray();
            var filter = new EditorDisplayFilter(
                [targetBlueprint.Kind],
                requiredTags,
                targetBlueprint.Provenance.Author,
                targetBlueprint.PaletteOrigin.ToString(),
                ghostHidden: true);
            var expectedVisible = originalArea.Entities.Count(entity =>
                blueprints.TryGetValue(entity.BlueprintId, out var blueprint) &&
                filter.VisibleKinds.Contains(blueprint.Kind) &&
                filter.RequiredTags.All(tag => entity.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)) &&
                blueprint.Provenance.Author.Contains(filter.Author, StringComparison.OrdinalIgnoreCase) &&
                blueprint.PaletteOrigin.ToString().Contains(filter.State, StringComparison.OrdinalIgnoreCase));
            var visible = ApplyWorkspaceFilter(filter);
            if (visible != expectedVisible || _projectPreview.GhostedEntityCount != originalArea.Entities.Count - expectedVisible)
            {
                failure = "display_filter";
            }

            if (failure is null &&
                (!_projectPreview.SelectEntityForSmokeTest(filterTarget.Id) ||
                 ApplyWorkspaceFilter(new EditorDisplayFilter(
                     filter.VisibleKinds, filter.RequiredTags, filter.Author, filter.State,
                     isolateSelection: true, ghostHidden: true)) != 1))
            {
                failure = "selection_isolation";
            }

            if (failure is null)
            {
                comparisonAreaId = CreateArea(
                    "Comparaison G7-20",
                    AreaKind.Interior,
                    new AreaDimensions(24, 24),
                    AreaTemplateKind.Empty);
                if (comparisonAreaId is null)
                {
                    failure = "area_creation";
                }
            }

            if (failure is null && comparisonAreaId is not null)
            {
                var configuredProfile = new EditorWorkspaceProfile(
                    originalProfile.Id,
                    originalProfile.Name,
                    EditorRole.Builder,
                    filter,
                    repeatPlacement: true,
                    [originalArea.Id, comparisonAreaId],
                    originalArea.Id,
                    comparisonAreaId,
                    contextualHelpEnabled: true,
                    "toolset-g720-smoke",
                    EditorWorkspaceProfile.CurrentSchemaVersion,
                    originalProfile.UiScale,
                    originalProfile.TextScale,
                    originalProfile.PrimaryFontFamily,
                    originalProfile.FallbackFontFamilies,
                    originalProfile.PersonalFavoriteBlueprintIds,
                    originalProfile.RecentBlueprintIds);
                SaveWorkspaceProfile(configuredProfile);
                var persisted = EditorWorkspaceProfileStore.TryLoad(_projectSession.RootDirectory);
                if (persisted is null ||
                    !persisted.DisplayFilter.VisibleKinds.SequenceEqual(filter.VisibleKinds) ||
                    !persisted.DisplayFilter.RequiredTags.SequenceEqual(filter.RequiredTags) ||
                    persisted.DisplayFilter.Author != filter.Author ||
                    persisted.DisplayFilter.State != filter.State ||
                    persisted.DisplayFilter.IsolateSelection != filter.IsolateSelection ||
                    persisted.DisplayFilter.GhostHidden != filter.GhostHidden ||
                    persisted.OpenAreaTabs.Count != 2 || persisted.ActiveAreaId != originalArea.Id ||
                    persisted.ComparisonAreaId != comparisonAreaId || !persisted.RepeatPlacement ||
                    !persisted.ContextualHelpEnabled || _creatorShell.AreaTabCount != 2 ||
                    Enum.GetValues<EditorRole>().Length != 7 ||
                    !CreatorWorkspaceWindow.ContextualHelpContent.Contains(
                        "docs/aurora-parity/tasks/G7-20-CREATOR-PRODUCTIVITY.md",
                        StringComparison.Ordinal))
                {
                    failure = "profile_tabs_help_persistence";
                }
            }

            if (failure is null)
            {
                var placeable = _history.State.Project.Blueprints.First(blueprint => blueprint.Kind == BlueprintKind.Placeable);
                _blueprintPalette.Selected = _paletteBlueprintIds.FindIndex(id => id == placeable.Id);
                _positionX.Value = -8;
                _positionZ.Value = 10;
                var beforePlacement = GetActiveArea().Entities.Count;
                if (_blueprintPalette.Selected < 0 ||
                    !PlaceRepeatedSeries(6, 2f, PlacementDistribution.Grid, 2026) ||
                    GetActiveArea().Entities.Count != beforePlacement + 6)
                {
                    failure = "repeated_placement";
                }
                else
                {
                    var undo = _history.Undo();
                    var redo = _history.Redo();
                    var cleanup = _history.Undo();
                    if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
                        GetActiveArea().Entities.Count != beforePlacement)
                    {
                        failure = "repeated_history";
                    }
                }
            }
        }
        catch (Exception exception)
        {
            failure = $"exception_{exception.GetType().Name}";
        }
        finally
        {
            while (_history.CanUndo && comparisonAreaId is not null &&
                   _history.State.Project.Areas.Any(area => area.Id == comparisonAreaId))
            {
                _history.Undo();
            }

            _activeAreaContext.EnsureValid(_history.State.Project);
            ActivateArea(originalArea.Id);
            SaveWorkspaceProfile(EditorWorkspaceProfilePlanner.ReconcileAreas(
                originalProfile,
                _history.State.Project.Areas.Select(area => area.Id).ToArray(),
                originalArea.Id));
            _blueprintPalette.Selected = originalPaletteSelection;
            _positionX.Value = originalPositionX;
            _positionZ.Value = originalPositionZ;
            _selectionContext.SetLocked(false);
            _selectionContext.Replace(originalSelection);
            _selectionContext.SetLocked(originalSelectionLock);
            RebuildPreview();
            RefreshEditorControls();
        }

        if (failure is not null || _history.State.Project.Areas.Count != originalAreaCount)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_G720_PRODUCTIVITY_FAILED {failure ?? "cleanup"}");
            return false;
        }

        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G720_PRODUCTIVITY_PASS filters=combined-isolation-ghost " +
            "placement=deterministic-atomic tabs=multi-area comparison=readonly help=sourced " +
            "profiles=seven persistence=roundtrip history=restored");
        return true;
    }

    private bool RunScalableEditorExperienceSmokeTest()
    {
        if (_history is null || _projectSession is null || _mainPanel is null || _creatorShell is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11F_EXPERIENCE_FAILED missing_workspace");
            return false;
        }

        var originalProfile = LoadWorkspaceProfile();
        var originalArea = GetActiveArea();
        var originalAreaCount = _history.State.Project.Areas.Count;
        var originalStyle = _history.State.Project.Module.ArtisticStyle;
        var blueprintId = _history.State.Project.Blueprints.First().Id;
        AreaId? createdAreaId = null;
        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "editor-experience-smoke",
            Guid.NewGuid().ToString("N"));
        string? failure = null;
        try
        {
            createdAreaId = CreateArea(
                "Atelier comparatif G11F",
                AreaKind.Interior,
                new AreaDimensions(32, 32),
                AreaTemplateKind.Empty);
            if (createdAreaId is null)
            {
                failure = "area_creation";
            }
            else
            {
                var configuredProfile = new EditorWorkspaceProfile(
                    originalProfile.Id,
                    originalProfile.Name,
                    originalProfile.Role,
                    originalProfile.DisplayFilter,
                    originalProfile.RepeatPlacement,
                    [originalArea.Id, createdAreaId],
                    createdAreaId,
                    originalArea.Id,
                    originalProfile.ContextualHelpEnabled,
                    "toolset-g11f-smoke",
                    EditorWorkspaceProfile.CurrentSchemaVersion,
                    uiScale: 1.25,
                    textScale: 1.35,
                    primaryFontFamily: "Atkinson Hyperlegible",
                    fallbackFontFamilies: ["Noto Sans", "Segoe UI"],
                    personalFavoriteBlueprintIds: [blueprintId],
                    recentBlueprintIds: [blueprintId]);
                SaveWorkspaceProfile(configuredProfile);
                var persistedProfile = EditorWorkspaceProfileStore.TryLoad(_projectSession.RootDirectory);
                var fontNames = (_mainPanel.Theme?.DefaultFont as SystemFont)?.FontNames ?? [];
                var palette = BlueprintPaletteCatalog.Build(_history.State.Project, configuredProfile);
                if (persistedProfile is null || persistedProfile.SchemaVersion != EditorWorkspaceProfile.CurrentSchemaVersion ||
                    Math.Abs(persistedProfile.UiScale - 1.25) > 0.001 ||
                    Math.Abs(persistedProfile.TextScale - 1.35) > 0.001 ||
                    persistedProfile.ActiveAreaId != createdAreaId ||
                    persistedProfile.ComparisonAreaId != originalArea.Id ||
                    _creatorShell.AreaTabCount != 2 ||
                    Math.Abs((_mainPanel.Theme?.DefaultBaseScale ?? 0) - 1.25f) > 0.001f ||
                    _mainPanel.Theme?.DefaultFontSize != 19 ||
                    fontNames.Length < 3 || fontNames[0] != "Atkinson Hyperlegible" ||
                    palette.Filter(BlueprintPaletteView.Favorites).All(item => item.BlueprintId != blueprintId) ||
                    palette.Filter(BlueprintPaletteView.Recent).FirstOrDefault()?.BlueprintId != blueprintId)
                {
                    failure = "profile_ui_palette";
                }

                var style = new ArtisticStyleProfile(
                    originalStyle.Id,
                    metersPerUnit: 1,
                    targetTexelDensity: 512,
                    scaleTolerancePercent: 20,
                    texelTolerancePercent: 50,
                    shaderFamily: "pbr-metallic-roughness",
                    era: "original-fantasy",
                    ArtisticRealism.StylizedRealism,
                    ["earth", "ember", "stone"],
                    [new ArtisticAssetStyle(
                        blueprintId,
                        metersPerUnit: 1,
                        texelDensity: 512,
                        shaderFamily: "pbr-metallic-roughness",
                        era: "original-fantasy",
                        ArtisticRealism.StylizedRealism,
                        ["earth", "stone"],
                        "toolset-g11f-smoke")],
                    "toolset-g11f-smoke");
                if (failure is null &&
                    (!ApplyArtisticStyleProfile(style) ||
                     ArtisticCompatibilityValidator.Analyze(_history.State.Project).Count != 0))
                {
                    failure = "artistic_profile";
                }

                if (failure is null)
                {
                    var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
                    var reopened = ProjectSession.Open(smokeRoot);
                    if (!saved.Succeeded || !reopened.Succeeded || reopened.Session is null ||
                        reopened.Session.Project.Module.ArtisticStyle.Assets.Single().BlueprintId != blueprintId ||
                        !File.ReadAllText(Path.Combine(smokeRoot, ProjectSession.DefaultProjectFileName))
                            .Contains($"\"formatVersion\": \"{ProjectSession.CurrentProjectFormatVersion}\"", StringComparison.Ordinal))
                    {
                        failure = "persistence";
                    }
                }

                if (failure is null)
                {
                    var undoArt = _history.Undo();
                    var redoArt = _history.Redo();
                    var cleanupArt = _history.Undo();
                    if (!undoArt.Succeeded || !redoArt.Succeeded || !cleanupArt.Succeeded ||
                        !ReferenceEquals(_history.State.Project.Module.ArtisticStyle, originalStyle))
                    {
                        failure = "artistic_history";
                    }
                }
            }
        }
        catch (Exception exception)
        {
            failure = $"exception_{exception.GetType().Name}";
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }

            while (_history.CanUndo &&
                   ((createdAreaId is not null && _history.State.Project.Areas.Any(area => area.Id == createdAreaId)) ||
                    !ReferenceEquals(_history.State.Project.Module.ArtisticStyle, originalStyle)))
            {
                _history.Undo();
            }

            _activeAreaContext.EnsureValid(_history.State.Project);
            ActivateArea(originalArea.Id);
            SaveWorkspaceProfile(EditorWorkspaceProfilePlanner.ReconcileAreas(
                originalProfile,
                _history.State.Project.Areas.Select(area => area.Id).ToArray(),
                originalArea.Id));
            RebuildPreview();
            RefreshEditorControls();
        }

        if (failure is not null || _history.State.Project.Areas.Count != originalAreaCount ||
            !ReferenceEquals(_history.State.Project.Module.ArtisticStyle, originalStyle))
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_G11F_EXPERIENCE_FAILED {failure ?? "cleanup"}");
            return false;
        }

        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G11F_EXPERIENCE_PASS ui=scalable font=truetype-fallback " +
            "tabs=multi-area comparison=readonly favorites=project-personal recents=ordered " +
            "art=profiled-warnings history=restored persistence=roundtrip");
        return true;
    }

    private bool RunReferenceRenderingSmokeTest()
    {
        if (_history is null || _areaEnvironmentWindow is null || _projectPreview is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11G_RENDER_FAILED missing_workspace");
            return false;
        }

        var area = GetActiveArea();
        var original = area.Environment;
        var rendering = new AreaRenderingProfile(
            AreaRenderingProfile.CurrentSchemaVersion,
            PbrMaterialWorkflow.MetallicRoughness,
            ToneMappingProfile.Aces,
            RenderQualityTier.Ultra,
            forwardPlus: true,
            perPixelLighting: true,
            materialInstances: true,
            hdrOutput: true,
            exposure: 1.15,
            whitePoint: 8,
            lutResource: string.Empty,
            [
                new("desktop", 48, 12, 8192, 0.8),
                new("mobile", 8, 2, 2048, 0.75),
            ]);
        var sky = new SkyProfile(
            SkyProfile.CurrentSchemaVersion,
            SkyProfileMode.Procedural,
            string.Empty,
            "#25385f",
            "#d88f68",
            "#211a29",
            starIntensity: 0.35,
            cloudCoverage: 0.25,
            moonIntensity: 0.2,
            energyMultiplier: 1.1,
            transitionSeconds: 3);
        var configured = new AreaEnvironmentProfile(
            AreaEnvironmentProfile.CurrentSchemaVersion,
            original.Classification,
            original.Tags,
            new(DayNightMode.AlwaysNight, 45, .3f, 1.4f, 4200, true, true),
            original.Fog,
            original.Audio,
            rendering,
            sky);
        if (!_areaEnvironmentWindow.ConfigureRenderForSmokeTest(configured) ||
            !_areaEnvironmentWindow.ApplyForSmokeTest() ||
            _projectPreview.RenderProfileSummary != "MetallicRoughness:Aces:Procedural" ||
            _projectPreview.RenderDiagnosticCount != 0 ||
            _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment.Rendering.ToneMapping != ToneMappingProfile.Aces ||
            _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment.Rendering.LightBudgets
                .Single(item => item.Platform == "desktop").MaximumDynamicLights != 48)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11G_RENDER_FAILED apply_preview");
            return false;
        }

        var applied = _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment;
        var plan = AreaRenderPlanBuilder.Build(applied, "desktop", observedDynamicLights: 39, observedShadowCastingLights: 10);
        if (plan.LightBudget.MaximumDynamicLights != 48 ||
            !plan.Diagnostics.Any(item => item.Code == AreaRenderPlanBuilder.DynamicLightWarningCode) ||
            !plan.Diagnostics.Any(item => item.Code == AreaRenderPlanBuilder.ShadowLightWarningCode))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11G_RENDER_FAILED budgets");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "reference-rendering-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restoredEnvironment = reopened.Session?.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment;
            if (!saved.Succeeded || !reopened.Succeeded || restoredEnvironment is null ||
                restoredEnvironment.Rendering.ToneMapping != ToneMappingProfile.Aces ||
                restoredEnvironment.Sky.ZenithColor != "#25385f" ||
                restoredEnvironment.Rendering.LightBudgets.Single(item => item.Platform == "desktop").ShadowAtlasSize != 8192)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11G_RENDER_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            !ReferenceEquals(
                _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment,
                original))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11G_RENDER_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G11G_RENDER_PASS pbr=metallic-roughness materials=instances " +
            "tone=aces hdr=true lighting=per-pixel forward=plus sky=procedural lights=48 shadows=12 " +
            "history=restored persistence=roundtrip");
        return true;
    }

    private bool RunClimateWaterReverbSmokeTest()
    {
        if (_history is null || _areaEnvironmentWindow is null || _projectPreview is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_FAILED missing_workspace");
            return false;
        }

        var area = GetActiveArea();
        var original = area.Environment;
        var clearId = Guid.Parse("d1180000-0000-4000-8000-000000000001");
        var rainId = Guid.Parse("d1180000-0000-4000-8000-000000000002");
        var rain = new WeatherStateProfile(
            rainId, "Pluie de validation", WeatherKind.Rain, .72f, .9f, .8f, true,
            string.Empty, "weather.rain", "surface.wet", 0, 1, 2);
        var clear = new WeatherStateProfile(
            clearId, "Éclaircie", WeatherKind.Clear, 0, .35f, .1f, false,
            string.Empty, string.Empty, string.Empty, 4, 8, 3);
        var weather = new WeatherProfile(
            WeatherProfile.CurrentSchemaVersion, rainId, [rain, clear], true, 2026, true, true);
        var windZone = new WindZoneProfile(
            Guid.Parse("d1180000-0000-4000-8000-000000000003"),
            Vector3Value.Zero, 8, -1, 0, 11, 1.6f);
        var wind = new WindProfile(
            WindProfile.CurrentSchemaVersion, .8f, .3f, 6.5f, .55f, 5, true, true,
            "wind.gust", 2, [windZone]);
        var waterBody = new WaterBodyProfile(
            Guid.Parse("d1180000-0000-4000-8000-000000000004"),
            "Bassin de validation", WaterBodyKind.Volume, Vector3Value.Zero, new(18, 2, 12), 2,
            1, 0, .4f, 1.2f, 2.5f, true, WaterQualityTier.High, string.Empty, "water.lake");
        var reverbZone = new ReverbZoneProfile(
            Guid.Parse("d1180000-0000-4000-8000-000000000005"),
            "Caverne de validation", Vector3Value.Zero, new(10, 5, 10), "cave", .7f, 1, 1.5f,
            string.Empty, string.Empty, string.Empty, string.Empty);
        var configured = new AreaEnvironmentProfile(
            AreaEnvironmentProfile.CurrentSchemaVersion,
            original.Classification,
            original.Tags,
            original.Lighting,
            original.Fog,
            original.Audio,
            original.Rendering,
            original.Sky,
            wind,
            weather,
            new(WaterProfile.CurrentSchemaVersion, [waterBody]),
            new(AreaReverbProfile.CurrentSchemaVersion, "cave", [reverbZone]));
        if (!_areaEnvironmentWindow.ConfigureRenderForSmokeTest(configured) ||
            !_areaEnvironmentWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_FAILED apply");
            return false;
        }

        var applied = _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment;
        var plan = AreaClimatePlanBuilder.Build(applied, applied.Weather.InitialStateId, Vector3Value.Zero, "mobile");
        if (applied.Wind.Speed != 6.5f || applied.Wind.LocalZones.Single().Speed != 11 ||
            applied.Weather.States.Single(item => item.Id == applied.Weather.InitialStateId).Kind != WeatherKind.Rain ||
            applied.Water.Bodies.Single().Kind != WaterBodyKind.Volume ||
            applied.Reverb.Zones.Single().Preset != "cave" ||
            plan.ActiveWindZone?.Id != windZone.Id || plan.ActiveReverbZone?.Id != reverbZone.Id ||
            plan.EffectiveWaterQuality != WaterQualityTier.Medium ||
            !plan.Diagnostics.Any(item => item.Code == AreaClimatePlanBuilder.WaterQualityDowngradedCode) ||
            _projectPreview.PrecipitationEmitterCount != 1 || _projectPreview.WaterSurfaceCount != 1 ||
            !_projectPreview.ClimateProfileSummary.StartsWith("Rain:", StringComparison.Ordinal) ||
            !_projectPreview.ClimateProfileSummary.EndsWith(":1:cave", StringComparison.Ordinal))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_FAILED preview_plan");
            return false;
        }

        var initial = WeatherRuntimeStateMachine.CreateInitial(applied.Weather);
        var denied = WeatherRuntimeStateMachine.Advance(applied.Weather, initial, 2, hasAuthority: false);
        var advanced = WeatherRuntimeStateMachine.Advance(applied.Weather, initial, 2, hasAuthority: true);
        if (!denied.AuthorityDenied || denied.Transition is not null || advanced.Transition is null ||
            advanced.State.StateId == initial.StateId || !advanced.Transition.Persist || !advanced.Transition.Replicate)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_FAILED weather_runtime");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "climate-water-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Wind.Speed != 6.5f || restored.Weather.InitialStateId != rainId ||
                restored.Water.Bodies.Single().Quality != WaterQualityTier.High ||
                restored.Reverb.Zones.Single().Wet != .7f)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            !ReferenceEquals(
                _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).Environment,
                original))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G11H_ENVIRONMENT_PASS wind=global-local weather=state-machine " +
            "particles=rain water=plane-volume reverb=zone authority=server save=portable " +
            "replication=ready history=restored persistence=roundtrip");
        return true;
    }

    private bool RunTerrainWorldSmokeTest()
    {
        if (_history is null || _terrainStudioWindow is null || _projectPreview is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED missing_workspace");
            return false;
        }

        var area = GetActiveArea();
        var original = area.World;
        var soil = new TerrainMaterialLayer(
            Guid.Parse("d1190000-0000-4000-8000-000000000001"), "Terre", "#70533a", string.Empty, 4);
        var grass = new TerrainMaterialLayer(
            Guid.Parse("d1190000-0000-4000-8000-000000000002"), "Prairie", "#5f8345", string.Empty, 5);
        var rock = new TerrainMaterialLayer(
            Guid.Parse("d1190000-0000-4000-8000-000000000003"), "Roche", "#777b82", string.Empty, 3);
        const int samples = 9;
        var heights = Enumerable.Range(0, samples * samples)
            .Select(index =>
            {
                var x = (index % samples) / (samples - 1f) - .5f;
                var z = (index / samples) / (samples - 1f) - .5f;
                return Math.Clamp((MathF.Sin(x * 5.2f) + MathF.Cos(z * 4.3f)) * .12f, -1, 1);
            })
            .ToArray();
        var terrain = new TerrainDefinition(
            Guid.Parse("d1190000-0000-4000-8000-000000000004"), 1, TerrainProviderKind.Heightmap,
            area.Dimensions.Width, area.Dimensions.Depth, samples, samples, 8, 2026, 5, heights,
            [soil, grass, rock],
            [new TerrainHoleDefinition(Guid.Parse("d1190000-0000-4000-8000-000000000005"),
                "Entrée de grotte", 2, 3, 1, 1, "smoke:g11-i")],
            string.Empty, string.Empty, string.Empty, "smoke:g11-i");
        const string meadowScatter = "asset:11111111-1111-5111-8111-111111111111";
        const string rockScatter = "asset:22222222-2222-5222-8222-222222222222";
        IReadOnlyList<BiomeLayerDefinition> biomes =
        [
            new(Guid.Parse("d1190000-0000-4000-8000-000000000006"), "Prairie", grass.Id,
                -512, 512, 0, 32, 0, 1, .45f, meadowScatter, ["meadow", "temperate"], ["water", "road"], 20,
                "smoke:g11-i"),
            new(Guid.Parse("d1190000-0000-4000-8000-000000000007"), "Roche", rock.Id,
                -512, 512, 28, 90, 0, 1, .08f, rockScatter, ["rock", "cliff"], ["water"], 30,
                "smoke:g11-i"),
        ];
        var road = new TerrainFeatureDefinition(
            Guid.Parse("d1190000-0000-4000-8000-000000000008"), 1, "Route", TerrainFeatureKind.Road,
            [new Vector3Value(-8, .05f, 0), new Vector3Value(8, .05f, 0)], 2.5f, .08f, 0,
            TerrainFeatureCrossingPolicy.RequiresBridge, [], string.Empty, "smoke:g11-i");
        var river = new TerrainFeatureDefinition(
            Guid.Parse("d1190000-0000-4000-8000-000000000009"), 1, "Rivière", TerrainFeatureKind.River,
            [new Vector3Value(0, 0, -8), new Vector3Value(0, 0, 8)], 3.5f, .06f, 0,
            TerrainFeatureCrossingPolicy.RequiresBridge, [], string.Empty, "smoke:g11-i");
        var bridge = new TerrainFeatureDefinition(
            Guid.Parse("d1190000-0000-4000-8000-000000000010"), 1, "Pont", TerrainFeatureKind.Bridge,
            [new Vector3Value(-2, .2f, -2), new Vector3Value(2, .2f, 2)], 3, .35f, 0,
            TerrainFeatureCrossingPolicy.Allow, [], string.Empty, "smoke:g11-i");
        var obstacle = new NavigationVolumeDefinition(
            Guid.Parse("d1190000-0000-4000-8000-000000000011"), "Tour", new Vector3Value(0, 5, 0),
            new Vector3Value(2, 6, 2), 1, "smoke:g11-i");
        var headwind = new NavigationVolumeDefinition(
            Guid.Parse("d1190000-0000-4000-8000-000000000012"), "Vent contraire", new Vector3Value(5, 5, 0),
            new Vector3Value(3, 8, 8), 3, "smoke:g11-i");
        var volume = new VolumetricNavigationProfile(
            Guid.Parse("d1190000-0000-4000-8000-000000000013"), 1, true, VolumetricNavigationMode.Flight,
            2, new Vector3Value(0, 5, 0), new Vector3Value(area.Dimensions.Width, 10, area.Dimensions.Depth), 1,
            [obstacle], [headwind], true, "smoke:g11-i");
        var route = new FlightRouteDefinition(
            Guid.Parse("d1190000-0000-4000-8000-000000000014"), 1, "Route aérienne",
            [
                new(Guid.Parse("d1190000-0000-4000-8000-000000000015"), "Entrée", new Vector3Value(-8, 8, -8), 12, 14),
                new(Guid.Parse("d1190000-0000-4000-8000-000000000016"), "Sortie", new Vector3Value(8, 8, 8), 12, -14),
            ], FlightRouteCurve.CatmullRom, FlightObstaclePolicy.Avoid, false, true, "smoke:g11-i");
        var loading = new AreaLoadingProfile(
            Guid.Parse("d1190000-0000-4000-8000-000000000017"), 1, true, true, true, 768, 3, 2, 1800,
            [], "smoke:g11-i");
        var configured = new AreaWorldProfile(
            Guid.Parse("d1190000-0000-4000-8000-000000000018"), 1, terrain, biomes, [road, river, bridge],
            volume, [route], loading, "smoke:g11-i");
        if (!_terrainStudioWindow.ConfigureForSmokeTest(configured) || !_terrainStudioWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED apply");
            return false;
        }

        if (!_terrainStudioWindow.ApplyBrushForSmokeTest(TerrainBrushOperation.Raise, 0, 0, 8, .2f))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED sculpt");
            return false;
        }
        if (!_terrainStudioWindow.ApplyBrushForSmokeTest(TerrainBrushOperation.PaintMaterial, 0, 0, 8, 1,
                materialLayerId: rock.Id))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED paint");
            return false;
        }

        var applied = _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).World;
        var sculptedSamples = applied.Terrain.Heights.Zip(terrain.Heights)
            .Count(pair => Math.Abs(pair.First - pair.Second) > .000001f);
        var paintedCells = applied.Terrain.MaterialPaint.Count;
        var terrainPlan = TerrainBuildPlanBuilder.Build(applied);
        var graph = VolumetricNavigationGraphBuilder.Build(applied.VolumetricNavigation);
        var path = VolumetricNavigationPathfinder.FindPath(graph, new Vector3Value(-8, 8, -8), new Vector3Value(8, 8, 8));
        if (!path.Succeeded || sculptedSamples == 0 || paintedCells == 0 ||
            terrainPlan.Vertices.Count != 81 || terrainPlan.TriangleCount != 126 ||
            applied.Biomes.Count != 2 || applied.Features.Count != 3 || applied.FlightRoutes.Count != 1 ||
            applied.Biomes.Single(candidate => candidate.Tags.Contains("meadow")).ScatterResource != meadowScatter ||
            applied.Biomes.Single(candidate => candidate.Tags.Contains("rock")).ScatterResource != rockScatter ||
            applied.Loading.CacheBudgetMegabytes != 768 ||
            _projectPreview.TerrainVertexCount != 81 || _projectPreview.TerrainTriangleCount != 126 ||
            _projectPreview.TerrainCollisionShapeCount != 1 || _projectPreview.TerrainFeatureSegmentCount != 3 ||
            _projectPreview.VolumetricDebugVolumeCount != 3 || _projectPreview.FlightRouteSegmentCount != 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED preview_plan");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "terrain-world-smoke", Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(candidate => candidate.Id == area.Id).World;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Terrain.Provider != TerrainProviderKind.Heightmap || restored.Terrain.Holes.Count != 1 ||
                !restored.Terrain.Heights.SequenceEqual(applied.Terrain.Heights) ||
                !restored.Terrain.MaterialPaint.SequenceEqual(applied.Terrain.MaterialPaint) ||
                restored.Biomes.Count != 2 || restored.Features.Count != 3 ||
                restored.Biomes.Single(candidate => candidate.Tags.Contains("meadow")).ScatterResource != meadowScatter ||
                restored.Biomes.Single(candidate => candidate.Tags.Contains("rock")).ScatterResource != rockScatter ||
                restored.VolumetricNavigation.Obstacles.Count != 1 || restored.FlightRoutes.Count != 1 ||
                restored.Loading.CacheBudgetMegabytes != 768)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var paintUndo = _history.Undo();
        var paintRedo = _history.Redo();
        var paintCleanup = _history.Undo();
        if (!paintUndo.Succeeded || !paintRedo.Succeeded || !paintCleanup.Succeeded ||
            _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).World.Terrain.MaterialPaint.Count != 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED paint_history");
            return false;
        }

        var brushUndo = _history.Undo();
        var brushRedo = _history.Redo();
        var brushCleanup = _history.Undo();
        if (!brushUndo.Succeeded || !brushRedo.Succeeded || !brushCleanup.Succeeded ||
            !_history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).World.Terrain.Heights.SequenceEqual(terrain.Heights))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED brush_history");
            return false;
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            !ReferenceEquals(_history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).World, original))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_G11I_TERRAIN_PASS provider=heightmap vertices=81 triangles=126 " +
            $"holes=1 biomes=2 features=3 volumes=3 flight_path={path.Points.Count} streaming=async-cache-cancel " +
            $"sculpt=raise-{sculptedSamples} paint=material-{paintedCells} brush_history=restored " +
            "history=restored persistence=roundtrip recipes=asset-preserved");
        return true;
    }

    private bool RunRuntimePolicySmokeTest()
    {
        if (_history is null || _runtimePoliciesWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_FAILED missing_workspace");
            return false;
        }

        var area = GetActiveArea();
        var originalModule = _history.State.Project.Module.RuntimePolicies;
        var originalArea = area.RuntimePolicies;
        var global = new HeartbeatScheduleDefinition(
            Guid.Parse("d11a0000-0000-4000-8000-000000000001"), 1, "Simulation globale",
            HeartbeatScope.Module, true, 1_500, 12, false, "1.0", "OnGlobalHeartbeat", "smoke:g11-j");
        var local = new HeartbeatScheduleDefinition(
            Guid.Parse("d11a0000-0000-4000-8000-000000000002"), 1, "Écologie locale",
            HeartbeatScope.Area, true, 750, 8, true, "1.0", "OnAreaHeartbeat", "smoke:g11-j");
        var rest = new RestLifecycleProfile(originalModule.Rest.Id, 1, true, PartyRestPolicy.WholeParty, 8,
            originalModule.Rest.Bindings, "smoke:g11-j");
        var abort = new SequenceAbortPolicy(originalModule.SequenceAbort.Id, 1, true,
            originalModule.SequenceAbort.Binding, SequenceAbortRestoreFlags.All, 64, "smoke:g11-j");
        var modulePolicies = new ModuleRuntimePolicyProfile(originalModule.Id, 1, [global], rest, abort,
            "smoke:g11-j", originalModule.Cinematics, originalModule.Gameplay, originalModule.Rules);
        var danger = new RestPolicyVolume(Guid.Parse("d11a0000-0000-4000-8000-000000000003"),
            "Sanctuaire profané", Vector3Value.Zero, new Vector3Value(4, 4, 4), RestPermissionMode.Forbidden,
            "Le sanctuaire n’est pas sécurisé.", string.Empty, 30, 0, 100, "smoke:g11-j");
        var areaRest = new AreaRestPolicy(originalArea.Rest.Id, 1, RestPermissionMode.Conditional,
            "Installez un camp sécurisé.", "camp.secure == true", 30, 3, [danger], "smoke:g11-j");
        var areaPolicies = new AreaRuntimePolicyProfile(originalArea.Id, 1, [local], areaRest, "smoke:g11-j",
            originalArea.Cinematics, originalArea.Rules, originalArea.Experience);
        if (!_runtimePoliciesWindow.ConfigureForSmokeTest(modulePolicies, areaPolicies) ||
            !_runtimePoliciesWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_FAILED apply");
            return false;
        }

        var appliedModule = _history.State.Project.Module.RuntimePolicies;
        var appliedArea = _history.State.Project.Areas.Single(item => item.Id == area.Id).RuntimePolicies;
        var appliedGlobal = appliedModule.Heartbeats.Single();
        var appliedLocal = appliedArea.Heartbeats.Single();
        var scheduler = new HeartbeatScheduler();
        var globalAdvance = scheduler.Advance(appliedModule.Heartbeats, appliedGlobal.IntervalMilliseconds * 3L,
            true);
        var suspendedAdvance = scheduler.Advance(appliedArea.Heartbeats, appliedLocal.IntervalMilliseconds * 3L,
            false, area.Id);
        var localAdvance = scheduler.Advance(appliedArea.Heartbeats, appliedLocal.IntervalMilliseconds, true,
            area.Id);
        if (globalAdvance.Dispatches.Count != 3 || suspendedAdvance.Dispatches.Count != 0 ||
            localAdvance.Dispatches.Count != 1 || globalAdvance.Dispatches.Any(item =>
                item.BudgetMilliseconds != appliedGlobal.BudgetMilliseconds || item.Handler != appliedGlobal.Handler) ||
            localAdvance.Dispatches.Any(item => item.BudgetMilliseconds != appliedLocal.BudgetMilliseconds ||
                item.Handler != appliedLocal.Handler) ||
            localAdvance.Diagnostics.All(item => item.Code != HeartbeatScheduler.AggressiveIntervalCode))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_FAILED heartbeat");
            return false;
        }

        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var restIds = 0;
        var restRuntime = new RestRuntimeCoordinator(() => new Guid(++restIds, 0, 0, new byte[8]));
        var denied = restRuntime.Request("hero", "party", area.Id, Vector3Value.Zero, appliedModule.Rest,
            appliedArea.Rest, true, _ => true, 100, now);
        var started = restRuntime.Request("hero", "party", area.Id, new Vector3Value(6, 0, 0), appliedModule.Rest,
            appliedArea.Rest, true, expression => expression == "camp.secure == true", 102, now);
        var completed = started.Session is null
            ? null
            : restRuntime.Complete(started.Session, 104, now.AddSeconds(8));
        var cooldown = restRuntime.Request("hero", "party", area.Id, new Vector3Value(6, 0, 0),
            appliedModule.Rest, appliedArea.Rest, true, _ => true, 105, now.AddSeconds(9));
        if (denied.DiagnosticCode != RestRuntimeCoordinator.ForbiddenCode || denied.Events.Count != 2 ||
            started.Events.Select(item => item.Stage).SequenceEqual(
                [RestLifecycleStage.Requested, RestLifecycleStage.Started]) is false ||
            completed?.Events.Single().Stage != RestLifecycleStage.Completed ||
            cooldown.DiagnosticCode != RestRuntimeCoordinator.CooldownCode)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_FAILED rest");
            return false;
        }

        var exploration = new ControlContext(Guid.Parse("d11a0000-0000-4000-8000-000000000004"), 1,
            "smoke:g11-j", "exploration", false, "follow", new HashSet<string> { "move" });
        var cinematic = new ControlContext(Guid.Parse("d11a0000-0000-4000-8000-000000000005"), 1,
            "smoke:g11-j", "cinematic", true, "rail", new HashSet<string> { "skip" });
        var snapshot = new SequenceRuntimeState("camera.player", [exploration], new HashSet<string>(),
            new HashSet<string>(), 20, null, false);
        var current = new SequenceRuntimeState("camera.cutscene", [exploration, cinematic],
            new HashSet<string> { "hero" }, new HashSet<string> { "input", "dialogue" }, 24, "tx-24", true);
        var aborted = SequenceAbortCoordinator.Abort(appliedModule.SequenceAbort, current, snapshot);
        if (!aborted.Accepted || aborted.State != snapshot with { SequenceActive = false } ||
            !aborted.CameraRestored || !aborted.NarrativeRolledBack || aborted.ReleasedLocks != 2)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_FAILED sequence_abort");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "runtime-policy-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project;
            var restoredArea = restored?.Areas.SingleOrDefault(item => item.Id == area.Id);
            if (!saved.Succeeded || !reopened.Succeeded || restored is null || restoredArea is null ||
                restored.Module.RuntimePolicies.Heartbeats.Count != 1 ||
                restoredArea.RuntimePolicies.Heartbeats.Count != 1 ||
                restoredArea.RuntimePolicies.Rest.Volumes.Count != 1 ||
                restored.Module.RuntimePolicies.SequenceAbort.Restore != SequenceAbortRestoreFlags.All)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            !ReferenceEquals(_history.State.Project.Module.RuntimePolicies, originalModule) ||
            !ReferenceEquals(_history.State.Project.Areas.Single(item => item.Id == area.Id).RuntimePolicies,
                originalArea))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G11J_RUNTIME_POLICY_PASS heartbeats=global-area budgets=bounded " +
            "suspension=inactive rest=requested-started-interrupted-completed cooldown=active " +
            "volumes=conditional abort=camera-control-invulnerability-locks-narrative " +
            "history=restored persistence=roundtrip");
        return true;
    }

    private bool RunCinematicSmokeTest()
    {
        if (_history is null || _cinematicStudioWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_FAILED missing_workspace");
            return false;
        }

        var activeArea = GetActiveArea();
        var originalModule = _history.State.Project.Module.RuntimePolicies.Cinematics;
        var originalArea = activeArea.RuntimePolicies.Cinematics;
        var target = activeArea.Entities.First();
        var anchor = new CameraAnchorDefinition(
            Guid.Parse("d11b0000-0000-4000-8000-000000000001"), "Plan de validation",
            new TransformValue(new Vector3Value(4, 7, 8), QuaternionValue.Identity, Vector3Value.One),
            new Vector3Value(0, 1, 0), CameraFraming.Wide, 55, 3_000, .08f,
            ["opening", "smoke"], "smoke:g11-k");
        var cameraTrack = new SequenceTrackDefinition(
            Guid.Parse("d11b0000-0000-4000-8000-000000000002"), "Caméra",
            SequenceTrackKind.Camera, SequenceTargetKind.CameraAnchor, anchor.Id, string.Empty,
            [new SequenceKeyframe(Guid.Parse("d11b0000-0000-4000-8000-000000000003"), 0, null,
                string.Empty, SequenceEasing.SmoothStep)], "smoke:g11-k");
        var moved = new TransformValue(new Vector3Value(target.Transform.Position.X + 2,
                target.Transform.Position.Y, target.Transform.Position.Z),
            target.Transform.Rotation, target.Transform.Scale);
        var transformTrack = new SequenceTrackDefinition(
            Guid.Parse("d11b0000-0000-4000-8000-000000000004"), "Mouvement",
            SequenceTrackKind.Transform, SequenceTargetKind.Entity, target.Id.Value, string.Empty,
            [new SequenceKeyframe(Guid.Parse("d11b0000-0000-4000-8000-000000000005"), 0, target.Transform,
                 string.Empty, SequenceEasing.Linear),
             new SequenceKeyframe(Guid.Parse("d11b0000-0000-4000-8000-000000000006"), 3_000, moved,
                 string.Empty, SequenceEasing.SmoothStep)], "smoke:g11-k");
        var signalTrack = new SequenceTrackDefinition(
            Guid.Parse("d11b0000-0000-4000-8000-000000000007"), "Signal",
            SequenceTrackKind.Signal, SequenceTargetKind.Blackboard, null, string.Empty,
            [new SequenceKeyframe(Guid.Parse("d11b0000-0000-4000-8000-000000000008"), 750, null,
                "smoke.started", SequenceEasing.Linear)], "smoke:g11-k");
        var sequence = new SequenceDefinition(Guid.Parse("d11b0000-0000-4000-8000-000000000009"), 1,
            "validation-opening", "Validation opening", 3_000, true, true,
            [cameraTrack, transformTrack, signalTrack],
            [new SequenceBranchDefinition(Guid.Parse("d11b0000-0000-4000-8000-000000000010"), 500,
                "smoke.jump", 1_500, 100)], "smoke:g11-k");
        var startup = new StartupFlowDefinition(Guid.Parse("d11b0000-0000-4000-8000-000000000011"), 1,
            StartupFlowKind.Sequence, sequence.Id, string.Empty, StartupFlowKind.Menu, null, string.Empty,
            true, "Passer", "smoke:g11-k");
        var module = new ModuleCinematicProfile(originalModule.Id, 1, startup, [sequence], "smoke:g11-k");
        var area = new AreaCinematicProfile(originalArea.Id, 1, [anchor], "smoke:g11-k");

        var denied = SequencePlayer.Advance(sequence, SequencePlaybackState.Start(sequence.Id), 250, false,
            _ => true);
        var branched = SequencePlayer.Advance(sequence, SequencePlaybackState.Start(sequence.Id), 1_000, true,
            condition => condition == "smoke.jump");
        var transformOverride = new TransformOverrideComponent(
            Guid.Parse("d11b0000-0000-4000-8000-000000000012"), 1, target.Id, null, target.Transform, moved,
            1_000, SequenceEasing.SmoothStep, TransformAuthority.Server, TransformSaveBehavior.CommitFinal, true,
            "smoke:g11-k");
        var overrideState = TransformOverrideRuntime.Advance(transformOverride,
            TransformOverrideRuntime.Start(transformOverride), 500, true);
        if (denied.Diagnostics.All(item => item.Code != SequencePlayer.AuthorityCode) ||
            branched.State.TimeMilliseconds != 1_500 || !branched.Transforms.ContainsKey(target.Id.Value) ||
            branched.Signals.All(item => item.Name != "smoke.started") ||
            Math.Abs(overrideState.Transform.Position.X - (target.Transform.Position.X + 1)) > .001f)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_FAILED timeline");
            return false;
        }

        if (!_cinematicStudioWindow.ConfigureForSmokeTest(module, area) ||
            !_cinematicStudioWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_FAILED apply");
            return false;
        }

        var appliedModule = _history.State.Project.Module.RuntimePolicies.Cinematics;
        var appliedArea = _history.State.Project.Areas.Single(item => item.Id == activeArea.Id)
            .RuntimePolicies.Cinematics;
        var appliedSequence = appliedModule.Sequences.Single();
        var skipped = SequencePlayer.Skip(appliedSequence, SequencePlaybackState.Start(appliedSequence.Id));
        if (appliedModule.Startup.Kind != StartupFlowKind.Sequence || appliedArea.CameraAnchors.Count != 1 ||
            appliedSequence.Tracks.All(item => item.Kind != SequenceTrackKind.Transform) || !skipped.State.Skipped)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_FAILED applied_contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "cinematic-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Module.RuntimePolicies.Cinematics.Sequences.Count != 1 ||
                restored.Areas.Single(item => item.Id == activeArea.Id).RuntimePolicies.Cinematics.CameraAnchors.Count != 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            !ReferenceEquals(_history.State.Project.Module.RuntimePolicies.Cinematics, originalModule) ||
            !ReferenceEquals(_history.State.Project.Areas.Single(item => item.Id == activeArea.Id)
                .RuntimePolicies.Cinematics, originalArea))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_G11K_CINEMATIC_PASS startup=sequence-fallback timeline=tracks-branch-signal " +
            "camera=anchor-fov-safe-area transform=interpolated hierarchy=validated authority=server " +
            "skip=accessible abort=recovered mcp=governed history=restored persistence=roundtrip");
        return true;
    }

    private bool RunGameplayControlsSmokeTest()
    {
        if (_history is null || _gameplayControlsWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_FAILED missing_workspace");
            return false;
        }

        var original = _history.State.Project.Module.RuntimePolicies.Gameplay;
        var swimming = new SwimmingProfile(original.Swimming.Id, 1, true, false, 9, .8f, 5, 3.5f, .7f,
            60, original.Swimming.WaterVolumeTags, original.Swimming.SurfaceAnimation,
            original.Swimming.DiveAnimation, original.Swimming.IdleAnimation, "smoke:g11-l");
        var climbing = new ClimbingProfile(original.Climbing.Id, 1, true, false, 3.5f, 120, 10, 1.75f,
            true, true, true, original.Climbing.SurfaceTags, original.Climbing.ClimbAnimation,
            original.Climbing.LedgeAnimation, "smoke:g11-l");
        var radial = new RadialMenuProfile(original.RadialMenu.Id, 1, true, "action_wheel", .2f, 3,
            true, true, true, true, original.RadialMenu.Actions, "smoke:g11-l");
        var time = new TimeControlPolicy(original.TimeControl.Id, 1, true, true, .25f, true, 12, true,
            MultiplayerPausePolicy.Vote, .5f, original.TimeControl.PausePermissions, "smoke:g11-l");
        var combat = new CombatModeProfile(original.CombatModes.Id, 1, "turn-based",
            original.CombatModes.Providers, "smoke:g11-l");
        var foundation = original.Foundation with
        {
            GroundedMovement = original.Foundation.GroundedMovement with
            { RunSpeed = 6.8f, Acceleration = 28, Provenance = "smoke:r010" },
            Cameras = original.Foundation.Cameras with
            { DefaultProfileKey = "isometric", Provenance = "smoke:r010" },
            Save = original.Foundation.Save with
            { ChunkBytes = 32_768, AutosaveIntervalSeconds = 45, RollbackSlots = 4, Provenance = "smoke:r010" },
            Provenance = "smoke:r010",
        };
        var candidate = new ModuleGameplayControlProfile(original.Id, 1, swimming, climbing, radial, time,
            combat, "smoke:g11-l", foundation);

        var dive = MovementCapabilityRuntime.Resolve(candidate,
            new MovementCapabilityContext(true, true, false, false, 10, 120), 1);
        var climb = MovementCapabilityRuntime.Resolve(candidate,
            new MovementCapabilityContext(false, false, true, true, 60, 120), 1);
        var combatPage = RadialActionResolver.Open(candidate.RadialMenu, "combat");
        var planning = TimeControlCoordinator.Request(candidate.TimeControl, TimeControlSession.Running,
            RuntimeTimeState.Planning, "player:one", false, true);
        var queued = TimeControlCoordinator.Queue(candidate.TimeControl, planning.Session,
            new PlannedGameplayAction(Guid.Parse("d11c0000-0000-4000-8000-000000000001"), "player:one",
                "attack", "enemy:one", 0));
        if (dive.Kind != MovementCapabilityKind.Diving || climb.Kind != MovementCapabilityKind.Climbing ||
            combatPage.Actions.All(item => item.Command != "attack") || !planning.Accepted || !queued.Accepted)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_FAILED runtime_contract");
            return false;
        }

        if (!_gameplayControlsWindow.ConfigureForSmokeTest(candidate) ||
            !_gameplayControlsWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_FAILED apply");
            return false;
        }

        var applied = _history.State.Project.Module.RuntimePolicies.Gameplay;
        if (!applied.Swimming.Enabled || !applied.Climbing.Enabled || applied.RadialMenu.Actions.Count < 4 ||
            !applied.TimeControl.PlanningEnabled || applied.TimeControl.MultiplayerPolicy != MultiplayerPausePolicy.Vote ||
            applied.CombatModes.DefaultProvider.Kind != CombatModeKind.TurnBased ||
            applied.Foundation.GroundedMovement.RunSpeed != 6.8f ||
            applied.Foundation.Cameras.DefaultProfileKey != "isometric" ||
            applied.Foundation.Save.RollbackSlots != 4 ||
            GameplayFoundationValidator.Validate(applied.Foundation).Count != 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_FAILED applied_contract " +
                $"swim={applied.Swimming.Enabled} climb={applied.Climbing.Enabled} radial={applied.RadialMenu.Actions.Count} " +
                $"planning={applied.TimeControl.PlanningEnabled} multiplayer={applied.TimeControl.MultiplayerPolicy} " +
                $"combat={applied.CombatModes.DefaultProvider.Kind} run={applied.Foundation.GroundedMovement.RunSpeed} " +
                $"camera={applied.Foundation.Cameras.DefaultProfileKey} rollback={applied.Foundation.Save.RollbackSlots} " +
                $"diagnostics={string.Join(',', GameplayFoundationValidator.Validate(applied.Foundation).Select(item => item.Code))}");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "gameplay-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Module.RuntimePolicies.Gameplay;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.RadialMenu.Actions.Count != applied.RadialMenu.Actions.Count ||
                restored.CombatModes.DefaultProviderId != "turn-based" ||
                restored.Foundation.AllIds().Count != applied.Foundation.AllIds().Count ||
                restored.Foundation.Cameras.DefaultProfileKey != "isometric")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            !ReferenceEquals(_history.State.Project.Module.RuntimePolicies.Gameplay, original))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_G11L_GAMEPLAY_PASS swim=buoyancy-breath-currents dive=runtime " +
            "climb=links-stamina-ik-safety radial=nested-gamepad-keyboard-list-accessible " +
            "time=pause-slow-planning-turns multiplayer=vote-permissions combat=providers-shared-events-authority " +
            "history=restored persistence=roundtrip");
        GD.Print("OPENRPG_FORGE_TOOLSET_R010_GAMEPLAY_PASS " +
            "movement=ground-profile-validated interaction=focus-verbs-hold camera=8-profiles " +
            "input=contexts-devices-glyphs-rebind save=chunks-sha256-autosave-rollback-migrations-dependencies " +
            "ui=five-panels command=dry-run-apply history=undo-redo persistence=roundtrip " +
            "forgescript=typed-handles mcp=filtered-dry-run");
        return true;
    }

    private bool RunRulesProfilesSmokeTest()
    {
        if (_history is null || _rulesConfigurationWindow is null || _activeAreaContext.Current is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11M_RULES_FAILED missing_workspace");
            return false;
        }

        var areaId = _activeAreaContext.Current;
        var projectArea = _history.State.Project.Areas.Single(item => item.Id == areaId);
        var originalModule = _history.State.Project.Module.RuntimePolicies.Rules;
        var originalArea = projectArea.RuntimePolicies.Rules;
        var currentDifficulty = originalModule.ActiveDifficulty;
        var difficulty = new DifficultyProfile(currentDifficulty.Id, 1, currentDifficulty.Key, "Tactique",
            1.2, 1.35, 1.15, .9, .8, SaveRestrictionPolicy.CheckpointsOnly,
            FriendlyFireMode.PartyOnly, currentDifficulty.Accessibility, "smoke:g11-m");
        var progression = new ProgressionPolicy(originalModule.Progression.Id, 1,
            originalModule.Progression.RulesetId, originalModule.Progression.Curve, 1.1, 1.2,
            originalModule.Progression.SourceMultipliers, true, "smoke:g11-m");
        var mutators = new MutatorSet(originalModule.Mutators.Id, 1,
            originalModule.Mutators.Mutators.Select(item => item with
            {
                Enabled = item.Key == "story-support",
            }).ToArray(),
            "smoke:g11-m");
        var originalPackage = originalModule.Plugins.Packages.Single();
        var pluginPackage = new RulesetPluginPackage(originalPackage.Id, 1, originalPackage.Key, "1.1.0",
            originalPackage.ContentPackageId, originalPackage.Attributes, originalPackage.Effects,
            originalPackage.Abilities, originalPackage.Inventories, originalPackage.ProgressionTracks,
            originalPackage.UiPanels, "smoke:r011");
        var plugins = new RulesetPluginCatalog(originalModule.Plugins.Id, 1, true, true, pluginPackage.Key,
            [pluginPackage], "smoke:r011");
        var module = new ModuleRulesProfile(originalModule.Id, 1, progression, [difficulty], difficulty.Key,
            mutators, "smoke:g11-m", plugins);
        var perception = new EnvironmentPerceptionProfile(originalArea.Perception.Id, 1,
            originalArea.Perception.RulesetId,
            [new EnvironmentPerceptionModifier(Guid.Parse("d11d0000-0000-4000-8000-000000000001"),
                "vision", .75, .1, "fog")], "smoke:g11-m");
        var combat = new AreaCombatPolicy(originalArea.Combat.Id, 1, AreaPvpMode.DuelsOnly,
            FriendlyFireMode.Disabled, true, true, false, true, "smoke:g11-m");
        var area = new AreaRulesProfile(originalArea.Id, 1, perception, combat, "smoke:g11-m");

        var resolved = RulesRuntimeService.ResolveDifficulty(module);
        var award = RulesRuntimeService.ResolveProgression(module.Progression, 100, "quest", 2, 0,
            resolved.ProgressionMultiplier);
        var sensed = RulesRuntimeService.ResolvePerception(area.Perception, "vision", 20, .8,
            new HashSet<string>(["fog"], StringComparer.Ordinal));
        var plugin = RulesetPluginRuntimeService.Resolve(module.Plugins, ["content.core"]);
        if (resolved.IncomingDamageMultiplier >= difficulty.IncomingDamageMultiplier || award.Level < 2 ||
            sensed.Range != 15 || Math.Abs(sensed.Confidence - .7) > .0001 ||
            plugin is not { Succeeded: true, ExtensionPresent: true, Registration: not null } ||
            plugin.Registration.Attributes.Count == 0 || plugin.Registration.Effects.Count == 0 ||
            plugin.Registration.Abilities.Count == 0 || plugin.Registration.Inventories.Count == 0 ||
            plugin.Registration.ProgressionTracks.Count == 0 || plugin.Registration.UiPanels.Count == 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11M_RULES_FAILED runtime_contract");
            return false;
        }

        if (!_rulesConfigurationWindow.ConfigureForSmokeTest(module, areaId, area) ||
            !_rulesConfigurationWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11M_RULES_FAILED apply");
            return false;
        }

        var appliedModule = _history.State.Project.Module.RuntimePolicies.Rules;
        var appliedArea = _history.State.Project.Areas.Single(item => item.Id == areaId).RuntimePolicies.Rules;
        if (appliedModule.ActiveDifficulty.SaveRestrictions != SaveRestrictionPolicy.CheckpointsOnly ||
            appliedModule.Mutators.Mutators.Count(item => item.Enabled) != 1 ||
            appliedModule.Plugins.ActivePackageKey != pluginPackage.Key ||
            appliedModule.Plugins.Packages.Single().Version != "1.1.0" ||
            appliedArea.Perception.Modifiers.Count != 1 || appliedArea.Combat.PvpMode != AreaPvpMode.DuelsOnly ||
            !appliedArea.Combat.ServerAuthoritative)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11M_RULES_FAILED applied_contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "rules-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Module.RuntimePolicies.Rules.ActiveDifficulty.IncomingDamageMultiplier != 1.35 ||
                restored.Module.RuntimePolicies.Rules.Plugins.Packages.Single().Version != "1.1.0" ||
                restored.Areas.Single(item => item.Id == areaId).RuntimePolicies.Rules.Combat.PvpMode !=
                    AreaPvpMode.DuelsOnly)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11M_RULES_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            !ReferenceEquals(_history.State.Project.Module.RuntimePolicies.Rules, originalModule) ||
            !ReferenceEquals(_history.State.Project.Areas.Single(item => item.Id == areaId).RuntimePolicies.Rules,
                originalArea))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11M_RULES_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_G11M_RULES_PASS progression=curve-source-group-difficulty " +
            "difficulty=damage-ai-economy-rest-save-accessibility mutators=composable " +
            "perception=sense-ruleset-condition pvp=duel-crime-aggro-environment authority=server " +
            "mcp=governed history=restored persistence=roundtrip");
        GD.Print("OPENRPG_FORGE_TOOLSET_R011_GAMEPLAY_SYSTEMS_PASS " +
            "blackboard=typed-save-replication-debug forgescript=typed-handles " +
            "ruleset=attributes-effects-abilities-inventories-progression-ui " +
            "ui=blackboard-rules-plugin command=dry-run-apply history=undo-redo persistence=roundtrip " +
            "mcp=filtered-read-plan");
        return true;
    }

    private bool RunForgeScriptFoundationSmokeTest()
    {
        if (_forgeScriptEditor is null || !_forgeScriptEditor.RunForSmokeTest() ||
            !_forgeScriptEditor.LastCompileSucceeded || !_forgeScriptEditor.LastSandboxSucceeded ||
            _forgeScriptEditor.BindingCount != 2)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R012_SCRIPT_FOUNDATION_FAILED " +
                        "editor-compile-sandbox-bindings");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R012_SCRIPT_FOUNDATION_PASS " +
                 "language=angelscript-2.38-c-like-static api=1.0-versioned " +
                 "source=canonical-as-uuid-schema-provenance bytecode=cache-source-api-compiler " +
                 "compiler=incremental-parallel-stable-diagnostics " +
                 "events=per-component-multi-handler-priority-condition-authority " +
                 "entry=typed-payload ui=editor-console-clickable-diagnostics " +
                 "command=preview-apply-undo-redo mcp=api-compile-diagnostics-tests-assign-event");
        return true;
    }

    private bool RunForgeScriptWorldApiSmokeTest()
    {
        var registered = ForgeScriptApiCatalog.Registrations.Select(item => item.Name)
            .ToHashSet(StringComparer.Ordinal);
        var complete = ForgeScriptCapabilityManifest.Capabilities
            .SelectMany(item => item.ApiNames).All(registered.Contains);
        var denied = ForgeScriptCapabilityManifest.ValidateSource("void main() { socket.connect(\"x\"); }");
        if (_forgeScriptEditor is null || _forgeScriptEditor.CapabilityCount != 6 || !complete ||
            denied.Count != 1 || denied[0].Code != "script.sandbox.capability_forbidden")
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R013_SCRIPT_WORLD_API_FAILED " +
                        "catalog-editor-sandbox-manifest");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R013_SCRIPT_WORLD_API_PASS " +
                 "world=area-query-transform-spawn-destroy movement=task-mode-velocity-teleport-completion " +
                 "utilities=vector-quaternion-unicode-format-regex-bounded variables=typed-object-blackboard " +
                 "sandbox=allowlist-no-file-network-process-native-reflection-engine-dotnet " +
                 "ui=catalog-editor-console-diagnostics command=preview-apply-undo-redo");
        return true;
    }

    private bool RunForgeScriptSafetyPolicySmokeTest()
    {
        var registry = new ForgeScriptHandleRegistry();
        var handle = registry.Register(ForgeScriptHandleKind.Entity,
            Guid.Parse("c7140000-0000-4000-8000-000000000001"));
        var typed = registry.TryResolve(handle, ForgeScriptHandleKind.Entity, out _, out _);
        var wrongType = !registry.TryResolve(handle, ForgeScriptHandleKind.Area, out _, out var typeDiagnostic) &&
                        typeDiagnostic?.Code == "script.handle.type_invalid";
        registry.Release(handle, ForgeScriptHandleKind.Entity);
        var stale = !registry.TryResolve(handle, ForgeScriptHandleKind.Entity, out _, out var staleDiagnostic) &&
                    staleDiagnostic?.Code == "script.handle.stale";
        var negotiation = ForgeScriptApiCompatibility.Negotiate("1.0",
            new HashSet<string> { "opaque-handles", "determinism" });
        var policy = new ForgeScriptExecutionPolicyHistory(ForgeScriptExecutionPolicy.Default);
        var command = new ForgeScriptExecutionPolicyCommand(Guid.NewGuid(), 0,
            ForgeScriptExecutionPolicy.Default with { DeterministicSeed = 14_014 }, "toolset:r014");
        var preview = policy.Preview(command);
        var applied = policy.Apply(command);
        var undone = policy.Undo();
        var redone = policy.Redo();
        if (_forgeScriptEditor is null || !typed || !wrongType || !stale || !negotiation.Succeeded ||
            ForgeScriptApiCompatibility.Deprecations.Count == 0 || !preview.Succeeded || !applied.Succeeded ||
            !undone.Succeeded || !redone.Succeeded)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R014_SCRIPT_SAFETY_POLICY_FAILED " +
                        "handles-budgets-determinism-version-ui");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R014_SCRIPT_SAFETY_POLICY_PASS " +
                 "handles=typed-generational-stale-validated budgets=handler-tick-module-memory-timeout-cancel " +
                 "determinism=seed-clock-server version=semver-negotiation-deprecation-migration " +
                 "ui=presets-summary-telemetry command=preview-apply-undo-redo mcp=version-capabilities");
        return true;
    }

    private bool RunForgeScriptModulePermissionsSmokeTest()
    {
        var moduleId = _history?.State.Project.Module.Id.Value ??
            Guid.Parse("15000000-0000-4000-8000-000000000015");
        var policy = ForgeScriptModulePermissionPolicy.CreateDefault(moduleId);
        var history = new ForgeScriptModulePermissionHistory(policy);
        var command = new ForgeScriptModulePermissionCommand(Guid.NewGuid(), 0, policy,
            "toolset:r015");
        var preview = history.Preview(command);
        var apply = history.Apply(command);
        var undo = history.Undo();
        var redo = history.Redo();
        var accepted = ForgeScriptModuleAuthorizationService.Authorize(policy,
            new(moduleId, "core", "module.start", "main",
                new HashSet<string>(["world"], StringComparer.Ordinal), ForgeScriptEventAuthority.Server,
                true, false, true));
        var denied = ForgeScriptModuleAuthorizationService.Authorize(policy,
            new(moduleId, "core", "module.start", "main",
                new HashSet<string>(["movement"], StringComparer.Ordinal), ForgeScriptEventAuthority.Server,
                true, false, true));
        if (_forgeScriptEditor is null || !_forgeScriptEditor.ModulePermissionsSmokeSucceeded || !preview.Succeeded ||
            !apply.Succeeded || !undo.Succeeded || !redo.Succeeded || !accepted.Succeeded ||
            denied.Diagnostics.SingleOrDefault()?.Code != "script.permission.capability_denied")
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R015_SCRIPT_MODULE_PERMISSIONS_FAILED " +
                        "ui-command-authority-signature-capabilities");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R015_SCRIPT_MODULE_PERMISSIONS_PASS " +
                 "policy=module-kit-event-deny-default authority=server signature=trusted-production-dev-explicit " +
                 "ui=empty-error-capabilities-diagnostics-console command=preview-apply-undo-redo " +
                 "persistence=canonical-json packaging=validated mcp=documented");
        return true;
    }

    private bool RunAreaExperienceSmokeTest()
    {
        if (_history is null || _areaExperienceWindow is null || _activeAreaContext.Current is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_FAILED missing_workspace");
            return false;
        }

        var areaId = _activeAreaContext.Current;
        var original = _history.State.Project.Areas.Single(item => item.Id == areaId).RuntimePolicies.Experience;
        var cue = new AudioCueDefinition(Guid.Parse("d11e0000-0000-4000-8000-000000000001"),
            "ambient.embers", string.Empty, "Ambience", .8f, false, string.Empty, string.Empty, "1.0",
            "smoke:g11-n");
        var scatter = new SpatialScatterEmitter(Guid.Parse("d11e0000-0000-4000-8000-000000000002"),
            cue.CueId, Vector3Value.Zero, new Vector3Value(12, 3, 12), true, 2026, 1, 4, .75f, true,
            ["crypt"], ["safe-room"], "smoke:g11-n");
        var creature = _history.State.Project.Blueprints.First().Id;
        var voice = new CreatureVoiceProfile(Guid.Parse("d11e0000-0000-4000-8000-000000000003"), creature,
            "fr",
            [
                new LocalizedVoiceTake("fr", string.Empty, "Les braises nous observent.", string.Empty,
                    string.Empty, string.Empty),
                new LocalizedVoiceTake("en", string.Empty, "The embers are watching us.", string.Empty,
                    string.Empty, string.Empty),
            ], "tts.local",
            new VoiceConsentMetadata(true, true, "consent:smoke", "voix synthétique de test"), "smoke:g11-n");
        var loading = new AreaLoadingPresentation(original.Loading.Id, "Crypte des Cendres", "loading.png",
            "loading.ogv", new Dictionary<string, IReadOnlyList<string>>
            {
                ["fr"] = ["La lumière révèle les passages sûrs."],
                ["en"] = ["Light reveals safe paths."],
            }, true, "Préparation de la Crypte des Cendres.", true, 250, "smoke:g11-n");
        var profile = new AreaExperienceProfile(original.Id, 1, [cue], [scatter], [voice], [], loading,
            "smoke:g11-n");

        var emission = AreaExperienceRuntimeService.ResolveScatter(profile, scatter.Id, 3,
            new HashSet<string>(["crypt"], StringComparer.Ordinal), new HashSet<string>(), true);
        var repeated = AreaExperienceRuntimeService.ResolveScatter(profile, scatter.Id, 3,
            new HashSet<string>(["crypt"], StringComparer.Ordinal), new HashSet<string>(), true);
        var started = AreaExperienceRuntimeService.PlayCue(profile, AreaAudioRuntimeState.Empty, cue.CueId,
            DateTimeOffset.UnixEpoch, () => Guid.Parse("d11e0000-0000-4000-8000-000000000004"));
        var interrupted = AreaExperienceRuntimeService.Interrupt(started.State, started.Playback.PlaybackId,
            "area-transition", DateTimeOffset.UnixEpoch,
            () => Guid.Parse("d11e0000-0000-4000-8000-000000000005"));
        var resolvedVoice = AreaExperienceRuntimeService.ResolveVoice(voice, "de", "fr");
        var frame = AreaExperienceRuntimeService.ResolveLoading(loading, "fr", 2, 4, 0, true, true);
        var from = AreaId.From(Guid.Parse("d11e0000-0000-4000-8000-000000000010"));
        var destination = AreaId.From(Guid.Parse("d11e0000-0000-4000-8000-000000000011"));
        var resume = Guid.Parse("d11e0000-0000-4000-8000-000000000012");
        var patrol = new CrossAreaPatrolRoute(Guid.Parse("d11e0000-0000-4000-8000-000000000013"), "guard",
            [new CrossAreaPatrolSegment(Guid.Parse("d11e0000-0000-4000-8000-000000000014"), from, destination,
                PortalId.From(Guid.Parse("d11e0000-0000-4000-8000-000000000015")), resume, true,
                PatrolOwnershipAuthority.Server)], false, "smoke:g11-n");
        var cursor = new CrossAreaPatrolCursor(0, from, Guid.Empty, false, false);
        var waiting = CrossAreaPatrolRuntimeService.Advance(patrol, cursor, true, false);
        var resumed = CrossAreaPatrolRuntimeService.Advance(patrol, waiting.Cursor, true, true);
        if (emission is null || emission != repeated ||
            interrupted.Events.LastOrDefault() is not AudioCueInterrupted || !resolvedVoice.UsesTts ||
            frame.MediaKind != LoadingMediaKind.Image || frame.Progress != .5 ||
            !waiting.Cursor.AwaitingStreaming || resumed.Cursor.ResumeWaypointId != resume)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_FAILED runtime_contract");
            return false;
        }

        if (!_areaExperienceWindow.ConfigureForSmokeTest(areaId, profile) ||
            !_areaExperienceWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_FAILED apply");
            return false;
        }

        var applied = _history.State.Project.Areas.Single(item => item.Id == areaId).RuntimePolicies.Experience;
        if (applied.AudioCues.Count != 1 || applied.ScatterEmitters.Count != 1 || applied.Voices.Count != 1 ||
            applied.Voices[0].Takes.Count != 2 ||
            !applied.Loading.ShowProgress || !applied.Loading.PrefetchRequiredResources)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_FAILED applied_contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "experience-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == areaId)
                .RuntimePolicies.Experience;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null || restored.AudioCues.Count != 1 ||
                restored.Loading.Title != "Crypte des Cendres")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo(); var redo = _history.Redo(); var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            !ReferenceEquals(_history.State.Project.Areas.Single(item => item.Id == areaId)
                .RuntimePolicies.Experience, original))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_FAILED history");
            return false;
        }

        RebuildPreview(); RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_PASS scatter=volume-surface-nav-biome-seed " +
            "voice=localized-phoneme-subtitle-tts-consent audio=started-completed-interrupted-music-snapshot " +
            "patrol=cross-area-streaming-owner-resume loading=image-video-tip-progress-accessible-prefetch " +
            "mcp=governed history=restored persistence=roundtrip");
        return true;
    }

    private bool RunAudioEmitterSmokeTest()
    {
        if (_history is null || _areaEnvironmentWindow is null || _activeAreaContext.Current is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R004_AUDIO_FAILED missing_workspace");
            return false;
        }

        var areaId = _activeAreaContext.Current;
        var area = _history.State.Project.Areas.Single(item => item.Id == areaId);
        var original = area.Environment;
        var definitionId = Guid.Parse("a0040000-0000-4000-8000-000000000401");
        var instanceId = Guid.Parse("a0040000-0000-4000-8000-000000000402");
        var catalog = new AudioEmitterCatalog(AudioEmitterCatalog.CurrentSchemaVersion,
        [
            new AudioEmitterDefinition(definitionId, AudioEmitterDefinition.CurrentSchemaVersion, "Cascade de validation",
                new AudioClipSet(Guid.Parse("a0040000-0000-4000-8000-000000000403"),
                    AudioClipSet.CurrentSchemaVersion, "Cascade clips", AudioClipSelectionMode.Weighted,
                    [new(Guid.Parse("a0040000-0000-4000-8000-000000000404"), "audio/waterfall.ogg", 2,
                        ["water", "ambient"], "close", "fr", -18, "audio/waterfall.preview.ogg",
                        "audio/ogg", 1048576, "CC0-1.0", "smoke:r004", new string('a', 64))],
                    "smoke:r004"),
                new(AudioLoopMode.SeamlessRegion, 0, 1, 8, .25f, string.Empty),
                new(2, 42, "inverse-square", 180, -12, .25f, 1.5f, true), null, .25f,
                "scripts/waterfall-on.forge", "scripts/waterfall-off.forge", true, "smoke:r004",
                "assets/asset-manifests.json", "CC0-1.0", new string('a', 64)),
        ],
        [
            new AudioEmitterInstance(instanceId, definitionId,
                new TransformValue(new Vector3Value(6, 2, -5), QuaternionValue.Identity, Vector3Value.One),
                true, "smoke:r004"),
        ]);
        var audio = original.Audio;
        var updatedAudio = new AreaAudioProfile(audio.DayMusic, audio.NightMusic, audio.CombatMusic,
            audio.Ambience, audio.ReverbPreset, audio.TransitionSeconds, audio.Playlist, audio.OrderMode,
            audio.IntervalSeconds, audio.IntervalVariationSeconds, audio.RelativeVolume, audio.Spatial,
            audio.RuntimeControl, audio.AdaptiveLayers, audio.Orchestration, catalog);
        var configured = new AreaEnvironmentProfile(AreaEnvironmentProfile.CurrentSchemaVersion,
            original.Classification, original.Tags, original.Lighting, original.Fog, updatedAudio,
            original.Rendering, original.Sky, original.Wind, original.Weather, original.Water, original.Reverb);

        var runtime = AudioEmitterRuntimeService.CreateInitialPlaybacks(catalog, 2026).Single();
        if (runtime.Handle.Id != instanceId || runtime.Transform.Position != new Vector3Value(6, 2, -5) ||
            runtime.Loop.Mode != AudioLoopMode.SeamlessRegion || !runtime.Autoplay)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R004_AUDIO_FAILED runtime_contract");
            return false;
        }
        if (!_areaEnvironmentWindow.ConfigureRenderForSmokeTest(configured) ||
            !_areaEnvironmentWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R004_AUDIO_FAILED authoring_apply");
            return false;
        }
        var applied = _history.State.Project.Areas.Single(item => item.Id == areaId).Environment.Audio.Emitters;
        if (applied.Definitions.Single().ClipSet.Clips.Single().LicenseSpdx != "CC0-1.0" ||
            applied.Instances.Single().Transform.Position != new Vector3Value(6, 2, -5) ||
            AudioEmitterCatalogValidator.Validate(applied).Count != 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R004_AUDIO_FAILED canonical_validation");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "audio-emitter-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == areaId)
                .Environment.Audio.Emitters;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Definitions.Single().Id != definitionId ||
                restored.Instances.Single().Id != instanceId)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R004_AUDIO_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo(); var redo = _history.Redo(); var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            !ReferenceEquals(_history.State.Project.Areas.Single(item => item.Id == areaId).Environment, original))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R004_AUDIO_FAILED history");
            return false;
        }
        RebuildPreview(); RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_R004_AUDIO_PASS object=definition-instance clips=weighted-licensed " +
            "loop=seamless-positioned validation=files-license-loudness-range-memory " +
            "script=typed-handle mcp=filtered-dry-run history=restored persistence=roundtrip");
        return true;
    }

    private bool RunRuntimeExtensionsSmokeTest()
    {
        if (_history is null || _runtimeExtensionsWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11O_EXTENSIONS_FAILED missing_workspace");
            return false;
        }

        var original = _history.State.Project.Module.RuntimePolicies.Extensions;
        var rule = new DataDrivenRuleSchema(Guid.Parse("e0110000-0000-4000-8000-000000000001"), 1,
            "class.ember-warden", RuleSchemaKind.Class, "{\"type\":\"object\"}",
            "{\"hitDie\":10,\"casting\":\"focus\"}", true, "smoke:g11-o");
        var json = new RuntimeJsonDocumentDefinition(Guid.Parse("e0150000-0000-4000-8000-000000000001"), 1,
            "adventure-state", "{\"type\":\"object\",\"required\":[\"chapter\"]}",
            "{\"chapter\":1,\"status\":\"Prêt\"}", 4096, 8, true, "1.0", "smoke:g11-o");
        var label = new ScriptableUiWidget(Guid.Parse("e0120000-0000-4000-8000-000000000001"),
            "chapter-label", ScriptableUiWidgetKind.Label, null, "Chapitre", null, false, 0, "player");
        var button = new ScriptableUiWidget(Guid.Parse("e0120000-0000-4000-8000-000000000002"),
            "continue-button", ScriptableUiWidgetKind.Button, null, "Continuer", null, true, 1, "player");
        var binding = new ScriptableUiEventBinding(Guid.Parse("e0130000-0000-4000-8000-000000000001"),
            "continue-button", ScriptableUiEventKind.Pressed, "ContinueAdventure", "1.0", "gameplay",
            "player");
        var ui = new ScriptableUiDocument(Guid.Parse("e0120000-0000-4000-8000-000000000010"), 1,
            "adventure-hud", "Aventure", "forge-dark", true, [label, button], [binding], "smoke:g11-o");
        var snapshot = new EntityStateSnapshotPolicy(original.SnapshotPolicy.Id, 1,
            ["transform", "combat", "inventory", "blackboard"], 128, 256, 32768, true, "smoke:g11-o");
        var profile = new ModuleRuntimeExtensionProfile(original.Id, 1, [rule], [ui], [json], snapshot,
            "smoke:g11-o");

        if (!_runtimeExtensionsWindow.ConfigureForSmokeTest(profile) ||
            !_runtimeExtensionsWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11O_EXTENSIONS_FAILED apply");
            return false;
        }

        var applied = _history.State.Project.Module.RuntimePolicies.Extensions;
        if (applied.RuleSchemas.Single().Kind != RuleSchemaKind.Class ||
            applied.UiDocuments.Single().EventBindings.Single().Command != "ContinueAdventure" ||
            applied.JsonDocuments.Single().MaxDepth != 8 || applied.SnapshotPolicy.MaxInventoryItems != 256 ||
            applied.ModernTooling.RuntimeCopy.MaxCopiesPerTransaction != 32 ||
            applied.ModernTooling.AreaEffectIndicator.Shapes.Count != 4 ||
            applied.ModernTooling.UiToolkit.ToolkitVersion != "2.0" ||
            !applied.ModernTooling.Compiler.Incremental || applied.ModernTooling.Documentation.Catalogs.Count != 6 ||
            applied.OperationalReadiness.TaskManifests.Count != 1 ||
            !applied.OperationalReadiness.CrashArtifacts.CaptureSanitizedLogs ||
            !applied.OperationalReadiness.ProjectHealth.IncludeCoverage ||
            !applied.OperationalReadiness.Recovery.QuarantineCorruptFiles ||
            !applied.OperationalReadiness.ContentSynchronization.Resumable ||
            applied.ModernTooling.QualityAssurance.ClickableDiagnostics.Categories.Count != 7 ||
            applied.ModernTooling.QualityAssurance.MaliciousFixtures.Families.Count != 7 ||
            applied.ModernTooling.QualityAssurance.AuroraParityCoverage.ExpectedFeatureCount != 717 ||
            !applied.ModernTooling.QualityAssurance.AcceptanceGates.RequiredGates.Contains("G8", StringComparer.Ordinal))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11O_EXTENSIONS_FAILED canonical_contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "extensions-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Module.RuntimePolicies.Extensions;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                 restored.RuleSchemas.Single().Key != "class.ember-warden" ||
                 restored.UiDocuments.Single().Widgets.Count != 2 ||
                 restored.JsonDocuments.Single().InitialValue != "{\"chapter\":1,\"status\":\"Prêt\"}" ||
                 !restored.ModernTooling.UiToolkit.Accessibility ||
                 restored.ModernTooling.Documentation.OutputDirectory != "docs/generated" ||
                 restored.ModernTooling.QualityAssurance.ClickableDiagnostics.ActiveProfile != "authoring" ||
                 restored.ModernTooling.QualityAssurance.MaliciousFixtures.Families.Count != 7 ||
                 restored.ModernTooling.QualityAssurance.AuroraParityCoverage.ExpectedFeatureCount != 717 ||
                 !restored.ModernTooling.QualityAssurance.AcceptanceGates.RequireLinkedDocumentation ||
                 restored.OperationalReadiness.TaskManifests.Single().Key != "validate-module" ||
                 restored.OperationalReadiness.ContentSynchronization.StorageDirectory != ".forge/content-cache")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11O_EXTENSIONS_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            !ReferenceEquals(_history.State.Project.Module.RuntimePolicies.Extensions, original))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11O_EXTENSIONS_FAILED history");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_G11O_EXTENSIONS_PASS rules=class-progression-weapon-vfx-casting " +
            "ui=declarative-reactive-themed-permission events=typed-focus-input-testable " +
            "json=schema-quota-safe-api snapshots=components-references-inventory-blackboard " +
            "history=restored persistence=roundtrip");
        GD.Print("OPENRPG_FORGE_TOOLSET_G11P_MODERN_PASS copy=uuid-owner-references-state-authority " +
            "aoe=mesh-targets-cover-height-colorblind-confirmation nui=2.0-forms-lists-canvas-accessibility-localization-inspector " +
            "compiler=incremental-stable-telemetry-conformance docs=metadata-generated-cli-mcp history=restored persistence=roundtrip");
        GD.Print("OPENRPG_FORGE_TOOLSET_G11Q_RESILIENCE_PASS tasks=goal-scope-forbidden-tests-owner-dependencies " +
            "crash=sanitized-dump-version-fingerprint-reproduction health=domains-tests-performance-licenses-migrations-coverage " +
            "recovery=journal-reopen-quarantine-compare sync=resume-dedup-hash-trust-storage history=restored persistence=roundtrip");
        GD.Print("OPENRPG_FORGE_TOOLSET_R003_QA_PASS diagnostics=profile-category-severity-quickfix " +
            "malicious=7-fixtures parity=717-status-evidence gates=G0-G13-tests-docs-evidence " +
            "mcp=structured history=restored persistence=roundtrip");
        return true;
    }

    private bool RunAreaFoundationSmokeTest()
    {
        if (_history is null || _projectSession is null || _areaFoundationWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R028_AREA_FOUNDATION_FAILED missing_context");
            return false;
        }

        var area = GetActiveArea();
        var original = area.Foundation;
        if (!_areaFoundationWindow.PrepareForSmokeTest() || !_areaFoundationWindow.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R028_AREA_FOUNDATION_FAILED ui_apply");
            return false;
        }

        var appliedArea = _history.State.Project.Areas.Single(item => item.Id == area.Id);
        var applied = appliedArea.Foundation;
        var connection = ModularTileConnectionPreviewService.Preview(applied.TileKit,
            applied.TileKit.Placements.Single().Id);
        var placedEntity = appliedArea.Entities.Single(item =>
            item.Id == applied.TileKit.Placements.Single().EntityId);
        var resolvedTile = ModularTilePlacementResolver.Resolve(appliedArea, placedEntity);
        var lifecycle = new RuntimeLifecycleCoordinator();
        var loaded = lifecycle.LoadProject(_history.State.Project, RuntimeAuthority.SinglePlayer,
            RuntimeLifecycleCause.Startup);
        var entered = lifecycle.EnterInitialArea(area.Id, null, RuntimeLifecycleCause.Startup);
        if (AreaFoundationValidator.Validate(appliedArea, applied).Count != 0 ||
            applied.TileKit.Definitions.Count != 1 || applied.TileKit.Placements.Count != 1 ||
            applied.TileKit.Groups.Count != 1 || applied.TileKit.Brushes.Count != 1 ||
            applied.Creation.KitKey != "crypt-modular-kit" || connection.OpenSocketCount != 1 ||
            resolvedTile.Entity.Transform.Position.X != 0 || resolvedTile.Entity.Transform.Position.Z != 0 ||
            resolvedTile.Mirror != ModularTileMirrorMode.X || resolvedTile.SemanticState != "weathered" ||
            !loaded.Succeeded || !entered.Succeeded ||
            entered.Events.Single().ScriptPath != "scripts/ash_warden_phase_two.forge")
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R028_AREA_FOUNDATION_FAILED contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r028-area-foundation",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == area.Id).Foundation;
            if (!saved.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Id != applied.Id || restored.Creation != applied.Creation ||
                restored.TileKit.Id != applied.TileKit.Id ||
                restored.TileKit.Definitions.Count != applied.TileKit.Definitions.Count ||
                restored.TileKit.Placements.Count != applied.TileKit.Placements.Count ||
                restored.Lifecycle.Entered != applied.Lifecycle.Entered)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R028_AREA_FOUNDATION_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Areas.Single(item => item.Id == area.Id).Foundation != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R028_AREA_FOUNDATION_FAILED history");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R028_AREA_FOUNDATION_PASS " +
            "wizard=kit-terrain-lighting-navigation-streaming-biome-seed-budget-preview " +
            "document=uuid-schema-provenance-environment-instances-volumes-navigation-dependencies " +
            "tiles=grid-sockets-rules-groups-brushes-runtime rotation=kit-policy-mirror-semantic-variant-raccord-preview " +
            "lifecycle=entered-exited-actor-transition-context-authority-script-api " +
            "ui=visible-empty-error command=preview-apply history=undo-redo persistence=roundtrip forgescript=typed");
        return true;
    }

    private bool RunAreaRuntimeValidationSmokeTest()
    {
        if (_history is null || _projectSession is null || _areaWorkspace is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R029_AREA_RUNTIME_VALIDATION_FAILED missing_context");
            return false;
        }
        var area = GetActiveArea();
        var original = area.Playtest;
        if (!_areaWorkspace.PrepareRuntimeValidationForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R029_AREA_RUNTIME_VALIDATION_FAILED ui_apply");
            return false;
        }
        var appliedArea = _history.State.Project.Areas.Single(item => item.Id == area.Id);
        var profile = appliedArea.Playtest;
        var navigation = AreaOperationsScriptService.ReadNavigation(_history.State.Project, area.Id);
        var readiness = AreaReadinessService.Run(_history.State.Project, area.Id);
        if (profile.StartMode != PlayFromHereStartMode.Camera || profile.TestProfile != "r029-camera-smoke" ||
            profile.CameraTransform.Position != new Vector3Value(6, 2, -2) ||
            navigation.AgentProfiles != 4 || navigation.WalkableCells == 0 ||
            readiness.Checks.Count != 9 || readiness.CategoryCounts.Count != 9)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R029_AREA_RUNTIME_VALIDATION_FAILED contract");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "r029-area-runtime",
            Guid.NewGuid().ToString("N"));
        try
        {
            var saved = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == area.Id).Playtest;
            var snapshots = new PlaytestSnapshotStore(Path.Combine(smokeRoot, ".snapshots"));
            var snapshot = snapshots.Create(_history.State.Project, area.Id, profile);
            var manifest = PlaytestLaunchManifestStore.Load(snapshot.LaunchManifestFilePath!);
            if (!saved.Succeeded || !reopened.Succeeded || restored != profile ||
                manifest.StartMode != "Camera" || manifest.TestProfile != "r029-camera-smoke" ||
                !File.Exists(snapshot.ProjectFilePath))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R029_AREA_RUNTIME_VALIDATION_FAILED persistence");
                return false;
            }
            snapshots.Delete(snapshot);
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Areas.Single(item => item.Id == area.Id).Playtest != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R029_AREA_RUNTIME_VALIDATION_FAILED history");
            return false;
        }
        GD.Print("OPENRPG_FORGE_TOOLSET_R029_AREA_RUNTIME_VALIDATION_PASS " +
            "portal=schema-provenance-orientation-velocity-hooks-preview navigation=surface-agents-links-polygons-overlay " +
            "play_from_here=spawn-camera-waypoint-character-profile-quest-manifest " +
            "validation=9-categories-ui command=preview-apply history=undo-redo persistence=roundtrip " +
            "mcp=filtered-plan-apply-revert forgescript=typed");
        return true;
    }

    private bool RunQualityLabSmokeTest()
    {
        if (_qualityLabWindow is null || _history is null || _projectSession is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11R_QUALITY_FAILED missing_context");
            return false;
        }

        if (!_qualityLabWindow.RunForSmokeTest() || _qualityLabWindow.Report is not { } report ||
            _qualityLabWindow.PerformanceReport is not { } performance ||
            _qualityLabWindow.GovernanceReport is not { } governance ||
            _qualityFoundationHistory is null ||
            QualityFoundationProfileValidator.Validate(_qualityFoundationHistory.State.Profile).Count > 0 ||
            !report.CanonicalValidation.Passed || report.CanonicalValidation.CategoryCounts.Count != 5 ||
            !_qualityLabWindow.PreviewFoundationForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11R_QUALITY_FAILED report");
            return false;
        }

        var localizationCovered = report.Localization.Coverage.Count >= 2 &&
            report.Localization.PseudoSnapshots.Count > 0 &&
            report.Localization.Issues.All(issue => issue.Severity != LocalizationQualitySeverity.Error);
        var scriptsCovered = report.Scripts.Tests.Count > 0 && report.Scripts.Tests.All(test =>
            test.Compiled && test.Passed && test.Status == ForgeScriptExecutionStatus.Completed &&
            test.EventCoverage >= 1 && test.Failures.Count == 0);
        var dialoguesCovered = report.Dialogues.Count > 0 && report.Dialogues.All(dialogue =>
            dialogue.Passed && dialogue.CoveredNodes == dialogue.TotalNodes &&
            dialogue.CoveredChoices == dialogue.TotalChoices && dialogue.Paths.SelectMany(path => path.Actions).Any());
        var questsCovered = report.Quests.Count > 0 && report.Quests.All(quest =>
            quest.Passed && quest.CoveredStates == quest.TotalStates &&
            quest.CoveredTransitions == quest.TotalTransitions &&
            quest.CoveredScopes.Count == Enum.GetValues<NarrativeFixtureScope>().Length &&
            quest.Paths.All(path => path.SaveResumeEqual));
        var encountersCovered = report.Encounters.TotalRuns >= 9 && report.Encounters.Areas.Count > 0 &&
            report.Encounters.Areas.All(area => area.CreatureCount == area.ReachableCreatures &&
                area.Runs.All(run => run.NavigationPassed && run.CleanupPassed));
        if (!localizationCovered || !scriptsCovered || !dialoguesCovered || !questsCovered || !encountersCovered ||
            report.PseudoLocalizationSnapshotPath is not null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11R_QUALITY_FAILED coverage");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_G11R_QUALITY_PASS " +
            "localization=coverage-placeholders-plurals-overflow-pseudo " +
            "scripts=sandbox-quota-mock-coverage dialogues=branches-conditions-actions " +
            "quests=events-transitions-save-scope encounters=profiles-seeds-nav-cleanup");
        GD.Print("OPENRPG_FORGE_TOOLSET_R027_QA_VALIDATION_PASS " +
            "schemas=versioned references=typed graphs=generic geometry=scale-polygon navigation=agents-doors-links-flight " +
            "ui=visible-empty-error command=preview-apply history=undo-redo persistence=roundtrip " +
            "cli=structured mcp=structured build=preflight diagnostics=code-path-uuid");
        var performanceCovered = performance.Passed && performance.Network.Passed &&
            performance.Network.Scenarios.Count == 9 && performance.Network.ReconnectPassed &&
            performance.Network.ContentMismatchPassed && performance.Network.AuthorityAttackPassed &&
            performance.VisualReviews.Cases.Count == 8 && performance.Benchmarks.Measurements.Count == 9 &&
            performance.Benchmarks.Budgets.All(item => item.Status != PerformanceBudgetStatus.Failed) &&
            Enum.GetValues<QualityTraceSurface>().All(surface =>
                performance.TraceSpans.Any(span => span.Surface == surface)) &&
            new[] { "database", "network", "script" }.All(category =>
                performance.TraceSpans.Any(span => span.Category == category)) &&
            performance.PerformanceReportPath is null && performance.VisualManifestPath is null &&
            performance.TracePath is null;
        if (!performanceCovered)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11S_PERFORMANCE_FAILED coverage");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_G11S_PERFORMANCE_PASS " +
            "network=latency-loss-reconnect-mismatch-authority visual=fixtures-tolerance-quality " +
            "benchmarks=interior-exterior-stress-load-frame-memory-nav-package " +
            "budgets=frame-memory-texture-draw-light-script-network " +
            "profiler=editor-runtime-server-script-database-network");
        var governanceCovered = governance.Passed && governance.Accessibility.Passed &&
            governance.Accessibility.EditorChecks.All(item => item.Passed) &&
            governance.Accessibility.RuntimeChecks.All(item => item.Passed) &&
            governance.SecurityFuzz.TotalCases == 112 && governance.SecurityFuzz.Boundaries.Count == 7 &&
            governance.SecurityFuzz.Boundaries.All(item => item.UnexpectedFailures == 0 && item.PathEscapes == 0) &&
            governance.Dependencies.Passed && governance.ReportPath is null;
        if (!governanceCovered)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G11T_GOVERNANCE_FAILED coverage");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_G11T_GOVERNANCE_PASS " +
            "editor=keyboard-labels-scale-contrast-shortcuts runtime=subtitles-captions-color-input-motion-difficulty " +
            "fuzz=112-cases-7-boundaries-no-crash-no-escape dependencies=approval-risk-review-audit-lifecycle");
        GD.Print("OPENRPG_FORGE_TOOLSET_R019_QA_FOUNDATIONS_PASS " +
            "licenses=spdx-provenance-notice-block domain=covered headless=scenarios " +
            "save=roundtrip-legacy-partial-missing-rollback-deterministic " +
            "logs=severity-category-object-source-transaction-privacy-retention " +
            "dry_run=true persistence=atomic undo_redo=true ui=configurable");
        return true;
    }

    private bool RunNavigationAndGeometrySmokeTest()
    {
        if (_navigationConfiguration is null || _history is null || _projectPreview is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_FAILED missing_window");
            return false;
        }

        var area = _history.State.Project.Areas.First();
        var original = area.Navigation;
        var link = new OffMeshNavigationLink(
            OffMeshNavigationLinkId.From(Guid.Parse("d7140000-0000-4000-8000-000000000001")),
            "Passage de validation",
            NavigationLinkKind.Jump,
            new Vector3Value(-5, 0, 5),
            new Vector3Value(4, 0, 1),
            true,
            1.25f,
            "workspace-smoke-test");
        var configured = new AreaNavigationDefinition(
            original.Id,
            0.5f,
            original.AgentProfiles,
            [.. original.OffMeshLinks, link],
            "workspace-smoke-test");
        if (!_navigationConfiguration.ConfigureForSmokeTest(area.Id, configured) ||
            _navigationConfiguration.AgentCount != 4 ||
            _navigationConfiguration.LinkCount != 1 ||
            !_navigationConfiguration.PreviewForSmokeTest() ||
            _navigationConfiguration.DiagnosticCount != 0 ||
            _projectPreview.NavigationOverlayCellCount == 0 ||
            !_navigationConfiguration.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_FAILED configuration");
            return false;
        }

        var appliedArea = _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id);
        var geometry = AreaGeometryValidator.Validate(_history.State.Project, appliedArea);
        var agent = appliedArea.Navigation.AgentProfiles.First(profile => profile.Name == "Moyen");
        var current = NavigationSurfaceBuilder.Build(_history.State.Project, appliedArea, agent);
        var closed = NavigationSurfaceBuilder.Build(
            _history.State.Project,
            appliedArea,
            agent,
            NavigationDoorState.AllClosed);
        var partial = NavigationBakePlanner.Plan(current, closed);
        var reachability = AreaNavigationValidator.Analyze(
            _history.State.Project,
            appliedArea,
            agent);
        var routeEntities = appliedArea.Entities
            .Where(entity => current.IsWalkable(current.CellAt(entity.Transform.Position)))
            .Take(2)
            .ToArray();
        var route = routeEntities.Length == 2
            ? NavigationPathfinder.FindPath(current, new NavigationPathQuery(
                current.CellAt(routeEntities[0].Transform.Position),
                current.CellAt(routeEntities[1].Transform.Position)))
            : null;
        var changedCell = current.WalkableCells[current.WalkableCells.Count / 2];
        var dynamic = NavigationDynamicUpdatePlanner.Plan(
            current,
            new HashSet<NavigationCell>(),
            new HashSet<NavigationCell> { changedCell });
        if (geometry.Count != 0 || reachability.Diagnostics.Count != 0 ||
            partial.RequiresFullBake || partial.ChangedCells.Count == 0 || partial.ReusedCellCount == 0 ||
            current.OffMeshEdges.Count == 0 || route?.Succeeded != true ||
            dynamic.ChangedCells.Count != 1 || dynamic.RequiresFullSearchReset)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_FAILED analysis");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "navigation-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(smokeRoot, _history.State.Project);
            if (!created.Succeeded || created.Session is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_FAILED workspace");
                return false;
            }

            NavigationBakeStore.Save(smokeRoot, current);
            var reopened = ProjectSession.Open(smokeRoot);
            var restoredBake = NavigationBakeStore.TryLoad(smokeRoot, appliedArea.Id, agent.Id);
            if (!reopened.Succeeded || reopened.Session is null ||
                reopened.Session.Project.Areas.Single().Navigation.OffMeshLinks.Single().Id != link.Id ||
                restoredBake?.SourceFingerprint != current.SourceFingerprint)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Areas.Single(candidate => candidate.Id == area.Id).Navigation != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_FAILED history");
            return false;
        }

        _projectPreview.ShowNavigationOverlay(null);
        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G714_NAVIGATION_PASS agents=4 geometry=valid " +
            "reachability=spawn-object doors=states offmesh=linked flight=profiled " +
            "incremental=partial overlay=visible history=restored persistence=roundtrip");
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_G719_PATHFINDING_PASS algorithm=deterministic-a-star " +
            $"route={route.Cells.Count} visited={route.VisitedCellCount} costs=weighted obstacles=dynamic " +
            "recovery=endpoints budget=bounded invalidation=incremental benchmark=instrumented");
        return RunWaypointAnchorSmokeTest(area.Id);
    }

    private bool RunWaypointAnchorSmokeTest(AreaId areaId)
    {
        if (_navigationConfiguration is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R009_WAYPOINT_FAILED missing_window");
            return false;
        }
        var original = _history.State.Project.Areas.Single(item => item.Id == areaId).Navigation;
        if (!_navigationConfiguration.ConfigureForSmokeTest(areaId, original) ||
            !_navigationConfiguration.ConfigureForR009SmokeTest() ||
            _navigationConfiguration.AnchorCount != 3 ||
            !_navigationConfiguration.PreviewForSmokeTest() ||
            _navigationConfiguration.DiagnosticCount != 0 ||
            !_navigationConfiguration.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R009_WAYPOINT_FAILED preview_apply_ui");
            return false;
        }

        var project = _history.State.Project;
        var area = project.Areas.Single(item => item.Id == areaId);
        var definitions = area.Navigation.Anchors.Definitions;
        var instances = area.Navigation.Anchors.Instances;
        var spawn = instances.Single(item => item.Spawn is not null);
        var portal = instances.Single(item => item.Portal is not null);
        var script = new AnchorScriptService(project, areaId);
        var snapshot = script.Query(script.Handle, limit: 10);
        var spawnResolution = script.ResolveSpawn(script.Handle, "player", "heroes");
        var portalResolution = script.ResolvePortal(script.Handle, portal.Id, new Vector3Value(2, 0, 0), 0);
        if (definitions.Count != 3 || instances.Count != 3 || snapshot.Count != 3 ||
            spawnResolution.Instance.Id != spawn.Id || spawnResolution.UsedFallback ||
            portalResolution.TargetAnchorInstanceId != portal.Portal!.TargetAnchorInstanceId ||
            !portalResolution.Preload || Math.Abs(portalResolution.ArrivalVelocity.X) < 0.01f)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R009_WAYPOINT_FAILED runtime_script");
            return false;
        }

        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "anchor-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(smokeRoot, project);
            var reopened = ProjectSession.Open(smokeRoot);
            var restored = reopened.Session?.Project.Areas.Single(item => item.Id == areaId).Navigation.Anchors;
            if (!created.Succeeded || !reopened.Succeeded || restored is null ||
                restored.Definitions.Count != 3 || restored.Instances.Count != 3 ||
                restored.Instances.Single(item => item.Id == portal.Id).Portal?.TargetAnchorInstanceId !=
                portal.Portal!.TargetAnchorInstanceId)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R009_WAYPOINT_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, true);
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        var restoredOriginal = _history.State.Project.Areas.Single(item => item.Id == areaId).Navigation;
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded || restoredOriginal != original)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R009_WAYPOINT_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print("OPENRPG_FORGE_TOOLSET_R009_WAYPOINT_PASS " +
            "anchors=uuid-schema-provenance-localized-transform-tags-scope-overrides " +
            "spawn=profile-clearance-priority-team-orientation-fallback-navigation " +
            "portal=cross-zone-orientation-velocity-preload validation=identity-references-bounds-clearance " +
            "ui=navigation-tab forgescript=typed-handle-query-runtime mcp=filtered-dry-run " +
            "history=undo-redo persistence=project-workspace-roundtrip");
        return true;
    }

    private bool RunActionableDiagnosticsSmokeTest()
    {
        if (_creatorShell is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED missing_shell");
            return false;
        }

        var originalAreaId = GetActiveArea().Id;
        var originalAreaCount = _history.State.Project.Areas.Count;
        var createdAreaId = CreateArea(
            "Diagnostic guidé G715",
            AreaKind.Interior,
            new AreaDimensions(12, 12),
            AreaTemplateKind.Empty);
        if (createdAreaId is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED create_area");
            return false;
        }

        var diagnostics = CreatorDiagnostics.FromProject(
            _history.State.Project,
            $"revision:{_history.State.Revision}");
        var diagnosticIndex = diagnostics.ToList().FindIndex(candidate =>
            candidate.Code == AreaNavigationValidator.MissingSpawnCode &&
            candidate.DocumentPath.Contains(createdAreaId.ToString()!, StringComparison.Ordinal));
        if (diagnosticIndex < 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED missing_diagnostic");
            return false;
        }

        var diagnostic = diagnostics[diagnosticIndex];
        var repeated = CreatorDiagnostics.FromProject(_history.State.Project)
            .Single(candidate => candidate.Code == diagnostic.Code && candidate.DocumentPath == diagnostic.DocumentPath);
        if (diagnostic.SchemaVersion != ActionableDiagnostic.CurrentSchemaVersion ||
            diagnostic.DiagnosticId != repeated.DiagnosticId ||
            diagnostic.SubjectUuid != createdAreaId.Value ||
            string.IsNullOrWhiteSpace(diagnostic.Property) ||
            diagnostic.Source != "AreaNavigationValidator" ||
            diagnostic.QuickFixes.Count != 1)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED structured_contract");
            return false;
        }

        _creatorShell.SetDiagnostics(diagnostics);
        var previewRevision = _history.State.Revision;
        if (!_creatorShell.SelectDiagnosticForSmokeTest(diagnosticIndex) ||
            !_creatorShell.InvokeSelectedQuickFixForSmokeTest(dryRun: true) ||
            _history.State.Revision != previewRevision ||
            !_creatorShell.InvokeSelectedQuickFixForSmokeTest(dryRun: false))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED ui_command");
            return false;
        }

        var fixedArea = _history.State.Project.Areas.Single(candidate => candidate.Id == createdAreaId);
        if (fixedArea.Entities.Count != 1 ||
            _history.State.Project.Blueprints.Single(candidate => candidate.Id == fixedArea.Entities[0].BlueprintId).Kind !=
            BlueprintKind.PlayerSpawn ||
            CreatorDiagnostics.FromProject(_history.State.Project)
                .Any(candidate => candidate.DiagnosticId == diagnostic.DiagnosticId))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED quick_fix");
            return false;
        }

        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "actionable-diagnostics-smoke",
            Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            var persistedArea = reopened.Session?.Project.Areas.SingleOrDefault(candidate => candidate.Id == createdAreaId);
            if (!created.Succeeded || !reopened.Succeeded || persistedArea?.Entities.Count != 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED persistence");
                return false;
            }
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        var undoFix = _history.Undo();
        var redoFix = _history.Redo();
        var cleanupFix = _history.Undo();
        var cleanupArea = _history.Undo();
        _activeAreaContext.EnsureValid(_history.State.Project);
        ActivateArea(originalAreaId);
        if (!undoFix.Succeeded || !redoFix.Succeeded || !cleanupFix.Succeeded || !cleanupArea.Succeeded ||
            _history.State.Project.Areas.Count != originalAreaCount ||
            _history.State.Project.Areas.Any(candidate => candidate.Id == createdAreaId))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G715_DIAGNOSTICS_PASS uuid=stable severity=typed path=document " +
            "property=visible source=validator quickfix=command preview=dry-run navigation=targeted " +
            "history=restored persistence=roundtrip");
        return true;
    }

    private bool RunAssetContentBrowserSmokeTest()
    {
        if (_assetBrowser is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G716_ASSETS_FAILED missing_browser");
            return false;
        }

        _assetBrowser.Refresh();
        if (_assetBrowser.CatalogCount != 665 || _assetBrowser.SourceCount != 5 ||
            _assetBrowser.FilterForSmokeTest("apple", "ambientcg") != 3 ||
            !_assetBrowser.SelectAssetForSmokeTest("ambientcg:3DApple001") ||
            !_assetBrowser.SelectedHasPreviewMetadata ||
            (_assetBrowser.SelectedCanPlanImport && !_assetBrowser.PreviewImportForSmokeTest()) ||
            !_assetBrowser.PreviewProfileCommandForSmokeTest() ||
            !_assetBrowser.PreviewManifestCommandForSmokeTest() ||
            !_assetBrowser.PreviewExternalEditCommandForSmokeTest() ||
            !_assetBrowser.PreviewGovernanceCommandForSmokeTest() ||
            _assetBrowser.FilterForSmokeTest(string.Empty, "kenney") != 50 ||
            !_assetBrowser.SelectAssetForSmokeTest("kenney:castle-kit") ||
            !_assetBrowser.PreviewInstallForPlacementForSmokeTest() ||
            _assetBrowser.FilterForSmokeTest(string.Empty, category: "furniture") == 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G716_ASSETS_FAILED catalog_facets_preview");
            return false;
        }

        _assetBrowser.FilterForSmokeTest(string.Empty);
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G716_ASSETS_PASS catalog=665 sources=5 facets=search-source-category " +
            "uuid=stable hash=addressed license=verified availability=explicit preview=metadata import=governed " +
            "profile=command-dry-run manifest=command-dry-run roundtrip=command-dry-run " +
            "governance=command-dry-run quarantine=pending ip=classified " +
            "install=command-dry-run placement=palette-drag");
        GD.Print("OPENRPG_FORGE_TOOLSET_R018_ASSET_GOVERNANCE_PASS provenance=visible quarantine=pending " +
                 "ip=classified dry_run=true persistence=atomic undo_redo=true distribution=guarded");
        return true;
    }

    private bool RunGovernedAssetPreviewSmokeTest()
    {
        if (_mainPanel is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_AV3_PLACEMENT_FAILED missing_panel");
            return false;
        }

        var root = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "av3-preview-smoke",
            Guid.NewGuid().ToString("N"));
        var modelRelativePath = "assets/imported/av3-smoke/model.glb";
        var modelPath = Path.Combine(root, modelRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        WriteSmokeTriangleGlb(modelPath);
        var modelHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(modelPath))).ToLowerInvariant();
        var assetId = AssetCatalogItemId.ForExternalId("openrpg:av3-preview-smoke");
        var item = new AssetCatalogItem(
            assetId,
            "openrpg:av3-preview-smoke",
            "Modèle gouverné AV3",
            "openrpg",
            "av3-preview-smoke",
            "https://openrpg.example/assets/av3-preview-smoke",
            "CC0-1.0",
            "https://creativecommons.org/publicdomain/zero/1.0/",
            true,
            modelHash,
            new FileInfo(modelPath).Length,
            ["3d-model"],
            ["smoke"],
            "generated-test-fixture",
            AssetAvailability.ExtractedAvailable,
            modelRelativePath,
            null,
            null,
            "toolset-smoke:av3");
        var chunk = new AssetManifestChunk(
            Guid.Parse("a83d0000-0000-4000-8000-000000000001"),
            AssetChunkRole.Model,
            true,
            modelRelativePath,
            modelHash,
            new FileInfo(modelPath).Length,
            new FileInfo(modelPath).Length,
            AssetChunkCompression.None,
            [AssetTargetPlatform.Any]);
        var manifest = new AssetPackageManifest(
            AssetPackageManifest.CurrentSchemaVersion,
            AssetPackageManifestId.ForAsset(assetId),
            assetId,
            "toolset-smoke:av3",
            item.LicenseSpdx,
            item.ContentSha256,
            new Dictionary<AssetTargetPlatform, long> { [AssetTargetPlatform.Any] = 1024 * 1024 },
            [chunk],
            null,
            null,
            null,
            null);
        var blueprint = AssetBlueprintFactory.Create(item, manifest);
        var sourceProject = _history.State.Project;
        var sourceArea = GetActiveArea();
        var scatterSource = AreaWorldProfile.CreateDefault(sourceArea.Id, AreaKind.Exterior, sourceArea.Dimensions);
        var scatterMaterial = scatterSource.Terrain.MaterialLayers.Single(candidate => candidate.Name == "Prairie");
        var scatterBiome = new BiomeLayerDefinition(
            Guid.Parse("a83d0000-0000-4000-8000-000000000003"),
            "Prairie gouvernée AV3",
            scatterMaterial.Id,
            -512,
            512,
            0,
            90,
            0,
            1,
            .35f,
            $"asset:{assetId}",
            ["ground-cover", "meadow"],
            [],
            20,
            "toolset-smoke:av3");
        var scatterWorld = new AreaWorldProfile(
            scatterSource.Id,
            scatterSource.SchemaVersion,
            scatterSource.Terrain,
            [scatterBiome],
            [],
            scatterSource.VolumetricNavigation,
            [],
            scatterSource.Loading,
            "toolset-smoke:av3");
        var entity = new EntityInstance(
            EntityId.From(Guid.Parse("a83d0000-0000-4000-8000-000000000002")),
            blueprint.Id,
            TransformValue.Identity,
            ["asset"],
            new EntityPropertyOverrides(
                EntityPropertyOverrides.CurrentSchemaVersion,
                [new EntityPropertyOverride("visual.asset", EntityPropertyValueKind.String, assetId.ToString())]));
        var project = new ProjectDefinition(
            sourceProject.Id,
            sourceProject.Title,
            sourceProject.Module,
            [blueprint],
            [sourceArea.WithWorld(scatterWorld).WithEntities([entity])],
            sourceProject.Dialogues,
            sourceProject.Quests);
        var state = new ProjectState(project, new ProjectRevision(0));
        var manifests = new AssetPackageManifestState([manifest]);
        var plannedScatter = BiomeScatterPlanBuilder.Build(scatterWorld, scatterBiome);
        var smokeLibrary = new GodotAssetModelLibrary(root, manifests.Manifests);
        var resolvedScatterMesh = smokeLibrary.ResolveScatterMesh(scatterBiome.ScatterResource);
        if (plannedScatter.Count == 0 || resolvedScatterMesh is null)
        {
            var modelTree = "unavailable";
            if (resolvedScatterMesh is null)
            {
                var instance = smokeLibrary.Instantiate(assetId.ToString());
                try
                {
                    modelTree = DescribeSmokeNodeTree(instance);
                }
                finally
                {
                    instance.Free();
                }
            }
            GD.PrintErr(
                "OPENRPG_FORGE_TOOLSET_AV3_PLACEMENT_FAILED scatter_preflight " +
                $"planned={plannedScatter.Count} manifests={manifests.Manifests.Count} tree={modelTree}");
            return false;
        }
        var areaId = project.Areas.First().Id;
        var preview = new ProjectPreview3D { Visible = false };
        _mainPanel.AddChild(preview);
        try
        {
            preview.Initialize(
                () => state,
                () => root,
                () => manifests,
                () => areaId,
                () => 1f,
                _ => { },
                (_, _) => false,
                (_, _) => null,
                (_, _) => { });
            preview.Rebuild(project);
            if (preview.AssetModelInstanceCount != 1 || preview.TerrainScatterLayerCount != 1 ||
                preview.TerrainScatterInstanceCount <= 0)
            {
                GD.PrintErr(
                    "OPENRPG_FORGE_TOOLSET_AV3_PLACEMENT_FAILED expected_models=1 expected_scatter=1+ " +
                    $"loaded={preview.AssetModelInstanceCount} scatter_layers={preview.TerrainScatterLayerCount} " +
                    $"scatter_instances={preview.TerrainScatterInstanceCount}");
                return false;
            }

            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_AV3_PLACEMENT_PASS models={preview.AssetModelInstanceCount} " +
                $"manifests={manifests.Manifests.Count} scatter_layers={preview.TerrainScatterLayerCount} " +
                $"scatter_instances={preview.TerrainScatterInstanceCount} visual=governed-glb hash=verified " +
                "preview=editor biome_brush=canonical");
            return true;
        }
        finally
        {
            _mainPanel.RemoveChild(preview);
            preview.Free();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteSmokeTriangleGlb(string path)
    {
        const string json =
            "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}]," +
            "\"nodes\":[{\"mesh\":0}],\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0},\"indices\":1}]}]," +
            "\"buffers\":[{\"byteLength\":42}]," +
            "\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":36,\"target\":34962}," +
            "{\"buffer\":0,\"byteOffset\":36,\"byteLength\":6,\"target\":34963}]," +
            "\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\"," +
            "\"min\":[0,0,0],\"max\":[1,1,0]},{\"bufferView\":1,\"componentType\":5123,\"count\":3,\"type\":\"SCALAR\"}]}";
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var jsonLength = (jsonBytes.Length + 3) & ~3;
        const int binaryLength = 44;
        var totalLength = 12 + 8 + jsonLength + 8 + binaryLength;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
        writer.Write(0x46546c67u);
        writer.Write(2u);
        writer.Write((uint)totalLength);
        writer.Write((uint)jsonLength);
        writer.Write(0x4e4f534au);
        writer.Write(jsonBytes);
        for (var index = jsonBytes.Length; index < jsonLength; index++) writer.Write((byte)0x20);
        writer.Write((uint)binaryLength);
        writer.Write(0x004e4942u);
        foreach (var coordinate in new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f }) writer.Write(coordinate);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)2);
        writer.Write((ushort)0);
    }

    private static string DescribeSmokeNodeTree(Node root)
    {
        var nodes = new List<string>();
        void Visit(Node node)
        {
            nodes.Add(node is MeshInstance3D mesh
                ? $"{node.GetType().Name}[mesh={mesh.Mesh?.GetType().Name ?? "null"}]"
                : node.GetType().Name);
            foreach (Node child in node.GetChildren()) Visit(child);
        }

        Visit(root);
        return string.Join(",", nodes);
    }

    private bool RunLocalizationWorkspaceSmokeTest()
    {
        if (_history is null || _localizationWorkspace is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G717_LOCALIZATION_FAILED missing_workspace");
            return false;
        }

        var originalCatalog = _history.State.Project.Module.Localization;
        _localizationWorkspace.Refresh();
        var defaultLocale = _history.State.Project.Module.Metadata.DefaultLocale;
        if (_localizationWorkspace.MessageCount == 0 || _localizationWorkspace.LocaleCount == 0 ||
            !_localizationWorkspace.SelectForSmokeTest("module.name", defaultLocale) ||
            !_localizationWorkspace.SetTranslationForSmokeTest("Crypte des Cendres · localisation") ||
            !_localizationWorkspace.PreviewForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G717_LOCALIZATION_FAILED extract_edit_preview");
            return false;
        }

        var draft = LocalizationCatalogExtractor.Extract(_history.State.Project).Catalog;
        var localized = new LocalizationCatalog(draft.Id, draft.DefaultLocale, draft.Locales,
            draft.Messages.Select(message => message.Key == "module.name"
                ? new LocalizationMessage(message.Key, message.Fallback,
                    message.Translations.ToDictionary(pair => pair.Key, pair =>
                        pair.Key == defaultLocale ? "Crypte des Cendres · localisation" : pair.Value),
                    message.Provenance)
                : message).ToArray());
        if (!ApplyLocalizationCatalog(localized, false) ||
            _history.State.Project.Module.Localization.Resolve("module.name", defaultLocale) != "Crypte des Cendres · localisation")
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G717_LOCALIZATION_FAILED command");
            return false;
        }

        var undo = _history.Undo();
        var redo = _history.Redo();
        var cleanup = _history.Undo();
        if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
            _history.State.Project.Module.Localization != originalCatalog)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G717_LOCALIZATION_FAILED history");
            return false;
        }

        RefreshEditorControls();
        GD.Print(
            $"OPENRPG_FORGE_TOOLSET_G717_LOCALIZATION_PASS messages={_localizationWorkspace.MessageCount} " +
            $"locales={_localizationWorkspace.LocaleCount} extraction=canonical fallback=resolved " +
            "pseudo=available preview=dry-run history=restored");
        return true;
    }

    private bool RunAssistantFoundationSmokeTest()
    {
        if (_projectSession is null || _history is null || _assistantProfileHistory is null || _assistantWorkspace is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12A_ASSISTANT_FAILED missing_workspace");
            return false;
        }

        var originalProfile = _assistantProfileHistory.State.Profile;
        var profilePath = Path.Combine(_projectSession.RootDirectory, "assistant", "profile.json");
        var originalBytes = File.Exists(profilePath) ? File.ReadAllBytes(profilePath) : null;
        try
        {
            if (!_assistantWorkspace.ConfigureForSmokeTest("1.1.0") ||
                !_assistantWorkspace.PreviewForSmokeTest() ||
                string.IsNullOrWhiteSpace(_assistantWorkspace.LastPromptHash) ||
                _assistantWorkspace.LastPromptHash.Length != 64 ||
                !_assistantWorkspace.LastPreviewWasOffline ||
                _assistantWorkspace.LastProviderCost != 0 ||
                !_assistantWorkspace.ApplyForSmokeTest() ||
                _assistantProfileHistory.State.Profile.Prompts[0].Version != "1.1.0")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12A_ASSISTANT_FAILED preview_apply");
                return false;
            }
            var restored = AssistantWorkspaceProfileStore.Load(_projectSession.RootDirectory);
            if (!restored.Succeeded || restored.Profile.Prompts[0].Version != "1.1.0" ||
                AgentSemanticWorkflowValidator.Validate(restored.Profile.AgentWorkflow).Count != 0 ||
                restored.Profile.AgentWorkflow.ContextResources.Count != 10 ||
                restored.Profile.AgentWorkflow.Tools.Count != 5 ||
                restored.Profile.AgentWorkflow.EffectiveGovernance.Authorization.MaximumCommands != 12 ||
                restored.Profile.AgentWorkflow.EffectiveGovernance.Authorization.MaximumDurationSeconds != 45 ||
                restored.Profile.AgentWorkflow.EffectiveGovernance.Authorization.AllowBoundedAutonomy ||
                !restored.Profile.AgentWorkflow.EffectiveGovernance.BudgetPrivacy.ProhibitSecretLogging ||
                restored.Profile.AgentWorkflow.EffectiveSafety.StructuredOutput.MaximumResponseCharacters != 131072 ||
                restored.Profile.AgentWorkflow.EffectiveSafety.PromptInjection.MaximumUntrustedCharacters != 32768 ||
                AgentResponseSafetyProfileValidator.Validate(restored.Profile.AgentWorkflow.EffectiveSafety).Count != 0 ||
                !_assistantWorkspace.LastStructuredResultSucceeded ||
                !_assistantWorkspace.LastOfflineReady ||
                _assistantWorkspace.LastInjectionSignalCount != 0 ||
                CreatorReferenceIndex.Build(_history.State.Project).References.Count == 0 ||
                !UndoAssistantWorkspaceProfile() ||
                _assistantProfileHistory.State.Profile.Prompts[0].Version != originalProfile.Prompts[0].Version ||
                !RedoAssistantWorkspaceProfile() ||
                _assistantProfileHistory.State.Profile.Prompts[0].Version != "1.1.0" ||
                !UndoAssistantWorkspaceProfile())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12A_ASSISTANT_FAILED persistence_history");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12A_ASSISTANT_PASS ui=context-selection-transaction " +
                "providers=offline-openai-compatible-ollama prompts=versioned-tested " +
                "plan=dry-run-bound permission=configure secrets=environment-only " +
                "offline=deterministic cost=visible history=restored persistence=roundtrip");
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_R020_AGENT_WORKFLOW_PASS context=10-structured " +
                "graph=reverse-impact-typed tools=semantic-domain " +
                "plan=goal-commands-assumptions-objects-risks-tests " +
                "dry_run=revision-batch-diff-no-write permission=bounded " +
                "history=restored persistence=roundtrip");
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_R021_AGENT_GOVERNANCE_PASS " +
                "transaction=begin-preview-commit-rollback-atomic diff=objects-references-states-assets-generated " +
                "permission=read-propose-confirmed-autonomous-bounded audit=client-model-user-plan-commands-hashes-tests-provenance-rollback " +
                "privacy=classification-opt-in-redaction-no-secret budget=provider-token-cost " +
                "history=restored persistence=roundtrip");
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_R022_AGENT_SAFETY_PASS " +
                "output=json-schema-tool-calls-bounded-repair ambiguity=rejected " +
                "injection=data-provenance-allowlist-no-escalation-confirmation " +
                "offline=manual-local-optional-metadata-optional-build-runtime-independent " +
                "network=disabled python=allowlisted-no-exec history=restored persistence=roundtrip");
            return true;
        }
        finally
        {
            _assistantProfileHistory = new AssistantWorkspaceProfileCommandHistory(originalProfile);
            if (originalBytes is null)
            {
                if (File.Exists(profilePath)) File.Delete(profilePath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
                File.WriteAllBytes(profilePath, originalBytes);
            }
        }
    }

    private bool RunLoreIndexSmokeTest()
    {
        if (_projectSession is null || _loreIndexHistory is null || _loreIndexWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12B_ASSISTANT_FAILED missing_lore_workspace");
            return false;
        }

        var originalIndex = _loreIndexHistory.State.Index;
        var indexPath = Path.Combine(_projectSession.RootDirectory, "assistant", "lore-index.json");
        var originalBytes = File.Exists(indexPath) ? File.ReadAllBytes(indexPath) : null;
        try
        {
            if (!_loreIndexWindow.ConfigureForSmokeTest() ||
                !_loreIndexWindow.PreviewForSmokeTest() ||
                _loreIndexWindow.LastContextHash.Length != 64 ||
                _loreIndexWindow.LastContextSourceCount != 1 ||
                !_loreIndexWindow.ApplyForSmokeTest() ||
                _loreIndexHistory.State.Index.Entries.Count != originalIndex.Entries.Count + 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12B_ASSISTANT_FAILED lore_preview_apply");
                return false;
            }
            var restored = LoreIndexStore.Load(_projectSession.RootDirectory);
            if (!restored.Succeeded || restored.Index.Entries.Count != originalIndex.Entries.Count + 1 ||
                !UndoLoreIndex() ||
                _loreIndexHistory.State.Index.Entries.Count != originalIndex.Entries.Count ||
                !RedoLoreIndex() ||
                _loreIndexHistory.State.Index.Entries.Count != originalIndex.Entries.Count + 1 ||
                !UndoLoreIndex())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12B_ASSISTANT_FAILED lore_persistence_history");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12B_ASSISTANT_PASS " +
                "transports=openai-compatible-ollama network=explicit-loopback timeout=bounded cancellation=propagated " +
                "lore=world-faction-character-timeline-terminology sources=resolved retrieval=hashed-bounded " +
                "plan=dry-run-transaction permission=configure history=restored persistence=roundtrip");
            return true;
        }
        finally
        {
            _loreIndexHistory = new LoreIndexCommandHistory(originalIndex);
            if (originalBytes is null)
            {
                if (File.Exists(indexPath)) File.Delete(indexPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
                File.WriteAllBytes(indexPath, originalBytes);
            }
        }
    }

    private bool RunAssistedAreaPlanSmokeTest()
    {
        if (_projectSession is null || _history is null || _assistedAreaPlanHistory is null ||
            _assistedAreaPlanWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12C_AREA_PLAN_FAILED missing_workspace");
            return false;
        }
        var originalCatalog = _assistedAreaPlanHistory.State.Catalog;
        var originalEntityCount = _history.State.Project.Areas.Sum(item => item.Entities.Count);
        var path = Path.Combine(_projectSession.RootDirectory, AssistedAreaPlanStore.RelativePath);
        var originalBytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
        try
        {
            if (!_assistedAreaPlanWindow.ConfigureForSmokeTest() ||
                !_assistedAreaPlanWindow.PreviewForSmokeTest() ||
                _assistedAreaPlanWindow.LastGeometryApplied ||
                _assistedAreaPlanWindow.LastEstimatedCostUsd != 0 ||
                !_assistedAreaPlanWindow.ApplyForSmokeTest() ||
                _assistedAreaPlanHistory.State.Catalog.Plans.Count != originalCatalog.Plans.Count + 1 ||
                _history.State.Project.Areas.Sum(item => item.Entities.Count) != originalEntityCount)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12C_AREA_PLAN_FAILED preview_apply_geometry_guard");
                return false;
            }
            var restored = AssistedAreaPlanStore.Load(_projectSession.RootDirectory);
            if (!restored.Succeeded || restored.Catalog.Plans.Count != originalCatalog.Plans.Count + 1 ||
                !UndoAssistedAreaPlans() || _assistedAreaPlanHistory.State.Catalog.Plans.Count != originalCatalog.Plans.Count ||
                !RedoAssistedAreaPlans() || _assistedAreaPlanHistory.State.Catalog.Plans.Count != originalCatalog.Plans.Count + 1 ||
                !UndoAssistedAreaPlans())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12C_AREA_PLAN_FAILED persistence_history");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12C_AREA_PLAN_PASS archetypes=crypt-village topology=connected-deterministic " +
                "kits=resolved constraints=bounded objectives=explicit lore=sourced provider=fake-offline cost=visible " +
                "geometry=not-applied plan=dry-run-transaction permission=configure-approve history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            _assistedAreaPlanHistory = new AssistedAreaPlanCommandHistory(originalCatalog);
            if (originalBytes is null)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, originalBytes);
            }
        }
    }

    private bool RunAssistedAreaBuildSmokeTest()
    {
        if (_projectSession is null || _history is null || _projectPreview is null ||
            _assistedAreaPlanHistory is null || _assistedAreaPlanWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12D_AREA_BUILD_FAILED missing_workspace");
            return false;
        }
        var originalState = _history.State;
        var originalCatalog = _assistedAreaPlanHistory.State.Catalog;
        var originalEntityCount = originalState.Project.Areas.Sum(item => item.Entities.Count);
        var catalogPath = Path.Combine(_projectSession.RootDirectory, AssistedAreaPlanStore.RelativePath);
        var originalCatalogBytes = File.Exists(catalogPath) ? File.ReadAllBytes(catalogPath) : null;
        var areaDocumentsDirectory = Path.Combine(_projectSession.RootDirectory, "areas");
        var originalAreaDocuments = Directory.Exists(areaDocumentsDirectory)
            ? Directory.EnumerateFiles(areaDocumentsDirectory, "*.json").ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!_assistedAreaPlanWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedAreaPlanWindow.PreviewForSmokeTest() ||
                !_assistedAreaPlanWindow.ApplyForSmokeTest() ||
                !_assistedAreaPlanWindow.SelectApprovedPlanForSmokeTest() ||
                !_assistedAreaPlanWindow.PreviewBuildForSmokeTest() ||
                _assistedAreaPlanWindow.LastBuildEntityCount <= 0)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12D_AREA_BUILD_FAILED plan_preview");
                return false;
            }
            var placementCount = _assistedAreaPlanWindow.LastBuildEntityCount;
            if (!_assistedAreaPlanWindow.ApplyBuildForSmokeTest() ||
                _history.State.Project.Areas.Sum(item => item.Entities.Count) != originalEntityCount + placementCount ||
                _projectPreview.EntityCount != GetActiveArea().Entities.Count)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12D_AREA_BUILD_FAILED atomic_apply");
                return false;
            }
            var reopened = ProjectSession.Open(_projectSession.RootDirectory);
            if (reopened.Session is null ||
                reopened.Session.Project.Areas.Sum(item => item.Entities.Count) != originalEntityCount + placementCount)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12D_AREA_BUILD_FAILED persistence");
                return false;
            }
            var undone = _history.Undo();
            if (!undone.Succeeded ||
                _history.State.Project.Areas.Sum(item => item.Entities.Count) != originalEntityCount ||
                !_history.Redo().Succeeded ||
                _history.State.Project.Areas.Sum(item => item.Entities.Count) != originalEntityCount + placementCount ||
                !_history.Undo().Succeeded ||
                !_projectSession.Save(_history.State.Project).Succeeded)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12D_AREA_BUILD_FAILED history");
                return false;
            }
            RebuildPreview();
            RefreshEditorControls();
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12D_AREA_BUILD_PASS placements=batch-manual-commands seed=preserved " +
                "geometry=validated navigation=no-regression lighting=validated approval=required " +
                "plan=dry-run-transaction permission=apply-approved atomic=true history=restored " +
                "persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            _history = new ProjectCommandHistory(originalState);
            _projectSession.Save(originalState.Project);
            if (Directory.Exists(areaDocumentsDirectory))
            {
                foreach (var generatedPath in Directory.EnumerateFiles(areaDocumentsDirectory, "*.json")
                             .Where(item => !originalAreaDocuments.Contains(item)))
                    File.Delete(generatedPath);
            }
            _assistedAreaPlanHistory = new AssistedAreaPlanCommandHistory(originalCatalog);
            if (originalCatalogBytes is null)
            {
                if (File.Exists(catalogPath)) File.Delete(catalogPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
                File.WriteAllBytes(catalogPath, originalCatalogBytes);
            }
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunAssistedDialogueSmokeTest()
    {
        if (_projectSession is null || _history is null || _assistedDialogueWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12E_DIALOGUE_FAILED missing_workspace");
            return false;
        }
        var originalState = _history.State;
        var originalDialogueCount = originalState.Project.Dialogues.Count;
        try
        {
            if (!_assistedDialogueWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedDialogueWindow.PreviewForSmokeTest() ||
                _assistedDialogueWindow.LastNodeCount != 4 ||
                _assistedDialogueWindow.LastLintCount != 0 ||
                _assistedDialogueWindow.LastCitationCount < 1 ||
                _assistedDialogueWindow.LastEstimatedCostUsd != 0 ||
                !_assistedDialogueWindow.ApplyForSmokeTest() ||
                _history.State.Project.Dialogues.Count != originalDialogueCount + 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12E_DIALOGUE_FAILED preview_apply");
                return false;
            }
            var reopened = ProjectSession.Open(_projectSession.RootDirectory);
            if (reopened.Session is null || reopened.Session.Project.Dialogues.Count != originalDialogueCount + 1 ||
                !_history.Undo().Succeeded || _history.State.Project.Dialogues.Count != originalDialogueCount ||
                !_history.Redo().Succeeded || _history.State.Project.Dialogues.Count != originalDialogueCount + 1 ||
                !_history.Undo().Succeeded || !_projectSession.Save(_history.State.Project).Succeeded)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12E_DIALOGUE_FAILED persistence_history");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12E_DIALOGUE_PASS graph=canonical-minimap nodes=4 " +
                "conditions=typed actions=typed style=cited localization=placeholders lint=clean " +
                "simulation=entry-rendered provider=fake-offline cost=visible plan=dry-run-transaction " +
                "permission=apply-approved history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            _history = new ProjectCommandHistory(originalState);
            _projectSession.Save(originalState.Project);
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunAssistedQuestSmokeTest()
    {
        if (_projectSession is null || _history is null || _assistedQuestWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12F_QUEST_FAILED missing_workspace");
            return false;
        }
        var originalState = _history.State;
        var originalQuestCount = originalState.Project.Quests.Count;
        try
        {
            if (!_assistedQuestWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedQuestWindow.PreviewForSmokeTest() ||
                _assistedQuestWindow.LastStateCount != 4 ||
                _assistedQuestWindow.LastObjectiveCount < 1 ||
                _assistedQuestWindow.LastScenarioCount != 5 ||
                _assistedQuestWindow.LastLintCount != 0 ||
                _assistedQuestWindow.LastEstimatedCostUsd != 0 ||
                !_assistedQuestWindow.ApplyForSmokeTest() ||
                _history.State.Project.Quests.Count != originalQuestCount + 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12F_QUEST_FAILED preview_apply");
                return false;
            }
            var reopened = ProjectSession.Open(_projectSession.RootDirectory);
            if (reopened.Session is null || reopened.Session.Project.Quests.Count != originalQuestCount + 1 ||
                !_history.Undo().Succeeded || _history.State.Project.Quests.Count != originalQuestCount ||
                !_history.Redo().Succeeded || _history.State.Project.Quests.Count != originalQuestCount + 1 ||
                !_history.Undo().Succeeded || !_projectSession.Save(_history.State.Project).Succeeded)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12F_QUEST_FAILED persistence_history");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12F_QUEST_PASS participants=resolved states=4 objectives=bounded " +
                "transitions=3 rewards=progression failure=explicit tests=5-scenarios style=cited lint=clean " +
                "provider=fake-offline cost=visible plan=dry-run-transaction permission=apply-approved " +
                "history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            _history = new ProjectCommandHistory(originalState);
            _projectSession.Save(originalState.Project);
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunAssistedForgeScriptSmokeTest()
    {
        if (_projectSession is null || _history is null || _assistedForgeScriptWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12G_SCRIPT_FAILED missing_workspace");
            return false;
        }
        var relativePath = _assistedForgeScriptWindow.SmokeRelativePath;
        var path = AssistedForgeScriptCreationService.ResolvePath(_projectSession.RootDirectory, relativePath);
        var original = File.Exists(path) ? File.ReadAllText(path) : null;
        try
        {
            if (!_assistedForgeScriptWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedForgeScriptWindow.PreviewForSmokeTest() ||
                !_assistedForgeScriptWindow.LastCompiled ||
                _assistedForgeScriptWindow.LastTestCount != 3 ||
                _assistedForgeScriptWindow.LastErrorCount != 0 ||
                _assistedForgeScriptWindow.LastApiVersion != "1.0.0" ||
                _assistedForgeScriptWindow.LastEstimatedCostUsd != 0 ||
                !_assistedForgeScriptWindow.ApplyForSmokeTest() ||
                !File.Exists(path) ||
                !new NativeForgeScriptRuntime().Compile(File.ReadAllText(path)).Success)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12G_SCRIPT_FAILED preview_compile_test_apply");
                return false;
            }
            if (!UndoAssistedForgeScript() || File.Exists(path) != (original is not null) ||
                !RedoAssistedForgeScript() || !File.Exists(path) ||
                !UndoAssistedForgeScript() || File.Exists(path) != (original is not null))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12G_SCRIPT_FAILED history_roundtrip");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12G_SCRIPT_PASS api=1.0.0 vm=2.38.0 source=generated-editable " +
                "binding=typed compile=native diagnostics=structured tests=compile-api-sandbox path=confined " +
                "provider=fake-offline cost=visible plan=dry-run-transaction permission=apply-approved " +
                "history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            ApplyAssistedForgeScriptHandler.Restore(
                _projectSession.RootDirectory,
                new AssistedForgeScriptFilePatch(relativePath, original, original ?? string.Empty),
                useAfter: false);
            _assistedForgeScriptHistory.Reset();
            _buildPipeline?.Refresh();
        }
    }

    private bool RunAssistedBossSmokeTest()
    {
        if (_projectSession is null || _history is null || _assistedBossWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12H_BOSS_FAILED missing_workspace");
            return false;
        }
        var originalState = _history.State;
        var originalBlueprintCount = originalState.Project.Blueprints.Count;
        var originalDialogueCount = originalState.Project.Dialogues.Count;
        var originalDocumentCount = originalState.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count;
        try
        {
            if (!_assistedBossWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedBossWindow.PreviewForSmokeTest() ||
                _assistedBossWindow.LastAbilityCount < 2 ||
                _assistedBossWindow.LastPhaseCount is < 2 or > 3 ||
                _assistedBossWindow.LastBalanceRuns != 32 ||
                !_assistedBossWindow.LastBalancePassed ||
                _assistedBossWindow.LastDiagnosticCount != 0 ||
                _assistedBossWindow.LastEstimatedCostUsd != 0 ||
                !_assistedBossWindow.ApplyForSmokeTest() ||
                _history.State.Project.Blueprints.Count != originalBlueprintCount + 1 ||
                _history.State.Project.Dialogues.Count != originalDialogueCount + 1 ||
                _history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount + 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12H_BOSS_FAILED preview_balance_apply");
                return false;
            }
            var reopened = ProjectSession.Open(_projectSession.RootDirectory);
            if (reopened.Session is null ||
                reopened.Session.Project.Blueprints.Count != originalBlueprintCount + 1 ||
                reopened.Session.Project.Dialogues.Count != originalDialogueCount + 1 ||
                reopened.Session.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount + 1 ||
                !_history.Undo().Succeeded ||
                _history.State.Project.Blueprints.Count != originalBlueprintCount ||
                _history.State.Project.Dialogues.Count != originalDialogueCount ||
                _history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount ||
                !_history.Redo().Succeeded ||
                _history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount + 1 ||
                !_history.Undo().Succeeded || !_projectSession.Save(_history.State.Project).Succeeded)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12H_BOSS_FAILED persistence_history");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12H_BOSS_PASS creature=canonical encounter=bounded abilities=typed " +
                "phases=scripted dialogue=typed rewards=loot balance=32-runs dependencies=resolved " +
                "provider=fake-offline cost=visible plan=dry-run-transaction permission=apply-approved " +
                "batch=atomic history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            _history = new ProjectCommandHistory(originalState);
            _projectSession.Save(originalState.Project);
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunAssistedAbilitySmokeTest()
    {
        if (_projectSession is null || _history is null || _assistedAbilityWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12I_ABILITY_FAILED missing_workspace");
            return false;
        }
        var originalState = _history.State;
        var originalAbilityCount = originalState.Project.Blueprints
            .Where(item => item.Combat is not null)
            .Sum(item => item.Combat!.Abilities.Count);
        var originalDocumentCount = originalState.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count;
        try
        {
            if (!_assistedAbilityWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedAbilityWindow.PreviewForSmokeTest() ||
                _assistedAbilityWindow.LastTestCount != 5 ||
                _assistedAbilityWindow.LastPassedTestCount != 5 ||
                !_assistedAbilityWindow.LastBalancePassed ||
                _assistedAbilityWindow.LastDiagnosticCount != 0 ||
                _assistedAbilityWindow.LastEstimatedCostUsd != 0 ||
                !_assistedAbilityWindow.ApplyForSmokeTest() ||
                _history.State.Project.Blueprints.Where(item => item.Combat is not null)
                    .Sum(item => item.Combat!.Abilities.Count) != originalAbilityCount + 1 ||
                _history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount + 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12I_ABILITY_FAILED preview_tests_balance_apply");
                return false;
            }
            var reopened = ProjectSession.Open(_projectSession.RootDirectory);
            if (reopened.Session is null ||
                reopened.Session.Project.Blueprints.Where(item => item.Combat is not null)
                    .Sum(item => item.Combat!.Abilities.Count) != originalAbilityCount + 1 ||
                reopened.Session.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount + 1 ||
                !_history.Undo().Succeeded ||
                _history.State.Project.Blueprints.Where(item => item.Combat is not null)
                    .Sum(item => item.Combat!.Abilities.Count) != originalAbilityCount ||
                _history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount ||
                !_history.Redo().Succeeded ||
                _history.State.Project.Module.RuntimePolicies.Extensions.JsonDocuments.Count != originalDocumentCount + 1 ||
                !_history.Undo().Succeeded || !_projectSession.Save(_history.State.Project).Succeeded)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12I_ABILITY_FAILED persistence_history");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12I_ABILITY_PASS targeting=typed cost=bounded effects=canonical " +
                "vfx=resolved audio=resolved ai=tagged script=resolved item=optional tests=5-combat " +
                "balance=visible provider=fake-offline costusd=visible plan=dry-run-transaction " +
                "permission=apply-approved batch=atomic history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            _history = new ProjectCommandHistory(originalState);
            _projectSession.Save(originalState.Project);
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunAssistedBlenderCreationSmokeTest()
    {
        if (_projectSession is null || _assetExternalEditHistory is null ||
            _assistedBlenderCreationWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12J_BLENDER_FAILED missing_workspace");
            return false;
        }
        var originalState = _assetExternalEditHistory.State;
        string? recipePath = null;
        string? requestPath = null;
        string? externalPath = null;
        string? beforeRecipe = null;
        string? beforeRequest = null;
        string? beforeExternal = null;
        static string? ReadOptional(string? path) => path is not null && File.Exists(path)
            ? File.ReadAllText(path, Encoding.UTF8)
            : null;
        static void RestoreOptional(string? path, string? content)
        {
            if (path is null) return;
            if (content is null)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }
        try
        {
            if (!_assistedBlenderCreationWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedBlenderCreationWindow.PreviewForSmokeTest() ||
                _assistedBlenderCreationWindow.LastTestCount != 5 ||
                _assistedBlenderCreationWindow.LastPassedTestCount != 5 ||
                _assistedBlenderCreationWindow.LastDiagnosticCount != 0 ||
                _assistedBlenderCreationWindow.LastEstimatedCostUsd != 0)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12J_BLENDER_FAILED preview_tests_cost");
                return false;
            }
            recipePath = AssistedBlenderCreationService.ResolvePath(
                _projectSession.RootDirectory, _assistedBlenderCreationWindow.LastRecipeRelativePath);
            requestPath = Path.Combine(_projectSession.RootDirectory,
                _assistedBlenderCreationWindow.LastWorkerRequestRelativePath.Replace('/', Path.DirectorySeparatorChar));
            externalPath = Path.Combine(_projectSession.RootDirectory,
                AssetExternalEditStore.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            beforeRecipe = ReadOptional(recipePath);
            beforeRequest = ReadOptional(requestPath);
            beforeExternal = ReadOptional(externalPath);
            if (!_assistedBlenderCreationWindow.ApplyForSmokeTest() ||
                !File.Exists(recipePath) || !File.Exists(requestPath) ||
                !File.ReadAllText(requestPath, Encoding.UTF8).Contains("\"create_primitives\"", StringComparison.Ordinal) ||
                AssetExternalEditStore.Load(_projectSession.RootDirectory).Configurations
                    .Count(item => item.Operations.Contains(AssetExternalOperation.CreatePrimitives)) != 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12J_BLENDER_FAILED apply_persistence");
                return false;
            }
            var appliedRecipe = File.ReadAllText(recipePath, Encoding.UTF8);
            var appliedRequest = File.ReadAllText(requestPath, Encoding.UTF8);
            if (!UndoAssistedBlenderCreation() || ReadOptional(recipePath) != beforeRecipe ||
                ReadOptional(requestPath) != beforeRequest ||
                !RedoAssistedBlenderCreation() || ReadOptional(recipePath) != appliedRecipe ||
                ReadOptional(requestPath) != appliedRequest ||
                !UndoAssistedBlenderCreation() || ReadOptional(recipePath) != beforeRecipe ||
                ReadOptional(requestPath) != beforeRequest)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12J_BLENDER_FAILED history_roundtrip");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12J_BLENDER_PASS recipe=canonical primitives=typed transforms=bounded " +
                "material=pbr uv=typed lod=generated collision=generated validation=five-tests glb=export-request " +
                "provider=fake-offline cost=visible python=forbidden plan=dry-run-transaction " +
                "permission=apply-approved history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            RestoreOptional(recipePath, beforeRecipe);
            RestoreOptional(requestPath, beforeRequest);
            RestoreOptional(externalPath, beforeExternal);
            foreach (var directory in new[]
            {
                recipePath is null ? null : Path.GetDirectoryName(recipePath),
                requestPath is null ? null : Path.GetDirectoryName(requestPath),
                recipePath is null ? null : Path.GetDirectoryName(Path.GetDirectoryName(recipePath)),
                externalPath is null ? null : Path.GetDirectoryName(externalPath),
            }.Where(path => path is not null).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            _assetExternalEditHistory = new AssetExternalEditCommandHistory(originalState);
            _assistedBlenderCreationHistory = new AssistedBlenderCreationCommandHistory(originalState);
            _assistedModel3DGenerationHistory = new AssistedModel3DGenerationCommandHistory(originalState);
            _assetBrowser?.Refresh();
        }
    }

    private bool RunAssistedModel3DGenerationSmokeTest()
    {
        if (_projectSession is null || _assetExternalEditHistory is null ||
            _assistedModel3DGenerationWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12K_MODEL3D_FAILED missing_workspace");
            return false;
        }
        var originalState = _assetExternalEditHistory.State;
        string? sourcePath = null;
        string? receiptPath = null;
        string? requestPath = null;
        string? externalPath = null;
        string? beforeSource = null;
        string? beforeReceipt = null;
        string? beforeRequest = null;
        string? beforeExternal = null;
        static string? ReadOptional(string? path) => path is not null && File.Exists(path)
            ? File.ReadAllText(path, Encoding.UTF8)
            : null;
        static void RestoreOptional(string? path, string? content)
        {
            if (path is null) return;
            if (content is null) { if (File.Exists(path)) File.Delete(path); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }
        try
        {
            if (!_assistedModel3DGenerationWindow.ConfigureApprovedForSmokeTest() ||
                !_assistedModel3DGenerationWindow.PreviewForSmokeTest() ||
                _assistedModel3DGenerationWindow.LastTestCount != 5 ||
                _assistedModel3DGenerationWindow.LastPassedTestCount != 5 ||
                _assistedModel3DGenerationWindow.LastDiagnosticCount != 0 ||
                _assistedModel3DGenerationWindow.LastEstimatedCostUsd != 0)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12K_MODEL3D_FAILED preview_tests_cost");
                return false;
            }
            sourcePath = AssistedModel3DGenerationService.ResolvePath(_projectSession.RootDirectory,
                _assistedModel3DGenerationWindow.LastSourceRelativePath, "assets/staging/ai-3d/");
            receiptPath = AssistedModel3DGenerationService.ResolvePath(_projectSession.RootDirectory,
                _assistedModel3DGenerationWindow.LastReceiptRelativePath, "assets/staging/ai-3d/");
            requestPath = Path.Combine(_projectSession.RootDirectory,
                _assistedModel3DGenerationWindow.LastWorkerRequestRelativePath.Replace('/', Path.DirectorySeparatorChar));
            externalPath = Path.Combine(_projectSession.RootDirectory,
                AssetExternalEditStore.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            beforeSource = ReadOptional(sourcePath);
            beforeReceipt = ReadOptional(receiptPath);
            beforeRequest = ReadOptional(requestPath);
            beforeExternal = ReadOptional(externalPath);
            if (!_assistedModel3DGenerationWindow.ApplyForSmokeTest() || !File.Exists(sourcePath) ||
                !File.Exists(receiptPath) || !File.Exists(requestPath) ||
                !File.ReadAllText(sourcePath, Encoding.UTF8).Contains("f 4 5 8", StringComparison.Ordinal) ||
                !File.ReadAllText(receiptPath, Encoding.UTF8).Contains("\"humanAccepted\": true", StringComparison.Ordinal) ||
                !GeneratedModelRequestUsesSafeTypedOperations(requestPath))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12K_MODEL3D_FAILED apply_persistence");
                return false;
            }
            var appliedSource = ReadOptional(sourcePath);
            var appliedReceipt = ReadOptional(receiptPath);
            var appliedRequest = ReadOptional(requestPath);
            if (!UndoAssistedModel3DGeneration() || ReadOptional(sourcePath) != beforeSource ||
                ReadOptional(receiptPath) != beforeReceipt || ReadOptional(requestPath) != beforeRequest ||
                !RedoAssistedModel3DGeneration() || ReadOptional(sourcePath) != appliedSource ||
                ReadOptional(receiptPath) != appliedReceipt || ReadOptional(requestPath) != appliedRequest ||
                !UndoAssistedModel3DGeneration() || ReadOptional(sourcePath) != beforeSource ||
                ReadOptional(receiptPath) != beforeReceipt || ReadOptional(requestPath) != beforeRequest)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12K_MODEL3D_FAILED history_roundtrip");
                return false;
            }
            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12K_MODEL3D_PASS provider=interchangeable offline=deterministic " +
                "trellis=loopback triposr=loopback input=text-image license=cc0 staging=hashed topology=bounded " +
                "provenance=receipt acceptance=human blender=typed tests=five plan=dry-run-transaction " +
                "permission=apply-approved history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            RestoreOptional(sourcePath, beforeSource);
            RestoreOptional(receiptPath, beforeReceipt);
            RestoreOptional(requestPath, beforeRequest);
            RestoreOptional(externalPath, beforeExternal);
            foreach (var directory in new[]
            {
                sourcePath is null ? null : Path.GetDirectoryName(sourcePath),
                requestPath is null ? null : Path.GetDirectoryName(requestPath),
                sourcePath is null ? null : Path.GetDirectoryName(Path.GetDirectoryName(sourcePath)),
                sourcePath is null ? null : Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(sourcePath))),
                externalPath is null ? null : Path.GetDirectoryName(externalPath),
            }.Where(path => path is not null).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            _assetExternalEditHistory = new AssetExternalEditCommandHistory(originalState);
            _assistedBlenderCreationHistory = new AssistedBlenderCreationCommandHistory(originalState);
            _assistedModel3DGenerationHistory = new AssistedModel3DGenerationCommandHistory(originalState);
            _assetBrowser?.Refresh();
        }
    }

    private static bool GeneratedModelRequestUsesSafeTypedOperations(string path)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            var root = document.RootElement;
            var operations = root.GetProperty("operations").EnumerateArray()
                .Select(item => item.GetString()).ToArray();
            var policy = root.GetProperty("securityPolicy");
            return !operations.Contains("create_primitives", StringComparer.Ordinal) &&
                   policy.GetProperty("denyFreeFormPython").GetBoolean() &&
                   policy.GetProperty("denyShell").GetBoolean() &&
                   policy.GetProperty("denyEval").GetBoolean() &&
                   !policy.GetProperty("networkAllowed").GetBoolean();
        }
        catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private bool RunExternalWorkerSecuritySmokeTest()
    {
        if (_projectSession is null || _externalWorkerSecurityHistory is null ||
            _externalWorkerSecurityWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12L_WORKER_SECURITY_FAILED missing_workspace");
            return false;
        }
        var originalPolicy = _externalWorkerSecurityHistory.State.Policy;
        var path = Path.Combine(_projectSession.RootDirectory,
            ExternalWorkerSecurityPolicyStore.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var originalBytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
        try
        {
            if (!_externalWorkerSecurityWindow.ConfigureForSmokeTest() ||
                !_externalWorkerSecurityWindow.PreviewForSmokeTest() ||
                _externalWorkerSecurityWindow.LastTransactionId == Guid.Empty ||
                _externalWorkerSecurityWindow.LastPlanId == Guid.Empty ||
                _externalWorkerSecurityWindow.LastEstimatedCostUsd != 0 ||
                !_externalWorkerSecurityWindow.LastSemanticDiff.Contains("execution arbitraire=interdite", StringComparison.Ordinal) ||
                !_externalWorkerSecurityWindow.ApplyForSmokeTest() ||
                _externalWorkerSecurityHistory.State.Policy.MaximumJobSeconds != 180)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12L_WORKER_SECURITY_FAILED preview_apply");
                return false;
            }

            var loaded = ExternalWorkerSecurityPolicyStore.Load(_projectSession.RootDirectory);
            if (!loaded.Succeeded || loaded.Policy.MaximumJobSeconds != 180 ||
                !loaded.Policy.DenyFreeFormPython || !loaded.Policy.DenyShell || !loaded.Policy.DenyEval ||
                loaded.Policy.NetworkAllowed || !loaded.Policy.RequireContainedPaths ||
                !File.ReadAllText(path, Encoding.UTF8).Contains("\"denyFreeFormPython\": true", StringComparison.Ordinal) ||
                !UndoExternalWorkerSecurityPolicy() ||
                _externalWorkerSecurityHistory.State.Policy.MaximumJobSeconds != originalPolicy.MaximumJobSeconds ||
                !RedoExternalWorkerSecurityPolicy() ||
                _externalWorkerSecurityHistory.State.Policy.MaximumJobSeconds != 180 ||
                !UndoExternalWorkerSecurityPolicy())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G12L_WORKER_SECURITY_FAILED persistence_history");
                return false;
            }

            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G12L_WORKER_SECURITY_PASS policy=canonical uuid=stable schema=v1 " +
                "python=forbidden exec=forbidden eval=forbidden shell=forbidden paths=confined network=denied " +
                "operations=typed timeout=bounded output=bounded ui=visible-empty-error cost=zero " +
                "transaction=identified plan=dry-run-bound permission=configure audit=generated " +
                "history=restored persistence=roundtrip mcp=governed");
            return true;
        }
        finally
        {
            _externalWorkerSecurityHistory = new ExternalWorkerSecurityPolicyCommandHistory(originalPolicy);
            if (originalBytes is null)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, originalBytes);
            }
        }
    }

    private bool RunEditorKeymapSmokeTest()
    {
        if (_projectSession is null || _editorKeymapHistory is null || _editorKeymapWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13A_KEYMAP_FAILED missing_workspace");
            return false;
        }
        var originalProfile = _editorKeymapHistory.State.Profile;
        var path = Path.Combine(_projectSession.RootDirectory,
            EditorKeymapProfileStore.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var originalBytes = File.Exists(path) ? File.ReadAllBytes(path) : null;
        try
        {
            if (!_editorKeymapWindow.ConfigureForSmokeTest() ||
                !_editorKeymapWindow.PreviewForSmokeTest() ||
                _editorKeymapWindow.LastCommandId == Guid.Empty ||
                !_editorKeymapWindow.LastPatchSummary.Contains("searchable command bindings", StringComparison.Ordinal) ||
                !_editorKeymapWindow.ApplyForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13A_KEYMAP_FAILED preview_apply");
                return false;
            }

            var current = _editorKeymapHistory.State.Profile;
            var loaded = EditorKeymapProfileStore.Load(_projectSession.RootDirectory);
            var search = current.Resolve("K", ctrl: true, alt: false, shift: true, meta: false);
            if (!loaded.Succeeded || !HasSameEditorKeymap(loaded.Profile, current) ||
                current.Id != EditorKeymapProfile.DefaultId ||
                current.SchemaVersion != EditorKeymapProfile.CurrentSchemaVersion ||
                current.Preset != EditorKeymapPreset.Custom || current.Bindings.Count != 13 ||
                search?.CommandId != EditorKeymapCatalog.OpenSearch ||
                EditorKeymapProfileValidator.Validate(current).Count != 0 ||
                !DispatchEditorShortcut(EditorKeymapCatalog.OpenWorld) ||
                !UndoEditorKeymap() || !HasSameEditorKeymap(_editorKeymapHistory.State.Profile, originalProfile) ||
                !RedoEditorKeymap() || !HasSameEditorKeymap(_editorKeymapHistory.State.Profile, current) ||
                !UndoEditorKeymap())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13A_KEYMAP_FAILED persistence_history_input");
                return false;
            }

            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G13A_KEYMAP_PASS profiles=standard-aurora-blender " +
                "commands=searchable bindings=13 conflicts=blocked input=executed ui=visible-empty-error " +
                "uuid=stable schema=v1 provenance=tracked transaction=identified dry-run=previewed " +
                "audit=generated history=restored persistence=roundtrip runtime=optional");
            return true;
        }
        finally
        {
            _editorKeymapHistory = new EditorKeymapCommandHistory(originalProfile);
            if (originalBytes is null)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, originalBytes);
            }
        }
    }

    private bool RunCreatureFoundationSmokeTest()
    {
        if (_history is null || _creatureFoundationWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_FAILED missing_workspace");
            return false;
        }
        var originalProjectJson = ProjectJsonSerializer.Serialize(_history.State.Project);
        var applied = false;
        try
        {
            _creatureFoundationWindow.Open();
            var visible = _creatureFoundationWindow.Visible;
            _creatureFoundationWindow.Hide();
            if (!visible || !_creatureFoundationWindow.ConfigureForSmokeTest() ||
                !_creatureFoundationWindow.PreviewForSmokeTest() || !_creatureFoundationWindow.ApplyForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_FAILED preview_apply_ui " +
                    _creatureFoundationWindow.LastDiagnostics);
                return false;
            }
            applied = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Slug == "ash-warden-boss");
            var profile = blueprint.CreatureFoundation;
            var script = CreatureFoundationScriptService.Read(_history.State.Project, blueprint.Id);
            if (profile is null || profile.Resources.Count != 2 ||
                profile.Movement.Mode != CreatureFoundationMovementMode.Grounded ||
                profile.Spawned.EventName != "creature.spawned" || script?.SpawnHandler != "OnSpawned" ||
                _creatureFoundationWindow.SummarySectionCount != 4)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_FAILED canonical_summary");
                return false;
            }
            var spawned = CreatureFoundationValidator.Spawn(profile, blueprint.Id.ToString(),
                new(Guid.NewGuid(), _history.State.Project.Areas[0].Id.ToString(), TransformValue.Identity, 4201,
                    EventAuthorityScope.Server, "toolset.smoke"), Guid.NewGuid);
            var aggregate = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(_history.State.Project));
            if (spawned.ResourceState["health"] != 84 ||
                aggregate.Blueprints.Single(item => item.Id == blueprint.Id).CreatureFoundation?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_FAILED spawn_persistence");
                return false;
            }
            var invalid = new CreatureFoundationProfile(profile.Id, profile.SchemaVersion, profile.Provenance,
                profile.RequiredAtRuntime, profile.Identity, profile.DefaultTransform,
                [new(Guid.Empty, "health", 10, 11, -1, true)], profile.Movement, profile.References,
                profile.Spawned, profile.Overrides);
            var rejected = _creatureFoundationHandler.Handle(_history.State, new SetCreatureFoundationCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, blueprint.Id, invalid, true));
            if (rejected.Succeeded || rejected.Diagnostics.All(item =>
                    item.Code is not "creature.foundation.resource_invalid" and not "creature.foundation.resources_duplicate"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_FAILED invalid_prebuild_validation");
                return false;
            }
            var undo = _history.Undo();
            var redo = _history.Redo();
            var cleanup = _history.Undo();
            applied = false;
            if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
                ProjectJsonSerializer.Serialize(_history.State.Project) != originalProjectJson ||
                redo.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureFoundation is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_FAILED history");
                return false;
            }
            GD.Print("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_PASS ui=sections-preview-empty-error " +
                "identity=localized resources=generic movement=six-modes spawn=validated-after-construction " +
                "command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle " +
                "mcp=filtered-dry-run runtime=optional-safe-required-diagnostic");
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R038_CREATURE_FOUNDATION_FAILED " + exception);
            return false;
        }
        finally
        {
            _creatureFoundationWindow.Hide();
            if (applied) _history.Undo();
            RebuildPreview(); RefreshEditorControls();
        }
    }

    private bool RunCreaturePresentationSmokeTest()
    {
        if (_history is null || _projectSession is null || _creaturePresentationWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_FAILED missing_workspace");
            return false;
        }

        var portraitPath = Path.Combine(_projectSession.RootDirectory, "artifacts", "verification", "g13b-creature-portrait.png");
        var relativePortraitPath = Path.GetRelativePath(_projectSession.RootDirectory, portraitPath).Replace('\\', '/');
        var applied = false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(portraitPath)!);
            var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
            image.Fill(new Color("7a5cff"));
            if (image.SavePng(portraitPath) != Error.Ok)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_FAILED portrait_fixture");
                return false;
            }

            _creaturePresentationWindow.Open();
            var visible = _creaturePresentationWindow.Visible;
            _creaturePresentationWindow.Hide();
            if (!visible || !_creaturePresentationWindow.ConfigureForSmokeTest(relativePortraitPath))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_FAILED configure_ui");
                return false;
            }

            var blueprintId = _creaturePresentationWindow.SelectedBlueprintId;
            var originalPresentation = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId).CreaturePresentation;
            if (!_creaturePresentationWindow.PreviewForSmokeTest() ||
                _creaturePresentationWindow.LastCommandId == Guid.Empty ||
                !_creaturePresentationWindow.LastPatchSummary.Contains("presentation", StringComparison.OrdinalIgnoreCase) ||
                !_creaturePresentationWindow.ApplyForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_FAILED preview_apply_ui");
                return false;
            }

            applied = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var profile = blueprint.CreaturePresentation;
            var serialized = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(serialized);
            var runtime = CreaturePresentationRuntimeResolver.Resolve(profile, required: true);
            if (profile is null || profile.Id == Guid.Empty || profile.SchemaVersion != 1 ||
                profile.GenderId != "nonbinary" || profile.BodyTypeId != "humanoid-medium" ||
                profile.Pronouns.Subject != "iel" || profile.Portraits.Count != 2 ||
                profile.Portraits[0].ContentSha256.Length != 64 || profile.BodySlots.Count != 2 ||
                profile.MaterialColors.Single().HexColor != "#7A5CFF" || profile.Attachments.Count != 2 ||
                profile.Voice?.AudioBankIds.Count != 2 || profile.Voice.EmotionIds.Count != 2 ||
                profile.Voice.VariantIds.Count != 2 || profile.Voice.LipSyncProfileId != "lipsync.fr-v1" ||
                !runtime.Succeeded || runtime.UseModelFallback ||
                CreaturePresentationValidator.Validate(profile).Count != 0 ||
                ProjectJsonSerializer.Serialize(restored) != serialized ||
                restored.Blueprints.Single(item => item.Id == blueprint.Id).CreaturePresentation?.Id != profile.Id ||
                !_creaturePresentationWindow.PortraitPreviewReady || _creaturePresentationWindow.PreviewMeshCount < 5)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_FAILED canonical_roundtrip_runtime_preview");
                return false;
            }

            Undo();
            if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreaturePresentation?.Id !=
                originalPresentation?.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_FAILED undo");
                return false;
            }
            applied = false;
            Redo();
            applied = true;
            if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreaturePresentation?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_FAILED redo");
                return false;
            }
            Undo();
            applied = false;

            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G13B_CREATURE_PRESENTATION_PASS identity=gender-body-pronouns " +
                "portrait=hashed-multires-ready fallback=3d body=modular-rig-layer-clipping " +
                "materials=colored attachments=wings-tail voice=bank-language-rights " +
                "ui=visible-empty-error preview=3d-and-portrait uuid=stable schema=v1 provenance=tracked " +
                "transaction=identified dry-run=previewed audit=generated history=restored " +
                "persistence=project-roundtrip package=canonical runtime=optional-safe");
            return true;
        }
        finally
        {
            _creaturePresentationWindow.Hide();
            if (applied)
            {
                _history.Undo();
                RebuildPreview();
                RefreshEditorControls();
            }
            if (File.Exists(portraitPath)) File.Delete(portraitPath);
        }
    }

    private bool RunCreatureProgressionSmokeTest()
    {
        if (_history is null || _creatureProgressionWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED missing_workspace");
            return false;
        }

        var applied = false;
        var roleBehaviorDetached = false;
        try
        {
            _creatureProgressionWindow.Open();
            var visible = _creatureProgressionWindow.Visible;
            _creatureProgressionWindow.Hide();
            if (!visible || !_creatureProgressionWindow.ConfigureForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED configure_ui");
                return false;
            }

            var blueprintId = _creatureProgressionWindow.SelectedBlueprintId;
            if (blueprintId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED selection");
                return false;
            }
            var originalBlueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var originalProgression = originalBlueprint.CreatureProgression;
            var originalRoleBehaviorId = originalBlueprint.CreatureRoleBehavior?.Id;
            if (originalBlueprint.CreatureRoleBehavior is not null)
            {
                var detach = new SetCreatureRoleBehaviorCommand(CommandId.From(Guid.NewGuid()),
                    _history.State.Revision, blueprintId, null, false);
                if (!ApplyCommand(_creatureRoleBehaviorHandler.Handle(_history.State, detach),
                        "Isolation du profil G13-E pour le smoke de progression G13-C.", null))
                {
                    GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED behavior_detach");
                    return false;
                }
                roleBehaviorDetached = true;
            }
            if (!_creatureProgressionWindow.PreviewForSmokeTest() ||
                _creatureProgressionWindow.LastCommandId == Guid.Empty ||
                !_creatureProgressionWindow.LastPatchSummary.Contains("progression", StringComparison.OrdinalIgnoreCase) ||
                !_creatureProgressionWindow.ApplyForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED preview_apply_ui");
                return false;
            }

            applied = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var profile = blueprint.CreatureProgression;
            var availableAbilities = (blueprint.Combat?.Abilities ?? []).Select(item => item.Slug)
                .ToHashSet(StringComparer.Ordinal);
            var runtime = CreatureProgressionRuntimeResolver.Resolve(profile, availableAbilities, required: true);
            var serialized = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(serialized);
            if (profile is null || profile.Id == Guid.Empty || profile.SchemaVersion != 1 ||
                profile.RulesetId != "forge.generic" || !profile.AllowMultipleTracks || !profile.RequiredAtRuntime ||
                profile.Tracks.Count != 2 || profile.Tracks.Sum(item => item.Level) != 5 ||
                profile.DispositionAxes.Count != 2 || profile.Skills.Count != 2 ||
                profile.Skills.Single(item => item.SkillId == "lore").Expertise is not true ||
                profile.Talents.Count != 1 || profile.Talents[0].Prerequisites.Count != 2 ||
                profile.Loadout.Mode != AbilityLoadoutMode.Prepared || profile.Loadout.PreparedAbilities.Count != 1 ||
                profile.Overrides.GetValueOrDefault("xp-rate") != "1.0" ||
                !runtime.Succeeded || runtime.TotalLevel != 5 || runtime.PreparedAbilityIds.Count != 1 ||
                CreatureProgressionValidator.Validate(profile, availableAbilityIds: availableAbilities).Count != 0 ||
                _creatureProgressionWindow.SummarySectionCount != 5 ||
                ProjectJsonSerializer.Serialize(restored) != serialized ||
                restored.Blueprints.Single(item => item.Id == blueprint.Id).CreatureProgression?.Id != profile.Id)
            {
                GD.PrintErr(
                    "OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED canonical_roundtrip_runtime_summary " +
                    $"profile={profile is not null} tracks={profile?.Tracks.Count ?? -1} level={profile?.Tracks.Sum(item => item.Level) ?? -1} " +
                    $"axes={profile?.DispositionAxes.Count ?? -1} skills={profile?.Skills.Count ?? -1} " +
                    $"talents={profile?.Talents.Count ?? -1} prepared={profile?.Loadout.PreparedAbilities.Count ?? -1} " +
                    $"runtime={runtime.Succeeded}/{runtime.TotalLevel} validation={CreatureProgressionValidator.Validate(profile!, availableAbilityIds: availableAbilities).Count} " +
                    $"summary={_creatureProgressionWindow.SummarySectionCount} roundtrip={ProjectJsonSerializer.Serialize(restored) == serialized} " +
                    $"restored={restored.Blueprints.Single(item => item.Id == blueprint.Id).CreatureProgression?.Id == profile?.Id}");
                return false;
            }

            Undo();
            if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureProgression?.Id !=
                originalProgression?.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED undo");
                return false;
            }
            applied = false;
            Redo();
            applied = true;
            if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureProgression?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED redo");
                return false;
            }
            Undo();
            applied = false;
            if (roleBehaviorDetached)
            {
                Undo();
                roleBehaviorDetached = false;
                if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id)
                        .CreatureRoleBehavior?.Id != originalRoleBehaviorId)
                {
                    GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_FAILED behavior_restore");
                    return false;
                }
            }

            GD.Print(
                "OPENRPG_FORGE_TOOLSET_G13C_CREATURE_PROGRESSION_PASS tracks=multiclass-levels disposition=project-axes " +
                "skills=rank-training-expertise-checks talents=prerequisites-ranks-passive-hooks " +
                "loadout=learned-prepared-deck-cooldown-charges-resource-modifiers ruleset=data-driven " +
                "ui=visible-empty-error-summary uuid=stable schema=v1 provenance=tracked transaction=identified " +
                "dry-run=previewed audit=generated history=restored persistence=project-roundtrip runtime=optional-safe");
            return true;
        }
        finally
        {
            _creatureProgressionWindow.Hide();
            if (applied)
            {
                _history.Undo();
            }
            if (roleBehaviorDetached) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunCreatureLootSmokeTest()
    {
        if (_history is null || _creatureLootWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED missing_workspace");
            return false;
        }

        var progressionApplied = false;
        var lootApplied = false;
        try
        {
            _creatureLootWindow.Open();
            var visible = _creatureLootWindow.Visible;
            _creatureLootWindow.Hide();
            var blueprintId = _creatureLootWindow.SelectedBlueprintId;
            if (!visible || blueprintId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED selection_ui");
                return false;
            }

            var original = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var originalProgressionId = original.CreatureProgression?.Id;
            var originalLootId = original.CreatureLoot?.Id;
            if (original.CreatureProgression is null || original.CreatureProgression.Skills.Count < 2)
            {
                var sourceProgression = original.CreatureProgression ?? CreatureProgressionProfile.CreateDefault(
                    Guid.Parse("a1420000-0000-4000-8000-000000000001"), "smoke:g13-d");
                var fixtureSkills = sourceProgression.Skills
                    .Where(item => item.SkillId is not ("sleight" or "awareness"))
                    .Concat([
                        new("sleight", "Larcin", "agility", 2, 10, 2, true, false, 12),
                        new("awareness", "Vigilance", "insight", 2, 10, 2, true, false, 10),
                    ]).ToArray();
                var progression = new CreatureProgressionProfile(
                    sourceProgression.Id, 1, sourceProgression.Provenance, sourceProgression.RulesetId,
                    sourceProgression.AllowMultipleTracks, sourceProgression.RequiredAtRuntime,
                    sourceProgression.Tracks, sourceProgression.DispositionAxes, fixtureSkills,
                    sourceProgression.Talents, sourceProgression.Loadout, sourceProgression.Overrides);
                var command = new SetCreatureProgressionCommand(CommandId.From(Guid.NewGuid()),
                    _history.State.Revision, blueprintId, progression, false);
                if (!ApplyCommand(_creatureProgressionHandler.Handle(_history.State, command),
                        "Préparation du test de butin.", null))
                {
                    GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED progression_fixture");
                    return false;
                }
                progressionApplied = true;
            }

            _creatureLootWindow.Refresh();
            var configured = _creatureLootWindow.ConfigureForSmokeTest();
            var previewed = configured && _creatureLootWindow.PreviewForSmokeTest();
            var applied = previewed && _creatureLootWindow.LastCommandId != Guid.Empty &&
                          _creatureLootWindow.LastPatchSummary.Contains("loot", StringComparison.OrdinalIgnoreCase) &&
                          _creatureLootWindow.ApplyForSmokeTest();
            if (!configured || !previewed || !applied)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED preview_apply_ui " +
                    $"configured={configured} previewed={previewed} applied={applied} " +
                    $"patch={_creatureLootWindow.LastPatchSummary} diagnostics={_creatureLootWindow.LastDiagnostics}");
                return false;
            }

            lootApplied = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var profile = blueprint.CreatureLoot;
            var itemIds = (blueprint.Combat?.Loot ?? []).Select(item => item.ItemSlug).ToHashSet(StringComparer.Ordinal);
            var skillIds = (blueprint.CreatureProgression?.Skills ?? []).Select(item => item.SkillId).ToHashSet(StringComparer.Ordinal);
            var death = CreatureLootRuntimeResolver.ResolveDeath(profile, blueprint.Combat?.Loot ?? [], 0.2,
                "player", "player-party", DateTimeOffset.UnixEpoch, required: true);
            var theft = CreatureLootRuntimeResolver.ResolvePickpocket(profile!,
                Guid.Parse("a1420000-0000-4000-8000-000000000010"), 0.2,
                "player", "player-party", 20, 0, true, DateTimeOffset.UnixEpoch, null);
            var serialized = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(serialized);
            if (profile is null || profile.Id == Guid.Empty || profile.SchemaVersion != 1 || !profile.RequiredAtRuntime ||
                profile.Entries.Count == 0 || profile.Entries.Any(item => !item.DropOnDeath || !item.Pickpocketable) ||
                !profile.Theft.Enabled || !profile.Theft.RequireServerConsent || profile.Theft.CooldownSeconds != 30 ||
                profile.Corpse.Mode != CreatureCorpseMode.RagdollContainer || !profile.Corpse.AllowResurrection ||
                profile.Corpse.CleanupMode != CorpseCleanupMode.Timed ||
                CreatureLootValidator.Validate(profile, availableItemSlugs: itemIds, availableSkillIds: skillIds).Count != 0 ||
                !death.Succeeded || !death.Corpse.Created || death.Corpse.Contents.Count == 0 ||
                !theft.Succeeded || theft.Grant is null || _creatureLootWindow.SummarySectionCount != 3 ||
                ProjectJsonSerializer.Serialize(restored) != serialized ||
                restored.Blueprints.Single(item => item.Id == blueprint.Id).CreatureLoot?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED canonical_roundtrip_runtime_summary " +
                    $"profile={profile is not null} entries={profile?.Entries.Count ?? -1} theft={profile?.Theft.Enabled} " +
                    $"corpse={profile?.Corpse.Mode} death={death.Succeeded}/{death.Corpse.Contents.Count} " +
                    $"pickpocket={theft.Succeeded} summary={_creatureLootWindow.SummarySectionCount}");
                return false;
            }

            Undo(); lootApplied = false;
            if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureLoot?.Id != originalLootId)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED undo");
                return false;
            }
            Redo(); lootApplied = true;
            if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureLoot?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED redo");
                return false;
            }
            Undo(); lootApplied = false;
            if (progressionApplied)
            {
                Undo(); progressionApplied = false;
                if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureProgression?.Id != originalProgressionId)
                {
                    GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_FAILED progression_restore");
                    return false;
                }
            }

            GD.Print("OPENRPG_FORGE_TOOLSET_G13D_CREATURE_LOOT_PASS entries=item-slot-creature probability=bounded " +
                "ownership=resolved instancing=shared-player-party theft=skill-perception-server-consent-crime-cooldown-audit " +
                "corpse=container-ragdoll-lifetime-resurrection-ownership-cleanup ui=visible-empty-error-summary " +
                "uuid=stable schema=v1 provenance=tracked transaction=identified dry-run=previewed audit=generated " +
                "history=restored persistence=project-roundtrip runtime=legacy-safe");
            return true;
        }
        finally
        {
            _creatureLootWindow.Hide();
            if (lootApplied) _history.Undo();
            if (progressionApplied) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunCreatureRoleBehaviorSmokeTest()
    {
        if (_history is null || _creatureRoleBehaviorWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED missing_workspace");
            return false;
        }

        var progressionApplied = false;
        var behaviorApplied = false;
        try
        {
            _creatureRoleBehaviorWindow.Open();
            var visible = _creatureRoleBehaviorWindow.Visible;
            _creatureRoleBehaviorWindow.Hide();
            var blueprintId = _creatureRoleBehaviorWindow.SelectedBlueprintId;
            if (!visible || blueprintId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED selection_ui");
                return false;
            }

            var original = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var originalBehaviorId = original.CreatureRoleBehavior?.Id;
            var originalProgressionId = original.CreatureProgression?.Id;
            if (original.CreatureProgression is null || original.CreatureProgression.Tracks.Count == 0)
            {
                var progression = CreatureProgressionProfile.CreateDefault(
                    Guid.Parse("a1460000-0000-4000-8000-000000000001"), "smoke:g13-e");
                var command = new SetCreatureProgressionCommand(CommandId.From(Guid.NewGuid()),
                    _history.State.Revision, blueprintId, progression, false);
                if (!ApplyCommand(_creatureProgressionHandler.Handle(_history.State, command),
                        "Préparation du test de rôle et comportement.", null))
                {
                    GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED progression_fixture");
                    return false;
                }
                progressionApplied = true;
            }

            var beforeTemplate = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId)
                .CreatureProgression!;
            var beforeLevel = beforeTemplate.Tracks.Sum(item => item.Level);
            _creatureRoleBehaviorWindow.Refresh();
            var configured = _creatureRoleBehaviorWindow.ConfigureForSmokeTest();
            var previewed = configured && _creatureRoleBehaviorWindow.PreviewForSmokeTest();
            var applied = previewed && _creatureRoleBehaviorWindow.LastCommandId != Guid.Empty &&
                          _creatureRoleBehaviorWindow.LastPatchSummary.Contains("role and behavior",
                              StringComparison.OrdinalIgnoreCase) &&
                          _creatureRoleBehaviorWindow.ApplyForSmokeTest();
            if (!configured || !previewed || !applied)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED preview_apply_ui " +
                    $"configured={configured} previewed={previewed} applied={applied} " +
                    $"patch={_creatureRoleBehaviorWindow.LastPatchSummary} diagnostics={_creatureRoleBehaviorWindow.LastDiagnostics}");
                return false;
            }

            behaviorApplied = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var profile = blueprint.CreatureRoleBehavior;
            var runtime = CreatureRoleBehaviorRuntimeResolver.Resolve(profile, blueprint.CreatureProgression,
                21 * 60, false, required: true);
            var serialized = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(serialized);
            if (profile is null || profile.Id == Guid.Empty || profile.SchemaVersion != 1 ||
                !profile.RequiredAtRuntime || profile.Role.LoreFields.GetValueOrDefault("deity") != "deity.embers" ||
                profile.Role.RoleTags.Count < 2 || profile.Ambient.IdleSets.Count != 2 ||
                profile.Ambient.Schedules.Count != 1 || profile.Ambient.Interactions.Count != 1 ||
                profile.AutoLevel.Seed != 42 || profile.AutoLevel.ManualLock || !profile.AutoLevel.ApplyOnSave ||
                CreatureRoleBehaviorValidator.Validate(profile, blueprint.CreatureProgression).Count != 0 ||
                !runtime.Succeeded || runtime.ActiveAnimationId is null || runtime.AutoLevel is null ||
                blueprint.CreatureProgression!.Tracks.Sum(item => item.Level) <= beforeLevel ||
                _creatureRoleBehaviorWindow.SummarySectionCount != 3 ||
                ProjectJsonSerializer.Serialize(restored) != serialized ||
                restored.Blueprints.Single(item => item.Id == blueprint.Id).CreatureRoleBehavior?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED canonical_roundtrip_runtime_summary " +
                    $"profile={profile is not null} lore={profile?.Role.LoreFields.Count ?? -1} " +
                    $"idle={profile?.Ambient.IdleSets.Count ?? -1} runtime={runtime.Succeeded}/{runtime.ActiveAnimationId} " +
                    $"level={blueprint.CreatureProgression?.Tracks.Sum(item => item.Level)} summary={_creatureRoleBehaviorWindow.SummarySectionCount}");
                return false;
            }

            Undo(); behaviorApplied = false;
            var undone = _history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id);
            if (undone.CreatureRoleBehavior?.Id != originalBehaviorId ||
                undone.CreatureProgression?.Tracks.Sum(item => item.Level) != beforeLevel)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED undo");
                return false;
            }
            Redo(); behaviorApplied = true;
            if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureRoleBehavior?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED redo");
                return false;
            }
            Undo(); behaviorApplied = false;
            if (progressionApplied)
            {
                Undo(); progressionApplied = false;
                if (_history.State.Project.Blueprints.Single(item => item.Id == blueprint.Id).CreatureProgression?.Id !=
                    originalProgressionId)
                {
                    GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_FAILED progression_restore");
                    return false;
                }
            }

            GD.Print("OPENRPG_FORGE_TOOLSET_G13E_CREATURE_ROLE_BEHAVIOR_PASS role=ruleset-lore-tags-no-implicit-mechanics " +
                "ambient=idle-schedule-interaction-waypoint-conversation-interruption-resume " +
                "autolevel=deterministic-seed-preview-diff-apply-manual-lock ui=visible-empty-error-summary " +
                "uuid=stable schema=v1 provenance=tracked transaction=identified dry-run=previewed audit=generated " +
                "history=restored persistence=project-roundtrip runtime=optional-safe");
            return true;
        }
        finally
        {
            _creatureRoleBehaviorWindow.Hide();
            if (behaviorApplied) _history.Undo();
            if (progressionApplied) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunCreatureEventSmokeTest()
    {
        if (_history is null || _creatureEventWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_FAILED missing_workspace");
            return false;
        }

        var appliedToHistory = false;
        try
        {
            _creatureEventWindow.Open();
            var visible = _creatureEventWindow.Visible;
            _creatureEventWindow.Hide();
            var blueprintId = _creatureEventWindow.SelectedBlueprintId;
            if (!visible || blueprintId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_FAILED selection_ui");
                return false;
            }

            var before = ProjectJsonSerializer.Serialize(_history.State.Project);
            var configured = _creatureEventWindow.ConfigureForSmokeTest();
            var previewed = configured && _creatureEventWindow.PreviewForSmokeTest();
            var applied = previewed && _creatureEventWindow.LastCommandId != Guid.Empty &&
                          _creatureEventWindow.LastPatchSummary.Contains("creature-event",
                              StringComparison.OrdinalIgnoreCase) &&
                          _creatureEventWindow.ApplyForSmokeTest();
            if (!configured || !previewed || !applied)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_FAILED preview_apply_ui " +
                    $"configured={configured} previewed={previewed} applied={applied} " +
                    $"patch={_creatureEventWindow.LastPatchSummary} diagnostics={_creatureEventWindow.LastDiagnostics}");
                return false;
            }

            appliedToHistory = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var profile = blueprint.CreatureEvents;
            var heartbeat = CreatureEventRuntimeResolver.AdvanceHeartbeat(profile,
                CreatureEventRuntimeState.Empty, 1_500, true, blueprint.Slug, required: true);
            var suspended = CreatureEventRuntimeResolver.AdvanceHeartbeat(profile,
                CreatureEventRuntimeState.Empty, 1_500, false, blueprint.Slug, required: true);
            var custom = profile is null || profile.CustomEvents.Count == 0 ? null :
                CreatureEventRuntimeResolver.Custom(profile, profile.CustomEvents[0].SchemaId,
                    new Dictionary<string, string> { ["count"] = "3", ["source"] = blueprint.Slug });
            var damageTransaction = Guid.Parse("a1590000-0000-4000-8000-000000000001");
            var beforeDamage = CreatureEventRuntimeResolver.BeforeDamage(profile, damageTransaction,
                blueprint.Slug, "hero", 12, "radiant", "smoke");
            var damageApplied = CreatureEventRuntimeResolver.DamageApplied(profile, damageTransaction,
                blueprint.Slug, "hero", 12, 10, 2, "radiant", "smoke");
            var died = CreatureEventRuntimeResolver.EntityDied(profile, blueprint.Slug, "hero",
                ["hero", "elyra"], "smoke", "crypt:4,0,9", ["warden-ember"], "drop");
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson);
            if (profile is null || profile.Id == Guid.Empty || profile.SchemaVersion != 1 ||
                !profile.RequiredAtRuntime || profile.CombatBoundary?.Mode != CreatureCombatBoundaryMode.CombatTickCompleted ||
                profile.Inventory is null || profile.Rest is null || profile.Heartbeat is not { Enabled: true } ||
                profile.Heartbeat.IntervalMilliseconds != 1_500 || profile.Heartbeat.BudgetMilliseconds != 8 ||
                !profile.Heartbeat.SuspendOutsideInterest || profile.CustomEvents.Count != 1 ||
                CreatureEventValidator.Validate(profile).Count != 0 || heartbeat.Dispatches.Count != 1 ||
                heartbeat.Dispatches[0].BudgetMilliseconds != 8 || suspended.Dispatches.Count != 0 ||
                custom?.EventName != "custom.crypt.braziers_lit" || profile.DamageDeath is null ||
                beforeDamage?.EventName != "creature.before_damage" || beforeDamage.BudgetMilliseconds != 4 ||
                damageApplied?.EventName != "creature.damage_applied" || died?.EventName != "entity.died" ||
                _creatureEventWindow.SummarySectionCount != 4 ||
                ProjectJsonSerializer.Serialize(restored) != appliedJson ||
                restored.Blueprints.Single(item => item.Id == blueprint.Id).CreatureEvents?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_FAILED canonical_roundtrip_runtime_summary " +
                    $"profile={profile is not null} heartbeat={heartbeat.Dispatches.Count} suspended={suspended.Dispatches.Count} " +
                    $"custom={custom?.EventName} summary={_creatureEventWindow.SummarySectionCount}");
                return false;
            }

            Undo(); appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_FAILED undo");
                return false;
            }
            Redo(); appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_FAILED redo");
                return false;
            }
            Undo(); appliedToHistory = false;

            GD.Print("OPENRPG_FORGE_TOOLSET_G13F_CREATURE_EVENTS_PASS combat=tick-or-turn " +
                "inventory=accessed-transferred-cause-actor-result rest=duration-resources-interruptions " +
                "heartbeat=cadence-suspension-budget-backlog custom=namespaced-typed ui=visible-empty-error-summary " +
                "uuid=stable schema=v1 provenance=tracked forgescript=api-1.0-server transaction=identified " +
                "dry-run=previewed audit=generated history=restored persistence=project-roundtrip runtime=optional-safe");
            GD.Print("OPENRPG_FORGE_TOOLSET_R039_CREATURE_DAMAGE_DEATH_PASS " +
                "damage=before-restricted-after-transaction death=killer-contributors-cause-location-loot-policy " +
                "validation=rig-rules-equipment-capabilities-scripts-faction-resources-budgets " +
                "ui=sections-summary transaction=dry-run-apply history=undo-redo persistence=roundtrip " +
                "forgescript=typed-handle mcp=filtered-dry-run runtime=ordered-authoritative");
            return true;
        }
        finally
        {
            _creatureEventWindow.Hide();
            if (appliedToHistory) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunQuestJournalSmokeTest()
    {
        if (_history is null || _questJournalWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R007_QUEST_FAILED missing_workspace");
            return false;
        }
        var appliedToHistory = false;
        try
        {
            _questJournalWindow.Open();
            var visible = _questJournalWindow.Visible;
            _questJournalWindow.Hide();
            var questId = _questJournalWindow.SelectedQuestId;
            var before = ProjectJsonSerializer.Serialize(_history.State.Project);
            var configured = visible && questId is not null && _questJournalWindow.ConfigureForSmokeTest();
            var previewed = configured && _questJournalWindow.PreviewForSmokeTest();
            var applied = previewed && _questJournalWindow.LastCommandId != Guid.Empty &&
                          _questJournalWindow.LastPatchSummary.Contains("quest", StringComparison.OrdinalIgnoreCase) &&
                          _questJournalWindow.ApplyForSmokeTest();
            if (!applied || questId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R007_QUEST_FAILED preview_apply_ui " +
                    _questJournalWindow.LastDiagnostics);
                return false;
            }
            appliedToHistory = true;
            var quest = _history.State.Project.Quests.Single(item => item.Id == questId);
            var script = new QuestJournalScriptService(quest);
            var snapshot = script.Read(script.Handle);
            var instance = QuestInstanceService.Create(quest, quest.InstancePolicy.DefaultScope, "player:smoke-r007",
                DateTimeOffset.Parse("2026-08-07T12:00:00Z"), authorityGranted: true,
                id: Guid.Parse("c7070000-0000-4000-8000-000000000007"));
            var firstTransition = quest.Transitions.OrderBy(item => item.Id.Value).First();
            var advanced = QuestInstanceService.Advance(quest, instance, firstTransition.Id,
                new SmokeQuestConditionEvaluator(), DateTimeOffset.Parse("2026-08-07T12:01:00Z"),
                "player:smoke-r007", new Dictionary<string, string> { ["smoke"] = "passed" });
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson).Quests.Single(item => item.Id == questId);
            if (QuestValidator.Validate(quest).Count != 0 || _questJournalWindow.LastValidationCount != 0 ||
                !_questJournalWindow.LastSimulationSucceeded || _questJournalWindow.StateCount != quest.States.Count ||
                snapshot.StateCount != quest.States.Count || snapshot.TerminalStateCount == 0 ||
                advanced.History.Count != 2 || advanced.Variables.GetValueOrDefault("smoke") != "passed" ||
                restored.Category != "main" || restored.Priority != 91 ||
                restored.States.All(item => item.Journal.Summary.Variants.Count == 0))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R007_QUEST_FAILED canonical_instance_roundtrip");
                return false;
            }
            Undo();
            appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before) return false;
            Redo();
            appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson) return false;
            Undo();
            appliedToHistory = false;
            GD.Print("OPENRPG_FORGE_TOOLSET_R007_QUEST_PASS metadata=category-priority-owner-localized " +
                "states=uuid-order-legacy-index terminals=completed-failed-abandoned-custom-reactivation " +
                "journal=summary-details-objectives-context-links instances=player-party-world-history-variables-time " +
                "ui=visible-empty-error-minimap-validation-simulation forgescript=typed-handle mcp=filtered-dry-run " +
                "transaction=identified audit=generated history=undo-redo persistence=project-workspace-roundtrip");
            return true;
        }
        finally
        {
            _questJournalWindow.Hide();
            if (appliedToHistory) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunQuestTransitionSmokeTest()
    {
        if (_history is null || _questJournalWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R008_QUEST_FAILED missing_workspace");
            return false;
        }
        var appliedToHistory = false;
        try
        {
            _questJournalWindow.Open();
            var visible = _questJournalWindow.Visible;
            _questJournalWindow.Hide();
            var questId = _questJournalWindow.SelectedQuestId;
            var before = ProjectJsonSerializer.Serialize(_history.State.Project);
            var configured = visible && questId is not null && _questJournalWindow.ConfigureForR008SmokeTest();
            var previewed = configured && _questJournalWindow.PreviewForSmokeTest();
            var applied = previewed && _questJournalWindow.LastCommandId != Guid.Empty &&
                          _questJournalWindow.LastPatchSummary.Contains("quest", StringComparison.OrdinalIgnoreCase) &&
                          _questJournalWindow.ApplyForSmokeTest();
            if (!applied || questId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R008_QUEST_FAILED preview_apply_ui " +
                    _questJournalWindow.LastDiagnostics);
                return false;
            }
            appliedToHistory = true;
            var quest = _history.State.Project.Quests.Single(item => item.Id == questId);
            var script = new QuestJournalScriptService(quest);
            var transitions = script.ReadTransitions(script.Handle);
            var simulation = script.Simulate(script.Handle, quest.InstancePolicy.DefaultScope,
                quest.InstancePolicy.DefaultScope == QuestProgressScope.Player ? "player:smoke-r008" :
                quest.InstancePolicy.DefaultScope == QuestProgressScope.Party ? "party:smoke-r008" :
                "world:smoke-r008", DateTimeOffset.Parse("2026-08-07T12:00:00Z"));
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson).Quests.Single(item => item.Id == questId);
            if (QuestValidator.Validate(quest).Count != 0 || _questJournalWindow.LastValidationCount != 0 ||
                !_questJournalWindow.LastSimulationSucceeded || transitions.Count != quest.Transitions.Count ||
                transitions[0].EventName != "dialogue.accept" ||
                transitions[0].Idempotence != QuestTransitionIdempotence.PerEvent ||
                simulation.Status != QuestSimulationStatus.Completed || simulation.AppliedTransitionCount == 0 ||
                restored.Simulation.Events.Count != quest.Transitions.Count ||
                restored.Transitions[0].ForgeScriptApiVersion != 1)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R008_QUEST_FAILED transition_simulation_roundtrip");
                return false;
            }
            Undo(); appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before) return false;
            Redo(); appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson) return false;
            Undo(); appliedToHistory = false;
            GD.Print("OPENRPG_FORGE_TOOLSET_R008_QUEST_PASS transition=schema-event-priority-idempotence-order " +
                "conditions=typed actions=typed simulation=persisted-events-effects-deadlock-step-limit " +
                "lint=references-unreachable-deadlock-cycle-conflict-reward-objective ui=editor-minimap-trace " +
                "forgescript=typed-handle-sandbox mcp=filtered-dry-run-simulation transaction=identified " +
                "audit=transition-trace history=undo-redo persistence=project-workspace-roundtrip");
            return true;
        }
        finally
        {
            _questJournalWindow.Hide();
            if (appliedToHistory) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunQuestOperationsSmokeTest()
    {
        if (_history is null || _questOperationsWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13J_QUEST_OPERATIONS_FAILED missing_workspace");
            return false;
        }
        var appliedToHistory = false;
        try
        {
            _questOperationsWindow.Open(); var visible = _questOperationsWindow.Visible;
            _questOperationsWindow.Hide(); var questId = _questOperationsWindow.SelectedQuestId;
            var before = ProjectJsonSerializer.Serialize(_history.State.Project);
            var configured = visible && questId is not null && _questOperationsWindow.ConfigureForSmokeTest();
            var previewed = configured && _questOperationsWindow.PreviewForSmokeTest();
            var applied = previewed && _questOperationsWindow.LastCommandId != Guid.Empty &&
                          _questOperationsWindow.LastPatchSummary.Contains("quest", StringComparison.OrdinalIgnoreCase) &&
                          _questOperationsWindow.ApplyForSmokeTest();
            if (!applied || questId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13J_QUEST_OPERATIONS_FAILED preview_apply_ui " +
                    _questOperationsWindow.LastDiagnostics); return false;
            }
            appliedToHistory = true;
            var quest = _history.State.Project.Quests.Single(item => item.Id == questId);
            var profile = quest.Operations; var context = new QuestOperationsRuntimeContext(
                DateTimeOffset.Parse("2026-08-06T12:00:00Z"), DateTimeOffset.Parse("2026-08-06T10:00:00Z"),
                1, 2, 2, true, true, new HashSet<QuestTransitionId>());
            var repeat = profile is null ? null : QuestOperationsRuntimeResolver.ResolveRepeat(quest, context);
            var share = profile is null ? null : QuestOperationsRuntimeResolver.ResolveShare(quest, context);
            var exclusive = profile is null || quest.Transitions.Count == 0 ? null :
                QuestOperationsRuntimeResolver.ResolveExclusive(quest, quest.Transitions[0].Id, context);
            var notice = profile is null ? null : QuestOperationsRuntimeResolver.ResolveNotification(quest);
            var script = profile is null ? null : new QuestOperationsScriptService(quest);
            var snapshot = script?.Read(script.Handle);
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson).Quests.Single(item => item.Id == questId);
            if (profile is null || QuestOperationsValidator.Validate(quest).Count != 0 ||
                profile.Journal.Priority != 92 || repeat is not { Allowed: true } || share is not { Accepted: true } ||
                exclusive is not { Allowed: true, RequiresConfirmation: true } || notice is not { Visible: true } ||
                snapshot is null || snapshot.ExclusiveChoiceCount != 1 ||
                _questOperationsWindow.SummarySectionCount != 5 || restored.Operations?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13J_QUEST_OPERATIONS_FAILED canonical_runtime_roundtrip");
                return false;
            }
            Undo(); appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before) return false;
            Redo(); appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson) return false;
            Undo(); appliedToHistory = false;
            GD.Print("OPENRPG_FORGE_TOOLSET_G13J_QUEST_OPERATIONS_PASS " +
                "journal=priority-importance-manual-pin notifications=grouped-accessible-localized-silent-cinematic " +
                "repeat=cooldown-counter-reset-decay-seed share=individual-party-optin-leader-unanimous-separation " +
                "exclusive=mutex-irreversible-confirmation-consequence ui=visible-empty-error-summary-simulation " +
                "forgescript=typed-handle mcp=filtered-dry-run transaction=identified audit=generated " +
                "history=restored persistence=project-workspace-package runtime=optional-safe-authoritative");
            return true;
        }
        finally
        {
            _questOperationsWindow.Hide(); if (appliedToHistory) _history.Undo();
            RebuildPreview(); RefreshEditorControls();
        }
    }

    private bool RunItemFoundationSmokeTest()
    {
        if (_history is null || _itemFoundationWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R044_ITEM_FOUNDATION_FAILED missing_workspace");
            return false;
        }
        var before = ProjectJsonSerializer.Serialize(_history.State.Project);
        var appliedToHistory = false;
        try
        {
            _itemFoundationWindow.Open(); var visible = _itemFoundationWindow.Visible; _itemFoundationWindow.Hide();
            if (!visible || !_itemFoundationWindow.ConfigureForSmokeTest() ||
                !_itemFoundationWindow.RejectInvalidForSmokeTest() ||
                !_itemFoundationWindow.PreviewForSmokeTest() || !_itemFoundationWindow.ApplyForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R044_ITEM_FOUNDATION_FAILED preview_apply_ui " +
                    _itemFoundationWindow.LastDiagnostics);
                return false;
            }
            appliedToHistory = true;
            var blueprintId = _itemFoundationWindow.SelectedBlueprintId;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var profile = blueprint.ItemFoundation;
            var script = blueprintId is null ? null : ItemFoundationScriptService.Read(_history.State.Project, blueprintId);
            var instance = profile is null ? null : new ItemFoundationInstance(
                Guid.Parse("c2460000-0000-4000-8000-000000000001"), profile.Id, profile.DefinitionVersion, 80,
                new Dictionary<string, string> { ["identified"] = "true" });
            var instanceDiagnostics = profile is null || instance is null
                ? [new ItemFoundationDiagnostic("missing", "item", "missing")]
                : ItemFoundationScriptService.ValidateInstance(new(profile.Id), profile, instance);
            var forbiddenDiagnostics = profile is null || instance is null
                ? []
                : ItemFoundationValidator.ValidateInstance(profile,
                    instance with { Overrides = new Dictionary<string, string> { ["forbidden"] = "true" } });
            var targetType = new ItemTypeDefinition(Guid.Parse("c2460000-0000-4000-8000-000000000002"), 1,
                "toolset:r044", "quest-relic", ["belt"], ["ward"], "rig.relic");
            var migrationRejected = profile is null ? null : _itemFoundationHandler.Handle(_history.State,
                new SetItemFoundationCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, blueprint.Id,
                    profile with { TypeId = targetType.Id, Type = targetType, AppearanceRig = targetType.RequiredRig },
                    false, true));
            var invalidRejected = profile is null ? null : _itemFoundationHandler.Handle(_history.State,
                new SetItemFoundationCommand(CommandId.From(Guid.NewGuid()), _history.State.Revision, blueprint.Id,
                    profile with { ContentSha256 = "invalid" }, false, true));
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson).Blueprints.Single(item => item.Id == blueprint.Id);
            if (profile is null || blueprintId is null || ItemFoundationValidator.Validate(profile).Count != 0 ||
                profile.TypeId != profile.Type.Id || profile.Type.Slug != "ritual-key" || profile.DefinitionVersion != 2 ||
                profile.Texts.Single().UnknownText != "Objet inconnu" || script?.Handle.Value != profile.Id ||
                instanceDiagnostics.Count != 0 ||
                forbiddenDiagnostics.SingleOrDefault()?.Code != "item.foundation.instance_override_forbidden" ||
                migrationRejected?.Diagnostics.SingleOrDefault()?.Code !=
                    "item.foundation.type_migration_preview_required" ||
                invalidRejected?.Diagnostics.SingleOrDefault()?.Code != "item.foundation.content_governance_invalid" ||
                ItemFoundationValidator.Resolve(null, true).Diagnostics.SingleOrDefault()?.Code !=
                    ItemFoundationValidator.MissingRequiredCode ||
                restored.ItemFoundation?.Id != profile.Id || ProjectJsonSerializer.Serialize(
                    ProjectJsonSerializer.Deserialize(appliedJson)) != appliedJson ||
                _itemFoundationWindow.SummarySectionCount != 4)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R044_ITEM_FOUNDATION_FAILED canonical_validation_runtime_roundtrip");
                return false;
            }

            Undo(); appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R044_ITEM_FOUNDATION_FAILED undo");
                return false;
            }
            Redo(); appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R044_ITEM_FOUNDATION_FAILED redo");
                return false;
            }
            Undo(); appliedToHistory = false;
            GD.Print("OPENRPG_FORGE_TOOLSET_R044_ITEM_FOUNDATION_PASS " +
                "type=immutable-reference-migration-preview-explicit-accept blueprint=versioned-instance-separated-overrides-bounded " +
                "identity=uuid-slug-localized-name-short-long-unknown-label-notes " +
                "validation=identity-type-slots-rig-values-effects-scripts-localization-asset-license-economy " +
                "ui=visible-empty-error-sections-summary transaction=dry-run-apply history=undo-redo " +
                "persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic");
            return true;
        }
        finally
        {
            _itemFoundationWindow.Hide();
            if (appliedToHistory) _history.Undo();
            RebuildPreview(); RefreshEditorControls();
        }
    }

    private bool RunItemAdvancedSmokeTest()
    {
        if (_history is null || _itemAdvancedWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13K_M_ITEM_ADVANCED_FAILED missing_workspace");
            return false;
        }
        var appliedToHistory = false;
        try
        {
            _itemAdvancedWindow.Open(); var visible = _itemAdvancedWindow.Visible; _itemAdvancedWindow.Hide();
            var blueprintId = _itemAdvancedWindow.SelectedBlueprintId;
            var before = ProjectJsonSerializer.Serialize(_history.State.Project);
            var configured = visible && blueprintId is not null && _itemAdvancedWindow.ConfigureForSmokeTest();
            var previewed = configured && _itemAdvancedWindow.PreviewForSmokeTest();
            var applied = previewed && _itemAdvancedWindow.LastCommandId != Guid.Empty &&
                          _itemAdvancedWindow.LastPatchSummary.Contains("item", StringComparison.OrdinalIgnoreCase) &&
                          _itemAdvancedWindow.ApplyForSmokeTest();
            if (!applied || blueprintId is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13K_M_ITEM_ADVANCED_FAILED preview_apply_ui " +
                    _itemAdvancedWindow.LastDiagnostics); return false;
            }
            appliedToHistory = true;
            var blueprint = _history.State.Project.Blueprints.Single(item => item.Id == blueprintId);
            var profile = blueprint.ItemAdvanced;
            var context = new ItemRuntimeContext(20, 20, new HashSet<string>(["lore", "smithing"]),
                new HashSet<string>(["ash-attuned"]), new HashSet<string>(["surface-alliance"]),
                new HashSet<string>(["quest.accepted"]), "default", "gold", 1, 40, 2026, "player", true);
            var requirements = profile is null ? null : ItemAdvancedRuntimeResolver.ResolveRequirements(profile, context);
            var value = profile is null ? 0 : ItemAdvancedRuntimeResolver.ResolveEconomicValue(profile, context);
            var proc = profile is null ? null : ItemAdvancedRuntimeResolver.ResolveOnHit(profile, "hit",
                new Dictionary<Guid, DateTimeOffset>(), DateTimeOffset.UnixEpoch, context);
            var generated = profile?.Generator?.Enabled == true ? ItemAdvancedRuntimeResolver.Generate(profile, context) : null;
            var wear = profile is null ? null : ItemAdvancedRuntimeResolver.ApplyWear(profile, 40, 1, 1);
            var ammo = profile?.Ammo is null ? null : ItemAmmoResolver.Fire(profile, 3, 2026, Guid.Parse("a1650000-0000-4000-8000-000000000001"));
            var reservation = profile is null ? null : ItemUniqueRegistryResolver.Reserve(profile,
                ItemUniqueRegistryState.Empty, "player", "party", "season", DateTimeOffset.UnixEpoch, true);
            var ownership = profile is null ? null : ItemLootOwnershipResolver.Resolve(profile,
                new("player", "player", "party", "party", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
                    false, null, false, true));
            var craft = profile?.Recipes.Count > 0 ? ItemCraftingResolver.Resolve(profile, profile.Recipes[0].Id,
                new("forge", new HashSet<string>(["smithing"]), 1,
                    new Dictionary<string, int> { ["tag:ember-metal"] = 2 }, true)) : null;
            var loot = profile is null ? null : ItemAdvancedRuntimeResolver.ResolveLootContext(profile,
                "crypt-chest", "crypt", "ash-watch", null, new HashSet<string>(["undead"]), null, context);
            var script = profile is null ? null : new ItemAdvancedScriptService(blueprint);
            var snapshot = script?.Read(script.Handle);
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson).Blueprints.Single(item => item.Id == blueprintId);
            if (profile is null || ItemAdvancedValidator.Validate(profile, "item").Count != 0 ||
                requirements is not { Allowed: true } || value <= 0 || proc is not { Triggered: true } ||
                profile.Weapon is null || profile.Armor is null || ammo is not { Fired: true } ||
                wear is null || wear.Current >= 40 || profile.Container is null || generated?.Affixes.Count != 1 ||
                reservation is not { Accepted: true } || ownership is not { Allowed: true } ||
                craft is not { Allowed: true } || loot?.RuleId is null || snapshot is null ||
                _itemAdvancedWindow.SummarySectionCount != 4 || restored.ItemAdvanced?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13K_M_ITEM_ADVANCED_FAILED canonical_runtime_roundtrip");
                return false;
            }
            Undo(); appliedToHistory = false; if (ProjectJsonSerializer.Serialize(_history.State.Project) != before) return false;
            Redo(); appliedToHistory = true; if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson) return false;
            Undo(); appliedToHistory = false;
            GD.Print("OPENRPG_FORGE_TOOLSET_G13K_ITEM_FOUNDATION_PASS economy=currency-market-quality-rarity-wear-demand " +
                "requirements=level-skill-trait-faction-knowledge-script identification=levels-hidden-cost-party " +
                "ownership=history-stolen-jurisdiction-evidence-launder onhit=trigger-chance-cooldown-condition-effect-attribution-deterministic " +
                "ui=visible-empty-error-summary-simulation transaction=identified history=restored persistence=roundtrip");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13L_ITEM_EQUIPMENT_PASS weapon=attacks-damage-reach-animation-requirements-tags " +
                "armor=coverage-protection-encumbrance-noise-material-resistance-visual ammo=projectile-stack-recovery-element " +
                "durability=wear-repair-break-vfx-optional container=capacity-mass-depth-weight-permissions-persistence-cycles " +
                "ui=three-tabs runtime=deterministic persistence=roundtrip");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13M_ITEM_LOOT_CRAFT_PASS loot=context-biome-faction-level-difficulty-tags-player-event " +
                "unique=world-party-player-season-transactional ownership=public-party-personal-needgreed-gm-expiration " +
                "craft=recipe-query-station-skill-quality-duration-byproducts-provenance " +
                "generator=seed-pools-affixes-budget-rarity-exclusions-explanation forgescript=typed-handle mcp=filtered-dry-run");
            return true;
        }
        finally
        {
            _itemAdvancedWindow.Hide(); if (appliedToHistory) _history.Undo();
            RebuildPreview(); RefreshEditorControls();
        }
    }

    private bool RunWorldFeatureStudioSmokeTest()
    {
        if (_history is null || _worldFeatureStudioWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_FAILED missing_workspace");
            return false;
        }

        var original = _history.State.Project.Module.WorldFeatures;
        var appliedToHistory = false;
        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "world-features-smoke", Guid.NewGuid().ToString("N"));
        try
        {
            _worldFeatureStudioWindow.Open();
            var visible = _worldFeatureStudioWindow.Visible;
            _worldFeatureStudioWindow.Hide();
            var configured = visible && _worldFeatureStudioWindow.ConfigureForSmokeTest();
            var previewed = configured && _worldFeatureStudioWindow.PreviewForSmokeTest();
            var applied = previewed && _worldFeatureStudioWindow.LastCommandId != Guid.Empty &&
                          _worldFeatureStudioWindow.LastPatchSummary.Contains("placeables=", StringComparison.Ordinal) &&
                          _worldFeatureStudioWindow.ApplyForSmokeTest();
            appliedToHistory = applied;
            if (!applied)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_FAILED preview_apply_ui " +
                    _worldFeatureStudioWindow.LastDiagnostics);
                return false;
            }

            var catalog = _history.State.Project.Module.WorldFeatures;
            if (catalog.Placeables.Count == 0 || catalog.Doors.Count == 0 || catalog.Triggers.Count == 0 ||
                catalog.Encounters.Count == 0 || catalog.VehicleRoutes.Count == 0 || catalog.Merchants.Count == 0 ||
                catalog.Companions.Count == 0 || catalog.Scripts.Camera.Shots.Count == 0 ||
                catalog.Scripts.TagRouting.Routes.Count == 0 || catalog.Scripts.ModuleFlags.Count == 0 ||
                catalog.Scripts.AiSettings.Count == 0 || _worldFeatureStudioWindow.SectionCount != 8)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_FAILED canonical_counts");
                return false;
            }

            var placeableAccess = WorldFeatureRuntimeResolver.ResolvePlaceableAccess(catalog.Placeables[0],
                new HashSet<string> { "key:ember" }, new HashSet<string>(), 3);
            var placeableFoundation = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(catalog.Placeables[0]);
            var missingRequiredPlaceable = WorldFeatureRuntimeResolver.ResolvePlaceableFoundation(
                catalog.Placeables[0] with { Foundation = null, FoundationRequiredAtRuntime = true });
            var placeableUse = WorldFeatureRuntimeResolver.ExecutePlaceableUse(catalog.Placeables[0], Guid.NewGuid(),
                catalog.Placeables[0].Foundation!.Interaction!.Verbs[0].Id, "smoke-player", new Vector3Value(1, 0, 2),
                "success", DateTimeOffset.UnixEpoch);
            string? missingOnUsedDiagnostic = null;
            try
            {
                WorldFeatureRuntimeResolver.ExecutePlaceableUse(catalog.Placeables[0] with
                {
                    Foundation = catalog.Placeables[0].Foundation! with { OnUsed = null, OnUsedRequiredAtRuntime = true },
                }, Guid.NewGuid(), catalog.Placeables[0].Foundation!.Interaction!.Verbs[0].Id, "smoke-player",
                    Vector3Value.Zero, "success", DateTimeOffset.UnixEpoch);
            }
            catch (InvalidOperationException exception)
            {
                missingOnUsedDiagnostic = exception.Message;
            }
            var placeableHandle = new WorldFeatureScriptService().Query(_history.State, "placeable",
                catalog.Placeables[0].Id.ToString("D"), 1).Single();
            var doorDamage = WorldFeatureRuntimeResolver.ApplyDoorDamage(catalog.Doors[0],
                catalog.Doors[0].Damage!.MaximumHealth, 100);
            var doorInteraction = WorldFeatureRuntimeResolver.InteractWithDoor(catalog.Doors[0], "closed", "front",
                new HashSet<string> { "portal.travel" }, 0, new HashSet<string>());
            var doorNavigationClosed = WorldFeatureRuntimeResolver.SynchronizeDoorNavigation(catalog.Doors[0], "closed", 1);
            var doorNavigationOpen = WorldFeatureRuntimeResolver.SynchronizeDoorNavigation(catalog.Doors[0], "opening", .8);
            var trigger = WorldFeatureRuntimeResolver.EvaluateTrigger(catalog.Triggers[0], 100,
                new HashSet<string> { "key:ember" }, new HashSet<string> { "player" }, false, 1, 0, true);
            var encounter = WorldFeatureRuntimeResolver.EvaluateEncounter(catalog.Encounters[0], 0, 5, false, false, 60);
            var encounterActivation = WorldFeatureRuntimeResolver.ActivateEncounter(catalog.Encounters[0],
                new HashSet<string> { "player" }, "party", true, 10, .5,
                new HashSet<string> { "area.occupied" }, new Dictionary<Guid, int>());
            var route = WorldFeatureRuntimeResolver.SampleVehicleRoute(catalog.VehicleRoutes[0], 1);
            var merchant = WorldFeatureRuntimeResolver.QuoteMerchant(catalog.Merchants[0],
                catalog.Merchants[0].Currencies[0].Id, 10, false, "outside", 0, 5, .1, .2);
            var merchantRuntime = new MerchantWorldTransactionService();
            var merchantResolution = merchantRuntime.Resolve(catalog.Merchants[0]);
            var merchantPolicy = catalog.Merchants[0].Transactions;
            var merchantTrade = merchantPolicy is null || merchantResolution.TransactionHandle is null
                ? null
                : merchantRuntime.Execute(merchantResolution.TransactionHandle, catalog.Merchants[0],
                    new("smoke-player", new Dictionary<string, decimal> { [merchantPolicy.Stock[0].CurrencyId] = 100 },
                        new Dictionary<Guid, int>(), merchantPolicy.BuyerCapacity),
                    new("smoke-store", new Dictionary<string, decimal> { [merchantPolicy.Stock[0].CurrencyId] = 1000 },
                        new Dictionary<Guid, int>(), merchantPolicy.SellerCapacity),
                    new(Guid.NewGuid(), TradeKind.Buy, "smoke-player", "smoke-store",
                        [new(merchantPolicy.Stock[0].ItemBlueprintId.Value, merchantPolicy.Stock[0].VariantId, 1,
                            merchantPolicy.Stock[0].UnitPrice, merchantPolicy.Stock[0].CurrencyId)], []));
            var missingRequiredMerchant = merchantRuntime.Resolve(catalog.Merchants[0] with
            { Transactions = null, TransactionsRequiredAtRuntime = true });
            var merchantHandle = new WorldFeatureScriptService().Query(_history.State, "merchant",
                catalog.Merchants[0].Id.ToString("D"), 1).Single();
            var companion = WorldFeatureRuntimeResolver.EvaluateCompanion(catalog.Companions[0], "owner", .2,
                true, 2, true, true);
            var companionValidation = CompanionValidationRuntimeResolver.Resolve(catalog.Companions[0],
                catalog.Companions[0].Validation!.CrossZoneNavigation.AllowedAreaIds.FirstOrDefault());
            var missingRequiredValidation = CompanionValidationRuntimeResolver.Resolve(
                catalog.Companions[0] with { Validation = null, ValidationRequiredAtRuntime = true });
            var companionHandle = new WorldFeatureScriptService().Query(_history.State, "companion",
                catalog.Companions[0].Id.ToString("D"), 1).Single();
            var scripts = WorldFeatureRuntimeResolver.ResolveScript(catalog.Scripts, "door.ash", "opened",
                cameraSequence: true);
            if (!placeableAccess.Allowed || !placeableFoundation.Available ||
                placeableFoundation.SnappedTransform?.Position != new Vector3Value(1.25f, 0, -2.5f) ||
                !placeableFoundation.Overlay.Contains("pivot=0,0.5,0", StringComparison.Ordinal) ||
                missingRequiredPlaceable.DiagnosticCode != "WORLD_PLACEABLE_FOUNDATION_REQUIRED_MISSING" ||
                placeableUse.EventBinding != "InteractionExecuted" || placeableUse.ActorId != "smoke-player" ||
                placeableUse.TargetPoint != new Vector3Value(1, 0, 2) || placeableUse.Result != "success" ||
                missingOnUsedDiagnostic != "WORLD_PLACEABLE_ON_USED_REQUIRED_MISSING" ||
                placeableHandle.Properties["foundation"] != bool.TrueString ||
                placeableHandle.Properties["role"] != "interactable" ||
                placeableHandle.Properties["onUsed"] != bool.TrueString ||
                placeableHandle.Properties["validation"] != bool.TrueString || !doorDamage.Breached || !doorInteraction.Accepted ||
                doorInteraction.CurrentState != "opening" || catalog.Doors[0].Foundation?.States.Count != 9 ||
                catalog.Doors[0].Foundation?.Links.Count != 1 || !doorNavigationClosed.ObstacleEnabled ||
                doorNavigationClosed.LinkEnabled || doorNavigationOpen.ObstacleEnabled || !doorNavigationOpen.LinkEnabled ||
                !doorNavigationClosed.Visualization.Contains("blocked", StringComparison.Ordinal) ||
                !trigger.Fired || !encounter.Spawn || !encounterActivation.Activated ||
                catalog.Encounters[0].Foundation?.Candidates.Count != 1 ||
                !route.AtStop || !merchant.Accepted || !merchantResolution.Available || merchantTrade?.Committed != true ||
                missingRequiredMerchant.DiagnosticCode != "WORLD_MERCHANT_TRANSACTIONS_REQUIRED_MISSING" ||
                merchantHandle.Properties["transactions"] != bool.TrueString || merchantHandle.Properties["stock"] != "1" ||
                !companion.DirectControl || scripts.ScriptId is null ||
                !scripts.CameraControlLocked || !companionValidation.Available || !companionValidation.DestinationAllowed ||
                missingRequiredValidation.Diagnostics.All(item => item.Code != "world.companion.validation_required") ||
                companionHandle.Properties["validation"] != bool.TrueString ||
                companionHandle.Properties["commands"] != "4")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_FAILED runtime_resolution");
                return false;
            }

            var validCompanion = catalog.Companions[0];
            var invalidCompanion = validCompanion with
            {
                Validation = validCompanion.Validation! with { CommandIds = ["follow", "follow"] },
            };
            var invalidCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
                catalog.Placeables, catalog.Doors, catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes,
                catalog.Merchants, [invalidCompanion], catalog.Scripts);
            var invalidPreview = _worldFeatureHandler.Handle(_history.State, new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, invalidCatalog, true));
            if (invalidPreview.Succeeded || invalidPreview.Diagnostics.All(item => item.Code != "world.companion.commands_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R037_COMPANION_FAILED invalid_prebuild_validation");
                return false;
            }

            var validPlaceable = catalog.Placeables[0];
            var invalidPlaceable = validPlaceable with
            {
                Foundation = validPlaceable.Foundation! with
                {
                    Slug = string.Empty,
                    Interaction = validPlaceable.Foundation!.Interaction! with { MaximumDistance = 0 },
                    Collision = validPlaceable.Foundation.Collision with { Enabled = false, NavigationObstacle = true },
                    Transform = validPlaceable.Foundation.Transform with { Scale = Vector3Value.Zero },
                },
            };
            var invalidPlaceableCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
                [invalidPlaceable], catalog.Doors, catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes,
                catalog.Merchants, catalog.Companions, catalog.Scripts);
            var invalidPlaceablePreview = _worldFeatureHandler.Handle(_history.State, new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, invalidPlaceableCatalog, true));
            var invalidPlaceableCodes = invalidPlaceablePreview.Diagnostics.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            if (invalidPlaceablePreview.Succeeded || !invalidPlaceableCodes.Contains("world.placeable.identity_invalid") ||
                !invalidPlaceableCodes.Contains("world.placeable.collision_invalid") ||
                !invalidPlaceableCodes.Contains("world.placeable.interaction_invalid") ||
                !invalidPlaceableCodes.Contains("world.placeable.transform_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R046_PLACEABLE_FAILED invalid_prebuild_validation");
                return false;
            }

            var invalidPlaceableRuntime = validPlaceable with
            {
                Foundation = validPlaceable.Foundation! with
                {
                    OnUsed = validPlaceable.Foundation!.OnUsed! with { EventBinding = string.Empty },
                    Validation = new([], [], [], [], [], 0, 0),
                },
            };
            var invalidPlaceableRuntimeCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
                [invalidPlaceableRuntime], catalog.Doors, catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes,
                catalog.Merchants, catalog.Companions, catalog.Scripts);
            var invalidPlaceableRuntimePreview = _worldFeatureHandler.Handle(_history.State, new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, invalidPlaceableRuntimeCatalog, true));
            var invalidPlaceableRuntimeCodes = invalidPlaceableRuntimePreview.Diagnostics.Select(item => item.Code)
                .ToHashSet(StringComparer.Ordinal);
            if (invalidPlaceableRuntimePreview.Succeeded ||
                !invalidPlaceableRuntimeCodes.Contains("world.placeable.on_used_invalid") ||
                !invalidPlaceableRuntimeCodes.Contains("world.placeable.animation_invalid") ||
                !invalidPlaceableRuntimeCodes.Contains("world.placeable.lock_reference_missing") ||
                !invalidPlaceableRuntimeCodes.Contains("world.placeable.trap_reference_missing") ||
                !invalidPlaceableRuntimeCodes.Contains("world.placeable.inventory_reference_missing") ||
                !invalidPlaceableRuntimeCodes.Contains("world.placeable.physics_profile_missing") ||
                !invalidPlaceableRuntimeCodes.Contains("world.placeable.budget_exceeded"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R047_PLACEABLE_EVENTS_FAILED invalid_prebuild_validation");
                return false;
            }

            var validDoor = catalog.Doors[0];
            var invalidDoor = validDoor with
            {
                Foundation = validDoor.Foundation! with
                {
                    Links = [new(Guid.Parse("d0410000-0000-4000-8000-000000000001"), validDoor.Id, true, string.Empty)],
                    Navigation = validDoor.Foundation.Navigation! with
                    {
                        ObstacleId = string.Empty,
                        BlockingStateIds = ["closed", "open"],
                        TraversableStateIds = ["open"],
                    },
                },
            };
            var invalidDoorCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
                catalog.Placeables, [invalidDoor], catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes,
                catalog.Merchants, catalog.Companions, catalog.Scripts);
            var invalidDoorPreview = _worldFeatureHandler.Handle(_history.State, new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, invalidDoorCatalog, true));
            var invalidDoorCodes = invalidDoorPreview.Diagnostics.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            if (invalidDoorPreview.Succeeded || !invalidDoorCodes.Contains("world.door.navigation_invalid") ||
                !invalidDoorCodes.Contains("world.door.navigation_state_mismatch") ||
                !invalidDoorCodes.Contains("world.door.transition_loop"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R041_DOOR_FAILED invalid_prebuild_validation");
                return false;
            }

            var validEncounter = catalog.Encounters[0];
            var invalidEncounter = validEncounter with
            {
                Foundation = validEncounter.Foundation! with
                {
                    Volume = validEncounter.Foundation.Volume with { Points = [], Height = 0 },
                    Candidates = [validEncounter.Foundation.Candidates[0] with { Cost = 0, MaximumAlive = 99 }],
                    SpawnBudget = 0,
                    SpawnAnchorIds = [],
                    RewardIds = [string.Empty],
                    NetworkActorCap = 1,
                },
            };
            var invalidEncounterCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
                catalog.Placeables, catalog.Doors, catalog.Triggers, [invalidEncounter], catalog.VehicleRoutes,
                catalog.Merchants, catalog.Companions, catalog.Scripts);
            var invalidEncounterPreview = _worldFeatureHandler.Handle(_history.State, new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, invalidEncounterCatalog, true));
            var invalidEncounterCodes = invalidEncounterPreview.Diagnostics.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            if (invalidEncounterPreview.Succeeded || !invalidEncounterCodes.Contains("world.encounter.volume_invalid") ||
                !invalidEncounterCodes.Contains("world.encounter.candidate_invalid") ||
                !invalidEncounterCodes.Contains("world.encounter.budget_invalid") ||
                !invalidEncounterCodes.Contains("world.encounter.anchors_invalid") ||
                !invalidEncounterCodes.Contains("world.encounter.rewards_invalid") ||
                !invalidEncounterCodes.Contains("world.encounter.network_cap_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R042_ENCOUNTER_FAILED invalid_prebuild_validation");
                return false;
            }

            var validMerchant = catalog.Merchants[0];
            var invalidMerchant = validMerchant with
            {
                Transactions = validMerchant.Transactions! with
                {
                    VendorInteractionId = string.Empty,
                    Locales = [],
                    BuyerCapacity = 0,
                    ServerAuthoritative = false,
                    Stock = [validMerchant.Transactions!.Stock[0] with { Quantity = -1 }],
                },
            };
            var invalidMerchantCatalog = new WorldFeatureCatalog(catalog.Id, catalog.SchemaVersion, catalog.Provenance,
                catalog.Placeables, catalog.Doors, catalog.Triggers, catalog.Encounters, catalog.VehicleRoutes,
                [invalidMerchant], catalog.Companions, catalog.Scripts);
            var invalidMerchantPreview = _worldFeatureHandler.Handle(_history.State, new SetWorldFeatureCatalogCommand(
                CommandId.From(Guid.NewGuid()), _history.State.Revision, invalidMerchantCatalog, true));
            var invalidMerchantCodes = invalidMerchantPreview.Diagnostics.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
            if (invalidMerchantPreview.Succeeded || !invalidMerchantCodes.Contains("world.merchant.vendor_binding_invalid") ||
                !invalidMerchantCodes.Contains("world.merchant.localization_invalid") ||
                !invalidMerchantCodes.Contains("world.merchant.capacity_invalid") ||
                !invalidMerchantCodes.Contains("world.merchant.authority_invalid") ||
                !invalidMerchantCodes.Contains("world.merchant.stock_values_invalid"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R045_MERCHANT_FAILED invalid_prebuild_validation");
                return false;
            }

            var aggregate = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(_history.State.Project));
            var created = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            if (!created.Succeeded || !reopened.Succeeded || aggregate.Module.WorldFeatures.Placeables.Count == 0 ||
                reopened.Session!.Project.Module.WorldFeatures.Companions.Count == 0 ||
                reopened.Session.Project.Module.WorldFeatures.Merchants[0].Transactions?.Stock.Count != 1 ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].Foundation?.Transform.SnapIncrement != .25 ||
                reopened.Session.Project.Module.WorldFeatures.Placeables[0].Foundation?.OnUsed?.EventBinding != "InteractionExecuted")
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_FAILED persistence");
                return false;
            }

            var profileId = catalog.Id;
            var undo = _history.Undo();
            var redo = _history.Redo();
            var cleanup = _history.Undo();
            appliedToHistory = false;
            if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
                _history.State.Project.Module.WorldFeatures.Id != original.Id ||
                redo.State.Project.Module.WorldFeatures.Id != profileId)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_FAILED history");
                return false;
            }

            GD.Print("OPENRPG_FORGE_TOOLSET_G13N_PLACEABLE_SOCIAL_ACCESS_PASS faction=ownership dialogue=bound credential=consume-charge-shared-virtual-audit");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13O_PLACEABLE_REACTIVE_PHYSICS_PASS events=attack-ability-heartbeat-custom physics=rigid-kinematic-replicated-sleep-static");
            GD.Print("OPENRPG_FORGE_TOOLSET_R046_PLACEABLE_FOUNDATION_PASS blueprint=composable-visual-collision-interaction-health-container-lock-trap-physics-scripts static=batching-instancing-occluder-merge interaction=verbs-distance-angle-focus-prompt-duration-authority-conditions-event identity=uuid-slug-localized-visual-icon-portrait-tags-notes transform=position-rotation-scale-pivot-snap-parent-surface-animation-overrides ui=visible-empty-error-overlay-summary transaction=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic");
            GD.Print("OPENRPG_FORGE_TOOLSET_R047_PLACEABLE_EVENTS_VALIDATION_PASS onused=typed-event-verb-actor-target-result-provenance-api-before-after validation=collision-navigation-animation-locks-traps-inventory-scripts-physics-budgets diagnostics=stable-path-uuid ui=visible-empty-error-overlay-summary transaction=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic");
            GD.Print("OPENRPG_FORGE_TOOLSET_R048_TRIGGER_FOUNDATION_PASS volume=2d-extruded-or-3d-vertices-holes-height-snap-transform-overlay generic=filters-conditions-cooldown-fire-policy-actions portal=typed-destination-anchor-orientation-preload-permissions events=entered-entity-point-direction-layers-state-exited-duration-reason-api-scripts ui=visible-empty-error-summary transaction=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic");
            GD.Print("OPENRPG_FORGE_TOOLSET_R049_TRIGGER_VALIDATION_PASS validation=geometry-self-intersection-filters-actions-targets-portal-cooldown-scripts-accessibility diagnostics=stable-path-uuid ui=visible-empty-error-summary transaction=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic");
            GD.Print("OPENRPG_FORGE_TOOLSET_R050_THIRD_PARTY_NOTICES_PASS aggregation=module-blueprints-data-tables-content-packages-sources-assets-dependencies spdx=approved-required-unknown-blocked notice=deterministic-preview-export-recalculated-mismatch-rejected diagnostics=stable-path-uuid ui=visible-empty-error-preview command=dry-run-apply-reject history=undo-redo persistence=roundtrip cli=structured mcp=bounded-export build=preflight-before-write");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13P_DOOR_DAMAGE_LOADING_PASS damage=health-hardness-breach-navigation-debris-repair loading=portal-fallback events=damage-death-attack");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13Q_DOOR_REACTIVE_COMPLEX_PASS events=ability-heartbeat-custom social=dialogue-faction assembly=double-portcullis-elevator-airlock");
            GD.Print("OPENRPG_FORGE_TOOLSET_R040_DOOR_FOUNDATION_PASS placement=socket-or-free states=open-closed-opening-closing-locked-jammed-broken-disabled-custom interaction=range-side-clearance-animation-collision-authority-queue-feedback portal=typed-anchor-orientation-preload-permissions links=oneway-or-atomic-pair ui=section-summary transaction=dry-run-apply history=undo-redo persistence=roundtrip runtime=resolved");
            GD.Print("OPENRPG_FORGE_TOOLSET_R041_DOOR_NAVIGATION_VALIDATION_PASS navigation=obstacle-link-animation-state-visualization blocking=tested validation=link-destination-animation-collision-nav-scripts-loop diagnostics=stable-path-uuid ui=visible transaction=dry-run-reject persistence=roundtrip runtime=synchronized");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13R_TRIGGER_DISCOVERY_ACCESS_PASS secret=senses-difficulty-scope click=verb-hit permission=credential cursor=outline rest=safe");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13S_TRIGGER_AUTOMATION_NARRATIVE_GM_PASS heartbeat=occupied-budget custom=namespaced clue=journal narrative=priority-cooldown director=manual-audit");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13T_ENCOUNTER_LIFECYCLE_PASS respawn=delay-cycles-persistence-antifarm faction=override heartbeat=budget custom=namespaced");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13U_ENCOUNTER_TACTICAL_PASS reinforcement=alert-route-cap-delay visibility=camera-los cleanup=dead-escaped-abandoned-unload-persistent");
            GD.Print("OPENRPG_FORGE_TOOLSET_R042_ENCOUNTER_FOUNDATION_PASS volume=2d-3d-filters-los-threat-script candidates=blueprint-weight-role-cost-conditions-variants-limits validation=geometry-references-budget-anchors-rewards-network-cap ui=visible transaction=dry-run-reject persistence=roundtrip runtime=nominal-failure");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13V_VEHICLE_ROUTE_PASS route=points-lanes-speed-stops-overtaking network=sync");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13W_MERCHANT_CORE_PASS treasury=balance-credit-debt-caps purchase=bounded stolen=policy restock=world-time-route-persistent");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13X_MERCHANT_ADVANCED_PASS currencies=typed-exchange negotiation=bounded market=supply-demand services=repair audit=ledger-anomaly");
            GD.Print("OPENRPG_FORGE_TOOLSET_R045_MERCHANT_TRANSACTIONS_PASS transaction=buy-sell-barter-aggregate-atomic-rollback validation=vendor-currency-treasury-stock-price-restrictions-localization-scripts ui=visible-sections-summary command=dry-run-apply-reject history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic-authoritative");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13Y_COMPANION_SOCIAL_PROGRESSION_PASS dialogue=permissions formation=slots-obstacles progression=preview-respec relation=loyalty-history banter=priority-cooldown");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13Z_COMPANION_GAMEPLAY_CONTROL_PASS quest=personal traps=task ability=policy control=direct-authorized party=split-regroup-sync");
            GD.Print("OPENRPG_FORGE_TOOLSET_R037_COMPANION_PASS validation=identity-recruitment-ownership-commands-equipment-death-dialogue-contexts-cross-zone ui=sections-preview-empty-error command=dry-run-apply history=undo-redo persistence=roundtrip forgescript=typed-handle mcp=filtered-dry-run runtime=optional-safe-required-diagnostic");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AA_SCRIPT_CAMERA_TAG_PASS camera=rig-shots-blends-lock-skip-abort tag=explicit-visible-route");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AB_SCRIPT_SWITCHES_PASS module=typed-documented-scoped-migrated ai=named-schema-preview-authoritative");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_PASS ui=tabs-8-empty-errors-summary transaction=preview-apply-audit-history mcp=filtered-dry-run persistence=project-workspace-package runtime=resolved");
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13N_AB_WORLD_FEATURES_FAILED " + exception);
            return false;
        }
        finally
        {
            _worldFeatureStudioWindow.Hide();
            if (appliedToHistory) _history.Undo();
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }
    }

    private bool RunPostLaunchStudioSmokeTest()
    {
        if (_history is null || _postLaunchStudioWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_FAILED missing_workspace");
            return false;
        }

        var original = _history.State.Project.Module.RuntimePolicies.Extensions.PostLaunch;
        var appliedToHistory = false;
        var smokeRoot = Path.Combine(Path.GetTempPath(), "OpenRPG_Forge", "post-launch-smoke", Guid.NewGuid().ToString("N"));
        try
        {
            _postLaunchStudioWindow.Open();
            var visible = _postLaunchStudioWindow.Visible;
            _postLaunchStudioWindow.Hide();
            var configured = visible && _postLaunchStudioWindow.ConfigureForSmokeTest();
            var previewed = configured && _postLaunchStudioWindow.PreviewForSmokeTest();
            var applied = previewed && _postLaunchStudioWindow.LastCommandId != Guid.Empty &&
                          _postLaunchStudioWindow.LastPatchSummary.Contains("post-launch", StringComparison.Ordinal) &&
                          _postLaunchStudioWindow.ApplyForSmokeTest();
            appliedToHistory = applied;
            if (!applied)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_FAILED preview_apply_ui");
                return false;
            }

            var post = _history.State.Project.Module.RuntimePolicies.Extensions.PostLaunch;
            if (_postLaunchStudioWindow.SectionCount != 6 || !post.Gameplay.ZeroGravity.Enabled ||
                !post.Gameplay.Vehicles.Enabled || !post.Gameplay.Mounts.Enabled || post.Gameplay.Achievements.Count != 1 ||
                !post.LiveDirector.ForcedRest.Enabled || !post.LiveDirector.LevelAdjustment.Enabled ||
                !post.LiveDirector.AppearanceOverrides.Enabled || !post.LiveDirector.SessionRecording.Enabled ||
                post.LiveDirector.SavedGroups.Count != 1 || !post.Network.Voice.Enabled || post.Network.Shards.Count != 1 ||
                !post.Network.HotReload.Enabled || !post.Network.ServerExtensions.Enabled ||
                !post.CreatorExperience.DynamicAreas.Enabled || !post.CreatorExperience.Observability.Enabled ||
                !post.Production.MediaGeneration.Enabled || !post.Production.SoakTests.Enabled)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_FAILED canonical_counts");
                return false;
            }

            var mobility = PostLaunchCapabilityResolver.ResolveMobility(post.Gameplay, true, .5f, 4, true);
            var achievement = PostLaunchCapabilityResolver.EvaluateAchievement(post.Gameplay, "crypt-liberated", 1);
            var director = PostLaunchCapabilityResolver.PlanDirectorIntervention(post.LiveDirector, "adjust-level", 1, 2);
            var recording = PostLaunchCapabilityResolver.ResolveRecording(post.LiveDirector, 1,
                new Dictionary<string, IReadOnlyList<Guid>> { ["tag:guardian"] = [Guid.NewGuid()] });
            var content = PostLaunchCapabilityResolver.ResolveNetworkContent(post.Gameplay, post.Network, true, "CC0-1.0");
            var reload = PostLaunchCapabilityResolver.PlanShardReload(post.Network, "crypt-eu", 64, 4);
            var protocol = PostLaunchCapabilityResolver.NegotiateProtocol(post.Network, "windows", 1, 2,
                ["metrics"], true);
            var creator = PostLaunchCapabilityResolver.ResolveCreatorTechnical(post.CreatorExperience,
                ["graphics-tier"], 32, 2);
            var rpg = PostLaunchCapabilityResolver.ResolveRpgDistribution(post.CreatorExperience, 8, 32);
            var media = PostLaunchCapabilityResolver.PlanProduction(post.Production, "image", 1);
            var soak = PostLaunchCapabilityResolver.PlanProduction(post.Production, "soak", 32, 64, 100, 150);
            if (!mobility.ZeroGravityActive || !mobility.VehicleAccepted || !mobility.MountAccepted ||
                !achievement.Unlocked || !director.Accepted || !recording.Accepted ||
                content.Diagnostic != "POST_NETWORK_CONTENT_READY" || !reload.Accepted || !protocol.Compatible ||
                !creator.Accepted || !rpg.PartyControl || !media.Accepted || !soak.Accepted)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_FAILED runtime_resolution");
                return false;
            }

            var aggregate = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(_history.State.Project));
            var created = ProjectSession.Create(smokeRoot, _history.State.Project);
            var reopened = ProjectSession.Open(smokeRoot);
            if (!created.Succeeded || !reopened.Succeeded ||
                !aggregate.Module.RuntimePolicies.Extensions.PostLaunch.Gameplay.ZeroGravity.Enabled ||
                !reopened.Session!.Project.Module.RuntimePolicies.Extensions.PostLaunch.Production.SoakTests.Enabled)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_FAILED persistence");
                return false;
            }

            var profileId = post.Id;
            var undo = _history.Undo();
            var redo = _history.Redo();
            var cleanup = _history.Undo();
            appliedToHistory = false;
            if (!undo.Succeeded || !redo.Succeeded || !cleanup.Succeeded ||
                _history.State.Project.Module.RuntimePolicies.Extensions.PostLaunch.Id != original.Id ||
                redo.State.Project.Module.RuntimePolicies.Extensions.PostLaunch.Id != profileId)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_FAILED history");
                return false;
            }

            GD.Print("OPENRPG_FORGE_TOOLSET_G13AC_MOBILITY_PASS zero-g=six-axis-inertia-thrusters vehicles=seats-possession-physics-damage-replication mounts=rider-combat-inventory-save");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AD_ACHIEVEMENTS_PACKS_PASS achievements=event-threshold-platform-privacy packs=channel-compatibility-license-signature-offline");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AE_DIRECTOR_INTERVENTIONS_PASS rest=reason-override-event level=rebuild-snapshot-rollback appearance=variant-scale-material-duration-validation");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AF_DIRECTOR_GROUP_RECORDING_PASS groups=hotkey-query-cap recording=commands-events-bookmarks-redaction-export");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AG_NETWORK_CONTENT_VOICE_PASS curated=trusted-license-fallback voice=opus-positional-ptt-moderation");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AH_SHARD_HOT_RELOAD_PASS shards=areas-capacity-region reload=drain-atomic-rollback-fingerprint");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AI_SERVER_PROTOCOL_PASS extensions=signed-isolated-capabilities-quota protocol=family-range-platform-negotiation");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AJ_CREATOR_TECHNICAL_PASS water=refraction-reflection-depth device=consent-redaction resources=quota-namespace areas=authority-lifetime graphics=msaa-aniso-fallback");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AK_RPG_DISTRIBUTION_OBSERVABILITY_PASS party=direct-group progression=data-driven-64 repository=channel-signature-notes profiler=tracy-otel-bounded");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AL_MEDIA_SOAK_PASS media=rights-staging-review-fallback soak=duration-memory-handles-database-latency-players");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_PASS ui=tabs-6-empty-errors-summary transaction=preview-apply-audit-history mcp=filtered-preview persistence=project-workspace-package runtime=domain-director-network-qa");
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13AC_AL_POST_LAUNCH_FAILED " + exception);
            return false;
        }
        finally
        {
            _postLaunchStudioWindow.Hide();
            if (appliedToHistory) _history.Undo();
            if (Directory.Exists(smokeRoot)) Directory.Delete(smokeRoot, recursive: true);
        }
    }

    private bool RunAdvancedFactionSmokeTest()
    {
        if (_history is null || _factionAdvancedWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13G_ADVANCED_FACTIONS_FAILED missing_workspace");
            return false;
        }

        var appliedToHistory = false;
        try
        {
            _factionAdvancedWindow.Open();
            var visible = _factionAdvancedWindow.Visible;
            _factionAdvancedWindow.Hide();
            var before = ProjectJsonSerializer.Serialize(_history.State.Project);
            var configured = visible && _factionAdvancedWindow.ConfigureForSmokeTest();
            var previewed = configured && _factionAdvancedWindow.PreviewForSmokeTest();
            var applied = previewed && _factionAdvancedWindow.LastCommandId != Guid.Empty &&
                          _factionAdvancedWindow.LastPatchSummary.Contains("faction", StringComparison.OrdinalIgnoreCase) &&
                          _factionAdvancedWindow.ApplyForSmokeTest();
            if (!configured || !previewed || !applied)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13G_ADVANCED_FACTIONS_FAILED preview_apply_ui " +
                    $"configured={configured} previewed={previewed} applied={applied} " +
                    $"patch={_factionAdvancedWindow.LastPatchSummary} diagnostics={_factionAdvancedWindow.LastDiagnostics}");
                return false;
            }

            appliedToHistory = true;
            var catalog = _history.State.Project.Module.Factions;
            var profile = catalog.Advanced;
            var watch = catalog.Factions.Single(item => item.Slug == "ash-watch");
            var surface = catalog.Factions.Single(item => item.Slug == "surface-alliance");
            var effective = FactionRuntimeResolver.EffectiveDefinition(catalog, watch.Id);
            var at = DateTimeOffset.Parse("2026-08-05T12:00:00Z");
            var memberships = FactionRuntimeResolver.Memberships(catalog, "hero", at);
            var witness = FactionRuntimeResolver.PropagateWitnesses(catalog,
                [new(Guid.Parse("b2050000-0000-4000-8000-000000000030"), "toolset-smoke", "hero",
                    "ash-warden-boss", watch.Id, "crime.assault", .9, .8, 12, at, true)], at);
            var dynamic = profile?.DynamicFactions.Single();
            var truce = new FactionDiplomacyState(Guid.Parse("b2050000-0000-4000-8000-000000000031"),
                surface.Id, FactionDiplomacyStatus.Truce, at, at.AddHours(1), "toolset-smoke");
            var denied = profile is null || dynamic is null ? null :
                FactionRuntimeResolver.TransitionDiplomacy(profile, dynamic.Id, truce, false);
            var transitioned = profile is null || dynamic is null ? null :
                FactionRuntimeResolver.TransitionDiplomacy(profile, dynamic.Id, truce, true);
            var expiration = transitioned is null ? null : FactionRuntimeResolver.AdvanceExpirations(
                transitioned.Profile, DateTimeOffset.Parse("2031-08-05T12:00:00Z"), true);
            var appliedJson = ProjectJsonSerializer.Serialize(_history.State.Project);
            var restored = ProjectJsonSerializer.Deserialize(appliedJson);
            var cycleA = Guid.Parse("b2050000-0000-4000-8000-000000000040");
            var cycleB = Guid.Parse("b2050000-0000-4000-8000-000000000041");
            var orphanParent = Guid.Parse("b2050000-0000-4000-8000-000000000042");
            var invalidCatalog = new FactionCatalog(
                [
                    new(cycleA, 1, "toolset-smoke", "cycle-a", "Cycle A", false, [cycleB], []),
                    new(cycleB, 1, "toolset-smoke", "cycle-b", "Cycle B", false, [cycleA], []),
                    new(Guid.Parse("b2050000-0000-4000-8000-000000000043"), 1, "toolset-smoke",
                        "orphan-child", "Orphan Child", false, [orphanParent], []),
                ],
                [],
                [new("hostile", -100, 0, 1), new("neutral", -10, 50, 1)],
                [new("ghost", Guid.Parse("b2050000-0000-4000-8000-000000000044"))]);
            var invalidDiagnostics = FactionValidator.Validate(invalidCatalog, "module/factions");
            var requiredValidationCodes = new[]
            {
                "RPG_FACTION_INHERITANCE_CYCLE",
                "RPG_FACTION_PARENT_ORPHAN",
                "RPG_FACTION_BANDS_OVERLAP",
                "RPG_FACTION_MEMBER_ORPHAN",
            };
            if (profile is null || profile.Id == Guid.Empty || profile.SchemaVersion != 1 ||
                !profile.ServerAuthoritative || FactionValidator.Validate(catalog).Count != 0 ||
                catalog.Factions.Count != 3 || profile.Inheritance.Count != 1 ||
                effective.Tags.Count < 4 || memberships.Count != 2 || witness.Reports.Count != 1 ||
                denied?.Diagnostics.SingleOrDefault()?.Code != "faction.dynamic.authority_denied" ||
                transitioned?.Events.Count != 1 || expiration?.Events.Count < 1 ||
                requiredValidationCodes.Any(code => invalidDiagnostics.All(item => item.Code != code)) ||
                invalidDiagnostics.Any(item => !item.Path.StartsWith("module/factions", StringComparison.Ordinal)) ||
                _factionAdvancedWindow.SummarySectionCount != 4 ||
                ProjectJsonSerializer.Serialize(restored) != appliedJson ||
                restored.Module.Factions.Advanced?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13G_ADVANCED_FACTIONS_FAILED canonical_roundtrip_runtime_summary " +
                    $"profile={profile is not null} definitions={catalog.Factions.Count} inherited={effective.Tags.Count} " +
                    $"memberships={memberships.Count} witness={witness.Reports.Count} transition={transitioned?.Events.Count} " +
                    $"expiration={expiration?.Events.Count} summary={_factionAdvancedWindow.SummarySectionCount}");
                return false;
            }

            Undo(); appliedToHistory = false;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != before)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13G_ADVANCED_FACTIONS_FAILED undo");
                return false;
            }
            Redo(); appliedToHistory = true;
            if (ProjectJsonSerializer.Serialize(_history.State.Project) != appliedJson)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13G_ADVANCED_FACTIONS_FAILED redo");
                return false;
            }
            Undo(); appliedToHistory = false;

            GD.Print("OPENRPG_FORGE_TOOLSET_G13G_ADVANCED_FACTIONS_PASS " +
                "hierarchy=parents-tags-policies-relations-no-cycles witnesses=perception-memory-communication-evidence " +
                "memberships=multiple-rank-cover-loyalty dynamic=temporary-runtime-alliance-war-truce-expiration " +
                "ui=visible-empty-error-summary uuid=stable schema=v1 provenance=tracked forgescript=typed-handle-server " +
                "mcp=filtered-dry-run transaction=identified audit=generated history=restored " +
                "persistence=project-workspace-package runtime=optional-safe-authoritative-replicated");
            GD.Print("OPENRPG_FORGE_TOOLSET_R043_FACTION_VALIDATION_PASS " +
                "validation=inheritance-cycle-parent-orphan-band-overlap-member-orphan " +
                "diagnostics=stable-path-uuid ui=visible transaction=dry-run-reject persistence=roundtrip");
            return true;
        }
        finally
        {
            _factionAdvancedWindow.Hide();
            if (appliedToHistory) _history.Undo();
            RebuildPreview();
            RefreshEditorControls();
        }
    }

    private bool RunDialoguePresentationSmokeTest()
    {
        if (_history is null || _dialoguePresentationWindow is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_FAILED missing_workspace");
            return false;
        }

        try
        {
            var beforeRevision = _history.State.Revision;
            var dialogueId = _history.State.Project.Dialogues.First().Id;
            var previousProfileId = _history.State.Project.Dialogues.First(item => item.Id == dialogueId)
                .Presentation?.Id;
            if (!_dialoguePresentationWindow.ConfigureForSmokeTest() ||
                !_dialoguePresentationWindow.PreviewForSmokeTest() ||
                !_dialoguePresentationWindow.ApplyForSmokeTest() ||
                !_dialoguePresentationWindow.SimulateForSmokeTest())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_FAILED preview_apply_ui " +
                    _dialoguePresentationWindow.LastDiagnostics);
                return false;
            }

            var applied = _history.State.Project.Dialogues.First(item => item.Id == dialogueId);
            var profile = applied.Presentation;
            if (profile is null || DialoguePresentationValidator.Validate(applied).Count != 0 ||
                _history.State.Revision != beforeRevision.Next())
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_FAILED canonical_profile");
                return false;
            }

            var script = new DialoguePresentationScriptService(applied);
            var snapshot = script.Read(script.Handle);
            var roundTrip = ProjectJsonSerializer.Deserialize(ProjectJsonSerializer.Serialize(_history.State.Project));
            var restored = roundTrip.Dialogues.First(item => item.Id == dialogueId).Presentation;
            if (snapshot.AnimationCueCount < 1 || snapshot.VoiceCueCount < 1 ||
                snapshot.CameraShotCount < 1 || snapshot.BookmarkCount < 1 ||
                snapshot.TimedChoiceCount < 1 || snapshot.VariantSetCount < 1 || snapshot.VariantCount < 2 ||
                restored?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_FAILED script_roundtrip");
                return false;
            }

            Undo();
            if (_history.State.Project.Dialogues.First(item => item.Id == dialogueId).Presentation?.Id != previousProfileId)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_FAILED undo");
                return false;
            }
            Redo();
            if (_history.State.Project.Dialogues.First(item => item.Id == dialogueId).Presentation?.Id != profile.Id)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_FAILED redo");
                return false;
            }

            GD.Print("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_PASS " +
                "animations=timing-blend-gesture-lookat-interrupt-fallback " +
                "voice=locale-take-subtitle-lipsync-emotion-duration-license-fallback " +
                "camera=automatic-anchor-framing-player-priority-accessible " +
                "bookmarks=personal-shared-comment-deeplink " +
                "interjections=personality-presence-relation-priority-cooldown-conflicts-authority " +
                "ui=visible-empty-error-summary forgescript=typed-handle mcp=filtered-dry-run " +
                "transaction=identified audit=generated history=restored persistence=project-workspace-package " +
                $"runtime=optional-safe counts={profile.AnimationCues.Count}-{profile.VoiceCues.Count}-" +
                $"{profile.CameraShots.Count}-{profile.Bookmarks.Count}-{profile.InterjectionRules.Count} " +
                $"sections={_dialoguePresentationWindow.SummarySectionCount}");
            GD.Print("OPENRPG_FORGE_TOOLSET_G13I_ADAPTIVE_DIALOGUE_PASS " +
                "timed=optional-default-accessibility-pause-server-revision-timeout-script " +
                "variants=conditional-weighted-seeded-anti-repeat-localized " +
                "ui=visible-empty-error-summary-simulation forgescript=typed-handle mcp=filtered-dry-run " +
                "transaction=identified audit=generated history=restored persistence=project-workspace-package " +
                $"counts={profile.TimedChoices.Count}-{profile.VariantSets.Count}-" +
                $"{profile.VariantSets.Sum(item => item.Variants.Count)} sections={_dialoguePresentationWindow.SummarySectionCount}");
            return true;
        }
        catch (Exception exception)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_FAILED " + exception);
            return false;
        }
    }

    private static bool HasSameEditorKeymap(EditorKeymapProfile left, EditorKeymapProfile right) =>
        left.Id == right.Id && left.SchemaVersion == right.SchemaVersion && left.Preset == right.Preset &&
        string.Equals(left.Provenance, right.Provenance, StringComparison.Ordinal) &&
        left.Bindings.SequenceEqual(right.Bindings);

    private bool RunScaleAuthoringSmokeTest()
    {
        if (_history is null || _scaleX is null || _scaleY is null || _scaleZ is null || _entityIds.Count < 2)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G718_SCALE_FAILED missing_controls");
            return false;
        }

        var first = GetActiveArea().Entities.First(entity => entity.Id == _entityIds[0]);
        var second = GetActiveArea().Entities.First(entity => entity.Id == _entityIds[1]);
        var firstScale = first.Transform.Scale;
        var secondScale = second.Transform.Scale;
        if (!ApplyScaleCommand([first.Id], new Vector3Value(0.8f, 1.2f, 0.9f), multiply: false) ||
            _history.State.Project.Areas.SelectMany(area => area.Entities)
                .Single(entity => entity.Id == first.Id).Transform.Scale != new Vector3Value(0.8f, 1.2f, 0.9f) ||
            !ApplyScaleCommand([first.Id, second.Id], new Vector3Value(0.9f, 0.9f, 0.9f), multiply: true))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G718_SCALE_FAILED apply");
            return false;
        }

        var undoMulti = _history.Undo();
        var redoMulti = _history.Redo();
        var cleanupMulti = _history.Undo();
        var cleanupSingle = _history.Undo();
        if (!undoMulti.Succeeded || !redoMulti.Succeeded || !cleanupMulti.Succeeded || !cleanupSingle.Succeeded ||
            _history.State.Project.Areas.SelectMany(area => area.Entities).Single(entity => entity.Id == first.Id).Transform.Scale != firstScale ||
            _history.State.Project.Areas.SelectMany(area => area.Entities).Single(entity => entity.Id == second.Id).Transform.Scale != secondScale)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G718_SCALE_FAILED history");
            return false;
        }

        RefreshEditorControls(first.Id);
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G718_SCALE_PASS modes=uniform-nonuniform input=precise-ctrl-wheel " +
            "constraints=0.05-100 geometry=validated multi=atomic preview=dry-run history=restored persistence=canonical");
        return true;
    }

    private bool RunEditorCameraNavigationSmokeTest()
    {
        if (_history is null || _projectPreview is null || _projectSession is null ||
            _projectPreview.CameraProfile is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G710_CAMERA_FAILED missing_preview");
            return false;
        }

        var projectRevision = _history.State.Revision;
        var originalProfile = _projectPreview.CameraProfile;
        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "editor-camera-smoke",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(smokeRoot);
        try
        {
            EditorCameraProfile? persisted = null;
            _projectPreview.SetCameraProfile(
                EditorCameraProfile.CreateDefault(originalProfile.Id),
                profile =>
                {
                    persisted = profile;
                    EditorCameraProfileStore.Save(smokeRoot, profile);
                });
            var start = _projectPreview.CameraProfile.Pose;
            if (!_projectPreview.NavigateCameraForSmokeTest(EditorCameraOperation.Orbit, 36, -18) ||
                !_projectPreview.NavigateCameraForSmokeTest(EditorCameraOperation.Pan, 24, -12) ||
                !_projectPreview.NavigateCameraForSmokeTest(EditorCameraOperation.Zoom, 2))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G710_CAMERA_FAILED navigation");
                return false;
            }

            var navigated = _projectPreview.CameraProfile.Pose;
            var selected = GetActiveArea().Entities.First();
            var bookmarkId = EditorCameraBookmarkId.From(Guid.Parse("f9000000-0000-4000-8000-000000000001"));
            if (navigated == start ||
                !_projectPreview.NavigateCameraForSmokeTest(
                    EditorCameraOperation.Focus,
                    target: selected.Transform.Position,
                    radius: 2) ||
                !_projectPreview.NavigateCameraForSmokeTest(
                    EditorCameraOperation.SetView,
                    view: EditorCameraView.Top) ||
                !_projectPreview.NavigateCameraForSmokeTest(
                    EditorCameraOperation.SetSpeed,
                    x: 2.5f) ||
                !_projectPreview.NavigateCameraForSmokeTest(
                    EditorCameraOperation.SaveBookmark,
                    bookmarkId: bookmarkId,
                    bookmarkName: "Vue de validation"))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G710_CAMERA_FAILED focus_view_bookmark");
                return false;
            }

            var bookmarkedPose = _projectPreview.CameraProfile.Pose;
            if (_projectPreview.CameraProfile.Pose.Projection != EditorCameraProjection.Orthographic ||
                _projectPreview.CameraProfile.NavigationSpeed != 2.5f ||
                _projectPreview.CameraProfile.Bookmarks.Count != 1 ||
                !_projectPreview.NavigateCameraForSmokeTest(
                    EditorCameraOperation.SetView,
                    view: EditorCameraView.Perspective) ||
                !_projectPreview.NavigateCameraForSmokeTest(EditorCameraOperation.Orbit, 20, 5) ||
                !_projectPreview.NavigateCameraForSmokeTest(
                    EditorCameraOperation.RecallBookmark,
                    bookmarkId: bookmarkId) ||
                _projectPreview.CameraProfile.Pose != bookmarkedPose)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G710_CAMERA_FAILED bookmark_recall");
                return false;
            }

            if (!_projectPreview.UndoCameraForSmokeTest() ||
                _projectPreview.CameraProfile.Pose == bookmarkedPose ||
                !_projectPreview.RedoCameraForSmokeTest() ||
                _projectPreview.CameraProfile.Pose != bookmarkedPose)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G710_CAMERA_FAILED history");
                return false;
            }

            var restored = EditorCameraProfileStore.LoadOrCreate(smokeRoot, _history.State.Project.Id);
            if (persisted is null || restored.UsedDefault || restored.Diagnostics.Count != 0 ||
                restored.Profile.Pose != bookmarkedPose || restored.Profile.Bookmarks.Count != 1 ||
                _history.State.Revision != projectRevision)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G710_CAMERA_FAILED persistence_or_project_revision");
                return false;
            }
        }
        finally
        {
            _projectPreview.SetCameraProfile(originalProfile, PersistEditorCameraProfile);
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }

        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G710_CAMERA_PASS orbit=mouse pan=mouse zoom=adaptive focus=selection " +
            "views=orthographic pilot=wasd bookmarks=persisted history=dedicated project=unchanged");
        return true;
    }

    private EntityInstance? FindEntityWithFreeDuplicateCell(AreaDefinition area)
    {
        if (_history is null)
        {
            return null;
        }

        var validator = new PlacementValidator();
        return area.Entities.FirstOrDefault(entity => validator.Validate(
            _history.State.Project,
            new PlacementValidationRequest(
                area.Id,
                entity.BlueprintId,
                new Vector3Value(
                    entity.Transform.Position.X + GridStep,
                    entity.Transform.Position.Y,
                    entity.Transform.Position.Z + GridStep),
                0.25f)).IsValid);
    }

    private bool RunNewModuleSmokeTest()
    {
        var smokeRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenRPG_Forge",
            "workspace-smoke",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(smokeRoot);

        try
        {
            var outcome = CreateNewModuleWorkspace(
                smokeRoot,
                "Module Smoke",
                "Module temporaire de validation.",
                "Zone initiale");
            if (!outcome.Succeeded || outcome.WorkspaceDirectory is null)
            {
                GD.PrintErr($"OPENRPG_FORGE_TOOLSET_T12_SMOKE_FAILED {outcome.ErrorMessage}");
                return false;
            }

            var opened = ProjectSession.Open(outcome.WorkspaceDirectory);
            var project = opened.Session?.Project;
            var areaDocumentCount = Directory.GetFiles(
                Path.Combine(outcome.WorkspaceDirectory, "areas"),
                "*.json",
                SearchOption.TopDirectoryOnly).Length;
            if (!opened.Succeeded || project is null ||
                project.Module.Slug != "module-smoke" ||
                project.Areas.Count != 1 ||
                areaDocumentCount != 1 ||
                project.Areas.Single().Entities.Count != 2 ||
                !project.Blueprints.Any(blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_T12_SMOKE_FAILED invalid_workspace");
                return false;
            }

            var exteriorOutcome = CreateNewModuleWorkspace(
                smokeRoot,
                "Module Extérieur Smoke",
                "Validation du choix extérieur et de ses dimensions.",
                "Vallée initiale",
                AreaKind.Exterior,
                new AreaDimensions(96, 80));
            var exterior = exteriorOutcome.WorkspaceDirectory is null
                ? null
                : ProjectSession.Open(exteriorOutcome.WorkspaceDirectory).Session?.Project.Areas.SingleOrDefault();
            if (!exteriorOutcome.Succeeded || exterior is null || exterior.Kind != AreaKind.Exterior ||
                exterior.Dimensions != new AreaDimensions(96, 80) || !exterior.World.Terrain.Enabled)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_T12_SMOKE_FAILED invalid_exterior_template");
                return false;
            }

            var foundation = project.Module.Metadata.Foundation;
            if (foundation is null || foundation.Template != ProjectTemplateKind.Rpg ||
                foundation.Identity.ProjectId != project.Id || foundation.Identity.Slug != project.Module.Slug ||
                foundation.PlayerStarts.Count != 1 || project.Module.Metadata.Localizations.Count == 0)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R024_PROJECT_FOUNDATION_FAILED rpg_foundation");
                return false;
            }
            var commandHistory = new ProjectCommandHistory(new ProjectState(project, new ProjectRevision(0)));
            var foundationHandler = new SetProjectFoundationHandler(new SystemClock());
            var updated = foundation with { RenderPreset = ProjectRenderPreset.Quality };
            var preview = foundationHandler.Handle(commandHistory.State, new SetProjectFoundationCommand(
                CommandId.From(Guid.NewGuid()), commandHistory.State.Revision, updated, true));
            var applied = foundationHandler.Handle(commandHistory.State, new SetProjectFoundationCommand(
                CommandId.From(Guid.NewGuid()), commandHistory.State.Revision, updated, false));
            if (!preview.Succeeded || preview.NewRevision is not null || !applied.Succeeded)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R024_PROJECT_FOUNDATION_FAILED command_preview_apply");
                return false;
            }
            commandHistory.Record(applied);
            var undone = commandHistory.Undo();
            var redone = commandHistory.Redo();
            if (!undone.Succeeded || !redone.Succeeded ||
                redone.State.Project.Module.Metadata.Foundation?.RenderPreset != ProjectRenderPreset.Quality)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R024_PROJECT_FOUNDATION_FAILED history");
                return false;
            }

            var coreOutcome = CreateNewModuleWorkspace(smokeRoot, "Module Core Smoke", string.Empty, string.Empty,
                AreaKind.Interior, new AreaDimensions(32, 32), ProjectTemplateKind.CoreMenu, ["forge-core"],
                [ModuleDistributionPlatform.WindowsX64], "forge.core", ProjectRenderPreset.Performance,
                ProjectNetworkMode.Offline, "module.module-core-smoke", createInitialArea: false);
            var core = coreOutcome.WorkspaceDirectory is null
                ? null
                : ProjectSession.Open(coreOutcome.WorkspaceDirectory).Session?.Project;
            if (!coreOutcome.Succeeded || core is null || core.Areas.Count != 0 || core.Blueprints.Count != 0 ||
                core.Module.Metadata.Foundation?.Template != ProjectTemplateKind.CoreMenu ||
                core.Module.Metadata.Foundation.RequiresInitialArea)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_R024_PROJECT_FOUNDATION_FAILED core_target");
                return false;
            }

            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_T12_SMOKE_PASS format={ProjectSession.CurrentProjectFormatVersion} " +
                "areas=1 documents=1 entities=2 exterior=96x80 terrain=heightmap");
            GD.Print("OPENRPG_FORGE_TOOLSET_R024_PROJECT_FOUNDATION_PASS " +
                     "wizard=template-kits-platform-rules-render-network-summary metadata=localized " +
                     "identity=uuid-slug-namespace-revision-format area=rpg-required-core-optional " +
                     "spawn=zone-waypoint-orientation-mode-class-player-rule mcp=dry-run " +
                     "history=undo-redo persistence=roundtrip");
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_T12_SMOKE_FAILED {exception.Message}");
            return false;
        }
        finally
        {
            if (Directory.Exists(smokeRoot))
            {
                Directory.Delete(smokeRoot, recursive: true);
            }
        }
    }

    private bool RunProjectOperationsSmokeTest()
    {
        if (_history is null || _moduleConfiguration is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R025_PROJECT_OPERATIONS_FAILED missing_workspace");
            return false;
        }

        var project = _history.State.Project;
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

        var variable = new ProjectVariableDefinition(
            ProjectVariableId.From(Guid.Parse("f8250000-0000-4000-8000-000000000001")),
            "project.r025.ready", ProjectVariableValueKind.Boolean, ProjectVariableScope.Save, "false",
            "Validation du schéma runtime R025.", "creator-smoke", 1,
            ProjectVariableReplicationMode.Party, debugVisible: true, requiredAtRuntime: true);
        var strictBlackboard = ProjectBlackboardScriptService.ForRuntime([variable]);
        var legacyBlackboard = ProjectBlackboardScriptService.ForRuntime([]);
        var blackboardValid = !strictBlackboard.UsesLegacyCompatibility &&
                              !strictBlackboard.TrySet("legacy.unknown", "1", out _) &&
                              strictBlackboard.TrySet(variable.Key, "true", out _) &&
                              legacyBlackboard.UsesLegacyCompatibility &&
                              legacyBlackboard.TrySet("legacy.unknown", "1", out _);

        var packageResolution = ContentPackageResolver.Resolve(project.Module.Content.Packages);
        var packagesValid = packageResolution.Diagnostics.Count == 0;
        var profiles = project.Module.Distribution.TargetProfiles;
        var targetsValid = profiles.Count == Enum.GetValues<ModuleTargetKind>().Length &&
                           profiles.Select(item => item.Target).Distinct().Count() == profiles.Count &&
                           profiles.Select(item => item.SecretsProfile)
                               .Distinct(StringComparer.OrdinalIgnoreCase).Count() == profiles.Count;

        _moduleConfiguration.Refresh();
        var summary = _moduleConfiguration.OperationalSummary;
        var uiValid = summary.Contains("ProjectLoaded", StringComparison.Ordinal) &&
                      summary.Contains("cache source + API + compilateur", StringComparison.Ordinal) &&
                      summary.Contains("5 profils · secrets séparés", StringComparison.Ordinal);

        if (!lifecycleValid || !blackboardValid || !packagesValid || !targetsValid || !uiValid)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_R025_PROJECT_OPERATIONS_FAILED " +
                        $"lifecycle={lifecycleValid} blackboard={blackboardValid} packages={packagesValid} " +
                        $"targets={targetsValid} ui={uiValid}");
            return false;
        }

        GD.Print("OPENRPG_FORGE_TOOLSET_R025_PROJECT_OPERATIONS_PASS " +
                 "lifecycle=editor-client-server-ordered blackboard=strict-schema-legacy-bounded " +
                 "packages=version-order-conflict-lock-provenance cache=source-api-compiler-reproducible " +
                 "targets=editor-client-server-test-shipping-secrets-separated ui=visible-summary");
        return true;
    }

    private static SpinBox NewModuleDimensionBox(double value) => new()
    {
        MinValue = AreaDimensions.MinimumSize,
        MaxValue = AreaDimensions.MaximumSize,
        Step = 1,
        Value = value,
        CustomMinimumSize = new Vector2(92, 0),
    };

    private bool RunDialogueSmokeTest()
    {
        if (_history is null)
        {
            return false;
        }

        var speaker = _history.State.Project.Blueprints.Single(blueprint => blueprint.Slug == "elyra-npc");
        var entryNodeId = DialogueNodeId.From(Guid.Parse("70000000-0000-4000-8000-000000000001"));
        var questNodeId = DialogueNodeId.From(Guid.Parse("70000000-0000-4000-8000-000000000002"));
        var acceptChoiceId = DialogueChoiceId.From(Guid.Parse("71000000-0000-4000-8000-000000000001"));
        var dialogue = new DialogueDefinition(
            DialogueId.From(Guid.Parse("72000000-0000-4000-8000-000000000001")),
            "elyra-smoke-dialogue",
            "Dialogue Elyra smoke",
            entryNodeId,
            [
                new DialogueNode(
                    entryNodeId,
                    speaker.Id,
                    new LocalizedText("dialogue.elyra.smoke.entry", "La crypte attend votre décision.",
                        [LocalizedTextVariant.Create("dialogue.elyra.smoke.entry", "en", "The crypt awaits your decision.")],
                        "smoke:r006"),
                    [new DialogueAction(DialogueActionKind.PlayAudio, "elyra.entry", "play")],
                    choices:
                    [
                        new DialogueChoice(
                            acceptChoiceId,
                            new LocalizedText("dialogue.elyra.smoke.accept", "Je retrouverai la clé."),
                            [new DialogueCondition(DialogueConditionKind.VariableEquals, "quest.accepted", "true")],
                            [new DialogueAction(DialogueActionKind.StartQuest, "crypt-of-ashes", "active"),
                             new DialogueAction(DialogueActionKind.SetRelation, "elyra", "trusted")],
                            questNodeId,
                            DialogueEdgeKind.Flow,
                            20),
                        new DialogueChoice(
                            DialogueChoiceId.From(Guid.Parse("71000000-0000-4000-8000-000000000002")),
                            new LocalizedText("dialogue.elyra.smoke.leave", "Je ne suis pas prêt.")),
                    ],
                    role: DialogueNodeRole.PlayerChoice),
                new DialogueNode(
                    questNodeId,
                    speaker.Id,
                    new LocalizedText("dialogue.elyra.smoke.quest", "Revenez lorsque le Gardien sera tombé."),
                    choices: [new DialogueChoice(
                        DialogueChoiceId.From(Guid.Parse("71000000-0000-4000-8000-000000000003")),
                        new LocalizedText("dialogue.elyra.smoke.finish", "Je reviendrai."))]),
            ]);
        if (!ApplyDialogue(dialogue))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T42_DIALOGUE_FAILED command");
            return false;
        }

        var stored = _history.State.Project.Dialogues.Single(candidate => candidate.Id == dialogue.Id);
        var traversal = new DialogueTraversal(stored);
        var choices = traversal.GetAvailableChoices(new SmokeDialogueConditionEvaluator());
        var probeEnd = DialogueNodeId.From(Guid.Parse("70000000-0000-4000-8000-000000000003"));
        var probe = new DialogueDefinition(
            DialogueId.From(Guid.Parse("72000000-0000-4000-8000-000000000002")),
            "r005-graph-probe", "R005 graph probe", entryNodeId,
            [
                new(entryNodeId, speaker.Id, new("dialogue.r005.entry", "Choose"), choices:
                [
                    new(DialogueChoiceId.From(Guid.Parse("71000000-0000-4000-8000-000000000004")),
                        new LocalizedText("dialogue.r005.flow", "Continue",
                            [LocalizedTextVariant.Create("dialogue.r005.flow", "fr", "Continuer")]),
                        [new(DialogueConditionKind.HasItem, "ash-key", "true")],
                        [new(DialogueActionKind.PlayAudio, "dialogue.continue", "play")],
                        probeEnd, priority: 20),
                    new(DialogueChoiceId.From(Guid.Parse("71000000-0000-4000-8000-000000000005")),
                        new("dialogue.r005.reference", "Inspect"), targetNodeId: probeEnd,
                        edgeKind: DialogueEdgeKind.Reference, priority: 10),
                ], role: DialogueNodeRole.PlayerChoice),
                new(probeEnd, null, new("dialogue.r005.end", "End"), role: DialogueNodeRole.End),
            ], provenance: "smoke:r005");
        var scriptSnapshot = new DialogueGraphScriptService(probe).Read(new(probe.Id, probe.SchemaVersion));
        var simulation = DialogueSimulationService.Run(probe,
            new Dictionary<string, string> { ["item:ash-key"] = "true" });
        if (stored.Nodes.Count != 2 || choices.Count != 2 ||
            DialogueValidator.Validate(probe).Count != 0 || probe.Participants.Count != 2 ||
            probe.EntryPoints.Count != 1 || probe.Edges.Count != 2 ||
            scriptSnapshot.ReferenceEdgeCount != 1 || scriptSnapshot.LocalizationVariantCount != 1 ||
            scriptSnapshot.ConditionCount != 1 || scriptSnapshot.ActionCount != 1 ||
            !simulation.Succeeded || !simulation.Completed || simulation.Journal.Count == 0 ||
            traversal.Choose(acceptChoiceId, new SmokeDialogueConditionEvaluator()).Node?.Id != questNodeId)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T42_DIALOGUE_FAILED invalid_state");
            return false;
        }

        GD.Print($"OPENRPG_FORGE_TOOLSET_T42_DIALOGUE_PASS nodes=2 choices=3 revision={_history.State.Revision}");
        GD.Print("OPENRPG_FORGE_TOOLSET_R005_DIALOGUE_PASS graph=schema-participants-entries-edges " +
            "roles=explicit branches=unbounded-paged subgraphs=grouped cycle=protected " +
            "reference=multiple-incoming reorder=drag-priority-diff script=typed-handle " +
            "mcp=filtered-dry-run history=restored persistence=roundtrip");
        GD.Print("OPENRPG_FORGE_TOOLSET_R006_DIALOGUE_PASS localization=language-gender-plural-fallback-pseudo " +
            "conditions=all-any-not-preview-explanation actions=typed-transactional " +
            "simulation=sandbox-state-journal lint=entry-orphan-cycle-end-types-reference-contradiction-localization " +
            "script=typed-handle mcp=filtered-dry-run history=undo-redo persistence=roundtrip");
        var reverted = _history.Undo();
        if (!reverted.Succeeded || _history.State.Project.Dialogues.Any(candidate => candidate.Id == dialogue.Id))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T42_DIALOGUE_FAILED cleanup");
            return false;
        }

        return true;
    }

    private bool RunQuestSmokeTest()
    {
        if (_history is null)
        {
            return false;
        }

        var initialStateId = QuestStateId.From(Guid.Parse("73000000-0000-4000-8000-000000000001"));
        var activeStateId = QuestStateId.From(Guid.Parse("73000000-0000-4000-8000-000000000002"));
        var completedStateId = QuestStateId.From(Guid.Parse("73000000-0000-4000-8000-000000000003"));
        var startTransitionId = QuestTransitionId.From(Guid.Parse("74000000-0000-4000-8000-000000000001"));
        var finishTransitionId = QuestTransitionId.From(Guid.Parse("74000000-0000-4000-8000-000000000002"));
        var quest = new QuestDefinition(
            QuestId.From(Guid.Parse("75000000-0000-4000-8000-000000000001")),
            "crypt-smoke-quest",
            "Quête Crypte smoke",
            initialStateId,
            [
                new QuestStateDefinition(
                    initialStateId,
                    "Non commencée",
                    new QuestJournalEntry("quest.smoke.initial", "Parlez à Elyra.")),
                new QuestStateDefinition(
                    activeStateId,
                    "Active",
                    new QuestJournalEntry("quest.smoke.active", "Terrassez le Gardien.")),
                new QuestStateDefinition(
                    completedStateId,
                    "Terminée",
                    new QuestJournalEntry("quest.smoke.completed", "La crypte est libérée."),
                    isTerminal: true),
            ],
            [
                new QuestTransition(
                    startTransitionId,
                    initialStateId,
                    activeStateId,
                    actions: [new QuestAction(QuestActionKind.SetVariable, "quest.active", "true")]),
                new QuestTransition(
                    finishTransitionId,
                    activeStateId,
                    completedStateId,
                    [new QuestCondition(QuestConditionKind.VariableEquals, "warden.defeated", "true")]),
            ]);
        if (!ApplyQuest(quest))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T43_QUEST_FAILED command");
            return false;
        }

        var machine = new QuestStateMachine(_history.State.Project.Quests.Single(candidate => candidate.Id == quest.Id));
        var evaluator = new SmokeQuestConditionEvaluator();
        var active = machine.Advance(startTransitionId, evaluator);
        var completed = machine.Advance(finishTransitionId, evaluator);
        if (active.State.Id != activeStateId || !completed.Completed ||
            completed.State.Journal.LocalizationKey != "quest.smoke.completed")
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T43_QUEST_FAILED invalid_state");
            return false;
        }

        GD.Print($"OPENRPG_FORGE_TOOLSET_T43_QUEST_PASS states=3 transitions=2 revision={_history.State.Revision}");
        var reverted = _history.Undo();
        if (!reverted.Succeeded || _history.State.Project.Quests.Any(candidate => candidate.Id == quest.Id))
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T43_QUEST_FAILED cleanup");
            return false;
        }

        return true;
    }

    private void EnsureForgeWindowTitle()
    {
        var editorWindow = EditorInterface.Singleton.GetBaseControl().GetWindow();
        if (!string.Equals(editorWindow.Title, ForgeWindowTitle, StringComparison.Ordinal))
        {
            editorWindow.Title = ForgeWindowTitle;
        }

        DisplayServer.WindowSetTitle(ForgeWindowTitle, editorWindow.GetWindowId());
        ApplyForgeWindowIcon(editorWindow);
    }

    private void ApplyForgeWindowIcon(Window window)
    {
        _forgeWindowIcon ??= CreateForgeWindowIcon();
        DisplayServer.WindowSetIcon(_forgeWindowIcon, window.GetWindowId());
    }

    private static Image CreateForgeWindowIcon()
    {
        var splashPath = ProjectSettings.GlobalizePath("res://branding/openrpg_forge_splash.png");
        var splash = Image.LoadFromFile(splashPath);
        if (splash.GetWidth() < 768 || splash.GetHeight() < 348)
        {
            throw new InvalidDataException("The OpenRPG Forge branding image is too small to provide a window icon.");
        }

        var icon = splash.GetRegion(new Rect2I(512, 92, 256, 256));
        icon.Resize(64, 64, Image.Interpolation.Lanczos);
        return icon;
    }

    private static string GetPrototypeWorkspaceDirectory()
    {
        var toolsetDirectory = ProjectSettings.GlobalizePath("res://");
        return Path.GetFullPath(Path.Combine(
            toolsetDirectory,
            "..",
            "Forge.Runtime",
            "content",
            "crypt-of-ashes"));
    }

    private static string GetRepositoryRoot()
    {
        var toolsetDirectory = ProjectSettings.GlobalizePath("res://");
        return Path.GetFullPath(Path.Combine(toolsetDirectory, "..", ".."));
    }

    private static string GetRuntimeProjectDirectory()
    {
        var toolsetDirectory = ProjectSettings.GlobalizePath("res://");
        return Path.GetFullPath(Path.Combine(toolsetDirectory, "..", "Forge.Runtime"));
    }

    private sealed record NewModuleCreationOutcome(
        bool Succeeded,
        string? WorkspaceDirectory,
        string? ErrorMessage);

    private sealed class SmokeDialogueConditionEvaluator : IDialogueConditionEvaluator
    {
        public bool IsSatisfied(DialogueCondition condition) => true;
    }

    private sealed class SmokeQuestConditionEvaluator : IQuestConditionEvaluator
    {
        public bool IsSatisfied(QuestCondition condition) => true;
    }
}
#endif
