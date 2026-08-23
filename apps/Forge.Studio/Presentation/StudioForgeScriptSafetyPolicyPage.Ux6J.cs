using System.Globalization;
using System.Text.RegularExpressions;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioForgeScriptSafetyPolicyPageUx6J : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx6JSourceSnapshot? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _sourceChoice;
    private readonly Label _selection;
    private readonly CodeEdit _source;
    private readonly OptionButton _preset;
    private readonly OptionButton _area;
    private readonly OptionButton _entity;
    private readonly OptionButton _asset;
    private readonly LineEdit _handlerInstructions;
    private readonly LineEdit _tickInstructions;
    private readonly LineEdit _moduleInstructions;
    private readonly LineEdit _timeout;
    private readonly LineEdit _recursion;
    private readonly LineEdit _spawn;
    private readonly LineEdit _contextMemory;
    private readonly LineEdit _moduleMemory;
    private readonly LineEdit _seed;
    private readonly LineEdit _simulationTick;
    private readonly LineEdit _simulationTime;
    private readonly OptionButton _authority;
    private readonly CheckBox _required;
    private readonly LineEdit _apiVersion;
    private readonly VBoxContainer _diagnosticRows;
    private readonly Label _policyReview;
    private readonly Label _runtimeReview;
    private readonly Label _console;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _simulate;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx6FSourceChoice> _sourceChoices = [];
    private StudioUx6JCatalog _catalog = new([], [], []);
    private StudioUx6FDraft? _sourceDraft;
    private StudioUx6JPolicyDraft? _policyDraft;
    private StudioUx6JPreview? _previewResult;
    private StudioUx6JSimulation? _simulationResult;
    private bool _explicitSource;
    private bool _explicitArea;
    private bool _explicitEntity;
    private bool _explicitAsset;
    private bool _updating;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? PolicyChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSource && _explicitArea && _explicitEntity &&
                                          _explicitAsset && _sourceDraft is not null && _policyDraft is not null;
    internal bool HumanCatalogsReady => _catalog.Complete;
    internal bool PreviewReady => _previewResult is { Succeeded: true, CanApply: true, CanonicalDryRun: true };
    internal bool SimulationReady => _simulationResult is
    {
        Succeeded: true,
        OpaqueHandles: true,
        WrongTypeBlocked: true,
        StaleHandleBlocked: true,
        GenerationAdvanced: true,
        InstructionQuotaBlocked: true,
        AggregateQuotaBlocked: true,
        MemoryQuotaBlocked: true,
        CancellationBlocked: true,
        RuntimeRecovered: true,
        DeterministicReplay: true,
        SimulationClock: true,
        ApiNegotiation: true,
        CanonicalSourceCompiled: true,
    };
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
            return text.Contains("Handles opaques", StringComparison.Ordinal) &&
                   text.Contains("Budget d’instructions", StringComparison.Ordinal) &&
                   text.Contains("Budget mémoire", StringComparison.Ordinal) &&
                   text.Contains("Déterminisme", StringComparison.Ordinal) &&
                   text.Contains("Version d’API", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec", StringComparison.Ordinal) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioForgeScriptSafetyPolicyPageUx6J(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.008f, .014f, .023f, .985f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6JForgeScriptSafety", StudioStyle.Surface, 12, 1);
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
        headingText.AddChild(StudioStyle.Label("ForgeScript · sécurité, budgets et compatibilité R014", 19));
        headingText.AddChild(StudioStyle.Label(
            "Handles opaques · Budget d’instructions · Budget mémoire · Déterminisme · Version d’API · Aperçu sec canonique · aucune donnée moteur exposée.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        var sourceRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sourceChoice = StudioUx2EnvBControls.Options();
        _sourceChoice.CustomMinimumSize = new Vector2(485, 34);
        _sourceChoice.ItemSelected += SelectSource;
        sourceRow.AddChild(Field("Source canonique · sélection explicite", _sourceChoice, 485));
        _preset = StudioUx2EnvBControls.Options();
        _preset.CustomMinimumSize = new Vector2(280, 34);
        _preset.AddItem("Conserver la politique", 0);
        _preset.AddItem("Profil strict auteur", 1);
        _preset.AddItem("Profil standard serveur", 2);
        _preset.AddItem("Profil large contrôlé", 3);
        _preset.ItemSelected += ApplyPreset;
        sourceRow.AddChild(Field("Profil humain", _preset, 280));
        var reload = StudioStyle.Button("Recharger", compact: true);
        reload.CustomMinimumSize = new Vector2(120, 34);
        reload.Pressed += Reload;
        sourceRow.AddChild(reload);
        root.AddChild(sourceRow);
        _selection = StudioStyle.Label(
            "Choisissez une source .as et trois sujets humains. Les handles restent internes et aucun UUID n’est affiché.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var body = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 485),
            CustomMaximumSize = new Vector2(-1, 720),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            PropagateMaximumSize = true,
        };
        body.AddThemeConstantOverride("separation", 9);
        root.AddChild(body);

        var left = PanelColumn(body, 640, 1.35f);
        left.AddChild(StudioStyle.Label("SOURCE REVUE · COMPILÉE SANS MUTATION", 10, StudioStyle.Accent));
        _source = new CodeEdit
        {
            Editable = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(640, 245),
            DrawTabs = true,
            GuttersDrawLineNumbers = true,
        };
        left.AddChild(_source);
        left.AddChild(StudioStyle.Label("SUJETS DE HANDLE · LIBELLÉS DU MODULE", 9, StudioStyle.Accent));
        var subjects = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _area = StudioUx2EnvBControls.Options();
        _entity = StudioUx2EnvBControls.Options();
        _asset = StudioUx2EnvBControls.Options();
        _area.ItemSelected += selected => SelectSubject(selected, SubjectKind.Area);
        _entity.ItemSelected += selected => SelectSubject(selected, SubjectKind.Entity);
        _asset.ItemSelected += selected => SelectSubject(selected, SubjectKind.Asset);
        subjects.AddChild(Field("Zone", _area, 190));
        subjects.AddChild(Field("Objet", _entity, 215));
        subjects.AddChild(Field("Ressource", _asset, 215));
        left.AddChild(subjects);
        left.AddChild(StudioStyle.Label("DIAGNOSTICS CLIQUABLES", 9, StudioStyle.Accent));
        var diagnosticScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 135),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _diagnosticRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        diagnosticScroll.AddChild(_diagnosticRows);
        left.AddChild(diagnosticScroll);

        var right = PanelColumn(body, 600, 1f);
        right.AddChild(StudioStyle.Label("POLITIQUE R014 · AUTORITÉ CANONIQUE VERSIONNÉE", 10,
            StudioStyle.Accent));
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 345),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        right.AddChild(tabsHost);
        var tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabsHost.AddChild(tabs);

        var handlesPage = TabPage(tabs, "Handles");
        handlesPage.AddChild(StudioStyle.Label("Handles opaques typés et générationnels", 12,
            StudioStyle.Accent));
        handlesPage.AddChild(StudioStyle.Label(
            "Entity, Area, Asset, Effect et MovementTask sont validés à chaque appel. Le mauvais type et les générations libérées sont refusés sans jamais exposer un pointeur.",
            10, StudioStyle.Muted, wrap: true));
        handlesPage.AddChild(StudioStyle.Label(
            "La simulation enregistre les trois sujets choisis, provoque un mauvais type, libère un handle puis vérifie l’incrément de génération.",
            10, StudioStyle.Muted, wrap: true));

        var instructionPage = TabPage(tabs, "Instructions");
        instructionPage.AddChild(StudioStyle.Label("Budget d’instructions et arrêt contrôlé", 12,
            StudioStyle.Accent));
        _handlerInstructions = TextNumber();
        _tickInstructions = TextNumber();
        _moduleInstructions = TextNumber();
        _timeout = TextNumber();
        _recursion = TextNumber();
        _spawn = TextNumber();
        instructionPage.AddChild(Pair("Par handler", _handlerInstructions, "Par tick", _tickInstructions));
        instructionPage.AddChild(Pair("Par module", _moduleInstructions, "Timeout (ms)", _timeout));
        instructionPage.AddChild(Pair("Récursion", _recursion, "Spawn", _spawn));
        instructionPage.AddChild(StudioStyle.Label(
            "Ordre obligatoire : handler ≤ tick ≤ module. Timeout, annulation et dépassement agrégé rendent un diagnostic sans arrêter la VM.",
            9, StudioStyle.Muted, wrap: true));

        var memoryPage = TabPage(tabs, "Mémoire");
        memoryPage.AddChild(StudioStyle.Label("Budget mémoire mesuré par contexte et module", 12,
            StudioStyle.Accent));
        _contextMemory = TextNumber();
        _moduleMemory = TextNumber();
        memoryPage.AddChild(Pair("Contexte (octets)", _contextMemory, "Module (octets)", _moduleMemory));
        memoryPage.AddChild(StudioStyle.Label(
            "Le pic d’allocation est télémétré. Un contexte dépassé est interrompu proprement et le handler suivant doit rester sain.",
            10, StudioStyle.Muted, wrap: true));

        var deterministicPage = TabPage(tabs, "Déterminisme");
        deterministicPage.AddChild(StudioStyle.Label("RNG, horloge simulée et autorité explicite", 12,
            StudioStyle.Accent));
        _seed = TextNumber();
        _simulationTick = TextNumber();
        _simulationTime = TextNumber();
        _authority = StudioUx2EnvBControls.Options();
        foreach (var authority in Enum.GetValues<ForgeScriptExecutionAuthority>())
            _authority.AddItem(HumanAuthority(authority), (int)authority);
        _required = new CheckBox { Text = "Politique requise au runtime", ButtonPressed = true };
        deterministicPage.AddChild(Pair("Graine non nulle", _seed, "Tick simulé", _simulationTick));
        deterministicPage.AddChild(Pair("Temps simulé (ms)", _simulationTime, "Autorité", _authority));
        deterministicPage.AddChild(_required);

        var apiPage = TabPage(tabs, "API");
        apiPage.AddChild(StudioStyle.Label("Version d’API et négociation de capacités", 12,
            StudioStyle.Accent));
        _apiVersion = StudioUx2EnvBControls.Text("1.0.0");
        apiPage.AddChild(Field("Version demandée", _apiVersion, 260));
        apiPage.AddChild(StudioStyle.Label(
            "La version 1.0 et 1.0.0 négocient les capacités handles opaques et déterminisme. Les dépréciations et migrations restent visibles avant compilation.",
            10, StudioStyle.Muted, wrap: true));

        HookPolicyControls();
        _policyReview = StudioStyle.Label(
            "Aucun aperçu sec. Les valeurs sont relues comme entiers exacts, sans arrondi ni clamp silencieux.",
            9, StudioStyle.Muted, wrap: true);
        _policyReview.CustomMinimumSize = new Vector2(0, 58);
        right.AddChild(_policyReview);
        _runtimeReview = StudioStyle.Label(
            "Aucune sandbox R014 exécutée. La simulation n’écrit ni source ni projet.",
            9, StudioStyle.Muted, wrap: true);
        _runtimeReview.CustomMinimumSize = new Vector2(0, 68);
        right.AddChild(_runtimeReview);
        _console = StudioStyle.Label("Console R014 prête.", 9, StudioStyle.Muted, wrap: true);
        _console.CustomMinimumSize = new Vector2(0, 74);
        right.AddChild(_console);

        _status = StudioStyle.Label("Sélectionnez une source et les trois sujets de handle.", 10,
            StudioStyle.Warning, wrap: true);
        root.AddChild(_status);
        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _preview = ActionButton("Prévisualiser", Preview, 126);
        _simulate = ActionButton("Simuler sandbox", Simulate, 145);
        _apply = ActionButton("Appliquer", Apply, 116);
        _undo = ActionButton("Annuler", Undo, 105);
        _redo = ActionButton("Rétablir", Redo, 105);
        _save = ActionButton("Enregistrer", Save, 120);
        footer.AddChild(_preview);
        footer.AddChild(_simulate);
        footer.AddChild(_apply);
        footer.AddChild(_undo);
        footer.AddChild(_redo);
        footer.AddChild(_save);
        root.AddChild(footer);
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
        if (Visible) _session.CancelUx6JApproval();
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
        if (!mode.StartsWith("ux6-j", StringComparison.OrdinalIgnoreCase))
            return FailEvidence("Mode de preuve UX6-J inconnu.");
        ReleaseEvidence();
        _sourceSnapshot = CaptureSource();
        if (_sourceSnapshot is null || _sourceSession.Project is null)
            return FailEvidence("Le module source n’est pas ouvert.");
        _evidenceRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-j-ui",
            Guid.NewGuid().ToString("N"));
        if (!ProjectSession.Create(_evidenceRoot, _sourceSession.Project).Succeeded)
            return FailEvidence("Impossible de créer la copie temporaire R014.");
        new ForgeScriptSourceStore(_evidenceRoot).Save(StudioSessionController.EvidenceDocumentUx6F());
        _evidenceSession = new StudioSessionController();
        if (!_evidenceSession.Open(_evidenceRoot).Succeeded)
            return FailEvidence("Impossible d’ouvrir la copie temporaire R014.");
        _session = _evidenceSession;
        OpenBound(_session, evidence: true);
        if (_sourceChoice.ItemCount < 2 || _area.ItemCount < 2 || _entity.ItemCount < 2 || _asset.ItemCount < 2)
            return FailEvidence("La copie temporaire ne fournit pas les sélections R014.");
        _sourceChoice.Select(1);
        SelectSource(1);
        _area.Select(1);
        SelectSubject(1, SubjectKind.Area);
        _entity.Select(1);
        SelectSubject(1, SubjectKind.Entity);
        _asset.Select(1);
        SelectSubject(1, SubjectKind.Asset);
        _preset.Select(1);
        ApplyPreset(1);
        Preview();
        Simulate();
        var isolated = UsesIsolatedEvidence;
        var explicitSelection = HasExplicitSelection;
        var humanCatalogs = HumanCatalogsReady;
        var preview = PreviewReady;
        var simulation = SimulationReady;
        var humanSurface = ShowsHumanSurface;
        var sourceUntouched = SourceMatches();
        var ready = isolated && explicitSelection && humanCatalogs && preview && simulation && humanSurface &&
                    sourceUntouched;
        if (!ready)
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_J_UI_FAILED " +
                        $"isolated={isolated.ToString().ToLowerInvariant()} " +
                        $"explicit={explicitSelection.ToString().ToLowerInvariant()} " +
                        $"catalogs={humanCatalogs.ToString().ToLowerInvariant()} " +
                        $"preview={preview.ToString().ToLowerInvariant()} " +
                        $"simulation={simulation.ToString().ToLowerInvariant()} " +
                        $"human={humanSurface.ToString().ToLowerInvariant()} " +
                        $"source={sourceUntouched.ToString().ToLowerInvariant()}");
        return ready || FailEvidence("La surface R014 n’a pas satisfait tous les invariants humains.");
    }

    internal bool RunUiTransactionEvidence()
    {
        var ready = PrepareEvidence("ux6-j-script-safety-policy");
        try
        {
            if (!ready || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !PreviewReady || !SimulationReady)
                return false;
            var draft = ReadPolicy();
            if (draft is null) return false;
            var applied = _session.ApplyApprovedExecutionPolicyUx6J(draft);
            var appliedPolicy = new ForgeScriptExecutionPolicyStore(_evidenceRoot!).Load();
            var undone = _session.UndoExecutionPolicyUx6J();
            var undoPolicy = new ForgeScriptExecutionPolicyStore(_evidenceRoot!).Load();
            var redone = _session.RedoExecutionPolicyUx6J();
            var redoPolicy = new ForgeScriptExecutionPolicyStore(_evidenceRoot!).Load();
            var saved = _session.SaveExecutionPolicyUx6J();
            var reopened = new StudioSessionController();
            var persisted = reopened.Open(_evidenceRoot!).Succeeded &&
                            reopened.CurrentExecutionPolicyUx6J() is { } reopenedDraft &&
                            PolicyContentsEqual(reopenedDraft, draft);
            reopened.Close();
            return applied.Succeeded && undone.Succeeded && redone.Succeeded && saved.Succeeded && persisted &&
                   PolicyContentsEqual(appliedPolicy, draft) &&
                   !PolicyContentsEqual(undoPolicy, draft) && PolicyContentsEqual(redoPolicy, draft) &&
                   SourceMatches();
        }
        finally
        {
            CloseEvidence();
        }
    }

    private void OpenBound(StudioSessionController session, bool evidence, bool cancelApproval = true)
    {
        _session = session;
        if (cancelApproval) _session.CancelUx6JApproval();
        Visible = true;
        ResetControls();
        RefreshSources();
        PopulateCatalog();
        ReloadPolicy();
        _selection.Text = evidence
            ? "COPIE TEMPORAIRE DU MODULE RÉEL · source, projet et approbations d’origine strictement inchangés."
            : "Choisissez une source .as et trois sujets humains. La politique reste séparée du projet.";
        UpdateBounds();
    }

    private void RefreshSources()
    {
        _sourceChoices = _session.ScriptSourceChoicesUx6F()
            .Where(choice => choice.Kind == StudioUx6FSourceKind.Canonical)
            .ToArray();
        _updating = true;
        _sourceChoice.Clear();
        _sourceChoice.AddItem("Choisir une source .as…", 0);
        foreach (var choice in _sourceChoices) _sourceChoice.AddItem($"{choice.Label} · {choice.Detail}");
        _sourceChoice.Select(0);
        _updating = false;
        _sourceDraft = null;
        _source.Text = _sourceChoices.Count == 0
            ? "// Aucune source canonique. Créez-la d’abord dans Fondation ForgeScript."
            : "// Sélectionnez explicitement une source canonique.";
        _explicitSource = false;
        Invalidate();
    }

    private void SelectSource(long selected)
    {
        if (_updating || selected <= 0 || selected > _sourceChoices.Count) return;
        var choice = _sourceChoices[(int)selected - 1];
        if (!_session.TryOpenCanonicalSourceUx6F(choice.RelativePath, out var draft, out var message) ||
            draft is null)
        {
            SetStatus(message, error: true);
            return;
        }
        _sourceDraft = draft;
        _source.Text = draft.Source;
        _explicitSource = true;
        Invalidate();
        SetStatus($"Source revue · {choice.Label}.");
    }

    private void PopulateCatalog()
    {
        _catalog = _session.ScriptSafetyCatalogUx6J();
        PopulateChoices(_area, "Choisir une zone…", _catalog.Areas);
        PopulateChoices(_entity, "Choisir un objet…", _catalog.Entities);
        PopulateChoices(_asset, "Choisir une ressource…", _catalog.Assets);
        _explicitArea = _explicitEntity = _explicitAsset = false;
    }

    private void SelectSubject(long selected, SubjectKind kind)
    {
        if (_updating || selected <= 0) return;
        switch (kind)
        {
            case SubjectKind.Area: _explicitArea = selected <= _catalog.Areas.Count; break;
            case SubjectKind.Entity: _explicitEntity = selected <= _catalog.Entities.Count; break;
            case SubjectKind.Asset: _explicitAsset = selected <= _catalog.Assets.Count; break;
        }
        InvalidateSimulation();
    }

    private void Reload()
    {
        RefreshSources();
        PopulateCatalog();
        ReloadPolicy();
        SetStatus("Sources, sujets et politique rechargés.");
    }

    private void ReloadPolicy()
    {
        _policyDraft = _session.CurrentExecutionPolicyUx6J();
        if (_policyDraft is null)
        {
            SetStatus("La politique R014 est absente ou invalide.", error: true);
            UpdateButtons();
            return;
        }
        PopulatePolicy(_policyDraft);
        _previewResult = null;
        _simulationResult = null;
        RenderDiagnostics([]);
        UpdateButtons();
    }

    private void PopulatePolicy(StudioUx6JPolicyDraft draft)
    {
        _updating = true;
        _handlerInstructions.Text = draft.HandlerInstructionLimit.ToString(CultureInfo.InvariantCulture);
        _tickInstructions.Text = draft.TickInstructionLimit.ToString(CultureInfo.InvariantCulture);
        _moduleInstructions.Text = draft.ModuleInstructionLimit.ToString(CultureInfo.InvariantCulture);
        _timeout.Text = draft.TimeoutMilliseconds.ToString(CultureInfo.InvariantCulture);
        _recursion.Text = draft.RecursionLimit.ToString(CultureInfo.InvariantCulture);
        _spawn.Text = draft.SpawnLimit.ToString(CultureInfo.InvariantCulture);
        _contextMemory.Text = draft.ContextMemoryLimitBytes.ToString(CultureInfo.InvariantCulture);
        _moduleMemory.Text = draft.ModuleMemoryLimitBytes.ToString(CultureInfo.InvariantCulture);
        _seed.Text = draft.DeterministicSeed.ToString(CultureInfo.InvariantCulture);
        _simulationTick.Text = draft.SimulationTick.ToString(CultureInfo.InvariantCulture);
        _simulationTime.Text = draft.SimulationTimeMilliseconds.ToString(CultureInfo.InvariantCulture);
        _authority.Select((int)draft.Authority);
        _required.ButtonPressed = draft.RequiredAtRuntime;
        _apiVersion.Text = draft.ApiVersion;
        _preset.Select(0);
        _updating = false;
    }

    private void ApplyPreset(long selected)
    {
        if (_updating || _policyDraft is null || selected <= 0) return;
        var preset = selected switch
        {
            1 => _policyDraft with
            {
                HandlerInstructionLimit = 5_000,
                TickInstructionLimit = 25_000,
                ModuleInstructionLimit = 500_000,
                TimeoutMilliseconds = 75,
                ContextMemoryLimitBytes = 4 * 1024 * 1024,
                ModuleMemoryLimitBytes = 64L * 1024 * 1024,
                DeterministicSeed = 14_014,
                Authority = ForgeScriptExecutionAuthority.Server,
            },
            2 => ForgeScriptExecutionPolicy.Default is var standard
                ? _policyDraft with
                {
                    HandlerInstructionLimit = standard.HandlerInstructionLimit,
                    TickInstructionLimit = standard.TickInstructionLimit,
                    ModuleInstructionLimit = standard.ModuleInstructionLimit,
                    TimeoutMilliseconds = standard.TimeoutMilliseconds,
                    RecursionLimit = standard.RecursionLimit,
                    SpawnLimit = standard.SpawnLimit,
                    ContextMemoryLimitBytes = standard.ContextMemoryLimitBytes,
                    ModuleMemoryLimitBytes = standard.ModuleMemoryLimitBytes,
                    DeterministicSeed = standard.DeterministicSeed,
                    Authority = standard.Authority,
                }
                : _policyDraft,
            3 => _policyDraft with
            {
                HandlerInstructionLimit = 20_000,
                TickInstructionLimit = 100_000,
                ModuleInstructionLimit = 2_000_000,
                TimeoutMilliseconds = 250,
                ContextMemoryLimitBytes = 16 * 1024 * 1024,
                ModuleMemoryLimitBytes = 256L * 1024 * 1024,
                DeterministicSeed = 26_014,
                Authority = ForgeScriptExecutionAuthority.Server,
            },
            _ => _policyDraft,
        };
        PopulatePolicy(preset);
        _preset.Select((int)selected);
        InvalidatePolicy();
        SetStatus("Profil chargé dans le brouillon · prévisualisez avant application.");
    }

    private StudioUx6JPolicyDraft? ReadPolicy()
    {
        if (_policyDraft is null) return null;
        if (!TryInt(_handlerInstructions, out var handler) || !TryInt(_tickInstructions, out var tick) ||
            !TryLong(_moduleInstructions, out var module) || !TryInt(_timeout, out var timeout) ||
            !TryInt(_recursion, out var recursion) || !TryInt(_spawn, out var spawn) ||
            !TryInt(_contextMemory, out var contextMemory) || !TryLong(_moduleMemory, out var moduleMemory) ||
            !TryUlong(_seed, out var seed) || !TryLong(_simulationTick, out var simulationTick) ||
            !TryLong(_simulationTime, out var simulationTime))
        {
            SetStatus("Valeur entière invalide · aucun arrondi n’est appliqué.", error: true);
            return null;
        }
        return _policyDraft with
        {
            Provenance = "studio:ux6-j:r014",
            ApiVersion = _apiVersion.Text.Trim(),
            HandlerInstructionLimit = handler,
            TickInstructionLimit = tick,
            ModuleInstructionLimit = module,
            TimeoutMilliseconds = timeout,
            RecursionLimit = recursion,
            SpawnLimit = spawn,
            ContextMemoryLimitBytes = contextMemory,
            ModuleMemoryLimitBytes = moduleMemory,
            DeterministicSeed = seed,
            SimulationTick = simulationTick,
            SimulationTimeMilliseconds = simulationTime,
            Authority = (ForgeScriptExecutionAuthority)_authority.GetSelectedId(),
            RequiredAtRuntime = _required.ButtonPressed,
        };
    }

    private StudioUx6JHandleDraft? ReadHandles()
    {
        var area = ChoiceAt(_catalog.Areas, _area);
        var entity = ChoiceAt(_catalog.Entities, _entity);
        var asset = ChoiceAt(_catalog.Assets, _asset);
        return area is null || entity is null || asset is null
            ? null
            : new(area.Id, entity.Id, asset.Id);
    }

    private void Preview()
    {
        var draft = ReadPolicy();
        if (draft is null) return;
        _previewResult = _session.PreviewExecutionPolicyUx6J(draft);
        _policyReview.Text = _previewResult.Message + System.Environment.NewLine +
                             (_previewResult.Succeeded
                                 ? $"{ProtectTechnicalIdentifiers(_previewResult.SemanticDiff)} · API {_previewResult.SelectedApiVersion} · " +
                                   $"capacités {string.Join(", ", _previewResult.Capabilities)}"
                                 : "La politique canonique reste inchangée.");
        RenderDiagnostics(_previewResult.Diagnostics);
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
        UpdateButtons();
    }

    private void Simulate()
    {
        var draft = ReadPolicy();
        var handles = ReadHandles();
        if (draft is null || handles is null || _sourceDraft is null)
        {
            SetStatus("Sélectionnez la source, la zone, l’objet et la ressource avant la sandbox.", error: true);
            return;
        }
        _simulationResult = _session.SimulateExecutionPolicyUx6J(draft, handles, _sourceDraft);
        _runtimeReview.Text = _simulationResult.Message + System.Environment.NewLine +
                              $"Handles={YesNo(_simulationResult.OpaqueHandles)} · " +
                              $"instruction={YesNo(_simulationResult.InstructionQuotaBlocked)} · " +
                              $"mémoire={YesNo(_simulationResult.MemoryQuotaBlocked)} · " +
                              $"replay={YesNo(_simulationResult.DeterministicReplay)} · " +
                              $"API={YesNo(_simulationResult.ApiNegotiation)}";
        _console.Text = _simulationResult.Events.Count == 0
            ? "Aucun événement sandbox."
            : string.Join(System.Environment.NewLine,
                _simulationResult.Events.Take(6).Select(ProtectTechnicalIdentifiers));
        RenderDiagnostics(_simulationResult.Diagnostics);
        SetStatus(_simulationResult.Message, !_simulationResult.Succeeded);
        UpdateButtons();
    }

    private void Apply()
    {
        var draft = ReadPolicy();
        if (draft is null) return;
        var result = _session.ApplyApprovedExecutionPolicyUx6J(draft);
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            ReloadPolicy();
            PolicyChanged?.Invoke();
        }
        UpdateButtons();
    }

    private void Undo()
    {
        var result = _session.UndoExecutionPolicyUx6J();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { ReloadPolicy(); PolicyChanged?.Invoke(); }
        UpdateButtons();
    }

    private void Redo()
    {
        var result = _session.RedoExecutionPolicyUx6J();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { ReloadPolicy(); PolicyChanged?.Invoke(); }
        UpdateButtons();
    }

    private void Save()
    {
        var result = _session.SaveExecutionPolicyUx6J();
        SetStatus(result.Message, !result.Succeeded);
        UpdateButtons();
    }

    private void HookPolicyControls()
    {
        foreach (var edit in new[]
                 {
                     _handlerInstructions, _tickInstructions, _moduleInstructions, _timeout, _recursion, _spawn,
                     _contextMemory, _moduleMemory, _seed, _simulationTick, _simulationTime, _apiVersion,
                 })
            edit.TextChanged += _ => InvalidatePolicy();
        _authority.ItemSelected += _ => InvalidatePolicy();
        _required.Toggled += _ => InvalidatePolicy();
    }

    private void InvalidatePolicy()
    {
        if (_updating) return;
        _session.CancelUx6JApproval();
        _previewResult = null;
        _simulationResult = null;
        _policyReview.Text = "Brouillon modifié · relancez l’aperçu sec.";
        _runtimeReview.Text = "Sandbox périmée · relancez la simulation.";
        UpdateButtons();
    }

    private void InvalidateSimulation()
    {
        _simulationResult = null;
        _runtimeReview.Text = "Sujets modifiés · relancez la simulation sandbox.";
        UpdateButtons();
    }

    private void Invalidate()
    {
        _previewResult = null;
        _simulationResult = null;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var selection = HasExplicitSelection;
        _preview.Disabled = _policyDraft is null;
        _simulate.Disabled = !selection;
        _apply.Disabled = !PreviewReady;
        _undo.Disabled = _session.CurrentExecutionPolicyUx6J() is null;
        _redo.Disabled = _session.CurrentExecutionPolicyUx6J() is null;
        _save.Disabled = _policyDraft is null || !_session.ExecutionPolicyHasUnsavedChangesUx6J;
    }

    private void ResetControls()
    {
        _explicitSource = _explicitArea = _explicitEntity = _explicitAsset = false;
        _sourceDraft = null;
        _policyDraft = null;
        _previewResult = null;
        _simulationResult = null;
        _source.Text = string.Empty;
        _policyReview.Text = "Aucun aperçu sec.";
        _runtimeReview.Text = "Aucune sandbox R014 exécutée.";
        _console.Text = "Console R014 prête.";
        RenderDiagnostics([]);
    }

    private void RenderDiagnostics(IReadOnlyList<StudioUx6FDiagnostic> diagnostics)
    {
        ClearChildren(_diagnosticRows);
        if (diagnostics.Count == 0)
        {
            _diagnosticRows.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 9, StudioStyle.Success));
            return;
        }
        foreach (var diagnostic in diagnostics.Take(8))
        {
            var button = StudioStyle.Button($"{HumanDiagnostic(diagnostic.Code)} · {HumanPath(diagnostic.Path)}",
                compact: true);
            button.TooltipText = diagnostic.Message;
            button.Pressed += () => FocusDiagnostic(diagnostic);
            _diagnosticRows.AddChild(button);
        }
    }

    private void FocusDiagnostic(StudioUx6FDiagnostic diagnostic)
    {
        _status.Text = $"{HumanDiagnostic(diagnostic.Code)} · {diagnostic.Message}";
        if (diagnostic.Path.Contains("apiVersion", StringComparison.Ordinal)) _apiVersion.GrabFocus();
        else if (diagnostic.Path.Contains("memory", StringComparison.Ordinal)) _contextMemory.GrabFocus();
        else if (diagnostic.Path.Contains("determinism", StringComparison.Ordinal)) _seed.GrabFocus();
        else _handlerInstructions.GrabFocus();
    }

    private StudioUx6JSourceSnapshot? CaptureSource()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        var session = _sourceSession.Session;
        if (project is null || history is null || session is null) return null;
        var policyPath = Path.Combine(session.RootDirectory, "forge-script-policy.json");
        return new(project, history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.CaptureScriptApprovalUx6F(),
            _sourceSession.CaptureExecutionPolicyApprovalUx6J(),
            File.Exists(policyPath), File.Exists(policyPath) ? File.ReadAllBytes(policyPath) : null);
    }

    private bool SourceMatches()
    {
        var snapshot = _sourceSnapshot;
        var history = _sourceSession.History;
        var session = _sourceSession.Session;
        if (snapshot is null || history is null || session is null) return false;
        var policyPath = Path.Combine(session.RootDirectory, "forge-script-policy.json");
        var existed = File.Exists(policyPath);
        var bytes = existed ? File.ReadAllBytes(policyPath) : null;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) && _sourceSession.IsDirty == snapshot.IsDirty &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.CaptureScriptApprovalUx6F() == snapshot.ScriptApproval &&
               _sourceSession.CaptureExecutionPolicyApprovalUx6J() == snapshot.PolicyApproval &&
               existed == snapshot.PolicyFileExisted &&
               ((bytes is null && snapshot.PolicyFileBytes is null) ||
                (bytes is not null && snapshot.PolicyFileBytes is not null &&
                 bytes.SequenceEqual(snapshot.PolicyFileBytes)));
    }

    private bool FailEvidence(string message)
    {
        SetStatus(message, error: true);
        ReleaseEvidence();
        _session = _sourceSession;
        Visible = false;
        return false;
    }

    private bool ReleaseEvidence()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        var released = StudioSessionController.DeleteUx6FTemporaryWorkspace(_evidenceRoot);
        _evidenceRoot = null;
        _evidenceReleased = released;
        return released;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Success;
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateBounds()
    {
        var viewport = GetViewportRect().Size;
        var width = Mathf.Clamp(viewport.X - 32, 1280, 1880);
        var height = Mathf.Clamp(viewport.Y - 28, 760, 1050);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2(Mathf.Max(16, (viewport.X - width) * .5f),
            Mathf.Max(14, (viewport.Y - height) * .5f));
    }

    private static VBoxContainer PanelColumn(HBoxContainer host, int minimum, float stretchRatio)
    {
        var panel = StudioStyle.Panel("Ux6JColumn", StudioStyle.SurfaceRaised, 8, 1);
        panel.CustomMinimumSize = new Vector2(minimum, 0);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsStretchRatio = stretchRatio;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        host.AddChild(panel);
        var margin = StudioStyle.Margin(10, 9, 10, 9);
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
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        tabs.AddChild(scroll);
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(page);
        return page;
    }

    private static VBoxContainer Field(string label, Control control, int minimum)
    {
        var field = new VBoxContainer { CustomMinimumSize = new Vector2(minimum, 0) };
        field.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted));
        field.AddChild(control);
        return field;
    }

    private static HBoxContainer Pair(string leftLabel, Control left, string rightLabel, Control right)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(Field(leftLabel, left, 250));
        row.AddChild(Field(rightLabel, right, 250));
        return row;
    }

    private static LineEdit TextNumber() => StudioUx2EnvBControls.Text("0");

    private static Button ActionButton(string text, Action action, int width)
    {
        var button = StudioStyle.Button(text, compact: true);
        button.CustomMinimumSize = new Vector2(width, 36);
        button.Pressed += action;
        return button;
    }

    private static void PopulateChoices(OptionButton option, string placeholder,
        IReadOnlyList<StudioUx6JChoice> choices)
    {
        option.Clear();
        option.AddItem(placeholder, 0);
        foreach (var choice in choices) option.AddItem($"{choice.Label} · {choice.Detail}");
        option.Select(0);
    }

    private static StudioUx6JChoice? ChoiceAt(IReadOnlyList<StudioUx6JChoice> choices,
        OptionButton option)
    {
        var index = option.Selected - 1;
        return index >= 0 && index < choices.Count ? choices[index] : null;
    }

    private static bool TryInt(LineEdit edit, out int value) =>
        int.TryParse(edit.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryLong(LineEdit edit, out long value) =>
        long.TryParse(edit.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryUlong(LineEdit edit, out ulong value) =>
        ulong.TryParse(edit.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool PolicyContentsEqual(StudioUx6JPolicyDraft left, StudioUx6JPolicyDraft right) =>
        left with { BaseRevision = 0, SourceFingerprint = string.Empty } ==
        right with { BaseRevision = 0, SourceFingerprint = string.Empty };

    private static bool PolicyContentsEqual(ForgeScriptExecutionPolicy left, StudioUx6JPolicyDraft right) =>
        left.Id == right.Id && left.SchemaVersion == right.SchemaVersion &&
        left.Provenance == right.Provenance && left.ApiVersion == right.ApiVersion &&
        left.HandlerInstructionLimit == right.HandlerInstructionLimit &&
        left.TickInstructionLimit == right.TickInstructionLimit &&
        left.ModuleInstructionLimit == right.ModuleInstructionLimit &&
        left.TimeoutMilliseconds == right.TimeoutMilliseconds && left.RecursionLimit == right.RecursionLimit &&
        left.SpawnLimit == right.SpawnLimit && left.ContextMemoryLimitBytes == right.ContextMemoryLimitBytes &&
        left.ModuleMemoryLimitBytes == right.ModuleMemoryLimitBytes &&
        left.DeterministicSeed == right.DeterministicSeed && left.SimulationTick == right.SimulationTick &&
        left.SimulationTimeMilliseconds == right.SimulationTimeMilliseconds &&
        left.Authority == right.Authority && left.RequiredAtRuntime == right.RequiredAtRuntime;

    private static string YesNo(bool value) => value ? "prouvé" : "non prouvé";

    private static string HumanAuthority(ForgeScriptExecutionAuthority authority) => authority switch
    {
        ForgeScriptExecutionAuthority.Local => "Local contrôlé",
        ForgeScriptExecutionAuthority.Owner => "Propriétaire autoritaire",
        ForgeScriptExecutionAuthority.Server => "Serveur autoritaire",
        _ => "Inconnue",
    };

    private static string HumanDiagnostic(string code) => code switch
    {
        "script.policy.instruction_budget_invalid" => "Budgets d’instructions incohérents",
        "script.policy.execution_budget_invalid" => "Limites d’exécution invalides",
        "script.policy.memory_budget_invalid" => "Budgets mémoire incohérents",
        "script.policy.determinism_invalid" => "Déterminisme incomplet",
        "script.policy.api_unsupported" => "Version d’API non prise en charge",
        "script.policy.revision_conflict" => "Révision périmée",
        "script.policy.source_changed" => "Politique source modifiée",
        "script.handle.subject_missing" => "Sujet de handle manquant",
        _ => code.Replace('.', ' '),
    };

    private static string HumanPath(string path) => path switch
    {
        "script/policy/instructions" => "Instructions",
        "script/policy/execution" => "Exécution",
        "script/policy/memory" => "Mémoire",
        "script/policy/determinism" => "Déterminisme",
        "script/policy/apiVersion" => "Version d’API",
        "script/handles/subjects" => "Sujets humains",
        _ => "Politique R014",
    };

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private static IEnumerable<string> DescendantText(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label) yield return label.Text;
            else if (child is Button button) yield return button.Text;
            else if (child is LineEdit edit) yield return edit.Text;
            else if (child is CodeEdit code) yield return code.Text;
            foreach (var text in DescendantText(child)) yield return text;
        }
    }

    private static bool ContainsUuid(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);

    private static string ProtectTechnicalIdentifiers(string text) => Regex.Replace(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        "référence interne protégée", RegexOptions.CultureInvariant);

    private enum SubjectKind { Area, Entity, Asset }
}
