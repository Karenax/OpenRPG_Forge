#if TOOLS
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Lifecycle;
using Forge.Domain.Spatial;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class CreatureEventAiCompositionWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<BlueprintId, CreatureFoundationProfile, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private BlueprintId? _selectedId;
    private Guid _profileId;
    private Guid _perceptionId;
    private Guid _detectedId;
    private Guid _lostId;
    private Guid _attackId;
    private Guid _declaredId;
    private Guid _resolvedId;
    private Guid _abilityId;
    private Guid _targetedId;
    private Guid _impactedId;
    private Guid _blockedId;
    private Guid _blockedEventId;
    private Guid _controllerId;
    private ItemList? _creatures;
    private ScrollContainer? _scroll;
    private CheckBox? _required;
    private LineEdit? _asset;
    private LineEdit? _perceptionHandlers;
    private LineEdit? _attackHandlers;
    private LineEdit? _abilityHandlers;
    private LineEdit? _blockedHandler;
    private LineEdit? _recoveryOrder;
    private OptionButton? _controllerKind;
    private LineEdit? _controllerDefinition;
    private TextEdit? _blackboard;
    private LineEdit? _allowedActions;
    private TextEdit? _overrides;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;
    private CreatureEventAiCompositionPreview? _preview;

    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public int SummarySectionCount { get; private set; }
    public string? LastDecisionAction { get; private set; }

    public void Initialize(Func<ProjectState?> load,
        Func<BlueprintId, CreatureFoundationProfile, bool, CommandResult> mutate,
        Action undo, Action redo, Action<Window> applyIcon)
    {
        _load = load;
        _mutate = mutate;
        _undo = undo;
        _redo = redo;
        _applyIcon = applyIcon;
    }

    public override void _Ready()
    {
        Title = "Événements & IA de créature · OpenRPG Forge";
        MinSize = new(1180, 760);
        Size = new(1500, 930);
        CloseRequested += Hide;
        Build();
        Refresh();
    }

    public void Open()
    {
        Refresh();
        PopupCentered();
        _applyIcon?.Invoke(this);
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
            var profile = blueprint.CreatureFoundation?.EventAiComposition;
            var index = _creatures!.AddItem($"{blueprint.DisplayName} — {blueprint.Slug} · " +
                (profile is null ? "événements/IA vides" :
                    $"{profile.Controller.Kind} · {profile.Controller.AllowedActions.Count} action(s)"));
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
        _asset!.Text = "asset.creature-event-ai.ash-warden";
        _perceptionHandlers!.Text = "OnPerceptionDetected|OnPerceptionLost";
        _attackHandlers!.Text = "BeforePhysicalAttacked|OnPhysicalAttacked";
        _abilityHandlers!.Text = "BeforeAbilityTargeted|OnSpellCastAt";
        _blockedHandler!.Text = "OnBlocked";
        _recoveryOrder!.Text = "repath, avoid, teleport, abort";
        _controllerKind!.Select((int)CreatureEventAiControllerKind.Utility);
        _controllerDefinition!.Text = "ai.ash-warden";
        _blackboard!.Text =
            "a1800000-0000-4000-8000-000000000013|state|text|patrol|true\n" +
            "a1800000-0000-4000-8000-000000000014|target|entity|player.hero|false\n" +
            "a1800000-0000-4000-8000-000000000015|alert|boolean|true|true";
        _allowedActions!.Text = "patrol, attack, guard";
        _overrides!.Text = "ai.preview=enabled";
        RebuildSummary();
        return SummarySectionCount == 5 && _summary!.Text.Contains("PRÊT", StringComparison.Ordinal) &&
               _preview is not null;
    }

    public bool PrepareForCapture()
    {
        var prepared = ConfigureForSmokeTest();
        if (prepared) SimulateDecision();
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
        SimulateDecision();
        return LastDecisionAction == "attack";
    }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 16);
        AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 9);
        margin.AddChild(root);
        var title = new Label { Text = "ÉVÉNEMENTS & INTELLIGENCE ARTIFICIELLE" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Perception, attaques, capacités, blocage et contrôleur IA dans un seul document canonique."
        });

        var outer = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [260]
        };
        root.AddChild(outer);
        var catalog = Panel("CRÉATURES");
        outer.AddChild(catalog);
        _creatures = new ItemList { CustomMinimumSize = new(250, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _creatures.ItemSelected += index => SelectCreature((int)index);
        catalog.GetChild<VBoxContainer>(0).AddChild(_creatures);

        var editor = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [700]
        };
        outer.AddChild(editor);
        _scroll = new ScrollContainer
        {
            CustomMinimumSize = new(670, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        editor.AddChild(_scroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 7);
        _scroll.AddChild(form);
        _required = new CheckBox { Text = "Événements & IA requis par le runtime" };
        form.AddChild(_required);
        _asset = Field(form, "AssetId", "asset.creature-event-ai.guardian");

        Section(form, "ONPERCEPTION · DÉTECTION / PERTE");
        _perceptionHandlers = Field(form, "Handlers — détecté|perdu", "OnPerceptionDetected|OnPerceptionLost");
        Section(form, "ONPHYSICALATTACKED · DÉCLARÉE / RÉSOLUE");
        _attackHandlers = Field(form, "Handlers — avant|après", "BeforePhysicalAttacked|OnPhysicalAttacked");
        Section(form, "ONSPELLCASTAT · CIBLÉE / IMPACTÉE");
        _abilityHandlers = Field(form, "Handlers — avant|après", "BeforeAbilityTargeted|OnSpellCastAt");
        Section(form, "ONBLOCKED · NAVIGATION");
        _blockedHandler = Field(form, "Handler MovementBlocked", "OnBlocked");
        _recoveryOrder = Field(form, "Récupération ordonnée", "repath, avoid, teleport, abort");
        Section(form, "INTELLIGENCE ARTIFICIELLE PAR PROFIL");
        _controllerKind = new OptionButton();
        foreach (var kind in Enum.GetValues<CreatureEventAiControllerKind>())
            _controllerKind.AddItem(kind.ToString(), (int)kind);
        form.AddChild(_controllerKind);
        _controllerDefinition = Field(form, "Définition du contrôleur", "ai.guardian");
        _blackboard = Multi(form, "Blackboard — uuid|clé|type|valeur|persistant", 95);
        _allowedActions = Field(form, "Actions autorisées", "idle, patrol, attack");
        _overrides = Multi(form, "Overrides explicites — clé=valeur", 52);

        var actions = new HBoxContainer();
        form.AddChild(actions);
        Button(actions, "Prévisualiser", () => Mutate(true));
        Button(actions, "Appliquer", () => Mutate(false), true);
        Button(actions, "Simuler IA", SimulateDecision);
        Button(actions, "Annuler", () => { _undo?.Invoke(); Refresh(_selectedId); });
        Button(actions, "Rétablir", () => { _redo?.Invoke(); Refresh(_selectedId); });

        var right = Panel("APERÇU & VALIDATION");
        editor.AddChild(right);
        var rightBox = right.GetChild<VBoxContainer>(0);
        _preview = new CreatureEventAiCompositionPreview();
        rightBox.AddChild(_preview);
        _summary = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(360, 190), FitContent = false };
        rightBox.AddChild(_summary);
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(360, 105), FitContent = false };
        rightBox.AddChild(_feedback);

        foreach (var control in new Control[] { _required, _asset, _perceptionHandlers, _attackHandlers,
                     _abilityHandlers, _blockedHandler, _recoveryOrder, _controllerKind, _controllerDefinition,
                     _blackboard, _allowedActions, _overrides })
            if (control is LineEdit line) line.TextChanged += _ => RebuildSummary();
            else if (control is TextEdit text) text.TextChanged += RebuildSummary;
            else if (control is CheckBox check) check.Toggled += _ => RebuildSummary();
            else if (control is OptionButton option) option.ItemSelected += _ => RebuildSummary();
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
        var profile = blueprint.CreatureFoundation?.EventAiComposition ??
                      CreatureEventAiCompositionProfile.CreateDefault(Guid.NewGuid(), "toolset:r080");
        CaptureIds(profile);
        _required!.ButtonPressed = blueprint.CreatureFoundation?.EventAiCompositionRequiredAtRuntime ?? false;
        _asset!.Text = profile.AssetId;
        _perceptionHandlers!.Text = $"{profile.Perception.Detected.Handler}|{profile.Perception.Lost.Handler}";
        _attackHandlers!.Text = $"{profile.PhysicalAttack.Declared.Handler}|{profile.PhysicalAttack.Resolved.Handler}";
        _abilityHandlers!.Text = $"{profile.Ability.Targeted.Handler}|{profile.Ability.Impacted.Handler}";
        _blockedHandler!.Text = profile.Blocked.Blocked.Handler;
        _recoveryOrder!.Text = string.Join(", ", profile.Blocked.RecoveryOrder.Select(item => item.ToString().ToLowerInvariant()));
        _controllerKind!.Select((int)profile.Controller.Kind);
        _controllerDefinition!.Text = profile.Controller.DefinitionId;
        _blackboard!.Text = string.Join('\n', profile.Controller.Blackboard.Select(item =>
            $"{item.Id:D}|{item.Key}|{item.Kind.ToString().ToLowerInvariant()}|{item.Value}|{item.Persistent.ToString().ToLowerInvariant()}"));
        _allowedActions!.Text = string.Join(", ", profile.Controller.AllowedActions);
        _overrides!.Text = string.Join('\n', profile.Overrides.Select(item => $"{item.Key}={item.Value}"));
        _feedback!.Text = blueprint.CreatureFoundation?.EventAiComposition is null
            ? "[color=#ffbd7a]État vide : profil initial non enregistré.[/color]"
            : "[color=#8ce0bd]Profil canonique chargé.[/color]";
        RebuildSummary();
    }

    private void CaptureIds(CreatureEventAiCompositionProfile profile)
    {
        _profileId = profile.Id;
        _perceptionId = profile.Perception.Id;
        _detectedId = profile.Perception.Detected.Id;
        _lostId = profile.Perception.Lost.Id;
        _attackId = profile.PhysicalAttack.Id;
        _declaredId = profile.PhysicalAttack.Declared.Id;
        _resolvedId = profile.PhysicalAttack.Resolved.Id;
        _abilityId = profile.Ability.Id;
        _targetedId = profile.Ability.Targeted.Id;
        _impactedId = profile.Ability.Impacted.Id;
        _blockedId = profile.Blocked.Id;
        _blockedEventId = profile.Blocked.Blocked.Id;
        _controllerId = profile.Controller.Id;
    }

    private CreatureEventAiCompositionProfile BuildProfile()
    {
        var perception = Pair(_perceptionHandlers!.Text, "OnPerception handlers attendus : détecté|perdu.");
        var attack = Pair(_attackHandlers!.Text, "OnPhysicalAttacked handlers attendus : avant|après.");
        var ability = Pair(_abilityHandlers!.Text, "OnSpellCastAt handlers attendus : avant|après.");
        var kind = (CreatureEventAiControllerKind)_controllerKind!.GetSelectedId();
        return new(_profileId, 1, "toolset:r080", _asset!.Text, TransformValue.Identity,
            new(_perceptionId, Binding(_detectedId, "creature.stimulus_detected", perception[0]),
                Binding(_lostId, "creature.stimulus_lost", perception[1])),
            new(_attackId, Binding(_declaredId, "creature.attack_declared", attack[0]),
                Binding(_resolvedId, "creature.attack_resolved", attack[1])),
            new(_abilityId, Binding(_targetedId, "creature.ability_targeted", ability[0]),
                Binding(_impactedId, "creature.ability_impacted", ability[1])),
            new(_blockedId, Binding(_blockedEventId, "creature.movement_blocked", _blockedHandler!.Text),
                ParseEnumList<CreatureEventAiRecoveryStrategy>(_recoveryOrder!.Text)),
            new(_controllerId, kind, _controllerDefinition!.Text, ParseBlackboard(_blackboard!.Text),
                ParseIds(_allowedActions!.Text)), ParseStringMap(_overrides!.Text));
    }

    private CreatureFoundationProfile BuildFoundation(CreatureEventAiCompositionProfile profile)
    {
        var state = _load?.Invoke() ?? throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var blueprint = state.Project.Blueprints.Single(item => item.Id == _selectedId);
        var foundation = blueprint.CreatureFoundation ?? CreatureFoundationProfile.CreateDefault(
            Guid.NewGuid(), blueprint.Slug, blueprint.DisplayName, "toolset:r080");
        return new(foundation.Id, foundation.SchemaVersion, foundation.Provenance, foundation.RequiredAtRuntime,
            foundation.Identity, foundation.DefaultTransform, foundation.Resources, foundation.Movement,
            foundation.References, foundation.Spawned, foundation.Overrides, foundation.AuthoringComposition,
            foundation.AuthoringCompositionRequiredAtRuntime, foundation.RulesComposition,
            foundation.RulesCompositionRequiredAtRuntime, foundation.InteractionComposition,
            foundation.InteractionCompositionRequiredAtRuntime, profile, _required!.ButtonPressed);
    }

    private void RebuildSummary()
    {
        if (_summary is null || _preview is null) return;
        try
        {
            var profile = BuildProfile();
            var diagnostics = CreatureEventAiCompositionValidator.Validate(profile);
            SummarySectionCount = 5;
            _summary.Text =
                $"[color=#ffad72]ONPERCEPTION[/color]\nDétection/perte · sens, confiance, position et mémoire\n\n" +
                $"[color=#ffad72]ONPHYSICALATTACKED[/color]\nDéclarée/résolue · attaquant, arme, résultat et contexte réseau\n\n" +
                $"[color=#ffad72]ONSPELLCASTAT[/color]\nCiblée/impactée · source, tags, contexte, résultat et magnitude\n\n" +
                $"[color=#ffad72]ONBLOCKED[/color]\n{profile.Blocked.RecoveryOrder.Count} stratégie(s) de récupération ordonnée(s)\n\n" +
                $"[color=#ffad72]IA PAR PROFIL[/color]\n{profile.Controller.Kind} · {profile.Controller.Blackboard.Count} valeur(s) · {profile.Controller.AllowedActions.Count} action(s)\n\n" +
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

    private void SimulateDecision()
    {
        try
        {
            var profile = BuildProfile();
            var decision = CreatureEventAiCompositionValidator.Decide(profile,
                new Dictionary<string, double> { ["attack"] = .9, ["guard"] = .4, ["patrol"] = .2 });
            LastDecisionAction = decision.Action;
            _preview?.ShowProfile(profile, decision);
            if (_feedback is not null)
                _feedback.Text = $"[color=#8ce0bd]Décision simulée : {decision.Action} · {decision.Score:0.##}[/color]";
        }
        catch (Exception exception)
        {
            LastDecisionAction = null;
            if (_feedback is not null)
                _feedback.Text = $"[color=#ff7777]Simulation impossible : {exception.Message}[/color]";
        }
    }

    private void Mutate(bool dryRun)
    {
        if (_mutate is null || _selectedId is null || _feedback is null) return;
        LastCommandId = Guid.NewGuid();
        LastDiagnostics = string.Empty;
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

    private static EventBinding Binding(Guid id, string eventName, string handler) =>
        new(id, 1, "toolset:r080", "1.0", eventName, handler, EventAuthorityScope.Server);

    private static string[] Pair(string text, string error)
    {
        var parts = text.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace)) throw new FormatException(error);
        return parts;
    }

    private static IReadOnlyList<CreatureEventAiBlackboardEntry> ParseBlackboard(string text) => Lines(text)
        .Select(line =>
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 5 || !Guid.TryParse(parts[0], out var id) ||
                !Enum.TryParse<CreatureEventAiBlackboardValueKind>(parts[2], true, out var kind) ||
                !bool.TryParse(parts[4], out var persistent))
                throw new FormatException("Blackboard attendu : uuid|clé|type|valeur|persistant.");
            return new CreatureEventAiBlackboardEntry(id, parts[1], kind, parts[3], persistent);
        }).ToArray();

    private static IReadOnlyList<T> ParseEnumList<T>(string text) where T : struct, Enum => ParseIds(text)
        .Select(value => Enum.TryParse<T>(value, true, out var parsed) ? parsed :
            throw new FormatException($"Valeur {typeof(T).Name} invalide : {value}.")).ToArray();

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

    private static void Section(Container owner, string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new Color("ffad70"));
        label.AddThemeFontSizeOverride("font_size", 15);
        owner.AddChild(label);
    }

    private static LineEdit Field(Container owner, string label, string placeholder)
    {
        owner.AddChild(new Label { Text = label });
        var field = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        owner.AddChild(field);
        return field;
    }

    private static TextEdit Multi(Container owner, string label, float height)
    {
        owner.AddChild(new Label { Text = label });
        var field = new TextEdit { CustomMinimumSize = new(0, height), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        owner.AddChild(field);
        return field;
    }

    private static void Button(Container owner, string text, Action action, bool primary = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new(115, 36) };
        if (primary) button.AddThemeColorOverride("font_color", new Color("fff4ec"));
        button.Pressed += action;
        owner.AddChild(button);
    }

    private static PanelContainer Panel(string title)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);
        var label = new Label { Text = title };
        label.AddThemeColorOverride("font_color", new Color("ffad70"));
        box.AddChild(label);
        return panel;
    }
}
#endif
