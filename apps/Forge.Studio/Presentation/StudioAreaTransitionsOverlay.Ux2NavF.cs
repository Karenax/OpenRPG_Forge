using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAreaTransitionsOverlayUx2NavF : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioUx2NavFEvidenceSourceSnapshot? _sourceSnapshot;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _area;
    private readonly OptionButton _portal;
    private readonly Button _newPortal;
    private readonly LineEdit _name;
    private readonly OptionButton _sourceEntity;
    private readonly OptionButton _destination;
    private readonly CheckBox _bidirectional;
    private readonly CheckBox _preload;
    private readonly OptionButton _orientation;
    private readonly SpinBox _yaw;
    private readonly OptionButton _velocity;
    private readonly LineEdit _beforeScript;
    private readonly LineEdit _afterScript;
    private readonly CheckBox _hooksRequired;
    private readonly CheckBox _remove;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _runtime;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private Viewport? _subscribedViewport;
    private IReadOnlyList<StudioUx2NavFAreaChoice> _areas = [];
    private IReadOnlyList<StudioUx2NavFPortalChoice> _portals = [];
    private IReadOnlyList<StudioUx2NavFEntityChoice> _sourceEntities = [];
    private IReadOnlyList<StudioUx2NavFDestinationChoice> _destinations = [];
    private StudioUx2NavFDraft? _draft;
    private StudioUx2NavFPreview? _lastPreview;
    private bool _updating;
    private bool _canonicalPreview;
    private bool _lastEvidenceReleased;
    private bool _lastSourceRestored = true;
    private bool _lastSourceUntouched;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? TransitionChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _areas.Count;
    internal bool HasExplicitSelection => _draft is not null && _area.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreview && _lastPreview?.Succeeded == true;
    internal bool ShowsRuntimeResolution => _lastPreview is { NoRuntimeExecuted: true } preview &&
                                            (preview.ForwardResolved || _draft?.Remove == true) &&
                                            (preview.ReverseResolved || _draft?.Remove == true);
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
                                                 _lastPreview.Diagnostics.All(item =>
                                                     !string.IsNullOrWhiteSpace(item.Code) &&
                                                     !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool EvidenceSourceUntouched => _sourceSnapshot is { } snapshot
        ? SourceMatches(snapshot) : _lastSourceUntouched;
    internal bool EvidenceResourcesReleased => _evidenceSession is null && _evidenceRoot is null && _lastEvidenceReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession) && _lastSourceRestored;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(_dialog));
            return text.Contains("Transitions entre zones", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("départ", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("destination", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun déplacement", StringComparison.OrdinalIgnoreCase) &&
                   !StudioSessionController.ContainsTechnicalIdentifierUx2NavF(text);
        }
    }

    internal StudioAreaTransitionsOverlayUx2NavF(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = session;
        Name = "StudioAreaTransitionsOverlayUx2NavF";
        Visible = false;
        ZIndex = 476;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .82f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _dialog = StudioStyle.Panel("AreaTransitionsDialogUx2NavF", StudioStyle.Chrome, 12, 2);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        _dialog.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("NAVIGATION · TRANSITIONS ENTRE ZONES", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Passages et arrivées", 20));
        heading.AddChild(StudioStyle.Label(
            "AREA-030 · départ et destination humains · aperçu sec canonique · aucun déplacement, " +
            "préchargement, script, moteur ou réseau exécuté dans cette surface",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);

        var selectors = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selectors.AddThemeConstantOverride("h_separation", 8);
        root.AddChild(selectors);
        _area = Option();
        _portal = Option();
        _newPortal = StudioStyle.Button("Nouveau passage", compact: true);
        selectors.AddChild(_area); selectors.AddChild(_portal); selectors.AddChild(_newPortal);
        _area.ItemSelected += index => SelectArea((int)index);
        _portal.ItemSelected += index => SelectPortal((int)index);
        _newPortal.Pressed += CreateNewPortal;

        var host = new Control
        {
            CustomMinimumSize = new Vector2(0, 500),
            CustomMaximumSize = new Vector2(-1, 760),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(host);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        host.AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(body);

        var endpoints = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        endpoints.AddThemeConstantOverride("h_separation", 10);
        endpoints.AddThemeConstantOverride("v_separation", 6);
        _name = new LineEdit { PlaceholderText = "Nom humain du passage" };
        _sourceEntity = Option(); _destination = Option();
        AddField(endpoints, "Nom", _name);
        AddField(endpoints, "Objet de départ", _sourceEntity);
        AddField(endpoints, "Destination humaine", _destination);
        _bidirectional = Check("Autoriser le retour par le même passage");
        _preload = Check("Précharger la zone de destination");
        endpoints.AddChild(_bidirectional); endpoints.AddChild(_preload);
        body.AddChild(Card("Endpoints", "Départ et destination",
            "Les objets sont choisis par nom de blueprint et zone ; aucune identité technique n’est affichée.", endpoints));

        var travel = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        travel.AddThemeConstantOverride("h_separation", 10); travel.AddThemeConstantOverride("v_separation", 6);
        _orientation = Option();
        foreach (var value in Enum.GetValues<PortalOrientationPolicy>())
            _orientation.AddItem(StudioSessionController.HumanOrientationUx2NavF(value));
        _yaw = new SpinBox
        {
            MinValue = -360,
            MaxValue = 360,
            Step = 0,
            CustomArrowStep = 1,
            AllowGreater = false,
            AllowLesser = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        StudioStyle.ApplySpinBox(_yaw);
        _velocity = Option();
        foreach (var value in Enum.GetValues<PortalVelocityPolicy>())
            _velocity.AddItem(StudioSessionController.HumanVelocityUx2NavF(value));
        AddField(travel, "Orientation à l’arrivée", _orientation);
        AddField(travel, "Angle fixe (°)", _yaw);
        AddField(travel, "Vitesse à l’arrivée", _velocity);
        body.AddChild(Card("Travel", "Orientation, vitesse et préchargement",
            "Le service canonique résout la transformation en mémoire, sans téléporter d’acteur.", travel));

        var hooks = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        hooks.AddThemeConstantOverride("h_separation", 10); hooks.AddThemeConstantOverride("v_separation", 6);
        _beforeScript = new LineEdit { PlaceholderText = "scripts/avant.forge (facultatif)" };
        _afterScript = new LineEdit { PlaceholderText = "scripts/apres.forge (facultatif)" };
        AddField(hooks, "Avant la transition", _beforeScript);
        AddField(hooks, "Après la transition", _afterScript);
        _hooksRequired = Check("Exiger les hooks au runtime");
        hooks.AddChild(_hooksRequired);
        hooks.AddChild(StudioStyle.Label("Chemins relatifs confinés sous scripts/ · aucune exécution dans l’aperçu.",
            10, StudioStyle.Muted, wrap: true));
        body.AddChild(Card("Hooks", "Scripts de transition", "Les scripts restent des bindings authorés.", hooks));

        _remove = Check("Supprimer ce passage existant");
        body.AddChild(Card("Removal", "Suppression transactionnelle",
            "La suppression possède son propre aperçu, patch inverse, annulation et rétablissement.", _remove));
        _diff = new VBoxContainer();
        body.AddChild(Card("Diff", "Diff humain avant application", "Une seule mutation de portail est envoyée.", _diff));
        _runtime = new VBoxContainer();
        body.AddChild(Card("Runtime", "Résolution runtime en lecture seule",
            "Aller et retour sont calculés avec PortalLinkRuntimeService ; aucun acteur n’est déplacé.", _runtime));
        _diagnostics = new VBoxContainer();
        body.AddChild(Card("Diagnostics", "Diagnostics localisés", "Codes et chemins restent internes.", _diagnostics));

        _status = StudioStyle.Label("Choisissez explicitement une zone puis un passage.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "Commande atomique : passages uniquement · navigation et profils UX2 voisins préservés.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        _preview = Action("Aperçu sec"); _apply = Action("Appliquer");
        _undo = Action("Annuler"); _redo = Action("Rétablir"); _save = Action("Enregistrer");
        actions.AddChild(_preview); actions.AddChild(_apply); actions.AddChild(_undo); actions.AddChild(_redo); actions.AddChild(_save);
        _preview.Pressed += Preview;
        _apply.Pressed += Apply;
        _undo.Pressed += Undo;
        _redo.Pressed += Redo;
        _save.Pressed += Save;
        ConnectInvalidation();
        Reset();
    }

    public override void _Ready()
    {
        UpdateDialogBounds();
        _subscribedViewport = GetViewport();
        _subscribedViewport.SizeChanged += UpdateDialogBounds;
    }

    public override void _ExitTree()
    {
        if (_subscribedViewport is not null)
        {
            _subscribedViewport.SizeChanged -= UpdateDialogBounds;
            _subscribedViewport = null;
        }
        ReleaseEvidence();
    }

    internal void Open()
    {
        ReleaseEvidence();
        _session = _sourceSession;
        _lastSourceRestored = true;
        Visible = true;
        PopulateAreas();
        ResetSelection();
        UpdateButtons();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && ReferenceEquals(_session, _sourceSession)) return;
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidence();
        else _session.CancelPortalApprovalUx2NavF();
        _session = _sourceSession;
        _lastSourceRestored = true;
        _draft = null; _lastPreview = null; _canonicalPreview = false;
        UpdateButtons();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession; Visible = true; PopulateAreas(); ResetSelection(); UpdateButtons();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        ReleaseEvidence();
        _sourceSnapshot = CaptureSource();
        var project = _sourceSession.Project;
        var fixture = project is null ? null : StudioSessionController.CreateUx2NavFEvidenceProject(project);
        if (fixture is null) return false;
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-f-evidence-" + Guid.NewGuid().ToString("N"));
        var controller = new StudioSessionController();
        if (!ProjectSession.Create(root, fixture).Succeeded || !controller.Open(root).Succeeded)
        {
            controller.Close(); StudioSessionController.DeleteUx2NavFTemporaryWorkspace(root); return false;
        }
        _evidenceSession = controller; _evidenceRoot = root; _session = controller;
        _lastEvidenceReleased = false; _lastSourceRestored = false;
        Visible = true; PopulateAreas();
        var areaIndex = _areas.ToList().FindIndex(item => item.HasPortals);
        if (areaIndex < 0) { ReleaseEvidence(); return false; }
        _area.Select(areaIndex + 1); SelectArea(areaIndex + 1);
        if (_portals.Count == 0) { ReleaseEvidence(); return false; }
        _portal.Select(1); SelectPortal(1);
        if (_draft is null) { ReleaseEvidence(); return false; }
        _name.Text = "Passage des lucioles";
        _bidirectional.ButtonPressed = true;
        _preload.ButtonPressed = true;
        _orientation.Select((int)PortalOrientationPolicy.FixedYaw);
        _yaw.Value = 127.5;
        _velocity.Select((int)PortalVelocityPolicy.RedirectForward);
        Preview();
        var ready = CanonicalPreviewReady && ShowsRuntimeResolution && ShowsStructuredDiagnostics &&
                    ShowsHumanSurface && UsesIsolatedEvidence && EvidenceSourceUntouched;
        if (!ready) ReleaseEvidence();
        return ready;
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource();
        if (!PrepareEvidence("ux2-nav-f")) return false;
        var applied = _session.ApplyApprovedPortalUx2NavF();
        var undo = _session.Undo();
        var redo = _session.Redo();
        var secondUndo = _session.Undo();
        var ok = applied.Succeeded && undo.Succeeded && redo.Succeeded && secondUndo.Succeeded;
        Close();
        return ok && SourceMatches(snapshot) && EvidenceResourcesReleased && SourceSessionRestored;
    }

    private void PopulateAreas()
    {
        _updating = true;
        _areas = _session.PortalAreaChoicesUx2NavF;
        _area.Clear(); _area.AddItem("Choisir une zone…");
        foreach (var item in _areas) _area.AddItem(item.Label);
        _area.Select(0);
        _updating = false;
    }

    private void SelectArea(int index)
    {
        if (_updating) return;
        _session.CancelPortalApprovalUx2NavF(); _lastPreview = null; _canonicalPreview = false;
        _draft = null; _portals = []; _portal.Clear(); _portal.AddItem("Choisir un passage…");
        if (index <= 0 || index > _areas.Count) { ResetSelection(); return; }
        var areaId = _areas[index - 1].AreaId;
        _portals = _session.PortalChoicesUx2NavF(areaId);
        foreach (var item in _portals) _portal.AddItem(item.Label);
        _portal.Select(0); _newPortal.Disabled = _session.CreateNewPortalDraftUx2NavF(areaId) is null;
        SetStatus("Zone choisie · sélectionnez un passage existant ou créez-en un.", false);
        UpdateButtons();
    }

    private void SelectPortal(int index)
    {
        if (_updating || _area.Selected <= 0) return;
        var areaId = _areas[_area.Selected - 1].AreaId;
        _draft = index <= 0 || index > _portals.Count ? null :
            _session.CreatePortalDraftUx2NavF(areaId, _portals[index - 1].PortalId);
        LoadDraft();
    }

    private void CreateNewPortal()
    {
        if (_area.Selected <= 0) return;
        _portal.Select(0);
        _draft = _session.CreateNewPortalDraftUx2NavF(_areas[_area.Selected - 1].AreaId);
        LoadDraft();
    }

    private void LoadDraft()
    {
        _updating = true;
        _lastPreview = null; _canonicalPreview = false;
        if (_draft is null) { _updating = false; ResetControls(); UpdateButtons(); return; }
        _sourceEntities = _session.SourceEntityChoicesUx2NavF(_draft.SourceAreaId);
        _destinations = _session.DestinationChoicesUx2NavF(_draft.SourceAreaId);
        _sourceEntity.Clear(); foreach (var item in _sourceEntities) _sourceEntity.AddItem(item.Label);
        _destination.Clear(); foreach (var item in _destinations) _destination.AddItem(item.Label);
        _name.Text = _draft.DisplayName;
        _sourceEntity.Select(Math.Max(0, _sourceEntities.ToList().FindIndex(item => item.EntityId == _draft.SourceEntityId)));
        _destination.Select(Math.Max(0, _destinations.ToList().FindIndex(item =>
            item.AreaId == _draft.DestinationAreaId && item.EntityId == _draft.DestinationEntityId)));
        _bidirectional.ButtonPressed = _draft.Bidirectional; _preload.ButtonPressed = _draft.PreloadDestination;
        _orientation.Select((int)_draft.Orientation); _yaw.Value = _draft.FixedYawDegrees;
        _velocity.Select((int)_draft.Velocity); _beforeScript.Text = _draft.BeforeTransitionScript;
        _afterScript.Text = _draft.AfterTransitionScript; _hooksRequired.ButtonPressed = _draft.HooksRequiredAtRuntime;
        _remove.ButtonPressed = _draft.Remove; _remove.Disabled = _draft.IsNew;
        _updating = false;
        SetStatus(_draft.IsNew ? "Nouveau passage prêt à configurer." : "Passage sélectionné explicitement.", false);
        UpdateButtons();
    }

    private StudioUx2NavFDraft? ReadDraft()
    {
        if (_draft is null || _sourceEntity.Selected < 0 || _sourceEntity.Selected >= _sourceEntities.Count ||
            _destination.Selected < 0 || _destination.Selected >= _destinations.Count) return null;
        var destination = _destinations[_destination.Selected];
        return _draft with
        {
            Remove = _remove.ButtonPressed,
            DisplayName = _name.Text,
            SourceEntityId = _sourceEntities[_sourceEntity.Selected].EntityId,
            DestinationAreaId = destination.AreaId,
            DestinationEntityId = destination.EntityId,
            Bidirectional = _bidirectional.ButtonPressed,
            PreloadDestination = _preload.ButtonPressed,
            Orientation = (PortalOrientationPolicy)_orientation.Selected,
            FixedYawDegrees = _yaw.Value,
            Velocity = (PortalVelocityPolicy)_velocity.Selected,
            BeforeTransitionScript = _beforeScript.Text,
            AfterTransitionScript = _afterScript.Text,
            HooksRequiredAtRuntime = _hooksRequired.ButtonPressed,
        };
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null) { SetStatus("Choisissez un départ et une destination.", true); return; }
        _draft = draft;
        _lastPreview = _session.PreviewPortalUx2NavF(draft);
        _canonicalPreview = _lastPreview.Succeeded;
        RenderPreview();
        SetStatus(_lastPreview.Message, !_lastPreview.Succeeded);
        UpdateButtons();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedPortalUx2NavF();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { TransitionChanged?.Invoke(); ReloadSelectedArea(); }
        UpdateButtons();
    }

    private void Undo()
    {
        var result = _session.Undo(); SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { TransitionChanged?.Invoke(); ReloadSelectedArea(); }
        UpdateButtons();
    }

    private void Redo()
    {
        var result = _session.Redo(); SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { TransitionChanged?.Invoke(); ReloadSelectedArea(); }
        UpdateButtons();
    }

    private void Save()
    {
        var result = _session.Save(); SetStatus(result.Message, !result.Succeeded); UpdateButtons();
    }

    private void ReloadSelectedArea()
    {
        var areaId = _draft?.SourceAreaId;
        PopulateAreas();
        var index = _areas.ToList().FindIndex(item => item.AreaId == areaId);
        if (index >= 0) { _area.Select(index + 1); SelectArea(index + 1); }
        _draft = null; _lastPreview = null; _canonicalPreview = false;
    }

    private void RenderPreview()
    {
        Clear(_diff); Clear(_runtime); Clear(_diagnostics);
        if (_lastPreview is null) return;
        foreach (var line in _lastPreview.Diff) _diff.AddChild(StudioStyle.Label("• " + line, 10, null, wrap: true));
        _runtime.AddChild(StudioStyle.Label(_lastPreview.Summary, 10, StudioStyle.Accent, wrap: true));
        _runtime.AddChild(StudioStyle.Label("Aucun acteur, moteur, script, préchargement ou réseau n’a été exécuté.",
            10, StudioStyle.Muted, wrap: true));
        if (_lastPreview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 10, StudioStyle.Success));
        foreach (var item in _lastPreview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label("• " + item.Message, 10, StudioStyle.Warning, wrap: true));
    }

    private void ConnectInvalidation()
    {
        _name.TextChanged += _ => Invalidate(); _sourceEntity.ItemSelected += _ => Invalidate();
        _destination.ItemSelected += _ => Invalidate(); _bidirectional.Toggled += _ => Invalidate();
        _preload.Toggled += _ => Invalidate(); _orientation.ItemSelected += _ => Invalidate();
        _yaw.ValueChanged += _ => Invalidate(); _velocity.ItemSelected += _ => Invalidate();
        _beforeScript.TextChanged += _ => Invalidate(); _afterScript.TextChanged += _ => Invalidate();
        _hooksRequired.Toggled += _ => Invalidate(); _remove.Toggled += _ => Invalidate();
    }

    private void Invalidate()
    {
        if (_updating) return;
        _session.CancelPortalApprovalUx2NavF(); _lastPreview = null; _canonicalPreview = false;
        UpdateButtons();
    }

    private void ResetSelection()
    {
        _portal.Clear(); _portal.AddItem("Choisir un passage…"); _portals = []; _draft = null;
        _newPortal.Disabled = true; ResetControls(); SetStatus("Choisissez explicitement une zone puis un passage.", false);
        UpdateButtons();
    }

    private void ResetControls()
    {
        _updating = true;
        _name.Text = string.Empty; _sourceEntity.Clear(); _destination.Clear();
        _bidirectional.ButtonPressed = false; _preload.ButtonPressed = false;
        _orientation.Select(0); _yaw.Value = 0; _velocity.Select(0);
        _beforeScript.Text = string.Empty; _afterScript.Text = string.Empty;
        _hooksRequired.ButtonPressed = false; _remove.ButtonPressed = false; _remove.Disabled = true;
        _updating = false; Clear(_diff); Clear(_runtime); Clear(_diagnostics);
    }

    private void Reset()
    {
        _areas = []; _portals = []; _sourceEntities = []; _destinations = [];
        _area.Clear(); _area.AddItem("Choisir une zone…"); ResetSelection();
    }

    private void UpdateButtons()
    {
        var selected = _draft is not null;
        _preview.Disabled = !selected;
        _apply.Disabled = !selected || !_canonicalPreview || _lastPreview?.CanApply != true;
        _undo.Disabled = !_session.CanUndo; _redo.Disabled = !_session.CanRedo;
        _save.Disabled = !_session.IsDirty;
    }

    private StudioUx2NavFEvidenceSourceSnapshot CaptureSource() => new(
        _sourceSession.Project!, _sourceSession.History!, _sourceSession.History!.State.Revision,
        _sourceSession.History.Journal.ToArray(), _sourceSession.IsDirty, _sourceSession.History.CanUndo,
        _sourceSession.History.CanRedo, _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(),
        _sourceSession.SelectedEntityId, _sourceSession.CaptureNavigationApprovalUx2NavB(),
        _sourceSession.CaptureWaypointApprovalUx2NavC(), _sourceSession.CapturePatrolApprovalUx2NavD(),
        _sourceSession.CapturePortalApprovalUx2NavF());

    private bool SourceMatches(StudioUx2NavFEvidenceSourceSnapshot snapshot) =>
        ReferenceEquals(_sourceSession.Project, snapshot.Project) && ReferenceEquals(_sourceSession.History, snapshot.History) &&
        _sourceSession.History?.State.Revision == snapshot.Revision &&
        (_sourceSession.History?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
        _sourceSession.IsDirty == snapshot.IsDirty && _sourceSession.History?.CanUndo == snapshot.CanUndo &&
        _sourceSession.History?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
        _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
        _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
        _sourceSession.CaptureNavigationApprovalUx2NavB() == snapshot.NavigationApproval &&
        _sourceSession.CaptureWaypointApprovalUx2NavC() == snapshot.WaypointApproval &&
        _sourceSession.CapturePatrolApprovalUx2NavD() == snapshot.PatrolApproval &&
        _sourceSession.CapturePortalApprovalUx2NavF() == snapshot.PortalApproval;

    private bool ReleaseEvidence()
    {
        var snapshot = _sourceSnapshot;
        var controller = _evidenceSession;
        var root = _evidenceRoot;
        _evidenceSession = null; _evidenceRoot = null; _session = _sourceSession;
        controller?.Close();
        var released = StudioSessionController.DeleteUx2NavFTemporaryWorkspace(root);
        _lastSourceUntouched = snapshot is not null && SourceMatches(snapshot);
        _lastEvidenceReleased = released; _lastSourceRestored = ReferenceEquals(_session, _sourceSession);
        _sourceSnapshot = null;
        return released;
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message; _status.Modulate = error ? StudioStyle.Warning : StudioStyle.Success;
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(Math.Max(24, size.X * .12f), Math.Max(20, size.Y * .045f));
        _dialog.Size = new Vector2(Math.Max(980, size.X * .76f), Math.Max(700, size.Y * .91f));
    }

    private static OptionButton Option()
    {
        var value = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(value); return value;
    }

    private static CheckBox Check(string text) => new() { Text = text };

    private static Button Action(string text)
    {
        var value = StudioStyle.Button(text, compact: true); value.CustomMinimumSize = new Vector2(110, 34); return value;
    }

    private static void AddField(GridContainer grid, string label, Control control)
    {
        grid.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted)); grid.AddChild(control);
    }

    private static PanelContainer Card(string name, string title, string help, Control content)
    {
        var panel = StudioStyle.Panel(name, StudioStyle.Surface, 8, 1);
        var margin = StudioStyle.Margin(12, 10, 12, 10); panel.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(StudioStyle.Label(title, 13));
        root.AddChild(StudioStyle.Label(help, 10, StudioStyle.Muted, wrap: true));
        root.AddChild(content); margin.AddChild(root); return panel;
    }

    private static void Clear(Node root)
    {
        foreach (var child in root.GetChildren()) { root.RemoveChild(child); child.QueueFree(); }
    }

    private static IEnumerable<string> VisibleText(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label) yield return label.Text;
            else if (child is Button button) yield return button.Text;
            foreach (var nested in VisibleText(child)) yield return nested;
        }
    }
}
