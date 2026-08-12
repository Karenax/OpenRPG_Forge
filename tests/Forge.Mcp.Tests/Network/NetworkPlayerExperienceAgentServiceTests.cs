using Forge.Mcp.Network;
using Forge.Network.Authoring;
using Forge.Network.Operations;

namespace Forge.Mcp.Tests.Network;

public sealed class NetworkPlayerExperienceAgentServiceTests
{
    [Fact]
    public void R121_ResourceIsFilteredAndMutationUsesDryRunApplyAndAudit()
    {
        var initial = NetworkPlayerExperienceProfile.CreateDefault("mcp:test:r121");
        var service = new NetworkPlayerExperienceAgentService(initial);
        Assert.True(service.Get(initial.Id).Found);
        Assert.True(service.Get(search: "r121").Found);
        Assert.Equal("NET_PLAYER_EXPERIENCE_NOT_FOUND",
            Assert.Single(service.Get(Guid.NewGuid()).Diagnostics).Code);

        var candidate = initial with
        {
            ServerRevision = 1,
            Lobby = initial.Lobby with { MaximumSlots = 16 }
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
        Assert.Contains(applied.Audit, item => item.StartsWith("applied:network.player-experience", StringComparison.Ordinal));
        Assert.Equal(initial, service.Revert("agent:network", NetworkRole.Administrator).Profile);
    }

    [Fact]
    public void R121_AgentRejectsWrongScopePermissionAndUnknownPlan()
    {
        var initial = NetworkPlayerExperienceProfile.CreateDefault("mcp:test:r121");
        var service = new NetworkPlayerExperienceAgentService(initial);
        var candidate = initial with { ServerRevision = 1 };

        var denied = service.Plan(new(Guid.NewGuid(), 0, "player", NetworkRole.Player,
            (NetworkAuthorityScope)99, candidate));
        Assert.False(denied.Succeeded);
        Assert.Contains(denied.Diagnostics, item => item.Code == "NET_PLAYER_SCOPE_INVALID");
        Assert.False(service.Apply(Guid.NewGuid().ToString("D")).Succeeded);
    }
}
