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
    bool ContainsPersonalData,
    bool Enabled = true,
    bool RequiredAtRuntime = false,
    string CloudAdapter = "none",
    string PrivacyClassification = "personal",
    bool EncryptAtRest = true,
    int MaximumSnapshots = 8);

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
    double SafeRadius,
    bool Enabled = true,
    bool RequiredAtRuntime = false,
    double RespawnDelaySeconds = 3,
    bool RequireSafeSpawn = true,
    string BeforeRespawnEvent = "player.respawn.before",
    string AfterRespawnEvent = "player.respawn.after");

public sealed record RuntimeShellProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string SkinId,
    double Scale,
    bool MapVisible,
    bool JournalVisible,
    bool HudVisible,
    IReadOnlyList<string> InputActions,
    bool Enabled = true,
    bool RequiredAtRuntime = false,
    string KitId = "core-rpg",
    bool InputAgnostic = true,
    bool Scalable = true,
    string LayoutDocument = "ui/runtime-shell.json");

public sealed record ControlContext(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string OwnerId,
    bool CursorVisible,
    string CameraMode,
    HashSet<string> AllowedInputActions,
    bool RestoreOnRelease = true,
    int Priority = 0);

public sealed record ControlLockProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool RequiredAtRuntime,
    IReadOnlyList<ControlContext> Contexts);

public sealed record ReplayFrame(long Tick, string Input, string Event, int Seed, string StateHash);
public sealed record ReplayRecording(Guid Id, int SchemaVersion, string Provenance, IReadOnlyList<ReplayFrame> Frames);
public sealed record ReplayDivergence(long Tick, string ExpectedHash, string ActualHash, ReplayRecording MinimalReproduction);

public sealed record ReplayTestProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool RequiredAtRuntime,
    bool RecordInputs,
    bool RecordEvents,
    bool RecordSeeds,
    bool RecordStateHashes,
    bool HeadlessReplay,
    bool EmitDivergenceReport,
    bool EmitMinimalPackage,
    int MaximumFrames,
    string PackageDirectory,
    bool IncludePersonalData);

public sealed record GameplaySessionProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    bool Enabled,
    bool RequiredAtRuntime,
    PersistenceProfile Persistence,
    IReadOnlyList<CheckpointProfile> Checkpoints,
    RuntimeShellProfile Shell,
    ControlLockProfile ControlLocks,
    ReplayTestProfile Replay)
{
    public const int CurrentSchemaVersion = 1;

    public static GameplaySessionProfile CreateDefault(Forge.Domain.Modules.ModuleId moduleId)
    {
        ArgumentNullException.ThrowIfNull(moduleId);
        Guid Stable(string key) => Forge.Domain.RuntimePolicies.RuntimePolicyIdFactory.Create(
            moduleId.Value, $"gameplay:session:{key}");
        const string provenance = "migration-default:r067";
        var persistence = new PersistenceProfile(Stable("persistence"), 1, provenance,
            PersistenceScope.Game, SaveAuthority.Host, SaveConflictPolicy.LatestRevisionWins,
            false, true, CloudAdapter: "none", PrivacyClassification: "personal",
            EncryptAtRest: true, MaximumSnapshots: 8);
        var checkpoint = new CheckpointProfile(Stable("checkpoint:start"), 1, provenance,
            "waypoint:start", 0, PartyRespawnPolicy.EntireParty, CorpsePolicy.TransferInventory, 3,
            RespawnDelaySeconds: 3, RequireSafeSpawn: true);
        var shell = new RuntimeShellProfile(Stable("shell"), 1, provenance, "forge-aurora-dark", 1,
            true, true, true, ["map", "journal", "inventory", "character", "pause"],
            KitId: "core-rpg", InputAgnostic: true, Scalable: true);
        var contexts = new[]
        {
            new ControlContext(Stable("control:exploration"), 1, provenance, "exploration", false,
                "orbit", new HashSet<string>(["move", "interact", "map", "journal"]), true, 0),
            new ControlContext(Stable("control:dialogue"), 1, provenance, "dialogue", true,
                "dialogue", new HashSet<string>(["dialogue.select", "dialogue.skip"]), true, 50),
            new ControlContext(Stable("control:cinematic"), 1, provenance, "cinematic", false,
                "cinematic", new HashSet<string>(["cinematic.skip"]), true, 100),
        };
        var locks = new ControlLockProfile(Stable("control"), 1, provenance, true, false, contexts);
        var replay = new ReplayTestProfile(Stable("replay"), 1, provenance, true, false,
            true, true, true, true, true, true, true, 100_000, ".artifacts/replays", false);
        return new GameplaySessionProfile(Stable("root"), 1, provenance, true, false, persistence,
            [checkpoint], shell, locks, replay);
    }

    public IReadOnlyList<Guid> AllIds() =>
    [
        Id, Persistence.Id, Shell.Id, ControlLocks.Id, Replay.Id,
        .. Checkpoints.Select(item => item.Id),
        .. ControlLocks.Contexts.Select(item => item.Id),
    ];
}
