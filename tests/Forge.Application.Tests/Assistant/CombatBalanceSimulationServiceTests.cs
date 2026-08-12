using Forge.Domain.Assistant;
using Forge.Domain.Blueprints;
using Forge.Domain.Combat;
using Forge.RpgKit.Application.Combat;

namespace Forge.Application.Tests.Assistant;

public sealed class CombatBalanceSimulationServiceTests
{
    [Fact]
    public void ComparesArchetypesAcrossSeedsWithDurationResourcesPathingAndPhases()
    {
        var firstId = BlueprintId.From(Guid.Parse("a1430000-0000-4000-8000-000000000010"));
        var secondId = BlueprintId.From(Guid.Parse("a1430000-0000-4000-8000-000000000011"));
        var profile = CombatSimulationProfile.CreateDefault() with
        {
            Enabled = true,
            RunsPerMatchup = 24,
            ArchetypeBlueprintIds = [firstId, secondId],
        };
        var archetypes = new[]
        {
            Archetype(firstId, "Guardian", 140, 8, 12, 3),
            Archetype(secondId, "Raider", 100, 12, 16, 1),
        };
        var paths = new Dictionary<string, float>
        {
            [CombatBalanceSimulationService.PairKey(firstId, secondId)] = 20,
        };

        var first = CombatBalanceSimulationService.Run(profile, archetypes, paths);
        var second = CombatBalanceSimulationService.Run(profile, archetypes, paths);
        var matchup = Assert.Single(first.Matchups);

        Assert.True(first.Succeeded);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal(first.Matchups.Single().FirstWins, second.Matchups.Single().FirstWins);
        Assert.Equal(first.Matchups.Single().AverageDurationSeconds, second.Matchups.Single().AverageDurationSeconds);
        Assert.Equal(24, matchup.FirstWins + matchup.SecondWins + matchup.Draws);
        Assert.InRange(matchup.FirstWinRate, 0, 1);
        Assert.True(matchup.AverageDurationSeconds >= 4);
        Assert.True(matchup.AverageFirstResourceUsed > 0);
        Assert.True(matchup.AverageSecondResourceUsed > 0);
        Assert.All(matchup.FirstPhaseTriggers.Values, value => Assert.InRange(value, 0, 24));
        Assert.Equal(64, first.Fingerprint.Length);
    }

    [Fact]
    public void RejectsDisabledMissingAndNonCombatArchetypesWithStableDiagnostics()
    {
        var id = BlueprintId.From(Guid.Parse("a1430000-0000-4000-8000-000000000012"));
        var disabled = CombatBalanceSimulationService.Run(CombatSimulationProfile.CreateDefault(), [], null);
        var missing = CombatBalanceSimulationService.Run(CombatSimulationProfile.CreateDefault() with
        {
            Enabled = true,
            ArchetypeBlueprintIds = [id],
        }, [], null);

        Assert.Contains(disabled.Diagnostics, item => item.Code == CombatBalanceSimulationService.DisabledCode);
        Assert.Contains(missing.Diagnostics, item => item.Code == CombatBalanceSimulationService.ArchetypeMissingCode);
    }

    private static CombatSimulationArchetype Archetype(BlueprintId id, string name, int health,
        int resource, int offense, int defense)
    {
        var ability = new AbilityDefinition(AbilityId.From(Guid.NewGuid()), "strike", "Strike", 1, 0,
            AbilityTargeting.SingleEnemy, [new(AbilityEffectKind.Damage, 8)],
            new AbilityVisualCue("combat.strike"));
        return new(id, name, new CombatProfile(new(health, resource, offense, defense, name, name), [ability]));
    }
}
