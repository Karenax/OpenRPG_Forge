using Forge.LiveDirector.Scenarios;

namespace Forge.LiveDirector.Tests;

public sealed class LiveDirectorScenarioTests
{
    [Fact]
    public void Scenario_runs_on_network_beta_with_authority_audit_and_shared_progress()
    {
        var result = LiveDirectorScenario.Run();
        Assert.True(result.Passed);
        Assert.Equal("LIVE_SPAWN_APPLIED", result.SpawnCode);
        Assert.Equal("LIVE_QUEST_APPLIED", result.QuestCode);
        Assert.Equal("LIVE_SPAWN_BLUEPRINT_DENIED", result.RejectedPlayerCode);
        Assert.Equal(1, result.AuditEntries);
        Assert.NotEqual("none", result.AuditHash);
    }

    [Fact]
    public void Scenario_is_deterministic()
    {
        var first = LiveDirectorScenario.Run();
        var second = LiveDirectorScenario.Run();
        Assert.Equal(first.NetworkDigest, second.NetworkDigest);
        Assert.Equal(first.AuditHash, second.AuditHash);
    }
}
