namespace Forge.Scripting;

public sealed record ForgeScriptExecutionResult(
    ForgeScriptExecutionStatus Status,
    IReadOnlyList<ForgeScriptEvent> Events,
    IReadOnlyList<ForgeScriptDiagnostic> Diagnostics);
