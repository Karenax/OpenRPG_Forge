using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Network.Authoring;

public enum NetworkAuthorityScope { Server }
public enum DedicatedServiceMode { Console, WindowsService }
public enum PartyLootPolicy { Leader, RoundRobin, NeedBeforeGreed }
public enum PartySeparationPolicy { Allow, Warn, Block }

public sealed record DedicatedServerPolicy(
    Guid Id,
    int SchemaVersion,
    bool Headless,
    bool EditorDependencyAllowed,
    DedicatedServiceMode ServiceMode,
    string HealthCheckPath);

public sealed record AuthoritativeGameplayPolicy(
    Guid Id,
    int SchemaVersion,
    bool ValidateAllMutations,
    bool ClientsSendIntentsOnly,
    bool ReplicateAcceptedState,
    bool AuditRejectedIntents);

public sealed record PartyFoundationPolicy(
    Guid Id,
    int SchemaVersion,
    int MaximumMembers,
    bool InvitationsEnabled,
    bool RolesEnabled,
    PartyLootPolicy Loot,
    bool ShareQuests,
    bool ShareMarkers,
    PartySeparationPolicy Separation);

public sealed record ComponentReplicationPolicy(
    string ComponentId,
    bool DeltaCompression,
    bool OwnerPrediction,
    bool Reconciliation);

public sealed record EntityReplicationPolicy(
    Guid Id,
    int SchemaVersion,
    IReadOnlyList<ComponentReplicationPolicy> Components,
    int MaximumDeltaBytes);

public sealed record InterestManagementPolicy(
    Guid Id,
    int SchemaVersion,
    bool ByArea,
    bool ByDistance,
    bool ByVisibility,
    bool ByParty,
    bool ByOwnership,
    bool ExplicitSubscriptions,
    float MaximumDistance);

public sealed record NetworkFoundationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    NetworkAuthorityScope AuthorityScope,
    long ServerRevision,
    string AuditIdentity,
    bool RequiredAtRuntime,
    DedicatedServerConfiguration Server,
    DedicatedServerPolicy Dedicated,
    AuthoritativeGameplayPolicy Authority,
    PartyFoundationPolicy Party,
    EntityReplicationPolicy Replication,
    InterestManagementPolicy Interest)
{
    public static NetworkFoundationProfile CreateDefault(string provenance = "openrpg:r102") => new(
        Guid.Parse("a1020000-0000-4000-8000-000000000001"),
        1,
        provenance,
        NetworkAuthorityScope.Server,
        0,
        "network.author",
        false,
        new(Guid.Parse("a1020000-0000-4000-8000-000000000002"), 1, "Serveur OpenRPG Forge",
            "127.0.0.1", 5121, 8, DiscoveryMode.Lan, "module-build-fingerprint", "1"),
        new(Guid.Parse("a1020000-0000-4000-8000-000000000003"), 1, true, false,
            DedicatedServiceMode.Console, "/health"),
        new(Guid.Parse("a1020000-0000-4000-8000-000000000004"), 1, true, true, true, true),
        new(Guid.Parse("a1020000-0000-4000-8000-000000000005"), 1, 8, true, true,
            PartyLootPolicy.NeedBeforeGreed, true, true, PartySeparationPolicy.Warn),
        new(Guid.Parse("a1020000-0000-4000-8000-000000000006"), 1,
            [new("transform", true, true, true), new("gameplay", true, false, true)], 262_144),
        new(Guid.Parse("a1020000-0000-4000-8000-000000000007"), 1, true, true, true, true, true,
            true, 250));
}

