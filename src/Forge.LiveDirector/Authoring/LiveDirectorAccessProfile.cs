using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Quality;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.LiveDirector.Authoring;

public sealed record DirectorAuthenticationPolicy(Guid Id, int SchemaVersion,
    IReadOnlyList<NetworkRole> AllowedRoles, bool RequireSecondFactor, string SecondFactorAdapter,
    int SessionLifetimeMinutes, bool RevokeSupported, bool AuditEveryDecision);

public sealed record DirectorInspectionPolicy(Guid Id, int SchemaVersion,
    IReadOnlyList<string> VisibleSections, bool ReadOnlyByDefault, bool PermissionedEdits,
    int HistoryEntryLimit, bool ShowOwnership);

public sealed record DirectorPlayerRosterPolicy(Guid Id, int SchemaVersion,
    IReadOnlyList<string> VisibleColumns, int RefreshSeconds, string LatencyAdapter,
    bool ShowVoiceStatus, bool ShowModerationFlags);

public sealed record DirectorAuditPolicy(Guid Id, int SchemaVersion, bool ImmutableHashChain,
    int RetentionDays, int MaximumEntries, string ExternalSinkAdapter, bool RequireBeforeAfter,
    bool RequireReason, bool RequireCorrelationId);

public sealed record DirectorChatPolicy(Guid Id, int SchemaVersion, IReadOnlyList<string> Channels,
    int MaximumMessageLength, bool ModerationEnabled, bool ImmutableLogs,
    string VoiceAdapter, bool ImpersonationReasonRequired);

public sealed record LiveDirectorAccessProfile(Guid Id, int SchemaVersion, string Provenance,
    NetworkAuthorityScope AuthorityScope, long ServerRevision, string AuditIdentity,
    bool RequiredAtRuntime, DirectorAuthenticationPolicy Authentication,
    DirectorInspectionPolicy Inspection, DirectorPlayerRosterPolicy Players,
    DirectorAuditPolicy Audit, DirectorChatPolicy Chat)
{
    public static LiveDirectorAccessProfile CreateDefault(string provenance = "openrpg:r105")
    {
        static Guid Id(int suffix) => Guid.Parse($"a1050000-0000-4000-8000-{suffix:D12}");
        return new(Id(1), 1, provenance, NetworkAuthorityScope.Server, 0,
            "live-director.author", false,
            new(Id(2), 1, [NetworkRole.Administrator], true, "totp:local", 30, true, true),
            new(Id(3), 1, ["components", "stats", "variables", "scripts", "ownership", "history"],
                true, false, 100, true),
            new(Id(4), 1, ["identity", "character", "zone", "latency", "party", "permissions", "voice", "moderation"],
                5, "network:authoritative-latency", true, true),
            new(Id(5), 1, true, 90, 100_000, "optional:none", true, true, true),
            new(Id(6), 1, ["local", "party", "tell", "server", "gm", "faction", "impersonation"],
                500, true, true, "optional:none", true));
    }
}

public static class LiveDirectorAccessValidator
{
    private static readonly string[] RequiredInspectionSections =
        ["components", "stats", "variables", "scripts", "ownership", "history"];
    private static readonly string[] RequiredPlayerColumns =
        ["identity", "character", "zone", "latency", "party", "permissions", "voice", "moderation"];
    private static readonly string[] RequiredChatChannels =
        ["local", "party", "tell", "server", "gm", "faction", "impersonation"];

