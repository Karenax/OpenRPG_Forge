using System.Text.RegularExpressions;
using Forge.ModuleFormat.Network;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioNetworkResilienceCapacityWorkbenchUx9E : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx9E? _sourceSnapshot;
    private StudioUx9EResilienceCapacityDraft? _draft;
    private StudioUx9EResilienceCapacityPreview? _preview;
    private bool _explicitSelection;
    private bool _updating;
    private bool _canUndo;
    private bool _canRedo;
    private bool _evidenceResourcesReleased = true;
    private bool _sourceSessionRestored = true;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private OptionButton _focusChoice = null!;
    private readonly List<Control> _focusPages = [];
    private SpinBox _snapshotInterval = null!;
    private SpinBox _backupRetention = null!;
    private SpinBox _maximumPlayers = null!;
    private SpinBox _reservedAdministratorSlots = null!;
    private SpinBox _reservationSeconds = null!;
    private CheckBox _approval = null!;
    private CheckBox _probeConfirmation = null!;
    private Label _previewSummary = null!;
    private Label _auditSummary = null!;
    private Label _status = null!;
    private Button _previewButton = null!;
    private Button _applyButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _saveButton = null!;
    private Button _probeButton = null!;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? WorkspaceChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence =>
        _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool HasExplicitSelection => _explicitSelection;
    internal bool PreviewReady => _preview is
    {
        Succeeded: true, CanApply: true, CanonicalDryRun: true,
        RollbackRuntime: true, CapacityRuntime: true
    };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx9E();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx9E(this));
            return Visible && text.Contains("ORF-MP-033", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-034", StringComparison.Ordinal) &&
                   text.Contains("Administrateur", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tester localement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun rollback du module réel", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("WAL", StringComparison.Ordinal) &&
                   text.Contains("PITR", StringComparison.Ordinal) &&
                   text.Contains("file d’attente", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx9E(text);
        }
    }

    internal StudioNetworkResilienceCapacityWorkbenchUx9E(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx9E();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9E();
    }

    internal void Close()
    {
        if (_evidenceSession is not null)
        {
            CloseEvidence();
            return;
        }
        if (!Visible) return;
        Visible = false;
        _session.CancelResilienceCapacityApprovalUx9E();
        ResetUx9E();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx9E(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-e-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        var baseline = NetworkResilienceCapacityProfile.CreateDefault("evidence:ux9-e-ui");
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !WorkspaceNetworkResilienceCapacityStore.Save(_evidenceRoot, baseline).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx9E();
            RestoreSourceSessionUx9E();
            return false;
        }
        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx9E();
        _moduleChoice.Select(1);
        SelectModuleUx9E(1);
        ConfigureEvidenceUx9E(mode);
        PreviewCurrentUx9E();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady && ShowsHumanSurface &&
                    EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_E_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
                 $"isolated={UsesIsolatedEvidence.ToString().ToLowerInvariant()} " +
                 $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                 $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                 $"source_untouched={EvidenceSourceUntouched.ToString().ToLowerInvariant()}");
        if (!ready) CloseEvidence();
        return ready;
    }

    internal bool RunUiEvidence()
    {
        if (!UsesIsolatedEvidence || !PreviewReady || _evidenceRoot is null) return false;
        var draft = ReadDraftUx9E();
        if (draft is null) return false;
        var preview = _session.PreviewResilienceCapacityUx9E(draft);
        var applied = preview.CanApply && _approval.ButtonPressed &&
                      _session.ApplyApprovedResilienceCapacityUx9E(draft).Succeeded;
        var undo = applied && _session.UndoResilienceCapacityUx9E().Succeeded;
        var redo = undo && _session.RedoResilienceCapacityUx9E().Succeeded;
        var save = redo && _session.SaveResilienceCapacityUx9E().Succeeded;
        var probed = save && _probeConfirmation.ButtonPressed &&
                     _session.RunLocalResilienceCapacityUx9E(true, NetworkRole.Administrator).Succeeded;
        var expected = _session.CurrentResilienceCapacityUx9E;
        var reopened = new StudioSessionController();
        var reopenedOk = probed && expected is not null && reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.CurrentResilienceCapacityUx9E is { } actual &&
                         string.Equals(new NetworkResilienceCapacityStore().Serialize(expected),
                             new NetworkResilienceCapacityStore().Serialize(actual),
                             StringComparison.Ordinal);
        reopened.Close();
        RefreshAuditUx9E();
        return preview.CanonicalDryRun && applied && undo && redo && save && probed && reopenedOk &&
               _session.ResilienceCapacityProbeUx9E.ResourcesReleased && EvidenceSourceUntouched;
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is not null)
        {
            _evidenceSession.Close();
            _evidenceSession = null;
        }
        Visible = false;
        ResetUx9E();
        DeleteEvidenceUx9E();
        RestoreSourceSessionUx9E();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9E();
        _moduleChoice.Select(1);
        SelectModuleUx9E(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx9E();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx9E;
        UpdateBoundsUx9E();
    }

    private void BuildUiUx9E()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color("101410f2");
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _dialog = StudioStyle.Panel("Ux9EResilienceCapacity", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var root = new VBoxContainer
        {
            Name = "Ux9ERoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 6);
        _dialog.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(headingText);
        headingText.AddChild(StudioStyle.Label("Réseau · résilience et capacité", 20,
            StudioStyle.Text));
        headingText.AddChild(StudioStyle.Label(
            "Préparer la restauration serveur et l’admission des joueurs sous autorité Administrateur.",
            11, StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);

        var routes = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        routes.AddThemeConstantOverride("separation", 8);
        root.AddChild(routes);
        routes.AddChild(RouteCardUx9E("ORF-MP-033", "Sauvegarde et rollback",
            "Snapshots, WAL, répétition, migrations, PITR et rétention."));
        routes.AddChild(RouteCardUx9E("ORF-MP-034", "Capacité et file d’attente",
            "Plafond configurable, slots réservés, position, délestage et métriques."));
        root.AddChild(StudioStyle.Label(
            "Limite : sondes en mémoire sur copie uniquement · aucun rollback du module réel, stockage externe, serveur public ou socket.",
            10, StudioStyle.Warning));

        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _moduleChoice.ItemSelected += index => SelectModuleUx9E((int)index);
        root.AddChild(LabeledRowUx9E("Module explicite", _moduleChoice));
        _focusChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _focusChoice.AddItem("Sauvegarde et rollback serveur");
        _focusChoice.AddItem("Capacité et file d’attente");
        _focusChoice.ItemSelected += index => SelectFocusUx9E((int)index);
        root.AddChild(LabeledRowUx9E("Workflow affiché", _focusChoice));

        var editor = StudioStyle.Panel("Ux9EEditor", StudioStyle.SurfaceRaised, 9, 1);
        editor.CustomMinimumSize = new Vector2(0, 300);
        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(editor);
        var editorStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        editorStack.AddThemeConstantOverride("separation", 7);
        editor.AddChild(editorStack);
        editorStack.AddChild(StudioStyle.Label("Édition directe de la politique sélectionnée", 14,
            StudioStyle.Text));
        BuildRollbackPageUx9E(editorStack);
        BuildCapacityPageUx9E(editorStack);

        var confirmations = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        confirmations.AddThemeConstantOverride("separation", 18);
        root.AddChild(confirmations);
        _approval = CheckUx9E(confirmations,
            "J’approuve ce seul aperçu sec pour la révision affichée");
        _probeConfirmation = CheckUx9E(confirmations,
            "Je confirme la répétition locale sur copie en rôle Administrateur");

        var review = StudioStyle.Panel("Ux9EReview", StudioStyle.SurfaceRaised, 7, 1);
        review.CustomMinimumSize = new Vector2(0, 132);
        root.AddChild(review);
        var reviewStack = new VBoxContainer();
        review.AddChild(reviewStack);
        _previewSummary = StudioStyle.Label(
            "Prévisualisez la configuration · aucune restauration ni file exécutée.", 10,
            StudioStyle.Muted);
        _previewSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        reviewStack.AddChild(_previewSummary);
        _auditSummary = StudioStyle.Label("Journal local · aucune entrée", 10, StudioStyle.Muted);
        _auditSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        reviewStack.AddChild(_auditSummary);
        _status = StudioStyle.Label("Sélectionnez explicitement le module.", 10, StudioStyle.Muted);
        root.AddChild(_status);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        _previewButton = ActionButtonUx9E(actions, "Prévisualiser", PreviewCurrentUx9E, 132, true);
        _applyButton = ActionButtonUx9E(actions, "Appliquer", ApplyCurrentUx9E, 104);
        _undoButton = ActionButtonUx9E(actions, "Annuler", UndoUx9E, 92);
        _redoButton = ActionButtonUx9E(actions, "Rétablir", RedoUx9E, 92);
        _saveButton = ActionButtonUx9E(actions, "Enregistrer", SaveUx9E, 112);
        _probeButton = ActionButtonUx9E(actions, "Tester localement", ProbeUx9E, 144);
        ConnectInvalidationUx9E();
        SelectFocusUx9E(0);
        UpdateActionsUx9E();
    }

    private void BuildRollbackPageUx9E(VBoxContainer parent)
    {
        var page = FocusPageUx9E(parent, "Restauration répétable et explicitement confirmée");
        var grid = GridUx9E(page);
        _snapshotInterval = SpinUx9E(grid, "Intervalle snapshot (minutes)", 1, 1440, 1);
        _backupRetention = SpinUx9E(grid, "Rétention (jours)", 1, 3650, 1);
        page.AddChild(StudioStyle.Label(
            "Imposé : WAL, répétition de restauration, migrations, PITR et confirmation destructive.",
            10, StudioStyle.Success));
        page.AddChild(StudioStyle.Label(
            "La prévisualisation construit une copie mémoire ; seul Tester localement applique puis vérifie un rollback sur cette copie.",
            10, StudioStyle.Muted));
    }

    private void BuildCapacityPageUx9E(VBoxContainer parent)
    {
        var page = FocusPageUx9E(parent, "Admission bornée et observable des joueurs");
        var grid = GridUx9E(page);
        _maximumPlayers = SpinUx9E(grid, "Capacité maximale", 1, 100_000, 1);
        _reservedAdministratorSlots = SpinUx9E(grid, "Slots Administrateur réservés", 0, 99_999, 1);
        _reservationSeconds = SpinUx9E(grid, "Réservation (secondes)", 1, 3600, 1);
        page.AddChild(StudioStyle.Label(
            "Imposé : file FIFO, position visible, délestage et métriques ; le plafond est configurable, jamais promis comme fixe.",
            10, StudioStyle.Success));
        page.AddChild(StudioStyle.Label(
            "La capacité publique est calculée après retrait des slots Administrateur réservés.",
            10, StudioStyle.Muted));
    }

    private VBoxContainer FocusPageUx9E(VBoxContainer parent, string title)
    {
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 7);
        page.AddChild(StudioStyle.Label(title, 12, StudioStyle.Text));
        parent.AddChild(page);
        _focusPages.Add(page);
        return page;
    }

    private void OpenCurrentUx9E()
    {
        Visible = true;
        ResetUx9E();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionner un module");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(
                _session.Project.Module.DisplayName));
        _moduleChoice.Select(0);
        RefreshAuditUx9E();
        UpdateBoundsUx9E();
        UpdateActionsUx9E();
    }

    private void SelectModuleUx9E(int index)
    {
        _explicitSelection = index == 1 && _session.Project is not null;
        _session.CancelResilienceCapacityApprovalUx9E();
        _preview = null;
        _approval.ButtonPressed = false;
        if (!_explicitSelection)
        {
            _draft = null;
            SetStatusUx9E("Sélectionnez explicitement le module.", false);
            UpdateActionsUx9E();
            return;
        }
        _draft = _session.CreateResilienceCapacityDraftUx9E(explicitlySelected: true);
        if (_draft is null)
        {
            SetStatusUx9E(_session.ResilienceCapacityLoadMessageUx9E ??
                          "La résilience réseau est indisponible.", true);
            UpdateActionsUx9E();
            return;
        }
        PopulateDraftUx9E(_draft);
        SetStatusUx9E(_session.ResilienceCapacityFileMissingUx9E
            ? "Profil initial chargé en mémoire · aucun fichier créé avant Enregistrer."
            : "Résilience et capacité chargées · prévisualisez avant toute application.", false);
        UpdateActionsUx9E();
    }

    private void PopulateDraftUx9E(StudioUx9EResilienceCapacityDraft draft)
    {
        _updating = true;
        _snapshotInterval.Value = draft.SnapshotIntervalMinutes;
        _backupRetention.Value = draft.BackupRetentionDays;
        _maximumPlayers.Value = draft.MaximumPlayers;
        _reservedAdministratorSlots.Value = draft.ReservedAdministratorSlots;
        _reservationSeconds.Value = draft.ReservationSeconds;
        _updating = false;
    }

    private StudioUx9EResilienceCapacityDraft? ReadDraftUx9E() => _draft is null ? null : _draft with
    {
        SnapshotIntervalMinutes = (int)_snapshotInterval.Value,
        BackupRetentionDays = (int)_backupRetention.Value,
        MaximumPlayers = (int)_maximumPlayers.Value,
        ReservedAdministratorSlots = (int)_reservedAdministratorSlots.Value,
        ReservationSeconds = (int)_reservationSeconds.Value
    };

    private void PreviewCurrentUx9E()
    {
        var draft = ReadDraftUx9E();
        if (draft is null)
        {
            SetStatusUx9E("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewResilienceCapacityUx9E(draft);
        PopulatePreviewUx9E();
        SetStatusUx9E(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx9E();
    }

    private void ApplyCurrentUx9E()
    {
        var draft = ReadDraftUx9E();
        if (draft is null || !_approval.ButtonPressed) return;
        var focus = _focusChoice.Selected;
        var result = _session.ApplyApprovedResilienceCapacityUx9E(draft);
        SetStatusUx9E(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9E(1);
            _focusChoice.Select(focus);
            SelectFocusUx9E(focus);
        }
        UpdateActionsUx9E();
    }

    private void UndoUx9E()
    {
        var result = _session.UndoResilienceCapacityUx9E();
        SetStatusUx9E(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9E(1);
        }
        UpdateActionsUx9E();
    }

    private void RedoUx9E()
    {
        var result = _session.RedoResilienceCapacityUx9E();
        SetStatusUx9E(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9E(1);
        }
        UpdateActionsUx9E();
    }

    private void SaveUx9E()
    {
        var result = _session.SaveResilienceCapacityUx9E();
        SetStatusUx9E(result.Message, !result.Succeeded);
        UpdateActionsUx9E();
    }

    private void ProbeUx9E()
    {
        var result = _session.RunLocalResilienceCapacityUx9E(_probeConfirmation.ButtonPressed,
            NetworkRole.Administrator);
        SetStatusUx9E(result.Message, !result.Succeeded);
        RefreshAuditUx9E();
        PopulatePreviewUx9E();
        UpdateActionsUx9E();
    }

    private void PopulatePreviewUx9E()
    {
        if (_preview is null)
        {
            var probe = _session.ResilienceCapacityProbeUx9E;
            _previewSummary.Text = probe.AuditEntries == 0
                ? "Prévisualisez la configuration · aucune restauration ni file exécutée."
                : $"Dernière sonde · {probe.Status} · restauration révision {probe.RestoredRevision} · " +
                  $"capacité publique {probe.PublicCapacity}, slots Administrateur {probe.ReservedAdministratorSlots}, " +
                  $"positions {probe.FirstQueuedPosition}/{probe.SecondQueuedPosition}, file restante {probe.QueueDepth}.";
            return;
        }
        var changes = _preview.SemanticDiff.Count == 0 ? "Aucun changement" :
            string.Join(" · ", _preview.SemanticDiff);
        var previewProbe = _preview.Probe;
        var rollback = previewProbe.RollbackPreviewCode == "NET_ROLLBACK_PREVIEW"
            ? "aperçu prêt, application non exécutée"
            : "aperçu refusé";
        _previewSummary.Text =
            $"{_preview.Message}\nChangements · {changes}\n" +
            $"Rollback · snapshot révision {previewProbe.SnapshotRevision}, {rollback}. " +
            $"Capacité · publique {previewProbe.PublicCapacity}, slots Administrateur {previewProbe.ReservedAdministratorSlots}, " +
            $"positions de file {previewProbe.FirstQueuedPosition}/{previewProbe.SecondQueuedPosition}, " +
            $"délestage {(previewProbe.LoadSheddingEnabled ? "actif" : "inactif")}, " +
            $"métriques {(previewProbe.MetricsEnabled ? "actives" : "inactives")}.";
    }

    private void RefreshAuditUx9E()
    {
        var audit = _session.ResilienceCapacityAuditUx9E;
        var probe = _session.ResilienceCapacityProbeUx9E;
        _auditSummary.Text = audit.Count == 0
            ? "Journal local · aucune entrée"
            : $"Journal local · {audit.Count} entrées · sonde : {probe.Status}, ressources " +
              $"{(probe.ResourcesReleased ? "libérées" : "encore actives")}.";
    }

    private void ConfigureEvidenceUx9E(string mode)
    {
        if (_draft is null) return;
        _updating = true;
        _snapshotInterval.Value = 10;
        _backupRetention.Value = 45;
        _maximumPlayers.Value = 3;
        _reservedAdministratorSlots.Value = 1;
        _reservationSeconds.Value = 20;
        _approval.ButtonPressed = true;
        _probeConfirmation.ButtonPressed = true;
        var focus = mode.Contains("capacity", StringComparison.OrdinalIgnoreCase) ||
                    mode.Contains("queue", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _focusChoice.Select(focus);
        SelectFocusUx9E(focus);
        _updating = false;
        InvalidateUx9E(cancelApproval: false, preserveApproval: true);
    }

    private void SelectFocusUx9E(int index)
    {
        for (var page = 0; page < _focusPages.Count; page++)
            _focusPages[page].Visible = page == index;
    }

    private void ConnectInvalidationUx9E()
    {
        foreach (var spin in new[]
                 {
                     _snapshotInterval, _backupRetention, _maximumPlayers,
                     _reservedAdministratorSlots, _reservationSeconds
                 })
            spin.ValueChanged += _ => InvalidateUx9E();
        _approval.Toggled += _ => UpdateActionsUx9E();
        _probeConfirmation.Toggled += _ => UpdateActionsUx9E();
    }

    private void InvalidateUx9E(bool cancelApproval = true, bool preserveApproval = false)
    {
        if (_updating) return;
        _preview = null;
        if (cancelApproval) _session.CancelResilienceCapacityApprovalUx9E();
        if (!preserveApproval) _approval.ButtonPressed = false;
        PopulatePreviewUx9E();
        UpdateActionsUx9E();
    }

    private void UpdateActionsUx9E()
    {
        var selected = _explicitSelection && _draft is not null;
        _previewButton.Disabled = !selected;
        _applyButton.Disabled = !selected || !PreviewReady || !_approval.ButtonPressed;
        _undoButton.Disabled = !_canUndo;
        _redoButton.Disabled = !_canRedo;
        _saveButton.Disabled = !_session.ResilienceCapacityHasUnsavedChangesUx9E;
        _probeButton.Disabled = !selected || PreviewReady ||
                                _session.ResilienceCapacityHasUnsavedChangesUx9E ||
                                !_probeConfirmation.ButtonPressed;
    }

    private void ResetUx9E()
    {
        _draft = null;
        _preview = null;
        _explicitSelection = false;
        _canUndo = false;
        _canRedo = false;
        _updating = true;
        if (_approval is not null) _approval.ButtonPressed = false;
        if (_probeConfirmation is not null) _probeConfirmation.ButtonPressed = false;
        _updating = false;
        if (_previewSummary is not null) PopulatePreviewUx9E();
    }

    private SourceSnapshotUx9E CaptureSourceUx9E(StudioSessionController session) => new(
        session.Project, session.History, session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [], session.IsDirty, session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(), session.SelectedEntityId,
        session.CaptureResilienceCapacityApprovalUx9E());

    private bool SourceUnchangedUx9E()
    {
        var source = _sourceSession;
        var snapshot = _sourceSnapshot;
        if (source is null || snapshot is null) return true;
        return ReferenceEquals(source.Project, snapshot.Project) &&
               ReferenceEquals(source.History, snapshot.History) &&
               source.History?.State.Revision == snapshot.Revision &&
               (source.History?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               source.IsDirty == snapshot.Dirty && source.ActiveAreaId == snapshot.Area &&
               source.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               source.SelectedEntityId == snapshot.Selected &&
               source.CaptureResilienceCapacityApprovalUx9E() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx9E()
    {
        if (_sourceSession is not null) _session = _sourceSession;
        _sourceSession = null;
        _sourceSnapshot = null;
        _sourceSessionRestored = true;
    }

    private void DeleteEvidenceUx9E()
    {
        var root = _evidenceRoot;
        _evidenceRoot = null;
        if (string.IsNullOrWhiteSpace(root))
        {
            _evidenceResourcesReleased = true;
            return;
        }
        try
        {
            var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(full)?.TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar), temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-e-ui-",
                    StringComparison.Ordinal))
            {
                _evidenceResourcesReleased = false;
                return;
            }
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            _evidenceResourcesReleased = !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _evidenceResourcesReleased = false;
            SetStatusUx9E($"La copie temporaire n’a pas pu être libérée : {exception.Message}", true);
        }
    }

    private void UpdateBoundsUx9E()
    {
        if (_dialog is null) return;
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(960, size.X - 48), Math.Max(700, size.Y - 36));
    }

    private void SetStatusUx9E(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private static Control RouteCardUx9E(string route, string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux9E" + route.Replace("-", string.Empty),
            StudioStyle.SurfaceRaised, 6, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 2);
        panel.AddChild(stack);
        stack.AddChild(StudioStyle.Label($"{route} · {title}", 10, StudioStyle.Text));
        var label = StudioStyle.Label(detail, 9, StudioStyle.Muted);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        stack.AddChild(label);
        return panel;
    }

    private static Control LabeledRowUx9E(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(title);
        row.AddChild(control);
        return row;
    }

    private static GridContainer GridUx9E(Control parent)
    {
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 7);
        parent.AddChild(grid);
        return grid;
    }

    private static SpinBox SpinUx9E(GridContainer parent, string label, double min, double max,
        double step)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            AllowGreater = false,
            AllowLesser = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(420, 34)
        };
        parent.AddChild(value);
        return value;
    }

    private static CheckBox CheckUx9E(Control parent, string text)
    {
        var check = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(check);
        return check;
    }

    private static Button ActionButtonUx9E(HBoxContainer parent, string text, Action action,
        float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static IEnumerable<string> DescendantTextUx9E(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is CheckBox check) yield return check.Text;
        if (node is OptionButton option)
            for (var index = 0; index < option.ItemCount; index++)
                yield return option.GetItemText(index);
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx9E(child))
                yield return value;
    }

    private static bool ContainsUuidUx9E(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);

    private sealed record SourceSnapshotUx9E(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool Dirty,
        Forge.Domain.Areas.AreaId? Area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? Selected,
        StudioUx9EApprovalSnapshot Approval);
}
