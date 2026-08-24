using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;

namespace Forge.Studio.Session;

internal sealed record StudioUx6EProfileChoice(string Label);

internal sealed record StudioUx6EZeroGravityDraft(
    bool Enabled,
    float ThrusterAcceleration,
    float MaximumSpeed,
    float LinearDrag,
    bool OrientationAssist,
    bool PreserveMomentum);

internal sealed record StudioUx6EVehicleDraft(
    bool Enabled,
    int MaximumSeats,
    string MotionMode,
    int MaximumIntegrity,
    bool AllowPossession,
    bool ReplicateAuthoritatively);

internal sealed record StudioUx6EMountDraft(
    bool Enabled,
    bool AllowCombat,
    bool AllowInventory,
    bool PersistRiderState,
    string DismountPolicy);

internal sealed record StudioUx6EAchievementDraft(
    Guid Id,
    string Key,
    string EventName,
    int Threshold,
    string? PlatformKey,
    bool PrivacySafeTelemetry);

internal sealed record StudioUx6EDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    ModuleRuntimeExtensionProfile SourceExtensions,
    StudioUx6EZeroGravityDraft ZeroGravity,
    StudioUx6EVehicleDraft Vehicles,
    StudioUx6EMountDraft Mounts,
    IReadOnlyList<StudioUx6EAchievementDraft> Achievements);

internal sealed record StudioUx6EDiagnostic(string Code, string Path, string Message, bool Blocking = true);

internal sealed record StudioUx6EPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx6EDiagnostic> Diagnostics,
    IReadOnlyList<string> RuntimePreview,
    bool CanonicalDryRun,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx6EActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6EApprovalSnapshot(
    StudioUx6EDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx6EEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    StudioUx6EApprovalSnapshot Approval);

internal sealed record StudioUx6ESmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealModuleData,
    bool ZeroGravityValidation,
    bool VehicleValidation,
    bool MountValidation,
    bool AchievementValidation,
    bool CanonicalDryRun,
    bool RuntimePreview,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborProfilesPreserved,
    bool StableIdentities,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
