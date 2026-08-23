using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioModernRuntimeWorkbenchUx6T : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx6T? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _updating;
    private bool _explicitSelection;

    private PanelContainer _dialog = null!;
    private OptionButton _profileChoice = null!;
    private TabContainer _tabs = null!;
    private readonly Dictionary<AreaEffectShape, CheckBox> _shapeChecks = [];
    private LineEdit _maxRange = null!;
    private LineEdit _maxHeight = null!;
    private CheckBox _evaluateCover = null!;
    private CheckBox _serverConfirmation = null!;
    private OptionButton _colorMode = null!;
    private LineEdit _validColor = null!;
    private LineEdit _invalidColor = null!;
    private ColorRect _validSwatch = null!;
    private ColorRect _invalidSwatch = null!;
    private OptionButton _previewShape = null!;
    private LineEdit _previewRange = null!;
    private LineEdit _previewRadius = null!;
    private LineEdit _previewWidth = null!;
    private LineEdit _previewAngle = null!;
    private CheckBox _previewConfirmed = null!;
    private LineEdit _toolkitVersion = null!;
    private CheckBox _responsive = null!;
    private CheckBox _forms = null!;
    private CheckBox _lists = null!;
    private CheckBox _canvas = null!;
    private CheckBox _accessibility = null!;
    private CheckBox _localization = null!;
    private CheckBox _developerInspector = null!;
    private VBoxContainer _runtimeResults = null!;
    private Label _review = null!;
    private Label _status = null!;
    private Button _apply = null!;
    private Button _undo = null!;
    private Button _redo = null!;

    private StudioUx6TDraft? _draft;
    private StudioUx6TPreview? _preview;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? ModernToolingChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                            !ReferenceEquals(_session, _sourceSession);
    internal bool HasExplicitSelection => _explicitSelection && _draft is not null;
    internal bool PreviewReady => _preview is
    {
        Succeeded: true,
        CanApply: true,
        CanonicalDryRun: true,
        AreaEffectResolved: true,
        NuiResolved: true,
        RuntimeClaimsBounded: true,
    };
    internal bool ShowsTwoPolicies => _tabs.GetTabCount() == 2 && _draft is not null;
    internal bool EvidenceSourceUntouched => SourceMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return Visible && text.Contains("Indicateur de zone d’effet", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("NUI 2.0", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec canonique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aucun mesh Godot", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioModernRuntimeWorkbenchUx6T(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        BuildUi();
    }

    private void BuildUi()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.015f, 0.02f, 0.035f, 0.92f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _dialog = StudioStyle.Panel("Ux6TModernRuntimeDialog", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        _dialog.PropagateMaximumSize = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(15, 12, 15, 12);
        margin.PropagateMaximumSize = true;
        _dialog.AddChild(margin);
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
        titles.AddChild(StudioStyle.Label("RUNTIME MODERNE · UX6-T", 10, StudioStyle.Accent));
        titles.AddChild(StudioStyle.Label(
            "Indicateur de zone d’effet et capacités NUI 2.0", 18, StudioStyle.Text, wrap: true));
        titles.AddChild(StudioStyle.Label(
            "Aperçu de décision uniquement · aucun mesh Godot, ciblage serveur ou document NUI n’est exécuté",
            9, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var selection = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selection.AddChild(StudioStyle.Label("PROFIL RUNTIME MODERNE DU MODULE", 9, StudioStyle.Accent));
        _profileChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _profileChoice.ItemSelected += index => SelectProfile((int)index);
        selection.AddChild(_profileChoice);
        root.AddChild(selection);

        var tabHost = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 350),
            CustomMaximumSize = new Vector2(-1, 640),
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabHost);
        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        tabHost.AddChild(_tabs);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildAreaEffectTab();
        BuildNuiTab();
        WireDraftInvalidation();

        var runtimePanel = StudioStyle.Panel("Ux6TModernRuntimePreview", StudioStyle.SurfaceRaised, 8, 1);
        runtimePanel.CustomMinimumSize = new Vector2(0, 112);
        var runtimeMargin = StudioStyle.Margin(9, 7, 9, 7);
        runtimePanel.AddChild(runtimeMargin);
        _runtimeResults = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _runtimeResults.AddChild(StudioStyle.Label("APERÇU RUNTIME · aucun aperçu sec canonique", 9,
            StudioStyle.Accent));
        runtimeMargin.AddChild(_runtimeResults);
        root.AddChild(runtimePanel);
        _review = StudioStyle.Label("Aucune modification approuvée.", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_review);
        _status = StudioStyle.Label("Sélectionnez explicitement le profil runtime moderne.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 8);
        actions.AddChild(ActionButton("Aperçu sec canonique", Preview, 172));
        _apply = ActionButton("Appliquer", Apply, 120, primary: true);
        _undo = ActionButton("Annuler", Undo, 110);
        _redo = ActionButton("Rétablir", Redo, 116);
        actions.AddChild(_apply);
        actions.AddChild(_undo);
        actions.AddChild(_redo);
        actions.AddChild(ActionButton("Enregistrer", Save, 126));
        root.AddChild(actions);
        UpdateButtons();
    }

    private void BuildAreaEffectTab()
    {
        var page = TabPage("Zone d’effet");
        page.AddChild(StudioStyle.Label("ORF-EE-022 · Indicateur de zone d’effet", 10,
            StudioStyle.Accent, wrap: true));
        page.AddChild(StudioStyle.Label(
            "Le service canonique filtre formes, portée, hauteur, couverture et confirmation serveur sur des cibles temporaires en mémoire.",
            9, StudioStyle.Muted, wrap: true));

        var shapeRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        shapeRow.AddChild(StudioStyle.Label("FORMES AUTORISÉES", 8, StudioStyle.Muted));
        foreach (var shape in Enum.GetValues<AreaEffectShape>())
        {
            var check = new CheckBox { Text = HumanShape(shape) };
            _shapeChecks[shape] = check;
            shapeRow.AddChild(check);
        }
        page.AddChild(shapeRow);

        var bounds = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _maxRange = Field("60");
        _maxHeight = Field("12");
        _colorMode = EnumChoice(Enum.GetValues<TargetIndicatorColorMode>().Select(HumanColorMode));
        bounds.AddChild(LabeledColumn("Portée maximale · m", _maxRange));
        bounds.AddChild(LabeledColumn("Écart hauteur · m", _maxHeight));
        bounds.AddChild(LabeledColumn("Palette accessible", _colorMode));
        page.AddChild(bounds);

        var gates = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _evaluateCover = new CheckBox { Text = "Évaluer la couverture" };
        _serverConfirmation = new CheckBox { Text = "Exiger la confirmation serveur" };
        gates.AddChild(_evaluateCover);
        gates.AddChild(_serverConfirmation);
        page.AddChild(gates);

        var colors = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _validColor = Field("4dd6a8a0");
        _invalidColor = Field("ff8a5ba0");
        _validSwatch = Swatch();
        _invalidSwatch = Swatch();
        colors.AddChild(LabeledColor("Couleur valide · RGB/RGBA", _validColor, _validSwatch));
        colors.AddChild(LabeledColor("Couleur refusée · RGB/RGBA", _invalidColor, _invalidSwatch));
        page.AddChild(colors);

        page.AddChild(StudioStyle.Label("APERÇU GÉOMÉTRIQUE EN MÉMOIRE", 8, StudioStyle.Muted));
        var preview = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _previewShape = EnumChoice(Enum.GetValues<AreaEffectShape>().Select(HumanShape));
        _previewRange = Field("12");
        _previewRadius = Field("6");
        _previewWidth = Field("4");
        _previewAngle = Field("90");
        preview.AddChild(LabeledColumn("Forme", _previewShape));
        preview.AddChild(LabeledColumn("Portée", _previewRange));
        preview.AddChild(LabeledColumn("Rayon", _previewRadius));
        preview.AddChild(LabeledColumn("Largeur", _previewWidth));
        preview.AddChild(LabeledColumn("Angle · degrés", _previewAngle));
        page.AddChild(preview);
        _previewConfirmed = new CheckBox { Text = "Simuler une confirmation serveur reçue" };
        page.AddChild(_previewConfirmed);
        page.AddChild(StudioStyle.Label(
            "Aucun mesh, décalque, raycast, prédiction réseau ou cible du module n’est créé par cet aperçu.",
            10, StudioStyle.Warning, wrap: true));
    }

    private void BuildNuiTab()
    {
        var page = TabPage("NUI 2.0");
        page.AddChild(StudioStyle.Label("ORF-EE-024 · Améliorations NUI", 10,
            StudioStyle.Accent, wrap: true));
        page.AddChild(StudioStyle.Label(
            "Le profil déclare les capacités du toolkit runtime. Les documents, widgets, bindings et événements existants restent inchangés.",
            9, StudioStyle.Muted, wrap: true));
        _toolkitVersion = Field("2.0");
        _toolkitVersion.Editable = false;
        AddLabeled(page, "Version protégée du toolkit", _toolkitVersion);

        var first = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _responsive = new CheckBox { Text = "Layout responsive" };
        _forms = new CheckBox { Text = "Formulaires" };
        _lists = new CheckBox { Text = "Listes" };
        _canvas = new CheckBox { Text = "Canvas" };
        first.AddChild(_responsive);
        first.AddChild(_forms);
        first.AddChild(_lists);
        first.AddChild(_canvas);
        page.AddChild(first);

        var second = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _accessibility = new CheckBox { Text = "Métadonnées d’accessibilité" };
        _localization = new CheckBox { Text = "Résolution de localisation" };
        _developerInspector = new CheckBox { Text = "Inspecteur développeur en lecture seule" };
        second.AddChild(_accessibility);
        second.AddChild(_localization);
        second.AddChild(_developerInspector);
        page.AddChild(second);

        var capabilities = StudioStyle.Panel("Ux6TNuiCapabilities", StudioStyle.Surface, 8, 1);
        var capMargin = StudioStyle.Margin(10, 9, 10, 9);
        capabilities.AddChild(capMargin);
        var capText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        capText.AddChild(StudioStyle.Label("PLAN DE CAPACITÉS", 9, StudioStyle.Accent));
        capText.AddChild(StudioStyle.Label(
            "Responsive · formulaires · listes · canvas · checkbox/slider · accessibilité · localisation · inspecteur",
            11, StudioStyle.Text, wrap: true));
        capText.AddChild(StudioStyle.Label(
            "L’aperçu compte les documents et widgets existants sans instancier RuntimeDeclarativeUi.",
            9, StudioStyle.Muted, wrap: true));
        capMargin.AddChild(capText);
        page.AddChild(capabilities);
        page.AddChild(StudioStyle.Label(
            "Aucun Control Godot, événement GUI, binding JSON ou écran joueur n’est exécuté dans Studio.",
            10, StudioStyle.Warning, wrap: true));
    }

    internal void Open() => OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CancelModernToolingApprovalUx6T();
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        ResetControls();
    }

    internal void CloseEvidence()
    {
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        ResetControls(keepReleaseState: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        if (_sourceSession.Project is null || _sourceSession.History is null) return false;
        if (_evidenceSession is not null) ReleaseEvidence();
        _sourceSnapshot = CaptureSource();
        _evidenceRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-t-ui-{Guid.NewGuid():N}");
        _evidenceReleased = false;
        try
        {
            var project = StudioSessionController.CreateEvidenceProjectUx6T(_sourceSession.Project);
            if (!ProjectSession.Create(_evidenceRoot, project).Succeeded)
            {
                ReleaseEvidence();
                return false;
            }
            _evidenceSession = new StudioSessionController();
            if (!_evidenceSession.Open(_evidenceRoot).Succeeded)
            {
                ReleaseEvidence();
                return false;
            }
            OpenBound(_evidenceSession, evidence: true, cancelApproval: false);
            _profileChoice.Select(1);
            SelectProfile(1);
            if (_draft is null)
            {
                ReleaseEvidence();
                return false;
            }
            _draft = StudioSessionController.ChangedModernToolingDraftUx6T(_draft);
            PopulateDraft();
            Preview();
            _tabs.CurrentTab = mode.Contains("nui", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var ready = PreviewReady && ShowsTwoPolicies && ShowsHumanSurface && SourceMatches();
            if (!ready)
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_T_UI_FAILED " +
                            $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                            $"policies={ShowsTwoPolicies.ToString().ToLowerInvariant()} " +
                            $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                            $"source={SourceMatches().ToString().ToLowerInvariant()}");
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_T_EVIDENCE_FAILED type={exception.GetType().Name}");
            ReleaseEvidence();
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var ready = PrepareEvidence("ux6-t-aoe");
        if (!ready || !UsesIsolatedEvidence || !PreviewReady || _evidenceSession is null)
        {
            CloseEvidence();
            return false;
        }
        try
        {
            var isolated = _evidenceSession;
            var before = isolated.Project;
            var applied = isolated.ApplyApprovedModernToolingUx6T();
            var after = isolated.Project;
            var undo = isolated.UndoModernToolingUx6T();
            var undone = ReferenceEquals(before, isolated.Project);
            var redo = isolated.RedoModernToolingUx6T();
            var redone = ReferenceEquals(after, isolated.Project);
            var saved = isolated.SaveModernToolingUx6T();
            return applied.Succeeded && undo.Succeeded && undone && redo.Succeeded && redone &&
                   saved.Succeeded && SourceMatches();
        }
        finally
        {
            CloseEvidence();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && IsInsideTree()) UpdateBounds();
    }

    private void OpenBound(StudioSessionController controller, bool evidence, bool cancelApproval)
    {
        _session = controller;
        if (cancelApproval) _session.CancelModernToolingApprovalUx6T();
        ResetControls(keepReleaseState: evidence);
        RebuildProfileChoices();
        Visible = true;
        UpdateBounds();
    }

    private void RebuildProfileChoices()
    {
        _updating = true;
        _profileChoice.Clear();
        _profileChoice.AddItem("Choisir explicitement le profil runtime moderne…");
        foreach (var choice in _session.ModernToolingChoicesUx6T()) _profileChoice.AddItem(choice.Label);
        _profileChoice.Select(0);
        _updating = false;
    }

    private void SelectProfile(int index)
    {
        if (_updating) return;
        _explicitSelection = index > 0;
        _draft = _session.CreateModernToolingDraftUx6T(_explicitSelection);
        _preview = null;
        if (_draft is null)
        {
            ClearEditors();
            SetStatus("Sélection explicite requise.", error: true);
            return;
        }
        PopulateDraft();
        SetStatus("Profil chargé · préparez un aperçu sec canonique.");
    }

    private void PopulateDraft()
    {
        if (_draft is null) return;
        _updating = true;
        foreach (var (shape, check) in _shapeChecks) check.ButtonPressed = _draft.Shapes.Contains(shape);
        _maxRange.Text = _draft.MaxRange.ToString("R", CultureInfo.InvariantCulture);
        _maxHeight.Text = _draft.MaxHeightDelta.ToString("R", CultureInfo.InvariantCulture);
        _evaluateCover.ButtonPressed = _draft.EvaluateCover;
        _serverConfirmation.ButtonPressed = _draft.RequireServerConfirmation;
        _colorMode.Select((int)_draft.ColorMode);
        _validColor.Text = _draft.ValidColor;
        _invalidColor.Text = _draft.InvalidColor;
        _toolkitVersion.Text = _draft.ToolkitVersion;
        _responsive.ButtonPressed = _draft.ResponsiveLayout;
        _forms.ButtonPressed = _draft.Forms;
        _lists.ButtonPressed = _draft.Lists;
        _canvas.ButtonPressed = _draft.Canvas;
        _accessibility.ButtonPressed = _draft.Accessibility;
        _localization.ButtonPressed = _draft.Localization;
        _developerInspector.ButtonPressed = _draft.DeveloperInspector;
        _previewShape.Select((int)_draft.PreviewShape);
        _previewRange.Text = _draft.PreviewRange.ToString("R", CultureInfo.InvariantCulture);
        _previewRadius.Text = _draft.PreviewRadius.ToString("R", CultureInfo.InvariantCulture);
        _previewWidth.Text = _draft.PreviewWidth.ToString("R", CultureInfo.InvariantCulture);
        _previewAngle.Text = _draft.PreviewAngleDegrees.ToString("R", CultureInfo.InvariantCulture);
        _previewConfirmed.ButtonPressed = _draft.PreviewServerConfirmed;
        UpdateSwatches();
        _updating = false;
        UpdateButtons();
    }

    private bool TryReadDraft(out StudioUx6TDraft? draft, out string error)
    {
        draft = null;
        error = string.Empty;
        if (_draft is null)
        {
            error = "Sélectionnez explicitement le profil runtime moderne.";
            return false;
        }
        if (!float.TryParse(_maxRange.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxRange) ||
            !float.TryParse(_maxHeight.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxHeight) ||
            !float.TryParse(_previewRange.Text, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var previewRange) ||
            !float.TryParse(_previewRadius.Text, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var previewRadius) ||
            !float.TryParse(_previewWidth.Text, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var previewWidth) ||
            !float.TryParse(_previewAngle.Text, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var previewAngle))
        {
            error = "Les portées, dimensions et angles doivent être des nombres exacts.";
            return false;
        }
        var shapes = _shapeChecks.Where(item => item.Value.ButtonPressed).Select(item => item.Key)
            .Order().ToArray();
        draft = _draft with
        {
            Shapes = shapes,
            MaxRange = maxRange,
            MaxHeightDelta = maxHeight,
            EvaluateCover = _evaluateCover.ButtonPressed,
            RequireServerConfirmation = _serverConfirmation.ButtonPressed,
            ColorMode = (TargetIndicatorColorMode)_colorMode.Selected,
            ValidColor = _validColor.Text.Trim().TrimStart('#'),
            InvalidColor = _invalidColor.Text.Trim().TrimStart('#'),
            ToolkitVersion = _toolkitVersion.Text.Trim(),
            ResponsiveLayout = _responsive.ButtonPressed,
            Forms = _forms.ButtonPressed,
            Lists = _lists.ButtonPressed,
            Canvas = _canvas.ButtonPressed,
            Accessibility = _accessibility.ButtonPressed,
            Localization = _localization.ButtonPressed,
            DeveloperInspector = _developerInspector.ButtonPressed,
            PreviewShape = (AreaEffectShape)_previewShape.Selected,
            PreviewRange = previewRange,
            PreviewRadius = previewRadius,
            PreviewWidth = previewWidth,
            PreviewAngleDegrees = previewAngle,
            PreviewServerConfirmed = _previewConfirmed.ButtonPressed,
        };
        return true;
    }

    private void Preview()
    {
        if (!TryReadDraft(out var draft, out var error) || draft is null)
        {
            SetStatus(error, error: true);
            return;
        }
        _draft = draft;
        _preview = _session.PreviewModernToolingUx6T(draft);
        RenderPreview();
        SetStatus(_preview.Message, error: !_preview.Succeeded);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedModernToolingUx6T();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded) return;
        ModernToolingChanged?.Invoke();
        ReloadAfterMutation();
    }

    private void Undo()
    {
        var result = _session.UndoModernToolingUx6T();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            ModernToolingChanged?.Invoke();
            ReloadAfterMutation();
        }
    }

    private void Redo()
    {
        var result = _session.RedoModernToolingUx6T();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            ModernToolingChanged?.Invoke();
            ReloadAfterMutation();
        }
    }

    private void Save()
    {
        var result = _session.SaveModernToolingUx6T();
        SetStatus(result.Message, error: !result.Succeeded);
    }

    private void ReloadAfterMutation()
    {
        _draft = _session.CreateModernToolingDraftUx6T(true);
        _preview = null;
        if (_draft is not null) PopulateDraft();
        ClearPreview();
        UpdateButtons();
    }

    private void RenderPreview()
    {
        ClearChildren(_runtimeResults);
        _runtimeResults.AddChild(StudioStyle.Label("APERÇU RUNTIME · aperçu sec canonique", 9,
            StudioStyle.Accent));
        if (_preview is null) return;
        foreach (var line in _preview.RuntimePreview.Take(5))
            _runtimeResults.AddChild(StudioStyle.Label(line, 9,
                line.StartsWith("Aucun ", StringComparison.Ordinal) ? StudioStyle.Warning : StudioStyle.Text,
                wrap: true));
        if (_preview.Diagnostics.Count > 0)
            foreach (var diagnostic in _preview.Diagnostics)
                _runtimeResults.AddChild(StudioStyle.Label($"{diagnostic.Code} · {diagnostic.Message}", 9,
                    StudioStyle.Danger, wrap: true));
        _review.Text = _preview.Diff.Count == 0
            ? "Aucune différence persistante."
            : "DIFF HUMAIN · " + string.Join(" · ", _preview.Diff);
        UpdateButtons();
    }

    private void ClearPreview()
    {
        ClearChildren(_runtimeResults);
        _runtimeResults.AddChild(StudioStyle.Label("APERÇU RUNTIME · aucun aperçu sec canonique", 9,
            StudioStyle.Accent));
        _review.Text = "Aucune modification approuvée.";
    }

    private void WireDraftInvalidation()
    {
        foreach (var check in _shapeChecks.Values.Concat(new[]
                 {
                     _evaluateCover, _serverConfirmation, _previewConfirmed, _responsive, _forms, _lists,
                     _canvas, _accessibility, _localization, _developerInspector,
                 }))
            check.Toggled += _ => DraftChanged();
        foreach (var field in new[]
                 {
                     _maxRange, _maxHeight, _validColor, _invalidColor, _previewRange, _previewRadius,
                     _previewWidth, _previewAngle,
                 })
            field.TextChanged += _ =>
            {
                UpdateSwatches();
                DraftChanged();
            };
        _colorMode.ItemSelected += _ => DraftChanged();
        _previewShape.ItemSelected += _ => DraftChanged();
    }

    private void DraftChanged()
    {
        if (_updating) return;
        _session.CancelModernToolingApprovalUx6T();
        _preview = null;
        ClearPreview();
        SetStatus("Brouillon modifié · relancez l’aperçu sec canonique.");
        UpdateButtons();
    }

    private void ResetControls(bool keepReleaseState = false)
    {
        _updating = true;
        _explicitSelection = false;
        _draft = null;
        _preview = null;
        if (_profileChoice is not null)
        {
            _profileChoice.Clear();
            _profileChoice.AddItem("Choisir explicitement le profil runtime moderne…");
            _profileChoice.Select(0);
        }
        ClearEditors();
        ClearPreview();
        _status.Text = "Sélectionnez explicitement le profil runtime moderne.";
        if (!keepReleaseState) _evidenceReleased = true;
        _updating = false;
        UpdateButtons();
    }

    private void ClearEditors()
    {
        foreach (var check in _shapeChecks.Values.Concat(new[]
                 {
                     _evaluateCover, _serverConfirmation, _previewConfirmed, _responsive, _forms, _lists,
                     _canvas, _accessibility, _localization, _developerInspector,
                 }))
            if (check is not null) check.ButtonPressed = false;
        foreach (var field in new[]
                 {
                     _maxRange, _maxHeight, _validColor, _invalidColor, _previewRange, _previewRadius,
                     _previewWidth, _previewAngle, _toolkitVersion,
                 })
            if (field is not null) field.Text = string.Empty;
        if (_colorMode is not null) _colorMode.Select(0);
        if (_previewShape is not null) _previewShape.Select(0);
        if (_validSwatch is not null) _validSwatch.Color = StudioStyle.Surface;
        if (_invalidSwatch is not null) _invalidSwatch.Color = StudioStyle.Surface;
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
        if (_apply is null) return;
        _apply.Disabled = !PreviewReady;
        _undo.Disabled = _session.History?.CanUndo != true;
        _redo.Disabled = _session.History?.CanRedo != true;
    }

    private void UpdateSwatches()
    {
        if (_validSwatch is null || _invalidSwatch is null) return;
        _validSwatch.Color = ParseColor(_validColor.Text, StudioStyle.Surface);
        _invalidSwatch.Color = ParseColor(_invalidColor.Text, StudioStyle.Surface);
    }

    private SourceSnapshotUx6T CaptureSource()
    {
        var history = _sourceSession.History!;
        return new(_sourceSession.Project!, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureModernToolingApprovalUx6T());
    }

    private bool SourceMatches()
    {
        var snapshot = _sourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.AreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureModernToolingApprovalUx6T() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        var released = string.IsNullOrWhiteSpace(_evidenceRoot) ||
                       StudioSessionController.DeleteTemporaryUx6T(_evidenceRoot);
        _evidenceRoot = null;
        _evidenceReleased = released;
        _session = _sourceSession;
        if (!released) GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_T_UI_CLEANUP_FAILED");
    }

    private VBoxContainer TabPage(string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var margin = StudioStyle.Margin(10, 9, 10, 9);
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(margin);
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 8);
        margin.AddChild(page);
        _tabs.AddChild(scroll);
        return page;
    }

    private static LineEdit Field(string placeholder) => new()
    {
        PlaceholderText = placeholder,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        CustomMinimumSize = new Vector2(120, 34),
    };

    private static OptionButton EnumChoice(IEnumerable<string> labels)
    {
        var choice = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(150, 34),
        };
        foreach (var label in labels) choice.AddItem(label);
        return choice;
    }

    private static ColorRect Swatch() => new()
    {
        CustomMinimumSize = new Vector2(42, 34),
        MouseFilter = MouseFilterEnum.Ignore,
        Color = StudioStyle.Surface,
    };

    private static VBoxContainer LabeledColumn(string label, Control control)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddChild(StudioStyle.Label(label.ToUpperInvariant(), 8, StudioStyle.Muted));
        column.AddChild(control);
        return column;
    }

    private static VBoxContainer LabeledColor(string label, LineEdit field, ColorRect swatch)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(field);
        row.AddChild(swatch);
        return LabeledColumn(label, row);
    }

    private static void AddLabeled(VBoxContainer parent, string label, Control control) =>
        parent.AddChild(LabeledColumn(label, control));

    private static Button ActionButton(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private void UpdateBounds()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 64, 1080, 1740);
        var height = Math.Clamp(viewport.Y - 44, 760, 1010);
        _dialog.Position = new Vector2((viewport.X - width) / 2f, (viewport.Y - height) / 2f);
        _dialog.Size = new Vector2(width, height);
    }

    private static Color ParseColor(string value, Color fallback)
    {
        var normalized = (value ?? string.Empty).Trim().TrimStart('#');
        return normalized.Length is 6 or 8 && normalized.All(Uri.IsHexDigit)
            ? new Color(normalized)
            : fallback;
    }

    private static string HumanShape(AreaEffectShape shape) => shape switch
    {
        AreaEffectShape.Circle => "Cercle",
        AreaEffectShape.Cone => "Cône",
        AreaEffectShape.Rectangle => "Rectangle",
        AreaEffectShape.Line => "Ligne",
        _ => "Inconnue",
    };

    private static string HumanColorMode(TargetIndicatorColorMode mode) => mode switch
    {
        TargetIndicatorColorMode.Standard => "Standard",
        TargetIndicatorColorMode.Deuteranopia => "Deutéranopie",
        TargetIndicatorColorMode.Tritanopia => "Tritanopie",
        TargetIndicatorColorMode.HighContrast => "Contraste élevé",
        _ => "Inconnue",
    };

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static IEnumerable<string> DescendantText(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            switch (child)
            {
                case Label label when label.Visible: yield return label.Text; break;
                case Button button when button.Visible: yield return button.Text; break;
                case CheckBox checkBox when checkBox.Visible: yield return checkBox.Text; break;
                case LineEdit lineEdit when lineEdit.Visible: yield return lineEdit.Text; break;
                case OptionButton option when option.Visible:
                    for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
                    break;
            }
            foreach (var text in DescendantText(child)) yield return text;
        }
    }

    private static bool ContainsUuid(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    private sealed record SourceSnapshotUx6T(
        ProjectDefinition Project,
        ProjectCommandHistory History,
        ProjectRevision Revision,
        IReadOnlyList<CommandHistoryJournalEntry> Journal,
        bool IsDirty,
        bool CanUndo,
        bool CanRedo,
        AreaId? AreaId,
        IReadOnlyList<EntityId> SelectedEntityIds,
        EntityId? SelectedEntityId,
        StudioUx6TApprovalSnapshot Approval);
}
