using System.Globalization;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioEncounterTacticalOverlayUx2LwJ : Control
{
    private static readonly EncounterWorldProfile.VisibilityMode[] VisibilityModes =
        Enum.GetValues<EncounterWorldProfile.VisibilityMode>();

    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceTempRoot;
    private StudioUx2LwJEvidenceSourceSnapshot? _evidenceSnapshot;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _encounter;
    private readonly Label _selection;
    private readonly Label _foundation;
    private readonly CheckBox _reinforcementEnabled;
    private readonly LineEdit _alertLevel;
    private readonly OptionButton _faction;
    private readonly Label _factionProvenance;
    private readonly OptionButton _route;
    private readonly Label _routeProvenance;
    private readonly LineEdit _actorCap;
    private readonly LineEdit _reinforcementDelay;
    private readonly LineEdit _cancelCondition;
    private readonly OptionButton _visibilityMode;
    private readonly LineEdit _cameraDistance;
    private readonly CheckBox _requireLosBlocked;
    private readonly LineEdit _spawnEffect;
    private readonly LineEdit _deadAfter;
    private readonly LineEdit _escapedAfter;
    private readonly LineEdit _abandonedAfter;
    private readonly CheckBox _cleanupOnUnload;
    private readonly CheckBox _preservePersistent;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _reinforcementPreview;
    private readonly Label _visibilityPreview;
    private readonly Label _cleanupPreview;
    private readonly Label _boundary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2LwJEncounterChoice> _encounterChoices = [];
    private IReadOnlyList<StudioUx2LwJFactionChoice> _factionChoices = [];
    private IReadOnlyList<StudioUx2LwJRouteChoice> _routeChoices = [];
    private readonly List<StudioUx2LwJEncounterChoice?> _encounterEntries = [];
    private readonly List<string?> _factionSlugs = [];
    private readonly List<Guid?> _routeIds = [];
    private StudioUx2LwJDraft? _draft;
    private StudioUx2LwJPreview? _lastPreview;
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
        !_reinforcementPreview.Text.Contains("non évalué", StringComparison.OrdinalIgnoreCase) &&
        !_visibilityPreview.Text.Contains("non évaluée", StringComparison.OrdinalIgnoreCase) &&
        !_cleanupPreview.Text.Contains("non évalué", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHonestRuntimeBoundary =>
        _boundary.Text.Contains("acteur", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("caméra", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("LOS", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("supprimée", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("réseau", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
        _lastPreview.Diagnostics.All(item => !string.IsNullOrWhiteSpace(item.Code) &&
                                             !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesOnlyRealReferences => _draft is { ReinforcementEnabled: false } || _draft is { } draft &&
        draft.FactionSlug is not null && _factionChoices.Any(item => item.Slug == draft.FactionSlug) &&
        draft.RouteId is not null && _routeChoices.Any(item => item.Id == draft.RouteId);
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
            return text.Contains("Renforts et alertes", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Visibilité et caméra", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Nettoyage", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Fiche Foundation requise", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("aucun choix implicite", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsTechnicalIdentifier(text);
        }
    }

    internal StudioEncounterTacticalOverlayUx2LwJ(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioEncounterTacticalOverlayUx2LwJ";
        Visible = false;
        ZIndex = 479;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new(0, 0, 0, .84f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _dialog = StudioStyle.Panel("EncounterTacticalDialog", StudioStyle.Chrome, 12, 2);
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
        heading.AddChild(StudioStyle.Label("Renforts, visibilité et nettoyage", 20));
        heading.AddChild(StudioStyle.Label(
            "ENC-016/017/019 · décisions tactiques bornées · aucun spawn, trajet, échantillon caméra/LOS, " +
            "nettoyage moteur ou transport réseau n’est lancé ici",
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
            CustomMaximumSize = new(-1, 790),
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
        content.AddChild(Card("EncounterTacticalDependency", "Fiche Foundation requise",
            "Cette surface ne synthétise ni rencontre, ni faction, ni itinéraire. Aucun choix implicite n’est effectué.",
            dependencyBody));

        _reinforcementEnabled = Check("Configurer les renforts optionnels");
        _alertLevel = NumberLine("2");
        _faction = Option();
        _faction.ItemSelected += _ => { UpdateReferenceProvenance(); DraftChanged(); };
        _factionProvenance = StudioStyle.Label("Choisissez une faction active du module.",
            10, StudioStyle.Muted, wrap: true);
        _route = Option();
        _route.ItemSelected += _ => { UpdateReferenceProvenance(); DraftChanged(); };
        _routeProvenance = StudioStyle.Label("Choisissez un itinéraire réel de la zone.",
            10, StudioStyle.Muted, wrap: true);
        _actorCap = NumberLine("4");
        _reinforcementDelay = NumberLine("3");
        _cancelCondition = Line("boss.defeated");
        var reinforcementGrid = Grid();
        AddRow(reinforcementGrid, "Politique", _reinforcementEnabled);
        AddRow(reinforcementGrid, "Niveau d’alerte minimum", _alertLevel);
        AddRow(reinforcementGrid, "Faction réelle", _faction);
        AddRow(reinforcementGrid, "Provenance faction", _factionProvenance);
        AddRow(reinforcementGrid, "Itinéraire réel", _route);
        AddRow(reinforcementGrid, "Provenance itinéraire", _routeProvenance);
        AddRow(reinforcementGrid, "Plafond d’acteurs", _actorCap);
        AddRow(reinforcementGrid, "Délai en secondes", _reinforcementDelay);
        AddRow(reinforcementGrid, "Condition d’annulation", _cancelCondition);
        content.AddChild(Card("EncounterReinforcements", "Renforts et alertes",
            "La façade autorise une référence de faction et de trajet déjà écrite. Elle ne crée, ne déplace et ne réplique aucun acteur.",
            reinforcementGrid));

        _visibilityMode = Option();
        foreach (var mode in VisibilityModes) _visibilityMode.AddItem(HumanMode(mode.ToString()));
        _visibilityMode.ItemSelected += _ => DraftChanged();
        _cameraDistance = NumberLine("10");
        _requireLosBlocked = Check("Exiger une ligne de vue bloquée observée");
        _spawnEffect = Line("vfx.spawn.embers");
        var visibilityGrid = Grid();
        AddRow(visibilityGrid, "Mode", _visibilityMode);
        AddRow(visibilityGrid, "Distance caméra minimum", _cameraDistance);
        AddRow(visibilityGrid, "Ligne de vue", _requireLosBlocked);
        AddRow(visibilityGrid, "Effet de spawn", _spawnEffect);
        content.AddChild(Card("EncounterVisibility", "Visibilité et caméra",
            "Le preview consomme distance, visibilité et LOS fournies par l’adaptateur ; il ne lit ni caméra ni physics world.",
            visibilityGrid));

        _deadAfter = NumberLine("30");
        _escapedAfter = NumberLine("45");
        _abandonedAfter = NumberLine("120");
        _cleanupOnUnload = Check("Rendre éligible au déchargement de zone");
        _preservePersistent = Check("Toujours préserver les acteurs persistants");
        var cleanupGrid = Grid();
        AddRow(cleanupGrid, "Mort après secondes", _deadAfter);
        AddRow(cleanupGrid, "Fuite après secondes", _escapedAfter);
        AddRow(cleanupGrid, "Abandon après secondes", _abandonedAfter);
        AddRow(cleanupGrid, "Déchargement", _cleanupOnUnload);
        AddRow(cleanupGrid, "Persistants", _preservePersistent);
        content.AddChild(Card("EncounterCleanup", "Nettoyage",
            "La politique marque une entité éligible ; aucune suppression moteur, libération de ressource ou décision réseau n’est exécutée.",
            cleanupGrid));

        _diff = new();
        _diagnostics = new();
        _reinforcementPreview = PreviewLabel("Renforts non évalués");
        _visibilityPreview = PreviewLabel("Visibilité non évaluée");
        _cleanupPreview = PreviewLabel("Cleanup non évalué");
        _boundary = StudioStyle.Label(
            "Aucun spawn, trajet, caméra, LOS, nettoyage moteur ou transport réseau n’a été exécuté.",
            10, StudioStyle.Warning, wrap: true);
        var previewBody = new VBoxContainer();
        previewBody.AddChild(_diff);
        previewBody.AddChild(_diagnostics);
        previewBody.AddChild(_reinforcementPreview);
        previewBody.AddChild(_visibilityPreview);
        previewBody.AddChild(_cleanupPreview);
        previewBody.AddChild(_boundary);
        content.AddChild(Card("EncounterTacticalPreview", "Aperçu sec canonique",
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

        _reinforcementEnabled.Toggled += _ => { UpdateReinforcementState(); DraftChanged(); };
        _alertLevel.TextChanged += _ => DraftChanged();
        _actorCap.TextChanged += _ => DraftChanged();
        _reinforcementDelay.TextChanged += _ => DraftChanged();
        _cancelCondition.TextChanged += _ => DraftChanged();
        _cameraDistance.TextChanged += _ => DraftChanged();
        _requireLosBlocked.Toggled += _ => DraftChanged();
        _spawnEffect.TextChanged += _ => DraftChanged();
        _deadAfter.TextChanged += _ => DraftChanged();
        _escapedAfter.TextChanged += _ => DraftChanged();
        _abandonedAfter.TextChanged += _ => DraftChanged();
        _cleanupOnUnload.Toggled += _ => DraftChanged();
        _preservePersistent.Toggled += _ => DraftChanged();
        Resized += FitDialog;
        ResetPreview("Choisissez une rencontre.");
    }

    internal void Open()
    {
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        _session.CancelEncounterTacticalPreviewUx2LwJ();
        Visible = true;
        Reload(clearSelection: true);
        FitDialog();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (_evidenceSession is not null) ReleaseEvidence();
        else _session.CancelEncounterTacticalPreviewUx2LwJ();
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
        var project = _sourceSession.CreateUx2LwJEvidenceProject();
        if (project is null) return false;
        _lastEvidenceSourceUntouched = false;
        _lastEvidenceResourcesReleased = false;
        _lastSourceRestored = false;
        _evidenceSnapshot = CaptureSourceSnapshot();
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-j-evidence-" + Guid.NewGuid().ToString("N"));
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
                MinimumAlertLevel = _draft.MinimumAlertLevel + 1,
                MaximumActors = _draft.MaximumActors + 1,
                MinimumCameraDistance = _draft.MinimumCameraDistance + .5,
                DeadAfterSeconds = _draft.DeadAfterSeconds + .5,
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
            var applied = _session.ApplyApprovedEncounterTacticalUx2LwJ();
            if (!applied.Succeeded) return false;
            var after = _session.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId);
            var changed = before.Reinforcements != after.Reinforcements ||
                          before.Visibility != after.Visibility || before.Cleanup != after.Cleanup;
            var undo = _session.Undo();
            var undone = _session.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId);
            var redo = _session.Redo();
            var redone = _session.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId);
            if (!changed || !undo.Succeeded || !redo.Succeeded || undone != before || redone != after)
                return false;
            var saved = _session.Save();
            if (!saved.Succeeded) return false;
            reopened = new StudioSessionController();
            if (!reopened.Open(_evidenceTempRoot).Succeeded) return false;
            var persisted = reopened.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId);
            return persisted.Reinforcements == after.Reinforcements &&
                   persisted.Visibility == after.Visibility && persisted.Cleanup == after.Cleanup &&
                   ProjectJsonSerializer.Serialize(_session.Project!) == ProjectJsonSerializer.Serialize(reopened.Project!);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_J_UI_TRANSACTION_FAILED {exception.Message}");
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
        _encounterChoices = _session.EncounterChoicesUx2LwJ;
        _factionChoices = _session.EncounterFactionChoicesUx2LwJ();
        _routeChoices = _session.EncounterRouteChoicesUx2LwJ();
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
        FillRoutes(null);
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
        _session.CancelEncounterTacticalPreviewUx2LwJ();
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
        _draft = _session.CreateEncounterTacticalDraftUx2LwJ(choice.ProfileId);
        if (_draft is null)
        {
            _selection.Text = "La rencontre n’est plus disponible. Rechargez la liste.";
            ResetPreview("Sélection périmée.");
            return;
        }
        _selection.Text = $"Rencontre choisie · {choice.Label}";
        _foundation.Text =
            "Respawn, faction de spawn, scheduler, Foundation, composition, scénario et statistiques restent intacts.";
        PopulateDraft();
        ResetPreview("Modifiez une politique puis prévisualisez.");
    }

    private void PopulateDraft()
    {
        if (_draft is null) return;
        _updating = true;
        _reinforcementEnabled.ButtonPressed = _draft.ReinforcementEnabled;
        _alertLevel.Text = _draft.MinimumAlertLevel.ToString(CultureInfo.InvariantCulture);
        FillFactions(_draft.FactionSlug);
        FillRoutes(_draft.RouteId);
        _actorCap.Text = _draft.MaximumActors.ToString(CultureInfo.InvariantCulture);
        _reinforcementDelay.Text = _draft.ReinforcementDelaySeconds.ToString("R", CultureInfo.InvariantCulture);
        _cancelCondition.Text = _draft.CancelCondition;
        _visibilityMode.Select(Math.Max(0, Array.IndexOf(VisibilityModes, _draft.VisibilityMode)));
        _cameraDistance.Text = _draft.MinimumCameraDistance.ToString("R", CultureInfo.InvariantCulture);
        _requireLosBlocked.ButtonPressed = _draft.RequireLineOfSightBlocked;
        _spawnEffect.Text = _draft.SpawnEffectId;
        _deadAfter.Text = _draft.DeadAfterSeconds.ToString("R", CultureInfo.InvariantCulture);
        _escapedAfter.Text = _draft.EscapedAfterSeconds.ToString("R", CultureInfo.InvariantCulture);
        _abandonedAfter.Text = _draft.AbandonedAfterSeconds.ToString("R", CultureInfo.InvariantCulture);
        _cleanupOnUnload.ButtonPressed = _draft.CleanupOnAreaUnload;
        _preservePersistent.ButtonPressed = _draft.PreservePersistentActors;
        UpdateReferenceProvenance();
        UpdateReinforcementState();
        _updating = false;
    }

    private bool TryReadDraft(out StudioUx2LwJDraft? draft)
    {
        draft = null;
        if (_draft is null) return false;
        var alert = ParseInt(_alertLevel.Text, -1);
        var cap = ParseInt(_actorCap.Text, 0);
        var delay = ParseDouble(_reinforcementDelay.Text);
        var camera = ParseDouble(_cameraDistance.Text);
        var dead = ParseDouble(_deadAfter.Text);
        var escaped = ParseDouble(_escapedAfter.Text);
        var abandoned = ParseDouble(_abandonedAfter.Text);
        var modeIndex = _visibilityMode.Selected;
        var mode = modeIndex >= 0 && modeIndex < VisibilityModes.Length
            ? VisibilityModes[modeIndex]
            : (EncounterWorldProfile.VisibilityMode)999;
        draft = _draft with
        {
            ReinforcementEnabled = _reinforcementEnabled.ButtonPressed,
            MinimumAlertLevel = alert,
            FactionSlug = SelectedFactionSlug(),
            RouteId = SelectedRouteId(),
            MaximumActors = cap,
            ReinforcementDelaySeconds = delay,
            CancelCondition = _cancelCondition.Text,
            VisibilityMode = mode,
            MinimumCameraDistance = camera,
            RequireLineOfSightBlocked = _requireLosBlocked.ButtonPressed,
            SpawnEffectId = _spawnEffect.Text,
            DeadAfterSeconds = dead,
            EscapedAfterSeconds = escaped,
            AbandonedAfterSeconds = abandoned,
            CleanupOnAreaUnload = _cleanupOnUnload.ButtonPressed,
            PreservePersistentActors = _preservePersistent.ButtonPressed,
        };
        return true;
    }

    private void Preview()
    {
        if (!TryReadDraft(out var draft) || draft is null) return;
        _draft = draft;
        _lastPreview = _session.PreviewEncounterTacticalUx2LwJ(draft);
        _canonicalPreviewReady = _lastPreview.Succeeded;
        RenderPreview(_lastPreview);
        SetStatus(_lastPreview.Message, !_lastPreview.Succeeded);
    }

    private void RenderPreview(StudioUx2LwJPreview preview)
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
        _reinforcementPreview.Text = preview.Runtime.Reinforcement;
        _visibilityPreview.Text = preview.Runtime.Visibility;
        _cleanupPreview.Text = preview.Runtime.Cleanup;
        _boundary.Text = preview.Runtime.Boundary;
        UpdateActions();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedEncounterTacticalUx2LwJ();
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
        _session.CancelEncounterTacticalPreviewUx2LwJ();
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
        _reinforcementPreview.Text = "Renforts non évalués";
        _visibilityPreview.Text = "Visibilité non évaluée";
        _cleanupPreview.Text = "Cleanup non évalué";
        _boundary.Text = "Aucun spawn, trajet, caméra, LOS, nettoyage moteur ou transport réseau n’a été exécuté.";
    }

    private void UpdateActions()
    {
        _preview.Disabled = _draft is null;
        _apply.Disabled = _lastPreview?.CanApply != true;
        _undo.Disabled = !_session.CanUndo;
        _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
    }

    private void UpdateReinforcementState()
    {
        var enabled = _reinforcementEnabled.ButtonPressed;
        _alertLevel.Editable = enabled;
        _faction.Disabled = !enabled;
        _route.Disabled = !enabled;
        _actorCap.Editable = enabled;
        _reinforcementDelay.Editable = enabled;
        _cancelCondition.Editable = enabled;
        UpdateReferenceProvenance();
    }

    private void FillFactions(string? selected)
    {
        _faction.Clear();
        _factionSlugs.Clear();
        _faction.AddItem("Choisir explicitement une faction…");
        _factionSlugs.Add(null);
        foreach (var choice in _factionChoices)
        {
            _faction.AddItem(choice.Label);
            _factionSlugs.Add(choice.Slug);
        }
        var index = selected is null ? 0 : _factionSlugs.FindIndex(item => item == selected);
        if (selected is not null && index < 0)
        {
            _faction.AddItem("Faction enregistrée indisponible · corriger avant application");
            _factionSlugs.Add(selected);
            index = _factionSlugs.Count - 1;
        }
        _faction.Select(Math.Max(0, index));
    }

    private void FillRoutes(Guid? selected)
    {
        _route.Clear();
        _routeIds.Clear();
        _route.AddItem("Choisir explicitement un itinéraire…");
        _routeIds.Add(null);
        foreach (var choice in _routeChoices)
        {
            _route.AddItem(choice.Label);
            _routeIds.Add(choice.Id);
        }
        var index = selected is null ? 0 : _routeIds.FindIndex(item => item == selected);
        if (selected is not null && index < 0)
        {
            _route.AddItem("Itinéraire enregistré indisponible · corriger avant application");
            _routeIds.Add(selected);
            index = _routeIds.Count - 1;
        }
        _route.Select(Math.Max(0, index));
    }

    private string? SelectedFactionSlug() => _faction.Selected >= 0 && _faction.Selected < _factionSlugs.Count
        ? _factionSlugs[_faction.Selected]
        : null;

    private Guid? SelectedRouteId() => _route.Selected >= 0 && _route.Selected < _routeIds.Count
        ? _routeIds[_route.Selected]
        : null;

    private void UpdateReferenceProvenance()
    {
        var faction = SelectedFactionSlug();
        var factionChoice = faction is null ? null : _factionChoices.FirstOrDefault(item => item.Slug == faction);
        _factionProvenance.Text = faction is null
            ? "Aucune faction choisie · aucun fallback implicite."
            : factionChoice is null
                ? "Référence enregistrée absente du module · application refusée."
                : $"{factionChoice.Provenance} · {factionChoice.Label} · aucune affectation exécutée.";
        var route = SelectedRouteId();
        var routeChoice = route is null ? null : _routeChoices.FirstOrDefault(item => item.Id == route);
        _routeProvenance.Text = route is null
            ? "Aucun itinéraire choisi · aucun fallback implicite."
            : routeChoice is null
                ? "Référence enregistrée absente de la zone · application refusée."
                : $"{routeChoice.Provenance} · {routeChoice.PointCount} étape(s) · aucun déplacement exécuté.";
    }

    private StudioUx2LwJEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History ?? throw new InvalidOperationException("Historique source absent.");
        return new(_sourceSession.Project ?? throw new InvalidOperationException("Projet source absent."),
            history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureEncounterTacticalApprovalUx2LwJ());
    }

    private bool SourceMatchesSnapshot(StudioUx2LwJEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureEncounterTacticalApprovalUx2LwJ() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        var snapshot = _evidenceSnapshot;
        _evidenceSession?.CancelEncounterTacticalPreviewUx2LwJ();
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

    private static int ParseInt(string value, int invalid) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : invalid;

    private static double ParseDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : double.NaN;

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
        path.Contains("/reinforcements", StringComparison.Ordinal) ? "Renforts" :
        path.Contains("/visibility", StringComparison.Ordinal) ? "Visibilité" :
        path.Contains("/cleanup", StringComparison.Ordinal) ? "Nettoyage" : "Fiche de rencontre";

    private static string HumanMode(string mode) => mode.Replace('-', ' ').Replace('_', ' ');

    private void FitDialog()
    {
        if (!Visible) return;
        var viewport = GetViewportRect().Size;
        var width = Mathf.Clamp(viewport.X - 48, 900, 1360);
        var height = Mathf.Clamp(viewport.Y - 36, 720, 1060);
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
