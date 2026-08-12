using Forge.LiveDirector.Authoring;
using Forge.Mcp.LiveDirector;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Mcp.Tests.LiveDirector;

public sealed class LiveDirectorWorkspaceAgentServiceTests
{
    [Fact]
    public void R125_FilterPlanApplyRevertAndAudit_AreBounded()
    {
        var initial = LiveDirectorWorkspaceProfile.CreateDefault("mcp:test:r125");
        var service = new LiveDirectorWorkspaceAgentService(initial);
        Assert.True(service.Get(initial.Id, "creator").Found);
        Assert.False(service.Get(Guid.NewGuid()).Found);
        var candidate = initial with { ServerRevision = 1, Spawn = initial.Spawn with { MaximumInstances = 80 } };
        var id = Guid.NewGuid();
        Assert.True(service.Plan(new(id, 0, "gm:agent", NetworkRole.GameMaster, NetworkAuthorityScope.Server, candidate)).Succeeded);
        Assert.True(service.Apply(id.ToString("D")).Applied);
        Assert.Equal(initial, service.Revert("gm:agent", NetworkRole.GameMaster).Profile);
    }
    [Fact]
    public void R125_WrongScopeAndUnknownPlan_AreRejected()
    {
        var initial = LiveDirectorWorkspaceProfile.CreateDefault();
        var service = new LiveDirectorWorkspaceAgentService(initial);
        Assert.Contains(service.Plan(new(Guid.NewGuid(), 0, "player", NetworkRole.Player, (NetworkAuthorityScope)99, initial with { ServerRevision = 1 })).Diagnostics, x => x.Code == "LIVE_WORKSPACE_SCOPE_INVALID");
        Assert.False(service.Apply(Guid.NewGuid().ToString("D")).Succeeded);
    }
}
