namespace Forge.Network.Operations;

public enum ModerationActionKind { Mute, Kick, Ban }
public sealed record ModerationAction(Guid ActionId, ModerationActionKind Kind, Guid TargetAccountId, Guid ActorAccountId, string Reason, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt, bool Revoked);
public sealed record ModerationDecision(bool Applied, string Code, ModerationAction? Action, string AuditEntry);

public sealed class ModerationService(AuthorizationService authorization, TimeProvider timeProvider)
{
    private readonly Dictionary<Guid, ModerationAction> _actions = [];

    public ModerationDecision Apply(AuthorizationSubject actor, ModerationActionKind kind, Guid targetAccountId, string reason, TimeSpan? duration)
    {
        var capability = kind == ModerationActionKind.Mute ? NetworkCapability.ModerateChat : NetworkCapability.ManagePlayers;
        var auth = authorization.Authorize(actor, capability);
        if (!auth.Allowed) return Reject(auth.Code, actor.AccountId, targetAccountId);
        if (targetAccountId == Guid.Empty || targetAccountId == actor.AccountId) return Reject("NET_MODERATION_TARGET_INVALID", actor.AccountId, targetAccountId);
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500) return Reject("NET_MODERATION_REASON_INVALID", actor.AccountId, targetAccountId);
        if (duration is { } value && (value <= TimeSpan.Zero || value > TimeSpan.FromDays(365))) return Reject("NET_MODERATION_DURATION_INVALID", actor.AccountId, targetAccountId);
        var now = timeProvider.GetUtcNow();
        var action = new ModerationAction(Guid.NewGuid(), kind, targetAccountId, actor.AccountId, reason.Trim(), now, duration is null ? null : now + duration, false);
        _actions.Add(action.ActionId, action);
        return new(true, "NET_MODERATION_APPLIED", action, $"applied:moderation:{kind}:actor={actor.AccountId}:target={targetAccountId}:action={action.ActionId}");
    }

    public bool IsRestricted(Guid accountId, ModerationActionKind kind)
    {
        var now = timeProvider.GetUtcNow();
        return _actions.Values.Any(x => x.TargetAccountId == accountId && x.Kind == kind && !x.Revoked && (x.ExpiresAt is null || x.ExpiresAt > now));
    }

    public IReadOnlyList<ModerationAction> AuditFor(Guid accountId) => _actions.Values.Where(x => x.TargetAccountId == accountId).OrderBy(x => x.CreatedAt).ThenBy(x => x.ActionId).ToArray();
    private static ModerationDecision Reject(string code, Guid actor, Guid target) => new(false, code, null, $"rejected:{code}:actor={actor}:target={target}");
}
