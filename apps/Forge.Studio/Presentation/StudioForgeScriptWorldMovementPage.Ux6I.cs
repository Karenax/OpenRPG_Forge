using System.Text.RegularExpressions;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioForgeScriptWorldMovementPageUx6I : ColorRect
{
    private const string ReviewedSourceMarker = "<studio:ux6-i-world-movement>";
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
    private readonly OptionButton _area;
    private readonly OptionButton _subjectTag;
    private readonly OptionButton _targetTag;
    private readonly OptionButton _blueprint;
    private readonly OptionButton _movementMode;
    private readonly SpinBox _destinationX;
    private readonly SpinBox _destinationY;
    private readonly SpinBox _destinationZ;
    private readonly SpinBox _velocityX;
    private readonly SpinBox _velocityY;
    private readonly SpinBox _velocityZ;
    private readonly SpinBox _spawnX;
    private readonly SpinBox _spawnY;
    private readonly SpinBox _spawnZ;
    private readonly SpinBox _followDistance;
    private readonly SpinBox _localInteger;
    private readonly SpinBox _localNumber;
    private readonly LineEdit _localText;
    private readonly LineEdit _regexValue;
    private readonly LineEdit _regexPattern;
    private readonly SpinBox _floatPrecision;
    private readonly Label _runtimeReview;
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
    private StudioUx6ICatalog _catalog = new([], []);
    private StudioUx6IAreaChoice? _selectedArea;
    private StudioUx6FDraft? _draft;
    private StudioUx6ISourcePlan? _sourcePlan;
    private StudioUx6IRuntimeResult? _runtimeResult;
    private StudioUx6FPreview? _previewResult;
    private StudioUx6FCompileResult? _compileResult;
    private StudioUx6FSandboxResult? _sandboxResult;
    private bool _explicitSource;
    private bool _explicitArea;
    private bool _updating;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? ScriptSourceChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSource && _explicitArea && _draft is not null;
    internal bool HumanCatalogsReady => _catalog.Complete;
    internal bool ReviewedSourceReady => _sourcePlan is { Succeeded: true } &&
                                          _source.Text.Contains(ReviewedSourceMarker,
                                              StringComparison.Ordinal);
    internal bool WorldMovementReady => _runtimeResult is
    {
        Succeeded: true,
        AreaAndQueries: true,
        SpawnAndDestroy: true,
        MovementTasks: true,
        TeleportAndVelocity: true,
        MovementCancelled: true,
        UtilitiesDeterministic: true,
        UnicodeAndRegex: true,
        TypedLocals: true,
        ForbiddenCapabilityBlocked: true,
        QuotaInterrupted: true,
        RuntimeRecovered: true,
        CanonicalDryRun: true,
        CommandApplyUndoRedo: true,
        InvalidIntentBlocked: true,
        StaleRevisionBlocked: true,
    };
    internal bool NativeExecutionReady => _compileResult is { Succeeded: true } &&
                                          _sandboxResult is { Succeeded: true };
    internal bool PreviewReady => _previewResult is
    { Succeeded: true, CanApply: true, CanonicalDryRun: true };
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
            return text.Contains("Zones et objets", StringComparison.Ordinal) &&
                   text.Contains("Mouvement", StringComparison.Ordinal) &&
                   text.Contains("Math et chaînes", StringComparison.Ordinal) &&
                   text.Contains("Variables locales", StringComparison.Ordinal) &&
                   text.Contains("Sandbox", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec", StringComparison.Ordinal) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioForgeScriptWorldMovementPageUx6I(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.008f, .014f, .023f, .985f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6IForgeScriptWorldMovement", StudioStyle.Surface, 12, 1);
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
        headingText.AddChild(StudioStyle.Label("ForgeScript · monde, mouvement et utilitaires R013", 19));
        headingText.AddChild(StudioStyle.Label(
            "Zones et objets · Mouvement · Math et chaînes · Variables locales · Sandbox stricte · même source canonique UX6-F.",
            10, StudioStyle.Muted, wrap: true));
        heading.AddChild(headingText);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);
        root.AddChild(heading);

        var sourceRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sourceChoice = StudioUx2EnvBControls.Options();
        _sourceChoice.CustomMinimumSize = new Vector2(420, 34);
        _sourceChoice.ItemSelected += SelectSource;
        sourceRow.AddChild(Field("Source canonique · sélection explicite", _sourceChoice, 420));
        _newSourcePath = StudioUx2EnvBControls.Text("scripts/world-movement.as");
        _newSourcePath.Text = "scripts/world-movement.as";
        _newSourcePath.CustomMinimumSize = new Vector2(285, 34);
        sourceRow.AddChild(Field("Créer si le module est vide", _newSourcePath, 285));
        _createSource = StudioStyle.Button("Créer en mémoire", compact: true);
        _createSource.CustomMinimumSize = new Vector2(145, 34);
        _createSource.Pressed += CreateSource;
        sourceRow.AddChild(_createSource);
        var reload = StudioStyle.Button("Recharger", compact: true);
        reload.CustomMinimumSize = new Vector2(115, 34);
        reload.Pressed += RefreshSources;
        sourceRow.AddChild(reload);
        root.AddChild(sourceRow);
        _selection = StudioStyle.Label(
            "Choisissez une source .as puis une zone réelle. Aucun identifiant technique n’est affiché.",
            10, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);
        _catalogStatus = StudioStyle.Label(
            "Les sélecteurs proviennent uniquement des zones, tags et blueprints du projet ouvert.",
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

        var left = PanelColumn(body, 685, expand: true);
        left.AddChild(StudioStyle.Label("SOURCE REVUE · AUTORITÉ UNIQUE UX6-F", 10, StudioStyle.Accent));
        _source = new CodeEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(645, 300),
            DrawTabs = true,
            GuttersDrawLineNumbers = true,
        };
        _source.TextChanged += InvalidateSource;
        left.AddChild(_source);
        var sourceActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _buildSource = StudioStyle.Button("Construire le bloc revu", compact: true);
        _buildSource.Pressed += BuildSource;
        _insertSource = StudioStyle.Button("Insérer dans le brouillon", compact: true);
        _insertSource.Pressed += InsertSource;
        sourceActions.AddChild(_buildSource);
        sourceActions.AddChild(_insertSource);
        left.AddChild(sourceActions);
        _sourceReview = StudioStyle.Label(
            "Aucun bloc généré. Le formulaire produit un bloc versionné à relire avant insertion.",
            9, StudioStyle.Muted, wrap: true);
        _sourceReview.CustomMinimumSize = new Vector2(0, 72);
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

        var right = PanelColumn(body, 755);
        right.AddChild(StudioStyle.Label("SCÉNARIO R013 · ÉTAT COPIÉ EN MÉMOIRE", 10, StudioStyle.Accent));
        var tabsHost = new Control
        {
            CustomMinimumSize = new Vector2(725, 400),
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

        var worldPage = TabPage(tabs, "Zones et objets");
        worldPage.AddChild(StudioStyle.Label("Zones et objets · références humaines du module", 11,
            StudioStyle.Accent));
        _area = StudioUx2EnvBControls.Options();
        _area.ItemSelected += SelectArea;
        _subjectTag = StudioUx2EnvBControls.Options();
        _targetTag = StudioUx2EnvBControls.Options();
        _blueprint = StudioUx2EnvBControls.Options();
        worldPage.AddChild(Field("Zone réelle · sélection explicite", _area));
        worldPage.AddChild(Pair("Sujet recherché par tag", _subjectTag,
            "Cible recherchée par tag", _targetTag));
        worldPage.AddChild(Field("Blueprint à instancier dans la copie", _blueprint));
        _spawnX = ExactNumber(2);
        _spawnY = ExactNumber(0);
        _spawnZ = ExactNumber(1);
        worldPage.AddChild(VectorRow("Position d’apparition X / Y / Z", _spawnX, _spawnY, _spawnZ));
        worldPage.AddChild(StudioStyle.Label(
            "L’apparition et la destruction sont appliquées uniquement à la zone copiée en mémoire.",
            9, StudioStyle.Warning, wrap: true));

        var movementPage = TabPage(tabs, "Mouvement");
        movementPage.AddChild(StudioStyle.Label("Mouvement · tâches opaques et commandes bornées", 11,
            StudioStyle.Accent));
        _movementMode = StudioUx2EnvBControls.Options();
        foreach (var label in new[] { "Marche", "Course", "Vol", "Nage" }) _movementMode.AddItem(label);
        _movementMode.Select(1);
        movementPage.AddChild(Field("Mode", _movementMode));
        _destinationX = ExactNumber(3);
        _destinationY = ExactNumber(0);
        _destinationZ = ExactNumber(4);
        movementPage.AddChild(VectorRow("Destination X / Y / Z", _destinationX, _destinationY,
            _destinationZ));
        _velocityX = ExactNumber(0);
        _velocityY = ExactNumber(0);
        _velocityZ = ExactNumber(3);
        movementPage.AddChild(VectorRow("Vélocité X / Y / Z", _velocityX, _velocityY, _velocityZ));
        _followDistance = ExactNumber(2, 0, 512);
        movementPage.AddChild(Field("Distance de suivi", _followDistance));
        movementPage.AddChild(StudioStyle.Label(
            "L’aperçu vérifie move_to, face_towards, follow, set_velocity, teleport et cancel_movement sans déplacer un acteur Godot.",
            9, StudioStyle.Muted, wrap: true));

        var utilitiesPage = TabPage(tabs, "Math et chaînes");
        utilitiesPage.AddChild(StudioStyle.Label("Math et chaînes · résultats déterministes", 11,
            StudioStyle.Accent));
        _regexValue = StudioUx2EnvBControls.Text("ash-guard");
        _regexValue.Text = "ash-guard";
        _regexPattern = StudioUx2EnvBControls.Text("^[a-z-]+$");
        _regexPattern.Text = "^[a-z-]+$";
        _floatPrecision = Number(0, 9, 1, 2);
        utilitiesPage.AddChild(Field("Texte Unicode testé", _regexValue));
        utilitiesPage.AddChild(Field("Expression régulière bornée", _regexPattern));
        utilitiesPage.AddChild(Field("Précision format_float", _floatPrecision));
        utilitiesPage.AddChild(StudioStyle.Label(
            "vec3, quat_identity, distance, dot, regex_match, unicode_length, format_int et format_float sont exécutés par le runtime natif.",
            9, StudioStyle.Muted, wrap: true));

        var localsPage = TabPage(tabs, "Variables locales");
        localsPage.AddChild(StudioStyle.Label("Variables locales · types explicites", 11,
            StudioStyle.Accent));
        _localInteger = Number(long.MinValue, long.MaxValue, 1, 2);
        _localNumber = ExactNumber(5);
        _localText = StudioUx2EnvBControls.Text("Gardienne");
        _localText.Text = "Gardienne";
        localsPage.AddChild(Field("Entier 64 bits", _localInteger));
        localsPage.AddChild(Field("Nombre réel", _localNumber));
        localsPage.AddChild(Field("Chaîne UTF-8", _localText));
        localsPage.AddChild(StudioStyle.Label(
            "Les valeurs int, float, string, EntityRef et Vec3 sont écrites puis relues sur le sujet copié.",
            9, StudioStyle.Muted, wrap: true));

        var sandboxPage = TabPage(tabs, "Sandbox");
        sandboxPage.AddChild(StudioStyle.Label("Sandbox · fichiers, réseau et processus interdits", 11,
            StudioStyle.Accent));
        sandboxPage.AddChild(StudioStyle.Label(
            "La preuve compile une tentative file_open et exige le diagnostic capability_forbidden. Aucun chemin de fichier n’est transmis à un module.",
            10, StudioStyle.Warning, wrap: true));
        sandboxPage.AddChild(StudioStyle.Label(
            "Une boucle abusive doit être interrompue par le quota d’instructions, puis le même runtime doit exécuter à nouveau un script valide.",
            10, StudioStyle.Muted, wrap: true));
        sandboxPage.AddChild(StudioStyle.Label(
            "Aucune API Godot, mémoire native, thread, DLL, socket, processus ou système de fichiers n’est exposée.",
            10, StudioStyle.Muted, wrap: true));

        HookWorldControls();

        _simulate = StudioStyle.Button("Simuler R013 sur la copie", primary: true, compact: true);
        _simulate.Pressed += Simulate;
        right.AddChild(_simulate);
        _runtimeReview = StudioStyle.Label(
            "Aucune simulation. Monde, mouvement, utilitaires et locaux restent sans effet sur le projet.",
            9, StudioStyle.Muted, wrap: true);
        _runtimeReview.CustomMinimumSize = new Vector2(0, 110);
        right.AddChild(_runtimeReview);

        _console = StudioStyle.Label("CONSOLE · aucune opération exécutée.", 9, StudioStyle.Muted, wrap: true);
        _console.CustomMinimumSize = new Vector2(0, 58);
        root.AddChild(_console);
        _status = StudioStyle.Label("Prêt.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 7);
        _preview = StudioStyle.Button("Aperçu sec", compact: true);
        _preview.Pressed += Preview;
        _compile = StudioStyle.Button("Compiler", compact: true);
        _compile.Pressed += Compile;
        _sandbox = StudioStyle.Button("Tester sandbox", compact: true);
        _sandbox.Pressed += RunSandbox;
        _apply = StudioStyle.Button("Appliquer et enregistrer", primary: true, compact: true);
        _apply.Pressed += Apply;
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += Undo;
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += Redo;
        foreach (var button in new[] { _preview, _compile, _sandbox, _apply, _undo, _redo })
        {
            button.CustomMinimumSize = new Vector2(button == _apply ? 185 : 126, 35);
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
        if (!mode.StartsWith("ux6-i", StringComparison.OrdinalIgnoreCase)) return false;
        CloseEvidence();
        _sourceSnapshot = CaptureSource();
        _evidenceReleased = false;
        _evidenceRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-i-ui",
            Guid.NewGuid().ToString("N"));
        var sourceProject = _sourceSession.Project;
        if (sourceProject is null || !ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded)
            return FailEvidence("La copie temporaire UX6-I n’a pas pu être créée.");
        new ForgeScriptSourceStore(_evidenceRoot).Save(StudioSessionController.EvidenceDocumentUx6F());
        _evidenceSession = new StudioSessionController();
        if (!_evidenceSession.Open(_evidenceRoot).Succeeded)
            return FailEvidence("La session temporaire UX6-I n’a pas pu être ouverte.");
        OpenBound(_evidenceSession, evidence: true);
        var sourceIndex = -1;
        for (var index = 0; index < _choices.Count; index++)
        {
            if (_choices[index].Kind != StudioUx6FSourceKind.Canonical) continue;
            sourceIndex = index;
            break;
        }
        if (sourceIndex < 0) return FailEvidence("Aucune source canonique n’est disponible dans la preuve.");
        _sourceChoice.Select(sourceIndex + 1);
        SelectSource(sourceIndex + 1);
        if (_catalog.Areas.Count == 0) return FailEvidence("Aucune zone R013 exploitable n’est disponible.");
        _area.Select(1);
        SelectArea(1);
        BuildSource();
        InsertSource();
        Simulate();
        Compile();
        RunSandbox();
        Preview();
        var ready = HasExplicitSelection && HumanCatalogsReady && ReviewedSourceReady &&
                    WorldMovementReady && NativeExecutionReady && PreviewReady && ShowsHumanSurface &&
                    UsesIsolatedEvidence && SourceMatches();
        if (ready) return true;
        GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_I_EVIDENCE_FAILED " +
                    $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                    $"catalogs={HumanCatalogsReady.ToString().ToLowerInvariant()} " +
                    $"plan={(_sourcePlan?.Succeeded == true).ToString().ToLowerInvariant()} " +
                    $"reviewed={ReviewedSourceReady.ToString().ToLowerInvariant()} " +
                    $"runtime={WorldMovementReady.ToString().ToLowerInvariant()} " +
                    $"runtime_codes={string.Join(',', _runtimeResult?.Diagnostics.Select(item => item.Code) ?? [])} " +
                    $"native={NativeExecutionReady.ToString().ToLowerInvariant()} " +
                    $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                    $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                    $"isolated={UsesIsolatedEvidence.ToString().ToLowerInvariant()} " +
                    $"source={SourceMatches().ToString().ToLowerInvariant()}");
        return FailEvidence("La surface UX6-I n’a pas satisfait tous les invariants de preuve.");
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource();
        try
        {
            if (!PrepareEvidence("ux6-i-world-movement-utilities") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !PreviewReady) return false;
            var candidate = ReadDraft();
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
            ? "Copie temporaire UX6-I · la source du module ouvert reste strictement inchangée."
            : "Choisissez une source puis une zone réelle pour construire le scénario R013.", false);
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
        PopulateCatalog();
        Invalidate();
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
        _updating = false;
        _selection.Text = HumanValue($"{choice.Label} · API {draft.ApiVersion} · {choice.Detail}");
        PopulateCatalog();
        Invalidate();
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
        _updating = false;
        _selection.Text = $"Nouvelle source en mémoire · {HumanValue(draft.RelativePath)}.";
        PopulateCatalog();
        Invalidate();
        SetStatus(message, false);
    }

    private void PopulateCatalog()
    {
        _catalog = _session.WorldMovementCatalogUx6I();
        _updating = true;
        _area.Clear();
        _area.AddItem("Choisir explicitement une zone…");
        foreach (var item in _catalog.Areas)
        {
            _area.AddItem(item.Label);
            _area.SetItemTooltip(_area.ItemCount - 1,
                $"{item.EntityCount} objet(s) · {item.Tags.Count} tag(s) humain(s)");
        }
        _area.Select(0);
        PopulateOptions(_blueprint, _catalog.Blueprints, "Aucun blueprint disponible");
        _subjectTag.Clear();
        _subjectTag.AddItem("Sélectionnez d’abord une zone");
        _subjectTag.Disabled = true;
        _targetTag.Clear();
        _targetTag.AddItem("Sélectionnez d’abord une zone");
        _targetTag.Disabled = true;
        _updating = false;
        _selectedArea = null;
        _explicitArea = false;
        _catalogStatus.Text = _catalog.Complete
            ? $"Références humaines prêtes · {_catalog.Areas.Count} zone(s) avec tags · " +
              $"{_catalog.Blueprints.Count} blueprint(s)."
            : "ÉTAT VIDE · aucune zone à deux tags ou aucun blueprint. Créez ces données dans leurs pages Studio.";
        _catalogStatus.AddThemeColorOverride("font_color", _catalog.Complete
            ? StudioStyle.Success
            : StudioStyle.Warning);
    }

    private void SelectArea(long selected)
    {
        if (_updating || selected <= 0 || selected > _catalog.Areas.Count) return;
        _selectedArea = _catalog.Areas[(int)selected - 1];
        _explicitArea = true;
        _updating = true;
        PopulateOptions(_subjectTag, _selectedArea.Tags, "Aucun tag sujet");
        PopulateOptions(_targetTag, _selectedArea.Tags, "Aucun tag cible", preferred: 1);
        _updating = false;
        Invalidate();
        SetStatus($"Zone {_selectedArea.Label} sélectionnée · aucune mutation effectuée.", false);
    }

    private StudioUx6FDraft? ReadDraft() => _draft is null ? null : _draft with { Source = _source.Text };

    private StudioUx6IWorldDraft? ReadWorld()
    {
        if (_selectedArea is null || !_explicitArea) return null;
        var subject = KeyAt(_selectedArea.Tags, _subjectTag);
        var target = KeyAt(_selectedArea.Tags, _targetTag);
        var blueprint = KeyAt(_catalog.Blueprints, _blueprint);
        if (new[] { subject, target, blueprint }.Any(string.IsNullOrWhiteSpace)) return null;
        var modes = new[] { "walk", "run", "fly", "swim" };
        return new(_selectedArea.Id, subject!, target!, blueprint!,
            modes[Math.Clamp(_movementMode.Selected, 0, modes.Length - 1)],
            _destinationX.Value, _destinationY.Value, _destinationZ.Value,
            _velocityX.Value, _velocityY.Value, _velocityZ.Value,
            _spawnX.Value, _spawnY.Value, _spawnZ.Value, _followDistance.Value,
            (long)_localInteger.Value, _localNumber.Value, _localText.Text,
            _regexValue.Text, _regexPattern.Text, (int)_floatPrecision.Value);
    }

    private void BuildSource()
    {
        var draft = ReadDraft();
        var world = ReadWorld();
        if (draft is null || world is null)
        {
            SetStatus("Sélectionnez explicitement une source, une zone et ses références.", true);
            return;
        }
        _sourcePlan = _session.BuildWorldMovementSourceUx6I(draft, world);
        _sourceReview.Text = _sourcePlan.Succeeded
            ? "SOURCE VERSIONNÉE À RELIRE\n" + HumanValue(_sourcePlan.ReviewedSource)
            : HumanValue(_sourcePlan.Message);
        RenderDiagnostics(_sourcePlan.Diagnostics);
        SetStatus(_sourcePlan.Message, !_sourcePlan.Succeeded);
        UpdateButtons();
    }

    private void InsertSource()
    {
        if (_sourcePlan is not { Succeeded: true })
        {
            SetStatus("Construisez et relisez d’abord un bloc R013 valide.", true);
            return;
        }
        var retained = _sourcePlan;
        _updating = true;
        _source.Text = retained.CandidateSource;
        _updating = false;
        Invalidate();
        _sourcePlan = retained;
        _sourceReview.Text = "Bloc revu inséré dans le brouillon · simulation et Aperçu sec requis avant écriture.";
        SetStatus("Insertion en mémoire terminée · aucun fichier enregistré.", false);
        UpdateButtons();
    }

    private void Simulate()
    {
        var draft = ReadDraft();
        var world = ReadWorld();
        if (draft is null || world is null)
        {
            SetStatus("Le scénario R013 est incomplet.", true);
            return;
        }
        _runtimeResult = _session.PreviewWorldMovementUx6I(draft, world);
        _runtimeReview.Text = HumanValue(
            $"{_runtimeResult.Message}\n" +
            $"Monde : zones/requêtes={_runtimeResult.AreaAndQueries}, apparition/destruction={_runtimeResult.SpawnAndDestroy}.\n" +
            $"Mouvement : tâches={_runtimeResult.MovementTasks}, téléport/vélocité={_runtimeResult.TeleportAndVelocity}, annulation={_runtimeResult.MovementCancelled}.\n" +
            $"Utilitaires : déterministes={_runtimeResult.UtilitiesDeterministic}, Unicode/regex={_runtimeResult.UnicodeAndRegex}, locaux typés={_runtimeResult.TypedLocals}.\n" +
            $"Sandbox : interdit bloqué={_runtimeResult.ForbiddenCapabilityBlocked}, quota={_runtimeResult.QuotaInterrupted}, récupération={_runtimeResult.RuntimeRecovered}.\n" +
            $"Commande : dry-run={_runtimeResult.CanonicalDryRun}, undo/redo={_runtimeResult.CommandApplyUndoRedo}, stale={_runtimeResult.StaleRevisionBlocked}.");
        RenderDiagnostics(_runtimeResult.Diagnostics);
        _console.Text = HumanValue("CONSOLE R013 · " + string.Join(" · ", _runtimeResult.Events.Take(6)));
        SetStatus(_runtimeResult.Message, !_runtimeResult.Succeeded);
        UpdateButtons();
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null || !WorldMovementReady || !ReviewedSourceReady)
        {
            SetStatus("Insérez le bloc revu et réussissez d’abord la simulation R013.", true);
            return;
        }
        _previewResult = _session.PreviewScriptSourceUx6F(draft);
        RenderDiagnostics(_previewResult.Diagnostics);
        _console.Text = HumanValue(
            $"APERÇU SEC · {_previewResult.Message}\n{_previewResult.SemanticDiff}\n" +
            "Même ForgeScriptSourceHistory UX6-F · aucune écriture avant Appliquer.");
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
        UpdateButtons();
    }

    private void Compile()
    {
        var draft = ReadDraft();
        if (draft is null) return;
        _compileResult = _session.CompileScriptSourceUx6F(draft);
        RenderDiagnostics(_compileResult.Diagnostics);
        _console.Text = HumanValue(
            $"COMPILATEUR · {_compileResult.Message}\nVM {_compileResult.VirtualMachineVersion} · " +
            $"API {_compileResult.ApiVersion} · {_compileResult.BytecodeBytes:N0} octets.");
        SetStatus(_compileResult.Message, !_compileResult.Succeeded);
        UpdateButtons();
    }

    private void RunSandbox()
    {
        var draft = ReadDraft();
        if (draft is null) return;
        _sandboxResult = _session.RunScriptSandboxUx6F(draft);
        RenderDiagnostics(_sandboxResult.Diagnostics);
        _console.Text = HumanValue(
            $"SANDBOX · {_sandboxResult.Message}\nStatut {_sandboxResult.Status} · " +
            $"{_sandboxResult.Instructions:N0} instructions · {_sandboxResult.PeakMemoryBytes:N0} octets.");
        SetStatus(_sandboxResult.Message, !_sandboxResult.Succeeded);
        UpdateButtons();
    }

    private void Apply()
    {
        var draft = ReadDraft();
        if (draft is null) return;
        var result = _session.ApplyApprovedScriptSourceUx6F(draft);
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        _draft = _session.CurrentScriptDraftUx6F();
        Invalidate();
        ScriptSourceChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.UndoScriptSourceUx6F();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) ReloadActiveDraft();
    }

    private void Redo()
    {
        var result = _session.RedoScriptSourceUx6F();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) ReloadActiveDraft();
    }

    private void ReloadActiveDraft()
    {
        _draft = _session.CurrentScriptDraftUx6F();
        if (_draft is null) return;
        _updating = true;
        _source.Text = _draft.Source;
        _updating = false;
        Invalidate();
        ScriptSourceChanged?.Invoke();
    }

    private void HookWorldControls()
    {
        foreach (var option in new[] { _subjectTag, _targetTag, _blueprint, _movementMode })
            option.ItemSelected += _ => InvalidateWorld();
        foreach (var number in new[]
                 {
                     _destinationX, _destinationY, _destinationZ, _velocityX, _velocityY, _velocityZ,
                     _spawnX, _spawnY, _spawnZ, _followDistance, _localInteger, _localNumber,
                     _floatPrecision,
                 })
            number.ValueChanged += _ => InvalidateWorld();
        foreach (var line in new[] { _localText, _regexValue, _regexPattern })
            line.TextChanged += _ => InvalidateWorld();
    }

    private void InvalidateSource()
    {
        if (_updating) return;
        Invalidate();
    }

    private void InvalidateWorld()
    {
        if (_updating) return;
        Invalidate();
    }

    private void Invalidate()
    {
        _session.CancelUx6FApproval();
        _sourcePlan = null;
        _runtimeResult = null;
        _previewResult = null;
        _compileResult = null;
        _sandboxResult = null;
        _sourceReview.Text = "Brouillon modifié · reconstruisez le bloc versionné.";
        _runtimeReview.Text = "Aucune simulation valide pour ce brouillon.";
        _console.Text = "CONSOLE · brouillon modifié · relancez construction, simulation et Aperçu sec.";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var selected = HasExplicitSelection;
        _buildSource.Disabled = !selected || !HumanCatalogsReady;
        _insertSource.Disabled = !selected || _sourcePlan is not { Succeeded: true };
        _simulate.Disabled = !selected || !HumanCatalogsReady || !ReviewedSourceReady;
        _preview.Disabled = !selected || !WorldMovementReady || !ReviewedSourceReady;
        _compile.Disabled = !selected;
        _sandbox.Disabled = !selected;
        _apply.Disabled = !selected || !PreviewReady || !WorldMovementReady;
        _undo.Disabled = !selected;
        _redo.Disabled = !selected;
    }

    private void ResetControls()
    {
        _updating = true;
        _sourceChoice.Clear();
        _sourceChoice.AddItem("Choisir explicitement une source…");
        _sourceChoice.Select(0);
        _source.Text = string.Empty;
        _newSourcePath.Text = "scripts/world-movement.as";
        _movementMode.Select(1);
        _destinationX.Value = 3;
        _destinationY.Value = 0;
        _destinationZ.Value = 4;
        _velocityX.Value = 0;
        _velocityY.Value = 0;
        _velocityZ.Value = 3;
        _spawnX.Value = 2;
        _spawnY.Value = 0;
        _spawnZ.Value = 1;
        _followDistance.Value = 2;
        _localInteger.Value = 2;
        _localNumber.Value = 5;
        _localText.Text = "Gardienne";
        _regexValue.Text = "ash-guard";
        _regexPattern.Text = "^[a-z-]+$";
        _floatPrecision.Value = 2;
        _updating = false;
        _choices = [];
        _catalog = new([], []);
        _selectedArea = null;
        _draft = null;
        _explicitSource = false;
        _explicitArea = false;
        _sourcePlan = null;
        _runtimeResult = null;
        _previewResult = null;
        _compileResult = null;
        _sandboxResult = null;
        _catalogStatus.Text = "Les sélecteurs utilisent seulement les références du projet ouvert.";
        _sourceReview.Text = "Aucun bloc généré.";
        _runtimeReview.Text = "Aucune simulation. Le projet et le moteur restent inchangés.";
        _console.Text = "CONSOLE · aucune opération exécutée.";
        ClearChildren(_diagnosticRows);
        UpdateButtons();
    }

    private void RenderDiagnostics(IReadOnlyList<StudioUx6FDiagnostic> diagnostics)
    {
        ClearChildren(_diagnosticRows);
        if (diagnostics.Count == 0)
        {
            _diagnosticRows.AddChild(StudioStyle.Label("✓ Aucun diagnostic bloquant.", 9,
                StudioStyle.Success));
            return;
        }
        foreach (var diagnostic in diagnostics.Take(8))
        {
            var text = HumanValue($"{diagnostic.Code} · {diagnostic.Path} · {diagnostic.Message}");
            var button = StudioStyle.Button(text, compact: true);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.Alignment = HorizontalAlignment.Left;
            button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            button.TooltipText = text;
            button.Pressed += () => FocusDiagnostic(diagnostic);
            _diagnosticRows.AddChild(button);
        }
    }

    private void FocusDiagnostic(StudioUx6FDiagnostic diagnostic)
    {
        if (diagnostic.Line > 0)
        {
            _source.SetCaretLine(Math.Clamp(diagnostic.Line - 1, 0,
                Math.Max(0, _source.GetLineCount() - 1)));
            _source.SetCaretColumn(Math.Max(0, diagnostic.Column - 1));
            _source.GrabFocus();
        }
        SetStatus($"Diagnostic {diagnostic.Code} · {diagnostic.Path}.", true);
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
        try
        {
            isolated?.Close();
        }
        catch (Exception exception)
        {
            sessionReleased = false;
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX6_I_RELEASE_FAILED type={exception.GetType().Name}");
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
        var width = Math.Clamp(viewport.X - 44, 1480, 1880);
        var height = Math.Clamp(viewport.Y - 44, 760, 1030);
        _dialog.Position = new Vector2((viewport.X - width) / 2, (viewport.Y - height) / 2);
        _dialog.Size = new Vector2(width, height);
    }

    private static VBoxContainer PanelColumn(HBoxContainer host, int minimum, bool expand = false)
    {
        var panel = StudioStyle.Panel("Ux6IColumn", StudioStyle.SurfaceRaised, 8, 1);
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
            CustomMinimumSize = new Vector2(680, 0),
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
        row.AddChild(Field(leftLabel, left, 315));
        row.AddChild(Field(rightLabel, right, 315));
        return row;
    }

    private static HBoxContainer VectorRow(string label, SpinBox x, SpinBox y, SpinBox z)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var value in new[] { x, y, z })
        {
            value.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(value);
        }
        box.AddChild(row);
        var wrapper = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        wrapper.AddChild(box);
        return wrapper;
    }

    private static SpinBox Number(double minimum, double maximum, double step, double value)
    {
        var number = StudioUx2EnvBControls.Number(minimum, maximum, step);
        number.Value = value;
        number.CustomArrowStep = step == 0 ? .25 : step;
        return number;
    }

    private static SpinBox ExactNumber(double value, double minimum = -1_000_000,
        double maximum = 1_000_000) => Number(minimum, maximum, 0, value);

    private static void PopulateOptions(OptionButton option, IReadOnlyList<StudioUx6IChoice> choices,
        string empty, int preferred = 0)
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

    private static string? KeyAt(IReadOnlyList<StudioUx6IChoice> choices, OptionButton option) =>
        choices.Count == 0 || option.Selected < 0 || option.Selected >= choices.Count
            ? null
            : choices[option.Selected].Key;

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren()) child.QueueFree();
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
