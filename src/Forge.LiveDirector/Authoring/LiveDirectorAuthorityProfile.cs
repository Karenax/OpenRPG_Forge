using System.Text.Json;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.LiveDirector.Authoring;

public sealed record LiveDirectorAuthorityProfile(Guid Id, int SchemaVersion, string Provenance,
    NetworkAuthorityScope AuthorityScope, long ServerRevision, string AuditIdentity, bool RequiredAtRuntime,
    IReadOnlyList<string> ImpersonationModes, bool ImpersonationDisclosure,
    int MaximumProtectionMinutes, IReadOnlyList<string> ProtectionScopes, bool ProtectionIndicator,
    long MaximumCurrencyAdjustment, long MaximumWallet, bool PersistentWalletAdapter,
    long MaximumExperienceAdjustment, bool RulesetLevelProjection, bool PersistentProgressionAdapter,
    IReadOnlyList<string> DoorStates, bool PortalRelink, bool DoorRuntimeAdapter)
{
    public static LiveDirectorAuthorityProfile CreateDefault(string provenance = "openrpg:r127") => new(
        Guid.Parse("d1270000-0000-4000-8000-000000000001"), 1, provenance, NetworkAuthorityScope.Server,
        0, "live.director", false, ["chat", "dialogue", "voice", "animation"], true,
        1440, ["damage", "effects", "all"], true, 100_000, 10_000_000, true,
        1_000_000, true, true, ["locked", "unlocked", "open", "repaired", "relinked"], true, true);
}

public static class LiveDirectorAuthorityValidator
{
    public static IReadOnlyList<NetworkDiagnostic> Validate(LiveDirectorAuthorityProfile p)
    {
        var d = new List<NetworkDiagnostic>();
        if (p.Id == Guid.Empty) Add("LIVE_AUTHORITY_UUID_INVALID", "/liveDirector/authority", "UUID is required.", p.Id, d);
        if (p.SchemaVersion != 1) Add("LIVE_AUTHORITY_SCHEMA_UNSUPPORTED", "/liveDirector/authority/schemaVersion", "Only schema v1 is supported.", p.Id, d);
        if (string.IsNullOrWhiteSpace(p.Provenance) || p.AuthorityScope != NetworkAuthorityScope.Server || p.ServerRevision < 0 || string.IsNullOrWhiteSpace(p.AuditIdentity)) Add("LIVE_AUTHORITY_IDENTITY_INVALID", "/liveDirector/authority/identity", "Server identity is required.", p.Id, d);
        if (p.ImpersonationModes.Count != 4 || !p.ImpersonationDisclosure) Add("LIVE_IMPERSONATION_POLICY_INVALID", "/liveDirector/authority/impersonation", "Four disclosed impersonation modes are required.", p.Id, d);
        if (p.MaximumProtectionMinutes is < 1 or > 1440 || p.ProtectionScopes.Count != 3 || !p.ProtectionIndicator) Add("LIVE_PROTECTION_POLICY_INVALID", "/liveDirector/authority/protection", "Bounded visible protection scopes are required.", p.Id, d);
        if (p.MaximumCurrencyAdjustment < 1 || p.MaximumWallet < p.MaximumCurrencyAdjustment || !p.PersistentWalletAdapter) Add("LIVE_CURRENCY_POLICY_INVALID", "/liveDirector/authority/currency", "Capped non-negative persistent wallet policy is required.", p.Id, d);
        if (p.MaximumExperienceAdjustment < 1 || !p.RulesetLevelProjection || !p.PersistentProgressionAdapter) Add("LIVE_EXPERIENCE_POLICY_INVALID", "/liveDirector/authority/experience", "Capped ruleset projected persistent XP policy is required.", p.Id, d);
        if (p.DoorStates.Count != 5 || !p.PortalRelink || !p.DoorRuntimeAdapter) Add("LIVE_DOOR_POLICY_INVALID", "/liveDirector/authority/door", "Five door states, relink and runtime adapter are required.", p.Id, d);
        return d;
    }
    private static void Add(string code, string path, string message, Guid id, ICollection<NetworkDiagnostic> d) => d.Add(new(code, path, message, id));
}

