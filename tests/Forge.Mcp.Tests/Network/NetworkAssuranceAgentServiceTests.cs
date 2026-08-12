using Forge.Mcp.Network;
using Forge.Network.Authoring;
using Forge.Network.Operations;

namespace Forge.Mcp.Tests.Network;

public sealed class NetworkAssuranceAgentServiceTests
{
    [Fact]
    public void Read_is_bounded_by_id_and_search()
    {
        var profile = NetworkAssuranceProfile.CreateDefault("mcp:test");
        var service = new NetworkAssuranceAgentService(profile);

        Assert.True(service.Get(profile.Id, "security").Found);
        Assert.False(service.Get(Guid.NewGuid(), null).Found);
        Assert.Equal("NET_ASSURANCE_NOT_FOUND", Assert.Single(service.Get(null, "missing").Diagnostics).Code);
    }

    [Fact]
    public void Plan_is_dry_run_and_apply_uses_the_previewed_revision()
    {
        var initial = NetworkAssuranceProfile.CreateDefault("mcp:test");
        var candidate = initial with
        {
            ServerRevision = 1,
            DeterministicTests = initial.DeterministicTests with { SoakCycles = 8 }
        };
        var service = new NetworkAssuranceAgentService(initial);
        var request = new NetworkAssurancePlanRequest(Guid.NewGuid(), 0, "agent",
            NetworkRole.Administrator, NetworkAuthorityScope.Server, candidate);

        var plan = service.Plan(request);
        var applied = service.Apply(plan.PlanId!);

        Assert.True(plan.Succeeded);
        Assert.False(plan.Applied);
        Assert.Contains("deterministic-tests", plan.SemanticDiff);
        Assert.True(applied.Applied);
        Assert.Equal(1, applied.Profile.ServerRevision);
        Assert.Contains(applied.Audit, item => item.StartsWith("applied:network.assurance", StringComparison.Ordinal));
    }

    [Fact]
    public void Scope_permission_and_unknown_plan_are_rejected_with_audit()
    {
        var initial = NetworkAssuranceProfile.CreateDefault("mcp:test");
        var service = new NetworkAssuranceAgentService(initial);
        var denied = service.Plan(new(Guid.NewGuid(), 0, "player", NetworkRole.Player,
            NetworkAuthorityScope.Server, initial with { ServerRevision = 1 }));
        var unknown = service.Apply(Guid.NewGuid().ToString("D"));

        Assert.Contains(denied.Diagnostics, item => item.Code == "NET_ASSURANCE_PERMISSION_DENIED");
        Assert.Equal("NET_ASSURANCE_PLAN_UNKNOWN", Assert.Single(unknown.Diagnostics).Code);
        Assert.NotEmpty(denied.Audit);
    }
}
