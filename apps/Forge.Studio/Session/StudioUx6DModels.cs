using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Gameplay;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Studio.Session;

internal sealed record StudioUx6DProfileChoice(string Label, AreaId ContextAreaId);

internal sealed record StudioUx6DFlightDraft(
    bool Enabled,
    bool Required,
    float MaximumSpeed,
    float Acceleration,
    float MaximumAltitude,
    float BankingDegrees,
    float Lift,
    bool RequiresLandingSurface,
    bool VolumetricNavigation,
    float LandingProbeDistance,
    string InputContext);

internal sealed record StudioUx6DProjectileDraft(
    bool Enabled,
    bool Required,
    ProjectileMode Mode,
    float Speed,
    float Gravity,
    float LifetimeSeconds,
    float HomingStrength,
    float CollisionRadius,
    bool ServerAuthoritative,
    bool Prediction,
    string VfxAssetId,
    string ImpactVfxAssetId,
    string CollisionMask,
    long ServerRevision,
    string AuditTag);

internal sealed record StudioUx6DPhysicsBodyDraft(
    Guid Id,
    string Name,
    PhysicsBodyMode Mode,
    float Mass,
    float LinearDamping,
    float GravityScale,
    bool IsTrigger,
    bool InteractsWithCharacters,
    Vector3Value BoundaryMinimum,
    Vector3Value BoundaryMaximum,
    string NetworkPolicy,
    bool RequiredAtRuntime);

internal sealed record StudioUx6DPhysicsJointDraft(
    Guid Id,
    string Name,
    PhysicsJointMode Mode,
    Guid BodyAId,
    Guid BodyBId,
    float BreakForce,
    bool CollisionExcluded);

internal sealed record StudioUx6DPhysicsDraft(
    bool Enabled,
    bool Required,
    float FixedStepSeconds,
    IReadOnlyList<StudioUx6DPhysicsBodyDraft> Bodies,
    IReadOnlyList<StudioUx6DPhysicsJointDraft> Joints);

internal sealed record StudioUx6DTargetingDraft(
    bool Enabled,
    bool Required,
    TargetingMode Mode,
    float MaximumRange,
    float Radius,
    float AngleDegrees,
    int MaximumTargets,
    bool RequiresLineOfSight,
    string InputAction,
    string PreviewColor,
    IReadOnlyList<string> RequiredTags,
    IReadOnlyList<string> BlockedTags);

internal sealed record StudioUx6DDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    AreaId ContextAreaId,
    ModuleGameplayControlProfile SourceGameplay,
    StudioUx6DFlightDraft Flight,
    StudioUx6DProjectileDraft Projectiles,
    StudioUx6DPhysicsDraft Physics,
    StudioUx6DTargetingDraft Targeting);

internal sealed record StudioUx6DDiagnostic(string Code, string Path, string Message, bool Blocking = true);

internal sealed record StudioUx6DPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx6DDiagnostic> Diagnostics,
    IReadOnlyList<string> RuntimePreview,
    bool CanonicalDryRun,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx6DActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6DApprovalSnapshot(
    StudioUx6DDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx6DEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    StudioUx6DApprovalSnapshot Approval);

internal sealed record StudioUx6DSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealModuleData,
    bool FlightValidation,
    bool ProjectileValidation,
    bool PhysicsValidation,
    bool TargetingValidation,
    bool CanonicalDryRun,
    bool RuntimePreview,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool WorldTimePreserved,
    bool GameplayTailsPreserved,
    bool StableIdentities,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
