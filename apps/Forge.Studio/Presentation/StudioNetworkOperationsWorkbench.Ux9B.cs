using System.Text.RegularExpressions;
using Forge.ModuleFormat.Network;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioNetworkOperationsWorkbenchUx9B : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx9B? _sourceSnapshot;
    private StudioUx9BOperationsDraft? _draft;
    private StudioUx9BOperationsPreview? _preview;
    private bool _explicitSelection;
    private bool _updating;
    private bool _canUndo;
    private bool _canRedo;
    private bool _evidenceResourcesReleased = true;
    private bool _sourceSessionRestored = true;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private OptionButton _focusChoice = null!;
    private readonly List<Control> _focusPages = [];
    private SpinBox _chunkKilobytes = null!;
    private SpinBox _cacheMegabytes = null!;
    private CheckBox _deltaEnabled = null!;
    private LineEdit _licenses = null!;
    private LineEdit _moduleId = null!;
    private LineEdit _moduleVersion = null!;
    private LineEdit _protocolVersion = null!;
    private LineEdit _contentFingerprint = null!;
    private OptionButton _editedRole = null!;
    private LineEdit _capabilities = null!;
    private LineEdit _customCapabilities = null!;
    private CheckBox _zoneScoped = null!;
    private SpinBox _localRadius = null!;
    private SpinBox _maximumCharacters = null!;
    private LineEdit _languages = null!;
    private LineEdit _filterToken = null!;
    private LineEdit _filterReplacement = null!;
    private CheckBox _allowGlobal = null!;
    private CheckBox _allowSystem = null!;
    private SpinBox _maximumPending = null!;
    private LineEdit _eventKind = null!;
    private LineEdit _subscriberId = null!;
    private SpinBox _eventPriority = null!;
    private CheckBox _approval = null!;
    private CheckBox _probeConfirmation = null!;
    private Label _previewSummary = null!;
    private Label _auditSummary = null!;
    private Label _status = null!;
    private Button _previewButton = null!;
    private Button _applyButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _saveButton = null!;
    private Button _probeButton = null!;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? WorkspaceChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool HasExplicitSelection => _explicitSelection;
    internal bool PreviewReady => _preview is
    {
        Succeeded: true, CanApply: true, CanonicalDryRun: true, ContentRuntime: true,
        VersionRuntime: true, RolesRuntime: true, ChatRuntime: true, EventsRuntime: true
    };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx9B();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx9B(this));
            return Visible && text.Contains("ORF-MP-013", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-015", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-017", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-020", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-027", StringComparison.Ordinal) &&
                   text.Contains("Administrateur", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tester localement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun transport distant", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx9B(text);
        }
    }

    internal StudioNetworkOperationsWorkbenchUx9B(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx9B();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9B();
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
        _session.CancelNetworkOperationsApprovalUx9B();
        ResetUx9B();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx9B(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux9-b-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        var baseline = NetworkOperationsProfile.CreateDefault("evidence:ux9-b-ui");
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !WorkspaceNetworkOperationsStore.Save(_evidenceRoot, baseline).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx9B();
            RestoreSourceSessionUx9B();
            return false;
        }

        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx9B();
        _moduleChoice.Select(1);
        SelectModuleUx9B(1);
        ConfigureEvidenceUx9B(mode);
        PreviewCurrentUx9B();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady && ShowsHumanSurface &&
                    EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_B_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
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
        var draft = ReadDraftUx9B();
        if (draft is null) return false;
        var preview = _session.PreviewNetworkOperationsUx9B(draft);
        var applied = preview.CanApply && _approval.ButtonPressed &&
                      _session.ApplyApprovedNetworkOperationsUx9B(draft).Succeeded;
        var undo = applied && _session.UndoNetworkOperationsUx9B().Succeeded;
        var redo = undo && _session.RedoNetworkOperationsUx9B().Succeeded;
        var save = redo && _session.SaveNetworkOperationsUx9B().Succeeded;
        var probed = save && _probeConfirmation.ButtonPressed &&
                     _session.RunLocalNetworkOperationsUx9B(true, NetworkRole.Administrator).Succeeded;
        var expected = _session.CurrentNetworkOperationsUx9B;
        var reopened = new StudioSessionController();
        var reopenedOk = probed && expected is not null && reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.CurrentNetworkOperationsUx9B is { } actual &&
                         string.Equals(new NetworkOperationsStore().Serialize(expected),
                             new NetworkOperationsStore().Serialize(actual), StringComparison.Ordinal);
        reopened.Close();
        RefreshAuditUx9B();
        return preview.CanonicalDryRun && applied && undo && redo && save && probed && reopenedOk &&
               _session.NetworkOperationsProbeUx9B.ResourcesReleased && EvidenceSourceUntouched;
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is not null)
        {
            _evidenceSession.Close();
            _evidenceSession = null;
        }
        Visible = false;
        ResetUx9B();
        DeleteEvidenceUx9B();
        RestoreSourceSessionUx9B();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9B();
        _moduleChoice.Select(1);
        SelectModuleUx9B(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx9B();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx9B;
        UpdateBoundsUx9B();
    }

    private void BuildUiUx9B()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color("101410f2");
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux9BNetworkOperations", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var root = new VBoxContainer
        {
            Name = "Ux9BRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 6);
        _dialog.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(headingText);
        headingText.AddChild(StudioStyle.Label("Réseau · opérations locales gouvernées", 20, StudioStyle.Text));
        headingText.AddChild(StudioStyle.Label(
            "Cinq politiques persistantes, une commande transactionnelle et des sondes locales sans serveur public.",
            11, StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);

        var routes = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        routes.AddThemeConstantOverride("separation", 7);
        root.AddChild(routes);
        routes.AddChild(RouteCardUx9B("ORF-MP-013", "Contenu", "Reprise, hash, cache et licences."));
        routes.AddChild(RouteCardUx9B("ORF-MP-015", "Version", "Module, protocole et empreinte."));
        routes.AddChild(RouteCardUx9B("ORF-MP-017", "Rôles", "Capacités et portée de zone."));
        routes.AddChild(RouteCardUx9B("ORF-MP-020", "Chat", "Canaux, langues et accessibilité."));
        routes.AddChild(RouteCardUx9B("ORF-MP-027", "Événements", "Priorité, trace et anti-rejeu."));
        root.AddChild(StudioStyle.Label(
            "Limite : sondes en mémoire et cache temporaire uniquement · aucun transport distant, compte ou administration publique.",
            10, StudioStyle.Warning));

        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _moduleChoice.ItemSelected += index => SelectModuleUx9B((int)index);
        root.AddChild(LabeledRowUx9B("Module explicite", _moduleChoice));

        _focusChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var label in new[]
                 {
                     "Synchronisation de contenu", "Verrou de version", "Rôles et permissions",
                     "Chat structuré", "Bus d’événements serveur"
                 })
            _focusChoice.AddItem(label);
        _focusChoice.ItemSelected += index => SelectFocusUx9B((int)index);
        root.AddChild(LabeledRowUx9B("Workflow affiché", _focusChoice));

        var editor = StudioStyle.Panel("Ux9BEditor", StudioStyle.SurfaceRaised, 9, 1);
        editor.CustomMinimumSize = new Vector2(0, 330);
        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(editor);
        var editorStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        editorStack.AddThemeConstantOverride("separation", 7);
        editor.AddChild(editorStack);
        editorStack.AddChild(StudioStyle.Label("Édition directe de la politique sélectionnée", 14, StudioStyle.Text));
        BuildContentPageUx9B(editorStack);
        BuildVersionPageUx9B(editorStack);
        BuildRolesPageUx9B(editorStack);
        BuildChatPageUx9B(editorStack);
        BuildEventsPageUx9B(editorStack);

        var confirmations = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        confirmations.AddThemeConstantOverride("separation", 18);
        root.AddChild(confirmations);
        _approval = CheckUx9B(confirmations, "J’approuve ce seul aperçu sec pour la révision affichée");
        _probeConfirmation = CheckUx9B(confirmations, "Je confirme la sonde locale après sauvegarde");

        var review = StudioStyle.Panel("Ux9BReview", StudioStyle.SurfaceRaised, 7, 1);
        review.CustomMinimumSize = new Vector2(0, 108);
        root.AddChild(review);
        var reviewStack = new VBoxContainer();
        review.AddChild(reviewStack);
        _previewSummary = StudioStyle.Label("Prévisualisez la configuration · aucune sonde exécutée.", 10,
            StudioStyle.Muted);
        _previewSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        reviewStack.AddChild(_previewSummary);
        _auditSummary = StudioStyle.Label("Journal local · aucune entrée", 10, StudioStyle.Muted);
        _auditSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        reviewStack.AddChild(_auditSummary);

        _status = StudioStyle.Label("Sélectionnez explicitement le module.", 10, StudioStyle.Muted);
        root.AddChild(_status);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        _previewButton = ActionButtonUx9B(actions, "Prévisualiser", PreviewCurrentUx9B, 132, true);
        _applyButton = ActionButtonUx9B(actions, "Appliquer", ApplyCurrentUx9B, 104);
        _undoButton = ActionButtonUx9B(actions, "Annuler", UndoUx9B, 92);
        _redoButton = ActionButtonUx9B(actions, "Rétablir", RedoUx9B, 92);
        _saveButton = ActionButtonUx9B(actions, "Enregistrer", SaveUx9B, 112);
        _probeButton = ActionButtonUx9B(actions, "Tester localement", ProbeUx9B, 144);

        ConnectInvalidationUx9B();
        SelectFocusUx9B(0);
        UpdateActionsUx9B();
    }

    private void BuildContentPageUx9B(VBoxContainer parent)
    {
        var page = FocusPageUx9B(parent, "Synchronisation bornée et vérifiée");
        var grid = GridUx9B(page);
        _chunkKilobytes = SpinUx9B(grid, "Bloc (Kio)", 16, 4096, 16);
        _cacheMegabytes = SpinUx9B(grid, "Cache maximal (Mio)", 1, 524288, 1);
        _licenses = LineUx9B(grid, "Licences admises");
        _deltaEnabled = CheckUx9B(page, "Activer les deltas · reprise, déduplication, hash et signataire fiable restent imposés");
    }

    private void BuildVersionPageUx9B(VBoxContainer parent)
    {
        var page = FocusPageUx9B(parent, "Verrou strict avant admission");
        var grid = GridUx9B(page);
        _moduleId = LineUx9B(grid, "Identifiant module");
        _moduleVersion = LineUx9B(grid, "Version module");
        _protocolVersion = LineUx9B(grid, "Version protocole");
        _contentFingerprint = LineUx9B(grid, "Empreinte SHA-256");
        page.AddChild(StudioStyle.Label("Le test local accepte la correspondance exacte et refuse une version divergente.",
            10, StudioStyle.Success));
    }

    private void BuildRolesPageUx9B(VBoxContainer parent)
    {
        var page = FocusPageUx9B(parent, "Droits appliqués par l’autorité runtime");
        var grid = GridUx9B(page);
        _editedRole = ChoiceUx9B(grid, "Rôle édité", Enum.GetNames<NetworkRole>());
        _capabilities = LineUx9B(grid, "Capacités");
        _customCapabilities = LineUx9B(grid, "Capacités personnalisées");
        _zoneScoped = CheckUx9B(page, "Limiter ce rôle aux zones explicitement accordées");
        page.AddChild(StudioStyle.Label("Permission demandée pour modifier et tester · Administrateur.",
            10, StudioStyle.Warning));
    }

    private void BuildChatPageUx9B(VBoxContainer parent)
    {
        var page = FocusPageUx9B(parent, "Canaux structurés, lisibles et audités");
        var grid = GridUx9B(page);
        _localRadius = SpinUx9B(grid, "Rayon local", 1, 10000, 1);
        _maximumCharacters = SpinUx9B(grid, "Caractères max.", 1, 4096, 1);
        _languages = LineUx9B(grid, "Langues admises");
        _filterToken = LineUx9B(grid, "Terme filtré");
        _filterReplacement = LineUx9B(grid, "Remplacement accessible");
        var checks = new HBoxContainer();
        page.AddChild(checks);
        _allowGlobal = CheckUx9B(checks, "Canal global");
        _allowSystem = CheckUx9B(checks, "Canal système");
        page.AddChild(StudioStyle.Label(
            "Local, groupe, message privé, cri et maître de jeu restent requis · journal structuré et texte accessible imposés.",
            10, StudioStyle.Success));
    }

    private void BuildEventsPageUx9B(VBoxContainer parent)
    {
        var page = FocusPageUx9B(parent, "Abonnements ordonnés et protégés");
        var grid = GridUx9B(page);
        _maximumPending = SpinUx9B(grid, "File maximale", 1, 65536, 1);
        _eventKind = LineUx9B(grid, "Type d’événement");
        _subscriberId = LineUx9B(grid, "Abonné nommé");
        _eventPriority = SpinUx9B(grid, "Priorité", -1000, 1000, 1);
        page.AddChild(StudioStyle.Label("Trace de dispatch, séquence et protection anti-rejeu restent imposées.",
            10, StudioStyle.Success));
    }

    private VBoxContainer FocusPageUx9B(VBoxContainer parent, string title)
    {
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 7);
        page.AddChild(StudioStyle.Label(title, 12, StudioStyle.Text));
        parent.AddChild(page);
        _focusPages.Add(page);
        return page;
    }

    private void OpenCurrentUx9B()
    {
        Visible = true;
        ResetUx9B();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionner un module");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(_session.Project.Module.DisplayName));
        _moduleChoice.Select(0);
        RefreshAuditUx9B();
        UpdateBoundsUx9B();
        UpdateActionsUx9B();
    }

    private void SelectModuleUx9B(int index)
    {
        _explicitSelection = index == 1 && _session.Project is not null;
        _session.CancelNetworkOperationsApprovalUx9B();
        _preview = null;
        _approval.ButtonPressed = false;
        if (!_explicitSelection)
        {
            _draft = null;
            SetStatusUx9B("Sélectionnez explicitement le module.", false);
            UpdateActionsUx9B();
            return;
        }
        _draft = _session.CreateNetworkOperationsDraftUx9B(explicitlySelected: true,
            (NetworkRole)_editedRole.Selected);
        if (_draft is null)
        {
            SetStatusUx9B(_session.NetworkOperationsLoadMessageUx9B ??
                          "Les opérations réseau sont indisponibles.", true);
            UpdateActionsUx9B();
            return;
        }
        PopulateDraftUx9B(_draft);
        SetStatusUx9B(_session.NetworkOperationsFileMissingUx9B
            ? "Profil initial chargé en mémoire · aucun fichier créé avant Enregistrer."
            : "Opérations réseau chargées · prévisualisez avant toute application.", false);
        UpdateActionsUx9B();
    }

    private void PopulateDraftUx9B(StudioUx9BOperationsDraft draft)
    {
        _updating = true;
        _chunkKilobytes.Value = draft.ChunkKilobytes;
        _cacheMegabytes.Value = draft.MaximumCacheMegabytes;
        _deltaEnabled.ButtonPressed = draft.DeltaEnabled;
        _licenses.Text = draft.AllowedLicenses;
        _moduleId.Text = draft.ModuleId;
        _moduleVersion.Text = draft.ModuleVersion;
        _protocolVersion.Text = draft.ProtocolVersion;
        _contentFingerprint.Text = draft.ContentFingerprint;
        _editedRole.Select((int)draft.EditedRole);
        _capabilities.Text = draft.Capabilities;
        _customCapabilities.Text = draft.CustomCapabilities;
        _zoneScoped.ButtonPressed = draft.ZoneScoped;
        _localRadius.Value = draft.LocalRadius;
        _maximumCharacters.Value = draft.MaximumCharacters;
        _languages.Text = draft.AllowedLanguages;
        _filterToken.Text = draft.FilterToken;
        _filterReplacement.Text = draft.FilterReplacement;
        _allowGlobal.ButtonPressed = draft.AllowGlobal;
        _allowSystem.ButtonPressed = draft.AllowSystem;
        _maximumPending.Value = draft.MaximumPendingEvents;
        _eventKind.Text = draft.EventKind;
        _subscriberId.Text = draft.SubscriberId;
        _eventPriority.Value = draft.EventPriority;
        _updating = false;
    }

    private StudioUx9BOperationsDraft? ReadDraftUx9B() => _draft is null ? null : _draft with
    {
        ChunkKilobytes = (int)_chunkKilobytes.Value,
        MaximumCacheMegabytes = (int)_cacheMegabytes.Value,
        DeltaEnabled = _deltaEnabled.ButtonPressed,
        AllowedLicenses = _licenses.Text,
        ModuleId = _moduleId.Text,
        ModuleVersion = _moduleVersion.Text,
        ProtocolVersion = _protocolVersion.Text,
        ContentFingerprint = _contentFingerprint.Text,
        EditedRole = (NetworkRole)_editedRole.Selected,
        Capabilities = _capabilities.Text,
        CustomCapabilities = _customCapabilities.Text,
        ZoneScoped = _zoneScoped.ButtonPressed,
        LocalRadius = (int)_localRadius.Value,
        MaximumCharacters = (int)_maximumCharacters.Value,
        AllowedLanguages = _languages.Text,
        FilterToken = _filterToken.Text,
        FilterReplacement = _filterReplacement.Text,
        AllowGlobal = _allowGlobal.ButtonPressed,
        AllowSystem = _allowSystem.ButtonPressed,
        MaximumPendingEvents = (int)_maximumPending.Value,
        EventKind = _eventKind.Text,
        SubscriberId = _subscriberId.Text,
        EventPriority = (int)_eventPriority.Value
    };

    private void PreviewCurrentUx9B()
    {
        var draft = ReadDraftUx9B();
        if (draft is null)
        {
            SetStatusUx9B("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewNetworkOperationsUx9B(draft);
        PopulatePreviewUx9B();
        SetStatusUx9B(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx9B();
    }

    private void ApplyCurrentUx9B()
    {
        var draft = ReadDraftUx9B();
        if (draft is null || !_approval.ButtonPressed) return;
        var focus = _focusChoice.Selected;
        var result = _session.ApplyApprovedNetworkOperationsUx9B(draft);
        SetStatusUx9B(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9B(1);
            _focusChoice.Select(focus);
            SelectFocusUx9B(focus);
        }
        UpdateActionsUx9B();
    }

    private void UndoUx9B()
    {
        var result = _session.UndoNetworkOperationsUx9B();
        SetStatusUx9B(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9B(1);
        }
        UpdateActionsUx9B();
    }

    private void RedoUx9B()
    {
        var result = _session.RedoNetworkOperationsUx9B();
        SetStatusUx9B(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9B(1);
        }
        UpdateActionsUx9B();
    }

    private void SaveUx9B()
    {
        var result = _session.SaveNetworkOperationsUx9B();
        SetStatusUx9B(result.Message, !result.Succeeded);
        UpdateActionsUx9B();
    }

    private void ProbeUx9B()
    {
        var result = _session.RunLocalNetworkOperationsUx9B(_probeConfirmation.ButtonPressed,
            NetworkRole.Administrator);
        SetStatusUx9B(result.Message, !result.Succeeded);
        RefreshAuditUx9B();
        UpdateActionsUx9B();
    }

    private void PopulatePreviewUx9B()
    {
        if (_preview is null)
        {
            _previewSummary.Text = "Prévisualisez la configuration · aucune sonde exécutée.";
            return;
        }
        var changes = _preview.SemanticDiff.Count == 0 ? "Aucun changement" :
            string.Join(" · ", _preview.SemanticDiff);
        var probe = _preview.Probe;
        _previewSummary.Text =
            $"{_preview.Message}\n" +
            $"Changements · {changes}\n" +
            $"Sondes · contenu {YesUx9B(_preview.ContentRuntime)} ({probe.ContentChunks} blocs, cache {YesUx9B(probe.CacheReused)}) · " +
            $"version {YesUx9B(_preview.VersionRuntime)} · droits {YesUx9B(_preview.RolesRuntime)} · " +
            $"chat {YesUx9B(_preview.ChatRuntime)} · événements {probe.EventHandlers} handlers/{probe.EventTraces} traces · " +
            $"ressources {ReleasedUx9B(probe.ResourcesReleased)}.";
    }

    private void RefreshAuditUx9B()
    {
        var audit = _session.NetworkOperationsAuditUx9B;
        var probe = _session.NetworkOperationsProbeUx9B;
        _auditSummary.Text = audit.Count == 0
            ? "Journal local · aucune entrée"
            : $"Journal local · {audit.Count} entrées · dernière opération auditée · sonde : {probe.Status}, ressources {ReleasedUx9B(probe.ResourcesReleased)}.";
    }

    private void ConfigureEvidenceUx9B(string mode)
    {
        if (_draft is null) return;
        _updating = true;
        _chunkKilobytes.Value = 32;
        _cacheMegabytes.Value = 256;
        _deltaEnabled.ButtonPressed = false;
        _licenses.Text = "CC0-1.0, MIT";
        _moduleVersion.Text = "1.1.0";
        _contentFingerprint.Text = new string('c', 64);
        _editedRole.Select((int)NetworkRole.Builder);
        _capabilities.Text = "Play, Chat, DirectMessage, BuildContent, ManageWorld, ManageEvents, ViewOperations, Observe";
        _customCapabilities.Text = "content.publish, world.inspect";
        _zoneScoped.ButtonPressed = true;
        _localRadius.Value = 42;
        _maximumCharacters.Value = 640;
        _languages.Text = "common, elvish, dwarven";
        _filterToken.Text = "spoiler";
        _filterReplacement.Text = "[masqué]";
        _allowGlobal.ButtonPressed = false;
        _allowSystem.ButtonPressed = true;
        _maximumPending.Value = 16;
        _eventKind.Text = "quest.completed";
        _subscriberId.Text = "gameplay.journal";
        _eventPriority.Value = 250;
        _approval.ButtonPressed = true;
        _probeConfirmation.ButtonPressed = true;
        var focus = mode.Contains("version", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("role", StringComparison.OrdinalIgnoreCase) ? 2 :
            mode.Contains("chat", StringComparison.OrdinalIgnoreCase) ? 3 :
            mode.Contains("event", StringComparison.OrdinalIgnoreCase) ? 4 : 0;
        _focusChoice.Select(focus);
        SelectFocusUx9B(focus);
        _updating = false;
        InvalidateUx9B(cancelApproval: false, preserveApproval: true);
    }

    private void SelectFocusUx9B(int index)
    {
        for (var page = 0; page < _focusPages.Count; page++) _focusPages[page].Visible = page == index;
    }

    private void SelectRoleUx9B(int index)
    {
        if (_updating || !_explicitSelection || _draft is null) return;
        var roleDraft = _session.CreateNetworkOperationsDraftUx9B(explicitlySelected: true, (NetworkRole)index);
        if (roleDraft is null) return;
        _updating = true;
        _capabilities.Text = roleDraft.Capabilities;
        _customCapabilities.Text = roleDraft.CustomCapabilities;
        _zoneScoped.ButtonPressed = roleDraft.ZoneScoped;
        _updating = false;
        InvalidateUx9B();
    }

    private void ConnectInvalidationUx9B()
    {
        foreach (var line in new[]
                 {
                     _licenses, _moduleId, _moduleVersion, _protocolVersion, _contentFingerprint,
                     _capabilities, _customCapabilities, _languages, _filterToken, _filterReplacement,
                     _eventKind, _subscriberId
                 })
            line.TextChanged += _ => InvalidateUx9B();
        foreach (var spin in new[]
                 {
                     _chunkKilobytes, _cacheMegabytes, _localRadius, _maximumCharacters,
                     _maximumPending, _eventPriority
                 })
            spin.ValueChanged += _ => InvalidateUx9B();
        foreach (var check in new[] { _deltaEnabled, _zoneScoped, _allowGlobal, _allowSystem })
            check.Toggled += _ => InvalidateUx9B();
        _editedRole.ItemSelected += index => SelectRoleUx9B((int)index);
        _approval.Toggled += _ => UpdateActionsUx9B();
        _probeConfirmation.Toggled += _ => UpdateActionsUx9B();
    }

    private void InvalidateUx9B(bool cancelApproval = true, bool preserveApproval = false)
    {
        if (_updating) return;
        _preview = null;
        if (cancelApproval) _session.CancelNetworkOperationsApprovalUx9B();
        if (!preserveApproval) _approval.ButtonPressed = false;
        PopulatePreviewUx9B();
        UpdateActionsUx9B();
    }

    private void UpdateActionsUx9B()
    {
        var selected = _explicitSelection && _draft is not null;
        _previewButton.Disabled = !selected;
        _applyButton.Disabled = !selected || !PreviewReady || !_approval.ButtonPressed;
        _undoButton.Disabled = !_canUndo;
        _redoButton.Disabled = !_canRedo;
        _saveButton.Disabled = !_session.NetworkOperationsHasUnsavedChangesUx9B;
        _probeButton.Disabled = !selected || PreviewReady || _session.NetworkOperationsHasUnsavedChangesUx9B ||
                                !_probeConfirmation.ButtonPressed;
    }

    private void ResetUx9B()
    {
        _draft = null;
        _preview = null;
        _explicitSelection = false;
        _canUndo = false;
        _canRedo = false;
        _updating = true;
        if (_approval is not null) _approval.ButtonPressed = false;
        if (_probeConfirmation is not null) _probeConfirmation.ButtonPressed = false;
        _updating = false;
        if (_previewSummary is not null) PopulatePreviewUx9B();
    }

    private SourceSnapshotUx9B CaptureSourceUx9B(StudioSessionController session) => new(
        session.Project, session.History, session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [], session.IsDirty, session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(), session.SelectedEntityId,
        session.CaptureNetworkOperationsApprovalUx9B());

    private bool SourceUnchangedUx9B()
    {
        var source = _sourceSession;
        var snapshot = _sourceSnapshot;
        if (source is null || snapshot is null) return true;
        return ReferenceEquals(source.Project, snapshot.Project) &&
               ReferenceEquals(source.History, snapshot.History) &&
               source.History?.State.Revision == snapshot.Revision &&
               (source.History?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               source.IsDirty == snapshot.Dirty && source.ActiveAreaId == snapshot.Area &&
               source.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               source.SelectedEntityId == snapshot.Selected &&
               source.CaptureNetworkOperationsApprovalUx9B() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx9B()
    {
        if (_sourceSession is not null) _session = _sourceSession;
        _sourceSession = null;
        _sourceSnapshot = null;
        _sourceSessionRestored = true;
    }

    private void DeleteEvidenceUx9B()
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
            var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(full)?.TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar), temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-b-ui-", StringComparison.Ordinal))
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
            SetStatusUx9B($"La copie temporaire n’a pas pu être libérée : {exception.Message}", true);
        }
    }

    private void UpdateBoundsUx9B()
    {
        if (_dialog is null) return;
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(960, size.X - 48), Math.Max(700, size.Y - 36));
    }

    private void SetStatusUx9B(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private static Control RouteCardUx9B(string route, string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux9B" + route.Replace("-", string.Empty),
            StudioStyle.SurfaceRaised, 6, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 2);
        panel.AddChild(stack);
        stack.AddChild(StudioStyle.Label($"{route} · {title}", 10, StudioStyle.Text));
        var label = StudioStyle.Label(detail, 9, StudioStyle.Muted);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        stack.AddChild(label);
        return panel;
    }

    private static Control LabeledRowUx9B(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(title);
        row.AddChild(control);
        return row;
    }

    private static GridContainer GridUx9B(Control parent)
    {
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 7);
        parent.AddChild(grid);
        return grid;
    }

    private static LineEdit LineUx9B(GridContainer parent, string label)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(420, 34)
        };
        parent.AddChild(value);
        return value;
    }

    private static SpinBox SpinUx9B(GridContainer parent, string label, double min, double max, double step)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            AllowGreater = false,
            AllowLesser = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(420, 34)
        };
        parent.AddChild(value);
        return value;
    }

    private static OptionButton ChoiceUx9B(GridContainer parent, string label, IReadOnlyList<string> values)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var choice = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(420, 34)
        };
        foreach (var value in values) choice.AddItem(value);
        parent.AddChild(choice);
        return choice;
    }

    private static CheckBox CheckUx9B(Control parent, string text)
    {
        var check = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(check);
        return check;
    }

    private static Button ActionButtonUx9B(HBoxContainer parent, string text, Action action, float width,
        bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static string YesUx9B(bool value) => value ? "validé" : "refusé";
    private static string ReleasedUx9B(bool value) => value ? "libérées" : "encore actives";

    private static IEnumerable<string> DescendantTextUx9B(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is CheckBox check) yield return check.Text;
        if (node is LineEdit line) yield return line.Text;
        if (node is OptionButton option)
            for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx9B(child)) yield return value;
    }

    private static bool ContainsUuidUx9B(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);

    private sealed record SourceSnapshotUx9B(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool Dirty,
        Forge.Domain.Areas.AreaId? Area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? Selected,
        StudioUx9BApprovalSnapshot Approval);
}
