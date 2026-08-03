namespace Forge.RpgKit.Domain.Creatures;

public static class CreatureCombatService
{
    public static (IReadOnlyDictionary<string, double> Resources, DamageApplied Applied, EntityDied? Died) ApplyDamage(
        CreatureDefinition definition,
        IReadOnlyDictionary<string, double> resources,
        DamageRequest request,
        IReadOnlyList<Func<DamageRequest, double, double>> beforeDamage,
        IReadOnlyList<string> contributors,
        IReadOnlyList<string> lootIds,
        string deathPolicy,
        Func<Guid> eventIdSource)
    {
        CreatureFactory.Validate(definition);
        if (request.TransactionId == Guid.Empty || request.TargetId == Guid.Empty || request.Amount < 0 || string.IsNullOrWhiteSpace(request.DamageType))
            throw new InvalidOperationException("RPG_CREATURE_DAMAGE_REQUEST_INVALID");
        var amount = beforeDamage.Aggregate(request.Amount, (current, hook) => Math.Clamp(hook(request, current), 0, request.Amount * 4));
        var health = resources.TryGetValue("health", out var value) ? value : throw new InvalidOperationException("RPG_CREATURE_HEALTH_MISSING");
        var remaining = Math.Max(0, health - amount);
        var updated = resources.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        updated["health"] = remaining;
        var applied = new DamageApplied(eventIdSource(), request.TransactionId, request.TargetId, request.SourceId, request.Amount, health - remaining, remaining, request.DamageType, request.Cause);
        EntityDied? died = remaining > 0 ? null : new EntityDied(eventIdSource(), request.TargetId, request.SourceId, contributors.Distinct(StringComparer.Ordinal).ToArray(), request.Cause, definition.DefaultTransform, lootIds, deathPolicy);
        return (updated, applied, died);
    }
}