public static class NetworkFoundationValidator
{
    public static IReadOnlyList<NetworkDiagnostic> Validate(NetworkFoundationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<NetworkDiagnostic>();
        RequiredId(profile.Id, "/network/id", diagnostics);
        SupportedSchema(profile.SchemaVersion, "/network/schemaVersion", profile.Id, diagnostics);
        if (string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(new("NET_PROVENANCE_REQUIRED", "/network/provenance", "Modification provenance is required.", profile.Id));
        if (profile.AuthorityScope != NetworkAuthorityScope.Server)
            diagnostics.Add(new("NET_AUTHORITY_SCOPE_INVALID", "/network/authorityScope", "Server authority is required.", profile.Id));
        if (profile.ServerRevision < 0)
            diagnostics.Add(new("NET_SERVER_REVISION_INVALID", "/network/serverRevision", "Server revision cannot be negative.", profile.Id));
        if (string.IsNullOrWhiteSpace(profile.AuditIdentity))
            diagnostics.Add(new("NET_AUDIT_IDENTITY_REQUIRED", "/network/auditIdentity", "An audit identity is required.", profile.Id));

        diagnostics.AddRange(NetworkValidation.Validate(profile.Server));
        var ids = new[] { profile.Id, profile.Server.ServerId, profile.Dedicated.Id, profile.Authority.Id,
            profile.Party.Id, profile.Replication.Id, profile.Interest.Id };
        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            diagnostics.Add(new("NET_UUID_UNIQUE_REQUIRED", "/network", "All network object UUIDs must be non-empty and unique.", profile.Id));

        SupportedSchema(profile.Server.SchemaVersion, "/network/server/schemaVersion", profile.Server.ServerId, diagnostics);
        SupportedSchema(profile.Dedicated.SchemaVersion, "/network/dedicated/schemaVersion", profile.Dedicated.Id, diagnostics);
        SupportedSchema(profile.Authority.SchemaVersion, "/network/authority/schemaVersion", profile.Authority.Id, diagnostics);
        SupportedSchema(profile.Party.SchemaVersion, "/network/party/schemaVersion", profile.Party.Id, diagnostics);
        SupportedSchema(profile.Replication.SchemaVersion, "/network/replication/schemaVersion", profile.Replication.Id, diagnostics);
        SupportedSchema(profile.Interest.SchemaVersion, "/network/interest/schemaVersion", profile.Interest.Id, diagnostics);

        if (!profile.Dedicated.Headless || profile.Dedicated.EditorDependencyAllowed)
            diagnostics.Add(new("NET_DEDICATED_HEADLESS_REQUIRED", "/network/dedicated/headless", "Dedicated hosting must be headless and editor independent.", profile.Dedicated.Id));
        if (string.IsNullOrWhiteSpace(profile.Dedicated.HealthCheckPath) || !profile.Dedicated.HealthCheckPath.StartsWith("/", StringComparison.Ordinal))
            diagnostics.Add(new("NET_HEALTH_PATH_INVALID", "/network/dedicated/healthCheckPath", "Health check path must be absolute.", profile.Dedicated.Id));
        if (!profile.Authority.ValidateAllMutations || !profile.Authority.ClientsSendIntentsOnly ||
            !profile.Authority.ReplicateAcceptedState || !profile.Authority.AuditRejectedIntents)
            diagnostics.Add(new("NET_SERVER_AUTHORITY_REQUIRED", "/network/authority", "All gameplay mutations must remain server authoritative and audited.", profile.Authority.Id));
        if (profile.Party.MaximumMembers is < 2 or > 64)
            diagnostics.Add(new("NET_PARTY_CAPACITY_INVALID", "/network/party/maximumMembers", "Party capacity must be between 2 and 64.", profile.Party.Id));
        if (!profile.Party.InvitationsEnabled || !profile.Party.RolesEnabled)
            diagnostics.Add(new("NET_PARTY_LIFECYCLE_REQUIRED", "/network/party", "Party invitations and roles are required.", profile.Party.Id));
        if (profile.Replication.Components.Count == 0 ||
            profile.Replication.Components.Any(item => string.IsNullOrWhiteSpace(item.ComponentId)) ||
            profile.Replication.Components.Select(item => item.ComponentId).Distinct(StringComparer.Ordinal).Count() != profile.Replication.Components.Count)
            diagnostics.Add(new("NET_REPLICATION_COMPONENTS_INVALID", "/network/replication/components", "Replication component IDs must be present and unique.", profile.Replication.Id));
        if (profile.Replication.MaximumDeltaBytes is < 1024 or > 4_194_304)
            diagnostics.Add(new("NET_REPLICATION_DELTA_BUDGET_INVALID", "/network/replication/maximumDeltaBytes", "Replication delta budget is outside supported bounds.", profile.Replication.Id));
        if (!(profile.Interest.ByArea || profile.Interest.ByDistance || profile.Interest.ByVisibility ||
              profile.Interest.ByParty || profile.Interest.ByOwnership || profile.Interest.ExplicitSubscriptions))
            diagnostics.Add(new("NET_INTEREST_RULE_REQUIRED", "/network/interest", "At least one interest rule is required.", profile.Interest.Id));
        if (profile.Interest.MaximumDistance is <= 0 or > 10_000)
            diagnostics.Add(new("NET_INTEREST_DISTANCE_INVALID", "/network/interest/maximumDistance", "Interest distance must be greater than zero and at most 10000.", profile.Interest.Id));
        return diagnostics;
    }

    private static void RequiredId(Guid id, string path, ICollection<NetworkDiagnostic> diagnostics)
    {
        if (id == Guid.Empty) diagnostics.Add(new("NET_ID_REQUIRED", path, "A stable UUID is required."));
    }

    private static void SupportedSchema(int schema, string path, Guid id, ICollection<NetworkDiagnostic> diagnostics)
    {
        if (schema != 1) diagnostics.Add(new("NET_SCHEMA_UNSUPPORTED", path, "Only network schema v1 is supported.", id));
    }
}

public sealed record NetworkFoundationCommand(
    Guid CommandId,
    int SchemaVersion,
    string Provenance,
    long ExpectedRevision,
    string ActorId,
    NetworkRole Role,
    bool Apply,
    NetworkFoundationProfile Candidate);

public sealed record NetworkFoundationResult(
    bool Succeeded,
    bool Applied,
    NetworkFoundationProfile Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics,
    string SemanticDiff,
    string AuditEntry);

public sealed class NetworkFoundationHistory
{
    private readonly Stack<NetworkFoundationProfile> _undo = [];
    private readonly Stack<NetworkFoundationProfile> _redo = [];

