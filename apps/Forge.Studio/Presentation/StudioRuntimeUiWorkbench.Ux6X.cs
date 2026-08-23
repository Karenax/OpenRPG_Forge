using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Authoring;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioRuntimeUiWorkbenchUx6X : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx6X? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _updating;
    private bool _explicitSelection;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _profileChoice;
    private readonly Label _selection;
    private readonly TabContainer _tabs;

    private readonly CheckBox _shellEnabled;
    private readonly CheckBox _shellRequired;
    private readonly LineEdit _kit;
    private readonly LineEdit _skin;
    private readonly LineEdit _layoutDocument;
    private readonly LineEdit _shellScale;
    private readonly CheckBox _mapVisible;
    private readonly CheckBox _journalVisible;
    private readonly CheckBox _hudVisible;
    private readonly CheckBox _inputAgnostic;
    private readonly CheckBox _scalable;
    private readonly TextEdit _inputActions;

    private readonly LineEdit _uiScale;
    private readonly LineEdit _textScale;
    private readonly OptionButton _fontChoice;
    private readonly LineEdit _primaryFont;
    private readonly TextEdit _fallbackFonts;
    private readonly Label _fontGovernance;
    private readonly Label _fontSample;

    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _results;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _previewButton;
    private readonly Button _applyButton;
    private readonly Button _undoButton;
    private readonly Button _redoButton;
    private readonly Button _saveButton;

    private StudioUx6XDraft? _draft;
    private StudioUx6XPreview? _preview;
    private bool _previewReady;
    private string? _previewFingerprint;
    private IReadOnlyList<StudioUx6XFontGovernance> _fonts = [];

    internal event Action<string, bool>? StatusChanged;
    internal event Action? RuntimeUiChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSelection && _draft is not null && _profileChoice.Selected > 0;
    internal bool PreviewReady => _previewReady && _preview is
    {
        Succeeded: true, CanApply: true, ProjectDryRun: true, WorkspaceDryRun: true,
        RuntimeShellResolved: true, TypographyResolved: true, RuntimeClaimsBounded: true
    };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => SourceMatchesUx6X();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsTwoWorkflows => _tabs.GetTabCount() == 2 &&
        _tabs.GetTabTitle(0) == "Carte, journal et HUD" &&
        _tabs.GetTabTitle(1) == "Police et échelles";
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx6X(this)
                .Concat(Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle)));
            return text.Contains("Expérience UI runtime", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec canonique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Carte, journal et HUD", StringComparison.Ordinal) &&
                   text.Contains("Police et échelles", StringComparison.Ordinal) &&
                   text.Contains("aucun HUD", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx6X(text);
        }
    }

    internal StudioRuntimeUiWorkbenchUx6X(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.012f, .018f, .027f, .975f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6XRuntimeUiDialog", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        _dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _dialog.OffsetLeft = 18;
        _dialog.OffsetTop = 14;
        _dialog.OffsetRight = -18;
        _dialog.OffsetBottom = -14;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("Expérience UI runtime", 18));
        headingText.AddChild(StudioStyle.Label(
            "Carte, journal, HUD, police et échelles · aperçu sec canonique · deux historiques explicites.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        _profileChoice = StudioUx2EnvBControls.Options();
        _profileChoice.ItemSelected += SelectProfileUx6X;
        root.AddChild(FieldUx6X("Module et profil utilisateur · sélection explicite", _profileChoice, 680));
        _selection = StudioStyle.Label("Choisissez explicitement le module ouvert avant toute prévisualisation.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 310),
            CustomMaximumSize = new Vector2(-1, 580),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(tabsHost);
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var shellTab = ScrollTabUx6X("Carte, journal et HUD");
        var shellFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _shellEnabled = StudioUx2EnvBControls.Check("Shell runtime activé");
        _shellRequired = StudioUx2EnvBControls.Check("Profil requis au runtime");
        _mapVisible = StudioUx2EnvBControls.Check("Carte visible");
        _journalVisible = StudioUx2EnvBControls.Check("Journal visible");
        _hudVisible = StudioUx2EnvBControls.Check("HUD visible");
        foreach (var item in new[] { _shellEnabled, _shellRequired, _mapVisible, _journalVisible, _hudVisible })
            shellFlags.AddChild(item);
        shellTab.AddChild(shellFlags);
        var identity = GridUx6X(3);
        _kit = StudioUx2EnvBControls.Text("core-rpg");
        _skin = StudioUx2EnvBControls.Text("forge-aurora-dark");
        _layoutDocument = StudioUx2EnvBControls.Text("ui/runtime-shell.json");
        identity.AddChild(FieldUx6X("Kit UI", _kit));
        identity.AddChild(FieldUx6X("Skin", _skin));
        identity.AddChild(FieldUx6X("Document de mise en page", _layoutDocument));
        shellTab.AddChild(StudioUx2EnvBControls.Card("Ux6XShellIdentity", "Kit et présentation",
            "Le shell reste data-driven et fourni par le kit ; aucun CanvasLayer n’est créé dans cet aperçu.",
            identity));
        var behavior = GridUx6X(3);
        _shellScale = NumberUx6X(.5, 3, .05);
        _inputAgnostic = StudioUx2EnvBControls.Check("Entrées clavier/manette agnostiques");
        _scalable = StudioUx2EnvBControls.Check("Reflow et mise à l’échelle autorisés");
        behavior.AddChild(FieldUx6X("Échelle du shell", _shellScale));
        behavior.AddChild(FieldUx6X("Entrées", _inputAgnostic));
        behavior.AddChild(FieldUx6X("Adaptation", _scalable));
        shellTab.AddChild(StudioUx2EnvBControls.Card("Ux6XShellBehavior", "Échelle et contrôles",
            "Les actions restent des identifiants de commande, indépendants du périphérique physique.", behavior));
        _inputActions = TextAreaUx6X("map\njournal\ninventory\ncharacter\npause", 92);
        shellTab.AddChild(FieldUx6X("Actions accessibles · une par ligne", _inputActions));

        var typeTab = ScrollTabUx6X("Police et échelles");
        var scales = GridUx6X(2);
        _uiScale = NumberUx6X(EditorWorkspaceProfile.MinimumUiScale,
            EditorWorkspaceProfile.MaximumUiScale, .05);
        _textScale = NumberUx6X(EditorWorkspaceProfile.MinimumTextScale,
            EditorWorkspaceProfile.MaximumTextScale, .05);
        scales.AddChild(FieldUx6X("Échelle interface Studio", _uiScale));
        scales.AddChild(FieldUx6X("Échelle du texte", _textScale));
        typeTab.AddChild(StudioUx2EnvBControls.Card("Ux6XScales", "Échelles utilisateur",
            "Les valeurs sont persistées dans le profil utilisateur et restent distinctes du shell du module.", scales));
        _fontChoice = StudioUx2EnvBControls.Options();
        _fontChoice.ItemSelected += SelectFontUx6X;
        _primaryFont = StudioUx2EnvBControls.Text("Noto Sans");
        _fallbackFonts = TextAreaUx6X("Segoe UI\nArial", 76);
        var fonts = GridUx6X(2);
        fonts.AddChild(FieldUx6X("Polices connues ou héritées", _fontChoice));
        fonts.AddChild(FieldUx6X("Police principale", _primaryFont));
        typeTab.AddChild(StudioUx2EnvBControls.Card("Ux6XFonts", "Chaîne TrueType",
            "Une police gouvernée expose scripts, locales et paliers DPI ; une police système reste explicitement héritée.", fonts));
        typeTab.AddChild(FieldUx6X("Fallbacks ordonnés · un par ligne", _fallbackFonts));
        _fontGovernance = StudioStyle.Label("Aucune police sélectionnée.", 10, StudioStyle.Muted, wrap: true);
        typeTab.AddChild(_fontGovernance);
        var sampleCard = StudioStyle.Panel("Ux6XTypographySample", StudioStyle.SurfaceRaised, 8, 1);
        var sampleMargin = StudioStyle.Margin(10, 8, 10, 8);
        sampleCard.AddChild(sampleMargin);
        _fontSample = StudioStyle.Label("Clairval — Carte, journal, météo · قدرة · accès clavier", 14,
            StudioStyle.Text, wrap: true);
        sampleMargin.AddChild(_fontSample);
        typeTab.AddChild(sampleCard);
        typeTab.AddChild(StudioStyle.Label(
            "L’échantillon utilise une police Godot locale, mais l’aperçu n’applique pas le thème global ni le HUD runtime.",
            10, StudioStyle.Muted, wrap: true));

        var review = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 138),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _diff = ReviewColumnUx6X(review, "DIFF HUMAIN");
        _results = ReviewColumnUx6X(review, "RÉSOLUTION COPIÉE");
        _diagnostics = ReviewColumnUx6X(review, "DIAGNOSTICS");
        root.AddChild(review);
        _summary = StudioStyle.Label("Sélectionnez le module, puis préparez un aperçu sec.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_summary);

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _previewButton = StudioStyle.Button("Aperçu sec", primary: true);
        _applyButton = StudioStyle.Button("Appliquer");
        _undoButton = StudioStyle.Button("Annuler");
        _redoButton = StudioStyle.Button("Rétablir");
        _saveButton = StudioStyle.Button("Enregistrer");
        foreach (var button in new[] { _previewButton, _applyButton, _undoButton, _redoButton, _saveButton })
        {
            button.CustomMinimumSize = new Vector2(button == _previewButton ? 132 : 108, 34);
            actions.AddChild(button);
        }
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _status = StudioStyle.Label("Aucune mutation appliquée.", 10, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        actions.AddChild(_status);
        root.AddChild(actions);

        _previewButton.Pressed += PreviewUx6X;
        _applyButton.Pressed += ApplyUx6X;
        _undoButton.Pressed += UndoUx6X;
        _redoButton.Pressed += RedoUx6X;
        _saveButton.Pressed += SaveUx6X;
        WireChangesUx6X();
        ResetControlsUx6X();
    }

    internal void Open() => OpenBoundUx6X(_sourceSession, evidence: false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBoundUx6X(_sourceSession, evidence: false, cancelApproval: false);

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CancelRuntimeUiApprovalUx6X();
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx6X();
        _session = _sourceSession;
        ResetControlsUx6X();
    }

    internal void CloseEvidence()
    {
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx6X();
        _session = _sourceSession;
        ResetControlsUx6X(keepReleaseState: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!mode.StartsWith("ux6-x", StringComparison.OrdinalIgnoreCase)) return false;
        ReleaseEvidenceUx6X();
        _sourceSnapshot = CaptureSourceUx6X();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-x-{Guid.NewGuid():N}");
        try
        {
            var source = _sourceSession.Project;
            if (source is null || !ProjectSession.Create(root, source).Succeeded) return false;
            var isolated = new StudioSessionController();
            if (!isolated.Open(root).Succeeded)
            {
                isolated.Close();
                StudioSessionController.DeleteTemporaryUx6X(root);
                return false;
            }
            _evidenceSession = isolated;
            _evidenceRoot = root;
            _evidenceReleased = false;
            OpenBoundUx6X(isolated, evidence: true, cancelApproval: true);
            _profileChoice.Select(1);
            SelectProfileUx6X(1);
            if (_draft is null) return false;
            _draft = StudioSessionController.ChangedRuntimeUiDraftUx6X(_draft);
            PopulateDraftUx6X();
            _preview = _session.PreviewRuntimeUiUx6X(_draft);
            RenderPreviewUx6X();
            _tabs.CurrentTab = mode.EndsWith("-type-scale", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var previewReady = PreviewReady;
            var workflows = ShowsTwoWorkflows;
            var human = ShowsHumanSurface;
            var sourceMatches = SourceMatchesUx6X();
            if (!previewReady || !workflows || !human || !sourceMatches)
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_X_UI_PREPARE_FAILED " +
                            $"preview={previewReady.ToString().ToLowerInvariant()} " +
                            $"workflows={workflows.ToString().ToLowerInvariant()} " +
                            $"human={human.ToString().ToLowerInvariant()} " +
                            $"source={sourceMatches.ToString().ToLowerInvariant()}");
            return previewReady && workflows && human && sourceMatches;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_X_EVIDENCE_FAILED type={exception.GetType().Name}");
            return false;
        }
        finally
        {
            if (!PreviewReady || !ShowsTwoWorkflows || !SourceMatchesUx6X())
            {
                if (_evidenceSession is not null) ReleaseEvidenceUx6X();
                else StudioSessionController.DeleteTemporaryUx6X(root);
            }
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceApproval = _sourceSession.CaptureRuntimeUiApprovalUx6X();
        try
        {
            var ready = PrepareEvidence("ux6-x-runtime-ui");
            if (!ready || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !PreviewReady || _draft is null || string.IsNullOrWhiteSpace(_evidenceRoot)) return false;
            var applied = _session.ApplyApprovedRuntimeUiUx6X(_draft);
            var undo = applied.Succeeded ? _session.UndoRuntimeUiUx6X() : new(false, "non exécuté");
            var redo = undo.Succeeded ? _session.RedoRuntimeUiUx6X() : new(false, "non exécuté");
            var saved = redo.Succeeded ? _session.SaveRuntimeUiUx6X() : new(false, "non exécuté");
            var projectJson = _session.Project is null ? string.Empty :
                ProjectJsonSerializer.Serialize(_session.Project);
            var workspaceJson = EditorWorkspaceProfileJsonSerializer.Serialize(_session.WorkspaceProfile);
            var reopened = new StudioSessionController();
            var roundTrip = saved.Succeeded && reopened.Open(_evidenceRoot).Succeeded &&
                            reopened.Project is not null &&
                            ProjectJsonSerializer.Serialize(reopened.Project) == projectJson &&
                            EditorWorkspaceProfileJsonSerializer.Serialize(reopened.WorkspaceProfile) == workspaceJson;
            reopened.Close();
            return applied.Succeeded && undo.Succeeded && redo.Succeeded && saved.Succeeded && roundTrip &&
                   SourceMatchesUx6X();
        }
        finally
        {
            CloseEvidence();
            _sourceSession.RestoreRuntimeUiApprovalUx6X(sourceApproval);
        }
    }

    private void OpenBoundUx6X(StudioSessionController session, bool evidence, bool cancelApproval)
    {
        _session = session;
        if (cancelApproval) _session.CancelRuntimeUiApprovalUx6X();
        Visible = true;
        _fonts = _session.FontGovernanceUx6X;
        _updating = true;
        _profileChoice.Clear();
        _profileChoice.AddItem("Choisir explicitement le module…");
        if (_session.Project is { } project)
            _profileChoice.AddItem($"{HumanUx6X(project.Module.DisplayName)} · shell runtime et profil utilisateur");
        _profileChoice.Select(0);
        PopulateFontChoicesUx6X();
        _updating = false;
        _explicitSelection = false;
        _draft = null;
        _selection.Text = evidence
            ? "Session de preuve isolée · choisissez le module copié."
            : "Choisissez explicitement le module ouvert avant toute prévisualisation.";
        ResetPreviewUx6X();
        UpdateButtonsUx6X();
    }

    private void SelectProfileUx6X(long index)
    {
        if (_updating) return;
        _explicitSelection = index == 1;
        _draft = _session.CreateRuntimeUiDraftUx6X(_explicitSelection);
        if (_draft is null)
        {
            _selection.Text = "Sélection explicite requise.";
            ResetPreviewUx6X();
            return;
        }
        _selection.Text = "Module sélectionné · modifiez les deux onglets puis prévisualisez.";
        PopulateDraftUx6X();
        ResetPreviewUx6X();
    }

    private void PopulateDraftUx6X()
    {
        if (_draft is null) return;
        _updating = true;
        var shell = _draft.RuntimeShell;
        _shellEnabled.ButtonPressed = shell.Enabled;
        _shellRequired.ButtonPressed = shell.RequiredAtRuntime;
        _kit.Text = shell.KitId;
        _skin.Text = shell.SkinId;
        _layoutDocument.Text = shell.LayoutDocument;
        _shellScale.Text = shell.Scale.ToString("0.##", CultureInfo.InvariantCulture);
        _mapVisible.ButtonPressed = shell.MapVisible;
        _journalVisible.ButtonPressed = shell.JournalVisible;
        _hudVisible.ButtonPressed = shell.HudVisible;
        _inputAgnostic.ButtonPressed = shell.InputAgnostic;
        _scalable.ButtonPressed = shell.Scalable;
        _inputActions.Text = string.Join('\n', shell.InputActions);
        var type = _draft.Typography;
        _uiScale.Text = type.UiScale.ToString("0.##", CultureInfo.InvariantCulture);
        _textScale.Text = type.TextScale.ToString("0.##", CultureInfo.InvariantCulture);
        _primaryFont.Text = type.PrimaryFontFamily;
        _fallbackFonts.Text = string.Join('\n', type.FallbackFontFamilies);
        var fontIndex = _fonts.ToList().FindIndex(item => string.Equals(item.Family,
            type.PrimaryFontFamily, StringComparison.OrdinalIgnoreCase));
        _fontChoice.Select(fontIndex + 1);
        RenderFontGovernanceUx6X();
        ApplyFontSampleUx6X(type);
        _updating = false;
    }

    private void PreviewUx6X()
    {
        if (!TryReadDraftUx6X(out var draft)) return;
        _draft = draft;
        _preview = _session.PreviewRuntimeUiUx6X(draft!);
        RenderPreviewUx6X();
    }

    private void ApplyUx6X()
    {
        if (!PreviewReady || !TryReadDraftUx6X(out var current) || current is null ||
            _previewFingerprint != _session.RuntimeUiDraftFingerprintUx6X(current))
        {
            SetLocalStatusUx6X("Le brouillon a changé · relancez l’aperçu sec.", true);
            ResetPreviewUx6X();
            return;
        }
        var result = _session.ApplyApprovedRuntimeUiUx6X(current);
        SetLocalStatusUx6X(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            RuntimeUiChanged?.Invoke();
            _draft = _session.CreateRuntimeUiDraftUx6X(true);
            PopulateDraftUx6X();
        }
        ResetPreviewUx6X();
    }

    private void UndoUx6X() => HandleActionUx6X(_session.UndoRuntimeUiUx6X());
    private void RedoUx6X() => HandleActionUx6X(_session.RedoRuntimeUiUx6X());
    private void SaveUx6X() => HandleActionUx6X(_session.SaveRuntimeUiUx6X(), reload: false);

    private void HandleActionUx6X(StudioUx6XActionResult result, bool reload = true)
    {
        SetLocalStatusUx6X(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            RuntimeUiChanged?.Invoke();
            if (reload)
            {
                _draft = _session.CreateRuntimeUiDraftUx6X(true);
                PopulateDraftUx6X();
            }
        }
        ResetPreviewUx6X();
    }

    private bool TryReadDraftUx6X(out StudioUx6XDraft? draft)
    {
        draft = null;
        if (_draft is null)
        {
            SetLocalStatusUx6X("Choisissez explicitement le module.", true);
            return false;
        }
        var actions = SplitLinesUx6X(_inputActions.Text);
        var fallbacks = SplitLinesUx6X(_fallbackFonts.Text);
        if (!TryParseNumberUx6X(_shellScale.Text, out var shellScale) ||
            !TryParseNumberUx6X(_uiScale.Text, out var uiScale) ||
            !TryParseNumberUx6X(_textScale.Text, out var textScale))
        {
            SetLocalStatusUx6X("Les trois échelles doivent être des nombres finis.", true);
            return false;
        }
        draft = _draft with
        {
            RuntimeShell = _draft.RuntimeShell with
            {
                Enabled = _shellEnabled.ButtonPressed,
                RequiredAtRuntime = _shellRequired.ButtonPressed,
                KitId = _kit.Text,
                SkinId = _skin.Text,
                LayoutDocument = _layoutDocument.Text,
                Scale = shellScale,
                MapVisible = _mapVisible.ButtonPressed,
                JournalVisible = _journalVisible.ButtonPressed,
                HudVisible = _hudVisible.ButtonPressed,
                InputAgnostic = _inputAgnostic.ButtonPressed,
                Scalable = _scalable.ButtonPressed,
                InputActions = actions,
            },
            Typography = _draft.Typography with
            {
                UiScale = uiScale,
                TextScale = textScale,
                PrimaryFontFamily = _primaryFont.Text,
                FallbackFontFamilies = fallbacks,
            },
        };
        ApplyFontSampleUx6X(draft.Typography);
        return true;
    }

    private void RenderPreviewUx6X()
    {
        ClearUx6X(_diff);
        ClearUx6X(_results);
        ClearUx6X(_diagnostics);
        if (_preview is null)
        {
            ResetPreviewUx6X();
            return;
        }
        foreach (var item in _preview.Diff) _diff.AddChild(StudioStyle.Label(item, 10, StudioStyle.Text, wrap: true));
        foreach (var item in _preview.Results) _results.AddChild(StudioStyle.Label(item, 10, StudioStyle.Text, wrap: true));
        foreach (var item in _preview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label($"{HumanUx6X(item.Code)} · {HumanUx6X(item.Message)}",
                10, StudioStyle.Danger, wrap: true));
        if (_preview.Diff.Count == 0) _diff.AddChild(StudioStyle.Label("Aucune différence.", 10, StudioStyle.Muted));
        if (_preview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 10, StudioStyle.Success));
        _summary.Text = _preview.Message;
        _previewReady = _preview.Succeeded && _preview.CanApply && _preview.ProjectDryRun &&
                        _preview.WorkspaceDryRun && _preview.RuntimeClaimsBounded && _draft is not null;
        _previewFingerprint = _previewReady ? _session.RuntimeUiDraftFingerprintUx6X(_draft!) : null;
        SetLocalStatusUx6X(_preview.Message, !_preview.Succeeded);
        UpdateButtonsUx6X();
    }

    private void SelectFontUx6X(long index)
    {
        if (_updating || index <= 0 || index > _fonts.Count) return;
        _primaryFont.Text = _fonts[(int)index - 1].Family;
        RenderFontGovernanceUx6X();
        InvalidatePreviewUx6X();
    }

    private void PopulateFontChoicesUx6X()
    {
        _fontChoice.Clear();
        _fontChoice.AddItem("Choisir une police connue…");
        foreach (var font in _fonts)
            _fontChoice.AddItem($"{HumanUx6X(font.Family)} · {(font.Governed ? "gouvernée" : "système héritée")}");
        _fontChoice.Select(0);
    }

    private void RenderFontGovernanceUx6X()
    {
        var selected = _fonts.FirstOrDefault(item => string.Equals(item.Family,
            _primaryFont.Text, StringComparison.OrdinalIgnoreCase));
        _fontGovernance.Text = selected is { Governed: true }
            ? $"Gouvernance : scripts {JoinUx6X(selected.ShapingScripts)} · locales {JoinUx6X(selected.Locales)} · DPI {JoinUx6X(selected.DpiBuckets.Select(item => item.ToString()).ToArray())}."
            : "Police système héritée · aucun chunk ou manifeste de police n’est créé implicitement.";
    }

    private void ApplyFontSampleUx6X(StudioUx6XTypographyDraft type)
    {
        var font = new SystemFont
        {
            FontNames = new[] { type.PrimaryFontFamily }.Concat(type.FallbackFontFamilies).ToArray(),
        };
        _fontSample.AddThemeFontOverride("font", font);
        _fontSample.AddThemeFontSizeOverride("font_size",
            Math.Max(10, (int)Math.Round(14 * type.TextScale)));
        _fontSample.CustomMinimumSize = new Vector2(0, Math.Max(42, 42 * (float)type.UiScale));
    }

    private void WireChangesUx6X()
    {
        foreach (var check in new[] { _shellEnabled, _shellRequired, _mapVisible, _journalVisible,
                     _hudVisible, _inputAgnostic, _scalable })
            check.Toggled += _ => InvalidatePreviewUx6X();
        foreach (var edit in new[] { _kit, _skin, _layoutDocument, _primaryFont })
            edit.TextChanged += _ =>
            {
                RenderFontGovernanceUx6X();
                InvalidatePreviewUx6X();
            };
        _inputActions.TextChanged += InvalidatePreviewUx6X;
        _fallbackFonts.TextChanged += InvalidatePreviewUx6X;
        _shellScale.TextChanged += _ => InvalidatePreviewUx6X();
        _uiScale.TextChanged += _ => InvalidatePreviewUx6X();
        _textScale.TextChanged += _ => InvalidatePreviewUx6X();
    }

    private void InvalidatePreviewUx6X()
    {
        if (_updating) return;
        _previewReady = false;
        _previewFingerprint = null;
        _session.CancelRuntimeUiApprovalUx6X();
        _summary.Text = "Brouillon modifié · relancez l’aperçu sec.";
        UpdateButtonsUx6X();
    }

    private void ResetPreviewUx6X()
    {
        _preview = null;
        _previewReady = false;
        _previewFingerprint = null;
        ClearUx6X(_diff);
        ClearUx6X(_results);
        ClearUx6X(_diagnostics);
        _summary.Text = _draft is null
            ? "Sélectionnez le module, puis préparez un aperçu sec."
            : "Modifiez le brouillon, puis préparez un aperçu sec.";
        UpdateButtonsUx6X();
    }

    private void ResetControlsUx6X(bool keepReleaseState = false)
    {
        _updating = true;
        _profileChoice.Clear();
        _profileChoice.AddItem("Choisir explicitement le module…");
        _profileChoice.Select(0);
        _fontChoice.Clear();
        _fontChoice.AddItem("Choisir une police connue…");
        _explicitSelection = false;
        _draft = null;
        _preview = null;
        _fonts = [];
        _selection.Text = "Choisissez explicitement le module ouvert avant toute prévisualisation.";
        _fontGovernance.Text = "Aucune police sélectionnée.";
        if (!keepReleaseState) _evidenceReleased = true;
        _updating = false;
        ResetPreviewUx6X();
    }

    private void UpdateButtonsUx6X()
    {
        _applyButton.Disabled = !PreviewReady;
        _undoButton.Disabled = _session.History?.CanUndo != true || !_session.CanUndoWorkspaceProfile;
        _redoButton.Disabled = _session.History?.CanRedo != true;
        _saveButton.Disabled = _session.Project is null;
    }

    private void SetLocalStatusUx6X(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private SourceSnapshotUx6X CaptureSourceUx6X()
    {
        var history = _sourceSession.History!;
        return new(_sourceSession.Project!, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            EditorWorkspaceProfileJsonSerializer.Serialize(_sourceSession.WorkspaceProfile),
            _sourceSession.CaptureRuntimeUiApprovalUx6X());
    }

    private bool SourceMatchesUx6X()
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
               EditorWorkspaceProfileJsonSerializer.Serialize(_sourceSession.WorkspaceProfile) ==
               snapshot.WorkspaceJson &&
               _sourceSession.CaptureRuntimeUiApprovalUx6X() == snapshot.Approval;
    }

    private void ReleaseEvidenceUx6X()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        var released = string.IsNullOrWhiteSpace(_evidenceRoot) ||
                       StudioSessionController.DeleteTemporaryUx6X(_evidenceRoot);
        _evidenceRoot = null;
        _evidenceReleased = released;
        _session = _sourceSession;
        if (!released) GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_X_UI_CLEANUP_FAILED");
    }

    private VBoxContainer ScrollTabUx6X(string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(body);
        _tabs.AddChild(scroll);
        return body;
    }

    private static GridContainer GridUx6X(int columns) => new()
    {
        Columns = columns,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static VBoxContainer FieldUx6X(string label, Control control, float width = 0)
    {
        var field = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        field.AddThemeConstantOverride("separation", 3);
        field.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted, wrap: true));
        if (width > 0) control.CustomMinimumSize = new Vector2(width, control.CustomMinimumSize.Y);
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        field.AddChild(control);
        return field;
    }

    private static LineEdit NumberUx6X(double minimum, double maximum, double arrowStep)
    {
        return new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = $"{minimum:0.##} à {maximum:0.##}",
            TooltipText = $"Valeur exacte de {minimum:0.##} à {maximum:0.##} · pas conseillé {arrowStep:0.##}.",
        };
    }

    private static bool TryParseNumberUx6X(string value, out double parsed)
    {
        var normalized = (value ?? string.Empty).Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) &&
               double.IsFinite(parsed);
    }

    private static TextEdit TextAreaUx6X(string text, float height) => new()
    {
        Text = text,
        CustomMinimumSize = new Vector2(0, height),
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        WrapMode = TextEdit.LineWrappingMode.Boundary,
        ScrollFitContentHeight = false,
    };

    private static VBoxContainer ReviewColumnUx6X(HBoxContainer row, string title)
    {
        var panel = StudioStyle.Panel($"Ux6X{title}", StudioStyle.SurfaceRaised, 8, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var margin = StudioStyle.Margin(8, 6, 8, 6);
        panel.AddChild(margin);
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 3);
        column.AddChild(StudioStyle.Label(title, 9, StudioStyle.Muted));
        margin.AddChild(column);
        row.AddChild(panel);
        return column;
    }

    private static IReadOnlyList<string> SplitLinesUx6X(string value) =>
        (value ?? string.Empty).Replace("\r", string.Empty).Split('\n')
        .Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();

    private static void ClearUx6X(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static IEnumerable<string> DescendantTextUx6X(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label) yield return label.Text;
            if (child is Button button) yield return button.Text;
            foreach (var text in DescendantTextUx6X(child)) yield return text;
        }
    }

    private static string HumanUx6X(string value) => Regex.Replace(value ?? string.Empty,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        "identifiant protégé").Replace('_', ' ').Trim();

    private static bool ContainsUuidUx6X(string value) => Regex.IsMatch(value,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");

    private static string JoinUx6X(IReadOnlyList<string> values) =>
        values.Count == 0 ? "non déclaré" : string.Join(", ", values.Select(HumanUx6X));

    private sealed record SourceSnapshotUx6X(ProjectDefinition Project, ProjectCommandHistory History,
        ProjectRevision Revision, IReadOnlyList<CommandHistoryJournalEntry> Journal, bool IsDirty, bool CanUndo,
        bool CanRedo, AreaId? AreaId, IReadOnlyList<EntityId> SelectedEntityIds, EntityId? SelectedEntityId,
        string WorkspaceJson, StudioUx6XApprovalSnapshot Approval);
}
