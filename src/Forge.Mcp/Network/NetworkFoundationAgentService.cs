using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Mcp.Network;

public sealed record NetworkFoundationResource(
    bool Found,
    NetworkFoundationProfile? Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics);

public sealed record NetworkFoundationPlanRequest(
    Guid PlanId,
    long BaseRevision,
    string ActorId,
    NetworkRole Role,
    NetworkAuthorityScope Scope,
    NetworkFoundationProfile Candidate);

public sealed record NetworkFoundationPlanResult(
    bool Succeeded,
    string? PlanId,
    bool Applied,
    NetworkFoundationProfile Profile,
    string SemanticDiff,
    IReadOnlyList<NetworkDiagnostic> Diagnostics,
    IReadOnlyList<string> Audit);

public sealed class NetworkFoundationAgentService
{
    private readonly NetworkFoundationHistory _history;
    private readonly Dictionary<string, NetworkFoundationPlanRequest> _plans = new(StringComparer.Ordinal);
    private readonly List<string> _audit = [];

    public NetworkFoundationAgentService() : this(NetworkFoundationProfile.CreateDefault("mcp:r102")) { }

    public NetworkFoundationAgentService(NetworkFoundationProfile initial) => _history = new(initial);

    public NetworkFoundationResource Get(Guid? profileId = null, string? search = null)
    {
        var profile = _history.Current;
        var matchesId = profileId is null || profile.Id == profileId;
        var matchesSearch = string.IsNullOrWhiteSpace(search) || profile.Server.Name.Contains(search,
            StringComparison.OrdinalIgnoreCase) || profile.Provenance.Contains(search, StringComparison.OrdinalIgnoreCase);
        return matchesId && matchesSearch
            ? new(true, profile, [])
            : new(false, null, [new("NET_FOUNDATION_NOT_FOUND", "/network", "No network foundation profile matched the bounded query.", profileId)]);
    }

    public NetworkFoundationPlanResult Plan(NetworkFoundationPlanRequest request)
    {
        if (request.Scope != NetworkAuthorityScope.Server)
            return Failure([new("NET_AUTHORITY_SCOPE_INVALID", "/network/authorityScope", "Server scope is required.", request.Candidate.Id)]);
        var command = ToCommand(request, false);
        var result = _history.Execute(command);
        _audit.Add(result.AuditEntry);
        if (result.Succeeded)
        {
            var id = request.PlanId.ToString("D");
            _plans[id] = request;
            return new(true, id, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
        }
        return new(false, null, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
    }

    public NetworkFoundationPlanResult Apply(string planId)
    {
        if (!_plans.Remove(planId, out var request))
            return Failure([new("NET_PLAN_UNKNOWN", "/network/planId", "Network foundation plan is unknown.")]);
        var result = _history.Execute(ToCommand(request, true));
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, planId, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    public NetworkFoundationPlanResult Revert(string actorId, NetworkRole role)
    {
        var result = _history.Undo(actorId, role);
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, null, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    public NetworkFoundationPlanResult Redo(string actorId, NetworkRole role)
    {
        var result = _history.Redo(actorId, role);
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, null, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    private NetworkFoundationPlanResult Failure(IReadOnlyList<NetworkDiagnostic> diagnostics) =>
        new(false, null, false, _history.Current, "no changes", diagnostics, _audit.ToArray());

    private static NetworkFoundationCommand ToCommand(NetworkFoundationPlanRequest request, bool apply) => new(
        request.PlanId,
        1,
        request.Candidate.Provenance,
        request.BaseRevision,
        request.ActorId,
        request.Role,
        apply,
        request.Candidate);
}
