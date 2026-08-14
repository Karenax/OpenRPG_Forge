#if TOOLS
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Items;
using Godot;

namespace Forge.Toolset.Editor;

[Tool]
public partial class ItemAdvancedWindow : Window
{
    private Func<ProjectState?>? _load;
    private Func<BlueprintId, ItemAdvancedProfile?, bool, CommandResult>? _mutate;
    private Action? _undo;
    private Action? _redo;
    private Action<Window>? _applyIcon;
    private ItemList? _items;
    private BlueprintId? _selectedBlueprintId;
    private ItemAdvancedProfile? _loadedProfile;
    private Guid _profileId;
    private Guid? _loadedProcId;
    private Guid? _loadedLootContextId;
    private LineEdit? _currency; private SpinBox? _baseCost; private SpinBox? _minimumLevel; private SpinBox? _minimumLore;
    private LineEdit? _skills; private LineEdit? _traits; private LineEdit? _factions; private LineEdit? _condition;
    private SpinBox? _identification; private CheckBox? _partyKnowledge; private CheckBox? _stolen;
    private LineEdit? _owner; private LineEdit? _jurisdiction; private LineEdit? _procTrigger; private LineEdit? _procEffect;
    private SpinBox? _procChance; private SpinBox? _procCooldown; private LineEdit? _procCondition;
    private LineEdit? _procAttribution; private CheckBox? _procDeterministic;
    private CheckBox? _weapon; private LineEdit? _weaponClass; private SpinBox? _minDamage; private SpinBox? _maxDamage; private SpinBox? _reach;
    private CheckBox? _armor; private LineEdit? _armorMaterial; private SpinBox? _protection;
    private CheckBox? _ammo; private LineEdit? _ammoTag; private LineEdit? _projectile; private SpinBox? _stack;
    private CheckBox? _durability; private SpinBox? _durabilityMaximum; private SpinBox? _wear;
    private CheckBox? _container; private SpinBox? _slots; private SpinBox? _mass; private SpinBox? _depth;
    private LineEdit? _lootSource; private LineEdit? _biome; private LineEdit? _lootFaction; private LineEdit? _lootTags;
    private SpinBox? _lootMinimumLevel; private SpinBox? _lootMaximumLevel; private LineEdit? _lootDifficulty;
    private LineEdit? _lootPlayerCondition; private LineEdit? _lootEvent; private SpinBox? _lootWeight;
    private CheckBox? _unique; private LineEdit? _registryKey; private OptionButton? _uniqueScope;
    private OptionButton? _lootOwnership; private SpinBox? _ownershipExpiry;
    private CheckBox? _recipe; private LineEdit? _ingredient; private SpinBox? _ingredientQuantity; private LineEdit? _station; private LineEdit? _craftSkill;
    private CheckBox? _generator; private SpinBox? _budget; private LineEdit? _pool; private LineEdit? _affixKey; private LineEdit? _affixProperties;
    private RichTextLabel? _summary; private RichTextLabel? _feedback; private bool _lastSucceeded;

    public Guid LastCommandId { get; private set; }
    public string LastPatchSummary { get; private set; } = string.Empty;
    public string LastDiagnostics { get; private set; } = string.Empty;
    public BlueprintId? SelectedBlueprintId => _selectedBlueprintId;
    public int SummarySectionCount { get; private set; }

    public void Initialize(Func<ProjectState?> load,
        Func<BlueprintId, ItemAdvancedProfile?, bool, CommandResult> mutate, Action undo, Action redo,
        Action<Window> applyIcon)
    { _load = load; _mutate = mutate; _undo = undo; _redo = redo; _applyIcon = applyIcon; }

    public override void _Ready()
    {
        Title = "Objets avancés · OpenRPG Forge"; MinSize = new(1160, 760); Size = new(1450, 900);
        CloseRequested += Hide; Build(); Refresh();
    }

    public void Open() { Refresh(); PopupCentered(); _applyIcon?.Invoke(this); }

