using System.Text.Json;
using Forge.LiveDirector.Authentication;
using Forge.LiveDirector.Catalog;
using Forge.LiveDirector.Client;
using Forge.LiveDirector.World;
using Forge.Network.Authoring;
using Forge.Network.Identity;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.LiveDirector.Authoring;

public sealed record DirectorClientPolicy(Guid Id, bool SeparateClient, bool EngineIndependent,
    bool RoleAuthentication, bool SecondFactor, int SessionMinutes, bool Revocation, bool Audit);
public sealed record DirectorCreatorPolicy(Guid Id, bool ServerAllowlist, bool PackageFilter, bool TextFilter,
    bool TypeFilter, bool CostFilter, bool ZoneFilter, bool PermissionFilter, bool Thumbnails);
public sealed record DirectorSpawnPolicy(Guid Id, int MaximumInstances, IReadOnlyList<string> PlacementModes,
    bool FormationCoordinates, bool Preview, bool Atomic, bool StableIds, bool SafeUndo);
public sealed record DirectorChooserPolicy(Guid Id, bool HierarchicalZones, bool InstanceNavigation,
    IReadOnlyList<string> Filters, bool Diagnostics, bool PrivacyFiltering);
public sealed record DirectorFocusPolicy(Guid Id, bool SpectatorCamera, bool CrossZone,
    bool PrivacyOverrideRequiresPermission, bool ReadOnly, bool Audit);
public sealed record LiveDirectorWorkspaceProfile(Guid Id, int SchemaVersion, string Provenance,
    NetworkAuthorityScope AuthorityScope, long ServerRevision, string AuditIdentity, bool RequiredAtRuntime,
    DirectorClientPolicy Client, DirectorCreatorPolicy Creator, DirectorSpawnPolicy Spawn,
    DirectorChooserPolicy Chooser, DirectorFocusPolicy Focus)
{
    public static LiveDirectorWorkspaceProfile CreateDefault(string provenance = "openrpg:r125") => new(
        Guid.Parse("d1250000-0000-4000-8000-000000000001"), 1, provenance, NetworkAuthorityScope.Server,
        0, "live.director", false,
        new(Guid.Parse("d1250000-0000-4000-8000-000000000002"), true, true, true, true, 30, true, true),
        new(Guid.Parse("d1250000-0000-4000-8000-000000000003"), true, true, true, true, true, true, true, true),
        new(Guid.Parse("d1250000-0000-4000-8000-000000000004"), 64, ["single", "formation", "scatter"], true, true, true, true, true),
        new(Guid.Parse("d1250000-0000-4000-8000-000000000005"), true, true, ["zone", "type", "name", "tag", "player-state"], true, true),
        new(Guid.Parse("d1250000-0000-4000-8000-000000000006"), true, true, true, true, true));
}

