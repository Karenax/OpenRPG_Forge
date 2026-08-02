using Forge.Mcp.Testing;

namespace Forge.Mcp.Tests.Testing;

public sealed class ScenarioTestAgentTests
{
    [Fact]
    public async Task LaunchesScenarioAndWritesStructuredArtifact()
    {
        var artifact = Path.Combine(Path.GetTempPath(), "openrpg-forge-scenario-tests", Guid.NewGuid().ToString("N"), "dotnet.log");
        var agent = new ScenarioTestAgent();

        var result = await agent.RunAsync(new ScenarioTestRequest(
            "dotnet",
            ["--version"],
            Environment.CurrentDirectory,
            ["10.0."],
            TimeSpan.FromSeconds(10),
            artifact));

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["10.0."], result.FoundMarkers);
        Assert.Empty(result.MissingMarkers);
        Assert.Empty(result.Diagnostics);
        Assert.True(File.Exists(result.ArtifactLogPath));
        Assert.Contains("10.0.", await File.ReadAllTextAsync(result.ArtifactLogPath));
    }

    [Fact]
    public async Task ReportsMissingMarkerAsDiagnostic()
    {
        var artifact = Path.Combine(Path.GetTempPath(), "openrpg-forge-scenario-tests", Guid.NewGuid().ToString("N"), "missing.log");
        var result = await new ScenarioTestAgent().RunAsync(new ScenarioTestRequest(
            "dotnet",
            ["--version"],
            Environment.CurrentDirectory,
            ["marker-that-does-not-exist"],
            TimeSpan.FromSeconds(10),
            artifact));

        Assert.False(result.Succeeded);
        Assert.Equal("scenario.marker_missing", Assert.Single(result.Diagnostics).Code);
    }
}
