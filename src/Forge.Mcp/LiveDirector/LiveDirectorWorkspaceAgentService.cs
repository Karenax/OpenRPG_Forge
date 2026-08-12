using Forge.LiveDirector.Authoring;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Mcp.LiveDirector;

public sealed record LiveDirectorWorkspaceResource(bool Found, LiveDirectorWorkspaceProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);
public sealed record LiveDirectorWorkspacePlanRequest(Guid PlanId, long BaseRevision, string ActorId, NetworkRole Role, NetworkAuthorityScope Scope, LiveDirectorWorkspaceProfile Candidate);
public sealed record LiveDirectorWorkspacePlanResult(bool Succeeded, string? PlanId, bool Applied, LiveDirectorWorkspaceProfile Profile, string SemanticDiff, IReadOnlyList<NetworkDiagnostic> Diagnostics, IReadOnlyList<string> Audit);
public sealed class LiveDirectorWorkspaceAgentService
{
    private readonly LiveDirectorWorkspaceHistory _history;
    private readonly Dictionary<string, LiveDirectorWorkspacePlanRequest> _plans = new(StringComparer.Ordinal);
    private readonly List<string> _audit = [];
    public LiveDirectorWorkspaceAgentService() : this(LiveDirectorWorkspaceProfile.CreateDefault("mcp:r125")) { }
    public LiveDirectorWorkspaceAgentService(LiveDirectorWorkspaceProfile initial) => _history = new(initial);
    public LiveDirectorWorkspaceResource Get(Guid? id = null, string? search = null)
    {
        var p = _history.Current;
        var found = (id is null || id == p.Id) && (string.IsNullOrWhiteSpace(search) || p.Provenance.Contains(search, StringComparison.OrdinalIgnoreCase) || "client creator spawn chooser focus".Contains(search, StringComparison.OrdinalIgnoreCase));
        return found ? new(true, p, []) : new(false, null, [new("LIVE_WORKSPACE_NOT_FOUND", "/liveDirector/workspace", "No workspace matched the bounded query.", id)]);
    }
    public LiveDirectorWorkspacePlanResult Plan(LiveDirectorWorkspacePlanRequest request)
    {
        if (request.Scope != NetworkAuthorityScope.Server) return Fail([new("LIVE_WORKSPACE_SCOPE_INVALID", "/liveDirector/workspace/authorityScope", "Server scope is required.", request.Candidate.Id)]);
        var r = _history.Execute(ToCommand(request, false)); _audit.Add(r.AuditEntry);
        if (!r.Succeeded) return new(false, null, false, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray());
        var id = request.PlanId.ToString("D"); _plans[id] = request; return new(true, id, false, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray());
    }
    public LiveDirectorWorkspacePlanResult Apply(string id)
    {
        if (!_plans.Remove(id, out var request)) return Fail([new("LIVE_WORKSPACE_PLAN_UNKNOWN", "/liveDirector/workspace/planId", "Workspace plan is unknown.")]);
        var r = _history.Execute(ToCommand(request, true)); _audit.Add(r.AuditEntry); return new(r.Succeeded, id, r.Applied, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray());
    }
    public LiveDirectorWorkspacePlanResult Revert(string actor, NetworkRole role)
    {
        var r = _history.Undo(actor, role); _audit.Add(r.AuditEntry); return new(r.Succeeded, null, r.Applied, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray());
    }
    private LiveDirectorWorkspacePlanResult Fail(IReadOnlyList<NetworkDiagnostic> d) => new(false, null, false, _history.Current, "no changes", d, _audit.ToArray());
    private static LiveDirectorWorkspaceCommand ToCommand(LiveDirectorWorkspacePlanRequest r, bool apply) => new(r.PlanId, 1, r.Candidate.Provenance, r.BaseRevision, r.ActorId, r.Role, apply, r.Candidate);
}
