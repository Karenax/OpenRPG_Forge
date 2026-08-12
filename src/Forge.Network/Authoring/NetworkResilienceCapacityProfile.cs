using System.Text.Json;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Network.Authoring;

public sealed record ServerBackupPolicy(Guid Id, int SnapshotIntervalMinutes, bool WriteAheadLog,
    bool RestoreRehearsal, bool PointInTimeRecovery, bool Migrations, int RetentionDays,
    bool DestructiveConfirmation);
public sealed record ServerCapacityPolicy(Guid Id, int MaximumPlayers, int ReservedAdministratorSlots,
    int ReservationSeconds, bool AdmissionQueue, bool LoadShedding, bool PositionVisible,
    bool MetricsEnabled);
public sealed record NetworkResilienceCapacityProfile(Guid Id, int SchemaVersion, string Provenance,
    NetworkAuthorityScope AuthorityScope, long ServerRevision, string AuditIdentity, bool RequiredAtRuntime,
    ServerBackupPolicy Backup, ServerCapacityPolicy Capacity)
{
    public static NetworkResilienceCapacityProfile CreateDefault(string provenance = "openrpg:r124") => new(
        Guid.Parse("a1240000-0000-4000-8000-000000000001"), 1, provenance, NetworkAuthorityScope.Server,
        0, "network.operator", false,
        new(Guid.Parse("a1240000-0000-4000-8000-000000000002"), 15, true, true, true, true, 30, true),
        new(Guid.Parse("a1240000-0000-4000-8000-000000000003"), 64, 4, 30, true, true, true, true));
}

public static class NetworkResilienceCapacityValidator
{
    public static IReadOnlyList<NetworkDiagnostic> Validate(NetworkResilienceCapacityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var result = new List<NetworkDiagnostic>();
        var ids = new[] { profile.Id, profile.Backup.Id, profile.Capacity.Id };
        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            Add("NET_RESILIENCE_UUID_INVALID", "/network/resilienceCapacity", "UUIDs must be unique.", profile.Id, result);
        if (profile.SchemaVersion != 1)
            Add("NET_RESILIENCE_SCHEMA_UNSUPPORTED", "/network/resilienceCapacity/schemaVersion", "Only schema v1 is supported.", profile.Id, result);
        if (string.IsNullOrWhiteSpace(profile.Provenance) || profile.AuthorityScope != NetworkAuthorityScope.Server ||
            profile.ServerRevision < 0 || string.IsNullOrWhiteSpace(profile.AuditIdentity))
            Add("NET_RESILIENCE_IDENTITY_INVALID", "/network/resilienceCapacity/identity", "Server identity is required.", profile.Id, result);
        if (profile.Backup.SnapshotIntervalMinutes is < 1 or > 10_080 || !profile.Backup.WriteAheadLog ||
            !profile.Backup.RestoreRehearsal || !profile.Backup.PointInTimeRecovery || !profile.Backup.Migrations ||
            profile.Backup.RetentionDays is < 1 or > 3_650 || !profile.Backup.DestructiveConfirmation)
            Add("NET_BACKUP_POLICY_INVALID", "/network/resilienceCapacity/backup", "Consistent WAL backups, rehearsal, migrations, PITR, retention and confirmation are required.", profile.Backup.Id, result);
        if (profile.Capacity.MaximumPlayers is < 1 or > 100_000 || profile.Capacity.ReservedAdministratorSlots < 0 ||
            profile.Capacity.ReservedAdministratorSlots >= profile.Capacity.MaximumPlayers ||
            profile.Capacity.ReservationSeconds is < 5 or > 600 || !profile.Capacity.AdmissionQueue ||
            !profile.Capacity.LoadShedding || !profile.Capacity.PositionVisible || !profile.Capacity.MetricsEnabled)
            Add("NET_CAPACITY_POLICY_INVALID", "/network/resilienceCapacity/capacity", "Caps, queue, reserved slots, load shedding, position and metrics are required.", profile.Capacity.Id, result);
        return result;
    }
    private static void Add(string code, string path, string message, Guid id, ICollection<NetworkDiagnostic> target) =>
        target.Add(new(code, path, message, id));
}

public sealed record NetworkResilienceCapacityCommand(Guid CommandId, int SchemaVersion, string Provenance,
    long ExpectedRevision, string ActorId, NetworkRole Role, bool Apply, NetworkResilienceCapacityProfile Candidate);
public sealed record NetworkResilienceCapacityResult(bool Succeeded, bool Applied,
    NetworkResilienceCapacityProfile Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics,
    string SemanticDiff, string AuditEntry);

