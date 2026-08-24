using System.Text.RegularExpressions;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioSecurityWorkersWorkbenchUx8J : ColorRect
{
    private readonly StudioSessionController _session;
    private readonly Dictionary<AssetExternalOperation, CheckBox> _operationChecks = [];
    private StudioUx8JWorkerDraft? _workerDraft;
    private StudioUx8JInjectionDraft? _injectionDraft;
    private StudioUx8JSecurityPreview? _workerPreview;
    private StudioUx8JSecurityPreview? _injectionPreview;
    private bool _explicitSelection;
    private bool _updating;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private SpinBox _workerSeconds = null!;
    private SpinBox _workerOutputMiB = null!;
    private OptionButton _workerPermission = null!;
    private CheckBox _workerConsent = null!;
    private LineEdit _untrustedCharacters = null!;
    private TextEdit _patterns = null!;
    private CheckBox _injectionConsent = null!;
    private Label _workerReport = null!;
    private Label _workerDiagnostics = null!;
    private Label _injectionReport = null!;
    private Label _injectionDiagnostics = null!;
    private Label _status = null!;
    private Button _workerPreviewButton = null!;
    private Button _workerApplyButton = null!;
    private Button _workerUndoButton = null!;
    private Button _workerRedoButton = null!;
    private Button _workerSaveButton = null!;
    private Button _injectionPreviewButton = null!;
    private Button _injectionApplyButton = null!;
    private Button _injectionUndoButton = null!;
    private Button _injectionRedoButton = null!;
    private Button _injectionSaveButton = null!;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _workerDraft is not null && _injectionDraft is not null;
    internal bool WorkerPreviewReady => _workerPreview is
    { Succeeded: true, CanonicalDryRun: true, ScopeBounded: true, DenialsVerified: true, AuditReady: true };
    internal bool InjectionPreviewReady => _injectionPreview is
    { Succeeded: true, CanonicalDryRun: true, ScopeBounded: true, DenialsVerified: true, AuditReady: true };
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx8J(this));
            return Visible &&
                   text.Contains("ORF-AI-023", StringComparison.Ordinal) &&
                   text.Contains("ORF-AI-032", StringComparison.Ordinal) &&
                   text.Contains("Python libre", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("prompt injection", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("consentement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("audit", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucune exécution", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx8J(text);
        }
    }

    internal StudioSecurityWorkersWorkbenchUx8J(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx8J();
    }

    internal void Open()
    {
        _session.CancelSecurityApprovalUx8J();
        ResetUx8J();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionnez explicitement le module…");
        if (_session.Project is not null)
            _moduleChoice.AddItem($"{_session.Project.Module.DisplayName} · workspace réel");
        _moduleChoice.Select(0);
        Visible = true;
        UpdateBoundsUx8J();
    }

    internal void Close()
    {
        if (!Visible) return;
        Visible = false;
        _session.CancelSecurityApprovalUx8J();
        ResetUx8J();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        Open();
        if (_moduleChoice.ItemCount < 2) return false;
        _moduleChoice.Select(1);
        SelectModuleUx8J(1);
        if (_workerDraft is null || _injectionDraft is null) return false;
        _workerDraft = _workerDraft with
        {
            MaximumJobSeconds = 240,
            MaximumOutputBytes = 384L * 1024 * 1024,
            AllowedOperations = ExternalWorkerSecurityPolicyValidator.SupportedBlenderOperations.ToArray(),
            Permission = AssistantPermissionLevel.Configure,
            Consent = true,
        };
        _injectionDraft = _injectionDraft with
        {
            MaximumUntrustedCharacters = 32_768,
            SuspiciousPatterns = _injectionDraft.SuspiciousPatterns
                .Concat(["disable safeguards", "read hidden instruction"])
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Consent = true,
        };
        PopulateWorkerUx8J(_workerDraft);
        PopulateInjectionUx8J(_injectionDraft);
        PreviewWorkerUx8J();
        PreviewInjectionUx8J();
        var ready = WorkerPreviewReady && InjectionPreviewReady && ShowsHumanSurface;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_J_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
                 $"worker={WorkerPreviewReady.ToString().ToLowerInvariant()} " +
                 $"injection={InjectionPreviewReady.ToString().ToLowerInvariant()} " +
                 $"human={ShowsHumanSurface.ToString().ToLowerInvariant()}");
        return ready;
    }

    internal bool RunUiEvidence()
    {
        var sourceProject = _session.Project;
        var sourceHistory = _session.History;
        var sourceRevision = sourceHistory?.State.Revision;
        var sourceJournal = sourceHistory?.Journal.ToArray() ?? [];
        var sourceDirty = _session.IsDirty;
        var approval = _session.CaptureSecurityApprovalUx8J();
        var worker = ReadWorkerUx8J();
        var injection = ReadInjectionUx8J();
        if (worker is null || injection is null) return false;
        var workerPreview = _session.PreviewWorkerSecurityUx8J(worker with { Consent = true });
        var injectionPreview = _session.PreviewInjectionSecurityUx8J(injection with { Consent = true });
        _session.RestoreSecurityApprovalUx8J(approval);
        var unchanged = ReferenceEquals(sourceProject, _session.Project) &&
                        ReferenceEquals(sourceHistory, _session.History) &&
                        _session.History?.State.Revision == sourceRevision &&
                        (_session.History?.Journal ?? []).SequenceEqual(sourceJournal) &&
                        _session.IsDirty == sourceDirty;
        return workerPreview.Succeeded && workerPreview.DenialsVerified &&
               injectionPreview.Succeeded && injectionPreview.DenialsVerified && unchanged && ShowsHumanSurface;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        Visible = true;
        UpdateBoundsUx8J();
    }

    internal void HideSourceSurfaceAfterSmoke() => Visible = false;

    public override void _Process(double delta)
    {
        _ = delta;
        if (Visible) UpdateBoundsUx8J();
    }

    private void BuildUiUx8J()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.01f, 0.016f, 0.027f, 0.97f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetProcess(true);

        _dialog = StudioStyle.Panel("Ux8JSecurityWorkersWorkbench", StudioStyle.Surface, 12, 1);
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
        titles.AddChild(StudioStyle.Label("SÉCURITÉ ASSISTANT & WORKERS · UX8-J", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label(
            "Permissions humaines, exécution confinée et données non fiables isolées",
            18, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Aperçu sec et audit canoniques · aucune exécution Python, shell, réseau, outil ou mutation assistant dans Studio",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        header.AddChild(ActionButtonUx8J("Fermer", Close, 82));
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddThemeConstantOverride("separation", 8);
        var selectionLabel = StudioStyle.Label("Module et périmètre", 10, StudioStyle.Muted);
        selectionLabel.CustomMinimumSize = new Vector2(155, 0);
        selection.AddChild(selectionLabel);
        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_moduleChoice);
        _moduleChoice.ItemSelected += index => SelectModuleUx8J((int)index);
        selection.AddChild(_moduleChoice);
        root.AddChild(selection);
        root.AddChild(StudioStyle.Label(
            "ORF-AI-023 · Pas d’exécution Python libre   |   ORF-AI-032 · Protection contre prompt injection",
            9, StudioStyle.Warning, wrap: true));

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 490),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(bodyHost);
        var body = new HSplitContainer { SplitOffsets = [760] };
        bodyHost.AddChild(body);
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildWorkerPanelUx8J(body);
        BuildInjectionPanelUx8J(body);

        var footer = StudioStyle.Panel("Ux8JStickyFooter", StudioStyle.SurfaceRaised, 8, 1);
        root.AddChild(footer);
        var footerMargin = StudioStyle.Margin(9, 6, 9, 6);
        footer.AddChild(footerMargin);
        var footerColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footerColumn.AddThemeConstantOverride("separation", 4);
        footerMargin.AddChild(footerColumn);
        var workerActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        workerActions.AddThemeConstantOverride("separation", 6);
        var workerActionLabel = StudioStyle.Label("Worker", 9, StudioStyle.Accent);
        workerActionLabel.CustomMinimumSize = new Vector2(90, 0);
        workerActions.AddChild(workerActionLabel);
        _workerPreviewButton = ActionButtonUx8J("Prévisualiser", PreviewWorkerUx8J, 118, primary: true);
        _workerApplyButton = ActionButtonUx8J("Appliquer", ApplyWorkerUx8J, 98);
        _workerUndoButton = ActionButtonUx8J("Annuler", UndoWorkerUx8J, 92);
        _workerRedoButton = ActionButtonUx8J("Rétablir", RedoWorkerUx8J, 92);
        _workerSaveButton = ActionButtonUx8J("Enregistrer", SaveWorkerUx8J, 108);
        foreach (var button in new[] { _workerPreviewButton, _workerApplyButton, _workerUndoButton, _workerRedoButton, _workerSaveButton })
            workerActions.AddChild(button);
        footerColumn.AddChild(workerActions);
        var injectionActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        injectionActions.AddThemeConstantOverride("separation", 6);
        var injectionActionLabel = StudioStyle.Label("Assistant", 9, StudioStyle.Accent);
        injectionActionLabel.CustomMinimumSize = new Vector2(90, 0);
        injectionActions.AddChild(injectionActionLabel);
        _injectionPreviewButton = ActionButtonUx8J("Tester l’injection", PreviewInjectionUx8J, 140, primary: true);
        _injectionApplyButton = ActionButtonUx8J("Appliquer", ApplyInjectionUx8J, 98);
        _injectionUndoButton = ActionButtonUx8J("Annuler", UndoInjectionUx8J, 92);
        _injectionRedoButton = ActionButtonUx8J("Rétablir", RedoInjectionUx8J, 92);
        _injectionSaveButton = ActionButtonUx8J("Enregistrer", SaveInjectionUx8J, 108);
        foreach (var button in new[] { _injectionPreviewButton, _injectionApplyButton, _injectionUndoButton, _injectionRedoButton, _injectionSaveButton })
            injectionActions.AddChild(button);
        footerColumn.AddChild(injectionActions);
        _status = StudioStyle.Label("Sélectionnez explicitement le module.", 9, StudioStyle.Muted, wrap: true);
        _status.CustomMinimumSize = new Vector2(0, 22);
        footerColumn.AddChild(_status);
        UpdateButtonsUx8J();
    }

    private void BuildWorkerPanelUx8J(HSplitContainer body)
    {
        var panel = StudioStyle.Panel("Ux8JWorkerPanel", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(640, 0);
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
        column.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(column);
        column.AddChild(SectionUx8J("WORKER EXTERNE CONFINÉ"));
        column.AddChild(StudioStyle.Label(
            "Python libre, shell, eval et réseau sont verrouillés sur REFUSÉ. Les chemins restent contenus dans le workspace.",
            9, StudioStyle.Warning, wrap: true));
        foreach (var label in new[]
                 {
                     "✓ Python libre refusé", "✓ Shell refusé", "✓ Eval refusé",
                     "✓ Réseau refusé", "✓ Chemins confinés",
                 })
        {
            var locked = new CheckBox { Text = label, ButtonPressed = true, Disabled = true };
            column.AddChild(locked);
        }
        var limits = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        limits.AddThemeConstantOverride("h_separation", 9);
        limits.AddThemeConstantOverride("v_separation", 6);
        limits.AddChild(FieldLabelUx8J("Durée maximale (secondes)"));
        _workerSeconds = ExactSpinUx8J(5, 900);
        limits.AddChild(_workerSeconds);
        limits.AddChild(FieldLabelUx8J("Sortie maximale (Mio)"));
        _workerOutputMiB = ExactSpinUx8J(1, 2048);
        limits.AddChild(_workerOutputMiB);
        limits.AddChild(FieldLabelUx8J("Permission humaine requise"));
        _workerPermission = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_workerPermission);
        _workerPermission.AddItem("Configurer");
        _workerPermission.AddItem("Appliquer un plan approuvé");
        _workerPermission.AddItem("Autonomie bornée");
        limits.AddChild(_workerPermission);
        column.AddChild(limits);
        column.AddChild(SectionUx8J("LISTE BLANCHE BLENDER TYPÉE"));
        var operations = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        operations.AddThemeConstantOverride("h_separation", 10);
        operations.AddThemeConstantOverride("v_separation", 3);
        foreach (var operation in ExternalWorkerSecurityPolicyValidator.SupportedBlenderOperations)
        {
            var check = new CheckBox { Text = HumanOperationUx8J(operation) };
            if (operation is AssetExternalOperation.ValidateModel or AssetExternalOperation.ExportGlb)
                check.Disabled = true;
            _operationChecks[operation] = check;
            operations.AddChild(check);
        }
        column.AddChild(operations);
        _workerConsent = new CheckBox { Text = "Je donne mon consentement explicite pour cette politique worker." };
        column.AddChild(_workerConsent);
        column.AddChild(SectionUx8J("APERÇU SEC & AUDIT"));
        _workerReport = ReportUx8J("Aucun aperçu worker.", 76);
        _workerDiagnostics = ReportUx8J("Aucun diagnostic.", 64);
        column.AddChild(_workerReport);
        column.AddChild(_workerDiagnostics);
    }

    private void BuildInjectionPanelUx8J(HSplitContainer body)
    {
        var panel = StudioStyle.Panel("Ux8JInjectionPanel", StudioStyle.SurfaceRaised, 8, 1);
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
        column.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(column);
        column.AddChild(SectionUx8J("SONDE DE PROMPT INJECTION"));
        column.AddChild(StudioStyle.Label(
            "Le contenu projet est étiqueté comme donnée non fiable. La sonde tente un outil shell et une élévation de permission : les deux doivent être refusés.",
            9, StudioStyle.Warning, wrap: true));
        foreach (var label in new[]
                 {
                     "✓ Données séparées des instructions", "✓ Provenance obligatoire",
                     "✓ Outils limités à la liste blanche", "✓ Escalade cachée interdite",
                     "✓ Confirmation du risque obligatoire",
                 })
        {
            var locked = new CheckBox { Text = label, ButtonPressed = true, Disabled = true };
            column.AddChild(locked);
        }
        column.AddChild(FieldLabelUx8J("Caractères non fiables maximaux (1024–1048576)"));
        _untrustedCharacters = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(_untrustedCharacters);
        column.AddChild(_untrustedCharacters);
        column.AddChild(FieldLabelUx8J("Signatures hostiles · une par ligne"));
        _patterns = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 118),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        column.AddChild(_patterns);
        _injectionConsent = new CheckBox { Text = "Je donne mon consentement explicite pour cette politique anti-injection." };
        column.AddChild(_injectionConsent);
        column.AddChild(SectionUx8J("RÉSULTAT DE LA SONDE & AUDIT"));
        _injectionReport = ReportUx8J("Aucune sonde hostile exécutée.", 76);
        _injectionDiagnostics = ReportUx8J("Aucun diagnostic.", 76);
        column.AddChild(_injectionReport);
        column.AddChild(_injectionDiagnostics);
    }

    private void SelectModuleUx8J(int index)
    {
        if (_updating) return;
        _explicitSelection = index == 1 && _session.Project is not null;
        _workerDraft = _explicitSelection ? _session.CurrentWorkerSecurityDraftUx8J() : null;
        _injectionDraft = _explicitSelection ? _session.CurrentInjectionSecurityDraftUx8J() : null;
        _workerPreview = null;
        _injectionPreview = null;
        if (_workerDraft is not null) PopulateWorkerUx8J(_workerDraft);
        if (_injectionDraft is not null) PopulateInjectionUx8J(_injectionDraft);
        SetStatusUx8J(_explicitSelection ? "Politiques chargées · lancez les deux aperçus secs." : "Sélectionnez explicitement le module.", false);
        UpdateButtonsUx8J();
    }

    private void PopulateWorkerUx8J(StudioUx8JWorkerDraft draft)
    {
        _updating = true;
        _workerSeconds.Value = draft.MaximumJobSeconds;
        _workerOutputMiB.Value = draft.MaximumOutputBytes / (1024d * 1024);
        _workerPermission.Select(draft.Permission switch
        {
            AssistantPermissionLevel.ApplyApprovedPlan => 1,
            AssistantPermissionLevel.BoundedAutonomous => 2,
            _ => 0,
        });
        foreach (var item in _operationChecks)
            item.Value.ButtonPressed = draft.AllowedOperations.Contains(item.Key);
        _workerConsent.ButtonPressed = draft.Consent;
        _workerReport.Text = "Brouillon chargé · aucune exécution lancée.";
        _workerDiagnostics.Text = "Aucun diagnostic.";
        _updating = false;
    }

    private void PopulateInjectionUx8J(StudioUx8JInjectionDraft draft)
    {
        _updating = true;
        _untrustedCharacters.Text = draft.MaximumUntrustedCharacters.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _patterns.Text = string.Join('\n', draft.SuspiciousPatterns);
        _injectionConsent.ButtonPressed = draft.Consent;
        _injectionReport.Text = "Brouillon chargé · aucune requête envoyée à un fournisseur.";
        _injectionDiagnostics.Text = "Aucun diagnostic.";
        _updating = false;
    }

    private StudioUx8JWorkerDraft? ReadWorkerUx8J()
    {
        if (_workerDraft is null) return null;
        var operations = _operationChecks.Where(item => item.Value.ButtonPressed).Select(item => item.Key).Order().ToArray();
        return _workerDraft = _workerDraft with
        {
            MaximumJobSeconds = (int)_workerSeconds.Value,
            MaximumOutputBytes = checked((long)_workerOutputMiB.Value * 1024 * 1024),
            AllowedOperations = operations,
            Permission = _workerPermission.Selected switch
            {
                1 => AssistantPermissionLevel.ApplyApprovedPlan,
                2 => AssistantPermissionLevel.BoundedAutonomous,
                _ => AssistantPermissionLevel.Configure,
            },
            Consent = _workerConsent.ButtonPressed,
        };
    }

    private StudioUx8JInjectionDraft? ReadInjectionUx8J()
    {
        if (_injectionDraft is null) return null;
        if (!int.TryParse(_untrustedCharacters.Text.Trim(), out var maximum)) maximum = 0;
        var patterns = _patterns.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();
        return _injectionDraft = _injectionDraft with
        {
            MaximumUntrustedCharacters = maximum,
            SuspiciousPatterns = patterns,
            Consent = _injectionConsent.ButtonPressed,
        };
    }

    private void PreviewWorkerUx8J()
    {
        var draft = ReadWorkerUx8J();
        if (draft is null) return;
        _workerPreview = _session.PreviewWorkerSecurityUx8J(draft);
        RenderPreviewUx8J(_workerPreview, _workerReport, _workerDiagnostics);
        SetStatusUx8J(_workerPreview.Message, !_workerPreview.Succeeded);
        UpdateButtonsUx8J();
    }

    private void PreviewInjectionUx8J()
    {
        var draft = ReadInjectionUx8J();
        if (draft is null) return;
        _injectionPreview = _session.PreviewInjectionSecurityUx8J(draft);
        RenderPreviewUx8J(_injectionPreview, _injectionReport, _injectionDiagnostics);
        SetStatusUx8J(_injectionPreview.Message, !_injectionPreview.Succeeded);
        UpdateButtonsUx8J();
    }

    private void ApplyWorkerUx8J()
    {
        var draft = ReadWorkerUx8J();
        if (draft is null) return;
        var result = _session.ApplyApprovedWorkerSecurityUx8J(draft);
        SetStatusUx8J(result.Message, !result.Succeeded);
        if (result.Succeeded) ReloadWorkerUx8J();
        UpdateButtonsUx8J();
    }

    private void ApplyInjectionUx8J()
    {
        var draft = ReadInjectionUx8J();
        if (draft is null) return;
        var result = _session.ApplyApprovedInjectionSecurityUx8J(draft);
        SetStatusUx8J(result.Message, !result.Succeeded);
        if (result.Succeeded) ReloadInjectionUx8J();
        UpdateButtonsUx8J();
    }

    private void UndoWorkerUx8J() { var result = _session.UndoWorkerSecurityUx8J(); SetStatusUx8J(result.Message, !result.Succeeded); if (result.Succeeded) ReloadWorkerUx8J(); UpdateButtonsUx8J(); }
    private void RedoWorkerUx8J() { var result = _session.RedoWorkerSecurityUx8J(); SetStatusUx8J(result.Message, !result.Succeeded); if (result.Succeeded) ReloadWorkerUx8J(); UpdateButtonsUx8J(); }
    private void SaveWorkerUx8J() { var result = _session.SaveWorkerSecurityUx8J(); SetStatusUx8J(result.Message, !result.Succeeded); UpdateButtonsUx8J(); }
    private void UndoInjectionUx8J() { var result = _session.UndoInjectionSecurityUx8J(); SetStatusUx8J(result.Message, !result.Succeeded); if (result.Succeeded) ReloadInjectionUx8J(); UpdateButtonsUx8J(); }
    private void RedoInjectionUx8J() { var result = _session.RedoInjectionSecurityUx8J(); SetStatusUx8J(result.Message, !result.Succeeded); if (result.Succeeded) ReloadInjectionUx8J(); UpdateButtonsUx8J(); }
    private void SaveInjectionUx8J() { var result = _session.SaveInjectionSecurityUx8J(); SetStatusUx8J(result.Message, !result.Succeeded); UpdateButtonsUx8J(); }

    private void ReloadWorkerUx8J()
    {
        _workerDraft = _session.CurrentWorkerSecurityDraftUx8J();
        _workerPreview = null;
        if (_workerDraft is not null) PopulateWorkerUx8J(_workerDraft);
    }

    private void ReloadInjectionUx8J()
    {
        _injectionDraft = _session.CurrentInjectionSecurityDraftUx8J();
        _injectionPreview = null;
        if (_injectionDraft is not null) PopulateInjectionUx8J(_injectionDraft);
    }

    private void ResetUx8J()
    {
        _workerDraft = null;
        _injectionDraft = null;
        _workerPreview = null;
        _injectionPreview = null;
        _explicitSelection = false;
        if (_workerReport is not null) _workerReport.Text = "Aucun aperçu worker.";
        if (_workerDiagnostics is not null) _workerDiagnostics.Text = "Aucun diagnostic.";
        if (_injectionReport is not null) _injectionReport.Text = "Aucune sonde hostile exécutée.";
        if (_injectionDiagnostics is not null) _injectionDiagnostics.Text = "Aucun diagnostic.";
        UpdateButtonsUx8J();
    }

    private void UpdateButtonsUx8J()
    {
        if (_workerPreviewButton is null) return;
        _workerPreviewButton.Disabled = _workerDraft is null;
        _workerApplyButton.Disabled = _workerPreview?.CanApply != true;
        _workerUndoButton.Disabled = !_session.CanUndoWorkerSecurityUx8J;
        _workerRedoButton.Disabled = !_session.CanRedoWorkerSecurityUx8J;
        _workerSaveButton.Disabled = _workerDraft is null;
        _injectionPreviewButton.Disabled = _injectionDraft is null;
        _injectionApplyButton.Disabled = _injectionPreview?.CanApply != true;
        _injectionUndoButton.Disabled = !_session.CanUndoInjectionSecurityUx8J;
        _injectionRedoButton.Disabled = !_session.CanRedoInjectionSecurityUx8J;
        _injectionSaveButton.Disabled = _injectionDraft is null;
    }

    private void SetStatusUx8J(string message, bool error)
    {
        if (_status is null) return;
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateBoundsUx8J()
    {
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(1040, size.X - 48), Math.Max(760, size.Y - 36));
    }

    private static void RenderPreviewUx8J(StudioUx8JSecurityPreview preview, Label report, Label diagnostics)
    {
        report.Text = string.Join('\n', preview.SemanticDiff.Select(item => "• " + ScrubHumanUx8J(item)));
        diagnostics.Text = preview.Diagnostics.Count == 0
            ? "Aucun diagnostic bloquant · audit prêt."
            : string.Join('\n', preview.Diagnostics.Select(item => "• " + ScrubHumanUx8J(item)));
    }

    private static SpinBox ExactSpinUx8J(double minimum, double maximum)
    {
        var spin = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Step = 1,
            AllowGreater = false,
            AllowLesser = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StudioStyle.ApplySpinBox(spin);
        return spin;
    }

    private static Label FieldLabelUx8J(string text)
    {
        var label = StudioStyle.Label(text, 9, StudioStyle.Muted, wrap: true);
        label.CustomMinimumSize = new Vector2(205, 0);
        return label;
    }

    private static Label SectionUx8J(string text) => StudioStyle.Label(text, 10, StudioStyle.Accent, wrap: true);

    private static Label ReportUx8J(string text, float height)
    {
        var label = StudioStyle.Label(text, 9, StudioStyle.Text, wrap: true);
        label.CustomMinimumSize = new Vector2(0, height);
        return label;
    }

    private static Button ActionButtonUx8J(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 32);
        button.Pressed += action;
        return button;
    }

    private static string HumanOperationUx8J(AssetExternalOperation operation) => operation switch
    {
        AssetExternalOperation.ValidateModel => "Valider le modèle · requis",
        AssetExternalOperation.NormalizeModel => "Normaliser le modèle",
        AssetExternalOperation.GenerateCollision => "Générer les collisions",
        AssetExternalOperation.GenerateLods => "Générer les LOD",
        AssetExternalOperation.ExportGlb => "Exporter en GLB · requis",
        AssetExternalOperation.GenerateThumbnail => "Générer la vignette",
        AssetExternalOperation.AuditQuality => "Auditer la qualité",
        AssetExternalOperation.CreatePrimitives => "Créer des primitives bornées",
        _ => "Opération protégée",
    };

    private static IEnumerable<string> DescendantTextUx8J(Node node)
    {
        switch (node)
        {
            case OptionButton option:
                for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                break;
            case Label label: yield return label.Text; break;
            case Button button: yield return button.Text; break;
            case LineEdit line: yield return line.Text; break;
            case TextEdit text: yield return text.Text; break;
        }
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx8J(child))
                yield return value;
    }

    private static bool ContainsUuidUx8J(string text) => Regex.IsMatch(text,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        RegexOptions.CultureInvariant);

    private static string ScrubHumanUx8J(string value) => Regex.Replace(value ?? string.Empty,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        "identifiant protégé", RegexOptions.CultureInvariant);
}
