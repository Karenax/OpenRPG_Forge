using Forge.Network.Operations;

namespace Forge.Network.Chat;

public enum ChatChannelKind { Local, Party, Global, System }
public sealed record ChatMessage(Guid MessageId, Guid SenderAccountId, Guid? SenderCharacterId, ChatChannelKind Channel, Guid? ScopeId, string Text, DateTimeOffset SentAt, long Sequence);
public sealed record ChatDecision(bool Accepted, string Code, ChatMessage? Message, string AuditEntry);

public sealed class ChatService(AuthorizationService authorization, ModerationService moderation, TimeProvider timeProvider)
{
    private readonly List<ChatMessage> _messages = [];
    private long _sequence;

    public ChatDecision Send(AuthorizationSubject sender, Guid? characterId, ChatChannelKind channel, Guid? scopeId, string text)
    {
        var auth = authorization.Authorize(sender, NetworkCapability.Chat, channel == ChatChannelKind.Local ? scopeId : null);
        if (!auth.Allowed) return Reject(auth.Code, sender.AccountId);
        if (moderation.IsRestricted(sender.AccountId, ModerationActionKind.Mute)) return Reject("NET_CHAT_MUTED", sender.AccountId);
        var normalized = text.Trim();
        if (normalized.Length is < 1 or > 500 || normalized.Any(char.IsControl)) return Reject("NET_CHAT_TEXT_INVALID", sender.AccountId);
        if (channel is ChatChannelKind.Local or ChatChannelKind.Party && scopeId is null) return Reject("NET_CHAT_SCOPE_REQUIRED", sender.AccountId);
        if (channel == ChatChannelKind.System && sender.Role != NetworkRole.Administrator) return Reject("NET_CHAT_SYSTEM_DENIED", sender.AccountId);
        _sequence++;
        var message = new ChatMessage(Guid.NewGuid(), sender.AccountId, characterId, channel, scopeId, normalized, timeProvider.GetUtcNow(), _sequence);
        _messages.Add(message);
        return new(true, "NET_CHAT_ACCEPTED", message, $"accepted:chat:{channel}:account={sender.AccountId}:sequence={_sequence}");
    }

    public IReadOnlyList<ChatMessage> Since(long sequence, Func<ChatMessage, bool> visibility) => _messages.Where(x => x.Sequence > sequence).Where(visibility).OrderBy(x => x.Sequence).ToArray();
    private static ChatDecision Reject(string code, Guid sender) => new(false, code, null, $"rejected:{code}:account={sender}");
}
