using Forge.Network.Operations;

namespace Forge.Network.Chat;

public enum ChatChannelKind { Local, Party, Tell, Shout, DungeonMaster, Global, System }

public sealed record ChatRuntimePolicy(
    float LocalRadius,
    int MaximumCharacters,
    IReadOnlyList<string> AllowedLanguages,
    IReadOnlyDictionary<string, string> LanguageFilter,
    bool StructuredLogging,
    bool AccessibilityText,
    bool BridgesEnabled,
    IReadOnlyList<ChatChannelKind>? AllowedChannels = null)
{
    public static ChatRuntimePolicy Default { get; } = new(30, 500, ["common"],
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), true, true, true,
        Enum.GetValues<ChatChannelKind>());
}

public sealed record ChatSendContext(Guid? ScopeId = null, Guid? TargetAccountId = null,
    string Language = "common", float SenderX = 0, float SenderY = 0);

public sealed record ChatObserverContext(Guid AccountId, Guid? PartyId, Guid? ZoneId, float X, float Y,
    NetworkRole Role);

public sealed record ChatMessage(Guid MessageId, Guid SenderAccountId, Guid? SenderCharacterId,
    ChatChannelKind Channel, Guid? ScopeId, string Text, DateTimeOffset SentAt, long Sequence,
    string Language = "common", string AccessibleText = "", Guid? TargetAccountId = null,
    float SenderX = 0, float SenderY = 0, IReadOnlyList<string>? BridgeReceipts = null);

public sealed record ChatDecision(bool Accepted, string Code, ChatMessage? Message, string AuditEntry);

public interface IChatBridgeAdapter
{
    string Id { get; }
    string Forward(ChatMessage message);
}

public sealed class ChatService
{
    private readonly AuthorizationService _authorization;
    private readonly ModerationService _moderation;
    private readonly TimeProvider _timeProvider;
    private readonly ChatRuntimePolicy _policy;
    private readonly IReadOnlyList<IChatBridgeAdapter> _bridges;
    private readonly List<ChatMessage> _messages = [];
    private readonly List<string> _audit = [];
    private long _sequence;

    public ChatService(AuthorizationService authorization, ModerationService moderation, TimeProvider timeProvider,
        ChatRuntimePolicy? policy = null, IReadOnlyList<IChatBridgeAdapter>? bridges = null)
    {
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _moderation = moderation ?? throw new ArgumentNullException(nameof(moderation));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _policy = policy ?? ChatRuntimePolicy.Default;
        _bridges = bridges ?? [];
        var allowedChannels = _policy.AllowedChannels ?? Enum.GetValues<ChatChannelKind>();
        if (_policy.LocalRadius is <= 0 or > 10_000 || _policy.MaximumCharacters is < 1 or > 4_096 ||
            _policy.AllowedLanguages.Count == 0 || _policy.AllowedLanguages.Any(string.IsNullOrWhiteSpace) ||
            allowedChannels.Count == 0 || allowedChannels.Distinct().Count() != allowedChannels.Count ||
            _bridges.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != _bridges.Count)
            throw new ArgumentException("NET_CHAT_POLICY_INVALID", nameof(policy));
    }

    public IReadOnlyList<string> Audit => _audit.ToArray();

    public ChatDecision Send(AuthorizationSubject sender, Guid? characterId, ChatChannelKind channel,
        Guid? scopeId, string text) => Send(sender, characterId, channel, text, new(scopeId));

