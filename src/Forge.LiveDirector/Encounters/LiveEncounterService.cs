using Forge.LiveDirector.Audit;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Encounters;

public sealed class LiveEncounterService(AuthorizationService authorization,
    LiveAuditJournal audit)
{
    public sealed record Encounter(Guid EncounterId, string Name, string State, int Wave,
        decimal Scale, int ActiveCreatures);
    public sealed record Request(Guid CommandId, Guid EncounterId, string Action, string? Value,
        string Reason, long ExpectedRevision);
    public sealed record Result(bool Applied, string Code, long Revision, Encounter? Before,
        Encounter? After, string Diff, IReadOnlyList<string> RuntimeAdapters, bool Restored,
        string AuditEntry);

    private readonly Dictionary<Guid, Encounter> _encounters = [];
    private readonly Stack<Encounter> _history = [];
    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public int HistoryCount => _history.Count;

    public void Upsert(Encounter encounter)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        if (encounter.EncounterId == Guid.Empty || string.IsNullOrWhiteSpace(encounter.Name) ||
            string.IsNullOrWhiteSpace(encounter.State) || encounter.Wave < 0 ||
            encounter.Scale is < 0.25m or > 4m || encounter.ActiveCreatures < 0)
            throw new ArgumentException("LIVE_ENCOUNTER_INVALID", nameof(encounter));
        _encounters[encounter.EncounterId] = encounter;
    }

    public Encounter? Inspect(Guid encounterId) => _encounters.GetValueOrDefault(encounterId);

    public Result Execute(AuthorizationSubject actor, Request request, bool apply)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(request);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld).Allowed)
            return Reject("LIVE_ENCOUNTER_PERMISSION_DENIED", request.CommandId);
        if (!_encounters.TryGetValue(request.EncounterId, out var before))
            return Reject("LIVE_ENCOUNTER_UNKNOWN", request.CommandId);
        if (request.CommandId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason))
            return Reject("LIVE_ENCOUNTER_COMMAND_INVALID", request.CommandId);
        if (request.ExpectedRevision != _revision)
            return Reject("LIVE_ENCOUNTER_REVISION_STALE", request.CommandId);
        if (_processed.Contains(request.CommandId))
            return Reject("LIVE_ENCOUNTER_COMMAND_REPLAYED", request.CommandId);
        var action = request.Action == "alter-wave" ? "wave" : request.Action;
        var after = Transform(before, action, request.Value);
        if (after is null)
            return Reject(action == "scale" ? "LIVE_ENCOUNTER_SCALE_INVALID" :
                "LIVE_ENCOUNTER_ACTION_INVALID", request.CommandId);
        var adapters = new[] { "spawn-authority", "ai-runtime" };
        var diff = Describe(action, before, after);
        if (action == "inspect")
            return new(false, "LIVE_ENCOUNTER_INSPECTED", _revision, before, after, diff,
                adapters, false,
                $"inspected:encounter={before.EncounterId}:actor={actor.AccountId}");
        if (!apply)
            return new(false, "LIVE_ENCOUNTER_PREVIEW", _revision, before, after, diff,
                adapters, false,
                $"previewed:encounter.{action}={before.EncounterId}:actor={actor.AccountId}");
        _history.Push(before);
        _encounters[before.EncounterId] = after;
        _processed.Add(request.CommandId);
        _revision++;
        var entry = audit.Append(actor.AccountId, $"encounter.{action}", [before.EncounterId],
            before.ToString(), after.ToString(), request.Reason, request.CommandId);
        return new(true, "LIVE_ENCOUNTER_APPLIED", _revision, before, after, diff, adapters,
            false, entry.Hash);
    }

    public Result Rollback(AuthorizationSubject actor, Guid commandId, string reason,
        long expectedRevision, bool apply)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld).Allowed)
            return Reject("LIVE_ENCOUNTER_PERMISSION_DENIED", commandId);
        if (_history.Count == 0) return Reject("LIVE_ENCOUNTER_ROLLBACK_EMPTY", commandId);
        if (commandId == Guid.Empty || string.IsNullOrWhiteSpace(reason))
            return Reject("LIVE_ENCOUNTER_COMMAND_INVALID", commandId);
        if (expectedRevision != _revision)
            return Reject("LIVE_ENCOUNTER_REVISION_STALE", commandId);
        if (_processed.Contains(commandId))
            return Reject("LIVE_ENCOUNTER_COMMAND_REPLAYED", commandId);
        var before = _encounters[_history.Peek().EncounterId];
        var after = _history.Peek();
        var adapters = new[] { "spawn-authority", "ai-runtime" };
        var diff = Describe("rollback", before, after);
        if (!apply)
            return new(false, "LIVE_ENCOUNTER_ROLLBACK_PREVIEW", _revision, before, after,
                diff, adapters, false,
                $"previewed:encounter.rollback={before.EncounterId}:actor={actor.AccountId}");
        _history.Pop();
        _encounters[after.EncounterId] = after;
        _processed.Add(commandId);
        _revision++;
        var entry = audit.Append(actor.AccountId, "encounter.rollback", [after.EncounterId],
            before.ToString(), after.ToString(), reason, commandId);
        return new(true, "LIVE_ENCOUNTER_ROLLED_BACK", _revision, before, after, diff,
            adapters, true, entry.Hash);
    }

    private static Encounter? Transform(Encounter value, string action, string? raw) => action switch
    {
        "inspect" => value,
        "activate" => value with { State = "active" },
        "pause" => value with { State = "paused" },
        "wave" when int.TryParse(raw, out var wave) && wave >= 0 => value with { Wave = wave },
        "scale" when decimal.TryParse(raw, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var scale) &&
            scale is >= 0.25m and <= 4m => value with { Scale = scale },
        "despawn" => value with { State = "despawned", ActiveCreatures = 0 },
        _ => null
    };

    private static string Describe(string action, Encounter before, Encounter after) =>
        $"encounter.{action}:id={before.EncounterId}:state={before.State}->{after.State}:" +
        $"wave={before.Wave}->{after.Wave}:scale={before.Scale}->{after.Scale}:" +
        $"active={before.ActiveCreatures}->{after.ActiveCreatures}:" +
        "adapters=spawn-authority,ai-runtime";

    private Result Reject(string code, Guid commandId) => new(false, code, _revision, null, null,
        "no changes", [], false, $"rejected:{code}:command={commandId}");
}
