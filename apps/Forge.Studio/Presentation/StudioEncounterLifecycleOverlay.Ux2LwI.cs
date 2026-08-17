using System.Globalization;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioEncounterLifecycleOverlayUx2LwI : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceTempRoot;
    private StudioUx2LwIEvidenceSourceSnapshot? _evidenceSnapshot;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _encounter;
    private readonly Label _selection;
    private readonly Label _foundation;
    private readonly CheckBox _respawnEnabled;
    private readonly LineEdit _delay;
    private readonly LineEdit _cycles;
    private readonly LineEdit _condition;
    private readonly CheckBox _resetWhenEmpty;
    private readonly CheckBox _persistent;
    private readonly LineEdit _antiFarm;
    private readonly OptionButton _faction;
    private readonly Label _factionProvenance;
    private readonly CheckBox _schedulerEnabled;
    private readonly CheckBox _heartbeatEnabled;
    private readonly LineEdit _interval;
    private readonly LineEdit _budget;
    private readonly LineEdit _customNamespace;
    private readonly Label _dormant;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _respawnPreview;
    private readonly Label _factionPreview;
    private readonly Label _heartbeatPreview;
    private readonly Label _customPreview;
    private readonly Label _boundary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2LwIEncounterChoice> _encounterChoices = [];
    private IReadOnlyList<StudioUx2LwIFactionChoice> _factionChoices = [];
    private readonly List<StudioUx2LwIEncounterChoice?> _encounterEntries = [];
    private readonly List<string?> _factionSlugs = [];
    private StudioUx2LwIDraft? _draft;
    private StudioUx2LwIPreview? _lastPreview;
    private bool _updating;
    private bool _canonicalPreviewReady;
    private bool _lastEvidenceSourceUntouched;
    private bool _lastEvidenceResourcesReleased;
    private bool _lastSourceRestored = true;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? EncounterChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _encounterChoices.Count;
    internal bool HasExplicitSelection => _draft is not null && _encounter.Selected > 0;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview?.Succeeded == true;
    internal bool ShowsPolicyRoutes => _lastPreview is not null &&
        !_respawnPreview.Text.Contains("non évalué", StringComparison.OrdinalIgnoreCase) &&
        !_factionPreview.Text.Contains("non évaluée", StringComparison.OrdinalIgnoreCase) &&
        !_heartbeatPreview.Text.Contains("non évalué", StringComparison.OrdinalIgnoreCase) &&
        !_customPreview.Text.Contains("non évalué", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHonestRuntimeBoundary =>
        _boundary.Text.Contains("timer", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("spawn", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("ForgeScript", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("faction", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("réseau", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
        _lastPreview.Diagnostics.All(item => !string.IsNullOrWhiteSpace(item.Code) &&
                                             !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesOnlyRealFactions => _draft?.SpawnFactionSlug is null || _factionChoices.Any(item =>
        string.Equals(item.Slug, _draft.SpawnFactionSlug, StringComparison.Ordinal));
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && ReferenceEquals(_session, _evidenceSession);
    internal bool EvidenceSourceUntouched => _evidenceSnapshot is { } snapshot
        ? SourceMatchesSnapshot(snapshot)
        : _lastEvidenceSourceUntouched;
    internal bool EvidenceResourcesReleased => _evidenceSession is null && _evidenceTempRoot is null &&
                                               _lastEvidenceResourcesReleased;
    internal bool SourceSessionRestored => ReferenceEquals(_session, _sourceSession) && _lastSourceRestored;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', DescendantLabels(_dialog).Select(item => item.Text));
            return text.Contains("Respawn", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Faction de spawn", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("OnHeartbeat et OnUserDefined", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Fiche Foundation requise", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("dormant", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsTechnicalIdentifier(text);
        }
    }

    internal StudioEncounterLifecycleOverlayUx2LwI(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioEncounterLifecycleOverlayUx2LwI";
        Visible = false;
        ZIndex = 478;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new(0, 0, 0, .84f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _dialog = StudioStyle.Panel("EncounterLifecycleDialog", StudioStyle.Chrome, 12, 2);
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
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("MONDE VIVANT · RENCONTRES", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Cycle de vie, faction et événements", 20));
        heading.AddChild(StudioStyle.Label(
            "ENC-008/010/014 · respawn borné, faction réelle et scheduler de politiques · " +
            "aucun timer, acteur, script, loot ou réseau n’est lancé ici",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);

        _encounter = Option();
        _encounter.ItemSelected += index => SelectEncounter((int)index);
        root.AddChild(_encounter);
        _selection = StudioStyle.Label("Choisissez explicitement une rencontre complète.",
            11, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var bodyHost = new Control
        {
            CustomMinimumSize = new(0, 520),
            CustomMaximumSize = new(-1, 850),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            PropagateMaximumSize = true,
        };
        root.AddChild(bodyHost);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 9);
        scroll.AddChild(content);

        _foundation = StudioStyle.Label("Aucune fiche sélectionnée.", 10, StudioStyle.Muted, wrap: true);
        var dependencyBody = new VBoxContainer();
        dependencyBody.AddChild(_foundation);
        content.AddChild(Card("EncounterLifecycleDependency", "Fiche Foundation requise",
            "Cette surface ne synthétise ni volume, ni candidat, ni budget. Toutes les autres politiques restent intactes.",
            dependencyBody));

        _respawnEnabled = Check("Configurer le respawn optionnel");
        _delay = NumberLine("30");
        _cycles = NumberLine("3");
        _condition = Line("area.occupied");
        _resetWhenEmpty = Check("Réinitialiser quand la zone n’a plus d’activateur éligible");
        _persistent = Check("Conserver le compteur après déchargement de zone");
        _antiFarm = NumberLine("0.5");
        var respawnGrid = Grid();
        AddRow(respawnGrid, "Politique", _respawnEnabled);
        AddRow(respawnGrid, "Délai en secondes", _delay);
        AddRow(respawnGrid, "Cycles maximum", _cycles);
        AddRow(respawnGrid, "Condition active", _condition);
        AddRow(respawnGrid, "Reset à vide", _resetWhenEmpty);
        AddRow(respawnGrid, "Persistance", _persistent);
        AddRow(respawnGrid, "Multiplicateur anti-farm", _antiFarm);
        content.AddChild(Card("EncounterRespawn", "Respawn",
            "Le runtime canonique décide délai, condition, compteur, reset et persistance. Le multiplicateur est transmis au loot, jamais appliqué ici.",
            respawnGrid));

        _faction = Option();
        _faction.ItemSelected += _ => { UpdateFactionProvenance(); DraftChanged(); };
        _factionProvenance = StudioStyle.Label("Faction héritée · aucune affectation exécutée.",
            10, StudioStyle.Muted, wrap: true);
        var factionGrid = Grid();
        AddRow(factionGrid, "Faction réelle", _faction);
        AddRow(factionGrid, "Provenance", _factionProvenance);
        content.AddChild(Card("EncounterSpawnFaction", "Faction de spawn",
            "Seules les factions actives du module sont proposées. La politique projette un slug ; elle n’assigne ni équipe ni acteur.",
            factionGrid));

        _schedulerEnabled = Check("Configurer le scheduler optionnel");
        _heartbeatEnabled = Check("Activer OnHeartbeat");
        _interval = NumberLine("5");
        _budget = NumberLine("12");
        _customNamespace = Line("encounter.runtime");
        _dormant = StudioStyle.Label(
            "Un heartbeat dormant conserve cadence, budget et namespace sans s’activer implicitement.",
            10, StudioStyle.Warning, wrap: true);
        var schedulerGrid = Grid();
        AddRow(schedulerGrid, "Politique", _schedulerEnabled);
        AddRow(schedulerGrid, "Heartbeat", _heartbeatEnabled);
        AddRow(schedulerGrid, "Cadence en secondes", _interval);
        AddRow(schedulerGrid, "Budget par minute", _budget);
        AddRow(schedulerGrid, "Namespace custom", _customNamespace);
        AddRow(schedulerGrid, "État dormant", _dormant);
        content.AddChild(Card("EncounterScheduler", "OnHeartbeat et OnUserDefined",
            "Le résolveur autorise ou refuse cadence, budget et namespace. Il ne planifie aucun tick et ne dispatch aucun ForgeScript.",
            schedulerGrid));

        _diff = new();
        _diagnostics = new();
        _respawnPreview = PreviewLabel("Respawn non évalué");
        _factionPreview = PreviewLabel("Faction non évaluée");
        _heartbeatPreview = PreviewLabel("Heartbeat non évalué");
        _customPreview = PreviewLabel("Événement custom non évalué");
        _boundary = StudioStyle.Label(
            "Aucun timer moteur, spawn, ForgeScript, loot, faction ou réseau n’a été exécuté.",
            10, StudioStyle.Warning, wrap: true);
        var previewBody = new VBoxContainer();
        previewBody.AddChild(_diff);
        previewBody.AddChild(_diagnostics);
        previewBody.AddChild(_respawnPreview);
        previewBody.AddChild(_factionPreview);
        previewBody.AddChild(_heartbeatPreview);
        previewBody.AddChild(_customPreview);
        previewBody.AddChild(_boundary);
        content.AddChild(Card("EncounterLifecyclePreview", "Aperçu sec canonique",
            "Le diff humain précède toute application ; code et localisation des diagnostics restent structurés.",
            previewBody));

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        _preview.Pressed += Preview;
        _apply.Pressed += Apply;
        _undo.Pressed += Undo;
        _redo.Pressed += Redo;
        _save.Pressed += Save;
        actions.AddChild(_preview);
        actions.AddChild(_apply);
        actions.AddChild(_undo);
        actions.AddChild(_redo);
        actions.AddChild(_save);
        root.AddChild(actions);
        _status = StudioStyle.Label("Choisissez une rencontre.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        _respawnEnabled.Toggled += _ => { UpdateOptionalStates(); DraftChanged(); };
        _delay.TextChanged += _ => DraftChanged();
        _cycles.TextChanged += _ => DraftChanged();
        _condition.TextChanged += _ => DraftChanged();
        _resetWhenEmpty.Toggled += _ => DraftChanged();
        _persistent.Toggled += _ => DraftChanged();
        _antiFarm.TextChanged += _ => DraftChanged();
        _schedulerEnabled.Toggled += _ => { UpdateOptionalStates(); DraftChanged(); };
        _heartbeatEnabled.Toggled += _ => { UpdateOptionalStates(); DraftChanged(); };
        _interval.TextChanged += _ => DraftChanged();
        _budget.TextChanged += _ => DraftChanged();
        _customNamespace.TextChanged += _ => DraftChanged();
        Resized += FitDialog;
        ResetPreview("Choisissez une rencontre.");
    }

    internal void Open()
    {
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        _session.CancelEncounterLifecyclePreviewUx2LwI();
        Visible = true;
        Reload(clearSelection: true);
        FitDialog();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (_evidenceSession is not null) ReleaseEvidence();
        else _session.CancelEncounterLifecyclePreviewUx2LwI();
        _draft = null;
        _lastPreview = null;
        _canonicalPreviewReady = false;
        Visible = false;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        Visible = true;
        Reload(clearSelection: true);
        FitDialog();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (_sourceSession.Project is null || _sourceSession.History is null) return false;
        if (_evidenceSession is not null) ReleaseEvidence();
        var project = _sourceSession.CreateUx2LwIEvidenceProject();
        if (project is null) return false;
        _lastEvidenceSourceUntouched = false;
        _lastEvidenceResourcesReleased = false;
        _lastSourceRestored = false;
        _evidenceSnapshot = CaptureSourceSnapshot();
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-i-evidence-" + Guid.NewGuid().ToString("N"));
        _evidenceTempRoot = root;
        try
        {
            if (!ProjectSession.Create(root, project).Succeeded)
            {
                ReleaseEvidence();
                return false;
            }
            var evidence = new StudioSessionController();
            if (!evidence.Open(root).Succeeded)
            {
                evidence.Close();
                ReleaseEvidence();
                return false;
            }
            _evidenceSession = evidence;
            _session = evidence;
            var encounter = project.Module.WorldFeatures.Encounters.FirstOrDefault(item => item.Foundation is not null);
            if (encounter is null || !evidence.SelectArea(encounter.AreaId))
            {
                ReleaseEvidence();
                return false;
            }
            Visible = true;
            FitDialog();
            Reload(clearSelection: true);
            var index = _encounterEntries.FindIndex(item => item is { IsComplete: true });
            if (index <= 0)
            {
                ReleaseEvidence();
                return false;
            }
            _encounter.Select(index);
            SelectEncounter(index);
            if (_draft is null)
            {
                ReleaseEvidence();
                return false;
            }
            _draft = _draft with
            {
                DelaySeconds = _draft.DelaySeconds + .5,
                MaximumCycles = _draft.MaximumCycles + 1,
                AntiFarmMultiplier = .6,
                HeartbeatEnabled = !_draft.HeartbeatEnabled,
            };
            PopulateDraft();
            Preview();
            _lastEvidenceSourceUntouched = SourceMatchesSnapshot(_evidenceSnapshot);
            SetStatus($"Preuve isolée prête · {HumanMode(mode)} · source intacte", false);
            return _lastPreview?.Succeeded == true && _lastPreview.CanApply && _lastEvidenceSourceUntouched;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            SetStatus($"Préparation de preuve refusée · {exception.Message}", true);
            ReleaseEvidence();
            return false;
        }
    }

    internal bool RunUiTransactionEvidence()
    {
        StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("transaction UI") || _draft is null || _evidenceTempRoot is null) return false;
            var targetId = _draft.ProfileId;
            var before = _session.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId);
            var applied = _session.ApplyApprovedEncounterLifecycleUx2LwI();
            if (!applied.Succeeded) return false;
            var after = _session.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId);
            var changed = before.Respawn != after.Respawn || before.SpawnFactionId != after.SpawnFactionId ||
                          before.Scheduler != after.Scheduler;
            var undo = _session.Undo();
            var redo = _session.Redo();
            var saved = _session.Save();
            reopened = new();
            var opened = saved.Succeeded && reopened.Open(_evidenceTempRoot).Succeeded;
            var persisted = opened ? reopened.Project!.Module.WorldFeatures.Encounters.Single(item =>
                item.Id == targetId) : null;
            return changed && undo.Succeeded && redo.Succeeded && persisted?.Respawn == after.Respawn &&
                   persisted?.SpawnFactionId == after.SpawnFactionId && persisted?.Scheduler == after.Scheduler &&
                   ProjectJsonSerializer.Serialize(_session.Project!) == ProjectJsonSerializer.Serialize(reopened.Project!);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_I_UI_TRANSACTION_FAILED {exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close();
            Close();
        }
    }

    private void Reload(bool clearSelection)
    {
        _updating = true;
        _encounterChoices = _session.EncounterChoicesUx2LwI;
        _factionChoices = _session.EncounterFactionChoicesUx2LwI();
        _encounter.Clear();
        _encounterEntries.Clear();
        _encounter.AddItem("Choisissez explicitement une rencontre…");
        _encounterEntries.Add(null);
        foreach (var choice in _encounterChoices)
        {
            _encounter.AddItem(choice.Label);
            _encounterEntries.Add(choice);
        }
        FillFactions(null);
        if (clearSelection)
        {
            _encounter.Select(0);
            _draft = null;
            _selection.Text = "Choisissez explicitement une rencontre complète.";
            _foundation.Text = "Aucune fiche sélectionnée.";
            ResetPreview("Choisissez une rencontre.");
        }
        _updating = false;
        UpdateActions();
    }

    private void SelectEncounter(int index)
    {
        if (_updating) return;
        _session.CancelEncounterLifecyclePreviewUx2LwI();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        var choice = index >= 0 && index < _encounterEntries.Count ? _encounterEntries[index] : null;
        if (choice is null)
        {
            _draft = null;
            _selection.Text = "Aucune rencontre sélectionnée.";
            _foundation.Text = "Aucune fiche sélectionnée.";
            ResetPreview("Choisissez une rencontre.");
            return;
        }
        if (!choice.IsComplete)
        {
            _draft = null;
            _selection.Text = "Cette rencontre est incomplète : ouvrez d’abord l’assistant Foundation LW-F.";
            _foundation.Text = "Foundation est requise ; aucune valeur n’a été synthétisée.";
            ResetPreview("Fiche Foundation requise.");
            return;
        }
        _draft = _session.CreateEncounterLifecycleDraftUx2LwI(choice.ProfileId);
        if (_draft is null)
        {
            _selection.Text = "La rencontre n’est plus disponible. Rechargez la liste.";
            ResetPreview("Sélection périmée.");
            return;
        }
        _selection.Text = $"Rencontre choisie · {choice.Label}";
        _foundation.Text =
            "Foundation, composition, scénario, statistiques, tactique, visibilité et cleanup sont conservés sans modification.";
        PopulateDraft();
        ResetPreview("Modifiez une politique puis prévisualisez.");
    }

    private void PopulateDraft()
    {
        if (_draft is null) return;
        _updating = true;
        _respawnEnabled.ButtonPressed = _draft.RespawnEnabled;
        _delay.Text = _draft.DelaySeconds.ToString("R", CultureInfo.InvariantCulture);
        _cycles.Text = _draft.MaximumCycles.ToString(CultureInfo.InvariantCulture);
        _condition.Text = _draft.Condition;
        _resetWhenEmpty.ButtonPressed = _draft.ResetWhenEmpty;
        _persistent.ButtonPressed = _draft.Persistent;
        _antiFarm.Text = _draft.AntiFarmMultiplier.ToString("R", CultureInfo.InvariantCulture);
        FillFactions(_draft.SpawnFactionSlug);
        _schedulerEnabled.ButtonPressed = _draft.SchedulerEnabled;
        _heartbeatEnabled.ButtonPressed = _draft.HeartbeatEnabled;
        _interval.Text = _draft.IntervalSeconds.ToString("R", CultureInfo.InvariantCulture);
        _budget.Text = _draft.BudgetPerMinute.ToString(CultureInfo.InvariantCulture);
        _customNamespace.Text = _draft.CustomEventNamespace;
        UpdateFactionProvenance();
        UpdateOptionalStates();
        _updating = false;
    }

    private bool TryReadDraft(out StudioUx2LwIDraft? draft)
    {
        draft = null;
        if (_draft is null) return false;
        if (!double.TryParse(_delay.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var delay))
            delay = double.NaN;
        if (!int.TryParse(_cycles.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cycles))
            cycles = -1;
        if (!double.TryParse(_antiFarm.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var antiFarm))
            antiFarm = double.NaN;
        if (!double.TryParse(_interval.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var interval))
            interval = double.NaN;
        if (!int.TryParse(_budget.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var budget))
            budget = 0;
        draft = _draft with
        {
            RespawnEnabled = _respawnEnabled.ButtonPressed,
            DelaySeconds = delay,
            MaximumCycles = cycles,
            Condition = _condition.Text,
            ResetWhenEmpty = _resetWhenEmpty.ButtonPressed,
            Persistent = _persistent.ButtonPressed,
            AntiFarmMultiplier = antiFarm,
            SpawnFactionSlug = SelectedFactionSlug(),
            SchedulerEnabled = _schedulerEnabled.ButtonPressed,
            HeartbeatEnabled = _heartbeatEnabled.ButtonPressed,
            IntervalSeconds = interval,
            BudgetPerMinute = budget,
            CustomEventNamespace = _customNamespace.Text,
        };
        return true;
    }

    private void Preview()
    {
        if (!TryReadDraft(out var draft) || draft is null) return;
        _draft = draft;
        _lastPreview = _session.PreviewEncounterLifecycleUx2LwI(draft);
        _canonicalPreviewReady = _lastPreview.Succeeded;
        RenderPreview(_lastPreview);
        SetStatus(_lastPreview.Message, !_lastPreview.Succeeded);
    }

    private void RenderPreview(StudioUx2LwIPreview preview)
    {
        Clear(_diff);
        if (preview.Diff.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucun changement à afficher.", 10, StudioStyle.Muted));
        foreach (var row in preview.Diff)
        {
            var color = row.Changed ? StudioStyle.Accent : StudioStyle.Muted;
            _diff.AddChild(StudioStyle.Label($"{row.Section} · avant : {row.Before}",
                10, StudioStyle.Muted, wrap: true));
            _diff.AddChild(StudioStyle.Label($"{row.Section} · après : {row.After}", 10, color, wrap: true));
        }
        Clear(_diagnostics);
        if (preview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Validation canonique conforme.", 10, StudioStyle.Success));
        foreach (var item in preview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label(
                $"{DiagnosticSection(item.Path)} · {item.Message} · code {item.Code}",
                10, StudioStyle.Danger, wrap: true));
        _respawnPreview.Text = preview.Runtime.Respawn;
        _factionPreview.Text = preview.Runtime.Faction;
        _heartbeatPreview.Text = preview.Runtime.Heartbeat;
        _customPreview.Text = preview.Runtime.CustomEvent;
        _boundary.Text = preview.Runtime.Boundary;
        UpdateActions();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedEncounterLifecycleUx2LwI();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded)
        {
            UpdateActions();
            return;
        }
        var id = _draft?.ProfileId;
        EncounterChanged?.Invoke();
        ReloadAndReselect(id);
    }

    private void Undo()
    {
        var id = _draft?.ProfileId;
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            EncounterChanged?.Invoke();
            ReloadAndReselect(id);
        }
    }

    private void Redo()
    {
        var id = _draft?.ProfileId;
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded)
        {
            EncounterChanged?.Invoke();
            ReloadAndReselect(id);
        }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
        UpdateActions();
    }

    private void ReloadAndReselect(Guid? profileId)
    {
        Reload(clearSelection: true);
        if (profileId is null) return;
        var index = _encounterEntries.FindIndex(item => item?.ProfileId == profileId);
        if (index <= 0) return;
        _encounter.Select(index);
        SelectEncounter(index);
    }

    private void DraftChanged()
    {
        if (_updating || _draft is null) return;
        _session.CancelEncounterLifecyclePreviewUx2LwI();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        Clear(_diff);
        _diff.AddChild(StudioStyle.Label(
            "Aperçu précédent invalidé. Prévisualisez les nouvelles valeurs.", 10, StudioStyle.Warning));
        Clear(_diagnostics);
        _diagnostics.AddChild(StudioStyle.Label("Validation en attente.", 10, StudioStyle.Muted));
        ResetRuntimeLabels();
        SetStatus("Aperçu invalidé par la modification.", false);
        UpdateActions();
    }

    private void ResetPreview(string message)
    {
        _lastPreview = null;
        _canonicalPreviewReady = false;
        Clear(_diff);
        _diff.AddChild(StudioStyle.Label("Prévisualisez pour afficher le diff humain.",
            10, StudioStyle.Muted));
        Clear(_diagnostics);
        _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic affiché.", 10, StudioStyle.Muted));
        ResetRuntimeLabels();
        SetStatus(message, false);
        UpdateActions();
    }

    private void ResetRuntimeLabels()
    {
        _respawnPreview.Text = "Respawn non évalué";
        _factionPreview.Text = "Faction non évaluée";
        _heartbeatPreview.Text = "Heartbeat non évalué";
        _customPreview.Text = "Événement custom non évalué";
        _boundary.Text = "Aucun timer moteur, spawn, ForgeScript, loot, faction ou réseau n’a été exécuté.";
    }

    private void UpdateActions()
    {
        _preview.Disabled = _draft is null;
        _apply.Disabled = _lastPreview?.CanApply != true;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
    }

    private void UpdateOptionalStates()
    {
        var respawn = _respawnEnabled.ButtonPressed;
        _delay.Editable = respawn;
        _cycles.Editable = respawn;
        _condition.Editable = respawn;
        _resetWhenEmpty.Disabled = !respawn;
        _persistent.Disabled = !respawn;
        _antiFarm.Editable = respawn;
        var scheduler = _schedulerEnabled.ButtonPressed;
        _heartbeatEnabled.Disabled = !scheduler;
        _interval.Editable = scheduler;
        _budget.Editable = scheduler;
        _customNamespace.Editable = scheduler;
        _dormant.Text = scheduler && !_heartbeatEnabled.ButtonPressed
            ? "Heartbeat dormant : cadence, budget et namespace seront conservés sans activation implicite."
            : scheduler
                ? "Heartbeat actif : la façade vérifie cadence et budget, sans planifier de tick."
                : "Scheduler optionnel absent : aucun heartbeat ni namespace n’est évalué.";
    }

    private void FillFactions(string? selected)
    {
        _faction.Clear();
        _factionSlugs.Clear();
        _faction.AddItem("Hériter · aucune faction imposée");
        _factionSlugs.Add(null);
        foreach (var choice in _factionChoices)
        {
            _faction.AddItem(choice.Label);
            _factionSlugs.Add(choice.Slug);
        }
        var index = selected is null ? 0 : _factionSlugs.FindIndex(item =>
            string.Equals(item, selected, StringComparison.Ordinal));
        if (selected is not null && index < 0)
        {
            _faction.AddItem("Faction enregistrée indisponible · corriger avant application");
            _factionSlugs.Add(selected);
            index = _factionSlugs.Count - 1;
        }
        _faction.Select(Math.Max(0, index));
    }

    private string? SelectedFactionSlug() => _faction.Selected >= 0 && _faction.Selected < _factionSlugs.Count
        ? _factionSlugs[_faction.Selected]
        : null;

    private void UpdateFactionProvenance()
    {
        var selected = SelectedFactionSlug();
        var choice = selected is null ? null : _factionChoices.FirstOrDefault(item =>
            string.Equals(item.Slug, selected, StringComparison.Ordinal));
        _factionProvenance.Text = selected is null
            ? "Faction héritée · aucune affectation d’acteur exécutée."
            : choice is null
                ? "Référence enregistrée absente du module · application refusée."
                : $"{choice.Provenance} · projection {choice.Label} · aucune affectation exécutée.";
    }

    private StudioUx2LwIEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History ?? throw new InvalidOperationException("Historique source absent.");
        return new(_sourceSession.Project ?? throw new InvalidOperationException("Projet source absent."),
            history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureEncounterLifecycleApprovalUx2LwI());
    }

    private bool SourceMatchesSnapshot(StudioUx2LwIEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureEncounterLifecycleApprovalUx2LwI() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        var snapshot = _evidenceSnapshot;
        _evidenceSession?.CancelEncounterLifecyclePreviewUx2LwI();
        _evidenceSession?.Close();
        _evidenceSession = null;
        var root = _evidenceTempRoot;
        _evidenceTempRoot = null;
        _session = _sourceSession;
        _lastSourceRestored = true;
        _lastEvidenceSourceUntouched = snapshot is not null && SourceMatchesSnapshot(snapshot);
        _evidenceSnapshot = null;
        var released = true;
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { released = false; }
            catch (UnauthorizedAccessException) { released = false; }
        }
        _lastEvidenceResourcesReleased = released && (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root));
    }

    private static OptionButton Option()
    {
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(option);
        return option;
    }

    private static LineEdit Line(string placeholder)
    {
        var line = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(line);
        return line;
    }

    private static LineEdit NumberLine(string placeholder)
    {
        var line = Line(placeholder);
        line.TooltipText = "Valeur brute sans clamp ni arrondi implicite ; la validation canonique décide.";
        return line;
    }

    private static CheckBox Check(string text)
    {
        var check = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        check.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        return check;
    }

    private static Label PreviewLabel(string text) => StudioStyle.Label(text, 10, StudioStyle.Muted, wrap: true);

    private static GridContainer Grid()
    {
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 6);
        return grid;
    }

    private static void AddRow(GridContainer grid, string label, Control control)
    {
        var caption = StudioStyle.Label(label, 10, StudioStyle.Muted, wrap: true);
        caption.CustomMinimumSize = new(210, 0);
        caption.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        grid.AddChild(caption);
        grid.AddChild(control);
    }

    private static PanelContainer Card(string name, string title, string help, Control body)
    {
        var card = StudioStyle.Panel(name, StudioStyle.Surface, 8);
        var margin = StudioStyle.Margin(12, 10, 12, 10);
        card.AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 5);
        margin.AddChild(root);
        root.AddChild(StudioStyle.Label(title, 13));
        root.AddChild(StudioStyle.Label(help, 10, StudioStyle.Muted, wrap: true));
        root.AddChild(body);
        return card;
    }

    private static string DiagnosticSection(string path) =>
        path.Contains("/respawn", StringComparison.Ordinal) ? "Respawn" :
        path.Contains("/spawnFactionId", StringComparison.Ordinal) ? "Faction de spawn" :
        path.Contains("/scheduler", StringComparison.Ordinal) ? "Scheduler" : "Fiche de rencontre";

    private static string HumanMode(string mode) => mode.Replace('-', ' ').Replace('_', ' ');

    private void FitDialog()
    {
        if (!Visible) return;
        var viewport = GetViewportRect().Size;
        var width = Mathf.Clamp(viewport.X - 48, 880, 1340);
        var height = Mathf.Clamp(viewport.Y - 36, 700, 1040);
        _dialog.Position = new((viewport.X - width) / 2, (viewport.Y - height) / 2);
        _dialog.Size = new(width, height);
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error);
    }

    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren()) child.Free();
    }

    private static IEnumerable<Label> DescendantLabels(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Label label) yield return label;
            foreach (var nested in DescendantLabels(child)) yield return nested;
        }
    }

    private static bool ContainsTechnicalIdentifier(string text) =>
        text.Split([' ', '·', '/', ':', ',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(token => Guid.TryParse(token, out _));
}
