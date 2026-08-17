using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Navigation;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioWaypointLocatorOverlayUx2NavE : Control
{
    private static readonly LocatorPurpose[] Purposes = Enum.GetValues<LocatorPurpose>();
    private static readonly LocatorSelectionMode[] SelectionModes =
        Enum.GetValues<LocatorSelectionMode>();

    private readonly StudioSessionController _sourceSession;
    private readonly StudioWorldViewport _worldViewport;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx2NavEEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _lastSourceRestored = true;
    private bool _lastUiTransaction;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly OptionButton _locator;
    private readonly Button _newLocator;
    private readonly OptionButton _collection;
    private readonly Button _newCollection;
    private readonly Label _selection;
    private readonly VBoxContainer _editor;

    private readonly LineEdit _locatorName;
    private readonly OptionButton _locatorPurpose;
    private readonly SpinBox _locatorX;
    private readonly SpinBox _locatorY;
    private readonly SpinBox _locatorZ;
    private readonly LineEdit _locatorTags;
    private readonly LineEdit _locatorAsset;
    private readonly OptionButton _locatorBlueprint;
    private readonly CheckBox _locatorRequired;
    private readonly Label _arrivalProtection;
    private readonly ItemList _overrideList;
    private readonly LineEdit _overrideKey;
    private readonly LineEdit _overrideValue;
    private readonly Button _saveOverride;
    private readonly Button _removeOverride;
    private readonly Button _moveOverrideUp;
    private readonly Button _moveOverrideDown;

    private readonly LineEdit _collectionName;
    private readonly LineEdit _collectionQuery;
    private readonly LineEdit _collectionTags;
    private readonly OptionButton _collectionPurpose;
    private readonly OptionButton _collectionSelection;
    private readonly ItemList _memberList;
    private readonly OptionButton _memberCandidate;
    private readonly Button _addMember;
    private readonly Button _removeMember;
    private readonly Button _moveMemberUp;
    private readonly Button _moveMemberDown;
    private readonly CheckBox _collectionHasOrigin;
    private readonly HBoxContainer _originFields;
    private readonly SpinBox _originX;
    private readonly SpinBox _originY;
    private readonly SpinBox _originZ;
    private readonly SpinBox _collectionSeed;
    private readonly CheckBox _collectionRequired;

    private readonly CheckBox _showSegments;
    private readonly CheckBox _showDirection;
    private readonly CheckBox _showCosts;
    private readonly CheckBox _showActions;
    private readonly CheckBox _showBroken;
    private readonly CheckBox _showProjection;
    private readonly CheckBox _showTiming;
    private readonly SpinBox _costPerMeter;
    private readonly CheckBox _visualRequired;

    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly VBoxContainer _resolved;
    private readonly VBoxContainer _routes;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx2NavEAreaChoice> _areaChoices = [];
    private IReadOnlyList<StudioUx2NavELocatorChoice> _locatorChoices = [];
    private IReadOnlyList<StudioUx2NavECollectionChoice> _collectionChoices = [];
    private IReadOnlyList<StudioUx2NavEBlueprintChoice> _blueprintChoices = [];
    private IReadOnlyList<StudioUx2NavELocatorChoice> _memberChoices = [];
    private readonly List<StudioUx2NavEOverrideDraft> _overrides = [];
    private readonly List<Guid> _memberIds = [];
    private StudioUx2NavELocatorDraft? _locatorDraft;
    private StudioUx2NavECollectionDraft? _collectionDraft;
    private StudioUx2NavEVisualizationDraft? _visualizationDraft;
    private StudioUx2NavEDraft? _draft;
    private StudioUx2NavEPreview? _lastPreview;
    private bool _updating;
    private bool _explicitSelection;
    private bool _canonicalPreviewReady;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? NavigationChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _areaChoices.Count;
    internal bool HasExplicitSelection => _draft is not null && _explicitSelection &&
                                          _area.Selected > 0 &&
                                          (_locator.Selected > 0 || _locatorDraft?.IsNew == true ||
                                           _collection.Selected > 0 ||
                                           _collectionDraft?.IsNew == true);
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
            var text = string.Join(' ', DescendantLabelsUx2NavE(_dialog).Select(item => item.Text));
            return HasExplicitSelection &&
                   text.Contains("Ancres audio", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Groupes", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Visualisation", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx2NavE(text);
        }
    }

    internal StudioWaypointLocatorOverlayUx2NavE(StudioSessionController session,
        StudioWorldViewport worldViewport)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        _worldViewport = worldViewport ?? throw new ArgumentNullException(nameof(worldViewport));
        Name = "StudioWaypointLocatorOverlayUx2NavE";
        Visible = false;
        ZIndex = 458;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _dialog = StudioStyle.Panel("WaypointLocatorDialogUx2NavE", StudioStyle.Chrome, 12, 2);
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
        heading.AddChild(StudioStyle.Label("NAVIGATION · ANCRES & DIAGNOSTICS", 10,
            StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Ancres audio/VFX, groupes et routes", 19));
        heading.AddChild(StudioStyle.Label(
            "WPT-014 / 015 / 016 · ressources typées, requêtes déterministes et couches de route. " +
            "La projection provient de la surface canonique ; aucun acteur runtime n’est déclenché.",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var selectors = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selectors.AddThemeConstantOverride("separation", 6);
        root.AddChild(selectors);
        selectors.AddChild(StudioStyle.Label("Zone", 10, StudioStyle.Muted));
        _area = StudioUx2NavCControls.Options();
        _area.ItemSelected += SelectArea;
        selectors.AddChild(_area);
        selectors.AddChild(StudioStyle.Label("Ancre", 10, StudioStyle.Muted));
        _locator = StudioUx2NavCControls.Options();
        _locator.ItemSelected += SelectLocator;
        selectors.AddChild(_locator);
        _newLocator = StudioStyle.Button("Nouvelle ancre", compact: true);
        _newLocator.Pressed += AddLocator;
        selectors.AddChild(_newLocator);
        selectors.AddChild(StudioStyle.Label("Groupe", 10, StudioStyle.Muted));
        _collection = StudioUx2NavCControls.Options();
        _collection.ItemSelected += SelectCollection;
        selectors.AddChild(_collection);
        _newCollection = StudioStyle.Button("Nouveau groupe", compact: true);
        _newCollection.Pressed += AddCollection;
        selectors.AddChild(_newCollection);
        _selection = StudioStyle.Label(
            "Choisissez explicitement une zone puis une ancre ou un groupe ; aucune fiche n’est chargée automatiquement.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var bodyHost = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 430),
            CustomMaximumSize = new Vector2(-1, 700),
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

        var authoringRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        authoringRow.AddThemeConstantOverride("separation", 8);
        _editor.AddChild(authoringRow);

        var locatorCard = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        locatorCard.AddThemeConstantOverride("separation", 5);
        _locatorName = StudioUx2NavCControls.Text("Nom humain de l’ancre");
        _locatorPurpose = StudioUx2NavCControls.Options(Purposes.Select(HumanPurpose).ToArray());
        var locatorIdentity = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        locatorIdentity.AddThemeConstantOverride("separation", 5);
        locatorIdentity.AddChild(StudioUx2NavCControls.Field("Nom", _locatorName, 180));
        locatorIdentity.AddChild(StudioUx2NavCControls.Field("Rôle", _locatorPurpose));
        locatorCard.AddChild(locatorIdentity);
        var locatorPosition = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        locatorPosition.AddThemeConstantOverride("separation", 5);
        _locatorX = StudioUx2NavCControls.Number(-100_000, 100_000, .25);
        _locatorY = StudioUx2NavCControls.Number(-100_000, 100_000, .25);
        _locatorZ = StudioUx2NavCControls.Number(-100_000, 100_000, .25);
        locatorPosition.AddChild(StudioUx2NavCControls.Field("Position X", _locatorX));
        locatorPosition.AddChild(StudioUx2NavCControls.Field("Altitude Y", _locatorY));
        locatorPosition.AddChild(StudioUx2NavCControls.Field("Position Z", _locatorZ));
        locatorCard.AddChild(locatorPosition);
        _locatorTags = StudioUx2NavCControls.Text("rituel, ambiance");
        _locatorAsset = StudioUx2NavCControls.Text("audio:cloche ou vfx:halo");
        locatorCard.AddChild(StudioUx2NavCControls.Field("Tags ordonnés", _locatorTags));
        locatorCard.AddChild(StudioUx2NavCControls.Field("Ressource nommée", _locatorAsset));
        _locatorBlueprint = StudioUx2NavCControls.Options();
        locatorCard.AddChild(StudioUx2NavCControls.Field("Blueprint humain (facultatif)",
            _locatorBlueprint));
        _locatorRequired = StudioUx2NavCControls.Check("Ancre exigée au runtime");
        locatorCard.AddChild(_locatorRequired);
        _arrivalProtection = StudioStyle.Label(
            "Comportement d’arrivée NAV-D protégé et préservé.", 9, StudioStyle.Muted, wrap: true);
        locatorCard.AddChild(_arrivalProtection);

        _overrideList = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 84),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _overrideList.ItemSelected += SelectOverride;
        locatorCard.AddChild(StudioUx2NavCControls.Field("Overrides explicites", _overrideList));
        var overrideEdit = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        overrideEdit.AddThemeConstantOverride("separation", 4);
        _overrideKey = StudioUx2NavCControls.Text("clé humaine");
        _overrideValue = StudioUx2NavCControls.Text("valeur");
        overrideEdit.AddChild(StudioUx2NavCControls.Field("Clé", _overrideKey));
        overrideEdit.AddChild(StudioUx2NavCControls.Field("Valeur", _overrideValue));
        _saveOverride = StudioStyle.Button("Ajouter / mettre à jour", compact: true);
        _saveOverride.Pressed += SaveOverride;
        overrideEdit.AddChild(_saveOverride);
        locatorCard.AddChild(overrideEdit);
        var overrideActions = new HBoxContainer();
        overrideActions.AddThemeConstantOverride("separation", 4);
        _removeOverride = StudioStyle.Button("Retirer", compact: true);
        _removeOverride.Pressed += RemoveOverride;
        overrideActions.AddChild(_removeOverride);
        _moveOverrideUp = StudioStyle.Button("Monter", compact: true);
        _moveOverrideUp.Pressed += MoveOverrideUp;
        overrideActions.AddChild(_moveOverrideUp);
        _moveOverrideDown = StudioStyle.Button("Descendre", compact: true);
        _moveOverrideDown.Pressed += MoveOverrideDown;
        overrideActions.AddChild(_moveOverrideDown);
        locatorCard.AddChild(overrideActions);
        authoringRow.AddChild(ExpandedCard("WaypointLocatorUx2NavE",
            "Ancre audio, VFX ou gameplay",
            "Position, rôle, tags, ressource, blueprint lisible et overrides ; l’arrivée voisine reste intacte.",
            locatorCard));

        var collectionCard = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        collectionCard.AddThemeConstantOverride("separation", 5);
        _collectionName = StudioUx2NavCControls.Text("Nom humain du groupe");
        _collectionQuery = StudioUx2NavCControls.Text("Recherche dans noms et tags");
        collectionCard.AddChild(StudioUx2NavCControls.Field("Nom", _collectionName));
        collectionCard.AddChild(StudioUx2NavCControls.Field("Requête", _collectionQuery));
        var collectionPolicy = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        collectionPolicy.AddThemeConstantOverride("separation", 5);
        _collectionPurpose = StudioUx2NavCControls.Options(
            new[] { "Tous les rôles" }.Concat(Purposes.Select(HumanPurpose)).ToArray());
        _collectionSelection = StudioUx2NavCControls.Options(
            SelectionModes.Select(HumanSelection).ToArray());
        collectionPolicy.AddChild(StudioUx2NavCControls.Field("Filtre de rôle", _collectionPurpose));
        collectionPolicy.AddChild(StudioUx2NavCControls.Field("Résolution", _collectionSelection));
        collectionCard.AddChild(collectionPolicy);
        _collectionTags = StudioUx2NavCControls.Text("Tags tous requis");
        collectionCard.AddChild(StudioUx2NavCControls.Field("Tags requis", _collectionTags));
        _memberList = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 110),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _memberList.ItemSelected += _ => UpdateMemberActionState();
        collectionCard.AddChild(StudioUx2NavCControls.Field("Membres ordonnés", _memberList));
        _memberCandidate = StudioUx2NavCControls.Options();
        collectionCard.AddChild(StudioUx2NavCControls.Field("Ancre à ajouter", _memberCandidate));
        var memberActions = new HBoxContainer();
        memberActions.AddThemeConstantOverride("separation", 4);
        _addMember = StudioStyle.Button("Ajouter", compact: true);
        _addMember.Pressed += AddMember;
        memberActions.AddChild(_addMember);
        _removeMember = StudioStyle.Button("Retirer", compact: true);
        _removeMember.Pressed += RemoveMember;
        memberActions.AddChild(_removeMember);
        _moveMemberUp = StudioStyle.Button("Monter", compact: true);
        _moveMemberUp.Pressed += MoveMemberUp;
        memberActions.AddChild(_moveMemberUp);
        _moveMemberDown = StudioStyle.Button("Descendre", compact: true);
        _moveMemberDown.Pressed += MoveMemberDown;
        memberActions.AddChild(_moveMemberDown);
        collectionCard.AddChild(memberActions);
        _collectionHasOrigin = StudioUx2NavCControls.Check("Définir un point d’origine");
        collectionCard.AddChild(_collectionHasOrigin);
        _originFields = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _originFields.AddThemeConstantOverride("separation", 5);
        _originX = StudioUx2NavCControls.Number(-100_000, 100_000, .25);
        _originY = StudioUx2NavCControls.Number(-100_000, 100_000, .25);
        _originZ = StudioUx2NavCControls.Number(-100_000, 100_000, .25);
        _originFields.AddChild(StudioUx2NavCControls.Field("Origine X", _originX));
        _originFields.AddChild(StudioUx2NavCControls.Field("Origine Y", _originY));
        _originFields.AddChild(StudioUx2NavCControls.Field("Origine Z", _originZ));
        collectionCard.AddChild(_originFields);
        _collectionSeed = StudioUx2NavCControls.Number(int.MinValue, int.MaxValue, 1);
        collectionCard.AddChild(StudioUx2NavCControls.Field("Graine déterministe", _collectionSeed));
        _collectionRequired = StudioUx2NavCControls.Check("Groupe exigé au runtime");
        collectionCard.AddChild(_collectionRequired);
        authoringRow.AddChild(ExpandedCard("WaypointCollectionUx2NavE",
            "Groupes et requêtes",
            "Membres humains ordonnés, filtres et résolution stable, proche ou aléatoire déterministe.",
            collectionCard));

        var visualization = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        visualization.AddThemeConstantOverride("separation", 5);
        var layers = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _showSegments = StudioUx2NavCControls.Check("Segments");
        _showDirection = StudioUx2NavCControls.Check("Sens");
        _showCosts = StudioUx2NavCControls.Check("Coûts");
        _showActions = StudioUx2NavCControls.Check("Actions");
        _showBroken = StudioUx2NavCControls.Check("Liens cassés");
        _showProjection = StudioUx2NavCControls.Check("Projection navigation");
        _showTiming = StudioUx2NavCControls.Check("Temps");
        layers.AddChild(_showSegments);
        layers.AddChild(_showDirection);
        layers.AddChild(_showCosts);
        layers.AddChild(_showActions);
        layers.AddChild(_showBroken);
        layers.AddChild(_showProjection);
        layers.AddChild(_showTiming);
        visualization.AddChild(layers);
        var visualPolicy = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _costPerMeter = StudioUx2NavCControls.Number(-100_000, 100_000, .05);
        visualPolicy.AddChild(StudioUx2NavCControls.Field("Coût par mètre", _costPerMeter));
        _visualRequired = StudioUx2NavCControls.Check("Diagnostic de route exigé au runtime");
        visualPolicy.AddChild(_visualRequired);
        visualization.AddChild(visualPolicy);
        _editor.AddChild(ExpandedCard("RouteVisualizationUx2NavE",
            "Visualisation des routes",
            "Le viewport réutilise la surface NAV-B et montre seulement les couches demandées.",
            visualization));

        var resultRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        resultRow.AddThemeConstantOverride("separation", 8);
        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _resolved = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _routes = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        resultRow.AddChild(ExpandedCard("WaypointLocatorDiffUx2NavE",
            "Diff sémantique", "Ce que remplacera la commande atomique.", _diff));
        resultRow.AddChild(ExpandedCard("WaypointLocatorDiagnosticsUx2NavE",
            "Diagnostics", "Codes stables et chemins humains avant application.", _diagnostics));
        resultRow.AddChild(ExpandedCard("WaypointCollectionRuntimeUx2NavE",
            "Résolution du groupe", "Résultats du service canonique, sans mutation.", _resolved));
        resultRow.AddChild(ExpandedCard("WaypointRouteRuntimeUx2NavE",
            "Routes & projection", "Coûts, temps et projection calculés sur la surface réelle.", _routes));
        _editor.AddChild(resultRow);
        _summary = StudioStyle.Label("Aucun aperçu calculé.", 10, StudioStyle.Muted, wrap: true);
        _editor.AddChild(_summary);

        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 6);
        root.AddChild(footer);
        _status = StudioStyle.Label("Sélection explicite requise.", 10, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(_status);
        _preview = StudioStyle.Button("Aperçu sec", compact: true);
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
            MoveMemberUp();
            GetViewport().SetInputAsHandled();
        }
        else if (key.AltPressed && key.Keycode == Key.Down)
        {
            MoveMemberDown();
            GetViewport().SetInputAsHandled();
        }
    }

    internal void Open()
    {
        ReleaseEvidenceWorkspace();
        _session = _sourceSession;
        _sourceSession.CancelLocatorsUx2NavE();
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;
        PopulateAreas();
        ClearSelection();
        UpdateDialogBounds();
        SetStatus("Choisissez explicitement une zone puis une ancre ou un groupe.");
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null)
        {
            return;
        }
        if (_evidenceSession is not null)
            _evidenceSession.CancelLocatorsUx2NavE();
        else
            _sourceSession.CancelLocatorsUx2NavE();
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _locatorDraft = null;
        _collectionDraft = null;
        _visualizationDraft = null;
        _draft = null;
        _lastPreview = null;
        _overrides.Clear();
        _memberIds.Clear();
        _canonicalPreviewReady = false;
        _explicitSelection = false;
        ReleaseEvidenceWorkspace();
        RestoreSourceViewport();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!string.Equals(mode, "ux2-nav-e", StringComparison.OrdinalIgnoreCase) ||
            _sourceSession.Project is null || _sourceSession.History is null)
        {
            return false;
        }
        ReleaseEvidenceWorkspace();
        _evidenceSourceSnapshot = CaptureSourceSnapshot();
        _lastEvidenceSourceUntouched = true;
        _lastEvidenceResourcesReleased = false;
        _lastSourceRestored = false;
        var project = _sourceSession.CreateUx2NavEEvidenceProject();
        if (project is null)
        {
            return false;
        }
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-e-evidence-" + Guid.NewGuid().ToString("N"));
        try
        {
            _evidenceTempRoot = tempRoot;
            var created = ProjectSession.Create(tempRoot, project);
            if (!created.Succeeded)
            {
                _lastEvidenceResourcesReleased =
                    StudioSessionController.DeleteUx2NavETemporaryWorkspace(tempRoot);
                if (_lastEvidenceResourcesReleased) _evidenceTempRoot = null;
                return false;
            }
            var evidence = new StudioSessionController();
            if (!evidence.Open(tempRoot).Succeeded)
            {
                evidence.Close();
                _lastEvidenceResourcesReleased =
                    StudioSessionController.DeleteUx2NavETemporaryWorkspace(tempRoot);
                if (_lastEvidenceResourcesReleased) _evidenceTempRoot = null;
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
            var locatorIndex = _locatorChoices.ToList().FindIndex(item => item.IsEvidence);
            var collectionIndex = _collectionChoices.ToList().FindIndex(item => item.IsEvidence);
            if (locatorIndex < 0 || collectionIndex < 0)
            {
                Close();
                return false;
            }
            _locator.Selected = locatorIndex + 1;
            SelectLocator(locatorIndex + 1);
            _collection.Selected = collectionIndex + 1;
            SelectCollection(collectionIndex + 1);
            _locatorAsset.Text = "vfx:sanctuary-halo-preview";
            _collectionSelection.Selected = Array.IndexOf(SelectionModes,
                LocatorSelectionMode.RandomDeterministic);
            _collectionSeed.Value = 4242;
            _showTiming.ButtonPressed = true;
            _costPerMeter.Value = 2.5;
            DraftChanged();
            Preview();
            UpdateDialogBounds();
            return HasExplicitSelection && _canonicalPreviewReady && ShowsViewportOverlay &&
                   UsesIsolatedEvidence && EvidenceSourceUntouched && ShowsHumanSurface;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or InvalidOperationException)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_E_EVIDENCE_FAILED type={exception.GetType().Name} message={exception.Message}");
            Close();
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        _lastUiTransaction = false;
        try
        {
            if (!PrepareEvidence("ux2-nav-e"))
            {
                Close();
                return false;
            }
            var beforeOrder = _memberIds.ToArray();
            if (_memberList.ItemCount > 1)
            {
                _memberList.Select(0);
                _moveMemberDown.EmitSignal(Button.SignalName.Pressed);
            }
            var movedThroughUi = beforeOrder.Length > 1 && beforeOrder.Length == _memberIds.Count &&
                                 !beforeOrder.SequenceEqual(_memberIds) &&
                                 _memberIds[1] == beforeOrder[0];
            _preview.EmitSignal(Button.SignalName.Pressed);
            var dryRun = _canonicalPreviewReady && !_apply.Disabled;
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
            _lastUiTransaction = movedThroughUi && dryRun && applied && undone && redone && saved;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX2_NAV_E_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
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
        var approval = _sourceSession.CaptureLocatorApprovalUx2NavE();
        Open();
        _sourceSession.RestoreLocatorApprovalUx2NavE(approval);
    }

    private void PopulateAreas()
    {
        _updating = true;
        _area.Clear();
        _area.AddItem("Choisir une zone…");
        _areaChoices = _session.LocatorAreaChoicesUx2NavE;
        foreach (var choice in _areaChoices)
            _area.AddItem(choice.Label);
        _area.Selected = 0;
        _updating = false;
    }

    private void SelectArea(long index)
    {
        if (_updating)
        {
            return;
        }
        _session.CancelLocatorsUx2NavE();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _explicitSelection = index > 0 && index <= _areaChoices.Count;
        _locatorDraft = null;
        _collectionDraft = null;
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _apply.Disabled = true;
        if (!_explicitSelection)
        {
            ClearSelection();
            return;
        }
        var areaId = _areaChoices[(int)index - 1].AreaId;
        _visualizationDraft = _session.CreateRouteVisualizationDraftUx2NavE(areaId);
        PopulateItemChoices(areaId);
        LoadVisualization();
        BindViewportToArea(_session, areaId);
        _selection.Text = "Zone choisie · sélectionnez explicitement une ancre ou un groupe.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        SetEditorEnabled(true);
        UpdateActionState();
    }

    private void PopulateItemChoices(AreaId areaId)
    {
        _updating = true;
        _locator.Clear();
        _locator.AddItem("Choisir une ancre…");
        _locatorChoices = _session.LocatorChoicesUx2NavE(areaId);
        foreach (var choice in _locatorChoices) _locator.AddItem(choice.Label);
        _locator.Selected = 0;
        _collection.Clear();
        _collection.AddItem("Choisir un groupe…");
        _collectionChoices = _session.LocatorCollectionChoicesUx2NavE(areaId);
        foreach (var choice in _collectionChoices) _collection.AddItem(choice.Label);
        _collection.Selected = 0;
        _memberChoices = _locatorChoices;
        PopulateMemberCandidates();
        _updating = false;
    }

    private void SelectLocator(long index)
    {
        if (_updating || _area.Selected <= 0)
        {
            return;
        }
        var areaId = SelectedAreaId();
        _locatorDraft = index > 0 && index <= _locatorChoices.Count
            ? _session.CreateLocatorDraftUx2NavE(areaId,
                _locatorChoices[(int)index - 1].LocatorId)
            : null;
        if (_locatorDraft is null)
            ClearLocatorFields();
        else
            LoadLocator(_locatorDraft);
        DraftChanged();
    }

    private void AddLocator()
    {
        if (_area.Selected <= 0)
        {
            return;
        }
        _updating = true;
        _locator.Selected = 0;
        _updating = false;
        _locatorDraft = _session.CreateNewLocatorDraftUx2NavE(SelectedAreaId());
        if (_locatorDraft is not null)
        {
            LoadLocator(_locatorDraft);
            _selection.Text = "Nouvelle ancre · identité créée seulement lors de l’application.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
        }
        DraftChanged();
    }

    private void LoadLocator(StudioUx2NavELocatorDraft draft)
    {
        _updating = true;
        var protectedName = ContainsUuidUx2NavE(draft.Name);
        var protectedTags = draft.Tags.Any(ContainsUuidUx2NavE);
        var protectedAsset = ContainsUuidUx2NavE(draft.AssetId);
        _locatorName.Editable = !protectedName;
        _locatorName.Text = protectedName ? "Nom existant protégé" : draft.Name;
        _locatorPurpose.Selected = Array.IndexOf(Purposes, draft.Purpose);
        _locatorX.Value = draft.Position.X;
        _locatorY.Value = draft.Position.Y;
        _locatorZ.Value = draft.Position.Z;
        _locatorTags.Editable = !protectedTags;
        _locatorTags.Text = protectedTags ? "Tags existants protégés" : string.Join(", ", draft.Tags);
        _locatorAsset.Editable = !protectedAsset;
        _locatorAsset.Text = protectedAsset ? "Ressource existante protégée" : draft.AssetId;
        _locatorRequired.ButtonPressed = draft.RequiredAtRuntime;
        _overrides.Clear();
        _overrides.AddRange(draft.ExplicitOverrides);
        RefreshOverrides();
        _blueprintChoices = _session.LocatorBlueprintChoicesUx2NavE(draft);
        _locatorBlueprint.Clear();
        foreach (var choice in _blueprintChoices) _locatorBlueprint.AddItem(choice.Label);
        var blueprintIndex = _blueprintChoices.ToList().FindIndex(item =>
            item.BlueprintId == draft.BlueprintId && item.IsProtected == draft.BlueprintIsProtected);
        _locatorBlueprint.Selected = Math.Max(0, blueprintIndex);
        _arrivalProtection.Text = draft.Original?.Arrival is null
            ? "Aucune arrivée existante · le lot NAV-E n’en crée pas."
            : "Arrivée existante protégée · orientation, animation, interaction et callback préservés.";
        _updating = false;
    }

    private void ClearLocatorFields()
    {
        _updating = true;
        _locatorName.Editable = true;
        _locatorTags.Editable = true;
        _locatorAsset.Editable = true;
        _locatorName.Text = string.Empty;
        _locatorTags.Text = string.Empty;
        _locatorAsset.Text = string.Empty;
        _overrides.Clear();
        RefreshOverrides();
        _blueprintChoices = [];
        _locatorBlueprint.Clear();
        _locatorBlueprint.AddItem("Aucun blueprint");
        _arrivalProtection.Text = "Sélectionnez une ancre pour voir les champs protégés.";
        _updating = false;
    }

    private void SelectCollection(long index)
    {
        if (_updating || _area.Selected <= 0)
        {
            return;
        }
        var areaId = SelectedAreaId();
        _collectionDraft = index > 0 && index <= _collectionChoices.Count
            ? _session.CreateLocatorCollectionDraftUx2NavE(areaId,
                _collectionChoices[(int)index - 1].CollectionId)
            : null;
        if (_collectionDraft is null)
            ClearCollectionFields();
        else
            LoadCollection(_collectionDraft);
        DraftChanged();
    }

    private void AddCollection()
    {
        if (_area.Selected <= 0)
        {
            return;
        }
        _updating = true;
        _collection.Selected = 0;
        _updating = false;
        _collectionDraft = _session.CreateNewLocatorCollectionDraftUx2NavE(SelectedAreaId());
        if (_collectionDraft is not null)
        {
            LoadCollection(_collectionDraft);
            _selection.Text = "Nouveau groupe · identité créée seulement lors de l’application.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
        }
        DraftChanged();
    }

    private void LoadCollection(StudioUx2NavECollectionDraft draft)
    {
        _updating = true;
        var protectedName = ContainsUuidUx2NavE(draft.Name);
        var protectedQuery = ContainsUuidUx2NavE(draft.Query);
        var protectedTags = draft.RequiredTags.Any(ContainsUuidUx2NavE);
        _collectionName.Editable = !protectedName;
        _collectionName.Text = protectedName ? "Groupe existant protégé" : draft.Name;
        _collectionQuery.Editable = !protectedQuery;
        _collectionQuery.Text = protectedQuery ? "Requête existante protégée" : draft.Query;
        _collectionTags.Editable = !protectedTags;
        _collectionTags.Text = protectedTags ? "Tags existants protégés" :
            string.Join(", ", draft.RequiredTags);
        _collectionPurpose.Selected = draft.Purpose is null
            ? 0 : Array.IndexOf(Purposes, draft.Purpose.Value) + 1;
        _collectionSelection.Selected = Array.IndexOf(SelectionModes, draft.Selection);
        _memberIds.Clear();
        _memberIds.AddRange(draft.LocatorIds);
        RefreshMembers();
        _collectionHasOrigin.ButtonPressed = draft.HasOrigin;
        _originX.Value = draft.Origin.X;
        _originY.Value = draft.Origin.Y;
        _originZ.Value = draft.Origin.Z;
        _collectionSeed.Value = draft.Seed;
        _collectionRequired.ButtonPressed = draft.RequiredAtRuntime;
        _originFields.Visible = draft.HasOrigin;
        _updating = false;
    }

    private void ClearCollectionFields()
    {
        _updating = true;
        _collectionName.Editable = true;
        _collectionQuery.Editable = true;
        _collectionTags.Editable = true;
        _collectionName.Text = string.Empty;
        _collectionQuery.Text = string.Empty;
        _collectionTags.Text = string.Empty;
        _memberIds.Clear();
        RefreshMembers();
        _collectionHasOrigin.ButtonPressed = false;
        _originFields.Visible = false;
        _updating = false;
    }

    private void LoadVisualization()
    {
        if (_visualizationDraft is null)
        {
            return;
        }
        _updating = true;
        _showSegments.ButtonPressed = _visualizationDraft.ShowSegments;
        _showDirection.ButtonPressed = _visualizationDraft.ShowDirection;
        _showCosts.ButtonPressed = _visualizationDraft.ShowCosts;
        _showActions.ButtonPressed = _visualizationDraft.ShowActions;
        _showBroken.ButtonPressed = _visualizationDraft.ShowBrokenLinks;
        _showProjection.ButtonPressed = _visualizationDraft.ShowNavigationProjection;
        _showTiming.ButtonPressed = _visualizationDraft.ShowTiming;
        _costPerMeter.Value = _visualizationDraft.CostPerMeter;
        _visualRequired.ButtonPressed = _visualizationDraft.RequiredAtRuntime;
        _updating = false;
    }

    private StudioUx2NavELocatorDraft? CollectLocatorDraft()
    {
        if (_locatorDraft is null)
        {
            return null;
        }
        var blueprintIndex = _locatorBlueprint.Selected;
        var blueprint = blueprintIndex >= 0 && blueprintIndex < _blueprintChoices.Count
            ? _blueprintChoices[blueprintIndex] : null;
        return _locatorDraft with
        {
            Name = _locatorDraft.Original is { } originalName && ContainsUuidUx2NavE(originalName.Name)
                ? originalName.Name : _locatorName.Text,
            Position = new Vector3Value((float)_locatorX.Value, (float)_locatorY.Value,
                (float)_locatorZ.Value),
            Purpose = Purposes[Math.Clamp(_locatorPurpose.Selected, 0, Purposes.Length - 1)],
            Tags = _locatorDraft.Original is { } originalTags &&
                   originalTags.Tags.Any(ContainsUuidUx2NavE)
                ? originalTags.Tags : ParseList(_locatorTags.Text),
            AssetId = _locatorDraft.Original is { } originalAsset &&
                      ContainsUuidUx2NavE(originalAsset.AssetId ?? string.Empty)
                ? originalAsset.AssetId ?? string.Empty : _locatorAsset.Text,
            BlueprintId = blueprint?.BlueprintId,
            BlueprintIsProtected = blueprint?.IsProtected == true,
            ExplicitOverrides = _overrides.ToArray(),
            RequiredAtRuntime = _locatorRequired.ButtonPressed,
        };
    }

    private StudioUx2NavECollectionDraft? CollectCollectionDraft()
    {
        if (_collectionDraft is null)
        {
            return null;
        }
        LocatorPurpose? purpose = _collectionPurpose.Selected <= 0 ? null :
            Purposes[Math.Clamp(_collectionPurpose.Selected - 1, 0, Purposes.Length - 1)];
        return _collectionDraft with
        {
            Name = _collectionDraft.Original is { } originalName &&
                   ContainsUuidUx2NavE(originalName.Name)
                ? originalName.Name : _collectionName.Text,
            Query = _collectionDraft.Original is { } originalQuery &&
                    ContainsUuidUx2NavE(originalQuery.Query)
                ? originalQuery.Query : _collectionQuery.Text,
            LocatorIds = _memberIds.ToArray(),
            Purpose = purpose,
            RequiredTags = _collectionDraft.Original is { } originalTags &&
                           (originalTags.RequiredTags ?? []).Any(ContainsUuidUx2NavE)
                ? originalTags.RequiredTags ?? [] : ParseList(_collectionTags.Text),
            Selection = SelectionModes[Math.Clamp(_collectionSelection.Selected, 0,
                SelectionModes.Length - 1)],
            HasOrigin = _collectionHasOrigin.ButtonPressed,
            Origin = new Vector3Value((float)_originX.Value, (float)_originY.Value,
                (float)_originZ.Value),
            Seed = (int)_collectionSeed.Value,
            RequiredAtRuntime = _collectionRequired.ButtonPressed,
        };
    }

    private StudioUx2NavEVisualizationDraft? CollectVisualizationDraft() =>
        _visualizationDraft is null ? null : _visualizationDraft with
        {
            ShowSegments = _showSegments.ButtonPressed,
            ShowDirection = _showDirection.ButtonPressed,
            ShowCosts = _showCosts.ButtonPressed,
            ShowActions = _showActions.ButtonPressed,
            ShowBrokenLinks = _showBroken.ButtonPressed,
            ShowNavigationProjection = _showProjection.ButtonPressed,
            ShowTiming = _showTiming.ButtonPressed,
            CostPerMeter = _costPerMeter.Value,
            RequiredAtRuntime = _visualRequired.ButtonPressed,
        };

    private void DraftChanged()
    {
        if (_updating || _area.Selected <= 0)
        {
            return;
        }
        _session.CancelLocatorsUx2NavE();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _locatorDraft = CollectLocatorDraft();
        _collectionDraft = CollectCollectionDraft();
        _visualizationDraft = CollectVisualizationDraft();
        _draft = _session.ComposeLocatorDraftUx2NavE(SelectedAreaId(), _locatorDraft,
            _collectionDraft, _visualizationDraft);
        _lastPreview = null;
        _canonicalPreviewReady = false;
        _apply.Disabled = true;
        _summary.Text = "Brouillon modifié · recalculez l’aperçu sec.";
        RenderPreview(null);
        UpdateActionState();
    }

    private void Preview()
    {
        DraftChanged();
        if (_draft is null || !HasExplicitSelection)
        {
            SetStatus("Choisissez explicitement une ancre ou un groupe avant l’aperçu.", true);
            return;
        }
        _lastPreview = _session.PreviewLocatorsUx2NavE(_draft);
        _canonicalPreviewReady = _lastPreview.Succeeded;
        _apply.Disabled = !_lastPreview.CanApply;
        if (_lastPreview.Succeeded)
            _worldViewport.ShowNavigationOverlayUx2NavB(_lastPreview.Overlay);
        else
            _worldViewport.ClearNavigationOverlayUx2NavB();
        RenderPreview(_lastPreview);
        SetStatus(_lastPreview.Message, !_lastPreview.Succeeded);
        UpdateActionState();
    }

    private void Apply()
    {
        var areaId = SelectedAreaId();
        var locatorId = _locatorDraft?.LocatorId;
        var collectionId = _collectionDraft?.CollectionId;
        var result = _session.ApplyApprovedLocatorsUx2NavE();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            return;
        }
        NavigationChanged?.Invoke();
        ReloadAfterMutation(areaId, locatorId, collectionId);
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            NavigationChanged?.Invoke();
            ReloadAfterMutation(SelectedAreaIdOrNull(), null, null);
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            NavigationChanged?.Invoke();
            ReloadAfterMutation(SelectedAreaIdOrNull(), null, null);
        }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
        UpdateActionState();
    }

    private void ReloadAfterMutation(AreaId? areaId, Guid? locatorId, Guid? collectionId)
    {
        _session.CancelLocatorsUx2NavE();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        PopulateAreas();
        var areaIndex = areaId is null ? -1 : _areaChoices.ToList().FindIndex(item =>
            item.AreaId == areaId);
        if (areaIndex < 0)
        {
            ClearSelection();
            return;
        }
        _area.Selected = areaIndex + 1;
        SelectArea(areaIndex + 1);
        var locatorIndex = locatorId is null ? -1 : _locatorChoices.ToList().FindIndex(item =>
            item.LocatorId == locatorId.Value);
        if (locatorIndex >= 0)
        {
            _locator.Selected = locatorIndex + 1;
            SelectLocator(locatorIndex + 1);
        }
        var collectionIndex = collectionId is null ? -1 : _collectionChoices.ToList().FindIndex(item =>
            item.CollectionId == collectionId.Value);
        if (collectionIndex >= 0)
        {
            _collection.Selected = collectionIndex + 1;
            SelectCollection(collectionIndex + 1);
        }
    }

    private void SaveOverride()
    {
        var selected = SelectedIndex(_overrideList);
        if (selected >= 0 && _overrides[selected].IsProtected)
        {
            SetStatus("Cet override technique est protégé et doit rester inchangé.", true);
            return;
        }
        if (_locatorDraft is null || string.IsNullOrWhiteSpace(_overrideKey.Text))
        {
            SetStatus("Nommez la clé d’override.", true);
            return;
        }
        var key = _overrideKey.Text.Trim();
        var existing = _overrides.FindIndex(item => string.Equals(item.Key, key,
            StringComparison.Ordinal));
        if (existing >= 0 && existing != selected)
        {
            SetStatus("Cette clé existe déjà ; sélectionnez-la pour la modifier.", true);
            return;
        }
        var entry = new StudioUx2NavEOverrideDraft(key, _overrideValue.Text.Trim());
        if (selected >= 0)
            _overrides[selected] = entry;
        else
            _overrides.Add(entry);
        RefreshOverrides();
        _overrideList.Select(Math.Max(0, selected >= 0 ? selected : _overrides.Count - 1));
        DraftChanged();
    }

    private void SelectOverride(long index)
    {
        if (index < 0 || index >= _overrides.Count)
        {
            return;
        }
        _updating = true;
        var entry = _overrides[(int)index];
        _overrideKey.Editable = !entry.IsProtected;
        _overrideValue.Editable = !entry.IsProtected;
        _overrideKey.Text = entry.IsProtected ? string.Empty : entry.Key;
        _overrideValue.Text = entry.IsProtected ? string.Empty : entry.Value;
        _overrideKey.PlaceholderText = entry.IsProtected ? "Clé technique protégée" : "clé humaine";
        _overrideValue.PlaceholderText = entry.IsProtected ? "Valeur protégée" : "valeur";
        _updating = false;
        UpdateOverrideActionState();
    }

    private void RemoveOverride()
    {
        var index = SelectedIndex(_overrideList);
        if (index < 0)
        {
            return;
        }
        if (_overrides[index].IsProtected)
        {
            SetStatus("Cet override technique est protégé et doit rester inchangé.", true);
            return;
        }
        _overrides.RemoveAt(index);
        RefreshOverrides();
        DraftChanged();
    }

    private void MoveOverrideUp() => MoveOverride(-1);
    private void MoveOverrideDown() => MoveOverride(1);

    private void MoveOverride(int delta)
    {
        var index = SelectedIndex(_overrideList);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= _overrides.Count)
        {
            return;
        }
        if (_overrides[index].IsProtected || _overrides[target].IsProtected)
        {
            SetStatus("L’ordre autour d’un override technique protégé doit rester inchangé.", true);
            return;
        }
        (_overrides[index], _overrides[target]) = (_overrides[target], _overrides[index]);
        RefreshOverrides();
        _overrideList.Select(target);
        DraftChanged();
    }

    private void RefreshOverrides()
    {
        _overrideList.Clear();
        foreach (var item in _overrides)
            _overrideList.AddItem(item.IsProtected
                ? "Override technique protégé · valeur conservée"
                : $"{item.Key} · {item.Value}");
        _overrideKey.Editable = true;
        _overrideValue.Editable = true;
        _overrideKey.PlaceholderText = "clé humaine";
        _overrideValue.PlaceholderText = "valeur";
        _overrideKey.Text = string.Empty;
        _overrideValue.Text = string.Empty;
        UpdateOverrideActionState();
    }

    private void AddMember()
    {
        if (_collectionDraft is null || _memberCandidate.Selected <= 0 ||
            _memberCandidate.Selected > _memberChoices.Count)
        {
            return;
        }
        var id = _memberChoices[_memberCandidate.Selected - 1].LocatorId;
        if (!_memberIds.Contains(id))
        {
            _memberIds.Add(id);
            RefreshMembers();
            _memberList.Select(_memberIds.Count - 1);
            DraftChanged();
        }
    }

    private void RemoveMember()
    {
        var index = SelectedIndex(_memberList);
        if (index < 0)
        {
            return;
        }
        _memberIds.RemoveAt(index);
        RefreshMembers();
        DraftChanged();
    }

    private void MoveMemberUp() => MoveMember(-1);
    private void MoveMemberDown() => MoveMember(1);

    private void MoveMember(int delta)
    {
        var index = SelectedIndex(_memberList);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= _memberIds.Count)
        {
            return;
        }
        (_memberIds[index], _memberIds[target]) = (_memberIds[target], _memberIds[index]);
        RefreshMembers();
        _memberList.Select(target);
        DraftChanged();
    }

    private void RefreshMembers()
    {
        _memberList.Clear();
        foreach (var id in _memberIds)
        {
            var choice = _memberChoices.FirstOrDefault(item => item.LocatorId == id);
            _memberList.AddItem(choice?.Label ?? "Ancre existante protégée");
        }
        UpdateMemberActionState();
    }

    private void PopulateMemberCandidates()
    {
        _memberCandidate.Clear();
        _memberCandidate.AddItem("Choisir une ancre à ajouter…");
        foreach (var choice in _memberChoices) _memberCandidate.AddItem(choice.Label);
        _memberCandidate.Selected = 0;
    }

    private void RenderPreview(StudioUx2NavEPreview? preview)
    {
        ClearChildren(_diff);
        ClearChildren(_diagnostics);
        ClearChildren(_resolved);
        ClearChildren(_routes);
        if (preview is null)
        {
            _diff.AddChild(StudioStyle.Label("Aucun diff calculé.", 9, StudioStyle.Muted));
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic calculé.", 9,
                StudioStyle.Muted));
            _resolved.AddChild(StudioStyle.Label("Aucun groupe prévisualisé.", 9,
                StudioStyle.Muted));
            _routes.AddChild(StudioStyle.Label("Aucune route analysée.", 9,
                StudioStyle.Muted));
            return;
        }
        _summary.Text = preview.Summary;
        foreach (var item in preview.Diff.Take(12))
            _diff.AddChild(StudioStyle.Label("• " + item, 9, StudioStyle.Text, wrap: true));
        if (preview.Diff.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucune différence sémantique.", 9, StudioStyle.Muted));
        foreach (var item in preview.Diagnostics.Take(10))
            _diagnostics.AddChild(StudioStyle.Label($"{item.Code} · {item.Path}\n{item.Message}",
                9, StudioStyle.Danger, wrap: true));
        if (preview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("✓ Contrat canonique valide", 9,
                StudioStyle.Success));
        foreach (var item in preview.ResolvedLocators.Take(10))
            _resolved.AddChild(StudioStyle.Label(
                $"• {item.Label} · {HumanPurpose(item.Purpose)} · {item.Position.X:0.##}, {item.Position.Z:0.##}",
                9, StudioStyle.Text, wrap: true));
        if (preview.ResolvedLocators.Count == 0)
            _resolved.AddChild(StudioStyle.Label("Aucun résultat pour le groupe sélectionné.", 9,
                StudioStyle.Muted));
        foreach (var item in preview.RouteDiagnostics.Take(12))
        {
            var projection = item.NavigationProjected ? "sur navigation" :
                preview.ProjectionAvailable ? "hors navigation" : "projection indisponible";
            _routes.AddChild(StudioStyle.Label(
                $"• {item.RouteLabel} · {item.FromOrder} → {item.ToOrder} · " +
                $"coût {item.Cost:0.##} · {item.TravelSeconds:0.##} s · {projection}",
                9, item.Broken ? StudioStyle.Danger : StudioStyle.Text, wrap: true));
        }
        if (preview.RouteDiagnostics.Count == 0)
            _routes.AddChild(StudioStyle.Label("Aucune patrouille à diagnostiquer.", 9,
                StudioStyle.Muted));
    }

    private void ClearSelection()
    {
        _updating = true;
        _locator.Clear();
        _locator.AddItem("Choisir une ancre…");
        _collection.Clear();
        _collection.AddItem("Choisir un groupe…");
        _memberCandidate.Clear();
        _memberCandidate.AddItem("Choisir une ancre à ajouter…");
        _updating = false;
        _locatorDraft = null;
        _collectionDraft = null;
        _visualizationDraft = null;
        _draft = null;
        _lastPreview = null;
        _explicitSelection = false;
        _canonicalPreviewReady = false;
        _selection.Text =
            "Choisissez explicitement une zone puis une ancre ou un groupe ; aucune fiche n’est chargée automatiquement.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        _summary.Text = "Aucun aperçu calculé.";
        ClearLocatorFields();
        ClearCollectionFields();
        RenderPreview(null);
        SetEditorEnabled(false);
        UpdateActionState();
    }

    private void WireDraftSignals()
    {
        _locatorName.TextChanged += _ => DraftChanged();
        _locatorPurpose.ItemSelected += _ => DraftChanged();
        _locatorX.ValueChanged += _ => DraftChanged();
        _locatorY.ValueChanged += _ => DraftChanged();
        _locatorZ.ValueChanged += _ => DraftChanged();
        _locatorTags.TextChanged += _ => DraftChanged();
        _locatorAsset.TextChanged += _ => DraftChanged();
        _locatorBlueprint.ItemSelected += _ => DraftChanged();
        _locatorRequired.Toggled += _ => DraftChanged();
        _collectionName.TextChanged += _ => DraftChanged();
        _collectionQuery.TextChanged += _ => DraftChanged();
        _collectionTags.TextChanged += _ => DraftChanged();
        _collectionPurpose.ItemSelected += _ => DraftChanged();
        _collectionSelection.ItemSelected += _ => DraftChanged();
        _collectionHasOrigin.Toggled += value =>
        {
            _originFields.Visible = value;
            DraftChanged();
        };
        _originX.ValueChanged += _ => DraftChanged();
        _originY.ValueChanged += _ => DraftChanged();
        _originZ.ValueChanged += _ => DraftChanged();
        _collectionSeed.ValueChanged += _ => DraftChanged();
        _collectionRequired.Toggled += _ => DraftChanged();
        _showSegments.Toggled += _ => DraftChanged();
        _showDirection.Toggled += _ => DraftChanged();
        _showCosts.Toggled += _ => DraftChanged();
        _showActions.Toggled += _ => DraftChanged();
        _showBroken.Toggled += _ => DraftChanged();
        _showProjection.Toggled += _ => DraftChanged();
        _showTiming.Toggled += _ => DraftChanged();
        _costPerMeter.ValueChanged += _ => DraftChanged();
        _visualRequired.Toggled += _ => DraftChanged();
    }

    private void SetEditorEnabled(bool enabled)
    {
        _editor.Visible = enabled;
        _newLocator.Disabled = !enabled;
        _newCollection.Disabled = !enabled;
        _preview.Disabled = !enabled;
    }

    private void UpdateActionState()
    {
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = !_session.IsDirty;
        _preview.Disabled = _draft is null || !HasExplicitSelection;
        if (!_canonicalPreviewReady) _apply.Disabled = true;
        UpdateOverrideActionState();
        UpdateMemberActionState();
    }

    private void UpdateOverrideActionState()
    {
        var index = SelectedIndex(_overrideList);
        var protectedEntry = index >= 0 && _overrides[index].IsProtected;
        _saveOverride.Disabled = _locatorDraft is null || protectedEntry;
        _removeOverride.Disabled = index < 0 || protectedEntry;
        _moveOverrideUp.Disabled = index <= 0 || protectedEntry ||
                                   index > 0 && _overrides[index - 1].IsProtected;
        _moveOverrideDown.Disabled = index < 0 || index >= _overrides.Count - 1 ||
                                     protectedEntry || index >= 0 && index < _overrides.Count - 1 &&
                                     _overrides[index + 1].IsProtected;
    }

    private void UpdateMemberActionState()
    {
        var index = SelectedIndex(_memberList);
        _addMember.Disabled = _collectionDraft is null;
        _removeMember.Disabled = index < 0;
        _moveMemberUp.Disabled = index <= 0;
        _moveMemberDown.Disabled = index < 0 || index >= _memberIds.Count - 1;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private AreaId SelectedAreaId() => _areaChoices[_area.Selected - 1].AreaId;

    private AreaId? SelectedAreaIdOrNull() => _area.Selected > 0 && _area.Selected <= _areaChoices.Count
        ? SelectedAreaId() : null;

    private StudioUx2NavEEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History!;
        return new StudioUx2NavEEvidenceSourceSnapshot(_sourceSession.Project!, history,
            history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureNavigationApprovalUx2NavB(),
            _sourceSession.CaptureWaypointApprovalUx2NavC(),
            _sourceSession.CapturePatrolApprovalUx2NavD(),
            _sourceSession.CaptureLocatorApprovalUx2NavE());
    }

    private bool SourceMatchesSnapshot(StudioUx2NavEEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureNavigationApprovalUx2NavB() == snapshot.NavigationApproval &&
               _sourceSession.CaptureWaypointApprovalUx2NavC() == snapshot.WaypointApproval &&
               _sourceSession.CapturePatrolApprovalUx2NavD() == snapshot.PatrolApproval &&
               _sourceSession.CaptureLocatorApprovalUx2NavE() == snapshot.LocatorApproval;
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
                StudioSessionController.DeleteUx2NavETemporaryWorkspace(root);
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
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or
                                           IOException)
        {
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_E_VIEWPORT_FALLBACK {exception.Message}");
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
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_E_VIEWPORT_RESTORE_FAILED {exception.Message}");
        }
    }

    private void UpdateDialogBounds()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0)
        {
            return;
        }
        var width = MathF.Min(1760, MathF.Max(1020, viewport.X - 48));
        var height = MathF.Min(980, MathF.Max(650, viewport.Y - 42));
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Position = new Vector2((viewport.X - width) / 2, (viewport.Y - height) / 2);
        _dialog.Size = new Vector2(width, height);
    }

    private static PanelContainer ExpandedCard(string name, string title, string help, Control content)
    {
        var card = StudioUx2NavCControls.Card(name, title, help, content);
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        card.SizeFlagsStretchRatio = 1;
        return card;
    }

    private static int SelectedIndex(ItemList list)
    {
        var selected = list.GetSelectedItems();
        return selected.Length == 0 ? -1 : selected[0];
    }

    private static IReadOnlyList<string> ParseList(string text) => text.Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static string HumanPurpose(LocatorPurpose purpose) => purpose switch
    {
        LocatorPurpose.Audio => "Audio",
        LocatorPurpose.Vfx => "Effet visuel",
        LocatorPurpose.Spawn => "Apparition",
        LocatorPurpose.Objective => "Objectif",
        _ => "Gameplay",
    };

    private static string HumanSelection(LocatorSelectionMode mode) => mode switch
    {
        LocatorSelectionMode.Nearest => "Plus proche",
        LocatorSelectionMode.RandomDeterministic => "Aléatoire déterministe",
        _ => "Ordre stable",
    };

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.Free();
        }
    }

    private static IEnumerable<Label> DescendantLabelsUx2NavE(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label)
                yield return label;
            foreach (var descendant in DescendantLabelsUx2NavE(child))
                yield return descendant;
        }
    }

    private static bool ContainsUuidUx2NavE(string text) =>
        System.Text.RegularExpressions.Regex.IsMatch(text,
            @"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
