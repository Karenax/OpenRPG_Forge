using Forge.Domain.Validation;

namespace Forge.Domain.Items;

public static class ItemAdvancedValidator
{
    public static IReadOnlyList<DomainDiagnostic> Validate(ItemAdvancedProfile profile, string path)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || profile.SchemaVersion != ItemAdvancedProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance))
            Add("item.advanced.identity_invalid", path, "Item profile identity is invalid.");
        if (string.IsNullOrWhiteSpace(profile.Economy.Currency) || profile.Economy.BaseCost < 0 ||
            !Multipliers(profile.Economy.QualityMultiplier, profile.Economy.RarityMultiplier,
                profile.Economy.WearMultiplier, profile.Economy.DemandMultiplier) ||
            profile.Economy.MarketOverrides.Any(item => string.IsNullOrWhiteSpace(item.Key) || item.Value < 0))
            Add("item.advanced.economy_invalid", $"{path}/economy", "Economic value, currency or market overrides are invalid.");
        if (profile.Requirements.MinimumLevel < 0 || profile.Requirements.MinimumLore < 0 ||
            !DistinctWords(profile.Requirements.Skills) || !DistinctWords(profile.Requirements.Traits) ||
            !DistinctWords(profile.Requirements.Factions) || !SafeReference(profile.Requirements.ScriptCondition))
            Add("item.advanced.requirements_invalid", $"{path}/requirements", "Item requirements are invalid.");
        if (profile.Identification.Difficulty < 0 || profile.Identification.AttemptCost < 0 ||
            string.IsNullOrWhiteSpace(profile.Identification.InitialLevel) ||
            string.IsNullOrWhiteSpace(profile.Identification.Currency) || profile.Identification.Levels.Count == 0 ||
            profile.Identification.Levels.Any(item => string.IsNullOrWhiteSpace(item.Id) || item.Threshold < 0) ||
            profile.Identification.Levels.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != profile.Identification.Levels.Count ||
            profile.Identification.Levels.All(item => item.Id != profile.Identification.InitialLevel))
            Add("item.advanced.identification_invalid", $"{path}/identification", "Identification levels, cost or difficulty are invalid.");
        if (profile.Ownership.Stolen && (string.IsNullOrWhiteSpace(profile.Ownership.CurrentOwnerId) ||
                                         string.IsNullOrWhiteSpace(profile.Ownership.Jurisdiction)) ||
            profile.Ownership.History.Select(item => item.Id).Distinct().Count() != profile.Ownership.History.Count ||
            profile.Ownership.History.Any(item => item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.OwnerId) ||
                                                  string.IsNullOrWhiteSpace(item.Jurisdiction) || string.IsNullOrWhiteSpace(item.Provenance)))
            Add("item.advanced.ownership_invalid", $"{path}/ownership", "Ownership history or stolen status is invalid.");

        var procIds = new HashSet<Guid>();
        foreach (var proc in profile.OnHitProcs)
            if (proc.Id == Guid.Empty || !procIds.Add(proc.Id) || string.IsNullOrWhiteSpace(proc.Trigger) ||
                proc.Chance is < 0 or > 1 || proc.InternalCooldownSeconds < 0 || string.IsNullOrWhiteSpace(proc.EffectId) ||
                string.IsNullOrWhiteSpace(proc.Attribution) || string.IsNullOrWhiteSpace(proc.Provenance) || !SafeReference(proc.Condition))
                Add("item.advanced.on_hit_invalid", $"{path}/onHitProcs/{proc.Id}", "On-hit proc identity, chance, effect or condition is invalid.");
        if (profile.Weapon is { } weapon && (string.IsNullOrWhiteSpace(weapon.WeaponClass) || weapon.AttacksPerSecond <= 0 ||
                                             weapon.Attacks.Count == 0 || !DistinctWords(weapon.Tags) ||
                                             weapon.Attacks.Any(item => string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.DamageChannel) ||
                                                                        item.MinimumDamage < 0 || item.MaximumDamage < item.MinimumDamage || item.ReachMeters <= 0 ||
                                                                        string.IsNullOrWhiteSpace(item.AnimationId))))
            Add("item.advanced.weapon_invalid", $"{path}/weapon", "Weapon attacks, damage, reach or animation are invalid.");
        if (profile.Armor is { } armor && (armor.Coverage.Count == 0 || !DistinctWords(armor.Coverage) ||
                                           armor.Protection is < 0 or > 100 || armor.Encumbrance < 0 || armor.Noise < 0 ||
                                           string.IsNullOrWhiteSpace(armor.Material) || armor.DamageResistances.Any(item =>
                                               string.IsNullOrWhiteSpace(item.Key) || !double.IsFinite(item.Value))))
            Add("item.advanced.armor_invalid", $"{path}/armor", "Armor coverage, protection, material or resistances are invalid.");
        if (profile.Ammo is { } ammo && (string.IsNullOrWhiteSpace(ammo.AmmoTag) || string.IsNullOrWhiteSpace(ammo.ProjectileAssetId) ||
                                         ammo.MaximumStack < 1 || ammo.RecoveryChance is < 0 or > 1))
            Add("item.advanced.ammo_invalid", $"{path}/ammo", "Ammo tag, projectile, stack or recovery chance is invalid.");
        if (profile.Durability is { } durability && (durability.Maximum < 1 || durability.Initial is < 0 || durability.Initial > durability.Maximum ||
                                                     durability.WearPerUse < 0 || durability.WearPerHit < 0 || string.IsNullOrWhiteSpace(durability.RepairSkill) ||
                                                     durability.RepairCostPerPoint < 0 || string.IsNullOrWhiteSpace(durability.BrokenEffectId)))
            Add("item.advanced.durability_invalid", $"{path}/durability", "Durability, wear, repair or broken-state data is invalid.");
        if (profile.Container is { } container && (container.CapacitySlots < 1 || container.CapacityMass <= 0 ||
                                                   container.MaximumDepth is < 1 or > 16 || container.NestedWeightMultiplier is < 0 or > 1 ||
                                                   !DistinctWords(container.AllowedTags) || !DistinctWords(container.RequiredPermissions) || !container.PreventCycles))
            Add("item.advanced.container_invalid", $"{path}/container", "Container capacity, depth, permissions or cycle prevention is invalid.");

        var contextIds = new HashSet<Guid>();
        foreach (var context in profile.LootContexts)
            if (context.Id == Guid.Empty || !contextIds.Add(context.Id) || string.IsNullOrWhiteSpace(context.Source) ||
                context.MinimumLevel < 0 || context.MaximumLevel < context.MinimumLevel || context.Weight <= 0 ||
                !DistinctWords(context.RequiredTags) || string.IsNullOrWhiteSpace(context.Provenance))
                Add("item.advanced.loot_context_invalid", $"{path}/lootContexts/{context.Id}", "Loot context identity, levels, tags or weight is invalid.");
        if (profile.Unique.Enabled && (string.IsNullOrWhiteSpace(profile.Unique.RegistryKey) || profile.Unique.ReservationSeconds < 0))
            Add("item.advanced.unique_invalid", $"{path}/unique", "Unique registry key or reservation duration is invalid.");
        if (profile.LootOwnership.ExpirationSeconds < 0)
            Add("item.advanced.loot_ownership_invalid", $"{path}/lootOwnership", "Loot ownership expiration is invalid.");

        var recipeIds = new HashSet<Guid>();
        foreach (var recipe in profile.Recipes)
            if (recipe.Id == Guid.Empty || !recipeIds.Add(recipe.Id) || string.IsNullOrWhiteSpace(recipe.Slug) ||
                recipe.Ingredients.Count == 0 || recipe.Ingredients.Any(item => string.IsNullOrWhiteSpace(item.Query) || item.Quantity < 1 || item.MinimumQuality < 0) ||
                string.IsNullOrWhiteSpace(recipe.StationTag) || string.IsNullOrWhiteSpace(recipe.SkillId) || recipe.Difficulty < 0 ||
                recipe.MinimumQuality < 0 || recipe.DurationSeconds < 0 || !DistinctWords(recipe.ByproductSlugs) || string.IsNullOrWhiteSpace(recipe.Provenance))
                Add("item.advanced.recipe_invalid", $"{path}/recipes/{recipe.Id}", "Recipe ingredients, station, skill, quality or provenance is invalid.");
        if (profile.Generator is { } generator)
        {
            var affixIds = new HashSet<Guid>();
            if (generator.Budget < 0 || generator.MinimumAffixes < 0 || generator.MaximumAffixes < generator.MinimumAffixes ||
                !DistinctWords(generator.Pools) || generator.RarityWeights.Count == 0 || generator.RarityWeights.Any(item =>
                    string.IsNullOrWhiteSpace(item.Key) || item.Value < 0) || generator.Affixes.Any(affix =>
                    affix.Id == Guid.Empty || !affixIds.Add(affix.Id) || string.IsNullOrWhiteSpace(affix.Key) ||
                    string.IsNullOrWhiteSpace(affix.Pool) || affix.Cost < 0 || affix.Weight < 0 ||
                    string.IsNullOrWhiteSpace(affix.Rarity) || !DistinctWords(affix.Exclusions) ||
                    !DistinctWords(affix.PropertyIds) || string.IsNullOrWhiteSpace(affix.Provenance)))
                Add("item.advanced.generator_invalid", $"{path}/generator", "Generator budget, pools, rarity or affixes are invalid.");
        }
        return diagnostics;

        void Add(string code, string diagnosticPath, string message) => diagnostics.Add(new(code, message, diagnosticPath));
    }

    private static bool Multipliers(params double[] values) => values.All(item => double.IsFinite(item) && item >= 0);
    private static bool DistinctWords(IReadOnlyList<string> values) =>
        values.All(item => !string.IsNullOrWhiteSpace(item)) && values.Distinct(StringComparer.Ordinal).Count() == values.Count;
    private static bool SafeReference(string? value) => string.IsNullOrWhiteSpace(value) ||
        value.All(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-' or '/');
}
