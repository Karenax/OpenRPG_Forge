namespace Forge.RpgKit.Domain.Scripting;

public enum EffectStacking { Replace, RefreshDuration, StackMagnitude, StackDuration, Independent, HighestWins }
public sealed record DamageSpec(double Amount, string DamageType, IReadOnlySet<string> Tags, bool CriticalAllowed, double ArmorPenetration);
public sealed record AttackContext(Guid TransactionId, string AttackerId, string TargetId, string AbilityId, double Accuracy, double CriticalChance, int Seed, string Authority, string Cause);
public sealed record ScriptCombatState(IReadOnlyDictionary<string, double> Health, IReadOnlyList<string> EffectIds, long Revision);
public sealed record ScriptCombatResult(bool Committed, ScriptCombatState State, double AppliedDamage, bool Critical, IReadOnlyList<string> Events, string SemanticDiff);
public sealed record EffectDefinition(string Id, int SchemaVersion, string Provenance, string Kind, EffectStacking Stacking, TimeSpan Duration, TimeSpan Period, double Magnitude, IReadOnlySet<string> DispelTags, bool Serializable);
public sealed record EffectSpec(Guid InstanceId, string DefinitionId, string SourceId, string TargetId, double MagnitudeMultiplier, TimeSpan? DurationOverride, DateTimeOffset AppliedAt);
public sealed record ActiveEffect(EffectSpec Spec, EffectDefinition Definition, int Stacks, DateTimeOffset ExpiresAt, DateTimeOffset NextTickAt);
public sealed record EffectApplyResult(IReadOnlyList<ActiveEffect> Effects, IReadOnlyList<string> Events, string SemanticDiff);

public static class ScriptGameplayService
{
    public static ScriptCombatResult Attack(ScriptCombatState original, AttackContext context, DamageSpec damage)
    {
        if (context.TransactionId == Guid.Empty || string.IsNullOrWhiteSpace(context.AttackerId) || string.IsNullOrWhiteSpace(context.TargetId) || context.Authority != "server" || damage.Amount < 0 || context.Accuracy is < 0 or > 1 || context.CriticalChance is < 0 or > 1) return new(false, original, 0, false, ["combat-rejected"], "no-change");
        var random = new Random(context.Seed); if (random.NextDouble() > context.Accuracy) return new(true, original with { Revision = checked(original.Revision + 1) }, 0, false, ["attack-missed"], $"attack:{context.TransactionId}|result:miss"); var critical = damage.CriticalAllowed && random.NextDouble() <= context.CriticalChance; var requested = damage.Amount * (critical ? 2 : 1); var health = original.Health.GetValueOrDefault(context.TargetId); var applied = Math.Min(health, requested); var updated = original.Health.ToDictionary(); updated[context.TargetId] = health - applied; return new(true, original with { Health = updated, Revision = checked(original.Revision + 1) }, applied, critical, ["attack-hit", $"damage:{damage.DamageType}", $"attribution:{context.AttackerId}", .. damage.Tags.Select(tag => $"effect-tag:{tag}")], $"attack:{context.TransactionId}|damage:{applied:0.##}|critical:{critical}|cause:{context.Cause}");
    }

    public static EffectApplyResult ApplyEffect(IReadOnlyList<ActiveEffect> current, EffectDefinition definition, EffectSpec spec)
    {
        if (spec.InstanceId == Guid.Empty || definition.SchemaVersion != 1 || string.IsNullOrWhiteSpace(definition.Provenance) || definition.Duration <= TimeSpan.Zero || definition.Period < TimeSpan.Zero || !double.IsFinite(definition.Magnitude) || spec.DefinitionId != definition.Id) throw new InvalidOperationException("RPG_SCRIPT_EFFECT_INVALID"); var effects = current.ToList(); var matches = effects.Where(item => item.Definition.Id == definition.Id && item.Spec.TargetId == spec.TargetId).ToArray(); var expires = spec.AppliedAt + (spec.DurationOverride ?? definition.Duration); var created = new ActiveEffect(spec, definition, 1, expires, spec.AppliedAt + definition.Period);
        switch (definition.Stacking)
        {
            case EffectStacking.Replace: effects.RemoveAll(item => matches.Contains(item)); effects.Add(created); break;
            case EffectStacking.RefreshDuration when matches.Length > 0: Replace(effects, matches[0], matches[0] with { ExpiresAt = expires }); break;
            case EffectStacking.StackMagnitude when matches.Length > 0: Replace(effects, matches[0], matches[0] with { Stacks = matches[0].Stacks + 1 }); break;
            case EffectStacking.StackDuration when matches.Length > 0: Replace(effects, matches[0], matches[0] with { ExpiresAt = matches[0].ExpiresAt + definition.Duration }); break;
            case EffectStacking.HighestWins when matches.Length > 0 && matches[0].Spec.MagnitudeMultiplier >= spec.MagnitudeMultiplier: break;
            case EffectStacking.HighestWins when matches.Length > 0: Replace(effects, matches[0], created); break;
            default: effects.Add(created); break;
        }
        return new(effects, ["effect-applied", $"stacking:{definition.Stacking}", $"source:{spec.SourceId}"], $"apply-effect:{spec.InstanceId}|definition:{definition.Id}|target:{spec.TargetId}");
    }
    public static EffectApplyResult Dispel(IReadOnlyList<ActiveEffect> current, string targetId, IReadOnlySet<string> tags) { var removed = current.Where(item => item.Spec.TargetId == targetId && item.Definition.DispelTags.Overlaps(tags)).ToArray(); return new(current.Except(removed).ToArray(), removed.Select(item => $"effect-dispelled:{item.Spec.InstanceId}").ToArray(), $"dispel:{targetId}|removed:{removed.Length}"); }
    private static void Replace(List<ActiveEffect> effects, ActiveEffect before, ActiveEffect after) { effects[effects.IndexOf(before)] = after; }
}
