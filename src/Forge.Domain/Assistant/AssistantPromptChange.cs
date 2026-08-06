namespace Forge.Domain.Assistant;

public sealed record AssistantPromptChange(string Version, string Summary)
{
    public string Version { get; init; } = (Version ?? string.Empty).Trim();
    public string Summary { get; init; } = (Summary ?? string.Empty).Trim();
}
