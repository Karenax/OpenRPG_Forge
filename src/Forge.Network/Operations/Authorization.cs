namespace Forge.Network.Operations;

public enum NetworkCapability
{
    Play,
    Chat,
    DirectMessage,
    BroadcastChat,
    ModerateChat,
    BuildContent,
    ManagePlayers,
    ManageWorld,
    ManageEvents,
    ViewOperations,
    Observe
}

public enum NetworkRole { Player, Moderator, Builder, GameMaster, Administrator, Spectator }

public sealed record AuthorizationSubject(Guid AccountId, NetworkRole Role, IReadOnlySet<Guid> ScopedZoneIds,
    IReadOnlySet<string>? CustomCapabilities = null);
public sealed record AuthorizationDecision(bool Allowed, string Code, string AuditEntry);

public sealed class AuthorizationService
{
    public AuthorizationDecision Authorize(AuthorizationSubject subject, NetworkCapability capability,
        Guid? zoneId = null, string? customCapability = null)
    {
        if (subject.AccountId == Guid.Empty) return Reject("NET_AUTH_SUBJECT_INVALID", subject.AccountId, capability);
        var customAllowed = string.IsNullOrWhiteSpace(customCapability) ||
            subject.Role == NetworkRole.Administrator ||
            subject.CustomCapabilities?.Contains(customCapability.Trim(), StringComparer.Ordinal) == true;
        var roleAllowed = subject.Role switch
        {
            NetworkRole.Administrator => true,
            NetworkRole.GameMaster => capability is not NetworkCapability.BuildContent,
            NetworkRole.Builder => capability is NetworkCapability.Play or NetworkCapability.Chat or
                NetworkCapability.DirectMessage or NetworkCapability.BuildContent or NetworkCapability.ManageWorld or
                NetworkCapability.ManageEvents or NetworkCapability.ViewOperations or NetworkCapability.Observe,
            NetworkRole.Moderator => capability is NetworkCapability.Play or NetworkCapability.Chat or
                NetworkCapability.DirectMessage or NetworkCapability.BroadcastChat or NetworkCapability.ModerateChat or
                NetworkCapability.ManagePlayers or NetworkCapability.ViewOperations or NetworkCapability.Observe,
            NetworkRole.Player => capability is NetworkCapability.Play or NetworkCapability.Chat or
                NetworkCapability.DirectMessage,
            NetworkRole.Spectator => capability is NetworkCapability.Chat or NetworkCapability.DirectMessage or
                NetworkCapability.ViewOperations or NetworkCapability.Observe,
            _ => false
        };
        var allowed = roleAllowed && customAllowed;
        if (allowed && zoneId is not null && subject.Role != NetworkRole.Administrator && !subject.ScopedZoneIds.Contains(zoneId.Value))
            return Reject("NET_AUTH_SCOPE_DENIED", subject.AccountId, capability);
        return allowed
            ? new(true, "NET_AUTH_ALLOWED", $"accepted:NET_AUTH_ALLOWED:account={subject.AccountId}:role={subject.Role}:capability={capability}:custom={customCapability}:zone={zoneId}")
            : Reject(customAllowed ? "NET_AUTH_CAPABILITY_DENIED" : "NET_AUTH_CUSTOM_CAPABILITY_DENIED",
                subject.AccountId, capability);
    }

    private static AuthorizationDecision Reject(string code, Guid accountId, NetworkCapability capability) => new(false, code, $"rejected:{code}:account={accountId}:capability={capability}");
}
