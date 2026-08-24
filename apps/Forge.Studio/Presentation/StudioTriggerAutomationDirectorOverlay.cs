using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioTriggerAutomationDirectorOverlay : Control
{
    private const double MinimumIntervalSeconds = .05;
    private const double MaximumIntervalSeconds = 86_400;
    private const double IntervalStepSeconds = .05;
    private const int MinimumBudgetPerMinute = 1;
    private const int MaximumBudgetPerMinute = 100_000;

    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private StudioUx1AHEvidenceSourceSnapshot? _evidenceSourceSnapshot;
    private string? _evidenceTempRoot;
    private bool _lastEvidenceSourceUntouched = true;
    private bool _lastEvidenceSourceApprovalPreserved = true;
    private bool _lastEvidenceResourcesReleased = true;
    private bool _lastSourceRestored = true;
    private bool _lastPlaceholderResetEvidence = true;

    private readonly PanelContainer _dialog;
    private readonly OptionButton _trigger;
    private readonly Label _selection;
    private readonly Button _heartbeatTab;
    private readonly Button _customEventTab;
    private readonly Button _directorTab;
    private readonly VBoxContainer _heartbeatPanel;
    private readonly VBoxContainer _customEventPanel;
    private readonly VBoxContainer _directorPanel;

    private readonly CheckBox _schedulerEnabled;
    private readonly SpinBox _interval;
    private readonly SpinBox _budget;
    private readonly CheckBox _whileOccupied;
    private readonly CheckBox _aggregate;
    private readonly LineEdit _customNamespace;
    private readonly CheckBox _directorEnabled;
    private readonly CheckBox _manualEnter;
    private readonly CheckBox _manualExit;
    private readonly CheckBox _manualFire;
    private readonly CheckBox _requireTarget;
    private readonly CheckBox _audit;

    private readonly Label _summary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;

    private IReadOnlyList<StudioTriggerAutomationDirectorChoice> _choices = [];
    private StudioTriggerAutomationDirectorDraft? _draft;
    private bool _updating;
    private bool _applied;
    private bool _runtimePreviewReady;
    private bool _noLiveDispatch = true;
    private int _section;

    internal event Action<string, bool>? StatusChanged;

    internal event Action? TriggerChanged;

    internal bool IsOpen => Visible;

    internal int SectionCount => 3;

    internal int ChoiceCount => _choices.Count;

    internal bool HasExplicitSelection => _draft is not null && _trigger.Selected > 0;

    internal bool RuntimePreviewReady => _runtimePreviewReady;

    internal bool NoLiveDispatch => _noLiveDispatch;

    internal bool UsesIsolatedEvidence =>
        _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);

    internal bool EvidenceSourceUntouched => _evidenceSourceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;

    internal bool EvidenceSourceApprovalPreserved => _evidenceSourceSnapshot is { } snapshot
        ? _sourceSession.TriggerAutomationDirectorApprovalMatchesUx1AH(snapshot.Approval)
        : _lastEvidenceSourceApprovalPreserved;

    internal bool UnrepresentableCanonicalValuesBlocked =>
        CanRepresentSchedulerValues(1.75, 17) &&
        !CanRepresentSchedulerValues(.01, 17) &&
        !CanRepresentSchedulerValues(2.53, 17) &&
        !CanRepresentSchedulerValues(2, 100_001);

    internal bool EvidenceResourcesReleased =>
        _evidenceSession is null && _evidenceTempRoot is null && _lastEvidenceResourcesReleased;

    internal bool SourceSessionRestored =>
        ReferenceEquals(_session, _sourceSession) && _lastSourceRestored;

    internal bool PlaceholderResetEvidence => _lastPlaceholderResetEvidence;

    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ',
                _selection.Text,
                _summary.Text,
                _status.Text,
                _heartbeatTab.Text,
                _customEventTab.Text,
                _directorTab.Text);
            return HasExplicitSelection && SectionCount == 3 &&
                   text.Contains("Heartbeat", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Contrôle MJ", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucune commande Live Director", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("hash", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal StudioTriggerAutomationDirectorOverlay(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioTriggerAutomationDirectorOverlay";
        Visible = false;
        ZIndex = 451;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, .8f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        _dialog = StudioStyle.Panel("TriggerAutomationDirectorDialog", StudioStyle.Chrome, 12, 2);
        _dialog.ClipContents = true;
        _dialog.PropagateMaximumSize = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        margin.PropagateMaximumSize = true;
        _dialog.AddChild(margin);
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            PropagateMaximumSize = true,
        };
        root.AddThemeConstantOverride("separation", 9);
        margin.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("AUTOMATISATION DU TRIGGER", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Heartbeat, événement et contrôle MJ", 20));
        heading.AddChild(StudioStyle.Label(
            "ORF-TRG-009 · ORF-TRG-010 · ORF-TRG-018 · aperçu obligatoire avant application",
            10,
            StudioStyle.Muted,
            wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.TooltipText = "Fermer sans appliquer le brouillon · Échap";
        close.Pressed += Close;
        header.AddChild(close);

        var selector = new HBoxContainer();
        selector.AddThemeConstantOverride("separation", 8);
        root.AddChild(selector);
        selector.AddChild(StudioStyle.Label("Déclencheur à configurer", 10, StudioStyle.Muted));
        _trigger = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(_trigger);
        _trigger.ItemSelected += SelectTrigger;
        selector.AddChild(_trigger);
        _selection = StudioStyle.Label(
            "Choisissez explicitement un déclencheur doté d’un volume de base.",
            11,
            StudioStyle.Warning,
            wrap: true);
        root.AddChild(_selection);

        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        root.AddChild(tabs);
        _heartbeatTab = Tab("Heartbeat");
        _heartbeatTab.Pressed += () => ShowSection(0);
        tabs.AddChild(_heartbeatTab);
        _customEventTab = Tab("Événement personnalisé");
        _customEventTab.Pressed += () => ShowSection(1);
        tabs.AddChild(_customEventTab);
        _directorTab = Tab("Contrôle MJ");
        _directorTab.Pressed += () => ShowSection(2);
        tabs.AddChild(_directorTab);

        var bodyHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 310),
            CustomMaximumSize = new Vector2(-1, 520),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(bodyHost);
        var body = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(body);
        var panels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(panels);

        _heartbeatPanel = PanelColumn();
        panels.AddChild(_heartbeatPanel);
        _heartbeatPanel.AddChild(StudioStyle.Label("Rythme Heartbeat", 17));
        _heartbeatPanel.AddChild(StudioStyle.Label(
            "Déclarez une cadence, un budget par minute et la manière de regrouper les occupants. Le Studio vérifie la politique mais ne démarre aucun timer.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _schedulerEnabled = Check("Activer le Heartbeat de ce déclencheur");
        _heartbeatPanel.AddChild(_schedulerEnabled);
        var heartbeatGrid = Grid();
        _heartbeatPanel.AddChild(heartbeatGrid);
        _interval = AddField(heartbeatGrid, "Cadence", Number(
            MinimumIntervalSeconds,
            MaximumIntervalSeconds,
            IntervalStepSeconds,
            " s"));
        _budget = AddField(heartbeatGrid, "Budget", Number(
            MinimumBudgetPerMinute,
            MaximumBudgetPerMinute,
            1,
            " / min"));
        _whileOccupied = AddField(heartbeatGrid, "Occupation", Check("Seulement si le volume est occupé"));
        _aggregate = AddField(heartbeatGrid, "Regroupement", Check("Agréger les occupants par pulsation"));
        _heartbeatPanel.AddChild(StudioStyle.Label(
            "L’aperçu vérifie le refus sans occupant, le plafond de budget et un passage autorisé. La cadence reste une déclaration pour le scheduler runtime.",
            10,
            StudioStyle.Warning,
            wrap: true));

        _customEventPanel = PanelColumn();
        panels.AddChild(_customEventPanel);
        _customEventPanel.AddChild(StudioStyle.Label("Événement personnalisé", 17));
        _customEventPanel.AddChild(StudioStyle.Label(
            "Choisissez un espace de noms stable pour éviter les collisions entre modules et triggers.",
            10,
            StudioStyle.Muted,
            wrap: true));
        var eventGrid = Grid();
        _customEventPanel.AddChild(eventGrid);
        _customNamespace = AddField(eventGrid, "Espace nommé", Text("trigger.sanctuaire"));
        _customEventPanel.AddChild(StudioStyle.Label(
            "Format attendu : mots minuscules séparés par des points, par exemple trigger.sanctuaire.ronde. Aucun script n’est exécuté pendant l’aperçu.",
            10,
            StudioStyle.Warning,
            wrap: true));

        _directorPanel = PanelColumn();
        panels.AddChild(_directorPanel);
        _directorPanel.AddChild(StudioStyle.Label("Contrôle manuel par le MJ", 17));
        _directorPanel.AddChild(StudioStyle.Label(
            "Cette fiche définit ce que le serveur pourra autoriser. Elle n’ouvre pas de session MJ et ne contourne jamais les permissions Live Director.",
            10,
            StudioStyle.Muted,
            wrap: true));
        _directorEnabled = Check("Autoriser un contrôle MJ borné");
        _directorPanel.AddChild(_directorEnabled);
        var directorGrid = Grid();
        _directorPanel.AddChild(directorGrid);
        _manualEnter = AddField(directorGrid, "Entrée", Check("Autoriser une entrée manuelle"));
        _manualExit = AddField(directorGrid, "Sortie", Check("Autoriser une sortie manuelle"));
        _manualFire = AddField(directorGrid, "Déclenchement", Check("Autoriser un déclenchement manuel"));
        _requireTarget = AddField(directorGrid, "Cible", Check("Exiger une cible explicite"));
        _audit = AddField(directorGrid, "Traçabilité", Check("Exiger l’audit serveur"));
        _directorPanel.AddChild(StudioStyle.Label(
            "Sécurité imposée : au moins une action, cible explicite et audit serveur. L’aperçu appelle seulement le resolver pur ; aucune commande Live Director n’est envoyée.",
            10,
            StudioStyle.Warning,
            wrap: true));

        _summary = StudioStyle.Label(
            "Sélectionnez un déclencheur puis prévisualisez · aucune commande Live Director envoyée.",
            10,
            StudioStyle.Muted,
            wrap: true);
        root.AddChild(_summary);
        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 7);
        root.AddChild(footer);
        _preview = StudioStyle.Button("1 · Prévisualiser", compact: true);
        _preview.Pressed += Preview;
        footer.AddChild(_preview);
        _apply = StudioStyle.Button("2 · Appliquer", primary: true, compact: true);
        _apply.Disabled = true;
        _apply.Pressed += Apply;
        footer.AddChild(_apply);
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _undo = StudioStyle.Button("Annuler l’automatisation", compact: true);
        _undo.Disabled = true;
        _undo.Pressed += Undo;
        footer.AddChild(_undo);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _redo.Disabled = true;
        _redo.Pressed += Redo;
        footer.AddChild(_redo);
        _status = StudioStyle.Label("Prêt · sélection explicite requise", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        ConnectInvalidation();
        Resized += UpdateDialogBounds;
        UpdateDialogBounds();
        ShowSection(0);
        SetEditingEnabled(false);
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
        {
            Close();
        }
        _session = _sourceSession;
        OpenBoundSession();
    }

    private void OpenBoundSession()
    {
        _session.CancelTriggerAutomationDirectorUx1AH();
        _choices = _session.TriggerAutomationDirectorChoicesUx1AH;
        _trigger.Clear();
        _trigger.AddItem("Choisissez explicitement un déclencheur…");
        foreach (var choice in _choices)
        {
            _trigger.AddItem(choice.Label);
        }
        _trigger.Select(0);
        _draft = null;
        _applied = false;
        _runtimePreviewReady = false;
        _noLiveDispatch = true;
        _selection.Text = _choices.Count == 0
            ? "Aucun profil de déclencheur n’est disponible dans la zone active."
            : "Choisissez explicitement un déclencheur doté d’un volume de base.";
        _selection.AddThemeColorOverride(
            "font_color",
            _choices.Count == 0 ? StudioStyle.Danger : StudioStyle.Warning);
        _summary.Text =
            "Sélectionnez un déclencheur puis prévisualisez · aucune commande Live Director envoyée.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        _status.Text = "Prêt · sélection explicite requise";
        SetEditingEnabled(false);
        _undo.Disabled = true;
        _redo.Disabled = true;
        Visible = true;
        MoveToFront();
        ShowSection(0);
        UpdateDialogBounds();
        _trigger.GrabFocus();
    }

    internal void Close()
    {
        var sourceUntouched = _evidenceSourceSnapshot is not { } snapshot || SourceMatchesSnapshot(snapshot);
        var approvalPreserved = _evidenceSourceSnapshot is not { } approvalSnapshot ||
                                _sourceSession.TriggerAutomationDirectorApprovalMatchesUx1AH(
                                    approvalSnapshot.Approval);
        _session.CancelTriggerAutomationDirectorUx1AH();
        Visible = false;
        _draft = null;
        _applied = false;
        _runtimePreviewReady = false;
        _noLiveDispatch = true;
        var released = ReleaseIsolatedEvidenceSession();
        _lastEvidenceSourceUntouched = sourceUntouched;
        _lastEvidenceSourceApprovalPreserved = approvalPreserved;
        _lastEvidenceResourcesReleased = released;
        _lastSourceRestored = ReferenceEquals(_session, _sourceSession);
    }

    internal bool PrepareEvidence(string mode)
    {
        ResetForIsolatedEvidencePreparation();
        if (!TryOpenIsolatedEvidenceSession())
        {
            return false;
        }
        var index = _choices.ToList().FindIndex(item =>
            item.IsEvidence && item.HasFoundation && item.HasScheduler && item.HasCustomEvent && item.HasDirector);
        if (index < 0)
        {
            Close();
            return false;
        }
        _trigger.Select(index + 1);
        LoadChoice(index);
        if (_draft is null)
        {
            Close();
            return false;
        }
        ShowSection(mode.Contains("event", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("director", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("gm", StringComparison.OrdinalIgnoreCase) ||
            mode.Contains("mj", StringComparison.OrdinalIgnoreCase) ? 2 : 0);
        Preview();
        _selection.Text += " · session de preuve isolée";
        _summary.Text =
            "PREUVE ISOLÉE · le projet source reste strictement inchangé.\n" + _summary.Text;
        SetStatus("Preuve isolée prête · fermeture = suppression de la session temporaire", error: false);
        return !_apply.Disabled && HasExplicitSelection && RuntimePreviewReady && NoLiveDispatch &&
               ShowsHumanSurface && UsesIsolatedEvidence && EvidenceSourceUntouched &&
               EvidenceSourceApprovalPreserved;
    }

    internal bool RunUiTransactionEvidence()
    {
        var sourceSnapshot = CaptureSourceSnapshot();
        if (sourceSnapshot is null)
        {
            return false;
        }
        try
        {
            var ready = PrepareEvidence("ux1-ah-trigger-automation-director");
            if (!ready || !UsesIsolatedEvidence)
            {
                ResetForIsolatedEvidencePreparation();
                return false;
            }
            var transactionSession = _session;
            var originalProject = transactionSession.Project;
            var originalDirty = transactionSession.IsDirty;
            var transactionHistory = transactionSession.History;
            var originalRevision = transactionHistory?.State.Revision;
            var originalJournal = transactionHistory?.Journal.ToArray() ?? [];
            var originalCanUndo = transactionHistory?.CanUndo;
            var originalCanRedo = transactionHistory?.CanRedo;
            var dryRun = ready && originalProject is not null &&
                         ReferenceEquals(originalProject, transactionSession.Project) &&
                         transactionSession.IsDirty == originalDirty &&
                         ReferenceEquals(transactionHistory, transactionSession.History) &&
                         transactionHistory?.State.Revision == originalRevision &&
                         (transactionHistory?.Journal ?? []).SequenceEqual(originalJournal) &&
                         transactionHistory?.CanUndo == originalCanUndo &&
                         transactionHistory?.CanRedo == originalCanRedo &&
                         UsesIsolatedEvidence && SourceMatchesSnapshot(sourceSnapshot);
            Apply();
            var appliedProject = transactionSession.Project;
            var applied = dryRun && _applied && !ReferenceEquals(originalProject, appliedProject);
            if (applied)
            {
                Undo();
            }
            var undone = applied && ReferenceEquals(originalProject, transactionSession.Project) &&
                         transactionSession.IsDirty == originalDirty;
            if (undone)
            {
                Redo();
            }
            var redone = undone && ReferenceEquals(appliedProject, transactionSession.Project);
            if (redone)
            {
                Undo();
            }
            var restored = redone && ReferenceEquals(originalProject, transactionSession.Project) &&
                           transactionSession.IsDirty == originalDirty;
            _trigger.Select(0);
            SelectTrigger(0);
            var placeholderReset = _trigger.Selected == 0 && _draft is null && !_applied &&
                                   !_runtimePreviewReady && _noLiveDispatch &&
                                   _apply.Disabled && _preview.Disabled && _undo.Disabled && _redo.Disabled &&
                                   _summary.Text.StartsWith("Sélectionnez un déclencheur", StringComparison.Ordinal) &&
                                   _status.Text == "Prêt · sélection explicite requise";
            _lastPlaceholderResetEvidence = placeholderReset;
            var sourceUntouched = SourceMatchesSnapshot(sourceSnapshot);
            Close();
            return dryRun && applied && undone && redone && restored && placeholderReset && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        {
            _lastPlaceholderResetEvidence = false;
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AH_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            ResetForIsolatedEvidencePreparation();
            return false;
        }
    }

    private void ResetForIsolatedEvidencePreparation()
    {
        if (_evidenceSession is not null)
        {
            _session = _evidenceSession;
            Close();
        }
        else if (_evidenceTempRoot is not null)
        {
            _lastEvidenceResourcesReleased = ReleaseIsolatedEvidenceSession();
        }

        _session = _sourceSession;
        Visible = false;
        _draft = null;
        _applied = false;
        _runtimePreviewReady = false;
        _noLiveDispatch = true;
        _apply.Disabled = true;
        _preview.Disabled = true;
        _undo.Disabled = true;
        _redo.Disabled = true;
    }

    private bool TryOpenIsolatedEvidenceSession()
    {
        var snapshot = CaptureSourceSnapshot();
        var evidenceProject = _sourceSession.CreateUx1AHEvidenceProject();
        if (snapshot is null || evidenceProject is null)
        {
            return false;
        }

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "openrpg-forge-studio-ux1ah-evidence-" + Guid.NewGuid().ToString("N"));
        var evidenceSession = new StudioSessionController();
        try
        {
            var created = ProjectSession.Create(tempRoot, evidenceProject);
            if (!created.Succeeded ||
                !evidenceSession.Open(tempRoot, snapshot.ActiveAreaId?.ToString()).Succeeded)
            {
                evidenceSession.Close();
                _lastEvidenceResourcesReleased = DeleteEvidenceRoot(tempRoot);
                _evidenceTempRoot = _lastEvidenceResourcesReleased ? null : tempRoot;
                _session = _sourceSession;
                _lastSourceRestored = true;
                return false;
            }

            _evidenceSourceSnapshot = snapshot;
            _evidenceSession = evidenceSession;
            _evidenceTempRoot = tempRoot;
            _session = evidenceSession;
            _lastEvidenceResourcesReleased = false;
            _lastSourceRestored = false;
            OpenBoundSession();
            var ready = _choices.Any(item => item.IsEvidence && item.HasFoundation) &&
                        SourceMatchesSnapshot(snapshot);
            if (!ready)
            {
                Close();
            }
            return ready;
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AH_EVIDENCE_FAILED type={exception.GetType().Name} message={exception.Message}");
            evidenceSession.Close();
            var released = DeleteEvidenceRoot(tempRoot);
            _evidenceSourceSnapshot = null;
            _evidenceSession = null;
            _evidenceTempRoot = released ? null : tempRoot;
            _session = _sourceSession;
            _lastEvidenceResourcesReleased = released;
            _lastSourceRestored = true;
            return false;
        }
    }

    private StudioUx1AHEvidenceSourceSnapshot? CaptureSourceSnapshot()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        if (project is null || history is null)
        {
            return null;
        }
        return new StudioUx1AHEvidenceSourceSnapshot(
            project,
            history,
            history.State.Revision,
            history.Journal.ToArray(),
            _sourceSession.IsDirty,
            history.CanUndo,
            history.CanRedo,
            _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId,
            _sourceSession.CaptureTriggerAutomationDirectorApprovalUx1AH());
    }

    private bool SourceMatchesSnapshot(StudioUx1AHEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) &&
               history is not null &&
               history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.TriggerAutomationDirectorApprovalMatchesUx1AH(snapshot.Approval);
    }

    private bool ReleaseIsolatedEvidenceSession()
    {
        var evidenceSession = _evidenceSession;
        var tempRoot = _evidenceTempRoot;
        _evidenceSession = null;
        _evidenceTempRoot = null;
        _evidenceSourceSnapshot = null;
        _session = _sourceSession;
        var released = true;
        try
        {
            evidenceSession?.Close();
        }
        catch (Exception exception)
        {
            released = false;
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AH_EVIDENCE_RELEASE_FAILED type={exception.GetType().Name} message={exception.Message}");
        }
        var rootReleased = DeleteEvidenceRoot(tempRoot);
        if (!rootReleased)
        {
            _evidenceTempRoot = tempRoot;
        }
        return released && rootReleased;
    }

    private static bool DeleteEvidenceRoot(string? tempRoot)
    {
        if (tempRoot is null)
        {
            return true;
        }
        try
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            return !Directory.Exists(tempRoot);
        }
        catch (Exception exception)
        {
            GD.PrintErr(
                $"OPENRPG_FORGE_STUDIO_UX1AH_EVIDENCE_DELETE_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _ExitTree()
    {
        if (_evidenceSession is not null || _evidenceTempRoot is not null)
        {
            Close();
        }
    }

    private void SelectTrigger(long index)
    {
        if (_updating)
        {
            return;
        }
        if (index <= 0)
        {
            _session.CancelTriggerAutomationDirectorUx1AH();
            _draft = null;
            _runtimePreviewReady = false;
            _noLiveDispatch = true;
            _applied = false;
            SetEditingEnabled(false);
            _apply.Disabled = true;
            _undo.Disabled = true;
            _redo.Disabled = true;
            _selection.Text = "Choisissez explicitement un déclencheur doté d’un volume de base.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Warning);
            _summary.Text =
                "Sélectionnez un déclencheur puis prévisualisez · aucune commande Live Director envoyée.";
            _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
            _status.Text = "Prêt · sélection explicite requise";
            _status.AddThemeColorOverride("font_color", StudioStyle.Muted);
            StatusChanged?.Invoke(_status.Text, false);
            return;
        }
        LoadChoice((int)index - 1);
    }

    private void LoadChoice(int choiceIndex)
    {
        _session.CancelTriggerAutomationDirectorUx1AH();
        _draft = null;
        _runtimePreviewReady = false;
        _noLiveDispatch = true;
        _applied = false;
        _apply.Disabled = true;
        _undo.Disabled = true;
        _redo.Disabled = true;
        if (choiceIndex < 0 || choiceIndex >= _choices.Count)
        {
            SetEditingEnabled(false);
            return;
        }
        var choice = _choices[choiceIndex];
        var draft = _session.CreateTriggerAutomationDirectorDraftUx1AH(choice.ProfileId);
        if (draft is null)
        {
            _selection.Text = choice.HasFoundation
                ? "Ce déclencheur a changé. Fermez puis rouvrez la fiche."
                : "Volume de base requis · configurez d’abord la forme, le filtre et les événements avec l’outil Déclencheur.";
            _selection.AddThemeColorOverride("font_color", StudioStyle.Danger);
            _summary.Text = "Aucune modification possible sans Foundation canonique.";
            _summary.AddThemeColorOverride("font_color", StudioStyle.Danger);
            SetEditingEnabled(false);
            return;
        }

        if (!CanPopulateDraftExactly(draft))
        {
            _selection.Text = choice.Label;
            _selection.AddThemeColorOverride("font_color", StudioStyle.Danger);
            _summary.Text =
                $"Valeurs canoniques conservées sans réécriture : cadence {draft.IntervalSeconds:0.###############} s · budget {draft.BudgetPerMinute}/min. " +
                "Cette fiche bornée ne peut pas les représenter exactement ; aucun champ n’a été chargé.";
            _summary.AddThemeColorOverride("font_color", StudioStyle.Danger);
            SetEditingEnabled(false);
            SetStatus(
                "Profil non chargé · ajustez sa cadence ou son budget avec un outil compatible avec ces valeurs.",
                error: true);
            return;
        }

        _draft = draft;
        Populate(draft);
        _selection.Text = choice.Label;
        _selection.AddThemeColorOverride("font_color", StudioStyle.Success);
        _summary.Text =
            "Brouillon chargé · prévisualisez la politique · aucune commande Live Director envoyée.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
        SetEditingEnabled(true);
        SetStatus("Brouillon local · aucun document ni session live modifié", error: false);
    }

    private void Populate(StudioTriggerAutomationDirectorDraft draft)
    {
        _updating = true;
        _schedulerEnabled.ButtonPressed = draft.SchedulerEnabled;
        _interval.Value = draft.IntervalSeconds;
        _budget.Value = draft.BudgetPerMinute;
        _whileOccupied.ButtonPressed = draft.WhileOccupiedOnly;
        _aggregate.ButtonPressed = draft.AggregateOccupants;
        _customNamespace.Text = draft.CustomEventNamespace;
        _directorEnabled.ButtonPressed = draft.DirectorEnabled;
        _manualEnter.ButtonPressed = draft.AllowManualEnter;
        _manualExit.ButtonPressed = draft.AllowManualExit;
        _manualFire.ButtonPressed = draft.AllowManualFire;
        _requireTarget.ButtonPressed = draft.RequireTarget;
        _audit.ButtonPressed = draft.Audit;
        _updating = false;
    }

    private StudioTriggerAutomationDirectorDraft? ReadDraft() => _draft is null
        ? null
        : _draft with
        {
            SchedulerEnabled = _schedulerEnabled.ButtonPressed,
            IntervalSeconds = _interval.Value,
            BudgetPerMinute = (int)Math.Round(_budget.Value),
            WhileOccupiedOnly = _whileOccupied.ButtonPressed,
            AggregateOccupants = _aggregate.ButtonPressed,
            CustomEventNamespace = _customNamespace.Text,
            DirectorEnabled = _directorEnabled.ButtonPressed,
            AllowManualEnter = _manualEnter.ButtonPressed,
            AllowManualExit = _manualExit.ButtonPressed,
            AllowManualFire = _manualFire.ButtonPressed,
            RequireTarget = _requireTarget.ButtonPressed,
            Audit = _audit.ButtonPressed,
        };

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            return;
        }
        var result = _session.PreviewTriggerAutomationDirectorUx1AH(draft);
        _runtimePreviewReady = result.Succeeded && result.OccupancyPolicyHonored &&
                               result.BudgetBlocked && result.ScheduledAllowed && result.NamespaceValid &&
                               result.ManualFirePolicyHonored && result.AuditRecorded;
        _noLiveDispatch = !result.LiveDispatchAttempted;
        _apply.Disabled = !result.CanApply || !_runtimePreviewReady || !_noLiveDispatch;
        _summary.Text = result.Succeeded ? result.Summary : result.Message;
        _summary.AddThemeColorOverride(
            "font_color",
            result.Succeeded ? StudioStyle.Success : StudioStyle.Danger);
        SetStatus(result.Message, error: !result.Succeeded);
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedTriggerAutomationDirectorUx1AH();
        SetStatus(result.Message, error: !result.Succeeded);
        if (!result.Succeeded)
        {
            _apply.Disabled = true;
            _runtimePreviewReady = false;
            return;
        }
        _applied = true;
        _apply.Disabled = true;
        _preview.Disabled = true;
        _undo.Disabled = false;
        _redo.Disabled = true;
        SetEditingEnabled(false);
        _summary.Text =
            "Appliqué · seuls Heartbeat, événement personnalisé et autorisations MJ ont changé. Aucun appel live n’a été émis.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Success);
        TriggerChanged?.Invoke();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            _applied = false;
            _undo.Disabled = true;
            _redo.Disabled = false;
            TriggerChanged?.Invoke();
        }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, error: !result.Succeeded);
        if (result.Succeeded)
        {
            _applied = true;
            _undo.Disabled = false;
            _redo.Disabled = true;
            TriggerChanged?.Invoke();
        }
    }

    private void ShowSection(int section)
    {
        _section = Math.Clamp(section, 0, 2);
        _heartbeatPanel.Visible = _section == 0;
        _customEventPanel.Visible = _section == 1;
        _directorPanel.Visible = _section == 2;
        ApplyTabStyle(_heartbeatTab, _section == 0);
        ApplyTabStyle(_customEventTab, _section == 1);
        ApplyTabStyle(_directorTab, _section == 2);
    }

    private void ConnectInvalidation()
    {
        _customNamespace.TextChanged += _ => InvalidatePreview();
        _interval.ValueChanged += _ => InvalidatePreview();
        _budget.ValueChanged += _ => InvalidatePreview();
        foreach (var check in new[]
                 {
                     _schedulerEnabled, _whileOccupied, _aggregate, _directorEnabled,
                     _manualEnter, _manualExit, _manualFire, _requireTarget, _audit,
                 })
        {
            check.Toggled += _ => InvalidatePreview();
        }
    }

    private void InvalidatePreview()
    {
        if (_updating || _draft is null || _applied)
        {
            return;
        }
        _session.CancelTriggerAutomationDirectorUx1AH();
        _runtimePreviewReady = false;
        _noLiveDispatch = true;
        _apply.Disabled = true;
        _preview.Disabled = false;
        _summary.Text =
            "Aperçu à recalculer · aucun changement appliqué · aucune commande Live Director envoyée.";
        _summary.AddThemeColorOverride("font_color", StudioStyle.Muted);
    }

    private void SetEditingEnabled(bool enabled)
    {
        _interval.Editable = enabled;
        _budget.Editable = enabled;
        _customNamespace.Editable = enabled;
        foreach (var check in new[]
                 {
                     _schedulerEnabled, _whileOccupied, _aggregate, _directorEnabled,
                     _manualEnter, _manualExit, _manualFire, _requireTarget, _audit,
                 })
        {
            check.Disabled = !enabled;
        }
        _heartbeatTab.Disabled = !enabled;
        _customEventTab.Disabled = !enabled;
        _directorTab.Disabled = !enabled;
        _preview.Disabled = !enabled;
        if (!enabled)
        {
            _apply.Disabled = true;
        }
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Success);
        StatusChanged?.Invoke(message, error);
    }

    private void UpdateDialogBounds()
    {
        var scale = Size.X >= 3200 ? 1.72f : Size.X >= 2300 ? 1.24f : 1f;
        var width = Math.Clamp((Size.X - 56) / scale, 860, 1100);
        var height = Math.Clamp((Size.Y - 72) / scale, 640, 800);
        var renderedWidth = width * scale;
        var renderedHeight = height * scale;
        _dialog.Scale = Vector2.One * scale;
        _dialog.CustomMaximumSize = new Vector2(width, height);
        _dialog.Position = new Vector2(
            (Size.X - renderedWidth) * .5f,
            Math.Max(24, (Size.Y - renderedHeight) * .45f));
        _dialog.Size = new Vector2(width, height);
    }

    private static VBoxContainer PanelColumn()
    {
        var panel = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeConstantOverride("separation", 8);
        return panel;
    }

    private static Button Tab(string text)
    {
        var button = StudioStyle.Button(text, compact: true);
        button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return button;
    }

    private static void ApplyTabStyle(Button button, bool selected)
    {
        button.AddThemeStyleboxOverride(
            "normal",
            StudioStyle.Box(
                selected ? StudioStyle.Selection : StudioStyle.SurfaceRaised,
                selected ? StudioStyle.Success : StudioStyle.Border,
                6,
                selected ? 2 : 1));
    }

    private static GridContainer Grid() => new()
    {
        Columns = 2,
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
    };

    private static T AddField<T>(GridContainer grid, string label, T control) where T : Control
    {
        var title = StudioStyle.Label(label, 10, StudioStyle.Muted);
        title.CustomMinimumSize = new Vector2(180, 0);
        title.VerticalAlignment = VerticalAlignment.Center;
        grid.AddChild(title);
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        grid.AddChild(control);
        return control;
    }

    private static LineEdit Text(string placeholder)
    {
        var value = new LineEdit { PlaceholderText = placeholder };
        StudioStyle.ApplyLineEdit(value);
        return value;
    }

    private static SpinBox Number(double min, double max, double step, string suffix)
    {
        var value = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Suffix = suffix,
            AllowGreater = false,
            AllowLesser = false,
        };
        StudioStyle.ApplySpinBox(value);
        return value;
    }

    private static bool CanPopulateDraftExactly(StudioTriggerAutomationDirectorDraft draft) =>
        !draft.SchedulerEnabled ||
        CanRepresentSchedulerValues(draft.IntervalSeconds, draft.BudgetPerMinute);

    private static bool CanRepresentSchedulerValues(double intervalSeconds, int budgetPerMinute) =>
        double.IsFinite(intervalSeconds) &&
        intervalSeconds is >= MinimumIntervalSeconds and <= MaximumIntervalSeconds &&
        IsStepAligned(intervalSeconds, IntervalStepSeconds) &&
        budgetPerMinute is >= MinimumBudgetPerMinute and <= MaximumBudgetPerMinute;

    private static bool IsStepAligned(double value, double step)
    {
        var units = value / step;
        return Math.Abs(units - Math.Round(units)) <= 1e-9 * Math.Max(1, Math.Abs(units));
    }

    private static CheckBox Check(string text)
    {
        var value = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        value.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        value.AddThemeColorOverride("font_color", StudioStyle.Text);
        return value;
    }
}
