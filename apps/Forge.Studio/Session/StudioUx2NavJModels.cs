using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed record StudioUx2NavJAreaChoice(AreaId AreaId, string Label, bool IsEvidence);

internal enum StudioUx2NavJItemKind
{
    Room,
    Portal,
    Occluder,
}

internal sealed record StudioUx2NavJRoomDraft(
    Guid Id,
    bool IsNew,
    AreaVisibilityRoomProfile? Original,
    bool NameIsProtected,
    string Name,
    TransformValue Transform,
    bool Exterior,
    double StreamingDistance,
    IReadOnlyDictionary<string, string> Overrides);

internal sealed record StudioUx2NavJPortalDraft(
    Guid Id,
    bool IsNew,
    AreaVisibilityPortalProfile? Original,
    bool NameIsProtected,
    string Name,
    Guid SourceRoomId,
    Guid DestinationRoomId,
    TransformValue Transform,
    AreaVisibilityPortalMode Mode,
    bool ConditionIsProtected,
    string Condition,
    bool TwoWay);

internal sealed record StudioUx2NavJOccluderDraft(
    Guid Id,
    bool IsNew,
    AreaOccluderProfile? Original,
    bool NameIsProtected,
    string Name,
    TransformValue Transform,
    bool Dynamic,
    bool MeshAssetIsProtected,
    string MeshAssetId,
    IReadOnlyDictionary<string, string> Overrides);

internal sealed record StudioUx2NavJDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    AreaAuthoringProfile OriginalAuthoring,
    AreaVisibilityAuthoringProfile OriginalVisibility,
    bool OcclusionCulling,
    bool DistanceStreaming,
    bool CellVisualizer,
    double CellSize,
    double StreamingDistance,
    IReadOnlyList<StudioUx2NavJRoomDraft> Rooms,
    IReadOnlyList<StudioUx2NavJPortalDraft> Portals,
    IReadOnlyList<StudioUx2NavJOccluderDraft> Occluders);

internal sealed record StudioUx2NavJDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2NavJRuntimePreview(
    int Rooms,
    int OpenPortals,
    int ClosedPortals,
    int ConditionalPortals,
    int Occluders,
    int DynamicOccluders,
    bool TypedRuntimeHandle,
    bool PerformanceWithinBudget,
    IReadOnlyList<string> PerformanceDiagnostics,
    string Boundary);

internal sealed record StudioUx2NavJPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx2NavJDiagnostic> Diagnostics,
    StudioNavigationOverlaySnapshot Overlay,
    StudioUx2NavJRuntimePreview? Runtime);

internal sealed record StudioUx2NavJActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2NavJApprovalSnapshot(
    StudioUx2NavJDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2NavJEvidenceSourceSnapshot(
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
    StudioUx2NavJApprovalSnapshot Approval);

internal sealed record StudioUx2NavJSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool HumanLabels,
    bool VisibilityAuthoring,
    bool StableOrder,
    bool UnknownNeighborsPreserved,
    bool LodPreservedByReference,
    bool OtherAuthoringPreserved,
    bool Validation,
    bool RuntimePreview,
    bool DryRun,
    bool StaleRevisionBlocked,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool ProjectNeighborsPreserved,
    bool SourceUntouched,
    bool HumanText);
