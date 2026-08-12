#if TOOLS
using System.Globalization;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Items;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class ItemGameplayWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<BlueprintId, ItemFoundationProfile?, bool, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private ItemList? _items;
    private BlueprintId? _selectedId;
    private ItemFoundationProfile? _foundation;
    private Guid _gameplayId;
    private Guid _modifierId;
    private Guid _abilityId;
    private CheckBox? _required;
    private LineEdit? _manifest;
    private LineEdit? _license;
    private LineEdit? _hash;
    private readonly Dictionary<string, CheckBox> _critical = new(StringComparer.Ordinal);
    private CheckBox? _allowGameMasterOverride;
    private readonly Dictionary<ItemTransferDestination, CheckBox> _transfers = [];
    private LineEdit? _permissions;
    private LineEdit? _modifierKind;
    private SpinBox? _modifierValue;
    private OptionButton? _stacking;
    private LineEdit? _condition;
    private LineEdit? _modifierSummary;
    private LineEdit? _beforeHook;
    private LineEdit? _afterHook;
    private CheckBox? _abilityEnabled;
    private LineEdit? _targeting;
    private LineEdit? _costs;
    private SpinBox? _chargeCost;
    private SpinBox? _cooldown;
    private LineEdit? _requestedEvent;
    private LineEdit? _resolvedEvent;
    private TextEdit? _slotDefinitions;
    private LineEdit? _requiredSlots;
    private LineEdit? _occupiedSlots;
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
        _load = load; _mutate = mutate; _undo = undo; _redo = redo; _applyIcon = applyIcon;
    }

    public override void _Ready()
    {
        Title = "Gameplay d’objet · OpenRPG Forge";
        MinSize = new(1120, 760); Size = new(1440, 920); CloseRequested += Hide;
        Build(); Refresh();
        var capture = OS.GetCmdlineUserArgs().FirstOrDefault(value =>
            value.StartsWith("--r089-item-gameplay-ui-capture=", StringComparison.Ordinal));
        if (capture is not null)
        {
            _capturePath = capture["--r089-item-gameplay-ui-capture=".Length..];
            SetProcess(true);
        }
    }

    public override void _Process(double delta)
    {
        if (_capturePath is null) return;
        _captureElapsed += delta;
        if (!_capturePrepared && _captureElapsed >= 4)
        {
            _capturePrepared = PrepareForCapture(); _captureElapsed = 0; return;
        }
        if (!_capturePrepared || !Visible || _captureElapsed < 2) return;
        var destination = Path.GetFullPath(_capturePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var error = GetViewport().GetTexture().GetImage().SavePng(destination);
        GD.Print($"OPENRPG_FORGE_R089_ITEM_GAMEPLAY_UI_CAPTURE_{(error == Error.Ok ? "PASS" : "FAILED")} " +
                 $"path={destination}");
        _capturePath = null; GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public void Open() { Refresh(); PopupCentered(); _applyIcon?.Invoke(this); }

    public bool ConfigureForSmokeTest()
    {
        var candidate = _load?.Invoke()?.Project.Blueprints.FirstOrDefault(item => item.Kind == BlueprintKind.Item);
        Refresh(candidate?.Id);
        if (_selectedId is null) return false;
        _required!.ButtonPressed = true; _manifest!.Text = "crypt-of-ashes"; _license!.Text = "CC0-1.0";
        _hash!.Text = new string('9', 64);
        foreach (var check in _critical.Values) check.ButtonPressed = true;
        _allowGameMasterOverride!.ButtonPressed = true;
        foreach (var (destination, check) in _transfers)
            check.ButtonPressed = destination is ItemTransferDestination.Container or ItemTransferDestination.GameMaster;
        _permissions!.Text = "item.transfer.quest";
        _modifierKind!.Text = "ward"; _modifierValue!.Value = 8; Select(_stacking!, "highest");
        _condition!.Text = "night"; _modifierSummary!.Text = "+8 ward at night";
        _beforeHook!.Text = "BeforeItemModifier"; _afterHook!.Text = "AfterItemModifier";
        _abilityEnabled!.ButtonPressed = true; _targeting!.Text = "point"; _costs!.Text = "focus=1";
        _chargeCost!.Value = 1; _cooldown!.Value = 12000;
        _requestedEvent!.Text = "ItemAbilityRequested"; _resolvedEvent!.Text = "ItemAbilityResolved";
        _slotDefinitions!.Text = "hand-r|forge|humanoid|2|hands-two; " +
                                 "hand-l|forge|humanoid|2|hands-two; " +
                                 "hands-two|forge|humanoid|3|hand-r,hand-l";
        _requiredSlots!.Text = "hand-r, hand-l"; _occupiedSlots!.Text = "hands-two";
        RebuildSummary();
        _feedback!.Text = "[color=#8ce0bd]Profil gameplay prêt pour prévisualisation.[/color]";
        return SummarySectionCount == 5 && ItemFoundationValidator.Validate(BuildFoundation()).Count == 0;
    }

    public bool PrepareForCapture() { var result = ConfigureForSmokeTest(); Open(); ConfigureForSmokeTest(); return result; }
    public bool PreviewForSmokeTest() { Mutate(true); return LastCommandId != Guid.Empty && LastDiagnostics.Length == 0; }
    public bool ApplyForSmokeTest() { Mutate(false); return LastCommandId != Guid.Empty && LastDiagnostics.Length == 0; }
    public bool RejectInvalidForSmokeTest()
    {
        var previous = _hash!.Text; _hash.Text = "invalid"; Mutate(true);
        var rejected = LastDiagnostics.Contains("item.gameplay.content_governance_invalid", StringComparison.Ordinal);
        _hash.Text = previous; RebuildSummary(); return rejected;
    }

    public void Refresh(BlueprintId? preferred = null)
    {
        var state = _load?.Invoke(); _items?.Clear();
        var candidates = state?.Project.Blueprints.Where(item => item.Kind == BlueprintKind.Item)
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal).ToArray() ?? [];
        foreach (var blueprint in candidates)
        {
            var index = _items!.AddItem($"{blueprint.DisplayName} · {blueprint.Slug} · " +
                (blueprint.ItemFoundation?.Gameplay is null ? "gameplay vide" : "gameplay P1"));
            _items.SetItemMetadata(index, blueprint.Id.ToString());
        }
        if (candidates.Length == 0)
        {
            _feedback!.Text = "[color=#ffbd7a]État vide : créez d’abord un blueprint objet.[/color]"; return;
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
        var title = new Label { Text = "GAMEPLAY D’OBJET P1" }; title.AddThemeFontSizeOverride("font_size", 24);
        title.AddThemeColorOverride("font_color", new("ff9858")); root.AddChild(title);
        root.AddChild(new Label { Text = "Protéger, transférer, modifier, activer et équiper sans quitter le document canonique." });
        var split = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [280]
        }; root.AddChild(split);
        var catalog = Panel("OBJETS"); catalog.CustomMinimumSize = new(285, 0); split.AddChild(catalog);
        _items = new ItemList { CustomMinimumSize = new(270, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _items.ItemSelected += index => SelectItem((int)index); catalog.GetChild<VBoxContainer>(0).AddChild(_items);
        var editor = new HSplitContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SplitOffsets = [650]
        }; split.AddChild(editor);
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new(610, 0)
        }; editor.AddChild(scroll);
        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 7); scroll.AddChild(form);
        _required = Check(form, "Gameplay objet requis au runtime");
        var governance = new GridContainer { Columns = 2 }; form.AddChild(governance);
        _manifest = Field(governance, "Manifeste", "project-content");
        _license = Field(governance, "Licence SPDX", "CC0-1.0");
        _hash = Field(form, "SHA-256 du contenu", new string('0', 64));
        Section(form, "OBJET PLOT · PROTECTIONS");
        var criticalGrid = new GridContainer { Columns = 4 }; form.AddChild(criticalGrid);
        foreach (var (key, label) in new[] { ("destroy", "Destruction"), ("sale", "Vente"),
                     ("drop", "Drop"), ("theft", "Vol"), ("consume", "Consommation"),
                     ("cleanup", "Nettoyage") })
            _critical[key] = Check(criticalGrid, label);
        _allowGameMasterOverride = Check(criticalGrid, "Override MJ audité");
        Section(form, "TRANSFERT · DESTINATIONS & PERMISSIONS");
        var transferGrid = new GridContainer { Columns = 4 }; form.AddChild(transferGrid);
        foreach (var destination in Enum.GetValues<ItemTransferDestination>())
            _transfers[destination] = Check(transferGrid, destination.ToString());
        _permissions = Field(form, "Permissions requises", "item.transfer.quest");
        Section(form, "PROPRIÉTÉ MAGIQUE DATA-DRIVEN");
        var modifierGrid = new GridContainer { Columns = 4 }; form.AddChild(modifierGrid);
        _modifierKind = CompactField(modifierGrid, "Type", "ward");
        _modifierValue = Spin(modifierGrid, "Valeur", -100000, 100000, 0, .25);
        _stacking = Option(modifierGrid, "Stacking", ["sum", "highest", "lowest"]);
        _condition = CompactField(modifierGrid, "Condition", "night");
        _modifierSummary = Field(form, "Résumé UI", "+8 ward at night");
        var hooks = new GridContainer { Columns = 2 }; form.AddChild(hooks);
        _beforeHook = Field(hooks, "Hook avant", "BeforeItemModifier");
        _afterHook = Field(hooks, "Hook après", "AfterItemModifier");
        Section(form, "POUVOIR UNIQUE");
        _abilityEnabled = Check(form, "Capacité activable");
        var abilityGrid = new GridContainer { Columns = 4 }; form.AddChild(abilityGrid);
        _targeting = CompactField(abilityGrid, "Ciblage", "point");
        _costs = CompactField(abilityGrid, "Coûts", "focus=1");
        _chargeCost = Spin(abilityGrid, "Charges", 1, 100000, 1);
        _cooldown = Spin(abilityGrid, "Cooldown ms", 0, 86400000, 0);
        var events = new GridContainer { Columns = 2 }; form.AddChild(events);
        _requestedEvent = Field(events, "Événement demandé", "ItemAbilityRequested");
        _resolvedEvent = Field(events, "Événement résolu", "ItemAbilityResolved");
        Section(form, "ÉQUIPEMENT · MULTI-SLOT & CONFLITS");
        _slotDefinitions = Multi(form, "Slots id|ruleset|rig|layer|conflits ; …", 80);
        var equipmentGrid = new GridContainer { Columns = 2 }; form.AddChild(equipmentGrid);
        _requiredSlots = Field(equipmentGrid, "Slots requis", "hand-r, hand-l");
        _occupiedSlots = Field(equipmentGrid, "Slots occupés (aperçu)", "hands-two");
        var summaryPanel = Panel("APERÇU DES RÈGLES"); summaryPanel.CustomMinimumSize = new(365, 0);
        editor.AddChild(summaryPanel); var summaryBox = summaryPanel.GetChild<VBoxContainer>(0);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new(350, 540),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        }; summaryBox.AddChild(_summary);
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(350, 140) };
        summaryBox.AddChild(_feedback);
        var actions = new HBoxContainer(); root.AddChild(actions);
        AddButton(actions, "Prévisualiser", () => Mutate(true)); AddButton(actions, "Appliquer", () => Mutate(false), true);
        AddButton(actions, "Annuler", () => { _undo?.Invoke(); Refresh(); });
        AddButton(actions, "Rétablir", () => { _redo?.Invoke(); Refresh(); }); AddButton(actions, "Fermer", Hide);
        ConnectPreviewSignals(form);
    }

    private void Load(BlueprintDefinition blueprint)
    {
        _selectedId = blueprint.Id;
        _foundation = blueprint.ItemFoundation ?? ItemFoundationProfile.CreateDefault(Guid.NewGuid(), blueprint.Slug,
            blueprint.DisplayName, "toolset:r089");
        var profile = _foundation.Gameplay ?? DefaultGameplay();
        _gameplayId = profile.Id; _modifierId = profile.Modifiers.FirstOrDefault()?.Id ?? Guid.NewGuid();
        _abilityId = profile.Ability?.AbilityDefinitionId ?? Guid.NewGuid();
        _required!.ButtonPressed = profile.RequiredAtRuntime; _manifest!.Text = profile.PackageManifestId;
        _license!.Text = profile.LicenseSpdx; _hash!.Text = profile.ContentSha256;
        _critical["destroy"].ButtonPressed = profile.Critical.ProtectDestruction;
        _critical["sale"].ButtonPressed = profile.Critical.ProtectSale;
        _critical["drop"].ButtonPressed = profile.Critical.ProtectDrop;
        _critical["theft"].ButtonPressed = profile.Critical.ProtectTheft;
        _critical["consume"].ButtonPressed = profile.Critical.ProtectConsumption;
        _critical["cleanup"].ButtonPressed = profile.Critical.ProtectCleanup;
        _allowGameMasterOverride!.ButtonPressed = profile.Critical.AllowAuditedGameMasterOverride;
        foreach (var (destination, check) in _transfers)
            check.ButtonPressed = profile.Transfer.AllowedDestinations.GetValueOrDefault(destination);
        _permissions!.Text = Join(profile.Transfer.RequiredPermissions);
        var modifier = profile.Modifiers.FirstOrDefault();
        _modifierKind!.Text = modifier?.Kind ?? "ward";
        _modifierValue!.Value = modifier?.Parameters.GetValueOrDefault("value") ?? 0;
        Select(_stacking!, modifier?.StackingRule ?? "sum"); _condition!.Text = modifier?.Condition ?? string.Empty;
        _modifierSummary!.Text = modifier?.UiSummary ?? "No modifier";
        _beforeHook!.Text = modifier?.BeforeHook ?? string.Empty; _afterHook!.Text = modifier?.AfterHook ?? string.Empty;
        _abilityEnabled!.ButtonPressed = profile.Ability is not null; _targeting!.Text = profile.Ability?.Targeting ?? "point";
        _costs!.Text = profile.Ability is null ? "focus=1" : JoinPairs(profile.Ability.Costs);
        _chargeCost!.Value = profile.Ability?.ChargeCost ?? 1; _cooldown!.Value = profile.Ability?.CooldownMilliseconds ?? 0;
        _requestedEvent!.Text = profile.Ability?.RequestedEvent ?? "ItemAbilityRequested";
        _resolvedEvent!.Text = profile.Ability?.ResolvedEvent ?? "ItemAbilityResolved";
        _slotDefinitions!.Text = string.Join("; ", profile.Equipment.SlotDefinitions.Select(slot =>
            $"{slot.Id}|{slot.Ruleset}|{slot.Rig}|{slot.Layer}|{Join(slot.Conflicts)}"));
        _requiredSlots!.Text = Join(profile.Equipment.RequiredSlots); _occupiedSlots!.Text = string.Empty;
        RebuildSummary();
    }

    private ItemFoundationProfile BuildFoundation() => _foundation! with { Gameplay = BuildGameplay() };

    private ItemGameplayProfile BuildGameplay()
    {
        var critical = new ItemCriticalPolicy(_critical["destroy"].ButtonPressed, _critical["sale"].ButtonPressed,
            _critical["drop"].ButtonPressed, _critical["theft"].ButtonPressed,
            _critical["consume"].ButtonPressed, _critical["cleanup"].ButtonPressed,
            _allowGameMasterOverride!.ButtonPressed);
        var transfer = new ItemTransferPolicy(_transfers.ToDictionary(item => item.Key, item => item.Value.ButtonPressed),
            Values(_permissions!.Text));
        var modifier = new ItemModifierPolicy(_modifierId, 1, "toolset:r089", _modifierKind!.Text,
            new Dictionary<string, double> { ["value"] = _modifierValue!.Value },
            _stacking!.GetItemText(_stacking.Selected), EmptyToNull(_condition!.Text), _modifierSummary!.Text,
            EmptyToNull(_beforeHook!.Text), EmptyToNull(_afterHook!.Text));
        var ability = _abilityEnabled!.ButtonPressed ? new ItemAbilityPolicy(_abilityId, _targeting!.Text,
            Pairs(_costs!.Text), (int)_chargeCost!.Value, (long)_cooldown!.Value,
            _requestedEvent!.Text, _resolvedEvent!.Text) : null;
        return new(_gameplayId, 1, "toolset:r089", _required!.ButtonPressed, _manifest!.Text, _license!.Text,
            _hash!.Text, critical, transfer, [modifier], ability,
            new(ParseSlots(_slotDefinitions!.Text), Values(_requiredSlots!.Text)));
    }

    private void Mutate(bool dryRun)
    {
        if (_selectedId is null || _mutate is null) return;
        var result = _mutate(_selectedId, BuildFoundation(), false, dryRun);
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
        var foundation = BuildFoundation(); var profile = foundation.Gameplay!;
        var diagnostics = ItemFoundationValidator.Validate(foundation);
        var adapted = Forge.RpgKit.Domain.Items.ItemGameplayRuntimeAdapter.Resolve(foundation);
        var modifierValues = adapted.Modifiers.Count == 0 ? new Dictionary<string, double>() :
            Forge.RpgKit.Domain.Items.ItemGameplayService.ResolveModifiers(adapted.Modifiers,
                Values(_condition!.Text).ToHashSet(StringComparer.Ordinal));
        var occupied = Values(_occupiedSlots!.Text).ToHashSet(StringComparer.Ordinal);
        var equipment = adapted.Slots.Count == 0 ? null : Forge.RpgKit.Domain.Items.ItemGameplayService.PreviewEquipment(
            new(Guid.Empty, adapted.RequiredSlots, adapted.Slots.Values.First().Ruleset,
                adapted.Slots.Values.First().Rig), adapted.Slots, occupied);
        var maximumLayer = profile.Equipment.SlotDefinitions.Count == 0
            ? "not-defined"
            : profile.Equipment.SlotDefinitions.Max(item => item.Layer).ToString(CultureInfo.InvariantCulture);
        SummarySectionCount = 5;
        _summary.Text = $"[color=#ffad70][b]OBJET PLOT[/b][/color]\n" +
            $"{_critical.Values.Count(item => item.ButtonPressed)}/6 protections · override MJ " +
            $"{(_allowGameMasterOverride!.ButtonPressed ? "audité" : "interdit")}\n\n" +
            $"[color=#ffad70][b]TRANSFERT[/b][/color]\n" +
            $"{_transfers.Count(item => item.Value.ButtonPressed)}/8 destinations · permissions {Join(profile.Transfer.RequiredPermissions)}\n\n" +
            $"[color=#ffad70][b]PROPRIÉTÉ MAGIQUE[/b][/color]\n" +
            $"{profile.Modifiers[0].UiSummary} · {profile.Modifiers[0].StackingRule} · " +
            $"résolu {string.Join(", ", modifierValues.Select(item => $"{item.Key}={item.Value:0.##}"))}\n" +
            $"hooks {profile.Modifiers[0].BeforeHook} → {profile.Modifiers[0].AfterHook}\n\n" +
            $"[color=#ffad70][b]POUVOIR UNIQUE[/b][/color]\n" +
            (profile.Ability is null ? "Désactivé\n\n" :
                $"{profile.Ability.AbilityDefinitionId:D}\n{profile.Ability.Targeting} · charge {profile.Ability.ChargeCost} · " +
                $"{profile.Ability.CooldownMilliseconds} ms\n{profile.Ability.RequestedEvent} → {profile.Ability.ResolvedEvent}\n\n") +
            $"[color=#ffad70][b]ÉQUIPEMENT & VALIDATION[/b][/color]\n" +
            $"{profile.Equipment.RequiredSlots.Count} slot(s) requis · couche " +
            $"{maximumLayer}\n" +
            (equipment is null ? "Aucun aperçu" : equipment.Allowed ? "[color=#8ce0bd]Aperçu compatible[/color]" :
                $"[color=#ffbd7a]Conflits : {string.Join(", ", equipment.Conflicts)}[/color]") + "\n" +
            (diagnostics.Count == 0 ? "[color=#8ce0bd]Document canonique valide[/color]" :
                $"[color=#ff7777]{diagnostics.Count} diagnostic(s) · {string.Join(", ", diagnostics.Select(item => item.Code))}[/color]");
    }

    private ItemGameplayProfile DefaultGameplay() => new(Guid.NewGuid(), 1, "toolset:r089", false,
        "project-content", "CC0-1.0", new string('0', 64),
        new(false, false, false, false, false, false, false),
        new(Enum.GetValues<ItemTransferDestination>().ToDictionary(item => item, item => true), []),
        [new(Guid.NewGuid(), 1, "toolset:r089", "utility", new Dictionary<string, double> { ["value"] = 0 },
            "sum", null, "Utility modifier", null, null)], null,
        new([new("inventory", "forge", "none", 0, [])], ["inventory"]));

    private void SelectItem(int index)
    {
        if (_items?.GetItemMetadata(index).AsString() is not { Length: > 0 } id) return;
        var blueprint = _load?.Invoke()?.Project.Blueprints.SingleOrDefault(item => item.Id.ToString() == id);
        if (blueprint is not null) Load(blueprint);
    }

    private static IReadOnlyList<ItemEquipmentSlotPolicy> ParseSlots(string text) => text.Split(';',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(entry =>
        {
            var parts = entry.Split('|', StringSplitOptions.TrimEntries);
            return parts.Length >= 4 ? new ItemEquipmentSlotPolicy(parts[0], parts[1], parts[2],
                int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var layer) ? layer : -1,
                parts.Length > 4 ? Values(parts[4]) : []) : new(string.Empty, string.Empty, string.Empty, -1, []);
        }).ToArray();
    private static IReadOnlyDictionary<string, double> Pairs(string text) => text.Split(',',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(entry => entry.Split('=',
        StringSplitOptions.TrimEntries)).ToDictionary(parts => parts[0], parts => parts.Length > 1 &&
        double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : double.NaN,
        StringComparer.Ordinal);
    private static string JoinPairs(IReadOnlyDictionary<string, double> values) => string.Join(", ", values.Select(item =>
        $"{item.Key}={item.Value.ToString(CultureInfo.InvariantCulture)}"));
    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static string[] Values(string text) => text.Split(',', StringSplitOptions.RemoveEmptyEntries |
        StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    private static string Join(IEnumerable<string> values) => string.Join(", ", values);
    private static void Select(OptionButton option, string value)
    {
        for (var index = 0; index < option.ItemCount; index++)
            if (option.GetItemText(index) == value) { option.Select(index); return; }
    }
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
                case OptionButton option: option.ItemSelected += _ => RebuildSummary(); break;
            }
            ConnectPreviewSignals(child);
        }
    }
    private static void Section(Container owner, string text)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new("ffad70")); owner.AddChild(label);
    }
    private static LineEdit Field(Container owner, string label, string placeholder)
    {
        owner.AddChild(new Label { Text = label }); var field = new LineEdit
        {
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        }; owner.AddChild(field); return field;
    }
    private static LineEdit CompactField(Container owner, string label, string placeholder)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddChild(new Label { Text = label }); var field = new LineEdit
        {
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        }; box.AddChild(field); owner.AddChild(box); return field;
    }
    private static TextEdit Multi(Container owner, string label, float height)
    {
        owner.AddChild(new Label { Text = label });
        var field = new TextEdit { CustomMinimumSize = new(0, height) }; owner.AddChild(field); return field;
    }
    private static CheckBox Check(Container owner, string text)
    {
        var field = new CheckBox { Text = text };
        owner.AddChild(field); return field;
    }
    private static SpinBox Spin(Container owner, string label, double minimum, double maximum, double value,
        double step = 1)
    {
        var box = new VBoxContainer(); box.AddChild(new Label { Text = label });
        var field = new SpinBox
        {
            MinValue = minimum,
            MaxValue = maximum,
            Value = value,
            Step = step,
            AllowGreater = false,
            AllowLesser = false
        }; box.AddChild(field); owner.AddChild(box); return field;
    }
    private static OptionButton Option(Container owner, string label, IEnumerable<string> items)
    {
        var box = new VBoxContainer(); box.AddChild(new Label { Text = label }); var field = new OptionButton();
        foreach (var item in items) field.AddItem(item); box.AddChild(field); owner.AddChild(box); return field;
    }
    private static PanelContainer Panel(string title)
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal =
        Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        }; var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8); panel.AddChild(box); var label = new Label { Text = title };
        label.AddThemeColorOverride("font_color", new("ffad70")); box.AddChild(label); return panel;
    }
    private static void AddButton(Container owner, string text, Action action, bool primary = false)
    {
        var button = new Button { Text = text, CustomMinimumSize = new(120, 36) };
        if (primary) button.AddThemeColorOverride("font_color", new("fff4ec"));
        button.Pressed += action; owner.AddChild(button);
    }
}
#endif
