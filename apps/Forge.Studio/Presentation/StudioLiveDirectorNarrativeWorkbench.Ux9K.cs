using System.Text.RegularExpressions;
using Forge.LiveDirector.Authoring;
using Forge.ModuleFormat.LiveDirector;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Operations;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioLiveDirectorNarrativeWorkbenchUx9K : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx9K? _sourceSnapshot;
    private StudioUx9KNarrativeDraft? _draft;
    private StudioUx9KNarrativePreview? _preview;
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
    private CheckBox _requiredAtRuntime = null!;
    private OptionButton _questScope = null!;
    private OptionButton _questTargetState = null!;
    private OptionButton _encounterAction = null!;
    private LineEdit _encounterValue = null!;
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
        QuestRuntime: true, EncounterRuntime: true
    };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx9K();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx9K(this));
            return Visible && text.Contains("ORF-DM-035", StringComparison.Ordinal) &&
                   text.Contains("ORF-DM-036", StringComparison.Ordinal) &&
                   text.Contains("conséquences", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("adaptateur RPG", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("cinq commandes", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("spawn et IA", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tester localement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucune quête, rencontre ou partie réelle",
                       StringComparison.OrdinalIgnoreCase) && !ContainsUuidUx9K(text);
        }
    }

    internal StudioLiveDirectorNarrativeWorkbenchUx9K(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx9K();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9K();
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
        _session.CancelLiveNarrativeApprovalUx9K();
        ResetUx9K();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx9K(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-k-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        var baseline = LiveDirectorNarrativeProfile.CreateDefault("evidence:ux9-k-ui");
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !WorkspaceLiveDirectorNarrativeStore.Save(_evidenceRoot, baseline).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx9K();
            RestoreSourceSessionUx9K();
            return false;
        }
        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx9K();
        _moduleChoice.Select(1);
        SelectModuleUx9K(1);
        ConfigureEvidenceUx9K(mode);
        PreviewCurrentUx9K();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady &&
                    ShowsHumanSurface && EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_K_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
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
        var draft = ReadDraftUx9K();
        var options = ReadOptionsUx9K();
        if (draft is null || options is null) return false;
        var preview = _session.PreviewLiveNarrativeUx9K(draft, options);
        var applied = preview.CanApply && _approval.ButtonPressed &&
                      _session.ApplyApprovedLiveNarrativeUx9K(draft).Succeeded;
        var undo = applied && _session.UndoLiveNarrativeUx9K().Succeeded;
        var redo = undo && _session.RedoLiveNarrativeUx9K().Succeeded;
        var save = redo && _session.SaveLiveNarrativeUx9K().Succeeded;
        var probed = save && _probeConfirmation.ButtonPressed &&
                     _session.RunLocalLiveNarrativeUx9K(true, NetworkRole.Administrator,
                         options).Succeeded;
        var expected = _session.CurrentLiveNarrativeUx9K;
        var reopened = new StudioSessionController();
        var reopenedOk = probed && expected is not null && reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.CurrentLiveNarrativeUx9K is { } actual &&
                         new LiveDirectorNarrativeStore().Serialize(expected) ==
                         new LiveDirectorNarrativeStore().Serialize(actual);
        reopened.Close();
        RefreshAuditUx9K();
        return preview.CanonicalDryRun && applied && undo && redo && save && probed &&
               reopenedOk && _session.LiveNarrativeProbeUx9K.ResourcesReleased &&
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
        ResetUx9K();
        DeleteEvidenceUx9K();
        RestoreSourceSessionUx9K();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9K();
        _moduleChoice.Select(1);
        SelectModuleUx9K(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx9K();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx9K;
        UpdateBoundsUx9K();
    }

    public override void _ExitTree() => CloseEvidence();

    private void BuildUiUx9K()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color("10121af2");
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _dialog = StudioStyle.Panel("Ux9KLiveNarrative", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var root = new VBoxContainer
        {
            Name = "Ux9KRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 7);
        _dialog.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(headingText);
        headingText.AddChild(StudioStyle.Label("Live Director · narration en direct", 20,
            StudioStyle.Text));
        headingText.AddChild(StudioStyle.Label(
            "Inspecter, prévisualiser, commander puis restaurer une sandbox narrative.", 11,
            StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);

        var routes = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        routes.AddThemeConstantOverride("h_separation", 8);
        root.AddChild(routes);
        routes.AddChild(RouteCardUx9K("ORF-DM-035", "Gérer une quête en direct",
            "Inspection, joueurs ciblés, conséquences et adaptateur RPG."));
        routes.AddChild(RouteCardUx9K("ORF-DM-036", "Gérer une rencontre en direct",
            "Cinq commandes, scaling borné et adaptateurs spawn et IA."));

        _moduleChoice = ChoiceUx9K(null, "Sélection", "Sélectionner un module");
        _moduleChoice.ItemSelected += index => SelectModuleUx9K((int)index);
        root.AddChild(LabeledRowUx9K("Module explicitement ciblé", _moduleChoice));
        _focusChoice = ChoiceUx9K(null, "Workflow", "Quête en direct",
            "Rencontre en direct");
        _focusChoice.ItemSelected += index => SelectFocusUx9K((int)index);
        root.AddChild(LabeledRowUx9K("Workflow direct", _focusChoice));

        var editor = StudioStyle.Panel("Ux9KEditor", StudioStyle.SurfaceRaised, 9, 1);
        editor.CustomMinimumSize = new Vector2(0, 300);
        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(editor);
        var stack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        stack.AddThemeConstantOverride("separation", 8);
        editor.AddChild(stack);
        BuildQuestPageUx9K(stack);
        BuildEncounterPageUx9K(stack);

        var decisions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        decisions.AddThemeConstantOverride("separation", 18);
        root.AddChild(decisions);
        _approval = CheckUx9K(decisions,
            "J’approuve ce brouillon et cette révision après l’aperçu sec");
        _probeConfirmation = CheckUx9K(decisions,
            "Je confirme la sandbox locale restaurée en fin d’exécution");

        var review = StudioStyle.Panel("Ux9KReview", StudioStyle.SurfaceRaised, 7, 1);
        review.CustomMinimumSize = new Vector2(0, 105);
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
            "Sélectionnez explicitement le module avant de diriger la narration.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "Sandbox isolée : aucune quête, rencontre ou partie réelle, aucun serveur et aucun joueur externe ne sont modifiés.",
            10, StudioStyle.Warning, wrap: true));

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 7);
        root.AddChild(actions);
        _previewButton = ActionButtonUx9K(actions, "Prévisualiser", PreviewCurrentUx9K, 122,
            primary: true);
        _applyButton = ActionButtonUx9K(actions, "Appliquer", ApplyCurrentUx9K, 104);
        _undoButton = ActionButtonUx9K(actions, "Annuler", UndoUx9K, 92);
        _redoButton = ActionButtonUx9K(actions, "Rétablir", RedoUx9K, 92);
        _saveButton = ActionButtonUx9K(actions, "Enregistrer", SaveUx9K, 112);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _probeButton = ActionButtonUx9K(actions, "Tester localement", ProbeUx9K, 148);

        ConnectInvalidationUx9K();
        SelectFocusUx9K(0);
        UpdateActionsUx9K();
    }

    private void BuildQuestPageUx9K(VBoxContainer parent)
    {
        var page = FocusPageUx9K(parent,
            "Quête Les Cendres · transition prévisualisée pour tous ou une sélection");
        var grid = GridUx9K(page);
        _questScope = ChoiceUx9K(grid, "Joueurs concernés", "Tous les joueurs",
            "Joueurs sélectionnés");
        _questTargetState = ChoiceUx9K(grid, "Transition", "Terminée", "Échouée");
        _requiredAtRuntime = CheckUx9K(page,
            "Exiger ce profil au démarrage du runtime Live Director");
        page.AddChild(StudioStyle.Label(
            "L’aperçu liste état, joueurs, scripts et journal ; l’adaptateur RPG projette ensuite la mise à jour de quête.",
            10, StudioStyle.Success, wrap: true));
    }

    private void BuildEncounterPageUx9K(VBoxContainer parent)
    {
        var page = FocusPageUx9K(parent,
            "Rencontre Gardien de la Crypte · inspection et commandes autoritaires");
        var grid = GridUx9K(page);
        _encounterAction = ChoiceUx9K(grid, "Commande mise en avant", "Activer", "Mettre en pause",
            "Changer de vague", "Ajuster le scaling", "Retirer les créatures");
        _encounterValue = LineUx9K(grid, "Valeur de vague ou scaling", "1.5");
        page.AddChild(StudioStyle.Label(
            "La sandbox vérifie les cinq commandes, la borne 0,25–4, puis les adaptateurs spawn et IA avant rollback complet.",
            10, StudioStyle.Success, wrap: true));
    }

    private VBoxContainer FocusPageUx9K(VBoxContainer parent, string title)
    {
        var page = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 8);
        page.AddChild(StudioStyle.Label(title, 12, StudioStyle.Text));
        parent.AddChild(page);
        _focusPages.Add(page);
        return page;
    }

    private void OpenCurrentUx9K()
    {
        Visible = true;
        ResetUx9K();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionner un module");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(
                _session.Project.Module.DisplayName));
        _moduleChoice.Select(0);
        RefreshAuditUx9K();
        UpdateBoundsUx9K();
        UpdateActionsUx9K();
    }

    private void SelectModuleUx9K(int index)
    {
        _explicitSelection = index == 1 && _session.Project is not null;
        _session.CancelLiveNarrativeApprovalUx9K();
        _preview = null;
        _approval.ButtonPressed = false;
        if (!_explicitSelection)
        {
            _draft = null;
            SetStatusUx9K("Sélectionnez explicitement le module.", false);
            UpdateActionsUx9K();
            return;
        }
        _draft = _session.CreateLiveNarrativeDraftUx9K(explicitlySelected: true);
        if (_draft is null)
        {
            SetStatusUx9K(_session.LiveNarrativeLoadMessageUx9K ??
                          "Le profil narratif est indisponible.", true);
            UpdateActionsUx9K();
            return;
        }
        PopulateDraftUx9K(_draft);
        PopulateOptionsUx9K(StudioUx9KNarrativeProbeOptions.CreateDefault());
        SetStatusUx9K(_session.LiveNarrativeFileMissingUx9K
            ? "Profil initial chargé en mémoire · aucun fichier créé avant Enregistrer."
            : "Profil narratif chargé · prévisualisez avant toute application.", false);
        UpdateActionsUx9K();
    }

    private void PopulateDraftUx9K(StudioUx9KNarrativeDraft draft)
    {
        _updating = true;
        _requiredAtRuntime.ButtonPressed = draft.RequiredAtRuntime;
        _updating = false;
    }

    private void PopulateOptionsUx9K(StudioUx9KNarrativeProbeOptions value)
    {
        _updating = true;
        _questScope.Select(Array.IndexOf(QuestScopeValues, value.QuestScope));
        _questTargetState.Select(Array.IndexOf(QuestStateValues, value.QuestTargetState));
        _encounterAction.Select(Array.IndexOf(EncounterActionValues, value.EncounterAction));
        _encounterValue.Text = value.EncounterValue;
        _updating = false;
    }

    private StudioUx9KNarrativeDraft? ReadDraftUx9K() => _draft is null ? null : _draft with
    {
        RequiredAtRuntime = _requiredAtRuntime.ButtonPressed
    };

    private StudioUx9KNarrativeProbeOptions? ReadOptionsUx9K()
    {
        if (!_explicitSelection) return null;
        return new(QuestScopeValues[_questScope.Selected],
            QuestStateValues[_questTargetState.Selected],
            EncounterActionValues[_encounterAction.Selected], _encounterValue.Text.Trim());
    }

    private void PreviewCurrentUx9K()
    {
        var draft = ReadDraftUx9K();
        var options = ReadOptionsUx9K();
        if (draft is null || options is null)
        {
            SetStatusUx9K("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewLiveNarrativeUx9K(draft, options);
        PopulatePreviewUx9K();
        SetStatusUx9K(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx9K();
    }

    private void ApplyCurrentUx9K()
    {
        var draft = ReadDraftUx9K();
        if (draft is null) return;
        var focus = _focusChoice.Selected;
        var result = _session.ApplyApprovedLiveNarrativeUx9K(draft);
        SetStatusUx9K(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            SelectModuleUx9K(1);
            SelectFocusUx9K(focus);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9K();
    }

    private void UndoUx9K()
    {
        var result = _session.UndoLiveNarrativeUx9K();
        SetStatusUx9K(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            SelectModuleUx9K(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9K();
    }

    private void RedoUx9K()
    {
        var result = _session.RedoLiveNarrativeUx9K();
        SetStatusUx9K(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            SelectModuleUx9K(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9K();
    }

    private void SaveUx9K()
    {
        var result = _session.SaveLiveNarrativeUx9K();
        SetStatusUx9K(result.Message, !result.Succeeded);
        UpdateActionsUx9K();
    }

    private void ProbeUx9K()
    {
        var options = ReadOptionsUx9K();
        if (options is null) return;
        var result = _session.RunLocalLiveNarrativeUx9K(_probeConfirmation.ButtonPressed,
            NetworkRole.Administrator, options);
        SetStatusUx9K(result.Message, !result.Succeeded);
        RefreshAuditUx9K();
        PopulatePreviewUx9K();
        UpdateActionsUx9K();
    }

    private void PopulatePreviewUx9K()
    {
        var probe = _session.LiveNarrativeProbeUx9K;
        _previewSummary.Text = _preview is null
            ? $"Sandbox · {probe.Status}."
            : $"{_preview.Message} · {string.Join(" · ", _preview.SemanticDiff)}\n" +
              $"Quête {HumanCodeUx9K(probe.QuestCode)} · {probe.AffectedPlayers} joueur(s) · " +
              $"{probe.QuestConsequences} conséquences · rencontre {HumanCodeUx9K(probe.EncounterActionCode)} · " +
              $"{probe.EncounterActions} commandes · scaling {(probe.ScalingBounded ? "borné" : "invalide")}.";
    }

    private void RefreshAuditUx9K()
    {
        var audit = _session.LiveNarrativeAuditUx9K;
        var probe = _session.LiveNarrativeProbeUx9K;
        _auditSummary.Text = audit.Count == 0
            ? "Journal local · aucune entrée"
            : $"Journal local · {audit.Count} entrée(s) · narration {(probe.Compensated ? "restaurée" : "active")} · " +
              $"ressources {(probe.ResourcesReleased ? "libérées" : "à vérifier")}.";
    }

    private void ConfigureEvidenceUx9K(string mode)
    {
        _updating = true;
        _requiredAtRuntime.ButtonPressed = true;
        PopulateOptionsUx9K(StudioUx9KNarrativeProbeOptions.CreateDefault());
        _approval.ButtonPressed = true;
        _probeConfirmation.ButtonPressed = true;
        _updating = false;
        var focus = mode.Contains("encounter", StringComparison.OrdinalIgnoreCase) ||
                    mode.Contains("rencontre", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _focusChoice.Select(focus);
        SelectFocusUx9K(focus);
    }

    private void SelectFocusUx9K(int index)
    {
        for (var i = 0; i < _focusPages.Count; i++) _focusPages[i].Visible = i == index;
    }

    private void ConnectInvalidationUx9K()
    {
        foreach (var option in new[] { _questScope, _questTargetState })
            option.ItemSelected += _ => InvalidateUx9K();
        _encounterAction.ItemSelected += EncounterActionChangedUx9K;
        _encounterValue.TextChanged += _ => InvalidateUx9K();
        _requiredAtRuntime.Toggled += _ => InvalidateUx9K();
        _approval.Toggled += _ => UpdateActionsUx9K();
        _probeConfirmation.Toggled += _ => UpdateActionsUx9K();
    }

    private void EncounterActionChangedUx9K(long index)
    {
        if (_updating) return;
        _updating = true;
        _encounterValue.Text = index switch
        {
            2 => "2",
            3 => "1.5",
            _ => string.Empty
        };
        _updating = false;
        InvalidateUx9K();
    }

    private void InvalidateUx9K()
    {
        if (_updating) return;
        _preview = null;
        _session.CancelLiveNarrativeApprovalUx9K();
        _approval.ButtonPressed = false;
        PopulatePreviewUx9K();
        UpdateActionsUx9K();
    }

    private void UpdateActionsUx9K()
    {
        var selected = _explicitSelection && _draft is not null;
        _previewButton.Disabled = !selected;
        _applyButton.Disabled = !selected || _preview is not { CanApply: true } ||
                                !_approval.ButtonPressed;
        _undoButton.Disabled = !_canUndo;
        _redoButton.Disabled = !_canRedo;
        _saveButton.Disabled = !_session.LiveNarrativeHasUnsavedChangesUx9K;
        _probeButton.Disabled = !selected || !_probeConfirmation.ButtonPressed ||
                                _session.LiveNarrativeHasUnsavedChangesUx9K;
    }

    private void ResetUx9K()
    {
        _draft = null;
        _preview = null;
        _explicitSelection = false;
        _canUndo = false;
        _canRedo = false;
        if (_previewSummary is not null) PopulatePreviewUx9K();
    }

    private SourceSnapshotUx9K CaptureSourceUx9K(StudioSessionController session) => new(
        session.Project, session.History, session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [], session.IsDirty, session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(), session.SelectedEntityId,
        session.CaptureLiveNarrativeApprovalUx9K());

    private bool SourceUnchangedUx9K()
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
               source.CaptureLiveNarrativeApprovalUx9K() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx9K()
    {
        if (_sourceSession is not null) _session = _sourceSession;
        _sourceSession = null;
        _sourceSnapshot = null;
        _sourceSessionRestored = true;
    }

    private void DeleteEvidenceUx9K()
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-k-ui-",
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
            SetStatusUx9K($"La copie temporaire n’a pas pu être libérée : {exception.Message}",
                true);
        }
    }

    private void UpdateBoundsUx9K()
    {
        if (_dialog is null) return;
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(960, size.X - 48), Math.Max(700, size.Y - 36));
    }

    private void SetStatusUx9K(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger :
            StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private static Control RouteCardUx9K(string route, string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux9K" + route.Replace("-", string.Empty),
            StudioStyle.SurfaceRaised, 7, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var stack = new VBoxContainer();
        panel.AddChild(stack);
        stack.AddChild(StudioStyle.Label($"{route} · {title}", 11, StudioStyle.Text,
            wrap: true));
        stack.AddChild(StudioStyle.Label(detail, 10, StudioStyle.Muted, wrap: true));
        return panel;
    }

    private static Control LabeledRowUx9K(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(title);
        row.AddChild(control);
        return row;
    }

    private static GridContainer GridUx9K(Control parent)
    {
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        parent.AddChild(grid);
        return grid;
    }

    private static LineEdit LineUx9K(GridContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new LineEdit
        {
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(320, 34)
        };
        parent.AddChild(value);
        return value;
    }

    private static OptionButton ChoiceUx9K(GridContainer? parent, string label,
        params string[] choices)
    {
        parent?.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var value = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(320, 34)
        };
        foreach (var choice in choices) value.AddItem(choice);
        parent?.AddChild(value);
        return value;
    }

    private static CheckBox CheckUx9K(Control parent, string text)
    {
        var check = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(check);
        return check;
    }

    private static Button ActionButtonUx9K(HBoxContainer parent, string text, Action action,
        float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static IEnumerable<string> DescendantTextUx9K(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is CheckBox check) yield return check.Text;
        if (node is LineEdit line) yield return line.Text;
        if (node is OptionButton option)
            for (var index = 0; index < option.ItemCount; index++)
                yield return option.GetItemText(index);
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx9K(child)) yield return value;
    }

    private static bool ContainsUuidUx9K(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);

    private static string HumanCodeUx9K(string code) => code switch
    {
        "LIVE_QUEST_APPLIED" => "appliquée puis restaurée",
        "LIVE_QUEST_PREVIEW" => "prévisualisée",
        "LIVE_ENCOUNTER_APPLIED" => "appliquée puis restaurée",
        "LIVE_ENCOUNTER_PREVIEW" => "prévisualisée",
        _ => code
    };

    private static readonly string[] QuestScopeValues =
        ["all-players", "selected-players"];
    private static readonly string[] QuestStateValues = ["completed", "failed"];
    private static readonly string[] EncounterActionValues =
        ["activate", "pause", "wave", "scale", "despawn"];

    private sealed record SourceSnapshotUx9K(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool Dirty,
        Forge.Domain.Areas.AreaId? Area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? Selected,
        StudioUx9KApprovalSnapshot Approval);
}
