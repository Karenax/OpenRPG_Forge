using Forge.LiveDirector.World;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Control;

public sealed class LiveDirectorControlService(LiveWorldChooser chooser, AuthorizationService authorization, Func<DateTimeOffset> utcNow)
{
    public sealed record Metadata(IReadOnlyDictionary<string, string> Components, IReadOnlyDictionary<string, decimal> Stats, IReadOnlyDictionary<string, string> Variables, IReadOnlyList<string> Scripts, Guid? OwnerAccountId, IReadOnlyList<string> History);
    public sealed record Request(Guid CommandId, string Kind, Guid TargetId, long ExpectedRevision, TimeSpan Duration, IReadOnlyDictionary<string, string> Arguments);
    public sealed record Result(bool Applied, string Code, long Revision, LiveWorldChooser.Entry? Target, Metadata? Inspection, DateTimeOffset? ExpiresAt, string SemanticDiff, string AuditEntry);

    private readonly Dictionary<Guid, Metadata> _metadata = [];
    private readonly Dictionary<Guid, (Guid TargetId, DateTimeOffset ExpiresAt, string ReturnPoint)> _possessions = [];
    private readonly HashSet<Guid> _processedCommands = [];
    private long _revision;

    public void SetMetadata(Guid objectId, Metadata metadata)
    {
        if (objectId == Guid.Empty) throw new ArgumentException("LIVE_INSPECTION_ID_INVALID", nameof(objectId));
        _metadata[objectId] = metadata;
    }

    public Result Execute(AuthorizationSubject actor, Request request, bool apply)
    {
        if (request.CommandId == Guid.Empty || request.ExpectedRevision != _revision)
            return Reject(request.ExpectedRevision != _revision ? "LIVE_CONTROL_REVISION_STALE" : "LIVE_CONTROL_COMMAND_INVALID", request);
        if (_processedCommands.Contains(request.CommandId)) return Reject("LIVE_CONTROL_COMMAND_REPLAYED", request);
        var target = chooser.Find(request.TargetId);
        if (target is null) return Reject("LIVE_CONTROL_TARGET_UNKNOWN", request);
        var capability = request.Kind == "director.inspect" ? NetworkCapability.ViewOperations : NetworkCapability.ManageWorld;
        if (!authorization.Authorize(actor, capability, target.ZoneId).Allowed) return Reject("LIVE_CONTROL_PERMISSION_DENIED", request);

        var validation = Validate(actor, request, target);
        if (validation is not null) return Reject(validation, request);
        var inspection = request.Kind == "director.inspect" ? _metadata.GetValueOrDefault(target.ObjectId) ?? EmptyMetadata(target) : null;
        DateTimeOffset? expiresAt = request.Kind is "director.possess" or "director.protect" ? utcNow().Add(request.Duration) : null;
        var diff = $"{request.Kind}:target={target.ObjectId}:duration={request.Duration}:arguments={string.Join(',', request.Arguments.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"))}";
        if (!apply || request.Kind == "director.inspect")
            return new(false, request.Kind == "director.inspect" ? "LIVE_INSPECTION_READY" : "LIVE_CONTROL_PREVIEW", _revision, target, inspection, expiresAt, diff, $"observed:{diff}:actor={actor.AccountId}");

        var updated = Apply(actor, request, target, expiresAt);
        chooser.Upsert(updated);
        _processedCommands.Add(request.CommandId);
        _revision++;
        return new(true, "LIVE_CONTROL_APPLIED", _revision, updated, null, expiresAt, diff, $"applied:{diff}:actor={actor.AccountId}:revision={_revision}");
    }

    public string ReleaseExpiredPossession(Guid actorAccountId)
    {
        if (!_possessions.TryGetValue(actorAccountId, out var possession)) return "LIVE_POSSESSION_NONE";
        if (possession.ExpiresAt > utcNow()) return "LIVE_POSSESSION_ACTIVE";
        _possessions.Remove(actorAccountId);
        return $"LIVE_POSSESSION_RETURNED:{possession.ReturnPoint}";
    }

    private string? Validate(AuthorizationSubject actor, Request request, LiveWorldChooser.Entry target) => request.Kind switch
    {
        "director.inspect" => null,
        "director.possess" when request.Duration <= TimeSpan.Zero || request.Duration > TimeSpan.FromHours(2) => "LIVE_POSSESSION_DURATION_INVALID",
        "director.possess" when target.PlayerAccountId is not null && request.Arguments.GetValueOrDefault("protectedPlayerConsent") != "granted" => "LIVE_POSSESSION_PLAYER_PROTECTED",
        "director.possess" when string.IsNullOrWhiteSpace(request.Arguments.GetValueOrDefault("returnPoint")) => "LIVE_POSSESSION_RETURN_POINT_REQUIRED",
        "director.possess" when _possessions.ContainsKey(actor.AccountId) => "LIVE_POSSESSION_ALREADY_ACTIVE",
        "director.possess" => null,
        "director.impersonate" when request.Arguments.GetValueOrDefault("mode") is not ("chat" or "dialogue" or "voice" or "animation") => "LIVE_IMPERSONATION_MODE_INVALID",
        "director.impersonate" when request.Arguments.GetValueOrDefault("disclosure") is not ("visible" or "hidden-by-policy") => "LIVE_IMPERSONATION_DISCLOSURE_REQUIRED",
        "director.impersonate" => null,
        "director.protect" when request.Duration <= TimeSpan.Zero || request.Duration > TimeSpan.FromHours(24) => "LIVE_PROTECTION_DURATION_INVALID",
        "director.protect" when request.Arguments.GetValueOrDefault("scope") is not ("damage" or "effects" or "all") => "LIVE_PROTECTION_SCOPE_INVALID",
        "director.protect" when string.IsNullOrWhiteSpace(request.Arguments.GetValueOrDefault("indicator")) => "LIVE_PROTECTION_INDICATOR_REQUIRED",
        "director.protect" => null,
        _ => "LIVE_CONTROL_KIND_UNKNOWN"
    };

    private LiveWorldChooser.Entry Apply(AuthorizationSubject actor, Request request, LiveWorldChooser.Entry target, DateTimeOffset? expiresAt)
    {
        if (request.Kind == "director.possess")
        {
            _possessions[actor.AccountId] = (target.ObjectId, expiresAt!.Value, request.Arguments["returnPoint"]);
            return target with { State = $"possessed-by:{actor.AccountId}:until:{expiresAt:O}" };
        }
        return request.Kind switch
        {
            "director.impersonate" => target with { State = $"impersonated:{request.Arguments["mode"]}:disclosure:{request.Arguments["disclosure"]}" },
            "director.protect" => target with { State = $"protected:{request.Arguments["scope"]}:indicator:{request.Arguments["indicator"]}:until:{expiresAt:O}" },
            _ => target
        };
    }

    private static Metadata EmptyMetadata(LiveWorldChooser.Entry target) => new(
        new Dictionary<string, string> { ["kind"] = target.Kind, ["state"] = target.State },
        new Dictionary<string, decimal>(), new Dictionary<string, string>(), [], target.PlayerAccountId, []);

    private Result Reject(string code, Request request) => new(false, code, _revision, chooser.Find(request.TargetId), null, null, "no changes", $"rejected:{code}:command={request.CommandId}");
}
