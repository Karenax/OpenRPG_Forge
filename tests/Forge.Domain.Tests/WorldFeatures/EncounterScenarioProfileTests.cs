using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Domain.Tests.WorldFeatures;

public sealed class EncounterScenarioProfileTests
{
    [Fact]
    public void Valid_scenario_resolves_manual_anchor_restrictions_lifecycle_and_wave_graph()
    {
        var encounter = Encounter();

        var resolution = EncounterScenarioProfileValidator.Resolve(encounter);

        Assert.True(resolution.Available);
        Assert.False(resolution.OptionalFallback);
        Assert.Empty(resolution.Diagnostics);
        Assert.Equal("anchor:test", Assert.Single(resolution.Profile!.SpawnAnchors).AnchorId);
        Assert.Equal("EncounterExhausted", resolution.Profile.Lifecycle.Exhausted.Binding);
        Assert.Equal(2, Assert.Single(resolution.Profile.Phases).Waves.Count);
    }

    [Fact]
    public void Manual_anchor_requires_canonical_bounded_navigable_capacity_and_one_fallback()
    {
        var encounter = Encounter();
        var invalid = encounter.Scenario! with
        {
            SpawnAnchors =
            [
                encounter.Scenario.SpawnAnchors[0] with
                {
                    AnchorId = "anchor:missing",
                    Transform = new(new(1_000_001, 0, 0), QuaternionValue.Identity, Vector3Value.One),
                    Capacity = 0,
                    Navigable = false,
                    IsFallback = false,
                },
            ],
        };

        var diagnostics = EncounterScenarioProfileValidator.Validate(invalid, encounter.Foundation,
            encounter.Composition, "encounter/scenario");

        Assert.Contains(diagnostics, item => item.Code == "world.encounter.spawn_anchor_policy_invalid");
    }

    [Fact]
    public void Activator_and_versioned_lifecycle_bindings_are_rejected_when_malformed()
    {
        var encounter = Encounter();
        var invalid = encounter.Scenario! with
        {
            Activator = encounter.Scenario.Activator with { RequiredTags = ["party", "party"] },
            Lifecycle = encounter.Scenario.Lifecycle with
            {
                ForgeScriptApiVersion = "latest",
                Entered = encounter.Scenario.Lifecycle.Entered with { Binding = "OnEnter" },
            },
        };

        var diagnostics = EncounterScenarioProfileValidator.Validate(invalid, encounter.Foundation,
            encounter.Composition, "encounter/scenario");

        Assert.Contains(diagnostics, item => item.Code == "world.encounter.activator_policy_invalid");
        Assert.Contains(diagnostics, item => item.Code == "world.encounter.lifecycle_binding_invalid");
    }

    [Fact]
    public void Wave_graph_rejects_missing_groups_unknown_objectives_and_backward_transitions()
    {
        var encounter = Encounter();
        var phase = encounter.Scenario!.Phases[0];
        var invalid = encounter.Scenario with
        {
            Phases =
            [
                phase with
                {
                    Waves =
                    [
                        phase.Waves[0] with
                        {
                            GroupIds = [Guid.NewGuid()],
                            Completion = EncounterWaveCompletionPolicy.ObjectiveCompleted,
                            QuestId = Guid.NewGuid(),
                            ObjectiveId = "missing",
                        },
                        phase.Waves[1] with { NextWaveId = phase.Waves[0].Id },
                    ],
                },
            ],
        };

        var diagnostics = EncounterScenarioProfileValidator.Validate(invalid, encounter.Foundation,
            encounter.Composition, "encounter/scenario", new HashSet<Guid> { encounter.Id },
            new HashSet<Guid>(), encounter.Id);

        Assert.Contains(diagnostics, item => item.Code == "world.encounter.wave_composition_invalid");
        Assert.Contains(diagnostics, item => item.Code == "world.encounter.wave_completion_invalid");
        Assert.Contains(diagnostics, item => item.Code == "world.encounter.wave_transition_invalid");
    }

