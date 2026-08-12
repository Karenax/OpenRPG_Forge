using Forge.Domain.Assistant;

namespace Forge.Mcp.Tests.Testing;

public sealed class AssistantTestAutomationTests
{
    [Fact]
    public void R120_McpTestAutomationIsBoundedStructuredAndShellFree()
    {
        using var workspace = new TestWorkspace();

        var report = workspace.Project.RunAssistantTestAutomation();

        Assert.Equal(AssistantTestAutomationProfile.CurrentSchemaVersion, report.SchemaVersion);
        Assert.NotEqual(Guid.Empty, report.ProfileId);
        Assert.Equal(64, report.Fingerprint.Length);
        Assert.All(report.GameplayActions,
            action => Assert.Contains(action, AssistantTestAutomationProfile.SupportedGameplayActions));
        Assert.DoesNotContain(report.GameplayActions, action => action.Contains("shell", StringComparison.Ordinal));
        Assert.All(report.Findings, finding =>
        {
            Assert.NotEqual(Guid.Empty, finding.Id);
            Assert.False(string.IsNullOrWhiteSpace(finding.Code));
            Assert.False(string.IsNullOrWhiteSpace(finding.DocumentPath));
            Assert.NotEmpty(finding.ReproductionSteps);
        });
    }
}
