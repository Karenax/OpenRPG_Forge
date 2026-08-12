using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Mcp.Network;

public sealed record NetworkResilienceCapacityResource(bool Found, NetworkResilienceCapacityProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);
public sealed record NetworkResilienceCapacityPlanRequest(Guid PlanId, long BaseRevision, string ActorId, NetworkRole Role, NetworkAuthorityScope Scope, NetworkResilienceCapacityProfile Candidate);
public sealed record NetworkResilienceCapacityPlanResult(bool Succeeded, string? PlanId, bool Applied, NetworkResilienceCapacityProfile Profile, string SemanticDiff, IReadOnlyList<NetworkDiagnostic> Diagnostics, IReadOnlyList<string> Audit);

public sealed class NetworkResilienceCapacityAgentService
{
    private readonly NetworkResilienceCapacityHistory _history;
    private readonly Dictionary<string, NetworkResilienceCapacityPlanRequest> _plans = new(StringComparer.Ordinal);
    private readonly List<string> _audit = [];
    public NetworkResilienceCapacityAgentService() : this(NetworkResilienceCapacityProfile.CreateDefault("mcp:r124")) { }
    public NetworkResilienceCapacityAgentService(NetworkResilienceCapacityProfile initial) => _history = new(initial);
    public NetworkResilienceCapacityResource Get(Guid? id = null, string? search = null)
    {
        var profile = _history.Current;
        var found = (id is null || id == profile.Id) && (string.IsNullOrWhiteSpace(search) || profile.Provenance.Contains(search, StringComparison.OrdinalIgnoreCase) || "backup capacity queue rollback".Contains(search, StringComparison.OrdinalIgnoreCase));
        return found ? new(true, profile, []) : new(false, null, [new("NET_RESILIENCE_NOT_FOUND", "/network/resilienceCapacity", "No resilience profile matched the bounded query.", id)]);
    }
    public NetworkResilienceCapacityPlanResult Plan(NetworkResilienceCapacityPlanRequest request)
    {
        if (request.Scope != NetworkAuthorityScope.Server) return Failure([new("NET_RESILIENCE_SCOPE_INVALID", "/network/resilienceCapacity/authorityScope", "Server scope is required.", request.Candidate.Id)]);
        var result = _history.Execute(ToCommand(request, false)); _audit.Add(result.AuditEntry);
        if (!result.Succeeded) return new(false, null, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
        var id = request.PlanId.ToString("D"); _plans[id] = request;
        return new(true, id, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
    }
    public NetworkResilienceCapacityPlanResult Apply(string id)
    {
        if (!_plans.Remove(id, out var request)) return Failure([new("NET_RESILIENCE_PLAN_UNKNOWN", "/network/resilienceCapacity/planId", "Resilience plan is unknown.")]);
        var result = _history.Execute(ToCommand(request, true)); _audit.Add(result.AuditEntry);
        return new(result.Succeeded, id, result.Applied, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
    }
    public NetworkResilienceCapacityPlanResult Revert(string actor, NetworkRole role)
    {
        var result = _history.Undo(actor, role); _audit.Add(result.AuditEntry);
        return new(result.Succeeded, null, result.Applied, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
    }
    private NetworkResilienceCapacityPlanResult Failure(IReadOnlyList<NetworkDiagnostic> diagnostics) => new(false, null, false, _history.Current, "no changes", diagnostics, _audit.ToArray());
    private static NetworkResilienceCapacityCommand ToCommand(NetworkResilienceCapacityPlanRequest request, bool apply) => new(request.PlanId, 1, request.Candidate.Provenance, request.BaseRevision, request.ActorId, request.Role, apply, request.Candidate);
}
