using Forge.Domain.Areas;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioVehicleRouteOverlayUx2NavL : Control
{
    private static readonly string[] DirectionsUx2NavL = ["forward", "reverse", "bidirectional"];
    private static readonly string[] SyncModesUx2NavL = ["server-snapshots", "bounded-prediction", "local"];

    private readonly StudioSessionController _sourceSession;
    private readonly StudioWorldViewport _worldViewport;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceTempRoot;
    private StudioUx2NavLEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _lastSourceRestored = true;
    private bool _lastUiTransaction;
    private bool _updating;
    private bool _explicitAreaSelection;
    private bool _explicitRouteSelection;
    private int _selectedPointIndex = -1;
    private int _selectedLaneIndex = -1;
    private int _selectedStopIndex = -1;
    private StudioUx2NavLDraft? _draft;
    private StudioUx2NavLPreview? _lastPreview;
    private IReadOnlyList<StudioUx2NavLAreaChoice> _areaChoices = [];
    private IReadOnlyList<StudioUx2NavLRouteChoice> _routeChoices = [];

    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly OptionButton _route;
    private readonly Button _addRoute;
    private readonly Button _removeRoute;
    private readonly Label _selection;
    private readonly SpinBox _defaultSpeed;
    private readonly CheckBox _overtaking;
    private readonly OptionButton _sync;
    private readonly OptionButton _point;
    private readonly Button _addPoint;
    private readonly Button _removePoint;
    private readonly Button _movePointUp;
    private readonly Button _movePointDown;
    private readonly SpinBox _pointX;
    private readonly SpinBox _pointY;
    private readonly SpinBox _pointZ;
    private readonly SpinBox _tangent;
    private readonly SpinBox _width;
    private readonly OptionButton _lane;
    private readonly Button _addLane;
    private readonly Button _removeLane;
    private readonly Button _moveLaneUp;
    private readonly Button _moveLaneDown;
    private readonly LineEdit _laneId;
    private readonly SpinBox _laneOffset;
    private readonly SpinBox _laneSpeed;
    private readonly OptionButton _laneDirection;
    private readonly OptionButton _stop;
    private readonly Button _addStop;
    private readonly Button _removeStop;
    private readonly Button _moveStopUp;
    private readonly Button _moveStopDown;
    private readonly OptionButton _stopPoint;
    private readonly SpinBox _wait;
    private readonly LineEdit _service;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly VBoxContainer _runtime;
    private readonly Label _summary;
    private readonly Label _boundary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? VehicleRouteChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _areaChoices.Count;
    internal bool HasExplicitSelection => _explicitAreaSelection && _explicitRouteSelection &&
                                          _selectedPointIndex >= 0 && _selectedLaneIndex >= 0 &&
                                          (_draft?.Stops.Count == 0 || _selectedStopIndex >= 0);
    internal bool CanonicalPreviewReady => _lastPreview?.Succeeded == true;
    internal bool ShowsViewportOverlay => _lastPreview is { Overlay.Markers.Count: > 1,
                                                Overlay.Segments.Count: > 0 } &&
                                          _worldViewport.NavigationOverlayPrimitiveCountUx2NavB > 0;
    internal bool ShowsStructuredDiagnostics => _lastPreview is { Succeeded: true } &&
                                                _lastPreview.Diagnostics.Count == 0;
    internal bool ShowsBoundedRuntimeClaim => _lastPreview?.Runtime is { Boundary: var boundary } &&
                                              boundary.Contains("position, vitesse et arrêt",
                                                  StringComparison.OrdinalIgnoreCase) &&
                                              boundary.Contains("non exécutés", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHumanSurface => _selection.Text.Contains("sélectionnée",
                                           StringComparison.OrdinalIgnoreCase) &&
                                       !_selection.Text.Contains("0000-", StringComparison.Ordinal);
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                          !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => _evidenceSourceSnapshot is null
        ? _lastEvidenceSourceUntouched
        : SourceSnapshotMatches(_evidenceSourceSnapshot);
    internal bool EvidenceResourcesReleased => _lastEvidenceResourcesReleased;
    internal bool SourceSessionRestored => _lastSourceRestored;
    internal bool LastUiTransaction => _lastUiTransaction;

    internal StudioVehicleRouteOverlayUx2NavL(StudioSessionController session,
        StudioWorldViewport worldViewport)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = session;
        _worldViewport = worldViewport ?? throw new ArgumentNullException(nameof(worldViewport));
        Name = "StudioVehicleRouteOverlayUx2NavL";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        var veil = new ColorRect { Color = new Color(.012f, .022f, .032f, .74f) };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);
        _dialog = StudioStyle.Panel("VehicleRouteDialogUx2NavL", StudioStyle.Surface, 12, 1);
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

        var header = RowUx2NavL();
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("NAVIGATION · ROUTES DE VÉHICULES", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Points, voies, arrêts et politique de synchronisation", 19));
        heading.AddChild(StudioStyle.Label(
            "ORF-WPT-012 · édite uniquement VehicleRoutes. Les profils WorldFeatures voisins et les lots " +
            "Navigation B–K restent protégés.", 9, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var selectors = RowUx2NavL();
        _area = StudioUx2NavCControls.Options();
        _area.ItemSelected += SelectArea;
        selectors.AddChild(StudioUx2NavCControls.Field("Zone", _area, 220));
        _route = StudioUx2NavCControls.Options();
        _route.ItemSelected += SelectRoute;
        selectors.AddChild(StudioUx2NavCControls.Field("Route", _route, 270));
        _addRoute = StudioStyle.Button("Nouvelle", compact: true);
        _addRoute.Pressed += AddRoute;
        selectors.AddChild(_addRoute);
        _removeRoute = StudioStyle.Button("Retirer", compact: true);
        _removeRoute.Pressed += RemoveRoute;
        selectors.AddChild(_removeRoute);
        root.AddChild(selectors);

        _selection = StudioStyle.Label(
            "Choisissez explicitement une zone puis une route ; aucune route n’est chargée automatiquement.",
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
        var editor = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        editor.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(editor);

        var policy = RowUx2NavL();
        _defaultSpeed = NumberUx2NavL(.01, 1_000_000, .25);
        policy.AddChild(StudioUx2NavCControls.Field("Vitesse globale", _defaultSpeed, 150));
        _overtaking = StudioUx2NavCControls.Check("Autoriser le dépassement");
        policy.AddChild(_overtaking);
        _sync = StudioUx2NavCControls.Options("Snapshots serveur", "Prédiction bornée", "Locale");
        _sync.ItemSelected += _ => DraftChanged();
        policy.AddChild(StudioUx2NavCControls.Field("Synchronisation persistée", _sync, 220));
        editor.AddChild(StudioUx2NavCControls.Card("VehiclePolicyUx2NavL", "Politique de route",
            "La fiche persiste ces choix ; elle ne simule ni trafic, ni conduite, ni réplication.", policy));

        var pointContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var pointSelector = RowUx2NavL();
        _point = StudioUx2NavCControls.Options();
        _point.ItemSelected += SelectPoint;
        pointSelector.AddChild(StudioUx2NavCControls.Field("Point ordonné", _point, 260));
        _addPoint = StudioStyle.Button("Ajouter", compact: true);
        _addPoint.Pressed += AddPoint;
        pointSelector.AddChild(_addPoint);
        _removePoint = StudioStyle.Button("Retirer", compact: true);
        _removePoint.Pressed += RemovePoint;
        pointSelector.AddChild(_removePoint);
        _movePointUp = StudioStyle.Button("Monter", compact: true);
        _movePointUp.Pressed += () => MovePoint(-1);
        pointSelector.AddChild(_movePointUp);
        _movePointDown = StudioStyle.Button("Descendre", compact: true);
        _movePointDown.Pressed += () => MovePoint(1);
        pointSelector.AddChild(_movePointDown);
        pointContent.AddChild(pointSelector);
        var coordinates = RowUx2NavL();
        _pointX = NumberUx2NavL(-1_000_000, 1_000_000, .25);
        _pointY = NumberUx2NavL(-1_000_000, 1_000_000, .25);
        _pointZ = NumberUx2NavL(-1_000_000, 1_000_000, .25);
        coordinates.AddChild(StudioUx2NavCControls.Field("X", _pointX));
        coordinates.AddChild(StudioUx2NavCControls.Field("Y", _pointY));
        coordinates.AddChild(StudioUx2NavCControls.Field("Z", _pointZ));
        pointContent.AddChild(coordinates);
        var pointShape = RowUx2NavL();
        _tangent = NumberUx2NavL(-360_000, 360_000, .5);
        _width = NumberUx2NavL(.01, 1_000_000, .25);
        pointShape.AddChild(StudioUx2NavCControls.Field("Tangente (°)", _tangent));
        pointShape.AddChild(StudioUx2NavCControls.Field("Largeur", _width));
        pointContent.AddChild(pointShape);
        editor.AddChild(StudioUx2NavCControls.Card("VehiclePointsUx2NavL", "Points de spline",
            "L’ordre définit l’interpolation linéaire canonique actuelle ; tangente et largeur restent authoring-only.",
            pointContent));

        var laneContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var laneSelector = RowUx2NavL();
        _lane = StudioUx2NavCControls.Options();
        _lane.ItemSelected += SelectLane;
        laneSelector.AddChild(StudioUx2NavCControls.Field("Voie ordonnée", _lane, 240));
        _addLane = StudioStyle.Button("Ajouter", compact: true);
        _addLane.Pressed += AddLane;
        laneSelector.AddChild(_addLane);
        _removeLane = StudioStyle.Button("Retirer", compact: true);
        _removeLane.Pressed += RemoveLane;
        laneSelector.AddChild(_removeLane);
        _moveLaneUp = StudioStyle.Button("Monter", compact: true);
        _moveLaneUp.Pressed += () => MoveLane(-1);
        laneSelector.AddChild(_moveLaneUp);
        _moveLaneDown = StudioStyle.Button("Descendre", compact: true);
        _moveLaneDown.Pressed += () => MoveLane(1);
        laneSelector.AddChild(_moveLaneDown);
        laneContent.AddChild(laneSelector);
        var laneFields = RowUx2NavL();
        _laneId = StudioUx2NavCControls.Text("ex. service-nord");
        laneFields.AddChild(StudioUx2NavCControls.Field("Clé humaine", _laneId, 210));
        _laneOffset = NumberUx2NavL(-1_000_000, 1_000_000, .25);
        laneFields.AddChild(StudioUx2NavCControls.Field("Offset latéral", _laneOffset, 140));
        _laneSpeed = NumberUx2NavL(.01, 1_000_000, .25);
        laneFields.AddChild(StudioUx2NavCControls.Field("Vitesse", _laneSpeed, 120));
        _laneDirection = StudioUx2NavCControls.Options();
        _laneDirection.ItemSelected += _ => DraftChanged();
        laneFields.AddChild(StudioUx2NavCControls.Field("Sens", _laneDirection, 170));
        laneContent.AddChild(laneFields);
        editor.AddChild(StudioUx2NavCControls.Card("VehicleLanesUx2NavL", "Voies",
            "Offsets et sens sont persistés ; SampleVehicleRoute utilise seulement la vitesse minimale.",
            laneContent));

        var stopContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var stopSelector = RowUx2NavL();
        _stop = StudioUx2NavCControls.Options();
        _stop.ItemSelected += SelectStop;
        stopSelector.AddChild(StudioUx2NavCControls.Field("Arrêt ordonné", _stop, 240));
        _addStop = StudioStyle.Button("Ajouter", compact: true);
        _addStop.Pressed += AddStop;
        stopSelector.AddChild(_addStop);
        _removeStop = StudioStyle.Button("Retirer", compact: true);
        _removeStop.Pressed += RemoveStop;
        stopSelector.AddChild(_removeStop);
        _moveStopUp = StudioStyle.Button("Monter", compact: true);
        _moveStopUp.Pressed += () => MoveStop(-1);
        stopSelector.AddChild(_moveStopUp);
        _moveStopDown = StudioStyle.Button("Descendre", compact: true);
        _moveStopDown.Pressed += () => MoveStop(1);
        stopSelector.AddChild(_moveStopDown);
        stopContent.AddChild(stopSelector);
        var stopFields = RowUx2NavL();
        _stopPoint = StudioUx2NavCControls.Options();
        _stopPoint.ItemSelected += _ => DraftChanged();
        stopFields.AddChild(StudioUx2NavCControls.Field("Point desservi", _stopPoint, 200));
        _wait = NumberUx2NavL(0, 1_000_000, .25);
        stopFields.AddChild(StudioUx2NavCControls.Field("Attente (s)", _wait, 120));
        _service = StudioUx2NavCControls.Text("ex. halte-marche");
        stopFields.AddChild(StudioUx2NavCControls.Field("Service", _service, 220));
        stopContent.AddChild(stopFields);
        editor.AddChild(StudioUx2NavCControls.Card("VehicleStopsUx2NavL", "Arrêts",
            "Chaque arrêt cible explicitement un point de la route ; retirer ce point rend l’aperçu invalide.",
            stopContent));

        var columns = RowUx2NavL();
        _diff = PreviewColumnUx2NavL(columns, "DIFF SÉMANTIQUE");
        _diagnostics = PreviewColumnUx2NavL(columns, "DIAGNOSTICS");
        _runtime = PreviewColumnUx2NavL(columns, "SAMPLE RUNTIME BORNÉ");
        var previewContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        previewContent.AddChild(columns);
        _summary = StudioStyle.Label("Aucun aperçu calculé.", 10, StudioStyle.Text, wrap: true);
        previewContent.AddChild(_summary);
        _boundary = StudioStyle.Label(
            "Limite : position, vitesse et arrêt seulement ; tangentes, largeur, offsets, sens, dépassement et " +
            "sync restent persistés sans exécution.", 9, StudioStyle.Warning, wrap: true);
        previewContent.AddChild(_boundary);
        editor.AddChild(StudioUx2NavCControls.Card("VehiclePreviewUx2NavL", "Aperçu avant application",
            "Le viewport B montre les points et segments de la route sélectionnée.", previewContent));

        var footer = RowUx2NavL();
        _status = StudioStyle.Label("Prêt · aucune mutation.", 9, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(_status);
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += Redo;
        footer.AddChild(_redo);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        _save.Pressed += Save;
        footer.AddChild(_save);
        root.AddChild(footer);

        foreach (var number in new[] { _defaultSpeed, _pointX, _pointY, _pointZ, _tangent, _width,
                     _laneOffset, _laneSpeed, _wait })
            number.ValueChanged += _ => DraftChanged();
        _overtaking.Toggled += _ => DraftChanged();
        _laneId.TextChanged += _ => DraftChanged();
        _service.TextChanged += _ => DraftChanged();
        Resized += UpdateDialogBounds;
        TreeExiting += Close;
        ClearSelection();
    }

    internal void Open()
    {
        ReleaseEvidenceWorkspace();
        _session = _sourceSession;
        _sourceSession.CancelVehicleRouteApprovalUx2NavL();
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;
        PopulateAreas();
        ClearSelection();
        UpdateDialogBounds();
        SetStatus("Choisissez explicitement une zone puis une route.");
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (_evidenceSession is not null) _evidenceSession.CancelVehicleRouteApprovalUx2NavL();
        else _sourceSession.CancelVehicleRouteApprovalUx2NavL();
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _draft = null;
        _lastPreview = null;
        _explicitAreaSelection = false;
        _explicitRouteSelection = false;
        _selectedPointIndex = -1;
        _selectedLaneIndex = -1;
        _selectedStopIndex = -1;
        ReleaseEvidenceWorkspace();
        RestoreSourceViewport();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!(mode.StartsWith("ux2-nav-l", StringComparison.OrdinalIgnoreCase) ||
              mode.StartsWith("ux2-wpt-012", StringComparison.OrdinalIgnoreCase)) ||
            _sourceSession.Project is null || _sourceSession.History is null) return false;
        ReleaseEvidenceWorkspace();
        _evidenceSourceSnapshot = CaptureSourceSnapshot();
        _lastEvidenceSourceUntouched = true;
        _lastEvidenceResourcesReleased = false;
        _lastSourceRestored = false;
        var project = _sourceSession.CreateUx2NavLEvidenceProject();
        if (project is null) return FailEvidencePreparation(null);
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-l-evidence-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (!ProjectSession.Create(tempRoot, project).Succeeded)
                return FailEvidencePreparation(tempRoot);
            _evidenceTempRoot = tempRoot;
            var evidence = new StudioSessionController();
            if (!evidence.Open(tempRoot).Succeeded)
            {
                evidence.Close();
                return FailEvidencePreparation(tempRoot);
            }
            _evidenceSession = evidence;
            _session = evidence;
            Visible = true;
            MouseFilter = MouseFilterEnum.Stop;
            PopulateAreas();
            var areaIndex = _areaChoices.ToList().FindIndex(item => item.IsEvidence);
            if (areaIndex < 0) { Close(); return false; }
            _area.Selected = areaIndex + 1;
            SelectArea(areaIndex + 1);
            var routeIndex = _routeChoices.ToList().FindIndex(item => item.IsEvidence);
            if (routeIndex < 0) { Close(); return false; }
            _route.Selected = routeIndex + 1;
            SelectRoute(routeIndex + 1);
            if (_draft is not { Points.Count: > 0, Lanes.Count: > 0, Stops.Count: > 0 })
            {
                Close();
                return false;
            }
            _point.Selected = 1;
            SelectPoint(1);
            _lane.Selected = 1;
            SelectLane(1);
            _stop.Selected = 1;
            SelectStop(1);
            _defaultSpeed.Value += 1;
            DraftChanged();
            Preview();
            UpdateDialogBounds();
            return HasExplicitSelection && CanonicalPreviewReady && ShowsViewportOverlay &&
                   ShowsStructuredDiagnostics && ShowsBoundedRuntimeClaim && ShowsHumanSurface &&
                   UsesIsolatedEvidence && EvidenceSourceUntouched;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_NAV_L_EVIDENCE_FAILED " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            return FailEvidencePreparation(tempRoot);
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        _lastUiTransaction = false;
        try
        {
            if (!PrepareEvidence("ux2-nav-l")) { Close(); return false; }
            var pointsBefore = _draft!.Points.Count;
            _addPoint.EmitSignal(Button.SignalName.Pressed);
            var pointAdded = _draft?.Points.Count == pointsBefore + 1;
            _removePoint.EmitSignal(Button.SignalName.Pressed);
            var pointRemoved = _draft?.Points.Count == pointsBefore;
            var lanesBefore = _draft!.Lanes.Count;
            _addLane.EmitSignal(Button.SignalName.Pressed);
            var laneAdded = _draft?.Lanes.Count == lanesBefore + 1;
            _removeLane.EmitSignal(Button.SignalName.Pressed);
            var laneRemoved = _draft?.Lanes.Count == lanesBefore;
            var stopsBefore = _draft!.Stops.Count;
            _addStop.EmitSignal(Button.SignalName.Pressed);
            var stopAdded = _draft?.Stops.Count == stopsBefore + 1;
            _removeStop.EmitSignal(Button.SignalName.Pressed);
            var stopRemoved = _draft?.Stops.Count == stopsBefore;
            _point.Selected = 1;
            SelectPoint(1);
            _lane.Selected = 1;
            SelectLane(1);
            _stop.Selected = 1;
            SelectStop(1);
            _preview.EmitSignal(Button.SignalName.Pressed);
            var dryRun = CanonicalPreviewReady && !_apply.Disabled;
            var projectBeforeApply = _session.Project;
            _apply.EmitSignal(Button.SignalName.Pressed);
            var appliedProject = _session.Project;
            var applied = appliedProject is not null && !ReferenceEquals(projectBeforeApply, appliedProject);
            _undo.EmitSignal(Button.SignalName.Pressed);
            var undone = ReferenceEquals(projectBeforeApply, _session.Project);
            _redo.EmitSignal(Button.SignalName.Pressed);
            var redone = ReferenceEquals(appliedProject, _session.Project);
            _save.EmitSignal(Button.SignalName.Pressed);
            var saved = !_session.IsDirty;
            _lastUiTransaction = pointAdded && pointRemoved && laneAdded && laneRemoved && stopAdded &&
                                 stopRemoved && dryRun && applied && undone && redone && saved;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_NAV_L_UI_TRANSACTION_FAILED " +
                        $"type={exception.GetType().Name} message={exception.Message}");
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
        var approval = _sourceSession.CaptureVehicleRouteApprovalUx2NavL();
        Open();
        _sourceSession.RestoreVehicleRouteApprovalUx2NavL(approval);
    }

    private void PopulateAreas()
    {
        _updating = true;
        _areaChoices = _session.VehicleRouteAreaChoicesUx2NavL;
        _area.Clear();
        _area.AddItem("Choisir une zone…");
        foreach (var choice in _areaChoices) _area.AddItem(choice.Label);
        _area.Selected = 0;
        _updating = false;
    }

    private void ClearSelection()
    {
        _updating = true;
        _route.Clear();
        _route.AddItem("Choisir une route…");
        _updating = false;
        _draft = null;
        _lastPreview = null;
        _routeChoices = [];
        _explicitAreaSelection = false;
        _explicitRouteSelection = false;
        _selectedPointIndex = -1;
        _selectedLaneIndex = -1;
        _selectedStopIndex = -1;
        _selection.Text =
            "Choisissez explicitement une zone puis une route ; aucune route n’est chargée automatiquement.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        ClearRouteFields();
        UpdateActionState();
    }

    private void SelectArea(long index)
    {
        if (_updating) return;
        _session.CancelVehicleRouteApprovalUx2NavL();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _draft = null;
        _lastPreview = null;
        _explicitRouteSelection = false;
        _explicitAreaSelection = index > 0 && index <= _areaChoices.Count;
        _updating = true;
        _route.Clear();
        _route.AddItem("Choisir une route…");
        _routeChoices = _explicitAreaSelection
            ? _session.VehicleRouteChoicesUx2NavL(_areaChoices[(int)index - 1].AreaId) : [];
        foreach (var choice in _routeChoices) _route.AddItem(choice.Label);
        _route.Selected = 0;
        _updating = false;
        ClearRouteFields();
        if (_explicitAreaSelection)
        {
            var choice = _areaChoices[(int)index - 1];
            _selection.Text = $"Zone « {choice.Label} » sélectionnée · choisissez une route ou créez-en une.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
            BindViewportToArea(_session, choice.AreaId);
        }
        else
        {
            _selection.Text = "Aucune zone sélectionnée.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        }
        UpdateActionState();
    }

    private void SelectRoute(long index)
    {
        if (_updating || !_explicitAreaSelection) return;
        _session.CancelVehicleRouteApprovalUx2NavL();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _lastPreview = null;
        _explicitRouteSelection = index > 0 && index <= _routeChoices.Count;
        if (!_explicitRouteSelection)
        {
            ClearRouteFields();
            UpdateActionState();
            return;
        }
        var areaChoice = _areaChoices[_area.Selected - 1];
        var routeChoice = _routeChoices[(int)index - 1];
        if (!routeChoice.IsEditable)
        {
            _explicitRouteSelection = false;
            ClearRouteFields();
            SetStatus(routeChoice.ReadOnlyReason ?? "Cette route ne peut pas être chargée sans perte.", true);
            return;
        }
        _draft = _session.CreateVehicleRouteDraftUx2NavL(areaChoice.AreaId, routeChoice.RouteId);
        if (_draft is null)
        {
            _explicitRouteSelection = false;
            ClearRouteFields();
            SetStatus("La route choisie n’est plus disponible.", true);
            return;
        }
        LoadDraft();
        _selection.Text = $"« {routeChoice.Label} » sélectionnée dans « {areaChoice.Label} » · " +
                          "choisissez explicitement point, voie et arrêt.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
        SetStatus("Route chargée sans mutation · aperçu sec obligatoire.");
    }

    private void AddRoute()
    {
        if (!_explicitAreaSelection) { SetStatus("Choisissez d’abord une zone.", true); return; }
        var areaChoice = _areaChoices[_area.Selected - 1];
        var draft = _session.CreateNewVehicleRouteDraftUx2NavL(areaChoice.AreaId);
        if (draft is null) { SetStatus("La route neuve n’a pas pu être préparée.", true); return; }
        _session.CancelVehicleRouteApprovalUx2NavL();
        _draft = draft;
        _explicitRouteSelection = true;
        _lastPreview = null;
        _updating = true;
        _route.Clear();
        _route.AddItem("Choisir une route…");
        _route.AddItem("Nouvelle route · non appliquée");
        _route.Selected = 1;
        _updating = false;
        LoadDraft();
        _selection.Text = $"Nouvelle route sélectionnée dans « {areaChoice.Label} » · brouillon local.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
        SetStatus("Nouvelle route prête dans le brouillon · sélectionnez ses composants.");
    }

    private void RemoveRoute()
    {
        if (_draft is null) return;
        _session.CancelVehicleRouteApprovalUx2NavL();
        if (_draft.IsNew)
        {
            SelectArea(_area.Selected);
            SetStatus("Brouillon neuf abandonné · aucun document modifié.");
            return;
        }
        CommitFields();
        _draft = _draft with { Remove = true };
        _lastPreview = null;
        _selection.Text = "Route sélectionnée pour retrait · prévisualisez avant application.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        SetEditorEnabled(false);
        RenderPreview(null);
        UpdateActionState();
    }

    private void LoadDraft()
    {
        if (_draft is null) { ClearRouteFields(); return; }
        _updating = true;
        _defaultSpeed.Value = _draft.DefaultSpeedLimit;
        _overtaking.ButtonPressed = _draft.AllowOvertaking;
        _sync.Selected = Math.Max(0, Array.IndexOf(SyncModesUx2NavL, _draft.NetworkSyncMode));
        PopulatePoints();
        PopulateLanes();
        PopulateStops();
        _point.Selected = 0;
        _lane.Selected = 0;
        _stop.Selected = 0;
        _selectedPointIndex = -1;
        _selectedLaneIndex = -1;
        _selectedStopIndex = -1;
        _updating = false;
        SetEditorEnabled(true);
        ClearPointFields();
        ClearLaneFields();
        ClearStopFields();
        RenderPreview(null);
        UpdateActionState();
    }

    private void PopulatePoints()
    {
        _point.Clear();
        _point.AddItem("Choisir un point…");
        _stopPoint.Clear();
        if (_draft is null) return;
        for (var index = 0; index < _draft.Points.Count; index++)
        {
            var item = _draft.Points[index];
            var label = $"Point {index + 1} · ({item.PositionX:0.##}, {item.PositionY:0.##}, {item.PositionZ:0.##})";
            _point.AddItem(label);
            _stopPoint.AddItem($"Point {index + 1}");
        }
    }

    private void PopulateLanes()
    {
        _lane.Clear();
        _lane.AddItem("Choisir une voie…");
        if (_draft is null) return;
        for (var index = 0; index < _draft.Lanes.Count; index++)
        {
            var item = _draft.Lanes[index];
            _lane.AddItem($"Voie {index + 1} · " +
                          (item.IdIsProtected ? "clé protégée" : item.Id));
        }
    }

    private void PopulateStops()
    {
        _stop.Clear();
        _stop.AddItem("Choisir un arrêt…");
        if (_draft is null) return;
        for (var index = 0; index < _draft.Stops.Count; index++)
        {
            var item = _draft.Stops[index];
            _stop.AddItem($"Arrêt {index + 1} · " +
                          (item.ServiceIdIsProtected ? "service protégé" : item.ServiceId));
        }
    }

    private void SelectPoint(long index)
    {
        if (_updating || _draft is null) return;
        CommitFields();
        _selectedPointIndex = index > 0 && index <= _draft.Points.Count ? (int)index - 1 : -1;
        InvalidatePreview("Point sélectionné · prévisualisation à recalculer.");
        if (_selectedPointIndex < 0) ClearPointFields(); else LoadPointFields();
        UpdateActionState();
    }

    private void SelectLane(long index)
    {
        if (_updating || _draft is null) return;
        CommitFields();
        _selectedLaneIndex = index > 0 && index <= _draft.Lanes.Count ? (int)index - 1 : -1;
        InvalidatePreview("Voie sélectionnée · prévisualisation à recalculer.");
        if (_selectedLaneIndex < 0) ClearLaneFields(); else LoadLaneFields();
        UpdateActionState();
    }

    private void SelectStop(long index)
    {
        if (_updating || _draft is null) return;
        CommitFields();
        _selectedStopIndex = index > 0 && index <= _draft.Stops.Count ? (int)index - 1 : -1;
        InvalidatePreview("Arrêt sélectionné · prévisualisation à recalculer.");
        if (_selectedStopIndex < 0) ClearStopFields(); else LoadStopFields();
        UpdateActionState();
    }

    private void AddPoint()
    {
        if (_draft is null) return;
        CommitFields();
        var item = _session.CreateNewVehiclePointUx2NavL(_draft);
        if (item is null) { SetStatus("La limite de 64 points est atteinte.", true); return; }
        _draft = _draft with { Points = _draft.Points.Concat([item]).ToArray() };
        _selectedPointIndex = _draft.Points.Count - 1;
        RefreshComponentSelectors();
        _point.Selected = _selectedPointIndex + 1;
        LoadPointFields();
        InvalidatePreview("Point ajouté au brouillon.");
    }

    private void RemovePoint()
    {
        if (_draft is null || _selectedPointIndex < 0) return;
        if (_draft.Points.Count <= 2)
        {
            SetStatus("Une route doit conserver au moins deux points.", true);
            return;
        }
        CommitFields();
        var removed = _selectedPointIndex;
        _draft = _draft with { Points = _draft.Points.Where((_, index) => index != removed).ToArray() };
        _selectedPointIndex = Math.Min(removed, _draft.Points.Count - 1);
        RefreshComponentSelectors();
        _point.Selected = _selectedPointIndex + 1;
        LoadPointFields();
        InvalidatePreview("Point retiré · les arrêts orphelins seront refusés par l’aperçu.");
    }

    private void MovePoint(int delta)
    {
        if (_draft is null || _selectedPointIndex < 0) return;
        CommitFields();
        var target = _selectedPointIndex + delta;
        if (target < 0 || target >= _draft.Points.Count) return;
        var values = _draft.Points.ToArray();
        (values[_selectedPointIndex], values[target]) = (values[target], values[_selectedPointIndex]);
        _draft = _draft with { Points = values };
        _selectedPointIndex = target;
        RefreshComponentSelectors();
        _point.Selected = target + 1;
        LoadPointFields();
        InvalidatePreview("Ordre des points modifié dans le brouillon.");
    }

    private void AddLane()
    {
        if (_draft is null) return;
        CommitFields();
        var item = _session.CreateNewVehicleLaneUx2NavL(_draft);
        if (item is null) { SetStatus("La limite de 16 voies est atteinte.", true); return; }
        _draft = _draft with { Lanes = _draft.Lanes.Concat([item]).ToArray() };
        _selectedLaneIndex = _draft.Lanes.Count - 1;
        RefreshComponentSelectors();
        _lane.Selected = _selectedLaneIndex + 1;
        LoadLaneFields();
        InvalidatePreview("Voie ajoutée au brouillon.");
    }

    private void RemoveLane()
    {
        if (_draft is null || _selectedLaneIndex < 0) return;
        if (_draft.Lanes.Count <= 1)
        {
            SetStatus("Une route doit conserver au moins une voie.", true);
            return;
        }
        CommitFields();
        var removed = _selectedLaneIndex;
        _draft = _draft with { Lanes = _draft.Lanes.Where((_, index) => index != removed).ToArray() };
        _selectedLaneIndex = Math.Min(removed, _draft.Lanes.Count - 1);
        RefreshComponentSelectors();
        _lane.Selected = _selectedLaneIndex + 1;
        LoadLaneFields();
        InvalidatePreview("Voie retirée du brouillon.");
    }

    private void MoveLane(int delta)
    {
        if (_draft is null || _selectedLaneIndex < 0) return;
        CommitFields();
        var target = _selectedLaneIndex + delta;
        if (target < 0 || target >= _draft.Lanes.Count) return;
        var values = _draft.Lanes.ToArray();
        (values[_selectedLaneIndex], values[target]) = (values[target], values[_selectedLaneIndex]);
        _draft = _draft with { Lanes = values };
        _selectedLaneIndex = target;
        RefreshComponentSelectors();
        _lane.Selected = target + 1;
        LoadLaneFields();
        InvalidatePreview("Ordre des voies modifié dans le brouillon.");
    }

    private void AddStop()
    {
        if (_draft is null) return;
        CommitFields();
        var item = _session.CreateNewVehicleStopUx2NavL(_draft);
        if (item is null) { SetStatus("Aucun point disponible ou limite de 64 arrêts atteinte.", true); return; }
        _draft = _draft with { Stops = _draft.Stops.Concat([item]).ToArray() };
        _selectedStopIndex = _draft.Stops.Count - 1;
        RefreshComponentSelectors();
        _stop.Selected = _selectedStopIndex + 1;
        LoadStopFields();
        InvalidatePreview("Arrêt ajouté au brouillon.");
    }

    private void RemoveStop()
    {
        if (_draft is null || _selectedStopIndex < 0) return;
        CommitFields();
        var removed = _selectedStopIndex;
        _draft = _draft with { Stops = _draft.Stops.Where((_, index) => index != removed).ToArray() };
        _selectedStopIndex = _draft.Stops.Count == 0 ? -1 : Math.Min(removed, _draft.Stops.Count - 1);
        RefreshComponentSelectors();
        if (_selectedStopIndex >= 0)
        {
            _stop.Selected = _selectedStopIndex + 1;
            LoadStopFields();
        }
        else ClearStopFields();
        InvalidatePreview("Arrêt retiré du brouillon.");
    }

    private void MoveStop(int delta)
    {
        if (_draft is null || _selectedStopIndex < 0) return;
        CommitFields();
        var target = _selectedStopIndex + delta;
        if (target < 0 || target >= _draft.Stops.Count) return;
        var values = _draft.Stops.ToArray();
        (values[_selectedStopIndex], values[target]) = (values[target], values[_selectedStopIndex]);
        _draft = _draft with { Stops = values };
        _selectedStopIndex = target;
        RefreshComponentSelectors();
        _stop.Selected = target + 1;
        LoadStopFields();
        InvalidatePreview("Ordre des arrêts modifié dans le brouillon.");
    }

    private void RefreshComponentSelectors()
    {
        _updating = true;
        PopulatePoints();
        PopulateLanes();
        PopulateStops();
        if (_selectedPointIndex >= 0) _point.Selected = _selectedPointIndex + 1;
        if (_selectedLaneIndex >= 0) _lane.Selected = _selectedLaneIndex + 1;
        if (_selectedStopIndex >= 0) _stop.Selected = _selectedStopIndex + 1;
        _updating = false;
    }

    private void LoadPointFields()
    {
        if (_draft is null || _selectedPointIndex < 0) { ClearPointFields(); return; }
        var item = _draft.Points[_selectedPointIndex];
        _updating = true;
        _pointX.Value = item.PositionX;
        _pointY.Value = item.PositionY;
        _pointZ.Value = item.PositionZ;
        _tangent.Value = item.TangentDegrees;
        _width.Value = item.Width;
        _updating = false;
        SetPointEnabled(true);
    }

    private void LoadLaneFields()
    {
        if (_draft is null || _selectedLaneIndex < 0) { ClearLaneFields(); return; }
        var item = _draft.Lanes[_selectedLaneIndex];
        _updating = true;
        _laneId.Text = item.IdIsProtected ? "Clé existante protégée" : item.Id;
        _laneId.Editable = !item.IdIsProtected;
        _laneOffset.Value = item.LateralOffset;
        _laneSpeed.Value = item.SpeedLimit;
        _laneDirection.Clear();
        if (item.DirectionIsProtected)
        {
            _laneDirection.AddItem("Sens existant protégé");
            _laneDirection.Selected = 0;
        }
        else
        {
            foreach (var direction in DirectionsUx2NavL)
                _laneDirection.AddItem(StudioSessionController.VehicleDirectionLabelUx2NavL(direction));
            _laneDirection.Selected = Math.Max(0, Array.IndexOf(DirectionsUx2NavL, item.Direction));
        }
        _updating = false;
        SetLaneEnabled(true);
    }

    private void LoadStopFields()
    {
        if (_draft is null || _selectedStopIndex < 0) { ClearStopFields(); return; }
        var item = _draft.Stops[_selectedStopIndex];
        _updating = true;
        PopulatePoints();
        if (_selectedPointIndex >= 0) _point.Selected = _selectedPointIndex + 1;
        _stopPoint.Selected = Math.Max(0, _draft.Points.ToList().FindIndex(point =>
            point.Id == item.RoutePointId));
        _wait.Value = item.WaitSeconds;
        _service.Text = item.ServiceIdIsProtected ? "Service existant protégé" : item.ServiceId;
        _service.Editable = !item.ServiceIdIsProtected;
        _updating = false;
        SetStopEnabled(true);
    }

    private void DraftChanged()
    {
        if (_updating || _draft is null) return;
        CommitFields();
        InvalidatePreview("Brouillon modifié · prévisualisez avant application.");
    }

    private void CommitFields()
    {
        if (_draft is null) return;
        var points = _draft.Points.ToArray();
        if (_selectedPointIndex >= 0 && _selectedPointIndex < points.Length)
            points[_selectedPointIndex] = points[_selectedPointIndex] with
            {
                PositionX = _pointX.Value,
                PositionY = _pointY.Value,
                PositionZ = _pointZ.Value,
                TangentDegrees = _tangent.Value,
                Width = _width.Value,
            };
        var lanes = _draft.Lanes.ToArray();
        if (_selectedLaneIndex >= 0 && _selectedLaneIndex < lanes.Length)
        {
            var lane = lanes[_selectedLaneIndex];
            var direction = lane.DirectionIsProtected
                ? lane.Direction
                : DirectionsUx2NavL[Math.Clamp(_laneDirection.Selected, 0, DirectionsUx2NavL.Length - 1)];
            lanes[_selectedLaneIndex] = lane with
            {
                Id = lane.IdIsProtected ? lane.Id : _laneId.Text,
                LateralOffset = _laneOffset.Value,
                SpeedLimit = _laneSpeed.Value,
                Direction = direction,
            };
        }
        var stops = _draft.Stops.ToArray();
        if (_selectedStopIndex >= 0 && _selectedStopIndex < stops.Length)
        {
            var stop = stops[_selectedStopIndex];
            var pointIndex = Math.Clamp(_stopPoint.Selected, 0, Math.Max(0, points.Length - 1));
            stops[_selectedStopIndex] = stop with
            {
                RoutePointId = points.Length == 0 ? stop.RoutePointId : points[pointIndex].Id,
                WaitSeconds = _wait.Value,
                ServiceId = stop.ServiceIdIsProtected ? stop.ServiceId : _service.Text,
            };
        }
        _draft = _draft with
        {
            Points = points,
            Lanes = lanes,
            Stops = stops,
            DefaultSpeedLimit = _defaultSpeed.Value,
            AllowOvertaking = _overtaking.ButtonPressed,
            NetworkSyncMode = SyncModesUx2NavL[Math.Clamp(_sync.Selected, 0, SyncModesUx2NavL.Length - 1)],
        };
    }

    private void Preview()
    {
        if (_draft is null) return;
        CommitFields();
        var preview = _session.PreviewVehicleRouteUx2NavL(_draft);
        _lastPreview = preview;
        RenderPreview(preview);
        if (preview.Succeeded)
        {
            if (preview.Overlay.Markers.Count > 0)
                _worldViewport.ShowNavigationOverlayUx2NavB(preview.Overlay);
            else _worldViewport.ClearNavigationOverlayUx2NavB();
        }
        else _worldViewport.ClearNavigationOverlayUx2NavB();
        SetStatus(preview.Message, !preview.Succeeded);
        UpdateActionState();
    }

    private void Apply()
    {
        CommitFields();
        var result = _session.ApplyApprovedVehicleRouteUx2NavL();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) { UpdateActionState(); return; }
        VehicleRouteChanged?.Invoke();
        var areaId = _draft?.AreaId;
        var routeId = _draft?.RouteId;
        var removed = _draft?.Remove == true;
        PopulateAreas();
        if (areaId is not null)
        {
            var areaIndex = _areaChoices.ToList().FindIndex(item => item.AreaId == areaId);
            if (areaIndex >= 0)
            {
                _area.Selected = areaIndex + 1;
                SelectArea(areaIndex + 1);
                if (!removed && routeId is not null)
                {
                    var routeIndex = _routeChoices.ToList().FindIndex(item => item.RouteId == routeId.Value);
                    if (routeIndex >= 0)
                    {
                        _route.Selected = routeIndex + 1;
                        SelectRoute(routeIndex + 1);
                    }
                }
            }
        }
        UpdateActionState();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) RefreshAfterHistory();
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) RefreshAfterHistory();
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
        UpdateActionState();
    }

    private void RefreshAfterHistory()
    {
        _session.CancelVehicleRouteApprovalUx2NavL();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        var areaId = _explicitAreaSelection && _area.Selected > 0
            ? _areaChoices[_area.Selected - 1].AreaId : null;
        var routeId = _draft?.RouteId;
        PopulateAreas();
        if (areaId is not null)
        {
            var areaIndex = _areaChoices.ToList().FindIndex(item => item.AreaId == areaId);
            if (areaIndex >= 0)
            {
                _area.Selected = areaIndex + 1;
                SelectArea(areaIndex + 1);
                if (routeId is not null)
                {
                    var routeIndex = _routeChoices.ToList().FindIndex(item => item.RouteId == routeId.Value);
                    if (routeIndex >= 0)
                    {
                        _route.Selected = routeIndex + 1;
                        SelectRoute(routeIndex + 1);
                    }
                }
            }
        }
        VehicleRouteChanged?.Invoke();
        UpdateActionState();
    }

    private void InvalidatePreview(string message)
    {
        _session.CancelVehicleRouteApprovalUx2NavL();
        _lastPreview = null;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        RenderPreview(null);
        SetStatus(message);
        UpdateActionState();
    }

    private void ClearRouteFields()
    {
        _draft = null;
        _explicitRouteSelection = false;
        _selectedPointIndex = -1;
        _selectedLaneIndex = -1;
        _selectedStopIndex = -1;
        _updating = true;
        _defaultSpeed.Value = 1;
        _overtaking.ButtonPressed = false;
        _sync.Selected = 0;
        _point.Clear();
        _point.AddItem("Choisir un point…");
        _lane.Clear();
        _lane.AddItem("Choisir une voie…");
        _stop.Clear();
        _stop.AddItem("Choisir un arrêt…");
        _updating = false;
        ClearPointFields();
        ClearLaneFields();
        ClearStopFields();
        SetEditorEnabled(false);
        RenderPreview(null);
        UpdateActionState();
    }

    private void ClearPointFields()
    {
        _updating = true;
        _pointX.Value = 0;
        _pointY.Value = 0;
        _pointZ.Value = 0;
        _tangent.Value = 0;
        _width.Value = 1;
        _updating = false;
        SetPointEnabled(false);
    }

    private void ClearLaneFields()
    {
        _updating = true;
        _laneId.Text = string.Empty;
        _laneOffset.Value = 0;
        _laneSpeed.Value = 1;
        _laneDirection.Clear();
        _laneDirection.AddItem("Choisir un sens…");
        _updating = false;
        SetLaneEnabled(false);
    }

    private void ClearStopFields()
    {
        _updating = true;
        _stopPoint.Clear();
        _stopPoint.AddItem("Choisir un point…");
        _wait.Value = 0;
        _service.Text = string.Empty;
        _updating = false;
        SetStopEnabled(false);
    }

    private void SetEditorEnabled(bool enabled)
    {
        _defaultSpeed.Editable = enabled;
        _overtaking.Disabled = !enabled;
        _sync.Disabled = !enabled;
        _point.Disabled = !enabled;
        _lane.Disabled = !enabled;
        _stop.Disabled = !enabled;
        _addPoint.Disabled = !enabled;
        _addLane.Disabled = !enabled;
        _addStop.Disabled = !enabled;
        _removeRoute.Disabled = !enabled;
        if (!enabled)
        {
            SetPointEnabled(false);
            SetLaneEnabled(false);
            SetStopEnabled(false);
        }
    }

    private void SetPointEnabled(bool enabled)
    {
        foreach (var value in new[] { _pointX, _pointY, _pointZ, _tangent, _width }) value.Editable = enabled;
        _removePoint.Disabled = !enabled;
        _movePointUp.Disabled = !enabled;
        _movePointDown.Disabled = !enabled;
    }

    private void SetLaneEnabled(bool enabled)
    {
        var protectedId = _draft is not null && _selectedLaneIndex >= 0 &&
                          _selectedLaneIndex < _draft.Lanes.Count &&
                          _draft.Lanes[_selectedLaneIndex].IdIsProtected;
        _laneId.Editable = enabled && !protectedId;
        _laneOffset.Editable = enabled;
        _laneSpeed.Editable = enabled;
        _laneDirection.Disabled = !enabled;
        _removeLane.Disabled = !enabled;
        _moveLaneUp.Disabled = !enabled;
        _moveLaneDown.Disabled = !enabled;
    }

    private void SetStopEnabled(bool enabled)
    {
        var protectedService = _draft is not null && _selectedStopIndex >= 0 &&
                               _selectedStopIndex < _draft.Stops.Count &&
                               _draft.Stops[_selectedStopIndex].ServiceIdIsProtected;
        _stopPoint.Disabled = !enabled;
        _wait.Editable = enabled;
        _service.Editable = enabled && !protectedService;
        _removeStop.Disabled = !enabled;
        _moveStopUp.Disabled = !enabled;
        _moveStopDown.Disabled = !enabled;
    }

    private void UpdateActionState()
    {
        var hasDraft = _draft is not null;
        _addRoute.Disabled = !_explicitAreaSelection;
        _removeRoute.Disabled = !hasDraft;
        _preview.Disabled = !hasDraft;
        _apply.Disabled = _lastPreview?.CanApply != true;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = !_session.IsDirty;
    }

    private void RenderPreview(StudioUx2NavLPreview? preview)
    {
        ReplaceColumn(_diff, preview?.Diff ?? [], "Aucun changement calculé.", StudioStyle.Text);
        ReplaceColumn(_diagnostics,
            preview?.Diagnostics.Select(item => $"{item.Path} · {item.Message}").ToArray() ?? [],
            preview is { Succeeded: true } ? "Aucun diagnostic bloquant." : "Prévisualisation requise.",
            preview is { Succeeded: true } ? StudioStyle.Success : StudioStyle.Muted);
        var runtimeLines = preview?.Runtime?.Samples.Select(item =>
            $"{item.Percent}% · ({item.PositionX:0.##}, {item.PositionY:0.##}, {item.PositionZ:0.##}) · " +
            $"vitesse {item.SpeedLimit:0.##}" + (item.AtStop ? $" · arrêt {item.WaitSeconds:0.##} s" : ""))
            .ToArray() ?? [];
        ReplaceColumn(_runtime, runtimeLines, "Aucun échantillon calculé.", StudioStyle.Text);
        _summary.Text = preview?.Summary ?? "Aucun aperçu calculé.";
        _boundary.Text = preview?.Runtime?.Boundary ??
                         "Limite : position, vitesse et arrêt seulement ; tangentes, largeur, offsets, sens, " +
                         "dépassement et sync restent persistés sans exécution.";
    }

    private static void ReplaceColumn(VBoxContainer column, IReadOnlyList<string> values,
        string empty, Color color)
    {
        foreach (var child in column.GetChildren()) child.QueueFree();
        if (values.Count == 0)
        {
            column.AddChild(StudioStyle.Label(empty, 9, color, wrap: true));
            return;
        }
        foreach (var value in values.Take(8))
            column.AddChild(StudioStyle.Label("• " + value, 9, color, wrap: true));
        if (values.Count > 8)
            column.AddChild(StudioStyle.Label($"+ {values.Count - 8} autre(s)", 9, StudioStyle.Muted));
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private bool FailEvidencePreparation(string? tempRoot)
    {
        _evidenceSession?.CancelVehicleRouteApprovalUx2NavL();
        _evidenceSession?.Close();
        _evidenceSession = null;
        _session = _sourceSession;
        var root = tempRoot ?? _evidenceTempRoot;
        _lastEvidenceResourcesReleased = StudioSessionController.DeleteUx2NavLTemporaryWorkspace(root);
        _evidenceTempRoot = null;
        _lastEvidenceSourceUntouched = _evidenceSourceSnapshot is null ||
                                       SourceSnapshotMatches(_evidenceSourceSnapshot);
        _lastSourceRestored = true;
        _evidenceSourceSnapshot = null;
        return false;
    }

    private StudioUx2NavLEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History!;
        return new(_sourceSession.Project!, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureVehicleRouteApprovalUx2NavL());
    }

    private bool SourceSnapshotMatches(StudioUx2NavLEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureVehicleRouteApprovalUx2NavL() == snapshot.Approval;
    }

    private void ReleaseEvidenceWorkspace()
    {
        var snapshot = _evidenceSourceSnapshot;
        var evidence = _evidenceSession;
        var root = _evidenceTempRoot;
        if (evidence is not null)
        {
            evidence.CancelVehicleRouteApprovalUx2NavL();
            evidence.Close();
        }
        _evidenceSession = null;
        _evidenceTempRoot = null;
        _session = _sourceSession;
        if (root is not null)
            _lastEvidenceResourcesReleased = StudioSessionController.DeleteUx2NavLTemporaryWorkspace(root);
        else if (evidence is null) _lastEvidenceResourcesReleased = true;
        _lastEvidenceSourceUntouched = snapshot is null || SourceSnapshotMatches(snapshot);
        _lastSourceRestored = ReferenceEquals(_session, _sourceSession);
        _evidenceSourceSnapshot = null;
    }

    private void BindViewportToArea(StudioSessionController session, AreaId areaId)
    {
        try { _worldViewport.RebuildArea(session, areaId, readOnly: true); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_L_VIEWPORT_FALLBACK {exception.Message}");
        }
    }

    private void RestoreSourceViewport()
    {
        try
        {
            if (_sourceSession.ActiveAreaId is { } activeArea)
            {
                _worldViewport.RebuildArea(_sourceSession, activeArea, readOnly: false, frameArea: false);
                _worldViewport.ApplyCameraProfile(_sourceSession.CameraProfile);
            }
            else _worldViewport.ShowNoArea(_sourceSession);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_L_VIEWPORT_RESTORE_FAILED {exception.Message}");
        }
    }

    private void UpdateDialogBounds()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = viewport.X >= 3_200 ? 1.5f : viewport.X >= 2_300 ? 1.15f : 1f;
        var width = Math.Clamp((viewport.X * .52f) / scale, 840f, 1_080f);
        var height = Math.Clamp((viewport.Y - 30f) / scale, 700f, 1_180f);
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2(viewport.X - width * scale - 15f,
            Math.Max(15f, (viewport.Y - height * scale) / 2f));
    }

    private static SpinBox NumberUx2NavL(double minimum, double maximum, double step)
    {
        var control = StudioUx2NavCControls.Number(minimum, maximum, step);
        control.AllowGreater = false;
        control.AllowLesser = false;
        return control;
    }

    private static HBoxContainer RowUx2NavL()
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        return row;
    }

    private static VBoxContainer PreviewColumnUx2NavL(HBoxContainer host, string title)
    {
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(220, 0),
        };
        column.AddThemeConstantOverride("separation", 3);
        var wrapper = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        wrapper.AddChild(StudioStyle.Label(title, 9, StudioStyle.Accent));
        wrapper.AddChild(column);
        host.AddChild(wrapper);
        return column;
    }
}
