namespace Forge.Application.Assistant;

public sealed record AssistantPromptRenderResult(
    string PromptKey,
    string PromptVersion,
    string SystemMessage,
    string UserMessage,
    string ResponseJsonSchema,
    string ContentSha256);
