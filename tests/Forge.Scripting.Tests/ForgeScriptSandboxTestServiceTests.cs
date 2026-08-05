namespace Forge.Scripting.Tests;

public sealed class ForgeScriptSandboxTestServiceTests
{
    private readonly ForgeScriptSandboxTestService _service = new(new NativeForgeScriptRuntime());

    [Fact]
    public void Sandbox_compiles_executes_with_quotas_mock_world_events_and_coverage()
    {
        var test = new ForgeScriptSandboxTestCase("quest", "scripts/quest.forge",
            "void main() { variable(\"quest.state\", \"active\"); say(\"Ready\"); quest(\"quality\"); }",
            new ForgeScriptQuotas(1_000, 100, 8, 2),
            new ForgeScriptMockWorld(new Dictionary<string, object?> { ["hero"] = 42 },
                new Dictionary<string, object?> { ["clock"] = "mock" },
                new HashSet<string>(["variable", "say", "quest"])),
            new HashSet<string>(["variable", "quest"]));

        var result = _service.Run(test);

        Assert.True(result.Passed);
        Assert.True(result.Compiled);
        Assert.Equal(1, result.EventCoverage);
        Assert.Equal(["quest", "say", "variable"], result.CoveredEventKinds);
        Assert.Equal(3, result.MockWorldCalls.Count);
    }

    [Fact]
    public void Sandbox_reports_quota_failure_without_poisoning_the_next_test()
    {
        var mock = new ForgeScriptMockWorld(new Dictionary<string, object?>(),
            new Dictionary<string, object?>(), new HashSet<string>(["say"]));
        var endless = new ForgeScriptSandboxTestCase("endless", "scripts/endless.forge",
            "void main() { while (true) { say(\"loop\"); } }", new ForgeScriptQuotas(8, 1_000), mock,
            new HashSet<string>());
        var healthy = endless with { Id = "healthy", SourcePath = "scripts/healthy.forge", Source = "void main() { say(\"ok\"); }" };

        var report = _service.RunSuite([endless, healthy]);

        Assert.False(report.Passed);
        Assert.Contains(report.Tests, test => test.Id == "endless" && test.Status == ForgeScriptExecutionStatus.QuotaExceeded);
        Assert.Contains(report.Tests, test => test.Id == "healthy" && test.Passed);
    }
}
