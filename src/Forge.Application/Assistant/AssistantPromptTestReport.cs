namespace Forge.Application.Assistant;

public sealed record AssistantPromptTestReport(
    bool Passed,
    int Total,
    int PassedCount,
    IReadOnlyList<string> Failures);
