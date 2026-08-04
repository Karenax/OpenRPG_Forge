using Forge.LiveDirector.Audit;
using Forge.Network.Operations;

namespace Forge.LiveDirector.Chat;

public sealed class LiveDirectorChatService(AuthorizationService authorization, LiveAuditJournal audit, Func<DateTimeOffset> utcNow)
{
    public sealed record Message(long Sequence, Guid ActorId, string Channel, Guid? ScopeId, string Text, Guid? ImpersonatedObjectId, DateTimeOffset At);
    private readonly List<Message> _messages = [];
    public IReadOnlyList<Message> Messages => _messages.ToArray();

    public Message Send(AuthorizationSubject actor, string channel, Guid? scopeId, string text, Guid? impersonatedObjectId, Guid correlationId)
    {
        if (!authorization.Authorize(actor, NetworkCapability.Chat).Allowed) throw new UnauthorizedAccessException("LIVE_CHAT_PERMISSION_DENIED");
        if (channel is not ("local" or "party" or "tell" or "server" or "gm" or "faction" or "impersonation")) throw new ArgumentException("LIVE_CHAT_CHANNEL_INVALID", nameof(channel));
        if (channel is "local" or "party" or "tell" or "faction" && scopeId is null) throw new ArgumentException("LIVE_CHAT_SCOPE_REQUIRED", nameof(scopeId));
        if (channel == "impersonation" && impersonatedObjectId is null) throw new ArgumentException("LIVE_CHAT_IMPERSONATION_REQUIRED", nameof(impersonatedObjectId));
        var normalized = text.Trim();
        if (normalized.Length is < 1 or > 500 || normalized.Any(char.IsControl)) throw new ArgumentException("LIVE_CHAT_TEXT_INVALID", nameof(text));
        var message = new Message(_messages.Count + 1L, actor.AccountId, channel, scopeId, normalized, impersonatedObjectId, utcNow());
        _messages.Add(message);
        audit.Append(actor.AccountId, "chat.send", impersonatedObjectId is null ? [] : [impersonatedObjectId.Value], "", $"channel={channel}:text={normalized}", "live communication", correlationId);
        return message;
    }
}
