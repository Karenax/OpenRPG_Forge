using System.Security.Cryptography;

namespace Forge.Network.Sessions;

public sealed record ReconnectionTicket(string Token, Guid AccountId, Guid CharacterId, Guid SessionId, long LastAcknowledgedRevision, DateTimeOffset ExpiresAt);
public sealed record ReconnectionDecision(bool Accepted, string Code, ReconnectionTicket? Ticket, string AuditEntry);

public sealed class ReconnectionService(TimeProvider timeProvider, TimeSpan lifetime)
{
    private readonly Dictionary<string, ReconnectionTicket> _tickets = new(StringComparer.Ordinal);

    public ReconnectionTicket Issue(Guid accountId, Guid characterId, Guid sessionId, long lastAcknowledgedRevision)
    {
        if (accountId == Guid.Empty || characterId == Guid.Empty || sessionId == Guid.Empty || lastAcknowledgedRevision < 0) throw new ArgumentException("NET_RECONNECT_IDENTITY_INVALID");
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var ticket = new ReconnectionTicket(token, accountId, characterId, sessionId, lastAcknowledgedRevision, timeProvider.GetUtcNow() + lifetime);
        _tickets.Add(token, ticket);
        return ticket;
    }

    public ReconnectionDecision Redeem(string token, Guid accountId, Guid sessionId)
    {
        if (!_tickets.Remove(token, out var ticket)) return Reject("NET_RECONNECT_TOKEN_INVALID", accountId);
        if (ticket.ExpiresAt <= timeProvider.GetUtcNow()) return Reject("NET_RECONNECT_TOKEN_EXPIRED", accountId);
        if (ticket.AccountId != accountId || ticket.SessionId != sessionId) return Reject("NET_RECONNECT_IDENTITY_MISMATCH", accountId);
        return new(true, "NET_RECONNECT_ACCEPTED", ticket, $"accepted:NET_RECONNECT_ACCEPTED:account={accountId}:revision={ticket.LastAcknowledgedRevision}");
    }

    private static ReconnectionDecision Reject(string code, Guid accountId) => new(false, code, null, $"rejected:{code}:account={accountId}");
}
