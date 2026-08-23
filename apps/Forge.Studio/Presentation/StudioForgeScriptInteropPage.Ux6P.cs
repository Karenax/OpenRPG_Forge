using System.Globalization;
using System.Text.RegularExpressions;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioForgeScriptInteropPageUx6P : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx6PSourceSnapshot? _sourceSnapshot;
    private string? _evidenceRoot;
    private byte[]? _evidenceSourceBytes;
    private byte[]? _evidenceMetadataBytes;
    private bool _evidenceReleased = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _sourceChoice;
    private readonly Label _selection;
    private readonly CodeEdit _source;
    private readonly TabContainer _tabs;
    private readonly LineEdit _maximumSources;
    private readonly LineEdit _maximumSourceBytes;
    private readonly LineEdit _testInstructions;
    private readonly LineEdit _testTimeout;
    private readonly LineEdit _testRecursion;
    private readonly LineEdit _testSpawn;
    private readonly CheckBox _requiredAtRuntime;
    private readonly LineEdit _target;
    private readonly LineEdit _component;
    private readonly LineEdit _eventName;
    private readonly LineEdit _handler;
    private readonly LineEdit _payload;
    private readonly LineEdit _priority;
    private readonly LineEdit _condition;
    private readonly OptionButton _authority;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _previewReview;
    private readonly Label _simulationReview;
    private readonly CodeEdit _results;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _simulate;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx6FSourceChoice> _sourceChoices = [];
    private StudioUx6FDraft? _sourceDraft;
    private StudioUx6PInteropDraft? _interopDraft;
    private StudioUx6PPreview? _previewResult;
    private StudioUx6PSimulation? _simulationResult;
    private bool _explicitSource;
    private bool _updating;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSource && _sourceDraft is not null && _interopDraft is not null;
    internal bool PreviewReady => _previewResult is
    {
        Succeeded: true,
        CanApply: true,
        CanonicalDryRun: true,
        SourcePlan: true,
        BindingPlan: true,
    };
    internal bool SimulationReady => _simulationResult is
    {
        Succeeded: true,
        Api: true,
        SourceTransactions: true,
        Compilation: true,
        SandboxTests: true,
        BindingTransactions: true,
        SnapshotRoundTrip: true,
        RuntimeAdapter: true,
        NativeSandbox: true,
        QuotaRecovery: true,
    };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => SourceMatches() && EvidenceScriptMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool HumanControlsReady => _authority.ItemCount == 3 &&
                                         _authority.GetItemId(0) == (int)ForgeScriptEventAuthority.Local &&
                                         _authority.GetItemId(1) == (int)ForgeScriptEventAuthority.Owner &&
                                         _authority.GetItemId(2) == (int)ForgeScriptEventAuthority.Server;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return text.Contains("Interop éditeur/MCP", StringComparison.Ordinal) &&
                   text.Contains("Profil R101", StringComparison.Ordinal) &&
                   text.Contains("Source et outils MCP", StringComparison.Ordinal) &&
                   text.Contains("Liaison d’événement typée", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec canonique", StringComparison.Ordinal) &&
                   text.Contains("Snapshot exact", StringComparison.Ordinal) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioForgeScriptInteropPageUx6P(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.008f, .014f, .023f, .985f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6PInterop", StudioStyle.Surface, 12, 1);
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
        headingText.AddChild(StudioStyle.Label("ForgeScript · Interop éditeur/MCP R101", 19));
        headingText.AddChild(StudioStyle.Label(
            "Profil R101 · Source et outils MCP · Liaison d’événement typée · Snapshot exact · Aperçu sec canonique · source UX6-F inchangée.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        var sourceRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sourceChoice = StudioUx2EnvBControls.Options();
        _sourceChoice.CustomMinimumSize = new Vector2(650, 34);
        _sourceChoice.ItemSelected += SelectSource;
        sourceRow.AddChild(Field("Source ForgeScript · sélection explicite", _sourceChoice, 650));
        sourceRow.AddChild(ActionButton("Recharger source et profil", Reload, 210));
        root.AddChild(sourceRow);
        _selection = StudioStyle.Label(
            "Choisissez une source .as canonique. L’interop ne crée ni second éditeur ni fichier source parallèle.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var body = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 500),
            CustomMaximumSize = new Vector2(-1, 720),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            PropagateMaximumSize = true,
        };
        body.AddThemeConstantOverride("separation", 9);
        root.AddChild(body);

        var left = PanelColumn(body, 610, 1f);
        left.AddChild(StudioStyle.Label("SOURCE REVUE · AUTORITÉ UX6-F", 10, StudioStyle.Accent));
        _source = new CodeEdit
        {
            Editable = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 230),
            DrawTabs = true,
            GuttersDrawLineNumbers = true,
        };
        left.AddChild(_source);
        left.AddChild(StudioStyle.Label("DIAGNOSTICS LOCALISÉS", 10, StudioStyle.Accent));
        var diagnosticScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 150),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        diagnosticScroll.AddChild(_diagnostics);
        left.AddChild(diagnosticScroll);

        var right = PanelColumn(body, 820, 1.35f);
        right.AddChild(StudioStyle.Label("FAÇADE COMMUNE · ÉDITEUR · MCP · RUNTIME OPTIONNEL", 10,
            StudioStyle.Accent));
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 350),
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

        var profilePage = TabPage(_tabs, "Profil R101");
        profilePage.AddChild(StudioStyle.Label("Profil R101 · identité, API et quotas", 12, StudioStyle.Accent));
        _maximumSources = StudioUx2EnvBControls.Text("512");
        _maximumSourceBytes = StudioUx2EnvBControls.Text("524288");
        _testInstructions = StudioUx2EnvBControls.Text("10000");
        _testTimeout = StudioUx2EnvBControls.Text("1000");
        _testRecursion = StudioUx2EnvBControls.Text("32");
        _testSpawn = StudioUx2EnvBControls.Text("8");
        profilePage.AddChild(Pair("Nombre maximal de sources", _maximumSources,
            "Quota par source (octets)", _maximumSourceBytes));
        profilePage.AddChild(Pair("Instructions de test", _testInstructions,
            "Timeout de test (ms)", _testTimeout));
        profilePage.AddChild(Pair("Profondeur de récursion", _testRecursion,
            "Limite de spawn", _testSpawn));
        _requiredAtRuntime = new CheckBox { Text = "Extension interop obligatoire au runtime" };
        profilePage.AddChild(_requiredAtRuntime);
        profilePage.AddChild(StudioStyle.Label(
            "Les valeurs exactes sont validées sans clamp. Le profil persiste séparément de la source et reste undo/redo.",
            9, StudioStyle.Muted, wrap: true));

        var bindingPage = TabPage(_tabs, "Liaison");
        bindingPage.AddChild(StudioStyle.Label("Liaison d’événement typée", 12, StudioStyle.Accent));
        _target = StudioUx2EnvBControls.Text("Gardien de la crypte");
        _component = StudioUx2EnvBControls.Text("creature.combat");
        _eventName = StudioUx2EnvBControls.Text("creature.damaged");
        _handler = StudioUx2EnvBControls.Text("OnDamagedHigh");
        _payload = StudioUx2EnvBControls.Text("int");
        _priority = StudioUx2EnvBControls.Text("90");
        _condition = StudioUx2EnvBControls.Text("is_enraged");
        _authority = StudioUx2EnvBControls.Options();
        _authority.AddItem("Locale", (int)ForgeScriptEventAuthority.Local);
        _authority.AddItem("Propriétaire", (int)ForgeScriptEventAuthority.Owner);
        _authority.AddItem("Serveur", (int)ForgeScriptEventAuthority.Server);
        bindingPage.AddChild(Pair("Cible humaine", _target, "Composant", _component));
        bindingPage.AddChild(Pair("Événement", _eventName, "Handler existant", _handler));
        bindingPage.AddChild(Pair("Payload typé", _payload, "Priorité", _priority));
        bindingPage.AddChild(Pair("Condition optionnelle", _condition, "Autorité", _authority));
        bindingPage.AddChild(StudioStyle.Label(
            "La liaison est planifiée puis appliquée dans un service MCP isolé. Aucun acteur ou bus live n’est modifié.",
            9, StudioStyle.Muted, wrap: true));

        var toolsPage = TabPage(_tabs, "Transactions");
        toolsPage.AddChild(StudioStyle.Label("Source et outils MCP", 12, StudioStyle.Accent));
        toolsPage.AddChild(StudioStyle.Label(
            "get_api · create/update · compile · diagnostics · run_tests · assign_event · undo/redo · export/restore snapshot",
            10, StudioStyle.Text, wrap: true));
        toolsPage.AddChild(StudioStyle.Label(
            "La simulation exerce plans source et binding, conflits de révision, snapshot exact, quotas, récupération et adaptateur runtime sur une copie temporaire.",
            9, StudioStyle.Muted, wrap: true));
        toolsPage.AddChild(StudioStyle.Label(
            "Limites : aucun accès fichier arbitraire, réseau, processus, objet moteur ou .NET depuis MCP.",
            9, StudioStyle.Warning, wrap: true));

        var review = StudioStyle.Panel("Ux6PReview", StudioStyle.SurfaceRaised, 8, 1);
        review.CustomMinimumSize = new Vector2(0, 145);
        right.AddChild(review);
        var reviewMargin = StudioStyle.Margin(9, 8, 9, 8);
        review.AddChild(reviewMargin);
        var reviewRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reviewMargin.AddChild(reviewRows);
        _previewReview = StudioStyle.Label("Aucun aperçu sec canonique.", 9, StudioStyle.Muted, wrap: true);
        _simulationReview = StudioStyle.Label("Aucune transaction interop exercée.", 9,
            StudioStyle.Muted, wrap: true);
        _results = new CodeEdit
        {
            Editable = false,
            CustomMinimumSize = new Vector2(0, 70),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ScrollFitContentHeight = true,
        };
        _results.Text = "Console R101 prête.";
        reviewRows.AddChild(_previewReview);
        reviewRows.AddChild(_simulationReview);
        reviewRows.AddChild(_results);

        _status = StudioStyle.Label("Interop ForgeScript prête.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 8);
        _preview = ActionButton("Prévisualiser", Preview, 128);
        _simulate = ActionButton("Exercer l’interop", Simulate, 165);
        _apply = ActionButton("Appliquer", Apply, 108);
        _undo = ActionButton("Annuler", Undo, 104);
        _redo = ActionButton("Rétablir", Redo, 104);
        _save = ActionButton("Enregistrer", Save, 120);
        footer.AddChild(_preview);
        footer.AddChild(_simulate);
        footer.AddChild(_apply);
        footer.AddChild(_undo);
        footer.AddChild(_redo);
        footer.AddChild(_save);
        root.AddChild(footer);

        HookEditors();
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
        if (Visible) _session.CancelUx6PApproval();
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
        if (!mode.StartsWith("ux6-p", StringComparison.OrdinalIgnoreCase))
            return FailEvidence("Mode de preuve UX6-P inconnu.");
        ReleaseEvidence();
        _sourceSnapshot = CaptureSource();
        if (_sourceSnapshot is null || _sourceSession.Project is null)
            return FailEvidence("Le module source n’est pas ouvert.");
        _evidenceRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-p-ui",
            Guid.NewGuid().ToString("N"));
        if (!ProjectSession.Create(_evidenceRoot, _sourceSession.Project).Succeeded)
            return FailEvidence("Impossible de créer la copie temporaire R101.");
        var document = StudioSessionController.EvidenceDocumentUx6F();
        new ForgeScriptSourceStore(_evidenceRoot).Save(document);
        var sourcePath = Path.Combine(_evidenceRoot,
            document.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        _evidenceSourceBytes = File.ReadAllBytes(sourcePath);
        _evidenceMetadataBytes = File.ReadAllBytes(sourcePath + ".meta.json");
        _evidenceSession = new StudioSessionController();
        if (!_evidenceSession.Open(_evidenceRoot).Succeeded)
            return FailEvidence("Impossible d’ouvrir la copie temporaire R101.");
        _session = _evidenceSession;
        OpenBound(_session, evidence: true, cancelApproval: false);
        if (_sourceChoice.ItemCount < 2) return FailEvidence("La copie temporaire ne fournit aucune source canonique.");
        _sourceChoice.Select(1);
        SelectSource(1);
        if (_interopDraft is null) return FailEvidence("Le profil R101 ne peut pas être construit.");
        _maximumSources.Text = (_interopDraft.MaximumSources + 1).ToString(CultureInfo.InvariantCulture);
        _maximumSourceBytes.Text = (_interopDraft.MaximumSourceBytes + 1024).ToString(CultureInfo.InvariantCulture);
        _priority.Text = "95";
        _requiredAtRuntime.ButtonPressed = true;
        Invalidate();
        Preview();
        Simulate();
        _tabs.CurrentTab = mode.Contains("binding", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("transactions", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        var isolated = UsesIsolatedEvidence;
        var explicitSelection = HasExplicitSelection;
        var controls = HumanControlsReady;
        var preview = PreviewReady;
        var simulation = SimulationReady;
        var human = ShowsHumanSurface;
        var sourceUntouched = EvidenceSourceUntouched;
        var ready = isolated && explicitSelection && controls && preview && simulation && human && sourceUntouched;
        if (!ready)
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_P_UI_FAILED " +
                        $"isolated={isolated.ToString().ToLowerInvariant()} " +
                        $"explicit={explicitSelection.ToString().ToLowerInvariant()} " +
                        $"controls={controls.ToString().ToLowerInvariant()} " +
                        $"preview={preview.ToString().ToLowerInvariant()} " +
                        $"simulation={simulation.ToString().ToLowerInvariant()} " +
                        $"human={human.ToString().ToLowerInvariant()} " +
                        $"source={sourceUntouched.ToString().ToLowerInvariant()}");
        if (!ready) FailEvidence("La surface R101 ne satisfait pas toutes les preuves UI.");
        return ready;
    }

    internal bool RunUiTransactionEvidence()
    {
        if (!PrepareEvidence("ux6-p-script-interop")) return false;
        try
        {
            var draft = ReadDraft();
            var source = _sourceDraft;
            if (draft is null || source is null || !PreviewReady || !SimulationReady ||
                _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession))
                return false;
            var apply = _session.ApplyApprovedScriptInteropUx6P(draft, source);
            var undo = _session.UndoScriptInteropUx6P();
            var redo = _session.RedoScriptInteropUx6P();
            var save = _session.SaveScriptInteropUx6P();
            var current = _session.CurrentScriptInteropUx6P(source);
            var persisted = current is not null && DraftContentsEqual(current, draft);
            return apply.Succeeded && undo.Succeeded && redo.Succeeded && save.Succeeded && persisted &&
                   EvidenceScriptMatches();
        }
        finally
        {
            CloseEvidence();
        }
    }

    private void OpenBound(StudioSessionController session, bool evidence, bool cancelApproval)
    {
        _session = session;
        if (cancelApproval) _session.CancelUx6PApproval();
        ResetControls();
        Visible = true;
        MoveToFront();
        UpdateBounds();
        _sourceChoices = _session.ScriptSourceChoicesUx6F();
        _sourceChoice.AddItem("Choisir une source ForgeScript…");
        foreach (var choice in _sourceChoices) _sourceChoice.AddItem(choice.Label);
        _selection.Text = evidence
            ? "Preuve isolée · source canonique copiée · module source strictement inchangé."
            : "Choisissez explicitement une source .as canonique du module.";
        SetStatus(evidence ? "Copie R101 prête pour preuve." : "Interop R101 prête.");
        UpdateButtons();
    }

    private void SelectSource(long index)
    {
        if (_updating || index <= 0 || index > _sourceChoices.Count)
        {
            _explicitSource = false;
            _sourceDraft = null;
            _interopDraft = null;
            UpdateButtons();
            return;
        }
        var choice = _sourceChoices[(int)index - 1];
        var failure = string.Empty;
        if (choice.Kind != StudioUx6FSourceKind.Canonical ||
            !_session.TryOpenCanonicalSourceUx6F(choice.RelativePath, out var source, out failure) ||
            source is null)
        {
            SetStatus(string.IsNullOrWhiteSpace(failure) ? "Cette source n’est pas éditable." : failure, error: true);
            _explicitSource = false;
            return;
        }
        _sourceDraft = source;
        _interopDraft = _session.CurrentScriptInteropUx6P(source);
        _explicitSource = _interopDraft is not null;
        if (_interopDraft is null)
        {
            SetStatus("Le profil R101 ne peut pas être chargé.", error: true);
            return;
        }
        Populate(_interopDraft, source);
        SetStatus($"Source sélectionnée · {choice.Label}.");
    }

    private void Reload()
    {
        if (_sourceDraft is null) return;
        var path = _sourceDraft.RelativePath;
        if (!_session.TryOpenCanonicalSourceUx6F(path, out var source, out var failure) || source is null)
        {
            SetStatus(failure, error: true);
            return;
        }
        _sourceDraft = source;
        _interopDraft = _session.CurrentScriptInteropUx6P(source);
        if (_interopDraft is not null) Populate(_interopDraft, source);
    }

    private void Populate(StudioUx6PInteropDraft draft, StudioUx6FDraft source)
    {
        _updating = true;
        _source.Text = source.Source;
        _maximumSources.Text = draft.MaximumSources.ToString(CultureInfo.InvariantCulture);
        _maximumSourceBytes.Text = draft.MaximumSourceBytes.ToString(CultureInfo.InvariantCulture);
        _testInstructions.Text = draft.TestInstructionLimit.ToString(CultureInfo.InvariantCulture);
        _testTimeout.Text = draft.TestTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture);
        _testRecursion.Text = draft.TestRecursionLimit.ToString(CultureInfo.InvariantCulture);
        _testSpawn.Text = draft.TestSpawnLimit.ToString(CultureInfo.InvariantCulture);
        _requiredAtRuntime.ButtonPressed = draft.RequiredAtRuntime;
        _target.Text = draft.TargetLabel;
        _component.Text = draft.ComponentId;
        _eventName.Text = draft.EventName;
        _handler.Text = draft.Handler;
        _payload.Text = draft.PayloadType;
        _priority.Text = draft.Priority.ToString(CultureInfo.InvariantCulture);
        _condition.Text = draft.EnableCondition ?? string.Empty;
        SelectAuthority(draft.Authority);
        _previewResult = null;
        _simulationResult = null;
        _previewReview.Text = "Aucun aperçu sec canonique.";
        _simulationReview.Text = "Aucune transaction interop exercée.";
        _results.Text = "Console R101 prête.";
        _updating = false;
        RenderDiagnostics([]);
        UpdateButtons();
    }

    private StudioUx6PInteropDraft? ReadDraft()
    {
        if (_interopDraft is null) return null;
        static int Parse(LineEdit edit) => int.TryParse(edit.Text.Trim(), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var value) ? value : 0;
        return _interopDraft with
        {
            RequiredAtRuntime = _requiredAtRuntime.ButtonPressed,
            MaximumSources = Parse(_maximumSources),
            MaximumSourceBytes = Parse(_maximumSourceBytes),
            TestInstructionLimit = Parse(_testInstructions),
            TestTimeoutMilliseconds = Parse(_testTimeout),
            TestRecursionLimit = Parse(_testRecursion),
            TestSpawnLimit = Parse(_testSpawn),
            TargetLabel = _target.Text.Trim(),
            ComponentId = _component.Text.Trim(),
            EventName = _eventName.Text.Trim(),
            Handler = _handler.Text.Trim(),
            PayloadType = _payload.Text.Trim(),
            Priority = Parse(_priority),
            EnableCondition = string.IsNullOrWhiteSpace(_condition.Text) ? null : _condition.Text.Trim(),
            Authority = (ForgeScriptEventAuthority)_authority.GetSelectedId(),
        };
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null || _sourceDraft is null) return;
        _previewResult = _session.PreviewScriptInteropUx6P(draft, _sourceDraft);
        _previewReview.Text = _previewResult.Succeeded
            ? $"Aperçu · {HumanSemanticDiff(_previewResult.SemanticDiff)}"
            : _previewResult.Message;
        RenderDiagnostics(_previewResult.Diagnostics);
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
        UpdateButtons();
    }

    private void Simulate()
    {
        var draft = ReadDraft();
        if (draft is null || _sourceDraft is null) return;
        _simulationResult = _session.SimulateScriptInteropUx6P(draft, _sourceDraft);
        _simulationReview.Text = _simulationResult.Message;
        _results.Text = string.Join(System.Environment.NewLine, _simulationResult.Results);
        RenderDiagnostics((_previewResult?.Diagnostics ?? []).Concat(_simulationResult.Diagnostics).ToArray());
        SetStatus(_simulationResult.Message, !_simulationResult.Succeeded);
        UpdateButtons();
    }

    private void Apply()
    {
        var draft = ReadDraft();
        if (draft is null || _sourceDraft is null) return;
        var result = _session.ApplyApprovedScriptInteropUx6P(draft, _sourceDraft);
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _interopDraft = _session.CurrentScriptInteropUx6P(_sourceDraft);
            if (_interopDraft is not null) Populate(_interopDraft, _sourceDraft);
        }
        UpdateButtons();
    }

    private void Undo() => Mutate(_session.UndoScriptInteropUx6P());
    private void Redo() => Mutate(_session.RedoScriptInteropUx6P());
    private void Save() => Mutate(_session.SaveScriptInteropUx6P());

    private void Mutate(StudioUx6PActionResult result)
    {
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded && _sourceDraft is not null)
        {
            _interopDraft = _session.CurrentScriptInteropUx6P(_sourceDraft);
            if (_interopDraft is not null) Populate(_interopDraft, _sourceDraft);
        }
        UpdateButtons();
    }

    private void HookEditors()
    {
        foreach (var edit in new[]
                 {
                     _maximumSources, _maximumSourceBytes, _testInstructions, _testTimeout, _testRecursion,
                     _testSpawn, _target, _component, _eventName, _handler, _payload, _priority, _condition,
                 })
            edit.TextChanged += _ => Invalidate();
        _requiredAtRuntime.Toggled += _ => Invalidate();
        _authority.ItemSelected += _ => Invalidate();
    }

    private void Invalidate()
    {
        if (_updating) return;
        _session.CancelUx6PApproval();
        _previewResult = null;
        _simulationResult = null;
        _previewReview.Text = "Aucun aperçu sec canonique.";
        _simulationReview.Text = "Aucune transaction interop exercée.";
        _results.Text = "Console R101 prête.";
        RenderDiagnostics([]);
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var selected = HasExplicitSelection;
        _preview.Disabled = !selected;
        _simulate.Disabled = !selected;
        _apply.Disabled = !PreviewReady || !SimulationReady;
        _undo.Disabled = !selected;
        _redo.Disabled = !selected;
        _save.Disabled = !selected || !_session.ScriptInteropHasUnsavedChangesUx6P;
    }

    private void ResetControls()
    {
        _updating = true;
        _sourceChoice.Clear();
        _source.Text = string.Empty;
        _results.Text = "Console R101 prête.";
        _sourceChoices = [];
        _sourceDraft = null;
        _interopDraft = null;
        _previewResult = null;
        _simulationResult = null;
        _explicitSource = false;
        _updating = false;
        RenderDiagnostics([]);
        UpdateButtons();
    }

    private void RenderDiagnostics(IReadOnlyList<StudioUx6FDiagnostic> diagnostics)
    {
        ClearChildren(_diagnostics);
        if (diagnostics.Count == 0)
        {
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 9, StudioStyle.Success));
            return;
        }
        foreach (var diagnostic in diagnostics.Take(16))
        {
            var row = StudioStyle.Panel("Ux6PDiagnostic", StudioStyle.SurfaceRaised, 6, 1);
            var margin = StudioStyle.Margin(7, 5, 7, 5);
            row.AddChild(margin);
            margin.AddChild(StudioStyle.Label(
                $"{HumanDiagnostic(diagnostic.Code)} · {HumanPath(diagnostic.Path)} · " +
                ProtectTechnicalIdentifiers(HumanDiagnosticMessage(diagnostic)),
                8, diagnostic.Severity == "error" ? StudioStyle.Danger : StudioStyle.Warning, wrap: true));
            _diagnostics.AddChild(row);
        }
    }

    private StudioUx6PSourceSnapshot? CaptureSource()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        var session = _sourceSession.Session;
        if (project is null || history is null || session is null) return null;
        var interopPath = Path.Combine(session.RootDirectory, ForgeScriptInteropProfileStore.RelativePath);
        var existed = File.Exists(interopPath);
        return new(project, history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.CaptureScriptApprovalUx6F(),
            _sourceSession.CaptureExecutionPolicyApprovalUx6J(),
            _sourceSession.CaptureModulePermissionApprovalUx6K(),
            _sourceSession.CaptureScriptAutomationApprovalUx6M(),
            _sourceSession.CaptureScriptQualityApprovalUx6O(),
            _sourceSession.CaptureScriptInteropApprovalUx6P(), existed,
            existed ? File.ReadAllBytes(interopPath) : null);
    }

    private bool SourceMatches()
    {
        var snapshot = _sourceSnapshot;
        var history = _sourceSession.History;
        var session = _sourceSession.Session;
        if (snapshot is null || history is null || session is null) return false;
        var interopPath = Path.Combine(session.RootDirectory, ForgeScriptInteropProfileStore.RelativePath);
        var existed = File.Exists(interopPath);
        var bytes = existed ? File.ReadAllBytes(interopPath) : null;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) && _sourceSession.IsDirty == snapshot.IsDirty &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.CaptureScriptApprovalUx6F() == snapshot.ScriptApproval &&
               _sourceSession.CaptureExecutionPolicyApprovalUx6J() == snapshot.ExecutionPolicyApproval &&
               _sourceSession.CaptureModulePermissionApprovalUx6K() == snapshot.PermissionApproval &&
               _sourceSession.CaptureScriptAutomationApprovalUx6M() == snapshot.AutomationApproval &&
               _sourceSession.CaptureScriptQualityApprovalUx6O() == snapshot.QualityApproval &&
               _sourceSession.CaptureScriptInteropApprovalUx6P() == snapshot.InteropApproval &&
               existed == snapshot.InteropFileExisted && BytesEqual(bytes, snapshot.InteropFileBytes);
    }

    private bool EvidenceScriptMatches()
    {
        if (_evidenceRoot is null || _sourceDraft is null || _evidenceSourceBytes is null ||
            _evidenceMetadataBytes is null) return false;
        var path = Path.Combine(_evidenceRoot,
            _sourceDraft.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) && File.Exists(path + ".meta.json") &&
               File.ReadAllBytes(path).SequenceEqual(_evidenceSourceBytes) &&
               File.ReadAllBytes(path + ".meta.json").SequenceEqual(_evidenceMetadataBytes);
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
        _evidenceSourceBytes = null;
        _evidenceMetadataBytes = null;
        _evidenceReleased = released;
        return released;
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = ProtectTechnicalIdentifiers(message);
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Success;
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateBounds()
    {
        var viewport = GetViewportRect().Size;
        var width = Mathf.Clamp(viewport.X - 32, 1500, 1880);
        var height = Mathf.Clamp(viewport.Y - 28, 840, 1050);
        _dialog.Size = new Vector2(width, height);
        _dialog.Position = new Vector2(Mathf.Max(16, (viewport.X - width) * .5f),
            Mathf.Max(14, (viewport.Y - height) * .5f));
    }

    private void SelectAuthority(ForgeScriptEventAuthority authority)
    {
        for (var index = 0; index < _authority.ItemCount; index++)
        {
            if (_authority.GetItemId(index) != (int)authority) continue;
            _authority.Select(index);
            return;
        }
        _authority.Select(0);
    }

    private static VBoxContainer PanelColumn(HBoxContainer host, int minimum, float stretchRatio)
    {
        var panel = StudioStyle.Panel("Ux6PColumn", StudioStyle.SurfaceRaised, 8, 1);
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
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        field.AddChild(control);
        return field;
    }

    private static HBoxContainer Pair(string leftLabel, Control left, string rightLabel, Control right)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(Field(leftLabel, left, 310));
        row.AddChild(Field(rightLabel, right, 310));
        return row;
    }

    private static Button ActionButton(string text, Action action, int width)
    {
        var button = StudioStyle.Button(text, compact: true);
        button.CustomMinimumSize = new Vector2(width, 36);
        button.Pressed += action;
        return button;
    }

    private static bool DraftContentsEqual(StudioUx6PInteropDraft left, StudioUx6PInteropDraft right) =>
        left.Id == right.Id && left.SchemaVersion == right.SchemaVersion && left.Provenance == right.Provenance &&
        left.ApiVersion == right.ApiVersion && left.EventBinding == right.EventBinding &&
        left.RequiredAtRuntime == right.RequiredAtRuntime && left.MaximumSources == right.MaximumSources &&
        left.MaximumSourceBytes == right.MaximumSourceBytes &&
        left.TestInstructionLimit == right.TestInstructionLimit &&
        left.TestTimeoutMilliseconds == right.TestTimeoutMilliseconds &&
        left.TestRecursionLimit == right.TestRecursionLimit && left.TestSpawnLimit == right.TestSpawnLimit &&
        left.ScriptSourceId == right.ScriptSourceId && left.ScriptRelativePath == right.ScriptRelativePath;

    private static bool BytesEqual(byte[]? left, byte[]? right) =>
        left is null ? right is null : right is not null && left.SequenceEqual(right);

    private static string HumanDiagnostic(string code) => code switch
    {
        "script.interop.uuid_required" => "Identité interop absente",
        "script.interop.schema_unsupported" => "Version de profil incompatible",
        "script.interop.api_unsupported" => "API ForgeScript incompatible",
        "script.interop.event_binding_required" => "Canal d’événement absent",
        "script.interop.source_quota_invalid" => "Quota source invalide",
        "script.interop.test_quota_invalid" => "Quota de test invalide",
        "script.interop.revision_conflict" => "Révision interop périmée",
        "script.interop.source_changed" => "Profil interop modifié",
        "script.interop.script_changed" => "Source ForgeScript modifiée",
        "script.interop.binding_invalid" => "Liaison incomplète",
        "script.binding.entry_missing" => "Handler absent de la source",
        "script.transaction_invalid" => "Transaction source invalide",
        "script.source_quota_exceeded" => "Source trop volumineuse",
        _ => code.Replace('.', ' '),
    };

    private static string HumanDiagnosticMessage(StudioUx6FDiagnostic diagnostic) => diagnostic.Code switch
    {
        "script.interop.source_quota_invalid" =>
            "Le nombre de sources doit rester entre 1 et 10 000 et le quota par source entre 1 Kio et 8 Mio.",
        "script.interop.test_quota_invalid" =>
            "Les limites de test doivent rester dans les bornes R101 documentées.",
        "script.interop.revision_conflict" => "Rechargez le profil avant de poursuivre.",
        "script.interop.source_changed" => "Le profil R101 a changé ; rechargez-le.",
        "script.interop.script_changed" => "La source UX6-F a changé ; rechargez-la.",
        "script.interop.binding_invalid" =>
            "Renseignez cible, composant, événement, handler, payload, priorité et autorité.",
        "script.binding.entry_missing" => "Choisissez un handler réellement déclaré dans la source sélectionnée.",
        "script.transaction_invalid" => "La source doit conserver son chemin .as sûr et sa révision courante.",
        _ => diagnostic.Message,
    };

    private static string HumanPath(string path) => path switch
    {
        var value when value.Contains("sourceQuota", StringComparison.Ordinal) => "Quotas source",
        var value when value.Contains("testQuota", StringComparison.Ordinal) => "Quotas sandbox",
        var value when value.Contains("eventBinding", StringComparison.Ordinal) => "Canal d’événement",
        var value when value.Contains("eventBindings", StringComparison.Ordinal) => "Liaison typée",
        var value when value.Contains("source", StringComparison.Ordinal) => "Source sélectionnée",
        _ => "Profil interop R101",
    };

    private static string HumanSemanticDiff(string diff)
    {
        if (string.Equals(diff, "no-change", StringComparison.Ordinal)) return "Aucun changement";
        var match = Regex.Match(diff, @"sources:(?<from>\d+)->(?<to>\d+)",
            RegexOptions.CultureInvariant);
        return match.Success
            ? $"Quota sources {match.Groups["from"].Value} → {match.Groups["to"].Value} · plans source et liaison valides"
            : "Profil et plans interop cohérents";
    }

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
}
