namespace Forge.Domain.Items;

public sealed record ItemEconomicValue(
    string Currency,
    decimal BaseCost,
    double QualityMultiplier,
    double RarityMultiplier,
    double WearMultiplier,
    double DemandMultiplier,
    IReadOnlyDictionary<string, decimal> MarketOverrides);

public sealed record ItemRequirementSet(
    int MinimumLevel,
    int MinimumLore,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Traits,
    IReadOnlyList<string> Factions,
    string? ScriptCondition);

public sealed record ItemKnowledgeLevel(string Id, int Threshold, IReadOnlyList<string> RevealedPropertyIds);
public sealed record ItemIdentificationPolicy(
    string InitialLevel,
    int Difficulty,
    decimal AttemptCost,
    string Currency,
    bool ShareWithParty,
    IReadOnlyList<ItemKnowledgeLevel> Levels);

public sealed record ItemOwnershipEvent(Guid Id, string OwnerId, string Jurisdiction, string EvidenceId,
    DateTimeOffset OccurredAt, string Provenance);
public sealed record ItemOwnershipPolicy(
    bool Stolen,
    string CurrentOwnerId,
    string Jurisdiction,
    bool CanBeLaundered,
    IReadOnlyList<ItemOwnershipEvent> History);

public sealed record ItemOnHitProc(
    Guid Id,
    string Trigger,
    double Chance,
    double InternalCooldownSeconds,
    string? Condition,
    string EffectId,
    string Attribution,
    bool Deterministic,
    string Provenance);

public sealed record ItemWeaponAttack(string Id, string DamageChannel, double MinimumDamage, double MaximumDamage,
    double ReachMeters, string AnimationId, IReadOnlyList<string> RequiredTags);
public sealed record ItemWeaponProfile(
    string WeaponClass,
    double AttacksPerSecond,
    bool TwoHanded,
    IReadOnlyList<ItemWeaponAttack> Attacks,
    IReadOnlyList<string> Tags);

public sealed record ItemArmorProfile(
    IReadOnlyList<string> Coverage,
    double Protection,
    double Encumbrance,
    double Noise,
    string Material,
    IReadOnlyDictionary<string, double> DamageResistances,
    IReadOnlyList<string> DamageStateVisuals);

public sealed record ItemAmmoPolicy(
    string AmmoTag,
    string ProjectileAssetId,
    int MaximumStack,
    double RecoveryChance,
    string? ElementalPayload,
    bool ConsumeOnUse);

public sealed record ItemDurabilityPolicy(
    bool Enabled,
    int Maximum,
    int Initial,
    double WearPerUse,
    double WearPerHit,
    string RepairSkill,
    decimal RepairCostPerPoint,
    string BrokenEffectId,
    string BrokenVisualId);

public sealed record ItemContainerPolicy(
    int CapacitySlots,
    double CapacityMass,
    int MaximumDepth,
    double NestedWeightMultiplier,
    IReadOnlyList<string> AllowedTags,
    IReadOnlyList<string> RequiredPermissions,
    bool PersistContents,
    bool PreventCycles);

public sealed record ItemLootContextRule(
    Guid Id,
    string Source,
    string? Biome,
    string? Faction,
    int MinimumLevel,
    int MaximumLevel,
    string? Difficulty,
    IReadOnlyList<string> RequiredTags,
    string? PlayerCondition,
    string? EventId,
    double Weight,
    string Provenance);

public enum ItemUniqueScope
{
    World,
    Party,
    Player,
    Season
}

public sealed record ItemUniquePolicy(bool Enabled, string RegistryKey, ItemUniqueScope Scope,
    long ReservationSeconds, bool ReleaseOnDestroy);

public enum ItemLootOwnershipMode
{
    Public,
    Party,
    Personal,
    NeedGreed,
    GameMaster
}

public sealed record ItemLootOwnershipPolicy(ItemLootOwnershipMode Mode, long ExpirationSeconds,
    bool TransferToPartyAfterExpiration, bool RequireServerAuthority);

public sealed record ItemIngredientQuery(string Query, int Quantity, bool Consume, int MinimumQuality);
public sealed record ItemRecipeDefinition(
    Guid Id,
    string Slug,
    IReadOnlyList<ItemIngredientQuery> Ingredients,
    string StationTag,
    string SkillId,
    int Difficulty,
    int MinimumQuality,
    double DurationSeconds,
    IReadOnlyList<string> ByproductSlugs,
    string Provenance);

public sealed record ItemAffixDefinition(Guid Id, string Key, string Pool, int Cost, int Weight, string Rarity,
    IReadOnlyList<string> Exclusions, IReadOnlyList<string> PropertyIds, string Provenance);
public sealed record ItemGeneratorPolicy(
    bool Enabled,
    int Budget,
    int MinimumAffixes,
    int MaximumAffixes,
    IReadOnlyList<string> Pools,
    IReadOnlyDictionary<string, int> RarityWeights,
    IReadOnlyList<ItemAffixDefinition> Affixes,
    int SeedOffset);

public sealed record ItemAdvancedProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool RequiredAtRuntime,
    ItemEconomicValue Economy,
    ItemRequirementSet Requirements,
    ItemIdentificationPolicy Identification,
    ItemOwnershipPolicy Ownership,
    IReadOnlyList<ItemOnHitProc> OnHitProcs,
    ItemWeaponProfile? Weapon,
    ItemArmorProfile? Armor,
    ItemAmmoPolicy? Ammo,
    ItemDurabilityPolicy? Durability,
    ItemContainerPolicy? Container,
    IReadOnlyList<ItemLootContextRule> LootContexts,
    ItemUniquePolicy Unique,
    ItemLootOwnershipPolicy LootOwnership,
    IReadOnlyList<ItemRecipeDefinition> Recipes,
    ItemGeneratorPolicy? Generator)
{
    public const int CurrentSchemaVersion = 1;

    public static ItemAdvancedProfile CreateDefault(Guid id, string provenance) => new(
        id, CurrentSchemaVersion, provenance, false,
        new("gold", 10, 1, 1, 1, 1, new Dictionary<string, decimal>(StringComparer.Ordinal)),
        new(0, 0, [], [], [], null),
        new("unknown", 10, 0, "gold", true,
            [new("unknown", 0, []), new("identified", 10, ["*"])]),
        new(false, string.Empty, "neutral", true, []), [], null, null, null,
        new(false, 100, 100, 0, 0, "repair", 0, "item.broken", "item.broken"),
        null, [], new(false, string.Empty, ItemUniqueScope.World, 0, true),
        new(ItemLootOwnershipMode.Public, 0, false, true), [], null);
}
