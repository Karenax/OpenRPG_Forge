using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed record StudioUx2NavLAreaChoice(
    AreaId AreaId,
    string Label,
    bool HasRoutes,
    bool IsEvidence);

internal sealed record StudioUx2NavLRouteChoice(
    Guid RouteId,
    string Label,
    bool IsEditable,
    string? ReadOnlyReason,
    bool IsEvidence);

internal sealed record StudioUx2NavLPointDraft(
    Guid Id,
    bool IsNew,
    VehicleRouteProfile.RoutePoint? Original,
    double PositionX,
    double PositionY,
    double PositionZ,
    double TangentDegrees,
    double Width);

internal sealed record StudioUx2NavLLaneDraft(
    bool IsNew,
    VehicleRouteProfile.Lane? Original,
    bool IdIsProtected,
    string Id,
    double LateralOffset,
    double SpeedLimit,
    bool DirectionIsProtected,
    string Direction);

internal sealed record StudioUx2NavLStopDraft(
    Guid Id,
    bool IsNew,
    VehicleRouteProfile.Stop? Original,
    Guid RoutePointId,
    double WaitSeconds,
    bool ServiceIdIsProtected,
    string ServiceId);

internal sealed record StudioUx2NavLDraft(
    AreaId AreaId,
    Guid RouteId,
    bool IsNew,
    bool Remove,
    VehicleRouteProfile? Original,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaLabel,
    IReadOnlyList<StudioUx2NavLPointDraft> Points,
    IReadOnlyList<StudioUx2NavLLaneDraft> Lanes,
    IReadOnlyList<StudioUx2NavLStopDraft> Stops,
    double DefaultSpeedLimit,
    bool AllowOvertaking,
    string NetworkSyncMode);

internal sealed record StudioUx2NavLDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2NavLRuntimeSample(
    int Percent,
    double PositionX,
    double PositionY,
    double PositionZ,
    double SpeedLimit,
    bool AtStop,
    double WaitSeconds);

internal sealed record StudioUx2NavLRuntimePreview(
    IReadOnlyList<StudioUx2NavLRuntimeSample> Samples,
    string Boundary);

internal sealed record StudioUx2NavLPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx2NavLDiagnostic> Diagnostics,
    StudioNavigationOverlaySnapshot Overlay,
    StudioUx2NavLRuntimePreview? Runtime);

internal sealed record StudioUx2NavLActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2NavLApprovalSnapshot(
    StudioUx2NavLDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2NavLEvidenceSourceSnapshot(
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
    StudioUx2NavLApprovalSnapshot Approval);

internal sealed record StudioUx2NavLSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool HumanLabels,
    bool FullSurface,
    bool CreateEditRemove,
    bool OrderedCollections,
    bool Validation,
    bool NoSilentClamp,
    bool DryRun,
    bool StaleRevisionBlocked,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool ApprovalFingerprintBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool RuntimeSample,
    bool RuntimeBoundary,
    bool WorldFeatureNeighborsPreserved,
    bool VehicleNeighborPreserved,
    bool NavigationLotsPreserved,
    bool ProjectNeighborsPreserved,
    bool SourceUntouched,
    bool HumanText);
