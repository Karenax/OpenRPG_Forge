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

    private static FactionDefinition Faction(string slug) => new(Guid.NewGuid(), 1, "author:veillee", slug, slug, false, [], [slug == "village" ? "civilian" : "hostile"]);
}
