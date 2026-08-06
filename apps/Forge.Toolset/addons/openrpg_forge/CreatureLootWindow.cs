#if TOOLS
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class CreatureLootWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<BlueprintId, CreatureLootProfile, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private ItemList? _creatures;
    private Label? _status;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;
    private CheckBox? _runtimeRequired;
    private TextEdit? _entries;
    private TextEdit? _overrides;
    private CheckBox? _theftEnabled;
    private LineEdit? _theftSkill;
    private LineEdit? _perceptionSkill;
    private SpinBox? _theftDifficulty;
    private SpinBox? _perceptionDifficulty;
    private CheckBox? _serverConsent;
    private LineEdit? _crime;
    private SpinBox? _cooldown;
    private LineEdit? _audit;
    private OptionButton? _corpseMode;
    private SpinBox? _lifetime;
    private LineEdit? _container;
    private LineEdit? _ragdoll;
    private CheckBox? _resurrection;
    private LineEdit? _corpseOwnership;
    private SpinBox? _maximumItems;
    private OptionButton? _cleanupMode;
    private BlueprintId? _selectedBlueprintId;
    private Guid _draftLootId;
    private bool _lastCommandSucceeded;

    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public BlueprintId? SelectedBlueprintId => _selectedBlueprintId;
    public int SummarySectionCount { get; private set; }

    public void Initialize(
        Func<ProjectState?> load,
        Func<BlueprintId, CreatureLootProfile, bool, CommandResult> mutate,
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
        Title = "Butin, vol & cadavres · OpenRPG Forge";
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
        var itemSlugs = blueprint?.Combat?.Loot.Select(item => item.ItemSlug).ToArray() ?? [];
        var skillIds = blueprint?.CreatureProgression?.Skills.Select(item => item.SkillId).ToArray() ?? [];
        if (_selectedBlueprintId is null || itemSlugs.Length == 0 || skillIds.Length < 2) return false;
        _runtimeRequired!.ButtonPressed = true;
        _entries!.Text = string.Join('\n', itemSlugs.Select((slug, index) =>
            $"{slug}|{(index == 0 ? "relic" : "pouch")}|1|{Math.Max(1, blueprint!.Combat!.Loot[index].Quantity)}|{(index == 0 ? "1" : "0.5")}|true|true|{(index == 0 ? LootOwnershipMode.Party : LootOwnershipMode.Killer)}|{(index == 0 ? LootInstancingMode.PerParty : LootInstancingMode.PerPlayer)}|"));
        _theftEnabled!.ButtonPressed = true;
        _theftSkill!.Text = skillIds[0];
        _perceptionSkill!.Text = skillIds[1];
        _theftDifficulty!.Value = 12;
        _perceptionDifficulty!.Value = 10;
        _serverConsent!.ButtonPressed = true;
        _crime!.Text = "crime.theft";
        _cooldown!.Value = 30;
        _audit!.Text = "loot.pickpocket";
        Select(_corpseMode!, CreatureCorpseMode.RagdollContainer);
        _lifetime!.Value = 60;
        _container!.Text = "corpse.standard";
        _ragdoll!.Text = "ragdoll.humanoid";
        _resurrection!.ButtonPressed = true;
        _corpseOwnership!.Text = "party";
        _maximumItems!.Value = 8;
        Select(_cleanupMode!, CorpseCleanupMode.Timed);
        _overrides!.Text = "sparkle=rare-only";
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
            .OrderBy(item => item.Combat?.Loot.Count > 0 ? 0 : 1)
            .ThenBy(item => item.IsReadOnly())
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        foreach (var blueprint in candidates)
        {
            var count = blueprint.CreatureLoot?.Entries.Count ?? 0;
            var index = _creatures!.AddItem($"{blueprint.DisplayName}\n{blueprint.Slug} · {count} règle(s) de butin");
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
        var title = new Label { Text = "BUTIN, VOL & CADAVRES" };
        title.AddThemeFontSizeOverride("font_size", 25);
        title.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(title);
        root.AddChild(new Label { Text = "Définissez ce qui tombe, qui peut le prendre, comment le voler et ce que le corps conserve." });
        _status = new Label(); _status.AddThemeColorOverride("font_color", new Color("8ce0bd")); root.AddChild(_status);

        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [260]
        }; root.AddChild(split);
        var catalog = Panel("CRÉATURES DU MODULE"); split.AddChild(catalog);
        _creatures = new ItemList { CustomMinimumSize = new Vector2(260, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _creatures.ItemSelected += OnCreatureSelected; catalog.GetChild<VBoxContainer>(0).AddChild(_creatures);

        var editor = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SplitOffsets = [680] };
        split.AddChild(editor);
        var formPanel = Panel("POLITIQUE CANONIQUE"); editor.AddChild(formPanel);
        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        formPanel.GetChild<VBoxContainer>(0).AddChild(tabs);
        BuildLootTab(tabs); BuildTheftTab(tabs); BuildCorpseTab(tabs);

        var summaryPanel = Panel("RÉSUMÉ JOUABLE"); editor.AddChild(summaryPanel);
        var summaryColumn = summaryPanel.GetChild<VBoxContainer>(0);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(320, 390)
        }; summaryColumn.AddChild(_summary);
        summaryColumn.AddChild(Button("Actualiser le résumé", RebuildSummary));
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new Vector2(320, 150) };
        summaryColumn.AddChild(_feedback);

        var actions = new HBoxContainer(); root.AddChild(actions);
        actions.AddChild(Button("Prévisualiser la transaction", () => Mutate(true)));
        actions.AddChild(Button("Appliquer au projet", () => Mutate(false), true));
        actions.AddChild(Button("Annuler", () => { _undo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Rétablir", () => { _redo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Fermer", Hide));
    }

    private void BuildLootTab(TabContainer tabs)
    {
        var form = Tab(tabs, "BUTIN");
        _runtimeRequired = new CheckBox { Text = "Profil requis par le runtime" }; form.AddChild(_runtimeRequired);
        form.AddChild(Help("Une ligne par règle. L’objet doit exister dans la table Combat de la créature."));
        _entries = Multi(form, "objet|slot|min|max|chance|mort|volable|propriétaire|instance|propriétaire fixe", 330);
        _overrides = Multi(form, "Overrides explicites — clé=valeur", 90);
    }

    private void BuildTheftTab(TabContainer tabs)
    {
        var form = Tab(tabs, "VOL À LA TIRE");
        _theftEnabled = new CheckBox { Text = "Autoriser le vol à la tire" }; form.AddChild(_theftEnabled);
        _theftSkill = Field(form, "Compétence du voleur", "sleight");
        _perceptionSkill = Field(form, "Compétence de perception", "awareness");
        _theftDifficulty = Spin(form, "Difficulté du vol", 0, 999, 12);
        _perceptionDifficulty = Spin(form, "Seuil de détection", 0, 999, 10);
        _serverConsent = new CheckBox { Text = "Exiger le consentement du serveur" }; form.AddChild(_serverConsent);
        _crime = Field(form, "Crime déclenché en cas de détection", "crime.theft");
        _cooldown = Spin(form, "Délai entre deux tentatives (secondes)", 0, 86400, 30);
        _audit = Field(form, "Tag d’audit", "loot.pickpocket");
    }

    private void BuildCorpseTab(TabContainer tabs)
    {
        var form = Tab(tabs, "CADAVRE");
        form.AddChild(new Label { Text = "Mode du cadavre" }); _corpseMode = EnumSelect<CreatureCorpseMode>(); form.AddChild(_corpseMode);
        _lifetime = Spin(form, "Durée de vie (secondes)", 0, 86400, 60);
        _container = Field(form, "Profil de conteneur", "corpse.standard");
        _ragdoll = Field(form, "Profil de ragdoll", "ragdoll.humanoid");
        _resurrection = new CheckBox { Text = "Autoriser la résurrection" }; form.AddChild(_resurrection);
        _corpseOwnership = Field(form, "Politique de propriété", "party");
        _maximumItems = Spin(form, "Nombre maximal d’objets", 0, 4096, 8);
        form.AddChild(new Label { Text = "Nettoyage" }); _cleanupMode = EnumSelect<CorpseCleanupMode>(); form.AddChild(_cleanupMode);
    }

    private void LoadBlueprint(BlueprintDefinition blueprint)
    {
        _selectedBlueprintId = blueprint.Id;
        var profile = blueprint.CreatureLoot ?? CreatureLootProfile.CreateDefault(Guid.NewGuid(), "toolset:creature-loot");
        _draftLootId = profile.Id;
        _runtimeRequired!.ButtonPressed = profile.RequiredAtRuntime;
        _entries!.Text = string.Join('\n', profile.Entries.Select(item =>
            $"{item.ItemSlug}|{item.SlotId}|{item.MinimumQuantity}|{item.MaximumQuantity}|{Invariant(item.Chance)}|{item.DropOnDeath}|{item.Pickpocketable}|{item.Ownership}|{item.Instancing}|{item.FixedOwnerId}"));
        _theftEnabled!.ButtonPressed = profile.Theft.Enabled;
        _theftSkill!.Text = profile.Theft.SkillId; _perceptionSkill!.Text = profile.Theft.PerceptionSkillId;
        _theftDifficulty!.Value = profile.Theft.Difficulty; _perceptionDifficulty!.Value = profile.Theft.PerceptionDifficulty;
        _serverConsent!.ButtonPressed = profile.Theft.RequireServerConsent; _crime!.Text = profile.Theft.CrimeId;
        _cooldown!.Value = profile.Theft.CooldownSeconds; _audit!.Text = profile.Theft.AuditTag;
        Select(_corpseMode!, profile.Corpse.Mode); _lifetime!.Value = profile.Corpse.LifetimeSeconds;
        _container!.Text = profile.Corpse.ContainerId ?? string.Empty; _ragdoll!.Text = profile.Corpse.RagdollProfileId ?? string.Empty;
        _resurrection!.ButtonPressed = profile.Corpse.AllowResurrection; _corpseOwnership!.Text = profile.Corpse.OwnershipPolicy;
        _maximumItems!.Value = profile.Corpse.MaximumItems; Select(_cleanupMode!, profile.Corpse.CleanupMode);
        _overrides!.Text = string.Join('\n', profile.Overrides.Select(item => $"{item.Key}={item.Value}"));
        _status!.Text = $"{blueprint.DisplayName} · UUID {profile.Id:D} · schéma v{profile.SchemaVersion} · {profile.Provenance}";
        _feedback!.Text = blueprint.CreatureLoot is null
            ? "[color=#b8a8ce]État vide : définissez une politique puis prévisualisez-la.[/color]"
            : "[color=#8ce0bd]Politique de butin canonique chargée.[/color]";
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
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            LastDiagnostics = exception.Message;
            _feedback.Text = $"[color=#ff7777]Erreur de formulaire[/color]\n{exception.Message}";
        }
    }

    private CreatureLootProfile BuildProfile() => new(
        _draftLootId, CreatureLootProfile.CurrentSchemaVersion, "toolset:creature-loot", _runtimeRequired!.ButtonPressed,
        Rows(_entries!.Text, 10, "butin").Select((parts, index) => new CreatureLootProfile.LootEntryPolicy(
            StableEntryId(_draftLootId, parts[0], parts[1], index), parts[0], Empty(parts[1]), Integer(parts[2], "minimum"),
            Integer(parts[3], "maximum"), Number(parts[4], "chance"), Boolean(parts[5], "mort"),
            Boolean(parts[6], "volable"), Enum.Parse<LootOwnershipMode>(parts[7]),
            Enum.Parse<LootInstancingMode>(parts[8]), Empty(parts[9]))).ToArray(),
        new(_theftEnabled!.ButtonPressed, _theftSkill!.Text, _perceptionSkill!.Text, _theftDifficulty!.Value,
            _perceptionDifficulty!.Value, _serverConsent!.ButtonPressed, _crime!.Text, _cooldown!.Value, _audit!.Text),
        new((CreatureCorpseMode)_corpseMode!.Selected, _lifetime!.Value, Empty(_container!.Text), Empty(_ragdoll!.Text),
            _resurrection!.ButtonPressed, _corpseOwnership!.Text, checked((int)_maximumItems!.Value),
            (CorpseCleanupMode)_cleanupMode!.Selected), ParseOverrides(_overrides!.Text));

    private void RebuildSummary()
    {
        if (_summary is null) return;
        try
        {
            var profile = BuildProfile(); var blueprint = SelectedBlueprint();
            var items = (blueprint?.Combat?.Loot ?? []).Select(item => item.ItemSlug).ToHashSet(StringComparer.Ordinal);
            var skills = (blueprint?.CreatureProgression?.Skills ?? []).Select(item => item.SkillId).ToHashSet(StringComparer.Ordinal);
            var diagnostics = CreatureLootValidator.Validate(profile, availableItemSlugs: items, availableSkillIds: skills);
            SummarySectionCount = 3;
            _summary.Text =
                $"[font_size=22][color=#ff9858]{profile.Entries.Count} règle(s) de butin[/color][/font_size]\n" +
                $"[color=#8ce0bd]{profile.Entries.Count(item => item.DropOnDeath)} à la mort · {profile.Entries.Count(item => item.Pickpocketable)} volable(s)[/color]\n\n" +
                $"[b]BUTIN & PROPRIÉTÉ[/b]\n{Lines(profile.Entries.Select(item => $"{item.ItemSlug} · {item.MinimumQuantity}-{item.MaximumQuantity} · {item.Chance:P0} · {item.Ownership}/{item.Instancing}"))}\n\n" +
                $"[b]VOL À LA TIRE[/b]\n{(profile.Theft.Enabled ? $"{profile.Theft.SkillId} contre {profile.Theft.PerceptionSkillId} · DD {profile.Theft.Difficulty:G} · délai {profile.Theft.CooldownSeconds:G}s" : "Désactivé")}\n\n" +
                $"[b]CADAVRE[/b]\n{profile.Corpse.Mode} · {profile.Corpse.LifetimeSeconds:G}s · {profile.Corpse.CleanupMode} · résurrection {(profile.Corpse.AllowResurrection ? "oui" : "non")}\n\n" +
                (diagnostics.Count == 0 ? "[color=#8ce0bd]✓ Politique valide et références résolues[/color]"
                    : $"[color=#ff7777]⚠ {diagnostics.Count} diagnostic(s)[/color]\n{Lines(diagnostics.Select(item => $"{item.Code} · {item.Path}"))}");
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            SummarySectionCount = 0; _summary.Text = $"[color=#ff7777]Formulaire incomplet[/color]\n{exception.Message}";
        }
    }

    private BlueprintDefinition? SelectedBlueprint() => _load?.Invoke()?.Project.Blueprints.SingleOrDefault(item => item.Id == _selectedBlueprintId);
    private void OnCreatureSelected(long index)
    {
        if (_creatures is null || _load?.Invoke() is not { } state) return;
        var id = BlueprintId.Parse(_creatures.GetItemMetadata((int)index).AsString());
        LoadBlueprint(state.Project.Blueprints.Single(item => item.Id == id));
    }
    private void SetEmpty(string message)
    {
        _selectedBlueprintId = null; _status!.Text = message; _feedback!.Text = $"[color=#b8a8ce]{message}[/color]";
        if (_summary is not null) _summary.Text = "[color=#b8a8ce]Sélectionnez une créature.[/color]";
        SummarySectionCount = 0;
    }
    private static Guid StableEntryId(Guid profileId, string item, string slot, int index)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{profileId:D}|{item}|{slot}|{index}"));
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
    private static IReadOnlyDictionary<string, string> ParseOverrides(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !result.TryAdd(parts[0], parts[1])) throw new FormatException($"Override invalide : '{line}'.");
        }
        return result;
    }
    private static int Integer(string text, string label) => int.TryParse(text, System.Globalization.NumberStyles.Integer,
        System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : throw new FormatException($"{label} invalide : '{text}'.");
    private static double Number(string text, string label) => double.TryParse(text, System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : throw new FormatException($"{label} invalide : '{text}'.");
    private static bool Boolean(string text, string label) => bool.TryParse(text, out var value) ? value : throw new FormatException($"{label} invalide : '{text}'.");
    private static string? Empty(string text) => string.IsNullOrWhiteSpace(text) ? null : text;
    private static string Invariant(double value) => value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
    private static OptionButton EnumSelect<T>() where T : struct, Enum { var result = new OptionButton(); foreach (var value in Enum.GetValues<T>()) result.AddItem(value.ToString()); return result; }
    private static void Select<T>(OptionButton control, T value) where T : struct, Enum => control.Select(Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
    private static PanelContainer Panel(string title) { var panel = new PanelContainer(); var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 9); panel.AddChild(column); var label = new Label { Text = title }; label.AddThemeColorOverride("font_color", new Color("ffad72")); column.AddChild(label); return panel; }
    private static VBoxContainer Tab(TabContainer tabs, string title) { var scroll = new ScrollContainer { Name = title, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; form.AddThemeConstantOverride("separation", 8); scroll.AddChild(form); tabs.AddChild(scroll); return form; }
    private static Label Help(string text) { var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart }; label.AddThemeColorOverride("font_color", new Color("b8a8ce")); return label; }
    private static LineEdit Field(VBoxContainer parent, string label, string placeholder) { parent.AddChild(new Label { Text = label }); var field = new LineEdit { PlaceholderText = placeholder }; parent.AddChild(field); return field; }
    private static TextEdit Multi(VBoxContainer parent, string label, float height) { parent.AddChild(new Label { Text = label }); var field = new TextEdit { CustomMinimumSize = new Vector2(0, height) }; parent.AddChild(field); return field; }
    private static SpinBox Spin(VBoxContainer parent, string label, double min, double max, double value) { parent.AddChild(new Label { Text = label }); var field = new SpinBox { MinValue = min, MaxValue = max, Step = 0.5, Value = value }; parent.AddChild(field); return field; }
    private static Button Button(string text, Action pressed, bool primary = false) { var button = new Button { Text = text }; if (primary) button.AddThemeColorOverride("font_color", new Color("fff1e8")); button.Pressed += pressed; return button; }
    private static string Lines(IEnumerable<string> values) { var items = values.ToArray(); return items.Length == 0 ? "[color=#776b88]Aucun[/color]" : string.Join('\n', items.Select(item => $"• {item}")); }
}
#endif
