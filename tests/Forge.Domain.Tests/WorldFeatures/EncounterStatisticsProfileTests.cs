using Forge.Domain.Areas;
using Forge.Domain.WorldFeatures;

namespace Forge.Domain.Tests.WorldFeatures;

public sealed class EncounterStatisticsProfileTests
{
    [Fact]
    public void Valid_profile_preserves_bounded_runs_estimators_seed_and_percentiles()
    {
        var profile = Valid();
        Assert.Empty(EncounterStatisticsProfileValidator.Validate(profile, "encounter/statistics"));
        var resolution = EncounterStatisticsProfileValidator.Resolve(Encounter(profile, true));
        Assert.True(resolution.Available);
        Assert.Equal([.5, .9, .99], resolution.Profile!.Percentiles);
    }

    [Fact]
    public void Invalid_configuration_reports_stable_identity_configuration_percentile_and_override_codes()
    {
        var invalid = Valid() with
        {
            Id = Guid.Empty,
            Runs = 0,
            DurationEstimatorId = string.Empty,
            SecondsPerActor = 0,
            RewardEstimatorId = string.Empty,
            RewardPerCost = -1,
            Percentiles = [.9, .5, .5, 1],
            Overrides = new Dictionary<string, string> { [string.Empty] = string.Empty },
        };
        var codes = EncounterStatisticsProfileValidator.Validate(invalid, "encounter/statistics")
            .Select(item => item.Code).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("world.encounter.statistics_identity_invalid", codes);
        Assert.Contains("world.encounter.statistics_configuration_invalid", codes);
        Assert.Contains("world.encounter.statistics_percentiles_invalid", codes);
        Assert.Contains("world.encounter.statistics_override_invalid", codes);
    }

    [Fact]
    public void Resolver_distinguishes_optional_absence_from_required_failure()
    {
        Assert.True(EncounterStatisticsProfileValidator.Resolve(Encounter(null, false)).OptionalFallback);
        Assert.Equal("world.encounter.statistics_required", Assert.Single(
            EncounterStatisticsProfileValidator.Resolve(Encounter(null, true)).Diagnostics).Code);
    }

    private static EncounterStatisticsProfile Valid() => new(
        Guid.Parse("85000000-0000-4000-8000-000000000001"), 1, "test:r085", 64, 840,
        "duration:actors-linear", 5, "reward:cost-linear", .5, [.5, .9, .99], true,
        new Dictionary<string, string>());

    private static EncounterWorldProfile Encounter(EncounterStatisticsProfile? statistics, bool required) => new(
        Guid.Parse("85000000-0000-4000-8000-000000000002"), 1, "test:r085",
        AreaId.From(Guid.Parse("85000000-0000-4000-8000-000000000003")), null, null, null, null,
        new(EncounterWorldProfile.VisibilityMode.Hidden, 0, false, string.Empty),
        new(0, 0, 0, true, true), Statistics: statistics, StatisticsRequiredAtRuntime: required);
}
