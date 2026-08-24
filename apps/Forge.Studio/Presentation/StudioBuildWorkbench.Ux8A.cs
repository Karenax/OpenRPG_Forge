using System.Text.RegularExpressions;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioBuildWorkbenchUx8A : ColorRect
{
    private readonly StudioSessionController _session;
    private SourceSnapshotUx8A? _sourceSnapshot;
    private StudioUx8ABuildSelection? _selection;
    private StudioUx8ABuildRunReport? _report;
    private StudioUx8ABuildRunReport? _cancelledReport;
    private CancellationTokenSource? _cancellation;
    private Task<StudioUx8ABuildRunReport>? _runTask;
    private bool _explicitSelection;
    private bool _logsVisible;
    private bool _updating;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private Label _scope = null!;
    private ItemList _results = null!;
    private Label _detailTitle = null!;
    private Label _detailVerdict = null!;
    private Label _detailSummary = null!;
    private Label _detailBody = null!;
    private TextEdit _logs = null!;
    private Label _status = null!;
    private Button _run = null!;
    private Button _cancel = null!;
    private Button _openOutput = null!;
    private Button _toggleLogs = null!;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _selection is not null;
    internal bool UsesRealCli => _report is { CliProcess: true, Succeeded: true };
    internal bool FiveResultsReady => _report is { Succeeded: true, Stages.Count: 5 } &&
                                      _report.Stages.All(stage => stage.Passed) && _results.ItemCount == 5;
    internal bool ResultNavigationReady => FiveResultsReady && _results.GetSelectedItems().Length == 1 &&
                                           !_detailBody.Text.Contains("Sélectionnez", StringComparison.Ordinal) &&
                                           !_openOutput.Disabled;
    internal bool CancellationReady => _cancelledReport is
    { Cancelled: true, CliProcess: true, TemporaryWorkspaceReleased: true };
    internal bool ArtifactConfined => _report is { ArtifactsConfined: true, ArtifactsRetained: true };
    internal bool EvidenceSourceUntouched => _report is { SourceUntouched: true } &&
                                             _cancelledReport is { SourceUntouched: true } && SourceMatches();
    internal bool EvidenceResourcesReleased => _session.LastUx8ATemporaryReleased;
    internal bool SourceSessionRestored => SourceMatches();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return Visible &&
                   text.Contains("Build du module", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Forge.Cli réel", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("copie temporaire", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("déterministe", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("incrémental", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Annuler", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("journaux", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("source ouverte inchangée", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioBuildWorkbenchUx8A(StudioSessionController session)
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

        _dialog = StudioStyle.Panel("Ux8ABuildWorkbench", StudioStyle.Surface, 12, 1);
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
        titles.AddChild(StudioStyle.Label("BUILD & IMPACT · UX8-A", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Build du module · Forge.Cli réel · résultat explicable",
            18, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Deux builds dans une copie temporaire · déterministe · incrémental · source ouverte inchangée",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButton("Fermer", Close, 82));
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddThemeConstantOverride("separation", 8);
        var moduleLabel = StudioStyle.Label("Module à construire", 11, StudioStyle.Muted);
        moduleLabel.CustomMinimumSize = new Vector2(150, 0);
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

        var scopePanel = StudioStyle.Panel("Ux8ABuildScope", StudioStyle.SurfaceRaised, 8, 1);
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
        scopeColumn.AddChild(StudioStyle.Label("PLAN DU BUILD", 10, StudioStyle.Accent));
        _scope = StudioStyle.Label("Sélectionnez explicitement le module.", 10, StudioStyle.Muted, wrap: true);
        _scope.CustomMinimumSize = new Vector2(0, 52);
        scopeColumn.AddChild(_scope);
        scopeColumn.AddChild(RouteCard("ORF-SHELL-019", "Build du module",
            "Produit un vrai paquet .orpmod avec Forge.Cli, jamais dans le workspace source."));
        scopeColumn.AddChild(RouteCard("ORF-EE-004", "Optimisation Build Module",
            "Matérialisation parallèle bornée, graphe de dépendances et ordre stable mesurés."));
        scopeColumn.AddChild(RouteCard("ORF-BUILD-003", "Build CLI",
            "Processus enfant dotnet/Forge.Cli réel, sortie JSON structurée et journaux à la demande."));
        scopeColumn.AddChild(RouteCard("ORF-BUILD-004", "Build déterministe",
            "Deux paquets comparés octet par octet, plus la vérification reproductible du CLI."));
        scopeColumn.AddChild(RouteCard("ORF-BUILD-005", "Build incrémental",
            "Premier passage reconstruit; le second réutilise le graphe persistant sans rebuild inutile."));
        var boundary = StudioStyle.Label(
            "Frontière honnête · la sortie est un paquet de développement non signé dans un isolat jetable. " +
            "Ce lot ne publie rien, ne signe rien et ne modifie ni le module ouvert ni Git.",
            10, StudioStyle.Warning, wrap: true);
        boundary.CustomMinimumSize = new Vector2(0, 64);
        scopeColumn.AddChild(boundary);

        var resultsPanel = StudioStyle.Panel("Ux8ABuildResults", StudioStyle.SurfaceRaised, 8, 1);
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
            CustomMinimumSize = new Vector2(0, 146),
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
        detail.AddThemeConstantOverride("separation", 6);
        detailScroll.AddChild(detail);
        _detailTitle = StudioStyle.Label("Aucun résultat sélectionné", 15, StudioStyle.Text, wrap: true);
        _detailVerdict = StudioStyle.Label("Le verdict apparaîtra ici.", 11, StudioStyle.Muted, wrap: true);
        _detailSummary = StudioStyle.Label("Lancez le build réel.", 10, StudioStyle.Muted, wrap: true);
        _detailBody = StudioStyle.Label("Sélectionnez un résultat après le build.", 10, StudioStyle.Text,
            wrap: true);
        _logs = new TextEdit
        {
            Editable = false,
            Visible = false,
            CustomMinimumSize = new Vector2(0, 118),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        detail.AddChild(_detailTitle);
        detail.AddChild(_detailVerdict);
        detail.AddChild(_detailSummary);
        detail.AddChild(new HSeparator());
        detail.AddChild(_detailBody);
        detail.AddChild(_logs);

        _status = StudioStyle.Label("Sélectionnez explicitement un module.", 10, StudioStyle.Muted, wrap: true);
        _status.CustomMinimumSize = new Vector2(0, 30);
        root.AddChild(_status);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 7);
        _run = ActionButton("Construire le module", StartRun, 176, primary: true);
        _cancel = ActionButton("Annuler", CancelRun, 96);
        _openOutput = ActionButton("Ouvrir la sortie", OpenOutput, 126);
        _toggleLogs = ActionButton("Afficher les journaux", ToggleLogs, 162);
        actions.AddChild(_run);
        actions.AddChild(_cancel);
        actions.AddChild(_openOutput);
        actions.AddChild(_toggleLogs);
        var note = StudioStyle.Label(
            "La sortie reste disponible seulement tant que cette surface est ouverte.",
            9, StudioStyle.Muted, wrap: true);
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
        ResetControls(releaseArtifacts: true);
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Choisir explicitement le module ouvert…");
        _moduleChoice.AddItem($"Construire · {HumanText(_session.Project?.Module.DisplayName ?? "Module indisponible")}");
        _moduleChoice.Select(0);
        Visible = true;
        UpdateBounds();
    }

    internal void Close()
    {
        if (!Visible && _runTask is null && _report is null) return;
        CancelRun();
        Visible = false;
        if (_runTask is null) ResetControls(releaseArtifacts: true);
    }

    internal void CloseEvidence()
    {
        Close();
        if (_runTask is null) ResetControls(releaseArtifacts: true);
    }

    internal void RestoreSourceSurfaceAfterSmoke() => Open();

    internal void HideSourceSurfaceAfterSmoke()
    {
        Visible = false;
        if (_runTask is null) ResetControls(releaseArtifacts: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        if (Visible && _report is { Succeeded: true, ArtifactsRetained: true } &&
            CancellationReady && SourceMatches())
        {
            PopulateResults();
            SelectEvidenceResult();
            return EvidenceReady();
        }
        Open();
        _moduleChoice.Select(1);
        SelectModule(1);
        if (_selection is null) return false;
        _sourceSnapshot = CaptureSource();
        using (var cancellation = new CancellationTokenSource())
            _cancelledReport = _session.RunBuildUx8A(_selection, cancellation.Token,
                retainArtifacts: false, cancellation.Cancel);
        _report = _session.RunBuildUx8A(_selection, CancellationToken.None, retainArtifacts: true);
        PopulateResults();
        SelectEvidenceResult();
        if (!_logsVisible) ToggleLogs();
        SetStatus(_report.Message, !_report.Succeeded);
        UpdateButtons();
        var ready = EvidenceReady();
        if (!ready)
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX8_A_UI_FAILED " +
                        $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                        $"cli={UsesRealCli.ToString().ToLowerInvariant()} " +
                        $"results={FiveResultsReady.ToString().ToLowerInvariant()} " +
                        $"navigation={ResultNavigationReady.ToString().ToLowerInvariant()} " +
                        $"cancel={CancellationReady.ToString().ToLowerInvariant()} " +
                        $"artifact={ArtifactConfined.ToString().ToLowerInvariant()} " +
                        $"source={EvidenceSourceUntouched.ToString().ToLowerInvariant()} " +
                        $"human={ShowsHumanSurface.ToString().ToLowerInvariant()}");
        return ready;
    }

    internal bool RunUiNavigationEvidence()
    {
        if (!PrepareEvidence("ux8-a-build-foundation")) return false;
        for (var index = 0; index < _results.ItemCount; index++)
        {
            _results.Select(index);
            SelectResult(index);
            if (_detailBody.Text.Contains("Sélectionnez", StringComparison.Ordinal)) return false;
        }
        SelectEvidenceResult();
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
            SelectEvidenceResult();
            SetStatus(_report.Message, !_report.Succeeded && !_report.Cancelled);
        }
        catch (Exception exception)
        {
            SetStatus("Build interrompu · " + exception.Message, true);
        }
        finally
        {
            _runTask = null;
            _cancellation?.Dispose();
            _cancellation = null;
            SetProcess(false);
            UpdateButtons();
            if (!Visible) ResetControls(releaseArtifacts: true);
        }
    }

    public override void _ExitTree()
    {
        _cancellation?.Cancel();
        _session.ReleaseBuildArtifactsUx8A();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBounds();
    }

    private void SelectModule(int index)
    {
        if (_updating) return;
        _session.ReleaseBuildArtifactsUx8A();
        _explicitSelection = index == 1;
        _selection = _explicitSelection ? _session.CurrentBuildUx8A() : null;
        _report = null;
        _cancelledReport = null;
        _results.Clear();
        ClearDetails();
        if (_selection is null)
        {
            _scope.Text = "Sélectionnez explicitement le module pour analyser son build.";
            SetStatus("Aucun module sélectionné.");
        }
        else
        {
            _scope.Text = $"{HumanText(_selection.ModuleName)} · {_selection.ResourceCount} ressources · " +
                          $"{_selection.DependencyCount} dépendances · {_selection.MaterializationTasks} tâches · " +
                          $"plan courant {_selection.PlannedRebuild} reconstruit / " +
                          $"{_selection.PlannedReuse} réutilisé · {_selection.CommandPreview}.";
            var ready = _selection.ProfileReady && _selection.CliReady;
            SetStatus(ready
                ? "Module sélectionné · le build s’exécutera uniquement dans une copie temporaire."
                : "Prérequis manquant · profil déterministe ou Forge.Cli compilé indisponible.", !ready);
        }
        UpdateButtons();
    }

    private void StartRun()
    {
        if (_runTask is not null || _selection is null || !_selection.ProfileReady || !_selection.CliReady)
        {
            SetStatus("Sélectionnez le module et vérifiez Forge.Cli ainsi que le profil déterministe.", true);
            return;
        }
        _session.ReleaseBuildArtifactsUx8A();
        _sourceSnapshot = CaptureSource();
        _report = null;
        _cancelledReport = null;
        _results.Clear();
        ClearDetails();
        _cancellation = new CancellationTokenSource();
        var selection = _selection;
        var token = _cancellation.Token;
        _runTask = Task.Run(() => _session.RunBuildUx8A(selection, token, retainArtifacts: true));
        SetProcess(true);
        SetStatus("Forge.Cli réel en cours · déterminisme et second passage incrémental · annulation disponible.");
        UpdateButtons();
    }

    private void CancelRun()
    {
        if (_runTask is null || _cancellation is null) return;
        _cancellation.Cancel();
        SetStatus("Annulation demandée · le processus CLI et l’isolat seront libérés.");
        UpdateButtons();
    }

    private void OpenOutput()
    {
        if (_report is not { ArtifactsRetained: true } || !Directory.Exists(_report.OutputDirectory))
        {
            SetStatus("La sortie temporaire n’est plus disponible.", true);
            return;
        }
        var uri = "file:///" + _report.OutputDirectory.Replace('\\', '/');
        var result = OS.ShellOpen(uri);
        SetStatus(result == Error.Ok
            ? "Dossier de sortie temporaire ouvert."
            : "Le dossier de sortie n’a pas pu être ouvert.", result != Error.Ok);
    }

    private void ToggleLogs()
    {
        _logsVisible = !_logsVisible;
        _logs.Visible = _logsVisible;
        _toggleLogs.Text = _logsVisible ? "Masquer les journaux" : "Afficher les journaux";
    }

    private void PopulateResults()
    {
        _results.Clear();
        if (_report is null) return;
        foreach (var stage in _report.Stages)
        {
            var verdict = stage.Passed ? "CONFORME" : stage.Completed ? "À EXAMINER" : "INCOMPLET";
            _results.AddItem($"{stage.Route} · {stage.Title} · {verdict} · " +
                             $"{stage.Rebuilt} reconstruit · {stage.Reused} réutilisé");
        }
    }

    private void SelectEvidenceResult()
    {
        if (_results.ItemCount == 0) return;
        var index = Math.Min(4, _results.ItemCount - 1);
        _results.Select(index);
        SelectResult(index);
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
        _detailVerdict.Text = stage.Passed ? "Conforme · exécution réelle" : "À examiner";
        _detailVerdict.Modulate = stage.Passed ? StudioStyle.Success : StudioStyle.Warning;
        _detailSummary.Text = HumanText(stage.Summary);
        _detailBody.Text = string.Join('\n', stage.Details.Select(detail => "• " + HumanText(detail)));
        _logs.Text = HumanText(stage.Log);
    }

    private void ClearDetails()
    {
        _detailTitle.Text = "Aucun résultat sélectionné";
        _detailVerdict.Text = "Le verdict apparaîtra ici.";
        _detailVerdict.Modulate = StudioStyle.Muted;
        _detailSummary.Text = "Lancez le build réel.";
        _detailBody.Text = "Sélectionnez un résultat après le build.";
        _logs.Text = string.Empty;
    }

    private void ResetControls(bool releaseArtifacts)
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
        _logsVisible = false;
        _logs.Visible = false;
        _toggleLogs.Text = "Afficher les journaux";
        _updating = false;
        if (releaseArtifacts) _session.CloseUx8A();
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var running = _runTask is not null;
        _run.Disabled = running || _selection is null || !_selection.ProfileReady || !_selection.CliReady;
        _cancel.Disabled = !running;
        _moduleChoice.Disabled = running;
        _openOutput.Disabled = running || _report is not { ArtifactsRetained: true } ||
                               !Directory.Exists(_report.OutputDirectory);
        _toggleLogs.Disabled = _report?.Stages.Count == 0;
    }

    private bool EvidenceReady() => HasExplicitSelection && UsesRealCli && FiveResultsReady &&
                                    ResultNavigationReady && CancellationReady && ArtifactConfined &&
                                    EvidenceSourceUntouched && ShowsHumanSurface;

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = HumanText(message);
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(_status.Text, error);
    }

    private SourceSnapshotUx8A CaptureSource() => new(
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
        var panel = StudioStyle.Panel("Ux8ARouteCard", StudioStyle.Surface, 6, 1);
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

    private sealed record SourceSnapshotUx8A(
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
