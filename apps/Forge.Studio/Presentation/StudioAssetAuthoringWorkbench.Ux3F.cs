using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Application.Content;
using Forge.Domain.Assets;
using Forge.Domain.Content;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAssetAuthoringWorkbenchUx3F : ColorRect
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx3F? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _usesRealModuleEvidence;
    private bool _usesCanonicalFallbackEvidence;
    private bool _explicitSelection;
    private bool _updating;

    private readonly Label _context;
    private readonly LineEdit _search;
    private readonly OptionButton _kindFilter;
    private readonly ItemList _results;
    private readonly Label _browserMetric;
    private readonly Label _selectionSummary;
    private readonly TabContainer _tabs;
    private readonly Label _identity;
    private readonly Label _governance;
    private readonly Label _technical;
    private readonly Label _actions;

    private readonly OptionButton _processingKind;
    private readonly LineEdit _budgetBytes;
    private readonly LineEdit _provenance;
    private readonly OptionButton _textureColorSpace;
    private readonly OptionButton _textureCompression;
    private readonly LineEdit _textureDimension;
    private readonly LineEdit _textureDensity;
    private readonly TextEdit _textureChannels;
    private readonly CheckBox _modelRig;
    private readonly CheckBox _modelAnimations;
    private readonly CheckBox _modelCollisions;
    private readonly LineEdit _modelSockets;
    private readonly LineEdit _modelLods;
    private readonly CheckBox _processingApproval;
    private readonly Button _processingPreviewButton;
    private readonly Button _processingApplyButton;
    private readonly Label _processingSummary;
    private readonly VBoxContainer _processingDiff;
    private readonly VBoxContainer _processingDiagnostics;

    private readonly Label _externalPaths;
    private readonly LineEdit _externalProvenance;
    private readonly CheckBox _watch;
    private readonly Dictionary<AssetExternalOperation, CheckBox> _operationChecks = [];
    private readonly Dictionary<AssetTargetPlatform, CheckBox> _platformChecks = [];
    private readonly CheckBox _externalApproval;
    private readonly Button _externalPreviewButton;
    private readonly Button _externalApplyButton;
    private readonly Label _externalSummary;
    private readonly VBoxContainer _externalDiff;
    private readonly VBoxContainer _externalDiagnostics;

    private readonly Label _status;
    private readonly Button _undoProcessing;
    private readonly Button _redoProcessing;
    private readonly Button _undoExternal;
    private readonly Button _redoExternal;
    private readonly Button _save;

    private IReadOnlyList<StudioUx3FAssetChoice> _choices = [];
    private StudioUx3FAssetChoice? _selected;
    private StudioUx3FProcessingDraft? _processingDraft;
    private StudioUx3FExternalDraft? _externalDraft;
    private StudioUx3FProcessingPreview? _processingPreview;
    private StudioUx3FExternalPreview? _externalPreview;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? WorkspaceChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleEvidence => _usesRealModuleEvidence;
    internal bool UsesCanonicalFallbackEvidence => _usesCanonicalFallbackEvidence;
    internal bool HasExplicitSelection => _explicitSelection && _selected?.Asset is not null;
    internal bool ProcessingPreviewReady => _processingPreview is
    { Succeeded: true, CanApply: true, DryRunSucceeded: true, ImportPlanReady: true, RuntimeClaimsBounded: true };
    internal bool ExternalPreviewReady => _externalPreview is
    {
        Succeeded: true, CanApply: true, DryRunSucceeded: true, WorkerRequestValidated: true,
        WatchSourceResolved: true, RuntimeClaimsBounded: true
    };
    internal bool EvidenceSourceUntouched => SourceMatchesUx3F();
    internal bool EvidenceResourcesReleased => _evidenceReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var labels = FindChildren("*", "Label", true, false).Cast<Label>()
                .Select(item => item.Text).Where(item => !string.IsNullOrWhiteSpace(item));
            var buttons = FindChildren("*", "Button", true, false).Cast<Button>()
                .Select(item => item.Text).Where(item => !string.IsNullOrWhiteSpace(item));
            var tabs = Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle);
            var all = labels.Concat(buttons).Concat(tabs).ToArray();
            var navigator = all.Any(item =>
                item.Contains("Navigateur de contenu", StringComparison.OrdinalIgnoreCase));
            var profile = all.Any(item => item.Contains("Profil d’import", StringComparison.OrdinalIgnoreCase));
            var blender = all.Any(item => item.Contains("Édition Blender", StringComparison.OrdinalIgnoreCase));
            var dryRun = all.Any(item => item.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase));
            var noUuid = all.All(item => !Regex.IsMatch(item,
                "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
                RegexOptions.CultureInvariant));
            var valid = navigator && profile && blender && dryRun && noUuid;
            if (!valid)
                GD.Print("OPENRPG_FORGE_STUDIO_UX3_F_HUMAN_FAILED " +
                         $"navigator={navigator.ToString().ToLowerInvariant()} " +
                         $"profile={profile.ToString().ToLowerInvariant()} " +
                         $"blender={blender.ToString().ToLowerInvariant()} " +
                         $"dry_run={dryRun.ToString().ToLowerInvariant()} " +
                         $"no_uuid={noUuid.ToString().ToLowerInvariant()}");
            return valid;
        }
    }

    internal StudioAssetAuthoringWorkbenchUx3F(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        Visible = false;
        Color = StudioStyle.Background;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 192;

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
        titles.AddChild(StudioStyle.Label("UX3-F · Navigateur de contenu & aller-retour d’asset", 20, StudioStyle.Text));
        titles.AddChild(StudioStyle.Label(
            "Trouver une texture ou un modèle, préparer son import, puis produire une requête Blender typée.",
            10, StudioStyle.Muted, wrap: true));
        _context = StudioStyle.Label("PROJET COURANT · catalogue et documents réels, aucune synthèse automatique.",
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
        main.SplitOffsets = [370];
        root.AddChild(main);

        var selectorPanel = StudioStyle.Panel("Ux3FSelector", StudioStyle.Surface, 9, 1);
        selectorPanel.CustomMinimumSize = new Vector2(345, 0);
        var selectorMargin = StudioStyle.Margin(12, 12, 12, 12);
        selectorPanel.AddChild(selectorMargin);
        var selector = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        selector.AddThemeConstantOverride("separation", 8);
        selectorMargin.AddChild(selector);
        selector.AddChild(StudioStyle.Label("NAVIGATEUR DE CONTENU", 10, StudioStyle.Muted));
        _search = new LineEdit { PlaceholderText = "Nom, source, licence, provenance…" };
        _search.TextChanged += _ => RefreshBrowserUx3F();
        selector.AddChild(_search);
        _kindFilter = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _kindFilter.AddItem("Toutes les ressources", -1);
        _kindFilter.AddItem("Textures", (int)ContentResourceKind.Texture);
        _kindFilter.AddItem("Modèles 3D", (int)ContentResourceKind.Model);
        _kindFilter.ItemSelected += _ => RefreshBrowserUx3F();
        selector.AddChild(_kindFilter);
        _browserMetric = StudioStyle.Label("Catalogue non chargé.", 9, StudioStyle.Muted, wrap: true);
        selector.AddChild(_browserMetric);
        _results = new ItemList
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single,
        };
        _results.ItemSelected += SelectAssetUx3F;
        selector.AddChild(_results);
        _selectionSummary = StudioStyle.Label("Choisissez une ressource explicitement.", 9, StudioStyle.Warning, wrap: true);
        selector.AddChild(_selectionSummary);
        selector.AddChild(new HSeparator());
        selector.AddChild(StudioStyle.Label(
            "LIMITES HONNÊTES\nLe navigateur n’acquiert aucun asset. L’aperçu ne transforme aucun fichier. " +
            "La requête Blender est une enveloppe allowlistée : aucun Python libre, shell, réseau ou processus n’est lancé.",
            9, StudioStyle.Muted, wrap: true));
        main.AddChild(selectorPanel);

        var contentPanel = StudioStyle.Panel("Ux3FContent", StudioStyle.Surface, 9, 1);
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

        var browserTab = ScrollTabUx3F("Navigateur de contenu", out var browserBody);
        browserBody.AddChild(StudioStyle.Label("RESSOURCE SÉLECTIONNÉE", 10, StudioStyle.Accent));
        _identity = StudioStyle.Label("Aucune ressource.", 11, StudioStyle.Text, wrap: true);
        _governance = StudioStyle.Label("Licence et provenance en attente.", 9, StudioStyle.Muted, wrap: true);
        _technical = StudioStyle.Label("Métadonnées techniques en attente.", 9, StudioStyle.Muted, wrap: true);
        _actions = StudioStyle.Label("Actions disponibles en attente.", 9, StudioStyle.Muted, wrap: true);
        browserBody.AddChild(_identity);
        browserBody.AddChild(new HSeparator());
        browserBody.AddChild(_governance);
        browserBody.AddChild(_technical);
        browserBody.AddChild(_actions);
        browserBody.AddChild(StudioStyle.Label(
            "La vignette et le turntable ne sont pas chargés dans cet aperçu sec. Le chemin complet, le hash et " +
            "l’identifiant interne restent protégés ; seul le nom humain et la gouvernance utile sont affichés.",
            9, StudioStyle.Muted, wrap: true));
        _tabs.AddChild(browserTab);

        var processingTab = ScrollTabUx3F("Profil d’import", out var processingBody);
        processingBody.AddChild(StudioStyle.Label(
            "APERÇU SEC CANONIQUE · texture KTX2 ou modèle GLB", 10, StudioStyle.Accent));
        processingBody.AddChild(StudioStyle.Label(
            "Le plan affiche ses tâches sans toucher la source. Le profil et son historique sont propres au workspace.",
            9, StudioStyle.Muted, wrap: true));
        _processingKind = EnumOptionUx3F<AssetProcessingKind>(processingBody, "Type de traitement", HumanProcessingKindUx3F);
        _processingKind.ItemSelected += _ => ProcessingKindChangedUx3F();
        var common = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        common.AddThemeConstantOverride("h_separation", 8);
        common.AddThemeConstantOverride("v_separation", 6);
        _budgetBytes = GridFieldUx3F(common, "Budget de sortie (octets)", "8388608");
        _provenance = GridFieldUx3F(common, "Provenance", "catalogue gouverné");
        processingBody.AddChild(common);

        processingBody.AddChild(StudioStyle.Label("TEXTURE", 10, StudioStyle.Muted));
        var textureGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        textureGrid.AddThemeConstantOverride("h_separation", 8);
        textureGrid.AddThemeConstantOverride("v_separation", 6);
        _textureColorSpace = GridEnumOptionUx3F<TextureColorSpace>(textureGrid, "Espace colorimétrique", value =>
            value == TextureColorSpace.Srgb ? "sRGB" : "Linéaire");
        _textureCompression = GridEnumOptionUx3F<TextureCompressionProfile>(textureGrid, "Compression", value =>
            value switch
            {
                TextureCompressionProfile.Lossless => "Sans perte",
                TextureCompressionProfile.Desktop => "Bureau",
                TextureCompressionProfile.Mobile => "Mobile",
                _ => "Universelle",
            });
        _textureDimension = GridFieldUx3F(textureGrid, "Dimension maximale", "4096");
        _textureDensity = GridFieldUx3F(textureGrid, "Densité texel", "10.24");
        processingBody.AddChild(textureGrid);
        processingBody.AddChild(StudioStyle.Label("Canaux · une ligne Canal=chemin relatif", 9, StudioStyle.Muted));
        _textureChannels = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 82),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "BaseColor=textures/pierre.png",
        };
        _textureChannels.TextChanged += DraftChangedUx3F;
        processingBody.AddChild(_textureChannels);

        processingBody.AddChild(StudioStyle.Label("MODÈLE 3D", 10, StudioStyle.Muted));
        var modelFlags = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        modelFlags.AddThemeConstantOverride("separation", 12);
        _modelRig = new CheckBox { Text = "Importer le rig" };
        _modelAnimations = new CheckBox { Text = "Importer les animations" };
        _modelCollisions = new CheckBox { Text = "Générer les collisions" };
        modelFlags.AddChild(_modelRig);
        modelFlags.AddChild(_modelAnimations);
        modelFlags.AddChild(_modelCollisions);
        processingBody.AddChild(modelFlags);
        var modelGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        modelGrid.AddThemeConstantOverride("h_separation", 8);
        modelGrid.AddThemeConstantOverride("v_separation", 6);
        _modelSockets = GridFieldUx3F(modelGrid, "Points d’attache (virgules)", "main_droite");
        _modelLods = GridFieldUx3F(modelGrid, "Ratios LOD (virgules)", "1, 0.5, 0.25");
        processingBody.AddChild(modelGrid);
        _processingApproval = new CheckBox { Text = "J’approuve ce profil et son plan d’import sec." };
        processingBody.AddChild(_processingApproval);
        var processingActions = new HBoxContainer();
        processingActions.AddThemeConstantOverride("separation", 8);
        _processingPreviewButton = StudioStyle.Button("Aperçu sec du profil", compact: true);
        _processingPreviewButton.Pressed += PreviewProcessingUx3F;
        processingActions.AddChild(_processingPreviewButton);
        _processingApplyButton = StudioStyle.Button("Appliquer le profil", compact: true);
        _processingApplyButton.Pressed += () => ApplyProcessingUx3F();
        processingActions.AddChild(_processingApplyButton);
        processingBody.AddChild(processingActions);
        _processingSummary = StudioStyle.Label("Sélectionnez un asset catalogué.", 10, StudioStyle.Muted, wrap: true);
        processingBody.AddChild(_processingSummary);
        AddReviewUx3F(processingBody, out _processingDiff, out _processingDiagnostics);
        _tabs.AddChild(processingTab);

        var externalTab = ScrollTabUx3F("Édition Blender", out var externalBody);
        externalBody.AddChild(StudioStyle.Label(
            "ALLER-RETOUR EXTERNE · requête Blender typée et confinée", 10, StudioStyle.Accent));
        externalBody.AddChild(StudioStyle.Label(
            "Cette surface prépare une requête ; elle ne lance pas Blender et ne réimporte rien automatiquement.",
            9, StudioStyle.Muted, wrap: true));
        _externalPaths = StudioStyle.Label("Sélectionnez un modèle local doté d’un manifeste.", 9,
            StudioStyle.Muted, wrap: true);
        externalBody.AddChild(_externalPaths);
        _externalProvenance = FieldUx3F(externalBody, "Provenance de modification", "studio:edition-externe");
        _watch = new CheckBox { Text = "Surveiller les changements du fichier source" };
        externalBody.AddChild(_watch);
        externalBody.AddChild(StudioStyle.Label("OPÉRATIONS AUTORISÉES", 10, StudioStyle.Muted));
        var operationsGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var operation in BlenderOperationsUx3F)
        {
            var check = new CheckBox { Text = HumanExternalOperationUx3F(operation) };
            check.Toggled += _ => DraftChangedUx3F();
            _operationChecks[operation] = check;
            operationsGrid.AddChild(check);
        }
        externalBody.AddChild(operationsGrid);
        externalBody.AddChild(StudioStyle.Label("PLATEFORMES CIBLES", 10, StudioStyle.Muted));
        var platforms = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        platforms.AddThemeConstantOverride("separation", 10);
        foreach (var platform in Enum.GetValues<AssetTargetPlatform>())
        {
            var check = new CheckBox { Text = HumanPlatformUx3F(platform) };
            check.Toggled += _ => DraftChangedUx3F();
            _platformChecks[platform] = check;
            platforms.AddChild(check);
        }
        externalBody.AddChild(platforms);
        _externalApproval = new CheckBox { Text = "J’approuve la configuration et la requête Blender allowlistée." };
        externalBody.AddChild(_externalApproval);
        var externalActions = new HBoxContainer();
        externalActions.AddThemeConstantOverride("separation", 8);
        _externalPreviewButton = StudioStyle.Button("Aperçu sec Blender", compact: true);
        _externalPreviewButton.Pressed += PreviewExternalUx3F;
        externalActions.AddChild(_externalPreviewButton);
        _externalApplyButton = StudioStyle.Button("Enregistrer la requête", compact: true);
        _externalApplyButton.Pressed += () => ApplyExternalUx3F();
        externalActions.AddChild(_externalApplyButton);
        externalBody.AddChild(externalActions);
        _externalSummary = StudioStyle.Label("Sélectionnez un modèle local avec manifeste.", 10,
            StudioStyle.Muted, wrap: true);
        externalBody.AddChild(_externalSummary);
        AddReviewUx3F(externalBody, out _externalDiff, out _externalDiagnostics);
        _tabs.AddChild(externalTab);

        WireDraftChangesUx3F();
        _status = StudioStyle.Label("Prêt.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 8);
        var note = StudioStyle.Label(
            "Historiques séparés · profil d’import et édition externe n’altèrent pas le document projet.",
            9, StudioStyle.Muted, wrap: true);
        note.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(note);
        _undoProcessing = StudioStyle.Button("Annuler profil", compact: true);
        _undoProcessing.Pressed += () => MoveProcessingUx3F(undo: true);
        footer.AddChild(_undoProcessing);
        _redoProcessing = StudioStyle.Button("Rétablir profil", compact: true);
        _redoProcessing.Pressed += () => MoveProcessingUx3F(undo: false);
        footer.AddChild(_redoProcessing);
        _undoExternal = StudioStyle.Button("Annuler externe", compact: true);
        _undoExternal.Pressed += () => MoveExternalUx3F(undo: true);
        footer.AddChild(_undoExternal);
        _redoExternal = StudioStyle.Button("Rétablir externe", compact: true);
        _redoExternal.Pressed += () => MoveExternalUx3F(undo: false);
        footer.AddChild(_redoExternal);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        _save.Pressed += SaveUx3F;
        footer.AddChild(_save);
        root.AddChild(footer);

        ResetUx3F();
    }

    internal void Open() => OpenBoundUx3F(_sourceSession, evidence: false, cancelApproval: false);
    internal void RestoreSourceSurfaceAfterSmoke() => OpenBoundUx3F(_sourceSession, evidence: false, cancelApproval: false);
    internal void HideSourceSurfaceAfterSmoke()
    {
        Visible = false;
        _session = _sourceSession;
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CancelAssetAuthoringApprovalsUx3F();
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx3F();
        _session = _sourceSession;
        ResetUx3F();
    }

    internal void CloseEvidence()
    {
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx3F();
        _session = _sourceSession;
        ResetUx3F(keepReleaseState: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        if (_evidenceSession is not null) ReleaseEvidenceUx3F();
        _sourceSnapshot = CaptureSourceUx3F();
        var evidence = _sourceSession.CreateAssetAuthoringEvidencePlanUx3F();
        if (evidence is null)
        {
            GD.Print("OPENRPG_FORGE_STUDIO_UX3_F_EVIDENCE_FAILED stage=plan");
            return false;
        }
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-f-ui-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
            !StudioSessionController.MaterializeAssetAuthoringEvidenceUx3F(root, evidence) ||
            !editing.Open(root).Succeeded)
        {
            GD.Print("OPENRPG_FORGE_STUDIO_UX3_F_EVIDENCE_FAILED stage=materialize");
            editing.Close();
            StudioSessionController.DeleteTemporaryBlueprintUx3A(root);
            return false;
        }
        _evidenceSession = editing;
        _evidenceRoot = root;
        _evidenceReleased = false;
        _usesRealModuleEvidence = evidence.UsesRealModule;
        _usesCanonicalFallbackEvidence = evidence.UsesCanonicalFallback;
        OpenBoundUx3F(editing, evidence: true, cancelApproval: true);
        var index = _choices.ToList().FindIndex(item => item.Asset?.Id == evidence.Asset.Id);
        if (index < 0)
        {
            GD.Print("OPENRPG_FORGE_STUDIO_UX3_F_EVIDENCE_FAILED stage=selection");
            return FailAndReleaseUx3F();
        }
        _results.Select(index);
        SelectAssetUx3F(index);
        if (_processingDraft is null || _externalDraft is null)
        {
            GD.Print("OPENRPG_FORGE_STUDIO_UX3_F_EVIDENCE_FAILED stage=draft " +
                     $"processing={(_processingDraft is not null).ToString().ToLowerInvariant()} " +
                     $"external={(_externalDraft is not null).ToString().ToLowerInvariant()}");
            return FailAndReleaseUx3F();
        }
        _processingDraft = ChangedProcessingDraftUx3F(_processingDraft);
        _externalDraft = ChangedExternalDraftUx3F(_externalDraft);
        PopulateProcessingUx3F();
        PopulateExternalUx3F();
        PreviewProcessingUx3F();
        PreviewExternalUx3F();
        _tabs.CurrentTab = mode.Contains("blender", StringComparison.OrdinalIgnoreCase) ||
                           mode.Contains("external", StringComparison.OrdinalIgnoreCase)
            ? 2
            : mode.Contains("import", StringComparison.OrdinalIgnoreCase) ||
              mode.Contains("profile", StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;
        var processingReady = ProcessingPreviewReady;
        var externalReady = ExternalPreviewReady;
        var humanReady = ShowsHumanSurface;
        var sourceReady = SourceMatchesUx3F();
        var ready = processingReady && externalReady && humanReady && sourceReady;
        if (!ready)
            GD.Print("OPENRPG_FORGE_STUDIO_UX3_F_EVIDENCE_FAILED stage=ready " +
                     $"processing={processingReady.ToString().ToLowerInvariant()} " +
                     $"external={externalReady.ToString().ToLowerInvariant()} " +
                     $"human={humanReady.ToString().ToLowerInvariant()} " +
                     $"source={sourceReady.ToString().ToLowerInvariant()}");
        return ready || FailAndReleaseUx3F();
    }

    internal bool RunUiTransactionEvidence()
    {
        if (!PrepareEvidence("ux3-f-content-browser")) return false;
        if (_evidenceSession is null || _evidenceRoot is null || !ReferenceEquals(_session, _evidenceSession) ||
            _processingDraft is null || _externalDraft is null || !ProcessingPreviewReady || !ExternalPreviewReady)
        {
            CloseEvidence();
            return false;
        }
        var isolated = _evidenceSession;
        var root = _evidenceRoot;
        var sourceProject = ProjectJsonSerializer.Serialize(isolated.Project!);
        var sourceProfiles = ReadOptionalFileUx3F(root, AssetProcessingProfileStore.RelativePath);
        var sourceExternal = ReadOptionalFileUx3F(root, AssetExternalEditStore.RelativePath);
        var processingApplied = ApplyProcessingUx3F();
        if (!processingApplied)
        {
            CloseEvidence();
            return false;
        }
        ReloadSelectionUx3F();
        if (_externalDraft is null)
        {
            CloseEvidence();
            return false;
        }
        _externalDraft = ChangedExternalDraftUx3F(_externalDraft);
        PopulateExternalUx3F();
        PreviewExternalUx3F();
        var externalApplied = ExternalPreviewReady && ApplyExternalUx3F();
        var finalProfiles = ReadOptionalFileUx3F(root, AssetProcessingProfileStore.RelativePath);
        var finalExternal = ReadOptionalFileUx3F(root, AssetExternalEditStore.RelativePath);
        var undoExternal = isolated.UndoExternalUx3F().Succeeded;
        var undoProcessing = isolated.UndoProcessingUx3F().Succeeded;
        var undone = ReadOptionalFileUx3F(root, AssetProcessingProfileStore.RelativePath) == sourceProfiles &&
                     ReadOptionalFileUx3F(root, AssetExternalEditStore.RelativePath) == sourceExternal;
        var redoProcessing = isolated.RedoProcessingUx3F().Succeeded;
        var redoExternal = isolated.RedoExternalUx3F().Succeeded;
        var redone = ReadOptionalFileUx3F(root, AssetProcessingProfileStore.RelativePath) == finalProfiles &&
                     ReadOptionalFileUx3F(root, AssetExternalEditStore.RelativePath) == finalExternal;
        var saved = isolated.SaveAssetAuthoringUx3F();
        var reopened = new StudioSessionController();
        var persistence = saved.Succeeded && reopened.Open(root).Succeeded &&
                          ReadOptionalFileUx3F(root, AssetProcessingProfileStore.RelativePath) == finalProfiles &&
                          ReadOptionalFileUx3F(root, AssetExternalEditStore.RelativePath) == finalExternal &&
                          ProjectJsonSerializer.Serialize(reopened.Project!) == sourceProject;
        reopened.Close();
        CloseEvidence();
        return externalApplied && undoExternal && undoProcessing && undone && redoProcessing && redoExternal && redone &&
               persistence && EvidenceResourcesReleased && SourceSessionRestored && SourceMatchesUx3F();
    }

    private void OpenBoundUx3F(StudioSessionController session, bool evidence, bool cancelApproval)
    {
        _session = session;
        if (cancelApproval) _session.CancelAssetAuthoringApprovalsUx3F();
        Visible = true;
        _context.Text = evidence
            ? _usesCanonicalFallbackEvidence
                ? "SESSION DE PREUVE ISOLÉE · asset réel copié · profil et manifeste canoniques temporaires · source strictement inchangée."
                : "SESSION DE PREUVE ISOLÉE · asset, profil et manifeste réels copiés · source strictement inchangée."
            : "PROJET COURANT · catalogue et documents réels, aucune synthèse automatique.";
        _updating = true;
        _search.Text = string.Empty;
        _kindFilter.Select(0);
        _updating = false;
        RefreshBrowserUx3F();
        ResetSelectionUx3F();
        UpdateButtonsUx3F();
    }

    private void RefreshBrowserUx3F()
    {
        if (_updating) return;
        var kind = _kindFilter.Selected switch
        {
            1 => ContentResourceKind.Texture,
            2 => ContentResourceKind.Model,
            _ => (ContentResourceKind?)null,
        };
        var result = _session.BrowseAssetsUx3F(_search.Text, kind, 2_000);
        _choices = result.Choices;
        _results.Clear();
        foreach (var choice in _choices) _results.AddItem(choice.Label);
        _browserMetric.Text = result.Succeeded
            ? $"{result.CatalogCount} assets catalogués · {result.UnifiedCount} ressources unifiées · {_choices.Count} visibles"
            : result.Message;
        _browserMetric.Modulate = result.Succeeded ? StudioStyle.Muted : StudioStyle.Danger;
        ResetSelectionUx3F();
    }

    private void SelectAssetUx3F(long index)
    {
        if (_updating || index < 0 || index >= _choices.Count) return;
        _explicitSelection = true;
        _selected = _choices[(int)index];
        RenderSelectedAssetUx3F();
        if (_selected.Asset is null)
        {
            _processingDraft = null;
            _externalDraft = null;
            _selectionSummary.Text = $"{_selected.Label}\nRessource du module en lecture seule ; aucun profil d’asset catalogué.";
            _selectionSummary.Modulate = StudioStyle.Warning;
            ResetPreviewsUx3F();
            UpdateButtonsUx3F();
            return;
        }
        var processingKind = _selected.Resource.Kind == ContentResourceKind.Texture
            ? AssetProcessingKind.Texture
            : AssetProcessingKind.Model;
        _processingDraft = _session.CreateProcessingDraftUx3F(_selected.Asset.Id, processingKind);
        _externalDraft = processingKind == AssetProcessingKind.Model
            ? _session.CreateExternalDraftUx3F(_selected.Asset.Id)
            : null;
        _selectionSummary.Text = $"{_selected.Label}\n{_selected.AvailabilityLabel} · " +
                                 $"profil {(_selected.HasProcessingProfile ? "configuré" : "à créer")} · " +
                                 $"édition externe {(_selected.HasExternalEdit ? "configurée" : "à préparer")}.";
        _selectionSummary.Modulate = _processingDraft is null ? StudioStyle.Warning : StudioStyle.Text;
        PopulateProcessingUx3F();
        PopulateExternalUx3F();
        ResetPreviewsUx3F();
        UpdateButtonsUx3F();
    }

    private void RenderSelectedAssetUx3F()
    {
        if (_selected is null) return;
        var resource = _selected.Resource;
        var asset = _selected.Asset;
        _identity.Text = $"{StudioSessionController.HumanPathUx3F(resource.DisplayName)} · {_selected.KindLabel}\n" +
                         $"{_selected.AvailabilityLabel} · source {HumanTextUx3F(resource.PackageNamespace)}";
        _governance.Text = $"Licence · {HumanTextUx3F(resource.LicenseSpdx)}\n" +
                           $"Provenance · {HumanTextUx3F(resource.Provenance)}\n" +
                           $"État · {HumanTextUx3F(resource.Status)}";
        _technical.Text = asset is null
            ? $"Ressource du module · références {resource.References.Count} · " +
              $"aperçu {(resource.PreviewPath is null ? "absent" : "catalogué")}."
            : $"Catalogue · {asset.Categories.Count} catégorie(s), {asset.Tags.Count} tag(s) · " +
              $"{asset.TotalBytes / 1024d:0.##} Kio · " +
              $"source locale {(_selected.HasLocalSource ? "disponible" : "absente")} · " +
              $"manifeste {(_selected.HasManifest ? "présent" : "absent")}.";
        _actions.Text = resource.Actions.Count == 0
            ? "Aucune action annoncée."
            : $"Actions canoniques · {string.Join(" · ", resource.Actions.Select(HumanActionUx3F))}.";
    }

    private void ReloadSelectionUx3F()
    {
        var selected = _selected?.Asset?.Id;
        RefreshBrowserUx3F();
        if (selected is null) return;
        var index = _choices.ToList().FindIndex(item => item.Asset?.Id == selected);
        if (index < 0) return;
        _updating = true;
        _results.Select(index);
        _updating = false;
        SelectAssetUx3F(index);
    }

    private void ProcessingKindChangedUx3F()
    {
        if (_updating || _selected?.Asset is null) return;
        _processingDraft = _session.CreateProcessingDraftUx3F(
            _selected.Asset.Id,
            (AssetProcessingKind)_processingKind.GetSelectedId());
        PopulateProcessingUx3F();
        DraftChangedUx3F();
    }

    private void PreviewProcessingUx3F()
    {
        var draft = ReadProcessingDraftUx3F();
        if (draft is null) return;
        _processingDraft = draft;
        _processingPreview = _session.PreviewProcessingUx3F(draft);
        _processingSummary.Text = _processingPreview.Message;
        _processingSummary.Modulate = _processingPreview.Succeeded ? StudioStyle.Success : StudioStyle.Danger;
        RenderReviewUx3F(_processingDiff, _processingDiagnostics,
            _processingPreview.SemanticDiff.Concat(_processingPreview.Tasks), _processingPreview.Diagnostics);
        SetLocalStatusUx3F(_processingPreview.Message, !_processingPreview.Succeeded);
        UpdateButtonsUx3F();
    }

    private void PreviewExternalUx3F()
    {
        var draft = ReadExternalDraftUx3F();
        if (draft is null) return;
        _externalDraft = draft;
        _externalPreview = _session.PreviewExternalUx3F(draft);
        _externalSummary.Text = _externalPreview.Message;
        _externalSummary.Modulate = _externalPreview.Succeeded ? StudioStyle.Success : StudioStyle.Danger;
        RenderReviewUx3F(_externalDiff, _externalDiagnostics,
            _externalPreview.SemanticDiff.Concat(_externalPreview.Operations), _externalPreview.Diagnostics);
        SetLocalStatusUx3F(_externalPreview.Message, !_externalPreview.Succeeded);
        UpdateButtonsUx3F();
    }

    private bool ApplyProcessingUx3F()
    {
        var draft = ReadProcessingDraftUx3F();
        if (draft is null) return false;
        var result = _session.ApplyApprovedProcessingUx3F(draft);
        SetLocalStatusUx3F(result.Message, !result.Succeeded);
        if (!result.Succeeded) return false;
        WorkspaceChanged?.Invoke();
        ReloadSelectionUx3F();
        return true;
    }

    private bool ApplyExternalUx3F()
    {
        var draft = ReadExternalDraftUx3F();
        if (draft is null) return false;
        var result = _session.ApplyApprovedExternalUx3F(draft);
        SetLocalStatusUx3F(result.Message, !result.Succeeded);
        if (!result.Succeeded) return false;
        WorkspaceChanged?.Invoke();
        ReloadSelectionUx3F();
        return true;
    }

    private void MoveProcessingUx3F(bool undo)
    {
        var result = undo ? _session.UndoProcessingUx3F() : _session.RedoProcessingUx3F();
        SetLocalStatusUx3F(result.Message, !result.Succeeded);
        if (result.Succeeded) { WorkspaceChanged?.Invoke(); ReloadSelectionUx3F(); }
    }

    private void MoveExternalUx3F(bool undo)
    {
        var result = undo ? _session.UndoExternalUx3F() : _session.RedoExternalUx3F();
        SetLocalStatusUx3F(result.Message, !result.Succeeded);
        if (result.Succeeded) { WorkspaceChanged?.Invoke(); ReloadSelectionUx3F(); }
    }

    private void SaveUx3F()
    {
        var result = _session.SaveAssetAuthoringUx3F();
        SetLocalStatusUx3F(result.Message, !result.Succeeded);
        if (result.Succeeded) WorkspaceChanged?.Invoke();
        UpdateButtonsUx3F();
    }

    private StudioUx3FProcessingDraft? ReadProcessingDraftUx3F()
    {
        if (_processingDraft is null ||
            !long.TryParse(_budgetBytes.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var budget) ||
            !int.TryParse(_textureDimension.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dimension) ||
            !decimal.TryParse(_textureDensity.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var density) ||
            !TryParseChannelsUx3F(_textureChannels.Text, out var channels) ||
            !TryParseDecimalsUx3F(_modelLods.Text, out var lods))
        {
            SetLocalStatusUx3F("Budget, dimension, densité, canaux ou ratios LOD invalides.", true);
            return null;
        }
        return _processingDraft with
        {
            Kind = (AssetProcessingKind)_processingKind.GetSelectedId(),
            SourceFormat = _processingDraft.SourceFormat,
            CanonicalFormat = (AssetProcessingKind)_processingKind.GetSelectedId() == AssetProcessingKind.Texture
                ? "ktx2" : "glb",
            Provenance = _provenance.Text,
            MaximumOutputBytes = budget,
            TextureColorSpace = (TextureColorSpace)_textureColorSpace.GetSelectedId(),
            TextureCompression = (TextureCompressionProfile)_textureCompression.GetSelectedId(),
            TextureMaximumDimension = dimension,
            TextureTexelDensity = density,
            TextureChannels = channels,
            ModelImportRig = _modelRig.ButtonPressed,
            ModelImportAnimations = _modelAnimations.ButtonPressed,
            ModelGenerateCollisions = _modelCollisions.ButtonPressed,
            ModelSockets = CsvUx3F(_modelSockets.Text),
            ModelLodRatios = lods,
            Approved = _processingApproval.ButtonPressed,
        };
    }

    private StudioUx3FExternalDraft? ReadExternalDraftUx3F()
    {
        if (_externalDraft is null)
        {
            SetLocalStatusUx3F("Cet asset n’a pas de source modèle et de manifeste compatibles.", true);
            return null;
        }
        return _externalDraft with
        {
            ModificationProvenance = _externalProvenance.Text,
            Operations = _operationChecks.Where(item => item.Value.ButtonPressed).Select(item => item.Key).ToArray(),
            WatchForChanges = _watch.ButtonPressed,
            TargetPlatforms = _platformChecks.Where(item => item.Value.ButtonPressed).Select(item => item.Key).ToArray(),
            Approved = _externalApproval.ButtonPressed,
        };
    }

    private void PopulateProcessingUx3F()
    {
        _updating = true;
        if (_processingDraft is null)
        {
            _processingKind.Select(-1);
            _budgetBytes.Text = string.Empty;
            _provenance.Text = string.Empty;
            _textureChannels.Text = string.Empty;
            _updating = false;
            return;
        }
        SelectIdUx3F(_processingKind, (int)_processingDraft.Kind);
        _processingKind.Disabled = true;
        _budgetBytes.Text = _processingDraft.MaximumOutputBytes.ToString(CultureInfo.InvariantCulture);
        _provenance.Text = _processingDraft.Provenance;
        SelectIdUx3F(_textureColorSpace, (int)_processingDraft.TextureColorSpace);
        SelectIdUx3F(_textureCompression, (int)_processingDraft.TextureCompression);
        _textureDimension.Text = _processingDraft.TextureMaximumDimension.ToString(CultureInfo.InvariantCulture);
        _textureDensity.Text = _processingDraft.TextureTexelDensity.ToString(CultureInfo.InvariantCulture);
        _textureChannels.Text = string.Join('\n', _processingDraft.TextureChannels.Select(item => $"{item.Key}={item.Value}"));
        _modelRig.ButtonPressed = _processingDraft.ModelImportRig;
        _modelAnimations.ButtonPressed = _processingDraft.ModelImportAnimations;
        _modelCollisions.ButtonPressed = _processingDraft.ModelGenerateCollisions;
        _modelSockets.Text = string.Join(", ", _processingDraft.ModelSockets);
        _modelLods.Text = string.Join(", ", _processingDraft.ModelLodRatios.Select(item =>
            item.ToString(CultureInfo.InvariantCulture)));
        _processingApproval.ButtonPressed = _processingDraft.Approved;
        _updating = false;
    }

    private void PopulateExternalUx3F()
    {
        _updating = true;
        if (_externalDraft is null)
        {
            _externalPaths.Text = "Édition externe indisponible · manifeste ou source locale absent.";
            _externalProvenance.Text = string.Empty;
            _watch.ButtonPressed = false;
            foreach (var check in _operationChecks.Values) check.ButtonPressed = false;
            foreach (var check in _platformChecks.Values) check.ButtonPressed = false;
            _externalApproval.ButtonPressed = false;
            _updating = false;
            return;
        }
        _externalPaths.Text =
            $"Source protégée · {StudioSessionController.HumanPathUx3F(_externalDraft.SourceRelativePath)}\n" +
            $"Sortie gouvernée · {StudioSessionController.HumanPathUx3F(_externalDraft.OutputRelativePath)}\n" +
            "Outil · Blender allowlisté · aucun exécutable lancé par l’aperçu.";
        _externalProvenance.Text = _externalDraft.ModificationProvenance;
        _watch.ButtonPressed = _externalDraft.WatchForChanges;
        foreach (var item in _operationChecks) item.Value.ButtonPressed = _externalDraft.Operations.Contains(item.Key);
        foreach (var item in _platformChecks) item.Value.ButtonPressed = _externalDraft.TargetPlatforms.Contains(item.Key);
        _externalApproval.ButtonPressed = _externalDraft.Approved;
        _updating = false;
    }

    private void WireDraftChangesUx3F()
    {
        foreach (var field in new[] { _budgetBytes, _provenance, _textureDimension, _textureDensity,
                     _modelSockets, _modelLods, _externalProvenance })
            field.TextChanged += _ => DraftChangedUx3F();
        _textureColorSpace.ItemSelected += _ => DraftChangedUx3F();
        _textureCompression.ItemSelected += _ => DraftChangedUx3F();
        _modelRig.Toggled += _ => DraftChangedUx3F();
        _modelAnimations.Toggled += _ => DraftChangedUx3F();
        _modelCollisions.Toggled += _ => DraftChangedUx3F();
        _processingApproval.Toggled += _ => DraftChangedUx3F();
        _watch.Toggled += _ => DraftChangedUx3F();
        _externalApproval.Toggled += _ => DraftChangedUx3F();
    }

    private void DraftChangedUx3F()
    {
        if (_updating) return;
        ResetPreviewsUx3F();
        UpdateButtonsUx3F();
    }

    private void ResetSelectionUx3F()
    {
        _updating = true;
        _results.DeselectAll();
        _updating = false;
        _selected = null;
        _processingDraft = null;
        _externalDraft = null;
        _explicitSelection = false;
        _selectionSummary.Text = _choices.Count == 0 ? "Aucune ressource ne correspond au filtre." :
            "Choisissez une ressource explicitement.";
        _selectionSummary.Modulate = StudioStyle.Warning;
        _identity.Text = "Aucune ressource.";
        _governance.Text = "Licence et provenance en attente.";
        _technical.Text = "Métadonnées techniques en attente.";
        _actions.Text = "Actions disponibles en attente.";
        PopulateProcessingUx3F();
        PopulateExternalUx3F();
        ResetPreviewsUx3F();
        UpdateButtonsUx3F();
    }

    private void ResetPreviewsUx3F()
    {
        _processingPreview = null;
        _externalPreview = null;
        _processingSummary.Text = _selected is null ? "Sélectionnez un asset catalogué." : "Relancez l’aperçu sec du profil.";
        _processingSummary.Modulate = StudioStyle.Muted;
        _externalSummary.Text = _selected is null ? "Sélectionnez un modèle local avec manifeste." :
            "Relancez l’aperçu sec Blender.";
        _externalSummary.Modulate = StudioStyle.Muted;
        ClearChildrenUx3F(_processingDiff);
        ClearChildrenUx3F(_processingDiagnostics);
        ClearChildrenUx3F(_externalDiff);
        ClearChildrenUx3F(_externalDiagnostics);
    }

    private void ResetUx3F(bool keepReleaseState = false)
    {
        _choices = [];
        _selected = null;
        _processingDraft = null;
        _externalDraft = null;
        _explicitSelection = false;
        _results.Clear();
        _search.Text = string.Empty;
        if (!keepReleaseState)
        {
            _evidenceReleased = true;
            _usesRealModuleEvidence = false;
            _usesCanonicalFallbackEvidence = false;
        }
        ResetSelectionUx3F();
    }

    private void UpdateButtonsUx3F()
    {
        _processingPreviewButton.Disabled = _processingDraft is null;
        _processingApplyButton.Disabled = !ProcessingPreviewReady;
        _externalPreviewButton.Disabled = _externalDraft is null;
        _externalApplyButton.Disabled = !ExternalPreviewReady;
        _undoProcessing.Disabled = !_session.CanUndoProcessingUx3F;
        _redoProcessing.Disabled = !_session.CanRedoProcessingUx3F;
        _undoExternal.Disabled = !_session.CanUndoExternalUx3F;
        _redoExternal.Disabled = !_session.CanRedoExternalUx3F;
        _save.Disabled = _selected is null;
    }

    private SourceSnapshotUx3F CaptureSourceUx3F()
    {
        var history = _sourceSession.History;
        var root = _sourceSession.Session?.RootDirectory;
        return new(
            _sourceSession.Project,
            history,
            history?.State.Revision,
            history?.Journal.ToArray() ?? [],
            _sourceSession.IsDirty,
            history?.CanUndo,
            history?.CanRedo,
            _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId,
            EditorWorkspaceProfileJsonSerializer.Serialize(_sourceSession.WorkspaceProfile),
            _sourceSession.CaptureAssetAuthoringApprovalUx3F(),
            root is null ? null : ReadOptionalFileUx3F(root, AssetProcessingProfileStore.RelativePath),
            root is null ? null : ReadOptionalFileUx3F(root, AssetExternalEditStore.RelativePath));
    }

    private bool SourceMatchesUx3F()
    {
        if (_sourceSnapshot is null) return true;
        var history = _sourceSession.History;
        var root = _sourceSession.Session?.RootDirectory;
        return ReferenceEquals(_sourceSnapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(_sourceSnapshot.History, history) &&
               history?.State.Revision == _sourceSnapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(_sourceSnapshot.Journal) &&
               _sourceSession.IsDirty == _sourceSnapshot.Dirty &&
               history?.CanUndo == _sourceSnapshot.CanUndo && history?.CanRedo == _sourceSnapshot.CanRedo &&
               _sourceSession.ActiveAreaId == _sourceSnapshot.Area &&
               _sourceSession.SelectedEntityIds.SequenceEqual(_sourceSnapshot.Selection) &&
               _sourceSession.SelectedEntityId == _sourceSnapshot.Active &&
               EditorWorkspaceProfileJsonSerializer.Serialize(_sourceSession.WorkspaceProfile) == _sourceSnapshot.Workspace &&
               _sourceSession.CaptureAssetAuthoringApprovalUx3F() == _sourceSnapshot.Approval &&
               (root is null ? null : ReadOptionalFileUx3F(root, AssetProcessingProfileStore.RelativePath)) ==
               _sourceSnapshot.ProcessingStore &&
               (root is null ? null : ReadOptionalFileUx3F(root, AssetExternalEditStore.RelativePath)) ==
               _sourceSnapshot.ExternalStore;
    }

    private void ReleaseEvidenceUx3F()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        var released = _evidenceRoot is null || StudioSessionController.DeleteTemporaryBlueprintUx3A(_evidenceRoot);
        _evidenceRoot = null;
        _session = _sourceSession;
        _evidenceReleased = released;
    }

    private bool FailAndReleaseUx3F()
    {
        ReleaseEvidenceUx3F();
        Visible = false;
        return false;
    }

    private void SetLocalStatusUx3F(string message, bool error)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Success;
        StatusChanged?.Invoke(message, error);
    }

    private static ScrollContainer ScrollTabUx3F(string title, out VBoxContainer body)
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

    private static LineEdit FieldUx3F(VBoxContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = placeholder };
        parent.AddChild(field);
        return field;
    }

    private static LineEdit GridFieldUx3F(GridContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = placeholder };
        parent.AddChild(field);
        return field;
    }

    private static OptionButton EnumOptionUx3F<T>(VBoxContainer parent, string label, Func<T, string> human)
        where T : struct, Enum
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var value in Enum.GetValues<T>()) option.AddItem(human(value), Convert.ToInt32(value, CultureInfo.InvariantCulture));
        parent.AddChild(option);
        return option;
    }

    private static OptionButton GridEnumOptionUx3F<T>(GridContainer parent, string label, Func<T, string> human)
        where T : struct, Enum
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var value in Enum.GetValues<T>()) option.AddItem(human(value), Convert.ToInt32(value, CultureInfo.InvariantCulture));
        parent.AddChild(option);
        return option;
    }

    private static void AddReviewUx3F(VBoxContainer body, out VBoxContainer diff, out VBoxContainer diagnostics)
    {
        var columns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 12);
        diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        diff.AddChild(StudioStyle.Label("DIFF SÉMANTIQUE", 9, StudioStyle.Muted));
        diagnostics.AddChild(StudioStyle.Label("DIAGNOSTICS", 9, StudioStyle.Muted));
        columns.AddChild(diff);
        columns.AddChild(diagnostics);
        body.AddChild(columns);
    }

    private static void RenderReviewUx3F(
        VBoxContainer diff,
        VBoxContainer diagnostics,
        IEnumerable<string> semantic,
        IReadOnlyList<StudioUx3FDiagnostic> issues)
    {
        ClearChildrenUx3F(diff);
        ClearChildrenUx3F(diagnostics);
        diff.AddChild(StudioStyle.Label("DIFF SÉMANTIQUE", 9, StudioStyle.Muted));
        foreach (var item in semantic) diff.AddChild(StudioStyle.Label($"• {HumanTextUx3F(item)}", 9, StudioStyle.Text, wrap: true));
        diagnostics.AddChild(StudioStyle.Label("DIAGNOSTICS", 9, StudioStyle.Muted));
        if (issues.Count == 0)
            diagnostics.AddChild(StudioStyle.Label("✓ Aucun diagnostic bloquant.", 9, StudioStyle.Success, wrap: true));
        else
            foreach (var item in issues)
                diagnostics.AddChild(StudioStyle.Label($"• {HumanTextUx3F(item.Message)}", 9, StudioStyle.Danger, wrap: true));
    }

    private static void ClearChildrenUx3F(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static bool TryParseChannelsUx3F(string text, out IReadOnlyDictionary<PbrTextureChannel, string> channels)
    {
        var result = new Dictionary<PbrTextureChannel, string>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !Enum.TryParse<PbrTextureChannel>(parts[0], ignoreCase: true, out var channel) ||
                string.IsNullOrWhiteSpace(parts[1]))
            {
                channels = new Dictionary<PbrTextureChannel, string>();
                return false;
            }
            result[channel] = parts[1];
        }
        channels = result;
        return true;
    }

    private static bool TryParseDecimalsUx3F(string text, out IReadOnlyList<decimal> values)
    {
        var result = new List<decimal>();
        foreach (var token in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                values = [];
                return false;
            }
            result.Add(value);
        }
        values = result;
        return true;
    }

    private static IReadOnlyList<string> CsvUx3F(string text) => text
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static void SelectIdUx3F(OptionButton option, int id)
    {
        var index = option.GetItemIndex(id);
        option.Select(index);
    }

    private static StudioUx3FProcessingDraft ChangedProcessingDraftUx3F(StudioUx3FProcessingDraft draft) => draft with
    {
        MaximumOutputBytes = checked(draft.MaximumOutputBytes + 4096),
        ModelGenerateCollisions = draft.Kind == AssetProcessingKind.Model && !draft.ModelGenerateCollisions,
        Approved = true,
    };

    private static StudioUx3FExternalDraft ChangedExternalDraftUx3F(StudioUx3FExternalDraft draft) => draft with
    {
        ModificationProvenance = draft.ModificationProvenance + ":ui-evidence",
        WatchForChanges = true,
        Approved = true,
    };

    private static string HumanProcessingKindUx3F(AssetProcessingKind value) =>
        value == AssetProcessingKind.Texture ? "Texture KTX2" : "Modèle GLB";

    private static string HumanPlatformUx3F(AssetTargetPlatform value) => value switch
    {
        AssetTargetPlatform.Any => "Toutes",
        AssetTargetPlatform.Windows => "Windows",
        AssetTargetPlatform.Linux => "Linux",
        AssetTargetPlatform.MacOS => "macOS",
        AssetTargetPlatform.Android => "Android",
        AssetTargetPlatform.IOS => "iOS",
        _ => value.ToString(),
    };

    private static string HumanExternalOperationUx3F(AssetExternalOperation operation) => operation switch
    {
        AssetExternalOperation.ValidateModel => "Valider le modèle",
        AssetExternalOperation.NormalizeModel => "Normaliser le modèle",
        AssetExternalOperation.GenerateCollision => "Générer les collisions",
        AssetExternalOperation.GenerateLods => "Générer les LOD",
        AssetExternalOperation.ExportGlb => "Exporter en GLB",
        AssetExternalOperation.GenerateThumbnail => "Générer la vignette",
        AssetExternalOperation.AuditQuality => "Auditer la qualité",
        _ => operation.ToString(),
    };

    private static string HumanActionUx3F(string action) => action switch
    {
        "preview" => "aperçu local",
        "plan-import" => "plan d’import sec",
        "inspect-references" => "inspecter les références",
        "translate" => "traduire",
        _ => HumanTextUx3F(action),
    };

    private static string HumanTextUx3F(string value) => Regex.Replace(
        value ?? string.Empty,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        "identifiant protégé",
        RegexOptions.CultureInvariant);

    private static string? ReadOptionalFileUx3F(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static readonly AssetExternalOperation[] BlenderOperationsUx3F =
    [
        AssetExternalOperation.ValidateModel,
        AssetExternalOperation.NormalizeModel,
        AssetExternalOperation.GenerateCollision,
        AssetExternalOperation.GenerateLods,
        AssetExternalOperation.ExportGlb,
        AssetExternalOperation.GenerateThumbnail,
        AssetExternalOperation.AuditQuality,
    ];

    private sealed record SourceSnapshotUx3F(
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
        StudioUx3FApprovalSnapshot Approval,
        string? ProcessingStore,
        string? ExternalStore);
}
