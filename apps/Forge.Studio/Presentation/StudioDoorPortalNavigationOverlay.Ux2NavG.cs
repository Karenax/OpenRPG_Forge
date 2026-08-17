using Forge.Domain.Navigation;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioDoorPortalNavigationOverlayUx2NavG : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioUx2NavGEvidenceSourceSnapshot? _sourceSnapshot;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _door;
    private readonly OptionButton _destination;
    private readonly OptionButton _arrival;
    private readonly SpinBox _yaw;
    private readonly CheckBox _preload;
    private readonly LineEdit _permissions;
    private readonly OptionButton _direction;
    private readonly LineEdit _returnLabel;
    private readonly LineEdit _portalApi;
    private readonly LineEdit _obstacle;
    private readonly LineEdit _navigationLink;
    private readonly LineEdit _blockingStates;
    private readonly LineEdit _traversableStates;
    private readonly SpinBox _openingThreshold;
    private readonly SpinBox _closingThreshold;
    private readonly CheckBox _visualize;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _runtime;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2NavGDoorChoice> _doors = [];
    private IReadOnlyList<StudioUx2NavGDestinationChoice> _destinations = [];
    private IReadOnlyList<StudioUx2NavGArrivalChoice> _arrivals = [];
    private StudioUx2NavGDraft? _draft;
    private StudioUx2NavGPreview? _lastPreview;
    private bool _updating;
    private bool _canonicalPreview;
    private bool _lastEvidenceReleased;
    private bool _lastSourceRestored = true;
    private bool _lastSourceUntouched;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? DoorNavigationChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _doors.Count;
    internal bool HasExplicitSelection => _draft is not null && _door.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreview && _lastPreview?.Succeeded == true;
    internal bool ShowsRuntimeResolution => _lastPreview is
    {
        NoRuntimeExecuted: true,
        DestinationResolved: true,
        PortalUseAccepted: true,
        PortalUseVetoed: true,
        DirectionValidated: true,
        DynamicNavigationValidated: true,
    };
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
                                                 _lastPreview.Diagnostics.All(item =>
                                                     !string.IsNullOrWhiteSpace(item.Code) &&
                                                     !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool EvidenceSourceUntouched => _sourceSnapshot is { } snapshot
        ? SourceMatches(snapshot)
        : _lastSourceUntouched;
    internal bool EvidenceResourcesReleased => _evidenceSession is null && _evidenceRoot is null &&
                                                _lastEvidenceReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession) && _lastSourceRestored;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(_dialog));
            return text.Contains("Portails de porte", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Destination", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("PortalUse", StringComparison.Ordinal) &&
                   text.Contains("Navigation dynamique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text);
        }
    }

    internal StudioDoorPortalNavigationOverlayUx2NavG(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = session;
        Name = "StudioDoorPortalNavigationOverlayUx2NavG";
        Visible = false;
        ZIndex = 478;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .82f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _dialog = StudioStyle.Panel("DoorPortalNavigationDialogUx2NavG", StudioStyle.Chrome, 12, 2);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("NAVIGATION · PORTAILS DE PORTE", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Destinations, retours et passage dynamique", 20));
        heading.AddChild(StudioStyle.Label(
            "DOOR-008/009/011/019 · destination et ancre humaines · paire atomique · PortalUse existant · " +
            "obstacle et liaison synchronisés · aucune transition exécutée dans cette surface",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);

        _door = Option();
        root.AddChild(_door);
        _door.ItemSelected += index => SelectDoor((int)index);

        var host = new Control
        {
            CustomMinimumSize = new Vector2(0, 510),
            CustomMaximumSize = new Vector2(-1, 770),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(host);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        host.AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(body);

        var portalGrid = Grid();
        _destination = Option();
        _arrival = Option();
        _yaw = Number(-360, 360, 1);
        _preload = Check("Précharger la destination");
        _permissions = new LineEdit { PlaceholderText = "portal.travel, party.enter" };
        AddField(portalGrid, "Destination typée", _destination);
        AddField(portalGrid, "Ancre d’arrivée", _arrival);
        AddField(portalGrid, "Orientation à l’arrivée (°)", _yaw);
        AddField(portalGrid, "Permissions", _permissions);
        portalGrid.AddChild(_preload);
        portalGrid.AddChild(StudioStyle.Label(
            "Les identités canoniques restent internes ; seuls les noms de zones, portes, points et ancres sont affichés.",
            10, StudioStyle.Muted, wrap: true));
        body.AddChild(Card("DoorPortal", "Destination et arrivée",
            "Le catalogue de navigation résout la destination typée et le slug global de l’ancre.", portalGrid));

        var directionGrid = Grid();
        _direction = Option();
        _direction.AddItem("Sens unique explicite");
        _direction.AddItem("Aller-retour atomique");
        _returnLabel = new LineEdit { PlaceholderText = "Libellé humain du retour" };
        AddField(directionGrid, "Direction entre portes", _direction);
        AddField(directionGrid, "Retour", _returnLabel);
        body.AddChild(Card("DoorDirection", "Lien porte-à-porte",
            "Pour une destination porte, l’aller et le retour sont écrits dans une seule commande et les autres liens restent en place.",
            directionGrid));

        var eventGrid = Grid();
        _portalApi = new LineEdit { PlaceholderText = "1.0.0" };
        AddField(eventGrid, "API ForgeScript", _portalApi);
        eventGrid.AddChild(StudioStyle.Label("Bindings protégés", 10, StudioStyle.Muted));
        eventGrid.AddChild(StudioStyle.Label("PortalUseRequested → veto possible → PortalUseCompleted", 10,
            StudioStyle.Accent, wrap: true));
        body.AddChild(Card("PortalUse", "Interception PortalUse existante",
            "L’identité et les voisins de la fiche d’accès UX1 sont préservés ; aucun verrou, piège ou refus n’est reconstruit.",
            eventGrid));

        var navigationGrid = Grid();
        _obstacle = new LineEdit { PlaceholderText = "nav-obstacle:porte" };
        _navigationLink = new LineEdit { PlaceholderText = "nav-link:porte" };
        _blockingStates = new LineEdit { PlaceholderText = "closed, locked, jammed" };
        _traversableStates = new LineEdit { PlaceholderText = "open, broken" };
        _openingThreshold = Number(0, 1, .01);
        _closingThreshold = Number(0, 1, .01);
        _visualize = Check("Afficher obstacle et liaison dans l’overlay");
        AddField(navigationGrid, "Obstacle", _obstacle);
        AddField(navigationGrid, "Liaison", _navigationLink);
        AddField(navigationGrid, "États bloquants", _blockingStates);
        AddField(navigationGrid, "États traversables", _traversableStates);
        AddField(navigationGrid, "Passable pendant l’ouverture", _openingThreshold);
        AddField(navigationGrid, "Bloquant pendant la fermeture", _closingThreshold);
        navigationGrid.AddChild(_visualize);
        navigationGrid.AddChild(StudioStyle.Label(
            "La simulation vérifie collision, seuils d’animation, obstacle et liaison sans lancer le moteur.",
            10, StudioStyle.Muted, wrap: true));
        body.AddChild(Card("DynamicNavigation", "Navigation dynamique",
            "Les états restent ceux de la fondation UX1 et sont référencés par leur nom lisible.", navigationGrid));

        _diff = new VBoxContainer();
        body.AddChild(Card("DoorPortalDiff", "Diff humain avant application",
            "Seuls le portail, le lien ciblé, la navigation et PortalUse apparaissent dans ce résumé.", _diff));
        _runtime = new VBoxContainer();
        body.AddChild(Card("DoorPortalRuntime", "Aperçu runtime en lecture seule",
            "Destination, demande acceptée, veto et seuils sont résolus en mémoire.", _runtime));
        _diagnostics = new VBoxContainer();
        body.AddChild(Card("DoorPortalDiagnostics", "Diagnostics localisés",
            "Les codes et chemins techniques restent internes à la validation.", _diagnostics));

        _status = StudioStyle.Label("Choisissez explicitement une porte…", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "Commande atomique : portail de porte uniquement · données UX1 et routes Navigation voisines préservées.",
            10, StudioStyle.Muted, wrap: true));
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        _preview = Action("Aperçu sec");
        _apply = Action("Appliquer");
        _undo = Action("Annuler");
        _redo = Action("Rétablir");
        _save = Action("Enregistrer");
        actions.AddChild(_preview);
        actions.AddChild(_apply);
        actions.AddChild(_undo);
        actions.AddChild(_redo);
        actions.AddChild(_save);
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
        GetViewport().SizeChanged += UpdateDialogBounds;
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= UpdateDialogBounds;
        ReleaseEvidence();
    }

    internal void Open()
    {
        ReleaseEvidence();
        _session = _sourceSession;
        _lastSourceRestored = true;
        Visible = true;
        PopulateDoors();
        ResetSelection();
        UpdateButtons();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && ReferenceEquals(_session, _sourceSession)) return;
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidence();
        else _session.CancelDoorNavigationApprovalUx2NavG();
        _session = _sourceSession;
        _lastSourceRestored = true;
        _draft = null;
        _lastPreview = null;
        _canonicalPreview = false;
        UpdateButtons();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        Visible = true;
        PopulateDoors();
        ResetSelection();
        UpdateButtons();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        ReleaseEvidence();
        if (_sourceSession.Project is null || _sourceSession.History is null) return false;
        _sourceSnapshot = CaptureSource();
        var fixture = _sourceSession.CreateUx2NavGEvidenceProject();
        if (fixture is null) return false;
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-g-evidence-" + Guid.NewGuid().ToString("N"));
        var controller = new StudioSessionController();
        if (!ProjectSession.Create(root, fixture).Succeeded || !controller.Open(root).Succeeded)
        {
            controller.Close();
            StudioSessionController.DeleteUx2NavGTemporaryWorkspace(root);
            return false;
        }
        _evidenceSession = controller;
        _evidenceRoot = root;
        _session = controller;
        _lastEvidenceReleased = false;
        _lastSourceRestored = false;
        Visible = true;
        PopulateDoors();
        var doorIndex = _doors.ToList().FindIndex(item => item.CanEdit &&
            item.Label.Contains("brumes", StringComparison.CurrentCultureIgnoreCase));
        if (doorIndex < 0) doorIndex = _doors.ToList().FindIndex(item => item.CanEdit);
        if (doorIndex < 0)
        {
            ReleaseEvidence();
            return false;
        }
        _door.Select(doorIndex + 1);
        SelectDoor(doorIndex + 1);
        if (_draft is null)
        {
            ReleaseEvidence();
            return false;
        }
        var destinationIndex = _destinations.ToList().FindIndex(item =>
            item.Kind == NavigationDestinationKind.Door &&
            item.Label.Contains("belvédère", StringComparison.CurrentCultureIgnoreCase));
        var arrivalIndex = _arrivals.ToList().FindIndex(item =>
            item.AnchorKey == "lisiere-arrivee");
        if (destinationIndex < 0 || arrivalIndex < 0)
        {
            ReleaseEvidence();
            return false;
        }
        _destination.Select(destinationIndex + 1);
        _arrival.Select(arrivalIndex + 1);
        _direction.Select(1);
        _returnLabel.Text = "Retour vers la porte des brumes";
        _yaw.Value = 145;
        _preload.ButtonPressed = false;
        _permissions.Text = "portal.travel, party.enter";
        _portalApi.Text = "1.1.0";
        _obstacle.Text = "nav-obstacle:porte-brumes";
        _navigationLink.Text = "nav-link:porte-brumes";
        _openingThreshold.Value = .8;
        _closingThreshold.Value = .2;
        _visualize.ButtonPressed = true;
        Preview();
        var ready = CanonicalPreviewReady && ShowsRuntimeResolution && ShowsStructuredDiagnostics &&
                    ShowsHumanSurface && UsesIsolatedEvidence && EvidenceSourceUntouched;
        if (!ready) ReleaseEvidence();
        return ready;
    }

    internal bool RunUiTransactionEvidence()
    {
        if (_sourceSession.Project is null || _sourceSession.History is null) return false;
        var snapshot = CaptureSource();
        if (!PrepareEvidence("ux2-nav-g")) return false;
        var applied = _session.ApplyApprovedDoorNavigationUx2NavG();
        var undo = _session.Undo();
        var redo = _session.Redo();
        var secondUndo = _session.Undo();
        var ok = applied.Succeeded && undo.Succeeded && redo.Succeeded && secondUndo.Succeeded;
        Close();
        return ok && SourceMatches(snapshot) && EvidenceResourcesReleased && SourceSessionRestored;
    }

    private void PopulateDoors()
    {
        _updating = true;
        _doors = _session.DoorNavigationChoicesUx2NavG;
        _door.Clear();
        _door.AddItem("Choisir une porte…");
        foreach (var item in _doors) _door.AddItem(item.Label);
        _door.Select(0);
        _updating = false;
    }

    private void SelectDoor(int index)
    {
        if (_updating) return;
        _session.CancelDoorNavigationApprovalUx2NavG();
        _lastPreview = null;
        _canonicalPreview = false;
        _draft = null;
        if (index <= 0 || index > _doors.Count)
        {
            ResetSelection();
            return;
        }
        var choice = _doors[index - 1];
        if (!choice.CanEdit)
        {
            ResetControls();
            SetStatus(choice.Availability, true);
            UpdateButtons();
            return;
        }
        if (!_session.TryCreateDoorNavigationDraftUx2NavG(choice.DoorId, out _draft, out var message))
        {
            ResetControls();
            SetStatus(string.IsNullOrWhiteSpace(message) ? choice.Availability : message, true);
            UpdateButtons();
            return;
        }
        LoadDraft();
    }

    private void LoadDraft()
    {
        _updating = true;
        _lastPreview = null;
        _canonicalPreview = false;
        if (_draft is null)
        {
            _updating = false;
            ResetControls();
            UpdateButtons();
            return;
        }
        _destinations = _session.DoorDestinationChoicesUx2NavG(_draft.DoorId);
        _arrivals = _session.DoorArrivalChoicesUx2NavG();
        _destination.Clear();
        _destination.AddItem("Choisir une destination…");
        foreach (var item in _destinations) _destination.AddItem(item.Label);
        _arrival.Clear();
        _arrival.AddItem("Choisir une ancre d’arrivée…");
        foreach (var item in _arrivals) _arrival.AddItem(item.Label);
        var destinationIndex = _destinations.ToList().FindIndex(item =>
            item.Kind == _draft.DestinationKind && item.Id == _draft.DestinationId);
        var arrivalIndex = _arrivals.ToList().FindIndex(item =>
            string.Equals(item.AnchorKey, _draft.SpawnAnchorKey, StringComparison.Ordinal));
        _destination.Select(destinationIndex < 0 ? 0 : destinationIndex + 1);
        _arrival.Select(arrivalIndex < 0 ? 0 : arrivalIndex + 1);
        _yaw.Value = _draft.ExitYawDegrees;
        _preload.ButtonPressed = _draft.Preload;
        _permissions.Text = string.Join(", ", _draft.Permissions);
        _direction.Select(_draft.Bidirectional ? 1 : 0);
        _returnLabel.Text = _draft.ReturnNavigationLabel;
        _portalApi.Text = _draft.PortalUseForgeScriptApiVersion;
        _obstacle.Text = _draft.ObstacleId;
        _navigationLink.Text = _draft.NavigationLinkId;
        _blockingStates.Text = string.Join(", ", _draft.BlockingStateIds);
        _traversableStates.Text = string.Join(", ", _draft.TraversableStateIds);
        _openingThreshold.Value = _draft.OpeningPassableAt;
        _closingThreshold.Value = _draft.ClosingBlockedAt;
        _visualize.ButtonPressed = _draft.VisualizeInEditor;
        UpdateDirectionState();
        _updating = false;
        SetStatus(destinationIndex < 0 || arrivalIndex < 0
            ? "Porte sélectionnée · choisissez une destination et une ancre canoniques."
            : "Porte sélectionnée explicitement · préparez puis prévisualisez les changements.", false);
        UpdateButtons();
    }

    private StudioUx2NavGDraft? ReadDraft()
    {
        if (_draft is null || _destination.Selected <= 0 || _destination.Selected > _destinations.Count ||
            _arrival.Selected <= 0 || _arrival.Selected > _arrivals.Count)
            return null;
        var destination = _destinations[_destination.Selected - 1];
        var arrival = _arrivals[_arrival.Selected - 1];
        var changedTarget = destination.Kind != _draft.DestinationKind || destination.Id != _draft.DestinationId;
        return _draft with
        {
            DestinationKind = destination.Kind,
            DestinationId = destination.Id,
            SpawnAnchorKey = arrival.AnchorKey,
            ExitYawDegrees = _yaw.Value,
            Preload = _preload.ButtonPressed,
            Permissions = ParseList(_permissions.Text),
            Bidirectional = destination.Kind == NavigationDestinationKind.Door && _direction.Selected == 1,
            ReturnNavigationLabel = _returnLabel.Text,
            LinkPolicyId = changedTarget ? Guid.NewGuid() : _draft.LinkPolicyId,
            ReverseLinkPolicyId = changedTarget ? Guid.NewGuid() : _draft.ReverseLinkPolicyId,
            PortalUseForgeScriptApiVersion = _portalApi.Text,
            ObstacleId = _obstacle.Text,
            NavigationLinkId = _navigationLink.Text,
            BlockingStateIds = ParseList(_blockingStates.Text),
            TraversableStateIds = ParseList(_traversableStates.Text),
            OpeningPassableAt = _openingThreshold.Value,
            ClosingBlockedAt = _closingThreshold.Value,
            VisualizeInEditor = _visualize.ButtonPressed,
        };
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            SetStatus("Choisissez une destination et une ancre d’arrivée.", true);
            return;
        }
        _draft = draft;
        _lastPreview = _session.PreviewDoorNavigationUx2NavG(draft);
        _canonicalPreview = _lastPreview.Succeeded;
        RenderPreview();
        SetStatus(_lastPreview.Message, !_lastPreview.Succeeded);
        UpdateButtons();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedDoorNavigationUx2NavG();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            DoorNavigationChanged?.Invoke();
            ReloadSelectedDoor();
        }
        UpdateButtons();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            DoorNavigationChanged?.Invoke();
            ReloadSelectedDoor();
        }
        UpdateButtons();
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            DoorNavigationChanged?.Invoke();
            ReloadSelectedDoor();
        }
        UpdateButtons();
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
        UpdateButtons();
    }

    private void ReloadSelectedDoor()
    {
        var doorId = _draft?.DoorId;
        PopulateDoors();
        var index = _doors.ToList().FindIndex(item => item.DoorId == doorId);
        if (index >= 0)
        {
            _door.Select(index + 1);
            SelectDoor(index + 1);
        }
        else
        {
            ResetSelection();
        }
    }

    private void RenderPreview()
    {
        Clear(_diff);
        Clear(_runtime);
        Clear(_diagnostics);
        if (_lastPreview is null) return;
        if (_lastPreview.Diff.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucun changement à appliquer.", 10, StudioStyle.Muted));
        foreach (var line in _lastPreview.Diff)
            _diff.AddChild(StudioStyle.Label("• " + line, 10, null, wrap: true));
        _runtime.AddChild(StudioStyle.Label(_lastPreview.Summary, 10, StudioStyle.Accent, wrap: true));
        _runtime.AddChild(StudioStyle.Label(
            "Aucun acteur, script, transition, préchargement, moteur ou réseau n’a été exécuté.",
            10, StudioStyle.Muted, wrap: true));
        if (_lastPreview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 10, StudioStyle.Success));
        foreach (var item in _lastPreview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label("• " + item.Message, 10, StudioStyle.Warning, wrap: true));
    }

    private void ConnectInvalidation()
    {
        _destination.ItemSelected += _ =>
        {
            UpdateDirectionState();
            Invalidate();
        };
        _arrival.ItemSelected += _ => Invalidate();
        _yaw.ValueChanged += _ => Invalidate();
        _preload.Toggled += _ => Invalidate();
        _permissions.TextChanged += _ => Invalidate();
        _direction.ItemSelected += _ =>
        {
            UpdateDirectionState();
            Invalidate();
        };
        _returnLabel.TextChanged += _ => Invalidate();
        _portalApi.TextChanged += _ => Invalidate();
        _obstacle.TextChanged += _ => Invalidate();
        _navigationLink.TextChanged += _ => Invalidate();
        _blockingStates.TextChanged += _ => Invalidate();
        _traversableStates.TextChanged += _ => Invalidate();
        _openingThreshold.ValueChanged += _ => Invalidate();
        _closingThreshold.ValueChanged += _ => Invalidate();
        _visualize.Toggled += _ => Invalidate();
    }

    private void UpdateDirectionState()
    {
        var isDoor = _destination.Selected > 0 && _destination.Selected <= _destinations.Count &&
                     _destinations[_destination.Selected - 1].Kind == NavigationDestinationKind.Door;
        _direction.Disabled = !isDoor;
        if (!isDoor) _direction.Select(0);
        _returnLabel.Editable = isDoor && _direction.Selected == 1;
    }

    private void Invalidate()
    {
        if (_updating) return;
        _session.CancelDoorNavigationApprovalUx2NavG();
        _lastPreview = null;
        _canonicalPreview = false;
        UpdateButtons();
    }

    private void ResetSelection()
    {
        _draft = null;
        ResetControls();
        SetStatus("Choisissez explicitement une porte…", false);
        UpdateButtons();
    }

    private void ResetControls()
    {
        _updating = true;
        _destination.Clear();
        _destination.AddItem("Choisir une destination…");
        _arrival.Clear();
        _arrival.AddItem("Choisir une ancre d’arrivée…");
        _yaw.Value = 0;
        _preload.ButtonPressed = false;
        _permissions.Text = string.Empty;
        _direction.Select(0);
        _direction.Disabled = true;
        _returnLabel.Text = string.Empty;
        _returnLabel.Editable = false;
        _portalApi.Text = string.Empty;
        _obstacle.Text = string.Empty;
        _navigationLink.Text = string.Empty;
        _blockingStates.Text = string.Empty;
        _traversableStates.Text = string.Empty;
        _openingThreshold.Value = .75;
        _closingThreshold.Value = .25;
        _visualize.ButtonPressed = false;
        _updating = false;
        Clear(_diff);
        Clear(_runtime);
        Clear(_diagnostics);
    }

    private void Reset()
    {
        _doors = [];
        _destinations = [];
        _arrivals = [];
        _door.Clear();
        _door.AddItem("Choisir une porte…");
        ResetSelection();
    }

    private void UpdateButtons()
    {
        var selected = _draft is not null;
        _preview.Disabled = !selected;
        _apply.Disabled = !selected || !_canonicalPreview || _lastPreview?.CanApply != true;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = !_session.IsDirty;
    }

    private StudioUx2NavGEvidenceSourceSnapshot CaptureSource()
    {
        var project = _sourceSession.Project ??
                      throw new InvalidOperationException("Aucun projet source pour la preuve NAV-G.");
        var history = _sourceSession.History ??
                      throw new InvalidOperationException("Aucun historique source pour la preuve NAV-G.");
        return new(project, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.CaptureDoorNavigationApprovalUx2NavG());
    }

    private bool SourceMatches(StudioUx2NavGEvidenceSourceSnapshot snapshot) =>
        ReferenceEquals(_sourceSession.Project, snapshot.Project) &&
        ReferenceEquals(_sourceSession.History, snapshot.History) &&
        _sourceSession.History?.State.Revision == snapshot.Revision &&
        (_sourceSession.History?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
        _sourceSession.IsDirty == snapshot.IsDirty &&
        _sourceSession.History?.CanUndo == snapshot.CanUndo &&
        _sourceSession.History?.CanRedo == snapshot.CanRedo &&
        _sourceSession.CaptureDoorNavigationApprovalUx2NavG() == snapshot.Approval;

    private bool ReleaseEvidence()
    {
        var snapshot = _sourceSnapshot;
        var controller = _evidenceSession;
        var root = _evidenceRoot;
        _evidenceSession = null;
        _evidenceRoot = null;
        _session = _sourceSession;
        controller?.Close();
        var released = StudioSessionController.DeleteUx2NavGTemporaryWorkspace(root);
        _lastSourceUntouched = snapshot is not null && SourceMatches(snapshot);
        _lastEvidenceReleased = released;
        _lastSourceRestored = ReferenceEquals(_session, _sourceSession);
        _sourceSnapshot = null;
        return released;
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Warning : StudioStyle.Success;
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(Math.Max(24, size.X * .08f), Math.Max(18, size.Y * .035f));
        _dialog.Size = new Vector2(Math.Max(1120, size.X * .84f), Math.Max(740, size.Y * .93f));
    }

    private static OptionButton Option()
    {
        var value = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(value);
        return value;
    }

    private static SpinBox Number(double minimum, double maximum, double step)
    {
        var value = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = step,
            CustomArrowStep = step,
            AllowGreater = false,
            AllowLesser = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(value);
        return value;
    }

    private static GridContainer Grid()
    {
        var value = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        value.AddThemeConstantOverride("h_separation", 10);
        value.AddThemeConstantOverride("v_separation", 6);
        return value;
    }

    private static CheckBox Check(string text) => new() { Text = text };

    private static Button Action(string text)
    {
        var value = StudioStyle.Button(text, compact: true);
        value.CustomMinimumSize = new Vector2(110, 34);
        return value;
    }

    private static void AddField(GridContainer grid, string label, Control control)
    {
        grid.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        grid.AddChild(control);
    }

    private static PanelContainer Card(string name, string title, string help, Control content)
    {
        var panel = StudioStyle.Panel(name, StudioStyle.Surface, 8, 1);
        var margin = StudioStyle.Margin(12, 10, 12, 10);
        panel.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(StudioStyle.Label(title, 13));
        root.AddChild(StudioStyle.Label(help, 10, StudioStyle.Muted, wrap: true));
        root.AddChild(content);
        margin.AddChild(root);
        return panel;
    }

    private static IReadOnlyList<string> ParseList(string value) => value
        .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(item => item.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static void Clear(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            root.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static IEnumerable<string> VisibleText(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label) yield return label.Text;
            else if (child is LineEdit lineEdit) yield return lineEdit.Text;
            else if (child is Button button) yield return button.Text;
            foreach (var nested in VisibleText(child)) yield return nested;
        }
    }

    private static bool ContainsGuid(string value)
    {
        foreach (var token in value.Split([' ', '\n', '\r', '\t', '·', ',', ';', '(', ')'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Guid.TryParse(token.TrimEnd('.', ':', '!', '?'), out _)) return true;
        return false;
    }
}
