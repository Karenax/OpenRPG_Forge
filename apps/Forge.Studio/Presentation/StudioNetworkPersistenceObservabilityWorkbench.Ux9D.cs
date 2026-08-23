using System.Text.RegularExpressions;
using Forge.ModuleFormat.Network;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioNetworkPersistenceObservabilityWorkbenchUx9D : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx9D? _sourceSnapshot;
    private StudioUx9DPersistenceObservabilityDraft? _draft;
    private StudioUx9DPersistenceObservabilityPreview? _preview;
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
    private SpinBox _backupRetention = null!;
    private LineEdit _operationIds = null!;
    private LineEdit _metrics = null!;
    private LineEdit _healthEndpoint = null!;
    private LineEdit _metricsEndpoint = null!;
    private SpinBox _maximumSpans = null!;
    private SpinBox _reconnectionGrace = null!;
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
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool HasExplicitSelection => _explicitSelection;
    internal bool PreviewReady => _preview is
    {
        Succeeded: true, CanApply: true, CanonicalDryRun: true, PersistenceRuntime: true,
        SqlRuntime: true, ObservabilityRuntime: true, ProfilerRuntime: true, ReconnectionRuntime: true
    };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx9D();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx9D(this));
            return Visible && text.Contains("ORF-MP-022", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-023", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-028", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-030", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-032", StringComparison.Ordinal) &&
                   text.Contains("Administrateur", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tester localement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun exporter externe", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx9D(text);
        }
    }

    internal StudioNetworkPersistenceObservabilityWorkbenchUx9D(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx9D();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9D();
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
        _session.CancelPersistenceObservabilityApprovalUx9D();
        ResetUx9D();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx9D(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux9-d-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        var baseline = NetworkPersistenceObservabilityProfile.CreateDefault("evidence:ux9-d-ui");
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !WorkspaceNetworkPersistenceObservabilityStore.Save(_evidenceRoot, baseline).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx9D();
            RestoreSourceSessionUx9D();
            return false;
        }
        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx9D();
        _moduleChoice.Select(1);
        SelectModuleUx9D(1);
        ConfigureEvidenceUx9D(mode);
        PreviewCurrentUx9D();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady && ShowsHumanSurface &&
                    EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_D_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
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
        var draft = ReadDraftUx9D();
        if (draft is null) return false;
        var preview = _session.PreviewPersistenceObservabilityUx9D(draft);
        var applied = preview.CanApply && _approval.ButtonPressed &&
                      _session.ApplyApprovedPersistenceObservabilityUx9D(draft).Succeeded;
        var undo = applied && _session.UndoPersistenceObservabilityUx9D().Succeeded;
        var redo = undo && _session.RedoPersistenceObservabilityUx9D().Succeeded;
        var save = redo && _session.SavePersistenceObservabilityUx9D().Succeeded;
        var probed = save && _probeConfirmation.ButtonPressed &&
                     _session.RunLocalPersistenceObservabilityUx9D(true, NetworkRole.Administrator).Succeeded;
        var expected = _session.CurrentPersistenceObservabilityUx9D;
        var reopened = new StudioSessionController();
        var reopenedOk = probed && expected is not null && reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.CurrentPersistenceObservabilityUx9D is { } actual &&
                         string.Equals(new NetworkPersistenceObservabilityStore().Serialize(expected),
                             new NetworkPersistenceObservabilityStore().Serialize(actual), StringComparison.Ordinal);
        reopened.Close();
        RefreshAuditUx9D();
        return preview.CanonicalDryRun && applied && undo && redo && save && probed && reopenedOk &&
               _session.PersistenceObservabilityProbeUx9D.ResourcesReleased && EvidenceSourceUntouched;
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is not null)
        {
            _evidenceSession.Close();
            _evidenceSession = null;
        }
        Visible = false;
        ResetUx9D();
        DeleteEvidenceUx9D();
        RestoreSourceSessionUx9D();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9D();
        _moduleChoice.Select(1);
        SelectModuleUx9D(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx9D();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx9D;
        UpdateBoundsUx9D();
    }

    private void BuildUiUx9D()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color("101410f2");
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _dialog = StudioStyle.Panel("Ux9DPersistenceObservability", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var root = new VBoxContainer
        {
            Name = "Ux9DRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 6);
        _dialog.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(headingText);
        headingText.AddChild(StudioStyle.Label("Réseau · persistance et observabilité locale", 20,
            StudioStyle.Text));
        headingText.AddChild(StudioStyle.Label(
            "Snapshots, données nommées, métriques, profilage et reconnexion sous autorité serveur.",
            11, StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);

        var routes = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        routes.AddThemeConstantOverride("separation", 7);
        root.AddChild(routes);
        routes.AddChild(RouteCardUx9D("ORF-MP-022", "Persistance", "Backups, rétention et failover."));
        routes.AddChild(RouteCardUx9D("ORF-MP-023", "SQL sécurisé", "Opérations nommées, aucun SQL brut."));
        routes.AddChild(RouteCardUx9D("ORF-MP-028", "Métriques", "Santé, exporters et confidentialité."));
        routes.AddChild(RouteCardUx9D("ORF-MP-030", "Profiler", "Sampling, traces et captures bornées."));
        routes.AddChild(RouteCardUx9D("ORF-MP-032", "Reconnexion", "Tickets à usage unique et handoff."));
        root.AddChild(StudioStyle.Label(
            "Limite : services en mémoire uniquement · aucun exporter externe, SQL distant, socket ou mutation du module réel.",
            10, StudioStyle.Warning));

        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _moduleChoice.ItemSelected += index => SelectModuleUx9D((int)index);
        root.AddChild(LabeledRowUx9D("Module explicite", _moduleChoice));
        _focusChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var label in new[]
                 {
                     "Persistance de monde", "SQL sécurisé", "Métriques et santé",
                     "Profiler serveur", "Reconnexion"
                 })
            _focusChoice.AddItem(label);
        _focusChoice.ItemSelected += index => SelectFocusUx9D((int)index);
        root.AddChild(LabeledRowUx9D("Workflow affiché", _focusChoice));

        var editor = StudioStyle.Panel("Ux9DEditor", StudioStyle.SurfaceRaised, 9, 1);
        editor.CustomMinimumSize = new Vector2(0, 330);
        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(editor);
        var editorStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        editorStack.AddThemeConstantOverride("separation", 7);
        editor.AddChild(editorStack);
        editorStack.AddChild(StudioStyle.Label("Édition directe de la politique sélectionnée", 14,
            StudioStyle.Text));
        BuildPersistencePageUx9D(editorStack);
        BuildSqlPageUx9D(editorStack);
        BuildObservabilityPageUx9D(editorStack);
        BuildProfilerPageUx9D(editorStack);
        BuildReconnectionPageUx9D(editorStack);

        var confirmations = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        confirmations.AddThemeConstantOverride("separation", 18);
        root.AddChild(confirmations);
        _approval = CheckUx9D(confirmations, "J’approuve ce seul aperçu sec pour la révision affichée");
        _probeConfirmation = CheckUx9D(confirmations,
            "Je confirme la sonde locale en rôle Administrateur après sauvegarde");
        var review = StudioStyle.Panel("Ux9DReview", StudioStyle.SurfaceRaised, 7, 1);
        review.CustomMinimumSize = new Vector2(0, 108);
        root.AddChild(review);
        var reviewStack = new VBoxContainer();
        review.AddChild(reviewStack);
        _previewSummary = StudioStyle.Label("Prévisualisez la configuration · aucune sonde exécutée.", 10,
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
        _previewButton = ActionButtonUx9D(actions, "Prévisualiser", PreviewCurrentUx9D, 132, true);
        _applyButton = ActionButtonUx9D(actions, "Appliquer", ApplyCurrentUx9D, 104);
        _undoButton = ActionButtonUx9D(actions, "Annuler", UndoUx9D, 92);
        _redoButton = ActionButtonUx9D(actions, "Rétablir", RedoUx9D, 92);
        _saveButton = ActionButtonUx9D(actions, "Enregistrer", SaveUx9D, 112);
        _probeButton = ActionButtonUx9D(actions, "Tester localement", ProbeUx9D, 144);
        ConnectInvalidationUx9D();
        SelectFocusUx9D(0);
        UpdateActionsUx9D();
    }

    private void BuildPersistencePageUx9D(VBoxContainer parent)
    {
        var page = FocusPageUx9D(parent, "Persistance transactionnelle et rétention bornée");
        var grid = GridUx9D(page);
        _backupRetention = SpinUx9D(grid, "Rétention backups (jours)", 1, 3650, 1);
        page.AddChild(StudioStyle.Label(
            "Embedded + relationnel, transactions, migrations, backups et failover restent imposés.",
            10, StudioStyle.Success));
    }

    private void BuildSqlPageUx9D(VBoxContainer parent)
    {
        var page = FocusPageUx9D(parent, "Données serveur par opérations nommées");
        var grid = GridUx9D(page);
        _operationIds = LineUx9D(grid, "Opérations autorisées");
        page.AddChild(StudioStyle.Label(
            "world_flag.upsert et world_flag.by_scope sont requis · paramètres typés, dépôts serveur et aucun SQL brut.",
            10, StudioStyle.Success));
    }

    private void BuildObservabilityPageUx9D(VBoxContainer parent)
    {
        var page = FocusPageUx9D(parent, "Métriques et santé filtrées");
        var grid = GridUx9D(page);
        _metrics = LineUx9D(grid, "Métriques autorisées");
        _healthEndpoint = LineUx9D(grid, "Endpoint santé");
        _metricsEndpoint = LineUx9D(grid, "Endpoint métriques");
        page.AddChild(StudioStyle.Label(
            "Prometheus + OpenTelemetry et filtrage privé restent imposés ; Studio n’ouvre aucun endpoint.",
            10, StudioStyle.Warning));
    }

    private void BuildProfilerPageUx9D(VBoxContainer parent)
    {
        var page = FocusPageUx9D(parent, "Profilage borné du tick serveur");
        var grid = GridUx9D(page);
        _maximumSpans = SpinUx9D(grid, "Captures max.", 100, 10_000_000, 100);
        page.AddChild(StudioStyle.Label(
            "Sampling, instrumentation, traces, breakdown tick et spans script/DB/réseau restent imposés.",
            10, StudioStyle.Success));
    }

    private void BuildReconnectionPageUx9D(VBoxContainer parent)
    {
        var page = FocusPageUx9D(parent, "Reconnexion sûre et reprise d’état");
        var grid = GridUx9D(page);
        _reconnectionGrace = SpinUx9D(grid, "Grâce (secondes)", 5, 3600, 5);
        page.AddChild(StudioStyle.Label(
            "Ticket à usage unique, handoff entité, groupe, transactions pendantes et repli sûr restent imposés.",
            10, StudioStyle.Success));
    }

    private VBoxContainer FocusPageUx9D(VBoxContainer parent, string title)
    {
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 7);
        page.AddChild(StudioStyle.Label(title, 12, StudioStyle.Text));
        parent.AddChild(page);
        _focusPages.Add(page);
        return page;
    }

    private void OpenCurrentUx9D()
    {
        Visible = true;
        ResetUx9D();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionner un module");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(_session.Project.Module.DisplayName));
        _moduleChoice.Select(0);
        RefreshAuditUx9D();
        UpdateBoundsUx9D();
        UpdateActionsUx9D();
    }

    private void SelectModuleUx9D(int index)
    {
        _explicitSelection = index == 1 && _session.Project is not null;
        _session.CancelPersistenceObservabilityApprovalUx9D();
        _preview = null;
        _approval.ButtonPressed = false;
        if (!_explicitSelection)
        {
            _draft = null;
            SetStatusUx9D("Sélectionnez explicitement le module.", false);
            UpdateActionsUx9D();
            return;
        }
        _draft = _session.CreatePersistenceObservabilityDraftUx9D(explicitlySelected: true);
        if (_draft is null)
        {
            SetStatusUx9D(_session.PersistenceObservabilityLoadMessageUx9D ??
                          "La persistance réseau est indisponible.", true);
            UpdateActionsUx9D();
            return;
        }
        PopulateDraftUx9D(_draft);
        SetStatusUx9D(_session.PersistenceObservabilityFileMissingUx9D
            ? "Profil initial chargé en mémoire · aucun fichier créé avant Enregistrer."
            : "Persistance réseau chargée · prévisualisez avant toute application.", false);
        UpdateActionsUx9D();
    }

    private void PopulateDraftUx9D(StudioUx9DPersistenceObservabilityDraft draft)
    {
        _updating = true;
        _backupRetention.Value = draft.BackupRetentionDays;
        _operationIds.Text = draft.AllowedOperationIds;
        _metrics.Text = draft.Metrics;
        _healthEndpoint.Text = draft.HealthEndpoint;
        _metricsEndpoint.Text = draft.MetricsEndpoint;
        _maximumSpans.Value = draft.MaximumCapturedSpans;
        _reconnectionGrace.Value = draft.ReconnectionGraceSeconds;
        _updating = false;
    }

    private StudioUx9DPersistenceObservabilityDraft? ReadDraftUx9D() => _draft is null ? null : _draft with
    {
        BackupRetentionDays = (int)_backupRetention.Value,
        AllowedOperationIds = _operationIds.Text,
        Metrics = _metrics.Text,
        HealthEndpoint = _healthEndpoint.Text,
        MetricsEndpoint = _metricsEndpoint.Text,
        MaximumCapturedSpans = (int)_maximumSpans.Value,
        ReconnectionGraceSeconds = (int)_reconnectionGrace.Value
    };

    private void PreviewCurrentUx9D()
    {
        var draft = ReadDraftUx9D();
        if (draft is null)
        {
            SetStatusUx9D("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewPersistenceObservabilityUx9D(draft);
        PopulatePreviewUx9D();
        SetStatusUx9D(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx9D();
    }

    private void ApplyCurrentUx9D()
    {
        var draft = ReadDraftUx9D();
        if (draft is null || !_approval.ButtonPressed) return;
        var focus = _focusChoice.Selected;
        var result = _session.ApplyApprovedPersistenceObservabilityUx9D(draft);
        SetStatusUx9D(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9D(1);
            _focusChoice.Select(focus);
            SelectFocusUx9D(focus);
        }
        UpdateActionsUx9D();
    }

    private void UndoUx9D()
    {
        var result = _session.UndoPersistenceObservabilityUx9D();
        SetStatusUx9D(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9D(1);
        }
        UpdateActionsUx9D();
    }

    private void RedoUx9D()
    {
        var result = _session.RedoPersistenceObservabilityUx9D();
        SetStatusUx9D(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9D(1);
        }
        UpdateActionsUx9D();
    }

    private void SaveUx9D()
    {
        var result = _session.SavePersistenceObservabilityUx9D();
        SetStatusUx9D(result.Message, !result.Succeeded);
        UpdateActionsUx9D();
    }

    private void ProbeUx9D()
    {
        var result = _session.RunLocalPersistenceObservabilityUx9D(_probeConfirmation.ButtonPressed,
            NetworkRole.Administrator);
        SetStatusUx9D(result.Message, !result.Succeeded);
        RefreshAuditUx9D();
        UpdateActionsUx9D();
    }

    private void PopulatePreviewUx9D()
    {
        if (_preview is null)
        {
            _previewSummary.Text = "Prévisualisez la configuration · aucune sonde exécutée.";
            return;
        }
        var changes = _preview.SemanticDiff.Count == 0 ? "Aucun changement" :
            string.Join(" · ", _preview.SemanticDiff);
        var probe = _preview.Probe;
        _previewSummary.Text =
            $"{_preview.Message}\nChangements · {changes}\n" +
            $"Sondes · snapshot révision {probe.SnapshotRevision} · données {probe.DataCode}, {probe.DataRows} ligne, " +
            $"opération non listée {BlockedUx9D(probe.UnlistedOperationBlocked)} · métriques {probe.MetricSignals}, " +
            $"santé {probe.HealthStatus}, signal non listé {BlockedUx9D(probe.UnlistedMetricBlocked)} · " +
            $"profiler {probe.ProfileOperation}, {probe.ProfileCaptures} capture · reconnexion {probe.ReconnectionCode}, " +
            $"rejeu {BlockedUx9D(probe.ReconnectionReplayBlocked)} · ressources {ReleasedUx9D(probe.ResourcesReleased)}.";
    }

    private void RefreshAuditUx9D()
    {
        var audit = _session.PersistenceObservabilityAuditUx9D;
        var probe = _session.PersistenceObservabilityProbeUx9D;
        _auditSummary.Text = audit.Count == 0
            ? "Journal local · aucune entrée"
            : $"Journal local · {audit.Count} entrées · sonde : {probe.Status}, ressources {ReleasedUx9D(probe.ResourcesReleased)}.";
    }

    private void ConfigureEvidenceUx9D(string mode)
    {
        if (_draft is null) return;
        _updating = true;
        _backupRetention.Value = 60;
        _operationIds.Text = "world_flag.upsert, world_flag.by_scope";
        _metrics.Text =
            "server.tps, server.tick.ms, process.memory, connections.active, queues.pending, scripts.hotspots";
        _healthEndpoint.Text = "/healthz";
        _metricsEndpoint.Text = "/metricsz";
        _maximumSpans.Value = 250000;
        _reconnectionGrace.Value = 180;
        _approval.ButtonPressed = true;
        _probeConfirmation.ButtonPressed = true;
        var focus = mode.Contains("sql", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("metric", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("observ", StringComparison.OrdinalIgnoreCase) ? 2 :
            mode.Contains("profiler", StringComparison.OrdinalIgnoreCase) ? 3 :
            mode.Contains("reconnect", StringComparison.OrdinalIgnoreCase) ? 4 : 0;
        _focusChoice.Select(focus);
        SelectFocusUx9D(focus);
        _updating = false;
        InvalidateUx9D(cancelApproval: false, preserveApproval: true);
    }

    private void SelectFocusUx9D(int index)
    {
        for (var page = 0; page < _focusPages.Count; page++) _focusPages[page].Visible = page == index;
    }

    private void ConnectInvalidationUx9D()
    {
        foreach (var line in new[] { _operationIds, _metrics, _healthEndpoint, _metricsEndpoint })
            line.TextChanged += _ => InvalidateUx9D();
        foreach (var spin in new[] { _backupRetention, _maximumSpans, _reconnectionGrace })
            spin.ValueChanged += _ => InvalidateUx9D();
        _approval.Toggled += _ => UpdateActionsUx9D();
        _probeConfirmation.Toggled += _ => UpdateActionsUx9D();
    }

    private void InvalidateUx9D(bool cancelApproval = true, bool preserveApproval = false)
    {
        if (_updating) return;
        _preview = null;
        if (cancelApproval) _session.CancelPersistenceObservabilityApprovalUx9D();
        if (!preserveApproval) _approval.ButtonPressed = false;
        PopulatePreviewUx9D();
        UpdateActionsUx9D();
    }

    private void UpdateActionsUx9D()
    {
        var selected = _explicitSelection && _draft is not null;
        _previewButton.Disabled = !selected;
        _applyButton.Disabled = !selected || !PreviewReady || !_approval.ButtonPressed;
        _undoButton.Disabled = !_canUndo;
        _redoButton.Disabled = !_canRedo;
        _saveButton.Disabled = !_session.PersistenceObservabilityHasUnsavedChangesUx9D;
        _probeButton.Disabled = !selected || PreviewReady ||
                                _session.PersistenceObservabilityHasUnsavedChangesUx9D ||
                                !_probeConfirmation.ButtonPressed;
    }

    private void ResetUx9D()
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
        if (_previewSummary is not null) PopulatePreviewUx9D();
    }

    private SourceSnapshotUx9D CaptureSourceUx9D(StudioSessionController session) => new(
        session.Project, session.History, session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [], session.IsDirty, session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(), session.SelectedEntityId,
        session.CapturePersistenceObservabilityApprovalUx9D());

    private bool SourceUnchangedUx9D()
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
               source.CapturePersistenceObservabilityApprovalUx9D() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx9D()
    {
        if (_sourceSession is not null) _session = _sourceSession;
        _sourceSession = null;
        _sourceSnapshot = null;
        _sourceSessionRestored = true;
    }

    private void DeleteEvidenceUx9D()
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-d-ui-", StringComparison.Ordinal))
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
            SetStatusUx9D($"La copie temporaire n’a pas pu être libérée : {exception.Message}", true);
        }
    }

    private void UpdateBoundsUx9D()
    {
        if (_dialog is null) return;
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(960, size.X - 48), Math.Max(700, size.Y - 36));
    }

    private void SetStatusUx9D(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private static Control RouteCardUx9D(string route, string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux9D" + route.Replace("-", string.Empty),
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

    private static Control LabeledRowUx9D(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(title);
        row.AddChild(control);
        return row;
    }

    private static GridContainer GridUx9D(Control parent)
    {
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 7);
        parent.AddChild(grid);
        return grid;
    }

    private static LineEdit LineUx9D(GridContainer parent, string label)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(420, 34)
        };
        parent.AddChild(value);
        return value;
    }

    private static SpinBox SpinUx9D(GridContainer parent, string label, double min, double max, double step)
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

    private static CheckBox CheckUx9D(Control parent, string text)
    {
        var check = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(check);
        return check;
    }

    private static Button ActionButtonUx9D(HBoxContainer parent, string text, Action action, float width,
        bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static string BlockedUx9D(bool value) => value ? "refusé" : "accepté à tort";
    private static string ReleasedUx9D(bool value) => value ? "libérées" : "encore actives";

    private static IEnumerable<string> DescendantTextUx9D(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is CheckBox check) yield return check.Text;
        if (node is LineEdit line) yield return line.Text;
        if (node is OptionButton option)
            for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx9D(child)) yield return value;
    }

    private static bool ContainsUuidUx9D(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);

    private sealed record SourceSnapshotUx9D(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool Dirty,
        Forge.Domain.Areas.AreaId? Area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? Selected,
        StudioUx9DApprovalSnapshot Approval);
}
