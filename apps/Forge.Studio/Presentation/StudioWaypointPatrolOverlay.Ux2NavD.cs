using Forge.Domain.Navigation;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed class StudioWaypointPatrolOverlayUx2NavD : Control
{
    private static readonly PatrolTraversalPolicy[] TraversalPolicies =
        Enum.GetValues<PatrolTraversalPolicy>();
    private static readonly ArrivalFacingMode[] FacingModes = Enum.GetValues<ArrivalFacingMode>();

    private readonly StudioSessionController _sourceSession;
    private readonly StudioWorldViewport _worldViewport;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2NavDEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _lastSourceRestored = true;
    private bool _lastUiTransaction;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly OptionButton _route;
    private readonly Button _newRoute;
    private readonly OptionButton _waypoint;
    private readonly Label _selection;
    private readonly VBoxContainer _editor;
    private readonly Control _routeCard;
    private readonly OptionButton _traversal;
    private readonly LineEdit _scriptHandler;
    private readonly OptionButton _point;
    private readonly Button _addPoint;
    private readonly Button _removePoint;
    private readonly Button _movePointUp;
    private readonly Button _movePointDown;
    private readonly VBoxContainer _pointFields;
    private readonly SpinBox _pointX;
    private readonly SpinBox _pointY;
    private readonly SpinBox _pointZ;
    private readonly SpinBox _pointWait;
    private readonly SpinBox _pointFacing;
    private readonly SpinBox _pointSpeed;
    private readonly SpinBox _pointWeight;
    private readonly LineEdit _pointAction;
    private readonly LineEdit _pointBranch;
    private readonly Control _arrivalCard;
    private readonly CheckBox _arrivalEnabled;
    private readonly VBoxContainer _arrivalFields;
    private readonly OptionButton _arrivalFacing;
    private readonly OptionButton _arrivalTarget;
    private readonly VBoxContainer _targetField;
    private readonly SpinBox _arrivalDirection;
    private readonly SpinBox _arrivalWait;
    private readonly LineEdit _arrivalAnimation;
    private readonly LineEdit _arrivalInteraction;
    private readonly LineEdit _arrivalCallback;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly VBoxContainer _runtime;
    private readonly Label _summary;
    private readonly Label _authority;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx2NavDAreaChoice> _areaChoices = [];
    private IReadOnlyList<StudioUx2NavDRouteChoice> _routeChoices = [];
    private IReadOnlyList<StudioUx2NavDWaypointChoice> _waypointChoices = [];
    private IReadOnlyList<StudioUx2NavDArrivalTargetChoice> _targetChoices = [];
    private StudioUx2NavDRouteDraft? _routeDraft;
    private StudioUx2NavDArrivalDraft? _arrivalDraft;
    private StudioUx2NavDDraft? _draft;
    private StudioUx2NavDPreview? _lastPreview;
    private Guid? _selectedPointId;
    private bool _updating;
    private bool _explicitSelection;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? NavigationChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _areaChoices.Count;
    internal bool HasExplicitSelection => _draft is not null && _explicitSelection && _area.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady;
    internal bool ShowsViewportOverlay => _lastPreview is { Succeeded: true } &&
                                          _worldViewport.NavigationOverlayPrimitiveCountUx2NavB > 0;
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
                                                 _lastPreview.Diagnostics.All(item =>
                                                     !string.IsNullOrWhiteSpace(item.Code) &&
                                                     !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                          ReferenceEquals(_session, _evidenceSession);
    internal bool EvidenceSourceUntouched => _evidenceSourceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;
    internal bool EvidenceResourcesReleased => _evidenceSession is null && _evidenceTempRoot is null &&
                                               _lastEvidenceResourcesReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession) && _lastSourceRestored;
    internal bool LastUiTransaction => _lastUiTransaction;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantLabelsUx2NavD(_dialog).Select(item => item.Text));
            return HasExplicitSelection && text.Contains("Points ordonnés", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Parcours", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Arrivée", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) && !ContainsUuidUx2NavD(text);
        }
    }

    internal StudioWaypointPatrolOverlayUx2NavD(StudioSessionController session,
        StudioWorldViewport worldViewport)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        _worldViewport = worldViewport ?? throw new ArgumentNullException(nameof(worldViewport));
        Name = "StudioWaypointPatrolOverlayUx2NavD";
        Visible = false;
        ZIndex = 457;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _dialog = StudioStyle.Panel("WaypointPatrolDialogUx2NavD", StudioStyle.Chrome, 12, 2);
        _dialog.MouseFilter = MouseFilterEnum.Stop;
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 7);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("NAVIGATION · PATROUILLES ET ARRIVÉES", 10,
            StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Routes ordonnées et comportement d’arrivée", 19));
        heading.AddChild(StudioStyle.Label(
            "WPT-007 / 008 / 009 · ordre, branches, poids, vitesse, attente, actions et orientation. " +
            "L’aperçu sec utilise le service canonique sans déplacer d’acteur ni exécuter d’action.",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var selectors = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selectors.AddThemeConstantOverride("separation", 5);
        root.AddChild(selectors);
        var areaRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        areaRow.AddThemeConstantOverride("separation", 7);
        selectors.AddChild(areaRow);
        areaRow.AddChild(StudioStyle.Label("Zone", 10, StudioStyle.Muted));
        _area = StudioUx2NavCControls.Options();
        _area.ItemSelected += SelectArea;
        areaRow.AddChild(_area);
        areaRow.AddChild(StudioStyle.Label("Route", 10, StudioStyle.Muted));
        _route = StudioUx2NavCControls.Options();
        _route.ItemSelected += SelectRoute;
        areaRow.AddChild(_route);
        _newRoute = StudioStyle.Button("Nouvelle route", compact: true);
        _newRoute.TooltipText = "Préparer explicitement une route valide de deux points";
        _newRoute.Pressed += AddRoute;
        areaRow.AddChild(_newRoute);
        var arrivalRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        arrivalRow.AddThemeConstantOverride("separation", 7);
        selectors.AddChild(arrivalRow);
        arrivalRow.AddChild(StudioStyle.Label("Arrivée du repère", 10, StudioStyle.Muted));
        _waypoint = StudioUx2NavCControls.Options();
        _waypoint.ItemSelected += SelectWaypoint;
        arrivalRow.AddChild(_waypoint);
        arrivalRow.AddChild(StudioStyle.Label(
            "Route et arrivée peuvent être éditées ensemble ou séparément.", 9, StudioStyle.Muted));
        _selection = StudioStyle.Label(
            "Choisissez explicitement une zone puis une route ou un repère ; aucune fiche n’est chargée automatiquement.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var bodyHost = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 430),
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(scroll);
        _editor = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _editor.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_editor);

        var routeContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        routeContent.AddThemeConstantOverride("separation", 6);
        var policyRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        policyRow.AddThemeConstantOverride("separation", 6);
        _traversal = StudioUx2NavCControls.Options(TraversalPolicies.Select(HumanTraversal).ToArray());
        _scriptHandler = StudioUx2NavCControls.Text("patrol.choose");
        policyRow.AddChild(StudioUx2NavCControls.Field("Parcours", _traversal));
        policyRow.AddChild(StudioUx2NavCControls.Field("Gestionnaire scripté", _scriptHandler, 180));
        routeContent.AddChild(policyRow);
        var pointRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        pointRow.AddThemeConstantOverride("separation", 5);
        _point = StudioUx2NavCControls.Options();
        _point.ItemSelected += SelectPoint;
        pointRow.AddChild(StudioUx2NavCControls.Field("Point ordonné", _point, 180));
        _addPoint = StudioStyle.Button("Ajouter", compact: true);
        _addPoint.Pressed += AddPoint;
        pointRow.AddChild(_addPoint);
        _removePoint = StudioStyle.Button("Retirer", compact: true);
        _removePoint.Pressed += RemovePoint;
        pointRow.AddChild(_removePoint);
        _movePointUp = StudioStyle.Button("Monter", compact: true);
        _movePointUp.TooltipText = "Déplacer le point avec son identité et ses actions";
        _movePointUp.Pressed += MovePointUp;
        pointRow.AddChild(_movePointUp);
        _movePointDown = StudioStyle.Button("Descendre", compact: true);
        _movePointDown.TooltipText = "Déplacer le point avec son identité et ses actions";
        _movePointDown.Pressed += MovePointDown;
        pointRow.AddChild(_movePointDown);
        routeContent.AddChild(pointRow);
        _pointFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _pointFields.AddThemeConstantOverride("separation", 5);
        routeContent.AddChild(_pointFields);
        var positionRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        positionRow.AddThemeConstantOverride("separation", 5);
        _pointX = StudioUx2NavCControls.Number(-100_000, 100_000, .25);
        _pointY = StudioUx2NavCControls.Number(-100_000, 100_000, .25);
        _pointZ = StudioUx2NavCControls.Number(-100_000, 100_000, .25);
        positionRow.AddChild(StudioUx2NavCControls.Field("Position X", _pointX));
        positionRow.AddChild(StudioUx2NavCControls.Field("Altitude Y", _pointY));
        positionRow.AddChild(StudioUx2NavCControls.Field("Position Z", _pointZ));
        _pointFields.AddChild(positionRow);
        var timingRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        timingRow.AddThemeConstantOverride("separation", 5);
        _pointWait = StudioUx2NavCControls.Number(0, 3600, .25);
        _pointFacing = StudioUx2NavCControls.Number(-360, 360, 1);
        _pointSpeed = StudioUx2NavCControls.Number(.01, 100, .1);
        _pointWeight = StudioUx2NavCControls.Number(0, 1_000_000, .1);
        timingRow.AddChild(StudioUx2NavCControls.Field("Attente (s)", _pointWait));
        timingRow.AddChild(StudioUx2NavCControls.Field("Orientation (°)", _pointFacing));
        timingRow.AddChild(StudioUx2NavCControls.Field("Vitesse", _pointSpeed));
        timingRow.AddChild(StudioUx2NavCControls.Field("Poids", _pointWeight));
        _pointFields.AddChild(timingRow);
        var actionRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actionRow.AddThemeConstantOverride("separation", 5);
        _pointAction = StudioUx2NavCControls.Text("observe");
        _pointBranch = StudioUx2NavCControls.Text("alert.high");
        actionRow.AddChild(StudioUx2NavCControls.Field("Action nommée", _pointAction));
        actionRow.AddChild(StudioUx2NavCControls.Field("Condition de branche", _pointBranch));
        _pointFields.AddChild(actionRow);
        routeContent.AddChild(StudioStyle.Label(
            "Monter/Descendre conserve l’identité du point. Les étapes canoniques suivent l’ordre affiché.",
            9, StudioStyle.Muted, wrap: true));
        var routePanel = StudioUx2NavCControls.Card("WaypointRoutePanelUx2NavD",
            "Points ordonnés et parcours",
            "La route reste un graphe ordonné ; les étapes runtime sont simulées sans acteur live.",
            routeContent);
        _editor.AddChild(routePanel);
        _routeCard = routePanel;

        var arrivalContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        arrivalContent.AddThemeConstantOverride("separation", 6);
        _arrivalEnabled = StudioUx2NavCControls.Check("Activer un comportement d’arrivée");
        arrivalContent.AddChild(_arrivalEnabled);
        _arrivalFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _arrivalFields.AddThemeConstantOverride("separation", 5);
        arrivalContent.AddChild(_arrivalFields);
        var facingRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        facingRow.AddThemeConstantOverride("separation", 5);
        _arrivalFacing = StudioUx2NavCControls.Options(FacingModes.Select(HumanFacing).ToArray());
        _arrivalDirection = StudioUx2NavCControls.Number(-360, 360, 1);
        _arrivalWait = StudioUx2NavCControls.Number(0, 3600, .25);
        facingRow.AddChild(StudioUx2NavCControls.Field("Orientation d’arrivée", _arrivalFacing));
        facingRow.AddChild(StudioUx2NavCControls.Field("Direction (°)", _arrivalDirection));
        facingRow.AddChild(StudioUx2NavCControls.Field("Attente (s)", _arrivalWait));
        _arrivalFields.AddChild(facingRow);
        _arrivalTarget = StudioUx2NavCControls.Options();
        _targetField = StudioUx2NavCControls.Field("Cible humaine", _arrivalTarget);
        _arrivalFields.AddChild(_targetField);
        var arrivalActionRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        arrivalActionRow.AddThemeConstantOverride("separation", 5);
        _arrivalAnimation = StudioUx2NavCControls.Text("arrival.salute");
        _arrivalInteraction = StudioUx2NavCControls.Text("arrival.inspect");
        _arrivalCallback = StudioUx2NavCControls.Text("waypoint.arrived");
        arrivalActionRow.AddChild(StudioUx2NavCControls.Field("Animation", _arrivalAnimation));
        arrivalActionRow.AddChild(StudioUx2NavCControls.Field("Interaction", _arrivalInteraction));
        arrivalActionRow.AddChild(StudioUx2NavCControls.Field("Callback", _arrivalCallback));
        _arrivalFields.AddChild(arrivalActionRow);
        arrivalContent.AddChild(StudioStyle.Label(
            "Une cible technique inconnue reste protégée : elle n’est ni affichée ni altérée tant qu’elle n’est pas remplacée.",
            9, StudioStyle.Muted, wrap: true));
        var arrivalPanel = StudioUx2NavCControls.Card("WaypointArrivalPanelUx2NavD",
            "Arrivée au waypoint",
            "Orientation, cible, animation, attente, interaction et callback sont résolus en preview pur.",
            arrivalContent);
        _editor.AddChild(arrivalPanel);
        _arrivalCard = arrivalPanel;

        var resultRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        resultRow.AddThemeConstantOverride("separation", 8);
        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _runtime = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        resultRow.AddChild(StudioUx2NavCControls.Card("WaypointPatrolDiffUx2NavD",
            "Diff sémantique", "Ce que remplacera la commande atomique.", _diff));
        resultRow.AddChild(StudioUx2NavCControls.Card("WaypointPatrolDiagnosticsUx2NavD",
            "Diagnostics", "Codes stables et chemins humains avant application.", _diagnostics));
        resultRow.AddChild(StudioUx2NavCControls.Card("WaypointPatrolRuntimeUx2NavD",
            "Preview runtime", "Étapes calculées par WaypointRouteService.", _runtime));
        _editor.AddChild(resultRow);
        _summary = StudioStyle.Label("Aucun aperçu calculé.", 10, StudioStyle.Muted, wrap: true);
        _editor.AddChild(_summary);
        _authority = StudioStyle.Label(
            "Autorité : aucune action d’arrivée n’est exécutée par cette surface.", 10,
            StudioStyle.Muted, wrap: true);
        _editor.AddChild(_authority);

        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 6);
        root.AddChild(footer);
        _status = StudioStyle.Label("Sélection explicite requise.", 10, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(_status);
        _preview = StudioStyle.Button("Aperçu sec", compact: true);
        _preview.TooltipText = "Valider et calculer le diff sans mutation";
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("Appliquer", primary: true, compact: true);
        _apply.Disabled = true;
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += Redo;
        footer.AddChild(_redo);
        _save = StudioStyle.Button("Sauvegarder", compact: true);
        _save.Pressed += Save;
        footer.AddChild(_save);

        WireDraftSignals();
        SetEditorEnabled(false);
        UpdateActionState();
        SetProcessUnhandledKeyInput(true);
        Resized += UpdateDialogBounds;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }
        if (key.Keycode == Key.Escape)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
        else if (key.AltPressed && key.Keycode == Key.Up)
        {
            MovePointUp();
            GetViewport().SetInputAsHandled();
        }
        else if (key.AltPressed && key.Keycode == Key.Down)
        {
            MovePointDown();
            GetViewport().SetInputAsHandled();
        }
    }

    internal void Open()
    {
        ReleaseEvidenceWorkspace();
        _session = _sourceSession;
        _sourceSession.CancelPatrolUx2NavD();
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;
        PopulateAreas();
        ClearSelection();
        UpdateDialogBounds();
        SetStatus("Choisissez explicitement une zone puis une route ou un repère d’arrivée.");
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null)
        {
            return;
        }
        if (_evidenceSession is not null)
        {
            _evidenceSession.CancelPatrolUx2NavD();
        }
        else
        {
            _sourceSession.CancelPatrolUx2NavD();
        }
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _routeDraft = null;
        _arrivalDraft = null;
        _draft = null;
        _lastPreview = null;
        _selectedPointId = null;
        _canonicalPreviewReady = false;
        _explicitSelection = false;
        ReleaseEvidenceWorkspace();
        RestoreSourceViewport();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!string.Equals(mode, "ux2-nav-d", StringComparison.OrdinalIgnoreCase) ||
            _sourceSession.Project is null || _sourceSession.History is null)
        {
            return false;
        }
        ReleaseEvidenceWorkspace();
        _evidenceSourceSnapshot = CaptureSourceSnapshot();
        _lastEvidenceSourceUntouched = true;
        _lastEvidenceResourcesReleased = false;
        _lastSourceRestored = false;
        var project = _sourceSession.CreateUx2NavDEvidenceProject();
        if (project is null)
        {
            return false;
        }
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-d-evidence-" + Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                _lastEvidenceResourcesReleased =
                    StudioSessionController.DeleteUx2NavDTemporaryWorkspace(tempRoot);
                return false;
            }
            _evidenceTempRoot = tempRoot;
            var evidence = new StudioSessionController();
            if (!evidence.Open(tempRoot).Succeeded)
            {
                evidence.Close();
                _lastEvidenceResourcesReleased =
                    StudioSessionController.DeleteUx2NavDTemporaryWorkspace(tempRoot);
                _evidenceTempRoot = null;
                return false;
            }
            _evidenceSession = evidence;
            _session = evidence;
            Visible = true;
            MouseFilter = MouseFilterEnum.Stop;
            PopulateAreas();
            var areaIndex = _areaChoices.ToList().FindIndex(item => item.IsEvidence);
            if (areaIndex < 0)
            {
                Close();
                return false;
            }
            _area.Selected = areaIndex + 1;
            SelectArea(areaIndex + 1);
            var routeIndex = _routeChoices.ToList().FindIndex(item => item.IsEvidence);
            var waypointIndex = _waypointChoices.ToList().FindIndex(item => item.IsEvidence);
            if (routeIndex < 0 || waypointIndex < 0)
            {
                Close();
                return false;
            }
            _route.Selected = routeIndex + 1;
            SelectRoute(routeIndex + 1);
            _waypoint.Selected = waypointIndex + 1;
            SelectWaypoint(waypointIndex + 1);
            _point.Selected = 1;
            SelectPoint(1);
            _pointWait.Value = 1.5;
            _pointSpeed.Value = 3.5;
            _pointAction.Text = "signal.ready";
            _pointBranch.Text = "alert.high";
            _arrivalEnabled.ButtonPressed = true;
            _arrivalFacing.Selected = Array.IndexOf(FacingModes, ArrivalFacingMode.Direction);
            _arrivalDirection.Value = 135;
            _arrivalWait.Value = 1.25;
            _arrivalAnimation.Text = "arrival.salute";
            _arrivalInteraction.Text = "arrival.inspect";
            _arrivalCallback.Text = "waypoint.arrived";
            DraftChanged();
            Preview();
            UpdateDialogBounds();
            return HasExplicitSelection && _selectedPointId is not null &&
                   _canonicalPreviewReady && ShowsViewportOverlay && UsesIsolatedEvidence &&
                   EvidenceSourceUntouched && ShowsHumanSurface;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_D_EVIDENCE_FAILED type={exception.GetType().Name} message={exception.Message}");
            Close();
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        _lastUiTransaction = false;
        try
        {
            if (!PrepareEvidence("ux2-nav-d"))
            {
                Close();
                return false;
            }
            var beforeOrder = _draft!.Route!.Points.Select(item => item.Id).ToArray();
            _movePointDown.EmitSignal(Button.SignalName.Pressed);
            var movedOrder = _draft?.Route?.Points.Select(item => item.Id).ToArray() ?? [];
            var movedThroughUi = beforeOrder.Length == movedOrder.Length &&
                                 !beforeOrder.SequenceEqual(movedOrder) &&
                                 movedOrder[1] == beforeOrder[0];
            _preview.EmitSignal(Button.SignalName.Pressed);
            var dryRun = _canonicalPreviewReady && !_apply.Disabled;
            var projectBeforeApply = _session.Project;
            _apply.EmitSignal(Button.SignalName.Pressed);
            var appliedProject = _session.Project;
            var applied = appliedProject is not null &&
                          !ReferenceEquals(projectBeforeApply, appliedProject);
            _undo.EmitSignal(Button.SignalName.Pressed);
            var undone = ReferenceEquals(projectBeforeApply, _session.Project);
            _redo.EmitSignal(Button.SignalName.Pressed);
            var redone = ReferenceEquals(appliedProject, _session.Project);
            _save.EmitSignal(Button.SignalName.Pressed);
            var saved = !_session.IsDirty;
            _lastUiTransaction = movedThroughUi && dryRun && applied && undone && redone && saved;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_D_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            _lastUiTransaction = false;
        }
        finally
        {
            Close();
        }
        return _lastUiTransaction && EvidenceResourcesReleased && SourceSessionRestored &&
               _lastEvidenceSourceUntouched;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        var approval = _sourceSession.CapturePatrolApprovalUx2NavD();
        Open();
        _sourceSession.RestorePatrolApprovalUx2NavD(approval);
    }

    private void PopulateAreas()
    {
        _updating = true;
        _area.Clear();
        _area.AddItem("Choisir une zone…");
        _areaChoices = _session.PatrolAreaChoicesUx2NavD;
        foreach (var choice in _areaChoices)
        {
            _area.AddItem(choice.Label);
        }
        _area.Selected = 0;
        _updating = false;
    }

    private void SelectArea(long index)
    {
        if (_updating)
        {
            return;
        }
        _session.CancelPatrolUx2NavD();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _routeDraft = null;
        _arrivalDraft = null;
        _draft = null;
        _selectedPointId = null;
        _explicitSelection = false;
        _updating = true;
        _route.Clear();
        _route.AddItem("Choisir une route…");
        _waypoint.Clear();
        _waypoint.AddItem("Choisir un repère d’arrivée…");
        _routeChoices = [];
        _waypointChoices = [];
        if (index > 0 && index <= _areaChoices.Count)
        {
            var choice = _areaChoices[(int)index - 1];
            _routeChoices = _session.PatrolRouteChoicesUx2NavD(choice.AreaId);
            foreach (var route in _routeChoices)
            {
                _route.AddItem(route.Label);
            }
            _waypointChoices = _session.ArrivalWaypointChoicesUx2NavD(choice.AreaId);
            foreach (var waypoint in _waypointChoices)
            {
                _waypoint.AddItem(waypoint.Label);
            }
            BindViewportToArea(_session, choice.AreaId);
            _selection.Text = $"{choice.Label} · choisissez explicitement une route, une arrivée, ou les deux.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        }
        else
        {
            _selection.Text = "Choisissez explicitement une zone ; aucune fiche n’est chargée automatiquement.";
        }
        _route.Selected = 0;
        _waypoint.Selected = 0;
        _updating = false;
        SetEditorEnabled(false);
        RenderPreview(null);
        UpdateActionState();
    }

    private void SelectRoute(long index)
    {
        if (_updating || _area.Selected <= 0 || _area.Selected > _areaChoices.Count)
        {
            return;
        }
        SaveControlValuesIntoDraft();
        _routeDraft = index > 0 && index <= _routeChoices.Count
            ? _session.CreatePatrolRouteDraftUx2NavD(
                _areaChoices[_area.Selected - 1].AreaId, _routeChoices[(int)index - 1].RouteId)
            : null;
        _selectedPointId = null;
        RecomposeDraft("route");
    }

    private void AddRoute()
    {
        if (_area.Selected <= 0 || _area.Selected > _areaChoices.Count)
        {
            SetStatus("Choisissez d’abord une zone.", true);
            return;
        }
        SaveControlValuesIntoDraft();
        var areaId = _areaChoices[_area.Selected - 1].AreaId;
        _routeDraft = _session.CreateNewPatrolRouteDraftUx2NavD(areaId);
        if (_routeDraft is null)
        {
            SetStatus("La nouvelle route ne peut pas être préparée.", true);
            return;
        }
        _route.Selected = 0;
        _selectedPointId = null;
        RecomposeDraft("nouvelle route explicite");
    }

    private void SelectWaypoint(long index)
    {
        if (_updating || _area.Selected <= 0 || _area.Selected > _areaChoices.Count)
        {
            return;
        }
        SaveControlValuesIntoDraft();
        _arrivalDraft = index > 0 && index <= _waypointChoices.Count
            ? _session.CreateArrivalDraftUx2NavD(_areaChoices[_area.Selected - 1].AreaId,
                _waypointChoices[(int)index - 1].WaypointId)
            : null;
        RecomposeDraft("arrivée");
    }

    private void RecomposeDraft(string selectionKind)
    {
        if (_area.Selected <= 0 || _area.Selected > _areaChoices.Count)
        {
            return;
        }
        var area = _areaChoices[_area.Selected - 1];
        _draft = _session.ComposePatrolDraftUx2NavD(area.AreaId, _routeDraft, _arrivalDraft);
        _explicitSelection = _draft is not null;
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _session.CancelPatrolUx2NavD();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        if (_draft is null)
        {
            SetEditorEnabled(false);
            _selection.Text = $"{area.Label} · choisissez explicitement une route ou un repère d’arrivée.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
            RenderPreview(null);
            UpdateActionState();
            return;
        }
        LoadDraft(_draft);
        SetEditorEnabled(true);
        _selection.Text = $"{area.Label} · {selectionKind} sélectionnée explicitement · non appliquée";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
        SetStatus("Brouillon chargé · choisissez un point si nécessaire, puis prévisualisez.");
        UpdateActionState();
    }

    private void LoadDraft(StudioUx2NavDDraft draft)
    {
        _updating = true;
        _routeCard.Visible = draft.Route is not null;
        if (draft.Route is { } route)
        {
            _traversal.Selected = Array.IndexOf(TraversalPolicies, route.Traversal);
            _scriptHandler.Text = route.ScriptHandler;
            PopulatePoints(route, _selectedPointId);
        }
        else
        {
            _point.Clear();
            _point.AddItem("Choisir un point…");
            _point.Selected = 0;
            _selectedPointId = null;
            _pointFields.Visible = false;
        }
        _arrivalCard.Visible = draft.Arrival is not null;
        if (draft.Arrival is { } arrival)
        {
            _arrivalEnabled.ButtonPressed = arrival.HasArrival;
            _arrivalFields.Visible = arrival.HasArrival;
            _arrivalFacing.Selected = Array.IndexOf(FacingModes, arrival.Facing);
            _arrivalDirection.Value = arrival.DirectionDegrees;
            _arrivalWait.Value = arrival.WaitSeconds;
            _arrivalAnimation.Text = arrival.Animation;
            _arrivalInteraction.Text = arrival.Interaction;
            _arrivalCallback.Text = arrival.Callback;
            PopulateTargets(draft.AreaId, arrival);
        }
        else
        {
            _arrivalTarget.Clear();
            _arrivalTarget.AddItem("Choisir une cible…");
            _arrivalTarget.Selected = 0;
            _targetChoices = [];
        }
        UpdateConditionalFields();
        _updating = false;
        RenderPreview(null);
    }

    private void PopulatePoints(StudioUx2NavDRouteDraft route, Guid? selectedId)
    {
        _point.Clear();
        _point.AddItem("Choisir un point…");
        for (var index = 0; index < route.Points.Count; index++)
        {
            var item = route.Points[index];
            var action = string.IsNullOrWhiteSpace(item.Action) ? "sans action" : item.Action;
            _point.AddItem($"Point {index + 1} · {action} · attente {item.WaitSeconds:0.##} s");
        }
        var selectedIndex = selectedId is null
            ? -1
            : route.Points.ToList().FindIndex(item => item.Id == selectedId.Value);
        if (selectedIndex >= 0)
        {
            _point.Selected = selectedIndex + 1;
            _selectedPointId = route.Points[selectedIndex].Id;
            LoadPoint(route.Points[selectedIndex]);
        }
        else
        {
            _point.Selected = 0;
            _selectedPointId = null;
            _pointFields.Visible = false;
        }
    }

    private void PopulateTargets(Forge.Domain.Areas.AreaId areaId,
        StudioUx2NavDArrivalDraft arrival)
    {
        _arrivalTarget.Clear();
        _arrivalTarget.AddItem("Choisir une cible…");
        _targetChoices = _session.ArrivalTargetChoicesUx2NavD(areaId, arrival);
        foreach (var choice in _targetChoices)
        {
            _arrivalTarget.AddItem(choice.Label);
        }
        var selected = string.IsNullOrWhiteSpace(arrival.TargetId)
            ? -1
            : _targetChoices.ToList().FindIndex(item =>
                string.Equals(item.Value, arrival.TargetId, StringComparison.Ordinal));
        _arrivalTarget.Selected = selected + 1;
    }

    private void SelectPoint(long index)
    {
        if (_updating || _routeDraft is null)
        {
            return;
        }
        SaveControlValuesIntoDraft();
        if (index <= 0 || index > _routeDraft.Points.Count)
        {
            _selectedPointId = null;
            _pointFields.Visible = false;
            InvalidatePreview("Choisissez explicitement un point pour modifier ses paramètres.");
            return;
        }
        _selectedPointId = _routeDraft.Points[(int)index - 1].Id;
        _updating = true;
        LoadPoint(_routeDraft.Points[(int)index - 1]);
        _updating = false;
        InvalidatePreview($"Point {index} sélectionné explicitement · relancez l’aperçu après modification.");
        UpdateActionState();
    }

    private void LoadPoint(StudioUx2NavDPointDraft point)
    {
        _pointX.Value = point.Position.X;
        _pointY.Value = point.Position.Y;
        _pointZ.Value = point.Position.Z;
        _pointWait.Value = point.WaitSeconds;
        _pointFacing.Value = point.FacingDegrees;
        _pointSpeed.Value = point.Speed;
        _pointWeight.Value = point.Weight;
        _pointAction.Text = point.Action;
        _pointBranch.Text = point.BranchCondition;
        _pointFields.Visible = true;
    }

    private void AddPoint()
    {
        SaveControlValuesIntoDraft();
        var draft = _draft;
        if (draft?.Route is not { } route)
        {
            SetStatus("Choisissez d’abord une route.", true);
            return;
        }
        var previous = route.Points.LastOrDefault();
        var previousPosition = previous?.Position ?? Vector3Value.Zero;
        var x = Math.Clamp(previousPosition.X + 1f, -draft.AreaWidth / 2f,
            draft.AreaWidth / 2f);
        var z = Math.Clamp(previousPosition.Z + 1f, -draft.AreaDepth / 2f,
            draft.AreaDepth / 2f);
        var point = new StudioUx2NavDPointDraft(Guid.NewGuid(), new Vector3Value(x,
            previousPosition.Y, z), 0, previous?.FacingDegrees ?? 0,
            Math.Max(.1, previous?.Speed ?? 2),
            string.Empty, string.Empty, 1);
        _routeDraft = route with { Points = route.Points.Append(point).ToArray() };
        _draft = draft with { Route = _routeDraft };
        _selectedPointId = point.Id;
        _updating = true;
        PopulatePoints(_routeDraft, point.Id);
        _updating = false;
        InvalidatePreview($"Point {route.Points.Count + 1} ajouté explicitement · aucun document modifié.");
        UpdateActionState();
    }

    private void RemovePoint()
    {
        SaveControlValuesIntoDraft();
        var draft = _draft;
        if (draft?.Route is not { } route || _selectedPointId is null)
        {
            SetStatus("Choisissez explicitement un point à retirer.", true);
            return;
        }
        if (route.Points.Count <= 2)
        {
            SetStatus("Une route doit conserver au moins deux points.", true);
            return;
        }
        var index = route.Points.ToList().FindIndex(item => item.Id == _selectedPointId.Value);
        if (index < 0)
        {
            return;
        }
        var points = route.Points.Where(item => item.Id != _selectedPointId.Value).ToArray();
        _routeDraft = route with { Points = points };
        _draft = draft with { Route = _routeDraft };
        _selectedPointId = points[Math.Min(index, points.Length - 1)].Id;
        _updating = true;
        PopulatePoints(_routeDraft, _selectedPointId);
        _updating = false;
        InvalidatePreview("Point retiré du brouillon · les étapes seront reconstruites après validation.");
        UpdateActionState();
    }

    private void MovePointUp() => MovePoint(-1);

    private void MovePointDown() => MovePoint(1);

    private void MovePoint(int direction)
    {
        SaveControlValuesIntoDraft();
        var draft = _draft;
        if (draft?.Route is not { } route || _selectedPointId is null)
        {
            SetStatus("Choisissez explicitement un point à déplacer.", true);
            return;
        }
        var points = route.Points.ToList();
        var index = points.FindIndex(item => item.Id == _selectedPointId.Value);
        var destination = index + direction;
        if (index < 0 || destination < 0 || destination >= points.Count)
        {
            SetStatus("Le point est déjà à cette extrémité de la route.");
            return;
        }
        (points[index], points[destination]) = (points[destination], points[index]);
        _routeDraft = route with { Points = points };
        _draft = draft with { Route = _routeDraft };
        _updating = true;
        PopulatePoints(_routeDraft, _selectedPointId);
        _updating = false;
        InvalidatePreview(
            $"Point déplacé en position {destination + 1} avec son identité, sa branche et ses actions.");
        UpdateActionState();
    }

    private StudioUx2NavDDraft? CollectDraft()
    {
        SaveControlValuesIntoDraft();
        return _draft;
    }

    private void SaveControlValuesIntoDraft()
    {
        if (_updating || _draft is null)
        {
            return;
        }
        if (_draft.Route is { } route)
        {
            var policyIndex = Math.Clamp(_traversal.Selected, 0, TraversalPolicies.Length - 1);
            var points = route.Points.ToArray();
            if (_selectedPointId is { } pointId)
            {
                var index = Array.FindIndex(points, item => item.Id == pointId);
                if (index >= 0)
                {
                    points[index] = points[index] with
                    {
                        Position = new Vector3Value((float)_pointX.Value, (float)_pointY.Value,
                            (float)_pointZ.Value),
                        WaitSeconds = _pointWait.Value,
                        FacingDegrees = _pointFacing.Value,
                        Speed = _pointSpeed.Value,
                        Weight = _pointWeight.Value,
                        Action = _pointAction.Text.Trim(),
                        BranchCondition = _pointBranch.Text.Trim(),
                    };
                }
            }
            _routeDraft = route with
            {
                Traversal = TraversalPolicies[policyIndex],
                ScriptHandler = _scriptHandler.Text.Trim(),
                Points = points,
            };
        }
        if (_draft.Arrival is { } arrival)
        {
            var facingIndex = Math.Clamp(_arrivalFacing.Selected, 0, FacingModes.Length - 1);
            var facing = FacingModes[facingIndex];
            var target = facing == ArrivalFacingMode.Target && _arrivalTarget.Selected > 0 &&
                         _arrivalTarget.Selected <= _targetChoices.Count
                ? _targetChoices[_arrivalTarget.Selected - 1]
                : null;
            _arrivalDraft = arrival with
            {
                HasArrival = _arrivalEnabled.ButtonPressed,
                Facing = facing,
                DirectionDegrees = _arrivalDirection.Value,
                TargetId = facing == ArrivalFacingMode.Target
                    ? target?.Value ?? string.Empty
                    : arrival.TargetId,
                TargetIsProtected = facing == ArrivalFacingMode.Target
                    ? target?.IsProtected ?? false
                    : arrival.TargetIsProtected,
                Animation = _arrivalAnimation.Text.Trim(),
                WaitSeconds = _arrivalWait.Value,
                Interaction = _arrivalInteraction.Text.Trim(),
                Callback = _arrivalCallback.Text.Trim(),
            };
        }
        _draft = _draft with { Route = _routeDraft, Arrival = _arrivalDraft };
    }

    private void Preview()
    {
        var draft = CollectDraft();
        if (draft is null)
        {
            SetStatus("Choisissez explicitement une route ou un repère avant l’aperçu.", true);
            return;
        }
        _lastPreview = _session.PreviewPatrolUx2NavD(draft);
        _canonicalPreviewReady = _lastPreview.Succeeded;
        RenderPreview(_lastPreview);
        if (_lastPreview.Succeeded)
        {
            _worldViewport.ShowNavigationOverlayUx2NavB(_lastPreview.Overlay);
        }
        else
        {
            _worldViewport.ClearNavigationOverlayUx2NavB();
        }
        _apply.Disabled = !_lastPreview.CanApply;
        SetStatus(_lastPreview.Message, !_lastPreview.Succeeded);
        UpdateActionState();
    }

    private void Apply()
    {
        var areaId = _draft?.AreaId;
        var routeId = _draft?.Route?.RouteId;
        var waypointId = _draft?.Arrival?.WaypointId;
        var result = _session.ApplyApprovedPatrolUx2NavD();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            _canonicalPreviewReady = false;
            return;
        }
        NavigationChanged?.Invoke();
        if (areaId is not null)
        {
            RefreshAfterMutation(areaId, routeId, waypointId);
        }
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Succeeded ? "Modification annulée." : result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            NavigationChanged?.Invoke();
            RefreshCurrentAreaAfterHistory();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Succeeded ? "Modification rétablie." : result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            NavigationChanged?.Invoke();
            RefreshCurrentAreaAfterHistory();
        }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Succeeded ? "Module sauvegardé avec ses patrouilles et arrivées." :
            result.Message, !result.Succeeded);
        UpdateActionState();
    }

    private void RefreshAfterMutation(Forge.Domain.Areas.AreaId areaId, Guid? routeId,
        Guid? waypointId)
    {
        var areaIndex = _areaChoices.ToList().FindIndex(item => item.AreaId == areaId);
        PopulateAreas();
        if (areaIndex < 0)
        {
            ClearSelection();
            return;
        }
        _area.Selected = areaIndex + 1;
        SelectArea(areaIndex + 1);
        if (routeId is { } selectedRoute)
        {
            var routeIndex = _routeChoices.ToList().FindIndex(item => item.RouteId == selectedRoute);
            if (routeIndex >= 0)
            {
                _route.Selected = routeIndex + 1;
                SelectRoute(routeIndex + 1);
            }
        }
        if (waypointId is { } selectedWaypoint)
        {
            var waypointIndex = _waypointChoices.ToList().FindIndex(item =>
                item.WaypointId == selectedWaypoint);
            if (waypointIndex >= 0)
            {
                _waypoint.Selected = waypointIndex + 1;
                SelectWaypoint(waypointIndex + 1);
            }
        }
    }

    private void RefreshCurrentAreaAfterHistory()
    {
        var areaId = _draft?.AreaId;
        var routeId = _draft?.Route?.RouteId;
        var waypointId = _draft?.Arrival?.WaypointId;
        if (areaId is not null)
        {
            RefreshAfterMutation(areaId, routeId, waypointId);
        }
        else
        {
            PopulateAreas();
            ClearSelection();
        }
    }

    private void ClearSelection()
    {
        _updating = true;
        _route.Clear();
        _route.AddItem("Choisir une route…");
        _route.Selected = 0;
        _waypoint.Clear();
        _waypoint.AddItem("Choisir un repère d’arrivée…");
        _waypoint.Selected = 0;
        _point.Clear();
        _point.AddItem("Choisir un point…");
        _point.Selected = 0;
        _routeChoices = [];
        _waypointChoices = [];
        _targetChoices = [];
        _routeDraft = null;
        _arrivalDraft = null;
        _draft = null;
        _lastPreview = null;
        _selectedPointId = null;
        _explicitSelection = false;
        _canonicalPreviewReady = false;
        _updating = false;
        _selection.Text =
            "Choisissez explicitement une zone puis une route ou un repère ; aucune fiche n’est chargée automatiquement.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        SetEditorEnabled(false);
        RenderPreview(null);
        UpdateActionState();
    }

    private void DraftChanged()
    {
        if (_updating || _draft is null)
        {
            return;
        }
        SaveControlValuesIntoDraft();
        UpdateConditionalFields();
        InvalidatePreview("Brouillon modifié · relancez l’aperçu sec.");
        UpdateActionState();
    }

    private void InvalidatePreview(string message)
    {
        _session.CancelPatrolUx2NavD();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _apply.Disabled = true;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _summary.Text = message;
        _authority.Text = "Autorité : aucune action d’arrivée n’est exécutée avant validation.";
        ClearChildren(_diff);
        ClearChildren(_diagnostics);
        ClearChildren(_runtime);
    }

    private void RenderPreview(StudioUx2NavDPreview? preview)
    {
        ClearChildren(_diff);
        ClearChildren(_diagnostics);
        ClearChildren(_runtime);
        if (preview is null)
        {
            _summary.Text = "Aucun aperçu calculé.";
            _authority.Text = "Autorité : aucune action d’arrivée n’est exécutée par cette surface.";
            return;
        }
        foreach (var item in preview.Diff)
        {
            _diff.AddChild(StudioStyle.Label($"• {item}", 9, StudioStyle.Text, wrap: true));
        }
        if (preview.Diff.Count == 0)
        {
            _diff.AddChild(StudioStyle.Label("Aucune différence.", 9, StudioStyle.Muted));
        }
        foreach (var item in preview.Diagnostics)
        {
            _diagnostics.AddChild(StudioStyle.Label(
                $"{item.Code} · {item.Path}\n{item.Message}", 9, StudioStyle.Danger, wrap: true));
        }
        if (preview.Diagnostics.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 9,
                StudioStyle.Success));
        }
        foreach (var step in preview.RuntimeSteps.Take(12))
        {
            _runtime.AddChild(StudioStyle.Label(
                $"{step.FromOrder} → {step.ToOrder} · " +
                $"{(step.Direction < 0 ? "retour" : "aller")} · {step.Distance:0.##} m · " +
                $"{step.TravelSeconds:0.##} s · action {step.Action} · branche {step.BranchCondition}",
                9, StudioStyle.Text, wrap: true));
        }
        if (preview.RuntimeSteps.Count > 12)
        {
            _runtime.AddChild(StudioStyle.Label(
                $"+ {preview.RuntimeSteps.Count - 12} étapes disponibles dans l’inventaire complet.",
                9, StudioStyle.Muted));
        }
        if (preview.RuntimeSteps.Count == 0)
        {
            _runtime.AddChild(StudioStyle.Label("Aucune route sélectionnée.", 9, StudioStyle.Muted));
        }
        _summary.Text = preview.Summary;
        _summary.AddThemeColorOverride("font_color", preview.Succeeded ? StudioStyle.Text :
            StudioStyle.Danger);
        _authority.Text = preview.ArrivalAuthorized && preview.ArrivalDeniedWithoutActions
            ? "Arrivée : résolution autorisée disponible ; sans autorité, animation, attente, interaction et callback restent bloqués. Simulation pure."
            : "Arrivée : aucune action exécutable dans cet aperçu ; aucun état runtime n’est persisté.";
        _authority.AddThemeColorOverride("font_color", StudioStyle.Accent);
    }

    private void WireDraftSignals()
    {
        _traversal.ItemSelected += _ => DraftChanged();
        _arrivalFacing.ItemSelected += _ => DraftChanged();
        _arrivalTarget.ItemSelected += _ => DraftChanged();
        foreach (var line in new[]
                 {
                     _scriptHandler, _pointAction, _pointBranch, _arrivalAnimation,
                     _arrivalInteraction, _arrivalCallback,
                 })
        {
            line.TextChanged += _ => DraftChanged();
        }
        foreach (var spin in new[]
                 {
                     _pointX, _pointY, _pointZ, _pointWait, _pointFacing, _pointSpeed,
                     _pointWeight, _arrivalDirection, _arrivalWait,
                 })
        {
            spin.ValueChanged += _ => DraftChanged();
        }
        _arrivalEnabled.Toggled += enabled =>
        {
            _arrivalFields.Visible = enabled;
            DraftChanged();
        };
    }

    private void UpdateConditionalFields()
    {
        var scripted = _traversal.Selected >= 0 &&
                       TraversalPolicies[Math.Clamp(_traversal.Selected, 0,
                           TraversalPolicies.Length - 1)] == PatrolTraversalPolicy.Scripted;
        _scriptHandler.Editable = scripted;
        var arrivalEnabled = _arrivalDraft?.HasArrival == true || _arrivalEnabled.ButtonPressed;
        _arrivalFields.Visible = _arrivalDraft is not null && arrivalEnabled;
        var targetFacing = _arrivalFacing.Selected >= 0 &&
                           FacingModes[Math.Clamp(_arrivalFacing.Selected, 0,
                               FacingModes.Length - 1)] == ArrivalFacingMode.Target;
        _targetField.Visible = targetFacing;
        _arrivalDirection.Editable = _arrivalFacing.Selected >= 0 &&
                                     FacingModes[Math.Clamp(_arrivalFacing.Selected, 0,
                                         FacingModes.Length - 1)] == ArrivalFacingMode.Direction;
    }

    private void SetEditorEnabled(bool enabled)
    {
        _editor.Visible = enabled;
        _preview.Disabled = !enabled;
    }

    private void UpdateActionState()
    {
        _newRoute.Disabled = _area.Selected <= 0 || _area.Selected > _areaChoices.Count;
        _preview.Disabled = _draft is null;
        _apply.Disabled = _lastPreview?.CanApply != true;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null || !_session.IsDirty;
        var pointIndex = _routeDraft is null || _selectedPointId is null
            ? -1
            : _routeDraft.Points.ToList().FindIndex(item => item.Id == _selectedPointId.Value);
        _addPoint.Disabled = _routeDraft is null;
        _removePoint.Disabled = _routeDraft is null || pointIndex < 0 ||
                                _routeDraft.Points.Count <= 2;
        _movePointUp.Disabled = pointIndex <= 0;
        _movePointDown.Disabled = _routeDraft is null || pointIndex < 0 ||
                                  pointIndex >= _routeDraft.Points.Count - 1;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private StudioUx2NavDEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History!;
        return new StudioUx2NavDEvidenceSourceSnapshot(_sourceSession.Project!, history,
            history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureNavigationApprovalUx2NavB(),
            _sourceSession.CaptureWaypointApprovalUx2NavC(),
            _sourceSession.CapturePatrolApprovalUx2NavD());
    }

    private bool SourceMatchesSnapshot(StudioUx2NavDEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               history?.CanUndo == snapshot.CanUndo && history?.CanRedo == snapshot.CanRedo &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureNavigationApprovalUx2NavB() == snapshot.NavigationApproval &&
               _sourceSession.CaptureWaypointApprovalUx2NavC() == snapshot.WaypointApproval &&
               _sourceSession.CapturePatrolApprovalUx2NavD() == snapshot.PatrolApproval;
    }

    private void ReleaseEvidenceWorkspace()
    {
        if (_evidenceSession is null && _evidenceTempRoot is null)
        {
            _session = _sourceSession;
            _lastSourceRestored = true;
            return;
        }
        try
        {
            _evidenceSession?.Close();
        }
        finally
        {
            _evidenceSession = null;
            var root = _evidenceTempRoot;
            _evidenceTempRoot = null;
            _session = _sourceSession;
            _lastEvidenceResourcesReleased =
                StudioSessionController.DeleteUx2NavDTemporaryWorkspace(root);
            _lastEvidenceSourceUntouched = _evidenceSourceSnapshot is null ||
                                           SourceMatchesSnapshot(_evidenceSourceSnapshot);
            _lastSourceRestored = true;
        }
    }

    private void BindViewportToArea(StudioSessionController session,
        Forge.Domain.Areas.AreaId areaId)
    {
        try
        {
            _worldViewport.RebuildArea(session, areaId, readOnly: true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           IOException)
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_D_VIEWPORT_FALLBACK {exception.Message}");
        }
    }

    private void RestoreSourceViewport()
    {
        try
        {
            if (_sourceSession.ActiveAreaId is { } activeArea)
            {
                _worldViewport.RebuildArea(_sourceSession, activeArea, readOnly: false,
                    frameArea: false);
                _worldViewport.ApplyCameraProfile(_sourceSession.CameraProfile);
            }
            else
            {
                _worldViewport.ShowNoArea(_sourceSession);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           IOException)
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_D_VIEWPORT_RESTORE_FAILED {exception.Message}");
        }
    }

    private void UpdateDialogBounds()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0)
        {
            return;
        }
        var scale = viewport.X >= 3_200 ? 1.5f : viewport.X >= 2_300 ? 1.15f : 1f;
        var width = Math.Clamp((viewport.X * .49f) / scale, 780f, 980f);
        var height = Math.Clamp((viewport.Y - 30f) / scale, 700f, 1_180f);
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2(viewport.X - width * scale - 15f,
            Math.Max(15f, (viewport.Y - height * scale) / 2f));
    }

    private static string HumanTraversal(PatrolTraversalPolicy policy) => policy switch
    {
        PatrolTraversalPolicy.Loop => "Boucle",
        PatrolTraversalPolicy.PingPong => "Aller-retour",
        PatrolTraversalPolicy.Once => "Une fois",
        PatrolTraversalPolicy.Random => "Aléatoire",
        PatrolTraversalPolicy.Weighted => "Pondérée",
        PatrolTraversalPolicy.Scripted => "Scriptée",
        _ => "Héritée",
    };

    private static string HumanFacing(ArrivalFacingMode mode) => mode switch
    {
        ArrivalFacingMode.Preserve => "Conserver l’orientation",
        ArrivalFacingMode.Direction => "Faire face à une direction",
        ArrivalFacingMode.Target => "Faire face à une cible",
        _ => "Héritée",
    };

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.Free();
        }
    }

    private static IEnumerable<Label> DescendantLabelsUx2NavD(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label)
            {
                yield return label;
            }
            foreach (var descendant in DescendantLabelsUx2NavD(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool ContainsUuidUx2NavD(string text) =>
        System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
