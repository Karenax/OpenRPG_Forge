using System.Text.RegularExpressions;
using Forge.Domain.Modules;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioDistributionReleaseWorkbenchUx8H : ColorRect
{
    private static readonly ModuleTargetKind[] TargetValues = Enum.GetValues<ModuleTargetKind>();
    private static readonly ModuleReleaseChannel[] ChannelValues = Enum.GetValues<ModuleReleaseChannel>();
    private static readonly ModuleSigningMode[] SigningValues = Enum.GetValues<ModuleSigningMode>();
    private static readonly WindowsPackageMode[] WindowsValues = Enum.GetValues<WindowsPackageMode>();
    private static readonly int[] ChunkValues =
    [
        64 * 1024, 128 * 1024, 256 * 1024, 512 * 1024, 1024 * 1024,
        2 * 1024 * 1024, 4 * 1024 * 1024, 8 * 1024 * 1024, 16 * 1024 * 1024,
    ];

    private readonly StudioSessionController _session;
    private StudioUx8HDistributionDraft? _draft;
    private StudioUx8HDistributionPreview? _preview;
    private StudioUx8HArtifactReport? _artifactReport;
    private Task<StudioUx8HArtifactReport>? _artifactTask;
    private CancellationTokenSource? _artifactCancellation;
    private bool _explicitSelection;
    private bool _updating;
    private bool _navigationReady;
    private bool _evidenceResourcesReleased = true;
    private int _selectedTarget;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private OptionButton _targetChoice = null!;
    private CheckBox _targetEnabled = null!;
    private CheckBox _editorData = null!;
    private CheckBox _debugSymbols = null!;
    private CheckBox _optimize = null!;
    private CheckBox _approvedLicenses = null!;
    private LineEdit _outputName = null!;
    private LineEdit _secretsProfile = null!;
    private OptionButton _channel = null!;
    private readonly Dictionary<ModuleDistributionPlatform, CheckBox> _platforms = [];
    private OptionButton _signingMode = null!;
    private LineEdit _signingProfile = null!;
    private LineEdit _trustedKeys = null!;
    private LineEdit _revokedKeys = null!;
    private CheckBox _unsignedDevelopment = null!;
    private CheckBox _deltaEnabled = null!;
    private OptionButton _chunkSize = null!;
    private CheckBox _deltaResume = null!;
    private CheckBox _deltaDeduplicate = null!;
    private CheckBox _deltaRollback = null!;
    private SpinBox _retainedVersions = null!;
    private OptionButton _windowsMode = null!;
    private CheckBox _detectPrerequisites = null!;
    private CheckBox _installLog = null!;
    private CheckBox _uninstaller = null!;
    private LineEdit _prerequisites = null!;
    private CheckBox _approved = null!;
    private Label _previewSummary = null!;
    private Label _plan = null!;
    private Label _diagnostics = null!;
    private Label _status = null!;
    private Button _previewButton = null!;
    private Button _applyButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _saveButton = null!;
    private Button _verifyButton = null!;
    private Button _cancelVerification = null!;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _draft is not null;
    internal bool PreviewReady => _preview is { Succeeded: true, CanonicalDryRun: true, Targets.Count: 5 };
    internal bool NavigationReady => _navigationReady;
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx8H(this));
            return Visible &&
                   text.Contains("ORF-PROJ-024", StringComparison.Ordinal) &&
                   text.Contains("ORF-BUILD-016", StringComparison.Ordinal) &&
                   text.Contains("ORF-BUILD-017", StringComparison.Ordinal) &&
                   text.Contains("ORF-CONT-028", StringComparison.Ordinal) &&
                   text.Contains("ORF-CONT-029", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Vérifier les bundles", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("clé privée", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucune publication", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx8H(text);
        }
    }

    internal StudioDistributionReleaseWorkbenchUx8H(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx8H();
    }

    private void BuildUiUx8H()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.012f, 0.018f, 0.03f, 0.96f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetProcess(true);

        _dialog = StudioStyle.Panel("Ux8HDistributionReleaseWorkbench", StudioStyle.Surface, 12, 1);
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
        titles.AddChild(StudioStyle.Label("LIVRAISON MULTI-PLATEFORME · UX8-H", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label(
            "Cibles, bundles, packaging Windows, signatures et mises à jour delta",
            18, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Commande transactionnelle · vérification réelle en isolat · aucune clé privée stockée · aucune publication",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButtonUx8H("Fermer", Close, 82));
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddThemeConstantOverride("separation", 8);
        var selectionLabel = StudioStyle.Label("Module à configurer", 10, StudioStyle.Muted);
        selectionLabel.CustomMinimumSize = new Vector2(150, 0);
        selection.AddChild(selectionLabel);
        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_moduleChoice);
        _moduleChoice.ItemSelected += index => SelectModuleUx8H((int)index);
        selection.AddChild(_moduleChoice);
        root.AddChild(selection);

        var routes = StudioStyle.Label(
            "ORF-PROJ-024 · paramètres par cible   |   ORF-BUILD-016 · multi-plateforme   |   " +
            "ORF-BUILD-017 · packaging Windows   |   ORF-CONT-028 · signatures   |   ORF-CONT-029 · deltas",
            9, StudioStyle.Warning, wrap: true);
        routes.CustomMinimumSize = new Vector2(0, 28);
        root.AddChild(routes);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 680),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.Fill,
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        var body = new HSplitContainer { SplitOffsets = [650] };
        bodyHost.AddChild(body);
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildFormUx8H(body);
        BuildPreviewUx8H(body);

        var footer = StudioStyle.Panel("Ux8HStickyFooter", StudioStyle.SurfaceRaised, 8, 1);
        root.AddChild(footer);
        var footerMargin = StudioStyle.Margin(9, 7, 9, 7);
        footer.AddChild(footerMargin);
        var footerColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footerColumn.AddThemeConstantOverride("separation", 5);
        footerMargin.AddChild(footerColumn);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
        _previewButton = ActionButtonUx8H("Prévisualiser", PreviewUx8H, 120, primary: true);
        _applyButton = ActionButtonUx8H("Appliquer", ApplyUx8H, 104);
        _undoButton = ActionButtonUx8H("Annuler", UndoUx8H, 96);
        _redoButton = ActionButtonUx8H("Rétablir", RedoUx8H, 96);
        _saveButton = ActionButtonUx8H("Enregistrer", SaveUx8H, 112);
        _verifyButton = ActionButtonUx8H("Vérifier les bundles", StartArtifactVerificationUx8H, 170);
        _cancelVerification = ActionButtonUx8H("Annuler la vérification", CancelArtifactVerificationUx8H, 175);
        foreach (var button in new[]
                 {
                     _previewButton, _applyButton, _undoButton, _redoButton, _saveButton,
                     _verifyButton, _cancelVerification,
                 })
            actions.AddChild(button);
        footerColumn.AddChild(actions);
        _status = StudioStyle.Label("Sélectionnez explicitement le module.", 10, StudioStyle.Muted, wrap: true);
        _status.CustomMinimumSize = new Vector2(0, 26);
        footerColumn.AddChild(_status);
        footerColumn.AddChild(StudioStyle.Label(
            "Aperçu sec : aucune compilation. Vérifier les bundles : build réel dans une copie temporaire, puis nettoyage. Une signature requise reste fournie par la CI externe.",
            9, StudioStyle.Warning, wrap: true));
        UpdateButtonsUx8H();
    }

    private void BuildFormUx8H(HSplitContainer body)
    {
        var panel = StudioStyle.Panel("Ux8HDistributionForm", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(590, 0);
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

        column.AddChild(SectionLabelUx8H("CIBLE DE BUILD"));
        _targetChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_targetChoice);
        foreach (var target in TargetValues) _targetChoice.AddItem(StudioSessionController.HumanTargetUx8H(target));
        _targetChoice.ItemSelected += index => SelectTargetUx8H((int)index);
        column.AddChild(_targetChoice);
        var targetGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _targetEnabled = AddCheckUx8H(targetGrid, "Cible activée");
        _editorData = AddCheckUx8H(targetGrid, "Données d’édition incluses");
        _debugSymbols = AddCheckUx8H(targetGrid, "Symboles de débogage");
        _optimize = AddCheckUx8H(targetGrid, "Optimiser le contenu");
        _approvedLicenses = AddCheckUx8H(targetGrid, "Licences approuvées requises");
        _outputName = AddFieldUx8H(targetGrid, "Nom de sortie");
        _secretsProfile = AddFieldUx8H(targetGrid, "Profil de secrets (nom seulement)");
        column.AddChild(targetGrid);

        column.AddChild(SectionLabelUx8H("CANAL ET PLATEFORMES"));
        _channel = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_channel);
        foreach (var channel in ChannelValues) _channel.AddItem(StudioSessionController.HumanChannelUx8H(channel));
        column.AddChild(_channel);
        var platformRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var platform in Enum.GetValues<ModuleDistributionPlatform>())
        {
            var check = new CheckBox { Text = StudioSessionController.HumanPlatformUx8H(platform) };
            platformRow.AddChild(check);
            _platforms[platform] = check;
        }
        column.AddChild(platformRow);

        column.AddChild(SectionLabelUx8H("SIGNATURES · SHA-256 PUBLIC"));
        var signingGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        signingGrid.AddChild(FieldLabelUx8H("Politique"));
        _signingMode = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_signingMode);
        foreach (var mode in SigningValues) _signingMode.AddItem(StudioSessionController.HumanSigningUx8H(mode));
        signingGrid.AddChild(_signingMode);
        _signingProfile = AddFieldUx8H(signingGrid, "Profil public");
        _trustedKeys = AddFieldUx8H(signingGrid, "Empreintes de confiance (une par ligne ou ;)");
        _revokedKeys = AddFieldUx8H(signingGrid, "Empreintes révoquées (une par ligne ou ;)");
        _unsignedDevelopment = AddCheckUx8H(signingGrid, "Autoriser le développement non signé");
        column.AddChild(signingGrid);
        column.AddChild(StudioStyle.Label(
            "La clé privée n’est jamais saisie ni persistée ici. Studio conserve uniquement des empreintes SHA-256 publiques.",
            9, StudioStyle.Warning, wrap: true));

        column.AddChild(SectionLabelUx8H("MISES À JOUR DELTA"));
        var deltaGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _deltaEnabled = AddCheckUx8H(deltaGrid, "Delta activé");
        deltaGrid.AddChild(FieldLabelUx8H("Taille de bloc"));
        _chunkSize = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_chunkSize);
        foreach (var value in ChunkValues) _chunkSize.AddItem($"{value / 1024:N0} Kio");
        deltaGrid.AddChild(_chunkSize);
        _deltaResume = AddCheckUx8H(deltaGrid, "Reprise autorisée");
        _deltaDeduplicate = AddCheckUx8H(deltaGrid, "Déduplication par empreinte");
        _deltaRollback = AddCheckUx8H(deltaGrid, "Retour arrière autorisé");
        deltaGrid.AddChild(FieldLabelUx8H("Versions conservées"));
        _retainedVersions = new SpinBox
        {
            MinValue = 1,
            MaxValue = 16,
            Step = 1,
            Rounded = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(_retainedVersions);
        deltaGrid.AddChild(_retainedVersions);
        column.AddChild(deltaGrid);

        column.AddChild(SectionLabelUx8H("PACKAGING WINDOWS"));
        var windowsGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        windowsGrid.AddChild(FieldLabelUx8H("Mode"));
        _windowsMode = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_windowsMode);
        foreach (var mode in WindowsValues) _windowsMode.AddItem(StudioSessionController.HumanWindowsModeUx8H(mode));
        windowsGrid.AddChild(_windowsMode);
        _detectPrerequisites = AddCheckUx8H(windowsGrid, "Détecter les pré-requis");
        _installLog = AddCheckUx8H(windowsGrid, "Écrire le journal d’installation");
        _uninstaller = AddCheckUx8H(windowsGrid, "Inclure le désinstalleur");
        _prerequisites = AddFieldUx8H(windowsGrid, "Pré-requis (un par ligne ou ;)");
        column.AddChild(windowsGrid);
        _approved = new CheckBox { Text = "J’approuve exactement ce brouillon pour cette révision" };
        column.AddChild(_approved);
    }

    private void BuildPreviewUx8H(HSplitContainer body)
    {
        var panel = StudioStyle.Panel("Ux8HDistributionPreview", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(650, 0);
        body.AddChild(panel);
        var margin = StudioStyle.Margin(10, 8, 10, 8);
        panel.AddChild(margin);
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        column.AddThemeConstantOverride("separation", 7);
        margin.AddChild(column);
        column.AddChild(SectionLabelUx8H("APERÇU SEC · CODE + CHEMIN"));
        _previewSummary = StudioStyle.Label("Sélectionnez explicitement le module.", 12, StudioStyle.Text, wrap: true);
        _previewSummary.CustomMinimumSize = new Vector2(0, 54);
        column.AddChild(_previewSummary);
        var planScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 410),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var planColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _plan = StudioStyle.Label("Aucun plan.", 10, StudioStyle.Text, wrap: true);
        _plan.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        planColumn.AddChild(_plan);
        planScroll.AddChild(planColumn);
        column.AddChild(planScroll);
        column.AddChild(SectionLabelUx8H("DIAGNOSTICS"));
        var diagnosticScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 130),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.Fill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var diagnosticColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _diagnostics = StudioStyle.Label("Aucun diagnostic.", 9, StudioStyle.Muted, wrap: true);
        _diagnostics.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        diagnosticColumn.AddChild(_diagnostics);
        diagnosticScroll.AddChild(diagnosticColumn);
        column.AddChild(diagnosticScroll);
    }

    internal void Open()
    {
        CancelArtifactVerificationUx8H();
        ResetUx8H();
        PopulateModuleChoiceUx8H();
        Visible = true;
        UpdateBoundsUx8H();
    }

    internal void Close()
    {
        if (!Visible && _artifactTask is null) return;
        Visible = false;
        CancelArtifactVerificationUx8H();
        _session.CancelDistributionApprovalUx8H();
        ResetUx8H();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        Open();
        if (_moduleChoice.ItemCount < 2) return false;
        _moduleChoice.Select(1);
        SelectModuleUx8H(1);
        if (_draft is null) return false;
        var target = _draft.Targets.Select(item => item with
        {
            Enabled = true,
            RequireApprovedLicenses = false,
            OutputName = item.Target == ModuleTargetKind.Shipping
                ? "shipping-ux8h-evidence"
                : item.OutputName,
        }).ToArray();
        _draft = _draft with
        {
            Targets = target,
            Channel = ModuleReleaseChannel.Preview,
            Platforms = Enum.GetValues<ModuleDistributionPlatform>(),
            SigningMode = ModuleSigningMode.Optional,
            SigningProfile = "ci-release",
            TrustedKeyFingerprints = [],
            RevokedKeyFingerprints = [],
            AllowUnsignedDevelopment = true,
            DeltaEnabled = true,
            DeltaAllowResume = true,
            DeltaDeduplicateByHash = true,
            DeltaAllowRollback = true,
            DeltaRetainedVersions = 3,
            WindowsMode = WindowsPackageMode.PortableAndInstaller,
            DetectWindowsPrerequisites = true,
            WriteWindowsInstallLog = true,
            IncludeWindowsUninstaller = true,
            WindowsPrerequisites = ["windows-x64", "vulkan-or-d3d12"],
            Approved = true,
        };
        PopulateDraftUx8H(_draft);
        PreviewUx8H();
        _navigationReady = _targetChoice.ItemCount == 5 && _channel.ItemCount == 3 && _platforms.Count == 3;
        var ready = PreviewReady && ShowsHumanSurface && _navigationReady;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_H_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
                 $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                 $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                 $"navigation={_navigationReady.ToString().ToLowerInvariant()} " +
                 $"diagnostics={_preview?.Diagnostics.Count ?? 0} " +
                 $"message={ScrubMarkerUx8H(_preview?.Message)}");
        return ready;
    }

    internal bool RunUiEvidence()
    {
        var sourceProject = _session.Project;
        var sourceHistory = _session.History;
        var sourceRevision = sourceHistory?.State.Revision;
        var sourceJournal = sourceHistory?.Journal.ToArray() ?? [];
        var sourceDirty = _session.IsDirty;
        var sourceApproval = _session.CaptureDistributionApprovalUx8H();
        if (_draft is null || sourceProject is null || sourceHistory is null) return false;
        var originalIndex = _targetChoice.Selected;
        _targetChoice.Select((int)ModuleTargetKind.Shipping);
        SelectTargetUx8H((int)ModuleTargetKind.Shipping);
        var current = ReadDraftUx8H();
        var preview = current is null ? null : _session.PreviewDistributionUx8H(current with { Approved = true });
        _targetChoice.Select(originalIndex);
        SelectTargetUx8H(originalIndex);
        _evidenceResourcesReleased = true;
        var sourceUnchanged = ReferenceEquals(sourceProject, _session.Project) &&
                              ReferenceEquals(sourceHistory, _session.History) &&
                              _session.History?.State.Revision == sourceRevision &&
                              (_session.History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                              _session.IsDirty == sourceDirty;
        _session.RestoreDistributionApprovalUx8H(sourceApproval);
        return preview is { Succeeded: true, CanonicalDryRun: true } && sourceUnchanged && ShowsHumanSurface;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (!Visible) Visible = true;
        UpdateBoundsUx8H();
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        Visible = false;
        CancelArtifactVerificationUx8H();
    }

    public override void _Process(double delta)
    {
        _ = delta;
        if (Visible) UpdateBoundsUx8H();
        if (_artifactTask is not { IsCompleted: true }) return;
        try
        {
            _artifactReport = _artifactTask.GetAwaiter().GetResult();
            _status.Text = _artifactReport.Message;
            _status.Modulate = _artifactReport.Succeeded ? StudioStyle.Success : StudioStyle.Danger;
            if (_artifactReport.Bundles.Count > 0)
                _plan.Text += "\n\nVÉRIFICATION RÉELLE\n" + string.Join('\n', _artifactReport.Bundles.Select(item => "• " + item));
            _evidenceResourcesReleased = _artifactReport.ResourcesReleased;
            StatusChanged?.Invoke(_artifactReport.Message, !_artifactReport.Succeeded);
        }
        finally
        {
            _artifactTask = null;
            _artifactCancellation?.Dispose();
            _artifactCancellation = null;
            UpdateButtonsUx8H();
        }
    }

    private void PopulateModuleChoiceUx8H()
    {
        _updating = true;
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionnez explicitement le module…");
        if (_session.Project is not null)
            _moduleChoice.AddItem($"{_session.Project.Module.DisplayName} · configuration réelle du workspace");
        _moduleChoice.Select(0);
        _updating = false;
    }

    private void SelectModuleUx8H(int index)
    {
        if (_updating) return;
        _explicitSelection = index == 1 && _session.Project is not null;
        _draft = _explicitSelection ? _session.CreateDistributionDraftUx8H() : null;
        _preview = null;
        if (_draft is not null) PopulateDraftUx8H(_draft);
        else ResetPreviewUx8H("Sélectionnez explicitement le module.");
        UpdateButtonsUx8H();
    }

    private void PopulateDraftUx8H(StudioUx8HDistributionDraft draft)
    {
        _updating = true;
        _selectedTarget = 0;
        _targetChoice.Select(0);
        PopulateTargetUx8H(draft.Targets[0]);
        _channel.Select(Array.IndexOf(ChannelValues, draft.Channel));
        foreach (var (platform, check) in _platforms)
            check.ButtonPressed = draft.Platforms.Contains(platform);
        _signingMode.Select(Array.IndexOf(SigningValues, draft.SigningMode));
        _signingProfile.Text = draft.SigningProfile;
        _trustedKeys.Text = string.Join("; ", draft.TrustedKeyFingerprints);
        _revokedKeys.Text = string.Join("; ", draft.RevokedKeyFingerprints);
        _unsignedDevelopment.ButtonPressed = draft.AllowUnsignedDevelopment;
        _deltaEnabled.ButtonPressed = draft.DeltaEnabled;
        var chunkIndex = Array.IndexOf(ChunkValues, draft.DeltaChunkSizeBytes);
        _chunkSize.Select(chunkIndex < 0 ? 0 : chunkIndex);
        _deltaResume.ButtonPressed = draft.DeltaAllowResume;
        _deltaDeduplicate.ButtonPressed = draft.DeltaDeduplicateByHash;
        _deltaRollback.ButtonPressed = draft.DeltaAllowRollback;
        _retainedVersions.Value = draft.DeltaRetainedVersions;
        _windowsMode.Select(Array.IndexOf(WindowsValues, draft.WindowsMode));
        _detectPrerequisites.ButtonPressed = draft.DetectWindowsPrerequisites;
        _installLog.ButtonPressed = draft.WriteWindowsInstallLog;
        _uninstaller.ButtonPressed = draft.IncludeWindowsUninstaller;
        _prerequisites.Text = string.Join("; ", draft.WindowsPrerequisites);
        _approved.ButtonPressed = draft.Approved;
        _updating = false;
        ResetPreviewUx8H("Brouillon chargé · lancez l’aperçu sec canonique.");
    }

    private void PopulateTargetUx8H(StudioUx8HTargetDraft target)
    {
        _targetEnabled.ButtonPressed = target.Enabled;
        _editorData.ButtonPressed = target.IncludeEditorData;
        _debugSymbols.ButtonPressed = target.IncludeDebugSymbols;
        _optimize.ButtonPressed = target.OptimizeContent;
        _approvedLicenses.ButtonPressed = target.RequireApprovedLicenses;
        _outputName.Text = target.OutputName;
        _secretsProfile.Text = target.SecretsProfile;
    }

    private void SelectTargetUx8H(int index)
    {
        if (_updating || _draft is null || index < 0 || index >= _draft.Targets.Count) return;
        CaptureTargetUx8H();
        _selectedTarget = index;
        _updating = true;
        PopulateTargetUx8H(_draft.Targets[index]);
        _updating = false;
    }

    private void CaptureTargetUx8H()
    {
        if (_draft is null || _selectedTarget < 0 || _selectedTarget >= _draft.Targets.Count) return;
        var targets = _draft.Targets.ToArray();
        targets[_selectedTarget] = targets[_selectedTarget] with
        {
            Enabled = _targetEnabled.ButtonPressed,
            IncludeEditorData = _editorData.ButtonPressed,
            IncludeDebugSymbols = _debugSymbols.ButtonPressed,
            OptimizeContent = _optimize.ButtonPressed,
            RequireApprovedLicenses = _approvedLicenses.ButtonPressed,
            OutputName = _outputName.Text,
            SecretsProfile = _secretsProfile.Text,
        };
        _draft = _draft with { Targets = targets };
    }

    private StudioUx8HDistributionDraft? ReadDraftUx8H()
    {
        if (_draft is null) return null;
        CaptureTargetUx8H();
        var platforms = _platforms.Where(item => item.Value.ButtonPressed).Select(item => item.Key).ToArray();
        var chunkIndex = _chunkSize.Selected;
        return _draft = _draft with
        {
            Channel = ChannelValues[Math.Clamp(_channel.Selected, 0, ChannelValues.Length - 1)],
            Platforms = platforms,
            SigningMode = SigningValues[Math.Clamp(_signingMode.Selected, 0, SigningValues.Length - 1)],
            SigningProfile = _signingProfile.Text,
            TrustedKeyFingerprints = SplitValuesUx8H(_trustedKeys.Text),
            RevokedKeyFingerprints = SplitValuesUx8H(_revokedKeys.Text),
            AllowUnsignedDevelopment = _unsignedDevelopment.ButtonPressed,
            DeltaEnabled = _deltaEnabled.ButtonPressed,
            DeltaChunkSizeBytes = ChunkValues[Math.Clamp(chunkIndex, 0, ChunkValues.Length - 1)],
            DeltaAllowResume = _deltaResume.ButtonPressed,
            DeltaDeduplicateByHash = _deltaDeduplicate.ButtonPressed,
            DeltaAllowRollback = _deltaRollback.ButtonPressed,
            DeltaRetainedVersions = (int)_retainedVersions.Value,
            WindowsMode = WindowsValues[Math.Clamp(_windowsMode.Selected, 0, WindowsValues.Length - 1)],
            DetectWindowsPrerequisites = _detectPrerequisites.ButtonPressed,
            WriteWindowsInstallLog = _installLog.ButtonPressed,
            IncludeWindowsUninstaller = _uninstaller.ButtonPressed,
            WindowsPrerequisites = SplitValuesUx8H(_prerequisites.Text),
            Approved = _approved.ButtonPressed,
        };
    }

    private void PreviewUx8H()
    {
        var draft = ReadDraftUx8H();
        if (draft is null)
        {
            SetStatusUx8H("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewDistributionUx8H(draft);
        RenderPreviewUx8H(_preview);
        SetStatusUx8H(_preview.Message, !_preview.Succeeded);
        UpdateButtonsUx8H();
    }

    private void RenderPreviewUx8H(StudioUx8HDistributionPreview preview)
    {
        _previewSummary.Text = preview.Message;
        _previewSummary.Modulate = preview.Succeeded ? StudioStyle.Success : StudioStyle.Danger;
        var lines = new List<string>
        {
            "CIBLES",
        };
        lines.AddRange(preview.Targets.Select(item =>
            $"• {item.Target} · {(item.Enabled ? "active" : "désactivée")} · {item.Output} · {item.Authoring} · {item.Optimization} · {item.LicenseGate}"));
        lines.Add(string.Empty);
        lines.Add("LIVRAISON");
        lines.AddRange(preview.ReleasePlan.Select(item => "• " + item));
        lines.Add(string.Empty);
        lines.Add("SIGNATURES");
        lines.AddRange(preview.SigningPlan.Select(item => "• " + item));
        lines.Add(string.Empty);
        lines.Add("DELTA");
        lines.AddRange(preview.DeltaPlan.Select(item => "• " + item));
        lines.Add(string.Empty);
        lines.Add("WINDOWS");
        lines.AddRange(preview.WindowsPlan.Select(item => "• " + item));
        lines.Add(string.Empty);
        lines.Add("COMMANDE");
        lines.Add("• " + preview.Patch);
        lines.Add("• Inverse · " + preview.Inverse);
        _plan.Text = string.Join('\n', lines);
        _diagnostics.Text = preview.Diagnostics.Count == 0
            ? "Aucun diagnostic · la commande reste la seule autorité d’application."
            : string.Join('\n', preview.Diagnostics.Select(item =>
                $"{item.Code}\nChemin · {item.Path}\n{item.Message}"));
        _diagnostics.Modulate = preview.Diagnostics.Count == 0 ? StudioStyle.Muted : StudioStyle.Danger;
    }

    private void ApplyUx8H()
    {
        var draft = ReadDraftUx8H();
        if (draft is null) return;
        var result = _session.ApplyApprovedDistributionUx8H(draft);
        SetStatusUx8H(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        _draft = _session.CreateDistributionDraftUx8H();
        _preview = null;
        if (_draft is not null) PopulateDraftUx8H(_draft);
        UpdateButtonsUx8H();
    }

    private void UndoUx8H() => ApplyHistoryResultUx8H(_session.UndoDistributionUx8H());
    private void RedoUx8H() => ApplyHistoryResultUx8H(_session.RedoDistributionUx8H());
    private void SaveUx8H() => ApplyHistoryResultUx8H(_session.SaveDistributionUx8H());

    private void ApplyHistoryResultUx8H(StudioUx8HActionResult result)
    {
        SetStatusUx8H(result.Message, !result.Succeeded);
        if (result.Succeeded && _session.Project is not null)
        {
            _draft = _session.CreateDistributionDraftUx8H();
            _preview = null;
            if (_draft is not null) PopulateDraftUx8H(_draft);
        }
        UpdateButtonsUx8H();
    }

    private void StartArtifactVerificationUx8H()
    {
        if (_artifactTask is not null || _session.Project is null) return;
        _artifactCancellation = new CancellationTokenSource();
        var token = _artifactCancellation.Token;
        _artifactReport = null;
        _evidenceResourcesReleased = false;
        _status.Text = "Build du module et vérification des bundles dans une copie temporaire…";
        _status.Modulate = StudioStyle.Warning;
        _artifactTask = Task.Run(() => _session.RunReleaseArtifactVerificationUx8H(token), token);
        UpdateButtonsUx8H();
    }

    private void CancelArtifactVerificationUx8H()
    {
        _artifactCancellation?.Cancel();
        if (_artifactTask is null)
        {
            _artifactCancellation?.Dispose();
            _artifactCancellation = null;
        }
        UpdateButtonsUx8H();
    }

    private void ResetUx8H()
    {
        _draft = null;
        _preview = null;
        _artifactReport = null;
        _explicitSelection = false;
        _navigationReady = false;
        _evidenceResourcesReleased = true;
        _selectedTarget = 0;
        ResetPreviewUx8H("Sélectionnez explicitement le module.");
        UpdateButtonsUx8H();
    }

    private void ResetPreviewUx8H(string message)
    {
        if (_previewSummary is null) return;
        _previewSummary.Text = message;
        _previewSummary.Modulate = StudioStyle.Text;
        _plan.Text = "Aucun plan.";
        _diagnostics.Text = "Aucun diagnostic.";
        _diagnostics.Modulate = StudioStyle.Muted;
    }

    private void UpdateButtonsUx8H()
    {
        if (_previewButton is null) return;
        var busy = _artifactTask is not null;
        var project = _session.Project is not null && _explicitSelection;
        _previewButton.Disabled = !project || busy;
        _applyButton.Disabled = !project || busy || _preview is not { CanApply: true };
        _undoButton.Disabled = busy || _session.History?.CanUndo != true;
        _redoButton.Disabled = busy || _session.History?.CanRedo != true;
        _saveButton.Disabled = busy || _session.Project is null;
        _verifyButton.Disabled = busy || _session.Project is null;
        _cancelVerification.Disabled = !busy;
    }

    private void SetStatusUx8H(string message, bool error)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateBoundsUx8H()
    {
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(900, size.X - 48), Math.Max(760, size.Y - 36));
    }

    private static LineEdit AddFieldUx8H(GridContainer grid, string label)
    {
        grid.AddChild(FieldLabelUx8H(label));
        var field = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(field);
        grid.AddChild(field);
        return field;
    }

    private static CheckBox AddCheckUx8H(GridContainer grid, string label)
    {
        grid.AddChild(FieldLabelUx8H(label));
        var field = new CheckBox { Text = "Oui" };
        grid.AddChild(field);
        return field;
    }

    private static Label FieldLabelUx8H(string text)
    {
        var label = StudioStyle.Label(text, 10, StudioStyle.Muted, wrap: true);
        label.CustomMinimumSize = new Vector2(220, 0);
        return label;
    }

    private static Label SectionLabelUx8H(string text) =>
        StudioStyle.Label(text, 10, StudioStyle.Accent, wrap: true);

    private static Button ActionButtonUx8H(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static IReadOnlyList<string> SplitValuesUx8H(string value) =>
        value.Split([';', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IEnumerable<string> DescendantTextUx8H(Node node)
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
            foreach (var text in DescendantTextUx8H(child))
                yield return text;
    }

    private static bool ContainsUuidUx8H(string text) => Regex.IsMatch(text,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        RegexOptions.CultureInvariant);

    private static string ScrubMarkerUx8H(string? value) => Regex.Replace(
        (value ?? "none").Replace(' ', '_').Replace('\r', '_').Replace('\n', '_'),
        "[^A-Za-z0-9À-ÿ_.-]", "_", RegexOptions.CultureInvariant);
}
