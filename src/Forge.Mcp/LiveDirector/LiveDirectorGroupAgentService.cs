using Forge.LiveDirector.Authoring;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;
namespace Forge.Mcp.LiveDirector;

public sealed record LiveDirectorGroupResource(bool Found, LiveDirectorGroupProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);
public sealed record LiveDirectorGroupPlanRequest(Guid PlanId, long BaseRevision, string ActorId, NetworkRole Role, NetworkAuthorityScope Scope, LiveDirectorGroupProfile Candidate);
public sealed record LiveDirectorGroupPlanResult(bool Succeeded, string? PlanId, bool Applied, LiveDirectorGroupProfile Profile, string SemanticDiff, IReadOnlyList<NetworkDiagnostic> Diagnostics, IReadOnlyList<string> Audit);
public sealed class LiveDirectorGroupAgentService
{
    private readonly LiveDirectorGroupHistory _history; private readonly Dictionary<string, LiveDirectorGroupPlanRequest> _plans = new(StringComparer.Ordinal); private readonly List<string> _audit = [];
    public LiveDirectorGroupAgentService() : this(LiveDirectorGroupProfile.CreateDefault("mcp:r128")) { }
    public LiveDirectorGroupAgentService(LiveDirectorGroupProfile p) => _history = new(p);
    public LiveDirectorGroupResource Get(Guid? id = null, string? search = null) { var p = _history.Current; var found = (id is null || id == p.Id) && (string.IsNullOrWhiteSpace(search) || p.Provenance.Contains(search, StringComparison.OrdinalIgnoreCase) || "trigger selection move attack teleport".Contains(search, StringComparison.OrdinalIgnoreCase)); return found ? new(true, p, []) : new(false, null, [new("LIVE_GROUP_NOT_FOUND", "/liveDirector/group", "No group profile matched.", id)]); }
    public LiveDirectorGroupPlanResult Plan(LiveDirectorGroupPlanRequest q) { if (q.Scope != NetworkAuthorityScope.Server) return Fail([new("LIVE_GROUP_SCOPE_INVALID", "/liveDirector/group/authorityScope", "Server scope required.", q.Candidate.Id)]); var r = _history.Execute(Cmd(q, false)); _audit.Add(r.AuditEntry); if (!r.Succeeded) return new(false, null, false, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray()); var id = q.PlanId.ToString("D"); _plans[id] = q; return new(true, id, false, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray()); }
    public LiveDirectorGroupPlanResult Apply(string id) { if (!_plans.Remove(id, out var q)) return Fail([new("LIVE_GROUP_PLAN_UNKNOWN", "/liveDirector/group/planId", "Plan unknown.")]); var r = _history.Execute(Cmd(q, true)); _audit.Add(r.AuditEntry); return new(r.Succeeded, id, r.Applied, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray()); }
    public LiveDirectorGroupPlanResult Revert(string actor, NetworkRole role) { var r = _history.Undo(actor, role); _audit.Add(r.AuditEntry); return new(r.Succeeded, null, r.Applied, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray()); }
    private LiveDirectorGroupPlanResult Fail(IReadOnlyList<NetworkDiagnostic> d) => new(false, null, false, _history.Current, "no changes", d, _audit.ToArray()); private static LiveDirectorGroupCommand Cmd(LiveDirectorGroupPlanRequest q, bool apply) => new(q.PlanId, 1, q.Candidate.Provenance, q.BaseRevision, q.ActorId, q.Role, apply, q.Candidate);
}
