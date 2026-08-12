using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Network.Authoring;

public enum AuthenticationProviderKind { Local, OAuth, OpenIdConnect, Platform, Anonymous }

public sealed record AreaInstancePolicy(
    Guid Id,
    IReadOnlyList<string> TemplateIds,
    int MaximumInstances,
    int MaximumPlayersPerInstance,
    bool PartyAllocation,
    bool PersistenceEnabled,
    bool RuntimeSceneInstantiation,
    int GarbageCollectionMinutes);

public sealed record DynamicAreaPolicy(
    Guid Id,
    bool TemplateInstantiation,
    bool CloneExistingArea,
    IReadOnlyList<string> AllowedOverrideKeys,
    int MaximumEntities,
    int MaximumResourceMegabytes,
    bool ServerAuthorityRequired);

public sealed record AuthenticationPolicy(
    Guid Id,
    IReadOnlyList<AuthenticationProviderKind> Providers,
    bool StableAccountId,
    bool PrivacyByDefault,
    bool SecretsHashed,
    bool NonEnumeratingFailures,
    int SessionLifetimeMinutes);

public sealed record RemoteAdministrationPolicy(
    Guid Id,
    bool ApiEnabled,
    bool CliEnabled,
    bool AuthenticationRequired,
    int RequestsPerMinute,
    bool AuditEnabled,
    bool HealthCommands,
    bool PlayerCommands,
    bool ConfigurationCommands,
    IReadOnlyList<string> SafeCommandIds);

public sealed record ModerationPolicy(
    Guid Id,
    bool AccountAdapter,
    bool IpAdapter,
    bool DeviceAdapter,
    bool Mute,
    bool Timeout,
    bool Kick,
    bool Ban,
    bool Reports,
    bool Evidence,
    bool AppealNotes,
    int PrivacyRetentionDays);

public sealed record NetworkWorldAdministrationProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    NetworkAuthorityScope AuthorityScope,
    long ServerRevision,
    string AuditIdentity,
    bool RequiredAtRuntime,
    AreaInstancePolicy Instances,
    DynamicAreaPolicy DynamicAreas,
    AuthenticationPolicy Authentication,
    RemoteAdministrationPolicy Administration,
    ModerationPolicy Moderation)
{
    public static NetworkWorldAdministrationProfile CreateDefault(string provenance = "openrpg:r122") => new(
        Guid.Parse("a1220000-0000-4000-8000-000000000001"), 1, provenance,
        NetworkAuthorityScope.Server, 0, "network.operator", false,
        new(Guid.Parse("a1220000-0000-4000-8000-000000000002"), ["area:crypt-template"],
            32, 16, true, true, true, 15),
        new(Guid.Parse("a1220000-0000-4000-8000-000000000003"), true, true,
            ["environment", "encounters", "entry-point"], 2_000, 1_024, true),
        new(Guid.Parse("a1220000-0000-4000-8000-000000000004"),
            [AuthenticationProviderKind.Local, AuthenticationProviderKind.OAuth,
                AuthenticationProviderKind.OpenIdConnect, AuthenticationProviderKind.Platform,
                AuthenticationProviderKind.Anonymous], true, true, true, true, 120),
        new(Guid.Parse("a1220000-0000-4000-8000-000000000005"), true, true, true, 120,
            true, true, true, true, ["health.read", "players.kick", "players.ban", "config.preview"]),
        new(Guid.Parse("a1220000-0000-4000-8000-000000000006"), true, true, true,
            true, true, true, true, true, true, true, 90));
}

