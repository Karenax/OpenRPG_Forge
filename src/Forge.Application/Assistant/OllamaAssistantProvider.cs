using System.Text.Json;
using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed class OllamaAssistantProvider : IAssistantProvider
{
    private readonly AssistantProviderDefinition _definition;
    private readonly HttpClient _httpClient;
    private readonly Func<string, string?> _readEnvironmentVariable;
    private readonly TimeSpan _requestTimeout;

    public OllamaAssistantProvider(
        AssistantProviderDefinition definition,
        HttpClient httpClient,
        Func<string, string?> readEnvironmentVariable,
        TimeSpan requestTimeout)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _readEnvironmentVariable = readEnvironmentVariable ?? throw new ArgumentNullException(nameof(readEnvironmentVariable));
        _requestTimeout = requestTimeout;
        if (definition.Kind != AssistantProviderKind.Ollama)
        {
            throw new ArgumentException("Ollama provider requires the matching provider kind.", nameof(definition));
        }
    }

    public async Task<AssistantProviderResponse> GenerateAsync(
        AssistantProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var endpoint = AssistantProviderTransport.AppendEndpoint(_definition.Endpoint, "/api/chat");
        var payload = new
        {
            model = _definition.Model,
            messages = new[]
            {
                new { role = "system", content = request.Prompt.SystemMessage },
                new { role = "user", content = request.Prompt.UserMessage },
            },
            stream = false,
            format = "json",
            options = new { num_predict = Math.Min(request.MaximumOutputTokens, _definition.MaximumOutputTokens) },
        };
        using var message = AssistantProviderTransport.CreateJsonRequest(
            HttpMethod.Post,
            endpoint,
            payload,
            _definition,
            request,
            _readEnvironmentVariable);
        using var document = await AssistantProviderTransport.SendAsync(
            _httpClient,
            message,
            _requestTimeout,
            _definition.LocalOnly,
            cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        if (!root.TryGetProperty("message", out var responseMessage))
        {
            throw new AssistantProviderTransportException(
                AssistantProviderTransport.ResponseInvalidCode,
                "Ollama response has no assistant message.");
        }
        var content = AssistantProviderTransport.RequiredString(responseMessage, "content");
        var inputTokens = AssistantProviderTransport.OptionalInt(root, "prompt_eval_count");
        var outputTokens = AssistantProviderTransport.OptionalInt(root, "eval_count");
        return new AssistantProviderResponse(
            _definition.Key,
            _definition.Model,
            content,
            inputTokens,
            outputTokens,
            AssistantProviderTransport.EstimateCost(_definition, inputTokens, outputTokens),
            $"{_definition.Provenance}:ollama:{request.Prompt.ContentSha256}");
    }
}
