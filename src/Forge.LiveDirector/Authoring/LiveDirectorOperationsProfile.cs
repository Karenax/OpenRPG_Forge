using System.Text.Json;
using Forge.LiveDirector.Audit;
using Forge.LiveDirector.Scenes;
using Forge.LiveDirector.Scripting;
using Forge.LiveDirector.Search;
using Forge.LiveDirector.State;
using Forge.LiveDirector.Visibility;
using Forge.LiveDirector.World;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.LiveDirector.Authoring;

public sealed record LiveDirectorOperationsProfile(Guid Id, int SchemaVersion, string Provenance,
    NetworkAuthorityScope AuthorityScope, long ServerRevision, string AuditIdentity,
    bool RequiredAtRuntime, bool IndexedSearch, bool NextMatch, bool SavedQueries,
    bool StableDeepLinks, bool VisualFocus, bool RegisteredCommandsOnly, bool SignedSnippets,
    bool RejectShellProcessFilesystemNetwork, bool ForgeScriptServerAdapter,
    IReadOnlyList<string> VariableKinds, bool VariableHistory, bool PersistentStateAdapter,
    IReadOnlyList<string> SceneFacets, bool MandatoryRollback, bool SceneRuntimeAdapters,
    IReadOnlyList<string> VisibilityLayers, bool SelectedPlayerProjection,
    bool ViewportOverlayAdapter)
{
    public static LiveDirectorOperationsProfile CreateDefault(
        string provenance = "openrpg:r129") => new(
        Guid.Parse("d1290000-0000-4000-8000-000000000001"), 1, provenance,
        NetworkAuthorityScope.Server, 0, "live.director", false,
        true, true, true, true, true,
        true, true, true, true,
        ["bool", "number", "enum", "string"], true, true,
        ["spawns", "dialogue", "music", "weather", "triggers"], true, true,
        ["hidden-objects", "secrets", "navigation", "encounters"], true, true);
}

public static class LiveDirectorOperationsValidator
{
    private static readonly string[] VariableKinds = ["bool", "number", "enum", "string"];
    private static readonly string[] SceneFacets =
        ["spawns", "dialogue", "music", "weather", "triggers"];
    private static readonly string[] VisibilityLayers =
        ["hidden-objects", "secrets", "navigation", "encounters"];

    public static IReadOnlyList<NetworkDiagnostic> Validate(
        LiveDirectorOperationsProfile? profile)
    {
        var diagnostics = new List<NetworkDiagnostic>();
        if (profile is null)
        {
            Add("LIVE_OPS_INCOMPLETE", "/liveDirector/operations",
                "Operations profile is incomplete.", Guid.Empty, diagnostics);
            return diagnostics;
        }
        if (profile.Id == Guid.Empty)
            Add("LIVE_OPS_UUID_INVALID", "/liveDirector/operations", "UUID required.",
                profile.Id, diagnostics);
        if (profile.SchemaVersion != 1)
            Add("LIVE_OPS_SCHEMA_UNSUPPORTED", "/liveDirector/operations/schemaVersion",
                "Only schema v1 is supported.", profile.Id, diagnostics);
        if (string.IsNullOrWhiteSpace(profile.Provenance) ||
            profile.AuthorityScope != NetworkAuthorityScope.Server ||
            profile.ServerRevision < 0 || string.IsNullOrWhiteSpace(profile.AuditIdentity))
            Add("LIVE_OPS_IDENTITY_INVALID", "/liveDirector/operations/identity",
                "Server identity required.", profile.Id, diagnostics);
        if (!profile.IndexedSearch || !profile.NextMatch || !profile.SavedQueries ||
            !profile.StableDeepLinks || !profile.VisualFocus)
            Add("LIVE_SEARCH_POLICY_INVALID", "/liveDirector/operations/search",
                "Indexed cycling search, saved queries, deep links and visual focus are required.",
                profile.Id, diagnostics);
        if (!profile.RegisteredCommandsOnly || !profile.SignedSnippets ||
            !profile.RejectShellProcessFilesystemNetwork || !profile.ForgeScriptServerAdapter)
            Add("LIVE_SCRIPT_POLICY_INVALID", "/liveDirector/operations/script",
                "Registered or signed bounded server scripts are required.", profile.Id,
                diagnostics);
        if (!Canonical(profile.VariableKinds, VariableKinds) || !profile.VariableHistory ||
            !profile.PersistentStateAdapter)
            Add("LIVE_VARIABLE_POLICY_INVALID", "/liveDirector/operations/variables",
                "Four typed persistent variable kinds and history are required.", profile.Id,
                diagnostics);
        if (!Canonical(profile.SceneFacets, SceneFacets) || !profile.MandatoryRollback ||
            !profile.SceneRuntimeAdapters)
            Add("LIVE_SCENE_POLICY_INVALID", "/liveDirector/operations/scenes",
                "Five scene facets, rollback and runtime adapters are required.", profile.Id,
                diagnostics);
        if (!Canonical(profile.VisibilityLayers, VisibilityLayers) ||
            !profile.SelectedPlayerProjection || !profile.ViewportOverlayAdapter)
            Add("LIVE_VISIBILITY_POLICY_INVALID", "/liveDirector/operations/visibility",
                "Four visibility layers, player projection and viewport overlay are required.",
                profile.Id, diagnostics);
        return diagnostics;
    }

