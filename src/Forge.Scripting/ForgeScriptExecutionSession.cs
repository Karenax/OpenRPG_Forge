namespace Forge.Scripting;

public sealed record ForgeScriptBudgetSnapshot(
    long Tick,
    long TickInstructions,
    long ModuleInstructions,
    long ModulePeakMemoryBytes,
    int HandlersExecuted,
    int HandlersRejected);

public sealed class ForgeScriptExecutionSession(NativeForgeScriptRuntime runtime, ForgeScriptExecutionPolicy policy)
{
    private readonly NativeForgeScriptRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    private ForgeScriptExecutionPolicy _policy = Validate(policy);
    private long _tickInstructions;
    private long _moduleInstructions;
    private long _modulePeakMemory;
    private int _executed;
    private int _rejected;

    public ForgeScriptExecutionPolicy Policy => _policy;
    public ForgeScriptBudgetSnapshot Snapshot => new(_policy.SimulationTick, _tickInstructions,
        _moduleInstructions, _modulePeakMemory, _executed, _rejected);

    public void AdvanceTick(long simulationTick, long simulationTimeMilliseconds)
    {
        if (simulationTick <= _policy.SimulationTick || simulationTimeMilliseconds < _policy.SimulationTimeMilliseconds)
            throw new InvalidOperationException("script.determinism.clock_regression");
        _policy = _policy with { SimulationTick = simulationTick, SimulationTimeMilliseconds = simulationTimeMilliseconds };
        _tickInstructions = 0;
    }

    public async Task<ForgeScriptExecutionResult> ExecuteHandlerAsync(ForgeScriptArtifact artifact,
        string handlerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerId);
        var tickRemaining = _policy.TickInstructionLimit - _tickInstructions;
        var moduleRemaining = _policy.ModuleInstructionLimit - _moduleInstructions;
        var instructionLimit = (int)Math.Min(_policy.HandlerInstructionLimit, Math.Min(tickRemaining, moduleRemaining));
        if (instructionLimit < 1 || _modulePeakMemory >= _policy.ModuleMemoryLimitBytes)
        {
            _rejected++;
            return new(ForgeScriptExecutionStatus.QuotaExceeded, [],
                [new("runtime", 0, 0, "error", instructionLimit < 1
                    ? "quota.aggregate_instruction_limit_exceeded"
                    : "quota.module_memory_limit_exceeded")]);
        }

        var contextMemory = (int)Math.Min(_policy.ContextMemoryLimitBytes,
            _policy.ModuleMemoryLimitBytes - _modulePeakMemory);
        if (contextMemory < 65_536)
        {
            _rejected++;
            return new(ForgeScriptExecutionStatus.QuotaExceeded, [],
                [new("runtime", 0, 0, "error", "quota.module_memory_limit_exceeded")]);
        }
        var quotas = new ForgeScriptQuotas(instructionLimit, _policy.TimeoutMilliseconds,
            _policy.RecursionLimit, _policy.SpawnLimit, contextMemory, _policy.DeterministicSeed,
            _policy.SimulationTick, _policy.SimulationTimeMilliseconds);
        var result = await _runtime.ExecuteAsync(artifact, quotas, cancellationToken).ConfigureAwait(false);
        var telemetry = result.Telemetry;
        _tickInstructions = checked(_tickInstructions + (telemetry?.Instructions ?? 0));
        _moduleInstructions = checked(_moduleInstructions + (telemetry?.Instructions ?? 0));
        _modulePeakMemory = Math.Max(_modulePeakMemory, telemetry?.PeakMemoryBytes ?? 0);
        _executed++;
        return result;
    }

    private static ForgeScriptExecutionPolicy Validate(ForgeScriptExecutionPolicy policy)
    {
        var diagnostics = ForgeScriptExecutionPolicyValidator.Validate(policy);
        return diagnostics.Count == 0 ? policy : throw new ArgumentException(
            string.Join("; ", diagnostics.Select(item => item.Code)), nameof(policy));
    }
}
