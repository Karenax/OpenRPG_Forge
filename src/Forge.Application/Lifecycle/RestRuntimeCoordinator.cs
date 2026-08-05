using Forge.Domain.Areas;
using Forge.Domain.Lifecycle;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Spatial;

namespace Forge.Application.Lifecycle;

public sealed class RestRuntimeCoordinator
{
    public const string DisabledCode = "runtime.rest.disabled";
    public const string ForbiddenCode = "runtime.rest.forbidden";
    public const string ConditionCode = "runtime.rest.condition_failed";
    public const string SecurityCode = "runtime.rest.security_failed";
    public const string CooldownCode = "runtime.rest.cooldown";
    public const string GroupCode = "runtime.rest.group_required";
    public const string SessionCode = "runtime.rest.session_invalid";

    private readonly Func<Guid> _ids;
    private readonly Dictionary<string, DateTimeOffset> _cooldowns = new(StringComparer.Ordinal);

    public RestRuntimeCoordinator(Func<Guid>? ids = null) => _ids = ids ?? Guid.NewGuid;

    public RestTransitionResult Request(string actorId, string groupId, AreaId areaId, Vector3Value position,
        RestLifecycleProfile lifecycle, AreaRestPolicy areaPolicy, bool secure,
        Func<string, bool> conditionEvaluator, long serverRevision, DateTimeOffset occurredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentNullException.ThrowIfNull(groupId);
        ArgumentNullException.ThrowIfNull(areaId);
        ArgumentNullException.ThrowIfNull(lifecycle);
        ArgumentNullException.ThrowIfNull(areaPolicy);
        ArgumentNullException.ThrowIfNull(conditionEvaluator);
        if (!lifecycle.Enabled)
        {
            return new RestTransitionResult(false, DisabledCode, [], null);
        }

        var policy = areaPolicy.ResolveAt(position);
        var requested = Event(actorId, groupId, areaId, RestLifecycleStage.Requested,
            lifecycle.DefaultDurationSeconds, "player-request", serverRevision, occurredAt);
        if (lifecycle.PartyPolicy != PartyRestPolicy.Individual && string.IsNullOrWhiteSpace(groupId))
        {
            return Denied(GroupCode, requested, actorId, groupId, areaId, lifecycle, serverRevision, occurredAt);
        }

        if (_cooldowns.TryGetValue(actorId, out var cooldownUntil) && cooldownUntil > occurredAt)
        {
            return Denied(CooldownCode, requested, actorId, groupId, areaId, lifecycle, serverRevision, occurredAt);
        }

        if (policy.Mode == RestPermissionMode.Forbidden)
        {
            return Denied(ForbiddenCode, requested, actorId, groupId, areaId, lifecycle, serverRevision, occurredAt,
                policy.DenialReason);
        }

        if (policy.Mode == RestPermissionMode.Conditional && !conditionEvaluator(policy.Condition))
        {
            return Denied(ConditionCode, requested, actorId, groupId, areaId, lifecycle, serverRevision, occurredAt,
                policy.DenialReason);
        }

        if (policy.RequiredSecureRadius > 0 && !secure)
        {
            return Denied(SecurityCode, requested, actorId, groupId, areaId, lifecycle, serverRevision, occurredAt,
                policy.DenialReason);
        }

        var started = Event(actorId, groupId, areaId, RestLifecycleStage.Started,
            lifecycle.DefaultDurationSeconds, "policy-accepted", serverRevision + 1, occurredAt.AddTicks(1));
        var session = new RestSession(_ids(), actorId, groupId.Trim(), areaId, lifecycle.PartyPolicy, occurredAt,
            occurredAt.AddSeconds(lifecycle.DefaultDurationSeconds), policy.CooldownSeconds, true);
        return new RestTransitionResult(true, null, [requested, started], session);
    }

    public RestTransitionResult Interrupt(RestSession session, string cause, long serverRevision,
        DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(cause);
        if (!session.Active)
        {
            return new RestTransitionResult(false, SessionCode, [], session);
        }

        var interrupted = Event(session.ActorId, session.GroupId, session.AreaId, RestLifecycleStage.Interrupted,
            (int)(session.CompletesAt - session.StartedAt).TotalSeconds, cause, serverRevision, occurredAt);
        return new RestTransitionResult(true, null, [interrupted], session with { Active = false });
    }

    public RestTransitionResult Complete(RestSession session, long serverRevision, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.Active || occurredAt < session.CompletesAt)
        {
            return new RestTransitionResult(false, SessionCode, [], session);
        }

        var completed = Event(session.ActorId, session.GroupId, session.AreaId, RestLifecycleStage.Completed,
            (int)(session.CompletesAt - session.StartedAt).TotalSeconds, "duration-complete", serverRevision,
            occurredAt);
        _cooldowns[session.ActorId] = occurredAt.AddSeconds(session.CooldownSeconds);
        return new RestTransitionResult(true, null, [completed], session with { Active = false });
    }

    private RestTransitionResult Denied(string code, RestLifecycleEvent requested, string actorId, string groupId,
        AreaId areaId, RestLifecycleProfile lifecycle, long serverRevision, DateTimeOffset occurredAt,
        string? authoredReason = null)
    {
        var interrupted = Event(actorId, groupId, areaId, RestLifecycleStage.Interrupted,
            lifecycle.DefaultDurationSeconds, string.IsNullOrWhiteSpace(authoredReason) ? code : authoredReason,
            serverRevision + 1, occurredAt.AddTicks(1));
        return new RestTransitionResult(false, code, [requested, interrupted], null);
    }

    private RestLifecycleEvent Event(string actorId, string groupId, AreaId areaId, RestLifecycleStage stage,
        int duration, string cause, long revision, DateTimeOffset occurredAt) =>
        new(_ids(), actorId, groupId.Trim(), areaId, stage, duration, cause, EventAuthorityScope.Server, revision,
            occurredAt);
}
