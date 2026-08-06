using Forge.Domain.Assistant;

namespace Forge.Domain.Tests.Assistant;

public sealed class AssistantWorkspaceProfileTests
{
    [Fact]
    public void DefaultProfileIsOfflineGovernedAndValid()
    {
        var profile = AssistantWorkspaceProfile.CreateDefault();

        Assert.Empty(AssistantWorkspaceProfileValidator.Validate(profile));
        Assert.Equal(AssistantProviderKind.DeterministicOffline, profile.ActiveProvider.Kind);
        Assert.False(profile.NetworkAllowed);
        Assert.False(profile.ShareProjectContent);
        Assert.Equal(AssistantPermissionLevel.Configure, profile.MaximumPermission);
        Assert.Single(profile.Prompts);
        Assert.Single(profile.Prompts[0].Tests);
    }

    [Fact]
    public void OpenAiCompatibleAndOllamaProvidersShareTheCanonicalContract()
    {
        var baseline = AssistantWorkspaceProfile.CreateDefault();
        var openAiCompatible = Provider(
            Guid.Parse("a1210000-0000-4000-8000-000000000001"),
            "lm-studio",
            AssistantProviderKind.OpenAiCompatible,
            "http://127.0.0.1:1234/v1");
        var ollama = Provider(
            Guid.Parse("a1210000-0000-4000-8000-000000000002"),
            "ollama",
            AssistantProviderKind.Ollama,
            "http://localhost:11434");
        var profile = new AssistantWorkspaceProfile(
            baseline.Id,
            baseline.SchemaVersion,
            openAiCompatible.Id,
            [openAiCompatible, ollama],
            baseline.Prompts,
            baseline.MaximumPermission,
            true,
            false,
            "test:g12-a");

        Assert.Empty(AssistantWorkspaceProfileValidator.Validate(profile));
        Assert.Equal("lm-studio", profile.ActiveProvider.Key);
    }

    [Fact]
    public void InvalidNetworkPolicyAndDuplicateProvidersReturnStableDiagnostics()
    {
        var baseline = AssistantWorkspaceProfile.CreateDefault();
        var network = Provider(
            Guid.Parse("a1210000-0000-4000-8000-000000000003"),
            "vllm",
            AssistantProviderKind.OpenAiCompatible,
            "http://127.0.0.1:8000/v1");
        var profile = new AssistantWorkspaceProfile(
            baseline.Id,
            baseline.SchemaVersion,
            network.Id,
            [network, network],
            baseline.Prompts,
            baseline.MaximumPermission,
            false,
            false,
            "test:g12-a");

        var codes = AssistantWorkspaceProfileValidator.Validate(profile).Select(item => item.Code).ToArray();

        Assert.Contains(AssistantWorkspaceProfileValidator.ProviderInvalidCode, codes);
        Assert.Contains(AssistantWorkspaceProfileValidator.NetworkPolicyInvalidCode, codes);
    }

    private static AssistantProviderDefinition Provider(
        Guid id,
        string key,
        AssistantProviderKind kind,
        string endpoint) =>
        new(id, 1, key, key, kind, endpoint, "local-model", string.Empty, true, true,
            8192, 2048, 0, 0, "test:g12-a");
}