public static class NetworkWorldAdministrationValidator
{
    public static IReadOnlyList<NetworkDiagnostic> Validate(NetworkWorldAdministrationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<NetworkDiagnostic>();
        var ids = new[] { profile.Id, profile.Instances.Id, profile.DynamicAreas.Id,
            profile.Authentication.Id, profile.Administration.Id, profile.Moderation.Id };
        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            Add("NET_WORLD_ADMIN_UUID_INVALID", "/network/worldAdministration", profile.Id,
                "World administration UUIDs must be non-empty and unique.", diagnostics);
        if (profile.SchemaVersion != 1)
            Add("NET_WORLD_ADMIN_SCHEMA_UNSUPPORTED", "/network/worldAdministration/schemaVersion", profile.Id,
                "Only world administration schema v1 is supported.", diagnostics);
        if (string.IsNullOrWhiteSpace(profile.Provenance) || profile.AuthorityScope != NetworkAuthorityScope.Server ||
            profile.ServerRevision < 0 || string.IsNullOrWhiteSpace(profile.AuditIdentity))
            Add("NET_WORLD_ADMIN_IDENTITY_INVALID", "/network/worldAdministration/identity", profile.Id,
                "Provenance, server scope, revision and audit identity are required.", diagnostics);

        if (profile.Instances.TemplateIds.Count == 0 || profile.Instances.TemplateIds.Any(string.IsNullOrWhiteSpace) ||
            profile.Instances.TemplateIds.Distinct(StringComparer.Ordinal).Count() != profile.Instances.TemplateIds.Count ||
            profile.Instances.MaximumInstances is < 1 or > 1_024 ||
            profile.Instances.MaximumPlayersPerInstance is < 1 or > 256 ||
            !profile.Instances.PartyAllocation || !profile.Instances.PersistenceEnabled ||
            !profile.Instances.RuntimeSceneInstantiation || profile.Instances.GarbageCollectionMinutes is < 1 or > 10_080)
            Add("NET_AREA_INSTANCE_POLICY_INVALID", "/network/worldAdministration/instances", profile.Instances.Id,
                "Templates, lifecycle, party allocation, persistence, capacity, scene creation and garbage collection are required.", diagnostics);

        if (!profile.DynamicAreas.TemplateInstantiation || !profile.DynamicAreas.CloneExistingArea ||
            profile.DynamicAreas.AllowedOverrideKeys.Count == 0 ||
            profile.DynamicAreas.AllowedOverrideKeys.Any(string.IsNullOrWhiteSpace) ||
            profile.DynamicAreas.AllowedOverrideKeys.Distinct(StringComparer.Ordinal).Count() != profile.DynamicAreas.AllowedOverrideKeys.Count ||
            profile.DynamicAreas.MaximumEntities is < 1 or > 100_000 ||
            profile.DynamicAreas.MaximumResourceMegabytes is < 1 or > 65_536 ||
            !profile.DynamicAreas.ServerAuthorityRequired)
            Add("NET_DYNAMIC_AREA_POLICY_INVALID", "/network/worldAdministration/dynamicAreas", profile.DynamicAreas.Id,
                "Template/clone creation, unique overrides and bounded server resources are required.", diagnostics);

        var providers = profile.Authentication.Providers;
        if (providers.Count == 0 || providers.Distinct().Count() != providers.Count ||
            !providers.Contains(AuthenticationProviderKind.Local) ||
            !providers.Contains(AuthenticationProviderKind.OpenIdConnect) ||
            !providers.Contains(AuthenticationProviderKind.Platform) ||
            !profile.Authentication.StableAccountId || !profile.Authentication.PrivacyByDefault ||
            !profile.Authentication.SecretsHashed || !profile.Authentication.NonEnumeratingFailures ||
            profile.Authentication.SessionLifetimeMinutes is < 5 or > 43_200)
            Add("NET_AUTHENTICATION_POLICY_INVALID", "/network/worldAdministration/authentication",
                profile.Authentication.Id, "Pluggable private authentication with stable IDs and safe secrets is required.", diagnostics);

        if (!profile.Administration.ApiEnabled || !profile.Administration.CliEnabled ||
            !profile.Administration.AuthenticationRequired || profile.Administration.RequestsPerMinute is < 1 or > 10_000 ||
            !profile.Administration.AuditEnabled || !profile.Administration.HealthCommands ||
            !profile.Administration.PlayerCommands || !profile.Administration.ConfigurationCommands ||
            profile.Administration.SafeCommandIds.Count == 0 ||
            profile.Administration.SafeCommandIds.Any(string.IsNullOrWhiteSpace) ||
            profile.Administration.SafeCommandIds.Distinct(StringComparer.Ordinal).Count() != profile.Administration.SafeCommandIds.Count)
            Add("NET_REMOTE_ADMIN_POLICY_INVALID", "/network/worldAdministration/administration",
                profile.Administration.Id, "Authenticated rate-limited API/CLI, audit and safe commands are required.", diagnostics);

        if (!profile.Moderation.AccountAdapter || !profile.Moderation.IpAdapter || !profile.Moderation.DeviceAdapter ||
            !profile.Moderation.Mute || !profile.Moderation.Timeout || !profile.Moderation.Kick ||
            !profile.Moderation.Ban || !profile.Moderation.Reports || !profile.Moderation.Evidence ||
            !profile.Moderation.AppealNotes || profile.Moderation.PrivacyRetentionDays is < 1 or > 3_650)
            Add("NET_MODERATION_POLICY_INVALID", "/network/worldAdministration/moderation", profile.Moderation.Id,
                "Moderation adapters, actions, reports, evidence, appeals and bounded retention are required.", diagnostics);
        return diagnostics;
    }

