using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Domain.Quality;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioQualityLabWorkbenchUx7B : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx7B? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _updating;
    private bool _explicitSelection;
    private bool _lastApply;
    private bool _lastUndo;
    private bool _lastRedo;
    private bool _lastSave;
    private bool _lastCancellation;
    private CancellationTokenSource? _runCancellation;
    private Task<StudioUx7BRunReport>? _runTask;

    private PanelContainer _dialog = null!;
    private OptionButton _profileChoice = null!;
    private TabContainer _tabs = null!;
    private TextEdit _domainAreas = null!;
    private TextEdit _headlessScenarios = null!;
    private LineEdit _reportPath = null!;
    private CheckBox _saveRoundTrip = null!;
    private CheckBox _saveOldVersions = null!;
    private CheckBox _savePartialFailure = null!;
    private CheckBox _saveMissingPackage = null!;
    private CheckBox _saveRollback = null!;
    private CheckBox _saveDeterministic = null!;
    private OptionButton _minimumSeverity = null!;
    private TextEdit _logCategories = null!;
    private CheckBox _includeObjectIds = null!;
    private CheckBox _includeSourceLines = null!;
    private CheckBox _includeTransactions = null!;
    private OptionButton _privacy = null!;
    private LineEdit _retentionDays = null!;
    private LineEdit _maximumEntries = null!;
    private Label _previewSummary = null!;
    private ItemList _results = null!;
    private Label _resultTitle = null!;
    private Label _resultSummary = null!;
    private Label _resultDetails = null!;
    private Label _status = null!;
    private Button _preview = null!;
    private Button _run = null!;
    private Button _cancelRun = null!;
    private Button _apply = null!;
    private Button _undo = null!;
    private Button _redo = null!;
    private Button _save = null!;

    private StudioUx7BProfileDraft? _draft;
    private StudioUx7BPreview? _previewResult;
    private StudioUx7BRunReport? _runReport;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                            !ReferenceEquals(_session, _sourceSession);
    internal bool HasExplicitSelection => _explicitSelection && _draft is not null;
    internal bool CanonicalPreviewReady => _previewResult is
    { Succeeded: true, Changed: true, CanonicalDryRun: true };
    internal bool FourResultsReady => _runReport is { Stages.Count: 4, Isolated: true } &&
                                      _results.ItemCount == 4;
    internal bool ResultNavigationReady => FourResultsReady && _results.GetSelectedItems().Length == 1 &&
                                           !_resultDetails.Text.Contains("Sélectionnez", StringComparison.Ordinal);
    internal bool CancellationReady => _lastCancellation;
    internal bool EvidenceSourceUntouched => SourceMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return Visible && text.Contains("Tests unitaires du domaine", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tests headless runtime", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tests de sauvegarde", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Logs structurés", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Annuler le run", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("pas le runner xUnit", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun runtime Godot", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioQualityLabWorkbenchUx7B(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = session;
        BuildUi();
    }

    private void BuildUi()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.012f, 0.018f, 0.03f, 0.95f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _dialog = StudioStyle.Panel("Ux7BQualityLab", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var contentHost = new Control { ClipContents = true };
        _dialog.AddChild(contentHost);
        contentHost.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = StudioStyle.Margin(15, 12, 15, 12);
        margin.PropagateMaximumSize = true;
        contentHost.AddChild(margin);
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
        titles.AddChild(StudioStyle.Label("LABORATOIRE QUALITÉ · UX7-B", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label(
            "Tests unitaires du domaine · Tests headless runtime · Tests de sauvegarde · Logs structurés",
            18, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Run intégré isolé et annulable · pas le runner xUnit du portail · aucun runtime Godot, réseau ou journal joueur n’est lancé",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = ActionButton("Fermer", Close, 82);
        header.AddChild(close);
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddThemeConstantOverride("separation", 8);
        selection.AddChild(StudioStyle.Label("Profil à tester", 11, StudioStyle.Muted));
        _profileChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_profileChoice);
        _profileChoice.ItemSelected += index => SelectProfile((int)index);
        selection.AddChild(_profileChoice);
        root.AddChild(selection);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 380),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        var body = new HSplitContainer
        {
            SplitOffsets = [780],
        };
        bodyHost.AddChild(body);
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var editorPanel = StudioStyle.Panel("Ux7BProfileEditor", StudioStyle.SurfaceRaised, 8, 1);
        editorPanel.CustomMinimumSize = new Vector2(620, 0);
        body.AddChild(editorPanel);
        var editorMargin = StudioStyle.Margin(10, 8, 10, 8);
        editorPanel.AddChild(editorMargin);
        var editor = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        editor.AddThemeConstantOverride("separation", 6);
        editorMargin.AddChild(editor);
        editor.AddChild(StudioStyle.Label("PROFIL CANONIQUE", 10, StudioStyle.Accent));
        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipTabs = true,
        };
        editor.AddChild(_tabs);
        BuildDomainTab();
        BuildHeadlessTab();
        BuildSaveTab();
        BuildLogsTab();
        _previewSummary = StudioStyle.Label(
            "Aperçu obligatoire avant application · les profils Licences et Validation restent protégés.",
            10, StudioStyle.Muted, wrap: true);
        _previewSummary.CustomMinimumSize = new Vector2(0, 46);
        editor.AddChild(_previewSummary);

        var resultPanel = StudioStyle.Panel("Ux7BResults", StudioStyle.SurfaceRaised, 8, 1);
        resultPanel.CustomMinimumSize = new Vector2(470, 0);
        body.AddChild(resultPanel);
        var resultMargin = StudioStyle.Margin(10, 8, 10, 8);
        resultPanel.AddChild(resultMargin);
        var resultColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        resultColumn.AddThemeConstantOverride("separation", 7);
        resultMargin.AddChild(resultColumn);
        resultColumn.AddChild(StudioStyle.Label("RÉSULTATS NAVIGABLES", 10, StudioStyle.Accent));
        _results = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 180),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
            AllowReselect = true,
        };
        _results.ItemSelected += index => SelectResult((int)index);
        resultColumn.AddChild(_results);
        var detailScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        resultColumn.AddChild(detailScroll);
        var detail = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detail.AddThemeConstantOverride("separation", 7);
        detailScroll.AddChild(detail);
        _resultTitle = StudioStyle.Label("Aucun résultat sélectionné", 15, StudioStyle.Text, wrap: true);
        _resultSummary = StudioStyle.Label("Lancez le profil pour produire quatre résultats.", 11,
            StudioStyle.Muted, wrap: true);
        _resultDetails = StudioStyle.Label("Sélectionnez une famille après le run.", 10,
            StudioStyle.Text, wrap: true);
        detail.AddChild(_resultTitle);
        detail.AddChild(_resultSummary);
        detail.AddChild(new HSeparator());
        detail.AddChild(_resultDetails);

        _status = StudioStyle.Label("Sélectionnez explicitement le profil qualité du module.", 10,
            StudioStyle.Muted, wrap: true);
        _status.CustomMinimumSize = new Vector2(0, 28);
        root.AddChild(_status);

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
        _preview = ActionButton("Aperçu sec", Preview, 104, primary: true);
        _run = ActionButton("Lancer isolé", StartRun, 112, primary: true);
        _cancelRun = ActionButton("Annuler le run", CancelRun, 118);
        _apply = ActionButton("Appliquer", Apply, 92);
        _undo = ActionButton("Annuler", Undo, 82);
        _redo = ActionButton("Rétablir", Redo, 86);
        _save = ActionButton("Enregistrer", Save, 100);
        actions.AddChild(_preview);
        actions.AddChild(_run);
        actions.AddChild(_cancelRun);
        actions.AddChild(_apply);
        actions.AddChild(_undo);
        actions.AddChild(_redo);
        actions.AddChild(_save);
        var actionNote = StudioStyle.Label(
            "Les rapports restent dans le workspace temporaire puis sont supprimés après lecture.",
            9, StudioStyle.Muted, wrap: true);
        actionNote.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actions.AddChild(actionNote);
        root.AddChild(actions);
        WatchControls();
        ResetControls();
    }

    private void BuildDomainTab()
    {
        var page = TabPage("Tests domaine");
        page.AddChild(StudioStyle.Label("ORF-QA-008 · Tests unitaires du domaine", 14, StudioStyle.Text));
        page.AddChild(StudioStyle.Label(
            "Un harness intégré vérifie le profil, son cas négatif et observe les diagnostics du projet sans mutation.",
            10, StudioStyle.Muted, wrap: true));
        _domainAreas = Multiline("Un domaine par ligne", 176);
        page.AddChild(Field("Familles couvertes", _domainAreas));
    }

    private void BuildHeadlessTab()
    {
        var page = TabPage("Runtime headless");
        page.AddChild(StudioStyle.Label("ORF-QA-010 · Tests headless runtime", 14, StudioStyle.Text));
        page.AddChild(StudioStyle.Label(
            "Replay déterministe en mémoire : entrées, événements, seeds et empreintes d’état, sans fenêtre Godot.",
            10, StudioStyle.Muted, wrap: true));
        _headlessScenarios = Multiline("Un scénario par ligne", 150);
        _reportPath = new LineEdit { PlaceholderText = ".forge/quality/reports" };
        StudioStyle.ApplyLineEdit(_reportPath);
        page.AddChild(Field("Scénarios déclarés", _headlessScenarios));
        page.AddChild(Field("Dossier relatif des rapports", _reportPath));
    }

    private void BuildSaveTab()
    {
        var page = TabPage("Sauvegardes");
        page.AddChild(StudioStyle.Label("ORF-QA-015 · Tests de sauvegarde", 14, StudioStyle.Text));
        page.AddChild(StudioStyle.Label(
            "Les six garanties sont exercées sur de vrais fichiers .orpsave dans une copie temporaire.",
            10, StudioStyle.Muted, wrap: true));
        _saveRoundTrip = Check("Round-trip écrire puis relire");
        _saveOldVersions = Check("Migration d’une ancienne version");
        _savePartialFailure = Check("Écriture partielle bloquée");
        _saveMissingPackage = Check("Fichier ou package manquant détecté");
        _saveRollback = Check("Rollback préserve le dernier état valide");
        _saveDeterministic = Check("État et octets déterministes");
        page.AddChild(_saveRoundTrip);
        page.AddChild(_saveOldVersions);
        page.AddChild(_savePartialFailure);
        page.AddChild(_saveMissingPackage);
        page.AddChild(_saveRollback);
        page.AddChild(_saveDeterministic);
    }

    private void BuildLogsTab()
    {
        var page = TabPage("Journaux");
        page.AddChild(StudioStyle.Label("ORF-QA-020 · Logs structurés", 14, StudioStyle.Text));
        page.AddChild(StudioStyle.Label(
            "Échantillon JSON en mémoire avec catégorie, source, transaction et identité joueur masquée ou hachée.",
            10, StudioStyle.Muted, wrap: true));
        _minimumSeverity = Options(Enum.GetNames<StructuredLogSeverity>());
        _logCategories = Multiline("Une catégorie par ligne", 94);
        _includeObjectIds = Check("Inclure l’identifiant humain de l’objet");
        _includeSourceLines = Check("Inclure le chemin et la ligne source");
        _includeTransactions = Check("Inclure la transaction");
        _privacy = Options("Masquer", "Hacher");
        _retentionDays = NumericLine("14");
        _maximumEntries = NumericLine("10000");
        page.AddChild(Field("Sévérité minimale", _minimumSeverity));
        page.AddChild(Field("Catégories", _logCategories));
        page.AddChild(_includeObjectIds);
        page.AddChild(_includeSourceLines);
        page.AddChild(_includeTransactions);
        page.AddChild(Field("Confidentialité joueur", _privacy));
        var limits = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        limits.AddChild(Field("Rétention en jours", _retentionDays, 220));
        limits.AddChild(Field("Entrées maximum", _maximumEntries, 220));
        page.AddChild(limits);
    }

    internal void Open()
    {
        if (_evidenceSession is not null) ReleaseEvidence();
        OpenBound(_sourceSession, evidence: false);
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && ReferenceEquals(_session, _sourceSession)) return;
        CancelRun();
        if (ReferenceEquals(_session, _sourceSession)) _session.CancelQualityFoundationApprovalUx7B();
        Visible = false;
        ReleaseEvidence();
        ResetControls();
    }

    internal void CloseEvidence()
    {
        CancelRun();
        if (_evidenceSession is not null) _evidenceSession.CancelQualityFoundationApprovalUx7B();
        Visible = false;
        ReleaseEvidence();
        ResetControls();
    }

    internal void RestoreSourceSurfaceAfterSmoke() => OpenBound(_sourceSession, evidence: false);

    internal void HideSourceSurfaceAfterSmoke()
    {
        Visible = false;
        _session = _sourceSession;
        ResetControls();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        if (_sourceSession.Project is null || _sourceSession.History is null || _sourceSession.Session is null)
            return FailEvidence("Aucun module source ouvert.");
        _sourceSnapshot = CaptureSource();
        _evidenceReleased = false;
        _evidenceRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux7-b-ui",
            Guid.NewGuid().ToString("N"));
        try
        {
            var created = ProjectSession.Create(_evidenceRoot, _sourceSession.Project);
            if (!created.Succeeded || created.Session is null)
                return FailEvidence("La copie temporaire n’a pas pu être créée.");
            _evidenceSession = new StudioSessionController();
            if (!_evidenceSession.Open(_evidenceRoot).Succeeded)
                return FailEvidence("La session temporaire n’a pas pu être ouverte.");
            OpenBound(_evidenceSession, evidence: true);
            _profileChoice.Select(1);
            SelectProfile(1);
            if (_draft is null) return FailEvidence("Le profil temporaire est absent.");
            _logCategories.Text += System.Environment.NewLine + "studio";
            _retentionDays.Text = Math.Min(365, _draft.RetentionDays + 1)
                .ToString(CultureInfo.InvariantCulture);
            Preview();
            if (!TryReadDraft(out var draft, out _)) return FailEvidence("Le brouillon de preuve est invalide.");
            _runReport = _session.RunIsolatedQualityUx7B(draft, CancellationToken.None);
            using (var cancellation = new CancellationTokenSource())
            {
                var cancelled = _session.RunIsolatedQualityUx7B(draft, cancellation.Token,
                    stage =>
                    {
                        if (stage.Key == "domain") cancellation.Cancel();
                    });
                _lastCancellation = cancelled.Cancelled && cancelled.Stages.Count == 1 &&
                                    cancelled.TemporaryWorkspaceReleased;
            }
            PopulateResults();
            if (_results.ItemCount > 0)
            {
                _results.Select(Math.Min(3, _results.ItemCount - 1));
                SelectResult(Math.Min(3, _results.ItemCount - 1));
            }
            var ready = UsesIsolatedEvidence && HasExplicitSelection && CanonicalPreviewReady &&
                        FourResultsReady && ResultNavigationReady && CancellationReady &&
                        ShowsHumanSurface && SourceMatches();
            if (!ready)
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX7_B_UI_FAILED " +
                            $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                            $"preview={CanonicalPreviewReady.ToString().ToLowerInvariant()} " +
                            $"results={FourResultsReady.ToString().ToLowerInvariant()} " +
                            $"navigation={ResultNavigationReady.ToString().ToLowerInvariant()} " +
                            $"cancel={CancellationReady.ToString().ToLowerInvariant()} " +
                            $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                            $"source={SourceMatches().ToString().ToLowerInvariant()}");
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX7_B_EVIDENCE_FAILED type={exception.GetType().Name}");
            return FailEvidence("La preuve UI a échoué.");
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var ready = PrepareEvidence("ux7-b-quality-lab");
        if (!ready || !UsesIsolatedEvidence || !CanonicalPreviewReady || _evidenceSession is null ||
            !TryReadDraft(out var candidate, out _))
        {
            CloseEvidence();
            return false;
        }
        try
        {
            Apply();
            var applied = _lastApply;
            Undo();
            var undone = _lastUndo;
            Redo();
            var redone = _lastRedo;
            Save();
            var saved = _lastSave;
            var reopened = new StudioSessionController();
            try
            {
                var opened = _evidenceRoot is not null && reopened.Open(_evidenceRoot).Succeeded;
                var persisted = opened && reopened.CurrentQualityFoundationUx7B() is { } reloaded &&
                                DraftContentMatches(candidate, reloaded);
                return applied && undone && redone && saved && persisted && SourceMatches();
            }
            finally
            {
                reopened.Close();
            }
        }
        finally
        {
            CloseEvidence();
        }
    }

    public override void _Process(double delta)
    {
        if (_runTask is null || !_runTask.IsCompleted) return;
        try
        {
            _runReport = _runTask.GetAwaiter().GetResult();
            SetStatus(_runReport.Message, !_runReport.Succeeded && !_runReport.Cancelled);
            PopulateResults();
            if (_results.ItemCount > 0)
            {
                _results.Select(0);
                SelectResult(0);
            }
        }
        catch (Exception exception)
        {
            SetStatus("Run interrompu : " + exception.Message, true);
        }
        finally
        {
            _runTask = null;
            _runCancellation?.Dispose();
            _runCancellation = null;
            SetProcess(false);
            UpdateButtons();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && IsInsideTree()) UpdateBounds();
    }

    private void OpenBound(StudioSessionController controller, bool evidence)
    {
        _session = controller;
        ResetControls(keepReleaseState: evidence);
        _profileChoice.Clear();
        _profileChoice.AddItem("Choisir explicitement le profil du module…");
        var module = _session.Project?.Module.DisplayName ?? "Module ouvert";
        _profileChoice.AddItem($"Profil qualité · {HumanText(module)}");
        _profileChoice.Select(0);
        Visible = true;
        UpdateBounds();
    }

    private void SelectProfile(int index)
    {
        if (_updating) return;
        _explicitSelection = index == 1;
        _draft = _explicitSelection ? _session.CurrentQualityFoundationUx7B() : null;
        _previewResult = null;
        _runReport = null;
        _results.Clear();
        ClearResultDetails();
        if (_draft is null)
        {
            SetEditing(false);
            SetStatus(_explicitSelection ? "Le profil qualité est indisponible." :
                "Sélectionnez explicitement le profil qualité du module.", _explicitSelection);
            return;
        }
        Populate(_draft);
        SetEditing(true);
        SetStatus("Profil chargé · lancez un aperçu sec ou un run isolé.");
    }

    private void Preview()
    {
        if (!TryReadDraft(out var draft, out var error))
        {
            SetStatus(error, true);
            return;
        }
        _draft = draft;
        _previewResult = _session.PreviewQualityFoundationUx7B(draft);
        _previewSummary.Text = _previewResult.SemanticDiff.Count == 0
            ? _previewResult.Message
            : _previewResult.Message + System.Environment.NewLine +
              string.Join(" · ", _previewResult.SemanticDiff);
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
        UpdateButtons();
    }

    private void StartRun()
    {
        if (_runTask is not null)
        {
            SetStatus("Un run est déjà en cours.", true);
            return;
        }
        if (!TryReadDraft(out var draft, out var error))
        {
            SetStatus(error, true);
            return;
        }
        _draft = draft;
        _runCancellation = new CancellationTokenSource();
        var token = _runCancellation.Token;
        _runReport = null;
        _results.Clear();
        ClearResultDetails();
        _runTask = Task.Run(() => _session.RunIsolatedQualityUx7B(draft, token));
        SetProcess(true);
        SetStatus("Run isolé en cours · vous pouvez l’annuler entre deux familles.");
        UpdateButtons();
    }

    private void CancelRun()
    {
        if (_runTask is null || _runCancellation is null) return;
        _runCancellation.Cancel();
        SetStatus("Annulation demandée · le workspace temporaire sera libéré.");
        UpdateButtons();
    }

    private void Apply()
    {
        if (!TryReadDraft(out var draft, out var error))
        {
            SetStatus(error, true);
            return;
        }
        var result = _session.ApplyApprovedQualityFoundationUx7B(draft);
        _lastApply = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) RefreshDraft();
    }

    private void Undo()
    {
        var result = _session.UndoQualityFoundationUx7B();
        _lastUndo = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) RefreshDraft();
    }

    private void Redo()
    {
        var result = _session.RedoQualityFoundationUx7B();
        _lastRedo = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) RefreshDraft();
    }

    private void Save()
    {
        var result = _session.SaveQualityFoundationUx7B();
        _lastSave = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        UpdateButtons();
    }

    private void RefreshDraft()
    {
        _draft = _session.CurrentQualityFoundationUx7B();
        _previewResult = null;
        if (_draft is not null) Populate(_draft);
        UpdateButtons();
    }

    private void Populate(StudioUx7BProfileDraft draft)
    {
        _updating = true;
        _domainAreas.Text = string.Join(System.Environment.NewLine, draft.DomainAreas);
        _headlessScenarios.Text = string.Join(System.Environment.NewLine, draft.HeadlessScenarios);
        _reportPath.Text = draft.ReportRelativePath;
        _saveRoundTrip.ButtonPressed = draft.SaveRoundTrip;
        _saveOldVersions.ButtonPressed = draft.SaveOldVersions;
        _savePartialFailure.ButtonPressed = draft.SavePartialFailure;
        _saveMissingPackage.ButtonPressed = draft.SaveMissingPackage;
        _saveRollback.ButtonPressed = draft.SaveRollback;
        _saveDeterministic.ButtonPressed = draft.SaveDeterministicState;
        _minimumSeverity.Select((int)draft.MinimumSeverity);
        _logCategories.Text = string.Join(System.Environment.NewLine, draft.LogCategories);
        _includeObjectIds.ButtonPressed = draft.IncludeObjectIds;
        _includeSourceLines.ButtonPressed = draft.IncludeSourceLines;
        _includeTransactions.ButtonPressed = draft.IncludeTransactions;
        _privacy.Select((int)draft.PlayerPrivacy);
        _retentionDays.Text = draft.RetentionDays.ToString(CultureInfo.InvariantCulture);
        _maximumEntries.Text = draft.MaximumEntries.ToString(CultureInfo.InvariantCulture);
        _updating = false;
        InvalidatePreview();
    }

    private bool TryReadDraft(out StudioUx7BProfileDraft draft, out string error)
    {
        draft = _draft!;
        error = string.Empty;
        if (_draft is null)
        {
            error = "Sélectionnez d’abord le profil qualité.";
            return false;
        }
        if (!int.TryParse(_retentionDays.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var retention) ||
            !int.TryParse(_maximumEntries.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var maximum))
        {
            error = "Rétention et nombre maximal doivent être des entiers.";
            return false;
        }
        draft = _draft with
        {
            DomainAreas = ReadList(_domainAreas.Text),
            HeadlessScenarios = ReadList(_headlessScenarios.Text),
            ReportRelativePath = _reportPath.Text,
            SaveRoundTrip = _saveRoundTrip.ButtonPressed,
            SaveOldVersions = _saveOldVersions.ButtonPressed,
            SavePartialFailure = _savePartialFailure.ButtonPressed,
            SaveMissingPackage = _saveMissingPackage.ButtonPressed,
            SaveRollback = _saveRollback.ButtonPressed,
            SaveDeterministicState = _saveDeterministic.ButtonPressed,
            MinimumSeverity = (StructuredLogSeverity)_minimumSeverity.Selected,
            LogCategories = ReadList(_logCategories.Text),
            IncludeObjectIds = _includeObjectIds.ButtonPressed,
            IncludeSourceLines = _includeSourceLines.ButtonPressed,
            IncludeTransactions = _includeTransactions.ButtonPressed,
            PlayerPrivacy = (PlayerPrivacyMode)_privacy.Selected,
            RetentionDays = retention,
            MaximumEntries = maximum,
        };
        return true;
    }

    private void PopulateResults()
    {
        _results.Clear();
        ClearResultDetails();
        if (_runReport is null) return;
        foreach (var stage in _runReport.Stages)
        {
            _results.AddItem($"{(stage.Passed ? "✓" : "!")} {stage.Route} · {stage.Label}\n" +
                             $"{stage.Summary} · {stage.DurationMilliseconds} ms");
        }
    }

    private void SelectResult(int index)
    {
        if (_runReport is null || index < 0 || index >= _runReport.Stages.Count)
        {
            ClearResultDetails();
            return;
        }
        var stage = _runReport.Stages[index];
        _resultTitle.Text = $"{stage.Route} · {stage.Label}";
        _resultSummary.Text = $"{(stage.Passed ? "RÉUSSI" : "ÉCHEC")} · {stage.Summary}";
        _resultSummary.Modulate = stage.Passed ? StudioStyle.Success : StudioStyle.Danger;
        _resultDetails.Text = string.Join(System.Environment.NewLine + System.Environment.NewLine,
            stage.Details.Select(detail => "• " + HumanText(detail)));
        _tabs.CurrentTab = index;
    }

    private void ClearResultDetails()
    {
        if (_resultTitle is null) return;
        _resultTitle.Text = "Aucun résultat sélectionné";
        _resultSummary.Text = "Lancez le profil pour produire quatre résultats.";
        _resultSummary.Modulate = StudioStyle.Muted;
        _resultDetails.Text = "Sélectionnez une famille après le run.";
    }

    private void InvalidatePreview()
    {
        if (_updating) return;
        _previewResult = null;
        _session.CancelQualityFoundationApprovalUx7B();
        if (_previewSummary is not null)
            _previewSummary.Text = "Brouillon modifié · relancez l’aperçu sec avant application.";
        UpdateButtons();
    }

    private void WatchControls()
    {
        _domainAreas.TextChanged += InvalidatePreview;
        _headlessScenarios.TextChanged += InvalidatePreview;
        _reportPath.TextChanged += _ => InvalidatePreview();
        foreach (var check in new[]
                 {
                     _saveRoundTrip, _saveOldVersions, _savePartialFailure, _saveMissingPackage,
                     _saveRollback, _saveDeterministic, _includeObjectIds, _includeSourceLines,
                     _includeTransactions,
                 })
            check.Toggled += _ => InvalidatePreview();
        _minimumSeverity.ItemSelected += _ => InvalidatePreview();
        _logCategories.TextChanged += InvalidatePreview;
        _privacy.ItemSelected += _ => InvalidatePreview();
        _retentionDays.TextChanged += _ => InvalidatePreview();
        _maximumEntries.TextChanged += _ => InvalidatePreview();
    }

    private void SetEditing(bool enabled)
    {
        _domainAreas.Editable = enabled;
        _headlessScenarios.Editable = enabled;
        _reportPath.Editable = enabled;
        foreach (var check in new[]
                 {
                     _saveRoundTrip, _saveOldVersions, _savePartialFailure, _saveMissingPackage,
                     _saveRollback, _saveDeterministic, _includeObjectIds, _includeSourceLines,
                     _includeTransactions,
                 })
            check.Disabled = !enabled;
        _minimumSeverity.Disabled = !enabled;
        _logCategories.Editable = enabled;
        _privacy.Disabled = !enabled;
        _retentionDays.Editable = enabled;
        _maximumEntries.Editable = enabled;
        UpdateButtons();
    }

    private void ResetControls(bool keepReleaseState = false)
    {
        _updating = true;
        _explicitSelection = false;
        _draft = null;
        _previewResult = null;
        _runReport = null;
        _lastApply = false;
        _lastUndo = false;
        _lastRedo = false;
        _lastSave = false;
        _lastCancellation = false;
        _domainAreas?.Clear();
        _headlessScenarios?.Clear();
        if (_reportPath is not null) _reportPath.Text = string.Empty;
        _logCategories?.Clear();
        _results?.Clear();
        if (_previewSummary is not null)
            _previewSummary.Text = "Aperçu obligatoire avant application · les profils voisins restent protégés.";
        if (_status is not null) _status.Text = "Sélectionnez explicitement le profil qualité du module.";
        if (!keepReleaseState) _evidenceReleased = true;
        _updating = false;
        ClearResultDetails();
        SetEditing(false);
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(message, error);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        if (_preview is null) return;
        var running = _runTask is not null;
        _preview.Disabled = !HasExplicitSelection || running;
        _run.Disabled = !HasExplicitSelection || running;
        _cancelRun.Disabled = !running;
        _apply.Disabled = !CanonicalPreviewReady || running;
        _undo.Disabled = !HasExplicitSelection || running;
        _redo.Disabled = !HasExplicitSelection || running;
        _save.Disabled = !HasExplicitSelection || running || !_session.QualityFoundationHasUnsavedChangesUx7B;
    }

    private SourceSnapshotUx7B CaptureSource()
    {
        var history = _sourceSession.History!;
        return new(_sourceSession.Project!, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureQualityFoundationApprovalUx7B());
    }

    private bool SourceMatches()
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
               _sourceSession.CaptureQualityFoundationApprovalUx7B() == snapshot.Approval;
    }

    private bool FailEvidence(string message)
    {
        SetStatus(message, true);
        ReleaseEvidence();
        return false;
    }

    private void ReleaseEvidence()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        var released = _evidenceRoot is null || StudioSessionController.DeleteTemporaryBlueprintUx3A(_evidenceRoot);
        _evidenceRoot = null;
        _evidenceReleased = released;
        _session = _sourceSession;
        if (!released) GD.PrintErr("OPENRPG_FORGE_STUDIO_UX7_B_UI_CLEANUP_FAILED");
    }

    private void UpdateBounds()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 54, 1180, 1840);
        var height = Math.Clamp(viewport.Y - 40, 780, 1040);
        _dialog.Position = new Vector2((viewport.X - width) / 2f, (viewport.Y - height) / 2f);
        _dialog.Size = new Vector2(width, height);
    }

    private VBoxContainer TabPage(string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _tabs.AddChild(scroll);
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(page);
        return page;
    }

    private static VBoxContainer Field(string label, Control control, float width = 0)
    {
        var field = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = width > 0 ? new Vector2(width, 0) : Vector2.Zero,
        };
        field.AddThemeConstantOverride("separation", 3);
        field.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        field.AddChild(control);
        return field;
    }

    private static TextEdit Multiline(string placeholder, float height)
    {
        var value = new TextEdit
        {
            PlaceholderText = placeholder,
            CustomMinimumSize = new Vector2(0, height),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        value.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.Surface,
            StudioStyle.Border, 5));
        value.AddThemeStyleboxOverride("focus", StudioStyle.Box(StudioStyle.SurfaceRaised,
            StudioStyle.Accent, 5, 2));
        value.AddThemeColorOverride("font_color", StudioStyle.Text);
        value.AddThemeColorOverride("font_placeholder_color", StudioStyle.Muted);
        value.AddThemeColorOverride("caret_color", StudioStyle.Accent);
        value.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        return value;
    }

    private static CheckBox Check(string text)
    {
        var value = new CheckBox { Text = text };
        value.AddThemeColorOverride("font_color", StudioStyle.Text);
        value.AddThemeFontSizeOverride("font_size", 11);
        return value;
    }

    private static OptionButton Options(params string[] values)
    {
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(option);
        foreach (var value in values) option.AddItem(value);
        return option;
    }

    private static LineEdit NumericLine(string placeholder)
    {
        var line = new LineEdit { PlaceholderText = placeholder };
        StudioStyle.ApplyLineEdit(line);
        return line;
    }

    private static Button ActionButton(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static IReadOnlyList<string> ReadList(string value) => value
        .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim().ToLowerInvariant())
        .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();

    private static bool DraftContentMatches(StudioUx7BProfileDraft expected, StudioUx7BProfileDraft actual) =>
        expected.DomainAreas.Order(StringComparer.Ordinal).SequenceEqual(actual.DomainAreas.Order(StringComparer.Ordinal)) &&
        expected.HeadlessScenarios.Order(StringComparer.Ordinal).SequenceEqual(actual.HeadlessScenarios.Order(StringComparer.Ordinal)) &&
        string.Equals(expected.ReportRelativePath.Replace('\\', '/'), actual.ReportRelativePath.Replace('\\', '/'),
            StringComparison.Ordinal) && expected.SaveRoundTrip == actual.SaveRoundTrip &&
        expected.SaveOldVersions == actual.SaveOldVersions &&
        expected.SavePartialFailure == actual.SavePartialFailure &&
        expected.SaveMissingPackage == actual.SaveMissingPackage && expected.SaveRollback == actual.SaveRollback &&
        expected.SaveDeterministicState == actual.SaveDeterministicState &&
        expected.MinimumSeverity == actual.MinimumSeverity &&
        expected.LogCategories.Order(StringComparer.Ordinal).SequenceEqual(actual.LogCategories.Order(StringComparer.Ordinal)) &&
        expected.IncludeObjectIds == actual.IncludeObjectIds &&
        expected.IncludeSourceLines == actual.IncludeSourceLines &&
        expected.IncludeTransactions == actual.IncludeTransactions &&
        expected.PlayerPrivacy == actual.PlayerPrivacy && expected.RetentionDays == actual.RetentionDays &&
        expected.MaximumEntries == actual.MaximumEntries;

    private static string HumanText(string value) => Regex.Replace(value.Replace('\\', '/'),
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        "élément protégé", RegexOptions.CultureInvariant);

    private static bool ContainsUuid(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    private static IEnumerable<string> DescendantText(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label when label.Visible: yield return label.Text; break;
                case Button button when button.Visible: yield return button.Text; break;
                case LineEdit line when line.Visible: yield return line.Text; break;
                case TextEdit text when text.Visible: yield return text.Text; break;
                case OptionButton option when option.Visible:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
                case ItemList list when list.Visible:
                    for (var index = 0; index < list.ItemCount; index++) yield return list.GetItemText(index);
                    break;
            }
            foreach (var text in DescendantText(child)) yield return text;
        }
    }

    private sealed record SourceSnapshotUx7B(
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
        StudioUx7BApprovalSnapshot Approval);
}
