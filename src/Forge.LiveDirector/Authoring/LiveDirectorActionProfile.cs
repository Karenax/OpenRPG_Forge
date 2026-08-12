using System.Text.Json;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.LiveDirector.Authoring;

public sealed record DirectorRelocationPolicy(Guid Id, bool NavigationProbe, bool PlayerConsent,
    bool PartyPolicy, bool PreserveOrientation, bool Preview);
public sealed record DirectorDestructionPolicy(Guid Id, bool CauseRequired, bool ConfirmationRequired,
    IReadOnlyList<string> LootPolicies, bool QuestImpactPreview, bool RollbackLimitVisible);
public sealed record DirectorHealingPolicy(Guid Id, IReadOnlyList<string> Scopes, bool ReasonRequired,
    bool Preview, bool RuntimeResourceAdapter);
public sealed record DirectorLimboPolicy(Guid Id, bool PreserveIdentity, bool AiToggle, bool MovementPolicy,
    bool CollisionPolicy, bool VisibilityPolicy, bool RuntimeNodeAdapter);
public sealed record DirectorPossessionPolicy(Guid Id, int MaximumMinutes, bool InputContext,
    bool ReturnPoint, bool ProtectedPlayerConsent, bool Expiry, bool RuntimeInputAdapter);
public sealed record LiveDirectorActionProfile(Guid Id, int SchemaVersion, string Provenance,
    NetworkAuthorityScope AuthorityScope, long ServerRevision, string AuditIdentity, bool RequiredAtRuntime,
    DirectorRelocationPolicy Relocation, DirectorDestructionPolicy Destruction,
    DirectorHealingPolicy Healing, DirectorLimboPolicy Limbo, DirectorPossessionPolicy Possession)
{
    public static LiveDirectorActionProfile CreateDefault(string provenance = "openrpg:r126") => new(
        Guid.Parse("d1260000-0000-4000-8000-000000000001"), 1, provenance, NetworkAuthorityScope.Server,
        0, "live.director", false,
        new(Guid.Parse("d1260000-0000-4000-8000-000000000002"), true, true, true, true, true),
        new(Guid.Parse("d1260000-0000-4000-8000-000000000003"), true, true, ["preserve", "discard"], true, true),
        new(Guid.Parse("d1260000-0000-4000-8000-000000000004"), ["health", "resources", "effects", "all"], true, true, true),
        new(Guid.Parse("d1260000-0000-4000-8000-000000000005"), true, true, true, true, true, true),
        new(Guid.Parse("d1260000-0000-4000-8000-000000000006"), 120, true, true, true, true, true));
}

