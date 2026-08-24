using Forge.Application.Creator;
using Forge.Domain.Authoring;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioViewportToolsOverlay : Control
{
    private static readonly (string Key, string Label)[] CameraActions =
    [
        ("forward", "Avancer"),
        ("back", "Reculer"),
        ("left", "Gauche"),
        ("right", "Droite"),
        ("up", "Monter"),
        ("down", "Descendre"),
        ("focus", "Cadrer la sélection"),
    ];

    private readonly StudioSessionController _session;
    private readonly StudioWorldViewport _viewport;
    private readonly Dictionary<string, LineEdit> _cameraBindings = new(StringComparer.Ordinal);
    private readonly List<EditorCameraBookmarkId> _bookmarkIds = [];
    private TabContainer _tabs = null!;
    private OptionButton _mode = null!;
    private Label _modeSummary = null!;
    private SpinBox _cameraSpeed = null!;
    private SpinBox _cameraSensitivity = null!;
    private OptionButton _pilotMode = null!;
    private LineEdit _bookmarkName = null!;
    private OptionButton _bookmarks = null!;
    private Button _recallBookmark = null!;
    private Label _cameraState = null!;
    private CheckBox _multiSelection = null!;
    private CheckBox _focusSelection = null!;
    private CheckBox _frameSelection = null!;
    private CheckBox _lockSelection = null!;
    private CheckBox _commonProperties = null!;
    private Label _selectionSummary = null!;
    private OptionButton _tool = null!;
    private OptionButton _coordinateSpace = null!;
    private OptionButton _pivotMode = null!;
    private SpinBox _translationSnap = null!;
    private SpinBox _rotationSnap = null!;
    private SpinBox _scaleSnap = null!;
    private CheckBox _numericInput = null!;
    private CheckBox _alignmentEnabled = null!;
    private SpinBox _valueX = null!;
    private SpinBox _valueY = null!;
    private SpinBox _valueZ = null!;
    private OptionButton _alignment = null!;
    private Label _validation = null!;
    private Label _status = null!;

    internal event Action? StateChanged;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;

    internal StudioViewportToolsOverlay(
        StudioSessionController session,
        StudioWorldViewport viewport)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        Name = "StudioViewportToolsOverlay";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        ZIndex = 92;

        var dim = new ColorRect
        {
            Color = new Color(0, 0, 0, .78f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var frame = StudioStyle.Panel("ViewportToolsFrame", StudioStyle.Chrome, 12, 1);
        frame.AnchorLeft = .055f;
        frame.AnchorTop = .045f;
        frame.AnchorRight = .945f;
        frame.AnchorBottom = .955f;
        AddChild(frame);
        var margin = StudioStyle.Margin(16, 14, 16, 14);
        frame.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("Outils 3D", 20));
        heading.AddChild(StudioStyle.Label(
            "Modes de contrôle, caméra, sélection multiple et transformations · aucune édition de fichier technique",
            11,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer · Échap", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddChild(_tabs);
        _tabs.AddChild(BuildModeTab());
        _tabs.AddChild(BuildCameraTab());
        _tabs.AddChild(BuildSelectionTab());
        _tabs.AddChild(BuildTransformTab());

        _validation = StudioStyle.Label(
            "Modifiez le brouillon puis demandez un aperçu avant de l’appliquer.",
            11,
            StudioStyle.Muted,
            wrap: true);
        root.AddChild(_validation);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 7);
        var preview = StudioStyle.Button("Prévisualiser le profil", compact: true);
        preview.Pressed += PreviewWorkspaceProfile;
        actions.AddChild(preview);
        var apply = StudioStyle.Button("Appliquer en mémoire", primary: true, compact: true);
        apply.Pressed += ApplyWorkspaceProfile;
        actions.AddChild(apply);
        var save = StudioStyle.Button("Enregistrer le profil", compact: true);
        save.Pressed += SaveWorkspaceProfile;
        actions.AddChild(save);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _status = StudioStyle.Label(string.Empty, 10, StudioStyle.Success);
        _status.CustomMinimumSize = new Vector2(170, 0);
        _status.HorizontalAlignment = HorizontalAlignment.Right;
        actions.AddChild(_status);
        root.AddChild(actions);
    }

    internal void Open(int tab = 0)
    {
        PopulateFromSession();
        _tabs.CurrentTab = Math.Clamp(tab, 0, 3);
        Visible = true;
        MoveToFront();
    }

    internal void Close()
    {
        Visible = false;
        _viewport.GrabFocus();
    }

    internal bool PrepareEvidence(string mode)
    {
        var tab = mode switch
        {
            "ux0-c-camera" => 1,
            "ux0-c-orthographic" => 1,
            "ux0-c-selection" => 2,
            "ux0-c-transform" => 3,
            "ux0-c-scale-gizmo" => 3,
            _ => 0,
        };
        Open(tab);
        if (tab == 0)
        {
            _mode.Select((int)EditorViewportMode.Navigation);
            UpdateModeSummary();
            ApplyWorkspaceProfile();
        }
        else if (tab == 1)
        {
            SetCameraView(EditorCameraView.Top);
        }
        else if (tab == 2)
        {
            SelectEvidenceEntities();
        }
        else if (tab == 3)
        {
            SelectEvidenceEntities();
            _tool.Select((int)EditorTransformTool.Scale);
            _coordinateSpace.Select((int)EditorCoordinateSpace.Local);
            _pivotMode.Select((int)EditorPivotMode.SelectionCenter);
            _valueX.Value = 1.1;
            _valueY.Value = 1.1;
            _valueZ.Value = 1.1;
            ApplyWorkspaceProfile();
        }
        if (string.Equals(mode, "ux0-c-world-navigation", StringComparison.OrdinalIgnoreCase))
        {
            _viewport.FrameAreaForEvidence();
            Close();
            return _viewport.ViewportMode == EditorViewportMode.Navigation &&
                   _viewport.DiagnosticOverlayName == "NavigationOverlay";
        }
        if (string.Equals(mode, "ux0-c-scale-gizmo", StringComparison.OrdinalIgnoreCase))
        {
            _viewport.FocusSelection();
            Close();
            return _viewport.ActiveTransformTool == EditorTransformTool.Scale &&
                   _viewport.ScaleGizmoHandleCount == 4 && _viewport.GizmoVisible;
        }
        if (string.Equals(mode, "ux0-c-orthographic", StringComparison.OrdinalIgnoreCase))
        {
            Close();
            return _viewport.CameraProjection == EditorCameraProjection.Orthographic;
        }
        return true;
    }

    private Control BuildModeTab()
    {
        var tab = BuildScrollableTab("Modes 3D", out var body);
        body.AddChild(StudioStyle.Label(
            "Choisissez ce que la vue doit rendre prioritaire. Le mode est conservé dans le profil d’espace de travail.",
            12,
            StudioStyle.Text,
            wrap: true));
        _mode = new OptionButton();
        StudioStyle.ApplyOptionButton(_mode);
        foreach (var mode in Enum.GetValues<EditorViewportMode>())
        {
            _mode.AddItem(ViewportModeName(mode));
        }
        _mode.ItemSelected += _ => UpdateModeSummary();
        body.AddChild(Field("Mode de la vue principale", _mode));
        _modeSummary = StudioStyle.Label(string.Empty, 11, StudioStyle.Muted, wrap: true);
        var summaryPanel = StudioStyle.Panel("ViewportModeSummary", StudioStyle.Surface, 9, 1);
        var summaryMargin = StudioStyle.Margin(12, 12, 12, 12);
        summaryPanel.AddChild(summaryMargin);
        summaryMargin.AddChild(_modeSummary);
        body.AddChild(summaryPanel);

        var cards = new GridContainer
        {
            Columns = 3,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        cards.AddThemeConstantOverride("h_separation", 8);
        cards.AddThemeConstantOverride("v_separation", 8);
        foreach (var mode in Enum.GetValues<EditorViewportMode>())
        {
            var card = StudioStyle.Panel("Mode" + mode, StudioStyle.Surface, 8, 1);
            card.CustomMinimumSize = new Vector2(360, 70);
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var margin = StudioStyle.Margin(10, 9, 10, 9);
            card.AddChild(margin);
            var column = new VBoxContainer();
            column.AddChild(StudioStyle.Label(ViewportModeName(mode), 12));
            column.AddChild(StudioStyle.Label(ViewportModeDescription(mode), 10, StudioStyle.Muted, wrap: true));
            margin.AddChild(column);
            cards.AddChild(card);
        }
        body.AddChild(cards);
        return tab;
    }

    private Control BuildCameraTab()
    {
        var tab = BuildScrollableTab("Caméra", out var body);
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 12);
        body.AddChild(columns);

        var settings = BuildColumn(columns, "NAVIGATION", 330);
        _cameraSpeed = AddSpin(settings, "Vitesse adaptative", .1, 20, .1, 1);
        _cameraSensitivity = AddSpin(settings, "Sensibilité de la souris", .05, 5, .05, .25);
        _pilotMode = new OptionButton();
        StudioStyle.ApplyOptionButton(_pilotMode);
        _pilotMode.AddItem("Orbiter autour de la cible");
        _pilotMode.AddItem("Piloter librement");
        settings.AddChild(Field("Mode de pilotage", _pilotMode));
        settings.AddChild(StudioStyle.SectionLabel("VUES ORTHOGRAPHIQUES"));
        var views = new GridContainer { Columns = 3 };
        AddViewButton(views, "Perspective", EditorCameraView.Perspective);
        AddViewButton(views, "Dessus", EditorCameraView.Top);
        AddViewButton(views, "Face", EditorCameraView.Front);
        AddViewButton(views, "Dos", EditorCameraView.Back);
        AddViewButton(views, "Gauche", EditorCameraView.Left);
        AddViewButton(views, "Droite", EditorCameraView.Right);
        settings.AddChild(views);
        settings.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        var cameraHistory = new HBoxContainer();
        var undo = StudioStyle.Button("Annuler caméra", compact: true);
        undo.Pressed += UndoCamera;
        cameraHistory.AddChild(undo);
        var redo = StudioStyle.Button("Rétablir", compact: true);
        redo.Pressed += RedoCamera;
        cameraHistory.AddChild(redo);
        settings.AddChild(cameraHistory);
        var save = StudioStyle.Button("Enregistrer la caméra", primary: true, compact: true);
        save.Pressed += SaveCamera;
        settings.AddChild(save);

        var bindings = BuildColumn(columns, "TOUCHES CONFIGURABLES", 360);
        foreach (var action in CameraActions)
        {
            var edit = new LineEdit
            {
                MaxLength = 12,
                PlaceholderText = "Touche",
                CustomMinimumSize = new Vector2(90, 0),
            };
            StudioStyle.ApplyLineEdit(edit);
            _cameraBindings[action.Key] = edit;
            bindings.AddChild(Field(action.Label, edit));
        }
        var preview = StudioStyle.Button("Prévisualiser les réglages", compact: true);
        preview.Pressed += PreviewCameraConfiguration;
        bindings.AddChild(preview);
        var apply = StudioStyle.Button("Appliquer les réglages", primary: true, compact: true);
        apply.Pressed += ApplyCameraConfiguration;
        bindings.AddChild(apply);

        var bookmarks = BuildColumn(columns, "POINTS DE VUE", 330);
        _bookmarkName = new LineEdit { PlaceholderText = "Ex. Entrée du village" };
        StudioStyle.ApplyLineEdit(_bookmarkName);
        bookmarks.AddChild(Field("Nom lisible", _bookmarkName));
        var saveBookmark = StudioStyle.Button("Mémoriser la vue actuelle", compact: true);
        saveBookmark.Pressed += SaveBookmark;
        bookmarks.AddChild(saveBookmark);
        _bookmarks = new OptionButton();
        StudioStyle.ApplyOptionButton(_bookmarks);
        bookmarks.AddChild(Field("Points de vue mémorisés", _bookmarks));
        _recallBookmark = StudioStyle.Button("Rappeler ce point de vue", primary: true, compact: true);
        _recallBookmark.Pressed += RecallBookmark;
        bookmarks.AddChild(_recallBookmark);
        _cameraState = StudioStyle.Label(string.Empty, 10, StudioStyle.Muted, wrap: true);
        bookmarks.AddChild(_cameraState);
        return tab;
    }

    private Control BuildSelectionTab()
    {
        var tab = BuildScrollableTab("Sélection", out var body);
        body.AddChild(StudioStyle.Label(
            "Ctrl+clic ajoute ou retire un objet. L’objet actif garde le gizmo ; les autres restent cerclés.",
            12,
            StudioStyle.Text,
            wrap: true));
        _multiSelection = AddCheck(body, "Autoriser la sélection multiple");
        _focusSelection = AddCheck(body, "Centrer la caméra sur la sélection");
        _frameSelection = AddCheck(body, "Cadrer tout le groupe");
        _lockSelection = AddCheck(body, "Verrouiller la sélection actuelle");
        _commonProperties = AddCheck(body, "N’éditer que les propriétés communes");
        var actions = new HBoxContainer();
        var all = StudioStyle.Button("Sélectionner tous les objets visibles", compact: true);
        all.Pressed += SelectAllEntities;
        actions.AddChild(all);
        var clear = StudioStyle.Button("Vider la sélection", compact: true);
        clear.Pressed += ClearSelection;
        actions.AddChild(clear);
        var focus = StudioStyle.Button("Cadrer maintenant", compact: true);
        focus.Pressed += _viewport.FocusSelection;
        actions.AddChild(focus);
        body.AddChild(actions);
        _selectionSummary = StudioStyle.Label(string.Empty, 11, StudioStyle.Success, wrap: true);
        body.AddChild(_selectionSummary);
        return tab;
    }

    private Control BuildTransformTab()
    {
        var tab = BuildScrollableTab("Transformer", out var body);
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 12);
        body.AddChild(columns);

        var gizmo = BuildColumn(columns, "GIZMO", 350);
        _tool = EnumOption<EditorTransformTool>(gizmo, "Outil", TransformToolName);
        _tool.ItemSelected += _ => PopulateTransformValueDefaults();
        _coordinateSpace = EnumOption<EditorCoordinateSpace>(gizmo, "Repère", CoordinateName);
        _pivotMode = EnumOption<EditorPivotMode>(gizmo, "Pivot", PivotName);
        _translationSnap = AddSpin(gizmo, "Pas de translation (m)", .001, 100, .05, .25);
        _rotationSnap = AddSpin(gizmo, "Pas de rotation (°)", .1, 180, .5, 5);
        _scaleSnap = AddSpin(gizmo, "Pas d’échelle", .005, 10, .005, .05);
        _numericInput = AddCheck(gizmo, "Saisie numérique activée");
        _alignmentEnabled = AddCheck(gizmo, "Alignements activés");

        var numeric = BuildColumn(columns, "TRANSFORMATION ATOMIQUE", 420);
        numeric.AddChild(StudioStyle.Label(
            "Les valeurs sont relatives : mètres, degrés ou facteurs d’échelle selon l’outil.",
            11,
            StudioStyle.Muted,
            wrap: true));
        var vector = new HBoxContainer();
        _valueX = AddVectorSpin(vector, "X", -100, 100, .05);
        _valueY = AddVectorSpin(vector, "Y", -100, 100, .05);
        _valueZ = AddVectorSpin(vector, "Z", -100, 100, .05);
        numeric.AddChild(vector);
        _alignment = EnumOption<EditorTransformAlignment>(numeric, "Alignement", AlignmentName);
        var preview = StudioStyle.Button("Prévisualiser · aucune mutation", compact: true);
        preview.Pressed += PreviewTransform;
        numeric.AddChild(preview);
        var apply = StudioStyle.Button("Appliquer à la sélection", primary: true, compact: true);
        apply.Pressed += ApplyTransform;
        numeric.AddChild(apply);
        numeric.AddChild(StudioStyle.Label(
            "Une seule commande couvre tout le groupe. Ctrl+Z restaure toutes les transformations ensemble.",
            10,
            StudioStyle.Success,
            wrap: true));
        return tab;
    }

    private void PopulateFromSession()
    {
        var workspace = _session.WorkspaceProfile;
        var direct = workspace.Shell.DirectManipulation;
        _mode.Select((int)workspace.Shell.ViewportMode);
        _multiSelection.ButtonPressed = direct.Selection.MultiSelectionEnabled;
        _focusSelection.ButtonPressed = direct.Selection.FocusOnSelection;
        _frameSelection.ButtonPressed = direct.Selection.FrameSelection;
        _lockSelection.ButtonPressed = direct.Selection.LockSelection;
        _commonProperties.ButtonPressed = direct.Selection.EditCommonPropertiesOnly;
        _tool.Select((int)direct.Transform.Tool);
        _coordinateSpace.Select((int)direct.Transform.CoordinateSpace);
        _pivotMode.Select((int)direct.Transform.PivotMode);
        _translationSnap.Value = direct.Transform.TranslationSnap;
        _rotationSnap.Value = direct.Transform.RotationSnapDegrees;
        _scaleSnap.Value = direct.Transform.ScaleSnap;
        _numericInput.ButtonPressed = direct.Transform.NumericInputEnabled;
        _alignmentEnabled.ButtonPressed = direct.Transform.AlignmentEnabled;

        var camera = _session.CameraProfile;
        _cameraSpeed.Value = camera.NavigationSpeed;
        _cameraSensitivity.Value = camera.MouseSensitivity;
        _pilotMode.Select((int)camera.Input.PilotMode);
        foreach (var action in CameraActions)
        {
            _cameraBindings[action.Key].Text = camera.Input.Bindings[action.Key];
        }
        PopulateBookmarks();
        PopulateTransformValueDefaults();
        UpdateModeSummary();
        UpdateSummaries();
        _validation.Text = "Brouillon synchronisé avec le profil actif · prévisualisez avant d’appliquer.";
        _validation.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _status.Text = string.Empty;
    }

    private void PreviewWorkspaceProfile()
    {
        if (!TryBuildWorkspaceCandidate(out var candidate, out var error))
        {
            ShowValidation(error, error: true);
            return;
        }
        var result = _session.PreviewWorkspaceProfile(candidate!);
        ShowValidation(result.Message, !result.Succeeded);
    }

    private void ApplyWorkspaceProfile()
    {
        if (!TryBuildWorkspaceCandidate(out var candidate, out var error))
        {
            ShowValidation(error, error: true);
            return;
        }
        var preview = _session.PreviewWorkspaceProfile(candidate!);
        if (!preview.Succeeded)
        {
            ShowValidation(preview.Message, error: true);
            return;
        }
        var result = _session.ApplyWorkspaceProfile(candidate!);
        if (result.Succeeded)
        {
            _viewport.SetViewportMode(_session.WorkspaceProfile.Shell.ViewportMode);
            _viewport.RefreshManipulationProfile();
            StateChanged?.Invoke();
        }
        ShowValidation(result.Message, !result.Succeeded);
    }

    private void SaveWorkspaceProfile()
    {
        ApplyWorkspaceProfile();
        if (_session.WorkspaceProfile.Shell.ViewportMode != (EditorViewportMode)_mode.Selected)
        {
            return;
        }
        var result = _session.SaveWorkspaceProfile();
        ShowValidation(result.Message, !result.Succeeded);
    }

    private bool TryBuildWorkspaceCandidate(
        out EditorWorkspaceProfile? candidate,
        out string error)
    {
        try
        {
            var current = _session.WorkspaceProfile;
            var source = current.Shell.DirectManipulation;
            var selection = new EditorSelectionConfiguration(
                source.Selection.Id,
                1,
                "studio:ux0-c-selection",
                _multiSelection.ButtonPressed,
                _focusSelection.ButtonPressed,
                _frameSelection.ButtonPressed,
                _lockSelection.ButtonPressed,
                _commonProperties.ButtonPressed);
            var transform = BuildTransformConfiguration(source.Transform);
            var direct = new EditorDirectManipulationProfile(
                source.Id,
                1,
                "studio:ux0-c-direct-manipulation",
                selection,
                transform);
            var shell = new EditorShellConfiguration(
                current.Shell.Id,
                EditorShellConfiguration.CurrentSchemaVersion,
                "studio:ux0-c-viewport-tools",
                current.Shell.Layout,
                current.Shell.ExplorerGrouping,
                (EditorViewportMode)_mode.Selected,
                current.Shell.PaletteFilter,
                direct);
            candidate = EditorWorkspaceProfilePlanner.ConfigureShell(current, shell);
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            candidate = null;
            error = "Configuration refusée · " + exception.Message;
            return false;
        }
    }

    private EditorTransformToolConfiguration BuildTransformConfiguration(
        EditorTransformToolConfiguration source) => new(
        source.Id,
        1,
        "studio:ux0-c-transform",
        (EditorTransformTool)_tool.Selected,
        (EditorCoordinateSpace)_coordinateSpace.Selected,
        (EditorPivotMode)_pivotMode.Selected,
        (float)_translationSnap.Value,
        (float)_rotationSnap.Value,
        (float)_scaleSnap.Value,
        _numericInput.ButtonPressed,
        _alignmentEnabled.ButtonPressed);

    private void PreviewCameraConfiguration()
    {
        if (!TryBuildCameraInput(out var input, out var error))
        {
            ShowCameraStatus(error, error: true);
            return;
        }
        var speed = _session.NavigateCamera(
            EditorCameraOperation.SetSpeed,
            x: (float)_cameraSpeed.Value,
            dryRun: true);
        var sensitivity = _session.NavigateCamera(
            EditorCameraOperation.SetSensitivity,
            x: (float)_cameraSensitivity.Value,
            dryRun: true);
        var bindings = _session.NavigateCamera(
            EditorCameraOperation.SetInput,
            input: input,
            dryRun: true);
        var valid = speed.Succeeded && sensitivity.Succeeded && bindings.Succeeded;
        ShowCameraStatus(
            valid
                ? "Aperçu valide · vitesse, sensibilité et sept touches · aucune préférence modifiée"
                : string.Join(" · ", new[] { speed, sensitivity, bindings }
                    .Where(result => !result.Succeeded)
                    .Select(result => result.Message)),
            !valid);
    }

    private void ApplyCameraConfiguration()
    {
        if (!TryBuildCameraInput(out var input, out var error))
        {
            ShowCameraStatus(error, error: true);
            return;
        }
        PreviewCameraConfiguration();
        var applied = 0;
        var results = new[]
        {
            _session.NavigateCamera(EditorCameraOperation.SetSpeed, x: (float)_cameraSpeed.Value),
            _session.NavigateCamera(EditorCameraOperation.SetSensitivity, x: (float)_cameraSensitivity.Value),
            _session.NavigateCamera(EditorCameraOperation.SetInput, input: input),
        };
        foreach (var result in results)
        {
            if (!result.Succeeded)
            {
                while (applied-- > 0)
                {
                    _session.UndoCamera();
                }
                _viewport.ApplyCameraProfile(_session.CameraProfile);
                ShowCameraStatus(result.Message, error: true);
                return;
            }
            applied++;
        }
        _viewport.ApplyCameraProfile(_session.CameraProfile);
        UpdateSummaries();
        ShowCameraStatus("Réglages appliqués en mémoire · utilisez Enregistrer la caméra pour les conserver");
    }

    private bool TryBuildCameraInput(
        out EditorCameraInputConfiguration? input,
        out string error)
    {
        try
        {
            var current = _session.CameraProfile.Input;
            input = new EditorCameraInputConfiguration(
                current.Id,
                EditorCameraInputConfiguration.CurrentSchemaVersion,
                "studio:ux0-c-camera-input",
                (EditorCameraPilotMode)_pilotMode.Selected,
                CameraActions.ToDictionary(
                    action => action.Key,
                    action => _cameraBindings[action.Key].Text,
                    StringComparer.Ordinal),
                current.ShareBookmarksWithProject);
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            input = null;
            error = "Touches refusées · chaque action exige une touche unique et non vide.";
            return false;
        }
    }

    private void SetCameraView(EditorCameraView view)
    {
        var result = _session.NavigateCamera(EditorCameraOperation.SetView, view: view);
        if (result.Succeeded)
        {
            _viewport.ApplyCameraProfile(_session.CameraProfile);
            UpdateSummaries();
        }
        ShowCameraStatus(result.Message, !result.Succeeded);
    }

    private void SaveBookmark()
    {
        var name = _bookmarkName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowCameraStatus("Donnez un nom lisible au point de vue.", error: true);
            return;
        }
        var existing = _session.CameraProfile.Bookmarks.FirstOrDefault(bookmark =>
            string.Equals(bookmark.Name, name, StringComparison.OrdinalIgnoreCase));
        var id = existing?.Id ?? EditorCameraBookmarkId.From(Guid.NewGuid());
        var result = _session.NavigateCamera(
            EditorCameraOperation.SaveBookmark,
            bookmarkId: id,
            bookmarkName: name);
        if (result.Succeeded)
        {
            PopulateBookmarks();
        }
        ShowCameraStatus(result.Succeeded ? $"Point de vue « {name} » mémorisé" : result.Message, !result.Succeeded);
    }

    private void RecallBookmark()
    {
        var index = (int)_bookmarks.Selected;
        if (index < 0 || index >= _bookmarkIds.Count)
        {
            ShowCameraStatus("Choisissez un point de vue mémorisé.", error: true);
            return;
        }
        var result = _session.NavigateCamera(
            EditorCameraOperation.RecallBookmark,
            bookmarkId: _bookmarkIds[index]);
        if (result.Succeeded)
        {
            _viewport.ApplyCameraProfile(_session.CameraProfile);
            UpdateSummaries();
        }
        ShowCameraStatus(result.Message, !result.Succeeded);
    }

    private void UndoCamera()
    {
        var result = _session.UndoCamera();
        if (result.Succeeded)
        {
            _viewport.ApplyCameraProfile(_session.CameraProfile);
            PopulateFromSessionCameraOnly();
        }
        ShowCameraStatus(result.Message, !result.Succeeded);
    }

    private void RedoCamera()
    {
        var result = _session.RedoCamera();
        if (result.Succeeded)
        {
            _viewport.ApplyCameraProfile(_session.CameraProfile);
            PopulateFromSessionCameraOnly();
        }
        ShowCameraStatus(result.Message, !result.Succeeded);
    }

    private void SaveCamera()
    {
        var result = _session.SaveCameraProfile();
        UpdateSummaries();
        ShowCameraStatus(result.Message, !result.Succeeded);
    }

    private void SelectAllEntities()
    {
        if (!_multiSelection.ButtonPressed)
        {
            ShowValidation("Activez d’abord la sélection multiple.", error: true);
            return;
        }
        var ids = _session.ActiveArea!.Entities.Select(entity => entity.Id).ToArray();
        if (!_session.SelectEntities(ids, ids.LastOrDefault()))
        {
            ShowValidation("La sélection complète a été refusée par le profil actif.", error: true);
            return;
        }
        _viewport.SetSelections(ids, ids.LastOrDefault());
        UpdateSummaries();
        StateChanged?.Invoke();
    }

    private void ClearSelection()
    {
        _session.SelectEntities([], null);
        _viewport.SetSelections([], null);
        UpdateSummaries();
        StateChanged?.Invoke();
    }

    private void SelectEvidenceEntities()
    {
        _multiSelection.ButtonPressed = true;
        var ids = _session.ActiveArea!.Entities.Take(3).Select(entity => entity.Id).ToArray();
        _session.SelectEntities(ids, ids.LastOrDefault());
        _viewport.SetSelections(ids, ids.LastOrDefault());
        UpdateSummaries();
        StateChanged?.Invoke();
    }

    private void PreviewTransform()
    {
        if (!TryBuildTransformRequest(out var tool, out var value, out var alignment, out var config, out var error))
        {
            ShowValidation(error, error: true);
            return;
        }
        var result = _session.PreviewSelectedManipulation(tool, value, alignment, config);
        ShowValidation(result.Message, !result.Succeeded);
    }

    private void ApplyTransform()
    {
        if (!TryBuildTransformRequest(out var tool, out var value, out var alignment, out var config, out var error))
        {
            ShowValidation(error, error: true);
            return;
        }
        var preview = _session.PreviewSelectedManipulation(tool, value, alignment, config);
        if (!preview.Succeeded)
        {
            ShowValidation(preview.Message, error: true);
            return;
        }
        var result = _session.ManipulateSelectedEntities(tool, value, alignment, config);
        if (result.Succeeded)
        {
            _viewport.Rebuild(_session, frameArea: false);
            _viewport.SetDisplayFilter(_session.WorkspaceProfile.DisplayFilter);
            _viewport.SetSelections(_session.SelectedEntityIds, _session.SelectedEntityId);
            StateChanged?.Invoke();
        }
        ShowValidation(result.Message, !result.Succeeded);
    }

    private bool TryBuildTransformRequest(
        out EditorTransformTool tool,
        out Vector3Value value,
        out EditorTransformAlignment alignment,
        out EditorTransformToolConfiguration? configuration,
        out string error)
    {
        tool = (EditorTransformTool)_tool.Selected;
        value = new Vector3Value((float)_valueX.Value, (float)_valueY.Value, (float)_valueZ.Value);
        alignment = (EditorTransformAlignment)_alignment.Selected;
        try
        {
            configuration = BuildTransformConfiguration(
                _session.WorkspaceProfile.Shell.DirectManipulation.Transform);
            if (!configuration.NumericInputEnabled)
            {
                error = "Activez la saisie numérique pour appliquer ces valeurs.";
                return false;
            }
            if (alignment != EditorTransformAlignment.None && !configuration.AlignmentEnabled)
            {
                error = "Activez les alignements avant de choisir cette opération.";
                return false;
            }
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            configuration = null;
            error = "Transformation refusée · " + exception.Message;
            return false;
        }
    }

    private void PopulateBookmarks()
    {
        _bookmarkIds.Clear();
        _bookmarks.Clear();
        foreach (var bookmark in _session.CameraProfile.Bookmarks)
        {
            _bookmarkIds.Add(bookmark.Id);
            _bookmarks.AddItem(bookmark.Name);
        }
        _bookmarks.Disabled = _bookmarkIds.Count == 0;
        _recallBookmark.Disabled = _bookmarkIds.Count == 0;
    }

    private void PopulateFromSessionCameraOnly()
    {
        var camera = _session.CameraProfile;
        _cameraSpeed.Value = camera.NavigationSpeed;
        _cameraSensitivity.Value = camera.MouseSensitivity;
        _pilotMode.Select((int)camera.Input.PilotMode);
        foreach (var action in CameraActions)
        {
            _cameraBindings[action.Key].Text = camera.Input.Bindings[action.Key];
        }
        PopulateBookmarks();
        UpdateSummaries();
    }

    private void PopulateTransformValueDefaults()
    {
        if (_valueX is null)
        {
            return;
        }
        switch ((EditorTransformTool)_tool.Selected)
        {
            case EditorTransformTool.Rotate:
                _valueX.Value = 0;
                _valueY.Value = _rotationSnap.Value;
                _valueZ.Value = 0;
                break;
            case EditorTransformTool.Scale:
                _valueX.Value = 1 + _scaleSnap.Value;
                _valueY.Value = 1 + _scaleSnap.Value;
                _valueZ.Value = 1 + _scaleSnap.Value;
                break;
            default:
                _valueX.Value = _translationSnap.Value;
                _valueY.Value = 0;
                _valueZ.Value = 0;
                break;
        }
    }

    private void UpdateModeSummary()
    {
        var mode = (EditorViewportMode)_mode.Selected;
        _modeSummary.Text = ViewportModeDescription(mode) +
                            "\n\nLe changement passe par la commande de profil, reste annulable et n’est écrit qu’après Enregistrer le profil.";
    }

    private void UpdateSummaries()
    {
        _selectionSummary.Text = _session.SelectedEntityIds.Count switch
        {
            0 => "Aucun objet sélectionné",
            1 => "1 objet sélectionné · Ctrl+clic pour composer un groupe",
            _ => $"{_session.SelectedEntityIds.Count} objets sélectionnés · un seul historique atomique",
        };
        var camera = _session.CameraProfile;
        _cameraState.Text =
            $"{(camera.Pose.Projection == EditorCameraProjection.Perspective ? "Perspective" : "Orthographique")} · " +
            $"vitesse {camera.NavigationSpeed:0.##} · sensibilité {camera.MouseSensitivity:0.##} · " +
            $"{camera.Bookmarks.Count} point(s) de vue" +
            (_session.CameraProfileHasUnsavedChanges ? " · non enregistré" : " · enregistré");
    }

    private void ShowValidation(string message, bool error = false)
    {
        _validation.Text = message;
        _validation.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        _status.Text = error ? "ACTION REFUSÉE" : "PRÊT";
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private void ShowCameraStatus(string message, bool error = false)
    {
        ShowValidation(message, error);
        UpdateSummaries();
    }

    private void AddViewButton(Container parent, string label, EditorCameraView view)
    {
        var button = StudioStyle.Button(label, compact: true);
        button.Pressed += () => SetCameraView(view);
        parent.AddChild(button);
    }

    private static Control BuildScrollableTab(string name, out VBoxContainer body)
    {
        var root = new MarginContainer { Name = name };
        root.AddThemeConstantOverride("margin_left", 10);
        root.AddThemeConstantOverride("margin_top", 12);
        root.AddThemeConstantOverride("margin_right", 10);
        root.AddThemeConstantOverride("margin_bottom", 10);
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        root.AddChild(scroll);
        body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 9);
        scroll.AddChild(body);
        return root;
    }

    private static VBoxContainer BuildColumn(Container parent, string title, float width)
    {
        var panel = StudioStyle.Panel("ViewportTools" + title, StudioStyle.Surface, 9, 1);
        panel.CustomMinimumSize = new Vector2(width, 0);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        parent.AddChild(panel);
        var margin = StudioStyle.Margin(11, 10, 11, 10);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 7);
        margin.AddChild(column);
        column.AddChild(StudioStyle.SectionLabel(title));
        return column;
    }

    private static VBoxContainer Field(string label, Control control)
    {
        var field = new VBoxContainer();
        field.AddThemeConstantOverride("separation", 3);
        field.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        field.AddChild(control);
        return field;
    }

    private static SpinBox AddSpin(
        Container parent,
        string label,
        double minimum,
        double maximum,
        double step,
        double value)
    {
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            Value = value,
            UpdateOnTextChanged = true,
        };
        StudioStyle.ApplySpinBox(spin);
        parent.AddChild(Field(label, spin));
        return spin;
    }

    private static SpinBox AddVectorSpin(
        Container parent,
        string label,
        double minimum,
        double maximum,
        double step)
    {
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            UpdateOnTextChanged = true,
        };
        StudioStyle.ApplySpinBox(spin);
        parent.AddChild(Field(label, spin));
        return spin;
    }

    private static CheckBox AddCheck(Container parent, string text)
    {
        var check = new CheckBox { Text = text };
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        parent.AddChild(check);
        return check;
    }

    private static OptionButton EnumOption<T>(
        Container parent,
        string label,
        Func<T, string> name)
        where T : struct, Enum
    {
        var option = new OptionButton();
        StudioStyle.ApplyOptionButton(option);
        foreach (var value in Enum.GetValues<T>())
        {
            option.AddItem(name(value));
        }
        parent.AddChild(Field(label, option));
        return option;
    }

    private static string ViewportModeName(EditorViewportMode mode) => mode switch
    {
        EditorViewportMode.Simulation => "Simulation",
        EditorViewportMode.Lighting => "Éclairage authored",
        EditorViewportMode.Navigation => "Navigation",
        EditorViewportMode.Collisions => "Collisions",
        EditorViewportMode.Performance => "Performance",
        _ => "Édition",
    };

    private static string ViewportModeDescription(EditorViewportMode mode) => mode switch
    {
        EditorViewportMode.Simulation => "Inspecte la composition comme une scène de jeu, sans masquer le contexte du Studio.",
        EditorViewportMode.Lighting => "Désactive les lumières de confort de l’éditeur afin de juger l’éclairage authored.",
        EditorViewportMode.Navigation => "Superpose la grille de navigation et son pas canonique sur la zone.",
        EditorViewportMode.Collisions => "Affiche des volumes filaires autour des objets pour repérer les emprises.",
        EditorViewportMode.Performance => "Coupe les ombres de diagnostic et réduit les mises à jour inutiles de la vue.",
        _ => "Active sélection, gizmos, placement et édition directe dans la zone 3D.",
    };

    private static string TransformToolName(EditorTransformTool tool) => tool switch
    {
        EditorTransformTool.Rotate => "Rotation",
        EditorTransformTool.Scale => "Échelle",
        _ => "Translation",
    };

    private static string CoordinateName(EditorCoordinateSpace space) =>
        space == EditorCoordinateSpace.Local ? "Local à chaque objet" : "Monde";

    private static string PivotName(EditorPivotMode pivot) => pivot switch
    {
        EditorPivotMode.Individual => "Pivot individuel",
        EditorPivotMode.ActiveObject => "Objet actif",
        _ => "Centre de la sélection",
    };

    private static string AlignmentName(EditorTransformAlignment alignment) => alignment switch
    {
        EditorTransformAlignment.MinimumX => "Bord gauche (X min)",
        EditorTransformAlignment.MaximumX => "Bord droit (X max)",
        EditorTransformAlignment.CenterX => "Centre X",
        EditorTransformAlignment.MinimumY => "Bas (Y min)",
        EditorTransformAlignment.MaximumY => "Haut (Y max)",
        EditorTransformAlignment.CenterY => "Centre Y",
        EditorTransformAlignment.MinimumZ => "Avant (Z min)",
        EditorTransformAlignment.MaximumZ => "Arrière (Z max)",
        EditorTransformAlignment.CenterZ => "Centre Z",
        EditorTransformAlignment.Ground => "Sol (Y = 0)",
        _ => "Aucun",
    };
}
