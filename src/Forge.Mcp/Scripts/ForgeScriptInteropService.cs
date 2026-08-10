using Forge.Scripting;

namespace Forge.Mcp.Scripts;

public sealed record ScriptSourceDocument(string Path, string Source, long Revision, string Provenance,
    Guid Id, int SchemaVersion, string ApiVersion, bool RequiredAtRuntime);
public sealed record ScriptSourceTransaction(Guid TransactionId, long BaseRevision, string Path, string Source,
    string Provenance, Guid? SourceId = null, bool RequiredAtRuntime = false);
public sealed record ScriptInteropResult(bool Succeeded, long Revision, ScriptSourceDocument? Document,
    string SemanticDiff, IReadOnlyList<AgentDiagnostic> Diagnostics, string? PlanId = null, bool Applied = false,
    bool Reverted = false);
public sealed record ScriptBindingPreview(string EntityId, string Event, string ScriptPath, long Revision,
    bool Resolved, string SemanticDiff);
public sealed record ScriptInteropTestResult(bool Succeeded, string ScriptPath, string Status,
    IReadOnlyList<ForgeScriptEventResponse> Events, IReadOnlyList<AgentDiagnostic> Diagnostics);
public sealed record ScriptEventBindingPlanRequest(Guid TransactionId, long BaseRevision, string EntityId,
    string ComponentId, string EventName, string ScriptPath, string Handler, string PayloadType, int Priority,
    string? EnableCondition, ForgeScriptEventAuthority Authority);
public sealed record ScriptAssignedEventBinding(Guid Id, string EntityId, string ComponentId, string EventName,
    string ScriptPath, string Handler, string PayloadType, int Priority, string? EnableCondition,
    ForgeScriptEventAuthority Authority, long Revision);
public sealed record ScriptEventBindingPlanResult(bool Succeeded, string? PlanId, long Revision,
    ScriptAssignedEventBinding? Binding, string SemanticDiff, IReadOnlyList<AgentDiagnostic> Diagnostics,
    bool Applied, bool Reverted);

public sealed class ForgeScriptInteropService
{
    private readonly ForgeScriptAgentService _agent;
    private readonly Dictionary<string, ScriptSourceDocument> _sources = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SourcePlan> _sourcePlans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BindingPlan> _bindingPlans = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, ScriptAssignedEventBinding> _bindings = [];
    private long _revision;

