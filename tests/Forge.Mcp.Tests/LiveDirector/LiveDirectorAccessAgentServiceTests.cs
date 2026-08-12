using Forge.LiveDirector.Authoring;
using Forge.Mcp.LiveDirector;
using Forge.Network.Authoring;
using Forge.Network.Operations;

namespace Forge.Mcp.Tests.LiveDirector;

public sealed class LiveDirectorAccessAgentServiceTests
{
    [Fact]
    public void Read_is_bounded_by_id_and_search()
    {
        var profile = LiveDirectorAccessProfile.CreateDefault("mcp:test");
        var service = new LiveDirectorAccessAgentService(profile);

        Assert.True(service.Get(profile.Id, "moderation").Found);
        Assert.False(service.Get(Guid.NewGuid(), null).Found);
        Assert.Equal("LIVE_ACCESS_NOT_FOUND", Assert.Single(service.Get(null, "missing").Diagnostics).Code);
    }

    [Fact]
    public void Plan_is_dry_run_and_apply_uses_the_previewed_revision()
    {
        var initial = LiveDirectorAccessProfile.CreateDefault("mcp:test");
        var candidate = initial with
        {
            ServerRevision = 1,
            Players = initial.Players with { RefreshSeconds = 3 }
        };
        var service = new LiveDirectorAccessAgentService(initial);
        var request = new LiveDirectorAccessPlanRequest(Guid.NewGuid(), 0, "agent",
            NetworkRole.Administrator, NetworkAuthorityScope.Server, candidate);

        var plan = service.Plan(request);
        var applied = service.Apply(plan.PlanId!);

        Assert.True(plan.Succeeded);
        Assert.False(plan.Applied);
        Assert.Contains("players", plan.SemanticDiff);
        Assert.True(applied.Applied);
        Assert.Equal(1, applied.Profile.ServerRevision);
        Assert.Contains(applied.Audit, item => item.StartsWith("applied:live.access", StringComparison.Ordinal));
    }

    [Fact]
    public void Scope_permission_and_unknown_plan_are_rejected_with_audit()
    {
        var initial = LiveDirectorAccessProfile.CreateDefault("mcp:test");
        var service = new LiveDirectorAccessAgentService(initial);
        var denied = service.Plan(new(Guid.NewGuid(), 0, "player", NetworkRole.Player,
            NetworkAuthorityScope.Server, initial with { ServerRevision = 1 }));
        var unknown = service.Apply(Guid.NewGuid().ToString("D"));

        Assert.Contains(denied.Diagnostics, item => item.Code == "LIVE_ACCESS_PERMISSION_DENIED");
        Assert.Equal("LIVE_ACCESS_PLAN_UNKNOWN", Assert.Single(unknown.Diagnostics).Code);
        Assert.NotEmpty(denied.Audit);
    }
}
