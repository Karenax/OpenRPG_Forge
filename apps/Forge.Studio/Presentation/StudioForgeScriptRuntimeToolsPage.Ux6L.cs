using System.Text.RegularExpressions;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioForgeScriptRuntimeToolsPageUx6L : ColorRect
{
    private const string ReviewedMarker = "<studio:ux6-l-runtime-tools>";
    private static readonly string[] ListenerPolicies = ["camera", "entity", "player", "every-listener"];

    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx6FEvidenceSourceSnapshot? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _sourceChoice;
    private readonly LineEdit _newSourcePath;
    private readonly CodeEdit _source;
    private readonly Label _selection;
    private readonly Label _review;
    private readonly Label _runtimeReview;
    private readonly Label _diagnostics;
    private readonly Label _console;
    private readonly Label _status;
    private readonly TabContainer _tabs;

    private readonly LineEdit _audioCue;
    private readonly LineEdit _audioBus;
    private readonly SpinBox _audioVolume;
    private readonly SpinBox _fadeIn;
    private readonly SpinBox _fadeOut;
    private readonly CheckButton _loop;
    private readonly LineEdit _snapshot;
    private readonly LineEdit _listener;
    private readonly OptionButton _listenerPolicy;

    private readonly SpinBox _timeScale;
    private readonly SpinBox _advance;
    private readonly SpinBox _delay;
    private readonly SpinBox _repeat;
    private readonly LineEdit _timerHandler;
    private readonly LineEdit _timerSchedule;

    private readonly LineEdit _eventName;
    private readonly SpinBox _eventVersion;
    private readonly LineEdit _eventField;
    private readonly OptionButton _eventFieldKind;
    private readonly LineEdit _eventValue;
    private readonly LineEdit _subscriberOne;
    private readonly LineEdit _subscriberTwo;

    private readonly Button _build;
    private readonly Button _insert;
    private readonly Button _simulate;
    private readonly Button _preview;
    private readonly Button _compile;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;

    private IReadOnlyList<StudioUx6FSourceChoice> _choices = [];
    private StudioUx6FDraft? _draft;
    private StudioUx6LSourcePlan? _plan;
    private StudioUx6LRuntimeResult? _runtime;
    private StudioUx6FPreview? _previewResult;
    private StudioUx6FCompileResult? _compileResult;
    private bool _explicitSource;
    private bool _updating;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? ScriptSourceChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSource && _draft is not null;
    internal bool ReviewedSourceReady => _plan is { Succeeded: true } &&
                                          _source.Text.Contains(ReviewedMarker, StringComparison.Ordinal) &&
                                          _source.Text.Contains("<studio:ux6-n-live-tools>", StringComparison.Ordinal);
    internal bool RuntimeReady => _runtime is
    {
        Succeeded: true,
        AudioLifecycle: true,
        DeterministicClock: true,
        EventDeliveryOrder: true,
        NativeExecution: true,
    } && LiveToolsReadyUx6N;
    internal bool PreviewReady => _previewResult is
    { Succeeded: true, CanApply: true, CanonicalDryRun: true };
    internal bool CompilationReady => _compileResult is { Succeeded: true };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => SourceMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return text.Contains("Audio & musique", StringComparison.Ordinal) &&
                   text.Contains("Horloge & timers", StringComparison.Ordinal) &&
                   text.Contains("Événements custom", StringComparison.Ordinal) &&
                   text.Contains("Hot reload", StringComparison.Ordinal) &&
                   text.Contains("Débogueur", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec", StringComparison.Ordinal) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioForgeScriptRuntimeToolsPageUx6L(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.008f, .014f, .023f, .988f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6LForgeScriptRuntimeTools", StudioStyle.Surface, 12, 1);
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
        headingText.AddChild(StudioStyle.Label("ForgeScript · Outils runtime R099", 19));
        headingText.AddChild(StudioStyle.Label(
            "Audio, temps, événements, hot reload et débogueur · simulation autoritaire en mémoire, même source UX6-F.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        var sourceRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sourceChoice = StudioUx2EnvBControls.Options();
        _sourceChoice.CustomMinimumSize = new Vector2(440, 34);
        _sourceChoice.ItemSelected += SelectSource;
        sourceRow.AddChild(Field("Source canonique · sélection explicite", _sourceChoice, 440));
        _newSourcePath = StudioUx2EnvBControls.Text("scripts/runtime-tools.as");
        _newSourcePath.Text = "scripts/runtime-tools.as";
        _newSourcePath.CustomMinimumSize = new Vector2(300, 34);
        sourceRow.AddChild(Field("Créer si le module est vide", _newSourcePath, 300));
        var create = StudioStyle.Button("Créer en mémoire", compact: true);
        create.CustomMinimumSize = new Vector2(145, 34);
        create.Pressed += CreateSource;
        sourceRow.AddChild(create);
        var reload = StudioStyle.Button("Recharger", compact: true);
        reload.CustomMinimumSize = new Vector2(115, 34);
        reload.Pressed += RefreshSources;
        sourceRow.AddChild(reload);
        root.AddChild(sourceRow);
        _selection = StudioStyle.Label(
            "Choisissez une source .as. Aucun son, temps système ou événement moteur n’est déclenché par l’aperçu.",
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

        var left = PanelColumn(body, 740, expand: true);
        left.AddChild(StudioStyle.Label("SOURCE REVUE · AUTORITÉ UNIQUE UX6-F", 10, StudioStyle.Accent));
        _source = new CodeEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(700, 285),
            DrawTabs = true,
            GuttersDrawLineNumbers = true,
        };
        _source.TextChanged += SourceChanged;
        left.AddChild(_source);
        var sourceActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _build = StudioStyle.Button("Construire le bloc revu", compact: true);
        _build.Pressed += BuildSource;
        _insert = StudioStyle.Button("Insérer dans le brouillon", compact: true);
        _insert.Pressed += InsertSource;
        sourceActions.AddChild(_build);
        sourceActions.AddChild(_insert);
        left.AddChild(sourceActions);
        _review = StudioStyle.Label(
            "Aucun bloc construit. Le formulaire génère du code versionné à relire, jamais une source parallèle.",
            9, StudioStyle.Muted, wrap: true);
        _review.CustomMinimumSize = new Vector2(0, 66);
        left.AddChild(_review);
        _diagnostics = StudioStyle.Label("Diagnostics · aucun aperçu.", 9, StudioStyle.Muted, wrap: true);
        _diagnostics.CustomMinimumSize = new Vector2(0, 70);
        left.AddChild(_diagnostics);

        var right = PanelColumn(body, 690);
        right.AddChild(StudioStyle.Label("SCÉNARIO RUNTIME · ÉTAT COPIÉ EN MÉMOIRE", 10, StudioStyle.Accent));
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(650, 360),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        right.AddChild(tabsHost);
        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(_tabs);

        var audioPage = TabPage(_tabs, "Audio & musique");
        audioPage.AddChild(StudioStyle.Label(
            "Audio & musique · contrôle typé des poignées runtime",
            12, StudioStyle.Text));
        _audioCue = Text("music:dawn");
        _audioBus = Text("music");
        audioPage.AddChild(Pair("Cue ou émetteur", _audioCue, "Bus de mixage", _audioBus));
        _audioVolume = Number(0, 1, .01, .8);
        _snapshot = Text("exploration");
        audioPage.AddChild(Pair("Volume", _audioVolume, "Snapshot", _snapshot));
        _fadeIn = Number(0, 120, .1, 1);
        _fadeOut = Number(0, 120, .1, 2);
        audioPage.AddChild(Pair("Fondu entrant · secondes", _fadeIn,
            "Fondu sortant · secondes", _fadeOut));
        _loop = new CheckButton { Text = "Boucle auteur", ButtonPressed = true };
        audioPage.AddChild(_loop);
        _listener = Text("hero");
        _listenerPolicy = Options("Caméra", "Entité", "Joueur", "Tous les auditeurs");
        _listenerPolicy.Select(2);
        audioPage.AddChild(Pair("Auditeur logique", _listener,
            "Politique d’écoute", _listenerPolicy));
        audioPage.AddChild(StudioStyle.Label(
            "La simulation vérifie cue, snapshot, listener, handles, start/pause/resume/fade/stop et complétion. Aucun AudioStreamPlayer n’est créé.",
            9, StudioStyle.Muted, wrap: true));

        var timePage = TabPage(_tabs, "Horloge & timers");
        timePage.AddChild(StudioStyle.Label(
            "Horloge & timers · planification déterministe",
            12, StudioStyle.Text));
        _timeScale = Number(.01, 100, .01, 2);
        _advance = Number(0, 86_400, .1, 3);
        timePage.AddChild(Pair("Échelle simulation", _timeScale,
            "Avance réelle · secondes", _advance));
        _delay = Number(0, 86_400, .1, 1);
        _repeat = Number(0, 86_400, .1, 5);
        timePage.AddChild(Pair("Délai · secondes", _delay, "Répétition · secondes", _repeat));
        _timerHandler = Text("OnStudioTick");
        _timerSchedule = Text("daily");
        timePage.AddChild(Pair("Handler", _timerHandler, "Schedule", _timerSchedule));
        timePage.AddChild(StudioStyle.Label(
            "L’horloge dérive uniquement de l’époque et du temps de simulation. Timer, répétition, due, annulation et calendrier restent déterministes.",
            9, StudioStyle.Muted, wrap: true));

        var eventPage = TabPage(_tabs, "Événements custom");
        eventPage.AddChild(StudioStyle.Label(
            "Événements custom · schéma versionné et livraison ordonnée",
            12, StudioStyle.Text));
        _eventName = Text("studio.damage");
        _eventVersion = Number(1, 1000, 1, 1);
        eventPage.AddChild(Pair("Nom namespacé", _eventName, "Version", _eventVersion));
        _eventField = Text("amount");
        _eventFieldKind = Options("Booléen", "Entier", "Entier long", "Nombre", "Texte", "Identifiant stable");
        _eventFieldKind.Select((int)StudioUx6LEventFieldKind.Integer);
        eventPage.AddChild(Pair("Champ payload", _eventField, "Type", _eventFieldKind));
        _eventValue = Text("4");
        eventPage.AddChild(Field("Valeur typée", _eventValue));
        _subscriberOne = Text("audio");
        _subscriberTwo = Text("quest");
        eventPage.AddChild(Pair("Abonné A", _subscriberOne, "Abonné B", _subscriberTwo));
        eventPage.AddChild(StudioStyle.Label(
            "Le schéma versionné valide le payload. La publication exige l’autorité serveur, une corrélation et livre les abonnés dans un ordre stable.",
            9, StudioStyle.Muted, wrap: true));

        BuildLiveToolsUx6N(_tabs);

        HookRuntimeControls();
        _runtimeReview = StudioStyle.Label("Simulation · en attente.", 9, StudioStyle.Muted, wrap: true);
        _runtimeReview.CustomMinimumSize = new Vector2(0, 62);
        right.AddChild(_runtimeReview);
        _console = StudioStyle.Label("Trace · aucune intention exécutée.", 9, StudioStyle.Muted, wrap: true);
        _console.CustomMinimumSize = new Vector2(0, 72);
        right.AddChild(_console);

        _status = StudioStyle.Label("Sélectionnez une source pour commencer.", 9, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _simulate = StudioStyle.Button("Simuler en mémoire", compact: true);
        _preview = StudioStyle.Button("Aperçu sec", compact: true);
        _compile = StudioStyle.Button("Compiler", compact: true);
        _apply = StudioStyle.Button("Enregistrer", primary: true, compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _simulate.Pressed += Simulate;
        _preview.Pressed += Preview;
        _compile.Pressed += Compile;
        _apply.Pressed += Apply;
        _undo.Pressed += Undo;
        _redo.Pressed += Redo;
        foreach (var button in new[] { _simulate, _preview, _compile, _apply, _undo, _redo })
        {
            button.CustomMinimumSize = new Vector2(button == _apply ? 165 : 135, 35);
            footer.AddChild(button);
        }
        root.AddChild(footer);
        ResetControls();
        UpdateButtons();
    }

    internal void Open() => OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBound(_sourceSession, evidence: false, cancelApproval: false);

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null) return;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
        else
        {
            _session.CancelUx6FApproval();
            _session = _sourceSession;
        }
        Visible = false;
        ResetControls();
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is null && _evidenceRoot is null) return;
        ReleaseEvidence();
        Visible = false;
        ResetControls();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!mode.StartsWith("ux6-l", StringComparison.OrdinalIgnoreCase) &&
            !mode.StartsWith("ux6-n", StringComparison.OrdinalIgnoreCase)) return false;
        CloseEvidence();
        _sourceSnapshot = CaptureSource();
        _evidenceReleased = false;
        _evidenceRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-l-ui",
            Guid.NewGuid().ToString("N"));
        var project = _sourceSession.Project;
        if (project is null || !ProjectSession.Create(_evidenceRoot, project).Succeeded)
            return FailEvidence("La copie temporaire UX6-L n’a pas pu être créée.");
        new ForgeScriptSourceStore(_evidenceRoot).Save(StudioSessionController.EvidenceDocumentUx6F());
        _evidenceSession = new StudioSessionController();
        if (!_evidenceSession.Open(_evidenceRoot).Succeeded)
            return FailEvidence("La session temporaire UX6-L n’a pas pu être ouverte.");
        OpenBound(_evidenceSession, evidence: true);
        var selected = _choices.Select((choice, index) => (choice, index))
            .FirstOrDefault(item => item.choice.Kind == StudioUx6FSourceKind.Canonical);
        if (_choices.Count == 0 || selected.choice?.Kind != StudioUx6FSourceKind.Canonical)
            return FailEvidence("Aucune source canonique n’est disponible dans la preuve.");
        _sourceChoice.Select(selected.index + 1);
        SelectSource(selected.index + 1);
        BuildSource();
        InsertSource();
        Simulate();
        Compile();
        Preview();
        if (!SelectLiveEvidenceTabUx6N(mode))
            _tabs.CurrentTab = mode.Contains("events", StringComparison.OrdinalIgnoreCase)
                ? 2
                : mode.Contains("time", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        var ready = HasExplicitSelection && ReviewedSourceReady && RuntimeReady && PreviewReady &&
                    CompilationReady && ShowsHumanSurface && UsesIsolatedEvidence && SourceMatches();
        if (!ready)
        {
            var humanText = string.Join(' ', DescendantText(this));
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_L_EVIDENCE_FAILED " +
                        $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                        $"reviewed={ReviewedSourceReady.ToString().ToLowerInvariant()} " +
                        $"runtime={RuntimeReady.ToString().ToLowerInvariant()} " +
                        $"runtime_codes={string.Join(',', _runtime?.Diagnostics.Select(item => item.Code) ?? [])} " +
                        $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                        $"compile={CompilationReady.ToString().ToLowerInvariant()} " +
                        $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                        $"human_audio={humanText.Contains("Audio & musique", StringComparison.Ordinal).ToString().ToLowerInvariant()} " +
                        $"human_time={humanText.Contains("Horloge & timers", StringComparison.Ordinal).ToString().ToLowerInvariant()} " +
                        $"human_events={humanText.Contains("Événements custom", StringComparison.Ordinal).ToString().ToLowerInvariant()} " +
                        $"human_reload={humanText.Contains("Hot reload", StringComparison.Ordinal).ToString().ToLowerInvariant()} " +
                        $"human_debug={humanText.Contains("Débogueur", StringComparison.Ordinal).ToString().ToLowerInvariant()} " +
                        $"human_preview={humanText.Contains("Aperçu sec", StringComparison.Ordinal).ToString().ToLowerInvariant()} " +
                        $"human_uuid={ContainsUuid(humanText).ToString().ToLowerInvariant()} " +
                        $"isolated={UsesIsolatedEvidence.ToString().ToLowerInvariant()} " +
                        $"source={SourceMatches().ToString().ToLowerInvariant()}");
            return FailEvidence("La surface UX6-L n’a pas satisfait tous les invariants de preuve.");
        }
        return true;
    }

    internal bool RunUiTransactionEvidence(string mode = "ux6-l-script-runtime-tools")
    {
        var snapshot = CaptureSource();
        try
        {
            if (!PrepareEvidence(mode) || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !PreviewReady) return false;
            var candidate = ReadSourceDraft();
            if (candidate is null) return false;
            var applied = _session.ApplyApprovedScriptSourceUx6F(candidate);
            if (!applied.Succeeded) return false;
            var undone = _session.UndoScriptSourceUx6F();
            var redone = _session.RedoScriptSourceUx6F();
            var reopened = new StudioSessionController();
            var persisted = reopened.Open(_evidenceRoot!).Succeeded &&
                            reopened.TryOpenCanonicalSourceUx6F(candidate.RelativePath,
                                out var reopenedDraft, out _) && reopenedDraft?.Source == candidate.Source;
            reopened.Close();
            return undone.Succeeded && redone.Succeeded && persisted && SourceMatches(snapshot);
        }
        finally
        {
            CloseEvidence();
            _sourceSession.RestoreScriptApprovalUx6F(snapshot.Approval);
        }
    }

    private void OpenBound(StudioSessionController session, bool evidence, bool cancelApproval = true)
    {
        _session = session;
        if (cancelApproval) _session.CancelUx6FApproval();
        ResetControls();
        RefreshSources();
        Visible = true;
        MoveToFront();
        UpdateBounds();
        SetStatus(evidence
            ? "Copie temporaire UX6-L · aucune source du module ouvert ne sera modifiée."
            : "Choisissez une source puis configurez les cinq scénarios runtime.", false);
    }

    private void RefreshSources()
    {
        _choices = _session.ScriptSourceChoicesUx6F();
        _updating = true;
        _sourceChoice.Clear();
        _sourceChoice.AddItem("Choisir explicitement une source…");
        foreach (var choice in _choices) _sourceChoice.AddItem(choice.Label);
        _sourceChoice.Select(0);
        _updating = false;
        _draft = null;
        _explicitSource = false;
        _source.Text = string.Empty;
        InvalidateAll();
    }

    private void SelectSource(long selected)
    {
        if (_updating || selected <= 0 || selected > _choices.Count) return;
        var choice = _choices[(int)selected - 1];
        if (!_session.TryOpenCanonicalSourceUx6F(choice.RelativePath, out var draft, out var message) ||
            draft is null)
        {
            SetStatus(message, true);
            return;
        }
        _draft = draft;
        _explicitSource = true;
        _updating = true;
        _source.Text = draft.Source;
        PopulateRuntime(_session.DefaultRuntimeToolsDraftUx6L());
        PopulateLiveToolsUx6N(_session.DefaultLiveToolsDraftUx6N());
        _updating = false;
        _selection.Text = HumanValue($"{choice.Label} · API {draft.ApiVersion} · {choice.Detail}");
        InvalidateAll();
        SetStatus(message, false);
    }

    private void CreateSource()
    {
        if (!_session.TryCreateCanonicalSourceUx6F(_newSourcePath.Text, out var draft, out var message) ||
            draft is null)
        {
            SetStatus(message, true);
            return;
        }
        _draft = draft;
        _explicitSource = true;
        _updating = true;
        _source.Text = draft.Source;
        PopulateRuntime(_session.DefaultRuntimeToolsDraftUx6L());
        PopulateLiveToolsUx6N(_session.DefaultLiveToolsDraftUx6N());
        _updating = false;
        _selection.Text = $"Nouvelle source canonique préparée en mémoire · API {draft.ApiVersion}.";
        InvalidateAll();
        SetStatus(message, false);
    }

    private void BuildSource()
    {
        var source = ReadSourceDraft();
        var runtime = ReadRuntimeDraft();
        if (source is null || runtime is null) return;
        _plan = _session.BuildRuntimeToolsSourceUx6L(source, runtime);
        InvalidateLiveToolsUx6N();
        if (_plan.Succeeded)
        {
            _livePlanUx6N = _session.BuildLiveToolsSourceUx6N(
                source with { Source = _plan.CandidateSource }, ReadLiveDraftUx6N()!);
            if (_livePlanUx6N.Succeeded)
                _plan = _plan with
                {
                    ReviewedSource = _plan.ReviewedSource + System.Environment.NewLine +
                                     System.Environment.NewLine +
                                     _livePlanUx6N.ReviewedSource,
                    CandidateSource = _livePlanUx6N.CandidateSource,
                };
            else
                _plan = _plan with
                {
                    Succeeded = false,
                    Message = _livePlanUx6N.Message,
                    Diagnostics = _livePlanUx6N.Diagnostics,
                };
        }
        _review.Text = _plan.Succeeded
            ? "SOURCE VERSIONNÉE À RELIRE\n" + HumanValue(_plan.ReviewedSource)
            : HumanValue(_plan.Message);
        _review.AddThemeColorOverride("font_color", _plan.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
        RenderDiagnostics(_plan.Diagnostics);
        UpdateButtons();
        SetStatus(_plan.Message, !_plan.Succeeded);
    }

    private void InsertSource()
    {
        if (_plan is not { Succeeded: true })
        {
            SetStatus("Construisez d’abord un bloc valide et relisez-le.", true);
            return;
        }
        _updating = true;
        _source.Text = _plan.CandidateSource;
        _updating = false;
        _runtime = null;
        _liveRuntimeUx6N = null;
        _previewResult = null;
        _compileResult = null;
        _session.CancelUx6FApproval();
        UpdateButtons();
        SetStatus("Bloc R099 inséré dans le brouillon · lancez simulation, compilation et aperçu sec.", false);
    }

    private void Simulate()
    {
        var source = ReadSourceDraft();
        var runtime = ReadRuntimeDraft();
        if (source is null || runtime is null) return;
        _runtime = _session.PreviewRuntimeToolsUx6L(source, runtime);
        var live = ReadLiveDraftUx6N();
        _liveRuntimeUx6N = live is null ? null : _session.PreviewLiveToolsUx6N(source, live);
        _runtimeReview.Text = HumanValue(_runtime.Message) + "\n" +
                              $"Audio {Bool(_runtime.AudioLifecycle && _runtime.AudioHandleControl)} · " +
                              $"Temps {Bool(_runtime.DeterministicClock && _runtime.TimerDueAndRepeat)} · " +
                              $"Événements {Bool(_runtime.EventSchema && _runtime.EventDeliveryOrder)} · " +
                              $"Natif {Bool(_runtime.NativeExecution)}\n" + LiveReviewUx6N();
        _runtimeReview.AddThemeColorOverride("font_color", RuntimeReady ? StudioStyle.Success : StudioStyle.Danger);
        var trace = _runtime.Events.Concat(_liveRuntimeUx6N?.Trace ?? []).ToArray();
        _console.Text = trace.Length == 0
            ? "Trace · aucune intention produite."
            : "TRACE BORNÉE\n" + string.Join("\n", trace.TakeLast(6).Select(HumanValue));
        RenderDiagnostics(_runtime.Diagnostics.Concat(_liveRuntimeUx6N?.Diagnostics ?? []).ToArray());
        UpdateButtons();
        SetStatus(_runtime.Message, !_runtime.Succeeded);
    }

    private void Preview()
    {
        var source = ReadSourceDraft();
        if (source is null || !ReviewedSourceReady)
        {
            SetStatus("Insérez d’abord le bloc R099 revu dans le brouillon.", true);
            return;
        }
        _previewResult = _session.PreviewScriptSourceUx6F(source);
        _review.Text = HumanValue(_previewResult.SemanticDiff) + "\n" + HumanValue(_previewResult.Message);
        RenderDiagnostics(_previewResult.Diagnostics);
        UpdateButtons();
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
    }

    private void Compile()
    {
        var source = ReadSourceDraft();
        if (source is null) return;
        _compileResult = _session.CompileScriptSourceUx6F(source);
        RenderDiagnostics(_compileResult.Diagnostics);
        UpdateButtons();
        SetStatus(_compileResult.Message, !_compileResult.Succeeded);
    }

    private void Apply()
    {
        var source = ReadSourceDraft();
        if (source is null || !PreviewReady)
        {
            SetStatus("Relancez l’aperçu sec avant d’enregistrer.", true);
            return;
        }
        var result = _session.ApplyApprovedScriptSourceUx6F(source);
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        ScriptSourceChanged?.Invoke();
        ReloadSelectedSource();
    }

    private void Undo()
    {
        var result = _session.UndoScriptSourceUx6F();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { ScriptSourceChanged?.Invoke(); ReloadSelectedSource(); }
    }

    private void Redo()
    {
        var result = _session.RedoScriptSourceUx6F();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { ScriptSourceChanged?.Invoke(); ReloadSelectedSource(); }
    }

    private void ReloadSelectedSource()
    {
        var relative = _draft?.RelativePath;
        if (relative is null) return;
        if (_session.TryOpenCanonicalSourceUx6F(relative, out var draft, out _) && draft is not null)
        {
            _draft = draft;
            _updating = true;
            _source.Text = draft.Source;
            _updating = false;
        }
        InvalidateAll();
    }

    private StudioUx6FDraft? ReadSourceDraft() => _draft is null ? null : _draft with { Source = _source.Text };

    private StudioUx6LRuntimeDraft? ReadRuntimeDraft()
    {
        if (!HasExplicitSelection) return null;
        return new(
            _audioCue.Text,
            _audioBus.Text,
            _audioVolume.Value,
            _fadeIn.Value,
            _fadeOut.Value,
            _loop.ButtonPressed,
            _snapshot.Text,
            _listener.Text,
            ListenerPolicies[Math.Clamp(_listenerPolicy.Selected, 0, ListenerPolicies.Length - 1)],
            _timeScale.Value,
            _advance.Value,
            _delay.Value,
            _repeat.Value,
            _timerHandler.Text,
            _timerSchedule.Text,
            _eventName.Text,
            (int)_eventVersion.Value,
            _eventField.Text,
            (StudioUx6LEventFieldKind)Math.Clamp(_eventFieldKind.Selected, 0,
                Enum.GetValues<StudioUx6LEventFieldKind>().Length - 1),
            _eventValue.Text,
            [_subscriberOne.Text, _subscriberTwo.Text]);
    }

    private void PopulateRuntime(StudioUx6LRuntimeDraft runtime)
    {
        _audioCue.Text = runtime.AudioCueId;
        _audioBus.Text = runtime.AudioBus;
        _audioVolume.Value = runtime.AudioVolume;
        _fadeIn.Value = runtime.FadeInSeconds;
        _fadeOut.Value = runtime.FadeOutSeconds;
        _loop.ButtonPressed = runtime.Loop;
        _snapshot.Text = runtime.Snapshot;
        _listener.Text = runtime.Listener;
        _listenerPolicy.Select(Array.IndexOf(ListenerPolicies, runtime.ListenerPolicy));
        _timeScale.Value = runtime.TimeScale;
        _advance.Value = runtime.AdvanceSeconds;
        _delay.Value = runtime.TimerDelaySeconds;
        _repeat.Value = runtime.TimerRepeatSeconds;
        _timerHandler.Text = runtime.TimerHandler;
        _timerSchedule.Text = runtime.TimerSchedule;
        _eventName.Text = runtime.EventName;
        _eventVersion.Value = runtime.EventVersion;
        _eventField.Text = runtime.EventField;
        _eventFieldKind.Select((int)runtime.EventFieldKind);
        _eventValue.Text = runtime.EventValue;
        _subscriberOne.Text = runtime.Subscribers.ElementAtOrDefault(0) ?? "audio";
        _subscriberTwo.Text = runtime.Subscribers.ElementAtOrDefault(1) ?? "quest";
    }

    private void HookRuntimeControls()
    {
        foreach (var line in new[] { _audioCue, _audioBus, _snapshot, _listener, _timerHandler,
                     _timerSchedule, _eventName, _eventField, _eventValue, _subscriberOne, _subscriberTwo })
            line.TextChanged += _ => RuntimeChanged();
        foreach (var number in new[] { _audioVolume, _fadeIn, _fadeOut, _timeScale, _advance,
                     _delay, _repeat, _eventVersion })
            number.ValueChanged += _ => RuntimeChanged();
        _loop.Toggled += _ => RuntimeChanged();
        _listenerPolicy.ItemSelected += _ => RuntimeChanged();
        _eventFieldKind.ItemSelected += _ => RuntimeChanged();
    }

    private void RuntimeChanged()
    {
        if (_updating) return;
        _plan = null;
        _runtime = null;
        InvalidateLiveToolsUx6N();
        _previewResult = null;
        _compileResult = null;
        _session.CancelUx6FApproval();
        _review.Text = "Paramètres modifiés · reconstruisez le bloc revu.";
        _runtimeReview.Text = "Simulation · en attente après modification.";
        UpdateButtons();
    }

    private void SourceChanged()
    {
        if (_updating) return;
        _runtime = null;
        _liveRuntimeUx6N = null;
        _previewResult = null;
        _compileResult = null;
        _session.CancelUx6FApproval();
        UpdateButtons();
    }

    private void InvalidateAll()
    {
        _plan = null;
        _runtime = null;
        InvalidateLiveToolsUx6N();
        _previewResult = null;
        _compileResult = null;
        _session.CancelUx6FApproval();
        _review.Text = "Aucun bloc construit · relisez la source générée avant insertion.";
        _runtimeReview.Text = "Simulation · en attente.";
        _diagnostics.Text = "Diagnostics · aucun aperçu.";
        _console.Text = "Trace · aucune intention exécutée.";
        UpdateButtons();
    }

    private void ResetControls()
    {
        _updating = true;
        _choices = [];
        _draft = null;
        _explicitSource = false;
        _sourceChoice.Clear();
        _sourceChoice.AddItem("Choisir explicitement une source…");
        _source.Text = string.Empty;
        PopulateRuntime(_session.DefaultRuntimeToolsDraftUx6L());
        PopulateLiveToolsUx6N(_session.DefaultLiveToolsDraftUx6N());
        _updating = false;
        _selection.Text = "Choisissez une source .as canonique.";
        InvalidateAll();
    }

    private void UpdateButtons()
    {
        var selected = HasExplicitSelection;
        _build.Disabled = !selected;
        _insert.Disabled = _plan is not { Succeeded: true };
        _simulate.Disabled = !selected || !ReviewedSourceReady;
        _preview.Disabled = !selected || !ReviewedSourceReady;
        _compile.Disabled = !selected || !ReviewedSourceReady;
        _apply.Disabled = !PreviewReady || !RuntimeReady || !CompilationReady;
        _undo.Disabled = !selected;
        _redo.Disabled = !selected;
    }

    private void RenderDiagnostics(IReadOnlyList<StudioUx6FDiagnostic> diagnostics)
    {
        _diagnostics.Text = diagnostics.Count == 0
            ? "Diagnostics · aucun blocage."
            : "DIAGNOSTICS\n" + string.Join("\n", diagnostics.Take(5)
                .Select(item => HumanValue($"{item.Code} · {item.Path} · {item.Message}")));
        _diagnostics.AddThemeColorOverride("font_color", diagnostics.Count == 0
            ? StudioStyle.Success
            : StudioStyle.Danger);
    }

    private StudioUx6FEvidenceSourceSnapshot CaptureSource()
    {
        var project = _sourceSession.Project ?? throw new InvalidOperationException("No source project.");
        var history = _sourceSession.History ?? throw new InvalidOperationException("No source history.");
        return new(project, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.CaptureScriptApprovalUx6F());
    }

    private bool SourceMatches(StudioUx6FEvidenceSourceSnapshot? expected = null)
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

    private bool FailEvidence(string message)
    {
        SetStatus(message, true);
        ReleaseEvidence();
        Visible = false;
        return false;
    }

    private bool ReleaseEvidence()
    {
        var isolated = _evidenceSession;
        var root = _evidenceRoot;
        var sessionReleased = true;
        try { isolated?.Close(); }
        catch (Exception exception)
        {
            sessionReleased = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_L_RELEASE_FAILED type={exception.GetType().Name}");
        }
        var rootReleased = StudioSessionController.DeleteUx6FTemporaryWorkspace(root);
        _evidenceSession = null;
        _evidenceRoot = null;
        _session = _sourceSession;
        _evidenceReleased = sessionReleased && rootReleased;
        return _evidenceReleased;
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = HumanValue(message);
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(HumanValue(message), error);
    }

    private void UpdateBounds()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 40, 1460, 1880);
        var height = Math.Clamp(viewport.Y - 40, 780, 1040);
        _dialog.Position = new Vector2((viewport.X - width) / 2, (viewport.Y - height) / 2);
        _dialog.Size = new Vector2(width, height);
    }

    private static VBoxContainer PanelColumn(HBoxContainer host, int minimum, bool expand = false)
    {
        var panel = StudioStyle.Panel("Ux6LColumn", StudioStyle.SurfaceRaised, 8, 1);
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

    private static VBoxContainer TabPage(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        tabs.AddChild(scroll);
        var margin = StudioStyle.Margin(10, 10, 10, 10);
        scroll.AddChild(margin);
        var page = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(600, 0),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        page.AddThemeConstantOverride("separation", 8);
        margin.AddChild(page);
        return page;
    }

    private static VBoxContainer Field(string label, Control control, int minimum = 118)
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(minimum, 0) };
        box.AddThemeConstantOverride("separation", 2);
        box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        box.AddChild(control);
        return box;
    }

    private static HBoxContainer Pair(string leftLabel, Control left, string rightLabel, Control right)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(Field(leftLabel, left, 270));
        row.AddChild(Field(rightLabel, right, 270));
        return row;
    }

    private static LineEdit Text(string value)
    {
        var line = StudioUx2EnvBControls.Text(value);
        line.Text = value;
        return line;
    }

    private static SpinBox Number(double minimum, double maximum, double step, double value)
    {
        var number = StudioUx2EnvBControls.Number(minimum, maximum, step);
        number.Value = value;
        number.CustomArrowStep = step;
        return number;
    }

    private static OptionButton Options(params string[] labels)
    {
        var option = StudioUx2EnvBControls.Options();
        foreach (var label in labels) option.AddItem(label);
        option.Select(0);
        return option;
    }

    private static IEnumerable<string> DescendantText(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label) yield return label.Text;
            if (child is Button button) yield return button.Text;
            if (child is OptionButton option)
                for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
            foreach (var value in DescendantText(child)) yield return value;
        }
    }

    private static bool ContainsUuid(string text) => Regex.IsMatch(text,
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.CultureInvariant);

    private static string HumanValue(string value) => Regex.Replace(value ?? string.Empty,
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        "identifiant protégé", RegexOptions.CultureInvariant);

    private static string Bool(bool value) => value ? "prêt" : "incomplet";

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBounds();
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
            Preview();
            GetViewport().SetInputAsHandled();
        }
    }
}
