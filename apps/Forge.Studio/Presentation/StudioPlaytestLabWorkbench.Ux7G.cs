using System.Text.RegularExpressions;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioPlaytestLabWorkbenchUx7G : ColorRect
{
    private readonly StudioSessionController _session;
    private SourceSnapshotUx7G? _sourceSnapshot;
    private StudioUx7GPlaytestSelection? _selection;
    private StudioUx7GPlaytestRunReport? _report;
    private StudioUx7GPlaytestRunReport? _cancelledReport;
    private CancellationTokenSource? _cancellation;
    private Task<StudioUx7GPlaytestRunReport>? _runTask;
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
    internal bool UsesIsolatedEvidence => _report is { Isolated: true };
    internal bool FiveResultsReady => _report is { Succeeded: true, Stages.Count: 5 } &&
                                      _report.Stages.All(stage => stage.Completed && stage.Passed) &&
                                      _results.ItemCount == 5;
    internal bool ResultNavigationReady => FiveResultsReady && _results.GetSelectedItems().Length == 1 &&
                                           !_detailBody.Text.Contains("Sélectionnez", StringComparison.Ordinal);
    internal bool CancellationReady => _cancelledReport is
    {
        Cancelled: true, Stages.Count: 1,
        TemporaryWorkspaceReleased: true
    };
    internal bool ArtifactConfined => _report is { ArtifactsConfined: true };
    internal bool EvidenceSourceUntouched => _report is { SourceUntouched: true } &&
                                             _cancelledReport is { SourceUntouched: true } && SourceMatches();
    internal bool EvidenceResourcesReleased => _report is { TemporaryWorkspaceReleased: true } &&
                                                _cancelledReport is { TemporaryWorkspaceReleased: true } &&
                                                _session.LastUx7GTemporaryReleased;
    internal bool SourceSessionRestored => SourceMatches();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return Visible &&
                   text.Contains("Aperçu et mode test", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Play From Here", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Replay deterministic test", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Agent de test", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Simulation de combat", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Annuler le run", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("copie temporaire", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun acteur", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioPlaytestLabWorkbenchUx7G(StudioSessionController session)
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

        _dialog = StudioStyle.Panel("Ux7GPlaytestLab", StudioStyle.Surface, 12, 1);
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
        titles.AddChild(StudioStyle.Label("LABORATOIRE PLAYTEST · UX7-G", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label(
            "Aperçu · Play From Here · Replay · Agent de test · Combat",
            18, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Run annulable et déterministe · résultats navigables · source ouverte inchangée",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButton("Fermer", Close, 82));
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddThemeConstantOverride("separation", 8);
        var moduleLabel = StudioStyle.Label("Module à tester", 11, StudioStyle.Muted);
        moduleLabel.CustomMinimumSize = new Vector2(130, 0);
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

        var scopePanel = StudioStyle.Panel("Ux7GPlaytestScope", StudioStyle.SurfaceRaised, 8, 1);
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
        scopeColumn.AddThemeConstantOverride("separation", 6);
        scopeScroll.AddChild(scopeColumn);
        scopeColumn.AddChild(StudioStyle.Label("PÉRIMÈTRE DU RUN", 10, StudioStyle.Accent));
        _scope = StudioStyle.Label("Sélectionnez explicitement le module.", 10, StudioStyle.Muted, wrap: true);
        _scope.CustomMinimumSize = new Vector2(0, 42);
        scopeColumn.AddChild(_scope);
        scopeColumn.AddChild(StageCard("ORF-SHELL-018", "Aperçu et mode test",
            "Snapshot, manifeste de lancement, rechargement et suppression bornée."));
        scopeColumn.AddChild(StageCard("ORF-AREA-036", "Play From Here",
            "Zone d’entrée explicite, commande sèche, application, historique et round-trip."));
        scopeColumn.AddChild(StageCard("ORF-GAME-029", "Replay deterministic test",
            "Entrées, événements, seeds, hash d’état et divergence minimale reproductible."));
        scopeColumn.AddChild(StageCard("ORF-AI-025", "Agent de test",
            "Scénario déterministe, couverture dialogue/quête et actions gouvernées avec citations."));
        scopeColumn.AddChild(StageCard("ORF-AI-028", "Simulation de combat",
            "Matchups seedés et métriques; archétypes réels si disponibles, fallback isolé sinon."));
        var boundary = StudioStyle.Label(
            "Frontière honnête · toutes les écritures restent dans une copie temporaire. " +
            "Aucun acteur, moteur physique, rendu, socket, serveur ou combat live n’est exécuté.",
            10, StudioStyle.Warning, wrap: true);
        boundary.CustomMinimumSize = new Vector2(0, 54);
        scopeColumn.AddChild(boundary);

        var resultsPanel = StudioStyle.Panel("Ux7GPlaytestResults", StudioStyle.SurfaceRaised, 8, 1);
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
        resultsColumn.AddChild(StudioStyle.Label("RÉSULTATS NAVIGABLES", 10, StudioStyle.Accent));
        _results = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 154),
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
        _detailSummary = StudioStyle.Label("Lancez les cinq parcours isolés.", 10, StudioStyle.Muted, wrap: true);
        _detailBody = StudioStyle.Label("Sélectionnez un résultat après le run.", 10, StudioStyle.Text, wrap: true);
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
        _run = ActionButton("Lancer les 5 parcours", StartRun, 200, primary: true);
        _cancel = ActionButton("Annuler le run", CancelRun, 140);
        actions.AddChild(_run);
        actions.AddChild(_cancel);
        var note = StudioStyle.Label(
            "Chaque résultat distingue une simulation déterministe d’une exécution runtime réelle.",
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
        _moduleChoice.AddItem($"Tester · {HumanText(_session.Project?.Module.DisplayName ?? "Module indisponible")}");
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
        _report = _session.RunIsolatedPlaytestUx7G(_selection, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        _cancelledReport = _session.RunIsolatedPlaytestUx7G(_selection, cancellation.Token, stage =>
        {
            if (stage.Key == "preview-mode") cancellation.Cancel();
        });
        PopulateResults();
        if (_results.ItemCount > 0)
        {
            var index = _results.ItemCount - 1;
            _results.Select(index);
            SelectResult(index);
        }
        SetStatus(_report.Message, !_report.Succeeded);
        var ready = HasExplicitSelection && UsesIsolatedEvidence && FiveResultsReady &&
                    ResultNavigationReady && CancellationReady && ArtifactConfined &&
                    EvidenceSourceUntouched && EvidenceResourcesReleased && ShowsHumanSurface;
        if (!ready)
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX7_G_UI_FAILED " +
                        $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                        $"isolated={UsesIsolatedEvidence.ToString().ToLowerInvariant()} " +
                        $"results={FiveResultsReady.ToString().ToLowerInvariant()} " +
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
        if (!PrepareEvidence("ux7-g-playtest-lab")) return false;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < _results.ItemCount; index++)
        {
            _results.Select(index);
            SelectResult(index);
            if (_report is not null && index < _report.Stages.Count) visited.Add(_report.Stages[index].Key);
        }
        return visited.SetEquals(["preview-mode", "play-from-here", "deterministic-replay",
                   "test-agent", "combat-simulation"]) && ResultNavigationReady && SourceMatches();
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
        _selection = _explicitSelection ? _session.CurrentPlaytestLabUx7G() : null;
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
            _scope.Text = $"{HumanText(_selection.ModuleName)} · entrée {HumanText(_selection.EntryAreaName)} · " +
                          $"zones {_selection.AreaCount} · dialogues {_selection.DialogueCount} · " +
                          $"quêtes {_selection.QuestCount} · profils combat {_selection.CombatBlueprintCount}.";
            SetStatus("Module sélectionné · toutes les écritures resteront dans une copie temporaire.");
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
        _report = null;
        _cancelledReport = null;
        _results.Clear();
        ClearDetails();
        _cancellation = new CancellationTokenSource();
        var selection = _selection;
        var token = _cancellation.Token;
        _runTask = Task.Run(() => _session.RunIsolatedPlaytestUx7G(selection, token));
        SetProcess(true);
        SetStatus("Run isolé en cours · annulation possible entre chaque parcours.");
        UpdateButtons();
    }

    private void CancelRun()
    {
        if (_runTask is null || _cancellation is null) return;
        _cancellation.Cancel();
        SetStatus("Annulation demandée · la copie temporaire sera libérée.");
        UpdateButtons();
    }

    private void PopulateResults()
    {
        _results.Clear();
        if (_report is null) return;
        foreach (var stage in _report.Stages)
        {
            var verdict = stage.Passed ? "CONFORME" : stage.Completed ? "À EXAMINER" : "INCOMPLET";
            _results.AddItem($"{stage.Route} · {stage.Title} · {verdict} · {stage.Cases} cas");
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
        _detailVerdict.Text = stage.Passed ? "Conforme" : stage.Completed ? "À examiner" : "Incomplet";
        _detailVerdict.Modulate = stage.Passed ? StudioStyle.Success :
            stage.Completed ? StudioStyle.Warning : StudioStyle.Danger;
        _detailSummary.Text = $"{stage.Summary} · {stage.Cases} cas exercés";
        _detailBody.Text = stage.Details.Count == 0 ? "Aucun détail produit." :
            string.Join(System.Environment.NewLine + System.Environment.NewLine, stage.Details.Select(HumanText));
    }

    private void ClearDetails()
    {
        if (_detailTitle is null) return;
        _detailTitle.Text = "Aucun résultat sélectionné";
        _detailVerdict.Text = "Le verdict apparaîtra ici.";
        _detailVerdict.Modulate = StudioStyle.Muted;
        _detailSummary.Text = "Lancez les cinq parcours isolés.";
        _detailBody.Text = "Sélectionnez un résultat après le run.";
    }

    private void ResetControls()
    {
        _updating = true;
        _explicitSelection = false;
        _selection = null;
        _report = null;
        _cancelledReport = null;
        _sourceSnapshot = null;
        _results?.Clear();
        if (_scope is not null) _scope.Text = "Sélectionnez explicitement le module.";
        if (_status is not null) _status.Text = "Sélectionnez explicitement un module.";
        _updating = false;
        ClearDetails();
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

    private SourceSnapshotUx7G CaptureSource()
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
        return ReferenceEquals(snapshot.Project, _session.Project) && ReferenceEquals(snapshot.History, history) &&
               history?.State.Revision == snapshot.Revision &&
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

    private static PanelContainer StageCard(string route, string title, string description)
    {
        var panel = StudioStyle.Panel("Ux7GStageCard", StudioStyle.Surface, 6, 1);
        var margin = StudioStyle.Margin(8, 5, 8, 5);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 2);
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

    private sealed record SourceSnapshotUx7G(
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