    private static bool Canonical(IReadOnlyList<string>? actual,
        IReadOnlyList<string> expected) => actual is not null && actual.SequenceEqual(expected);

    private static void Add(string code, string path, string message, Guid id,
        ICollection<NetworkDiagnostic> diagnostics) =>
        diagnostics.Add(new(code, path, message, id));
}

public sealed record LiveDirectorOperationsCommand(Guid CommandId, int SchemaVersion,
    string Provenance, long ExpectedRevision, string ActorId, NetworkRole Role, bool Apply,
    LiveDirectorOperationsProfile Candidate);

public sealed record LiveDirectorOperationsResult(bool Succeeded, bool Applied,
    LiveDirectorOperationsProfile Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics,
    string SemanticDiff, string AuditEntry);

public sealed class LiveDirectorOperationsHistory
{
    private readonly Stack<LiveDirectorOperationsProfile> _undo = [];
    private readonly Stack<LiveDirectorOperationsProfile> _redo = [];

    public LiveDirectorOperationsHistory(LiveDirectorOperationsProfile initial)
    {
        if (LiveDirectorOperationsValidator.Validate(initial).Count != 0)
            throw new ArgumentException("LIVE_OPS_INITIAL_INVALID", nameof(initial));
        Current = initial;
    }

    public LiveDirectorOperationsProfile Current { get; private set; }

    public LiveDirectorOperationsResult Execute(LiveDirectorOperationsCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var diagnostics = LiveDirectorOperationsValidator.Validate(command.Candidate).ToList();
        if (command.CommandId == Guid.Empty || command.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(command.Provenance))
            diagnostics.Add(new("LIVE_OPS_COMMAND_INVALID",
                "/liveDirector/operations/command", "Command identity required.",
                command.CommandId));
        if (string.IsNullOrWhiteSpace(command.ActorId) ||
            command.Role is not (NetworkRole.GameMaster or NetworkRole.Administrator))
            diagnostics.Add(new("LIVE_OPS_PERMISSION_DENIED",
                "/liveDirector/operations/audit/actor", "GM identity required.",
                command.Candidate.Id));
        if (command.ExpectedRevision != Current.ServerRevision ||
            command.Candidate.ServerRevision != Current.ServerRevision + 1)
            diagnostics.Add(new("LIVE_OPS_REVISION_STALE",
                "/liveDirector/operations/serverRevision", "Revision must advance once.",
                command.Candidate.Id));
        var diff = Diff(Current, command.Candidate);
        if (diagnostics.Count != 0)
            return Result(false, false, Current, diagnostics, diff, "rejected",
                command.ActorId);
        if (!command.Apply)
            return Result(true, false, Current, [], diff, "previewed", command.ActorId);
        _undo.Push(Current);
        Current = command.Candidate;
        _redo.Clear();
        return Result(true, true, Current, [], diff, "applied", command.ActorId);
    }

