using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;
using Forge.RpgKit.Application.Encounters;

namespace Forge.RpgKit.Domain.Tests.Encounters;

public sealed class EncounterCompositionFoundationServiceTests
{
    [Fact]
    public void Canonical_profile_reuses_seeded_composition_with_candidate_id_translation()
    {
        var encounter = Encounter();

        var first = EncounterCompositionFoundationService.Preview(encounter, 4, 12,
            new HashSet<string>(["party"], StringComparer.Ordinal));
        var second = EncounterCompositionFoundationService.Preview(encounter, 4, 12,
            new HashSet<string>(["party"], StringComparer.Ordinal));

        Assert.Empty(first.Diagnostics);
        Assert.InRange(first.Composition.TotalCount, 2, 4);
        Assert.InRange(first.Composition.TotalCost, 0, 36);
        Assert.Equal(first.Composition.Candidates.Select(item => item.CreatureId),
            second.Composition.Candidates.Select(item => item.CreatureId));
        Assert.Equal(first.Composition.Summary, second.Composition.Summary);
        Assert.All(first.Composition.Candidates,
            item => Assert.Equal(encounter.Foundation!.Candidates[0].BlueprintId.Value, item.CreatureId));
    }

    [Fact]
    public void Existing_authoring_validation_accepts_resolved_canonical_references()
    {
        var encounter = Encounter();
        var definition = EncounterCompositionFoundationService.CreateDefinition(encounter);
        var context = new Forge.RpgKit.Domain.Encounters.EncounterValidationContext(
            definition.Candidates.Select(item => item.CreatureId).ToHashSet(),
            definition.Candidates.Select(item => item.BlueprintId).ToHashSet(StringComparer.Ordinal),
            new HashSet<string>([definition.ZoneId], StringComparer.Ordinal),
            definition.SpawnAnchorIds.ToHashSet(StringComparer.Ordinal),
            new HashSet<string>([definition.Volume.ActivationScript!], StringComparer.Ordinal),
            definition.RewardIds.ToHashSet(StringComparer.Ordinal), 128, 100000);

        var diagnostics = EncounterCompositionFoundationService.ValidateAuthoring(encounter, context);

        Assert.Empty(diagnostics);
        Assert.Equal("ash-ambush", definition.Slug);
        Assert.Equal(36, definition.DifficultyProfile.EffectiveBudget);
    }

    [Fact]
    public void Seven_activation_modes_map_without_ruleset_hard_coding_and_missing_policy_is_explicit()
    {
        var encounter = Encounter();
        foreach (var mode in Enum.GetValues<EncounterActivationMode>())
        {
            var profile = encounter with
            {
                Composition = encounter.Composition! with
                {
                    Activation = encounter.Composition.Activation with
                    {
                        Mode = mode,
                        IntervalSeconds = mode == EncounterActivationMode.Timed ? 30 : 0,
                        ScriptId = mode == EncounterActivationMode.Script ? "scripts/policy" : null,
                    },
                },
            };
            Assert.Equal((int)mode, (int)EncounterCompositionFoundationService.CreateRuntimePolicy(profile).Activation);
        }

        Assert.True(EncounterCompositionFoundationService.Resolve(encounter with
        { Composition = null, CompositionRequiredAtRuntime = false }).OptionalFallback);
        Assert.Equal("world.encounter.composition_required", Assert.Single(
            EncounterCompositionFoundationService.Resolve(encounter with { Composition = null }).Diagnostics).Code);
    }

