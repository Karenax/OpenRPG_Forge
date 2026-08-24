using System.Text.RegularExpressions;
using Forge.ModuleFormat.Network;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioNetworkWorldAdministrationWorkbenchUx9C : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx9C? _sourceSnapshot;
    private StudioUx9CWorldAdministrationDraft? _draft;
    private StudioUx9CWorldAdministrationPreview? _preview;
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
    private LineEdit _templateIds = null!;
    private SpinBox _maximumInstances = null!;
    private SpinBox _maximumPlayers = null!;
    private SpinBox _garbageCollection = null!;
    private LineEdit _overrideKeys = null!;
    private SpinBox _maximumEntities = null!;
    private SpinBox _maximumResources = null!;
    private SpinBox _sessionLifetime = null!;
    private CheckBox _oauth = null!;
    private CheckBox _anonymous = null!;
    private SpinBox _requestsPerMinute = null!;
    private LineEdit _safeCommands = null!;
    private SpinBox _moderationRetention = null!;
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
        Succeeded: true, CanApply: true, CanonicalDryRun: true, InstancesRuntime: true,
        DynamicAreasRuntime: true, AuthenticationRuntime: true, AdministrationRuntime: true,
        ModerationRuntime: true
    };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx9C();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx9C(this));
            return Visible && text.Contains("ORF-MP-009", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-010", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-016", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-018", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-019", StringComparison.Ordinal) &&
                   text.Contains("Administrateur", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tester localement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucune API distante", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx9C(text);
        }
    }

    internal StudioNetworkWorldAdministrationWorkbenchUx9C(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx9C();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9C();
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
        _session.CancelWorldAdministrationApprovalUx9C();
        ResetUx9C();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx9C(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux9-c-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        var baseline = NetworkWorldAdministrationProfile.CreateDefault("evidence:ux9-c-ui");
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !WorkspaceNetworkWorldAdministrationStore.Save(_evidenceRoot, baseline).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx9C();
            RestoreSourceSessionUx9C();
            return false;
        }
        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx9C();
        _moduleChoice.Select(1);
        SelectModuleUx9C(1);
        ConfigureEvidenceUx9C(mode);
        PreviewCurrentUx9C();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady && ShowsHumanSurface &&
                    EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_C_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
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
        var draft = ReadDraftUx9C();
        if (draft is null) return false;
        var preview = _session.PreviewWorldAdministrationUx9C(draft);
        var applied = preview.CanApply && _approval.ButtonPressed &&
                      _session.ApplyApprovedWorldAdministrationUx9C(draft).Succeeded;
        var undo = applied && _session.UndoWorldAdministrationUx9C().Succeeded;
        var redo = undo && _session.RedoWorldAdministrationUx9C().Succeeded;
        var save = redo && _session.SaveWorldAdministrationUx9C().Succeeded;
        var probed = save && _probeConfirmation.ButtonPressed &&
                     _session.RunLocalWorldAdministrationUx9C(true, NetworkRole.Administrator).Succeeded;
        var expected = _session.CurrentWorldAdministrationUx9C;
        var reopened = new StudioSessionController();
        var reopenedOk = probed && expected is not null && reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.CurrentWorldAdministrationUx9C is { } actual &&
                         string.Equals(new NetworkWorldAdministrationStore().Serialize(expected),
                             new NetworkWorldAdministrationStore().Serialize(actual), StringComparison.Ordinal);
        reopened.Close();
        RefreshAuditUx9C();
        return preview.CanonicalDryRun && applied && undo && redo && save && probed && reopenedOk &&
               _session.WorldAdministrationProbeUx9C.ResourcesReleased && EvidenceSourceUntouched;
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is not null)
        {
            _evidenceSession.Close();
            _evidenceSession = null;
        }
        Visible = false;
        ResetUx9C();
        DeleteEvidenceUx9C();
        RestoreSourceSessionUx9C();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9C();
        _moduleChoice.Select(1);
        SelectModuleUx9C(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx9C();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx9C;
        UpdateBoundsUx9C();
    }

    private void BuildUiUx9C()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color("101410f2");
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _dialog = StudioStyle.Panel("Ux9CWorldAdministration", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var root = new VBoxContainer
        {
            Name = "Ux9CRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 6);
        _dialog.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(headingText);
        headingText.AddChild(StudioStyle.Label("Réseau · monde et administration locale", 20, StudioStyle.Text));
        headingText.AddChild(StudioStyle.Label(
            "Instances, zones dynamiques, comptes, commandes et modération sous une autorité persistante.",
            11, StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);

        var routes = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        routes.AddThemeConstantOverride("separation", 7);
        root.AddChild(routes);
        routes.AddChild(RouteCardUx9C("ORF-MP-009", "Instances", "Capacités, groupes et cycle de vie."));
        routes.AddChild(RouteCardUx9C("ORF-MP-010", "Zones dynamiques", "Template, clone, budgets et surcharges."));
        routes.AddChild(RouteCardUx9C("ORF-MP-016", "Comptes", "Identité stable, secrets et erreurs privées."));
        routes.AddChild(RouteCardUx9C("ORF-MP-018", "Administration", "Commandes sûres, débit et audit."));
        routes.AddChild(RouteCardUx9C("ORF-MP-019", "Modération", "Actions, preuves, appels et rétention."));
        root.AddChild(StudioStyle.Label(
            "Limite : services en mémoire uniquement · aucune API distante, socket, compte externe ou action sur le module réel.",
            10, StudioStyle.Warning));

        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _moduleChoice.ItemSelected += index => SelectModuleUx9C((int)index);
        root.AddChild(LabeledRowUx9C("Module explicite", _moduleChoice));
        _focusChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var label in new[]
                 {
                     "Instances de zones", "Création dynamique", "Authentification et comptes",
                     "Administration gouvernée", "Modération"
                 })
            _focusChoice.AddItem(label);
        _focusChoice.ItemSelected += index => SelectFocusUx9C((int)index);
        root.AddChild(LabeledRowUx9C("Workflow affiché", _focusChoice));

        var editor = StudioStyle.Panel("Ux9CEditor", StudioStyle.SurfaceRaised, 9, 1);
        editor.CustomMinimumSize = new Vector2(0, 330);
        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(editor);
        var editorStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        editorStack.AddThemeConstantOverride("separation", 7);
        editor.AddChild(editorStack);
        editorStack.AddChild(StudioStyle.Label("Édition directe de la politique sélectionnée", 14, StudioStyle.Text));
        BuildInstancesPageUx9C(editorStack);
        BuildDynamicAreasPageUx9C(editorStack);
        BuildAuthenticationPageUx9C(editorStack);
        BuildAdministrationPageUx9C(editorStack);
        BuildModerationPageUx9C(editorStack);

        var confirmations = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        confirmations.AddThemeConstantOverride("separation", 18);
        root.AddChild(confirmations);
        _approval = CheckUx9C(confirmations, "J’approuve ce seul aperçu sec pour la révision affichée");
        _probeConfirmation = CheckUx9C(confirmations, "Je confirme la sonde locale après sauvegarde");
        var review = StudioStyle.Panel("Ux9CReview", StudioStyle.SurfaceRaised, 7, 1);
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
        _previewButton = ActionButtonUx9C(actions, "Prévisualiser", PreviewCurrentUx9C, 132, true);
        _applyButton = ActionButtonUx9C(actions, "Appliquer", ApplyCurrentUx9C, 104);
        _undoButton = ActionButtonUx9C(actions, "Annuler", UndoUx9C, 92);
        _redoButton = ActionButtonUx9C(actions, "Rétablir", RedoUx9C, 92);
        _saveButton = ActionButtonUx9C(actions, "Enregistrer", SaveUx9C, 112);
        _probeButton = ActionButtonUx9C(actions, "Tester localement", ProbeUx9C, 144);
        ConnectInvalidationUx9C();
        SelectFocusUx9C(0);
        UpdateActionsUx9C();
    }

    private void BuildInstancesPageUx9C(VBoxContainer parent)
    {
        var page = FocusPageUx9C(parent, "Instances bornées par template");
        var grid = GridUx9C(page);
        _templateIds = LineUx9C(grid, "Templates autorisés");
        _maximumInstances = SpinUx9C(grid, "Instances max.", 1, 128, 1);
        _maximumPlayers = SpinUx9C(grid, "Joueurs / instance", 1, 64, 1);
        _garbageCollection = SpinUx9C(grid, "Collecte (minutes)", 1, 10080, 1);
        page.AddChild(StudioStyle.Label(
            "Allocation de groupe, persistance de métadonnées et instanciation de scène restent imposées.",
            10, StudioStyle.Success));
    }

    private void BuildDynamicAreasPageUx9C(VBoxContainer parent)
    {
        var page = FocusPageUx9C(parent, "Création dynamique sous budgets serveur");
        var grid = GridUx9C(page);
        _overrideKeys = LineUx9C(grid, "Surcharges autorisées");
        _maximumEntities = SpinUx9C(grid, "Entités max.", 1, 100000, 1);
        _maximumResources = SpinUx9C(grid, "Ressources max. (Mio)", 1, 65536, 1);
        page.AddChild(StudioStyle.Label(
            "Création par template, clone d’une zone existante et autorité serveur restent imposés.",
            10, StudioStyle.Success));
    }

    private void BuildAuthenticationPageUx9C(VBoxContainer parent)
    {
        var page = FocusPageUx9C(parent, "Authentification privée et non énumérable");
        var grid = GridUx9C(page);
        _sessionLifetime = SpinUx9C(grid, "Session (minutes)", 5, 43200, 5);
        var checks = new HBoxContainer();
        page.AddChild(checks);
        _oauth = CheckUx9C(checks, "Activer OAuth");
        _anonymous = CheckUx9C(checks, "Activer Anonymous");
        page.AddChild(StudioStyle.Label(
            "Local, OpenID Connect et Platform restent requis · IDs stables, confidentialité, secrets hachés et erreurs identiques imposés.",
            10, StudioStyle.Success));
    }

    private void BuildAdministrationPageUx9C(VBoxContainer parent)
    {
        var page = FocusPageUx9C(parent, "Commandes locales autorisées et auditées");
        var grid = GridUx9C(page);
        _requestsPerMinute = SpinUx9C(grid, "Requêtes / minute", 1, 10000, 1);
        _safeCommands = LineUx9C(grid, "Commandes sûres");
        page.AddChild(StudioStyle.Label(
            "Permission demandée · Administrateur. API/CLI décrivent la politique ; Studio n’ouvre aucun listener.",
            10, StudioStyle.Warning));
    }

    private void BuildModerationPageUx9C(VBoxContainer parent)
    {
        var page = FocusPageUx9C(parent, "Modération traçable et rétention bornée");
        var grid = GridUx9C(page);
        _moderationRetention = SpinUx9C(grid, "Rétention privée (jours)", 1, 3650, 1);
        page.AddChild(StudioStyle.Label(
            "Compte, IP, appareil, mute, timeout, kick, ban, signalement, preuve et note d’appel restent imposés.",
            10, StudioStyle.Success));
    }

    private VBoxContainer FocusPageUx9C(VBoxContainer parent, string title)
    {
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 7);
        page.AddChild(StudioStyle.Label(title, 12, StudioStyle.Text));
        parent.AddChild(page);
        _focusPages.Add(page);
        return page;
    }

    private void OpenCurrentUx9C()
    {
        Visible = true;
        ResetUx9C();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionner un module");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(_session.Project.Module.DisplayName));
        _moduleChoice.Select(0);
        RefreshAuditUx9C();
        UpdateBoundsUx9C();
        UpdateActionsUx9C();
    }

    private void SelectModuleUx9C(int index)
    {
        _explicitSelection = index == 1 && _session.Project is not null;
        _session.CancelWorldAdministrationApprovalUx9C();
        _preview = null;
        _approval.ButtonPressed = false;
        if (!_explicitSelection)
        {
            _draft = null;
            SetStatusUx9C("Sélectionnez explicitement le module.", false);
            UpdateActionsUx9C();
            return;
        }
        _draft = _session.CreateWorldAdministrationDraftUx9C(explicitlySelected: true);
        if (_draft is null)
        {
            SetStatusUx9C(_session.WorldAdministrationLoadMessageUx9C ??
                          "L’administration réseau est indisponible.", true);
            UpdateActionsUx9C();
            return;
        }
        PopulateDraftUx9C(_draft);
        SetStatusUx9C(_session.WorldAdministrationFileMissingUx9C
            ? "Profil initial chargé en mémoire · aucun fichier créé avant Enregistrer."
            : "Administration réseau chargée · prévisualisez avant toute application.", false);
        UpdateActionsUx9C();
    }

    private void PopulateDraftUx9C(StudioUx9CWorldAdministrationDraft draft)
    {
        _updating = true;
        _templateIds.Text = draft.TemplateIds;
        _maximumInstances.Value = draft.MaximumInstances;
        _maximumPlayers.Value = draft.MaximumPlayersPerInstance;
        _garbageCollection.Value = draft.GarbageCollectionMinutes;
        _overrideKeys.Text = draft.AllowedOverrideKeys;
        _maximumEntities.Value = draft.MaximumEntities;
        _maximumResources.Value = draft.MaximumResourceMegabytes;
        _sessionLifetime.Value = draft.SessionLifetimeMinutes;
        _oauth.ButtonPressed = draft.OAuthEnabled;
        _anonymous.ButtonPressed = draft.AnonymousEnabled;
        _requestsPerMinute.Value = draft.RequestsPerMinute;
        _safeCommands.Text = draft.SafeCommandIds;
        _moderationRetention.Value = draft.ModerationRetentionDays;
        _updating = false;
    }

    private StudioUx9CWorldAdministrationDraft? ReadDraftUx9C() => _draft is null ? null : _draft with
    {
        TemplateIds = _templateIds.Text,
        MaximumInstances = (int)_maximumInstances.Value,
        MaximumPlayersPerInstance = (int)_maximumPlayers.Value,
        GarbageCollectionMinutes = (int)_garbageCollection.Value,
        AllowedOverrideKeys = _overrideKeys.Text,
        MaximumEntities = (int)_maximumEntities.Value,
        MaximumResourceMegabytes = (int)_maximumResources.Value,
        SessionLifetimeMinutes = (int)_sessionLifetime.Value,
        OAuthEnabled = _oauth.ButtonPressed,
        AnonymousEnabled = _anonymous.ButtonPressed,
        RequestsPerMinute = (int)_requestsPerMinute.Value,
        SafeCommandIds = _safeCommands.Text,
        ModerationRetentionDays = (int)_moderationRetention.Value
    };

    private void PreviewCurrentUx9C()
    {
        var draft = ReadDraftUx9C();
        if (draft is null)
        {
            SetStatusUx9C("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewWorldAdministrationUx9C(draft);
        PopulatePreviewUx9C();
        SetStatusUx9C(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx9C();
    }

    private void ApplyCurrentUx9C()
    {
        var draft = ReadDraftUx9C();
        if (draft is null || !_approval.ButtonPressed) return;
        var focus = _focusChoice.Selected;
        var result = _session.ApplyApprovedWorldAdministrationUx9C(draft);
        SetStatusUx9C(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9C(1);
            _focusChoice.Select(focus);
            SelectFocusUx9C(focus);
        }
        UpdateActionsUx9C();
    }

    private void UndoUx9C()
    {
        var result = _session.UndoWorldAdministrationUx9C();
        SetStatusUx9C(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9C(1);
        }
        UpdateActionsUx9C();
    }

    private void RedoUx9C()
    {
        var result = _session.RedoWorldAdministrationUx9C();
        SetStatusUx9C(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9C(1);
        }
        UpdateActionsUx9C();
    }

    private void SaveUx9C()
    {
        var result = _session.SaveWorldAdministrationUx9C();
        SetStatusUx9C(result.Message, !result.Succeeded);
        UpdateActionsUx9C();
    }

    private void ProbeUx9C()
    {
        var result = _session.RunLocalWorldAdministrationUx9C(_probeConfirmation.ButtonPressed,
            NetworkRole.Administrator);
        SetStatusUx9C(result.Message, !result.Succeeded);
        RefreshAuditUx9C();
        UpdateActionsUx9C();
    }

    private void PopulatePreviewUx9C()
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
            $"{_preview.Message}\nChangements · {changes}\n" +
            $"Sondes · instance {probe.InstanceCode}, {probe.InstancePlayers} joueur · zone {probe.DynamicAreaCode} · " +
            $"compte {probe.AuthenticationCode}, erreur privée {YesUx9C(probe.NonEnumeratingFailure)} · " +
            $"administration {probe.AdministrationCode}, commande non listée {BlockedUx9C(probe.UnsafeCommandBlocked)} · " +
            $"modération {probe.ModerationCode}, audit {probe.ModerationAuditEntries} · ressources {ReleasedUx9C(probe.ResourcesReleased)}.";
    }

    private void RefreshAuditUx9C()
    {
        var audit = _session.WorldAdministrationAuditUx9C;
        var probe = _session.WorldAdministrationProbeUx9C;
        _auditSummary.Text = audit.Count == 0
            ? "Journal local · aucune entrée"
            : $"Journal local · {audit.Count} entrées · sonde : {probe.Status}, ressources {ReleasedUx9C(probe.ResourcesReleased)}.";
    }

    private void ConfigureEvidenceUx9C(string mode)
    {
        if (_draft is null) return;
        _updating = true;
        _templateIds.Text = "area:crypt-template, area:forest-template";
        _maximumInstances.Value = 24;
        _maximumPlayers.Value = 12;
        _garbageCollection.Value = 10;
        _overrideKeys.Text = "environment, encounters, entry-point, lighting";
        _maximumEntities.Value = 1500;
        _maximumResources.Value = 768;
        _sessionLifetime.Value = 180;
        _oauth.ButtonPressed = false;
        _anonymous.ButtonPressed = false;
        _requestsPerMinute.Value = 90;
        _safeCommands.Text = "operations.inspect, player.kick";
        _moderationRetention.Value = 120;
        _approval.ButtonPressed = true;
        _probeConfirmation.ButtonPressed = true;
        var focus = mode.Contains("dynamic", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("auth", StringComparison.OrdinalIgnoreCase) ? 2 :
            mode.Contains("admin", StringComparison.OrdinalIgnoreCase) ? 3 :
            mode.Contains("moderation", StringComparison.OrdinalIgnoreCase) ? 4 : 0;
        _focusChoice.Select(focus);
        SelectFocusUx9C(focus);
        _updating = false;
        InvalidateUx9C(cancelApproval: false, preserveApproval: true);
    }

    private void SelectFocusUx9C(int index)
    {
        for (var page = 0; page < _focusPages.Count; page++) _focusPages[page].Visible = page == index;
    }

    private void ConnectInvalidationUx9C()
    {
        foreach (var line in new[] { _templateIds, _overrideKeys, _safeCommands })
            line.TextChanged += _ => InvalidateUx9C();
        foreach (var spin in new[]
                 {
                     _maximumInstances, _maximumPlayers, _garbageCollection, _maximumEntities,
                     _maximumResources, _sessionLifetime, _requestsPerMinute, _moderationRetention
                 })
            spin.ValueChanged += _ => InvalidateUx9C();
        foreach (var check in new[] { _oauth, _anonymous }) check.Toggled += _ => InvalidateUx9C();
        _approval.Toggled += _ => UpdateActionsUx9C();
        _probeConfirmation.Toggled += _ => UpdateActionsUx9C();
    }

    private void InvalidateUx9C(bool cancelApproval = true, bool preserveApproval = false)
    {
        if (_updating) return;
        _preview = null;
        if (cancelApproval) _session.CancelWorldAdministrationApprovalUx9C();
        if (!preserveApproval) _approval.ButtonPressed = false;
        PopulatePreviewUx9C();
        UpdateActionsUx9C();
    }

    private void UpdateActionsUx9C()
    {
        var selected = _explicitSelection && _draft is not null;
        _previewButton.Disabled = !selected;
        _applyButton.Disabled = !selected || !PreviewReady || !_approval.ButtonPressed;
        _undoButton.Disabled = !_canUndo;
        _redoButton.Disabled = !_canRedo;
        _saveButton.Disabled = !_session.WorldAdministrationHasUnsavedChangesUx9C;
        _probeButton.Disabled = !selected || PreviewReady || _session.WorldAdministrationHasUnsavedChangesUx9C ||
                                !_probeConfirmation.ButtonPressed;
    }

    private void ResetUx9C()
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
        if (_previewSummary is not null) PopulatePreviewUx9C();
    }

    private SourceSnapshotUx9C CaptureSourceUx9C(StudioSessionController session) => new(
        session.Project, session.History, session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [], session.IsDirty, session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(), session.SelectedEntityId,
        session.CaptureWorldAdministrationApprovalUx9C());

    private bool SourceUnchangedUx9C()
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
               source.CaptureWorldAdministrationApprovalUx9C() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx9C()
    {
        if (_sourceSession is not null) _session = _sourceSession;
        _sourceSession = null;
        _sourceSnapshot = null;
        _sourceSessionRestored = true;
    }

    private void DeleteEvidenceUx9C()
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-c-ui-", StringComparison.Ordinal))
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
            SetStatusUx9C($"La copie temporaire n’a pas pu être libérée : {exception.Message}", true);
        }
    }

    private void UpdateBoundsUx9C()
    {
        if (_dialog is null) return;
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(960, size.X - 48), Math.Max(700, size.Y - 36));
    }

    private void SetStatusUx9C(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private static Control RouteCardUx9C(string route, string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux9C" + route.Replace("-", string.Empty),
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

    private static Control LabeledRowUx9C(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(title);
        row.AddChild(control);
        return row;
    }

    private static GridContainer GridUx9C(Control parent)
    {
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 7);
        parent.AddChild(grid);
        return grid;
    }

    private static LineEdit LineUx9C(GridContainer parent, string label)
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

    private static SpinBox SpinUx9C(GridContainer parent, string label, double min, double max, double step)
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

    private static CheckBox CheckUx9C(Control parent, string text)
    {
        var check = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(check);
        return check;
    }

    private static Button ActionButtonUx9C(HBoxContainer parent, string text, Action action, float width,
        bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static string YesUx9C(bool value) => value ? "validée" : "refusée";
    private static string BlockedUx9C(bool value) => value ? "refusée" : "acceptée à tort";
    private static string ReleasedUx9C(bool value) => value ? "libérées" : "encore actives";

    private static IEnumerable<string> DescendantTextUx9C(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is CheckBox check) yield return check.Text;
        if (node is LineEdit line) yield return line.Text;
        if (node is OptionButton option)
            for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx9C(child)) yield return value;
    }

    private static bool ContainsUuidUx9C(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);

    private sealed record SourceSnapshotUx9C(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool Dirty,
        Forge.Domain.Areas.AreaId? Area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? Selected,
        StudioUx9CApprovalSnapshot Approval);
}
