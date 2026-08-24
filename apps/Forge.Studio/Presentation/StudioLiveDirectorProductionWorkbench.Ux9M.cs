using System.Text.RegularExpressions;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Operations;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioLiveDirectorProductionWorkbenchUx9M : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioUx9MSourceSnapshot? _sourceSnapshot;
    private StudioUx9MProductionDraft? _draft;
    private StudioUx9MProductionPreview? _preview;
    private bool _explicitSelection;
    private bool _updating;
    private bool _canUndo;
    private bool _canRedo;
    private bool _canSave;
    private bool _evidenceResourcesReleased = true;
    private bool _sourceSessionRestored = true;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private CheckBox _restEnabled = null!;
    private SpinBox _restTargets = null!;
    private CheckBox _levelEnabled = null!;
    private SpinBox _levelDelta = null!;
    private CheckBox _rollbackSnapshot = null!;
    private CheckBox _appearanceEnabled = null!;
    private SpinBox _minimumScale = null!;
    private SpinBox _maximumScale = null!;
    private CheckBox _groupEnabled = null!;
    private LineEdit _groupKey = null!;
    private LineEdit _groupQuery = null!;
    private SpinBox _groupHotkey = null!;
    private SpinBox _groupMembers = null!;
    private CheckBox _recordingEnabled = null!;
    private OptionButton _exportFormat = null!;
    private SpinBox _recordingMinutes = null!;
    private CheckBox _redactPrivate = null!;
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
    internal bool EvidenceSourceUntouched => SourceUnchangedUx9M();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx9M(this));
            return Visible && new[]
                   {
                       "ORF-DM-015", "ORF-DM-018", "ORF-DM-030", "ORF-DM-022",
                       "ORF-DM-037"
                   }.All(route => text.Contains(route, StringComparison.Ordinal)) &&
                   text.Contains("motif", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("snapshot", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("validation des assets", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("raccourci", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("données privées", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tester localement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun joueur ni serveur réel",
                       StringComparison.OrdinalIgnoreCase) && !ContainsUuidUx9M(text);
        }
    }

    internal StudioLiveDirectorProductionWorkbenchUx9M(
        StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx9M();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9M();
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
        _session.CancelLiveProductionApprovalUx9M();
        ResetUx9M();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx9M(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-m-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx9M();
            RestoreSourceSessionUx9M();
            return false;
        }
        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx9M();
        _moduleChoice.Select(1);
        SelectModuleUx9M(1);
        ConfigureEvidenceUx9M(mode);
        PreviewCurrentUx9M();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady &&
                    ShowsHumanSurface && EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_M_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
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
        var draft = ReadDraftUx9M();
        if (draft is null) return false;
        var preview = _session.PreviewLiveProductionUx9M(draft);
        var applied = preview.CanApply && _approval.ButtonPressed &&
                      _session.ApplyApprovedLiveProductionUx9M(draft).Succeeded;
        var undo = applied && _session.UndoLiveProductionUx9M().Succeeded;
        var redo = undo && _session.RedoLiveProductionUx9M().Succeeded;
        var save = redo && _session.SaveLiveProductionUx9M().Succeeded;
        var probed = save && _sandboxConfirmation.ButtonPressed &&
                     _session.RunLocalLiveProductionUx9M(true,
                         NetworkRole.Administrator).Succeeded;
        var expected = _session.Project;
        var reopened = new StudioSessionController();
        var reopenedOk = probed && expected is not null &&
                         reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.Project is { } actual &&
                         string.Equals(ProjectJsonSerializer.Serialize(expected),
                             ProjectJsonSerializer.Serialize(actual), StringComparison.Ordinal);
        reopened.Close();
        RefreshAuditUx9M();
        return preview.CanonicalDryRun && applied && undo && redo && save && probed &&
               reopenedOk && _session.LiveProductionProbeUx9M.ResourcesReleased &&
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
        ResetUx9M();
        DeleteEvidenceUx9M();
        RestoreSourceSessionUx9M();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9M();
        _moduleChoice.Select(1);
        SelectModuleUx9M(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx9M();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx9M;
        UpdateBoundsUx9M();
    }

    public override void _ExitTree() => CloseEvidence();

    private void BuildUiUx9M()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color("10121af2");
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _dialog = StudioStyle.Panel("Ux9MLiveProduction", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var root = new VBoxContainer
        {
            Name = "Ux9MRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 7);
        _dialog.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(headingText);
        headingText.AddChild(StudioStyle.Label(
            "Live Director · interventions et enregistrement", 20, StudioStyle.Text));
        headingText.AddChild(StudioStyle.Label(
            "Cinq politiques de production configurées sans identifiant technique.",
            11, StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);

        _moduleChoice = ChoiceUx9M("Sélectionner un module");
        _moduleChoice.ItemSelected += index => SelectModuleUx9M((int)index);
        root.AddChild(LabeledRowUx9M("Module explicitement ciblé", _moduleChoice));

        var policies = new GridContainer
        {
            Columns = 5,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            CustomMinimumSize = new Vector2(0, 390)
        };
        policies.AddThemeConstantOverride("h_separation", 7);
        root.AddChild(policies);

        var rest = PolicyCardUx9M(policies, "ORF-DM-015", "Repos forcé",
            "Motif obligatoire, plafond de cibles et audit.");
        _restEnabled = CheckUx9M(rest, "Autoriser le repos forcé");
        _restTargets = NumberUx9M(rest, "Cibles maximum", 1, 256, 32);

        var level = PolicyCardUx9M(policies, "ORF-DM-018", "Ajuster le niveau",
            "Confirmation et retour sûr avant toute correction.");
        _levelEnabled = CheckUx9M(level, "Autoriser l’ajustement");
        _levelDelta = NumberUx9M(level, "Amplitude maximum", 1, 100, 5);
        _rollbackSnapshot = CheckUx9M(level, "Exiger un snapshot de retour");

        var appearance = PolicyCardUx9M(policies, "ORF-DM-030", "Apparence",
            "Échelle temporaire et validation des assets.");
        _appearanceEnabled = CheckUx9M(appearance, "Autoriser l’apparence");
        _minimumScale = NumberUx9M(appearance, "Échelle minimum", .1, 10, .5,
            step: .05);
        _maximumScale = NumberUx9M(appearance, "Échelle maximum", .1, 10, 2,
            step: .05);

        var group = PolicyCardUx9M(policies, "ORF-DM-022", "Groupe sauvegardé",
            "Requête, raccourci et plafond de membres.");
        _groupEnabled = CheckUx9M(group, "Conserver ce groupe");
        _groupKey = TextUx9M(group, "Nom lisible", "groupe-live");
        _groupQuery = TextUx9M(group, "Requête", "tag:compagnons");
        _groupHotkey = NumberUx9M(group, "Raccourci (1–12)", 1, 12, 1);
        _groupMembers = NumberUx9M(group, "Membres maximum", 1, 512, 16);

        var recording = PolicyCardUx9M(policies, "ORF-DM-037",
            "Enregistrement", "Export explicite et données privées expurgées.");
        _recordingEnabled = CheckUx9M(recording, "Autoriser l’enregistrement");
        recording.AddChild(StudioStyle.Label("Format d’export", 9, StudioStyle.Muted));
        _exportFormat = ChoiceUx9M("Replay OpenRPG", "Journal JSONL");
        recording.AddChild(_exportFormat);
        _recordingMinutes = NumberUx9M(recording, "Durée maximum (min)", 1, 1440,
            240);
        _redactPrivate = CheckUx9M(recording, "Expurger les données privées");

        var decisions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        decisions.AddThemeConstantOverride("separation", 18);
        root.AddChild(decisions);
        _approval = CheckUx9M(decisions,
            "J’approuve ce brouillon et cette révision après l’aperçu sec");
        _sandboxConfirmation = CheckUx9M(decisions,
            "Je confirme la sandbox locale restaurée en fin d’exécution");

        var review = StudioStyle.Panel("Ux9MReview", StudioStyle.SurfaceRaised, 7, 1);
        review.CustomMinimumSize = new Vector2(0, 92);
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
            "Sélectionnez explicitement le module avant toute intervention.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "Sandbox isolée : aucun joueur ni serveur réel n’est modifié ; aucun port n’est ouvert.",
            10, StudioStyle.Warning, wrap: true));

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 7);
        root.AddChild(actions);
        _previewButton = ActionButtonUx9M(actions, "Prévisualiser", PreviewCurrentUx9M,
            122, primary: true);
        _applyButton = ActionButtonUx9M(actions, "Appliquer", ApplyCurrentUx9M, 104);
        _undoButton = ActionButtonUx9M(actions, "Annuler", UndoUx9M, 92);
        _redoButton = ActionButtonUx9M(actions, "Rétablir", RedoUx9M, 92);
        _saveButton = ActionButtonUx9M(actions, "Enregistrer", SaveUx9M, 112);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _sandboxButton = ActionButtonUx9M(actions, "Tester localement", SandboxUx9M,
            148);

        ConnectInvalidationUx9M();
        UpdateActionsUx9M();
    }

    private void OpenCurrentUx9M()
    {
        Visible = true;
        ResetUx9M();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionner un module");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(
                _session.Project.Module.DisplayName));
        _moduleChoice.Select(0);
        RefreshAuditUx9M();
        UpdateBoundsUx9M();
        UpdateActionsUx9M();
    }

    private void SelectModuleUx9M(int index)
    {
        _explicitSelection = index == 1 && _session.Project is not null;
        _session.CancelLiveProductionApprovalUx9M();
        _preview = null;
        _approval.ButtonPressed = false;
        if (!_explicitSelection)
        {
            _draft = null;
            SetStatusUx9M("Sélectionnez explicitement le module.", false);
            UpdateActionsUx9M();
            return;
        }
        _draft = _session.CreateLiveProductionDraftUx9M(explicitlySelected: true);
        if (_draft is null)
        {
            SetStatusUx9M("Les politiques Live Director sont indisponibles.", true);
            UpdateActionsUx9M();
            return;
        }
        PopulateDraftUx9M(_draft);
        SetStatusUx9M(
            "Politiques chargées · prévisualisez avant toute application.", false);
        UpdateActionsUx9M();
    }

    private void PopulateDraftUx9M(StudioUx9MProductionDraft draft)
    {
        _updating = true;
        _restEnabled.ButtonPressed = draft.ForcedRestEnabled;
        _restTargets.Value = draft.MaximumRestTargets;
        _levelEnabled.ButtonPressed = draft.LevelAdjustmentEnabled;
        _levelDelta.Value = draft.MaximumLevelDelta;
        _rollbackSnapshot.ButtonPressed = draft.RequireRollbackSnapshot;
        _appearanceEnabled.ButtonPressed = draft.AppearanceEnabled;
        _minimumScale.Value = draft.MinimumScale;
        _maximumScale.Value = draft.MaximumScale;
        _groupEnabled.ButtonPressed = draft.HasSavedGroup;
        _groupKey.Text = draft.GroupKey;
        _groupQuery.Text = draft.GroupQuery;
        _groupHotkey.Value = draft.GroupHotkey;
        _groupMembers.Value = draft.MaximumGroupMembers;
        _recordingEnabled.ButtonPressed = draft.RecordingEnabled;
        _exportFormat.Select(draft.ExportFormat == "jsonl" ? 1 : 0);
        _recordingMinutes.Value = draft.MaximumRecordingMinutes;
        _redactPrivate.ButtonPressed = draft.RedactPrivateData;
        _updating = false;
    }

    private StudioUx9MProductionDraft? ReadDraftUx9M() => _draft is null ? null :
        _draft with
        {
            ForcedRestEnabled = _restEnabled.ButtonPressed,
            MaximumRestTargets = (int)_restTargets.Value,
            LevelAdjustmentEnabled = _levelEnabled.ButtonPressed,
            MaximumLevelDelta = (int)_levelDelta.Value,
            RequireRollbackSnapshot = _rollbackSnapshot.ButtonPressed,
            AppearanceEnabled = _appearanceEnabled.ButtonPressed,
            MinimumScale = _minimumScale.Value,
            MaximumScale = _maximumScale.Value,
            HasSavedGroup = _groupEnabled.ButtonPressed,
            GroupKey = _groupKey.Text.Trim(),
            GroupQuery = _groupQuery.Text.Trim(),
            GroupHotkey = (int)_groupHotkey.Value,
            MaximumGroupMembers = (int)_groupMembers.Value,
            RecordingEnabled = _recordingEnabled.ButtonPressed,
            ExportFormat = _exportFormat.Selected == 1 ? "jsonl" : "orp-replay",
            MaximumRecordingMinutes = (int)_recordingMinutes.Value,
            RedactPrivateData = _redactPrivate.ButtonPressed
        };

    private void PreviewCurrentUx9M()
    {
        var draft = ReadDraftUx9M();
        if (draft is null)
        {
            SetStatusUx9M("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewLiveProductionUx9M(draft);
        PopulatePreviewUx9M();
        SetStatusUx9M(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx9M();
    }

    private void ApplyCurrentUx9M()
    {
        var draft = ReadDraftUx9M();
        if (draft is null) return;
        var result = _session.ApplyApprovedLiveProductionUx9M(draft);
        SetStatusUx9M(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            _canSave = true;
            SelectModuleUx9M(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9M();
    }

    private void UndoUx9M()
    {
        var result = _session.UndoLiveProductionUx9M();
        SetStatusUx9M(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            _canSave = true;
            SelectModuleUx9M(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9M();
    }

    private void RedoUx9M()
    {
        var result = _session.RedoLiveProductionUx9M();
        SetStatusUx9M(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            _canSave = true;
            SelectModuleUx9M(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9M();
    }

    private void SaveUx9M()
    {
        var result = _session.SaveLiveProductionUx9M();
        SetStatusUx9M(result.Message, !result.Succeeded);
        if (result.Succeeded) _canSave = false;
        UpdateActionsUx9M();
    }

    private void SandboxUx9M()
    {
        var result = _session.RunLocalLiveProductionUx9M(
            _sandboxConfirmation.ButtonPressed, NetworkRole.Administrator);
        SetStatusUx9M(result.Message, !result.Succeeded);
        RefreshAuditUx9M();
        PopulatePreviewUx9M();
        UpdateActionsUx9M();
    }

    private void PopulatePreviewUx9M()
    {
        var probe = _session.LiveProductionProbeUx9M;
        _previewSummary.Text = _preview is null
            ? $"Sandbox · {probe.Status}."
            : $"{_preview.Message} · {string.Join(" · ", _preview.SemanticDiff)}\n" +
              $"Repos {(probe.RestPolicyRespected ? "validé" : "refusé")} · " +
              $"niveau {(probe.LevelPolicyRespected ? "validé" : "refusé")} · " +
              $"apparence {(probe.AppearancePolicyRespected ? "validée" : "refusée")} · " +
              $"enregistrement {(probe.RecordingPolicyRespected ? "validé" : "refusé")} · " +
              $"{probe.RecordedMembers} membre(s), données privées " +
              $"{(probe.PrivateDataRedacted ? "expurgées" : "conservées")}.";
    }

    private void RefreshAuditUx9M()
    {
        var audit = _session.LiveProductionAuditUx9M;
        _auditSummary.Text = audit.Count == 0
            ? "Journal local · aucune entrée"
            : $"Journal local · {audit.Count} entrée(s) · ressources " +
              $"{(_session.LiveProductionProbeUx9M.ResourcesReleased ? "libérées" : "à vérifier")}.";
    }

    private void ConfigureEvidenceUx9M(string mode)
    {
        if (_draft is null) return;
        _updating = true;
        _restEnabled.ButtonPressed = true;
        _restTargets.Value = _draft.MaximumRestTargets == 24 ? 25 : 24;
        _levelEnabled.ButtonPressed = true;
        _levelDelta.Value = _draft.MaximumLevelDelta == 7 ? 8 : 7;
        _rollbackSnapshot.ButtonPressed = true;
        _appearanceEnabled.ButtonPressed = true;
        _minimumScale.Value = Math.Abs(_draft.MinimumScale - .75) < .001 ? .8 : .75;
        _maximumScale.Value = Math.Abs(_draft.MaximumScale - 1.75) < .001 ? 1.8 : 1.75;
        _groupEnabled.ButtonPressed = true;
        _groupKey.Text = _draft.HasSavedGroup ? _draft.GroupKey : "groupe-preuve";
        _groupQuery.Text = _draft.HasSavedGroup ? _draft.GroupQuery : "tag:compagnons";
        _groupHotkey.Value = _draft.GroupHotkey;
        _groupMembers.Value = _draft.MaximumGroupMembers == 24 ? 25 : 24;
        _recordingEnabled.ButtonPressed = true;
        _exportFormat.Select(_draft.ExportFormat == "orp-replay" ? 1 : 0);
        _recordingMinutes.Value = _draft.MaximumRecordingMinutes == 180 ? 181 : 180;
        _redactPrivate.ButtonPressed = true;
        _approval.ButtonPressed = true;
        _sandboxConfirmation.ButtonPressed = true;
        _updating = false;
    }

    private void ConnectInvalidationUx9M()
    {
        foreach (var check in new[]
                 {
                     _restEnabled, _levelEnabled, _rollbackSnapshot, _appearanceEnabled,
                     _groupEnabled, _recordingEnabled, _redactPrivate
                 })
            check.Toggled += _ => InvalidateUx9M();
        foreach (var number in new[]
                 {
                     _restTargets, _levelDelta, _minimumScale, _maximumScale,
                     _groupHotkey, _groupMembers, _recordingMinutes
                 })
            number.ValueChanged += _ => InvalidateUx9M();
        _groupKey.TextChanged += _ => InvalidateUx9M();
        _groupQuery.TextChanged += _ => InvalidateUx9M();
        _exportFormat.ItemSelected += _ => InvalidateUx9M();
        _approval.Toggled += _ => UpdateActionsUx9M();
        _sandboxConfirmation.Toggled += _ => UpdateActionsUx9M();
    }

    private void InvalidateUx9M()
    {
        if (_updating) return;
        _preview = null;
        _session.CancelLiveProductionApprovalUx9M();
        _approval.ButtonPressed = false;
        PopulatePreviewUx9M();
        UpdateActionsUx9M();
    }

    private void UpdateActionsUx9M()
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

    private void ResetUx9M()
    {
        _draft = null;
        _preview = null;
        _explicitSelection = false;
        _canUndo = false;
        _canRedo = false;
        _canSave = false;
        if (_previewSummary is not null) PopulatePreviewUx9M();
    }

    private StudioUx9MSourceSnapshot CaptureSourceUx9M(
        StudioSessionController session) => new(session.Project, session.History,
        session.History?.State.Revision, session.History?.Journal.ToArray() ?? [],
        session.IsDirty, session.History?.CanUndo ?? false,
        session.History?.CanRedo ?? false, session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(), session.SelectedEntityId,
        session.CaptureLiveProductionApprovalUx9M());

    private bool SourceUnchangedUx9M()
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
               source.CaptureLiveProductionApprovalUx9M() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx9M()
    {
        if (_sourceSession is not null)
        {
            _session = _sourceSession;
            if (_sourceSnapshot is not null)
                _session.RestoreLiveProductionApprovalUx9M(_sourceSnapshot.Approval);
        }
        _sourceSession = null;
        _sourceSnapshot = null;
        _sourceSessionRestored = true;
    }

    private void DeleteEvidenceUx9M()
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-m-ui-",
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
            SetStatusUx9M(
                $"La copie temporaire n’a pas pu être libérée : {exception.Message}", true);
        }
    }

    private void UpdateBoundsUx9M()
    {
        if (_dialog is null) return;
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(1100, size.X - 48),
            Math.Max(760, size.Y - 36));
    }

    private void SetStatusUx9M(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger :
            StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private static VBoxContainer PolicyCardUx9M(Control parent, string route,
        string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux9M" + route.Replace("-", string.Empty),
            StudioStyle.SurfaceRaised, 8, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;
        parent.AddChild(panel);
        var stack = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        stack.AddThemeConstantOverride("separation", 6);
        panel.AddChild(stack);
        stack.AddChild(StudioStyle.Label($"{route} · {title}", 11, StudioStyle.Text,
            wrap: true));
        stack.AddChild(StudioStyle.Label(detail, 9, StudioStyle.Muted, wrap: true));
        return stack;
    }

    private static Control LabeledRowUx9M(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(title);
        row.AddChild(control);
        return row;
    }

    private static OptionButton ChoiceUx9M(params string[] choices)
    {
        var value = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(170, 32)
        };
        foreach (var choice in choices) value.AddItem(choice);
        return value;
    }

    private static CheckBox CheckUx9M(Control parent, string text)
    {
        var value = new CheckBox
        {
            Text = text,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        parent.AddChild(value);
        return value;
    }

    private static SpinBox NumberUx9M(Control parent, string label, double min,
        double max, double value, double step = 1)
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var number = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            AllowGreater = false,
            AllowLesser = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(170, 32)
        };
        parent.AddChild(number);
        return number;
    }

    private static LineEdit TextUx9M(Control parent, string label, string value)
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit
        {
            Text = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(170, 32)
        };
        parent.AddChild(field);
        return field;
    }

    private static Button ActionButtonUx9M(Control parent, string text, Action action,
        float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static IEnumerable<string> DescendantTextUx9M(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is LineEdit line) yield return line.Text;
        if (node is OptionButton option)
            for (var index = 0; index < option.ItemCount; index++)
                yield return option.GetItemText(index);
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx9M(child)) yield return value;
    }

    private static bool ContainsUuidUx9M(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);
}
