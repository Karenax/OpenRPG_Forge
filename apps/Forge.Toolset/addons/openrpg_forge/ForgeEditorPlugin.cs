#if TOOLS
using System.Collections.Concurrent;
using Forge.Application.Blueprints;
using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Dialogues;
using Forge.Application.Entities;
using Forge.Application.Projects;
using Forge.Application.Quests;
using Forge.Application.Time;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Spatial;
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
    private readonly TransformEntityHandler _transformHandler = new(new SystemClock());
    private readonly RemoveEntityHandler _removeHandler = new(new SystemClock());
    private readonly SetEntityOverridesHandler _setOverridesHandler = new(new SystemClock());
    private readonly NewModuleHandler _newModuleHandler = new(new SystemClock());
    private readonly SetDialogueHandler _setDialogueHandler = new(new SystemClock());
    private readonly SetQuestHandler _setQuestHandler = new(new SystemClock());
    private readonly UpsertBlueprintHandler _upsertBlueprintHandler = new(new SystemClock());
    private readonly CreateBlueprintFromEntityHandler _createBlueprintFromEntityHandler = new(new SystemClock());
    private readonly ConcurrentQueue<Action> _mainThreadActions = new();
    private readonly CreatorSelectionContext _selectionContext = new();
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
    private Button? _undoButton;
    private Button? _redoButton;
    private Button? _playButton;
    private OptionButton? _blueprintPalette;
    private OptionButton? _entityPicker;
    private OptionButton? _spawnPicker;
    private Label? _overridesSummary;
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
    private ForgeCreatorShell? _creatorShell;
    private ProjectSession? _projectSession;
    private ProjectPreview3D? _projectPreview;
    private ProjectCommandHistory? _history;
    private Image? _forgeWindowIcon;
    private bool _playtestRunning;
    private bool _showEntityPropertiesDialogWhenReady;
    private bool _showDialogueEditorWhenReady;
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
            selection => { _selectionContext.Replace(selection); },
            ApplyForgeWindowIcon);
        _mainPanel.AddChild(_blueprintCatalog);
        _projectPreview = new ProjectPreview3D();
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
        _undoButton = null;
        _redoButton = null;
        _playButton = null;
        _blueprintPalette = null;
        _entityPicker = null;
        _spawnPicker = null;
        _overridesSummary = null;
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
        _overridesSummary = GetControl<Label>("Margin/Content/Editing/OverridesRow/OverridesSummary");
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
        _creatorShell!.ExplorerSelectionRequested += HandleExplorerSelection;
        _creatorShell.DiagnosticSelectionRequested += HandleDiagnosticSelection;
        _creatorShell.WorkspaceRequested += HandleWorkspaceRequest;
        _creatorShell.SelectionLockRequested += HandleSelectionLockRequest;
    }

    private void DisconnectSignals()
    {
        if (_openButton is not null) _openButton.Pressed -= ShowProjectPicker;
        if (_newModuleButton is not null) _newModuleButton.Pressed -= ShowNewModuleDialog;
        if (_dialoguesButton is not null) _dialoguesButton.Pressed -= OpenDialogueEditor;
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
        if (_creatorShell is not null)
        {
            _creatorShell.ExplorerSelectionRequested -= HandleExplorerSelection;
            _creatorShell.DiagnosticSelectionRequested -= HandleDiagnosticSelection;
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
        if (selection is null || !_selectionContext.Replace(selection))
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
            _detailsLabel!.Text = $"{diagnostic.Message}\n{diagnostic.DocumentPath}";
        }
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

            var exported = ModulePackager.Export(_projectSession.RootDirectory, destinationPath);
            _statusLabel.Text = "✓ Module exporté et validé";
            _detailsLabel.Text =
                $"{exported.Manifest.Slug} · {exported.FileCount} fichiers · {exported.ArchiveBytes / 1024d:0.0} Kio";
            GD.Print(
                $"OPENRPG_FORGE_TOOLSET_MODULE_EXPORTED slug={exported.Manifest.Slug} " +
                $"files={exported.FileCount} bytes={exported.ArchiveBytes}");
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

        var entities = _history.State.Project.Areas.First().Entities;
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

        var area = _history.State.Project.Areas.First();
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

        var result = ProjectSession.Open(workspaceDirectory);
        if (!result.Succeeded || result.Session is null)
        {
            _projectSession = null;
            _history = null;
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

    private EntityId? PlaceSelectedBlueprint()
    {
        if (_history is null || _blueprintPalette is null ||
            _positionX is null || _positionZ is null ||
            _blueprintPalette.Selected < 0 ||
            _blueprintPalette.Selected >= _paletteBlueprintIds.Count)
        {
            return null;
        }

        var area = _history.State.Project.Areas.First();
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

        var area = _history.State.Project.Areas.First();
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

        var area = _history.State.Project.Areas.First();
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

        var area = _history.State.Project.Areas.First();
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
            _overridesButton is null || _overridesSummary is null || _dialoguesButton is null)
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
            _blueprintPalette.AddItem(blueprint.DisplayName);
        }

        var blueprintNames = project.Blueprints.ToDictionary(blueprint => blueprint.Id, blueprint => blueprint.DisplayName);
        foreach (var entity in project.Areas.First().Entities)
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
        if (!hasEntity)
        {
            _overridesSummary!.Text = "Sélectionnez une entité";
        }
        _undoButton!.Disabled = true;
        _redoButton!.Disabled = true;
        _spawnPicker!.Disabled = !enabled || _spawnEntityIds.Count == 0;
        _playButton!.Disabled = !enabled || _spawnEntityIds.Count == 0 || _playtestRunning;
        _dialoguesButton!.Disabled = !enabled;
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
        return _history.State.Project.Areas.First().Entities.Single(entity => entity.Id == entityId);
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
            if (!opened.Succeeded || project is null ||
                project.Module.Slug != "module-smoke" ||
                project.Areas.Count != 1 ||
                project.Areas.Single().Entities.Count != 2 ||
                !project.Blueprints.Any(blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn))
            {
                GD.PrintErr("OPENRPG_FORGE_TOOLSET_T12_SMOKE_FAILED invalid_workspace");
                return false;
            }

            GD.Print("OPENRPG_FORGE_TOOLSET_T12_SMOKE_PASS format=0.6.0 areas=1 entities=2");
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
