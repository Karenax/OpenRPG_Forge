using Forge.Domain.Assistant;

namespace Forge.Domain.Tests.Assistant;

public sealed class AssistedAreaPlanTests
{
    [Fact]
    public void ValidatorAcceptsBoundedConnectedTopology()
    {
        var plan = Plan(connected: true);
        var catalog = new AssistedAreaPlanCatalog(Guid.NewGuid(), 1, [plan], "test:g12-c");

        Assert.Empty(AssistedAreaPlanValidator.Validate(catalog));
    }

    [Fact]
    public void ValidatorRejectsDisconnectedTopologyWithStablePath()
    {
        var plan = Plan(connected: false);
        var catalog = new AssistedAreaPlanCatalog(Guid.NewGuid(), 1, [plan], "test:g12-c");

        var diagnostic = Assert.Single(AssistedAreaPlanValidator.Validate(catalog),
            item => item.Code == AssistedAreaPlanValidator.TopologyInvalidCode);
        Assert.Contains(plan.Id.ToString("D"), diagnostic.Path, StringComparison.Ordinal);
    }

    private static AssistedAreaPlanDefinition Plan(bool connected)
    {
        var first = new AssistedAreaPlanNode(Guid.NewGuid(), 1, AssistedAreaPlanNodeKind.Entry,
            "Entrée", 0, 0, 8, 8, [], ["entry"]);
        var second = new AssistedAreaPlanNode(Guid.NewGuid(), 1, AssistedAreaPlanNodeKind.Exit,
            "Sortie", 12, 0, 8, 8, [], ["exit"]);
        return new AssistedAreaPlanDefinition(
            Guid.NewGuid(), 1, Guid.NewGuid(), "Plan borné", AssistedAreaArchetype.Crypt, 42, 32, 32,
            [Guid.NewGuid()], ["Keep paths navigable."], ["Reach the exit."], [first, second],
            connected ? [new AssistedAreaPlanLink(Guid.NewGuid(), 1, first.Id, second.Id, "door", true)] : [],
            ["manual:test-lore"], false, 0, "offline-ci", "test:g12-c");
    }
}
