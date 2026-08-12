using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Mcp.Network;

public sealed record NetworkWorldAdministrationResource(bool Found,
    NetworkWorldAdministrationProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);
public sealed record NetworkWorldAdministrationPlanRequest(Guid PlanId, long BaseRevision, string ActorId,
    NetworkRole Role, NetworkAuthorityScope Scope, NetworkWorldAdministrationProfile Candidate);
public sealed record NetworkWorldAdministrationPlanResult(bool Succeeded, string? PlanId, bool Applied,
    NetworkWorldAdministrationProfile Profile, string SemanticDiff,
    IReadOnlyList<NetworkDiagnostic> Diagnostics, IReadOnlyList<string> Audit);

public sealed class NetworkWorldAdministrationAgentService
{
    private readonly NetworkWorldAdministrationHistory _history;
    private readonly Dictionary<string, NetworkWorldAdministrationPlanRequest> _plans = new(StringComparer.Ordinal);
    private readonly List<string> _audit = [];

    public NetworkWorldAdministrationAgentService() :
        this(NetworkWorldAdministrationProfile.CreateDefault("mcp:r122"))
    {
    }

    public NetworkWorldAdministrationAgentService(NetworkWorldAdministrationProfile initial) => _history = new(initial);

    public NetworkWorldAdministrationResource Get(Guid? profileId = null, string? search = null)
    {
        var profile = _history.Current;
        var matches = (profileId is null || profile.Id == profileId) &&
                      (string.IsNullOrWhiteSpace(search) ||
                       profile.Provenance.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                       profile.Instances.TemplateIds.Any(item => item.Contains(search, StringComparison.OrdinalIgnoreCase)));
        return matches
            ? new(true, profile, [])
            : new(false, null, [new("NET_WORLD_ADMIN_NOT_FOUND", "/network/worldAdministration",
                "No world administration profile matched the bounded query.", profileId)]);
    }

    public NetworkWorldAdministrationPlanResult Plan(NetworkWorldAdministrationPlanRequest request)
    {
        if (request.Scope != NetworkAuthorityScope.Server)
            return Failure([new("NET_WORLD_ADMIN_SCOPE_INVALID", "/network/worldAdministration/authorityScope",
                "Server scope is required.", request.Candidate.Id)]);
        var result = _history.Execute(ToCommand(request, false));
        _audit.Add(result.AuditEntry);
        if (!result.Succeeded)
            return new(false, null, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
        var id = request.PlanId.ToString("D");
        _plans[id] = request;
        return new(true, id, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
    }

    public NetworkWorldAdministrationPlanResult Apply(string planId)
    {
        if (!_plans.Remove(planId, out var request))
            return Failure([new("NET_WORLD_ADMIN_PLAN_UNKNOWN", "/network/worldAdministration/planId",
                "World administration plan is unknown.")]);
        var result = _history.Execute(ToCommand(request, true));
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, planId, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    public NetworkWorldAdministrationPlanResult Revert(string actorId, NetworkRole role)
    {
        var result = _history.Undo(actorId, role);
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, null, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    private NetworkWorldAdministrationPlanResult Failure(IReadOnlyList<NetworkDiagnostic> diagnostics) =>
        new(false, null, false, _history.Current, "no changes", diagnostics, _audit.ToArray());

    private static NetworkWorldAdministrationCommand ToCommand(NetworkWorldAdministrationPlanRequest request,
        bool apply) => new(request.PlanId, 1, request.Candidate.Provenance, request.BaseRevision,
        request.ActorId, request.Role, apply, request.Candidate);
}
