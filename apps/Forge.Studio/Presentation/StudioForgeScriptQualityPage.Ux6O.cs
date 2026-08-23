using System.Globalization;
using System.Text.RegularExpressions;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioForgeScriptQualityPageUx6O : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx6OSourceSnapshot? _sourceSnapshot;
    private string? _evidenceRoot;
    private byte[]? _evidenceSourceBytes;
    private byte[]? _evidenceMetadataBytes;
    private bool _evidenceReleased = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _sourceChoice;
    private readonly Label _selection;
    private readonly CodeEdit _source;
    private readonly TabContainer _tabs;
    private readonly LineEdit _seed;
    private readonly LineEdit _maximumCases;
    private readonly LineEdit _instructionThreshold;
    private readonly LineEdit _allocationThreshold;
    private readonly LineEdit _cpuThreshold;
    private readonly LineEdit _documentationPath;
    private readonly Dictionary<string, CheckBox> _availability = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CheckBox> _rules = new(StringComparer.Ordinal);
    private readonly CheckBox _warningsAsErrors;
    private readonly LineEdit _maximumStateBytes;
    private readonly CodeEdit _components;
    private readonly CheckBox _requiredAtRuntime;
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
    private StudioUx6OQualityDraft? _qualityDraft;
    private StudioUx6OPreview? _previewResult;
    private StudioUx6OSimulation? _simulationResult;
    private bool _explicitSource;
    private bool _updating;

    internal event Action<string, bool>? StatusChanged;

    internal bool IsOpen => Visible;
    internal bool HasExplicitSelection => _explicitSource && _sourceDraft is not null && _qualityDraft is not null;
    internal bool PreviewReady => _previewResult is { Succeeded: true, CanApply: true, CanonicalDryRun: true };
    internal bool SimulationReady => _simulationResult is
    {
        Succeeded: true,
        UnitTests: true,
        DeterministicReport: true,
        Profiler: true,
        Documentation: true,
        StaticAnalysis: true,
        StateRoundTrip: true,
        Adapter: true,
        NativeSandbox: true,
    };
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                           !ReferenceEquals(_session, _sourceSession);
    internal bool EvidenceSourceUntouched => SourceMatches() && EvidenceScriptMatches();
    internal bool EvidenceResourcesReleased => _evidenceReleased && _evidenceSession is null &&
                                                _evidenceRoot is null;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool HumanControlsReady => _availability.Count == 3 && _rules.Count == 5 &&
                                         _availability.Keys.All(value => value is "server" or "client" or "rpg-kit") &&
                                         _rules.Keys.All(ForgeScriptQualityProfileValidator.SupportedRules.Contains);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantText(this));
            return text.Contains("Tests unitaires ForgeScript", StringComparison.Ordinal) &&
                   text.Contains("Profilage de scripts", StringComparison.Ordinal) &&
                   text.Contains("Documentation générée", StringComparison.Ordinal) &&
                   text.Contains("Analyse statique", StringComparison.Ordinal) &&
                   text.Contains("Sérialisation d’état script", StringComparison.Ordinal) &&
                   text.Contains("Aperçu sec canonique", StringComparison.Ordinal) &&
                   !ContainsUuid(text);
        }
    }

    internal StudioForgeScriptQualityPageUx6O(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color(.008f, .014f, .023f, .985f);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux6OQuality", StudioStyle.Surface, 12, 1);
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
        headingText.AddChild(StudioStyle.Label("ForgeScript · Qualité R100", 19));
        headingText.AddChild(StudioStyle.Label(
            "Tests · Profilage · Documentation · Analyse statique · État persistant · Aperçu sec canonique · source UX6-F inchangée.",
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
        var reload = ActionButton("Recharger source et profil", Reload, 210);
        sourceRow.AddChild(reload);
        root.AddChild(sourceRow);
        _selection = StudioStyle.Label(
            "Choisissez une source .as canonique. Le profil R100 reste un document séparé et transactionnel.",
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
        right.AddChild(StudioStyle.Label("PROFIL QUALITÉ VERSIONNÉ · CINQ OUTILS R100", 10,
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

        var testsPage = TabPage(_tabs, "Tests");
        testsPage.AddChild(StudioStyle.Label("Tests unitaires ForgeScript", 12, StudioStyle.Accent));
        _seed = StudioUx2EnvBControls.Text("2026");
        _maximumCases = StudioUx2EnvBControls.Text("128");
        testsPage.AddChild(Pair("Seed déterministe non nul", _seed, "Nombre maximal de cas", _maximumCases));
        testsPage.AddChild(StudioStyle.Label(
            "TestWorld utilise fixtures, services simulés, assertions et rapport CI. Aucun monde de jeu live n’est démarré.",
            9, StudioStyle.Muted, wrap: true));

        var profilerPage = TabPage(_tabs, "Profilage");
        profilerPage.AddChild(StudioStyle.Label("Profilage de scripts", 12, StudioStyle.Accent));
        _instructionThreshold = StudioUx2EnvBControls.Text("10000");
        _allocationThreshold = StudioUx2EnvBControls.Text("8388608");
        _cpuThreshold = StudioUx2EnvBControls.Text("100");
        profilerPage.AddChild(Pair("Seuil d’instructions / handler", _instructionThreshold,
            "Seuil d’allocations (octets)", _allocationThreshold));
        profilerPage.AddChild(Field("Seuil CPU par handler (ms)", _cpuThreshold, 320));
        profilerPage.AddChild(StudioStyle.Label(
            "La prévisualisation agrège des échantillons déterministes, hotspots, graphe d’appels et alertes ; elle ne profile aucun processus distant.",
            9, StudioStyle.Muted, wrap: true));

        var documentationPage = TabPage(_tabs, "Documentation");
        documentationPage.AddChild(StudioStyle.Label("Documentation générée", 12, StudioStyle.Accent));
        _documentationPath = StudioUx2EnvBControls.Text("docs/generated/forgescript-api.md");
        documentationPage.AddChild(Field("Chemin relatif gouverné sous docs/generated", _documentationPath, 580));
        var availabilityRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddAvailability(availabilityRow, "server", "Serveur");
        AddAvailability(availabilityRow, "client", "Client");
        AddAvailability(availabilityRow, "rpg-kit", "RPG Kit");
        documentationPage.AddChild(availabilityRow);
        documentationPage.AddChild(StudioStyle.Label(
            "La référence est produite en mémoire depuis le catalogue versionné et expose ses ressources MCP ; aucun fichier généré n’est écrit par l’aperçu.",
            9, StudioStyle.Muted, wrap: true));

        var staticPage = TabPage(_tabs, "Analyse");
        staticPage.AddChild(StudioStyle.Label("Analyse statique", 12, StudioStyle.Accent));
        var ruleGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var rule in ForgeScriptQualityProfileValidator.SupportedRules)
        {
            var check = new CheckBox { Text = HumanRule(rule) };
            _rules[rule] = check;
            ruleGrid.AddChild(check);
        }
        staticPage.AddChild(ruleGrid);
        _warningsAsErrors = new CheckBox { Text = "Traiter les avertissements comme des erreurs" };
        staticPage.AddChild(_warningsAsErrors);
        staticPage.AddChild(StudioStyle.Label(
            "Les diagnostics conservent code et ligne, puis présentent une suggestion humaine sans masquer le chemin canonique interne.",
            9, StudioStyle.Muted, wrap: true));

        var statePage = TabPage(_tabs, "État");
        statePage.AddChild(StudioStyle.Label("Sérialisation d’état script", 12, StudioStyle.Accent));
        _maximumStateBytes = StudioUx2EnvBControls.Text("1048576");
        statePage.AddChild(Field("Quota maximal de l’enveloppe (octets)", _maximumStateBytes, 360));
        _components = new CodeEdit
        {
            CustomMinimumSize = new Vector2(0, 100),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "quest=1\nscore=1",
            GuttersDrawLineNumbers = true,
        };
        statePage.AddChild(Field("Composants enregistrés · un nom=version par ligne", _components, 520));
        _requiredAtRuntime = new CheckBox { Text = "Extension qualité obligatoire au runtime" };
        statePage.AddChild(_requiredAtRuntime);
        statePage.AddChild(StudioStyle.Label(
            "Seuls les composants enregistrés sont sérialisés. Pointeurs VM, handles opaques et objets moteur restent exclus.",
            9, StudioStyle.Muted, wrap: true));

        var review = StudioStyle.Panel("Ux6OReview", StudioStyle.SurfaceRaised, 8, 1);
        review.CustomMinimumSize = new Vector2(0, 135);
        right.AddChild(review);
        var reviewMargin = StudioStyle.Margin(9, 8, 9, 8);
        review.AddChild(reviewMargin);
        var reviewRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reviewMargin.AddChild(reviewRows);
        _previewReview = StudioStyle.Label("Aucun aperçu sec canonique.", 9, StudioStyle.Muted, wrap: true);
        _simulationReview = StudioStyle.Label("Aucune simulation qualité sur copie contrôlée.", 9,
            StudioStyle.Muted, wrap: true);
        _results = new CodeEdit
        {
            Editable = false,
            CustomMinimumSize = new Vector2(0, 64),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ScrollFitContentHeight = true,
        };
        _results.Text = "Console R100 prête.";
        reviewRows.AddChild(_previewReview);
        reviewRows.AddChild(_simulationReview);
        reviewRows.AddChild(_results);

        _status = StudioStyle.Label("Qualité ForgeScript prête.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 8);
        _preview = ActionButton("Prévisualiser", Preview, 128);
        _simulate = ActionButton("Exercer les cinq outils", Simulate, 190);
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
        if (Visible) _session.CancelUx6OApproval();
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
        if (!mode.StartsWith("ux6-o", StringComparison.OrdinalIgnoreCase))
            return FailEvidence("Mode de preuve UX6-O inconnu.");
        ReleaseEvidence();
        _sourceSnapshot = CaptureSource();
        if (_sourceSnapshot is null || _sourceSession.Project is null)
            return FailEvidence("Le module source n’est pas ouvert.");
        _evidenceRoot = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux6-o-ui",
            Guid.NewGuid().ToString("N"));
        if (!ProjectSession.Create(_evidenceRoot, _sourceSession.Project).Succeeded)
            return FailEvidence("Impossible de créer la copie temporaire R100.");
        var document = StudioSessionController.EvidenceDocumentUx6F();
        new ForgeScriptSourceStore(_evidenceRoot).Save(document);
        var sourcePath = Path.Combine(_evidenceRoot,
            document.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        _evidenceSourceBytes = File.ReadAllBytes(sourcePath);
        _evidenceMetadataBytes = File.ReadAllBytes(sourcePath + ".meta.json");
        _evidenceSession = new StudioSessionController();
        if (!_evidenceSession.Open(_evidenceRoot).Succeeded)
            return FailEvidence("Impossible d’ouvrir la copie temporaire R100.");
        _session = _evidenceSession;
        OpenBound(_session, evidence: true);
        if (_sourceChoice.ItemCount < 2) return FailEvidence("La copie temporaire ne fournit aucune source canonique.");
        _sourceChoice.Select(1);
        SelectSource(1);
        if (_qualityDraft is null) return FailEvidence("Le profil R100 ne peut pas être construit.");
        _maximumCases.Text = (_qualityDraft.MaximumCases + 1).ToString(CultureInfo.InvariantCulture);
        _instructionThreshold.Text = (_qualityDraft.InstructionThreshold + 100).ToString(CultureInfo.InvariantCulture);
        _warningsAsErrors.ButtonPressed = true;
        _maximumStateBytes.Text = (_qualityDraft.MaximumStateBytes + 1024).ToString(CultureInfo.InvariantCulture);
        foreach (var check in _availability.Values) check.ButtonPressed = true;
        Invalidate();
        Preview();
        Simulate();
        _tabs.CurrentTab = mode.Contains("profile", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("documentation", StringComparison.OrdinalIgnoreCase) ? 2 :
            mode.Contains("analysis", StringComparison.OrdinalIgnoreCase) ? 3 :
            mode.Contains("state", StringComparison.OrdinalIgnoreCase) ? 4 : 0;
        var isolated = UsesIsolatedEvidence;
        var explicitSelection = HasExplicitSelection;
        var controls = HumanControlsReady;
        var preview = PreviewReady;
        var simulation = SimulationReady;
        var human = ShowsHumanSurface;
        var sourceUntouched = EvidenceSourceUntouched;
        var ready = isolated && explicitSelection && controls && preview && simulation && human && sourceUntouched;
        if (!ready)
            GD.PrintErr("OPENRPG_FORGE_STUDIO_UX6_O_UI_FAILED " +
                        $"isolated={isolated.ToString().ToLowerInvariant()} " +
                        $"explicit={explicitSelection.ToString().ToLowerInvariant()} " +
                        $"controls={controls.ToString().ToLowerInvariant()} " +
                        $"preview={preview.ToString().ToLowerInvariant()} " +
                        $"simulation={simulation.ToString().ToLowerInvariant()} " +
                        $"human={human.ToString().ToLowerInvariant()} " +
                        $"source={sourceUntouched.ToString().ToLowerInvariant()}");
        return ready || FailEvidence("La surface R100 n’a pas satisfait tous les invariants humains.");
    }

    internal bool RunUiTransactionEvidence()
    {
        var ready = PrepareEvidence("ux6-o-script-quality");
        try
        {
            if (!ready || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
                !PreviewReady || !SimulationReady || _sourceDraft is null)
                return false;
            var draft = ReadDraft();
            if (draft is null) return false;
            var store = new ForgeScriptQualityStore(_evidenceRoot!);
            var applied = _session.ApplyApprovedScriptQualityUx6O(draft, _sourceDraft);
            var appliedProfile = store.Load();
            var undone = _session.UndoScriptQualityUx6O();
            var undoProfile = store.Load();
            var redone = _session.RedoScriptQualityUx6O();
            var redoProfile = store.Load();
            var saved = _session.SaveScriptQualityUx6O();
            var reopened = new StudioSessionController();
            var persisted = reopened.Open(_evidenceRoot!).Succeeded &&
                            reopened.TryOpenCanonicalSourceUx6F(_sourceDraft.RelativePath, out var reopenedSource,
                                out _) && reopenedSource is not null &&
                            reopened.CurrentScriptQualityUx6O(reopenedSource) is { } reopenedDraft &&
                            DraftContentsEqual(reopenedDraft, draft) && reopened.Project is not null &&
                            _sourceSnapshot is not null &&
                            string.Equals(ProjectJsonSerializer.Serialize(reopened.Project),
                                ProjectJsonSerializer.Serialize(_sourceSnapshot.Project), StringComparison.Ordinal);
            reopened.Close();
            return applied.Succeeded && undone.Succeeded && redone.Succeeded && saved.Succeeded && persisted &&
                   ProfileContentsEqual(appliedProfile, draft) && !ProfileContentsEqual(undoProfile, draft) &&
                   ProfileContentsEqual(redoProfile, draft) && EvidenceSourceUntouched;
        }
        finally
        {
            CloseEvidence();
        }
    }

    private void OpenBound(StudioSessionController session, bool evidence, bool cancelApproval = true)
    {
        _session = session;
        if (cancelApproval) _session.CancelUx6OApproval();
        Visible = true;
        ResetControls();
        RefreshSources();
        _selection.Text = evidence
            ? "COPIE TEMPORAIRE DU MODULE RÉEL · projet, historique, source et approbations strictement inchangés."
            : "Choisissez une source canonique. Le profil qualité est séparé et aucun UUID n’est affiché.";
        UpdateBounds();
    }

    private void RefreshSources()
    {
        _sourceChoices = _session.ScriptSourceChoicesUx6F()
            .Where(choice => choice.Kind == StudioUx6FSourceKind.Canonical)
            .ToArray();
        _updating = true;
        _sourceChoice.Clear();
        _sourceChoice.AddItem("Choisir une source canonique…", 0);
        foreach (var choice in _sourceChoices)
            _sourceChoice.AddItem(ProtectTechnicalIdentifiers($"{choice.Label} · {choice.Detail}"));
        _sourceChoice.Select(0);
        _updating = false;
        _sourceDraft = null;
        _qualityDraft = null;
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
        if (!_session.TryOpenCanonicalSourceUx6F(choice.RelativePath, out var source, out var message) || source is null)
        {
            SetStatus(message, error: true);
            return;
        }
        var quality = _session.CurrentScriptQualityUx6O(source);
        if (quality is null)
        {
            SetStatus("Le profil qualité R100 est absent ou invalide.", error: true);
            return;
        }
        _sourceDraft = source;
        _qualityDraft = quality;
        _source.Text = source.Source;
        _explicitSource = true;
        Populate(quality);
        SetStatus($"Source revue · {ProtectTechnicalIdentifiers(choice.Label)}.");
    }

    private void Reload()
    {
        RefreshSources();
        SetStatus("Source et profil qualité R100 rechargés.");
    }

    private void Populate(StudioUx6OQualityDraft draft)
    {
        _updating = true;
        _seed.Text = draft.DeterministicSeed.ToString(CultureInfo.InvariantCulture);
        _maximumCases.Text = draft.MaximumCases.ToString(CultureInfo.InvariantCulture);
        _instructionThreshold.Text = draft.InstructionThreshold.ToString(CultureInfo.InvariantCulture);
        _allocationThreshold.Text = draft.AllocationThresholdBytes.ToString(CultureInfo.InvariantCulture);
        _cpuThreshold.Text = draft.CpuThresholdMilliseconds.ToString(CultureInfo.InvariantCulture);
        _documentationPath.Text = draft.DocumentationPath;
        foreach (var item in _availability) item.Value.ButtonPressed = draft.DocumentationAvailability.Contains(item.Key);
        foreach (var item in _rules) item.Value.ButtonPressed = draft.EnabledRules.Contains(item.Key);
        _warningsAsErrors.ButtonPressed = draft.TreatWarningsAsErrors;
        _maximumStateBytes.Text = draft.MaximumStateBytes.ToString(CultureInfo.InvariantCulture);
        _components.Text = string.Join(System.Environment.NewLine,
            draft.RegisteredComponents.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => $"{item.Key}={item.Value}"));
        _requiredAtRuntime.ButtonPressed = draft.RequiredAtRuntime;
        _updating = false;
        Invalidate();
    }

    private StudioUx6OQualityDraft? ReadDraft()
    {
        if (_qualityDraft is null || _sourceDraft is null)
        {
            SetStatus("Sélectionnez une source canonique.", error: true);
            return null;
        }
        if (!int.TryParse(_seed.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed) ||
            !int.TryParse(_maximumCases.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var maximumCases) ||
            !long.TryParse(_instructionThreshold.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var instructionThreshold) ||
            !long.TryParse(_allocationThreshold.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var allocationThreshold) ||
            !int.TryParse(_cpuThreshold.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var cpuThreshold) ||
            !int.TryParse(_maximumStateBytes.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var maximumStateBytes))
        {
            SetStatus("Les seuils doivent être des nombres entiers exacts.", error: true);
            return null;
        }
        var components = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in _components.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var pair = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || string.IsNullOrWhiteSpace(pair[0]) ||
                !int.TryParse(pair[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var version) ||
                !components.TryAdd(pair[0], version))
            {
                SetStatus("Chaque composant doit utiliser une ligne unique nom=version.", error: true);
                return null;
            }
        }
        return _qualityDraft with
        {
            DeterministicSeed = seed,
            MaximumCases = maximumCases,
            InstructionThreshold = instructionThreshold,
            AllocationThresholdBytes = allocationThreshold,
            CpuThresholdMilliseconds = cpuThreshold,
            DocumentationPath = _documentationPath.Text.Trim(),
            DocumentationAvailability = Selected(_availability),
            EnabledRules = Selected(_rules),
            TreatWarningsAsErrors = _warningsAsErrors.ButtonPressed,
            MaximumStateBytes = maximumStateBytes,
            RegisteredComponents = components,
            RequiredAtRuntime = _requiredAtRuntime.ButtonPressed,
        };
    }

    private void Preview()
    {
        if (_sourceDraft is null || ReadDraft() is not { } draft) return;
        _previewResult = _session.PreviewScriptQualityUx6O(draft, _sourceDraft);
        _previewReview.Text = ProtectTechnicalIdentifiers(
            $"{_previewResult.Message} · {HumanSemanticDiff(_previewResult.SemanticDiff)}");
        RenderDiagnostics(_previewResult.Diagnostics);
        SetStatus(_previewResult.Message, error: !_previewResult.Succeeded);
        UpdateButtons();
    }

    private void Simulate()
    {
        if (_sourceDraft is null || ReadDraft() is not { } draft) return;
        _simulationResult = _session.SimulateScriptQualityUx6O(draft, _sourceDraft);
        _simulationReview.Text = ProtectTechnicalIdentifiers(_simulationResult.Message);
        _results.Text = string.Join(System.Environment.NewLine, _simulationResult.Results);
        RenderDiagnostics(_simulationResult.Diagnostics);
        SetStatus(_simulationResult.Message, error: !_simulationResult.Succeeded);
        UpdateButtons();
    }

    private void Apply()
    {
        if (_sourceDraft is null || ReadDraft() is not { } draft) return;
        var result = _session.ApplyApprovedScriptQualityUx6O(draft, _sourceDraft);
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded) ReloadCurrent();
        UpdateButtons();
    }

    private void Undo()
    {
        var result = _session.UndoScriptQualityUx6O();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded) ReloadCurrent();
        UpdateButtons();
    }

    private void Redo()
    {
        var result = _session.RedoScriptQualityUx6O();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded) ReloadCurrent();
        UpdateButtons();
    }

    private void Save()
    {
        var result = _session.SaveScriptQualityUx6O();
        SetStatus(result.Message, error: !result.Succeeded);
        UpdateButtons();
    }

    private void ReloadCurrent()
    {
        if (_sourceDraft is null) return;
        _qualityDraft = _session.CurrentScriptQualityUx6O(_sourceDraft);
        if (_qualityDraft is not null) Populate(_qualityDraft);
    }

    private void HookEditors()
    {
        foreach (var edit in new[]
                 {
                     _seed, _maximumCases, _instructionThreshold, _allocationThreshold, _cpuThreshold,
                     _documentationPath, _maximumStateBytes,
                 })
            edit.TextChanged += _ => Invalidate();
        _components.TextChanged += Invalidate;
        foreach (var check in _availability.Values.Concat(_rules.Values)) check.Toggled += _ => Invalidate();
        _warningsAsErrors.Toggled += _ => Invalidate();
        _requiredAtRuntime.Toggled += _ => Invalidate();
    }

    private void Invalidate()
    {
        if (_updating) return;
        _session.CancelUx6OApproval();
        _previewResult = null;
        _simulationResult = null;
        _previewReview.Text = "Aucun aperçu sec canonique.";
        _simulationReview.Text = "Aucune simulation qualité sur copie contrôlée.";
        _results.Text = "Console R100 prête.";
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
        _save.Disabled = !selected || !_session.ScriptQualityHasUnsavedChangesUx6O;
    }

    private void ResetControls()
    {
        _updating = true;
        _sourceChoice.Clear();
        _source.Text = string.Empty;
        _components.Text = string.Empty;
        _results.Text = "Console R100 prête.";
        _sourceChoices = [];
        _sourceDraft = null;
        _qualityDraft = null;
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
            var row = StudioStyle.Panel("Ux6ODiagnostic", StudioStyle.SurfaceRaised, 6, 1);
            var margin = StudioStyle.Margin(7, 5, 7, 5);
            row.AddChild(margin);
            margin.AddChild(StudioStyle.Label(
                $"{HumanDiagnostic(diagnostic.Code)} · {HumanPath(diagnostic.Path)}" +
                $"{(diagnostic.Line > 0 ? $" · ligne {diagnostic.Line}" : string.Empty)} · " +
                ProtectTechnicalIdentifiers(HumanDiagnosticMessage(diagnostic)),
                8, diagnostic.Severity == "error" ? StudioStyle.Danger : StudioStyle.Warning, wrap: true));
            _diagnostics.AddChild(row);
        }
    }

    private StudioUx6OSourceSnapshot? CaptureSource()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        var session = _sourceSession.Session;
        if (project is null || history is null || session is null) return null;
        var qualityPath = Path.Combine(session.RootDirectory, ForgeScriptQualityStore.RelativePath);
        var existed = File.Exists(qualityPath);
        return new(project, history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.CaptureScriptApprovalUx6F(),
            _sourceSession.CaptureExecutionPolicyApprovalUx6J(),
            _sourceSession.CaptureModulePermissionApprovalUx6K(),
            _sourceSession.CaptureScriptAutomationApprovalUx6M(),
            _sourceSession.CaptureScriptQualityApprovalUx6O(), existed,
            existed ? File.ReadAllBytes(qualityPath) : null);
    }

    private bool SourceMatches()
    {
        var snapshot = _sourceSnapshot;
        var history = _sourceSession.History;
        var session = _sourceSession.Session;
        if (snapshot is null || history is null || session is null) return false;
        var qualityPath = Path.Combine(session.RootDirectory, ForgeScriptQualityStore.RelativePath);
        var existed = File.Exists(qualityPath);
        var bytes = existed ? File.ReadAllBytes(qualityPath) : null;
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
               existed == snapshot.QualityFileExisted && BytesEqual(bytes, snapshot.QualityFileBytes);
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

    private void AddAvailability(Control host, string id, string label)
    {
        var check = new CheckBox { Text = label };
        _availability[id] = check;
        host.AddChild(check);
    }

    private static VBoxContainer PanelColumn(HBoxContainer host, int minimum, float stretchRatio)
    {
        var panel = StudioStyle.Panel("Ux6OColumn", StudioStyle.SurfaceRaised, 8, 1);
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

    private static HashSet<string> Selected(IReadOnlyDictionary<string, CheckBox> controls) =>
        controls.Where(item => item.Value.ButtonPressed).Select(item => item.Key)
            .ToHashSet(StringComparer.Ordinal);

    private static bool DraftContentsEqual(StudioUx6OQualityDraft left, StudioUx6OQualityDraft right) =>
        left.Id == right.Id && left.SchemaVersion == right.SchemaVersion && left.Provenance == right.Provenance &&
        left.ApiVersion == right.ApiVersion && left.EventBinding == right.EventBinding &&
        left.RequiredAtRuntime == right.RequiredAtRuntime && left.UnitTestsId == right.UnitTestsId &&
        left.DeterministicSeed == right.DeterministicSeed && left.MaximumCases == right.MaximumCases &&
        left.ProfilerId == right.ProfilerId && left.InstructionThreshold == right.InstructionThreshold &&
        left.AllocationThresholdBytes == right.AllocationThresholdBytes &&
        left.CpuThresholdMilliseconds == right.CpuThresholdMilliseconds &&
        left.DocumentationId == right.DocumentationId && left.DocumentationPath == right.DocumentationPath &&
        left.DocumentationAvailability.SetEquals(right.DocumentationAvailability) &&
        left.StaticAnalysisId == right.StaticAnalysisId && left.EnabledRules.SetEquals(right.EnabledRules) &&
        left.TreatWarningsAsErrors == right.TreatWarningsAsErrors && left.StateId == right.StateId &&
        left.EnvelopeSchemaVersion == right.EnvelopeSchemaVersion && left.MaximumStateBytes == right.MaximumStateBytes &&
        DictionaryEqual(left.RegisteredComponents, right.RegisteredComponents) &&
        left.ScriptSourceId == right.ScriptSourceId && left.ScriptRelativePath == right.ScriptRelativePath;

    private static bool ProfileContentsEqual(ForgeScriptQualityProfile profile, StudioUx6OQualityDraft draft) =>
        profile.Id == draft.Id && profile.SchemaVersion == draft.SchemaVersion &&
        profile.Provenance == draft.Provenance && profile.ApiVersion == draft.ApiVersion &&
        profile.EventBinding == draft.EventBinding && profile.RequiredAtRuntime == draft.RequiredAtRuntime &&
        profile.UnitTests.Id == draft.UnitTestsId &&
        profile.UnitTests.DeterministicSeed == draft.DeterministicSeed &&
        profile.UnitTests.MaximumCases == draft.MaximumCases && profile.Profiler.Id == draft.ProfilerId &&
        profile.Profiler.InstructionThreshold == draft.InstructionThreshold &&
        profile.Profiler.AllocationThresholdBytes == draft.AllocationThresholdBytes &&
        profile.Profiler.CpuThresholdMilliseconds == draft.CpuThresholdMilliseconds &&
        profile.Documentation.Id == draft.DocumentationId &&
        profile.Documentation.OutputRelativePath == draft.DocumentationPath &&
        profile.Documentation.Availability.ToHashSet(StringComparer.Ordinal)
            .SetEquals(draft.DocumentationAvailability) &&
        profile.StaticAnalysis.Id == draft.StaticAnalysisId &&
        profile.StaticAnalysis.EnabledRules.ToHashSet(StringComparer.Ordinal).SetEquals(draft.EnabledRules) &&
        profile.StaticAnalysis.TreatWarningsAsErrors == draft.TreatWarningsAsErrors &&
        profile.State.Id == draft.StateId && profile.State.EnvelopeSchemaVersion == draft.EnvelopeSchemaVersion &&
        profile.State.MaximumBytes == draft.MaximumStateBytes &&
        DictionaryEqual(profile.State.RegisteredComponents, draft.RegisteredComponents);

    private static bool DictionaryEqual(IReadOnlyDictionary<string, int> left,
        IReadOnlyDictionary<string, int> right) =>
        left.Count == right.Count && left.All(item => right.TryGetValue(item.Key, out var value) && value == item.Value);

    private static bool BytesEqual(byte[]? left, byte[]? right) =>
        left is null ? right is null : right is not null && left.SequenceEqual(right);

    private static string HumanRule(string rule) => rule switch
    {
        "expensive-heartbeat" => "Heartbeat coûteux",
        "unsafe-query" => "Requête non bornée",
        "nullable-access" => "Accès nullable / handle invalide",
        "deprecated-api" => "API dépréciée",
        "authority-violation" => "Violation d’autorité",
        _ => rule,
    };

    private static string HumanDiagnostic(string code) => code switch
    {
        "script.quality.tests_budget_invalid" => "Budget de tests invalide",
        "script.quality.profiler_threshold_invalid" => "Seuil de profilage invalide",
        "script.quality.documentation_path_invalid" => "Chemin de documentation invalide",
        "script.quality.documentation_availability_invalid" => "Disponibilité documentaire invalide",
        "script.quality.static_rule_invalid" => "Règle d’analyse inconnue",
        "script.quality.state_budget_invalid" => "Quota d’état invalide",
        "script.quality.state_component_invalid" => "Composant persistant invalide",
        "script.quality.revision_conflict" => "Révision qualité périmée",
        "script.quality.source_changed" => "Profil qualité source modifié",
        "script.quality.script_changed" => "Source ForgeScript modifiée",
        "SCR-LINT-EXPENSIVE-HEARTBEAT" => "Heartbeat coûteux",
        "SCR-LINT-UNSAFE-QUERY" => "Requête non bornée",
        "SCR-LINT-NULL" => "Accès nullable",
        "SCR-LINT-DEPRECATED" => "API dépréciée",
        "SCR-LINT-AUTHORITY" => "Violation d’autorité",
        _ => code.Replace('.', ' '),
    };

    private static string HumanDiagnosticMessage(StudioUx6FDiagnostic diagnostic) => diagnostic.Code switch
    {
        "script.quality.tests_budget_invalid" =>
            "Le seed doit être non nul et la suite contenir entre 1 et 10 000 cas.",
        "script.quality.profiler_threshold_invalid" =>
            "Les seuils d’instructions, d’allocations et de temps CPU doivent être positifs.",
        "script.quality.documentation_path_invalid" =>
            "Choisissez un fichier Markdown relatif sous docs/generated, sans remontée de dossier.",
        "script.quality.documentation_availability_invalid" =>
            "Sélectionnez au moins Serveur, Client ou RPG Kit.",
        "script.quality.static_rule_invalid" =>
            "Sélectionnez au moins une règle issue du catalogue versionné.",
        "script.quality.state_budget_invalid" =>
            "Le quota d’état doit rester entre 1 Kio et 64 Mio avec une enveloppe v1.",
        "script.quality.state_component_invalid" =>
            "Chaque composant persistant exige un nom unique et une version positive.",
        "script.quality.revision_conflict" => "Rechargez le profil avant de poursuivre.",
        "script.quality.source_changed" => "Le profil qualité source a changé ; rechargez-le.",
        "script.quality.script_changed" => "La source ForgeScript a changé ; rechargez-la.",
        "SCR-LINT-EXPENSIVE-HEARTBEAT" =>
            "Une requête globale est exécutée dans un heartbeat ; mettez le résultat en cache ou utilisez un événement.",
        "SCR-LINT-UNSAFE-QUERY" =>
            "La requête n’est pas bornée ; indiquez une limite et un périmètre explicites.",
        "SCR-LINT-NULL" =>
            "La référence peut être absente ou invalide ; ajoutez un contrôle explicite avant usage.",
        "SCR-LINT-DEPRECATED" =>
            "Cette API est dépréciée ; utilisez la référence générée pour choisir son remplacement.",
        "SCR-LINT-AUTHORITY" =>
            "Cet appel exige l’autorité serveur ; déplacez-le dans un handler serveur.",
        _ => diagnostic.Message,
    };

    private static string HumanSemanticDiff(string diff)
    {
        if (string.Equals(diff, "no-change", StringComparison.Ordinal)) return "Aucun changement";
        var tests = Regex.Match(diff, @"(?:^|\|)tests:(?<from>\d+)->(?<to>\d+)(?:\||$)",
            RegexOptions.CultureInvariant);
        var state = Regex.Match(diff, @"(?:^|\|)state:(?<from>\d+)->(?<to>\d+)(?:\||$)",
            RegexOptions.CultureInvariant);
        var parts = new List<string>();
        if (tests.Success) parts.Add($"Tests max {tests.Groups["from"].Value} → {tests.Groups["to"].Value}");
        if (state.Success) parts.Add($"Quota état {state.Groups["from"].Value} → {state.Groups["to"].Value} octets");
        return parts.Count > 0 ? string.Join(" · ", parts) : "Profil qualité modifié";
    }

    private static string HumanPath(string path) => path switch
    {
        var value when value.Contains("unitTests", StringComparison.Ordinal) => "Tests unitaires",
        var value when value.Contains("profiler", StringComparison.Ordinal) => "Profilage",
        var value when value.Contains("documentation", StringComparison.Ordinal) => "Documentation générée",
        var value when value.Contains("staticAnalysis", StringComparison.Ordinal) => "Analyse statique",
        var value when value.Contains("state", StringComparison.Ordinal) => "État persistant",
        var value when value.Contains("source", StringComparison.Ordinal) => "Source sélectionnée",
        _ => "Profil qualité R100",
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
}
