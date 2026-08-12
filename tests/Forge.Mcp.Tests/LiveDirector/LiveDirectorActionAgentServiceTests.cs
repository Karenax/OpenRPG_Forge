using Forge.LiveDirector.Authoring;
using Forge.Mcp.LiveDirector;
using Forge.Network.Authoring;
using Forge.Network.Operations;

namespace Forge.Mcp.Tests.LiveDirector;

public sealed class LiveDirectorActionAgentServiceTests
{
    [Fact]
    public void R126_FilterPlanApplyRevert_AreBounded()
    {
        var initial = LiveDirectorActionProfile.CreateDefault(); var service = new LiveDirectorActionAgentService(initial);
        Assert.True(service.Get(initial.Id, "heal").Found); Assert.False(service.Get(Guid.NewGuid()).Found);
        var candidate = initial with { ServerRevision = 1, Possession = initial.Possession with { MaximumMinutes = 90 } }; var id = Guid.NewGuid();
        Assert.True(service.Plan(new(id, 0, "gm:agent", NetworkRole.GameMaster, NetworkAuthorityScope.Server, candidate)).Succeeded);
        Assert.True(service.Apply(id.ToString("D")).Applied); Assert.Equal(initial, service.Revert("gm:agent", NetworkRole.GameMaster).Profile);
    }
    [Fact]
    public void R126_WrongScopeAndUnknownPlan_AreRejected()
    {
        var initial = LiveDirectorActionProfile.CreateDefault(); var service = new LiveDirectorActionAgentService(initial);
        Assert.Contains(service.Plan(new(Guid.NewGuid(), 0, "player", NetworkRole.Player, (NetworkAuthorityScope)99, initial with { ServerRevision = 1 })).Diagnostics, x => x.Code == "LIVE_ACTION_PROFILE_SCOPE_INVALID");
        Assert.False(service.Apply(Guid.NewGuid().ToString("D")).Succeeded);
    }
}
