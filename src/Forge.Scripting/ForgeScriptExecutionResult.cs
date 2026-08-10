namespace Forge.Scripting;

public sealed record ForgeScriptExecutionResult(
    ForgeScriptExecutionStatus Status,
    IReadOnlyList<ForgeScriptEvent> Events,
    IReadOnlyList<ForgeScriptDiagnostic> Diagnostics,
    ForgeScriptExecutionTelemetry? Telemetry = null);

public sealed record ForgeScriptExecutionTelemetry(
    long Instructions,
    long PeakMemoryBytes,
    long ElapsedMicroseconds,
    ulong DeterministicSeed,
    long SimulationTick,
    long SimulationTimeMilliseconds);
