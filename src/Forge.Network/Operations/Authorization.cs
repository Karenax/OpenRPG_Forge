namespace Forge.Network.Operations;

public enum NetworkCapability
{
    Play,
    Chat,
    ModerateChat,
    ManagePlayers,
    ManageWorld,
    ViewOperations
}

public enum NetworkRole { Player, Moderator, Administrator }

public sealed record AuthorizationSubject(Guid AccountId, NetworkRole Role, IReadOnlySet<Guid> ScopedZoneIds);
public sealed record AuthorizationDecision(bool Allowed, string Code, string AuditEntry);

public sealed class AuthorizationService
{
    public AuthorizationDecision Authorize(AuthorizationSubject subject, NetworkCapability capability, Guid? zoneId = null)
    {
        if (subject.AccountId == Guid.Empty) return Reject("NET_AUTH_SUBJECT_INVALID", subject.AccountId, capability);
        var allowed = subject.Role switch
        {
            NetworkRole.Administrator => true,
            NetworkRole.Moderator => capability is NetworkCapability.Play or NetworkCapability.Chat or NetworkCapability.ModerateChat or NetworkCapability.ViewOperations,
            NetworkRole.Player => capability is NetworkCapability.Play or NetworkCapability.Chat,
            _ => false
        };
        if (allowed && zoneId is not null && subject.Role != NetworkRole.Administrator && !subject.ScopedZoneIds.Contains(zoneId.Value))
            return Reject("NET_AUTH_SCOPE_DENIED", subject.AccountId, capability);
        return allowed
            ? new(true, "NET_AUTH_ALLOWED", $"accepted:NET_AUTH_ALLOWED:account={subject.AccountId}:capability={capability}:zone={zoneId}")
            : Reject("NET_AUTH_CAPABILITY_DENIED", subject.AccountId, capability);
    }

    private static AuthorizationDecision Reject(string code, Guid accountId, NetworkCapability capability) => new(false, code, $"rejected:{code}:account={accountId}:capability={capability}");
}
