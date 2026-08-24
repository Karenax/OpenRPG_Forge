using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioAssetGovernanceWorkbenchUx3I : ColorRect
{
    private static readonly AssetOriginKind[] Origins = Enum.GetValues<AssetOriginKind>();

    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private SourceSnapshotUx3I? _sourceSnapshot;
    private string? _evidenceRoot;
    private bool _evidenceReleased = true;
    private bool _usesRealModuleEvidence;
    private bool _usesCanonicalFallbackEvidence;
    private bool _updating;
    private bool _assetExplicit;
    private bool _mediaExplicit;
    private bool _showQuarantineEvidence;
    private int _evidenceLayoutFramesRemaining;

    private readonly Label _context;
    private readonly TabContainer _tabs;
    private readonly ScrollContainer _governanceScroll;
    private readonly Control _quarantineEvidenceTarget;
    private readonly OptionButton _asset;
    private readonly Label _assetSummary;
    private readonly LineEdit _license;
    private readonly LineEdit _author;
    private readonly LineEdit _sourceUrl;
    private readonly LineEdit _permission;
    private readonly TextEdit _modifications;
    private readonly CheckBox _aiGenerated;
    private readonly LineEdit _aiModel;
    private readonly LineEdit _aiPolicyHash;
    private readonly LineEdit _budget;
    private readonly OptionButton _origin;
    private readonly CheckBox _redistribution;
    private readonly CheckBox _compatibility;
    private readonly LineEdit _legalReview;
    private readonly CheckBox _approveImport;
    private readonly LineEdit _reviewer;
    private readonly CheckBox _governanceApproval;
    private readonly Button _governancePreviewButton;
    private readonly Button _governanceApply;
    private readonly Button _governanceUndo;
    private readonly Button _governanceRedo;
    private readonly Button _governanceSave;
    private readonly Label _governanceSummary;
    private readonly VBoxContainer _governanceDiff;
    private readonly VBoxContainer _governanceDiagnostics;

    private readonly Button _loadMedia;
    private readonly CheckBox _mediaEnabled;
    private readonly CheckBox _kindImage;
    private readonly CheckBox _kindAudio;
    private readonly CheckBox _kindVfx;
    private readonly LineEdit _providers;
    private readonly LineEdit _maximumJobs;
    private readonly LineEdit _maximumBytes;
    private readonly CheckBox _fallback;
    private readonly OptionButton _jobKind;
    private readonly LineEdit _jobProvider;
    private readonly LineEdit _jobLicense;
    private readonly LineEdit _jobRights;
    private readonly LineEdit _jobBytes;
    private readonly CheckBox _jobAvailable;
    private readonly CheckBox _mediaApproval;
    private readonly Button _mediaPreviewButton;
    private readonly Button _mediaApply;
    private readonly Button _mediaUndo;
    private readonly Button _mediaRedo;
    private readonly Button _mediaSave;
    private readonly Label _mediaSummary;
    private readonly VBoxContainer _mediaDiff;
    private readonly VBoxContainer _mediaDiagnostics;
    private readonly VBoxContainer _mediaPlan;
    private readonly Label _status;

    private IReadOnlyList<StudioUx3IAssetChoice> _choices = [];
    private StudioUx3IAssetChoice? _selectedAsset;
    private StudioUx3IGovernanceDraft? _governanceDraft;
    private StudioUx3IGovernancePreview? _governancePreview;
    private StudioUx3IMediaDraft? _mediaDraft;
    private StudioUx3IMediaPreview? _mediaPreview;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? WorkspaceChanged;

    public override void _Process(double delta)
    {
        if (_evidenceLayoutFramesRemaining <= 0) return;
        _evidenceLayoutFramesRemaining--;
        if (_evidenceLayoutFramesRemaining > 0) return;
        _governanceScroll.ScrollVertical = _showQuarantineEvidence
            ? Math.Max(0, (int)_quarantineEvidenceTarget.Position.Y - 12)
            : 0;
    }

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleEvidence => _usesRealModuleEvidence;
    internal bool UsesCanonicalFallbackEvidence => _usesCanonicalFallbackEvidence;
    internal bool HasExplicitSelections => _assetExplicit && _selectedAsset is not null && _mediaExplicit;
    internal bool GovernancePreviewReady => _governancePreview is
    {
        Succeeded: true,
        CanApply: true,
        DryRunSucceeded: true,
        QuarantineDryRunSucceeded: true,
        DistributionReady: true,
        RuntimeClaimsBounded: true,
    };
    internal bool MediaPreviewReady => _mediaPreview is
    {
        Succeeded: true,
        CanApply: true,
        DryRunSucceeded: true,
        JobPlanSucceeded: true,
        RuntimeClaimsBounded: true,
    };
    internal bool EvidenceSourceUntouched => SourceMatchesUx3I();
    internal bool EvidenceResourcesReleased => _evidenceReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession);
    internal bool ShowsHumanSurface
    {
        get
        {
            var labels = FindChildren("*", "Label", true, false).Cast<Label>().Select(item => item.Text);
            var buttons = FindChildren("*", "Button", true, false).Cast<Button>().Select(item => item.Text);
            var tabs = Enumerable.Range(0, _tabs.GetTabCount()).Select(_tabs.GetTabTitle);
            var text = labels.Concat(buttons).Concat(tabs).Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
            return text.Any(item => item.Contains("Provenance et quarantaine", StringComparison.Ordinal)) &&
                   text.Any(item => item.Contains("Génération média gouvernée", StringComparison.Ordinal)) &&
                   text.Any(item => item.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase)) &&
                   text.Any(item => item.Contains("aucun fournisseur", StringComparison.OrdinalIgnoreCase)) &&
                   text.All(item => !Regex.IsMatch(item,
                       "(?i)[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
                       RegexOptions.CultureInvariant));
        }
    }

    internal StudioAssetGovernanceWorkbenchUx3I(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        Visible = false;
        Color = StudioStyle.Background;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        ZIndex = 195;

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
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("UX3-I · Provenance, quarantaine et médias gouvernés", 20, StudioStyle.Text));
        titles.AddChild(StudioStyle.Label(
            "Trois routes finales UX3 · scan fail-closed · décisions humaines · plan média sans appel fournisseur.",
            10, StudioStyle.Muted, wrap: true));
        _context = StudioStyle.Label("PROJET COURANT · choisissez explicitement un asset et la politique média.",
            10, StudioStyle.Accent, wrap: true);
        titles.AddChild(_context);
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        _tabs = new TabContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddChild(_tabs);

        _governanceScroll = CreateTabUx3I("Provenance et quarantaine", out var governance);
        _tabs.AddChild(_governanceScroll);
        governance.AddChild(StudioStyle.Label(
            "DOSSIER CIBLE · seuls PNG, JPEG, GLB et glTF ouverts sont acceptés par le parser durci.",
            10, StudioStyle.Accent, wrap: true));
        _asset = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _asset.ItemSelected += SelectAssetUx3I;
        governance.AddChild(FieldRowUx3I("Asset réel", _asset));
        _assetSummary = StudioStyle.Label("Aucune sélection.", 9, StudioStyle.Warning, wrap: true);
        governance.AddChild(_assetSummary);

        var provenance = StudioStyle.Panel("Ux3IProvenance", StudioStyle.SurfaceRaised, 8, 1);
        var provenanceMargin = StudioStyle.Margin(12, 10, 12, 10);
        provenance.AddChild(provenanceMargin);
        var provenanceBody = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        provenanceBody.AddThemeConstantOverride("separation", 7);
        provenanceMargin.AddChild(provenanceBody);
        provenanceBody.AddChild(StudioStyle.Label("PROVENANCE ET FRONTIÈRE DE DROITS", 10, StudioStyle.Accent));
        _license = AddTextUx3I(provenanceBody, "Licence SPDX", "CC0-1.0");
        _author = AddTextUx3I(provenanceBody, "Auteur", "Auteur du contenu");
        _sourceUrl = AddTextUx3I(provenanceBody, "URL source", "https://example.invalid/source");
        _permission = AddTextUx3I(provenanceBody, "Preuve de permission", "permission:author-confirmed");
        _modifications = new TextEdit { CustomMinimumSize = new Vector2(0, 66), WrapMode = TextEdit.LineWrappingMode.Boundary };
        provenanceBody.AddChild(FieldRowUx3I("Modifications", _modifications));
        var aiRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _aiGenerated = new CheckBox { Text = "Contenu généré avec IA" };
        _aiGenerated.Toggled += _ => InvalidateGovernanceUx3I();
        aiRow.AddChild(_aiGenerated);
        _redistribution = new CheckBox { Text = "Redistribution autorisée" };
        _redistribution.Toggled += _ => InvalidateGovernanceUx3I();
        aiRow.AddChild(_redistribution);
        _compatibility = new CheckBox { Text = "Compatibilité propriétaire isolée" };
        _compatibility.Toggled += _ => InvalidateGovernanceUx3I();
        aiRow.AddChild(_compatibility);
        provenanceBody.AddChild(aiRow);
        _aiModel = AddTextUx3I(provenanceBody, "Modèle IA", "provider/model-version");
        _aiPolicyHash = AddTextUx3I(provenanceBody, "SHA-256 politique de prompt", new string('d', 64));
        _budget = AddTextUx3I(provenanceBody, "Budget plateforme (octets)", "8388608");
        _origin = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        for (var index = 0; index < Origins.Length; index++)
            _origin.AddItem(HumanOriginUx3I(Origins[index]), index);
        _origin.ItemSelected += _ => InvalidateGovernanceUx3I();
        provenanceBody.AddChild(FieldRowUx3I("Origine", _origin));
        _legalReview = AddTextUx3I(provenanceBody, "Référence de revue légale", "non requise pour contenu ouvert");
        governance.AddChild(provenance);

        var quarantine = StudioStyle.Panel("Ux3IQuarantine", StudioStyle.SurfaceRaised, 8, 1);
        var quarantineMargin = StudioStyle.Margin(12, 10, 12, 10);
        quarantine.AddChild(quarantineMargin);
        var quarantineBody = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        quarantineBody.AddThemeConstantOverride("separation", 7);
        quarantineMargin.AddChild(quarantineBody);
        quarantineBody.AddChild(StudioStyle.Label("QUARANTAINE ET DÉCISION HUMAINE", 10, StudioStyle.Accent));
        quarantineBody.AddChild(StudioStyle.Label(
            "L’aperçu scanne une copie temporaire. Appliquer écrit uniquement dans .forge/assets/quarantine et le registre ; " +
            "un refus supprime immédiatement cette copie. Le dossier importé canonique n’est jamais touché ici.",
            9, StudioStyle.Muted, wrap: true));
        _approveImport = new CheckBox { Text = "Approuver la copie confinée après scan et parser" };
        _approveImport.Toggled += _ => InvalidateGovernanceUx3I();
        quarantineBody.AddChild(_approveImport);
        _reviewer = AddTextUx3I(quarantineBody, "Responsable de revue", "Studio author");
        _governanceApproval = new CheckBox { Text = "J’approuve l’aperçu sec de provenance et quarantaine" };
        _governanceApproval.Toggled += _ => InvalidateGovernanceUx3I(clearApprovalControl: false);
        quarantineBody.AddChild(_governanceApproval);
        governance.AddChild(quarantine);
        _quarantineEvidenceTarget = quarantine;

        var governanceReview = StudioStyle.Panel("Ux3IReview", StudioStyle.SurfaceRaised, 8, 1);
        var governanceReviewMargin = StudioStyle.Margin(12, 10, 12, 10);
        governanceReview.AddChild(governanceReviewMargin);
        var governanceReviewBody = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        governanceReviewBody.AddThemeConstantOverride("separation", 7);
        governanceReviewMargin.AddChild(governanceReviewBody);
        governanceReviewBody.AddChild(StudioStyle.Label("APERÇU SEC ET DIAGNOSTICS", 10, StudioStyle.Accent));
        _governanceSummary = StudioStyle.Label("Aucun aperçu.", 9, StudioStyle.Muted, wrap: true);
        governanceReviewBody.AddChild(_governanceSummary);
        _governanceDiff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        governanceReviewBody.AddChild(_governanceDiff);
        _governanceDiagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        governanceReviewBody.AddChild(_governanceDiagnostics);
        governance.AddChild(governanceReview);

        var mediaTab = CreateTabUx3I("Génération média gouvernée", out var media);
        _tabs.AddChild(mediaTab);
        media.AddChild(StudioStyle.Label(
            "POLITIQUE PROJET · cette vue planifie image, audio ou VFX mais n’appelle aucun fournisseur, " +
            "ne génère aucun octet et n’importe aucun candidat.", 10, StudioStyle.Accent, wrap: true));
        _loadMedia = StudioStyle.Button("Choisir la politique du module", compact: false);
        _loadMedia.Pressed += LoadMediaUx3I;
        media.AddChild(_loadMedia);
        _mediaEnabled = new CheckBox { Text = "Autoriser les plans de génération gouvernée" };
        _mediaEnabled.Toggled += _ => InvalidateMediaUx3I();
        media.AddChild(_mediaEnabled);
        var kinds = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _kindImage = new CheckBox { Text = "Images" };
        _kindAudio = new CheckBox { Text = "Audio" };
        _kindVfx = new CheckBox { Text = "VFX" };
        foreach (var item in new[] { _kindImage, _kindAudio, _kindVfx })
        {
            item.Toggled += _ => InvalidateMediaUx3I();
            kinds.AddChild(item);
        }
        media.AddChild(FieldRowUx3I("Types autorisés", kinds));
        _providers = AddTextUx3I(media, "Fournisseurs allowlistés (séparés par virgule)", "local");
        _maximumJobs = AddTextUx3I(media, "Tâches simultanées maximales", "8");
        _maximumBytes = AddTextUx3I(media, "Octets maximum par tâche", "268435456");
        _fallback = new CheckBox { Text = "Autoriser un fallback manuel indépendant du module" };
        _fallback.Toggled += _ => InvalidateMediaUx3I();
        media.AddChild(_fallback);

        var job = StudioStyle.Panel("Ux3IJob", StudioStyle.SurfaceRaised, 8, 1);
        var jobMargin = StudioStyle.Margin(12, 10, 12, 10);
        job.AddChild(jobMargin);
        var jobBody = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        jobBody.AddThemeConstantOverride("separation", 7);
        jobMargin.AddChild(jobBody);
        jobBody.AddChild(StudioStyle.Label("CANDIDAT À PLANIFIER", 10, StudioStyle.Accent));
        _jobKind = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _jobKind.AddItem("Image", 0); _jobKind.AddItem("Audio", 1); _jobKind.AddItem("VFX", 2);
        _jobKind.ItemSelected += _ => InvalidateMediaUx3I();
        jobBody.AddChild(FieldRowUx3I("Type", _jobKind));
        _jobProvider = AddTextUx3I(jobBody, "Fournisseur demandé", "local");
        _jobLicense = AddTextUx3I(jobBody, "Licence du candidat", "CC0-1.0");
        _jobRights = AddTextUx3I(jobBody, "Provenance des droits", "Auteur confirmé · droits vérifiés");
        _jobBytes = AddTextUx3I(jobBody, "Taille attendue (octets)", "1048576");
        _jobAvailable = new CheckBox { Text = "Fournisseur déclaré disponible pour ce plan" };
        _jobAvailable.Toggled += _ => InvalidateMediaUx3I();
        jobBody.AddChild(_jobAvailable);
        _mediaApproval = new CheckBox { Text = "J’approuve l’aperçu sec de politique et de candidat" };
        _mediaApproval.Toggled += _ => InvalidateMediaUx3I(clearApprovalControl: false);
        jobBody.AddChild(_mediaApproval);
        media.AddChild(job);

        var mediaReview = StudioStyle.Panel("Ux3IMediaReview", StudioStyle.SurfaceRaised, 8, 1);
        var mediaReviewMargin = StudioStyle.Margin(12, 10, 12, 10);
        mediaReview.AddChild(mediaReviewMargin);
        var mediaReviewBody = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        mediaReviewBody.AddThemeConstantOverride("separation", 7);
        mediaReviewMargin.AddChild(mediaReviewBody);
        _mediaSummary = StudioStyle.Label("Choisissez explicitement la politique du module.", 9, StudioStyle.Muted, wrap: true);
        mediaReviewBody.AddChild(_mediaSummary);
        _mediaDiff = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        mediaReviewBody.AddChild(_mediaDiff);
        _mediaPlan = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        mediaReviewBody.AddChild(_mediaPlan);
        _mediaDiagnostics = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        mediaReviewBody.AddChild(_mediaDiagnostics);
        media.AddChild(mediaReview);

        var footer = StudioStyle.Panel("Ux3IFooter", StudioStyle.SurfaceRaised, 8, 1);
        var footerMargin = StudioStyle.Margin(12, 9, 12, 9);
        footer.AddChild(footerMargin);
        var footerBody = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footerBody.AddThemeConstantOverride("separation", 6);
        footerMargin.AddChild(footerBody);
        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 6);
        _governancePreviewButton = StudioStyle.Button("Aperçu provenance", compact: true);
        _governancePreviewButton.Pressed += PreviewGovernanceUx3I;
        actions.AddChild(_governancePreviewButton);
        _governanceApply = StudioStyle.Button("Appliquer provenance", compact: true, primary: true);
        _governanceApply.Pressed += ApplyGovernanceUx3I;
        actions.AddChild(_governanceApply);
        _governanceUndo = StudioStyle.Button("Annuler provenance", compact: true);
        _governanceUndo.Pressed += UndoGovernanceUx3I;
        actions.AddChild(_governanceUndo);
        _governanceRedo = StudioStyle.Button("Rétablir provenance", compact: true);
        _governanceRedo.Pressed += RedoGovernanceUx3I;
        actions.AddChild(_governanceRedo);
        _governanceSave = StudioStyle.Button("Enregistrer registre", compact: true);
        _governanceSave.Pressed += SaveGovernanceUx3I;
        actions.AddChild(_governanceSave);
        actions.AddChild(new VSeparator());
        _mediaPreviewButton = StudioStyle.Button("Aperçu média", compact: true);
        _mediaPreviewButton.Pressed += PreviewMediaUx3I;
        actions.AddChild(_mediaPreviewButton);
        _mediaApply = StudioStyle.Button("Appliquer politique", compact: true, primary: true);
        _mediaApply.Pressed += ApplyMediaUx3I;
        actions.AddChild(_mediaApply);
        _mediaUndo = StudioStyle.Button("Annuler politique", compact: true);
        _mediaUndo.Pressed += UndoMediaUx3I;
        actions.AddChild(_mediaUndo);
        _mediaRedo = StudioStyle.Button("Rétablir politique", compact: true);
        _mediaRedo.Pressed += RedoMediaUx3I;
        actions.AddChild(_mediaRedo);
        _mediaSave = StudioStyle.Button("Enregistrer projet", compact: true);
        _mediaSave.Pressed += SaveMediaUx3I;
        actions.AddChild(_mediaSave);
        footerBody.AddChild(actions);
        _status = StudioStyle.Label(
            "Aperçu sec requis · quarantaine confinée · aucun fournisseur média exécuté.",
            9, StudioStyle.Muted, wrap: true);
        footerBody.AddChild(_status);
        root.AddChild(footer);

        foreach (var line in new[] { _license, _author, _sourceUrl, _permission, _aiModel, _aiPolicyHash,
                     _budget, _legalReview, _reviewer })
            line.TextChanged += _ => InvalidateGovernanceUx3I();
        _modifications.TextChanged += () => InvalidateGovernanceUx3I();
        foreach (var line in new[] { _providers, _maximumJobs, _maximumBytes, _jobProvider, _jobLicense,
                     _jobRights, _jobBytes })
            line.TextChanged += _ => InvalidateMediaUx3I();
        RefreshButtonsUx3I();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        _session = _sourceSession;
        _context.Text = "PROJET COURANT · aucun asset n’est choisi automatiquement.";
        _usesRealModuleEvidence = false;
        _usesCanonicalFallbackEvidence = false;
        Visible = true;
        PopulateAssetsUx3I();
        ResetMediaUx3I();
        RefreshButtonsUx3I();
    }

    internal void Close()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        _session = _sourceSession;
        _sourceSession.RestoreGovernanceApprovalUx3I(new(null, null, null, null));
        _sourceSession.RestoreMediaApprovalUx3I(new(null, null, null, null));
        _assetExplicit = false;
        _mediaExplicit = false;
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var plan = _sourceSession.CreateEvidencePlanUx3I();
        if (plan is null) return false;
        CaptureSourceUx3I();
        var root = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux3-i-ui-{Guid.NewGuid():N}");
        var editing = new StudioSessionController();
        _evidenceReleased = false;
        try
        {
            if (!StudioSessionController.MaterializeEvidenceUx3I(root, plan, out var choice) ||
                !editing.Open(root).Succeeded)
            {
                editing.Close();
                DeleteRootUx3I(root);
                return false;
            }
            editing.UseEvidenceContextUx3I(choice);
            _evidenceSession = editing;
            _evidenceRoot = root;
            _session = editing;
            _usesRealModuleEvidence = plan.UsesRealModule;
            _usesCanonicalFallbackEvidence = plan.UsesCanonicalFallback;
            _context.Text = plan.UsesRealModule
                ? "PREUVE ISOLÉE · copie temporaire d’un asset réel · source strictement inchangée."
                : "PREUVE ISOLÉE · fallback canonique clairement identifié · source strictement inchangée.";
            Visible = true;
            PopulateAssetsUx3I();
            if (_choices.Count == 0) return false;
            _asset.Select(0);
            SelectAssetUx3I(0);
            ConfigureGovernanceEvidenceUx3I();
            PreviewGovernanceUx3I();
            LoadMediaUx3I();
            ConfigureMediaEvidenceUx3I();
            PreviewMediaUx3I();
            SelectEvidenceSectionUx3I(mode);
            return GovernancePreviewReady && MediaPreviewReady && HasExplicitSelections &&
                   ShowsHumanSurface && SourceMatchesUx3I();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            SetStatusUx3I(exception.Message, true);
            CloseEvidence();
            return false;
        }
    }

    private void SelectEvidenceSectionUx3I(string mode)
    {
        if (mode.Contains("media-generation", StringComparison.OrdinalIgnoreCase))
        {
            _tabs.CurrentTab = 1;
            return;
        }

        _tabs.CurrentTab = 0;
        _showQuarantineEvidence = mode.Contains("quarantine", StringComparison.OrdinalIgnoreCase);
        _evidenceLayoutFramesRemaining = 2;
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is not null)
        {
            _evidenceSession.Close();
            _evidenceSession = null;
        }
        if (_evidenceRoot is not null)
        {
            _evidenceReleased = DeleteRootUx3I(_evidenceRoot);
            _evidenceRoot = null;
        }
        else _evidenceReleased = true;
        _session = _sourceSession;
        RestoreSourceApprovalsUx3I();
        _usesRealModuleEvidence = false;
        _usesCanonicalFallbackEvidence = false;
        _assetExplicit = false;
        _mediaExplicit = false;
        _governancePreview = null;
        _mediaPreview = null;
    }

    internal bool RunUiTransactionEvidence()
    {
        var ready = PrepareEvidence("ux3-i-governed-assets");
        if (!ready || _evidenceSession is null || !ReferenceEquals(_session, _evidenceSession) ||
            !GovernancePreviewReady || !MediaPreviewReady)
        {
            CloseEvidence();
            return false;
        }
        try
        {
            var governance = ReadGovernanceDraftUx3I();
            var media = ReadMediaDraftUx3I();
            if (governance is null || media is null) return false;
            var appliedGovernance = _session.ApplyApprovedGovernanceUx3I(governance).Succeeded;
            var undoGovernance = appliedGovernance && _session.UndoGovernanceUx3I().Succeeded;
            var redoGovernance = undoGovernance && _session.RedoGovernanceUx3I().Succeeded;
            var savedGovernance = redoGovernance && _session.SaveGovernanceUx3I().Succeeded;
            var appliedMedia = _session.ApplyApprovedMediaUx3I(media).Succeeded;
            var undoMedia = appliedMedia && _session.UndoMediaUx3I().Succeeded;
            var redoMedia = undoMedia && _session.RedoMediaUx3I().Succeeded;
            var savedMedia = redoMedia && _session.SaveMediaUx3I().Succeeded;
            var reopened = savedMedia && _evidenceRoot is not null ? ProjectSession.Open(_evidenceRoot) : null;
            var governanceStore = _evidenceRoot is null ? null : AssetGovernanceStore.Load(_evidenceRoot);
            return appliedGovernance && undoGovernance && redoGovernance && savedGovernance &&
                   appliedMedia && undoMedia && redoMedia && savedMedia &&
                   reopened?.Succeeded == true && governanceStore?.Succeeded == true &&
                   governanceStore.Records.Any(item => item.AssetId == governance.Asset.AssetId) &&
                   SourceMatchesUx3I();
        }
        finally
        {
            CloseEvidence();
        }
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        Visible = true;
        PopulateAssetsUx3I();
        ResetMediaUx3I();
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        Visible = false;
    }

    private void PopulateAssetsUx3I()
    {
        _updating = true;
        _choices = _session.ListGovernanceAssetsUx3I();
        _asset.Clear();
        foreach (var choice in _choices) _asset.AddItem(choice.Label);
        _selectedAsset = null;
        _governanceDraft = null;
        _governancePreview = null;
        _assetExplicit = false;
        _assetSummary.Text = _choices.Count == 0
            ? "État vide : aucun PNG/JPEG/GLB/glTF local et gouvernable n’est disponible."
            : "Choisissez explicitement un asset ; aucune première entrée n’est chargée automatiquement.";
        _governanceSummary.Text = "Aucun aperçu.";
        RenderListUx3I(_governanceDiff, []);
        RenderDiagnosticsUx3I(_governanceDiagnostics, []);
        _updating = false;
    }

    private void SelectAssetUx3I(long index)
    {
        if (_updating || index < 0 || index >= _choices.Count) return;
        _selectedAsset = _choices[(int)index];
        _assetExplicit = true;
        _governanceDraft = _session.CreateGovernanceDraftUx3I(_selectedAsset.AssetId);
        if (_governanceDraft is null)
        {
            SetStatusUx3I("Le dossier de gouvernance ne peut pas être chargé.", true);
            return;
        }
        PopulateGovernanceDraftUx3I(_governanceDraft);
        _assetSummary.Text = $"{_selectedAsset.Label} · {_selectedAsset.MediaType} · {_selectedAsset.Bytes} octets · " +
                             $"source {(_selectedAsset.UsesRealModuleSource ? "réelle" : "fallback isolé")}";
        SetStatusUx3I("Asset sélectionné ; complétez provenance et décision humaine.", false);
    }

    private void PopulateGovernanceDraftUx3I(StudioUx3IGovernanceDraft draft)
    {
        _updating = true;
        _license.Text = draft.LicenseSpdx;
        _author.Text = draft.Author;
        _sourceUrl.Text = draft.SourceUrl;
        _permission.Text = draft.PermissionEvidence;
        _modifications.Text = draft.Modifications;
        _aiGenerated.ButtonPressed = draft.AiGenerated;
        _aiModel.Text = draft.AiModel;
        _aiPolicyHash.Text = string.IsNullOrWhiteSpace(draft.AiPromptPolicySha256)
            ? new string('d', 64) : draft.AiPromptPolicySha256;
        _budget.Text = draft.PlatformBudgetBytes.ToString(CultureInfo.InvariantCulture);
        _origin.Select(Array.IndexOf(Origins, draft.Origin));
        _redistribution.ButtonPressed = draft.RedistributionAllowed;
        _compatibility.ButtonPressed = draft.CompatibilityIsolated;
        _legalReview.Text = draft.LegalReviewReference;
        _approveImport.ButtonPressed = draft.ApproveImport;
        _reviewer.Text = draft.Reviewer;
        _governanceApproval.ButtonPressed = false;
        _governancePreview = null;
        _updating = false;
        RefreshButtonsUx3I();
    }

    private StudioUx3IGovernanceDraft? ReadGovernanceDraftUx3I()
    {
        var source = _selectedAsset is null ? null : _session.CreateGovernanceDraftUx3I(_selectedAsset.AssetId);
        if (source is null || !long.TryParse(_budget.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var budget))
            return null;
        return source with
        {
            LicenseSpdx = _license.Text,
            Author = _author.Text,
            SourceUrl = _sourceUrl.Text,
            PermissionEvidence = _permission.Text,
            Modifications = _modifications.Text,
            AiGenerated = _aiGenerated.ButtonPressed,
            AiModel = _aiModel.Text,
            AiPromptPolicySha256 = _aiGenerated.ButtonPressed ? _aiPolicyHash.Text : string.Empty,
            PlatformBudgetBytes = budget,
            Origin = Origins[Math.Clamp(_origin.Selected, 0, Origins.Length - 1)],
            RedistributionAllowed = _redistribution.ButtonPressed,
            CompatibilityIsolated = _compatibility.ButtonPressed,
            LegalReviewReference = _legalReview.Text,
            ApproveImport = _approveImport.ButtonPressed,
            Reviewer = _reviewer.Text,
            Approved = _governanceApproval.ButtonPressed,
        };
    }

    private void PreviewGovernanceUx3I()
    {
        var draft = ReadGovernanceDraftUx3I();
        if (draft is null)
        {
            SetStatusUx3I("Sélection ou budget invalide.", true);
            return;
        }
        _governanceDraft = draft;
        _governancePreview = _session.PreviewGovernanceUx3I(draft);
        _governanceSummary.Text = _governancePreview.Message + "\n" +
                                  _governancePreview.ScannerLabel + "\n" + _governancePreview.QuarantinePlan;
        RenderListUx3I(_governanceDiff, _governancePreview.Diff);
        RenderDiagnosticsUx3I(_governanceDiagnostics, _governancePreview.Diagnostics);
        SetStatusUx3I(_governancePreview.Message, !_governancePreview.Succeeded);
        RefreshButtonsUx3I();
    }

    private void ApplyGovernanceUx3I()
    {
        var draft = ReadGovernanceDraftUx3I();
        if (draft is null) return;
        var result = _session.ApplyApprovedGovernanceUx3I(draft);
        SetStatusUx3I(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _governancePreview = null;
            WorkspaceChanged?.Invoke();
        }
        RefreshButtonsUx3I();
    }

    private void UndoGovernanceUx3I() => RunGovernanceActionUx3I(_session.UndoGovernanceUx3I());
    private void RedoGovernanceUx3I() => RunGovernanceActionUx3I(_session.RedoGovernanceUx3I());
    private void SaveGovernanceUx3I() => RunGovernanceActionUx3I(_session.SaveGovernanceUx3I());

    private void RunGovernanceActionUx3I(StudioUx3IActionResult result)
    {
        SetStatusUx3I(result.Message, !result.Succeeded);
        if (result.Succeeded) WorkspaceChanged?.Invoke();
        RefreshButtonsUx3I();
    }

    private void ResetMediaUx3I()
    {
        _mediaExplicit = false;
        _mediaDraft = null;
        _mediaPreview = null;
        _mediaSummary.Text = "Choisissez explicitement la politique du module.";
        RenderListUx3I(_mediaDiff, []);
        RenderListUx3I(_mediaPlan, []);
        RenderDiagnosticsUx3I(_mediaDiagnostics, []);
        RefreshButtonsUx3I();
    }

    private void LoadMediaUx3I()
    {
        _mediaDraft = _session.CreateMediaDraftUx3I(explicitlySelected: true);
        if (_mediaDraft is null)
        {
            SetStatusUx3I("La politique média du module est indisponible.", true);
            return;
        }
        _mediaExplicit = true;
        PopulateMediaDraftUx3I(_mediaDraft);
        SetStatusUx3I("Politique média choisie ; aucun fournisseur n’est appelé.", false);
    }

    private void PopulateMediaDraftUx3I(StudioUx3IMediaDraft draft)
    {
        _updating = true;
        _mediaEnabled.ButtonPressed = draft.Enabled;
        _kindImage.ButtonPressed = draft.MediaKinds.Contains("image", StringComparer.Ordinal);
        _kindAudio.ButtonPressed = draft.MediaKinds.Contains("audio", StringComparer.Ordinal);
        _kindVfx.ButtonPressed = draft.MediaKinds.Contains("vfx", StringComparer.Ordinal);
        _providers.Text = string.Join(", ", draft.Providers);
        _maximumJobs.Text = draft.MaximumJobs.ToString(CultureInfo.InvariantCulture);
        _maximumBytes.Text = draft.MaximumBytesPerJob.ToString(CultureInfo.InvariantCulture);
        _fallback.ButtonPressed = draft.ModuleIndependentFallback;
        _jobKind.Select(draft.JobKind switch { "audio" => 1, "vfx" => 2, _ => 0 });
        _jobProvider.Text = draft.JobProvider;
        _jobLicense.Text = draft.JobLicenseSpdx;
        _jobRights.Text = draft.JobRightsProvenance;
        _jobBytes.Text = draft.JobExpectedBytes.ToString(CultureInfo.InvariantCulture);
        _jobAvailable.ButtonPressed = draft.JobProviderAvailable;
        _mediaApproval.ButtonPressed = false;
        _mediaPreview = null;
        _updating = false;
        RefreshButtonsUx3I();
    }

    private StudioUx3IMediaDraft? ReadMediaDraftUx3I()
    {
        var source = _session.CreateMediaDraftUx3I(_mediaExplicit);
        if (source is null || !int.TryParse(_maximumJobs.Text, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var jobs) ||
            !long.TryParse(_maximumBytes.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes) ||
            !long.TryParse(_jobBytes.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var jobBytes))
            return null;
        var kinds = new List<string>();
        if (_kindImage.ButtonPressed) kinds.Add("image");
        if (_kindAudio.ButtonPressed) kinds.Add("audio");
        if (_kindVfx.ButtonPressed) kinds.Add("vfx");
        var providers = _providers.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return source with
        {
            Enabled = _mediaEnabled.ButtonPressed,
            MediaKinds = kinds,
            Providers = providers,
            MaximumJobs = jobs,
            MaximumBytesPerJob = bytes,
            ModuleIndependentFallback = _fallback.ButtonPressed,
            JobKind = _jobKind.Selected switch { 1 => "audio", 2 => "vfx", _ => "image" },
            JobProvider = _jobProvider.Text,
            JobLicenseSpdx = _jobLicense.Text,
            JobRightsProvenance = _jobRights.Text,
            JobExpectedBytes = jobBytes,
            JobProviderAvailable = _jobAvailable.ButtonPressed,
            Approved = _mediaApproval.ButtonPressed,
        };
    }

    private void PreviewMediaUx3I()
    {
        var draft = ReadMediaDraftUx3I();
        if (draft is null)
        {
            SetStatusUx3I("Choisissez la politique et saisissez des quotas entiers valides.", true);
            return;
        }
        _mediaDraft = draft;
        _mediaPreview = _session.PreviewMediaUx3I(draft);
        _mediaSummary.Text = _mediaPreview.Message;
        RenderListUx3I(_mediaDiff, _mediaPreview.Diff);
        RenderListUx3I(_mediaPlan, _mediaPreview.Plan);
        RenderDiagnosticsUx3I(_mediaDiagnostics, _mediaPreview.Diagnostics);
        SetStatusUx3I(_mediaPreview.Message, !_mediaPreview.Succeeded);
        RefreshButtonsUx3I();
    }

    private void ApplyMediaUx3I()
    {
        var draft = ReadMediaDraftUx3I();
        if (draft is null) return;
        var result = _session.ApplyApprovedMediaUx3I(draft);
        SetStatusUx3I(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _mediaPreview = null;
            WorkspaceChanged?.Invoke();
        }
        RefreshButtonsUx3I();
    }

    private void UndoMediaUx3I() => RunMediaActionUx3I(_session.UndoMediaUx3I());
    private void RedoMediaUx3I() => RunMediaActionUx3I(_session.RedoMediaUx3I());
    private void SaveMediaUx3I() => RunMediaActionUx3I(_session.SaveMediaUx3I());

    private void RunMediaActionUx3I(StudioUx3IActionResult result)
    {
        SetStatusUx3I(result.Message, !result.Succeeded);
        if (result.Succeeded) WorkspaceChanged?.Invoke();
        RefreshButtonsUx3I();
    }

    private void ConfigureGovernanceEvidenceUx3I()
    {
        _license.Text = "CC0-1.0";
        _author.Text = "Autrice de preuve UX3-I";
        _sourceUrl.Text = "https://openrpg-forge.invalid/evidence/ux3-i";
        _permission.Text = "permission:isolated-evidence";
        _modifications.Text = "Copie isolée ; aucune modification de la source produit.";
        _aiGenerated.ButtonPressed = false;
        _budget.Text = "8388608";
        _origin.Select(Array.IndexOf(Origins, AssetOriginKind.UserOwned));
        _redistribution.ButtonPressed = true;
        _compatibility.ButtonPressed = false;
        _legalReview.Text = "non requise pour la fixture ouverte";
        _approveImport.ButtonPressed = true;
        _reviewer.Text = "Relectrice UX3-I";
        _governanceApproval.ButtonPressed = true;
    }

    private void ConfigureMediaEvidenceUx3I()
    {
        _mediaEnabled.ButtonPressed = true;
        _kindImage.ButtonPressed = true;
        _kindAudio.ButtonPressed = true;
        _kindVfx.ButtonPressed = true;
        _providers.Text = "studio-evidence";
        _maximumJobs.Text = "8";
        _maximumBytes.Text = "268435456";
        _fallback.ButtonPressed = true;
        _jobKind.Select(0);
        _jobProvider.Text = "studio-evidence";
        _jobLicense.Text = "CC0-1.0";
        _jobRights.Text = "Auteur confirmé · preuve UX3-I isolée";
        _jobBytes.Text = "4096";
        _jobAvailable.ButtonPressed = true;
        _mediaApproval.ButtonPressed = true;
    }

    private void InvalidateGovernanceUx3I(bool clearApprovalControl = true)
    {
        if (_updating) return;
        _governancePreview = null;
        if (clearApprovalControl)
        {
            _updating = true;
            _governanceApproval.ButtonPressed = false;
            _updating = false;
        }
        _session.RestoreGovernanceApprovalUx3I(new(null, null, null, null));
        _governanceSummary.Text = "Brouillon modifié · relancez l’aperçu sec.";
        RefreshButtonsUx3I();
    }

    private void InvalidateMediaUx3I(bool clearApprovalControl = true)
    {
        if (_updating) return;
        _mediaPreview = null;
        if (clearApprovalControl)
        {
            _updating = true;
            _mediaApproval.ButtonPressed = false;
            _updating = false;
        }
        _session.RestoreMediaApprovalUx3I(new(null, null, null, null));
        _mediaSummary.Text = "Brouillon modifié · relancez l’aperçu sec.";
        RefreshButtonsUx3I();
    }

    private void RefreshButtonsUx3I()
    {
        _governancePreviewButton.Disabled = !_assetExplicit;
        _governanceApply.Disabled = !GovernancePreviewReady;
        _governanceUndo.Disabled = !_session.CanUndoGovernanceUx3I;
        _governanceRedo.Disabled = !_session.CanRedoGovernanceUx3I;
        _governanceSave.Disabled = !_assetExplicit;
        _mediaPreviewButton.Disabled = !_mediaExplicit;
        _mediaApply.Disabled = !MediaPreviewReady;
        _mediaUndo.Disabled = !_session.CanUndo;
        _mediaRedo.Disabled = !_session.CanRedo;
        _mediaSave.Disabled = !_mediaExplicit;
    }

    private void CaptureSourceUx3I()
    {
        var history = _sourceSession.History;
        _sourceSnapshot = new(_sourceSession.Project, history, history?.State.Revision,
            history?.Journal.ToArray() ?? [], _sourceSession.IsDirty, history?.CanUndo, history?.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureGovernanceApprovalUx3I(), _sourceSession.CaptureMediaApprovalUx3I());
    }

    private bool SourceMatchesUx3I()
    {
        var snapshot = _sourceSnapshot;
        if (snapshot is null) return true;
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.Dirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.Area &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               _sourceSession.SelectedEntityId == snapshot.ActiveSelection &&
               _sourceSession.CaptureGovernanceApprovalUx3I() == snapshot.GovernanceApproval &&
               _sourceSession.CaptureMediaApprovalUx3I() == snapshot.MediaApproval;
    }

    private void RestoreSourceApprovalsUx3I()
    {
        if (_sourceSnapshot is null) return;
        _sourceSession.RestoreGovernanceApprovalUx3I(_sourceSnapshot.GovernanceApproval);
        _sourceSession.RestoreMediaApprovalUx3I(_sourceSnapshot.MediaApproval);
    }

    private void SetStatusUx3I(string message, bool error)
    {
        _status.Text = message;
        _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        StatusChanged?.Invoke(message, error);
    }

    private static ScrollContainer CreateTabUx3I(string title, out VBoxContainer body)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(body);
        return scroll;
    }

    private static HBoxContainer FieldRowUx3I(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 9);
        var text = StudioStyle.Label(label, 9, StudioStyle.Muted, wrap: true);
        text.CustomMinimumSize = new Vector2(225, 0);
        row.AddChild(text);
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(control);
        return row;
    }

    private static LineEdit AddTextUx3I(VBoxContainer parent, string label, string value)
    {
        var edit = new LineEdit { Text = value, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(FieldRowUx3I(label, edit));
        return edit;
    }

    private static void RenderListUx3I(VBoxContainer container, IReadOnlyList<string> values)
    {
        foreach (var child in container.GetChildren()) child.QueueFree();
        foreach (var value in values)
            container.AddChild(StudioStyle.Label("• " + value, 9, StudioStyle.Text, wrap: true));
    }

    private static void RenderDiagnosticsUx3I(VBoxContainer container, IReadOnlyList<StudioUx3IDiagnostic> values)
    {
        foreach (var child in container.GetChildren()) child.QueueFree();
        foreach (var value in values)
            container.AddChild(StudioStyle.Label($"{value.Code} · {value.Message}", 9, StudioStyle.Warning, wrap: true));
    }

    private static string HumanOriginUx3I(AssetOriginKind origin) => origin switch
    {
        AssetOriginKind.OpenLicensed => "Licence ouverte",
        AssetOriginKind.UserOwned => "Propriété de l’utilisateur",
        AssetOriginKind.AiGenerated => "Généré avec IA",
        AssetOriginKind.ProprietaryNwn => "NWN propriétaire · redistribution interdite",
        AssetOriginKind.ProprietaryCep => "CEP propriétaire · redistribution interdite",
        _ => "Origine inconnue · distribution bloquée",
    };

    private static bool DeleteRootUx3I(string root)
    {
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux3-i-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private sealed record SourceSnapshotUx3I(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        ProjectRevision? Revision,
        IReadOnlyList<CommandHistoryJournalEntry> Journal,
        bool Dirty,
        bool? CanUndo,
        bool? CanRedo,
        Forge.Domain.Areas.AreaId? Area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? ActiveSelection,
        StudioUx3IGovernanceApprovalSnapshot GovernanceApproval,
        StudioUx3IMediaApprovalSnapshot MediaApproval);
}
