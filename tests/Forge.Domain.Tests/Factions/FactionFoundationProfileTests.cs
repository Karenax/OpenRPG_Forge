using Forge.Domain.Factions;

namespace Forge.Domain.Tests.Factions;

public sealed class FactionFoundationProfileTests
{
    [Fact]
    public void FoundationResolvesDirectionalMatrixBandsAndPersonalOverlay()
    {
        var catalog = Complete();

        var resolution = FactionRuntimeResolver.ResolveFoundation(catalog);

        Assert.Same(catalog, resolution.Catalog);
        Assert.Empty(resolution.Diagnostics);
        Assert.Equal("hostile", FactionValidator.ResolveBand(catalog,
            catalog.Factions[0].Id, catalog.Factions[1].Id));
        Assert.Equal("neutral", FactionValidator.ResolveBand(catalog,
            catalog.Factions[1].Id, catalog.Factions[0].Id));
        Assert.Equal(PersonalReputationSubjectKind.Player,
            Assert.Single(catalog.EffectivePersonalReputation).SubjectKind);
    }

    [Fact]
    public void OptionalEmptyFoundationIsSafeAndRequiredEmptyFoundationIsDiagnostic()
    {
        var optional = FactionRuntimeResolver.ResolveFoundation(FactionCatalog.Empty);
        var required = FactionRuntimeResolver.ResolveFoundation(
            FactionCatalog.Empty with { RequireFoundationRuntime = true });

        Assert.True(optional.OptionalFallback);
        Assert.Empty(optional.Diagnostics);
        Assert.Equal(FactionValidator.FoundationMissingCode, Assert.Single(required.Diagnostics).Code);
        Assert.False(required.OptionalFallback);
    }

    [Fact]
    public void InvalidIdentityMatrixBandsAndPersonalOverlayHaveStablePaths()
    {
        var faction = new FactionDefinition(Guid.Empty, 2, "", "", "", false, [], []);
        var catalog = new FactionCatalog(
            [faction, faction],
            [new(Guid.NewGuid(), Guid.NewGuid(), 200, "", "")],
            [new("", 10, -10, -1), new("", -20, 20, 0)],
            [],
            PersonalReputation:
            [
                new(Guid.Empty, 2, "", "", Guid.Empty, Guid.Empty, 200, null, ""),
                new(Guid.Empty, 2, "", "", Guid.Empty, Guid.Empty, 200, null, ""),
            ]);

        var diagnostics = FactionValidator.Validate(catalog);

        Assert.Contains(diagnostics, item => item.Code == "RPG_FACTION_UUID_DUPLICATE");
        Assert.Contains(diagnostics, item => item.Code == "RPG_FACTION_RELATION_INVALID");
        Assert.Contains(diagnostics, item => item.Code == "RPG_FACTION_BAND_INVALID");
        Assert.Contains(diagnostics, item => item.Code == "RPG_FACTION_BAND_DUPLICATE");
        Assert.Contains(diagnostics, item => item.Code == "RPG_FACTION_PERSONAL_REPUTATION_INVALID");
        Assert.Contains(diagnostics, item => item.Code == "RPG_FACTION_PERSONAL_REPUTATION_UUID_DUPLICATE");
        Assert.All(diagnostics, item => Assert.StartsWith("module/factions", item.Path, StringComparison.Ordinal));
    }

    internal static FactionCatalog Complete()
    {
        var first = Guid.Parse("b2860000-0000-4000-8000-000000000001");
        var second = Guid.Parse("b2860000-0000-4000-8000-000000000002");
        return new(
            [
                new(first, 1, "test:r086", "first", "First", false, [], ["guard"]),
                new(second, 1, "test:r086", "second", "Second", false, [], ["merchant"]),
            ],
            [
                new(first, second, -75, "raid", "war"),
                new(second, first, 20, "trade", "neutral"),
            ],
            [new("hostile", -100, -1, 1), new("neutral", 0, 50, 1), new("allied", 51, 100, 1)],
            [],
            PersonalReputation:
            [new(Guid.Parse("b2860000-0000-4000-8000-000000000003"), 1, "test:r086", "hero",
                second, first, 35, DateTimeOffset.Parse("2030-01-01T00:00:00Z"), "saved-trader",
                PersonalReputationSubjectKind.Player)],
            RequireFoundationRuntime: true);
    }
}
