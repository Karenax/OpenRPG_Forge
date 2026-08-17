using System.Globalization;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioEncounterStatisticsOverlayUx2LwK : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceTempRoot;
    private StudioUx2LwKEvidenceSourceSnapshot? _evidenceSnapshot;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _encounter;
    private readonly Label _selection;
    private readonly Label _dependencies;
    private readonly LineEdit _runs;
    private readonly LineEdit _seed;
    private readonly LineEdit _durationEstimator;
    private readonly LineEdit _secondsPerActor;
    private readonly LineEdit _rewardEstimator;
    private readonly LineEdit _rewardPerCost;
    private readonly LineEdit _percentiles;
    private readonly CheckBox _includeDetails;
    private readonly TextEdit _overrides;
    private readonly CheckBox _required;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _metrics;
    private readonly VBoxContainer _percentileRows;
    private readonly VBoxContainer _compositionRows;
    private readonly VBoxContainer _runRows;
    private readonly Label _detailState;
    private readonly Label _boundary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2LwKEncounterChoice> _encounterChoices = [];
    private readonly List<StudioUx2LwKEncounterChoice?> _encounterEntries = [];
    private StudioUx2LwKDraft? _draft;
    private StudioUx2LwKPreview? _lastPreview;
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
    internal bool ShowsCompleteMetrics => _lastPreview?.Simulation is { } simulation &&
        !_metrics.Text.Contains("non calculées", StringComparison.OrdinalIgnoreCase) &&
        simulation.Percentiles.Count > 0 && simulation.Composition.Count > 0 &&
        _percentileRows.GetChildCount() == simulation.Percentiles.Count &&
        _compositionRows.GetChildCount() == simulation.Composition.Count;
    internal bool ShowsHonestOfflineBoundary =>
        _boundary.Text.Contains("offline", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("aucun acteur", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("spawn", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("combat", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("réseau", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
        _lastPreview.Diagnostics.All(item => !string.IsNullOrWhiteSpace(item.Code) &&
                                             !string.IsNullOrWhiteSpace(item.Path));
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
            return text.Contains("Prévisualisation statistique", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Échantillon et graine", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Percentiles", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Composition cumulée", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu offline", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Foundation et composition", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsTechnicalIdentifier(text);
        }
    }

    internal StudioEncounterStatisticsOverlayUx2LwK(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioEncounterStatisticsOverlayUx2LwK";
        Visible = false;
        ZIndex = 480;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new(0, 0, 0, .84f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _dialog = StudioStyle.Panel("EncounterStatisticsDialog", StudioStyle.Chrome, 12, 2);
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
        heading.AddChild(StudioStyle.Label("Prévisualisation statistique", 20));
        heading.AddChild(StudioStyle.Label(
            "ENC-018 · échantillon déterministe, coûts, durées, récompenses, variances et percentiles · " +
            "aucun combat ou spawn live",
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
            CustomMinimumSize = new(0, 500),
            CustomMaximumSize = new(-1, 770),
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

        _dependencies = StudioStyle.Label("Aucune rencontre sélectionnée.",
            10, StudioStyle.Muted, wrap: true);
        var dependencyBody = new VBoxContainer();
        dependencyBody.AddChild(_dependencies);
        content.AddChild(Card("EncounterStatisticsDependencies", "Foundation et composition requises",
            "Cette surface ne synthétise ni volume, ni candidats, ni groupes. Les profils incomplets sont bloqués honnêtement.",
            dependencyBody));

        _runs = NumberLine("256");
        _seed = NumberLine("42");
        var sampleGrid = Grid();
        AddRow(sampleGrid, "Nombre de simulations", _runs);
        AddRow(sampleGrid, "Graine de départ", _seed);
        content.AddChild(Card("EncounterStatisticsSampling", "Échantillon et graine",
            "Chaque run utilise une graine consécutive. Les champs restent bruts : aucun clamp ou arrondi silencieux.",
            sampleGrid));

        _durationEstimator = Line("duration:actors-linear");
        _secondsPerActor = NumberLine("5");
        _rewardEstimator = Line("reward:cost-linear");
        _rewardPerCost = NumberLine("0.5");
        var estimatorGrid = Grid();
        AddRow(estimatorGrid, "Estimateur de durée", _durationEstimator);
        AddRow(estimatorGrid, "Secondes par acteur", _secondsPerActor);
        AddRow(estimatorGrid, "Estimateur de récompense", _rewardEstimator);
        AddRow(estimatorGrid, "Valeur par coût", _rewardPerCost);
        content.AddChild(Card("EncounterStatisticsEstimators", "Durée et récompense estimées",
            "Les identifiants sont conservés comme métadonnées ; le service actuel applique uniquement les coefficients linéaires du contrat existant et ne charge aucun plug-in.",
            estimatorGrid));

        _percentiles = Line("25%, 50%, 90%");
        _includeDetails = Check("Conserver les détails par run dans l’aperçu");
        _overrides = Multiline("clé=valeur · une entrée par ligne", 84);
        _required = Check("Profil statistique requis au runtime");
        var outputGrid = Grid();
        AddRow(outputGrid, "Percentiles", _percentiles);
        AddRow(outputGrid, "Détails", _includeDetails);
        AddRow(outputGrid, "Paramètres avancés", _overrides);
        AddRow(outputGrid, "Exigence", _required);
        content.AddChild(Card("EncounterStatisticsOutput", "Percentiles, détails et paramètres",
            "Les percentiles acceptent 25% ou 0.25. Les paramètres avancés restent des métadonnées. Au plus douze runs sont affichés, sans limiter le calcul canonique.",
            outputGrid));

        _diff = new();
        _diagnostics = new();
        _metrics = PreviewLabel("Moyennes et variances non calculées.");
        _percentileRows = new();
        _compositionRows = new();
        _runRows = new();
        _detailState = PreviewLabel("Détails par run non calculés.");
        _boundary = StudioStyle.Label(
            "Aperçu offline : aucun acteur, spawn, combat, loot, script, audio, rendu ou réseau n’est exécuté.",
            10, StudioStyle.Warning, wrap: true);
        var previewBody = new VBoxContainer();
        previewBody.AddChild(_diff);
        previewBody.AddChild(_diagnostics);
        previewBody.AddChild(StudioStyle.Label("Moyennes et variances", 11, StudioStyle.Accent));
        previewBody.AddChild(_metrics);
        previewBody.AddChild(StudioStyle.Label("Percentiles", 11, StudioStyle.Accent));
        previewBody.AddChild(_percentileRows);
        previewBody.AddChild(StudioStyle.Label("Composition cumulée", 11, StudioStyle.Accent));
        previewBody.AddChild(_compositionRows);
        previewBody.AddChild(StudioStyle.Label("Détails bornés à l’affichage", 11, StudioStyle.Accent));
        previewBody.AddChild(_runRows);
        previewBody.AddChild(_detailState);
        previewBody.AddChild(_boundary);
        content.AddChild(Card("EncounterStatisticsPreview", "Aperçu offline canonique",
            "Le dry-run de la commande complète précède la simulation déterministe et toute application.",
            previewBody));

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
            button.CustomMinimumSize = new(118, 34);
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

        _runs.TextChanged += _ => DraftChanged();
        _seed.TextChanged += _ => DraftChanged();
        _durationEstimator.TextChanged += _ => DraftChanged();
        _secondsPerActor.TextChanged += _ => DraftChanged();
        _rewardEstimator.TextChanged += _ => DraftChanged();
        _rewardPerCost.TextChanged += _ => DraftChanged();
        _percentiles.TextChanged += _ => DraftChanged();
        _includeDetails.Toggled += _ => DraftChanged();
        _overrides.TextChanged += DraftChanged;
        _required.Toggled += _ => DraftChanged();
        Resized += FitDialog;
        ResetPreview("Choisissez une rencontre.");
    }

    internal void Open()
    {
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        _session.CancelEncounterStatisticsPreviewUx2LwK();
        Visible = true;
        Reload(clearSelection: true);
        FitDialog();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (_evidenceSession is not null) ReleaseEvidence();
        else _session.CancelEncounterStatisticsPreviewUx2LwK();
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
        var project = _sourceSession.CreateUx2LwKEvidenceProject();
        if (project is null) return false;
        _lastEvidenceSourceUntouched = false;
        _lastEvidenceResourcesReleased = false;
        _lastSourceRestored = false;
        _evidenceSnapshot = CaptureSourceSnapshot();
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-k-evidence-" + Guid.NewGuid().ToString("N"));
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
            var encounter = project.Module.WorldFeatures.Encounters.FirstOrDefault(item =>
                item.Foundation is not null && item.Composition is not null && item.Statistics is not null);
            if (encounter is null || !evidence.SelectArea(encounter.AreaId))
            {
                ReleaseEvidence();
                return false;
            }
            Visible = true;
            FitDialog();
            Reload(clearSelection: true);
            var index = _encounterEntries.FindIndex(item => item is { IsComplete: true, HasStatistics: true });
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
                Runs = _draft.Runs + 8,
                RewardPerCost = _draft.RewardPerCost + .05,
                Percentiles = [.25, .5, .9, .99],
                IncludeRunDetails = true,
            };
            PopulateDraft();
            Preview();
            _lastEvidenceSourceUntouched = SourceMatchesSnapshot(_evidenceSnapshot);
            SetStatus($"Preuve isolée prête · {HumanMode(mode)} · source intacte", false);
            return _lastPreview?.Succeeded == true && _lastPreview.CanApply &&
                   _lastPreview.Simulation is not null && _lastEvidenceSourceUntouched;
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
            var beforeProject = _session.Project!;
            var before = beforeProject.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId);
            var applied = _session.ApplyApprovedEncounterStatisticsUx2LwK();
            if (!applied.Succeeded) return false;
            var afterProject = _session.Project!;
            var after = afterProject.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId);
            var changed = before.Statistics != after.Statistics ||
                          before.StatisticsRequiredAtRuntime != after.StatisticsRequiredAtRuntime;
            var undo = _session.Undo();
            var undoneProject = _session.Project!;
            var undone = undoneProject.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId);
            var redo = _session.Redo();
            var redoneProject = _session.Project!;
            var redone = redoneProject.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId);
            var saved = _session.Save();
            reopened = new();
            var opened = saved.Succeeded && reopened.Open(_evidenceTempRoot).Succeeded;
            var persisted = opened ? reopened.Project!.Module.WorldFeatures.Encounters.Single(item =>
                item.Id == targetId) : null;
            return changed && undo.Succeeded && redo.Succeeded &&
                   EncounterJson(undone, undoneProject) == EncounterJson(before, beforeProject) &&
                   EncounterJson(redone, redoneProject) == EncounterJson(after, afterProject) &&
                   persisted is not null && EncounterJson(persisted, reopened.Project!) ==
                   EncounterJson(after, afterProject) &&
                   ProjectJsonSerializer.Serialize(_session.Project!) == ProjectJsonSerializer.Serialize(reopened.Project!);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_K_UI_TRANSACTION_FAILED {exception.Message}");
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
        _encounterChoices = _session.EncounterChoicesUx2LwK;
        _encounter.Clear();
        _encounterEntries.Clear();
        _encounter.AddItem("Choisissez explicitement une rencontre…");
        _encounterEntries.Add(null);
        foreach (var choice in _encounterChoices)
        {
            _encounter.AddItem(choice.Label);
            _encounterEntries.Add(choice);
        }
        if (clearSelection)
        {
            _encounter.Select(0);
            _draft = null;
            _selection.Text = "Choisissez explicitement une rencontre complète.";
            _dependencies.Text = "Aucune rencontre sélectionnée.";
            ResetPreview("Choisissez une rencontre.");
        }
        _updating = false;
        UpdateActions();
    }

    private void SelectEncounter(int index)
    {
        if (_updating) return;
        _session.CancelEncounterStatisticsPreviewUx2LwK();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        var choice = index >= 0 && index < _encounterEntries.Count ? _encounterEntries[index] : null;
        if (choice is null)
        {
            _draft = null;
            _selection.Text = "Aucune rencontre sélectionnée.";
            _dependencies.Text = "Aucune fiche sélectionnée.";
            ResetPreview("Choisissez une rencontre.");
            return;
        }
        if (!choice.IsComplete)
        {
            _draft = null;
            _selection.Text = "Cette rencontre est incomplète : ouvrez d’abord l’assistant Foundation et composition.";
            _dependencies.Text = "Foundation et composition sont requises ; aucune valeur n’a été synthétisée.";
            ResetPreview("Dépendances requises.");
            return;
        }
        _draft = _session.CreateEncounterStatisticsDraftUx2LwK(choice.ProfileId);
        if (_draft is null)
        {
            _selection.Text = "La rencontre n’est plus disponible. Rechargez la liste.";
            ResetPreview("Sélection périmée.");
            return;
        }
        _selection.Text = $"Rencontre choisie · {choice.Label}";
        _dependencies.Text =
            "Foundation et composition réelles disponibles · scénario, sélection, activation, respawn, " +
            "scheduler, visibilité, cleanup et rencontres voisines seront conservés.";
        PopulateDraft();
        ResetPreview("Ajustez le profil puis prévisualisez la simulation offline.");
    }

    private void PopulateDraft()
    {
        if (_draft is null) return;
        _updating = true;
        _runs.Text = _draft.Runs.ToString(CultureInfo.InvariantCulture);
        _seed.Text = _draft.BaseSeed.ToString(CultureInfo.InvariantCulture);
        _durationEstimator.Text = _draft.DurationEstimatorId;
        _secondsPerActor.Text = _draft.SecondsPerActor.ToString("R", CultureInfo.InvariantCulture);
        _rewardEstimator.Text = _draft.RewardEstimatorId;
        _rewardPerCost.Text = _draft.RewardPerCost.ToString("R", CultureInfo.InvariantCulture);
        _percentiles.Text = string.Join(", ", _draft.Percentiles.Select(item =>
            $"{item * 100:0.##}%"));
        _includeDetails.ButtonPressed = _draft.IncludeRunDetails;
        _overrides.Text = string.Join('\n', _draft.Overrides.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => $"{item.Key}={item.Value}"));
        _required.ButtonPressed = _draft.RequiredAtRuntime;
        _updating = false;
    }

    private bool TryReadDraft(out StudioUx2LwKDraft? draft)
    {
        draft = null;
        if (_draft is null) return false;
        if (!int.TryParse(_runs.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var runs)) runs = 0;
        if (!int.TryParse(_seed.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed)) seed = 0;
        if (!double.TryParse(_secondsPerActor.Text, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var secondsPerActor))
            secondsPerActor = double.NaN;
        if (!double.TryParse(_rewardPerCost.Text, NumberStyles.Float, CultureInfo.InvariantCulture,
                out var rewardPerCost))
            rewardPerCost = double.NaN;
        if (!TryParsePercentiles(_percentiles.Text, out var percentiles))
        {
            SetStatus("Percentiles illisibles : utilisez par exemple 25%, 50%, 90%.", true);
            return false;
        }
        if (!TryParseOverrides(_overrides.Text, out var overrides, out var overrideError))
        {
            SetStatus(overrideError, true);
            return false;
        }
        draft = _draft with
        {
            Runs = runs,
            BaseSeed = seed,
            DurationEstimatorId = _durationEstimator.Text,
            SecondsPerActor = secondsPerActor,
            RewardEstimatorId = _rewardEstimator.Text,
            RewardPerCost = rewardPerCost,
            Percentiles = percentiles,
            IncludeRunDetails = _includeDetails.ButtonPressed,
            Overrides = overrides,
            RequiredAtRuntime = _required.ButtonPressed,
        };
        return true;
    }

    private void Preview()
    {
        if (!TryReadDraft(out var draft) || draft is null) return;
        _draft = draft;
        _lastPreview = _session.PreviewEncounterStatisticsUx2LwK(draft);
        _canonicalPreviewReady = _lastPreview.Succeeded;
        RenderPreview(_lastPreview);
        SetStatus(_lastPreview.Message, !_lastPreview.Succeeded);
    }

    private void RenderPreview(StudioUx2LwKPreview preview)
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
                $"{DiagnosticSection(item.Path)} · {item.Message}",
                10, StudioStyle.Danger, wrap: true));
        Clear(_percentileRows);
        Clear(_compositionRows);
        Clear(_runRows);
        if (preview.Simulation is not { } simulation)
        {
            ResetSimulationLabels();
            UpdateActions();
            return;
        }
        _metrics.Text =
            $"{simulation.Runs} runs · graines {simulation.FirstSeed} à {simulation.LastSeed} · " +
            $"moyenne acteurs {simulation.MeanCount:0.###} · coût {simulation.MeanCost:0.###} · " +
            $"durée {simulation.MeanDurationSeconds:0.###} s · récompense {simulation.MeanRewardValue:0.###}\n" +
            $"variances : acteurs {simulation.CountVariance:0.####} · coût {simulation.CostVariance:0.####} · " +
            $"durée {simulation.DurationVariance:0.####} · récompense {simulation.RewardVariance:0.####}";
        foreach (var row in simulation.Percentiles)
            _percentileRows.AddChild(PreviewLabel(
                $"{row.Label} · {row.SpawnedCount} acteur(s) · coût {row.Cost:0.###} · " +
                $"durée {row.DurationSeconds:0.###} s · récompense {row.RewardValue:0.###}"));
        foreach (var row in simulation.Composition)
            _compositionRows.AddChild(PreviewLabel(
                $"{row.Creature} · {row.TotalAcrossRuns} apparition(s) · moyenne {row.MeanPerRun:0.###}/run"));
        foreach (var row in simulation.DisplayedRuns)
            _runRows.AddChild(PreviewLabel(
                $"Run {row.Ordinal} · graine {row.Seed} · {row.SpawnedCount} acteur(s) · " +
                $"coût {row.Cost:0.###} · durée {row.DurationSeconds:0.###} s · " +
                $"récompense {row.RewardValue:0.###} · {row.Composition}"));
        _detailState.Text = simulation.DisplayedRuns.Count == 0
            ? "Détails non matérialisés par choix ; les agrégats couvrent tous les runs."
            : simulation.HiddenRunCount == 0
                ? $"{simulation.DisplayedRuns.Count} run(s) affiché(s)."
                : $"{simulation.DisplayedRuns.Count} run(s) affiché(s) · {simulation.HiddenRunCount} masqué(s) à l’écran.";
        _boundary.Text = simulation.Boundary;
        UpdateActions();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedEncounterStatisticsUx2LwK();
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
        _session.CancelEncounterStatisticsPreviewUx2LwK();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        Clear(_diff);
        _diff.AddChild(StudioStyle.Label(
            "Aperçu précédent invalidé. Relancez la simulation avec les nouvelles valeurs.",
            10, StudioStyle.Warning, wrap: true));
        Clear(_diagnostics);
        _diagnostics.AddChild(StudioStyle.Label("Validation en attente.", 10, StudioStyle.Muted));
        ResetSimulationLabels();
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
        ResetSimulationLabels();
        SetStatus(message, false);
        UpdateActions();
    }

    private void ResetSimulationLabels()
    {
        _metrics.Text = "Moyennes et variances non calculées.";
        Clear(_percentileRows);
        _percentileRows.AddChild(PreviewLabel("Percentiles non calculés."));
        Clear(_compositionRows);
        _compositionRows.AddChild(PreviewLabel("Composition non calculée."));
        Clear(_runRows);
        _runRows.AddChild(PreviewLabel("Détails par run non calculés."));
        _detailState.Text = "Détails par run non calculés.";
        _boundary.Text =
            "Aperçu offline : aucun acteur, spawn, combat, loot, script, audio, rendu ou réseau n’est exécuté.";
    }

    private void UpdateActions()
    {
        _preview.Disabled = _draft is null;
        _apply.Disabled = _lastPreview?.CanApply != true;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
    }

    private StudioUx2LwKEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History ?? throw new InvalidOperationException("Historique source absent.");
        return new(_sourceSession.Project ?? throw new InvalidOperationException("Projet source absent."),
            history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureEncounterStatisticsApprovalUx2LwK());
    }

    private bool SourceMatchesSnapshot(StudioUx2LwKEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureEncounterStatisticsApprovalUx2LwK() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        var snapshot = _evidenceSnapshot;
        _evidenceSession?.CancelEncounterStatisticsPreviewUx2LwK();
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

    private static bool TryParsePercentiles(string source, out IReadOnlyList<double> values)
    {
        var parsed = new List<double>();
        foreach (var raw in source.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim();
            var percentage = token.EndsWith('%');
            if (percentage) token = token[..^1].Trim();
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                values = [];
                return false;
            }
            parsed.Add(percentage ? value / 100 : value);
        }
        values = parsed;
        return true;
    }

    private static bool TryParseOverrides(
        string source,
        out IReadOnlyDictionary<string, string> values,
        out string error)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in source.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var separator = line.IndexOf('=');
            if (separator <= 0 || separator == line.Length - 1)
            {
                values = parsed;
                error = "Paramètre avancé illisible : utilisez clé=valeur, une entrée par ligne.";
                return false;
            }
            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (!parsed.TryAdd(key, value))
            {
                values = parsed;
                error = $"Le paramètre « {key} » apparaît plusieurs fois.";
                return false;
            }
        }
        values = parsed;
        error = string.Empty;
        return true;
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

    private static TextEdit Multiline(string placeholder, float height)
    {
        var edit = new TextEdit
        {
            PlaceholderText = placeholder,
            CustomMinimumSize = new(0, height),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary,
        };
        edit.AddThemeStyleboxOverride("normal", StudioStyle.Box(StudioStyle.Surface, StudioStyle.Border, 5));
        edit.AddThemeStyleboxOverride("focus", StudioStyle.Box(StudioStyle.SurfaceRaised, StudioStyle.Accent, 5, 2));
        edit.AddThemeColorOverride("font_color", StudioStyle.Text);
        edit.AddThemeColorOverride("font_placeholder_color", StudioStyle.Muted);
        edit.AddThemeColorOverride("caret_color", StudioStyle.Accent);
        edit.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        return edit;
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
        path.Contains("percentiles", StringComparison.Ordinal) ? "Percentiles" :
        path.Contains("overrides", StringComparison.Ordinal) ? "Paramètres avancés" :
        path.Contains("baseSeed", StringComparison.Ordinal) ? "Échantillon et graine" :
        path.Contains("statistics", StringComparison.Ordinal) ? "Profil statistique" :
        "Fiche de rencontre";

    private static string EncounterJson(
        Forge.Domain.WorldFeatures.EncounterWorldProfile profile,
        Forge.Domain.Projects.ProjectDefinition project)
    {
        var source = project.Module.WorldFeatures;
        var catalog = new Forge.Domain.WorldFeatures.WorldFeatureCatalog(source.Id, source.SchemaVersion,
            source.Provenance, source.Placeables, source.Doors, source.Triggers, [profile],
            source.VehicleRoutes, source.Merchants, source.Companions, source.Scripts);
        return ProjectJsonSerializer.Serialize(new(project.Id, project.Title,
            project.Module.WithWorldFeatures(catalog), project.Blueprints, project.Areas,
            project.Dialogues, project.Quests));
    }

    private static string HumanMode(string mode) => mode.Replace('-', ' ').Replace('_', ' ');

    private void FitDialog()
    {
        if (!Visible) return;
        var viewport = GetViewportRect().Size;
        var width = Mathf.Clamp(viewport.X - 48, 900, 1380);
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
        text.Split([' ', '·', '/', ':', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Any(token => Guid.TryParse(token, out _));
}
