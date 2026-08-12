using Forge.Mcp.Network;
using Forge.Network.Authoring;
using Forge.Network.Operations;

namespace Forge.Mcp.Tests.Network;

public sealed class NetworkOperationsAgentServiceTests
{
    [Fact]
    public void Get_is_bounded_by_id_and_search()
    {
        var profile = NetworkOperationsProfile.CreateDefault("mcp:test");
        var service = new NetworkOperationsAgentService(profile);

        Assert.True(service.Get(profile.Id, "crypt").Found);
        Assert.False(service.Get(Guid.NewGuid(), null).Found);
        Assert.Equal("NET_OPERATIONS_NOT_FOUND", Assert.Single(service.Get(null, "missing").Diagnostics).Code);
    }

    [Fact]
    public void Plan_is_dry_run_then_apply_is_revisioned_and_audited()
    {
        var initial = NetworkOperationsProfile.CreateDefault("mcp:test");
        var candidate = initial with { ServerRevision = 1, Chat = initial.Chat with { LocalRadius = 55 } };
        var service = new NetworkOperationsAgentService(initial);
        var request = new NetworkOperationsPlanRequest(Guid.NewGuid(), 0, "agent", NetworkRole.Administrator,
            NetworkAuthorityScope.Server, candidate);

        var planned = service.Plan(request);
        var applied = service.Apply(planned.PlanId!);

        Assert.True(planned.Succeeded);
        Assert.False(planned.Applied);
        Assert.True(applied.Applied);
        Assert.Equal(1, applied.Profile.ServerRevision);
        Assert.Contains(applied.Audit, item => item.StartsWith("previewed:", StringComparison.Ordinal));
        Assert.Contains(applied.Audit, item => item.StartsWith("applied:", StringComparison.Ordinal));
    }

    [Fact]
    public void Scope_permission_and_history_are_enforced()
    {
        var initial = NetworkOperationsProfile.CreateDefault("mcp:test");
        var service = new NetworkOperationsAgentService(initial);
        var candidate = initial with { ServerRevision = 1 };

        var wrongScope = service.Plan(new(Guid.NewGuid(), 0, "agent", NetworkRole.Administrator,
            (NetworkAuthorityScope)99, candidate));
        var denied = service.Plan(new(Guid.NewGuid(), 0, "agent", NetworkRole.Player,
            NetworkAuthorityScope.Server, candidate));

        Assert.Equal("NET_OPERATIONS_SCOPE_INVALID", Assert.Single(wrongScope.Diagnostics).Code);
        Assert.Equal("NET_OPERATIONS_PERMISSION_DENIED", Assert.Single(denied.Diagnostics).Code);
        Assert.False(service.Revert("agent", NetworkRole.Player).Succeeded);
    }
}
