using Forge.Domain.Spatial;

namespace Forge.Domain.WorldFeatures;

public enum DoorAccessLockState
{
    Unlocked,
    Locked,
    Jammed,
    Broken,
}

public sealed record DoorAccessEventProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string AssetId,
    TransformValue Transform,
    DoorAccessEventProfile.DoorLockPolicy Lock,
    DoorAccessEventProfile.DoorOpenRejectedPolicy Rejection,
    DoorAccessEventProfile.DoorTrapPolicy Trap,
    DoorAccessEventProfile.DoorPortalUsePolicy PortalUse,
    DoorAccessEventProfile.DoorStateEventPolicy StateEvents,
    IReadOnlyDictionary<string, string> Overrides,
    DoorAccessEventProfile.DoorLockEventPolicy? LockStateEvents = null,
    bool LockStateEventsRequiredAtRuntime = false)
{
    public const int CurrentSchemaVersion = 1;

    public sealed record DoorLockPolicy(
        Guid Id,
        DoorAccessLockState State,
        IReadOnlyList<string> CredentialIds,
        string SkillId,
        double Difficulty,
        string? CodeHash,
        bool Hackable,
        bool Relockable,
        bool CanJam,
        bool ConsumeCredential,
        IReadOnlyList<string> Permissions);

    public sealed record LocalizedFailure(string Locale, string Text);

    public sealed record DoorOpenRejectedPolicy(
        Guid Id,
        string ForgeScriptApiVersion,
        string EventBinding,
        IReadOnlyList<LocalizedFailure> Texts,
        string SoundAssetId,
        string AnimationId,
        string ScriptId);

    public sealed record DoorTrapPolicy(
        Guid Id,
        string TriggerId,
        string PayloadId,
        double DetectionDifficulty,
        double DisarmDifficulty,
        double ResetDelaySeconds,
        string OwnerId,
        string VisualEffectId,
        string FriendlyPolicy,
        bool Armed,
        bool OneShot,
        IReadOnlyList<string> Sides,
        IReadOnlyList<string> Events);

    public sealed record DoorPortalUsePolicy(
        Guid Id,
        string ForgeScriptApiVersion,
        string RequestedBinding,
        string CompletedBinding);

    public sealed record DoorStateEventPolicy(
        Guid Id,
        string ForgeScriptApiVersion,
        string OpenedBinding,
        string ClosedBinding);

    public sealed record DoorLockEventPolicy(
        Guid Id,
        int SchemaVersion,
        string Provenance,
        string ForgeScriptApiVersion,
        string LockedBinding,
        string UnlockedBinding);
}
