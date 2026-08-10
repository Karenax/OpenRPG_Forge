#if TOOLS
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Lifecycle;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class CreatureEventWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<BlueprintId, CreatureEventProfile, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private ItemList? _creatures;
    private Label? _status;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;
    private CheckBox? _runtimeRequired;
    private OptionButton? _combatMode;
    private LineEdit? _combatHandler;
    private LineEdit? _accessedHandler;
    private LineEdit? _transferredHandler;
    private LineEdit? _restHandler;
    private LineEdit? _beforeDamageHandler;
    private LineEdit? _damageAppliedHandler;
    private LineEdit? _deathHandler;
    private SpinBox? _beforeDamageBudget;
    private SpinBox? _maximumDamageAdjustment;
    private CheckBox? _heartbeatEnabled;
    private SpinBox? _heartbeatInterval;
    private SpinBox? _heartbeatBudget;
    private CheckBox? _suspendOutsideInterest;
    private LineEdit? _heartbeatHandler;
    private TextEdit? _customEvents;
    private TextEdit? _overrides;
    private BlueprintId? _selectedBlueprintId;
    private Guid _draftProfileId;
    private bool _lastCommandSucceeded;

    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public BlueprintId? SelectedBlueprintId => _selectedBlueprintId;
    public int SummarySectionCount { get; private set; }

    public void Initialize(
        Func<ProjectState?> load,
        Func<BlueprintId, CreatureEventProfile, bool, CommandResult> mutate,
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
        Title = "Événements de créature · OpenRPG Forge";
        MinSize = new Vector2I(1120, 720);
        Size = new Vector2I(1380, 860);
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
        if (_selectedBlueprintId is null) return false;
        _runtimeRequired!.ButtonPressed = true;
        Select(_combatMode!, CreatureCombatBoundaryMode.CombatTickCompleted);
        _combatHandler!.Text = "OnCombatTickCompleted";
        _accessedHandler!.Text = "OnInventoryAccessed";
        _transferredHandler!.Text = "OnItemTransferred";
        _restHandler!.Text = "OnRestCompleted";
        _beforeDamageHandler!.Text = "BeforeDamage";
        _damageAppliedHandler!.Text = "OnDamaged";
        _deathHandler!.Text = "OnDeath";
        _beforeDamageBudget!.Value = 4;
        _maximumDamageAdjustment!.Value = 0.5;
        _heartbeatEnabled!.ButtonPressed = true;
        _heartbeatInterval!.Value = 1500;
        _heartbeatBudget!.Value = 8;
        _suspendOutsideInterest!.ButtonPressed = true;
        _heartbeatHandler!.Text = "OnHeartbeat";
        _customEvents!.Text = "crypt|braziers_lit|1|true|count:Integer,source:EntityId|OnBraziersLit";
        _overrides!.Text = "telemetry.channel=ash-warden";
        RebuildSummary();
        return true;
    }

    public bool PreviewForSmokeTest() { Mutate(true); return _lastCommandSucceeded; }
    public bool ApplyForSmokeTest() { Mutate(false); return _lastCommandSucceeded; }
    public bool PrepareForCapture() => ConfigureForSmokeTest() && Visible;

    public void Refresh()
    {
        var state = _load?.Invoke();
        var previous = _selectedBlueprintId;
        _creatures?.Clear();
        if (state is null) { SetEmpty("Aucun projet ouvert."); return; }
        var candidates = state.Project.Blueprints
            .Where(item => item.Kind is BlueprintKind.Creature or BlueprintKind.PlayerSpawn)
            .OrderBy(item => item.IsReadOnly())
            .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        foreach (var blueprint in candidates)
        {
            var count = blueprint.CreatureEvents is null
                ? "non configuré"
                : $"{CreatureEventValidator.EnumerateBindings(blueprint.CreatureEvents).Count} bindings";
            var index = _creatures!.AddItem($"{blueprint.DisplayName}\n{blueprint.Slug} · {count}");
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
        var title = new Label { Text = "ÉVÉNEMENTS AVANCÉS DE CRÉATURE" };
        title.AddThemeFontSizeOverride("font_size", 25);
        title.AddThemeColorOverride("font_color", new Color("ff9858")); root.AddChild(title);
        root.AddChild(new Label { Text = "Configurez des bindings ForgeScript typés, budgétés et indépendants du moteur 3D." });
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
        BuildBindingsTab(tabs); BuildHeartbeatTab(tabs); BuildCustomTab(tabs);

        var summaryPanel = Panel("APERÇU & DIFF"); editor.AddChild(summaryPanel);
        var summaryColumn = summaryPanel.GetChild<VBoxContainer>(0);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(340, 390)
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

    private void BuildBindingsTab(TabContainer tabs)
    {
        var form = Tab(tabs, "BINDINGS");
        _runtimeRequired = new CheckBox { Text = "Profil requis par le runtime" }; form.AddChild(_runtimeRequired);
        form.AddChild(new Label { Text = "Frontière de combat" });
        _combatMode = EnumSelect<CreatureCombatBoundaryMode>(); form.AddChild(_combatMode);
        _combatHandler = Field(form, "Handler fin de tick/tour", "OnCombatTickCompleted");
        _accessedHandler = Field(form, "Handler inventaire consulté", "OnInventoryAccessed");
        _transferredHandler = Field(form, "Handler objet transféré", "OnItemTransferred");
        _restHandler = Field(form, "Handler repos terminé", "OnRestCompleted");
        form.AddChild(new Label { Text = "Dégâts et mort transactionnels" });
        _beforeDamageHandler = Field(form, "Hook restreint avant dégâts", "BeforeDamage");
        _damageAppliedHandler = Field(form, "Handler dégâts appliqués", "OnDamaged");
        _deathHandler = Field(form, "Handler mort", "OnDeath");
        _beforeDamageBudget = Spin(form, "Budget BeforeDamage (ms)", 1, 50, 4, 1);
        _maximumDamageAdjustment = Spin(form, "Ajustement maximal (ratio)", 0, 1, 0.5, 0.05);
        form.AddChild(new Label { Text = "ForgeScript API 1.0 · autorité serveur · UUID stables générés par le profil" });
    }

    private void BuildHeartbeatTab(TabContainer tabs)
    {
        var form = Tab(tabs, "HEARTBEAT");
        _heartbeatEnabled = new CheckBox { Text = "Activer le scheduler périodique" }; form.AddChild(_heartbeatEnabled);
        _heartbeatInterval = Spin(form, "Cadence (millisecondes)", 250, 86_400_000, 2_000, 250);
        _heartbeatBudget = Spin(form, "Budget maximal (millisecondes)", 1, 1_000, 8, 1);
        _suspendOutsideInterest = new CheckBox { Text = "Suspendre hors zone d’intérêt" }; form.AddChild(_suspendOutsideInterest);
        _heartbeatHandler = Field(form, "Handler périodique", "OnHeartbeat");
        form.AddChild(new Label { Text = "Le runtime borne le rattrapage à quatre appels et diagnostique tout backlog tronqué." });
    }

    private void BuildCustomTab(TabContainer tabs)
    {
        var form = Tab(tabs, "ÉVÉNEMENTS UTILISATEUR");
        _customEvents = Multi(form,
            "namespace|nom|version|required|champ:Type,...|handler — types String, Integer, Number, Boolean, EntityId, ItemId", 260);
        _overrides = Multi(form, "Overrides explicites — clé=valeur", 130);
    }

    private void LoadBlueprint(BlueprintDefinition blueprint)
    {
        _selectedBlueprintId = blueprint.Id;
        var profile = blueprint.CreatureEvents ?? CreatureEventProfile.CreateDefault(
            Guid.NewGuid(), "toolset:creature-events");
        _draftProfileId = profile.Id;
        _runtimeRequired!.ButtonPressed = profile.RequiredAtRuntime;
        var combat = profile.CombatBoundary ?? throw new InvalidOperationException("Combat binding missing.");
        Select(_combatMode!, combat.Mode);
        _combatHandler!.Text = combat.Binding.Handler;
        _accessedHandler!.Text = profile.Inventory?.Accessed.Handler ?? "OnInventoryAccessed";
        _transferredHandler!.Text = profile.Inventory?.Transferred.Handler ?? "OnItemTransferred";
        _restHandler!.Text = profile.Rest?.Completed.Handler ?? "OnRestCompleted";
        _beforeDamageHandler!.Text = profile.DamageDeath?.BeforeDamage.Handler ?? "BeforeDamage";
        _damageAppliedHandler!.Text = profile.DamageDeath?.DamageApplied.Handler ?? "OnDamaged";
        _deathHandler!.Text = profile.DamageDeath?.Died.Handler ?? "OnDeath";
        _beforeDamageBudget!.Value = profile.DamageDeath?.BeforeDamageBudgetMilliseconds ?? 4;
        _maximumDamageAdjustment!.Value = profile.DamageDeath?.MaximumAdjustmentRatio ?? 0.5;
        _heartbeatEnabled!.ButtonPressed = profile.Heartbeat?.Enabled == true;
        _heartbeatInterval!.Value = profile.Heartbeat?.IntervalMilliseconds ?? 2_000;
        _heartbeatBudget!.Value = profile.Heartbeat?.BudgetMilliseconds ?? 8;
        _suspendOutsideInterest!.ButtonPressed = profile.Heartbeat?.SuspendOutsideInterest != false;
        _heartbeatHandler!.Text = profile.Heartbeat?.Binding.Handler ?? "OnHeartbeat";
        _customEvents!.Text = string.Join('\n', profile.CustomEvents.Select(item =>
            $"{item.Namespace}|{item.Name}|{item.PayloadVersion}|{item.Required.ToString().ToLowerInvariant()}|" +
            $"{string.Join(',', item.Fields.Select(field => $"{field.Key}:{field.Value}"))}|{item.Binding.Handler}"));
        _overrides!.Text = string.Join('\n', profile.Overrides.Select(item => $"{item.Key}={item.Value}"));
        _status!.Text = $"{blueprint.DisplayName} · UUID {profile.Id:D} · schéma v{profile.SchemaVersion} · {profile.Provenance}";
        _feedback!.Text = blueprint.CreatureEvents is null
            ? "[color=#b8a8ce]État vide : configurez les bindings et budgets de cette créature.[/color]"
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

    private CreatureEventProfile BuildProfile()
    {
        var mode = (CreatureCombatBoundaryMode)_combatMode!.Selected;
        var combatEvent = mode == CreatureCombatBoundaryMode.TurnEnded
            ? "creature.turn_ended"
            : "creature.combat_tick_completed";
        return new CreatureEventProfile(
            _draftProfileId, CreatureEventProfile.CurrentSchemaVersion, "toolset:creature-events",
            _runtimeRequired!.ButtonPressed,
            new(mode, Binding("combat", combatEvent, _combatHandler!.Text)),
            new(Binding("inventory-accessed", "creature.inventory_accessed", _accessedHandler!.Text),
                Binding("item-transferred", "creature.item_transferred", _transferredHandler!.Text)),
            new(Binding("rest-completed", "creature.rest_completed", _restHandler!.Text)),
            new(_heartbeatEnabled!.ButtonPressed, checked((int)_heartbeatInterval!.Value),
                checked((int)_heartbeatBudget!.Value), _suspendOutsideInterest!.ButtonPressed,
                Binding("heartbeat", "creature.heartbeat", _heartbeatHandler!.Text)),
            Rows(_customEvents!.Text, 6, "événement utilisateur").Select((parts, index) =>
            {
                var fields = new Dictionary<string, CreatureCustomFieldType>(StringComparer.Ordinal);
                foreach (var field in Csv(parts[4]))
                {
                    var pair = field.Split(':', 2, StringSplitOptions.TrimEntries);
                    if (pair.Length != 2 || !Enum.TryParse<CreatureCustomFieldType>(pair[1], true, out var type) ||
                        !fields.TryAdd(pair[0], type))
                        throw new FormatException($"Champ typé invalide : '{field}'.");
                }
                return new CreatureEventProfile.CreatureCustomEventBinding(
                    StableId(_draftProfileId, "custom-schema", $"{parts[0]}.{parts[1]}", index), parts[0], parts[1],
                    Integer(parts[2], "version"), fields, Boolean(parts[3], "required"),
                    Binding($"custom-{parts[0]}-{parts[1]}", $"custom.{parts[0]}.{parts[1]}", parts[5]));
            }).ToArray(),
            ParseMap(_overrides!.Text),
            new(
                Binding("before-damage", "creature.before_damage", _beforeDamageHandler!.Text),
                Binding("damage-applied", "creature.damage_applied", _damageAppliedHandler!.Text),
                Binding("entity-died", "entity.died", _deathHandler!.Text),
                checked((int)_beforeDamageBudget!.Value), _maximumDamageAdjustment!.Value));
    }

    private EventBinding Binding(string group, string eventName, string handler) => new(
        StableId(_draftProfileId, "binding", group, 0), 1, "toolset:creature-events", "1.0", eventName,
        handler, EventAuthorityScope.Server);

    private void RebuildSummary()
    {
        if (_summary is null) return;
        try
        {
            var profile = BuildProfile();
            var diagnostics = CreatureEventValidator.Validate(profile);
            var heartbeat = diagnostics.Count == 0
                ? CreatureEventRuntimeResolver.AdvanceHeartbeat(profile, CreatureEventRuntimeState.Empty,
                    profile.Heartbeat?.IntervalMilliseconds ?? 0, true, SelectedBlueprint()?.Slug ?? "preview")
                : null;
            var custom = diagnostics.Count == 0 ? profile.CustomEvents.FirstOrDefault() : null;
            SummarySectionCount = 4;
            _summary.Text =
                $"[font_size=22][color=#ff9858]{CreatureEventValidator.EnumerateBindings(profile).Count} binding(s)[/color][/font_size]\n" +
                $"[color=#8ce0bd]UUID stable · schéma v{profile.SchemaVersion} · ForgeScript 1.0[/color]\n\n" +
                $"[b]CYCLE DE VIE[/b]\nCombat {profile.CombatBoundary?.Mode} → {profile.CombatBoundary?.Binding.Handler}\n" +
                $"Inventaire → {profile.Inventory?.Accessed.Handler} / {profile.Inventory?.Transferred.Handler}\n" +
                $"Repos → {profile.Rest?.Completed.Handler}\n\n" +
                $"[b]DÉGÂTS & MORT[/b]\nBeforeDamage → {profile.DamageDeath?.BeforeDamage.Handler} · " +
                $"budget {profile.DamageDeath?.BeforeDamageBudgetMilliseconds} ms · ajustement ±{profile.DamageDeath?.MaximumAdjustmentRatio:0.##}\n" +
                $"DamageApplied → {profile.DamageDeath?.DamageApplied.Handler}\n" +
                $"EntityDied → {profile.DamageDeath?.Died.Handler}\n\n" +
                $"[b]HEARTBEAT[/b]\n{(profile.Heartbeat?.Enabled == true ? "actif" : "inactif")} · " +
                $"{profile.Heartbeat?.IntervalMilliseconds} ms · budget {profile.Heartbeat?.BudgetMilliseconds} ms · " +
                $"{heartbeat?.Dispatches.Count ?? 0} dispatch d’aperçu\n\n" +
                $"[b]UTILISATEUR TYPÉ[/b]\n{profile.CustomEvents.Count} schéma(s)\n" +
                (custom is null ? "[color=#776b88]Aucun[/color]" :
                    $"{custom.QualifiedName}@{custom.PayloadVersion} · {string.Join(", ", custom.Fields.Select(item => $"{item.Key}:{item.Value}"))}") + "\n\n" +
                (diagnostics.Count == 0 ? "[color=#8ce0bd]✓ Profil valide et scheduler borné[/color]"
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
            if (parts.Length != count)
                throw new FormatException($"Ligne {label} invalide : '{line}' ({count} colonnes attendues).");
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
    private static bool Boolean(string text, string label) => bool.TryParse(text, out var value)
        ? value : throw new FormatException($"{label} invalide : '{text}'.");
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
