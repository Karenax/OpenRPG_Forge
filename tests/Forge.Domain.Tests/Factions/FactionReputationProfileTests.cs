using Forge.Domain.Factions;

namespace Forge.Domain.Tests.Factions;

public sealed class FactionReputationProfileTests
{
    [Fact]
    public void ValidatesCanonicalProfileTagsTransactionsSnapshotsAndReaction()
    {
        var catalog = Sample();

        Assert.Empty(FactionValidator.Validate(catalog));
        var resolution = FactionRuntimeResolver.ResolveReputation(catalog);
        Assert.NotNull(resolution.Profile);
        Assert.False(resolution.OptionalFallback);
    }

    [Fact]
    public void OptionalProfileFallsBackAndRequiredProfileProducesStableDiagnostic()
    {
        var optional = FactionRuntimeResolver.ResolveReputation(FactionCatalog.Empty);
        var required = FactionRuntimeResolver.ResolveReputation(
            FactionCatalog.Empty with { RequireReputationRuntime = true });

        Assert.True(optional.OptionalFallback);
        Assert.Equal(FactionValidator.ReputationMissingCode, Assert.Single(required.Diagnostics).Code);
        Assert.Equal("module/factions/reputation", required.Diagnostics[0].Path);
    }

    [Fact]
    public void InvalidCanonicalFieldsProduceLocalizedStableDiagnostics()
    {
        var valid = Sample();
        var profile = valid.Reputation!;
        var invalid = valid with
        {
            Factions = [valid.Factions[0] with { Tags = [" Guard ", "guard", "guard"] }, valid.Factions[1]],
            Reputation = profile with
            {
                Transactions = [profile.Transactions[0] with
                {
                    Id = Guid.Empty,
                    Delta = 101,
                    TargetFactionId = profile.Transactions[0].SourceFactionId,
                }],
                HeatmapHistory = [profile.HeatmapHistory[0] with { Id = profile.Id, Label = "" }],
                Reaction = profile.Reaction with { Id = profile.Id, DefaultScopeId = "" },
            },
        };

        var diagnostics = FactionValidator.Validate(invalid);

        Assert.Contains(diagnostics, item => item.Code == "RPG_FACTION_TAG_INVALID");
        Assert.Contains(diagnostics, item => item.Code == "RPG_FACTION_TAG_DUPLICATE");
        Assert.Contains(diagnostics, item => item.Code == "RPG_FACTION_REPUTATION_TRANSACTION_INVALID");
        Assert.Contains(diagnostics, item => item.Code == "RPG_FACTION_HEATMAP_SNAPSHOT_INVALID");
        Assert.Contains(diagnostics, item => item.Code == "RPG_FACTION_REACTION_POLICY_INVALID");
        Assert.All(diagnostics, item => Assert.StartsWith("module/factions", item.Path, StringComparison.Ordinal));
    }

    private static FactionCatalog Sample()
    {
        var guard = Guid.Parse("b2070000-0000-4000-8000-000000000001");
        var undead = Guid.Parse("b2070000-0000-4000-8000-000000000002");
        var at = DateTimeOffset.Parse("2026-08-11T10:00:00Z");
        var relationship = new FactionRelationship(guard, undead, -50, "graveyard-crime", "hostile");
        return new(
            [
                new(guard, 1, "test:r087", "city-watch", "City Watch", false, [], ["guard", "civilian"]),
                new(undead, 1, "test:r087", "restless-dead", "Restless Dead", false, [], ["undead"]),
            ],
            [relationship],
            [new("hostile", -100, -1, 1), new("neutral", 0, 100, 1)],
            [],
            Reputation: new(
                Guid.Parse("b2070000-0000-4000-8000-000000000010"), 1, "test:r087",
                [new(Guid.Parse("b2070000-0000-4000-8000-000000000011"), 1, "test:r087",
                    ReputationScope.World, "world", guard, undead, -10, "grave-opened", at)],
                [new(Guid.Parse("b2070000-0000-4000-8000-000000000012"), 1, "test:r087", at,
                    "baseline", [relationship])],
                new(Guid.Parse("b2070000-0000-4000-8000-000000000013"), 1, "test:r087",
                    ReputationScope.World, "world", true, true, true, true, true)),
            RequireReputationRuntime: true);
    }
}
