using Forge.Network.Identity;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Authentication;

public sealed class DirectorAuthenticationService(
    AccountService accounts,
    Func<DateTimeOffset> utcNow,
    Func<Guid> sessionIdFactory,
    TimeSpan sessionLifetime,
    bool requireSecondFactor = true)
{
    public sealed record Request(string Login, string Password, string? SecondFactor);
    public sealed record Session(bool Authenticated, string Code, Guid? AccountId, Guid? SessionId, DateTimeOffset? ExpiresAt, AuthorizationSubject? Subject, string AuditEntry);

    private readonly Dictionary<Guid, AuthorizationSubject> _subjects = [];
    private readonly HashSet<Guid> _revokedSessions = [];

    public void GrantRole(Guid accountId, NetworkRole role,
        IReadOnlySet<Guid>? scopedZoneIds = null)
    {
        if (accountId == Guid.Empty || role is not (NetworkRole.GameMaster or NetworkRole.Administrator))
            throw new ArgumentException("LIVE_GM_ROLE_INVALID", nameof(role));
        _subjects[accountId] = new AuthorizationSubject(accountId, role,
            scopedZoneIds ?? new HashSet<Guid>());
    }

    public Session Authenticate(Request request, Func<string, bool>? secondFactorVerifier = null)
    {
        var identity = accounts.Authenticate(request.Login, request.Password);
        if (!identity.Authenticated) return Reject(identity.Code, identity.AccountId);
        if (!_subjects.TryGetValue(identity.AccountId!.Value, out var subject))
            return Reject("LIVE_GM_ROLE_REQUIRED", identity.AccountId);
        if (requireSecondFactor && (secondFactorVerifier is null || request.SecondFactor is null ||
                                    !secondFactorVerifier(request.SecondFactor)))
            return Reject("LIVE_GM_SECOND_FACTOR_INVALID", identity.AccountId);

        var now = utcNow();
        var sessionId = sessionIdFactory();
        if (sessionId == Guid.Empty || sessionLifetime <= TimeSpan.Zero) throw new InvalidOperationException("LIVE_SESSION_CONFIGURATION_INVALID");
        return new(true, "LIVE_GM_AUTHENTICATED", identity.AccountId, sessionId, now.Add(sessionLifetime), subject,
            $"accepted:LIVE_GM_AUTHENTICATED:account={identity.AccountId}:session={sessionId}");
    }

    public bool IsActive(Session session) => session.Authenticated &&
        session.SessionId is not null && !_revokedSessions.Contains(session.SessionId.Value) &&
        session.ExpiresAt > utcNow();

    public string Revoke(Session session)
    {
        if (session.SessionId is null) return "rejected:LIVE_SESSION_INVALID";
        _revokedSessions.Add(session.SessionId.Value);
        return $"revoked:LIVE_SESSION_REVOKED:session={session.SessionId}";
    }

    private static Session Reject(string code, Guid? accountId) =>
        new(false, code, accountId, null, null, null, $"rejected:{code}:account={accountId}");
}
