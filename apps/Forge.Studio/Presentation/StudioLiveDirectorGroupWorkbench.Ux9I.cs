using System.Text.RegularExpressions;
using Forge.LiveDirector.Authoring;
using Forge.ModuleFormat.LiveDirector;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Operations;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioLiveDirectorGroupWorkbenchUx9I : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx9I? _sourceSnapshot;
    private StudioUx9IGroupDraft? _draft;
    private StudioUx9IGroupPreview? _preview;
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
    private SpinBox _selectionCap = null!;
    private CheckBox _requiredAtRuntime = null!;
    private OptionButton _triggerEvent = null!;
    private LineEdit _triggerPayload = null!;
    private OptionButton _movementFormation = null!;
    private CheckBox _factionWarning = null!;
    private OptionButton _travelMode = null!;
    private CheckBox _travelConfirmation = null!;
    private CheckBox _partyConsent = null!;
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
        TriggerRuntime: true, SelectionRuntime: true, MovementRuntime: true,
        AttackRuntime: true, TravelRuntime: true
    };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx9I();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx9I(this));
            return Visible && text.Contains("ORF-DM-020", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-021", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-023", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-024", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-026", StringComparison.Ordinal) &&
                   text.Contains("lasso", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("échec partiel", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("sécurité de faction", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("transition caméra", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tester localement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun trigger, groupe ou joueur réel",
                       StringComparison.OrdinalIgnoreCase) && !ContainsUuidUx9I(text);
        }
    }

    internal StudioLiveDirectorGroupWorkbenchUx9I(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx9I();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9I();
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
        _session.CancelLiveGroupApprovalUx9I();
        ResetUx9I();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx9I(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-i-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        var baseline = LiveDirectorGroupProfile.CreateDefault("evidence:ux9-i-ui");
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !WorkspaceLiveDirectorGroupStore.Save(_evidenceRoot, baseline).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx9I();
            RestoreSourceSessionUx9I();
            return false;
        }
        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx9I();
        _moduleChoice.Select(1);
        SelectModuleUx9I(1);
        ConfigureEvidenceUx9I(mode);
        PreviewCurrentUx9I();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady &&
                    ShowsHumanSurface && EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_I_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
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
        var draft = ReadDraftUx9I();
        var options = ReadOptionsUx9I();
        if (draft is null || options is null) return false;
        var preview = _session.PreviewLiveGroupUx9I(draft, options);
        var applied = preview.CanApply && _approval.ButtonPressed &&
                      _session.ApplyApprovedLiveGroupUx9I(draft).Succeeded;
        var undo = applied && _session.UndoLiveGroupUx9I().Succeeded;
        var redo = undo && _session.RedoLiveGroupUx9I().Succeeded;
        var save = redo && _session.SaveLiveGroupUx9I().Succeeded;
        var probed = save && _probeConfirmation.ButtonPressed &&
                     _session.RunLocalLiveGroupUx9I(true, NetworkRole.Administrator,
                         options).Succeeded;
        var expected = _session.CurrentLiveGroupUx9I;
        var reopened = new StudioSessionController();
        var reopenedOk = probed && expected is not null && reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.CurrentLiveGroupUx9I is { } actual &&
                         new LiveDirectorGroupStore().Serialize(expected) ==
                         new LiveDirectorGroupStore().Serialize(actual);
        reopened.Close();
        RefreshAuditUx9I();
        return preview.CanonicalDryRun && applied && undo && redo && save && probed &&
               reopenedOk && _session.LiveGroupProbeUx9I.ResourcesReleased &&
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
        ResetUx9I();
        DeleteEvidenceUx9I();
        RestoreSourceSessionUx9I();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9I();
        _moduleChoice.Select(1);
        SelectModuleUx9I(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx9I();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx9I;
        UpdateBoundsUx9I();
    }

    public override void _ExitTree() => CloseEvidence();

    private void BuildUiUx9I()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color("10121af2");
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _dialog = StudioStyle.Panel("Ux9ILiveGroup", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var root = new VBoxContainer
        {
            Name = "Ux9IRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 6);
        _dialog.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(headingText);
        headingText.AddChild(StudioStyle.Label("Live Director · groupes et voyage joueur", 20,
            StudioStyle.Text));
        headingText.AddChild(StudioStyle.Label(
            "Simuler, sélectionner, commander puis restaurer chaque intervention locale.", 11,
            StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);

        var routes = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        routes.AddThemeConstantOverride("h_separation", 7);
        root.AddChild(routes);
        routes.AddChild(RouteCardUx9I("ORF-DM-020", "Simuler un trigger",
            "Entrée/sortie avec acteur, payload et trace ForgeScript."));
        routes.AddChild(RouteCardUx9I("ORF-DM-021", "Sélection multiple",
            "Lasso permissionné, plafond et identifiant stable."));
        routes.AddChild(RouteCardUx9I("ORF-DM-023", "Déplacer un groupe",
            "Formation, aperçu de chemin et rapport d’échec partiel."));
        routes.AddChild(RouteCardUx9I("ORF-DM-024", "Ordonner une attaque",
            "Sécurité de faction, objectif et adaptateur combat."));
        routes.AddChild(RouteCardUx9I("ORF-DM-026", "Voyager vers un joueur",
            "Quatre modes, confirmation, consentement et transition caméra."));

        _moduleChoice = ChoiceUx9I(null, "Sélection", "Sélectionner un module");
        _moduleChoice.ItemSelected += index => SelectModuleUx9I((int)index);
        root.AddChild(LabeledRowUx9I("Module explicitement ciblé", _moduleChoice));
        _focusChoice = ChoiceUx9I(null, "Workflow", "Trigger et sélection",
            "Déplacement et attaque", "Voyage vers un joueur");
        _focusChoice.ItemSelected += index => SelectFocusUx9I((int)index);
        root.AddChild(LabeledRowUx9I("Workflow direct", _focusChoice));

        var editor = StudioStyle.Panel("Ux9IEditor", StudioStyle.SurfaceRaised, 9, 1);
        editor.CustomMinimumSize = new Vector2(0, 270);
        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(editor);
        var stack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        stack.AddThemeConstantOverride("separation", 7);
        editor.AddChild(stack);
        BuildTriggerSelectionPageUx9I(stack);
        BuildMovementAttackPageUx9I(stack);
        BuildTravelPageUx9I(stack);

        var decisions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        decisions.AddThemeConstantOverride("separation", 18);
        root.AddChild(decisions);
        _approval = CheckUx9I(decisions,
            "J’approuve ce brouillon et cette révision après l’aperçu sec");
        _probeConfirmation = CheckUx9I(decisions,
            "Je confirme la sandbox locale restaurée en fin d’exécution");

        var review = StudioStyle.Panel("Ux9IReview", StudioStyle.SurfaceRaised, 7, 1);
        review.CustomMinimumSize = new Vector2(0, 102);
        root.AddChild(review);
        var reviewStack = new VBoxContainer();
        review.AddChild(reviewStack);
        _previewSummary = StudioStyle.Label("Aucun aperçu sec.", 10, StudioStyle.Text,
            wrap: true);
        reviewStack.AddChild(_previewSummary);
        _auditSummary = StudioStyle.Label("Journal local · aucune entrée", 10,
            StudioStyle.Muted, wrap: true);
        reviewStack.AddChild(_auditSummary);
        _status = StudioStyle.Label(
            "Sélectionnez explicitement le module avant de commander un groupe.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "Sandbox isolée : aucun trigger, groupe ou joueur réel, aucun serveur et aucun déplacement externe ne sont modifiés.",
            10, StudioStyle.Warning, wrap: true));

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 7);
        root.AddChild(actions);
        _previewButton = ActionButtonUx9I(actions, "Prévisualiser", PreviewCurrentUx9I, 122,
            primary: true);
        _applyButton = ActionButtonUx9I(actions, "Appliquer", ApplyCurrentUx9I, 104);
        _undoButton = ActionButtonUx9I(actions, "Annuler", UndoUx9I, 92);
        _redoButton = ActionButtonUx9I(actions, "Rétablir", RedoUx9I, 92);
        _saveButton = ActionButtonUx9I(actions, "Enregistrer", SaveUx9I, 112);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _probeButton = ActionButtonUx9I(actions, "Tester localement", ProbeUx9I, 148);

        ConnectInvalidationUx9I();
        SelectFocusUx9I(0);
        UpdateActionsUx9I();
    }

    private void BuildTriggerSelectionPageUx9I(VBoxContainer parent)
    {
        var page = FocusPageUx9I(parent,
            "Trigger et sélection · simulation traçable puis lasso permissionné");
        var grid = GridUx9I(page);
        _selectionCap = SpinUx9I(grid, "Plafond de sélection", 1, 1024, 1);
        _triggerEvent = ChoiceUx9I(grid, "Événement du trigger", "Entrée", "Sortie");
        _triggerPayload = LineUx9I(grid, "Payload lisible", "quête:cendres");
        _requiredAtRuntime = CheckUx9I(page,
            "Exiger ce profil au démarrage du runtime Live Director");
        page.AddChild(StudioStyle.Label(
            "Acteur simulé : Garde mobile · sélection Garde par lasso avec identifiant stable et filtrage des permissions.",
            10, StudioStyle.Success, wrap: true));
    }

    private void BuildMovementAttackPageUx9I(VBoxContainer parent)
    {
        var page = FocusPageUx9I(parent,
            "Déplacement et attaque · chemin, échec partiel et objectif autoritaire");
        var grid = GridUx9I(page);
        _movementFormation = ChoiceUx9I(grid, "Formation", "Ligne", "Coin", "Cercle",
            "Libre");
        _factionWarning = CheckUx9I(page,
            "J’accepte l’avertissement de sécurité de faction avant l’ordre d’attaque");
        page.AddChild(StudioStyle.Label(
            "Le pathfinding prévisualise un membre mobile et un membre bloqué ; l’adaptateur combat assigne ensuite l’objectif synthétique.",
            10, StudioStyle.Success, wrap: true));
    }

    private void BuildTravelPageUx9I(VBoxContainer parent)
    {
        var page = FocusPageUx9I(parent,
            "Voyage joueur · observer, rejoindre ou rappeler avec transition caméra");
        var grid = GridUx9I(page);
        _travelMode = ChoiceUx9I(grid, "Mode de voyage", "Observer", "Téléporter le MJ",
            "Faire venir le joueur", "Faire venir le groupe");
        _travelConfirmation = CheckUx9I(page,
            "Je confirme le voyage ou le rappel dans la sandbox");
        _partyConsent = CheckUx9I(page,
            "Le groupe synthétique a consenti au rappel collectif");
        page.AddChild(StudioStyle.Label(
            "La transition caméra est annoncée dans l’aperçu ; chaque position synthétique est restaurée après le test.",
            10, StudioStyle.Success, wrap: true));
    }

    private VBoxContainer FocusPageUx9I(VBoxContainer parent, string title)
    {
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 7);
        page.AddChild(StudioStyle.Label(title, 12, StudioStyle.Text));
        parent.AddChild(page);
        _focusPages.Add(page);
        return page;
    }

    private void OpenCurrentUx9I()
    {
        Visible = true;
        ResetUx9I();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionner un module");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(
                _session.Project.Module.DisplayName));
        _moduleChoice.Select(0);
        RefreshAuditUx9I();
        UpdateBoundsUx9I();
        UpdateActionsUx9I();
    }

    private void SelectModuleUx9I(int index)
    {
        _explicitSelection = index == 1 && _session.Project is not null;
        _session.CancelLiveGroupApprovalUx9I();
        _preview = null;
        _approval.ButtonPressed = false;
        if (!_explicitSelection)
        {
            _draft = null;
            SetStatusUx9I("Sélectionnez explicitement le module.", false);
            UpdateActionsUx9I();
            return;
        }
        _draft = _session.CreateLiveGroupDraftUx9I(explicitlySelected: true);
        if (_draft is null)
        {
            SetStatusUx9I(_session.LiveGroupLoadMessageUx9I ??
                          "Le profil de groupe est indisponible.", true);
            UpdateActionsUx9I();
            return;
        }
        PopulateDraftUx9I(_draft);
        PopulateOptionsUx9I(StudioUx9IGroupProbeOptions.CreateDefault());
        SetStatusUx9I(_session.LiveGroupFileMissingUx9I
            ? "Profil initial chargé en mémoire · aucun fichier créé avant Enregistrer."
            : "Profil de groupe chargé · prévisualisez avant toute application.", false);
        UpdateActionsUx9I();
    }

    private void PopulateDraftUx9I(StudioUx9IGroupDraft draft)
    {
        _updating = true;
        _selectionCap.Value = draft.SelectionCap;
        _requiredAtRuntime.ButtonPressed = draft.RequiredAtRuntime;
        _updating = false;
    }

    private void PopulateOptionsUx9I(StudioUx9IGroupProbeOptions value)
    {
        _updating = true;
        _triggerEvent.Select(Array.IndexOf(TriggerValues, value.TriggerEvent));
        _triggerPayload.Text = value.TriggerPayload;
        _movementFormation.Select(Array.IndexOf(FormationValues, value.MovementFormation));
        _factionWarning.ButtonPressed = value.FactionWarningAccepted;
        _travelMode.Select(Array.IndexOf(TravelValues, value.PlayerTravelMode));
        _travelConfirmation.ButtonPressed = value.PlayerTravelConfirmed;
        _partyConsent.ButtonPressed = value.PartyConsentConfirmed;
        _updating = false;
    }

    private StudioUx9IGroupDraft? ReadDraftUx9I() => _draft is null ? null : _draft with
    {
        SelectionCap = (int)_selectionCap.Value,
        RequiredAtRuntime = _requiredAtRuntime.ButtonPressed
    };

    private StudioUx9IGroupProbeOptions? ReadOptionsUx9I()
    {
        if (!_explicitSelection) return null;
        return new(TriggerValues[_triggerEvent.Selected], _triggerPayload.Text.Trim(),
            FormationValues[_movementFormation.Selected], _factionWarning.ButtonPressed,
            TravelValues[_travelMode.Selected], _travelConfirmation.ButtonPressed,
            _partyConsent.ButtonPressed);
    }

    private void PreviewCurrentUx9I()
    {
        var draft = ReadDraftUx9I();
        var options = ReadOptionsUx9I();
        if (draft is null || options is null)
        {
            SetStatusUx9I("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewLiveGroupUx9I(draft, options);
        PopulatePreviewUx9I();
        SetStatusUx9I(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx9I();
    }

    private void ApplyCurrentUx9I()
    {
        var draft = ReadDraftUx9I();
        if (draft is null) return;
        var focus = _focusChoice.Selected;
        var result = _session.ApplyApprovedLiveGroupUx9I(draft);
        SetStatusUx9I(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            SelectModuleUx9I(1);
            SelectFocusUx9I(focus);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9I();
    }

    private void UndoUx9I()
    {
        var result = _session.UndoLiveGroupUx9I();
        SetStatusUx9I(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            SelectModuleUx9I(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9I();
    }

    private void RedoUx9I()
    {
        var result = _session.RedoLiveGroupUx9I();
        SetStatusUx9I(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            SelectModuleUx9I(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9I();
    }

    private void SaveUx9I()
    {
        var result = _session.SaveLiveGroupUx9I();
        SetStatusUx9I(result.Message, !result.Succeeded);
        UpdateActionsUx9I();
    }

    private void ProbeUx9I()
    {
        var options = ReadOptionsUx9I();
        if (options is null) return;
        var result = _session.RunLocalLiveGroupUx9I(_probeConfirmation.ButtonPressed,
            NetworkRole.Administrator, options);
        SetStatusUx9I(result.Message, !result.Succeeded);
        RefreshAuditUx9I();
        PopulatePreviewUx9I();
        UpdateActionsUx9I();
    }

    private void PopulatePreviewUx9I()
    {
        var probe = _session.LiveGroupProbeUx9I;
        _previewSummary.Text = _preview is null
            ? $"Sandbox · {probe.Status}."
            : $"{_preview.Message} · {string.Join(" · ", _preview.SemanticDiff)}\n" +
              $"Trigger {HumanCodeUx9I(probe.TriggerCode)}, sélection {probe.SelectionCount}, " +
              $"déplacement {HumanCodeUx9I(probe.MovementCode)} ({probe.PartialFailures} échec), " +
              $"attaque {HumanCodeUx9I(probe.AttackCode)}, voyage {HumanCodeUx9I(probe.TravelCode)} · {probe.CameraTransition}.";
    }

    private void RefreshAuditUx9I()
    {
        var audit = _session.LiveGroupAuditUx9I;
        var probe = _session.LiveGroupProbeUx9I;
        _auditSummary.Text = audit.Count == 0
            ? "Journal local · aucune entrée"
            : $"Journal local · {audit.Count} entrée(s) · groupe {(probe.Compensated ? "restauré" : "actif")} · " +
              $"ressources {(probe.ResourcesReleased ? "libérées" : "à vérifier")}.";
    }

    private void ConfigureEvidenceUx9I(string mode)
    {
        _updating = true;
        _selectionCap.Value = 96;
        _requiredAtRuntime.ButtonPressed = true;
        PopulateOptionsUx9I(StudioUx9IGroupProbeOptions.CreateDefault());
        _approval.ButtonPressed = true;
        _probeConfirmation.ButtonPressed = true;
        _updating = false;
        var focus = mode.Contains("move", StringComparison.OrdinalIgnoreCase) ||
                    mode.Contains("movement", StringComparison.OrdinalIgnoreCase) ||
                    mode.Contains("attack", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("travel", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("player", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        _focusChoice.Select(focus);
        SelectFocusUx9I(focus);
    }

    private void SelectFocusUx9I(int index)
    {
        for (var i = 0; i < _focusPages.Count; i++) _focusPages[i].Visible = i == index;
    }

    private void ConnectInvalidationUx9I()
    {
        _selectionCap.ValueChanged += _ => InvalidateUx9I();
        _triggerPayload.TextChanged += _ => InvalidateUx9I();
        foreach (var option in new[] { _triggerEvent, _movementFormation, _travelMode })
            option.ItemSelected += _ => InvalidateUx9I();
        foreach (var check in new[] { _requiredAtRuntime, _factionWarning,
                     _travelConfirmation, _partyConsent })
            check.Toggled += _ => InvalidateUx9I();
        _approval.Toggled += _ => UpdateActionsUx9I();
        _probeConfirmation.Toggled += _ => UpdateActionsUx9I();
    }

    private void InvalidateUx9I()
    {
        if (_updating) return;
        _preview = null;
        _session.CancelLiveGroupApprovalUx9I();
        _approval.ButtonPressed = false;
        PopulatePreviewUx9I();
        UpdateActionsUx9I();
    }

    private void UpdateActionsUx9I()
    {
        var selected = _explicitSelection && _draft is not null;
        _previewButton.Disabled = !selected;
        _applyButton.Disabled = !selected || _preview is not { CanApply: true } ||
                                !_approval.ButtonPressed;
        _undoButton.Disabled = !_canUndo;
        _redoButton.Disabled = !_canRedo;
        _saveButton.Disabled = !_session.LiveGroupHasUnsavedChangesUx9I;
        _probeButton.Disabled = !selected || !_probeConfirmation.ButtonPressed ||
                                _session.LiveGroupHasUnsavedChangesUx9I;
    }

    private void ResetUx9I()
    {
        _draft = null;
        _preview = null;
        _explicitSelection = false;
        _canUndo = false;
        _canRedo = false;
        if (_previewSummary is not null) PopulatePreviewUx9I();
    }

    private SourceSnapshotUx9I CaptureSourceUx9I(StudioSessionController session) => new(
        session.Project, session.History, session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [], session.IsDirty, session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(), session.SelectedEntityId,
        session.CaptureLiveGroupApprovalUx9I());

    private bool SourceUnchangedUx9I()
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
               source.CaptureLiveGroupApprovalUx9I() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx9I()
    {
        if (_sourceSession is not null) _session = _sourceSession;
        _sourceSession = null;
        _sourceSnapshot = null;
        _sourceSessionRestored = true;
    }

    private void DeleteEvidenceUx9I()
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-i-ui-",
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
            SetStatusUx9I($"La copie temporaire n’a pas pu être libérée : {exception.Message}",
                true);
        }
    }

    private void UpdateBoundsUx9I()
    {
        if (_dialog is null) return;
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(960, size.X - 48), Math.Max(700, size.Y - 36));
    }

    private void SetStatusUx9I(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger :
            StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private static Control RouteCardUx9I(string route, string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux9I" + route.Replace("-", string.Empty),
            StudioStyle.SurfaceRaised, 6, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var stack = new VBoxContainer();
        panel.AddChild(stack);
        stack.AddChild(StudioStyle.Label($"{route} · {title}", 10, StudioStyle.Text,
            wrap: true));
        stack.AddChild(StudioStyle.Label(detail, 9, StudioStyle.Muted, wrap: true));
        return panel;
    }

    private static Control LabeledRowUx9I(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(title);
        row.AddChild(control);
        return row;
    }

    private static GridContainer GridUx9I(Control parent)
    {
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 7);
        parent.AddChild(grid);
        return grid;
    }

    private static SpinBox SpinUx9I(GridContainer parent, string label, double min, double max,
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

    private static LineEdit LineUx9I(GridContainer parent, string label, string placeholder)
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

    private static OptionButton ChoiceUx9I(GridContainer? parent, string label,
        params string[] choices)
    {
        parent?.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(260, 34)
        };
        foreach (var choice in choices) value.AddItem(choice);
        parent?.AddChild(value);
        return value;
    }

    private static CheckBox CheckUx9I(Control parent, string text)
    {
        var check = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(check);
        return check;
    }

    private static Button ActionButtonUx9I(HBoxContainer parent, string text, Action action,
        float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static IEnumerable<string> DescendantTextUx9I(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is CheckBox check) yield return check.Text;
        if (node is LineEdit line) yield return line.Text;
        if (node is OptionButton option)
            for (var index = 0; index < option.ItemCount; index++)
                yield return option.GetItemText(index);
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx9I(child)) yield return value;
    }

    private static bool ContainsUuidUx9I(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);

    private static string HumanCodeUx9I(string code) => code switch
    {
        "LIVE_RULE_APPLIED" => "appliqué",
        "LIVE_RULE_PREVIEW" => "prévisualisé",
        "LIVE_MOVEMENT_PARTIAL" => "appliqué partiellement",
        "LIVE_MOVEMENT_PREVIEW" => "prévisualisé",
        "LIVE_ATTACK_APPLIED" => "appliquée",
        "LIVE_ATTACK_PREVIEW" => "prévisualisée",
        "LIVE_PLAYER_TRAVEL_APPLIED" => "appliqué puis restauré",
        "LIVE_PLAYER_TRAVEL_PREVIEW" => "prévisualisé",
        _ => code
    };

    private static readonly string[] TriggerValues = ["enter", "exit"];
    private static readonly string[] FormationValues = ["line", "wedge", "circle", "free"];
    private static readonly string[] TravelValues =
        ["spectate", "teleport-gm", "summon-player", "summon-party"];

    private sealed record SourceSnapshotUx9I(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool Dirty,
        Forge.Domain.Areas.AreaId? Area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? Selected,
        StudioUx9IApprovalSnapshot Approval);
}
