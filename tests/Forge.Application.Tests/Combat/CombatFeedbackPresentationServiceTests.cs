using Forge.Application.Combat;
using Forge.Domain.Combat;

namespace Forge.Application.Tests.Combat;

public sealed class CombatFeedbackPresentationServiceTests
{
    private static readonly AbilityId AbilityId = Forge.Domain.Combat.AbilityId.From(
        Guid.Parse("71000000-0000-4000-8000-000000000001"));

    [Fact]
    public void Create_PreservesExactPostCastStateAndSemanticOrder()
    {
        var ability = new AbilityDefinition(
            AbilityId,
            "verdant-exchange",
            "Échange verdoyant",
            2,
            1.5,
            AbilityTargeting.SingleEnemy,
            [
                new AbilityEffect(AbilityEffectKind.Damage, 5),
                new AbilityEffect(AbilityEffectKind.Heal, 2),
                new AbilityEffect(AbilityEffectKind.RestoreResource, 3),
            ],
            new AbilityVisualCue("cue.verdant-exchange"));
        var source = Combatant("hero", "heroes", 20, 8, 4, 1, [ability]);
        var target = Combatant("warden", "hostiles", 18, 6, 2, 2);
        target.Restore(12, 1);
        var result = CombatResolver.Cast(source, target, ability.Id, 10);
        var transactionId = Guid.Parse("71000000-0000-4000-8000-000000000002");

        var presentation = CombatFeedbackPresentationService.Create(
            transactionId, source, target, result, 10.25);

        Assert.Equal(transactionId, presentation.TransactionId);
        Assert.Equal("hero", presentation.SourceCombatantId);
        Assert.Equal("warden", presentation.TargetCombatantId);
        Assert.Equal(ability.Id, presentation.AbilityId);
        Assert.Equal("verdant-exchange", presentation.AbilitySlug);
        Assert.Equal("Échange verdoyant", presentation.AbilityDisplayName);
        Assert.Equal("cue.verdant-exchange", presentation.VisualCueKey);
        Assert.Collection(presentation.Amounts,
            amount => Assert.Equal(
                new CombatFeedbackAmount(CombatFeedbackAmountKind.Damage, result.Damage), amount),
            amount => Assert.Equal(
                new CombatFeedbackAmount(CombatFeedbackAmountKind.Healing, result.Healing), amount),
            amount => Assert.Equal(
                new CombatFeedbackAmount(CombatFeedbackAmountKind.ResourceRestored, result.ResourceRestored), amount));
        Assert.Equal(target.Health, presentation.TargetHealth);
        Assert.Equal(18, presentation.TargetMaximumHealth);
        Assert.Equal(source.Resource, presentation.SourceResource);
        Assert.Equal(8, presentation.SourceMaximumResource);
        Assert.Equal(1.25, presentation.CooldownRemainingSeconds, 6);
        Assert.Equal(1.5, presentation.CooldownDurationSeconds, 6);
        Assert.False(presentation.TargetDied);
    }

    [Fact]
    public void Create_OmitsZeroAmountsAndPreservesDeath()
    {
        var ability = DamageAbility(damage: 50, cooldown: 0);
        var source = Combatant("hero", "heroes", 20, 8, 4, 1, [ability]);
        var target = Combatant("warden", "hostiles", 4, 0, 1, 0);
        var result = CombatResolver.Cast(source, target, ability.Id, 0);

        var presentation = CombatFeedbackPresentationService.Create(
            Guid.Parse("71000000-0000-4000-8000-000000000003"), source, target, result, 0);

        var amount = Assert.Single(presentation.Amounts);
        Assert.Equal(CombatFeedbackAmountKind.Damage, amount.Kind);
        Assert.Equal(result.Damage, amount.Value);
        Assert.Equal(0, presentation.TargetHealth);
        Assert.Equal(0, presentation.CooldownRemainingSeconds);
        Assert.True(presentation.TargetDied);
    }

    [Fact]
    public void Create_RejectsInvalidInputsAndTime()
    {
        var ability = DamageAbility(2, 1);
        var source = Combatant("hero", "heroes", 20, 8, 4, 1, [ability]);
        var target = Combatant("warden", "hostiles", 20, 0, 1, 1);
        var result = CombatResolver.Cast(source, target, ability.Id, 0);

        Assert.Throws<ArgumentException>(() =>
            CombatFeedbackPresentationService.Create(Guid.Empty, source, target, result, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CombatFeedbackPresentationService.Create(Guid.NewGuid(), source, target, result, double.PositiveInfinity));
        Assert.Throws<ArgumentNullException>(() =>
            CombatFeedbackPresentationService.Create(Guid.NewGuid(), null!, target, result, 0));
    }

    private static AbilityDefinition DamageAbility(int damage, double cooldown) => new(
        AbilityId,
        "test-strike",
        "Frappe de la clairière",
        0,
        cooldown,
        AbilityTargeting.SingleEnemy,
        [new AbilityEffect(AbilityEffectKind.Damage, damage)],
        new AbilityVisualCue("cue.test-strike"));

    private static CombatantState Combatant(
        string id,
        string team,
        int health,
        int resource,
        int offense,
        int defense,
        IReadOnlyList<AbilityDefinition>? abilities = null) =>
        new(id, new CombatProfile(new CombatStats(health, resource, offense, defense, team, team), abilities));
}
