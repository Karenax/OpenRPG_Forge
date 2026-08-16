using Forge.Domain.Blueprints;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioUx1ZPlaceableOption(
    BlueprintId BlueprintId,
    string DisplayName,
    bool HasProfile);

internal sealed record StudioUx1ZCatalog(
    IReadOnlyList<StudioUx1ZPlaceableOption> Placeables);

internal sealed record StudioUx1ZEventDraft(
    string EventName,
    string ScriptId,
    bool Before,
    bool Authoritative);

internal sealed record StudioPlaceableReactivePhysicsDraft(
    BlueprintId BlueprintId,
    Guid ProfileId,
    bool PhysicalAttackEnabled,
    StudioUx1ZEventDraft PhysicalAttackTargeted,
    StudioUx1ZEventDraft PhysicalAttackResolved,
    bool SpellCastEnabled,
    StudioUx1ZEventDraft AbilityTargeted,
    StudioUx1ZEventDraft AbilityImpacted,
    bool SchedulerEnabled,
    StudioUx1ZEventDraft Heartbeat,
    double HeartbeatIntervalSeconds,
    int HeartbeatBudgetPerMinute,
    bool SuspendWhenUnobserved,
    string CustomNamespace,
    StudioUx1ZEventDraft CustomEvent,
    string? OriginalCustomEventName,
    bool PhysicsEnabled,
    PlaceableWorldProfile.BodyMode BodyMode,
    double Mass,
    string ConstraintsText,
    bool Replicated,
    double SleepAfterSeconds,
    bool ConvertToStaticWhenSleeping,
    string Authority,
    bool ExistingProfile,
    PlaceableWorldProfile OriginalProfile);

internal sealed record StudioUx1ZPathPreview(
    bool Succeeded,
    string Summary,
    string DiagnosticCode);

internal sealed record StudioUx1ZPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    StudioUx1ZPathPreview PhysicalAttack,
    StudioUx1ZPathPreview SpellCast,
    StudioUx1ZPathPreview HeartbeatAndCustom,
    StudioUx1ZPathPreview Physics);

internal sealed record StudioUx1ZActionResult(bool Succeeded, string Message);

internal sealed record StudioUx1ZSmokeResult(
    bool Succeeded,
    bool PhysicalAttackPreview,
    bool SpellCastPreview,
    bool HeartbeatAndCustomPreview,
    bool PhysicsPreview,
    bool DryRun,
    bool CommandParity,
    bool InvalidCadenceBlocked,
    bool InvalidBudgetBlocked,
    bool InvalidNamespaceBlocked,
    bool InvalidMassBlocked,
    bool StalePreviewBlocked,
    bool ExplicitSelectionRequired,
    bool SafeProfileCreation,
    bool DormantSchedulerPreserved,
    bool NeighborCapabilitiesPreserved,
    bool NeighborPlaceablePreserved,
    bool UndoRedo,
    bool Persistence,
    bool HumanDiagnostics);