    public LiveDirectorOperationsResult Undo(string actor, NetworkRole role) =>
        Move(_undo, _redo, actor, role, "undo");

    public LiveDirectorOperationsResult Redo(string actor, NetworkRole role) =>
        Move(_redo, _undo, actor, role, "redo");

    private LiveDirectorOperationsResult Move(Stack<LiveDirectorOperationsProfile> from,
        Stack<LiveDirectorOperationsProfile> to, string actor, NetworkRole role, string action)
    {
        if (from.Count == 0 || string.IsNullOrWhiteSpace(actor) ||
            role is not (NetworkRole.GameMaster or NetworkRole.Administrator))
            return Result(false, false, Current,
                [new("LIVE_OPS_HISTORY_DENIED", "/liveDirector/operations/history",
                    "History unavailable.", Current.Id)], "no changes", "rejected", actor);
        var before = Current;
        to.Push(before);
        Current = from.Pop();
        return Result(true, true, Current, [], Diff(before, Current),
            $"compensated:{action}", actor);
    }

    private static string Diff(LiveDirectorOperationsProfile before,
        LiveDirectorOperationsProfile after)
    {
        var values = new[]
        {
            before.IndexedSearch != after.IndexedSearch || before.NextMatch != after.NextMatch ||
            before.SavedQueries != after.SavedQueries ||
            before.StableDeepLinks != after.StableDeepLinks ||
            before.VisualFocus != after.VisualFocus ? "search" : null,
            before.RegisteredCommandsOnly != after.RegisteredCommandsOnly ||
            before.SignedSnippets != after.SignedSnippets ||
            before.RejectShellProcessFilesystemNetwork !=
            after.RejectShellProcessFilesystemNetwork ||
            before.ForgeScriptServerAdapter != after.ForgeScriptServerAdapter ? "script" : null,
            !Same(before.VariableKinds, after.VariableKinds) ||
            before.VariableHistory != after.VariableHistory ||
            before.PersistentStateAdapter != after.PersistentStateAdapter ? "variables" : null,
            !Same(before.SceneFacets, after.SceneFacets) ||
            before.MandatoryRollback != after.MandatoryRollback ||
            before.SceneRuntimeAdapters != after.SceneRuntimeAdapters ? "scenes" : null,
            !Same(before.VisibilityLayers, after.VisibilityLayers) ||
            before.SelectedPlayerProjection != after.SelectedPlayerProjection ||
            before.ViewportOverlayAdapter != after.ViewportOverlayAdapter ? "visibility" : null,
            before.RequiredAtRuntime != after.RequiredAtRuntime ? "runtime" : null
        }.Where(value => value is not null).ToArray();
        return values.Length == 0 ? "revision-only" : string.Join(',', values);
    }

    private static bool Same(IReadOnlyList<string>? left, IReadOnlyList<string>? right) =>
        left is not null && right is not null && left.SequenceEqual(right);

    private static LiveDirectorOperationsResult Result(bool succeeded, bool applied,
        LiveDirectorOperationsProfile profile, IReadOnlyList<NetworkDiagnostic> diagnostics,
        string diff, string action, string actor) => new(succeeded, applied, profile,
        diagnostics, diff,
        $"{action}:live.operations:actor={actor}:revision={profile.ServerRevision}");
}

public sealed class LiveDirectorOperationsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string Serialize(LiveDirectorOperationsProfile profile)
    {
        var diagnostics = LiveDirectorOperationsValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return JsonSerializer.Serialize(profile, Options)
                   .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public LiveDirectorOperationsProfile Deserialize(string json)
    {
        var profile = JsonSerializer.Deserialize<LiveDirectorOperationsProfile>(json, Options)
                      ?? throw new InvalidDataException("LIVE_OPS_EMPTY");
        var diagnostics = LiveDirectorOperationsValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return profile;
    }
}

public sealed record LiveDirectorOperationsResolution(bool Available,
    LiveDirectorOperationsProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);

public sealed record LiveDirectorOperationsRuntime(LiveDirectorOperationsProfile Profile,
    AuthorizationService Authorization, LiveWorldChooser Chooser, LiveAuditJournal Audit,
    LiveSearchService Search, LiveScriptService Scripts, LiveVariableService Variables,
    LiveSceneService Scenes, LiveVisibilityService Visibility);

