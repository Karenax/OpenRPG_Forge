using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed record StudioUx2NavDAreaChoice(AreaId AreaId, string Label, bool IsEvidence);

internal sealed record StudioUx2NavDRouteChoice(Guid RouteId, string Label, bool IsEvidence);

internal sealed record StudioUx2NavDWaypointChoice(Guid WaypointId, string Label, bool HasArrival,
    bool IsEvidence);

internal sealed record StudioUx2NavDArrivalTargetChoice(string Value, string Label, bool IsProtected);

internal sealed record StudioUx2NavDPointDraft(
    Guid Id,
    Vector3Value Position,
    double WaitSeconds,
    double FacingDegrees,
    double Speed,
    string Action,
    string BranchCondition,
    double Weight);

internal sealed record StudioUx2NavDRouteDraft(
    Guid RouteId,
    bool IsNew,
    PatrolRoute? Original,
    PatrolTraversalPolicy Traversal,
    IReadOnlyList<StudioUx2NavDPointDraft> Points,
    string ScriptHandler);

internal sealed record StudioUx2NavDArrivalDraft(
    Guid WaypointId,
    WaypointProfile OriginalWaypoint,
    ArrivalBehavior? Original,
    bool HasArrival,
    ArrivalFacingMode Facing,
    double DirectionDegrees,
    string TargetId,
    bool TargetIsProtected,
    string Animation,
    double WaitSeconds,
    string Interaction,
    string Callback);

internal sealed record StudioUx2NavDDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    int AreaWidth,
    int AreaDepth,
    StudioUx2NavDRouteDraft? Route,
    StudioUx2NavDArrivalDraft? Arrival);

internal sealed record StudioUx2NavDDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2NavDRuntimeStep(
    int FromOrder,
    int ToOrder,
    int Direction,
    double Distance,
    double TravelSeconds,
    string Action,
    string BranchCondition);

internal sealed record StudioUx2NavDPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx2NavDDiagnostic> Diagnostics,
    StudioNavigationOverlaySnapshot Overlay,
    IReadOnlyList<StudioUx2NavDRuntimeStep> RuntimeSteps,
    bool ArrivalAuthorized,
    bool ArrivalDeniedWithoutActions);

internal sealed record StudioUx2NavDActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2NavDApprovalSnapshot(
    StudioUx2NavDDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2NavDEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    IReadOnlyList<EntityId> SelectedEntityIds,
    EntityId? SelectedEntityId,
    StudioUx2NavBApprovalSnapshot NavigationApproval,
    StudioUx2NavCApprovalSnapshot WaypointApproval,
    StudioUx2NavDApprovalSnapshot PatrolApproval);

internal sealed record StudioUx2NavDSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool HumanLabels,
    bool RouteCreation,
    bool StablePointOrder,
    bool RouteValidation,
    bool ArrivalValidation,
    bool TraversalPreview,
    bool ArrivalAuthority,
    bool DryRun,
    bool StaleRevisionBlocked,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool RouteNeighborsPreserved,
    bool WaypointNeighborsPreserved,
    bool WaypointTailsPreserved,
    bool NavigationNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool SourceUntouched,
    bool HumanText);
