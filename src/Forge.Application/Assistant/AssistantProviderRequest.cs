using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed record AssistantProviderRequest(
    Guid PlanId,
    Guid TransactionId,
    AssistantPromptRenderResult Prompt,
    AssistantPermissionLevel Permission,
    int MaximumOutputTokens,
    bool NetworkAuthorized = false,
    bool ProjectContentAuthorized = false);
