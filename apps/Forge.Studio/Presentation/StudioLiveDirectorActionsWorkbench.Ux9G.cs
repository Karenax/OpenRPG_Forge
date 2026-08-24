using System.Text.RegularExpressions;
using Forge.LiveDirector.Authoring;
using Forge.ModuleFormat.LiveDirector;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Operations;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioLiveDirectorActionsWorkbenchUx9G : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx9G? _sourceSnapshot;
    private StudioUx9GLiveActionDraft? _draft;
    private StudioUx9GLiveActionPreview? _preview;
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
    private SpinBox _maximumPossessionMinutes = null!;
    private CheckBox _requiredAtRuntime = null!;
    private OptionButton _partyPolicy = null!;
    private CheckBox _playerConsent = null!;
    private LineEdit _destructionCause = null!;
    private OptionButton _lootPolicy = null!;
    private OptionButton _questImpact = null!;
    private OptionButton _rollbackLimit = null!;
    private CheckBox _destructionConfirmed = null!;
    private OptionButton _healingScope = null!;
    private LineEdit _healingReason = null!;
    private OptionButton _aiState = null!;
    private OptionButton _movementPolicy = null!;
    private OptionButton _collisionPolicy = null!;
    private OptionButton _visibilityPolicy = null!;
    private SpinBox _possessionMinutes = null!;
    private OptionButton _inputContext = null!;
    private LineEdit _returnPoint = null!;
    private CheckBox _protectedPlayerConsent = null!;
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
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                          ReferenceEquals(_session, _evidenceSession);
    internal bool HasExplicitSelection => _explicitSelection;
    internal bool PreviewReady => _preview is
    {
        Succeeded: true, CanApply: true, CanonicalDryRun: true,
        RelocationRuntime: true, DestructionRuntime: true, HealingRuntime: true,
        LimboRuntime: true, PossessionRuntime: true
    };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx9G();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx9G(this));
            return Visible && text.Contains("ORF-DM-007", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-008", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-009", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-010", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-012", StringComparison.Ordinal) &&
                   text.Contains("orientation", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("impact quête", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("limbo", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("point de retour", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tester localement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucune connexion au serveur réel",
                       StringComparison.OrdinalIgnoreCase) && !ContainsUuidUx9G(text);
        }
    }

    internal StudioLiveDirectorActionsWorkbenchUx9G(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx9G();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9G();
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
        _session.CancelLiveActionApprovalUx9G();
        ResetUx9G();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx9G(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-g-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        var baseline = LiveDirectorActionProfile.CreateDefault("evidence:ux9-g-ui");
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !WorkspaceLiveDirectorActionStore.Save(_evidenceRoot, baseline).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx9G();
            RestoreSourceSessionUx9G();
            return false;
        }
        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx9G();
        _moduleChoice.Select(1);
        SelectModuleUx9G(1);
        ConfigureEvidenceUx9G(mode);
        PreviewCurrentUx9G();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady &&
                    ShowsHumanSurface && EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_G_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
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
        var draft = ReadDraftUx9G();
        var options = ReadOptionsUx9G();
        if (draft is null || options is null) return false;
        var preview = _session.PreviewLiveActionUx9G(draft, options);
        var applied = preview.CanApply && _approval.ButtonPressed &&
                      _session.ApplyApprovedLiveActionUx9G(draft).Succeeded;
        var undo = applied && _session.UndoLiveActionUx9G().Succeeded;
        var redo = undo && _session.RedoLiveActionUx9G().Succeeded;
        var save = redo && _session.SaveLiveActionUx9G().Succeeded;
        var probed = save && _probeConfirmation.ButtonPressed &&
                     _session.RunLocalLiveActionUx9G(true, NetworkRole.Administrator,
                         options).Succeeded;
        var expected = _session.CurrentLiveActionUx9G;
        var reopened = new StudioSessionController();
        var reopenedOk = probed && expected is not null && reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.CurrentLiveActionUx9G is { } actual &&
                         new LiveDirectorActionStore().Serialize(expected) ==
                         new LiveDirectorActionStore().Serialize(actual);
        reopened.Close();
        RefreshAuditUx9G();
        return preview.CanonicalDryRun && applied && undo && redo && save && probed &&
               reopenedOk && _session.LiveActionProbeUx9G.ResourcesReleased &&
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
        ResetUx9G();
        DeleteEvidenceUx9G();
        RestoreSourceSessionUx9G();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9G();
        _moduleChoice.Select(1);
        SelectModuleUx9G(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx9G();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx9G;
        UpdateBoundsUx9G();
    }

    private void BuildUiUx9G()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color("10121af2");
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _dialog = StudioStyle.Panel("Ux9GLiveActions", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var root = new VBoxContainer
        {
            Name = "Ux9GRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 6);
        _dialog.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(headingText);
        headingText.AddChild(StudioStyle.Label("Live Director · actions sûres", 20,
            StudioStyle.Text));
        headingText.AddChild(StudioStyle.Label(
            "Prévisualiser, autoriser puis compenser les interventions sur le monde live.",
            11, StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);

        var routes = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        routes.AddThemeConstantOverride("h_separation", 7);
        root.AddChild(routes);
        routes.AddChild(RouteCardUx9G("ORF-DM-007", "Téléporter",
            "Navigation, consentement, groupe, orientation et aperçu."));
        routes.AddChild(RouteCardUx9G("ORF-DM-008", "Détruire",
            "Cause, confirmation, butin, impact quête et retour borné."));
        routes.AddChild(RouteCardUx9G("ORF-DM-009", "Soigner",
            "Santé, ressources, effets ou tout avec raison."));
        routes.AddChild(RouteCardUx9G("ORF-DM-010", "Limbo",
            "Identité, IA, mouvement, collision et visibilité."));
        routes.AddChild(RouteCardUx9G("ORF-DM-012", "Posséder",
            "Durée, commandes, point de retour, consentement et expiration."));
        root.AddChild(StudioStyle.Label(
            "Limite : sandbox mémoire isolée uniquement · aucune connexion au serveur réel et aucune action dans le module ouvert.",
            10, StudioStyle.Warning));

        _moduleChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _moduleChoice.ItemSelected += index => SelectModuleUx9G((int)index);
        root.AddChild(LabeledRowUx9G("Module explicite", _moduleChoice));
        _focusChoice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _focusChoice.AddItem("Téléportation et destruction");
        _focusChoice.AddItem("Soin et limbo");
        _focusChoice.AddItem("Possession et restitution");
        _focusChoice.ItemSelected += index => SelectFocusUx9G((int)index);
        root.AddChild(LabeledRowUx9G("Workflow affiché", _focusChoice));

        var editor = StudioStyle.Panel("Ux9GEditor", StudioStyle.SurfaceRaised, 9, 1);
        editor.CustomMinimumSize = new Vector2(0, 310);
        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(editor);
        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        stack.AddThemeConstantOverride("separation", 7);
        editor.AddChild(stack);
        stack.AddChild(StudioStyle.Label("Édition directe et sandbox locale", 14,
            StudioStyle.Text));
        BuildRelocationDestructionPageUx9G(stack);
        BuildHealingLimboPageUx9G(stack);
        BuildPossessionPageUx9G(stack);

        var confirmations = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        confirmations.AddThemeConstantOverride("separation", 18);
        root.AddChild(confirmations);
        _approval = CheckUx9G(confirmations,
            "J’approuve ce seul aperçu sec pour la révision affichée");
        _probeConfirmation = CheckUx9G(confirmations,
            "Je confirme la sandbox locale en rôle Administrateur");

        var review = StudioStyle.Panel("Ux9GReview", StudioStyle.SurfaceRaised, 7, 1);
        review.CustomMinimumSize = new Vector2(0, 104);
        root.AddChild(review);
        var reviewStack = new VBoxContainer();
        review.AddChild(reviewStack);
        _previewSummary = StudioStyle.Label(
            "Prévisualisez le profil · aucune action exécutée.", 10, StudioStyle.Muted);
        _previewSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        reviewStack.AddChild(_previewSummary);
        _auditSummary = StudioStyle.Label("Journal local · aucune entrée", 10,
            StudioStyle.Muted);
        _auditSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        reviewStack.AddChild(_auditSummary);
        _status = StudioStyle.Label("Sélectionnez explicitement le module.", 10,
            StudioStyle.Muted);
        root.AddChild(_status);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        root.AddChild(actions);
        _previewButton = ActionButtonUx9G(actions, "Prévisualiser", PreviewCurrentUx9G, 132,
            true);
        _applyButton = ActionButtonUx9G(actions, "Appliquer", ApplyCurrentUx9G, 104);
        _undoButton = ActionButtonUx9G(actions, "Annuler", UndoUx9G, 92);
        _redoButton = ActionButtonUx9G(actions, "Rétablir", RedoUx9G, 92);
        _saveButton = ActionButtonUx9G(actions, "Enregistrer", SaveUx9G, 112);
        _probeButton = ActionButtonUx9G(actions, "Tester localement", ProbeUx9G, 144);
        ConnectInvalidationUx9G();
        SelectFocusUx9G(0);
        UpdateActionsUx9G();
    }

    private void BuildRelocationDestructionPageUx9G(VBoxContainer parent)
    {
        var page = FocusPageUx9G(parent, "Déplacement sûr et destruction réversible en session");
        var grid = GridUx9G(page);
        _partyPolicy = ChoiceUx9G(grid, "Politique du groupe",
            "Cible seule", "Membres consentants", "PNJ du groupe");
        _destructionCause = LineUx9G(grid, "Cause de destruction", "Décision du maître de jeu");
        _lootPolicy = ChoiceUx9G(grid, "Butin", "Conserver", "Supprimer");
        _questImpact = ChoiceUx9G(grid, "Impact quête", "Prévisualisé", "Aucun", "Journalisé");
        _rollbackLimit = ChoiceUx9G(grid, "Limite de retour", "Session uniquement",
            "Point de contrôle", "Aucun retour");
        _playerConsent = CheckUx9G(page, "Consentement du joueur protégé accordé");
        _destructionConfirmed = CheckUx9G(page, "Je confirme la destruction dans la sandbox");
        page.AddChild(StudioStyle.Label(
            "La téléportation conserve l’orientation ; la destruction affiche la limite de restauration avant application.",
            10, StudioStyle.Success));
    }

    private void BuildHealingLimboPageUx9G(VBoxContainer parent)
    {
        var page = FocusPageUx9G(parent, "Soin RPG et limbo sans remplacement d’identité");
        var grid = GridUx9G(page);
        _healingScope = ChoiceUx9G(grid, "Portée du soin", "Tout", "Santé",
            "Ressources", "Effets");
        _healingReason = LineUx9G(grid, "Raison du soin", "Réinitialisation de rencontre");
        _aiState = ChoiceUx9G(grid, "Intelligence artificielle", "Suspendue", "Active");
        _movementPolicy = ChoiceUx9G(grid, "Mouvement", "Bloqué", "Préservé", "Libre");
        _collisionPolicy = ChoiceUx9G(grid, "Collision", "Préservée", "Désactivée", "Activée");
        _visibilityPolicy = ChoiceUx9G(grid, "Visibilité", "Masquée", "Préservée", "Visible");
        page.AddChild(StudioStyle.Label(
            "Le limbo modifie les états moteur de l’objet existant ; son identité, son nom et sa balise restent inchangés.",
            10, StudioStyle.Success));
    }

    private void BuildPossessionPageUx9G(VBoxContainer parent)
    {
        var page = FocusPageUx9G(parent, "Possession bornée avec restitution automatique");
        var profileGrid = GridUx9G(page);
        _maximumPossessionMinutes = SpinUx9G(profileGrid, "Limite du profil (minutes)", 1,
            120, 1);
        _possessionMinutes = SpinUx9G(profileGrid, "Durée de la sandbox (minutes)", 1,
            120, 1);
        _inputContext = ChoiceUx9G(profileGrid, "Contexte de commandes", "Director", "GM",
            "Spectateur");
        _returnPoint = LineUx9G(profileGrid, "Point de retour", "Entrée de la crypte");
        _requiredAtRuntime = CheckUx9G(page,
            "Exiger ce profil au démarrage du runtime réseau");
        _protectedPlayerConsent = CheckUx9G(page,
            "Consentement du joueur protégé accordé");
        page.AddChild(StudioStyle.Label(
            "À l’expiration, le contexte de commandes est libéré et l’objet retrouve son état et son point de retour.",
            10, StudioStyle.Success));
    }

    private VBoxContainer FocusPageUx9G(VBoxContainer parent, string title)
    {
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 7);
        page.AddChild(StudioStyle.Label(title, 12, StudioStyle.Text));
        parent.AddChild(page);
        _focusPages.Add(page);
        return page;
    }

    private void OpenCurrentUx9G()
    {
        Visible = true;
        ResetUx9G();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionner un module");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(
                _session.Project.Module.DisplayName));
        _moduleChoice.Select(0);
        RefreshAuditUx9G();
        UpdateBoundsUx9G();
        UpdateActionsUx9G();
    }

    private void SelectModuleUx9G(int index)
    {
        _explicitSelection = index == 1 && _session.Project is not null;
        _session.CancelLiveActionApprovalUx9G();
        _preview = null;
        _approval.ButtonPressed = false;
        if (!_explicitSelection)
        {
            _draft = null;
            SetStatusUx9G("Sélectionnez explicitement le module.", false);
            UpdateActionsUx9G();
            return;
        }
        _draft = _session.CreateLiveActionDraftUx9G(explicitlySelected: true);
        if (_draft is null)
        {
            SetStatusUx9G(_session.LiveActionLoadMessageUx9G ??
                          "Le profil des actions est indisponible.", true);
            UpdateActionsUx9G();
            return;
        }
        PopulateDraftUx9G(_draft);
        PopulateOptionsUx9G(StudioUx9GProbeOptions.CreateDefault());
        SetStatusUx9G(_session.LiveActionFileMissingUx9G
            ? "Profil initial chargé en mémoire · aucun fichier créé avant Enregistrer."
            : "Actions Live Director chargées · prévisualisez avant toute application.", false);
        UpdateActionsUx9G();
    }

    private void PopulateDraftUx9G(StudioUx9GLiveActionDraft draft)
    {
        _updating = true;
        _maximumPossessionMinutes.Value = draft.MaximumPossessionMinutes;
        _requiredAtRuntime.ButtonPressed = draft.RequiredAtRuntime;
        _updating = false;
    }

    private void PopulateOptionsUx9G(StudioUx9GProbeOptions value)
    {
        _updating = true;
        _partyPolicy.Select(Array.IndexOf(PartyValues, value.PartyPolicy));
        _playerConsent.ButtonPressed = value.PlayerConsent;
        _destructionCause.Text = value.DestructionCause;
        _lootPolicy.Select(Array.IndexOf(LootValues, value.LootPolicy));
        _questImpact.Select(Array.IndexOf(QuestValues, value.QuestImpact));
        _rollbackLimit.Select(Array.IndexOf(RollbackValues, value.RollbackLimit));
        _destructionConfirmed.ButtonPressed = value.DestructionConfirmed;
        _healingScope.Select(Array.IndexOf(HealingValues, value.HealingScope));
        _healingReason.Text = value.HealingReason;
        _aiState.Select(Array.IndexOf(AiValues, value.AiState));
        _movementPolicy.Select(Array.IndexOf(MovementValues, value.MovementPolicy));
        _collisionPolicy.Select(Array.IndexOf(CollisionValues, value.CollisionPolicy));
        _visibilityPolicy.Select(Array.IndexOf(VisibilityValues, value.VisibilityPolicy));
        _possessionMinutes.Value = value.PossessionMinutes;
        _inputContext.Select(Array.IndexOf(InputValues, value.InputContext));
        _returnPoint.Text = value.ReturnPoint;
        _protectedPlayerConsent.ButtonPressed = value.ProtectedPlayerConsent;
        _updating = false;
    }

    private StudioUx9GLiveActionDraft? ReadDraftUx9G() => _draft is null ? null : _draft with
    {
        MaximumPossessionMinutes = (int)_maximumPossessionMinutes.Value,
        RequiredAtRuntime = _requiredAtRuntime.ButtonPressed
    };

    private StudioUx9GProbeOptions? ReadOptionsUx9G()
    {
        if (!_explicitSelection) return null;
        return new(PartyValues[_partyPolicy.Selected], _playerConsent.ButtonPressed,
            _destructionCause.Text.Trim(), LootValues[_lootPolicy.Selected],
            QuestValues[_questImpact.Selected], RollbackValues[_rollbackLimit.Selected],
            _destructionConfirmed.ButtonPressed, HealingValues[_healingScope.Selected],
            _healingReason.Text.Trim(), AiValues[_aiState.Selected],
            MovementValues[_movementPolicy.Selected], CollisionValues[_collisionPolicy.Selected],
            VisibilityValues[_visibilityPolicy.Selected], (int)_possessionMinutes.Value,
            InputValues[_inputContext.Selected], _returnPoint.Text.Trim(),
            _protectedPlayerConsent.ButtonPressed);
    }

    private void PreviewCurrentUx9G()
    {
        var draft = ReadDraftUx9G();
        var options = ReadOptionsUx9G();
        if (draft is null || options is null)
        {
            SetStatusUx9G("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewLiveActionUx9G(draft, options);
        PopulatePreviewUx9G();
        SetStatusUx9G(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx9G();
    }

    private void ApplyCurrentUx9G()
    {
        var draft = ReadDraftUx9G();
        if (draft is null) return;
        var focus = _focusChoice.Selected;
        var result = _session.ApplyApprovedLiveActionUx9G(draft);
        SetStatusUx9G(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            SelectModuleUx9G(1);
            SelectFocusUx9G(focus);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9G();
    }

    private void UndoUx9G()
    {
        var result = _session.UndoLiveActionUx9G();
        SetStatusUx9G(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            SelectModuleUx9G(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9G();
    }

    private void RedoUx9G()
    {
        var result = _session.RedoLiveActionUx9G();
        SetStatusUx9G(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            SelectModuleUx9G(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9G();
    }

    private void SaveUx9G()
    {
        var result = _session.SaveLiveActionUx9G();
        SetStatusUx9G(result.Message, !result.Succeeded);
        UpdateActionsUx9G();
    }

    private void ProbeUx9G()
    {
        var options = ReadOptionsUx9G();
        if (options is null) return;
        var result = _session.RunLocalLiveActionUx9G(_probeConfirmation.ButtonPressed,
            NetworkRole.Administrator, options);
        SetStatusUx9G(result.Message, !result.Succeeded);
        RefreshAuditUx9G();
        PopulatePreviewUx9G();
        UpdateActionsUx9G();
    }

    private void PopulatePreviewUx9G()
    {
        var probe = _session.LiveActionProbeUx9G;
        _previewSummary.Text = _preview is null
            ? $"Sandbox · {probe.Status}."
            : $"{_preview.Message} · {string.Join(" · ", _preview.SemanticDiff)}\n" +
              $"Téléportation {HumanCodeUx9G(probe.RelocationCode)}, destruction {HumanCodeUx9G(probe.DestructionCode)}, " +
              $"soin {HumanCodeUx9G(probe.HealingCode)}, limbo {HumanCodeUx9G(probe.LimboCode)}, possession {HumanCodeUx9G(probe.PossessionCode)}.";
    }

    private void RefreshAuditUx9G()
    {
        var audit = _session.LiveActionAuditUx9G;
        var probe = _session.LiveActionProbeUx9G;
        _auditSummary.Text = audit.Count == 0
            ? "Journal local · aucune entrée"
            : $"Journal local · {audit.Count} entrée(s) · compensation {HumanCodeUx9G(probe.RelocationCompensationCode)} · restitution {(probe.ReturnPointRestored ? "validée" : "en attente")}.";
    }

    private void ConfigureEvidenceUx9G(string mode)
    {
        _maximumPossessionMinutes.Value = 45;
        _requiredAtRuntime.ButtonPressed = true;
        _approval.ButtonPressed = true;
        _probeConfirmation.ButtonPressed = true;
        PopulateOptionsUx9G(StudioUx9GProbeOptions.CreateDefault());
        var focus = mode.Contains("heal", StringComparison.OrdinalIgnoreCase) ||
                    mode.Contains("limbo", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("possess", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("return", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        _focusChoice.Select(focus);
        SelectFocusUx9G(focus);
        InvalidateUx9G(cancelApproval: false, preserveApproval: true);
    }

    private void SelectFocusUx9G(int index)
    {
        for (var i = 0; i < _focusPages.Count; i++) _focusPages[i].Visible = i == index;
    }

    private void ConnectInvalidationUx9G()
    {
        foreach (var spin in new[] { _maximumPossessionMinutes, _possessionMinutes })
            spin.ValueChanged += _ => InvalidateUx9G();
        foreach (var line in new[] { _destructionCause, _healingReason, _returnPoint })
            line.TextChanged += _ => InvalidateUx9G();
        foreach (var option in new[] { _partyPolicy, _lootPolicy, _questImpact, _rollbackLimit,
                     _healingScope, _aiState, _movementPolicy, _collisionPolicy,
                     _visibilityPolicy, _inputContext })
            option.ItemSelected += _ => InvalidateUx9G();
        foreach (var check in new[] { _playerConsent, _destructionConfirmed,
                     _requiredAtRuntime, _protectedPlayerConsent })
            check.Toggled += _ => InvalidateUx9G();
        _approval.Toggled += _ => UpdateActionsUx9G();
        _probeConfirmation.Toggled += _ => UpdateActionsUx9G();
    }

    private void InvalidateUx9G(bool cancelApproval = true, bool preserveApproval = false)
    {
        if (_updating) return;
        _preview = null;
        if (cancelApproval) _session.CancelLiveActionApprovalUx9G();
        if (!preserveApproval) _approval.ButtonPressed = false;
        PopulatePreviewUx9G();
        UpdateActionsUx9G();
    }

    private void UpdateActionsUx9G()
    {
        var selected = _explicitSelection && _draft is not null;
        _previewButton.Disabled = !selected;
        _applyButton.Disabled = !selected || _preview is not { CanApply: true } ||
                                !_approval.ButtonPressed;
        _undoButton.Disabled = !_canUndo;
        _redoButton.Disabled = !_canRedo;
        _saveButton.Disabled = !_session.LiveActionHasUnsavedChangesUx9G;
        _probeButton.Disabled = !selected || !_probeConfirmation.ButtonPressed ||
                                _session.LiveActionHasUnsavedChangesUx9G;
    }

    private void ResetUx9G()
    {
        _draft = null;
        _preview = null;
        _explicitSelection = false;
        _canUndo = false;
        _canRedo = false;
        if (_previewSummary is not null) PopulatePreviewUx9G();
    }

    private SourceSnapshotUx9G CaptureSourceUx9G(StudioSessionController session) => new(
        session.Project, session.History, session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [], session.IsDirty, session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(), session.SelectedEntityId,
        session.CaptureLiveActionApprovalUx9G());

    private bool SourceUnchangedUx9G()
    {
        var source = _sourceSession;
        var snapshot = _sourceSnapshot;
        if (source is null || snapshot is null) return _evidenceSession is null;
        return ReferenceEquals(source.Project, snapshot.Project) &&
               ReferenceEquals(source.History, snapshot.History) &&
               source.History?.State.Revision == snapshot.Revision &&
               (source.History?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               source.IsDirty == snapshot.Dirty && source.ActiveAreaId == snapshot.Area &&
               source.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               source.SelectedEntityId == snapshot.Selected &&
               source.CaptureLiveActionApprovalUx9G() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx9G()
    {
        if (_sourceSession is not null) _session = _sourceSession;
        _sourceSession = null;
        _sourceSnapshot = null;
        _sourceSessionRestored = true;
    }

    private void DeleteEvidenceUx9G()
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-g-ui-",
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
            SetStatusUx9G($"La copie temporaire n’a pas pu être libérée : {exception.Message}",
                true);
        }
    }

    private void UpdateBoundsUx9G()
    {
        if (_dialog is null) return;
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(960, size.X - 48), Math.Max(700, size.Y - 36));
    }

    private void SetStatusUx9G(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger :
            StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private static Control RouteCardUx9G(string route, string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux9G" + route.Replace("-", string.Empty),
            StudioStyle.SurfaceRaised, 6, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var stack = new VBoxContainer();
        panel.AddChild(stack);
        stack.AddChild(StudioStyle.Label($"{route} · {title}", 10, StudioStyle.Text));
        var label = StudioStyle.Label(detail, 9, StudioStyle.Muted);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        stack.AddChild(label);
        return panel;
    }

    private static Control LabeledRowUx9G(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(title);
        row.AddChild(control);
        return row;
    }

    private static GridContainer GridUx9G(Control parent)
    {
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 7);
        parent.AddChild(grid);
        return grid;
    }

    private static SpinBox SpinUx9G(GridContainer parent, string label, double min, double max,
        double step)
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
            CustomMinimumSize = new Vector2(260, 34)
        };
        parent.AddChild(value);
        return value;
    }

    private static LineEdit LineUx9G(GridContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new LineEdit
        {
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(260, 34)
        };
        parent.AddChild(value);
        return value;
    }

    private static OptionButton ChoiceUx9G(GridContainer parent, string label,
        params string[] choices)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(260, 34)
        };
        foreach (var choice in choices) value.AddItem(choice);
        parent.AddChild(value);
        return value;
    }

    private static CheckBox CheckUx9G(Control parent, string text)
    {
        var check = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(check);
        return check;
    }

    private static Button ActionButtonUx9G(HBoxContainer parent, string text, Action action,
        float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static IEnumerable<string> DescendantTextUx9G(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is CheckBox check) yield return check.Text;
        if (node is LineEdit line) yield return line.Text;
        if (node is OptionButton option)
            for (var index = 0; index < option.ItemCount; index++)
                yield return option.GetItemText(index);
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx9G(child))
                yield return value;
    }

    private static bool ContainsUuidUx9G(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);

    private static string HumanCodeUx9G(string code) => code switch
    {
        "LIVE_ACTION_APPLIED" => "appliquée",
        "LIVE_ACTION_PREVIEW" => "prévisualisée",
        "LIVE_ACTION_COMPENSATED" => "compensée",
        "LIVE_ACTION_COMPENSATION_NOT_REQUIRED" => "non requise",
        "LIVE_CONTROL_APPLIED" => "appliquée puis expirée",
        "LIVE_CONTROL_PREVIEW" => "prévisualisée",
        _ => code
    };

    private static readonly string[] PartyValues = ["target-only", "party-consenting", "party-all-npcs"];
    private static readonly string[] LootValues = ["preserve", "discard"];
    private static readonly string[] QuestValues = ["previewed", "none", "recorded"];
    private static readonly string[] RollbackValues = ["session-only", "checkpoint-only", "none"];
    private static readonly string[] HealingValues = ["all", "health", "resources", "effects"];
    private static readonly string[] AiValues = ["suspended", "active"];
    private static readonly string[] MovementValues = ["blocked", "preserved", "free"];
    private static readonly string[] CollisionValues = ["preserved", "disabled", "enabled"];
    private static readonly string[] VisibilityValues = ["hidden", "preserved", "visible"];
    private static readonly string[] InputValues = ["director", "gm", "spectator"];

    private sealed record SourceSnapshotUx9G(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool Dirty,
        Forge.Domain.Areas.AreaId? Area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? Selected,
        StudioUx9GApprovalSnapshot Approval);
}
