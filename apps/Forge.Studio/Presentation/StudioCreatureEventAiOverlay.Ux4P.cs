using Forge.Domain.Creatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCreatureEventAiOverlayUx4P : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private Forge.Domain.Blueprints.BlueprintId? _evidenceBlueprintId;
    private StudioCreatureEventAiEvidenceSourceSnapshotUx4P? _sourceSnapshot;
    private bool _usesRealProfile;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _updating;
    private bool _applied;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioCreatureEventAiChoiceUx4P> _choices = [];
    private StudioCreatureEventAiDraftUx4P? _draft;
    private StudioCreatureEventAiPreviewUx4P? _previewResult;

    private readonly OptionButton _creature;
    private readonly CheckBox _enabled;
    private readonly CheckBox _required;
    private readonly TabContainer _tabs;
    private readonly LineEdit _asset;
    private readonly LineEdit _perceptionDetected;
    private readonly LineEdit _perceptionLost;
    private readonly LineEdit _attackDeclared;
    private readonly LineEdit _attackResolved;
    private readonly LineEdit _abilityTargeted;
    private readonly LineEdit _abilityImpacted;
    private readonly LineEdit _blocked;
    private readonly LineEdit _recovery;
    private readonly OptionButton _controllerKind;
    private readonly LineEdit _controllerDefinition;
    private readonly TextEdit _blackboard;
    private readonly LineEdit _actions;
    private readonly TextEdit _overrides;
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
    internal bool ShowsEvents =>
        _summary.Text.Contains("OnPerception", StringComparison.OrdinalIgnoreCase) &&
        _summary.Text.Contains("OnPhysicalAttacked", StringComparison.OrdinalIgnoreCase) &&
        _summary.Text.Contains("OnSpellCastAt", StringComparison.OrdinalIgnoreCase) &&
        _summary.Text.Contains("OnBlocked", StringComparison.OrdinalIgnoreCase) &&
        _summary.Text.Contains("OnStimulusDetected", StringComparison.OrdinalIgnoreCase) &&
        _summary.Text.Contains("OnMovementBlocked", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsController =>
        _summary.Text.Contains("IA par profil", StringComparison.OrdinalIgnoreCase) &&
        _summary.Text.Contains("Utility", StringComparison.OrdinalIgnoreCase) &&
        _summary.Text.Contains("ai.utility.warden", StringComparison.OrdinalIgnoreCase) &&
        _summary.Text.Contains("7 bindings", StringComparison.OrdinalIgnoreCase) &&
        _summary.Text.Contains("décision attack", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Événements et intelligence de créature", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Perception détectée et perdue", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Attaque physique déclarée et résolue", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Capacité ciblée et impactée", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Mouvement bloqué et récupération", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Contrôleur IA par profil", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aucun stimulus, attaque", StringComparison.OrdinalIgnoreCase) &&
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

    internal StudioCreatureEventAiOverlayUx4P(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        Name = "StudioCreatureEventAiOverlayUx4P";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var shade = new ColorRect { Color = new Color(0.012f, 0.018f, 0.03f, 0.94f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        var dialog = StudioStyle.Panel("CreatureEventAiDialogUx4P", StudioStyle.Chrome, 14, 2);
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
        titles.AddChild(StudioStyle.Label("ÉVÉNEMENTS ET INTELLIGENCE DE CRÉATURE · UX4-P", 17,
            StudioStyle.Accent, true));
        titles.AddChild(StudioStyle.Label(
            "Perception · attaque · capacité · blocage · contrôleur IA, avec aperçu sec canonique.",
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
        _enabled = new CheckBox { Text = "Activer Event/IA" };
        selection.AddChild(_enabled);
        _required = new CheckBox { Text = "Requis au runtime" };
        selection.AddChild(_required);
        _context = StudioStyle.Label("Sélection explicite requise.", 9, StudioStyle.Muted, wrap: true);
        _context.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        selection.AddChild(_context);
        root.AddChild(selection);

        var tabHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 405),
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            ClipContents = true,
        };
        root.AddChild(tabHost);
        _tabs = new TabContainer();
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabHost.AddChild(_tabs);

        var events = Tab(_tabs, "ÉVÉNEMENTS");
        _asset = Field(events, "Identité d’asset Event/IA", "asset.creature-event-ai.default");
        events.AddChild(StudioStyle.Label("Perception détectée et perdue", 10, StudioStyle.Accent, true));
        var perception = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _perceptionDetected = Field(perception, "Stimulus détecté", "OnPerceptionDetected");
        _perceptionLost = Field(perception, "Stimulus perdu", "OnPerceptionLost");
        events.AddChild(perception);
        events.AddChild(StudioStyle.Label("Attaque physique déclarée et résolue", 10, StudioStyle.Accent, true));
        var attack = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _attackDeclared = Field(attack, "Avant l’attaque", "BeforePhysicalAttacked");
        _attackResolved = Field(attack, "Après l’attaque", "OnPhysicalAttacked");
        events.AddChild(attack);
        events.AddChild(StudioStyle.Label("Capacité ciblée et impactée", 10, StudioStyle.Accent, true));
        var ability = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _abilityTargeted = Field(ability, "Avant ciblage", "BeforeAbilityTargeted");
        _abilityImpacted = Field(ability, "Après impact", "OnSpellCastAt");
        events.AddChild(ability);
        events.AddChild(StudioStyle.Label("Mouvement bloqué et récupération", 10, StudioStyle.Accent, true));
        var blocked = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _blocked = Field(blocked, "Handler de blocage", "OnBlocked");
        _recovery = Field(blocked, "Ordre humain", "Repath, Avoid, Teleport, Abort");
        events.AddChild(blocked);

        var controller = Tab(_tabs, "CONTRÔLEUR IA");
        controller.AddChild(StudioStyle.Label("Contrôleur IA par profil", 12, StudioStyle.Accent, true));
        var controllerRow = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var kindBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        kindBox.AddChild(StudioStyle.Label("Famille de contrôleur", 9, StudioStyle.Muted));
        _controllerKind = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var kind in Enum.GetValues<CreatureEventAiControllerKind>())
            _controllerKind.AddItem(HumanController(kind), (int)kind);
        kindBox.AddChild(_controllerKind);
        controllerRow.AddChild(kindBox);
        _controllerDefinition = Field(controllerRow, "Définition", "ai.default");
        controller.AddChild(controllerRow);
        _blackboard = Multi(controller, "Blackboard : clé|type|valeur|persistant", 145,
            "state|Text|idle|true\ntarget|Entity|entity.none|false");
        _actions = Field(controller, "Actions autorisées, dans l’ordre", "idle, patrol");
        _overrides = Multi(controller, "Overrides : clé=valeur", 80, "decision.threshold=0.65");

        var qualification = Tab(_tabs, "QUALIFICATION");
        qualification.AddChild(StudioStyle.Label("Projection Event/IA sans exécution", 12,
            StudioStyle.Accent, true));
        qualification.AddChild(StudioStyle.Label(
            "Le preview résout les sept bindings, la projection ForgeScript, le contrôleur RPG Kit et une " +
            "décision déterministe. Les UUID internes restent stables et invisibles.",
            10, StudioStyle.Text, wrap: true));
        qualification.AddChild(StudioStyle.Label(
            "La fondation, les profils UX4-D à UX4-O et tous les blueprints voisins sont préservés.",
            9, StudioStyle.Muted, wrap: true));

        _diagnostics = new VBoxContainer();
        root.AddChild(_diagnostics);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 135),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FitContent = false,
            SelectionEnabled = true,
        };
        root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Aperçu Domain/Application/RPG Kit uniquement. Aucun stimulus, attaque, capacité, mouvement, script, acteur ou réseau n’est exécuté.",
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
        RenderEmpty("Choisissez explicitement une créature dont la fondation existe.");
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
        if (cancelApproval) _session.CancelCreatureEventAiUx4P();
        _choices = _session.CreatureEventAiChoicesUx4P;
        _updating = true;
        _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label);
        _creature.Select(0);
        _updating = false;
        _draft = null;
        _previewResult = null;
        ClearControls();
        RenderEmpty(_choices.Count == 0 ? "Aucune créature éditable dans ce module." :
            "Choisissez explicitement une créature dont la fondation existe.");
        SetEditingEnabled(false);
        Visible = true;
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null &&
            ReferenceEquals(_session, _sourceSession)) return;
        if (ReferenceEquals(_session, _sourceSession)) _session.CancelCreatureEventAiUx4P();
        Visible = false;
        _draft = null;
        _previewResult = null;
        _applied = false;
        ClearControls();
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
        _session = _sourceSession;
        OpenBoundSession(cancelApproval: false);
    }

    private void SelectCreature(long index)
    {
        if (_updating) return;
        _session.CancelCreatureEventAiUx4P();
        _previewResult = null;
        _apply.Disabled = true;
        if (index <= 0 || index > _choices.Count)
        {
            _draft = null;
            ClearControls();
            RenderEmpty("Choisissez explicitement une créature dont la fondation existe.");
            SetEditingEnabled(false);
            return;
        }
        var choice = _choices[(int)index - 1];
        _draft = _session.CreateCreatureEventAiDraftUx4P(choice.BlueprintId);
        if (_draft is null)
        {
            ClearControls();
            RenderEmpty(choice.HasFoundation
                ? "Cette créature est protégée ou devenue indisponible."
                : "La fondation de cette créature doit être créée dans UX4-D avant Event/IA.");
            SetEditingEnabled(false);
            return;
        }
        Populate(_draft);
        _context.Text = $"{choice.Label} · fondation existante, voisins strictement préservés.";
        RenderEmpty("Modifiez les champs puis prévisualisez sans mutation.");
        SetEditingEnabled(true);
    }

    private void Populate(StudioCreatureEventAiDraftUx4P draft)
    {
        _updating = true;
        _enabled.ButtonPressed = draft.Enabled;
        _required.ButtonPressed = draft.RequiredAtRuntime;
        _asset.Text = draft.AssetId;
        _perceptionDetected.Text = draft.PerceptionDetectedHandler;
        _perceptionLost.Text = draft.PerceptionLostHandler;
        _attackDeclared.Text = draft.AttackDeclaredHandler;
        _attackResolved.Text = draft.AttackResolvedHandler;
        _abilityTargeted.Text = draft.AbilityTargetedHandler;
        _abilityImpacted.Text = draft.AbilityImpactedHandler;
        _blocked.Text = draft.BlockedHandler;
        _recovery.Text = draft.RecoveryOrder;
        _controllerKind.Select((int)draft.ControllerKind);
        _controllerDefinition.Text = draft.ControllerDefinitionId;
        _blackboard.Text = draft.Blackboard;
        _actions.Text = draft.AllowedActions;
        _overrides.Text = draft.Overrides;
        _updating = false;
        UpdateFieldStates();
    }

    private StudioCreatureEventAiDraftUx4P? ReadDraft()
    {
        if (_draft is null) return null;
        return _draft with
        {
            Enabled = _enabled.ButtonPressed,
            RequiredAtRuntime = _required.ButtonPressed,
            AssetId = _asset.Text,
            PerceptionDetectedHandler = _perceptionDetected.Text,
            PerceptionLostHandler = _perceptionLost.Text,
            AttackDeclaredHandler = _attackDeclared.Text,
            AttackResolvedHandler = _attackResolved.Text,
            AbilityTargetedHandler = _abilityTargeted.Text,
            AbilityImpactedHandler = _abilityImpacted.Text,
            BlockedHandler = _blocked.Text,
            RecoveryOrder = _recovery.Text,
            ControllerKind = (CreatureEventAiControllerKind)_controllerKind.GetSelectedId(),
            ControllerDefinitionId = _controllerDefinition.Text,
            Blackboard = _blackboard.Text,
            AllowedActions = _actions.Text,
            Overrides = _overrides.Text,
        };
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null) return;
        _previewResult = _session.PreviewCreatureEventAiUx4P(draft);
        RenderPreview(_previewResult);
        _apply.Disabled = !_previewResult.CanApply;
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
    }

    private void Apply()
    {
        var draft = ReadDraft();
        if (draft is null) return;
        var result = _session.ApplyApprovedCreatureEventAiUx4P(draft);
        _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        CreatureChanged?.Invoke();
        ReloadSelection(draft.BlueprintId);
    }

    private void Undo()
    {
        var selected = _draft?.BlueprintId;
        var result = _session.UndoCreatureEventAiUx4P();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        CreatureChanged?.Invoke();
        if (selected is { } id) ReloadSelection(id);
    }

    private void Redo()
    {
        var selected = _draft?.BlueprintId;
        var result = _session.RedoCreatureEventAiUx4P();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        CreatureChanged?.Invoke();
        if (selected is { } id) ReloadSelection(id);
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Succeeded ? "Workspace enregistré." : result.Message, !result.Succeeded);
    }

    private void ReloadSelection(Forge.Domain.Blueprints.BlueprintId blueprintId)
    {
        _choices = _session.CreatureEventAiChoicesUx4P;
        var index = _choices.ToList().FindIndex(item => item.BlueprintId == blueprintId);
        _updating = true;
        _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label);
        _creature.Select(index >= 0 ? index + 1 : 0);
        _updating = false;
        SelectCreature(index >= 0 ? index + 1 : 0);
    }

    private void RenderPreview(StudioCreatureEventAiPreviewUx4P result)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        foreach (var diagnostic in result.Diagnostics.Take(5))
            _diagnostics.AddChild(StudioStyle.Label(
                $"{diagnostic.HumanMessage} · {HumanPath(diagnostic.Path)}", 9,
                StudioStyle.Danger, wrap: true));
        _summary.Text = $"[b]{result.Message}[/b]\n" +
                        $"[color=#8ce0bd]{result.PatchSummary}[/color]\n" +
                        $"{result.PerceptionSummary}\n{result.CombatSummary}\n{result.BlockedSummary}\n" +
                        $"{result.ControllerSummary}\n{result.RuntimeSummary}\n" +
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
        _session.CancelCreatureEventAiUx4P();
        _previewResult = null;
        _apply.Disabled = true;
    }

    private void UpdateFieldStates()
    {
        var enabled = _draft is not null && _enabled.ButtonPressed;
        _required.Disabled = _draft is null || !_enabled.ButtonPressed;
        foreach (var field in LineFields()) field.Editable = enabled;
        _controllerKind.Disabled = !enabled;
        _blackboard.Editable = enabled;
        _overrides.Editable = enabled;
    }

    private void SetEditingEnabled(bool enabled)
    {
        _enabled.Disabled = !enabled;
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
        _enabled.ButtonPressed = false;
        _required.ButtonPressed = false;
        foreach (var field in LineFields()) field.Text = string.Empty;
        _controllerKind.Select(0);
        _blackboard.Text = string.Empty;
        _overrides.Text = string.Empty;
        _context.Text = "Sélection explicite requise.";
        _updating = false;
        UpdateFieldStates();
    }

    private void ConnectEvents()
    {
        _creature.ItemSelected += SelectCreature;
        _enabled.Toggled += value =>
        {
            if (!value)
            {
                _updating = true;
                _required.ButtonPressed = false;
                _updating = false;
            }
            UpdateFieldStates();
            InvalidatePreview();
        };
        _required.Toggled += _ => InvalidatePreview();
        _controllerKind.ItemSelected += _ => InvalidatePreview();
        foreach (var field in LineFields()) field.TextChanged += _ => InvalidatePreview();
        _blackboard.TextChanged += InvalidatePreview;
        _overrides.TextChanged += InvalidatePreview;
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
        _draft = StudioSessionController.ChangedCreatureEventAiDraftUx4P(_draft);
        Populate(_draft);
        _tabs.CurrentTab = mode.Contains("controller", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("qualification", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
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
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsEvents &&
                    ShowsController && ShowsHumanSurface && UsesIsolatedEvidence && EvidenceSourceUntouched &&
                    (_usesRealProfile ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:" +
                           $"events={ShowsEvents}:controller={ShowsController}:human={ShowsHumanSurface}:" +
                           $"isolated={UsesIsolatedEvidence}:source={EvidenceSourceUntouched}:" +
                           $"origin={_usesRealProfile ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4P_EVIDENCE_FAILED failure={_lastFailure}");
            Close();
            return false;
        }
        SetStatus("Preuve UX4-P isolée prête · aucune donnée source modifiée.", false);
        return true;
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource();
        if (snapshot is null) return false;
        StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-p-creature-event-ai") || _evidenceSession is null ||
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
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4P_UI_TRANSACTION_FAILED type={exception.GetType().Name} " +
                        $"message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreCreatureEventAiApprovalUx4P(snapshot.Approval);
        }
    }

    internal void CloseEvidence() => Close();

    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource();
        var evidence = _sourceSession.CreateUx4PEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux4p-evidence-" + Guid.NewGuid().ToString("N"));
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

    private StudioCreatureEventAiEvidenceSourceSnapshotUx4P? CaptureSource()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId, _sourceSession.CaptureCreatureEventAiApprovalUx4P());
    }

    private bool SourceMatches(StudioCreatureEventAiEvidenceSourceSnapshotUx4P snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history is not null &&
               history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureCreatureEventAiApprovalUx4P() == snapshot.Approval;
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4p-evidence-",
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
        yield return _asset;
        yield return _perceptionDetected; yield return _perceptionLost;
        yield return _attackDeclared; yield return _attackResolved;
        yield return _abilityTargeted; yield return _abilityImpacted;
        yield return _blocked; yield return _recovery;
        yield return _controllerDefinition; yield return _actions;
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

    private static string HumanController(CreatureEventAiControllerKind kind) => kind switch
    {
        CreatureEventAiControllerKind.StateMachine => "Machine à états",
        CreatureEventAiControllerKind.BehaviorTree => "Arbre de comportement",
        CreatureEventAiControllerKind.Utility => "Utilité pondérée",
        CreatureEventAiControllerKind.Script => "Script",
        _ => kind.ToString(),
    };

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
