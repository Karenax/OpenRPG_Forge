using System.Globalization;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioCreatureCapabilitiesOverlayUx4H : Control
{
    private StudioSessionController _session;
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioCreatureCapabilityEvidenceSourceSnapshotUx4H? _sourceSnapshot;
    private BlueprintId? _evidenceBlueprintId;
    private bool _usesRealProfile;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _updating;
    private bool _applied;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioCreatureCapabilityChoiceUx4H> _choices = [];
    private IReadOnlyList<StudioItemChoiceUx4H> _itemChoices = [];
    private StudioCreatureCapabilityDraftUx4H? _draft;
    private StudioCreatureCapabilityPreviewUx4H? _previewResult;
    private readonly List<CreatureRulesAbilityInstance> _abilities = [];
    private readonly List<CreatureRulesInventoryContainer> _containers = [];
    private readonly Dictionary<string, Guid> _equipment = new(StringComparer.Ordinal);

    private readonly OptionButton _creature;
    private readonly Label _context;
    private readonly TabContainer _tabs;
    private readonly ItemList _abilityList;
    private readonly LineEdit _abilityId;
    private readonly LineEdit _abilityLevel;
    private readonly LineEdit _abilityCharges;
    private readonly LineEdit _abilityCooldown;
    private readonly LineEdit _abilityConditions;
    private readonly LineEdit _abilityTags;
    private readonly LineEdit _abilityScript;
    private readonly LineEdit _owner;
    private readonly CheckBox _persistent;
    private readonly ItemList _containerList;
    private readonly LineEdit _containerId;
    private readonly LineEdit _containerSlots;
    private readonly LineEdit _containerWeight;
    private readonly LineEdit _containerVolume;
    private readonly ItemList _itemList;
    private readonly OptionButton _itemBlueprint;
    private readonly LineEdit _itemQuantity;
    private readonly LineEdit _itemWeight;
    private readonly LineEdit _itemVolume;
    private readonly ItemList _equipmentList;
    private readonly LineEdit _equipmentSlot;
    private readonly OptionButton _equipmentEntry;
    private readonly RichTextLabel _summary;
    private readonly VBoxContainer _diagnostics;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action? CreatureChanged;
    internal event Action<string, bool>? StatusChanged;
    internal bool IsOpen => Visible;
    internal int ChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _creature.Selected > 0 && _draft is not null;
    internal bool CanonicalPreviewReady => _previewResult?.CanApply == true;
    internal bool ShowsAbilities => _abilities.Count > 0 && _summary.Text.Contains("Capacités", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsInventory => _containers.Count > 0 && _summary.Text.Contains("Inventaire", StringComparison.OrdinalIgnoreCase);
    internal bool ShowsEquipment => _equipment.Count > 0;
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Capacités spéciales", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Inventaire de créature", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Aperçu et validation", StringComparison.OrdinalIgnoreCase) && !ContainsGuid(text);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealProfileEvidence => _usesRealProfile;
    internal bool UsesCanonicalFallbackEvidence => _usesFallback;
    internal bool EvidencePreviewDryRun => _previewDryRun;
    internal bool EvidenceSourceUntouched => _sourceSnapshot is not null && SourceMatches(_sourceSnapshot);
    internal bool EvidenceResourcesReleased => _resourcesReleased;
    internal bool SourceSessionRestored => _sourceRestored;
    internal string LastEvidenceFailure => _lastFailure;

    internal StudioCreatureCapabilitiesOverlayUx4H(StudioSessionController session)
    {
        _session = session; _sourceSession = session; Name = "StudioCreatureCapabilitiesOverlayUx4H";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); MouseFilter = MouseFilterEnum.Stop; Visible = false;
        var shade = new ColorRect { Color = new Color(0.012f, 0.018f, 0.03f, 0.94f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(shade);
        var dialog = StudioStyle.Panel("CreatureCapabilitiesDialogUx4H", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dialog.OffsetLeft = 58; dialog.OffsetTop = 30; dialog.OffsetRight = -58; dialog.OffsetBottom = -30;
        AddChild(dialog);
        var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 7); dialog.AddChild(root);
        var header = new HBoxContainer();
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddChild(StudioStyle.Label("CAPACITÉS ET INVENTAIRE DE CRÉATURE · UX4-H", 17,
            StudioStyle.Accent, true));
        titles.AddChild(StudioStyle.Label(
            "Capacités spéciales, conteneurs, objets et équipement · aucun UUID ni JSON affiché.",
            10, StudioStyle.Muted, wrap: true)); header.AddChild(titles);
        var close = StudioStyle.Button("Fermer", compact: true); close.Pressed += Close; header.AddChild(close);
        root.AddChild(header);
        var contextRow = new HBoxContainer(); contextRow.AddChild(StudioStyle.Label("Créature", 9, StudioStyle.Muted));
        _creature = new OptionButton { CustomMinimumSize = new Vector2(390, 30) }; contextRow.AddChild(_creature);
        _context = StudioStyle.Label("Sélection explicite requise · la fondation UX4-D doit exister.",
            9, StudioStyle.Muted, wrap: true); _context.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        contextRow.AddChild(_context); root.AddChild(contextRow);
        var tabHost = new Control
        {
            CustomMinimumSize = new Vector2(0, 410),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true
        }; root.AddChild(tabHost);
        _tabs = new TabContainer(); _tabs.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); tabHost.AddChild(_tabs);

        var abilities = Tab(_tabs, "1 · Capacités spéciales");
        abilities.AddChild(StudioStyle.Label("Capacités spéciales", 11, StudioStyle.Accent));
        abilities.AddChild(StudioStyle.Label(
            "Niveau, charges, cooldown, conditions, tags IA et liaison script ; aucune capacité n’est exécutée.",
            9, StudioStyle.Muted, wrap: true));
        _abilityList = List(abilities, 120);
        var abilityGrid = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _abilityId = GridField(abilityGrid, "Clé de capacité", "ability.guardian-shout");
        _abilityLevel = GridField(abilityGrid, "Niveau", "1");
        _abilityCharges = GridField(abilityGrid, "Charges", "0");
        _abilityCooldown = GridField(abilityGrid, "Cooldown (s)", "0");
        _abilityConditions = GridField(abilityGrid, "Conditions (virgules)", "combat.active");
        _abilityTags = GridField(abilityGrid, "Tags IA (virgules)", "defense,guardian");
        _abilityScript = GridField(abilityGrid, "Clé script", "script.guardian-shout");
        abilityGrid.AddChild(new Control()); abilities.AddChild(abilityGrid);
        abilities.AddChild(CollectionButtons("Nouvelle capacité", () => SaveAbility(), RemoveAbility, ClearAbility));

        var inventory = Tab(_tabs, "2 · Inventaire de créature");
        inventory.AddChild(StudioStyle.Label("Inventaire de créature", 11, StudioStyle.Accent));
        inventory.AddChild(StudioStyle.Label(
            "Objets issus uniquement du catalogue Item réel ; limites, ownership et équipement sont validés ensemble.",
            9, StudioStyle.Muted, wrap: true));
        var inventoryHeader = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _owner = GridField(inventoryHeader, "Owner", "creature.owner");
        var persistence = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        persistence.AddChild(StudioStyle.Label("Persistance", 9, StudioStyle.Muted));
        _persistent = new CheckBox { Text = "Inventaire persistant" }; persistence.AddChild(_persistent);
        inventoryHeader.AddChild(persistence); inventory.AddChild(inventoryHeader);
        var inventoryColumns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var containers = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        containers.AddChild(StudioStyle.Label("Conteneurs", 10, StudioStyle.Text));
        _containerList = List(containers, 105); _containerId = Field(containers, "Clé", "container.backpack");
        var containerGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _containerSlots = GridField(containerGrid, "Slots", "8");
        _containerWeight = GridField(containerGrid, "Poids max", "100");
        _containerVolume = GridField(containerGrid, "Volume max", "100"); containers.AddChild(containerGrid);
        containers.AddChild(CollectionButtons("Nouveau conteneur", () => SaveContainer(), RemoveContainer,
            ClearContainer)); inventoryColumns.AddChild(containers);
        var items = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        items.AddChild(StudioStyle.Label("Objets du conteneur sélectionné", 10, StudioStyle.Text));
        _itemList = List(items, 105); _itemBlueprint = Choice(items, "Objet réel", []);
        var itemGrid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _itemQuantity = GridField(itemGrid, "Quantité", "1");
        _itemWeight = GridField(itemGrid, "Poids unitaire", "1");
        _itemVolume = GridField(itemGrid, "Volume unitaire", "1"); items.AddChild(itemGrid);
        items.AddChild(CollectionButtons("Nouvel objet", () => SaveItem(), RemoveItem, ClearItem));
        inventoryColumns.AddChild(items);
        var equipment = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        equipment.AddChild(StudioStyle.Label("Équipement", 10, StudioStyle.Text));
        _equipmentList = List(equipment, 105); _equipmentSlot = Field(equipment, "Slot", "relic");
        _equipmentEntry = Choice(equipment, "Entrée d’inventaire", []);
        equipment.AddChild(CollectionButtons("Nouvel équipement", () => SaveEquipment(), RemoveEquipment,
            ClearEquipment)); inventoryColumns.AddChild(equipment); inventory.AddChild(inventoryColumns);

        var review = Tab(_tabs, "3 · Aperçu et validation");
        review.AddChild(StudioStyle.Label("Aperçu et validation", 11, StudioStyle.Accent));
        review.AddChild(StudioStyle.Label(
            "Projection Domain/Application uniquement : aucun script, objet, équipement, acteur ou réseau n’est exécuté.",
            9, StudioStyle.Muted, wrap: true)); _diagnostics = new VBoxContainer(); review.AddChild(_diagnostics);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 145),
            FitContent = false,
            SelectionEnabled = true
        }; root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Prévisualiser ne modifie rien. Appliquer écrit une commande annulable ; Enregistrer persiste ensuite le workspace.",
            9, StudioStyle.Muted, wrap: true));
        var footer = new HBoxContainer(); _preview = StudioStyle.Button("Prévisualiser", primary: true, compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true); _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true); _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        { button.CustomMinimumSize = new Vector2(button == _preview ? 132 : 104, 34); footer.AddChild(button); }
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }); root.AddChild(footer);
        ConnectEvents(); RenderEmpty("Choisissez explicitement une créature."); SetEditingEnabled(false);
    }

    internal void Open()
    { if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence(); _session = _sourceSession; OpenBoundSession(); }
    internal void RestoreSourceSurfaceAfterSmoke() { _session = _sourceSession; OpenBoundSession(); }
    internal void Close()
    {
        if (!Visible && ReferenceEquals(_session, _sourceSession) && _evidenceSession is null && _evidenceRoot is null) return;
        _session.CancelCreatureCapabilityUx4H(); _previewResult = null; _draft = null; Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }
    private void OpenBoundSession()
    {
        _session.CancelCreatureCapabilityUx4H(); _previewResult = null; _draft = null; _applied = false;
        _choices = _session.CreatureCapabilityChoicesUx4H; _itemChoices = _session.ItemChoicesUx4H;
        _updating = true; _creature.Clear(); _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label); _creature.Select(0); _updating = false;
        PopulateItemChoices(); ClearControls(); Visible = true; SetEditingEnabled(false);
        _context.Text = _choices.Count == 0 ? "Aucune créature éditable dans ce module." :
            "Sélection explicite requise · la fondation UX4-D doit exister.";
        RenderEmpty(_choices.Count == 0 ? "État vide réel : aucune créature éditable." :
            "Choisissez une créature pour ouvrir ses capacités et son inventaire.");
    }
    private void SelectCreature(long selected)
    {
        if (_updating) return; _session.CancelCreatureCapabilityUx4H(); _previewResult = null; _apply.Disabled = true;
        if (selected <= 0 || selected > _choices.Count)
        { _draft = null; ClearControls(); SetEditingEnabled(false); RenderEmpty("Sélection explicite requise."); return; }
        var choice = _choices[(int)selected - 1]; _draft = _session.CreateCreatureCapabilityDraftUx4H(choice.BlueprintId);
        if (_draft is null)
        { ClearControls(); SetEditingEnabled(false); RenderEmpty(choice.IsReadOnly ? "Blueprint installé en lecture seule." : "Fondation UX4-D requise."); return; }
        Populate(_draft); _context.Text = $"{choice.Label} · {_itemChoices.Count} objet(s) réel(s) disponible(s)";
        SetEditingEnabled(true); RenderEmpty(choice.HasRules ? "Composition chargée. Modifiez puis prévisualisez." :
            "Règles absentes : une composition complète est proposée sans écriture avant Appliquer.");
    }
    private void Populate(StudioCreatureCapabilityDraftUx4H draft)
    {
        _updating = true; _abilities.Clear(); _abilities.AddRange(draft.Abilities); _containers.Clear();
        _containers.AddRange(draft.Containers); _equipment.Clear(); foreach (var item in draft.EquipmentSlots) _equipment[item.Key] = item.Value;
        _owner.Text = draft.OwnerId; _persistent.ButtonPressed = draft.Persistent;
        RebuildAbilityList(0); RebuildContainerList(0); RebuildEquipmentList(0); _updating = false;
    }
    private StudioCreatureCapabilityDraftUx4H? ReadDraft(bool commitEditors = true)
    {
        if (_draft is null || commitEditors && !CommitEditors()) return null;
        return _draft with
        {
            Abilities = _abilities.ToArray(),
            Containers = _containers.ToArray(),
            EquipmentSlots = _equipment.ToDictionary(),
            OwnerId = _owner.Text,
            Persistent = _persistent.ButtonPressed
        };
    }
    private bool CommitEditors()
    {
        if (Selected(_abilityList) >= 0 && !SaveAbility(false)) return false;
        if (Selected(_containerList) >= 0 && !SaveContainer(false)) return false;
        if (Selected(_itemList) >= 0 && !SaveItem(false)) return false;
        if (Selected(_equipmentList) >= 0 && !SaveEquipment(false)) return false;
        return true;
    }

    private bool SaveAbility(bool showStatus = true)
    {
        if (!TryInt(_abilityLevel.Text, out var level) || !TryInt(_abilityCharges.Text, out var charges) ||
            !TryDouble(_abilityCooldown.Text, out var cooldown))
        { if (showStatus) SetStatus("Niveau, charges ou cooldown invalides.", true); return false; }
        var selected = Selected(_abilityList); var id = selected >= 0 && selected < _abilities.Count
            ? _abilities[selected].Id : StableId("ability:" + _abilityId.Text + ":" + _abilities.Count);
        var item = new CreatureRulesAbilityInstance(id, _abilityId.Text, level, charges, cooldown,
            Csv(_abilityConditions.Text), Csv(_abilityTags.Text), _abilityScript.Text);
        if (selected >= 0 && selected < _abilities.Count) _abilities[selected] = item; else _abilities.Add(item);
        RebuildAbilityList(selected >= 0 ? selected : _abilities.Count - 1); InvalidatePreview(); return true;
    }
    private bool SaveContainer(bool showStatus = true)
    {
        if (!TryInt(_containerSlots.Text, out var slots) || !TryDouble(_containerWeight.Text, out var weight) ||
            !TryDouble(_containerVolume.Text, out var volume))
        { if (showStatus) SetStatus("Slots, poids ou volume invalides.", true); return false; }
        var selected = Selected(_containerList); var source = selected >= 0 && selected < _containers.Count ? _containers[selected] : null;
        var item = new CreatureRulesInventoryContainer(source?.Id ?? StableId("container:" + _containerId.Text + ":" + _containers.Count),
            _containerId.Text, slots, weight, volume, source?.Items ?? []);
        if (selected >= 0 && selected < _containers.Count) _containers[selected] = item; else _containers.Add(item);
        RebuildContainerList(selected >= 0 ? selected : _containers.Count - 1); InvalidatePreview(); return true;
    }
    private bool SaveItem(bool showStatus = true)
    {
        var containerIndex = Selected(_containerList); var blueprintIndex = _itemBlueprint.Selected;
        if (containerIndex < 0 || containerIndex >= _containers.Count || blueprintIndex < 0 || blueprintIndex >= _itemChoices.Count ||
            !TryInt(_itemQuantity.Text, out var quantity) || !TryDouble(_itemWeight.Text, out var weight) ||
            !TryDouble(_itemVolume.Text, out var volume))
        { if (showStatus) SetStatus("Conteneur, objet ou valeurs d’inventaire invalides.", true); return false; }
        var container = _containers[containerIndex]; var items = container.Items.ToList(); var selected = Selected(_itemList);
        var id = selected >= 0 && selected < items.Count ? items[selected].Id : StableId($"item:{container.Id}:{items.Count}");
        var item = new CreatureRulesInventoryEntry(id, _itemChoices[blueprintIndex].BlueprintId, quantity, weight, volume);
        if (selected >= 0 && selected < items.Count) items[selected] = item; else items.Add(item);
        _containers[containerIndex] = container with { Items = items.ToArray() };
        RebuildItemList(containerIndex, selected >= 0 ? selected : items.Count - 1); RebuildEquipmentEntryChoices();
        InvalidatePreview(); return true;
    }
    private bool SaveEquipment(bool showStatus = true)
    {
        var entries = InventoryEntries(); var entryIndex = _equipmentEntry.Selected;
        if (string.IsNullOrWhiteSpace(_equipmentSlot.Text) || entryIndex < 0 || entryIndex >= entries.Count)
        { if (showStatus) SetStatus("Slot ou entrée d’équipement invalide.", true); return false; }
        var selected = Selected(_equipmentList); if (selected >= 0 && selected < _equipment.Count)
            _equipment.Remove(_equipment.Keys.Order(StringComparer.Ordinal).ElementAt(selected));
        _equipment[_equipmentSlot.Text] = entries[entryIndex].Entry.Id;
        RebuildEquipmentList(_equipment.Keys.Order(StringComparer.Ordinal).ToList().IndexOf(_equipmentSlot.Text));
        InvalidatePreview(); return true;
    }
    private void RemoveAbility() => RemoveAt(_abilities, _abilityList, RebuildAbilityList);
    private void RemoveContainer()
    {
        var selected = Selected(_containerList); if (selected < 0 || selected >= _containers.Count) return;
        var ids = _containers[selected].Items.Select(item => item.Id).ToHashSet(); _containers.RemoveAt(selected);
        foreach (var slot in _equipment.Where(item => ids.Contains(item.Value)).Select(item => item.Key).ToArray()) _equipment.Remove(slot);
        RebuildContainerList(Math.Min(selected, _containers.Count - 1)); RebuildEquipmentList(0); InvalidatePreview();
    }
    private void RemoveItem()
    {
        var containerIndex = Selected(_containerList); var selected = Selected(_itemList);
        if (containerIndex < 0 || selected < 0) return; var container = _containers[containerIndex]; var items = container.Items.ToList();
        if (selected >= items.Count) return; var id = items[selected].Id; items.RemoveAt(selected);
        _containers[containerIndex] = container with { Items = items.ToArray() };
        foreach (var slot in _equipment.Where(item => item.Value == id).Select(item => item.Key).ToArray()) _equipment.Remove(slot);
        RebuildItemList(containerIndex, Math.Min(selected, items.Count - 1)); RebuildEquipmentList(0); InvalidatePreview();
    }
    private void RemoveEquipment()
    {
        var selected = Selected(_equipmentList); var keys = _equipment.Keys.Order(StringComparer.Ordinal).ToArray();
        if (selected < 0 || selected >= keys.Length) return; _equipment.Remove(keys[selected]);
        RebuildEquipmentList(Math.Min(selected, _equipment.Count - 1)); InvalidatePreview();
    }
    private void RemoveAt<T>(List<T> values, ItemList list, Action<int> rebuild)
    { var selected = Selected(list); if (selected < 0 || selected >= values.Count) return; values.RemoveAt(selected); rebuild(Math.Min(selected, values.Count - 1)); InvalidatePreview(); }

    private void SelectAbility(long index)
    {
        if (_updating || index < 0 || index >= _abilities.Count) return; var item = _abilities[(int)index]; _updating = true;
        _abilityId.Text = item.AbilityId; _abilityLevel.Text = item.Level.ToString(); _abilityCharges.Text = item.Charges.ToString();
        _abilityCooldown.Text = Format(item.CooldownSeconds); _abilityConditions.Text = string.Join(',', item.Conditions);
        _abilityTags.Text = string.Join(',', item.AiTags); _abilityScript.Text = item.ScriptId; _updating = false;
    }
    private void SelectContainer(long index)
    {
        if (_updating || index < 0 || index >= _containers.Count) return; var item = _containers[(int)index]; _updating = true;
        _containerId.Text = item.ContainerId; _containerSlots.Text = item.SlotCount.ToString();
        _containerWeight.Text = Format(item.MaximumWeight); _containerVolume.Text = Format(item.MaximumVolume);
        _updating = false; RebuildItemList((int)index, 0);
    }
    private void SelectItem(long index)
    {
        var containerIndex = Selected(_containerList); if (_updating || containerIndex < 0 || index < 0) return;
        var items = _containers[containerIndex].Items; if (index >= items.Count) return; var item = items[(int)index]; _updating = true;
        SelectItemChoice(item.ItemBlueprintId); _itemQuantity.Text = item.Quantity.ToString();
        _itemWeight.Text = Format(item.UnitWeight); _itemVolume.Text = Format(item.UnitVolume); _updating = false;
    }
    private void SelectEquipment(long index)
    {
        var keys = _equipment.Keys.Order(StringComparer.Ordinal).ToArray(); if (_updating || index < 0 || index >= keys.Length) return;
        _updating = true; _equipmentSlot.Text = keys[(int)index]; SelectEquipmentEntry(_equipment[keys[(int)index]]); _updating = false;
    }
    private void RebuildAbilityList(int selected) => Rebuild(_abilityList,
        _abilities.Select(item => $"{item.AbilityId} · niveau {item.Level} · {item.Charges} charge(s) · {item.CooldownSeconds:0.##} s"), selected, SelectAbility);
    private void RebuildContainerList(int selected)
    {
        Rebuild(_containerList, _containers.Select(item => $"{item.ContainerId} · {item.Items.Count}/{item.SlotCount} entrée(s)"), selected, SelectContainer);
        if (_containers.Count == 0) _itemList.Clear(); RebuildEquipmentEntryChoices();
    }
    private void RebuildItemList(int containerIndex, int selected)
    {
        if (containerIndex < 0 || containerIndex >= _containers.Count) { _itemList.Clear(); return; }
        Rebuild(_itemList, _containers[containerIndex].Items.Select(item =>
            $"{HumanItemLabel(item.ItemBlueprintId)} · quantité {item.Quantity} · {item.UnitWeight:0.##} kg"), selected, SelectItem);
    }
    private void RebuildEquipmentList(int selected)
    {
        Rebuild(_equipmentList, _equipment.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item =>
        $"{item.Key} · {HumanEntryLabel(item.Value)}"), selected, SelectEquipment); RebuildEquipmentEntryChoices();
    }
    private void Rebuild(ItemList list, IEnumerable<string> labels, int selected, Action<long> select)
    {
        _updating = true; list.Clear(); foreach (var label in labels) list.AddItem(label);
        if (selected >= 0 && selected < list.ItemCount) { list.Select(selected); _updating = false; select(selected); }
        else _updating = false;
    }
    private void ClearAbility() { _abilityList.DeselectAll(); SetText(_abilityId, "ability.guardian-shout"); SetText(_abilityLevel, "1"); SetText(_abilityCharges, "0"); SetText(_abilityCooldown, "0"); SetText(_abilityConditions, ""); SetText(_abilityTags, "guardian"); SetText(_abilityScript, "script.guardian-shout"); }
    private void ClearContainer() { _containerList.DeselectAll(); _itemList.Clear(); SetText(_containerId, "container.backpack"); SetText(_containerSlots, "8"); SetText(_containerWeight, "100"); SetText(_containerVolume, "100"); }
    private void ClearItem() { _itemList.DeselectAll(); if (_itemBlueprint.ItemCount > 0) _itemBlueprint.Select(0); SetText(_itemQuantity, "1"); SetText(_itemWeight, "1"); SetText(_itemVolume, "1"); }
    private void ClearEquipment() { _equipmentList.DeselectAll(); SetText(_equipmentSlot, "relic"); if (_equipmentEntry.ItemCount > 0) _equipmentEntry.Select(0); }

    private void Preview()
    {
        var draft = ReadDraft(); if (draft is null) { RenderEmpty("Un éditeur contient une valeur invalide."); return; }
        var project = _session.Project; var history = _session.History; var revision = history?.State.Revision;
        var journal = history?.Journal.ToArray() ?? []; var dirty = _session.IsDirty;
        _previewResult = _session.PreviewCreatureCapabilityUx4H(draft);
        _previewDryRun = project is not null && history is not null && ReferenceEquals(project, _session.Project) &&
                         ReferenceEquals(history, _session.History) && history.State.Revision == revision &&
                         history.Journal.SequenceEqual(journal) && _session.IsDirty == dirty;
        RenderPreview(_previewResult, _previewDryRun);
    }
    private void Apply()
    {
        var draft = ReadDraft(commitEditors: false); if (draft is null) return;
        var result = _session.ApplyApprovedCreatureCapabilityUx4H(draft); _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded); if (!result.Succeeded) { _apply.Disabled = true; return; }
        CreatureChanged?.Invoke(); RefreshAfterMutation(draft.BlueprintId);
    }
    private void Undo() { var selected = _draft?.BlueprintId; var result = _session.UndoCreatureCapabilityUx4H(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) { CreatureChanged?.Invoke(); RefreshAfterMutation(selected); } }
    private void Redo() { var selected = _draft?.BlueprintId; var result = _session.RedoCreatureCapabilityUx4H(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) { CreatureChanged?.Invoke(); RefreshAfterMutation(selected); } }
    private void Save() { var result = _session.Save(); SetStatus(result.Message, !result.Succeeded); }
    private void RefreshAfterMutation(BlueprintId? selected)
    {
        _choices = _session.CreatureCapabilityChoicesUx4H; _itemChoices = _session.ItemChoicesUx4H; PopulateItemChoices();
        _updating = true; _creature.Clear(); _creature.AddItem("Choisir explicitement une créature…");
        foreach (var choice in _choices) _creature.AddItem(choice.Label); var index = selected is null ? -1 :
            _choices.ToList().FindIndex(item => item.BlueprintId == selected); _creature.Select(index >= 0 ? index + 1 : 0);
        _updating = false; SelectCreature(index >= 0 ? index + 1 : 0);
    }
    private void RenderPreview(StudioCreatureCapabilityPreviewUx4H result, bool dryRun)
    {
        _summary.Text = $"[b]{(result.Succeeded ? "APERÇU CANONIQUE" : "CONFIGURATION REFUSÉE")}[/b]\n" +
                        $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n\n" +
                        $"[color=#b8d9ff]Capacités[/color] · {result.AbilitySummary}\n" +
                        $"[color=#b8d9ff]Inventaire[/color] · {result.InventorySummary}\n" +
                        $"[color=#b8d9ff]Runtime[/color] · {result.RuntimeSummary}\n" +
                        $"[color={(dryRun ? "#8ce0bd" : "#ff7777")}]Aperçu sans mutation : {dryRun.ToString().ToLowerInvariant()}[/color]";
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        if (result.Diagnostics.Count == 0) _diagnostics.AddChild(StudioStyle.Label("Aucun diagnostic bloquant.", 10, StudioStyle.Success, true));
        else foreach (var item in result.Diagnostics.Take(7)) _diagnostics.AddChild(StudioStyle.Label($"{item.Code}\n{item.HumanMessage}", 9, StudioStyle.Danger, true));
        _apply.Disabled = !result.CanApply;
    }
    private void RenderEmpty(string message)
    { _summary.Text = $"[color=#b8a8ce]{message}[/color]"; foreach (var child in _diagnostics.GetChildren()) child.QueueFree(); _diagnostics.AddChild(StudioStyle.Label("Aucune écriture sans aperçu approuvé.", 10, StudioStyle.Muted, true)); _apply.Disabled = true; }
    private void InvalidatePreview() { if (_updating) return; _session.CancelCreatureCapabilityUx4H(); _previewResult = null; _apply.Disabled = true; }
    private void SetEditingEnabled(bool enabled)
    { _preview.Disabled = !enabled; _apply.Disabled = !enabled || !CanonicalPreviewReady; _undo.Disabled = !_session.CanUndo; _redo.Disabled = !_session.CanRedo; _save.Disabled = _session.Project is null; }
    private void ClearControls()
    {
        _updating = true; _abilities.Clear(); _containers.Clear(); _equipment.Clear();
        foreach (var list in new[] { _abilityList, _containerList, _itemList, _equipmentList }) list.Clear();
        _owner.Text = string.Empty; _persistent.ButtonPressed = false; _updating = false;
    }
    private void ConnectEvents()
    {
        _creature.ItemSelected += SelectCreature; _abilityList.ItemSelected += SelectAbility;
        _containerList.ItemSelected += SelectContainer; _itemList.ItemSelected += SelectItem;
        _equipmentList.ItemSelected += SelectEquipment; _preview.Pressed += Preview; _apply.Pressed += Apply;
        _undo.Pressed += Undo; _redo.Pressed += Redo; _save.Pressed += Save;
    }

    internal bool PrepareEvidence(string mode)
    {
        _lastFailure = "none"; _previewResult = null;
        if ((_evidenceSession is not null || _evidenceRoot is not null) && !ReleaseEvidence()) { _lastFailure = "release_previous"; return false; }
        if (!TryOpenEvidence()) { _lastFailure = _lastFailure == "none" ? "open" : _lastFailure; return false; }
        var index = _choices.ToList().FindIndex(item => item.BlueprintId == _evidenceBlueprintId);
        if (index < 0) { _lastFailure = "fixture_creature_missing"; Close(); return false; }
        _creature.Select(index + 1); SelectCreature(index + 1);
        if (_draft is null || _itemChoices.Count == 0) { _lastFailure = "draft_or_item"; Close(); return false; }
        _draft = StudioSessionController.ChangedCreatureCapabilityDraftUx4H(_draft, _itemChoices[0].BlueprintId); Populate(_draft);
        var project = _session.Project!; var history = _session.History!; var revision = history.State.Revision;
        var journal = history.Journal.ToArray(); var dirty = _session.IsDirty; Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched;
        _summary.Text = $"[b]{(_usesRealProfile ? "CAPTURE RÉELLE CRYPTE" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] · copie isolée, source strictement inchangée.\n{_summary.Text}";
        _tabs.CurrentTab = mode.Contains("invent", StringComparison.OrdinalIgnoreCase) ? 1 :
            mode.Contains("preview", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        var ready = _previewDryRun && CanonicalPreviewReady && HasExplicitSelection && ShowsHumanSurface &&
                    ShowsAbilities && ShowsInventory && ShowsEquipment && UsesIsolatedEvidence && EvidenceSourceUntouched &&
                    (_usesRealProfile ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:human={ShowsHumanSurface}:abilities={ShowsAbilities}:inventory={ShowsInventory}:equipment={ShowsEquipment}:isolated={UsesIsolatedEvidence}:source={EvidenceSourceUntouched}:origin={_usesRealProfile ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4H_EVIDENCE_FAILED failure={_lastFailure}"); Close(); return false;
        }
        SetStatus("Preuve UX4-H isolée prête · aucune donnée source modifiée.", false); return true;
    }
    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource(); if (snapshot is null) return false; StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-h-creature-capabilities-inventory") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session; var original = transaction.Project; var history = transaction.History;
            if (original is null || history is null) return false; Apply();
            var applied = _applied && transaction.Project is { } appliedProject && !ReferenceEquals(original, appliedProject);
            if (applied) Undo(); var undone = applied && ReferenceEquals(original, transaction.Project);
            if (undone) Redo(); var redoneProject = transaction.Project;
            var redone = undone && redoneProject is not null && !ReferenceEquals(original, redoneProject);
            var saved = redone && transaction.Save().Succeeded; var persisted = false;
            if (saved && transaction.Session is { } workspace)
            { reopened = new(); persisted = reopened.Open(workspace.RootDirectory).Succeeded && ProjectJsonSerializer.Serialize(reopened.Project!) == ProjectJsonSerializer.Serialize(redoneProject!); }
            var sourceUntouched = SourceMatches(snapshot); reopened?.Close(); reopened = null; Close();
            return applied && undone && redone && saved && persisted && sourceUntouched && EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        { GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4H_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}"); return false; }
        finally
        { reopened?.Close(); if (_evidenceSession is not null || _evidenceRoot is not null) Close(); _sourceSession.RestoreCreatureCapabilityApprovalUx4H(snapshot.Approval); }
    }
    internal void CloseEvidence() => Close();
    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource(); var evidence = _sourceSession.CreateUx4HEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4h-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !session.Open(root).Succeeded)
            { session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); return false; }
            _sourceSnapshot = snapshot; _evidenceSession = session; _evidenceRoot = root; _evidenceBlueprintId = evidence.BlueprintId;
            _session = session; _usesRealProfile = evidence.UsesRealProfile; _usesFallback = evidence.UsesCanonicalFallback;
            _previewDryRun = false; _resourcesReleased = false; _sourceRestored = false; OpenBoundSession();
            return SourceMatches(snapshot) && (_usesRealProfile ^ _usesFallback);
        }
        catch (Exception exception)
        { _lastFailure = "open_exception:" + exception.GetType().Name; session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); _session = _sourceSession; _sourceRestored = true; return false; }
    }
    private StudioCreatureCapabilityEvidenceSourceSnapshotUx4H? CaptureSource()
    {
        var project = _sourceSession.Project; var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureCreatureCapabilityApprovalUx4H());
    }
    private bool SourceMatches(StudioCreatureCapabilityEvidenceSourceSnapshotUx4H snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.IsDirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId && _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.SelectedEntityIds) &&
               _sourceSession.SelectedEntityId == snapshot.SelectedEntityId &&
               _sourceSession.CaptureCreatureCapabilityApprovalUx4H() == snapshot.Approval;
    }
    private bool ReleaseEvidence()
    {
        var session = _evidenceSession; var root = _evidenceRoot; _evidenceSession = null; _evidenceRoot = null;
        _sourceSnapshot = null; _evidenceBlueprintId = null; _session = _sourceSession; _usesRealProfile = false;
        _usesFallback = false; _previewDryRun = false; var released = true; try { session?.Close(); } catch { released = false; }
        var rootReleased = DeleteEvidenceRoot(root); if (!rootReleased) _evidenceRoot = root;
        _resourcesReleased = released && rootReleased; _sourceRestored = true; return _resourcesReleased;
    }
    private static bool DeleteEvidenceRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try
        {
            var full = Path.GetFullPath(root); var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("openrpg-forge-studio-ux4h-evidence-", StringComparison.Ordinal)) return false;
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true); return !Directory.Exists(full) && !File.Exists(full);
        }
        catch { return false; }
    }

    private void PopulateItemChoices()
    { _updating = true; _itemBlueprint.Clear(); foreach (var choice in _itemChoices) _itemBlueprint.AddItem(choice.Label); if (_itemChoices.Count > 0) _itemBlueprint.Select(0); _updating = false; RebuildEquipmentEntryChoices(); }
    private void RebuildEquipmentEntryChoices()
    { var entries = InventoryEntries(); _updating = true; _equipmentEntry.Clear(); foreach (var item in entries) _equipmentEntry.AddItem(item.Label); if (entries.Count > 0) _equipmentEntry.Select(0); _updating = false; }
    private IReadOnlyList<(Guid Id, CreatureRulesInventoryEntry Entry, string Label)> InventoryEntries() => _containers.SelectMany(container => container.Items.Select(item =>
        (item.Id, item, $"{container.ContainerId} · {HumanItemLabel(item.ItemBlueprintId)} · x{item.Quantity}"))).ToArray();
    private void SelectItemChoice(BlueprintId id) { var index = _itemChoices.ToList().FindIndex(item => item.BlueprintId == id); if (index >= 0) _itemBlueprint.Select(index); }
    private void SelectEquipmentEntry(Guid id) { var entries = InventoryEntries(); var index = entries.ToList().FindIndex(item => item.Id == id); if (index >= 0) _equipmentEntry.Select(index); }
    private string HumanItemLabel(BlueprintId id) => _itemChoices.FirstOrDefault(item => item.BlueprintId == id)?.Label ?? "Objet hérité protégé";
    private string HumanEntryLabel(Guid id) => InventoryEntries().FirstOrDefault(item => item.Id == id).Label ?? "Entrée héritée protégée";
    private Guid StableId(string segment) => StudioSessionController.StableCreatureRulesIdUx4F(_draft!.BlueprintId.Value, "ux4h-ui:" + segment);
    private static VBoxContainer Tab(TabContainer tabs, string title) { var scroll = new ScrollContainer { Name = title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto, FollowFocus = true }; var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; content.AddThemeConstantOverride("separation", 7); scroll.AddChild(content); tabs.AddChild(scroll); return content; }
    private static ItemList List(Control parent, float height) { var list = new ItemList { CustomMinimumSize = new Vector2(0, height), SizeFlagsHorizontal = SizeFlags.ExpandFill }; parent.AddChild(list); return list; }
    private static LineEdit Field(Control parent, string label, string placeholder) { var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted)); var field = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(field); parent.AddChild(box); return field; }
    private static LineEdit GridField(GridContainer grid, string label, string placeholder) => Field(grid, label, placeholder);
    private static OptionButton Choice(Control parent, string label, IEnumerable<string> choices) { var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(StudioStyle.Label(label, 9, StudioStyle.Muted)); var choice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill }; foreach (var item in choices) choice.AddItem(item); box.AddChild(choice); parent.AddChild(box); return choice; }
    private static HBoxContainer CollectionButtons(string addLabel, Action add, Action remove, Action clear) { var row = new HBoxContainer(); var create = StudioStyle.Button(addLabel, compact: true); var update = StudioStyle.Button("Mettre à jour", compact: true); var delete = StudioStyle.Button("Retirer", compact: true); create.Pressed += clear; update.Pressed += add; delete.Pressed += remove; row.AddChild(create); row.AddChild(update); row.AddChild(delete); return row; }
    private void SetStatus(string message, bool error) { StatusChanged?.Invoke(message, error); }
    private static int Selected(ItemList list) => list.GetSelectedItems().FirstOrDefault(-1);
    private static bool TryInt(string text, out int value) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    private static bool TryDouble(string text, out double value) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static IReadOnlyList<string> Csv(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static void SetText(LineEdit field, string text) => field.Text = text;
    private static IEnumerable<string> VisibleText(Node node) { foreach (var child in node.GetChildren()) { if (child is Label { Visible: true } label) yield return label.Text; if (child is Button { Visible: true } button) yield return button.Text; foreach (var value in VisibleText(child)) yield return value; } }
    private static bool ContainsGuid(string text) => System.Text.RegularExpressions.Regex.IsMatch(text, @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b");
}
