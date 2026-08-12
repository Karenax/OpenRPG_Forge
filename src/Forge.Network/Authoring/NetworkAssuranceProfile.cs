using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Quality;
using Forge.Network.Observability;
using Forge.Network.Operations;
using Forge.Network.Security;
using Forge.Network.Sessions;
using Forge.Network.Testing;

namespace Forge.Network.Authoring;

public sealed record NetworkAssuranceProfile(Guid Id, int SchemaVersion, string Provenance,
    NetworkAuthorityScope AuthorityScope, long ServerRevision, string AuditIdentity,
    bool RequiredAtRuntime, NetworkStructuredLogPolicy Logs, AntiCheatPolicy AntiCheat,
    DeterministicNetworkTestPolicy DeterministicTests)
{
    public static NetworkAssuranceProfile CreateDefault(string provenance = "openrpg:r104")
    {
        static Guid Id(int suffix) => Guid.Parse($"a1040000-0000-4000-8000-{suffix:D12}");
        return new(Id(1), 1, provenance, NetworkAuthorityScope.Server, 0,
            "network.assurance.author", false,
            new(Id(2), 1, StructuredLogSeverity.Information, ["network", "security", "runtime"],
                PlayerPrivacyMode.Hash, StructuredLogOutputFormat.JsonAndText, 30, 100_000,
                4096, 4, [new(Id(10), 1, "audit", RemoteLogSinkKind.Syslog,
                    "udp://127.0.0.1:5514", true, true)]),
            new(Id(3), 1, 12, 8, 20, true, [new string('a', 64)], true, false, 2026),
            new(Id(4), 1, 3, 2, 5, 7, true, 3, 6, 42));
    }
}