    [Fact]
    public void Manual_spawn_anchor_and_authoritative_player_restrictions_map_to_existing_runtime_policy()
    {
        var encounter = Encounter();
        var policy = EncounterCompositionFoundationService.CreateRuntimePolicy(encounter);
        var accepted = new Forge.RpgKit.Domain.Encounters.Activator("player:one", true, false, false, false,
            false, Forge.RpgKit.Domain.Encounters.NetworkAuthority.Server,
            new HashSet<string>(["party"], StringComparer.Ordinal));
        var denied = accepted with { IsCompanion = true };

        Assert.Equal(new Forge.Domain.Spatial.Vector3Value(2, 0, 4), Assert.Single(policy.Anchors).Transform.Position);
        Assert.Equal(8, policy.Anchors[0].Capacity);
        Assert.True(Forge.RpgKit.Domain.Encounters.EncounterCompositionService.CanActivate(accepted,
            policy.Activators));
        Assert.False(Forge.RpgKit.Domain.Encounters.EncounterCompositionService.CanActivate(denied,
            policy.Activators));
    }

    [Fact]
    public void Scenario_preview_reuses_lifecycle_waves_statistics_and_complete_exhausted_payload()
    {
        var encounter = Encounter();
        var activator = new Forge.RpgKit.Domain.Encounters.Activator("player:one", true, false, false, false,
            false, Forge.RpgKit.Domain.Encounters.NetworkAuthority.Server,
            new HashSet<string>(["party"], StringComparer.Ordinal));

        var preview = EncounterCompositionFoundationService.PreviewScenario(encounter, activator, 4, 12,
            new HashSet<string>(["party"], StringComparer.Ordinal), DateTimeOffset.UnixEpoch,
            TimeSpan.FromSeconds(9));

        Assert.Empty(preview.Diagnostics);
        Assert.True(preview.ActivatorAccepted);
        Assert.Equal(TimeSpan.FromSeconds(9), preview.Exited.DurationInside);
        Assert.Equal("waves-completed", preview.Exhausted.Cause);
        Assert.True(preview.Exhausted.Kills > 0);
        Assert.Equal(2, preview.Exhausted.Waves);
        Assert.Equal(["reward:test"], preview.Exhausted.RewardIds);
        Assert.Equal("audio:warning", preview.Phases[0].Waves[0].VoiceCueId);
        Assert.Equal(preview.Phases[0].Waves[1].Id, preview.Phases[0].Waves[0].NextWaveId);
    }

    [Fact]
    public void Scenario_resolver_distinguishes_optional_fallback_and_required_diagnostic()
    {
        var encounter = Encounter();

        Assert.True(EncounterCompositionFoundationService.ResolveScenario(encounter with
        { Scenario = null, ScenarioRequiredAtRuntime = false }).OptionalFallback);
        Assert.Equal("world.encounter.scenario_required", Assert.Single(
            EncounterCompositionFoundationService.ResolveScenario(encounter with { Scenario = null }).Diagnostics).Code);
    }