    public ForgeScriptInteropService(IForgeScriptRuntime runtime) => _agent = new(runtime);
    public long Revision => _revision;
    public ForgeScriptApiDescription GetApi() => _agent.GetApi();
    public IReadOnlyList<ScriptSourceDocument> GetSources() =>
        _sources.Values.OrderBy(item => item.Path, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<ScriptAssignedEventBinding> GetBindings() => _bindings.Values
        .OrderBy(item => item.EntityId, StringComparer.Ordinal).ThenBy(item => item.ComponentId, StringComparer.Ordinal)
        .ThenBy(item => item.EventName, StringComparer.Ordinal).ThenByDescending(item => item.Priority)
        .ThenBy(item => item.Id).ToArray();

    public ScriptInteropResult PlanCreateOrUpdateSource(ScriptSourceTransaction transaction)
    {
        if (transaction.TransactionId == Guid.Empty || transaction.BaseRevision != _revision ||
            !ForgeScriptSourceValidator.IsSafeSourcePath(transaction.Path) ||
            string.IsNullOrWhiteSpace(transaction.Provenance))
            return SourceFailure("script.transaction_invalid",
                "Transaction, current revision, safe .as path and provenance are required.");
        var compile = _agent.Compile(transaction.Source);
        if (!compile.Succeeded) return SourceFailure(compile.Diagnostics);
        var requestedSourceId = transaction.SourceId.GetValueOrDefault();
        var sourceId = requestedSourceId == Guid.Empty ? StableId(transaction.Path) : requestedSourceId;
        var before = _sources.GetValueOrDefault(transaction.Path);
        var candidate = new ScriptSourceDocument(transaction.Path, transaction.Source, _revision,
            transaction.Provenance, sourceId, 1, _agent.GetApi().ApiVersion, transaction.RequiredAtRuntime);
        var planId = Guid.NewGuid().ToString("D");
        _sourcePlans.Add(planId, new(planId, transaction.BaseRevision, before, candidate));
        return new(true, _revision, candidate,
            $"source:{transaction.Path}|before:{before?.Revision ?? -1}|bytes:{System.Text.Encoding.UTF8.GetByteCount(transaction.Source)}",
            [], planId);
    }

    public ScriptInteropResult ApplySourcePlan(string planId)
    {
        if (!_sourcePlans.TryGetValue(planId, out var plan) || plan.Applied || plan.Reverted)
            return SourceFailure("script.plan_not_applicable", planId);
        if (plan.BaseRevision != _revision) return SourceFailure("script.revision_conflict", planId);
        _revision = checked(_revision + 1);
        var applied = plan.Candidate with { Revision = _revision };
        _sources[applied.Path] = applied;
        plan.Applied = true;
        plan.AppliedRevision = _revision;
        return new(true, _revision, applied, $"apply-source:{applied.Path}", [], planId, true);
    }

    public ScriptInteropResult RevertSourcePlan(string planId)
    {
        if (!_sourcePlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted ||
            plan.AppliedRevision != _revision)
            return SourceFailure("script.plan_not_revertible", planId);
        if (plan.Before is null) _sources.Remove(plan.Candidate.Path);
        else _sources[plan.Before.Path] = plan.Before with { Revision = checked(_revision + 1) };
        _revision = checked(_revision + 1);
        plan.Reverted = true;
        return new(true, _revision, plan.Before, $"revert-source:{plan.Candidate.Path}", [], planId, true, true);
    }

    public ScriptInteropResult CreateOrUpdateSource(ScriptSourceTransaction transaction)
    {
        var planned = PlanCreateOrUpdateSource(transaction);
        return planned.Succeeded && planned.PlanId is not null ? ApplySourcePlan(planned.PlanId) : planned;
    }

    public ForgeScriptCompileResponse Compile(string path) => _sources.TryGetValue(path, out var document)
        ? _agent.Compile(document.Source)
        : new(false, 0, "unknown", "unknown", [new("script.source_missing", path, path)]);

    public IReadOnlyList<AgentDiagnostic> GetDiagnostics(string path) => Compile(path).Diagnostics;

    public ScriptInteropTestResult RunTests(string path, ForgeScriptQuotas? quotas = null)
    {
        if (!_sources.TryGetValue(path, out var document))
            return new(false, path, "source_missing", [], [new("script.source_missing", path, path)]);
        var response = _agent.TestInSandbox(document.Source, quotas);
        return new(response.Succeeded, path, response.Status, response.Events, response.Diagnostics);
    }

    public ScriptEventBindingPlanResult PlanEventBinding(ScriptEventBindingPlanRequest request)
    {
        if (request.TransactionId == Guid.Empty || request.BaseRevision != _revision ||
            string.IsNullOrWhiteSpace(request.EntityId) || string.IsNullOrWhiteSpace(request.ComponentId) ||
            !_sources.TryGetValue(request.ScriptPath, out var source))
            return BindingFailure("script.binding.transaction_invalid", "Current revision, target and source are required.");
        var eventContract = new ForgeScriptEventContract(StableId($"event:{request.EventName}"), 1, "mcp:event",
            request.EventName, request.PayloadType, true);
        var binding = new ForgeScriptEventBinding(StableId($"binding:{request.EntityId}:{request.ComponentId}:" +
            $"{request.EventName}:{request.ScriptPath}:{request.Handler}:{request.Priority}"), 1, "mcp:assign-event",
            request.ComponentId, request.EventName, request.Handler, request.PayloadType, request.Priority,
            request.EnableCondition, request.Authority, true);
        var document = new ForgeScriptSourceDocument(source.Id, source.SchemaVersion, source.Provenance, source.Path,
            source.Source, source.ApiVersion, ForgeScriptSourceDistributionPolicy.SourceAndBytecode,
            source.RequiredAtRuntime, [], [binding]);
        var diagnostics = ForgeScriptSourceValidator.Validate(document, [eventContract]);
        if (diagnostics.Count > 0)
            return new(false, null, _revision, null, "no-change", diagnostics.Select(Map).ToArray(), false, false);
        var assigned = new ScriptAssignedEventBinding(binding.Id, request.EntityId, request.ComponentId,
            request.EventName, request.ScriptPath, request.Handler, request.PayloadType, request.Priority,
            request.EnableCondition, request.Authority, _revision);
        var planId = Guid.NewGuid().ToString("D");
        _bindingPlans.Add(planId, new(planId, request.BaseRevision, assigned));
        return new(true, planId, _revision, assigned,
            $"assign:{request.EntityId}/{request.ComponentId}.{request.EventName}->{request.ScriptPath}::{request.Handler}" +
            $"|priority:{request.Priority}|authority:{request.Authority}", [], false, false);
    }

    public ScriptEventBindingPlanResult ApplyEventBindingPlan(string planId)
    {
        if (!_bindingPlans.TryGetValue(planId, out var plan) || plan.Applied || plan.Reverted)
            return BindingFailure("script.binding.plan_not_applicable", planId);
        if (plan.BaseRevision != _revision) return BindingFailure("script.binding.revision_conflict", planId);
        _revision = checked(_revision + 1);
        var applied = plan.Binding with { Revision = _revision };
        _bindings[applied.Id] = applied;
        plan.Applied = true;
        plan.AppliedRevision = _revision;
        return new(true, planId, _revision, applied, $"apply-binding:{applied.Id}", [], true, false);
    }

    public ScriptEventBindingPlanResult RevertEventBindingPlan(string planId)
    {
        if (!_bindingPlans.TryGetValue(planId, out var plan) || !plan.Applied || plan.Reverted ||
            plan.AppliedRevision != _revision)
            return BindingFailure("script.binding.plan_not_revertible", planId);
        _bindings.Remove(plan.Binding.Id);
        _revision = checked(_revision + 1);
        plan.Reverted = true;
        return new(true, planId, _revision, plan.Binding, $"revert-binding:{plan.Binding.Id}", [], true, true);
    }

    public ScriptBindingPreview PreviewBinding(string entityId, string @event, string scriptPath)
    {
        var resolved = !string.IsNullOrWhiteSpace(entityId) && !string.IsNullOrWhiteSpace(@event) &&
                       _sources.ContainsKey(scriptPath);
        return new(entityId, @event, scriptPath, _revision, resolved,
            resolved ? $"bind:{entityId}.{@event}->{scriptPath}" : "no-change");
    }

    private ScriptInteropResult SourceFailure(string code, string message) =>
        new(false, _revision, null, "no-change", [new(code, message, "script")]);
    private ScriptInteropResult SourceFailure(IReadOnlyList<AgentDiagnostic> diagnostics) =>
        new(false, _revision, null, "no-change", diagnostics);
    private ScriptEventBindingPlanResult BindingFailure(string code, string message) =>
        new(false, null, _revision, null, "no-change", [new(code, message, "script/eventBindings")], false, false);
    private static AgentDiagnostic Map(ForgeScriptSourceDiagnostic diagnostic) =>
        new(diagnostic.Code, diagnostic.Message, diagnostic.Path);

    private static Guid StableId(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private sealed class SourcePlan(string id, long baseRevision, ScriptSourceDocument? before,
        ScriptSourceDocument candidate)
    {
        public string Id { get; } = id;
        public long BaseRevision { get; } = baseRevision;
        public ScriptSourceDocument? Before { get; } = before;
        public ScriptSourceDocument Candidate { get; } = candidate;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long AppliedRevision { get; set; }
    }

    private sealed class BindingPlan(string id, long baseRevision, ScriptAssignedEventBinding binding)
    {
        public string Id { get; } = id;
        public long BaseRevision { get; } = baseRevision;
        public ScriptAssignedEventBinding Binding { get; } = binding;
        public bool Applied { get; set; }
        public bool Reverted { get; set; }
        public long AppliedRevision { get; set; }
    }
}
