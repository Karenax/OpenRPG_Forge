using System.Text.RegularExpressions;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Operations;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioNetworkProductionWorkbenchUx9N : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioUx9NSourceSnapshot? _sourceSnapshot;
    private StudioUx9NNetworkProductionDraft? _draft;
    private StudioUx9NNetworkProductionPreview? _preview;
    private Guid _selectedAreaId;
    private bool _explicitSelection;
    private bool _updating;
    private bool _canUndo;
    private bool _canRedo;
    private bool _canSave;
    private bool _evidenceResourcesReleased = true;
    private bool _sourceSessionRestored = true;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private LineEdit _curatedChannel = null!;
    private LineEdit _curatedCompatibility = null!;
    private LineEdit _licenses = null!;
    private CheckBox _trustedSignature = null!;
    private CheckBox _offlineFallback = null!;
    private CheckBox _voiceEnabled = null!;
    private OptionButton _voiceCodec = null!;
    private SpinBox _voiceBitrate = null!;
    private CheckBox _voicePositional = null!;
    private CheckBox _voicePushToTalk = null!;
    private CheckBox _voiceModeration = null!;
    private CheckBox _shardEnabled = null!;
    private LineEdit _shardKey = null!;
    private OptionButton _areaChoice = null!;
    private SpinBox _shardPlayers = null!;
    private LineEdit _shardRegion = null!;
    private CheckBox _hotReloadEnabled = null!;
    private SpinBox _packageMegabytes = null!;
    private SpinBox _drainSeconds = null!;
    private CheckBox _extensionsEnabled = null!;
    private LineEdit _capabilities = null!;
    private SpinBox _cpuBudget = null!;
    private LineEdit _protocolFamily = null!;
    private SpinBox _protocolMinimum = null!;
    private SpinBox _protocolMaximum = null!;
    private LineEdit _protocolPlatforms = null!;
    private CheckBox _rejectUnknownFields = null!;
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
    {
        Succeeded: true, CanApply: true, CanonicalDryRun: true, RuntimeReady: true
    };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx9N();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx9N(this));
            return Visible && new[]
                   {
                       "ORF-MP-014", "ORF-MP-021", "ORF-MP-024", "ORF-MP-025",
                       "ORF-MP-026", "ORF-MP-035"
                   }.All(route => text.Contains(route, StringComparison.Ordinal)) &&
                   text.Contains("licences", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("push-to-talk", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("bascule atomique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("signature et isolation", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("plateformes", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tester localement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun contenu, appel vocal ou serveur réel",
                       StringComparison.OrdinalIgnoreCase) && !ContainsUuidUx9N(text);
        }
    }

    internal StudioNetworkProductionWorkbenchUx9N(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx9N();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9N();
    }

    internal void Close()
    {
        if (_evidenceSession is not null)
        {
            CloseEvidence();
            return;
        }
        if (!Visible) return;
        Visible = false;
        _session.CancelNetworkProductionApprovalUx9N();
        ResetUx9N();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx9N(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-n-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx9N();
            RestoreSourceSessionUx9N();
            return false;
        }
        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx9N();
        _moduleChoice.Select(1);
        SelectModuleUx9N(1);
        ConfigureEvidenceUx9N(mode);
        PreviewCurrentUx9N();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady &&
                    ShowsHumanSurface && EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_N_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
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
        var draft = ReadDraftUx9N();
        if (draft is null) return false;
        var preview = _session.PreviewNetworkProductionUx9N(draft);
        var applied = preview.CanApply && _approval.ButtonPressed &&
                      _session.ApplyApprovedNetworkProductionUx9N(draft).Succeeded;
        var undo = applied && _session.UndoNetworkProductionUx9N().Succeeded;
        var redo = undo && _session.RedoNetworkProductionUx9N().Succeeded;
        var save = redo && _session.SaveNetworkProductionUx9N().Succeeded;
        var probed = save && _sandboxConfirmation.ButtonPressed &&
                     _session.RunLocalNetworkProductionUx9N(true,
                         NetworkRole.Administrator).Succeeded;
        var expected = _session.Project;
        var reopened = new StudioSessionController();
        var reopenedOk = probed && expected is not null &&
                         reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.Project is { } actual &&
                         string.Equals(ProjectJsonSerializer.Serialize(expected),
                             ProjectJsonSerializer.Serialize(actual), StringComparison.Ordinal);
        reopened.Close();
        RefreshAuditUx9N();
        return preview.CanonicalDryRun && applied && undo && redo && save && probed &&
               reopenedOk && _session.NetworkProductionProbeUx9N.ResourcesReleased &&
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
        ResetUx9N();
        DeleteEvidenceUx9N();
        RestoreSourceSessionUx9N();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9N();
        _moduleChoice.Select(1);
        SelectModuleUx9N(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx9N();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx9N;
        UpdateBoundsUx9N();
    }

    public override void _ExitTree() => CloseEvidence();

    private void BuildUiUx9N()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color("10121af2");
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _dialog = StudioStyle.Panel("Ux9NNetworkProduction", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var root = new VBoxContainer
        {
            Name = "Ux9NRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 6);
        _dialog.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(headingText);
        headingText.AddChild(StudioStyle.Label("Production réseau avancée", 20,
            StudioStyle.Text));
        headingText.AddChild(StudioStyle.Label(
            "Six politiques gouvernées, sans démarrer de service externe.", 11,
            StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);

        _moduleChoice = ChoiceUx9N("Sélectionner un module");
        _moduleChoice.ItemSelected += index => SelectModuleUx9N((int)index);
        root.AddChild(LabeledRowUx9N("Module explicitement ciblé", _moduleChoice));

        var policies = new GridContainer
        {
            Columns = 6,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(0, 455)
        };
        policies.AddThemeConstantOverride("h_separation", 6);
        root.AddChild(policies);

        var content = PolicyCardUx9N(policies, "ORF-MP-014", "Contenu curaté",
            "Canal, compatibilité, licences et source de confiance.");
        _curatedChannel = TextUx9N(content, "Canal", "stable");
        _curatedCompatibility = TextUx9N(content, "Compatibilité", "1.x");
        _licenses = TextUx9N(content, "Licences autorisées", "CC0-1.0");
        _trustedSignature = CheckUx9N(content, "Exiger une signature fiable");
        _offlineFallback = CheckUx9N(content, "Autoriser le repli hors ligne");

        var voice = PolicyCardUx9N(policies, "ORF-MP-021", "Voix",
            "Codec, débit, position, push-to-talk et modération.");
        _voiceEnabled = CheckUx9N(voice, "Activer la voix");
        voice.AddChild(StudioStyle.Label("Codec", 9, StudioStyle.Muted));
        _voiceCodec = ChoiceUx9N("Opus", "PCM");
        voice.AddChild(_voiceCodec);
        _voiceBitrate = NumberUx9N(voice, "Débit maximum (kb/s)", 8, 512, 64);
        _voicePositional = CheckUx9N(voice, "Voix positionnelle");
        _voicePushToTalk = CheckUx9N(voice, "Push-to-talk");
        _voiceModeration = CheckUx9N(voice, "Modération active");

        var shard = PolicyCardUx9N(policies, "ORF-MP-024", "Shard serveur",
            "Zone, capacité et région nommée.");
        _shardEnabled = CheckUx9N(shard, "Conserver ce shard");
        _shardKey = TextUx9N(shard, "Nom", "serveur-principal");
        shard.AddChild(StudioStyle.Label("Zone", 9, StudioStyle.Muted));
        _areaChoice = ChoiceUx9N("Zone du module");
        _areaChoice.ItemSelected += SelectAreaUx9N;
        shard.AddChild(_areaChoice);
        _shardPlayers = NumberUx9N(shard, "Joueurs maximum", 1, 100000, 64);
        _shardRegion = TextUx9N(shard, "Région", "local");

        var reload = PolicyCardUx9N(policies, "ORF-MP-025", "Hot reload",
            "Bascule atomique, drain et rollback obligatoires.");
        _hotReloadEnabled = CheckUx9N(reload, "Activer le hot reload");
        _packageMegabytes = NumberUx9N(reload, "Paquet maximum (Mo)", 1, 65536, 512);
        _drainSeconds = NumberUx9N(reload, "Drain maximum (s)", 1, 3600, 60);
        reload.AddChild(StudioStyle.Label(
            "La bascule atomique et le rollback restent toujours imposés.", 9,
            StudioStyle.Success, wrap: true));

        var extensions = PolicyCardUx9N(policies, "ORF-MP-026",
            "Extensions serveur", "Signature et isolation avec capacités allowlistées.");
        _extensionsEnabled = CheckUx9N(extensions, "Activer les extensions");
        _capabilities = TextUx9N(extensions, "Capacités autorisées", "metrics");
        _cpuBudget = NumberUx9N(extensions, "Budget CPU (ms)", 1, 10000, 50);
        extensions.AddChild(StudioStyle.Label(
            "Signature et isolation restent obligatoires.", 9, StudioStyle.Success,
            wrap: true));

        var protocol = PolicyCardUx9N(policies, "ORF-MP-035", "Protocole",
            "Famille, versions, plateformes et champs inconnus.");
        _protocolFamily = TextUx9N(protocol, "Famille", "openrpg-net");
        _protocolMinimum = NumberUx9N(protocol, "Version minimum", 1, 1000, 1);
        _protocolMaximum = NumberUx9N(protocol, "Version maximum", 1, 1000, 1);
        _protocolPlatforms = TextUx9N(protocol, "Plateformes", "windows, linux");
        _rejectUnknownFields = CheckUx9N(protocol, "Refuser les champs inconnus");

        var decisions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        decisions.AddThemeConstantOverride("separation", 18);
        root.AddChild(decisions);
        _approval = CheckUx9N(decisions,
            "J’approuve ce brouillon et cette révision après l’aperçu sec");
        _sandboxConfirmation = CheckUx9N(decisions,
            "Je confirme la sandbox locale restaurée en fin d’exécution");

        var review = StudioStyle.Panel("Ux9NReview", StudioStyle.SurfaceRaised, 7, 1);
        review.CustomMinimumSize = new Vector2(0, 88);
        root.AddChild(review);
        var reviewStack = new VBoxContainer();
        review.AddChild(reviewStack);
        _previewSummary = StudioStyle.Label("Aucun aperçu sec.", 10,
            StudioStyle.Text, wrap: true);
        reviewStack.AddChild(_previewSummary);
        _auditSummary = StudioStyle.Label("Journal local · aucune entrée", 10,
            StudioStyle.Muted, wrap: true);
        reviewStack.AddChild(_auditSummary);
        _status = StudioStyle.Label(
            "Sélectionnez explicitement le module avant de configurer le réseau.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "Sandbox isolée : aucun contenu, appel vocal ou serveur réel n’est lancé ; aucun port n’est ouvert.",
            10, StudioStyle.Warning, wrap: true));

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 7);
        root.AddChild(actions);
        _previewButton = ActionButtonUx9N(actions, "Prévisualiser", PreviewCurrentUx9N,
            122, primary: true);
        _applyButton = ActionButtonUx9N(actions, "Appliquer", ApplyCurrentUx9N, 104);
        _undoButton = ActionButtonUx9N(actions, "Annuler", UndoUx9N, 92);
        _redoButton = ActionButtonUx9N(actions, "Rétablir", RedoUx9N, 92);
        _saveButton = ActionButtonUx9N(actions, "Enregistrer", SaveUx9N, 112);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _sandboxButton = ActionButtonUx9N(actions, "Tester localement", SandboxUx9N, 148);

        ConnectInvalidationUx9N();
        UpdateActionsUx9N();
    }

    private void OpenCurrentUx9N()
    {
        Visible = true;
        ResetUx9N();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionner un module");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(
                _session.Project.Module.DisplayName));
        _moduleChoice.Select(0);
        RefreshAuditUx9N();
        UpdateBoundsUx9N();
        UpdateActionsUx9N();
    }

    private void SelectModuleUx9N(int index)
    {
        _explicitSelection = index == 1 && _session.Project is not null;
        _session.CancelNetworkProductionApprovalUx9N();
        _preview = null;
        _approval.ButtonPressed = false;
        if (!_explicitSelection)
        {
            _draft = null;
            SetStatusUx9N("Sélectionnez explicitement le module.", false);
            UpdateActionsUx9N();
            return;
        }
        _draft = _session.CreateNetworkProductionDraftUx9N(explicitlySelected: true);
        if (_draft is null)
        {
            SetStatusUx9N("Les politiques réseau sont indisponibles.", true);
            UpdateActionsUx9N();
            return;
        }
        PopulateDraftUx9N(_draft);
        SetStatusUx9N("Politiques chargées · prévisualisez avant toute application.", false);
        UpdateActionsUx9N();
    }

    private void PopulateDraftUx9N(StudioUx9NNetworkProductionDraft draft)
    {
        _updating = true;
        _curatedChannel.Text = draft.CuratedChannel;
        _curatedCompatibility.Text = draft.CuratedCompatibility;
        _licenses.Text = draft.AllowedLicensesText;
        _trustedSignature.ButtonPressed = draft.RequireTrustedSignature;
        _offlineFallback.ButtonPressed = draft.AllowOfflineFallback;
        _voiceEnabled.ButtonPressed = draft.VoiceEnabled;
        _voiceCodec.Select(draft.VoiceCodec == "pcm" ? 1 : 0);
        _voiceBitrate.Value = draft.VoiceBitrateKbps;
        _voicePositional.ButtonPressed = draft.VoicePositional;
        _voicePushToTalk.ButtonPressed = draft.VoicePushToTalk;
        _voiceModeration.ButtonPressed = draft.VoiceModeration;
        _shardEnabled.ButtonPressed = draft.HasShard;
        _shardKey.Text = draft.ShardKey;
        PopulateAreasUx9N(draft.ShardAreaId);
        _shardPlayers.Value = draft.ShardMaximumPlayers;
        _shardRegion.Text = draft.ShardRegion;
        _hotReloadEnabled.ButtonPressed = draft.HotReloadEnabled;
        _packageMegabytes.Value = draft.MaximumPackageMegabytes;
        _drainSeconds.Value = draft.DrainTimeoutSeconds;
        _extensionsEnabled.ButtonPressed = draft.ExtensionsEnabled;
        _capabilities.Text = draft.AllowedCapabilitiesText;
        _cpuBudget.Value = draft.ExtensionCpuBudgetMilliseconds;
        _protocolFamily.Text = draft.ProtocolFamily;
        _protocolMinimum.Value = draft.ProtocolMinimumVersion;
        _protocolMaximum.Value = draft.ProtocolMaximumVersion;
        _protocolPlatforms.Text = draft.ProtocolPlatformsText;
        _rejectUnknownFields.ButtonPressed = draft.RejectUnknownFields;
        _updating = false;
    }

    private void PopulateAreasUx9N(Guid selected)
    {
        _areaChoice.Clear();
        var choices = _session.NetworkProductionAreaChoicesUx9N;
        if (choices.Count == 0)
        {
            _areaChoice.AddItem("Zone d’entrée du module");
            _selectedAreaId = selected;
            return;
        }
        var selectedIndex = 0;
        for (var index = 0; index < choices.Count; index++)
        {
            _areaChoice.AddItem(choices[index].Label);
            if (choices[index].Id == selected) selectedIndex = index;
        }
        _areaChoice.Select(selectedIndex);
        _selectedAreaId = choices[selectedIndex].Id;
    }

    private void SelectAreaUx9N(long index)
    {
        var choices = _session.NetworkProductionAreaChoicesUx9N;
        if (index >= 0 && index < choices.Count) _selectedAreaId = choices[(int)index].Id;
        InvalidateUx9N();
    }

    private StudioUx9NNetworkProductionDraft? ReadDraftUx9N() => _draft is null ? null :
        _draft with
        {
            CuratedChannel = _curatedChannel.Text.Trim(),
            CuratedCompatibility = _curatedCompatibility.Text.Trim(),
            AllowedLicensesText = _licenses.Text.Trim(),
            RequireTrustedSignature = _trustedSignature.ButtonPressed,
            AllowOfflineFallback = _offlineFallback.ButtonPressed,
            VoiceEnabled = _voiceEnabled.ButtonPressed,
            VoiceCodec = _voiceCodec.Selected == 1 ? "pcm" : "opus",
            VoiceBitrateKbps = (int)_voiceBitrate.Value,
            VoicePositional = _voicePositional.ButtonPressed,
            VoicePushToTalk = _voicePushToTalk.ButtonPressed,
            VoiceModeration = _voiceModeration.ButtonPressed,
            HasShard = _shardEnabled.ButtonPressed,
            ShardKey = _shardKey.Text.Trim(),
            ShardAreaId = _selectedAreaId,
            ShardMaximumPlayers = (int)_shardPlayers.Value,
            ShardRegion = _shardRegion.Text.Trim(),
            HotReloadEnabled = _hotReloadEnabled.ButtonPressed,
            MaximumPackageMegabytes = (int)_packageMegabytes.Value,
            DrainTimeoutSeconds = (int)_drainSeconds.Value,
            ExtensionsEnabled = _extensionsEnabled.ButtonPressed,
            AllowedCapabilitiesText = _capabilities.Text.Trim(),
            ExtensionCpuBudgetMilliseconds = (int)_cpuBudget.Value,
            ProtocolFamily = _protocolFamily.Text.Trim(),
            ProtocolMinimumVersion = (int)_protocolMinimum.Value,
            ProtocolMaximumVersion = (int)_protocolMaximum.Value,
            ProtocolPlatformsText = _protocolPlatforms.Text.Trim(),
            RejectUnknownFields = _rejectUnknownFields.ButtonPressed
        };

    private void PreviewCurrentUx9N()
    {
        var draft = ReadDraftUx9N();
        if (draft is null)
        {
            SetStatusUx9N("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewNetworkProductionUx9N(draft);
        PopulatePreviewUx9N();
        SetStatusUx9N(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx9N();
    }

    private void ApplyCurrentUx9N()
    {
        var draft = ReadDraftUx9N();
        if (draft is null) return;
        var result = _session.ApplyApprovedNetworkProductionUx9N(draft);
        SetStatusUx9N(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            _canSave = true;
            SelectModuleUx9N(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9N();
    }

    private void UndoUx9N()
    {
        var result = _session.UndoNetworkProductionUx9N();
        SetStatusUx9N(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            _canSave = true;
            SelectModuleUx9N(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9N();
    }

    private void RedoUx9N()
    {
        var result = _session.RedoNetworkProductionUx9N();
        SetStatusUx9N(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            _canSave = true;
            SelectModuleUx9N(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9N();
    }

    private void SaveUx9N()
    {
        var result = _session.SaveNetworkProductionUx9N();
        SetStatusUx9N(result.Message, !result.Succeeded);
        if (result.Succeeded) _canSave = false;
        UpdateActionsUx9N();
    }

    private void SandboxUx9N()
    {
        var result = _session.RunLocalNetworkProductionUx9N(
            _sandboxConfirmation.ButtonPressed, NetworkRole.Administrator);
        SetStatusUx9N(result.Message, !result.Succeeded);
        RefreshAuditUx9N();
        PopulatePreviewUx9N();
        UpdateActionsUx9N();
    }

    private void PopulatePreviewUx9N()
    {
        var probe = _session.NetworkProductionProbeUx9N;
        _previewSummary.Text = _preview is null
            ? $"Sandbox · {probe.Status}."
            : $"{_preview.Message} · {string.Join(" · ", _preview.SemanticDiff)}\n" +
              $"Contenu {(probe.CuratedContent ? "validé" : "refusé")} · " +
              $"voix {(probe.Voice ? $"validée à {probe.VoiceBitrateKbps} kb/s" : "refusée")} · " +
              $"shard {(probe.Shard ? "validé" : "refusé")} · reload " +
              $"{(probe.HotReload ? "réversible" : "refusé")} · extensions " +
              $"{(probe.Extensions ? "isolées" : "refusées")} · protocole " +
              $"{(probe.Protocol ? "compatible" : "incompatible")}.";
    }

    private void RefreshAuditUx9N()
    {
        var audit = _session.NetworkProductionAuditUx9N;
        _auditSummary.Text = audit.Count == 0
            ? "Journal local · aucune entrée"
            : $"Journal local · {audit.Count} entrée(s) · ressources " +
              $"{(_session.NetworkProductionProbeUx9N.ResourcesReleased ? "libérées" : "à vérifier")}.";
    }

    private void ConfigureEvidenceUx9N(string mode)
    {
        if (_draft is null) return;
        _updating = true;
        _curatedChannel.Text = _draft.CuratedChannel == "stable" ? "candidate" : "stable";
        _curatedCompatibility.Text = _draft.CuratedCompatibility == "2.x" ? "1.x" : "2.x";
        _licenses.Text = "CC0-1.0, MIT";
        _trustedSignature.ButtonPressed = true;
        _offlineFallback.ButtonPressed = true;
        _voiceEnabled.ButtonPressed = true;
        _voiceCodec.Select(0);
        _voiceBitrate.Value = _draft.VoiceBitrateKbps == 96 ? 112 : 96;
        _voicePositional.ButtonPressed = true;
        _voicePushToTalk.ButtonPressed = true;
        _voiceModeration.ButtonPressed = true;
        _shardEnabled.ButtonPressed = true;
        _shardKey.Text = _draft.HasShard ? _draft.ShardKey : "shard-preuve";
        _shardPlayers.Value = _draft.ShardMaximumPlayers == 48 ? 64 : 48;
        _shardRegion.Text = _draft.HasShard ? _draft.ShardRegion : "local";
        _hotReloadEnabled.ButtonPressed = true;
        _packageMegabytes.Value = _draft.MaximumPackageMegabytes == 256 ? 512 : 256;
        _drainSeconds.Value = _draft.DrainTimeoutSeconds == 45 ? 60 : 45;
        _extensionsEnabled.ButtonPressed = true;
        _capabilities.Text = "metrics, world-read";
        _cpuBudget.Value = _draft.ExtensionCpuBudgetMilliseconds == 75 ? 50 : 75;
        _protocolFamily.Text = _draft.ProtocolFamily;
        _protocolMinimum.Value = 1;
        _protocolMaximum.Value = _draft.ProtocolMaximumVersion == 2 ? 3 : 2;
        _protocolPlatforms.Text = "windows, linux, macos";
        _rejectUnknownFields.ButtonPressed = true;
        _approval.ButtonPressed = true;
        _sandboxConfirmation.ButtonPressed = true;
        _updating = false;
    }

    private void ConnectInvalidationUx9N()
    {
        foreach (var check in new[]
                 {
                     _trustedSignature, _offlineFallback, _voiceEnabled,
                     _voicePositional, _voicePushToTalk, _voiceModeration, _shardEnabled,
                     _hotReloadEnabled, _extensionsEnabled, _rejectUnknownFields
                 })
            check.Toggled += _ => InvalidateUx9N();
        foreach (var number in new[]
                 {
                     _voiceBitrate, _shardPlayers, _packageMegabytes, _drainSeconds,
                     _cpuBudget, _protocolMinimum, _protocolMaximum
                 })
            number.ValueChanged += _ => InvalidateUx9N();
        foreach (var field in new[]
                 {
                     _curatedChannel, _curatedCompatibility, _licenses, _shardKey,
                     _shardRegion, _capabilities, _protocolFamily, _protocolPlatforms
                 })
            field.TextChanged += _ => InvalidateUx9N();
        _voiceCodec.ItemSelected += _ => InvalidateUx9N();
        _approval.Toggled += _ => UpdateActionsUx9N();
        _sandboxConfirmation.Toggled += _ => UpdateActionsUx9N();
    }

    private void InvalidateUx9N()
    {
        if (_updating) return;
        _preview = null;
        _session.CancelNetworkProductionApprovalUx9N();
        _approval.ButtonPressed = false;
        PopulatePreviewUx9N();
        UpdateActionsUx9N();
    }

    private void UpdateActionsUx9N()
    {
        var selected = _explicitSelection && _draft is not null;
        _previewButton.Disabled = !selected;
        _applyButton.Disabled = !selected || _preview is not { CanApply: true } ||
                                !_approval.ButtonPressed;
        _undoButton.Disabled = !_canUndo;
        _redoButton.Disabled = !_canRedo;
        _saveButton.Disabled = !_canSave;
        _sandboxButton.Disabled = !selected || !_sandboxConfirmation.ButtonPressed ||
                                  _canSave;
    }

    private void ResetUx9N()
    {
        _draft = null;
        _preview = null;
        _selectedAreaId = Guid.Empty;
        _explicitSelection = false;
        _canUndo = false;
        _canRedo = false;
        _canSave = false;
        if (_previewSummary is not null) PopulatePreviewUx9N();
    }

    private StudioUx9NSourceSnapshot CaptureSourceUx9N(
        StudioSessionController session) => new(session.Project, session.History,
        session.History?.State.Revision, session.History?.Journal.ToArray() ?? [],
        session.IsDirty, session.History?.CanUndo ?? false,
        session.History?.CanRedo ?? false, session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(), session.SelectedEntityId,
        session.CaptureNetworkProductionApprovalUx9N());

    private bool SourceUnchangedUx9N()
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
               source.CaptureNetworkProductionApprovalUx9N() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx9N()
    {
        if (_sourceSession is not null)
        {
            _session = _sourceSession;
            if (_sourceSnapshot is not null)
                _session.RestoreNetworkProductionApprovalUx9N(_sourceSnapshot.Approval);
        }
        _sourceSession = null;
        _sourceSnapshot = null;
        _sourceSessionRestored = true;
    }

    private void DeleteEvidenceUx9N()
    {
        var root = _evidenceRoot;
        _evidenceRoot = null;
        if (string.IsNullOrWhiteSpace(root))
        {
            _evidenceResourcesReleased = true;
            return;
        }
        try
        {
            var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(full)?.TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), temp,
                    StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-n-ui-",
                    StringComparison.Ordinal))
            {
                _evidenceResourcesReleased = false;
                return;
            }
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            _evidenceResourcesReleased = !Directory.Exists(full);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _evidenceResourcesReleased = false;
            SetStatusUx9N(
                $"La copie temporaire n’a pas pu être libérée : {exception.Message}", true);
        }
    }

    private void UpdateBoundsUx9N()
    {
        if (_dialog is null) return;
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(1200, size.X - 48),
            Math.Max(800, size.Y - 36));
    }

    private void SetStatusUx9N(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger :
            StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private static VBoxContainer PolicyCardUx9N(Control parent, string route,
        string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux9N" + route.Replace("-", string.Empty),
            StudioStyle.SurfaceRaised, 7, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        parent.AddChild(panel);
        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        stack.AddThemeConstantOverride("separation", 5);
        panel.AddChild(stack);
        stack.AddChild(StudioStyle.Label($"{route} · {title}", 10, StudioStyle.Text,
            wrap: true));
        stack.AddChild(StudioStyle.Label(detail, 8, StudioStyle.Muted, wrap: true));
        return stack;
    }

    private static Control LabeledRowUx9N(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(title);
        row.AddChild(control);
        return row;
    }

    private static OptionButton ChoiceUx9N(params string[] choices)
    {
        var value = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(145, 30)
        };
        foreach (var choice in choices) value.AddItem(choice);
        return value;
    }

    private static CheckBox CheckUx9N(Control parent, string text)
    {
        var value = new CheckBox
        {
            Text = text,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        parent.AddChild(value);
        return value;
    }

    private static SpinBox NumberUx9N(Control parent, string label, double min,
        double max, double value)
    {
        parent.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted));
        var number = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = 1,
            Value = value,
            AllowGreater = false,
            AllowLesser = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(145, 30)
        };
        parent.AddChild(number);
        return number;
    }

    private static LineEdit TextUx9N(Control parent, string label, string value)
    {
        parent.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted));
        var field = new LineEdit
        {
            Text = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(145, 30)
        };
        parent.AddChild(field);
        return field;
    }

    private static Button ActionButtonUx9N(Control parent, string text, Action action,
        float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static IEnumerable<string> DescendantTextUx9N(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is LineEdit line) yield return line.Text;
        if (node is OptionButton option)
            for (var index = 0; index < option.ItemCount; index++)
                yield return option.GetItemText(index);
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx9N(child)) yield return value;
    }

    private static bool ContainsUuidUx9N(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);
}
