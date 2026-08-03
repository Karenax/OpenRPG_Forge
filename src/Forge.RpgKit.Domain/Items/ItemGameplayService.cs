namespace Forge.RpgKit.Domain.Items;

public enum TransferDestination { Drop, Trade, Store, Container, Mail, Destroy, Consume, GameMaster }
public sealed record TransferPolicy(IReadOnlyDictionary<TransferDestination, bool> AllowedDestinations, IReadOnlySet<string> RequiredPermissions);
public sealed record ItemModifierDefinition(Guid Id, int SchemaVersion, string Provenance, string Kind, IReadOnlyDictionary<string, double> Parameters, string StackingRule, string Condition, string UiSummary, string? BeforeHook, string? AfterHook);
public sealed record ItemAbilityBinding(Guid AbilityDefinitionId, string Targeting, IReadOnlyDictionary<string, double> Costs, int ChargeCost, TimeSpan Cooldown, string RequestedEvent, string ResolvedEvent);
public sealed record EquipmentSlotDefinition(string Id, string Ruleset, string Rig, int Layer, IReadOnlySet<string> Conflicts);
public sealed record EquipmentRequest(Guid ItemInstanceId, IReadOnlySet<string> SlotIds, string Ruleset, string Rig);
public sealed record EquipmentPreview(bool Allowed, IReadOnlyList<string> Conflicts, IReadOnlyList<string> DisplacedSlots);
public sealed record LootEntry(Guid ItemDefinitionId, string Group, double Weight, int MinimumQuantity, int MaximumQuantity, int MinimumQuality, string Condition, bool Unique);
public sealed record LootTable(Guid Id, int SchemaVersion, string Provenance, IReadOnlyList<LootEntry> Entries);
public sealed record LootResult(Guid ItemDefinitionId, int Quantity, int Quality, string Group);
public sealed record LootPreview(Guid ItemDefinitionId, double Probability, double ExpectedQuantity);
public sealed record ItemAbilityEvent(string Name, Guid ItemInstanceId, Guid AbilityDefinitionId, string TargetId, IReadOnlyDictionary<string, double> Costs);

public static class ItemGameplayService
{
    public static ItemActionDecision AuthorizeTransfer(TransferPolicy policy, TransferDestination destination, IReadOnlySet<string> permissions, string actorId)
    {
        if (!policy.AllowedDestinations.GetValueOrDefault(destination)) return new(false, "RPG_ITEM_TRANSFER_BLOCKED", $"blocked:{destination}:{actorId}");
        if (!policy.RequiredPermissions.All(permissions.Contains)) return new(false, "RPG_ITEM_TRANSFER_PERMISSION_MISSING", $"permission-missing:{destination}:{actorId}");
        return new(true, "RPG_ITEM_TRANSFER_ALLOWED", $"allowed:{destination}:{actorId}");
    }

    public static IReadOnlyDictionary<string, double> ResolveModifiers(IEnumerable<ItemModifierDefinition> modifiers, IReadOnlySet<string> activeConditions)
    {
        var applicable = modifiers.Where(item => string.IsNullOrWhiteSpace(item.Condition) || activeConditions.Contains(item.Condition));
        return applicable.GroupBy(item => item.Kind, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().StackingRule switch
        {
            "highest" => group.Max(item => item.Parameters.GetValueOrDefault("value")),
            "lowest" => group.Min(item => item.Parameters.GetValueOrDefault("value")),
            _ => group.Sum(item => item.Parameters.GetValueOrDefault("value"))
        }, StringComparer.Ordinal);
    }

    public static (ItemAbilityEvent Requested, ItemAbilityEvent Resolved, UsageResource Usage) ActivateAbility(Guid itemInstanceId, ItemAbilityBinding ability, string targetId, UsageResource usage, DateTimeOffset now)
    {
        if (ability.AbilityDefinitionId == Guid.Empty || string.IsNullOrWhiteSpace(targetId) || ability.ChargeCost < 1) throw new InvalidOperationException("RPG_ITEM_ABILITY_INVALID");
        var updated = usage;
        for (var i = 0; i < ability.ChargeCost; i++) updated = ItemInventoryService.Consume(updated with { CooldownUntil = i == 0 ? updated.CooldownUntil : null }, now);
        updated = updated with { CooldownUntil = now + ability.Cooldown };
        return (new(ability.RequestedEvent, itemInstanceId, ability.AbilityDefinitionId, targetId, ability.Costs), new(ability.ResolvedEvent, itemInstanceId, ability.AbilityDefinitionId, targetId, ability.Costs), updated);
    }

    public static EquipmentPreview PreviewEquipment(EquipmentRequest request, IReadOnlyDictionary<string, EquipmentSlotDefinition> slots, IReadOnlySet<string> occupiedSlots)
    {
        var conflicts = new List<string>();
        foreach (var slotId in request.SlotIds)
        {
            if (!slots.TryGetValue(slotId, out var slot)) { conflicts.Add($"missing:{slotId}"); continue; }
            if (slot.Ruleset != request.Ruleset || slot.Rig != request.Rig) conflicts.Add($"incompatible:{slotId}");
            conflicts.AddRange(slot.Conflicts.Where(occupiedSlots.Contains).Select(item => $"conflict:{slotId}:{item}"));
        }
        var displaced = request.SlotIds.Where(occupiedSlots.Contains).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        return new(conflicts.Count == 0, conflicts.OrderBy(item => item, StringComparer.Ordinal).ToArray(), displaced);
    }

    public static IReadOnlyList<LootResult> Roll(LootTable table, int seed, IReadOnlySet<string> conditions, int qualityBonus = 0)
    {
        var random = new Random(seed); var results = new List<LootResult>(); var unique = new HashSet<Guid>();
        foreach (var group in table.Entries.Where(item => string.IsNullOrWhiteSpace(item.Condition) || conditions.Contains(item.Condition)).GroupBy(item => item.Group, StringComparer.Ordinal).OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var candidates = group.Where(item => !item.Unique || !unique.Contains(item.ItemDefinitionId)).ToArray(); var total = candidates.Sum(item => item.Weight); if (total <= 0) continue;
            var roll = random.NextDouble() * total; LootEntry? selected = null;
            foreach (var candidate in candidates) { roll -= candidate.Weight; if (roll <= 0) { selected = candidate; break; } }
            selected ??= candidates[^1]; if (selected.Unique) unique.Add(selected.ItemDefinitionId);
            results.Add(new(selected.ItemDefinitionId, random.Next(selected.MinimumQuantity, selected.MaximumQuantity + 1), Math.Max(0, selected.MinimumQuality + qualityBonus), selected.Group));
        }
        return results;
    }

    public static IReadOnlyList<LootPreview> Preview(LootTable table, IReadOnlySet<string> conditions)
        => table.Entries.Where(item => string.IsNullOrWhiteSpace(item.Condition) || conditions.Contains(item.Condition)).GroupBy(item => item.Group, StringComparer.Ordinal).SelectMany(group =>
        {
            var total = group.Sum(item => item.Weight);
            return group.Select(item => new LootPreview(item.ItemDefinitionId, total <= 0 ? 0 : item.Weight / total, (item.MinimumQuantity + item.MaximumQuantity) / 2d));
        }).OrderBy(item => item.ItemDefinitionId).ToArray();
}