    private static void Add(string code, string path, Guid id, string message,
        ICollection<NetworkDiagnostic> diagnostics) => diagnostics.Add(new(code, path, message, id));
}

public sealed record NetworkWorldAdministrationCommand(Guid CommandId, int SchemaVersion, string Provenance,
    long ExpectedRevision, string ActorId, NetworkRole Role, bool Apply,
    NetworkWorldAdministrationProfile Candidate);

public sealed record NetworkWorldAdministrationResult(bool Succeeded, bool Applied,
    NetworkWorldAdministrationProfile Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics,
    string SemanticDiff, string AuditEntry);

public sealed class NetworkWorldAdministrationHistory
{
    private readonly Stack<NetworkWorldAdministrationProfile> _undo = [];
    private readonly Stack<NetworkWorldAdministrationProfile> _redo = [];

    public NetworkWorldAdministrationHistory(NetworkWorldAdministrationProfile initial)
    {
        if (NetworkWorldAdministrationValidator.Validate(initial).Count != 0)
            throw new ArgumentException("NET_WORLD_ADMIN_INITIAL_INVALID", nameof(initial));
        Current = initial;
    }

    public NetworkWorldAdministrationProfile Current { get; private set; }

    public NetworkWorldAdministrationResult Execute(NetworkWorldAdministrationCommand command)
    {
        var diagnostics = NetworkWorldAdministrationValidator.Validate(command.Candidate).ToList();
        if (command.CommandId == Guid.Empty || command.SchemaVersion != 1 || string.IsNullOrWhiteSpace(command.Provenance))
            diagnostics.Add(new("NET_WORLD_ADMIN_COMMAND_INVALID", "/network/worldAdministration/command",
                "Command UUID, schema and provenance are required.", command.CommandId));
        if (string.IsNullOrWhiteSpace(command.ActorId) || command.Role != NetworkRole.Administrator)
            diagnostics.Add(new("NET_WORLD_ADMIN_PERMISSION_DENIED", "/network/worldAdministration/audit/actor",
                "Administrator identity is required.", command.Candidate.Id));
        if (command.ExpectedRevision != Current.ServerRevision ||
            command.Candidate.ServerRevision != Current.ServerRevision + 1)
            diagnostics.Add(new("NET_WORLD_ADMIN_REVISION_STALE", "/network/worldAdministration/serverRevision",
                "Candidate must advance the current server revision exactly once.", command.Candidate.Id));
        var diff = Diff(Current, command.Candidate);
        if (diagnostics.Count != 0) return Result(false, false, Current, diagnostics, diff, "rejected", command.ActorId);
        if (!command.Apply) return Result(true, false, Current, [], diff, "previewed", command.ActorId);
        _undo.Push(Current);
        Current = command.Candidate;
        _redo.Clear();
        return Result(true, true, Current, [], diff, "applied", command.ActorId);
    }

    public NetworkWorldAdministrationResult Undo(string actorId, NetworkRole role) => Move(_undo, _redo, actorId, role, "undo");
    public NetworkWorldAdministrationResult Redo(string actorId, NetworkRole role) => Move(_redo, _undo, actorId, role, "redo");

