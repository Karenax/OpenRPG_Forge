using System.Text.RegularExpressions;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioHistoryRecoveryWorkbenchUx7F : ColorRect
{
    private readonly StudioSessionController _session;
    private SourceSnapshotUx7F? _sourceSnapshot;
    private StudioUx7FHistorySelection? _selection;
    private StudioUx7FHistoryRunReport? _runReport;
    private StudioUx7FHistoryRunReport? _cancellationReport;
    private CancellationTokenSource? _runCancellation;
    private Task<StudioUx7FHistoryRunReport>? _runTask;
    private bool _updating;
    private bool _explicitSelection;
    private bool _lastCancellation;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private Label _scope = null!;
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
    internal bool ArtifactConfined => _runReport is { ArtifactsConfined: true };
    internal bool EvidenceSourceUntouched => _runReport is { SourceUntouched: true } &&
                                             _cancellationReport is { SourceUntouched: true } && SourceMatches();
    internal bool EvidenceResourcesReleased => _runReport is { TemporaryWorkspaceReleased: true } &&
                                                _cancellationReport is { TemporaryWorkspaceReleased: true } &&
                                                _session.LastUx7FTemporaryReleased;
    internal bool SourceSessionRestored => SourceMatches();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx7F(this));
            return Visible &&
                   text.Contains("Sauvegarde automatique et récupération", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Sauvegarde et chargement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Sauvegarde joueur/partie/serveur", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Checkpoints et respawn", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Crash recovery", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Annuler le run", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("copie temporaire", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun cloud", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx7F(text);
        }
    }

    internal StudioHistoryRecoveryWorkbenchUx7F(StudioSessionController session)
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

        _dialog = StudioStyle.Panel("Ux7FHistoryRecovery", StudioStyle.Surface, 12, 1);
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
        titles.AddChild(StudioStyle.Label("HISTORIQUE ET RÉCUPÉRATION · UX7-F", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label(
            "Autosaves · Archives · Portées · Checkpoints · Crash recovery",
            18, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Run annulable et isolé · résultats navigables · source ouverte strictement inchangée",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButtonUx7F("Fermer", Close, 82));
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddThemeConstantOverride("separation", 8);
        var moduleLabel = StudioStyle.Label("Module à inspecter", 11, StudioStyle.Muted);
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

        var scopePanel = StudioStyle.Panel("Ux7FHistoryScope", StudioStyle.SurfaceRaised, 8, 1);
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
        scopeColumn.AddChild(StageCardUx7F("ORF-SHELL-024", "Sauvegarde automatique et récupération",
            "Session interrompue, snapshot versionné, comparaison, restauration sélective et historique."));
        scopeColumn.AddChild(StageCardUx7F("ORF-GAME-020", "Sauvegarde et chargement",
            "Archive locale, révisions, rollback, intégrité de contenu et cadence d’autosave."));
        scopeColumn.AddChild(StageCardUx7F("ORF-GAME-021", "Sauvegarde joueur/partie/serveur",
            "Portées Player, Game et Server, autorité, confidentialité et conflits déterministes."));
        scopeColumn.AddChild(StageCardUx7F("ORF-GAME-025", "Checkpoints et respawn",
            "Checkpoint, coût, groupe, cadavre, hooks et refus d’autorité ou de zone dangereuse."));
        scopeColumn.AddChild(StageCardUx7F("ORF-QA-028", "Crash recovery",
            "Snapshot sain et corrompu, quarantaine, journal réparé et conservation des versions."));
        var boundary = StudioStyle.Label(
            "Frontière honnête · toutes les écritures sont confinées à une copie temporaire. " +
            "Aucun cloud, serveur, socket, acteur ou monde moteur n’est restauré ni déplacé.",
            10, StudioStyle.Warning, wrap: true);
        boundary.CustomMinimumSize = new Vector2(0, 54);
        scopeColumn.AddChild(boundary);

        var resultsPanel = StudioStyle.Panel("Ux7FHistoryResults", StudioStyle.SurfaceRaised, 8, 1);
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
        var details = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        details.AddThemeConstantOverride("separation", 7);
        detailScroll.AddChild(details);
        _resultTitle = StudioStyle.Label("Aucun résultat sélectionné", 15, StudioStyle.Text, wrap: true);
        _resultVerdict = StudioStyle.Label("Le verdict apparaîtra ici.", 11, StudioStyle.Muted, wrap: true);
        _resultSummary = StudioStyle.Label("Lancez les cinq analyses isolées.", 10,
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
        _run = ActionButtonUx7F("Lancer les 5 analyses", StartRun, 200, primary: true);
        _cancel = ActionButtonUx7F("Annuler le run", CancelRun, 140);
        actions.AddChild(_run);
        actions.AddChild(_cancel);
        var note = StudioStyle.Label(
            "Chaque résultat distingue la preuve locale d’une restauration runtime réellement exécutée.",
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
        _moduleChoice.AddItem($"Inspecter · {HumanTextUx7F(_session.Project?.Module.DisplayName ?? "Module indisponible")}");
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
        _runReport = _session.RunIsolatedHistoryUx7F(_selection, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        _cancellationReport = _session.RunIsolatedHistoryUx7F(_selection, cancellation.Token, stage =>
        {
            if (stage.Key == "autosave-recovery") cancellation.Cancel();
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
                    ResultNavigationReady && CancellationReady && ArtifactConfined &&
                    EvidenceSourceUntouched && EvidenceResourcesReleased && ShowsHumanSurface;
        if (!ready)
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX7_F_UI_FAILED " +
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
        if (!PrepareEvidence("ux7-f-history-recovery")) return false;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < _results.ItemCount; index++)
        {
            _results.Select(index);
            SelectResult(index);
            if (_runReport is not null && index < _runReport.Stages.Count)
                visited.Add(_runReport.Stages[index].Key);
        }
        return visited.SetEquals(["autosave-recovery", "save-load", "save-scopes", "checkpoint-respawn",
                   "crash-recovery"]) && ResultNavigationReady && SourceMatches();
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
        _selection = _explicitSelection ? _session.CurrentHistoryLabUx7F() : null;
        _runReport = null;
        _cancellationReport = null;
        _lastCancellation = false;
        _results.Clear();
        ClearResultDetails();
        if (_selection is null)
        {
            _scope.Text = "Sélectionnez explicitement le module pour préparer le run.";
            SetStatus("Aucun module sélectionné.");
        }
        else
        {
            _scope.Text =
                $"{HumanTextUx7F(_selection.ModuleName)} · autosave {_selection.AutosaveIntervalSeconds}s · " +
                $"rétention {_selection.RetainedRecoverySnapshots} · rollback {_selection.SaveRollbackSlots} · " +
                $"portée {_selection.PersistenceScope} · checkpoints {_selection.CheckpointCount}.";
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
        _runReport = null;
        _cancellationReport = null;
        _lastCancellation = false;
        _results.Clear();
        ClearResultDetails();
        _runCancellation = new CancellationTokenSource();
        var selection = _selection;
        var token = _runCancellation.Token;
        _runTask = Task.Run(() => _session.RunIsolatedHistoryUx7F(selection, token));
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
        _resultSummary.Text = $"{stage.Summary} · {stage.Cases} cas exercés";
        _resultDetails.Text = stage.Details.Count == 0
            ? "Aucun détail produit."
            : string.Join(System.Environment.NewLine + System.Environment.NewLine,
                stage.Details.Select(HumanTextUx7F));
    }

    private void ClearResultDetails()
    {
        if (_resultTitle is null) return;
        _resultTitle.Text = "Aucun résultat sélectionné";
        _resultVerdict.Text = "Le verdict apparaîtra ici.";
        _resultVerdict.Modulate = StudioStyle.Muted;
        _resultSummary.Text = "Lancez les cinq analyses isolées.";
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
        if (_scope is not null) _scope.Text = "Sélectionnez explicitement le module.";
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

    private SourceSnapshotUx7F CaptureSource()
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

    private static PanelContainer StageCardUx7F(string route, string title, string description)
    {
        var panel = StudioStyle.Panel("Ux7FStageCard", StudioStyle.Surface, 6, 1);
        var margin = StudioStyle.Margin(8, 5, 8, 5);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 2);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label($"{route} · {title}", 11, StudioStyle.Text, wrap: true));
        column.AddChild(StudioStyle.Label(description, 9, StudioStyle.Muted, wrap: true));
        return panel;
    }

    private static Button ActionButtonUx7F(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static string HumanTextUx7F(string value) => Regex.Replace(value.Replace('\\', '/'),
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        "élément protégé", RegexOptions.CultureInvariant);

    private static bool ContainsUuidUx7F(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    private static IEnumerable<string> DescendantTextUx7F(Node root)
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
            foreach (var text in DescendantTextUx7F(child)) yield return text;
        }
    }

    private sealed record SourceSnapshotUx7F(
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
