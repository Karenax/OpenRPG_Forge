using System.Text.RegularExpressions;
using Forge.Domain.Quality;
using Forge.ModuleFormat.Network;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Observability;
using Forge.Network.Operations;
using Forge.Network.World;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioNetworkClosureWorkbenchUx9O : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioUx9OSourceSnapshot? _sourceSnapshot;
    private StudioUx9ONetworkClosureDraft? _draft;
    private StudioUx9ONetworkClosurePreview? _preview;
    private bool _explicitSelection;
    private bool _updating;
    private bool _canUndo;
    private bool _canRedo;
    private bool _canSave;
    private bool _evidenceResourcesReleased = true;
    private bool _sourceSessionRestored = true;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private LineEdit _serverName = null!;
    private LineEdit _serverHost = null!;
    private SpinBox _serverPort = null!;
    private SpinBox _serverCapacity = null!;
    private CheckBox _dedicatedRequired = null!;
    private OptionButton _serviceMode = null!;
    private LineEdit _healthPath = null!;
    private CheckBox _pvpEnabled = null!;
    private OptionButton _friendlyFire = null!;
    private CheckBox _globalCombat = null!;
    private CheckBox _areaOverrides = null!;
    private CheckBox _partyOverrides = null!;
    private CheckBox _consent = null!;
    private CheckBox _duels = null!;
    private CheckBox _crime = null!;
    private CheckBox _safeZones = null!;
    private CheckBox _serverOverride = null!;
    private OptionButton _logSeverity = null!;
    private LineEdit _logCategories = null!;
    private OptionButton _privacy = null!;
    private OptionButton _logFormat = null!;
    private SpinBox _retention = null!;
    private SpinBox _maximumEntries = null!;
    private SpinBox _rotationBytes = null!;
    private SpinBox _rotationFiles = null!;
    private LineEdit _sinkName = null!;
    private OptionButton _sinkKind = null!;
    private LineEdit _sinkEndpoint = null!;
    private CheckBox _sinkEnabled = null!;
    private CheckBox _sinkRequired = null!;
    private SpinBox _maximumSpeed = null!;
    private SpinBox _intentRate = null!;
    private SpinBox _anomalyThreshold = null!;
    private CheckBox _signedContent = null!;
    private LineEdit _trustedSigners = null!;
    private CheckBox _authoritativeRandom = null!;
    private CheckBox _trustClientSaves = null!;
    private SpinBox _randomSeed = null!;
    private CheckBox _approval = null!;
    private CheckBox _sandboxConfirmation = null!;
    private Label _previewSummary = null!;
    private Label _auditSummary = null!;
    private Label _status = null!;
    private Button _previewButton = null!;
    private Button _applyButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _saveButton = null!;
    private Button _sandboxButton = null!;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? WorkspaceChanged;
    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                          ReferenceEquals(_session, _evidenceSession);
    internal bool HasExplicitSelection => _explicitSelection;
    internal bool PreviewReady => _preview is
    { Succeeded: true, CanApply: true, CanonicalDryRun: true, RuntimeReady: true };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx9O();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx9O(this));
            return Visible && new[] { "ORF-MP-001", "ORF-MP-008", "ORF-MP-029", "ORF-MP-031" }
                       .All(route => text.Contains(route, StringComparison.Ordinal)) &&
                   text.Contains("headless", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("consentement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("confidentialité", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("RNG autoritaire", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tester localement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun port, audio ou service externe",
                       StringComparison.OrdinalIgnoreCase) && !ContainsUuidUx9O(text);
        }
    }

    internal StudioNetworkClosureWorkbenchUx9O(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx9O();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9O();
    }

    internal void Close()
    {
        if (_evidenceSession is not null) { CloseEvidence(); return; }
        if (!Visible) return;
        Visible = false;
        _session.CancelNetworkClosureApprovalUx9O();
        ResetUx9O();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx9O(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-o-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !WorkspaceNetworkFoundationStore.Save(_evidenceRoot,
                NetworkFoundationProfile.CreateDefault("evidence:ux9-o-ui")).Succeeded ||
            !WorkspaceNetworkPlayerExperienceStore.Save(_evidenceRoot,
                NetworkPlayerExperienceProfile.CreateDefault("evidence:ux9-o-ui")).Succeeded ||
            !WorkspaceNetworkAssuranceStore.Save(_evidenceRoot,
                NetworkAssuranceProfile.CreateDefault("evidence:ux9-o-ui")).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx9O();
            RestoreSourceSessionUx9O();
            return false;
        }
        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx9O();
        _moduleChoice.Select(1);
        SelectModuleUx9O(1);
        ConfigureEvidenceUx9O(mode);
        PreviewCurrentUx9O();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady &&
                    ShowsHumanSurface && EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_O_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
                 $"isolated={UsesIsolatedEvidence.ToString().ToLowerInvariant()} " +
                 $"selection={HasExplicitSelection.ToString().ToLowerInvariant()} " +
                 $"preview={PreviewReady.ToString().ToLowerInvariant()} " +
                 $"human={ShowsHumanSurface.ToString().ToLowerInvariant()} " +
                 $"source_untouched={EvidenceSourceUntouched.ToString().ToLowerInvariant()}");
        if (!ready) CloseEvidence();
        return ready;
    }

    internal bool RunUiEvidence()
    {
        if (!UsesIsolatedEvidence || !PreviewReady || _evidenceRoot is null) return false;
        var draft = ReadDraftUx9O();
        if (draft is null) return false;
        var preview = _session.PreviewNetworkClosureUx9O(draft);
        var applied = preview.CanApply && _approval.ButtonPressed &&
                      _session.ApplyApprovedNetworkClosureUx9O(draft).Succeeded;
        var undo = applied && _session.UndoNetworkClosureUx9O().Succeeded;
        var redo = undo && _session.RedoNetworkClosureUx9O().Succeeded;
        var save = redo && _session.SaveNetworkClosureUx9O().Succeeded;
        var probed = save && _sandboxConfirmation.ButtonPressed &&
                     _session.RunLocalNetworkClosureUx9O(true,
                         NetworkRole.Administrator).Succeeded;
        var foundation = _session.CurrentNetworkFoundationUx9A;
        var player = _session.CurrentNetworkExperienceUx8M;
        var assurance = _session.CurrentNetworkAssuranceUx9O;
        var reopened = new StudioSessionController();
        var reopenedOk = probed && foundation is not null && player is not null &&
                         assurance is not null && reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.CurrentNetworkFoundationUx9A is { } actualFoundation &&
                         reopened.CurrentNetworkExperienceUx8M is { } actualPlayer &&
                         reopened.CurrentNetworkAssuranceUx9O is { } actualAssurance &&
                         new NetworkFoundationStore().Serialize(foundation) ==
                         new NetworkFoundationStore().Serialize(actualFoundation) &&
                         new NetworkPlayerExperienceStore().Serialize(player) ==
                         new NetworkPlayerExperienceStore().Serialize(actualPlayer) &&
                         new NetworkAssuranceStore().Serialize(assurance) ==
                         new NetworkAssuranceStore().Serialize(actualAssurance);
        reopened.Close();
        RefreshAuditUx9O();
        return preview.CanonicalDryRun && applied && undo && redo && save && probed &&
               reopenedOk && _session.NetworkClosureProbeUx9O.ResourcesReleased &&
               EvidenceSourceUntouched;
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is not null)
        {
            _evidenceSession.Close();
            _evidenceSession = null;
        }
        Visible = false;
        ResetUx9O();
        DeleteEvidenceUx9O();
        RestoreSourceSessionUx9O();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9O();
        _moduleChoice.Select(1);
        SelectModuleUx9O(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx9O();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx9O;
        UpdateBoundsUx9O();
    }

    public override void _ExitTree() => CloseEvidence();

    private void BuildUiUx9O()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color("10121af2");
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _dialog = StudioStyle.Panel("Ux9ONetworkClosure", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var root = new VBoxContainer
        {
            Name = "Ux9ORoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 5);
        _dialog.AddChild(root);
        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(headingText);
        headingText.AddChild(StudioStyle.Label("Clôture de production réseau", 20,
            StudioStyle.Text));
        headingText.AddChild(StudioStyle.Label(
            "Quatre politiques canoniques, testées dans une sandbox locale libérée.", 11,
            StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);

        _moduleChoice = ChoiceUx9O("Sélectionner un module");
        _moduleChoice.ItemSelected += index => SelectModuleUx9O((int)index);
        root.AddChild(LabeledRowUx9O("Module explicitement ciblé", _moduleChoice));

        var policies = new GridContainer
        {
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(0, 520)
        };
        policies.AddThemeConstantOverride("h_separation", 7);
        root.AddChild(policies);

        var dedicated = PolicyCardUx9O(policies, "ORF-MP-001", "Serveur dédié",
            "Headless, sans dépendance éditeur, santé et mode service.");
        _serverName = TextUx9O(dedicated, "Nom", "Serveur OpenRPG Forge");
        _serverHost = TextUx9O(dedicated, "Adresse locale", "127.0.0.1");
        _serverPort = NumberUx9O(dedicated, "Port", 1024, 65535, 5121);
        _serverCapacity = NumberUx9O(dedicated, "Joueurs maximum", 1, 256, 8);
        _dedicatedRequired = CheckUx9O(dedicated, "Requis au runtime");
        dedicated.AddChild(StudioStyle.Label("Mode", 8, StudioStyle.Muted));
        _serviceMode = ChoiceUx9O("Console", "Service Windows");
        dedicated.AddChild(_serviceMode);
        _healthPath = TextUx9O(dedicated, "Chemin de santé", "/health");
        dedicated.AddChild(StudioStyle.Label(
            "Headless et indépendance de l’éditeur restent imposés.", 9,
            StudioStyle.Success, wrap: true));

        var combat = PolicyCardUx9O(policies, "ORF-MP-008", "PvP et tir ami",
            "Priorité serveur, consentement, duels, crime et zones sûres.");
        _pvpEnabled = CheckUx9O(combat, "Activer le PvP");
        combat.AddChild(StudioStyle.Label("Tir ami", 8, StudioStyle.Muted));
        _friendlyFire = ChoiceUx9O("Désactivé", "Groupe protégé", "Activé");
        combat.AddChild(_friendlyFire);
        _globalCombat = CheckUx9O(combat, "Politique globale");
        _areaOverrides = CheckUx9O(combat, "Surcharges par zone");
        _partyOverrides = CheckUx9O(combat, "Surcharges par groupe");
        _consent = CheckUx9O(combat, "Consentement requis");
        _duels = CheckUx9O(combat, "Duels disponibles");
        _crime = CheckUx9O(combat, "Crime géré");
        _safeZones = CheckUx9O(combat, "Zones sûres");
        _serverOverride = CheckUx9O(combat, "Priorité serveur");

        var logs = PolicyCardUx9O(policies, "ORF-MP-029", "Journaux structurés",
            "Sévérité, catégories, confidentialité, rotation et sink nommé.");
        logs.AddChild(StudioStyle.Label("Sévérité minimum", 8, StudioStyle.Muted));
        _logSeverity = ChoiceUx9O("Trace", "Debug", "Information", "Warning",
            "Error", "Critical");
        logs.AddChild(_logSeverity);
        _logCategories = TextUx9O(logs, "Catégories", "network, security, runtime");
        logs.AddChild(StudioStyle.Label("Confidentialité joueur", 8, StudioStyle.Muted));
        _privacy = ChoiceUx9O("Expurger", "Hacher");
        logs.AddChild(_privacy);
        logs.AddChild(StudioStyle.Label("Format", 8, StudioStyle.Muted));
        _logFormat = ChoiceUx9O("JSON", "Texte", "JSON + texte");
        logs.AddChild(_logFormat);
        var logNumbers = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        logs.AddChild(logNumbers);
        _retention = NumberUx9O(logNumbers, "Rétention (j)", 1, 365, 30);
        _maximumEntries = NumberUx9O(logNumbers, "Entrées", 1, 1000000, 100000);
        _rotationBytes = NumberUx9O(logNumbers, "Rotation (octets)", 256, 1073741824, 4096);
        _rotationFiles = NumberUx9O(logNumbers, "Fichiers", 1, 100, 4);
        _sinkName = TextUx9O(logs, "Sink", "audit");
        logs.AddChild(StudioStyle.Label("Type de sink", 8, StudioStyle.Muted));
        _sinkKind = ChoiceUx9O("Syslog", "OpenTelemetry", "Webhook");
        logs.AddChild(_sinkKind);
        _sinkEndpoint = TextUx9O(logs, "Endpoint", "udp://127.0.0.1:5514");
        var sinkChecks = new HBoxContainer();
        logs.AddChild(sinkChecks);
        _sinkEnabled = CheckUx9O(sinkChecks, "Actif");
        _sinkRequired = CheckUx9O(sinkChecks, "Obligatoire");

        var anti = PolicyCardUx9O(policies, "ORF-MP-031", "Anti-triche",
            "Intentions bornées, contenu signé, RNG serveur et sauvegardes non fiables.");
        _maximumSpeed = NumberUx9O(anti, "Vitesse max (u/s)", 0.1, 1000, 12,
            step: .1);
        _intentRate = NumberUx9O(anti, "Intentions/s", 1, 10000, 8);
        _anomalyThreshold = NumberUx9O(anti, "Seuil anomalie", 1, 10000, 20);
        _signedContent = CheckUx9O(anti, "Exiger contenu signé");
        _trustedSigners = TextUx9O(anti, "Empreintes signataires",
            new string('a', 64), secret: true);
        _authoritativeRandom = CheckUx9O(anti, "RNG autoritaire");
        _trustClientSaves = CheckUx9O(anti, "Accepter les sauvegardes client");
        _randomSeed = NumberUx9O(anti, "Graine déterministe", int.MinValue,
            int.MaxValue, 2026);
        anti.AddChild(StudioStyle.Label(
            "Les intentions et sauvegardes restent validées par le serveur.", 9,
            StudioStyle.Success, wrap: true));

        var decisions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        decisions.AddThemeConstantOverride("separation", 18);
        root.AddChild(decisions);
        _approval = CheckUx9O(decisions,
            "J’approuve ces trois brouillons et leurs révisions après l’aperçu sec");
        _sandboxConfirmation = CheckUx9O(decisions,
            "Je confirme la sandbox locale et sa libération en fin d’exécution");

        var review = StudioStyle.Panel("Ux9OReview", StudioStyle.SurfaceRaised, 6, 1);
        review.CustomMinimumSize = new Vector2(0, 72);
        root.AddChild(review);
        var reviewStack = new VBoxContainer();
        review.AddChild(reviewStack);
        _previewSummary = StudioStyle.Label("Aucun aperçu sec.", 9,
            StudioStyle.Text, wrap: true);
        reviewStack.AddChild(_previewSummary);
        _auditSummary = StudioStyle.Label("Journal local · aucune entrée", 9,
            StudioStyle.Muted, wrap: true);
        reviewStack.AddChild(_auditSummary);
        _status = StudioStyle.Label(
            "Sélectionnez explicitement le module avant toute configuration.", 9,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "Sandbox isolée : aucun port, audio ou service externe n’est ouvert ; aucun serveur persistant n’est lancé.",
            9, StudioStyle.Warning, wrap: true));

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 7);
        root.AddChild(actions);
        _previewButton = ActionButtonUx9O(actions, "Prévisualiser", PreviewCurrentUx9O,
            122, primary: true);
        _applyButton = ActionButtonUx9O(actions, "Appliquer", ApplyCurrentUx9O, 104);
        _undoButton = ActionButtonUx9O(actions, "Annuler", UndoUx9O, 92);
        _redoButton = ActionButtonUx9O(actions, "Rétablir", RedoUx9O, 92);
        _saveButton = ActionButtonUx9O(actions, "Enregistrer", SaveUx9O, 112);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _sandboxButton = ActionButtonUx9O(actions, "Tester localement", SandboxUx9O, 148);
        ConnectInvalidationUx9O();
        UpdateActionsUx9O();
    }

    private void OpenCurrentUx9O()
    {
        Visible = true;
        ResetUx9O();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionner un module");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(
                _session.Project.Module.DisplayName));
        _moduleChoice.Select(0);
        RefreshAuditUx9O();
        UpdateBoundsUx9O();
        UpdateActionsUx9O();
    }

    private void SelectModuleUx9O(int index)
    {
        _explicitSelection = index == 1 && _session.Project is not null;
        _session.CancelNetworkClosureApprovalUx9O();
        _preview = null;
        _approval.ButtonPressed = false;
        if (!_explicitSelection)
        {
            _draft = null;
            SetStatusUx9O("Sélectionnez explicitement le module.", false);
            UpdateActionsUx9O();
            return;
        }
        _draft = _session.CreateNetworkClosureDraftUx9O(true);
        if (_draft is null)
        {
            SetStatusUx9O("Les trois profils réseau sont indisponibles.", true);
            UpdateActionsUx9O();
            return;
        }
        PopulateDraftUx9O(_draft);
        SetStatusUx9O("Profils chargés · prévisualisez avant toute application.", false);
        UpdateActionsUx9O();
    }

    private void PopulateDraftUx9O(StudioUx9ONetworkClosureDraft draft)
    {
        _updating = true;
        _serverName.Text = draft.ServerName;
        _serverHost.Text = draft.ServerHost;
        _serverPort.Value = draft.ServerPort;
        _serverCapacity.Value = draft.ServerCapacity;
        _dedicatedRequired.ButtonPressed = draft.DedicatedRequired;
        _serviceMode.Select(draft.ServiceMode == DedicatedServiceMode.WindowsService ? 1 : 0);
        _healthPath.Text = draft.HealthCheckPath;
        _pvpEnabled.ButtonPressed = draft.PvpEnabled;
        _friendlyFire.Select((int)draft.FriendlyFire);
        _globalCombat.ButtonPressed = draft.GlobalCombatPolicy;
        _areaOverrides.ButtonPressed = draft.AreaOverrides;
        _partyOverrides.ButtonPressed = draft.PartyOverrides;
        _consent.ButtonPressed = draft.ConsentRequired;
        _duels.ButtonPressed = draft.DuelsEnabled;
        _crime.ButtonPressed = draft.CrimeEnabled;
        _safeZones.ButtonPressed = draft.SafeZonesEnabled;
        _serverOverride.ButtonPressed = draft.ServerOverride;
        _logSeverity.Select((int)draft.MinimumSeverity);
        _logCategories.Text = draft.LogCategoriesText;
        _privacy.Select((int)draft.PlayerPrivacy);
        _logFormat.Select((int)draft.LogOutputFormat);
        _retention.Value = draft.LogRetentionDays;
        _maximumEntries.Value = draft.LogMaximumEntries;
        _rotationBytes.Value = draft.LogRotationBytes;
        _rotationFiles.Value = draft.LogRotationFiles;
        _sinkName.Text = draft.RemoteSinkName;
        _sinkKind.Select((int)draft.RemoteSinkKind);
        _sinkEndpoint.Text = draft.RemoteSinkEndpoint;
        _sinkEnabled.ButtonPressed = draft.RemoteSinkEnabled;
        _sinkRequired.ButtonPressed = draft.RemoteSinkRequired;
        _maximumSpeed.Value = draft.MaximumUnitsPerSecond;
        _intentRate.Value = draft.MaximumIntentsPerSecond;
        _anomalyThreshold.Value = draft.AnomalyThreshold;
        _signedContent.ButtonPressed = draft.RequireSignedContent;
        _trustedSigners.Text = draft.TrustedSignerFingerprintsText;
        _authoritativeRandom.ButtonPressed = draft.AuthoritativeRandom;
        _trustClientSaves.ButtonPressed = draft.TrustClientSaves;
        _randomSeed.Value = draft.RandomSeed;
        _updating = false;
    }

    private StudioUx9ONetworkClosureDraft? ReadDraftUx9O()
    {
        var source = _draft;
        if (!_explicitSelection || source is null) return null;
        return source with
        {
            ServerName = _serverName.Text,
            ServerHost = _serverHost.Text,
            ServerPort = (int)_serverPort.Value,
            ServerCapacity = (int)_serverCapacity.Value,
            DedicatedRequired = _dedicatedRequired.ButtonPressed,
            ServiceMode = _serviceMode.Selected == 1
                ? DedicatedServiceMode.WindowsService : DedicatedServiceMode.Console,
            HealthCheckPath = _healthPath.Text,
            PvpEnabled = _pvpEnabled.ButtonPressed,
            FriendlyFire = (FriendlyFirePolicy)_friendlyFire.Selected,
            GlobalCombatPolicy = _globalCombat.ButtonPressed,
            AreaOverrides = _areaOverrides.ButtonPressed,
            PartyOverrides = _partyOverrides.ButtonPressed,
            ConsentRequired = _consent.ButtonPressed,
            DuelsEnabled = _duels.ButtonPressed,
            CrimeEnabled = _crime.ButtonPressed,
            SafeZonesEnabled = _safeZones.ButtonPressed,
            ServerOverride = _serverOverride.ButtonPressed,
            MinimumSeverity = (StructuredLogSeverity)_logSeverity.Selected,
            LogCategoriesText = _logCategories.Text,
            PlayerPrivacy = (PlayerPrivacyMode)_privacy.Selected,
            LogOutputFormat = (StructuredLogOutputFormat)_logFormat.Selected,
            LogRetentionDays = (int)_retention.Value,
            LogMaximumEntries = (int)_maximumEntries.Value,
            LogRotationBytes = (int)_rotationBytes.Value,
            LogRotationFiles = (int)_rotationFiles.Value,
            RemoteSinkName = _sinkName.Text,
            RemoteSinkKind = (RemoteLogSinkKind)_sinkKind.Selected,
            RemoteSinkEndpoint = _sinkEndpoint.Text,
            RemoteSinkEnabled = _sinkEnabled.ButtonPressed,
            RemoteSinkRequired = _sinkRequired.ButtonPressed,
            MaximumUnitsPerSecond = (float)_maximumSpeed.Value,
            MaximumIntentsPerSecond = (int)_intentRate.Value,
            AnomalyThreshold = (int)_anomalyThreshold.Value,
            RequireSignedContent = _signedContent.ButtonPressed,
            TrustedSignerFingerprintsText = _trustedSigners.Text,
            AuthoritativeRandom = _authoritativeRandom.ButtonPressed,
            TrustClientSaves = _trustClientSaves.ButtonPressed,
            RandomSeed = (int)_randomSeed.Value
        };
    }

    private void PreviewCurrentUx9O()
    {
        var draft = ReadDraftUx9O();
        if (draft is null) return;
        _preview = _session.PreviewNetworkClosureUx9O(draft);
        _approval.ButtonPressed = _preview.CanApply && _approval.ButtonPressed;
        PopulatePreviewUx9O();
        SetStatusUx9O(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx9O();
    }

    private void ApplyCurrentUx9O()
    {
        var draft = ReadDraftUx9O();
        if (draft is null || !_approval.ButtonPressed) return;
        var result = _session.ApplyApprovedNetworkClosureUx9O(draft);
        SetStatusUx9O(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true; _canRedo = false; _canSave = true;
            ReloadDraftUx9O();
            WorkspaceChanged?.Invoke();
        }
        _preview = null;
        _approval.ButtonPressed = false;
        PopulatePreviewUx9O();
        RefreshAuditUx9O();
        UpdateActionsUx9O();
    }

    private void UndoUx9O()
    {
        var result = _session.UndoNetworkClosureUx9O();
        SetStatusUx9O(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false; _canRedo = true; _canSave = true;
            ReloadDraftUx9O(); WorkspaceChanged?.Invoke();
        }
        RefreshAuditUx9O(); UpdateActionsUx9O();
    }

    private void RedoUx9O()
    {
        var result = _session.RedoNetworkClosureUx9O();
        SetStatusUx9O(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true; _canRedo = false; _canSave = true;
            ReloadDraftUx9O(); WorkspaceChanged?.Invoke();
        }
        RefreshAuditUx9O(); UpdateActionsUx9O();
    }

    private void SaveUx9O()
    {
        var result = _session.SaveNetworkClosureUx9O();
        SetStatusUx9O(result.Message, !result.Succeeded);
        if (result.Succeeded) _canSave = false;
        RefreshAuditUx9O(); UpdateActionsUx9O();
    }

    private void SandboxUx9O()
    {
        var result = _session.RunLocalNetworkClosureUx9O(
            _sandboxConfirmation.ButtonPressed, NetworkRole.Administrator);
        SetStatusUx9O(result.Message, !result.Succeeded);
        RefreshAuditUx9O(); UpdateActionsUx9O();
    }

    private void ReloadDraftUx9O()
    {
        _draft = _session.CreateNetworkClosureDraftUx9O(true);
        if (_draft is not null) PopulateDraftUx9O(_draft);
    }

    private void PopulatePreviewUx9O()
    {
        if (_previewSummary is null) return;
        if (_preview is null)
        {
            _previewSummary.Text = "Aucun aperçu sec.";
            return;
        }
        var probe = _preview.Probe;
        _previewSummary.Text = _preview.Succeeded
            ? $"{_preview.Message} · Changements : {string.Join(" · ", _preview.SemanticDiff)}\n" +
              $"Dédié : {probe.DedicatedStatus} · Combat : {probe.CombatStatus} · " +
              $"Logs : {probe.LogStatus} · Anti-triche : {probe.AntiCheatStatus}"
            : $"Aperçu refusé · {string.Join(" · ", _preview.Diagnostics)}";
    }

    private void RefreshAuditUx9O()
    {
        if (_auditSummary is null) return;
        var audit = _session.NetworkClosureAuditUx9O;
        _auditSummary.Text = audit.Count == 0 ? "Journal local · aucune entrée" :
            $"Journal local · {audit.Count} entrée(s) · {audit[^1]}";
    }

    private void ConfigureEvidenceUx9O(string mode)
    {
        _serverName.Text = "Crypte des Cendres · dédié local";
        _serverPort.Value = 5122;
        _serverCapacity.Value = 24;
        _dedicatedRequired.ButtonPressed = true;
        _serviceMode.Select(1);
        _healthPath.Text = "/healthz";
        _pvpEnabled.ButtonPressed = true;
        _friendlyFire.Select(0);
        _logSeverity.Select((int)StructuredLogSeverity.Warning);
        _logCategories.Text = "network, security";
        _privacy.Select((int)PlayerPrivacyMode.Hash);
        _logFormat.Select((int)StructuredLogOutputFormat.JsonAndText);
        _retention.Value = 14;
        _maximumEntries.Value = 4096;
        _rotationBytes.Value = 1024;
        _rotationFiles.Value = 3;
        _sinkName.Text = "audit";
        _sinkKind.Select((int)RemoteLogSinkKind.OpenTelemetry);
        _sinkEndpoint.Text = "http://127.0.0.1:4318";
        _sinkEnabled.ButtonPressed = true;
        _sinkRequired.ButtonPressed = true;
        _maximumSpeed.Value = 18;
        _intentRate.Value = 12;
        _anomalyThreshold.Value = 20;
        _trustedSigners.Text = new string('b', 64);
        _randomSeed.Value = 2042;
        _approval.ButtonPressed = true;
        _sandboxConfirmation.ButtonPressed = true;
        InvalidateUx9O();
        _approval.ButtonPressed = true;
        _sandboxConfirmation.ButtonPressed = true;
    }

    private void ConnectInvalidationUx9O()
    {
        foreach (var line in new[] { _serverName, _serverHost, _healthPath, _logCategories,
                     _sinkName, _sinkEndpoint, _trustedSigners })
            line.TextChanged += _ => InvalidateUx9O();
        foreach (var number in new[] { _serverPort, _serverCapacity, _retention,
                     _maximumEntries, _rotationBytes, _rotationFiles, _maximumSpeed,
                     _intentRate, _anomalyThreshold, _randomSeed })
            number.ValueChanged += _ => InvalidateUx9O();
        foreach (var check in new[] { _dedicatedRequired, _pvpEnabled, _globalCombat,
                     _areaOverrides, _partyOverrides, _consent, _duels, _crime,
                     _safeZones, _serverOverride, _sinkEnabled, _sinkRequired,
                     _signedContent, _authoritativeRandom, _trustClientSaves })
            check.Toggled += _ => InvalidateUx9O();
        foreach (var choice in new[] { _serviceMode, _friendlyFire, _logSeverity,
                     _privacy, _logFormat, _sinkKind })
            choice.ItemSelected += _ => InvalidateUx9O();
        _approval.Toggled += _ => UpdateActionsUx9O();
        _sandboxConfirmation.Toggled += _ => UpdateActionsUx9O();
    }

    private void InvalidateUx9O()
    {
        if (_updating) return;
        _session.CancelNetworkClosureApprovalUx9O();
        _preview = null;
        _approval.ButtonPressed = false;
        PopulatePreviewUx9O();
        UpdateActionsUx9O();
    }

    private void UpdateActionsUx9O()
    {
        if (_previewButton is null) return;
        var selected = _explicitSelection && _draft is not null;
        _previewButton.Disabled = !selected;
        _applyButton.Disabled = !selected || _preview is not { CanApply: true } ||
                                !_approval.ButtonPressed;
        _undoButton.Disabled = !_canUndo;
        _redoButton.Disabled = !_canRedo;
        _saveButton.Disabled = !_canSave;
        _sandboxButton.Disabled = !selected || !_sandboxConfirmation.ButtonPressed;
    }

    private void ResetUx9O()
    {
        _draft = null; _preview = null; _explicitSelection = false;
        _canUndo = false; _canRedo = false; _canSave = false;
        if (_previewSummary is not null) PopulatePreviewUx9O();
    }

    private StudioUx9OSourceSnapshot CaptureSourceUx9O(StudioSessionController session) =>
        new(session.Project, session.History, session.History?.State.Revision,
            session.History?.Journal.ToArray() ?? [], session.IsDirty,
            session.History?.CanUndo ?? false, session.History?.CanRedo ?? false,
            session.ActiveAreaId, session.SelectedEntityIds.ToArray(), session.SelectedEntityId,
            session.CaptureNetworkFoundationApprovalUx9A(),
            session.CaptureNetworkExperienceApprovalUx8M(),
            session.CaptureNetworkClosureApprovalUx9O());

    private bool SourceUnchangedUx9O()
    {
        var source = _sourceSession;
        var snapshot = _sourceSnapshot;
        if (source is null || snapshot is null) return _evidenceSession is null;
        return ReferenceEquals(source.Project, snapshot.Project) &&
               ReferenceEquals(source.History, snapshot.History) &&
               source.History?.State.Revision == snapshot.Revision &&
               (source.History?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               source.IsDirty == snapshot.Dirty &&
               (source.History?.CanUndo ?? false) == snapshot.CanUndo &&
               (source.History?.CanRedo ?? false) == snapshot.CanRedo &&
               source.ActiveAreaId == snapshot.Area &&
               source.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               source.SelectedEntityId == snapshot.Selected &&
               source.CaptureNetworkFoundationApprovalUx9A() == snapshot.FoundationApproval &&
               source.CaptureNetworkExperienceApprovalUx8M() == snapshot.PlayerApproval &&
               source.CaptureNetworkClosureApprovalUx9O() == snapshot.ClosureApproval;
    }

    private void RestoreSourceSessionUx9O()
    {
        if (_sourceSession is not null)
        {
            _session = _sourceSession;
            if (_sourceSnapshot is not null)
            {
                _session.RestoreNetworkFoundationApprovalUx9A(
                    _sourceSnapshot.FoundationApproval);
                _session.RestoreNetworkExperienceApprovalUx8M(
                    _sourceSnapshot.PlayerApproval);
                _session.RestoreNetworkClosureApprovalUx9O(
                    _sourceSnapshot.ClosureApproval);
            }
        }
        _sourceSession = null;
        _sourceSnapshot = null;
        _sourceSessionRestored = true;
    }

    private void DeleteEvidenceUx9O()
    {
        var root = _evidenceRoot;
        _evidenceRoot = null;
        if (string.IsNullOrWhiteSpace(root)) { _evidenceResourcesReleased = true; return; }
        try
        {
            var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(full)?.TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), temp,
                    StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-o-ui-",
                    StringComparison.Ordinal))
            { _evidenceResourcesReleased = false; return; }
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            _evidenceResourcesReleased = !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _evidenceResourcesReleased = false;
            SetStatusUx9O($"La copie temporaire n’a pas pu être libérée : {exception.Message}", true);
        }
    }

    private void UpdateBoundsUx9O()
    {
        if (_dialog is null) return;
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(1200, size.X - 48), Math.Max(800, size.Y - 36));
    }

    private void SetStatusUx9O(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private static VBoxContainer PolicyCardUx9O(Control parent, string route,
        string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux9O" + route.Replace("-", string.Empty),
            StudioStyle.SurfaceRaised, 7, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        parent.AddChild(panel);
        var stack = new VBoxContainer
        { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        stack.AddThemeConstantOverride("separation", 3);
        panel.AddChild(stack);
        stack.AddChild(StudioStyle.Label($"{route} · {title}", 10, StudioStyle.Text, wrap: true));
        stack.AddChild(StudioStyle.Label(detail, 8, StudioStyle.Muted, wrap: true));
        return stack;
    }

    private static Control LabeledRowUx9O(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(title); row.AddChild(control); return row;
    }

    private static OptionButton ChoiceUx9O(params string[] choices)
    {
        var value = new OptionButton
        { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(130, 28) };
        foreach (var choice in choices) value.AddItem(choice);
        return value;
    }

    private static CheckBox CheckUx9O(Control parent, string text)
    {
        var value = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(value); return value;
    }

    private static SpinBox NumberUx9O(Control parent, string label, double min,
        double max, double value, double step = 1)
    {
        var group = parent is GridContainer ? new VBoxContainer
        { SizeFlagsHorizontal = SizeFlags.ExpandFill } : parent;
        if (!ReferenceEquals(group, parent)) parent.AddChild(group);
        group.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted));
        var number = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            AllowGreater = false,
            AllowLesser = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(120, 28)
        };
        group.AddChild(number); return number;
    }

    private static LineEdit TextUx9O(Control parent, string label, string value,
        bool secret = false)
    {
        parent.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted));
        var field = new LineEdit
        {
            Text = value,
            Secret = secret,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(130, 28)
        };
        parent.AddChild(field); return field;
    }

    private static Button ActionButtonUx9O(Control parent, string text, Action action,
        float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action; parent.AddChild(button); return button;
    }

    private static IEnumerable<string> DescendantTextUx9O(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is LineEdit line && !line.Secret) yield return line.Text;
        if (node is OptionButton option)
            for (var index = 0; index < option.ItemCount; index++)
                yield return option.GetItemText(index);
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx9O(child)) yield return value;
    }

    private static bool ContainsUuidUx9O(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);
}
