using System.Globalization;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCreatureRulesOverlayUx4F : Control
{
    private StudioSessionController _session;
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioCreatureRulesEvidenceSourceSnapshotUx4F? _sourceSnapshot;
    private BlueprintId? _evidenceBlueprintId;
    private bool _usesRealProfile;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _updating;
    private bool _applied;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioCreatureRulesChoiceUx4F> _choices = [];
    private StudioCreatureRulesDraftUx4F? _draft;
    private StudioCreatureRulesPreviewUx4F? _previewResult;
    private readonly List<CreatureAnimationState> _states = [];
    private readonly List<CreatureAnimationTransition> _transitions = [];
    private readonly List<CreatureFoundationResourcePool> _resources = [];
    private readonly Dictionary<string, double> _baseAttributes = new(StringComparer.Ordinal);
    private readonly List<CreatureRulesAttributeModifier> _modifiers = [];
    private readonly List<CreatureRulesDerivedAttribute> _derived = [];
    private readonly Dictionary<string, double> _resistances = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _saves = new(StringComparer.Ordinal);
    private readonly List<string> _immunities = [];

    private readonly OptionButton _creature;
    private readonly Label _context;
    private readonly TabContainer _tabs;
    private readonly LineEdit _retarget;
    private readonly ItemList _stateList;
    private readonly LineEdit _stateId;
    private readonly OptionButton _stateCategory;
    private readonly LineEdit _stateClip;
    private readonly ItemList _transitionList;
    private readonly LineEdit _transitionFrom;
    private readonly LineEdit _transitionTo;
    private readonly LineEdit _transitionCondition;
    private readonly OptionButton _lifeState;
    private readonly LineEdit _initialLoop;
    private readonly LineEdit _initialPose;
    private readonly CheckBox _initialInvisible;
    private readonly ItemList _resourceList;
    private readonly LineEdit _resourceId;
    private readonly LineEdit _resourceMaximum;
    private readonly LineEdit _resourceCurrent;
    private readonly LineEdit _resourceRegeneration;
    private readonly CheckBox _resourcePersistent;
    private readonly LineEdit _ruleset;
    private readonly ItemList _baseList;
    private readonly LineEdit _baseId;
    private readonly LineEdit _baseValue;
    private readonly ItemList _modifierList;
    private readonly LineEdit _modifierAttribute;
    private readonly LineEdit _modifierSource;
    private readonly LineEdit _modifierValue;
    private readonly ItemList _derivedList;
    private readonly LineEdit _derivedId;
    private readonly OptionButton _derivedOperation;
    private readonly LineEdit _derivedDependencies;
    private readonly LineEdit _derivedConstant;
    private readonly LineEdit _avoidance;
    private readonly LineEdit _armor;
    private readonly LineEdit _flatMitigation;
    private readonly ItemList _resistanceList;
    private readonly LineEdit _resistanceId;
    private readonly LineEdit _resistanceValue;
    private readonly ItemList _saveList;
    private readonly LineEdit _saveId;
    private readonly LineEdit _saveValue;
    private readonly ItemList _immunityList;
    private readonly LineEdit _immunityId;
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
    internal bool ShowsAnimationGraph => _states.Count >= 4 &&
        _summary.Text.Contains("Animation", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsInitialState => _summary.Text.Contains("État initial", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsResources => _resources.Count > 0 &&
        _summary.Text.Contains("Ressources", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsAttributes => _baseAttributes.Count > 0 &&
        _summary.Text.Contains("Attributs", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsDefenses => _summary.Text.Contains("Défenses", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Animation set", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("État initial et ressources", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Attributs", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Défenses et sauvegardes", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu et validation", StringComparison.OrdinalIgnoreCase) && !ContainsGuid(text);
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

    internal StudioCreatureRulesOverlayUx4F(StudioSessionController session)
    {
        _session = session;
        _sourceSession = session;
        Name = "StudioCreatureRulesOverlayUx4F";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var shade = new ColorRect { Color = new Color(0.012f, 0.018f, 0.03f, 0.94f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);
        var dialog = StudioStyle.Panel("CreatureRulesDialogUx4F", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 58; dialog.OffsetTop = 30; dialog.OffsetRight = -58; dialog.OffsetBottom = -30;
        AddChild(dialog);
        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 7);
        dialog.AddChild(root);

        var header = new HBoxContainer();
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("ANIMATION ET RÈGLES DE CRÉATURE · UX4-F", 17, StudioStyle.Accent, true));
        titles.AddChild(StudioStyle.Label(
            "Animation set, état initial, ressources, attributs, défenses et sauvegardes · aucun UUID affiché.",
            10, StudioStyle.Muted, wrap: true));
        header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true); close.Pressed += Close; header.AddChild(close);
        root.AddChild(header);

        var contextRow = new HBoxContainer();
        contextRow.AddChild(StudioStyle.Label("Créature", 9, StudioStyle.Muted));
        _creature = new OptionButton { CustomMinimumSize = new Vector2(360, 30) };
        contextRow.AddChild(_creature);
        _context = StudioStyle.Label("Sélection explicite requise · la fondation UX4-D doit exister.",
            9, StudioStyle.Muted, wrap: true);
        _context.SizeFlagsHorizontal = SizeFlags.ExpandFill; contextRow.AddChild(_context); root.AddChild(contextRow);

        var tabHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 410),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true
        };
        root.AddChild(tabHost);
        _tabs = new TabContainer(); _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); tabHost.AddChild(_tabs);

        var animation = Tab(_tabs, "1 · Animation set");
        animation.AddChild(StudioStyle.Label("Animation set", 11, StudioStyle.Accent));
        animation.AddChild(StudioStyle.Label(
            "États locomotion, combat, emote et additive, transitions résolues et profil de retargeting.",
            9, StudioStyle.Muted, wrap: true));
        _retarget = Field(animation, "Profil de retargeting", "retarget.humanoid");
        var animationColumns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var stateColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        stateColumn.AddChild(StudioStyle.Label("États", 10, StudioStyle.Text));
        _stateList = new ItemList { CustomMinimumSize = new Vector2(0, 115) }; stateColumn.AddChild(_stateList);
        _stateId = Field(stateColumn, "État", "idle");
        _stateCategory = Choice(stateColumn, "Catégorie", ["locomotion", "combat", "emote", "additive"]);
        _stateClip = Field(stateColumn, "Clip", "clip.creature.idle");
        stateColumn.AddChild(CollectionButtons("Nouvel état", () => SaveState(), RemoveState, ClearStateEditor));
        animationColumns.AddChild(stateColumn);
        var transitionColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        transitionColumn.AddChild(StudioStyle.Label("Transitions", 10, StudioStyle.Text));
        _transitionList = new ItemList { CustomMinimumSize = new Vector2(0, 115) };
        transitionColumn.AddChild(_transitionList);
        _transitionFrom = Field(transitionColumn, "Depuis", "idle");
        _transitionTo = Field(transitionColumn, "Vers", "walk");
        _transitionCondition = Field(transitionColumn, "Condition", "movement.requested");
        transitionColumn.AddChild(CollectionButtons("Nouvelle transition", () => SaveTransition(), RemoveTransition,
            ClearTransitionEditor));
        animationColumns.AddChild(transitionColumn); animation.AddChild(animationColumns);

        var initialResources = Tab(_tabs, "2 · État initial et ressources");
        initialResources.AddChild(StudioStyle.Label("État initial et ressources", 11, StudioStyle.Accent));
        initialResources.AddChild(StudioStyle.Label(
            "Pose et boucle initiales, invisibilité, puis pools complets avec courant, maximum et régénération.",
            9, StudioStyle.Muted, wrap: true));
        var initialGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _lifeState = Choice(initialGrid, "État de vie", Enum.GetNames<CreatureRulesInitialLifeState>());
        _initialLoop = GridField(initialGrid, "Boucle d’animation", "clip.creature.idle");
        _initialPose = GridField(initialGrid, "Pose", "pose.creature.idle");
        var invisibleBox = new VBoxContainer(); invisibleBox.AddChild(StudioStyle.Label("Visibilité", 9, StudioStyle.Muted));
        _initialInvisible = new CheckBox { Text = "Invisible au chargement" }; invisibleBox.AddChild(_initialInvisible);
        initialGrid.AddChild(invisibleBox); initialResources.AddChild(initialGrid);
        var resourceRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _resourceList = new ItemList { CustomMinimumSize = new Vector2(320, 205) }; resourceRow.AddChild(_resourceList);
        var resourceForm = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _resourceId = Field(resourceForm, "Ressource", "health");
        var resourceGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _resourceMaximum = GridField(resourceGrid, "Maximum", "100");
        _resourceCurrent = GridField(resourceGrid, "Courant", "100");
        _resourceRegeneration = GridField(resourceGrid, "Régénération/s", "0");
        resourceForm.AddChild(resourceGrid);
        _resourcePersistent = new CheckBox { Text = "Persistante" }; resourceForm.AddChild(_resourcePersistent);
        resourceForm.AddChild(CollectionButtons("Nouvelle ressource", () => SaveResource(), RemoveResource,
            ClearResourceEditor));
        resourceRow.AddChild(resourceForm); initialResources.AddChild(resourceRow);

        var attributes = Tab(_tabs, "3 · Attributs");
        attributes.AddChild(StudioStyle.Label("Attributs", 11, StudioStyle.Accent));
        attributes.AddChild(StudioStyle.Label(
            "Bases, modificateurs sourcés et formules inspectables Sum/Product/Ratio sans code arbitraire.",
            9, StudioStyle.Muted, wrap: true));
        _ruleset = Field(attributes, "Ruleset", "ruleset.core");
        var attributeColumns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var baseColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        baseColumn.AddChild(StudioStyle.Label("Bases", 10, StudioStyle.Text));
        _baseList = new ItemList { CustomMinimumSize = new Vector2(0, 100) }; baseColumn.AddChild(_baseList);
        _baseId = Field(baseColumn, "Attribut", "strength"); _baseValue = Field(baseColumn, "Valeur", "10");
        baseColumn.AddChild(MapButtons("Nouvelle base", () => SaveBase(), RemoveBase, ClearBaseEditor));
        attributeColumns.AddChild(baseColumn);
        var modifierColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        modifierColumn.AddChild(StudioStyle.Label("Modificateurs", 10, StudioStyle.Text));
        _modifierList = new ItemList { CustomMinimumSize = new Vector2(0, 100) }; modifierColumn.AddChild(_modifierList);
        _modifierAttribute = Field(modifierColumn, "Attribut", "strength");
        _modifierSource = Field(modifierColumn, "Source", "species.human");
        _modifierValue = Field(modifierColumn, "Valeur", "1");
        modifierColumn.AddChild(CollectionButtons("Nouveau modificateur", () => SaveModifier(), RemoveModifier,
            ClearModifierEditor)); attributeColumns.AddChild(modifierColumn);
        var derivedColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        derivedColumn.AddChild(StudioStyle.Label("Dérivés", 10, StudioStyle.Text));
        _derivedList = new ItemList { CustomMinimumSize = new Vector2(0, 100) }; derivedColumn.AddChild(_derivedList);
        _derivedId = Field(derivedColumn, "Attribut dérivé", "power");
        _derivedOperation = Choice(derivedColumn, "Opération", Enum.GetNames<CreatureRulesAttributeFormulaOperation>());
        _derivedDependencies = Field(derivedColumn, "Dépendances", "strength,vitality");
        _derivedConstant = Field(derivedColumn, "Constante", "0");
        derivedColumn.AddChild(CollectionButtons("Nouveau dérivé", () => SaveDerived(), RemoveDerived,
            ClearDerivedEditor)); attributeColumns.AddChild(derivedColumn);
        attributes.AddChild(attributeColumns);

        var defenses = Tab(_tabs, "4 · Défenses et sauvegardes");
        defenses.AddChild(StudioStyle.Label("Défenses et sauvegardes", 11, StudioStyle.Accent));
        defenses.AddChild(StudioStyle.Label(
            "Évitement, armure, mitigation, résistances, sauvegardes et immunités avec simulation déterministe.",
            9, StudioStyle.Muted, wrap: true));
        var defenseGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _avoidance = GridField(defenseGrid, "Évitement", "0");
        _armor = GridField(defenseGrid, "Armure", "0");
        _flatMitigation = GridField(defenseGrid, "Mitigation fixe", "0"); defenses.AddChild(defenseGrid);
        var defenseColumns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var resistanceColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        resistanceColumn.AddChild(StudioStyle.Label("Résistances 0..1", 10, StudioStyle.Text));
        _resistanceList = new ItemList { CustomMinimumSize = new Vector2(0, 120) };
        resistanceColumn.AddChild(_resistanceList);
        _resistanceId = Field(resistanceColumn, "Type", "fire");
        _resistanceValue = Field(resistanceColumn, "Valeur", "0.25");
        resistanceColumn.AddChild(MapButtons("Nouvelle résistance", () => SaveResistance(), RemoveResistance,
            ClearResistanceEditor)); defenseColumns.AddChild(resistanceColumn);
        var saveColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        saveColumn.AddChild(StudioStyle.Label("Sauvegardes", 10, StudioStyle.Text));
        _saveList = new ItemList { CustomMinimumSize = new Vector2(0, 120) }; saveColumn.AddChild(_saveList);
        _saveId = Field(saveColumn, "Sauvegarde", "will"); _saveValue = Field(saveColumn, "Valeur", "5");
        saveColumn.AddChild(MapButtons("Nouvelle sauvegarde", () => SaveSave(), RemoveSave, ClearSaveEditor));
        defenseColumns.AddChild(saveColumn);
        var immunityColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        immunityColumn.AddChild(StudioStyle.Label("Immunités", 10, StudioStyle.Text));
        _immunityList = new ItemList { CustomMinimumSize = new Vector2(0, 120) };
        immunityColumn.AddChild(_immunityList);
        _immunityId = Field(immunityColumn, "Immunité", "sleep");
        immunityColumn.AddChild(StringButtons("Nouvelle immunité", () => SaveImmunity(), RemoveImmunity,
            ClearImmunityEditor)); defenseColumns.AddChild(immunityColumn); defenses.AddChild(defenseColumns);

        var review = Tab(_tabs, "5 · Aperçu et validation");
        review.AddChild(StudioStyle.Label("Aperçu et validation", 11, StudioStyle.Accent));
        review.AddChild(StudioStyle.Label(
            "Projection Domain et RPG Kit uniquement : aucun clip, pose, combat, acteur, moteur ou réseau n’est exécuté.",
            9, StudioStyle.Muted, wrap: true));
        _diagnostics = new VBoxContainer(); review.AddChild(_diagnostics);

        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 145),
            FitContent = false,
            SelectionEnabled = true
        };
        root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Prévisualiser ne modifie rien. Appliquer écrit une commande annulable ; Enregistrer persiste ensuite le workspace.",
            9, StudioStyle.Muted, wrap: true));
        var footer = new HBoxContainer();
        _preview = StudioStyle.Button("Prévisualiser", primary: true, compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true);
        _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true);
        _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        {
            button.CustomMinimumSize = new Vector2(button == _preview ? 132 : 104, 34); footer.AddChild(button);
        }
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }); root.AddChild(footer);

        ConnectEvents();
        RenderEmpty("Choisissez explicitement une créature. La fondation UX4-D reste l’autorité de création.");
        SetEditingEnabled(false);
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
        _session.CancelCreatureRulesUx4F(); _previewResult = null; _draft = null; Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }

    private void OpenBoundSession()
    {
        _session.CancelCreatureRulesUx4F(); _previewResult = null; _draft = null; _applied = false;
        _choices = _session.CreatureRulesChoicesUx4F;
        _updating = true; _creature.Clear(); _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label);
        _creature.Select(0); _updating = false; ClearControls(); Visible = true; SetEditingEnabled(false);
        _context.Text = _choices.Count == 0 ? "Aucune créature éditable dans ce module." :
            "Sélection explicite requise · la fondation UX4-D doit exister.";
        RenderEmpty(_choices.Count == 0 ? "État vide réel : ajoutez d’abord un blueprint Créature." :
            "Choisissez une créature pour ouvrir ses règles canoniques.");
    }

    private void SelectCreature(long selected)
    {
        if (_updating) return;
        _session.CancelCreatureRulesUx4F(); _previewResult = null; _apply.Disabled = true;
        if (selected <= 0 || selected > _choices.Count)
        {
            _draft = null; ClearControls(); SetEditingEnabled(false); RenderEmpty("Sélection explicite requise.");
            return;
        }
        var choice = _choices[(int)selected - 1];
        _draft = _session.CreateCreatureRulesDraftUx4F(choice.BlueprintId);
        if (_draft is null)
        {
            ClearControls(); SetEditingEnabled(false);
            RenderEmpty(choice.IsReadOnly ? "Ce blueprint installé est en lecture seule." :
                !choice.HasFoundation ? "Fondation absente : utilisez d’abord Fondation créature UX4-D." :
                "Les règles ne peuvent pas être ouvertes.");
            return;
        }
        Populate(_draft);
        _context.Text = $"{choice.Label} · " +
                        (choice.HasAuthoringComposition && choice.HasRulesComposition ? "profils existants" :
                            "initialisation explicite proposée") + " · capacités et inventaire protégés";
        SetEditingEnabled(true);
        RenderEmpty(choice.HasAuthoringComposition && choice.HasRulesComposition
            ? "Profils chargés. Modifiez puis prévisualisez."
            : "Compositions absentes : des valeurs complètes sont proposées sans écriture avant Appliquer.");
    }

    private void Populate(StudioCreatureRulesDraftUx4F draft)
    {
        _updating = true;
        _retarget.Text = draft.RetargetProfileId;
        _states.Clear(); _states.AddRange(draft.AnimationStates);
        _transitions.Clear(); _transitions.AddRange(draft.AnimationTransitions);
        _resources.Clear(); _resources.AddRange(draft.Resources);
        _baseAttributes.Clear(); foreach (var item in draft.BaseAttributes) _baseAttributes[item.Key] = item.Value;
        _modifiers.Clear(); _modifiers.AddRange(draft.AttributeModifiers);
        _derived.Clear(); _derived.AddRange(draft.DerivedAttributes);
        _resistances.Clear(); foreach (var item in draft.Resistances) _resistances[item.Key] = item.Value;
        _saves.Clear(); foreach (var item in draft.Saves) _saves[item.Key] = item.Value;
        _immunities.Clear(); _immunities.AddRange(draft.Immunities);
        SelectChoice(_lifeState, draft.InitialLifeState.ToString());
        _initialLoop.Text = draft.InitialAnimationLoopAssetId ?? string.Empty;
        _initialPose.Text = draft.InitialPoseId ?? string.Empty; _initialInvisible.ButtonPressed = draft.InitiallyInvisible;
        _ruleset.Text = draft.RulesetId; _avoidance.Text = Format(draft.Avoidance); _armor.Text = Format(draft.Armor);
        _flatMitigation.Text = Format(draft.FlatMitigation);
        RebuildStateList(0); RebuildTransitionList(0); RebuildResourceList(0); RebuildBaseList(0);
        RebuildModifierList(0); RebuildDerivedList(0); RebuildResistanceList(0); RebuildSaveList(0);
        RebuildImmunityList(0); _updating = false;
    }

    private StudioCreatureRulesDraftUx4F? ReadDraft(bool commitEditors = true)
    {
        if (_draft is null || commitEditors && !CommitEditors()) return null;
        if (!Enum.TryParse<CreatureRulesInitialLifeState>(_lifeState.GetItemText(_lifeState.Selected), true,
                out var life) || !TryDouble(_avoidance.Text, out var avoidance) ||
            !TryDouble(_armor.Text, out var armor) || !TryDouble(_flatMitigation.Text, out var mitigation))
        {
            SetStatus("État initial ou valeurs de défense invalides.", true); return null;
        }
        return _draft with
        {
            RetargetProfileId = _retarget.Text,
            AnimationStates = _states.ToArray(),
            AnimationTransitions = _transitions.ToArray(),
            InitialLifeState = life,
            InitialAnimationLoopAssetId = Optional(_initialLoop.Text),
            InitialPoseId = Optional(_initialPose.Text),
            InitiallyInvisible = _initialInvisible.ButtonPressed,
            Resources = _resources.ToArray(),
            RulesetId = _ruleset.Text,
            BaseAttributes = _baseAttributes.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            AttributeModifiers = _modifiers.ToArray(),
            DerivedAttributes = _derived.ToArray(),
            Avoidance = avoidance,
            Armor = armor,
            Resistances = _resistances.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            Saves = _saves.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            Immunities = _immunities.ToArray(),
            FlatMitigation = mitigation,
        };
    }

    private void Preview()
    {
        var draft = ReadDraft();
        if (draft is null) { RenderEmpty("Un éditeur contient une valeur invalide."); return; }
        var project = _session.Project; var history = _session.History; var revision = history?.State.Revision;
        var journal = history?.Journal.ToArray() ?? []; var dirty = _session.IsDirty;
        _previewResult = _session.PreviewCreatureRulesUx4F(draft);
        _previewDryRun = project is not null && history is not null && ReferenceEquals(project, _session.Project) &&
                         ReferenceEquals(history, _session.History) && history.State.Revision == revision &&
                         history.Journal.SequenceEqual(journal) && _session.IsDirty == dirty;
        RenderPreview(_previewResult, _previewDryRun);
    }

    private void Apply()
    {
        var draft = ReadDraft(commitEditors: false); if (draft is null) return;
        var result = _session.ApplyApprovedCreatureRulesUx4F(draft); _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded);
        if (!result.Succeeded) { _apply.Disabled = true; return; }
        CreatureChanged?.Invoke(); RefreshAfterMutation(draft.BlueprintId);
    }

    private void Undo()
    {
        var selected = _draft?.BlueprintId; var result = _session.UndoCreatureRulesUx4F();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { CreatureChanged?.Invoke(); RefreshAfterMutation(selected); }
    }

    private void Redo()
    {
        var selected = _draft?.BlueprintId; var result = _session.RedoCreatureRulesUx4F();
        SetStatus(result.Message, !result.Succeeded);
        if (result.Succeeded) { CreatureChanged?.Invoke(); RefreshAfterMutation(selected); }
    }

    private void Save()
    {
        var result = _session.Save(); SetStatus(result.Message, !result.Succeeded);
    }

    private void RefreshAfterMutation(BlueprintId? selected)
    {
        _choices = _session.CreatureRulesChoicesUx4F; _updating = true; _creature.Clear();
        _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label);
        var index = selected is null ? -1 : _choices.ToList().FindIndex(item => item.BlueprintId == selected);
        _creature.Select(index >= 0 ? index + 1 : 0); _updating = false; SelectCreature(index >= 0 ? index + 1 : 0);
    }

    private void RenderPreview(StudioCreatureRulesPreviewUx4F result, bool dryRun)
    {
        _summary.Text = $"[b]{(result.Succeeded ? "APERÇU CANONIQUE" : "CONFIGURATION REFUSÉE")}[/b]\n" +
                        $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n\n" +
                        $"[color=#b8d9ff]Animation set[/color] · {result.AnimationSummary}\n" +
                        $"[color=#b8d9ff]État initial[/color] · {result.InitialStateSummary}\n" +
                        $"[color=#b8d9ff]Ressources[/color] · {result.ResourceSummary}\n" +
                        $"[color=#b8d9ff]Attributs[/color] · {result.AttributeSummary}\n" +
                        $"[color=#b8d9ff]Défenses[/color] · {result.DefenseSummary}\n" +
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
    {
        if (_updating) return;
        _session.CancelCreatureRulesUx4F(); _previewResult = null; _apply.Disabled = true;
    }

    private void SetEditingEnabled(bool enabled)
    {
        _preview.Disabled = !enabled; _apply.Disabled = !enabled || !CanonicalPreviewReady;
        _undo.Disabled = !_session.CanUndo; _redo.Disabled = !_session.CanRedo; _save.Disabled = _session.Project is null;
    }

    private void ClearControls()
    {
        _updating = true;
        foreach (var field in AllFields()) field.Text = string.Empty;
        _states.Clear(); _transitions.Clear(); _resources.Clear(); _baseAttributes.Clear(); _modifiers.Clear();
        _derived.Clear(); _resistances.Clear(); _saves.Clear(); _immunities.Clear();
        foreach (var list in AllLists()) list.Clear();
        _initialInvisible.ButtonPressed = false; _resourcePersistent.ButtonPressed = false;
        _updating = false;
    }

    private void ConnectEvents()
    {
        _creature.ItemSelected += SelectCreature;
        foreach (var field in AllFields()) field.TextChanged += _ => InvalidatePreview();
        foreach (var choice in new[] { _stateCategory, _lifeState, _derivedOperation })
            choice.ItemSelected += _ => InvalidatePreview();
        _initialInvisible.Toggled += _ => InvalidatePreview(); _resourcePersistent.Toggled += _ => InvalidatePreview();
        _stateList.ItemSelected += SelectState; _transitionList.ItemSelected += SelectTransition;
        _resourceList.ItemSelected += SelectResource; _baseList.ItemSelected += SelectBase;
        _modifierList.ItemSelected += SelectModifier; _derivedList.ItemSelected += SelectDerived;
        _resistanceList.ItemSelected += SelectResistance; _saveList.ItemSelected += SelectSave;
        _immunityList.ItemSelected += SelectImmunity;
        _preview.Pressed += Preview; _apply.Pressed += Apply; _undo.Pressed += Undo; _redo.Pressed += Redo;
        _save.Pressed += Save;
    }

    private bool CommitEditors()
    {
        if (!string.IsNullOrWhiteSpace(_stateId.Text) && !SaveState(false)) return false;
        if (!string.IsNullOrWhiteSpace(_transitionFrom.Text) && !SaveTransition(false)) return false;
        if (!string.IsNullOrWhiteSpace(_resourceId.Text) && !SaveResource(false)) return false;
        if (!string.IsNullOrWhiteSpace(_baseId.Text) && !SaveBase(false)) return false;
        if (!string.IsNullOrWhiteSpace(_modifierAttribute.Text) && !SaveModifier(false)) return false;
        if (!string.IsNullOrWhiteSpace(_derivedId.Text) && !SaveDerived(false)) return false;
        if (!string.IsNullOrWhiteSpace(_resistanceId.Text) && !SaveResistance(false)) return false;
        if (!string.IsNullOrWhiteSpace(_saveId.Text) && !SaveSave(false)) return false;
        if (!string.IsNullOrWhiteSpace(_immunityId.Text) && !SaveImmunity(false)) return false;
        return true;
    }

    private bool SaveState(bool showStatus = true)
    {
        var category = _stateCategory.GetItemText(_stateCategory.Selected);
        var selected = Selected(_stateList); var id = selected >= 0 && selected < _states.Count
            ? _states[selected].Id : Guid.NewGuid();
        var item = new CreatureAnimationState(id, _stateId.Text, category, _stateClip.Text);
        if (selected >= 0 && selected < _states.Count) _states[selected] = item; else _states.Add(item);
        RebuildStateList(Math.Clamp(selected >= 0 ? selected : _states.Count - 1, 0, _states.Count - 1));
        InvalidatePreview(); return true;
    }

    private bool SaveTransition(bool showStatus = true)
    {
        var selected = Selected(_transitionList); var id = selected >= 0 && selected < _transitions.Count
            ? _transitions[selected].Id : Guid.NewGuid();
        var item = new CreatureAnimationTransition(id, _transitionFrom.Text, _transitionTo.Text,
            _transitionCondition.Text);
        if (selected >= 0 && selected < _transitions.Count) _transitions[selected] = item;
        else _transitions.Add(item);
        RebuildTransitionList(Math.Clamp(selected >= 0 ? selected : _transitions.Count - 1, 0,
            _transitions.Count - 1)); InvalidatePreview(); return true;
    }

    private bool SaveResource(bool showStatus = true)
    {
        if (!TryDouble(_resourceMaximum.Text, out var maximum) || !TryDouble(_resourceCurrent.Text, out var current) ||
            !TryDouble(_resourceRegeneration.Text, out var regeneration))
        {
            if (showStatus) SetStatus("Maximum, courant et régénération doivent être numériques.", true); return false;
        }
        var selected = Selected(_resourceList); var id = selected >= 0 && selected < _resources.Count
            ? _resources[selected].Id : Guid.NewGuid();
        var item = new CreatureFoundationResourcePool(id, _resourceId.Text, maximum, current, regeneration,
            _resourcePersistent.ButtonPressed);
        if (selected >= 0 && selected < _resources.Count) _resources[selected] = item; else _resources.Add(item);
        RebuildResourceList(Math.Clamp(selected >= 0 ? selected : _resources.Count - 1, 0, _resources.Count - 1));
        InvalidatePreview(); return true;
    }

    private bool SaveBase(bool showStatus = true)
    {
        if (!TryDouble(_baseValue.Text, out var value))
        { if (showStatus) SetStatus("La valeur de base doit être numérique.", true); return false; }
        var selected = Selected(_baseList); if (selected >= 0 && selected < Ordered(_baseAttributes).Count)
            _baseAttributes.Remove(Ordered(_baseAttributes)[selected].Key);
        _baseAttributes[_baseId.Text] = value; RebuildBaseList(IndexOf(_baseAttributes, _baseId.Text));
        InvalidatePreview(); return true;
    }

    private bool SaveModifier(bool showStatus = true)
    {
        if (!TryDouble(_modifierValue.Text, out var value))
        { if (showStatus) SetStatus("La valeur du modificateur doit être numérique.", true); return false; }
        var selected = Selected(_modifierList); var id = selected >= 0 && selected < _modifiers.Count
            ? _modifiers[selected].Id : Guid.NewGuid();
        var item = new CreatureRulesAttributeModifier(id, _modifierAttribute.Text, _modifierSource.Text, value);
        if (selected >= 0 && selected < _modifiers.Count) _modifiers[selected] = item; else _modifiers.Add(item);
        RebuildModifierList(Math.Clamp(selected >= 0 ? selected : _modifiers.Count - 1, 0, _modifiers.Count - 1));
        InvalidatePreview(); return true;
    }

    private bool SaveDerived(bool showStatus = true)
    {
        if (!TryDouble(_derivedConstant.Text, out var constant) ||
            !Enum.TryParse<CreatureRulesAttributeFormulaOperation>(
                _derivedOperation.GetItemText(_derivedOperation.Selected), true, out var operation))
        { if (showStatus) SetStatus("Opération ou constante dérivée invalide.", true); return false; }
        var selected = Selected(_derivedList); var id = selected >= 0 && selected < _derived.Count
            ? _derived[selected].Id : Guid.NewGuid();
        var item = new CreatureRulesDerivedAttribute(id, _derivedId.Text, operation, Csv(_derivedDependencies.Text),
            constant);
        if (selected >= 0 && selected < _derived.Count) _derived[selected] = item; else _derived.Add(item);
        RebuildDerivedList(Math.Clamp(selected >= 0 ? selected : _derived.Count - 1, 0, _derived.Count - 1));
        InvalidatePreview(); return true;
    }

    private bool SaveResistance(bool showStatus = true) => SaveMap(_resistances, _resistanceList,
        _resistanceId, _resistanceValue, RebuildResistanceList, showStatus);
    private bool SaveSave(bool showStatus = true) => SaveMap(_saves, _saveList, _saveId, _saveValue,
        RebuildSaveList, showStatus);

    private bool SaveMap(Dictionary<string, double> map, ItemList list, LineEdit id, LineEdit value,
        Action<int> rebuild, bool showStatus)
    {
        if (!TryDouble(value.Text, out var numeric))
        { if (showStatus) SetStatus("La valeur doit être numérique.", true); return false; }
        var selected = Selected(list); if (selected >= 0 && selected < Ordered(map).Count)
            map.Remove(Ordered(map)[selected].Key);
        map[id.Text] = numeric; rebuild(IndexOf(map, id.Text)); InvalidatePreview(); return true;
    }

    private bool SaveImmunity(bool showStatus = true)
    {
        var selected = Selected(_immunityList);
        if (selected >= 0 && selected < _immunities.Count) _immunities[selected] = _immunityId.Text;
        else _immunities.Add(_immunityId.Text);
        _immunities.Sort(StringComparer.Ordinal); RebuildImmunityList(_immunities.IndexOf(_immunityId.Text));
        InvalidatePreview(); return true;
    }

    private void RemoveState() => RemoveAt(_states, _stateList, RebuildStateList);
    private void RemoveTransition() => RemoveAt(_transitions, _transitionList, RebuildTransitionList);
    private void RemoveResource() => RemoveAt(_resources, _resourceList, RebuildResourceList);
    private void RemoveModifier() => RemoveAt(_modifiers, _modifierList, RebuildModifierList);
    private void RemoveDerived() => RemoveAt(_derived, _derivedList, RebuildDerivedList);
    private void RemoveImmunity() => RemoveAt(_immunities, _immunityList, RebuildImmunityList);
    private void RemoveBase() => RemoveMap(_baseAttributes, _baseList, RebuildBaseList);
    private void RemoveResistance() => RemoveMap(_resistances, _resistanceList, RebuildResistanceList);
    private void RemoveSave() => RemoveMap(_saves, _saveList, RebuildSaveList);

    private void RemoveAt<T>(List<T> values, ItemList list, Action<int> rebuild)
    {
        var selected = Selected(list); if (selected < 0 || selected >= values.Count) return;
        values.RemoveAt(selected); rebuild(Math.Min(selected, values.Count - 1)); InvalidatePreview();
    }

    private void RemoveMap(Dictionary<string, double> map, ItemList list, Action<int> rebuild)
    {
        var selected = Selected(list); var ordered = Ordered(map); if (selected < 0 || selected >= ordered.Count) return;
        map.Remove(ordered[selected].Key); rebuild(Math.Min(selected, map.Count - 1)); InvalidatePreview();
    }

    private void SelectState(long index)
    {
        if (_updating || index < 0 || index >= _states.Count) return; var item = _states[(int)index];
        _updating = true; _stateId.Text = item.StateId; SelectChoice(_stateCategory, item.Category);
        _stateClip.Text = item.ClipAssetId; _updating = false;
    }
    private void SelectTransition(long index)
    {
        if (_updating || index < 0 || index >= _transitions.Count) return; var item = _transitions[(int)index];
        _updating = true; _transitionFrom.Text = item.FromStateId; _transitionTo.Text = item.ToStateId;
        _transitionCondition.Text = item.ConditionId; _updating = false;
    }
    private void SelectResource(long index)
    {
        if (_updating || index < 0 || index >= _resources.Count) return; var item = _resources[(int)index];
        _updating = true; _resourceId.Text = item.ResourceId; _resourceMaximum.Text = Format(item.Maximum);
        _resourceCurrent.Text = Format(item.Current); _resourceRegeneration.Text = Format(item.RegenerationPerSecond);
        _resourcePersistent.ButtonPressed = item.Persistent; _updating = false;
    }
    private void SelectBase(long index) => SelectMap(_baseAttributes, index, _baseId, _baseValue);
    private void SelectResistance(long index) => SelectMap(_resistances, index, _resistanceId, _resistanceValue);
    private void SelectSave(long index) => SelectMap(_saves, index, _saveId, _saveValue);
    private void SelectModifier(long index)
    {
        if (_updating || index < 0 || index >= _modifiers.Count) return; var item = _modifiers[(int)index];
        _updating = true; _modifierAttribute.Text = item.AttributeId; _modifierSource.Text = item.SourceId;
        _modifierValue.Text = Format(item.Value); _updating = false;
    }
    private void SelectDerived(long index)
    {
        if (_updating || index < 0 || index >= _derived.Count) return; var item = _derived[(int)index];
        _updating = true; _derivedId.Text = item.AttributeId; SelectChoice(_derivedOperation, item.Operation.ToString());
        _derivedDependencies.Text = string.Join(',', item.Dependencies); _derivedConstant.Text = Format(item.Constant);
        _updating = false;
    }
    private void SelectImmunity(long index)
    {
        if (_updating || index < 0 || index >= _immunities.Count) return;
        _updating = true; _immunityId.Text = _immunities[(int)index]; _updating = false;
    }
    private void SelectMap(Dictionary<string, double> map, long index, LineEdit id, LineEdit value)
    {
        var ordered = Ordered(map); if (_updating || index < 0 || index >= ordered.Count) return;
        _updating = true; id.Text = ordered[(int)index].Key; value.Text = Format(ordered[(int)index].Value);
        _updating = false;
    }

    private void RebuildStateList(int selected) => Rebuild(_stateList,
        _states.Select(item => $"{item.StateId} · {item.Category} · {item.ClipAssetId}"), selected, SelectState);
    private void RebuildTransitionList(int selected) => Rebuild(_transitionList,
        _transitions.Select(item => $"{item.FromStateId} → {item.ToStateId} · {item.ConditionId}"), selected,
        SelectTransition);
    private void RebuildResourceList(int selected) => Rebuild(_resourceList,
        _resources.Select(item => $"{item.ResourceId} · {item.Current:0.##}/{item.Maximum:0.##} · +{item.RegenerationPerSecond:0.##}/s"),
        selected, SelectResource);
    private void RebuildBaseList(int selected) => Rebuild(_baseList,
        Ordered(_baseAttributes).Select(item => $"{item.Key} · {item.Value:0.###}"), selected, SelectBase);
    private void RebuildModifierList(int selected) => Rebuild(_modifierList,
        _modifiers.Select(item => $"{item.AttributeId} · {item.SourceId} · {item.Value:+0.###;-0.###;0}"),
        selected, SelectModifier);
    private void RebuildDerivedList(int selected) => Rebuild(_derivedList,
        _derived.Select(item => $"{item.AttributeId} · {item.Operation}({string.Join(',', item.Dependencies)})"),
        selected, SelectDerived);
    private void RebuildResistanceList(int selected) => Rebuild(_resistanceList,
        Ordered(_resistances).Select(item => $"{item.Key} · {item.Value:P0}"), selected, SelectResistance);
    private void RebuildSaveList(int selected) => Rebuild(_saveList,
        Ordered(_saves).Select(item => $"{item.Key} · {item.Value:0.###}"), selected, SelectSave);
    private void RebuildImmunityList(int selected) => Rebuild(_immunityList, _immunities, selected, SelectImmunity);

    private void Rebuild(ItemList list, IEnumerable<string> labels, int selected, Action<long> select)
    {
        _updating = true; list.Clear(); foreach (var label in labels) list.AddItem(label);
        if (selected >= 0 && selected < list.ItemCount) { list.Select(selected); _updating = false; select(selected); }
        else _updating = false;
    }

    private void ClearStateEditor() { _stateList.DeselectAll(); SetText(_stateId, "new-state"); SelectChoice(_stateCategory, "emote"); SetText(_stateClip, "clip.creature.new"); }
    private void ClearTransitionEditor() { _transitionList.DeselectAll(); SetText(_transitionFrom, "idle"); SetText(_transitionTo, "walk"); SetText(_transitionCondition, "movement.requested"); }
    private void ClearResourceEditor() { _resourceList.DeselectAll(); SetText(_resourceId, "mana"); SetText(_resourceMaximum, "100"); SetText(_resourceCurrent, "100"); SetText(_resourceRegeneration, "0"); _resourcePersistent.ButtonPressed = false; }
    private void ClearBaseEditor() { _baseList.DeselectAll(); SetText(_baseId, "strength"); SetText(_baseValue, "10"); }
    private void ClearModifierEditor() { _modifierList.DeselectAll(); SetText(_modifierAttribute, "strength"); SetText(_modifierSource, "species.human"); SetText(_modifierValue, "1"); }
    private void ClearDerivedEditor() { _derivedList.DeselectAll(); SetText(_derivedId, "power"); SelectChoice(_derivedOperation, "Sum"); SetText(_derivedDependencies, "strength,vitality"); SetText(_derivedConstant, "0"); }
    private void ClearResistanceEditor() { _resistanceList.DeselectAll(); SetText(_resistanceId, "fire"); SetText(_resistanceValue, "0.25"); }
    private void ClearSaveEditor() { _saveList.DeselectAll(); SetText(_saveId, "will"); SetText(_saveValue, "5"); }
    private void ClearImmunityEditor() { _immunityList.DeselectAll(); SetText(_immunityId, "sleep"); }

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
        ConfigureEvidence();
        var project = _session.Project!; var history = _session.History!; var revision = history.State.Revision;
        var journal = history.Journal.ToArray(); var dirty = _session.IsDirty;
        Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched;
        _summary.Text = $"[b]{(_usesRealProfile ? "CAPTURE RÉELLE CRYPTE" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] · " +
                        $"copie isolée, source strictement inchangée.\n{_summary.Text}";
        _tabs.CurrentTab = mode.Contains("animation", StringComparison.OrdinalIgnoreCase) ? 0 :
            mode.Contains("initial", StringComparison.OrdinalIgnoreCase) || mode.Contains("resource", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("attribute", StringComparison.OrdinalIgnoreCase) ? 2 :
            mode.Contains("defense", StringComparison.OrdinalIgnoreCase) || mode.Contains("save", StringComparison.OrdinalIgnoreCase) ? 3 :
            mode.Contains("preview", StringComparison.OrdinalIgnoreCase) ? 4 : 0;
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsHumanSurface &&
                    ShowsAnimationGraph && ShowsInitialState && ShowsResources && ShowsAttributes && ShowsDefenses &&
                    UsesIsolatedEvidence && EvidenceSourceUntouched && (_usesRealProfile ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:human={ShowsHumanSurface}:" +
                           $"animation={ShowsAnimationGraph}:initial={ShowsInitialState}:resources={ShowsResources}:" +
                           $"attributes={ShowsAttributes}:defenses={ShowsDefenses}:isolated={UsesIsolatedEvidence}:" +
                           $"source={EvidenceSourceUntouched}:origin={_usesRealProfile ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4F_EVIDENCE_FAILED failure={_lastFailure}"); Close(); return false;
        }
        SetStatus("Preuve UX4-F isolée prête · aucune donnée source modifiée.", false); return true;
    }

    private void ConfigureEvidence()
    {
        _retarget.Text = "retarget.proof";
        if (_states.All(item => item.StateId != "victory"))
        {
            _states.Add(new(StudioSessionController.StableCreatureRulesIdUx4F(_draft!.BlueprintId.Value,
                "ui-state-victory"), "victory", "emote", "clip.proof.victory")); RebuildStateList(_states.Count - 1);
        }
        SelectChoice(_lifeState, CreatureRulesInitialLifeState.Sleeping.ToString());
        _initialLoop.Text = "clip.proof.sleeping"; _initialPose.Text = "pose.proof.sleeping";
        _initialInvisible.ButtonPressed = true;
        if (_resources.All(item => item.ResourceId != "mana"))
        {
            _resources.Add(new(StudioSessionController.StableCreatureRulesIdUx4F(_draft!.BlueprintId.Value,
                "ui-resource-mana"), "mana", 80, 32, 1.5, false)); RebuildResourceList(_resources.Count - 1);
        }
        _ruleset.Text = "ruleset.proof"; _baseAttributes["strength"] = 9; _baseAttributes["vitality"] = 11;
        RebuildBaseList(IndexOf(_baseAttributes, "strength")); _avoidance.Text = "5"; _armor.Text = "3";
        _flatMitigation.Text = "2"; _resistances["fire"] = 0.35; _saves["will"] = 7;
        if (!_immunities.Contains("sleep", StringComparer.Ordinal)) _immunities.Add("sleep");
        _immunities.Sort(StringComparer.Ordinal); RebuildResistanceList(IndexOf(_resistances, "fire"));
        RebuildSaveList(IndexOf(_saves, "will")); RebuildImmunityList(_immunities.IndexOf("sleep"));
    }

    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource(); if (snapshot is null) return false; StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-f-creature-animation-rules") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session; var original = transaction.Project; var history = transaction.History;
            if (original is null || history is null) return false;
            Apply(); var applied = _applied && transaction.Project is { } appliedProject &&
                                        !ReferenceEquals(original, appliedProject);
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
            return applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4F_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}");
            return false;
        }
        finally
        {
            reopened?.Close(); if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreCreatureRulesApprovalUx4F(snapshot.Approval);
        }
    }

    internal void CloseEvidence() => Close();

    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource(); var evidence = _sourceSession.CreateUx4FEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4f-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !session.Open(root).Succeeded)
            { session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); return false; }
            _sourceSnapshot = snapshot; _evidenceSession = session; _evidenceRoot = root;
            _evidenceBlueprintId = evidence.BlueprintId; _session = session; _usesRealProfile = evidence.UsesRealProfile;
            _usesFallback = evidence.UsesCanonicalFallback; _previewDryRun = false; _resourcesReleased = false;
            _sourceRestored = false; OpenBoundSession();
            return SourceMatches(snapshot) && (_usesRealProfile ^ _usesFallback);
        }
        catch (Exception exception)
        {
            _lastFailure = "open_exception:" + exception.GetType().Name; session.Close();
            _resourcesReleased = DeleteEvidenceRoot(root); _session = _sourceSession; _sourceRestored = true;
            return false;
        }
    }

    private StudioCreatureRulesEvidenceSourceSnapshotUx4F? CaptureSource()
    {
        var project = _sourceSession.Project; var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureCreatureRulesApprovalUx4F());
    }

    private bool SourceMatches(StudioCreatureRulesEvidenceSourceSnapshotUx4F snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision &&
               history.Journal.SequenceEqual(snapshot.Journal) && history.CanUndo == snapshot.CanUndo &&
               history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId &&
               _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureCreatureRulesApprovalUx4F() == snapshot.Approval;
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
                !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4f-evidence-", StringComparison.Ordinal))
                return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
            return !Directory.Exists(full) && !File.Exists(full);
        }
        catch { return false; }
    }

    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true
        };
        tabs.AddChild(scroll); tabs.SetTabTitle(tabs.GetTabCount() - 1, title);
        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 6); scroll.AddChild(content); return content;
    }

    private static LineEdit Field(VBoxContainer parent, string label, string placeholder)
    {
        parent.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted));
        var field = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(field); return field;
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

    private static HBoxContainer MapButtons(string newLabel, Func<bool> save, Action remove, Action clear) =>
        CollectionButtons(newLabel, save, remove, clear);
    private static HBoxContainer StringButtons(string newLabel, Func<bool> save, Action remove, Action clear) =>
        CollectionButtons(newLabel, save, remove, clear);

    private IEnumerable<LineEdit> AllFields() =>
    [
        _retarget, _stateId, _stateClip, _transitionFrom, _transitionTo, _transitionCondition, _initialLoop,
        _initialPose, _resourceId, _resourceMaximum, _resourceCurrent, _resourceRegeneration, _ruleset, _baseId,
        _baseValue, _modifierAttribute, _modifierSource, _modifierValue, _derivedId, _derivedDependencies,
        _derivedConstant, _avoidance, _armor, _flatMitigation, _resistanceId, _resistanceValue, _saveId,
        _saveValue, _immunityId,
    ];

    private IEnumerable<ItemList> AllLists() =>
    [
        _stateList, _transitionList, _resourceList, _baseList, _modifierList, _derivedList, _resistanceList,
        _saveList, _immunityList,
    ];

    private static int Selected(ItemList list) => list.GetSelectedItems().FirstOrDefault(-1);
    private static IReadOnlyList<KeyValuePair<string, double>> Ordered(Dictionary<string, double> map) =>
        map.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
    private static int IndexOf(Dictionary<string, double> map, string key) =>
        Ordered(map).ToList().FindIndex(item => item.Key == key);
    private static bool TryDouble(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string? Optional(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static IReadOnlyList<string> Csv(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries |
        StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToArray();
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

    private static bool ContainsGuid(string text) =>
        System.Text.RegularExpressions.Regex.IsMatch(text,
            "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
}