    [Fact]
    public void Statistical_preview_is_reproducible_and_reports_composition_cost_duration_rewards_variances_percentiles()
    {
        var encounter = Encounter();
        var first = EncounterCompositionFoundationService.PreviewStatistics(encounter);
        var second = EncounterCompositionFoundationService.PreviewStatistics(encounter);

        Assert.Equal(64, first.Statistics.Runs);
        Assert.Equal(Enumerable.Range(840, 64), first.Statistics.Results.Select(item => item.Seed));
        Assert.Equal(first.Summary, second.Summary);
        Assert.Equal(first.Statistics.Results.Select(item =>
                $"{item.Seed}|{item.SpawnedCount}|{item.Cost}|{item.DurationSeconds}|{item.RewardValue}|" +
                string.Join(',', item.Composition.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"))),
            second.Statistics.Results.Select(item =>
                $"{item.Seed}|{item.SpawnedCount}|{item.Cost}|{item.DurationSeconds}|{item.RewardValue}|" +
                string.Join(',', item.Composition.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"))));
        Assert.NotEmpty(first.Statistics.CompositionTotals);
        Assert.True(first.Statistics.MeanCost > 0);
        Assert.True(first.Statistics.MeanDurationSeconds > 0);
        Assert.True(first.Statistics.MeanRewardValue > 0);
        Assert.True(first.Statistics.CostVariance >= 0);
        Assert.True(first.Statistics.DurationVariance >= 0);
        Assert.True(first.Statistics.RewardVariance >= 0);
        Assert.Equal([.5, .9, .99], first.Statistics.Percentiles!.Keys);
    }

    [Fact]
    public void Statistics_resolver_distinguishes_optional_fallback_and_required_diagnostic()
    {
        var encounter = Encounter();
        Assert.True(EncounterCompositionFoundationService.ResolveStatistics(encounter with
        { Statistics = null, StatisticsRequiredAtRuntime = false }).OptionalFallback);
        Assert.Equal("world.encounter.statistics_required", Assert.Single(
            EncounterCompositionFoundationService.ResolveStatistics(encounter with { Statistics = null }).Diagnostics).Code);
    }

    private static EncounterWorldProfile Encounter()
    {
        var encounterId = Guid.Parse("83000000-0000-4000-8000-000000000101");
        var candidateId = Guid.Parse("83000000-0000-4000-8000-000000000102");
        return new(encounterId, 1, "test:r083",
            AreaId.From(Guid.Parse("83000000-0000-4000-8000-000000000103")), null, null, null, null,
            new(EncounterWorldProfile.VisibilityMode.BehindCover, 10, true, "vfx:spawn"),
            new(30, 30, 120, true, true),
            new(new(EncounterWorldProfile.VolumeShape.ExtrudedPolygon2D,
                    [new(-4, 0, -4), new(4, 0, -4), new(4, 0, 4), new(-4, 0, 4)], Vector3Value.Zero,
                    4, 8, ["player"], ["party"], true, 18, "scripts/activate", true),
                [new(candidateId, BlueprintId.From(Guid.Parse("83000000-0000-4000-8000-000000000104")),
                    EncounterWorldProfile.CandidateRole.Controller, 3, 8, [], ["ember"], 4)],
                36, ["anchor:test"], ["reward:test"], 12),
            new(Guid.Parse("83000000-0000-4000-8000-000000000110"), 1, "test:r083", "ash-ambush",
                "Ash ambush", "ambush", "ash-crypt", EncounterWorldProfile.CandidateRole.Controller,
                new(Guid.Parse("83000000-0000-4000-8000-000000000111"), "ruleset:forge",
                    "party:forge-generic", EncounterDifficultyBand.Hard, 4, 12, 20, 1.2, 1.5),
                [new(Guid.Parse("83000000-0000-4000-8000-000000000112"), "wave:warden", 2, 4, 36,
                    [candidateId])],
                new(Guid.Parse("83000000-0000-4000-8000-000000000113"), "party:forge-generic",
                    "challenge-rating", 42, true),
                new(Guid.Parse("83000000-0000-4000-8000-000000000114"),
                    EncounterActivationMode.PopulationMaintained, 30, null, EncounterSpawnDistribution.Weighted),
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
                    [new(Guid.Parse("84000000-0000-4000-8000-000000000121"), 1,
                            [Guid.Parse("83000000-0000-4000-8000-000000000112")], 0,
                            EncounterWaveCompletionPolicy.AllDefeated, null, null, null, "audio:warning",
                            Guid.Parse("84000000-0000-4000-8000-000000000122")),
                        new(Guid.Parse("84000000-0000-4000-8000-000000000122"), 2,
                            [Guid.Parse("83000000-0000-4000-8000-000000000112")], 2,
                            EncounterWaveCompletionPolicy.TimerElapsed, null, null, null, "audio:advance", null)],
                    [])], new Dictionary<string, string>()), true,
            new(Guid.Parse("85000000-0000-4000-8000-000000000001"), 1, "test:r085", 64, 840,
                "duration:actors-linear", 5, "reward:cost-linear", .5, [.5, .9, .99], true,
                new Dictionary<string, string>()), true);
    }
}
