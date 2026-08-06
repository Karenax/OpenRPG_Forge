using Forge.Domain.Factions;

namespace Forge.Domain.Tests.Factions;

public sealed class FactionAdvancedProfileTests
{
    [Fact]
    public void HierarchyInheritsTagsPoliciesAndRelationshipsWithoutCycles()
    {
        var catalog = CompleteCatalog();
        var child = catalog.Factions.Single(item => item.Slug == "ash-watch");
        var outsider = catalog.Factions.Single(item => item.Slug == "glass-cult");

        var effective = FactionRuntimeResolver.EffectiveDefinition(catalog, child.Id);
        var relationship = FactionRuntimeResolver.EffectiveRelationship(catalog, child.Id, outsider.Id);

        Assert.Empty(FactionValidator.Validate(catalog));
        Assert.Contains("guard", effective.Tags);
        Assert.Contains("watch", effective.Tags);
        Assert.Equal("protect", effective.Policies["civilian-response"]);
        Assert.Equal(-70, relationship!.Score);
        Assert.Equal(child.Id, relationship.SourceId);
    }

    [Fact]
    public void WitnessPropagationUsesPerceptionEvidenceMemoryAndCommunication()
    {
        var catalog = CompleteCatalog();
        var watch = catalog.Factions.Single(item => item.Slug == "ash-watch");
        var now = DateTimeOffset.Parse("2026-08-05T12:00:00Z");
        var accepted = Observation("b2010000-0000-4000-8000-000000000031", watch.Id, now.AddSeconds(-30), .9, .8, 8, true);
        var forgotten = Observation("b2010000-0000-4000-8000-000000000032", watch.Id, now.AddMinutes(-30), .9, .8, 8, true);
        var silent = Observation("b2010000-0000-4000-8000-000000000033", watch.Id, now.AddSeconds(-10), .9, .8, 8, false);

        var result = FactionRuntimeResolver.PropagateWitnesses(catalog, [forgotten, accepted, silent], now);

        var report = Assert.Single(result.Reports);
        Assert.Equal(accepted.Id, report.ObservationId);
        Assert.Equal(-15, report.ReputationDelta);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void MultipleMembershipsResolvePrimaryCoverAndExpirationDeterministically()
    {
        var catalog = CompleteCatalog();
        var now = DateTimeOffset.Parse("2026-08-05T12:00:00Z");

        var memberships = FactionRuntimeResolver.Memberships(catalog, "warden", now);
        var expired = FactionRuntimeResolver.Memberships(catalog, "warden", now.AddDays(2));

        Assert.Equal(2, memberships.Count);
        Assert.True(memberships[0].IsPrimary);
        Assert.Equal("captain", memberships[0].Rank);
        Assert.Equal("Ember Pilgrim", memberships[1].CoverIdentity);
        Assert.Single(expired);
        Assert.True(expired[0].IsPrimary);
    }

    [Fact]
    public void DynamicDiplomacyRequiresServerAndExpiresWithMonotonicRevision()
    {
        var catalog = CompleteCatalog();
        var profile = catalog.Advanced!;
        var dynamic = Assert.Single(profile.DynamicFactions);
        var target = catalog.Factions.Single(item => item.Slug == "glass-cult");
        var now = DateTimeOffset.Parse("2026-08-05T12:00:00Z");
        var truce = new FactionDiplomacyState(Guid.Parse("b2010000-0000-4000-8000-000000000041"),
            target.Id, FactionDiplomacyStatus.Truce, now, now.AddHours(1), "boss-defeated");

        var denied = FactionRuntimeResolver.TransitionDiplomacy(profile, dynamic.Id, truce, false);
        var applied = FactionRuntimeResolver.TransitionDiplomacy(profile, dynamic.Id, truce, true);
        var expired = FactionRuntimeResolver.AdvanceExpirations(applied.Profile, now.AddDays(20), true);

        Assert.Equal("faction.dynamic.authority_denied", Assert.Single(denied.Diagnostics).Code);
        Assert.Equal(2, Assert.Single(applied.Profile.DynamicFactions).Revision);
        Assert.Equal(FactionDiplomacyStatus.Truce,
            Assert.Single(Assert.Single(applied.Profile.DynamicFactions).Diplomacy).Status);
        Assert.False(Assert.Single(expired.Profile.DynamicFactions).Active);
        Assert.Contains(expired.Events, item => item.Code == "faction.dynamic.expired");
    }

    [Fact]
    public void InvalidAndRequiredMissingProfilesProduceStableLocalizedDiagnostics()
    {
        var valid = CompleteCatalog();
        var invalid = valid with
        {
            Advanced = valid.Advanced! with
            {
                Id = Guid.Empty,
                ServerAuthoritative = false,
                Memberships = [valid.Advanced.Memberships[0] with { Weight = 2 }],
            },
        };
        var requiredMissing = FactionRuntimeResolver.Resolve(FactionCatalog.Empty with
        {
            RequireAdvancedRuntime = true,
        });
        var optional = FactionRuntimeResolver.Resolve(FactionCatalog.Empty);

        var diagnostics = FactionValidator.Validate(invalid, "module/factions");
        Assert.Contains(diagnostics, item => item.Code == "faction.advanced.uuid_empty");
        Assert.Contains(diagnostics, item => item.Code == "faction.advanced.authority_required");
        Assert.Contains(diagnostics, item => item.Code == "faction.membership.invalid");
        Assert.All(diagnostics, item => Assert.StartsWith("module/factions", item.Path));
        Assert.Equal(FactionValidator.AdvancedMissingCode, Assert.Single(requiredMissing.Diagnostics).Code);
        Assert.True(optional.OptionalFallback);
    }

    internal static FactionCatalog CompleteCatalog()
    {
        var parentId = Guid.Parse("b2010000-0000-4000-8000-000000000001");
        var childId = Guid.Parse("b2010000-0000-4000-8000-000000000002");
        var outsiderId = Guid.Parse("b2010000-0000-4000-8000-000000000003");
        var created = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        return new FactionCatalog(
            [
                new(parentId, 1, "author:test", "ember-guard", "Garde des braises", false, [],
                    ["guard", "civilian"],
                    new Dictionary<string, string> { ["civilian-response"] = "protect" }),
                new(childId, 1, "author:test", "ash-watch", "Veilleurs des cendres", false, [parentId],
                    ["watch"],
                    new Dictionary<string, string> { ["patrol"] = "crypt" }),
                new(outsiderId, 1, "author:test", "glass-cult", "Culte de verre", false, [],
                    ["cult"]),
            ],
            [new(parentId, outsiderId, -70, "ancestral-war", "hostile")],
            [new("hostile", -100, -1, 1), new("neutral", 0, 50, 1), new("allied", 51, 100, 1)],
            [],
            new FactionAdvancedProfile(
                Guid.Parse("b2010000-0000-4000-8000-000000000010"), 1, "author:test", true,
                [new(Guid.Parse("b2010000-0000-4000-8000-000000000011"), childId, true, true, true)],
                [new(Guid.Parse("b2010000-0000-4000-8000-000000000012"), childId, .6, .5, 30, 300, -15, true)],
                [
                    new(Guid.Parse("b2010000-0000-4000-8000-000000000013"), 1, "author:test", "warden",
                        childId, "captain", null, 95, 1, true, null),
                    new(Guid.Parse("b2010000-0000-4000-8000-000000000014"), 1, "author:test", "warden",
                        outsiderId, "initiate", "Ember Pilgrim", -20, .3, false, created.AddDays(5)),
                ],
                [new(Guid.Parse("b2010000-0000-4000-8000-000000000015"), 1, "runtime:test", childId,
                    DynamicFactionKind.Temporary, created, created.AddDays(10), true, true, 1, [])]),
            true);
    }

    private static FactionWitnessObservation Observation(
        string id, Guid factionId, DateTimeOffset at, double perception, double evidence, double distance,
        bool canCommunicate) => new(Guid.Parse(id), "crime-ember-theft", "hero", "warden", factionId,
        "theft", perception, evidence, distance, at, canCommunicate);
}