public static class LiveDirectorWorkspaceValidator
{
    public static IReadOnlyList<NetworkDiagnostic> Validate(LiveDirectorWorkspaceProfile profile)
    {
        var d = new List<NetworkDiagnostic>();
        if (profile is null)
        {
            Add("LIVE_WORKSPACE_INCOMPLETE", "/liveDirector/workspace",
                "Workspace profile is incomplete.", Guid.Empty, d);
            return d;
        }
        if (profile.Client is null || profile.Creator is null || profile.Spawn is null ||
            profile.Chooser is null || profile.Focus is null)
        {
            Add("LIVE_WORKSPACE_INCOMPLETE", "/liveDirector/workspace",
                "Workspace policies are incomplete.", profile.Id, d);
            return d;
        }
        var ids = new[] { profile.Id, profile.Client.Id, profile.Creator.Id, profile.Spawn.Id, profile.Chooser.Id, profile.Focus.Id };
        if (ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Length) Add("LIVE_WORKSPACE_UUID_INVALID", "/liveDirector/workspace", "UUIDs must be unique.", profile.Id, d);
        if (profile.SchemaVersion != 1) Add("LIVE_WORKSPACE_SCHEMA_UNSUPPORTED", "/liveDirector/workspace/schemaVersion", "Only schema v1 is supported.", profile.Id, d);
        if (string.IsNullOrWhiteSpace(profile.Provenance) || profile.AuthorityScope != NetworkAuthorityScope.Server || profile.ServerRevision < 0 || string.IsNullOrWhiteSpace(profile.AuditIdentity)) Add("LIVE_WORKSPACE_IDENTITY_INVALID", "/liveDirector/workspace/identity", "Server identity is required.", profile.Id, d);
        if (!profile.Client.SeparateClient || !profile.Client.EngineIndependent || !profile.Client.RoleAuthentication || !profile.Client.SecondFactor || profile.Client.SessionMinutes is < 1 or > 1440 || !profile.Client.Revocation || !profile.Client.Audit) Add("LIVE_CLIENT_POLICY_INVALID", "/liveDirector/workspace/client", "Separate authenticated revocable audited client is required.", profile.Client.Id, d);
        if (!profile.Creator.ServerAllowlist || !profile.Creator.PackageFilter || !profile.Creator.TextFilter || !profile.Creator.TypeFilter || !profile.Creator.CostFilter || !profile.Creator.ZoneFilter || !profile.Creator.PermissionFilter || !profile.Creator.Thumbnails) Add("LIVE_CREATOR_POLICY_INVALID", "/liveDirector/workspace/creator", "Filtered allowlisted creator palette with thumbnails is required.", profile.Creator.Id, d);
        if (profile.Spawn.MaximumInstances is < 1 or > 1024 || profile.Spawn.PlacementModes is null ||
            profile.Spawn.PlacementModes.Count < 3 ||
            !new[] { "single", "formation", "scatter" }.All(profile.Spawn.PlacementModes.Contains) ||
            !profile.Spawn.FormationCoordinates || !profile.Spawn.Preview || !profile.Spawn.Atomic ||
            !profile.Spawn.StableIds || !profile.Spawn.SafeUndo) Add("LIVE_SPAWN_POLICY_INVALID", "/liveDirector/workspace/spawn", "Bounded previewed atomic spatial spawn and safe undo are required.", profile.Spawn.Id, d);
        if (!profile.Chooser.HierarchicalZones || !profile.Chooser.InstanceNavigation ||
            profile.Chooser.Filters is null || profile.Chooser.Filters.Count < 5 ||
            !profile.Chooser.Diagnostics || !profile.Chooser.PrivacyFiltering) Add("LIVE_CHOOSER_POLICY_INVALID", "/liveDirector/workspace/chooser", "Hierarchical private filtered chooser is required.", profile.Chooser.Id, d);
        if (!profile.Focus.SpectatorCamera || !profile.Focus.CrossZone || !profile.Focus.PrivacyOverrideRequiresPermission || !profile.Focus.ReadOnly || !profile.Focus.Audit) Add("LIVE_FOCUS_POLICY_INVALID", "/liveDirector/workspace/focus", "Read-only audited spectator focus is required.", profile.Focus.Id, d);
        return d;
    }
    private static void Add(string code, string path, string message, Guid id, ICollection<NetworkDiagnostic> d) => d.Add(new(code, path, message, id));
}

public sealed record LiveDirectorWorkspaceCommand(Guid CommandId, int SchemaVersion, string Provenance,
    long ExpectedRevision, string ActorId, NetworkRole Role, bool Apply, LiveDirectorWorkspaceProfile Candidate);
