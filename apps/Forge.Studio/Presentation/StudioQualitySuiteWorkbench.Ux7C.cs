using System.Text.RegularExpressions;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioQualitySuiteWorkbenchUx7C : ColorRect
{
    private readonly StudioSessionController _session;
    private SourceSnapshotUx7C? _sourceSnapshot;
    private StudioUx7CSuiteSelection? _selection;
    private StudioUx7CRunReport? _runReport;
    private StudioUx7CRunReport? _cancellationReport;
    private CancellationTokenSource? _runCancellation;
    private Task<StudioUx7CRunReport>? _runTask;
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
    internal bool FiveResultsReady => _runReport is { Succeeded: true, Stages.Count: 5 } &&
                                      _runReport.Stages.All(stage => stage.Completed) &&
                                      _results.ItemCount == 5;
    internal bool ResultNavigationReady => FiveResultsReady && _results.GetSelectedItems().Length == 1 &&
                                           !_resultDetails.Text.Contains("Sélectionnez", StringComparison.Ordinal);
    internal bool CancellationReady => _lastCancellation &&
                                       _cancellationReport is
                                       {
                                           Cancelled: true, Stages.Count: 1,
                                           TemporaryWorkspaceReleased: true
                                       };
    internal bool EvidenceSourceUntouched => _runReport is { SourceUntouched: true } &&
                                             _cancellationReport is { SourceUntouched: true } &&
                                             SourceMatches();
    internal bool EvidenceResourcesReleased => _runReport is { TemporaryWorkspaceReleased: true } &&
                                                _cancellationReport is { TemporaryWorkspaceReleased: true } &&
                                                _session.LastUx7CTemporaryReleased;
    internal bool SourceSessionRestored => SourceMatches();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return Visible && text.Contains("Tests ForgeScript", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tests de dialogue", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tests de quête", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tests de rencontre", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tests réseau", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Annuler le run", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun socket", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun acteur", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioQualitySuiteWorkbenchUx7C(StudioSessionController session)
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

        _dialog = StudioStyle.Panel("Ux7CQualitySuites", StudioStyle.Surface, 12, 1);
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
        titles.AddChild(StudioStyle.Label("SUITES QUALITÉ · UX7-C", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label(
            "Tests ForgeScript · Tests de dialogue · Tests de quête · Tests de rencontre · Tests réseau",
            18, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Run annulable dans une copie temporaire · verdicts du contenu séparés de la réussite technique du run",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButton("Fermer", Close, 82));
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddThemeConstantOverride("separation", 8);
        selection.AddChild(StudioStyle.Label("Module à tester", 11, StudioStyle.Muted));
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

        var suitePanel = StudioStyle.Panel("Ux7CSuites", StudioStyle.SurfaceRaised, 8, 1);
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
            "Sélectionnez explicitement le module pour afficher les volumes de test.",
            11, StudioStyle.Muted, wrap: true);
        _suiteSummary.CustomMinimumSize = new Vector2(0, 54);
        suiteColumn.AddChild(_suiteSummary);
        suiteColumn.AddChild(SuiteCard("ORF-QA-009", "Tests ForgeScript",
            "Compilation et exécution dans un monde mock gouverné."));
        suiteColumn.AddChild(SuiteCard("ORF-QA-011", "Tests de dialogue",
            "Couverture des nœuds, choix, branches et arrêts déterministes."));
        suiteColumn.AddChild(SuiteCard("ORF-QA-012", "Tests de quête",
            "États, transitions, portées joueur/groupe et reprise de sauvegarde."));
        suiteColumn.AddChild(SuiteCard("ORF-QA-013", "Tests de rencontre",
            "Sélection, navigation et nettoyage déterministes, sans acteur moteur."));
        suiteColumn.AddChild(SuiteCard("ORF-QA-014", "Tests réseau",
            "Latence, pertes, reconnexion, mismatch et autorité en mémoire, sans socket."));
        var boundary = StudioStyle.Label(
            "Frontière honnête · aucun acteur n’est instancié, aucun socket ni pair distant n’est ouvert, " +
            "aucun résultat n’est appliqué au module source.",
            10, StudioStyle.Warning, wrap: true);
        boundary.CustomMinimumSize = new Vector2(0, 58);
        suiteColumn.AddChild(boundary);

        var resultPanel = StudioStyle.Panel("Ux7CResults", StudioStyle.SurfaceRaised, 8, 1);
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
        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        details.AddThemeConstantOverride("separation", 7);
        detailScroll.AddChild(details);
        _resultTitle = StudioStyle.Label("Aucun résultat sélectionné", 15, StudioStyle.Text, wrap: true);
        _resultVerdict = StudioStyle.Label("Le verdict du contenu apparaîtra ici.", 11,
            StudioStyle.Muted, wrap: true);
        _resultSummary = StudioStyle.Label("Lancez les cinq suites isolées.", 10,
            StudioStyle.Muted, wrap: true);
        _resultDetails = StudioStyle.Label("Sélectionnez une famille après le run.", 10,
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
        _run = ActionButton("Lancer les 5 suites", StartRun, 154, primary: true);
        _cancel = ActionButton("Annuler le run", CancelRun, 124);
        actions.AddChild(_run);
        actions.AddChild(_cancel);
        var actionNote = StudioStyle.Label(
            "Les échecs de contenu restent consultables : ils ne sont jamais masqués par le succès technique du runner.",
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
        var module = _session.Project?.Module.DisplayName ?? "Module indisponible";
        _moduleChoice.AddItem($"Suite complète · {HumanText(module)}");
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
        _runReport = _session.RunIsolatedQualityUx7C(_selection, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        _cancellationReport = _session.RunIsolatedQualityUx7C(_selection, cancellation.Token, stage =>
        {
            if (stage.Key == "forgescript") cancellation.Cancel();
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
        var ready = HasExplicitSelection && UsesIsolatedEvidence && FiveResultsReady &&
                    ResultNavigationReady && CancellationReady && EvidenceSourceUntouched &&
                    EvidenceResourcesReleased && ShowsHumanSurface;
        if (!ready)
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX7_C_UI_FAILED " +
                        $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                        $"isolated={UsesIsolatedEvidence.ToString().ToLowerInvariant()} " +
                        $"results={FiveResultsReady.ToString().ToLowerInvariant()} " +
                        $"navigation={ResultNavigationReady.ToString().ToLowerInvariant()} " +
                        $"cancel={CancellationReady.ToString().ToLowerInvariant()} " +
                        $"source={EvidenceSourceUntouched.ToString().ToLowerInvariant()} " +
                        $"released={EvidenceResourcesReleased.ToString().ToLowerInvariant()} " +
                        $"human={ShowsHumanSurface.ToString().ToLowerInvariant()}");
        return ready;
    }

    internal bool RunUiNavigationEvidence()
    {
        if (!PrepareEvidence("ux7-c-quality-suites")) return false;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < _results.ItemCount; index++)
        {
            _results.Select(index);
            SelectResult(index);
            if (_runReport is not null && index < _runReport.Stages.Count &&
                _resultTitle.Text.Contains(_runReport.Stages[index].Title, StringComparison.Ordinal))
                visited.Add(_runReport.Stages[index].Key);
        }
        return visited.SetEquals(["forgescript", "dialogue", "quest", "encounter", "network"]) &&
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
        if (what == NotificationResized && IsInsideTree()) UpdateBounds();
    }

    private void SelectModule(int index)
    {
        if (_updating) return;
        _explicitSelection = index == 1;
        _selection = _explicitSelection ? _session.CurrentQualitySuiteUx7C() : null;
        _runReport = null;
        _cancellationReport = null;
        _lastCancellation = false;
        _results.Clear();
        ClearResultDetails();
        if (_selection is null)
        {
            _suiteSummary.Text = "Sélectionnez explicitement le module pour préparer les cinq suites.";
            SetStatus(_explicitSelection ? "Le module ou ses sources sont indisponibles." :
                "Sélectionnez explicitement un module.", _explicitSelection);
            UpdateButtons();
            return;
        }
        _suiteSummary.Text =
            $"{HumanText(_selection.ModuleName)} · scripts {_selection.ScriptCount} · " +
            $"dialogues {_selection.DialogueCount} · quêtes {_selection.QuestCount} · " +
            $"zones {_selection.AreaCount} · scénarios réseau {_selection.NetworkScenarioCount}.";
        SetStatus("Module sélectionné · lancez les cinq suites dans une copie temporaire.");
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
        _runTask = Task.Run(() => _session.RunIsolatedQualityUx7C(selection, token));
        SetProcess(true);
        SetStatus("Run isolé en cours · annulation possible entre chaque cas et chaque famille.");
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
            var marker = !stage.Completed ? "×" : stage.Passed ? "✓" : "!";
            _results.AddItem($"{marker}  {stage.Route} · {stage.Title} · {stage.PassedCases}/{stage.Cases}");
        }
        UpdateButtons();
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
        _resultVerdict.Text = !stage.Completed
            ? "Suite interrompue · aucun verdict exploitable."
            : stage.Passed
                ? "Verdict du contenu · réussi."
                : "Verdict du contenu · des éléments sont à corriger (le runner a néanmoins terminé).";
        _resultVerdict.Modulate = stage.Passed ? StudioStyle.Success : StudioStyle.Warning;
        _resultSummary.Text = HumanText(stage.Summary);
        _resultDetails.Text = stage.Details.Count == 0
            ? "Aucun détail produit."
            : string.Join(System.Environment.NewLine + System.Environment.NewLine,
                stage.Details.Select(HumanText));
    }

    private void ClearResultDetails()
    {
        if (_resultTitle is null) return;
        _resultTitle.Text = "Aucun résultat sélectionné";
        _resultVerdict.Text = "Le verdict du contenu apparaîtra ici.";
        _resultVerdict.Modulate = StudioStyle.Muted;
        _resultSummary.Text = "Lancez les cinq suites isolées.";
        _resultDetails.Text = "Sélectionnez une famille après le run.";
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
            _suiteSummary.Text = "Sélectionnez explicitement le module pour afficher les volumes de test.";
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

    private SourceSnapshotUx7C CaptureSource()
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

    private static PanelContainer SuiteCard(string route, string title, string description)
    {
        var panel = StudioStyle.Panel("Ux7CSuiteCard", StudioStyle.Surface, 6, 1);
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 2);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label($"{route} · {title}", 12, StudioStyle.Text, wrap: true));
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

    private sealed record SourceSnapshotUx7C(
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
