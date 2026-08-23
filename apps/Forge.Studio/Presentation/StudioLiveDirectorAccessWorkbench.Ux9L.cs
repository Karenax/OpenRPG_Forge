using System.Text.RegularExpressions;
using Forge.LiveDirector.Authoring;
using Forge.ModuleFormat.LiveDirector;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Operations;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioLiveDirectorAccessWorkbenchUx9L : ColorRect
{
    private StudioSessionController _session;
    private StudioSessionController? _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private SourceSnapshotUx9L? _sourceSnapshot;
    private StudioUx9LAccessDraft? _draft;
    private StudioUx9LAccessPreview? _preview;
    private bool _explicitSelection;
    private bool _updating;
    private bool _canUndo;
    private bool _canRedo;
    private bool _evidenceResourcesReleased = true;
    private bool _sourceSessionRestored = true;

    private PanelContainer _dialog = null!;
    private OptionButton _moduleChoice = null!;
    private CheckBox _requiredAtRuntime = null!;
    private SpinBox _sessionMinutes = null!;
    private SpinBox _inspectionHistory = null!;
    private SpinBox _playerRefresh = null!;
    private SpinBox _auditRetention = null!;
    private SpinBox _chatLength = null!;
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
        Succeeded: true, CanApply: true, CanonicalDryRun: true, RuntimeReady: true
    };
    internal bool EvidenceResourcesReleased => _evidenceResourcesReleased;
    internal bool SourceSessionRestored => _sourceSessionRestored;
    internal bool EvidenceSourceUntouched => SourceUnchangedUx9L();
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantTextUx9L(this));
            return Visible && new[]
                   {
                       "ORF-DM-002", "ORF-DM-011", "ORF-DM-025", "ORF-DM-031",
                       "ORF-DM-034"
                   }.All(route => text.Contains(route, StringComparison.Ordinal)) &&
                   text.Contains("MFA", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("lecture seule", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("latence", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("chaîne immuable", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("canaux MJ", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Tester localement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun compte, joueur, message ou serveur réel",
                       StringComparison.OrdinalIgnoreCase) && !ContainsUuidUx9L(text);
        }
    }

    internal StudioLiveDirectorAccessWorkbenchUx9L(StudioSessionController session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        BuildUiUx9L();
    }

    internal void Open()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9L();
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
        _session.CancelLiveAccessApprovalUx9L();
        ResetUx9L();
    }

    internal bool PrepareEvidence(string mode)
    {
        CloseEvidence();
        var sourceProject = _session.Project;
        if (sourceProject is null) return false;
        _sourceSession = _session;
        _sourceSnapshot = CaptureSourceUx9L(_sourceSession);
        _evidenceRoot = Path.Combine(Path.GetTempPath(),
            $"openrpg-forge-studio-ux9-l-ui-{Guid.NewGuid():N}");
        _evidenceResourcesReleased = false;
        _sourceSessionRestored = false;
        var controller = new StudioSessionController();
        var baseline = LiveDirectorAccessProfile.CreateDefault("evidence:ux9-l-ui");
        if (!ProjectSession.Create(_evidenceRoot, sourceProject).Succeeded ||
            !WorkspaceLiveDirectorAccessStore.Save(_evidenceRoot, baseline).Succeeded ||
            !controller.Open(_evidenceRoot).Succeeded)
        {
            controller.Close();
            DeleteEvidenceUx9L();
            RestoreSourceSessionUx9L();
            return false;
        }
        _evidenceSession = controller;
        _session = controller;
        OpenCurrentUx9L();
        _moduleChoice.Select(1);
        SelectModuleUx9L(1);
        ConfigureEvidenceUx9L(mode);
        PreviewCurrentUx9L();
        var ready = UsesIsolatedEvidence && HasExplicitSelection && PreviewReady &&
                    ShowsHumanSurface && EvidenceSourceUntouched;
        GD.Print($"OPENRPG_FORGE_STUDIO_UX9_L_EVIDENCE_{(ready ? "READY" : "FAILED")} " +
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
        var draft = ReadDraftUx9L();
        if (draft is null) return false;
        var preview = _session.PreviewLiveAccessUx9L(draft);
        var applied = preview.CanApply && _approval.ButtonPressed &&
                      _session.ApplyApprovedLiveAccessUx9L(draft).Succeeded;
        var undo = applied && _session.UndoLiveAccessUx9L().Succeeded;
        var redo = undo && _session.RedoLiveAccessUx9L().Succeeded;
        var save = redo && _session.SaveLiveAccessUx9L().Succeeded;
        var probed = save && _probeConfirmation.ButtonPressed &&
                     _session.RunLocalLiveAccessUx9L(true,
                         NetworkRole.Administrator).Succeeded;
        var expected = _session.CurrentLiveAccessUx9L;
        var reopened = new StudioSessionController();
        var reopenedOk = probed && expected is not null && reopened.Open(_evidenceRoot).Succeeded &&
                         reopened.CurrentLiveAccessUx9L is { } actual &&
                         new LiveDirectorAccessStore().Serialize(expected) ==
                         new LiveDirectorAccessStore().Serialize(actual);
        reopened.Close();
        RefreshAuditUx9L();
        return preview.CanonicalDryRun && applied && undo && redo && save && probed &&
               reopenedOk && _session.LiveAccessProbeUx9L.ResourcesReleased &&
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
        ResetUx9L();
        DeleteEvidenceUx9L();
        RestoreSourceSessionUx9L();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        OpenCurrentUx9L();
        _moduleChoice.Select(1);
        SelectModuleUx9L(1);
    }

    internal void HideSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) CloseEvidence();
        Visible = false;
        ResetUx9L();
    }

    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateBoundsUx9L;
        UpdateBoundsUx9L();
    }

    public override void _ExitTree() => CloseEvidence();

    private void BuildUiUx9L()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Color = new Color("10121af2");
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _dialog = StudioStyle.Panel("Ux9LLiveAccess", StudioStyle.Surface, 10, 1);
        AddChild(_dialog);
        var root = new VBoxContainer
        {
            Name = "Ux9LRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 7);
        _dialog.AddChild(root);

        var heading = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(heading);
        var headingText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(headingText);
        headingText.AddChild(StudioStyle.Label("Live Director · accès et supervision", 20,
            StudioStyle.Text));
        headingText.AddChild(StudioStyle.Label(
            "Configurer et vérifier cinq politiques serveur sans identifiant technique.",
            11, StudioStyle.Muted));
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        heading.AddChild(close);

        var routes = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        routes.AddThemeConstantOverride("h_separation", 7);
        root.AddChild(routes);
        routes.AddChild(RouteCardUx9L("ORF-DM-002", "Authentification MJ",
            "MFA, expiration, révocation et audit."));
        routes.AddChild(RouteCardUx9L("ORF-DM-011", "Examiner",
            "Six sections en lecture seule."));
        routes.AddChild(RouteCardUx9L("ORF-DM-025", "Liste des joueurs",
            "Colonnes opérationnelles et latence."));
        routes.AddChild(RouteCardUx9L("ORF-DM-031", "Journal d’audit MJ",
            "Chaîne immuable, filtres et rétention."));
        routes.AddChild(RouteCardUx9L("ORF-DM-034", "Chat et canaux MJ",
            "Modération, logs et incarnation motivée."));

        _moduleChoice = ChoiceUx9L("Sélectionner un module");
        _moduleChoice.ItemSelected += index => SelectModuleUx9L((int)index);
        root.AddChild(LabeledRowUx9L("Module explicitement ciblé", _moduleChoice));

        var editor = StudioStyle.Panel("Ux9LEditor", StudioStyle.SurfaceRaised, 9, 1);
        editor.CustomMinimumSize = new Vector2(0, 255);
        editor.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        root.AddChild(editor);
        var editorStack = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        editorStack.AddThemeConstantOverride("separation", 9);
        editor.AddChild(editorStack);
        editorStack.AddChild(StudioStyle.Label(
            "Politiques bornées · les garde-fous obligatoires restent actifs", 12,
            StudioStyle.Text));
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 9);
        editorStack.AddChild(grid);
        _sessionMinutes = NumberUx9L(grid, "Session MJ (minutes)", 1, 1440, 30);
        _inspectionHistory = NumberUx9L(grid, "Historique d’inspection", 1, 10000, 100);
        _playerRefresh = NumberUx9L(grid, "Rafraîchissement joueurs (s)", 1, 300, 5);
        _auditRetention = NumberUx9L(grid, "Rétention audit (jours)", 1, 3650, 90);
        _chatLength = NumberUx9L(grid, "Longueur message", 1, 500, 500);
        _requiredAtRuntime = CheckUx9L(editorStack,
            "Exiger ce profil au démarrage du runtime Live Director");
        editorStack.AddChild(StudioStyle.Label(
            "MFA, inspection lecture seule, huit colonnes joueurs, chaîne d’audit immuable et sept canaux MJ restent imposés.",
            10, StudioStyle.Success, wrap: true));

        var decisions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        decisions.AddThemeConstantOverride("separation", 18);
        root.AddChild(decisions);
        _approval = CheckUx9L(decisions,
            "J’approuve ce brouillon et cette révision après l’aperçu sec");
        _probeConfirmation = CheckUx9L(decisions,
            "Je confirme la sandbox locale restaurée en fin d’exécution");

        var review = StudioStyle.Panel("Ux9LReview", StudioStyle.SurfaceRaised, 7, 1);
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
            "Sélectionnez explicitement le module avant de configurer l’accès.", 10,
            StudioStyle.Muted, wrap: true);
        root.AddChild(_status);
        root.AddChild(StudioStyle.Label(
            "Sandbox isolée : aucun compte, joueur, message ou serveur réel n’est modifié ; aucun port n’est ouvert.",
            10, StudioStyle.Warning, wrap: true));

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddThemeConstantOverride("separation", 7);
        root.AddChild(actions);
        _previewButton = ActionButtonUx9L(actions, "Prévisualiser", PreviewCurrentUx9L, 122,
            primary: true);
        _applyButton = ActionButtonUx9L(actions, "Appliquer", ApplyCurrentUx9L, 104);
        _undoButton = ActionButtonUx9L(actions, "Annuler", UndoUx9L, 92);
        _redoButton = ActionButtonUx9L(actions, "Rétablir", RedoUx9L, 92);
        _saveButton = ActionButtonUx9L(actions, "Enregistrer", SaveUx9L, 112);
        actions.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _probeButton = ActionButtonUx9L(actions, "Tester localement", ProbeUx9L, 148);

        ConnectInvalidationUx9L();
        UpdateActionsUx9L();
    }

    private void OpenCurrentUx9L()
    {
        Visible = true;
        ResetUx9L();
        _moduleChoice.Clear();
        _moduleChoice.AddItem("Sélectionner un module");
        if (_session.Project is not null)
            _moduleChoice.AddItem(StudioSessionController.HumanUx8N(
                _session.Project.Module.DisplayName));
        _moduleChoice.Select(0);
        RefreshAuditUx9L();
        UpdateBoundsUx9L();
        UpdateActionsUx9L();
    }

    private void SelectModuleUx9L(int index)
    {
        _explicitSelection = index == 1 && _session.Project is not null;
        _session.CancelLiveAccessApprovalUx9L();
        _preview = null;
        _approval.ButtonPressed = false;
        if (!_explicitSelection)
        {
            _draft = null;
            SetStatusUx9L("Sélectionnez explicitement le module.", false);
            UpdateActionsUx9L();
            return;
        }
        _draft = _session.CreateLiveAccessDraftUx9L(explicitlySelected: true);
        if (_draft is null)
        {
            SetStatusUx9L(_session.LiveAccessLoadMessageUx9L ??
                          "Le profil d’accès est indisponible.", true);
            UpdateActionsUx9L();
            return;
        }
        PopulateDraftUx9L(_draft);
        SetStatusUx9L(_session.LiveAccessFileMissingUx9L
            ? "Profil initial chargé en mémoire · aucun fichier créé avant Enregistrer."
            : "Profil d’accès chargé · prévisualisez avant toute application.", false);
        UpdateActionsUx9L();
    }

    private void PopulateDraftUx9L(StudioUx9LAccessDraft draft)
    {
        _updating = true;
        _requiredAtRuntime.ButtonPressed = draft.RequiredAtRuntime;
        _sessionMinutes.Value = draft.SessionLifetimeMinutes;
        _inspectionHistory.Value = draft.InspectionHistoryLimit;
        _playerRefresh.Value = draft.PlayerRefreshSeconds;
        _auditRetention.Value = draft.AuditRetentionDays;
        _chatLength.Value = draft.ChatMessageLimit;
        _updating = false;
    }

    private StudioUx9LAccessDraft? ReadDraftUx9L() => _draft is null ? null : _draft with
    {
        RequiredAtRuntime = _requiredAtRuntime.ButtonPressed,
        SessionLifetimeMinutes = (int)_sessionMinutes.Value,
        InspectionHistoryLimit = (int)_inspectionHistory.Value,
        PlayerRefreshSeconds = (int)_playerRefresh.Value,
        AuditRetentionDays = (int)_auditRetention.Value,
        ChatMessageLimit = (int)_chatLength.Value
    };

    private void PreviewCurrentUx9L()
    {
        var draft = ReadDraftUx9L();
        if (draft is null)
        {
            SetStatusUx9L("Sélectionnez d’abord le module.", true);
            return;
        }
        _preview = _session.PreviewLiveAccessUx9L(draft);
        PopulatePreviewUx9L();
        SetStatusUx9L(_preview.Message, !_preview.Succeeded);
        UpdateActionsUx9L();
    }

    private void ApplyCurrentUx9L()
    {
        var draft = ReadDraftUx9L();
        if (draft is null) return;
        var result = _session.ApplyApprovedLiveAccessUx9L(draft);
        SetStatusUx9L(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            SelectModuleUx9L(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9L();
    }

    private void UndoUx9L()
    {
        var result = _session.UndoLiveAccessUx9L();
        SetStatusUx9L(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = false;
            _canRedo = true;
            SelectModuleUx9L(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9L();
    }

    private void RedoUx9L()
    {
        var result = _session.RedoLiveAccessUx9L();
        SetStatusUx9L(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            _canUndo = true;
            _canRedo = false;
            SelectModuleUx9L(1);
            WorkspaceChanged?.Invoke();
        }
        UpdateActionsUx9L();
    }

    private void SaveUx9L()
    {
        var result = _session.SaveLiveAccessUx9L();
        SetStatusUx9L(result.Message, !result.Succeeded);
        UpdateActionsUx9L();
    }

    private void ProbeUx9L()
    {
        var result = _session.RunLocalLiveAccessUx9L(_probeConfirmation.ButtonPressed,
            NetworkRole.Administrator);
        SetStatusUx9L(result.Message, !result.Succeeded);
        RefreshAuditUx9L();
        PopulatePreviewUx9L();
        UpdateActionsUx9L();
    }

    private void PopulatePreviewUx9L()
    {
        var probe = _session.LiveAccessProbeUx9L;
        _previewSummary.Text = _preview is null
            ? $"Sandbox · {probe.Status}."
            : $"{_preview.Message} · {string.Join(" · ", _preview.SemanticDiff)}\n" +
              $"Session {(probe.SessionActive ? "active" : "refusée")} · inspection {probe.InspectionSections}/6 · " +
              $"{probe.Players} joueur(s) à {probe.LatencyMs} ms · audit {(probe.AuditVerified ? "vérifié" : "invalide")} · " +
              $"{probe.ChatMessages} messages, dernier canal {HumanChannelUx9L(probe.LastChannel)}.";
    }

    private void RefreshAuditUx9L()
    {
        var audit = _session.LiveAccessAuditUx9L;
        var probe = _session.LiveAccessProbeUx9L;
        _auditSummary.Text = audit.Count == 0
            ? "Journal local · aucune entrée"
            : $"Journal local · {audit.Count} entrée(s) · chaîne {(probe.AuditVerified ? "vérifiée" : "à vérifier")} · " +
              $"ressources {(probe.ResourcesReleased ? "libérées" : "à vérifier")}.";
    }

    private void ConfigureEvidenceUx9L(string mode)
    {
        _updating = true;
        _requiredAtRuntime.ButtonPressed = true;
        _sessionMinutes.Value = 45;
        _inspectionHistory.Value = 120;
        _playerRefresh.Value = 3;
        _auditRetention.Value = 120;
        _chatLength.Value = 420;
        _approval.ButtonPressed = true;
        _probeConfirmation.ButtonPressed = true;
        _updating = false;
    }

    private void ConnectInvalidationUx9L()
    {
        foreach (var number in new[]
                 {
                     _sessionMinutes, _inspectionHistory, _playerRefresh, _auditRetention,
                     _chatLength
                 })
            number.ValueChanged += _ => InvalidateUx9L();
        _requiredAtRuntime.Toggled += _ => InvalidateUx9L();
        _approval.Toggled += _ => UpdateActionsUx9L();
        _probeConfirmation.Toggled += _ => UpdateActionsUx9L();
    }

    private void InvalidateUx9L()
    {
        if (_updating) return;
        _preview = null;
        _session.CancelLiveAccessApprovalUx9L();
        _approval.ButtonPressed = false;
        PopulatePreviewUx9L();
        UpdateActionsUx9L();
    }

    private void UpdateActionsUx9L()
    {
        var selected = _explicitSelection && _draft is not null;
        _previewButton.Disabled = !selected;
        _applyButton.Disabled = !selected || _preview is not { CanApply: true } ||
                                !_approval.ButtonPressed;
        _undoButton.Disabled = !_canUndo;
        _redoButton.Disabled = !_canRedo;
        _saveButton.Disabled = !_session.LiveAccessHasUnsavedChangesUx9L;
        _probeButton.Disabled = !selected || !_probeConfirmation.ButtonPressed ||
                                _session.LiveAccessHasUnsavedChangesUx9L;
    }

    private void ResetUx9L()
    {
        _draft = null;
        _preview = null;
        _explicitSelection = false;
        _canUndo = false;
        _canRedo = false;
        if (_previewSummary is not null) PopulatePreviewUx9L();
    }

    private SourceSnapshotUx9L CaptureSourceUx9L(StudioSessionController session) => new(
        session.Project, session.History, session.History?.State.Revision,
        session.History?.Journal.ToArray() ?? [], session.IsDirty, session.ActiveAreaId,
        session.SelectedEntityIds.ToArray(), session.SelectedEntityId,
        session.CaptureLiveAccessApprovalUx9L());

    private bool SourceUnchangedUx9L()
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
               source.CaptureLiveAccessApprovalUx9L() == snapshot.Approval;
    }

    private void RestoreSourceSessionUx9L()
    {
        if (_sourceSession is not null) _session = _sourceSession;
        _sourceSession = null;
        _sourceSnapshot = null;
        _sourceSessionRestored = true;
    }

    private void DeleteEvidenceUx9L()
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux9-l-ui-",
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
            SetStatusUx9L($"La copie temporaire n’a pas pu être libérée : {exception.Message}",
                true);
        }
    }

    private void UpdateBoundsUx9L()
    {
        if (_dialog is null) return;
        var size = GetViewportRect().Size;
        _dialog.Position = new Vector2(24, 18);
        _dialog.Size = new Vector2(Math.Max(960, size.X - 48), Math.Max(700, size.Y - 36));
    }

    private void SetStatusUx9L(string message, bool error)
    {
        var text = StudioSessionController.HumanUx8N(message);
        _status.Text = text;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger :
            StudioStyle.Muted);
        StatusChanged?.Invoke(text, error);
    }

    private static Control RouteCardUx9L(string route, string title, string detail)
    {
        var panel = StudioStyle.Panel("Ux9L" + route.Replace("-", string.Empty),
            StudioStyle.SurfaceRaised, 7, 1);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var stack = new VBoxContainer();
        panel.AddChild(stack);
        stack.AddChild(StudioStyle.Label($"{route} · {title}", 10, StudioStyle.Text,
            wrap: true));
        stack.AddChild(StudioStyle.Label(detail, 9, StudioStyle.Muted, wrap: true));
        return panel;
    }

    private static Control LabeledRowUx9L(string label, Control control)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(230, 0);
        row.AddChild(title);
        row.AddChild(control);
        return row;
    }

    private static OptionButton ChoiceUx9L(params string[] choices)
    {
        var value = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(320, 34)
        };
        foreach (var choice in choices) value.AddItem(choice);
        return value;
    }

    private static SpinBox NumberUx9L(GridContainer parent, string label, double min,
        double max, double value)
    {
        parent.AddChild(StudioStyle.Label(label, 10, StudioStyle.Muted));
        var number = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = 1,
            Value = value,
            AllowGreater = false,
            AllowLesser = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(280, 34)
        };
        parent.AddChild(number);
        return number;
    }

    private static CheckBox CheckUx9L(Control parent, string text)
    {
        var value = new CheckBox { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(value);
        return value;
    }

    private static Button ActionButtonUx9L(Control parent, string text, Action action,
        float width, bool primary = false)
    {
        var button = StudioStyle.Button(text, primary: primary, compact: true);
        button.CustomMinimumSize = new Vector2(width, 34);
        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static IEnumerable<string> DescendantTextUx9L(Node node)
    {
        if (node is Label label) yield return label.Text;
        if (node is Button button) yield return button.Text;
        if (node is OptionButton option)
            for (var index = 0; index < option.ItemCount; index++)
                yield return option.GetItemText(index);
        foreach (var child in node.GetChildren())
            foreach (var value in DescendantTextUx9L(child)) yield return value;
    }

    private static bool ContainsUuidUx9L(string text) => Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant);

    private static string HumanChannelUx9L(string channel) => channel == "impersonation"
        ? "incarnation" : channel;

    private sealed record SourceSnapshotUx9L(
        Forge.Domain.Projects.ProjectDefinition? Project,
        Forge.Application.Commands.ProjectCommandHistory? History,
        Forge.Application.Projects.ProjectRevision? Revision,
        IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
        bool Dirty,
        Forge.Domain.Areas.AreaId? Area,
        IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
        Forge.Domain.Entities.EntityId? Selected,
        StudioUx9LApprovalSnapshot Approval);
}
