#if TOOLS
using System.Collections.Concurrent;
using System.IO.Compression;
using Forge.Application.Areas;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Dialogues;
using Forge.Application.Entities;
using Forge.Application.Localization;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.Spatial;
using Forge.Application.Time;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Localization;
using Forge.Domain.Modules;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Spatial;
using Forge.Domain.Validation;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Navigation;
using Forge.ModuleFormat.Playtesting;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Workspaces;
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
    private readonly CreateAreaHandler _createAreaHandler = new(new SystemClock());
    private readonly DuplicateAreaHandler _duplicateAreaHandler = new(new SystemClock());
    private readonly UpsertPortalLinkHandler _upsertPortalHandler = new(new SystemClock());
    private readonly TransformEntityHandler _transformHandler = new(new SystemClock());
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
    private readonly SetLocalizationCatalogHandler _setLocalizationCatalogHandler = new(new SystemClock());
    private readonly SetAreaNavigationHandler _setAreaNavigationHandler = new(new SystemClock());
    private readonly ApplyDiagnosticQuickFixHandler _diagnosticQuickFixHandler = new(new SystemClock());
    private readonly ConcurrentQueue<Action> _mainThreadActions = new();
    private readonly CreatorSelectionContext _selectionContext = new();
    private readonly ActiveAreaContext _activeAreaContext = new();
    private readonly SemanticClipboard _semanticClipboard = new();
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
    private Button? _overridesButton;
    private Button? _copyButton;
    private Button? _pasteButton;
    private Button? _duplicateButton;
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
    private LineEdit? _newModuleAreaName;
    private Button? _newModuleNextButton;
    private Button? _newModuleCancelButton;
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
    private BlueprintCatalogWindow? _blueprintCatalog;
    private AssetContentBrowserWindow? _assetBrowser;
    private LocalizationWorkspaceWindow? _localizationWorkspace;
    private GlobalSearchWindow? _globalSearch;
    private ModuleConfigurationWindow? _moduleConfiguration;
    private AreaWorkspaceWindow? _areaWorkspace;
    private BuildPipelineWindow? _buildPipeline;
    private DistributionProfilesWindow? _distributionProfiles;
    private NavigationConfigurationWindow? _navigationConfiguration;
    private ForgeCreatorShell? _creatorShell;
    private ProjectSession? _projectSession;
    private ProjectPreview3D? _projectPreview;
    private ProjectCommandHistory? _history;
    private Image? _forgeWindowIcon;
    private bool _playtestRunning;
    private bool _showEntityPropertiesDialogWhenReady;
    private bool _showDialogueEditorWhenReady;
    private ModuleTargetKind _distributionTarget = ModuleTargetKind.Editor;
    private int _selectedDraftOverrideIndex = -1;
    private double _editorReadyElapsed;
    private double _titleRefreshElapsed;

    public override void _EnterTree()
    {
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
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_dialogueEditor);
        _blueprintCatalog = new BlueprintCatalogWindow();
        _blueprintCatalog.Initialize(
            () => _history?.State,
            UpsertBlueprint,
            CreateBlueprintFromSelectedEntity,
            ConfigureBlueprintPalette,
            selection => { _selectionContext.Replace(selection); },
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_blueprintCatalog);
        _assetBrowser = new AssetContentBrowserWindow();
        _assetBrowser.Initialize(
            GetRepositoryRoot,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_assetBrowser);
        _localizationWorkspace = new LocalizationWorkspaceWindow();
        _localizationWorkspace.Initialize(
            () => _history?.State,
            ApplyLocalizationCatalog,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_localizationWorkspace);
        _globalSearch = new GlobalSearchWindow();
        _globalSearch.Initialize(
            () => _history?.State,
            GetSearchDiagnostics,
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
            DuplicateArea,
            OpenNavigationConfiguration,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_areaWorkspace);
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
        _distributionProfiles = new DistributionProfilesWindow();
        _distributionProfiles.Initialize(
            () => _history?.State,
            ApplyModuleDistribution,
            target => _distributionTarget = target,
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_distributionProfiles);
        _projectPreview = new ProjectPreview3D();
        _projectPreview.Initialize(
            () => _history?.State,
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
        EnsureForgeWindowTitle();
        _MakeVisible(false);
        _mainThreadActions.Enqueue(() => EditorInterface.Singleton.SetMainScreenEditor("Forge"));
        OpenProjectDirectory(GetPrototypeWorkspaceDirectory());

        var userArguments = OS.GetCmdlineUserArgs();
        if (userArguments.Contains("--show-new-module-dialog", StringComparer.Ordinal))
        {
            _mainThreadActions.Enqueue(ShowNewModuleDialog);
        }

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
        _overridesButton = null;
        _copyButton = null;
        _pasteButton = null;
        _duplicateButton = null;
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
        _newModuleAreaName = null;
        _newModuleNextButton = null;
        _newModuleCancelButton = null;
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
        _blueprintCatalog = null;
        _assetBrowser = null;
        _localizationWorkspace = null;
        _globalSearch = null;
        _moduleConfiguration = null;
        _areaWorkspace = null;
        _buildPipeline = null;
        _distributionProfiles = null;
        _navigationConfiguration = null;
        _creatorShell = null;
        _projectSession = null;
        _projectPreview = null;
        _history = null;
        GD.Print("OPENRPG_FORGE_PLUGIN_UNLOADED");
    }

    public override void _Process(double delta)
    {
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
        _overridesButton = GetControl<Button>("Margin/Content/Editing/OverridesRow/OverridesButton");
        _copyButton = GetControl<Button>("Margin/Content/Editing/ClipboardRow/CopyButton");
        _pasteButton = GetControl<Button>("Margin/Content/Editing/ClipboardRow/PasteButton");
        _duplicateButton = GetControl<Button>("Margin/Content/Editing/ClipboardRow/DuplicateButton");
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
            Title = "Créer un nouveau module Forge",
            MinSize = new Vector2I(600, 500),
            Size = new Vector2I(600, 500),
            Exclusive = false,
            Transient = true,
            Unresizable = true,
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

        var form = new VBoxContainer();
        form.AddThemeConstantOverride("separation", 10);
        margin.AddChild(form);
        form.AddChild(new Label
        {
            Text = "NOUVEAU MODULE",
            ThemeTypeVariation = "HeaderSmall",
        });
        form.AddChild(new Label
        {
            Text = "Forge prépare une première zone jouable que vous pourrez enrichir immédiatement.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        form.AddChild(new Label { Text = "Titre du module" });
        _newModuleTitle = new LineEdit
        {
            PlaceholderText = "Les Îles Brisées",
            Text = "Mon nouveau module",
        };
        form.AddChild(_newModuleTitle);
        form.AddChild(new Label { Text = "Description" });
        _newModuleDescription = new TextEdit
        {
            CustomMinimumSize = new Vector2(520, 86),
            PlaceholderText = "Décrivez en quelques lignes l’aventure et son ambiance.",
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        form.AddChild(_newModuleDescription);
        form.AddChild(new Label { Text = "Première zone" });
        _newModuleAreaName = new LineEdit
        {
            PlaceholderText = "Zone de départ",
            Text = "Zone de départ",
        };
        form.AddChild(_newModuleAreaName);

        var buttons = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
        };
        buttons.AddThemeConstantOverride("separation", 8);
        form.AddChild(buttons);
        _newModuleCancelButton = new Button
        {
            CustomMinimumSize = new Vector2(120, 38),
            Text = "Annuler",
        };
        buttons.AddChild(_newModuleCancelButton);
        _newModuleNextButton = new Button
        {
            CustomMinimumSize = new Vector2(190, 38),
            Text = "Choisir l’emplacement",
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
        _overridesButton!.Pressed += ShowEntityPropertiesDialog;
        _copyButton!.Pressed += CopySelectedEntity;
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
        _creatorShell.WorkspaceRequested += HandleWorkspaceRequest;
        _creatorShell.SelectionLockRequested += HandleSelectionLockRequest;
    }

    private void DisconnectSignals()
    {
        if (_openButton is not null) _openButton.Pressed -= ShowProjectPicker;
        if (_newModuleButton is not null) _newModuleButton.Pressed -= ShowNewModuleDialog;
        if (_dialoguesButton is not null) _dialoguesButton.Pressed -= OpenDialogueEditor;
        if (_areasButton is not null) _areasButton.Pressed -= OpenAreaWorkspace;
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
        if (_overridesButton is not null) _overridesButton.Pressed -= ShowEntityPropertiesDialog;
        if (_copyButton is not null) _copyButton.Pressed -= CopySelectedEntity;
        if (_pasteButton is not null) _pasteButton.Pressed -= PasteSemanticClipboard;
        if (_duplicateButton is not null) _duplicateButton.Pressed -= DuplicateSelectedEntity;
        if (_entityPicker is not null) _entityPicker.ItemSelected -= EntitySelectionChanged;
        if (_undoButton is not null) _undoButton.Pressed -= Undo;
        if (_redoButton is not null) _redoButton.Pressed -= Redo;
        if (_playButton is not null) _playButton.Pressed -= StartPlaytestFromUi;
        if (_projectDialog is not null) _projectDialog.DirSelected -= OpenSelectedProject;
        if (_exportDialog is not null) _exportDialog.FileSelected -= ExportModule;
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
            _creatorShell.WorkspaceRequested -= HandleWorkspaceRequest;
            _creatorShell.SelectionLockRequested -= HandleSelectionLockRequest;
        }
    }

    private void HandleWorkspaceRequest(string workspace)
    {
        _creatorShell?.SetWorkspace(workspace);
        if (string.Equals(workspace, "Dialogues", StringComparison.Ordinal))
        {
            OpenDialogueEditor();
            return;
        }

        if (string.Equals(workspace, "Blueprints", StringComparison.Ordinal))
        {
            OpenBlueprintCatalog();
            return;
        }

        if (string.Equals(workspace, "Scripts", StringComparison.Ordinal))
        {
            _buildPipeline?.Open();
            return;
        }

        if (string.Equals(workspace, "Assets", StringComparison.Ordinal))
        {
            _assetBrowser?.Open();
            return;
        }

        if (string.Equals(workspace, "Validation", StringComparison.Ordinal))
        {
            _distributionProfiles?.Open();
            return;
        }

        if (string.Equals(workspace, "Assistant", StringComparison.Ordinal))
        {
            _localizationWorkspace?.Open();
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

    private void HandleSelectionLockRequest(bool locked) => _selectionContext.SetLocked(locked);

    private void HandleSharedSelectionChanged()
    {
        _creatorShell?.SetSelection(_selectionContext.Current, _selectionContext.IsLocked);
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

    private void OpenNavigationConfiguration() => _navigationConfiguration?.Open();

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
        return true;
    }

    private AreaId? CreateArea(
        string displayName,
        AreaKind kind,
        AreaDimensions dimensions,
        AreaTemplateKind template)
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
            DryRun: false);
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
        var selected = GetSelectedEntity();
        if (_history is null || selected is null)
        {
            return;
        }

        var area = GetActiveArea();
        _semanticClipboard.CopyEntities(_history.State.Project, area.Id, [selected.Id]);
        _clipboardSummary!.Text =
            $"Aperçu · 1 entité · blueprint et propriétés partagés · nouvel UUID au collage";
        _pasteButton!.Disabled = false;
        _statusLabel!.Text = "✓ Entité copiée dans le clipboard sémantique";
        _detailsLabel!.Text = "Le collage sera prévalidé dans la zone active avec un décalage de grille.";
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
        var command = new PasteEntitiesCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            payload,
            mapping,
            new Vector3Value(GridStep, 0, GridStep),
            DryRun: false);
        var firstPastedId = mapping[payload.Entities[0].Id];
        if (ApplyCommand(
                _pasteEntitiesHandler.Handle(_history.State, command),
                $"{payload.Entities.Count} entité(s) collée(s) avec de nouveaux UUID.",
                firstPastedId))
        {
            _clipboardSummary!.Text =
                $"{payload.Entities.Count} entité(s) disponible(s) · dernier collage appliqué";
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

    private bool UpsertBlueprint(
        BlueprintId blueprintId,
        string slug,
        string displayName,
        BlueprintKind kind)
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
            DryRun: false);
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
        string displayName)
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
            DryRun: false);
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

        _newModuleDialog?.PopupCentered(new Vector2I(600, 500));
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
            string.IsNullOrWhiteSpace(_newModuleAreaName.Text))
        {
            _statusLabel!.Text = "✗ Informations incomplètes";
            _detailsLabel!.Text = "Le titre du module et le nom de la première zone sont obligatoires.";
            return;
        }

        HideNewModuleDialog();
        _newModuleLocationDialog!.CurrentDir = _projectSession is null
            ? System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments)
            : Directory.GetParent(_projectSession.RootDirectory)?.FullName ?? _projectSession.RootDirectory;
        _newModuleLocationDialog.PopupCenteredRatio(0.72f);
        ApplyForgeWindowIcon(_newModuleLocationDialog);
    }

    private void CreateNewModuleIn(string parentDirectory)
    {
        var outcome = CreateNewModuleWorkspace(
            parentDirectory,
            _newModuleTitle!.Text,
            _newModuleDescription!.Text,
            _newModuleAreaName!.Text);
        if (!outcome.Succeeded || outcome.WorkspaceDirectory is null)
        {
            _statusLabel!.Text = "✗ Création du module impossible";
            _detailsLabel!.Text = outcome.ErrorMessage ?? "Une erreur inconnue a interrompu la création.";
            GD.PrintErr($"OPENRPG_FORGE_TOOLSET_NEW_MODULE_FAILED {outcome.ErrorMessage}");
            return;
        }

        OpenProjectDirectory(outcome.WorkspaceDirectory);
        _statusLabel!.Text = "✓ Nouveau module prêt à construire";
        _detailsLabel!.Text =
            $"{_history!.State.Project.Module.DisplayName} · première zone et point de départ créés";
        GD.Print($"OPENRPG_FORGE_TOOLSET_NEW_MODULE_CREATED path={outcome.WorkspaceDirectory}");
    }

    private NewModuleCreationOutcome CreateNewModuleWorkspace(
        string parentDirectory,
        string title,
        string description,
        string areaDisplayName)
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
            DryRun: false));
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
        PublishSelectedEntity();
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
        _selectionContext.SetLocked(false);
        _selectionContext.Clear();
        _history = new ProjectCommandHistory(new ProjectState(
            _projectSession.Project,
            new ProjectRevision(0)));
        var cameraProfile = EditorCameraProfileStore.LoadOrCreate(
            _projectSession.RootDirectory,
            _projectSession.Project.Id);
        _projectPreview?.SetCameraProfile(cameraProfile.Profile, PersistEditorCameraProfile);
        _activeAreaContext.Reset(_projectSession.Project);
        RebuildPreview();
        RefreshEditorControls();
        var project = _history.State.Project;
        var entityCount = project.Areas.Sum(area => area.Entities.Count);
        _statusLabel.Text = "✓ Projet Forge ouvert et valide";
        _detailsLabel.Text = $"{project.Title} — {project.Areas.Count} zone · {project.Blueprints.Count} blueprints · {entityCount} entités";
        _projectPathLabel.Text = _projectSession.RootDirectory;
        _saveButton.Disabled = false;
        _exportButton.Disabled = false;
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
        var command = new PlaceEntityCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            entityId,
            _paletteBlueprintIds[_blueprintPalette.Selected],
            transform,
            dryRun: false);
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
        var command = new PlaceEntityCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            entityId,
            blueprintId,
            new TransformValue(position, QuaternionValue.Identity, Vector3Value.One),
            dryRun: false);
        return ApplyCommand(
            _placeHandler.Handle(_history.State, command),
            "Blueprint déposé directement dans le Monde.",
            entityId)
                ? entityId
                : null;
    }

    private bool TransformEntityFromViewport(EntityId entityId, TransformValue transform)
    {
        if (_history is null)
        {
            return false;
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
        PublishSelectedEntity();
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
        var selected = GetSelectedEntity();
        if (_history is null || selected is null)
        {
            return;
        }

        var area = GetActiveArea();
        var position = selected.Transform.Position;
        var transform = new TransformValue(
            new Vector3Value(
                SnapToGrid(position.X + deltaX),
                position.Y,
                SnapToGrid(position.Z + deltaZ)),
            selected.Transform.Rotation,
            selected.Transform.Scale);
        var command = new TransformEntityCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            selected.Id,
            transform,
            DryRun: false);
        ApplyCommand(
            _transformHandler.Handle(_history.State, command),
            "Entité déplacée sur la grille.",
            selected.Id);
    }

    private void RotateSelected()
    {
        var selected = GetSelectedEntity();
        if (_history is null || selected is null)
        {
            return;
        }

        var area = GetActiveArea();
        var quarterTurn = new QuaternionValue(0, QuarterTurnComponent, 0, QuarterTurnComponent);
        var transform = new TransformValue(
            selected.Transform.Position,
            MultiplyAndNormalize(selected.Transform.Rotation, quarterTurn),
            selected.Transform.Scale);
        var command = new TransformEntityCommand(
            CommandId.From(Guid.NewGuid()),
            _history.State.Revision,
            area.Id,
            selected.Id,
            transform,
            DryRun: false);
        ApplyCommand(
            _transformHandler.Handle(_history.State, command),
            "Entité tournée de 90°.",
            selected.Id);
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

        _history.Record(result);
        _activeAreaContext.EnsureValid(_history.State.Project);
        RebuildPreview();
        RefreshEditorControls(selectedEntityId);
        ShowEditStatus(successMessage);
        GD.Print($"OPENRPG_FORGE_TOOLSET_COMMAND_APPLIED operation={result.Patch?.Operation} revision={_history.State.Revision}");
        return true;
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
        GD.Print("OPENRPG_FORGE_TOOLSET_PROJECT_SAVED");
    }

    private void RefreshEditorControls(EntityId? selectedEntityId = null)
    {
        if (_blueprintPalette is null || _entityPicker is null || _spawnPicker is null ||
            _placeButton is null || _revisionLabel is null ||
            _undoButton is null || _redoButton is null || _playButton is null ||
            _overridesButton is null || _overridesSummary is null || _dialoguesButton is null ||
            _areasButton is null || _searchButton is null || _moduleSettingsButton is null ||
            _copyButton is null || _pasteButton is null ||
            _duplicateButton is null || _clipboardSummary is null)
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
        RefreshCreatorShell();
        _blueprintCatalog?.Refresh();
        _areaWorkspace?.Refresh();
        _globalSearch?.Refresh();
        _moduleConfiguration?.Refresh();
        _distributionProfiles?.Refresh();
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
        _overridesButton!.Disabled = !hasEntity;
        _copyButton!.Disabled = !hasEntity;
        _duplicateButton!.Disabled = !hasEntity;
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
        if (_creatorShell is null || _history is null)
        {
            return;
        }

        var project = _history.State.Project;
        _creatorShell.SetProject(CreatorProjectExplorer.Build(project));
        _creatorShell.SetDiagnostics(CreatorDiagnostics.FromProject(
            project,
            $"revision:{_history.State.Revision}"));
    }

    private void PublishSelectedEntity()
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
        _selectionContext.Replace(new CreatorSelection(
            CreatorSelectionKind.Entity,
            entity.Id.ToString(),
            blueprintName,
            $"areas/{area.Id}/entities/{entity.Id}"));
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

    private void StartPlaytestFromUi() => StartPlaytest(headless: false, workspaceSmokeTest: false);

    private void StartPlaytest(bool headless, bool workspaceSmokeTest)
    {
        var spawnEntityId = GetSelectedSpawnId();
        if (_history is null || spawnEntityId is null || _playtestRunning)
        {
            return;
        }

        PlaytestSnapshot snapshot;
        try
        {
            snapshot = _playtestSnapshots.Create(_history.State.Project, spawnEntityId);
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
            $"Snapshot temporaire prêt.\nPoint de départ : {spawnEntityId}\n" +
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

        if (!RunDistributionProfilesSmokeTest())
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

        if (!RunLocalizationWorkspaceSmokeTest())
        {
            return;
        }

        if (!RunNewModuleSmokeTest())
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

    private bool RunCreatorShellSmokeTest()
    {
        if (_creatorShell is null || _history is null)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G702_SHELL_FAILED missing_shell");
            return false;
        }

        var entity = _history.State.Project.Areas.First().Entities.First();
        if (_creatorShell.WorkspaceCount != 9 ||
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
        GD.Print($"OPENRPG_FORGE_TOOLSET_G702_SHELL_PASS workspaces={_creatorShell.WorkspaceCount} explorer={_creatorShell.ExplorerNodeCount} selection=shared diagnostics=clickable");
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

        var retargeted = _history.State.Project.Areas.First().Entities.Single(candidate => candidate.Id == entity.Id);
        var references = BlueprintReferenceIndex.Build(_history.State.Project).Find(derivedBlueprintId);
        if (_history.State.Project.Blueprints.Count != initialBlueprintCount + 1 ||
            retargeted.BlueprintId != derivedBlueprintId ||
            retargeted.Overrides != entity.Overrides ||
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

        var undoTransform = _history.Undo();
        var undoPlacement = _history.Undo();
        if (!undoTransform.Succeeded || !undoPlacement.Succeeded ||
            _history.State.Project.Areas.First().Entities.Count != initialEntityCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED undo");
            return false;
        }

        var redoPlacement = _history.Redo();
        var redoTransform = _history.Redo();
        if (!redoPlacement.Succeeded || !redoTransform.Succeeded ||
            _history.State.Project.Areas.First().Entities.Single(entity => entity.Id == placedEntityId).Transform.Position != movePreview.CorrectedPosition)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED redo");
            return false;
        }

        var cleanupTransform = _history.Undo();
        var cleanupPlacement = _history.Undo();
        if (!cleanupTransform.Succeeded || !cleanupPlacement.Succeeded ||
            _history.State.Project.Areas.First().Entities.Count != initialEntityCount)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G704_DIRECT_FAILED cleanup");
            return false;
        }

        RebuildPreview();
        RefreshEditorControls(area.Entities.First().Id);
        GD.Print("OPENRPG_FORGE_TOOLSET_G704_DIRECT_PASS drag=placed gizmo=moved invalid=blocked history=restored");
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
            "creator-smoke");
        if (!_moduleConfiguration.ConfigureForSmokeTest(metadata, [variable]) ||
            !_moduleConfiguration.ApplyForSmokeTest())
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G708_MODULE_FAILED ui_workflow");
            return false;
        }

        var configured = _history.State.Project.Module;
        var blackboard = new ProjectBlackboard(configured.Variables);
        var searchResults = CreatorSearchIndex.Build(_history.State.Project).Search(
            new CreatorSearchQuery("smoke.g708.ready", CreatorContentKind.ProjectVariable));
        if (configured.Metadata.Localizations.Count != 2 || configured.Metadata.Version != "0.8.0" ||
            configured.Variables.Count != 1 || searchResults.Count != 1 ||
            !blackboard.TrySet(variable.Key, "true", out _) ||
            blackboard.TrySet(variable.Key, "not-a-boolean", out _))
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
        var installedProvenance = new BlueprintProvenance(
            BlueprintProvenance.CurrentSchemaVersion,
            $"pack://forge-core/1.0.0/{blueprint.Slug}",
            "OpenRPG Forge",
            "CC0-1.0",
            new string('a', 64),
            false,
            ["Validated installed pack content"]);
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
            ["Generated for G7-09", "Reviewed by module author"]);
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
                restoredBlueprint.Provenance.LicenseSpdx != "CC0-1.0")
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
            restored.Provenance != originalProvenance)
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
        return true;
    }

    private bool RunBuildPipelineSmokeTest()
    {
        if (_buildPipeline is null || _history is null || !_buildPipeline.AnalyzeForSmokeTest() ||
            _buildPipeline.ResourceCount != 5 || _buildPipeline.ScriptCount != 2)
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

            var first = ModulePackager.Export(workspace, artifact);
            var second = ModulePackager.Export(workspace, artifact);
            var restored = WorkspaceBuildStateStore.TryLoad(workspace);
            if (first.BuildPlan.RebuiltResources.Count == 0 ||
                second.BuildPlan.RebuiltResources.Count != 0 ||
                second.BuildPlan.ReusedResources.Count == 0 ||
                restored?.SchemaVersion != BuildDependencyGraph.CurrentSchemaVersion ||
                !File.Exists(Path.Combine(workspace, ".forge", "build-graph.json")))
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
            "dependencies=automatic incremental=reused persistence=restored");
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
        var configuration = new ModuleDistributionConfiguration(
            original.Id,
            profiles,
            [approval],
            "workspace-smoke-test");
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
            var created = ProjectSession.Create(workspace, _history.State.Project);
            if (!created.Succeeded || created.Session is null)
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_G713_DISTRIBUTION_FAILED workspace");
                return false;
            }

            var reopened = ProjectSession.Open(workspace);
            var exported = ModulePackager.Export(workspace, artifact, target: ModuleTargetKind.Editor);
            using var archive = ZipFile.OpenRead(artifact);
            if (!reopened.Succeeded || reopened.Session is null ||
                reopened.Session.Project.Module.Distribution.TargetProfiles.Count != 5 ||
                reopened.Session.Project.Module.Distribution.Dependencies.Single().Id != approval.Id ||
                exported.Manifest.Target != ModuleTargetKind.Editor ||
                exported.Manifest.ThirdPartyDependencies.Single().Id != approval.Id ||
                archive.GetEntry(DistributionNoticeBuilder.PackagePath) is null)
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
            "licenses=diagnostic registry=versioned notices=generated history=restored persistence=roundtrip");
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
        if (geometry.Count != 0 || reachability.Diagnostics.Count != 0 ||
            partial.RequiresFullBake || partial.ChangedCells.Count == 0 || partial.ReusedCellCount == 0 ||
            current.OffMeshEdges.Count == 0)
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
        if (_assetBrowser.CatalogCount != 605 || _assetBrowser.SourceCount != 3 ||
            _assetBrowser.FilterForSmokeTest("apple", "ambientcg") != 3 ||
            !_assetBrowser.SelectAssetForSmokeTest("ambientcg:3DApple001") ||
            !_assetBrowser.SelectedHasPreviewMetadata ||
            (_assetBrowser.SelectedCanPlanImport && !_assetBrowser.PreviewImportForSmokeTest()) ||
            _assetBrowser.FilterForSmokeTest(string.Empty, "kenney") != 50 ||
            _assetBrowser.FilterForSmokeTest(string.Empty, category: "furniture") == 0)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_G716_ASSETS_FAILED catalog_facets_preview");
            return false;
        }

        _assetBrowser.FilterForSmokeTest(string.Empty);
        GD.Print(
            "OPENRPG_FORGE_TOOLSET_G716_ASSETS_PASS catalog=605 sources=3 facets=search-source-category " +
            "uuid=stable hash=addressed license=verified availability=explicit preview=metadata import=governed");
        return true;
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

            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_T12_SMOKE_PASS format={ProjectSession.CurrentProjectFormatVersion} " +
                "areas=1 documents=1 entities=2");
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
                    new LocalizedText("dialogue.elyra.smoke.entry", "La crypte attend votre décision."),
                    choices:
                    [
                        new DialogueChoice(
                            acceptChoiceId,
                            new LocalizedText("dialogue.elyra.smoke.accept", "Je retrouverai la clé."),
                            [new DialogueCondition(DialogueConditionKind.VariableEquals, "quest.accepted", "true")],
                            [new DialogueAction(DialogueActionKind.StartQuest, "crypt-of-ashes", "active")],
                            questNodeId),
                        new DialogueChoice(
                            DialogueChoiceId.From(Guid.Parse("71000000-0000-4000-8000-000000000002")),
                            new LocalizedText("dialogue.elyra.smoke.leave", "Je ne suis pas prêt.")),
                    ]),
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
        if (stored.Nodes.Count != 2 || choices.Count != 2 ||
            traversal.Choose(acceptChoiceId, new SmokeDialogueConditionEvaluator()).Node?.Id != questNodeId)
        {
            GD.PrintErr("OPENRPG_FORGE_TOOLSET_T42_DIALOGUE_FAILED invalid_state");
            return false;
        }

        GD.Print($"OPENRPG_FORGE_TOOLSET_T42_DIALOGUE_PASS nodes=2 choices=3 revision={_history.State.Revision}");
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
