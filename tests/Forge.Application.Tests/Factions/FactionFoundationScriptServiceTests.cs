using Forge.Application.Factions;
using Forge.Domain.Factions;

namespace Forge.Application.Tests.Factions;

public sealed class FactionFoundationScriptServiceTests
{
    [Fact]
    public void TypedHandlesReadAsymmetricRelationsBandsAndPersonalReputation()
    {
        var catalog = FactionAdvancedTestData.Complete();
        var service = new FactionFoundationScriptService(catalog);
        var guild = catalog.Factions.Single(item => item.Slug == "ember-guild");
        var cult = catalog.Factions.Single(item => item.Slug == "glass-cult");
        var guildHandle = service.Handle(guild.Id);
        var cultHandle = service.Handle(cult.Id);

        var snapshot = service.Read(guildHandle);
        var relation = service.Relationship(guildHandle, cultHandle);
        var reverse = service.Relationship(cultHandle, guildHandle);
        var disposition = service.Disposition(guildHandle, cultHandle, "hero",
            DateTimeOffset.Parse("2026-08-02T00:00:00Z"));

        Assert.Equal("ember-guild", snapshot.Slug);
        Assert.Equal(1, snapshot.PersonalReputationCount);
        Assert.Equal(-80, relation!.Score);
        Assert.Null(reverse);
        Assert.Equal("hostile", disposition);
    }

    [Fact]
    public void MissingFoundationAndInvalidHandleAreRejectedWithStableCodes()
    {
        var missing = Assert.Throws<ArgumentException>(() =>
            new FactionFoundationScriptService(FactionCatalog.Empty));
        var service = new FactionFoundationScriptService(FactionAdvancedTestData.Complete());
        var invalid = Assert.Throws<KeyNotFoundException>(() => service.Read(
            new(Guid.Parse("b2860000-0000-4000-8000-000000000099"), 1)));

        Assert.Contains("FACTION_FOUNDATION_MISSING", missing.Message, StringComparison.Ordinal);
        Assert.Contains("FACTION_DEFINITION_HANDLE_INVALID", invalid.Message, StringComparison.Ordinal);
    }
}
