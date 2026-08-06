using Forge.Application.Factions;
using Forge.Domain.Factions;

namespace Forge.Application.Tests.Factions;

public sealed class FactionScriptServiceTests
{
    [Fact]
    public void TypedHandleReadsHierarchyMembershipsWitnessesAndAuthoritativeDiplomacy()
    {
        var catalog = FactionAdvancedTestData.Complete();
        var service = new FactionScriptService(catalog);
        var now = DateTimeOffset.Parse("2026-08-05T12:00:00Z");
        var watch = catalog.Factions.Single(item => item.Slug == "ash-watch");
        var cult = catalog.Factions.Single(item => item.Slug == "glass-cult");
        var dynamic = Assert.Single(catalog.Advanced!.DynamicFactions);
        var observation = new FactionWitnessObservation(
            Guid.Parse("b2020000-0000-4000-8000-000000000020"), "theft", "hero", "warden", watch.Id,
            "crime.theft", .9, .8, 10, now.AddSeconds(-20), true);
        var diplomacy = new FactionDiplomacyState(Guid.Parse("b2020000-0000-4000-8000-000000000021"),
            cult.Id, FactionDiplomacyStatus.Truce, now, now.AddHours(1), "quest-completed");

        var snapshot = service.Read(service.Handle);
        var effective = service.EffectiveDefinition(service.Handle, watch.Id);
        var memberships = service.Memberships(service.Handle, "warden", now);
        var reports = service.PropagateWitnesses(service.Handle, [observation], now);
        var denied = service.TransitionDiplomacy(service.Handle, dynamic.Id, diplomacy, false);
        var applied = service.TransitionDiplomacy(service.Handle, dynamic.Id, diplomacy, true);

        Assert.Equal(3, snapshot.FactionCount);
        Assert.Contains("guild", effective.Tags);
        Assert.Single(memberships);
        Assert.Single(reports.Reports);
        Assert.Equal("faction.dynamic.authority_denied", Assert.Single(denied.Diagnostics).Code);
        Assert.Equal(FactionDiplomacyStatus.Truce,
            Assert.Single(Assert.Single(applied.Profile.DynamicFactions).Diplomacy).Status);
    }

    [Fact]
    public void UnknownHandleAndMissingProfileAreRejectedWithStableCodes()
    {
        var service = new FactionScriptService(FactionAdvancedTestData.Complete());
        var exception = Assert.Throws<KeyNotFoundException>(() => service.Read(
            new FactionHandle(Guid.Parse("b2020000-0000-4000-8000-000000000099"), 1)));
        var missing = Assert.Throws<ArgumentException>(() => new FactionScriptService(FactionCatalog.Empty));

        Assert.Contains("FACTION_HANDLE_INVALID", exception.Message, StringComparison.Ordinal);
        Assert.Contains("FACTION_ADVANCED_PROFILE_MISSING", missing.Message, StringComparison.Ordinal);
    }
}
