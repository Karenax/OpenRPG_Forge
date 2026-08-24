using System.Text.RegularExpressions;
using Forge.Domain.Modules;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioSupplyChainEvidenceWorkbenchUx8F : ColorRect
{
    private readonly StudioSessionController _session;
    private CancellationTokenSource? _cancellation;
    private Task<StudioUx8FReport>? _runTask;
    private StudioUx8FSelection? _selection;
    private StudioUx8FReport? _report;
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
    private Button _editApprovals = null!;
    private StudioDependencyApprovalDialogUx8F _dependencyEditor = null!;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _selection is not null;
    internal bool FourResultsReady => _report is
    { Succeeded: true, Cancelled: false, ResourcesReleased: true, Stages.Count: 4 };
    internal bool EvidenceResourcesReleased => _report is { ResourcesReleased: true };
    internal bool CancellationReady => _cancellationReady;
    internal bool NavigationReady => _navigationReady;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx8F(this));
            return Visible &&
                   text.Contains("Dependency approval", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Third-party notices", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Credits and notices", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("IP boundary", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Annuler l’inspection", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Éditer les approbations", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Afficher le journal", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucune publication", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx8F(text);
        }
    }

    internal StudioSupplyChainEvidenceWorkbenchUx8F(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx8F();
    }

    private void BuildUiUx8F()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.012f, 0.018f, 0.03f, 0.95f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetProcess(true);

        _dialog = StudioStyle.Panel("Ux8FSupplyChainEvidenceWorkbench", StudioStyle.Surface, 12, 1);
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
        titles.AddChild(StudioStyle.Label("CHAÎNE DE CONTENU · UX8-F", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Dépendances, notices, crédits et frontière IP", 18, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Inspection annulable · journal à la demande · génération en mémoire sans publication",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButtonUx8F("Fermer", Close, 82));
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddThemeConstantOverride("separation", 8);
        var moduleLabel = StudioStyle.Label("Module et workspace", 11, StudioStyle.Muted);
        moduleLabel.CustomMinimumSize = new Vector2(150, 0);
        selection.AddChild(moduleLabel);
        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_moduleChoice);
        _moduleChoice.ItemSelected += index => SelectModuleUx8F((int)index);
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
        BuildRouteColumnUx8F(body);
        BuildResultColumnUx8F(body);

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
        _run = ActionButtonUx8F("Inspecter la chaîne", StartRunUx8F, 165, primary: true);
        _cancel = ActionButtonUx8F("Annuler l’inspection", CancelRunUx8F, 155);
        _toggleLogs = ActionButtonUx8F("Afficher le journal", ToggleLogsUx8F, 145);
        _editApprovals = ActionButtonUx8F("Éditer les approbations", OpenDependencyEditorUx8F, 175);
        actions.AddChild(_run);
        actions.AddChild(_cancel);
        actions.AddChild(_toggleLogs);
        actions.AddChild(_editApprovals);
        root.AddChild(actions);

        _status = StudioStyle.Label("Sélectionnez explicitement le module.", 10, StudioStyle.Muted, wrap: true);
        _status.CustomMinimumSize = new Vector2(0, 28);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "Frontière honnête · aucune approbation automatique, aucune publication ; seule la commande prévisualisée est enregistrable, les notices restent générées en mémoire.",
            9, StudioStyle.Warning, wrap: true));
        _dependencyEditor = new StudioDependencyApprovalDialogUx8F(_session);
        _dependencyEditor.StatusChanged += SetStatusUx8F;
        AddChild(_dependencyEditor);
        UpdateButtonsUx8F();
    }

    private void BuildRouteColumnUx8F(HSplitContainer body)
    {
        var panel = StudioStyle.Panel("Ux8FRoutes", StudioStyle.SurfaceRaised, 8, 1);
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
        column.AddChild(StudioStyle.Label("QUATRE CONTRÔLES DE CHAÎNE", 10, StudioStyle.Accent));
        _scope = StudioStyle.Label("Sélectionnez explicitement le module.", 10, StudioStyle.Muted, wrap: true);
        _scope.CustomMinimumSize = new Vector2(0, 52);
        column.AddChild(_scope);
        column.AddChild(RouteCardUx8F("ORF-BUILD-022", "Dependency approval",
            "État, risque, quorum, audit et cibles sont croisés par le validateur de distribution."));
        column.AddChild(RouteCardUx8F("ORF-BUILD-023", "Third-party notices",
            "La notice Shipping canonique est générée deux fois en mémoire et comparée."));
        column.AddChild(RouteCardUx8F("ORF-CONT-030", "Credits and notices generation",
            "Blueprints, tables, packages, sources, assets et dépendances sont agrégés."));
        column.AddChild(RouteCardUx8F("ORF-CONT-031", "IP boundary enforcement",
            "Le registre workspace est lu ; origine, redistribution, isolation et revue restent explicites."));
        column.AddChild(StudioStyle.Label(
            "Un registre absent reste absent : la bibliothèque globale n’est jamais convertie en permission de redistribution.",
            10, StudioStyle.Warning, wrap: true));
    }

    private void BuildResultColumnUx8F(HSplitContainer body)
    {
        var panel = StudioStyle.Panel("Ux8FResults", StudioStyle.SurfaceRaised, 8, 1);
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
        _results.ItemSelected += index => SelectResultUx8F((int)index);
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
        _dependencyEditor?.Close();
        CancelRunUx8F();
        ResetUx8F();
        PopulateModuleChoiceUx8F();
        Visible = true;
        UpdateBoundsUx8F();
    }

    internal void Close()
    {
        if (!Visible && _runTask is null) return;
        _dependencyEditor.Close();
        Visible = false;
        CancelRunUx8F();
        if (_runTask is null) ResetUx8F();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        if (_session.Project is null) return false;
        if (!Visible) Open();
        _moduleChoice.Select(1);
        SelectModuleUx8F(1);
        if (_selection is null) return false;
        _report = _session.RunSupplyChainEvidenceUx8F(_selection, CancellationToken.None);
        PopulateResultsUx8F();
        SelectResultUx8F(3);
        var transactionReady = _dependencyEditor.PrepareEvidence();
        UpdateBoundsUx8F();
        SetStatusUx8F(_report.Message, !_report.Succeeded || !_report.ResourcesReleased);
        UpdateButtonsUx8F();
        return HasExplicitSelection && FourResultsReady && transactionReady && ShowsHumanSurface;
    }

    internal bool RunUiEvidence()
    {
        if ((!FourResultsReady || !_dependencyEditor.IsOpen) &&
            (!PrepareEvidence("ux8-f-supply-chain-evidence") || _selection is null)) return false;
        var selection = _selection;
        if (selection is null) return false;
        _navigationReady = true;
        for (var index = 0; index < 4; index++)
        {
            _results.Select(index);
            SelectResultUx8F(index);
            _navigationReady &= _detailTitle.Text.Contains("ORF-", StringComparison.Ordinal) &&
                                _detailBody.Text.Contains('•');
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = _session.RunSupplyChainEvidenceUx8F(selection, cancellation.Token);
        _cancellationReady = cancelled.Succeeded && cancelled.Cancelled && cancelled.Stages.Count == 0;
        ToggleLogsUx8F();
        var logsReady = _logs.Visible && !string.IsNullOrWhiteSpace(_logs.Text);
        ToggleLogsUx8F();
        SelectResultUx8F(3);
        var transactionReady = _dependencyEditor.RunUiEvidence();
        return FourResultsReady && _navigationReady && _cancellationReady && logsReady && transactionReady &&
               ShowsHumanSurface;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _dependencyEditor.Close();
        ResetUx8F();
        PopulateModuleChoiceUx8F();
        Visible = true;
        UpdateBoundsUx8F();
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        _dependencyEditor.Close();
        Visible = false;
        ResetUx8F();
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (_runTask is null || !_runTask.IsCompleted) return;
        try
        {
            _report = _runTask.GetAwaiter().GetResult();
            if (_report.Cancelled) _cancellationReady = true;
            PopulateResultsUx8F();
            if (_report.Stages.Count > 0) SelectResultUx8F(Math.Min(3, _report.Stages.Count - 1));
            SetStatusUx8F(_report.Message, !_report.Succeeded || !_report.ResourcesReleased);
        }
        catch (Exception exception)
        {
            SetStatusUx8F("Inspection interrompue · " + HumanTextUx8F(exception.Message), true);
        }
        finally
        {
            _runTask = null;
            _cancellation?.Dispose();
            _cancellation = null;
            UpdateButtonsUx8F();
            if (!Visible) ResetUx8F();
        }
    }

    public override void _ExitTree() => CancelRunUx8F();

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBoundsUx8F();
    }

    private void PopulateModuleChoiceUx8F()
    {
        _updating = true;
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Choisir explicitement le module ouvert…");
        _moduleChoice.AddItem("Inspecter · " + HumanTextUx8F(_session.Project?.Module.DisplayName ??
                                                               "Module indisponible"));
        _moduleChoice.Select(0);
        _updating = false;
    }

    private void SelectModuleUx8F(int index)
    {
        if (_updating || _runTask is not null) return;
        _explicitSelection = index == 1;
        _selection = _explicitSelection ? _session.CurrentSupplyChainEvidenceUx8F() : null;
        _report = null;
        _results.Clear();
        ClearDetailsUx8F();
        if (_selection is null)
        {
            _scope.Text = "Sélectionnez explicitement le module pour inspecter sa chaîne de contenu.";
            SetStatusUx8F("Aucun module sélectionné.");
        }
        else
        {
            _scope.Text = $"{HumanTextUx8F(_selection.ModuleName)} · {HumanTextUx8F(_selection.WorkspaceName)} · " +
                          (_selection.GovernanceFileAvailable
                              ? "registre IP présent."
                              : "registre IP absent · état affiché sans synthèse.");
            SetStatusUx8F("Module sélectionné · inspection locale disponible.");
        }
        UpdateButtonsUx8F();
    }

    private void StartRunUx8F()
    {
        if (_runTask is not null || _selection is null)
        {
            SetStatusUx8F("Sélectionnez explicitement le module avant l’inspection.", true);
            return;
        }
        _report = null;
        _results.Clear();
        ClearDetailsUx8F();
        _cancellation = new CancellationTokenSource();
        var selection = _selection;
        var token = _cancellation.Token;
        _runTask = Task.Run(() => _session.RunSupplyChainEvidenceUx8F(selection, token));
        SetStatusUx8F("Inspection en cours · annulation disponible · aucune publication distante.");
        UpdateButtonsUx8F();
    }

    private void CancelRunUx8F()
    {
        if (_runTask is null || _cancellation is null) return;
        _cancellation.Cancel();
        SetStatusUx8F("Annulation demandée · aucune écriture n’est en attente.");
        UpdateButtonsUx8F();
    }

    private void ToggleLogsUx8F()
    {
        _logsVisible = !_logsVisible;
        _logs.Visible = _logsVisible;
        _toggleLogs.Text = _logsVisible ? "Masquer le journal" : "Afficher le journal";
    }

    private void OpenDependencyEditorUx8F()
    {
        if (_selection is null)
        {
            SetStatusUx8F("Sélectionnez explicitement le module avant d’éditer ses approbations.", true);
            return;
        }
        _dependencyEditor.Open();
    }

    private void PopulateResultsUx8F()
    {
        _results.Clear();
        if (_report is null) return;
        foreach (var stage in _report.Stages)
            _results.AddItem($"{stage.Route} · {stage.Title} · {stage.Verdict}");
    }

    private void SelectResultUx8F(int index)
    {
        if (_report is null || index < 0 || index >= _report.Stages.Count)
        {
            ClearDetailsUx8F();
            return;
        }
        _results.Select(index);
        var stage = _report.Stages[index];
        _detailTitle.Text = $"{stage.Route} · {stage.Title}";
        _detailVerdict.Text = stage.Verdict;
        _detailVerdict.Modulate = stage.Ready ? StudioStyle.Success : stage.Evaluated ? StudioStyle.Warning : StudioStyle.Danger;
        _detailSummary.Text = HumanTextUx8F(stage.Summary);
        _detailBody.Text = string.Join('\n', stage.Details.Select(detail => "• " + HumanTextUx8F(detail)));
        _logs.Text = string.Join('\n', stage.Log.Select(line => HumanTextUx8F(line)));
    }

    private void ClearDetailsUx8F()
    {
        _detailTitle.Text = "Aucun résultat sélectionné";
        _detailVerdict.Text = "Lancez l’inspection locale.";
        _detailVerdict.Modulate = StudioStyle.Muted;
        _detailSummary.Text = "Les preuves apparaîtront ici.";
        _detailBody.Text = "Aucun détail.";
        _logs.Text = string.Empty;
    }

    private void ResetUx8F()
    {
        _explicitSelection = false;
        _selection = null;
        _report = null;
        _cancellationReady = false;
        _navigationReady = false;
        _results.Clear();
        _scope.Text = "Sélectionnez explicitement le module.";
        ClearDetailsUx8F();
        _logsVisible = false;
        _logs.Visible = false;
        _toggleLogs.Text = "Afficher le journal";
        _status.Text = "Sélectionnez explicitement le module.";
        _status.Modulate = StudioStyle.Muted;
        UpdateButtonsUx8F();
    }

    private void UpdateButtonsUx8F()
    {
        var running = _runTask is not null;
        _run.Disabled = running || !HasExplicitSelection;
        _cancel.Disabled = !running;
        _moduleChoice.Disabled = running;
        _toggleLogs.Disabled = _report?.Stages.Count == 0;
        _editApprovals.Disabled = running || !HasExplicitSelection;
    }

    private void SetStatusUx8F(string message, bool error = false)
    {
        _status.Text = HumanTextUx8F(message);
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(_status.Text, error);
    }

    private void UpdateBoundsUx8F()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 54, 1180, 1840);
        var height = Math.Clamp(viewport.Y - 40, 780, 1040);
        _dialog.Position = new Vector2((viewport.X - width) / 2f, (viewport.Y - height) / 2f);
        _dialog.Size = new Vector2(width, height);
    }

    private static PanelContainer RouteCardUx8F(string route, string title, string description)
    {
        var panel = StudioStyle.Panel("Ux8FRouteCard", StudioStyle.Surface, 6, 1);
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 3);
        margin.AddChild(column);
        column.AddChild(StudioStyle.Label($"{route} · {title}", 11, StudioStyle.Text, wrap: true));
        column.AddChild(StudioStyle.Label(description, 9, StudioStyle.Muted, wrap: true));
        return panel;
    }

    private static Button ActionButtonUx8F(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static string HumanTextUx8F(string value) => Regex.Replace(value.Replace('\\', '/'),
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        "élément protégé", RegexOptions.CultureInvariant);

    private static bool ContainsUuidUx8F(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b");

    private static IEnumerable<string> DescendantTextUx8F(Node root)
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
            foreach (var text in DescendantTextUx8F(child)) yield return text;
        }
    }
}

