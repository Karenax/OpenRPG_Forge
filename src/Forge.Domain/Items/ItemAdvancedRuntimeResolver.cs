using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Forge.Domain.Items;

public sealed record ItemRuntimeContext(
    int Level,
    int Lore,
    IReadOnlySet<string> Skills,
    IReadOnlySet<string> Traits,
    IReadOnlySet<string> Factions,
    IReadOnlySet<string> Conditions,
    string Market,
    string Currency,
    double Demand,
    int CurrentDurability,
    int Seed,
    string ActorId,
    bool ServerAuthorized);

public sealed record ItemRequirementDecision(bool Allowed, IReadOnlyList<string> Missing);
public sealed record ItemProcDecision(bool Triggered, Guid? ProcId, string? EffectId, double Roll);
public sealed record ItemDurabilityState(int Current, bool Broken, decimal RepairCost, string? BrokenEffectId);
public sealed record ItemLootContextDecision(Guid? RuleId, double Weight, string Code);
public sealed record ItemGeneratedAffix(Guid Id, string Key, int Cost, string Rarity, IReadOnlyList<string> PropertyIds);
public sealed record ItemGenerationResult(int Seed, int BudgetSpent, string Rarity, IReadOnlyList<ItemGeneratedAffix> Affixes, string Explanation);

public static class ItemAdvancedRuntimeResolver
{
    public static decimal ResolveEconomicValue(ItemAdvancedProfile profile, ItemRuntimeContext context)
    {
        RequireValid(profile);
        var economy = profile.Economy;
        var basis = economy.MarketOverrides.GetValueOrDefault(context.Market, economy.BaseCost);
        var durabilityFactor = profile.Durability is { Enabled: true, Maximum: > 0 }
            ? Math.Clamp(context.CurrentDurability / (double)profile.Durability.Maximum, 0, 1)
            : 1;
        var demand = Math.Max(0, context.Demand) * economy.DemandMultiplier;
        return decimal.Round(basis * (decimal)(economy.QualityMultiplier * economy.RarityMultiplier *
                                              (1 - ((1 - durabilityFactor) * economy.WearMultiplier)) * demand), 2,
            MidpointRounding.ToEven);
    }

    public static ItemRequirementDecision ResolveRequirements(ItemAdvancedProfile profile, ItemRuntimeContext context)
    {
        RequireValid(profile);
        var missing = new List<string>();
        if (context.Level < profile.Requirements.MinimumLevel) missing.Add($"level:{profile.Requirements.MinimumLevel}");
        if (context.Lore < profile.Requirements.MinimumLore) missing.Add($"lore:{profile.Requirements.MinimumLore}");
        missing.AddRange(profile.Requirements.Skills.Where(item => !context.Skills.Contains(item)).Select(item => $"skill:{item}"));
        missing.AddRange(profile.Requirements.Traits.Where(item => !context.Traits.Contains(item)).Select(item => $"trait:{item}"));
        if (profile.Requirements.Factions.Count > 0 && !profile.Requirements.Factions.Any(context.Factions.Contains))
            missing.Add("faction:any");
        if (profile.Requirements.ScriptCondition is { } script && !context.Conditions.Contains(script))
            missing.Add($"condition:{script}");
        return new(missing.Count == 0, missing);
    }

    public static IReadOnlySet<string> ResolveIdentifiedProperties(ItemAdvancedProfile profile, int knowledge)
    {
        RequireValid(profile);
        return profile.Identification.Levels.Where(item => item.Threshold <= knowledge)
            .SelectMany(item => item.RevealedPropertyIds).ToHashSet(StringComparer.Ordinal);
    }

    public static ItemProcDecision ResolveOnHit(ItemAdvancedProfile profile, string trigger,
        IReadOnlyDictionary<Guid, DateTimeOffset> lastTriggered, DateTimeOffset now, ItemRuntimeContext context)
    {
        RequireValid(profile);
        foreach (var proc in profile.OnHitProcs.Where(item => string.Equals(item.Trigger, trigger, StringComparison.Ordinal))
                     .OrderBy(item => item.Id))
        {
            if (proc.Condition is { } condition && !context.Conditions.Contains(condition)) continue;
            if (lastTriggered.TryGetValue(proc.Id, out var last) && last.AddSeconds(proc.InternalCooldownSeconds) > now) continue;
            var roll = Roll(context.Seed, profile.Id, proc.Id, context.ActorId);
            if (roll <= proc.Chance) return new(true, proc.Id, proc.EffectId, roll);
        }
        return new(false, null, null, 1);
    }

    public static ItemDurabilityState ApplyWear(ItemAdvancedProfile profile, int current, double uses, double hits)
    {
        RequireValid(profile);
        if (profile.Durability is not { Enabled: true } durability)
            return new(current, false, 0, null);
        var wear = (int)Math.Ceiling((uses * durability.WearPerUse) + (hits * durability.WearPerHit));
        var next = Math.Clamp(current - wear, 0, durability.Maximum);
        return new(next, next == 0, (durability.Maximum - next) * durability.RepairCostPerPoint,
            next == 0 ? durability.BrokenEffectId : null);
    }

