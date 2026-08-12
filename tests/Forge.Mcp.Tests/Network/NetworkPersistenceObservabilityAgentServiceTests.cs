using Forge.Mcp.Network;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Mcp.Tests.Network;

public sealed class NetworkPersistenceObservabilityAgentServiceTests
{
    [Fact]
    public void R123_FilteredResourceDryRunApplyRevertAndAuditShareCanonicalHistory()
    {
        var initial = NetworkPersistenceObservabilityProfile.CreateDefault("mcp:test:r123");
        var service = new NetworkPersistenceObservabilityAgentService(initial);
        Assert.True(service.Get(initial.Id).Found);
        Assert.True(service.Get(search: "server.tps").Found);
        Assert.Equal("NET_PERSIST_OBSERVE_NOT_FOUND", Assert.Single(service.Get(Guid.NewGuid()).Diagnostics).Code);
        var candidate = initial with
        {
            ServerRevision = 1,
            Persistence = initial.Persistence with { BackupRetentionDays = 45 }
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
    public void R123_AgentRejectsWrongScopePermissionAndUnknownPlan()
    {
        var initial = NetworkPersistenceObservabilityProfile.CreateDefault("mcp:test:r123");
        var service = new NetworkPersistenceObservabilityAgentService(initial);
        var denied = service.Plan(new(Guid.NewGuid(), 0, "player", NetworkRole.Player,
            (NetworkAuthorityScope)99, initial with { ServerRevision = 1 }));
        Assert.Contains(denied.Diagnostics, item => item.Code == "NET_PERSIST_OBSERVE_SCOPE_INVALID");
        Assert.False(service.Apply(Guid.NewGuid().ToString("D")).Succeeded);
    }
}
