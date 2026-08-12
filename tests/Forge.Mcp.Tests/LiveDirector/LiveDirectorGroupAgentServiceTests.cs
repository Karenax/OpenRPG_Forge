using Forge.LiveDirector.Authoring;
using Forge.Mcp.LiveDirector;
using Forge.Network.Authoring;
using Forge.Network.Operations;
namespace Forge.Mcp.Tests.LiveDirector;

public sealed class LiveDirectorGroupAgentServiceTests
{
    [Fact] public void R128_FilterPlanApplyRevert_AreBounded() { var i = LiveDirectorGroupProfile.CreateDefault(); var s = new LiveDirectorGroupAgentService(i); Assert.True(s.Get(i.Id, "attack").Found); Assert.False(s.Get(Guid.NewGuid()).Found); var c = i with { ServerRevision = 1, SelectionCap = 96 }; var id = Guid.NewGuid(); Assert.True(s.Plan(new(id, 0, "gm:agent", NetworkRole.GameMaster, NetworkAuthorityScope.Server, c)).Succeeded); Assert.True(s.Apply(id.ToString("D")).Applied); Assert.Equal(i, s.Revert("gm:agent", NetworkRole.GameMaster).Profile); }
    [Fact] public void R128_WrongScopeAndUnknownPlan_AreRejected() { var i = LiveDirectorGroupProfile.CreateDefault(); var s = new LiveDirectorGroupAgentService(i); Assert.Contains(s.Plan(new(Guid.NewGuid(), 0, "player", NetworkRole.Player, (NetworkAuthorityScope)99, i with { ServerRevision = 1 })).Diagnostics, x => x.Code == "LIVE_GROUP_SCOPE_INVALID"); Assert.False(s.Apply(Guid.NewGuid().ToString("D")).Succeeded); }
}