    public bool ConfigureForSmokeTest()
    {
        Refresh(); if (_selectedBlueprintId is null) return false;
        _currency!.Text = "gold"; _baseCost!.Value = 150; _minimumLevel!.Value = 1; _minimumLore!.Value = 2;
        _skills!.Text = "lore"; _traits!.Text = "ash-attuned"; _factions!.Text = "surface-alliance";
        _condition!.Text = "quest.accepted"; _identification!.Value = 12; _partyKnowledge!.ButtonPressed = true;
        _stolen!.ButtonPressed = true; _owner!.Text = "ash-watch"; _jurisdiction!.Text = "crypt";
        _procTrigger!.Text = "hit"; _procEffect!.Text = "ash-key-pulse"; _procChance!.Value = 1;
        _procCooldown!.Value = 0; _procCondition!.Text = string.Empty; _procAttribution!.Text = "wielder";
        _procDeterministic!.ButtonPressed = true;
        _weapon!.ButtonPressed = true; _weaponClass!.Text = "ritual-key"; _minDamage!.Value = 1;
        _maxDamage!.Value = 4; _reach!.Value = 1.2; _armor!.ButtonPressed = true;
        _armorMaterial!.Text = "ember-steel"; _protection!.Value = 5; _ammo!.ButtonPressed = true;
        _ammoTag!.Text = "ash-spark"; _projectile!.Text = "assets/projectiles/ash-spark.glb"; _stack!.Value = 12;
        _durability!.ButtonPressed = true; _durabilityMaximum!.Value = 40; _wear!.Value = 1;
        _container!.ButtonPressed = true; _slots!.Value = 4; _mass!.Value = 10; _depth!.Value = 2;
        _lootSource!.Text = "crypt-chest"; _biome!.Text = "crypt"; _lootFaction!.Text = "ash-watch"; _lootTags!.Text = "undead";
        _lootMinimumLevel!.Value = 0; _lootMaximumLevel!.Value = 100; _lootDifficulty!.Text = string.Empty;
        _lootPlayerCondition!.Text = string.Empty; _lootEvent!.Text = string.Empty; _lootWeight!.Value = 1;
        _unique!.ButtonPressed = true; _registryKey!.Text = "unique:ash-key"; Select(_uniqueScope!, ItemUniqueScope.World);
        Select(_lootOwnership!, ItemLootOwnershipMode.Personal); _ownershipExpiry!.Value = 120;
        _recipe!.ButtonPressed = true; _ingredient!.Text = "tag:ember-metal"; _ingredientQuantity!.Value = 2;
        _station!.Text = "forge"; _craftSkill!.Text = "smithing"; _generator!.ButtonPressed = true;
        _budget!.Value = 6; _pool!.Text = "ritual-key"; _affixKey!.Text = "keen"; _affixProperties!.Text = "critical";
        RebuildSummary(); return true;
    }

    public bool PreviewForSmokeTest() { Mutate(true); return _lastSucceeded; }
    public bool ApplyForSmokeTest() { Mutate(false); return _lastSucceeded; }

    public void Refresh()
    {
        var state = _load?.Invoke(); var previous = _selectedBlueprintId; _items?.Clear();
        var candidates = state?.Project.Blueprints.Where(item => item.Kind == BlueprintKind.Item)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray() ?? [];
        if (candidates.Length == 0) { SetEmpty("Aucun blueprint objet dans ce projet."); return; }
        foreach (var blueprint in candidates)
        {
            var profile = blueprint.ItemAdvanced;
            var index = _items!.AddItem($"{blueprint.DisplayName}\n{blueprint.Slug} · " +
                (profile is null ? "non configuré" : $"{profile.Economy.BaseCost} {profile.Economy.Currency} · {profile.Recipes.Count} recette(s)"));
            _items.SetItemMetadata(index, blueprint.Id.ToString());
        }
        var selected = Array.FindIndex(candidates, item => item.Id == previous); if (selected < 0) selected = 0;
        _items!.Select(selected); LoadItem(candidates[selected]);
    }

