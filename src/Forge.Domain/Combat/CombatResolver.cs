namespace Forge.Domain.Combat;

public static class CombatResolver
{
    public static CombatActionResult Cast(
        CombatantState caster,
        CombatantState target,
        AbilityId abilityId,
        double elapsedSeconds,
        double lootRoll = 0)
    {
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(abilityId);
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        if (!double.IsFinite(lootRoll) || lootRoll is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lootRoll));
        }

        if (caster.IsDead)
        {
            throw new InvalidOperationException("A defeated combatant cannot cast an ability.");
        }

        var ability = caster.Profile.Abilities.SingleOrDefault(candidate => candidate.Id == abilityId)
            ?? throw new InvalidOperationException($"Combatant '{caster.Id}' does not know ability '{abilityId}'.");
        ValidateTarget(caster, target, ability.Targeting);
        if (caster.Resource < ability.ResourceCost)
        {
            throw new InvalidOperationException($"Combatant '{caster.Id}' lacks resource for '{ability.Slug}'.");
        }

        if (!caster.IsAbilityReady(ability, elapsedSeconds))
        {
            throw new InvalidOperationException($"Ability '{ability.Slug}' is on cooldown.");
        }

        caster.SpendResource(ability.ResourceCost);
        caster.StartCooldown(ability, elapsedSeconds);
        var damage = 0;
        var healing = 0;
        var restored = 0;
        foreach (var effect in ability.Effects)
        {
            switch (effect.Kind)
            {
                case AbilityEffectKind.Damage:
                    damage += target.Damage(Math.Max(1, caster.Profile.Stats.Offense + effect.Magnitude - target.Profile.Stats.Defense));
                    break;
                case AbilityEffectKind.Heal:
                    healing += target.Heal(effect.Magnitude + caster.Profile.Stats.Offense);
                    break;
                case AbilityEffectKind.RestoreResource:
                    restored += target.RestoreResource(effect.Magnitude);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effect), effect.Kind, "Unsupported ability effect.");
            }
        }

        var loot = target.IsDead
            ? target.Profile.Loot.Where(drop => lootRoll <= drop.Chance).ToArray()
            : [];
        return new CombatActionResult(ability, damage, healing, restored, target.IsDead, loot, ability.VisualCue);
    }

    private static void ValidateTarget(CombatantState caster, CombatantState target, AbilityTargeting targeting)
    {
        var allied = string.Equals(
            caster.Profile.Stats.Team,
            target.Profile.Stats.Team,
            StringComparison.Ordinal);
        var valid = targeting switch
        {
            AbilityTargeting.Self => ReferenceEquals(caster, target),
            AbilityTargeting.SingleAlly => allied,
            AbilityTargeting.SingleEnemy => !allied,
            AbilityTargeting.PointArea => true,
            _ => false,
        };
        if (!valid)
        {
            throw new InvalidOperationException($"Target '{target.Id}' is invalid for {targeting}.");
        }
    }
}
