using Forge.Mcp.Network;
using Forge.Network.Authoring;
using Forge.Network.Operations;

namespace Forge.Mcp.Tests.Network;

public sealed class NetworkWorldAdministrationAgentServiceTests
{
    [Fact]
    public void R122_FilteredResourceDryRunApplyRevertAndAuditShareCanonicalHistory()
    {
        var initial = NetworkWorldAdministrationProfile.CreateDefault("mcp:test:r122");
        var service = new NetworkWorldAdministrationAgentService(initial);
        Assert.True(service.Get(initial.Id).Found);
        Assert.True(service.Get(search: "crypt-template").Found);
        Assert.Equal("NET_WORLD_ADMIN_NOT_FOUND", Assert.Single(service.Get(Guid.NewGuid()).Diagnostics).Code);

        var candidate = initial with
        {
            ServerRevision = 1,
            Instances = initial.Instances with { MaximumInstances = 40 }
        };
        var planId = Guid.NewGuid();
        var preview = service.Plan(new(planId, 0, "agent:operator", NetworkRole.Administrator,
            NetworkAuthorityScope.Server, candidate));
        Assert.True(preview.Succeeded);
        Assert.False(preview.Applied);
        Assert.True(service.Apply(planId.ToString("D")).Applied);
        Assert.Equal(initial, service.Revert("agent:operator", NetworkRole.Administrator).Profile);
    }

    [Fact]
    public void R122_AgentRejectsWrongScopePermissionAndUnknownPlan()
    {
        var initial = NetworkWorldAdministrationProfile.CreateDefault("mcp:test:r122");
        var service = new NetworkWorldAdministrationAgentService(initial);
        var denied = service.Plan(new(Guid.NewGuid(), 0, "player", NetworkRole.Player,
            (NetworkAuthorityScope)99, initial with { ServerRevision = 1 }));
        Assert.Contains(denied.Diagnostics, item => item.Code == "NET_WORLD_ADMIN_SCOPE_INVALID");
        Assert.False(service.Apply(Guid.NewGuid().ToString("D")).Succeeded);
    }
}
