using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Network.Operations;
using Forge.Network.Sessions;
using Forge.Network.World;

namespace Forge.Network.Authoring;

public enum CharacterAuthorityMode { Local, SignedUpload, ServerManaged, AccountBound }

public sealed record DiscoveryAccessPolicy(
    Guid Id,
    bool DirectAddress,
    bool LanDiscovery,
    bool CuratedDirectory,
    bool Favorites,
    bool RecentServers,
    bool InviteLinks);

public sealed record LobbyAccessPolicy(
    Guid Id,
    string RulesetId,
    string ContentFingerprint,
    int MaximumSlots,
    int MaximumTeams,
    bool CharacterSelection,
    bool PermissionsVisible,
    bool ReadyState);

public sealed record CharacterBuilderPolicy(
    Guid Id,
    string RulesetId,
    IReadOnlyList<string> TemplateIds,
    bool PreviewEnabled,
    bool ImportAllowed,
    bool ServerApprovalRequired,
    int MinimumAttribute,
    int MaximumAttribute);

public sealed record CharacterAuthorityPolicy(
    Guid Id,
    IReadOnlyList<CharacterAuthorityMode> AllowedModes,
    bool SignedUploadsRequired,
    bool EncryptedAtRest,
    bool MigrationEnabled,
    bool AccountBindingRequired);

public sealed record PlayerCombatPolicy(
    Guid Id,
    bool GlobalPolicy,
    bool AreaOverrides,
    bool PartyOverrides,
    bool ConsentRequired,
    bool DuelsEnabled,
    bool CrimeEnabled,
    bool SafeZonesEnabled,
    bool ServerOverride,
    bool PvpEnabled,
    FriendlyFirePolicy FriendlyFire);

public sealed record NetworkPlayerExperienceProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    NetworkAuthorityScope AuthorityScope,
    long ServerRevision,
    string AuditIdentity,
    bool RequiredAtRuntime,
    DiscoveryAccessPolicy Discovery,
    LobbyAccessPolicy Lobby,
    CharacterBuilderPolicy CharacterBuilder,
    CharacterAuthorityPolicy CharacterAuthority,
    PlayerCombatPolicy Combat)
{
    public static NetworkPlayerExperienceProfile CreateDefault(string provenance = "openrpg:r121") => new(
        Guid.Parse("a1210000-0000-4000-8000-000000000001"), 1, provenance,
        NetworkAuthorityScope.Server, 0, "network.author", false,
        new(Guid.Parse("a1210000-0000-4000-8000-000000000002"), true, true, true, true, true, true),
        new(Guid.Parse("a1210000-0000-4000-8000-000000000003"), "ruleset:default",
            "module-build-fingerprint", 8, 2, true, true, true),
        new(Guid.Parse("a1210000-0000-4000-8000-000000000004"), "ruleset:default",
            ["template:adventurer"], true, true, true, 3, 20),
        new(Guid.Parse("a1210000-0000-4000-8000-000000000005"),
            [
                CharacterAuthorityMode.Local,
                CharacterAuthorityMode.SignedUpload,
                CharacterAuthorityMode.ServerManaged,
                CharacterAuthorityMode.AccountBound
            ], true, true, true, true),
        new(Guid.Parse("a1210000-0000-4000-8000-000000000006"), true, true, true, true, true, true, true, true,
            false, FriendlyFirePolicy.Disabled));
}

