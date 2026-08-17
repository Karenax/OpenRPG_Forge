using System.Text.RegularExpressions;
using Forge.Domain.Areas;
using Forge.Domain.RuntimePolicies;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCrossAreaPatrolOverlayUx2NavK : Control
{
    private static readonly PatrolOwnershipAuthority[] AuthoritiesUx2NavK =
        Enum.GetValues<PatrolOwnershipAuthority>();

    private readonly StudioSessionController _sourceSession;
    private readonly StudioWorldViewport _worldViewport;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceTempRoot;
    private StudioUx2NavKEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _lastSourceRestored = true;
    private bool _lastUiTransaction;
    private bool _updating;
    private bool _explicitAreaSelection;
    private bool _explicitRouteSelection;
    private int _selectedSegmentIndex = -1;
    private StudioUx2NavKDraft? _draft;
    private StudioUx2NavKPreview? _lastPreview;
    private IReadOnlyList<StudioUx2NavKAreaChoice> _areaChoices = [];
    private IReadOnlyList<StudioUx2NavKRouteChoice> _routeChoices = [];
    private IReadOnlyList<StudioUx2NavKPortalChoice> _portalChoices = [];
    private IReadOnlyList<StudioUx2NavKWaypointChoice> _waypointChoices = [];

    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly OptionButton _route;
    private readonly Button _addRoute;
    private readonly Button _removeRoute;
    private readonly Label _selection;
    private readonly LineEdit _owner;
    private readonly CheckBox _loop;
    private readonly OptionButton _segment;
    private readonly Button _addSegment;
    private readonly Button _removeSegment;
    private readonly Button _moveSegmentUp;
    private readonly Button _moveSegmentDown;
    private readonly OptionButton _portal;
    private readonly OptionButton _waypoint;
    private readonly CheckBox _prefetch;
    private readonly OptionButton _authority;
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
    internal event Action? CrossAreaPatrolChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _areaChoices.Count;
    internal bool HasExplicitSelection => _explicitAreaSelection && _explicitRouteSelection &&
                                          _selectedSegmentIndex >= 0;
    internal bool CanonicalPreviewReady => _lastPreview?.Succeeded == true;
    internal bool ShowsViewportOverlay => _lastPreview?.Overlay.Markers.Count > 0 &&
                                          _worldViewport.NavigationOverlayPrimitiveCountUx2NavB > 0;
    internal bool ShowsStructuredDiagnostics => _lastPreview is { Succeeded: true } &&
                                                _lastPreview.Diagnostics.Count == 0;
    internal bool ShowsBoundedRuntimeClaim => _lastPreview?.Runtime is { Boundary: var boundary } &&
                                              boundary.Contains("aucun chargement moteur",
                                                  StringComparison.OrdinalIgnoreCase) &&
                                              boundary.Contains("transport inter-processus",
                                                  StringComparison.OrdinalIgnoreCase);
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

    internal StudioCrossAreaPatrolOverlayUx2NavK(StudioSessionController session,
        StudioWorldViewport worldViewport)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = session;
        _worldViewport = worldViewport ?? throw new ArgumentNullException(nameof(worldViewport));
        Name = "StudioCrossAreaPatrolOverlayUx2NavK";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        var veil = new ColorRect { Color = new Color(.012f, .022f, .032f, .74f) };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);
        _dialog = StudioStyle.Panel("CrossAreaPatrolDialogUx2NavK", StudioStyle.Surface, 12, 1);
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

        var header = RowUx2NavK();
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("NAVIGATION · PATROUILLE INTER-ZONES", 10,
            StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Trajet, passage, reprise et autorité", 19));
        heading.AddChild(StudioStyle.Label(
            "ORF-WPT-010 · édite uniquement les routes inter-zones du profil d’expérience. " +
            "Les routes locales, waypoints, locators, vols et autres lots NAV B–J restent protégés.",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var selectors = RowUx2NavK();
        _area = StudioUx2NavCControls.Options();
        _area.ItemSelected += SelectArea;
        selectors.AddChild(StudioUx2NavCControls.Field("Zone propriétaire", _area, 210));
        _route = StudioUx2NavCControls.Options();
        _route.ItemSelected += SelectRoute;
        selectors.AddChild(StudioUx2NavCControls.Field("Patrouille nommée", _route, 240));
        _addRoute = StudioStyle.Button("Nouvelle", compact: true);
        _addRoute.Pressed += AddRoute;
        selectors.AddChild(_addRoute);
        _removeRoute = StudioStyle.Button("Retirer", compact: true);
        _removeRoute.Pressed += RemoveRoute;
        selectors.AddChild(_removeRoute);
        root.AddChild(selectors);

        _selection = StudioStyle.Label(
            "Choisissez explicitement une zone puis une patrouille ; aucune route n’est chargée automatiquement.",
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

        var routeFields = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var routeRow = RowUx2NavK();
        _owner = StudioUx2NavCControls.Text("ex. garde des remparts");
        routeRow.AddChild(StudioUx2NavCControls.Field("Propriétaire runtime", _owner, 360));
        _loop = StudioUx2NavCControls.Check("Revenir au départ");
        routeRow.AddChild(_loop);
        routeFields.AddChild(routeRow);
        routeFields.AddChild(StudioStyle.Label(
            "Une clé propriétaire existante opaque reste protégée. Une nouvelle clé doit être humaine ; " +
            "aucun acteur, lease ou serveur n’est créé par cette fiche.", 9, StudioStyle.Muted, wrap: true));
        editor.AddChild(StudioUx2NavCControls.Card("CrossAreaRouteUx2NavK", "Route sélectionnée",
            "La zone cible est dérivée du passage afin d’éviter une incohérence silencieuse.", routeFields));

        var segmentContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var segmentSelector = RowUx2NavK();
        _segment = StudioUx2NavCControls.Options();
        _segment.ItemSelected += SelectSegment;
        segmentSelector.AddChild(StudioUx2NavCControls.Field("Étape ordonnée", _segment, 300));
        _addSegment = StudioStyle.Button("Ajouter", compact: true);
        _addSegment.Pressed += AddSegment;
        segmentSelector.AddChild(_addSegment);
        _removeSegment = StudioStyle.Button("Retirer", compact: true);
        _removeSegment.Pressed += RemoveSegment;
        segmentSelector.AddChild(_removeSegment);
        _moveSegmentUp = StudioStyle.Button("Monter", compact: true);
        _moveSegmentUp.Pressed += () => MoveSegment(-1);
        segmentSelector.AddChild(_moveSegmentUp);
        _moveSegmentDown = StudioStyle.Button("Descendre", compact: true);
        _moveSegmentDown.Pressed += () => MoveSegment(1);
        segmentSelector.AddChild(_moveSegmentDown);
        segmentContent.AddChild(segmentSelector);
        var destinations = RowUx2NavK();
        _portal = StudioUx2NavCControls.Options();
        _portal.ItemSelected += SelectPortal;
        destinations.AddChild(StudioUx2NavCControls.Field("Passage et zone cible", _portal, 340));
        _waypoint = StudioUx2NavCControls.Options();
        _waypoint.ItemSelected += SelectWaypoint;
        destinations.AddChild(StudioUx2NavCControls.Field("Point de reprise", _waypoint, 300));
        segmentContent.AddChild(destinations);
        var policy = RowUx2NavK();
        _prefetch = StudioUx2NavCControls.Check("Demander le préchargement");
        policy.AddChild(_prefetch);
        _authority = StudioUx2NavCControls.Options("Locale", "Hôte", "Serveur");
        _authority.ItemSelected += _ => DraftChanged();
        policy.AddChild(StudioUx2NavCControls.Field("Autorité de transfert", _authority, 180));
        segmentContent.AddChild(policy);
        segmentContent.AddChild(StudioStyle.Label(
            "L’ordre est libre dans le brouillon ; l’aperçu refuse une chaîne discontinue ou une boucle " +
            "qui ne revient pas à son départ.", 9, StudioStyle.Muted, wrap: true));
        editor.AddChild(StudioUx2NavCControls.Card("CrossAreaSegmentUx2NavK", "Étapes inter-zones",
            "Chaque étape choisit un passage existant puis un waypoint humain de sa zone cible.",
            segmentContent));

        var columns = RowUx2NavK();
        _diff = PreviewColumnUx2NavK(columns, "DIFF SÉMANTIQUE");
        _diagnostics = PreviewColumnUx2NavK(columns, "DIAGNOSTICS");
        _runtime = PreviewColumnUx2NavK(columns, "CURSEUR RUNTIME BORNÉ");
        var previewContent = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        previewContent.AddChild(columns);
        _summary = StudioStyle.Label("Aucun aperçu calculé.", 10, StudioStyle.Text, wrap: true);
        previewContent.AddChild(_summary);
        _boundary = StudioStyle.Label(
            "Limite : aucun chargement moteur, lease, transport inter-processus, transfert réseau ou " +
            "préchargement réel n’est exécuté dans le Studio.", 9, StudioStyle.Warning, wrap: true);
        previewContent.AddChild(_boundary);
        editor.AddChild(StudioUx2NavCControls.Card("CrossAreaPreviewUx2NavK", "Aperçu avant application",
            "Le viewport marque les passages de la zone visible ; les autres zones restent une liste nommée.",
            previewContent));

        var footer = RowUx2NavK();
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

        _owner.TextChanged += _ => DraftChanged();
        _loop.Toggled += _ => DraftChanged();
        _prefetch.Toggled += _ => DraftChanged();
        Resized += UpdateDialogBounds;
        TreeExiting += Close;
        ClearSelection();
    }

    internal void Open()
    {
        ReleaseEvidenceWorkspace();
        _session = _sourceSession;
        _sourceSession.CancelCrossAreaPatrolApprovalUx2NavK();
        Visible = true;
        MouseFilter = MouseFilterEnum.Stop;
        PopulateAreas();
        ClearSelection();
        UpdateDialogBounds();
        SetStatus("Choisissez explicitement une zone puis une patrouille.");
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (_evidenceSession is not null) _evidenceSession.CancelCrossAreaPatrolApprovalUx2NavK();
        else _sourceSession.CancelCrossAreaPatrolApprovalUx2NavK();
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _draft = null;
        _lastPreview = null;
        _explicitAreaSelection = false;
        _explicitRouteSelection = false;
        _selectedSegmentIndex = -1;
        ReleaseEvidenceWorkspace();
        RestoreSourceViewport();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!(mode.StartsWith("ux2-nav-k", StringComparison.OrdinalIgnoreCase) ||
              mode.StartsWith("ux2-wpt-010", StringComparison.OrdinalIgnoreCase)) ||
            _sourceSession.Project is null || _sourceSession.History is null) return false;
        ReleaseEvidenceWorkspace();
        _evidenceSourceSnapshot = CaptureSourceSnapshot();
        _lastEvidenceSourceUntouched = true;
        _lastEvidenceResourcesReleased = false;
        _lastSourceRestored = false;
        var project = _sourceSession.CreateUx2NavKEvidenceProject();
        if (project is null) return FailEvidencePreparation(null);
        var tempRoot = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-k-evidence-" + Guid.NewGuid().ToString("N"));
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
            if (_draft?.Segments.Count is not > 0) { Close(); return false; }
            _segment.Selected = 1;
            SelectSegment(1);
            _prefetch.ButtonPressed = !_prefetch.ButtonPressed;
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
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_NAV_K_EVIDENCE_FAILED " +
                        $"type={exception.GetType().Name} message={exception.Message}");
            return FailEvidencePreparation(tempRoot);
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        _lastUiTransaction = false;
        try
        {
            if (!PrepareEvidence("ux2-nav-k")) { Close(); return false; }
            var countBefore = _draft!.Segments.Count;
            _addSegment.EmitSignal(Button.SignalName.Pressed);
            var addedThroughUi = _draft?.Segments.Count == countBefore + 1;
            _removeSegment.EmitSignal(Button.SignalName.Pressed);
            var removedThroughUi = _draft?.Segments.Count == countBefore;
            _segment.Selected = 1;
            SelectSegment(1);
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
            _lastUiTransaction = addedThroughUi && removedThroughUi && dryRun && applied && undone && redone && saved;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_NAV_K_UI_TRANSACTION_FAILED " +
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
        var approval = _sourceSession.CaptureCrossAreaPatrolApprovalUx2NavK();
        Open();
        _sourceSession.RestoreCrossAreaPatrolApprovalUx2NavK(approval);
    }

    private void PopulateAreas()
    {
        _updating = true;
        _areaChoices = _session.CrossAreaPatrolAreaChoicesUx2NavK;
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
        _route.AddItem("Choisir une patrouille…");
        _segment.Clear();
        _segment.AddItem("Choisir une étape…");
        _portal.Clear();
        _portal.AddItem("Choisir un passage…");
        _waypoint.Clear();
        _waypoint.AddItem("Choisir un point de reprise…");
        _updating = false;
        _draft = null;
        _lastPreview = null;
        _routeChoices = [];
        _portalChoices = [];
        _waypointChoices = [];
        _explicitAreaSelection = false;
        _explicitRouteSelection = false;
        _selectedSegmentIndex = -1;
        _selection.Text =
            "Choisissez explicitement une zone puis une patrouille ; aucune route n’est chargée automatiquement.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        SetEditorEnabled(false);
        RenderPreview(null);
        UpdateActionState();
    }

    private void SelectArea(long index)
    {
        if (_updating) return;
        _session.CancelCrossAreaPatrolApprovalUx2NavK();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _draft = null;
        _lastPreview = null;
        _explicitRouteSelection = false;
        _selectedSegmentIndex = -1;
        _explicitAreaSelection = index > 0 && index <= _areaChoices.Count;
        _updating = true;
        _route.Clear();
        _route.AddItem("Choisir une patrouille…");
        _routeChoices = _explicitAreaSelection
            ? _session.CrossAreaPatrolRouteChoicesUx2NavK(_areaChoices[(int)index - 1].AreaId) : [];
        foreach (var choice in _routeChoices) _route.AddItem(choice.Label);
        _route.Selected = 0;
        _updating = false;
        ClearRouteFields();
        if (_explicitAreaSelection)
        {
            var choice = _areaChoices[(int)index - 1];
            _selection.Text = $"Zone « {choice.Label} » sélectionnée · choisissez une patrouille ou créez-en une.";
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
        _session.CancelCrossAreaPatrolApprovalUx2NavK();
        _worldViewport.ClearNavigationOverlayUx2NavB();
        _lastPreview = null;
        _selectedSegmentIndex = -1;
        _explicitRouteSelection = index > 0 && index <= _routeChoices.Count;
        if (!_explicitRouteSelection)
        {
            ClearRouteFields();
            UpdateActionState();
            return;
        }
        var areaChoice = _areaChoices[_area.Selected - 1];
        var routeChoice = _routeChoices[(int)index - 1];
        _draft = _session.CreateCrossAreaPatrolDraftUx2NavK(areaChoice.AreaId, routeChoice.RouteId);
        if (_draft is null)
        {
            ClearRouteFields();
            SetStatus("La patrouille choisie n’est plus disponible.", true);
            return;
        }
        LoadDraft();
        _selection.Text = $"« {routeChoice.Label} » sélectionnée dans « {areaChoice.Label} » · " +
                          "choisissez explicitement une étape.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
        SetStatus("Patrouille chargée sans mutation · choisissez une étape puis prévisualisez.");
    }

    private void AddRoute()
    {
        if (!_explicitAreaSelection) { SetStatus("Choisissez d’abord une zone.", true); return; }
        var areaChoice = _areaChoices[_area.Selected - 1];
        var draft = _session.CreateNewCrossAreaPatrolDraftUx2NavK(areaChoice.AreaId);
        if (draft is null)
        {
            SetStatus("Aucun passage sortant avec waypoint de reprise humain n’est disponible.", true);
            return;
        }
        _session.CancelCrossAreaPatrolApprovalUx2NavK();
        _draft = draft;
        _explicitRouteSelection = true;
        _lastPreview = null;
        _updating = true;
        _route.Clear();
        _route.AddItem("Choisir une patrouille…");
        _route.AddItem("Nouvelle patrouille · non appliquée");
        _route.Selected = 1;
        _updating = false;
        LoadDraft();
        _selection.Text = $"Nouvelle patrouille sélectionnée dans « {areaChoice.Label} » · brouillon local.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Accent);
        SetStatus("Nouvelle patrouille prête dans le brouillon · prévisualisation obligatoire.");
    }

    private void RemoveRoute()
    {
        if (_draft is null) return;
        _session.CancelCrossAreaPatrolApprovalUx2NavK();
        if (_draft.IsNew)
        {
            SelectArea(_area.Selected);
            SetStatus("Brouillon neuf abandonné · aucun document modifié.");
            return;
        }
        CommitFields();
        _draft = _draft with { Remove = true };
        _lastPreview = null;
        _selection.Text = "Patrouille sélectionnée pour retrait · prévisualisez avant application.";
        _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
        SetEditorEnabled(false);
        _preview.Disabled = false;
        RenderPreview(null);
        UpdateActionState();
    }

    private void LoadDraft()
    {
        if (_draft is null) { ClearRouteFields(); return; }
        _updating = true;
        _owner.Text = _draft.OwnerIdIsProtected ? "Clé existante protégée" : _draft.OwnerId;
        _owner.Editable = !_draft.OwnerIdIsProtected;
        _loop.ButtonPressed = _draft.Loop;
        PopulateSegments();
        _segment.Selected = 0;
        _selectedSegmentIndex = -1;
        _updating = false;
        SetEditorEnabled(true);
        ClearSegmentFields();
        RenderPreview(null);
        UpdateActionState();
    }

    private void PopulateSegments()
    {
        _segment.Clear();
        _segment.AddItem("Choisir une étape…");
        if (_draft is null) return;
        for (var index = 0; index < _draft.Segments.Count; index++)
        {
            var item = _draft.Segments[index];
            _segment.AddItem($"{index + 1}. {AreaLabel(item.FromAreaId)} → {AreaLabel(item.ToAreaId)}");
        }
    }

    private void SelectSegment(long index)
    {
        if (_updating || _draft is null) return;
        CommitFields();
        _selectedSegmentIndex = index > 0 && index <= _draft.Segments.Count ? (int)index - 1 : -1;
        _lastPreview = null;
        _session.CancelCrossAreaPatrolApprovalUx2NavK();
        if (_selectedSegmentIndex < 0) ClearSegmentFields();
        else LoadSegmentFields();
        RenderPreview(null);
        UpdateActionState();
    }

    private void AddSegment()
    {
        if (_draft is null) return;
        CommitFields();
        var item = _session.CreateNewCrossAreaPatrolSegmentUx2NavK(_draft);
        if (item is null)
        {
            SetStatus("Aucun passage sortant avec waypoint de reprise n’est disponible depuis la dernière zone.",
                true);
            return;
        }
        _draft = _draft with { Segments = _draft.Segments.Concat([item]).ToArray() };
        _updating = true;
        PopulateSegments();
        _segment.Selected = _draft.Segments.Count;
        _updating = false;
        _selectedSegmentIndex = _draft.Segments.Count - 1;
        LoadSegmentFields();
        InvalidatePreview("Étape ajoutée au brouillon · vérifiez la continuité avant application.");
    }

    private void RemoveSegment()
    {
        if (_draft is null || _selectedSegmentIndex < 0) return;
        if (_draft.Segments.Count <= 1)
        {
            SetStatus("Une patrouille doit conserver au moins une étape.", true);
            return;
        }
        CommitFields();
        var removed = _selectedSegmentIndex;
        _draft = _draft with
        {
            Segments = _draft.Segments.Where((_, index) => index != removed).ToArray(),
        };
        _selectedSegmentIndex = Math.Min(removed, _draft.Segments.Count - 1);
        _updating = true;
        PopulateSegments();
        _segment.Selected = _selectedSegmentIndex + 1;
        _updating = false;
        LoadSegmentFields();
        InvalidatePreview("Étape retirée du brouillon · continuité à prévisualiser.");
    }

    private void MoveSegment(int delta)
    {
        if (_draft is null || _selectedSegmentIndex < 0) return;
        CommitFields();
        var target = _selectedSegmentIndex + delta;
        if (target < 0 || target >= _draft.Segments.Count) return;
        var values = _draft.Segments.ToArray();
        (values[_selectedSegmentIndex], values[target]) = (values[target], values[_selectedSegmentIndex]);
        _draft = _draft with { Segments = values };
        _selectedSegmentIndex = target;
        _updating = true;
        PopulateSegments();
        _segment.Selected = target + 1;
        _updating = false;
        LoadSegmentFields();
        InvalidatePreview("Ordre modifié au clavier ou au bouton · l’aperçu vérifiera la chaîne.");
    }

    private void LoadSegmentFields()
    {
        if (_draft is null || _selectedSegmentIndex < 0) { ClearSegmentFields(); return; }
        var item = _draft.Segments[_selectedSegmentIndex];
        _updating = true;
        _portalChoices = _session.CrossAreaPatrolPortalChoicesUx2NavK(item);
        _portal.Clear();
        foreach (var choice in _portalChoices) _portal.AddItem(choice.Label);
        _portal.Selected = Math.Max(0, _portalChoices.ToList().FindIndex(choice =>
            choice.PortalId == item.PortalId));
        _waypointChoices = _session.CrossAreaPatrolWaypointChoicesUx2NavK(item);
        _waypoint.Clear();
        foreach (var choice in _waypointChoices) _waypoint.AddItem(choice.Label);
        _waypoint.Selected = Math.Max(0, _waypointChoices.ToList().FindIndex(choice =>
            choice.WaypointId == item.ResumeWaypointId));
        _prefetch.ButtonPressed = item.PrefetchDestination;
        _authority.Selected = Array.IndexOf(AuthoritiesUx2NavK, item.Authority);
        _updating = false;
        SetSegmentEnabled(true);
    }

    private void SelectPortal(long index)
    {
        if (_updating || _draft is null || _selectedSegmentIndex < 0 ||
            index < 0 || index >= _portalChoices.Count) return;
        CommitRouteFields();
        var current = _draft.Segments[_selectedSegmentIndex];
        var changed = _session.ChangeCrossAreaPatrolPortalUx2NavK(current,
            _portalChoices[(int)index].PortalId);
        if (changed is null)
        {
            SetStatus("Ce passage ne possède aucun waypoint de reprise humain dans sa zone cible.", true);
            LoadSegmentFields();
            return;
        }
        _draft = _draft with
        {
            Segments = _draft.Segments.Select((item, position) =>
                position == _selectedSegmentIndex ? changed : item).ToArray(),
        };
        _updating = true;
        PopulateSegments();
        _segment.Selected = _selectedSegmentIndex + 1;
        _updating = false;
        LoadSegmentFields();
        InvalidatePreview("Passage modifié · la zone cible et la reprise ont été réalignées.");
    }

    private void SelectWaypoint(long index)
    {
        if (_updating || _draft is null || _selectedSegmentIndex < 0 ||
            index < 0 || index >= _waypointChoices.Count) return;
        CommitRouteFields();
        var choice = _waypointChoices[(int)index];
        var current = _draft.Segments[_selectedSegmentIndex] with
        {
            ResumeWaypointId = choice.WaypointId,
            ResumeWaypointIsProtected = choice.IsProtected,
        };
        _draft = _draft with
        {
            Segments = _draft.Segments.Select((item, position) =>
                position == _selectedSegmentIndex ? current : item).ToArray(),
        };
        InvalidatePreview("Point de reprise modifié dans le brouillon.");
    }

    private void DraftChanged()
    {
        if (_updating || _draft is null) return;
        CommitFields();
        InvalidatePreview("Brouillon modifié · prévisualisez avant application.");
    }

    private void CommitFields()
    {
        CommitRouteFields();
        if (_draft is null || _selectedSegmentIndex < 0 ||
            _selectedSegmentIndex >= _draft.Segments.Count) return;
        var item = _draft.Segments[_selectedSegmentIndex];
        if (_waypoint.Selected >= 0 && _waypoint.Selected < _waypointChoices.Count)
        {
            var waypoint = _waypointChoices[_waypoint.Selected];
            item = item with
            {
                ResumeWaypointId = waypoint.WaypointId,
                ResumeWaypointIsProtected = waypoint.IsProtected,
            };
        }
        var authorityIndex = Math.Clamp(_authority.Selected, 0, AuthoritiesUx2NavK.Length - 1);
        item = item with
        {
            PrefetchDestination = _prefetch.ButtonPressed,
            Authority = AuthoritiesUx2NavK[authorityIndex],
        };
        _draft = _draft with
        {
            Segments = _draft.Segments.Select((value, position) =>
                position == _selectedSegmentIndex ? item : value).ToArray(),
        };
    }

    private void CommitRouteFields()
    {
        if (_draft is null) return;
        _draft = _draft with
        {
            OwnerId = _draft.OwnerIdIsProtected ? _draft.OwnerId : _owner.Text,
            Loop = _loop.ButtonPressed,
        };
    }

    private void Preview()
    {
        if (_draft is null) return;
        CommitFields();
        var preview = _session.PreviewCrossAreaPatrolUx2NavK(_draft);
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
        var result = _session.ApplyApprovedCrossAreaPatrolUx2NavK();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) { UpdateActionState(); return; }
        CrossAreaPatrolChanged?.Invoke();
        var areaId = _draft?.OwnerAreaId;
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
        _session.CancelCrossAreaPatrolApprovalUx2NavK();
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
        CrossAreaPatrolChanged?.Invoke();
        UpdateActionState();
    }

    private void InvalidatePreview(string message)
    {
        _session.CancelCrossAreaPatrolApprovalUx2NavK();
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
        _selectedSegmentIndex = -1;
        _updating = true;
        _owner.Text = string.Empty;
        _owner.Editable = false;
        _loop.ButtonPressed = false;
        _segment.Clear();
        _segment.AddItem("Choisir une étape…");
        _updating = false;
        ClearSegmentFields();
        SetEditorEnabled(false);
        RenderPreview(null);
        UpdateActionState();
    }

    private void ClearSegmentFields()
    {
        _updating = true;
        _portalChoices = [];
        _waypointChoices = [];
        _portal.Clear();
        _portal.AddItem("Choisir un passage…");
        _waypoint.Clear();
        _waypoint.AddItem("Choisir un point de reprise…");
        _prefetch.ButtonPressed = false;
        _authority.Selected = 2;
        _updating = false;
        SetSegmentEnabled(false);
    }

    private void SetEditorEnabled(bool enabled)
    {
        _owner.Editable = enabled && _draft?.OwnerIdIsProtected != true;
        _loop.Disabled = !enabled;
        _segment.Disabled = !enabled;
        _addSegment.Disabled = !enabled;
        _removeRoute.Disabled = !enabled;
        if (!enabled) SetSegmentEnabled(false);
    }

    private void SetSegmentEnabled(bool enabled)
    {
        _portal.Disabled = !enabled;
        _waypoint.Disabled = !enabled;
        _prefetch.Disabled = !enabled;
        _authority.Disabled = !enabled;
        _removeSegment.Disabled = !enabled;
        _moveSegmentUp.Disabled = !enabled;
        _moveSegmentDown.Disabled = !enabled;
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

    private void RenderPreview(StudioUx2NavKPreview? preview)
    {
        ReplaceColumn(_diff, preview?.Diff ?? [], "Aucun changement calculé.", StudioStyle.Text);
        ReplaceColumn(_diagnostics,
            preview?.Diagnostics.Select(item => $"{item.Path} · {item.Message}").ToArray() ?? [],
            preview is { Succeeded: true } ? "Aucun diagnostic bloquant." : "Prévisualisation requise.",
            preview is { Succeeded: true } ? StudioStyle.Success : StudioStyle.Muted);
        var runtimeLines = preview?.Runtime?.Steps.Select(item =>
            $"{item.Order}. {item.SourceArea} → {item.DestinationArea}\n" +
            $"{item.Portal} · reprise {item.ResumeWaypoint} · {item.Authority} · " +
            $"préchargement {(item.PrefetchDestination ? "demandé" : "non demandé")}").ToArray() ?? [];
        ReplaceColumn(_runtime, runtimeLines, "Aucun curseur simulé.", StudioStyle.Text);
        _summary.Text = preview?.Summary ?? "Aucun aperçu calculé.";
        _boundary.Text = preview?.Runtime?.Boundary ??
                         "Limite : aucun chargement moteur, lease, transport inter-processus, transfert réseau " +
                         "ou préchargement réel n’est exécuté dans le Studio.";
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

    private string AreaLabel(AreaId id)
    {
        var area = _session.Project?.Areas.SingleOrDefault(item => item.Id == id);
        var value = area?.DisplayName?.Trim();
        return string.IsNullOrWhiteSpace(value) || Guid.TryParse(value, out _) || Regex.IsMatch(value,
            "(?i)[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}") ? "Zone protégée" : value;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private bool FailEvidencePreparation(string? tempRoot)
    {
        _evidenceSession?.CancelCrossAreaPatrolApprovalUx2NavK();
        _evidenceSession?.Close();
        _evidenceSession = null;
        _session = _sourceSession;
        var root = tempRoot ?? _evidenceTempRoot;
        _lastEvidenceResourcesReleased = StudioSessionController.DeleteUx2NavKTemporaryWorkspace(root);
        _evidenceTempRoot = null;
        _lastEvidenceSourceUntouched = _evidenceSourceSnapshot is null ||
                                       SourceSnapshotMatches(_evidenceSourceSnapshot);
        _lastSourceRestored = true;
        _evidenceSourceSnapshot = null;
        return false;
    }

    private StudioUx2NavKEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History!;
        return new(_sourceSession.Project!, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureCrossAreaPatrolApprovalUx2NavK());
    }

    private bool SourceSnapshotMatches(StudioUx2NavKEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureCrossAreaPatrolApprovalUx2NavK() == snapshot.Approval;
    }

    private void ReleaseEvidenceWorkspace()
    {
        var snapshot = _evidenceSourceSnapshot;
        var evidence = _evidenceSession;
        var root = _evidenceTempRoot;
        if (evidence is not null)
        {
            evidence.CancelCrossAreaPatrolApprovalUx2NavK();
            evidence.Close();
        }
        _evidenceSession = null;
        _evidenceTempRoot = null;
        _session = _sourceSession;
        if (root is not null)
            _lastEvidenceResourcesReleased = StudioSessionController.DeleteUx2NavKTemporaryWorkspace(root);
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
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_K_VIEWPORT_FALLBACK {exception.Message}");
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
            GD.PushWarning($"OPENRPG_FORGE_STUDIO_UX2_NAV_K_VIEWPORT_RESTORE_FAILED {exception.Message}");
        }
    }

    private void UpdateDialogBounds()
    {
        var viewport = GetViewportRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        var scale = viewport.X >= 3_200 ? 1.5f : viewport.X >= 2_300 ? 1.15f : 1f;
        var width = Math.Clamp((viewport.X * .49f) / scale, 800f, 1_020f);
        var height = Math.Clamp((viewport.Y - 30f) / scale, 700f, 1_180f);
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2(viewport.X - width * scale - 15f,
            Math.Max(15f, (viewport.Y - height * scale) / 2f));
    }

    private static HBoxContainer RowUx2NavK()
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        return row;
    }

    private static VBoxContainer PreviewColumnUx2NavK(HBoxContainer host, string title)
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
