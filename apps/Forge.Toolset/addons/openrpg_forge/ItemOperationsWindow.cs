#if TOOLS
using System.Globalization;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Items;
using Forge.RpgKit.Application.Items;
using Forge.RpgKit.Domain.Items;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class ItemOperationsWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<BlueprintId, ItemFoundationProfile?, bool, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private ItemList? _items;
    private BlueprintId? _selectedId;
    private ItemFoundationProfile? _foundation;
    private Guid _profileId;
    private Guid _lootId;
    private Guid _inspectorId;
    private readonly Dictionary<ItemLifecycleEventType, Guid> _eventIds = [];
    private CheckBox? _required;
    private LineEdit? _manifest;
    private LineEdit? _license;
    private LineEdit? _hash;
    private TextEdit? _lootEntries;
    private LineEdit? _lootConditions;
    private SpinBox? _seed;
    private TextEdit? _placements;
    private LineEdit? _selectedItems;
    private LineEdit? _targetContainer;
    private LineEdit? _targetSlot;
    private SpinBox? _gridX;
    private SpinBox? _gridY;
    private LineEdit? _filterContainer;
    private LineEdit? _filterTag;
    private LineEdit? _filterText;
    private TextEdit? _events;
    private RichTextLabel? _summary;
    private RichTextLabel? _feedback;
    private string? _capturePath;
    private double _captureElapsed;
    private bool _capturePrepared;

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
        _load = load;
        _mutate = mutate;
        _undo = undo;
        _redo = redo;
        _applyIcon = applyIcon;
    }

    public override void _Ready()
    {
        Title = "Loot, inventaire & événements · OpenRPG Forge";
        MinSize = new(1120, 760);
        Size = new(1440, 920);
        CloseRequested += Hide;
        Build();
        Refresh();
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r090-item-operations-ui-capture=", StringComparison.Ordinal));
        if (capture is not null)
        {
            _capturePath = capture["--r090-item-operations-ui-capture=".Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureElapsed += delta;
        if (!_capturePrepared && _captureElapsed >= 4)
        {
            _capturePrepared = PrepareForCapture();
            _captureElapsed = 0;
            return;
        }
        if (!_capturePrepared || !Visible || _captureElapsed < 2) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var error = GetViewport().GetTexture().GetImage().SavePng(destination);
        GD.Print($"OPENRPG_FORGE_R090_ITEM_OPERATIONS_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination}");
        _capturePath = null;
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open()
    {
        Refresh();
        PopupCentered();
        _applyIcon?.Invoke(this);
    }

    public bool ConfigureForSmokeTest()
    {
        var candidate = _load?.Invoke()?.Project.Blueprints.FirstOrDefault(item => item.Kind == BlueprintKind.Item);
        Refresh(candidate?.Id);
        if (_selectedId is null) return false;
        _required!.ButtonPressed = true;
        _manifest!.Text = "crypt-of-ashes";
        _license!.Text = "CC0-1.0";
        _hash!.Text = new string('7', 64);
        _lootEntries!.Text =
            "c2900000-0000-4000-8000-000000000071|relic|1|1|1|2|crypt-open|true\n" +
            "c2900000-0000-4000-8000-000000000072|relic|3|1|3|1|crypt-open|false";
        _lootConditions!.Text = "crypt-open";
        _seed!.Value = 42;
        _placements!.Text =
            "c2900000-0000-4000-8000-000000000073|backpack||0|0|1|quest|asset.items.ash-key.preview\n" +
            "c2900000-0000-4000-8000-000000000074|backpack||1|0|2|consumable|asset.items.ember.preview";
        _selectedItems!.Text = "c2900000-0000-4000-8000-000000000073, c2900000-0000-4000-8000-000000000074";
        _targetContainer!.Text = "vault";
        _targetSlot!.Text = string.Empty;
        _gridX!.Value = 3;
        _gridY!.Value = 2;
        _filterContainer!.Text = "backpack";
        _filterTag!.Text = string.Empty;
        _filterText!.Text = string.Empty;
        _events!.Text = string.Join("\n", Enum.GetValues<ItemLifecycleEventType>()
            .Select(kind => $"{kind}|On{kind}|server"));
        RebuildSummary();
        _feedback!.Text = "[color=#8ce0bd]Profil opérations prêt pour prévisualisation.[/color]";
        return SummarySectionCount == 3 && ItemFoundationValidator.Validate(BuildFoundation()).Count == 0;
    }

    public bool PrepareForCapture()
    {
        var result = ConfigureForSmokeTest();
        Open();
        ConfigureForSmokeTest();
        return result;
    }

    public bool PreviewForSmokeTest() { Mutate(true); return LastCommandId != Guid.Empty && LastDiagnostics.Length == 0; }
    public bool ApplyForSmokeTest() { Mutate(false); return LastCommandId != Guid.Empty && LastDiagnostics.Length == 0; }

    public bool RejectInvalidForSmokeTest()
    {
        var previous = _hash!.Text;
        _hash.Text = "invalid";
        Mutate(true);
        var rejected = LastDiagnostics.Contains("item.operations.content_governance_invalid", StringComparison.Ordinal);
        _hash.Text = previous;
        RebuildSummary();
        return rejected;
    }

    public void Refresh(BlueprintId? preferred = null)
    {
        var state = _load?.Invoke();
        _items?.Clear();
        var candidates = state?.Project.Blueprints.Where(item => item.Kind == BlueprintKind.Item)
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal).ToArray() ?? [];
        foreach (var blueprint in candidates)
        {
            var index = _items!.AddItem($"{blueprint.DisplayName} · {blueprint.Slug} · " +
                (blueprint.ItemFoundation?.Operations is null ? "opérations vides" : "opérations P1"));
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
        _items!.Select(selectedIndex);
        Load(candidates[selectedIndex]);
    }

    private void Build()
    {
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 18);
        AddChild(margin);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);
        var title = new Label { Text = "LOOT, INVENTAIRE & ÉVÉNEMENTS P1" };
        title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new("ff9858"));
        root.AddChild(title);
        root.AddChild(new Label
        {
            Text = "Composez le loot, prévisualisez les déplacements et liez les neuf événements d’objet.",
        });
        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [280],
        };
        root.AddChild(split);
        var catalog = Panel("OBJETS");
        catalog.CustomMinimumSize = new(285, 0);
        split.AddChild(catalog);
        _items = new ItemList { CustomMinimumSize = new(270, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _items.ItemSelected += index => SelectItem((int)index);
        catalog.GetChild<VBoxContainer>(0).AddChild(_items);
        var editor = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [650],
        };
        split.AddChild(editor);
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(610, 0),
        };
        editor.AddChild(scroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(form);
        _required = Check(form, "Opérations objet requises au runtime");
        var governance = new GridContainer { Columns = 2 };
        form.AddChild(governance);
        _manifest = Field(governance, "Manifeste", "project-content");
        _license = Field(governance, "Licence SPDX", "CC0-1.0");
        _hash = Field(form, "SHA-256 du contenu", new string('0', 64));
        Section(form, "LOOT TABLE · PONDÉRATION & PREVIEW");
        _lootEntries = Multi(form, "item UUID | groupe | poids | min | max | qualité | condition | unique", 105);
        var lootGrid = new GridContainer { Columns = 2 };
        form.AddChild(lootGrid);
        _lootConditions = Field(lootGrid, "Conditions actives", "crypt-open");
        _seed = Spin(lootGrid, "Seed", 0, int.MaxValue, 42);
        Section(form, "INSPECTEUR · DRAG-DROP, FILTRES & MULTI-EDIT");
        _placements = Multi(form, "instance | conteneur | slot | x | y | quantité | tags | preview", 105);
        _selectedItems = Field(form, "Instances sélectionnées", "UUID, UUID");
        var moveGrid = new GridContainer { Columns = 4 };
        form.AddChild(moveGrid);
        _targetContainer = CompactField(moveGrid, "Conteneur cible", "vault");
        _targetSlot = CompactField(moveGrid, "Slot cible", "optionnel");
        _gridX = Spin(moveGrid, "Grille X", 0, 10000, 0);
        _gridY = Spin(moveGrid, "Grille Y", 0, 10000, 0);
        var filterGrid = new GridContainer { Columns = 3 };
        form.AddChild(filterGrid);
        _filterContainer = CompactField(filterGrid, "Filtre conteneur", "backpack");
        _filterTag = CompactField(filterGrid, "Filtre tag", "quest");
        _filterText = CompactField(filterGrid, "Recherche", "UUID ou tag");
        Section(form, "ÉVÉNEMENTS · FORGESCRIPT 1.0");
        _events = Multi(form, "événement | handler | autorité", 155);
        var summaryPanel = Panel("APERÇU");
        summaryPanel.CustomMinimumSize = new(365, 0);
        editor.AddChild(summaryPanel);
        var summaryBox = summaryPanel.GetChild<VBoxContainer>(0);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new(350, 540),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        summaryBox.AddChild(_summary);
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(350, 140) };
        summaryBox.AddChild(_feedback);
        var actions = new HBoxContainer();
        root.AddChild(actions);
        AddButton(actions, "Prévisualiser", () => Mutate(true));
        AddButton(actions, "Appliquer", () => Mutate(false), true);
        AddButton(actions, "Annuler", () => { _undo?.Invoke(); Refresh(); });
        AddButton(actions, "Rétablir", () => { _redo?.Invoke(); Refresh(); });
        AddButton(actions, "Fermer", Hide);
        ConnectPreviewSignals(form);
    }

    private void Load(BlueprintDefinition blueprint)
    {
        _selectedId = blueprint.Id;
        _foundation = blueprint.ItemFoundation ?? ItemFoundationProfile.CreateDefault(Guid.NewGuid(), blueprint.Slug,
            blueprint.DisplayName, "toolset:r090");
        var profile = _foundation.Operations ?? DefaultOperations();
        _profileId = profile.Id;
        _lootId = profile.Loot.Id;
        _inspectorId = profile.Inspector.Id;
        _eventIds.Clear();
        foreach (var binding in profile.Events.Bindings) _eventIds[binding.Kind] = binding.Id;
        _required!.ButtonPressed = profile.RequiredAtRuntime;
        _manifest!.Text = profile.PackageManifestId;
        _license!.Text = profile.LicenseSpdx;
        _hash!.Text = profile.ContentSha256;
        _lootEntries!.Text = string.Join("\n", profile.Loot.Entries.Select(item =>
            $"{item.ItemDefinitionId:D}|{item.Group}|{item.Weight.ToString(CultureInfo.InvariantCulture)}|" +
            $"{item.MinimumQuantity}|{item.MaximumQuantity}|{item.MinimumQuality}|{item.Condition}|{item.Unique}"));
        _lootConditions!.Text = string.Empty;
        _seed!.Value = 42;
        _placements!.Text = string.Join("\n", profile.Inspector.Placements.Select(item =>
            $"{item.ItemInstanceId:D}|{item.ContainerId}|{item.SlotId}|{item.GridX}|{item.GridY}|{item.Quantity}|" +
            $"{Join(item.Tags)}|{item.PreviewAssetId}"));
        _selectedItems!.Text = string.Empty;
        _targetContainer!.Text = "vault";
        _targetSlot!.Text = string.Empty;
        _gridX!.Value = 0;
        _gridY!.Value = 0;
        _filterContainer!.Text = string.Empty;
        _filterTag!.Text = string.Empty;
        _filterText!.Text = string.Empty;
        _events!.Text = string.Join("\n", profile.Events.Bindings.OrderBy(item => item.Kind).Select(item =>
            $"{item.Kind}|{item.HandlerId}|{item.Authority}"));
        RebuildSummary();
    }

    private ItemFoundationProfile BuildFoundation() => _foundation! with { Operations = BuildOperations() };

    private ItemOperationsProfile BuildOperations()
    {
        var bindings = ParseEvents(_events!.Text);
        return new(_profileId, 1, "toolset:r090", _required!.ButtonPressed, _manifest!.Text, _license!.Text,
            _hash!.Text, new(_lootId, 1, "toolset:r090", ParseLoot(_lootEntries!.Text)),
            new(_inspectorId, 1, "toolset:r090", _foundation?.Operations?.Inspector.Revision ?? 0,
                ParsePlacements(_placements!.Text), true, true, true), new("1.0", bindings));
    }

    private void Mutate(bool dryRun)
    {
        if (_selectedId is null || _mutate is null) return;
        var result = _mutate(_selectedId, BuildFoundation(), false, dryRun);
        LastCommandId = result.Audit.CommandId.Value;
        LastPatchSummary = result.Patch?.Summary ?? string.Empty;
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
        var foundation = BuildFoundation();
        var profile = foundation.Operations!;
        var diagnostics = ItemFoundationValidator.Validate(foundation);
        var resolved = ItemOperationsRuntimeAdapter.Resolve(foundation);
        var conditions = Values(_lootConditions!.Text).ToHashSet(StringComparer.Ordinal);
        var lootPreview = resolved.Loot is null ? [] : ItemGameplayService.Preview(resolved.Loot, conditions);
        var roll = resolved.Loot is null ? [] : ItemGameplayService.Roll(resolved.Loot, (int)_seed!.Value, conditions);
        var selected = Guids(_selectedItems!.Text);
        InventoryEditResult? move = null;
        IReadOnlyList<InventoryPlacement> filtered = [];
        if (resolved.Inventory is not null)
        {
            filtered = InventoryAuthoringService.Filter(resolved.Inventory, EmptyToNull(_filterContainer!.Text),
                EmptyToNull(_filterTag!.Text), EmptyToNull(_filterText!.Text));
            if (selected.Count > 0)
            {
                try
                {
                    move = InventoryAuthoringService.Move(resolved.Inventory,
                        new(selected, _targetContainer!.Text, EmptyToNull(_targetSlot!.Text),
                            (int)_gridX!.Value, (int)_gridY!.Value, true), resolved.Inventory.Revision);
                }
                catch (InvalidOperationException exception)
                {
                    move = new(resolved.Inventory, [], [], [exception.Message], string.Empty, string.Empty);
                }
            }
        }
        SummarySectionCount = 3;
        _summary.Text = $"[color=#ffad70][b]LOOT TABLE[/b][/color]\n" +
            $"{profile.Loot.Entries.Count} entrée(s) · seed {(int)_seed!.Value} · preview {lootPreview.Count}\n" +
            $"tirage {string.Join(", ", roll.Select(item => $"{item.ItemDefinitionId:D} x{item.Quantity} q{item.Quality}"))}\n\n" +
            $"[color=#ffad70][b]INSPECTEUR D’INVENTAIRE[/b][/color]\n" +
            $"révision {profile.Inspector.Revision} · {profile.Inspector.Placements.Count} placements · " +
            $"{filtered.Count} filtré(s)\n" +
            (move is null ? "Aucun déplacement sélectionné" : move.Diagnostics.Count == 0
                ? $"[color=#8ce0bd]Preview multi-edit : {move.After.Count} objet(s) → {_targetContainer!.Text}[/color]"
                : $"[color=#ffbd7a]{string.Join(", ", move.Diagnostics)}[/color]") + "\n\n" +
            $"[color=#ffad70][b]ÉVÉNEMENTS D’OBJET[/b][/color]\n" +
            $"ForgeScript {profile.Events.ForgeScriptApiVersion} · {profile.Events.Bindings.Count}/9 bindings\n" +
            $"{string.Join(" · ", profile.Events.Bindings.Select(item => item.Kind))}\n\n" +
            (diagnostics.Count == 0 ? "[color=#8ce0bd]Document canonique valide[/color]" :
                $"[color=#ff7777]{diagnostics.Count} diagnostic(s) · {string.Join(", ", diagnostics.Select(item => item.Code))}[/color]");
    }

    private ItemOperationsProfile DefaultOperations()
    {
        var bindings = Enum.GetValues<ItemLifecycleEventType>().Select(kind =>
            new ItemLifecycleBindingPolicy(Guid.NewGuid(), 1, "toolset:r090", kind, $"On{kind}", "server")).ToArray();
        return new(Guid.NewGuid(), 1, "toolset:r090", false, "project-content", "CC0-1.0",
            new string('0', 64), new(Guid.NewGuid(), 1, "toolset:r090",
            [new(Guid.NewGuid(), "default", 1, 1, 1, 0, string.Empty, false)]),
            new(Guid.NewGuid(), 1, "toolset:r090", 0,
                [new(Guid.NewGuid(), "inventory", null, 0, 0, 1, ["default"], "asset.item.placeholder")],
                true, true, true), new("1.0", bindings));
    }

    private IReadOnlyList<ItemLifecycleBindingPolicy> ParseEvents(string text) => Lines(text).Select(line =>
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        var kind = parts.Length > 0 && Enum.TryParse<ItemLifecycleEventType>(parts[0], out var parsed)
            ? parsed
            : (ItemLifecycleEventType)(-1);
        var id = _eventIds.TryGetValue(kind, out var existing) ? existing : Guid.NewGuid();
        return new ItemLifecycleBindingPolicy(id, 1, "toolset:r090", kind,
            parts.Length > 1 ? parts[1] : string.Empty, parts.Length > 2 ? parts[2] : string.Empty);
    }).ToArray();

    private static IReadOnlyList<ItemLootEntryPolicy> ParseLoot(string text) => Lines(text).Select(line =>
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        return new ItemLootEntryPolicy(GuidValue(parts, 0), Part(parts, 1), Double(parts, 2), Int(parts, 3),
            Int(parts, 4), Int(parts, 5), Part(parts, 6), Bool(parts, 7));
    }).ToArray();

    private static IReadOnlyList<ItemInventoryPlacementPolicy> ParsePlacements(string text) => Lines(text).Select(line =>
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        return new ItemInventoryPlacementPolicy(GuidValue(parts, 0), Part(parts, 1), EmptyToNull(Part(parts, 2)),
            Int(parts, 3), Int(parts, 4), Int(parts, 5), Values(Part(parts, 6)), Part(parts, 7));
    }).ToArray();

    private void SelectItem(int index)
    {
        if (_items?.GetItemMetadata(index).AsString() is not { Length: > 0 } id) return;
        var blueprint = _load?.Invoke()?.Project.Blueprints.SingleOrDefault(item => item.Id.ToString() == id);
        if (blueprint is not null) Load(blueprint);
    }

    private static string[] Lines(string text) => text.Split(['\r', '\n'],
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string Part(string[] parts, int index) => parts.Length > index ? parts[index] : string.Empty;
    private static Guid GuidValue(string[] parts, int index) => Guid.TryParse(Part(parts, index), out var id) ? id : Guid.Empty;
    private static int Int(string[] parts, int index) => int.TryParse(Part(parts, index),
        NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : -1;
    private static double Double(string[] parts, int index) => double.TryParse(Part(parts, index),
        NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN;
    private static bool Bool(string[] parts, int index) => bool.TryParse(Part(parts, index), out var value) && value;
    private static IReadOnlyList<Guid> Guids(string text) => text.Split(',',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty).ToArray();
    private static string[] Values(string text) => text.Split(',', StringSplitOptions.RemoveEmptyEntries |
        StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    private static string Join(IEnumerable<string> values) => string.Join(",", values);
    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private void ConnectPreviewSignals(Node owner)
    {
        foreach (var child in owner.GetChildren())
        {
            switch (child)
            {
                case LineEdit line: line.TextChanged += _ => RebuildSummary(); break;
                case TextEdit text: text.TextChanged += RebuildSummary; break;
                case SpinBox spin: spin.ValueChanged += _ => RebuildSummary(); break;
                case CheckBox check: check.Toggled += _ => RebuildSummary(); break;
            }
            ConnectPreviewSignals(child);
        }
    }
    private static void Section(Container owner, string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new("ffad70"));
        owner.AddChild(label);
    }
    private static LineEdit Field(Container owner, string label, string placeholder)
    {
        owner.AddChild(new Label { Text = label });
        var field = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        owner.AddChild(field);
        return field;
    }
    private static LineEdit CompactField(Container owner, string label, string placeholder)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddChild(new Label { Text = label });
        var field = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddChild(field);
        owner.AddChild(box);
        return field;
    }
    private static TextEdit Multi(Container owner, string label, float height)
    {
        owner.AddChild(new Label { Text = label });
        var field = new TextEdit { CustomMinimumSize = new(0, height) };
        owner.AddChild(field);
        return field;
    }
    private static CheckBox Check(Container owner, string text)
    {
        var field = new CheckBox { Text = text };
        owner.AddChild(field);
        return field;
    }
    private static SpinBox Spin(Container owner, string label, double minimum, double maximum, double value)
    {
        var box = new VBoxContainer();
        box.AddChild(new Label { Text = label });
        var field = new SpinBox { MinValue = minimum, MaxValue = maximum, Value = value, Step = 1 };
        box.AddChild(field);
        owner.AddChild(box);
        return field;
    }
    private static PanelContainer Panel(string title)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);
        var label = new Label { Text = title };
        label.AddThemeColorOverride("font_color", new("ffad70"));
        box.AddChild(label);
        return panel;
    }
    private static void AddButton(Container owner, string text, Action action, bool primary = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new(120, 36) };
        if (primary) button.AddThemeColorOverride("font_color", new("fff4ec"));
        button.Pressed += action;
        owner.AddChild(button);
    }
}
#endif