public static class NetworkAssuranceValidator
{
    public static IReadOnlyList<NetworkDiagnostic> Validate(NetworkAssuranceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<NetworkDiagnostic>();
        var ids = new[] { profile.Id, profile.Logs.Id, profile.AntiCheat.Id, profile.DeterministicTests.Id }
            .Concat(profile.Logs.RemoteSinks.Select(item => item.Id)).ToArray();
        if (ids.Any(item => item == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            Add("NET_ASSURANCE_UUID_UNIQUE_REQUIRED", "/network/assurance", "All assurance UUIDs must be non-empty and unique.", profile.Id, diagnostics);
        if (new[] { profile.SchemaVersion, profile.Logs.SchemaVersion, profile.AntiCheat.SchemaVersion,
                profile.DeterministicTests.SchemaVersion }.Concat(profile.Logs.RemoteSinks.Select(item => item.SchemaVersion))
            .Any(item => item != 1))
            Add("NET_ASSURANCE_SCHEMA_UNSUPPORTED", "/network/assurance/schemaVersion", "Only network assurance schema v1 is supported.", profile.Id, diagnostics);
        if (string.IsNullOrWhiteSpace(profile.Provenance))
            Add("NET_ASSURANCE_PROVENANCE_REQUIRED", "/network/assurance/provenance", "Modification provenance is required.", profile.Id, diagnostics);
        if (profile.AuthorityScope != NetworkAuthorityScope.Server)
            Add("NET_ASSURANCE_SCOPE_INVALID", "/network/assurance/authorityScope", "Server authority scope is required.", profile.Id, diagnostics);
        if (profile.ServerRevision < 0)
            Add("NET_ASSURANCE_REVISION_INVALID", "/network/assurance/serverRevision", "Server revision cannot be negative.", profile.Id, diagnostics);
        if (string.IsNullOrWhiteSpace(profile.AuditIdentity))
            Add("NET_ASSURANCE_AUDIT_REQUIRED", "/network/assurance/auditIdentity", "An audit identity is required.", profile.Id, diagnostics);

        var logs = profile.Logs;
        if (logs.Categories.Count == 0 || logs.Categories.Any(string.IsNullOrWhiteSpace) ||
            logs.Categories.Distinct(StringComparer.OrdinalIgnoreCase).Count() != logs.Categories.Count ||
            logs.RetentionDays is < 1 or > 365 || logs.MaximumEntries is < 1 or > 1_000_000 ||
            logs.RotationBytes is < 256 or > 1_073_741_824 || logs.RotationFiles is < 1 or > 100 ||
            logs.RemoteSinks.Count == 0 || logs.RemoteSinks.Any(item => string.IsNullOrWhiteSpace(item.Name) ||
                !Uri.TryCreate(item.Endpoint, UriKind.Absolute, out _)) ||
            logs.RemoteSinks.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() != logs.RemoteSinks.Count)
            Add("NET_ASSURANCE_LOG_POLICY_INVALID", "/network/assurance/logs", "Structured logs require categories, privacy, bounded rotation and valid remote sinks.", logs.Id, diagnostics);

        var antiCheat = profile.AntiCheat;
        if (antiCheat.MaximumUnitsPerSecond is <= 0 or > 1_000 ||
            antiCheat.MaximumIntentsPerSecond is < 1 or > 10_000 ||
            antiCheat.AnomalyThreshold is < 1 or > 10_000 || !antiCheat.RequireSignedContent ||
            antiCheat.TrustedSignerFingerprints.Count == 0 ||
            antiCheat.TrustedSignerFingerprints.Any(item => item.Length != 64 || !item.All(Uri.IsHexDigit)) ||
            !antiCheat.AuthoritativeRandom || antiCheat.TrustClientSaves)
            Add("NET_ASSURANCE_ANTICHEAT_POLICY_INVALID", "/network/assurance/antiCheat", "Anti-cheat requires bounded intents, signed content, authoritative RNG and untrusted client saves.", antiCheat.Id, diagnostics);

        var tests = profile.DeterministicTests;
        if (tests.ClientCount is < 2 or > 128 || tests.LatencyTicks is < 0 or > 10_000 ||
            tests.DropEvery is < 0 or 1 || tests.DuplicateEvery is < 0 or 1 ||
            tests.ReplayPasses is < 2 or > 100 || tests.SoakCycles is < 1 or > 100_000 ||
            tests.ExpectedAuthoritativeRevision < 0)
            Add("NET_ASSURANCE_TEST_POLICY_INVALID", "/network/assurance/deterministicTests", "Deterministic tests require simulated clients, bounded faults, replay, soak and a valid authoritative revision.", tests.Id, diagnostics);
        return diagnostics;
    }

    private static void Add(string code, string path, string message, Guid id,
        ICollection<NetworkDiagnostic> diagnostics) => diagnostics.Add(new(code, path, message, id));
}

public sealed record NetworkAssuranceCommand(Guid CommandId, int SchemaVersion, string Provenance,
    long ExpectedRevision, string ActorId, NetworkRole Role, bool Apply, NetworkAssuranceProfile Candidate);

public sealed record NetworkAssuranceResult(bool Succeeded, bool Applied, NetworkAssuranceProfile Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics, string SemanticDiff, string AuditEntry);

public sealed class NetworkAssuranceHistory
{
    private readonly Stack<NetworkAssuranceProfile> _undo = [];
    private readonly Stack<NetworkAssuranceProfile> _redo = [];

    public NetworkAssuranceHistory(NetworkAssuranceProfile initial)
    {
        if (NetworkAssuranceValidator.Validate(initial).Count != 0)
            throw new ArgumentException("NET_ASSURANCE_INITIAL_INVALID", nameof(initial));
        Current = initial;
    }

    public NetworkAssuranceProfile Current { get; private set; }

    public NetworkAssuranceResult Execute(NetworkAssuranceCommand command)
    {
        var diagnostics = NetworkAssuranceValidator.Validate(command.Candidate).ToList();
        if (command.CommandId == Guid.Empty || command.SchemaVersion != 1 || string.IsNullOrWhiteSpace(command.Provenance))
            diagnostics.Add(new("NET_ASSURANCE_COMMAND_INVALID", "/network/assurance/command", "Command UUID, schema and provenance are required.", command.CommandId));
        if (string.IsNullOrWhiteSpace(command.ActorId))
            diagnostics.Add(new("NET_ASSURANCE_ACTOR_REQUIRED", "/network/assurance/audit/actorId", "An author identity is required.", command.Candidate.Id));
        if (command.Role != NetworkRole.Administrator)
            diagnostics.Add(new("NET_ASSURANCE_PERMISSION_DENIED", "/network/assurance/audit/role", "Administrator permission is required.", command.Candidate.Id));
        if (command.ExpectedRevision != Current.ServerRevision)
            diagnostics.Add(new("NET_ASSURANCE_REVISION_STALE", "/network/assurance/serverRevision", "The assurance profile revision is stale.", command.Candidate.Id));
        if (command.Candidate.ServerRevision != Current.ServerRevision + 1)
            diagnostics.Add(new("NET_ASSURANCE_NEXT_REVISION_REQUIRED", "/network/assurance/serverRevision", "Candidate revision must advance exactly once.", command.Candidate.Id));
        var diff = Diff(Current, command.Candidate);
        if (diagnostics.Count != 0)
            return new(false, false, Current, diagnostics, diff,
                $"rejected:network.assurance:actor={command.ActorId}:revision={Current.ServerRevision}");
        if (!command.Apply)
            return new(true, false, Current, [], diff,
                $"previewed:network.assurance:actor={command.ActorId}:revision={Current.ServerRevision}");
        _undo.Push(Current);
        Current = command.Candidate;
        _redo.Clear();
        return new(true, true, Current, [], diff,
            $"applied:network.assurance:actor={command.ActorId}:revision={Current.ServerRevision}");
    }

    public NetworkAssuranceResult Undo(string actorId, NetworkRole role) => Move(_undo, _redo, "undo", actorId, role);
    public NetworkAssuranceResult Redo(string actorId, NetworkRole role) => Move(_redo, _undo, "redo", actorId, role);

    private NetworkAssuranceResult Move(Stack<NetworkAssuranceProfile> source,
        Stack<NetworkAssuranceProfile> destination, string action, string actorId, NetworkRole role)
    {
        if (source.Count == 0 || role != NetworkRole.Administrator)
            return new(false, false, Current,
                [new("NET_ASSURANCE_HISTORY_DENIED", "/network/assurance/history", "History action is unavailable or not permitted.", Current.Id)],
                "no changes", $"rejected:network.assurance:{action}:actor={actorId}:revision={Current.ServerRevision}");
        var previous = Current;
        destination.Push(previous);
        Current = source.Pop();
        return new(true, true, Current, [], Diff(previous, Current),
            $"compensated:network.assurance:{action}:actor={actorId}:revision={Current.ServerRevision}");
    }

    private static string Diff(NetworkAssuranceProfile before, NetworkAssuranceProfile after)
    {
        var changes = new List<string>();
        if (before.Logs != after.Logs) changes.Add("structured-logs");
        if (before.AntiCheat != after.AntiCheat) changes.Add("anti-cheat");
        if (before.DeterministicTests != after.DeterministicTests) changes.Add("deterministic-tests");
        return changes.Count == 0 ? "revision-only" : string.Join(',', changes);
    }
}

public sealed class NetworkAssuranceStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string Serialize(NetworkAssuranceProfile profile)
    {
        var diagnostics = NetworkAssuranceValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return JsonSerializer.Serialize(profile, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public NetworkAssuranceProfile Deserialize(string json)
    {
        var profile = JsonSerializer.Deserialize<NetworkAssuranceProfile>(json, Options)
            ?? throw new InvalidDataException("NET_ASSURANCE_EMPTY");
        var diagnostics = NetworkAssuranceValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return profile;
    }
}

public readonly record struct NetworkAssuranceHandle(Guid ProfileId, int Generation);
public readonly record struct NetworkLogPolicyRef(Guid ProfileId, Guid PolicyId, int Generation);
public readonly record struct AntiCheatPolicyRef(Guid ProfileId, Guid PolicyId, int Generation);
public readonly record struct DeterministicNetworkTestPolicyRef(Guid ProfileId, Guid PolicyId, int Generation);

public sealed record NetworkAssuranceScriptSnapshot(Guid Id, long Revision, int LogCategories,
    int RemoteSinks, int AnomalyThreshold, int SimulatedClients, int ReplayPasses, string Provenance);

public sealed class NetworkAssuranceScriptService(NetworkAssuranceProfile profile)
{
    private const int Generation = 1;
    public NetworkAssuranceHandle Handle { get; } = new(profile.Id, Generation);

    public NetworkAssuranceScriptSnapshot Read(NetworkAssuranceHandle handle)
    {
        Validate(handle);
        return new(profile.Id, profile.ServerRevision, profile.Logs.Categories.Count,
            profile.Logs.RemoteSinks.Count, profile.AntiCheat.AnomalyThreshold,
            profile.DeterministicTests.ClientCount, profile.DeterministicTests.ReplayPasses,
            profile.Provenance);
    }

    public NetworkLogPolicyRef Logs(NetworkAssuranceHandle handle) => new(Read(handle).Id, profile.Logs.Id, Generation);
    public AntiCheatPolicyRef AntiCheat(NetworkAssuranceHandle handle) => new(Read(handle).Id, profile.AntiCheat.Id, Generation);
    public DeterministicNetworkTestPolicyRef Tests(NetworkAssuranceHandle handle) => new(Read(handle).Id, profile.DeterministicTests.Id, Generation);

    private void Validate(NetworkAssuranceHandle handle)
    {
        if (handle.ProfileId != profile.Id || handle.Generation != Generation)
            throw new InvalidOperationException("NET_ASSURANCE_HANDLE_INVALID");
    }
}

public sealed record NetworkAssuranceResolution(bool Available, NetworkAssuranceProfile? Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics);

public sealed record NetworkAssuranceRuntime(StructuredLogRouter Logs, AntiCheatService AntiCheat,
    DeterministicNetworkTestPolicy DeterministicTests, NetworkAssuranceScriptService Scripts);

public static class NetworkAssuranceRuntimeAdapter
{
    public static NetworkAssuranceResolution Resolve(NetworkAssuranceProfile? profile, bool required)
    {
        if (profile is null)
            return required
                ? new(false, null, [new("NET_ASSURANCE_REQUIRED_MISSING", "/network/assurance", "Required network assurance profile is missing.")])
                : new(false, null, []);
        var diagnostics = NetworkAssuranceValidator.Validate(profile);
        return diagnostics.Count == 0 ? new(true, profile, []) : new(false, null, diagnostics);
    }

    public static NetworkAssuranceRuntime Build(NetworkAssuranceProfile profile,
        TimeProvider? timeProvider = null, IReadOnlyList<IRemoteLogSink>? sinks = null)
    {
        var resolution = Resolve(profile, profile.RequiredAtRuntime);
        if (!resolution.Available) throw new InvalidOperationException(resolution.Diagnostics[0].Code);
        return new(new(profile.Logs, timeProvider ?? TimeProvider.System, sinks),
            new(profile.AntiCheat), profile.DeterministicTests, new(profile));
    }
}
