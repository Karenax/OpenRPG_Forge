using System.Text.Json;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.LiveDirector.Authoring;

public sealed record LiveDirectorGroupProfile(Guid Id, int SchemaVersion, string Provenance,
    NetworkAuthorityScope AuthorityScope, long ServerRevision, string AuditIdentity, bool RequiredAtRuntime,
    IReadOnlyList<string> TriggerEvents, bool TriggerActorRequired, bool ScriptTracePreview,
    int SelectionCap, bool PermissionFilteredSelection, bool StableSelectionIds, bool ViewportLasso,
    IReadOnlyList<string> MovementFormations, bool PathPreview, bool PartialFailureReport, bool RuntimePathAdapter,
    bool FactionSafetyAcknowledgement, bool AttackObjective, bool RuntimeCombatAdapter,
    IReadOnlyList<string> PlayerTravelModes, bool PlayerConfirmation, bool PartyConsent, bool CameraTransitionAdapter)
{
    public static LiveDirectorGroupProfile CreateDefault(string provenance = "openrpg:r128") => new(
        Guid.Parse("d1280000-0000-4000-8000-000000000001"), 1, provenance, NetworkAuthorityScope.Server,
        0, "live.director", false, ["enter", "exit"], true, true, 64, true, true, true,
        ["line", "wedge", "circle", "scatter"], true, true, true, true, true, true,
        ["spectate", "teleport-gm", "summon-player", "summon-party"], true, true, true);
}
public static class LiveDirectorGroupValidator
{
    public static IReadOnlyList<NetworkDiagnostic> Validate(LiveDirectorGroupProfile p)
    {
        var d = new List<NetworkDiagnostic>();
        if (p.Id == Guid.Empty) Add("LIVE_GROUP_UUID_INVALID", "/liveDirector/group", "UUID required.", p.Id, d);
        if (p.SchemaVersion != 1) Add("LIVE_GROUP_SCHEMA_UNSUPPORTED", "/liveDirector/group/schemaVersion", "Only schema v1 is supported.", p.Id, d);
        if (string.IsNullOrWhiteSpace(p.Provenance) || p.AuthorityScope != NetworkAuthorityScope.Server || p.ServerRevision < 0 || string.IsNullOrWhiteSpace(p.AuditIdentity)) Add("LIVE_GROUP_IDENTITY_INVALID", "/liveDirector/group/identity", "Server identity required.", p.Id, d);
        if (p.TriggerEvents.Count != 2 || !p.TriggerActorRequired || !p.ScriptTracePreview) Add("LIVE_TRIGGER_SIMULATION_POLICY_INVALID", "/liveDirector/group/trigger", "Enter/exit actor and script trace required.", p.Id, d);
        if (p.SelectionCap is < 1 or > 1024 || !p.PermissionFilteredSelection || !p.StableSelectionIds || !p.ViewportLasso) Add("LIVE_SELECTION_POLICY_INVALID", "/liveDirector/group/selection", "Bounded permission filtered lasso selection required.", p.Id, d);
        if (p.MovementFormations.Count < 4 || !p.PathPreview || !p.PartialFailureReport || !p.RuntimePathAdapter) Add("LIVE_GROUP_MOVEMENT_POLICY_INVALID", "/liveDirector/group/movement", "Formation path preview partial report and adapter required.", p.Id, d);
        if (!p.FactionSafetyAcknowledgement || !p.AttackObjective || !p.RuntimeCombatAdapter) Add("LIVE_GROUP_ATTACK_POLICY_INVALID", "/liveDirector/group/attack", "Faction safe attack objective adapter required.", p.Id, d);
        if (p.PlayerTravelModes.Count != 4 || !p.PlayerConfirmation || !p.PartyConsent || !p.CameraTransitionAdapter) Add("LIVE_PLAYER_TRAVEL_POLICY_INVALID", "/liveDirector/group/playerTravel", "Four confirmed consented travel modes and camera adapter required.", p.Id, d);
        return d;
    }
    private static void Add(string code, string path, string message, Guid id, ICollection<NetworkDiagnostic> d) => d.Add(new(code, path, message, id));
}
public sealed record LiveDirectorGroupCommand(Guid CommandId, int SchemaVersion, string Provenance, long ExpectedRevision, string ActorId, NetworkRole Role, bool Apply, LiveDirectorGroupProfile Candidate);
public sealed record LiveDirectorGroupResult(bool Succeeded, bool Applied, LiveDirectorGroupProfile Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics, string SemanticDiff, string AuditEntry);
public sealed class LiveDirectorGroupHistory
{
    private readonly Stack<LiveDirectorGroupProfile> _undo = []; private readonly Stack<LiveDirectorGroupProfile> _redo = [];
    public LiveDirectorGroupHistory(LiveDirectorGroupProfile initial) { if (LiveDirectorGroupValidator.Validate(initial).Count != 0) throw new ArgumentException("LIVE_GROUP_INITIAL_INVALID"); Current = initial; }
    public LiveDirectorGroupProfile Current { get; private set; }
    public LiveDirectorGroupResult Execute(LiveDirectorGroupCommand c) { var d = LiveDirectorGroupValidator.Validate(c.Candidate).ToList(); if (c.CommandId == Guid.Empty || c.SchemaVersion != 1 || string.IsNullOrWhiteSpace(c.Provenance)) d.Add(new("LIVE_GROUP_COMMAND_INVALID", "/liveDirector/group/command", "Command identity required.", c.CommandId)); if (string.IsNullOrWhiteSpace(c.ActorId) || c.Role is not (NetworkRole.GameMaster or NetworkRole.Administrator)) d.Add(new("LIVE_GROUP_PERMISSION_DENIED", "/liveDirector/group/audit/actor", "GM identity required.", c.Candidate.Id)); if (c.ExpectedRevision != Current.ServerRevision || c.Candidate.ServerRevision != Current.ServerRevision + 1) d.Add(new("LIVE_GROUP_REVISION_STALE", "/liveDirector/group/serverRevision", "Revision must advance once.", c.Candidate.Id)); const string diff = "trigger,selection,movement,attack,player-travel"; if (d.Count != 0) return Result(false, false, Current, d, diff, "rejected", c.ActorId); if (!c.Apply) return Result(true, false, Current, [], diff, "previewed", c.ActorId); _undo.Push(Current); Current = c.Candidate; _redo.Clear(); return Result(true, true, Current, [], diff, "applied", c.ActorId); }
    public LiveDirectorGroupResult Undo(string actor, NetworkRole role) => Move(_undo, _redo, actor, role, "undo"); public LiveDirectorGroupResult Redo(string actor, NetworkRole role) => Move(_redo, _undo, actor, role, "redo");
    private LiveDirectorGroupResult Move(Stack<LiveDirectorGroupProfile> from, Stack<LiveDirectorGroupProfile> to, string actor, NetworkRole role, string action) { if (from.Count == 0 || role is not (NetworkRole.GameMaster or NetworkRole.Administrator)) return Result(false, false, Current, [new("LIVE_GROUP_HISTORY_DENIED", "/liveDirector/group/history", "History unavailable.", Current.Id)], "no changes", "rejected", actor); var before = Current; to.Push(before); Current = from.Pop(); return Result(true, true, Current, [], "group-actions", $"compensated:{action}", actor); }
    private static LiveDirectorGroupResult Result(bool ok, bool applied, LiveDirectorGroupProfile p, IReadOnlyList<NetworkDiagnostic> d, string diff, string action, string actor) => new(ok, applied, p, d, diff, $"{action}:live.group:actor={actor}:revision={p.ServerRevision}");
}
public sealed class LiveDirectorGroupStore
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public string Serialize(LiveDirectorGroupProfile p) { var d = LiveDirectorGroupValidator.Validate(p); if (d.Count != 0) throw new InvalidDataException(d[0].Code); return JsonSerializer.Serialize(p, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n"; }
    public LiveDirectorGroupProfile Deserialize(string json) { var p = JsonSerializer.Deserialize<LiveDirectorGroupProfile>(json, Options) ?? throw new InvalidDataException("LIVE_GROUP_EMPTY"); var d = LiveDirectorGroupValidator.Validate(p); if (d.Count != 0) throw new InvalidDataException(d[0].Code); return p; }
}
public sealed record LiveDirectorGroupResolution(bool Available, LiveDirectorGroupProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);
public static class LiveDirectorGroupRuntimeAdapter { public static LiveDirectorGroupResolution Resolve(LiveDirectorGroupProfile? p, bool required) { if (p is null) return required ? new(false, null, [new("LIVE_GROUP_REQUIRED_MISSING", "/liveDirector/group", "Required group profile missing.")]) : new(false, null, []); var d = LiveDirectorGroupValidator.Validate(p); return new(d.Count == 0, d.Count == 0 ? p : null, d); } }
public readonly record struct LiveDirectorGroupHandle(Guid ProfileId, int Generation);
public sealed record LiveDirectorGroupSnapshot(Guid Id, long Revision, int SelectionCap, int Formations, int TravelModes, bool Lasso);
public sealed class LiveDirectorGroupScriptService(LiveDirectorGroupProfile p) { public LiveDirectorGroupHandle Handle { get; } = new(p.Id, 1); public LiveDirectorGroupSnapshot Read(LiveDirectorGroupHandle h) { if (h.ProfileId != p.Id || h.Generation != 1) throw new InvalidOperationException("LIVE_GROUP_HANDLE_INVALID"); return new(p.Id, p.ServerRevision, p.SelectionCap, p.MovementFormations.Count, p.PlayerTravelModes.Count, p.ViewportLasso); } }
