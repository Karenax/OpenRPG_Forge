namespace Forge.Network.Sessions;

public enum DiscoveryMode { Direct, Lan }
public enum SessionRole { Player, Moderator, Administrator }

public sealed record DedicatedServerConfiguration(
    Guid ServerId,
    int SchemaVersion,
    string Name,
    string BindAddress,
    int Port,
    int Capacity,
    DiscoveryMode Discovery,
    string ModuleFingerprint,
    string ProtocolVersion);

public sealed record NetworkDiagnostic(string Code, string Path, string Message);

public sealed record CharacterDraft(
    Guid CharacterId,
    string Name,
    string AncestryId,
    string ClassId,
    IReadOnlyDictionary<string, int> Attributes);

public sealed record SessionIdentity(Guid AccountId, string DisplayName, SessionRole Role);
public sealed record LobbyMember(Guid ConnectionId, SessionIdentity Identity, CharacterDraft? Character, bool Ready);
public sealed record LobbySnapshot(Guid SessionId, long Revision, IReadOnlyList<LobbyMember> Members);

public sealed record ClientIntent(
    Guid IntentId,
    Guid ConnectionId,
    long ExpectedRevision,
    string Kind,
    IReadOnlyDictionary<string, string> Payload);

public sealed record AuthorityDecision(
    bool Accepted,
    string Code,
    long Revision,
    LobbySnapshot Snapshot,
    string AuditEntry);
