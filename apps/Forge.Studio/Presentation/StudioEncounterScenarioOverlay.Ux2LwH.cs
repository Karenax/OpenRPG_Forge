using System.Globalization;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioEncounterScenarioOverlayUx2LwH : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceTempRoot;
    private StudioUx2LwHEvidenceSourceSnapshot? _evidenceSnapshot;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _encounter;
    private readonly Label _selection;
    private readonly VBoxContainer _creationCard;
    private readonly OptionButton _creationAnchor;
    private readonly OptionButton _creationGroup;
    private readonly Button _createScenario;
    private readonly OptionButton _distribution;
    private readonly ItemList _anchors;
    private readonly OptionButton _anchorReference;
    private readonly LineEdit _positionX;
    private readonly LineEdit _positionY;
    private readonly LineEdit _positionZ;
    private readonly LineEdit _rotationX;
    private readonly LineEdit _rotationY;
    private readonly LineEdit _rotationZ;
    private readonly LineEdit _rotationW;
    private readonly LineEdit _scaleX;
    private readonly LineEdit _scaleY;
    private readonly LineEdit _scaleZ;
    private readonly LineEdit _anchorCapacity;
    private readonly CheckBox _anchorNavigable;
    private readonly OptionButton _anchorVisibility;
    private readonly CheckBox _anchorFallback;
    private readonly LineEdit _anchorWeight;
    private readonly Button _addAnchor;
    private readonly Button _updateAnchor;
    private readonly Button _removeAnchor;
    private readonly CheckBox _playersOnly;
    private readonly CheckBox _ignoreSummons;
    private readonly CheckBox _ignoreCompanions;
    private readonly CheckBox _ignoreGameMasters;
    private readonly CheckBox _ignoreSpectators;
    private readonly OptionButton _authority;
    private readonly LineEdit _requiredTags;
    private readonly LineEdit _apiVersion;
    private readonly OptionButton _enteredScript;
    private readonly OptionButton _exitedScript;
    private readonly OptionButton _exhaustedScript;
    private readonly ItemList _phases;
    private readonly LineEdit _phaseOrder;
    private readonly LineEdit _phaseName;
    private readonly LineEdit _phaseConditions;
    private readonly Label _reinforcements;
    private readonly Button _addPhase;
    private readonly Button _updatePhase;
    private readonly Button _removePhase;
    private readonly ItemList _waves;
    private readonly LineEdit _waveOrder;
    private readonly ItemList _waveGroups;
    private readonly LineEdit _waveDelay;
    private readonly OptionButton _waveCompletion;
    private readonly LineEdit _completionTarget;
    private readonly OptionButton _quest;
    private readonly OptionButton _objective;
    private readonly OptionButton _voice;
    private readonly OptionButton _nextWave;
    private readonly Button _addWave;
    private readonly Button _updateWave;
    private readonly Button _removeWave;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _anchorPreview;
    private readonly Label _activatorPreview;
    private readonly Label _lifecyclePreview;
    private readonly Label _wavePreview;
    private readonly Label _boundary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2LwHEncounterChoice> _encounterChoices = [];
    private IReadOnlyList<StudioUx2LwHReferenceChoice<string>> _anchorChoices = [];
    private IReadOnlyList<StudioUx2LwHReferenceChoice<Guid>> _groupChoices = [];
    private IReadOnlyList<StudioUx2LwHReferenceChoice<string>> _scriptChoices = [];
    private IReadOnlyList<StudioUx2LwHQuestChoice> _questChoices = [];
    private IReadOnlyList<StudioUx2LwHReferenceChoice<string>> _audioChoices = [];
    private readonly List<StudioUx2LwHEncounterChoice?> _encounterEntries = [];
    private readonly List<string?> _creationAnchorIds = [];
    private readonly List<Guid?> _creationGroupIds = [];
    private readonly List<string?> _anchorReferenceIds = [];
    private readonly List<string?> _enteredScriptIds = [];
    private readonly List<string?> _exitedScriptIds = [];
    private readonly List<string?> _exhaustedScriptIds = [];
    private readonly List<Guid?> _questIds = [];
    private readonly List<string?> _objectiveIds = [];
    private readonly List<string?> _voiceIds = [];
    private readonly List<Guid?> _nextWaveIds = [];
    private StudioUx2LwHEncounterChoice? _selectedChoice;
    private StudioUx2LwHDraft? _draft;
    private StudioUx2LwHPreview? _lastPreview;
    private int _anchorIndex = -1;
    private int _phaseIndex = -1;
    private int _waveIndex = -1;
    private bool _updating;
    private bool _canonicalPreviewReady;
    private bool _lastEvidenceSourceUntouched;
    private bool _lastEvidenceResourcesReleased;
    private bool _lastSourceRestored = true;

    internal event Action<string, bool>? StatusChanged;
    internal event Action? EncounterChanged;

    internal bool IsOpen => Visible;
    internal int ChoiceCount => _encounterChoices.Count;
    internal bool HasExplicitSelection => _selectedChoice is not null && _encounter.Selected > 0 && _draft is not null;
    internal bool CanonicalPreviewReady => _canonicalPreviewReady && _lastPreview?.Succeeded == true;
    internal bool ShowsFiveRoutes => _lastPreview is not null &&
        !_anchorPreview.Text.Contains("non évaluée", StringComparison.OrdinalIgnoreCase) &&
        !_activatorPreview.Text.Contains("non évalué", StringComparison.OrdinalIgnoreCase) &&
        !_lifecyclePreview.Text.Contains("non évalué", StringComparison.OrdinalIgnoreCase) &&
        !_wavePreview.Text.Contains("non évaluée", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHonestRuntimeBoundary =>
        _boundary.Text.Contains("gizmo", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("ForgeScript", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("quête", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("audio", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("spawn", StringComparison.OrdinalIgnoreCase) &&
        _boundary.Text.Contains("réseau", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
        _lastPreview.Diagnostics.All(item => !string.IsNullOrWhiteSpace(item.Code) &&
                                             !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesRealReferences
    {
        get
        {
            var draft = _draft;
            if (draft is null || _anchorChoices.Count == 0 || _groupChoices.Count == 0) return false;
            var waves = draft.Phases.SelectMany(item => item.Waves).ToArray();
            var waveIds = waves.Select(item => item.Id).ToHashSet();
            return draft.SpawnAnchors.All(anchor => _anchorChoices.Any(item => item.Id == anchor.AnchorId)) &&
                   waves.SelectMany(item => item.GroupIds)
                       .All(group => _groupChoices.Any(item => item.Id == group)) &&
                   new[]
                   {
                       draft.Lifecycle.Entered.ScriptId,
                       draft.Lifecycle.Exited.ScriptId,
                       draft.Lifecycle.Exhausted.ScriptId,
                   }.All(script => script is null || _scriptChoices.Any(item => item.Id == script)) &&
                   waves.All(wave => wave.VoiceCueId is null ||
                                     _audioChoices.Any(item => item.Id == wave.VoiceCueId)) &&
                   waves.All(wave => wave.NextWaveId is null || waveIds.Contains(wave.NextWaveId.Value)) &&
                   waves.All(ObjectiveReferenceIsAvailable);
        }
    }
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
            return text.Contains("Points de spawn manuels", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Restriction joueurs", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("OnEnter, OnExit et OnExhausted", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Phases, vagues et actions", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsTechnicalIdentifier(text);
        }
    }

    internal StudioEncounterScenarioOverlayUx2LwH(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioEncounterScenarioOverlayUx2LwH";
        Visible = false; ZIndex = 477; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color(0, 0, 0, .84f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        _dialog = StudioStyle.Panel("EncounterScenarioDialog", StudioStyle.Chrome, 12, 2);
        _dialog.ClipContents = true; _dialog.PropagateMaximumSize = true; AddChild(_dialog);
        var margin = StudioStyle.Margin(18, 14, 18, 14); margin.PropagateMaximumSize = true;
        _dialog.AddChild(margin);
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill, PropagateMaximumSize = true };
        root.AddThemeConstantOverride("separation", 8); margin.AddChild(root);

        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; root.AddChild(header);
        var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("MONDE VIVANT · RENCONTRES", 10, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label("Scénario de rencontre", 20));
        heading.AddChild(StudioStyle.Label(
            "ENC-009/011/012/013/015 · ancres, activateur, lifecycle et listes de vagues uniquement · " +
            "aucun gizmo, moteur de quête, audio, script, spawn ou réseau n’est exécuté ici",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true); close.Pressed += Close; header.AddChild(close);
        _encounter = Option(); _encounter.ItemSelected += index => SelectEncounter((int)index); root.AddChild(_encounter);
        _selection = StudioStyle.Label("Choisissez explicitement une rencontre complète.",
            11, StudioStyle.Warning, wrap: true); root.AddChild(_selection);

        var bodyHost = new Control { CustomMinimumSize = new Vector2(0, 330),
            CustomMaximumSize = new Vector2(-1, 620), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true, PropagateMaximumSize = true };
        root.AddChild(bodyHost);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); bodyHost.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 9); scroll.AddChild(content);

        _creationAnchor = Option(); _creationGroup = Option();
        _createScenario = StudioStyle.Button("Créer le scénario avec ces références", compact: true);
        _createScenario.Pressed += CreateScenario;
        var creationGrid = Grid(); AddRow(creationGrid, "Ancre réelle initiale", _creationAnchor);
        AddRow(creationGrid, "Groupe LW-F initial", _creationGroup); AddRow(creationGrid, "Action", _createScenario);
        _creationCard = new VBoxContainer(); _creationCard.AddChild(Card("ScenarioCreation",
            "Créer un scénario explicitement",
            "Aucune ancre ni composition n’est choisie implicitement. Les valeurs externes restent celles du projet.",
            creationGrid));
        _creationCard.Visible = false; content.AddChild(_creationCard);

        _distribution = EnumOption(Enum.GetValues<EncounterSpawnDistribution>(),
            StudioSessionController.HumanDistributionUx2LwH);
        _anchors = List(single: true, height: 100); _anchors.ItemSelected += index => SelectAnchor((int)index);
        _anchorReference = Option();
        _positionX = NumberLine("0"); _positionY = NumberLine("0"); _positionZ = NumberLine("0");
        _rotationX = NumberLine("0"); _rotationY = NumberLine("0"); _rotationZ = NumberLine("0");
        _rotationW = NumberLine("1"); _scaleX = NumberLine("1"); _scaleY = NumberLine("1");
        _scaleZ = NumberLine("1"); _anchorCapacity = NumberLine("1");
        _anchorNavigable = Check("Ancre navigable");
        _anchorVisibility = EnumOption(Enum.GetValues<EncounterWorldProfile.VisibilityMode>(),
            StudioSessionController.HumanVisibilityUx2LwH);
        _anchorFallback = Check("Fallback unique"); _anchorWeight = NumberLine("1");
        _addAnchor = StudioStyle.Button("Ajouter l’ancre choisie", compact: true);
        _updateAnchor = StudioStyle.Button("Mettre à jour l’ancre", compact: true);
        _removeAnchor = StudioStyle.Button("Retirer l’ancre", compact: true);
        _addAnchor.Pressed += AddAnchor; _updateAnchor.Pressed += UpdateAnchor; _removeAnchor.Pressed += RemoveAnchor;
        var anchorButtons = Buttons(_addAnchor, _updateAnchor, _removeAnchor);
        var anchorGrid = Grid(); AddRow(anchorGrid, "Répartition", _distribution); AddRow(anchorGrid, "Ancres", _anchors);
        AddRow(anchorGrid, "Référence réelle", _anchorReference);
        AddRow(anchorGrid, "Position X / Y / Z", Three(_positionX, _positionY, _positionZ));
        AddRow(anchorGrid, "Orientation X / Y / Z / W", Four(_rotationX, _rotationY, _rotationZ, _rotationW));
        AddRow(anchorGrid, "Échelle X / Y / Z", Three(_scaleX, _scaleY, _scaleZ));
        AddRow(anchorGrid, "Capacité", _anchorCapacity); AddRow(anchorGrid, "Navigation", _anchorNavigable);
        AddRow(anchorGrid, "Visibilité", _anchorVisibility); AddRow(anchorGrid, "Fallback", _anchorFallback);
        AddRow(anchorGrid, "Poids", _anchorWeight); AddRow(anchorGrid, "Actions", anchorButtons);
        content.AddChild(Card("ScenarioAnchors", "Points de spawn manuels",
            "Transform, capacité, navigation, visibilité, fallback et poids sont persistés. Aucun gizmo ni nav bake n’est créé.",
            anchorGrid));

        _playersOnly = Check("Joueurs uniquement"); _ignoreSummons = Check("Ignorer les invocations");
        _ignoreCompanions = Check("Ignorer les compagnons"); _ignoreGameMasters = Check("Ignorer les MJ");
        _ignoreSpectators = Check("Ignorer les spectateurs");
        _authority = EnumOption(Enum.GetValues<EncounterNetworkAuthority>(),
            StudioSessionController.HumanAuthorityUx2LwH);
        _requiredTags = Line("party, zone-ready");
        var activatorGrid = Grid(); AddRow(activatorGrid, "Cible", _playersOnly);
        AddRow(activatorGrid, "Invocations", _ignoreSummons); AddRow(activatorGrid, "Compagnons", _ignoreCompanions);
        AddRow(activatorGrid, "Maîtres de jeu", _ignoreGameMasters); AddRow(activatorGrid, "Spectateurs", _ignoreSpectators);
        AddRow(activatorGrid, "Autorité requise", _authority); AddRow(activatorGrid, "Tags requis", _requiredTags);
        content.AddChild(Card("ScenarioActivator", "Restriction joueurs",
            "Cette politique vérifie un profil d’activateur en mémoire ; elle ne démarre aucun serveur multijoueur.",
            activatorGrid));

        _apiVersion = Line("1.0.0"); _enteredScript = Option(); _exitedScript = Option(); _exhaustedScript = Option();
        var lifecycleGrid = Grid(); AddRow(lifecycleGrid, "Version API ForgeScript", _apiVersion);
        AddRow(lifecycleGrid, "EncounterEntered", _enteredScript); AddRow(lifecycleGrid, "EncounterExited", _exitedScript);
        AddRow(lifecycleGrid, "EncounterExhausted", _exhaustedScript);
        content.AddChild(Card("ScenarioLifecycle", "OnEnter, OnExit et OnExhausted",
            "Les bindings sont fixes et versionnés. Les scripts proposés proviennent du workspace, mais ne sont jamais exécutés dans l’aperçu.",
            lifecycleGrid));

        _phases = List(single: true, height: 90); _phases.ItemSelected += index => SelectPhase((int)index);
        _phaseOrder = NumberLine("1"); _phaseName = Line("Ouverture"); _phaseConditions = Line("area.occupied");
        _reinforcements = StudioStyle.Label("Aucun renfort · ENC-016 hors périmètre", 10, StudioStyle.Muted, wrap: true);
        _addPhase = StudioStyle.Button("Ajouter une phase", compact: true);
        _updatePhase = StudioStyle.Button("Mettre à jour la phase", compact: true);
        _removePhase = StudioStyle.Button("Retirer la phase", compact: true);
        _addPhase.Pressed += AddPhase; _updatePhase.Pressed += UpdatePhase; _removePhase.Pressed += RemovePhase;
        var phaseGrid = Grid(); AddRow(phaseGrid, "Phases", _phases); AddRow(phaseGrid, "Ordre", _phaseOrder);
        AddRow(phaseGrid, "Nom", _phaseName); AddRow(phaseGrid, "Conditions d’entrée", _phaseConditions);
        AddRow(phaseGrid, "Renforts préservés", _reinforcements);
        AddRow(phaseGrid, "Actions phase", Buttons(_addPhase, _updatePhase, _removePhase));

        _waves = List(single: true, height: 90); _waves.ItemSelected += index => SelectWave((int)index);
        _waveOrder = NumberLine("1"); _waveGroups = List(single: false, height: 90);
        _waveDelay = NumberLine("0");
        _waveCompletion = EnumOption(Enum.GetValues<EncounterWaveCompletionPolicy>(),
            StudioSessionController.HumanCompletionUx2LwH);
        _completionTarget = Line("binding de complétion"); _quest = Option(); _objective = Option();
        _voice = Option(); _nextWave = Option();
        _quest.ItemSelected += _ => { if (!_updating) FillObjectives(null); DraftChanged(); };
        _waveCompletion.ItemSelected += _ => { UpdateWaveFieldState(); DraftChanged(); };
        _addWave = StudioStyle.Button("Ajouter une vague", compact: true);
        _updateWave = StudioStyle.Button("Mettre à jour la vague", compact: true);
        _removeWave = StudioStyle.Button("Retirer la vague", compact: true);
        _addWave.Pressed += AddWave; _updateWave.Pressed += UpdateWave; _removeWave.Pressed += RemoveWave;
        var graphBody = new VBoxContainer(); graphBody.AddChild(phaseGrid);
        var waveGrid = Grid(); AddRow(waveGrid, "Vagues de la phase", _waves); AddRow(waveGrid, "Ordre", _waveOrder);
        AddRow(waveGrid, "Groupes LW-F", _waveGroups); AddRow(waveGrid, "Délai", _waveDelay);
        AddRow(waveGrid, "Complétion", _waveCompletion); AddRow(waveGrid, "Cible de binding", _completionTarget);
        AddRow(waveGrid, "Quête réelle", _quest); AddRow(waveGrid, "Objectif", _objective);
        AddRow(waveGrid, "Repère audio", _voice); AddRow(waveGrid, "Transition suivante", _nextWave);
        AddRow(waveGrid, "Actions vague", Buttons(_addWave, _updateWave, _removeWave)); graphBody.AddChild(waveGrid);
        content.AddChild(Card("ScenarioGraph", "Phases, vagues et actions",
            "Éditeur de listes uniquement : groupes, délais, complétion, quête, voix et transition. " +
            "Aucun graphe universel, moteur de quête ou mixer audio n’est ouvert.", graphBody));

        _diff = new VBoxContainer(); _diagnostics = new VBoxContainer();
        _anchorPreview = StudioStyle.Label("Ancres non évaluées", 10, StudioStyle.Muted, wrap: true);
        _activatorPreview = StudioStyle.Label("Activateur non évalué", 10, StudioStyle.Muted, wrap: true);
        _lifecyclePreview = StudioStyle.Label("Lifecycle non évalué", 10, StudioStyle.Muted, wrap: true);
        _wavePreview = StudioStyle.Label("Vagues non évaluées", 10, StudioStyle.Muted, wrap: true);
        _boundary = StudioStyle.Label(
            "Aucun gizmo, nav bake, ForgeScript, quête, audio, spawn d’acteur ou transport réseau n’est exécuté.",
            10, StudioStyle.Warning, wrap: true);
        var previewBody = new VBoxContainer(); previewBody.AddChild(_diff); previewBody.AddChild(_diagnostics);
        previewBody.AddChild(_anchorPreview); previewBody.AddChild(_activatorPreview);
        previewBody.AddChild(_lifecyclePreview); previewBody.AddChild(_wavePreview); previewBody.AddChild(_boundary);
        content.AddChild(Card("ScenarioPreview", "Aperçu sec canonique",
            "Le diff humain précède l’application ; codes et localisations canoniques restent structurés.", previewBody));

        var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _preview = StudioStyle.Button("Prévisualiser", compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true); _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        _preview.Pressed += Preview; _apply.Pressed += Apply; _undo.Pressed += Undo;
        _redo.Pressed += Redo; _save.Pressed += Save;
        actions.AddChild(_preview); actions.AddChild(_apply); actions.AddChild(_undo);
        actions.AddChild(_redo); actions.AddChild(_save); root.AddChild(actions);
        _status = StudioStyle.Label("Choisissez une rencontre.", 10, StudioStyle.Muted, wrap: true); root.AddChild(_status);

        foreach (var line in EditableLines()) line.TextChanged += _ => DraftChanged();
        foreach (var option in new[]
                 { _distribution, _anchorVisibility, _authority, _enteredScript, _exitedScript,
                     _exhaustedScript, _objective, _voice, _nextWave })
            option.ItemSelected += _ => DraftChanged();
        foreach (var check in new[]
                 { _anchorNavigable, _anchorFallback, _playersOnly, _ignoreSummons, _ignoreCompanions,
                     _ignoreGameMasters, _ignoreSpectators })
            check.Toggled += _ => DraftChanged();
        Resized += FitDialog; ResetPreview("Choisissez une rencontre.");
    }

    internal void Open()
    {
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession; _session.CancelEncounterScenarioPreviewUx2LwH();
        Visible = true; Reload(clearSelection: true); FitDialog();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (_evidenceSession is not null) ReleaseEvidence();
        else _session.CancelEncounterScenarioPreviewUx2LwH();
        _selectedChoice = null; _draft = null; _lastPreview = null; _canonicalPreviewReady = false; Visible = false;
    }

    internal void RestoreSourceSurfaceAfterSmoke()
    {
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession; Visible = true; Reload(clearSelection: true); FitDialog();
    }

    internal bool PrepareEvidence(string mode)
    {
        if (_sourceSession.Project is null || _sourceSession.History is null) return false;
        if (_evidenceSession is not null) ReleaseEvidence();
        var project = _sourceSession.CreateUx2LwHEvidenceProject();
        if (project is null) return false;
        _lastEvidenceSourceUntouched = false; _lastEvidenceResourcesReleased = false; _lastSourceRestored = false;
        var snapshot = CaptureSourceSnapshot();
        _evidenceSnapshot = snapshot;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux2-lw-h-evidence-" + Guid.NewGuid().ToString("N"));
        _evidenceTempRoot = root;
        try
        {
            if (!ProjectSession.Create(root, project).Succeeded || !CopyScripts(_sourceSession.Session?.RootDirectory, root))
            { ReleaseEvidence(); return false; }
            var evidence = new StudioSessionController();
            if (!evidence.Open(root).Succeeded) { evidence.Close(); ReleaseEvidence(); return false; }
            _evidenceSession = evidence;
            _session = evidence;
            var encounterAreaId = project.Module.WorldFeatures.Encounters.FirstOrDefault(item =>
                item.Foundation is not null && item.Composition is not null && item.Scenario is not null)?.AreaId;
            if (encounterAreaId is null || !evidence.SelectArea(encounterAreaId))
            { ReleaseEvidence(); return false; }
            Visible = true;
            FitDialog();
            Reload(clearSelection: true);
            var index = _encounterEntries.FindIndex(item => item is { IsFoundationComplete: true, HasScenario: true });
            if (index <= 0) { ReleaseEvidence(); return false; }
            _encounter.Select(index); SelectEncounter(index);
            if (_draft is null) { ReleaseEvidence(); return false; }
            var realScript = _scriptChoices.FirstOrDefault()?.Id;
            _draft = _draft with
            {
                SpawnAnchors = _draft.SpawnAnchors.Select((anchor, anchorIndex) => anchorIndex == 0
                    ? anchor with { Capacity = anchor.Capacity + 1, Weight = anchor.Weight + .5 }
                    : anchor).ToArray(),
                Activator = _draft.Activator with { IgnoreSpectators = !_draft.Activator.IgnoreSpectators },
                Lifecycle = _draft.Lifecycle with
                {
                    Entered = _draft.Lifecycle.Entered with { ScriptId = realScript },
                    Exited = _draft.Lifecycle.Exited with { ScriptId = realScript },
                    Exhausted = _draft.Lifecycle.Exhausted with { ScriptId = realScript },
                },
                Phases = _draft.Phases.Select((phase, phaseIndex) => phaseIndex == 0
                    ? phase with { DisplayName = phase.DisplayName + " · preuve" }
                    : phase).ToArray(),
            };
            PopulateDraft(); Preview();
            _lastEvidenceSourceUntouched = SourceMatchesSnapshot(snapshot);
            SetStatus($"Preuve isolée prête · {HumanMode(mode)} · source intacte", false);
            return _lastPreview?.Succeeded == true && _lastPreview.CanApply && _lastEvidenceSourceUntouched;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_H_EVIDENCE_FAILED {exception.Message}");
            SetStatus("Préparation de preuve refusée · ressources libérées · consultez le journal.", true);
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
            var applied = _session.ApplyApprovedEncounterScenarioUx2LwH();
            if (!applied.Succeeded) return false;
            var after = _session.Project!.Module.WorldFeatures.Encounters.Single(item => item.Id == targetId);
            var afterJson = ProjectJsonSerializer.Serialize(_session.Project!);
            var changed = !ReferenceEquals(before, after) && after.Scenario is not null;
            var undo = _session.Undo(); var redo = _session.Redo(); var saved = _session.Save();
            reopened = new StudioSessionController();
            var opened = saved.Succeeded && reopened.Open(_evidenceTempRoot).Succeeded;
            return changed && undo.Succeeded && redo.Succeeded && opened && afterJson ==
                   ProjectJsonSerializer.Serialize(reopened.Project!);
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_H_UI_TRANSACTION_FAILED {exception.Message}"); return false;
        }
        finally { reopened?.Close(); Close(); }
    }

    private void Reload(bool clearSelection)
    {
        _updating = true;
        _encounterChoices = _session.EncounterChoicesUx2LwH;
        _scriptChoices = _session.EncounterScriptChoicesUx2LwH();
        _questChoices = _session.EncounterQuestChoicesUx2LwH();
        _audioChoices = _session.EncounterAudioChoicesUx2LwH();
        _encounter.Clear(); _encounterEntries.Clear();
        _encounter.AddItem("Choisissez explicitement une rencontre…"); _encounterEntries.Add(null);
        foreach (var choice in _encounterChoices) { _encounter.AddItem(choice.Label); _encounterEntries.Add(choice); }
        if (clearSelection)
        {
            _encounter.Select(0); _selectedChoice = null; _draft = null; _creationCard.Visible = false;
            _selection.Text = "Choisissez explicitement une rencontre complète.";
            ResetPreview("Choisissez une rencontre.");
        }
        _updating = false; UpdateActions();
    }

    private void SelectEncounter(int index)
    {
        if (_updating) return;
        _session.CancelEncounterScenarioPreviewUx2LwH(); _canonicalPreviewReady = false; _lastPreview = null;
        _selectedChoice = index >= 0 && index < _encounterEntries.Count ? _encounterEntries[index] : null;
        _draft = null; _creationCard.Visible = false;
        if (_selectedChoice is null)
        { _selection.Text = "Aucune rencontre sélectionnée."; ResetPreview("Choisissez une rencontre."); return; }
        if (!_selectedChoice.IsFoundationComplete)
        {
            _selection.Text = "La fiche LW-F est incomplète ; aucune valeur Scenario n’a été synthétisée.";
            ResetPreview("Terminez d’abord Foundation et composition."); return;
        }
        LoadReferences(_selectedChoice.ProfileId);
        if (!_selectedChoice.HasScenario)
        {
            _creationCard.Visible = true; FillCreationChoices();
            _selection.Text = "Scénario absent · choisissez explicitement une ancre et un groupe pour le créer.";
            ResetPreview("Création explicite requise."); return;
        }
        _draft = _session.CreateEncounterScenarioDraftUx2LwH(_selectedChoice.ProfileId);
        if (_draft is null) { ResetPreview("Sélection périmée."); return; }
        _selection.Text = $"Rencontre choisie · {_selectedChoice.Label}";
        PopulateDraft(); ResetPreview("Modifiez le scénario puis prévisualisez.");
    }

    private void LoadReferences(Guid profileId)
    {
        _anchorChoices = _session.EncounterAnchorChoicesUx2LwH(profileId);
        _groupChoices = _session.EncounterGroupChoicesUx2LwH(profileId);
    }

    private void FillCreationChoices()
    {
        FillOption(_creationAnchor, _creationAnchorIds, _anchorChoices, "Choisissez une ancre réelle…");
        FillOption(_creationGroup, _creationGroupIds, _groupChoices, "Choisissez un groupe LW-F…");
    }

    private void CreateScenario()
    {
        var anchorId = Selected(_creationAnchor, _creationAnchorIds);
        var groupId = Selected(_creationGroup, _creationGroupIds);
        _draft = _session.CreateNewEncounterScenarioDraftUx2LwH(_selectedChoice?.ProfileId, anchorId, groupId);
        if (_draft is null)
        { SetStatus("Choisissez explicitement une ancre réelle et un groupe LW-F.", true); return; }
        _creationCard.Visible = false; PopulateDraft(); ResetPreview("Scénario préparé en mémoire · prévisualisez avant application.");
    }

    private void PopulateDraft()
    {
        if (_draft is null) return;
        _updating = true;
        SelectEnum(_distribution, _draft.Distribution);
        FillOption(_anchorReference, _anchorReferenceIds, _anchorChoices, "Choisissez une ancre réelle…");
        FillScript(_enteredScript, _enteredScriptIds, _draft.Lifecycle.Entered.ScriptId);
        FillScript(_exitedScript, _exitedScriptIds, _draft.Lifecycle.Exited.ScriptId);
        FillScript(_exhaustedScript, _exhaustedScriptIds, _draft.Lifecycle.Exhausted.ScriptId);
        _playersOnly.ButtonPressed = _draft.Activator.PlayersOnly;
        _ignoreSummons.ButtonPressed = _draft.Activator.IgnoreSummons;
        _ignoreCompanions.ButtonPressed = _draft.Activator.IgnoreCompanions;
        _ignoreGameMasters.ButtonPressed = _draft.Activator.IgnoreGameMasters;
        _ignoreSpectators.ButtonPressed = _draft.Activator.IgnoreSpectators;
        SelectEnum(_authority, _draft.Activator.RequiredAuthority);
        _requiredTags.Text = string.Join(", ", _draft.Activator.RequiredTags);
        _apiVersion.Text = _draft.Lifecycle.ForgeScriptApiVersion;
        FillQuest(null); FillVoice(null); FillGroups([]); FillPhases();
        _updating = false; UpdateActions();
    }

    private bool CaptureTopLevelDraft()
    {
        if (_draft is null) return false;
        _draft = _draft with
        {
            Distribution = SelectedEnum<EncounterSpawnDistribution>(_distribution),
            Activator = _draft.Activator with
            {
                PlayersOnly = _playersOnly.ButtonPressed, IgnoreSummons = _ignoreSummons.ButtonPressed,
                IgnoreCompanions = _ignoreCompanions.ButtonPressed,
                IgnoreGameMasters = _ignoreGameMasters.ButtonPressed,
                IgnoreSpectators = _ignoreSpectators.ButtonPressed,
                RequiredAuthority = SelectedEnum<EncounterNetworkAuthority>(_authority),
                RequiredTags = Tokens(_requiredTags.Text),
            },
            Lifecycle = _draft.Lifecycle with
            {
                ForgeScriptApiVersion = _apiVersion.Text,
                Entered = _draft.Lifecycle.Entered with { ScriptId = Selected(_enteredScript, _enteredScriptIds) },
                Exited = _draft.Lifecycle.Exited with { ScriptId = Selected(_exitedScript, _exitedScriptIds) },
                Exhausted = _draft.Lifecycle.Exhausted with { ScriptId = Selected(_exhaustedScript, _exhaustedScriptIds) },
            },
        };
        return true;
    }

    private void FillAnchors()
    {
        _anchors.Clear(); _anchorIndex = -1;
        if (_draft is null) return;
        foreach (var anchor in _draft.SpawnAnchors)
        {
            var label = _anchorChoices.FirstOrDefault(item => item.Id == anchor.AnchorId)?.Label ?? "Ancre enregistrée absente";
            _anchors.AddItem($"{label} · capacité {anchor.Capacity} · " +
                             $"{(anchor.IsFallback ? "fallback" : "secondaire")}");
        }
    }

    private void SelectAnchor(int index)
    {
        if (_updating || _draft is null || index < 0 || index >= _draft.SpawnAnchors.Count) return;
        _anchorIndex = index; var anchor = _draft.SpawnAnchors[index]; _updating = true;
        Select(_anchorReference, _anchorReferenceIds, anchor.AnchorId);
        Set(_positionX, anchor.Transform.Position.X); Set(_positionY, anchor.Transform.Position.Y); Set(_positionZ, anchor.Transform.Position.Z);
        Set(_rotationX, anchor.Transform.Rotation.X); Set(_rotationY, anchor.Transform.Rotation.Y);
        Set(_rotationZ, anchor.Transform.Rotation.Z); Set(_rotationW, anchor.Transform.Rotation.W);
        Set(_scaleX, anchor.Transform.Scale.X); Set(_scaleY, anchor.Transform.Scale.Y); Set(_scaleZ, anchor.Transform.Scale.Z);
        _anchorCapacity.Text = anchor.Capacity.ToString(CultureInfo.InvariantCulture);
        _anchorNavigable.ButtonPressed = anchor.Navigable; SelectEnum(_anchorVisibility, anchor.Visibility);
        _anchorFallback.ButtonPressed = anchor.IsFallback; Set(_anchorWeight, anchor.Weight);
        _updating = false; UpdateActions();
    }

    private EncounterScenarioProfile.SpawnAnchorPolicy? ReadAnchor(Guid id)
    {
        var anchorId = Selected(_anchorReference, _anchorReferenceIds);
        if (anchorId is null || !TryInt(_anchorCapacity, out var capacity) || !TryDouble(_anchorWeight, out var weight) ||
            !TryFloat(_positionX, out var px) || !TryFloat(_positionY, out var py) || !TryFloat(_positionZ, out var pz) ||
            !TryFloat(_rotationX, out var rx) || !TryFloat(_rotationY, out var ry) ||
            !TryFloat(_rotationZ, out var rz) || !TryFloat(_rotationW, out var rw) ||
            !TryFloat(_scaleX, out var sx) || !TryFloat(_scaleY, out var sy) || !TryFloat(_scaleZ, out var sz))
        { SetStatus("Renseignez une ancre réelle et des valeurs numériques explicites.", true); return null; }
        try
        {
            return new(id, anchorId, new(new(px, py, pz), new(rx, ry, rz, rw), new(sx, sy, sz)),
                capacity, _anchorNavigable.ButtonPressed,
                SelectedEnum<EncounterWorldProfile.VisibilityMode>(_anchorVisibility),
                _anchorFallback.ButtonPressed, weight);
        }
        catch (ArgumentException exception) { SetStatus($"Transform refusé · {exception.Message}", true); return null; }
    }

    private void AddAnchor()
    {
        if (_draft is null) return;
        var anchor = ReadAnchor(Guid.NewGuid());
        if (anchor is null || _draft.SpawnAnchors.Any(item => item.AnchorId == anchor.AnchorId))
        { if (anchor is not null) SetStatus("Cette ancre figure déjà dans le scénario.", true); return; }
        _draft = _draft with { SpawnAnchors = [.. _draft.SpawnAnchors, anchor] }; FillAnchors(); DraftChanged();
    }

    private void UpdateAnchor()
    {
        if (_draft is null || _anchorIndex < 0 || _anchorIndex >= _draft.SpawnAnchors.Count) return;
        var updatedIndex = _anchorIndex;
        var anchor = ReadAnchor(_draft.SpawnAnchors[updatedIndex].Id); if (anchor is null) return;
        _draft = _draft with { SpawnAnchors = _draft.SpawnAnchors.Select((item, index) => index == updatedIndex ? anchor : item).ToArray() };
        FillAnchors(); _anchorIndex = updatedIndex; _anchors.Select(updatedIndex); DraftChanged();
    }

    private void RemoveAnchor()
    {
        if (_draft is null || _anchorIndex < 0 || _anchorIndex >= _draft.SpawnAnchors.Count) return;
        _draft = _draft with { SpawnAnchors = _draft.SpawnAnchors.Where((_, index) => index != _anchorIndex).ToArray() };
        FillAnchors(); DraftChanged();
    }

    private void FillPhases()
    {
        _phases.Clear(); _phaseIndex = -1; _waves.Clear(); _waveIndex = -1;
        if (_draft is null) return;
        foreach (var phase in _draft.Phases.OrderBy(item => item.Order))
            _phases.AddItem($"{phase.Order} · {phase.DisplayName} · {phase.Waves.Count} vague(s)");
        FillAnchors(); FillNextWaves(null); UpdateActions();
    }

    private void SelectPhase(int visualIndex)
    {
        if (_updating || _draft is null) return;
        var ordered = _draft.Phases.Select((phase, index) => (phase, index)).OrderBy(item => item.phase.Order).ToArray();
        if (visualIndex < 0 || visualIndex >= ordered.Length) return;
        _phaseIndex = ordered[visualIndex].index; var phase = ordered[visualIndex].phase; _updating = true;
        _phaseOrder.Text = phase.Order.ToString(CultureInfo.InvariantCulture); _phaseName.Text = phase.DisplayName;
        _phaseConditions.Text = string.Join(", ", phase.EntryConditions);
        _reinforcements.Text = phase.ReinforcementEncounterIds.Count == 0
            ? "Aucun renfort · ENC-016 hors périmètre"
            : $"{phase.ReinforcementEncounterIds.Count} renfort(s) préservé(s) · ENC-016 hors périmètre";
        FillWaves(phase); _updating = false; UpdateActions();
    }

    private void AddPhase()
    {
        if (_draft is null || !TryInt(_phaseOrder, out var order) || string.IsNullOrWhiteSpace(_phaseName.Text))
        { SetStatus("La phase exige un ordre entier et un nom.", true); return; }
        var phase = new EncounterScenarioProfile.PhasePolicy(Guid.NewGuid(), order, _phaseName.Text.Trim(),
            Tokens(_phaseConditions.Text), [], []);
        _draft = _draft with { Phases = [.. _draft.Phases, phase] }; FillPhases(); DraftChanged();
    }

    private void UpdatePhase()
    {
        if (_draft is null || _phaseIndex < 0 || !TryInt(_phaseOrder, out var order) ||
            string.IsNullOrWhiteSpace(_phaseName.Text)) return;
        var source = _draft.Phases[_phaseIndex];
        var phase = source with { Order = order, DisplayName = _phaseName.Text.Trim(),
            EntryConditions = Tokens(_phaseConditions.Text) };
        _draft = _draft with { Phases = _draft.Phases.Select((item, index) => index == _phaseIndex ? phase : item).ToArray() };
        FillPhases(); DraftChanged();
    }

    private void RemovePhase()
    {
        if (_draft is null || _phaseIndex < 0) return;
        _draft = _draft with { Phases = _draft.Phases.Where((_, index) => index != _phaseIndex).ToArray() };
        FillPhases(); DraftChanged();
    }

    private void FillWaves(EncounterScenarioProfile.PhasePolicy phase)
    {
        _waves.Clear(); _waveIndex = -1;
        foreach (var wave in phase.Waves.OrderBy(item => item.Order))
            _waves.AddItem($"{wave.Order} · {StudioSessionController.HumanCompletionUx2LwH(wave.Completion)} · " +
                           $"{wave.GroupIds.Count} groupe(s)");
        FillGroups([]); FillQuest(null); FillVoice(null); FillNextWaves(null);
    }

    private void SelectWave(int visualIndex)
    {
        if (_updating || _draft is null || _phaseIndex < 0) return;
        var phase = _draft.Phases[_phaseIndex];
        var ordered = phase.Waves.Select((wave, index) => (wave, index)).OrderBy(item => item.wave.Order).ToArray();
        if (visualIndex < 0 || visualIndex >= ordered.Length) return;
        _waveIndex = ordered[visualIndex].index; var wave = ordered[visualIndex].wave; _updating = true;
        _waveOrder.Text = wave.Order.ToString(CultureInfo.InvariantCulture); FillGroups(wave.GroupIds);
        Set(_waveDelay, wave.DelaySeconds); SelectEnum(_waveCompletion, wave.Completion);
        _completionTarget.Text = wave.CompletionTargetId ?? string.Empty; FillQuest(wave.QuestId);
        FillObjectives(wave.ObjectiveId); FillVoice(wave.VoiceCueId); FillNextWaves(wave.NextWaveId, wave.Id);
        UpdateWaveFieldState(); _updating = false; UpdateActions();
    }

    private EncounterScenarioProfile.WavePolicy? ReadWave(Guid id)
    {
        if (!TryInt(_waveOrder, out var order) || !TryDouble(_waveDelay, out var delay))
        { SetStatus("La vague exige un ordre entier et un délai numérique.", true); return null; }
        var groups = _waveGroups.GetSelectedItems().Where(index => index < _groupChoices.Count)
            .Select(index => _groupChoices[index].Id).ToArray();
        return new(id, order, groups, delay, SelectedEnum<EncounterWaveCompletionPolicy>(_waveCompletion),
            NullIfWhite(_completionTarget.Text), Selected(_quest, _questIds), Selected(_objective, _objectiveIds),
            Selected(_voice, _voiceIds), Selected(_nextWave, _nextWaveIds));
    }

    private void AddWave()
    {
        if (_draft is null || _phaseIndex < 0) { SetStatus("Choisissez d’abord une phase.", true); return; }
        var wave = ReadWave(Guid.NewGuid()); if (wave is null) return;
        var phase = _draft.Phases[_phaseIndex] with { Waves = [.. _draft.Phases[_phaseIndex].Waves, wave] };
        _draft = _draft with { Phases = _draft.Phases.Select((item, index) => index == _phaseIndex ? phase : item).ToArray() };
        FillPhases(); DraftChanged();
    }

    private void UpdateWave()
    {
        if (_draft is null || _phaseIndex < 0 || _waveIndex < 0) return;
        var source = _draft.Phases[_phaseIndex]; var wave = ReadWave(source.Waves[_waveIndex].Id); if (wave is null) return;
        var phase = source with { Waves = source.Waves.Select((item, index) => index == _waveIndex ? wave : item).ToArray() };
        _draft = _draft with { Phases = _draft.Phases.Select((item, index) => index == _phaseIndex ? phase : item).ToArray() };
        FillPhases(); DraftChanged();
    }

    private void RemoveWave()
    {
        if (_draft is null || _phaseIndex < 0 || _waveIndex < 0) return;
        var source = _draft.Phases[_phaseIndex];
        var phase = source with { Waves = source.Waves.Where((_, index) => index != _waveIndex).ToArray() };
        _draft = _draft with { Phases = _draft.Phases.Select((item, index) => index == _phaseIndex ? phase : item).ToArray() };
        FillPhases(); DraftChanged();
    }

    private void FillGroups(IReadOnlyList<Guid> selected)
    {
        _waveGroups.Clear();
        for (var index = 0; index < _groupChoices.Count; index++)
        {
            _waveGroups.AddItem($"{_groupChoices[index].Label} · {_groupChoices[index].Provenance}");
            if (selected.Contains(_groupChoices[index].Id)) _waveGroups.Select(index, false);
        }
    }

    private void FillQuest(Guid? selected)
    {
        _quest.Clear(); _questIds.Clear(); _quest.AddItem("Aucune quête"); _questIds.Add(null);
        foreach (var choice in _questChoices)
        { _quest.AddItem($"{choice.Label} · {choice.Provenance}"); _questIds.Add(choice.Id); }
        var index = selected is null ? 0 : _questIds.FindIndex(item => item == selected);
        if (selected is not null && index < 0)
        { _quest.AddItem("Quête enregistrée absente · correction requise"); _questIds.Add(selected); index = _questIds.Count - 1; }
        _quest.Select(Math.Max(0, index)); FillObjectives(null);
    }

    private void FillObjectives(string? selected)
    {
        _objective.Clear(); _objectiveIds.Clear(); _objective.AddItem("Aucun objectif"); _objectiveIds.Add(null);
        var questId = Selected(_quest, _questIds);
        var quest = questId is null ? null : _questChoices.FirstOrDefault(item => item.Id == questId.Value);
        foreach (var choice in quest?.Objectives ?? [])
        { _objective.AddItem($"{choice.Label} · {choice.Provenance}"); _objectiveIds.Add(choice.Id); }
        var index = selected is null ? 0 : _objectiveIds.FindIndex(item => item == selected);
        if (selected is not null && index < 0)
        {
            _objective.AddItem($"Clé enregistrée · {StudioSessionController.HumanPolicyReferenceUx2LwH(selected)} · résolution live non vérifiée");
            _objectiveIds.Add(selected); index = _objectiveIds.Count - 1;
        }
        _objective.Select(Math.Max(0, index));
    }

    private void FillVoice(string? selected)
    {
        FillOption(_voice, _voiceIds, _audioChoices, "Aucun repère audio", selected,
            "Adresse enregistrée absente · exécution non vérifiée");
    }

    private void FillNextWaves(Guid? selected, Guid? current = null)
    {
        _nextWave.Clear(); _nextWaveIds.Clear(); _nextWave.AddItem("Fin de la branche"); _nextWaveIds.Add(null);
        if (_draft is not null)
        {
            foreach (var phase in _draft.Phases.OrderBy(item => item.Order))
            foreach (var wave in phase.Waves.OrderBy(item => item.Order).Where(item => item.Id != current))
            { _nextWave.AddItem($"{phase.DisplayName} · vague {wave.Order}"); _nextWaveIds.Add(wave.Id); }
        }
        var index = selected is null ? 0 : _nextWaveIds.FindIndex(item => item == selected);
        if (selected is not null && index < 0)
        { _nextWave.AddItem("Transition enregistrée absente"); _nextWaveIds.Add(selected); index = _nextWaveIds.Count - 1; }
        _nextWave.Select(Math.Max(0, index));
    }

    private void FillScript(OptionButton option, List<string?> ids, string? selected) =>
        FillOption(option, ids, _scriptChoices, "Aucun script", selected,
            "Script enregistré absent · correction requise");

    private void UpdateWaveFieldState()
    {
        var completion = SelectedEnum<EncounterWaveCompletionPolicy>(_waveCompletion);
        _completionTarget.Editable = completion == EncounterWaveCompletionPolicy.Script;
        _quest.Disabled = completion != EncounterWaveCompletionPolicy.ObjectiveCompleted;
        _objective.Disabled = completion != EncounterWaveCompletionPolicy.ObjectiveCompleted;
    }

    private void Preview()
    {
        if (!CaptureTopLevelDraft() || _draft is null) return;
        _lastPreview = _session.PreviewEncounterScenarioUx2LwH(_draft);
        _canonicalPreviewReady = _lastPreview.Succeeded; RenderPreview(_lastPreview);
        SetStatus(_lastPreview.Message, !_lastPreview.Succeeded);
    }

    private void RenderPreview(StudioUx2LwHPreview preview)
    {
        Clear(_diff);
        if (preview.Diff.Count == 0) _diff.AddChild(StudioStyle.Label("Aucun changement.", 10, StudioStyle.Muted));
        foreach (var row in preview.Diff)
        {
            _diff.AddChild(StudioStyle.Label($"{row.Section} · avant : {row.Before}", 10, StudioStyle.Muted, wrap: true));
            _diff.AddChild(StudioStyle.Label($"{row.Section} · après : {row.After}", 10,
                row.Changed ? StudioStyle.Accent : StudioStyle.Muted, wrap: true));
        }
        Clear(_diagnostics);
        if (preview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Validation canonique conforme.", 10, StudioStyle.Success));
        foreach (var item in preview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label($"{DiagnosticSection(item.Path)} · {item.Message} · code {item.Code}",
                10, StudioStyle.Danger, wrap: true));
        _anchorPreview.Text = preview.Runtime.Anchors; _activatorPreview.Text = preview.Runtime.Activator;
        _lifecyclePreview.Text = preview.Runtime.Lifecycle; _wavePreview.Text = preview.Runtime.Waves;
        _boundary.Text = preview.Runtime.Boundary; UpdateActions();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedEncounterScenarioUx2LwH(); SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) { UpdateActions(); return; }
        var id = _draft?.ProfileId; EncounterChanged?.Invoke(); ReloadAndReselect(id);
    }

    private void Undo()
    {
        var id = _draft?.ProfileId; var result = _session.Undo(); SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { EncounterChanged?.Invoke(); ReloadAndReselect(id); }
    }

    private void Redo()
    {
        var id = _draft?.ProfileId; var result = _session.Redo(); SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { EncounterChanged?.Invoke(); ReloadAndReselect(id); }
    }

    private void Save() { var result = _session.Save(); SetStatus(result.Message, !result.Succeeded); UpdateActions(); }

    private void ReloadAndReselect(Guid? profileId)
    {
        Reload(clearSelection: true); if (profileId is null) return;
        var index = _encounterEntries.FindIndex(item => item?.ProfileId == profileId);
        if (index > 0) { _encounter.Select(index); SelectEncounter(index); }
    }

    private void DraftChanged()
    {
        if (_updating || _draft is null) return;
        _session.CancelEncounterScenarioPreviewUx2LwH(); _canonicalPreviewReady = false; _lastPreview = null;
        Clear(_diff); _diff.AddChild(StudioStyle.Label("Aperçu précédent invalidé.", 10, StudioStyle.Warning));
        Clear(_diagnostics); _diagnostics.AddChild(StudioStyle.Label("Validation en attente.", 10, StudioStyle.Muted));
        _anchorPreview.Text = "Ancres non évaluées"; _activatorPreview.Text = "Activateur non évalué";
        _lifecyclePreview.Text = "Lifecycle non évalué"; _wavePreview.Text = "Vagues non évaluées";
        _boundary.Text = "Aucun gizmo, nav bake, ForgeScript, quête, audio, spawn d’acteur ou transport réseau n’est exécuté.";
        SetStatus("Aperçu invalidé par la modification.", false); UpdateActions();
    }

    private void ResetPreview(string message)
    {
        _lastPreview = null; _canonicalPreviewReady = false;
        Clear(_diff); _diff.AddChild(StudioStyle.Label("Prévisualisez pour afficher le diff humain.", 10, StudioStyle.Muted));
        Clear(_diagnostics); _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic affiché.", 10, StudioStyle.Muted));
        _anchorPreview.Text = "Ancres non évaluées"; _activatorPreview.Text = "Activateur non évalué";
        _lifecyclePreview.Text = "Lifecycle non évalué"; _wavePreview.Text = "Vagues non évaluées";
        _boundary.Text = "Aucun gizmo, nav bake, ForgeScript, quête, audio, spawn d’acteur ou transport réseau n’est exécuté.";
        SetStatus(message, false); UpdateActions();
    }

    private void UpdateActions()
    {
        _preview.Disabled = _draft is null; _apply.Disabled = _lastPreview?.CanApply != true;
        _undo.Disabled = !_session.CanUndo; _redo.Disabled = !_session.CanRedo; _save.Disabled = _session.Project is null;
        _updateAnchor.Disabled = _anchorIndex < 0; _removeAnchor.Disabled = _anchorIndex < 0;
        _updatePhase.Disabled = _phaseIndex < 0; _removePhase.Disabled = _phaseIndex < 0;
        _addWave.Disabled = _phaseIndex < 0; _updateWave.Disabled = _waveIndex < 0; _removeWave.Disabled = _waveIndex < 0;
    }

    private StudioUx2LwHEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History ?? throw new InvalidOperationException("Historique source absent.");
        return new(_sourceSession.Project ?? throw new InvalidOperationException("Projet source absent."),
            history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureEncounterScenarioApprovalUx2LwH());
    }

    private bool SourceMatchesSnapshot(StudioUx2LwHEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history?.State.Revision == snapshot.Revision && (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureEncounterScenarioApprovalUx2LwH() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        var snapshot = _evidenceSnapshot; _evidenceSession?.CancelEncounterScenarioPreviewUx2LwH();
        _evidenceSession?.Close(); _evidenceSession = null; var root = _evidenceTempRoot; _evidenceTempRoot = null;
        _session = _sourceSession; _lastSourceRestored = true;
        _lastEvidenceSourceUntouched = snapshot is not null && SourceMatchesSnapshot(snapshot); _evidenceSnapshot = null;
        var released = true;
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root))
        {
            try { Directory.Delete(root, recursive: true); }
            catch (IOException) { released = false; }
            catch (UnauthorizedAccessException) { released = false; }
        }
        _lastEvidenceResourcesReleased = released && (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root));
    }

    private IEnumerable<LineEdit> EditableLines()
    {
        yield return _positionX; yield return _positionY; yield return _positionZ;
        yield return _rotationX; yield return _rotationY; yield return _rotationZ; yield return _rotationW;
        yield return _scaleX; yield return _scaleY; yield return _scaleZ; yield return _anchorCapacity;
        yield return _anchorWeight; yield return _requiredTags; yield return _apiVersion;
        yield return _phaseOrder; yield return _phaseName; yield return _phaseConditions;
        yield return _waveOrder; yield return _waveDelay; yield return _completionTarget;
    }

    private static OptionButton Option()
    {
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(option); return option;
    }

    private static OptionButton EnumOption<T>(IEnumerable<T> values, Func<T, string> label) where T : struct, Enum
    { var option = Option(); foreach (var value in values) option.AddItem(label(value)); return option; }

    private static LineEdit Line(string placeholder)
    {
        var line = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyLineEdit(line); return line;
    }

    private static LineEdit NumberLine(string placeholder)
    { var line = Line(placeholder); line.TooltipText = "Valeur brute, sans clamp ni arrondi implicite."; return line; }

    private static CheckBox Check(string text)
    {
        var check = new CheckBox { Text = text, FocusMode = FocusModeEnum.All };
        check.AddThemeColorOverride("font_color", StudioStyle.Text);
        check.AddThemeFontSizeOverride("font_size", StudioStyle.BaseFontSize); return check;
    }

    private static ItemList List(bool single, float height)
    {
        var list = new ItemList { SelectMode = single ? ItemList.SelectModeEnum.Single : ItemList.SelectModeEnum.Multi,
            CustomMinimumSize = new Vector2(0, height), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        return list;
    }

    private static HBoxContainer Buttons(params Button[] buttons)
    { var row = new HBoxContainer(); foreach (var button in buttons) row.AddChild(button); return row; }
    private static HBoxContainer Three(Control a, Control b, Control c) => Row(a, b, c);
    private static HBoxContainer Four(Control a, Control b, Control c, Control d) => Row(a, b, c, d);
    private static HBoxContainer Row(params Control[] controls)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var control in controls) { control.SizeFlagsHorizontal = SizeFlags.ExpandFill; row.AddChild(control); }
        return row;
    }

    private static GridContainer Grid()
    { var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 6); return grid; }
    private static void AddRow(GridContainer grid, string label, Control control)
    { var caption = StudioStyle.Label(label, 10, StudioStyle.Muted, wrap: true);
        caption.CustomMinimumSize = new Vector2(220, 0); caption.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        grid.AddChild(caption); grid.AddChild(control); }
    private static PanelContainer Card(string name, string title, string help, Control body)
    {
        var card = StudioStyle.Panel(name, StudioStyle.Surface, 8); var margin = StudioStyle.Margin(12, 10, 12, 10);
        card.AddChild(margin); var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 5);
        margin.AddChild(root); root.AddChild(StudioStyle.Label(title, 13));
        root.AddChild(StudioStyle.Label(help, 10, StudioStyle.Muted, wrap: true)); root.AddChild(body); return card;
    }

    private static void FillOption<T>(OptionButton option, List<T?> ids,
        IReadOnlyList<StudioUx2LwHReferenceChoice<T>> choices, string placeholder) where T : class
    { option.Clear(); ids.Clear(); option.AddItem(placeholder); ids.Add(null);
        foreach (var choice in choices) { option.AddItem($"{choice.Label} · {choice.Provenance}"); ids.Add(choice.Id); }
        option.Select(0); }
    private static void FillOption(OptionButton option, List<Guid?> ids,
        IReadOnlyList<StudioUx2LwHReferenceChoice<Guid>> choices, string placeholder)
    { option.Clear(); ids.Clear(); option.AddItem(placeholder); ids.Add(null);
        foreach (var choice in choices) { option.AddItem($"{choice.Label} · {choice.Provenance}"); ids.Add(choice.Id); }
        option.Select(0); }
    private static void FillOption(OptionButton option, List<string?> ids,
        IReadOnlyList<StudioUx2LwHReferenceChoice<string>> choices, string placeholder,
        string? selected, string missingLabel)
    {
        FillOption(option, ids, choices, placeholder);
        var index = selected is null ? 0 : ids.FindIndex(item => item == selected);
        if (selected is not null && index < 0) { option.AddItem(missingLabel); ids.Add(selected); index = ids.Count - 1; }
        option.Select(Math.Max(0, index));
    }
    private static string? Selected(OptionButton option, IReadOnlyList<string?> ids) =>
        option.Selected >= 0 && option.Selected < ids.Count ? ids[option.Selected] : null;
    private static Guid? Selected(OptionButton option, IReadOnlyList<Guid?> ids) =>
        option.Selected >= 0 && option.Selected < ids.Count ? ids[option.Selected] : null;
    private static void Select(OptionButton option, IReadOnlyList<string?> ids, string value)
    { var index = ids.ToList().FindIndex(item => item == value); option.Select(Math.Max(0, index)); }
    private static T SelectedEnum<T>(OptionButton option) where T : struct, Enum
    { var values = Enum.GetValues<T>(); return option.Selected >= 0 && option.Selected < values.Length ? values[option.Selected] : values[0]; }
    private static void SelectEnum<T>(OptionButton option, T value) where T : struct, Enum
    { option.Select(Math.Max(0, Array.IndexOf(Enum.GetValues<T>(), value))); }
    private static IReadOnlyList<string> Tokens(string value) => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.Ordinal).ToArray();
    private static string? NullIfWhite(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private bool ObjectiveReferenceIsAvailable(EncounterScenarioProfile.WavePolicy wave)
    {
        if (wave.Completion != EncounterWaveCompletionPolicy.ObjectiveCompleted &&
            wave.QuestId is null && string.IsNullOrWhiteSpace(wave.ObjectiveId))
            return true;
        if (wave.QuestId is null || string.IsNullOrWhiteSpace(wave.ObjectiveId)) return false;
        var quest = _questChoices.FirstOrDefault(item => item.Id == wave.QuestId.Value);
        if (quest is null) return false;
        return !Guid.TryParse(wave.ObjectiveId, out var objectiveId) || quest.Objectives.Any(item =>
            Guid.TryParse(item.Id, out var knownObjectiveId) && knownObjectiveId == objectiveId);
    }

    private static bool TryInt(LineEdit line, out int value) => int.TryParse(line.Text, NumberStyles.Integer,
        CultureInfo.InvariantCulture, out value);
    private static bool TryDouble(LineEdit line, out double value) => double.TryParse(line.Text, NumberStyles.Float,
        CultureInfo.InvariantCulture, out value);
    private static bool TryFloat(LineEdit line, out float value) => float.TryParse(line.Text, NumberStyles.Float,
        CultureInfo.InvariantCulture, out value);
    private static void Set(LineEdit line, double value) => line.Text = value.ToString("R", CultureInfo.InvariantCulture);

    private static string DiagnosticSection(string path) =>
        path.Contains("spawnAnchors", StringComparison.Ordinal) ? "Points de spawn" :
        path.Contains("activator", StringComparison.Ordinal) ? "Activateur" :
        path.Contains("lifecycle", StringComparison.Ordinal) ? "Lifecycle" :
        path.Contains("phases", StringComparison.Ordinal) ? "Phases et vagues" : "Scénario";
    private static string HumanMode(string mode) => mode.Replace('-', ' ').Replace('_', ' ');
    private static bool CopyScripts(string? sourceRoot, string destinationRoot)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot)) return true; var scripts = Path.Combine(sourceRoot, "scripts");
        if (!Directory.Exists(scripts)) return true;
        try { foreach (var source in Directory.EnumerateFiles(scripts, "*.forge", SearchOption.AllDirectories))
            { var destination = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, source));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(source, destination, true); }
            return true; }
        catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; }
    }
    private void FitDialog()
    { if (!Visible) return; var viewport = GetViewportRect().Size; var width = Mathf.Clamp(viewport.X - 48, 960, 1540);
        var height = Mathf.Clamp(viewport.Y - 36, 760, 1120); _dialog.Position = new((viewport.X - width) / 2, (viewport.Y - height) / 2);
        _dialog.Size = new(width, height); }
    private void SetStatus(string message, bool error)
    { _status.Text = message; _status.AddThemeColorOverride("font_color", error ? StudioStyle.Danger : StudioStyle.Muted);
        StatusChanged?.Invoke(message, error); }
    private static void Clear(Node node) { foreach (var child in node.GetChildren()) child.Free(); }
    private static IEnumerable<Label> DescendantLabels(Node node)
    { foreach (var child in node.GetChildren()) { if (child is Label label) yield return label;
            foreach (var nested in DescendantLabels(child)) yield return nested; } }
    private static bool ContainsTechnicalIdentifier(string text) =>
        text.Split([' ', '·', '/', ':', ',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(token => Guid.TryParse(token, out _));
}
