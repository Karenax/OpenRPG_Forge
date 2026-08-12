using Forge.LiveDirector.Authoring;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Mcp.LiveDirector;

public sealed record LiveDirectorAuthorityResource(bool Found, LiveDirectorAuthorityProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);
public sealed record LiveDirectorAuthorityPlanRequest(Guid PlanId, long BaseRevision, string ActorId, NetworkRole Role, NetworkAuthorityScope Scope, LiveDirectorAuthorityProfile Candidate);
public sealed record LiveDirectorAuthorityPlanResult(bool Succeeded, string? PlanId, bool Applied, LiveDirectorAuthorityProfile Profile, string SemanticDiff, IReadOnlyList<NetworkDiagnostic> Diagnostics, IReadOnlyList<string> Audit);
public sealed class LiveDirectorAuthorityAgentService
{
    private readonly LiveDirectorAuthorityHistory _history; private readonly Dictionary<string, LiveDirectorAuthorityPlanRequest> _plans = new(StringComparer.Ordinal); private readonly List<string> _audit = [];
    public LiveDirectorAuthorityAgentService() : this(LiveDirectorAuthorityProfile.CreateDefault("mcp:r127")) { }
    public LiveDirectorAuthorityAgentService(LiveDirectorAuthorityProfile p) => _history = new(p);
    public LiveDirectorAuthorityResource Get(Guid? id = null, string? search = null) { var p = _history.Current; var found = (id is null || id == p.Id) && (string.IsNullOrWhiteSpace(search) || p.Provenance.Contains(search, StringComparison.OrdinalIgnoreCase) || "impersonate protect gold xp door".Contains(search, StringComparison.OrdinalIgnoreCase)); return found ? new(true, p, []) : new(false, null, [new("LIVE_AUTHORITY_NOT_FOUND", "/liveDirector/authority", "No authority profile matched.", id)]); }
    public LiveDirectorAuthorityPlanResult Plan(LiveDirectorAuthorityPlanRequest q) { if (q.Scope != NetworkAuthorityScope.Server) return Fail([new("LIVE_AUTHORITY_SCOPE_INVALID", "/liveDirector/authority/authorityScope", "Server scope required.", q.Candidate.Id)]); var r = _history.Execute(Cmd(q, false)); _audit.Add(r.AuditEntry); if (!r.Succeeded) return new(false, null, false, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray()); var id = q.PlanId.ToString("D"); _plans[id] = q; return new(true, id, false, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray()); }
    public LiveDirectorAuthorityPlanResult Apply(string id) { if (!_plans.Remove(id, out var q)) return Fail([new("LIVE_AUTHORITY_PLAN_UNKNOWN", "/liveDirector/authority/planId", "Plan unknown.")]); var r = _history.Execute(Cmd(q, true)); _audit.Add(r.AuditEntry); return new(r.Succeeded, id, r.Applied, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray()); }
    public LiveDirectorAuthorityPlanResult Revert(string actor, NetworkRole role) { var r = _history.Undo(actor, role); _audit.Add(r.AuditEntry); return new(r.Succeeded, null, r.Applied, r.Profile, r.SemanticDiff, r.Diagnostics, _audit.ToArray()); }
    private LiveDirectorAuthorityPlanResult Fail(IReadOnlyList<NetworkDiagnostic> d) => new(false, null, false, _history.Current, "no changes", d, _audit.ToArray()); private static LiveDirectorAuthorityCommand Cmd(LiveDirectorAuthorityPlanRequest q, bool apply) => new(q.PlanId, 1, q.Candidate.Provenance, q.BaseRevision, q.ActorId, q.Role, apply, q.Candidate);
}
