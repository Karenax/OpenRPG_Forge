using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal enum StudioUx2BTileOperation
{
    Place,
    Group,
    Erase,
    Replace,
    Transform,
    AutoFit,
}

internal sealed record StudioUx2BAreaChoice(AreaId AreaId, string Label, bool HasTileKit);

internal sealed record StudioUx2BDefinitionChoice(Guid DefinitionId, string Label);

internal sealed record StudioUx2BPlacementChoice(
    Guid PlacementId,
    EntityId EntityId,
    string Label,
    int GridX,
    int GridZ);

internal sealed record StudioUx2BTileDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    StudioUx2BTileOperation Operation,
    IReadOnlyList<Guid> SelectedPlacementIds,
    Guid PlacementId,
    EntityId EntityId,
    Guid DefinitionId,
    int GridX,
    int GridZ,
    float RotationDegrees,
    ModularTileMirrorMode Mirror,
    string VariantKey,
    Guid GroupId,
    Guid EditableGroupId,
    string GroupName,
    bool PreserveAttachedObjects);

internal sealed record StudioUx2BTilePreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    IReadOnlyList<string> Changes,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Notices,
    int PlacementCount,
    int GroupCount,
    int PreservedAttachmentCount);

internal sealed record StudioUx2BActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2BApprovalSnapshot(
    StudioUx2BTileDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2BSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool CompleteKit,
    bool DirectGridInput,
    bool Placement,
    bool Group,
    bool EraseReplace,
    bool Transform,
    bool AutoFit,
    bool InvalidValuesBlocked,
    bool DryRun,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighboursPreserved,
    bool HumanText);