    public static IReadOnlyList<NetworkDiagnostic> Validate(LiveDirectorAccessProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<NetworkDiagnostic>();
        var ids = new[] { profile.Id, profile.Authentication.Id, profile.Inspection.Id,
            profile.Players.Id, profile.Audit.Id, profile.Chat.Id };
        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            Add("LIVE_ACCESS_UUID_UNIQUE_REQUIRED", "/liveDirector/access", "All access UUIDs must be non-empty and unique.", profile.Id, diagnostics);
        if (new[] { profile.SchemaVersion, profile.Authentication.SchemaVersion, profile.Inspection.SchemaVersion,
                profile.Players.SchemaVersion, profile.Audit.SchemaVersion, profile.Chat.SchemaVersion }.Any(version => version != 1))
            Add("LIVE_ACCESS_SCHEMA_UNSUPPORTED", "/liveDirector/access/schemaVersion", "Only Live Director access schema v1 is supported.", profile.Id, diagnostics);
        if (string.IsNullOrWhiteSpace(profile.Provenance))
            Add("LIVE_ACCESS_PROVENANCE_REQUIRED", "/liveDirector/access/provenance", "Modification provenance is required.", profile.Id, diagnostics);
        if (profile.AuthorityScope != NetworkAuthorityScope.Server)
            Add("LIVE_ACCESS_SCOPE_INVALID", "/liveDirector/access/authorityScope", "Server authority is required.", profile.Id, diagnostics);
        if (profile.ServerRevision < 0)
            Add("LIVE_ACCESS_REVISION_INVALID", "/liveDirector/access/serverRevision", "Server revision cannot be negative.", profile.Id, diagnostics);
        if (string.IsNullOrWhiteSpace(profile.AuditIdentity))
            Add("LIVE_ACCESS_AUDIT_IDENTITY_REQUIRED", "/liveDirector/access/auditIdentity", "An audit identity is required.", profile.Id, diagnostics);

        var authentication = profile.Authentication;
        if (authentication.AllowedRoles.Count != 1 || authentication.AllowedRoles[0] != NetworkRole.Administrator ||
            !authentication.RequireSecondFactor || string.IsNullOrWhiteSpace(authentication.SecondFactorAdapter) ||
            authentication.SessionLifetimeMinutes is < 1 or > 1440 || !authentication.RevokeSupported || !authentication.AuditEveryDecision)
            Add("LIVE_ACCESS_AUTH_POLICY_INVALID", "/liveDirector/access/authentication", "Authentication requires a GM-only role, MFA adapter, bounded expiry, revocation and audit.", authentication.Id, diagnostics);

        var inspection = profile.Inspection;
        if (!ContainsAll(inspection.VisibleSections, RequiredInspectionSections) || !inspection.ReadOnlyByDefault ||
            inspection.PermissionedEdits || inspection.HistoryEntryLimit is < 1 or > 10_000 || !inspection.ShowOwnership)
            Add("LIVE_ACCESS_INSPECTION_POLICY_INVALID", "/liveDirector/access/inspection", "Inspection requires read-only components, stats, variables, scripts, ownership and bounded history.", inspection.Id, diagnostics);

        var players = profile.Players;
        if (!ContainsAll(players.VisibleColumns, RequiredPlayerColumns) || players.RefreshSeconds is < 1 or > 300 ||
            string.IsNullOrWhiteSpace(players.LatencyAdapter) || !players.ShowVoiceStatus || !players.ShowModerationFlags)
            Add("LIVE_ACCESS_PLAYER_POLICY_INVALID", "/liveDirector/access/players", "The player roster requires all operational columns and a bounded authoritative refresh adapter.", players.Id, diagnostics);

        var audit = profile.Audit;
        if (!audit.ImmutableHashChain || audit.RetentionDays is < 1 or > 3650 || audit.MaximumEntries is < 1 or > 10_000_000 ||
            string.IsNullOrWhiteSpace(audit.ExternalSinkAdapter) || !audit.RequireBeforeAfter || !audit.RequireReason || !audit.RequireCorrelationId)
            Add("LIVE_ACCESS_AUDIT_POLICY_INVALID", "/liveDirector/access/audit", "Audit requires an immutable bounded hash chain with before/after, reason and correlation.", audit.Id, diagnostics);

        var chat = profile.Chat;
        if (!ContainsAll(chat.Channels, RequiredChatChannels) || chat.MaximumMessageLength is < 1 or > 500 ||
            !chat.ModerationEnabled || !chat.ImmutableLogs || string.IsNullOrWhiteSpace(chat.VoiceAdapter) || !chat.ImpersonationReasonRequired)
            Add("LIVE_ACCESS_CHAT_POLICY_INVALID", "/liveDirector/access/chat", "GM chat requires all channels, moderation, immutable logs and reasoned impersonation.", chat.Id, diagnostics);
        return diagnostics;
    }

    private static bool ContainsAll(IReadOnlyList<string> actual, IReadOnlyList<string> required) =>
        actual.Count == actual.Distinct(StringComparer.OrdinalIgnoreCase).Count() &&
        required.All(item => actual.Contains(item, StringComparer.OrdinalIgnoreCase));

    private static void Add(string code, string path, string message, Guid id,
        ICollection<NetworkDiagnostic> diagnostics) => diagnostics.Add(new(code, path, message, id));
}

