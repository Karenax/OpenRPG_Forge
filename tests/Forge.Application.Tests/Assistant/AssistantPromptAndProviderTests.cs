using Forge.Application.Assistant;
using Forge.Domain.Assistant;

namespace Forge.Application.Tests.Assistant;

public sealed class AssistantPromptAndProviderTests
{
    [Fact]
    public async Task VersionedPromptIsDeterministicAndOfflineProviderReturnsPlanOnly()
    {
        var profile = AssistantWorkspaceProfile.CreateDefault();
        var prompt = profile.Prompts[0];
        var variables = prompt.Tests[0].Variables;

        var first = AssistantPromptRenderer.Render(prompt, variables);
        var second = AssistantPromptRenderer.Render(prompt, variables);
        var report = AssistantPromptTestRunner.Run(prompt);
        var provider = new AssistantProviderRegistry().Resolve(profile.ActiveProvider);
        var response = await provider.GenerateAsync(new AssistantProviderRequest(
            Guid.Parse("a1230000-0000-4000-8000-000000000001"),
            Guid.Parse("a1230000-0000-4000-8000-000000000002"),
            first,
            AssistantPermissionLevel.Propose,
            256));

        Assert.Equal(first.ContentSha256, second.ContentSha256);
        Assert.Equal(64, first.ContentSha256.Length);
        Assert.True(report.Passed);
        Assert.Contains("semanticDiff", response.Content, StringComparison.Ordinal);
        Assert.Contains("planId", response.Content, StringComparison.Ordinal);
        Assert.Equal(0, response.EstimatedCost);
        Assert.Contains("deterministic-offline", response.Provenance, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingPromptVariableAndUnavailableProviderAreExplicit()
    {
        var profile = AssistantWorkspaceProfile.CreateDefault();
        Assert.Throws<ArgumentException>(() =>
            AssistantPromptRenderer.Render(profile.Prompts[0], new Dictionary<string, string>()));

        var remote = new AssistantProviderDefinition(
            Guid.NewGuid(), 1, "vllm", "vLLM", AssistantProviderKind.OpenAiCompatible,
            "http://127.0.0.1:8000/v1", "model", string.Empty, true, true,
            4096, 1024, 0, 0, "test:g12-a");
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new AssistantProviderRegistry().Resolve(remote));
        Assert.Contains(AssistantProviderRegistry.ProviderUnavailableCode, exception.Message, StringComparison.Ordinal);
    }
}
