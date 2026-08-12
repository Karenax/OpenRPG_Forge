using Forge.Mcp.Network;
using Forge.Network.Authoring;
using Forge.Network.Operations;

namespace Forge.Mcp.Tests.Network;

public sealed class NetworkFoundationAgentServiceTests
{
    [Fact]
    public void Resource_is_filtered_by_stable_id_or_bounded_search()
    {
        var profile = NetworkFoundationProfile.CreateDefault("mcp:test:r102");
        var service = new NetworkFoundationAgentService(profile);
        Assert.True(service.Get(profile.Id).Found);
        Assert.True(service.Get(search: "OpenRPG").Found);
        var missing = service.Get(Guid.NewGuid());
        Assert.False(missing.Found);
        Assert.Equal("NET_FOUNDATION_NOT_FOUND", Assert.Single(missing.Diagnostics).Code);
    }

    [Fact]
    public void Plan_apply_revert_and_redo_share_one_transactional_history()
    {
        var initial = NetworkFoundationProfile.CreateDefault("mcp:test:r102");
        var service = new NetworkFoundationAgentService(initial);
        var candidate = initial with
        {
            ServerRevision = 1,
            Server = initial.Server with { Capacity = 12 },
            Party = initial.Party with { MaximumMembers = 6 }
        };
        var planId = Guid.NewGuid();
        var preview = service.Plan(new(planId, 0, "agent:network", NetworkRole.Administrator,
            NetworkAuthorityScope.Server, candidate));
        Assert.True(preview.Succeeded);
        Assert.False(preview.Applied);
        Assert.Equal(initial, preview.Profile);
        var applied = service.Apply(planId.ToString("D"));
        Assert.True(applied.Applied);
        Assert.Equal(candidate, applied.Profile);
        Assert.Equal(initial, service.Revert("agent:network", NetworkRole.Administrator).Profile);
        Assert.Equal(candidate, service.Redo("agent:network", NetworkRole.Administrator).Profile);
        Assert.Contains(applied.Audit, entry => entry.StartsWith("applied:network.foundation", StringComparison.Ordinal));
    }

    [Fact]
    public void Agent_requires_identity_admin_server_scope_and_fresh_revision()
    {
        var initial = NetworkFoundationProfile.CreateDefault("mcp:test:r102");
        var service = new NetworkFoundationAgentService(initial);
        var candidate = initial with { ServerRevision = 1 };
        var denied = service.Plan(new(Guid.NewGuid(), 9, "", NetworkRole.Player,
            (NetworkAuthorityScope)99, candidate));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Diagnostics, item => item.Code == "NET_AUTHORITY_SCOPE_INVALID");
        Assert.False(service.Apply(Guid.NewGuid().ToString("D")).Succeeded);
    }
}
