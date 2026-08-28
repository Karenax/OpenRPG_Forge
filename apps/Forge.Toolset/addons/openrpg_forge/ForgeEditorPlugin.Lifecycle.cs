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
using Forge.Domain.Combat;
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
using Forge.EngineAdapter.Audio;
using Forge.EngineAdapter.Rendering;
using Forge.EngineAdapter.World;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Assets;
using Forge.ModuleFormat.Content;
using Forge.ModuleFormat.Distribution;
using Forge.ModuleFormat.Diagnostics;
using Forge.ModuleFormat.Launcher;
using Forge.ModuleFormat.Navigation;
using Forge.ModuleFormat.Playtesting;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Quality;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Content;
using Forge.Network.Replication;
using Forge.RpgKit.Application.Abilities;
using Forge.RpgKit.Application.Bosses;
using Forge.RpgKit.Application.Companions;
using Forge.RpgKit.Application.Combat;
using Forge.RpgKit.Application.Creatures;
using Forge.RpgKit.Application.Dialogues;
using Forge.RpgKit.Application.Doors;
using Forge.RpgKit.Application.Encounters;
using Forge.RpgKit.Application.Factions;
using Forge.RpgKit.Application.Merchants;
using Forge.RpgKit.Application.Placeables;
using Forge.RpgKit.Application.Quests;
using Forge.RpgKit.Application.Triggers;
using Forge.RpgKit.Domain.Merchants;
using Forge.RpgKit.Domain.Companions;
using Forge.RpgKit.Domain.Factions;
using RpgLockState = Forge.RpgKit.Domain.Placeables.LockState;
using RpgActivator = Forge.RpgKit.Domain.Encounters.Activator;
using RpgNetworkAuthority = Forge.RpgKit.Domain.Encounters.NetworkAuthority;
using RpgQueryableEntity = Forge.RpgKit.Domain.Triggers.QueryableEntity;
using RpgTriggerEventTrace = Forge.RpgKit.Domain.Triggers.TriggerEventTrace;
using RpgTriggerRuntimeState = Forge.RpgKit.Domain.Triggers.TriggerRuntimeState;
using Forge.Scripting;
using Godot;

namespace Forge.Toolset.Editor;

