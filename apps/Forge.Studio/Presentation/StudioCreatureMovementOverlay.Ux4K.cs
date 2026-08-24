using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCreatureMovementOverlayUx4K : Control
{
    private static readonly string[] ModeLabels =
    [
        "Au sol", "Vol", "Nage", "Escalade", "Véhicule", "Zéro gravité",
    ];

    private StudioSessionController _session;
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioCreatureMovementEvidenceSourceSnapshotUx4K? _sourceSnapshot;
    private BlueprintId? _evidenceBlueprintId;
    private bool _usesRealProfile;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _updating;
    private bool _applied;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioCreatureMovementChoiceUx4K> _choices = [];
    private StudioCreatureMovementDraftUx4K? _draft;
    private StudioCreatureMovementPreviewUx4K? _previewResult;

    private readonly OptionButton _creature;
    private readonly OptionButton _mode;
    private readonly LineEdit _maximumSpeed;
    private readonly LineEdit _acceleration;
    private readonly LineEdit _deceleration;
    private readonly LineEdit _minimumAltitude;
    private readonly LineEdit _maximumAltitude;
    private readonly LineEdit _maximumSlope;
    private readonly Label _context;
    private readonly Label _modeExplanation;
    private readonly VBoxContainer _diagnostics;
    private readonly RichTextLabel _summary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action? CreatureChanged;
    internal event Action<string, bool>? StatusChanged;
    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _creature.Selected > 0 && _draft is not null;
    internal bool CanonicalPreviewReady => _previewResult?.CanApply == true;
    internal bool ShowsSixModes => _mode.ItemCount == ModeLabels.Length &&
                                   ModeLabels.All(label => Enumerable.Range(0, _mode.ItemCount)
                                       .Any(index => _mode.GetItemText(index) == label));
    internal bool ShowsCompleteValues
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Vitesse maximale", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Accélération", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Décélération", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Altitude minimale", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Altitude maximale", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Pente maximale", StringComparison.OrdinalIgnoreCase);
        }
    }
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Vitesse et profils de déplacement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("six modes", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aucun acteur ni moteur physique", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text) && !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealProfileEvidence => _usesRealProfile;
    internal bool UsesCanonicalFallbackEvidence => _usesFallback;
    internal bool EvidencePreviewDryRun => _previewDryRun;
    internal bool EvidenceSourceUntouched => _sourceSnapshot is not null && SourceMatches(_sourceSnapshot);
    internal bool EvidenceResourcesReleased => _resourcesReleased;
    internal bool SourceSessionRestored => _sourceRestored;
    internal string LastEvidenceFailure => _lastFailure;

    internal StudioCreatureMovementOverlayUx4K(StudioSessionController session)
    {
        _session = session;
        _sourceSession = session;
        Name = "StudioCreatureMovementOverlayUx4K";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var shade = new ColorRect { Color = new Color(0.012f, 0.018f, 0.03f, 0.94f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        var dialog = StudioStyle.Panel("CreatureMovementDialogUx4K", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 68;
        dialog.OffsetTop = 36;
        dialog.OffsetRight = -68;
        dialog.OffsetBottom = -36;
        AddChild(dialog);

        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 8);
        dialog.AddChild(root);
        var header = new HBoxContainer();
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("VITESSE ET PROFILS DE DÉPLACEMENT · UX4-K", 17,
            StudioStyle.Accent, true));
        titles.AddChild(StudioStyle.Label(
            "Six modes canoniques et leurs valeurs complètes, sans UUID ni donnée technique brute affichée.",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);
        root.AddChild(header);

        var selection = new HBoxContainer();
        selection.AddChild(StudioStyle.Label("Créature", 9, StudioStyle.Muted));
        _creature = new OptionButton { CustomMinimumSize = new Vector2(410, 30) };
        selection.AddChild(_creature);
        _context = StudioStyle.Label("Sélection explicite requise · fondation de créature obligatoire.",
            9, StudioStyle.Muted, wrap: true);
        _context.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        selection.AddChild(_context);
        root.AddChild(selection);

        var editorHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 350),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        root.AddChild(editorHost);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true,
        };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        editorHost.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 9);
        scroll.AddChild(content);

        content.AddChild(StudioStyle.Label("Profil de déplacement", 12, StudioStyle.Accent));
        content.AddChild(StudioStyle.Label(
            "Choisissez l’un des six modes : au sol, vol, nage, escalade, véhicule ou zéro gravité.",
            9, StudioStyle.Muted, wrap: true));
        var modeRow = new HBoxContainer();
        modeRow.AddChild(StudioStyle.Label("Mode", 9, StudioStyle.Muted));
        _mode = new OptionButton { CustomMinimumSize = new Vector2(260, 30) };
        foreach (var label in ModeLabels) _mode.AddItem(label);
        modeRow.AddChild(_mode);
        _modeExplanation = StudioStyle.Label("Mouvement borné par le sol et la pente maximale.",
            9, StudioStyle.Text, wrap: true);
        _modeExplanation.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        modeRow.AddChild(_modeExplanation);
        content.AddChild(modeRow);

        var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _maximumSpeed = Field(grid, "Vitesse maximale (m/s)", "9.25");
        _acceleration = Field(grid, "Accélération (m/s²)", "13.5");
        _deceleration = Field(grid, "Décélération (m/s²)", "7.75");
        _minimumAltitude = Field(grid, "Altitude minimale (m)", "0");
        _maximumAltitude = Field(grid, "Altitude maximale (m)", "0");
        _maximumSlope = Field(grid, "Pente maximale (degrés)", "45");
        content.AddChild(grid);
        content.AddChild(StudioStyle.Label(
            "Les valeurs sont relues telles qu’écrites : aucune grille, aucun clamp silencieux. " +
            "Pour le vol, l’altitude maximale doit dépasser l’altitude minimale.",
            9, StudioStyle.Muted, wrap: true));

        content.AddChild(StudioStyle.Label("Aperçu canonique et diagnostics", 12, StudioStyle.Accent));
        content.AddChild(StudioStyle.Label(
            "Projection Domain/Application en mémoire. Aucun acteur ni moteur physique n’est exécuté ; " +
            "les déplacements réels restent sous l’autorité du runtime.",
            9, StudioStyle.Muted, wrap: true));
        _diagnostics = new VBoxContainer();
        content.AddChild(_diagnostics);

        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 142),
            FitContent = false,
            SelectionEnabled = true,
        };
        root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Prévisualiser ne modifie rien. Appliquer crée une commande annulable ; Enregistrer persiste ensuite le workspace.",
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

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        _session = _sourceSession;
        OpenBoundSession();
    }

    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) &&
            _evidenceSession is null && _evidenceRoot is null) return;
        _session.CancelCreatureMovementUx4K();
        _previewResult = null;
        _draft = null;
        Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }

    private void OpenBoundSession()
    {
        _session.CancelCreatureMovementUx4K();
        _previewResult = null;
        _draft = null;
        _applied = false;
        _choices = _session.CreatureMovementChoicesUx4K;
        _updating = true;
        _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label);
        _creature.Select(0);
        _updating = false;
        ClearControls();
        Visible = true;
        SetEditingEnabled(false);
        _context.Text = _choices.Count == 0
            ? "État vide réel : aucune créature avec fondation éditable."
            : "Sélection explicite requise · fondation de créature obligatoire.";
        RenderEmpty(_choices.Count == 0
            ? "Aucune synthèse produit : créez d’abord une créature avec fondation."
            : "Choisissez une créature pour éditer sa vitesse et son profil de déplacement.");
    }

    private void SelectCreature(long selected)
    {
        if (_updating) return;
        _session.CancelCreatureMovementUx4K();
        _previewResult = null;
        _apply.Disabled = true;
        if (selected <= 0 || selected > _choices.Count)
        {
            _draft = null;
            ClearControls();
            SetEditingEnabled(false);
            RenderEmpty("Sélection explicite requise.");
            return;
        }

        var choice = _choices[(int)selected - 1];
        _draft = _session.CreateCreatureMovementDraftUx4K(choice.BlueprintId);
        if (_draft is null)
        {
            ClearControls();
            SetEditingEnabled(false);
            RenderEmpty(choice.IsReadOnly
                ? "Créature installée en lecture seule : créez une variante projet."
                : "Cette créature ne possède plus de fondation éditable.");
            return;
        }

        Populate(_draft);
        SetEditingEnabled(true);
        _context.Text = $"{choice.Label} · six modes disponibles · valeurs brutes conservées.";
        RenderEmpty("Modifiez le brouillon puis prévisualisez la commande canonique.");
    }

    private void Populate(StudioCreatureMovementDraftUx4K draft)
    {
        _updating = true;
        _mode.Select((int)draft.Mode);
        _maximumSpeed.Text = draft.MaximumSpeed;
        _acceleration.Text = draft.Acceleration;
        _deceleration.Text = draft.Deceleration;
        _minimumAltitude.Text = draft.MinimumAltitude;
        _maximumAltitude.Text = draft.MaximumAltitude;
        _maximumSlope.Text = draft.MaximumSlopeDegrees;
        UpdateModeExplanation();
        _updating = false;
    }

    private StudioCreatureMovementDraftUx4K? ReadDraft() => _draft is null ? null : _draft with
    {
        Mode = (CreatureFoundationMovementMode)_mode.Selected,
        MaximumSpeed = _maximumSpeed.Text,
        Acceleration = _acceleration.Text,
        Deceleration = _deceleration.Text,
        MinimumAltitude = _minimumAltitude.Text,
        MaximumAltitude = _maximumAltitude.Text,
        MaximumSlopeDegrees = _maximumSlope.Text,
    };

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            SetStatus("Sélectionnez une créature avant de prévisualiser.", true);
            return;
        }
        _draft = draft;
        _previewResult = _session.PreviewCreatureMovementUx4K(draft);
        _previewDryRun = _previewResult.Succeeded;
        RenderPreview(_previewResult, _previewDryRun);
        _apply.Disabled = !_previewResult.CanApply;
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
    }

    private void Apply()
    {
        var draft = ReadDraft();
        if (draft is null)
        {
            SetStatus("Brouillon incomplet.", true);
            return;
        }
        var result = _session.ApplyApprovedCreatureMovementUx4K(draft);
        _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        CreatureChanged?.Invoke();
        _draft = _session.CreateCreatureMovementDraftUx4K(draft.BlueprintId);
        if (_draft is not null) Populate(_draft);
        _previewResult = null;
        _apply.Disabled = true;
    }

    private void Undo()
    {
        var result = _session.UndoCreatureMovementUx4K();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) Reload();
    }

    private void Redo()
    {
        var result = _session.RedoCreatureMovementUx4K();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) Reload();
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
    }

    private void Reload()
    {
        if (_draft is null) return;
        _draft = _session.CreateCreatureMovementDraftUx4K(_draft.BlueprintId);
        if (_draft is not null) Populate(_draft);
        _previewResult = null;
        _apply.Disabled = true;
        CreatureChanged?.Invoke();
    }

    private void RenderPreview(StudioCreatureMovementPreviewUx4K result, bool dryRun)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        foreach (var diagnostic in result.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label($"{diagnostic.HumanMessage} · {diagnostic.Path}",
                9, StudioStyle.Danger, wrap: true));
        _summary.Text = $"[b]{(result.Succeeded ? "APERÇU CANONIQUE" : "APERÇU REFUSÉ")}[/b]\n" +
                        $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n\n" +
                        $"{result.MovementSummary}\n{result.RuntimeSummary}\n" +
                        $"[color={(dryRun ? "#8ce0bd" : "#ff7777")}]Aperçu sans mutation : " +
                        $"{dryRun.ToString().ToLowerInvariant()}[/color]";
    }

    private void RenderEmpty(string message)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        _summary.Text = message;
    }

    private void InvalidatePreview()
    {
        if (_updating) return;
        _session.CancelCreatureMovementUx4K();
        _previewResult = null;
        _apply.Disabled = true;
    }

    private void UpdateModeExplanation()
    {
        _modeExplanation.Text = (CreatureFoundationMovementMode)_mode.Selected switch
        {
            CreatureFoundationMovementMode.Grounded => "Au sol · vitesse, accélération, freinage et pente.",
            CreatureFoundationMovementMode.Flying => "Vol · plage d’altitude non vide obligatoire.",
            CreatureFoundationMovementMode.Swimming => "Nage · projection aquatique, sans flottabilité simulée ici.",
            CreatureFoundationMovementMode.Climbing => "Escalade · pente persistée, sans surface grimpable simulée ici.",
            CreatureFoundationMovementMode.Vehicle => "Véhicule · profil de vitesse, sans conduite ni physique simulée ici.",
            CreatureFoundationMovementMode.ZeroG => "Zéro gravité · profil libre, sans gravité moteur simulée ici.",
            _ => "Mode hérité protégé.",
        };
    }

    private void SetEditingEnabled(bool enabled)
    {
        _mode.Disabled = !enabled;
        foreach (var field in Fields()) field.Editable = enabled;
        _preview.Disabled = !enabled;
        _apply.Disabled = !enabled || !CanonicalPreviewReady;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
    }

    private void ClearControls()
    {
        _updating = true;
        _mode.Select(0);
        foreach (var field in Fields()) field.Text = string.Empty;
        UpdateModeExplanation();
        _updating = false;
    }

    private void ConnectEvents()
    {
        _creature.ItemSelected += SelectCreature;
        _mode.ItemSelected += _ => { UpdateModeExplanation(); InvalidatePreview(); };
        foreach (var field in Fields()) field.TextChanged += _ => InvalidatePreview();
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
        _draft = _draft with
        {
            Mode = CreatureFoundationMovementMode.Flying,
            MaximumSpeed = "9.25",
            Acceleration = "13.5",
            Deceleration = "7.75",
            MinimumAltitude = "2.5",
            MaximumAltitude = "48.125",
            MaximumSlopeDegrees = "42.25",
        };
        Populate(_draft);
        var project = _session.Project!;
        var history = _session.History!;
        var revision = history.State.Revision;
        var journal = history.Journal.ToArray();
        var dirty = _session.IsDirty;
        Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched;
        _summary.Text = $"[b]{(_usesRealProfile ? "CAPTURE RÉELLE CRYPTE" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] " +
                        $"· copie isolée, source strictement inchangée.\n{_summary.Text}";
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsSixModes &&
                    ShowsCompleteValues && ShowsHumanSurface && UsesIsolatedEvidence && EvidenceSourceUntouched &&
                    (_usesRealProfile ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:" +
                           $"modes={ShowsSixModes}:values={ShowsCompleteValues}:human={ShowsHumanSurface}:" +
                           $"isolated={UsesIsolatedEvidence}:source={EvidenceSourceUntouched}:" +
                           $"origin={_usesRealProfile ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4K_EVIDENCE_FAILED failure={_lastFailure}");
            Close();
            return false;
        }
        SetStatus("Preuve UX4-K isolée prête · aucune donnée source modifiée.", false);
        return true;
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource();
        if (snapshot is null) return false;
        StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-k-creature-movement") || _evidenceSession is null ||
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
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4K_UI_TRANSACTION_FAILED type={exception.GetType().Name} " +
                        $"message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreCreatureMovementApprovalUx4K(snapshot.Approval);
        }
    }

    internal void CloseEvidence() => Close();

    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource();
        var evidence = _sourceSession.CreateUx4KEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux4k-evidence-" + Guid.NewGuid().ToString("N"));
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

    private StudioCreatureMovementEvidenceSourceSnapshotUx4K? CaptureSource()
    {
        var project = _sourceSession.Project;
        var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureCreatureMovementApprovalUx4K());
    }

    private bool SourceMatches(StudioCreatureMovementEvidenceSourceSnapshotUx4K snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history is not null &&
               history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo &&
               _sourceSession.IsDirty == snapshot.IsDirty && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureCreatureMovementApprovalUx4K() == snapshot.Approval;
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4k-evidence-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch { return false; }
    }

    private IEnumerable<LineEdit> Fields()
    {
        yield return _maximumSpeed;
        yield return _acceleration;
        yield return _deceleration;
        yield return _minimumAltitude;
        yield return _maximumAltitude;
        yield return _maximumSlope;
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

    private static IEnumerable<string> VisibleText(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label { Visible: true } label) yield return label.Text;
            if (child is Button { Visible: true } button) yield return button.Text;
            if (child is OptionButton { Visible: true } option)
                for (var index = 0; index < option.ItemCount; index++) yield return option.GetItemText(index);
            foreach (var value in VisibleText(child)) yield return value;
        }
    }

    private static bool ContainsGuid(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");
}
