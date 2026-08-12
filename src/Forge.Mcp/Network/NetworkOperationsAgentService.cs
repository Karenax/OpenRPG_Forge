using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Mcp.Network;

public sealed record NetworkOperationsResource(bool Found, NetworkOperationsProfile? Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics);

public sealed record NetworkOperationsPlanRequest(Guid PlanId, long BaseRevision, string ActorId,
    NetworkRole Role, NetworkAuthorityScope Scope, NetworkOperationsProfile Candidate);

public sealed record NetworkOperationsPlanResult(bool Succeeded, string? PlanId, bool Applied,
    NetworkOperationsProfile Profile, string SemanticDiff, IReadOnlyList<NetworkDiagnostic> Diagnostics,
    IReadOnlyList<string> Audit);

public sealed class NetworkOperationsAgentService
{
    private readonly NetworkOperationsHistory _history;
    private readonly Dictionary<string, NetworkOperationsPlanRequest> _plans = new(StringComparer.Ordinal);
    private readonly List<string> _audit = [];

    public NetworkOperationsAgentService() : this(NetworkOperationsProfile.CreateDefault("mcp:r103")) { }
    public NetworkOperationsAgentService(NetworkOperationsProfile initial) => _history = new(initial);

    public NetworkOperationsResource Get(Guid? profileId = null, string? search = null)
    {
        var profile = _history.Current;
        var matchesId = profileId is null || profile.Id == profileId;
        var matchesSearch = string.IsNullOrWhiteSpace(search) ||
            profile.VersionLock.ModuleId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            profile.Provenance.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            profile.Chat.BridgeAdapters.Any(item => item.Contains(search, StringComparison.OrdinalIgnoreCase));
        return matchesId && matchesSearch
            ? new(true, profile, [])
            : new(false, null, [new("NET_OPERATIONS_NOT_FOUND", "/network/operations", "No network operations profile matched the bounded query.", profileId)]);
    }

    public NetworkOperationsPlanResult Plan(NetworkOperationsPlanRequest request)
    {
        if (request.Scope != NetworkAuthorityScope.Server)
            return Failure([new("NET_OPERATIONS_SCOPE_INVALID", "/network/operations/authorityScope", "Server scope is required.", request.Candidate.Id)]);
        var result = _history.Execute(ToCommand(request, false));
        _audit.Add(result.AuditEntry);
        if (!result.Succeeded)
            return new(false, null, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
        var id = request.PlanId.ToString("D");
        _plans[id] = request;
        return new(true, id, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
    }

    public NetworkOperationsPlanResult Apply(string planId)
    {
        if (!_plans.Remove(planId, out var request))
            return Failure([new("NET_OPERATIONS_PLAN_UNKNOWN", "/network/operations/planId", "Network operations plan is unknown.")]);
        var result = _history.Execute(ToCommand(request, true));
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, planId, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    public NetworkOperationsPlanResult Revert(string actorId, NetworkRole role)
    {
        var result = _history.Undo(actorId, role);
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, null, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    public NetworkOperationsPlanResult Redo(string actorId, NetworkRole role)
    {
        var result = _history.Redo(actorId, role);
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, null, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    private NetworkOperationsPlanResult Failure(IReadOnlyList<NetworkDiagnostic> diagnostics) =>
        new(false, null, false, _history.Current, "no changes", diagnostics, _audit.ToArray());

    private static NetworkOperationsCommand ToCommand(NetworkOperationsPlanRequest request, bool apply) => new(
        request.PlanId, 1, request.Candidate.Provenance, request.BaseRevision, request.ActorId,
        request.Role, apply, request.Candidate);
}
