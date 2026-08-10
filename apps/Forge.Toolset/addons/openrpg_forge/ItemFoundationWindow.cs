#if TOOLS
using System.Globalization;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Items;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class ItemFoundationWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<BlueprintId, ItemFoundationProfile?, bool, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private ItemList? _items;
    private ScrollContainer? _scroll;
    private BlueprintId? _selectedId;
    private Guid _profileId;
    private Guid _typeId;
    private Guid _propertyId;
    private CheckBox? _required;
    private LineEdit? _typeSlug;
    private LineEdit? _allowedSlots;
    private LineEdit? _allowedProperties;
    private LineEdit? _requiredRig;
    private LineEdit? _manifest;
    private LineEdit? _license;
    private LineEdit? _hash;
    private LineEdit? _appearance;
    private SpinBox? _version;
    private SpinBox? _quality;
    private SpinBox? _level;
    private SpinBox? _value;
    private LineEdit? _slots;
    private LineEdit? _property;
    private LineEdit? _name;
    private TextEdit? _short;
    private TextEdit? _long;
    private LineEdit? _unknown;
    private LineEdit? _typeLabel;
    private TextEdit? _notes;
    private LineEdit? _overrides;
    private CheckBox? _acceptMigration;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;

    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public int SummarySectionCount { get; private set; }
    public BlueprintId? SelectedBlueprintId => _selectedId;

    public void Initialize(
        Func<ProjectState?> load,
        Func<BlueprintId, ItemFoundationProfile?, bool, bool, CommandResult> mutate,
        Action undo,
        Action redo,
        Action<Window> applyIcon)
    {
        _load = load; _mutate = mutate; _undo = undo; _redo = redo; _applyIcon = applyIcon;
    }

    public override void _Ready()
    {
        Title = "Fondations d’objet · OpenRPG Forge";
        MinSize = new(1120, 760); Size = new(1440, 920); CloseRequested += Hide;
        Build(); Refresh();
    }

    public void Open() { Refresh(); PopupCentered(); _applyIcon?.Invoke(this); }

    public bool ConfigureForSmokeTest()
    {
        var candidate = _load?.Invoke()?.Project.Blueprints.FirstOrDefault(item => item.Kind == BlueprintKind.Item);
        Refresh(candidate?.Id);
        if (_selectedId is null) return false;
        _required!.ButtonPressed = true;
        _typeSlug!.Text = "ritual-key"; _allowedSlots!.Text = "hand, inventory";
        _allowedProperties!.Text = "utility"; _requiredRig!.Text = "rig.hand";
        _manifest!.Text = "crypt-of-ashes"; _license!.Text = "CC0-1.0"; _hash!.Text = new string('d', 64);
        _appearance!.Text = "asset.items.ash-key"; _version!.Value = 2; _quality!.Value = 2;
        _level!.Value = 4; _value!.Value = 75; _slots!.Text = "hand";
        _property!.Text = "utility|1|effect.ash-key|OnUse";
        _name!.Text = "Clé des cendres"; _short!.Text = "Une clé rituelle.";
        _long!.Text = "Une clé forgée pour ouvrir le sanctuaire de la crypte.";
        _unknown!.Text = "Objet inconnu"; _typeLabel!.Text = "Clé";
        _notes!.Text = "Récompense principale du module."; _overrides!.Text = "identified, durability, owner";
        _acceptMigration!.ButtonPressed = false;
        _feedback!.Text = "[color=#8ce0bd]Formulaire prêt pour prévisualisation.[/color]";
        RebuildSummary();
        return SummarySectionCount == 4 && ItemFoundationValidator.Validate(BuildProfile()).Count == 0;
    }

    public bool PrepareForCapture()
    {
        var prepared = ConfigureForSmokeTest();
        if (_scroll is not null) _scroll.ScrollVertical = 0;
        Open(); ConfigureForSmokeTest();
        return prepared;
    }

    public bool PreviewForSmokeTest() { Mutate(true); return LastCommandId != Guid.Empty && LastDiagnostics.Length == 0; }
    public bool ApplyForSmokeTest() { Mutate(false); return LastCommandId != Guid.Empty && LastDiagnostics.Length == 0; }

    public bool RejectInvalidForSmokeTest()
    {
        var previous = _hash!.Text; _hash.Text = "invalid"; Mutate(true);
        var rejected = LastDiagnostics.Contains("item.foundation.content_governance_invalid", StringComparison.Ordinal);
        _hash.Text = previous; RebuildSummary();
        return rejected;
    }

    public void Refresh(BlueprintId? preferred = null)
    {
        var state = _load?.Invoke(); _items?.Clear();
        var candidates = state?.Project.Blueprints.Where(item => item.Kind == BlueprintKind.Item)
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal).ToArray() ?? [];
        foreach (var blueprint in candidates)
        {
            var profile = blueprint.ItemFoundation;
            var index = _items!.AddItem($"{blueprint.DisplayName} · {blueprint.Slug} · " +
                (profile is null ? "fondation vide" : $"{profile.Type.Slug} · v{profile.DefinitionVersion}"));
            _items.SetItemMetadata(index, blueprint.Id.ToString());
        }
        if (candidates.Length == 0)
        {
            _feedback!.Text = "[color=#ffbd7a]État vide : créez d’abord un blueprint objet.[/color]";
            return;
        }
        var selected = preferred ?? _selectedId ?? candidates[0].Id;
        var selectedIndex = Array.FindIndex(candidates, item => item.Id == selected);
        if (selectedIndex < 0) selectedIndex = 0;
        _items!.Select(selectedIndex); Load(candidates[selectedIndex]);
    }

    private void Build()
    {
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 18);
        AddChild(margin); var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 10); margin.AddChild(root);
        var title = new Label { Text = "FONDATIONS D’OBJET" }; title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new("ff9858")); root.AddChild(title);
        root.AddChild(new Label { Text = "Type immuable, blueprint versionné, identité localisée et état d’instance séparé." });
        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [280]
        }; root.AddChild(split);
        var catalog = Panel("OBJETS"); catalog.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        catalog.CustomMinimumSize = new(285, 0); split.AddChild(catalog);
        _items = new ItemList { CustomMinimumSize = new(270, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _items.ItemSelected += index => SelectItem((int)index); catalog.GetChild<VBoxContainer>(0).AddChild(_items);
        var editor = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [650]
        }; split.AddChild(editor);
        _scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(610, 0)
        }; editor.AddChild(_scroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 7); _scroll.AddChild(form);
        _required = Check(form, "Fondation requise au runtime");
        Section(form, "TYPE IMMUABLE & GOUVERNANCE");
        _typeSlug = Field(form, "Type référencé", "generic-item");
        _allowedSlots = Field(form, "Slots autorisés", "hand, inventory");
        _allowedProperties = Field(form, "Propriétés autorisées", "utility");
        _requiredRig = Field(form, "Rig requis", "none");
        _manifest = Field(form, "Manifeste de package", "project-content");
        _license = Field(form, "Licence SPDX", "CC0-1.0");
        _hash = Field(form, "SHA-256 du contenu", new string('0', 64));
        _acceptMigration = Check(form, "Accepter explicitement les incompatibilités de migration");
        Section(form, "BLUEPRINT & RÈGLES");
        _appearance = Field(form, "Asset d’apparence", "asset.item.placeholder");
        var values = new GridContainer { Columns = 4 }; form.AddChild(values);
        _version = Spin(values, "Version", 1, 100000, 1); _quality = Spin(values, "Qualité", 0, 5, 0);
        _level = Spin(values, "Niveau", 0, 1000, 0); _value = Spin(values, "Valeur", 0, 1_000_000, 0);
        _slots = Field(form, "Slots actifs", "inventory");
        _property = Field(form, "Propriété kind|valeur|effet|script", "utility|1|effect.use|OnUse");
        Section(form, "IDENTITÉ LOCALISÉE · FR");
        _name = Field(form, "Nom", "Objet"); _short = Multi(form, "Description courte", 55);
        _long = Multi(form, "Description longue", 75); _unknown = Field(form, "Texte inconnu", "Objet inconnu");
        _typeLabel = Field(form, "Label de type", "Objet"); _notes = Multi(form, "Notes auteur", 55);
        _overrides = Field(form, "Overrides d’instance autorisés", "identified, durability, owner");
        var summaryPanel = Panel("APERÇU & VALIDATION"); summaryPanel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        summaryPanel.CustomMinimumSize = new(365, 0); editor.AddChild(summaryPanel);
        var summaryBox = summaryPanel.GetChild<VBoxContainer>(0);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new(350, 430),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        }; summaryBox.AddChild(_summary);
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(350, 160) };
        summaryBox.AddChild(_feedback);
        var actions = new HBoxContainer(); root.AddChild(actions);
        AddButton(actions, "Prévisualiser", () => Mutate(true)); AddButton(actions, "Appliquer", () => Mutate(false), true);
        AddButton(actions, "Annuler", () => { _undo?.Invoke(); Refresh(); });
        AddButton(actions, "Rétablir", () => { _redo?.Invoke(); Refresh(); }); AddButton(actions, "Fermer", Hide);
    }

    private void Load(BlueprintDefinition blueprint)
    {
        _selectedId = blueprint.Id;
        var profile = blueprint.ItemFoundation ?? ItemFoundationProfile.CreateDefault(Guid.NewGuid(), blueprint.Slug,
            blueprint.DisplayName, "toolset:r044");
        _profileId = profile.Id; _typeId = profile.Type.Id;
        _propertyId = profile.Properties.FirstOrDefault()?.Id ?? Guid.NewGuid();
        _required!.ButtonPressed = profile.RequiredAtRuntime; _typeSlug!.Text = profile.Type.Slug;
        _allowedSlots!.Text = Join(profile.Type.AllowedSlots); _allowedProperties!.Text = Join(profile.Type.AllowedPropertyKinds);
        _requiredRig!.Text = profile.Type.RequiredRig; _manifest!.Text = profile.PackageManifestId;
        _license!.Text = profile.LicenseSpdx; _hash!.Text = profile.ContentSha256; _appearance!.Text = profile.AppearanceAssetId;
        _version!.Value = profile.DefinitionVersion; _quality!.Value = profile.Quality; _level!.Value = profile.Level;
        _value!.Value = (double)profile.BaseValue; _slots!.Text = Join(profile.Slots);
        var property = profile.Properties.FirstOrDefault();
        _property!.Text = property is null ? "utility|1|effect.use|OnUse" :
            $"{property.Kind}|{property.Value.ToString(CultureInfo.InvariantCulture)}|{property.EffectId}|{property.ScriptId}";
        var text = profile.Texts.FirstOrDefault(item => item.Locale == "fr") ?? profile.Texts.First();
        _name!.Text = text.Name; _short!.Text = text.ShortDescription; _long!.Text = text.LongDescription;
        _unknown!.Text = text.UnknownText; _typeLabel!.Text = text.TypeLabel; _notes!.Text = text.Notes;
        _overrides!.Text = Join(profile.AllowedInstanceOverrides); _acceptMigration!.ButtonPressed = false;
        RebuildSummary();
    }

    private ItemFoundationProfile BuildProfile()
    {
        var state = _load?.Invoke() ?? throw new InvalidOperationException("Aucun projet Forge ouvert.");
        var blueprint = state.Project.Blueprints.Single(item => item.Id == _selectedId);
        var parts = _property!.Text.Split('|', StringSplitOptions.TrimEntries);
        var property = parts.Length >= 3
            ? new ItemFoundationProperty(_propertyId, parts[0], double.TryParse(parts[1], NumberStyles.Float,
                CultureInfo.InvariantCulture, out var number) ? number : double.NaN, parts[2],
                parts.Length > 3 && parts[3].Length > 0 ? parts[3] : null)
            : new ItemFoundationProperty(_propertyId, string.Empty, double.NaN, string.Empty, null);
        var type = new ItemTypeDefinition(_typeId, 1, "toolset:r044", _typeSlug!.Text,
            Values(_allowedSlots!.Text), Values(_allowedProperties!.Text), _requiredRig!.Text);
        return new(_profileId, 1, "toolset:r044", _required!.ButtonPressed, _manifest!.Text, _license!.Text,
            _hash!.Text, type.Id, type, blueprint.Slug, _appearance!.Text, type.RequiredRig, (int)_version!.Value,
            (int)_quality!.Value, (int)_level!.Value, (decimal)_value!.Value, Values(_slots!.Text), [property],
            [new("fr", _name!.Text, _short!.Text, _long!.Text, _unknown!.Text, _typeLabel!.Text, _notes!.Text)],
            Values(_overrides!.Text));
    }

    private void Mutate(bool dryRun)
    {
        if (_selectedId is null || _mutate is null) return;
        var result = _mutate(_selectedId, BuildProfile(), _acceptMigration!.ButtonPressed, dryRun);
        LastCommandId = result.Audit.CommandId.Value; LastPatchSummary = result.Patch?.Summary ?? string.Empty;
        LastDiagnostics = string.Join("\n", result.Diagnostics.Select(item => $"{item.Code} · {item.Message}"));
        _feedback!.Text = result.Succeeded
            ? dryRun ? $"[color=#ffbd7a]APERÇU · aucune écriture[/color]\n{LastPatchSummary}"
                : $"[color=#8ce0bd]APPLIQUÉ[/color]\n{LastPatchSummary}"
            : $"[color=#ff7777]TRANSACTION REFUSÉE[/color]\n{LastDiagnostics}";
        if (!dryRun && result.Succeeded) Refresh(_selectedId);
    }

    private void RebuildSummary()
    {
        if (_summary is null || _selectedId is null) return;
        var profile = BuildProfile(); var diagnostics = ItemFoundationValidator.Validate(profile);
        SummarySectionCount = 4;
        _summary.Text = $"[color=#ffad70][b]TYPE IMMUABLE[/b][/color]\n{profile.Type.Slug} · {profile.TypeId:D}\n" +
            $"Slots {Join(profile.Type.AllowedSlots)} · propriétés {Join(profile.Type.AllowedPropertyKinds)}\n\n" +
            $"[color=#ffad70][b]BLUEPRINT VERSIONNÉ[/b][/color]\n{profile.Archetype} · v{profile.DefinitionVersion} · {profile.AppearanceAssetId}\n" +
            $"Licence {profile.LicenseSpdx} · manifeste {profile.PackageManifestId}\n\n" +
            $"[color=#ffad70][b]IDENTITÉ & INSTANCE[/b][/color]\n{profile.Texts[0].Name} · {profile.Texts[0].TypeLabel}\n" +
            $"Overrides bornés : {Join(profile.AllowedInstanceOverrides)}\n\n" +
            $"[color=#ffad70][b]VALIDATION[/b][/color]\n" +
            (diagnostics.Count == 0 ? "[color=#8ce0bd]Prêt · document canonique valide[/color]" :
                $"[color=#ff7777]{diagnostics.Count} diagnostic(s)\n{string.Join("\n", diagnostics.Select(item => item.Code))}[/color]");
    }

    private void SelectItem(int index)
    {
        if (_items?.GetItemMetadata(index).AsString() is not { Length: > 0 } id) return;
        var blueprint = _load?.Invoke()?.Project.Blueprints.SingleOrDefault(item => item.Id.ToString() == id);
        if (blueprint is not null) Load(blueprint);
    }

    private static string[] Values(string text) => text.Split(',', StringSplitOptions.RemoveEmptyEntries |
        StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    private static string Join(IEnumerable<string> values) => string.Join(", ", values);
    private static void Section(Container owner, string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new("ffad70")); owner.AddChild(label);
    }
    private static LineEdit Field(Container owner, string label, string placeholder)
    {
        owner.AddChild(new Label { Text = label });
        var field = new LineEdit { PlaceholderText = placeholder }; field.TextChanged += _ => { }; owner.AddChild(field); return field;
    }
    private static TextEdit Multi(Container owner, string label, float height)
    {
        owner.AddChild(new Label { Text = label });
        var field = new TextEdit { CustomMinimumSize = new(0, height) }; owner.AddChild(field); return field;
    }
    private static CheckBox Check(Container owner, string text) { var field = new CheckBox { Text = text }; owner.AddChild(field); return field; }
    private static SpinBox Spin(Container owner, string label, double minimum, double maximum, double value)
    {
        var box = new VBoxContainer(); box.AddChild(new Label { Text = label }); var field = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Value = value,
            Step = 1,
            AllowGreater = false,
            AllowLesser = false
        };
        box.AddChild(field); owner.AddChild(box); return field;
    }
    private static PanelContainer Panel(string title)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        }; var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box); var label = new Label { Text = title }; label.AddThemeColorOverride("font_color", new("ffad70"));
        box.AddChild(label); return panel;
    }
    private static void AddButton(Container owner, string text, Action action, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new(120, 36)
        }; if (primary) button.AddThemeColorOverride("font_color", new("fff4ec"));
        button.Pressed += action; owner.AddChild(button);
    }
}
#endif
