using Forge.Application.Factions;
using Forge.Domain.Factions;

namespace Forge.Application.Tests.Factions;

public sealed class FactionReputationScriptServiceTests
{
    [Fact]
    public void ReadsTypedProfileTransactionsHistoryReactionAndSemanticTags()
    {
        var catalog = FactionAdvancedTestData.Complete();
        var service = new FactionReputationScriptService(catalog);

        var snapshot = service.Read(service.Handle);
        var transaction = Assert.Single(service.Transactions(service.Handle, ReputationScope.Campaign, "ashes"));
        var history = Assert.Single(service.HeatmapHistory(service.Handle));
        var reaction = service.Reaction(service.Handle);
        var tagged = service.QueryByTags(service.Handle, ["guild"]);

        Assert.Equal(1, snapshot.TransactionCount);
        Assert.Equal(1, snapshot.HeatmapHistoryCount);
        Assert.True(snapshot.RequiredAtRuntime);
        Assert.Equal("rescued-merchant", transaction.Cause);
        Assert.Equal("before-truce", history.Label);
        Assert.True(reaction.AllowGameMasterOverride);
        Assert.Equal(2, tagged.Count);
        Assert.Contains(tagged, item => item.Id == catalog.Factions[0].Id);
        Assert.Contains(tagged, item => item.Id == catalog.Factions[1].Id);
    }

    [Fact]
    public void RejectsMissingInvalidAndStaleHandles()
    {
        Assert.Throws<ArgumentException>(() => new FactionReputationScriptService(FactionCatalog.Empty));
        var service = new FactionReputationScriptService(FactionAdvancedTestData.Complete());

        Assert.Throws<KeyNotFoundException>(() => service.Read(service.Handle with { SchemaVersion = 2 }));
    }
}
