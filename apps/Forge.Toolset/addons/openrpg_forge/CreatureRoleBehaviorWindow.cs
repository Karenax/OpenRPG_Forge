#if TOOLS
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class CreatureRoleBehaviorWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<BlueprintId, CreatureRoleBehaviorProfile, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private ItemList? _creatures;
    private Label? _status;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;
    private CheckBox? _runtimeRequired;
    private LineEdit? _ruleset;
    private TextEdit? _lore;
    private LineEdit? _roleTags;
    private LineEdit? _graphId;
    private TextEdit? _idleSets;
    private TextEdit? _schedules;
    private TextEdit? _interactions;
    private CheckBox? _interruptCombat;
    private CheckBox? _interruptConversation;
    private CheckBox? _interruptMovement;
    private OptionButton? _resumeMode;
    private SpinBox? _resumeDelay;
    private LineEdit? _templateId;
    private SpinBox? _seed;
    private SpinBox? _targetLevel;
    private CheckBox? _applyOnSave;
    private CheckBox? _manualLock;
    private TextEdit? _trackRules;
    private TextEdit? _skillRules;
    private TextEdit? _talentRules;
    private TextEdit? _overrides;
    private BlueprintId? _selectedBlueprintId;
    private Guid _draftProfileId;
    private Guid _draftTemplateId;
    private bool _lastCommandSucceeded;

    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public BlueprintId? SelectedBlueprintId => _selectedBlueprintId;
    public int SummarySectionCount { get; private set; }

    public void Initialize(
        Func<ProjectState?> load,
        Func<BlueprintId, CreatureRoleBehaviorProfile, bool, CommandResult> mutate,
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
        Title = "Rôle, activité & auto-level · OpenRPG Forge";
        MinSize = new Vector2I(1180, 760);
        Size = new Vector2I(1420, 900);
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
        var progression = SelectedBlueprint()?.CreatureProgression;
        if (_selectedBlueprintId is null || progression is null || progression.Tracks.Count == 0) return false;
        _runtimeRequired!.ButtonPressed = true;
        _ruleset!.Text = progression.RulesetId;
        _lore!.Text = "deity=deity.embers\noccupation=crypt-warden";
        _roleTags!.Text = "guardian, mystic";
        _graphId!.Text = "ambient.warden";
        _idleSets!.Text = "idle.watch|idle,look-around|1|2|6\nidle.inspect|inspect,warm-hands|0.5|3|8";
        _schedules!.Text = "night-watch|1200|360|idle.watch||";
        _interactions!.Text = "inspect-brazier|brazier|inspect|12";
        _interruptCombat!.ButtonPressed = true;
        _interruptConversation!.ButtonPressed = true;
        _interruptMovement!.ButtonPressed = true;
        Select(_resumeMode!, AmbientResumeMode.Resume);
        _resumeDelay!.Value = .5;
        _templateId!.Text = "warden-balanced";
        _seed!.Value = 42;
        _targetLevel!.Value = Math.Max(progression.Tracks.Count, progression.Tracks.Sum(item => item.Level) + 2);
        _applyOnSave!.ButtonPressed = true;
        _manualLock!.ButtonPressed = false;
        _trackRules!.Text = string.Join('\n', progression.Tracks.Select((item, index) =>
            $"{item.TrackId}|{(index == 0 ? 2 : 1)}"));
        _skillRules!.Text = string.Join('\n', progression.Skills.Select(item =>
            $"{item.SkillId}|2|{item.MaximumRank}"));
        _talentRules!.Text = string.Join('\n', progression.Talents.Select(item => $"{item.TalentId}|2"));
        _overrides!.Text = "ambient.radius=4";
        RebuildSummary();
        return true;
    }

    public bool PreviewForSmokeTest() { Mutate(true); return _lastCommandSucceeded; }
    public bool ApplyForSmokeTest() { Mutate(false); return _lastCommandSucceeded; }

    public void Refresh()
    {
        var state = _load?.Invoke();
        var previous = _selectedBlueprintId;
        _creatures?.Clear();
        if (state is null) { SetEmpty("Aucun projet ouvert."); return; }
        var candidates = state.Project.Blueprints
            .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .OrderBy(item => item.CreatureProgression is null ? 1 : 0)
            .ThenBy(item => item.IsReadOnly())
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        foreach (var blueprint in candidates)
        {
            var graph = blueprint.CreatureRoleBehavior?.Ambient.GraphId ?? "non configuré";
            var index = _creatures!.AddItem($"{blueprint.DisplayName}\n{blueprint.Slug} · {graph}");
            _creatures.SetItemMetadata(index, blueprint.Id.ToString());
        }
        if (candidates.Length == 0) { SetEmpty("Aucune créature dans ce module."); return; }
        var selected = Array.FindIndex(candidates, item => item.Id == previous);
        if (selected < 0) selected = 0;
        _creatures!.Select(selected);
        LoadBlueprint(candidates[selected]);
    }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 22);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 12); margin.AddChild(root);
        var title = new Label { Text = "RÔLE, ACTIVITÉ & AUTO-LEVEL" };
        title.AddThemeFontSizeOverride("font_size", 25);
        title.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(title);
        root.AddChild(new Label { Text = "Décrivez le rôle sans règle implicite, orchestrez les routines et prévisualisez une progression reproductible." });
        _status = new Label(); _status.AddThemeColorOverride("font_color", new Color("8ce0bd")); root.AddChild(_status);

        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [260]
        };
        root.AddChild(split);
        var catalog = Panel("CRÉATURES DU MODULE"); split.AddChild(catalog);
        _creatures = new ItemList { CustomMinimumSize = new Vector2(260, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _creatures.ItemSelected += OnCreatureSelected; catalog.GetChild<VBoxContainer>(0).AddChild(_creatures);

        var editor = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SplitOffsets = [720] };
        split.AddChild(editor);
        var formPanel = Panel("PROFIL CANONIQUE"); editor.AddChild(formPanel);
        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        formPanel.GetChild<VBoxContainer>(0).AddChild(tabs);
        BuildRoleTab(tabs); BuildAmbientTab(tabs); BuildAutoLevelTab(tabs);

        var summaryPanel = Panel("APERÇU & DIFF"); editor.AddChild(summaryPanel);
        var summaryColumn = summaryPanel.GetChild<VBoxContainer>(0);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(340, 410)
        };
        summaryColumn.AddChild(_summary);
        summaryColumn.AddChild(Button("Actualiser l’aperçu", RebuildSummary));
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new Vector2(340, 145) };
        summaryColumn.AddChild(_feedback);

        var actions = new HBoxContainer(); root.AddChild(actions);
        actions.AddChild(Button("Prévisualiser la transaction", () => Mutate(true)));
        actions.AddChild(Button("Appliquer au projet", () => Mutate(false), true));
        actions.AddChild(Button("Annuler", () => { _undo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Rétablir", () => { _redo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Fermer", Hide));
    }

    private void BuildRoleTab(TabContainer tabs)
    {
        var form = Tab(tabs, "RÔLE & LORE");
        _runtimeRequired = new CheckBox { Text = "Profil requis par le runtime" }; form.AddChild(_runtimeRequired);
        _ruleset = Field(form, "Template de ruleset", "forge.generic");
        _lore = Multi(form, "Champs de lore — clé=valeur, sans effet mécanique implicite", 220);
        _roleTags = Field(form, "Tags de rôle séparés par des virgules", "guardian, merchant");
        _overrides = Multi(form, "Overrides explicites — clé=valeur", 110);
    }

    private void BuildAmbientTab(TabContainer tabs)
    {
        var form = Tab(tabs, "ACTIVITÉ AMBIANTE");
        _graphId = Field(form, "Identifiant du graphe", "ambient.standard");
        _idleSets = Multi(form, "set|animations séparées par ,|poids|min s|max s", 135);
        _schedules = Multi(form, "horaire|début min|fin min|set|waypoint|conversation", 115);
        _interactions = Multi(form, "interaction|cible|animation|cooldown s", 90);
        _interruptCombat = new CheckBox { Text = "Interrompre en combat" }; form.AddChild(_interruptCombat);
        _interruptConversation = new CheckBox { Text = "Interrompre en conversation" }; form.AddChild(_interruptConversation);
        _interruptMovement = new CheckBox { Text = "Interrompre lors d’un déplacement" }; form.AddChild(_interruptMovement);
        form.AddChild(new Label { Text = "Reprise après interruption" });
        _resumeMode = EnumSelect<AmbientResumeMode>(); form.AddChild(_resumeMode);
        _resumeDelay = Spin(form, "Délai de reprise (secondes)", 0, 86400, .5, .1);
    }

    private void BuildAutoLevelTab(TabContainer tabs)
    {
        var form = Tab(tabs, "AUTO-LEVEL");
        _templateId = Field(form, "Identifiant du template", "progression.manual");
        _seed = Spin(form, "Seed déterministe", int.MinValue, int.MaxValue, 1, 1);
        _targetLevel = Spin(form, "Niveau total cible", 1, 999, 1, 1);
        _applyOnSave = new CheckBox { Text = "Appliquer le résultat lors de la transaction" }; form.AddChild(_applyOnSave);
        _manualLock = new CheckBox { Text = "Verrouillage manuel — aucun changement automatique" }; form.AddChild(_manualLock);
        _trackRules = Multi(form, "piste|poids", 105);
        _skillRules = Multi(form, "compétence|un rang tous les N niveaux|max", 105);
        _talentRules = Multi(form, "talent|niveau de déverrouillage", 90);
    }

    private void LoadBlueprint(BlueprintDefinition blueprint)
    {
        _selectedBlueprintId = blueprint.Id;
        var profile = blueprint.CreatureRoleBehavior ?? CreatureRoleBehaviorProfile.CreateDefault(
            Guid.NewGuid(), "toolset:creature-role-behavior");
        _draftProfileId = profile.Id;
        _draftTemplateId = profile.AutoLevel.Id;
        _runtimeRequired!.ButtonPressed = profile.RequiredAtRuntime;
        _ruleset!.Text = profile.Role.RulesetTemplateId;
        _lore!.Text = string.Join('\n', profile.Role.LoreFields.Select(item => $"{item.Key}={item.Value}"));
        _roleTags!.Text = string.Join(", ", profile.Role.RoleTags);
        _graphId!.Text = profile.Ambient.GraphId;
        _idleSets!.Text = string.Join('\n', profile.Ambient.IdleSets.Select(item =>
            $"{item.SetId}|{string.Join(',', item.AnimationIds)}|{Invariant(item.Weight)}|{Invariant(item.MinimumSeconds)}|{Invariant(item.MaximumSeconds)}"));
        _schedules!.Text = string.Join('\n', profile.Ambient.Schedules.Select(item =>
            $"{item.ScheduleId}|{item.StartMinute}|{item.EndMinute}|{item.IdleSetId}|{item.WaypointId}|{item.ConversationId}"));
        _interactions!.Text = string.Join('\n', profile.Ambient.Interactions.Select(item =>
            $"{item.InteractionId}|{item.TargetId}|{item.AnimationId}|{Invariant(item.CooldownSeconds)}"));
        _interruptCombat!.ButtonPressed = profile.Ambient.Interruption.InterruptOnCombat;
        _interruptConversation!.ButtonPressed = profile.Ambient.Interruption.InterruptOnConversation;
        _interruptMovement!.ButtonPressed = profile.Ambient.Interruption.InterruptOnMovement;
        Select(_resumeMode!, profile.Ambient.Interruption.ResumeMode);
        _resumeDelay!.Value = profile.Ambient.Interruption.ResumeDelaySeconds;
        _templateId!.Text = profile.AutoLevel.TemplateId;
        _seed!.Value = profile.AutoLevel.Seed;
        _targetLevel!.Value = profile.AutoLevel.TargetLevel;
        _applyOnSave!.ButtonPressed = profile.AutoLevel.ApplyOnSave;
        _manualLock!.ButtonPressed = profile.AutoLevel.ManualLock;
        _trackRules!.Text = string.Join('\n', profile.AutoLevel.Tracks.Select(item => $"{item.TrackId}|{item.Weight}"));
        _skillRules!.Text = string.Join('\n', profile.AutoLevel.Skills.Select(item =>
            $"{item.SkillId}|{item.RankEveryLevels}|{item.MaximumRank}"));
        _talentRules!.Text = string.Join('\n', profile.AutoLevel.Talents.Select(item => $"{item.TalentId}|{item.UnlockLevel}"));
        _overrides!.Text = string.Join('\n', profile.Overrides.Select(item => $"{item.Key}={item.Value}"));
        _status!.Text = $"{blueprint.DisplayName} · UUID {profile.Id:D} · schéma v{profile.SchemaVersion} · {profile.Provenance}";
        _feedback!.Text = blueprint.CreatureRoleBehavior is null
            ? "[color=#b8a8ce]État vide : définissez le rôle, l’activité et le template.[/color]"
            : "[color=#8ce0bd]Profil canonique chargé.[/color]";
        RebuildSummary();
    }

    private void Mutate(bool dryRun)
    {
        _lastCommandSucceeded = false; LastCommandId = Guid.Empty; LastPatchSummary = string.Empty; LastDiagnostics = string.Empty;
        if (_selectedBlueprintId is null || _mutate is null || _feedback is null) return;
        try
        {
            var result = _mutate(_selectedBlueprintId, BuildProfile(), dryRun);
            LastCommandId = result.Audit.CommandId.Value; LastPatchSummary = result.Patch?.Summary ?? string.Empty;
            if (!result.Succeeded)
            {
                LastDiagnostics = string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
                _feedback.Text = $"[color=#ff7777]Configuration refusée[/color]\n{string.Join("\n", result.Diagnostics.Select(item => $"{item.Code} · {item.Message}"))}";
                return;
            }
            _lastCommandSucceeded = true;
            _feedback.Text = dryRun
                ? $"[color=#ffbd7a]APERÇU — aucune donnée modifiée[/color]\n{result.Patch?.Summary}\nInverse : {result.Inverse?.Summary}"
                : $"[color=#8ce0bd]APPLIQUÉ AU PROJET[/color]\n{result.Patch?.Summary}\nRévision {result.NewRevision} · sauvegardez pour écrire sur disque";
            if (!dryRun) Refresh();
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            LastDiagnostics = exception.Message;
            _feedback.Text = $"[color=#ff7777]Erreur de formulaire[/color]\n{exception.Message}";
        }
    }

    private CreatureRoleBehaviorProfile BuildProfile() => new(
        _draftProfileId, CreatureRoleBehaviorProfile.CurrentSchemaVersion, "toolset:creature-role-behavior",
        _runtimeRequired!.ButtonPressed,
        new(_ruleset!.Text, ParseMap(_lore!.Text), Csv(_roleTags!.Text)),
        new(_graphId!.Text,
            Rows(_idleSets!.Text, 5, "idle").Select((parts, index) => new CreatureRoleBehaviorProfile.AmbientIdleSet(
                StableId(_draftProfileId, "idle", parts[0], index), parts[0], Csv(parts[1]), Number(parts[2], "poids"),
                Number(parts[3], "minimum"), Number(parts[4], "maximum"))).ToArray(),
            Rows(_schedules!.Text, 6, "horaire").Select((parts, index) => new CreatureRoleBehaviorProfile.AmbientScheduleEntry(
                StableId(_draftProfileId, "schedule", parts[0], index), parts[0], Integer(parts[1], "début"),
                Integer(parts[2], "fin"), parts[3], Empty(parts[4]), Empty(parts[5]))).ToArray(),
            Rows(_interactions!.Text, 4, "interaction").Select((parts, index) => new CreatureRoleBehaviorProfile.AmbientInteraction(
                StableId(_draftProfileId, "interaction", parts[0], index), parts[0], parts[1], parts[2],
                Number(parts[3], "cooldown"))).ToArray(),
            new(_interruptCombat!.ButtonPressed, _interruptConversation!.ButtonPressed,
                _interruptMovement!.ButtonPressed, (AmbientResumeMode)_resumeMode!.Selected, _resumeDelay!.Value)),
        new(_draftTemplateId, _templateId!.Text, checked((int)_seed!.Value), checked((int)_targetLevel!.Value),
            _applyOnSave!.ButtonPressed, _manualLock!.ButtonPressed,
            Rows(_trackRules!.Text, 2, "piste").Select(parts =>
                new CreatureRoleBehaviorProfile.TrackAllocation(parts[0], Integer(parts[1], "poids"))).ToArray(),
            Rows(_skillRules!.Text, 3, "compétence").Select(parts =>
                new CreatureRoleBehaviorProfile.SkillAllocation(parts[0], Integer(parts[1], "fréquence"), Integer(parts[2], "maximum"))).ToArray(),
            Rows(_talentRules!.Text, 2, "talent").Select(parts =>
                new CreatureRoleBehaviorProfile.TalentUnlock(parts[0], Integer(parts[1], "niveau"))).ToArray()),
        ParseMap(_overrides!.Text));

    private void RebuildSummary()
    {
        if (_summary is null) return;
        try
        {
            var profile = BuildProfile();
            var progression = SelectedBlueprint()?.CreatureProgression;
            var diagnostics = CreatureRoleBehaviorValidator.Validate(profile, progression);
            var preview = diagnostics.Count == 0
                ? CreatureRoleBehaviorRuntimeResolver.PreviewAutoLevel(profile, progression)
                : null;
            var runtime = diagnostics.Count == 0
                ? CreatureRoleBehaviorRuntimeResolver.Resolve(profile, progression, 21 * 60, false)
                : null;
            SummarySectionCount = 3;
            _summary.Text =
                $"[font_size=22][color=#ff9858]{profile.Role.RoleTags.Count} rôle(s) · {profile.Ambient.IdleSets.Count} idle set(s)[/color][/font_size]\n" +
                $"[color=#8ce0bd]UUID stable · schéma v{profile.SchemaVersion} · seed {profile.AutoLevel.Seed}[/color]\n\n" +
                $"[b]RÔLE & LORE[/b]\nRuleset {profile.Role.RulesetTemplateId}\n{Lines(profile.Role.LoreFields.Select(item => $"{item.Key} = {item.Value}"))}\n\n" +
                $"[b]ACTIVITÉ À 21:00[/b]\n{runtime?.ActiveIdleSetId ?? "indisponible"} · animation {runtime?.ActiveAnimationId ?? "aucune"}\n" +
                $"{profile.Ambient.Schedules.Count} horaire(s) · {profile.Ambient.Interactions.Count} interaction(s) · reprise {profile.Ambient.Interruption.ResumeMode}\n\n" +
                $"[b]AUTO-LEVEL[/b]\n{profile.AutoLevel.TemplateId} · cible {profile.AutoLevel.TargetLevel} · " +
                $"{(profile.AutoLevel.ManualLock ? "verrouillé" : $"{preview?.Changes.Count ?? 0} changement(s)")}\n" +
                Lines(preview?.Changes.Take(8).Select(item => $"{item.Path}: {item.Before} → {item.After}") ?? []) + "\n\n" +
                (diagnostics.Count == 0 ? "[color=#8ce0bd]✓ Profil valide et aperçu déterministe[/color]"
                    : $"[color=#ff7777]⚠ {diagnostics.Count} diagnostic(s)[/color]\n{Lines(diagnostics.Select(item => $"{item.Code} · {item.Path}"))}");
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            SummarySectionCount = 0;
            _summary.Text = $"[color=#ff7777]Formulaire incomplet[/color]\n{exception.Message}";
        }
    }

    private BlueprintDefinition? SelectedBlueprint() => _load?.Invoke()?.Project.Blueprints
        .SingleOrDefault(item => item.Id == _selectedBlueprintId);

    private void OnCreatureSelected(long index)
    {
        if (_creatures is null || _load?.Invoke() is not { } state) return;
        var id = BlueprintId.Parse(_creatures.GetItemMetadata((int)index).AsString());
        LoadBlueprint(state.Project.Blueprints.Single(item => item.Id == id));
    }

    private void SetEmpty(string message)
    {
        _selectedBlueprintId = null; _status!.Text = message;
        _feedback!.Text = $"[color=#b8a8ce]{message}[/color]";
        if (_summary is not null) _summary.Text = "[color=#b8a8ce]Sélectionnez une créature.[/color]";
        SummarySectionCount = 0;
    }

    private static Guid StableId(Guid profileId, string group, string value, int index)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{profileId:D}|{group}|{value}|{index}"));
        return new Guid(bytes[..16]);
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

    private static IReadOnlyDictionary<string, string> ParseMap(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !result.TryAdd(parts[0], parts[1]))
                throw new FormatException($"Entrée clé=valeur invalide : '{line}'.");
        }
        return result;
    }

    private static string[] Csv(string text) => text.Split(',',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static int Integer(string text, string label) => int.TryParse(text,
        System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value)
        ? value : throw new FormatException($"{label} invalide : '{text}'.");
    private static double Number(string text, string label) => double.TryParse(text,
        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
        ? value : throw new FormatException($"{label} invalide : '{text}'.");
    private static string? Empty(string text) => string.IsNullOrWhiteSpace(text) ? null : text;
    private static string Invariant(double value) => value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
    private static OptionButton EnumSelect<T>() where T : struct, Enum { var result = new OptionButton(); foreach (var value in Enum.GetValues<T>()) result.AddItem(value.ToString()); return result; }
    private static void Select<T>(OptionButton control, T value) where T : struct, Enum => control.Select(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
    private static PanelContainer Panel(string title) { var panel = new PanelContainer(); var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 9); panel.AddChild(column); var label = new Label { Text = title }; label.AddThemeColorOverride("font_color", new Color("ffad72")); column.AddChild(label); return panel; }
    private static VBoxContainer Tab(TabContainer tabs, string title) { var scroll = new ScrollContainer { Name = title, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; form.AddThemeConstantOverride("separation", 8); scroll.AddChild(form); tabs.AddChild(scroll); return form; }
    private static LineEdit Field(VBoxContainer parent, string label, string placeholder) { parent.AddChild(new Label { Text = label }); var field = new LineEdit { PlaceholderText = placeholder }; parent.AddChild(field); return field; }
    private static TextEdit Multi(VBoxContainer parent, string label, float height) { parent.AddChild(new Label { Text = label }); var field = new TextEdit { CustomMinimumSize = new Vector2(0, height) }; parent.AddChild(field); return field; }
    private static SpinBox Spin(VBoxContainer parent, string label, double min, double max, double value, double step) { parent.AddChild(new Label { Text = label }); var field = new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value }; parent.AddChild(field); return field; }
    private static Button Button(string text, Action pressed, bool primary = false) { var button = new Button { Text = text }; if (primary) button.AddThemeColorOverride("font_color", new Color("fff1e8")); button.Pressed += pressed; return button; }
    private static string Lines(IEnumerable<string> values) { var items = values.ToArray(); return items.Length == 0 ? "[color=#776b88]Aucun[/color]" : string.Join('\n', items.Select(item => $"• {item}")); }
}
#endif