public sealed record LiveDirectorAccessCommand(Guid CommandId, int SchemaVersion, string Provenance,
    long ExpectedRevision, string ActorId, NetworkRole Role, NetworkAuthorityScope Scope,
    bool Apply, LiveDirectorAccessProfile Candidate);

public sealed record LiveDirectorAccessResult(bool Succeeded, bool Applied, LiveDirectorAccessProfile Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics, string SemanticDiff, string AuditEntry);

public sealed class LiveDirectorAccessHistory
{
    private readonly Stack<LiveDirectorAccessProfile> _undo = [];
    private readonly Stack<LiveDirectorAccessProfile> _redo = [];

    public LiveDirectorAccessHistory(LiveDirectorAccessProfile initial)
    {
        if (LiveDirectorAccessValidator.Validate(initial).Count != 0)
            throw new ArgumentException("LIVE_ACCESS_INITIAL_INVALID", nameof(initial));
        Current = initial;
    }

    public LiveDirectorAccessProfile Current { get; private set; }

    public LiveDirectorAccessResult Execute(LiveDirectorAccessCommand command)
    {
        var diagnostics = LiveDirectorAccessValidator.Validate(command.Candidate).ToList();
        if (command.CommandId == Guid.Empty || command.SchemaVersion != 1 || string.IsNullOrWhiteSpace(command.Provenance))
            diagnostics.Add(new("LIVE_ACCESS_COMMAND_INVALID", "/liveDirector/access/command", "Command UUID, schema and provenance are required.", command.CommandId));
        if (string.IsNullOrWhiteSpace(command.ActorId))
            diagnostics.Add(new("LIVE_ACCESS_ACTOR_REQUIRED", "/liveDirector/access/audit/actorId", "An author identity is required.", command.Candidate.Id));
        if (command.Role != NetworkRole.Administrator)
            diagnostics.Add(new("LIVE_ACCESS_PERMISSION_DENIED", "/liveDirector/access/audit/role", "Administrator permission is required.", command.Candidate.Id));
        if (command.Scope != NetworkAuthorityScope.Server)
            diagnostics.Add(new("LIVE_ACCESS_SCOPE_INVALID", "/liveDirector/access/authorityScope", "Server scope is required.", command.Candidate.Id));
        if (command.ExpectedRevision != Current.ServerRevision)
            diagnostics.Add(new("LIVE_ACCESS_REVISION_STALE", "/liveDirector/access/serverRevision", "The profile revision is stale.", command.Candidate.Id));
        if (command.Candidate.ServerRevision != Current.ServerRevision + 1)
            diagnostics.Add(new("LIVE_ACCESS_NEXT_REVISION_REQUIRED", "/liveDirector/access/serverRevision", "Candidate revision must advance exactly once.", command.Candidate.Id));
        var diff = Diff(Current, command.Candidate);
        if (diagnostics.Count != 0)
            return new(false, false, Current, diagnostics, diff, $"rejected:live.access:actor={command.ActorId}:revision={Current.ServerRevision}");
        if (!command.Apply)
            return new(true, false, Current, [], diff, $"previewed:live.access:actor={command.ActorId}:revision={Current.ServerRevision}");
        _undo.Push(Current);
        Current = command.Candidate;
        _redo.Clear();
        return new(true, true, Current, [], diff, $"applied:live.access:actor={command.ActorId}:revision={Current.ServerRevision}");
    }

    public LiveDirectorAccessResult Undo(string actorId, NetworkRole role) => Move(_undo, _redo, "undo", actorId, role);
    public LiveDirectorAccessResult Redo(string actorId, NetworkRole role) => Move(_redo, _undo, "redo", actorId, role);

    private LiveDirectorAccessResult Move(Stack<LiveDirectorAccessProfile> source,
        Stack<LiveDirectorAccessProfile> destination, string action, string actorId, NetworkRole role)
    {
        if (source.Count == 0 || role != NetworkRole.Administrator)
            return new(false, false, Current,
                [new("LIVE_ACCESS_HISTORY_DENIED", "/liveDirector/access/history", "History action is unavailable or not permitted.", Current.Id)],
                "no changes", $"rejected:live.access:{action}:actor={actorId}:revision={Current.ServerRevision}");
        var previous = Current;
        destination.Push(previous);
        Current = source.Pop();
        return new(true, true, Current, [], Diff(previous, Current),
            $"compensated:live.access:{action}:actor={actorId}:revision={Current.ServerRevision}");
    }