public static class LiveDirectorActionValidator
{
    public static IReadOnlyList<NetworkDiagnostic> Validate(LiveDirectorActionProfile p)
    {
        var d = new List<NetworkDiagnostic>();
        var ids = new[] { p.Id, p.Relocation.Id, p.Destruction.Id, p.Healing.Id, p.Limbo.Id, p.Possession.Id };
        if (ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Length) Add("LIVE_ACTION_PROFILE_UUID_INVALID", "/liveDirector/actions", "UUIDs must be unique.", p.Id, d);
        if (p.SchemaVersion != 1) Add("LIVE_ACTION_PROFILE_SCHEMA_UNSUPPORTED", "/liveDirector/actions/schemaVersion", "Only schema v1 is supported.", p.Id, d);
        if (string.IsNullOrWhiteSpace(p.Provenance) || p.AuthorityScope != NetworkAuthorityScope.Server || p.ServerRevision < 0 || string.IsNullOrWhiteSpace(p.AuditIdentity)) Add("LIVE_ACTION_PROFILE_IDENTITY_INVALID", "/liveDirector/actions/identity", "Server identity is required.", p.Id, d);
        if (!p.Relocation.NavigationProbe || !p.Relocation.PlayerConsent || !p.Relocation.PartyPolicy || !p.Relocation.PreserveOrientation || !p.Relocation.Preview) Add("LIVE_RELOCATION_POLICY_INVALID", "/liveDirector/actions/relocation", "Safe consented previewed relocation is required.", p.Relocation.Id, d);
        if (!p.Destruction.CauseRequired || !p.Destruction.ConfirmationRequired || p.Destruction.LootPolicies.Count != 2 || !p.Destruction.QuestImpactPreview || !p.Destruction.RollbackLimitVisible) Add("LIVE_DESTRUCTION_POLICY_INVALID", "/liveDirector/actions/destruction", "Confirmed destruction with loot quest and rollback policy is required.", p.Destruction.Id, d);
        if (p.Healing.Scopes.Count != 4 || !p.Healing.ReasonRequired || !p.Healing.Preview || !p.Healing.RuntimeResourceAdapter) Add("LIVE_HEALING_POLICY_INVALID", "/liveDirector/actions/healing", "Previewed reasoned resource healing is required.", p.Healing.Id, d);
        if (!p.Limbo.PreserveIdentity || !p.Limbo.AiToggle || !p.Limbo.MovementPolicy || !p.Limbo.CollisionPolicy || !p.Limbo.VisibilityPolicy || !p.Limbo.RuntimeNodeAdapter) Add("LIVE_LIMBO_POLICY_INVALID", "/liveDirector/actions/limbo", "Identity preserving AI limbo adapter is required.", p.Limbo.Id, d);
        if (p.Possession.MaximumMinutes is < 1 or > 120 || !p.Possession.InputContext || !p.Possession.ReturnPoint || !p.Possession.ProtectedPlayerConsent || !p.Possession.Expiry || !p.Possession.RuntimeInputAdapter) Add("LIVE_POSSESSION_POLICY_INVALID", "/liveDirector/actions/possession", "Bounded consented possession with return and expiry is required.", p.Possession.Id, d);
        return d;
    }
    private static void Add(string code, string path, string message, Guid id, ICollection<NetworkDiagnostic> d) => d.Add(new(code, path, message, id));
}

public sealed record LiveDirectorActionCommand(Guid CommandId, int SchemaVersion, string Provenance,
    long ExpectedRevision, string ActorId, NetworkRole Role, bool Apply, LiveDirectorActionProfile Candidate);
