using System.Net;
using System.Text;
using Forge.Application.Assistant;
using Forge.Domain.Assistant;

namespace Forge.Application.Tests.Assistant;

public sealed class AssistantProviderTransportTests
{
    [Fact]
    public async Task OpenAiCompatibleProviderUsesGovernedLocalEndpointSecretAndUsage()
    {
        var handler = new RecordingHandler(
            """
            {"choices":[{"message":{"content":"{\"summary\":\"ok\",\"semanticDiff\":[]}"}}],"usage":{"prompt_tokens":120,"completion_tokens":40}}
            """);
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var definition = Provider(
            AssistantProviderKind.OpenAiCompatible,
            "http://127.0.0.1:1234/v1",
            "LOCAL_ASSISTANT_KEY",
            2m,
            4m);
        var provider = AssistantProviderRegistry.CreateWithLocalTransports(
            client,
            name => name == "LOCAL_ASSISTANT_KEY" ? "test-secret" : null,
            TimeSpan.FromSeconds(2)).Resolve(definition);

        var response = await provider.GenerateAsync(Request(networkAuthorized: true));

        Assert.Equal("http://127.0.0.1:1234/v1/chat/completions", handler.RequestUri?.AbsoluteUri);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-secret", handler.AuthorizationParameter);
        Assert.Contains("response_format", handler.RequestBody, StringComparison.Ordinal);
        Assert.Equal(120, response.InputTokens);
        Assert.Equal(40, response.OutputTokens);
        Assert.Equal(0.0004m, response.EstimatedCost);
        Assert.Contains("openai-compatible", response.Provenance, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OllamaProviderUsesNativeChatContractWithoutCredential()
    {
        var handler = new RecordingHandler(
            """
            {"message":{"role":"assistant","content":"{\"summary\":\"local\",\"semanticDiff\":[]}"},"prompt_eval_count":90,"eval_count":30}
            """);
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var provider = AssistantProviderRegistry.CreateWithLocalTransports(
            client,
            _ => null,
            TimeSpan.FromSeconds(2)).Resolve(Provider(
                AssistantProviderKind.Ollama,
                "http://localhost:11434",
                string.Empty,
                0,
                0));

        var response = await provider.GenerateAsync(Request(networkAuthorized: true));

        Assert.Equal("http://localhost:11434/api/chat", handler.RequestUri?.AbsoluteUri);
        Assert.Null(handler.AuthorizationScheme);
        Assert.Contains("num_predict", handler.RequestBody, StringComparison.Ordinal);
        Assert.Equal(90, response.InputTokens);
        Assert.Equal(30, response.OutputTokens);
        Assert.Equal(0, response.EstimatedCost);
        Assert.Contains("ollama", response.Provenance, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NetworkAndCredentialPoliciesFailBeforeSendingContent()
    {
        var handler = new RecordingHandler("{}");
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var registry = AssistantProviderRegistry.CreateWithLocalTransports(
            client,
            _ => null,
            TimeSpan.FromSeconds(2));
        var provider = registry.Resolve(Provider(
            AssistantProviderKind.OpenAiCompatible,
            "http://127.0.0.1:8000/v1",
            "MISSING_KEY",
            0,
            0));

        var denied = await Assert.ThrowsAsync<AssistantProviderTransportException>(() =>
            provider.GenerateAsync(Request(networkAuthorized: false)));
        var missing = await Assert.ThrowsAsync<AssistantProviderTransportException>(() =>
            provider.GenerateAsync(Request(networkAuthorized: true)));

        Assert.Equal("assistant.provider.network_denied", denied.Code);
        Assert.Equal("assistant.provider.credential_missing", missing.Code);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task InvalidResponseHasStableDiagnostic()
    {
        var handler = new RecordingHandler("{\"choices\":[]}");
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var provider = AssistantProviderRegistry.CreateWithLocalTransports(
            client,
            _ => null,
            TimeSpan.FromSeconds(2)).Resolve(Provider(
                AssistantProviderKind.OpenAiCompatible,
                "http://127.0.0.1:8000/v1",
                string.Empty,
                0,
                0));

        var exception = await Assert.ThrowsAsync<AssistantProviderTransportException>(() =>
            provider.GenerateAsync(Request(networkAuthorized: true)));

        Assert.Equal("assistant.provider.response_invalid", exception.Code);
    }

    [Fact]
    public async Task TimeoutAndCallerCancellationRemainBoundedAndDistinct()
    {
        using var timeoutClient = new HttpClient(new BlockingHandler()) { Timeout = Timeout.InfiniteTimeSpan };
        var timeoutProvider = AssistantProviderRegistry.CreateWithLocalTransports(
            timeoutClient,
            _ => null,
            TimeSpan.FromMilliseconds(40)).Resolve(Provider(
                AssistantProviderKind.Ollama,
                "http://127.0.0.1:11434",
                string.Empty,
                0,
                0));
        var timedOut = await Assert.ThrowsAsync<AssistantProviderTransportException>(() =>
            timeoutProvider.GenerateAsync(Request(networkAuthorized: true)));

        using var cancellationClient = new HttpClient(new BlockingHandler()) { Timeout = Timeout.InfiniteTimeSpan };
        var cancellationProvider = AssistantProviderRegistry.CreateWithLocalTransports(
            cancellationClient,
            _ => null,
            TimeSpan.FromSeconds(2)).Resolve(Provider(
                AssistantProviderKind.Ollama,
                "http://127.0.0.1:11434",
                string.Empty,
                0,
                0));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(40));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cancellationProvider.GenerateAsync(Request(networkAuthorized: true), cancellation.Token));
        Assert.Equal("assistant.provider.request_failed", timedOut.Code);
    }

    private static AssistantProviderDefinition Provider(
        AssistantProviderKind kind,
        string endpoint,
        string credential,
        decimal inputCost,
        decimal outputCost) =>
        new(
            Guid.Parse("b1200000-0000-4000-8000-000000000001"),
            1,
            kind == AssistantProviderKind.Ollama ? "ollama-local" : "openai-local",
            kind.ToString(),
            kind,
            endpoint,
            "forge-test",
            credential,
            true,
            true,
            8192,
            1024,
            inputCost,
            outputCost,
            "test:g12-b");

    private static AssistantProviderRequest Request(bool networkAuthorized)
    {
        var prompt = new AssistantPromptRenderResult(
            "creator-plan",
            "1.0.0",
            "Return JSON only.",
            "Prepare a bounded plan.",
            "{\"type\":\"object\"}",
            new string('a', 64));
        return new AssistantProviderRequest(
            Guid.Parse("b1200000-0000-4000-8000-000000000002"),
            Guid.Parse("b1200000-0000-4000-8000-000000000003"),
            prompt,
            AssistantPermissionLevel.Propose,
            512,
            networkAuthorized,
            ProjectContentAuthorized: true);
    }

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Cancellation was expected.");
        }
    }
}