public sealed record LiveDirectorAuthorityCommand(Guid CommandId, int SchemaVersion, string Provenance, long ExpectedRevision, string ActorId, NetworkRole Role, bool Apply, LiveDirectorAuthorityProfile Candidate);
public sealed record LiveDirectorAuthorityResult(bool Succeeded, bool Applied, LiveDirectorAuthorityProfile Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics, string SemanticDiff, string AuditEntry);
public sealed class LiveDirectorAuthorityHistory
{
    private readonly Stack<LiveDirectorAuthorityProfile> _undo = []; private readonly Stack<LiveDirectorAuthorityProfile> _redo = [];
    public LiveDirectorAuthorityHistory(LiveDirectorAuthorityProfile initial) { if (LiveDirectorAuthorityValidator.Validate(initial).Count != 0) throw new ArgumentException("LIVE_AUTHORITY_INITIAL_INVALID"); Current = initial; }
    public LiveDirectorAuthorityProfile Current { get; private set; }
    public LiveDirectorAuthorityResult Execute(LiveDirectorAuthorityCommand c)
    {
        var d = LiveDirectorAuthorityValidator.Validate(c.Candidate).ToList();
        if (c.CommandId == Guid.Empty || c.SchemaVersion != 1 || string.IsNullOrWhiteSpace(c.Provenance)) d.Add(new("LIVE_AUTHORITY_COMMAND_INVALID", "/liveDirector/authority/command", "Command identity required.", c.CommandId));
        if (string.IsNullOrWhiteSpace(c.ActorId) || c.Role is not (NetworkRole.GameMaster or NetworkRole.Administrator)) d.Add(new("LIVE_AUTHORITY_PERMISSION_DENIED", "/liveDirector/authority/audit/actor", "GM identity required.", c.Candidate.Id));
        if (c.ExpectedRevision != Current.ServerRevision || c.Candidate.ServerRevision != Current.ServerRevision + 1) d.Add(new("LIVE_AUTHORITY_REVISION_STALE", "/liveDirector/authority/serverRevision", "Revision must advance once.", c.Candidate.Id));
        const string diff = "impersonation,protection,currency,experience,door";
        if (d.Count != 0) return Result(false, false, Current, d, diff, "rejected", c.ActorId); if (!c.Apply) return Result(true, false, Current, [], diff, "previewed", c.ActorId);
        _undo.Push(Current); Current = c.Candidate; _redo.Clear(); return Result(true, true, Current, [], diff, "applied", c.ActorId);
    }
    public LiveDirectorAuthorityResult Undo(string actor, NetworkRole role) => Move(_undo, _redo, actor, role, "undo"); public LiveDirectorAuthorityResult Redo(string actor, NetworkRole role) => Move(_redo, _undo, actor, role, "redo");
    private LiveDirectorAuthorityResult Move(Stack<LiveDirectorAuthorityProfile> from, Stack<LiveDirectorAuthorityProfile> to, string actor, NetworkRole role, string action) { if (from.Count == 0 || role is not (NetworkRole.GameMaster or NetworkRole.Administrator)) return Result(false, false, Current, [new("LIVE_AUTHORITY_HISTORY_DENIED", "/liveDirector/authority/history", "History unavailable.", Current.Id)], "no changes", "rejected", actor); var before = Current; to.Push(before); Current = from.Pop(); return Result(true, true, Current, [], "authority-actions", $"compensated:{action}", actor); }
    private static LiveDirectorAuthorityResult Result(bool ok, bool applied, LiveDirectorAuthorityProfile p, IReadOnlyList<NetworkDiagnostic> d, string diff, string action, string actor) => new(ok, applied, p, d, diff, $"{action}:live.authority:actor={actor}:revision={p.ServerRevision}");
}
public sealed class LiveDirectorAuthorityStore
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public string Serialize(LiveDirectorAuthorityProfile p) { var d = LiveDirectorAuthorityValidator.Validate(p); if (d.Count != 0) throw new InvalidDataException(d[0].Code); return JsonSerializer.Serialize(p, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n"; }
    public LiveDirectorAuthorityProfile Deserialize(string json) { var p = JsonSerializer.Deserialize<LiveDirectorAuthorityProfile>(json, Options) ?? throw new InvalidDataException("LIVE_AUTHORITY_EMPTY"); var d = LiveDirectorAuthorityValidator.Validate(p); if (d.Count != 0) throw new InvalidDataException(d[0].Code); return p; }
}
public sealed record LiveDirectorAuthorityResolution(bool Available, LiveDirectorAuthorityProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);
public static class LiveDirectorAuthorityRuntimeAdapter { public static LiveDirectorAuthorityResolution Resolve(LiveDirectorAuthorityProfile? p, bool required) { if (p is null) return required ? new(false, null, [new("LIVE_AUTHORITY_REQUIRED_MISSING", "/liveDirector/authority", "Required authority profile is missing.")]) : new(false, null, []); var d = LiveDirectorAuthorityValidator.Validate(p); return new(d.Count == 0, d.Count == 0 ? p : null, d); } }
public readonly record struct LiveDirectorAuthorityHandle(Guid ProfileId, int Generation);
public sealed record LiveDirectorAuthoritySnapshot(Guid Id, long Revision, int ImpersonationModes, int ProtectionMinutes, long CurrencyCap, long ExperienceCap, int DoorStates);
public sealed class LiveDirectorAuthorityScriptService(LiveDirectorAuthorityProfile p) { public LiveDirectorAuthorityHandle Handle { get; } = new(p.Id, 1); public LiveDirectorAuthoritySnapshot Read(LiveDirectorAuthorityHandle h) { if (h.ProfileId != p.Id || h.Generation != 1) throw new InvalidOperationException("LIVE_AUTHORITY_HANDLE_INVALID"); return new(p.Id, p.ServerRevision, p.ImpersonationModes.Count, p.MaximumProtectionMinutes, p.MaximumCurrencyAdjustment, p.MaximumExperienceAdjustment, p.DoorStates.Count); } }
