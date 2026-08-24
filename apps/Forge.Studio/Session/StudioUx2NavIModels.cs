using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed record StudioUx2NavIAreaChoice(AreaId AreaId, string Label, bool IsEvidence);

internal sealed record StudioUx2NavIRouteChoice(Guid RouteId, string Label, bool IsEvidence);

internal enum StudioUx2NavIVolumeKind
{
    Obstacle,
    Cost,
}

internal sealed record StudioUx2NavIVolumeDraft(
    Guid Id,
    bool IsNew,
    StudioUx2NavIVolumeKind Kind,
    NavigationVolumeDefinition? Original,
    bool NameIsProtected,
    string Name,
    Vector3Value Center,
    Vector3Value Size,
    double CostMultiplier);

internal sealed record StudioUx2NavINodeDraft(
    Guid Id,
    bool IsNew,
    FlightRouteNode? Original,
    bool NameIsProtected,
    string Name,
    Vector3Value Position,
    double Speed,
    double BankDegrees);

internal sealed record StudioUx2NavIRouteDraft(
    Guid RouteId,
    bool IsNew,
    FlightRouteDefinition? Original,
    bool NameIsProtected,
    string Name,
    FlightRouteCurve Curve,
    FlightObstaclePolicy ObstaclePolicy,
    bool Closed,
    bool DebugVisible,
    IReadOnlyList<StudioUx2NavINodeDraft> Nodes);

internal sealed record StudioUx2NavIDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    AreaWorldProfile OriginalWorld,
    bool Enabled,
    VolumetricNavigationMode Modes,
    double CellSize,
    Vector3Value Center,
    Vector3Value Size,
    double AvoidanceRadius,
    bool DebugVisible,
    IReadOnlyList<StudioUx2NavIVolumeDraft> Volumes,
    StudioUx2NavIRouteDraft? Route);

internal sealed record StudioUx2NavIDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2NavIRuntimePreview(
    int GridCells,
    int BlockedCells,
    int CostCells,
    int PathPoints,
    string SteeringDiagnostic,
    string FlightDiagnostic,
    Vector3Value FlightPosition,
    Vector3Value FlightForward,
    double FlightSpeed,
    double FlightBankDegrees,
    string Boundary);

internal sealed record StudioUx2NavIPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx2NavIDiagnostic> Diagnostics,
    StudioNavigationOverlaySnapshot Overlay,
    StudioUx2NavIRuntimePreview? Runtime);

internal sealed record StudioUx2NavIActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2NavIApprovalSnapshot(
    StudioUx2NavIDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2NavIEvidenceSourceSnapshot(
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
    StudioUx2NavIApprovalSnapshot Approval);

internal sealed record StudioUx2NavISmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool HumanLabels,
    bool VolumetricAuthoring,
    bool FlightRouteAuthoring,
    bool StableOrder,
    bool Validation,
    bool RuntimePreview,
    bool DryRun,
    bool StaleRevisionBlocked,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool WorldNeighborsPreserved,
    bool RouteNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool SourceUntouched,
    bool HumanText);