public sealed class NetworkResilienceCapacityHistory
{
    private readonly Stack<NetworkResilienceCapacityProfile> _undo = [];
    private readonly Stack<NetworkResilienceCapacityProfile> _redo = [];
    public NetworkResilienceCapacityHistory(NetworkResilienceCapacityProfile initial)
    {
        if (NetworkResilienceCapacityValidator.Validate(initial).Count != 0) throw new ArgumentException("NET_RESILIENCE_INITIAL_INVALID");
        Current = initial;
    }
    public NetworkResilienceCapacityProfile Current { get; private set; }
    public NetworkResilienceCapacityResult Execute(NetworkResilienceCapacityCommand command)
    {
        var diagnostics = NetworkResilienceCapacityValidator.Validate(command.Candidate).ToList();
        if (command.CommandId == Guid.Empty || command.SchemaVersion != 1 || string.IsNullOrWhiteSpace(command.Provenance))
            diagnostics.Add(new("NET_RESILIENCE_COMMAND_INVALID", "/network/resilienceCapacity/command", "Command identity is required.", command.CommandId));
        if (string.IsNullOrWhiteSpace(command.ActorId) || command.Role != NetworkRole.Administrator)
            diagnostics.Add(new("NET_RESILIENCE_PERMISSION_DENIED", "/network/resilienceCapacity/audit/actor", "Administrator identity is required.", command.Candidate.Id));
        if (command.ExpectedRevision != Current.ServerRevision || command.Candidate.ServerRevision != Current.ServerRevision + 1)
            diagnostics.Add(new("NET_RESILIENCE_REVISION_STALE", "/network/resilienceCapacity/serverRevision", "Revision must advance exactly once.", command.Candidate.Id));
        var diff = Diff(Current, command.Candidate);
        if (diagnostics.Count != 0) return Result(false, false, Current, diagnostics, diff, "rejected", command.ActorId);
        if (!command.Apply) return Result(true, false, Current, [], diff, "previewed", command.ActorId);
        _undo.Push(Current); Current = command.Candidate; _redo.Clear();
        return Result(true, true, Current, [], diff, "applied", command.ActorId);
    }
    public NetworkResilienceCapacityResult Undo(string actor, NetworkRole role) => Move(_undo, _redo, actor, role, "undo");
    public NetworkResilienceCapacityResult Redo(string actor, NetworkRole role) => Move(_redo, _undo, actor, role, "redo");
    private NetworkResilienceCapacityResult Move(Stack<NetworkResilienceCapacityProfile> from, Stack<NetworkResilienceCapacityProfile> to, string actor, NetworkRole role, string action)
    {
        if (from.Count == 0 || role != NetworkRole.Administrator) return Result(false, false, Current,
            [new("NET_RESILIENCE_HISTORY_DENIED", "/network/resilienceCapacity/history", "History action unavailable.", Current.Id)], "no changes", "rejected", actor);
        var before = Current; to.Push(before); Current = from.Pop();
        return Result(true, true, Current, [], Diff(before, Current), $"compensated:{action}", actor);
    }
    private NetworkResilienceCapacityResult Result(bool ok, bool applied, NetworkResilienceCapacityProfile profile,
        IReadOnlyList<NetworkDiagnostic> diagnostics, string diff, string action, string actor) =>
        new(ok, applied, profile, diagnostics, diff, $"{action}:network.resilience-capacity:actor={actor}:revision={profile.ServerRevision}");
    private static string Diff(NetworkResilienceCapacityProfile before, NetworkResilienceCapacityProfile after) =>
        string.Join(',', new[] { before.Backup != after.Backup ? "backup" : null, before.Capacity != after.Capacity ? "capacity" : null }.Where(x => x is not null)) is { Length: > 0 } value ? value : "revision-only";
}

public sealed class NetworkResilienceCapacityStore
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public string Serialize(NetworkResilienceCapacityProfile profile)
    {
        var diagnostics = NetworkResilienceCapacityValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return JsonSerializer.Serialize(profile, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }
    public NetworkResilienceCapacityProfile Deserialize(string json)
    {
        var profile = JsonSerializer.Deserialize<NetworkResilienceCapacityProfile>(json, Options) ?? throw new InvalidDataException("NET_RESILIENCE_EMPTY");
        var diagnostics = NetworkResilienceCapacityValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return profile;
    }
}

public sealed record NetworkResilienceCapacityResolution(bool Available, NetworkResilienceCapacityProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);
public static class NetworkResilienceCapacityRuntimeAdapter
{
    public static NetworkResilienceCapacityResolution Resolve(NetworkResilienceCapacityProfile? profile, bool required)
    {
        if (profile is null) return required ? new(false, null, [new("NET_RESILIENCE_REQUIRED_MISSING", "/network/resilienceCapacity", "Required resilience profile is missing.")]) : new(false, null, []);
        var diagnostics = NetworkResilienceCapacityValidator.Validate(profile);
        return new(diagnostics.Count == 0, diagnostics.Count == 0 ? profile : null, diagnostics);
    }
}
public readonly record struct NetworkResilienceCapacityHandle(Guid ProfileId, int Generation);
public sealed record NetworkResilienceCapacitySnapshot(Guid Id, long Revision, int SnapshotMinutes, int MaximumPlayers, int ReservedSlots, int ReservationSeconds);
public sealed class NetworkResilienceCapacityScriptService(NetworkResilienceCapacityProfile profile)
{
    public NetworkResilienceCapacityHandle Handle { get; } = new(profile.Id, 1);
    public NetworkResilienceCapacitySnapshot Read(NetworkResilienceCapacityHandle handle)
    {
        if (handle.ProfileId != profile.Id || handle.Generation != 1) throw new InvalidOperationException("NET_RESILIENCE_HANDLE_INVALID");
        return new(profile.Id, profile.ServerRevision, profile.Backup.SnapshotIntervalMinutes,
            profile.Capacity.MaximumPlayers, profile.Capacity.ReservedAdministratorSlots, profile.Capacity.ReservationSeconds);
    }
}
