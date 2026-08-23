using System.Text.Json;
using Forge.LiveDirector.Audit;
using Forge.LiveDirector.Encounters;
using Forge.LiveDirector.Narrative;
using Forge.Network.Authoring;
using Forge.Network.Operations;
using Forge.Network.Sessions;

namespace Forge.LiveDirector.Authoring;

public sealed record LiveDirectorNarrativeProfile(Guid Id, int SchemaVersion, string Provenance,
    NetworkAuthorityScope AuthorityScope, long ServerRevision, string AuditIdentity,
    bool RequiredAtRuntime, bool QuestInspection, IReadOnlyList<string> QuestScopes,
    bool ConsequencePreview, bool QuestRuntimeAdapter, IReadOnlyList<string> EncounterActions,
    bool BoundedScaling, bool CreatureSpawnAdapter, bool AiRuntimeAdapter)
{
    public static LiveDirectorNarrativeProfile CreateDefault(
        string provenance = "openrpg:r130") => new(
        Guid.Parse("d1300000-0000-4000-8000-000000000001"), 1, provenance,
        NetworkAuthorityScope.Server, 0, "live.director", false, true,
        ["all-players", "selected-players"], true, true,
        ["activate", "pause", "wave", "scale", "despawn"], true, true, true);
}

public static class LiveDirectorNarrativeValidator
{
    private static readonly string[] QuestScopes = ["all-players", "selected-players"];
    private static readonly string[] EncounterActions =
        ["activate", "pause", "wave", "scale", "despawn"];

    public static IReadOnlyList<NetworkDiagnostic> Validate(
        LiveDirectorNarrativeProfile? profile)
    {
        var diagnostics = new List<NetworkDiagnostic>();
        if (profile is null)
        {
            Add("LIVE_NARRATIVE_INCOMPLETE", "/liveDirector/narrative",
                "Narrative profile is incomplete.", Guid.Empty, diagnostics);
            return diagnostics;
        }
        if (profile.Id == Guid.Empty)
            Add("LIVE_NARRATIVE_UUID_INVALID", "/liveDirector/narrative", "UUID required.",
                profile.Id, diagnostics);
        if (profile.SchemaVersion != 1)
            Add("LIVE_NARRATIVE_SCHEMA_UNSUPPORTED", "/liveDirector/narrative/schemaVersion",
                "Only schema v1 is supported.", profile.Id, diagnostics);
        if (string.IsNullOrWhiteSpace(profile.Provenance) ||
            profile.AuthorityScope != NetworkAuthorityScope.Server ||
            profile.ServerRevision < 0 || string.IsNullOrWhiteSpace(profile.AuditIdentity))
            Add("LIVE_NARRATIVE_IDENTITY_INVALID", "/liveDirector/narrative/identity",
                "Server identity required.", profile.Id, diagnostics);
        if (!profile.QuestInspection || !Canonical(profile.QuestScopes, QuestScopes) ||
            !profile.ConsequencePreview || !profile.QuestRuntimeAdapter)
            Add("LIVE_QUEST_POLICY_INVALID", "/liveDirector/narrative/quests",
                "Inspection, two player scopes, consequence preview and RPG adapter are required.",
                profile.Id, diagnostics);
        if (!Canonical(profile.EncounterActions, EncounterActions) ||
            !profile.BoundedScaling || !profile.CreatureSpawnAdapter ||
            !profile.AiRuntimeAdapter)
            Add("LIVE_ENCOUNTER_POLICY_INVALID", "/liveDirector/narrative/encounters",
                "Five encounter commands, bounded scaling and spawn/AI adapters are required.",
                profile.Id, diagnostics);
        return diagnostics;
    }

    private static bool Canonical(IReadOnlyList<string>? actual,
        IReadOnlyList<string> expected) => actual is not null && actual.SequenceEqual(expected);

    private static void Add(string code, string path, string message, Guid id,
        ICollection<NetworkDiagnostic> diagnostics) =>
        diagnostics.Add(new(code, path, message, id));
}

public sealed record LiveDirectorNarrativeCommand(Guid CommandId, int SchemaVersion,
    string Provenance, long ExpectedRevision, string ActorId, NetworkRole Role, bool Apply,
    LiveDirectorNarrativeProfile Candidate);

public sealed record LiveDirectorNarrativeResult(bool Succeeded, bool Applied,
    LiveDirectorNarrativeProfile Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics,
    string SemanticDiff, string AuditEntry);