    [Fact]
    public void Runtime_mapped_enums_are_rejected_before_the_adapter_boundary()
    {
        var encounter = Encounter();
        var phase = encounter.Scenario!.Phases[0];
        var invalid = encounter.Scenario with
        {
            Distribution = (EncounterSpawnDistribution)999,
            SpawnAnchors =
            [
                encounter.Scenario.SpawnAnchors[0] with
                { Visibility = (EncounterWorldProfile.VisibilityMode)999 },
            ],
            Activator = encounter.Scenario.Activator with
            { RequiredAuthority = (EncounterNetworkAuthority)999 },
            Phases =
            [
                phase with
                {
                    Waves =
                    [
                        phase.Waves[0] with { Completion = (EncounterWaveCompletionPolicy)999 },
                        phase.Waves[1],
                    ],
                },
            ],
        };

        var diagnostics = EncounterScenarioProfileValidator.Validate(invalid, encounter.Foundation,
            encounter.Composition, "encounter/scenario");

        Assert.Contains(diagnostics, item => item.Code == "world.encounter.spawn_distribution_invalid");
        Assert.Contains(diagnostics, item => item.Code == "world.encounter.spawn_anchor_policy_invalid");
        Assert.Contains(diagnostics, item => item.Code == "world.encounter.activator_policy_invalid");
        Assert.Contains(diagnostics, item => item.Code == "world.encounter.wave_completion_invalid");
    }

    private static EncounterWorldProfile Encounter()
    {
        var encounterId = Guid.Parse("84000000-0000-4000-8000-000000000101");
        var candidateId = Guid.Parse("84000000-0000-4000-8000-000000000102");
        var groupId = Guid.Parse("84000000-0000-4000-8000-000000000112");
        var waveOne = Guid.Parse("84000000-0000-4000-8000-000000000121");
        var waveTwo = Guid.Parse("84000000-0000-4000-8000-000000000122");
        return new(encounterId, 1, "test:r084",
            AreaId.From(Guid.Parse("84000000-0000-4000-8000-000000000103")), null, null, null, null,
            new(EncounterWorldProfile.VisibilityMode.BehindCover, 10, true, "vfx:spawn"),
            new(30, 30, 120, true, true),
            new(new(EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D,
                    [new(-4, 0, -4), new(4, 0, -4), new(4, 0, 4), new(-4, 0, 4)], Vector3Value.Zero,
                    4, 8, ["player"], ["party"], true, 18, "scripts/activate", true),
                [new(candidateId, BlueprintId.From(Guid.Parse("84000000-0000-4000-8000-000000000104")),
                    EncounterWorldProfile.CandidateRole.Controller, 3, 8, [], ["ember"], 4)],
                36, ["anchor:test"], ["reward:test"], 12),
            new(Guid.Parse("84000000-0000-4000-8000-000000000110"), 1, "test:r084", "ash-ambush",
                "Ash ambush", "ambush", "ash-crypt", EncounterWorldProfile.CandidateRole.Controller,
                new(Guid.Parse("84000000-0000-4000-8000-000000000111"), "ruleset:forge",
                    "party:forge-generic", EncounterDifficultyBand.Hard, 4, 12, 20, 1.2, 1.5),
                [new(groupId, "wave:warden", 2, 4, 36, [candidateId])],
                new(Guid.Parse("84000000-0000-4000-8000-000000000113"), "party:forge-generic",
                    "challenge-rating", 42, true),
                new(Guid.Parse("84000000-0000-4000-8000-000000000114"),
                    EncounterActivationMode.Wave, 0, null, EncounterSpawnDistribution.Weighted),
                new Dictionary<string, string>()), true,
            new(Guid.Parse("84000000-0000-4000-8000-000000000115"), 1, "test:r084",
                [new(Guid.Parse("84000000-0000-4000-8000-000000000116"), "anchor:test",
                    new(new(2, 0, 4), QuaternionValue.Identity, Vector3Value.One), 8, true,
                    EncounterWorldProfile.VisibilityMode.BehindCover, true, 10)],
                EncounterSpawnDistribution.Weighted,
                new(Guid.Parse("84000000-0000-4000-8000-000000000117"), true, true, true, true, true,
                    EncounterNetworkAuthority.Server, ["party"]),
                new(Guid.Parse("84000000-0000-4000-8000-000000000118"), "1.0.0",
                    new(Guid.Parse("84000000-0000-4000-8000-000000000119"), "EncounterEntered", "scripts/entered"),
                    new(Guid.Parse("84000000-0000-4000-8000-00000000011a"), "EncounterExited", "scripts/exited"),
                    new(Guid.Parse("84000000-0000-4000-8000-00000000011b"), "EncounterExhausted", "scripts/exhausted")),
                [new(Guid.Parse("84000000-0000-4000-8000-000000000120"), 1, "Opening", ["area.occupied"],
                    [new(waveOne, 1, [groupId], 0, EncounterWaveCompletionPolicy.AllDefeated, null, null,
                            null, "audio:warning", waveTwo),
                        new(waveTwo, 2, [groupId], 3, EncounterWaveCompletionPolicy.TimerElapsed, null, null,
                            null, "audio:advance", null)], [])], new Dictionary<string, string>()), true);
    }
}
