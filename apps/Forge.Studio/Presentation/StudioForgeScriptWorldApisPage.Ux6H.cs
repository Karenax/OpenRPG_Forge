using System.Text.RegularExpressions;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioForgeScriptWorldApisPageUx6H : ColorRect
{
    private const string ReviewedSourceMarkerUx6H = "<studio:ux6-h-world-apis>";
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
    private readonly Label _catalogStatus;
    private readonly CodeEdit _source;
    private readonly Label _sourceReview;
    private readonly VBoxContainer _diagnosticRows;
    private readonly OptionButton _inventoryOwner;
    private readonly OptionButton _inventoryRecipient;
    private readonly OptionButton _inventoryItem;
    private readonly SpinBox _inventoryQuantity;
    private readonly OptionButton _quest;
    private readonly LineEdit _questScope;
    private readonly OptionButton _questFrom;
    private readonly OptionButton _questTo;
    private readonly OptionButton _questObjective;
    private readonly SpinBox _questDelta;
    private readonly OptionButton _dialogue;
    private readonly OptionButton _dialogueSpeaker;
    private readonly OptionButton _dialogueListener;
    private readonly LineEdit _dialogueOutcome;
    private readonly OptionButton _factionActor;
    private readonly OptionButton _faction;
    private readonly SpinBox _reputationDelta;
    private readonly CheckButton _joinFaction;
    private readonly LineEdit _factionScope;
    private readonly OptionButton _observer;
    private readonly OptionButton _target;
    private readonly OptionButton _sense;
    private readonly SpinBox _distance;
    private readonly SpinBox _range;
    private readonly CheckButton _lineOfSight;
    private readonly SpinBox _signal;
    private readonly SpinBox _seed;
    private readonly Label _worldReview;
    private readonly Label _console;
    private readonly Label _status;
    private readonly Button _buildSource;
    private readonly Button _insertSource;
    private readonly Button _simulate;
    private readonly Button _preview;
    private readonly Button _compile;
    private readonly Button _sandbox;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;

    private IReadOnlyList<StudioUx6FSourceChoice> _choices = [];
    private StudioUx6HWorldCatalog _catalog = new([], [], [], [], []);
    private IReadOnlyList<StudioUx6HChoice> _questStates = [];
    private IReadOnlyList<StudioUx6HChoice> _questObjectives = [];
    private StudioUx6FDraft? _draft;
    private StudioUx6HSourcePlan? _sourcePlan;
    private StudioUx6HWorldResult? _worldResult;
    private StudioUx6FPreview? _previewResult;
    private StudioUx6FCompileResult? _compileResult;
    private StudioUx6FSandboxResult? _sandboxResult;
    private bool _explicitSource;
    private bool _updating;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? ScriptSourceChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSource && _draft is not null;
    internal bool HumanCatalogsReady => _catalog.Complete;
    internal bool ReviewedSourceReady => _sourcePlan is { Succeeded: true } &&
                                          _source.Text.Contains(ReviewedSourceMarkerUx6H,
                                              StringComparison.Ordinal);
    internal bool WorldApisReady => _worldResult is
    {
        Succeeded: true,
        InventoryCommitted: true,
        QuestTransitioned: true,
        DialogueCompleted: true,
        FactionJoined: true,
        DeterministicPerception: true,
        NativeSandboxExecuted: true,
    };
    internal bool PreviewReady => _previewResult is
    { Succeeded: true, CanApply: true, CanonicalDryRun: true };
    internal bool NativeExecutionReady => _compileResult is { Succeeded: true } &&
                                          _sandboxResult is { Succeeded: true };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => SourceMatchesUx6H();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx6H(this));
            return text.Contains("Inventaire", StringComparison.Ordinal) &&
                   text.Contains("Journal et quêtes", StringComparison.Ordinal) &&
                   text.Contains("Dialogues", StringComparison.Ordinal) &&
                   text.Contains("Factions et réputation", StringComparison.Ordinal) &&
                   text.Contains("Perception", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec", StringComparison.Ordinal) &&
                   !ContainsUuidUx6H(text);
        }
    }

    internal StudioForgeScriptWorldApisPageUx6H(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.008f, .014f, .023f, .985f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6HForgeScriptWorldApis", StudioStyle.Surface, 12, 1);
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
        headingText.AddChild(StudioStyle.Label("ForgeScript · API monde R098", 19));
        headingText.AddChild(StudioStyle.Label(
            "Inventaire · Journal et quêtes · Dialogues · Factions et réputation · Perception · même source canonique UX6-F.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        var sourceRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sourceChoice = StudioUx2EnvBControls.Options();
        _sourceChoice.CustomMinimumSize = new Vector2(430, 34);
        _sourceChoice.ItemSelected += SelectSourceUx6H;
        sourceRow.AddChild(FieldUx6H("Source canonique · sélection explicite", _sourceChoice, 430));
        _newSourcePath = StudioUx2EnvBControls.Text("scripts/world-apis.as");
        _newSourcePath.Text = "scripts/world-apis.as";
        _newSourcePath.CustomMinimumSize = new Vector2(300, 34);
        sourceRow.AddChild(FieldUx6H("Créer si le module est vide", _newSourcePath, 300));
        _createSource = StudioStyle.Button("Créer en mémoire", compact: true);
        _createSource.CustomMinimumSize = new Vector2(145, 34);
        _createSource.Pressed += CreateSourceUx6H;
        sourceRow.AddChild(_createSource);
        var reload = StudioStyle.Button("Recharger", compact: true);
        reload.CustomMinimumSize = new Vector2(120, 34);
        reload.Pressed += RefreshSourcesUx6H;
        sourceRow.AddChild(reload);
        root.AddChild(sourceRow);
        _selection = StudioStyle.Label(
            "Choisissez une source .as. Les scripts .forge historiques restent protégés par l’atelier fondation.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);
        _catalogStatus = StudioStyle.Label(
            "Les sélecteurs utilisent seulement les objets, acteurs, quêtes, dialogues et factions du projet ouvert.",
            9, StudioStyle.Muted, wrap: true);
        root.AddChild(_catalogStatus);

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

        var left = PanelColumnUx6H(body, 720, expand: true);
        left.AddChild(StudioStyle.Label("SOURCE REVUE · AUTORITÉ UNIQUE UX6-F", 10, StudioStyle.Accent));
        _source = new CodeEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(680, 300),
            DrawTabs = true,
            GuttersDrawLineNumbers = true,
        };
        _source.TextChanged += InvalidateSourceUx6H;
        left.AddChild(_source);
        var sourceActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _buildSource = StudioStyle.Button("Construire le bloc revu", compact: true);
        _buildSource.Pressed += BuildSourceUx6H;
        _insertSource = StudioStyle.Button("Insérer dans le brouillon", compact: true);
        _insertSource.Pressed += InsertSourceUx6H;
        sourceActions.AddChild(_buildSource);
        sourceActions.AddChild(_insertSource);
        left.AddChild(sourceActions);
        _sourceReview = StudioStyle.Label(
            "Aucun bloc généré. Le formulaire produit du code versionné à relire, jamais un fichier généré parallèle.",
            9, StudioStyle.Muted, wrap: true);
        _sourceReview.CustomMinimumSize = new Vector2(0, 78);
        left.AddChild(_sourceReview);
        left.AddChild(StudioStyle.Label("DIAGNOSTICS CLIQUABLES", 9, StudioStyle.Accent));
        var diagnosticScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 105),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _diagnosticRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        diagnosticScroll.AddChild(_diagnosticRows);
        left.AddChild(diagnosticScroll);

        var right = PanelColumnUx6H(body, 720);
        right.AddChild(StudioStyle.Label("SCÉNARIO MONDE · ÉTAT COPIÉ EN MÉMOIRE", 10, StudioStyle.Accent));
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(690, 400),
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

        var inventoryPage = TabPageUx6H(tabs, "Inventaire");
        _inventoryOwner = StudioUx2EnvBControls.Options();
        _inventoryRecipient = StudioUx2EnvBControls.Options();
        _inventoryItem = StudioUx2EnvBControls.Options();
        _inventoryQuantity = NumberUx6H(1, 999, 1, 1);
        inventoryPage.AddChild(FieldUx6H("Propriétaire source", _inventoryOwner));
        inventoryPage.AddChild(FieldUx6H("Destinataire", _inventoryRecipient));
        inventoryPage.AddChild(FieldUx6H("Objet réel", _inventoryItem));
        inventoryPage.AddChild(FieldUx6H("Quantité", _inventoryQuantity));
        inventoryPage.AddChild(StudioStyle.Label(
            "Transfert serveur, capacité, query, audit et rollback client sont vérifiés sans toucher aux inventaires du projet.",
            9, StudioStyle.Muted, wrap: true));

        var questPage = TabPageUx6H(tabs, "Journal et quêtes");
        _quest = StudioUx2EnvBControls.Options();
        _quest.ItemSelected += QuestSelectedUx6H;
        _questScope = StudioUx2EnvBControls.Text("party:studio");
        _questScope.Text = "party:studio";
        _questFrom = StudioUx2EnvBControls.Options();
        _questTo = StudioUx2EnvBControls.Options();
        _questObjective = StudioUx2EnvBControls.Options();
        _questDelta = NumberUx6H(-999, 999, 1, 1);
        questPage.AddChild(FieldUx6H("Quête réelle", _quest));
        questPage.AddChild(FieldUx6H("Portée d’instance", _questScope));
        questPage.AddChild(PairUx6H("État source", _questFrom, "État cible", _questTo));
        questPage.AddChild(PairUx6H("Objectif", _questObjective, "Progression", _questDelta));
        questPage.AddChild(StudioStyle.Label(
            "La simulation projette état, objectif, historique, cause, portée et révision.",
            9, StudioStyle.Muted, wrap: true));

        var dialoguePage = TabPageUx6H(tabs, "Dialogues");
        _dialogue = StudioUx2EnvBControls.Options();
        _dialogueSpeaker = StudioUx2EnvBControls.Options();
        _dialogueListener = StudioUx2EnvBControls.Options();
        _dialogueOutcome = StudioUx2EnvBControls.Text("accepted");
        _dialogueOutcome.Text = "accepted";
        dialoguePage.AddChild(FieldUx6H("Graphe réel", _dialogue));
        dialoguePage.AddChild(PairUx6H("Locuteur", _dialogueSpeaker, "Interlocuteur", _dialogueListener));
        dialoguePage.AddChild(FieldUx6H("Résultat nominal", _dialogueOutcome));
        dialoguePage.AddChild(StudioStyle.Label(
            "Démarrage, participants, variables, complétion et interruption sûre sont exercés sur une session copiée.",
            9, StudioStyle.Muted, wrap: true));

        var factionPage = TabPageUx6H(tabs, "Factions et réputation");
        _factionActor = StudioUx2EnvBControls.Options();
        _faction = StudioUx2EnvBControls.Options();
        _reputationDelta = NumberUx6H(-200, 200, 1, 25);
        _joinFaction = new CheckButton { Text = "Rejoindre la faction", ButtonPressed = true };
        _factionScope = StudioUx2EnvBControls.Text("module");
        _factionScope.Text = "module";
        factionPage.AddChild(FieldUx6H("Acteur réel", _factionActor));
        factionPage.AddChild(FieldUx6H("Faction active", _faction));
        factionPage.AddChild(PairUx6H("Réputation", _reputationDelta, "Portée", _factionScope));
        factionPage.AddChild(_joinFaction);
        factionPage.AddChild(StudioStyle.Label(
            "Si le module ne contient aucune faction, l’état vide renvoie explicitement vers UX4 Contenu social.",
            9, StudioStyle.Warning, wrap: true));

        var perceptionPage = TabPageUx6H(tabs, "Perception");
        _observer = StudioUx2EnvBControls.Options();
        _target = StudioUx2EnvBControls.Options();
        _sense = StudioUx2EnvBControls.Options("Vue", "Ouïe", "Odorat", "Magie", "Tremblements");
        _distance = NumberUx6H(0, 1_000_000, 0, 2);
        _range = NumberUx6H(0, 1_000_000, 0, 10);
        _lineOfSight = new CheckButton { Text = "Ligne de vue disponible", ButtonPressed = true };
        _signal = NumberUx6H(0, 1, 0, .8);
        _seed = NumberUx6H(int.MinValue, int.MaxValue, 1, 98);
        perceptionPage.AddChild(PairUx6H("Observateur", _observer, "Cible", _target));
        perceptionPage.AddChild(FieldUx6H("Sens", _sense));
        perceptionPage.AddChild(PairUx6H("Distance", _distance, "Portée", _range));
        perceptionPage.AddChild(PairUx6H("Signal", _signal, "Seed", _seed));
        perceptionPage.AddChild(_lineOfSight);
        perceptionPage.AddChild(StudioStyle.Label(
            "Confiance, stimuli et trace sont déterministes. Aucun raycast, acteur ou audio Godot n’est lancé.",
            9, StudioStyle.Muted, wrap: true));

        _simulate = StudioStyle.Button("Simuler les cinq API", compact: true);
        _simulate.Pressed += SimulateUx6H;
        right.AddChild(_simulate);
        _worldReview = StudioStyle.Label(
            "Aucune simulation. Le scénario reste une projection immuable et bornée.",
            9, StudioStyle.Muted, wrap: true);
        _worldReview.CustomMinimumSize = new Vector2(0, 115);
        right.AddChild(_worldReview);

        HookWorldControlsUx6H();

        _console = StudioStyle.Label(
            "CONSOLE · construction, simulation, compilation et sandbox sont sans écriture avant Aperçu sec puis Appliquer.",
            10, StudioStyle.Muted, wrap: true);
        _console.CustomMinimumSize = new Vector2(0, 58);
        root.AddChild(_console);
        _status = StudioStyle.Label("Prêt.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 7);
        _preview = StudioStyle.Button("Aperçu sec", compact: true);
        _preview.Pressed += PreviewUx6H;
        _compile = StudioStyle.Button("Compiler", compact: true);
        _compile.Pressed += CompileUx6H;
        _sandbox = StudioStyle.Button("Tester sandbox", compact: true);
        _sandbox.Pressed += SandboxUx6H;
        _apply = StudioStyle.Button("Appliquer et enregistrer", primary: true, compact: true);
        _apply.Pressed += ApplyUx6H;
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += UndoUx6H;
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += RedoUx6H;
        foreach (var button in new[] { _preview, _compile, _sandbox, _apply, _undo, _redo })
        {
            button.CustomMinimumSize = new Vector2(button == _apply ? 185 : 126, 35);
            footer.AddChild(button);
        }
        root.AddChild(footer);

        ResetControlsUx6H();
        UpdateButtonsUx6H();
    }

    internal void Open() => OpenBoundUx6H(_sourceSession, evidence: false, cancelApproval: false);

    internal void RestoreSourceSurfaceAfterSmoke() =>
        OpenBoundUx6H(_sourceSession, evidence: false, cancelApproval: false);

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null) return;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidenceUx6H();
        else
        {
            _session.CancelUx6FApproval();
            _session = _sourceSession;
        }
        Visible = false;
        ResetControlsUx6H();
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is null && _evidenceRoot is null) return;
        ReleaseEvidenceUx6H();
        Visible = false;
        ResetControlsUx6H();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (!mode.StartsWith("ux6-h", StringComparison.OrdinalIgnoreCase)) return false;
        CloseEvidence();
        _sourceSnapshot = CaptureSourceUx6H();
        _evidenceReleased = false;
        _evidenceRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-h-ui",
            Guid.NewGuid().ToString("N"));
        var sourceProject = _sourceSession.Project;
        if (sourceProject is null || !ProjectSession.Create(_evidenceRoot,
                StudioSessionController.CreateEvidenceProjectUx6H(sourceProject)).Succeeded)
            return FailEvidenceUx6H("La copie temporaire UX6-H n’a pas pu être créée.");
        new ForgeScriptSourceStore(_evidenceRoot).Save(StudioSessionController.EvidenceDocumentUx6F());
        _evidenceSession = new StudioSessionController();
        if (!_evidenceSession.Open(_evidenceRoot).Succeeded)
            return FailEvidenceUx6H("La session temporaire UX6-H n’a pas pu être ouverte.");
        OpenBoundUx6H(_evidenceSession, evidence: true);
        var sourceIndex = _choices.Select((choice, index) => (choice, index))
            .FirstOrDefault(item => item.choice.Kind == StudioUx6FSourceKind.Canonical).index;
        if (_choices.Count == 0 || _choices[sourceIndex].Kind != StudioUx6FSourceKind.Canonical)
            return FailEvidenceUx6H("Aucune source canonique n’est disponible dans la preuve.");
        _sourceChoice.Select(sourceIndex + 1);
        SelectSourceUx6H(sourceIndex + 1);
        BuildSourceUx6H();
        InsertSourceUx6H();
        SimulateUx6H();
        CompileUx6H();
        SandboxUx6H();
        PreviewUx6H();
        var ready = HasExplicitSelection && HumanCatalogsReady && ReviewedSourceReady && WorldApisReady &&
                    NativeExecutionReady && PreviewReady && ShowsHumanSurface && UsesIsolatedEvidence &&
                    SourceMatchesUx6H();
        if (!ready)
        {
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_H_EVIDENCE_FAILED " +
                        $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                        $"catalogs={HumanCatalogsReady.ToString().ToLowerInvariant()} " +
                        $"plan={(_sourcePlan is { Succeeded: true }).ToString().ToLowerInvariant()} " +
                        $"marker={_source.Text.Contains(ReviewedSourceMarkerUx6H, StringComparison.Ordinal).ToString().ToLowerInvariant()} " +
                        $"reviewed={ReviewedSourceReady.ToString().ToLowerInvariant()} " +
                        $"world_result={(_worldResult?.Succeeded == true).ToString().ToLowerInvariant()} " +
                        $"world_codes={string.Join(',', _worldResult?.Diagnostics.Select(item => item.Code) ?? [])} " +
                        $"world={WorldApisReady.ToString().ToLowerInvariant()} " +
                        $"native={NativeExecutionReady.ToString().ToLowerInvariant()} " +
                        $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                        $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                        $"isolated={UsesIsolatedEvidence.ToString().ToLowerInvariant()} " +
                        $"source={SourceMatchesUx6H().ToString().ToLowerInvariant()}");
            return FailEvidenceUx6H("La surface UX6-H n’a pas satisfait tous les invariants de preuve.");
        }
        return true;
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSourceUx6H();
        try
        {
            if (!PrepareEvidence("ux6-h-script-world-apis") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !PreviewReady) return false;
            var candidate = ReadDraftUx6H();
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
            return undone.Succeeded && redone.Succeeded && persisted && SourceMatchesUx6H(snapshot);
        }
        finally
        {
            CloseEvidence();
            _sourceSession.RestoreScriptApprovalUx6F(snapshot.Approval);
        }
    }

    private void OpenBoundUx6H(StudioSessionController session, bool evidence, bool cancelApproval = true)
    {
        _session = session;
        if (cancelApproval) _session.CancelUx6FApproval();
        ResetControlsUx6H();
        RefreshSourcesUx6H();
        Visible = true;
        MoveToFront();
        UpdateBoundsUx6H();
        SetStatusUx6H(evidence
            ? "Copie temporaire UX6-H · aucune source du module ouvert ne sera modifiée."
            : "Choisissez une source et les références monde réelles.", false);
    }

    private void RefreshSourcesUx6H()
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
        InvalidateUx6H();
    }

    private void SelectSourceUx6H(long selected)
    {
        if (_updating || selected <= 0 || selected > _choices.Count) return;
        var choice = _choices[(int)selected - 1];
        if (!_session.TryOpenCanonicalSourceUx6F(choice.RelativePath, out var draft, out var message) ||
            draft is null)
        {
            SetStatusUx6H(message, true);
            return;
        }
        _draft = draft;
        _explicitSource = true;
        _updating = true;
        _source.Text = draft.Source;
        _updating = false;
        _selection.Text = HumanValueUx6H($"{choice.Label} · API {draft.ApiVersion} · {choice.Detail}");
        PopulateWorldCatalogUx6H();
        InvalidateUx6H();
        SetStatusUx6H(message, false);
    }

    private void CreateSourceUx6H()
    {
        if (!_session.TryCreateCanonicalSourceUx6F(_newSourcePath.Text, out var draft, out var message) ||
            draft is null)
        {
            SetStatusUx6H(message, true);
            return;
        }
        _draft = draft;
        _explicitSource = true;
        _updating = true;
        _source.Text = draft.Source;
        _updating = false;
        _selection.Text = $"Nouvelle source en mémoire · {HumanValueUx6H(draft.RelativePath)}.";
        PopulateWorldCatalogUx6H();
        InvalidateUx6H();
        SetStatusUx6H(message, false);
    }

    private void PopulateWorldCatalogUx6H()
    {
        _catalog = _session.WorldCatalogUx6H();
        PopulateOptionsUx6H(_inventoryOwner, _catalog.Actors, "Aucun acteur disponible");
        PopulateOptionsUx6H(_inventoryRecipient, _catalog.Actors, "Aucun destinataire disponible", 1);
        PopulateOptionsUx6H(_inventoryItem, _catalog.Items, "Aucun objet disponible");
        PopulateQuestOptionsUx6H();
        PopulateOptionsUx6H(_dialogue, _catalog.Dialogues, "Aucun dialogue disponible");
        PopulateOptionsUx6H(_dialogueSpeaker, _catalog.Actors, "Aucun locuteur disponible");
        PopulateOptionsUx6H(_dialogueListener, _catalog.Actors, "Aucun interlocuteur disponible", 1);
        PopulateOptionsUx6H(_factionActor, _catalog.Actors, "Aucun acteur disponible", 1);
        PopulateOptionsUx6H(_faction, _catalog.Factions,
            "Aucune faction · ouvrez UX4 Contenu social");
        PopulateOptionsUx6H(_observer, _catalog.Actors, "Aucun observateur disponible");
        PopulateOptionsUx6H(_target, _catalog.Actors, "Aucune cible disponible", 1);
        _catalogStatus.Text = _catalog.Complete
            ? $"Références humaines prêtes · {_catalog.Items.Count} objet(s), {_catalog.Actors.Count} acteur(s), " +
              $"{_catalog.Quests.Count} quête(s), {_catalog.Dialogues.Count} dialogue(s), " +
              $"{_catalog.Factions.Count} faction(s)."
            : "ÉTAT VIDE · une famille requise manque. Créez-la dans sa page Studio avant de générer le bloc monde.";
        _catalogStatus.AddThemeColorOverride("font_color", _catalog.Complete ? StudioStyle.Success : StudioStyle.Warning);
    }

    private void PopulateQuestOptionsUx6H()
    {
        var choices = _catalog.Quests.Select(item => new StudioUx6HChoice(item.Key, item.Label,
            $"{item.States.Count} état(s) · {item.Objectives.Count} objectif(s)")).ToArray();
        PopulateOptionsUx6H(_quest, choices, "Aucune quête disponible");
        PopulateQuestDetailsUx6H();
    }

    private void QuestSelectedUx6H(long selected)
    {
        if (_updating) return;
        PopulateQuestDetailsUx6H();
        InvalidateUx6H();
    }

    private void PopulateQuestDetailsUx6H()
    {
        var index = Math.Clamp(_quest.Selected, 0, Math.Max(0, _catalog.Quests.Count - 1));
        var selected = _catalog.Quests.Count == 0 ? null : _catalog.Quests[index];
        _questStates = selected?.States ?? [];
        _questObjectives = selected?.Objectives.Count > 0
            ? selected.Objectives
            : [new StudioUx6HChoice("progression-principale", "Progression principale",
                "Clé de simulation quand la quête n’expose aucun objectif auteur.")];
        PopulateOptionsUx6H(_questFrom, _questStates, "Aucun état source");
        PopulateOptionsUx6H(_questTo, _questStates, "Aucun état cible", 1);
        PopulateOptionsUx6H(_questObjective, _questObjectives, "Aucun objectif");
    }

    private StudioUx6FDraft? ReadDraftUx6H() => _draft is null ? null : _draft with { Source = _source.Text };

    private StudioUx6HWorldDraft? ReadWorldUx6H()
    {
        var owner = KeyAtUx6H(_catalog.Actors, _inventoryOwner);
        var recipient = KeyAtUx6H(_catalog.Actors, _inventoryRecipient);
        var item = KeyAtUx6H(_catalog.Items, _inventoryItem);
        var quest = _catalog.Quests.Count == 0 || _quest.Selected < 0 || _quest.Selected >= _catalog.Quests.Count
            ? null
            : _catalog.Quests[_quest.Selected].Key;
        var from = KeyAtUx6H(_questStates, _questFrom);
        var to = KeyAtUx6H(_questStates, _questTo);
        var objective = KeyAtUx6H(_questObjectives, _questObjective);
        var dialogue = KeyAtUx6H(_catalog.Dialogues, _dialogue);
        var speaker = KeyAtUx6H(_catalog.Actors, _dialogueSpeaker);
        var listener = KeyAtUx6H(_catalog.Actors, _dialogueListener);
        var factionActor = KeyAtUx6H(_catalog.Actors, _factionActor);
        var faction = KeyAtUx6H(_catalog.Factions, _faction);
        var observer = KeyAtUx6H(_catalog.Actors, _observer);
        var target = KeyAtUx6H(_catalog.Actors, _target);
        if (new[] { owner, recipient, item, quest, from, to, objective, dialogue, speaker, listener,
                factionActor, faction, observer, target }.Any(string.IsNullOrWhiteSpace))
            return null;
        var senses = new[] { "sight", "hearing", "smell", "magic", "tremor" };
        var sense = senses[Math.Clamp(_sense.Selected, 0, senses.Length - 1)];
        return new(owner!, recipient!, item!, (int)_inventoryQuantity.Value, quest!, _questScope.Text.Trim(),
            from!, to!, objective!, (int)_questDelta.Value, dialogue!, speaker!, listener!,
            _dialogueOutcome.Text.Trim(), factionActor!, faction!, (int)_reputationDelta.Value,
            _joinFaction.ButtonPressed, _factionScope.Text.Trim(), observer!, target!, sense,
            _distance.Value, _range.Value, _lineOfSight.ButtonPressed, _signal.Value, (int)_seed.Value);
    }

    private void BuildSourceUx6H()
    {
        var draft = ReadDraftUx6H();
        var world = ReadWorldUx6H();
        if (draft is null || world is null)
        {
            SetStatusUx6H("Sélectionnez une source et toutes les références projet requises.", true);
            return;
        }
        _sourcePlan = _session.BuildWorldApiSourceUx6H(draft, world);
        _sourceReview.Text = _sourcePlan.Succeeded
            ? "SOURCE VERSIONNÉE À RELIRE\n" + HumanValueUx6H(_sourcePlan.ReviewedSource)
            : HumanValueUx6H(_sourcePlan.Message);
        RenderDiagnosticsUx6H(_sourcePlan.Diagnostics);
        SetStatusUx6H(_sourcePlan.Message, !_sourcePlan.Succeeded);
        UpdateButtonsUx6H();
    }

    private void InsertSourceUx6H()
    {
        if (_sourcePlan is not { Succeeded: true })
        {
            SetStatusUx6H("Construisez et relisez d’abord un bloc API monde valide.", true);
            return;
        }
        var retained = _sourcePlan;
        _updating = true;
        _source.Text = retained.CandidateSource;
        _updating = false;
        InvalidateUx6H();
        _sourcePlan = retained;
        _sourceReview.Text = "Bloc revu inséré dans le brouillon · simulation et Aperçu sec requis avant écriture.";
        SetStatusUx6H("Insertion en mémoire terminée · aucun fichier enregistré.", false);
        UpdateButtonsUx6H();
    }

    private void SimulateUx6H()
    {
        var draft = ReadDraftUx6H();
        var world = ReadWorldUx6H();
        if (draft is null || world is null)
        {
            SetStatusUx6H("Le scénario monde est incomplet.", true);
            return;
        }
        _worldResult = _session.PreviewWorldApisUx6H(draft, world);
        _worldReview.Text = HumanValueUx6H(
            $"{_worldResult.Message}\n" +
            $"Inventaire : committed={_worldResult.InventoryCommitted}, rollback={_worldResult.InventoryRollback}, " +
            $"quantité={_worldResult.InventoryQuantity}/{_worldResult.InventoryCapacity}.\n" +
            $"Quête : transition={_worldResult.QuestTransitioned}, progression={_worldResult.QuestProgress}. " +
            $"Dialogue : start={_worldResult.DialogueStarted}, complete={_worldResult.DialogueCompleted}, " +
            $"interrupt={_worldResult.DialogueInterrupted}.\n" +
            $"Faction : membre={_worldResult.FactionJoined}, réputation={_worldResult.Reputation}. " +
            $"Perception : perçu={_worldResult.Perceived}, confiance={_worldResult.Confidence:0.000}, " +
            $"déterministe={_worldResult.DeterministicPerception}.\n" +
            $"Sandbox native={_worldResult.NativeSandboxExecuted} · handle périmé refusé={_worldResult.StaleHandleBlocked}.");
        RenderDiagnosticsUx6H(_worldResult.Diagnostics);
        _console.Text = HumanValueUx6H("CONSOLE R098 · " +
                                      string.Join(" · ", _worldResult.NativeEvents.Take(5)));
        SetStatusUx6H(_worldResult.Message, !_worldResult.Succeeded);
        UpdateButtonsUx6H();
    }

    private void PreviewUx6H()
    {
        var draft = ReadDraftUx6H();
        if (draft is null || !WorldApisReady || !ReviewedSourceReady)
        {
            SetStatusUx6H("Insérez le bloc revu et réussissez d’abord la simulation des cinq API.", true);
            return;
        }
        _previewResult = _session.PreviewScriptSourceUx6F(draft);
        RenderDiagnosticsUx6H(_previewResult.Diagnostics);
        _console.Text = HumanValueUx6H(
            $"APERÇU SEC · {_previewResult.Message}\n{_previewResult.SemanticDiff}\n" +
            "Même ForgeScriptSourceHistory UX6-F · aucune écriture avant Appliquer.");
        SetStatusUx6H(_previewResult.Message, !_previewResult.Succeeded);
        UpdateButtonsUx6H();
    }

    private void CompileUx6H()
    {
        var draft = ReadDraftUx6H();
        if (draft is null) return;
        _compileResult = _session.CompileScriptSourceUx6F(draft);
        RenderDiagnosticsUx6H(_compileResult.Diagnostics);
        _console.Text = HumanValueUx6H(
            $"COMPILATEUR · {_compileResult.Message}\nVM {_compileResult.VirtualMachineVersion} · " +
            $"API {_compileResult.ApiVersion} · {_compileResult.BytecodeBytes:N0} octets.");
        SetStatusUx6H(_compileResult.Message, !_compileResult.Succeeded);
        UpdateButtonsUx6H();
    }

    private void SandboxUx6H()
    {
        var draft = ReadDraftUx6H();
        if (draft is null) return;
        _sandboxResult = _session.RunScriptSandboxUx6F(draft);
        RenderDiagnosticsUx6H(_sandboxResult.Diagnostics);
        _console.Text = HumanValueUx6H(
            $"SANDBOX · {_sandboxResult.Message}\nStatut {_sandboxResult.Status} · " +
            $"{_sandboxResult.Instructions:N0} instructions · {_sandboxResult.PeakMemoryBytes:N0} octets.");
        SetStatusUx6H(_sandboxResult.Message, !_sandboxResult.Succeeded);
        UpdateButtonsUx6H();
    }

    private void ApplyUx6H()
    {
        var draft = ReadDraftUx6H();
        if (draft is null) return;
        var result = _session.ApplyApprovedScriptSourceUx6F(draft);
        SetStatusUx6H(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        _draft = _session.CurrentScriptDraftUx6F();
        InvalidateUx6H();
        ScriptSourceChanged?.Invoke();
    }

    private void UndoUx6H()
    {
        var result = _session.UndoScriptSourceUx6F();
        SetStatusUx6H(result.Message, !result.Succeeded);
        if (result.Succeeded) ReloadActiveDraftUx6H();
    }

    private void RedoUx6H()
    {
        var result = _session.RedoScriptSourceUx6F();
        SetStatusUx6H(result.Message, !result.Succeeded);
        if (result.Succeeded) ReloadActiveDraftUx6H();
    }

    private void ReloadActiveDraftUx6H()
    {
        _draft = _session.CurrentScriptDraftUx6F();
        if (_draft is null) return;
        _updating = true;
        _source.Text = _draft.Source;
        _updating = false;
        InvalidateUx6H();
        ScriptSourceChanged?.Invoke();
    }

    private void HookWorldControlsUx6H()
    {
        foreach (var option in new[]
                 {
                     _inventoryOwner, _inventoryRecipient, _inventoryItem, _questFrom, _questTo,
                     _questObjective, _dialogue, _dialogueSpeaker, _dialogueListener, _factionActor,
                     _faction, _observer, _target, _sense,
                 })
            option.ItemSelected += _ => InvalidateWorldUx6H();
        foreach (var line in new[] { _questScope, _dialogueOutcome, _factionScope })
            line.TextChanged += _ => InvalidateWorldUx6H();
        foreach (var number in new[]
                 {
                     _inventoryQuantity, _questDelta, _reputationDelta, _distance, _range, _signal, _seed,
                 })
            number.ValueChanged += _ => InvalidateWorldUx6H();
        _joinFaction.Toggled += _ => InvalidateWorldUx6H();
        _lineOfSight.Toggled += _ => InvalidateWorldUx6H();
    }

    private void InvalidateSourceUx6H()
    {
        if (_updating) return;
        InvalidateUx6H();
    }

    private void InvalidateWorldUx6H()
    {
        if (_updating) return;
        InvalidateUx6H();
    }

    private void InvalidateUx6H()
    {
        _session.CancelUx6FApproval();
        _sourcePlan = null;
        _worldResult = null;
        _previewResult = null;
        _compileResult = null;
        _sandboxResult = null;
        _sourceReview.Text = "Brouillon modifié · reconstruisez le bloc versionné.";
        _worldReview.Text = "Aucune simulation valide pour ce brouillon.";
        _console.Text = "CONSOLE · brouillon modifié · relancez construction, simulation et Aperçu sec.";
        UpdateButtonsUx6H();
    }

    private void UpdateButtonsUx6H()
    {
        var selected = HasExplicitSelection;
        _buildSource.Disabled = !selected || !HumanCatalogsReady;
        _insertSource.Disabled = !selected || _sourcePlan is not { Succeeded: true };
        _simulate.Disabled = !selected || !HumanCatalogsReady || !ReviewedSourceReady;
        _preview.Disabled = !selected || !WorldApisReady || !ReviewedSourceReady;
        _compile.Disabled = !selected;
        _sandbox.Disabled = !selected;
        _apply.Disabled = !selected || !PreviewReady || !WorldApisReady;
        _undo.Disabled = !selected;
        _redo.Disabled = !selected;
    }

    private void ResetControlsUx6H()
    {
        _updating = true;
        _sourceChoice.Clear();
        _sourceChoice.AddItem("Choisir explicitement une source…");
        _sourceChoice.Select(0);
        _source.Text = string.Empty;
        _newSourcePath.Text = "scripts/world-apis.as";
        _questScope.Text = "party:studio";
        _dialogueOutcome.Text = "accepted";
        _factionScope.Text = "module";
        _inventoryQuantity.Value = 1;
        _questDelta.Value = 1;
        _reputationDelta.Value = 25;
        _joinFaction.ButtonPressed = true;
        _sense.Select(0);
        _distance.Value = 2;
        _range.Value = 10;
        _lineOfSight.ButtonPressed = true;
        _signal.Value = .8;
        _seed.Value = 98;
        _updating = false;
        _choices = [];
        _catalog = new([], [], [], [], []);
        _questStates = [];
        _questObjectives = [];
        _draft = null;
        _explicitSource = false;
        _sourcePlan = null;
        _worldResult = null;
        _previewResult = null;
        _compileResult = null;
        _sandboxResult = null;
        _catalogStatus.Text = "Les sélecteurs utilisent seulement les références du projet ouvert.";
        _sourceReview.Text = "Aucun bloc généré.";
        _worldReview.Text = "Aucune simulation. Le scénario reste une projection immuable et bornée.";
        _console.Text = "CONSOLE · aucune opération exécutée.";
        ClearChildrenUx6H(_diagnosticRows);
        UpdateButtonsUx6H();
    }

    private void RenderDiagnosticsUx6H(IReadOnlyList<StudioUx6FDiagnostic> diagnostics)
    {
        ClearChildrenUx6H(_diagnosticRows);
        if (diagnostics.Count == 0)
        {
            _diagnosticRows.AddChild(StudioStyle.Label("✓ Aucun diagnostic bloquant.", 9, StudioStyle.Success));
            return;
        }
        foreach (var diagnostic in diagnostics.Take(8))
        {
            var text = HumanValueUx6H($"{diagnostic.Code} · {diagnostic.Path} · {diagnostic.Message}");
            var button = StudioStyle.Button(text, compact: true);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.Alignment = HorizontalAlignment.Left;
            button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            button.TooltipText = text;
            button.Pressed += () => FocusDiagnosticUx6H(diagnostic);
            _diagnosticRows.AddChild(button);
        }
    }

    private void FocusDiagnosticUx6H(StudioUx6FDiagnostic diagnostic)
    {
        if (diagnostic.Line > 0)
        {
            _source.SetCaretLine(Math.Clamp(diagnostic.Line - 1, 0, Math.Max(0, _source.GetLineCount() - 1)));
            _source.SetCaretColumn(Math.Max(0, diagnostic.Column - 1));
            _source.GrabFocus();
        }
        SetStatusUx6H($"Diagnostic {diagnostic.Code} · {diagnostic.Path}.", true);
    }

    private StudioUx6FEvidenceSourceSnapshot CaptureSourceUx6H()
    {
        var project = _sourceSession.Project ?? throw new InvalidOperationException("No source project.");
        var history = _sourceSession.History ?? throw new InvalidOperationException("No source history.");
        return new(project, history, history.State.Revision, history.Journal.ToArray(),
            _sourceSession.IsDirty, history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.CaptureScriptApprovalUx6F());
    }

    private bool SourceMatchesUx6H(StudioUx6FEvidenceSourceSnapshot? expected = null)
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

    private bool FailEvidenceUx6H(string message)
    {
        SetStatusUx6H(message, true);
        ReleaseEvidenceUx6H();
        Visible = false;
        return false;
    }

    private bool ReleaseEvidenceUx6H()
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
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_H_RELEASE_FAILED type={exception.GetType().Name}");
        }
        var rootReleased = StudioSessionController.DeleteUx6FTemporaryWorkspace(root);
        _evidenceSession = null;
        _evidenceRoot = null;
        _session = _sourceSession;
        _evidenceReleased = sessionReleased && rootReleased;
        return _evidenceReleased;
    }

    private void SetStatusUx6H(string message, bool error)
    {
        _status.Text = HumanValueUx6H(message);
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(HumanValueUx6H(message), error);
    }

    private void UpdateBoundsUx6H()
    {
        var viewport = GetViewportRect().Size;
        var width = Math.Clamp(viewport.X - 44, 1420, 1880);
        var height = Math.Clamp(viewport.Y - 44, 760, 1030);
        _dialog.Position = new Vector2((viewport.X - width) / 2, (viewport.Y - height) / 2);
        _dialog.Size = new Vector2(width, height);
    }

    private static VBoxContainer PanelColumnUx6H(HBoxContainer host, int minimum, bool expand = false)
    {
        var panel = StudioStyle.Panel("Ux6HColumn", StudioStyle.SurfaceRaised, 8, 1);
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

    private static VBoxContainer TabPageUx6H(TabContainer tabs, string title)
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
        var page = new VBoxContainer { CustomMinimumSize = new Vector2(640, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 8);
        margin.AddChild(page);
        return page;
    }

    private static VBoxContainer FieldUx6H(string label, Control control, int minimum = 118)
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(minimum, 0) };
        box.AddThemeConstantOverride("separation", 2);
        box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        box.AddChild(control);
        return box;
    }

    private static HBoxContainer PairUx6H(string leftLabel, Control left, string rightLabel, Control right)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(FieldUx6H(leftLabel, left, 275));
        row.AddChild(FieldUx6H(rightLabel, right, 275));
        return row;
    }

    private static SpinBox NumberUx6H(double minimum, double maximum, double step, double value)
    {
        var number = StudioUx2EnvBControls.Number(minimum, maximum, step);
        number.Value = value;
        number.CustomArrowStep = step == 0 ? .1 : step;
        return number;
    }

    private static void PopulateOptionsUx6H(
        OptionButton option,
        IReadOnlyList<StudioUx6HChoice> choices,
        string empty,
        int preferred = 0)
    {
        option.Clear();
        if (choices.Count == 0)
        {
            option.AddItem(empty);
            option.Select(0);
            option.Disabled = true;
            return;
        }
        option.Disabled = false;
        foreach (var choice in choices)
        {
            option.AddItem(choice.Label);
            option.SetItemTooltip(option.ItemCount - 1, choice.Detail);
        }
        option.Select(Math.Clamp(preferred, 0, choices.Count - 1));
    }

    private static string? KeyAtUx6H(IReadOnlyList<StudioUx6HChoice> choices, OptionButton option) =>
        choices.Count == 0 || option.Selected < 0 || option.Selected >= choices.Count
            ? null
            : choices[option.Selected].Key;

    private static void ClearChildrenUx6H(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
    }

    private static IEnumerable<string> DescendantTextUx6H(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Label label) yield return label.Text;
            if (child is Button button) yield return button.Text;
            if (child is OptionButton option)
                for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
            foreach (var value in DescendantTextUx6H(child)) yield return value;
        }
    }

    private static bool ContainsUuidUx6H(string text) => Regex.IsMatch(text,
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.CultureInvariant);

    private static string HumanValueUx6H(string value) => Regex.Replace(value ?? string.Empty,
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        "identifiant protégé", RegexOptions.CultureInvariant);

    public override void _Notification(int what)
    {
        if (what == NotificationResized && Visible) UpdateBoundsUx6H();
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
            PreviewUx6H();
            GetViewport().SetInputAsHandled();
        }
    }
}
