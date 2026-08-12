using Forge.LiveDirector.Authoring;
using Forge.Mcp.LiveDirector;
using Forge.Network.Authoring;
using Forge.Network.Operations;

namespace Forge.Mcp.Tests.LiveDirector;

public sealed class LiveDirectorAuthorityAgentServiceTests
{
    [Fact] public void R127_FilterPlanApplyRevert_AreBounded() { var i = LiveDirectorAuthorityProfile.CreateDefault(); var s = new LiveDirectorAuthorityAgentService(i); Assert.True(s.Get(i.Id, "door").Found); Assert.False(s.Get(Guid.NewGuid()).Found); var c = i with { ServerRevision = 1, MaximumCurrencyAdjustment = 120_000 }; var id = Guid.NewGuid(); Assert.True(s.Plan(new(id, 0, "gm:agent", NetworkRole.GameMaster, NetworkAuthorityScope.Server, c)).Succeeded); Assert.True(s.Apply(id.ToString("D")).Applied); Assert.Equal(i, s.Revert("gm:agent", NetworkRole.GameMaster).Profile); }
    [Fact] public void R127_WrongScopeAndUnknownPlan_AreRejected() { var i = LiveDirectorAuthorityProfile.CreateDefault(); var s = new LiveDirectorAuthorityAgentService(i); Assert.Contains(s.Plan(new(Guid.NewGuid(), 0, "player", NetworkRole.Player, (NetworkAuthorityScope)99, i with { ServerRevision = 1 })).Diagnostics, x => x.Code == "LIVE_AUTHORITY_SCOPE_INVALID"); Assert.False(s.Apply(Guid.NewGuid().ToString("D")).Succeeded); }
}
