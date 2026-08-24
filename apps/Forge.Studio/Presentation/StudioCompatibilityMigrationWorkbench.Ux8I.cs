using System.Text.RegularExpressions;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCompatibilityMigrationWorkbenchUx8I : ColorRect
{
    private static readonly AssetTargetPlatform[] Platforms =
    [
        AssetTargetPlatform.Windows,
        AssetTargetPlatform.Linux,
        AssetTargetPlatform.MacOS,
        AssetTargetPlatform.Android,
        AssetTargetPlatform.IOS,
    ];

    private readonly StudioSessionController _session;
    private StudioUx8ICompatibilityDraft? _draft;
    private StudioUx8ICompatibilityPreview? _preview;
    private StudioUx8IPlatformReport? _platformReport;
    private IReadOnlyList<StudioUx8IPlatformChoice> _platformChoices = [];
    private bool _explicitSelection;
    private bool _updating;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private LineEdit _minimumRuntime = null!;
    private LineEdit _projectFormat = null!;
    private CheckBox _automaticMigrations = null!;
    private CheckBox _runtimeRequired = null!;
    private CheckBox _approved = null!;
    private OptionButton _manifestChoice = null!;
    private OptionButton _platformChoice = null!;
    private Label _manifestDetail = null!;
    private Label _compatibilitySummary = null!;
    private Label _migrationPlan = null!;
    private Label _platformPlan = null!;
    private Label _diagnostics = null!;
    private Label _status = null!;
    private Button _previewButton = null!;
    private Button _applyButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _saveButton = null!;
    private Button _platformButton = null!;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _draft is not null;
    internal bool PreviewReady => _preview is { Succeeded: true, CanonicalDryRun: true, MigrationDryRunReady: true };
    internal bool PlatformReady => _platformReport is { Succeeded: true, RequiredRolesCovered: true, ContentVerified: true };
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx8I(this));
            return Visible &&
                   text.Contains("ORF-PROJ-025", StringComparison.Ordinal) &&
                   text.Contains("ORF-CONT-025", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("migration", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("variante exacte", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("repli commun", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucune mesure matérielle", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx8I(text);
        }
    }

    internal StudioCompatibilityMigrationWorkbenchUx8I(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx8I();
    }

    internal void Open()
    {
        _session.CancelCompatibilityApprovalUx8I();
        ResetUx8I();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionnez explicitement le module…");
        if (_session.Project is not null)
            _moduleChoice.AddItem($"{_session.Project.Module.DisplayName} · workspace réel");
        _moduleChoice.Select(0);
        Visible = true;
        UpdateBoundsUx8I();
    }

    internal void Close()
    {
        if (!Visible) return;
        Visible = false;
        _session.CancelCompatibilityApprovalUx8I();
        ResetUx8I();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        Open();
        if (_moduleChoice.ItemCount < 2) return false;
        _moduleChoice.Select(1);
        SelectModuleUx8I(1);
        if (_draft is null) return false;
        _draft = _draft with
        {
            MinimumRuntimeVersion = ModuleRuntimeCompatibility.CurrentRuntimeVersion,
            ProjectFormatVersion = ProjectSession.CurrentProjectFormatVersion,
            AutomaticMigrations = true,
            RequiredAtRuntime = true,
            Approved = true,
        };
        PopulateDraftUx8I(_draft);
        PreviewUx8I();
        for (var index = 0; index < _platformChoices.Count; index++)
        {
            var report = _session.AnalyzePlatformUx8I(_platformChoices[index].AssetId, AssetTargetPlatform.Windows);
            if (!report.Succeeded) continue;
            _manifestChoice.Select(index + 1);
            _platformChoice.Select(0);
            SelectManifestUx8I(index + 1);
            _platformReport = report;
            RenderPlatformUx8I(report);
            break;
        }
        var ready = PreviewReady && PlatformReady && ShowsHumanSurface;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_I_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
                 $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                 $"platform={PlatformReady.ToString().ToLowerInvariant()} " +
                 $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                 $"manifests={_platformChoices.Count}");
        return ready;
    }

    internal bool RunUiEvidence()
    {
        var sourceProject = _session.Project;
        var sourceHistory = _session.History;
        var sourceRevision = sourceHistory?.State.Revision;
        var sourceJournal = sourceHistory?.Journal.ToArray() ?? [];
        var sourceDirty = _session.IsDirty;
        var approval = _session.CaptureCompatibilityApprovalUx8I();
        var draft = ReadDraftUx8I();
        if (draft is null || _manifestChoice.Selected <= 0) return false;
        var preview = _session.PreviewCompatibilityUx8I(draft with { Approved = true });
        var report = _session.AnalyzePlatformUx8I(
            _platformChoices[_manifestChoice.Selected - 1].AssetId,
            Platforms[Math.Clamp(_platformChoice.Selected, 0, Platforms.Length - 1)]);
        _session.RestoreCompatibilityApprovalUx8I(approval);
        var sourceUnchanged = ReferenceEquals(sourceProject, _session.Project) &&
                              ReferenceEquals(sourceHistory, _session.History) &&
                              _session.History?.State.Revision == sourceRevision &&
                              (_session.History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                              _session.IsDirty == sourceDirty;
        return preview.Succeeded && preview.CanonicalDryRun && report.Succeeded &&
               sourceUnchanged && ShowsHumanSurface;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        Visible = true;
        UpdateBoundsUx8I();
    }

    internal void HideSourceSurfaceAfterSmoke() => Visible = false;

    public override void _Process(double delta)
    {
        _ = delta;
        if (Visible) UpdateBoundsUx8I();
    }

    private void BuildUiUx8I()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.012f, 0.018f, 0.03f, 0.96f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetProcess(true);

        _dialog = StudioStyle.Panel("Ux8ICompatibilityMigrationWorkbench", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(15, 12, 15, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 7);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("COMPATIBILITÉ & MIGRATIONS · UX8-I", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label(
            "Contrat projet versionné et compatibilité réelle des assets par plateforme",
            18, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Dry-run canonique · fichiers et SHA-256 réellement inspectés · aucune mesure matérielle ni migration implicite",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButtonUx8I("Fermer", Close, 82));
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddThemeConstantOverride("separation", 8);
        var selectionLabel = StudioStyle.Label("Module à vérifier", 10, StudioStyle.Muted);
        selectionLabel.CustomMinimumSize = new Vector2(150, 0);
        selection.AddChild(selectionLabel);
        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_moduleChoice);
        _moduleChoice.ItemSelected += index => SelectModuleUx8I((int)index);
        selection.AddChild(_moduleChoice);
        root.AddChild(selection);

        root.AddChild(StudioStyle.Label(
            "ORF-PROJ-025 · compatibilité et migrations   |   ORF-CONT-025 · compatibilité de plateforme",
            9, StudioStyle.Warning, wrap: true));

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 620),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        var body = new HSplitContainer { SplitOffsets = [590] };
        bodyHost.AddChild(body);
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildFormUx8I(body);
        BuildReportUx8I(body);

        var footer = StudioStyle.Panel("Ux8IStickyFooter", StudioStyle.SurfaceRaised, 8, 1);
        root.AddChild(footer);
        var footerMargin = StudioStyle.Margin(9, 7, 9, 7);
        footer.AddChild(footerMargin);
        var footerColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footerColumn.AddThemeConstantOverride("separation", 5);
        footerMargin.AddChild(footerColumn);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
        _previewButton = ActionButtonUx8I("Prévisualiser", PreviewUx8I, 120, primary: true);
        _applyButton = ActionButtonUx8I("Appliquer", ApplyUx8I, 104);
        _undoButton = ActionButtonUx8I("Annuler", UndoUx8I, 96);
        _redoButton = ActionButtonUx8I("Rétablir", RedoUx8I, 96);
        _saveButton = ActionButtonUx8I("Enregistrer", SaveUx8I, 112);
        _platformButton = ActionButtonUx8I("Vérifier la plateforme", AnalyzePlatformUx8I, 180);
        foreach (var button in new[]
                 {
                     _previewButton, _applyButton, _undoButton, _redoButton, _saveButton, _platformButton,
                 })
            actions.AddChild(button);
        footerColumn.AddChild(actions);
        _status = StudioStyle.Label("Sélectionnez explicitement le module.", 10, StudioStyle.Muted, wrap: true);
        _status.CustomMinimumSize = new Vector2(0, 24);
        footerColumn.AddChild(_status);
        footerColumn.AddChild(StudioStyle.Label(
            "L’analyse plateforme choisit une variante exacte ou un repli commun, vérifie budget, rôles requis et SHA-256. Elle ne lance ni GPU, ni appareil, ni benchmark.",
            9, StudioStyle.Warning, wrap: true));
        UpdateButtonsUx8I();
    }

    private void BuildFormUx8I(HSplitContainer body)
    {
        var panel = StudioStyle.Panel("Ux8ICompatibilityForm", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(530, 0);
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
        column.AddThemeConstantOverride("separation", 9);
        scroll.AddChild(column);

        column.AddChild(SectionLabelUx8I("CONTRAT PROJET VERSIONNÉ"));
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 9);
        grid.AddThemeConstantOverride("v_separation", 7);
        _minimumRuntime = AddFieldUx8I(grid, "Runtime minimal (SemVer)");
        _projectFormat = AddFieldUx8I(grid, "Format projet cible (SemVer)");
        _automaticMigrations = AddCheckUx8I(grid, "Migrations versionnées automatiques");
        _runtimeRequired = AddCheckUx8I(grid, "Refuser un runtime incompatible");
        _approved = AddCheckUx8I(grid, "J’approuve ce changement");
        column.AddChild(grid);
        column.AddChild(StudioStyle.Label(
            "L’identité, le schéma et la provenance restent protégés. Le dry-run de migration ne réécrit aucun document.",
            9, StudioStyle.Muted, wrap: true));

        column.AddChild(SectionLabelUx8I("ASSET ET PLATEFORME À INSPECTER"));
        _manifestChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_manifestChoice);
        _manifestChoice.ItemSelected += index => SelectManifestUx8I((int)index);
        column.AddChild(_manifestChoice);
        _manifestDetail = StudioStyle.Label("Aucun manifeste sélectionné.", 9, StudioStyle.Muted, wrap: true);
        _manifestDetail.CustomMinimumSize = new Vector2(0, 40);
        column.AddChild(_manifestDetail);
        _platformChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_platformChoice);
        foreach (var platform in Platforms) _platformChoice.AddItem(HumanPlatformUx8I(platform));
        _platformChoice.Select(0);
        column.AddChild(_platformChoice);
        column.AddChild(StudioStyle.Label(
            "Les manifests restent propriété d’UX3-G : cette surface les inspecte sans les modifier.",
            9, StudioStyle.Muted, wrap: true));
    }

    private void BuildReportUx8I(HSplitContainer body)
    {
        var panel = StudioStyle.Panel("Ux8ICompatibilityReport", StudioStyle.SurfaceRaised, 8, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
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
        column.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(column);
        column.AddChild(SectionLabelUx8I("APERÇU SEC · COMPATIBILITÉ"));
        _compatibilitySummary = ReportLabelUx8I("Aucun aperçu sec.", 92);
        column.AddChild(_compatibilitySummary);
        column.AddChild(SectionLabelUx8I("PLAN DE MIGRATION"));
        _migrationPlan = ReportLabelUx8I("Aucune migration inspectée.", 105);
        column.AddChild(_migrationPlan);
        column.AddChild(SectionLabelUx8I("VARIANTE EXACTE OU REPLI COMMUN"));
        _platformPlan = ReportLabelUx8I("Sélectionnez un asset et une plateforme.", 180);
        column.AddChild(_platformPlan);
        column.AddChild(SectionLabelUx8I("DIAGNOSTICS · CODE + CHEMIN"));
        _diagnostics = ReportLabelUx8I("Aucun diagnostic.", 120);
        column.AddChild(_diagnostics);
    }

    private void SelectModuleUx8I(int index)
    {
        if (_updating) return;
        _explicitSelection = index == 1 && _session.Project is not null;
        _draft = _explicitSelection ? _session.CurrentCompatibilityDraftUx8I() : null;
        _preview = null;
        _platformReport = null;
        _platformChoices = _explicitSelection ? _session.ListPlatformChoicesUx8I() : [];
        PopulateManifestChoicesUx8I();
        if (_draft is not null) PopulateDraftUx8I(_draft);
        else ResetReportsUx8I("Sélectionnez explicitement le module.");
        UpdateButtonsUx8I();
    }

    private void PopulateDraftUx8I(StudioUx8ICompatibilityDraft draft)
    {
        _updating = true;
        _minimumRuntime.Text = draft.MinimumRuntimeVersion;
        _projectFormat.Text = draft.ProjectFormatVersion;
        _automaticMigrations.ButtonPressed = draft.AutomaticMigrations;
        _runtimeRequired.ButtonPressed = draft.RequiredAtRuntime;
        _approved.ButtonPressed = draft.Approved;
        _updating = false;
        ResetReportsUx8I("Brouillon chargé · lancez l’aperçu sec canonique.");
    }

    private void PopulateManifestChoicesUx8I()
    {
        _updating = true;
        _manifestChoice.Clear();
        _manifestChoice.AddItem("Sélectionnez explicitement un asset…");
        foreach (var choice in _platformChoices) _manifestChoice.AddItem(choice.Label);
        _manifestChoice.Select(0);
        _manifestDetail.Text = _platformChoices.Count == 0
            ? "Aucun manifeste d’asset dans ce workspace · état vide honnête."
            : $"{_platformChoices.Count} manifeste(s) gouverné(s) disponibles.";
        _updating = false;
    }

    private void SelectManifestUx8I(int index)
    {
        if (_updating) return;
        _platformReport = null;
        _manifestDetail.Text = index > 0 && index <= _platformChoices.Count
            ? _platformChoices[index - 1].Detail
            : "Sélectionnez explicitement un manifeste d’asset.";
        _platformPlan.Text = "Cliquez sur Vérifier la plateforme.";
        UpdateButtonsUx8I();
    }

    private StudioUx8ICompatibilityDraft? ReadDraftUx8I()
    {
        if (_draft is null) return null;
        return _draft = _draft with
        {
            MinimumRuntimeVersion = _minimumRuntime.Text,
            ProjectFormatVersion = _projectFormat.Text,
            AutomaticMigrations = _automaticMigrations.ButtonPressed,
            RequiredAtRuntime = _runtimeRequired.ButtonPressed,
            Approved = _approved.ButtonPressed,
        };
    }

    private void PreviewUx8I()
    {
        var draft = ReadDraftUx8I();
        if (draft is null)
        {
            SetStatusUx8I("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewCompatibilityUx8I(draft);
        _compatibilitySummary.Text = string.Join('\n', _preview.SemanticDiff.Select(item => "• " + item));
        _migrationPlan.Text = string.Join('\n', _preview.MigrationPlan.Select(item => "• " + item));
        RenderDiagnosticsUx8I();
        SetStatusUx8I(_preview.Message, !_preview.Succeeded);
        UpdateButtonsUx8I();
    }

    private void AnalyzePlatformUx8I()
    {
        if (_manifestChoice.Selected <= 0 || _manifestChoice.Selected > _platformChoices.Count)
        {
            SetStatusUx8I("Sélectionnez explicitement un manifeste d’asset.", true);
            return;
        }
        var platform = Platforms[Math.Clamp(_platformChoice.Selected, 0, Platforms.Length - 1)];
        _platformReport = _session.AnalyzePlatformUx8I(
            _platformChoices[_manifestChoice.Selected - 1].AssetId, platform);
        RenderPlatformUx8I(_platformReport);
        RenderDiagnosticsUx8I();
        SetStatusUx8I(_platformReport.Succeeded
            ? $"{HumanPlatformUx8I(platform)} · fichiers, rôles et budget compatibles."
            : $"{HumanPlatformUx8I(platform)} · compatibilité incomplète ; consultez les diagnostics.",
            !_platformReport.Succeeded);
        UpdateButtonsUx8I();
    }

    private void RenderPlatformUx8I(StudioUx8IPlatformReport report)
    {
        _platformPlan.Text = string.Join('\n', report.Plan.Select(item => "• " + item));
        if (report.Plan.Count == 0) _platformPlan.Text = "Aucun fichier compatible sélectionné.";
    }

    private void RenderDiagnosticsUx8I()
    {
        var diagnostics = (_preview?.Diagnostics ?? []).Concat(_platformReport?.Diagnostics ?? [])
            .Distinct(StringComparer.Ordinal).ToArray();
        _diagnostics.Text = diagnostics.Length == 0
            ? "Aucun diagnostic bloquant."
            : string.Join('\n', diagnostics.Select(item => "• " + ScrubHumanUx8I(item)));
    }

    private void ApplyUx8I()
    {
        var draft = ReadDraftUx8I();
        if (draft is null) return;
        var result = _session.ApplyApprovedCompatibilityUx8I(draft);
        SetStatusUx8I(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _draft = _session.CurrentCompatibilityDraftUx8I();
            if (_draft is not null) PopulateDraftUx8I(_draft);
        }
        UpdateButtonsUx8I();
    }

    private void UndoUx8I()
    {
        var result = _session.UndoCompatibilityUx8I();
        SetStatusUx8I(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _draft = _session.CurrentCompatibilityDraftUx8I();
            if (_draft is not null) PopulateDraftUx8I(_draft);
        }
        UpdateButtonsUx8I();
    }

    private void RedoUx8I()
    {
        var result = _session.RedoCompatibilityUx8I();
        SetStatusUx8I(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _draft = _session.CurrentCompatibilityDraftUx8I();
            if (_draft is not null) PopulateDraftUx8I(_draft);
        }
        UpdateButtonsUx8I();
    }

    private void SaveUx8I()
    {
        var result = _session.SaveCompatibilityUx8I();
        SetStatusUx8I(result.Message, !result.Succeeded);
        UpdateButtonsUx8I();
    }

    private void ResetUx8I()
    {
        _draft = null;
        _preview = null;
        _platformReport = null;
        _platformChoices = [];
        _explicitSelection = false;
        if (_moduleChoice is not null) _moduleChoice.Clear();
        if (_manifestChoice is not null) _manifestChoice.Clear();
        ResetReportsUx8I("Sélectionnez explicitement le module.");
        UpdateButtonsUx8I();
    }

    private void ResetReportsUx8I(string message)
    {
        _preview = null;
        _compatibilitySummary.Text = "Aucun aperçu sec.";
        _migrationPlan.Text = "Aucune migration inspectée.";
        _platformPlan.Text = "Sélectionnez un asset et une plateforme.";
        _diagnostics.Text = "Aucun diagnostic.";
        SetStatusUx8I(message, false);
    }

    private void UpdateButtonsUx8I()
    {
        if (_previewButton is null) return;
        _previewButton.Disabled = _draft is null;
        _applyButton.Disabled = _draft is null || _preview?.CanApply != true;
        _undoButton.Disabled = !_session.CanUndoCompatibilityUx8I;
        _redoButton.Disabled = !_session.CanRedoCompatibilityUx8I;
        _saveButton.Disabled = _draft is null;
        _platformButton.Disabled = _manifestChoice is null || _manifestChoice.Selected <= 0;
    }

    private void SetStatusUx8I(string message, bool error)
    {
        if (_status is null) return;
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateBoundsUx8I()
    {
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(900, size.X - 48), Math.Max(760, size.Y - 36));
    }

    private static LineEdit AddFieldUx8I(GridContainer grid, string label)
    {
        grid.AddChild(FieldLabelUx8I(label));
        var field = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(field);
        grid.AddChild(field);
        return field;
    }

    private static CheckBox AddCheckUx8I(GridContainer grid, string label)
    {
        grid.AddChild(FieldLabelUx8I(label));
        var field = new CheckBox { Text = "Oui" };
        grid.AddChild(field);
        return field;
    }

    private static Label FieldLabelUx8I(string text)
    {
        var label = StudioStyle.Label(text, 10, StudioStyle.Muted, wrap: true);
        label.CustomMinimumSize = new Vector2(215, 0);
        return label;
    }

    private static Label SectionLabelUx8I(string text) =>
        StudioStyle.Label(text, 10, StudioStyle.Accent, wrap: true);

    private static Label ReportLabelUx8I(string text, float height)
    {
        var label = StudioStyle.Label(text, 10, StudioStyle.Text, wrap: true);
        label.CustomMinimumSize = new Vector2(0, height);
        return label;
    }

    private static Button ActionButtonUx8I(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static string HumanPlatformUx8I(AssetTargetPlatform platform) => platform switch
    {
        AssetTargetPlatform.Windows => "Windows",
        AssetTargetPlatform.Linux => "Linux",
        AssetTargetPlatform.MacOS => "macOS",
        AssetTargetPlatform.Android => "Android",
        AssetTargetPlatform.IOS => "iOS",
        _ => "Toutes plateformes",
    };

    private static IEnumerable<string> DescendantTextUx8I(Node node)
    {
        switch (node)
        {
            case Label label:
                yield return label.Text;
                break;
            case OptionButton option:
                for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                break;
            case Button button:
                yield return button.Text;
                break;
            case LineEdit line:
                yield return line.Text;
                break;
        }
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx8I(child))
                yield return value;
    }

    private static bool ContainsUuidUx8I(string text) => Regex.IsMatch(text,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        RegexOptions.CultureInvariant);

    private static string ScrubHumanUx8I(string value) => Regex.Replace(
        value ?? string.Empty,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        "identifiant protégé",
        RegexOptions.CultureInvariant);
}
