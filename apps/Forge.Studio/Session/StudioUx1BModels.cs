using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Domain.Entities;

namespace Forge.Studio.Session;

internal enum StudioAreaResizeLossPolicy
{
    RepositionInside,
    Remove,
}

internal sealed record StudioAreaResizeDraft(
    AreaId SourceAreaId,
    int Width,
    int Depth,
    AreaResizeAnchor Anchor,
    StudioAreaResizeLossPolicy LossPolicy);

internal sealed record StudioAreaResizePreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    int KeptCount = 0,
    int RemovedCount = 0,
    int RepositionedCount = 0,
    IReadOnlyList<string>? AtRiskObjects = null);

internal sealed record StudioAreaCloneDraft(
    AreaId SourceAreaId,
    AreaId DestinationAreaId,
    string DisplayName,
    AreaCloneMode Mode,
    ExternalPortalPolicy ExternalPortalPolicy,
    IReadOnlyList<EntityIdRemap> EntityIdMap,
    IReadOnlyList<PortalIdRemap> PortalIdMap);

internal sealed record StudioAreaClonePreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    AreaDefinition? Candidate = null);

internal sealed record StudioAreaDocumentOperationResult(
    bool Succeeded,
    string Message,
    AreaId? AreaId = null);

internal sealed record StudioUx1BSmokeResult(
    bool Succeeded,
    bool ResizePreview,
    bool RepositionLosses,
    bool RemovalPreview,
    bool ResizeDryRun,
    bool ResizeUndoRedo,
    bool ResizePersistence,
    bool NeighborPreserved,
    bool ClonePreview,
    bool FullClone,
    bool TemplateClone,
    bool PortalRemap,
    bool NewIdentities,
    bool CloneUndoRedo,
    bool IndependentDocument,
    bool ClonePersistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);
