using System.Text.Json;
using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed class OpenAiCompatibleAssistantProvider : IAssistantProvider
{
    private readonly AssistantProviderDefinition _definition;
    private readonly HttpClient _httpClient;
    private readonly Func<string, string?> _readEnvironmentVariable;
    private readonly TimeSpan _requestTimeout;

    public OpenAiCompatibleAssistantProvider(
        AssistantProviderDefinition definition,
        HttpClient httpClient,
        Func<string, string?> readEnvironmentVariable,
        TimeSpan requestTimeout)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _readEnvironmentVariable = readEnvironmentVariable ?? throw new ArgumentNullException(nameof(readEnvironmentVariable));
        _requestTimeout = requestTimeout;
        if (definition.Kind != AssistantProviderKind.OpenAiCompatible)
        {
            throw new ArgumentException("OpenAI-compatible provider requires the matching provider kind.", nameof(definition));
        }
    }

    public async Task<AssistantProviderResponse> GenerateAsync(
        AssistantProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var endpoint = AssistantProviderTransport.AppendEndpoint(_definition.Endpoint, "/chat/completions");
        var payload = new
        {
            model = _definition.Model,
            messages = new[]
            {
                new { role = "system", content = request.Prompt.SystemMessage },
                new { role = "user", content = request.Prompt.UserMessage },
            },
            max_tokens = Math.Min(request.MaximumOutputTokens, _definition.MaximumOutputTokens),
            stream = false,
            response_format = new { type = "json_object" },
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
        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0 ||
            !choices[0].TryGetProperty("message", out var responseMessage))
        {
            throw new AssistantProviderTransportException(
                AssistantProviderTransport.ResponseInvalidCode,
                "OpenAI-compatible response has no assistant message choice.");
        }
        var content = AssistantProviderTransport.RequiredString(responseMessage, "content");
        var inputTokens = 0;
        var outputTokens = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            inputTokens = AssistantProviderTransport.OptionalInt(usage, "prompt_tokens");
            outputTokens = AssistantProviderTransport.OptionalInt(usage, "completion_tokens");
        }
        return new AssistantProviderResponse(
            _definition.Key,
            _definition.Model,
            content,
            inputTokens,
            outputTokens,
            AssistantProviderTransport.EstimateCost(_definition, inputTokens, outputTokens),
            $"{_definition.Provenance}:openai-compatible:{request.Prompt.ContentSha256}");
    }
}