public sealed record LiveDirectorWorkspaceResult(bool Succeeded, bool Applied, LiveDirectorWorkspaceProfile Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics, string SemanticDiff, string AuditEntry);
public sealed class LiveDirectorWorkspaceHistory
{
    private readonly Stack<LiveDirectorWorkspaceProfile> _undo = [];
    private readonly Stack<LiveDirectorWorkspaceProfile> _redo = [];
    public LiveDirectorWorkspaceHistory(LiveDirectorWorkspaceProfile initial)
    {
        if (LiveDirectorWorkspaceValidator.Validate(initial).Count != 0) throw new ArgumentException("LIVE_WORKSPACE_INITIAL_INVALID");
        Current = initial;
    }
    public LiveDirectorWorkspaceProfile Current { get; private set; }
    public LiveDirectorWorkspaceResult Execute(LiveDirectorWorkspaceCommand command)
    {
        var d = LiveDirectorWorkspaceValidator.Validate(command.Candidate).ToList();
        if (command.CommandId == Guid.Empty || command.SchemaVersion != 1 || string.IsNullOrWhiteSpace(command.Provenance)) d.Add(new("LIVE_WORKSPACE_COMMAND_INVALID", "/liveDirector/workspace/command", "Command identity required.", command.CommandId));
        if (string.IsNullOrWhiteSpace(command.ActorId) || command.Role is not (NetworkRole.GameMaster or NetworkRole.Administrator)) d.Add(new("LIVE_WORKSPACE_PERMISSION_DENIED", "/liveDirector/workspace/audit/actor", "GM identity required.", command.Candidate.Id));
        if (command.ExpectedRevision != Current.ServerRevision || command.Candidate.ServerRevision != Current.ServerRevision + 1) d.Add(new("LIVE_WORKSPACE_REVISION_STALE", "/liveDirector/workspace/serverRevision", "Revision must advance once.", command.Candidate.Id));
        var diff = Diff(Current, command.Candidate);
        if (d.Count != 0) return Result(false, false, Current, d, diff, "rejected", command.ActorId);
        if (!command.Apply) return Result(true, false, Current, [], diff, "previewed", command.ActorId);
        _undo.Push(Current); Current = command.Candidate; _redo.Clear(); return Result(true, true, Current, [], diff, "applied", command.ActorId);
    }
    public LiveDirectorWorkspaceResult Undo(string actor, NetworkRole role) => Move(_undo, _redo, actor, role, "undo");
    public LiveDirectorWorkspaceResult Redo(string actor, NetworkRole role) => Move(_redo, _undo, actor, role, "redo");
    private LiveDirectorWorkspaceResult Move(Stack<LiveDirectorWorkspaceProfile> from, Stack<LiveDirectorWorkspaceProfile> to, string actor, NetworkRole role, string action)
    {
        if (from.Count == 0 || role is not (NetworkRole.GameMaster or NetworkRole.Administrator)) return Result(false, false, Current, [new("LIVE_WORKSPACE_HISTORY_DENIED", "/liveDirector/workspace/history", "History action unavailable.", Current.Id)], "no changes", "rejected", actor);
        var before = Current; to.Push(before); Current = from.Pop(); return Result(true, true, Current, [], Diff(before, Current), $"compensated:{action}", actor);
    }
    private LiveDirectorWorkspaceResult Result(bool ok, bool applied, LiveDirectorWorkspaceProfile p, IReadOnlyList<NetworkDiagnostic> d, string diff, string action, string actor) => new(ok, applied, p, d, diff, $"{action}:live.workspace:actor={actor}:revision={p.ServerRevision}");
    private static string Diff(LiveDirectorWorkspaceProfile a, LiveDirectorWorkspaceProfile b)
    {
        var values = new[] { a.Client != b.Client ? "client" : null, a.Creator != b.Creator ? "creator" : null, a.Spawn != b.Spawn ? "spawn" : null, a.Chooser != b.Chooser ? "chooser" : null, a.Focus != b.Focus ? "focus" : null, a.RequiredAtRuntime != b.RequiredAtRuntime ? "runtime" : null }.Where(x => x is not null).ToArray();
        return values.Length == 0 ? "revision-only" : string.Join(',', values);
    }
}

