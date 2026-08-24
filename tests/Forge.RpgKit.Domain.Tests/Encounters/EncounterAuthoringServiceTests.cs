using Forge.RpgKit.Domain.Creatures;
using Forge.RpgKit.Domain.Encounters;
using Forge.RpgKit.Domain.Placeables;
using Forge.RpgKit.Domain.Triggers;

namespace Forge.RpgKit.Domain.Tests.Encounters;

public sealed class EncounterAuthoringServiceTests
{
    [Fact]
    public void Wizard_previews_and_applies_a_deterministic_canonical_encounter()
    {
        var creatureId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var request = Request(creatureId);
        var context = Context(creatureId);
        var preview = EncounterAuthoringService.Preview(request, context);
        Assert.True(preview.CanApply);
        Assert.Empty(preview.Diagnostics);
        Assert.Contains("role:Controller", preview.SemanticDiff);
        Assert.Equal(36, preview.Encounter.DifficultyProfile.EffectiveBudget);
        Assert.Equal(preview.Encounter.Id, EncounterAuthoringService.Apply(request, context).Id);
    }

    [Fact]
    public void Validation_localizes_empty_pool_missing_references_bad_geometry_budget_and_cap()
    {
        var request = Request(Guid.NewGuid()) with
        {
            ZoneId = "missing-zone",
            Candidates = [],
            SpawnAnchorIds = ["missing-anchor"],
            RewardIds = ["missing-reward"],
            NetworkActorCap = 99,
            Volume = Request(Guid.NewGuid()).Volume with { ThreatRadius = -1, ActivationScript = "missing-script" },
            DifficultyProfile = Request(Guid.NewGuid()).DifficultyProfile with { BaseBudget = 9999 }
        };
        var preview = EncounterAuthoringService.Preview(request, Context(Guid.NewGuid()));
        Assert.False(preview.CanApply);
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_ENCOUNTER_POOL_EMPTY" && item.Path.EndsWith("/candidates", StringComparison.Ordinal));
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_ENCOUNTER_ZONE_MISSING");
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_ENCOUNTER_VOLUME_INVALID");
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_ENCOUNTER_SCRIPT_MISSING");
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_ENCOUNTER_ANCHOR_INVALID");
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_ENCOUNTER_REWARD_MISSING");
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_ENCOUNTER_BUDGET_INVALID");
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_ENCOUNTER_NETWORK_CAP_INVALID");
        Assert.Throws<InvalidOperationException>(() => EncounterAuthoringService.Apply(request, Context(Guid.NewGuid())));
    }

    [Fact]
    public void Candidate_pool_carries_weight_role_cost_conditions_variants_and_limits()
    {
        var creatureId = Guid.NewGuid();
        var candidate = EncounterAuthoringService.Apply(Request(creatureId), Context(creatureId)).Candidates.Single();
        Assert.Equal(EncounterRole.Controller, candidate.Role);
        Assert.Equal(3, candidate.Weight);
        Assert.Contains("night", candidate.Conditions);
        Assert.Contains("ember", candidate.Variants);
        Assert.Equal(4, candidate.MaximumAlive);
    }

    [Fact]
    public void Preview_rejects_non_finite_values_and_unknown_roles_or_difficulty()
    {
        var creatureId = Guid.NewGuid();
        var source = Request(creatureId);
        var request = source with
        {
            PrimaryRole = (EncounterRole)999,
            Candidates = [source.Candidates[0] with
            {
                Role = (EncounterRole)999,
                Weight = double.NaN,
            }],
            Volume = source.Volume with { ThreatRadius = double.PositiveInfinity },
            DifficultyProfile = source.DifficultyProfile with
            {
                Difficulty = (EncounterDifficulty)999,
                BaseBudget = double.NaN,
            },
        };

        var preview = EncounterAuthoringService.Preview(request, Context(creatureId));

        Assert.False(preview.CanApply);
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_ENCOUNTER_ROLE_INVALID");
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_ENCOUNTER_CANDIDATE_INVALID");
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_ENCOUNTER_VOLUME_INVALID");
        Assert.Contains(preview.Diagnostics, item => item.Code == "RPG_ENCOUNTER_BUDGET_INVALID");
    }

    private static EncounterWizardRequest Request(Guid creatureId)
    {
        var shape = new TriggerShape(TriggerShapeKind.ExtrudedPolygon, [new(0, 0, 0), new(8, 0, 0), new(8, 0, 8), new(0, 0, 8)], [], new(0, 0, 0), 4, 0.5);
        var filter = new TriggerFilter(new HashSet<string> { "creature" }, new HashSet<string> { "player" }, new HashSet<string>(), new HashSet<string> { "characters" });
        return new("demo-encounter", "Encounter de démonstration", "ambush", "demo-light", "zone:observatory", EncounterRole.Controller,
            [new(creatureId, "creature:glass-warden", EncounterRole.Controller, 3, 8, new HashSet<string> { "night" }, ["ember"], 4)],
            new(shape, filter, true, 18, "script:encounter-activation"),
            new("ruleset:forge", EncounterDifficulty.Hard, 4, 12, 20, 1.2, 1.5), ["group:vigil"], ["anchor:north"], ["reward:shard"], 12, "author:test");
    }

    private static EncounterValidationContext Context(Guid creatureId) => new(
        new HashSet<Guid> { creatureId }, new HashSet<string> { "creature:glass-warden" }, new HashSet<string> { "zone:observatory" },
        new HashSet<string> { "anchor:north" }, new HashSet<string> { "script:encounter-activation" }, new HashSet<string> { "reward:shard" }, 32, 100);
}
