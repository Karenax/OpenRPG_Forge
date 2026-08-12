using Forge.RpgKit.Application.Factions;
using Forge.Domain.Factions;

namespace Forge.RpgKit.Domain.Tests.Factions;

public sealed class FactionFoundationTests
{
    [Fact]
    public void DirectionalMatrixAndBandsRemainAsymmetricAndExplainable()
    {
        var village = Faction("village"); var glass = Faction("glass");
        var catalog = new FactionCatalog([village, glass], [new(village.Id, glass.Id, -80, "raids", "war"), new(glass.Id, village.Id, 20, "harvest", "predator")],
            [new("hostile", -100, -25, 1), new("neutral", -24, 49, 1), new("allied", 50, 100, 1)], []);
        Assert.Empty(FactionValidator.Validate(catalog)); Assert.Equal("hostile", FactionValidator.ResolveBand(catalog, village.Id, glass.Id)); Assert.Equal("neutral", FactionValidator.ResolveBand(catalog, glass.Id, village.Id));
    }

    [Fact]
    public void AuthoringSupportsDryRunArchiveAndSafeRemovalWithMigration()
    {
        var village = Faction("village"); var glass = Faction("glass"); var state = new FactionProjectState(0, new([village, glass], [], [new("neutral", -100, 100, 1)], [new("sentinel", glass.Id)]));
        var preview = FactionAuthoringService.ArchiveOrRemove(state, 0, glass.Id, village.Id, true, true); Assert.True(preview.Succeeded); Assert.False(preview.Applied); Assert.Equal(0, preview.State.Revision);
        var applied = FactionAuthoringService.ArchiveOrRemove(state, 0, glass.Id, village.Id, true, false); Assert.True(applied.Applied); Assert.Equal(village.Id, Assert.Single(applied.State.Catalog.Members).FactionId); Assert.DoesNotContain(applied.State.Catalog.Factions, item => item.Id == glass.Id);
    }

    [Fact]
    public void ValidatorDetectsCycleOrphansMembersAndOverlappingBands()
    {
        var firstId = Guid.NewGuid(); var secondId = Guid.NewGuid(); var orphanId = Guid.NewGuid();
        var catalog = new FactionCatalog(
            [
                new(firstId, 1, "test", "a", "A", false, [secondId], []),
                new(secondId, 1, "test", "b", "B", false, [firstId], []),
                new(Guid.NewGuid(), 1, "test", "c", "C", false, [orphanId], []),
            ],
            [],
            [new("x", -10, 10, 1), new("y", 0, 20, 1)],
            [new("ghost", Guid.NewGuid())]);

        var diagnostics = FactionValidator.Validate(catalog);

        Assert.Contains(diagnostics, item => item.Code == "RPG_FACTION_INHERITANCE_CYCLE");
        Assert.Contains(diagnostics, item => item.Code == "RPG_FACTION_PARENT_ORPHAN" &&
            item.Path.EndsWith($"/parents/{orphanId}", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item => item.Code == "RPG_FACTION_MEMBER_ORPHAN");
        Assert.Contains(diagnostics, item => item.Code == "RPG_FACTION_BANDS_OVERLAP");
    }

    [Fact]
    public void SafeRemovalMigratesLegacyAdvancedAndPersonalReferences()
    {
        var source = Faction("source");
        var target = Faction("target");
        var advanced = new FactionAdvancedProfile(Guid.NewGuid(), 1, "test:r086", true, [], [],
            [new(Guid.NewGuid(), 1, "test:r086", "hero", source.Id, "member", null, 10, 1, true, null)],
            []);
        var personal = new PersonalReputationOverlay(Guid.NewGuid(), 1, "test:r086", "hero",
            target.Id, source.Id, 20, null, "rescued", PersonalReputationSubjectKind.Player);
        var at = DateTimeOffset.UnixEpoch;
        var reputation = new FactionReputationProfile(Guid.NewGuid(), 1, "test:r087",
            [new(Guid.NewGuid(), 1, "test:r087", ReputationScope.World, "world",
                target.Id, source.Id, 10, "truce", at)],
            [new(Guid.NewGuid(), 1, "test:r087", at, "before",
                [new(target.Id, source.Id, -20, "distrust", "wary")])],
            new(Guid.NewGuid(), 1, "test:r087", ReputationScope.World, "world",
                true, true, true, true, true));
        var catalog = new FactionCatalog([source, target], [], [new("neutral", -100, 100, 1)],
            [new("legacy", source.Id)], advanced, true, [personal], true, reputation, true);

        var preview = FactionAuthoringService.ArchiveOrRemove(
            new(0, catalog), 0, source.Id, target.Id, remove: true, dryRun: true);
        var applied = FactionAuthoringService.ArchiveOrRemove(
            new(0, catalog), 0, source.Id, target.Id, remove: true, dryRun: false);

        Assert.True(preview.Succeeded);
        Assert.False(preview.Applied);
        Assert.True(applied.Applied);
        Assert.DoesNotContain(applied.State.Catalog.Factions, item => item.Id == source.Id);
        Assert.All(applied.State.Catalog.Members, item => Assert.Equal(target.Id, item.FactionId));
        Assert.All(applied.State.Catalog.Advanced!.Memberships, item => Assert.Equal(target.Id, item.FactionId));
        Assert.Empty(applied.State.Catalog.EffectivePersonalReputation);
        Assert.Empty(applied.State.Catalog.Reputation!.Transactions);
        Assert.Empty(Assert.Single(applied.State.Catalog.Reputation.HeatmapHistory).Relationships);
        Assert.Empty(FactionValidator.Validate(applied.State.Catalog));
    }

    private static FactionDefinition Faction(string slug) => new(Guid.NewGuid(), 1, "author:veillee", slug, slug, false, [], [slug == "village" ? "civilian" : "hostile"]);
}
