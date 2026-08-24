using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Gameplay;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Studio.Session;

internal sealed record StudioUx6BProfileChoice(string Label, AreaId ContextAreaId);

internal sealed record StudioUx6BMovementDraft(
    bool Enabled,
    bool Required,
    float WalkSpeed,
    float RunSpeed,
    float Acceleration,
    float Deceleration,
    float MaximumSlopeDegrees,
    float StepHeight,
    float JumpVelocity,
    float CrouchSpeedMultiplier,
    bool RootMotion,
    bool NetworkPrediction);

internal sealed record StudioUx6BVerbDraft(
    Guid Id,
    string Key,
    string Label,
    string Command,
    float HoldSeconds,
    IReadOnlyList<string> TargetTags,
    bool ServerAuthoritative);

internal sealed record StudioUx6BInteractionDraft(
    bool Enabled,
    bool Required,
    string InputAction,
    InteractionTargetingMode Targeting,
    float FocusDistance,
    float ViewAngleDegrees,
    bool ShowPrompt,
    IReadOnlyList<StudioUx6BVerbDraft> Verbs);

internal sealed record StudioUx6BCameraDraft(
    Guid Id,
    string Key,
    string DisplayName,
    GameplayCameraMode Mode,
    float FieldOfViewDegrees,
    float Distance,
    float MinimumDistance,
    float MaximumDistance,
    float PitchDegrees,
    Vector3Value FocusOffset,
    bool CollisionAvoidance,
    bool AlignCharacterFacing);

internal sealed record StudioUx6BCameraCatalogDraft(
    bool Required,
    string DefaultProfileKey,
    IReadOnlyList<StudioUx6BCameraDraft> Profiles);

internal sealed record StudioUx6BActionDraft(
    Guid Id,
    string Key,
    string Label,
    string Context,
    bool AccessibilityRequired);

internal sealed record StudioUx6BBindingDraft(
    Guid Id,
    string ActionKey,
    RuntimeInputDevice Device,
    string Binding,
    string Glyph,
    bool Primary);

internal sealed record StudioUx6BInputDraft(
    bool Required,
    bool AllowRebinding,
    bool PerPlayerLocal,
    IReadOnlyList<string> Contexts,
    IReadOnlyList<StudioUx6BActionDraft> Actions,
    IReadOnlyList<StudioUx6BBindingDraft> Bindings);

internal sealed record StudioUx6BDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    AreaId ContextAreaId,
    ModuleGameplayControlProfile SourceGameplay,
    StudioUx6BMovementDraft Movement,
    StudioUx6BInteractionDraft Interaction,
    StudioUx6BCameraCatalogDraft Cameras,
    StudioUx6BInputDraft Input);

internal sealed record StudioUx6BDiagnostic(string Code, string Path, string Message, bool Blocking = true);

internal sealed record StudioUx6BPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx6BDiagnostic> Diagnostics,
    IReadOnlyList<string> RuntimePreview,
    bool CanonicalDryRun,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx6BActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6BApprovalSnapshot(
    StudioUx6BDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx6BEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    StudioUx6BApprovalSnapshot Approval);

internal sealed record StudioUx6BSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealModuleData,
    bool MovementValidation,
    bool InteractionValidation,
    bool CameraValidation,
    bool InputValidation,
    bool CanonicalDryRun,
    bool RuntimePreview,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool SavePolicyPreserved,
    bool GameplayTailsPreserved,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
