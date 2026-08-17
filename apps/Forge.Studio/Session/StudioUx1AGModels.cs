using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioDoorAccessEventsChoice(
    BlueprintId BlueprintId,
    string Label,
    bool Configured);

internal sealed record StudioDoorAccessEventsDraft(
    BlueprintId BlueprintId,
    bool AccessRequiredAtRuntime,
    DoorAccessLockState LockState,
    IReadOnlyList<string> CredentialIds,
    string SkillId,
    double Difficulty,
    bool Hackable,
    bool Relockable,
    bool CanJam,
    bool ConsumeCredential,
    IReadOnlyList<string> Permissions,
    string RejectionForgeScriptApiVersion,
    string RejectionFrenchText,
    string RejectionEnglishText,
    string RejectionSoundAssetId,
    string RejectionAnimationId,
    string RejectionScriptId,
    string TrapTriggerId,
    string TrapPayloadId,
    double TrapDetectionDifficulty,
    double TrapDisarmDifficulty,
    double TrapResetDelaySeconds,
    string TrapOwnerId,
    string TrapVisualEffectId,
    string TrapFriendlyPolicy,
    bool TrapArmed,
    bool TrapOneShot,
    IReadOnlyList<string> TrapSides,
    IReadOnlyList<string> TrapEvents,
    string StateForgeScriptApiVersion,
    bool LockStateEventsEnabled,
    bool LockStateEventsRequiredAtRuntime,
    string LockStateForgeScriptApiVersion);

internal sealed record StudioDoorAccessEventsPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    bool LockSimulationAccepted = false,
    bool RejectionSimulationAccepted = false,
    bool TrapSimulationAccepted = false,
    bool StateEventsSimulationAccepted = false,
    bool LockEventsSimulationAccepted = false);

internal sealed record StudioDoorAccessEventsActionResult(
    bool Succeeded,
    string Message,
    bool StaleRevision = false);

internal sealed record StudioUx1AGSmokeResult(
    bool Succeeded,
    bool ExplicitDoorSelection,
    bool FreshHiddenRequiredScaffold,
    bool HiddenRequiredScaffoldPreserved,
    bool LockAndCredentials,
    bool Rejection,
    bool Trap,
    bool TerminalStateEvents,
    bool TerminalLockEvents,
    bool DryRun,
    bool ApprovalRevisionGuard,
    bool CommandRevisionGuard,
    bool CanonicalCommand,
    bool Preservation,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);
