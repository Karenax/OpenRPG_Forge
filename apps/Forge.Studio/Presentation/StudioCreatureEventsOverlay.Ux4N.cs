using Forge.Domain.Creatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCreatureEventsOverlayUx4N : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private Forge.Domain.Blueprints.BlueprintId? _evidenceBlueprintId;
    private StudioCreatureEventEvidenceSourceSnapshotUx4N? _sourceSnapshot;
    private bool _usesRealProfile;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _updating;
    private bool _applied;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioCreatureEventChoiceUx4N> _choices = [];
    private StudioCreatureEventDraftUx4N? _draft;
    private StudioCreatureEventPreviewUx4N? _previewResult;

    private readonly OptionButton _creature;
    private readonly CheckBox _required;
    private readonly TabContainer _tabs;
    private readonly CheckBox _combatEnabled;
    private readonly OptionButton _combatMode;
    private readonly LineEdit _combatHandler;
    private readonly CheckBox _inventoryEnabled;
    private readonly LineEdit _inventoryAccessed;
    private readonly LineEdit _itemTransferred;
    private readonly CheckBox _restEnabled;
    private readonly LineEdit _restHandler;
    private readonly CheckBox _heartbeatEnabled;
    private readonly LineEdit _heartbeatInterval;
    private readonly LineEdit _heartbeatBudget;
    private readonly CheckBox _suspendOutsideInterest;
    private readonly LineEdit _heartbeatHandler;
    private readonly TextEdit _customEvents;
    private readonly Label _context;
    private readonly VBoxContainer _diagnostics;
    private readonly RichTextLabel _summary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? CreatureChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _creature.Selected > 0 && _draft is not null;
    internal bool CanonicalPreviewReady => _previewResult?.CanApply == true;
    internal bool ShowsLifecycle => _summary.Text.Contains("Combat", StringComparison.OrdinalIgnoreCase) &&
                                    _summary.Text.Contains("inventaire", StringComparison.OrdinalIgnoreCase) &&
                                    _summary.Text.Contains("repos", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHeartbeat => _summary.Text.Contains("Heartbeat actif", StringComparison.OrdinalIgnoreCase) &&
                                    _summary.Text.Contains("1500 ms", StringComparison.OrdinalIgnoreCase) &&
                                    _summary.Text.Contains("budget 8", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsCustomEvents => _summary.Text.Contains("crypt.braziers_lit@2", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Événements avancés de créature", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Fin de tick ou de tour", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Inventaire consulté et objet transféré", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Repos terminé", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Heartbeat", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Événements utilisateur typés", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aucun script, acteur", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text) && !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null &&
                                          !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealProfileEvidence => _usesRealProfile;
    internal bool UsesCanonicalFallbackEvidence => _usesFallback;
    internal bool EvidencePreviewDryRun => _previewDryRun;
    internal bool EvidenceSourceUntouched => _sourceSnapshot is not null && SourceMatches(_sourceSnapshot);
    internal bool EvidenceResourcesReleased => _resourcesReleased;
    internal bool SourceSessionRestored => _sourceRestored;
    internal string LastEvidenceFailure => _lastFailure;

    internal StudioCreatureEventsOverlayUx4N(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        Name = "StudioCreatureEventsOverlayUx4N";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var shade = new ColorRect { Color = new Color(0.012f, 0.018f, 0.03f, 0.94f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        var dialog = StudioStyle.Panel("CreatureEventsDialogUx4N", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 52;
        dialog.OffsetTop = 28;
        dialog.OffsetRight = -52;
        dialog.OffsetBottom = -28;
        AddChild(dialog);
        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 7);
        dialog.AddChild(root);

        var header = new HBoxContainer();
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("ÉVÉNEMENTS AVANCÉS DE CRÉATURE · UX4-N", 17,
            StudioStyle.Accent, true));
        titles.AddChild(StudioStyle.Label(
            "Fin de combat · inventaire · repos · heartbeat · événements utilisateur, avec aperçu sec canonique.",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var selection = new HBoxContainer();
        selection.AddChild(StudioStyle.Label("Créature", 9, StudioStyle.Muted));
        _creature = new OptionButton { CustomMinimumSize = new Vector2(390, 30) };
        selection.AddChild(_creature);
        _required = new CheckBox { Text = "Profil requis au runtime" };
        selection.AddChild(_required);
        _context = StudioStyle.Label("Sélection explicite requise.", 9, StudioStyle.Muted, wrap: true);
        _context.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        selection.AddChild(_context);
        root.AddChild(selection);

        var tabHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 370),
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            ClipContents = true,
        };
        root.AddChild(tabHost);
        _tabs = new TabContainer();
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabHost.AddChild(_tabs);

        var lifecycle = Tab(_tabs, "CYCLE DE VIE");
        lifecycle.AddChild(StudioStyle.Label(
            "Fin de tick ou de tour · le Core ne suppose aucun modèle de rounds.",
            9, StudioStyle.Muted, wrap: true));
        _combatEnabled = new CheckBox { Text = "Activer la frontière de combat" };
        lifecycle.AddChild(_combatEnabled);
        var combatRow = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var modeBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        modeBox.AddChild(StudioStyle.Label("Frontière", 9, StudioStyle.Muted));
        _combatMode = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _combatMode.AddItem("Fin de tick de combat");
        _combatMode.AddItem("Fin de tour");
        modeBox.AddChild(_combatMode);
        combatRow.AddChild(modeBox);
        _combatHandler = Field(combatRow, "Handler ForgeScript", "OnCombatTickCompleted");
        lifecycle.AddChild(combatRow);
        _inventoryEnabled = new CheckBox { Text = "Inventaire consulté et objet transféré" };
        lifecycle.AddChild(_inventoryEnabled);
        var inventory = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _inventoryAccessed = Field(inventory, "Handler consultation", "OnInventoryAccessed");
        _itemTransferred = Field(inventory, "Handler transfert", "OnItemTransferred");
        lifecycle.AddChild(inventory);
        _restEnabled = new CheckBox { Text = "Repos terminé" };
        lifecycle.AddChild(_restEnabled);
        _restHandler = Field(lifecycle, "Handler de fin de repos", "OnRestCompleted");

        var heartbeat = Tab(_tabs, "HEARTBEAT");
        heartbeat.AddChild(StudioStyle.Label(
            "Scheduler serveur budgété · suspension hors intérêt · rattrapage limité à quatre dispatches.",
            9, StudioStyle.Muted, wrap: true));
        _heartbeatEnabled = new CheckBox { Text = "Heartbeat actif" };
        heartbeat.AddChild(_heartbeatEnabled);
        var scheduler = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _heartbeatInterval = Field(scheduler, "Cadence (ms)", "2000");
        _heartbeatBudget = Field(scheduler, "Budget (ms)", "8");
        _heartbeatHandler = Field(scheduler, "Handler ForgeScript", "OnHeartbeat");
        heartbeat.AddChild(scheduler);
        _suspendOutsideInterest = new CheckBox { Text = "Suspendre hors zone d’intérêt" };
        heartbeat.AddChild(_suspendOutsideInterest);

        var custom = Tab(_tabs, "ÉVÉNEMENTS UTILISATEUR");
        custom.AddChild(StudioStyle.Label(
            "Événements utilisateur typés · namespace|nom|version|required|champ:Type,...|handler.",
            9, StudioStyle.Muted, wrap: true));
        _customEvents = Multi(custom,
            "Types : String, Integer, Number, Boolean, EntityId, ItemId", 250,
            "crypt|braziers_lit|2|true|count:Integer,source:EntityId|OnBraziersLit");

        _diagnostics = new VBoxContainer();
        root.AddChild(_diagnostics);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 160),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FitContent = false,
            SelectionEnabled = true,
        };
        root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Aperçu Domain/Application uniquement. Aucun script, acteur, inventaire, repos ou réseau n’est exécuté.",
            9, StudioStyle.Muted, wrap: true));
        var footer = new HBoxContainer();
        _preview = StudioStyle.Button("Prévisualiser", primary: true, compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(button == _preview ? 132 : 104, 34);
            footer.AddChild(button);
        }
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        root.AddChild(footer);

        ConnectEvents();
        RenderEmpty("Choisissez explicitement une créature.");
        SetEditingEnabled(false);
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
        _session = _sourceSession;
        OpenBoundSession();
    }

    private void OpenBoundSession(bool cancelApproval = true)
    {
        if (cancelApproval) _session.CancelCreatureEventsUx4N();
        Visible = true;
        _previewResult = null;
        _draft = null;
        _applied = false;
        _choices = _session.CreatureEventChoicesUx4N;
        _updating = true;
        _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label);
        _creature.Select(0);
        _updating = false;
        ClearControls();
        _context.Text = _choices.Count == 0
            ? "Aucune créature éditable dans ce module."
            : $"{_choices.Count} créature(s) disponible(s) · aucun choix automatique.";
        RenderEmpty("Sélectionnez une créature pour charger ses événements.");
        SetEditingEnabled(false);
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null &&
            ReferenceEquals(_session, _sourceSession)) return;
        Visible = false;
        _session.CancelCreatureEventsUx4N();
        _draft = null;
        _previewResult = null;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
        else _session = _sourceSession;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        OpenBoundSession(cancelApproval: false);
    }

    private void SelectCreature(long index)
    {
        if (_updating) return;
        _session.CancelCreatureEventsUx4N();
        _previewResult = null;
        _apply.Disabled = true;
        if (index <= 0 || index > _choices.Count)
        {
            _draft = null;
            ClearControls();
            RenderEmpty("Sélection explicite requise.");
            SetEditingEnabled(false);
            return;
        }
        var choice = _choices[(int)index - 1];
        _draft = _session.CreateCreatureEventDraftUx4N(choice.BlueprintId);
        if (_draft is null)
        {
            ClearControls();
            RenderEmpty(choice.IsReadOnly
                ? "Cette créature installée est en lecture seule."
                : "Le profil existant ne peut pas être chargé sans normalisation implicite.");
            SetEditingEnabled(false);
            return;
        }
        Populate(_draft);
        _context.Text = $"{choice.Label} · dégâts et mort restent protégés et inchangés dans UX4-N.";
        RenderEmpty("Profil chargé. Prévisualisez avant toute application.");
        SetEditingEnabled(true);
    }

    private void Populate(StudioCreatureEventDraftUx4N draft)
    {
        _updating = true;
        _required.ButtonPressed = draft.RequiredAtRuntime;
        _combatEnabled.ButtonPressed = draft.CombatEnabled;
        _combatMode.Select((int)draft.CombatMode);
        _combatHandler.Text = draft.CombatHandler;
        _inventoryEnabled.ButtonPressed = draft.InventoryEnabled;
        _inventoryAccessed.Text = draft.InventoryAccessedHandler;
        _itemTransferred.Text = draft.ItemTransferredHandler;
        _restEnabled.ButtonPressed = draft.RestEnabled;
        _restHandler.Text = draft.RestHandler;
        _heartbeatEnabled.ButtonPressed = draft.HeartbeatEnabled;
        _heartbeatInterval.Text = draft.HeartbeatIntervalMilliseconds;
        _heartbeatBudget.Text = draft.HeartbeatBudgetMilliseconds;
        _suspendOutsideInterest.ButtonPressed = draft.SuspendOutsideInterest;
        _heartbeatHandler.Text = draft.HeartbeatHandler;
        _customEvents.Text = draft.CustomEvents;
        _updating = false;
        UpdateFieldStates();
    }

    private StudioCreatureEventDraftUx4N? ReadDraft()
    {
        if (_draft is null) return null;
        return _draft with
        {
            RequiredAtRuntime = _required.ButtonPressed,
            CombatEnabled = _combatEnabled.ButtonPressed,
            CombatMode = (CreatureCombatBoundaryMode)_combatMode.Selected,
            CombatHandler = _combatHandler.Text,
            InventoryEnabled = _inventoryEnabled.ButtonPressed,
            InventoryAccessedHandler = _inventoryAccessed.Text,
            ItemTransferredHandler = _itemTransferred.Text,
            RestEnabled = _restEnabled.ButtonPressed,
            RestHandler = _restHandler.Text,
            HeartbeatEnabled = _heartbeatEnabled.ButtonPressed,
            HeartbeatIntervalMilliseconds = _heartbeatInterval.Text,
            HeartbeatBudgetMilliseconds = _heartbeatBudget.Text,
            SuspendOutsideInterest = _suspendOutsideInterest.ButtonPressed,
            HeartbeatHandler = _heartbeatHandler.Text,
            CustomEvents = _customEvents.Text,
        };
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null) return;
        _previewResult = _session.PreviewCreatureEventsUx4N(draft);
        RenderPreview(_previewResult);
        _apply.Disabled = !_previewResult.CanApply;
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
    }

    private void Apply()
    {
        var draft = ReadDraft();
        if (draft is null) return;
        var result = _session.ApplyApprovedCreatureEventsUx4N(draft);
        _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        CreatureChanged?.Invoke();
        ReloadSelection(draft.BlueprintId);
    }

    private void Undo()
    {
        var selected = _draft?.BlueprintId;
        var result = _session.UndoCreatureEventsUx4N();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            CreatureChanged?.Invoke();
            if (selected is { } id) ReloadSelection(id);
        }
    }

    private void Redo()
    {
        var selected = _draft?.BlueprintId;
        var result = _session.RedoCreatureEventsUx4N();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            CreatureChanged?.Invoke();
            if (selected is { } id) ReloadSelection(id);
        }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Succeeded ? "Workspace enregistré." : result.Message, !result.Succeeded);
    }

    private void ReloadSelection(Forge.Domain.Blueprints.BlueprintId blueprintId)
    {
        _choices = _session.CreatureEventChoicesUx4N;
        var index = _choices.ToList().FindIndex(item => item.BlueprintId == blueprintId);
        _updating = true;
        _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label);
        _creature.Select(index >= 0 ? index + 1 : 0);
        _updating = false;
        SelectCreature(index >= 0 ? index + 1 : 0);
    }

    private void RenderPreview(StudioCreatureEventPreviewUx4N result)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        foreach (var diagnostic in result.Diagnostics.Take(5))
            _diagnostics.AddChild(StudioStyle.Label(
                $"{diagnostic.HumanMessage} · {HumanPath(diagnostic.Path)}", 9,
                StudioStyle.Danger, wrap: true));
        _summary.Text = $"[b]{result.Message}[/b]\n" +
                        $"[color=#8ce0bd]{result.PatchSummary}[/color]\n" +
                        $"Cycle : {result.LifecycleSummary}\n" +
                        $"Scheduler : {result.HeartbeatSummary}\n" +
                        $"Utilisateur : {result.CustomSummary}\n" +
                        $"{result.RuntimeSummary}\n" +
                        $"[color={(result.CanApply ? "#8ce0bd" : "#ff7777")}]Aperçu sans mutation : " +
                        $"{result.Succeeded.ToString().ToLowerInvariant()}[/color]";
    }

    private void RenderEmpty(string message)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        _summary.Text = message;
    }

    private void InvalidatePreview()
    {
        if (_updating) return;
        _session.CancelCreatureEventsUx4N();
        _previewResult = null;
        _apply.Disabled = true;
    }

    private void UpdateFieldStates()
    {
        var enabled = _draft is not null;
        _combatMode.Disabled = !enabled || !_combatEnabled.ButtonPressed;
        _combatHandler.Editable = enabled && _combatEnabled.ButtonPressed;
        _inventoryAccessed.Editable = enabled && _inventoryEnabled.ButtonPressed;
        _itemTransferred.Editable = enabled && _inventoryEnabled.ButtonPressed;
        _restHandler.Editable = enabled && _restEnabled.ButtonPressed;
        _heartbeatInterval.Editable = enabled;
        _heartbeatBudget.Editable = enabled;
        _heartbeatHandler.Editable = enabled;
        _suspendOutsideInterest.Disabled = !enabled;
        _customEvents.Editable = enabled;
    }

    private void SetEditingEnabled(bool enabled)
    {
        _required.Disabled = !enabled;
        _combatEnabled.Disabled = !enabled;
        _inventoryEnabled.Disabled = !enabled;
        _restEnabled.Disabled = !enabled;
        _heartbeatEnabled.Disabled = !enabled;
        _preview.Disabled = !enabled;
        _apply.Disabled = !enabled || !CanonicalPreviewReady;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
        UpdateFieldStates();
    }

    private void ClearControls()
    {
        _updating = true;
        _required.ButtonPressed = false;
        _combatEnabled.ButtonPressed = false;
        _combatMode.Select(0);
        _combatHandler.Text = string.Empty;
        _inventoryEnabled.ButtonPressed = false;
        _inventoryAccessed.Text = string.Empty;
        _itemTransferred.Text = string.Empty;
        _restEnabled.ButtonPressed = false;
        _restHandler.Text = string.Empty;
        _heartbeatEnabled.ButtonPressed = false;
        _heartbeatInterval.Text = string.Empty;
        _heartbeatBudget.Text = string.Empty;
        _suspendOutsideInterest.ButtonPressed = false;
        _heartbeatHandler.Text = string.Empty;
        _customEvents.Text = string.Empty;
        _updating = false;
        UpdateFieldStates();
    }

    private void ConnectEvents()
    {
        _creature.ItemSelected += SelectCreature;
        _required.Toggled += _ => InvalidatePreview();
        _combatEnabled.Toggled += _ => { UpdateFieldStates(); InvalidatePreview(); };
        _combatMode.ItemSelected += _ => InvalidatePreview();
        _inventoryEnabled.Toggled += _ => { UpdateFieldStates(); InvalidatePreview(); };
        _restEnabled.Toggled += _ => { UpdateFieldStates(); InvalidatePreview(); };
        _heartbeatEnabled.Toggled += _ => { UpdateFieldStates(); InvalidatePreview(); };
        _suspendOutsideInterest.Toggled += _ => InvalidatePreview();
        foreach (var field in LineFields()) field.TextChanged += _ => InvalidatePreview();
        _customEvents.TextChanged += InvalidatePreview;
        _preview.Pressed += Preview;
        _apply.Pressed += Apply;
        _undo.Pressed += Undo;
        _redo.Pressed += Redo;
        _save.Pressed += Save;
    }

    internal bool PrepareEvidence(string mode)
    {
        _lastFailure = "none";
        _previewResult = null;
        if ((_evidenceSession is not null || _evidenceRoot is not null) && !ReleaseEvidence())
        {
            _lastFailure = "release_previous";
            return false;
        }
        if (!TryOpenEvidence())
        {
            _lastFailure = _lastFailure == "none" ? "open" : _lastFailure;
            return false;
        }
        var index = _choices.ToList().FindIndex(item => item.BlueprintId == _evidenceBlueprintId);
        if (index < 0)
        {
            _lastFailure = "fixture_creature_missing";
            Close();
            return false;
        }
        _creature.Select(index + 1);
        SelectCreature(index + 1);
        if (_draft is null)
        {
            _lastFailure = "draft";
            Close();
            return false;
        }
        _draft = StudioSessionController.ChangedCreatureEventDraftUx4N(_draft);
        Populate(_draft);
        _tabs.CurrentTab = mode.Contains("heartbeat", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("custom", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        var project = _session.Project!;
        var history = _session.History!;
        var revision = history.State.Revision;
        var journal = history.Journal.ToArray();
        var dirty = _session.IsDirty;
        Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched;
        _summary.Text = $"[b]{(_usesRealProfile ? "PROFIL RÉEL CRYPTE" :
            "FALLBACK CANONIQUE TEMPORAIRE")}[/b] · copie isolée, source strictement inchangée.\n{_summary.Text}";
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsLifecycle &&
                    ShowsHeartbeat && ShowsCustomEvents && ShowsHumanSurface && UsesIsolatedEvidence &&
                    EvidenceSourceUntouched && (_usesRealProfile ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:" +
                           $"lifecycle={ShowsLifecycle}:heartbeat={ShowsHeartbeat}:custom={ShowsCustomEvents}:" +
                           $"human={ShowsHumanSurface}:isolated={UsesIsolatedEvidence}:" +
                           $"source={EvidenceSourceUntouched}:origin={_usesRealProfile ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4N_EVIDENCE_FAILED failure={_lastFailure}");
            Close();
            return false;
        }
        SetStatus("Preuve UX4-N isolée prête · aucune donnée source modifiée.", false);
        return true;
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource();
        if (snapshot is null) return false;
        StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-n-creature-events") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session;
            var original = transaction.Project;
            if (original is null || transaction.History is null) return false;
            var originalJson = ProjectJsonSerializer.Serialize(original);
            Apply();
            var appliedProject = transaction.Project;
            var applied = _applied && appliedProject is not null &&
                          ProjectJsonSerializer.Serialize(appliedProject) != originalJson;
            var appliedJson = appliedProject is null ? string.Empty : ProjectJsonSerializer.Serialize(appliedProject);
            if (applied) Undo();
            var undone = applied && transaction.Project is not null &&
                         ProjectJsonSerializer.Serialize(transaction.Project) == originalJson;
            if (undone) Redo();
            var redone = undone && transaction.Project is not null &&
                         ProjectJsonSerializer.Serialize(transaction.Project) == appliedJson;
            var saved = redone && transaction.Save().Succeeded;
            var persisted = false;
            if (saved && transaction.Session is { } workspace)
            {
                reopened = new();
                persisted = reopened.Open(workspace.RootDirectory).Succeeded &&
                            ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
            }
            var sourceUntouched = SourceMatches(snapshot);
            reopened?.Close();
            reopened = null;
            Close();
            return applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4N_UI_TRANSACTION_FAILED type={exception.GetType().Name} " +
                        $"message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreCreatureEventApprovalUx4N(snapshot.Approval);
        }
    }

    internal void CloseEvidence() => Close();

    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource();
        var evidence = _sourceSession.CreateUx4NEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux4n-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !session.Open(root).Succeeded)
            {
                session.Close();
                _resourcesReleased = DeleteEvidenceRoot(root);
                return false;
            }
            _sourceSnapshot = snapshot;
            _evidenceSession = session;
            _evidenceRoot = root;
            _evidenceBlueprintId = evidence.BlueprintId;
            _session = session;
            _usesRealProfile = evidence.UsesRealProfile;
            _usesFallback = evidence.UsesCanonicalFallback;
            _previewDryRun = false;
            _resourcesReleased = false;
            _sourceRestored = false;
            OpenBoundSession();
            return SourceMatches(snapshot) && (_usesRealProfile ^ _usesFallback);
        }
        catch (Exception exception)
        {
            _lastFailure = "open_exception:" + exception.GetType().Name;
            session.Close();
            _resourcesReleased = DeleteEvidenceRoot(root);
            _session = _sourceSession;
            _sourceRestored = true;
            return false;
        }
    }

    private StudioCreatureEventEvidenceSourceSnapshotUx4N? CaptureSource()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId, _sourceSession.CaptureCreatureEventApprovalUx4N());
    }

    private bool SourceMatches(StudioCreatureEventEvidenceSourceSnapshotUx4N snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history is not null &&
               history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureCreatureEventApprovalUx4N() == snapshot.Approval;
    }

    private bool ReleaseEvidence()
    {
        var session = _evidenceSession;
        var root = _evidenceRoot;
        _evidenceSession = null;
        _evidenceRoot = null;
        _sourceSnapshot = null;
        _evidenceBlueprintId = null;
        _session = _sourceSession;
        _usesRealProfile = false;
        _usesFallback = false;
        _previewDryRun = false;
        var released = true;
        try { session?.Close(); }
        catch { released = false; }
        var rootReleased = DeleteEvidenceRoot(root);
        if (!rootReleased) _evidenceRoot = root;
        _resourcesReleased = released && rootReleased;
        _sourceRestored = true;
        return _resourcesReleased;
    }

    private static bool DeleteEvidenceRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var full = Path.GetFullPath(root);
            var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4n-evidence-",
                    StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch
        {
            return false;
        }
    }

    private IEnumerable<LineEdit> LineFields()
    {
        yield return _combatHandler;
        yield return _inventoryAccessed;
        yield return _itemTransferred;
        yield return _restHandler;
        yield return _heartbeatInterval;
        yield return _heartbeatBudget;
        yield return _heartbeatHandler;
    }

    private void SetStatus(string message, bool error) => StatusChanged?.Invoke(message, error);

    private static LineEdit Field(Control parent, string label, string placeholder)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(field);
        parent.AddChild(box);
        return field;
    }

    private static TextEdit Multi(Control parent, string label, float height, string placeholder)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted, wrap: true));
        var field = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, height),
            PlaceholderText = placeholder,
            WrapMode = TextEdit.LineWrappingMode.None,
            ScrollFitContentHeight = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        box.AddChild(field);
        parent.AddChild(box);
        return field;
    }

    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true,
        };
        tabs.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(content);
        return content;
    }

    private static string HumanPath(string path)
    {
        var last = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "profil";
        return last.Length > 28 ? "champ concerné" : last;
    }

    private static IEnumerable<string> VisibleText(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label { Visible: true } label) yield return label.Text;
            if (child is Button { Visible: true } button) yield return button.Text;
            if (child is CheckBox { Visible: true } check) yield return check.Text;
            foreach (var value in VisibleText(child)) yield return value;
        }
    }

    private static bool ContainsGuid(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");
}