public sealed class LiveDirectorNarrativeHistory
{
    private readonly Stack<LiveDirectorNarrativeProfile> _undo = [];
    private readonly Stack<LiveDirectorNarrativeProfile> _redo = [];

    public LiveDirectorNarrativeHistory(LiveDirectorNarrativeProfile initial)
    {
        if (LiveDirectorNarrativeValidator.Validate(initial).Count != 0)
            throw new ArgumentException("LIVE_NARRATIVE_INITIAL_INVALID", nameof(initial));
        Current = initial;
    }

    public LiveDirectorNarrativeProfile Current { get; private set; }

    public LiveDirectorNarrativeResult Execute(LiveDirectorNarrativeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var diagnostics = LiveDirectorNarrativeValidator.Validate(command.Candidate).ToList();
        if (command.CommandId == Guid.Empty || command.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(command.Provenance))
            diagnostics.Add(new("LIVE_NARRATIVE_COMMAND_INVALID",
                "/liveDirector/narrative/command", "Command identity required.",
                command.CommandId));
        if (string.IsNullOrWhiteSpace(command.ActorId) ||
            command.Role is not (NetworkRole.GameMaster or NetworkRole.Administrator))
            diagnostics.Add(new("LIVE_NARRATIVE_PERMISSION_DENIED",
                "/liveDirector/narrative/audit/actor", "GM identity required.",
                command.Candidate.Id));
        if (command.ExpectedRevision != Current.ServerRevision ||
            command.Candidate.ServerRevision != Current.ServerRevision + 1)
            diagnostics.Add(new("LIVE_NARRATIVE_REVISION_STALE",
                "/liveDirector/narrative/serverRevision", "Revision must advance once.",
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

    public LiveDirectorNarrativeResult Undo(string actor, NetworkRole role) =>
        Move(_undo, _redo, actor, role, "undo");

    public LiveDirectorNarrativeResult Redo(string actor, NetworkRole role) =>
        Move(_redo, _undo, actor, role, "redo");

    private LiveDirectorNarrativeResult Move(Stack<LiveDirectorNarrativeProfile> from,
        Stack<LiveDirectorNarrativeProfile> to, string actor, NetworkRole role, string action)
    {
        if (from.Count == 0 || string.IsNullOrWhiteSpace(actor) ||
            role is not (NetworkRole.GameMaster or NetworkRole.Administrator))
            return Result(false, false, Current,
                [new("LIVE_NARRATIVE_HISTORY_DENIED", "/liveDirector/narrative/history",
                    "History unavailable.", Current.Id)], "no changes", "rejected", actor);
        var before = Current;
        to.Push(before);
        Current = from.Pop();
        return Result(true, true, Current, [], Diff(before, Current),
            $"compensated:{action}", actor);
    }

    private static string Diff(LiveDirectorNarrativeProfile before,
        LiveDirectorNarrativeProfile after)
    {
        var values = new[]
        {
            before.QuestInspection != after.QuestInspection ||
            !Same(before.QuestScopes, after.QuestScopes) ||
            before.ConsequencePreview != after.ConsequencePreview ||
            before.QuestRuntimeAdapter != after.QuestRuntimeAdapter ? "quests" : null,
            !Same(before.EncounterActions, after.EncounterActions) ||
            before.BoundedScaling != after.BoundedScaling ||
            before.CreatureSpawnAdapter != after.CreatureSpawnAdapter ||
            before.AiRuntimeAdapter != after.AiRuntimeAdapter ? "encounters" : null,
            before.RequiredAtRuntime != after.RequiredAtRuntime ? "runtime" : null
        }.Where(value => value is not null).ToArray();
        return values.Length == 0 ? "revision-only" : string.Join(',', values);
    }

    private static bool Same(IReadOnlyList<string>? left, IReadOnlyList<string>? right) =>
        left is not null && right is not null && left.SequenceEqual(right);

    private static LiveDirectorNarrativeResult Result(bool succeeded, bool applied,
        LiveDirectorNarrativeProfile profile, IReadOnlyList<NetworkDiagnostic> diagnostics,
        string diff, string action, string actor) => new(succeeded, applied, profile,
        diagnostics, diff,
        $"{action}:live.narrative:actor={actor}:revision={profile.ServerRevision}");
}

public sealed class LiveDirectorNarrativeStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string Serialize(LiveDirectorNarrativeProfile profile)
    {
        var diagnostics = LiveDirectorNarrativeValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return JsonSerializer.Serialize(profile, Options)
                   .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public LiveDirectorNarrativeProfile Deserialize(string json)
    {
        var profile = JsonSerializer.Deserialize<LiveDirectorNarrativeProfile>(json, Options)
                      ?? throw new InvalidDataException("LIVE_NARRATIVE_EMPTY");
        var diagnostics = LiveDirectorNarrativeValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        return profile;
    }
}

public sealed record LiveDirectorNarrativeResolution(bool Available,
    LiveDirectorNarrativeProfile? Profile, IReadOnlyList<NetworkDiagnostic> Diagnostics);

public sealed record LiveDirectorNarrativeRuntime(LiveDirectorNarrativeProfile Profile,
    AuthorizationService Authorization, LiveAuditJournal Audit, LiveQuestService Quests,
    LiveEncounterService Encounters);

public static class LiveDirectorNarrativeRuntimeAdapter
{
    public static LiveDirectorNarrativeResolution Resolve(LiveDirectorNarrativeProfile? profile,
        bool required)
    {
        if (profile is null)
            return required
                ? new(false, null,
                    [new("LIVE_NARRATIVE_REQUIRED_MISSING", "/liveDirector/narrative",
                        "Required narrative profile missing.")])
                : new(false, null, []);
        var diagnostics = LiveDirectorNarrativeValidator.Validate(profile);
        return new(diagnostics.Count == 0, diagnostics.Count == 0 ? profile : null,
            diagnostics);
    }

    public static LiveDirectorNarrativeRuntime Create(LiveDirectorNarrativeProfile profile,
        Func<DateTimeOffset> utcNow)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(utcNow);
        var diagnostics = LiveDirectorNarrativeValidator.Validate(profile);
        if (diagnostics.Count != 0) throw new InvalidDataException(diagnostics[0].Code);
        var authorization = new AuthorizationService();
        var audit = new LiveAuditJournal(utcNow);
        return new(profile, authorization, audit,
            new LiveQuestService(authorization, audit),
            new LiveEncounterService(authorization, audit));
    }
}

public static class LiveDirectorNarrativeLocalization
{
    public static string French(string? code) => code switch
    {
        "LIVE_QUEST_PERMISSION_DENIED" => "Le rôle courant ne peut pas modifier cette quête.",
        "LIVE_QUEST_PLAYER_SCOPE_INVALID" => "Choisissez tous les joueurs ou une sélection non vide.",
        "LIVE_QUEST_TRANSITION_INVALID" => "Cette transition de quête n’est pas autorisée.",
        "LIVE_ENCOUNTER_PERMISSION_DENIED" => "Le rôle courant ne peut pas commander cette rencontre.",
        "LIVE_ENCOUNTER_ACTION_INVALID" => "L’action ou sa valeur est invalide.",
        "LIVE_ENCOUNTER_SCALE_INVALID" => "Le facteur doit rester compris entre 0,25 et 4.",
        "LIVE_NARRATIVE_PERMISSION_DENIED" => "La permission Maître du jeu est requise.",
        "LIVE_NARRATIVE_REVISION_STALE" => "La source a changé ; prévisualisez de nouveau.",
        null or "" => "Commande narrative Live Director invalide.",
        _ => "Action refusée par l’autorité narrative Live Director."
    };
}

public readonly record struct LiveDirectorNarrativeHandle(Guid ProfileId, int Generation);
public sealed record LiveDirectorNarrativeSnapshot(Guid Id, long Revision, int QuestScopes,
    int EncounterActions, bool SpawnAdapter, bool AiAdapter);

public sealed class LiveDirectorNarrativeScriptService(LiveDirectorNarrativeProfile profile)
{
    public LiveDirectorNarrativeHandle Handle { get; } = new(profile.Id, 1);

    public LiveDirectorNarrativeSnapshot Read(LiveDirectorNarrativeHandle handle)
    {
        if (handle.ProfileId != profile.Id || handle.Generation != 1)
            throw new InvalidOperationException("LIVE_NARRATIVE_HANDLE_INVALID");
        return new(profile.Id, profile.ServerRevision, profile.QuestScopes.Count,
            profile.EncounterActions.Count, profile.CreatureSpawnAdapter,
            profile.AiRuntimeAdapter);
    }
}
