using Forge.LiveDirector.Authoring;
using Forge.Mcp.LiveDirector;
using Forge.Network.Authoring;
using Forge.Network.Operations;
namespace Forge.Mcp.Tests.LiveDirector;

public sealed class LiveDirectorOperationsAgentServiceTests { [Fact] public void R129_FilterPlanApplyRevert() { var i = LiveDirectorOperationsProfile.CreateDefault(); var s = new LiveDirectorOperationsAgentService(i); Assert.True(s.Get(i.Id, "scene").Found); var c = i with { ServerRevision = 1, NextMatch = true }; var id = Guid.NewGuid(); Assert.True(s.Plan(new(id, 0, "gm", NetworkRole.GameMaster, NetworkAuthorityScope.Server, c)).Succeeded); Assert.True(s.Apply(id.ToString("D")).Applied); Assert.Equal(i, s.Revert("gm", NetworkRole.GameMaster).Profile); } [Fact] public void R129_ScopeAndUnknownRejected() { var i = LiveDirectorOperationsProfile.CreateDefault(); var s = new LiveDirectorOperationsAgentService(i); Assert.Contains(s.Plan(new(Guid.NewGuid(), 0, "p", NetworkRole.Player, (NetworkAuthorityScope)99, i with { ServerRevision = 1 })).Diagnostics, x => x.Code == "LIVE_OPS_SCOPE_INVALID"); Assert.False(s.Apply(Guid.NewGuid().ToString("D")).Succeeded); } }
