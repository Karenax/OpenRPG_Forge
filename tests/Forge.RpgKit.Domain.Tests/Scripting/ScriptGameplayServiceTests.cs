using Forge.RpgKit.Domain.Scripting;

namespace Forge.RpgKit.Domain.Tests.Scripting;

public sealed class ScriptGameplayServiceTests
{
    [Fact]
    public void Authoritative_combat_is_seeded_attributed_armored_effect_aware_and_transactional()
    {
        var original = new ScriptCombatState(
            new Dictionary<string, double> { ["target"] = 30 }, [], 0,
            new Dictionary<string, double> { ["target"] = 4 });
        var context = new AttackContext(Guid.NewGuid(), "attacker", "target", "ability:strike",
            1, 1, 42, "server", "script:test");
        var damage = new DamageSpec(8, "radiant", new HashSet<string> { "stagger" }, true, 0.5);

        var result = ScriptGameplayService.Attack(original, context, damage);
        var replay = ScriptGameplayService.Attack(original, context, damage);
        var rejected = ScriptGameplayService.Attack(original, context with { Authority = "client" }, damage);

        Assert.True(result.Committed);
        Assert.True(result.Critical);
        Assert.Equal(14, result.AppliedDamage);
        Assert.Equal(16, result.State.Health["target"]);
        Assert.Equal(result.AppliedDamage, replay.AppliedDamage);
        Assert.Equal(result.Critical, replay.Critical);
        Assert.Equal(result.State.Health["target"], replay.State.Health["target"]);
        Assert.Equal(result.Events, replay.Events);
        Assert.Equal(result.SemanticDiff, replay.SemanticDiff);
        Assert.Contains("attribution:attacker", result.Events);
        Assert.Contains("ability:ability:strike", result.Events);
        Assert.False(rejected.Committed);
        Assert.Same(original, rejected.State);
    }

    [Fact]
    public void Effects_support_six_stacking_modes_duration_period_source_dispel_and_serialization()
    {
        Assert.Equal(6, Enum.GetValues<EffectStacking>().Length);
        var at = DateTimeOffset.UnixEpoch;
        var definition = Definition(EffectStacking.StackMagnitude);
        var firstSpec = Spec(definition, at);

        var first = ScriptGameplayService.ApplyEffect([], definition, firstSpec);
        var second = ScriptGameplayService.ApplyEffect(first.Effects, definition,
            firstSpec with { InstanceId = Guid.NewGuid() });
        var advanced = ScriptGameplayService.AdvanceEffects(second.Effects, at.AddSeconds(2));
        var serialized = ScriptGameplayService.SerializeEffects(advanced.Effects, at.AddSeconds(2));
        var restored = ScriptGameplayService.DeserializeEffects(serialized);
        var dispelled = ScriptGameplayService.Dispel(restored.Effects, "target", new HashSet<string> { "fire" });

        Assert.Single(first.Effects);
        Assert.Equal(at.AddSeconds(2), first.Effects[0].NextTickAt);
        Assert.True(first.Effects[0].Definition.Serializable);
        Assert.Equal(2, second.Effects.Single().Stacks);
        Assert.Contains(advanced.Events, item => item.StartsWith("effect-tick:", StringComparison.Ordinal));
        Assert.Equal(advanced.Effects.Select(item => item.Spec.InstanceId),
            restored.Effects.Select(item => item.Spec.InstanceId));
        Assert.Equal(advanced.Effects.Select(item => item.NextTickAt),
            restored.Effects.Select(item => item.NextTickAt));
        Assert.Empty(dispelled.Effects);
        Assert.Single(dispelled.Events);
    }

    [Theory]
    [InlineData(EffectStacking.Replace)]
    [InlineData(EffectStacking.RefreshDuration)]
    [InlineData(EffectStacking.StackMagnitude)]
    [InlineData(EffectStacking.StackDuration)]
    [InlineData(EffectStacking.Independent)]
    [InlineData(EffectStacking.HighestWins)]
    public void Every_effect_stacking_policy_is_deterministic(EffectStacking stacking)
    {
        var at = DateTimeOffset.UnixEpoch;
        var definition = Definition(stacking);
        var first = ScriptGameplayService.ApplyEffect([], definition, Spec(definition, at));
        var second = ScriptGameplayService.ApplyEffect(first.Effects, definition,
            Spec(definition, at) with { MagnitudeMultiplier = 2 });

        Assert.NotEmpty(second.Effects);
        Assert.Equal(second.Effects.OrderBy(item => item.Spec.InstanceId), second.Effects);
    }

    [Fact]
    public void Unknown_or_non_serializable_effect_state_is_rejected_without_partial_state()
    {
        var definition = Definition(EffectStacking.Independent) with { Serializable = false };
        var effect = ScriptGameplayService.ApplyEffect([], definition,
            Spec(definition, DateTimeOffset.UnixEpoch));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ScriptGameplayService.SerializeEffects(effect.Effects, DateTimeOffset.UnixEpoch));

        Assert.Equal("RPG_SCRIPT_EFFECT_NOT_SERIALIZABLE", exception.Message);
        Assert.Single(effect.Effects);
    }

    private static EffectDefinition Definition(EffectStacking stacking) =>
        new("effect:burn", 1, "author:test", "damage-over-time", stacking,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2), 3,
            new HashSet<string> { "fire" }, true);

    private static EffectSpec Spec(EffectDefinition definition, DateTimeOffset at) =>
        new(Guid.NewGuid(), definition.Id, "caster", "target", 1, null, at);
}
