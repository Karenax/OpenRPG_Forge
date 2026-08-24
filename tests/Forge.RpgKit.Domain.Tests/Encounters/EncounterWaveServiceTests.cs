using Forge.Domain.Spatial;
using Forge.RpgKit.Domain.Creatures;
using Forge.RpgKit.Domain.Encounters;
using Forge.RpgKit.Domain.Triggers;

namespace Forge.RpgKit.Domain.Tests.Encounters;

public sealed class EncounterWaveServiceTests
{
    [Fact]
    public void Lifecycle_emits_enter_exit_duration_and_exhausted_events()
    {
        var encounter = Fixture(); var lifecycle = new EncounterLifecycle(); var at = DateTimeOffset.UnixEpoch;
        Assert.Equal("player:one", lifecycle.Enter(encounter, "player:one", at).ActivatorId);
        Assert.Equal(TimeSpan.FromSeconds(9), lifecycle.Exit(encounter, "player:one", at.AddSeconds(9))!.DurationInside);
        Assert.Null(lifecycle.Exit(encounter, "missing", at));
        Assert.Equal(5, EncounterLifecycle.Exhaust(encounter, at, 5).SpawnedCount);
    }

    [Fact]
    public void Phases_and_waves_validate_order_timing_and_completion_targets()
    {
        var valid = new EncounterPhase("phase:one", 1, [new("wave:one", 1, [], TimeSpan.Zero, WaveCompletionPolicy.AllDefeated, null), new("wave:two", 2, [], TimeSpan.FromSeconds(2), WaveCompletionPolicy.Script, "script:advance")], new HashSet<string> { "alarm" }, ["encounter:reinforcements"]);
        Assert.Empty(EncounterWaveService.Validate(Fixture(), [valid]));
        var invalid = valid with { Waves = [new("wave:bad", 1, [], TimeSpan.FromSeconds(-1), WaveCompletionPolicy.Script, null)] };
        Assert.Contains(EncounterWaveService.Validate(Fixture(), [invalid]), item => item.Code == "RPG_ENCOUNTER_WAVE_INVALID");
    }

    [Fact]
    public void Statistical_preview_reports_runs_composition_cost_duration_rewards_variance_and_reproducible_seeds()
    {
        var encounter = Fixture(); var candidateId = encounter.Candidates[0].CreatureId;
        var policy = new EncounterRuntimePolicy(EncounterActivationPolicy.Wave, TimeSpan.Zero, [new("group", new(1, 3), 20, [candidateId])], [new("anchor", TransformValue.Identity, 4, true, SpawnVisibilityPolicy.Hidden, true, 1)], SpawnDistribution.Sequential, new(false, false, false, false, false, NetworkAuthority.Server, new HashSet<string>()));
        var party = new PartyAssessment(3, 8, new HashSet<string>(), "ruleset:test");
        var first = EncounterWaveService.Simulate(encounter, policy, party, 5, 100, c => c.TotalCount * 6, c => c.TotalCost * 0.5);
        var second = EncounterWaveService.Simulate(encounter, policy, party, 5, 100, c => c.TotalCount * 6, c => c.TotalCost * 0.5);
        Assert.Equal(5, first.Runs); Assert.Equal([100, 101, 102, 103, 104], first.Results.Select(item => item.Seed));
        Assert.Equal(first.MeanCost, second.MeanCost); Assert.Equal(first.CostVariance, second.CostVariance);
        Assert.True(first.MeanDurationSeconds > 0); Assert.True(first.MeanRewardValue > 0); Assert.NotEmpty(first.CompositionTotals);
    }

    private static EncounterDefinition Fixture()
    {
        var candidate = new SpawnCandidate(Guid.NewGuid(), "creature:shardling", EncounterRole.Minion, 1, 4, new HashSet<string>(), [], 5);
        var shape = new TriggerShape(TriggerShapeKind.Box, [], [], new(4, 3, 4), 0, 0);
        return new(Guid.NewGuid(), 1, "author:test", "demo-wave", "Vague de démonstration", "defense", "glass", "zone:vigil", new(shape, new(new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>()), false, 10, null), [candidate], new("ruleset:test", EncounterDifficulty.Standard, 3, 8, 20, 1, 1), ["group"], ["anchor"], ["reward"], 10);
    }
}
