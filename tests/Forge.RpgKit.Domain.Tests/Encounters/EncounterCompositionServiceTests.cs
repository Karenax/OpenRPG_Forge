using Forge.Domain.Spatial;
using Forge.RpgKit.Domain.Creatures;
using Forge.RpgKit.Domain.Encounters;
using Forge.RpgKit.Domain.Placeables;
using Forge.RpgKit.Domain.Triggers;

namespace Forge.RpgKit.Domain.Tests.Encounters;

public sealed class EncounterCompositionServiceTests
{
    [Fact]
    public void Data_driven_party_assessment_previews_bounded_seeded_composition_and_manual_anchors()
    {
        var encounter = Encounter(); var policy = Policy(encounter.Candidates[0].CreatureId);
        var first = EncounterCompositionService.Preview(encounter, policy, new(4, 12, new HashSet<string> { "veteran" }, "ruleset:forge-party"), 42);
        var repeated = EncounterCompositionService.Preview(encounter, policy, new(4, 12, new HashSet<string> { "veteran" }, "ruleset:forge-party"), 42);
        Assert.Equal(4, first.TotalCount);
        Assert.True(first.TotalCost <= 36);
        Assert.Equal(first.TotalCount, repeated.TotalCount);
        Assert.Equal(first.TotalCost, repeated.TotalCost);
        Assert.Equal(first.Candidates.Select(item => item.BlueprintId), repeated.Candidates.Select(item => item.BlueprintId));
        Assert.Equal(first.AnchorIds, repeated.AnchorIds);
        Assert.Equal(first.Summary, repeated.Summary);
        Assert.All(first.AnchorIds, id => Assert.Equal("anchor:north", id));
        Assert.Contains("seed:42", first.Summary);
    }

    [Fact]
    public void Activator_query_excludes_non_players_summons_companions_gm_spectators_and_wrong_authority()
    {
        var query = Policy(Guid.NewGuid()).Activators;
        var player = new Forge.RpgKit.Domain.Encounters.Activator("player", true, false, false, false, false, NetworkAuthority.Server, new HashSet<string> { "party" });
        Assert.True(EncounterCompositionService.CanActivate(player, query));
        Assert.False(EncounterCompositionService.CanActivate(player with { IsSummon = true }, query));
        Assert.False(EncounterCompositionService.CanActivate(player with { IsCompanion = true }, query));
        Assert.False(EncounterCompositionService.CanActivate(player with { IsGameMaster = true }, query));
        Assert.False(EncounterCompositionService.CanActivate(player with { IsSpectator = true }, query));
        Assert.False(EncounterCompositionService.CanActivate(player with { Authority = NetworkAuthority.Host }, query));
        Assert.False(EncounterCompositionService.CanActivate(player with { Tags = new HashSet<string>() }, query));
    }

    [Fact]
    public void Runtime_policy_supports_all_activation_modes_and_rejects_invalid_counts_timing_anchors_and_caps()
    {
        Assert.Equal(7, Enum.GetValues<EncounterActivationPolicy>().Length);
        var encounter = Encounter();
        var invalid = Policy(encounter.Candidates[0].CreatureId) with
        {
            Activation = EncounterActivationPolicy.Timed,
            Interval = TimeSpan.Zero,
            Groups = [new("wave:bad", new(8, 4), 0, [])],
            Anchors = [new("anchor:bad", TransformValue.Identity, 0, false, SpawnVisibilityPolicy.InViewAllowed, false, -1)]
        };
        var diagnostics = EncounterCompositionService.Validate(encounter, invalid);
        Assert.Contains(diagnostics, item => item.Code == "RPG_ENCOUNTER_COUNT_INVALID");
        Assert.Contains(diagnostics, item => item.Code == "RPG_ENCOUNTER_SPAWN_ANCHOR_INVALID");
        Assert.Contains(diagnostics, item => item.Code == "RPG_ENCOUNTER_INTERVAL_INVALID");
    }

    private static EncounterRuntimePolicy Policy(Guid candidateId) => new(
        EncounterActivationPolicy.PopulationMaintained,
        TimeSpan.FromSeconds(30),
        [new("wave:vigil", new(2, 4), 36, [candidateId])],
        [new("anchor:north", new(new(2, 0, 4), QuaternionValue.Identity, new(1, 1, 1)), 8, true, SpawnVisibilityPolicy.BehindCover, true, 10)],
        SpawnDistribution.Weighted,
        new(true, true, true, true, true, NetworkAuthority.Server, new HashSet<string> { "party" }));

    private static EncounterDefinition Encounter()
    {
        var candidate = new SpawnCandidate(Guid.NewGuid(), "creature:glass-warden", EncounterRole.Controller, 3, 8, new HashSet<string>(), ["ember"], 8);
        var shape = new TriggerShape(TriggerShapeKind.ExtrudedPolygon, [new(0, 0, 0), new(8, 0, 0), new(8, 0, 8)], [], new(0, 0, 0), 4, 0.5);
        return new(Guid.NewGuid(), 1, "author:test", "demo-encounter", "Encounter de démonstration", "ambush", "glass", "zone:observatory",
            new(shape, new(new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>()), true, 18, null), [candidate],
            new("ruleset:forge", EncounterDifficulty.Hard, 4, 12, 20, 1.2, 1.5), ["wave:vigil"], ["anchor:north"], [], 12);
    }
}
