using Forge.Domain.Areas;
using Forge.Domain.Entities;

namespace Forge.Studio.Session;

internal sealed record StudioAreaLayerEntryDraft(
    Guid Id,
    string Name,
    Guid? ParentId,
    bool Visible,
    bool Locked,
    string Owner,
    IReadOnlyList<EntityId> EntityIds,
    bool ExportEnabled,
    string OriginalProvenance);

internal sealed record StudioAreaLayersDraft(
    AreaId AreaId,
    IReadOnlyList<StudioAreaLayerEntryDraft> Layers,
    bool RequireRuntimeMetadata);

internal sealed record StudioAreaLayerEntityOption(EntityId Id, string DisplayName);

internal sealed record StudioAreaLayerCatalog(IReadOnlyList<StudioAreaLayerEntityOption> Entities);

internal sealed record StudioAreaLayerEditResult(
    bool Succeeded,
    string Message,
    StudioAreaLayersDraft Draft,
    Guid? SelectedLayerId = null);

internal sealed record StudioAreaLayersPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    Guid? SelectedLayerId = null,
    IReadOnlyList<AreaLayerEffectiveState>? Hierarchy = null,
    IReadOnlyList<EntityId>? SelectedEntityIds = null,
    IReadOnlyList<string>? SelectedEntityLabels = null,
    IReadOnlyList<string>? ExportLayerLabels = null,
    IReadOnlyList<string>? ExportEntityLabels = null,
    IReadOnlyList<string>? ExportBlueprintLabels = null);

internal sealed record StudioAreaLayersResult(bool Succeeded, string Message);

internal sealed record StudioUx1DSmokeResult(
    bool Succeeded,
    bool Preview,
    bool DryRun,
    bool NestedHierarchy,
    bool VisibilityAndLocking,
    bool Ownership,
    bool GroupSelection,
    bool PartialExport,
    bool DirectReparent,
    bool EntityAssignment,
    bool OtherAuthoringPreserved,
    bool NeighborPreserved,
    bool RuntimeSemantics,
    bool UndoRedo,
    bool Persistence,
    bool InvalidBlocked,
    bool HumanDiagnostics);