    private void Build()
    {
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (var side in new[] { "margin_left", "margin_top", "margin_right", "margin_bottom" }) margin.AddThemeConstantOverride(side, 22);
        AddChild(margin); var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 12); margin.AddChild(root);
        var title = new Label { Text = "OBJETS · ÉCONOMIE, ÉQUIPEMENT, BUTIN & CRAFT" }; title.AddThemeFontSizeOverride("font_size", 25);
        title.AddThemeColorOverride("font_color", new("ff9858")); root.AddChild(title);
        root.AddChild(new Label { Text = "Un profil unique relie l’inventaire, le combat, l’économie et la génération procédurale." });
        var split = new HSplitContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, SplitOffsets = [280] }; root.AddChild(split);
        var catalog = Panel("OBJETS DU MODULE"); split.AddChild(catalog); _items = new ItemList { CustomMinimumSize = new(280, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _items.ItemSelected += OnSelected; catalog.GetChild<VBoxContainer>(0).AddChild(_items);
        var editor = new HSplitContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SplitOffsets = [720] }; split.AddChild(editor);
        var formPanel = Panel("PROFIL CANONIQUE"); editor.AddChild(formPanel); var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        formPanel.GetChild<VBoxContainer>(0).AddChild(tabs);
        BuildFoundation(Tab(tabs, "IDENTITÉ & USAGE")); BuildEquipment(Tab(tabs, "ÉQUIPEMENT & CONTENEUR"));
        BuildLoot(Tab(tabs, "BUTIN, CRAFT & GÉNÉRATION"));
        var summaryPanel = Panel("APERÇU & SIMULATION"); editor.AddChild(summaryPanel); var box = summaryPanel.GetChild<VBoxContainer>(0);
        _summary = new RichTextLabel { BbcodeEnabled = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new(350, 430) };
        box.AddChild(_summary); box.AddChild(Button("Actualiser la simulation", RebuildSummary));
        _feedback = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new(350, 150) }; box.AddChild(_feedback);
        var actions = new HBoxContainer(); root.AddChild(actions); actions.AddChild(Button("Prévisualiser", () => Mutate(true)));
        actions.AddChild(Button("Appliquer", () => Mutate(false), true)); actions.AddChild(Button("Annuler", () => { _undo?.Invoke(); Refresh(); }));
        actions.AddChild(Button("Rétablir", () => { _redo?.Invoke(); Refresh(); })); actions.AddChild(Button("Fermer", Hide));
    }

    private void BuildFoundation(VBoxContainer form)
    {
        _currency = Field(form, "Monnaie", "gold"); _baseCost = Spin(form, "Valeur de base", 0, 1_000_000, 10, 1);
        _minimumLevel = Spin(form, "Niveau minimal", 0, 100, 0, 1); _minimumLore = Spin(form, "Connaissance minimale", 0, 100, 0, 1);
        _skills = Field(form, "Compétences requises (virgules)", "lore,smithing"); _traits = Field(form, "Traits requis", "ash-attuned");
        _factions = Field(form, "Factions autorisées", "surface-alliance"); _condition = Field(form, "Condition de script", "quest.accepted");
        _identification = Spin(form, "Difficulté d’identification", 0, 100, 10, 1); _partyKnowledge = Check(form, "Partager l’identification au groupe");
        _stolen = Check(form, "Objet volé"); _owner = Field(form, "Propriétaire", "ash-watch"); _jurisdiction = Field(form, "Juridiction", "crypt");
        _procTrigger = Field(form, "Déclencheur de l'effet", "hit");
        _procEffect = Field(form, "Effet au toucher", "ash-key-pulse"); _procChance = Spin(form, "Chance au toucher", 0, 1, .25, .05);
        _procCooldown = Spin(form, "Délai interne (s)", 0, 86_400, 0, .1);
        _procCondition = Field(form, "Condition de l'effet (facultative)", "quest.accepted");
        _procAttribution = Field(form, "Attribution de l'effet", "wielder");
        _procDeterministic = Check(form, "Résolution déterministe");
    }

    private void BuildEquipment(VBoxContainer form)
    {
        _weapon = Check(form, "Profil d’arme"); _weaponClass = Field(form, "Classe d’arme", "longsword");
        _minDamage = Spin(form, "Dégâts minimum", 0, 10000, 1, .5); _maxDamage = Spin(form, "Dégâts maximum", 0, 10000, 4, .5);
        _reach = Spin(form, "Portée (m)", .1, 100, 1.5, .1); _armor = Check(form, "Profil d’armure");
        _armorMaterial = Field(form, "Matériau", "steel"); _protection = Spin(form, "Protection", 0, 100, 10, 1);
        _ammo = Check(form, "Munitions / projectile"); _ammoTag = Field(form, "Tag de munition", "arrow");
        _projectile = Field(form, "Asset projectile", "assets/projectiles/arrow.glb"); _stack = Spin(form, "Pile maximale", 1, 9999, 20, 1);
        _durability = Check(form, "Durabilité"); _durabilityMaximum = Spin(form, "Durabilité maximale", 1, 100000, 100, 1);
        _wear = Spin(form, "Usure par coup", 0, 1000, 1, .25); _container = Check(form, "Conteneur imbriquable");
        _slots = Spin(form, "Emplacements", 1, 1000, 8, 1); _mass = Spin(form, "Masse maximale", .1, 100000, 40, 1);
        _depth = Spin(form, "Profondeur maximale", 1, 16, 3, 1);
    }

    private void BuildLoot(VBoxContainer form)
    {
        _lootSource = Field(form, "Source de butin", "chest"); _biome = Field(form, "Biome", "crypt");
        _lootFaction = Field(form, "Faction source", "ash-watch"); _lootTags = Field(form, "Tags contextuels", "undead");
        _lootMinimumLevel = Spin(form, "Niveau minimum du butin", 0, 100, 0, 1);
        _lootMaximumLevel = Spin(form, "Niveau maximum du butin", 0, 100, 100, 1);
        _lootDifficulty = Field(form, "Difficulté requise (facultative)", "hard");
        _lootPlayerCondition = Field(form, "Condition joueur (facultative)", "quest.accepted");
        _lootEvent = Field(form, "Événement requis (facultatif)", "boss.defeated");
        _lootWeight = Spin(form, "Poids de la règle", .001, 1_000_000, 1, .1);
        _unique = Check(form, "Objet unique"); _registryKey = Field(form, "Clé du registre", "unique:item");
        _uniqueScope = EnumSelect<ItemUniqueScope>(); form.AddChild(Labeled("Portée d’unicité", _uniqueScope));
        _lootOwnership = EnumSelect<ItemLootOwnershipMode>(); form.AddChild(Labeled("Droits de butin", _lootOwnership));
        _ownershipExpiry = Spin(form, "Expiration des droits (s)", 0, 31_536_000, 120, 1);
        _recipe = Check(form, "Recette de craft"); _ingredient = Field(form, "Requête ingrédient", "tag:metal");
        _ingredientQuantity = Spin(form, "Quantité", 1, 9999, 1, 1); _station = Field(form, "Station", "forge");
        _craftSkill = Field(form, "Compétence", "smithing"); _generator = Check(form, "Génération procédurale");
        _budget = Spin(form, "Budget d’affixes", 0, 1000, 4, 1); _pool = Field(form, "Pool", "weapon");
        _affixKey = Field(form, "Affixe", "keen"); _affixProperties = Field(form, "Propriétés générées", "critical");
    }

    private void LoadItem(BlueprintDefinition blueprint)
    {
        _selectedBlueprintId = blueprint.Id; var profile = blueprint.ItemAdvanced ?? ItemAdvancedProfile.CreateDefault(Guid.NewGuid(), "toolset:g13-k-m");
        _loadedProfile = profile;
        _profileId = profile.Id; _currency!.Text = profile.Economy.Currency; _baseCost!.Value = (double)profile.Economy.BaseCost;
        _minimumLevel!.Value = profile.Requirements.MinimumLevel; _minimumLore!.Value = profile.Requirements.MinimumLore;
        _skills!.Text = Join(profile.Requirements.Skills); _traits!.Text = Join(profile.Requirements.Traits); _factions!.Text = Join(profile.Requirements.Factions);
        _condition!.Text = profile.Requirements.ScriptCondition ?? string.Empty; _identification!.Value = profile.Identification.Difficulty;
        _partyKnowledge!.ButtonPressed = profile.Identification.ShareWithParty; _stolen!.ButtonPressed = profile.Ownership.Stolen;
        _owner!.Text = profile.Ownership.CurrentOwnerId; _jurisdiction!.Text = profile.Ownership.Jurisdiction;
        var proc = profile.OnHitProcs.FirstOrDefault(item => string.Equals(item.Trigger, "hit", StringComparison.Ordinal))
            ?? profile.OnHitProcs.FirstOrDefault();
        _loadedProcId = proc?.Id;
        _procTrigger!.Text = proc?.Trigger ?? "hit"; _procEffect!.Text = proc?.EffectId ?? "effect.on-hit";
        _procChance!.Value = proc?.Chance ?? .25; _procCooldown!.Value = proc?.InternalCooldownSeconds ?? 0;
        _procCondition!.Text = proc?.Condition ?? string.Empty; _procAttribution!.Text = proc?.Attribution ?? "wielder";
        _procDeterministic!.ButtonPressed = proc?.Deterministic ?? true;
        _weapon!.ButtonPressed = profile.Weapon is not null; _weaponClass!.Text = profile.Weapon?.WeaponClass ?? "longsword";
        _minDamage!.Value = profile.Weapon?.Attacks.FirstOrDefault()?.MinimumDamage ?? 1; _maxDamage!.Value = profile.Weapon?.Attacks.FirstOrDefault()?.MaximumDamage ?? 4;
        _reach!.Value = profile.Weapon?.Attacks.FirstOrDefault()?.ReachMeters ?? 1.5; _armor!.ButtonPressed = profile.Armor is not null;
        _armorMaterial!.Text = profile.Armor?.Material ?? "steel"; _protection!.Value = profile.Armor?.Protection ?? 10;
        _ammo!.ButtonPressed = profile.Ammo is not null; _ammoTag!.Text = profile.Ammo?.AmmoTag ?? "arrow";
        _projectile!.Text = profile.Ammo?.ProjectileAssetId ?? "assets/projectiles/arrow.glb"; _stack!.Value = profile.Ammo?.MaximumStack ?? 20;
        _durability!.ButtonPressed = profile.Durability?.Enabled == true; _durabilityMaximum!.Value = profile.Durability?.Maximum ?? 100;
        _wear!.Value = profile.Durability?.WearPerHit ?? 1; _container!.ButtonPressed = profile.Container is not null;
        _slots!.Value = profile.Container?.CapacitySlots ?? 8; _mass!.Value = profile.Container?.CapacityMass ?? 40; _depth!.Value = profile.Container?.MaximumDepth ?? 3;
        var context = profile.LootContexts.FirstOrDefault(); _loadedLootContextId = context?.Id;
        _lootSource!.Text = context?.Source ?? "chest"; _biome!.Text = context?.Biome ?? string.Empty;
        _lootFaction!.Text = context?.Faction ?? string.Empty; _lootTags!.Text = Join(context?.RequiredTags ?? []);
        _lootMinimumLevel!.Value = context?.MinimumLevel ?? 0; _lootMaximumLevel!.Value = context?.MaximumLevel ?? 100;
        _lootDifficulty!.Text = context?.Difficulty ?? string.Empty; _lootPlayerCondition!.Text = context?.PlayerCondition ?? string.Empty;
        _lootEvent!.Text = context?.EventId ?? string.Empty; _lootWeight!.Value = context?.Weight ?? 1;
        _unique!.ButtonPressed = profile.Unique.Enabled; _registryKey!.Text = profile.Unique.RegistryKey; Select(_uniqueScope!, profile.Unique.Scope);
        Select(_lootOwnership!, profile.LootOwnership.Mode); _ownershipExpiry!.Value = profile.LootOwnership.ExpirationSeconds;
        var recipe = profile.Recipes.FirstOrDefault(); _recipe!.ButtonPressed = recipe is not null; _ingredient!.Text = recipe?.Ingredients.FirstOrDefault()?.Query ?? "tag:metal";
        _ingredientQuantity!.Value = recipe?.Ingredients.FirstOrDefault()?.Quantity ?? 1; _station!.Text = recipe?.StationTag ?? "forge"; _craftSkill!.Text = recipe?.SkillId ?? "smithing";
        _generator!.ButtonPressed = profile.Generator?.Enabled == true; _budget!.Value = profile.Generator?.Budget ?? 4;
        _pool!.Text = profile.Generator?.Pools.FirstOrDefault() ?? "weapon"; _affixKey!.Text = profile.Generator?.Affixes.FirstOrDefault()?.Key ?? "keen";
        _affixProperties!.Text = Join(profile.Generator?.Affixes.FirstOrDefault()?.PropertyIds ?? ["critical"]);
        _feedback!.Text = blueprint.ItemAdvanced is null ? "[color=#b8a8ce]État vide : créez le profil complet de cet objet.[/color]" :
            "[color=#8ce0bd]Profil canonique chargé.[/color]"; RebuildSummary();
    }

    private ItemAdvancedProfile BuildProfile()
    {
        var loaded = _loadedProfile ?? ItemAdvancedProfile.CreateDefault(_profileId, "toolset:g13-k-m");
        var skills = Csv(_skills!.Text); var traits = Csv(_traits!.Text); var factions = Csv(_factions!.Text);
        var loadedProc = _loadedProcId is { } loadedProcId
            ? loaded.OnHitProcs.FirstOrDefault(item => item.Id == loadedProcId)
            : null;
        var editedProc = string.IsNullOrWhiteSpace(_procEffect!.Text) ? null :
            (loadedProc ?? new ItemOnHitProc(Stable("proc"), "hit", .25, 0, null,
                "effect.on-hit", "wielder", true, "toolset:g13-k")) with
            {
                Trigger = string.IsNullOrWhiteSpace(_procTrigger!.Text) ? "hit" : _procTrigger.Text.Trim(),
                Chance = _procChance!.Value,
                InternalCooldownSeconds = _procCooldown!.Value,
                Condition = Null(_procCondition!.Text),
                EffectId = _procEffect.Text.Trim(),
                Attribution = string.IsNullOrWhiteSpace(_procAttribution!.Text) ? "wielder" : _procAttribution.Text.Trim(),
                Deterministic = _procDeterministic!.ButtonPressed,
            };
        ItemWeaponProfile? weapon = null;
        if (_weapon!.ButtonPressed)
        {
            var loadedWeapon = loaded.Weapon ?? new ItemWeaponProfile("longsword", 1, false, [], ["authored"]);
            var attack = (loadedWeapon.Attacks.FirstOrDefault() ?? new ItemWeaponAttack(
                "primary", "physical", 1, 4, 1.5, "attack.primary", ["equipped"])) with
            {
                MinimumDamage = _minDamage!.Value,
                MaximumDamage = _maxDamage!.Value,
                ReachMeters = _reach!.Value,
            };
            weapon = loadedWeapon with
            {
                WeaponClass = _weaponClass!.Text.Trim(),
                Attacks = ReplaceFirst(loadedWeapon.Attacks, attack),
            };
        }
        var armor = _armor!.ButtonPressed
            ? (loaded.Armor ?? new ItemArmorProfile(["torso"], 10, 1, 1, "steel",
                new Dictionary<string, double>(), ["damaged", "broken"])) with
            {
                Protection = _protection!.Value,
                Material = _armorMaterial!.Text.Trim(),
            }
            : null;
        var ammo = _ammo!.ButtonPressed
            ? (loaded.Ammo ?? new ItemAmmoPolicy("arrow", "assets/projectiles/arrow.glb", 20, .25, null, true)) with
            {
                AmmoTag = _ammoTag!.Text.Trim(),
                ProjectileAssetId = _projectile!.Text.Trim(),
                MaximumStack = (int)_stack!.Value,
            }
            : null;
        var durability = (loaded.Durability ?? new ItemDurabilityPolicy(false, 100, 100, 0, 1,
            "smithing", 1, "item.broken", "visual.broken")) with
        {
            Enabled = _durability!.ButtonPressed,
            Maximum = (int)_durabilityMaximum!.Value,
            Initial = Math.Min((int)_durabilityMaximum.Value,
                loaded.Durability?.Initial ?? (int)_durabilityMaximum.Value),
            WearPerHit = _wear!.Value,
        };
        var container = _container!.ButtonPressed
            ? (loaded.Container ?? new ItemContainerPolicy(8, 40, 3, .8, ["loot"], ["inventory.open"], true, true)) with
            {
                CapacitySlots = (int)_slots!.Value,
                CapacityMass = _mass!.Value,
                MaximumDepth = (int)_depth!.Value,
            }
            : null;
        var loadedLoot = _loadedLootContextId is { } loadedLootId
            ? loaded.LootContexts.FirstOrDefault(item => item.Id == loadedLootId)
            : null;
        var editedLoot = string.IsNullOrWhiteSpace(_lootSource!.Text) ? null :
            (loadedLoot ?? new ItemLootContextRule(Stable("loot"), "chest", null, null,
                0, 100, null, [], null, null, 1, "toolset:g13-m")) with
            {
                Source = _lootSource.Text.Trim(),
                Biome = Null(_biome!.Text),
                Faction = Null(_lootFaction!.Text),
                MinimumLevel = (int)_lootMinimumLevel!.Value,
                MaximumLevel = (int)_lootMaximumLevel!.Value,
                Difficulty = Null(_lootDifficulty!.Text),
                RequiredTags = Csv(_lootTags!.Text),
                PlayerCondition = Null(_lootPlayerCondition!.Text),
                EventId = Null(_lootEvent!.Text),
                Weight = _lootWeight!.Value,
            };
        ItemRecipeDefinition? editedRecipe = null;
        if (_recipe!.ButtonPressed)
        {
            var loadedRecipe = loaded.Recipes.FirstOrDefault() ?? new ItemRecipeDefinition(Stable("recipe"),
                "authored-recipe", [], "forge", "smithing", 10, 0, 5, [], "toolset:g13-m");
            var ingredient = (loadedRecipe.Ingredients.FirstOrDefault() ?? new ItemIngredientQuery("tag:metal", 1,
                true, 0)) with
            {
                Query = _ingredient!.Text.Trim(),
                Quantity = (int)_ingredientQuantity!.Value,
            };
            editedRecipe = loadedRecipe with
            {
                Ingredients = ReplaceFirst(loadedRecipe.Ingredients, ingredient),
                StationTag = _station!.Text.Trim(),
                SkillId = _craftSkill!.Text.Trim(),
            };
        }
        ItemGeneratorPolicy? generator = null;
        if (_generator!.ButtonPressed)
        {
            var loadedGenerator = loaded.Generator ?? new ItemGeneratorPolicy(true, 4, 1, 1, [],
                new Dictionary<string, int> { ["common"] = 100 }, [], 2026);
            var affix = (loadedGenerator.Affixes.FirstOrDefault() ?? new ItemAffixDefinition(Stable("affix"),
                "keen", "weapon", 1, 1, "common", [], ["critical"], "toolset:g13-m")) with
            {
                Key = _affixKey!.Text.Trim(),
                Pool = _pool!.Text.Trim(),
                Cost = Math.Max(1, (int)_budget!.Value / 2),
                PropertyIds = Csv(_affixProperties!.Text),
            };
            generator = loadedGenerator with
            {
                Enabled = true,
                Budget = (int)_budget.Value,
                Pools = ReplaceFirst(loadedGenerator.Pools, _pool.Text.Trim()),
                Affixes = ReplaceFirst(loadedGenerator.Affixes, affix),
            };
        }
        return loaded with
        {
            RequiredAtRuntime = true,
            Economy = loaded.Economy with
            {
                Currency = _currency!.Text.Trim(),
                BaseCost = (decimal)_baseCost!.Value,
            },
            Requirements = loaded.Requirements with
            {
                MinimumLevel = (int)_minimumLevel!.Value,
                MinimumLore = (int)_minimumLore!.Value,
                Skills = skills,
                Traits = traits,
                Factions = factions,
                ScriptCondition = Null(_condition!.Text),
            },
            Identification = loaded.Identification with
            {
                Difficulty = (int)_identification!.Value,
                Currency = _currency.Text.Trim(),
                ShareWithParty = _partyKnowledge!.ButtonPressed,
            },
            Ownership = loaded.Ownership with
            {
                Stolen = _stolen!.ButtonPressed,
                CurrentOwnerId = _owner!.Text.Trim(),
                Jurisdiction = _jurisdiction!.Text.Trim(),
            },
            OnHitProcs = ReplaceOrRemoveSelected(loaded.OnHitProcs, _loadedProcId, editedProc, item => item.Id),
            Weapon = weapon,
            Armor = armor,
            Ammo = ammo,
            Durability = durability,
            Container = container,
            LootContexts = ReplaceOrRemoveSelected(loaded.LootContexts, _loadedLootContextId, editedLoot, item => item.Id),
            Unique = loaded.Unique with
            {
                Enabled = _unique!.ButtonPressed,
                RegistryKey = _registryKey!.Text.Trim(),
                Scope = (ItemUniqueScope)_uniqueScope!.Selected,
            },
            LootOwnership = loaded.LootOwnership with
            {
                Mode = (ItemLootOwnershipMode)_lootOwnership!.Selected,
                ExpirationSeconds = (long)_ownershipExpiry!.Value,
            },
            Recipes = ReplaceOrRemoveFirst(loaded.Recipes, editedRecipe),
            Generator = generator,
        };
    }

    private static IReadOnlyList<T> ReplaceFirst<T>(IReadOnlyList<T> source, T replacement)
    {
        if (source.Count == 0) return [replacement];
        var result = source.ToArray();
        result[0] = replacement;
        return result;
    }

    private static IReadOnlyList<T> ReplaceOrRemoveFirst<T>(IReadOnlyList<T> source, T? replacement)
        where T : class
    {
        if (replacement is not null) return ReplaceFirst(source, replacement);
        return source.Skip(1).ToArray();
    }

    private static IReadOnlyList<T> ReplaceOrRemoveSelected<T>(IReadOnlyList<T> source, Guid? selectedId,
        T? replacement, Func<T, Guid> identify) where T : class
    {
        if (selectedId is null)
            return replacement is null ? source : source.Append(replacement).ToArray();

        var selectedIndex = Array.FindIndex(source.ToArray(), item => identify(item) == selectedId.Value);
        if (selectedIndex < 0 || selectedIndex >= source.Count)
            return replacement is null ? source : source.Append(replacement).ToArray();

        if (replacement is null)
            return source.Where((_, index) => index != selectedIndex).ToArray();

        var result = source.ToArray();
        result[selectedIndex] = replacement;
        return result;
    }

    private void Mutate(bool dryRun)
    {
        _lastSucceeded = false; LastCommandId = Guid.Empty; LastPatchSummary = LastDiagnostics = string.Empty;
        if (_selectedBlueprintId is null || _mutate is null) return;
        try
        {
            var result = _mutate(_selectedBlueprintId, BuildProfile(), dryRun); LastCommandId = result.Audit.CommandId.Value;
            LastPatchSummary = result.Patch?.Summary ?? string.Empty;
            if (!result.Succeeded)
            {
                LastDiagnostics = string.Join(" | ", result.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
                _feedback!.Text = $"[color=#ff7777]Transaction refusée[/color]\n{LastDiagnostics}"; return;
            }
            _lastSucceeded = true; _feedback!.Text = dryRun
                ? $"[color=#ffbd7a]APERÇU — aucune donnée modifiée[/color]\n{result.Patch?.Summary}\nInverse : {result.Inverse?.Summary}"
                : $"[color=#8ce0bd]APPLIQUÉ AU PROJET[/color]\n{result.Patch?.Summary}\nRévision {result.NewRevision}";
            if (!dryRun) Refresh();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { LastDiagnostics = exception.Message; _feedback!.Text = $"[color=#ff7777]Formulaire incomplet[/color]\n{exception.Message}"; }
    }

    private void RebuildSummary()
    {
        if (_summary is null) return;
        try
        {
            var profile = BuildProfile(); var diagnostics = ItemAdvancedValidator.Validate(profile, "toolset/item");
            var context = new ItemRuntimeContext(20, 20, Csv(_skills!.Text).ToHashSet(StringComparer.Ordinal),
                Csv(_traits!.Text).ToHashSet(StringComparer.Ordinal), Csv(_factions!.Text).ToHashSet(StringComparer.Ordinal),
                string.IsNullOrWhiteSpace(_condition!.Text) ? new HashSet<string>() : new HashSet<string>([_condition.Text.Trim()]),
                "default", profile.Economy.Currency, 1, profile.Durability?.Initial ?? 100, 2026, "preview", true);
            var requirements = diagnostics.Count == 0 ? ItemAdvancedRuntimeResolver.ResolveRequirements(profile, context) : null;
            var value = diagnostics.Count == 0 ? ItemAdvancedRuntimeResolver.ResolveEconomicValue(profile, context) : 0;
            var generated = diagnostics.Count == 0 && profile.Generator?.Enabled == true ? ItemAdvancedRuntimeResolver.Generate(profile, context) : null;
            SummarySectionCount = 4; _summary.Text = $"[font_size=22][color=#ff9858]{value:0.##} {profile.Economy.Currency}[/color][/font_size]\n" +
                $"[color=#8ce0bd]UUID {profile.Id:D} · schéma v{profile.SchemaVersion}[/color]\n\n" +
                $"[b]USAGE & CONNAISSANCE[/b]\n{(requirements?.Allowed == true ? "autorisé" : string.Join(", ", requirements?.Missing ?? []))} · " +
                $"{profile.Identification.Levels.Count} niveaux · {profile.OnHitProcs.Count} proc(s)\n\n" +
                $"[b]ÉQUIPEMENT[/b]\nArme {profile.Weapon?.WeaponClass ?? "—"} · Armure {profile.Armor?.Material ?? "—"} · " +
                $"Munitions {(profile.Ammo is null ? "—" : profile.Ammo.MaximumStack)} · Durabilité {profile.Durability?.Maximum}\n\n" +
                $"[b]INVENTAIRE & BUTIN[/b]\nConteneur {profile.Container?.CapacitySlots ?? 0} cases · {profile.LootContexts.Count} contexte(s) · " +
                $"unique {profile.Unique.Enabled} · droits {profile.LootOwnership.Mode}\n\n" +
                $"[b]CRAFT & GÉNÉRATION[/b]\n{profile.Recipes.Count} recette(s) · {generated?.Affixes.Count ?? 0} affixe(s) simulé(s)\n\n" +
                (diagnostics.Count == 0 ? "[color=#8ce0bd]✓ Profil valide et simulation déterministe[/color]" :
                    $"[color=#ff7777]⚠ {diagnostics.Count} diagnostic(s)[/color]\n{string.Join('\n', diagnostics.Select(item => item.Code))}");
        }
        catch (Exception exception) { SummarySectionCount = 0; _summary.Text = $"[color=#ff7777]{exception.Message}[/color]"; }
    }

    private void OnSelected(long index)
    {
        if (_items is null || _load?.Invoke() is not { } state) return;
        var id = BlueprintId.Parse(_items.GetItemMetadata((int)index).AsString()); LoadItem(state.Project.Blueprints.Single(item => item.Id == id));
    }
    private void SetEmpty(string message)
    {
        _selectedBlueprintId = null; _loadedProfile = null; _loadedProcId = null; _loadedLootContextId = null;
        if (_feedback is not null) _feedback.Text = message;
        if (_summary is not null) _summary.Text = "[color=#b8a8ce]Aucun objet sélectionné.[/color]";
    }
    private Guid Stable(string key) { var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{_profileId:D}|{key}")); return new(hash.AsSpan(0, 16)); }
    private static string[] Csv(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToArray();
    private static string Join(IEnumerable<string> values) => string.Join(",", values); private static string? Null(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static PanelContainer Panel(string title)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8); panel.AddChild(box); var label = new Label { Text = title }; label.AddThemeColorOverride("font_color", new("ffad70")); box.AddChild(label); return panel;
    }
    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer { Name = title, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; box.AddThemeConstantOverride("separation", 8); scroll.AddChild(box); tabs.AddChild(scroll); return box;
    }
    private static Control Labeled(string label, Control control) { var box = new VBoxContainer(); box.AddChild(new Label { Text = label }); box.AddChild(control); return box; }
    private static CheckBox Check(Container owner, string text) { var value = new CheckBox { Text = text }; owner.AddChild(value); return value; }
    private static LineEdit Field(Container owner, string label, string placeholder) { var box = new VBoxContainer(); box.AddChild(new Label { Text = label }); var value = new LineEdit { PlaceholderText = placeholder }; box.AddChild(value); owner.AddChild(box); return value; }
    private static SpinBox Spin(Container owner, string label, double min, double max, double value, double step) { var box = new VBoxContainer(); box.AddChild(new Label { Text = label }); var spin = new SpinBox { MinValue = min, MaxValue = max, Value = value, Step = step }; box.AddChild(spin); owner.AddChild(box); return spin; }
    private static OptionButton EnumSelect<T>() where T : struct, Enum { var result = new OptionButton(); foreach (var value in Enum.GetValues<T>()) result.AddItem(value.ToString()); return result; }
    private static void Select<T>(OptionButton option, T value) where T : struct, Enum => option.Select(Convert.ToInt32(value));
    private static Button Button(string text, Action action, bool primary = false) { var button = new Button { Text = text, CustomMinimumSize = new(140, 36) }; if (primary) button.AddThemeColorOverride("font_color", new("fff4ec")); button.Pressed += action; return button; }
}
#endif
