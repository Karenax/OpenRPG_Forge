using System.Text.RegularExpressions;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPerformanceLabWorkbenchUx7D : ColorRect
{
    private readonly StudioSessionController _session;
    private SourceSnapshotUx7D? _sourceSnapshot;
    private StudioUx7DPerformanceSelection? _selection;
    private StudioUx7DRunReport? _runReport;
    private StudioUx7DRunReport? _cancellationReport;
    private CancellationTokenSource? _runCancellation;
    private Task<StudioUx7DRunReport>? _runTask;
    private bool _updating;
    private bool _explicitSelection;
    private bool _lastCancellation;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private Label _suiteSummary = null!;
    private ItemList _results = null!;
    private Label _resultTitle = null!;
    private Label _resultVerdict = null!;
    private Label _resultSummary = null!;
    private Label _resultDetails = null!;
    private Label _status = null!;
    private Button _run = null!;
    private Button _cancel = null!;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _selection is not null;
    internal bool UsesIsolatedEvidence => _runReport is { Isolated: true };
    internal bool ThreeResultsReady => _runReport is { Succeeded: true, Stages.Count: 3 } &&
                                       _runReport.Stages.All(stage => stage.Completed) &&
                                       _results.ItemCount == 3;
    internal bool ResultNavigationReady => ThreeResultsReady && _results.GetSelectedItems().Length == 1 &&
                                           !_resultDetails.Text.Contains("Sélectionnez", StringComparison.Ordinal);
    internal bool CancellationReady => _lastCancellation &&
                                       _cancellationReport is
                                       {
                                           Cancelled: true, Stages.Count: 1,
                                           TemporaryWorkspaceReleased: true
                                       };
    internal bool ArtifactsConfined => _runReport is { ArtifactsConfined: true };
    internal bool EvidenceSourceUntouched => _runReport is { SourceUntouched: true } &&
                                             _cancellationReport is { SourceUntouched: true } && SourceMatches();
    internal bool EvidenceResourcesReleased => _runReport is { TemporaryWorkspaceReleased: true } &&
                                                _cancellationReport is { TemporaryWorkspaceReleased: true } &&
                                                _session.LastUx7DTemporaryReleased;
    internal bool SourceSessionRestored => SourceMatches();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx7D(this));
            return Visible && text.Contains("Benchmarks de zone", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Budgets par plateforme", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Profilage intégré", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Annuler le run", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("modèles déterministes", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Chrome/Perfetto", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tests visuels runtime restent planifiés", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tracy reste planifié", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx7D(text);
        }
    }

    internal StudioPerformanceLabWorkbenchUx7D(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUi();
    }

    private void BuildUi()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.012f, 0.018f, 0.03f, 0.95f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _dialog = StudioStyle.Panel("Ux7DPerformanceLab", StudioStyle.Surface, 12, 1);
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
        titles.AddChild(StudioStyle.Label("PERFORMANCE ET PROFILAGE · UX7-D", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label(
            "Benchmarks de zone · Budgets par plateforme · Profilage intégré",
            18, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Run annulable dans une copie temporaire · résultats techniques et verdicts de budget séparés",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButtonUx7D("Fermer", Close, 82));
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddThemeConstantOverride("separation", 8);
        selection.AddChild(StudioStyle.Label("Module à mesurer", 11, StudioStyle.Muted));
        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_moduleChoice);
        _moduleChoice.ItemSelected += index => SelectModule((int)index);
        selection.AddChild(_moduleChoice);
        root.AddChild(selection);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 420),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        var body = new HSplitContainer { SplitOffsets = [650] };
        bodyHost.AddChild(body);
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var suitePanel = StudioStyle.Panel("Ux7DPerformanceScope", StudioStyle.SurfaceRaised, 8, 1);
        suitePanel.CustomMinimumSize = new Vector2(520, 0);
        body.AddChild(suitePanel);
        var suiteMargin = StudioStyle.Margin(10, 8, 10, 8);
        suitePanel.AddChild(suiteMargin);
        var suiteColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        suiteColumn.AddThemeConstantOverride("separation", 7);
        suiteMargin.AddChild(suiteColumn);
        suiteColumn.AddChild(StudioStyle.Label("PÉRIMÈTRE DU RUN", 10, StudioStyle.Accent));
        _suiteSummary = StudioStyle.Label(
            "Sélectionnez explicitement le module pour afficher scénarios, budgets et surfaces.",
            11, StudioStyle.Muted, wrap: true);
        _suiteSummary.CustomMinimumSize = new Vector2(0, 54);
        suiteColumn.AddChild(_suiteSummary);
        suiteColumn.AddChild(StageCardUx7D("ORF-QA-017", "Benchmarks de zone",
            "Navigation reconstruite réellement ; frame, mémoire, textures, script et réseau restent des modèles déterministes."));
        suiteColumn.AddChild(StageCardUx7D("ORF-QA-018", "Budgets par plateforme",
            "Desktop, mobile et serveur dédié évalués avec seuils d’avertissement et gate configuré."));
        suiteColumn.AddChild(StageCardUx7D("ORF-QA-019", "Profilage intégré",
            "Spans éditeur, runtime et serveur exportés en JSON Chrome/Perfetto dans l’isolat."));
        var boundary = StudioStyle.Label(
            "Frontière honnête · les Tests visuels runtime restent planifiés ; Tracy reste planifié tant qu’aucun " +
            "export natif n’est branché. Aucun compteur GPU constructeur n’est mesuré.",
            10, StudioStyle.Warning, wrap: true);
        boundary.CustomMinimumSize = new Vector2(0, 66);
        suiteColumn.AddChild(boundary);

        var resultPanel = StudioStyle.Panel("Ux7DPerformanceResults", StudioStyle.SurfaceRaised, 8, 1);
        resultPanel.CustomMinimumSize = new Vector2(520, 0);
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
            CustomMinimumSize = new Vector2(0, 150),
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
        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        details.AddThemeConstantOverride("separation", 7);
        detailScroll.AddChild(details);
        _resultTitle = StudioStyle.Label("Aucun résultat sélectionné", 15, StudioStyle.Text, wrap: true);
        _resultVerdict = StudioStyle.Label("Le verdict apparaîtra ici.", 11, StudioStyle.Muted, wrap: true);
        _resultSummary = StudioStyle.Label("Lancez les trois analyses isolées.", 10,
            StudioStyle.Muted, wrap: true);
        _resultDetails = StudioStyle.Label("Sélectionnez un résultat après le run.", 10,
            StudioStyle.Text, wrap: true);
        details.AddChild(_resultTitle);
        details.AddChild(_resultVerdict);
        details.AddChild(_resultSummary);
        details.AddChild(new HSeparator());
        details.AddChild(_resultDetails);

        _status = StudioStyle.Label("Sélectionnez explicitement un module.", 10,
            StudioStyle.Muted, wrap: true);
        _status.CustomMinimumSize = new Vector2(0, 30);
        root.AddChild(_status);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 7);
        _run = ActionButtonUx7D("Lancer les 3 analyses", StartRun, 170, primary: true);
        _cancel = ActionButtonUx7D("Annuler le run", CancelRun, 124);
        actions.AddChild(_run);
        actions.AddChild(_cancel);
        var actionNote = StudioStyle.Label(
            "Un budget dépassé reste visible : il ne transforme pas un runner terminé en faux échec technique.",
            10, StudioStyle.Muted, wrap: true);
        actionNote.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actions.AddChild(actionNote);
        root.AddChild(actions);
        UpdateButtons();
    }

    internal void Open()
    {
        if (_runTask is not null)
        {
            Visible = true;
            UpdateBounds();
            return;
        }
        ResetControls();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Choisir explicitement le module ouvert…");
        _moduleChoice.AddItem($"Mesurer · {HumanTextUx7D(_session.Project?.Module.DisplayName ?? "Module indisponible")}");
        _moduleChoice.Select(0);
        Visible = true;
        UpdateBounds();
    }

    internal void Close()
    {
        if (!Visible && _runTask is null) return;
        CancelRun();
        Visible = false;
        if (_runTask is null) ResetControls();
    }

    internal void CloseEvidence()
    {
        Close();
        if (_runTask is null) ResetControls();
    }

    internal void RestoreSourceSurfaceAfterSmoke() => Open();

    internal void HideSourceSurfaceAfterSmoke()
    {
        Visible = false;
        if (_runTask is null) ResetControls();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        Open();
        _moduleChoice.Select(1);
        SelectModule(1);
        if (_selection is null) return false;
        _sourceSnapshot = CaptureSource();
        _runReport = _session.RunIsolatedPerformanceUx7D(_selection, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        _cancellationReport = _session.RunIsolatedPerformanceUx7D(_selection, cancellation.Token, stage =>
        {
            if (stage.Key == "benchmarks") cancellation.Cancel();
        });
        _lastCancellation = _cancellationReport.Cancelled && _cancellationReport.Stages.Count == 1 &&
                            _cancellationReport.TemporaryWorkspaceReleased;
        PopulateResults();
        if (_results.ItemCount > 0)
        {
            var index = _results.ItemCount - 1;
            _results.Select(index);
            SelectResult(index);
        }
        SetStatus(_runReport.Message, !_runReport.Succeeded);
        var ready = HasExplicitSelection && UsesIsolatedEvidence && ThreeResultsReady &&
                    ResultNavigationReady && CancellationReady && ArtifactsConfined &&
                    EvidenceSourceUntouched && EvidenceResourcesReleased && ShowsHumanSurface;
        if (!ready)
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX7_D_UI_FAILED " +
                        $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                        $"isolated={UsesIsolatedEvidence.ToString().ToLowerInvariant()} " +
                        $"results={ThreeResultsReady.ToString().ToLowerInvariant()} " +
                        $"navigation={ResultNavigationReady.ToString().ToLowerInvariant()} " +
                        $"cancel={CancellationReady.ToString().ToLowerInvariant()} " +
                        $"artifacts={ArtifactsConfined.ToString().ToLowerInvariant()} " +
                        $"source={EvidenceSourceUntouched.ToString().ToLowerInvariant()} " +
                        $"released={EvidenceResourcesReleased.ToString().ToLowerInvariant()} " +
                        $"human={ShowsHumanSurface.ToString().ToLowerInvariant()}");
        return ready;
    }

    internal bool RunUiNavigationEvidence()
    {
        if (!PrepareEvidence("ux7-d-performance-lab")) return false;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < _results.ItemCount; index++)
        {
            _results.Select(index);
            SelectResult(index);
            if (_runReport is not null && index < _runReport.Stages.Count)
                visited.Add(_runReport.Stages[index].Key);
        }
        return visited.SetEquals(["benchmarks", "budgets", "profiling"]) &&
               ResultNavigationReady && SourceMatches();
    }

    public override void _Process(double delta)
    {
        _ = delta;
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
            SetStatus("Run interrompu · " + exception.Message, true);
        }
        finally
        {
            _runTask = null;
            _runCancellation?.Dispose();
            _runCancellation = null;
            SetProcess(false);
            UpdateButtons();
            if (!Visible) ResetControls();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBounds();
    }

    private void SelectModule(int index)
    {
        if (_updating) return;
        _explicitSelection = index == 1;
        _selection = _explicitSelection ? _session.CurrentPerformanceLabUx7D() : null;
        _runReport = null;
        _cancellationReport = null;
        _lastCancellation = false;
        _results.Clear();
        ClearResultDetails();
        if (_selection is null)
        {
            _suiteSummary.Text = "Sélectionnez explicitement le module pour préparer les mesures.";
            SetStatus("Aucun module sélectionné.");
        }
        else
        {
            _suiteSummary.Text =
                $"{HumanTextUx7D(_selection.ModuleName)} · {_selection.AreaCount} zones · " +
                $"{_selection.ScriptCount} scripts · {_selection.ScenarioCount} scénarios · " +
                $"{_selection.BudgetCount} plateformes · {_selection.ProfilerSurfaceCount} surfaces.";
            SetStatus("Module sélectionné · le run restera dans une copie temporaire.");
        }
        UpdateButtons();
    }

    private void StartRun()
    {
        if (_runTask is not null || _selection is null)
        {
            SetStatus("Sélectionnez le module ou attendez la fin du run courant.", true);
            return;
        }
        _sourceSnapshot = CaptureSource();
        _runReport = null;
        _cancellationReport = null;
        _lastCancellation = false;
        _results.Clear();
        ClearResultDetails();
        _runCancellation = new CancellationTokenSource();
        var selection = _selection;
        var token = _runCancellation.Token;
        _runTask = Task.Run(() => _session.RunIsolatedPerformanceUx7D(selection, token));
        SetProcess(true);
        SetStatus("Run isolé en cours · annulation possible entre chaque analyse.");
        UpdateButtons();
    }

    private void CancelRun()
    {
        if (_runTask is null || _runCancellation is null) return;
        _runCancellation.Cancel();
        SetStatus("Annulation demandée · la copie temporaire sera libérée.");
        UpdateButtons();
    }

    private void PopulateResults()
    {
        _results.Clear();
        if (_runReport is null) return;
        foreach (var stage in _runReport.Stages)
        {
            var verdict = stage.Passed ? "CONFORME" : stage.Completed ? "À EXAMINER" : "INCOMPLET";
            _results.AddItem($"{stage.Route} · {stage.Title} · {verdict} · {stage.Cases} cas");
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
        _resultTitle.Text = $"{stage.Route} · {stage.Title}";
        _resultVerdict.Text = stage.Passed ? "Conforme" : stage.Completed ? "À examiner" : "Incomplet";
        _resultVerdict.Modulate = stage.Passed ? StudioStyle.Success :
            stage.Completed ? StudioStyle.Warning : StudioStyle.Danger;
        _resultSummary.Text = $"{stage.Summary} · avertissements {stage.WarningCases} · blocages {stage.FailedCases}";
        _resultDetails.Text = stage.Details.Count == 0
            ? "Aucun détail produit."
            : string.Join(System.Environment.NewLine + System.Environment.NewLine,
                stage.Details.Select(HumanTextUx7D));
    }

    private void ClearResultDetails()
    {
        if (_resultTitle is null) return;
        _resultTitle.Text = "Aucun résultat sélectionné";
        _resultVerdict.Text = "Le verdict apparaîtra ici.";
        _resultVerdict.Modulate = StudioStyle.Muted;
        _resultSummary.Text = "Lancez les trois analyses isolées.";
        _resultDetails.Text = "Sélectionnez un résultat après le run.";
    }

    private void ResetControls()
    {
        _updating = true;
        _explicitSelection = false;
        _selection = null;
        _runReport = null;
        _cancellationReport = null;
        _lastCancellation = false;
        _sourceSnapshot = null;
        _results?.Clear();
        if (_suiteSummary is not null)
            _suiteSummary.Text = "Sélectionnez explicitement le module pour afficher scénarios, budgets et surfaces.";
        if (_status is not null) _status.Text = "Sélectionnez explicitement un module.";
        _updating = false;
        ClearResultDetails();
        UpdateButtons();
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
        if (_run is null) return;
        var running = _runTask is not null;
        _run.Disabled = !HasExplicitSelection || running;
        _cancel.Disabled = !running;
    }

    private SourceSnapshotUx7D CaptureSource()
    {
        var history = _session.History!;
        return new(_session.Project!, history, history.State.Revision, history.Journal.ToArray(),
            _session.IsDirty, history.CanUndo, history.CanRedo, _session.ActiveAreaId,
            _session.SelectedEntityIds.ToArray(), _session.SelectedEntityId);
    }

    private bool SourceMatches()
    {
        var snapshot = _sourceSnapshot;
        if (snapshot is null) return true;
        var history = _session.History;
        return ReferenceEquals(snapshot.Project, _session.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _session.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _session.ActiveAreaId == snapshot.AreaId &&
               _session.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _session.SelectedEntityId == snapshot.SelectedEntityId;
    }

    private void UpdateBounds()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 54, 1180, 1840);
        var height = Math.Clamp(viewport.Y - 40, 780, 1040);
        _dialog.Position = new Vector2((viewport.X - width) / 2f, (viewport.Y - height) / 2f);
        _dialog.Size = new Vector2(width, height);
    }

    private static PanelContainer StageCardUx7D(string route, string title, string description)
    {
        var panel = StudioStyle.Panel("Ux7DStageCard", StudioStyle.Surface, 6, 1);
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 2);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label($"{route} · {title}", 12, StudioStyle.Text, wrap: true));
        column.AddChild(StudioStyle.Label(description, 9, StudioStyle.Muted, wrap: true));
        return panel;
    }

    private static Button ActionButtonUx7D(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static string HumanTextUx7D(string value) => Regex.Replace(value.Replace('\\', '/'),
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        "élément protégé", RegexOptions.CultureInvariant);

    private static bool ContainsUuidUx7D(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    private static IEnumerable<string> DescendantTextUx7D(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label when label.Visible: yield return label.Text; break;
                case Button button when button.Visible: yield return button.Text; break;
                case OptionButton option when option.Visible:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
                case ItemList list when list.Visible:
                    for (var index = 0; index < list.ItemCount; index++) yield return list.GetItemText(index);
                    break;
            }
            foreach (var text in DescendantTextUx7D(child)) yield return text;
        }
    }

    private sealed record SourceSnapshotUx7D(
        Forge.Domain.Projects.ProjectDefinition Project,
        Forge.Application.Commands.ProjectCommandHistory History,
        Forge.Application.Projects.ProjectRevision Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool IsDirty,
        bool CanUndo,
        bool CanRedo,
        Forge.Domain.Areas.AreaId? AreaId,
        IReadOnlyList<Forge.Domain.Entities.EntityId> SelectedEntityIds,
        Forge.Domain.Entities.EntityId? SelectedEntityId);
}
