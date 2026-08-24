using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCreatureDamageValidationOverlayUx4O : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private Forge.Domain.Blueprints.BlueprintId? _evidenceBlueprintId;
    private StudioCreatureDamageEvidenceSourceSnapshotUx4O? _sourceSnapshot;
    private bool _usesRealProfile;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _updating;
    private bool _applied;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioCreatureDamageChoiceUx4O> _choices = [];
    private StudioCreatureDamageDraftUx4O? _draft;
    private StudioCreatureDamagePreviewUx4O? _previewResult;

    private readonly OptionButton _creature;
    private readonly CheckBox _enabled;
    private readonly TabContainer _tabs;
    private readonly LineEdit _beforeDamage;
    private readonly LineEdit _damageApplied;
    private readonly LineEdit _died;
    private readonly LineEdit _budget;
    private readonly LineEdit _ratio;
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
    internal bool ShowsDamageLifecycle =>
        _summary.Text.Contains("BeforeDamageBounded", StringComparison.OrdinalIgnoreCase) &&
        _summary.Text.Contains("OnDamageCommitted", StringComparison.OrdinalIgnoreCase) &&
        _summary.Text.Contains("4 ms", StringComparison.OrdinalIgnoreCase) &&
        _summary.Text.Contains("0.25", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsDeath =>
        _summary.Text.Contains("OnEntityDeathComplete", StringComparison.OrdinalIgnoreCase) &&
        _summary.Text.Contains("killer", StringComparison.OrdinalIgnoreCase) &&
        _summary.Text.Contains("loot", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsValidation =>
        _summary.Text.Contains("Validation ciblée", StringComparison.OrdinalIgnoreCase) &&
        _summary.Text.Contains("0 diagnostic", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Dégâts, mort et validation créature", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Avant les dégâts", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Dégâts appliqués", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Mort transactionnelle", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Validation complète de la créature", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aucun dégât, acteur", StringComparison.OrdinalIgnoreCase) &&
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

    internal StudioCreatureDamageValidationOverlayUx4O(StudioSessionController session)
    {
        _sourceSession = session;
        _session = session;
        Name = "StudioCreatureDamageValidationOverlayUx4O";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var shade = new ColorRect { Color = new Color(0.012f, 0.018f, 0.03f, 0.94f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        var dialog = StudioStyle.Panel("CreatureDamageValidationDialogUx4O", StudioStyle.Chrome, 14, 2);
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
        titles.AddChild(StudioStyle.Label("DÉGÂTS, MORT ET VALIDATION CRÉATURE · UX4-O", 17,
            StudioStyle.Accent, true));
        titles.AddChild(StudioStyle.Label(
            "Cycle transactionnel borné · payload de mort · validation complète, avec aperçu sec canonique.",
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
        _enabled = new CheckBox { Text = "Activer dégâts et mort transactionnels" };
        selection.AddChild(_enabled);
        _context = StudioStyle.Label("Sélection explicite requise.", 9, StudioStyle.Muted, wrap: true);
        _context.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        selection.AddChild(_context);
        root.AddChild(selection);

        var tabHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 350),
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            ClipContents = true,
        };
        root.AddChild(tabHost);
        _tabs = new TabContainer();
        _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabHost.AddChild(_tabs);

        var damage = Tab(_tabs, "DÉGÂTS & MORT");
        damage.AddChild(StudioStyle.Label(
            "Avant les dégâts, la modification reste bornée par un budget et un ratio explicites.",
            9, StudioStyle.Muted, wrap: true));
        var handlers = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _beforeDamage = Field(handlers, "Avant les dégâts", "BeforeDamage");
        _damageApplied = Field(handlers, "Dégâts appliqués", "OnDamaged");
        _died = Field(handlers, "Mort transactionnelle", "OnDeath");
        damage.AddChild(handlers);
        var bounds = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _budget = Field(bounds, "Budget BeforeDamage (ms)", "4");
        _ratio = Field(bounds, "Ajustement maximal (0 à 1)", "0.5");
        damage.AddChild(bounds);
        damage.AddChild(StudioStyle.Label(
            "Le payload de mort transporte killer, contributeurs, cause, position, loot et politique de drop.",
            9, StudioStyle.Muted, wrap: true));

        var validation = Tab(_tabs, "VALIDATION CRÉATURE");
        validation.AddChild(StudioStyle.Label("Validation complète de la créature", 12,
            StudioStyle.Accent, true));
        validation.AddChild(StudioStyle.Label(
            "La prévisualisation revalide l’événement, la fondation, la présentation, la progression, le loot, " +
            "le rôle, les règles et les références du blueprint ciblé.", 10, StudioStyle.Text, wrap: true));
        validation.AddChild(StudioStyle.Label(
            "Les événements UX4-N — combat, inventaire, repos, heartbeat et événements utilisateur — sont " +
            "conservés sans réécriture.", 9, StudioStyle.Muted, wrap: true));
        validation.AddChild(StudioStyle.Label(
            "Les diagnostics restent liés au chemin canonique mais sont présentés ici en langage humain.",
            9, StudioStyle.Muted, wrap: true));

        _diagnostics = new VBoxContainer();
        root.AddChild(_diagnostics);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 150),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            FitContent = false,
            SelectionEnabled = true,
        };
        root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Aperçu Domain/Application uniquement. Aucun dégât, acteur, loot, script ou réseau n’est exécuté.",
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
        if (cancelApproval) _session.CancelCreatureDamageUx4O();
        _choices = _session.CreatureDamageChoicesUx4O;
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
            "Choisissez explicitement une créature.");
        SetEditingEnabled(false);
        Visible = true;
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null &&
            ReferenceEquals(_session, _sourceSession)) return;
        if (ReferenceEquals(_session, _sourceSession)) _session.CancelCreatureDamageUx4O();
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
        _session.CancelCreatureDamageUx4O();
        _previewResult = null;
        _apply.Disabled = true;
        if (index <= 0 || index > _choices.Count)
        {
            _draft = null;
            ClearControls();
            RenderEmpty("Choisissez explicitement une créature.");
            SetEditingEnabled(false);
            return;
        }
        var choice = _choices[(int)index - 1];
        _draft = _session.CreateCreatureDamageDraftUx4O(choice.BlueprintId);
        if (_draft is null)
        {
            ClearControls();
            RenderEmpty("Cette créature est protégée ou devenue indisponible.");
            SetEditingEnabled(false);
            return;
        }
        Populate(_draft);
        _context.Text = $"{choice.Label} · validation ciblée et préservation UX4-N.";
        RenderEmpty("Modifiez les champs puis prévisualisez sans mutation.");
        SetEditingEnabled(true);
    }

    private void Populate(StudioCreatureDamageDraftUx4O draft)
    {
        _updating = true;
        _enabled.ButtonPressed = draft.DamageDeathEnabled;
        _beforeDamage.Text = draft.BeforeDamageHandler;
        _damageApplied.Text = draft.DamageAppliedHandler;
        _died.Text = draft.DeathHandler;
        _budget.Text = draft.BeforeDamageBudgetMilliseconds;
        _ratio.Text = draft.MaximumAdjustmentRatio;
        _updating = false;
        UpdateFieldStates();
    }

    private StudioCreatureDamageDraftUx4O? ReadDraft()
    {
        if (_draft is null) return null;
        return _draft with
        {
            DamageDeathEnabled = _enabled.ButtonPressed,
            BeforeDamageHandler = _beforeDamage.Text,
            DamageAppliedHandler = _damageApplied.Text,
            DeathHandler = _died.Text,
            BeforeDamageBudgetMilliseconds = _budget.Text,
            MaximumAdjustmentRatio = _ratio.Text,
        };
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null) return;
        _previewResult = _session.PreviewCreatureDamageUx4O(draft);
        RenderPreview(_previewResult);
        _apply.Disabled = !_previewResult.CanApply;
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
    }

    private void Apply()
    {
        var draft = ReadDraft();
        if (draft is null) return;
        var result = _session.ApplyApprovedCreatureDamageUx4O(draft);
        _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        CreatureChanged?.Invoke();
        ReloadSelection(draft.BlueprintId);
    }

    private void Undo()
    {
        var selected = _draft?.BlueprintId;
        var result = _session.UndoCreatureDamageUx4O();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        CreatureChanged?.Invoke();
        if (selected is { } id) ReloadSelection(id);
    }

    private void Redo()
    {
        var selected = _draft?.BlueprintId;
        var result = _session.RedoCreatureDamageUx4O();
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
        _choices = _session.CreatureDamageChoicesUx4O;
        var index = _choices.ToList().FindIndex(item => item.BlueprintId == blueprintId);
        _updating = true;
        _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label);
        _creature.Select(index >= 0 ? index + 1 : 0);
        _updating = false;
        SelectCreature(index >= 0 ? index + 1 : 0);
    }

    private void RenderPreview(StudioCreatureDamagePreviewUx4O result)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        foreach (var diagnostic in result.Diagnostics.Take(5))
            _diagnostics.AddChild(StudioStyle.Label(
                $"{diagnostic.HumanMessage} · {HumanPath(diagnostic.Path)}", 9,
                StudioStyle.Danger, wrap: true));
        _summary.Text = $"[b]{result.Message}[/b]\n" +
                        $"[color=#8ce0bd]{result.PatchSummary}[/color]\n" +
                        $"Dégâts : {result.DamageSummary}\n" +
                        $"Mort : {result.DeathSummary}\n" +
                        $"{result.ValidationSummary}\n" +
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
        _session.CancelCreatureDamageUx4O();
        _previewResult = null;
        _apply.Disabled = true;
    }

    private void UpdateFieldStates()
    {
        var enabled = _draft is not null && _enabled.ButtonPressed;
        _beforeDamage.Editable = enabled;
        _damageApplied.Editable = enabled;
        _died.Editable = enabled;
        _budget.Editable = enabled;
        _ratio.Editable = enabled;
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
        _beforeDamage.Text = string.Empty;
        _damageApplied.Text = string.Empty;
        _died.Text = string.Empty;
        _budget.Text = string.Empty;
        _ratio.Text = string.Empty;
        _context.Text = "Sélection explicite requise.";
        _updating = false;
        UpdateFieldStates();
    }

    private void ConnectEvents()
    {
        _creature.ItemSelected += SelectCreature;
        _enabled.Toggled += _ => { UpdateFieldStates(); InvalidatePreview(); };
        foreach (var field in new[] { _beforeDamage, _damageApplied, _died, _budget, _ratio })
            field.TextChanged += _ => InvalidatePreview();
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
        _draft = StudioSessionController.ChangedCreatureDamageDraftUx4O(_draft);
        Populate(_draft);
        _tabs.CurrentTab = mode.Contains("validation", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
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
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsDamageLifecycle &&
                    ShowsDeath && ShowsValidation && ShowsHumanSurface && UsesIsolatedEvidence &&
                    EvidenceSourceUntouched && (_usesRealProfile ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:" +
                           $"damage={ShowsDamageLifecycle}:death={ShowsDeath}:validation={ShowsValidation}:" +
                           $"human={ShowsHumanSurface}:isolated={UsesIsolatedEvidence}:" +
                           $"source={EvidenceSourceUntouched}:origin={_usesRealProfile ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4O_EVIDENCE_FAILED failure={_lastFailure}");
            Close();
            return false;
        }
        SetStatus("Preuve UX4-O isolée prête · aucune donnée source modifiée.", false);
        return true;
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource();
        if (snapshot is null) return false;
        StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-o-creature-damage-validation") || _evidenceSession is null ||
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
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4O_UI_TRANSACTION_FAILED type={exception.GetType().Name} " +
                        $"message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreCreatureDamageApprovalUx4O(snapshot.Approval);
        }
    }

    internal void CloseEvidence() => Close();

    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource();
        var evidence = _sourceSession.CreateUx4OEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux4o-evidence-" + Guid.NewGuid().ToString("N"));
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

    private StudioCreatureDamageEvidenceSourceSnapshotUx4O? CaptureSource()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(),
            _sourceSession.SelectedEntityId, _sourceSession.CaptureCreatureDamageApprovalUx4O());
    }

    private bool SourceMatches(StudioCreatureDamageEvidenceSourceSnapshotUx4O snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history is not null &&
               history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureCreatureDamageApprovalUx4O() == snapshot.Approval;
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4o-evidence-",
                    StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch
        {
            return false;
        }
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
        content.AddThemeConstantOverride("separation", 8);
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