    public NetworkFoundationHistory(NetworkFoundationProfile initial)
    {
        if (NetworkFoundationValidator.Validate(initial).Count != 0) throw new ArgumentException("NET_FOUNDATION_INITIAL_INVALID", nameof(initial));
        Current = initial;
    }

    public NetworkFoundationProfile Current { get; private set; }

    public NetworkFoundationResult Execute(NetworkFoundationCommand command)
    {
        var diagnostics = NetworkFoundationValidator.Validate(command.Candidate).ToList();
        if (command.CommandId == Guid.Empty || command.SchemaVersion != 1 || string.IsNullOrWhiteSpace(command.Provenance))
            diagnostics.Add(new("NET_COMMAND_IDENTITY_INVALID", "/network/command", "Command UUID, schema and provenance are required.", command.CommandId));
        if (string.IsNullOrWhiteSpace(command.ActorId))
            diagnostics.Add(new("NET_AUTHOR_ID_REQUIRED", "/network/audit/actorId", "An author identity is required.", command.Candidate.Id));
        if (command.Role != NetworkRole.Administrator)
            diagnostics.Add(new("NET_PERMISSION_DENIED", "/network/audit/role", "Administrator permission is required.", command.Candidate.Id));
        if (command.ExpectedRevision != Current.ServerRevision)
            diagnostics.Add(new("NET_REVISION_STALE", "/network/serverRevision", "The network configuration revision is stale.", command.Candidate.Id));
        if (command.Candidate.ServerRevision != Current.ServerRevision + 1)
            diagnostics.Add(new("NET_NEXT_REVISION_REQUIRED", "/network/serverRevision", "Candidate revision must advance exactly once.", command.Candidate.Id));
        var diff = BuildDiff(Current, command.Candidate);
        if (diagnostics.Count != 0)
            return new(false, false, Current, diagnostics, diff, $"rejected:network.foundation:actor={command.ActorId}:revision={Current.ServerRevision}");
        if (!command.Apply)
            return new(true, false, Current, diagnostics, diff, $"previewed:network.foundation:actor={command.ActorId}:revision={Current.ServerRevision}");
        _undo.Push(Current);
        Current = command.Candidate;
        _redo.Clear();
        return new(true, true, Current, diagnostics, diff, $"applied:network.foundation:actor={command.ActorId}:revision={Current.ServerRevision}");
    }

    public NetworkFoundationResult Undo(string actorId, NetworkRole role)
    {
        if (_undo.Count == 0 || role != NetworkRole.Administrator)
            return RejectedHistory("undo", actorId);
        _redo.Push(Current);
        var previous = Current;
        Current = _undo.Pop();
        return new(true, true, Current, [], BuildDiff(previous, Current), $"compensated:network.foundation:undo:actor={actorId}:revision={Current.ServerRevision}");
    }