public partial class ForgeEditorPlugin
{
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
        _gameDistributionDialog = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenDir,
            Title = "Choisir le dossier qui recevra le jeu et son lanceur",
        };
        _mainPanel.AddChild(_gameDistributionDialog);
        CreateNewModuleDialogs();
        CreateEntityPropertiesDialog();
        _dialogueEditor = new DialogueEditorWindow();
        _dialogueEditor.Initialize(
            () => _history?.State,
            ApplyDialogue,
            dialogueId => DeleteAuthoringElement(AuthoringElementKind.Dialogue, dialogueId.Value),
            dialogueId => _dialoguePresentationWindow?.Open(dialogueId),
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_dialogueEditor);
        _questJournalWindow = new QuestJournalWindow();
        _questJournalWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyQuestJournal,
            questId => DeleteAuthoringElement(AuthoringElementKind.Quest, questId.Value),
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
            blueprintId => DeleteAuthoringElement(AuthoringElementKind.Blueprint, blueprintId.Value),
            CreateBlueprintFromSelectedEntity,
            ConfigureBlueprintPalette,
            UpdateBlueprintFromEntity,
            ConfigureBlueprintPrefab,
            PlacePrefabComposition,
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
        _creatureAuthoringCompositionWindow = new CreatureAuthoringCompositionWindow();
        _creatureAuthoringCompositionWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyCreatureFoundation,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_creatureAuthoringCompositionWindow);
        _creatureRulesCompositionWindow = new CreatureRulesCompositionWindow();
        _creatureRulesCompositionWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyCreatureFoundation,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_creatureRulesCompositionWindow);
        _creatureInteractionCompositionWindow = new CreatureInteractionCompositionWindow();
        _creatureInteractionCompositionWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyCreatureFoundation,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_creatureInteractionCompositionWindow);
        _creatureEventAiCompositionWindow = new CreatureEventAiCompositionWindow();
        _creatureEventAiCompositionWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyCreatureFoundation,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_creatureEventAiCompositionWindow);
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
        _itemGameplayWindow = new ItemGameplayWindow();
        _itemGameplayWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyItemFoundation,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_itemGameplayWindow);
        _itemOperationsWindow = new ItemOperationsWindow();
        _itemOperationsWindow.Initialize(
            () => _history?.State,
            PreviewOrApplyItemFoundation,
            Undo,
            Redo,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_itemOperationsWindow);
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
            areaId => DeleteAuthoringElement(AuthoringElementKind.Area, areaId.Value),
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
        _areaAuthoringWindow.Initialize(() => _history?.State, () => _activeAreaContext.Current,
            ApplyAreaAuthoring, SelectAreaLayerEntities, ApplyForgeWindowIcon);
        _mainPanel.AddChild(_areaAuthoringWindow);
        _areaFoundationWindow = new AreaFoundationWindow();
        _areaFoundationWindow.Initialize(() => _history?.State, () => _activeAreaContext.Current,
            ApplyAreaFoundation, ApplyAreaTileEdit, ApplyForgeWindowIcon);
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
            () => _creatureAuthoringCompositionWindow?.Open(),
            () => _creatureRulesCompositionWindow?.Open(),
            () => _creatureInteractionCompositionWindow?.Open(),
            () => _creatureEventAiCompositionWindow?.Open(),
            () => _creaturePresentationWindow?.Open(),
            () => _creatureProgressionWindow?.Open(),
            () => _creatureLootWindow?.Open(),
            () => _creatureRoleBehaviorWindow?.Open(),
            () => _creatureEventWindow?.Open(),
            () => _factionAdvancedWindow?.Open(),
            () => _questOperationsWindow?.Open(),
            () => _itemFoundationWindow?.Open(),
            () => _itemGameplayWindow?.Open(),
            () => _itemOperationsWindow?.Open(),
            () => _itemAdvancedWindow?.Open(),
            () => _worldFeatureStudioWindow?.Open(),
            () => _postLaunchStudioWindow?.Open(),
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_rpgKitAuthoringWindow);
        _networkAssuranceWindow = new NetworkAssuranceWindow();
        _networkAssuranceWindow.Initialize(ApplyForgeWindowIcon);
        _mainPanel.AddChild(_networkAssuranceWindow);
        _networkBetaWindow = new NetworkBetaWindow();
        _networkBetaWindow.Initialize(ApplyForgeWindowIcon, () => _networkAssuranceWindow?.Open());
        _mainPanel.AddChild(_networkBetaWindow);
        _liveDirectorAccessWindow = new LiveDirectorAccessWindow();
        _liveDirectorAccessWindow.Initialize(ApplyForgeWindowIcon);
        _mainPanel.AddChild(_liveDirectorAccessWindow);
        _liveDirectorWindow = new LiveDirectorWindow();
        _liveDirectorWindow.Initialize(ApplyForgeWindowIcon, () => _liveDirectorAccessWindow?.Open());
        _mainPanel.AddChild(_liveDirectorWindow);
        _workspaceWindow = new CreatorWorkspaceWindow();
        _workspaceWindow.Initialize(
            () => _history?.State,
            LoadWorkspaceProfile,
            SaveWorkspaceProfile,
            () => _editorKeymapWindow?.Open(),
            () => _productivityWindow?.Open(),
            () => _history?.State.Project.Module.ArtisticStyle ??
                  ArtisticStyleProfile.CreateDefault(Guid.Parse("10000000-0000-4000-8000-000000000001")),
            ApplyArtisticStyleProfile,
            ActivateArea,
            ApplyWorkspaceFilter,
            PlaceRepeatedSeries,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_workspaceWindow);
        _multiAreaSplitViewWindow = new MultiAreaSplitViewWindow();
        _multiAreaSplitViewWindow.Initialize(() => _history?.State, () => _projectSession?.RootDirectory,
            () => _assetPackageManifestHistory?.State, ApplyForgeWindowIcon);
        _mainPanel.AddChild(_multiAreaSplitViewWindow);
        _productivityWindow = new CreatorProductivityWindow();
        _productivityWindow.Initialize(() => _history?.State, LoadWorkspaceProfile, SaveWorkspaceProfile,
            ApplyWorkspaceFilter, PlaceRepeatedSeriesAdvanced, CopySelectionToComparisonArea,
            (left, right, axis) => _multiAreaSplitViewWindow?.Open(left, right, axis), ApplyForgeWindowIcon);
        _mainPanel.AddChild(_productivityWindow);
        _recoveryWindow = new RecoveryWindow();
        _recoveryWindow.Initialize(ListRecoverySnapshots, LoadRecoveryPolicy,
            () => _recoverySessionInspection, PreviewRecoverySnapshot, RestoreRecoverySnapshot,
            ConfigureRecoveryPolicy, CaptureRecoverySnapshot, ClearRecoverySnapshots, ApplyForgeWindowIcon);
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
            ShowGameDistributionDialog,
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
            ApplyTerrainBrush,
            ShowViewportPlacementFeedback);
        _mainPanel.GetNode<Control>("Margin/Content/PreviewHost").AddChild(_projectPreview);
        _creatorShell = new ForgeCreatorShell(_mainPanel);
        ConnectSignals();

        // Every specialized editor is prepared while the panel is still detached. This prevents
        // Window's default visible state from opening the entire tool catalogue during startup.
        _toolWindowManager = new CreatorToolWindowManager(_mainPanel, ApplyForgeWindowIcon);
        _toolWindowManager.PrepareExistingWindows();
        EditorInterface.Singleton.GetEditorMainScreen().AddChild(_mainPanel);
        _toolWindowManager.NormalizeAfterTreeReady();
        SetProcess(true);
        SetProcessInput(true);
        EnsureForgeWindowTitle();
        _MakeVisible(false);
        var userArguments = OS.GetCmdlineUserArgs();
        _desktopCreatorShell = userArguments.Contains("--creator-shell", StringComparer.Ordinal);
        _creatorShellNormalizationFrames = _desktopCreatorShell ? 180 : 0;
        _mainThreadActions.Enqueue(() =>
        {
            EditorInterface.Singleton.SetMainScreenEditor("Forge");
            if (_desktopCreatorShell)
            {
                EditorInterface.Singleton.DistractionFreeMode = true;
                HideBottomPanel();
            }
        });
        var startupWorkspace = GetStartupWorkspaceDirectory(userArguments);
        GD.Print($"OPENRPG_FORGE_CREATOR_STARTUP_MODULE slug={Path.GetFileName(startupWorkspace)}");
        OpenProjectDirectory(startupWorkspace);

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

        var r053AreaEditingCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r053-area-tile-editing-ui-capture=", StringComparison.Ordinal));
        if (r053AreaEditingCapture is not null)
        {
            _areaFoundationCapturePath = r053AreaEditingCapture[
                "--r053-area-tile-editing-ui-capture=".Length..];
            _areaFoundationCaptureAdvanced = true;
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

        var r077CreatureAuthoringCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r077-creature-authoring-ui-capture=", StringComparison.Ordinal));
        if (r077CreatureAuthoringCapture is not null)
            _r077CreatureAuthoringCapturePath = r077CreatureAuthoringCapture[
                "--r077-creature-authoring-ui-capture=".Length..];

        var r078CreatureRulesCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r078-creature-rules-ui-capture=", StringComparison.Ordinal));
        if (r078CreatureRulesCapture is not null)
            _r078CreatureRulesCapturePath = r078CreatureRulesCapture[
                "--r078-creature-rules-ui-capture=".Length..];

        var r079CreatureInteractionCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r079-creature-interaction-ui-capture=", StringComparison.Ordinal));
        if (r079CreatureInteractionCapture is not null)
            _r079CreatureInteractionCapturePath = r079CreatureInteractionCapture[
                "--r079-creature-interaction-ui-capture=".Length..];

        var r080CreatureEventAiCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r080-creature-event-ai-ui-capture=", StringComparison.Ordinal));
        if (r080CreatureEventAiCapture is not null)
            _r080CreatureEventAiCapturePath = r080CreatureEventAiCapture[
                "--r080-creature-event-ai-ui-capture=".Length..];

        var r039CreatureEventsCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r039-creature-events-ui-capture=", StringComparison.Ordinal));
        if (r039CreatureEventsCapture is not null)
            _r039CreatureEventsCapturePath = r039CreatureEventsCapture[
                "--r039-creature-events-ui-capture=".Length..];

        var r044ItemFoundationCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r044-item-foundation-ui-capture=", StringComparison.Ordinal));
        var r088ItemCreationCapture = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--r088-item-creation-ui-capture=", StringComparison.Ordinal));
        if (r088ItemCreationCapture is not null)
        {
            _r044ItemFoundationCapturePath = r088ItemCreationCapture[
                "--r088-item-creation-ui-capture=".Length..];
            _itemFoundationCaptureMarker = "R088_ITEM_CREATION";
        }
        else if (r044ItemFoundationCapture is not null)
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
        EndRecoverySession();
        SetProcessInput(false);
        _nativeUndoRedo = null;
        DisconnectSignals();
        _toolWindowManager?.Dispose();
        _toolWindowManager = null;
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
        _gameDistributionDialog = null;
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
        _areaFoundationCaptureAdvanced = false;
        _areaRuntimeCapturePath = null;
        _r035DiagnosticsCapturePath = null;
        _r036ScaleCapturePath = null;
        _r038CreatureFoundationCapturePath = null;
        _r077CreatureAuthoringCapturePath = null;
        _r078CreatureRulesCapturePath = null;
        _r079CreatureInteractionCapturePath = null;
        _r080CreatureEventAiCapturePath = null;
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
        _creatureAuthoringCompositionWindow = null;
        _creatureRulesCompositionWindow = null;
        _creatureProgressionWindow = null;
        _creatureLootWindow = null;
        _creatureRoleBehaviorWindow = null;
        _creatureEventWindow = null;
        _questOperationsWindow = null;
        _itemAdvancedWindow = null;
        _itemFoundationWindow = null;
        _itemGameplayWindow = null;
        _itemOperationsWindow = null;
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
        _productivityWindow = null;
        _multiAreaSplitViewWindow = null;
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
        if (_creatorShellNormalizationFrames > 0)
        {
            EditorInterface.Singleton.DistractionFreeMode = true;
            HideBottomPanel();
            _creatorShellNormalizationFrames--;
        }

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
                GD.Print($"OPENRPG_FORGE_{_itemFoundationCaptureMarker}_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
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

        if (_r077CreatureAuthoringCapturePath is not null)
        {
            _r077CreatureAuthoringCaptureElapsed += delta;
            if (!_r077CreatureAuthoringCapturePrepared && _r077CreatureAuthoringCaptureElapsed >= 4 &&
                _creatureAuthoringCompositionWindow is not null)
            {
                _creatureAuthoringCompositionWindow.Open();
                _r077CreatureAuthoringCapturePrepared = _creatureAuthoringCompositionWindow.PrepareForCapture();
                _r077CreatureAuthoringCaptureElapsed = 0;
            }
            else if (_r077CreatureAuthoringCapturePrepared &&
                     _creatureAuthoringCompositionWindow?.Visible == true &&
                     _r077CreatureAuthoringCaptureElapsed >= 2)
            {
                var destination = Path.GetFullPath(_r077CreatureAuthoringCapturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var error = _creatureAuthoringCompositionWindow.GetViewport().GetTexture().GetImage().SavePng(destination);
                GD.Print($"OPENRPG_FORGE_R077_CREATURE_AUTHORING_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
                _r077CreatureAuthoringCapturePath = null;
                GetTree().Quit(error == Error.Ok ? 0 : 1);
            }
        }

        if (_r078CreatureRulesCapturePath is not null)
        {
            _r078CreatureRulesCaptureElapsed += delta;
            if (!_r078CreatureRulesCapturePrepared && _r078CreatureRulesCaptureElapsed >= 4 &&
                _creatureRulesCompositionWindow is not null)
            {
                _creatureRulesCompositionWindow.Open();
                _r078CreatureRulesCapturePrepared = _creatureRulesCompositionWindow.PrepareForCapture();
                _r078CreatureRulesCaptureElapsed = 0;
            }
            else if (_r078CreatureRulesCapturePrepared &&
                     _creatureRulesCompositionWindow?.Visible == true &&
                     _r078CreatureRulesCaptureElapsed >= 2)
            {
                var destination = Path.GetFullPath(_r078CreatureRulesCapturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var error = _creatureRulesCompositionWindow.GetViewport().GetTexture().GetImage().SavePng(destination);
                GD.Print($"OPENRPG_FORGE_R078_CREATURE_RULES_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
                _r078CreatureRulesCapturePath = null;
                GetTree().Quit(error == Error.Ok ? 0 : 1);
            }
        }

        if (_r079CreatureInteractionCapturePath is not null)
        {
            _r079CreatureInteractionCaptureElapsed += delta;
            if (!_r079CreatureInteractionCapturePrepared && _r079CreatureInteractionCaptureElapsed >= 4 &&
                _creatureInteractionCompositionWindow is not null)
            {
                _creatureInteractionCompositionWindow.Open();
                _r079CreatureInteractionCapturePrepared =
                    _creatureInteractionCompositionWindow.PrepareForCapture();
                _r079CreatureInteractionCaptureElapsed = 0;
            }
            else if (_r079CreatureInteractionCapturePrepared &&
                     _creatureInteractionCompositionWindow?.Visible == true &&
                     _r079CreatureInteractionCaptureElapsed >= 2)
            {
                var destination = Path.GetFullPath(_r079CreatureInteractionCapturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var error = _creatureInteractionCompositionWindow.GetViewport().GetTexture().GetImage()
                    .SavePng(destination);
                GD.Print($"OPENRPG_FORGE_R079_CREATURE_INTERACTION_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
                _r079CreatureInteractionCapturePath = null;
                GetTree().Quit(error == Error.Ok ? 0 : 1);
            }
        }

        if (_r080CreatureEventAiCapturePath is not null)
        {
            _r080CreatureEventAiCaptureElapsed += delta;
            if (!_r080CreatureEventAiCapturePrepared && _r080CreatureEventAiCaptureElapsed >= 4 &&
                _creatureEventAiCompositionWindow is not null)
            {
                _creatureEventAiCompositionWindow.Open();
                _r080CreatureEventAiCapturePrepared = _creatureEventAiCompositionWindow.PrepareForCapture();
                _r080CreatureEventAiCaptureElapsed = 0;
            }
            else if (_r080CreatureEventAiCapturePrepared &&
                     _creatureEventAiCompositionWindow?.Visible == true &&
                     _r080CreatureEventAiCaptureElapsed >= 2)
            {
                var destination = Path.GetFullPath(_r080CreatureEventAiCapturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var error = _creatureEventAiCompositionWindow.GetViewport().GetTexture().GetImage()
                    .SavePng(destination);
                GD.Print($"OPENRPG_FORGE_R080_CREATURE_EVENT_AI_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
                _r080CreatureEventAiCapturePath = null;
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
                if (_areaFoundationCaptureAdvanced
                        ? _areaFoundationWindow.PrepareR053ForSmokeTest()
                        : _areaFoundationWindow.PrepareForSmokeTest())
                {
                    _areaFoundationCapturePrepared = true;
                    if (_areaFoundationCaptureAdvanced) _areaFoundationWindow.ShowAdvancedForCapture();
                    _areaFoundationCaptureElapsed = 0;
                }
            }
            else if (_areaFoundationCapturePrepared && _areaFoundationWindow?.Visible == true &&
                     _areaFoundationCaptureElapsed >= 2)
            {
                var destination = Path.GetFullPath(_areaFoundationCapturePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                var error = _areaFoundationWindow.GetViewport().GetTexture().GetImage().SavePng(destination);
                var marker = _areaFoundationCaptureAdvanced
                    ? "OPENRPG_FORGE_R053_AREA_TILE_EDITING_UI_CAPTURE"
                    : "OPENRPG_FORGE_R028_AREA_FOUNDATION_UI_CAPTURE";
                GD.Print($"{marker}_{(error == Error.Ok ? "PASS" : "FAILED")} path={destination}");
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
            PlaceholderText = "Module de démonstration",
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
        _gameDistributionDialog!.DirSelected += ExportGameDistribution;
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
        if (_gameDistributionDialog is not null) _gameDistributionDialog.DirSelected -= ExportGameDistribution;
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


}
#endif