    private static string Diff(LiveDirectorAccessProfile before, LiveDirectorAccessProfile after)
    {
        var changes = new List<string>();
        if (before.Authentication != after.Authentication) changes.Add("authentication");
        if (before.Inspection != after.Inspection) changes.Add("inspection");
        if (before.Players != after.Players) changes.Add("players");
        if (before.Audit != after.Audit) changes.Add("audit");
        if (before.Chat != after.Chat) changes.Add("chat");
        return changes.Count == 0 ? "revision-only" : string.Join(',', changes);
    }
}

public sealed class LiveDirectorAccessStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string Serialize(LiveDirectorAccessProfile profile)
    {
        var diagnostics = LiveDirectorAccessValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return JsonSerializer.Serialize(profile, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public LiveDirectorAccessProfile Deserialize(string json)
    {
        var profile = JsonSerializer.Deserialize<LiveDirectorAccessProfile>(json, Options)
            ?? throw new InvalidDataException("LIVE_ACCESS_EMPTY");
        var diagnostics = LiveDirectorAccessValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return profile;
    }
}

public readonly record struct LiveDirectorAccessHandle(Guid ProfileId, int Generation);
public readonly record struct DirectorAccessPolicyRef(Guid ProfileId, Guid PolicyId, int Generation);
public sealed record LiveDirectorAccessScriptSnapshot(Guid Id, long Revision, int SessionMinutes,
    int InspectionSections, int PlayerColumns, int AuditRetentionDays, int ChatChannels, string Provenance);

public sealed class LiveDirectorAccessScriptService(LiveDirectorAccessProfile profile)
{
    private const int Generation = 1;
    public LiveDirectorAccessHandle Handle { get; } = new(profile.Id, Generation);

    public LiveDirectorAccessScriptSnapshot Read(LiveDirectorAccessHandle handle)
    {
        Validate(handle);
        return new(profile.Id, profile.ServerRevision, profile.Authentication.SessionLifetimeMinutes,
            profile.Inspection.VisibleSections.Count, profile.Players.VisibleColumns.Count,
            profile.Audit.RetentionDays, profile.Chat.Channels.Count, profile.Provenance);
    }

    public DirectorAccessPolicyRef Authentication(LiveDirectorAccessHandle handle) => Ref(handle, profile.Authentication.Id);
    public DirectorAccessPolicyRef Inspection(LiveDirectorAccessHandle handle) => Ref(handle, profile.Inspection.Id);
    public DirectorAccessPolicyRef Players(LiveDirectorAccessHandle handle) => Ref(handle, profile.Players.Id);
    public DirectorAccessPolicyRef Audit(LiveDirectorAccessHandle handle) => Ref(handle, profile.Audit.Id);
    public DirectorAccessPolicyRef Chat(LiveDirectorAccessHandle handle) => Ref(handle, profile.Chat.Id);

    private DirectorAccessPolicyRef Ref(LiveDirectorAccessHandle handle, Guid policyId)
    {
        Validate(handle);
        return new(profile.Id, policyId, Generation);
    }

    private void Validate(LiveDirectorAccessHandle handle)
    {
        if (handle.ProfileId != profile.Id || handle.Generation != Generation)
            throw new InvalidOperationException("LIVE_ACCESS_HANDLE_INVALID");
    }
}

public sealed record LiveDirectorAccessResolution(bool Available, LiveDirectorAccessProfile? Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics);
public sealed record LiveDirectorAccessRuntime(LiveDirectorAccessProfile Profile,
    LiveDirectorAccessScriptService Scripts);

public static class LiveDirectorAccessRuntimeAdapter
{
    public static LiveDirectorAccessResolution Resolve(LiveDirectorAccessProfile? profile, bool required)
    {
        if (profile is null)
            return required
                ? new(false, null, [new("LIVE_ACCESS_REQUIRED_MISSING", "/liveDirector/access", "Required Live Director access profile is missing.")])
                : new(false, null, []);
        var diagnostics = LiveDirectorAccessValidator.Validate(profile);
        return diagnostics.Count == 0 ? new(true, profile, []) : new(false, null, diagnostics);
    }

    public static LiveDirectorAccessRuntime Build(LiveDirectorAccessProfile profile)
    {
        var resolution = Resolve(profile, profile.RequiredAtRuntime);
        if (!resolution.Available) throw new InvalidOperationException(resolution.Diagnostics[0].Code);
        return new(profile, new(profile));
    }
}