internal sealed partial class StudioDependencyApprovalDialogUx8F : ColorRect
{
    private readonly StudioSessionController _session;
    private readonly List<StudioUx8FDependencyChoice> _choices = [];
    private StudioUx8FDependencyDraft? _draft;
    private StudioUx8FDistributionPreview? _preview;
    private int _newIndex;
    private bool _updating;

    private PanelContainer _dialog = null!;
    private OptionButton _dependencyChoice = null!;
    private LineEdit _name = null!;
    private LineEdit _version = null!;
    private LineEdit _license = null!;
    private LineEdit _owner = null!;
    private LineEdit _rationale = null!;
    private LineEdit _cadence = null!;
    private LineEdit _removal = null!;
    private LineEdit _source = null!;
    private LineEdit _targets = null!;
    private OptionButton _state = null!;
    private OptionButton _risk = null!;
    private LineEdit _roles = null!;
    private SpinBox _requiredApprovals = null!;
    private LineEdit _securityAudit = null!;
    private Label _previewSummary = null!;
    private TextEdit _previewDetails = null!;
    private Label _status = null!;
    private Button _previewButton = null!;
    private Button _applyButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _saveButton = null!;

    internal event Action<string, bool>? StatusChanged;
    internal bool IsOpen => Visible;
    internal bool PreviewReady => _preview is { Succeeded: true, Changed: true, CanonicalDryRun: true, CanApply: true };
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return Visible && text.Contains("APPROBATION DE DÉPENDANCE", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Appliquer", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Annuler", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Rétablir", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Enregistrer", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Notice tierce", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioDependencyApprovalDialogUx8F(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUi();
    }

