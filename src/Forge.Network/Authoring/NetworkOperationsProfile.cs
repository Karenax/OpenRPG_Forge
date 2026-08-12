using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.RuntimeExtensions;
using Forge.Network.Chat;
using Forge.Network.Content;
using Forge.Network.Events;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.Network.Authoring;

public sealed record ContentDeliveryPolicy(Guid Id, int SchemaVersion, bool Resumable,
    bool DeduplicateByHash, bool VerifyHashes, bool DeltaEnabled, int ChunkBytes,
    string CacheDirectory, long MaximumCacheBytes, IReadOnlyList<string> AllowedLicenses,
    bool RequireTrustedSigner, IReadOnlyList<string> TrustedSignerFingerprints);

public sealed record VersionLockPolicy(Guid Id, int SchemaVersion, string ModuleId, string ModuleVersion,
    string ProtocolVersion, string ContentFingerprint, bool StrictBeforeJoin);

public sealed record RoleGrant(Guid Id, int SchemaVersion, NetworkRole Role,
    IReadOnlyList<NetworkCapability> Capabilities, IReadOnlyList<string> CustomCapabilities,
    bool ZoneScoped);

public sealed record RolePermissionPolicy(Guid Id, int SchemaVersion, IReadOnlyList<RoleGrant> Grants);

public sealed record ChatOperationsPolicy(Guid Id, int SchemaVersion,
    IReadOnlyList<ChatChannelKind> Channels, float LocalRadius, int MaximumCharacters,
    IReadOnlyList<string> AllowedLanguages, IReadOnlyDictionary<string, string> LanguageFilter,
    bool StructuredLogging, bool AccessibilityText, IReadOnlyList<string> BridgeAdapters);

public sealed record ServerEventSubscriptionPolicy(Guid Id, int SchemaVersion, string EventKind,
    string SubscriberId, int Priority);

public sealed record ServerEventOperationsPolicy(Guid Id, int SchemaVersion, int MaximumPendingEvents,
    bool TraceEnabled, bool ReplayProtection, IReadOnlyList<ServerEventSubscriptionPolicy> Subscriptions);

public sealed record NetworkOperationsProfile(Guid Id, int SchemaVersion, string Provenance,
    NetworkAuthorityScope AuthorityScope, long ServerRevision, string AuditIdentity,
    bool RequiredAtRuntime, ContentDeliveryPolicy Content, VersionLockPolicy VersionLock,
    RolePermissionPolicy Roles, ChatOperationsPolicy Chat, ServerEventOperationsPolicy Events)
{
    public static NetworkOperationsProfile CreateDefault(string provenance = "openrpg:r103")
    {
        static Guid Id(int suffix) => Guid.Parse($"a1030000-0000-4000-8000-{suffix:D12}");
        var grants = new[]
        {
            new RoleGrant(Id(10), 1, NetworkRole.Player,
                [NetworkCapability.Play, NetworkCapability.Chat, NetworkCapability.DirectMessage], [], false),
            new RoleGrant(Id(11), 1, NetworkRole.Moderator,
                [NetworkCapability.Play, NetworkCapability.Chat, NetworkCapability.DirectMessage,
                    NetworkCapability.BroadcastChat, NetworkCapability.ModerateChat,
                    NetworkCapability.ManagePlayers, NetworkCapability.ViewOperations,
                    NetworkCapability.Observe], ["chat.review"], true),
            new RoleGrant(Id(12), 1, NetworkRole.Builder,
                [NetworkCapability.Play, NetworkCapability.Chat, NetworkCapability.DirectMessage,
                    NetworkCapability.BuildContent, NetworkCapability.ManageWorld,
                    NetworkCapability.ManageEvents, NetworkCapability.ViewOperations,
                    NetworkCapability.Observe], ["content.publish"], true),
            new RoleGrant(Id(13), 1, NetworkRole.GameMaster,
                Enum.GetValues<NetworkCapability>().Where(item => item != NetworkCapability.BuildContent).ToArray(),
                ["encounter.direct"], true),
            new RoleGrant(Id(14), 1, NetworkRole.Administrator,
                Enum.GetValues<NetworkCapability>(), ["*"], false),
            new RoleGrant(Id(15), 1, NetworkRole.Spectator,
                [NetworkCapability.Chat, NetworkCapability.DirectMessage, NetworkCapability.ViewOperations,
                    NetworkCapability.Observe], [], false)
        };
        return new(Id(1), 1, provenance, NetworkAuthorityScope.Server, 0, "network.operations.author", false,
            new(Id(2), 1, true, true, true, true, 64 * 1024, "network/content-cache",
                4L * 1024 * 1024 * 1024, ["CC0-1.0", "MIT"], true, [new string('a', 64)]),
            new(Id(3), 1, "crypt-of-ashes", "1.0.0", "1", new string('b', 64), true),
            new(Id(4), 1, grants),
            new(Id(5), 1, Enum.GetValues<ChatChannelKind>(), 30, 500, ["common", "elvish"],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["spoiler"] = "•••••••" },
                true, true, ["audit-log"]),
            new(Id(6), 1, 256, true, true,
                [new(Id(20), 1, "quest.updated", "gameplay.quest", 100),
                    new(Id(21), 1, "quest.updated", "audit.trace", 10)]));
    }
}

