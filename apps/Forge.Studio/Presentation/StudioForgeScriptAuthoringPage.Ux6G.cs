using System.Text.RegularExpressions;
using Forge.ModuleFormat.Workspaces;
using Forge.RpgKit.Domain.Scripting;
using Forge.Scripting;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioForgeScriptAuthoringPageUx6G : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx6FEvidenceSourceSnapshot? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _sourceChoice;
    private readonly LineEdit _newSourcePath;
    private readonly Button _createSource;
    private readonly Label _selection;
    private readonly CodeEdit _source;
    private readonly OptionButton _snippet;
    private readonly VBoxContainer _completionRows;
    private readonly Label _signature;
    private readonly VBoxContainer _diagnosticRows;
    private readonly LineEdit _wizardFunction;
    private readonly LineEdit _wizardCondition;
    private readonly OptionButton _wizardAction;
    private readonly LineEdit _wizardValue;
    private readonly SpinBox _wizardPriority;
    private readonly SpinBox _wizardAmount;
    private readonly SpinBox _wizardDuration;
    private readonly SpinBox _wizardSeed;
    private readonly Label _wizardReview;
    private readonly LineEdit _taskAction;
    private readonly SpinBox _taskPriority;
    private readonly LineEdit _ability;
    private readonly SpinBox _damage;
    private readonly SpinBox _armorPenetration;
    private readonly SpinBox _seed;
    private readonly LineEdit _effectId;
    private readonly SpinBox _magnitude;
    private readonly SpinBox _duration;
    private readonly SpinBox _period;
    private readonly OptionButton _stacking;
    private readonly Label _runtimeReview;
    private readonly Label _console;
    private readonly Label _status;
    private readonly Button _analyze;
    private readonly Button _format;
    private readonly Button _generateWizard;
    private readonly Button _insertWizard;
    private readonly Button _simulate;
    private readonly Button _preview;
    private readonly Button _compile;
    private readonly Button _sandbox;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;

    private IReadOnlyList<StudioUx6FSourceChoice> _choices = [];
    private StudioUx6FDraft? _draft;
    private StudioUx6GEditorAnalysis? _analysisResult;
    private StudioUx6GWizardResult? _wizardResult;
    private StudioUx6GGameplayResult? _gameplayResult;
    private StudioUx6FPreview? _previewResult;
    private StudioUx6FCompileResult? _compileResult;
    private StudioUx6FSandboxResult? _sandboxResult;
    private bool _explicitSource;
    private bool _updating;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? ScriptSourceChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSource && _draft is not null;
    internal bool PreviewReady => _previewResult is
    { Succeeded: true, CanApply: true, CanonicalDryRun: true };
    internal bool EditorReady => _analysisResult is
    { Succeeded: true, SyntaxSpanCount: > 0 };
    internal bool WizardReady => _wizardResult is { Succeeded: true };
    internal bool GameplayReady => _gameplayResult is
    { Succeeded: true, TaskCompleted: true, CombatCommitted: true, EffectRoundTrip: true };
    internal bool NativeExecutionReady => _compileResult is { Succeeded: true } &&
                                          _sandboxResult is { Succeeded: true };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => SourceMatchesUx6G();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx6G(this));
            return text.Contains("Éditeur", StringComparison.Ordinal) &&
                   text.Contains("Script Wizard", StringComparison.Ordinal) &&
                   text.Contains("Actions différées", StringComparison.Ordinal) &&
                   text.Contains("Combat", StringComparison.Ordinal) &&
                   text.Contains("Effets", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec", StringComparison.Ordinal) &&
                   !ContainsUuidUx6G(text);
        }
    }

    internal StudioForgeScriptAuthoringPageUx6G(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.008f, .014f, .023f, .985f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6GForgeScriptAuthoring", StudioStyle.Surface, 12, 1);
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
        headingText.AddChild(StudioStyle.Label("ForgeScript · éditeur, Wizard et APIs gameplay", 19));
        headingText.AddChild(StudioStyle.Label(
            "Éditeur assisté · Script Wizard revu · Actions différées · Combat · Effets · même source canonique UX6-F.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        var sourceRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sourceChoice = StudioUx2EnvBControls.Options();
        _sourceChoice.CustomMinimumSize = new Vector2(430, 34);
        _sourceChoice.ItemSelected += SelectSourceUx6G;
        sourceRow.AddChild(FieldUx6G("Source canonique · sélection explicite", _sourceChoice, 430));
        _newSourcePath = StudioUx2EnvBControls.Text("scripts/authoring-gameplay.as");
        _newSourcePath.CustomMinimumSize = new Vector2(300, 34);
        sourceRow.AddChild(FieldUx6G("Créer si le module est vide", _newSourcePath, 300));
        _createSource = StudioStyle.Button("Créer en mémoire", compact: true);
        _createSource.CustomMinimumSize = new Vector2(145, 34);
        _createSource.Pressed += CreateSourceUx6G;
        sourceRow.AddChild(_createSource);
        var reload = StudioStyle.Button("Recharger", compact: true);
        reload.CustomMinimumSize = new Vector2(120, 34);
        reload.Pressed += RefreshSourcesUx6G;
        sourceRow.AddChild(reload);
        root.AddChild(sourceRow);
        _selection = StudioStyle.Label(
            "Choisissez une source .as. Les scripts .forge historiques restent protégés dans l’atelier fondation.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var body = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 455),
            CustomMaximumSize = new Vector2(-1, 700),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            PropagateMaximumSize = true,
        };
        body.AddThemeConstantOverride("separation", 9);
        root.AddChild(body);

        var left = PanelColumnUx6G(body, 315);
        left.AddChild(StudioStyle.Label("ÉDITEUR ASSISTÉ", 10, StudioStyle.Accent));
        left.AddChild(StudioStyle.Label(
            "Analyse locale : coloration, complétions, signatures, documentation, symboles, snippets, formatage et diagnostics cliquables.",
            9, StudioStyle.Muted, wrap: true));
        var editorActions = new HBoxContainer();
        _analyze = StudioStyle.Button("Analyser", compact: true);
        _analyze.Pressed += AnalyzeUx6G;
        _format = StudioStyle.Button("Formater", compact: true);
        _format.Pressed += FormatUx6G;
        editorActions.AddChild(_analyze);
        editorActions.AddChild(_format);
        left.AddChild(editorActions);
        _snippet = StudioUx2EnvBControls.Options("main", "handler", "deferred", "combat", "effect");
        var snippetRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        snippetRow.AddChild(_snippet);
        var insertSnippet = StudioStyle.Button("Insérer snippet", compact: true);
        insertSnippet.Pressed += InsertSnippetUx6G;
        snippetRow.AddChild(insertSnippet);
        left.AddChild(snippetRow);
        _signature = StudioStyle.Label("Signature · lancez l’analyse au curseur.", 9, StudioStyle.Muted, wrap: true);
        left.AddChild(_signature);
        left.AddChild(StudioStyle.Label("COMPLÉTIONS & DOCUMENTATION", 9, StudioStyle.Accent));
        var completionScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 120),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _completionRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        completionScroll.AddChild(_completionRows);
        left.AddChild(completionScroll);
        left.AddChild(StudioStyle.Label("DIAGNOSTICS CLIQUABLES", 9, StudioStyle.Accent));
        var diagnosticScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 115),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _diagnosticRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        diagnosticScroll.AddChild(_diagnosticRows);
        left.AddChild(diagnosticScroll);

        var center = PanelColumnUx6G(body, 620, expand: true);
        center.AddChild(StudioStyle.Label("SOURCE REVUE · AUTORITÉ UNIQUE UX6-F", 10, StudioStyle.Accent));
        _source = new CodeEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(600, 390),
            DrawTabs = true,
            GuttersDrawLineNumbers = true,
        };
        var highlighter = new CodeHighlighter
        {
            SymbolColor = new Color("89ddff"),
            NumberColor = new Color("f78c6c"),
            FunctionColor = new Color("82aaff"),
        };
        foreach (var keyword in StudioSessionController.EditorContextUx6G().Keywords)
            highlighter.AddKeywordColor(keyword, new Color("c792ea"));
        foreach (var api in ForgeScriptApiCatalog.Registrations.Take(64))
            highlighter.AddKeywordColor(api.Name, new Color("82aaff"));
        _source.SyntaxHighlighter = highlighter;
        _source.TextChanged += InvalidateUx6G;
        center.AddChild(_source);
        center.AddChild(StudioStyle.Label(
            "Le Wizard et les snippets ne modifient que ce brouillon. Seul Aperçu sec puis Appliquer écrit atomiquement .as + .meta.json.",
            9, StudioStyle.Muted, wrap: true));

        var rightFrame = new Control
        {
            CustomMinimumSize = new Vector2(430, 0),
            CustomMaximumSize = new Vector2(430, 700),
            SizeFlagsHorizontal = SizeFlags.Fill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        body.AddChild(rightFrame);
        var rightHost = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        rightHost.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        rightFrame.AddChild(rightHost);
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(410, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 6);
        rightHost.AddChild(right);
        right.AddChild(StudioStyle.Label("SCRIPT WIZARD · SOURCE TYPÉE À RELIRE", 10, StudioStyle.Accent));
        _wizardFunction = StudioUx2EnvBControls.Text("StudioReviewedAction");
        right.AddChild(FieldUx6G("Fonction générée", _wizardFunction, 390));
        _wizardCondition = StudioUx2EnvBControls.Text("true");
        right.AddChild(FieldUx6G("Condition booléenne revue", _wizardCondition, 390));
        _wizardAction = StudioUx2EnvBControls.Options("Message sûr", "Action différée", "Attaque combat", "Appliquer effet");
        right.AddChild(FieldUx6G("Action typée", _wizardAction, 260));
        _wizardValue = StudioUx2EnvBControls.Text("Action générée et revue");
        right.AddChild(FieldUx6G("Message / action / capacité / effet", _wizardValue, 390));
        var wizardNumbers = new HBoxContainer();
        _wizardPriority = NumberUx6G(0, 10_000, 1, 100);
        _wizardAmount = NumberUx6G(0, 1_000_000, 0, 12);
        _wizardDuration = NumberUx6G(.001, 86_400, 0, 10);
        _wizardSeed = NumberUx6G(int.MinValue, int.MaxValue, 1, 97);
        wizardNumbers.AddChild(FieldUx6G("Priorité", _wizardPriority, 90));
        wizardNumbers.AddChild(FieldUx6G("Montant", _wizardAmount, 90));
        wizardNumbers.AddChild(FieldUx6G("Durée s", _wizardDuration, 90));
        wizardNumbers.AddChild(FieldUx6G("Seed", _wizardSeed, 90));
        right.AddChild(wizardNumbers);
        var wizardActions = new HBoxContainer();
        _generateWizard = StudioStyle.Button("Générer revue", compact: true);
        _generateWizard.Pressed += GenerateWizardUx6G;
        _insertWizard = StudioStyle.Button("Insérer dans brouillon", compact: true);
        _insertWizard.Pressed += InsertWizardUx6G;
        wizardActions.AddChild(_generateWizard);
        wizardActions.AddChild(_insertWizard);
        right.AddChild(wizardActions);
        _wizardReview = StudioStyle.Label("Aucune source Wizard générée.", 9, StudioStyle.Muted, wrap: true);
        _wizardReview.CustomMinimumSize = new Vector2(0, 75);
        right.AddChild(_wizardReview);

        right.AddChild(StudioStyle.Label("ACTIONS DIFFÉRÉES · COMBAT · EFFETS", 10, StudioStyle.Accent));
        right.AddChild(StudioStyle.Label(
            "Simulation déterministe sur état copié : file et autorité, attaque attribuée avec armure, effet périodique sérialisable.",
            9, StudioStyle.Muted, wrap: true));
        _taskAction = StudioUx2EnvBControls.Text("attack");
        _taskPriority = NumberUx6G(0, 10_000, 1, 100);
        right.AddChild(PairUx6G("Action", _taskAction, "Priorité", _taskPriority));
        _ability = StudioUx2EnvBControls.Text("ability:studio-slash");
        _damage = NumberUx6G(0, 1_000_000, 0, 12);
        right.AddChild(PairUx6G("Capacité", _ability, "Dégâts", _damage));
        _armorPenetration = NumberUx6G(0, 1, 0, .5);
        _seed = NumberUx6G(int.MinValue, int.MaxValue, 1, 97);
        right.AddChild(PairUx6G("Pénétration", _armorPenetration, "Seed", _seed));
        _effectId = StudioUx2EnvBControls.Text("effect:studio-burn");
        _magnitude = NumberUx6G(-1_000_000, 1_000_000, 0, 2);
        right.AddChild(PairUx6G("Effet", _effectId, "Magnitude", _magnitude));
        _duration = NumberUx6G(.001, 86_400, 0, 10);
        _period = NumberUx6G(0, 86_400, 0, 2);
        right.AddChild(PairUx6G("Durée s", _duration, "Période s", _period));
        _stacking = StudioUx2EnvBControls.Options(
            "Remplacer", "Rafraîchir durée", "Empiler magnitude", "Empiler durée", "Indépendant", "Plus fort gagne");
        right.AddChild(FieldUx6G("Empilement", _stacking, 280));
        _simulate = StudioStyle.Button("Simuler les trois APIs", compact: true);
        _simulate.Pressed += SimulateUx6G;
        right.AddChild(_simulate);
        _runtimeReview = StudioStyle.Label(
            "Aucune simulation. Aucun acteur, objet moteur ou transport réseau n’est créé.",
            9, StudioStyle.Muted, wrap: true);
        _runtimeReview.CustomMinimumSize = new Vector2(0, 95);
        right.AddChild(_runtimeReview);

        _console = StudioStyle.Label(
            "CONSOLE · analyse, compilation et sandbox sont sans écriture ; les simulations gameplay restent en mémoire.",
            10, StudioStyle.Muted, wrap: true);
        _console.CustomMinimumSize = new Vector2(0, 62);
        root.AddChild(_console);
        _status = StudioStyle.Label("Prêt.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 7);
        _preview = StudioStyle.Button("Aperçu sec", compact: true);
        _preview.Pressed += PreviewUx6G;
        _compile = StudioStyle.Button("Compiler", compact: true);
        _compile.Pressed += CompileUx6G;
        _sandbox = StudioStyle.Button("Tester sandbox", compact: true);
        _sandbox.Pressed += SandboxUx6G;
        _apply = StudioStyle.Button("Appliquer et enregistrer", primary: true, compact: true);
        _apply.Pressed += ApplyUx6G;
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += UndoUx6G;
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += RedoUx6G;
        foreach (var button in new[] { _preview, _compile, _sandbox, _apply, _undo, _redo })
        {
            button.CustomMinimumSize = new Vector2(button == _apply ? 185 : 126, 35);
            footer.AddChild(button);
        }
        root.AddChild(footer);

        ResetControlsUx6G();
        UpdateButtonsUx6G();
    }

    internal void Open() => OpenBoundUx6G(_sourceSession, evidence: false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBoundUx6G(_sourceSession, evidence: false, cancelApproval: false);

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null) return;
        if (_evidenceSession is not null || _evidenceRoot is not null)
            ReleaseEvidenceUx6G();
        else
        {
            _session.CancelUx6FApproval();
            _session = _sourceSession;
        }
        Visible = false;
        ResetControlsUx6G();
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is null && _evidenceRoot is null) return;
        ReleaseEvidenceUx6G();
        Visible = false;
        ResetControlsUx6G();
    }

    internal bool PrepareEvidence(string mode)
    {
        _ = mode;
        CloseEvidence();
        if (_sourceSession.Project is null || _sourceSession.History is null ||
            _sourceSession.Session is null) return false;
        _sourceSnapshot = CaptureSourceUx6G();
        _evidenceRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux6-g-{Guid.NewGuid():N}");
        _evidenceSession = new StudioSessionController();
        _evidenceReleased = false;
        if (!ProjectSession.Create(_evidenceRoot, _sourceSession.Project).Succeeded)
        {
            ReleaseEvidenceUx6G();
            return false;
        }
        new ForgeScriptSourceStore(_evidenceRoot).Save(StudioSessionController.EvidenceDocumentUx6F());
        if (!_evidenceSession.Open(_evidenceRoot).Succeeded)
        {
            ReleaseEvidenceUx6G();
            return false;
        }
        OpenBoundUx6G(_evidenceSession, evidence: true);
        var sourceIndex = _choices.ToList().FindIndex(item =>
            string.Equals(item.RelativePath, StudioSessionController.EvidenceDocumentUx6F().RelativePath,
                StringComparison.Ordinal));
        if (sourceIndex < 0)
        {
            CloseEvidence();
            return false;
        }
        _sourceChoice.Select(sourceIndex + 1);
        SelectSourceUx6G(sourceIndex + 1);
        if (_draft is null)
        {
            CloseEvidence();
            return false;
        }
        PopulateDefaultsUx6G();
        GenerateWizardUx6G();
        InsertWizardUx6G();
        AnalyzeUx6G();
        PreviewUx6G();
        CompileUx6G();
        SandboxUx6G();
        SimulateUx6G();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && EditorReady && WizardReady &&
                    GameplayReady && NativeExecutionReady && PreviewReady && ShowsHumanSurface &&
                    SourceMatchesUx6G();
        if (!ready) CloseEvidence();
        return ready;
    }

    internal bool RunUiTransactionEvidence()
    {
        var source = CaptureSourceUx6G();
        try
        {
            if (!PrepareEvidence("ux6-g-script-authoring-gameplay") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !PreviewReady) return false;
            var candidate = ReadDraftUx6G();
            if (candidate is null) return false;
            var expected = candidate.Source;
            var applied = _session.ApplyApprovedScriptSourceUx6F(candidate);
            var appliedDocument = new ForgeScriptSourceStore(_evidenceRoot!).Load(candidate.RelativePath);
            var undone = _session.UndoScriptSourceUx6F();
            var undoDocument = new ForgeScriptSourceStore(_evidenceRoot!).Load(candidate.RelativePath);
            var redone = _session.RedoScriptSourceUx6F();
            var redoDocument = new ForgeScriptSourceStore(_evidenceRoot!).Load(candidate.RelativePath);
            var reopened = new StudioSessionController();
            var persisted = reopened.Open(_evidenceRoot!).Succeeded &&
                            reopened.TryOpenCanonicalSourceUx6F(candidate.RelativePath, out var reopenedDraft,
                                out _) && reopenedDraft?.Source == expected;
            reopened.Close();
            var valid = applied.Succeeded && appliedDocument.Source == expected && undone.Succeeded &&
                        !undoDocument.Source.Contains("StudioReviewedAction", StringComparison.Ordinal) &&
                        redone.Succeeded && redoDocument.Source == expected && persisted &&
                        SourceMatchesUx6G(source);
            if (!valid)
                GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_G_UI_TRANSACTION_FAILED " +
                            $"apply={applied.Succeeded.ToString().ToLowerInvariant()} " +
                            $"undo={undone.Succeeded.ToString().ToLowerInvariant()} " +
                            $"redo={redone.Succeeded.ToString().ToLowerInvariant()} " +
                            $"persisted={persisted.ToString().ToLowerInvariant()} " +
                            $"source={SourceMatchesUx6G(source).ToString().ToLowerInvariant()}");
            return valid;
        }
        finally
        {
            CloseEvidence();
        }
    }

    private void OpenBoundUx6G(StudioSessionController session, bool evidence, bool cancelApproval = true)
    {
        _session = session;
        if (cancelApproval) _session.CancelUx6FApproval();
        ResetControlsUx6G();
        RefreshSourcesUx6G();
        _selection.Text = evidence
            ? "Copie temporaire isolée · source, Wizard et simulations séparés · module source strictement inchangé."
            : "Choisissez une source .as ; l’atelier partage exactement l’historique et l’écriture atomique UX6-F.";
        Visible = true;
        MoveToFront();
        UpdateBoundsUx6G();
        UpdateButtonsUx6G();
    }

    private void RefreshSourcesUx6G()
    {
        _choices = _session.ScriptSourceChoicesUx6F();
        _sourceChoice.Clear();
        _sourceChoice.AddItem("Choisir explicitement une source…");
        foreach (var choice in _choices)
            _sourceChoice.AddItem(HumanValueUx6G(choice.Label));
        _sourceChoice.Select(0);
        _explicitSource = false;
        _draft = null;
        _source.Text = string.Empty;
        InvalidateUx6G();
        UpdateButtonsUx6G();
    }

    private void SelectSourceUx6G(long selected)
    {
        if (_updating || selected <= 0 || selected > _choices.Count)
        {
            _explicitSource = false;
            _draft = null;
            UpdateButtonsUx6G();
            return;
        }
        var choice = _choices[(int)selected - 1];
        if (!_session.TryOpenCanonicalSourceUx6F(choice.RelativePath, out var draft, out var message) ||
            draft is null)
        {
            SetStatusUx6G(message, true);
            _sourceChoice.Select(0);
            _explicitSource = false;
            _draft = null;
            UpdateButtonsUx6G();
            return;
        }
        _draft = draft;
        _explicitSource = true;
        _updating = true;
        _source.Text = draft.Source;
        _updating = false;
        PopulateDefaultsUx6G();
        InvalidateUx6G();
        SetStatusUx6G(message, false);
        UpdateButtonsUx6G();
    }

    private void CreateSourceUx6G()
    {
        if (!_session.TryCreateCanonicalSourceUx6F(_newSourcePath.Text, out var draft, out var message) ||
            draft is null)
        {
            SetStatusUx6G(message, true);
            return;
        }
        _draft = draft;
        _explicitSource = true;
        _updating = true;
        _source.Text = draft.Source;
        _updating = false;
        PopulateDefaultsUx6G();
        InvalidateUx6G();
        SetStatusUx6G(message, false);
        UpdateButtonsUx6G();
    }

    private StudioUx6FDraft? ReadDraftUx6G() =>
        _draft is null ? null : _draft with { Source = _source.Text };

    private void AnalyzeUx6G()
    {
        var draft = ReadDraftUx6G();
        if (draft is null) return;
        var cursor = CursorOffsetUx6G();
        _analysisResult = _session.AnalyzeScriptSourceUx6G(draft, cursor);
        RenderAnalysisUx6G(_analysisResult);
        RenderDiagnosticsUx6G(_analysisResult.Diagnostics);
        _console.Text = HumanValueUx6G(_analysisResult.Message);
        SetStatusUx6G(_analysisResult.Message, !_analysisResult.Succeeded);
        UpdateButtonsUx6G();
    }

    private void FormatUx6G()
    {
        AnalyzeUx6G();
        if (_analysisResult is not { Succeeded: true }) return;
        _updating = true;
        _source.Text = _analysisResult.FormattedSource;
        _updating = false;
        InvalidateUx6G();
        SetStatusUx6G("Formatage déterministe placé dans le brouillon · relancez l’aperçu sec.", false);
    }

    private void InsertSnippetUx6G()
    {
        var context = StudioSessionController.EditorContextUx6G();
        var key = _snippet.GetItemText(_snippet.Selected);
        if (!context.Snippets.TryGetValue(key, out var snippet)) return;
        _updating = true;
        _source.Text = _source.Text.TrimEnd() + System.Environment.NewLine + System.Environment.NewLine + snippet;
        _updating = false;
        InvalidateUx6G();
        SetStatusUx6G($"Snippet {key} inséré dans le brouillon · source non enregistrée.", false);
    }

    private void GenerateWizardUx6G()
    {
        var draft = ReadDraftUx6G();
        if (draft is null) return;
        _wizardResult = _session.BuildScriptWizardUx6G(draft, ReadWizardUx6G());
        _wizardReview.Text = _wizardResult.Succeeded
            ? "SOURCE GÉNÉRÉE À RELIRE\n" + HumanValueUx6G(_wizardResult.ReviewedSource)
            : HumanValueUx6G(_wizardResult.Message);
        RenderDiagnosticsUx6G(_wizardResult.Diagnostics);
        SetStatusUx6G(_wizardResult.Message, !_wizardResult.Succeeded);
        UpdateButtonsUx6G();
    }

    private void InsertWizardUx6G()
    {
        if (_wizardResult is not { Succeeded: true })
        {
            SetStatusUx6G("Générez et relisez d’abord une source Wizard valide.", true);
            return;
        }
        _updating = true;
        _source.Text = _wizardResult.CandidateSource;
        _updating = false;
        var retained = _wizardResult;
        InvalidateUx6G();
        _wizardResult = retained;
        _wizardReview.Text = "Source Wizard insérée dans le brouillon · Aperçu sec requis avant écriture.";
        SetStatusUx6G("Insertion en mémoire terminée · aucune source n’a été enregistrée.", false);
        UpdateButtonsUx6G();
    }

    private void SimulateUx6G()
    {
        var draft = ReadDraftUx6G();
        if (draft is null) return;
        _gameplayResult = _session.PreviewScriptGameplayUx6G(draft, ReadGameplayUx6G());
        _runtimeReview.Text = HumanValueUx6G(
            $"{_gameplayResult.Message}\n" +
            $"Action : queued={_gameplayResult.TaskQueued}, completed={_gameplayResult.TaskCompleted}, autorité refusée={_gameplayResult.UnauthorizedTaskBlocked}.\n" +
            $"Combat : dégâts {_gameplayResult.AppliedDamage:0.##}, vie {_gameplayResult.RemainingHealth:0.##}, client refusé={_gameplayResult.UnauthorizedCombatBlocked}.\n" +
            $"Effets : {_gameplayResult.EffectCount}, périodique={_gameplayResult.EffectAdvanced}, round-trip={_gameplayResult.EffectRoundTrip}.\n" +
            string.Join(" · ", _gameplayResult.Events.Take(5)));
        RenderDiagnosticsUx6G(_gameplayResult.Diagnostics);
        SetStatusUx6G(_gameplayResult.Message, !_gameplayResult.Succeeded);
        UpdateButtonsUx6G();
    }

    private void PreviewUx6G()
    {
        var draft = ReadDraftUx6G();
        if (draft is null) return;
        _previewResult = _session.PreviewScriptSourceUx6F(draft);
        RenderDiagnosticsUx6G(_previewResult.Diagnostics);
        _console.Text = HumanValueUx6G(
            $"APERÇU SEC · {_previewResult.Message}\n{_previewResult.SemanticDiff}\n" +
            "Même ForgeScriptSourceHistory UX6-F · aucune écriture avant Appliquer.");
        SetStatusUx6G(_previewResult.Message, !_previewResult.Succeeded);
        UpdateButtonsUx6G();
    }

    private void CompileUx6G()
    {
        var draft = ReadDraftUx6G();
        if (draft is null) return;
        _compileResult = _session.CompileScriptSourceUx6F(draft);
        RenderDiagnosticsUx6G(_compileResult.Diagnostics);
        _console.Text = HumanValueUx6G(
            $"COMPILATEUR · {_compileResult.Message}\nVM {_compileResult.VirtualMachineVersion} · " +
            $"API {_compileResult.ApiVersion} · {_compileResult.BytecodeBytes:N0} octets · " +
            $"cache={_compileResult.CacheHit}.");
        SetStatusUx6G(_compileResult.Message, !_compileResult.Succeeded);
        UpdateButtonsUx6G();
    }

    private void SandboxUx6G()
    {
        var draft = ReadDraftUx6G();
        if (draft is null) return;
        _sandboxResult = _session.RunScriptSandboxUx6F(draft);
        RenderDiagnosticsUx6G(_sandboxResult.Diagnostics);
        _console.Text = HumanValueUx6G(
            $"SANDBOX · {_sandboxResult.Message}\nStatut {_sandboxResult.Status} · " +
            $"{_sandboxResult.Instructions:N0} instructions · {_sandboxResult.PeakMemoryBytes:N0} octets · " +
            string.Join(" · ", _sandboxResult.Events.Take(4)));
        SetStatusUx6G(_sandboxResult.Message, !_sandboxResult.Succeeded);
        UpdateButtonsUx6G();
    }

    private void ApplyUx6G()
    {
        var draft = ReadDraftUx6G();
        if (draft is null) return;
        var result = _session.ApplyApprovedScriptSourceUx6F(draft);
        SetStatusUx6G(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        _draft = _session.CurrentScriptDraftUx6F();
        InvalidateUx6G();
        ScriptSourceChanged?.Invoke();
    }

    private void UndoUx6G()
    {
        var result = _session.UndoScriptSourceUx6F();
        SetStatusUx6G(result.Message, !result.Succeeded);
        if (result.Succeeded) ReloadActiveDraftUx6G();
    }

    private void RedoUx6G()
    {
        var result = _session.RedoScriptSourceUx6F();
        SetStatusUx6G(result.Message, !result.Succeeded);
        if (result.Succeeded) ReloadActiveDraftUx6G();
    }

    private void ReloadActiveDraftUx6G()
    {
        _draft = _session.CurrentScriptDraftUx6F();
        if (_draft is null) return;
        _updating = true;
        _source.Text = _draft.Source;
        _updating = false;
        InvalidateUx6G();
        ScriptSourceChanged?.Invoke();
    }

    private StudioUx6GWizardDraft ReadWizardUx6G() => new(
        _wizardFunction.Text.Trim(),
        _wizardCondition.Text.Trim(),
        (StudioUx6GWizardAction)_wizardAction.Selected,
        _wizardValue.Text,
        (int)_wizardPriority.Value,
        _wizardAmount.Value,
        _wizardDuration.Value,
        (int)_wizardSeed.Value);

    private StudioUx6GGameplayDraft ReadGameplayUx6G() => new(
        _taskAction.Text.Trim(),
        (int)_taskPriority.Value,
        _ability.Text.Trim(),
        _damage.Value,
        _armorPenetration.Value,
        (int)_seed.Value,
        _effectId.Text.Trim(),
        _magnitude.Value,
        _duration.Value,
        _period.Value,
        (EffectStacking)_stacking.Selected);

    private void PopulateDefaultsUx6G()
    {
        var wizard = StudioSessionController.DefaultWizardUx6G();
        var gameplay = StudioSessionController.DefaultGameplayUx6G();
        _updating = true;
        _wizardFunction.Text = wizard.FunctionName;
        _wizardCondition.Text = wizard.BooleanCondition;
        _wizardAction.Select((int)wizard.Action);
        _wizardValue.Text = wizard.ActionOrAbility;
        _wizardPriority.Value = wizard.Priority;
        _wizardAmount.Value = wizard.Amount;
        _wizardDuration.Value = wizard.DurationSeconds;
        _wizardSeed.Value = wizard.Seed;
        _taskAction.Text = gameplay.ActionName;
        _taskPriority.Value = gameplay.Priority;
        _ability.Text = gameplay.AbilityId;
        _damage.Value = gameplay.Damage;
        _armorPenetration.Value = gameplay.ArmorPenetration;
        _seed.Value = gameplay.Seed;
        _effectId.Text = gameplay.EffectId;
        _magnitude.Value = gameplay.Magnitude;
        _duration.Value = gameplay.DurationSeconds;
        _period.Value = gameplay.PeriodSeconds;
        _stacking.Select((int)gameplay.Stacking);
        _updating = false;
    }

    private void RenderAnalysisUx6G(StudioUx6GEditorAnalysis analysis)
    {
        ClearChildrenUx6G(_completionRows);
        foreach (var completion in analysis.Completions.Take(8))
        {
            var button = StudioStyle.Button(HumanValueUx6G(completion), compact: true);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.Alignment = HorizontalAlignment.Left;
            button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            button.TooltipText = HumanValueUx6G(completion);
            button.Pressed += () => SetStatusUx6G(HumanValueUx6G(completion), false);
            _completionRows.AddChild(button);
        }
        if (analysis.Completions.Count == 0)
            _completionRows.AddChild(StudioStyle.Label("Aucune complétion au curseur.", 9, StudioStyle.Muted));
        _signature.Text = string.IsNullOrWhiteSpace(analysis.SignatureHelp)
            ? $"Symboles du document : {analysis.Symbols.Count} · aucun appel actif au curseur."
            : "Signature · " + HumanValueUx6G(analysis.SignatureHelp);
    }

    private void RenderDiagnosticsUx6G(IReadOnlyList<StudioUx6FDiagnostic> diagnostics)
    {
        ClearChildrenUx6G(_diagnosticRows);
        if (diagnostics.Count == 0)
        {
            _diagnosticRows.AddChild(StudioStyle.Label("✓ Aucun diagnostic bloquant.", 9, StudioStyle.Success));
            return;
        }
        foreach (var diagnostic in diagnostics.Take(8))
        {
            var text = HumanValueUx6G(
                $"{diagnostic.Code} · {diagnostic.Path} · {diagnostic.Message}");
            var button = StudioStyle.Button(text, compact: true);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.Alignment = HorizontalAlignment.Left;
            button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            button.TooltipText = text;
            button.Pressed += () => FocusDiagnosticUx6G(diagnostic);
            _diagnosticRows.AddChild(button);
        }
    }

    private void FocusDiagnosticUx6G(StudioUx6FDiagnostic diagnostic)
    {
        if (diagnostic.Line > 0)
        {
            _source.SetCaretLine(Math.Clamp(diagnostic.Line - 1, 0, Math.Max(0, _source.GetLineCount() - 1)));
            _source.SetCaretColumn(Math.Max(0, diagnostic.Column - 1));
            _source.GrabFocus();
        }
        SetStatusUx6G($"Diagnostic {diagnostic.Code} · ligne {Math.Max(1, diagnostic.Line)}, " +
                      $"colonne {Math.Max(1, diagnostic.Column)}.", true);
    }

    private void InvalidateUx6G()
    {
        if (_updating) return;
        _session.CancelUx6FApproval();
        _analysisResult = null;
        _wizardResult = null;
        _gameplayResult = null;
        _previewResult = null;
        _compileResult = null;
        _sandboxResult = null;
        _wizardReview.Text = "Aucune source Wizard générée.";
        _runtimeReview.Text = "Aucune simulation. Aucun acteur, objet moteur ou transport réseau n’est créé.";
        _console.Text = "CONSOLE · brouillon modifié · relancez analyse, simulation et aperçu sec.";
        UpdateButtonsUx6G();
    }

    private void UpdateButtonsUx6G()
    {
        var selected = HasExplicitSelection;
        foreach (var button in new[]
                 {
                     _analyze, _format, _generateWizard, _simulate, _preview, _compile, _sandbox,
                 })
            button.Disabled = !selected;
        _insertWizard.Disabled = !selected || _wizardResult is not { Succeeded: true };
        _apply.Disabled = !selected || !PreviewReady;
        _undo.Disabled = !selected;
        _redo.Disabled = !selected;
    }

    private void ResetControlsUx6G()
    {
        _updating = true;
        _sourceChoice.Clear();
        _sourceChoice.AddItem("Choisir explicitement une source…");
        _sourceChoice.Select(0);
        _source.Text = string.Empty;
        _updating = false;
        _choices = [];
        _draft = null;
        _explicitSource = false;
        _analysisResult = null;
        _wizardResult = null;
        _gameplayResult = null;
        _previewResult = null;
        _compileResult = null;
        _sandboxResult = null;
        ClearChildrenUx6G(_completionRows);
        ClearChildrenUx6G(_diagnosticRows);
        _signature.Text = "Signature · lancez l’analyse au curseur.";
        _wizardReview.Text = "Aucune source Wizard générée.";
        _runtimeReview.Text = "Aucune simulation. Aucun acteur, objet moteur ou transport réseau n’est créé.";
        _console.Text = "CONSOLE · analyse, compilation et sandbox sont sans écriture.";
        PopulateDefaultsUx6G();
        UpdateButtonsUx6G();
    }

    private StudioUx6FEvidenceSourceSnapshot CaptureSourceUx6G()
    {
        var project = _sourceSession.Project ?? throw new InvalidOperationException("No source project.");
        var history = _sourceSession.History ?? throw new InvalidOperationException("No source history.");
        return new(project, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.CaptureScriptApprovalUx6F());
    }

    private bool SourceMatchesUx6G(StudioUx6FEvidenceSourceSnapshot? expected = null)
    {
        var snapshot = expected ?? _sourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.CaptureScriptApprovalUx6F() == snapshot.Approval;
    }

    private bool ReleaseEvidenceUx6G()
    {
        var isolated = _evidenceSession;
        var root = _evidenceRoot;
        var sessionReleased = true;
        try
        {
            isolated?.Close();
        }
        catch (Exception exception)
        {
            sessionReleased = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_G_RELEASE_FAILED type={exception.GetType().Name}");
        }
        var rootReleased = StudioSessionController.DeleteUx6FTemporaryWorkspace(root);
        _evidenceSession = null;
        _evidenceRoot = null;
        _session = _sourceSession;
        _evidenceReleased = sessionReleased && rootReleased;
        return _evidenceReleased;
    }

    private void SetStatusUx6G(string message, bool error)
    {
        _status.Text = HumanValueUx6G(message);
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(HumanValueUx6G(message), error);
    }

    private int CursorOffsetUx6G()
    {
        var line = Math.Clamp(_source.GetCaretLine(), 0, Math.Max(0, _source.GetLineCount() - 1));
        var offset = 0;
        for (var index = 0; index < line; index++) offset += _source.GetLine(index).Length + 1;
        return Math.Min(_source.Text.Length, offset + Math.Max(0, _source.GetCaretColumn()));
    }

    private void UpdateBoundsUx6G()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 44, 1420, 1880);
        var height = Math.Clamp(viewport.Y - 44, 760, 1030);
        _dialog.Position = new Vector2((viewport.X - width) / 2, (viewport.Y - height) / 2);
        _dialog.Size = new Vector2(width, height);
    }

    private static VBoxContainer PanelColumnUx6G(HBoxContainer host, int minimum, bool expand = false)
    {
        var panel = StudioStyle.Panel("Ux6GColumn", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(minimum, 0);
        panel.SizeFlagsHorizontal = expand ? SizeFlags.ExpandFill : SizeFlags.Fill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        host.AddChild(panel);
        var margin = StudioStyle.Margin(9, 9, 9, 9);
        panel.AddChild(margin);
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        column.AddThemeConstantOverride("separation", 6);
        margin.AddChild(column);
        return column;
    }

    private static VBoxContainer FieldUx6G(string label, Control control, int minimum = 118)
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(minimum, 0) };
        box.AddThemeConstantOverride("separation", 2);
        box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        box.AddChild(control);
        return box;
    }

    private static HBoxContainer PairUx6G(string leftLabel, Control left, string rightLabel, Control right)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(FieldUx6G(leftLabel, left, 195));
        row.AddChild(FieldUx6G(rightLabel, right, 195));
        return row;
    }

    private static SpinBox NumberUx6G(double minimum, double maximum, double step, double value)
    {
        var number = StudioUx2EnvBControls.Number(minimum, maximum, step);
        number.Value = value;
        number.CustomArrowStep = step == 0 ? .1 : step;
        return number;
    }

    private static void ClearChildrenUx6G(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private static IEnumerable<string> DescendantTextUx6G(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label) yield return label.Text;
            if (child is Button button) yield return button.Text;
            if (child is OptionButton option)
                for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
            foreach (var value in DescendantTextUx6G(child)) yield return value;
        }
    }

    private static bool ContainsUuidUx6G(string text) => Regex.IsMatch(text,
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.CultureInvariant);

    private static string HumanValueUx6G(string value) => Regex.Replace(value ?? string.Empty,
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        "identifiant protégé", RegexOptions.CultureInvariant);

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBoundsUx6G();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Escape)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
        else if (key.CtrlPressed && key.Keycode == Key.Enter)
        {
            PreviewUx6G();
            GetViewport().SetInputAsHandled();
        }
    }
}
