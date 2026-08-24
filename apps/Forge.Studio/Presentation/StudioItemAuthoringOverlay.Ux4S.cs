using System.Globalization;
using System.Text.RegularExpressions;
using Forge.Domain.Blueprints;
using Forge.Domain.Items;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;
using Forge.Studio.Session;
using Godot;

namespace Forge.Studio.Presentation;

internal sealed partial class StudioItemAuthoringOverlayUx4S : Control
{
    private readonly StudioSessionController _sourceSession;
    private StudioSessionController _session;
    private StudioSessionController? _evidenceSession;
    private string? _evidenceRoot;
    private StudioItemEvidenceSourceSnapshotUx4S? _sourceSnapshot;
    private BlueprintId? _evidenceItemId;
    private bool _usesRealModule;
    private bool _usesFallback;
    private bool _previewDryRun;
    private bool _resourcesReleased = true;
    private bool _sourceRestored = true;
    private bool _applied;
    private bool _updating;
    private string _lastFailure = "none";
    private IReadOnlyList<StudioItemChoiceUx4S> _choices = [];
    private readonly Dictionary<string, Guid> _itemReferences = new(StringComparer.Ordinal);
    private StudioItemDraftUx4S? _draft;
    private StudioItemPreviewUx4S? _previewResult;

    private readonly OptionButton _item;
    private readonly LineEdit _slug;
    private readonly LineEdit _displayName;
    private readonly LineEdit _type;
    private readonly TextEdit _localizations;
    private readonly LineEdit _foundation;
    private readonly TextEdit _properties;
    private readonly LineEdit _appearance;
    private readonly LineEdit _inventoryUsage;
    private readonly LineEdit _critical;
    private readonly LineEdit _transfers;
    private readonly TextEdit _modifiers;
    private readonly LineEdit _ability;
    private readonly TextEdit _equipmentSlots;
    private readonly LineEdit _requiredSlots;
    private readonly LineEdit _economy;
    private readonly LineEdit _requirements;
    private readonly LineEdit _identification;
    private readonly TextEdit _knowledge;
    private readonly LineEdit _ownership;
    private readonly TextEdit _ownershipHistory;
    private readonly TextEdit _onHit;
    private readonly LineEdit _weapon;
    private readonly TextEdit _attacks;
    private readonly LineEdit _armor;
    private readonly LineEdit _ammo;
    private readonly LineEdit _durability;
    private readonly LineEdit _container;
    private readonly TextEdit _lootEntries;
    private readonly TextEdit _lootContexts;
    private readonly LineEdit _unique;
    private readonly LineEdit _lootOwnership;
    private readonly TextEdit _recipes;
    private readonly LineEdit _generator;
    private readonly TextEdit _affixes;
    private readonly TextEdit _placements;
    private readonly TextEdit _events;
    private readonly CheckBox _acceptMigration;
    private readonly VBoxContainer _diagnostics;
    private readonly RichTextLabel _summary;
    private readonly Button _preview;
    private readonly Button _apply;
    private readonly Button _undo;
    private readonly Button _redo;
    private readonly Button _save;

    internal event Action? ItemChanged;
    internal event Action<string, bool>? StatusChanged;
    internal bool IsOpen => Visible;
    internal int ItemChoiceCount => _choices.Count;
    internal bool HasExplicitSelection => _item.Selected > 0 && _draft is not null;
    internal bool CanonicalPreviewReady => _previewResult?.CanApply == true;
    internal bool ShowsCreation => _draft?.Foundation.Creation is { Wizard.Sections.Count: 8 };
    internal bool ShowsGameplay => _draft?.Foundation.Gameplay is { Transfer.AllowedDestinations.Count: 8 };
    internal bool ShowsAdvanced => _draft?.Advanced is { Weapon: not null, Armor: not null, Recipes.Count: > 0 };
    internal bool ShowsOperations => _draft?.Foundation.Operations is { Events.Bindings.Count: 9 };
    internal bool ShowsHumanSurface
    {
        get
        {
            var text = string.Join(' ', VisibleText(this));
            return text.Contains("Assistant d’objet complet", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Identité et création", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Loot et artisanat", StringComparison.OrdinalIgnoreCase) &&
                   text.Contains("Inventaire et événements", StringComparison.OrdinalIgnoreCase) &&
                   !ContainsGuid(text) && !text.Contains(".json", StringComparison.OrdinalIgnoreCase);
        }
    }
    internal bool UsesIsolatedEvidence => _evidenceSession is not null && !ReferenceEquals(_session, _sourceSession);
    internal bool UsesRealModuleEvidence => _usesRealModule;
    internal bool UsesCanonicalFallbackEvidence => _usesFallback;
    internal bool EvidencePreviewDryRun => _previewDryRun;
    internal bool EvidenceSourceUntouched => _sourceSnapshot is not null && SourceMatches(_sourceSnapshot);
    internal bool EvidenceResourcesReleased => _resourcesReleased;
    internal bool SourceSessionRestored => _sourceRestored;
    internal string LastEvidenceFailure => _lastFailure;