public static class LiveDirectorOperationsRuntimeAdapter
{
    public static LiveDirectorOperationsResolution Resolve(LiveDirectorOperationsProfile? profile,
        bool required)
    {
        if (profile is null)
            return required
                ? new(false, null,
                    [new("LIVE_OPS_REQUIRED_MISSING", "/liveDirector/operations",
                        "Required operations profile missing.")])
                : new(false, null, []);
        var diagnostics = LiveDirectorOperationsValidator.Validate(profile);
        return new(diagnostics.Count == 0, diagnostics.Count == 0 ? profile : null,
            diagnostics);
    }

    public static LiveDirectorOperationsRuntime Create(LiveDirectorOperationsProfile profile,
        Func<string, bool> signatureVerifier, Func<DateTimeOffset> utcNow)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(signatureVerifier);
        ArgumentNullException.ThrowIfNull(utcNow);
        var diagnostics = LiveDirectorOperationsValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        var authorization = new AuthorizationService();
        var chooser = new LiveWorldChooser(authorization);
        var audit = new LiveAuditJournal(utcNow);
        return new(profile, authorization, chooser, audit, new LiveSearchService(chooser),
            new LiveScriptService(authorization, signatureVerifier),
            new LiveVariableService(authorization, audit),
            new LiveSceneService(authorization, audit),
            new LiveVisibilityService(authorization));
    }
}

public static class LiveDirectorOperationsLocalization
{
    public static string French(string? code) => code switch
    {
        "LIVE_SEARCH_QUERY_INVALID" => "La recherche enregistrée est incomplète.",
        "LIVE_SCRIPT_PERMISSION_DENIED" => "Le rôle courant ne peut pas exécuter ce script.",
        "LIVE_SCRIPT_REVISION_STALE" => "Le moteur de script a changé ; prévisualisez de nouveau.",
        "LIVE_SCRIPT_FORBIDDEN_API" => "Le script tente d’utiliser une API interdite.",
        "LIVE_SCRIPT_SIGNATURE_INVALID" => "La signature du snippet est invalide.",
        "LIVE_VARIABLE_VALUE_INVALID" => "La valeur ne respecte pas son type ou ses bornes.",
        "LIVE_VARIABLE_PERMISSION_DENIED" => "Le rôle courant ne peut pas modifier ces variables.",
        "LIVE_SCENE_PERMISSION_DENIED" => "Le rôle courant ne peut pas lancer cette scène.",
        "LIVE_SCENE_ROLLBACK_EMPTY" => "Aucune scène active ne peut être restaurée.",
        "LIVE_VISIBILITY_PERMISSION_DENIED" => "Le rôle courant ne peut pas projeter la visibilité.",
        "LIVE_OPS_PERMISSION_DENIED" => "La permission Maître du jeu est requise.",
        "LIVE_OPS_REVISION_STALE" => "La source a changé ; prévisualisez de nouveau.",
        null or "" => "Opération Live Director invalide.",
        _ => "Action refusée par l’autorité d’opérations Live Director."
    };
}

public readonly record struct LiveDirectorOperationsHandle(Guid ProfileId, int Generation);
public sealed record LiveDirectorOperationsSnapshot(Guid Id, long Revision, int VariableKinds,
    int SceneFacets, int VisibilityLayers, bool VisualFocus);

public sealed class LiveDirectorOperationsScriptService(LiveDirectorOperationsProfile profile)
{
    public LiveDirectorOperationsHandle Handle { get; } = new(profile.Id, 1);

    public LiveDirectorOperationsSnapshot Read(LiveDirectorOperationsHandle handle)
    {
        if (handle.ProfileId != profile.Id || handle.Generation != 1)
            throw new InvalidOperationException("LIVE_OPS_HANDLE_INVALID");
        return new(profile.Id, profile.ServerRevision, profile.VariableKinds.Count,
            profile.SceneFacets.Count, profile.VisibilityLayers.Count, profile.VisualFocus);
    }
}
