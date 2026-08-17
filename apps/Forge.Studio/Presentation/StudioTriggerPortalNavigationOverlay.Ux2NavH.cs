using Forge.Domain.Navigation;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioTriggerPortalNavigationOverlayUx2NavH : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioUx2NavHEvidenceSourceSnapshot? _sourceSnapshot;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _trigger;
    private readonly CheckBox _enabled;
    private readonly OptionButton _destination;
    private readonly OptionButton _arrival;
    private readonly SpinBox _orientationX;
    private readonly SpinBox _orientationY;
    private readonly SpinBox _orientationZ;
    private readonly CheckBox _preload;
    private readonly LineEdit _permissions;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _runtime;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2NavHTriggerChoice> _triggers = [];
    private IReadOnlyList<StudioUx2NavHDestinationChoice> _destinations = [];
    private IReadOnlyList<StudioUx2NavHArrivalChoice> _arrivals = [];
    private StudioUx2NavHDraft? _draft;
    private StudioUx2NavHPreview? _lastPreview;
    private bool _updating;
    private bool _canonicalPreview;
    private bool _lastEvidenceReleased;
    private bool _lastSourceRestored = true;
    private bool _lastSourceUntouched;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? TriggerPortalChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _triggers.Count;
    internal bool HasExplicitSelection => _draft is not null && _trigger.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreview && _lastPreview?.Succeeded == true;
    internal bool ShowsRuntimeResolution => _lastPreview is
    {
        NoExternalRuntimeExecuted: true,
        DestinationResolved: true,
        TriggerFired: true,
        PermissionPolicyValidated: true,
        ValidationContextSynchronized: true,
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
            return text.Contains("Transitions de déclencheur", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Destination", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Permissions", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun moteur", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text);
        }
    }

    internal StudioTriggerPortalNavigationOverlayUx2NavH(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = session;
        Name = "StudioTriggerPortalNavigationOverlayUx2NavH";
        Visible = false;
        ZIndex = 479;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .82f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _dialog = StudioStyle.Panel("TriggerPortalNavigationDialogUx2NavH", StudioStyle.Chrome, 12, 2);
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
        heading.AddChild(StudioStyle.Label("NAVIGATION · TRANSITIONS DE DÉCLENCHEUR", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Transitions de déclencheur", 20));
        heading.AddChild(StudioStyle.Label(
            "TRG-003 · destination et ancre humaines · orientation XYZ · préchargement déclaré · " +
            "permissions résolues · fondation UX1-Y préservée",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);

        _trigger = Option();
        root.AddChild(_trigger);
        _trigger.ItemSelected += index => SelectTrigger((int)index);

        var host = new Control
        {
            CustomMinimumSize = new Vector2(0, 500),
            CustomMaximumSize = new Vector2(-1, 760),
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

        _enabled = Check("Activer la transition sur entrée du volume");
        var enableBox = new VBoxContainer();
        enableBox.AddChild(_enabled);
        enableBox.AddChild(StudioStyle.Label(
            "Désactiver retire seulement le portail ; géométrie, filtres, actions et événements restent identiques.",
            10, StudioStyle.Muted, wrap: true));
        body.AddChild(Card("TriggerPortalModeUx2NavH", "Cycle de transition",
            "VolumeEntered reste l’événement canonique qui autorise la demande de transition.", enableBox));

        var destinationGrid = Grid();
        _destination = Option();
        _arrival = Option();
        AddField(destinationGrid, "Destination typée", _destination);
        AddField(destinationGrid, "Ancre d’arrivée", _arrival);
        destinationGrid.AddChild(StudioStyle.Label("Identités", 10, StudioStyle.Muted));
        destinationGrid.AddChild(StudioStyle.Label(
            "Les identités canoniques restent internes ; seuls les noms lisibles sont affichés.",
            10, StudioStyle.Muted, wrap: true));
        body.AddChild(Card("TriggerPortalDestinationUx2NavH", "Destination et arrivée",
            "Le catalogue Navigation vérifie la destination typée et le slug global de l’ancre.",
            destinationGrid));

        var orientationGrid = Grid();
        _orientationX = Number(-360, 360, 1);
        _orientationY = Number(-360, 360, 1);
        _orientationZ = Number(-360, 360, 1);
        _preload = Check("Demander le préchargement de la destination");
        AddField(orientationGrid, "Rotation X (°)", _orientationX);
        AddField(orientationGrid, "Rotation Y (°)", _orientationY);
        AddField(orientationGrid, "Rotation Z (°)", _orientationZ);
        orientationGrid.AddChild(_preload);
        orientationGrid.AddChild(StudioStyle.Label(
            "Le préchargement est enregistré comme intention ; cette page ne charge aucune zone.",
            10, StudioStyle.Muted, wrap: true));
        body.AddChild(Card("TriggerPortalOrientationUx2NavH", "Orientation et préchargement",
            "Les trois axes restent indépendants de Godot et aucun déplacement n’est exécuté.", orientationGrid));

        var permissionGrid = Grid();
        _permissions = new LineEdit
        {
            PlaceholderText = "travel,chapter.two",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(480, 0),
        };
        AddField(permissionGrid, "Permissions", _permissions);
        permissionGrid.AddChild(StudioStyle.Label("Résolution", 10, StudioStyle.Muted));
        var permissionResolution = StudioStyle.Label(
            "L’aperçu vérifie une autorisation et un refus en mémoire lorsqu’une permission est requise.",
            10, StudioStyle.Muted, wrap: true);
        permissionResolution.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        permissionResolution.CustomMinimumSize = new Vector2(480, 0);
        permissionGrid.AddChild(permissionResolution);
        body.AddChild(Card("TriggerPortalPermissionsUx2NavH", "Permissions de passage",
            "Séparez les permissions par une virgule sans espaces ; aucune autorité ou règle réseau n’est créée ici.",
            permissionGrid));

        _diff = new VBoxContainer();
        body.AddChild(Card("TriggerPortalDiffUx2NavH", "Diff humain avant application",
            "Le résumé est borné au portail et à sa destination connue associée.", _diff));
        _runtime = new VBoxContainer();
        body.AddChild(Card("TriggerPortalRuntimeUx2NavH", "Aperçu runtime en lecture seule",
            "Le vrai resolver de volume et le bridge de transition pur sont exécutés en mémoire.", _runtime));
        _diagnostics = new VBoxContainer();
        body.AddChild(Card("TriggerPortalDiagnosticsUx2NavH", "Diagnostics localisés",
            "Les codes et chemins techniques restent internes à la validation.", _diagnostics));

        _status = StudioStyle.Label("Choisissez explicitement un déclencheur…", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "Aperçu sec : aucun moteur, réseau, script, téléportation ou préchargement réel n’est exécuté.",
            10, StudioStyle.Muted, wrap: true));
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        _preview = ActionButton("Aperçu sec");
        _apply = ActionButton("Appliquer");
        _undo = ActionButton("Annuler");
        _redo = ActionButton("Rétablir");
        _save = ActionButton("Enregistrer");
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
        _session.CancelTriggerPortalApprovalUx2NavH();
        _lastSourceRestored = true;
        Visible = true;
        PopulateTriggers();
        ResetSelection();
        UpdateButtons();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && ReferenceEquals(_session, _sourceSession)) return;
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidence();
        else _session.CancelTriggerPortalApprovalUx2NavH();
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
        PopulateTriggers();
        ResetSelection();
        UpdateButtons();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        ReleaseEvidence();
        if (_sourceSession.Project is null || _sourceSession.History is null) return false;
        _sourceSnapshot = CaptureSource();
        var fixture = _sourceSession.CreateUx2NavHEvidenceProject();
        if (fixture is null) return false;
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-nav-h-evidence-" + Guid.NewGuid().ToString("N"));
        var controller = new StudioSessionController();
        var created = ProjectSession.Create(root, fixture);
        if (!created.Succeeded)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_NAV_H_EVIDENCE_CREATE_FAILED " +
                        string.Join(" | ", created.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
            controller.Close();
            StudioSessionController.DeleteUx2NavHTemporaryWorkspace(root);
            return false;
        }
        var opened = controller.Open(root);
        if (!opened.Succeeded)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_NAV_H_EVIDENCE_OPEN_FAILED " +
                        string.Join(" | ", opened.Diagnostics.Select(item => $"{item.Code}:{item.Message}")));
            controller.Close();
            StudioSessionController.DeleteUx2NavHTemporaryWorkspace(root);
            return false;
        }
        _evidenceSession = controller;
        _evidenceRoot = root;
        _session = controller;
        _lastEvidenceReleased = false;
        _lastSourceRestored = false;
        Visible = true;
        PopulateTriggers();
        var triggerIndex = _triggers.ToList().FindIndex(item => item.CanEdit &&
            item.Label.Contains("brumes", StringComparison.CurrentCultureIgnoreCase));
        if (triggerIndex < 0)
        {
            ReleaseEvidence();
            return false;
        }
        _trigger.Select(triggerIndex + 1);
        SelectTrigger(triggerIndex + 1);
        if (_draft is null)
        {
            ReleaseEvidence();
            return false;
        }
        var destinationIndex = _destinations.ToList().FindIndex(item =>
            item.Kind == NavigationDestinationKind.SpawnAnchor &&
            item.Label.Contains("lisière", StringComparison.CurrentCultureIgnoreCase));
        var arrivalIndex = _arrivals.ToList().FindIndex(item => item.AnchorKey == "lisiere-arrivee");
        if (destinationIndex < 0 || arrivalIndex < 0)
        {
            ReleaseEvidence();
            return false;
        }
        _enabled.ButtonPressed = true;
        _destination.Select(destinationIndex + 1);
        _arrival.Select(arrivalIndex + 1);
        _orientationX.Value = 5;
        _orientationY.Value = 135;
        _orientationZ.Value = -2;
        _preload.ButtonPressed = false;
        _permissions.Text = "travel,chapter.two";
        UpdateEnabledState();
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
        if (!PrepareEvidence("ux2-nav-h")) return false;
        var applied = _session.ApplyApprovedTriggerPortalUx2NavH();
        var undo = _session.Undo();
        var redo = _session.Redo();
        var secondUndo = _session.Undo();
        var ok = applied.Succeeded && undo.Succeeded && redo.Succeeded && secondUndo.Succeeded;
        Close();
        return ok && SourceMatches(snapshot) && EvidenceResourcesReleased && SourceSessionRestored;
    }

    private void PopulateTriggers()
    {
        _updating = true;
        _triggers = _session.TriggerPortalChoicesUx2NavH;
        _trigger.Clear();
        _trigger.AddItem("Choisir un déclencheur…");
        foreach (var item in _triggers) _trigger.AddItem(item.Label);
        _trigger.Select(0);
        _updating = false;
    }

    private void SelectTrigger(int index)
    {
        if (_updating) return;
        _session.CancelTriggerPortalApprovalUx2NavH();
        _lastPreview = null;
        _canonicalPreview = false;
        _draft = null;
        if (index <= 0 || index > _triggers.Count)
        {
            ResetSelection();
            return;
        }
        var choice = _triggers[index - 1];
        if (!choice.CanEdit)
        {
            ResetControls();
            SetStatus(choice.Availability, true);
            UpdateButtons();
            return;
        }
        if (!_session.TryCreateTriggerPortalDraftUx2NavH(choice.TriggerId, out _draft, out var message))
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
        _destinations = _session.TriggerDestinationChoicesUx2NavH(_draft.TriggerId);
        _arrivals = _session.TriggerArrivalChoicesUx2NavH();
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
        _enabled.ButtonPressed = _draft.Enabled;
        _destination.Select(destinationIndex < 0 ? 0 : destinationIndex + 1);
        _arrival.Select(arrivalIndex < 0 ? 0 : arrivalIndex + 1);
        _orientationX.Value = _draft.ExitOrientationDegrees.X;
        _orientationY.Value = _draft.ExitOrientationDegrees.Y;
        _orientationZ.Value = _draft.ExitOrientationDegrees.Z;
        _preload.ButtonPressed = _draft.Preload;
        _permissions.Text = string.Join(",", _draft.Permissions);
        UpdateEnabledState();
        _updating = false;
        SetStatus(_draft.Enabled && (destinationIndex < 0 || arrivalIndex < 0)
            ? "Déclencheur sélectionné · choisissez une destination et une ancre canoniques."
            : "Déclencheur sélectionné explicitement · prévisualisez avant application.", false);
        UpdateButtons();
    }

    private StudioUx2NavHDraft? ReadDraft()
    {
        if (_draft is null) return null;
        if (!_enabled.ButtonPressed) return _draft with { Enabled = false };
        if (_destination.Selected <= 0 || _destination.Selected > _destinations.Count ||
            _arrival.Selected <= 0 || _arrival.Selected > _arrivals.Count) return null;
        var destination = _destinations[_destination.Selected - 1];
        var arrival = _arrivals[_arrival.Selected - 1];
        return _draft with
        {
            Enabled = true,
            DestinationKind = destination.Kind,
            DestinationId = destination.Id,
            SpawnAnchorKey = arrival.AnchorKey,
            ExitOrientationDegrees = new Vector3Value((float)_orientationX.Value, (float)_orientationY.Value,
                (float)_orientationZ.Value),
            Preload = _preload.ButtonPressed,
            Permissions = ParseList(_permissions.Text),
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
        _lastPreview = _session.PreviewTriggerPortalUx2NavH(draft);
        _canonicalPreview = _lastPreview.Succeeded;
        RenderPreview();
        SetStatus(_lastPreview.Message, !_lastPreview.Succeeded);
        UpdateButtons();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedTriggerPortalUx2NavH();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            TriggerPortalChanged?.Invoke();
            ReloadSelectedTrigger();
        }
        UpdateButtons();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            TriggerPortalChanged?.Invoke();
            ReloadSelectedTrigger();
        }
        UpdateButtons();
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            TriggerPortalChanged?.Invoke();
            ReloadSelectedTrigger();
        }
        UpdateButtons();
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
        UpdateButtons();
    }

    private void ReloadSelectedTrigger()
    {
        var id = _draft?.TriggerId;
        PopulateTriggers();
        var index = _triggers.ToList().FindIndex(item => item.TriggerId == id);
        if (index >= 0)
        {
            _trigger.Select(index + 1);
            SelectTrigger(index + 1);
        }
        else ResetSelection();
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
            "Aucun moteur, acteur, réseau, script, déplacement ou préchargement réel n’a été exécuté.",
            10, StudioStyle.Muted, wrap: true));
        if (_lastPreview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 10, StudioStyle.Success));
        foreach (var item in _lastPreview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label("• " + item.Message, 10, StudioStyle.Warning, wrap: true));
    }

    private void ConnectInvalidation()
    {
        _enabled.Toggled += _ =>
        {
            UpdateEnabledState();
            Invalidate();
        };
        _destination.ItemSelected += _ => Invalidate();
        _arrival.ItemSelected += _ => Invalidate();
        _orientationX.ValueChanged += _ => Invalidate();
        _orientationY.ValueChanged += _ => Invalidate();
        _orientationZ.ValueChanged += _ => Invalidate();
        _preload.Toggled += _ => Invalidate();
        _permissions.TextChanged += _ => Invalidate();
    }

    private void UpdateEnabledState()
    {
        var enabled = _enabled.ButtonPressed;
        _destination.Disabled = !enabled;
        _arrival.Disabled = !enabled;
        _orientationX.Editable = enabled;
        _orientationY.Editable = enabled;
        _orientationZ.Editable = enabled;
        _preload.Disabled = !enabled;
        _permissions.Editable = enabled;
    }

    private void Invalidate()
    {
        if (_updating) return;
        _session.CancelTriggerPortalApprovalUx2NavH();
        _lastPreview = null;
        _canonicalPreview = false;
        UpdateButtons();
    }

    private void ResetSelection()
    {
        _draft = null;
        ResetControls();
        SetStatus("Choisissez explicitement un déclencheur…", false);
        UpdateButtons();
    }

    private void ResetControls()
    {
        _updating = true;
        _enabled.ButtonPressed = false;
        _destination.Clear();
        _destination.AddItem("Choisir une destination…");
        _arrival.Clear();
        _arrival.AddItem("Choisir une ancre d’arrivée…");
        _orientationX.Value = 0;
        _orientationY.Value = 0;
        _orientationZ.Value = 0;
        _preload.ButtonPressed = false;
        _permissions.Text = string.Empty;
        UpdateEnabledState();
        _updating = false;
        Clear(_diff);
        Clear(_runtime);
        Clear(_diagnostics);
    }

    private void Reset()
    {
        _triggers = [];
        _destinations = [];
        _arrivals = [];
        _trigger.Clear();
        _trigger.AddItem("Choisir un déclencheur…");
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

    private StudioUx2NavHEvidenceSourceSnapshot CaptureSource()
    {
        var project = _sourceSession.Project ??
                      throw new InvalidOperationException("Aucun projet source pour la preuve NAV-H.");
        var history = _sourceSession.History ??
                      throw new InvalidOperationException("Aucun historique source pour la preuve NAV-H.");
        return new(project, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.CaptureTriggerPortalApprovalUx2NavH());
    }

    private bool SourceMatches(StudioUx2NavHEvidenceSourceSnapshot snapshot) =>
        ReferenceEquals(_sourceSession.Project, snapshot.Project) &&
        ReferenceEquals(_sourceSession.History, snapshot.History) &&
        _sourceSession.History?.State.Revision == snapshot.Revision &&
        (_sourceSession.History?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
        _sourceSession.IsDirty == snapshot.IsDirty &&
        _sourceSession.History?.CanUndo == snapshot.CanUndo &&
        _sourceSession.History?.CanRedo == snapshot.CanRedo &&
        _sourceSession.CaptureTriggerPortalApprovalUx2NavH() == snapshot.Approval;

    private bool ReleaseEvidence()
    {
        var snapshot = _sourceSnapshot;
        var controller = _evidenceSession;
        var root = _evidenceRoot;
        _evidenceSession = null;
        _evidenceRoot = null;
        _session = _sourceSession;
        controller?.Close();
        var released = StudioSessionController.DeleteUx2NavHTemporaryWorkspace(root);
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

    private static Button ActionButton(string text)
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

    private static IReadOnlyList<string> ParseList(string value) => string.IsNullOrEmpty(value)
        ? []
        : value.Split([',', ';', '\n', '\r'], StringSplitOptions.None);

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
