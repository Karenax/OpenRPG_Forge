namespace Forge.LiveDirector.Client;

public sealed class LiveDirectorClient
{
    public sealed record Connection(Guid ClientId, Guid AccountId, Guid SessionId, DateTimeOffset ExpiresAt, string AuditEntry);

    public Connection Connect(Guid clientId, Authentication.DirectorAuthenticationService.Session session)
    {
        if (clientId == Guid.Empty) throw new ArgumentException("LIVE_CLIENT_ID_INVALID", nameof(clientId));
        if (!session.Authenticated) throw new InvalidOperationException(session.Code);
        return new(clientId, session.AccountId!.Value, session.SessionId!.Value, session.ExpiresAt!.Value,
            $"connected:LIVE_DIRECTOR_CLIENT_CONNECTED:client={clientId}:account={session.AccountId}");
    }
}