    public static ItemLootContextDecision ResolveLootContext(ItemAdvancedProfile profile, string source,
        string? biome, string? faction, string? difficulty, IReadOnlySet<string> tags, string? eventId,
        ItemRuntimeContext context)
    {
        RequireValid(profile);
        var eligible = profile.LootContexts.Where(item => string.Equals(item.Source, source, StringComparison.Ordinal) &&
            (item.Biome is null || string.Equals(item.Biome, biome, StringComparison.Ordinal)) &&
            (item.Faction is null || string.Equals(item.Faction, faction, StringComparison.Ordinal)) &&
            context.Level >= item.MinimumLevel && context.Level <= item.MaximumLevel &&
            (item.Difficulty is null || string.Equals(item.Difficulty, difficulty, StringComparison.Ordinal)) &&
            item.RequiredTags.All(tags.Contains) &&
            (item.PlayerCondition is null || context.Conditions.Contains(item.PlayerCondition)) &&
            (item.EventId is null || string.Equals(item.EventId, eventId, StringComparison.Ordinal))).OrderBy(item => item.Id).ToArray();
        if (eligible.Length == 0) return new(null, 0, "item.loot_context.none");
        var total = eligible.Sum(item => item.Weight);
        var cursor = Roll(context.Seed, profile.Id, Guid.Empty, source) * total;
        foreach (var rule in eligible)
        {
            cursor -= rule.Weight;
            if (cursor <= 0) return new(rule.Id, rule.Weight, "item.loot_context.selected");
        }
        return new(eligible[^1].Id, eligible[^1].Weight, "item.loot_context.selected");
    }

    public static ItemGenerationResult Generate(ItemAdvancedProfile profile, ItemRuntimeContext context)
    {
        RequireValid(profile);
        var generator = profile.Generator ?? throw new InvalidOperationException("item.generator.missing");
        if (!generator.Enabled) throw new InvalidOperationException("item.generator.disabled");
        var candidates = generator.Affixes.Where(item => generator.Pools.Contains(item.Pool) && item.Cost <= generator.Budget)
            .OrderBy(item => item.Id).ToList();
        var selected = new List<ItemGeneratedAffix>();
        var spent = 0;
        var step = 0;
        while (selected.Count < generator.MaximumAffixes && candidates.Count > 0)
        {
            var compatible = candidates.Where(candidate => candidate.Cost + spent <= generator.Budget &&
                selected.All(existing => !candidate.Exclusions.Contains(existing.Key) &&
                                         !generator.Affixes.Single(item => item.Id == existing.Id).Exclusions.Contains(candidate.Key))).ToArray();
            if (compatible.Length == 0) break;
            var total = compatible.Sum(item => item.Weight);
            if (total <= 0) break;
            var cursor = Roll(context.Seed + generator.SeedOffset + step++, profile.Id, Guid.Empty, "affix") * total;
            var picked = compatible[^1];
            foreach (var candidate in compatible) { cursor -= candidate.Weight; if (cursor <= 0) { picked = candidate; break; } }
            selected.Add(new(picked.Id, picked.Key, picked.Cost, picked.Rarity, picked.PropertyIds));
            spent += picked.Cost;
            candidates.RemoveAll(item => item.Id == picked.Id);
        }
        if (selected.Count < generator.MinimumAffixes)
            throw new InvalidOperationException("item.generator.minimum_unreachable");
        var rarity = selected.Select(item => item.Rarity).GroupBy(item => item, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal).FirstOrDefault()?.Key ?? "common";
        return new(context.Seed + generator.SeedOffset, spent, rarity, selected,
            $"seed={context.Seed + generator.SeedOffset}; budget={spent}/{generator.Budget}; affixes={string.Join(',', selected.Select(item => item.Key))}");
    }

    public static bool CanNest(Guid containerId, Guid itemId, IReadOnlyDictionary<Guid, Guid?> parents, int maximumDepth)
    {
        if (containerId == itemId || maximumDepth < 1) return false;
        var seen = new HashSet<Guid> { itemId };
        var current = containerId;
        for (var depth = 0; depth < maximumDepth; depth++)
        {
            if (!seen.Add(current)) return false;
            if (!parents.TryGetValue(current, out var parent) || parent is null) return true;
            current = parent.Value;
        }
        return false;
    }

    private static void RequireValid(ItemAdvancedProfile profile)
    {
        var diagnostics = ItemAdvancedValidator.Validate(profile, $"items/{profile.Id}");
        if (diagnostics.Count > 0) throw new InvalidOperationException($"{diagnostics[0].Code}: {diagnostics[0].Path}");
    }

    private static double Roll(int seed, Guid profileId, Guid itemId, string scope)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{seed}|{profileId:D}|{itemId:D}|{scope}"));
        return BinaryPrimitives.ReadUInt64BigEndian(hash) / (double)ulong.MaxValue;
    }
}
