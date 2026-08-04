using Forge.Network.Operations;

namespace Forge.LiveDirector.World;

public sealed class LiveProgressionWorldService(LiveWorldChooser chooser, AuthorizationService authorization, Func<long, int> levelForExperience)
{
    public sealed record Request(Guid CommandId, string Kind, Guid TargetId, long ExpectedRevision, IReadOnlyDictionary<string, string> Arguments);
    public sealed record Result(bool Applied, string Code, long Revision, string SemanticDiff, IReadOnlyList<string> ScriptTrace, string AuditEntry);

    private readonly Dictionary<Guid, long> _experience = [];
    private readonly HashSet<Guid> _processed = [];
    private long _revision;

    public Result Execute(AuthorizationSubject actor, Request request, bool apply)
    {
        var target = chooser.Find(request.TargetId);
        if (target is null) return Reject("LIVE_RULE_TARGET_UNKNOWN", request);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld, target.ZoneId).Allowed) return Reject("LIVE_RULE_PERMISSION_DENIED", request);
        if (request.CommandId == Guid.Empty || request.ExpectedRevision != _revision) return Reject(request.CommandId == Guid.Empty ? "LIVE_RULE_COMMAND_INVALID" : "LIVE_RULE_REVISION_STALE", request);
        if (_processed.Contains(request.CommandId)) return Reject("LIVE_RULE_COMMAND_REPLAYED", request);
        var validation = Validate(request, target);
        if (validation is not null) return Reject(validation, request);
        var (diff, trace) = Preview(request, target);
        if (!apply) return new(false, "LIVE_RULE_PREVIEW", _revision, diff, trace, $"previewed:{diff}:actor={actor.AccountId}");
        Apply(request, target);
        _processed.Add(request.CommandId);
        _revision++;
        return new(true, "LIVE_RULE_APPLIED", _revision, diff, trace, $"applied:{diff}:actor={actor.AccountId}:revision={_revision}");
    }

    private string? Validate(Request request, LiveWorldChooser.Entry target) => request.Kind switch
    {
        "progression.adjust" when !long.TryParse(request.Arguments.GetValueOrDefault("amount"), out var amount) || amount == 0 || Math.Abs((decimal)amount) > 1_000_000 => "LIVE_XP_AMOUNT_INVALID",
        "progression.adjust" when string.IsNullOrWhiteSpace(request.Arguments.GetValueOrDefault("reason")) => "LIVE_XP_REASON_REQUIRED",
        "progression.adjust" => null,
        "door.set-state" when target.Kind != "door" => "LIVE_DOOR_TARGET_INVALID",
        "door.set-state" when request.Arguments.GetValueOrDefault("state") is not ("locked" or "unlocked" or "open" or "repaired" or "relinked") => "LIVE_DOOR_STATE_INVALID",
        "door.set-state" when request.Arguments.GetValueOrDefault("state") == "relinked" && string.IsNullOrWhiteSpace(request.Arguments.GetValueOrDefault("portal")) => "LIVE_DOOR_PORTAL_REQUIRED",
        "door.set-state" => null,
        "trigger.simulate" when target.Kind != "trigger" => "LIVE_TRIGGER_TARGET_INVALID",
        "trigger.simulate" when request.Arguments.GetValueOrDefault("event") is not ("enter" or "exit") => "LIVE_TRIGGER_EVENT_INVALID",
        "trigger.simulate" when !Guid.TryParse(request.Arguments.GetValueOrDefault("actor"), out _) => "LIVE_TRIGGER_ACTOR_INVALID",
        "trigger.simulate" => null,
        _ => "LIVE_RULE_KIND_UNKNOWN"
    };

    private (string Diff, IReadOnlyList<string> Trace) Preview(Request request, LiveWorldChooser.Entry target)
    {
        if (request.Kind == "progression.adjust")
        {
            var previous = _experience.GetValueOrDefault(target.ObjectId);
            var amount = long.Parse(request.Arguments["amount"]);
            var candidate = Math.Max(0, previous + amount);
            return ($"progression.adjust:target={target.ObjectId}:xp={previous}->{candidate}:level={levelForExperience(previous)}->{levelForExperience(candidate)}:reason={request.Arguments["reason"]}", []);
        }
        if (request.Kind == "door.set-state")
            return ($"door.set-state:target={target.ObjectId}:state={request.Arguments["state"]}:portal={request.Arguments.GetValueOrDefault("portal", "unchanged")}", []);
        var trace = new[] { $"trigger:{target.ObjectId}:{request.Arguments["event"]}", $"actor:{request.Arguments["actor"]}", $"payload:{request.Arguments.GetValueOrDefault("payload", "{}")}" };
        return ($"trigger.simulate:target={target.ObjectId}:event={request.Arguments["event"]}", trace);
    }

    private void Apply(Request request, LiveWorldChooser.Entry target)
    {
        if (request.Kind == "progression.adjust")
            _experience[target.ObjectId] = Math.Max(0, _experience.GetValueOrDefault(target.ObjectId) + long.Parse(request.Arguments["amount"]));
        else if (request.Kind == "door.set-state")
            chooser.Upsert(target with { State = $"door:{request.Arguments["state"]}:portal:{request.Arguments.GetValueOrDefault("portal", "unchanged")}" });
    }

    private Result Reject(string code, Request request) => new(false, code, _revision, "no changes", [], $"rejected:{code}:command={request.CommandId}");
}
