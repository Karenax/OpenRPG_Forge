namespace Forge.Application.Assistant;

public sealed record AssistantProviderResponse(
    string ProviderKey,
    string Model,
    string Content,
    int InputTokens,
    int OutputTokens,
    decimal EstimatedCost,
    string Provenance);
