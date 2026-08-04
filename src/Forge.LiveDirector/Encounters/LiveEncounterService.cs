using Forge.LiveDirector.Audit;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Encounters;

public sealed class LiveEncounterService(AuthorizationService authorization, LiveAuditJournal audit)
{
    public sealed record Encounter(Guid EncounterId, string Name, string State, int Wave, decimal Scale, int ActiveCreatures);
    public sealed record Request(Guid CommandId, Guid EncounterId, string Action, string? Value, string Reason, long ExpectedRevision);
    public sealed record Result(bool Applied, string Code, long Revision, Encounter? Before, Encounter? After, string Diff);
    private readonly Dictionary<Guid, Encounter> _encounters = [];
    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public void Upsert(Encounter encounter) => _encounters[encounter.EncounterId] = encounter;
    public Encounter? Inspect(Guid encounterId) => _encounters.GetValueOrDefault(encounterId);

    public Result Execute(AuthorizationSubject actor, Request request, bool apply)
    {
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld).Allowed) return Reject("LIVE_ENCOUNTER_PERMISSION_DENIED", request);
        if (!_encounters.TryGetValue(request.EncounterId, out var before)) return Reject("LIVE_ENCOUNTER_UNKNOWN", request);
        if (request.ExpectedRevision != _revision) return Reject("LIVE_ENCOUNTER_REVISION_STALE", request);
        if (_processed.Contains(request.CommandId)) return Reject("LIVE_ENCOUNTER_COMMAND_REPLAYED", request);
        var after = Transform(before, request);
        if (after is null) return Reject("LIVE_ENCOUNTER_ACTION_INVALID", request);
        var diff = $"encounter.{request.Action}:id={before.EncounterId}:state={before.State}->{after.State}:wave={before.Wave}->{after.Wave}:scale={before.Scale}->{after.Scale}:active={before.ActiveCreatures}->{after.ActiveCreatures}";
        if (!apply || request.Action == "inspect") return new(false, request.Action == "inspect" ? "LIVE_ENCOUNTER_INSPECTED" : "LIVE_ENCOUNTER_PREVIEW", _revision, before, after, diff);
        _encounters[before.EncounterId] = after;
        _processed.Add(request.CommandId);
        _revision++;
        audit.Append(actor.AccountId, $"encounter.{request.Action}", [before.EncounterId], before.ToString(), after.ToString(), request.Reason, request.CommandId);
        return new(true, "LIVE_ENCOUNTER_APPLIED", _revision, before, after, diff);
    }

    private static Encounter? Transform(Encounter value, Request request) => request.Action switch
    {
        "inspect" => value,
        "activate" => value with { State = "active" },
        "pause" => value with { State = "paused" },
        "alter-wave" when int.TryParse(request.Value, out var wave) && wave >= 0 => value with { Wave = wave },
        "scale" when decimal.TryParse(request.Value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var scale) && scale is >= 0.25m and <= 4m => value with { Scale = scale },
        "despawn" => value with { State = "despawned", ActiveCreatures = 0 },
        _ => null
    };
    private Result Reject(string code, Request request) => new(false, code, _revision, null, null, "no changes");
}
