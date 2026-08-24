namespace Forge.Network.Sessions;

public sealed class AuthoritativeSession
{
    private readonly DedicatedServerConfiguration _configuration;
    private readonly Dictionary<Guid, LobbyMember> _members = [];
    private readonly HashSet<Guid> _processedIntents = [];
    private long _revision;

    public AuthoritativeSession(DedicatedServerConfiguration configuration, Guid? sessionId = null)
    {
        var diagnostics = NetworkValidation.Validate(configuration);
        if (diagnostics.Count != 0) throw new ArgumentException(diagnostics[0].Code, nameof(configuration));
        _configuration = configuration;
        SessionId = sessionId ?? Guid.NewGuid();
    }

    public Guid SessionId { get; }
    public LobbySnapshot Snapshot => new(SessionId, _revision, _members.Values.OrderBy(x => x.ConnectionId).ToArray());

    public AuthorityDecision Connect(Guid connectionId, SessionIdentity identity, string fingerprint, string protocolVersion)
    {
        if (fingerprint != _configuration.ModuleFingerprint) return Reject("NET_CONTENT_MISMATCH", connectionId);
        if (protocolVersion != _configuration.ProtocolVersion) return Reject("NET_PROTOCOL_MISMATCH", connectionId);
        if (_members.Count >= _configuration.Capacity) return Reject("NET_SERVER_FULL", connectionId);
        if (connectionId == Guid.Empty || identity.AccountId == Guid.Empty) return Reject("NET_IDENTITY_INVALID", connectionId);
        _members[connectionId] = new(connectionId, identity, null, false);
        _revision++;
        return Accept("NET_CONNECTED", connectionId);
    }

    public AuthorityDecision Apply(ClientIntent intent)
    {
        if (!_members.TryGetValue(intent.ConnectionId, out var member)) return Reject("NET_CONNECTION_UNKNOWN", intent.ConnectionId);
        if (!_processedIntents.Add(intent.IntentId)) return Reject("NET_INTENT_REPLAYED", intent.ConnectionId);
        if (intent.ExpectedRevision != _revision) return Reject("NET_REVISION_STALE", intent.ConnectionId);
        if (intent.Kind != "lobby.ready") return Reject("NET_INTENT_FORBIDDEN", intent.ConnectionId);
        if (!bool.TryParse(intent.Payload.GetValueOrDefault("ready"), out var ready)) return Reject("NET_INTENT_INVALID", intent.ConnectionId);
        _members[intent.ConnectionId] = member with { Ready = ready };
        _revision++;
        return Accept("NET_INTENT_APPLIED", intent.ConnectionId);
    }

    public AuthorityDecision SelectCharacter(Guid connectionId, CharacterDraft draft, long expectedRevision)
    {
        if (!_members.TryGetValue(connectionId, out var member)) return Reject("NET_CONNECTION_UNKNOWN", connectionId);
        if (expectedRevision != _revision) return Reject("NET_REVISION_STALE", connectionId);
        var diagnostics = NetworkValidation.Validate(draft);
        if (diagnostics.Count != 0) return Reject(diagnostics[0].Code, connectionId);
        if (_members.Values.Any(x => x.Character?.CharacterId == draft.CharacterId)) return Reject("NET_CHARACTER_ALREADY_SELECTED", connectionId);
        _members[connectionId] = member with { Character = draft, Ready = false };
        _revision++;
        return Accept("NET_CHARACTER_SELECTED", connectionId);
    }

    public AuthorityDecision Disconnect(Guid connectionId)
    {
        if (!_members.Remove(connectionId)) return Reject("NET_CONNECTION_UNKNOWN", connectionId);
        _revision++;
        return Accept("NET_DISCONNECTED", connectionId);
    }

    private AuthorityDecision Accept(string code, Guid connectionId) => new(true, code, _revision, Snapshot, $"accepted:{code}:connection={connectionId}:revision={_revision}");
    private AuthorityDecision Reject(string code, Guid connectionId) => new(false, code, _revision, Snapshot, $"rejected:{code}:connection={connectionId}:revision={_revision}");
}