public sealed record LiveDirectorActionResult(bool Succeeded, bool Applied, LiveDirectorActionProfile Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics, string SemanticDiff, string AuditEntry);
public sealed class LiveDirectorActionHistory
{
    private readonly Stack<LiveDirectorActionProfile> _undo = [];
    private readonly Stack<LiveDirectorActionProfile> _redo = [];
    public LiveDirectorActionHistory(LiveDirectorActionProfile initial) { if (LiveDirectorActionValidator.Validate(initial).Count != 0) throw new ArgumentException("LIVE_ACTION_PROFILE_INITIAL_INVALID"); Current = initial; }
    public LiveDirectorActionProfile Current { get; private set; }
    public LiveDirectorActionResult Execute(LiveDirectorActionCommand c)
    {
        var d = LiveDirectorActionValidator.Validate(c.Candidate).ToList();
        if (c.CommandId == Guid.Empty || c.SchemaVersion != 1 || string.IsNullOrWhiteSpace(c.Provenance)) d.Add(new("LIVE_ACTION_PROFILE_COMMAND_INVALID", "/liveDirector/actions/command", "Command identity required.", c.CommandId));
        if (string.IsNullOrWhiteSpace(c.ActorId) || c.Role is not (NetworkRole.GameMaster or NetworkRole.Administrator)) d.Add(new("LIVE_ACTION_PROFILE_PERMISSION_DENIED", "/liveDirector/actions/audit/actor", "GM identity required.", c.Candidate.Id));
        if (c.ExpectedRevision != Current.ServerRevision || c.Candidate.ServerRevision != Current.ServerRevision + 1) d.Add(new("LIVE_ACTION_PROFILE_REVISION_STALE", "/liveDirector/actions/serverRevision", "Revision must advance once.", c.Candidate.Id));
        var diff = Diff(Current, c.Candidate);
        if (d.Count != 0) return Result(false, false, Current, d, diff, "rejected", c.ActorId);
        if (!c.Apply) return Result(true, false, Current, [], diff, "previewed", c.ActorId);
        _undo.Push(Current); Current = c.Candidate; _redo.Clear(); return Result(true, true, Current, [], diff, "applied", c.ActorId);
    }
    public LiveDirectorActionResult Undo(string actor, NetworkRole role) => Move(_undo, _redo, actor, role, "undo");
    public LiveDirectorActionResult Redo(string actor, NetworkRole role) => Move(_redo, _undo, actor, role, "redo");
    private LiveDirectorActionResult Move(Stack<LiveDirectorActionProfile> from, Stack<LiveDirectorActionProfile> to, string actor, NetworkRole role, string action)
    {
        if (from.Count == 0 || role is not (NetworkRole.GameMaster or NetworkRole.Administrator)) return Result(false, false, Current, [new("LIVE_ACTION_PROFILE_HISTORY_DENIED", "/liveDirector/actions/history", "History action unavailable.", Current.Id)], "no changes", "rejected", actor);
        var before = Current; to.Push(before); Current = from.Pop(); return Result(true, true, Current, [], Diff(before, Current), $"compensated:{action}", actor);
    }
    private LiveDirectorActionResult Result(bool ok, bool applied, LiveDirectorActionProfile p, IReadOnlyList<NetworkDiagnostic> d, string diff, string action, string actor) => new(ok, applied, p, d, diff, $"{action}:live.actions:actor={actor}:revision={p.ServerRevision}");
    private static string Diff(LiveDirectorActionProfile a, LiveDirectorActionProfile b)
    {
        var v = new[] { a.Relocation != b.Relocation ? "relocation" : null, a.Destruction != b.Destruction ? "destruction" : null, a.Healing != b.Healing ? "healing" : null, a.Limbo != b.Limbo ? "limbo" : null, a.Possession != b.Possession ? "possession" : null }.Where(x => x is not null).ToArray(); return v.Length == 0 ? "revision-only" : string.Join(',', v);
    }
}
public sealed class LiveDirectorActionStore
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public string Serialize(LiveDirectorActionProfile p) { var d = LiveDirectorActionValidator.Validate(p); if (d.Count != 0) throw new InvalidDataException(d[0].Code); return JsonSerializer.Serialize(p, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n"; }
    public LiveDirectorActionProfile Deserialize(string json) { var p = JsonSerializer.Deserialize<LiveDirectorActionProfile>(json, Options) ?? throw new InvalidDataException("LIVE_ACTION_PROFILE_EMPTY"); var d = LiveDirectorActionValidator.Validate(p); if (d.Count != 0) throw new InvalidDataException(d[0].Code); return p; }
}
public sealed record LiveDirectorActionResolution(bool Available, LiveDirectorActionProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);
public static class LiveDirectorActionRuntimeAdapter
{
    public static LiveDirectorActionResolution Resolve(LiveDirectorActionProfile? p, bool required) { if (p is null) return required ? new(false, null, [new("LIVE_ACTION_PROFILE_REQUIRED_MISSING", "/liveDirector/actions", "Required action profile is missing.")]) : new(false, null, []); var d = LiveDirectorActionValidator.Validate(p); return new(d.Count == 0, d.Count == 0 ? p : null, d); }
}
public readonly record struct LiveDirectorActionHandle(Guid ProfileId, int Generation);
public sealed record LiveDirectorActionSnapshot(Guid Id, long Revision, int HealScopeCount, int PossessionMinutes, bool Confirmation, bool Consent);
public sealed class LiveDirectorActionScriptService(LiveDirectorActionProfile p)
{
    public LiveDirectorActionHandle Handle { get; } = new(p.Id, 1);
    public LiveDirectorActionSnapshot Read(LiveDirectorActionHandle h) { if (h.ProfileId != p.Id || h.Generation != 1) throw new InvalidOperationException("LIVE_ACTION_PROFILE_HANDLE_INVALID"); return new(p.Id, p.ServerRevision, p.Healing.Scopes.Count, p.Possession.MaximumMinutes, p.Destruction.ConfirmationRequired, p.Relocation.PlayerConsent); }
}
