using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Gameplay;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx6CProfileChoice(string Label, AreaId ContextAreaId);

internal sealed record StudioUx6CSwimmingDraft(
    bool Enabled,
    bool Required,
    float Buoyancy,
    float SurfaceSnapDistance,
    float SurfaceSpeed,
    float DiveSpeed,
    float CurrentInfluence,
    float BreathSeconds,
    IReadOnlyList<string> WaterVolumeTags,
    string SurfaceAnimation,
    string DiveAnimation,
    string IdleAnimation);

internal sealed record StudioUx6CClimbingDraft(
    bool Enabled,
    bool Required,
    float ClimbSpeed,
    float StaminaCapacity,
    float StaminaDrainPerSecond,
    float LedgeReach,
    bool InverseKinematics,
    bool RequireAuthoredLinks,
    bool PreventUnsafeDrop,
    IReadOnlyList<string> SurfaceTags,
    string ClimbAnimation,
    string LedgeAnimation);

internal sealed record StudioUx6CRadialActionDraft(
    Guid Id,
    string Key,
    string Label,
    string AccessibleLabel,
    RadialActionKind Kind,
    string Command,
    string ParentKey,
    int Order,
    bool RequiresTarget,
    IReadOnlyList<string> Tags);

internal sealed record StudioUx6CRadialMenuDraft(
    bool Enabled,
    string InputAction,
    float DeadZone,
    int MaximumDepth,
    bool GamepadEnabled,
    bool KeyboardNavigation,
    bool ListFallback,
    bool ScreenReaderLabels,
    IReadOnlyList<StudioUx6CRadialActionDraft> Actions);

internal sealed record StudioUx6CTimeControlDraft(
    bool PauseEnabled,
    bool SlowMotionEnabled,
    float SlowMotionScale,
    bool PlanningEnabled,
    int MaximumPlannedActions,
    bool TurnModeEnabled,
    MultiplayerPausePolicy MultiplayerPolicy,
    float RequiredVoteFraction,
    IReadOnlyList<string> PausePermissions);

internal sealed record StudioUx6CCombatProviderDraft(
    Guid Id,
    string ProviderId,
    string DisplayName,
    CombatModeKind Kind,
    string CustomProvider,
    IReadOnlyList<string> EventContracts,
    bool ServerAuthoritative);

internal sealed record StudioUx6CCombatDraft(
    string DefaultProviderId,
    IReadOnlyList<StudioUx6CCombatProviderDraft> Providers);

internal sealed record StudioUx6CDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    AreaId ContextAreaId,
    ModuleGameplayControlProfile SourceGameplay,
    StudioUx6CSwimmingDraft Swimming,
    StudioUx6CClimbingDraft Climbing,
    StudioUx6CRadialMenuDraft RadialMenu,
    StudioUx6CTimeControlDraft TimeControl,
    StudioUx6CCombatDraft CombatModes);

internal sealed record StudioUx6CDiagnostic(string Code, string Path, string Message, bool Blocking = true);

internal sealed record StudioUx6CPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx6CDiagnostic> Diagnostics,
    IReadOnlyList<string> RuntimePreview,
    bool CanonicalDryRun,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx6CActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6CApprovalSnapshot(
    StudioUx6CDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx6CEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    StudioUx6CApprovalSnapshot Approval);

internal sealed record StudioUx6CSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealModuleData,
    bool SwimmingValidation,
    bool ClimbingValidation,
    bool RadialValidation,
    bool TimeControlValidation,
    bool CombatValidation,
    bool CanonicalDryRun,
    bool RuntimePreview,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool FoundationPreserved,
    bool GameplayTailsPreserved,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
