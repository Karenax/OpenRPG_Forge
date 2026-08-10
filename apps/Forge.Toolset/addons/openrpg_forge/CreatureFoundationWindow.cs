#if TOOLS
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Lifecycle;
using Forge.Domain.Spatial;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class CreatureFoundationWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<BlueprintId, CreatureFoundationProfile, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private ItemList? _creatures;
    private ScrollContainer? _scroll;
    private BlueprintId? _selectedId;
    private Guid _profileId;
    private CheckBox? _required;
    private LineEdit? _slug;
    private LineEdit? _name;
    private LineEdit? _pronouns;
    private LineEdit? _aliases;
    private TextEdit? _biography;
    private TextEdit? _notes;
    private LineEdit? _tags;
    private TextEdit? _resources;
    private OptionButton? _movement;
    private SpinBox? _speed;
    private SpinBox? _acceleration;
    private SpinBox? _deceleration;
    private SpinBox? _minimumAltitude;
    private SpinBox? _maximumAltitude;
    private SpinBox? _maximumSlope;
    private TextEdit? _references;
    private LineEdit? _spawnHandler;
    private TextEdit? _overrides;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;

    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public int SummarySectionCount { get; private set; }

    public void Initialize(
        Func<ProjectState?> load,
        Func<BlueprintId, CreatureFoundationProfile, bool, CommandResult> mutate,
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
        Title = "Fondations de créature · OpenRPG Forge";
        MinSize = new(1080, 720);
        Size = new(1420, 900);
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
        var warden = _load?.Invoke()?.Project.Blueprints.FirstOrDefault(item => item.Slug == "ash-warden-boss");
        Refresh(warden?.Id);
        if (_selectedId is null || _resources is null || _spawnHandler is null) return false;
        _required!.ButtonPressed = true;
        _resources.Text = "health|100|84|0.5|true\nstamina|40|40|2|false";
        _movement!.Select((int)CreatureFoundationMovementMode.Grounded);
        _speed!.Value = 6; _acceleration!.Value = 12; _deceleration!.Value = 8;
        _minimumAltitude!.Value = 0; _maximumAltitude!.Value = 0; _maximumSlope!.Value = 50;
        _spawnHandler.Text = "OnSpawned";
        RebuildSummary();
        return SummarySectionCount == 4 && _summary!.Text.Contains("Prêt", StringComparison.Ordinal);
    }

    public bool PrepareForCapture()
    {
        var prepared = ConfigureForSmokeTest();
        if (_scroll is not null) _scroll.ScrollVertical = 520;
        return prepared;
    }

    public bool PreviewForSmokeTest()
    {
        Mutate(true);
        return LastCommandId != Guid.Empty && LastPatchSummary.Contains("resource pools", StringComparison.Ordinal);
    }

    public bool ApplyForSmokeTest()
    {
        Mutate(false);
        return LastCommandId != Guid.Empty && LastDiagnostics.Length == 0;
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
            var profile = blueprint.CreatureFoundation;
            var index = _creatures!.AddItem($"{blueprint.DisplayName}\n{blueprint.Slug} · " +
                (profile is null ? "fondation vide" : $"{profile.Resources.Count} ressources · {profile.Movement.Mode}"));
            _creatures.SetItemMetadata(index, blueprint.Id.ToString());
        }
        if (candidates.Length == 0)
        {
            _feedback!.Text = "[color=#ffbd7a]État vide : créez d’abord un blueprint de créature.[/color]";
            return;
        }
        var selected = preferred ?? _selectedId ?? candidates[0].Id;
        var selectedIndex = Array.FindIndex(candidates, item => item.Id == selected);
        if (selectedIndex < 0) selectedIndex = 0;
        _creatures!.Select(selectedIndex);
        LoadBlueprint(candidates[selectedIndex]);
    }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 18);
        AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 10); margin.AddChild(root);
        var title = new Label { Text = "FONDATIONS DE CRÉATURE" };
        title.AddThemeFontSizeOverride("font_size", 24); title.AddThemeColorOverride("font_color", new Color("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label { Text = "Identité, ressources, déplacement, composants et événement de spawn dans un seul document canonique." });

        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [285]
        }; root.AddChild(split);
        var catalog = Panel("CRÉATURES"); split.AddChild(catalog);
        _creatures = new ItemList { CustomMinimumSize = new(270, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _creatures.ItemSelected += index => SelectCreature((int)index);
        catalog.GetChild<VBoxContainer>(0).AddChild(_creatures);

        _scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        }; split.AddChild(_scroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 8); _scroll.AddChild(form);
        _required = new CheckBox { Text = "Fondation requise par le runtime" }; form.AddChild(_required);
        Section(form, "IDENTITÉ LOCALISÉE");
        _slug = Field(form, "Slug canonique", "ash-warden");
        _name = Field(form, "Nom français", "Gardien des cendres");
        _pronouns = Field(form, "Pronoms", "il");
        _aliases = Field(form, "Alias (séparés par des virgules)", "gardien, sentinelle");
        _biography = Multi(form, "Biographie française", 80);
        _notes = Multi(form, "Notes auteur", 65);
        _tags = Field(form, "Tags narratifs", "boss, crypt");

        Section(form, "RESSOURCES");
        _resources = Multi(form, "resource|max|current|regen|persistent — une ligne par pool", 110);
        Section(form, "DÉPLACEMENT");
        _movement = new OptionButton(); foreach (var mode in Enum.GetValues<CreatureFoundationMovementMode>())
            _movement.AddItem(mode.ToString()); form.AddChild(_movement);
        var movementGrid = new GridContainer { Columns = 3 }; form.AddChild(movementGrid);
        _speed = Spin(movementGrid, "Vitesse max", .1, 1000, 5);
        _acceleration = Spin(movementGrid, "Accélération", .1, 1000, 10);
        _deceleration = Spin(movementGrid, "Décélération", 0, 1000, 8);
        _minimumAltitude = Spin(movementGrid, "Altitude min", -10000, 10000, 0);
        _maximumAltitude = Spin(movementGrid, "Altitude max", -10000, 10000, 0);
        _maximumSlope = Spin(movementGrid, "Pente max", 0, 90, 45);

        Section(form, "COMPOSANTS & SCRIPT");
        _references = Multi(form,
            "appearance|rig|perception|combat|dialogue optionnel|inventory|faction", 70);
        _spawnHandler = Field(form, "Handler creature.spawned (ForgeScript 1.0, serveur)", "OnSpawned");
        _overrides = Multi(form, "Overrides explicites — clé=valeur", 70);
        _summary = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(0, 155), FitContent = false };
        form.AddChild(_summary);
        var actions = new HBoxContainer(); form.AddChild(actions);
        Button(actions, "Prévisualiser", () => Mutate(true));
        Button(actions, "Appliquer", () => Mutate(false), true);
        Button(actions, "Annuler", () => { _undo?.Invoke(); Refresh(_selectedId); });
        Button(actions, "Rétablir", () => { _redo?.Invoke(); Refresh(_selectedId); });
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(0, 110), FitContent = false };
        form.AddChild(_feedback);
        foreach (var control in new Control[] { _required, _slug, _name, _pronouns, _aliases, _biography, _notes,
                     _tags, _resources, _movement, _speed, _acceleration, _deceleration, _minimumAltitude,
                     _maximumAltitude, _maximumSlope, _references, _spawnHandler, _overrides })
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
        var blueprint = state.Project.Blueprints.Single(item => item.Id == id);
        LoadBlueprint(blueprint);
    }

    private void LoadBlueprint(BlueprintDefinition blueprint)
    {
        _selectedId = blueprint.Id;
        var profile = blueprint.CreatureFoundation ?? CreatureFoundationProfile.CreateDefault(
            Guid.NewGuid(), blueprint.Slug, blueprint.DisplayName, "toolset:creature-foundation");
        _profileId = profile.Id;
        _required!.ButtonPressed = profile.RequiredAtRuntime;
        _slug!.Text = profile.Identity.Slug;
        _name!.Text = profile.Identity.Names.GetValueOrDefault("fr", blueprint.DisplayName);
        _pronouns!.Text = profile.Identity.Pronouns;
        _aliases!.Text = string.Join(", ", profile.Identity.Aliases);
        _biography!.Text = profile.Identity.Biographies.GetValueOrDefault("fr", string.Empty);
        _notes!.Text = profile.Identity.AuthorNotes;
        _tags!.Text = string.Join(", ", profile.Identity.NarrativeTags);
        _resources!.Text = string.Join('\n', profile.Resources.Select(item =>
            $"{item.ResourceId}|{Number(item.Maximum)}|{Number(item.Current)}|{Number(item.RegenerationPerSecond)}|{item.Persistent.ToString().ToLowerInvariant()}"));
        _movement!.Select((int)profile.Movement.Mode);
        _speed!.Value = profile.Movement.MaximumSpeed; _acceleration!.Value = profile.Movement.Acceleration;
        _deceleration!.Value = profile.Movement.Deceleration; _minimumAltitude!.Value = profile.Movement.MinimumAltitude;
        _maximumAltitude!.Value = profile.Movement.MaximumAltitude; _maximumSlope!.Value = profile.Movement.MaximumSlopeDegrees;
        var refs = profile.References;
        _references!.Text = string.Join('|', new[] { refs.AppearanceId, refs.RigId, refs.PerceptionProfileId,
            refs.CombatProfileId, refs.DialogueId ?? string.Empty, refs.InventoryProfileId, refs.FactionId });
        _spawnHandler!.Text = profile.Spawned.Handler;
        _overrides!.Text = string.Join('\n', profile.Overrides.Select(item => $"{item.Key}={item.Value}"));
        _feedback!.Text = blueprint.CreatureFoundation is null
            ? "[color=#b8a8ce]État vide : les valeurs sûres sont préremplies, aucune donnée n’est encore écrite.[/color]"
            : $"[color=#8ce0bd]Fondation canonique chargée · UUID {profile.Id:D} · schéma v{profile.SchemaVersion}[/color]";
        RebuildSummary();
    }

    private CreatureFoundationProfile BuildProfile()
    {
        var resourceRows = Rows(_resources!.Text, 5, "ressource");
        var resources = resourceRows.Select((parts, index) => new CreatureFoundationResourcePool(
            StableId(_profileId, "resource", parts[0], index), parts[0], Decimal(parts[1]), Decimal(parts[2]),
            Decimal(parts[3]), bool.Parse(parts[4]))).ToArray();
        var refs = Rows(_references!.Text, 7, "références").Single();
        return new(_profileId, CreatureFoundationProfile.CurrentSchemaVersion, "toolset:creature-foundation",
            _required!.ButtonPressed,
            new(_slug!.Text, new Dictionary<string, string> { ["fr"] = _name!.Text }, Csv(_aliases!.Text),
                _pronouns!.Text, new Dictionary<string, string> { ["fr"] = _biography!.Text }, _notes!.Text,
                Csv(_tags!.Text)),
            TransformValue.Identity, resources,
            new((CreatureFoundationMovementMode)_movement!.Selected, _speed!.Value, _acceleration!.Value,
                _deceleration!.Value, _minimumAltitude!.Value, _maximumAltitude!.Value, _maximumSlope!.Value),
            new(refs[0], refs[1], refs[2], refs[3], string.IsNullOrWhiteSpace(refs[4]) ? null : refs[4], refs[5], refs[6]),
            new(StableId(_profileId, "binding", "spawned", 0), 1, "toolset:creature-foundation", "1.0",
                "creature.spawned", _spawnHandler!.Text, EventAuthorityScope.Server), ParseMap(_overrides!.Text));
    }

    private void Mutate(bool dryRun)
    {
        LastCommandId = Guid.Empty; LastPatchSummary = string.Empty; LastDiagnostics = string.Empty;
        if (_selectedId is null || _mutate is null) return;
        try
        {
            var result = _mutate(_selectedId, BuildProfile(), dryRun);
            LastCommandId = result.Audit.CommandId.Value;
            LastPatchSummary = result.Patch?.Summary ?? string.Empty;
            if (!result.Succeeded)
            {
                LastDiagnostics = string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
                _feedback!.Text = $"[color=#ff7777]Configuration refusée[/color]\n{LastDiagnostics}";
                return;
            }
            _feedback!.Text = dryRun
                ? $"[color=#ffbd7a]APERÇU — aucune donnée modifiée[/color]\n{result.Patch?.Summary}\nInverse : {result.Inverse?.Summary}"
                : $"[color=#8ce0bd]APPLIQUÉ AU PROJET[/color]\n{result.Patch?.Summary}\nRévision {result.NewRevision}";
            if (!dryRun) Refresh(_selectedId);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or InvalidOperationException)
        {
            LastDiagnostics = exception.Message;
            _feedback!.Text = $"[color=#ff7777]Erreur de formulaire[/color]\n{exception.Message}";
        }
    }

    private void RebuildSummary()
    {
        if (_summary is null) return;
        try
        {
            var profile = BuildProfile();
            var diagnostics = CreatureFoundationValidator.Validate(profile);
            SummarySectionCount = 4;
            _summary.Text = $"[color=#ffad70]APERÇU DES RÈGLES[/color]\n" +
                $"Identité · {profile.Identity.Names.GetValueOrDefault("fr", profile.Identity.Slug)} · {profile.Identity.NarrativeTags.Count} tags\n" +
                $"Ressources · {string.Join(" · ", profile.Resources.Select(item => $"{item.ResourceId} {Number(item.Current)}/{Number(item.Maximum)}"))}\n" +
                $"Déplacement · {profile.Movement.Mode} · {Number(profile.Movement.MaximumSpeed)} m/s · pente {Number(profile.Movement.MaximumSlopeDegrees)}°\n" +
                $"Spawn · {profile.Spawned.EventName} → {profile.Spawned.Handler} · serveur\n" +
                (diagnostics.Count == 0 ? "[color=#8ce0bd]Prêt : validation canonique réussie.[/color]" :
                    $"[color=#ff7777]{diagnostics.Count} diagnostic(s) : {diagnostics[0].Code} · {diagnostics[0].Path}[/color]");
        }
        catch (Exception exception)
        {
            SummarySectionCount = 0;
            _summary.Text = $"[color=#ff7777]Formulaire incomplet : {exception.Message}[/color]";
        }
    }

    private static PanelContainer Panel(string title)
    {
        var panel = new PanelContainer(); var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box); var label = new Label { Text = title }; label.AddThemeColorOverride("font_color", new Color("ffad70"));
        box.AddChild(label); return panel;
    }

    private static void Section(Container owner, string text)
    {
        var label = new Label { Text = text }; label.AddThemeColorOverride("font_color", new Color("ffad70"));
        label.AddThemeFontSizeOverride("font_size", 16); owner.AddChild(label);
    }

    private static LineEdit Field(Container owner, string label, string placeholder)
    {
        owner.AddChild(new Label { Text = label }); var field = new LineEdit { PlaceholderText = placeholder };
        owner.AddChild(field); return field;
    }

    private static TextEdit Multi(Container owner, string label, float height)
    {
        owner.AddChild(new Label { Text = label }); var field = new TextEdit { CustomMinimumSize = new(0, height) };
        owner.AddChild(field); return field;
    }

    private static SpinBox Spin(Container owner, string label, double minimum, double maximum, double value)
    {
        var box = new VBoxContainer(); owner.AddChild(box); box.AddChild(new Label { Text = label });
        var spin = new SpinBox { MinValue = minimum, MaxValue = maximum, Step = .1, Value = value };
        box.AddChild(spin); return spin;
    }

    private static void Button(Container owner, string text, Action action, bool primary = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new(120, 36) };
        if (primary) button.AddThemeColorOverride("font_color", new Color("fff4ec"));
        button.Pressed += action; owner.AddChild(button);
    }

    private static string[][] Rows(string text, int columns, string label) => text
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(row => row.Split('|', StringSplitOptions.TrimEntries))
        .Select(parts => parts.Length == columns ? parts : throw new FormatException($"Ligne {label} invalide : {columns} colonnes attendues."))
        .ToArray();

    private static string[] Csv(string text) => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static double Decimal(string text) => double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static IReadOnlyDictionary<string, string> ParseMap(string text) => text
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(row => row.Split('=', 2, StringSplitOptions.TrimEntries))
        .Select(parts => parts.Length == 2 ? parts : throw new FormatException("Override invalide : clé=valeur attendu."))
        .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

    private static Guid StableId(Guid owner, string scope, string key, int index)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{owner:D}:{scope}:{key}:{index}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50); bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
    }
}
#endif
