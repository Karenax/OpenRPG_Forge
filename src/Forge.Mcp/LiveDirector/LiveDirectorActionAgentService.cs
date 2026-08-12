using Forge.LiveDirector.Authoring;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Mcp.LiveDirector;

public sealed record LiveDirectorActionResource(bool Found, LiveDirectorActionProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);
public sealed record LiveDirectorActionPlanRequest(Guid PlanId, long BaseRevision, string ActorId, NetworkRole Role, NetworkAuthorityScope Scope, LiveDirectorActionProfile Candidate);
public sealed record LiveDirectorActionPlanResult(bool Succeeded, string? PlanId, bool Applied, LiveDirectorActionProfile Profile, string SemanticDiff, IReadOnlyList<NetworkDiagnostic> Diagnostics, IReadOnlyList<string> Audit);
public sealed class LiveDirectorActionAgentService
{
    private readonly LiveDirectorActionHistory _history; private readonly Dictionary<string, LiveDirectorActionPlanRequest> _plans = new(StringComparer.Ordinal); private readonly List<string> _audit = [];
    public LiveDirectorActionAgentService() : this(LiveDirectorActionProfile.CreateDefault("mcp:r126")) { }
    public LiveDirectorActionAgentService(LiveDirectorActionProfile p) => _history = new(p);
    public LiveDirectorActionResource Get(Guid? id = null, string? search = null) { var p = _history.Current; var found = (id is null || id == p.Id) && (string.IsNullOrWhiteSpace(search) || p.Provenance.Contains(search, StringComparison.OrdinalIgnoreCase) || "relocate destroy heal limbo possess".Contains(search, StringComparison.OrdinalIgnoreCase)); return found ? new(true, p, []) : new(false, null, [new("LIVE_ACTION_PROFILE_NOT_FOUND", "/liveDirector/actions", "No action profile matched.", id)]); }
    public LiveDirectorActionPlanResult Plan(LiveDirectorActionPlanRequest request) { if (request.Scope != NetworkAuthorityScope.Server) return Fail([new("LIVE_ACTION_PROFILE_SCOPE_INVALID", "/liveDirector/actions/authorityScope", "Server scope required.", request.Candidate.Id)]); var r = _history.Execute(ToCommand(request, false)); _audit.Add(r.AuditEntry); if (!r.Succeeded) return new(false, null, false, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray()); var id = request.PlanId.ToString("D"); _plans[id] = request; return new(true, id, false, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray()); }
    public LiveDirectorActionPlanResult Apply(string id) { if (!_plans.Remove(id, out var request)) return Fail([new("LIVE_ACTION_PROFILE_PLAN_UNKNOWN", "/liveDirector/actions/planId", "Action plan unknown.")]); var r = _history.Execute(ToCommand(request, true)); _audit.Add(r.AuditEntry); return new(r.Succeeded, id, r.Applied, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray()); }
    public LiveDirectorActionPlanResult Revert(string actor, NetworkRole role) { var r = _history.Undo(actor, role); _audit.Add(r.AuditEntry); return new(r.Succeeded, null, r.Applied, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray()); }
    private LiveDirectorActionPlanResult Fail(IReadOnlyList<NetworkDiagnostic> d) => new(false, null, false, _history.Current, "no changes", d, _audit.ToArray());
    private static LiveDirectorActionCommand ToCommand(LiveDirectorActionPlanRequest r, bool apply) => new(r.PlanId, 1, r.Candidate.Provenance, r.BaseRevision, r.ActorId, r.Role, apply, r.Candidate);
}
