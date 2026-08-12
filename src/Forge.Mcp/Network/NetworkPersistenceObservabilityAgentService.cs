using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Mcp.Network;

public sealed record NetworkPersistenceObservabilityResource(bool Found,
    NetworkPersistenceObservabilityProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);
public sealed record NetworkPersistenceObservabilityPlanRequest(Guid PlanId, long BaseRevision, string ActorId,
    NetworkRole Role, NetworkAuthorityScope Scope, NetworkPersistenceObservabilityProfile Candidate);
public sealed record NetworkPersistenceObservabilityPlanResult(bool Succeeded, string? PlanId, bool Applied,
    NetworkPersistenceObservabilityProfile Profile, string SemanticDiff,
    IReadOnlyList<NetworkDiagnostic> Diagnostics, IReadOnlyList<string> Audit);

public sealed class NetworkPersistenceObservabilityAgentService
{
    private readonly NetworkPersistenceObservabilityHistory _history;
    private readonly Dictionary<string, NetworkPersistenceObservabilityPlanRequest> _plans = new(StringComparer.Ordinal);
    private readonly List<string> _audit = [];

    public NetworkPersistenceObservabilityAgentService() :
        this(NetworkPersistenceObservabilityProfile.CreateDefault("mcp:r123"))
    { }

    public NetworkPersistenceObservabilityAgentService(NetworkPersistenceObservabilityProfile initial) =>
        _history = new(initial);

    public NetworkPersistenceObservabilityResource Get(Guid? profileId = null, string? search = null)
    {
        var profile = _history.Current;
        var matches = (profileId is null || profile.Id == profileId) &&
            (string.IsNullOrWhiteSpace(search) ||
             profile.Provenance.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             profile.Observability.Metrics.Any(item => item.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
             profile.Sql.AllowedOperationIds.Any(item => item.Contains(search, StringComparison.OrdinalIgnoreCase)));
        return matches ? new(true, profile, []) : new(false, null,
            [new("NET_PERSIST_OBSERVE_NOT_FOUND", "/network/persistenceObservability",
                "No persistence and observability profile matched the bounded query.", profileId)]);
    }

    public NetworkPersistenceObservabilityPlanResult Plan(NetworkPersistenceObservabilityPlanRequest request)
    {
        if (request.Scope != NetworkAuthorityScope.Server)
            return Failure([new("NET_PERSIST_OBSERVE_SCOPE_INVALID",
                "/network/persistenceObservability/authorityScope", "Server scope is required.", request.Candidate.Id)]);
        var result = _history.Execute(ToCommand(request, false));
        _audit.Add(result.AuditEntry);
        if (!result.Succeeded)
            return new(false, null, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
        var id = request.PlanId.ToString("D");
        _plans[id] = request;
        return new(true, id, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
    }

    public NetworkPersistenceObservabilityPlanResult Apply(string planId)
    {
        if (!_plans.Remove(planId, out var request))
            return Failure([new("NET_PERSIST_OBSERVE_PLAN_UNKNOWN", "/network/persistenceObservability/planId",
                "Persistence and observability plan is unknown.")]);
        var result = _history.Execute(ToCommand(request, true));
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, planId, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    public NetworkPersistenceObservabilityPlanResult Revert(string actorId, NetworkRole role)
    {
        var result = _history.Undo(actorId, role);
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, null, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    private NetworkPersistenceObservabilityPlanResult Failure(IReadOnlyList<NetworkDiagnostic> diagnostics) =>
        new(false, null, false, _history.Current, "no changes", diagnostics, _audit.ToArray());

    private static NetworkPersistenceObservabilityCommand ToCommand(
        NetworkPersistenceObservabilityPlanRequest request, bool apply) =>
        new(request.PlanId, 1, request.Candidate.Provenance, request.BaseRevision, request.ActorId,
            request.Role, apply, request.Candidate);
}