    internal StudioItemAuthoringOverlayUx4S(StudioSessionController session)
    {
        _session = session; _sourceSession = session; Name = "StudioItemAuthoringOverlayUx4S";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); Visible = false; MouseFilter = MouseFilterEnum.Stop;
        var veil = new ColorRect { Color = new Color(0, 0, 0, .74f) };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(veil);
        var dialog = StudioStyle.Panel("ItemAuthoringDialogUx4S", StudioStyle.Chrome, 14, 2);
        dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, margin: 24); AddChild(dialog);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 7); dialog.AddChild(root);
        var header = new HBoxContainer(); var heading = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddChild(StudioStyle.Label("ASSISTANT D’OBJET COMPLET · UX4-S", 14, StudioStyle.Accent));
        heading.AddChild(StudioStyle.Label(
            "Une commande atomique · aperçu sec · les identités techniques restent internes.", 9, StudioStyle.Muted));
        heading.AddChild(StudioStyle.Label(
            "Identité et création · Règles et pouvoir · Économie et équipement · Loot et artisanat · Inventaire et événements",
            8, StudioStyle.Muted, wrap: true));
        header.AddChild(heading); var close = StudioStyle.Button("Fermer", compact: true);
        close.CustomMinimumSize = new Vector2(76, 34); close.Pressed += Close; header.AddChild(close); root.AddChild(header);
        _item = Choice(root, "Objet à éditer ou création explicite");

        var tabs = new TabContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 350)
        };
        var identityTab = Tab(tabs, "Identité et création");
        var identity = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _slug = Field(identity, "Slug canonique", "boussole-de-braise");
        _displayName = Field(identity, "Nom affiché", "Boussole de braise"); identityTab.AddChild(identity);
        _type = Field(identityTab, "Type immuable · type | slots | propriétés | rig", "relic | hand,inventory | utility,ward | hand-rig");
        _localizations = Multi(identityTab, "Localisation · langue | nom | court | long | inconnu | type | notes", 84);
        _foundation = Field(identityTab, "Blueprint · apparence | rig | qualité | niveau | valeur | slots | overrides", string.Empty);
        _properties = Multi(identityTab, "Propriétés · type | valeur | effet | script optionnel", 70);
        _appearance = Field(identityTab, "Apparence · meshes | matériaux | sockets | icône | VFX | aperçu équipé", string.Empty);
        _inventoryUsage = Field(identityTab,
            "Inventaire/piles/usages · masse | volume | grille | facteurs | pile | charges | cooldown | durabilité", string.Empty);

        var gameplayTab = Tab(tabs, "Règles et pouvoir");
        _critical = Field(gameplayTab,
            "Protections · destruction | vente | drop | vol | consommation | nettoyage | override MJ", string.Empty);
        _transfers = Field(gameplayTab, "Transferts autorisés | permissions", string.Empty);
        _modifiers = Multi(gameplayTab,
            "Propriétés magiques · type | paramètres | cumul | condition | résumé | hook avant | hook après", 88);
        _ability = Field(gameplayTab,
            "Pouvoir unique · actif | ciblage | coûts | charges | cooldown | demandé | résolu", string.Empty);
        _equipmentSlots = Multi(gameplayTab, "Slots · identifiant | règles | rig | couche | conflits", 88);
        _requiredSlots = Field(gameplayTab, "Slots requis", "hand");

        var advancedTab = Tab(tabs, "Économie et équipement");
        _economy = Field(advancedTab, "Économie · monnaie | coût | qualité | rareté | usure | demande | marchés", string.Empty);
        _requirements = Field(advancedTab, "Prérequis · niveau | lore | compétences | traits | factions | condition", string.Empty);
        _identification = Field(advancedTab, "Identification · état initial | difficulté | coût | monnaie | partage groupe", string.Empty);
        _knowledge = Multi(advancedTab, "Niveaux de connaissance · nom | seuil | propriétés révélées", 66);
        _ownership = Field(advancedTab, "Objet volé · volé | propriétaire | juridiction | blanchissable", string.Empty);
        _ownershipHistory = Multi(advancedTab, "Historique · propriétaire | juridiction | preuve | date ISO | provenance", 66);
        _onHit = Multi(advancedTab, "On Hit · déclencheur | chance | cooldown | condition | effet | attribution | déterministe", 72);
        _weapon = Field(advancedTab, "Arme · active | classe | attaques/s | deux mains | tags", string.Empty);
        _attacks = Multi(advancedTab, "Attaques · nom | canal | min | max | portée | animation | tags", 72);
        _armor = Field(advancedTab, "Armure · active | couverture | protection | encombrement | bruit | matériau | résistances | visuels", string.Empty);
        _ammo = Field(advancedTab, "Munitions · actives | tag | projectile | pile | récupération | payload | consommer", string.Empty);
        _durability = Field(advancedTab, "Usure · active | max | initiale | par usage | par hit | compétence | coût | effet | visuel", string.Empty);
        _container = Field(advancedTab, "Conteneur · actif | slots | masse | profondeur | poids imbriqué | tags | permissions | persistance", string.Empty);

        var lootTab = Tab(tabs, "Loot et artisanat");
        _lootEntries = Multi(lootTab, "Loot table · objet humain | groupe | poids | min | max | qualité | condition | unique", 76);
        _lootContexts = Multi(lootTab,
            "Loot contextuel · source | biome | faction | niveau min | max | difficulté | tags | condition | événement | poids", 76);
        _unique = Field(lootTab, "Objet unique · actif | clé registre | portée | réservation | libération", string.Empty);
        _lootOwnership = Field(lootTab, "Butin instancié · mode | expiration | transfert groupe | autorité serveur", string.Empty);
        _recipes = Multi(lootTab,
            "Recettes · slug | ingrédients query:qté:consomme:qualité | station | compétence | difficulté | qualité | durée | sous-produits", 82);
        _generator = Field(lootTab, "Générateur · actif | budget | affixes min | max | pools | raretés | seed", string.Empty);
        _affixes = Multi(lootTab, "Affixes · clé | pool | coût | poids | rareté | exclusions | propriétés | provenance", 76);

        var operationsTab = Tab(tabs, "Inventaire et événements");
        _placements = Multi(operationsTab,
            "Inspecteur · conteneur | slot | x | y | quantité | tags | aperçu (identité interne protégée)", 130);
        _events = Multi(operationsTab, "Neuf événements · événement | handler | autorité", 170);
        operationsTab.AddChild(StudioStyle.Label(
            "Le Studio préserve les placements et lie les neuf événements. Aucun drag-drop runtime ni ForgeScript n’est exécuté ici.",
            9, StudioStyle.Muted, wrap: true));
        tabs.CurrentTab = 0; root.AddChild(tabs);

        _acceptMigration = new CheckBox { Text = "J’accepte explicitement les pertes d’une migration de type immuable" };
        root.AddChild(_acceptMigration);
        _diagnostics = new VBoxContainer(); root.AddChild(_diagnostics);
        _summary = new RichTextLabel
        {
            BbcodeEnabled = true,
            CustomMinimumSize = new Vector2(0, 120),
            FitContent = false,
            SelectionEnabled = true
        }; root.AddChild(_summary);
        root.AddChild(StudioStyle.Label(
            "Prévisualiser ne modifie rien. Appliquer remplace le lot complet dans une seule commande annulable.",
            9, StudioStyle.Muted, wrap: true));
        var footer = new HBoxContainer(); _preview = StudioStyle.Button("Prévisualiser", primary: true, compact: true);
        _apply = StudioStyle.Button("Appliquer", compact: true); _undo = StudioStyle.Button("Annuler", compact: true);
        _redo = StudioStyle.Button("Rétablir", compact: true); _save = StudioStyle.Button("Enregistrer", compact: true);
        foreach (var button in new[] { _preview, _apply, _undo, _redo, _save })
        { button.CustomMinimumSize = new Vector2(button == _preview ? 132 : 104, 34); footer.AddChild(button); }
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill }); root.AddChild(footer);
        ConnectEvents(); ClearControls(); SetEditingEnabled(false);
    }

    internal void Open()
    { if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence(); _session = _sourceSession; OpenBound(); }
    internal void RestoreSourceSurfaceAfterSmoke() { _session = _sourceSession; OpenBound(); }
    internal void HideSourceSurfaceAfterSmoke() { Visible = false; }
    internal void Close()
    {
        if (!Visible && _evidenceSession is null && _evidenceRoot is null && ReferenceEquals(_session, _sourceSession)) return;
        _session.CancelItemUx4S(); _draft = null; _previewResult = null; Visible = false;
        if (_evidenceSession is not null || _evidenceRoot is not null) ReleaseEvidence();
    }
    private void OpenBound()
    {
        _session.CancelItemUx4S(); _choices = _session.ItemChoicesUx4S; _updating = true; _item.Clear();
        _item.AddItem("Choisir un objet…"); _item.AddItem("Créer un nouvel objet…");
        foreach (var choice in _choices) _item.AddItem(choice.Label); _item.Select(0); _updating = false;
        ClearControls(); Visible = true; SetEditingEnabled(false);
    }
    private void SelectionChanged(long _)
    {
        if (_updating) return; _session.CancelItemUx4S(); _previewResult = null; _apply.Disabled = true;
        if (_item.Selected <= 0) { _draft = null; ClearControls(); SetEditingEnabled(false); return; }
        _draft = _item.Selected == 1 ? _session.CreateItemDraftUx4S() :
            _session.CreateItemDraftUx4S(_choices[_item.Selected - 2].BlueprintId);
        if (_draft is null) { ClearControls(); SetEditingEnabled(false); RenderEmpty("Objet absent ou protégé en lecture seule."); return; }
        Populate(_draft); SetEditingEnabled(true); RenderEmpty("Complétez les cinq onglets puis prévisualisez le lot atomique.");
    }

    private void Populate(StudioItemDraftUx4S draft)
    {
        _updating = true; _itemReferences.Clear();
        foreach (var choice in _session.ItemChoicesUx4S) _itemReferences[choice.Label.Split('·')[1].Trim()] = choice.BlueprintId.Value;
        _itemReferences[draft.Slug] = draft.BlueprintId.Value;
        _slug.Text = draft.Slug; _displayName.Text = draft.DisplayName;
        var foundation = draft.Foundation; var creation = foundation.Creation!; var gameplay = foundation.Gameplay!;
        var operations = foundation.Operations!; var advanced = draft.Advanced;
        _type.Text = Join(foundation.Type.Slug, Csv(foundation.Type.AllowedSlots), Csv(foundation.Type.AllowedPropertyKinds), foundation.Type.RequiredRig);
        _localizations.Text = string.Join('\n', foundation.Texts.Select(item => Join(item.Locale, item.Name,
            item.ShortDescription, item.LongDescription, item.UnknownText, item.TypeLabel, item.Notes)));
        _foundation.Text = Join(foundation.AppearanceAssetId, foundation.AppearanceRig, foundation.Quality, foundation.Level,
            foundation.BaseValue, Csv(foundation.Slots), Csv(foundation.AllowedInstanceOverrides));
        _properties.Text = string.Join('\n', foundation.Properties.Select(item =>
            Join(item.Kind, item.Value, item.EffectId, item.ScriptId ?? string.Empty)));
        _appearance.Text = Join(Csv(creation.Appearance.MeshAssetIds), Csv(creation.Appearance.MaterialAssetIds),
            Csv(creation.Appearance.Sockets), creation.Appearance.IconAssetId, Csv(creation.Appearance.VisualEffectAssetIds),
            creation.Appearance.EquippedPreviewAssetId);
        var inventory = creation.Inventory; var stack = creation.Stack; var usage = creation.Usage;
        _inventoryUsage.Text = Join(inventory.Mass, inventory.Volume, inventory.GridWidth, inventory.GridHeight,
            inventory.StackMassFactor, inventory.StackVolumeFactor, stack.Maximum, stack.CompatibilityKey,
            stack.Replicated, usage.TotalCharges, usage.CurrentCharges, usage.UsesPerRest, usage.UsesRemaining,
            usage.CooldownMilliseconds, usage.Durability, usage.Consumable, usage.RechargeRule);
        _critical.Text = Join(gameplay.Critical.ProtectDestruction, gameplay.Critical.ProtectSale,
            gameplay.Critical.ProtectDrop, gameplay.Critical.ProtectTheft, gameplay.Critical.ProtectConsumption,
            gameplay.Critical.ProtectCleanup, gameplay.Critical.AllowAuditedGameMasterOverride);
        _transfers.Text = Join(Csv(gameplay.Transfer.AllowedDestinations.Where(item => item.Value).Select(item => item.Key.ToString())),
            Csv(gameplay.Transfer.RequiredPermissions));
        _modifiers.Text = string.Join('\n', gameplay.Modifiers.Select(item => Join(item.Kind, Pairs(item.Parameters),
            item.StackingRule, item.Condition ?? string.Empty, item.UiSummary, item.BeforeHook ?? string.Empty,
            item.AfterHook ?? string.Empty)));
        _ability.Text = gameplay.Ability is null ? "false | point |  | 0 | 0 | ItemAbilityRequested | ItemAbilityResolved" :
            Join(true, gameplay.Ability.Targeting, Pairs(gameplay.Ability.Costs), gameplay.Ability.ChargeCost,
                gameplay.Ability.CooldownMilliseconds, gameplay.Ability.RequestedEvent, gameplay.Ability.ResolvedEvent);
        _equipmentSlots.Text = string.Join('\n', gameplay.Equipment.SlotDefinitions.Select(item =>
            Join(item.Id, item.Ruleset, item.Rig, item.Layer, Csv(item.Conflicts))));
        _requiredSlots.Text = Csv(gameplay.Equipment.RequiredSlots);
        _economy.Text = Join(advanced.Economy.Currency, advanced.Economy.BaseCost, advanced.Economy.QualityMultiplier,
            advanced.Economy.RarityMultiplier, advanced.Economy.WearMultiplier, advanced.Economy.DemandMultiplier,
            Pairs(advanced.Economy.MarketOverrides));
        _requirements.Text = Join(advanced.Requirements.MinimumLevel, advanced.Requirements.MinimumLore,
            Csv(advanced.Requirements.Skills), Csv(advanced.Requirements.Traits), Csv(advanced.Requirements.Factions),
            advanced.Requirements.ScriptCondition ?? string.Empty);
        _identification.Text = Join(advanced.Identification.InitialLevel, advanced.Identification.Difficulty,
            advanced.Identification.AttemptCost, advanced.Identification.Currency, advanced.Identification.ShareWithParty);
        _knowledge.Text = string.Join('\n', advanced.Identification.Levels.Select(item =>
            Join(item.Id, item.Threshold, Csv(item.RevealedPropertyIds))));
        _ownership.Text = Join(advanced.Ownership.Stolen, advanced.Ownership.CurrentOwnerId,
            advanced.Ownership.Jurisdiction, advanced.Ownership.CanBeLaundered);
        _ownershipHistory.Text = string.Join('\n', advanced.Ownership.History.Select(item => Join(item.OwnerId,
            item.Jurisdiction, item.EvidenceId, item.OccurredAt.ToString("O", CultureInfo.InvariantCulture), item.Provenance)));
        _onHit.Text = string.Join('\n', advanced.OnHitProcs.Select(item => Join(item.Trigger, item.Chance,
            item.InternalCooldownSeconds, item.Condition ?? string.Empty, item.EffectId, item.Attribution, item.Deterministic)));
        _weapon.Text = advanced.Weapon is null ? "false |  | 1 | false | " : Join(true,
            advanced.Weapon.WeaponClass, advanced.Weapon.AttacksPerSecond, advanced.Weapon.TwoHanded, Csv(advanced.Weapon.Tags));
        _attacks.Text = advanced.Weapon is null ? string.Empty : string.Join('\n', advanced.Weapon.Attacks.Select(item =>
            Join(item.Id, item.DamageChannel, item.MinimumDamage, item.MaximumDamage, item.ReachMeters,
                item.AnimationId, Csv(item.RequiredTags))));
        _armor.Text = advanced.Armor is null ? "false |  | 0 | 0 | 0 |  |  | " : Join(true,
            Csv(advanced.Armor.Coverage), advanced.Armor.Protection, advanced.Armor.Encumbrance, advanced.Armor.Noise,
            advanced.Armor.Material, Pairs(advanced.Armor.DamageResistances), Csv(advanced.Armor.DamageStateVisuals));
        _ammo.Text = advanced.Ammo is null ? "false |  |  | 1 | 0 |  | true" : Join(true,
            advanced.Ammo.AmmoTag, advanced.Ammo.ProjectileAssetId, advanced.Ammo.MaximumStack,
            advanced.Ammo.RecoveryChance, advanced.Ammo.ElementalPayload ?? string.Empty, advanced.Ammo.ConsumeOnUse);
        _durability.Text = advanced.Durability is null ? "false | 100 | 100 | 0 | 0 | repair | 0 | item.broken | item.broken" : Join(true,
            advanced.Durability.Maximum, advanced.Durability.Initial, advanced.Durability.WearPerUse,
            advanced.Durability.WearPerHit, advanced.Durability.RepairSkill, advanced.Durability.RepairCostPerPoint,
            advanced.Durability.BrokenEffectId, advanced.Durability.BrokenVisualId);
        _container.Text = advanced.Container is null ? "false | 1 | 1 | 1 | 1 |  |  | true" : Join(true,
            advanced.Container.CapacitySlots, advanced.Container.CapacityMass, advanced.Container.MaximumDepth,
            advanced.Container.NestedWeightMultiplier, Csv(advanced.Container.AllowedTags),
            Csv(advanced.Container.RequiredPermissions), advanced.Container.PersistContents);
        _lootEntries.Text = string.Join('\n', operations.Loot.Entries.Select(item => Join(ItemReferenceLabel(item.ItemDefinitionId),
            item.Group, item.Weight, item.MinimumQuantity, item.MaximumQuantity, item.MinimumQuality, item.Condition, item.Unique)));
        _lootContexts.Text = string.Join('\n', advanced.LootContexts.Select(item => Join(item.Source, item.Biome ?? string.Empty,
            item.Faction ?? string.Empty, item.MinimumLevel, item.MaximumLevel, item.Difficulty ?? string.Empty,
            Csv(item.RequiredTags), item.PlayerCondition ?? string.Empty, item.EventId ?? string.Empty, item.Weight)));
        _unique.Text = Join(advanced.Unique.Enabled, advanced.Unique.RegistryKey, advanced.Unique.Scope,
            advanced.Unique.ReservationSeconds, advanced.Unique.ReleaseOnDestroy);
        _lootOwnership.Text = Join(advanced.LootOwnership.Mode, advanced.LootOwnership.ExpirationSeconds,
            advanced.LootOwnership.TransferToPartyAfterExpiration, advanced.LootOwnership.RequireServerAuthority);
        _recipes.Text = string.Join('\n', advanced.Recipes.Select(item => Join(item.Slug,
            string.Join(',', item.Ingredients.Select(ingredient =>
                $"{ingredient.Query}:{ingredient.Quantity}:{ingredient.Consume.ToString().ToLowerInvariant()}:{ingredient.MinimumQuality}")),
            item.StationTag, item.SkillId, item.Difficulty, item.MinimumQuality, item.DurationSeconds,
            Csv(item.ByproductSlugs), item.Provenance)));
        _generator.Text = advanced.Generator is null ? "false | 0 | 0 | 0 |  | common=1 | 0" : Join(
            advanced.Generator.Enabled, advanced.Generator.Budget, advanced.Generator.MinimumAffixes,
            advanced.Generator.MaximumAffixes, Csv(advanced.Generator.Pools), Pairs(advanced.Generator.RarityWeights),
            advanced.Generator.SeedOffset);
        _affixes.Text = advanced.Generator is null ? string.Empty : string.Join('\n', advanced.Generator.Affixes.Select(item =>
            Join(item.Key, item.Pool, item.Cost, item.Weight, item.Rarity, Csv(item.Exclusions),
                Csv(item.PropertyIds), item.Provenance)));
        _placements.Text = string.Join('\n', operations.Inspector.Placements.Select(item => Join(item.ContainerId,
            item.SlotId ?? string.Empty, item.GridX, item.GridY, item.Quantity, Csv(item.Tags), item.PreviewAssetId)));
        _events.Text = string.Join('\n', operations.Events.Bindings.Select(item => Join(item.Kind, item.HandlerId, item.Authority)));
        _acceptMigration.ButtonPressed = draft.AcceptTypeMigration; _updating = false;
    }

    private StudioItemDraftUx4S? ReadDraft(out string error)
    {
        error = string.Empty; if (_draft is null) { error = "Aucun objet sélectionné."; return null; }
        try
        {
            var source = _draft; var foundation = source.Foundation; var creation = foundation.Creation!;
            var gameplay = foundation.Gameplay!; var operations = foundation.Operations!; var advanced = source.Advanced;
            var type = Parts(_type.Text, 4); var foundationParts = Parts(_foundation.Text, 7);
            var appearance = Parts(_appearance.Text, 6); var inventory = Parts(_inventoryUsage.Text, 17);
            var localized = Lines(_localizations.Text).Select(line =>
            {
                var p = Parts(line, 7);
                return new ItemFoundationText(p[0], p[1], p[2], p[3], p[4], p[5], p[6]);
            }).ToArray();
            var properties = Lines(_properties.Text).Select((line, index) =>
            {
                var p = Parts(line, 4);
                var existing = foundation.Properties.ElementAtOrDefault(index)?.Id ??
                    StudioSessionController.StableItemGuidUx4S(source.BlueprintId.Value, $"property:{index}:{p[0]}");
                return new ItemFoundationProperty(existing, p[0], Double(p[1]), p[2], Empty(p[3]));
            }).ToArray();
            var rebuiltCreation = creation with
            {
                Appearance = creation.Appearance with
                {
                    MeshAssetIds = CsvValues(appearance[0]),
                    MaterialAssetIds = CsvValues(appearance[1]),
                    Sockets = CsvValues(appearance[2]),
                    IconAssetId = appearance[3],
                    VisualEffectAssetIds = CsvValues(appearance[4]),
                    EquippedPreviewAssetId = appearance[5]
                },
                Inventory = new(Double(inventory[0]), Double(inventory[1]), Int(inventory[2]), Int(inventory[3]),
                    Double(inventory[4]), Double(inventory[5])),
                Stack = new(Int(inventory[6]), inventory[7], Bool(inventory[8])),
                Usage = new(Int(inventory[9]), Int(inventory[10]), Int(inventory[11]), Int(inventory[12]),
                    Long(inventory[13]), Int(inventory[14]), Bool(inventory[15]), inventory[16])
            };
            var critical = Parts(_critical.Text, 7); var transfer = Parts(_transfers.Text, 2);
            var allowed = CsvValues(transfer[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var modifiers = Lines(_modifiers.Text).Select((line, index) =>
            {
                var p = Parts(line, 7);
                var id = gameplay.Modifiers.ElementAtOrDefault(index)?.Id ??
                    StudioSessionController.StableItemGuidUx4S(source.BlueprintId.Value, $"modifier:{index}:{p[0]}");
                return new ItemModifierPolicy(id, 1, gameplay.Provenance, p[0], DoublePairs(p[1]), p[2], Empty(p[3]),
                    p[4], Empty(p[5]), Empty(p[6]));
            }).ToArray();
            var ability = Parts(_ability.Text, 7); var abilityProfile = Bool(ability[0])
                ? new ItemAbilityPolicy(gameplay.Ability?.AbilityDefinitionId ??
                    StudioSessionController.StableItemGuidUx4S(source.BlueprintId.Value, "ability"), ability[1],
                    DoublePairs(ability[2]), Int(ability[3]), Long(ability[4]), ability[5], ability[6]) : null;
            var slots = Lines(_equipmentSlots.Text).Select(line =>
            {
                var p = Parts(line, 5);
                return new ItemEquipmentSlotPolicy(p[0], p[1], p[2], Int(p[3]), CsvValues(p[4]));
            }).ToArray();
            var rebuiltGameplay = gameplay with
            {
                Critical = new(Bool(critical[0]), Bool(critical[1]), Bool(critical[2]), Bool(critical[3]),
                    Bool(critical[4]), Bool(critical[5]), Bool(critical[6])),
                Transfer = new(Enum.GetValues<ItemTransferDestination>().ToDictionary(item => item,
                    item => allowed.Contains(item.ToString())), CsvValues(transfer[1])),
                Modifiers = modifiers,
                Ability = abilityProfile,
                Equipment = new(slots, CsvValues(_requiredSlots.Text))
            };
            var rebuiltFoundation = foundation with
            {
                TypeId = foundation.TypeId,
                Type = foundation.Type with
                {
                    Slug = type[0],
                    AllowedSlots = CsvValues(type[1]),
                    AllowedPropertyKinds = CsvValues(type[2]),
                    RequiredRig = type[3]
                },
                Archetype = _slug.Text.Trim(),
                AppearanceAssetId = foundationParts[0],
                AppearanceRig = foundationParts[1],
                Quality = Int(foundationParts[2]),
                Level = Int(foundationParts[3]),
                BaseValue = Decimal(foundationParts[4]),
                Slots = CsvValues(foundationParts[5]),
                AllowedInstanceOverrides = CsvValues(foundationParts[6]),
                Properties = properties,
                Texts = localized,
                Creation = rebuiltCreation,
                Gameplay = rebuiltGameplay
            };

            var economy = Parts(_economy.Text, 7); var requirements = Parts(_requirements.Text, 6);
            var identification = Parts(_identification.Text, 5); var ownership = Parts(_ownership.Text, 4);
            var knowledge = Lines(_knowledge.Text).Select(line =>
            {
                var p = Parts(line, 3);
                return new ItemKnowledgeLevel(p[0], Int(p[1]), CsvValues(p[2]));
            }).ToArray();
            var history = Lines(_ownershipHistory.Text).Select((line, index) =>
            {
                var p = Parts(line, 5);
                var id = advanced.Ownership.History.ElementAtOrDefault(index)?.Id ??
                    StudioSessionController.StableItemGuidUx4S(source.BlueprintId.Value, $"owner:{index}:{p[0]}");
                return new ItemOwnershipEvent(id, p[0], p[1], p[2],
                    DateTimeOffset.Parse(p[3], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), p[4]);
            }).ToArray();
            var procs = Lines(_onHit.Text).Select((line, index) =>
            {
                var p = Parts(line, 7);
                var id = advanced.OnHitProcs.ElementAtOrDefault(index)?.Id ??
                    StudioSessionController.StableItemGuidUx4S(source.BlueprintId.Value, $"proc:{index}:{p[0]}");
                return new ItemOnHitProc(id, p[0], Double(p[1]), Double(p[2]), Empty(p[3]), p[4], p[5],
                    Bool(p[6]), advanced.Provenance);
            }).ToArray();
            var weapon = Parts(_weapon.Text, 5); var attacks = Lines(_attacks.Text).Select(line =>
            {
                var p = Parts(line, 7);
                return new ItemWeaponAttack(p[0], p[1], Double(p[2]), Double(p[3]), Double(p[4]), p[5], CsvValues(p[6]));
            }).ToArray();
            var weaponProfile = Bool(weapon[0]) ? new ItemWeaponProfile(weapon[1], Double(weapon[2]), Bool(weapon[3]), attacks, CsvValues(weapon[4])) : null;
            var armor = Parts(_armor.Text, 8); var armorProfile = Bool(armor[0]) ? new ItemArmorProfile(CsvValues(armor[1]),
                Double(armor[2]), Double(armor[3]), Double(armor[4]), armor[5], DoublePairs(armor[6]), CsvValues(armor[7])) : null;
            var ammo = Parts(_ammo.Text, 7); var ammoProfile = Bool(ammo[0]) ? new ItemAmmoPolicy(ammo[1], ammo[2],
                Int(ammo[3]), Double(ammo[4]), Empty(ammo[5]), Bool(ammo[6])) : null;
            var durability = Parts(_durability.Text, 9); var durabilityProfile = Bool(durability[0]) ? new ItemDurabilityPolicy(true,
                Int(durability[1]), Int(durability[2]), Double(durability[3]), Double(durability[4]), durability[5],
                Decimal(durability[6]), durability[7], durability[8]) : null;
            var container = Parts(_container.Text, 8); var containerProfile = Bool(container[0]) ? new ItemContainerPolicy(
                Int(container[1]), Double(container[2]), Int(container[3]), Double(container[4]), CsvValues(container[5]),
                CsvValues(container[6]), Bool(container[7]), true) : null;
            var contexts = Lines(_lootContexts.Text).Select((line, index) =>
            {
                var p = Parts(line, 10);
                var id = advanced.LootContexts.ElementAtOrDefault(index)?.Id ??
                    StudioSessionController.StableItemGuidUx4S(source.BlueprintId.Value, $"context:{index}:{p[0]}");
                return new ItemLootContextRule(id, p[0], Empty(p[1]), Empty(p[2]), Int(p[3]), Int(p[4]), Empty(p[5]),
                    CsvValues(p[6]), Empty(p[7]), Empty(p[8]), Double(p[9]), advanced.Provenance);
            }).ToArray();
            var unique = Parts(_unique.Text, 5); var ownershipLoot = Parts(_lootOwnership.Text, 4);
            var recipes = Lines(_recipes.Text).Select((line, index) =>
            {
                var p = Parts(line, 9);
                var id = advanced.Recipes.ElementAtOrDefault(index)?.Id ??
                    StudioSessionController.StableItemGuidUx4S(source.BlueprintId.Value, $"recipe:{index}:{p[0]}");
                var ingredients = CsvValues(p[1]).Select(value =>
                {
                    var segments = value.Split(':');
                    if (segments.Length < 4) throw new FormatException("Ingrédient invalide.");
                    return new ItemIngredientQuery(string.Join(':', segments[..^3]), Int(segments[^3]), Bool(segments[^2]), Int(segments[^1]));
                }).ToArray();
                return new ItemRecipeDefinition(id, p[0], ingredients, p[2], p[3], Int(p[4]), Int(p[5]),
                    Double(p[6]), CsvValues(p[7]), p[8]);
            }).ToArray();
            var generator = Parts(_generator.Text, 7); var affixes = Lines(_affixes.Text).Select((line, index) =>
            {
                var p = Parts(line, 8);
                var id = advanced.Generator?.Affixes.ElementAtOrDefault(index)?.Id ??
                    StudioSessionController.StableItemGuidUx4S(source.BlueprintId.Value, $"affix:{index}:{p[0]}");
                return new ItemAffixDefinition(id, p[0], p[1], Int(p[2]), Int(p[3]), p[4], CsvValues(p[5]),
                    CsvValues(p[6]), p[7]);
            }).ToArray();
            var generatorProfile = Bool(generator[0]) ? new ItemGeneratorPolicy(true, Int(generator[1]), Int(generator[2]),
                Int(generator[3]), CsvValues(generator[4]), IntPairs(generator[5]), affixes, Int(generator[6])) : null;
            var rebuiltAdvanced = advanced with
            {
                Economy = new(economy[0], Decimal(economy[1]), Double(economy[2]), Double(economy[3]),
                    Double(economy[4]), Double(economy[5]), DecimalPairs(economy[6])),
                Requirements = new(Int(requirements[0]), Int(requirements[1]), CsvValues(requirements[2]),
                    CsvValues(requirements[3]), CsvValues(requirements[4]), Empty(requirements[5])),
                Identification = new(identification[0], Int(identification[1]), Decimal(identification[2]),
                    identification[3], Bool(identification[4]), knowledge),
                Ownership = new(Bool(ownership[0]), ownership[1], ownership[2], Bool(ownership[3]), history),
                OnHitProcs = procs,
                Weapon = weaponProfile,
                Armor = armorProfile,
                Ammo = ammoProfile,
                Durability = durabilityProfile,
                Container = containerProfile,
                LootContexts = contexts,
                Unique = new(Bool(unique[0]), unique[1], Enum.Parse<ItemUniqueScope>(unique[2], true),
                    Long(unique[3]), Bool(unique[4])),
                LootOwnership = new(Enum.Parse<ItemLootOwnershipMode>(ownershipLoot[0], true), Long(ownershipLoot[1]),
                    Bool(ownershipLoot[2]), Bool(ownershipLoot[3])),
                Recipes = recipes,
                Generator = generatorProfile
            };
            var lootEntries = Lines(_lootEntries.Text).Select(line =>
            {
                var p = Parts(line, 8);
                if (!_itemReferences.TryGetValue(p[0], out var itemId)) throw new FormatException($"Objet de loot inconnu : {p[0]}");
                return new ItemLootEntryPolicy(itemId, p[1], Double(p[2]), Int(p[3]), Int(p[4]), Int(p[5]), p[6], Bool(p[7]));
            }).ToArray();
            var placements = Lines(_placements.Text).Select((line, index) =>
            {
                var p = Parts(line, 7);
                var id = operations.Inspector.Placements.ElementAtOrDefault(index)?.ItemInstanceId ??
                    StudioSessionController.StableItemGuidUx4S(source.BlueprintId.Value, $"placement:{index}");
                return new ItemInventoryPlacementPolicy(id, p[0], Empty(p[1]), Int(p[2]), Int(p[3]), Int(p[4]),
                    CsvValues(p[5]), p[6]);
            }).ToArray();
            var bindings = Lines(_events.Text).Select(line =>
            {
                var p = Parts(line, 3);
                var kind = Enum.Parse<ItemLifecycleEventType>(p[0], true);
                var id = operations.Events.Bindings.FirstOrDefault(item => item.Kind == kind)?.Id ??
                    StudioSessionController.StableItemGuidUx4S(source.BlueprintId.Value, $"event:{kind}");
                return new ItemLifecycleBindingPolicy(id, 1, operations.Provenance, kind, p[1], p[2]);
            }).ToArray();
            rebuiltFoundation = rebuiltFoundation with
            {
                Operations = operations with
                {
                    Loot = operations.Loot with { Entries = lootEntries },
                    Inspector = operations.Inspector with { Placements = placements },
                    Events = operations.Events with { Bindings = bindings }
                }
            };
            return source with
            {
                Slug = _slug.Text.Trim(),
                DisplayName = _displayName.Text.Trim(),
                Foundation = rebuiltFoundation,
                Advanced = rebuiltAdvanced,
                AcceptTypeMigration = _acceptMigration.ButtonPressed
            };
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        { error = exception.Message; return null; }
    }

    private void Preview()
    {
        var draft = ReadDraft(out var error); if (draft is null) { SetStatus($"Brouillon invalide : {error}", true); return; }
        _draft = draft; _previewResult = _session.PreviewItemUx4S(draft); _previewDryRun = _previewResult.Succeeded;
        RenderPreview(_previewResult); _apply.Disabled = !_previewResult.CanApply;
        SetStatus(_previewResult.Message, !_previewResult.Succeeded);
    }
    private void Apply()
    {
        var draft = ReadDraft(out var error); if (draft is null) { SetStatus(error, true); return; }
        var result = _session.ApplyApprovedItemUx4S(draft); _applied = result.Succeeded;
        SetStatus(result.Message, !result.Succeeded); if (!result.Succeeded) return;
        ItemChanged?.Invoke(); _previewResult = null; _apply.Disabled = true;
    }
    private void Undo() { var result = _session.UndoItemUx4S(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) Reload(); }
    private void Redo() { var result = _session.RedoItemUx4S(); SetStatus(result.Message, !result.Succeeded); if (result.Succeeded) Reload(); }
    private void Save() { var result = _session.Save(); SetStatus(result.Message, !result.Succeeded); }
    private void Reload()
    {
        if (_draft is null) return; _draft = _session.CreateItemDraftUx4S(_draft.BlueprintId);
        if (_draft is not null) Populate(_draft); _previewResult = null; _apply.Disabled = true; ItemChanged?.Invoke();
    }
    private void RenderPreview(StudioItemPreviewUx4S result)
    {
        foreach (var child in _diagnostics.GetChildren()) child.QueueFree();
        foreach (var diagnostic in result.Diagnostics.Take(5))
            _diagnostics.AddChild(StudioStyle.Label($"{diagnostic.HumanMessage} · {diagnostic.Path}", 9, StudioStyle.Danger, wrap: true));
        _summary.Text = $"[b]{(result.CanApply ? "APERÇU CANONIQUE · OBJET COMPLET" : "APERÇU REFUSÉ")}[/b]\n" +
            $"{result.Message}\n{result.PatchSummary}\nInverse : {result.InverseSummary}\n" +
            $"Fondation : {result.FoundationSummary}\nGameplay : {result.GameplaySummary}\n" +
            $"Avancé : {result.AdvancedSummary}\nOpérations : {result.OperationsSummary}\n{result.RuntimeSummary}\n" +
            $"[color={(_previewDryRun ? "#8ce0bd" : "#ff7777")}]Aperçu sans mutation : {_previewDryRun.ToString().ToLowerInvariant()}[/color]";
    }
    private void RenderEmpty(string text)
    { foreach (var child in _diagnostics.GetChildren()) child.QueueFree(); _summary.Text = text; }
    private void ClearControls()
    {
        _updating = true; foreach (var field in LineFields()) field.Text = string.Empty;
        foreach (var field in MultiFields()) field.Text = string.Empty;
        _acceptMigration.ButtonPressed = false; _updating = false; RenderEmpty("Choisissez explicitement un objet ou sa création.");
    }
    private void SetEditingEnabled(bool enabled)
    {
        _preview.Disabled = !enabled; _apply.Disabled = !enabled || !CanonicalPreviewReady;
        _undo.Disabled = !_session.CanUndo; _redo.Disabled = !_session.CanRedo; _save.Disabled = _session.Project is null;
    }
    private void InvalidatePreview()
    { if (_updating) return; _session.CancelItemUx4S(); _previewResult = null; _apply.Disabled = true; }
    private void ConnectEvents()
    {
        _item.ItemSelected += SelectionChanged; _preview.Pressed += Preview; _apply.Pressed += Apply;
        _undo.Pressed += Undo; _redo.Pressed += Redo; _save.Pressed += Save;
        foreach (var field in LineFields()) field.TextChanged += _ => InvalidatePreview();
        foreach (var field in MultiFields()) field.TextChanged += InvalidatePreview;
        _acceptMigration.Toggled += _ => InvalidatePreview();
    }

    internal bool PrepareEvidence(string mode)
    {
        _lastFailure = "none"; _previewResult = null;
        if ((_evidenceSession is not null || _evidenceRoot is not null) && !ReleaseEvidence()) return false;
        if (!TryOpenEvidence()) return false;
        var index = _choices.ToList().FindIndex(item => item.BlueprintId == _evidenceItemId);
        if (index < 0) { _lastFailure = "item_missing"; Close(); return false; }
        _updating = true; _item.Select(index + 2); _updating = false; SelectionChanged(0);
        if (_draft is null) { _lastFailure = "draft"; Close(); return false; }
        _draft = _session.CompleteItemDraftUx4S(_draft); Populate(_draft);
        _displayName.Text = $"{_displayName.Text} · preuve isolée";
        var project = _session.Project!; var history = _session.History!; var revision = history.State.Revision;
        var journal = history.Journal.ToArray(); var dirty = _session.IsDirty; Preview();
        _previewDryRun = ReferenceEquals(project, _session.Project) && ReferenceEquals(history, _session.History) &&
                         history.State.Revision == revision && history.Journal.SequenceEqual(journal) &&
                         _session.IsDirty == dirty && EvidenceSourceUntouched && CanonicalPreviewReady;
        _summary.Text = $"[b]{(_usesRealModule ? "MODULE RÉEL COPIÉ" : "FALLBACK CANONIQUE TEMPORAIRE")}[/b] · " +
                        $"session isolée, source strictement inchangée.\n{_summary.Text}";
        var ready = _previewDryRun && HasExplicitSelection && ShowsCreation && ShowsGameplay && ShowsAdvanced &&
                    ShowsOperations && ShowsHumanSurface && UsesIsolatedEvidence && EvidenceSourceUntouched &&
                    (_usesRealModule ^ _usesFallback);
        if (!ready)
        {
            _lastFailure = $"ready:preview={CanonicalPreviewReady}:selection={HasExplicitSelection}:creation={ShowsCreation}:" +
                           $"gameplay={ShowsGameplay}:advanced={ShowsAdvanced}:operations={ShowsOperations}:human={ShowsHumanSurface}:" +
                           $"isolated={UsesIsolatedEvidence}:source={EvidenceSourceUntouched}:origin={_usesRealModule ^ _usesFallback}";
            GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4S_EVIDENCE_FAILED failure={_lastFailure}"); Close(); return false;
        }
        SetStatus("Preuve UX4-S isolée prête · aucun runtime live exécuté.", false); return true;
    }
    internal bool RunUiTransactionEvidence()
    {
        var snapshot = CaptureSource(); if (snapshot is null) return false; StudioSessionController? reopened = null;
        try
        {
            if (!PrepareEvidence("ux4-s-item-authoring") || _evidenceSession is null ||
                !ReferenceEquals(_session, _evidenceSession) || !CanonicalPreviewReady) return false;
            var transaction = _session; var original = transaction.Project; if (original is null) return false;
            var originalJson = ProjectJsonSerializer.Serialize(original); Apply(); var appliedProject = transaction.Project;
            var applied = _applied && appliedProject is not null && ProjectJsonSerializer.Serialize(appliedProject) != originalJson;
            var appliedJson = appliedProject is null ? string.Empty : ProjectJsonSerializer.Serialize(appliedProject);
            if (applied) Undo(); var undone = applied && ProjectJsonSerializer.Serialize(transaction.Project!) == originalJson;
            if (undone) Redo(); var redone = undone && ProjectJsonSerializer.Serialize(transaction.Project!) == appliedJson;
            var saved = redone && transaction.Save().Succeeded; var persisted = false;
            if (saved && transaction.Session is { } workspace)
            {
                reopened = new(); persisted = reopened.Open(workspace.RootDirectory).Succeeded &&
                ProjectJsonSerializer.Serialize(reopened.Project!) == appliedJson;
            }
            var sourceUntouched = SourceMatches(snapshot); reopened?.Close(); reopened = null; Close();
            return applied && undone && redone && saved && persisted && sourceUntouched &&
                   EvidenceResourcesReleased && SourceSessionRestored;
        }
        catch (Exception exception)
        { GD.PrintErr($"OPENRPG_FORGE_STUDIO_UX4S_UI_TRANSACTION_FAILED type={exception.GetType().Name} message={exception.Message}"); return false; }
        finally
        {
            reopened?.Close(); if (_evidenceSession is not null || _evidenceRoot is not null) Close();
            _sourceSession.RestoreItemApprovalUx4S(snapshot.Approval);
        }
    }
    internal void CloseEvidence() => Close();
    private bool TryOpenEvidence()
    {
        var snapshot = CaptureSource(); var evidence = _sourceSession.CreateUx4SEvidenceProject();
        if (snapshot is null || evidence is null) return false;
        var root = Path.Combine(Path.GetTempPath(), "openrpg-forge-studio-ux4s-evidence-" + Guid.NewGuid().ToString("N"));
        var session = new StudioSessionController();
        try
        {
            if (!ProjectSession.Create(root, evidence.Project).Succeeded || !session.Open(root).Succeeded)
            { session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); return false; }
            _sourceSnapshot = snapshot; _evidenceSession = session; _evidenceRoot = root; _evidenceItemId = evidence.BlueprintId;
            _session = session; _usesRealModule = evidence.UsesRealModule; _usesFallback = evidence.UsesCanonicalFallback;
            _previewDryRun = false; _resourcesReleased = false; _sourceRestored = false; OpenBound();
            return SourceMatches(snapshot) && (_usesRealModule ^ _usesFallback);
        }
        catch { session.Close(); _resourcesReleased = DeleteEvidenceRoot(root); _session = _sourceSession; _sourceRestored = true; return false; }
    }
    private StudioItemEvidenceSourceSnapshotUx4S? CaptureSource()
    {
        var project = _sourceSession.Project; var history = _sourceSession.History;
        return project is null || history is null ? null : new(project, history, history.State.Revision,
            history.Journal.ToArray(), _sourceSession.IsDirty, history.CanUndo, history.CanRedo,
            _sourceSession.ActiveAreaId, _sourceSession.SelectedEntityIds.ToArray(), _sourceSession.SelectedEntityId,
            _sourceSession.CaptureItemApprovalUx4S());
    }
    private bool SourceMatches(StudioItemEvidenceSourceSnapshotUx4S snapshot)
    {
        var history = _sourceSession.History;
        return ReferenceEquals(snapshot.Project, _sourceSession.Project) && ReferenceEquals(snapshot.History, history) &&
               history is not null && history.State.Revision == snapshot.Revision && history.Journal.SequenceEqual(snapshot.Journal) &&
               history.CanUndo == snapshot.CanUndo && history.CanRedo == snapshot.CanRedo && _sourceSession.IsDirty == snapshot.Dirty &&
               _sourceSession.ActiveAreaId == snapshot.ActiveAreaId && _sourceSession.SelectedEntityIds.SequenceEqual(snapshot.Selection) &&
               _sourceSession.SelectedEntityId == snapshot.ActiveEntity && _sourceSession.CaptureItemApprovalUx4S() == snapshot.Approval;
    }
    private bool ReleaseEvidence()
    {
        var session = _evidenceSession; var root = _evidenceRoot; _evidenceSession = null; _evidenceRoot = null;
        _sourceSnapshot = null; _evidenceItemId = null; _session = _sourceSession; _usesRealModule = false;
        _usesFallback = false; _previewDryRun = false; var released = true;
        try { session?.Close(); } catch { released = false; }
        released &= DeleteEvidenceRoot(root); _resourcesReleased = released; _sourceRestored = true; return released;
    }

    private IEnumerable<LineEdit> LineFields() => [_slug, _displayName, _type, _foundation, _appearance,
        _inventoryUsage, _critical, _transfers, _ability, _requiredSlots, _economy, _requirements, _identification,
        _ownership, _weapon, _armor, _ammo, _durability, _container, _unique, _lootOwnership, _generator];
    private IEnumerable<TextEdit> MultiFields() => [_localizations, _properties, _modifiers, _equipmentSlots,
        _knowledge, _ownershipHistory, _onHit, _attacks, _lootEntries, _lootContexts, _recipes, _affixes, _placements, _events];
    private string ItemReferenceLabel(Guid id)
    {
        var known = _session.ItemChoicesUx4S.FirstOrDefault(item => item.BlueprintId.Value == id);
        var label = known is not null ? known.Label.Split('·')[1].Trim() : $"référence héritée protégée {_itemReferences.Count + 1}";
        _itemReferences[label] = id; return label;
    }
    private static VBoxContainer Tab(TabContainer tabs, string title)
    {
        var scroll = new ScrollContainer
        {
            Name = title,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            FollowFocus = true
        };
        var form = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; form.AddThemeConstantOverride("separation", 7);
        scroll.AddChild(form); tabs.AddChild(scroll); return form;
    }
    private static OptionButton Choice(Container parent, string label)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted));
        var result = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(result); parent.AddChild(box); return result;
    }
    private static LineEdit Field(Container parent, string label, string placeholder)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted, wrap: true));
        var result = new LineEdit { PlaceholderText = placeholder, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(result); parent.AddChild(box); return result;
    }
    private static TextEdit Multi(Container parent, string label, float height)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; box.AddChild(StudioStyle.Label(label, 8, StudioStyle.Muted, wrap: true));
        var result = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, height),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary
        }; box.AddChild(result); parent.AddChild(box); return result;
    }
    private static IReadOnlyList<string> Lines(string value) => value.Replace("\r", string.Empty, StringComparison.Ordinal)
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string[] Parts(string value, int count)
    {
        var parts = value.Split('|').Select(item => item.Trim()).ToArray();
        if (parts.Length != count) throw new FormatException($"{count} colonnes attendues, {parts.Length} reçues.");
        return parts;
    }
    private static IReadOnlyList<string> CsvValues(string value) => value.Split(',',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).ToArray();
    private static string Csv(IEnumerable<string> values) => string.Join(',', values);
    private static string Pairs<T>(IReadOnlyDictionary<string, T> values) => string.Join(',', values
        .OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}={Convert.ToString(item.Value, CultureInfo.InvariantCulture)}"));
    private static Dictionary<string, double> DoublePairs(string value) => PairValues(value)
        .ToDictionary(item => item.Key, item => Double(item.Value), StringComparer.Ordinal);
    private static Dictionary<string, decimal> DecimalPairs(string value) => PairValues(value)
        .ToDictionary(item => item.Key, item => Decimal(item.Value), StringComparer.Ordinal);
    private static Dictionary<string, int> IntPairs(string value) => PairValues(value)
        .ToDictionary(item => item.Key, item => Int(item.Value), StringComparer.Ordinal);
    private static IEnumerable<KeyValuePair<string, string>> PairValues(string value)
    {
        foreach (var item in CsvValues(value))
        {
            var separator = item.IndexOf('='); if (separator <= 0) throw new FormatException($"Paire clé=valeur invalide : {item}");
            yield return new(item[..separator].Trim(), item[(separator + 1)..].Trim());
        }
    }
    private static int Int(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    private static long Long(string value) => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    private static double Double(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static decimal Decimal(string value) => decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
    private static bool Bool(string value) => bool.Parse(value);
    private static string? Empty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Join(params object?[] values) => string.Join(" | ", values.Select(value => value switch
    { bool boolean => boolean.ToString().ToLowerInvariant(), IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture), _ => value?.ToString() ?? string.Empty }));
    private static IEnumerable<string> VisibleText(Node node)
    {
        if (node is Label label && label.Visible) yield return label.Text;
        if (node is Button button && button.Visible) yield return button.Text;
        foreach (var child in node.GetChildren()) foreach (var text in VisibleText(child)) yield return text;
    }
    private static bool ContainsGuid(string value) => Regex.IsMatch(value,
        "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}");
    private static bool DeleteEvidenceRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) return true;
        try { if (Directory.Exists(root)) Directory.Delete(root, true); return !Directory.Exists(root); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return false; }
    }
    private void SetStatus(string message, bool error) => StatusChanged?.Invoke(message, error);
}
