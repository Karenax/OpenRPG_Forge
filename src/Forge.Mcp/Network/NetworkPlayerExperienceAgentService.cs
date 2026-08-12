using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Mcp.Network;

public sealed record NetworkPlayerExperienceResource(
    bool Found,
    NetworkPlayerExperienceProfile? Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics);

public sealed record NetworkPlayerExperiencePlanRequest(
    Guid PlanId,
    long BaseRevision,
    string ActorId,
    NetworkRole Role,
    NetworkAuthorityScope Scope,
    NetworkPlayerExperienceProfile Candidate);

public sealed record NetworkPlayerExperiencePlanResult(
    bool Succeeded,
    string? PlanId,
    bool Applied,
    NetworkPlayerExperienceProfile Profile,
    string SemanticDiff,
    IReadOnlyList<NetworkDiagnostic> Diagnostics,
    IReadOnlyList<string> Audit);

public sealed class NetworkPlayerExperienceAgentService
{
    private readonly NetworkPlayerExperienceHistory _history;
    private readonly Dictionary<string, NetworkPlayerExperiencePlanRequest> _plans = new(StringComparer.Ordinal);
    private readonly List<string> _audit = [];

    public NetworkPlayerExperienceAgentService() :
        this(NetworkPlayerExperienceProfile.CreateDefault("mcp:r121"))
    { }

    public NetworkPlayerExperienceAgentService(NetworkPlayerExperienceProfile initial) =>
        _history = new(initial);

    public NetworkPlayerExperienceResource Get(Guid? profileId = null, string? search = null)
    {
        var profile = _history.Current;
        var matchesId = profileId is null || profile.Id == profileId;
        var matchesSearch = string.IsNullOrWhiteSpace(search) ||
            profile.Provenance.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            profile.Lobby.RulesetId.Contains(search, StringComparison.OrdinalIgnoreCase);
        return matchesId && matchesSearch
            ? new(true, profile, [])
            : new(false, null, [new("NET_PLAYER_EXPERIENCE_NOT_FOUND", "/network/playerExperience",
                "No player experience profile matched the bounded query.", profileId)]);
    }

    public NetworkPlayerExperiencePlanResult Plan(NetworkPlayerExperiencePlanRequest request)
    {
        if (request.Scope != NetworkAuthorityScope.Server)
            return Failure([new("NET_PLAYER_SCOPE_INVALID", "/network/playerExperience/authorityScope",
                "Server scope is required.", request.Candidate.Id)]);
        var result = _history.Execute(ToCommand(request, false));
        _audit.Add(result.AuditEntry);
        if (!result.Succeeded)
            return new(false, null, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
        var planId = request.PlanId.ToString("D");
        _plans[planId] = request;
        return new(true, planId, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
    }

    public NetworkPlayerExperiencePlanResult Apply(string planId)
    {
        if (!_plans.Remove(planId, out var request))
            return Failure([new("NET_PLAYER_PLAN_UNKNOWN", "/network/playerExperience/planId",
                "Player experience plan is unknown.")]);
        var result = _history.Execute(ToCommand(request, true));
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, planId, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    public NetworkPlayerExperiencePlanResult Revert(string actorId, NetworkRole role)
    {
        var result = _history.Undo(actorId, role);
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, null, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    private NetworkPlayerExperiencePlanResult Failure(IReadOnlyList<NetworkDiagnostic> diagnostics) =>
        new(false, null, false, _history.Current, "no changes", diagnostics, _audit.ToArray());

    private static NetworkPlayerExperienceCommand ToCommand(NetworkPlayerExperiencePlanRequest request, bool apply) =>
        new(request.PlanId, 1, request.Candidate.Provenance, request.BaseRevision, request.ActorId,
            request.Role, apply, request.Candidate);
}
