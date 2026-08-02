using Forge.Domain.Combat;

namespace Forge.Domain.Tests.Combat;

public sealed class CombatResolverTests
{
    private static readonly AbilityId StrikeId = AbilityId.From(
        Guid.Parse("91000000-0000-4000-8000-000000000001"));

    [Fact]
    public void CombatStats_ExposeHealthResourceOffenseDefenseFactionAndTeam()
    {
        var stats = new CombatStats(12, 5, 3, 2, "crypt-guardians", "hostile");

        Assert.Equal(12, stats.MaximumHealth);
        Assert.Equal(5, stats.MaximumResource);
        Assert.Equal(3, stats.Offense);
        Assert.Equal(2, stats.Defense);
        Assert.Equal("crypt-guardians", stats.Faction);
        Assert.Equal("hostile", stats.Team);
    }

    [Theory]
    [InlineData(0, 1, 1, 1)]
    [InlineData(1, -1, 1, 1)]
    [InlineData(1, 1, -1, 1)]
    [InlineData(1, 1, 1, -1)]
    public void CombatStats_RejectInvalidValues(int health, int resource, int offense, int defense)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CombatStats(health, resource, offense, defense, "faction", "team"));
    }

    [Fact]
    public void Ability_SeparatesCostCooldownTargetingEffectsAndVisualCue()
    {
        var ability = CreateStrike();

        Assert.Equal(2, ability.ResourceCost);
        Assert.Equal(1.5, ability.CooldownSeconds);
        Assert.Equal(AbilityTargeting.SingleEnemy, ability.Targeting);
        Assert.Equal(AbilityEffectKind.Damage, Assert.Single(ability.Effects).Kind);
        Assert.Equal("cue.ember-strike", ability.VisualCue.CueKey);
    }

    [Fact]
    public void Cast_AppliesDamageCostCooldownDeathAndLootDeterministically()
    {
        var caster = CreateCombatant("hero", "heroes", 10, 5, 4, 1, [CreateStrike()]);
        var target = CreateCombatant(
            "warden",
            "hostile",
            6,
            0,
            1,
            2,
            loot: [new LootDrop("warden-ember", 1)]);

        var first = CombatResolver.Cast(caster, target, StrikeId, 0);

        Assert.Equal(4, first.Damage);
        Assert.Equal(3, caster.Resource);
        Assert.False(first.TargetDied);
        Assert.Empty(first.Loot);
        Assert.Throws<InvalidOperationException>(() => CombatResolver.Cast(caster, target, StrikeId, 1));

        var second = CombatResolver.Cast(caster, target, StrikeId, 1.5);

        Assert.True(second.TargetDied);
        Assert.Equal(0, target.Health);
        Assert.Equal("warden-ember", Assert.Single(second.Loot).ItemSlug);
        Assert.Equal("cue.ember-strike", second.VisualCue.CueKey);
    }

    [Fact]
    public void Cast_RejectsAlliedTargetForEnemyAbility()
    {
        var caster = CreateCombatant("hero", "heroes", 10, 5, 4, 1, [CreateStrike()]);
        var ally = CreateCombatant("ally", "heroes", 8, 3, 2, 1);

        Assert.Throws<InvalidOperationException>(() => CombatResolver.Cast(caster, ally, StrikeId, 0));
    }

    [Fact]
    public void CombatProfile_RejectsDuplicateAbilities()
    {
        var ability = CreateStrike();

        Assert.Throws<ArgumentException>(() =>
            new CombatProfile(new CombatStats(5, 5, 1, 1, "heroes", "heroes"), [ability, ability]));
    }

    [Fact]
    public void CombatantState_RestoresHealthAndResourceWithinProfileLimits()
    {
        var combatant = CreateCombatant("hero", "heroes", 10, 5, 2, 1);

        combatant.Restore(4, 3);

        Assert.Equal(4, combatant.Health);
        Assert.Equal(3, combatant.Resource);
        Assert.Throws<ArgumentOutOfRangeException>(() => combatant.Restore(11, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => combatant.Restore(4, 6));
    }

    private static AbilityDefinition CreateStrike() => new(
        StrikeId,
        "ember-strike",
        "Frappe de braise",
        2,
        1.5,
        AbilityTargeting.SingleEnemy,
        [new AbilityEffect(AbilityEffectKind.Damage, 2)],
        new AbilityVisualCue("cue.ember-strike"));

    private static CombatantState CreateCombatant(
        string id,
        string team,
        int health,
        int resource,
        int offense,
        int defense,
        IReadOnlyList<AbilityDefinition>? abilities = null,
        IReadOnlyList<LootDrop>? loot = null) =>
        new(
            id,
            new CombatProfile(
                new CombatStats(health, resource, offense, defense, team, team),
                abilities,
                loot));
}
