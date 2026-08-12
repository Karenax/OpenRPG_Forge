using Forge.LiveDirector.Authoring;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Mcp.LiveDirector;

public sealed record LiveDirectorAccessResource(bool Found, LiveDirectorAccessProfile? Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics);

public sealed record LiveDirectorAccessPlanRequest(Guid PlanId, long BaseRevision, string ActorId,
    NetworkRole Role, NetworkAuthorityScope Scope, LiveDirectorAccessProfile Candidate);

public sealed record LiveDirectorAccessPlanResult(bool Succeeded, string? PlanId, bool Applied,
    LiveDirectorAccessProfile Profile, string SemanticDiff, IReadOnlyList<NetworkDiagnostic> Diagnostics,
    IReadOnlyList<string> Audit);

public sealed class LiveDirectorAccessAgentService
{
    private readonly LiveDirectorAccessHistory _history;
    private readonly Dictionary<string, LiveDirectorAccessPlanRequest> _plans = new(StringComparer.Ordinal);
    private readonly List<string> _audit = [];

    public LiveDirectorAccessAgentService() : this(LiveDirectorAccessProfile.CreateDefault("mcp:r105")) { }
    public LiveDirectorAccessAgentService(LiveDirectorAccessProfile initial) => _history = new(initial);

    public LiveDirectorAccessResource Get(Guid? profileId = null, string? search = null)
    {
        var profile = _history.Current;
        var matchesId = profileId is null || profile.Id == profileId;
        var matchesSearch = string.IsNullOrWhiteSpace(search) ||
            profile.Provenance.Contains(search, StringComparison.OrdinalIgnoreCase) ||
            profile.Inspection.VisibleSections.Any(item => item.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
            profile.Players.VisibleColumns.Any(item => item.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
            profile.Chat.Channels.Any(item => item.Contains(search, StringComparison.OrdinalIgnoreCase));
        return matchesId && matchesSearch
            ? new(true, profile, [])
            : new(false, null, [new("LIVE_ACCESS_NOT_FOUND", "/liveDirector/access",
                "No Live Director access profile matched the bounded query.", profileId)]);
    }

    public LiveDirectorAccessPlanResult Plan(LiveDirectorAccessPlanRequest request)
    {
        var result = _history.Execute(ToCommand(request, false));
        _audit.Add(result.AuditEntry);
        if (!result.Succeeded)
            return new(false, null, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
        var planId = request.PlanId.ToString("D");
        _plans[planId] = request;
        return new(true, planId, false, result.Profile, result.SemanticDiff, result.Diagnostics, _audit.ToArray());
    }

    public LiveDirectorAccessPlanResult Apply(string planId)
    {
        if (!_plans.Remove(planId, out var request))
            return Failure([new("LIVE_ACCESS_PLAN_UNKNOWN", "/liveDirector/access/planId",
                "Live Director access plan is unknown.")]);
        var result = _history.Execute(ToCommand(request, true));
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, planId, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    public LiveDirectorAccessPlanResult Revert(string actorId, NetworkRole role)
    {
        var result = _history.Undo(actorId, role);
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, null, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    public LiveDirectorAccessPlanResult Redo(string actorId, NetworkRole role)
    {
        var result = _history.Redo(actorId, role);
        _audit.Add(result.AuditEntry);
        return new(result.Succeeded, null, result.Applied, result.Profile, result.SemanticDiff,
            result.Diagnostics, _audit.ToArray());
    }

    private LiveDirectorAccessPlanResult Failure(IReadOnlyList<NetworkDiagnostic> diagnostics) =>
        new(false, null, false, _history.Current, "no changes", diagnostics, _audit.ToArray());

    private static LiveDirectorAccessCommand ToCommand(LiveDirectorAccessPlanRequest request, bool apply) =>
        new(request.PlanId, 1, request.Candidate.Provenance, request.BaseRevision, request.ActorId,
            request.Role, request.Scope, apply, request.Candidate);
}