    public NetworkFoundationResult Redo(string actorId, NetworkRole role)
    {
        if (_redo.Count == 0 || role != NetworkRole.Administrator)
            return RejectedHistory("redo", actorId);
        _undo.Push(Current);
        var previous = Current;
        Current = _redo.Pop();
        return new(true, true, Current, [], BuildDiff(previous, Current), $"compensated:network.foundation:redo:actor={actorId}:revision={Current.ServerRevision}");
    }

    private NetworkFoundationResult RejectedHistory(string action, string actorId) => new(false, false, Current,
        [new("NET_HISTORY_ACTION_DENIED", "/network/history", "History action is unavailable or not permitted.", Current.Id)],
        "no changes", $"rejected:network.foundation:{action}:actor={actorId}:revision={Current.ServerRevision}");

    private static string BuildDiff(NetworkFoundationProfile before, NetworkFoundationProfile after)
    {
        var changes = new List<string>();
        if (before.Server != after.Server || before.Dedicated != after.Dedicated) changes.Add("dedicated-server");
        if (before.Authority != after.Authority) changes.Add("authority");
        if (before.Party != after.Party) changes.Add("party");
        if (before.Replication != after.Replication) changes.Add("replication");
        if (before.Interest != after.Interest) changes.Add("interest");
        return changes.Count == 0 ? "revision-only" : string.Join(',', changes);
    }
}

public sealed class NetworkFoundationStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string Serialize(NetworkFoundationProfile profile)
    {
        var diagnostics = NetworkFoundationValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return JsonSerializer.Serialize(profile, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public NetworkFoundationProfile Deserialize(string json)
    {
        var profile = JsonSerializer.Deserialize<NetworkFoundationProfile>(json, Options)
            ?? throw new InvalidDataException("NET_FOUNDATION_EMPTY");
        var diagnostics = NetworkFoundationValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return profile;
    }

    public void Save(string path, NetworkFoundationProfile profile)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + ".tmp";
        File.WriteAllText(temporary, Serialize(profile));
        File.Move(temporary, fullPath, true);
    }

    public NetworkFoundationProfile Load(string path) => Deserialize(File.ReadAllText(Path.GetFullPath(path)));
}

public readonly record struct NetworkFoundationHandle(Guid ProfileId, int Generation);
public readonly record struct NetworkPartyPolicyRef(Guid ProfileId, Guid PolicyId, int Generation);
public readonly record struct NetworkReplicationPolicyRef(Guid ProfileId, Guid PolicyId, int Generation);
public readonly record struct NetworkInterestPolicyRef(Guid ProfileId, Guid PolicyId, int Generation);

public sealed record NetworkFoundationScriptSnapshot(
    Guid Id,
    long Revision,
    string Authority,
    int PartyCapacity,
    int ReplicatedComponents,
    string Provenance);

public sealed class NetworkFoundationScriptService(NetworkFoundationProfile profile)
{
    private const int Generation = 1;
    public NetworkFoundationHandle Handle { get; } = new(profile.Id, Generation);

    public NetworkFoundationScriptSnapshot Read(NetworkFoundationHandle handle)
    {
        Validate(handle.ProfileId, profile.Id, handle.Generation, "NET_FOUNDATION_HANDLE_INVALID");
        return new(profile.Id, profile.ServerRevision, profile.AuthorityScope.ToString(),
            profile.Party.MaximumMembers, profile.Replication.Components.Count, profile.Provenance);
    }

    public NetworkPartyPolicyRef Party(NetworkFoundationHandle handle)
    {
        Read(handle);
        return new(profile.Id, profile.Party.Id, Generation);
    }

    public NetworkReplicationPolicyRef Replication(NetworkFoundationHandle handle)
    {
        Read(handle);
        return new(profile.Id, profile.Replication.Id, Generation);
    }

    public NetworkInterestPolicyRef Interest(NetworkFoundationHandle handle)
    {
        Read(handle);
        return new(profile.Id, profile.Interest.Id, Generation);
    }

    private static void Validate(Guid actualProfile, Guid expectedProfile, int generation, string code)
    {
        if (actualProfile != expectedProfile || generation != Generation) throw new InvalidOperationException(code);
    }
}