    private NetworkWorldAdministrationResult Move(Stack<NetworkWorldAdministrationProfile> source,
        Stack<NetworkWorldAdministrationProfile> destination, string actorId, NetworkRole role, string action)
    {
        if (source.Count == 0 || role != NetworkRole.Administrator)
            return Result(false, false, Current,
                [new("NET_WORLD_ADMIN_HISTORY_DENIED", "/network/worldAdministration/history",
                    "History action is unavailable or forbidden.", Current.Id)], "no changes", "rejected", actorId);
        var previous = Current;
        destination.Push(previous);
        Current = source.Pop();
        return Result(true, true, Current, [], Diff(previous, Current), $"compensated:{action}", actorId);
    }

    private NetworkWorldAdministrationResult Result(bool succeeded, bool applied,
        NetworkWorldAdministrationProfile profile, IReadOnlyList<NetworkDiagnostic> diagnostics, string diff,
        string action, string actorId) => new(succeeded, applied, profile, diagnostics, diff,
        $"{action}:network.world-administration:actor={actorId}:revision={profile.ServerRevision}");

    private static string Diff(NetworkWorldAdministrationProfile before, NetworkWorldAdministrationProfile after)
    {
        var changes = new List<string>();
        if (before.Instances != after.Instances) changes.Add("instances");
        if (before.DynamicAreas != after.DynamicAreas) changes.Add("dynamic-areas");
        if (before.Authentication != after.Authentication) changes.Add("authentication");
        if (before.Administration != after.Administration) changes.Add("administration");
        if (before.Moderation != after.Moderation) changes.Add("moderation");
        return changes.Count == 0 ? "revision-only" : string.Join(',', changes);
    }
}

public sealed class NetworkWorldAdministrationStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string Serialize(NetworkWorldAdministrationProfile profile)
    {
        var diagnostics = NetworkWorldAdministrationValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return JsonSerializer.Serialize(profile, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public NetworkWorldAdministrationProfile Deserialize(string json)
    {
        var profile = JsonSerializer.Deserialize<NetworkWorldAdministrationProfile>(json, Options)
            ?? throw new InvalidDataException("NET_WORLD_ADMIN_EMPTY");
        var diagnostics = NetworkWorldAdministrationValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return profile;
    }
}

public sealed record NetworkWorldAdministrationResolution(bool Available,
    NetworkWorldAdministrationProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);

public static class NetworkWorldAdministrationRuntimeAdapter
{
    public static NetworkWorldAdministrationResolution Resolve(NetworkWorldAdministrationProfile? profile,
        bool required)
    {
        if (profile is null)
            return required
                ? new(false, null, [new("NET_WORLD_ADMIN_REQUIRED_MISSING", "/network/worldAdministration",
                    "Required world administration profile is missing.")])
                : new(false, null, []);
        var diagnostics = NetworkWorldAdministrationValidator.Validate(profile);
        return new(diagnostics.Count == 0, diagnostics.Count == 0 ? profile : null, diagnostics);
    }
}

public readonly record struct NetworkWorldAdministrationHandle(Guid ProfileId, int Generation);
public sealed record NetworkWorldAdministrationSnapshot(Guid Id, long Revision, int MaximumInstances,
    int ProviderCount, int SafeCommandCount, int RetentionDays);

public sealed class NetworkWorldAdministrationScriptService(NetworkWorldAdministrationProfile profile)
{
    public NetworkWorldAdministrationHandle Handle { get; } = new(profile.Id, 1);

    public NetworkWorldAdministrationSnapshot Read(NetworkWorldAdministrationHandle handle)
    {
        if (handle.ProfileId != profile.Id || handle.Generation != 1)
            throw new InvalidOperationException("NET_WORLD_ADMIN_HANDLE_INVALID");
        return new(profile.Id, profile.ServerRevision, profile.Instances.MaximumInstances,
            profile.Authentication.Providers.Count, profile.Administration.SafeCommandIds.Count,
            profile.Moderation.PrivacyRetentionDays);
    }
}
