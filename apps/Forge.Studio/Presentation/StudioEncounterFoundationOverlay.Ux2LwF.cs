using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioEncounterFoundationOverlayUx2LwF : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceTempRoot;
    private StudioUx2LwFEvidenceSourceSnapshot? _evidenceSnapshot;
    private readonly PanelContainer _dialog;
    private readonly OptionButton _encounter;
    private readonly Label _selection;
    private readonly PanelContainer _newEncounterCard;
    private readonly OptionButton _newArea;
    private readonly OptionButton _newCreature;
    private readonly OptionButton _newAnchor;
    private readonly LineEdit _slug;
    private readonly LineEdit _displayName;
    private readonly LineEdit _category;
    private readonly LineEdit _theme;
    private readonly OptionButton _primaryRole;
    private readonly OptionButton _shape;
    private readonly LineEdit _height;
    private readonly LineEdit _radius;
    private readonly LineEdit _threatRadius;
    private readonly LineEdit _requiredTags;
    private readonly LineEdit _allowedFactions;
    private readonly CheckBox _lineOfSight;
    private readonly OptionButton _activationScript;
    private readonly ItemList _candidates;
    private readonly OptionButton _candidateBlueprint;
    private readonly OptionButton _candidateRole;
    private readonly LineEdit _candidateWeight;
    private readonly LineEdit _candidateCost;
    private readonly LineEdit _candidateMaximum;
    private readonly LineEdit _candidateConditions;
    private readonly LineEdit _candidateVariants;
    private readonly Button _addCandidate;
    private readonly Button _removeCandidate;
    private readonly LineEdit _spawnBudget;
    private readonly LineEdit _networkCap;
    private readonly Label _anchors;
    private readonly LineEdit _ruleset;
    private readonly LineEdit _partyProvider;
    private readonly OptionButton _difficultyBand;
    private readonly LineEdit _partySize;
    private readonly LineEdit _partyPower;
    private readonly LineEdit _baseBudget;
    private readonly LineEdit _partyMultiplier;
    private readonly LineEdit _difficultyMultiplier;
    private readonly ItemList _groups;
    private readonly LineEdit _groupSlug;
    private readonly LineEdit _groupMinimum;
    private readonly LineEdit _groupMaximum;
    private readonly LineEdit _groupBudget;
    private readonly Button _addGroup;
    private readonly Button _removeGroup;
    private readonly VBoxContainer _diff;
    private readonly VBoxContainer _diagnostics;
    private readonly Label _volumeSummary;
    private readonly Label _poolSummary;
    private readonly Label _difficultySummary;
    private readonly Label _countSummary;
    private readonly Label _boundary;
    private readonly Label _status;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;
    private IReadOnlyList<StudioUx2LwFEncounterChoice> _encounterChoices = [];
    private IReadOnlyList<StudioUx2LwFReferenceChoice<AreaId>> _areaChoices = [];
    private IReadOnlyList<StudioUx2LwFReferenceChoice<BlueprintId>> _creatureChoices = [];
    private IReadOnlyList<StudioUx2LwFReferenceChoice<string>> _scriptChoices = [];
    private IReadOnlyList<StudioUx2LwFReferenceChoice<string>> _anchorChoices = [];
    private readonly List<StudioUx2LwFEncounterChoice?> _encounterEntries = [];
    private readonly List<AreaId?> _areaIds = [];
    private readonly List<BlueprintId?> _creatureIds = [];
    private readonly List<string?> _anchorIds = [];
    private readonly List<string?> _scriptIds = [];
    private StudioUx2LwFDraft? _draft;
    private StudioUx2LwFPreview? _lastPreview;
    private int _candidateIndex = -1;
    private int _groupIndex = -1;
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
    internal bool ShowsFiveRoutes => _lastPreview is not null &&
                                     !_volumeSummary.Text.Contains("non évalué", StringComparison.OrdinalIgnoreCase) &&
                                     !_poolSummary.Text.Contains("non évalué", StringComparison.OrdinalIgnoreCase) &&
                                     !_difficultySummary.Text.Contains("non évaluée", StringComparison.OrdinalIgnoreCase) &&
                                     !_countSummary.Text.Contains("non évalué", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHonestRuntimeBoundary => _boundary.Text.Contains("aucun volume moteur", StringComparison.OrdinalIgnoreCase) &&
                                                _boundary.Text.Contains("ForgeScript", StringComparison.OrdinalIgnoreCase) &&
                                                _boundary.Text.Contains("transport réseau", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsStructuredDiagnostics => _lastPreview is not null &&
                                                 _lastPreview.Diagnostics.All(item =>
                                                     !string.IsNullOrWhiteSpace(item.Code) &&
                                                     !string.IsNullOrWhiteSpace(item.Path));
    internal bool UsesRealReferences => _areaChoices.Count > 0 && _creatureChoices.Count > 0 &&
                                        _areaChoices.All(item => item.Provenance.Contains("module", StringComparison.OrdinalIgnoreCase)) &&
                                        _creatureChoices.All(item => item.Provenance.Contains("module", StringComparison.OrdinalIgnoreCase)) &&
                                        (_draft?.ActivationScriptId is null || _scriptChoices.Any(item =>
                                            string.Equals(item.Id, _draft.ActivationScriptId, StringComparison.Ordinal)));
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
            return text.Contains("Assistant de rencontre", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Volume d’activation", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Créatures candidates", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Difficulté", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Minimum et maximum", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu sec", StringComparison.OrdinalIgnoreCase) &&
                   !text.Contains("UUID", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsTechnicalIdentifier(text);
        }
    }

    internal StudioEncounterFoundationOverlayUx2LwF(StudioSessionController session)
    {
        _sourceSession = session ?? throw new ArgumentNullException(nameof(session));
        _session = _sourceSession;
        Name = "StudioEncounterFoundationOverlayUx2LwF";
        Visible = false;
        ZIndex = 475;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new Color(0, 0, 0, .84f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        _dialog = StudioStyle.Panel("EncounterFoundationDialog", StudioStyle.Chrome, 12, 2);
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
        heading.AddChild(StudioStyle.Label("Assistant de rencontre", 20));
        heading.AddChild(StudioStyle.Label(
            "ENC-001/002/003/004/005 · fiche canonique, aperçu sec déterministe et application transactionnelle · " +
            "aucun gizmo, acteur, script ou réseau n’est exécuté ici", 10, StudioStyle.Muted, wrap: true));
        header.AddChild(heading);
        var close = StudioStyle.Button("Fermer", compact: true);
        close.Pressed += Close;
        header.AddChild(close);

        _encounter = Option();
        _encounter.ItemSelected += index => SelectEncounter((int)index);
        root.AddChild(_encounter);
        _selection = StudioStyle.Label(
            "Choisissez explicitement une rencontre, ou choisissez explicitement une zone, une créature et une ancre pour en créer une.",
            11, StudioStyle.Warning, wrap: true);
        root.AddChild(_selection);

        var bodyHost = new Control { CustomMinimumSize = new Vector2(0, 590),
            CustomMaximumSize = new Vector2(-1, 850), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true, PropagateMaximumSize = true };
        root.AddChild(bodyHost);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        bodyHost.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 9);
        scroll.AddChild(content);

        _newArea = Option();
        _newCreature = Option();
        _newAnchor = Option();
        _newArea.ItemSelected += _ => NewAreaChanged();
        _newCreature.ItemSelected += _ => TryCreateNewDraft();
        _newAnchor.ItemSelected += _ => TryCreateNewDraft();
        var newGrid = Grid();
        AddRow(newGrid, "Zone réelle", _newArea);
        AddRow(newGrid, "Créature initiale", _newCreature);
        AddRow(newGrid, "Ancre réelle", _newAnchor);
        _newEncounterCard = Card("EncounterNew", "Nouvelle rencontre",
            "Aucune valeur n’est choisie implicitement. Cette action crée uniquement une fiche canonique après votre sélection.",
            newGrid);
        _newEncounterCard.Visible = false;
        content.AddChild(_newEncounterCard);

        _slug = Line("clairiere-embuscade");
        _displayName = Line("Embuscade de la clairière");
        _category = Line("embuscade");
        _theme = Line("forêt");
        _primaryRole = EnumOption(Enum.GetValues<EncounterWorldProfile.CandidateRole>(), HumanRole);
        var identityGrid = Grid();
        AddRow(identityGrid, "Identifiant lisible", _slug);
        AddRow(identityGrid, "Nom", _displayName);
        AddRow(identityGrid, "Catégorie", _category);
        AddRow(identityGrid, "Thème", _theme);
        AddRow(identityGrid, "Rôle principal", _primaryRole);
        content.AddChild(Card("EncounterWizard", "Assistant de rencontre",
            "Décrit l’intention humaine de la rencontre sans synthétiser de créature, de zone ou de règleset absent.",
            identityGrid));

        _shape = EnumOption(Enum.GetValues<EncounterWorldProfile.VolumeShape>(), HumanShape);
        _height = NumberLine("4");
        _radius = NumberLine("8");
        _threatRadius = NumberLine("18");
        _requiredTags = Line("player");
        _allowedFactions = Line("laisser vide pour toutes");
        _lineOfSight = Check("Exiger une ligne de vue");
        _activationScript = Option();
        var volumeGrid = Grid();
        AddRow(volumeGrid, "Forme de données", _shape);
        AddRow(volumeGrid, "Hauteur", _height);
        AddRow(volumeGrid, "Rayon / portée", _radius);
        AddRow(volumeGrid, "Rayon de menace", _threatRadius);
        AddRow(volumeGrid, "Tags requis", _requiredTags);
        AddRow(volumeGrid, "Factions autorisées", _allowedFactions);
        AddRow(volumeGrid, "Détection", _lineOfSight);
        AddRow(volumeGrid, "Script réel optionnel", _activationScript);
        content.AddChild(Card("EncounterVolume", "Volume d’activation",
            "Édition numérique du contrat 2D/3D. Aucun gizmo, test de visibilité ou volume moteur n’est créé dans cet écran.",
            volumeGrid));

        _candidates = new ItemList { SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0, 92), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _candidates.ItemSelected += index => SelectCandidate((int)index);
        _candidateBlueprint = Option();
        _candidateRole = EnumOption(Enum.GetValues<EncounterWorldProfile.CandidateRole>(), HumanRole);
        _candidateWeight = NumberLine("1");
        _candidateCost = NumberLine("4");
        _candidateMaximum = NumberLine("3");
        _candidateConditions = Line("conditions séparées par des virgules");
        _candidateVariants = Line("variantes séparées par des virgules");
        _addCandidate = StudioStyle.Button("Ajouter la créature choisie", compact: true);
        _removeCandidate = StudioStyle.Button("Retirer le candidat", compact: true);
        _addCandidate.Pressed += AddCandidate;
        _removeCandidate.Pressed += RemoveCandidate;
        var candidateButtons = new HBoxContainer();
        candidateButtons.AddChild(_addCandidate); candidateButtons.AddChild(_removeCandidate);
        var candidateGrid = Grid();
        AddRow(candidateGrid, "Liste", _candidates);
        AddRow(candidateGrid, "Blueprint réel", _candidateBlueprint);
        AddRow(candidateGrid, "Rôle", _candidateRole);
        AddRow(candidateGrid, "Poids", _candidateWeight);
        AddRow(candidateGrid, "Coût", _candidateCost);
        AddRow(candidateGrid, "Maximum vivant", _candidateMaximum);
        AddRow(candidateGrid, "Conditions", _candidateConditions);
        AddRow(candidateGrid, "Variantes", _candidateVariants);
        AddRow(candidateGrid, "Actions", candidateButtons);
        content.AddChild(Card("EncounterCandidates", "Créatures candidates",
            "Chaque entrée conserve son blueprint réel, son rôle, son poids, son coût, ses conditions, variantes et plafond.",
            candidateGrid));

        _spawnBudget = NumberLine("12");
        _networkCap = NumberLine("8");
        _anchors = StudioStyle.Label("Aucune ancre", 10, StudioStyle.Muted, wrap: true);
        var budgetGrid = Grid();
        AddRow(budgetGrid, "Budget d’apparition", _spawnBudget);
        AddRow(budgetGrid, "Plafond autoritaire", _networkCap);
        AddRow(budgetGrid, "Ancres conservées", _anchors);
        content.AddChild(Card("EncounterBudget", "Budget et politique d’apparition",
            "Le plafond est une donnée autoritaire ; cet écran ne démarre aucun serveur et n’alloue aucun acteur.",
            budgetGrid));

        _ruleset = Line("ruleset:forge");
        _partyProvider = Line("party:studio-authored");
        _difficultyBand = EnumOption(Enum.GetValues<EncounterDifficultyBand>(), HumanDifficulty);
        _partySize = NumberLine("4");
        _partyPower = NumberLine("10");
        _baseBudget = NumberLine("12");
        _partyMultiplier = NumberLine("1");
        _difficultyMultiplier = NumberLine("1");
        var difficultyGrid = Grid();
        AddRow(difficultyGrid, "Règles", _ruleset);
        AddRow(difficultyGrid, "Estimateur de groupe", _partyProvider);
        AddRow(difficultyGrid, "Palier", _difficultyBand);
        AddRow(difficultyGrid, "Taille attendue", _partySize);
        AddRow(difficultyGrid, "Puissance attendue", _partyPower);
        AddRow(difficultyGrid, "Budget de base", _baseBudget);
        AddRow(difficultyGrid, "Multiplicateur de groupe", _partyMultiplier);
        AddRow(difficultyGrid, "Multiplicateur de difficulté", _difficultyMultiplier);
        content.AddChild(Card("EncounterDifficulty", "Difficulté de rencontre",
            "L’aperçu utilise uniquement l’estimation écrite dans la fiche ; il ne lit pas un groupe de joueurs en direct.",
            difficultyGrid));

        _groups = new ItemList { SelectMode = ItemList.SelectModeEnum.Single,
            CustomMinimumSize = new Vector2(0, 84), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _groups.ItemSelected += index => SelectGroup((int)index);
        _groupSlug = Line("groupe-principal");
        _groupMinimum = NumberLine("1");
        _groupMaximum = NumberLine("3");
        _groupBudget = NumberLine("12");
        _addGroup = StudioStyle.Button("Ajouter un groupe", compact: true);
        _removeGroup = StudioStyle.Button("Retirer le groupe", compact: true);
        _addGroup.Pressed += AddGroup;
        _removeGroup.Pressed += RemoveGroup;
        var groupButtons = new HBoxContainer(); groupButtons.AddChild(_addGroup); groupButtons.AddChild(_removeGroup);
        var groupGrid = Grid();
        AddRow(groupGrid, "Groupes", _groups);
        AddRow(groupGrid, "Nom lisible", _groupSlug);
        AddRow(groupGrid, "Minimum", _groupMinimum);
        AddRow(groupGrid, "Maximum", _groupMaximum);
        AddRow(groupGrid, "Budget", _groupBudget);
        AddRow(groupGrid, "Actions", groupButtons);
        content.AddChild(Card("EncounterCounts", "Minimum et maximum",
            "Les candidats déjà liés au groupe sont conservés ; retirer un candidat rend un groupe vide explicitement invalide.",
            groupGrid));

        _diff = new VBoxContainer();
        _diagnostics = new VBoxContainer();
        _volumeSummary = StudioStyle.Label("Volume non évalué", 10, StudioStyle.Muted, wrap: true);
        _poolSummary = StudioStyle.Label("Candidats non évalués", 10, StudioStyle.Muted, wrap: true);
        _difficultySummary = StudioStyle.Label("Difficulté non évaluée", 10, StudioStyle.Muted, wrap: true);
        _countSummary = StudioStyle.Label("Comptages non évalués", 10, StudioStyle.Muted, wrap: true);
        _boundary = StudioStyle.Label(
            "Projection de politique uniquement : aucun volume moteur, acteur, ForgeScript ou transport réseau n’est exécuté.",
            10, StudioStyle.Warning, wrap: true);
        var previewBody = new VBoxContainer();
        previewBody.AddChild(_diff); previewBody.AddChild(_diagnostics);
        previewBody.AddChild(_volumeSummary); previewBody.AddChild(_poolSummary);
        previewBody.AddChild(_difficultySummary); previewBody.AddChild(_countSummary); previewBody.AddChild(_boundary);
        content.AddChild(Card("EncounterPreview", "Aperçu sec canonique",
            "Le diff humain est affiché avant toute application. Les diagnostics gardent leur code et leur chemin canonique.",
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
        actions.AddChild(_preview); actions.AddChild(_apply); actions.AddChild(_undo);
        actions.AddChild(_redo); actions.AddChild(_save);
        root.AddChild(actions);
        _status = StudioStyle.Label("Choisissez une rencontre.", 10, StudioStyle.Muted, wrap: true);
        root.AddChild(_status);

        foreach (var line in EditableLines()) line.TextChanged += _ => DraftChanged();
        foreach (var option in new[]
                 {
                     _primaryRole, _shape, _activationScript, _candidateBlueprint, _candidateRole, _difficultyBand,
                 })
            option.ItemSelected += _ => DraftChanged();
        _lineOfSight.Toggled += _ => DraftChanged();
        Resized += FitDialog;
        ResetPreview("Choisissez une rencontre.");
    }

    internal void Open()
    {
        if (_evidenceSession is not null) ReleaseEvidence();
        _session = _sourceSession;
        Visible = true;
        Reload(clearSelection: true);
        FitDialog();
    }

    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceTempRoot is null) return;
        if (_evidenceSession is not null) ReleaseEvidence();
        else _session.CancelEncounterPreviewUx2LwF();
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
        var project = _sourceSession.CreateUx2LwFEvidenceProject();
        if (project is null) return false;
        _lastEvidenceSourceUntouched = false;
        _lastEvidenceResourcesReleased = false;
        _lastSourceRestored = false;
        _evidenceSnapshot = CaptureSourceSnapshot();
        var root = Path.Combine(Path.GetTempPath(),
            "openrpg-forge-studio-ux2-lw-f-evidence-" + Guid.NewGuid().ToString("N"));
        _evidenceTempRoot = root;
        try
        {
            var created = ProjectSession.Create(root, project);
            if (!created.Succeeded || !CopyScripts(_sourceSession.Session?.RootDirectory, root))
            {
                if (!created.Succeeded)
                {
                    GD.PrintErr("OPENRPG_FORGE_STUDIO_UX2_LW_F_EVIDENCE_CREATE_FAILED " +
                                string.Join(" | ", created.Diagnostics.Select(item =>
                                    $"{item.Code}:{item.Path}:{item.Message}")));
                }
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
            var encounterAreaId = project.Module.WorldFeatures.Encounters
                .FirstOrDefault(item => item.Foundation is not null && item.Composition is not null)?.AreaId;
            if (encounterAreaId is null || !evidence.SelectArea(encounterAreaId))
            {
                ReleaseEvidence();
                return false;
            }
            Visible = true;
            FitDialog();
            Reload(clearSelection: true);
            var index = _encounterEntries.FindIndex(item => item is { IsNew: false, IsComplete: true });
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
            var realScript = _scriptChoices.FirstOrDefault()?.Id;
            _draft = _draft with
            {
                ThreatRadius = _draft.ThreatRadius + 1,
                Candidates = _draft.Candidates.Select((item, candidateIndex) => candidateIndex == 0
                    ? item with { Weight = item.Weight + 1 }
                    : item).ToArray(),
                Groups = _draft.Groups.Select((item, groupIndex) => groupIndex == 0
                    ? item with { Minimum = Math.Max(0, item.Minimum - 1) }
                    : item).ToArray(),
                ActivationScriptId = realScript,
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
            if (!PrepareEvidence("transaction UI") || _draft is null || _evidenceTempRoot is null)
                return false;
            var before = _session.Project;
            var preview = _lastPreview;
            var applied = _session.ApplyApprovedEncounterUx2LwF();
            if (!applied.Succeeded || before is null || preview?.Succeeded != true) return false;
            var changed = !ReferenceEquals(before, _session.Project);
            var undo = _session.Undo();
            var redo = _session.Redo();
            var saved = _session.Save();
            reopened = new StudioSessionController();
            var opened = saved.Succeeded && reopened.Open(_evidenceTempRoot).Succeeded;
            return changed && undo.Succeeded && redo.Succeeded && opened &&
                   ProjectJsonSerializer.Serialize(_session.Project!) ==
                   ProjectJsonSerializer.Serialize(reopened.Project!);
        }
        catch (Exception exception)
        {
            Godot.GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX2_LW_F_UI_TRANSACTION_FAILED {exception.Message}");
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
        _encounterChoices = _session.EncounterChoicesUx2LwF;
        _areaChoices = _session.EncounterAreaChoicesUx2LwF();
        _creatureChoices = _session.EncounterCreatureChoicesUx2LwF();
        _scriptChoices = _session.EncounterScriptChoicesUx2LwF();
        _encounter.Clear(); _encounterEntries.Clear();
        _encounter.AddItem("Choisissez explicitement une rencontre…");
        _encounterEntries.Add(null);
        foreach (var choice in _encounterChoices)
        {
            _encounter.AddItem(choice.Label);
            _encounterEntries.Add(choice);
        }
        FillReferenceOption(_newArea, _areaIds, _areaChoices, "Choisissez une zone…");
        FillReferenceOption(_newCreature, _creatureIds, _creatureChoices, "Choisissez une créature…");
        FillReferenceOption(_candidateBlueprint, _creatureIds, _creatureChoices, "Choisissez une créature…");
        FillScriptOption(null);
        _newAnchor.Clear(); _anchorIds.Clear();
        _newAnchor.AddItem("Choisissez d’abord une zone…"); _anchorIds.Add(null);
        if (clearSelection)
        {
            _encounter.Select(0);
            _draft = null;
            _newEncounterCard.Visible = false;
            _selection.Text = "Aucune rencontre ouverte automatiquement.";
        }
        _updating = false;
        ResetPreview(clearSelection ? "Choisissez une rencontre." : "Fiche rechargée.");
        UpdateActions();
    }

    private void SelectEncounter(int index)
    {
        if (_updating) return;
        _session.CancelEncounterPreviewUx2LwF();
        _draft = null;
        _candidateIndex = -1;
        _groupIndex = -1;
        var choice = index >= 0 && index < _encounterEntries.Count ? _encounterEntries[index] : null;
        if (choice is null)
        {
            _newEncounterCard.Visible = false;
            _selection.Text = "Choisissez explicitement une rencontre.";
            ResetPreview("Aucune fiche sélectionnée.");
            return;
        }
        if (choice.IsNew)
        {
            _newEncounterCard.Visible = true;
            _newArea.Select(0); _newCreature.Select(0); _newAnchor.Select(0);
            _selection.Text = "Nouvelle rencontre · choisissez les trois références réelles avant d’ouvrir la fiche.";
            ResetPreview("Sélectionnez une zone, une créature et une ancre.");
            return;
        }
        _newEncounterCard.Visible = false;
        _draft = _session.CreateEncounterDraftUx2LwF(choice.ProfileId);
        if (_draft is null)
        {
            _selection.Text = "Cette rencontre est incomplète. Utilisez l’assistant historique pour fournir Foundation et Composition.";
            ResetPreview("Prérequis canoniques manquants.");
            return;
        }
        _selection.Text = $"Rencontre ouverte · {choice.Label}";
        PopulateDraft();
        ResetPreview("Fiche chargée · prévisualisez avant d’appliquer.");
    }

    private void NewAreaChanged()
    {
        if (_updating) return;
        var areaId = Selected(_newArea, _areaIds);
        _anchorChoices = _session.EncounterAnchorChoicesUx2LwF(areaId);
        FillReferenceOption(_newAnchor, _anchorIds, _anchorChoices, "Choisissez une ancre…");
        TryCreateNewDraft();
    }

    private void TryCreateNewDraft()
    {
        if (_updating || _encounter.Selected <= 0 ||
            _encounterEntries[_encounter.Selected] is not { IsNew: true }) return;
        _draft = _session.CreateNewEncounterDraftUx2LwF(
            Selected(_newArea, _areaIds), Selected(_newCreature, _creatureIds),
            Selected(_newAnchor, _anchorIds));
        if (_draft is null)
        {
            ResetPreview("Les trois références explicites sont requises.");
            return;
        }
        _selection.Text = "Nouvelle fiche prête · aucune mutation tant que vous n’appliquez pas l’aperçu.";
        PopulateDraft();
        ResetPreview("Nouvelle fiche prête · prévisualisez avant d’appliquer.");
    }

    private void PopulateDraft()
    {
        var draft = _draft;
        if (draft is null) return;
        _updating = true;
        _slug.Text = draft.Slug; _displayName.Text = draft.DisplayName; _category.Text = draft.Category;
        _theme.Text = draft.Theme; SelectEnum(_primaryRole, draft.PrimaryRole); SelectEnum(_shape, draft.Shape);
        _height.Text = Format(draft.Height); _radius.Text = Format(draft.Radius);
        _threatRadius.Text = Format(draft.ThreatRadius);
        _requiredTags.Text = Csv(draft.RequiredEntityTags); _allowedFactions.Text = Csv(draft.AllowedFactions);
        _lineOfSight.ButtonPressed = draft.RequireLineOfSight;
        FillScriptOption(draft.ActivationScriptId);
        _spawnBudget.Text = Format(draft.SpawnBudget); _networkCap.Text = draft.NetworkActorCap.ToString();
        _anchors.Text = draft.SpawnAnchorIds.Count == 0 ? "Aucune ancre" :
            string.Join(" · ", draft.SpawnAnchorIds.Select(HumanAnchor));
        _ruleset.Text = draft.RulesetId; _partyProvider.Text = draft.PartyAssessmentProviderId;
        SelectEnum(_difficultyBand, draft.DifficultyBand);
        _partySize.Text = draft.ExpectedPartySize.ToString(); _partyPower.Text = Format(draft.ExpectedPartyPower);
        _baseBudget.Text = Format(draft.BaseBudget); _partyMultiplier.Text = Format(draft.PartySizeMultiplier);
        _difficultyMultiplier.Text = Format(draft.DifficultyMultiplier);
        ReloadCandidateList(); ReloadGroupList();
        if (draft.Candidates.Count > 0) { _candidates.Select(0); SelectCandidate(0); }
        if (draft.Groups.Count > 0) { _groups.Select(0); SelectGroup(0); }
        _updating = false;
        UpdateActions();
    }

    private void FillScriptOption(string? selected)
    {
        _activationScript.Clear(); _scriptIds.Clear();
        _activationScript.AddItem("Aucun script · activation purement déclarative"); _scriptIds.Add(null);
        foreach (var choice in _scriptChoices)
        {
            _activationScript.AddItem($"{choice.Label} · source locale réelle"); _scriptIds.Add(choice.Id);
        }
        if (!string.IsNullOrWhiteSpace(selected) && !_scriptIds.Contains(selected, StringComparer.Ordinal))
        {
            _activationScript.AddItem("Source héritée indisponible · choisissez une source réelle ou aucune");
            _scriptIds.Add(selected);
        }
        var index = _scriptIds.FindIndex(item => string.Equals(item, selected, StringComparison.Ordinal));
        _activationScript.Select(Math.Max(0, index));
    }

    private void ReloadCandidateList()
    {
        _candidates.Clear();
        if (_draft is null) return;
        foreach (var candidate in _draft.Candidates)
        {
            var creature = _creatureChoices.FirstOrDefault(item => item.Id == candidate.BlueprintId);
            _candidates.AddItem($"{creature?.Label ?? "Créature protégée"} · {HumanRole(candidate.Role)} · " +
                                $"poids {candidate.Weight:0.##}, coût {candidate.Cost:0.##}, max {candidate.MaximumAlive}");
        }
    }

    private void SelectCandidate(int index)
    {
        if (_draft is null || index < 0 || index >= _draft.Candidates.Count) return;
        _candidateIndex = index;
        var candidate = _draft.Candidates[index];
        _updating = true;
        Select(_candidateBlueprint, _creatureIds, candidate.BlueprintId);
        SelectEnum(_candidateRole, candidate.Role);
        _candidateWeight.Text = Format(candidate.Weight); _candidateCost.Text = Format(candidate.Cost);
        _candidateMaximum.Text = candidate.MaximumAlive.ToString();
        _candidateConditions.Text = Csv(candidate.Conditions); _candidateVariants.Text = Csv(candidate.Variants);
        _updating = false;
        UpdateActions();
    }

    private void UpdateCandidateFromControls()
    {
        if (_draft is null || _candidateIndex < 0 || _candidateIndex >= _draft.Candidates.Count) return;
        var blueprint = Selected(_candidateBlueprint, _creatureIds);
        if (blueprint is null) return;
        var items = _draft.Candidates.ToArray();
        items[_candidateIndex] = items[_candidateIndex] with
        {
            BlueprintId = blueprint,
            Role = SelectedEnum<EncounterWorldProfile.CandidateRole>(_candidateRole),
            Weight = ReadDouble(_candidateWeight),
            Cost = ReadDouble(_candidateCost),
            MaximumAlive = ReadInt(_candidateMaximum),
            Conditions = ParseCsv(_candidateConditions.Text),
            Variants = ParseCsv(_candidateVariants.Text),
        };
        _draft = _draft with { Candidates = items };
        ReloadCandidateList();
        _candidates.Select(_candidateIndex);
    }

    private void AddCandidate()
    {
        if (_draft is null) return;
        var blueprint = Selected(_candidateBlueprint, _creatureIds);
        if (blueprint is null)
        {
            SetStatus("Choisissez explicitement une créature réelle à ajouter.", true);
            return;
        }
        if (_draft.Candidates.Any(item => item.BlueprintId == blueprint))
        {
            SetStatus("Cette créature figure déjà dans la liste.", true);
            return;
        }
        var candidate = new StudioUx2LwFCandidateDraft(Guid.NewGuid(), blueprint,
            SelectedEnum<EncounterWorldProfile.CandidateRole>(_candidateRole),
            ReadDouble(_candidateWeight), ReadDouble(_candidateCost),
            ParseCsv(_candidateConditions.Text), ParseCsv(_candidateVariants.Text),
            ReadInt(_candidateMaximum));
        var groups = _draft.Groups.Select((group, index) => index == _groupIndex
            ? group with { CandidateIds = [.. group.CandidateIds, candidate.Id] }
            : group).ToArray();
        _draft = _draft with { Candidates = [.. _draft.Candidates, candidate], Groups = groups };
        ReloadCandidateList(); ReloadGroupList();
        _candidateIndex = _draft.Candidates.Count - 1;
        _candidates.Select(_candidateIndex); SelectCandidate(_candidateIndex);
        ResetPreview("Créature ajoutée à la fiche locale · prévisualisez pour valider.");
    }

    private void RemoveCandidate()
    {
        if (_draft is null || _candidateIndex < 0 || _candidateIndex >= _draft.Candidates.Count) return;
        var removed = _draft.Candidates[_candidateIndex].Id;
        _draft = _draft with
        {
            Candidates = _draft.Candidates.Where((_, index) => index != _candidateIndex).ToArray(),
            Groups = _draft.Groups.Select(group => group with
            {
                CandidateIds = group.CandidateIds.Where(id => id != removed).ToArray(),
            }).ToArray(),
        };
        _candidateIndex = -1;
        ReloadCandidateList(); ReloadGroupList();
        if (_draft.Candidates.Count > 0) { _candidates.Select(0); SelectCandidate(0); }
        ResetPreview("Candidat retiré localement · un groupe vide sera refusé par la validation.");
    }

    private void ReloadGroupList()
    {
        _groups.Clear();
        if (_draft is null) return;
        foreach (var group in _draft.Groups)
            _groups.AddItem($"{HumanToken(group.Slug)} · {group.Minimum} à {group.Maximum} · budget {group.Budget:0.##}");
    }

    private void SelectGroup(int index)
    {
        if (_draft is null || index < 0 || index >= _draft.Groups.Count) return;
        _groupIndex = index;
        var group = _draft.Groups[index];
        _updating = true;
        _groupSlug.Text = group.Slug; _groupMinimum.Text = group.Minimum.ToString();
        _groupMaximum.Text = group.Maximum.ToString(); _groupBudget.Text = Format(group.Budget);
        _updating = false;
        UpdateActions();
    }

    private void UpdateGroupFromControls()
    {
        if (_draft is null || _groupIndex < 0 || _groupIndex >= _draft.Groups.Count) return;
        var items = _draft.Groups.ToArray();
        items[_groupIndex] = items[_groupIndex] with
        {
            Slug = _groupSlug.Text,
            Minimum = ReadInt(_groupMinimum),
            Maximum = ReadInt(_groupMaximum),
            Budget = ReadDouble(_groupBudget),
        };
        _draft = _draft with { Groups = items };
        ReloadGroupList(); _groups.Select(_groupIndex);
    }

    private void AddGroup()
    {
        if (_draft is null) return;
        var group = new StudioUx2LwFGroupDraft(Guid.NewGuid(),
            string.IsNullOrWhiteSpace(_groupSlug.Text) ? $"groupe-{_draft.Groups.Count + 1}" : _groupSlug.Text,
            ReadInt(_groupMinimum), ReadInt(_groupMaximum), ReadDouble(_groupBudget),
            _draft.Candidates.Select(item => item.Id).ToArray());
        _draft = _draft with { Groups = [.. _draft.Groups, group] };
        ReloadGroupList(); _groupIndex = _draft.Groups.Count - 1;
        _groups.Select(_groupIndex); SelectGroup(_groupIndex);
        ResetPreview("Groupe ajouté localement · prévisualisez pour valider ses limites.");
    }

    private void RemoveGroup()
    {
        if (_draft is null || _groupIndex < 0 || _groupIndex >= _draft.Groups.Count) return;
        _draft = _draft with { Groups = _draft.Groups.Where((_, index) => index != _groupIndex).ToArray() };
        _groupIndex = -1; ReloadGroupList();
        if (_draft.Groups.Count > 0) { _groups.Select(0); SelectGroup(0); }
        ResetPreview("Groupe retiré localement · une liste vide sera refusée.");
    }

    private void DraftChanged()
    {
        if (_updating || _draft is null) return;
        UpdateCandidateFromControls();
        UpdateGroupFromControls();
        _draft = ReadMainDraft(_draft);
        ResetPreview("Fiche modifiée · relancez l’aperçu sec.");
    }

    private StudioUx2LwFDraft ReadMainDraft(StudioUx2LwFDraft draft) => draft with
    {
        Slug = _slug.Text,
        DisplayName = _displayName.Text,
        Category = _category.Text,
        Theme = _theme.Text,
        PrimaryRole = SelectedEnum<EncounterWorldProfile.CandidateRole>(_primaryRole),
        Shape = SelectedEnum<EncounterWorldProfile.VolumeShape>(_shape),
        Height = ReadDouble(_height),
        Radius = ReadDouble(_radius),
        RequiredEntityTags = ParseCsv(_requiredTags.Text),
        AllowedFactions = ParseCsv(_allowedFactions.Text),
        RequireLineOfSight = _lineOfSight.ButtonPressed,
        ThreatRadius = ReadDouble(_threatRadius),
        ActivationScriptId = Selected(_activationScript, _scriptIds),
        SpawnBudget = ReadDouble(_spawnBudget),
        NetworkActorCap = ReadInt(_networkCap),
        RulesetId = _ruleset.Text,
        PartyAssessmentProviderId = _partyProvider.Text,
        DifficultyBand = SelectedEnum<EncounterDifficultyBand>(_difficultyBand),
        ExpectedPartySize = ReadInt(_partySize),
        ExpectedPartyPower = ReadDouble(_partyPower),
        BaseBudget = ReadDouble(_baseBudget),
        PartySizeMultiplier = ReadDouble(_partyMultiplier),
        DifficultyMultiplier = ReadDouble(_difficultyMultiplier),
    };

    private void Preview()
    {
        if (_draft is null)
        {
            SetStatus("Choisissez d’abord une rencontre.", true);
            return;
        }
        UpdateCandidateFromControls(); UpdateGroupFromControls();
        _draft = ReadMainDraft(_draft);
        _lastPreview = _session.PreviewEncounterUx2LwF(_draft);
        _canonicalPreviewReady = _lastPreview.Succeeded;
        Clear(_diff);
        foreach (var row in _lastPreview.Diff)
        {
            var color = row.Changed ? StudioStyle.Accent : StudioStyle.Muted;
            _diff.AddChild(StudioStyle.Label($"{row.Section} · {row.Before} → {row.After}", 10, color, wrap: true));
        }
        if (_lastPreview.Diff.Count == 0)
            _diff.AddChild(StudioStyle.Label("Aucun diff disponible.", 10, StudioStyle.Muted));
        Clear(_diagnostics);
        foreach (var diagnostic in _lastPreview.Diagnostics)
            _diagnostics.AddChild(StudioStyle.Label(
                $"{HumanPath(diagnostic.Path)} · {diagnostic.Message} · code {diagnostic.Code}",
                10, StudioStyle.Danger, wrap: true));
        if (_lastPreview.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 10, StudioStyle.Success));
        _volumeSummary.Text = _lastPreview.Runtime.Volume;
        _poolSummary.Text = _lastPreview.Runtime.Pool;
        _difficultySummary.Text = _lastPreview.Runtime.Difficulty;
        _countSummary.Text = _lastPreview.Runtime.Count;
        _boundary.Text = _lastPreview.Runtime.Boundary;
        _apply.Disabled = !_lastPreview.CanApply;
        SetStatus(_lastPreview.Message, !_lastPreview.Succeeded);
        UpdateActions();
    }

    private void Apply()
    {
        var result = _session.ApplyApprovedEncounterUx2LwF();
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) return;
        EncounterChanged?.Invoke();
        ReloadKeepingSelection();
    }

    private void Undo()
    {
        var result = _session.Undo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { EncounterChanged?.Invoke(); ReloadKeepingSelection(); }
    }

    private void Redo()
    {
        var result = _session.Redo();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { EncounterChanged?.Invoke(); ReloadKeepingSelection(); }
    }

    private void Save()
    {
        var result = _session.Save();
        SetStatus(result.Message, !result.Succeeded);
    }

    private void ReloadKeepingSelection()
    {
        var id = _draft?.ProfileId;
        Reload(clearSelection: true);
        if (id is null) return;
        var index = _encounterEntries.FindIndex(item => item?.ProfileId == id);
        if (index <= 0) return;
        _encounter.Select(index); SelectEncounter(index);
    }

    private void ResetPreview(string message)
    {
        _session.CancelEncounterPreviewUx2LwF();
        _lastPreview = null; _canonicalPreviewReady = false; _apply.Disabled = true;
        Clear(_diff); _diff.AddChild(StudioStyle.Label("Prévisualisez pour afficher le diff humain.", 10, StudioStyle.Muted));
        Clear(_diagnostics); _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic affiché.", 10, StudioStyle.Muted));
        _volumeSummary.Text = "Volume non évalué"; _poolSummary.Text = "Candidats non évalués";
        _difficultySummary.Text = "Difficulté non évaluée"; _countSummary.Text = "Comptages non évalués";
        _boundary.Text = "Projection de politique uniquement : aucun volume moteur, acteur, ForgeScript ou transport réseau n’est exécuté.";
        SetStatus(message, false); UpdateActions();
    }

    private void UpdateActions()
    {
        var hasDraft = _draft is not null;
        _preview.Disabled = !hasDraft;
        _undo.Disabled = !_session.CanUndo; _redo.Disabled = !_session.CanRedo;
        _save.Disabled = _session.Project is null;
        _removeCandidate.Disabled = _candidateIndex < 0;
        _removeGroup.Disabled = _groupIndex < 0;
    }

    private StudioUx2LwFEvidenceSourceSnapshot CaptureSourceSnapshot()
    {
        var history = _sourceSession.History ?? throw new InvalidOperationException("Historique source absent.");
        return new(_sourceSession.Project ?? throw new InvalidOperationException("Projet source absent."),
            history, history.State.Revision, history.Journal.ToArray(), _sourceSession.IsDirty,
            history.CanUndo, history.CanRedo, _sourceSession.ActiveAreaId,
            _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureEncounterApprovalUx2LwF());
    }

    private bool SourceMatchesSnapshot(StudioUx2LwFEvidenceSourceSnapshot snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) &&
               ReferenceEquals(snapshot.History, history) && history?.State.Revision == snapshot.Revision &&
               (history?.Journal ?? []).SequenceEqual(snapshot.Journal) &&
               _sourceSession.IsDirty == snapshot.IsDirty && history?.CanUndo == snapshot.CanUndo &&
               history?.CanRedo == snapshot.CanRedo && _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureEncounterApprovalUx2LwF() == snapshot.Approval;
    }

    private void ReleaseEvidence()
    {
        var snapshot = _evidenceSnapshot;
        _evidenceSession?.CancelEncounterPreviewUx2LwF();
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

    private IEnumerable<LineEdit> EditableLines()
    {
        yield return _slug; yield return _displayName; yield return _category; yield return _theme;
        yield return _height; yield return _radius; yield return _threatRadius;
        yield return _requiredTags; yield return _allowedFactions;
        yield return _candidateWeight; yield return _candidateCost; yield return _candidateMaximum;
        yield return _candidateConditions; yield return _candidateVariants;
        yield return _spawnBudget; yield return _networkCap;
        yield return _ruleset; yield return _partyProvider; yield return _partySize; yield return _partyPower;
        yield return _baseBudget; yield return _partyMultiplier; yield return _difficultyMultiplier;
        yield return _groupSlug; yield return _groupMinimum; yield return _groupMaximum; yield return _groupBudget;
    }

    private static OptionButton Option()
    {
        var option = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        StudioStyle.ApplyOptionButton(option);
        return option;
    }

    private static OptionButton EnumOption<T>(IEnumerable<T> values, Func<T, string> label)
        where T : struct, Enum
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
        line.TooltipText = "Valeur brute conservée sans arrondi ni limitation implicite ; la validation canonique décide.";
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
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 6);
        return grid;
    }

    private static void AddRow(GridContainer grid, string label, Control control)
    {
        var caption = StudioStyle.Label(label, 10, StudioStyle.Muted, wrap: true);
        caption.CustomMinimumSize = new Vector2(190, 0);
        caption.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        grid.AddChild(caption); grid.AddChild(control);
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

    private static void FillReferenceOption<T>(OptionButton option, List<T?> ids,
        IReadOnlyList<StudioUx2LwFReferenceChoice<T>> choices, string placeholder)
        where T : class
    {
        option.Clear(); ids.Clear(); option.AddItem(placeholder); ids.Add(null);
        foreach (var choice in choices) { option.AddItem($"{choice.Label} · {choice.Provenance}"); ids.Add(choice.Id); }
        option.Select(0);
    }

    private static void FillReferenceOption(OptionButton option, List<string?> ids,
        IReadOnlyList<StudioUx2LwFReferenceChoice<string>> choices, string placeholder)
    {
        option.Clear(); ids.Clear(); option.AddItem(placeholder); ids.Add(null);
        foreach (var choice in choices) { option.AddItem($"{choice.Label} · {choice.Provenance}"); ids.Add(choice.Id); }
        option.Select(0);
    }

    private static T? Selected<T>(OptionButton option, IReadOnlyList<T?> ids) where T : class =>
        option.Selected >= 0 && option.Selected < ids.Count ? ids[option.Selected] : null;

    private static string? Selected(OptionButton option, IReadOnlyList<string?> ids) =>
        option.Selected >= 0 && option.Selected < ids.Count ? ids[option.Selected] : null;

    private static void Select<T>(OptionButton option, IReadOnlyList<T?> ids, T value) where T : class
    {
        var index = ids.Select((item, position) => (item, position))
            .FirstOrDefault(pair => EqualityComparer<T?>.Default.Equals(pair.item, value)).position;
        option.Select(index);
    }

    private static T SelectedEnum<T>(OptionButton option) where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        return option.Selected >= 0 && option.Selected < values.Length ? values[option.Selected] : values[0];
    }

    private static void SelectEnum<T>(OptionButton option, T value) where T : struct, Enum
    {
        var values = Enum.GetValues<T>();
        var index = Array.IndexOf(values, value);
        option.Select(Math.Max(0, index));
    }

    private static double ReadDouble(LineEdit line)
    {
        var value = line.Text.Trim();
        if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var invariant)) return invariant;
        return double.TryParse(value, out var local) ? local : double.NaN;
    }

    private static int ReadInt(LineEdit line) => int.TryParse(line.Text.Trim(), out var value) ? value : int.MinValue;

    private static string Format(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    private static IReadOnlyList<string> ParseCsv(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(item => item.Trim()).Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).ToArray();

    private static string Csv(IEnumerable<string> values) => string.Join(", ", values);

    private string HumanAnchor(string id) => _anchorChoices.FirstOrDefault(item => item.Id == id)?.Label ??
                                              "Référence héritée protégée";

    private static string HumanRole(EncounterWorldProfile.CandidateRole role) => role switch
    {
        EncounterWorldProfile.CandidateRole.Brute => "Brute",
        EncounterWorldProfile.CandidateRole.Skirmisher => "Escarmouche",
        EncounterWorldProfile.CandidateRole.Controller => "Contrôle",
        EncounterWorldProfile.CandidateRole.Support => "Soutien",
        EncounterWorldProfile.CandidateRole.Boss => "Boss",
        EncounterWorldProfile.CandidateRole.Ambient => "Ambiance",
        _ => "Rôle inconnu",
    };

    private static string HumanDifficulty(EncounterDifficultyBand band) => band switch
    {
        EncounterDifficultyBand.Story => "Narrative",
        EncounterDifficultyBand.Easy => "Facile",
        EncounterDifficultyBand.Standard => "Standard",
        EncounterDifficultyBand.Hard => "Difficile",
        EncounterDifficultyBand.Extreme => "Extrême",
        EncounterDifficultyBand.Custom => "Personnalisée",
        _ => "Inconnue",
    };

    private static string HumanShape(EncounterWorldProfile.VolumeShape shape) => shape switch
    {
        EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D => "Polygone extrudé 2D",
        EncounterWorldProfile.VolumeShape.Box3D => "Boîte 3D",
        EncounterWorldProfile.VolumeShape.Sphere3D => "Sphère 3D",
        EncounterWorldProfile.VolumeShape.Capsule3D => "Capsule 3D",
        _ => "Forme inconnue",
    };

    private static string HumanToken(string value) => string.IsNullOrWhiteSpace(value)
        ? "sans nom"
        : Guid.TryParse(value, out _) ? "élément protégé" : value.Replace('-', ' ').Replace('_', ' ');

    private static string HumanPath(string path)
    {
        if (path.Contains("/volume", StringComparison.Ordinal)) return "Volume d’activation";
        if (path.Contains("/candidates", StringComparison.Ordinal)) return "Créatures candidates";
        if (path.Contains("/difficulty", StringComparison.Ordinal)) return "Difficulté";
        if (path.Contains("/groups", StringComparison.Ordinal)) return "Minimum et maximum";
        if (path.EndsWith("/areaId", StringComparison.Ordinal)) return "Zone";
        return "Assistant de rencontre";
    }

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
        var width = Mathf.Clamp(viewport.X - 48, 880, 1420);
        var height = Mathf.Clamp(viewport.Y - 36, 740, 1080);
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
