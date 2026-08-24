using Forge.Domain.Areas;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAerialNavigationOverlayUx2NavI : Control
{
    private const string ProtectedNameUx2NavI = "Nom existant protégé";
    private static readonly FlightRouteCurve[] CurveValues = Enum.GetValues<FlightRouteCurve>();
    private static readonly FlightObstaclePolicy[] ObstaclePolicies =
        Enum.GetValues<FlightObstaclePolicy>();

    private readonly StudioSessionController _sourceSession;
    private readonly StudioWorldViewport _worldViewport;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2NavIEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _lastSourceRestored = true;
    private bool _lastUiTransaction;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly OptionButton _route;
    private readonly Button _newRoute;
    private readonly Label _selection;
    private readonly VBoxContainer _editor;
    private readonly CheckBox _enabled;
    private readonly CheckBox _flight;
    private readonly CheckBox _swim;
    private readonly CheckBox _space;
    private readonly SpinBox _cellSize;
    private readonly SpinBox _centerX;
    private readonly SpinBox _centerY;
    private readonly SpinBox _centerZ;
    private readonly SpinBox _sizeX;
    private readonly SpinBox _sizeY;
    private readonly SpinBox _sizeZ;
    private readonly SpinBox _avoidance;
    private readonly CheckBox _volumeDebug;
    private readonly OptionButton _volume;
    private readonly Label _volumeKind;
    private readonly Button _addObstacle;
    private readonly Button _addCost;
    private readonly Button _removeVolume;
    private readonly VBoxContainer _volumeFields;
    private readonly LineEdit _volumeName;
    private readonly SpinBox _volumeCenterX;
    private readonly SpinBox _volumeCenterY;
    private readonly SpinBox _volumeCenterZ;
    private readonly SpinBox _volumeSizeX;
    private readonly SpinBox _volumeSizeY;
    private readonly SpinBox _volumeSizeZ;
    private readonly SpinBox _volumeCost;
    private readonly Control _routeCard;
    private readonly LineEdit _routeName;
    private readonly OptionButton _curve;
    private readonly OptionButton _obstaclePolicy;
    private readonly CheckBox _closed;
    private readonly CheckBox _routeDebug;
    private readonly OptionButton _node;
    private readonly Button _addNode;
    private readonly Button _removeNode;
    private readonly Button _moveNodeUp;
    private readonly Button _moveNodeDown;
    private readonly VBoxContainer _nodeFields;
    private readonly LineEdit _nodeName;
    private readonly SpinBox _nodeX;
    private readonly SpinBox _nodeY;
    private readonly SpinBox _nodeZ;
    private readonly SpinBox _nodeSpeed;
    private readonly SpinBox _nodeBank;
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

    private IReadOnlyList<StudioUx2NavIAreaChoice> _areaChoices = [];
    private IReadOnlyList<StudioUx2NavIRouteChoice> _routeChoices = [];
    private StudioUx2NavIDraft? _draft;
    private StudioUx2NavIPreview? _lastPreview;
    private Guid? _selectedVolumeId;
    private Guid? _selectedNodeId;
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
            var text = string.Join(' ', DescendantLabelsUx2NavI(_dialog).Select(item => item.Text));
            return HasExplicitSelection && text.Contains("Navigation volumétrique",
                       StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Volumes nommés", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Route aérienne", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) && !ContainsUuidUx2NavI(text);
        }
    }

    internal StudioAerialNavigationOverlayUx2NavI(StudioSessionController session,
        StudioWorldViewport worldViewport)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        _worldViewport = worldViewport ?? throw new ArgumentNullException(nameof(worldViewport));
        Name = "StudioAerialNavigationOverlayUx2NavI";
        Visible = false;
        ZIndex = 463;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _dialog = StudioStyle.Panel("AerialNavigationDialogUx2NavI", StudioStyle.Chrome, 12, 2);
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

        var header = RowUx2NavI();
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("NAVIGATION · VOLUME ET ROUTES AÉRIENNES", 10,
            StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Navigation 3D authorée sans déplacer d’acteur", 19));
        heading.AddChild(StudioStyle.Label(
            "ORF-AREA-032 / ORF-WPT-011 · grille XYZ, obstacles, coûts, altitude, courbe, vitesse, banking et politique d’obstacle. " +
            "Le preview exécute les services Domain purs ; il ne simule ni IA autonome ni collision Godot.",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var selectorRow = RowUx2NavI();
        root.AddChild(selectorRow);
        _area = StudioUx2NavCControls.Options();
        _area.ItemSelected += SelectArea;
        selectorRow.AddChild(StudioUx2NavCControls.Field("Zone", _area, 200));
        _route = StudioUx2NavCControls.Options();
        _route.ItemSelected += SelectRoute;
        selectorRow.AddChild(StudioUx2NavCControls.Field("Route aérienne", _route, 240));
        _newRoute = StudioStyle.Button("Nouvelle route", compact: true);
        _newRoute.TooltipText = "Créer explicitement une route de deux étapes dans le volume";
        _newRoute.Pressed += AddRoute;
        selectorRow.AddChild(_newRoute);
        _selection = StudioStyle.Label(
            "Choisissez explicitement une zone ; aucune route ni valeur n’est chargée automatiquement.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var bodyHost = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 440),
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

        var profileContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        profileContent.AddThemeConstantOverride("separation", 6);
        var modeRow = RowUx2NavI();
        _enabled = StudioUx2NavCControls.Check("Activer la navigation 3D");
        _flight = StudioUx2NavCControls.Check("Vol");
        _swim = StudioUx2NavCControls.Check("Nage");
        _space = StudioUx2NavCControls.Check("Espace");
        _volumeDebug = StudioUx2NavCControls.Check("Afficher le diagnostic 3D");
        modeRow.AddChild(_enabled);
        modeRow.AddChild(_flight);
        modeRow.AddChild(_swim);
        modeRow.AddChild(_space);
        modeRow.AddChild(_volumeDebug);
        profileContent.AddChild(modeRow);
        var gridRow = RowUx2NavI();
        _cellSize = StudioUx2NavCControls.Number(.5, 64, .25);
        _avoidance = StudioUx2NavCControls.Number(0, 64, .1);
        _centerX = NumberUx2NavI();
        _centerY = NumberUx2NavI();
        _centerZ = NumberUx2NavI();
        gridRow.AddChild(StudioUx2NavCControls.Field("Cellule", _cellSize));
        gridRow.AddChild(StudioUx2NavCControls.Field("Évitement", _avoidance));
        gridRow.AddChild(StudioUx2NavCControls.Field("Centre X", _centerX));
        gridRow.AddChild(StudioUx2NavCControls.Field("Centre Y", _centerY));
        gridRow.AddChild(StudioUx2NavCControls.Field("Centre Z", _centerZ));
        profileContent.AddChild(gridRow);
        var sizeRow = RowUx2NavI();
        _sizeX = PositiveFloatNumberUx2NavI();
        _sizeY = PositiveFloatNumberUx2NavI();
        _sizeZ = PositiveFloatNumberUx2NavI();
        sizeRow.AddChild(StudioUx2NavCControls.Field("Largeur X", _sizeX));
        sizeRow.AddChild(StudioUx2NavCControls.Field("Hauteur Y", _sizeY));
        sizeRow.AddChild(StudioUx2NavCControls.Field("Profondeur Z", _sizeZ));
        profileContent.AddChild(sizeRow);
        _editor.AddChild(StudioUx2NavCControls.Card("AerialVolumeProfileUx2NavI",
            "Navigation volumétrique",
            "Le volume principal borne la grille voxel. Les modes absents restent ignorés proprement au runtime.",
            profileContent));

        var namedVolumeContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        namedVolumeContent.AddThemeConstantOverride("separation", 6);
        var volumeSelector = RowUx2NavI();
        _volume = StudioUx2NavCControls.Options();
        _volume.ItemSelected += SelectVolume;
        volumeSelector.AddChild(StudioUx2NavCControls.Field("Volume sélectionné", _volume, 230));
        _volumeKind = StudioStyle.Label("Aucun volume sélectionné", 10, StudioStyle.Muted);
        volumeSelector.AddChild(_volumeKind);
        _addObstacle = StudioStyle.Button("Ajouter obstacle", compact: true);
        _addObstacle.Pressed += () => AddVolume(StudioUx2NavIVolumeKind.Obstacle);
        volumeSelector.AddChild(_addObstacle);
        _addCost = StudioStyle.Button("Ajouter coût", compact: true);
        _addCost.Pressed += () => AddVolume(StudioUx2NavIVolumeKind.Cost);
        volumeSelector.AddChild(_addCost);
        _removeVolume = StudioStyle.Button("Retirer", compact: true);
        _removeVolume.Pressed += RemoveVolume;
        volumeSelector.AddChild(_removeVolume);
        namedVolumeContent.AddChild(volumeSelector);
        _volumeFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _volumeFields.AddThemeConstantOverride("separation", 5);
        namedVolumeContent.AddChild(_volumeFields);
        _volumeName = StudioUx2NavCControls.Text("Courant ascendant");
        _volumeFields.AddChild(StudioUx2NavCControls.Field("Nom humain", _volumeName));
        var volumePositionRow = RowUx2NavI();
        _volumeCenterX = NumberUx2NavI();
        _volumeCenterY = NumberUx2NavI();
        _volumeCenterZ = NumberUx2NavI();
        volumePositionRow.AddChild(StudioUx2NavCControls.Field("Centre X", _volumeCenterX));
        volumePositionRow.AddChild(StudioUx2NavCControls.Field("Centre Y", _volumeCenterY));
        volumePositionRow.AddChild(StudioUx2NavCControls.Field("Centre Z", _volumeCenterZ));
        _volumeFields.AddChild(volumePositionRow);
        var volumeSizeRow = RowUx2NavI();
        _volumeSizeX = PositiveFloatNumberUx2NavI();
        _volumeSizeY = PositiveFloatNumberUx2NavI();
        _volumeSizeZ = PositiveFloatNumberUx2NavI();
        _volumeCost = StudioUx2NavCControls.Number(0, 100, .1);
        volumeSizeRow.AddChild(StudioUx2NavCControls.Field("Taille X", _volumeSizeX));
        volumeSizeRow.AddChild(StudioUx2NavCControls.Field("Taille Y", _volumeSizeY));
        volumeSizeRow.AddChild(StudioUx2NavCControls.Field("Taille Z", _volumeSizeZ));
        volumeSizeRow.AddChild(StudioUx2NavCControls.Field("Coût", _volumeCost));
        _volumeFields.AddChild(volumeSizeRow);
        _editor.AddChild(StudioUx2NavCControls.Card("AerialNamedVolumesUx2NavI",
            "Volumes nommés · obstacles et coûts",
            "Chaque entrée conserve son identité et sa provenance ; seule l’entrée sélectionnée est modifiée.",
            namedVolumeContent));

        var routeContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        routeContent.AddThemeConstantOverride("separation", 6);
        var routePolicyRow = RowUx2NavI();
        _routeName = StudioUx2NavCControls.Text("Ronde des hautes branches");
        _curve = StudioUx2NavCControls.Options(CurveValues.Select(HumanCurve).ToArray());
        _obstaclePolicy = StudioUx2NavCControls.Options(ObstaclePolicies.Select(HumanObstaclePolicy).ToArray());
        _closed = StudioUx2NavCControls.Check("Boucle fermée");
        _routeDebug = StudioUx2NavCControls.Check("Afficher la route");
        routePolicyRow.AddChild(StudioUx2NavCControls.Field("Nom de route", _routeName, 180));
        routePolicyRow.AddChild(StudioUx2NavCControls.Field("Courbe", _curve));
        routePolicyRow.AddChild(StudioUx2NavCControls.Field("Obstacle", _obstaclePolicy));
        routePolicyRow.AddChild(_closed);
        routePolicyRow.AddChild(_routeDebug);
        routeContent.AddChild(routePolicyRow);
        var nodeSelector = RowUx2NavI();
        _node = StudioUx2NavCControls.Options();
        _node.ItemSelected += SelectNode;
        nodeSelector.AddChild(StudioUx2NavCControls.Field("Étape ordonnée", _node, 220));
        _addNode = StudioStyle.Button("Ajouter", compact: true);
        _addNode.Pressed += AddNode;
        nodeSelector.AddChild(_addNode);
        _removeNode = StudioStyle.Button("Retirer", compact: true);
        _removeNode.Pressed += RemoveNode;
        nodeSelector.AddChild(_removeNode);
        _moveNodeUp = StudioStyle.Button("Monter", compact: true);
        _moveNodeUp.TooltipText = "Alternative clavier : Alt+Flèche haut";
        _moveNodeUp.Pressed += MoveNodeUp;
        nodeSelector.AddChild(_moveNodeUp);
        _moveNodeDown = StudioStyle.Button("Descendre", compact: true);
        _moveNodeDown.TooltipText = "Alternative clavier : Alt+Flèche bas";
        _moveNodeDown.Pressed += MoveNodeDown;
        nodeSelector.AddChild(_moveNodeDown);
        routeContent.AddChild(nodeSelector);
        _nodeFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _nodeFields.AddThemeConstantOverride("separation", 5);
        routeContent.AddChild(_nodeFields);
        _nodeName = StudioUx2NavCControls.Text("Canopée");
        _nodeFields.AddChild(StudioUx2NavCControls.Field("Nom de l’étape", _nodeName));
        var nodePosition = RowUx2NavI();
        _nodeX = NumberUx2NavI();
        _nodeY = NumberUx2NavI();
        _nodeZ = NumberUx2NavI();
        nodePosition.AddChild(StudioUx2NavCControls.Field("Position X", _nodeX));
        nodePosition.AddChild(StudioUx2NavCControls.Field("Altitude Y", _nodeY));
        nodePosition.AddChild(StudioUx2NavCControls.Field("Position Z", _nodeZ));
        _nodeFields.AddChild(nodePosition);
        var nodeMotion = RowUx2NavI();
        _nodeSpeed = StudioUx2NavCControls.Number(.01, 500, .1);
        _nodeBank = StudioUx2NavCControls.Number(-89, 89, 1);
        nodeMotion.AddChild(StudioUx2NavCControls.Field("Vitesse", _nodeSpeed));
        nodeMotion.AddChild(StudioUx2NavCControls.Field("Banking (°)", _nodeBank));
        _nodeFields.AddChild(nodeMotion);
        routeContent.AddChild(StudioStyle.Label(
            "Monter/Descendre conserve l’identité et tous les réglages de l’étape. L’altitude Y est une donnée canonique.",
            9, StudioStyle.Muted, wrap: true));
        var routePanel = StudioUx2NavCControls.Card("AerialFlightRouteUx2NavI",
            "Route aérienne 3D",
            "Choisissez une route ou créez-la explicitement. Le volume seul peut être édité sans route.",
            routeContent);
        _editor.AddChild(routePanel);
        _routeCard = routePanel;

        var resultRow = RowUx2NavI();
        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _runtime = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        resultRow.AddChild(StudioUx2NavCControls.Card("AerialDiffUx2NavI",
            "Diff sémantique", "Ce que remplacera la commande atomique.", _diff));
        resultRow.AddChild(StudioUx2NavCControls.Card("AerialDiagnosticsUx2NavI",
            "Diagnostics", "Codes stables et chemins humains.", _diagnostics));
        resultRow.AddChild(StudioUx2NavCControls.Card("AerialRuntimeUx2NavI",
            "Preview runtime", "Grille, A* et contrôleur de vol Domain.", _runtime));
        _editor.AddChild(resultRow);
        _summary = StudioStyle.Label("Aucun aperçu calculé.", 10, StudioStyle.Muted, wrap: true);
        _editor.AddChild(_summary);
        _boundary = StudioStyle.Label(
            "Limite : aucun acteur, collision Godot, IA autonome, réseau ou préchargement n’est exécuté.",
            10, StudioStyle.Warning, wrap: true);
        _editor.AddChild(_boundary);

        var footer = RowUx2NavI();
        root.AddChild(footer);
        _status = StudioStyle.Label("Sélection explicite requise.", 10, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(_status);
        _preview = StudioStyle.Button("Aperçu sec", compact: true);
        _preview.TooltipText = "Valider, construire la grille et calculer le diff sans mutation";
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
        RenderPreview(null);
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
            MoveNodeUp();
            GetViewport().SetInputAsHandled();
        }
        else if (key.AltPressed && key.Keycode == Key.Down)
        {
            MoveNodeDown();
            GetViewport().SetInputAsHandled();
        }
    }

    internal void Open()
    {
        ReleaseEvidenceWorkspace();
        _session = _sourceSession;
        _sourceSession.CancelAerialNavigationUx2NavI();
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;
        PopulateAreas();
        ClearSelection();
        UpdateDialogBounds();
        SetStatus("Choisissez explicitement une zone ; aucune fiche n’est chargée automatiquement.");
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null)
        {
            return;
        }
        if (_evidenceSession is not null)
        {
            _evidenceSession.CancelAerialNavigationUx2NavI();
        }
        else
        {
            _sourceSession.CancelAerialNavigationUx2NavI();
        }
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _draft = null;
        _lastPreview = null;
        _selectedVolumeId = null;
        _selectedNodeId = null;
        _canonicalPreviewReady = false;
        _explicitSelection = false;
        ReleaseEvidenceWorkspace();
        RestoreSourceViewport();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!string.Equals(mode, "ux2-nav-i", StringComparison.OrdinalIgnoreCase) ||
            _sourceSession.Project is null || _sourceSession.History is null)
        {
            return false;
        }
        ReleaseEvidenceWorkspace();
        _evidenceSourceSnapshot = CaptureSourceSnapshot();
        _lastEvidenceSourceUntouched = true;
        _lastEvidenceResourcesReleased = false;
        _lastSourceRestored = false;
        var project = _sourceSession.CreateUx2NavIEvidenceProject();
        if (project is null)
        {
            return false;
        }
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-i-evidence-" + Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                _lastEvidenceResourcesReleased =
                    StudioSessionController.DeleteUx2NavITemporaryWorkspace(tempRoot);
                return false;
            }
            _evidenceTempRoot = tempRoot;
            var evidence = new StudioSessionController();
            if (!evidence.Open(tempRoot).Succeeded)
            {
                evidence.Close();
                _lastEvidenceResourcesReleased =
                    StudioSessionController.DeleteUx2NavITemporaryWorkspace(tempRoot);
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
            if (routeIndex < 0)
            {
                Close();
                return false;
            }
            _route.Selected = routeIndex + 1;
            SelectRoute(routeIndex + 1);
            var volumeIndex = _draft!.Volumes.ToList().FindIndex(item =>
                item.Kind == StudioUx2NavIVolumeKind.Cost);
            if (volumeIndex >= 0)
            {
                _volume.Selected = volumeIndex + 1;
                SelectVolume(volumeIndex + 1);
                _volumeCost.Value = 3.25;
            }
            _node.Selected = 2;
            SelectNode(2);
            _nodeY.Value = 13;
            _nodeSpeed.Value = 13;
            _nodeBank.Value = 24;
            _curve.Selected = Array.IndexOf(CurveValues, FlightRouteCurve.Bezier);
            _obstaclePolicy.Selected = Array.IndexOf(ObstaclePolicies, FlightObstaclePolicy.Avoid);
            _space.ButtonPressed = true;
            DraftChanged();
            Preview();
            UpdateDialogBounds();
            return HasExplicitSelection && _selectedNodeId is not null && _selectedVolumeId is not null &&
                   _canonicalPreviewReady && ShowsViewportOverlay && UsesIsolatedEvidence &&
                   EvidenceSourceUntouched && ShowsHumanSurface;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_I_EVIDENCE_FAILED type={exception.GetType().Name} message={exception.Message}");
            Close();
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        _lastUiTransaction = false;
        try
        {
            if (!PrepareEvidence("ux2-nav-i"))
            {
                Close();
                return false;
            }
            var beforeOrder = _draft!.Route!.Nodes.Select(item => item.Id).ToArray();
            _moveNodeDown.EmitSignal(Button.SignalName.Pressed);
            var movedOrder = _draft?.Route?.Nodes.Select(item => item.Id).ToArray() ?? [];
            var movedThroughUi = beforeOrder.Length == movedOrder.Length &&
                                 !beforeOrder.SequenceEqual(movedOrder) &&
                                 movedOrder[2] == beforeOrder[1];
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
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_I_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
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
        var approval = _sourceSession.CaptureAerialApprovalUx2NavI();
        Open();
        _sourceSession.RestoreAerialApprovalUx2NavI(approval);
    }

    private void PopulateAreas()
    {
        _updating = true;
        _area.Clear();
        _area.AddItem("Choisir une zone…");
        _areaChoices = _session.AerialAreaChoicesUx2NavI;
        foreach (var choice in _areaChoices)
        {
            _area.AddItem(choice.Label);
        }
        _area.Selected = 0;
        _updating = false;
    }

    private void ClearSelection()
    {
        _updating = true;
        _route.Clear();
        _route.AddItem("Choisir une route…");
        _route.Selected = 0;
        _volume.Clear();
        _volume.AddItem("Choisir un volume nommé…");
        _volume.Selected = 0;
        _node.Clear();
        _node.AddItem("Choisir une étape…");
        _node.Selected = 0;
        _updating = false;
        _routeChoices = [];
        _draft = null;
        _lastPreview = null;
        _selectedVolumeId = null;
        _selectedNodeId = null;
        _explicitSelection = false;
        _canonicalPreviewReady = false;
        _selection.Text =
            "Choisissez explicitement une zone ; aucune route ni valeur n’est chargée automatiquement.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        SetEditorEnabled(false);
        RenderPreview(null);
        UpdateActionState();
    }

    private void SelectArea(long index)
    {
        if (_updating)
        {
            return;
        }
        _session.CancelAerialNavigationUx2NavI();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _selectedVolumeId = null;
        _selectedNodeId = null;
        _explicitSelection = false;
        _draft = null;
        _updating = true;
        _route.Clear();
        _route.AddItem("Choisir une route…");
        _routeChoices = [];
        if (index > 0 && index <= _areaChoices.Count)
        {
            var choice = _areaChoices[(int)index - 1];
            _draft = _session.CreateAerialDraftUx2NavI(choice.AreaId);
            _routeChoices = _session.AerialRouteChoicesUx2NavI(choice.AreaId);
            foreach (var route in _routeChoices)
            {
                _route.AddItem(route.Label);
            }
            BindViewportToArea(_session, choice.AreaId);
        }
        _route.Selected = 0;
        _updating = false;
        if (_draft is null)
        {
            ClearSelection();
            return;
        }
        _explicitSelection = true;
        LoadDraft(_draft);
        SetEditorEnabled(true);
        _routeCard.Visible = false;
        _selection.Text = $"{_draft.AreaDisplayName} · volume sélectionné explicitement · aucune route chargée";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
        SetStatus("Volume chargé · choisissez éventuellement une route, puis prévisualisez.");
        UpdateActionState();
    }

    private void SelectRoute(long index)
    {
        if (_updating || _draft is null || _area.Selected <= 0 ||
            _area.Selected > _areaChoices.Count)
        {
            return;
        }
        SaveControlValuesIntoDraft();
        var route = index > 0 && index <= _routeChoices.Count
            ? _session.CreateAerialRouteDraftUx2NavI(_draft.AreaId,
                _routeChoices[(int)index - 1].RouteId)
            : null;
        _draft = _draft with { Route = route };
        _selectedNodeId = null;
        LoadDraft(_draft);
        InvalidatePreview();
        _selection.Text = route is null
            ? $"{_draft.AreaDisplayName} · volume sélectionné · aucune route chargée"
            : $"{_draft.AreaDisplayName} · route « {HumanTextUx2NavI(route.Name, "Nom existant protégé")} » " +
              "sélectionnée explicitement · non appliquée";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
    }

    private void AddRoute()
    {
        if (_draft is null)
        {
            SetStatus("Choisissez d’abord une zone.", true);
            return;
        }
        SaveControlValuesIntoDraft();
        var route = _session.CreateNewAerialRouteDraftUx2NavI(_draft.AreaId);
        if (route is null)
        {
            SetStatus("La nouvelle route ne peut pas être préparée.", true);
            return;
        }
        _draft = _draft with { Route = route };
        _route.Selected = 0;
        _selectedNodeId = null;
        LoadDraft(_draft);
        InvalidatePreview();
        _selection.Text = $"{_draft.AreaDisplayName} · nouvelle route préparée explicitement · non appliquée";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
    }

    private void SelectVolume(long index)
    {
        if (_updating || _draft is null)
        {
            return;
        }
        SaveSelectedVolume();
        _selectedVolumeId = index > 0 && index <= _draft.Volumes.Count
            ? _draft.Volumes[(int)index - 1].Id
            : null;
        LoadSelectedVolume();
        InvalidatePreview();
    }

    private void AddVolume(StudioUx2NavIVolumeKind kind)
    {
        if (_draft is null)
        {
            SetStatus("Choisissez d’abord une zone.", true);
            return;
        }
        SaveControlValuesIntoDraft();
        var created = _session.CreateNewNavigationVolumeDraftUx2NavI(_draft, kind);
        _draft = _draft with { Volumes = _draft.Volumes.Concat([created]).ToArray() };
        _selectedVolumeId = created.Id;
        PopulateVolumes();
        LoadSelectedVolume();
        InvalidatePreview();
        SetStatus(kind == StudioUx2NavIVolumeKind.Obstacle
            ? "Obstacle ajouté au brouillon · aucun document modifié."
            : "Zone de coût ajoutée au brouillon · aucun document modifié.");
    }

    private void RemoveVolume()
    {
        if (_draft is null || _selectedVolumeId is null)
        {
            return;
        }
        _draft = _draft with
        {
            Volumes = _draft.Volumes.Where(item => item.Id != _selectedVolumeId.Value).ToArray(),
        };
        _selectedVolumeId = null;
        PopulateVolumes();
        LoadSelectedVolume();
        InvalidatePreview();
    }

    private void SelectNode(long index)
    {
        if (_updating || _draft?.Route is null)
        {
            return;
        }
        SaveSelectedNode();
        _selectedNodeId = index > 0 && index <= _draft.Route.Nodes.Count
            ? _draft.Route.Nodes[(int)index - 1].Id
            : null;
        LoadSelectedNode();
        InvalidatePreview();
    }

    private void AddNode()
    {
        if (_draft?.Route is not { } route)
        {
            return;
        }
        SaveControlValuesIntoDraft();
        route = _draft.Route!;
        var source = _selectedNodeId is null
            ? route.Nodes[^1]
            : route.Nodes.Single(item => item.Id == _selectedNodeId.Value);
        var offset = (float)Math.Clamp(_draft.CellSize, .5, 64);
        var position = new Vector3Value(
            OffsetInsideUx2NavI(source.Position.X, _draft.Center.X, _draft.Size.X, offset),
            source.Position.Y,
            OffsetInsideUx2NavI(source.Position.Z, _draft.Center.Z, _draft.Size.Z, offset));
        var created = new StudioUx2NavINodeDraft(Guid.NewGuid(), true, null, false,
            $"Étape {route.Nodes.Count + 1}", position, source.Speed, source.BankDegrees);
        var nodes = route.Nodes.ToList();
        var insertion = _selectedNodeId is null
            ? nodes.Count
            : nodes.FindIndex(item => item.Id == _selectedNodeId.Value) + 1;
        nodes.Insert(insertion, created);
        _draft = _draft with { Route = route with { Nodes = nodes } };
        _selectedNodeId = created.Id;
        PopulateNodes();
        LoadSelectedNode();
        InvalidatePreview();
    }

    private void RemoveNode()
    {
        if (_draft?.Route is not { } route || _selectedNodeId is null || route.Nodes.Count <= 2)
        {
            return;
        }
        _draft = _draft with
        {
            Route = route with
            {
                Nodes = route.Nodes.Where(item => item.Id != _selectedNodeId.Value).ToArray(),
            },
        };
        _selectedNodeId = null;
        PopulateNodes();
        LoadSelectedNode();
        InvalidatePreview();
    }

    private void MoveNodeUp() => MoveNode(-1);

    private void MoveNodeDown() => MoveNode(1);

    private void MoveNode(int direction)
    {
        if (_draft?.Route is not { } route || _selectedNodeId is null)
        {
            return;
        }
        SaveControlValuesIntoDraft();
        route = _draft.Route!;
        var nodes = route.Nodes.ToList();
        var from = nodes.FindIndex(item => item.Id == _selectedNodeId.Value);
        var to = from + direction;
        if (from < 0 || to < 0 || to >= nodes.Count)
        {
            return;
        }
        (nodes[from], nodes[to]) = (nodes[to], nodes[from]);
        _draft = _draft with { Route = route with { Nodes = nodes } };
        PopulateNodes();
        LoadSelectedNode();
        InvalidatePreview();
        SetStatus("Ordre des étapes modifié dans le brouillon · aperçu sec requis.");
    }

    private void LoadDraft(StudioUx2NavIDraft draft)
    {
        _updating = true;
        _enabled.ButtonPressed = draft.Enabled;
        _flight.ButtonPressed = draft.Modes.HasFlag(VolumetricNavigationMode.Flight);
        _swim.ButtonPressed = draft.Modes.HasFlag(VolumetricNavigationMode.Swim);
        _space.ButtonPressed = draft.Modes.HasFlag(VolumetricNavigationMode.Space);
        _cellSize.Value = draft.CellSize;
        _centerX.Value = draft.Center.X;
        _centerY.Value = draft.Center.Y;
        _centerZ.Value = draft.Center.Z;
        _sizeX.Value = draft.Size.X;
        _sizeY.Value = draft.Size.Y;
        _sizeZ.Value = draft.Size.Z;
        _avoidance.Value = draft.AvoidanceRadius;
        _volumeDebug.ButtonPressed = draft.DebugVisible;
        _routeCard.Visible = draft.Route is not null;
        if (draft.Route is { } route)
        {
            _routeName.Text = route.NameIsProtected ? ProtectedNameUx2NavI : route.Name;
            _curve.Selected = Math.Max(0, Array.IndexOf(CurveValues, route.Curve));
            _obstaclePolicy.Selected = Math.Max(0,
                Array.IndexOf(ObstaclePolicies, route.ObstaclePolicy));
            _closed.ButtonPressed = route.Closed;
            _routeDebug.ButtonPressed = route.DebugVisible;
        }
        PopulateVolumes();
        PopulateNodes();
        LoadSelectedVolume();
        LoadSelectedNode();
        _updating = false;
        UpdateActionState();
    }

    private void PopulateVolumes()
    {
        if (_draft is null)
        {
            return;
        }
        _updating = true;
        _volume.Clear();
        _volume.AddItem("Choisir un volume nommé…");
        foreach (var item in _draft.Volumes)
        {
            _volume.AddItem($"{(item.Kind == StudioUx2NavIVolumeKind.Obstacle ? "Obstacle" : "Coût")} · " +
                            $"{HumanTextUx2NavI(item.Name, "Volume nommé")}");
        }
        var index = _selectedVolumeId is null
            ? -1
            : _draft.Volumes.ToList().FindIndex(item => item.Id == _selectedVolumeId.Value);
        if (index < 0)
        {
            _selectedVolumeId = null;
            _volume.Selected = 0;
        }
        else
        {
            _volume.Selected = index + 1;
        }
        _updating = false;
    }

    private void PopulateNodes()
    {
        _updating = true;
        _node.Clear();
        _node.AddItem("Choisir une étape…");
        if (_draft?.Route is { } route)
        {
            for (var index = 0; index < route.Nodes.Count; index++)
            {
                _node.AddItem($"Étape {index + 1} · {HumanTextUx2NavI(route.Nodes[index].Name, "Sans nom")}");
            }
            var selected = _selectedNodeId is null
                ? -1
                : route.Nodes.ToList().FindIndex(item => item.Id == _selectedNodeId.Value);
            if (selected < 0)
            {
                _selectedNodeId = null;
                _node.Selected = 0;
            }
            else
            {
                _node.Selected = selected + 1;
            }
        }
        else
        {
            _selectedNodeId = null;
            _node.Selected = 0;
        }
        _updating = false;
    }

    private void LoadSelectedVolume()
    {
        var item = _draft?.Volumes.SingleOrDefault(candidate => candidate.Id == _selectedVolumeId);
        _updating = true;
        _volumeFields.Visible = item is not null;
        _removeVolume.Disabled = item is null;
        if (item is null)
        {
            _volumeKind.Text = "Aucun volume sélectionné";
        }
        else
        {
            _volumeKind.Text = item.Kind == StudioUx2NavIVolumeKind.Obstacle
                ? "Obstacle · zone bloquée"
                : "Zone de coût · multiplicateur de parcours";
            _volumeName.Text = item.NameIsProtected ? ProtectedNameUx2NavI : item.Name;
            _volumeCenterX.Value = item.Center.X;
            _volumeCenterY.Value = item.Center.Y;
            _volumeCenterZ.Value = item.Center.Z;
            _volumeSizeX.Value = item.Size.X;
            _volumeSizeY.Value = item.Size.Y;
            _volumeSizeZ.Value = item.Size.Z;
            _volumeCost.Value = item.CostMultiplier;
            _volumeCost.Editable = item.Kind == StudioUx2NavIVolumeKind.Cost;
        }
        _updating = false;
    }

    private void LoadSelectedNode()
    {
        var item = _draft?.Route?.Nodes.SingleOrDefault(candidate => candidate.Id == _selectedNodeId);
        _updating = true;
        _nodeFields.Visible = item is not null;
        _removeNode.Disabled = item is null || _draft?.Route?.Nodes.Count <= 2;
        if (item is not null)
        {
            _nodeName.Text = item.NameIsProtected ? ProtectedNameUx2NavI : item.Name;
            _nodeX.Value = item.Position.X;
            _nodeY.Value = item.Position.Y;
            _nodeZ.Value = item.Position.Z;
            _nodeSpeed.Value = item.Speed;
            _nodeBank.Value = item.BankDegrees;
        }
        UpdateNodeMoveState();
        _updating = false;
    }

    private void SaveControlValuesIntoDraft()
    {
        if (_draft is null)
        {
            return;
        }
        SaveSelectedVolume();
        SaveSelectedNode();
        var modes = VolumetricNavigationMode.None;
        if (_flight.ButtonPressed) modes |= VolumetricNavigationMode.Flight;
        if (_swim.ButtonPressed) modes |= VolumetricNavigationMode.Swim;
        if (_space.ButtonPressed) modes |= VolumetricNavigationMode.Space;
        var route = _draft.Route;
        if (route is not null)
        {
            var routeNameProtected = route.NameIsProtected &&
                                     string.Equals(_routeName.Text, ProtectedNameUx2NavI,
                                         StringComparison.Ordinal);
            route = route with
            {
                Name = routeNameProtected ? route.Name : _routeName.Text,
                NameIsProtected = routeNameProtected,
                Curve = _curve.Selected >= 0 && _curve.Selected < CurveValues.Length
                    ? CurveValues[_curve.Selected] : route.Curve,
                ObstaclePolicy = _obstaclePolicy.Selected >= 0 &&
                                 _obstaclePolicy.Selected < ObstaclePolicies.Length
                    ? ObstaclePolicies[_obstaclePolicy.Selected] : route.ObstaclePolicy,
                Closed = _closed.ButtonPressed,
                DebugVisible = _routeDebug.ButtonPressed,
            };
        }
        _draft = _draft with
        {
            Enabled = _enabled.ButtonPressed,
            Modes = modes,
            CellSize = _cellSize.Value,
            Center = VectorUx2NavI(_centerX, _centerY, _centerZ),
            Size = VectorUx2NavI(_sizeX, _sizeY, _sizeZ),
            AvoidanceRadius = _avoidance.Value,
            DebugVisible = _volumeDebug.ButtonPressed,
            Route = route,
        };
    }

    private void SaveSelectedVolume()
    {
        if (_draft is null || _selectedVolumeId is null)
        {
            return;
        }
        var index = _draft.Volumes.ToList().FindIndex(item => item.Id == _selectedVolumeId.Value);
        if (index < 0)
        {
            return;
        }
        var current = _draft.Volumes[index];
        var nameProtected = current.NameIsProtected &&
                            string.Equals(_volumeName.Text, ProtectedNameUx2NavI,
                                StringComparison.Ordinal);
        var updated = current with
        {
            Name = nameProtected ? current.Name : _volumeName.Text,
            NameIsProtected = nameProtected,
            Center = VectorUx2NavI(_volumeCenterX, _volumeCenterY, _volumeCenterZ),
            Size = VectorUx2NavI(_volumeSizeX, _volumeSizeY, _volumeSizeZ),
            CostMultiplier = current.Kind == StudioUx2NavIVolumeKind.Obstacle
                ? current.CostMultiplier : _volumeCost.Value,
        };
        var volumes = _draft.Volumes.ToArray();
        volumes[index] = updated;
        _draft = _draft with { Volumes = volumes };
    }

    private void SaveSelectedNode()
    {
        if (_draft?.Route is not { } route || _selectedNodeId is null)
        {
            return;
        }
        var index = route.Nodes.ToList().FindIndex(item => item.Id == _selectedNodeId.Value);
        if (index < 0)
        {
            return;
        }
        var nodes = route.Nodes.ToArray();
        var nameProtected = nodes[index].NameIsProtected &&
                            string.Equals(_nodeName.Text, ProtectedNameUx2NavI,
                                StringComparison.Ordinal);
        nodes[index] = nodes[index] with
        {
            Name = nameProtected ? nodes[index].Name : _nodeName.Text,
            NameIsProtected = nameProtected,
            Position = VectorUx2NavI(_nodeX, _nodeY, _nodeZ),
            Speed = _nodeSpeed.Value,
            BankDegrees = _nodeBank.Value,
        };
        _draft = _draft with { Route = route with { Nodes = nodes } };
    }

    private void DraftChanged()
    {
        if (_updating || _draft is null)
        {
            return;
        }
        SaveControlValuesIntoDraft();
        InvalidatePreview();
        SetStatus("Brouillon modifié · aperçu sec requis avant application.");
    }

    private void InvalidatePreview()
    {
        _session.CancelAerialNavigationUx2NavI();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        RenderPreview(null);
        UpdateActionState();
    }

    private void Preview()
    {
        if (_draft is null || !_explicitSelection)
        {
            SetStatus("Choisissez explicitement une zone avant l’aperçu.", true);
            return;
        }
        SaveControlValuesIntoDraft();
        _lastPreview = _session.PreviewAerialNavigationUx2NavI(_draft);
        _canonicalPreviewReady = _lastPreview.Succeeded;
        RenderPreview(_lastPreview);
        SetStatus(_lastPreview.Message, !_lastPreview.Succeeded);
        UpdateActionState();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedAerialNavigationUx2NavI();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _canonicalPreviewReady = false;
            UpdateActionState();
            return;
        }
        NavigationChanged?.Invoke();
        ReloadSelectedArea();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            NavigationChanged?.Invoke();
            ReloadSelectedArea();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            NavigationChanged?.Invoke();
            ReloadSelectedArea();
        }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
        UpdateActionState();
    }

    private void ReloadSelectedArea()
    {
        if (_area.Selected <= 0 || _area.Selected > _areaChoices.Count)
        {
            ClearSelection();
            return;
        }
        var areaId = _areaChoices[_area.Selected - 1].AreaId;
        var selectedRouteId = _draft?.Route?.RouteId;
        _draft = _session.CreateAerialDraftUx2NavI(areaId);
        if (_draft is null)
        {
            ClearSelection();
            return;
        }
        _routeChoices = _session.AerialRouteChoicesUx2NavI(areaId);
        _updating = true;
        _route.Clear();
        _route.AddItem("Choisir une route…");
        foreach (var choice in _routeChoices) _route.AddItem(choice.Label);
        var routeIndex = selectedRouteId is null
            ? -1 : _routeChoices.ToList().FindIndex(item => item.RouteId == selectedRouteId.Value);
        _route.Selected = routeIndex + 1;
        _updating = false;
        if (routeIndex >= 0)
        {
            _draft = _draft with
            {
                Route = _session.CreateAerialRouteDraftUx2NavI(areaId, selectedRouteId),
            };
        }
        _selectedVolumeId = null;
        _selectedNodeId = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _explicitSelection = true;
        LoadDraft(_draft);
        RenderPreview(null);
    }

    private void RenderPreview(StudioUx2NavIPreview? preview)
    {
        ClearChildrenUx2NavI(_diff);
        ClearChildrenUx2NavI(_diagnostics);
        ClearChildrenUx2NavI(_runtime);
        if (preview is null)
        {
            _diff.AddChild(StudioStyle.Label("Prévisualisez pour voir les changements.", 9,
                StudioStyle.Muted, wrap: true));
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic calculé.", 9,
                StudioStyle.Muted, wrap: true));
            _runtime.AddChild(StudioStyle.Label("Aucun service runtime exécuté.", 9,
                StudioStyle.Muted, wrap: true));
            _summary.Text = "Aucun aperçu calculé.";
            _boundary.Text =
                "Limite : aucun acteur, collision Godot, IA autonome, réseau ou préchargement n’est exécuté.";
            return;
        }
        foreach (var line in preview.Diff.DefaultIfEmpty("Aucune différence sémantique."))
        {
            _diff.AddChild(StudioStyle.Label("• " + line, 9, StudioStyle.Text, wrap: true));
        }
        foreach (var item in preview.Diagnostics)
        {
            _diagnostics.AddChild(StudioStyle.Label($"{item.Code}\n{item.Path}\n{item.Message}", 9,
                StudioStyle.Warning, wrap: true));
        }
        if (preview.Diagnostics.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label("✓ Profil et références valides.", 9,
                StudioStyle.Success, wrap: true));
        }
        if (preview.Runtime is { } runtime)
        {
            _runtime.AddChild(StudioStyle.Label(
                $"Grille {runtime.GridCells:N0} · bloquées {runtime.BlockedCells:N0} · coûts {runtime.CostCells:N0}",
                9, StudioStyle.Text, wrap: true));
            _runtime.AddChild(StudioStyle.Label(
                $"A* {runtime.PathPoints} points · {HumanDiagnostic(runtime.SteeringDiagnostic)}",
                9, StudioStyle.Text, wrap: true));
            _runtime.AddChild(StudioStyle.Label(
                $"Vol {HumanDiagnostic(runtime.FlightDiagnostic)} · vitesse {runtime.FlightSpeed:0.##} · banking {runtime.FlightBankDegrees:0.##}°",
                9, StudioStyle.Text, wrap: true));
            _boundary.Text = runtime.Boundary;
        }
        else
        {
            _runtime.AddChild(StudioStyle.Label("Aucun aperçu runtime disponible.", 9,
                StudioStyle.Muted, wrap: true));
        }
        _summary.Text = preview.Summary;
        if (preview.Succeeded)
        {
            _worldViewport.ShowNavigationOverlayUx2NavB(preview.Overlay);
        }
        else
        {
            _worldViewport.ClearNavigationOverlayUx2NavB();
        }
    }

    private void WireDraftSignals()
    {
        foreach (var check in new[] { _enabled, _flight, _swim, _space, _volumeDebug, _closed, _routeDebug })
        {
            check.Toggled += _ => DraftChanged();
        }
        foreach (var number in new[]
                 {
                     _cellSize, _centerX, _centerY, _centerZ, _sizeX, _sizeY, _sizeZ, _avoidance,
                     _volumeCenterX, _volumeCenterY, _volumeCenterZ, _volumeSizeX, _volumeSizeY,
                     _volumeSizeZ, _volumeCost, _nodeX, _nodeY, _nodeZ, _nodeSpeed, _nodeBank,
                 })
        {
            number.ValueChanged += _ => DraftChanged();
        }
        foreach (var line in new[] { _volumeName, _routeName, _nodeName })
        {
            line.TextChanged += _ => DraftChanged();
        }
        _curve.ItemSelected += _ => DraftChanged();
        _obstaclePolicy.ItemSelected += _ => DraftChanged();
    }

    private void SetEditorEnabled(bool enabled)
    {
        _editor.Visible = enabled;
        _route.Disabled = !enabled;
        _newRoute.Disabled = !enabled;
        _enabled.Disabled = !enabled;
        _flight.Disabled = !enabled;
        _swim.Disabled = !enabled;
        _space.Disabled = !enabled;
        _volumeDebug.Disabled = !enabled;
        _cellSize.Editable = enabled;
        _avoidance.Editable = enabled;
        foreach (var number in new[] { _centerX, _centerY, _centerZ, _sizeX, _sizeY, _sizeZ })
        {
            number.Editable = enabled;
        }
        _volume.Disabled = !enabled;
        _addObstacle.Disabled = !enabled;
        _addCost.Disabled = !enabled;
        _preview.Disabled = !enabled;
        UpdateActionState();
    }

    private void UpdateActionState()
    {
        _apply.Disabled = !_canonicalPreviewReady || _lastPreview?.CanApply != true;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null || !_session.IsDirty;
        _removeVolume.Disabled = _selectedVolumeId is null;
        _addNode.Disabled = _draft?.Route is null;
        _removeNode.Disabled = _selectedNodeId is null || _draft?.Route?.Nodes.Count <= 2;
        UpdateNodeMoveState();
    }

    private void UpdateNodeMoveState()
    {
        var route = _draft?.Route;
        var index = route is null || _selectedNodeId is null
            ? -1
            : route.Nodes.ToList().FindIndex(item => item.Id == _selectedNodeId.Value);
        _moveNodeUp.Disabled = index <= 0;
        _moveNodeDown.Disabled = route is null || index < 0 || index >= route.Nodes.Count - 1;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private StudioUx2NavIEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History!;
        return new StudioUx2NavIEvidenceSourceSnapshot(_sourceSession.Project!, history,
            history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureAerialApprovalUx2NavI());
    }

    private bool SourceMatchesSnapshot(StudioUx2NavIEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureAerialApprovalUx2NavI() == snapshot.Approval;
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
            _lastEvidenceResourcesReleased = StudioSessionController.DeleteUx2NavITemporaryWorkspace(root);
            _lastEvidenceSourceUntouched = _evidenceSourceSnapshot is null ||
                                           SourceMatchesSnapshot(_evidenceSourceSnapshot);
            _lastSourceRestored = true;
        }
    }

    private void BindViewportToArea(StudioSessionController session, AreaId areaId)
    {
        try
        {
            _worldViewport.RebuildArea(session, areaId, readOnly: true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_I_VIEWPORT_FALLBACK {exception.Message}");
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
            else
            {
                _worldViewport.ShowNoArea(_sourceSession);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_I_VIEWPORT_RESTORE_FAILED {exception.Message}");
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
        var width = Math.Clamp((viewport.X * .48f) / scale, 760f, 980f);
        var height = Math.Clamp((viewport.Y - 30f) / scale, 700f, 1_180f);
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2(viewport.X - width * scale - 15f,
            Math.Max(15f, (viewport.Y - height * scale) / 2f));
    }

    private static HBoxContainer RowUx2NavI()
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        return row;
    }

    private static SpinBox NumberUx2NavI()
    {
        var result = StudioUx2NavCControls.Number(-float.MaxValue, float.MaxValue, .25);
        result.AllowGreater = false;
        result.AllowLesser = false;
        return result;
    }

    private static SpinBox PositiveFloatNumberUx2NavI()
    {
        var result = StudioUx2NavCControls.Number(0, float.MaxValue, .25);
        result.AllowGreater = false;
        result.AllowLesser = false;
        return result;
    }

    private static Vector3Value VectorUx2NavI(SpinBox x, SpinBox y, SpinBox z) =>
        new((float)x.Value, (float)y.Value, (float)z.Value);

    private static float OffsetInsideUx2NavI(float value, float center, float size, float offset)
    {
        var minimum = center - size / 2;
        var maximum = center + size / 2;
        var plus = value + offset;
        if (plus < maximum)
        {
            return plus;
        }
        var minus = value - offset;
        return minus > minimum ? minus : center;
    }

    private static string HumanTextUx2NavI(string? text, string fallback)
    {
        var value = text?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }
        return value.Contains("UUID", StringComparison.OrdinalIgnoreCase) || ContainsUuidUx2NavI(value)
            ? fallback : value;
    }

    private static string HumanCurve(FlightRouteCurve value) => value switch
    {
        FlightRouteCurve.Linear => "Linéaire",
        FlightRouteCurve.CatmullRom => "Courbe douce",
        FlightRouteCurve.Bezier => "Transition lissée",
        _ => "Héritée",
    };

    private static string HumanObstaclePolicy(FlightObstaclePolicy value) => value switch
    {
        FlightObstaclePolicy.Avoid => "Contourner",
        FlightObstaclePolicy.Stop => "S’arrêter",
        FlightObstaclePolicy.Ignore => "Ignorer",
        _ => "Héritée",
    };

    private static string HumanDiagnostic(string value) => value switch
    {
        "area.volumetric.steering_ready" => "direction 3D prête",
        "area.volumetric.steering_blocked" => "arrêt avant cellule bloquée",
        "area.volumetric.crowd_avoided" => "séparation de foule appliquée",
        "area.volumetric.disabled" => "navigation désactivée",
        "waypoint.flight.following" => "route suivie",
        "waypoint.flight.obstacle_avoided" => "obstacle contourné",
        "waypoint.flight.obstacle_stopped" => "arrêt devant obstacle",
        "waypoint.flight.obstacle_ignored" => "obstacle ignoré",
        "waypoint.flight.completed" => "route terminée",
        "waypoint.flight.not_selected" => "aucune route sélectionnée",
        _ => "diagnostic runtime disponible",
    };

    private static void ClearChildrenUx2NavI(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.Free();
        }
    }

    private static IEnumerable<Label> DescendantLabelsUx2NavI(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label)
            {
                yield return label;
            }
            foreach (var descendant in DescendantLabelsUx2NavI(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool ContainsUuidUx2NavI(string text) =>
        System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
