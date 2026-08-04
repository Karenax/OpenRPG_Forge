namespace Forge.Network.World;

public enum CharacterStorageScope { Local, Server }
public enum FriendlyFirePolicy { Disabled, PartyOnly, Enabled }
public enum ZoneLifecycle { Persistent, Session, Ephemeral }

public sealed record NetworkCharacter(
    Guid CharacterId,
    Guid OwnerAccountId,
    string Name,
    CharacterStorageScope StorageScope,
    string ContentFingerprint,
    long Revision);

public sealed record Party(Guid PartyId, Guid LeaderCharacterId, IReadOnlySet<Guid> MemberCharacterIds, long Revision);

public sealed record ZoneTemplate(Guid TemplateId, string AreaId, int MaxInstances, int MaxPlayersPerInstance, bool AllowsDynamicCreation);

public sealed record ZoneInstance(
    Guid InstanceId,
    Guid TemplateId,
    ZoneLifecycle Lifecycle,
    Guid? OwnerPartyId,
    IReadOnlySet<Guid> PlayerCharacterIds,
    long Revision);

public sealed record WorldDiagnostic(string Code, string Path, string Message);
public sealed record WorldDecision(bool Accepted, string Code, long Revision, string AuditEntry);
