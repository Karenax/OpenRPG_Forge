using System.Text.Json;
using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed class DeterministicAssistantProvider : IAssistantProvider
{
    private readonly AssistantProviderDefinition _definition;

    public DeterministicAssistantProvider(AssistantProviderDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (definition.Kind != AssistantProviderKind.DeterministicOffline)
        {
            throw new ArgumentException("Deterministic provider requires the offline provider kind.", nameof(definition));
        }
    }

    public Task<AssistantProviderResponse> GenerateAsync(
        AssistantProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var content = JsonSerializer.Serialize(new
        {
            summary = $"Prévisualisation hors ligne · {request.Prompt.PromptKey}@{request.Prompt.PromptVersion}",
            semanticDiff = Array.Empty<object>(),
            planId = request.PlanId,
            transactionId = request.TransactionId,
            permission = request.Permission.ToString(),
            promptSha256 = request.Prompt.ContentSha256,
        });
        return Task.FromResult(new AssistantProviderResponse(
            _definition.Key,
            _definition.Model,
            content,
            Math.Max(1, (request.Prompt.SystemMessage.Length + request.Prompt.UserMessage.Length) / 4),
            Math.Min(request.MaximumOutputTokens, Math.Max(1, content.Length / 4)),
            0,
            $"{_definition.Provenance}:deterministic-offline"));
    }
}
