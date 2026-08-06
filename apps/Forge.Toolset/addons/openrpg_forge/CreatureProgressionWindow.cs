#if TOOLS
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class CreatureProgressionWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<BlueprintId, CreatureProgressionProfile, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private ItemList? _creatures;
    private Label? _status;
    private RichTextLabel? _feedback;
    private RichTextLabel? _summary;
    private LineEdit? _ruleset;
    private CheckBox? _multiclass;
    private CheckBox? _runtimeRequired;
    private TextEdit? _tracks;
    private TextEdit? _axes;
    private TextEdit? _skills;
    private TextEdit? _talents;
    private OptionButton? _loadoutMode;
    private LineEdit? _provider;
    private SpinBox? _capacity;
    private LineEdit? _learned;
    private TextEdit? _prepared;
    private TextEdit? _overrides;
    private BlueprintId? _selectedBlueprintId;
    private Guid _draftProgressionId;
    private bool _lastCommandSucceeded;

    public Guid LastCommandId { get; private set; }

    public string LastPatchSummary { get; private set; } = string.Empty;

    public BlueprintId? SelectedBlueprintId => _selectedBlueprintId;

    public int SummarySectionCount { get; private set; }

    public void Initialize(
        Func<ProjectState?> load,
        Func<BlueprintId, CreatureProgressionProfile, bool, CommandResult> mutate,
        Action undo,
        Action redo,
        Action<Window> applyIcon)
    {
        _load = load;
        _mutate = mutate;
        _undo = undo;
        _redo = redo;
        _applyIcon = applyIcon;
    }

    public override void _Ready()
    {
        Title = "Progression de créature · OpenRPG Forge";
        MinSize = new Vector2I(1160, 760);
        Size = new Vector2I(1380, 880);
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

    public bool ConfigureForSmokeTest()
    {
        Refresh();
        var blueprint = SelectedBlueprint();
        var abilityIds = blueprint?.Combat?.Abilities.Select(item => item.Slug).ToArray() ?? [];
        if (_selectedBlueprintId is null || abilityIds.Length == 0) return false;
        var primaryAbility = abilityIds[0];
        _ruleset!.Text = "forge.generic";
        _multiclass!.ButtonPressed = true;
        _runtimeRequired!.ButtonPressed = true;
        _tracks!.Text = "warden|Gardien|3|20|2400|ember-mastery\nscholar|Érudit|2|10|900|";
        _axes!.Text = "mercy|Impitoyable|Miséricordieux|0.65\ntradition|Novateur|Traditionaliste|-0.2";
        _skills!.Text = "lore|Savoir ancien|insight|3|10|4|true|true|12\nathletics|Athlétisme|might|2|10|2|true|false|10";
        _talents!.Text = $"ember-mastery|Maîtrise des braises|1|3|true|track:warden:2,skill:lore:2|{primaryAbility}|combat.on-hit";
        SelectLoadoutMode(AbilityLoadoutMode.Prepared);
        _provider!.Text = "prepared";
        _capacity!.Value = Math.Max(2, abilityIds.Length);
        _learned!.Text = string.Join(", ", abilityIds);
        _prepared!.Text = $"{primaryAbility}|2|3|4.5|focus|2|empowered";
        _overrides!.Text = "xp-rate=1.0";
        RebuildSummary();
        return true;
    }

    public bool PreviewForSmokeTest()
    {
        Mutate(true);
        return _lastCommandSucceeded;
    }

    public bool ApplyForSmokeTest()
    {
        Mutate(false);
        return _lastCommandSucceeded;
    }

    public void Refresh()
    {
        var state = _load?.Invoke();
        var previous = _selectedBlueprintId;
        _creatures?.Clear();
        if (state is null)
        {
            SetEmpty("Aucun projet ouvert.");
            return;
        }

        var candidates = state.Project.Blueprints
            .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .OrderBy(item => item.Combat?.Abilities.Count > 0 ? 0 : 1)
            .ThenBy(item => item.IsReadOnly())
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        foreach (var blueprint in candidates)
        {
            var level = blueprint.CreatureProgression?.Tracks.Sum(item => item.Level) ?? 0;
            var index = _creatures!.AddItem($"{blueprint.DisplayName}\n{blueprint.Slug} · niveau {level}");
            _creatures.SetItemMetadata(index, blueprint.Id.ToString());
        }

        if (candidates.Length == 0)
        {
            SetEmpty("Aucun blueprint de créature. Créez-en un dans l’Atelier RPG Kit.");
            return;
        }

        var selectedIndex = Array.FindIndex(candidates, item => item.Id == previous);
        if (selectedIndex < 0) selectedIndex = 0;
        _creatures!.Select(selectedIndex);
        LoadBlueprint(candidates[selectedIndex]);
    }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 22);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 12); margin.AddChild(root);
        var title = new Label { Text = "PROGRESSION & POUVOIRS" };
        title.AddThemeFontSizeOverride("font_size", 25);
        title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Composez librement classes, dispositions, compétences, talents et pouvoirs — sans règles propriétaires imposées.",
        });
        _status = new Label(); _status.AddThemeColorOverride("font_color", new Color("8ce0bd")); root.AddChild(_status);

        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [260],
        };
        root.AddChild(split);
        var catalog = Panel("CRÉATURES DU MODULE"); split.AddChild(catalog);
        _creatures = new ItemList
        {
            CustomMinimumSize = new Vector2(260, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _creatures.ItemSelected += OnCreatureSelected;
        catalog.GetChild<VBoxContainer>(0).AddChild(_creatures);

        var editor = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SplitOffsets = [670],
        };
        split.AddChild(editor);
        var formPanel = Panel("CONFIGURATION CANONIQUE"); editor.AddChild(formPanel);
        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        formPanel.GetChild<VBoxContainer>(0).AddChild(tabs);
        BuildRulesTab(tabs);
        BuildSkillsTab(tabs);
        BuildTalentsTab(tabs);
        BuildLoadoutTab(tabs);

        var summaryPanel = Panel("RÉSUMÉ DES RÈGLES"); editor.AddChild(summaryPanel);
        var summaryColumn = summaryPanel.GetChild<VBoxContainer>(0);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(320, 390),
        };
        summaryColumn.AddChild(_summary);
        var rebuild = Button("Actualiser le résumé", RebuildSummary); summaryColumn.AddChild(rebuild);
        _feedback = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            CustomMinimumSize = new Vector2(320, 150),
        };
        summaryColumn.AddChild(_feedback);

        var actions = new HBoxContainer(); root.AddChild(actions);
        actions.AddChild(Button("Prévisualiser la transaction", () => Mutate(true)));
        actions.AddChild(Button("Appliquer au projet", () => Mutate(false), true));
        actions.AddChild(Button("Annuler", () => { _undo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Rétablir", () => { _redo?.Invoke(); Refresh(); }));
        var close = Button("Fermer", Hide); close.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd; actions.AddChild(close);
    }

    private void BuildRulesTab(TabContainer tabs)
    {
        var form = Tab(tabs, "PARCOURS");
        _ruleset = Field(form, "Ruleset data-driven", "forge.generic");
        _multiclass = new CheckBox { Text = "Autoriser plusieurs pistes de progression" }; form.AddChild(_multiclass);
        _runtimeRequired = new CheckBox { Text = "Profil requis par le runtime (sinon erreur explicite)" }; form.AddChild(_runtimeRequired);
        _tracks = Multi(form, "Pistes — id|nom|niveau|max|expérience|talents", 150);
        _axes = Multi(form, "Axes de disposition libres — id|pôle négatif|pôle positif|valeur (-1..1)", 115);
        _overrides = Multi(form, "Overrides explicites — clé=valeur", 90);
    }

    private void BuildSkillsTab(TabContainer tabs)
    {
        var form = Tab(tabs, "COMPÉTENCES");
        form.AddChild(Help("Une ligne par compétence. L’expertise exige que la compétence soit entraînée."));
        _skills = Multi(form, "id|nom|attribut|rang|max|points|entraînée|expertise|difficulté", 390);
    }

    private void BuildTalentsTab(TabContainer tabs)
    {
        var form = Tab(tabs, "TALENTS");
        form.AddChild(Help("Prérequis : track:warden:2, skill:lore:2 ou talent:focus:1. Les pouvoirs doivent exister dans Combat."));
        _talents = Multi(form, "id|nom|rang|max|passif|prérequis|pouvoirs|hooks", 390);
    }

    private void BuildLoadoutTab(TabContainer tabs)
    {
        var form = Tab(tabs, "POUVOIRS");
        form.AddChild(new Label { Text = "Mode de chargement" });
        _loadoutMode = new OptionButton();
        foreach (var mode in Enum.GetValues<AbilityLoadoutMode>()) _loadoutMode.AddItem(mode.ToString());
        form.AddChild(_loadoutMode);
        _provider = Field(form, "Provider", "prepared");
        form.AddChild(new Label { Text = "Capacité du loadout" });
        _capacity = new SpinBox { MinValue = 0, MaxValue = 999, Step = 1, Value = 4 }; form.AddChild(_capacity);
        _learned = Field(form, "Pouvoirs appris (séparés par des virgules)", "ember-strike");
        _prepared = Multi(form, "Préparés — pouvoir|rang|charges|cooldown|ressource|coût|modificateurs", 250);
    }

    private void LoadBlueprint(BlueprintDefinition blueprint)
    {
        _selectedBlueprintId = blueprint.Id;
        var profile = blueprint.CreatureProgression ??
            CreatureProgressionProfile.CreateDefault(Guid.NewGuid(), "toolset:creature-progression");
        _draftProgressionId = profile.Id;
        _ruleset!.Text = profile.RulesetId;
        _multiclass!.ButtonPressed = profile.AllowMultipleTracks;
        _runtimeRequired!.ButtonPressed = profile.RequiredAtRuntime;
        _tracks!.Text = string.Join('\n', profile.Tracks.Select(item =>
            $"{item.TrackId}|{item.DisplayName}|{item.Level}|{item.MaximumLevel}|{Invariant(item.Experience)}|{string.Join(',', item.GrantedTalentIds)}"));
        _axes!.Text = string.Join('\n', profile.DispositionAxes.Select(item =>
            $"{item.AxisId}|{item.NegativeLabel}|{item.PositiveLabel}|{Invariant(item.Value)}"));
        _skills!.Text = string.Join('\n', profile.Skills.Select(item =>
            $"{item.SkillId}|{item.DisplayName}|{item.GoverningAttributeId}|{item.Rank}|{item.MaximumRank}|{item.TrainingPoints}|{item.Trained}|{item.Expertise}|{Invariant(item.DefaultDifficulty)}"));
        _talents!.Text = string.Join('\n', profile.Talents.Select(item =>
            $"{item.TalentId}|{item.DisplayName}|{item.Rank}|{item.MaximumRank}|{item.Passive}|" +
            $"{string.Join(',', item.Prerequisites.Select(value => $"{value.Kind}:{value.ReferenceId}:{value.MinimumRank}"))}|" +
            $"{string.Join(',', item.GrantedAbilityIds)}|{string.Join(',', item.HookIds)}"));
        SelectLoadoutMode(profile.Loadout.Mode);
        _provider!.Text = profile.Loadout.ProviderId;
        _capacity!.Value = profile.Loadout.Capacity;
        _learned!.Text = string.Join(", ", profile.Loadout.LearnedAbilityIds);
        _prepared!.Text = string.Join('\n', profile.Loadout.PreparedAbilities.Select(item =>
            $"{item.AbilityId}|{item.Rank}|{item.Charges}|{Invariant(item.CooldownSeconds)}|{item.ResourceId}|{item.ResourceCost}|{string.Join(',', item.Modifiers)}"));
        _overrides!.Text = string.Join('\n', profile.Overrides.Select(item => $"{item.Key}={item.Value}"));
        _status!.Text = $"{blueprint.DisplayName} · UUID {profile.Id:D} · schéma v{profile.SchemaVersion} · {profile.Provenance}";
        _feedback!.Text = blueprint.CreatureProgression is null
            ? "[color=#b8a8ce]État vide : un parcours générique est proposé. Configurez puis prévisualisez.[/color]"
            : "[color=#8ce0bd]Progression canonique chargée.[/color]";
        SetEnabled(true);
        RebuildSummary();
    }

    private void Mutate(bool dryRun)
    {
        _lastCommandSucceeded = false;
        LastCommandId = Guid.Empty;
        LastPatchSummary = string.Empty;
        if (_selectedBlueprintId is null || _mutate is null || _feedback is null) return;
        try
        {
            var profile = BuildProfile();
            var result = _mutate(_selectedBlueprintId, profile, dryRun);
            LastCommandId = result.Audit.CommandId.Value;
            LastPatchSummary = result.Patch?.Summary ?? string.Empty;
            if (!result.Succeeded)
            {
                _feedback.Text = $"[color=#ff7777]Configuration refusée[/color]\n{string.Join("\n", result.Diagnostics.Select(item => $"{item.Code} · {item.Message}"))}";
                return;
            }

            _lastCommandSucceeded = true;
            _feedback.Text = dryRun
                ? $"[color=#ffbd7a]APERÇU — aucune donnée modifiée[/color]\n{result.Patch?.Summary}\nInverse : {result.Inverse?.Summary}"
                : $"[color=#8ce0bd]APPLIQUÉ AU PROJET[/color]\n{result.Patch?.Summary}\nRévision {result.NewRevision} · utilisez Sauvegarder pour écrire sur disque";
            if (!dryRun) Refresh();
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            _feedback.Text = $"[color=#ff7777]Erreur de formulaire[/color]\n{exception.Message}";
        }
    }

    private CreatureProgressionProfile BuildProfile()
    {
        var mode = Enum.Parse<AbilityLoadoutMode>(_loadoutMode!.GetItemText(_loadoutMode.Selected));
        return new CreatureProgressionProfile(
            _draftProgressionId,
            CreatureProgressionProfile.CurrentSchemaVersion,
            "toolset:creature-progression",
            _ruleset!.Text,
            _multiclass!.ButtonPressed,
            _runtimeRequired!.ButtonPressed,
            ParseTracks(_tracks!.Text),
            ParseAxes(_axes!.Text),
            ParseSkills(_skills!.Text),
            ParseTalents(_talents!.Text),
            new CreatureProgressionProfile.AbilityLoadout(
                _provider!.Text, mode, checked((int)_capacity!.Value), Csv(_learned!.Text), ParsePrepared(_prepared!.Text)),
            ParseOverrides(_overrides!.Text));
    }

    private void RebuildSummary()
    {
        if (_summary is null) return;
        try
        {
            var profile = BuildProfile();
            var blueprint = SelectedBlueprint();
            var abilities = (blueprint?.Combat?.Abilities ?? []).Select(item => item.Slug).ToHashSet(StringComparer.Ordinal);
            var diagnostics = CreatureProgressionValidator.Validate(profile, availableAbilityIds: abilities);
            var level = profile.Tracks.Sum(item => item.Level);
            SummarySectionCount = 5;
            _summary.Text =
                $"[font_size=22][color=#ff9858]Niveau total {level}[/color][/font_size]\n" +
                $"[color=#8ce0bd]{profile.RulesetId}[/color] · {(profile.AllowMultipleTracks ? "multiclasse" : "piste unique")}\n\n" +
                $"[b]PARCOURS[/b]\n{Lines(profile.Tracks.Select(item => $"{item.DisplayName} · niv. {item.Level}/{item.MaximumLevel} · {item.Experience:G} XP"))}\n\n" +
                $"[b]DISPOSITIONS[/b]\n{Lines(profile.DispositionAxes.Select(item => $"{item.NegativeLabel} ← {item.Value:+0.00;-0.00;0.00} → {item.PositiveLabel}"))}\n\n" +
                $"[b]COMPÉTENCES[/b]\n{Lines(profile.Skills.Select(item => $"{item.DisplayName} · rang {item.Rank}/{item.MaximumRank}{(item.Expertise ? " · expertise" : item.Trained ? " · entraînée" : "")}"))}\n\n" +
                $"[b]TALENTS[/b]\n{Lines(profile.Talents.Select(item => $"{item.DisplayName} · rang {item.Rank}/{item.MaximumRank} · {string.Join(", ", item.GrantedAbilityIds)}"))}\n\n" +
                $"[b]LOADOUT {profile.Loadout.Mode.ToString().ToUpperInvariant()}[/b]\n" +
                $"{profile.Loadout.PreparedAbilities.Count}/{profile.Loadout.Capacity} préparé(s) · {profile.Loadout.LearnedAbilityIds.Count} appris\n\n" +
                (diagnostics.Count == 0
                    ? "[color=#8ce0bd]✓ Configuration valide et références résolues[/color]"
                    : $"[color=#ff7777]⚠ {diagnostics.Count} diagnostic(s)[/color]\n{Lines(diagnostics.Select(item => $"{item.Code} · {item.Path}"))}");
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            SummarySectionCount = 0;
            _summary.Text = $"[color=#ff7777]Formulaire incomplet[/color]\n{exception.Message}";
        }
    }

    private static IReadOnlyList<CreatureProgressionProfile.ProgressionTrack> ParseTracks(string text) =>
        Rows(text, 6, "piste").Select(parts => new CreatureProgressionProfile.ProgressionTrack(
            parts[0], parts[1], Integer(parts[2], "niveau"), Integer(parts[3], "niveau maximal"),
            Number(parts[4], "expérience"), Csv(parts[5]))).ToArray();

    private static IReadOnlyList<CreatureProgressionProfile.DispositionAxis> ParseAxes(string text) =>
        Rows(text, 4, "axe").Select(parts => new CreatureProgressionProfile.DispositionAxis(
            parts[0], parts[1], parts[2], Number(parts[3], "valeur d’axe"))).ToArray();

    private static IReadOnlyList<CreatureProgressionProfile.SkillProgression> ParseSkills(string text) =>
        Rows(text, 9, "compétence").Select(parts => new CreatureProgressionProfile.SkillProgression(
            parts[0], parts[1], parts[2], Integer(parts[3], "rang"), Integer(parts[4], "rang maximal"),
            Integer(parts[5], "points"), Boolean(parts[6], "entraînée"), Boolean(parts[7], "expertise"),
            Number(parts[8], "difficulté"))).ToArray();

    private static IReadOnlyList<CreatureProgressionProfile.TalentProgression> ParseTalents(string text) =>
        Rows(text, 8, "talent").Select(parts => new CreatureProgressionProfile.TalentProgression(
            parts[0], parts[1], Integer(parts[2], "rang"), Integer(parts[3], "rang maximal"),
            Boolean(parts[4], "passif"), ParsePrerequisites(parts[5]), Csv(parts[6]), Csv(parts[7]))).ToArray();

    private static IReadOnlyList<CreatureProgressionProfile.TalentPrerequisite> ParsePrerequisites(string text) =>
        Csv(text).Select(value =>
        {
            var parts = value.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 3) throw new FormatException($"Prérequis invalide : '{value}'. Format attendu type:id:rang.");
            return new CreatureProgressionProfile.TalentPrerequisite(parts[0], parts[1], Integer(parts[2], "rang requis"));
        }).ToArray();

    private static IReadOnlyList<CreatureProgressionProfile.PreparedAbility> ParsePrepared(string text) =>
        Rows(text, 7, "pouvoir préparé").Select(parts => new CreatureProgressionProfile.PreparedAbility(
            parts[0], Integer(parts[1], "rang"), Integer(parts[2], "charges"), Number(parts[3], "cooldown"),
            string.IsNullOrWhiteSpace(parts[4]) ? null : parts[4], Integer(parts[5], "coût"), Csv(parts[6]))).ToArray();

    private static IReadOnlyDictionary<string, string> ParseOverrides(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                throw new FormatException($"Override invalide : '{line}'. Format attendu clé=valeur.");
            if (!result.TryAdd(parts[0], parts[1])) throw new FormatException($"Override dupliqué : '{parts[0]}'.");
        }
        return result;
    }

    private static IEnumerable<string[]> Rows(string text, int count, string label)
    {
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != count) throw new FormatException($"Ligne {label} invalide : '{line}' ({count} colonnes attendues).");
            yield return parts;
        }
    }

    private static string[] Csv(string text) => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int Integer(string text, string label) =>
        int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value : throw new FormatException($"{label} invalide : '{text}'.");

    private static double Number(string text, string label) =>
        double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value : throw new FormatException($"{label} invalide : '{text}'. Utilisez le point décimal.");

    private static string Invariant(double value) =>
        value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);

    private static bool Boolean(string text, string label) =>
        bool.TryParse(text, out var value) ? value : throw new FormatException($"{label} invalide : '{text}' (true/false attendu).");

    private BlueprintDefinition? SelectedBlueprint() => _load?.Invoke()?.Project.Blueprints
        .SingleOrDefault(item => item.Id == _selectedBlueprintId);

    private void OnCreatureSelected(long index)
    {
        if (_creatures is null || _load?.Invoke() is not { } state) return;
        var id = BlueprintId.Parse(_creatures.GetItemMetadata((int)index).AsString());
        var blueprint = state.Project.Blueprints.Single(item => item.Id == id);
        LoadBlueprint(blueprint);
    }

    private void SelectLoadoutMode(AbilityLoadoutMode mode)
    {
        if (_loadoutMode is null) return;
        for (var index = 0; index < _loadoutMode.ItemCount; index++)
        {
            if (string.Equals(_loadoutMode.GetItemText(index), mode.ToString(), StringComparison.Ordinal))
            {
                _loadoutMode.Select(index);
                return;
            }
        }
    }

    private void SetEmpty(string message)
    {
        _selectedBlueprintId = null;
        _status!.Text = message;
        _feedback!.Text = $"[color=#b8a8ce]{message}[/color]";
        if (_summary is not null) _summary.Text = "[color=#b8a8ce]Sélectionnez une créature pour inspecter sa progression.[/color]";
        SummarySectionCount = 0;
        SetEnabled(false);
    }

    private void SetEnabled(bool enabled)
    {
        foreach (var control in new Control?[] { _ruleset, _multiclass, _runtimeRequired, _tracks, _axes, _skills,
                     _talents, _loadoutMode, _provider, _capacity, _learned, _prepared, _overrides })
            if (control is not null) control.MouseFilter = enabled ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Ignore;
    }

    private static PanelContainer Panel(string title)
    {
        var panel = new PanelContainer();
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 9); panel.AddChild(column);
        var label = new Label { Text = title }; label.AddThemeColorOverride("font_color", new Color("ffad72"));
        column.AddChild(label);
        return panel;
    }

    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer { Name = title, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 8); scroll.AddChild(form); tabs.AddChild(scroll);
        return form;
    }

    private static Label Help(string text)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeColorOverride("font_color", new Color("b8a8ce"));
        return label;
    }

    private static LineEdit Field(VBoxContainer parent, string label, string placeholder)
    {
        parent.AddChild(new Label { Text = label });
        var field = new LineEdit { PlaceholderText = placeholder }; parent.AddChild(field); return field;
    }

    private static TextEdit Multi(VBoxContainer parent, string label, float height)
    {
        parent.AddChild(new Label { Text = label });
        var field = new TextEdit { CustomMinimumSize = new Vector2(0, height) }; parent.AddChild(field); return field;
    }

    private static Button Button(string text, Action pressed, bool primary = false)
    {
        var button = new Button { Text = text };
        if (primary) button.AddThemeColorOverride("font_color", new Color("fff1e8"));
        button.Pressed += pressed;
        return button;
    }

    private static string Lines(IEnumerable<string> values)
    {
        var materialized = values.ToArray();
        return materialized.Length == 0 ? "[color=#776b88]Aucun[/color]" : string.Join("\n", materialized.Select(item => $"• {item}"));
    }
}
#endif
