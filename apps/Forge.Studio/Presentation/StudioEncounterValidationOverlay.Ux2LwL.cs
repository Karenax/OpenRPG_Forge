using System.Globalization;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioEncounterValidationOverlayUx2LwL : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceTempRoot;
    private StudioUx2LwLEvidenceSourceSnapshot? _evidenceSnapshot;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _encounter;
    private readonly Label _selection;
    private readonly Label _dependencies;
    private readonly LineEdit _anchors;
    private readonly OptionButton _anchorSuggestion;
    private readonly Button _useAnchor;
    private readonly OptionButton _script;
    private readonly LineEdit _budget;
    private readonly LineEdit _networkCap;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly VBoxContainer _coverage;
    private readonly Label _boundary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2LwLEncounterChoice> _encounterChoices = [];
    private readonly List<StudioUx2LwLEncounterChoice?> _encounterEntries = [];
    private readonly List<StudioUx2LwLReferenceChoice?> _anchorEntries = [];
    private readonly List<StudioUx2LwLReferenceChoice?> _scriptEntries = [];
    private StudioUx2LwLDraft? _draft;
    private StudioUx2LwLPreview? _lastPreview;
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
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview is
        { Succeeded: true, CanApply: true, DryRunPure: true };
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
        _lastPreview.Diagnostics.All(item => !string.IsNullOrWhiteSpace(item.Code) &&
                                             !string.IsNullOrWhiteSpace(item.Path));
    internal bool ShowsHonestCoverage => _lastPreview?.Coverage.Any(item =>
        !item.Evaluated && item.Status == "Non évalué" &&
        item.Section is "Récompenses" or "Conditions" or "Plafonds globaux") == true;
    internal bool ShowsHonestOfflineBoundary =>
        _boundary.Text.Contains("offline", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("aucune navigation", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("récompense", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("réseau", StringComparison.OrdinalIgnoreCase);
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
            return text.Contains("Validation complète de rencontre", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Correction explicite", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Couverture réelle", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Non évalué", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu offline", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsTechnicalIdentifier(text);
        }
    }

    internal StudioEncounterValidationOverlayUx2LwL(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioEncounterValidationOverlayUx2LwL";
        Visible = false;
        ZIndex = 485;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new(0, 0, 0, .84f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _dialog = StudioStyle.Panel("EncounterValidationDialog", StudioStyle.Chrome, 12, 2);
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
        heading.AddChild(StudioStyle.Label("Validation complète de rencontre", 20));
        heading.AddChild(StudioStyle.Label(
            "ENC-020 · document, composition, scénario, statistiques, lifecycle, factions, routes, ancres et scripts réels · diagnostic Code + chemin conservé",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);

        _encounter = Option();
        _encounter.ItemSelected += index => SelectEncounter((int)index);
        root.AddChild(_encounter);
        _selection = StudioStyle.Label("Choisissez explicitement une rencontre.",
            11, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var bodyHost = new Control
        {
            CustomMinimumSize = new(0, 500),
            CustomMaximumSize = new(-1, 780),
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
        content.AddChild(Card("EncounterValidationDependencies", "Autorités canoniques réutilisées",
            "Cette surface ne duplique aucun validateur et ne synthétise ni Foundation, ni route, ni faction, ni script, ni récompense.",
            dependencyBody));

        _anchors = Line("ancre-nord, ancre-est");
        _anchorSuggestion = Option();
        _useAnchor = StudioStyle.Button("Remplacer par cette ancre", compact: true);
        _useAnchor.Pressed += UseSuggestedAnchor;
        var anchorPicker = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        anchorPicker.AddChild(_anchorSuggestion);
        anchorPicker.AddChild(_useAnchor);
        var anchorGrid = Grid();
        AddRow(anchorGrid, "Ancres authorées", _anchors);
        AddRow(anchorGrid, "Suggestion réelle", anchorPicker);
        content.AddChild(Card("EncounterValidationAnchors", "Ancres et navigation réelles",
            "Chaque clé doit résoudre une ancre de spawn enregistrée dans la zone. La suggestion ne s’applique qu’après votre clic explicite.",
            anchorGrid));

        _script = Option();
        _budget = NumberLine("40");
        _networkCap = NumberLine("12");
        var correctionGrid = Grid();
        AddRow(correctionGrid, "Script d’activation", _script);
        AddRow(correctionGrid, "Budget local", _budget);
        AddRow(correctionGrid, "Cap réseau local", _networkCap);
        content.AddChild(Card("EncounterValidationCorrection", "Correction explicite",
            "Seuls l’ancre, le script réel ou absent, le budget local et le cap local sont modifiables ici. Aucun changement n’est appliqué sans diff et dry-run valides.",
            correctionGrid));

        _diff = new();
        _diagnostics = new();
        _coverage = new();
        _boundary = StudioStyle.Label(
            "Aperçu offline : aucune navigation, apparition, caméra, récompense, condition, suppression, simulation moteur ou opération réseau n’est exécutée.",
            10, StudioStyle.Warning, wrap: true);
        var previewBody = new VBoxContainer();
        previewBody.AddChild(StudioStyle.Label("Diff humain", 11, StudioStyle.Accent));
        previewBody.AddChild(_diff);
        previewBody.AddChild(StudioStyle.Label("Diagnostics localisés", 11, StudioStyle.Accent));
        previewBody.AddChild(_diagnostics);
        previewBody.AddChild(StudioStyle.Label("Couverture réelle", 11, StudioStyle.Accent));
        previewBody.AddChild(_coverage);
        previewBody.AddChild(_boundary);
        content.AddChild(Card("EncounterValidationPreview", "Aperçu offline canonique",
            "Le handler canonique s’exécute en dry-run, puis les autorités runtime existantes complètent le diagnostic avant toute application.",
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

        _anchors.TextChanged += _ => DraftChanged();
        _anchorSuggestion.ItemSelected += _ => UpdateActions();
        _script.ItemSelected += _ => DraftChanged();
        _budget.TextChanged += _ => DraftChanged();
        _networkCap.TextChanged += _ => DraftChanged();
        Resized += FitDialog;
        ResetPreview("Choisissez une rencontre.");
    }

    internal void Open()
    {
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        _session.CancelEncounterValidationPreviewUx2LwL();
        Visible = true;
        Reload(clearSelection: true);
        FitDialog();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (_evidenceSession is not null) ReleaseEvidence();
        else _session.CancelEncounterValidationPreviewUx2LwL();
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
        var project = _sourceSession.CreateUx2LwLEvidenceProject();
        if (project is null) return false;
        _lastEvidenceSourceUntouched = false;
        _lastEvidenceResourcesReleased = false;
        _lastSourceRestored = false;
        _evidenceSnapshot = CaptureSourceSnapshot();
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-l-evidence-" + Guid.NewGuid().ToString("N"));
        _evidenceTempRoot = root;
        try
        {
            if (!ProjectSession.Create(root, project).Succeeded ||
                !StudioSessionController.WriteUx2LwLEvidenceScript(root))
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
            var encounter = project.Module.WorldFeatures.Encounters.FirstOrDefault(item => item.IsEvidenceUx2LwL());
            if (encounter is null || !evidence.SelectArea(encounter.AreaId))
            {
                ReleaseEvidence();
                return false;
            }
            Visible = true;
            FitDialog();
            Reload(clearSelection: true);
            var index = _encounterEntries.FindIndex(item => item is { IsEvidence: true, HasFoundation: true });
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
            _budget.Text = (_draft.SpawnBudget + 2.5).ToString("0.###", CultureInfo.InvariantCulture);
            _networkCap.Text = (_draft.NetworkActorCap + 1).ToString(CultureInfo.InvariantCulture);
            Preview();
            _lastEvidenceSourceUntouched = SourceMatchesSnapshot(_evidenceSnapshot);
            SetStatus($"Preuve isolée prête · {HumanMode(mode)} · source intacte", false);
            return _lastPreview is { Succeeded: true, CanApply: true, DryRunPure: true } &&
                   _lastEvidenceSourceUntouched;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                           InvalidOperationException)
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
            var applied = _session.ApplyApprovedEncounterValidationUx2LwL();
            if (!applied.Succeeded) return false;
            var afterProject = _session.Project!;
            var after = afterProject.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId);
            var changed = before.Foundation?.SpawnBudget != after.Foundation?.SpawnBudget ||
                          before.Foundation?.NetworkActorCap != after.Foundation?.NetworkActorCap;
            var undo = _session.Undo();
            var undoneProject = _session.Project!;
            var undone = undoneProject.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId);
            var redo = _session.Redo();
            var redoneProject = _session.Project!;
            var redone = redoneProject.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId);
            var saved = _session.Save();
            reopened = new();
            var opened = saved.Succeeded && reopened.Open(_evidenceTempRoot).Succeeded;
            var persisted = opened
                ? reopened.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId)
                : null;
            return changed && undo.Succeeded && redo.Succeeded &&
                   EncounterJson(undone, undoneProject) == EncounterJson(before, beforeProject) &&
                   EncounterJson(redone, redoneProject) == EncounterJson(after, afterProject) &&
                   persisted is not null && EncounterJson(persisted, reopened.Project!) ==
                   EncounterJson(after, afterProject) &&
                   ProjectJsonSerializer.Serialize(_session.Project!) ==
                   ProjectJsonSerializer.Serialize(reopened.Project!);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_L_UI_TRANSACTION_FAILED {exception.Message}");
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
        _encounterChoices = _session.EncounterChoicesUx2LwL;
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
            _selection.Text = "Choisissez explicitement une rencontre.";
            _dependencies.Text = "Aucune rencontre sélectionnée.";
            ResetPreview("Choisissez une rencontre.");
        }
        _updating = false;
        UpdateActions();
    }

    private void SelectEncounter(int index)
    {
        if (_updating) return;
        _session.CancelEncounterValidationPreviewUx2LwL();
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
        if (!choice.HasFoundation)
        {
            _draft = null;
            _selection.Text = "Foundation absente : ouvrez d’abord l’assistant de rencontre.";
            _dependencies.Text = "Aucune Foundation n’a été synthétisée par cette surface.";
            ResetPreview("Dépendance requise.");
            return;
        }
        _draft = _session.CreateEncounterValidationDraftUx2LwL(choice.ProfileId);
        if (_draft is null)
        {
            _selection.Text = "La rencontre n’est plus disponible. Rechargez la liste.";
            ResetPreview("Sélection périmée.");
            return;
        }
        _selection.Text = $"Rencontre choisie · {choice.Label}";
        _dependencies.Text =
            "La correction conserve candidats, récompenses, composition, scénario, statistiques, respawn, scheduler, " +
            "renforts, visibilité, cleanup et toutes les rencontres voisines.";
        PopulateDraft();
        ResetPreview("Authoriez une correction explicite, puis prévisualisez.");
    }

    private void PopulateDraft()
    {
        if (_draft is null) return;
        _updating = true;
        _anchors.Text = string.Join(", ", _draft.SpawnAnchorIds);
        _budget.Text = _draft.SpawnBudget.ToString("R", CultureInfo.InvariantCulture);
        _networkCap.Text = _draft.NetworkActorCap.ToString(CultureInfo.InvariantCulture);
        PopulateAnchorChoices();
        PopulateScriptChoices();
        _updating = false;
    }

    private void PopulateAnchorChoices()
    {
        _anchorSuggestion.Clear();
        _anchorEntries.Clear();
        _anchorSuggestion.AddItem("Choisissez une ancre réelle…");
        _anchorEntries.Add(null);
        if (_draft is null) return;
        foreach (var choice in _session.EncounterAnchorChoicesUx2LwL(_draft.OriginalProfile.AreaId))
        {
            _anchorSuggestion.AddItem($"{choice.Label} · {choice.Provenance}");
            _anchorEntries.Add(choice);
        }
        _anchorSuggestion.Select(0);
    }

    private void PopulateScriptChoices()
    {
        _script.Clear();
        _scriptEntries.Clear();
        _script.AddItem("Aucun script d’activation");
        _scriptEntries.Add(null);
        var current = _draft?.ActivationScriptId;
        var selected = 0;
        foreach (var choice in _session.EncounterScriptChoicesUx2LwL())
        {
            _script.AddItem($"{choice.Label} · {choice.Provenance}");
            _scriptEntries.Add(choice);
            if (string.Equals(choice.Id, current, StringComparison.Ordinal)) selected = _scriptEntries.Count - 1;
        }
        if (!string.IsNullOrWhiteSpace(current) && selected == 0)
        {
            _script.AddItem("Script actuel introuvable · correction requise");
            _scriptEntries.Add(new(current!, "Script actuel introuvable", "Référence du document"));
            selected = _scriptEntries.Count - 1;
        }
        _script.Select(selected);
    }

    private void UseSuggestedAnchor()
    {
        var index = _anchorSuggestion.Selected;
        if (index <= 0 || index >= _anchorEntries.Count || _anchorEntries[index] is not { } choice) return;
        _anchors.Text = choice.Id;
        SetStatus($"Ancre choisie · {choice.Label} · prévisualisez avant application.", false);
    }

    private bool TryReadDraft(out StudioUx2LwLDraft? draft)
    {
        draft = null;
        if (_draft is null) return false;
        if (!double.TryParse(_budget.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var budget))
        {
            SetStatus("Budget illisible : utilisez un nombre décimal brut.", true);
            return false;
        }
        if (!int.TryParse(_networkCap.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cap))
        {
            SetStatus("Cap réseau illisible : utilisez un entier brut.", true);
            return false;
        }
        var anchors = _anchors.Text.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();
        var scriptIndex = _script.Selected;
        var script = scriptIndex > 0 && scriptIndex < _scriptEntries.Count
            ? _scriptEntries[scriptIndex]?.Id
            : null;
        draft = _draft with
        {
            SpawnAnchorIds = anchors,
            ActivationScriptId = script,
            SpawnBudget = budget,
            NetworkActorCap = cap,
        };
        return true;
    }

    private void Preview()
    {
        if (!TryReadDraft(out var draft) || draft is null) return;
        _draft = draft;
        _lastPreview = _session.PreviewEncounterValidationUx2LwL(draft);
        _canonicalPreviewReady = _lastPreview.Succeeded && _lastPreview.CanApply;
        RenderPreview(_lastPreview);
        SetStatus(_lastPreview.Message, !_lastPreview.Succeeded);
    }

    private void RenderPreview(StudioUx2LwLPreview preview)
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
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 10, StudioStyle.Success));
        foreach (var item in preview.Diagnostics)
        {
            var color = item.Blocking ? StudioStyle.Danger : StudioStyle.Warning;
            _diagnostics.AddChild(StudioStyle.Label(
                $"{item.Section} · {item.Message} · code {item.Code} · emplacement {HumanPath(item.Path)}",
                10, color, wrap: true));
        }
        Clear(_coverage);
        foreach (var item in preview.Coverage)
        {
            var color = item.Evaluated ? StudioStyle.Success : StudioStyle.Warning;
            _coverage.AddChild(StudioStyle.Label($"{item.Section} · {item.Status} · {item.Detail}",
                10, color, wrap: true));
        }
        _boundary.Text = preview.Boundary;
        UpdateActions();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedEncounterValidationUx2LwL();
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
        _session.CancelEncounterValidationPreviewUx2LwL();
        _canonicalPreviewReady = false;
        _lastPreview = null;
        Clear(_diff);
        _diff.AddChild(StudioStyle.Label(
            "Aperçu précédent invalidé. Relancez la validation avec les nouvelles valeurs.",
            10, StudioStyle.Warning, wrap: true));
        Clear(_diagnostics);
        _diagnostics.AddChild(StudioStyle.Label("Validation en attente.", 10, StudioStyle.Muted));
        Clear(_coverage);
        _coverage.AddChild(StudioStyle.Label("Couverture recalculée au prochain aperçu.", 10, StudioStyle.Muted));
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
        Clear(_coverage);
        _coverage.AddChild(StudioStyle.Label(
            "Récompenses, conditions et plafonds globaux seront indiqués Non évalué tant qu’aucune autorité canonique n’existe.",
            10, StudioStyle.Warning, wrap: true));
        _boundary.Text =
            "Aperçu offline : aucune navigation, apparition, caméra, récompense, condition, suppression, simulation moteur ou opération réseau n’est exécutée.";
        SetStatus(message, false);
        UpdateActions();
    }

    private void UpdateActions()
    {
        _preview.Disabled = _draft is null;
        _apply.Disabled = _lastPreview?.CanApply != true;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
        _useAnchor.Disabled = _draft is null || _anchorSuggestion.Selected <= 0;
    }

    private StudioUx2LwLEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History ?? throw new InvalidOperationException("Historique source absent.");
        return new(_sourceSession.Project ?? throw new InvalidOperationException("Projet source absent."),
            history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureEncounterValidationApprovalUx2LwL());
    }

    private bool SourceMatchesSnapshot(StudioUx2LwLEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureEncounterValidationApprovalUx2LwL() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        var snapshot = _evidenceSnapshot;
        _evidenceSession?.CancelEncounterValidationPreviewUx2LwL();
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
        _lastEvidenceResourcesReleased = released &&
                                         (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root));
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

    private static string HumanPath(string path)
    {
        var segment = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "rencontre";
        if (Guid.TryParse(segment, out _)) segment = "élément protégé";
        return segment.Replace('-', ' ').Replace('_', ' ');
    }

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

internal static class StudioEncounterValidationEvidenceExtensionsUx2LwL
{
    internal static bool IsEvidenceUx2LwL(this Forge.Domain.WorldFeatures.EncounterWorldProfile profile) =>
        string.Equals(profile.Provenance, "studio:ux2-lw-l:evidence", StringComparison.Ordinal);
}
