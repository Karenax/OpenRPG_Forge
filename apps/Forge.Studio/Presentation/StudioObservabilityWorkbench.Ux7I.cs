using System.Text.RegularExpressions;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioObservabilityWorkbenchUx7I : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx7I? _sourceSnapshot;
    private StudioUx7IDraft? _draft;
    private StudioUx7IPreview? _previewReport;
    private StudioUx7ICaptureResult? _captureReport;
    private StudioUx7ICaptureResult? _nativeReport;
    private StudioUx7ICaptureResult? _cancelledReport;
    private CancellationTokenSource? _runCancellation;
    private Task<StudioUx7ICaptureResult>? _runTask;
    private bool _explicitSelection;
    private bool _applied;
    private bool _updating;
    private bool _evidenceReleased = true;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private CheckBox _enabled = null!;
    private CheckBox _tracyCompatible = null!;
    private CheckBox _openTelemetryCompatible = null!;
    private CheckBox _includeScripts = null!;
    private SpinBox _maximumSeconds = null!;
    private SpinBox _requestedSeconds = null!;
    private OptionButton _backend = null!;
    private Label _selection = null!;
    private Label _previewSummary = null!;
    private Label _backendStatus = null!;
    private ItemList _results = null!;
    private Label _resultTitle = null!;
    private Label _resultDetails = null!;
    private Label _status = null!;
    private Button _preview = null!;
    private Button _apply = null!;
    private Button _undo = null!;
    private Button _redo = null!;
    private Button _save = null!;
    private Button _run = null!;
    private Button _cancel = null!;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _draft is not null;
    internal bool CanonicalPreviewReady => _previewReport is { Succeeded: true, CanonicalDryRun: true };
    internal bool OpenTelemetryResultReady => _captureReport is
    { Succeeded: true, Isolated: true, SpanCount: >= 5, Surfaces.Count: 3 };
    internal bool NativeTracyDiagnosticReady => _nativeReport is { Succeeded: false } &&
        _nativeReport.Diagnostics.Any(item => item.Code == "OBSERVABILITY_TRACY_RUNTIME_MISSING");
    internal bool CancellationReady => _cancelledReport is { Cancelled: true, TemporaryWorkspaceReleased: true };
    internal bool ResultNavigationReady => _results.ItemCount >= 4 && _results.GetSelectedItems().Length == 1 &&
                                           !_resultDetails.Text.Contains("Sélectionnez", StringComparison.Ordinal);
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && _evidenceRoot is not null;
    internal bool EvidenceSourceUntouched => SourceMatchesUx7I();
    internal bool EvidenceResourcesReleased => _evidenceReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession) && SourceMatchesUx7I();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx7I(this));
            return Visible && text.Contains("Profiler Tracy", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Godot instrumenté", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Annuler la capture", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Editor", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Runtime", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Serveur", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx7I(text);
        }
    }

    internal StudioObservabilityWorkbenchUx7I(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = session;
        BuildUiUx7I();
    }

    private void BuildUiUx7I()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.01f, 0.016f, 0.028f, .96f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _dialog = StudioStyle.Panel("Ux7IObservability", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var host = new Control { ClipContents = true };
        _dialog.AddChild(host);
        host.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = StudioStyle.Margin(16, 12, 16, 12);
        margin.PropagateMaximumSize = true;
        host.AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            PropagateMaximumSize = true,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("OBSERVABILITÉ · UX7-I", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Profiler Tracy et fallback OpenTelemetry", 19, StudioStyle.Text));
        titles.AddChild(StudioStyle.Label(
            "Configuration canonique · capture isolée annulable · Editor, Runtime et Serveur",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButtonUx7I("Fermer", Close, 84));
        root.AddChild(header);

        var selectionRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selectionRow.AddThemeConstantOverride("separation", 8);
        var selectionLabel = StudioStyle.Label("Module", 11, StudioStyle.Muted);
        selectionLabel.CustomMinimumSize = new Vector2(100, 0);
        selectionRow.AddChild(selectionLabel);
        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_moduleChoice);
        _moduleChoice.ItemSelected += SelectModuleUx7I;
        selectionRow.AddChild(_moduleChoice);
        root.AddChild(selectionRow);
        _selection = StudioStyle.Label("Sélection explicite requise.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_selection);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 390),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        var body = new HSplitContainer { SplitOffsets = [610] };
        bodyHost.AddChild(body);
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var configuration = StudioStyle.Panel("Ux7IConfiguration", StudioStyle.SurfaceRaised, 8, 1);
        configuration.CustomMinimumSize = new Vector2(520, 0);
        body.AddChild(configuration);
        var configMargin = StudioStyle.Margin(11, 9, 11, 9);
        configuration.AddChild(configMargin);
        var configScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        configMargin.AddChild(configScroll);
        var config = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        config.AddThemeConstantOverride("separation", 7);
        configScroll.AddChild(config);
        config.AddChild(StudioStyle.Label("CONFIGURATION CANONIQUE", 10, StudioStyle.Accent));
        _enabled = CheckUx7I("Activer l’observabilité", config);
        _tracyCompatible = CheckUx7I("Autoriser Tracy natif", config);
        _openTelemetryCompatible = CheckUx7I("Autoriser le fallback OpenTelemetry", config);
        _includeScripts = CheckUx7I("Inclure les spans ForgeScript", config);
        _maximumSeconds = NumberUx7I(config, "Durée maximale canonique", 1, 3600, " secondes");
        _requestedSeconds = NumberUx7I(config, "Durée demandée pour ce run", 1, 3600, " secondes");
        config.AddChild(StudioStyle.Label("Backend du run", 10, StudioStyle.Muted));
        _backend = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_backend);
        _backend.AddItem("OpenTelemetry · export JSON compatible");
        _backend.AddItem("Tracy natif · moteur instrumenté requis");
        _backend.ItemSelected += _ => DraftChangedUx7I();
        config.AddChild(_backend);
        var boundary = StudioStyle.Label(
            "Tracy natif n’est pas simulé : il exige un Godot instrumenté avec Tracy et un serveur de capture " +
            "correspondant. Le fallback OpenTelemetry produit ici un vrai artefact confiné; il n’est jamais " +
            "présenté comme une capture Tracy native.",
            10, StudioStyle.Warning, wrap: true);
        boundary.CustomMinimumSize = new Vector2(0, 72);
        config.AddChild(boundary);
        config.AddChild(new HSeparator());
        config.AddChild(StudioStyle.Label("APERÇU SEC", 10, StudioStyle.Accent));
        _previewSummary = StudioStyle.Label("Prévisualisez avant toute application.", 10,
            StudioStyle.Muted, wrap: true);
        _backendStatus = StudioStyle.Label("Aucun backend évalué.", 10, StudioStyle.Muted, wrap: true);
        config.AddChild(_previewSummary);
        config.AddChild(_backendStatus);

        var resultsPanel = StudioStyle.Panel("Ux7IResults", StudioStyle.SurfaceRaised, 8, 1);
        resultsPanel.CustomMinimumSize = new Vector2(520, 0);
        body.AddChild(resultsPanel);
        var resultMargin = StudioStyle.Margin(11, 9, 11, 9);
        resultsPanel.AddChild(resultMargin);
        var results = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        results.AddThemeConstantOverride("separation", 7);
        resultMargin.AddChild(results);
        results.AddChild(StudioStyle.Label("RÉSULTATS NAVIGABLES", 10, StudioStyle.Accent));
        _results = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 178),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
            AllowReselect = true,
        };
        _results.ItemSelected += SelectResultUx7I;
        results.AddChild(_results);
        var detailScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        results.AddChild(detailScroll);
        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detailScroll.AddChild(details);
        _resultTitle = StudioStyle.Label("Aucun résultat", 15, StudioStyle.Text, wrap: true);
        _resultDetails = StudioStyle.Label("Sélectionnez un résultat après la capture.", 10,
            StudioStyle.Muted, wrap: true);
        details.AddChild(_resultTitle);
        details.AddChild(_resultDetails);

        _status = StudioStyle.Label("Prêt · sélection explicite requise.", 10, StudioStyle.Muted, wrap: true);
        _status.CustomMinimumSize = new Vector2(0, 28);
        root.AddChild(_status);
        var transaction = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        transaction.AddThemeConstantOverride("separation", 6);
        _preview = ActionButtonUx7I("Prévisualiser", PreviewUx7I, 124, primary: true);
        _apply = ActionButtonUx7I("Appliquer", ApplyUx7I, 112);
        _undo = ActionButtonUx7I("Annuler", UndoUx7I, 96);
        _redo = ActionButtonUx7I("Rétablir", RedoUx7I, 96);
        _save = ActionButtonUx7I("Enregistrer", SaveUx7I, 112);
        _run = ActionButtonUx7I("Lancer la capture isolée", StartRunUx7I, 188, primary: true);
        _cancel = ActionButtonUx7I("Annuler la capture", CancelRunUx7I, 150);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save, _run, _cancel })
            transaction.AddChild(button);
        root.AddChild(transaction);

        foreach (var check in new[] { _enabled, _tracyCompatible, _openTelemetryCompatible, _includeScripts })
            check.Toggled += _ => DraftChangedUx7I();
        _maximumSeconds.ValueChanged += _ => DraftChangedUx7I();
        _requestedSeconds.ValueChanged += _ => DraftChangedUx7I();
        UpdateButtonsUx7I();
    }

    internal void Open()
    {
        ReleaseEvidenceUx7I();
        _session = _sourceSession;
        ResetUx7I();
        PopulateModuleChoicesUx7I();
        Visible = true;
        UpdateBoundsUx7I();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _runTask is null) return;
        CancelRunUx7I();
        Visible = false;
        _session.CancelObservabilityApprovalUx7I();
        ReleaseEvidenceUx7I();
        _session = _sourceSession;
        ResetUx7I();
    }

    internal void CloseEvidence() => Close();

    internal void RestoreSourceSurfaceAfterSmoke() => Open();

    internal void HideSourceSurfaceAfterSmoke()
    {
        Visible = false;
        ReleaseEvidenceUx7I();
        _session = _sourceSession;
        ResetUx7I();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        var sourceApproval = _sourceSession.CaptureObservabilityApprovalUx7I();
        Close();
        _sourceSession.RestoreObservabilityApprovalUx7I(sourceApproval);
        var sourceProject = _sourceSession.Project;
        if (sourceProject is null || _sourceSession.History is null || _sourceSession.Session is null) return false;
        _sourceSnapshot = CaptureSourceUx7I();
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux7-i-ui-" + Guid.NewGuid().ToString("N"));
        var evidence = new StudioSessionController();
        _evidenceReleased = false;
        try
        {
            var created = ProjectSession.Create(root, sourceProject);
            if (!created.Succeeded || !CopyWorkspaceUx7I(_sourceSession.Session.RootDirectory, root) ||
                !evidence.Open(root).Succeeded)
                throw new InvalidOperationException("La session de preuve isolée ne peut pas être ouverte.");
            _evidenceSession = evidence;
            _evidenceRoot = root;
            _session = evidence;
            ResetUx7I();
            PopulateModuleChoicesUx7I();
            Visible = true;
            UpdateBoundsUx7I();
            _moduleChoice.Select(1);
            SelectModuleUx7I(1);
            if (_draft is null) return false;
            _enabled.ButtonPressed = true;
            _tracyCompatible.ButtonPressed = true;
            _openTelemetryCompatible.ButtonPressed = true;
            _includeScripts.ButtonPressed = true;
            _maximumSeconds.Value = _maximumSeconds.Value >= 3600
                ? 3599
                : Math.Max(30, _maximumSeconds.Value) + 1;
            _requestedSeconds.Value = 30;
            _backend.Select(0);
            DraftChangedUx7I();
            PreviewUx7I();
            ApplyUx7I();
            if (!_applied) return false;
            var runDraft = _session.CreateObservabilityDraftUx7I(explicitlySelected: true)! with
            {
                Backend = Forge.ModuleFormat.Quality.ObservabilityCaptureBackend.OpenTelemetry,
                RequestedCaptureSeconds = 30,
            };
            _captureReport = _session.RunIsolatedObservabilityUx7I(runDraft, CancellationToken.None);
            _nativeReport = _session.RunIsolatedObservabilityUx7I(runDraft with
            {
                Backend = Forge.ModuleFormat.Quality.ObservabilityCaptureBackend.TracyNative,
            }, CancellationToken.None);
            using var cancelledSource = new CancellationTokenSource();
            cancelledSource.Cancel();
            _cancelledReport = _session.RunIsolatedObservabilityUx7I(runDraft, cancelledSource.Token);
            PopulateResultsUx7I();
            if (_results.ItemCount > 0)
            {
                _results.Select(_results.ItemCount - 1);
                SelectResultUx7I(_results.ItemCount - 1);
            }
            var ready = HasExplicitSelection && CanonicalPreviewReady && OpenTelemetryResultReady &&
                        NativeTracyDiagnosticReady && CancellationReady && ResultNavigationReady &&
                        UsesIsolatedEvidence && EvidenceSourceUntouched && ShowsHumanSurface;
            if (!ready)
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX7_I_UI_FAILED " +
                    $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                    $"preview={CanonicalPreviewReady.ToString().ToLowerInvariant()} " +
                    $"otel={OpenTelemetryResultReady.ToString().ToLowerInvariant()} " +
                    $"tracy={NativeTracyDiagnosticReady.ToString().ToLowerInvariant()} " +
                    $"cancel={CancellationReady.ToString().ToLowerInvariant()} " +
                    $"navigation={ResultNavigationReady.ToString().ToLowerInvariant()} " +
                    $"source={EvidenceSourceUntouched.ToString().ToLowerInvariant()} " +
                    $"human={ShowsHumanSurface.ToString().ToLowerInvariant()}");
            return ready;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException or ArgumentException)
        {
            SetStatusUx7I("Preuve refusée · " + exception.Message, true);
            ReleaseEvidenceUx7I();
            _session = _sourceSession;
            Visible = false;
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        if (!PrepareEvidence("ux7-i-observability"))
        {
            CloseEvidence();
            return false;
        }
        var editing = _session;
        var appliedJson = ProjectJsonSerializer.Serialize(editing.Project!);
        var undo = editing.Undo();
        var redo = editing.Redo();
        var undoRedo = undo.Succeeded && redo.Succeeded &&
                       string.Equals(appliedJson, ProjectJsonSerializer.Serialize(editing.Project!),
                           StringComparison.Ordinal);
        var save = editing.Save();
        var reopened = new StudioSessionController();
        var roundTrip = false;
        try
        {
            roundTrip = save.Succeeded && _evidenceRoot is not null && reopened.Open(_evidenceRoot).Succeeded &&
                        reopened.Project is not null &&
                        string.Equals(appliedJson, ProjectJsonSerializer.Serialize(reopened.Project),
                            StringComparison.Ordinal);
        }
        finally
        {
            reopened.Close();
        }
        var uiReady = undoRedo && roundTrip && OpenTelemetryResultReady && NativeTracyDiagnosticReady &&
                      CancellationReady && ResultNavigationReady && EvidenceSourceUntouched;
        CloseEvidence();
        return uiReady && EvidenceResourcesReleased && SourceSessionRestored;
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (_runTask is null || !_runTask.IsCompleted) return;
        try
        {
            _captureReport = _runTask.GetAwaiter().GetResult();
            PopulateResultsUx7I();
            if (_results.ItemCount > 0)
            {
                _results.Select(0);
                SelectResultUx7I(0);
            }
            SetStatusUx7I(_captureReport.Message, !_captureReport.Succeeded && !_captureReport.Cancelled);
        }
        catch (Exception exception)
        {
            SetStatusUx7I("Capture interrompue · " + exception.Message, true);
        }
        finally
        {
            _runTask = null;
            _runCancellation?.Dispose();
            _runCancellation = null;
            SetProcess(false);
            UpdateButtonsUx7I();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBoundsUx7I();
    }

    private void PopulateModuleChoicesUx7I()
    {
        _updating = true;
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Choisir explicitement le module ouvert…");
        foreach (var choice in _session.ObservabilityChoicesUx7I) _moduleChoice.AddItem(choice.Label);
        _moduleChoice.Select(0);
        _updating = false;
    }

    private void SelectModuleUx7I(long index)
    {
        if (_updating) return;
        _explicitSelection = index == 1;
        _draft = _explicitSelection ? _session.CreateObservabilityDraftUx7I(explicitlySelected: true) : null;
        if (_draft is null)
        {
            _selection.Text = "Sélectionnez explicitement le module.";
            SetEditingUx7I(false);
            UpdateButtonsUx7I();
            return;
        }
        PopulateDraftUx7I(_draft);
        _selection.Text = "Module sélectionné · configuration canonique chargée sans exposer son UUID.";
        _selection.Modulate = StudioStyle.Success;
        SetEditingUx7I(true);
        InvalidatePreviewUx7I();
    }

    private void PopulateDraftUx7I(StudioUx7IDraft draft)
    {
        _updating = true;
        _enabled.ButtonPressed = draft.Enabled;
        _tracyCompatible.ButtonPressed = draft.TracyCompatible;
        _openTelemetryCompatible.ButtonPressed = draft.OpenTelemetryCompatible;
        _includeScripts.ButtonPressed = draft.IncludeScripts;
        _maximumSeconds.Value = draft.MaximumCaptureSeconds;
        _requestedSeconds.MaxValue = draft.MaximumCaptureSeconds;
        _requestedSeconds.Value = Math.Min(draft.RequestedCaptureSeconds, draft.MaximumCaptureSeconds);
        _backend.Select(draft.Backend == Forge.ModuleFormat.Quality.ObservabilityCaptureBackend.OpenTelemetry ? 0 : 1);
        _updating = false;
    }

    private StudioUx7IDraft? ReadDraftUx7I()
    {
        var source = _draft;
        if (source is null) return null;
        return source with
        {
            Enabled = _enabled.ButtonPressed,
            TracyCompatible = _tracyCompatible.ButtonPressed,
            OpenTelemetryCompatible = _openTelemetryCompatible.ButtonPressed,
            MaximumCaptureSeconds = (int)_maximumSeconds.Value,
            IncludeScripts = _includeScripts.ButtonPressed,
            Backend = _backend.Selected == 0
                ? Forge.ModuleFormat.Quality.ObservabilityCaptureBackend.OpenTelemetry
                : Forge.ModuleFormat.Quality.ObservabilityCaptureBackend.TracyNative,
            RequestedCaptureSeconds = (int)_requestedSeconds.Value,
        };
    }

    private void DraftChangedUx7I()
    {
        if (_updating || _draft is null) return;
        _requestedSeconds.MaxValue = Math.Max(1, _maximumSeconds.Value);
        InvalidatePreviewUx7I();
    }

    private void PreviewUx7I()
    {
        var draft = ReadDraftUx7I();
        if (draft is null) return;
        _draft = draft;
        _previewReport = _session.PreviewObservabilityUx7I(draft);
        _previewSummary.Text = _previewReport.Summary +
            (_previewReport.Diff.Entries.Count == 0 ? "" : "\n" + string.Join("\n", _previewReport.Diff.Entries));
        _backendStatus.Text = _previewReport.Diagnostics.Count == 0
            ? _previewReport.BackendStatus
            : string.Join("\n", _previewReport.Diagnostics.Select(item => $"{item.Code} · {item.Message}"));
        _backendStatus.Modulate = _previewReport.Succeeded ? StudioStyle.Success : StudioStyle.Danger;
        SetStatusUx7I(_previewReport.Summary, !_previewReport.Succeeded);
        UpdateButtonsUx7I();
    }

    private void ApplyUx7I()
    {
        var result = _session.ApplyApprovedObservabilityUx7I();
        _applied = result.Succeeded;
        SetStatusUx7I(result.Message, !result.Succeeded);
        UpdateButtonsUx7I();
    }

    private void UndoUx7I()
    {
        var result = _session.Undo();
        _applied = false;
        SetStatusUx7I(result.Message, !result.Succeeded);
        UpdateButtonsUx7I();
    }

    private void RedoUx7I()
    {
        var result = _session.Redo();
        _applied = result.Succeeded;
        SetStatusUx7I(result.Message, !result.Succeeded);
        UpdateButtonsUx7I();
    }

    private void SaveUx7I()
    {
        var result = _session.Save();
        SetStatusUx7I(result.Message, !result.Succeeded);
        UpdateButtonsUx7I();
    }

    private void StartRunUx7I()
    {
        if (_runTask is not null) return;
        var draft = ReadDraftUx7I();
        if (draft is null || !draft.Enabled)
        {
            SetStatusUx7I("Activez et appliquez l’observabilité avant le run.", true);
            return;
        }
        _runCancellation = new CancellationTokenSource();
        var token = _runCancellation.Token;
        _runTask = Task.Run(() => _session.RunIsolatedObservabilityUx7I(draft, token));
        SetProcess(true);
        SetStatusUx7I("Capture isolée en cours · annulation disponible.");
        UpdateButtonsUx7I();
    }

    private void CancelRunUx7I()
    {
        if (_runCancellation is null) return;
        _runCancellation.Cancel();
        SetStatusUx7I("Annulation demandée · l’isolat sera libéré.");
    }

    private void PopulateResultsUx7I()
    {
        _results.Clear();
        var report = _captureReport;
        if (report is not null)
        {
            foreach (var surface in report.Surfaces)
                _results.AddItem($"{SurfaceUx7I(surface)} · spans capturés");
            _results.AddItem($"Artefact · {report.ArtifactLabel} · {report.SpanCount} spans");
        }
        if (_nativeReport is not null)
            _results.AddItem("Tracy natif · diagnostic d’environnement");
        if (_cancelledReport is not null)
            _results.AddItem("Annulation · isolat libéré");
    }

    private void SelectResultUx7I(long rawIndex)
    {
        var index = (int)rawIndex;
        var report = _captureReport;
        if (report is null || index < 0 || index >= _results.ItemCount)
        {
            _resultTitle.Text = "Aucun résultat";
            _resultDetails.Text = "Sélectionnez un résultat après la capture.";
            return;
        }
        if (index < report.Surfaces.Count)
        {
            var surface = report.Surfaces[index];
            _resultTitle.Text = SurfaceUx7I(surface);
            _resultDetails.Text = $"Surface {SurfaceUx7I(surface)} présente dans la trace OpenTelemetry-compatible.\n" +
                                  $"Catégories : {string.Join(", ", report.Categories)}.\n" +
                                  "Aucun nœud Godot ni fichier source du module n’a été modifié.";
            return;
        }
        if (index == report.Surfaces.Count)
        {
            _resultTitle.Text = "Artefact OpenTelemetry";
            _resultDetails.Text = $"{report.SpanCount} spans · {report.ArtifactLabel}.\n" +
                                  "Le fichier est écrit uniquement dans la copie temporaire puis supprimé avec elle.";
            return;
        }
        if (_nativeReport is not null && index == report.Surfaces.Count + 1)
        {
            _resultTitle.Text = "Tracy natif · indisponible honnêtement";
            _resultDetails.Text = _nativeReport.Diagnostics.FirstOrDefault()?.Message ??
                                  "Godot instrumenté et serveur Tracy requis.";
            return;
        }
        _resultTitle.Text = "Annulation de capture";
        _resultDetails.Text = _cancelledReport?.Message ?? "Capture annulée proprement.";
    }

    private void InvalidatePreviewUx7I()
    {
        _session.CancelObservabilityApprovalUx7I();
        _previewReport = null;
        _applied = false;
        _previewSummary.Text = "Aperçu sec requis après chaque modification.";
        _backendStatus.Text = "Le backend sera vérifié pendant l’aperçu.";
        _backendStatus.Modulate = StudioStyle.Muted;
        UpdateButtonsUx7I();
    }

    private void SetEditingUx7I(bool enabled)
    {
        foreach (var control in new Control[]
                 {
                     _enabled, _tracyCompatible, _openTelemetryCompatible, _includeScripts,
                     _maximumSeconds, _requestedSeconds, _backend,
                 })
            control.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _enabled.Disabled = !enabled;
        _tracyCompatible.Disabled = !enabled;
        _openTelemetryCompatible.Disabled = !enabled;
        _includeScripts.Disabled = !enabled;
        _maximumSeconds.Editable = enabled;
        _requestedSeconds.Editable = enabled;
        _backend.Disabled = !enabled;
    }

    private void UpdateButtonsUx7I()
    {
        if (_preview is null) return;
        var running = _runTask is not null;
        _preview.Disabled = !HasExplicitSelection || running;
        _apply.Disabled = _previewReport is not { Succeeded: true, CanApply: true } || running;
        _undo.Disabled = _session.History?.CanUndo != true || running;
        _redo.Disabled = _session.History?.CanRedo != true || running;
        _save.Disabled = _session.Project is null || running;
        _run.Disabled = !HasExplicitSelection || running;
        _cancel.Disabled = !running;
    }

    private void SetStatusUx7I(string message, bool error = false)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(message, error);
        UpdateButtonsUx7I();
    }

    private void ResetUx7I()
    {
        _updating = true;
        _explicitSelection = false;
        _draft = null;
        _previewReport = null;
        _captureReport = null;
        _nativeReport = null;
        _cancelledReport = null;
        _applied = false;
        _results?.Clear();
        if (_selection is not null)
        {
            _selection.Text = "Sélection explicite requise.";
            _selection.Modulate = StudioStyle.Muted;
            _previewSummary.Text = "Prévisualisez avant toute application.";
            _backendStatus.Text = "Aucun backend évalué.";
            _resultTitle.Text = "Aucun résultat";
            _resultDetails.Text = "Sélectionnez un résultat après la capture.";
        }
        _updating = false;
        if (_enabled is not null) SetEditingUx7I(false);
        UpdateButtonsUx7I();
    }

    private SourceSnapshotUx7I CaptureSourceUx7I()
    {
        var history = _sourceSession.History!;
        return new(_sourceSession.Project!, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureObservabilityApprovalUx7I());
    }

    private bool SourceMatchesUx7I()
    {
        var snapshot = _sourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.AreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureObservabilityApprovalUx7I() == snapshot.Approval;
    }

    private void ReleaseEvidenceUx7I()
    {
        var sessionReleased = true;
        if (_evidenceSession is not null)
        {
            try { _evidenceSession.Close(); }
            catch { sessionReleased = false; }
        }
        var rootReleased = StudioSessionController.DeleteUx7ITemporaryWorkspace(_evidenceRoot);
        _evidenceSession = null;
        _evidenceRoot = null;
        _session = _sourceSession;
        _evidenceReleased = sessionReleased && rootReleased;
    }

    private static bool CopyWorkspaceUx7I(string sourceRoot, string destinationRoot)
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceRoot, path).Replace('\\', '/');
                if (relative.Equals("project.json", StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith(".forge/", StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith(".godot/", StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase))
                    continue;
                var output = Path.GetFullPath(Path.Combine(destinationRoot,
                    relative.Replace('/', Path.DirectorySeparatorChar)));
                var root = Path.GetFullPath(destinationRoot);
                if (!output.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return false;
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.Copy(path, output, true);
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private void UpdateBoundsUx7I()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 48, 1180, 1840);
        var height = Math.Clamp(viewport.Y - 34, 780, 1040);
        _dialog.Position = new Vector2((viewport.X - width) / 2f, (viewport.Y - height) / 2f);
        _dialog.Size = new Vector2(width, height);
    }

    private static CheckBox CheckUx7I(string text, Container parent)
    {
        var control = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(control);
        return control;
    }

    private static SpinBox NumberUx7I(Container parent, string label, double minimum, double maximum,
        string suffix)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var control = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = 1,
            Suffix = suffix,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        parent.AddChild(control);
        return control;
    }

    private static Button ActionButtonUx7I(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static string SurfaceUx7I(Forge.Domain.RuntimeExtensions.QualityTraceSurface surface) => surface switch
    {
        Forge.Domain.RuntimeExtensions.QualityTraceSurface.Editor => "Editor",
        Forge.Domain.RuntimeExtensions.QualityTraceSurface.Runtime => "Runtime",
        _ => "Serveur",
    };

    private static bool ContainsUuidUx7I(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    private static IEnumerable<string> DescendantTextUx7I(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label when label.Visible: yield return label.Text; break;
                case Button button when button.Visible: yield return button.Text; break;
                case CheckBox check when check.Visible: yield return check.Text; break;
                case OptionButton option when option.Visible:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
                case ItemList list when list.Visible:
                    for (var index = 0; index < list.ItemCount; index++) yield return list.GetItemText(index);
                    break;
            }
            foreach (var text in DescendantTextUx7I(child)) yield return text;
        }
    }

    private sealed record SourceSnapshotUx7I(
        Forge.Domain.Projects.ProjectDefinition Project,
        Forge.Application.Commands.ProjectCommandHistory History,
        Forge.Application.Projects.ProjectRevision Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool IsDirty,
        bool CanUndo,
        bool CanRedo,
        Forge.Domain.Areas.AreaId? AreaId,
        IReadOnlyList<Forge.Domain.Entities.EntityId> SelectedEntityIds,
        Forge.Domain.Entities.EntityId? SelectedEntityId,
        StudioUx7IApprovalSnapshot Approval);
}
