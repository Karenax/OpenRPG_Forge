using Forge.LiveDirector.World;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Control;

public sealed class LiveDirectorControlService(LiveWorldChooser chooser, AuthorizationService authorization,
    Func<DateTimeOffset> utcNow, TimeSpan? maximumPossessionDuration = null,
    TimeSpan? maximumProtectionDuration = null)
{
    public sealed record Metadata(IReadOnlyDictionary<string, string> Components, IReadOnlyDictionary<string, decimal> Stats, IReadOnlyDictionary<string, string> Variables, IReadOnlyList<string> Scripts, Guid? OwnerAccountId, IReadOnlyList<string> History);
    public sealed record Request(Guid CommandId, string Kind, Guid TargetId, long ExpectedRevision, TimeSpan Duration, IReadOnlyDictionary<string, string> Arguments);
    public sealed record Result(bool Applied, string Code, long Revision, LiveWorldChooser.Entry? Target, Metadata? Inspection, DateTimeOffset? ExpiresAt, string SemanticDiff, string AuditEntry);

    private readonly Dictionary<Guid, Metadata> _metadata = [];
    private readonly Dictionary<Guid, Possession> _possessions = [];
    private readonly Dictionary<Guid, LiveWorldChooser.Entry> _impersonations = [];
    private readonly Dictionary<Guid, Protection> _protections = [];
    private readonly HashSet<Guid> _processedCommands = [];
    private readonly TimeSpan _maximumPossessionDuration = maximumPossessionDuration ?? TimeSpan.FromHours(2);
    private readonly TimeSpan _maximumProtectionDuration = maximumProtectionDuration ?? TimeSpan.FromHours(24);
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
        chooser.Upsert(possession.Original);
        _revision++;
        return $"LIVE_POSSESSION_RETURNED:{possession.ReturnPoint}";
    }

    public Result ReleasePossession(AuthorizationSubject actor, bool apply)
    {
        if (!_possessions.TryGetValue(actor.AccountId, out var possession))
            return Reject("LIVE_POSSESSION_NONE", new(Guid.Empty, "director.possess",
                Guid.Empty, _revision, TimeSpan.Zero, new Dictionary<string, string>()));
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld,
                possession.Original.ZoneId).Allowed)
            return Reject("LIVE_CONTROL_PERMISSION_DENIED", new(Guid.Empty, "director.possess",
                possession.TargetId, _revision, TimeSpan.Zero, new Dictionary<string, string>()));
        var diff = $"director.release:target={possession.TargetId}:returnPoint={possession.ReturnPoint}";
        if (!apply)
            return new(false, "LIVE_POSSESSION_RELEASE_PREVIEW", _revision,
                chooser.Find(possession.TargetId), null, possession.ExpiresAt, diff,
                $"previewed:{diff}:actor={actor.AccountId}");
        chooser.Upsert(possession.Original);
        _possessions.Remove(actor.AccountId);
        _revision++;
        return new(true, "LIVE_POSSESSION_RETURNED", _revision, possession.Original, null,
            possession.ExpiresAt, diff,
            $"compensated:{diff}:actor={actor.AccountId}:revision={_revision}");
    }

    public Result RestoreTarget(AuthorizationSubject actor, Guid targetId, string kind, bool apply)
    {
        LiveWorldChooser.Entry? original = kind switch
        {
            "director.impersonate" => _impersonations.GetValueOrDefault(targetId),
            "director.protect" => _protections.GetValueOrDefault(targetId)?.Original,
            _ => null
        };
        if (original is null)
            return new(false, "LIVE_CONTROL_RESTORE_UNKNOWN", _revision,
                chooser.Find(targetId), null, null, "no changes",
                $"rejected:LIVE_CONTROL_RESTORE_UNKNOWN:target={targetId}");
        if (!authorization.Authorize(actor, NetworkCapability.ManageWorld,
                original.ZoneId).Allowed)
            return new(false, "LIVE_CONTROL_PERMISSION_DENIED", _revision,
                chooser.Find(targetId), null, null, "no changes",
                $"rejected:LIVE_CONTROL_PERMISSION_DENIED:target={targetId}");
        var diff = $"restore:{kind}:target={targetId}:state={original.State}";
        if (!apply)
            return new(false, "LIVE_CONTROL_RESTORE_PREVIEW", _revision,
                chooser.Find(targetId), null, null, diff,
                $"previewed:{diff}:actor={actor.AccountId}");
        chooser.Upsert(original);
        _impersonations.Remove(targetId);
        _protections.Remove(targetId);
        _revision++;
        return new(true, "LIVE_CONTROL_RESTORED", _revision, original, null, null, diff,
            $"compensated:{diff}:actor={actor.AccountId}:revision={_revision}");
    }

    public string ReleaseExpiredProtection(Guid targetId)
    {
        if (!_protections.TryGetValue(targetId, out var protection))
            return "LIVE_PROTECTION_NONE";
        if (protection.ExpiresAt > utcNow()) return "LIVE_PROTECTION_ACTIVE";
        _protections.Remove(targetId);
        chooser.Upsert(protection.Original);
        _revision++;
        return $"LIVE_PROTECTION_RETURNED:{targetId}";
    }

    private string? Validate(AuthorizationSubject actor, Request request, LiveWorldChooser.Entry target) => request.Kind switch
    {
        "director.inspect" => null,
        "director.possess" when request.Duration <= TimeSpan.Zero || request.Duration > _maximumPossessionDuration => "LIVE_POSSESSION_DURATION_INVALID",
        "director.possess" when target.PlayerAccountId is not null && request.Arguments.GetValueOrDefault("protectedPlayerConsent") != "granted" => "LIVE_POSSESSION_PLAYER_PROTECTED",
        "director.possess" when string.IsNullOrWhiteSpace(request.Arguments.GetValueOrDefault("returnPoint")) => "LIVE_POSSESSION_RETURN_POINT_REQUIRED",
        "director.possess" when request.Arguments.GetValueOrDefault("inputContext") is not ("gm" or "director" or "spectator") => "LIVE_POSSESSION_INPUT_CONTEXT_REQUIRED",
        "director.possess" when _possessions.ContainsKey(actor.AccountId) => "LIVE_POSSESSION_ALREADY_ACTIVE",
        "director.possess" => null,
        "director.impersonate" when request.Arguments.GetValueOrDefault("mode") is not ("chat" or "dialogue" or "voice" or "animation") => "LIVE_IMPERSONATION_MODE_INVALID",
        "director.impersonate" when request.Arguments.GetValueOrDefault("disclosure") is not ("visible" or "hidden-by-policy") => "LIVE_IMPERSONATION_DISCLOSURE_REQUIRED",
        "director.impersonate" when request.Arguments.TryGetValue("renderAdapter", out var renderer) &&
                                    renderer != RenderAdapter(request.Arguments["mode"]) =>
            "LIVE_IMPERSONATION_RENDER_ADAPTER_INVALID",
        "director.impersonate" => null,
        "director.protect" when request.Duration <= TimeSpan.Zero || request.Duration > _maximumProtectionDuration => "LIVE_PROTECTION_DURATION_INVALID",
        "director.protect" when request.Arguments.GetValueOrDefault("scope") is not ("damage" or "effects" or "all") => "LIVE_PROTECTION_SCOPE_INVALID",
        "director.protect" when string.IsNullOrWhiteSpace(request.Arguments.GetValueOrDefault("indicator")) => "LIVE_PROTECTION_INDICATOR_REQUIRED",
        "director.protect" when request.Arguments.TryGetValue("damageAdapter", out var adapter) &&
                                adapter is not ("runtime" or "effects") =>
            "LIVE_PROTECTION_DAMAGE_ADAPTER_INVALID",
        "director.protect" => null,
        _ => "LIVE_CONTROL_KIND_UNKNOWN"
    };

    private LiveWorldChooser.Entry Apply(AuthorizationSubject actor, Request request, LiveWorldChooser.Entry target, DateTimeOffset? expiresAt)
    {
        if (request.Kind == "director.possess")
        {
            _possessions[actor.AccountId] = new(target.ObjectId, expiresAt!.Value,
                request.Arguments["returnPoint"], target);
            return target with { State = $"possessed-by:{actor.AccountId}:until:{expiresAt:O}" };
        }
        if (request.Kind == "director.impersonate")
        {
            _impersonations[target.ObjectId] = target;
            return target with
            {
                State = $"impersonated:{request.Arguments["mode"]}:disclosure:{request.Arguments["disclosure"]}:renderer:{request.Arguments.GetValueOrDefault("renderAdapter", RenderAdapter(request.Arguments["mode"]))}"
            };
        }
        if (request.Kind == "director.protect")
        {
            _protections[target.ObjectId] = new(target, expiresAt!.Value);
            return target with
            {
                State = $"protected:{request.Arguments["scope"]}:indicator:{request.Arguments["indicator"]}:damage-adapter:{request.Arguments.GetValueOrDefault("damageAdapter", "runtime")}:until:{expiresAt:O}"
            };
        }
        return request.Kind switch
        {
            _ => target
        };
    }

    private static Metadata EmptyMetadata(LiveWorldChooser.Entry target) => new(
        new Dictionary<string, string> { ["kind"] = target.Kind, ["state"] = target.State },
        new Dictionary<string, decimal>(), new Dictionary<string, string>(), [], target.PlayerAccountId, []);

    private Result Reject(string code, Request request) => new(false, code, _revision, chooser.Find(request.TargetId), null, null, "no changes", $"rejected:{code}:command={request.CommandId}");

    private sealed record Possession(Guid TargetId, DateTimeOffset ExpiresAt,
        string ReturnPoint, LiveWorldChooser.Entry Original);

    private sealed record Protection(LiveWorldChooser.Entry Original, DateTimeOffset ExpiresAt);

    private static string RenderAdapter(string mode) => mode switch
    {
        "chat" => "text",
        "dialogue" => "cinematic",
        "voice" => "audio",
        "animation" => "animation",
        _ => "unknown"
    };
}