public static class NetworkOperationsValidator
{
    private static readonly NetworkRole[] RequiredRoles =
    [
        NetworkRole.Player, NetworkRole.Moderator, NetworkRole.Builder, NetworkRole.GameMaster,
        NetworkRole.Administrator, NetworkRole.Spectator
    ];

    private static readonly ChatChannelKind[] RequiredChannels =
    [
        ChatChannelKind.Local, ChatChannelKind.Party, ChatChannelKind.Tell,
        ChatChannelKind.Shout, ChatChannelKind.DungeonMaster
    ];

    public static IReadOnlyList<NetworkDiagnostic> Validate(NetworkOperationsProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<NetworkDiagnostic>();
        var ids = new[] { profile.Id, profile.Content.Id, profile.VersionLock.Id, profile.Roles.Id,
                profile.Chat.Id, profile.Events.Id }
            .Concat(profile.Roles.Grants.Select(item => item.Id))
            .Concat(profile.Events.Subscriptions.Select(item => item.Id)).ToArray();
        if (ids.Any(item => item == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            Add("NET_OPERATIONS_UUID_UNIQUE_REQUIRED", "/network/operations", "All operation UUIDs must be non-empty and unique.", profile.Id, diagnostics);
        if (new[] { profile.SchemaVersion, profile.Content.SchemaVersion, profile.VersionLock.SchemaVersion,
                profile.Roles.SchemaVersion, profile.Chat.SchemaVersion, profile.Events.SchemaVersion }
            .Concat(profile.Roles.Grants.Select(item => item.SchemaVersion))
            .Concat(profile.Events.Subscriptions.Select(item => item.SchemaVersion)).Any(item => item != 1))
            Add("NET_OPERATIONS_SCHEMA_UNSUPPORTED", "/network/operations/schemaVersion", "Only network operations schema v1 is supported.", profile.Id, diagnostics);
        if (string.IsNullOrWhiteSpace(profile.Provenance))
            Add("NET_OPERATIONS_PROVENANCE_REQUIRED", "/network/operations/provenance", "Modification provenance is required.", profile.Id, diagnostics);
        if (profile.AuthorityScope != NetworkAuthorityScope.Server)
            Add("NET_OPERATIONS_SCOPE_INVALID", "/network/operations/authorityScope", "Server authority scope is required.", profile.Id, diagnostics);
        if (profile.ServerRevision < 0)
            Add("NET_OPERATIONS_REVISION_INVALID", "/network/operations/serverRevision", "Server revision cannot be negative.", profile.Id, diagnostics);
        if (string.IsNullOrWhiteSpace(profile.AuditIdentity))
            Add("NET_OPERATIONS_AUDIT_REQUIRED", "/network/operations/auditIdentity", "An audit identity is required.", profile.Id, diagnostics);

        var content = profile.Content;
        if (!content.Resumable || !content.DeduplicateByHash || !content.VerifyHashes ||
            content.ChunkBytes is < 16 * 1024 or > 4 * 1024 * 1024 ||
            content.MaximumCacheBytes is < 1_048_576 or > 549_755_813_888 ||
            string.IsNullOrWhiteSpace(content.CacheDirectory) || Path.IsPathRooted(content.CacheDirectory) ||
            content.CacheDirectory.Replace('\\', '/').Split('/').Any(item => item is "." or "..") ||
            content.AllowedLicenses.Count == 0 || content.AllowedLicenses.Any(string.IsNullOrWhiteSpace) ||
            content.RequireTrustedSigner && content.TrustedSignerFingerprints.Count == 0 ||
            content.TrustedSignerFingerprints.Any(item => !IsSha256(item)))
            Add("NET_CONTENT_POLICY_INVALID", "/network/operations/content", "Content sync requires bounded chunks, cache, hashes, licenses and trust.", content.Id, diagnostics);

        var version = profile.VersionLock;
        if (string.IsNullOrWhiteSpace(version.ModuleId) || string.IsNullOrWhiteSpace(version.ModuleVersion) ||
            string.IsNullOrWhiteSpace(version.ProtocolVersion) || !IsSha256(version.ContentFingerprint) ||
            !version.StrictBeforeJoin)
            Add("NET_VERSION_LOCK_INVALID", "/network/operations/versionLock", "Strict module, protocol and content lock values are required before join.", version.Id, diagnostics);

        var roles = profile.Roles.Grants;
        if (roles.Select(item => item.Role).Distinct().Count() != roles.Count ||
            RequiredRoles.Any(role => roles.All(item => item.Role != role)) ||
            roles.Any(item => item.Capabilities.Count == 0 || item.Capabilities.Distinct().Count() != item.Capabilities.Count ||
                item.CustomCapabilities.Any(string.IsNullOrWhiteSpace) ||
                item.CustomCapabilities.Distinct(StringComparer.Ordinal).Count() != item.CustomCapabilities.Count))
            Add("NET_ROLE_GRANTS_INVALID", "/network/operations/roles/grants", "All standard roles need unique capabilities and valid custom grants.", profile.Roles.Id, diagnostics);

        var chat = profile.Chat;
        if (RequiredChannels.Any(channel => !chat.Channels.Contains(channel)) ||
            chat.Channels.Distinct().Count() != chat.Channels.Count || chat.LocalRadius is <= 0 or > 10_000 ||
            chat.MaximumCharacters is < 1 or > 4_096 || chat.AllowedLanguages.Count == 0 ||
            chat.AllowedLanguages.Any(string.IsNullOrWhiteSpace) || !chat.StructuredLogging ||
            !chat.AccessibilityText || chat.BridgeAdapters.Count == 0 ||
            chat.BridgeAdapters.Any(string.IsNullOrWhiteSpace))
            Add("NET_CHAT_POLICY_INVALID", "/network/operations/chat", "Chat requires typed channels, proximity, languages, logs, accessibility and bridges.", chat.Id, diagnostics);

        var events = profile.Events;
        if (events.MaximumPendingEvents is < 1 or > 65_536 || !events.TraceEnabled ||
            !events.ReplayProtection || events.Subscriptions.Count == 0 ||
            events.Subscriptions.Any(item => string.IsNullOrWhiteSpace(item.EventKind) ||
                string.IsNullOrWhiteSpace(item.SubscriberId) || item.Priority is < -1_000 or > 1_000) ||
            events.Subscriptions.Select(item => (item.EventKind, item.SubscriberId)).Distinct().Count() !=
            events.Subscriptions.Count)
            Add("NET_EVENT_POLICY_INVALID", "/network/operations/events", "Event bus requires bounded backpressure, trace, replay protection and unique subscriptions.", events.Id, diagnostics);
        return diagnostics;
    }

    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
    private static void Add(string code, string path, string message, Guid id,
        ICollection<NetworkDiagnostic> diagnostics) => diagnostics.Add(new(code, path, message, id));
}

public sealed record NetworkOperationsCommand(Guid CommandId, int SchemaVersion, string Provenance,
    long ExpectedRevision, string ActorId, NetworkRole Role, bool Apply, NetworkOperationsProfile Candidate);

public sealed record NetworkOperationsResult(bool Succeeded, bool Applied, NetworkOperationsProfile Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics, string SemanticDiff, string AuditEntry);

public sealed class NetworkOperationsHistory
{
    private readonly Stack<NetworkOperationsProfile> _undo = [];
    private readonly Stack<NetworkOperationsProfile> _redo = [];

    public NetworkOperationsHistory(NetworkOperationsProfile initial)
    {
        if (NetworkOperationsValidator.Validate(initial).Count != 0)
            throw new ArgumentException("NET_OPERATIONS_INITIAL_INVALID", nameof(initial));
        Current = initial;
    }

    public NetworkOperationsProfile Current { get; private set; }

    public NetworkOperationsResult Execute(NetworkOperationsCommand command)
    {
        var diagnostics = NetworkOperationsValidator.Validate(command.Candidate).ToList();
        if (command.CommandId == Guid.Empty || command.SchemaVersion != 1 || string.IsNullOrWhiteSpace(command.Provenance))
            diagnostics.Add(new("NET_OPERATIONS_COMMAND_INVALID", "/network/operations/command", "Command UUID, schema and provenance are required.", command.CommandId));
        if (string.IsNullOrWhiteSpace(command.ActorId))
            diagnostics.Add(new("NET_OPERATIONS_ACTOR_REQUIRED", "/network/operations/audit/actorId", "An author identity is required.", command.Candidate.Id));
        if (command.Role != NetworkRole.Administrator)
            diagnostics.Add(new("NET_OPERATIONS_PERMISSION_DENIED", "/network/operations/audit/role", "Administrator permission is required.", command.Candidate.Id));
        if (command.ExpectedRevision != Current.ServerRevision)
            diagnostics.Add(new("NET_OPERATIONS_REVISION_STALE", "/network/operations/serverRevision", "The operation profile revision is stale.", command.Candidate.Id));
        if (command.Candidate.ServerRevision != Current.ServerRevision + 1)
            diagnostics.Add(new("NET_OPERATIONS_NEXT_REVISION_REQUIRED", "/network/operations/serverRevision", "Candidate revision must advance exactly once.", command.Candidate.Id));
        var diff = Diff(Current, command.Candidate);
        if (diagnostics.Count != 0)
            return new(false, false, Current, diagnostics, diff,
                $"rejected:network.operations:actor={command.ActorId}:revision={Current.ServerRevision}");
        if (!command.Apply)
            return new(true, false, Current, [], diff,
                $"previewed:network.operations:actor={command.ActorId}:revision={Current.ServerRevision}");
        _undo.Push(Current);
        Current = command.Candidate;
        _redo.Clear();
        return new(true, true, Current, [], diff,
            $"applied:network.operations:actor={command.ActorId}:revision={Current.ServerRevision}");
    }

    public NetworkOperationsResult Undo(string actorId, NetworkRole role) => Move(_undo, _redo, "undo", actorId, role);
    public NetworkOperationsResult Redo(string actorId, NetworkRole role) => Move(_redo, _undo, "redo", actorId, role);

    private NetworkOperationsResult Move(Stack<NetworkOperationsProfile> source,
        Stack<NetworkOperationsProfile> destination, string action, string actorId, NetworkRole role)
    {
        if (source.Count == 0 || role != NetworkRole.Administrator)
            return new(false, false, Current,
                [new("NET_OPERATIONS_HISTORY_DENIED", "/network/operations/history", "History action is unavailable or not permitted.", Current.Id)],
                "no changes", $"rejected:network.operations:{action}:actor={actorId}:revision={Current.ServerRevision}");
        var previous = Current;
        destination.Push(previous);
        Current = source.Pop();
        return new(true, true, Current, [], Diff(previous, Current),
            $"compensated:network.operations:{action}:actor={actorId}:revision={Current.ServerRevision}");
    }

    private static string Diff(NetworkOperationsProfile before, NetworkOperationsProfile after)
    {
        var changes = new List<string>();
        if (before.Content != after.Content) changes.Add("content-sync");
        if (before.VersionLock != after.VersionLock) changes.Add("version-lock");
        if (before.Roles != after.Roles) changes.Add("roles");
        if (before.Chat != after.Chat) changes.Add("chat");
        if (before.Events != after.Events) changes.Add("event-bus");
        return changes.Count == 0 ? "revision-only" : string.Join(',', changes);
    }
}

public sealed class NetworkOperationsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string Serialize(NetworkOperationsProfile profile)
    {
        var diagnostics = NetworkOperationsValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return JsonSerializer.Serialize(profile, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public NetworkOperationsProfile Deserialize(string json)
    {
        var profile = JsonSerializer.Deserialize<NetworkOperationsProfile>(json, Options)
            ?? throw new InvalidDataException("NET_OPERATIONS_EMPTY");
        var diagnostics = NetworkOperationsValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return profile;
    }
}

public sealed record VersionLockDecision(bool Compatible, string Code, IReadOnlyList<string> Details,
    string AuditEntry);

public static class VersionLockNegotiator
{
    public static VersionLockDecision Check(VersionLockPolicy expected, string moduleId,
        string moduleVersion, string protocolVersion, string contentFingerprint)
    {
        var details = new List<string>();
        if (!string.Equals(expected.ModuleId, moduleId, StringComparison.Ordinal) ||
            !string.Equals(expected.ModuleVersion, moduleVersion, StringComparison.Ordinal))
            details.Add($"Module attendu {expected.ModuleId}@{expected.ModuleVersion}, reçu {moduleId}@{moduleVersion}.");
        if (!string.Equals(expected.ProtocolVersion, protocolVersion, StringComparison.Ordinal))
            details.Add($"Protocole attendu {expected.ProtocolVersion}, reçu {protocolVersion}.");
        if (!string.Equals(expected.ContentFingerprint, contentFingerprint, StringComparison.OrdinalIgnoreCase))
            details.Add($"Contenu attendu {expected.ContentFingerprint}, reçu {contentFingerprint}.");
        var compatible = details.Count == 0;
        var code = compatible ? "NET_VERSION_LOCK_COMPATIBLE" : "NET_VERSION_LOCK_MISMATCH";
        return new(compatible, code, details,
            $"{(compatible ? "accepted" : "rejected")}:{code}:module={moduleId}:version={moduleVersion}:protocol={protocolVersion}");
    }
}

public readonly record struct NetworkOperationsHandle(Guid ProfileId, int Generation);
public readonly record struct ContentDeliveryPolicyRef(Guid ProfileId, Guid PolicyId, int Generation);
public readonly record struct RolePermissionPolicyRef(Guid ProfileId, Guid PolicyId, int Generation);
public readonly record struct ChatOperationsPolicyRef(Guid ProfileId, Guid PolicyId, int Generation);
public readonly record struct ServerEventOperationsPolicyRef(Guid ProfileId, Guid PolicyId, int Generation);

public sealed record NetworkOperationsScriptSnapshot(Guid Id, long Revision, string ModuleVersion,
    int RoleCount, int ChatChannelCount, int EventSubscriptionCount, string Provenance);

public sealed class NetworkOperationsScriptService(NetworkOperationsProfile profile)
{
    private const int Generation = 1;
    public NetworkOperationsHandle Handle { get; } = new(profile.Id, Generation);

    public NetworkOperationsScriptSnapshot Read(NetworkOperationsHandle handle)
    {
        Validate(handle.ProfileId, profile.Id, handle.Generation);
        return new(profile.Id, profile.ServerRevision, profile.VersionLock.ModuleVersion,
            profile.Roles.Grants.Count, profile.Chat.Channels.Count, profile.Events.Subscriptions.Count,
            profile.Provenance);
    }

    public ContentDeliveryPolicyRef Content(NetworkOperationsHandle handle) =>
        new(Read(handle).Id, profile.Content.Id, Generation);
    public RolePermissionPolicyRef Roles(NetworkOperationsHandle handle) =>
        new(Read(handle).Id, profile.Roles.Id, Generation);
    public ChatOperationsPolicyRef Chat(NetworkOperationsHandle handle) =>
        new(Read(handle).Id, profile.Chat.Id, Generation);
    public ServerEventOperationsPolicyRef Events(NetworkOperationsHandle handle) =>
        new(Read(handle).Id, profile.Events.Id, Generation);

    private static void Validate(Guid actual, Guid expected, int generation)
    {
        if (actual != expected || generation != Generation)
            throw new InvalidOperationException("NET_OPERATIONS_HANDLE_INVALID");
    }
}

public sealed record NetworkOperationsResolution(bool Available, NetworkOperationsProfile? Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics);

public sealed record NetworkOperationsRuntime(ContentSynchronizationPolicy ContentPolicy,
    AuthorizationService Authorization, ChatService Chat, ServerEventBus Events,
    NetworkOperationsScriptService Scripts);

public static class NetworkOperationsRuntimeAdapter
{
    public static NetworkOperationsResolution Resolve(NetworkOperationsProfile? profile, bool required)
    {
        if (profile is null)
            return required
                ? new(false, null, [new("NET_OPERATIONS_REQUIRED_MISSING", "/network/operations", "Required network operations profile is missing.")])
                : new(false, null, []);
        var diagnostics = NetworkOperationsValidator.Validate(profile);
        return diagnostics.Count == 0 ? new(true, profile, []) : new(false, null, diagnostics);
    }

    public static NetworkOperationsRuntime Build(NetworkOperationsProfile profile, TimeProvider? timeProvider = null,
        IReadOnlyList<IChatBridgeAdapter>? bridges = null)
    {
        var resolution = Resolve(profile, profile.RequiredAtRuntime);
        if (!resolution.Available) throw new InvalidOperationException(resolution.Diagnostics[0].Code);
        var content = new ContentSynchronizationPolicy(profile.Content.Id, 1, profile.Content.Resumable,
            profile.Content.DeduplicateByHash, profile.Content.VerifyHashes, profile.Content.RequireTrustedSigner,
            profile.Content.CacheDirectory, profile.Content.MaximumCacheBytes, profile.Content.AllowedLicenses,
            profile.Content.TrustedSignerFingerprints, profile.Provenance);
        var authorization = new AuthorizationService();
        var clock = timeProvider ?? TimeProvider.System;
        var moderation = new ModerationService(authorization, clock);
        var chatPolicy = new ChatRuntimePolicy(profile.Chat.LocalRadius, profile.Chat.MaximumCharacters,
            profile.Chat.AllowedLanguages, profile.Chat.LanguageFilter, profile.Chat.StructuredLogging,
            profile.Chat.AccessibilityText, profile.Chat.BridgeAdapters.Count != 0);
        return new(content, authorization, new(authorization, moderation, clock, chatPolicy, bridges),
            new(profile.Events.MaximumPendingEvents, profile.Events.TraceEnabled), new(profile));
    }
}
