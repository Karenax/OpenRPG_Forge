using System.Text.RegularExpressions;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioVisualRegressionWorkbenchUx7H : ColorRect
{
    private readonly StudioSessionController _session;
    private SourceSnapshotUx7H? _sourceSnapshot;
    private StudioUx7HVisualSelection? _selection;
    private StudioUx7HVisualRunReport? _report;
    private StudioUx7HVisualRunReport? _cancelledReport;
    private CancellationTokenSource? _cancellation;
    private Task<StudioUx7HVisualRunReport>? _runTask;
    private bool _explicitSelection;
    private bool _updating;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private Label _scope = null!;
    private ItemList _results = null!;
    private Label _detailTitle = null!;
    private Label _detailVerdict = null!;
    private Label _detailSummary = null!;
    private Label _detailBody = null!;
    private Label _status = null!;
    private Button _run = null!;
    private Button _cancel = null!;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _selection is not null;
    internal bool UsesOfficialEngine => _report is { OfficialEngine: true, RuntimeProcess: true };
    internal bool VisualResultReady => _report is { Succeeded: true, Stages.Count: 1 } &&
                                       _report.Stages[0] is
                                       {
                                           Route: "ORF-QA-016", Passed: true, Cases: 8,
                                           ArtifactCount: 25
                                       } && _results.ItemCount == 1;
    internal bool ResultNavigationReady => VisualResultReady && _results.GetSelectedItems().Length == 1 &&
                                           !_detailBody.Text.Contains("Sélectionnez", StringComparison.Ordinal);
    internal bool CancellationReady => _cancelledReport is
    {
        Cancelled: true, RuntimeProcess: true,
        TemporaryWorkspaceReleased: true
    };
    internal bool ArtifactConfined => _report is { ArtifactsConfined: true };
    internal bool EvidenceSourceUntouched => _report is { SourceUntouched: true } &&
                                             _cancelledReport is { SourceUntouched: true } && SourceMatches();
    internal bool EvidenceResourcesReleased => _report is { TemporaryWorkspaceReleased: true } &&
                                                _cancelledReport is { TemporaryWorkspaceReleased: true } &&
                                                _session.LastUx7HTemporaryReleased;
    internal bool SourceSessionRestored => SourceMatches();
    internal bool TracyDeferred => ShowsHumanSurface;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return Visible &&
                   text.Contains("Tests visuels", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Runtime réel", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Profiler Tracy", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("moteur instrumenté", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("non qualifié", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Annuler le run", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("copie temporaire", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioVisualRegressionWorkbenchUx7H(StudioSessionController session)
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

        _dialog = StudioStyle.Panel("Ux7HVisualRegression", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var host = new Control { ClipContents = true };
        _dialog.AddChild(host);
        host.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = StudioStyle.Margin(15, 12, 15, 12);
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
        titles.AddChild(StudioStyle.Label("RÉGRESSION VISUELLE RUNTIME · UX7-H", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Tests visuels réels · 8 comparaisons · résultats navigables",
            18, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Godot 4.7.1 .NET officiel · run annulable · source ouverte inchangée",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButton("Fermer", Close, 82));
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddThemeConstantOverride("separation", 8);
        var moduleLabel = StudioStyle.Label("Module à vérifier", 11, StudioStyle.Muted);
        moduleLabel.CustomMinimumSize = new Vector2(140, 0);
        selection.AddChild(moduleLabel);
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

        var scopePanel = StudioStyle.Panel("Ux7HVisualScope", StudioStyle.SurfaceRaised, 8, 1);
        scopePanel.CustomMinimumSize = new Vector2(520, 0);
        body.AddChild(scopePanel);
        var scopeMargin = StudioStyle.Margin(10, 8, 10, 8);
        scopePanel.AddChild(scopeMargin);
        var scopeScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        scopeMargin.AddChild(scopeScroll);
        var scopeColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scopeColumn.AddThemeConstantOverride("separation", 7);
        scopeScroll.AddChild(scopeColumn);
        scopeColumn.AddChild(StudioStyle.Label("PÉRIMÈTRE DU RUN", 10, StudioStyle.Accent));
        _scope = StudioStyle.Label("Sélectionnez explicitement le module.", 10, StudioStyle.Muted, wrap: true);
        _scope.CustomMinimumSize = new Vector2(0, 48);
        scopeColumn.AddChild(_scope);
        scopeColumn.AddChild(RouteCard("ORF-QA-016", "Tests visuels · QUALIFIÉ PAR CE LOT",
            "Runtime réel : 2 résolutions × 2 caméras × 2 qualités, références et candidats PNG, tolérance pixel et revues JSON."));
        scopeColumn.AddChild(RouteCard("ORF-EE-029", "Profiler Tracy · NON QUALIFIÉ",
            "Un moteur instrumenté Tracy et le capture server de version identique sont obligatoires. " +
            "Le dépôt interdit ce build personnalisé sans validation explicite; aucune trace JSON n’est renommée."));
        var boundary = StudioStyle.Label(
            "Frontière honnête · le Runtime officiel lit une copie temporaire. Les 25 artefacts sont vérifiés puis " +
            "supprimés avec l’isolat. Aucun moteur instrumenté, capture Tracy native ou compteur GPU n’est revendiqué.",
            10, StudioStyle.Warning, wrap: true);
        boundary.CustomMinimumSize = new Vector2(0, 72);
        scopeColumn.AddChild(boundary);

        var resultsPanel = StudioStyle.Panel("Ux7HVisualResults", StudioStyle.SurfaceRaised, 8, 1);
        resultsPanel.CustomMinimumSize = new Vector2(520, 0);
        body.AddChild(resultsPanel);
        var resultsMargin = StudioStyle.Margin(10, 8, 10, 8);
        resultsPanel.AddChild(resultsMargin);
        var resultsColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        resultsColumn.AddThemeConstantOverride("separation", 7);
        resultsMargin.AddChild(resultsColumn);
        resultsColumn.AddChild(StudioStyle.Label("RÉSULTAT NAVIGABLE", 10, StudioStyle.Accent));
        _results = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 94),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
            AllowReselect = true,
        };
        _results.ItemSelected += index => SelectResult((int)index);
        resultsColumn.AddChild(_results);
        var detailScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        resultsColumn.AddChild(detailScroll);
        var detail = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detail.AddThemeConstantOverride("separation", 7);
        detailScroll.AddChild(detail);
        _detailTitle = StudioStyle.Label("Aucun résultat sélectionné", 15, StudioStyle.Text, wrap: true);
        _detailVerdict = StudioStyle.Label("Le verdict apparaîtra ici.", 11, StudioStyle.Muted, wrap: true);
        _detailSummary = StudioStyle.Label("Lancez la suite visuelle Runtime.", 10, StudioStyle.Muted, wrap: true);
        _detailBody = StudioStyle.Label("Sélectionnez le résultat après le run.", 10, StudioStyle.Text, wrap: true);
        detail.AddChild(_detailTitle);
        detail.AddChild(_detailVerdict);
        detail.AddChild(_detailSummary);
        detail.AddChild(new HSeparator());
        detail.AddChild(_detailBody);

        _status = StudioStyle.Label("Sélectionnez explicitement un module.", 10, StudioStyle.Muted, wrap: true);
        _status.CustomMinimumSize = new Vector2(0, 30);
        root.AddChild(_status);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 7);
        _run = ActionButton("Lancer les 8 captures", StartRun, 190, primary: true);
        _cancel = ActionButton("Annuler le run", CancelRun, 140);
        actions.AddChild(_run);
        actions.AddChild(_cancel);
        var note = StudioStyle.Label(
            "Le processus Runtime est réel; les résultats restent lisibles après la suppression de la copie temporaire.",
            10, StudioStyle.Muted, wrap: true);
        note.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actions.AddChild(note);
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
        _moduleChoice.AddItem($"Vérifier · {HumanText(_session.Project?.Module.DisplayName ?? "Module indisponible")}");
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
        _report = _session.RunVisualRegressionUx7H(_selection, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        _cancelledReport = _session.RunVisualRegressionUx7H(_selection, cancellation.Token, cancellation.Cancel);
        PopulateResults();
        if (_results.ItemCount > 0)
        {
            _results.Select(0);
            SelectResult(0);
        }
        SetStatus(_report.Message, !_report.Succeeded);
        var ready = HasExplicitSelection && UsesOfficialEngine && VisualResultReady && ResultNavigationReady &&
                    CancellationReady && ArtifactConfined && EvidenceSourceUntouched &&
                    EvidenceResourcesReleased && ShowsHumanSurface;
        if (!ready)
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX7_H_UI_FAILED " +
                        $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                        $"engine={UsesOfficialEngine.ToString().ToLowerInvariant()} " +
                        $"result={VisualResultReady.ToString().ToLowerInvariant()} " +
                        $"navigation={ResultNavigationReady.ToString().ToLowerInvariant()} " +
                        $"cancel={CancellationReady.ToString().ToLowerInvariant()} " +
                        $"artifact={ArtifactConfined.ToString().ToLowerInvariant()} " +
                        $"source={EvidenceSourceUntouched.ToString().ToLowerInvariant()} " +
                        $"released={EvidenceResourcesReleased.ToString().ToLowerInvariant()} " +
                        $"human={ShowsHumanSurface.ToString().ToLowerInvariant()}");
        return ready;
    }

    internal bool RunUiNavigationEvidence()
    {
        if (!PrepareEvidence("ux7-h-visual-regression")) return false;
        _results.Select(0);
        SelectResult(0);
        return ResultNavigationReady && SourceMatches();
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (_runTask is null || !_runTask.IsCompleted) return;
        try
        {
            _report = _runTask.GetAwaiter().GetResult();
            PopulateResults();
            if (_results.ItemCount > 0)
            {
                _results.Select(0);
                SelectResult(0);
            }
            SetStatus(_report.Message, !_report.Succeeded && !_report.Cancelled);
        }
        catch (Exception exception)
        {
            SetStatus("Run interrompu · " + exception.Message, true);
        }
        finally
        {
            _runTask = null;
            _cancellation?.Dispose();
            _cancellation = null;
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
        _selection = _explicitSelection ? _session.CurrentVisualRegressionUx7H() : null;
        _report = null;
        _cancelledReport = null;
        _results.Clear();
        ClearDetails();
        if (_selection is null)
        {
            _scope.Text = "Sélectionnez explicitement le module pour préparer le run.";
            SetStatus("Aucun module sélectionné.");
        }
        else
        {
            _scope.Text = $"{HumanText(_selection.ModuleName)} · {_selection.CaseCount} cas · " +
                          $"{_selection.ResolutionCount} résolutions · {_selection.CameraCount} caméras · " +
                          $"{_selection.QualityTierCount} qualités · moteur {_selection.EngineVersion} · " +
                          $"runtime {(_selection.RuntimeReady ? "compilé" : "indisponible")}.";
            SetStatus(_selection.OfficialEngineReady && _selection.RuntimeReady
                ? "Module sélectionné · les captures resteront dans une copie temporaire."
                : "Prérequis manquant · moteur officiel ou Forge.Runtime compilé introuvable.",
                !_selection.OfficialEngineReady || !_selection.RuntimeReady);
        }
        UpdateButtons();
    }

    private void StartRun()
    {
        if (_runTask is not null || _selection is null || !_selection.OfficialEngineReady ||
            !_selection.RuntimeReady)
        {
            SetStatus("Sélectionnez le module et vérifiez les prérequis Runtime.", true);
            return;
        }
        _sourceSnapshot = CaptureSource();
        _report = null;
        _cancelledReport = null;
        _results.Clear();
        ClearDetails();
        _cancellation = new CancellationTokenSource();
        var selection = _selection;
        var token = _cancellation.Token;
        _runTask = Task.Run(() => _session.RunVisualRegressionUx7H(selection, token));
        SetProcess(true);
        SetStatus("Runtime officiel en cours · huit comparaisons visuelles · annulation disponible.");
        UpdateButtons();
    }

    private void CancelRun()
    {
        if (_runTask is null || _cancellation is null) return;
        _cancellation.Cancel();
        SetStatus("Annulation demandée · le processus Runtime et l’isolat seront libérés.");
        UpdateButtons();
    }

    private void PopulateResults()
    {
        _results.Clear();
        if (_report is null) return;
        foreach (var stage in _report.Stages)
        {
            var verdict = stage.Passed ? "CONFORME" : stage.Completed ? "À EXAMINER" : "INCOMPLET";
            _results.AddItem($"{stage.Route} · {stage.Title} · {verdict} · {stage.Cases} cas · " +
                             $"{stage.ArtifactCount} artefacts");
        }
    }

    private void SelectResult(int index)
    {
        if (_report is null || index < 0 || index >= _report.Stages.Count)
        {
            ClearDetails();
            return;
        }
        var stage = _report.Stages[index];
        _detailTitle.Text = $"{stage.Route} · {stage.Title}";
        _detailVerdict.Text = stage.Passed ? "Conforme · rendu Runtime réel" : "À examiner";
        _detailVerdict.Modulate = stage.Passed ? StudioStyle.Success : StudioStyle.Warning;
        _detailSummary.Text = stage.Summary;
        _detailBody.Text = string.Join('\n', stage.Details.Select(detail => "• " + HumanText(detail)));
    }

    private void ClearDetails()
    {
        _detailTitle.Text = "Aucun résultat sélectionné";
        _detailVerdict.Text = "Le verdict apparaîtra ici.";
        _detailVerdict.Modulate = StudioStyle.Muted;
        _detailSummary.Text = "Lancez la suite visuelle Runtime.";
        _detailBody.Text = "Sélectionnez le résultat après le run.";
    }

    private void ResetControls()
    {
        _updating = true;
        _explicitSelection = false;
        _selection = null;
        _report = null;
        _cancelledReport = null;
        _results.Clear();
        _scope.Text = "Sélectionnez explicitement le module.";
        ClearDetails();
        _status.Text = "Sélectionnez explicitement un module.";
        _status.Modulate = StudioStyle.Muted;
        _updating = false;
        _session.CloseUx7H();
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var running = _runTask is not null;
        _run.Disabled = running || _selection is null || !_selection.OfficialEngineReady ||
                        !_selection.RuntimeReady;
        _cancel.Disabled = !running;
        _moduleChoice.Disabled = running;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = HumanText(message);
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(_status.Text, error);
    }

    private SourceSnapshotUx7H CaptureSource() => new(
        _session.Project!, _session.History!, _session.History!.State.Revision,
        _session.History.Journal.ToArray(), _session.IsDirty, _session.History.CanUndo,
        _session.History.CanRedo, _session.ActiveAreaId, _session.SelectedEntityIds.ToArray(),
        _session.SelectedEntityId);

    private bool SourceMatches()
    {
        var snapshot = _sourceSnapshot;
        var history = _session.History;
        return snapshot is not null && ReferenceEquals(snapshot.Project, _session.Project) &&
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

    private static PanelContainer RouteCard(string route, string title, string description)
    {
        var panel = StudioStyle.Panel("Ux7HRouteCard", StudioStyle.Surface, 6, 1);
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 3);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label($"{route} · {title}", 11, StudioStyle.Text, wrap: true));
        column.AddChild(StudioStyle.Label(description, 9, StudioStyle.Muted, wrap: true));
        return panel;
    }

    private static Button ActionButton(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

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

    private sealed record SourceSnapshotUx7H(
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
