using System.Globalization;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCreatureProgressionOverlayUx4G : Control
{
    private StudioSessionController _session;
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioCreatureProgressionEvidenceSourceSnapshotUx4G? _sourceSnapshot;
    private BlueprintId? _evidenceBlueprintId;
    private bool _usesRealProfile;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _updating;
    private bool _applied;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioCreatureProgressionChoiceUx4G> _choices = [];
    private IReadOnlyList<StudioCreatureAbilityChoiceUx4G> _abilityChoices = [];
    private StudioCreatureProgressionDraftUx4G? _draft;
    private StudioCreatureProgressionPreviewUx4G? _previewResult;
    private readonly List<CreatureProgressionProfile.ProgressionTrack> _tracks = [];
    private readonly List<CreatureProgressionProfile.DispositionAxis> _axes = [];
    private readonly List<CreatureProgressionProfile.SkillProgression> _skills = [];
    private readonly List<CreatureProgressionProfile.TalentProgression> _talents = [];
    private readonly List<string> _learned = [];
    private readonly List<CreatureProgressionProfile.PreparedAbility> _prepared = [];

    private readonly OptionButton _creature;
    private readonly Label _context;
    private readonly TabContainer _tabs;
    private readonly LineEdit _ruleset;
    private readonly CheckBox _allowMultiple;
    private readonly CheckBox _requiredRuntime;
    private readonly ItemList _trackList;
    private readonly LineEdit _trackId;
    private readonly LineEdit _trackName;
    private readonly LineEdit _trackLevel;
    private readonly LineEdit _trackMaximum;
    private readonly LineEdit _trackExperience;
    private readonly LineEdit _trackGrants;
    private readonly ItemList _axisList;
    private readonly LineEdit _axisId;
    private readonly LineEdit _axisNegative;
    private readonly LineEdit _axisPositive;
    private readonly LineEdit _axisValue;
    private readonly ItemList _skillList;
    private readonly LineEdit _skillId;
    private readonly LineEdit _skillName;
    private readonly LineEdit _skillAttribute;
    private readonly LineEdit _skillRank;
    private readonly LineEdit _skillMaximum;
    private readonly LineEdit _skillPoints;
    private readonly CheckBox _skillTrained;
    private readonly CheckBox _skillExpertise;
    private readonly LineEdit _skillDifficulty;
    private readonly ItemList _talentList;
    private readonly LineEdit _talentId;
    private readonly LineEdit _talentName;
    private readonly LineEdit _talentRank;
    private readonly LineEdit _talentMaximum;
    private readonly CheckBox _talentPassive;
    private readonly LineEdit _talentPrerequisites;
    private readonly LineEdit _talentAbilities;
    private readonly LineEdit _talentHooks;
    private readonly LineEdit _loadoutProvider;
    private readonly OptionButton _loadoutMode;
    private readonly LineEdit _loadoutCapacity;
    private readonly ItemList _learnedList;
    private readonly OptionButton _learnedAbility;
    private readonly ItemList _preparedList;
    private readonly OptionButton _preparedAbility;
    private readonly LineEdit _preparedRank;
    private readonly LineEdit _preparedCharges;
    private readonly LineEdit _preparedCooldown;
    private readonly LineEdit _preparedResource;
    private readonly LineEdit _preparedCost;
    private readonly LineEdit _preparedModifiers;
    private readonly RichTextLabel _summary;
    private readonly VBoxContainer _diagnostics;
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
    internal bool ShowsTracks => _tracks.Count > 0 && _summary.Text.Contains("Classes", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsDisposition => _axes.Count > 0 && _summary.Text.Contains("Disposition", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsSkills => _skills.Count > 0 && _summary.Text.Contains("Compétences", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsTalents => _talents.Count > 0 && _summary.Text.Contains("Talents", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsLoadout => _summary.Text.Contains("Pouvoirs", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Classes et niveaux", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Alignement", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Compétences", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Dons et talents", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Sorts et pouvoirs préparés", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text);
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

    internal StudioCreatureProgressionOverlayUx4G(StudioSessionController session)
    {
        _session = session;
        _sourceSession = session;
        Name = "StudioCreatureProgressionOverlayUx4G";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        var shade = new ColorRect { Color = new Color(0.012f, 0.018f, 0.03f, 0.94f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        var dialog = StudioStyle.Panel("CreatureProgressionDialogUx4G", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 58; dialog.OffsetTop = 30; dialog.OffsetRight = -58; dialog.OffsetBottom = -30;
        AddChild(dialog);
        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 7); dialog.AddChild(root);
        var header = new HBoxContainer();
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("PROGRESSION ET POUVOIRS DE CRÉATURE · UX4-G", 17,
            StudioStyle.Accent, true));
        titles.AddChild(StudioStyle.Label(
            "Classes, alignement, compétences, talents et loadout · aucun UUID ni JSON affiché.",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true); close.Pressed += Close; header.AddChild(close);
        root.AddChild(header);
        var contextRow = new HBoxContainer();
        contextRow.AddChild(StudioStyle.Label("Créature", 9, StudioStyle.Muted));
        _creature = new OptionButton { CustomMinimumSize = new Vector2(360, 30) }; contextRow.AddChild(_creature);
        _context = StudioStyle.Label("Sélection explicite requise · un profil absent reste un brouillon jusqu’à Appliquer.",
            9, StudioStyle.Muted, wrap: true);
        _context.SizeFlagsHorizontal = SizeFlags.ExpandFill; contextRow.AddChild(_context); root.AddChild(contextRow);

        var tabHost = new Control { CustomMinimumSize = new Vector2(0, 410),
            SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true };
        root.AddChild(tabHost); _tabs = new TabContainer(); _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        tabHost.AddChild(_tabs);

        var classes = Tab(_tabs, "1 · Classes et niveaux");
        classes.AddChild(StudioStyle.Label("Classes et niveaux", 11, StudioStyle.Accent));
        classes.AddChild(StudioStyle.Label("Pistes génériques, niveaux, expérience et multiclassage data-driven.",
            9, StudioStyle.Muted, wrap: true));
        var classGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _ruleset = GridField(classGrid, "Ruleset", "forge.generic");
        var flags = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        flags.AddChild(StudioStyle.Label("Politiques", 9, StudioStyle.Muted));
        _allowMultiple = new CheckBox { Text = "Multiclassage autorisé" }; flags.AddChild(_allowMultiple);
        _requiredRuntime = new CheckBox { Text = "Requis au runtime" }; flags.AddChild(_requiredRuntime);
        classGrid.AddChild(flags); classGrid.AddChild(new Control()); classes.AddChild(classGrid);
        _trackList = List(classes, 112);
        var trackGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _trackId = GridField(trackGrid, "Clé de classe", "warden");
        _trackName = GridField(trackGrid, "Nom humain", "Gardien");
        _trackLevel = GridField(trackGrid, "Niveau", "1");
        _trackMaximum = GridField(trackGrid, "Niveau maximum", "20");
        _trackExperience = GridField(trackGrid, "Expérience", "0");
        _trackGrants = GridField(trackGrid, "Talents accordés (clés séparées par virgule)", "");
        classes.AddChild(trackGrid);
        classes.AddChild(CollectionButtons("Nouvelle classe", () => SaveTrack(), RemoveTrack, ClearTrack));

        var alignment = Tab(_tabs, "2 · Alignement");
        alignment.AddChild(StudioStyle.Label("Alignement", 11, StudioStyle.Accent));
        alignment.AddChild(StudioStyle.Label(
            "Axes facultatifs définis par le projet ; aucune grille morale propriétaire n’est imposée.",
            9, StudioStyle.Muted, wrap: true));
        _axisList = List(alignment, 120);
        var axisGrid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _axisId = GridField(axisGrid, "Clé de l’axe", "mercy");
        _axisNegative = GridField(axisGrid, "Pôle négatif", "Impitoyable");
        _axisPositive = GridField(axisGrid, "Pôle positif", "Miséricordieux");
        _axisValue = GridField(axisGrid, "Valeur -1..1", "0"); alignment.AddChild(axisGrid);
        alignment.AddChild(CollectionButtons("Nouvel axe", () => SaveAxis(), RemoveAxis, ClearAxis));

        var skills = Tab(_tabs, "3 · Compétences");
        skills.AddChild(StudioStyle.Label("Compétences", 11, StudioStyle.Accent));
        skills.AddChild(StudioStyle.Label("Rangs, entraînement, expertise, points et difficulté par défaut.",
            9, StudioStyle.Muted, wrap: true));
        _skillList = List(skills, 105);
        var skillGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _skillId = GridField(skillGrid, "Clé de compétence", "lore");
        _skillName = GridField(skillGrid, "Nom humain", "Savoir ancien");
        _skillAttribute = GridField(skillGrid, "Attribut directeur", "insight");
        _skillRank = GridField(skillGrid, "Rang", "0");
        _skillMaximum = GridField(skillGrid, "Rang maximum", "10");
        _skillPoints = GridField(skillGrid, "Points d’entraînement", "0");
        _skillDifficulty = GridField(skillGrid, "Difficulté par défaut", "10");
        var skillFlags = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        skillFlags.AddChild(StudioStyle.Label("Maîtrise", 9, StudioStyle.Muted));
        _skillTrained = new CheckBox { Text = "Entraînée" }; _skillExpertise = new CheckBox { Text = "Expertise" };
        skillFlags.AddChild(_skillTrained); skillFlags.AddChild(_skillExpertise); skillGrid.AddChild(skillFlags);
        skillGrid.AddChild(new Control()); skills.AddChild(skillGrid);
        skills.AddChild(CollectionButtons("Nouvelle compétence", () => SaveSkill(), RemoveSkill, ClearSkill));

        var talents = Tab(_tabs, "4 · Dons et talents");
        talents.AddChild(StudioStyle.Label("Dons et talents", 11, StudioStyle.Accent));
        talents.AddChild(StudioStyle.Label(
            "Rangs, prérequis type:clé:rang, passifs, pouvoirs accordés et hooks typés.",
            9, StudioStyle.Muted, wrap: true));
        _talentList = List(talents, 105);
        var talentGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _talentId = GridField(talentGrid, "Clé du talent", "discipline");
        _talentName = GridField(talentGrid, "Nom humain", "Discipline");
        _talentRank = GridField(talentGrid, "Rang", "1");
        _talentMaximum = GridField(talentGrid, "Rang maximum", "1");
        _talentPrerequisites = GridField(talentGrid, "Prérequis (type:clé:rang)", "track:warden:1");
        _talentAbilities = GridField(talentGrid, "Pouvoirs accordés", "");
        _talentHooks = GridField(talentGrid, "Hooks typés", "progression.on-rank");
        var passiveBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        passiveBox.AddChild(StudioStyle.Label("Type", 9, StudioStyle.Muted));
        _talentPassive = new CheckBox { Text = "Passif" }; passiveBox.AddChild(_talentPassive);
        talentGrid.AddChild(passiveBox); talentGrid.AddChild(new Control()); talents.AddChild(talentGrid);
        talents.AddChild(CollectionButtons("Nouveau talent", () => SaveTalent(), RemoveTalent, ClearTalent));

        var powers = Tab(_tabs, "5 · Sorts et pouvoirs préparés");
        powers.AddChild(StudioStyle.Label("Sorts et pouvoirs préparés", 11, StudioStyle.Accent));
        powers.AddChild(StudioStyle.Label(
            "Les choix viennent exclusivement du profil de combat de la créature ; aucun pouvoir n’est exécuté.",
            9, StudioStyle.Muted, wrap: true));
        var loadoutGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _loadoutProvider = GridField(loadoutGrid, "Fournisseur", "standard");
        _loadoutMode = Choice(loadoutGrid, "Mode", Enum.GetNames<AbilityLoadoutMode>());
        _loadoutCapacity = GridField(loadoutGrid, "Capacité", "0"); powers.AddChild(loadoutGrid);
        var powerColumns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var learnedColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        learnedColumn.AddChild(StudioStyle.Label("Pouvoirs appris", 10, StudioStyle.Muted));
        _learnedList = List(learnedColumn, 105); _learnedAbility = Choice(learnedColumn, "Pouvoir réel", []);
        learnedColumn.AddChild(SimpleButtons("Ajouter", AddLearned, RemoveLearned)); powerColumns.AddChild(learnedColumn);
        var preparedColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        preparedColumn.AddChild(StudioStyle.Label("Pouvoirs préparés", 10, StudioStyle.Muted));
        _preparedList = List(preparedColumn, 105); _preparedAbility = Choice(preparedColumn, "Pouvoir appris", []);
        var preparedGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _preparedRank = GridField(preparedGrid, "Rang", "1");
        _preparedCharges = GridField(preparedGrid, "Charges", "0");
        _preparedCooldown = GridField(preparedGrid, "Cooldown (s)", "0");
        _preparedResource = GridField(preparedGrid, "Ressource facultative", "");
        _preparedCost = GridField(preparedGrid, "Coût", "0");
        _preparedModifiers = GridField(preparedGrid, "Modificateurs", ""); preparedColumn.AddChild(preparedGrid);
        preparedColumn.AddChild(CollectionButtons("Nouveau pouvoir préparé", () => SavePrepared(), RemovePrepared,
            ClearPrepared)); powerColumns.AddChild(preparedColumn); powers.AddChild(powerColumns);

        var review = StudioStyle.Panel("CreatureProgressionReviewUx4G", StudioStyle.Surface, 10, 1);
        review.CustomMinimumSize = new Vector2(0, 145); root.AddChild(review);
        var reviewBox = new VBoxContainer(); review.AddChild(reviewBox);
        _summary = new RichTextLabel { BbcodeEnabled = true, FitContent = false, CustomMinimumSize = new Vector2(0, 92),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, ScrollActive = true };
        reviewBox.AddChild(_summary); _diagnostics = new VBoxContainer(); reviewBox.AddChild(_diagnostics);
        root.AddChild(StudioStyle.Label(
            "Prévisualiser ne modifie rien. Appliquer écrit une commande annulable ; Enregistrer persiste ensuite le workspace.",
            9, StudioStyle.Muted, wrap: true));
        var footer = new HBoxContainer();
        _preview = StudioStyle.Button("Prévisualiser", primary: true, compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true); _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true); _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        { button.CustomMinimumSize = new Vector2(button == _preview ? 132 : 104, 34); footer.AddChild(button); }
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }); root.AddChild(footer);
        ConnectEvents(); RenderEmpty("Choisissez explicitement une créature."); SetEditingEnabled(false);
    }

    internal void Open()
    {
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
        _session = _sourceSession; OpenBoundSession();
    }
    internal void RestoreSourceSurfaceAfterSmoke() { _session = _sourceSession; OpenBoundSession(); }
    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null && _evidenceRoot is null)
            return;
        _session.CancelCreatureProgressionUx4G(); _previewResult = null; _draft = null; Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }

    private void OpenBoundSession()
    {
        _session.CancelCreatureProgressionUx4G(); _previewResult = null; _draft = null; _applied = false;
        _choices = _session.CreatureProgressionChoicesUx4G; _updating = true; _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…"); foreach (var choice in _choices) _creature.AddItem(choice.Label);
        _creature.Select(0); _updating = false; ClearControls(); Visible = true; SetEditingEnabled(false);
        _context.Text = _choices.Count == 0 ? "Aucune créature éditable dans ce module." :
            "Sélection explicite requise · une progression absente sera seulement proposée.";
        RenderEmpty(_choices.Count == 0 ? "État vide réel : ajoutez d’abord un blueprint Créature." :
            "Choisissez une créature pour ouvrir sa progression canonique.");
    }

    private void SelectCreature(long selected)
    {
        if (_updating) return;
        _session.CancelCreatureProgressionUx4G(); _previewResult = null; _apply.Disabled = true;
        if (selected <= 0 || selected > _choices.Count)
        { _draft = null; ClearControls(); SetEditingEnabled(false); RenderEmpty("Sélection explicite requise."); return; }
        var choice = _choices[(int)selected - 1]; _draft = _session.CreateCreatureProgressionDraftUx4G(choice.BlueprintId);
        if (_draft is null)
        {
            ClearControls(); SetEditingEnabled(false);
            RenderEmpty(choice.IsReadOnly ? "Ce blueprint installé est en lecture seule." :
                "La progression ne peut pas être ouverte."); return;
        }
        _abilityChoices = _session.CreatureAbilityChoicesUx4G(choice.BlueprintId); PopulateAbilityChoices();
        Populate(_draft); _context.Text = $"{choice.Label} · " +
            (choice.HasProgression ? "profil existant" : "initialisation explicite proposée") +
            $" · {_abilityChoices.Count} pouvoir(s) de combat disponible(s)";
        SetEditingEnabled(true); RenderEmpty(choice.HasProgression ?
            "Progression chargée. Modifiez puis prévisualisez." :
            "Profil absent : des valeurs complètes sont proposées sans écriture avant Appliquer.");
    }

    private void Populate(StudioCreatureProgressionDraftUx4G draft)
    {
        _updating = true; _ruleset.Text = draft.RulesetId; _allowMultiple.ButtonPressed = draft.AllowMultipleTracks;
        _requiredRuntime.ButtonPressed = draft.RequiredAtRuntime;
        _tracks.Clear(); _tracks.AddRange(draft.Tracks); _axes.Clear(); _axes.AddRange(draft.DispositionAxes);
        _skills.Clear(); _skills.AddRange(draft.Skills); _talents.Clear(); _talents.AddRange(draft.Talents);
        _learned.Clear(); _learned.AddRange(draft.LearnedAbilityIds); _prepared.Clear(); _prepared.AddRange(draft.PreparedAbilities);
        _loadoutProvider.Text = draft.LoadoutProviderId; SelectChoice(_loadoutMode, draft.LoadoutMode.ToString());
        _loadoutCapacity.Text = draft.LoadoutCapacity.ToString(CultureInfo.InvariantCulture);
        RebuildTrackList(0); RebuildAxisList(0); RebuildSkillList(0); RebuildTalentList(0);
        RebuildLearnedList(0); RebuildPreparedList(0); _updating = false;
    }

    private StudioCreatureProgressionDraftUx4G? ReadDraft(bool commitEditors = true)
    {
        if (_draft is null || commitEditors && !CommitEditors()) return null;
        if (!int.TryParse(_loadoutCapacity.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var capacity) ||
            !Enum.TryParse<AbilityLoadoutMode>(_loadoutMode.GetItemText(_loadoutMode.Selected), true, out var mode))
        { SetStatus("Capacité ou mode de loadout invalide.", true); return null; }
        return _draft with
        {
            RulesetId = _ruleset.Text, AllowMultipleTracks = _allowMultiple.ButtonPressed,
            RequiredAtRuntime = _requiredRuntime.ButtonPressed, Tracks = _tracks.ToArray(),
            DispositionAxes = _axes.ToArray(), Skills = _skills.ToArray(), Talents = _talents.ToArray(),
            LoadoutProviderId = _loadoutProvider.Text, LoadoutMode = mode, LoadoutCapacity = capacity,
            LearnedAbilityIds = _learned.ToArray(), PreparedAbilities = _prepared.ToArray(),
        };
    }

    private void Preview()
    {
        var draft = ReadDraft(); if (draft is null) { RenderEmpty("Un éditeur contient une valeur invalide."); return; }
        var project = _session.Project; var history = _session.History; var revision = history?.State.Revision;
        var journal = history?.Journal.ToArray() ?? []; var dirty = _session.IsDirty;
        _previewResult = _session.PreviewCreatureProgressionUx4G(draft);
        _previewDryRun = project is not null && history is not null && ReferenceEquals(project, _session.Project) &&
                         ReferenceEquals(history, _session.History) && history.State.Revision == revision &&
                         history.Journal.SequenceEqual(journal) && _session.IsDirty == dirty;
        RenderPreview(_previewResult, _previewDryRun);
    }
    private void Apply()
    {
        var draft = ReadDraft(commitEditors: false); if (draft is null) return;
        var result = _session.ApplyApprovedCreatureProgressionUx4G(draft); _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded); if (!result.Succeeded) { _apply.Disabled = true; return; }
        CreatureChanged?.Invoke(); RefreshAfterMutation(draft.BlueprintId);
    }
    private void Undo()
    {
        var selected = _draft?.BlueprintId; var result = _session.UndoCreatureProgressionUx4G();
        SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) { CreatureChanged?.Invoke(); RefreshAfterMutation(selected); }
    }
    private void Redo()
    {
        var selected = _draft?.BlueprintId; var result = _session.RedoCreatureProgressionUx4G();
        SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) { CreatureChanged?.Invoke(); RefreshAfterMutation(selected); }
    }
    private void Save() { var result = _session.Save(); SetStatus(result.Message, !result.Succeeded); }

    private void RefreshAfterMutation(BlueprintId? selected)
    {
        _choices = _session.CreatureProgressionChoicesUx4G; _updating = true; _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…"); foreach (var choice in _choices) _creature.AddItem(choice.Label);
        var index = selected is null ? -1 : _choices.ToList().FindIndex(item => item.BlueprintId == selected);
        _creature.Select(index >= 0 ? index + 1 : 0); _updating = false; SelectCreature(index >= 0 ? index + 1 : 0);
    }

    private void RenderPreview(StudioCreatureProgressionPreviewUx4G result, bool dryRun)
    {
        _summary.Text = $"[b]{(result.Succeeded ? "APERÇU CANONIQUE" : "CONFIGURATION REFUSÉE")}[/b]\n" +
                        $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n\n" +
                        $"[color=#b8d9ff]Classes[/color] · {result.TrackSummary}\n" +
                        $"[color=#b8d9ff]Disposition[/color] · {result.DispositionSummary}\n" +
                        $"[color=#b8d9ff]Compétences[/color] · {result.SkillSummary}\n" +
                        $"[color=#b8d9ff]Talents[/color] · {result.TalentSummary}\n" +
                        $"[color=#b8d9ff]Pouvoirs[/color] · {result.LoadoutSummary}\n" +
                        $"[color=#b8d9ff]Runtime[/color] · {result.RuntimeSummary}\n" +
                        $"[color={(dryRun ? "#8ce0bd" : "#ff7777")}]Aperçu sans mutation : {dryRun.ToString().ToLowerInvariant()}[/color]";
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        if (result.Diagnostics.Count == 0)
            _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 10, StudioStyle.Success, true));
        else foreach (var item in result.Diagnostics.Take(7))
            _diagnostics.AddChild(StudioStyle.Label($"{item.Code}\n{item.HumanMessage}", 9, StudioStyle.Danger, true));
        _apply.Disabled = !result.CanApply;
    }
    private void RenderEmpty(string message)
    {
        _summary.Text = $"[color=#b8a8ce]{message}[/color]";
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        _diagnostics.AddChild(StudioStyle.Label("Aucune écriture sans aperçu approuvé.", 10, StudioStyle.Muted, true));
        _apply.Disabled = true;
    }
    private void InvalidatePreview()
    { if (_updating) return; _session.CancelCreatureProgressionUx4G(); _previewResult = null; _apply.Disabled = true; }
    private void SetEditingEnabled(bool enabled)
    {
        _preview.Disabled = !enabled; _apply.Disabled = !enabled || !CanonicalPreviewReady;
        _undo.Disabled = !_session.CanUndo; _redo.Disabled = !_session.CanRedo; _save.Disabled = _session.Project is null;
    }
    private void ClearControls()
    {
        _updating = true; foreach (var field in AllFields()) field.Text = string.Empty;
        foreach (var list in AllLists()) list.Clear(); _tracks.Clear(); _axes.Clear(); _skills.Clear(); _talents.Clear();
        _learned.Clear(); _prepared.Clear(); _allowMultiple.ButtonPressed = false; _requiredRuntime.ButtonPressed = false;
        _skillTrained.ButtonPressed = false; _skillExpertise.ButtonPressed = false; _talentPassive.ButtonPressed = false;
        _updating = false;
    }

    private void ConnectEvents()
    {
        _creature.ItemSelected += SelectCreature; foreach (var field in AllFields()) field.TextChanged += _ => InvalidatePreview();
        foreach (var choice in new[] { _loadoutMode, _learnedAbility, _preparedAbility }) choice.ItemSelected += _ => InvalidatePreview();
        foreach (var check in new[] { _allowMultiple, _requiredRuntime, _skillTrained, _skillExpertise, _talentPassive })
            check.Toggled += _ => InvalidatePreview();
        _trackList.ItemSelected += SelectTrack; _axisList.ItemSelected += SelectAxis; _skillList.ItemSelected += SelectSkill;
        _talentList.ItemSelected += SelectTalent; _learnedList.ItemSelected += SelectLearned;
        _preparedList.ItemSelected += SelectPrepared; _preview.Pressed += Preview; _apply.Pressed += Apply;
        _undo.Pressed += Undo; _redo.Pressed += Redo; _save.Pressed += Save;
    }

    private bool CommitEditors()
    {
        if (!string.IsNullOrWhiteSpace(_trackId.Text) && !SaveTrack(false)) return false;
        if (!string.IsNullOrWhiteSpace(_axisId.Text) && !SaveAxis(false)) return false;
        if (!string.IsNullOrWhiteSpace(_skillId.Text) && !SaveSkill(false)) return false;
        if (!string.IsNullOrWhiteSpace(_talentId.Text) && !SaveTalent(false)) return false;
        if (Selected(_preparedList) >= 0 && !SavePrepared(false)) return false;
        return true;
    }

    private bool SaveTrack(bool showStatus = true)
    {
        if (!TryInt(_trackLevel.Text, out var level) || !TryInt(_trackMaximum.Text, out var maximum) ||
            !TryDouble(_trackExperience.Text, out var experience))
        { if (showStatus) SetStatus("Niveaux ou expérience invalides.", true); return false; }
        var selected = Selected(_trackList); var item = new CreatureProgressionProfile.ProgressionTrack(
            _trackId.Text, _trackName.Text, level, maximum, experience, Csv(_trackGrants.Text));
        if (selected >= 0 && selected < _tracks.Count) _tracks[selected] = item; else _tracks.Add(item);
        RebuildTrackList(Math.Clamp(selected >= 0 ? selected : _tracks.Count - 1, 0, _tracks.Count - 1));
        InvalidatePreview(); return true;
    }
    private bool SaveAxis(bool showStatus = true)
    {
        if (!TryDouble(_axisValue.Text, out var value)) { if (showStatus) SetStatus("Valeur d’axe invalide.", true); return false; }
        var selected = Selected(_axisList); var item = new CreatureProgressionProfile.DispositionAxis(
            _axisId.Text, _axisNegative.Text, _axisPositive.Text, value);
        if (selected >= 0 && selected < _axes.Count) _axes[selected] = item; else _axes.Add(item);
        RebuildAxisList(Math.Clamp(selected >= 0 ? selected : _axes.Count - 1, 0, _axes.Count - 1));
        InvalidatePreview(); return true;
    }
    private bool SaveSkill(bool showStatus = true)
    {
        if (!TryInt(_skillRank.Text, out var rank) || !TryInt(_skillMaximum.Text, out var maximum) ||
            !TryInt(_skillPoints.Text, out var points) || !TryDouble(_skillDifficulty.Text, out var difficulty))
        { if (showStatus) SetStatus("Rangs, points ou difficulté invalides.", true); return false; }
        var selected = Selected(_skillList); var item = new CreatureProgressionProfile.SkillProgression(
            _skillId.Text, _skillName.Text, _skillAttribute.Text, rank, maximum, points,
            _skillTrained.ButtonPressed, _skillExpertise.ButtonPressed, difficulty);
        if (selected >= 0 && selected < _skills.Count) _skills[selected] = item; else _skills.Add(item);
        RebuildSkillList(Math.Clamp(selected >= 0 ? selected : _skills.Count - 1, 0, _skills.Count - 1));
        InvalidatePreview(); return true;
    }
    private bool SaveTalent(bool showStatus = true)
    {
        if (!TryInt(_talentRank.Text, out var rank) || !TryInt(_talentMaximum.Text, out var maximum) ||
            !TryPrerequisites(_talentPrerequisites.Text, out var prerequisites))
        { if (showStatus) SetStatus("Rangs ou prérequis invalides. Format : type:clé:rang.", true); return false; }
        var selected = Selected(_talentList); var item = new CreatureProgressionProfile.TalentProgression(
            _talentId.Text, _talentName.Text, rank, maximum, _talentPassive.ButtonPressed, prerequisites,
            Csv(_talentAbilities.Text), Csv(_talentHooks.Text));
        if (selected >= 0 && selected < _talents.Count) _talents[selected] = item; else _talents.Add(item);
        RebuildTalentList(Math.Clamp(selected >= 0 ? selected : _talents.Count - 1, 0, _talents.Count - 1));
        InvalidatePreview(); return true;
    }
    private void AddLearned()
    {
        var ability = SelectedAbility(_learnedAbility); if (string.IsNullOrWhiteSpace(ability)) return;
        if (!_learned.Contains(ability, StringComparer.Ordinal)) _learned.Add(ability);
        _learned.Sort(StringComparer.Ordinal); RebuildLearnedList(_learned.IndexOf(ability)); InvalidatePreview();
    }
    private bool SavePrepared(bool showStatus = true)
    {
        var ability = SelectedAbility(_preparedAbility);
        if (string.IsNullOrWhiteSpace(ability) || !TryInt(_preparedRank.Text, out var rank) ||
            !TryInt(_preparedCharges.Text, out var charges) || !TryDouble(_preparedCooldown.Text, out var cooldown) ||
            !TryInt(_preparedCost.Text, out var cost))
        { if (showStatus) SetStatus("Pouvoir, rang, charges, cooldown ou coût invalides.", true); return false; }
        var selected = Selected(_preparedList); var item = new CreatureProgressionProfile.PreparedAbility(
            ability, rank, charges, cooldown, Optional(_preparedResource.Text), cost, Csv(_preparedModifiers.Text));
        if (selected >= 0 && selected < _prepared.Count) _prepared[selected] = item; else _prepared.Add(item);
        RebuildPreparedList(Math.Clamp(selected >= 0 ? selected : _prepared.Count - 1, 0, _prepared.Count - 1));
        InvalidatePreview(); return true;
    }

    private void RemoveTrack() => RemoveAt(_tracks, _trackList, RebuildTrackList);
    private void RemoveAxis() => RemoveAt(_axes, _axisList, RebuildAxisList);
    private void RemoveSkill() => RemoveAt(_skills, _skillList, RebuildSkillList);
    private void RemoveTalent() => RemoveAt(_talents, _talentList, RebuildTalentList);
    private void RemoveLearned() => RemoveAt(_learned, _learnedList, RebuildLearnedList);
    private void RemovePrepared() => RemoveAt(_prepared, _preparedList, RebuildPreparedList);
    private void RemoveAt<T>(List<T> values, ItemList list, Action<int> rebuild)
    {
        var selected = Selected(list); if (selected < 0 || selected >= values.Count) return;
        values.RemoveAt(selected); rebuild(Math.Min(selected, values.Count - 1)); InvalidatePreview();
    }

    private void SelectTrack(long index)
    {
        if (_updating || index < 0 || index >= _tracks.Count) return; var item = _tracks[(int)index]; _updating = true;
        _trackId.Text = item.TrackId; _trackName.Text = item.DisplayName; _trackLevel.Text = item.Level.ToString();
        _trackMaximum.Text = item.MaximumLevel.ToString(); _trackExperience.Text = Format(item.Experience);
        _trackGrants.Text = string.Join(',', item.GrantedTalentIds); _updating = false;
    }
    private void SelectAxis(long index)
    {
        if (_updating || index < 0 || index >= _axes.Count) return; var item = _axes[(int)index]; _updating = true;
        _axisId.Text = item.AxisId; _axisNegative.Text = item.NegativeLabel; _axisPositive.Text = item.PositiveLabel;
        _axisValue.Text = Format(item.Value); _updating = false;
    }
    private void SelectSkill(long index)
    {
        if (_updating || index < 0 || index >= _skills.Count) return; var item = _skills[(int)index]; _updating = true;
        _skillId.Text = item.SkillId; _skillName.Text = item.DisplayName; _skillAttribute.Text = item.GoverningAttributeId;
        _skillRank.Text = item.Rank.ToString(); _skillMaximum.Text = item.MaximumRank.ToString();
        _skillPoints.Text = item.TrainingPoints.ToString(); _skillTrained.ButtonPressed = item.Trained;
        _skillExpertise.ButtonPressed = item.Expertise; _skillDifficulty.Text = Format(item.DefaultDifficulty);
        _updating = false;
    }
    private void SelectTalent(long index)
    {
        if (_updating || index < 0 || index >= _talents.Count) return; var item = _talents[(int)index]; _updating = true;
        _talentId.Text = item.TalentId; _talentName.Text = item.DisplayName; _talentRank.Text = item.Rank.ToString();
        _talentMaximum.Text = item.MaximumRank.ToString(); _talentPassive.ButtonPressed = item.Passive;
        _talentPrerequisites.Text = string.Join(',', item.Prerequisites.Select(value =>
            $"{value.Kind}:{value.ReferenceId}:{value.MinimumRank}"));
        _talentAbilities.Text = string.Join(',', item.GrantedAbilityIds); _talentHooks.Text = string.Join(',', item.HookIds);
        _updating = false;
    }
    private void SelectLearned(long index)
    { if (_updating || index < 0 || index >= _learned.Count) return; SelectAbilityChoice(_learnedAbility, _learned[(int)index]); }
    private void SelectPrepared(long index)
    {
        if (_updating || index < 0 || index >= _prepared.Count) return; var item = _prepared[(int)index]; _updating = true;
        SelectAbilityChoice(_preparedAbility, item.AbilityId); _preparedRank.Text = item.Rank.ToString();
        _preparedCharges.Text = item.Charges.ToString(); _preparedCooldown.Text = Format(item.CooldownSeconds);
        _preparedResource.Text = item.ResourceId ?? string.Empty; _preparedCost.Text = item.ResourceCost.ToString();
        _preparedModifiers.Text = string.Join(',', item.Modifiers); _updating = false;
    }

    private void RebuildTrackList(int selected) => Rebuild(_trackList,
        _tracks.Select(item => $"{item.DisplayName} · niveau {item.Level}/{item.MaximumLevel} · {item.Experience:0.##} XP"), selected, SelectTrack);
    private void RebuildAxisList(int selected) => Rebuild(_axisList,
        _axes.Select(item => $"{item.NegativeLabel} ← {item.Value:0.##} → {item.PositiveLabel}"), selected, SelectAxis);
    private void RebuildSkillList(int selected) => Rebuild(_skillList,
        _skills.Select(item => $"{item.DisplayName} · rang {item.Rank}/{item.MaximumRank} · {item.GoverningAttributeId}"), selected, SelectSkill);
    private void RebuildTalentList(int selected) => Rebuild(_talentList,
        _talents.Select(item => $"{item.DisplayName} · rang {item.Rank}/{item.MaximumRank} · {(item.Passive ? "passif" : "actif")}"), selected, SelectTalent);
    private void RebuildLearnedList(int selected) => Rebuild(_learnedList,
        _learned.Select(HumanAbilityLabel), selected, SelectLearned);
    private void RebuildPreparedList(int selected) => Rebuild(_preparedList,
        _prepared.Select(item => $"{HumanAbilityLabel(item.AbilityId)} · rang {item.Rank} · {item.Charges} charge(s)"), selected, SelectPrepared);
    private void Rebuild(ItemList list, IEnumerable<string> labels, int selected, Action<long> select)
    {
        _updating = true; list.Clear(); foreach (var label in labels) list.AddItem(label);
        if (selected >= 0 && selected < list.ItemCount) { list.Select(selected); _updating = false; select(selected); }
        else _updating = false;
    }

    private void ClearTrack() { _trackList.DeselectAll(); SetText(_trackId, "new-track"); SetText(_trackName, "Nouvelle classe"); SetText(_trackLevel, "1"); SetText(_trackMaximum, "20"); SetText(_trackExperience, "0"); SetText(_trackGrants, ""); }
    private void ClearAxis() { _axisList.DeselectAll(); SetText(_axisId, "mercy"); SetText(_axisNegative, "Impitoyable"); SetText(_axisPositive, "Miséricordieux"); SetText(_axisValue, "0"); }
    private void ClearSkill() { _skillList.DeselectAll(); SetText(_skillId, "lore"); SetText(_skillName, "Savoir ancien"); SetText(_skillAttribute, "insight"); SetText(_skillRank, "0"); SetText(_skillMaximum, "10"); SetText(_skillPoints, "0"); _skillTrained.ButtonPressed = false; _skillExpertise.ButtonPressed = false; SetText(_skillDifficulty, "10"); }
    private void ClearTalent() { _talentList.DeselectAll(); SetText(_talentId, "discipline"); SetText(_talentName, "Discipline"); SetText(_talentRank, "1"); SetText(_talentMaximum, "1"); _talentPassive.ButtonPressed = true; SetText(_talentPrerequisites, ""); SetText(_talentAbilities, ""); SetText(_talentHooks, "progression.on-rank"); }
    private void ClearPrepared() { _preparedList.DeselectAll(); if (_preparedAbility.ItemCount > 0) _preparedAbility.Select(0); SetText(_preparedRank, "1"); SetText(_preparedCharges, "0"); SetText(_preparedCooldown, "0"); SetText(_preparedResource, ""); SetText(_preparedCost, "0"); SetText(_preparedModifiers, ""); }

    internal bool PrepareEvidence(string mode)
    {
        _lastFailure = "none"; _previewResult = null;
        if ((_evidenceSession is not null || _evidenceRoot is not null) && !ReleaseEvidence())
        { _lastFailure = "release_previous"; return false; }
        if (!TryOpenEvidence()) { _lastFailure = _lastFailure == "none" ? "open" : _lastFailure; return false; }
        var index = _choices.ToList().FindIndex(item => item.BlueprintId == _evidenceBlueprintId);
        if (index < 0) { _lastFailure = "fixture_creature_missing"; Close(); return false; }
        _creature.Select(index + 1); SelectCreature(index + 1);
        if (_draft is null) { _lastFailure = "draft"; Close(); return false; }
        ConfigureEvidence(); var project = _session.Project!; var history = _session.History!;
        var revision = history.State.Revision; var journal = history.Journal.ToArray(); var dirty = _session.IsDirty;
        Preview(); _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                                   history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                                   _session.IsDirty == dirty && EvidenceSourceUntouched;
        _summary.Text = $"[b]{(_usesRealProfile ? "CAPTURE RÉELLE CRYPTE" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] · " +
                        $"copie isolée, source strictement inchangée.\n{_summary.Text}";
        _tabs.CurrentTab = mode.Contains("class", StringComparison.OrdinalIgnoreCase) || mode.Contains("level", StringComparison.OrdinalIgnoreCase) ? 0 :
            mode.Contains("align", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("skill", StringComparison.OrdinalIgnoreCase) ? 2 :
            mode.Contains("talent", StringComparison.OrdinalIgnoreCase) ? 3 :
            mode.Contains("power", StringComparison.OrdinalIgnoreCase) || mode.Contains("spell", StringComparison.OrdinalIgnoreCase) || mode.Contains("preview", StringComparison.OrdinalIgnoreCase) ? 4 : 0;
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsHumanSurface && ShowsTracks &&
                    ShowsDisposition && ShowsSkills && ShowsTalents && ShowsLoadout && UsesIsolatedEvidence &&
                    EvidenceSourceUntouched && (_usesRealProfile ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:human={ShowsHumanSurface}:" +
                           $"tracks={ShowsTracks}:alignment={ShowsDisposition}:skills={ShowsSkills}:talents={ShowsTalents}:" +
                           $"loadout={ShowsLoadout}:isolated={UsesIsolatedEvidence}:source={EvidenceSourceUntouched}:" +
                           $"origin={_usesRealProfile ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4G_EVIDENCE_FAILED failure={_lastFailure}"); Close(); return false;
        }
        SetStatus("Preuve UX4-G isolée prête · aucune donnée source modifiée.", false); return true;
    }

    private void ConfigureEvidence()
    {
        if (_draft is null) return;
        _draft = StudioSessionController.ChangedCreatureProgressionDraftUx4G(_draft); Populate(_draft);
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource(); if (snapshot is null) return false; StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-g-creature-progression") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session; var original = transaction.Project; var history = transaction.History;
            if (original is null || history is null) return false;
            Apply(); var applied = _applied && transaction.Project is { } appliedProject && !ReferenceEquals(original, appliedProject);
            if (applied) Undo(); var undone = applied && ReferenceEquals(original, transaction.Project);
            if (undone) Redo(); var redoneProject = transaction.Project;
            var redone = undone && redoneProject is not null && !ReferenceEquals(original, redoneProject);
            var saved = redone && transaction.Save().Succeeded; var persisted = false;
            if (saved && transaction.Session is { } workspace)
            {
                reopened = new StudioSessionController(); persisted = reopened.Open(workspace.RootDirectory).Succeeded &&
                    ProjectJsonSerializer.Serialize(reopened.Project!) == ProjectJsonSerializer.Serialize(redoneProject!);
            }
            var sourceUntouched = SourceMatches(snapshot); reopened?.Close(); reopened = null; Close();
            return applied && undone && redone && saved && persisted && sourceUntouched && EvidenceResourcesReleased &&
                   SourceSessionRestored;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4G_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close(); if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreCreatureProgressionApprovalUx4G(snapshot.Approval);
        }
    }

    internal void CloseEvidence() => Close();
    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource(); var evidence = _sourceSession.CreateUx4GEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4g-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !session.Open(root).Succeeded)
            { session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); return false; }
            _sourceSnapshot = snapshot; _evidenceSession = session; _evidenceRoot = root;
            _evidenceBlueprintId = evidence.BlueprintId; _session = session; _usesRealProfile = evidence.UsesRealProfile;
            _usesFallback = evidence.UsesCanonicalFallback; _previewDryRun = false; _resourcesReleased = false;
            _sourceRestored = false; OpenBoundSession(); return SourceMatches(snapshot) && (_usesRealProfile ^ _usesFallback);
        }
        catch (Exception exception)
        {
            _lastFailure = "open_exception:" + exception.GetType().Name; session.Close();
            _resourcesReleased = DeleteEvidenceRoot(root); _session = _sourceSession; _sourceRestored = true; return false;
        }
    }
    private StudioCreatureProgressionEvidenceSourceSnapshotUx4G? CaptureSource()
    {
        var project = _sourceSession.Project; var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureCreatureProgressionApprovalUx4G());
    }
    private bool SourceMatches(StudioCreatureProgressionEvidenceSourceSnapshotUx4G snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) && history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureCreatureProgressionApprovalUx4G() == snapshot.Approval;
    }
    private bool ReleaseEvidence()
    {
        var session = _evidenceSession; var root = _evidenceRoot; _evidenceSession = null; _evidenceRoot = null;
        _sourceSnapshot = null; _evidenceBlueprintId = null; _session = _sourceSession; _usesRealProfile = false;
        _usesFallback = false; _previewDryRun = false; var released = true;
        try { session?.Close(); } catch { released = false; }
        var rootReleased = DeleteEvidenceRoot(root); if (!rootReleased) _evidenceRoot = root;
        _resourcesReleased = released && rootReleased; _sourceRestored = true; return _resourcesReleased;
    }
    private static bool DeleteEvidenceRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var full = Path.GetFullPath(root); var temp = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4g-evidence-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch { return false; }
    }

    private void PopulateAbilityChoices()
    {
        _updating = true; _learnedAbility.Clear(); _preparedAbility.Clear();
        foreach (var choice in _abilityChoices) { _learnedAbility.AddItem(choice.Label); _preparedAbility.AddItem(choice.Label); }
        if (_abilityChoices.Count > 0) { _learnedAbility.Select(0); _preparedAbility.Select(0); }
        _updating = false;
    }
    private string SelectedAbility(OptionButton choice) => choice.Selected >= 0 && choice.Selected < _abilityChoices.Count
        ? _abilityChoices[choice.Selected].AbilityId : string.Empty;
    private void SelectAbilityChoice(OptionButton choice, string abilityId)
    {
        var index = _abilityChoices.ToList().FindIndex(item => item.AbilityId == abilityId);
        if (index >= 0) choice.Select(index);
    }
    private string HumanAbilityLabel(string abilityId) =>
        _abilityChoices.FirstOrDefault(item => item.AbilityId == abilityId)?.Label ?? $"Pouvoir hérité protégé · {abilityId}";

    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer { Name = title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto, FollowFocus = true };
        tabs.AddChild(scroll); tabs.SetTabTitle(tabs.GetTabCount() - 1, title);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 6); scroll.AddChild(content); return content;
    }
    private static ItemList List(Container parent, float height)
    {
        var list = new ItemList { CustomMinimumSize = new Vector2(0, height),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SelectMode = ItemList.SelectModeEnum.Single };
        parent.AddChild(list); return list;
    }
    private static LineEdit GridField(GridContainer parent, string label, string placeholder)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(field); parent.AddChild(box); return field;
    }
    private static OptionButton Choice(Container parent, string label, IEnumerable<string> values)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted)); var choice = new OptionButton();
        foreach (var value in values) choice.AddItem(value); box.AddChild(choice); parent.AddChild(box); return choice;
    }
    private static HBoxContainer CollectionButtons(string newLabel, Func<bool> save, Action remove, Action clear)
    {
        var row = new HBoxContainer(); var create = StudioStyle.Button(newLabel, compact: true);
        var update = StudioStyle.Button("Mettre à jour", compact: true); var delete = StudioStyle.Button("Retirer", compact: true);
        create.Pressed += clear; update.Pressed += () => save(); delete.Pressed += remove;
        row.AddChild(create); row.AddChild(update); row.AddChild(delete); return row;
    }
    private static HBoxContainer SimpleButtons(string addLabel, Action add, Action remove)
    {
        var row = new HBoxContainer(); var create = StudioStyle.Button(addLabel, compact: true);
        var delete = StudioStyle.Button("Retirer", compact: true); create.Pressed += add; delete.Pressed += remove;
        row.AddChild(create); row.AddChild(delete); return row;
    }
    private IEnumerable<LineEdit> AllFields() =>
    [
        _ruleset, _trackId, _trackName, _trackLevel, _trackMaximum, _trackExperience, _trackGrants,
        _axisId, _axisNegative, _axisPositive, _axisValue, _skillId, _skillName, _skillAttribute,
        _skillRank, _skillMaximum, _skillPoints, _skillDifficulty, _talentId, _talentName, _talentRank,
        _talentMaximum, _talentPrerequisites, _talentAbilities, _talentHooks, _loadoutProvider,
        _loadoutCapacity, _preparedRank, _preparedCharges, _preparedCooldown, _preparedResource,
        _preparedCost, _preparedModifiers,
    ];
    private IEnumerable<ItemList> AllLists() => [_trackList, _axisList, _skillList, _talentList, _learnedList, _preparedList];
    private static int Selected(ItemList list) => list.GetSelectedItems().FirstOrDefault(-1);
    private static bool TryInt(string text, out int value) => int.TryParse(text, NumberStyles.Integer,
        CultureInfo.InvariantCulture, out value);
    private static bool TryDouble(string text, out double value) => double.TryParse(text, NumberStyles.Float,
        CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string? Optional(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static IReadOnlyList<string> Csv(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries |
        StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToArray();
    private static bool TryPrerequisites(string text,
        out IReadOnlyList<CreatureProgressionProfile.TalentPrerequisite> prerequisites)
    {
        var result = new List<CreatureProgressionProfile.TalentPrerequisite>();
        foreach (var token in Csv(text))
        {
            var parts = token.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 3 || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var rank)) { prerequisites = []; return false; }
            result.Add(new(parts[0], parts[1], rank));
        }
        prerequisites = result; return true;
    }
    private static void SelectChoice(OptionButton choice, string value)
    {
        for (var index = 0; index < choice.ItemCount; index++)
            if (string.Equals(choice.GetItemText(index), value, StringComparison.OrdinalIgnoreCase))
            { choice.Select(index); return; }
    }
    private void SetText(LineEdit field, string text) { _updating = true; field.Text = text; _updating = false; }
    private void SetStatus(string message, bool error) => StatusChanged?.Invoke(message, error);
    private static IEnumerable<string> VisibleText(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            switch (child)
            {
                case Label label when label.Visible: yield return label.Text; break;
                case Button button when button.Visible: yield return button.Text; break;
                case RichTextLabel rich when rich.Visible: yield return rich.Text; break;
            }
            foreach (var text in VisibleText(child)) yield return text;
        }
    }
    private static bool ContainsGuid(string text) => System.Text.RegularExpressions.Regex.IsMatch(text,
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
}
