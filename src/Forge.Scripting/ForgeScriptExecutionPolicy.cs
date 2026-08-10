using System.Text.Json;

namespace Forge.Scripting;

public enum ForgeScriptExecutionAuthority { Local, Owner, Server }

public sealed record ForgeScriptExecutionPolicy(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string ApiVersion,
    int HandlerInstructionLimit,
    int TickInstructionLimit,
    long ModuleInstructionLimit,
    int TimeoutMilliseconds,
    int RecursionLimit,
    int SpawnLimit,
    int ContextMemoryLimitBytes,
    long ModuleMemoryLimitBytes,
    ulong DeterministicSeed,
    long SimulationTick,
    long SimulationTimeMilliseconds,
    ForgeScriptExecutionAuthority Authority,
    bool RequiredAtRuntime)
{
    public const int CurrentSchemaVersion = 1;

    public static ForgeScriptExecutionPolicy Default { get; } = new(
        Guid.Parse("14000000-0000-4000-8000-000000000001"), CurrentSchemaVersion, "forge:default",
        "1.0.0", 10_000, 50_000, 1_000_000, 100, 32, 32, 8 * 1024 * 1024,
        128L * 1024 * 1024, 2026, 0, 0, ForgeScriptExecutionAuthority.Server, true);
}

public sealed record ForgeScriptExecutionPolicyDiagnostic(
    string Code, string Path, Guid? SubjectId, string Severity, string Message);

public static class ForgeScriptExecutionPolicyValidator
{
    public static IReadOnlyList<ForgeScriptExecutionPolicyDiagnostic> Validate(ForgeScriptExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var diagnostics = new List<ForgeScriptExecutionPolicyDiagnostic>();
        Add(policy.Id == Guid.Empty, "script.policy.uuid_required", "script/policy/id", "A stable UUID is required.");
        Add(policy.SchemaVersion != ForgeScriptExecutionPolicy.CurrentSchemaVersion,
            "script.policy.schema_unsupported", "script/policy/schemaVersion", "Execution policy schema v1 is required.");
        Add(string.IsNullOrWhiteSpace(policy.Provenance), "script.policy.provenance_required",
            "script/policy/provenance", "Execution policy provenance is required.");
        Add(!ForgeScriptApiCompatibility.IsSupported(policy.ApiVersion), "script.policy.api_unsupported",
            "script/policy/apiVersion", $"ForgeScript API '{policy.ApiVersion}' is unsupported.");
        Add(policy.HandlerInstructionLimit < 1 || policy.TickInstructionLimit < policy.HandlerInstructionLimit ||
            policy.ModuleInstructionLimit < policy.TickInstructionLimit,
            "script.policy.instruction_budget_invalid", "script/policy/instructions",
            "Instruction budgets must be positive and ordered handler <= tick <= module.");
        Add(policy.TimeoutMilliseconds is < 1 or > 60_000 || policy.RecursionLimit is < 1 or > 1024 ||
            policy.SpawnLimit is < 0 or > 100_000, "script.policy.execution_budget_invalid",
            "script/policy/execution", "Timeout, recursion or spawn budget is outside the supported range.");
        Add(policy.ContextMemoryLimitBytes is < 65_536 or > 1_073_741_824 ||
            policy.ModuleMemoryLimitBytes < policy.ContextMemoryLimitBytes ||
            policy.ModuleMemoryLimitBytes > 8L * 1_073_741_824,
            "script.policy.memory_budget_invalid", "script/policy/memory",
            "Memory budgets must be ordered context <= module and remain in the supported range.");
        Add(policy.DeterministicSeed == 0 || policy.SimulationTick < 0 || policy.SimulationTimeMilliseconds < 0,
            "script.policy.determinism_invalid", "script/policy/determinism",
            "Seed must be non-zero and simulation clock values cannot be negative.");
        Add(!Enum.IsDefined(policy.Authority), "script.policy.authority_invalid", "script/policy/authority",
            "Execution authority must be explicit.");
        return diagnostics;

        void Add(bool condition, string code, string path, string message)
        {
            if (condition) diagnostics.Add(new(code, path, policy.Id == Guid.Empty ? null : policy.Id, "error", message));
        }
    }
}

