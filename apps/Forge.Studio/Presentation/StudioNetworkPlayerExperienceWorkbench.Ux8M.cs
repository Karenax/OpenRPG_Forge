using System.Text.RegularExpressions;
using Forge.ModuleFormat.Network;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioNetworkPlayerExperienceWorkbenchUx8M : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx8M? _sourceSnapshot;
    private StudioUx8MNetworkDraft? _draft;
    private StudioUx8MNetworkPreview? _preview;
    private bool _explicitSelection;
    private bool _updating;
    private bool _evidenceResourcesReleased = true;
    private bool _sourceSessionRestored = true;
    private bool _canUndo;
    private bool _canRedo;

    private PanelContainer _dialog = null!;
    private TabContainer _tabs = null!;
    private OptionButton _moduleChoice = null!;
    private CheckBox _direct = null!;
    private CheckBox _lan = null!;
    private CheckBox _curated = null!;
    private CheckBox _favorites = null!;
    private CheckBox _recent = null!;
    private CheckBox _invites = null!;
    private LineEdit _lobbyRuleset = null!;
    private LineEdit _contentVersion = null!;
    private LineEdit _slots = null!;
    private LineEdit _teams = null!;
    private CheckBox _characterSelection = null!;
    private CheckBox _permissions = null!;
    private CheckBox _ready = null!;
    private LineEdit _builderRuleset = null!;
    private ItemList _templates = null!;
    private LineEdit _newTemplate = null!;
    private CheckBox _builderPreview = null!;
    private CheckBox _import = null!;
    private CheckBox _serverApproval = null!;
    private LineEdit _minimumAttribute = null!;
    private LineEdit _maximumAttribute = null!;
    private readonly Dictionary<CharacterAuthorityMode, CheckBox> _modeChecks = [];
    private CheckBox _signedUploads = null!;
    private CheckBox _encrypted = null!;
    private CheckBox _migration = null!;
    private CheckBox _accountBinding = null!;
    private Label _summary = null!;
    private Label _diagnostics = null!;
    private Label _status = null!;
    private Button _previewButton = null!;
    private Button _applyButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _saveButton = null!;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? WorkspaceChanged;

    internal bool IsOpen => Visible;
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool HasExplicitSelection => _explicitSelection && _draft is not null;
    internal bool PreviewReady => _preview is
    { Succeeded: true, CanApply: true, CanonicalDryRun: true, RuntimeResolved: true, ScriptProjectionReadable: true };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx8M();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx8M(this));
            return Visible && text.Contains("ORF-MP-003", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-004", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-005", StringComparison.Ordinal) &&
                   text.Contains("ORF-MP-006", StringComparison.Ordinal) &&
                   text.Contains("aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun transport distant", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsUuidUx8M(text);
        }
    }

    internal StudioNetworkPlayerExperienceWorkbenchUx8M(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx8M();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentSessionUx8M();
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
        _session.CancelNetworkExperienceApprovalUx8M();
        ResetUx8M();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx8M(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(), $"openrpg-forge-studio-ux8-m-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded)
        {
            DeleteEvidenceUx8M();
            RestoreSourceSessionUx8M();
            return false;
        }
        var baseline = NetworkPlayerExperienceProfile.CreateDefault("evidence:ux8-m-ui");
        if (!WorkspaceNetworkPlayerExperienceStore.Save(_evidenceRoot, baseline).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx8M();
            RestoreSourceSessionUx8M();
            return false;
        }

        _evidenceSession = controller;
        _session = controller;
        OpenCurrentSessionUx8M();
        _moduleChoice.Select(1);
        SelectModuleUx8M(1);
        if (_draft is null)
        {
            CloseEvidence();
            return false;
        }
        _draft = _draft with
        {
            DirectAddress = false,
            LanDiscovery = true,
            CuratedDirectory = true,
            MaximumSlots = 12,
            MaximumTeams = 3,
            TemplateIds = ["template:adventurer", "template:ranger"],
            ImportAllowed = false,
            MinimumAttribute = 2,
            MaximumAttribute = 24,
            AllowedModes =
            [
                CharacterAuthorityMode.SignedUpload,
                CharacterAuthorityMode.ServerManaged,
                CharacterAuthorityMode.AccountBound
            ]
        };
        PopulateUx8M(_draft);
        PreviewUx8M();
        _tabs.CurrentTab = mode.Contains("lobby", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("builder", StringComparison.OrdinalIgnoreCase) ? 2 :
            mode.Contains("authority", StringComparison.OrdinalIgnoreCase) ? 3 : 0;
        var ready = UsesIsolatedEvidence && PreviewReady && HasExplicitSelection && ShowsHumanSurface &&
                    EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX8_M_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
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
        if (!UsesIsolatedEvidence || !PreviewReady || _draft is null || _evidenceRoot is null) return false;
        var before = _session.CurrentNetworkExperienceUx8M;
        var draft = ReadDraftUx8M();
        if (before is null || draft is null) return false;
        var preview = _session.PreviewNetworkExperienceUx8M(draft);
        var applied = preview.CanApply && _session.ApplyApprovedNetworkExperienceUx8M(draft).Succeeded;
        var afterApply = _session.CurrentNetworkExperienceUx8M;
        var changed = applied && afterApply is not null && afterApply.ServerRevision == before.ServerRevision + 1;
        var undone = changed && _session.UndoNetworkExperienceUx8M().Succeeded;
        var redone = undone && _session.RedoNetworkExperienceUx8M().Succeeded;
        var saved = redone && _session.SaveNetworkExperienceUx8M().Succeeded;
        var loaded = WorkspaceNetworkPlayerExperienceStore.Load(_evidenceRoot);
        var serializer = new NetworkPlayerExperienceStore();
        var persisted = saved && loaded.Succeeded && !loaded.FileMissing &&
                        _session.CurrentNetworkExperienceUx8M is { } current &&
                        serializer.Serialize(current) == serializer.Serialize(loaded.Profile);
        var reopened = new StudioSessionController();
        var reopenedOk = persisted && reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.CreateNetworkExperienceDraftUx8M(explicitlySelected: true) is { } reopenedDraft &&
                         reopenedDraft.BaseServerRevision == loaded.Profile.ServerRevision;
        reopened.Close();
        ReloadUx8M();
        return preview.CanonicalDryRun && applied && changed && undone && redone && saved && persisted && reopenedOk &&
               !_session.NetworkExperienceHasUnsavedChangesUx8M && EvidenceSourceUntouched;
    }

    internal void CloseEvidence()
    {
        if (_evidenceSession is not null)
        {
            _evidenceSession.Close();
            _evidenceSession = null;
        }
        DeleteEvidenceUx8M();
        RestoreSourceSessionUx8M();
        Visible = false;
        ResetUx8M();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        Visible = true;
        UpdateBoundsUx8M();
    }

    internal void HideSourceSurfaceAfterSmoke() => Visible = false;

    public override void _Process(double delta)
    {
        _ = delta;
        if (Visible) UpdateBoundsUx8M();
    }

    private void BuildUiUx8M()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        Color = new Color(0.01f, 0.016f, 0.027f, 0.97f);
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetProcess(true);

        _dialog = StudioStyle.Panel("Ux8MNetworkPlayerExperienceWorkbench", StudioStyle.Surface, 12, 1);
        _dialog.ClipContents = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(20, 16, 20, 14);
        _dialog.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var heading = new HBoxContainer();
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("EXPÉRIENCE JOUEUR MULTIJOUEUR", 22, StudioStyle.Text));
        titles.AddChild(StudioStyle.Label(
            "ORF-MP-003 · ORF-MP-004 · ORF-MP-005 · ORF-MP-006", 12, StudioStyle.Muted));
        heading.AddChild(titles);
        heading.AddChild(StudioStyle.Label("PROFIL WORKSPACE · AUTORITÉ SERVEUR", 11, StudioStyle.Muted));
        root.AddChild(heading);
        root.AddChild(StudioStyle.Label(
            "Configurer découverte, lobby, création et autorité des personnages. Aperçu sec canonique ; aucun transport distant, serveur public ou session joueur n’est lancé.",
            12, StudioStyle.Muted, wrap: true));

        var selection = new HBoxContainer();
        selection.AddThemeConstantOverride("separation", 8);
        selection.AddChild(StudioStyle.Label("Module", 12, StudioStyle.Muted));
        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _moduleChoice.ItemSelected += SelectModuleUx8M;
        selection.AddChild(_moduleChoice);
        root.AddChild(selection);

        _tabs = new TabContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 390)
        };
        root.AddChild(_tabs);
        BuildDiscoveryTabUx8M(_tabs);
        BuildLobbyTabUx8M(_tabs);
        BuildBuilderTabUx8M(_tabs);
        BuildAuthorityTabUx8M(_tabs);

        _summary = StudioStyle.Label("Sélectionnez le module pour préparer un aperçu sec.", 12, StudioStyle.Text, wrap: true);
        _summary.CustomMinimumSize = new Vector2(0, 76);
        root.AddChild(_summary);
        _diagnostics = StudioStyle.Label(string.Empty, 11, StudioStyle.Danger, wrap: true);
        _diagnostics.CustomMinimumSize = new Vector2(0, 32);
        root.AddChild(_diagnostics);
        _status = StudioStyle.Label(
            "Configuration uniquement · le runtime et le serveur restent les autorités d’exécution.", 11,
            new Color(0.95f, 0.69f, 0.28f), wrap: true);
        root.AddChild(_status);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        _previewButton = ActionButtonUx8M("Prévisualiser", PreviewUx8M, 128, primary: true);
        _applyButton = ActionButtonUx8M("Appliquer", ApplyUx8M, 116);
        _undoButton = ActionButtonUx8M("Annuler", UndoUx8M, 104);
        _redoButton = ActionButtonUx8M("Rétablir", RedoUx8M, 104);
        _saveButton = ActionButtonUx8M("Enregistrer", SaveUx8M, 122);
        actions.AddChild(_previewButton);
        actions.AddChild(_applyButton);
        actions.AddChild(_undoButton);
        actions.AddChild(_redoButton);
        actions.AddChild(_saveButton);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        actions.AddChild(ActionButtonUx8M("Fermer", Close, 100));
        root.AddChild(actions);
        ConnectChangeSignalsUx8M();
        UpdateButtonsUx8M();
    }

    private void BuildDiscoveryTabUx8M(TabContainer tabs)
    {
        var body = AddTabUx8M(tabs, "Connexion · MP-003");
        body.AddChild(StudioStyle.Label("Découverte et connexion", 17, StudioStyle.Text));
        body.AddChild(StudioStyle.Label(
            "Choisissez au moins une voie. Favoris, récents et invitations restent obligatoires pour un parcours joueur complet.",
            12, StudioStyle.Muted, wrap: true));
        _direct = AddCheckUx8M(body, "Adresse directe");
        _lan = AddCheckUx8M(body, "Découverte sur le réseau local");
        _curated = AddCheckUx8M(body, "Annuaire curaté optionnel");
        _favorites = AddCheckUx8M(body, "Favoris");
        _recent = AddCheckUx8M(body, "Serveurs récents");
        _invites = AddCheckUx8M(body, "Liens d’invitation");
    }

    private void BuildLobbyTabUx8M(TabContainer tabs)
    {
        var body = AddTabUx8M(tabs, "Lobby · MP-004");
        body.AddChild(StudioStyle.Label("Lobby et sélection de personnage", 17, StudioStyle.Text));
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 8);
        body.AddChild(grid);
        AddLineUx8M(grid, "Profil de règles", out _lobbyRuleset);
        AddLineUx8M(grid, "Version de contenu", out _contentVersion);
        AddLineUx8M(grid, "Nombre maximal de joueurs", out _slots);
        AddLineUx8M(grid, "Nombre maximal d’équipes", out _teams);
        _characterSelection = AddCheckUx8M(body, "Sélection de personnage visible");
        _permissions = AddCheckUx8M(body, "Permissions visibles dans le lobby");
        _ready = AddCheckUx8M(body, "État prêt requis avant lancement");
    }

    private void BuildBuilderTabUx8M(TabContainer tabs)
    {
        var body = AddTabUx8M(tabs, "Création · MP-005");
        body.AddChild(StudioStyle.Label("Création de personnage pilotée par les règles", 17, StudioStyle.Text));
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 8);
        body.AddChild(grid);
        AddLineUx8M(grid, "Profil de règles", out _builderRuleset);
        AddLineUx8M(grid, "Attribut minimal", out _minimumAttribute);
        AddLineUx8M(grid, "Attribut maximal", out _maximumAttribute);
        body.AddChild(StudioStyle.Label("Modèles de personnage", 12, StudioStyle.Muted));
        _templates = new ItemList
        {
            CustomMinimumSize = new Vector2(0, 116),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single
        };
        body.AddChild(_templates);
        var templateActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _newTemplate = new LineEdit
        {
            PlaceholderText = "template:nom-humain",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(360, 0)
        };
        templateActions.AddChild(_newTemplate);
        templateActions.AddChild(ActionButtonUx8M("Ajouter", AddTemplateUx8M, 92));
        templateActions.AddChild(ActionButtonUx8M("Retirer", RemoveTemplateUx8M, 92));
        templateActions.AddChild(ActionButtonUx8M("Monter", () => MoveTemplateUx8M(-1), 88));
        templateActions.AddChild(ActionButtonUx8M("Descendre", () => MoveTemplateUx8M(1), 96));
        body.AddChild(templateActions);
        _builderPreview = AddCheckUx8M(body, "Aperçu de création disponible");
        _import = AddCheckUx8M(body, "Import local autorisé sous contrôle du serveur");
        _serverApproval = AddCheckUx8M(body, "Approbation serveur obligatoire");
    }

    private void BuildAuthorityTabUx8M(TabContainer tabs)
    {
        var body = AddTabUx8M(tabs, "Autorité · MP-006");
        body.AddChild(StudioStyle.Label("Personnages locaux ou serveur", 17, StudioStyle.Text));
        body.AddChild(StudioStyle.Label(
            "Les modes décrivent qui possède et valide le personnage. Les secrets et comptes ne sont jamais stockés dans le module.",
            12, StudioStyle.Muted, wrap: true));
        foreach (var mode in Enum.GetValues<CharacterAuthorityMode>())
        {
            var check = AddCheckUx8M(body, HumanModeUx8M(mode));
            _modeChecks[mode] = check;
        }
        _signedUploads = AddCheckUx8M(body, "Signature obligatoire pour les imports");
        _encrypted = AddCheckUx8M(body, "Stockage serveur chiffré");
        _migration = AddCheckUx8M(body, "Migration explicite autorisée");
        _accountBinding = AddCheckUx8M(body, "Liaison au compte obligatoire");
    }

    private void OpenCurrentSessionUx8M()
    {
        ResetUx8M();
        Visible = true;
        UpdateBoundsUx8M();
        _moduleChoice.AddItem("Choisir explicitement le module…");
        _moduleChoice.SetItemDisabled(0, true);
        _moduleChoice.AddItem(_session.Project?.Module.DisplayName ?? "Module ouvert");
        _moduleChoice.Select(0);
        SetStatusUx8M("Sélectionnez le module avant d’afficher ou modifier son profil réseau.", false);
        UpdateButtonsUx8M();
    }

    private void SelectModuleUx8M(long index)
    {
        if (_updating || index != 1) return;
        var draft = _session.CreateNetworkExperienceDraftUx8M(explicitlySelected: true);
        if (draft is null)
        {
            SetStatusUx8M(_session.NetworkExperienceLoadMessageUx8M ?? "Le profil réseau ne peut pas être chargé.", true);
            return;
        }
        _explicitSelection = true;
        _draft = draft;
        PopulateUx8M(draft);
        var message = _session.NetworkExperienceFileMissingUx8M
            ? "Profil initial chargé en mémoire · aucun fichier n’a encore été créé."
            : "Profil réseau enregistré chargé.";
        SetStatusUx8M(message, false);
        UpdateButtonsUx8M();
    }

    private void PopulateUx8M(StudioUx8MNetworkDraft draft)
    {
        _updating = true;
        _direct.ButtonPressed = draft.DirectAddress;
        _lan.ButtonPressed = draft.LanDiscovery;
        _curated.ButtonPressed = draft.CuratedDirectory;
        _favorites.ButtonPressed = draft.Favorites;
        _recent.ButtonPressed = draft.RecentServers;
        _invites.ButtonPressed = draft.InviteLinks;
        _lobbyRuleset.Text = draft.LobbyRulesetId;
        _contentVersion.Text = draft.ContentFingerprint;
        _slots.Text = draft.MaximumSlots.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _teams.Text = draft.MaximumTeams.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _characterSelection.ButtonPressed = draft.CharacterSelection;
        _permissions.ButtonPressed = draft.PermissionsVisible;
        _ready.ButtonPressed = draft.ReadyState;
        _builderRuleset.Text = draft.BuilderRulesetId;
        _minimumAttribute.Text = draft.MinimumAttribute.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _maximumAttribute.Text = draft.MaximumAttribute.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _templates.Clear();
        foreach (var template in draft.TemplateIds) _templates.AddItem(template);
        _builderPreview.ButtonPressed = draft.PreviewEnabled;
        _import.ButtonPressed = draft.ImportAllowed;
        _serverApproval.ButtonPressed = draft.ServerApprovalRequired;
        foreach (var item in _modeChecks) item.Value.ButtonPressed = draft.AllowedModes.Contains(item.Key);
        _signedUploads.ButtonPressed = draft.SignedUploadsRequired;
        _encrypted.ButtonPressed = draft.EncryptedAtRest;
        _migration.ButtonPressed = draft.MigrationEnabled;
        _accountBinding.ButtonPressed = draft.AccountBindingRequired;
        _updating = false;
        InvalidatePreviewUx8M();
    }

    private StudioUx8MNetworkDraft? ReadDraftUx8M()
    {
        if (_draft is null) return null;
        if (!int.TryParse(_slots.Text.Trim(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var slots) ||
            !int.TryParse(_teams.Text.Trim(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var teams) ||
            !int.TryParse(_minimumAttribute.Text.Trim(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var minimum) ||
            !int.TryParse(_maximumAttribute.Text.Trim(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var maximum))
        {
            SetStatusUx8M("Slots, équipes et bornes d’attribut doivent être des nombres entiers.", true);
            return null;
        }
        var templates = Enumerable.Range(0, _templates.ItemCount).Select(_templates.GetItemText).ToArray();
        var modes = _modeChecks.Where(item => item.Value.ButtonPressed).Select(item => item.Key).ToArray();
        return _draft with
        {
            DirectAddress = _direct.ButtonPressed,
            LanDiscovery = _lan.ButtonPressed,
            CuratedDirectory = _curated.ButtonPressed,
            Favorites = _favorites.ButtonPressed,
            RecentServers = _recent.ButtonPressed,
            InviteLinks = _invites.ButtonPressed,
            LobbyRulesetId = _lobbyRuleset.Text,
            ContentFingerprint = _contentVersion.Text,
            MaximumSlots = slots,
            MaximumTeams = teams,
            CharacterSelection = _characterSelection.ButtonPressed,
            PermissionsVisible = _permissions.ButtonPressed,
            ReadyState = _ready.ButtonPressed,
            BuilderRulesetId = _builderRuleset.Text,
            TemplateIds = templates,
            PreviewEnabled = _builderPreview.ButtonPressed,
            ImportAllowed = _import.ButtonPressed,
            ServerApprovalRequired = _serverApproval.ButtonPressed,
            MinimumAttribute = minimum,
            MaximumAttribute = maximum,
            AllowedModes = modes,
            SignedUploadsRequired = _signedUploads.ButtonPressed,
            EncryptedAtRest = _encrypted.ButtonPressed,
            MigrationEnabled = _migration.ButtonPressed,
            AccountBindingRequired = _accountBinding.ButtonPressed
        };
    }

    private void PreviewUx8M()
    {
        var draft = ReadDraftUx8M();
        if (draft is null) return;
        _draft = draft;
        _preview = _session.PreviewNetworkExperienceUx8M(draft);
        _summary.Text = _preview.Succeeded
            ? _preview.Message + "\n" + (_preview.SemanticDiff.Count == 0
                ? "Aucun changement sémantique."
                : "Changements : " + string.Join(" · ", _preview.SemanticDiff))
            : _preview.Message;
        _diagnostics.Text = _preview.Diagnostics.Count == 0
            ? string.Empty
            : "À corriger : " + string.Join(" · ", _preview.Diagnostics);
        SetStatusUx8M(_preview.Message, !_preview.Succeeded);
        UpdateButtonsUx8M();
    }

    private void ApplyUx8M()
    {
        var draft = ReadDraftUx8M();
        if (draft is null) return;
        var result = _session.ApplyApprovedNetworkExperienceUx8M(draft);
        SetStatusUx8M(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            ReloadUx8M();
            WorkspaceChanged?.Invoke();
        }
        UpdateButtonsUx8M();
    }

    private void UndoUx8M()
    {
        var result = _session.UndoNetworkExperienceUx8M();
        SetStatusUx8M(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            ReloadUx8M();
            WorkspaceChanged?.Invoke();
        }
        UpdateButtonsUx8M();
    }

    private void RedoUx8M()
    {
        var result = _session.RedoNetworkExperienceUx8M();
        SetStatusUx8M(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            ReloadUx8M();
            WorkspaceChanged?.Invoke();
        }
        UpdateButtonsUx8M();
    }

    private void SaveUx8M()
    {
        var result = _session.SaveNetworkExperienceUx8M();
        SetStatusUx8M(result.Message, !result.Succeeded);
        UpdateButtonsUx8M();
    }

    private void ReloadUx8M()
    {
        var refreshed = _session.CreateNetworkExperienceDraftUx8M(explicitlySelected: true);
        if (refreshed is null) return;
        _draft = refreshed;
        PopulateUx8M(refreshed);
        _summary.Text = "Profil rechargé depuis l’historique réseau de la session.";
    }

    private void AddTemplateUx8M()
    {
        var value = _newTemplate.Text.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;
        _templates.AddItem(value);
        _newTemplate.Clear();
        InvalidatePreviewUx8M();
    }

    private void RemoveTemplateUx8M()
    {
        var selected = _templates.GetSelectedItems();
        if (selected.Length == 0) return;
        _templates.RemoveItem(selected[0]);
        InvalidatePreviewUx8M();
    }

    private void MoveTemplateUx8M(int delta)
    {
        var selected = _templates.GetSelectedItems();
        if (selected.Length == 0) return;
        var from = selected[0];
        var to = from + delta;
        if (to < 0 || to >= _templates.ItemCount) return;
        _templates.MoveItem(from, to);
        _templates.Select(to);
        InvalidatePreviewUx8M();
    }

    private void ConnectChangeSignalsUx8M()
    {
        foreach (var check in new[]
                 {
                     _direct, _lan, _curated, _favorites, _recent, _invites, _characterSelection, _permissions,
                     _ready, _builderPreview, _import, _serverApproval, _signedUploads, _encrypted, _migration,
                     _accountBinding
                 }.Concat(_modeChecks.Values))
            check.Toggled += _ => InvalidatePreviewUx8M();
        foreach (var line in new[]
                 {
                     _lobbyRuleset, _contentVersion, _slots, _teams, _builderRuleset, _minimumAttribute,
                     _maximumAttribute
                 })
            line.TextChanged += _ => InvalidatePreviewUx8M();
    }

    private void InvalidatePreviewUx8M()
    {
        if (_updating) return;
        _session.CancelNetworkExperienceApprovalUx8M();
        _preview = null;
        if (_summary is not null && _draft is not null)
            _summary.Text = "Brouillon modifié · relancez l’aperçu sec avant d’appliquer.";
        if (_diagnostics is not null) _diagnostics.Text = string.Empty;
        UpdateButtonsUx8M();
    }

    private void ResetUx8M()
    {
        _updating = true;
        if (_moduleChoice is not null) _moduleChoice.Clear();
        if (_templates is not null) _templates.Clear();
        if (_newTemplate is not null) _newTemplate.Clear();
        _draft = null;
        _preview = null;
        _explicitSelection = false;
        _canUndo = false;
        _canRedo = false;
        if (_summary is not null) _summary.Text = "Sélectionnez le module pour préparer un aperçu sec.";
        if (_diagnostics is not null) _diagnostics.Text = string.Empty;
        _updating = false;
    }

    private void UpdateButtonsUx8M()
    {
        if (_previewButton is null) return;
        _previewButton.Disabled = _draft is null;
        _applyButton.Disabled = _preview?.CanApply != true;
        _undoButton.Disabled = !_canUndo;
        _redoButton.Disabled = !_canRedo;
        _saveButton.Disabled = _draft is null || !_session.NetworkExperienceHasUnsavedChangesUx8M;
    }

    private SourceSnapshotUx8M CaptureSourceUx8M(StudioSessionController session) => new(
        session.Project,
        session.History,
        session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [],
        session.IsDirty,
        session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(),
        session.SelectedEntityId,
        session.CaptureNetworkExperienceApprovalUx8M());

    private bool SourceUnchangedUx8M()
    {
        var source = _sourceSession;
        var snapshot = _sourceSnapshot;
        return source is null || snapshot is null ||
               ReferenceEquals(source.Project, snapshot.Project) &&
               ReferenceEquals(source.History, snapshot.History) &&
               source.History?.State.Revision == snapshot.Revision &&
               (source.History?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               source.IsDirty == snapshot.IsDirty && source.ActiveAreaId == snapshot.AreaId &&
               source.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               source.SelectedEntityId == snapshot.SelectedEntity &&
               source.CaptureNetworkExperienceApprovalUx8M() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx8M()
    {
        if (_sourceSession is null) return;
        _session = _sourceSession;
        _sourceSession = null;
        _sourceSessionRestored = true;
        _sourceSnapshot = null;
    }

    private void DeleteEvidenceUx8M()
    {
        var root = _evidenceRoot;
        if (root is not null)
        {
            try
            {
                var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(Path.GetDirectoryName(fullRoot)?.TrimEnd(Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar), temp, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(fullRoot).StartsWith("openrpg-forge-studio-ux8-m-ui-", StringComparison.Ordinal) &&
                    Directory.Exists(fullRoot))
                    Directory.Delete(fullRoot, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                SetStatusUx8M($"La copie temporaire n’a pas pu être libérée : {exception.Message}", true);
            }
        }
        _evidenceResourcesReleased = root is null || !Directory.Exists(root);
        _evidenceRoot = null;
    }

    private void UpdateBoundsUx8M()
    {
        var viewport = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(1280, viewport.X - 48), Math.Max(720, viewport.Y - 36));
    }

    private void SetStatusUx8M(string message, bool error)
    {
        var text = ScrubHumanUx8M(message);
        if (_status is not null)
        {
            _status.Text = text;
            _status.Modulate = error ? StudioStyle.Danger : StudioStyle.Muted;
        }
        StatusChanged?.Invoke(text, error);
    }

    private static VBoxContainer AddTabUx8M(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        var margin = StudioStyle.Margin(14, 12, 14, 12);
        margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(margin);
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 8);
        margin.AddChild(body);
        tabs.AddChild(scroll);
        return body;
    }

    private static CheckBox AddCheckUx8M(VBoxContainer body, string text)
    {
        var check = new CheckBox { Text = text };
        body.AddChild(check);
        return check;
    }

    private static void AddLineUx8M(GridContainer grid, string label, out LineEdit edit)
    {
        var caption = StudioStyle.Label(label, 12, StudioStyle.Muted);
        caption.CustomMinimumSize = new Vector2(220, 0);
        grid.AddChild(caption);
        edit = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(460, 0)
        };
        grid.AddChild(edit);
    }

    private static Button ActionButtonUx8M(string text, Action action, float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        return button;
    }

    private static string HumanModeUx8M(CharacterAuthorityMode mode) => mode switch
    {
        CharacterAuthorityMode.Local => "Personnage local",
        CharacterAuthorityMode.SignedUpload => "Import local signé",
        CharacterAuthorityMode.ServerManaged => "Personnage géré par le serveur",
        CharacterAuthorityMode.AccountBound => "Personnage lié au compte",
        _ => mode.ToString()
    };

    private static string ScrubHumanUx8M(string? value) => UuidPatternUx8M().Replace(value ?? string.Empty, "identifiant protégé");
    private static bool ContainsUuidUx8M(string value) => UuidPatternUx8M().IsMatch(value);

    [GeneratedRegex(@"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b")]
    private static partial Regex UuidPatternUx8M();

    private static IEnumerable<string> DescendantTextUx8M(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label label) yield return label.Text;
            if (child is Button button) yield return button.Text;
            if (child is LineEdit line) yield return line.Text;
            foreach (var nested in DescendantTextUx8M(child)) yield return nested;
        }
    }

    private sealed record SourceSnapshotUx8M(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool IsDirty,
        Forge.Domain.Areas.AreaId? AreaId,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? SelectedEntity,
        StudioUx8MApprovalSnapshot Approval);
}
