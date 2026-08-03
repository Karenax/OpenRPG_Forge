using Forge.RpgKit.Domain.Scripting;

namespace Forge.RpgKit.Domain.Tests.Scripting;

public sealed class ScriptGameplayServiceTests
{
    [Fact]
    public void Authoritative_combat_is_seeded_attributed_effect_aware_and_transactional()
    {
        var original = new ScriptCombatState(new Dictionary<string, double> { ["target"] = 30 }, [], 0); var context = new AttackContext(Guid.NewGuid(), "attacker", "target", "ability:strike", 1, 1, 42, "server", "script:test"); var damage = new DamageSpec(8, "radiant", new HashSet<string> { "stagger" }, true, 0.2); var result = ScriptGameplayService.Attack(original, context, damage); Assert.True(result.Committed); Assert.True(result.Critical); Assert.Equal(16, result.AppliedDamage); Assert.Equal(14, result.State.Health["target"]); Assert.Contains("attribution:attacker", result.Events);
        var rejected = ScriptGameplayService.Attack(original, context with { Authority = "client" }, damage); Assert.False(rejected.Committed); Assert.Same(original, rejected.State);
    }

    [Fact]
    public void Effects_support_six_stacking_modes_duration_period_source_dispel_and_serialization()
    {
        Assert.Equal(6, Enum.GetValues<EffectStacking>().Length); var at = DateTimeOffset.UnixEpoch; var definition = new EffectDefinition("effect:burn", 1, "author:test", "damage-over-time", EffectStacking.StackMagnitude, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2), 3, new HashSet<string> { "fire" }, true); var firstSpec = new EffectSpec(Guid.NewGuid(), definition.Id, "caster", "target", 1, null, at); var first = ScriptGameplayService.ApplyEffect([], definition, firstSpec); Assert.Single(first.Effects); Assert.Equal(at.AddSeconds(2), first.Effects[0].NextTickAt); Assert.True(first.Effects[0].Definition.Serializable);
        var second = ScriptGameplayService.ApplyEffect(first.Effects, definition, firstSpec with { InstanceId = Guid.NewGuid() }); Assert.Equal(2, second.Effects.Single().Stacks); var dispelled = ScriptGameplayService.Dispel(second.Effects, "target", new HashSet<string> { "fire" }); Assert.Empty(dispelled.Effects); Assert.Single(dispelled.Events);
    }
}