    private void BuildUi()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.008f, 0.012f, 0.02f, 0.98f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _dialog = StudioStyle.Panel("Ux8FDependencyApprovalDialog", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var contentHost = new Control { ClipContents = true };
        _dialog.AddChild(contentHost);
        var margin = StudioStyle.Margin(16, 12, 16, 12);
        contentHost.AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("APPROBATION DE DÉPENDANCE · BUILD-022/023", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label("Commande canonique et notice tierce", 18, StudioStyle.Text));
        titles.AddChild(StudioStyle.Label(
            "Chaque application exige un aperçu sec lié à la révision et au brouillon exacts.",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButton("Fermer", Close, 82));
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var selectionLabel = StudioStyle.Label("Dépendance", 11, StudioStyle.Muted);
        selectionLabel.CustomMinimumSize = new Vector2(145, 0);
        selection.AddChild(selectionLabel);
        _dependencyChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_dependencyChoice);
        _dependencyChoice.ItemSelected += index => SelectDependency((int)index);
        selection.AddChild(_dependencyChoice);
        root.AddChild(selection);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 560),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        var body = new HSplitContainer { SplitOffsets = [720] };
        bodyHost.AddChild(body);
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var formPanel = StudioStyle.Panel("Ux8FDependencyForm", StudioStyle.SurfaceRaised, 8, 1);
        body.AddChild(formPanel);
        var formMargin = StudioStyle.Margin(10, 8, 10, 8);
        formPanel.AddChild(formMargin);
        var formScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        formMargin.AddChild(formScroll);
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 6);
        formScroll.AddChild(grid);
        _name = AddField(grid, "Nom");
        _version = AddField(grid, "Version sémantique");
        _license = AddField(grid, "Licence SPDX");
        _owner = AddField(grid, "Responsable");
        _rationale = AddField(grid, "Justification");
        _cadence = AddField(grid, "Cadence de mise à jour");
        _removal = AddField(grid, "Plan de retrait");
        _source = AddField(grid, "Référence source");
        _targets = AddField(grid, "Cibles (séparées par virgule)");
        grid.AddChild(FieldLabel("État d’approbation"));
        _state = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_state);
        foreach (var value in Enum.GetValues<DependencyApprovalState>()) _state.AddItem(value.ToString());
        grid.AddChild(_state);
        grid.AddChild(FieldLabel("Niveau de risque"));
        _risk = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_risk);
        foreach (var value in Enum.GetValues<DependencyRiskLevel>()) _risk.AddItem(value.ToString());
        grid.AddChild(_risk);
        _roles = AddField(grid, "Rôles de revue (virgules)");
        grid.AddChild(FieldLabel("Quorum requis"));
        _requiredApprovals = new SpinBox
        {
            MinValue = 1,
            MaxValue = 8,
            Step = 1,
            Value = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(_requiredApprovals);
        grid.AddChild(_requiredApprovals);
        _securityAudit = AddField(grid, "Référence d’audit sécurité");

        var previewPanel = StudioStyle.Panel("Ux8FDependencyPreview", StudioStyle.SurfaceRaised, 8, 1);
        body.AddChild(previewPanel);
        var previewMargin = StudioStyle.Margin(10, 8, 10, 8);
        previewPanel.AddChild(previewMargin);
        var previewColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        previewColumn.AddThemeConstantOverride("separation", 7);
        previewMargin.AddChild(previewColumn);
        previewColumn.AddChild(StudioStyle.Label("APERÇU SEC · NOTICE TIERCE", 10, StudioStyle.Accent));
        _previewSummary = StudioStyle.Label("Sélectionnez ou créez une dépendance.", 12, StudioStyle.Text, wrap: true);
        previewColumn.AddChild(_previewSummary);
        _previewDetails = new TextEdit
        {
            Editable = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        previewColumn.AddChild(_previewDetails);

        var footer = StudioStyle.Panel("Ux8FDependencyStickyFooter", StudioStyle.Surface, 8, 1);
        var footerOverlay = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _dialog.AddChild(footerOverlay);
        footerOverlay.AddChild(footer);
        footer.SetAnchorsPreset(LayoutPreset.BottomWide);
        footer.OffsetLeft = 15;
        footer.OffsetRight = -15;
        footer.OffsetTop = -122;
        footer.OffsetBottom = -12;
        var footerMargin = StudioStyle.Margin(10, 7, 10, 7);
        footer.AddChild(footerMargin);
        var footerColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footerColumn.AddThemeConstantOverride("separation", 5);
        footerMargin.AddChild(footerColumn);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
        _previewButton = ActionButton("Aperçu sec", Preview, 118, primary: true);
        _applyButton = ActionButton("Appliquer", Apply, 106);
        _undoButton = ActionButton("Annuler", Undo, 94);
        _redoButton = ActionButton("Rétablir", Redo, 94);
        _saveButton = ActionButton("Enregistrer", Save, 112);
        actions.AddChild(_previewButton);
        actions.AddChild(_applyButton);
        actions.AddChild(_undoButton);
        actions.AddChild(_redoButton);
        actions.AddChild(_saveButton);
        footerColumn.AddChild(actions);
        _status = StudioStyle.Label("Aucun brouillon sélectionné.", 10, StudioStyle.Muted, wrap: true);
        footerColumn.AddChild(_status);
        footerColumn.AddChild(StudioStyle.Label(
            "Limite honnête · Studio enregistre la gouvernance du module ; il ne télécharge, n’audite ni ne publie aucune bibliothèque.",
            9, StudioStyle.Warning, wrap: true));
        UpdateButtons();
    }

    internal void Open()
    {
        _session.CancelDependencyApprovalUx8F();
        PopulateChoices();
        _draft = null;
        _preview = null;
        ClearForm();
        Visible = true;
        UpdateBounds();
        SetStatus("Choisissez une dépendance existante ou créez-en explicitement une nouvelle.");
    }

    internal void Close()
    {
        if (!Visible && _draft is null) return;
        _session.CancelDependencyApprovalUx8F();
        Visible = false;
        _draft = null;
        _preview = null;
        ClearForm();
    }

    internal bool PrepareEvidence()
    {
        if (!Visible) Open();
        _dependencyChoice.Select(_newIndex);
        SelectDependency(_newIndex);
        if (_draft is null) return false;
        _draft = _draft with
        {
            Name = "Bibliothèque témoin gouvernée",
            Version = "1.2.3",
            LicenseSpdx = "MIT",
            Owner = "Équipe Forge",
            Rationale = "Dépendance isolée pour la preuve transactionnelle UX8-F.",
            UpdateCadence = "trimestrielle",
            RemovalPlan = "Remplacer par l’implémentation interne.",
            SourceReference = "https://example.invalid/ux8-f-library",
            RequiredForTargets = [ModuleTargetKind.Shipping],
            ApprovalState = DependencyApprovalState.Approved,
            RiskLevel = DependencyRiskLevel.Medium,
            ApproverRoles = ["legal-review", "security-review"],
            RequiredApprovals = 2,
            SecurityAuditReference = "audit:ux8-f",
        };
        WriteDraft(_draft);
        Preview();
        return PreviewReady && ShowsHumanSurface;
    }

    internal bool RunUiEvidence()
    {
        if (!Visible && !PrepareEvidence()) return false;
        if (!PreviewReady) Preview();
        return PreviewReady && ShowsHumanSurface && !_applyButton.Disabled &&
               _previewDetails.Text.Contains("module/distribution", StringComparison.Ordinal) &&
               _previewDetails.Text.Contains("Notice tierce SHA-256", StringComparison.Ordinal);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBounds();
    }

    private void PopulateChoices()
    {
        _updating = true;
        _choices.Clear();
        _choices.AddRange(_session.CurrentDependencyChoicesUx8F());
        _dependencyChoice.Clear();
        _dependencyChoice.AddItem("Choisir explicitement une dépendance…");
        foreach (var choice in _choices) _dependencyChoice.AddItem(HumanText(choice.Label));
        _newIndex = _choices.Count + 1;
        _dependencyChoice.AddItem("Nouvelle approbation…");
        _dependencyChoice.Select(0);
        _updating = false;
    }

    private void SelectDependency(int index)
    {
        if (_updating) return;
        _session.CancelDependencyApprovalUx8F();
        _preview = null;
        _draft = index == _newIndex
            ? _session.CreateDependencyDraftUx8F(null)
            : index > 0 && index <= _choices.Count
                ? _session.CreateDependencyDraftUx8F(_choices[index - 1].Id)
                : null;
        if (_draft is null)
        {
            ClearForm();
            SetStatus("Aucune dépendance sélectionnée.");
        }
        else
        {
            WriteDraft(_draft);
            SetStatus(_draft.IsNew
                ? "Nouvelle approbation explicite · renseignez les champs puis lancez l’aperçu sec."
                : "Approbation existante chargée · aucun champ caché n’est supprimé.");
        }
        UpdateButtons();
    }

    private bool TryReadDraft(out StudioUx8FDependencyDraft draft, out string failure)
    {
        draft = _draft!;
        failure = string.Empty;
        if (_draft is null)
        {
            failure = "Sélectionnez une dépendance.";
            return false;
        }
        var targets = SplitValues(_targets.Text);
        var parsedTargets = new List<ModuleTargetKind>();
        foreach (var target in targets)
        {
            if (!Enum.TryParse<ModuleTargetKind>(target, ignoreCase: true, out var value) || !Enum.IsDefined(value))
            {
                failure = $"Cible inconnue · {target}.";
                return false;
            }
            parsedTargets.Add(value);
        }
        if (parsedTargets.Count == 0)
        {
            failure = "Déclarez au moins une cible.";
            return false;
        }
        draft = _draft with
        {
            Name = _name.Text,
            Version = _version.Text,
            LicenseSpdx = _license.Text,
            Owner = _owner.Text,
            Rationale = _rationale.Text,
            UpdateCadence = _cadence.Text,
            RemovalPlan = _removal.Text,
            SourceReference = _source.Text,
            RequiredForTargets = parsedTargets.Distinct().Order().ToArray(),
            ApprovalState = Enum.GetValues<DependencyApprovalState>()[_state.Selected],
            RiskLevel = Enum.GetValues<DependencyRiskLevel>()[_risk.Selected],
            ApproverRoles = SplitValues(_roles.Text).Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            RequiredApprovals = (int)_requiredApprovals.Value,
            SecurityAuditReference = _securityAudit.Text,
        };
        return true;
    }

    private void Preview()
    {
        if (!TryReadDraft(out var draft, out var failure))
        {
            SetStatus("Aperçu refusé · " + failure, true);
            return;
        }
        _draft = draft;
        _preview = _session.PreviewDependencyUx8F(draft);
        _previewSummary.Text = _preview.Message;
        _previewSummary.Modulate = _preview.Succeeded ? StudioStyle.Success : StudioStyle.Danger;
        _previewDetails.Text = _preview.Succeeded
            ? $"Dry-run canonique · {_preview.CanonicalDryRun}\n" +
              $"Patch · module/distribution · {_preview.Patch}\n" +
              $"Inverse · module/distribution · {_preview.Inverse}\n" +
              $"Notice tierce SHA-256 · {_preview.NoticeSha256}\n" +
              "Application · aucune écriture avant confirmation explicite."
            : string.Join('\n', _preview.Diagnostics.Select(item => $"{item.Code} · {HumanText(item.Message)}"));
        SetStatus(_preview.Message, !_preview.Succeeded);
        UpdateButtons();
    }

    private void Apply()
    {
        if (!TryReadDraft(out var draft, out var failure))
        {
            SetStatus("Application refusée · " + failure, true);
            return;
        }
        var id = draft.Id;
        var result = _session.ApplyApprovedDependencyUx8F(draft);
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            PopulateChoices();
            var index = _choices.FindIndex(item => item.Id == id);
            if (index >= 0)
            {
                _dependencyChoice.Select(index + 1);
                SelectDependency(index + 1);
            }
        }
        UpdateButtons();
    }

    private void Undo()
    {
        var result = _session.UndoDependencyUx8F();
        SetStatus(result.Message, !result.Succeeded);
        PopulateChoices();
        _draft = null;
        _preview = null;
        ClearForm();
        UpdateButtons();
    }

    private void Redo()
    {
        var result = _session.RedoDependencyUx8F();
        SetStatus(result.Message, !result.Succeeded);
        PopulateChoices();
        _draft = null;
        _preview = null;
        ClearForm();
        UpdateButtons();
    }

    private void Save()
    {
        var result = _session.SaveDependencyUx8F();
        SetStatus(result.Message, !result.Succeeded);
        UpdateButtons();
    }

    private void WriteDraft(StudioUx8FDependencyDraft draft)
    {
        _name.Text = draft.Name;
        _version.Text = draft.Version;
        _license.Text = draft.LicenseSpdx;
        _owner.Text = draft.Owner;
        _rationale.Text = draft.Rationale;
        _cadence.Text = draft.UpdateCadence;
        _removal.Text = draft.RemovalPlan;
        _source.Text = draft.SourceReference;
        _targets.Text = string.Join(", ", draft.RequiredForTargets);
        _state.Select((int)draft.ApprovalState);
        _risk.Select((int)draft.RiskLevel);
        _roles.Text = string.Join(", ", draft.ApproverRoles);
        _requiredApprovals.Value = draft.RequiredApprovals;
        _securityAudit.Text = draft.SecurityAuditReference;
        _previewSummary.Text = "Brouillon chargé · aperçu sec requis avant application.";
        _previewSummary.Modulate = StudioStyle.Text;
        _previewDetails.Text = string.Empty;
    }

    private void ClearForm()
    {
        foreach (var field in new[]
                 {
                     _name, _version, _license, _owner, _rationale, _cadence, _removal, _source,
                     _targets, _roles, _securityAudit,
                 })
            field.Text = string.Empty;
        _state.Select(0);
        _risk.Select(0);
        _requiredApprovals.Value = 2;
        _previewSummary.Text = "Sélectionnez ou créez une dépendance.";
        _previewSummary.Modulate = StudioStyle.Muted;
        _previewDetails.Text = string.Empty;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        if (_previewButton is null) return;
        _previewButton.Disabled = _draft is null;
        _applyButton.Disabled = !PreviewReady;
        _undoButton.Disabled = !_session.CanUndo;
        _redoButton.Disabled = !_session.CanRedo;
        _saveButton.Disabled = !_session.IsDirty;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = HumanText(message);
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(_status.Text, error);
    }

    private void UpdateBounds()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 70, 1220, 1760);
        var height = Math.Clamp(viewport.Y - 52, 800, 1010);
        _dialog.Position = new Vector2((viewport.X - width) / 2f, (viewport.Y - height) / 2f);
        _dialog.Size = new Vector2(width, height);
    }

    private static LineEdit AddField(GridContainer grid, string label)
    {
        grid.AddChild(FieldLabel(label));
        var field = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(field);
        grid.AddChild(field);
        return field;
    }

    private static Label FieldLabel(string text)
    {
        var label = StudioStyle.Label(text, 10, StudioStyle.Muted, wrap: true);
        label.CustomMinimumSize = new Vector2(180, 0);
        return label;
    }

    private static Button ActionButton(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static string[] SplitValues(string value) => value.Split(',', StringSplitOptions.TrimEntries |
        StringSplitOptions.RemoveEmptyEntries);

    private static string HumanText(string value) => Regex.Replace(value.Replace('\\', '/'),
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        "élément protégé", RegexOptions.CultureInvariant);

    private static bool ContainsUuid(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b");

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
                case LineEdit line when line.Visible: yield return line.Text; break;
            }
            foreach (var text in DescendantText(child)) yield return text;
        }
    }
}
