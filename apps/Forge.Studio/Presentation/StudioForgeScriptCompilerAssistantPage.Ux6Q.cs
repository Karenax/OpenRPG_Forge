using System.Globalization;
using System.Text.RegularExpressions;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioForgeScriptCompilerAssistantPageUx6Q : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx6QSourceSnapshot? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _sourceAssistantExisted;
    private bool _sourceAssistantMetadataExisted;
    private byte[]? _sourceAssistantBytes;
    private byte[]? _sourceAssistantMetadataBytes;
    private bool _evidenceReleased = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _profileChoice;
    private readonly OptionButton _sourceChoice;
    private readonly LineEdit _newPath;
    private readonly TabContainer _tabs;
    private readonly CheckBox _incremental;
    private readonly CheckBox _stableDiagnostics;
    private readonly CheckBox _telemetry;
    private readonly LineEdit _parallelism;
    private readonly LineEdit _sourceQuota;
    private readonly LineEdit _suites;
    private readonly Label _compilerInventory;
    private readonly CodeEdit _compilerResults;
    private readonly VBoxContainer _compilerDiagnostics;
    private readonly LineEdit _eventBinding;
    private readonly TextEdit _intent;
    private readonly LineEdit _message;
    private readonly LineEdit _variableKey;
    private readonly LineEdit _variableValue;
    private readonly LineEdit _citations;
    private readonly LineEdit _styleRules;
    private readonly LineEdit _author;
    private readonly LineEdit _seed;
    private readonly CodeEdit _sourceOverride;
    private readonly CodeEdit _proposalSource;
    private readonly Label _proposalEvidence;
    private readonly VBoxContainer _assistantDiagnostics;
    private readonly Label _compilerReview;
    private readonly Label _assistantReview;
    private readonly Label _status;
    private readonly HBoxContainer _compilerActions;
    private readonly HBoxContainer _assistantActions;
    private readonly Button _applyCompiler;
    private readonly Button _undoCompiler;
    private readonly Button _redoCompiler;
    private readonly Button _applyAssistant;
    private readonly Button _undoAssistant;
    private readonly Button _redoAssistant;

    private StudioUx6QCompilerDraft? _compilerDraft;
    private StudioUx6QAssistantDraft? _assistantDraft;
    private IReadOnlyList<StudioUx6QSourceChoice> _sourceChoices = [];
    private StudioUx6QCompilerPreview? _compilerPreview;
    private StudioUx6QCompilationEvidence? _compilation;
    private StudioUx6QProposalView? _proposal;
    private StudioUx6QAssistantPreview? _assistantPreview;
    private bool _explicitProfile;
    private bool _explicitAssistant;
    private bool _assistantCanUndo;
    private bool _assistantCanRedo;
    private bool _updating;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitProfile && _compilerDraft is not null &&
                                          _explicitAssistant && _assistantDraft is not null;
    internal bool CompilerPreviewReady => _compilerPreview is
    { Succeeded: true, CanApply: true, CanonicalDryRun: true };
    internal bool CompilerEvidenceReady => _compilation is
    {
        Succeeded: true,
        InventoryPresent: true,
        DependencyAware: true,
        CacheReused: true,
        DependencyInvalidated: true,
        StableDiagnostics: true,
        Telemetry: true,
        Conformance: true,
        TemporaryCacheReleased: true,
    };
    internal bool ProposalReady => _proposal is { Succeeded: true, CanPreview: true };
    internal bool AssistantPreviewReady => _assistantPreview is
    {
        Succeeded: true,
        CanApply: true,
        CanonicalDryRun: true,
        ApprovalBound: true,
        PermissionBound: true,
    };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => SourceMatches() && SourceAssistantMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool HumanControlsReady => _profileChoice.ItemCount >= 2 &&
                                         _newPath.Text.EndsWith(".as", StringComparison.OrdinalIgnoreCase) &&
                                         _tabs.GetTabCount() == 2;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return text.Contains("Compilateur moderne", StringComparison.Ordinal) &&
                   text.Contains("Création assistée hors ligne", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec canonique", StringComparison.Ordinal) &&
                   text.Contains("Dépendances et cache", StringComparison.Ordinal) &&
                   text.Contains("Approbation humaine", StringComparison.Ordinal) &&
                   text.Contains("Aucun fournisseur externe", StringComparison.Ordinal) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioForgeScriptCompilerAssistantPageUx6Q(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.008f, .014f, .023f, .988f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6QCompilerAssistant", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 7);
        margin.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headingText.AddChild(StudioStyle.Label("ForgeScript · compilateur & création assistée", 19));
        headingText.AddChild(StudioStyle.Label(
            "Compilateur moderne · Dépendances et cache · Création assistée hors ligne · Approbation humaine · Aperçu sec canonique.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = ActionButton("Fermer", Close, 96);
        heading.AddChild(close);
        root.AddChild(heading);

        var selectors = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _profileChoice = StudioUx2EnvBControls.Options();
        _profileChoice.CustomMinimumSize = new Vector2(440, 34);
        _profileChoice.ItemSelected += SelectProfile;
        selectors.AddChild(Field("Profil compilateur · sélection explicite", _profileChoice, 440));
        _sourceChoice = StudioUx2EnvBControls.Options();
        _sourceChoice.CustomMinimumSize = new Vector2(420, 34);
        _sourceChoice.ItemSelected += SelectSource;
        selectors.AddChild(Field("Source à réparer · optionnelle", _sourceChoice, 420));
        _newPath = StudioUx2EnvBControls.Text(Ux6QDefaultPathForUi);
        selectors.AddChild(Field("Nouvelle source .as", _newPath, 330));
        selectors.AddChild(ActionButton("Préparer", PrepareAssistant, 105));
        root.AddChild(selectors);

        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 500),
            CustomMaximumSize = new Vector2(-1, 730),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(tabsHost);
        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _tabs.TabChanged += OnTabChanged;
        tabsHost.AddChild(_tabs);

        var compilerPage = TabPage(_tabs, "Compilation moderne");
        compilerPage.AddChild(StudioStyle.Label("PROFIL COMPILATEUR MODERNE", 11, StudioStyle.Accent));
        compilerPage.AddChild(StudioStyle.Label(
            "La transaction remplace uniquement le compilateur. Les autres extensions, sources et métadonnées restent inchangées.",
            9, StudioStyle.Muted, wrap: true));
        var toggles = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _incremental = StudioUx2EnvBControls.Check("Compilation incrémentale");
        _stableDiagnostics = StudioUx2EnvBControls.Check("Diagnostics stables");
        _telemetry = StudioUx2EnvBControls.Check("Télémétrie locale");
        toggles.AddChild(_incremental);
        toggles.AddChild(_stableDiagnostics);
        toggles.AddChild(_telemetry);
        compilerPage.AddChild(toggles);
        _parallelism = StudioUx2EnvBControls.Text("4");
        _sourceQuota = StudioUx2EnvBControls.Text("524288");
        _suites = StudioUx2EnvBControls.Text("syntax; diagnostics; sandbox; incremental");
        compilerPage.AddChild(Pair("Workers parallèles (1–32)", _parallelism,
            "Quota source en octets", _sourceQuota));
        compilerPage.AddChild(Field("Suites de conformité (séparées par ;)", _suites, 720));
        _compilerInventory = StudioStyle.Label(
            "État vide · choisissez explicitement le profil pour inventorier les sources canoniques.",
            10, StudioStyle.Warning, wrap: true);
        compilerPage.AddChild(_compilerInventory);
        _compilerResults = new CodeEdit
        {
            Editable = false,
            CustomMinimumSize = new Vector2(0, 210),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            GuttersDrawLineNumbers = false,
        };
        _compilerResults.Text = "Aucune compilation exercée.";
        compilerPage.AddChild(_compilerResults);
        compilerPage.AddChild(StudioStyle.Label("DIAGNOSTICS LOCALISÉS", 10, StudioStyle.Accent));
        _compilerDiagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        compilerPage.AddChild(_compilerDiagnostics);
        compilerPage.AddChild(StudioStyle.Label(
            "L’aperçu compile des copies en mémoire et un cache temporaire supprimé après usage. Aucun bytecode n’est distribué par cette surface.",
            9, StudioStyle.Muted, wrap: true));

        var assistantPage = TabPage(_tabs, "Création assistée");
        assistantPage.AddChild(StudioStyle.Label("CRÉATION ASSISTÉE HORS LIGNE", 11, StudioStyle.Accent));
        assistantPage.AddChild(StudioStyle.Label(
            "Aucun fournisseur externe, réseau ou contenu distant. Le générateur local compile, teste la sandbox, puis exige un plan et une approbation humaine.",
            9, StudioStyle.Warning, wrap: true));
        _eventBinding = StudioUx2EnvBControls.Text("OnUse");
        _message = StudioUx2EnvBControls.Text("Une étincelle répond.");
        _variableKey = StudioUx2EnvBControls.Text("assistant.script.used");
        _variableValue = StudioUx2EnvBControls.Text("true");
        assistantPage.AddChild(Pair("Contexte d’événement", _eventBinding, "Message joueur", _message));
        assistantPage.AddChild(Pair("Variable", _variableKey, "Valeur", _variableValue));
        _intent = new TextEdit
        {
            Text = "Créer une interaction locale déterministe et lisible.",
            CustomMinimumSize = new Vector2(0, 68),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        assistantPage.AddChild(Field("Intention", _intent, 0));
        _citations = StudioUx2EnvBControls.Text("module:workspace");
        _styleRules = StudioUx2EnvBControls.Text("style:brief; sandbox:no-external-io");
        assistantPage.AddChild(Pair("Citations (séparées par ;)", _citations,
            "Règles de style", _styleRules));
        _author = StudioUx2EnvBControls.Text("@studio-author");
        _seed = StudioUx2EnvBControls.Text("20260821");
        assistantPage.AddChild(Pair("Auteur", _author, "Graine déterministe", _seed));

        var sourceColumns = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 225),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        sourceColumns.AddThemeConstantOverride("separation", 8);
        var overrideColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 1,
        };
        overrideColumn.AddChild(StudioStyle.Label("SOURCE DE RÉPARATION OPTIONNELLE", 9, StudioStyle.Accent));
        _sourceOverride = CodeEditor(editable: true);
        overrideColumn.AddChild(_sourceOverride);
        sourceColumns.AddChild(overrideColumn);
        var proposalColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 1,
        };
        proposalColumn.AddChild(StudioStyle.Label("PROPOSITION COMPILÉE · LECTURE SEULE", 9,
            StudioStyle.Accent));
        _proposalSource = CodeEditor(editable: false);
        proposalColumn.AddChild(_proposalSource);
        sourceColumns.AddChild(proposalColumn);
        assistantPage.AddChild(sourceColumns);
        _proposalEvidence = StudioStyle.Label("Aucune proposition locale.", 9, StudioStyle.Muted, wrap: true);
        assistantPage.AddChild(_proposalEvidence);
        assistantPage.AddChild(StudioStyle.Label("DIAGNOSTICS & TESTS SANDBOX", 10, StudioStyle.Accent));
        _assistantDiagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        assistantPage.AddChild(_assistantDiagnostics);

        var review = StudioStyle.Panel("Ux6QReview", StudioStyle.SurfaceRaised, 8, 1);
        review.CustomMinimumSize = new Vector2(0, 82);
        root.AddChild(review);
        var reviewMargin = StudioStyle.Margin(9, 7, 9, 7);
        review.AddChild(reviewMargin);
        var reviewRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reviewMargin.AddChild(reviewRows);
        _compilerReview = StudioStyle.Label("Compilateur · aucun aperçu sec canonique.", 9,
            StudioStyle.Muted, wrap: true);
        _assistantReview = StudioStyle.Label("Assistant · aucune proposition approuvée.", 9,
            StudioStyle.Muted, wrap: true);
        reviewRows.AddChild(_compilerReview);
        reviewRows.AddChild(_assistantReview);
        _status = StudioStyle.Label("UX6-Q prête · sélectionnez le profil ou préparez une source .as.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        _compilerActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _compilerActions.AddThemeConstantOverride("separation", 7);
        _compilerActions.AddChild(ActionButton("Aperçu profil", PreviewCompiler, 124));
        _compilerActions.AddChild(ActionButton("Exercer compilateur", ExerciseCompiler, 158));
        _applyCompiler = ActionButton("Appliquer profil", ApplyCompiler, 136, primary: true);
        _undoCompiler = ActionButton("Annuler projet", UndoCompiler, 120);
        _redoCompiler = ActionButton("Rétablir projet", RedoCompiler, 128);
        _compilerActions.AddChild(_applyCompiler);
        _compilerActions.AddChild(_undoCompiler);
        _compilerActions.AddChild(_redoCompiler);
        _compilerActions.AddChild(ActionButton("Enregistrer projet", SaveCompiler, 146));
        root.AddChild(_compilerActions);

        _assistantActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _assistantActions.AddThemeConstantOverride("separation", 7);
        _assistantActions.AddChild(ActionButton("Proposer hors ligne", ProposeAssistant, 160));
        _assistantActions.AddChild(ActionButton("Prévisualiser & approuver", PreviewAssistant, 198));
        _applyAssistant = ActionButton("Appliquer source", ApplyAssistant, 140, primary: true);
        _undoAssistant = ActionButton("Annuler source", UndoAssistant, 126);
        _redoAssistant = ActionButton("Rétablir source", RedoAssistant, 134);
        _assistantActions.AddChild(_applyAssistant);
        _assistantActions.AddChild(_undoAssistant);
        _assistantActions.AddChild(_redoAssistant);
        root.AddChild(_assistantActions);

        HookEditors();
        OnTabChanged(0);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized && IsInsideTree()) UpdateBounds();
    }

    internal void Open() => OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CloseUx6Q();
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        ResetControls();
    }

    internal void CloseEvidence()
    {
        Visible = false;
        ReleaseEvidence();
        _session = _sourceSession;
        ResetControls();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!mode.StartsWith("ux6-q", StringComparison.OrdinalIgnoreCase))
            return FailEvidence("Mode de preuve UX6-Q inconnu.");
        ReleaseEvidence();
        _sourceSnapshot = CaptureSource();
        if (_sourceSnapshot is null || _sourceSession.Project is null)
            return FailEvidence("Le module source n’est pas ouvert.");
        CaptureSourceAssistantFiles();
        _evidenceRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-q-ui",
            Guid.NewGuid().ToString("N"));
        if (!ProjectSession.Create(_evidenceRoot, _sourceSession.Project).Succeeded)
            return FailEvidence("La copie temporaire UX6-Q ne peut pas être créée.");
        CreateEvidenceSources(_evidenceRoot);
        _evidenceSession = new StudioSessionController();
        if (!_evidenceSession.Open(_evidenceRoot).Succeeded)
            return FailEvidence("La copie temporaire UX6-Q ne peut pas être ouverte.");
        _session = _evidenceSession;
        OpenBound(_session, evidence: true, cancelApproval: false);

        _profileChoice.Select(1);
        SelectProfile(1);
        if (_compilerDraft is null) return FailEvidence("Le profil compilateur ne peut pas être chargé.");
        _parallelism.Text = (_compilerDraft.MaxParallelism < 32
            ? _compilerDraft.MaxParallelism + 1
            : _compilerDraft.MaxParallelism - 1).ToString(CultureInfo.InvariantCulture);
        _suites.Text = string.Join("; ", _compilerDraft.ConformanceSuites.Append("ux6-q")
            .Distinct(StringComparer.Ordinal));
        InvalidateCompiler();
        PreviewCompiler();
        ExerciseCompiler();

        _newPath.Text = Ux6QDefaultPathForUi;
        PrepareAssistant();
        if (_assistantDraft is null) return FailEvidence("Le brouillon assisté ne peut pas être créé.");
        ProposeAssistant();
        PreviewAssistant();
        _tabs.CurrentTab = mode.Contains("assistant", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        OnTabChanged(_tabs.CurrentTab);

        var ready = UsesIsolatedEvidence && HasExplicitSelection && HumanControlsReady &&
                    CompilerPreviewReady && CompilerEvidenceReady && ProposalReady &&
                    AssistantPreviewReady && ShowsHumanSurface && EvidenceSourceUntouched;
        if (!ready)
        {
            var compiler = _compilation;
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_Q_UI_FAILED " +
                        $"isolated={UsesIsolatedEvidence.ToString().ToLowerInvariant()} " +
                        $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                        $"controls={HumanControlsReady.ToString().ToLowerInvariant()} " +
                        $"compiler_preview={CompilerPreviewReady.ToString().ToLowerInvariant()} " +
                        $"compiler_evidence={CompilerEvidenceReady.ToString().ToLowerInvariant()} " +
                        $"proposal={ProposalReady.ToString().ToLowerInvariant()} " +
                        $"assistant_preview={AssistantPreviewReady.ToString().ToLowerInvariant()} " +
                        $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                        $"source={EvidenceSourceUntouched.ToString().ToLowerInvariant()} " +
                        $"compiler_success={(compiler?.Succeeded == true).ToString().ToLowerInvariant()} " +
                        $"inventory={(compiler?.InventoryPresent == true).ToString().ToLowerInvariant()} " +
                        $"dependency={(compiler?.DependencyAware == true).ToString().ToLowerInvariant()} " +
                        $"cache={(compiler?.CacheReused == true).ToString().ToLowerInvariant()} " +
                        $"invalidation={(compiler?.DependencyInvalidated == true).ToString().ToLowerInvariant()} " +
                        $"diagnostics={(compiler?.StableDiagnostics == true).ToString().ToLowerInvariant()} " +
                        $"telemetry={(compiler?.Telemetry == true).ToString().ToLowerInvariant()} " +
                        $"conformance={(compiler?.Conformance == true).ToString().ToLowerInvariant()} " +
                        $"cache_released={(compiler?.TemporaryCacheReleased == true).ToString().ToLowerInvariant()} " +
                        $"sources={compiler?.SourceCount ?? 0} " +
                        $"first_compiled={compiler?.FirstCompiled ?? 0} " +
                        $"second_reused={compiler?.SecondReused ?? 0} " +
                        $"changed_compiled={compiler?.ChangedCompiled ?? 0} " +
                        $"compiler_diagnostics={string.Join(',', compiler?.Diagnostics.Select(item => item.Code) ?? [])} " +
                        $"compiler_message={compiler?.Message ?? "missing"}");
            return FailEvidence("La surface UX6-Q ne satisfait pas toutes les preuves UI.");
        }
        return true;
    }

    internal bool RunUiTransactionEvidence()
    {
        if (!PrepareEvidence("ux6-q-compiler-assistant")) return false;
        try
        {
            if (_evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !CompilerPreviewReady || !CompilerEvidenceReady || !AssistantPreviewReady ||
                _compilerDraft is null || _assistantDraft is null)
                return false;
            var assistantApply = _session.ApplyApprovedAssistantUx6Q(ReadAssistantDraft()!);
            var sourcePath = AssistedForgeScriptCreationService.ResolvePath(_evidenceRoot!,
                _assistantDraft.RelativePath);
            var assistantUndo = _session.UndoAssistantUx6Q();
            var removed = !File.Exists(sourcePath) && !File.Exists(sourcePath + ".meta.json");
            var assistantRedo = _session.RedoAssistantUx6Q();
            var persisted = new ForgeScriptSourceStore(_evidenceRoot!).Load(_assistantDraft.RelativePath);
            var compilerApply = _session.ApplyApprovedCompilerUx6Q();
            var compilerUndo = _session.UndoCompilerUx6Q();
            var compilerRedo = _session.RedoCompilerUx6Q();
            var compilerSave = _session.SaveCompilerUx6Q();
            return compilerApply.Succeeded && compilerUndo.Succeeded && compilerRedo.Succeeded &&
                   compilerSave.Succeeded && assistantApply.Succeeded && assistantUndo.Succeeded &&
                   assistantRedo.Succeeded && removed && persisted.Source == _proposal?.Source &&
                   EvidenceSourceUntouched;
        }
        finally
        {
            CloseEvidence();
        }
    }

    private void OpenBound(StudioSessionController session, bool evidence, bool cancelApproval)
    {
        _session = session;
        if (cancelApproval) _session.CloseUx6Q();
        ResetControls();
        PopulateChoices();
        Visible = true;
        UpdateBounds();
        SetStatus(evidence
            ? "Copie temporaire UX6-Q · source strictement inchangée."
            : "Choisissez explicitement le profil compilateur ou une source .as.", false);
    }

    private void PopulateChoices()
    {
        _updating = true;
        _profileChoice.Clear();
        _profileChoice.AddItem("Choisir le profil compilateur…", 0);
        foreach (var choice in _session.CompilerProfileChoicesUx6Q()) _profileChoice.AddItem(choice.Label);
        _profileChoice.Select(0);
        _sourceChoices = _session.AssistantSourceChoicesUx6Q();
        _sourceChoice.Clear();
        _sourceChoice.AddItem("Nouvelle source ou réparation…", 0);
        foreach (var choice in _sourceChoices) _sourceChoice.AddItem(choice.Label);
        _sourceChoice.Select(0);
        _updating = false;
        UpdateActions();
    }

    private void SelectProfile(long index)
    {
        if (_updating) return;
        _compilerDraft = index <= 0 ? null : _session.CreateCompilerDraftUx6Q(explicitlySelected: true);
        _explicitProfile = _compilerDraft is not null;
        if (_compilerDraft is not null) PopulateCompiler(_compilerDraft);
        else ResetCompilerControls();
        InvalidateCompiler();
        UpdateActions();
    }

    private void SelectSource(long index)
    {
        if (_updating || index <= 0 || index > _sourceChoices.Count) return;
        var choice = _sourceChoices[(int)index - 1];
        if (!choice.Canonical)
        {
            SetStatus("Cette source est incomplète et reste protégée.", true);
            return;
        }
        _newPath.Text = choice.RelativePath;
        PrepareAssistant();
    }

    private void PrepareAssistant()
    {
        if (!_session.TryCreateAssistantDraftUx6Q(_newPath.Text, out var draft, out var message) || draft is null)
        {
            _assistantDraft = null;
            _explicitAssistant = false;
            SetStatus(message, true);
            UpdateActions();
            return;
        }
        _assistantDraft = draft;
        _explicitAssistant = true;
        PopulateAssistant(draft);
        InvalidateAssistant();
        SetStatus(message, false);
        UpdateActions();
    }

    private void PreviewCompiler()
    {
        var draft = ReadCompilerDraft();
        if (draft is null)
        {
            SetStatus("Choisissez explicitement le profil compilateur.", true);
            return;
        }
        _compilerPreview = _session.PreviewCompilerUx6Q(draft);
        _compilation = _compilerPreview.Compilation;
        RenderCompiler();
        SetStatus(_compilerPreview.Message, !_compilerPreview.Succeeded);
        UpdateActions();
    }

    private void ExerciseCompiler()
    {
        var draft = ReadCompilerDraft();
        if (draft is null)
        {
            SetStatus("Choisissez explicitement le profil compilateur.", true);
            return;
        }
        _compilation = _session.RunCompilerCompilationUx6Q(draft);
        RenderCompiler();
        SetStatus(_compilation.Message, !_compilation.Succeeded);
        UpdateActions();
    }

    private void ApplyCompiler() => ApplyAction(_session.ApplyApprovedCompilerUx6Q(), compiler: true);
    private void UndoCompiler() => ApplyAction(_session.UndoCompilerUx6Q(), compiler: true);
    private void RedoCompiler() => ApplyAction(_session.RedoCompilerUx6Q(), compiler: true);
    private void SaveCompiler() => ApplyAction(_session.SaveCompilerUx6Q(), compiler: true);

    private void ProposeAssistant()
    {
        var draft = ReadAssistantDraft();
        if (draft is null)
        {
            SetStatus("Préparez explicitement une source .as.", true);
            return;
        }
        _proposal = _session.GenerateAssistantProposalUx6Q(draft);
        _proposalSource.Text = _proposal.Source;
        _proposalEvidence.Text = _proposal.Succeeded
            ? $"API {_proposal.ApiVersion} · VM {_proposal.VirtualMachineVersion} · fournisseur {_proposal.Provider} · coût {_proposal.EstimatedCostUsd:0.0000} USD · {_proposal.Tests.Count} test(s)."
            : _proposal.Message;
        RenderAssistant();
        SetStatus(_proposal.Message, !_proposal.Succeeded);
        UpdateActions();
    }

    private void PreviewAssistant()
    {
        var draft = ReadAssistantDraft();
        if (draft is null)
        {
            SetStatus("Préparez explicitement une source .as.", true);
            return;
        }
        _assistantPreview = _session.PreviewAssistantUx6Q(draft);
        RenderAssistant();
        SetStatus(_assistantPreview.Message, !_assistantPreview.Succeeded);
        UpdateActions();
    }

    private void ApplyAssistant()
    {
        var draft = ReadAssistantDraft();
        if (draft is null)
        {
            SetStatus("Préparez explicitement une source .as.", true);
            return;
        }
        ApplyAction(_session.ApplyApprovedAssistantUx6Q(draft), compiler: false);
    }

    private void UndoAssistant() => ApplyAction(_session.UndoAssistantUx6Q(), compiler: false);
    private void RedoAssistant() => ApplyAction(_session.RedoAssistantUx6Q(), compiler: false);

    private void ApplyAction(StudioUx6QActionResult result, bool compiler)
    {
        if (result.Succeeded)
        {
            if (compiler)
            {
                _compilerPreview = null;
                _compilation = null;
                _compilerReview.Text = "Compilateur · rechargez puis relancez l’aperçu sec.";
            }
            else
            {
                _assistantPreview = null;
                _assistantReview.Text = "Assistant · transaction terminée; rechargez avant une nouvelle proposition.";
            }
        }
        if (!compiler)
        {
            _assistantCanUndo = result.CanUndo;
            _assistantCanRedo = result.CanRedo;
        }
        SetStatus(result.Message, !result.Succeeded);
        UpdateActions(result, compiler);
    }

    private StudioUx6QCompilerDraft? ReadCompilerDraft()
    {
        if (_compilerDraft is null ||
            !int.TryParse(_parallelism.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var parallelism) ||
            !int.TryParse(_sourceQuota.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var quota)) return null;
        return _compilerDraft with
        {
            Incremental = _incremental.ButtonPressed,
            StableDiagnostics = _stableDiagnostics.ButtonPressed,
            PerformanceTelemetry = _telemetry.ButtonPressed,
            MaxParallelism = parallelism,
            MaxSourceBytes = quota,
            ConformanceSuites = Split(_suites.Text),
        };
    }

    private StudioUx6QAssistantDraft? ReadAssistantDraft()
    {
        if (_assistantDraft is null ||
            !int.TryParse(_seed.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
            return null;
        return _assistantDraft with
        {
            RelativePath = _newPath.Text.Trim().Replace('\\', '/'),
            EventBinding = _eventBinding.Text,
            Intent = _intent.Text,
            Message = _message.Text,
            VariableKey = _variableKey.Text,
            VariableValue = _variableValue.Text,
            ContextCitations = Split(_citations.Text),
            StyleRules = Split(_styleRules.Text),
            AuthorId = _author.Text,
            Seed = seed,
            SourceOverride = _sourceOverride.Text,
        };
    }

    private void PopulateCompiler(StudioUx6QCompilerDraft draft)
    {
        _updating = true;
        _incremental.ButtonPressed = draft.Incremental;
        _stableDiagnostics.ButtonPressed = draft.StableDiagnostics;
        _telemetry.ButtonPressed = draft.PerformanceTelemetry;
        _parallelism.Text = draft.MaxParallelism.ToString(CultureInfo.InvariantCulture);
        _sourceQuota.Text = draft.MaxSourceBytes.ToString(CultureInfo.InvariantCulture);
        _suites.Text = string.Join("; ", draft.ConformanceSuites);
        var sources = _session.AssistantSourceChoicesUx6Q().Count(item => item.Canonical);
        _compilerInventory.Text = sources == 0
            ? "État vide · aucune source .as canonique; le compilateur reste configurable sans inventer de script."
            : $"Inventaire canonique · {sources} source(s) .as avec métadonnées; dépendances lues depuis leurs documents.";
        _updating = false;
    }

    private void PopulateAssistant(StudioUx6QAssistantDraft draft)
    {
        _updating = true;
        _newPath.Text = draft.RelativePath;
        _eventBinding.Text = draft.EventBinding;
        _intent.Text = draft.Intent;
        _message.Text = draft.Message;
        _variableKey.Text = draft.VariableKey;
        _variableValue.Text = draft.VariableValue;
        _citations.Text = string.Join("; ", draft.ContextCitations);
        _styleRules.Text = string.Join("; ", draft.StyleRules);
        _author.Text = draft.AuthorId;
        _seed.Text = draft.Seed.ToString(CultureInfo.InvariantCulture);
        _sourceOverride.Text = draft.SourceOverride;
        _proposalSource.Text = string.Empty;
        _proposalEvidence.Text = draft.NewSource
            ? "Nouvelle source · aucune écriture avant plan approuvé."
            : "Réparation d’une source canonique · identité, bindings, dépendances et capacités préservés.";
        _updating = false;
    }

    private void RenderCompiler()
    {
        ClearChildren(_compilerDiagnostics);
        var compilation = _compilation;
        if (compilation is not null)
        {
            _compilerResults.Text = string.Join('\n', new[]
            {
                $"Sources : {compilation.SourceCount}",
                $"Premier passage compilé : {compilation.FirstCompiled}",
                $"Second passage réutilisé : {compilation.SecondReused}",
                $"Après dépendance modifiée : {compilation.ChangedCompiled}",
                $"Cache : {(compilation.CacheReused ? "réutilisé" : "incomplet")}",
                $"Conformité : {(compilation.Conformance ? "validée" : "incomplète")}",
                $"Cache temporaire : {(compilation.TemporaryCacheReleased ? "libéré" : "à vérifier")}",
            }.Concat(compilation.Entries));
            foreach (var diagnostic in compilation.Diagnostics) AddDiagnostic(_compilerDiagnostics, diagnostic);
        }
        if (_compilerPreview is not null)
        {
            foreach (var diagnostic in _compilerPreview.Diagnostics) AddDiagnostic(_compilerDiagnostics, diagnostic);
            _compilerReview.Text = $"Compilateur · {_compilerPreview.Message} · " +
                                   $"{_compilerPreview.Changes.Count} changement(s).";
        }
        else if (compilation is not null)
        {
            _compilerReview.Text = $"Compilateur · {compilation.Message}";
        }
    }

    private void RenderAssistant()
    {
        ClearChildren(_assistantDiagnostics);
        if (_proposal is not null)
        {
            foreach (var test in _proposal.Tests)
                _assistantDiagnostics.AddChild(StudioStyle.Label($"• {test}", 9, StudioStyle.Text, wrap: true));
            foreach (var diagnostic in _proposal.Diagnostics) AddDiagnostic(_assistantDiagnostics, diagnostic);
        }
        if (_assistantPreview is not null)
        {
            foreach (var diagnostic in _assistantPreview.Diagnostics) AddDiagnostic(_assistantDiagnostics, diagnostic);
            _assistantReview.Text = $"Assistant · {_assistantPreview.Message}";
        }
        else if (_proposal is not null)
        {
            _assistantReview.Text = $"Assistant · {_proposal.Message}";
        }
    }

    private void HookEditors()
    {
        foreach (var check in new[] { _incremental, _stableDiagnostics, _telemetry })
            check.Toggled += _ => InvalidateCompiler();
        foreach (var edit in new[] { _parallelism, _sourceQuota, _suites })
            edit.TextChanged += _ => InvalidateCompiler();
        foreach (var edit in new[]
                 {
                     _newPath, _eventBinding, _message, _variableKey, _variableValue, _citations,
                     _styleRules, _author, _seed,
                 })
            edit.TextChanged += _ => InvalidateAssistant();
        _intent.TextChanged += InvalidateAssistant;
        _sourceOverride.TextChanged += InvalidateAssistant;
    }

    private void InvalidateCompiler()
    {
        if (_updating) return;
        _compilerPreview = null;
        _compilation = null;
        _compilerReview.Text = "Compilateur · brouillon modifié; relancez l’aperçu sec.";
        UpdateActions();
    }

    private void InvalidateAssistant()
    {
        if (_updating) return;
        _proposal = null;
        _assistantPreview = null;
        _proposalSource.Text = string.Empty;
        _assistantReview.Text = "Assistant · brouillon modifié; régénérez la proposition.";
        UpdateActions();
    }

    private void ResetControls()
    {
        _compilerDraft = null;
        _assistantDraft = null;
        _sourceChoices = [];
        _compilerPreview = null;
        _compilation = null;
        _proposal = null;
        _assistantPreview = null;
        _explicitProfile = false;
        _explicitAssistant = false;
        _assistantCanUndo = false;
        _assistantCanRedo = false;
        _profileChoice.Clear();
        _sourceChoice.Clear();
        _newPath.Text = Ux6QDefaultPathForUi;
        ResetCompilerControls();
        _sourceOverride.Text = string.Empty;
        _proposalSource.Text = string.Empty;
        _compilerResults.Text = "Aucune compilation exercée.";
        _proposalEvidence.Text = "Aucune proposition locale.";
        _compilerReview.Text = "Compilateur · aucun aperçu sec canonique.";
        _assistantReview.Text = "Assistant · aucune proposition approuvée.";
        ClearChildren(_compilerDiagnostics);
        ClearChildren(_assistantDiagnostics);
        UpdateActions();
    }

    private void ResetCompilerControls()
    {
        _updating = true;
        _incremental.ButtonPressed = true;
        _stableDiagnostics.ButtonPressed = true;
        _telemetry.ButtonPressed = true;
        _parallelism.Text = "4";
        _sourceQuota.Text = "524288";
        _suites.Text = "syntax; diagnostics; sandbox; incremental";
        _compilerInventory.Text = "État vide · sélection explicite requise.";
        _updating = false;
    }

    private void UpdateActions(StudioUx6QActionResult? result = null, bool? compilerResult = null)
    {
        _applyCompiler.Disabled = !CompilerPreviewReady;
        _undoCompiler.Disabled = compilerResult == true && result is not null
            ? !result.CanUndo
            : !_session.CanUndo;
        _redoCompiler.Disabled = compilerResult == true && result is not null
            ? !result.CanRedo
            : !_session.CanRedo;
        _applyAssistant.Disabled = !AssistantPreviewReady;
        _undoAssistant.Disabled = !_assistantCanUndo;
        _redoAssistant.Disabled = !_assistantCanRedo;
    }

    private void OnTabChanged(long tab)
    {
        _compilerActions.Visible = tab == 0;
        _assistantActions.Visible = tab == 1;
    }

    private StudioUx6QSourceSnapshot? CaptureSource()
    {
        var history = _sourceSession.History;
        var project = _sourceSession.Project;
        if (history is null || project is null) return null;
        return new(project, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.CaptureScriptApprovalUx6F(),
            _sourceSession.CaptureExecutionPolicyApprovalUx6J(),
            _sourceSession.CaptureModulePermissionApprovalUx6K(),
            _sourceSession.CaptureScriptAutomationApprovalUx6M(),
            _sourceSession.CaptureScriptQualityApprovalUx6O(),
            _sourceSession.CaptureScriptInteropApprovalUx6P(),
            _sourceSession.CaptureCompilerAssistantApprovalUx6Q());
    }

    private bool SourceMatches()
    {
        var snapshot = _sourceSnapshot;
        var history = _sourceSession.History;
        return snapshot is not null && history is not null &&
               ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.CaptureScriptApprovalUx6F() == snapshot.SourceApproval &&
               _sourceSession.CaptureExecutionPolicyApprovalUx6J() == snapshot.ExecutionApproval &&
               _sourceSession.CaptureModulePermissionApprovalUx6K() == snapshot.PermissionApproval &&
               _sourceSession.CaptureScriptAutomationApprovalUx6M() == snapshot.AutomationApproval &&
               _sourceSession.CaptureScriptQualityApprovalUx6O() == snapshot.QualityApproval &&
               _sourceSession.CaptureScriptInteropApprovalUx6P() == snapshot.InteropApproval &&
               _sourceSession.CaptureCompilerAssistantApprovalUx6Q() == snapshot.CompilerAssistantApproval;
    }

    private void CaptureSourceAssistantFiles()
    {
        var root = _sourceSession.Session?.RootDirectory;
        if (root is null) return;
        var path = AssistedForgeScriptCreationService.ResolvePath(root, Ux6QDefaultPathForUi);
        var metadata = path + ".meta.json";
        _sourceAssistantExisted = File.Exists(path);
        _sourceAssistantMetadataExisted = File.Exists(metadata);
        _sourceAssistantBytes = _sourceAssistantExisted ? File.ReadAllBytes(path) : null;
        _sourceAssistantMetadataBytes = _sourceAssistantMetadataExisted ? File.ReadAllBytes(metadata) : null;
    }

    private bool SourceAssistantMatches()
    {
        var root = _sourceSession.Session?.RootDirectory;
        if (root is null) return false;
        var path = AssistedForgeScriptCreationService.ResolvePath(root, Ux6QDefaultPathForUi);
        var metadata = path + ".meta.json";
        return File.Exists(path) == _sourceAssistantExisted &&
               File.Exists(metadata) == _sourceAssistantMetadataExisted &&
               (!_sourceAssistantExisted || File.ReadAllBytes(path).SequenceEqual(_sourceAssistantBytes!)) &&
               (!_sourceAssistantMetadataExisted ||
                File.ReadAllBytes(metadata).SequenceEqual(_sourceAssistantMetadataBytes!));
    }

    private void ReleaseEvidence()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        if (_evidenceRoot is not null)
        {
            try
            {
                if (Directory.Exists(_evidenceRoot)) Directory.Delete(_evidenceRoot, true);
                _evidenceReleased = !Directory.Exists(_evidenceRoot);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _evidenceReleased = false;
                GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_Q_TEMP_RELEASE_FAILED {exception.Message}");
            }
        }
        else
        {
            _evidenceReleased = true;
        }
        _evidenceRoot = null;
        _session = _sourceSession;
    }

    private bool FailEvidence(string message)
    {
        SetStatus(message, true);
        ReleaseEvidence();
        return false;
    }

    private static void CreateEvidenceSources(string root)
    {
        var store = new ForgeScriptSourceStore(root);
        var shared = new ForgeScriptSourceDocument(Guid.NewGuid(), 1, "studio:ux6-q:evidence",
            "scripts/ux6-q-ui-shared.as", "void main() { say(\"shared\"); }\n", "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode, false, [], []);
        var main = new ForgeScriptSourceDocument(Guid.NewGuid(), 1, "studio:ux6-q:evidence",
            "scripts/ux6-q-ui-main.as", "void main() { say(\"main\"); }\n", "1.0.0",
            ForgeScriptSourceDistributionPolicy.SourceAndBytecode, false, [shared.RelativePath], []);
        store.Save(shared);
        store.Save(main);
    }

    private void UpdateBounds()
    {
        var viewport = GetViewportRect().Size;
        var width = Mathf.Clamp(viewport.X - 28, 1180, 1900);
        var height = Mathf.Clamp(viewport.Y - 24, 720, 1120);
        _dialog.Position = new Vector2((viewport.X - width) * .5f, (viewport.Y - height) * .5f);
        _dialog.Size = new Vector2(width, height);
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(message, error);
    }

    private static VBoxContainer TabPage(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var margin = StudioStyle.Margin(10, 9, 10, 12);
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(margin);
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 7);
        margin.AddChild(page);
        tabs.AddChild(scroll);
        return page;
    }

    private static CodeEdit CodeEditor(bool editable) => new()
    {
        Editable = editable,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        SizeFlagsVertical = SizeFlags.ExpandFill,
        CustomMinimumSize = new Vector2(0, 190),
        GuttersDrawLineNumbers = true,
        DrawTabs = true,
    };

    private static VBoxContainer Field(string label, Control control, float width)
    {
        var field = new VBoxContainer { SizeFlagsHorizontal = width <= 0 ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin };
        field.AddThemeConstantOverride("separation", 3);
        field.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        if (width > 0) control.CustomMinimumSize = new Vector2(width, Math.Max(34, control.CustomMinimumSize.Y));
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        field.AddChild(control);
        return field;
    }

    private static HBoxContainer Pair(string leftLabel, Control left, string rightLabel, Control right)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(Field(leftLabel, left, 0));
        row.AddChild(Field(rightLabel, right, 0));
        return row;
    }

    private static Button ActionButton(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static void AddDiagnostic(VBoxContainer target, StudioUx6QDiagnostic diagnostic)
    {
        target.AddChild(StudioStyle.Label(
            $"{HumanDiagnostic(diagnostic.Code)} · {diagnostic.Message}", 9,
            diagnostic.Severity.Equals("error", StringComparison.OrdinalIgnoreCase)
                ? StudioStyle.Danger
                : StudioStyle.Warning,
            wrap: true));
    }

    private static string HumanDiagnostic(string code) => code switch
    {
        "studio.compiler.quotas_invalid" => "Quotas compilateur invalides",
        "assistant.script.preview_failed" => "Plan assisté refusé",
        _ => code.Replace('.', ' ').Replace('_', ' '),
    };

    private static IReadOnlyList<string> Split(string value) => value.Split(';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal).ToArray();

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private static IEnumerable<string> DescendantText(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            switch (child)
            {
                case Label label when !string.IsNullOrWhiteSpace(label.Text):
                    yield return label.Text;
                    break;
                case Button button when !string.IsNullOrWhiteSpace(button.Text):
                    yield return button.Text;
                    break;
                case CheckBox check when !string.IsNullOrWhiteSpace(check.Text):
                    yield return check.Text;
                    break;
                case OptionButton options:
                    for (var index = 0; index < options.ItemCount; index++) yield return options.GetItemText(index);
                    break;
            }
            foreach (var text in DescendantText(child)) yield return text;
        }
    }

    private static bool ContainsUuid(string value) => Regex.IsMatch(value,
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}",
        RegexOptions.CultureInvariant);

    private const string Ux6QDefaultPathForUi = "scripts/assisted-interaction.as";
}