    public ChatDecision Send(AuthorizationSubject sender, Guid? characterId, ChatChannelKind channel,
        string text, ChatSendContext context)
    {
        var allowedChannels = _policy.AllowedChannels ?? Enum.GetValues<ChatChannelKind>();
        if (!allowedChannels.Contains(channel)) return Reject("NET_CHAT_CHANNEL_DENIED", sender.AccountId);
        var capability = channel is ChatChannelKind.Shout or ChatChannelKind.System
            ? NetworkCapability.BroadcastChat
            : NetworkCapability.Chat;
        if (channel == ChatChannelKind.Tell) capability = NetworkCapability.DirectMessage;
        var auth = _authorization.Authorize(sender, capability,
            channel == ChatChannelKind.Local ? context.ScopeId : null);
        if (!auth.Allowed) return Reject(auth.Code, sender.AccountId);
        if (_moderation.IsRestricted(sender.AccountId, ModerationActionKind.Mute))
            return Reject("NET_CHAT_MUTED", sender.AccountId);
        if (channel == ChatChannelKind.System && sender.Role != NetworkRole.Administrator)
            return Reject("NET_CHAT_SYSTEM_DENIED", sender.AccountId);
        if (channel == ChatChannelKind.DungeonMaster && sender.Role is not
            (NetworkRole.GameMaster or NetworkRole.Administrator))
            return Reject("NET_CHAT_DM_DENIED", sender.AccountId);
        if (channel is ChatChannelKind.Local or ChatChannelKind.Party && context.ScopeId is null)
            return Reject("NET_CHAT_SCOPE_REQUIRED", sender.AccountId);
        if (channel == ChatChannelKind.Tell && context.TargetAccountId is null)
            return Reject("NET_CHAT_TARGET_REQUIRED", sender.AccountId);

        var language = context.Language.Trim().ToLowerInvariant();
        if (!_policy.AllowedLanguages.Contains(language, StringComparer.Ordinal))
            return Reject("NET_CHAT_LANGUAGE_DENIED", sender.AccountId);
        var normalized = text.Trim();
        if (normalized.Length is < 1 || normalized.Length > _policy.MaximumCharacters ||
            normalized.Any(char.IsControl))
            return Reject("NET_CHAT_TEXT_INVALID", sender.AccountId);
        foreach (var filter in _policy.LanguageFilter.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            normalized = normalized.Replace(filter.Key, filter.Value, StringComparison.OrdinalIgnoreCase);

        _sequence++;
        var accessible = _policy.AccessibilityText
            ? $"{channel}, message de {sender.AccountId:D} : {normalized}"
            : normalized;
        var draft = new ChatMessage(Guid.NewGuid(), sender.AccountId, characterId, channel, context.ScopeId,
            normalized, _timeProvider.GetUtcNow(), _sequence, language, accessible,
            context.TargetAccountId, context.SenderX, context.SenderY, []);
        var receipts = _policy.BridgesEnabled
            ? _bridges.Select(adapter => $"{adapter.Id}:{adapter.Forward(draft)}").ToArray()
            : [];
        var message = draft with { BridgeReceipts = receipts };
        _messages.Add(message);
        var audit = $"accepted:chat:{channel}:account={sender.AccountId}:sequence={_sequence}:language={language}:bridges={receipts.Length}";
        if (_policy.StructuredLogging) _audit.Add(audit);
        return new(true, "NET_CHAT_ACCEPTED", message, audit);
    }

    public bool VisibleTo(ChatMessage message, ChatObserverContext observer)
    {
        if (message.SenderAccountId == observer.AccountId || observer.Role == NetworkRole.Administrator) return true;
        return message.Channel switch
        {
            ChatChannelKind.Local => message.ScopeId == observer.ZoneId &&
                MathF.Sqrt(MathF.Pow(message.SenderX - observer.X, 2) + MathF.Pow(message.SenderY - observer.Y, 2)) <= _policy.LocalRadius,
            ChatChannelKind.Party => message.ScopeId == observer.PartyId,
            ChatChannelKind.Tell => message.TargetAccountId == observer.AccountId,
            ChatChannelKind.DungeonMaster => observer.Role == NetworkRole.GameMaster,
            ChatChannelKind.System or ChatChannelKind.Global or ChatChannelKind.Shout => true,
            _ => false
        };
    }

    public IReadOnlyList<ChatMessage> Since(long sequence, Func<ChatMessage, bool> visibility) =>
        _messages.Where(item => item.Sequence > sequence).Where(visibility).OrderBy(item => item.Sequence).ToArray();

    private ChatDecision Reject(string code, Guid sender)
    {
        var audit = $"rejected:{code}:account={sender}";
        if (_policy.StructuredLogging) _audit.Add(audit);
        return new(false, code, null, audit);
    }
}
