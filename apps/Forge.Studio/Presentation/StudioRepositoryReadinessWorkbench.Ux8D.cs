using System.Text.RegularExpressions;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioRepositoryReadinessWorkbenchUx8D : ColorRect
{
    private readonly StudioSessionController _session;
    private CancellationTokenSource? _cancellation;
    private Task<StudioUx8DReport>? _runTask;
    private StudioUx8DSelection? _selection;
    private StudioUx8DReport? _report;
    private bool _explicitSelection;
    private bool _updating;
    private bool _logsVisible;
    private bool _cancellationReady;
    private bool _navigationReady;

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
    private Button _toggleLogs = null!;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _selection is not null;
    internal bool FourResultsReady => _report is { Succeeded: true, Cancelled: false, Stages.Count: 4 };
    internal bool CancellationReady => _cancellationReady;
    internal bool NavigationReady => _navigationReady;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx8D(this));
            return Visible &&
                   text.Contains("Dépôt Git canonique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Structure de projet standard", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Versioning sémantique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Décisions d’architecture", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Annuler l’inspection", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Afficher le journal", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucune commande Git", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx8D(text);
        }
    }

    internal StudioRepositoryReadinessWorkbenchUx8D(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx8D();
    }

    private void BuildUiUx8D()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.012f, 0.018f, 0.03f, 0.95f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetProcess(true);

        _dialog = StudioStyle.Panel("Ux8DRepositoryReadinessWorkbench", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(15, 12, 15, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("PRÉPARATION PRODUIT · UX8-D", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Dépôt, structure, versions et décisions", 18, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Inspection locale annulable · journal à la demande · aucune commande Git ni écriture",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButtonUx8D("Fermer", Close, 82));
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddThemeConstantOverride("separation", 8);
        var moduleLabel = StudioStyle.Label("Module et dépôt", 11, StudioStyle.Muted);
        moduleLabel.CustomMinimumSize = new Vector2(150, 0);
        selection.AddChild(moduleLabel);
        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_moduleChoice);
        _moduleChoice.ItemSelected += index => SelectModuleUx8D((int)index);
        selection.AddChild(_moduleChoice);
        root.AddChild(selection);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 620),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.Fill,
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        var body = new HSplitContainer { SplitOffsets = [475] };
        bodyHost.AddChild(body);
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildRouteColumnUx8D(body);
        BuildResultColumnUx8D(body);

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
        _run = ActionButtonUx8D("Inspecter le dépôt", StartRunUx8D, 150, primary: true);
        _cancel = ActionButtonUx8D("Annuler l’inspection", CancelRunUx8D, 155);
        _toggleLogs = ActionButtonUx8D("Afficher le journal", ToggleLogsUx8D, 145);
        actions.AddChild(_run);
        actions.AddChild(_cancel);
        actions.AddChild(_toggleLogs);
        root.AddChild(actions);

        _status = StudioStyle.Label("Sélectionnez explicitement le module.", 10, StudioStyle.Muted, wrap: true);
        _status.CustomMinimumSize = new Vector2(0, 28);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "Frontière honnête · état local seulement : aucune commande Git, aucun push, aucun générateur et aucune ADR ne sont écrits.",
            9, StudioStyle.Warning, wrap: true));
        UpdateButtonsUx8D();
    }

    private void BuildRouteColumnUx8D(HSplitContainer body)
    {
        var panel = StudioStyle.Panel("Ux8DRoutes", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(430, 0);
        body.AddChild(panel);
        var margin = StudioStyle.Margin(10, 8, 10, 8);
        panel.AddChild(margin);
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        margin.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(column);
        column.AddChild(StudioStyle.Label("QUATRE ÉTATS PROUVÉS", 10, StudioStyle.Accent));
        _scope = StudioStyle.Label("Sélectionnez explicitement le module.", 10, StudioStyle.Muted, wrap: true);
        _scope.CustomMinimumSize = new Vector2(0, 52);
        column.AddChild(_scope);
        column.AddChild(RouteCardUx8D("ORF-BUILD-001", "Dépôt Git canonique",
            "Profil, dépôt, fichier d’exclusion et guide inspectés sans lancer Git."));
        column.AddChild(RouteCardUx8D("ORF-BUILD-002", "Structure de projet standard",
            "Huit emplacements configurés sont distingués des dossiers réellement matérialisés."));
        column.AddChild(RouteCardUx8D("ORF-BUILD-018", "Versioning sémantique",
            "Core, kits, package, schémas, règles de compatibilité et changelog restent séparés."));
        column.AddChild(RouteCardUx8D("ORF-BUILD-019", "Décisions d’architecture",
            "Dossier, modèle, statuts et preuves de tests/benchmarks sont vérifiés en lecture seule."));
        column.AddChild(StudioStyle.Label(
            "Un état absent reste affiché comme absent : cette surface ne crée aucun dossier et ne maquille aucune preuve.",
            10, StudioStyle.Warning, wrap: true));
    }

    private void BuildResultColumnUx8D(HSplitContainer body)
    {
        var panel = StudioStyle.Panel("Ux8DResults", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(650, 0);
        body.AddChild(panel);
        var margin = StudioStyle.Margin(10, 8, 10, 8);
        panel.AddChild(margin);
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        column.AddThemeConstantOverride("separation", 6);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label("RÉSULTATS NAVIGABLES", 10, StudioStyle.Accent));
        _results = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 128),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _results.ItemSelected += index => SelectResultUx8D((int)index);
        column.AddChild(_results);
        _detailTitle = StudioStyle.Label("Aucun résultat sélectionné", 13, StudioStyle.Text, wrap: true);
        _detailVerdict = StudioStyle.Label("Lancez l’inspection locale.", 11, StudioStyle.Muted, wrap: true);
        _detailSummary = StudioStyle.Label("Les preuves apparaîtront ici.", 10, StudioStyle.Muted, wrap: true);
        _detailBody = StudioStyle.Label("Aucun détail.", 10, StudioStyle.Text, wrap: true);
        _detailBody.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        column.AddChild(_detailTitle);
        column.AddChild(_detailVerdict);
        column.AddChild(_detailSummary);
        var detailsScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 150),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var detailsColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detailsColumn.AddChild(_detailBody);
        detailsScroll.AddChild(detailsColumn);
        column.AddChild(detailsScroll);
        _logs = new TextEdit
        {
            Editable = false,
            Visible = false,
            CustomMinimumSize = new Vector2(0, 108),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        column.AddChild(_logs);
    }

    internal void Open()
    {
        CancelRunUx8D();
        ResetUx8D();
        PopulateModuleChoiceUx8D();
        Visible = true;
        UpdateBoundsUx8D();
    }

    internal void Close()
    {
        if (!Visible && _runTask is null) return;
        Visible = false;
        CancelRunUx8D();
        if (_runTask is null) ResetUx8D();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        if (_session.Project is null) return false;
        if (!Visible) Open();
        _moduleChoice.Select(1);
        SelectModuleUx8D(1);
        if (_selection is null) return false;
        _report = _session.RunRepositoryReadinessUx8D(_selection, CancellationToken.None);
        PopulateResultsUx8D();
        SelectResultUx8D(2);
        UpdateBoundsUx8D();
        SetStatusUx8D(_report.Message, !_report.Succeeded);
        UpdateButtonsUx8D();
        return HasExplicitSelection && FourResultsReady && ShowsHumanSurface;
    }

    internal bool RunUiEvidence()
    {
        if (!PrepareEvidence("ux8-d-repository-readiness") || _selection is null) return false;
        _navigationReady = true;
        for (var index = 0; index < 4; index++)
        {
            _results.Select(index);
            SelectResultUx8D(index);
            _navigationReady &= _detailTitle.Text.Contains("ORF-BUILD-", StringComparison.Ordinal) &&
                                _detailBody.Text.Contains('•');
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = _session.RunRepositoryReadinessUx8D(_selection, cancellation.Token);
        _cancellationReady = cancelled.Succeeded && cancelled.Cancelled && cancelled.Stages.Count == 0;
        ToggleLogsUx8D();
        var logsReady = _logs.Visible && !string.IsNullOrWhiteSpace(_logs.Text);
        ToggleLogsUx8D();
        SelectResultUx8D(2);
        return FourResultsReady && _navigationReady && _cancellationReady && logsReady && ShowsHumanSurface;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        ResetUx8D();
        PopulateModuleChoiceUx8D();
        Visible = true;
        UpdateBoundsUx8D();
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        Visible = false;
        ResetUx8D();
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (_runTask is null || !_runTask.IsCompleted) return;
        try
        {
            _report = _runTask.GetAwaiter().GetResult();
            if (_report.Cancelled) _cancellationReady = true;
            PopulateResultsUx8D();
            if (_report.Stages.Count > 0) SelectResultUx8D(Math.Min(2, _report.Stages.Count - 1));
            SetStatusUx8D(_report.Message, !_report.Succeeded);
        }
        catch (Exception exception)
        {
            SetStatusUx8D("Inspection interrompue · " + HumanTextUx8D(exception.Message), true);
        }
        finally
        {
            _runTask = null;
            _cancellation?.Dispose();
            _cancellation = null;
            UpdateButtonsUx8D();
            if (!Visible) ResetUx8D();
        }
    }

    public override void _ExitTree() => CancelRunUx8D();

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBoundsUx8D();
    }

    private void PopulateModuleChoiceUx8D()
    {
        _updating = true;
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Choisir explicitement le module ouvert…");
        _moduleChoice.AddItem("Inspecter · " + HumanTextUx8D(_session.Project?.Module.DisplayName ??
                                                               "Module indisponible"));
        _moduleChoice.Select(0);
        _updating = false;
    }

    private void SelectModuleUx8D(int index)
    {
        if (_updating || _runTask is not null) return;
        _explicitSelection = index == 1;
        _selection = _explicitSelection ? _session.CurrentRepositoryReadinessUx8D() : null;
        _report = null;
        _results.Clear();
        ClearDetailsUx8D();
        if (_selection is null)
        {
            _scope.Text = "Sélectionnez explicitement le module pour inspecter son dépôt.";
            SetStatusUx8D("Aucun module sélectionné.");
        }
        else
        {
            _scope.Text = $"{HumanTextUx8D(_selection.ModuleName)} · " +
                          $"{HumanTextUx8D(_selection.RepositoryName)} · profil {HumanTextUx8D(_selection.ProfileProvenance)}.";
            SetStatusUx8D(_selection.RepositoryAvailable
                ? "Module sélectionné · inspection locale disponible, sans écriture."
                : "Module sélectionné · profil lisible, dépôt source indisponible.");
        }
        UpdateButtonsUx8D();
    }

    private void StartRunUx8D()
    {
        if (_runTask is not null || _selection is null)
        {
            SetStatusUx8D("Sélectionnez explicitement le module avant l’inspection.", true);
            return;
        }
        _report = null;
        _results.Clear();
        ClearDetailsUx8D();
        _cancellation = new CancellationTokenSource();
        var selection = _selection;
        var token = _cancellation.Token;
        _runTask = Task.Run(() => _session.RunRepositoryReadinessUx8D(selection, token));
        SetStatusUx8D("Inspection locale en cours · annulation disponible · aucune commande Git exécutée.");
        UpdateButtonsUx8D();
    }

    private void CancelRunUx8D()
    {
        if (_runTask is null || _cancellation is null) return;
        _cancellation.Cancel();
        SetStatusUx8D("Annulation demandée · aucune écriture n’est en attente.");
        UpdateButtonsUx8D();
    }

    private void ToggleLogsUx8D()
    {
        _logsVisible = !_logsVisible;
        _logs.Visible = _logsVisible;
        _toggleLogs.Text = _logsVisible ? "Masquer le journal" : "Afficher le journal";
    }

    private void PopulateResultsUx8D()
    {
        _results.Clear();
        if (_report is null) return;
        foreach (var stage in _report.Stages)
            _results.AddItem($"{stage.Route} · {stage.Title} · {stage.Verdict}");
    }

    private void SelectResultUx8D(int index)
    {
        if (_report is null || index < 0 || index >= _report.Stages.Count)
        {
            ClearDetailsUx8D();
            return;
        }
        _results.Select(index);
        var stage = _report.Stages[index];
        _detailTitle.Text = $"{stage.Route} · {stage.Title}";
        _detailVerdict.Text = stage.Verdict;
        _detailVerdict.Modulate = stage.Configured ? StudioStyle.Success : StudioStyle.Warning;
        _detailSummary.Text = HumanTextUx8D(stage.Summary);
        _detailBody.Text = string.Join('\n', stage.Details.Select(detail => "• " + HumanTextUx8D(detail)));
        _logs.Text = string.Join('\n', stage.Log.Select(line => HumanTextUx8D(line)));
    }

    private void ClearDetailsUx8D()
    {
        _detailTitle.Text = "Aucun résultat sélectionné";
        _detailVerdict.Text = "Lancez l’inspection locale.";
        _detailVerdict.Modulate = StudioStyle.Muted;
        _detailSummary.Text = "Les preuves apparaîtront ici.";
        _detailBody.Text = "Aucun détail.";
        _logs.Text = string.Empty;
    }

    private void ResetUx8D()
    {
        _explicitSelection = false;
        _selection = null;
        _report = null;
        _cancellationReady = false;
        _navigationReady = false;
        _results.Clear();
        _scope.Text = "Sélectionnez explicitement le module.";
        ClearDetailsUx8D();
        _logsVisible = false;
        _logs.Visible = false;
        _toggleLogs.Text = "Afficher le journal";
        _status.Text = "Sélectionnez explicitement le module.";
        _status.Modulate = StudioStyle.Muted;
        UpdateButtonsUx8D();
    }

    private void UpdateButtonsUx8D()
    {
        var running = _runTask is not null;
        _run.Disabled = running || !HasExplicitSelection;
        _cancel.Disabled = !running;
        _moduleChoice.Disabled = running;
        _toggleLogs.Disabled = _report?.Stages.Count == 0;
    }

    private void SetStatusUx8D(string message, bool error = false)
    {
        _status.Text = HumanTextUx8D(message);
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(_status.Text, error);
    }

    private void UpdateBoundsUx8D()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 54, 1180, 1840);
        var height = Math.Clamp(viewport.Y - 40, 780, 1040);
        _dialog.Position = new Vector2((viewport.X - width) / 2f, (viewport.Y - height) / 2f);
        _dialog.Size = new Vector2(width, height);
    }

    private static PanelContainer RouteCardUx8D(string route, string title, string description)
    {
        var panel = StudioStyle.Panel("Ux8DRouteCard", StudioStyle.Surface, 6, 1);
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 3);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label($"{route} · {title}", 11, StudioStyle.Text, wrap: true));
        column.AddChild(StudioStyle.Label(description, 9, StudioStyle.Muted, wrap: true));
        return panel;
    }

    private static Button ActionButtonUx8D(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static string HumanTextUx8D(string value) => Regex.Replace(value.Replace('\\', '/'),
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        "élément protégé", RegexOptions.CultureInvariant);

    private static bool ContainsUuidUx8D(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b");

    private static IEnumerable<string> DescendantTextUx8D(Node root)
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
            foreach (var text in DescendantTextUx8D(child)) yield return text;
        }
    }
}
