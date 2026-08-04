using Forge.Network.Operations;

namespace Forge.LiveDirector.Visibility;

public sealed class LiveVisibilityService(AuthorizationService authorization)
{
    public sealed record View(string Mode, Guid? PlayerAccountId, bool HiddenObjects, bool Secrets, bool Navigation, bool Encounters, string AuditEntry);
    public View Configure(AuthorizationSubject actor, string mode, Guid? playerAccountId, bool hidden, bool secrets, bool navigation, bool encounters)
    {
        if (!authorization.Authorize(actor, NetworkCapability.ViewOperations).Allowed) throw new UnauthorizedAccessException("LIVE_VISIBILITY_PERMISSION_DENIED");
        if (mode is not ("omniscient" or "selected-player") || mode == "selected-player" && playerAccountId is null) throw new ArgumentException("LIVE_VISIBILITY_MODE_INVALID", nameof(mode));
        return new(mode, playerAccountId, hidden, secrets, navigation, encounters, $"configured:visibility={mode}:actor={actor.AccountId}:player={playerAccountId}");
    }
}
