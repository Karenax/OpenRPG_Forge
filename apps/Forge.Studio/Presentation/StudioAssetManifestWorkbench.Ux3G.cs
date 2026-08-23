using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAssetManifestWorkbenchUx3G : ColorRect
{
    private static readonly AssetChunkRole[] ChunkRoles = Enum.GetValues<AssetChunkRole>();
    private static readonly AssetChunkCompression[] ChunkCompressions = Enum.GetValues<AssetChunkCompression>();
    private static readonly AssetCollisionGeneration[] CollisionGenerations = Enum.GetValues<AssetCollisionGeneration>();
    private static readonly AssetNavigationGeneration[] NavigationGenerations = Enum.GetValues<AssetNavigationGeneration>();
    private static readonly AssetAnimationCompression[] AnimationCompressions = Enum.GetValues<AssetAnimationCompression>();

    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx3G? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _usesRealModuleEvidence;
    private bool _usesCanonicalFallbackEvidence;
    private bool _updating;
    private bool _explicitSelection;

    private readonly Label _context;
    private readonly OptionButton _manifest;
    private readonly Label _selection;
    private readonly TabContainer _tabs;

    private readonly LineEdit _provenance;
    private readonly LineEdit _license;
    private readonly LineEdit _sourceHash;
    private readonly TextEdit _platformBudgets;
    private readonly OptionButton _chunk;
    private readonly OptionButton _chunkRole;
    private readonly CheckBox _chunkRequired;
    private readonly LineEdit _chunkPath;
    private readonly LineEdit _chunkHash;
    private readonly LineEdit _chunkUncompressed;
    private readonly LineEdit _chunkStored;
    private readonly OptionButton _chunkCompression;
    private readonly LineEdit _chunkPlatforms;
    private readonly Button _chunkAdd;
    private readonly Button _chunkRemove;

    private readonly CheckBox _collisionEnabled;
    private readonly CheckBox _collisionRequired;
    private readonly OptionButton _collisionChunk;
    private readonly OptionButton _collisionGeneration;
    private readonly LineEdit _collisionTags;
    private readonly LineEdit _collisionPlatforms;
    private readonly CheckBox _navigationEnabled;
    private readonly CheckBox _navigationRequired;
    private readonly OptionButton _navigationChunk;
    private readonly OptionButton _navigationGeneration;
    private readonly LineEdit _navigationTags;
    private readonly LineEdit _navigationPlatforms;

    private readonly CheckBox _animationEnabled;
    private readonly CheckBox _animationRequired;
    private readonly OptionButton _animationChunk;
    private readonly LineEdit _rig;
    private readonly LineEdit _retarget;
    private readonly OptionButton _clip;
    private readonly LineEdit _clipName;
    private readonly LineEdit _clipDuration;
    private readonly CheckBox _clipRootMotion;
    private readonly OptionButton _clipCompression;
    private readonly LineEdit _clipEvents;
    private readonly Button _clipAdd;
    private readonly Button _clipRemove;
    private readonly Button _clipUp;
    private readonly Button _clipDown;

    private readonly CheckBox _vfxEnabled;
    private readonly CheckBox _vfxRequired;
    private readonly OptionButton _vfxChunk;
    private readonly LineEdit _vfxProvider;
    private readonly LineEdit _vfxParticles;
    private readonly LineEdit _vfxDrawCalls;
    private readonly LineEdit _vfxCullDistance;
    private readonly CheckBox _lodEnabled;
    private readonly CheckBox _lodRequired;
    private readonly LineEdit _lodCrossFade;
    private readonly OptionButton _lodLevel;
    private readonly OptionButton _lodChunk;
    private readonly LineEdit _lodRatio;
    private readonly LineEdit _lodTriangles;
    private readonly Button _lodAdd;
    private readonly Button _lodRemove;
    private readonly Button _lodUp;
    private readonly Button _lodDown;

    private readonly CheckBox _approval;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Label _previewSummary;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly VBoxContainer _plans;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private readonly Label _status;

    private IReadOnlyList<StudioUx3GManifestChoice> _choices = [];
    private StudioUx3GManifestChoice? _selected;
    private StudioUx3GManifestDraft? _draft;
    private StudioUx3GManifestPreview? _manifestPreview;
    private int _chunkIndex = -1;
    private int _clipIndex = -1;
    private int _lodIndex = -1;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? WorkspaceChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleEvidence => _usesRealModuleEvidence;
    internal bool UsesCanonicalFallbackEvidence => _usesCanonicalFallbackEvidence;
    internal bool HasExplicitSelection => _explicitSelection && _selected is not null;
    internal bool ManifestPreviewReady => _manifestPreview is
    {
        Succeeded: true,
        CanApply: true,
        DryRunSucceeded: true,
        PlatformResolutionReady: true,
        VfxPlanReady: true,
        RuntimeClaimsBounded: true,
    };
    internal bool EvidenceSourceUntouched => SourceMatchesUx3G();
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
            return all.Any(item => item.Contains("Manifest et fichiers", StringComparison.Ordinal)) &&
                   all.Any(item => item.Contains("Collision et navigation", StringComparison.Ordinal)) &&
                   all.Any(item => item.Contains("Animations", StringComparison.Ordinal)) &&
                   all.Any(item => item.Contains("VFX et LOD", StringComparison.Ordinal)) &&
                   all.Any(item => item.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase)) &&
                   all.All(item => !Regex.IsMatch(item,
                       "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
                       RegexOptions.CultureInvariant));
        }
    }

    internal StudioAssetManifestWorkbenchUx3G(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        Visible = false;
        Color = StudioStyle.Background;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 193;

        var margin = StudioStyle.Margin(22, 18, 22, 18);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 9);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddThemeConstantOverride("separation", 10);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("UX3-G · Paquets d’assets de production", 20, StudioStyle.Text));
        titles.AddChild(StudioStyle.Label(
            "Manifest adressé par contenu · collision/navigation · bibliothèque d’animations · VFX et LOD.",
            10, StudioStyle.Muted, wrap: true));
        _context = StudioStyle.Label(
            "PROJET COURANT · les extensions absentes restent absentes jusqu’à une action explicite.",
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

        var selectorPanel = StudioStyle.Panel("Ux3GSelector", StudioStyle.Surface, 9, 1);
        selectorPanel.CustomMinimumSize = new Vector2(285, 0);
        var selectorMargin = StudioStyle.Margin(12, 12, 12, 12);
        selectorPanel.AddChild(selectorMargin);
        var selector = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        selector.AddThemeConstantOverride("separation", 8);
        selectorMargin.AddChild(selector);
        selector.AddChild(StudioStyle.Label("MANIFESTE CIBLE", 10, StudioStyle.Muted));
        selector.AddChild(StudioStyle.Label(
            "Choisissez explicitement un asset réel. Les IDs stables et métadonnées hors périmètre restent protégés.",
            9, StudioStyle.Text, wrap: true));
        _manifest = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _manifest.ItemSelected += SelectManifestUx3G;
        selector.AddChild(_manifest);
        _selection = StudioStyle.Label("Aucune sélection.", 9, StudioStyle.Warning, wrap: true);
        selector.AddChild(_selection);
        selector.AddChild(new HSeparator());
        selector.AddChild(StudioStyle.Label(
            "LIMITES HONNÊTES\nCette surface décrit, valide et persiste les paquets. Elle ne génère pas de mesh de collision, " +
            "de navigation, d’animation, de particules ou de LOD ; elle n’instancie aucun provider Godot et ne mesure aucun GPU.",
            9, StudioStyle.Muted, wrap: true));
        main.AddChild(selectorPanel);

        var contentPanel = StudioStyle.Panel("Ux3GContent", StudioStyle.Surface, 9, 1);
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

        var manifestTab = ScrollTabUx3G("Manifest et fichiers", out var manifestBody);
        manifestBody.AddChild(StudioStyle.Label(
            "GOUVERNANCE ET VARIANTES PLATEFORME", 10, StudioStyle.Accent));
        _provenance = FieldUx3G(manifestBody, "Provenance de modification", "author:studio:asset@1.0.0");
        _license = FieldUx3G(manifestBody, "Licence SPDX", "CC0-1.0");
        _sourceHash = FieldUx3G(manifestBody, "SHA-256 source", new string('0', 64));
        manifestBody.AddChild(StudioStyle.Label("Budgets · une ligne plateforme=octets", 9, StudioStyle.Muted));
        _platformBudgets = TextAreaUx3G("windows=1048576\nlinux=1048576", 74);
        manifestBody.AddChild(_platformBudgets);
        manifestBody.AddChild(new HSeparator());
        var chunkHeading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        chunkHeading.AddChild(StudioStyle.Label("FICHIERS ADRESSÉS PAR CONTENU", 10, StudioStyle.Accent));
        _chunkAdd = StudioStyle.Button("Ajouter", compact: true);
        _chunkAdd.Pressed += AddChunkUx3G;
        chunkHeading.AddChild(_chunkAdd);
        _chunkRemove = StudioStyle.Button("Retirer", compact: true);
        _chunkRemove.Pressed += RemoveChunkUx3G;
        chunkHeading.AddChild(_chunkRemove);
        manifestBody.AddChild(chunkHeading);
        _chunk = ChoiceUx3G(manifestBody, "Fichier sélectionné", SelectChunkUx3G);
        _chunkRole = EnumChoiceUx3G(manifestBody, "Rôle", ChunkRoles, StudioSessionController.HumanChunkRoleUx3G);
        _chunkRequired = CheckUx3G(manifestBody, "Fichier requis pour la plateforme");
        _chunkPath = FieldUx3G(manifestBody, "Chemin relatif sûr", "assets/package/file.bin");
        _chunkHash = FieldUx3G(manifestBody, "SHA-256 du fichier", new string('0', 64));
        _chunkUncompressed = FieldUx3G(manifestBody, "Octets non compressés", "1024");
        _chunkStored = FieldUx3G(manifestBody, "Octets stockés", "1024");
        _chunkCompression = EnumChoiceUx3G(manifestBody, "Compression", ChunkCompressions, HumanCompressionUx3G);
        _chunkPlatforms = FieldUx3G(manifestBody, "Plateformes (virgules)", "windows,linux");
        _tabs.AddChild(manifestTab);

        var collisionTab = ScrollTabUx3G("Collision et navigation", out var collisionBody);
        collisionBody.AddChild(StudioStyle.Label(
            "COLLISION · description uniquement, aucune géométrie générée", 10, StudioStyle.Accent));
        _collisionEnabled = CheckUx3G(collisionBody, "Activer le profil de collision");
        _collisionRequired = CheckUx3G(collisionBody, "Collision requise");
        _collisionChunk = ChoiceUx3G(collisionBody, "Fichier collision", null);
        _collisionGeneration = EnumChoiceUx3G(collisionBody, "Mode", CollisionGenerations, HumanCollisionUx3G);
        _collisionTags = FieldUx3G(collisionBody, "Matériaux (virgules)", "default");
        _collisionPlatforms = FieldUx3G(collisionBody, "Cuisson plateforme (virgules)", "windows,linux");
        collisionBody.AddChild(new HSeparator());
        collisionBody.AddChild(StudioStyle.Label(
            "NAVIGATION · chunk dédié obligatoire lorsqu’il est référencé", 10, StudioStyle.Accent));
        _navigationEnabled = CheckUx3G(collisionBody, "Activer le profil de navigation");
        _navigationRequired = CheckUx3G(collisionBody, "Navigation requise");
        _navigationChunk = ChoiceUx3G(collisionBody, "Fichier navigation", null);
        _navigationGeneration = EnumChoiceUx3G(collisionBody, "Mode", NavigationGenerations, HumanNavigationUx3G);
        _navigationTags = FieldUx3G(collisionBody, "Matériaux (virgules)", "walkable");
        _navigationPlatforms = FieldUx3G(collisionBody, "Cuisson plateforme (virgules)", "windows,linux");
        _tabs.AddChild(collisionTab);

        var animationTab = ScrollTabUx3G("Animations", out var animationBody);
        _animationEnabled = CheckUx3G(animationBody, "Activer la bibliothèque d’animations");
        _animationRequired = CheckUx3G(animationBody, "Animation requise");
        _animationChunk = ChoiceUx3G(animationBody, "Fichier animation", null);
        _rig = FieldUx3G(animationBody, "Rig", "humanoid");
        _retarget = FieldUx3G(animationBody, "Retarget map relative (optionnel)", string.Empty);
        var clipHeading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        clipHeading.AddChild(StudioStyle.Label("CLIPS ORDONNÉS", 10, StudioStyle.Accent));
        _clipAdd = StudioStyle.Button("Ajouter", compact: true);
        _clipAdd.Pressed += AddClipUx3G;
        clipHeading.AddChild(_clipAdd);
        _clipRemove = StudioStyle.Button("Retirer", compact: true);
        _clipRemove.Pressed += RemoveClipUx3G;
        clipHeading.AddChild(_clipRemove);
        _clipUp = StudioStyle.Button("Monter", compact: true);
        _clipUp.Pressed += () => MoveClipUx3G(-1);
        clipHeading.AddChild(_clipUp);
        _clipDown = StudioStyle.Button("Descendre", compact: true);
        _clipDown.Pressed += () => MoveClipUx3G(1);
        clipHeading.AddChild(_clipDown);
        animationBody.AddChild(clipHeading);
        _clip = ChoiceUx3G(animationBody, "Clip sélectionné", SelectClipUx3G);
        _clipName = FieldUx3G(animationBody, "Nom", "preview");
        _clipDuration = FieldUx3G(animationBody, "Durée (secondes)", "1");
        _clipRootMotion = CheckUx3G(animationBody, "Mouvement racine");
        _clipCompression = EnumChoiceUx3G(animationBody, "Compression", AnimationCompressions, HumanAnimationCompressionUx3G);
        _clipEvents = FieldUx3G(animationBody, "Événements ordonnés (virgules)", string.Empty);
        _tabs.AddChild(animationTab);

        var vfxTab = ScrollTabUx3G("VFX et LOD", out var vfxBody);
        vfxBody.AddChild(StudioStyle.Label(
            "VFX · plan de capacités déterministe, aucun provider exécuté", 10, StudioStyle.Accent));
        _vfxEnabled = CheckUx3G(vfxBody, "Activer le profil VFX");
        _vfxRequired = CheckUx3G(vfxBody, "Provider VFX requis");
        _vfxChunk = ChoiceUx3G(vfxBody, "Fichier VFX", null);
        _vfxProvider = FieldUx3G(vfxBody, "Provider", "forge-vfx");
        _vfxParticles = FieldUx3G(vfxBody, "Particules maximum", "2048");
        _vfxDrawCalls = FieldUx3G(vfxBody, "Appels de rendu maximum", "8");
        _vfxCullDistance = FieldUx3G(vfxBody, "Distance de masquage", "50");
        vfxBody.AddChild(new HSeparator());
        var lodHeading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        lodHeading.AddChild(StudioStyle.Label("LOD · niveaux ordonnés", 10, StudioStyle.Accent));
        _lodAdd = StudioStyle.Button("Ajouter", compact: true);
        _lodAdd.Pressed += AddLodUx3G;
        lodHeading.AddChild(_lodAdd);
        _lodRemove = StudioStyle.Button("Retirer", compact: true);
        _lodRemove.Pressed += RemoveLodUx3G;
        lodHeading.AddChild(_lodRemove);
        _lodUp = StudioStyle.Button("Monter", compact: true);
        _lodUp.Pressed += () => MoveLodUx3G(-1);
        lodHeading.AddChild(_lodUp);
        _lodDown = StudioStyle.Button("Descendre", compact: true);
        _lodDown.Pressed += () => MoveLodUx3G(1);
        lodHeading.AddChild(_lodDown);
        vfxBody.AddChild(lodHeading);
        _lodEnabled = CheckUx3G(vfxBody, "Activer les niveaux de détail");
        _lodRequired = CheckUx3G(vfxBody, "Tous les niveaux sont requis");
        _lodCrossFade = FieldUx3G(vfxBody, "Fondu croisé (secondes)", "0.15");
        _lodLevel = ChoiceUx3G(vfxBody, "Niveau sélectionné", SelectLodUx3G);
        _lodChunk = ChoiceUx3G(vfxBody, "Fichier du niveau", null);
        _lodRatio = FieldUx3G(vfxBody, "Ratio écran", "1");
        _lodTriangles = FieldUx3G(vfxBody, "Triangles maximum", "100000");
        _tabs.AddChild(vfxTab);

        var reviewTab = ScrollTabUx3G("Aperçu sec", out var reviewBody);
        reviewBody.AddChild(StudioStyle.Label(
            "Le dry-run valide le manifeste, résout les fichiers présents et calcule un plan VFX sans effet live.",
            9, StudioStyle.Muted, wrap: true));
        _approval = CheckUx3G(reviewBody, "J’approuve ce manifeste et ses budgets.");
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 8);
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _preview.Pressed += PreviewUx3G;
        actions.AddChild(_preview);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _apply.Pressed += () => ApplyUx3G();
        actions.AddChild(_apply);
        reviewBody.AddChild(actions);
        _previewSummary = StudioStyle.Label("Aucun aperçu sec.", 10, StudioStyle.Muted, wrap: true);
        reviewBody.AddChild(_previewSummary);
        reviewBody.AddChild(StudioStyle.Label("DIFF SÉMANTIQUE", 10, StudioStyle.Accent));
        _diff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reviewBody.AddChild(_diff);
        reviewBody.AddChild(StudioStyle.Label("DIAGNOSTICS CODE + CHEMIN", 10, StudioStyle.Accent));
        _diagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reviewBody.AddChild(_diagnostics);
        reviewBody.AddChild(StudioStyle.Label("PLANS DE RÉSOLUTION", 10, StudioStyle.Accent));
        _plans = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        reviewBody.AddChild(_plans);
        _tabs.AddChild(reviewTab);

        var footerNote = StudioStyle.Label(
            "Transaction locale : aperçu sec → application → historique → fichier canonique. Aucun build de paquet n’est lancé.",
            9, StudioStyle.Muted, wrap: true);
        root.AddChild(footerNote);
        var footer = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddThemeConstantOverride("separation", 8);
        _status = StudioStyle.Label("Sélectionnez un manifeste.", 9, StudioStyle.Muted, wrap: true);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(_status);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _undo.Pressed += () => MoveHistoryUx3G(undo: true);
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Pressed += () => MoveHistoryUx3G(undo: false);
        footer.AddChild(_redo);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        _save.Pressed += SaveUx3G;
        footer.AddChild(_save);
        root.AddChild(footer);

        ResetUx3G();
    }

    internal void Open() => OpenBoundUx3G(_sourceSession, evidence: false, cancelApproval: false);
    internal void RestoreSourceSurfaceAfterSmoke() => OpenBoundUx3G(_sourceSession, evidence: false, cancelApproval: false);
    internal void HideSourceSurfaceAfterSmoke()
    {
        Visible = false;
        _session = _sourceSession;
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null) return;
        if (Visible) _session.CancelManifestApprovalUx3G();
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx3G();
        _session = _sourceSession;
        ResetUx3G();
    }

    internal void CloseEvidence()
    {
        Visible = false;
        if (_evidenceSession is not null) ReleaseEvidenceUx3G();
        _session = _sourceSession;
        ResetUx3G(keepReleaseState: true);
    }

    internal bool PrepareEvidence(string mode)
    {
        if (_evidenceSession is not null) ReleaseEvidenceUx3G();
        _sourceSnapshot = CaptureSourceUx3G();
        var evidence = _sourceSession.CreateManifestEvidencePlanUx3G();
        if (evidence is null)
        {
            GD.Print("OPENRPG_FORGE_STUDIO_UX3_G_EVIDENCE_FAILED stage=plan");
            return false;
        }
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-g-ui-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        if (!ProjectSession.Create(root, evidence.Project).Succeeded ||
            !StudioSessionController.MaterializeManifestEvidenceUx3G(root, evidence) ||
            !editing.Open(root).Succeeded)
        {
            GD.Print("OPENRPG_FORGE_STUDIO_UX3_G_EVIDENCE_FAILED stage=materialize");
            editing.Close();
            StudioSessionController.DeleteTemporaryBlueprintUx3A(root);
            return false;
        }
        _evidenceSession = editing;
        _evidenceRoot = root;
        _evidenceReleased = false;
        _usesRealModuleEvidence = evidence.UsesRealModule;
        _usesCanonicalFallbackEvidence = evidence.UsesCanonicalFallback;
        OpenBoundUx3G(editing, evidence: true, cancelApproval: true);
        var index = _choices.ToList().FindIndex(item => item.AssetId == evidence.Manifest.AssetId);
        if (index < 0) return FailAndReleaseUx3G("selection");
        _manifest.Select(index + 1);
        SelectManifestUx3G(index + 1);
        if (_draft is null) return FailAndReleaseUx3G("draft");
        _provenance.Text = _provenance.Text + ":ui";
        _approval.ButtonPressed = true;
        PreviewUx3G();
        _tabs.CurrentTab = mode.Contains("collision", StringComparison.OrdinalIgnoreCase) ||
                           mode.Contains("navigation", StringComparison.OrdinalIgnoreCase)
            ? 1
            : mode.Contains("animation", StringComparison.OrdinalIgnoreCase)
                ? 2
                : mode.Contains("vfx", StringComparison.OrdinalIgnoreCase) ||
                  mode.Contains("lod", StringComparison.OrdinalIgnoreCase)
                    ? 3
                    : 4;
        var ready = ManifestPreviewReady && ShowsHumanSurface && SourceMatchesUx3G();
        if (!ready)
            GD.Print("OPENRPG_FORGE_STUDIO_UX3_G_EVIDENCE_FAILED stage=ready " +
                     $"preview={ManifestPreviewReady.ToString().ToLowerInvariant()} " +
                     $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                     $"source={SourceMatchesUx3G().ToString().ToLowerInvariant()}");
        return ready || FailAndReleaseUx3G("ready");
    }

    internal bool RunUiTransactionEvidence()
    {
        if (!PrepareEvidence("ux3-g-asset-manifest")) return false;
        if (_evidenceSession is null || _evidenceRoot is null || !ReferenceEquals(_session, _evidenceSession) ||
            _draft is null || !ManifestPreviewReady)
        {
            CloseEvidence();
            return false;
        }
        var isolated = _evidenceSession;
        var root = _evidenceRoot;
        var sourceProject = ProjectJsonSerializer.Serialize(isolated.Project!);
        var sourceStore = ReadOptionalFileUx3G(root, AssetPackageManifestStore.RelativePath);
        var applied = ApplyUx3G();
        var finalStore = ReadOptionalFileUx3G(root, AssetPackageManifestStore.RelativePath);
        var undo = isolated.UndoManifestUx3G().Succeeded;
        var undone = ReadOptionalFileUx3G(root, AssetPackageManifestStore.RelativePath) == sourceStore;
        var redo = isolated.RedoManifestUx3G().Succeeded;
        var redone = ReadOptionalFileUx3G(root, AssetPackageManifestStore.RelativePath) == finalStore;
        var saved = isolated.SaveManifestUx3G();
        var reopened = new StudioSessionController();
        var persistence = saved.Succeeded && reopened.Open(root).Succeeded &&
                          ReadOptionalFileUx3G(root, AssetPackageManifestStore.RelativePath) == finalStore &&
                          ProjectJsonSerializer.Serialize(reopened.Project!) == sourceProject;
        reopened.Close();
        CloseEvidence();
        return applied && undo && undone && redo && redone && persistence &&
               EvidenceResourcesReleased && SourceSessionRestored && SourceMatchesUx3G();
    }

    private void OpenBoundUx3G(StudioSessionController session, bool evidence, bool cancelApproval)
    {
        _session = session;
        if (cancelApproval) _session.CancelManifestApprovalUx3G();
        Visible = true;
        _context.Text = evidence
            ? _usesCanonicalFallbackEvidence
                ? "SESSION DE PREUVE ISOLÉE · manifeste canonique temporaire · source strictement inchangée."
                : "SESSION DE PREUVE ISOLÉE · manifeste réel copié, extensions navigation/VFX temporaires · source strictement inchangée."
            : "PROJET COURANT · manifests réels ; aucune extension n’est synthétisée automatiquement.";
        RefreshChoicesUx3G();
        ResetSelectionUx3G();
        UpdateButtonsUx3G();
    }

    private void RefreshChoicesUx3G(AssetCatalogItemId? retain = null)
    {
        _choices = _session.ListManifestChoicesUx3G();
        _updating = true;
        _manifest.Clear();
        _manifest.AddItem("Choisir explicitement un manifeste…");
        foreach (var choice in _choices) _manifest.AddItem(choice.Label);
        _manifest.Select(0);
        _updating = false;
        if (retain is null) return;
        var index = _choices.ToList().FindIndex(item => item.AssetId == retain);
        if (index >= 0)
        {
            _manifest.Select(index + 1);
            SelectManifestUx3G(index + 1);
        }
    }

    private void SelectManifestUx3G(long index)
    {
        if (_updating || index <= 0 || index > _choices.Count) return;
        _explicitSelection = true;
        _selected = _choices[(int)index - 1];
        _draft = _session.CreateManifestDraftUx3G(_selected.AssetId);
        _manifestPreview = null;
        if (_draft is null)
        {
            _selection.Text = "Le manifeste sélectionné est indisponible.";
            return;
        }
        _selection.Text = $"{_selected.Label}\n{_selected.SourceLabel}";
        _selection.Modulate = StudioStyle.Text;
        PopulateDraftUx3G();
        ClearPreviewUx3G();
        UpdateButtonsUx3G();
    }

    private void PopulateDraftUx3G()
    {
        if (_draft is null) return;
        _updating = true;
        _provenance.Text = _draft.ModificationProvenance;
        _license.Text = _draft.LicenseSpdx;
        _sourceHash.Text = _draft.SourceContentSha256;
        _platformBudgets.Text = string.Join('\n', _draft.PlatformBudgets.OrderBy(item => item.Key)
            .Select(item => $"{PlatformTokenUx3G(item.Key)}={item.Value.ToString(CultureInfo.InvariantCulture)}"));
        RebuildChunkChoicesUx3G();
        PopulateCollisionNavigationUx3G();
        PopulateAnimationUx3G();
        PopulateVfxLodUx3G();
        _approval.ButtonPressed = _draft.Approved;
        _updating = false;
    }

    private void RebuildChunkChoicesUx3G()
    {
        _chunk.Clear();
        foreach (var item in _draft?.Chunks ?? [])
            _chunk.AddItem($"{StudioSessionController.HumanChunkRoleUx3G(item.Role)} · {StudioSessionController.HumanPathUx3G(item.RelativePath)}");
        RebuildChunkReferenceChoiceUx3G(_collisionChunk);
        RebuildChunkReferenceChoiceUx3G(_navigationChunk);
        RebuildChunkReferenceChoiceUx3G(_animationChunk);
        RebuildChunkReferenceChoiceUx3G(_vfxChunk);
        RebuildChunkReferenceChoiceUx3G(_lodChunk);
        _chunkIndex = _draft is { Chunks.Count: > 0 } ? 0 : -1;
        if (_chunkIndex >= 0)
        {
            _chunk.Select(_chunkIndex);
            PopulateChunkUx3G(_chunkIndex);
        }
        else ClearChunkUx3G();
    }

    private void RebuildChunkReferenceChoiceUx3G(OptionButton option)
    {
        option.Clear();
        option.AddItem("Aucun fichier référencé");
        foreach (var item in _draft?.Chunks ?? [])
            option.AddItem($"{StudioSessionController.HumanChunkRoleUx3G(item.Role)} · {StudioSessionController.HumanPathUx3G(item.RelativePath)}");
    }

    private void PopulateChunkUx3G(int index)
    {
        if (_draft is null || index < 0 || index >= _draft.Chunks.Count) return;
        var item = _draft.Chunks[index];
        SelectEnumUx3G(_chunkRole, ChunkRoles, item.Role);
        _chunkRequired.ButtonPressed = item.Required;
        _chunkPath.Text = item.RelativePath;
        _chunkHash.Text = item.ContentSha256;
        _chunkUncompressed.Text = item.UncompressedBytes.ToString(CultureInfo.InvariantCulture);
        _chunkStored.Text = item.StoredBytes.ToString(CultureInfo.InvariantCulture);
        SelectEnumUx3G(_chunkCompression, ChunkCompressions, item.Compression);
        _chunkPlatforms.Text = string.Join(',', item.Platforms.Select(PlatformTokenUx3G));
    }

    private void ClearChunkUx3G()
    {
        _chunkRole.Select(0);
        _chunkRequired.ButtonPressed = false;
        _chunkPath.Text = string.Empty;
        _chunkHash.Text = string.Empty;
        _chunkUncompressed.Text = string.Empty;
        _chunkStored.Text = string.Empty;
        _chunkCompression.Select(0);
        _chunkPlatforms.Text = string.Empty;
    }

    private void PopulateCollisionNavigationUx3G()
    {
        var collision = _draft!.Collision;
        _collisionEnabled.ButtonPressed = collision is not null;
        _collisionRequired.ButtonPressed = collision?.Required ?? false;
        SelectChunkReferenceUx3G(_collisionChunk, collision?.ChunkId);
        SelectEnumUx3G(_collisionGeneration, CollisionGenerations,
            collision?.Generation ?? AssetCollisionGeneration.Embedded);
        _collisionTags.Text = string.Join(',', collision?.MaterialTags ?? []);
        _collisionPlatforms.Text = string.Join(',', (collision?.CookPlatforms ?? []).Select(PlatformTokenUx3G));
        var navigation = _draft.Navigation;
        _navigationEnabled.ButtonPressed = navigation is not null;
        _navigationRequired.ButtonPressed = navigation?.Required ?? false;
        SelectChunkReferenceUx3G(_navigationChunk, navigation?.ChunkId);
        SelectEnumUx3G(_navigationGeneration, NavigationGenerations,
            navigation?.Generation ?? AssetNavigationGeneration.Generated);
        _navigationTags.Text = string.Join(',', navigation?.MaterialTags ?? []);
        _navigationPlatforms.Text = string.Join(',', (navigation?.CookPlatforms ?? []).Select(PlatformTokenUx3G));
    }

    private void PopulateAnimationUx3G()
    {
        var animation = _draft!.Animation;
        _animationEnabled.ButtonPressed = animation is not null;
        _animationRequired.ButtonPressed = animation?.Required ?? false;
        SelectChunkReferenceUx3G(_animationChunk, animation?.ChunkId);
        _rig.Text = animation?.Rig ?? string.Empty;
        _retarget.Text = animation?.RetargetMap ?? string.Empty;
        _clip.Clear();
        foreach (var item in animation?.Clips ?? []) _clip.AddItem(ScrubUx3G(item.Name));
        _clipIndex = animation is { Clips.Count: > 0 } ? 0 : -1;
        if (_clipIndex >= 0)
        {
            _clip.Select(0);
            PopulateClipUx3G(0);
        }
        else ClearClipUx3G();
    }

    private void PopulateClipUx3G(int index)
    {
        var animation = _draft?.Animation;
        if (animation is null || index < 0 || index >= animation.Clips.Count) return;
        var item = animation.Clips[index];
        _clipName.Text = item.Name;
        _clipDuration.Text = item.DurationSeconds.ToString(CultureInfo.InvariantCulture);
        _clipRootMotion.ButtonPressed = item.RootMotion;
        SelectEnumUx3G(_clipCompression, AnimationCompressions, item.Compression);
        _clipEvents.Text = string.Join(',', item.Events);
    }

    private void ClearClipUx3G()
    {
        _clipName.Text = string.Empty;
        _clipDuration.Text = string.Empty;
        _clipRootMotion.ButtonPressed = false;
        _clipCompression.Select(0);
        _clipEvents.Text = string.Empty;
    }

    private void PopulateVfxLodUx3G()
    {
        var vfx = _draft!.Vfx;
        _vfxEnabled.ButtonPressed = vfx is not null;
        _vfxRequired.ButtonPressed = vfx?.Required ?? false;
        SelectChunkReferenceUx3G(_vfxChunk, vfx?.ChunkId);
        _vfxProvider.Text = vfx?.Provider ?? string.Empty;
        _vfxParticles.Text = vfx?.MaximumParticles.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _vfxDrawCalls.Text = vfx?.MaximumDrawCalls.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _vfxCullDistance.Text = vfx?.CullDistance.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        var lod = _draft.Lod;
        _lodEnabled.ButtonPressed = lod is not null;
        _lodRequired.ButtonPressed = lod?.Required ?? false;
        _lodCrossFade.Text = lod?.CrossFadeSeconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _lodLevel.Clear();
        foreach (var item in lod?.Levels ?? []) _lodLevel.AddItem($"Niveau {item.Level}");
        _lodIndex = lod is { Levels.Count: > 0 } ? 0 : -1;
        if (_lodIndex >= 0)
        {
            _lodLevel.Select(0);
            PopulateLodUx3G(0);
        }
        else ClearLodUx3G();
    }

    private void PopulateLodUx3G(int index)
    {
        var lod = _draft?.Lod;
        if (lod is null || index < 0 || index >= lod.Levels.Count) return;
        var item = lod.Levels[index];
        SelectChunkReferenceUx3G(_lodChunk, item.ChunkId);
        _lodRatio.Text = item.ScreenRatio.ToString(CultureInfo.InvariantCulture);
        _lodTriangles.Text = item.MaximumTriangles.ToString(CultureInfo.InvariantCulture);
    }

    private void ClearLodUx3G()
    {
        _lodChunk.Select(0);
        _lodRatio.Text = string.Empty;
        _lodTriangles.Text = string.Empty;
    }

    private bool TryReadDraftUx3G(out StudioUx3GManifestDraft? draft, out string error)
    {
        draft = null;
        error = string.Empty;
        if (_draft is null)
        {
            error = "Sélectionnez d’abord un manifeste.";
            return false;
        }
        if (!CommitCurrentChunkUx3G(out error) || !CommitCurrentClipUx3G(out error) || !CommitCurrentLodUx3G(out error))
            return false;
        try
        {
            var budgets = ParseBudgetsUx3G(_platformBudgets.Text);
            var collision = _collisionEnabled.ButtonPressed
                ? new AssetCollisionManifest(
                    _collisionRequired.ButtonPressed,
                    SelectedChunkIdUx3G(_collisionChunk),
                    SelectedEnumUx3G(_collisionGeneration, CollisionGenerations),
                    ParseStringsUx3G(_collisionTags.Text),
                    ParsePlatformsUx3G(_collisionPlatforms.Text))
                : null;
            var navigation = _navigationEnabled.ButtonPressed
                ? new AssetNavigationManifest(
                    _navigationRequired.ButtonPressed,
                    SelectedChunkIdUx3G(_navigationChunk),
                    SelectedEnumUx3G(_navigationGeneration, NavigationGenerations),
                    ParseStringsUx3G(_navigationTags.Text),
                    ParsePlatformsUx3G(_navigationPlatforms.Text))
                : null;
            var clips = _draft.Animation?.Clips ?? [];
            var animation = _animationEnabled.ButtonPressed
                ? new AssetAnimationManifest(
                    _animationRequired.ButtonPressed,
                    SelectedChunkIdUx3G(_animationChunk),
                    _rig.Text,
                    string.IsNullOrWhiteSpace(_retarget.Text) ? null : _retarget.Text,
                    clips)
                : null;
            var vfx = _vfxEnabled.ButtonPressed
                ? new AssetVfxManifest(
                    _vfxRequired.ButtonPressed,
                    SelectedChunkIdUx3G(_vfxChunk),
                    _vfxProvider.Text,
                    ParseIntUx3G(_vfxParticles.Text, "particules maximum"),
                    ParseIntUx3G(_vfxDrawCalls.Text, "appels de rendu maximum"),
                    ParseDecimalUx3G(_vfxCullDistance.Text, "distance de masquage"))
                : null;
            var lod = _lodEnabled.ButtonPressed
                ? new AssetLodManifest(
                    _lodRequired.ButtonPressed,
                    ParseDecimalUx3G(_lodCrossFade.Text, "fondu LOD"),
                    (_draft.Lod?.Levels ?? []).Select((item, index) => item with { Level = index }).ToArray())
                : null;
            draft = _draft with
            {
                ModificationProvenance = _provenance.Text,
                LicenseSpdx = _license.Text,
                SourceContentSha256 = _sourceHash.Text,
                PlatformBudgets = budgets,
                Collision = collision,
                Navigation = navigation,
                Animation = animation,
                Vfx = vfx,
                Lod = lod,
                Approved = _approval.ButtonPressed,
            };
            _draft = draft;
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private bool CommitCurrentChunkUx3G(out string error)
    {
        error = string.Empty;
        if (_draft is null || _chunkIndex < 0 || _chunkIndex >= _draft.Chunks.Count) return true;
        try
        {
            var current = _draft.Chunks[_chunkIndex];
            var replacement = current with
            {
                Role = SelectedEnumUx3G(_chunkRole, ChunkRoles),
                Required = _chunkRequired.ButtonPressed,
                RelativePath = _chunkPath.Text,
                ContentSha256 = _chunkHash.Text.Trim().ToLowerInvariant(),
                UncompressedBytes = ParseLongUx3G(_chunkUncompressed.Text, "octets non compressés"),
                StoredBytes = ParseLongUx3G(_chunkStored.Text, "octets stockés"),
                Compression = SelectedEnumUx3G(_chunkCompression, ChunkCompressions),
                Platforms = ParsePlatformsUx3G(_chunkPlatforms.Text),
            };
            var chunks = _draft.Chunks.ToArray();
            chunks[_chunkIndex] = replacement;
            _draft = _draft with { Chunks = chunks };
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private bool CommitCurrentClipUx3G(out string error)
    {
        error = string.Empty;
        var animation = _draft?.Animation;
        if (animation is null || _clipIndex < 0 || _clipIndex >= animation.Clips.Count) return true;
        try
        {
            var clips = animation.Clips.ToArray();
            clips[_clipIndex] = clips[_clipIndex] with
            {
                Name = _clipName.Text,
                DurationSeconds = ParseDecimalUx3G(_clipDuration.Text, "durée du clip"),
                RootMotion = _clipRootMotion.ButtonPressed,
                Compression = SelectedEnumUx3G(_clipCompression, AnimationCompressions),
                Events = ParseStringsUx3G(_clipEvents.Text, preserveOrder: true),
            };
            _draft = _draft! with { Animation = animation with { Clips = clips } };
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private bool CommitCurrentLodUx3G(out string error)
    {
        error = string.Empty;
        var lod = _draft?.Lod;
        if (lod is null || _lodIndex < 0 || _lodIndex >= lod.Levels.Count) return true;
        try
        {
            var levels = lod.Levels.ToArray();
            levels[_lodIndex] = levels[_lodIndex] with
            {
                ChunkId = SelectedChunkIdUx3G(_lodChunk),
                ScreenRatio = ParseDecimalUx3G(_lodRatio.Text, "ratio écran"),
                MaximumTriangles = ParseIntUx3G(_lodTriangles.Text, "triangles maximum"),
            };
            _draft = _draft! with { Lod = lod with { Levels = levels } };
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private void SelectChunkUx3G(long index)
    {
        if (_updating || _draft is null || index < 0 || index >= _draft.Chunks.Count) return;
        if (!CommitCurrentChunkUx3G(out var error))
        {
            SetStatusUx3G(error, true);
            _chunk.Select(_chunkIndex);
            return;
        }
        _chunkIndex = (int)index;
        _updating = true;
        PopulateChunkUx3G(_chunkIndex);
        _updating = false;
    }

    private void AddChunkUx3G()
    {
        if (_draft is null)
        {
            SetStatusUx3G("Sélectionnez d’abord un manifeste.", true);
            return;
        }
        if (!CommitCurrentChunkUx3G(out var error))
        {
            SetStatusUx3G(error, true);
            return;
        }
        var index = _draft.Chunks.Count + 1;
        var chunk = new AssetManifestChunk(
            Guid.NewGuid(), AssetChunkRole.Navigation, false,
            $"assets/package/fichier-{index}.bin", new string('0', 64), 1, 1,
            AssetChunkCompression.None, [AssetTargetPlatform.Windows]);
        _draft = _draft with { Chunks = _draft.Chunks.Append(chunk).ToArray() };
        _updating = true;
        RebuildChunkChoicesUx3G();
        _chunkIndex = _draft.Chunks.Count - 1;
        _chunk.Select(_chunkIndex);
        PopulateChunkUx3G(_chunkIndex);
        _updating = false;
        InvalidatePreviewUx3G();
    }

    private void RemoveChunkUx3G()
    {
        if (_draft is null || _chunkIndex < 0 || _chunkIndex >= _draft.Chunks.Count) return;
        var id = _draft.Chunks[_chunkIndex].Id;
        if (ReferencesChunkUx3G(id))
        {
            SetStatusUx3G("Ce fichier reste référencé par une extension ; retirez d’abord la référence.", true);
            return;
        }
        _draft = _draft with { Chunks = _draft.Chunks.Where((_, index) => index != _chunkIndex).ToArray() };
        _updating = true;
        RebuildChunkChoicesUx3G();
        _updating = false;
        InvalidatePreviewUx3G();
    }

    private bool ReferencesChunkUx3G(Guid id) => _draft is not null &&
        (_draft.Collision?.ChunkId == id || _draft.Navigation?.ChunkId == id ||
         _draft.Animation?.ChunkId == id || _draft.Vfx?.ChunkId == id ||
         _draft.Lod?.Levels.Any(item => item.ChunkId == id) == true ||
         _draft.ModelMetadata?.Variants.Any(item => item.ModelChunkId == id) == true ||
         _draft.Font?.PrimaryChunkId == id || _draft.Font?.Fallbacks.Any(item => item.ChunkId == id) == true);

    private void SelectClipUx3G(long index)
    {
        var animation = _draft?.Animation;
        if (_updating || animation is null || index < 0 || index >= animation.Clips.Count) return;
        if (!CommitCurrentClipUx3G(out var error))
        {
            SetStatusUx3G(error, true);
            _clip.Select(_clipIndex);
            return;
        }
        _clipIndex = (int)index;
        _updating = true;
        PopulateClipUx3G(_clipIndex);
        _updating = false;
    }

    private void AddClipUx3G()
    {
        if (_draft is null) return;
        if (!CommitCurrentClipUx3G(out var error))
        {
            SetStatusUx3G(error, true);
            return;
        }
        var animation = _draft.Animation ?? new(false, null, "generic", null, []);
        var clip = new AssetAnimationClipManifest(
            $"nouveau-clip-{animation.Clips.Count + 1}", 1m, false, AssetAnimationCompression.Balanced, []);
        _draft = _draft with { Animation = animation with { Clips = animation.Clips.Append(clip).ToArray() } };
        _animationEnabled.ButtonPressed = true;
        _updating = true;
        PopulateAnimationUx3G();
        _clipIndex = _draft.Animation.Clips.Count - 1;
        _clip.Select(_clipIndex);
        PopulateClipUx3G(_clipIndex);
        _updating = false;
        InvalidatePreviewUx3G();
    }

    private void RemoveClipUx3G()
    {
        var animation = _draft?.Animation;
        if (animation is null || _clipIndex < 0 || _clipIndex >= animation.Clips.Count) return;
        _draft = _draft! with
        {
            Animation = animation with { Clips = animation.Clips.Where((_, index) => index != _clipIndex).ToArray() },
        };
        _updating = true;
        PopulateAnimationUx3G();
        _updating = false;
        InvalidatePreviewUx3G();
    }

    private void MoveClipUx3G(int delta)
    {
        var animation = _draft?.Animation;
        if (animation is null)
        {
            SetStatusUx3G("Aucune bibliothèque d’animations sélectionnée.", true);
            return;
        }
        if (!CommitCurrentClipUx3G(out var error))
        {
            SetStatusUx3G(error, true);
            return;
        }
        var target = _clipIndex + delta;
        if (_clipIndex < 0 || target < 0 || target >= animation.Clips.Count) return;
        var clips = _draft!.Animation!.Clips.ToList();
        (clips[_clipIndex], clips[target]) = (clips[target], clips[_clipIndex]);
        _draft = _draft with { Animation = _draft.Animation with { Clips = clips } };
        _clipIndex = target;
        _updating = true;
        PopulateAnimationUx3G();
        _clipIndex = target;
        _clip.Select(target);
        PopulateClipUx3G(target);
        _updating = false;
        InvalidatePreviewUx3G();
    }

    private void SelectLodUx3G(long index)
    {
        var lod = _draft?.Lod;
        if (_updating || lod is null || index < 0 || index >= lod.Levels.Count) return;
        if (!CommitCurrentLodUx3G(out var error))
        {
            SetStatusUx3G(error, true);
            _lodLevel.Select(_lodIndex);
            return;
        }
        _lodIndex = (int)index;
        _updating = true;
        PopulateLodUx3G(_lodIndex);
        _updating = false;
    }

    private void AddLodUx3G()
    {
        if (_draft is null) return;
        if (!CommitCurrentLodUx3G(out var error))
        {
            SetStatusUx3G(error, true);
            return;
        }
        var lod = _draft.Lod ?? new(false, 0.15m, []);
        var previousRatio = lod.Levels.LastOrDefault()?.ScreenRatio ?? 1m;
        var level = new AssetLodLevelManifest(lod.Levels.Count, null,
            Math.Max(0.001m, previousRatio / 2m),
            Math.Max(1, (lod.Levels.LastOrDefault()?.MaximumTriangles ?? 100_000) / 2));
        _draft = _draft with { Lod = lod with { Levels = lod.Levels.Append(level).ToArray() } };
        _lodEnabled.ButtonPressed = true;
        _updating = true;
        PopulateVfxLodUx3G();
        _lodIndex = _draft.Lod.Levels.Count - 1;
        _lodLevel.Select(_lodIndex);
        PopulateLodUx3G(_lodIndex);
        _updating = false;
        InvalidatePreviewUx3G();
    }

    private void RemoveLodUx3G()
    {
        var lod = _draft?.Lod;
        if (lod is null || _lodIndex < 0 || _lodIndex >= lod.Levels.Count) return;
        var levels = lod.Levels.Where((_, index) => index != _lodIndex)
            .Select((item, index) => item with { Level = index }).ToArray();
        _draft = _draft! with { Lod = lod with { Levels = levels } };
        _updating = true;
        PopulateVfxLodUx3G();
        _updating = false;
        InvalidatePreviewUx3G();
    }

    private void MoveLodUx3G(int delta)
    {
        var lod = _draft?.Lod;
        if (lod is null)
        {
            SetStatusUx3G("Aucun profil LOD sélectionné.", true);
            return;
        }
        if (!CommitCurrentLodUx3G(out var error))
        {
            SetStatusUx3G(error, true);
            return;
        }
        var target = _lodIndex + delta;
        if (_lodIndex < 0 || target < 0 || target >= lod.Levels.Count) return;
        var levels = _draft!.Lod!.Levels.ToList();
        (levels[_lodIndex], levels[target]) = (levels[target], levels[_lodIndex]);
        levels = levels.Select((item, index) => item with { Level = index }).ToList();
        _draft = _draft with { Lod = _draft.Lod with { Levels = levels } };
        _lodIndex = target;
        _updating = true;
        PopulateVfxLodUx3G();
        _lodIndex = target;
        _lodLevel.Select(target);
        PopulateLodUx3G(target);
        _updating = false;
        InvalidatePreviewUx3G();
    }

    private void PreviewUx3G()
    {
        if (!TryReadDraftUx3G(out var draft, out var error) || draft is null)
        {
            RenderReadErrorUx3G(error);
            return;
        }
        _manifestPreview = _session.PreviewManifestUx3G(draft);
        RenderPreviewUx3G();
        SetStatusUx3G(_manifestPreview.Message, !_manifestPreview.Succeeded);
        UpdateButtonsUx3G();
    }

    private bool ApplyUx3G()
    {
        if (!TryReadDraftUx3G(out var draft, out var error) || draft is null)
        {
            RenderReadErrorUx3G(error);
            return false;
        }
        var assetId = draft.AssetId;
        var result = _session.ApplyApprovedManifestUx3G(draft);
        SetStatusUx3G(result.Message, !result.Succeeded);
        if (!result.Succeeded) return false;
        RefreshChoicesUx3G(assetId);
        WorkspaceChanged?.Invoke();
        return true;
    }

    private void MoveHistoryUx3G(bool undo)
    {
        var assetId = _selected?.AssetId;
        var result = undo ? _session.UndoManifestUx3G() : _session.RedoManifestUx3G();
        SetStatusUx3G(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            RefreshChoicesUx3G(assetId);
            WorkspaceChanged?.Invoke();
        }
        UpdateButtonsUx3G();
    }

    private void SaveUx3G()
    {
        var result = _session.SaveManifestUx3G();
        SetStatusUx3G(result.Message, !result.Succeeded);
    }

    private void RenderPreviewUx3G()
    {
        ClearContainerUx3G(_diff);
        ClearContainerUx3G(_diagnostics);
        ClearContainerUx3G(_plans);
        if (_manifestPreview is null)
        {
            _previewSummary.Text = "Aucun aperçu sec.";
            return;
        }
        _previewSummary.Text = $"{_manifestPreview.Message}\n{_manifestPreview.PatchSummary} · inverse : {_manifestPreview.InverseSummary}";
        _previewSummary.Modulate = _manifestPreview.Succeeded ? StudioStyle.Text : StudioStyle.Danger;
        foreach (var item in _manifestPreview.SemanticDiff)
            _diff.AddChild(StudioStyle.Label($"• {ScrubUx3G(item)}", 9, StudioStyle.Text, wrap: true));
        foreach (var item in _manifestPreview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label(
                $"{ScrubUx3G(item.Code)} · {HumanDiagnosticPathUx3G(item.Path)}\n{ScrubUx3G(item.Message)}",
                9, StudioStyle.Danger, wrap: true));
        if (_manifestPreview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 9, StudioStyle.Accent, wrap: true));
        foreach (var item in _manifestPreview.ChunkPlan.Take(12))
            _plans.AddChild(StudioStyle.Label($"• {ScrubUx3G(item)}", 9, StudioStyle.Text, wrap: true));
        foreach (var item in _manifestPreview.AnimationPlan.Take(8))
            _plans.AddChild(StudioStyle.Label($"• Animation · {ScrubUx3G(item)}", 9, StudioStyle.Text, wrap: true));
        foreach (var item in _manifestPreview.VfxPlan)
            _plans.AddChild(StudioStyle.Label($"• {ScrubUx3G(item)}", 9, StudioStyle.Text, wrap: true));
    }

    private void RenderReadErrorUx3G(string error)
    {
        _manifestPreview = null;
        _previewSummary.Text = error;
        _previewSummary.Modulate = StudioStyle.Danger;
        ClearContainerUx3G(_diagnostics);
        _diagnostics.AddChild(StudioStyle.Label(ScrubUx3G(error), 9, StudioStyle.Danger, wrap: true));
        SetStatusUx3G(error, true);
        UpdateButtonsUx3G();
    }

    private void ClearPreviewUx3G()
    {
        _manifestPreview = null;
        _previewSummary.Text = "Aucun aperçu sec.";
        _previewSummary.Modulate = StudioStyle.Muted;
        ClearContainerUx3G(_diff);
        ClearContainerUx3G(_diagnostics);
        ClearContainerUx3G(_plans);
    }

    private void InvalidatePreviewUx3G()
    {
        _session.CancelManifestApprovalUx3G();
        _approval.ButtonPressed = false;
        ClearPreviewUx3G();
        UpdateButtonsUx3G();
    }

    private void UpdateButtonsUx3G()
    {
        var selected = _draft is not null;
        _preview.Disabled = !selected;
        _apply.Disabled = !selected || !ManifestPreviewReady;
        _undo.Disabled = !_session.CanUndoManifestUx3G;
        _redo.Disabled = !_session.CanRedoManifestUx3G;
        _save.Disabled = !selected;
        _chunkAdd.Disabled = !selected;
        _chunkRemove.Disabled = !selected || _chunkIndex < 0;
        _clipAdd.Disabled = !selected;
        _clipRemove.Disabled = !selected || _clipIndex < 0;
        _clipUp.Disabled = _clipIndex <= 0;
        _clipDown.Disabled = _draft?.Animation is null || _clipIndex < 0 || _clipIndex >= _draft.Animation.Clips.Count - 1;
        _lodAdd.Disabled = !selected;
        _lodRemove.Disabled = !selected || _lodIndex < 0;
        _lodUp.Disabled = _lodIndex <= 0;
        _lodDown.Disabled = _draft?.Lod is null || _lodIndex < 0 || _lodIndex >= _draft.Lod.Levels.Count - 1;
    }

    private void ResetSelectionUx3G()
    {
        _explicitSelection = false;
        _selected = null;
        _draft = null;
        _manifestPreview = null;
        _chunkIndex = _clipIndex = _lodIndex = -1;
        _updating = true;
        _manifest.Select(0);
        _selection.Text = _choices.Count == 0
            ? "Aucun manifeste : l’état vide est conservé."
            : $"{_choices.Count} manifeste(s) réel(s) · aucune sélection automatique.";
        _selection.Modulate = _choices.Count == 0 ? StudioStyle.Warning : StudioStyle.Muted;
        _updating = false;
        ClearPreviewUx3G();
        UpdateButtonsUx3G();
    }

    private void ResetUx3G(bool keepReleaseState = false)
    {
        _choices = [];
        _selected = null;
        _draft = null;
        _manifestPreview = null;
        _explicitSelection = false;
        _chunkIndex = _clipIndex = _lodIndex = -1;
        _updating = true;
        _manifest.Clear();
        _manifest.AddItem("Choisir explicitement un manifeste…");
        _selection.Text = "Aucune sélection.";
        _updating = false;
        if (!keepReleaseState) _evidenceReleased = _evidenceSession is null;
        ClearPreviewUx3G();
        UpdateButtonsUx3G();
    }

    private SourceSnapshotUx3G CaptureSourceUx3G()
    {
        var history = _sourceSession.History;
        var root = _sourceSession.Session?.RootDirectory;
        return new(
            _sourceSession.Project,
            history,
            history?.State.Revision,
            history?.Journal.ToArray() ?? [],
            history?.CanUndo,
            history?.CanRedo,
            _sourceSession.IsDirty,
            _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId,
            _sourceSession.CaptureManifestApprovalUx3G(),
            root,
            root is null ? null : ReadOptionalFileUx3G(root, AssetPackageManifestStore.RelativePath));
    }

    private bool SourceMatchesUx3G()
    {
        if (_sourceSnapshot is null) return true;
        var snapshot = _sourceSnapshot;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               history?.CanUndo == snapshot.CanUndo && history?.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.Dirty &&
               _sourceSession.ActiveAreaId == snapshot.Area &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               _sourceSession.SelectedEntityId == snapshot.Active &&
               _sourceSession.CaptureManifestApprovalUx3G() == snapshot.Approval &&
               (snapshot.Root is null ? null : ReadOptionalFileUx3G(snapshot.Root, AssetPackageManifestStore.RelativePath)) ==
               snapshot.ManifestStore;
    }

    private bool FailAndReleaseUx3G(string stage)
    {
        GD.Print($"OPENRPG_FORGE_STUDIO_UX3_G_EVIDENCE_FAILED stage={stage}");
        CloseEvidence();
        return false;
    }

    private void ReleaseEvidenceUx3G()
    {
        _evidenceSession?.Close();
        _evidenceSession = null;
        var released = _evidenceRoot is null || StudioSessionController.DeleteTemporaryBlueprintUx3A(_evidenceRoot);
        _evidenceRoot = null;
        _evidenceReleased = released;
        _usesRealModuleEvidence = false;
        _usesCanonicalFallbackEvidence = false;
    }

    private void SetStatusUx3G(string message, bool error)
    {
        _status.Text = ScrubUx3G(message);
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(_status.Text, error);
    }

    private ScrollContainer ScrollTabUx3G(string title, out VBoxContainer body)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(body);
        return scroll;
    }

    private static LineEdit FieldUx3G(VBoxContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(field);
        return field;
    }

    private static TextEdit TextAreaUx3G(string placeholder, float height) => new()
    {
        PlaceholderText = placeholder,
        CustomMinimumSize = new Vector2(0, height),
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        WrapMode = TextEdit.LineWrappingMode.None,
        ScrollFitContentHeight = false,
    };

    private static CheckBox CheckUx3G(VBoxContainer parent, string text)
    {
        var check = new CheckBox { Text = text };
        parent.AddChild(check);
        return check;
    }

    private static OptionButton ChoiceUx3G(VBoxContainer parent, string label, Action<long>? selected)
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        if (selected is not null) option.ItemSelected += index => selected(index);
        parent.AddChild(option);
        return option;
    }

    private static OptionButton EnumChoiceUx3G<T>(
        VBoxContainer parent,
        string label,
        IReadOnlyList<T> values,
        Func<T, string> human)
    {
        var option = ChoiceUx3G(parent, label, null);
        foreach (var value in values) option.AddItem(human(value));
        return option;
    }

    private void SelectChunkReferenceUx3G(OptionButton option, Guid? id)
    {
        var index = id is null || _draft is null ? -1 : _draft.Chunks.ToList().FindIndex(item => item.Id == id);
        option.Select(index < 0 ? 0 : index + 1);
    }

    private Guid? SelectedChunkIdUx3G(OptionButton option)
    {
        if (_draft is null || option.Selected <= 0 || option.Selected > _draft.Chunks.Count) return null;
        return _draft.Chunks[option.Selected - 1].Id;
    }

    private static void SelectEnumUx3G<T>(OptionButton option, IReadOnlyList<T> values, T value) where T : struct, Enum
    {
        var index = values.ToList().FindIndex(item => EqualityComparer<T>.Default.Equals(item, value));
        option.Select(Math.Max(0, index));
    }

    private static T SelectedEnumUx3G<T>(OptionButton option, IReadOnlyList<T> values) where T : struct, Enum =>
        option.Selected >= 0 && option.Selected < values.Count ? values[option.Selected] : values[0];

    private static IReadOnlyDictionary<AssetTargetPlatform, long> ParseBudgetsUx3G(string value)
    {
        var result = new Dictionary<AssetTargetPlatform, long>();
        foreach (var line in value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !TryParsePlatformUx3G(parts[0], out var platform) ||
                !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes))
                throw new ArgumentException($"Budget invalide : {ScrubUx3G(line)}.");
            if (!result.TryAdd(platform, bytes)) throw new ArgumentException("Chaque plateforme ne peut apparaître qu’une fois.");
        }
        return result;
    }

    private static IReadOnlyList<AssetTargetPlatform> ParsePlatformsUx3G(string value)
    {
        var result = new List<AssetTargetPlatform>();
        foreach (var token in ParseStringsUx3G(value))
        {
            if (!TryParsePlatformUx3G(token, out var platform))
                throw new ArgumentException($"Plateforme inconnue : {ScrubUx3G(token)}.");
            if (!result.Contains(platform)) result.Add(platform);
        }
        return result;
    }

    private static bool TryParsePlatformUx3G(string value, out AssetTargetPlatform platform)
    {
        var token = value.Trim().Replace("-", string.Empty).ToLowerInvariant();
        platform = token switch
        {
            "any" or "toutes" => AssetTargetPlatform.Any,
            "windows" => AssetTargetPlatform.Windows,
            "linux" => AssetTargetPlatform.Linux,
            "macos" or "mac" => AssetTargetPlatform.MacOS,
            "android" => AssetTargetPlatform.Android,
            "ios" => AssetTargetPlatform.IOS,
            _ => default,
        };
        return token is "any" or "toutes" or "windows" or "linux" or "macos" or "mac" or "android" or "ios";
    }

    private static string PlatformTokenUx3G(AssetTargetPlatform value) => value switch
    {
        AssetTargetPlatform.Any => "any",
        AssetTargetPlatform.Windows => "windows",
        AssetTargetPlatform.Linux => "linux",
        AssetTargetPlatform.MacOS => "macos",
        AssetTargetPlatform.Android => "android",
        AssetTargetPlatform.IOS => "ios",
        _ => "any",
    };

    private static IReadOnlyList<string> ParseStringsUx3G(string value, bool preserveOrder = false)
    {
        var items = value.Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item));
        return preserveOrder
            ? items.Distinct(StringComparer.Ordinal).ToArray()
            : items.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static int ParseIntUx3G(string value, string label) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new ArgumentException($"{label} doit être un entier.");

    private static long ParseLongUx3G(string value, string label) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new ArgumentException($"{label} doit être un entier.");

    private static decimal ParseDecimalUx3G(string value, string label) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new ArgumentException($"{label} doit être un nombre au format invariant.");

    private static string HumanCompressionUx3G(AssetChunkCompression value) => value switch
    {
        AssetChunkCompression.None => "Aucune",
        AssetChunkCompression.Brotli => "Brotli",
        AssetChunkCompression.Zstandard => "Zstandard",
        _ => "Compression",
    };

    private static string HumanCollisionUx3G(AssetCollisionGeneration value) => value switch
    {
        AssetCollisionGeneration.Embedded => "Intégrée au modèle",
        AssetCollisionGeneration.ConvexDecomposition => "Décomposition convexe",
        AssetCollisionGeneration.Primitive => "Primitive",
        AssetCollisionGeneration.Manual => "Fichier manuel",
        _ => "Collision",
    };

    private static string HumanNavigationUx3G(AssetNavigationGeneration value) => value switch
    {
        AssetNavigationGeneration.Generated => "Générée par le pipeline",
        AssetNavigationGeneration.Embedded => "Intégrée",
        AssetNavigationGeneration.Manual => "Fichier manuel",
        _ => "Navigation",
    };

    private static string HumanAnimationCompressionUx3G(AssetAnimationCompression value) => value switch
    {
        AssetAnimationCompression.Lossless => "Sans perte",
        AssetAnimationCompression.Balanced => "Équilibrée",
        AssetAnimationCompression.Aggressive => "Agressive",
        _ => "Compression",
    };

    private static string HumanDiagnosticPathUx3G(string path)
    {
        var last = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "manifeste";
        return last switch
        {
            "governance" => "Gouvernance",
            "chunks" => "Fichiers",
            "platformBudgets" => "Budgets plateforme",
            "collision" => "Collision",
            "navigation" => "Navigation",
            "animation" => "Animations",
            "vfx" => "VFX",
            "lod" => "LOD",
            "chunkId" => "Référence de fichier",
            _ => ScrubUx3G(last),
        };
    }

    private static string ScrubUx3G(string value) => Regex.Replace(
        value ?? string.Empty,
        "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        "identifiant protégé",
        RegexOptions.CultureInvariant).Trim();

    private static void ClearContainerUx3G(Node container)
    {
        foreach (var child in container.GetChildren()) child.QueueFree();
    }

    private static string? ReadOptionalFileUx3G(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private sealed record SourceSnapshotUx3G(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool? CanUndo,
        bool? CanRedo,
        bool Dirty,
        Forge.Domain.Areas.AreaId? Area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? Active,
        StudioUx3GApprovalSnapshot Approval,
        string? Root,
        string? ManifestStore);
}
