using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.RuntimeExtensions;
using Forge.Domain.Spatial;

namespace Forge.Studio.Session;

internal sealed record StudioZoneEntityChoice(EntityId Id, string Label);

internal sealed record StudioZoneScaleDraft(
    AreaId AreaId,
    IReadOnlyList<EntityId> EntityIds,
    Vector3Value Scale,
    bool Multiply);

internal sealed record StudioZoneScalePreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    int ObjectCount = 0);

internal sealed record StudioDynamicAreaDraft(
    AreaId PreviewAreaId,
    bool Enabled,
    int MaximumInstances,
    int LifetimeSeconds,
    bool ResolveDependencies,
    int RequestedInstances);

internal sealed record StudioDynamicAreaPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    int RuntimeLimit = 0);

internal sealed record StudioRuntimeCopyDraft(
    AreaId AreaId,
    EntityId SourceEntityId,
    int MaximumCopiesPerTransaction,
    int RequestedCopies,
    bool RequireServerAuthority,
    bool HasServerAuthority,
    bool CopyOwnership,
    RuntimeReferenceCopyMode ReferenceMode,
    IReadOnlyList<string> StateComponents,
    Vector3Value PositionOffset);

internal sealed record StudioRuntimeCopyPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    IReadOnlyList<EntityId>? DestinationIds = null,
    bool OwnershipCopied = false,
    bool ReferencesRemapped = false,
    bool StateFiltered = false);

internal sealed record StudioUx1WActionResult(bool Succeeded, string Message);

internal sealed record StudioUx1WSmokeResult(
    bool Succeeded,
    bool ScaleMultiEdit,
    bool ScaleConstraints,
    bool ScaleDryRun,
    bool DynamicAreaPreview,
    bool DynamicQuota,
    bool RuntimeCopyPreview,
    bool FreshIdentities,
    bool OwnershipReferencesState,
    bool CanonicalCommands,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool InvalidBlocked,
    bool HumanDiagnostics);
