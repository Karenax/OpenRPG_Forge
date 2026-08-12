#if TOOLS
using System.Globalization;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class CreatureAuthoringCompositionWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<BlueprintId, CreatureFoundationProfile, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private BlueprintId? _selectedId;
    private Guid _profileId;
    private Guid _graphId;
    private ItemList? _creatures;
    private ScrollContainer? _scroll;
    private CheckBox? _required;
    private LineEdit? _archetype;
    private LineEdit? _ruleset;
    private LineEdit? _rig;
    private LineEdit? _faction;
    private OptionButton? _role;
    private SpinBox? _threat;
    private LineEdit? _visualKit;
    private LineEdit? _combatTags;
    private LineEdit? _species;
    private LineEdit? _parents;
    private TextEdit? _traits;
    private LineEdit? _variants;
    private LineEdit? _skeleton;
    private LineEdit? _asset;
    private LineEdit? _visualArchetype;
    private LineEdit? _mesh;
    private TextEdit? _morphs;
    private TextEdit? _bodySlots;
    private LineEdit? _materials;
    private LineEdit? _palettes;
    private LineEdit? _attachments;
    private LineEdit? _preset;
    private LineEdit? _retarget;
    private TextEdit? _states;
    private TextEdit? _transitions;
    private TextEdit? _overrides;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;
    private CreatureAuthoringCompositionPreview? _preview;

    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public int SummarySectionCount { get; private set; }
    public string LastSimulatedState { get; private set; } = string.Empty;

    public void Initialize(Func<ProjectState?> load,
        Func<BlueprintId, CreatureFoundationProfile, bool, CommandResult> mutate,
        Action undo, Action redo, Action<Window> applyIcon)
    {
        _load = load; _mutate = mutate; _undo = undo; _redo = redo; _applyIcon = applyIcon;
    }

    public override void _Ready()
    {
        Title = "Assistant & composition de créature · OpenRPG Forge";
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
            var profile = blueprint.CreatureFoundation?.AuthoringComposition;
            var index = _creatures!.AddItem($"{blueprint.DisplayName}\n{blueprint.Slug} · " +
                (profile is null ? "assistant vide" : $"{profile.EncounterRole} · menace {profile.ThreatRating:0.#}"));
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
        var target = _load?.Invoke()?.Project.Blueprints.FirstOrDefault(item => item.Slug == "ash-warden-boss");
        Refresh(target?.Id);
        if (_selectedId is null || _states is null) return false;
        _required!.ButtonPressed = true;
        _archetype!.Text = "archetype.ash-warden";
        _ruleset!.Text = "ruleset.crypt";
        _rig!.Text = "rig.humanoid";
        _faction!.Text = "faction.ashes";
        _role!.Select((int)CreatureAuthoringEncounterRole.Boss);
        _threat!.Value = 12;
        _visualKit!.Text = "visual.ash-warden";
        _combatTags!.Text = "boss, guardian, melee";
        _species!.Text = "species.emberborn";
        _parents!.Text = "species.construct";
        _traits!.Text = "ember.core=fire-resistant,humanoid\nwarden.oath=guardian,unyielding";
        _variants!.Text = "ancient, awakened";
        _skeleton!.Text = "rig.humanoid";
        _asset!.Text = "asset.creature.ash-warden";
        _visualArchetype!.Text = "visual.ash-warden";
        _mesh!.Text = "mesh.ash-warden";
        _morphs!.Text = "height=0.18\nshoulders=0.25";
        _bodySlots!.Text = "head=asset.warden.helm\nmain-hand=asset.warden.blade";
        _materials!.Text = "material.ash-steel, material.ember-cloth";
        _palettes!.Text = "palette.ember";
        _attachments!.Text = "attachment.ember-aura";
        _preset!.Text = "preset.ash-warden";
        _retarget!.Text = "retarget.humanoid";
        _states.Text =
            "a1770000-0000-4000-8000-000000000001|idle|locomotion|clip.warden.idle\n" +
            "a1770000-0000-4000-8000-000000000002|walk|locomotion|clip.warden.walk\n" +
            "a1770000-0000-4000-8000-000000000003|guard|combat|clip.warden.guard\n" +
            "a1770000-0000-4000-8000-000000000004|roar|emote|clip.warden.roar\n" +
            "a1770000-0000-4000-8000-000000000005|embers|additive|clip.warden.embers";
        _transitions!.Text =
            "a1770000-0000-4000-8000-000000000011|idle|walk|movement.requested\n" +
            "a1770000-0000-4000-8000-000000000012|walk|guard|combat.started";
        _overrides!.Text = "preview.scale=1.15\nanimation.speed=1.0";
        RebuildSummary();
        return SummarySectionCount == 4 && _summary!.Text.Contains("PRÊT", StringComparison.Ordinal) &&
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
        LastSimulatedState = _preview?.SimulateNextState() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(LastSimulatedState);
    }

    private void Build()
    {
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 16);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 9); margin.AddChild(root);
        var title = new Label { Text = "ASSISTANT & COMPOSITION DE CRÉATURE" };
        title.AddThemeFontSizeOverride("font_size", 24); title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label { Text = "Rôle, espèce, modèle et AnimationGraph dans le blueprint canonique — aperçu et validation avant build." });

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
            SplitOffsets = [680]
        };
        outer.AddChild(editor);
        _scroll = new ScrollContainer
        {
            CustomMinimumSize = new(650, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        editor.AddChild(_scroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 7); _scroll.AddChild(form);
        _required = new CheckBox { Text = "Assistant et composition requis par le runtime" }; form.AddChild(_required);
        Section(form, "ASSISTANT · RÔLE & DÉFI");
        var wizardGrid = new GridContainer { Columns = 2 }; form.AddChild(wizardGrid);
        _archetype = Field(wizardGrid, "CreatureArchetype", "archetype.guardian");
        _ruleset = Field(wizardGrid, "Ruleset", "ruleset.core");
        _rig = Field(wizardGrid, "Rig", "rig.humanoid");
        _faction = Field(wizardGrid, "Faction", "faction.neutral");
        wizardGrid.AddChild(new Label { Text = "EncounterRole" });
        _role = new OptionButton(); foreach (var value in Enum.GetValues<CreatureAuthoringEncounterRole>())
            _role.AddItem(value.ToString()); wizardGrid.AddChild(_role);
        _threat = Spin(wizardGrid, "ThreatRating", 0, 1000, 3);
        _visualKit = Field(wizardGrid, "Kit visuel", "visual.guardian");
        _combatTags = Field(wizardGrid, "Tags de combat", "guardian, melee");

        Section(form, "ESPÈCE · TRAITS & VARIANTES");
        _species = Field(form, "SpeciesDefinition", "species.human");
        _parents = Field(form, "Parents contrôlés", "species.humanoid");
        _traits = Multi(form, "TraitSet — id=trait,trait", 70);
        _variants = Field(form, "Variantes", "standard");
        _skeleton = Field(form, "Squelette requis", "rig.humanoid");

        Section(form, "APPARENCE & MODÈLE");
        var visualGrid = new GridContainer { Columns = 2 }; form.AddChild(visualGrid);
        _asset = Field(visualGrid, "AssetId", "asset.creature.guardian");
        _visualArchetype = Field(visualGrid, "VisualArchetype", "visual.guardian");
        _mesh = Field(visualGrid, "Mesh", "mesh.guardian");
        _preset = Field(visualGrid, "Preset", "preset.guardian");
        _morphs = Multi(form, "Morphs — paramètre=valeur [-1..1]", 65);
        _bodySlots = Multi(form, "Références de slots — slot=asset", 65);
        _materials = Field(form, "Matériaux", "material.guardian");
        _palettes = Field(form, "Palettes", "palette.ember");
        _attachments = Field(form, "Attachments génériques", "attachment.aura");

        Section(form, "ANIMATIONGRAPH · MINI-CARTE & SIMULATION");
        _retarget = Field(form, "Profil de retargeting", "retarget.humanoid");
        _states = Multi(form, "uuid|state|locomotion/combat/emote/additive|clip", 130);
        _transitions = Multi(form, "uuid|from|to|condition", 90);
        _overrides = Multi(form, "Overrides explicites — clé=valeur", 65);
        var actions = new HBoxContainer(); form.AddChild(actions);
        Button(actions, "Prévisualiser", () => Mutate(true));
        Button(actions, "Appliquer", () => Mutate(false), true);
        Button(actions, "Simuler l’état suivant", () =>
        {
            LastSimulatedState = _preview?.SimulateNextState() ?? string.Empty;
            if (_feedback is not null) _feedback.Text = $"[color=#8ce0bd]Simulation : {LastSimulatedState}[/color]";
        });
        Button(actions, "Annuler", () => { _undo?.Invoke(); Refresh(_selectedId); });
        Button(actions, "Rétablir", () => { _redo?.Invoke(); Refresh(_selectedId); });

        var right = Panel("APERÇU & VALIDATION"); editor.AddChild(right);
        var rightBox = right.GetChild<VBoxContainer>(0);
        _preview = new CreatureAuthoringCompositionPreview(); rightBox.AddChild(_preview);
        _summary = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(350, 180), FitContent = false };
        rightBox.AddChild(_summary);
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(350, 115), FitContent = false };
        rightBox.AddChild(_feedback);

        foreach (var control in new Control[] { _required, _archetype, _ruleset, _rig, _faction, _role, _threat,
                     _visualKit, _combatTags, _species, _parents, _traits, _variants, _skeleton, _asset,
                     _visualArchetype, _mesh, _morphs, _bodySlots, _materials, _palettes, _attachments, _preset,
                     _retarget, _states, _transitions, _overrides })
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
        var profile = blueprint.CreatureFoundation?.AuthoringComposition ??
                      CreatureAuthoringCompositionProfile.CreateDefault(Guid.NewGuid(), "toolset:r077");
        _profileId = profile.Id; _graphId = profile.AnimationGraph.Id;
        _required!.ButtonPressed = blueprint.CreatureFoundation?.AuthoringCompositionRequiredAtRuntime ?? false;
        _archetype!.Text = profile.ArchetypeId; _ruleset!.Text = profile.RulesetId; _rig!.Text = profile.RigId;
        _faction!.Text = profile.FactionId; _role!.Select((int)profile.EncounterRole); _threat!.Value = profile.ThreatRating;
        _visualKit!.Text = profile.VisualKitId; _combatTags!.Text = string.Join(", ", profile.CombatTags);
        _species!.Text = profile.Species.SpeciesId; _parents!.Text = string.Join(", ", profile.Species.ParentSpeciesIds);
        _traits!.Text = string.Join("\n", profile.Species.TraitSets.Select(item => $"{item.Id}={string.Join(',', item.Traits)}"));
        _variants!.Text = string.Join(", ", profile.Species.Variants); _skeleton!.Text = profile.Species.RequiredSkeletonId;
        _asset!.Text = profile.AssetId; _visualArchetype!.Text = profile.Appearance.VisualArchetypeId;
        _mesh!.Text = profile.Appearance.MeshAssetId; _preset!.Text = profile.Appearance.PresetId;
        _morphs!.Text = string.Join("\n", profile.Appearance.Morphs.Select(item => $"{item.Key}={item.Value.ToString(CultureInfo.InvariantCulture)}"));
        _bodySlots!.Text = string.Join("\n", profile.Appearance.BodySlots.Select(item => $"{item.Key}={item.Value}"));
        _materials!.Text = string.Join(", ", profile.Appearance.MaterialIds);
        _palettes!.Text = string.Join(", ", profile.Appearance.PaletteIds);
        _attachments!.Text = string.Join(", ", profile.Appearance.AttachmentIds);
        _retarget!.Text = profile.AnimationGraph.RetargetProfileId;
        _states!.Text = string.Join("\n", profile.AnimationGraph.States.Select(item =>
            $"{item.Id:D}|{item.StateId}|{item.Category}|{item.ClipAssetId}"));
        _transitions!.Text = string.Join("\n", profile.AnimationGraph.Transitions.Select(item =>
            $"{item.Id:D}|{item.FromStateId}|{item.ToStateId}|{item.ConditionId}"));
        _overrides!.Text = string.Join("\n", profile.Overrides.Select(item => $"{item.Key}={item.Value}"));
        _feedback!.Text = blueprint.CreatureFoundation?.AuthoringComposition is null
            ? "[color=#ffbd7a]État vide : le formulaire propose un profil initial non enregistré.[/color]"
            : "[color=#8ce0bd]Profil canonique chargé.[/color]";
        RebuildSummary();
    }

    private CreatureAuthoringCompositionProfile BuildProfile()
    {
        return new(_profileId, 1, "toolset:r077", _asset!.Text, Forge.Domain.Spatial.TransformValue.Identity,
            _archetype!.Text, _ruleset!.Text, _rig!.Text, _faction!.Text,
            (CreatureAuthoringEncounterRole)_role!.Selected, _threat!.Value, _visualKit!.Text,
            ParseIds(_combatTags!.Text),
            new(_species!.Text, ParseIds(_parents!.Text), ParseTraits(_traits!.Text), ParseIds(_variants!.Text),
                _skeleton!.Text),
            new(_visualArchetype!.Text, _mesh!.Text, _rig!.Text, ParseDoubleMap(_morphs!.Text),
                ParseStringMap(_bodySlots!.Text), ParseIds(_materials!.Text), ParseIds(_palettes!.Text),
                ParseIds(_attachments!.Text), _preset!.Text),
            new(_graphId, 1, "toolset:r077", ParseStates(_states!.Text), ParseTransitions(_transitions!.Text),
                _retarget!.Text),
            ParseStringMap(_overrides!.Text));
    }

    private CreatureFoundationProfile BuildFoundation(CreatureAuthoringCompositionProfile profile)
    {
        var state = _load?.Invoke() ?? throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var blueprint = state.Project.Blueprints.Single(item => item.Id == _selectedId);
        var foundation = blueprint.CreatureFoundation ?? CreatureFoundationProfile.CreateDefault(
            Guid.NewGuid(), blueprint.Slug, blueprint.DisplayName, "toolset:r077");
        return new(foundation.Id, foundation.SchemaVersion, foundation.Provenance, foundation.RequiredAtRuntime,
            foundation.Identity, foundation.DefaultTransform, foundation.Resources, foundation.Movement,
            foundation.References, foundation.Spawned, foundation.Overrides, profile, _required!.ButtonPressed,
            foundation.RulesComposition, foundation.RulesCompositionRequiredAtRuntime,
            foundation.InteractionComposition, foundation.InteractionCompositionRequiredAtRuntime,
            foundation.EventAiComposition, foundation.EventAiCompositionRequiredAtRuntime);
    }

    private void RebuildSummary()
    {
        if (_summary is null || _preview is null) return;
        try
        {
            var profile = BuildProfile();
            var diagnostics = CreatureAuthoringCompositionValidator.Validate(profile);
            SummarySectionCount = 4;
            _summary.Text = $"[color=#ffad72]ASSISTANT[/color]\n{profile.ArchetypeId} · {profile.RulesetId} · {profile.EncounterRole} · menace {profile.ThreatRating:0.#}\n\n" +
                $"[color=#ffad72]ESPÈCE[/color]\n{profile.Species.SpeciesId} · {profile.Species.TraitSets.Count} TraitSet · squelette {profile.Species.RequiredSkeletonId}\n\n" +
                $"[color=#ffad72]APPARENCE[/color]\n{profile.Appearance.MeshAssetId} · {profile.Appearance.MaterialIds.Count} matériau(x) · preset {profile.Appearance.PresetId}\n\n" +
                $"[color=#ffad72]ANIMATIONGRAPH[/color]\n{profile.AnimationGraph.States.Count} états · {profile.AnimationGraph.Transitions.Count} transitions · retarget {profile.AnimationGraph.RetargetProfileId}\n\n" +
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
            var profile = BuildProfile();
            var foundation = BuildFoundation(profile);
            var result = _mutate(_selectedId, foundation, dryRun);
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

    private static IReadOnlyList<string> ParseIds(string text) => text.Split([',', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static IReadOnlyList<CreatureSpeciesTraitSet> ParseTraits(string text) => Lines(text).Select(line =>
    {
        var pair = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (pair.Length != 2) throw new FormatException("TraitSet attendu : id=trait,trait.");
        return new CreatureSpeciesTraitSet(pair[0], ParseIds(pair[1]));
    }).ToArray();

    private static IReadOnlyDictionary<string, double> ParseDoubleMap(string text) => Lines(text).Select(line =>
    {
        var pair = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (pair.Length != 2 || !double.TryParse(pair[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            throw new FormatException("Morph attendu : paramètre=valeur.");
        return (pair[0], value);
    }).ToDictionary(item => item.Item1, item => item.value, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> ParseStringMap(string text) => Lines(text).Select(line =>
    {
        var pair = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (pair.Length != 2) throw new FormatException("Entrée attendue : clé=valeur.");
        return (pair[0], pair[1]);
    }).ToDictionary(item => item.Item1, item => item.Item2, StringComparer.Ordinal);

    private static IReadOnlyList<CreatureAnimationState> ParseStates(string text) => Lines(text).Select(line =>
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || !Guid.TryParse(parts[0], out var id))
            throw new FormatException("État attendu : uuid|state|catégorie|clip.");
        return new CreatureAnimationState(id, parts[1], parts[2], parts[3]);
    }).ToArray();

    private static IReadOnlyList<CreatureAnimationTransition> ParseTransitions(string text) => Lines(text).Select(line =>
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || !Guid.TryParse(parts[0], out var id))
            throw new FormatException("Transition attendue : uuid|from|to|condition.");
        return new CreatureAnimationTransition(id, parts[1], parts[2], parts[3]);
    }).ToArray();

    private static IReadOnlyList<string> Lines(string text) => text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

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