public sealed class LiveDirectorWorkspaceStore
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public string Serialize(LiveDirectorWorkspaceProfile profile)
    {
        var d = LiveDirectorWorkspaceValidator.Validate(profile); if (d.Count != 0) throw new InvalidDataException(d[0].Code);
        return JsonSerializer.Serialize(profile, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }
    public LiveDirectorWorkspaceProfile Deserialize(string json)
    {
        var profile = JsonSerializer.Deserialize<LiveDirectorWorkspaceProfile>(json, Options) ?? throw new InvalidDataException("LIVE_WORKSPACE_EMPTY");
        var d = LiveDirectorWorkspaceValidator.Validate(profile); if (d.Count != 0) throw new InvalidDataException(d[0].Code); return profile;
    }
}
public sealed record LiveDirectorWorkspaceResolution(bool Available, LiveDirectorWorkspaceProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);
public sealed record LiveDirectorWorkspaceRuntime(
    LiveDirectorWorkspaceProfile Profile,
    AuthorizationService Authorization,
    AccountService Accounts,
    DirectorAuthenticationService Authentication,
    LiveDirectorClient Client,
    LiveCatalogService Catalog,
    LiveWorldChooser Chooser,
    LiveSpawnService Spawn,
    LiveWorldActionService WorldActions);
public static class LiveDirectorWorkspaceRuntimeAdapter
{
    public static LiveDirectorWorkspaceResolution Resolve(LiveDirectorWorkspaceProfile? profile, bool required)
    {
        if (profile is null) return required ? new(false, null, [new("LIVE_WORKSPACE_REQUIRED_MISSING", "/liveDirector/workspace", "Required workspace profile is missing.")]) : new(false, null, []);
        var d = LiveDirectorWorkspaceValidator.Validate(profile); return new(d.Count == 0, d.Count == 0 ? profile : null, d);
    }

    public static LiveDirectorWorkspaceRuntime Create(LiveDirectorWorkspaceProfile profile,
        Func<DateTimeOffset> utcNow, Func<Guid> sessionIdFactory, Func<Guid> objectIdFactory)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(utcNow);
        ArgumentNullException.ThrowIfNull(sessionIdFactory);
        ArgumentNullException.ThrowIfNull(objectIdFactory);
        var diagnostics = LiveDirectorWorkspaceValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        var authorization = new AuthorizationService();
        var accounts = new AccountService();
        var chooser = new LiveWorldChooser(authorization);
        var catalog = new LiveCatalogService(authorization);
        return new(profile, authorization, accounts,
            new DirectorAuthenticationService(accounts, utcNow, sessionIdFactory,
                TimeSpan.FromMinutes(profile.Client.SessionMinutes)),
            new LiveDirectorClient(), catalog, chooser,
            new LiveSpawnService(catalog, chooser, objectIdFactory,
                profile.Spawn.MaximumInstances),
            new LiveWorldActionService(chooser, authorization));
    }
}
public readonly record struct LiveDirectorWorkspaceHandle(Guid ProfileId, int Generation);
public sealed record LiveDirectorWorkspaceSnapshot(Guid Id, long Revision, int SessionMinutes, int MaximumInstances, int FilterCount, bool SpectatorCamera);
public sealed class LiveDirectorWorkspaceScriptService(LiveDirectorWorkspaceProfile profile)
{
    public LiveDirectorWorkspaceHandle Handle { get; } = new(profile.Id, 1);
    public LiveDirectorWorkspaceSnapshot Read(LiveDirectorWorkspaceHandle handle)
    {
        if (handle.ProfileId != profile.Id || handle.Generation != 1) throw new InvalidOperationException("LIVE_WORKSPACE_HANDLE_INVALID");
        return new(profile.Id, profile.ServerRevision, profile.Client.SessionMinutes, profile.Spawn.MaximumInstances, profile.Chooser.Filters.Count, profile.Focus.SpectatorCamera);
    }
}
