namespace Forge.Scripting;

public sealed record ForgeScriptQuotas
{
    public int InstructionLimit { get; }

    public int TimeoutMilliseconds { get; }

    public int RecursionLimit { get; }

    public int SpawnCap { get; }
    public int MemoryLimitBytes { get; }
    public ulong DeterministicSeed { get; }
    public long SimulationTick { get; }
    public long SimulationTimeMilliseconds { get; }

    public ForgeScriptQuotas(
        int instructionLimit = 10_000,
        int timeoutMilliseconds = 100,
        int recursionLimit = 32,
        int spawnCap = 32,
        int memoryLimitBytes = 8 * 1024 * 1024,
        ulong deterministicSeed = 2026,
        long simulationTick = 0,
        long simulationTimeMilliseconds = 0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(instructionLimit, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeoutMilliseconds, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(recursionLimit, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(spawnCap);
        ArgumentOutOfRangeException.ThrowIfLessThan(memoryLimitBytes, 65_536);
        ArgumentOutOfRangeException.ThrowIfZero(deterministicSeed);
        ArgumentOutOfRangeException.ThrowIfNegative(simulationTick);
        ArgumentOutOfRangeException.ThrowIfNegative(simulationTimeMilliseconds);
        InstructionLimit = instructionLimit;
        TimeoutMilliseconds = timeoutMilliseconds;
        RecursionLimit = recursionLimit;
        SpawnCap = spawnCap;
        MemoryLimitBytes = memoryLimitBytes;
        DeterministicSeed = deterministicSeed;
        SimulationTick = simulationTick;
        SimulationTimeMilliseconds = simulationTimeMilliseconds;
    }
}
