namespace Forge.Scripting;

public sealed record ForgeScriptQuotas
{
    public int InstructionLimit { get; }

    public int TimeoutMilliseconds { get; }

    public int RecursionLimit { get; }

    public int SpawnCap { get; }

    public ForgeScriptQuotas(
        int instructionLimit = 10_000,
        int timeoutMilliseconds = 100,
        int recursionLimit = 32,
        int spawnCap = 32)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(instructionLimit, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutMilliseconds, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(recursionLimit, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(spawnCap);
        InstructionLimit = instructionLimit;
        TimeoutMilliseconds = timeoutMilliseconds;
        RecursionLimit = recursionLimit;
        SpawnCap = spawnCap;
    }
}