public sealed record ForgeScriptExecutionPolicyCommand(Guid TransactionId, long ExpectedRevision,
    ForgeScriptExecutionPolicy Policy, string Reason);

public sealed record ForgeScriptExecutionPolicyMutation(bool Succeeded, ForgeScriptExecutionPolicy Policy,
    long Revision, string SemanticDiff, IReadOnlyList<ForgeScriptExecutionPolicyDiagnostic> Diagnostics);

public sealed class ForgeScriptExecutionPolicyHistory(ForgeScriptExecutionPolicy initial)
{
    private readonly Stack<ForgeScriptExecutionPolicy> _undo = [];
    private readonly Stack<ForgeScriptExecutionPolicy> _redo = [];
    public ForgeScriptExecutionPolicy Policy { get; private set; } = initial;
    public long Revision { get; private set; }
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public ForgeScriptExecutionPolicyMutation Preview(ForgeScriptExecutionPolicyCommand command) => Evaluate(command);
    public ForgeScriptExecutionPolicyMutation Apply(ForgeScriptExecutionPolicyCommand command)
    {
        var result = Evaluate(command);
        if (!result.Succeeded) return result;
        _undo.Push(Policy); _redo.Clear(); Policy = command.Policy; Revision++;
        return result with { Policy = Policy, Revision = Revision };
    }
    public ForgeScriptExecutionPolicyMutation Undo()
    {
        if (!CanUndo) return Failure("script.policy.undo_empty", "script/policy/history", "Nothing to undo.");
        _redo.Push(Policy); Policy = _undo.Pop(); Revision++;
        return new(true, Policy, Revision, "undo:script-execution-policy", []);
    }
    public ForgeScriptExecutionPolicyMutation Redo()
    {
        if (!CanRedo) return Failure("script.policy.redo_empty", "script/policy/history", "Nothing to redo.");
        _undo.Push(Policy); Policy = _redo.Pop(); Revision++;
        return new(true, Policy, Revision, "redo:script-execution-policy", []);
    }
    private ForgeScriptExecutionPolicyMutation Evaluate(ForgeScriptExecutionPolicyCommand command)
    {
        if (command.TransactionId == Guid.Empty || command.ExpectedRevision != Revision ||
            string.IsNullOrWhiteSpace(command.Reason))
            return Failure("script.policy.command_invalid", "script/policy/command",
                "Transaction identity, current revision and reason are required.");
        var diagnostics = ForgeScriptExecutionPolicyValidator.Validate(command.Policy);
        return diagnostics.Count > 0
            ? new(false, Policy, Revision, "no-change", diagnostics)
            : new(true, command.Policy, Revision,
                $"policy:{Policy.Id:D}|instructions:{Policy.HandlerInstructionLimit}->{command.Policy.HandlerInstructionLimit}|memory:{Policy.ContextMemoryLimitBytes}->{command.Policy.ContextMemoryLimitBytes}", []);
    }
    private ForgeScriptExecutionPolicyMutation Failure(string code, string path, string message) =>
        new(false, Policy, Revision, "no-change", [new(code, path, Policy.Id, "error", message)]);
}

public sealed class ForgeScriptExecutionPolicyStore(string root)
{
    private readonly string _root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root)));
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public void Save(ForgeScriptExecutionPolicy policy)
    {
        var diagnostics = ForgeScriptExecutionPolicyValidator.Validate(policy);
        if (diagnostics.Count > 0) throw new InvalidOperationException(string.Join("; ", diagnostics.Select(item => item.Code)));
        Directory.CreateDirectory(_root);
        var destination = Path.Combine(_root, "forge-script-policy.json");
        var temporary = destination + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(policy, JsonOptions)); File.Move(temporary, destination, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public ForgeScriptExecutionPolicy Load() => JsonSerializer.Deserialize<ForgeScriptExecutionPolicy>(
        File.ReadAllText(Path.Combine(_root, "forge-script-policy.json")), JsonOptions)
        ?? throw new InvalidOperationException("script.policy.document_invalid");
}
