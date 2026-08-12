using Forge.Mcp.Network;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Mcp.Tests.Network;

public sealed class NetworkResilienceCapacityAgentServiceTests
{
    [Fact]
    public void R124_FilterPlanApplyRevertAndAudit_AreBounded()
    {
        var initial = NetworkResilienceCapacityProfile.CreateDefault("mcp:test:r124");
        var service = new NetworkResilienceCapacityAgentService(initial);
        Assert.True(service.Get(initial.Id, "backup").Found);
        Assert.False(service.Get(Guid.NewGuid()).Found);
        var candidate = initial with { ServerRevision = 1, Capacity = initial.Capacity with { MaximumPlayers = 96 } };
        var id = Guid.NewGuid();
        Assert.True(service.Plan(new(id, 0, "agent:operator", NetworkRole.Administrator, NetworkAuthorityScope.Server, candidate)).Succeeded);
        Assert.True(service.Apply(id.ToString("D")).Applied);
        Assert.Equal(initial, service.Revert("agent:operator", NetworkRole.Administrator).Profile);
    }

    [Fact]
    public void R124_AgentRejectsWrongScopeAndUnknownPlan()
    {
        var initial = NetworkResilienceCapacityProfile.CreateDefault();
        var service = new NetworkResilienceCapacityAgentService(initial);
        var result = service.Plan(new(Guid.NewGuid(), 0, "player", NetworkRole.Player, (NetworkAuthorityScope)99, initial with { ServerRevision = 1 }));
        Assert.Contains(result.Diagnostics, item => item.Code == "NET_RESILIENCE_SCOPE_INVALID");
        Assert.False(service.Apply(Guid.NewGuid().ToString("D")).Succeeded);
    }
}
