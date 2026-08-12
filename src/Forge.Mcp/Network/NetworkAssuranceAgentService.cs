using Forge.Network.Authoring;
using Forge.Network.Operations;

namespace Forge.Mcp.Network;

public sealed record NetworkAssuranceResource(bool Found, NetworkAssuranceProfile? Profile,
    IReadOnlyList<Forge.Network.Sessions.NetworkDiagnostic> Diagnostics);

public sealed record NetworkAssurancePlanRequest(Guid PlanId, long BaseRevision, string ActorId,
    NetworkRole Role, NetworkAuthorityScope Scope, NetworkAssuranceProfile Candidate);

public sealed record NetworkAssurancePlanResult(bool Succeeded, string? PlanId, bool Applied,
    NetworkAssuranceProfile Profile, string SemanticDiff,
    IReadOnlyList<Forge.Network.Sessions.NetworkDiagnostic> Diagnostics, IReadOnlyList<string> Audit);

public sealed class NetworkAssuranceAgentService
{
    private readonly NetworkAssuranceHistory _history;
    private readonly Dictionary<string, NetworkAssurancePlanRequest> _plans = new(StringComparer.Ordinal);
    private readonly List<string> _audit = [];

    public NetworkAssuranceAgentService() : this(NetworkAssuranceProfile.CreateDefault("mcp:r104")) { }
    public NetworkAssuranceAgentService(NetworkAssuranceProfile initial) => _history = new(initial);

    public NetworkAssuranceResource Get(Guid? profileId = null, string? search = null)
    {
        var profile = _history.Current;
        var matchesId = profileId is null || profile.Id == profileId;
        var matchesSearch = string.IsNullOrWhiteSpace(search) ||
            profile.Provenance.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            profile.Logs.Categories.Any(item => item.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
            profile.Logs.RemoteSinks.Any(item => item.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        return matchesId && matchesSearch
            ? new(true, profile, [])
            : new(false, null, [new("NET_ASSURANCE_NOT_FOUND", "/network/assurance",
                "No network assurance profile matched the bounded query.", profileId)]);
    }

    public NetworkAssurancePlanResult Plan(NetworkAssurancePlanRequest request)
    {
        if (request.Scope != NetworkAuthorityScope.Server)
            return Failure([new("NET_ASSURANCE_SCOPE_INVALID", "/network/assurance/authorityScope",
                "Server scope is required.", request.Candidate.Id)]);
        var result = _history.Execute(ToCommand(request, false));
        _audit.Add(result.AuditEntry);
        if (!result.Succeeded)
            return new(false, null, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
        var id = request.PlanId.ToString("D");
        _plans[id] = request;
        return new(true, id, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
    }

    public NetworkAssurancePlanResult Apply(string planId)
    {
        if (!_plans.Remove(planId, out var request))
            return Failure([new("NET_ASSURANCE_PLAN_UNKNOWN", "/network/assurance/planId",
                "Network assurance plan is unknown.")]);
        var result = _history.Execute(ToCommand(request, true));
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, planId, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    public NetworkAssurancePlanResult Revert(string actorId, NetworkRole role)
    {
        var result = _history.Undo(actorId, role);
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, null, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    public NetworkAssurancePlanResult Redo(string actorId, NetworkRole role)
    {
        var result = _history.Redo(actorId, role);
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, null, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    private NetworkAssurancePlanResult Failure(
        IReadOnlyList<Forge.Network.Sessions.NetworkDiagnostic> diagnostics) =>
        new(false, null, false, _history.Current, "no changes", diagnostics, _audit.ToArray());

    private static NetworkAssuranceCommand ToCommand(NetworkAssurancePlanRequest request, bool apply) => new(
        request.PlanId, 1, request.Candidate.Provenance, request.BaseRevision, request.ActorId,
        request.Role, apply, request.Candidate);
}
