using System.Globalization;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed class StudioEncounterSelectionActivationOverlayUx2LwG : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceTempRoot;
    private StudioUx2LwGEvidenceSourceSnapshot? _evidenceSnapshot;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _encounter;
    private readonly Label _selection;
    private readonly Label _foundation;
    private readonly Label _provider;
    private readonly LineEdit _strategy;
    private readonly LineEdit _seed;
    private readonly CheckBox _previewComposition;
    private readonly OptionButton _activationMode;
    private readonly LineEdit _interval;
    private readonly OptionButton _activationScript;
    private readonly Label _scriptProvenance;
    private readonly OptionButton _distribution;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _partyPreview;
    private readonly Label _compositionPreview;
    private readonly Label _activationPreview;
    private readonly Label _boundary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2LwGEncounterChoice> _encounterChoices = [];
    private IReadOnlyList<StudioUx2LwGScriptChoice> _scriptChoices = [];
    private readonly List<StudioUx2LwGEncounterChoice?> _encounterEntries = [];
    private readonly List<string?> _scriptIds = [];
    private StudioUx2LwGDraft? _draft;
    private StudioUx2LwGPreview? _lastPreview;
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
    internal bool ShowsTwoRoutes => _lastPreview is not null &&
                                     !_partyPreview.Text.Contains("non évalué", StringComparison.OrdinalIgnoreCase) &&
                                     !_compositionPreview.Text.Contains("non évaluée", StringComparison.OrdinalIgnoreCase) &&
                                     !_activationPreview.Text.Contains("non évaluée", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHonestRuntimeBoundary =>
        _boundary.Text.Contains("minuteur", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("ForgeScript", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("spawn", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("réseau", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
        _lastPreview.Diagnostics.All(item => !string.IsNullOrWhiteSpace(item.Code) &&
                                             !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesOnlyRealScripts => _draft?.ScriptId is null || _scriptChoices.Any(item =>
        string.Equals(item.Id, _draft.ScriptId, StringComparison.Ordinal));
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
            return text.Contains("Sélection selon la difficulté", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Rencontre unique ou continue", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Fiche de rencontre requise", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsTechnicalIdentifier(text);
        }
    }

    internal StudioEncounterSelectionActivationOverlayUx2LwG(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioEncounterSelectionActivationOverlayUx2LwG";
        Visible = false;
        ZIndex = 476;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new Color(0, 0, 0, .84f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _dialog = StudioStyle.Panel("EncounterSelectionActivationDialog", StudioStyle.Chrome, 12, 2);
        _dialog.ClipContents = true;
        _dialog.PropagateMaximumSize = true;
        AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14);
        margin.PropagateMaximumSize = true;
        _dialog.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill, PropagateMaximumSize = true };
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("MONDE VIVANT · RENCONTRES", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Sélection et activation", 20));
        heading.AddChild(StudioStyle.Label(
            "ENC-006/007 · évaluation de groupe data-driven, aperçu déterministe et politique unique ou continue · " +
            "aucun fournisseur externe, minuteur, script, acteur ou réseau n’est lancé ici",
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

        var bodyHost = new Control { CustomMinimumSize = new Vector2(0, 520),
            CustomMaximumSize = new Vector2(-1, 820), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true, PropagateMaximumSize = true };
        root.AddChild(bodyHost);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 9);
        scroll.AddChild(content);

        _foundation = StudioStyle.Label("Aucune fiche sélectionnée.", 10, StudioStyle.Muted, wrap: true);
        var dependencyBody = new VBoxContainer();
        dependencyBody.AddChild(_foundation);
        content.AddChild(Card("EncounterPolicyDependency", "Fiche de rencontre requise",
            "Cette surface ne synthétise ni volume, ni candidat, ni budget : elle exige la fiche complète créée par LW-F.",
            dependencyBody));

        _provider = StudioStyle.Label("Fournisseur non évalué", 10, StudioStyle.Muted, wrap: true);
        _strategy = Line("challenge-budget");
        _seed = NumberLine("42");
        _previewComposition = Check("Demander un aperçu de composition");
        var selectionGrid = Grid();
        AddRow(selectionGrid, "Évaluation de groupe héritée", _provider);
        AddRow(selectionGrid, "Stratégie data-driven", _strategy);
        AddRow(selectionGrid, "Graine déterministe", _seed);
        AddRow(selectionGrid, "Aperçu", _previewComposition);
        content.AddChild(Card("EncounterSelection", "Sélection selon la difficulté",
            "Le fournisseur reste celui de la difficulté LW-F. Le Studio utilise les valeurs de groupe enregistrées ; " +
            "le fournisseur et l’exécuteur de stratégie ne sont pas invoqués en direct.",
            selectionGrid));

        _activationMode = EnumOption(Enum.GetValues<EncounterActivationMode>(),
            StudioSessionController.HumanActivationUx2LwG);
        _interval = NumberLine("30");
        _activationScript = Option();
        _scriptProvenance = StudioStyle.Label("Aucun script choisi.", 10, StudioStyle.Muted, wrap: true);
        _distribution = EnumOption(Enum.GetValues<EncounterSpawnDistribution>(),
            StudioSessionController.HumanDistributionUx2LwG);
        var activationGrid = Grid();
        AddRow(activationGrid, "Mode", _activationMode);
        AddRow(activationGrid, "Cadence en secondes", _interval);
        AddRow(activationGrid, "ForgeScript réel", _activationScript);
        AddRow(activationGrid, "Provenance", _scriptProvenance);
        AddRow(activationGrid, "Répartition", _distribution);
        content.AddChild(Card("EncounterActivation", "Rencontre unique ou continue",
            "La cadence et le script sont des données persistées. Cet écran ne planifie pas de tick et n’exécute aucun script.",
            activationGrid));

        _diff = new VBoxContainer();
        _diagnostics = new VBoxContainer();
        _partyPreview = StudioStyle.Label("Groupe non évalué", 10, StudioStyle.Muted, wrap: true);
        _compositionPreview = StudioStyle.Label("Composition non évaluée", 10, StudioStyle.Muted, wrap: true);
        _activationPreview = StudioStyle.Label("Activation non évaluée", 10, StudioStyle.Muted, wrap: true);
        _boundary = StudioStyle.Label(
            "Aucun fournisseur, minuteur moteur, ForgeScript, spawn d’acteur ou transport réseau n’est exécuté.",
            10, StudioStyle.Warning, wrap: true);
        var previewBody = new VBoxContainer();
        previewBody.AddChild(_diff); previewBody.AddChild(_diagnostics);
        previewBody.AddChild(_partyPreview); previewBody.AddChild(_compositionPreview);
        previewBody.AddChild(_activationPreview); previewBody.AddChild(_boundary);
        content.AddChild(Card("EncounterPolicyPreview", "Aperçu sec canonique",
            "Le diff humain est affiché avant application ; les diagnostics conservent code et localisation en mémoire.",
            previewBody));

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        _preview.Pressed += Preview; _apply.Pressed += Apply; _undo.Pressed += Undo;
        _redo.Pressed += Redo; _save.Pressed += Save;
        actions.AddChild(_preview); actions.AddChild(_apply); actions.AddChild(_undo);
        actions.AddChild(_redo); actions.AddChild(_save);
        root.AddChild(actions);
        _status = StudioStyle.Label("Choisissez une rencontre.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        _strategy.TextChanged += _ => DraftChanged();
        _seed.TextChanged += _ => DraftChanged();
        _interval.TextChanged += _ => DraftChanged();
        _previewComposition.Toggled += _ => DraftChanged();
        _activationMode.ItemSelected += _ => { UpdateScriptState(); DraftChanged(); };
        _activationScript.ItemSelected += _ => { UpdateScriptProvenance(); DraftChanged(); };
        _distribution.ItemSelected += _ => DraftChanged();
        Resized += FitDialog;
        ResetPreview("Choisissez une rencontre.");
    }

    internal void Open()
    {
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        _session.CancelEncounterPolicyPreviewUx2LwG();
        Visible = true;
        Reload(clearSelection: true);
        FitDialog();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (_evidenceSession is not null) ReleaseEvidence();
        else _session.CancelEncounterPolicyPreviewUx2LwG();
        _draft = null; _lastPreview = null; _canonicalPreviewReady = false; Visible = false;
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
        var project = _sourceSession.CreateUx2LwGEvidenceProject();
        if (project is null) return false;
        _lastEvidenceSourceUntouched = false; _lastEvidenceResourcesReleased = false;
        _lastSourceRestored = false; _evidenceSnapshot = CaptureSourceSnapshot();
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-g-evidence-" + Guid.NewGuid().ToString("N"));
        _evidenceTempRoot = root;
        try
        {
            if (!ProjectSession.Create(root, project).Succeeded ||
                !CopyScripts(_sourceSession.Session?.RootDirectory, root))
            {
                ReleaseEvidence();
                return false;
            }
            var evidence = new StudioSessionController();
            if (!evidence.Open(root).Succeeded)
            {
                evidence.Close(); ReleaseEvidence(); return false;
            }
            _evidenceSession = evidence; _session = evidence; Visible = true;
            Reload(clearSelection: true);
            var index = _encounterEntries.FindIndex(item => item is { IsComplete: true });
            if (index <= 0) { ReleaseEvidence(); return false; }
            _encounter.Select(index); SelectEncounter(index);
            if (_draft is null) { ReleaseEvidence(); return false; }
            _draft = _draft with
            {
                Strategy = _draft.Strategy + "-preuve",
                DeterministicSeed = _draft.DeterministicSeed ^ 1,
                PreviewComposition = true,
                ActivationMode = EncounterActivationMode.Timed,
                IntervalSeconds = Math.Max(1, _draft.IntervalSeconds + 1),
                ScriptId = null,
                Distribution = EncounterSpawnDistribution.Even,
            };
            PopulateDraft(); Preview();
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
            var applied = _session.ApplyApprovedEncounterPolicyUx2LwG();
            if (!applied.Succeeded) return false;
            var after = _session.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId);
            var changed = before.Composition?.Selection != after.Composition?.Selection ||
                          before.Composition?.Activation != after.Composition?.Activation;
            var undo = _session.Undo(); var redo = _session.Redo(); var saved = _session.Save();
            reopened = new StudioSessionController();
            var opened = saved.Succeeded && reopened.Open(_evidenceTempRoot).Succeeded;
            var persisted = opened ? reopened.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId) : null;
            return changed && undo.Succeeded && redo.Succeeded && persisted?.Composition?.Selection == after.Composition?.Selection &&
                   persisted.Composition?.Activation == after.Composition?.Activation &&
                   ProjectJsonSerializer.Serialize(_session.Project!) == ProjectJsonSerializer.Serialize(reopened.Project!);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_G_UI_TRANSACTION_FAILED {exception.Message}");
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
        _encounterChoices = _session.EncounterChoicesUx2LwG;
        _scriptChoices = _session.EncounterScriptChoicesUx2LwG();
        _encounter.Clear(); _encounterEntries.Clear();
        _encounter.AddItem("Choisissez explicitement une rencontre…"); _encounterEntries.Add(null);
        foreach (var choice in _encounterChoices)
        {
            _encounter.AddItem(choice.Label); _encounterEntries.Add(choice);
        }
        FillScripts(null);
        if (clearSelection)
        {
            _encounter.Select(0); _draft = null;
            _selection.Text = "Choisissez explicitement une rencontre complète.";
            _foundation.Text = "Aucune fiche sélectionnée.";
            _provider.Text = "Fournisseur non évalué";
            ResetPreview("Choisissez une rencontre.");
        }
        _updating = false;
        UpdateActions();
    }

    private void SelectEncounter(int index)
    {
        if (_updating) return;
        _session.CancelEncounterPolicyPreviewUx2LwG();
        _canonicalPreviewReady = false; _lastPreview = null;
        var choice = index >= 0 && index < _encounterEntries.Count ? _encounterEntries[index] : null;
        if (choice is null)
        {
            _draft = null; _selection.Text = "Aucune rencontre sélectionnée.";
            _foundation.Text = "Aucune fiche sélectionnée."; ResetPreview("Choisissez une rencontre."); return;
        }
        if (!choice.IsComplete)
        {
            _draft = null; _selection.Text = "Cette rencontre est incomplète : ouvrez d’abord l’assistant LW-F.";
            _foundation.Text = "Foundation et composition sont requises ; aucune valeur n’a été synthétisée.";
            ResetPreview("Fiche LW-F requise."); return;
        }
        _draft = _session.CreateEncounterPolicyDraftUx2LwG(choice.ProfileId);
        if (_draft is null)
        {
            _selection.Text = "La rencontre n’est plus disponible. Rechargez la liste.";
            ResetPreview("Sélection périmée."); return;
        }
        _selection.Text = $"Rencontre choisie · {choice.Label}";
        _foundation.Text = "Volume, candidats, difficulté, budgets et groupes conservés sans modification.";
        PopulateDraft(); ResetPreview("Modifiez une politique puis prévisualisez.");
    }

    private void PopulateDraft()
    {
        if (_draft is null) return;
        _updating = true;
        _provider.Text = $"{StudioSessionController.HumanPolicyTokenUx2LwG(_draft.PartyAssessmentProviderId)} · " +
                         "hérité de la difficulté";
        _strategy.Text = _draft.Strategy;
        _seed.Text = _draft.DeterministicSeed.ToString(CultureInfo.InvariantCulture);
        _previewComposition.ButtonPressed = _draft.PreviewComposition;
        SelectEnum(_activationMode, _draft.ActivationMode);
        _interval.Text = _draft.IntervalSeconds.ToString("R", CultureInfo.InvariantCulture);
        FillScripts(_draft.ScriptId);
        SelectEnum(_distribution, _draft.Distribution);
        UpdateScriptState(); UpdateScriptProvenance();
        _updating = false;
    }

    private bool TryReadDraft(out StudioUx2LwGDraft? draft)
    {
        draft = null;
        if (_draft is null) return false;
        if (!int.TryParse(_seed.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
        {
            SetStatus("La graine doit être un entier explicite.", true); return false;
        }
        if (!double.TryParse(_interval.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var interval))
            interval = double.NaN;
        draft = _draft with
        {
            Strategy = _strategy.Text,
            DeterministicSeed = seed,
            PreviewComposition = _previewComposition.ButtonPressed,
            ActivationMode = SelectedEnum<EncounterActivationMode>(_activationMode),
            IntervalSeconds = interval,
            ScriptId = SelectedScript(),
            Distribution = SelectedEnum<EncounterSpawnDistribution>(_distribution),
        };
        return true;
    }

    private void Preview()
    {
        if (!TryReadDraft(out var draft) || draft is null) return;
        _draft = draft;
        _lastPreview = _session.PreviewEncounterPolicyUx2LwG(draft);
        _canonicalPreviewReady = _lastPreview.Succeeded;
        RenderPreview(_lastPreview);
        SetStatus(_lastPreview.Message, !_lastPreview.Succeeded);
    }

    private void RenderPreview(StudioUx2LwGPreview preview)
    {
        Clear(_diff);
        if (preview.Diff.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucun changement à afficher.", 10, StudioStyle.Muted));
        foreach (var row in preview.Diff)
        {
            var color = row.Changed ? StudioStyle.Accent : StudioStyle.Muted;
            _diff.AddChild(StudioStyle.Label($"{row.Section} · avant : {row.Before}", 10, StudioStyle.Muted, wrap: true));
            _diff.AddChild(StudioStyle.Label($"{row.Section} · après : {row.After}", 10, color, wrap: true));
        }
        Clear(_diagnostics);
        if (preview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Validation canonique conforme.", 10, StudioStyle.Success));
        foreach (var item in preview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label(
                $"{DiagnosticSection(item.Path)} · {item.Message} · code {item.Code}",
                10, StudioStyle.Danger, wrap: true));
        _partyPreview.Text = preview.Runtime.PartyAssessment;
        _compositionPreview.Text = preview.Runtime.Composition;
        _activationPreview.Text = preview.Runtime.Activation;
        _boundary.Text = preview.Runtime.Boundary;
        UpdateActions();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedEncounterPolicyUx2LwG();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) { UpdateActions(); return; }
        var id = _draft?.ProfileId;
        EncounterChanged?.Invoke();
        ReloadAndReselect(id);
    }

    private void Undo()
    {
        var id = _draft?.ProfileId; var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { EncounterChanged?.Invoke(); ReloadAndReselect(id); }
    }

    private void Redo()
    {
        var id = _draft?.ProfileId; var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { EncounterChanged?.Invoke(); ReloadAndReselect(id); }
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
        _encounter.Select(index); SelectEncounter(index);
    }

    private void DraftChanged()
    {
        if (_updating || _draft is null) return;
        _session.CancelEncounterPolicyPreviewUx2LwG();
        _canonicalPreviewReady = false; _lastPreview = null;
        Clear(_diff); _diff.AddChild(StudioStyle.Label(
            "Aperçu précédent invalidé. Prévisualisez les nouvelles valeurs.", 10, StudioStyle.Warning));
        Clear(_diagnostics); _diagnostics.AddChild(StudioStyle.Label("Validation en attente.", 10, StudioStyle.Muted));
        _partyPreview.Text = "Groupe non évalué"; _compositionPreview.Text = "Composition non évaluée";
        _activationPreview.Text = "Activation non évaluée";
        _boundary.Text = "Aucun fournisseur, minuteur moteur, ForgeScript, spawn d’acteur ou transport réseau n’est exécuté.";
        SetStatus("Aperçu invalidé par la modification.", false); UpdateActions();
    }

    private void ResetPreview(string message)
    {
        _lastPreview = null; _canonicalPreviewReady = false;
        Clear(_diff); _diff.AddChild(StudioStyle.Label("Prévisualisez pour afficher le diff humain.", 10, StudioStyle.Muted));
        Clear(_diagnostics); _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic affiché.", 10, StudioStyle.Muted));
        _partyPreview.Text = "Groupe non évalué"; _compositionPreview.Text = "Composition non évaluée";
        _activationPreview.Text = "Activation non évaluée";
        _boundary.Text = "Aucun fournisseur, minuteur moteur, ForgeScript, spawn d’acteur ou transport réseau n’est exécuté.";
        SetStatus(message, false); UpdateActions();
    }

    private void UpdateActions()
    {
        _preview.Disabled = _draft is null;
        _apply.Disabled = _lastPreview?.CanApply != true;
        _undo.Disabled = !_session.CanUndo; _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
    }

    private void FillScripts(string? selected)
    {
        _activationScript.Clear(); _scriptIds.Clear();
        _activationScript.AddItem("Aucun script"); _scriptIds.Add(null);
        foreach (var choice in _scriptChoices)
        {
            _activationScript.AddItem($"{choice.Label} · {choice.Provenance}"); _scriptIds.Add(choice.Id);
        }
        var index = selected is null ? 0 : _scriptIds.FindIndex(item => string.Equals(item, selected, StringComparison.Ordinal));
        if (selected is not null && index < 0)
        {
            _activationScript.AddItem("Script enregistré indisponible · corriger avant application");
            _scriptIds.Add(selected); index = _scriptIds.Count - 1;
        }
        _activationScript.Select(Math.Max(0, index));
    }

    private string? SelectedScript() => _activationScript.Selected >= 0 &&
        _activationScript.Selected < _scriptIds.Count ? _scriptIds[_activationScript.Selected] : null;

    private void UpdateScriptState() =>
        _activationScript.Disabled = SelectedEnum<EncounterActivationMode>(_activationMode) != EncounterActivationMode.Script;

    private void UpdateScriptProvenance()
    {
        var selected = SelectedScript();
        var choice = selected is null ? null : _scriptChoices.FirstOrDefault(item => item.Id == selected);
        _scriptProvenance.Text = selected is null ? "Aucun script choisi." :
            choice is null ? "Référence enregistrée absente du workspace · application refusée." : choice.Provenance;
    }

    private StudioUx2LwGEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History ?? throw new InvalidOperationException("Historique source absent.");
        return new(_sourceSession.Project ?? throw new InvalidOperationException("Projet source absent."),
            history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureEncounterPolicyApprovalUx2LwG());
    }

    private bool SourceMatchesSnapshot(StudioUx2LwGEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureEncounterPolicyApprovalUx2LwG() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        var snapshot = _evidenceSnapshot;
        _evidenceSession?.CancelEncounterPolicyPreviewUx2LwG();
        _evidenceSession?.Close(); _evidenceSession = null;
        var root = _evidenceTempRoot; _evidenceTempRoot = null;
        _session = _sourceSession; _lastSourceRestored = true;
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

    private static OptionButton EnumOption<T>(IEnumerable<T> values, Func<T, string> label) where T : struct, Enum
    {
        var option = Option();
        foreach (var value in values) option.AddItem(label(value));
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
        line.TooltipText = "Valeur brute sans arrondi implicite ; la validation canonique décide.";
        return line;
    }

    private static CheckBox Check(string text)
    {
        var check = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        check.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize);
        return check;
    }

    private static GridContainer Grid()
    {
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 6);
        return grid;
    }

    private static void AddRow(GridContainer grid, string label, Control control)
    {
        var caption = StudioStyle.Label(label, 10, StudioStyle.Muted, wrap: true);
        caption.CustomMinimumSize = new Vector2(210, 0); caption.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        grid.AddChild(caption); grid.AddChild(control);
    }

    private static PanelContainer Card(string name, string title, string help, Control body)
    {
        var card = StudioStyle.Panel(name, StudioStyle.Surface, 8);
        var margin = StudioStyle.Margin(12, 10, 12, 10); card.AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 5); margin.AddChild(root);
        root.AddChild(StudioStyle.Label(title, 13));
        root.AddChild(StudioStyle.Label(help, 10, StudioStyle.Muted, wrap: true)); root.AddChild(body);
        return card;
    }

    private static T SelectedEnum<T>(OptionButton option) where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        return option.Selected >= 0 && option.Selected < values.Length ? values[option.Selected] : values[0];
    }

    private static void SelectEnum<T>(OptionButton option, T value) where T : struct, Enum
    {
        var index = Array.IndexOf(Enum.GetValues<T>(), value); option.Select(Math.Max(0, index));
    }

    private static string DiagnosticSection(string path) =>
        path.Contains("/selection", StringComparison.Ordinal) ? "Sélection" :
        path.Contains("/activation", StringComparison.Ordinal) ? "Activation" : "Fiche de rencontre";

    private static string HumanMode(string mode) => mode.Replace('-', ' ').Replace('_', ' ');

    private static bool CopyScripts(string? sourceRoot, string destinationRoot)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot)) return true;
        var scripts = Path.Combine(sourceRoot, "scripts");
        if (!Directory.Exists(scripts)) return true;
        try
        {
            foreach (var source in Directory.EnumerateFiles(scripts, "*.forge", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceRoot, source);
                var destination = Path.Combine(destinationRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, overwrite: true);
            }
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private void FitDialog()
    {
        if (!Visible) return;
        var viewport = GetViewportRect().Size;
        var width = Mathf.Clamp(viewport.X - 48, 860, 1320);
        var height = Mathf.Clamp(viewport.Y - 36, 700, 1020);
        _dialog.Position = new Vector2((viewport.X - width) / 2, (viewport.Y - height) / 2);
        _dialog.Size = new Vector2(width, height);
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
