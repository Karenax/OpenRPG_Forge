namespace Forge.Domain.Gameplay;

public enum PersistenceScope { Player, Game, Server }
public enum SaveAuthority { LocalPlayer, Host, Server }
public enum SaveConflictPolicy { Reject, LatestRevisionWins, ServerWins }

public sealed record PersistenceProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    PersistenceScope Scope,
    SaveAuthority Authority,
    SaveConflictPolicy ConflictPolicy,
    bool CloudAdapterEnabled,
    bool ContainsPersonalData);

public sealed record SaveEnvelope(
    PersistenceProfile Profile,
    string OwnerId,
    long Revision,
    DateTimeOffset SavedAt,
    IReadOnlyDictionary<string, string> Values);

public enum PartyRespawnPolicy { Individual, EntireParty, LeaderDecision }
public enum CorpsePolicy { Preserve, TransferInventory, Remove }

public sealed record CheckpointProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string WaypointId,
    int CurrencyCost,
    PartyRespawnPolicy PartyPolicy,
    CorpsePolicy CorpsePolicy,
    double SafeRadius);

public sealed record RuntimeShellProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string SkinId,
    double Scale,
    bool MapVisible,
    bool JournalVisible,
    bool HudVisible,
    IReadOnlyList<string> InputActions);

public sealed record ControlContext(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string OwnerId,
    bool CursorVisible,
    string CameraMode,
    IReadOnlySet<string> AllowedInputActions);

public sealed record ReplayFrame(long Tick, string Input, string Event, int Seed, string StateHash);
public sealed record ReplayRecording(Guid Id, int SchemaVersion, string Provenance, IReadOnlyList<ReplayFrame> Frames);
public sealed record ReplayDivergence(long Tick, string ExpectedHash, string ActualHash, ReplayRecording MinimalReproduction);
