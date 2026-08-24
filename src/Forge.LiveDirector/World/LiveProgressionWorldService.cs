using Forge.Network.Operations;

namespace Forge.LiveDirector.World;

public sealed class LiveProgressionWorldService(LiveWorldChooser chooser,
    AuthorizationService authorization, Func<long, int> levelForExperience,
    long absoluteExperienceCap = 1_000_000)
{
    public sealed record Request(Guid CommandId, string Kind, Guid TargetId, long ExpectedRevision, IReadOnlyDictionary<string, string> Arguments);
    public sealed record Result(bool Applied, string Code, long Revision, string SemanticDiff, IReadOnlyList<string> ScriptTrace, string AuditEntry);

    private readonly Dictionary<Guid, long> _experience = [];
    private readonly HashSet<Guid> _processed = [];
    private readonly Dictionary<Guid, Compensation> _compensations = [];
    private long _revision;

    public long Experience(Guid targetId) => _experience.GetValueOrDefault(targetId);

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
        var previousExperience = Experience(target.ObjectId);
        Apply(request, target);
        var updatedTarget = chooser.Find(target.ObjectId);
        _processed.Add(request.CommandId);
        _revision++;
        if (request.Kind is "progression.adjust" or "door.set-state")
            _compensations[request.CommandId] = new(request.Kind, target, updatedTarget,
                previousExperience, Experience(target.ObjectId));
        return new(true, "LIVE_RULE_APPLIED", _revision, diff, trace, $"applied:{diff}:actor={actor.AccountId}:revision={_revision}");
    }

    public Result Compensate(AuthorizationSubject actor, Guid commandId, long expectedRevision,
        bool apply)
    {
        if (!_compensations.TryGetValue(commandId, out var compensation))
            return RejectCompensation("LIVE_RULE_COMPENSATION_UNKNOWN", commandId);
        if (expectedRevision != _revision)
            return RejectCompensation("LIVE_RULE_COMPENSATION_REVISION_STALE", commandId);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld,
                compensation.Before.ZoneId).Allowed)
            return RejectCompensation("LIVE_RULE_PERMISSION_DENIED", commandId);
        var conflict = compensation.Kind == "progression.adjust"
            ? Experience(compensation.Before.ObjectId) != compensation.AppliedExperience
            : chooser.Find(compensation.Before.ObjectId) != compensation.After;
        if (conflict) return RejectCompensation("LIVE_RULE_COMPENSATION_CONFLICT", commandId);
        var diff = $"restore:{compensation.Kind}:target={compensation.Before.ObjectId}:limit=session-only";
        if (!apply)
            return new(false, "LIVE_RULE_COMPENSATION_PREVIEW", _revision, diff, [],
                $"previewed:{diff}:actor={actor.AccountId}");
        if (compensation.Kind == "progression.adjust")
            _experience[compensation.Before.ObjectId] = compensation.PreviousExperience;
        else
            chooser.Upsert(compensation.Before);
        _compensations.Remove(commandId);
        _revision++;
        return new(true, "LIVE_RULE_COMPENSATED", _revision, diff, [],
            $"compensated:{diff}:actor={actor.AccountId}:revision={_revision}");
    }

    public IReadOnlyDictionary<Guid, long> ExportExperience() =>
        new SortedDictionary<Guid, long>(_experience);

    public void ImportExperience(IReadOnlyDictionary<Guid, long> experience)
    {
        ArgumentNullException.ThrowIfNull(experience);
        if (experience.Any(item => item.Key == Guid.Empty || item.Value < 0))
            throw new InvalidDataException("LIVE_XP_SNAPSHOT_INVALID");
        _experience.Clear();
        foreach (var item in experience.OrderBy(item => item.Key))
            _experience[item.Key] = item.Value;
    }

    private string? Validate(Request request, LiveWorldChooser.Entry target) => request.Kind switch
    {
        "progression.adjust" when !long.TryParse(request.Arguments.GetValueOrDefault("amount"), out var amount) || amount == 0 || Math.Abs((decimal)amount) > absoluteExperienceCap => "LIVE_XP_AMOUNT_INVALID",
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
        {
            var state = request.Arguments["state"];
            var collision = state is "open" or "relinked" ? "passable" : "blocked";
            return ($"door.set-state:target={target.ObjectId}:state={state}:collision={collision}:portal={request.Arguments.GetValueOrDefault("portal", "unchanged")}", []);
        }
        var trace = new[] { $"trigger:{target.ObjectId}:{request.Arguments["event"]}", $"actor:{request.Arguments["actor"]}", $"payload:{request.Arguments.GetValueOrDefault("payload", "{}")}" };
        return ($"trigger.simulate:target={target.ObjectId}:event={request.Arguments["event"]}", trace);
    }

    private void Apply(Request request, LiveWorldChooser.Entry target)
    {
        if (request.Kind == "progression.adjust")
            _experience[target.ObjectId] = Math.Max(0, _experience.GetValueOrDefault(target.ObjectId) + long.Parse(request.Arguments["amount"]));
        else if (request.Kind == "door.set-state")
        {
            var state = request.Arguments["state"];
            var collision = state is "open" or "relinked" ? "passable" : "blocked";
            chooser.Upsert(target with
            {
                State = $"door:{state}:collision:{collision}:portal:{request.Arguments.GetValueOrDefault("portal", "unchanged")}"
            });
        }
    }

    private Result Reject(string code, Request request) => new(false, code, _revision, "no changes", [], $"rejected:{code}:command={request.CommandId}");

    private Result RejectCompensation(string code, Guid commandId) => new(false, code,
        _revision, "no changes", [], $"rejected:{code}:command={commandId}");

    private sealed record Compensation(string Kind, LiveWorldChooser.Entry Before,
        LiveWorldChooser.Entry? After, long PreviousExperience, long AppliedExperience);
}
