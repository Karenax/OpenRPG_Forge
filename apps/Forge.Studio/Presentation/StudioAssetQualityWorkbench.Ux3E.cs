using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Application.Blueprints;
using Forge.Domain.Assets;
using Forge.Domain.Blueprints;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAssetQualityWorkbenchUx3E : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx3E? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _usesRealModuleEvidence;
    private bool _usesCanonicalFallbackEvidence;
    private bool _updating;
    private bool _explicitSelection;

    private readonly Label _context;
    private readonly OptionButton _blueprint;
    private readonly Label _selectionSummary;
    private readonly TabContainer _tabs;

    private readonly OptionButton _glb;
    private readonly OptionButton _texture;
    private readonly Button _measure;
    private readonly Label _measurement;
    private readonly LineEdit _targetMeters;
    private readonly LineEdit _targetTexel;
    private readonly LineEdit _scaleTolerance;
    private readonly LineEdit _texelTolerance;
    private readonly LineEdit _targetShader;
    private readonly LineEdit _targetEra;
    private readonly OptionButton _targetRealism;
    private readonly LineEdit _targetPalette;
    private readonly LineEdit _targetProvenance;
    private readonly LineEdit _assetMeters;
    private readonly LineEdit _assetTexel;
    private readonly LineEdit _assetShader;
    private readonly LineEdit _assetEra;
    private readonly OptionButton _assetRealism;
    private readonly LineEdit _assetPalette;
    private readonly LineEdit _assetProvenance;
    private readonly CheckBox _styleApproval;
    private readonly Button _stylePreviewButton;
    private readonly Button _styleApplyButton;
    private readonly Label _styleSummary;
    private readonly VBoxContainer _styleDiff;
    private readonly VBoxContainer _styleDiagnostics;

    private readonly LineEdit _triangles;
    private readonly LineEdit _materials;
    private readonly LineEdit _textures;
    private readonly LineEdit _memory;
    private readonly LineEdit _animations;
    private readonly LineEdit _lodLevels;
    private readonly LineEdit _collisions;
    private readonly LineEdit _budgetProvenance;
    private readonly CheckBox _runtimeRequired;
    private readonly OptionButton _platform;
    private readonly LineEdit _maximumTriangles;
    private readonly LineEdit _maximumMaterials;
    private readonly LineEdit _maximumTextures;
    private readonly LineEdit _maximumMemory;
    private readonly LineEdit _maximumAnimations;
    private readonly LineEdit _maximumLods;
    private readonly LineEdit _maximumCollisions;
    private readonly CheckBox _gateBuild;
    private readonly VBoxContainer _platformRows;
    private readonly CheckBox _budgetApproval;
    private readonly Button _budgetPreviewButton;
    private readonly Button _budgetApplyButton;
    private readonly Label _budgetSummary;
    private readonly VBoxContainer _budgetDiff;
    private readonly VBoxContainer _budgetDiagnostics;

    private readonly Label _status;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    private IReadOnlyList<StudioUx3EBlueprintChoice> _choices = [];
    private IReadOnlyList<StudioUx3EWorkspaceAssetChoice> _glbChoices = [];
    private IReadOnlyList<StudioUx3EWorkspaceAssetChoice> _textureChoices = [];
    private StudioUx3EBlueprintChoice? _selected;
    private StudioUx3EStyleDraft? _styleDraft;
    private StudioUx3EBudgetDraft? _budgetDraft;
    private StudioUx3EStylePreview? _stylePreview;
    private StudioUx3EBudgetPreview? _budgetPreview;
    private List<AssetPlatformBudget> _platforms = [];

    internal event Action<string, bool>? StatusChanged;
    internal event Action? ProjectChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleEvidence => _usesRealModuleEvidence;
    internal bool UsesCanonicalFallbackEvidence => _usesCanonicalFallbackEvidence;
    internal bool HasExplicitSelection => _explicitSelection && _selected is not null;
    internal bool StylePreviewReady => _stylePreview is
    { Succeeded: true, CanApply: true, DryRunSucceeded: true, BuildDiagnosticLocalized: true, RuntimeClaimsBounded: true };
    internal bool BudgetPreviewReady => _budgetPreview is
    { Succeeded: true, CanApply: true, DryRunSucceeded: true, BuildGateEvaluated: true, RuntimeClaimsBounded: true };
    internal bool EvidenceSourceUntouched => SourceMatchesUx3E();
    internal bool EvidenceResourcesReleased => _evidenceReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var labels = FindChildren("*", "Label", true, false).Cast<Label>()
                .Select(item => item.Text).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
            var buttons = FindChildren("*", "Button", true, false).Cast<Button>()
                .Select(item => item.Text).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
            var tabs = Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle).ToArray();
            var all = labels.Concat(buttons).Concat(tabs).ToArray();
            return all.Any(item => item.Contains("Compatibilité artistique", StringComparison.Ordinal)) &&
                   all.Any(item => item.Contains("Budget d’asset", StringComparison.Ordinal)) &&
                   all.Any(item => item.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase)) &&
                   all.All(item => !Regex.IsMatch(item,
                       "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
                       RegexOptions.CultureInvariant));
        }
    }

    internal StudioAssetQualityWorkbenchUx3E(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        Visible = false;
        Color = StudioStyle.Background;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 191;

        var margin = StudioStyle.Margin(22, 18, 22, 18);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddThemeConstantOverride("separation", 10);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("UX3-E · Qualité artistique des assets", 20, StudioStyle.Text));
        titles.AddChild(StudioStyle.Label(
            "Compatibilité artistique et budgets plateforme · deux aperçus, deux transactions canoniques.",
            10, StudioStyle.Muted, wrap: true));
        _context = StudioStyle.Label("Projet courant · les profils vides restent vides jusqu’à une action explicite.",
            10, StudioStyle.Accent, wrap: true);
        titles.AddChild(_context);
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var main = new HSplitContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        main.SplitOffsets = [300];
        root.AddChild(main);
        var selectorPanel = StudioStyle.Panel("Ux3ESelector", StudioStyle.Surface, 9, 1);
        selectorPanel.CustomMinimumSize = new Vector2(280, 0);
        var selectorMargin = StudioStyle.Margin(12, 12, 12, 12);
        selectorPanel.AddChild(selectorMargin);
        var selector = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selector.AddThemeConstantOverride("separation", 8);
        selectorMargin.AddChild(selector);
        selector.AddChild(StudioStyle.Label("BLUEPRINT CIBLE", 10, StudioStyle.Muted));
        selector.AddChild(StudioStyle.Label(
            "Choisissez explicitement un blueprint. Un asset Standard peut recevoir une analyse artistique, " +
            "mais son budget authoré reste protégé.", 10, StudioStyle.Text, wrap: true));
        _blueprint = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _blueprint.ItemSelected += SelectBlueprintUx3E;
        selector.AddChild(_blueprint);
        _selectionSummary = StudioStyle.Label("Aucune sélection.", 10, StudioStyle.Warning, wrap: true);
        selector.AddChild(_selectionSummary);
        selector.AddChild(new HSeparator());
        selector.AddChild(StudioStyle.Label(
            "LIMITES HONNÊTES\nLa mesure lit seulement des fichiers locaux GLB/PNG. " +
            "L’aperçu ne charge aucun modèle, ne mesure aucun GPU et ne modifie aucun asset. " +
            "Les budgets décrivent le document canonique ; le runtime et le build restent autorités.",
            9, StudioStyle.Muted, wrap: true));
        main.AddChild(selectorPanel);

        var contentPanel = StudioStyle.Panel("Ux3EContent", StudioStyle.Surface, 9, 1);
        contentPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        contentPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        var contentMargin = StudioStyle.Margin(12, 10, 12, 10);
        contentPanel.AddChild(contentMargin);
        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        contentMargin.AddChild(_tabs);
        main.AddChild(contentPanel);

        var styleTab = ScrollTabUx3E("Compatibilité artistique", out var styleBody);
        styleBody.AddChild(StudioStyle.Label(
            "MESURE LOCALE OPTIONNELLE · GLB + texture PNG gouvernés", 10, StudioStyle.Accent));
        styleBody.AddChild(StudioStyle.Label(
            "La mesure remplit densité texel et famille de shader. Époque, réalisme et palette restent des choix auteur.",
            9, StudioStyle.Muted, wrap: true));
        var measurementGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        measurementGrid.AddThemeConstantOverride("h_separation", 8);
        measurementGrid.AddThemeConstantOverride("v_separation", 6);
        measurementGrid.AddChild(StudioStyle.Label("Modèle GLB", 9, StudioStyle.Muted));
        _glb = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        measurementGrid.AddChild(_glb);
        measurementGrid.AddChild(StudioStyle.Label("Texture PNG", 9, StudioStyle.Muted));
        _texture = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        measurementGrid.AddChild(_texture);
        styleBody.AddChild(measurementGrid);
        _measure = StudioStyle.Button("Mesurer les fichiers locaux", compact: true);
        _measure.Pressed += MeasureUx3E;
        styleBody.AddChild(_measure);
        _measurement = StudioStyle.Label("Aucune mesure exécutée.", 9, StudioStyle.Muted, wrap: true);
        styleBody.AddChild(_measurement);

        var styleColumns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        styleColumns.AddThemeConstantOverride("separation", 12);
        var target = FieldColumnUx3E(styleColumns, "PROFIL CIBLE DU MODULE");
        _targetMeters = FieldUx3E(target, "Mètres par unité", "1");
        _targetTexel = FieldUx3E(target, "Densité texel cible", "512");
        _scaleTolerance = FieldUx3E(target, "Tolérance d’échelle (%)", "20");
        _texelTolerance = FieldUx3E(target, "Tolérance texel (%)", "50");
        _targetShader = FieldUx3E(target, "Famille de shader", "pbr-metallic-roughness");
        _targetEra = FieldUx3E(target, "Époque", "original-fantasy");
        _targetRealism = EnumUx3E(target, "Réalisme", Enum.GetValues<ArtisticRealism>(), HumanRealismUx3E);
        _targetPalette = FieldUx3E(target, "Palette (virgules)", "earth, stone");
        _targetProvenance = FieldUx3E(target, "Provenance du profil", "studio:profil-artistique");
        var asset = FieldColumnUx3E(styleColumns, "ASSET SÉLECTIONNÉ");
        _assetMeters = FieldUx3E(asset, "Mètres par unité", "1");
        _assetTexel = FieldUx3E(asset, "Densité texel", "512");
        _assetShader = FieldUx3E(asset, "Famille de shader", "pbr-metallic-roughness");
        _assetEra = FieldUx3E(asset, "Époque", "original-fantasy");
        _assetRealism = EnumUx3E(asset, "Réalisme", Enum.GetValues<ArtisticRealism>(), HumanRealismUx3E);
        _assetPalette = FieldUx3E(asset, "Palette (virgules)", "earth, stone");
        _assetProvenance = FieldUx3E(asset, "Provenance de mesure", "workspace://asset.glb");
        styleBody.AddChild(styleColumns);
        _styleApproval = new CheckBox { Text = "J’approuve le profil artistique et l’asset sélectionné." };
        styleBody.AddChild(_styleApproval);
        var styleActions = new HBoxContainer();
        styleActions.AddThemeConstantOverride("separation", 8);
        _stylePreviewButton = StudioStyle.Button("Aperçu sec artistique", compact: true);
        _stylePreviewButton.Pressed += PreviewStyleUx3E;
        styleActions.AddChild(_stylePreviewButton);
        _styleApplyButton = StudioStyle.Button("Appliquer le profil", compact: true);
        _styleApplyButton.Pressed += () => ApplyStyleUx3E();
        styleActions.AddChild(_styleApplyButton);
        styleBody.AddChild(styleActions);
        _styleSummary = StudioStyle.Label("Sélectionnez un blueprint.", 10, StudioStyle.Muted, wrap: true);
        styleBody.AddChild(_styleSummary);
        AddReviewUx3E(styleBody, out _styleDiff, out _styleDiagnostics);
        _tabs.AddChild(styleTab);

        var budgetTab = ScrollTabUx3E("Budget d’asset", out var budgetBody);
        budgetBody.AddChild(StudioStyle.Label(
            "SEPT MESURES · budgets explicites par plateforme", 10, StudioStyle.Accent));
        budgetBody.AddChild(StudioStyle.Label(
            "Un dépassement avec « Bloquer le build » refuse l’application. Sans blocage, il reste un avertissement visible.",
            9, StudioStyle.Muted, wrap: true));
        var metricsGrid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        metricsGrid.AddThemeConstantOverride("h_separation", 8);
        metricsGrid.AddThemeConstantOverride("v_separation", 6);
        _triangles = GridFieldUx3E(metricsGrid, "Triangles", "0");
        _materials = GridFieldUx3E(metricsGrid, "Matériaux", "0");
        _textures = GridFieldUx3E(metricsGrid, "Textures", "0");
        _memory = GridFieldUx3E(metricsGrid, "Mémoire (octets)", "0");
        _animations = GridFieldUx3E(metricsGrid, "Animations", "0");
        _lodLevels = GridFieldUx3E(metricsGrid, "Niveaux LOD", "0");
        _collisions = GridFieldUx3E(metricsGrid, "Formes de collision", "0");
        _budgetProvenance = GridFieldUx3E(metricsGrid, "Provenance", "studio:budget-asset");
        budgetBody.AddChild(metricsGrid);
        _runtimeRequired = new CheckBox { Text = "Budget requis par les lecteurs runtime" };
        budgetBody.AddChild(_runtimeRequired);
        budgetBody.AddChild(new HSeparator());
        budgetBody.AddChild(StudioStyle.Label("PLATEFORME SÉLECTIONNÉE", 10, StudioStyle.Muted));
        _platform = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var value in Enum.GetValues<AssetTargetPlatform>())
            _platform.AddItem(HumanPlatformUx3E(value), (int)value);
        _platform.ItemSelected += _ => PopulatePlatformEditorUx3E();
        budgetBody.AddChild(_platform);
        var platformGrid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        platformGrid.AddThemeConstantOverride("h_separation", 8);
        platformGrid.AddThemeConstantOverride("v_separation", 6);
        _maximumTriangles = GridFieldUx3E(platformGrid, "Triangles max.", "100000");
        _maximumMaterials = GridFieldUx3E(platformGrid, "Matériaux max.", "24");
        _maximumTextures = GridFieldUx3E(platformGrid, "Textures max.", "48");
        _maximumMemory = GridFieldUx3E(platformGrid, "Mémoire max. (octets)", "536870912");
        _maximumAnimations = GridFieldUx3E(platformGrid, "Animations max.", "32");
        _maximumLods = GridFieldUx3E(platformGrid, "LOD max.", "4");
        _maximumCollisions = GridFieldUx3E(platformGrid, "Collisions max.", "12");
        _gateBuild = new CheckBox { Text = "Bloquer le build" };
        platformGrid.AddChild(_gateBuild);
        budgetBody.AddChild(platformGrid);
        var platformActions = new HBoxContainer();
        platformActions.AddThemeConstantOverride("separation", 8);
        var upsertPlatform = StudioStyle.Button("Ajouter / mettre à jour", compact: true);
        upsertPlatform.Pressed += UpsertPlatformUx3E;
        platformActions.AddChild(upsertPlatform);
        var removePlatform = StudioStyle.Button("Retirer la plateforme", compact: true);
        removePlatform.Pressed += RemovePlatformUx3E;
        platformActions.AddChild(removePlatform);
        budgetBody.AddChild(platformActions);
        _platformRows = new VBoxContainer();
        _platformRows.AddThemeConstantOverride("separation", 4);
        budgetBody.AddChild(_platformRows);
        _budgetApproval = new CheckBox { Text = "J’approuve les mesures et budgets plateforme." };
        budgetBody.AddChild(_budgetApproval);
        var budgetActions = new HBoxContainer();
        budgetActions.AddThemeConstantOverride("separation", 8);
        _budgetPreviewButton = StudioStyle.Button("Aperçu sec des budgets", compact: true);
        _budgetPreviewButton.Pressed += PreviewBudgetUx3E;
        budgetActions.AddChild(_budgetPreviewButton);
        _budgetApplyButton = StudioStyle.Button("Appliquer le budget", compact: true);
        _budgetApplyButton.Pressed += () => ApplyBudgetUx3E();
        budgetActions.AddChild(_budgetApplyButton);
        budgetBody.AddChild(budgetActions);
        _budgetSummary = StudioStyle.Label("Sélectionnez un blueprint.", 10, StudioStyle.Muted, wrap: true);
        budgetBody.AddChild(_budgetSummary);
        AddReviewUx3E(budgetBody, out _budgetDiff, out _budgetDiagnostics);
        _tabs.AddChild(budgetTab);

        _status = StudioStyle.Label("Prêt.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 8);
        var footerNote = StudioStyle.Label(
            "Transactions séparées · après une application, rechargez et prévisualisez l’autre onglet.",
            9, StudioStyle.Muted, wrap: true);
        footerNote.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(footerNote);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += UndoUx3E;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += RedoUx3E;
        footer.AddChild(_redo);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        _save.Pressed += SaveUx3E;
        footer.AddChild(_save);
        root.AddChild(footer);

        WireDraftChangesUx3E();
        ResetUx3E();
    }

    internal void Open() => OpenBoundUx3E(_sourceSession, evidence: false, cancelApproval: false);
    internal void RestoreSourceSurfaceAfterSmoke() => OpenBoundUx3E(_sourceSession, evidence: false, cancelApproval: false);
    internal void HideSourceSurfaceAfterSmoke()
    {
        Visible = false;
        _session = _sourceSession;
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CloseUx3E();
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx3E();
        _session = _sourceSession;
        ResetUx3E();
    }

    internal void CloseEvidence()
    {
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx3E();
        _session = _sourceSession;
        ResetUx3E(keepReleaseState: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        if (_evidenceSession is not null) ReleaseEvidenceUx3E();
        _sourceSnapshot = CaptureSourceUx3E();
        var evidence = _sourceSession.CreateAssetQualityEvidenceProjectUx3E();
        if (evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-e-ui-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
            !StudioSessionController.MaterializeAssetQualityEvidenceFilesUx3E(root, evidence) ||
            !editing.Open(root).Succeeded)
        {
            editing.Close();
            StudioSessionController.DeleteTemporaryBlueprintUx3A(root);
            return false;
        }

        _evidenceSession = editing;
        _evidenceRoot = root;
        _evidenceReleased = false;
        _usesRealModuleEvidence = evidence.UsesRealModule;
        _usesCanonicalFallbackEvidence = evidence.UsesCanonicalFallback;
        OpenBoundUx3E(editing, evidence: true, cancelApproval: true);
        var index = _choices.ToList().FindIndex(item => item.Id == evidence.EditableBlueprintId);
        if (index < 0) return FailAndReleaseUx3E();
        _blueprint.Select(index);
        SelectBlueprintUx3E(index);
        SelectWorkspaceAssetUx3E(_glb, _glbChoices, evidence.GlbRelativePath);
        SelectWorkspaceAssetUx3E(_texture, _textureChoices, evidence.TextureRelativePath);
        MeasureUx3E();
        if (_styleDraft is null || _budgetDraft is null) return FailAndReleaseUx3E();
        _styleDraft = StudioSessionController.ChangedStyleDraftUx3E(_styleDraft);
        _budgetDraft = StudioSessionController.ChangedBudgetDraftUx3E(_budgetDraft);
        PopulateStyleUx3E();
        PopulateBudgetUx3E();
        PreviewStyleUx3E();
        PreviewBudgetUx3E();
        if (mode.Contains("budget", StringComparison.OrdinalIgnoreCase)) _tabs.CurrentTab = 1;
        var ready = StylePreviewReady && BudgetPreviewReady && ShowsHumanSurface && SourceMatchesUx3E();
        return ready || FailAndReleaseUx3E();
    }

    internal bool RunUiTransactionEvidence()
    {
        if (!PrepareEvidence("ux3-e-asset-quality")) return false;
        if (_evidenceSession is null || _evidenceRoot is null || !ReferenceEquals(_session, _evidenceSession) ||
            !StylePreviewReady || !BudgetPreviewReady || _styleDraft is null || _budgetDraft is null)
        {
            CloseEvidence();
            return false;
        }
        var isolated = _evidenceSession;
        var root = _evidenceRoot;
        var sourceJson = ProjectJsonSerializer.Serialize(isolated.Project!);
        var sourceRevision = isolated.History!.State.Revision;
        var styleApplied = ApplyStyleUx3E();
        if (!styleApplied)
        {
            CloseEvidence();
            return false;
        }
        var styleJson = ProjectJsonSerializer.Serialize(isolated.Project!);
        ReloadSelectionUx3E();
        if (_budgetDraft is null)
        {
            CloseEvidence();
            return false;
        }
        _budgetDraft = StudioSessionController.ChangedBudgetDraftUx3E(_budgetDraft);
        PopulateBudgetUx3E();
        PreviewBudgetUx3E();
        var budgetApplied = BudgetPreviewReady && ApplyBudgetUx3E();
        var finalJson = budgetApplied ? ProjectJsonSerializer.Serialize(isolated.Project!) : string.Empty;
        var undoBudget = isolated.UndoAssetQualityUx3E();
        var budgetUndone = undoBudget.Succeeded && ProjectJsonSerializer.Serialize(isolated.Project!) == styleJson;
        var undoStyle = isolated.UndoAssetQualityUx3E();
        var styleUndone = undoStyle.Succeeded && ProjectJsonSerializer.Serialize(isolated.Project!) == sourceJson;
        var redoStyle = isolated.RedoAssetQualityUx3E();
        var redoBudget = isolated.RedoAssetQualityUx3E();
        var redone = redoStyle.Succeeded && redoBudget.Succeeded &&
                     ProjectJsonSerializer.Serialize(isolated.Project!) == finalJson;
        var saved = isolated.SaveAssetQualityUx3E();
        var reopened = new StudioSessionController();
        var persistence = saved.Succeeded && reopened.Open(root).Succeeded &&
                          ProjectJsonSerializer.Serialize(reopened.Project!) == finalJson;
        reopened.Close();
        var history = isolated.History;
        var canonical = history is not null && history.Journal.Any(item => item.CommandName == "set_artistic_style_profile") &&
                        history.Journal.Any(item => item.CommandName == "configure_blueprint_prefab");
        var revisionMoved = history?.State.Revision.Value == sourceRevision.Value + 6;
        CloseEvidence();
        return budgetApplied && budgetUndone && styleUndone && redone && persistence && canonical && revisionMoved &&
               EvidenceResourcesReleased && SourceSessionRestored && SourceMatchesUx3E();
    }

    private void OpenBoundUx3E(StudioSessionController session, bool evidence, bool cancelApproval)
    {
        _session = session;
        if (cancelApproval) _session.CloseUx3E();
        Visible = true;
        _context.Text = evidence
            ? "SESSION DE PREUVE ISOLÉE · profil canonique temporaire · source strictement inchangée."
            : "PROJET COURANT · les modules réels sans profil restent dans un état vide explicite.";
        RebuildChoicesUx3E();
        ResetSelectionUx3E();
        UpdateButtonsUx3E();
    }

    private void RebuildChoicesUx3E()
    {
        _choices = _session.AssetQualityBlueprintChoicesUx3E;
        _blueprint.Clear();
        foreach (var choice in _choices) _blueprint.AddItem(choice.Label);
        _glbChoices = _session.WorkspaceGlbChoicesUx3E;
        _textureChoices = _session.WorkspacePngChoicesUx3E;
        RebuildAssetChoiceUx3E(_glb, _glbChoices, "Aucun GLB local");
        RebuildAssetChoiceUx3E(_texture, _textureChoices, "Aucune texture PNG locale");
    }

    private void SelectBlueprintUx3E(long index)
    {
        if (_updating || index < 0 || index >= _choices.Count) return;
        _explicitSelection = true;
        _selected = _choices[(int)index];
        _styleDraft = _session.CreateStyleDraftUx3E(_selected.Id);
        _budgetDraft = _session.CreateBudgetDraftUx3E(_selected.Id);
        _selectionSummary.Text = _selected.IsReadOnly
            ? $"{_selected.Label}\nAnalyse artistique autorisée · budget Standard protégé."
            : $"{_selected.Label}\nStyle : {(_selected.HasArtisticStyle ? "configuré" : "vide")} · " +
              $"budget : {(_selected.HasAssetBudget ? "configuré" : "vide")}.";
        _selectionSummary.Modulate = _selected.IsReadOnly ? StudioStyle.Warning : StudioStyle.Text;
        PopulateStyleUx3E();
        PopulateBudgetUx3E();
        ResetPreviewsUx3E();
        UpdateButtonsUx3E();
    }

    private void ReloadSelectionUx3E()
    {
        var selectedId = _selected?.Id;
        RebuildChoicesUx3E();
        if (selectedId is null) return;
        var index = _choices.ToList().FindIndex(item => item.Id == selectedId.Value);
        if (index < 0) return;
        _updating = true;
        _blueprint.Select(index);
        _updating = false;
        SelectBlueprintUx3E(index);
    }

    private void MeasureUx3E()
    {
        if (_selected is null || _glb.Selected < 0 || _texture.Selected < 0 ||
            _glb.Selected >= _glbChoices.Count || _texture.Selected >= _textureChoices.Count)
        {
            SetLocalStatusUx3E("Choisissez un blueprint, un GLB et une texture PNG.", true);
            return;
        }
        var result = _session.MeasureArtisticAssetUx3E(
            _selected.Id, _glbChoices[_glb.Selected].RelativePath, _textureChoices[_texture.Selected].RelativePath);
        _measurement.Text = result.Message;
        _measurement.Modulate = result.Succeeded ? StudioStyle.Success : StudioStyle.Danger;
        if (!result.Succeeded) return;
        _assetTexel.Text = result.EstimatedTexelDensity.ToString(CultureInfo.InvariantCulture);
        _assetShader.Text = result.ShaderFamily;
        _assetProvenance.Text = result.Provenance;
        DraftChangedUx3E();
    }

    private void PreviewStyleUx3E()
    {
        var draft = ReadStyleDraftUx3E();
        if (draft is null) return;
        _styleDraft = draft;
        _stylePreview = _session.PreviewStyleUx3E(draft);
        _styleSummary.Text = _stylePreview.Message;
        _styleSummary.Modulate = _stylePreview.Succeeded ? StudioStyle.Success : StudioStyle.Danger;
        RenderReviewUx3E(_styleDiff, _styleDiagnostics, _stylePreview.SemanticDiff, _stylePreview.Diagnostics);
        SetLocalStatusUx3E(_stylePreview.Message, !_stylePreview.Succeeded);
        UpdateButtonsUx3E();
    }

    private void PreviewBudgetUx3E()
    {
        var draft = ReadBudgetDraftUx3E();
        if (draft is null) return;
        _budgetDraft = draft;
        _budgetPreview = _session.PreviewBudgetUx3E(draft);
        _budgetSummary.Text = _budgetPreview.Message;
        _budgetSummary.Modulate = _budgetPreview.Succeeded ? StudioStyle.Success : StudioStyle.Danger;
        var semantic = _budgetPreview.SemanticDiff.Concat(_budgetPreview.Evaluations.Select(item =>
            $"{HumanPlatformUx3E(item.Platform)} · {HumanBudgetStatusUx3E(item.Status)}" +
            (item.ExceededMetrics.Count == 0 ? string.Empty : $" · {string.Join(", ", item.ExceededMetrics)}"))).ToArray();
        RenderReviewUx3E(_budgetDiff, _budgetDiagnostics, semantic, _budgetPreview.Diagnostics);
        SetLocalStatusUx3E(_budgetPreview.Message, !_budgetPreview.Succeeded);
        UpdateButtonsUx3E();
    }

    private bool ApplyStyleUx3E()
    {
        var draft = ReadStyleDraftUx3E();
        if (draft is null) return false;
        var result = _session.ApplyApprovedStyleUx3E(draft);
        SetLocalStatusUx3E(result.Message, !result.Succeeded);
        if (!result.Succeeded) return false;
        ProjectChanged?.Invoke();
        ReloadSelectionUx3E();
        return true;
    }

    private bool ApplyBudgetUx3E()
    {
        var draft = ReadBudgetDraftUx3E();
        if (draft is null) return false;
        var result = _session.ApplyApprovedBudgetUx3E(draft);
        SetLocalStatusUx3E(result.Message, !result.Succeeded);
        if (!result.Succeeded) return false;
        ProjectChanged?.Invoke();
        ReloadSelectionUx3E();
        return true;
    }

    private void UndoUx3E()
    {
        var result = _session.UndoAssetQualityUx3E();
        SetLocalStatusUx3E(result.Message, !result.Succeeded);
        if (result.Succeeded) { ProjectChanged?.Invoke(); ReloadSelectionUx3E(); }
    }

    private void RedoUx3E()
    {
        var result = _session.RedoAssetQualityUx3E();
        SetLocalStatusUx3E(result.Message, !result.Succeeded);
        if (result.Succeeded) { ProjectChanged?.Invoke(); ReloadSelectionUx3E(); }
    }

    private void SaveUx3E()
    {
        var result = _session.SaveAssetQualityUx3E();
        SetLocalStatusUx3E(result.Message, !result.Succeeded);
        if (result.Succeeded) ProjectChanged?.Invoke();
        UpdateButtonsUx3E();
    }

    private StudioUx3EStyleDraft? ReadStyleDraftUx3E()
    {
        if (_styleDraft is null ||
            !TryDoubleUx3E(_targetMeters.Text, out var targetMeters) ||
            !int.TryParse(_targetTexel.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetTexel) ||
            !TryDoubleUx3E(_scaleTolerance.Text, out var scaleTolerance) ||
            !TryDoubleUx3E(_texelTolerance.Text, out var texelTolerance) ||
            !TryDoubleUx3E(_assetMeters.Text, out var assetMeters) ||
            !int.TryParse(_assetTexel.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var assetTexel))
        {
            SetLocalStatusUx3E("Les valeurs d’échelle, densité et tolérance doivent être numériques.", true);
            return null;
        }
        return _styleDraft with
        {
            TargetMetersPerUnit = targetMeters,
            TargetTexelDensity = targetTexel,
            ScaleTolerancePercent = scaleTolerance,
            TexelTolerancePercent = texelTolerance,
            TargetShaderFamily = _targetShader.Text,
            TargetEra = _targetEra.Text,
            TargetRealism = (ArtisticRealism)_targetRealism.GetSelectedId(),
            TargetPalette = CsvUx3E(_targetPalette.Text),
            TargetProvenance = _targetProvenance.Text,
            AssetMetersPerUnit = assetMeters,
            AssetTexelDensity = assetTexel,
            AssetShaderFamily = _assetShader.Text,
            AssetEra = _assetEra.Text,
            AssetRealism = (ArtisticRealism)_assetRealism.GetSelectedId(),
            AssetPalette = CsvUx3E(_assetPalette.Text),
            AssetProvenance = _assetProvenance.Text,
            Approved = _styleApproval.ButtonPressed,
        };
    }

    private StudioUx3EBudgetDraft? ReadBudgetDraftUx3E()
    {
        if (_budgetDraft is null ||
            !int.TryParse(_triangles.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var triangles) ||
            !int.TryParse(_materials.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var materials) ||
            !int.TryParse(_textures.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var textures) ||
            !long.TryParse(_memory.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var memory) ||
            !int.TryParse(_animations.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var animations) ||
            !int.TryParse(_lodLevels.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lods) ||
            !int.TryParse(_collisions.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var collisions))
        {
            SetLocalStatusUx3E("Les sept mesures doivent être des nombres entiers.", true);
            return null;
        }
        return _budgetDraft with
        {
            Provenance = _budgetProvenance.Text,
            Metrics = new AssetBudgetMetrics(triangles, materials, textures, memory, animations, lods, collisions),
            Platforms = _platforms.ToArray(),
            RequiredAtRuntime = _runtimeRequired.ButtonPressed,
            Approved = _budgetApproval.ButtonPressed,
        };
    }

    private void UpsertPlatformUx3E()
    {
        if (!TryReadPlatformUx3E(out var budget)) return;
        _platforms.RemoveAll(item => item.Platform == budget.Platform);
        _platforms.Add(budget);
        _platforms = _platforms.OrderBy(item => item.Platform).ToList();
        RenderPlatformsUx3E();
        DraftChangedUx3E();
    }

    private void RemovePlatformUx3E()
    {
        var platform = (AssetTargetPlatform)_platform.GetSelectedId();
        _platforms.RemoveAll(item => item.Platform == platform);
        RenderPlatformsUx3E();
        DraftChangedUx3E();
    }

    private bool TryReadPlatformUx3E(out AssetPlatformBudget budget)
    {
        budget = default!;
        if (!int.TryParse(_maximumTriangles.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var triangles) ||
            !int.TryParse(_maximumMaterials.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var materials) ||
            !int.TryParse(_maximumTextures.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var textures) ||
            !long.TryParse(_maximumMemory.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var memory) ||
            !int.TryParse(_maximumAnimations.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var animations) ||
            !int.TryParse(_maximumLods.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lods) ||
            !int.TryParse(_maximumCollisions.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var collisions))
        {
            SetLocalStatusUx3E("Les limites de plateforme doivent être des nombres entiers.", true);
            return false;
        }
        budget = new AssetPlatformBudget((AssetTargetPlatform)_platform.GetSelectedId(), triangles, materials,
            textures, memory, animations, lods, collisions, _gateBuild.ButtonPressed);
        return true;
    }

    private void PopulateStyleUx3E()
    {
        if (_styleDraft is null) return;
        _updating = true;
        _targetMeters.Text = FormatDoubleUx3E(_styleDraft.TargetMetersPerUnit);
        _targetTexel.Text = _styleDraft.TargetTexelDensity.ToString(CultureInfo.InvariantCulture);
        _scaleTolerance.Text = FormatDoubleUx3E(_styleDraft.ScaleTolerancePercent);
        _texelTolerance.Text = FormatDoubleUx3E(_styleDraft.TexelTolerancePercent);
        _targetShader.Text = _styleDraft.TargetShaderFamily;
        _targetEra.Text = _styleDraft.TargetEra;
        SelectEnumUx3E(_targetRealism, (int)_styleDraft.TargetRealism);
        _targetPalette.Text = string.Join(", ", _styleDraft.TargetPalette);
        _targetProvenance.Text = _styleDraft.TargetProvenance;
        _assetMeters.Text = FormatDoubleUx3E(_styleDraft.AssetMetersPerUnit);
        _assetTexel.Text = _styleDraft.AssetTexelDensity.ToString(CultureInfo.InvariantCulture);
        _assetShader.Text = _styleDraft.AssetShaderFamily;
        _assetEra.Text = _styleDraft.AssetEra;
        SelectEnumUx3E(_assetRealism, (int)_styleDraft.AssetRealism);
        _assetPalette.Text = string.Join(", ", _styleDraft.AssetPalette);
        _assetProvenance.Text = _styleDraft.AssetProvenance;
        _styleApproval.ButtonPressed = _styleDraft.Approved;
        _updating = false;
    }

    private void PopulateBudgetUx3E()
    {
        if (_budgetDraft is null) return;
        _updating = true;
        var metrics = _budgetDraft.Metrics;
        _triangles.Text = metrics.TriangleCount.ToString(CultureInfo.InvariantCulture);
        _materials.Text = metrics.MaterialCount.ToString(CultureInfo.InvariantCulture);
        _textures.Text = metrics.TextureCount.ToString(CultureInfo.InvariantCulture);
        _memory.Text = metrics.MemoryBytes.ToString(CultureInfo.InvariantCulture);
        _animations.Text = metrics.AnimationCount.ToString(CultureInfo.InvariantCulture);
        _lodLevels.Text = metrics.LodLevelCount.ToString(CultureInfo.InvariantCulture);
        _collisions.Text = metrics.CollisionShapeCount.ToString(CultureInfo.InvariantCulture);
        _budgetProvenance.Text = _budgetDraft.Provenance;
        _runtimeRequired.ButtonPressed = _budgetDraft.RequiredAtRuntime;
        _budgetApproval.ButtonPressed = _budgetDraft.Approved;
        _platforms = _budgetDraft.Platforms.ToList();
        _updating = false;
        RenderPlatformsUx3E();
        PopulatePlatformEditorUx3E();
    }

    private void PopulatePlatformEditorUx3E()
    {
        if (_updating) return;
        var selected = (AssetTargetPlatform)_platform.GetSelectedId();
        var budget = _platforms.FirstOrDefault(item => item.Platform == selected) ??
                     new AssetPlatformBudget(selected, 100_000, 24, 48, 536_870_912, 32, 4, 12);
        _updating = true;
        _maximumTriangles.Text = budget.MaximumTriangles.ToString(CultureInfo.InvariantCulture);
        _maximumMaterials.Text = budget.MaximumMaterials.ToString(CultureInfo.InvariantCulture);
        _maximumTextures.Text = budget.MaximumTextures.ToString(CultureInfo.InvariantCulture);
        _maximumMemory.Text = budget.MaximumMemoryBytes.ToString(CultureInfo.InvariantCulture);
        _maximumAnimations.Text = budget.MaximumAnimations.ToString(CultureInfo.InvariantCulture);
        _maximumLods.Text = budget.MaximumLodLevels.ToString(CultureInfo.InvariantCulture);
        _maximumCollisions.Text = budget.MaximumCollisionShapes.ToString(CultureInfo.InvariantCulture);
        _gateBuild.ButtonPressed = budget.GateBuild;
        _updating = false;
    }

    private void RenderPlatformsUx3E()
    {
        ClearChildrenUx3E(_platformRows);
        if (_platforms.Count == 0)
        {
            _platformRows.AddChild(StudioStyle.Label("Aucune plateforme · ajoutez au moins un budget.",
                9, StudioStyle.Warning, wrap: true));
            return;
        }
        foreach (var item in _platforms.OrderBy(value => value.Platform))
        {
            _platformRows.AddChild(StudioStyle.Label(
                $"{HumanPlatformUx3E(item.Platform)} · {item.MaximumTriangles:N0} tri. · " +
                $"{item.MaximumMaterials} mat. · {item.MaximumTextures} tex. · " +
                $"{item.MaximumMemoryBytes / 1_048_576d:0.##} Mio · " +
                $"{(item.GateBuild ? "bloquant" : "avertissement")}",
                9, item.GateBuild ? StudioStyle.Text : StudioStyle.Warning, wrap: true));
        }
    }

    private void WireDraftChangesUx3E()
    {
        foreach (var field in new[]
                 {
                     _targetMeters, _targetTexel, _scaleTolerance, _texelTolerance, _targetShader, _targetEra,
                     _targetPalette, _targetProvenance, _assetMeters, _assetTexel, _assetShader, _assetEra,
                     _assetPalette, _assetProvenance, _triangles, _materials, _textures, _memory, _animations,
                     _lodLevels, _collisions, _budgetProvenance,
                 })
            field.TextChanged += _ => DraftChangedUx3E();
        _targetRealism.ItemSelected += _ => DraftChangedUx3E();
        _assetRealism.ItemSelected += _ => DraftChangedUx3E();
        _styleApproval.Toggled += _ => DraftChangedUx3E();
        _budgetApproval.Toggled += _ => DraftChangedUx3E();
        _runtimeRequired.Toggled += _ => DraftChangedUx3E();
    }

    private void DraftChangedUx3E()
    {
        if (_updating) return;
        ResetPreviewsUx3E();
        UpdateButtonsUx3E();
    }

    private void ResetSelectionUx3E()
    {
        _updating = true;
        _blueprint.Select(-1);
        _updating = false;
        _selected = null;
        _styleDraft = null;
        _budgetDraft = null;
        _explicitSelection = false;
        _selectionSummary.Text = _choices.Count == 0
            ? "Aucun blueprint dans ce projet."
            : "Choisissez un blueprint explicitement.";
        _selectionSummary.Modulate = StudioStyle.Warning;
        ResetPreviewsUx3E();
    }

    private void ResetPreviewsUx3E()
    {
        _stylePreview = null;
        _budgetPreview = null;
        _styleSummary.Text = _selected is null ? "Sélectionnez un blueprint." : "Relancez l’aperçu sec artistique.";
        _styleSummary.Modulate = StudioStyle.Muted;
        _budgetSummary.Text = _selected is null ? "Sélectionnez un blueprint." : "Relancez l’aperçu sec des budgets.";
        _budgetSummary.Modulate = StudioStyle.Muted;
        ClearChildrenUx3E(_styleDiff);
        ClearChildrenUx3E(_styleDiagnostics);
        ClearChildrenUx3E(_budgetDiff);
        ClearChildrenUx3E(_budgetDiagnostics);
    }

    private void ResetUx3E(bool keepReleaseState = false)
    {
        _choices = [];
        _glbChoices = [];
        _textureChoices = [];
        _selected = null;
        _styleDraft = null;
        _budgetDraft = null;
        _platforms = [];
        _explicitSelection = false;
        _blueprint.Clear();
        _glb.Clear();
        _texture.Clear();
        _selectionSummary.Text = "Aucune sélection.";
        _measurement.Text = "Aucune mesure exécutée.";
        if (!keepReleaseState)
        {
            _evidenceReleased = true;
            _usesRealModuleEvidence = false;
            _usesCanonicalFallbackEvidence = false;
        }
        ResetPreviewsUx3E();
        UpdateButtonsUx3E();
    }

    private void UpdateButtonsUx3E()
    {
        var selected = _selected is not null;
        _measure.Disabled = !selected || _glbChoices.Count == 0 || _textureChoices.Count == 0;
        _stylePreviewButton.Disabled = !selected;
        _styleApplyButton.Disabled = !StylePreviewReady;
        _budgetPreviewButton.Disabled = !selected || _selected!.IsReadOnly;
        _budgetApplyButton.Disabled = !BudgetPreviewReady || _selected?.IsReadOnly == true;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = !_session.IsDirty;
    }

    private SourceSnapshotUx3E CaptureSourceUx3E()
    {
        var history = _sourceSession.History;
        return new SourceSnapshotUx3E(
            _sourceSession.Project, history, history?.State.Revision, history?.Journal.ToArray() ?? [],
            _sourceSession.IsDirty, history?.CanUndo, history?.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            EditorWorkspaceProfileJsonSerializer.Serialize(_sourceSession.WorkspaceProfile),
            _sourceSession.CaptureAssetQualityApprovalUx3E());
    }

    private bool SourceMatchesUx3E()
    {
        if (_sourceSnapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(_sourceSnapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(_sourceSnapshot.History, history) &&
               history?.State.Revision == _sourceSnapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(_sourceSnapshot.Journal) &&
               _sourceSession.IsDirty == _sourceSnapshot.Dirty && history?.CanUndo == _sourceSnapshot.CanUndo &&
               history?.CanRedo == _sourceSnapshot.CanRedo && _sourceSession.ActiveAreaId == _sourceSnapshot.Area &&
               _sourceSession.SelectedEntityIds.SequenceEqual(_sourceSnapshot.Selection) &&
               _sourceSession.SelectedEntityId == _sourceSnapshot.Active &&
               EditorWorkspaceProfileJsonSerializer.Serialize(_sourceSession.WorkspaceProfile) == _sourceSnapshot.Workspace &&
               _sourceSession.CaptureAssetQualityApprovalUx3E() == _sourceSnapshot.Approval;
    }

    private void ReleaseEvidenceUx3E()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        var released = _evidenceRoot is null || StudioSessionController.DeleteTemporaryBlueprintUx3A(_evidenceRoot);
        _evidenceRoot = null;
        _session = _sourceSession;
        _evidenceReleased = released;
    }

    private bool FailAndReleaseUx3E()
    {
        ReleaseEvidenceUx3E();
        Visible = false;
        return false;
    }

    private void SetLocalStatusUx3E(string message, bool error)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Success;
        StatusChanged?.Invoke(message, error);
    }

    private static ScrollContainer ScrollTabUx3E(string title, out VBoxContainer body)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(body);
        return scroll;
    }

    private static VBoxContainer FieldColumnUx3E(HBoxContainer host, string title)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 5);
        column.AddChild(StudioStyle.Label(title, 10, StudioStyle.Muted));
        host.AddChild(column);
        return column;
    }

    private static LineEdit FieldUx3E(VBoxContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = placeholder };
        parent.AddChild(field);
        return field;
    }

    private static LineEdit GridFieldUx3E(GridContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = placeholder };
        parent.AddChild(field);
        return field;
    }

    private static OptionButton EnumUx3E<T>(VBoxContainer parent, string label, IReadOnlyList<T> values,
        Func<T, string> human) where T : struct, Enum
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var value in values) option.AddItem(human(value), Convert.ToInt32(value, CultureInfo.InvariantCulture));
        parent.AddChild(option);
        return option;
    }

    private static void AddReviewUx3E(VBoxContainer body, out VBoxContainer diff, out VBoxContainer diagnostics)
    {
        var columns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 12);
        diff = ReviewColumnUx3E(columns, "DIFF HUMAIN");
        diagnostics = ReviewColumnUx3E(columns, "DIAGNOSTICS");
        body.AddChild(columns);
    }

    private static VBoxContainer ReviewColumnUx3E(HBoxContainer host, string title)
    {
        var panel = StudioStyle.Panel($"Ux3E{title}", StudioStyle.SurfaceRaised, 7, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.CustomMinimumSize = new Vector2(0, 112);
        var margin = StudioStyle.Margin(9, 8, 9, 8);
        panel.AddChild(margin);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 4);
        body.AddChild(StudioStyle.Label(title, 9, StudioStyle.Muted));
        margin.AddChild(body);
        host.AddChild(panel);
        return body;
    }

    private static void RenderReviewUx3E(VBoxContainer diff, VBoxContainer diagnostics,
        IReadOnlyList<string> semantic, IReadOnlyList<StudioUx3EDiagnostic> issues)
    {
        ClearChildrenUx3E(diff, keepFirst: true);
        ClearChildrenUx3E(diagnostics, keepFirst: true);
        if (semantic.Count == 0) diff.AddChild(StudioStyle.Label("Aucune modification.", 9, StudioStyle.Muted));
        foreach (var line in semantic)
            diff.AddChild(StudioStyle.Label(HumanTextUx3E(line), 9, StudioStyle.Text, wrap: true));
        if (issues.Count == 0) diagnostics.AddChild(StudioStyle.Label("Aucun blocage canonique.", 9, StudioStyle.Success));
        foreach (var issue in issues)
            diagnostics.AddChild(StudioStyle.Label(
                $"{issue.Code} · {StudioSessionController.HumanAssetQualityPathUx3E(issue.Path)}\n{HumanTextUx3E(issue.Message)}",
                9, issue.Code.Contains("mismatch", StringComparison.Ordinal) ? StudioStyle.Warning : StudioStyle.Danger,
                wrap: true));
    }

    private static void ClearChildrenUx3E(Node node, bool keepFirst = false)
    {
        var children = node.GetChildren().Cast<Node>().ToArray();
        foreach (var child in keepFirst ? children.Skip(1) : children) child.QueueFree();
    }

    private static void RebuildAssetChoiceUx3E(OptionButton option,
        IReadOnlyList<StudioUx3EWorkspaceAssetChoice> choices, string empty)
    {
        option.Clear();
        if (choices.Count == 0)
        {
            option.AddItem(empty);
            option.Disabled = true;
            return;
        }
        option.Disabled = false;
        foreach (var choice in choices) option.AddItem(choice.Label);
        option.Select(0);
    }

    private static void SelectWorkspaceAssetUx3E(OptionButton option,
        IReadOnlyList<StudioUx3EWorkspaceAssetChoice> choices, string relativePath)
    {
        var index = choices.ToList().FindIndex(item => item.RelativePath == relativePath);
        if (index >= 0) option.Select(index);
    }

    private static void SelectEnumUx3E(OptionButton option, int id)
    {
        for (var index = 0; index < option.ItemCount; index++)
            if (option.GetItemId(index) == id) { option.Select(index); return; }
    }

    private static IReadOnlyList<string> CsvUx3E(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool TryDoubleUx3E(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static string FormatDoubleUx3E(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);

    private static string HumanRealismUx3E(ArtisticRealism value) => value switch
    {
        ArtisticRealism.Stylized => "Stylisé",
        ArtisticRealism.StylizedRealism => "Réalisme stylisé",
        ArtisticRealism.Realistic => "Réaliste",
        _ => value.ToString(),
    };

    private static string HumanPlatformUx3E(AssetTargetPlatform value) => value switch
    {
        AssetTargetPlatform.Any => "Toutes les plateformes",
        AssetTargetPlatform.Windows => "Windows",
        AssetTargetPlatform.Linux => "Linux",
        AssetTargetPlatform.MacOS => "macOS",
        AssetTargetPlatform.Android => "Android",
        AssetTargetPlatform.IOS => "iOS",
        _ => value.ToString(),
    };

    private static string HumanBudgetStatusUx3E(AssetBudgetEvaluationStatus value) => value switch
    {
        AssetBudgetEvaluationStatus.Passed => "conforme",
        AssetBudgetEvaluationStatus.Warning => "avertissement",
        AssetBudgetEvaluationStatus.Failed => "bloquant",
        _ => value.ToString(),
    };

    private static string HumanTextUx3E(string value) => Regex.Replace(value,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        "identifiant-protégé", RegexOptions.CultureInvariant);

    private sealed record SourceSnapshotUx3E(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool Dirty,
        bool? CanUndo,
        bool? CanRedo,
        Forge.Domain.Areas.AreaId? Area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? Active,
        string Workspace,
        StudioUx3EApprovalSnapshot Approval);
}
