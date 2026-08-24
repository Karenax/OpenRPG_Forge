using System.Text.RegularExpressions;
using Forge.ModuleFormat.Network;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioNetworkFoundationWorkbenchUx9A : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx9A? _sourceSnapshot;
    private StudioUx9AFoundationDraft? _draft;
    private StudioUx9AFoundationPreview? _preview;
    private bool _explicitSelection;
    private bool _updating;
    private bool _canUndo;
    private bool _canRedo;
    private bool _evidenceResourcesReleased = true;
    private bool _sourceSessionRestored = true;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private LineEdit _serverName = null!;
    private SpinBox _port = null!;
    private SpinBox _serverCapacity = null!;
    private SpinBox _partyCapacity = null!;
    private OptionButton _loot = null!;
    private OptionButton _separation = null!;
    private SpinBox _deltaBudget = null!;
    private SpinBox _interestDistance = null!;
    private CheckBox _interestArea = null!;
    private CheckBox _interestDistanceRule = null!;
    private CheckBox _interestVisibility = null!;
    private CheckBox _interestParty = null!;
    private CheckBox _interestOwnership = null!;
    private CheckBox _explicitSubscriptions = null!;
    private CheckBox _approval = null!;
    private CheckBox _liveConfirmation = null!;
    private Label _previewSummary = null!;
    private Label _liveSummary = null!;
    private Label _auditSummary = null!;
    private Label _status = null!;
    private Button _previewButton = null!;
    private Button _applyButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _saveButton = null!;
    private Button _startButton = null!;
    private Button _disconnectButton = null!;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? WorkspaceChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool HasExplicitSelection => _explicitSelection;
    internal bool PreviewReady => _preview is
    {
        Succeeded: true, CanApply: true, CanonicalDryRun: true,
        AuthoritativeRuntime: true, PartyRuntime: true, ReplicationRuntime: true, InterestRuntime: true
    };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx9A();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx9A(this));
            return Visible && text.Contains("ORF-MP-002", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-007", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-011", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-012", StringComparison.Ordinal) &&
                   text.Contains("loopback", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Déconnecter", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun client distant", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx9A(text);
        }
    }

    internal StudioNetworkFoundationWorkbenchUx9A(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx9A();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9A();
    }

    internal void Close()
    {
        if (_evidenceSession is not null)
        {
            CloseEvidence();
            return;
        }
        if (!Visible) return;
        if (_session.IsLocalNetworkConnectedUx9A) _session.DisconnectLocalNetworkSessionUx9A();
        Visible = false;
        _session.CancelNetworkFoundationApprovalUx9A();
        ResetUx9A();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx9A(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux9-a-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        var baseline = NetworkFoundationProfile.CreateDefault("evidence:ux9-a-ui");
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !WorkspaceNetworkFoundationStore.Save(_evidenceRoot, baseline).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx9A();
            RestoreSourceSessionUx9A();
            return false;
        }

        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx9A();
        _moduleChoice.Select(1);
        SelectModuleUx9A(1);
        ConfigureEvidenceUx9A(mode);
        PreviewCurrentUx9A();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady && ShowsHumanSurface &&
                    EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_A_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
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
        var draft = ReadDraftUx9A();
        if (draft is null) return false;
        var preview = _session.PreviewNetworkFoundationUx9A(draft);
        var applied = preview.CanApply && _approval.ButtonPressed &&
                      _session.ApplyApprovedNetworkFoundationUx9A(draft).Succeeded;
        var undo = applied && _session.UndoNetworkFoundationUx9A().Succeeded;
        var redo = undo && _session.RedoNetworkFoundationUx9A().Succeeded;
        var save = redo && _session.SaveNetworkFoundationUx9A().Succeeded;
        var started = save && _liveConfirmation.ButtonPressed &&
                      _session.StartLocalNetworkSessionUx9A(true, NetworkRole.Administrator).Succeeded;
        var connected = started && _session.LocalNetworkSnapshotUx9A.Connected;
        var disconnected = connected && _session.DisconnectLocalNetworkSessionUx9A().Succeeded &&
                           !_session.IsLocalNetworkConnectedUx9A;
        var expected = _session.CurrentNetworkFoundationUx9A;
        var reopened = new StudioSessionController();
        var reopenedOk = disconnected && expected is not null && reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.CurrentNetworkFoundationUx9A is { } actual &&
                         string.Equals(new NetworkFoundationStore().Serialize(expected),
                             new NetworkFoundationStore().Serialize(actual), StringComparison.Ordinal);
        reopened.Close();
        RefreshLiveUx9A();
        return preview.CanonicalDryRun && applied && undo && redo && save && started && connected && disconnected &&
               reopenedOk && EvidenceSourceUntouched;
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is not null)
        {
            if (_evidenceSession.IsLocalNetworkConnectedUx9A)
                _evidenceSession.DisconnectLocalNetworkSessionUx9A();
            _evidenceSession.Close();
            _evidenceSession = null;
        }
        Visible = false;
        ResetUx9A();
        DeleteEvidenceUx9A();
        RestoreSourceSessionUx9A();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9A();
        _moduleChoice.Select(1);
        SelectModuleUx9A(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx9A();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx9A;
        UpdateBoundsUx9A();
    }

    private void BuildUiUx9A()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color("101410f2");
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        _dialog = StudioStyle.Panel("Ux9ANetworkFoundation", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var root = new VBoxContainer
        {
            Name = "Ux9ARoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 8);
        _dialog.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(headingText);
        headingText.AddChild(StudioStyle.Label("Session réseau locale autoritaire", 20, StudioStyle.Text));
        headingText.AddChild(StudioStyle.Label(
            "Configuration persistante, sonde réelle en mémoire et connexion loopback toujours déconnectable.",
            11, StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);

        var routes = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        routes.AddThemeConstantOverride("separation", 8);
        root.AddChild(routes);
        routes.AddChild(RouteCardUx9A("ORF-MP-002", "Serveur autoritaire", "Intentions clientes validées, état accepté répliqué et refus audités."));
        routes.AddChild(RouteCardUx9A("ORF-MP-007", "Parties & groupes", "Invitations, rôles, butin, quêtes, marqueurs et séparation."));
        routes.AddChild(RouteCardUx9A("ORF-MP-011", "Réplication", "Composants allowlistés, deltas bornés, prédiction et réconciliation."));
        routes.AddChild(RouteCardUx9A("ORF-MP-012", "Gestion d’intérêt", "Zone, distance, visibilité, groupe, propriété et abonnements."));
        root.AddChild(StudioStyle.Label(
            "Limite : session locale dans Studio uniquement · aucun client distant, port public ou hébergement persistant.",
            10, StudioStyle.Warning));

        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _moduleChoice.ItemSelected += index => SelectModuleUx9A((int)index);
        root.AddChild(LabeledRowUx9A("Module explicite", _moduleChoice));

        var columns = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(0, 430)
        };
        columns.AddThemeConstantOverride("separation", 10);
        root.AddChild(columns);
        var configuration = StudioStyle.Panel("Ux9AConfiguration", StudioStyle.SurfaceRaised, 8, 1);
        configuration.CustomMinimumSize = new Vector2(1120, 0);
        configuration.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        columns.AddChild(configuration);
        var configRoot = new VBoxContainer();
        configRoot.AddThemeConstantOverride("separation", 6);
        configuration.AddChild(configRoot);
        configRoot.AddChild(StudioStyle.Label("Configuration et politiques", 14, StudioStyle.Text));
        var configGrid = new GridContainer
        {
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        configGrid.AddThemeConstantOverride("h_separation", 8);
        configGrid.AddThemeConstantOverride("v_separation", 5);
        configRoot.AddChild(configGrid);
        _serverName = LineUx9A(configGrid, "Nom local");
        _port = SpinUx9A(configGrid, "Port loopback", 1024, 65535, 1);
        _serverCapacity = SpinUx9A(configGrid, "Capacité serveur", 1, 256, 1);
        _partyCapacity = SpinUx9A(configGrid, "Taille de groupe", 2, 64, 1);
        _loot = ChoiceUx9A(configGrid, "Butin", ["Chef", "Tour par tour", "Besoin avant cupidité"]);
        _separation = ChoiceUx9A(configGrid, "Séparation", ["Autoriser", "Avertir", "Bloquer"]);
        _deltaBudget = SpinUx9A(configGrid, "Budget delta (octets)", 1024, 4_194_304, 1024);
        _interestDistance = SpinUx9A(configGrid, "Distance d’intérêt", 1, 10_000, 1);
        configRoot.AddChild(StudioStyle.Label(
            "Autorité imposée : mutations validées serveur · clients = intentions · état accepté répliqué · refus audités.",
            10, StudioStyle.Success));
        var checks = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        configRoot.AddChild(checks);
        _interestArea = CheckUx9A(checks, "Même zone");
        _interestDistanceRule = CheckUx9A(checks, "Distance");
        _interestVisibility = CheckUx9A(checks, "Visibilité");
        _interestParty = CheckUx9A(checks, "Même groupe");
        _interestOwnership = CheckUx9A(checks, "Propriété");
        _explicitSubscriptions = CheckUx9A(checks, "Abonnement explicite");

        var operations = StudioStyle.Panel("Ux9AOperations", StudioStyle.SurfaceRaised, 8, 1);
        operations.CustomMinimumSize = new Vector2(650, 0);
        operations.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        columns.AddChild(operations);
        var operationRoot = new VBoxContainer();
        operationRoot.AddThemeConstantOverride("separation", 7);
        operations.AddChild(operationRoot);
        operationRoot.AddChild(StudioStyle.Label("Contrôle local et audit", 14, StudioStyle.Text));
        operationRoot.AddChild(StudioStyle.Label("Permission demandée · Administrateur", 11, StudioStyle.Muted));
        _liveSummary = StudioStyle.Label("Déconnecté", 12, StudioStyle.Text);
        _liveSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        operationRoot.AddChild(_liveSummary);
        _auditSummary = StudioStyle.Label("Journal local · aucune entrée", 10, StudioStyle.Muted);
        _auditSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        operationRoot.AddChild(_auditSummary);
        _liveConfirmation = CheckUx9A(operationRoot,
            "Je confirme le démarrage de cette session locale");
        operationRoot.AddChild(StudioStyle.Label(
            "Déconnecter ferme la connexion opérateur et libère la session locale.", 10, StudioStyle.Muted));

        _approval = CheckUx9A(root, "J’approuve ce seul aperçu sec pour la révision affichée");
        var review = StudioStyle.Panel("Ux9AReview", StudioStyle.SurfaceRaised, 7, 1);
        review.CustomMinimumSize = new Vector2(0, 92);
        root.AddChild(review);
        _previewSummary = StudioStyle.Label("Prévisualisez la configuration · aucune session démarrée.", 10,
            StudioStyle.Muted);
        _previewSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        review.AddChild(_previewSummary);

        _status = StudioStyle.Label("Sélectionnez explicitement le module.", 10, StudioStyle.Muted);
        root.AddChild(_status);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        _previewButton = ActionButtonUx9A(actions, "Prévisualiser", PreviewCurrentUx9A, 132, true);
        _applyButton = ActionButtonUx9A(actions, "Appliquer", ApplyCurrentUx9A, 104);
        _undoButton = ActionButtonUx9A(actions, "Annuler", UndoUx9A, 92);
        _redoButton = ActionButtonUx9A(actions, "Rétablir", RedoUx9A, 92);
        _saveButton = ActionButtonUx9A(actions, "Enregistrer", SaveUx9A, 112);
        _startButton = ActionButtonUx9A(actions, "Démarrer local", StartLocalUx9A, 138);
        _disconnectButton = ActionButtonUx9A(actions, "Déconnecter", DisconnectUx9A, 116);

        ConnectInvalidationUx9A();
        UpdateActionsUx9A();
    }

    private void OpenCurrentUx9A()
    {
        Visible = true;
        ResetUx9A();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionner un module");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(_session.Project.Module.DisplayName));
        RefreshLiveUx9A();
        UpdateBoundsUx9A();
        UpdateActionsUx9A();
    }

    private void SelectModuleUx9A(int index)
    {
        _explicitSelection = index == 1 && _session.Project is not null;
        _session.CancelNetworkFoundationApprovalUx9A();
        _preview = null;
        _approval.ButtonPressed = false;
        if (!_explicitSelection)
        {
            _draft = null;
            SetStatusUx9A("Sélectionnez explicitement le module.", false);
            UpdateActionsUx9A();
            return;
        }
        _draft = _session.CreateNetworkFoundationDraftUx9A(explicitlySelected: true);
        if (_draft is null)
        {
            SetStatusUx9A(_session.NetworkFoundationLoadMessageUx9A ?? "Le socle réseau est indisponible.", true);
            UpdateActionsUx9A();
            return;
        }
        PopulateDraftUx9A(_draft);
        SetStatusUx9A(_session.NetworkFoundationFileMissingUx9A
            ? "Profil initial chargé en mémoire · aucun fichier créé avant Enregistrer."
            : "Socle réseau chargé · prévisualisez avant toute application.", false);
        UpdateActionsUx9A();
    }

    private void PopulateDraftUx9A(StudioUx9AFoundationDraft draft)
    {
        _updating = true;
        _serverName.Text = draft.ServerName;
        _port.Value = draft.Port;
        _serverCapacity.Value = draft.ServerCapacity;
        _partyCapacity.Value = draft.PartyCapacity;
        _loot.Select((int)draft.Loot);
        _separation.Select((int)draft.Separation);
        _deltaBudget.Value = draft.MaximumDeltaBytes;
        _interestDistance.Value = draft.MaximumDistance;
        _interestArea.ButtonPressed = draft.InterestByArea;
        _interestDistanceRule.ButtonPressed = draft.InterestByDistance;
        _interestVisibility.ButtonPressed = draft.InterestByVisibility;
        _interestParty.ButtonPressed = draft.InterestByParty;
        _interestOwnership.ButtonPressed = draft.InterestByOwnership;
        _explicitSubscriptions.ButtonPressed = draft.ExplicitSubscriptions;
        _updating = false;
    }

    private StudioUx9AFoundationDraft? ReadDraftUx9A() => _draft is null ? null : _draft with
    {
        ServerName = _serverName.Text,
        Port = (int)_port.Value,
        ServerCapacity = (int)_serverCapacity.Value,
        PartyCapacity = (int)_partyCapacity.Value,
        Loot = (PartyLootPolicy)_loot.Selected,
        Separation = (PartySeparationPolicy)_separation.Selected,
        MaximumDeltaBytes = (int)_deltaBudget.Value,
        MaximumDistance = (float)_interestDistance.Value,
        InterestByArea = _interestArea.ButtonPressed,
        InterestByDistance = _interestDistanceRule.ButtonPressed,
        InterestByVisibility = _interestVisibility.ButtonPressed,
        InterestByParty = _interestParty.ButtonPressed,
        InterestByOwnership = _interestOwnership.ButtonPressed,
        ExplicitSubscriptions = _explicitSubscriptions.ButtonPressed
    };

    private void PreviewCurrentUx9A()
    {
        var draft = ReadDraftUx9A();
        if (draft is null)
        {
            SetStatusUx9A("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewNetworkFoundationUx9A(draft);
        PopulatePreviewUx9A();
        SetStatusUx9A(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx9A();
    }

    private void ApplyCurrentUx9A()
    {
        var draft = ReadDraftUx9A();
        if (draft is null || !_approval.ButtonPressed) return;
        var result = _session.ApplyApprovedNetworkFoundationUx9A(draft);
        SetStatusUx9A(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9A(1);
        }
        UpdateActionsUx9A();
    }

    private void UndoUx9A()
    {
        var result = _session.UndoNetworkFoundationUx9A();
        SetStatusUx9A(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9A(1);
        }
        UpdateActionsUx9A();
    }

    private void RedoUx9A()
    {
        var result = _session.RedoNetworkFoundationUx9A();
        SetStatusUx9A(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            WorkspaceChanged?.Invoke();
            SelectModuleUx9A(1);
        }
        UpdateActionsUx9A();
    }

    private void SaveUx9A()
    {
        var result = _session.SaveNetworkFoundationUx9A();
        SetStatusUx9A(result.Message, !result.Succeeded);
        UpdateActionsUx9A();
    }

    private void StartLocalUx9A()
    {
        var result = _session.StartLocalNetworkSessionUx9A(_liveConfirmation.ButtonPressed,
            NetworkRole.Administrator);
        SetStatusUx9A(result.Message, !result.Succeeded);
        RefreshLiveUx9A();
        UpdateActionsUx9A();
    }

    private void DisconnectUx9A()
    {
        var result = _session.DisconnectLocalNetworkSessionUx9A();
        SetStatusUx9A(result.Message, !result.Succeeded);
        _liveConfirmation.ButtonPressed = false;
        RefreshLiveUx9A();
        UpdateActionsUx9A();
    }

    private void PopulatePreviewUx9A()
    {
        if (_preview is null)
        {
            _previewSummary.Text = "Prévisualisez la configuration · aucune session démarrée.";
            return;
        }
        var changes = _preview.SemanticDiff.Count == 0 ? "Aucun changement" :
            string.Join(" · ", _preview.SemanticDiff);
        var probe = _preview.Probe;
        _previewSummary.Text =
            $"{_preview.Message}\n" +
            $"Changements · {changes}\n" +
            $"Sonde locale · autorité {YesUx9A(_preview.AuthoritativeRuntime)} · groupe {probe.PartyMembers} membres · " +
            $"réplication {probe.ReplicatedEntities} entité · delta {(probe.DeltaCompressed ? "compressé" : "brut")} · " +
            $"intérêt {YesUx9A(_preview.InterestRuntime)} · audit {probe.AuditEntries} entrées.";
    }

    private void RefreshLiveUx9A()
    {
        var live = _session.LocalNetworkSnapshotUx9A;
        _liveSummary.Text = live.Connected
            ? $"{live.Status}\nJoueurs {live.ConnectedPlayers} · groupe {live.PartyMembers} · entités {live.ReplicatedEntities} · {live.AuthorityDecision}"
            : "Déconnecté · aucune ressource réseau locale active.";
        var audit = _session.LocalNetworkAuditUx9A;
        _auditSummary.Text = audit.Count == 0
            ? "Journal local · aucune entrée"
            : $"Journal local · {audit.Count} entrées · {HumanAuditUx9A(audit[^1])}";
    }

    private void ConfigureEvidenceUx9A(string mode)
    {
        if (_draft is null) return;
        _updating = true;
        _serverName.Text = "Crypte des Cendres · session locale";
        _serverCapacity.Value = 12;
        _partyCapacity.Value = 6;
        _loot.Select((int)PartyLootPolicy.RoundRobin);
        _separation.Select((int)PartySeparationPolicy.Block);
        _deltaBudget.Value = 131_072;
        _interestDistance.Value = 384;
        _interestArea.ButtonPressed = true;
        _interestDistanceRule.ButtonPressed = true;
        _interestVisibility.ButtonPressed = false;
        _interestParty.ButtonPressed = true;
        _interestOwnership.ButtonPressed = true;
        _explicitSubscriptions.ButtonPressed = true;
        _approval.ButtonPressed = true;
        _liveConfirmation.ButtonPressed = true;
        _updating = false;
        InvalidateUx9A(cancelApproval: false, preserveApproval: true);
        _ = mode;
    }

    private void ConnectInvalidationUx9A()
    {
        _serverName.TextChanged += _ => InvalidateUx9A();
        foreach (var spin in new[] { _port, _serverCapacity, _partyCapacity, _deltaBudget, _interestDistance })
            spin.ValueChanged += _ => InvalidateUx9A();
        _loot.ItemSelected += _ => InvalidateUx9A();
        _separation.ItemSelected += _ => InvalidateUx9A();
        foreach (var check in new[]
                 {
                     _interestArea, _interestDistanceRule, _interestVisibility, _interestParty,
                     _interestOwnership, _explicitSubscriptions
                 })
            check.Toggled += _ => InvalidateUx9A();
        _approval.Toggled += _ => UpdateActionsUx9A();
        _liveConfirmation.Toggled += _ => UpdateActionsUx9A();
    }

    private void InvalidateUx9A(bool cancelApproval = true, bool preserveApproval = false)
    {
        if (_updating) return;
        _preview = null;
        if (cancelApproval) _session.CancelNetworkFoundationApprovalUx9A();
        if (!preserveApproval) _approval.ButtonPressed = false;
        PopulatePreviewUx9A();
        UpdateActionsUx9A();
    }

    private void UpdateActionsUx9A()
    {
        var selected = _explicitSelection && _draft is not null;
        var connected = _session.IsLocalNetworkConnectedUx9A;
        _previewButton.Disabled = !selected || connected;
        _applyButton.Disabled = !selected || connected || !PreviewReady || !_approval.ButtonPressed;
        _undoButton.Disabled = connected || !_canUndo;
        _redoButton.Disabled = connected || !_canRedo;
        _saveButton.Disabled = connected || !_session.NetworkFoundationHasUnsavedChangesUx9A;
        _startButton.Disabled = !selected || connected || PreviewReady || _session.NetworkFoundationHasUnsavedChangesUx9A ||
                                !_liveConfirmation.ButtonPressed;
        _disconnectButton.Disabled = !connected;
    }

    private void ResetUx9A()
    {
        _draft = null;
        _preview = null;
        _explicitSelection = false;
        _canUndo = false;
        _canRedo = false;
        _updating = true;
        if (_approval is not null) _approval.ButtonPressed = false;
        if (_liveConfirmation is not null) _liveConfirmation.ButtonPressed = false;
        _updating = false;
        if (_previewSummary is not null) PopulatePreviewUx9A();
    }

    private SourceSnapshotUx9A CaptureSourceUx9A(StudioSessionController session) => new(
        session.Project,
        session.History,
        session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [],
        session.IsDirty,
        session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(),
        session.SelectedEntityId,
        session.CaptureNetworkFoundationApprovalUx9A());

    private bool SourceUnchangedUx9A()
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
               source.CaptureNetworkFoundationApprovalUx9A() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx9A()
    {
        if (_sourceSession is not null) _session = _sourceSession;
        _sourceSession = null;
        _sourceSnapshot = null;
        _sourceSessionRestored = true;
    }

    private void DeleteEvidenceUx9A()
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-a-ui-", StringComparison.Ordinal))
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
            SetStatusUx9A($"La copie temporaire n’a pas pu être libérée : {exception.Message}", true);
        }
    }

    private void UpdateBoundsUx9A()
    {
        if (_dialog is null) return;
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(960, size.X - 48), Math.Max(700, size.Y - 36));
    }

    private void SetStatusUx9A(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private static Control RouteCardUx9A(string route, string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux9A" + route.Replace("-", string.Empty), StudioStyle.SurfaceRaised, 6, 1);
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

    private static Control LabeledRowUx9A(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(title);
        row.AddChild(control);
        return row;
    }

    private static LineEdit LineUx9A(GridContainer parent, string label)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(220, 32) };
        parent.AddChild(value);
        return value;
    }

    private static SpinBox SpinUx9A(GridContainer parent, string label, double min, double max, double step)
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
            CustomMinimumSize = new Vector2(220, 32)
        };
        parent.AddChild(value);
        return value;
    }

    private static OptionButton ChoiceUx9A(GridContainer parent, string label, IReadOnlyList<string> values)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var choice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(220, 32) };
        foreach (var value in values) choice.AddItem(value);
        parent.AddChild(choice);
        return choice;
    }

    private static CheckBox CheckUx9A(Control parent, string text)
    {
        var check = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(check);
        return check;
    }

    private static Button ActionButtonUx9A(HBoxContainer parent, string text, Action action, float width,
        bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static string YesUx9A(bool value) => value ? "validée" : "refusée";

    private static string HumanAuditUx9A(string audit) =>
        audit.Contains("NET_DISCONNECTED", StringComparison.Ordinal) ? "déconnexion auditée" :
        audit.Contains("permission-denied", StringComparison.Ordinal) ? "permission refusée" :
        audit.Contains("confirmation-required", StringComparison.Ordinal) ? "confirmation exigée" :
        audit.Contains("NET_INTENT_FORBIDDEN", StringComparison.Ordinal) ? "mutation cliente refusée" :
        audit.StartsWith("applied:network.foundation", StringComparison.Ordinal) ? "configuration appliquée" :
        "opération réseau locale auditée";

    private static IEnumerable<string> DescendantTextUx9A(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is CheckBox check) yield return check.Text;
        if (node is LineEdit line) yield return line.Text;
        if (node is OptionButton option)
            for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx9A(child)) yield return value;
    }

    private static bool ContainsUuidUx9A(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);

    private sealed record SourceSnapshotUx9A(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool Dirty,
        Forge.Domain.Areas.AreaId? Area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? Selected,
        StudioUx9AApprovalSnapshot Approval);
}
