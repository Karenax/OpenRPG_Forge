#if TOOLS
using System.Globalization;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Spatial;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class CreatureRulesCompositionWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<BlueprintId, CreatureFoundationProfile, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private BlueprintId? _selectedId;
    private Guid _profileId;
    private Guid _initialId;
    private Guid _attributesId;
    private Guid _defenseId;
    private Guid _inventoryId;
    private ItemList? _creatures;
    private ScrollContainer? _scroll;
    private CheckBox? _required;
    private LineEdit? _asset;
    private OptionButton? _lifeState;
    private LineEdit? _animationLoop;
    private LineEdit? _pose;
    private CheckBox? _invisible;
    private LineEdit? _ruleset;
    private TextEdit? _baseValues;
    private TextEdit? _modifiers;
    private TextEdit? _derived;
    private SpinBox? _avoidance;
    private SpinBox? _armor;
    private SpinBox? _mitigation;
    private TextEdit? _resistances;
    private TextEdit? _saves;
    private LineEdit? _immunities;
    private TextEdit? _abilities;
    private LineEdit? _owner;
    private CheckBox? _persistent;
    private TextEdit? _containers;
    private TextEdit? _items;
    private TextEdit? _equipment;
    private TextEdit? _overrides;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;
    private CreatureRulesCompositionPreview? _preview;

    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public int SummarySectionCount { get; private set; }
    public double LastMitigatedDamage { get; private set; }

    public void Initialize(Func<ProjectState?> load,
        Func<BlueprintId, CreatureFoundationProfile, bool, CommandResult> mutate,
        Action undo, Action redo, Action<Window> applyIcon)
    {
        _load = load; _mutate = mutate; _undo = undo; _redo = redo; _applyIcon = applyIcon;
    }

    public override void _Ready()
    {
        Title = "Règles & inventaire de créature · OpenRPG Forge";
        MinSize = new(1180, 760); Size = new(1500, 930); CloseRequested += Hide;
        Build(); Refresh();
    }

    public void Open()
    {
        Refresh(); PopupCentered(); _applyIcon?.Invoke(this);
    }

    public void Refresh(BlueprintId? preferred = null)
    {
        var state = _load?.Invoke();
        _creatures?.Clear();
        var candidates = state?.Project.Blueprints
            .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal).ToArray() ?? [];
        foreach (var blueprint in candidates)
        {
            var profile = blueprint.CreatureFoundation?.RulesComposition;
            var index = _creatures!.AddItem($"{blueprint.DisplayName} — {blueprint.Slug} · " +
                (profile is null ? "règles vides" : $"{profile.InitialState.LifeState} · {profile.Abilities.Count} capacité(s)"));
            _creatures.SetItemMetadata(index, blueprint.Id.ToString());
        }
        if (candidates.Length == 0)
        {
            _feedback!.Text = "[color=#ffbd7a]État vide : créez d’abord un blueprint de créature.[/color]";
            _preview?.ShowProfile(null);
            return;
        }
        var selected = preferred ?? _selectedId ?? candidates[0].Id;
        var selectedIndex = Array.FindIndex(candidates, item => item.Id == selected);
        if (selectedIndex < 0) selectedIndex = 0;
        _creatures!.Select(selectedIndex);
        LoadBlueprint(candidates[selectedIndex]);
    }

    public bool ConfigureForSmokeTest()
    {
        var state = _load?.Invoke();
        var target = state?.Project.Blueprints.FirstOrDefault(item => item.Slug == "ash-warden-boss");
        var item = state?.Project.Blueprints.FirstOrDefault(item => item.Slug == "ash-key");
        Refresh(target?.Id);
        if (_selectedId is null || item is null) return false;
        _required!.ButtonPressed = true;
        _asset!.Text = "asset.creature-rules.ash-warden";
        _lifeState!.Select((int)CreatureRulesInitialLifeState.Seated);
        _animationLoop!.Text = "clip.warden.seated";
        _pose!.Text = "pose.warden-throne";
        _invisible!.ButtonPressed = false;
        _ruleset!.Text = "ruleset.crypt";
        _baseValues!.Text = "agility=6\nstrength=8";
        _modifiers!.Text = "a1780000-0000-4000-8000-000000000003|agility|species.emberborn|1";
        _derived!.Text = "a1780000-0000-4000-8000-000000000004|evasion|sum|agility,strength|1";
        _avoidance!.Value = 4; _armor!.Value = 2; _mitigation!.Value = 1;
        _resistances!.Text = "fire=0.5\nglass=0.2";
        _saves!.Text = "fortitude=6\nreflex=4\nwill=5";
        _immunities!.Text = "radiant";
        _abilities!.Text = "a1780000-0000-4000-8000-000000000006|ability.ash-nova|3|2|12|state.alive|boss,guardian|script.ash-nova";
        _owner!.Text = "creature.ash-warden"; _persistent!.ButtonPressed = true;
        _containers!.Text = "a1780000-0000-4000-8000-000000000008|container.backpack|8|100|100";
        _items!.Text = $"container.backpack|a1780000-0000-4000-8000-000000000009|{item.Id}|1|0.2|0.1";
        _equipment!.Text = "main-hand=a1780000-0000-4000-8000-000000000009";
        _overrides!.Text = "rules.preview=enabled";
        RebuildSummary();
        return SummarySectionCount == 5 && _summary!.Text.Contains("PRÊT", StringComparison.Ordinal) &&
               _preview is not null;
    }

    public bool PrepareForCapture()
    {
        var prepared = ConfigureForSmokeTest();
        if (_scroll is not null) _scroll.ScrollVertical = 0;
        return prepared;
    }

    public bool PreviewForSmokeTest()
    {
        Mutate(true);
        return LastCommandId != Guid.Empty && LastPatchSummary.Contains("creature foundation", StringComparison.OrdinalIgnoreCase);
    }

    public bool ApplyForSmokeTest()
    {
        Mutate(false);
        return LastCommandId != Guid.Empty && LastDiagnostics.Length == 0;
    }

    public bool SimulateForSmokeTest()
    {
        LastMitigatedDamage = _preview?.SimulateDamage() ?? -1;
        return Math.Abs(LastMitigatedDamage - 5) < .001;
    }

    private void Build()
    {
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 16);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 9); margin.AddChild(root);
        var title = new Label { Text = "RÈGLES & INVENTAIRE DE CRÉATURE" };
        title.AddThemeFontSizeOverride("font_size", 24); title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label { Text = "État, attributs, défenses, capacités et inventaire dans le blueprint canonique — calculs inspectables avant build." });

        var outer = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [260]
        };
        root.AddChild(outer);
        var catalog = Panel("CRÉATURES"); outer.AddChild(catalog);
        _creatures = new ItemList { CustomMinimumSize = new(250, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _creatures.ItemSelected += index => SelectCreature((int)index);
        catalog.GetChild<VBoxContainer>(0).AddChild(_creatures);

        var editor = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [690]
        };
        outer.AddChild(editor);
        _scroll = new ScrollContainer
        {
            CustomMinimumSize = new(660, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        editor.AddChild(_scroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 7); _scroll.AddChild(form);
        _required = new CheckBox { Text = "Règles de créature requises par le runtime" }; form.AddChild(_required);

        Section(form, "ÉTAT INITIAL & POSE");
        var stateGrid = new GridContainer { Columns = 2 }; form.AddChild(stateGrid);
        _asset = Field(stateGrid, "AssetId", "asset.creature-rules.guardian");
        stateGrid.AddChild(new Label { Text = "État de vie" });
        _lifeState = new OptionButton(); foreach (var value in Enum.GetValues<CreatureRulesInitialLifeState>())
            _lifeState.AddItem(value.ToString()); stateGrid.AddChild(_lifeState);
        _animationLoop = Field(stateGrid, "Boucle d’animation", "clip.guardian.idle");
        _pose = Field(stateGrid, "Pose", "pose.guardian.ready");
        _invisible = new CheckBox { Text = "Invisible au chargement" }; form.AddChild(_invisible);

        Section(form, "ATTRIBUTS · RULESET & FORMULES");
        _ruleset = Field(form, "Ruleset", "ruleset.core");
        _baseValues = Multi(form, "Bases — attribut=valeur", 65);
        _modifiers = Multi(form, "Modifiers — uuid|attribut|source|valeur", 65);
        _derived = Multi(form, "Dérivés — uuid|attribut|sum/product/ratio|dépendances|constante", 75);

        Section(form, "DÉFENSES & SAUVEGARDES");
        var defenseGrid = new GridContainer { Columns = 2 }; form.AddChild(defenseGrid);
        _avoidance = Spin(defenseGrid, "Évitement", 0, 1000, 0);
        _armor = Spin(defenseGrid, "Armure", 0, 1000, 0);
        _mitigation = Spin(defenseGrid, "Mitigation plate", 0, 1000, 0);
        _immunities = Field(defenseGrid, "Immunités", "radiant, poison");
        _resistances = Multi(form, "Résistances — type=ratio [0..1]", 65);
        _saves = Multi(form, "Sauvegardes — id=valeur", 65);

        Section(form, "CAPACITÉS SPÉCIALES");
        _abilities = Multi(form, "uuid|ability|niveau|charges|cooldown|conditions|tags IA|script", 100);

        Section(form, "INVENTAIRE & ÉQUIPEMENT");
        var inventoryGrid = new GridContainer { Columns = 2 }; form.AddChild(inventoryGrid);
        _owner = Field(inventoryGrid, "Owner", "creature.guardian");
        _persistent = new CheckBox { Text = "Inventaire persistant" }; inventoryGrid.AddChild(_persistent);
        inventoryGrid.AddChild(new Label());
        _containers = Multi(form, "Conteneurs — uuid|id|slots|poids max|volume max", 65);
        _items = Multi(form, "Objets — conteneur|uuid|BlueprintId|quantité|poids|volume", 75);
        _equipment = Multi(form, "Équipement — slot=uuid objet", 65);
        _overrides = Multi(form, "Overrides explicites — clé=valeur", 55);

        var actions = new HBoxContainer(); form.AddChild(actions);
        Button(actions, "Prévisualiser", () => Mutate(true));
        Button(actions, "Appliquer", () => Mutate(false), true);
        Button(actions, "Simuler 10 dégâts", () =>
        {
            LastMitigatedDamage = _preview?.SimulateDamage() ?? -1;
            if (_feedback is not null) _feedback.Text = $"[color=#8ce0bd]Simulation : 10 glass → {LastMitigatedDamage:0.##} dégâts[/color]";
        });
        Button(actions, "Annuler", () => { _undo?.Invoke(); Refresh(_selectedId); });
        Button(actions, "Rétablir", () => { _redo?.Invoke(); Refresh(_selectedId); });

        var right = Panel("APERÇU & VALIDATION"); editor.AddChild(right);
        var rightBox = right.GetChild<VBoxContainer>(0);
        _preview = new CreatureRulesCompositionPreview(); rightBox.AddChild(_preview);
        _summary = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(350, 180), FitContent = false };
        rightBox.AddChild(_summary);
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(350, 115), FitContent = false };
        rightBox.AddChild(_feedback);

        foreach (var control in new Control[] { _required, _asset, _lifeState, _animationLoop, _pose, _invisible,
                     _ruleset, _baseValues, _modifiers, _derived, _avoidance, _armor, _mitigation, _resistances,
                     _saves, _immunities, _abilities, _owner, _persistent, _containers, _items, _equipment, _overrides })
            if (control is LineEdit line) line.TextChanged += _ => RebuildSummary();
            else if (control is TextEdit text) text.TextChanged += RebuildSummary;
            else if (control is CheckBox check) check.Toggled += _ => RebuildSummary();
            else if (control is OptionButton option) option.ItemSelected += _ => RebuildSummary();
            else if (control is SpinBox spin) spin.ValueChanged += _ => RebuildSummary();
    }

    private void SelectCreature(int index)
    {
        var state = _load?.Invoke();
        if (state is null || _creatures is null) return;
        var id = BlueprintId.Parse(_creatures.GetItemMetadata(index).AsString());
        LoadBlueprint(state.Project.Blueprints.Single(item => item.Id == id));
    }

    private void LoadBlueprint(BlueprintDefinition blueprint)
    {
        _selectedId = blueprint.Id;
        var profile = blueprint.CreatureFoundation?.RulesComposition ??
                      CreatureRulesCompositionProfile.CreateDefault(Guid.NewGuid(), "toolset:r078");
        _profileId = profile.Id; _initialId = profile.InitialState.Id; _attributesId = profile.Attributes.Id;
        _defenseId = profile.Defenses.Id; _inventoryId = profile.Inventory.Id;
        _required!.ButtonPressed = blueprint.CreatureFoundation?.RulesCompositionRequiredAtRuntime ?? false;
        _asset!.Text = profile.AssetId; _lifeState!.Select((int)profile.InitialState.LifeState);
        _animationLoop!.Text = profile.InitialState.AnimationLoopAssetId ?? string.Empty;
        _pose!.Text = profile.InitialState.PoseId ?? string.Empty; _invisible!.ButtonPressed = profile.InitialState.Invisible;
        _ruleset!.Text = profile.Attributes.RulesetId;
        _baseValues!.Text = string.Join("\n", profile.Attributes.BaseValues.Select(item => $"{item.Key}={Format(item.Value)}"));
        _modifiers!.Text = string.Join("\n", profile.Attributes.Modifiers.Select(item =>
            $"{item.Id:D}|{item.AttributeId}|{item.SourceId}|{Format(item.Value)}"));
        _derived!.Text = string.Join("\n", profile.Attributes.Derived.Select(item =>
            $"{item.Id:D}|{item.AttributeId}|{item.Operation.ToString().ToLowerInvariant()}|{string.Join(',', item.Dependencies)}|{Format(item.Constant)}"));
        _avoidance!.Value = profile.Defenses.Avoidance; _armor!.Value = profile.Defenses.Armor;
        _mitigation!.Value = profile.Defenses.FlatMitigation;
        _resistances!.Text = string.Join("\n", profile.Defenses.Resistances.Select(item => $"{item.Key}={Format(item.Value)}"));
        _saves!.Text = string.Join("\n", profile.Defenses.Saves.Select(item => $"{item.Key}={Format(item.Value)}"));
        _immunities!.Text = string.Join(", ", profile.Defenses.Immunities);
        _abilities!.Text = string.Join("\n", profile.Abilities.Select(item =>
            $"{item.Id:D}|{item.AbilityId}|{item.Level}|{item.Charges}|{Format(item.CooldownSeconds)}|" +
            $"{string.Join(',', item.Conditions)}|{string.Join(',', item.AiTags)}|{item.ScriptId}"));
        _owner!.Text = profile.Inventory.OwnerId; _persistent!.ButtonPressed = profile.Inventory.Persistent;
        _containers!.Text = string.Join("\n", profile.Inventory.Containers.Select(item =>
            $"{item.Id:D}|{item.ContainerId}|{item.SlotCount}|{Format(item.MaximumWeight)}|{Format(item.MaximumVolume)}"));
        _items!.Text = string.Join("\n", profile.Inventory.Containers.SelectMany(container => container.Items.Select(item =>
            $"{container.ContainerId}|{item.Id:D}|{item.ItemBlueprintId}|{item.Quantity}|{Format(item.UnitWeight)}|{Format(item.UnitVolume)}")));
        _equipment!.Text = string.Join("\n", profile.Inventory.EquipmentSlots.Select(item => $"{item.Key}={item.Value:D}"));
        _overrides!.Text = string.Join("\n", profile.Overrides.Select(item => $"{item.Key}={item.Value}"));
        _feedback!.Text = blueprint.CreatureFoundation?.RulesComposition is null
            ? "[color=#ffbd7a]État vide : le formulaire propose un profil initial non enregistré.[/color]"
            : "[color=#8ce0bd]Profil canonique chargé.[/color]";
        RebuildSummary();
    }

    private CreatureRulesCompositionProfile BuildProfile()
    {
        var containers = ParseContainers(_containers!.Text, _items!.Text);
        return new(_profileId, 1, "toolset:r078", _asset!.Text, TransformValue.Identity,
            new(_initialId, (CreatureRulesInitialLifeState)_lifeState!.Selected,
                EmptyToNull(_animationLoop!.Text), EmptyToNull(_pose!.Text), _invisible!.ButtonPressed),
            new(_attributesId, _ruleset!.Text, ParseDoubleMap(_baseValues!.Text), ParseModifiers(_modifiers!.Text),
                ParseDerived(_derived!.Text)),
            new(_defenseId, _avoidance!.Value, _armor!.Value, ParseDoubleMap(_resistances!.Text),
                ParseDoubleMap(_saves!.Text), ParseIds(_immunities!.Text), _mitigation!.Value),
            ParseAbilities(_abilities!.Text),
            new(_inventoryId, containers, ParseGuidMap(_equipment!.Text), _owner!.Text,
                _persistent!.ButtonPressed), ParseStringMap(_overrides!.Text));
    }

    private CreatureFoundationProfile BuildFoundation(CreatureRulesCompositionProfile profile)
    {
        var state = _load?.Invoke() ?? throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var blueprint = state.Project.Blueprints.Single(item => item.Id == _selectedId);
        var foundation = blueprint.CreatureFoundation ?? CreatureFoundationProfile.CreateDefault(
            Guid.NewGuid(), blueprint.Slug, blueprint.DisplayName, "toolset:r078");
        return new(foundation.Id, foundation.SchemaVersion, foundation.Provenance, foundation.RequiredAtRuntime,
            foundation.Identity, foundation.DefaultTransform, foundation.Resources, foundation.Movement,
            foundation.References, foundation.Spawned, foundation.Overrides, foundation.AuthoringComposition,
            foundation.AuthoringCompositionRequiredAtRuntime, profile, _required!.ButtonPressed,
            foundation.InteractionComposition, foundation.InteractionCompositionRequiredAtRuntime,
            foundation.EventAiComposition, foundation.EventAiCompositionRequiredAtRuntime);
    }

    private void RebuildSummary()
    {
        if (_summary is null || _preview is null) return;
        try
        {
            var profile = BuildProfile();
            var diagnostics = CreatureRulesCompositionValidator.Validate(profile);
            var values = diagnostics.Count == 0
                ? CreatureRulesCompositionValidator.EvaluateAttributes(profile.Attributes)
                : new Dictionary<string, double>();
            SummarySectionCount = 5;
            _summary.Text = $"[color=#ffad72]ÉTAT & POSE[/color]\n{profile.InitialState.LifeState} · {profile.InitialState.PoseId ?? "sans pose"} · boucle {profile.InitialState.AnimationLoopAssetId ?? "—"}\n\n" +
                $"[color=#ffad72]ATTRIBUTS[/color]\n{profile.Attributes.RulesetId} · {values.Count} valeur(s) calculée(s) · {profile.Attributes.Modifiers.Count} modifier(s)\n\n" +
                $"[color=#ffad72]DÉFENSES[/color]\nÉvitement {profile.Defenses.Avoidance:0.#} · Armure {profile.Defenses.Armor:0.#} · {profile.Defenses.Saves.Count} save(s)\n\n" +
                $"[color=#ffad72]CAPACITÉS[/color]\n{profile.Abilities.Count} instance(s) · charges, cooldowns, conditions, IA et scripts\n\n" +
                $"[color=#ffad72]INVENTAIRE[/color]\n{profile.Inventory.Containers.Count} conteneur(s) · {profile.Inventory.Containers.Sum(item => item.Items.Sum(entry => entry.Quantity))} objet(s) · {profile.Inventory.EquipmentSlots.Count} équipé(s)\n\n" +
                (diagnostics.Count == 0 ? "[color=#8ce0bd]PRÊT · validation pré-build verte[/color]" :
                    $"[color=#ff7777]{diagnostics.Count} diagnostic(s) avant build[/color]");
            _preview.ShowProfile(diagnostics.Count == 0 ? profile : null);
        }
        catch (Exception exception)
        {
            SummarySectionCount = 0;
            _summary.Text = $"[color=#ff7777]Erreur de formulaire : {exception.Message}[/color]";
            _preview.ShowProfile(null);
        }
    }

    private void Mutate(bool dryRun)
    {
        if (_mutate is null || _selectedId is null || _feedback is null) return;
        LastCommandId = Guid.NewGuid(); LastDiagnostics = string.Empty;
        try
        {
            var result = _mutate(_selectedId, BuildFoundation(BuildProfile()), dryRun);
            LastPatchSummary = result.Patch?.Summary ?? string.Empty;
            LastDiagnostics = string.Join("\n", result.Diagnostics.Select(item => $"{item.Code} · {item.Message}"));
            _feedback.Text = result.Succeeded
                ? dryRun ? $"[color=#ffbd7a]APERÇU — aucune donnée modifiée[/color]\n{result.Patch?.Summary}\nInverse : {result.Inverse?.Summary}"
                    : $"[color=#8ce0bd]APPLIQUÉ · révision {result.NewRevision}[/color]\n{result.Patch?.Summary}"
                : $"[color=#ff7777]Transaction refusée[/color]\n{LastDiagnostics}";
        }
        catch (Exception exception)
        {
            LastDiagnostics = exception.Message;
            _feedback.Text = $"[color=#ff7777]Erreur de formulaire[/color]\n{exception.Message}";
        }
    }

    private static IReadOnlyList<CreatureRulesAttributeModifier> ParseModifiers(string text) => Lines(text).Select(line =>
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || !Guid.TryParse(parts[0], out var id) || !TryDouble(parts[3], out var value))
            throw new FormatException("Modifier attendu : uuid|attribut|source|valeur.");
        return new CreatureRulesAttributeModifier(id, parts[1], parts[2], value);
    }).ToArray();

    private static IReadOnlyList<CreatureRulesDerivedAttribute> ParseDerived(string text) => Lines(text).Select(line =>
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 5 || !Guid.TryParse(parts[0], out var id) ||
            !Enum.TryParse<CreatureRulesAttributeFormulaOperation>(parts[2], true, out var operation) ||
            !TryDouble(parts[4], out var constant))
            throw new FormatException("Formule attendue : uuid|attribut|sum/product/ratio|dépendances|constante.");
        return new CreatureRulesDerivedAttribute(id, parts[1], operation, ParseIds(parts[3]), constant);
    }).ToArray();

    private static IReadOnlyList<CreatureRulesAbilityInstance> ParseAbilities(string text) => Lines(text).Select(line =>
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 8 || !Guid.TryParse(parts[0], out var id) || !int.TryParse(parts[2], out var level) ||
            !int.TryParse(parts[3], out var charges) || !TryDouble(parts[4], out var cooldown))
            throw new FormatException("Capacité attendue : uuid|ability|niveau|charges|cooldown|conditions|tags|script.");
        return new CreatureRulesAbilityInstance(id, parts[1], level, charges, cooldown, ParseIds(parts[5]),
            ParseIds(parts[6]), parts[7]);
    }).ToArray();

    private static IReadOnlyList<CreatureRulesInventoryContainer> ParseContainers(string containerText, string itemText)
    {
        var items = Lines(itemText).Select(line =>
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 6 || !Guid.TryParse(parts[1], out var id) ||
                !int.TryParse(parts[3], out var quantity) || !TryDouble(parts[4], out var weight) ||
                !TryDouble(parts[5], out var volume))
                throw new FormatException("Objet attendu : conteneur|uuid|BlueprintId|quantité|poids|volume.");
            return (ContainerId: parts[0], Entry: new CreatureRulesInventoryEntry(id,
                BlueprintId.Parse(parts[2]), quantity, weight, volume));
        }).ToArray();
        return Lines(containerText).Select(line =>
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 5 || !Guid.TryParse(parts[0], out var id) ||
                !int.TryParse(parts[2], out var slots) || !TryDouble(parts[3], out var weight) ||
                !TryDouble(parts[4], out var volume))
                throw new FormatException("Conteneur attendu : uuid|id|slots|poids max|volume max.");
            return new CreatureRulesInventoryContainer(id, parts[1], slots, weight, volume,
                items.Where(item => string.Equals(item.ContainerId, parts[1], StringComparison.Ordinal))
                    .Select(item => item.Entry).ToArray());
        }).ToArray();
    }

    private static IReadOnlyDictionary<string, double> ParseDoubleMap(string text) => Lines(text).Select(line =>
    {
        var pair = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (pair.Length != 2 || !TryDouble(pair[1], out var value))
            throw new FormatException("Entrée numérique attendue : clé=valeur.");
        return (pair[0], value);
    }).ToDictionary(item => item.Item1, item => item.value, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> ParseStringMap(string text) => Lines(text).Select(line =>
    {
        var pair = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (pair.Length != 2) throw new FormatException("Entrée attendue : clé=valeur.");
        return (pair[0], pair[1]);
    }).ToDictionary(item => item.Item1, item => item.Item2, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, Guid> ParseGuidMap(string text) => Lines(text).Select(line =>
    {
        var pair = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (pair.Length != 2 || !Guid.TryParse(pair[1], out var value))
            throw new FormatException("Équipement attendu : slot=uuid objet.");
        return (pair[0], value);
    }).ToDictionary(item => item.Item1, item => item.value, StringComparer.Ordinal);

    private static IReadOnlyList<string> ParseIds(string text) => text.Split([',', '\n'],
        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static IReadOnlyList<string> Lines(string text) => text.Split('\n',
        StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static bool TryDouble(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static void Section(Container owner, string text)
    {
        var label = new Label { Text = text }; label.AddThemeColorOverride("font_color", new Color("ffad70"));
        label.AddThemeFontSizeOverride("font_size", 15); owner.AddChild(label);
    }

    private static LineEdit Field(Container owner, string label, string placeholder)
    {
        owner.AddChild(new Label { Text = label });
        var field = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        owner.AddChild(field); return field;
    }

    private static TextEdit Multi(Container owner, string label, float height)
    {
        owner.AddChild(new Label { Text = label });
        var field = new TextEdit { CustomMinimumSize = new(0, height), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        owner.AddChild(field); return field;
    }

    private static SpinBox Spin(Container owner, string label, double minimum, double maximum, double value)
    {
        owner.AddChild(new Label { Text = label });
        var spin = new SpinBox { MinValue = minimum, MaxValue = maximum, Step = .1, Value = value };
        owner.AddChild(spin); return spin;
    }

    private static void Button(Container owner, string text, Action action, bool primary = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new(115, 36) };
        if (primary) button.AddThemeColorOverride("font_color", new Color("fff4ec"));
        button.Pressed += action; owner.AddChild(button);
    }

    private static PanelContainer Panel(string title)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 8); panel.AddChild(box);
        var label = new Label { Text = title }; label.AddThemeColorOverride("font_color", new Color("ffad70"));
        box.AddChild(label); return panel;
    }
}
#endif
