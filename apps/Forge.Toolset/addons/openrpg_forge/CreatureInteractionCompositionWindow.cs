#if TOOLS
using System.Globalization;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Dialogues;
using Forge.Domain.Lifecycle;
using Forge.Domain.Spatial;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class CreatureInteractionCompositionWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<BlueprintId, CreatureFoundationProfile, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private BlueprintId? _selectedId;
    private Guid _profileId;
    private Guid _factionsId;
    private Guid _perceptionId;
    private Guid _protectionId;
    private Guid _eventId;
    private ItemList? _creatures;
    private ScrollContainer? _scroll;
    private CheckBox? _required;
    private LineEdit? _asset;
    private LineEdit? _primaryFaction;
    private TextEdit? _memberships;
    private TextEdit? _senses;
    private SpinBox? _memory;
    private TextEdit? _dialogueBindings;
    private CheckBox? _invulnerable;
    private CheckBox? _narrativeDeath;
    private CheckBox? _dialogueInterruptible;
    private LineEdit? _gmPermissions;
    private LineEdit? _onConversationHandler;
    private TextEdit? _overrides;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;
    private CreatureInteractionCompositionPreview? _preview;

    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public int SummarySectionCount { get; private set; }
    public string? LastSelectedDialogueId { get; private set; }

    public void Initialize(Func<ProjectState?> load,
        Func<BlueprintId, CreatureFoundationProfile, bool, CommandResult> mutate,
        Action undo, Action redo, Action<Window> applyIcon)
    {
        _load = load; _mutate = mutate; _undo = undo; _redo = redo; _applyIcon = applyIcon;
    }

    public override void _Ready()
    {
        Title = "Interactions de créature · OpenRPG Forge";
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
            var profile = blueprint.CreatureFoundation?.InteractionComposition;
            var index = _creatures!.AddItem($"{blueprint.DisplayName} — {blueprint.Slug} · " +
                (profile is null ? "interactions vides" :
                    $"{profile.Factions.Memberships.Count} faction(s) · {profile.Perception.Senses.Count} sens"));
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
        Refresh(target?.Id);
        if (_selectedId is null) return false;
        _required!.ButtonPressed = true;
        _asset!.Text = "asset.creature-interaction.ash-warden";
        _primaryFaction!.Text = "b2050000-0000-4000-8000-000000000002";
        _memberships!.Text =
            "a1790000-0000-4000-8000-000000000002|b2050000-0000-4000-8000-000000000002|1|rank.captain|35|true\n" +
            "a1790000-0000-4000-8000-000000000003|b2050000-0000-4000-8000-000000000001|0.25|rank.ally|5|true";
        _senses!.Text =
            "a1790000-0000-4000-8000-000000000005|vision|30|120|true|stimulus.visual\n" +
            "a1790000-0000-4000-8000-000000000006|hearing|20|360|true|stimulus.sound\n" +
            "a1790000-0000-4000-8000-000000000007|smell|8|360|false|stimulus.scent\n" +
            "a1790000-0000-4000-8000-000000000008|magic|15|360|false|stimulus.magic";
        _memory!.Value = 12;
        _dialogueBindings!.Text =
            "a1790000-0000-4000-8000-000000000009|greeting|20|40000000-0000-4000-8000-000000000001|player.hero|state.hostile|false\n" +
            "a1790000-0000-4000-8000-000000000010|greeting|1|40000000-0000-4000-8000-000000000001|||true\n" +
            "a1790000-0000-4000-8000-000000000011|interrupt|1|40000000-0000-4000-8000-000000000001|||true";
        _invulnerable!.ButtonPressed = true; _narrativeDeath!.ButtonPressed = true;
        _dialogueInterruptible!.ButtonPressed = false;
        _gmPermissions!.Text = "creature.dialogue.interrupt, creature.plot.override";
        _onConversationHandler!.Text = "OnConversation";
        _overrides!.Text = "interaction.preview=enabled";
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
        return LastCommandId != Guid.Empty && LastPatchSummary.Contains("creature foundation",
            StringComparison.OrdinalIgnoreCase);
    }

    public bool ApplyForSmokeTest()
    {
        Mutate(false);
        return LastCommandId != Guid.Empty && LastDiagnostics.Length == 0;
    }

    public bool SimulateForSmokeTest()
    {
        SimulateDialogue();
        return LastSelectedDialogueId == "40000000-0000-4000-8000-000000000001";
    }

    private void Build()
    {
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 16);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 9); margin.AddChild(root);
        var title = new Label { Text = "INTERACTIONS DE CRÉATURE" };
        title.AddThemeFontSizeOverride("font_size", 24); title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Factions, perception, dialogues, protections et OnConversation dans un seul document canonique."
        });

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
        _required = new CheckBox { Text = "Interactions requises par le runtime" }; form.AddChild(_required);
        _asset = Field(form, "AssetId", "asset.creature-interaction.guardian");

        Section(form, "FACTION DE CRÉATURE");
        _primaryFaction = Field(form, "Faction principale (UUID)", "00000000-0000-0000-0000-000000000000");
        _memberships = Multi(form, "Memberships — uuid|faction|poids|rang|réputation|hérité", 82);

        Section(form, "PERCEPTION · SENSE SYSTEM");
        _senses = Multi(form, "Sens — uuid|vision/hearing/smell/magic|portée|cône|occlusion|stimuli", 105);
        _memory = Spin(form, "Mémoire des stimuli (secondes)", 0, 86400, 10);

        Section(form, "CONVERSATIONS ASSOCIÉES");
        _dialogueBindings = Multi(form,
            "Bindings — uuid|contexte|priorité|DialogueId|interlocuteur|état|fallback", 105);

        Section(form, "PLOT, IMMORTALITÉ & INTERRUPTIBILITÉ");
        var protection = new HBoxContainer(); form.AddChild(protection);
        _invulnerable = new CheckBox { Text = "Invulnérable" }; protection.AddChild(_invulnerable);
        _narrativeDeath = new CheckBox { Text = "Mort narrative" }; protection.AddChild(_narrativeDeath);
        _dialogueInterruptible = new CheckBox { Text = "Dialogue interruptible" };
        protection.AddChild(_dialogueInterruptible);
        _gmPermissions = Field(form, "Permissions GM", "creature.plot.override");

        Section(form, "ONCONVERSATION");
        _onConversationHandler = Field(form, "Handler ForgeScript serveur", "OnConversation");
        _overrides = Multi(form, "Overrides explicites — clé=valeur", 55);

        var actions = new HBoxContainer(); form.AddChild(actions);
        Button(actions, "Prévisualiser", () => Mutate(true));
        Button(actions, "Appliquer", () => Mutate(false), true);
        Button(actions, "Simuler dialogue", SimulateDialogue);
        Button(actions, "Annuler", () => { _undo?.Invoke(); Refresh(_selectedId); });
        Button(actions, "Rétablir", () => { _redo?.Invoke(); Refresh(_selectedId); });

        var right = Panel("APERÇU & VALIDATION"); editor.AddChild(right);
        var rightBox = right.GetChild<VBoxContainer>(0);
        _preview = new CreatureInteractionCompositionPreview(); rightBox.AddChild(_preview);
        _summary = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(350, 190), FitContent = false };
        rightBox.AddChild(_summary);
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(350, 110), FitContent = false };
        rightBox.AddChild(_feedback);

        foreach (var control in new Control[] { _required, _asset, _primaryFaction, _memberships, _senses, _memory,
                     _dialogueBindings, _invulnerable, _narrativeDeath, _dialogueInterruptible, _gmPermissions,
                     _onConversationHandler, _overrides })
            if (control is LineEdit line) line.TextChanged += _ => RebuildSummary();
            else if (control is TextEdit text) text.TextChanged += RebuildSummary;
            else if (control is CheckBox check) check.Toggled += _ => RebuildSummary();
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
        var state = _load?.Invoke() ?? throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var profile = blueprint.CreatureFoundation?.InteractionComposition ?? CreateDefault(state);
        _profileId = profile.Id; _factionsId = profile.Factions.Id; _perceptionId = profile.Perception.Id;
        _protectionId = profile.Protection.Id; _eventId = profile.OnConversation.Id;
        _required!.ButtonPressed = blueprint.CreatureFoundation?.InteractionCompositionRequiredAtRuntime ?? false;
        _asset!.Text = profile.AssetId;
        _primaryFaction!.Text = profile.Factions.PrimaryFactionId.ToString("D");
        _memberships!.Text = string.Join('\n', profile.Factions.Memberships.Select(item =>
            $"{item.Id:D}|{item.FactionId:D}|{Format(item.Weight)}|{item.Rank}|{Format(item.PersonalReputation)}|{item.Inherited.ToString().ToLowerInvariant()}"));
        _senses!.Text = string.Join('\n', profile.Perception.Senses.Select(item =>
            $"{item.Id:D}|{item.Kind.ToString().ToLowerInvariant()}|{Format(item.Range)}|{Format(item.ConeDegrees)}|{item.UsesOcclusion.ToString().ToLowerInvariant()}|{string.Join(',', item.StimulusTags)}"));
        _memory!.Value = profile.Perception.MemorySeconds;
        _dialogueBindings!.Text = string.Join('\n', profile.DialogueBindings.Select(item =>
            $"{item.Id:D}|{item.Context}|{item.Priority}|{item.DialogueId}|{item.InterlocutorId}|{item.RequiredState}|{item.Fallback.ToString().ToLowerInvariant()}"));
        _invulnerable!.ButtonPressed = profile.Protection.Invulnerable;
        _narrativeDeath!.ButtonPressed = profile.Protection.UsesNarrativeDeath;
        _dialogueInterruptible!.ButtonPressed = profile.Protection.DialogueInterruptible;
        _gmPermissions!.Text = string.Join(", ", profile.Protection.GameMasterPermissions);
        _onConversationHandler!.Text = profile.OnConversation.Handler;
        _overrides!.Text = string.Join('\n', profile.Overrides.Select(item => $"{item.Key}={item.Value}"));
        _feedback!.Text = blueprint.CreatureFoundation?.InteractionComposition is null
            ? "[color=#ffbd7a]État vide : profil initial non enregistré ; vérifiez les références proposées.[/color]"
            : "[color=#8ce0bd]Profil canonique chargé.[/color]";
        RebuildSummary();
    }

    private CreatureInteractionCompositionProfile BuildProfile() => new(
        _profileId, 1, "toolset:r079", _asset!.Text, TransformValue.Identity,
        new(_factionsId, Guid.Parse(_primaryFaction!.Text), ParseMemberships(_memberships!.Text)),
        new(_perceptionId, ParseSenses(_senses!.Text), _memory!.Value),
        ParseDialogueBindings(_dialogueBindings!.Text),
        new(_protectionId, _invulnerable!.ButtonPressed, _narrativeDeath!.ButtonPressed,
            _dialogueInterruptible!.ButtonPressed, ParseIds(_gmPermissions!.Text)),
        new(_eventId, 1, "toolset:r079", "1.0", "creature.interaction_requested",
            _onConversationHandler!.Text, EventAuthorityScope.Server), ParseStringMap(_overrides!.Text));

    private CreatureFoundationProfile BuildFoundation(CreatureInteractionCompositionProfile profile)
    {
        var state = _load?.Invoke() ?? throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var blueprint = state.Project.Blueprints.Single(item => item.Id == _selectedId);
        var foundation = blueprint.CreatureFoundation ?? CreatureFoundationProfile.CreateDefault(
            Guid.NewGuid(), blueprint.Slug, blueprint.DisplayName, "toolset:r079");
        return new(foundation.Id, foundation.SchemaVersion, foundation.Provenance, foundation.RequiredAtRuntime,
            foundation.Identity, foundation.DefaultTransform, foundation.Resources, foundation.Movement,
            foundation.References, foundation.Spawned, foundation.Overrides, foundation.AuthoringComposition,
            foundation.AuthoringCompositionRequiredAtRuntime, foundation.RulesComposition,
            foundation.RulesCompositionRequiredAtRuntime, profile, _required!.ButtonPressed,
            foundation.EventAiComposition, foundation.EventAiCompositionRequiredAtRuntime);
    }

    private void RebuildSummary()
    {
        if (_summary is null || _preview is null) return;
        try
        {
            var profile = BuildProfile();
            var diagnostics = CreatureInteractionCompositionValidator.Validate(profile);
            SummarySectionCount = 5;
            _summary.Text =
                $"[color=#ffad72]FACTIONS[/color]\n{profile.Factions.Memberships.Count} membership(s) · principale {profile.Factions.PrimaryFactionId:D}\n\n" +
                $"[color=#ffad72]PERCEPTION[/color]\n{profile.Perception.Senses.Count} sens · mémoire {profile.Perception.MemorySeconds:0.#}s · cônes, occlusion et stimuli\n\n" +
                $"[color=#ffad72]DIALOGUES[/color]\n{profile.DialogueBindings.Count} binding(s) · contexte, priorité, interlocuteur, état et fallback\n\n" +
                $"[color=#ffad72]PROTECTION[/color]\nInvulnérable {profile.Protection.Invulnerable} · mort narrative {profile.Protection.UsesNarrativeDeath} · interruptible {profile.Protection.DialogueInterruptible}\n\n" +
                $"[color=#ffad72]ONCONVERSATION[/color]\n{profile.OnConversation.Handler} · serveur · ForgeScript {profile.OnConversation.ForgeScriptApiVersion}\n\n" +
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

    private void SimulateDialogue()
    {
        try
        {
            var profile = BuildProfile();
            var decision = CreatureInteractionCompositionValidator.RequestDialogue(
                profile, "greeting", "player.hero", "state.hostile");
            LastSelectedDialogueId = decision.Binding?.DialogueId.ToString();
            _preview?.ShowProfile(profile, decision);
            if (_feedback is not null)
                _feedback.Text = decision.Accepted
                    ? $"[color=#8ce0bd]Dialogue sélectionné : {LastSelectedDialogueId}[/color]"
                    : $"[color=#ff7777]Dialogue refusé : {decision.RefusalCode}[/color]";
        }
        catch (Exception exception)
        {
            LastSelectedDialogueId = null;
            if (_feedback is not null) _feedback.Text = $"[color=#ff7777]Simulation impossible : {exception.Message}[/color]";
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
            LastDiagnostics = string.Join('\n', result.Diagnostics.Select(item => $"{item.Code} · {item.Message}"));
            _feedback.Text = result.Succeeded
                ? dryRun
                    ? $"[color=#ffbd7a]APERÇU — aucune donnée modifiée[/color]\n{result.Patch?.Summary}\nInverse : {result.Inverse?.Summary}"
                    : $"[color=#8ce0bd]APPLIQUÉ · révision {result.NewRevision}[/color]\n{result.Patch?.Summary}"
                : $"[color=#ff7777]Transaction refusée[/color]\n{LastDiagnostics}";
        }
        catch (Exception exception)
        {
            LastDiagnostics = exception.Message;
            _feedback.Text = $"[color=#ff7777]Erreur de formulaire[/color]\n{exception.Message}";
        }
    }

    private static CreatureInteractionCompositionProfile CreateDefault(ProjectState state)
    {
        var factionId = state.Project.Module.Factions.Factions.FirstOrDefault()?.Id ?? Guid.Empty;
        var dialogueId = state.Project.Dialogues.FirstOrDefault()?.Id;
        return new(Guid.NewGuid(), 1, "toolset:r079", "asset.creature-interaction.default", TransformValue.Identity,
            new(Guid.NewGuid(), factionId,
                factionId == Guid.Empty ? [] : [new(Guid.NewGuid(), factionId, 1, "rank.member", 0, false)]),
            new(Guid.NewGuid(), [new(Guid.NewGuid(), CreatureInteractionSenseKind.Vision, 20, 120, true,
                ["stimulus.visual"])], 10),
            dialogueId is null ? [] : [new(Guid.NewGuid(), "greeting", 1, dialogueId, null, null, true)],
            new(Guid.NewGuid(), false, false, true, ["creature.inspect"]),
            new(Guid.NewGuid(), 1, "toolset:r079", "1.0", "creature.interaction_requested", "OnConversation",
                EventAuthorityScope.Server), new Dictionary<string, string>());
    }

    private static IReadOnlyList<CreatureInteractionFactionMembership> ParseMemberships(string text) =>
        Lines(text).Select(line =>
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 6 || !Guid.TryParse(parts[0], out var id) ||
                !Guid.TryParse(parts[1], out var factionId) || !TryDouble(parts[2], out var weight) ||
                !TryDouble(parts[4], out var reputation) || !bool.TryParse(parts[5], out var inherited))
                throw new FormatException("Membership attendu : uuid|faction|poids|rang|réputation|hérité.");
            return new CreatureInteractionFactionMembership(id, factionId, weight, parts[3], reputation, inherited);
        }).ToArray();

    private static IReadOnlyList<CreatureInteractionSenseProfile> ParseSenses(string text) => Lines(text).Select(line =>
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 6 || !Guid.TryParse(parts[0], out var id) ||
            !Enum.TryParse<CreatureInteractionSenseKind>(parts[1], true, out var kind) ||
            !TryDouble(parts[2], out var range) || !TryDouble(parts[3], out var cone) ||
            !bool.TryParse(parts[4], out var occlusion))
            throw new FormatException("Sens attendu : uuid|kind|portée|cône|occlusion|stimuli.");
        return new CreatureInteractionSenseProfile(id, kind, range, cone, occlusion, ParseIds(parts[5]));
    }).ToArray();

    private static IReadOnlyList<CreatureInteractionDialogueBinding> ParseDialogueBindings(string text) =>
        Lines(text).Select(line =>
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 7 || !Guid.TryParse(parts[0], out var id) ||
                !int.TryParse(parts[2], out var priority) || !bool.TryParse(parts[6], out var fallback))
                throw new FormatException("Binding attendu : uuid|contexte|priorité|DialogueId|interlocuteur|état|fallback.");
            return new CreatureInteractionDialogueBinding(id, parts[1], priority, DialogueId.Parse(parts[3]),
                EmptyToNull(parts[4]), EmptyToNull(parts[5]), fallback);
        }).ToArray();

    private static IReadOnlyDictionary<string, string> ParseStringMap(string text) => Lines(text).Select(line =>
    {
        var pair = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (pair.Length != 2) throw new FormatException("Entrée attendue : clé=valeur.");
        return (pair[0], pair[1]);
    }).ToDictionary(item => item.Item1, item => item.Item2, StringComparer.Ordinal);

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