public static class NetworkPlayerExperienceValidator
{
    public static IReadOnlyList<NetworkDiagnostic> Validate(NetworkPlayerExperienceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = new List<NetworkDiagnostic>();
        if (profile.Discovery is null || profile.Lobby is null || profile.CharacterBuilder is null ||
            profile.CharacterAuthority is null || profile.Combat is null ||
            profile.CharacterBuilder.TemplateIds is null || profile.CharacterAuthority.AllowedModes is null)
        {
            diagnostics.Add(Diagnostic("NET_PLAYER_PROFILE_INCOMPLETE", "/network/playerExperience", profile.Id,
                "Discovery, lobby, character builder, authority and combat policies are required."));
            return diagnostics;
        }
        var ids = new[]
        {
            profile.Id, profile.Discovery.Id, profile.Lobby.Id, profile.CharacterBuilder.Id,
            profile.CharacterAuthority.Id, profile.Combat.Id
        };
        if (ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            diagnostics.Add(Diagnostic("NET_PLAYER_UUID_INVALID", "/network/playerExperience", profile.Id,
                "Player experience UUIDs must be non-empty and unique."));
        if (profile.SchemaVersion != 1)
            diagnostics.Add(Diagnostic("NET_PLAYER_SCHEMA_UNSUPPORTED", "/network/playerExperience/schemaVersion", profile.Id,
                "Only player experience schema v1 is supported."));
        if (string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(Diagnostic("NET_PLAYER_PROVENANCE_REQUIRED", "/network/playerExperience/provenance", profile.Id,
                "Modification provenance is required."));
        if (profile.AuthorityScope != NetworkAuthorityScope.Server)
            diagnostics.Add(Diagnostic("NET_PLAYER_SCOPE_INVALID", "/network/playerExperience/authorityScope", profile.Id,
                "Server authority is required."));
        if (profile.ServerRevision < 0 || string.IsNullOrWhiteSpace(profile.AuditIdentity))
            diagnostics.Add(Diagnostic("NET_PLAYER_AUDIT_INVALID", "/network/playerExperience/audit", profile.Id,
                "A non-negative revision and audit identity are required."));
        if (!(profile.Discovery.DirectAddress || profile.Discovery.LanDiscovery || profile.Discovery.CuratedDirectory))
            diagnostics.Add(Diagnostic("NET_DISCOVERY_ROUTE_REQUIRED", "/network/playerExperience/discovery", profile.Discovery.Id,
                "At least one connection route is required."));
        if (!profile.Discovery.Favorites || !profile.Discovery.RecentServers || !profile.Discovery.InviteLinks)
            diagnostics.Add(Diagnostic("NET_DISCOVERY_HISTORY_REQUIRED", "/network/playerExperience/discovery/history", profile.Discovery.Id,
                "Favorites, recent servers and invite links must remain available."));
        if (string.IsNullOrWhiteSpace(profile.Lobby.RulesetId) || string.IsNullOrWhiteSpace(profile.Lobby.ContentFingerprint) ||
            profile.Lobby.MaximumSlots is < 1 or > 64 || profile.Lobby.MaximumTeams is < 1 or > 16 ||
            !profile.Lobby.CharacterSelection || !profile.Lobby.PermissionsVisible || !profile.Lobby.ReadyState)
            diagnostics.Add(Diagnostic("NET_LOBBY_POLICY_INVALID", "/network/playerExperience/lobby", profile.Lobby.Id,
                "Lobby rules, content, slots, teams, character selection, permissions and ready state are required."));
        if (string.IsNullOrWhiteSpace(profile.CharacterBuilder.RulesetId) || profile.CharacterBuilder.TemplateIds.Count == 0 ||
            profile.CharacterBuilder.TemplateIds.Any(string.IsNullOrWhiteSpace) ||
            profile.CharacterBuilder.TemplateIds.Distinct(StringComparer.Ordinal).Count() != profile.CharacterBuilder.TemplateIds.Count ||
            !profile.CharacterBuilder.PreviewEnabled || !profile.CharacterBuilder.ServerApprovalRequired ||
            profile.CharacterBuilder.MinimumAttribute < 0 ||
            profile.CharacterBuilder.MaximumAttribute <= profile.CharacterBuilder.MinimumAttribute)
            diagnostics.Add(Diagnostic("NET_CHARACTER_BUILDER_INVALID", "/network/playerExperience/characterBuilder", profile.CharacterBuilder.Id,
                "Ruleset, unique templates, preview, server approval and attribute bounds are required."));
        var modes = profile.CharacterAuthority.AllowedModes;
        if (modes.Count == 0 || modes.Distinct().Count() != modes.Count ||
            !modes.Contains(CharacterAuthorityMode.ServerManaged) ||
            !modes.Contains(CharacterAuthorityMode.AccountBound) || !profile.CharacterAuthority.SignedUploadsRequired ||
            !profile.CharacterAuthority.EncryptedAtRest || !profile.CharacterAuthority.MigrationEnabled ||
            !profile.CharacterAuthority.AccountBindingRequired)
            diagnostics.Add(Diagnostic("NET_CHARACTER_AUTHORITY_INVALID", "/network/playerExperience/characterAuthority",
                profile.CharacterAuthority.Id, "Signed, encrypted, migratable server/account character authority is required."));
        if (!profile.Combat.GlobalPolicy || !profile.Combat.AreaOverrides || !profile.Combat.PartyOverrides ||
            !profile.Combat.ConsentRequired || !profile.Combat.DuelsEnabled || !profile.Combat.CrimeEnabled ||
            !profile.Combat.SafeZonesEnabled || !profile.Combat.ServerOverride ||
            !Enum.IsDefined(profile.Combat.FriendlyFire))
            diagnostics.Add(Diagnostic("NET_COMBAT_POLICY_INVALID", "/network/playerExperience/combat", profile.Combat.Id,
                "Global, area, party, consent, duel, crime, safe-zone and server policies are required."));
        return diagnostics;
    }

    private static NetworkDiagnostic Diagnostic(string code, string path, Guid id, string message) =>
        new(code, path, message, id);
}

public sealed record NetworkPlayerExperienceCommand(
    Guid CommandId,
    int SchemaVersion,
    string Provenance,
    long ExpectedRevision,
    string ActorId,
    NetworkRole Role,
    bool Apply,
    NetworkPlayerExperienceProfile Candidate);

public sealed record NetworkPlayerExperienceResult(
    bool Succeeded,
    bool Applied,
    NetworkPlayerExperienceProfile Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics,
    string SemanticDiff,
    string AuditEntry);

public sealed class NetworkPlayerExperienceHistory
{
    private readonly Stack<NetworkPlayerExperienceProfile> _undo = [];
    private readonly Stack<NetworkPlayerExperienceProfile> _redo = [];

    public NetworkPlayerExperienceHistory(NetworkPlayerExperienceProfile initial)
    {
        if (NetworkPlayerExperienceValidator.Validate(initial).Count != 0)
            throw new ArgumentException("NET_PLAYER_EXPERIENCE_INITIAL_INVALID", nameof(initial));
        Current = initial;
    }

    public NetworkPlayerExperienceProfile Current { get; private set; }

    public NetworkPlayerExperienceResult Execute(NetworkPlayerExperienceCommand command)
    {
        var diagnostics = NetworkPlayerExperienceValidator.Validate(command.Candidate).ToList();
        if (command.CommandId == Guid.Empty || command.SchemaVersion != 1 || string.IsNullOrWhiteSpace(command.Provenance))
            diagnostics.Add(new("NET_PLAYER_COMMAND_INVALID", "/network/playerExperience/command",
                "Command UUID, schema and provenance are required.", command.CommandId));
        if (string.IsNullOrWhiteSpace(command.ActorId) || command.Role != NetworkRole.Administrator)
            diagnostics.Add(new("NET_PLAYER_PERMISSION_DENIED", "/network/playerExperience/audit/actor",
                "Administrator identity is required.", command.Candidate.Id));
        if (command.ExpectedRevision != Current.ServerRevision ||
            command.Candidate.ServerRevision != Current.ServerRevision + 1)
            diagnostics.Add(new("NET_PLAYER_REVISION_STALE", "/network/playerExperience/serverRevision",
                "Candidate must advance the current server revision exactly once.", command.Candidate.Id));
        var diff = Diff(Current, command.Candidate);
        if (diagnostics.Count != 0)
            return Result(false, false, Current, diagnostics, diff, "rejected", command.ActorId);
        if (!command.Apply)
            return Result(true, false, Current, [], diff, "previewed", command.ActorId);
        _undo.Push(Current);
        Current = command.Candidate;
        _redo.Clear();
        return Result(true, true, Current, [], diff, "applied", command.ActorId);
    }

    public NetworkPlayerExperienceResult Undo(string actorId, NetworkRole role) => Move(_undo, _redo, actorId, role, "undo");
    public NetworkPlayerExperienceResult Redo(string actorId, NetworkRole role) => Move(_redo, _undo, actorId, role, "redo");

    private NetworkPlayerExperienceResult Move(Stack<NetworkPlayerExperienceProfile> source,
        Stack<NetworkPlayerExperienceProfile> destination, string actorId, NetworkRole role, string action)
    {
        if (source.Count == 0 || role != NetworkRole.Administrator)
            return Result(false, false, Current,
                [new("NET_PLAYER_HISTORY_DENIED", "/network/playerExperience/history", "History action is unavailable or forbidden.", Current.Id)],
                "no changes", "rejected", actorId);
        var previous = Current;
        destination.Push(previous);
        Current = source.Pop();
        return Result(true, true, Current, [], Diff(previous, Current), $"compensated:{action}", actorId);
    }

    private NetworkPlayerExperienceResult Result(bool succeeded, bool applied,
        NetworkPlayerExperienceProfile profile, IReadOnlyList<NetworkDiagnostic> diagnostics, string diff,
        string action, string actorId) => new(succeeded, applied, profile, diagnostics, diff,
        $"{action}:network.player-experience:actor={actorId}:revision={profile.ServerRevision}");

    private static string Diff(NetworkPlayerExperienceProfile before, NetworkPlayerExperienceProfile after)
    {
        var changes = new List<string>();
        if (before.Discovery != after.Discovery) changes.Add("discovery");
        if (before.Lobby != after.Lobby) changes.Add("lobby");
        if (!CharacterBuilderEquals(before.CharacterBuilder, after.CharacterBuilder)) changes.Add("character-builder");
        if (!CharacterAuthorityEquals(before.CharacterAuthority, after.CharacterAuthority)) changes.Add("character-authority");
        if (before.Combat != after.Combat) changes.Add("combat");
        return changes.Count == 0 ? "revision-only" : string.Join(',', changes);
    }

    private static bool CharacterBuilderEquals(CharacterBuilderPolicy left, CharacterBuilderPolicy right) =>
        left.Id == right.Id && string.Equals(left.RulesetId, right.RulesetId, StringComparison.Ordinal) &&
        left.TemplateIds.SequenceEqual(right.TemplateIds, StringComparer.Ordinal) &&
        left.PreviewEnabled == right.PreviewEnabled && left.ImportAllowed == right.ImportAllowed &&
        left.ServerApprovalRequired == right.ServerApprovalRequired &&
        left.MinimumAttribute == right.MinimumAttribute && left.MaximumAttribute == right.MaximumAttribute;

    private static bool CharacterAuthorityEquals(CharacterAuthorityPolicy left, CharacterAuthorityPolicy right) =>
        left.Id == right.Id && left.AllowedModes.SequenceEqual(right.AllowedModes) &&
        left.SignedUploadsRequired == right.SignedUploadsRequired && left.EncryptedAtRest == right.EncryptedAtRest &&
        left.MigrationEnabled == right.MigrationEnabled && left.AccountBindingRequired == right.AccountBindingRequired;
}

public sealed class NetworkPlayerExperienceStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string Serialize(NetworkPlayerExperienceProfile profile)
    {
        var diagnostics = NetworkPlayerExperienceValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return JsonSerializer.Serialize(profile, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public NetworkPlayerExperienceProfile Deserialize(string json)
    {
        var profile = JsonSerializer.Deserialize<NetworkPlayerExperienceProfile>(json, Options)
            ?? throw new InvalidDataException("NET_PLAYER_EXPERIENCE_EMPTY");
        var diagnostics = NetworkPlayerExperienceValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return profile;
    }
}

public sealed record NetworkPlayerExperienceResolution(
    bool Available,
    NetworkPlayerExperienceProfile? Profile,
    IReadOnlyList<NetworkDiagnostic> Diagnostics);

public static class NetworkPlayerExperienceRuntimeAdapter
{
    public static NetworkPlayerExperienceResolution Resolve(NetworkPlayerExperienceProfile? profile, bool required)
    {
        if (profile is null)
            return required
                ? new(false, null, [new("NET_PLAYER_EXPERIENCE_REQUIRED_MISSING", "/network/playerExperience",
                    "Required player experience profile is missing.")])
                : new(false, null, []);
        var diagnostics = NetworkPlayerExperienceValidator.Validate(profile);
        return new(diagnostics.Count == 0, diagnostics.Count == 0 ? profile : null, diagnostics);
    }
}

public readonly record struct NetworkPlayerExperienceHandle(Guid ProfileId, int Generation);

public sealed record NetworkPlayerExperienceSnapshot(
    Guid Id,
    long Revision,
    int ConnectionRoutes,
    int LobbySlots,
    int CharacterTemplates,
    int AuthorityModes,
    bool ConsentRequired,
    bool PvpEnabled,
    FriendlyFirePolicy FriendlyFire);

public sealed class NetworkPlayerExperienceScriptService(NetworkPlayerExperienceProfile profile)
{
    public NetworkPlayerExperienceHandle Handle { get; } = new(profile.Id, 1);

    public NetworkPlayerExperienceSnapshot Read(NetworkPlayerExperienceHandle handle)
    {
        if (handle.ProfileId != profile.Id || handle.Generation != 1)
            throw new InvalidOperationException("NET_PLAYER_EXPERIENCE_HANDLE_INVALID");
        var routes = Convert.ToInt32(profile.Discovery.DirectAddress) + Convert.ToInt32(profile.Discovery.LanDiscovery) +
                     Convert.ToInt32(profile.Discovery.CuratedDirectory);
        return new(profile.Id, profile.ServerRevision, routes, profile.Lobby.MaximumSlots,
            profile.CharacterBuilder.TemplateIds.Count, profile.CharacterAuthority.AllowedModes.Count,
            profile.Combat.ConsentRequired, profile.Combat.PvpEnabled, profile.Combat.FriendlyFire);
    }
}
