using Forge.Network.Operations;

namespace Forge.LiveDirector.World;

public sealed class LiveWorldActionService(LiveWorldChooser chooser, AuthorizationService authorization)
{
    public sealed record Request(Guid CommandId, string Kind, Guid TargetId, Guid? DestinationId, long ExpectedRevision, IReadOnlyDictionary<string, string> Arguments);
    public sealed record Result(bool Applied, string Code, long Revision, LiveWorldChooser.Entry? Target, string SemanticDiff, string AuditEntry);

    private readonly HashSet<Guid> _processedCommands = [];
    private long _revision;

    public Result Execute(AuthorizationSubject actor, Request request, bool apply)
    {
        if (request.CommandId == Guid.Empty) return Reject("LIVE_ACTION_COMMAND_INVALID", request);
        if (request.ExpectedRevision != _revision) return Reject("LIVE_ACTION_REVISION_STALE", request);
        if (_processedCommands.Contains(request.CommandId)) return Reject("LIVE_ACTION_COMMAND_REPLAYED", request);
        var target = chooser.Find(request.TargetId);
        if (target is null) return Reject("LIVE_ACTION_TARGET_UNKNOWN", request);
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld, target.ZoneId).Allowed)
            return Reject("LIVE_ACTION_PERMISSION_DENIED", request);

        var validation = Validate(request, target);
        if (validation is not null) return Reject(validation, request);
        var diff = Describe(request, target);
        if (!apply) return new(false, "LIVE_ACTION_PREVIEW", _revision, target, diff, $"previewed:{diff}:actor={actor.AccountId}");

        var updated = Apply(request, target);
        if (request.Kind == "world.destroy") chooser.Remove(target.ObjectId);
        else chooser.Upsert(updated);
        _processedCommands.Add(request.CommandId);
        _revision++;
        return new(true, "LIVE_ACTION_APPLIED", _revision, request.Kind == "world.destroy" ? null : updated, diff,
            $"applied:{diff}:actor={actor.AccountId}:revision={_revision}");
    }

    private string? Validate(Request request, LiveWorldChooser.Entry target)
    {
        return request.Kind switch
        {
            "director.focus" when target.Private && request.Arguments.GetValueOrDefault("privacyOverride") != "approved" => "LIVE_FOCUS_PRIVACY_DENIED",
            "director.focus" => null,
            "world.relocate" when request.DestinationId is null || chooser.Find(request.DestinationId.Value) is null => "LIVE_RELOCATE_DESTINATION_UNKNOWN",
            "world.relocate" when request.Arguments.GetValueOrDefault("navigationSafe") != "true" => "LIVE_RELOCATE_NAVIGATION_UNSAFE",
            "world.relocate" when target.PlayerAccountId is not null && request.Arguments.GetValueOrDefault("consent") != "granted" => "LIVE_RELOCATE_CONSENT_REQUIRED",
            "world.relocate" => null,
            "world.destroy" when string.IsNullOrWhiteSpace(request.Arguments.GetValueOrDefault("cause")) => "LIVE_DESTROY_CAUSE_REQUIRED",
            "world.destroy" when request.Arguments.GetValueOrDefault("confirmed") != "true" => "LIVE_DESTROY_CONFIRMATION_REQUIRED",
            "world.destroy" when request.Arguments.GetValueOrDefault("lootPolicy") is not ("preserve" or "discard") => "LIVE_DESTROY_LOOT_POLICY_INVALID",
            "world.destroy" => null,
            "world.heal" when request.Arguments.GetValueOrDefault("scope") is not ("health" or "resources" or "effects" or "all") => "LIVE_HEAL_SCOPE_INVALID",
            "world.heal" when string.IsNullOrWhiteSpace(request.Arguments.GetValueOrDefault("reason")) => "LIVE_HEAL_REASON_REQUIRED",
            "world.heal" => null,
            "world.freeze" when request.Arguments.GetValueOrDefault("ai") is not ("suspended" or "active") => "LIVE_FREEZE_AI_INVALID",
            "world.freeze" => null,
            _ => "LIVE_ACTION_KIND_UNKNOWN"
        };
    }

    private LiveWorldChooser.Entry Apply(Request request, LiveWorldChooser.Entry target)
    {
        if (request.Kind == "world.relocate")
        {
            var destination = chooser.Find(request.DestinationId!.Value)!;
            return target with { ZoneId = destination.ZoneId, InstanceId = destination.InstanceId, X = destination.X, Y = destination.Y, Z = destination.Z, State = "relocated" };
        }
        return request.Kind switch
        {
            "world.heal" => target with { State = $"restored:{request.Arguments["scope"]}" },
            "world.freeze" => target with { State = $"ai:{request.Arguments["ai"]};movement:{request.Arguments.GetValueOrDefault("movement", "preserved")};collision:{request.Arguments.GetValueOrDefault("collision", "preserved")};visibility:{request.Arguments.GetValueOrDefault("visibility", "preserved")}" },
            _ => target
        };
    }

    private static string Describe(Request request, LiveWorldChooser.Entry target) =>
        $"{request.Kind}:target={target.ObjectId}:destination={request.DestinationId}:arguments={string.Join(',', request.Arguments.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"))}";

    private Result Reject(string code, Request request) => new(false, code, _revision, chooser.Find(request.TargetId), "no changes", $"rejected:{code}:command={request.CommandId}");
}
