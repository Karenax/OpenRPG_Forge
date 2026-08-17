using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.RuntimePolicies;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed record StudioUx2NavKAreaChoice(
    AreaId AreaId,
    string Label,
    bool HasRoutes,
    bool IsEvidence);

internal sealed record StudioUx2NavKRouteChoice(
    Guid RouteId,
    string Label,
    bool OwnerIsProtected,
    bool IsEvidence);

internal sealed record StudioUx2NavKPortalChoice(
    PortalId PortalId,
    AreaId FromAreaId,
    AreaId ToAreaId,
    string Label,
    bool IsProtected = false);

internal sealed record StudioUx2NavKWaypointChoice(
    Guid WaypointId,
    AreaId AreaId,
    string Label,
    bool IsProtected = false);

internal sealed record StudioUx2NavKSegmentDraft(
    Guid Id,
    bool IsNew,
    CrossAreaPatrolSegment? Original,
    AreaId FromAreaId,
    AreaId ToAreaId,
    PortalId PortalId,
    Guid ResumeWaypointId,
    bool ResumeWaypointIsProtected,
    bool PrefetchDestination,
    PatrolOwnershipAuthority Authority);

internal sealed record StudioUx2NavKDraft(
    AreaId OwnerAreaId,
    Guid RouteId,
    bool IsNew,
    bool Remove,
    CrossAreaPatrolRoute? Original,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaLabel,
    bool OwnerIdIsProtected,
    string OwnerId,
    bool Loop,
    IReadOnlyList<StudioUx2NavKSegmentDraft> Segments);

internal sealed record StudioUx2NavKDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2NavKRuntimeStep(
    int Order,
    string SourceArea,
    string DestinationArea,
    string Portal,
    string ResumeWaypoint,
    bool WaitsForStreaming,
    bool ResumesAfterLoading,
    bool PrefetchDestination,
    string Authority);

internal sealed record StudioUx2NavKRuntimePreview(
    IReadOnlyList<StudioUx2NavKRuntimeStep> Steps,
    bool OwnershipGuarded,
    bool CompletesOrLoops,
    string Boundary);

internal sealed record StudioUx2NavKPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx2NavKDiagnostic> Diagnostics,
    StudioNavigationOverlaySnapshot Overlay,
    StudioUx2NavKRuntimePreview? Runtime);

internal sealed record StudioUx2NavKActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2NavKApprovalSnapshot(
    StudioUx2NavKDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2NavKEvidenceSourceSnapshot(
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
    StudioUx2NavKApprovalSnapshot Approval);

internal sealed record StudioUx2NavKSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool HumanLabels,
    bool CreateEditRemove,
    bool SegmentOrder,
    bool PortalWaypointResolution,
    bool RuntimePreview,
    bool RuntimeBoundary,
    bool Validation,
    bool DryRun,
    bool StaleRevisionBlocked,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool ApprovalFingerprintBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool ExperienceTailsPreserved,
    bool NeighborRoutesPreserved,
    bool NavigationLotsPreserved,
    bool ProjectNeighborsPreserved,
    bool SourceUntouched,
    bool HumanText);
