using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed record StudioUx2NavEAreaChoice(AreaId AreaId, string Label, bool IsEvidence);

internal sealed record StudioUx2NavELocatorChoice(Guid LocatorId, string Label, bool IsEvidence);

internal sealed record StudioUx2NavECollectionChoice(Guid CollectionId, string Label, bool IsEvidence);

internal sealed record StudioUx2NavEBlueprintChoice(BlueprintId? BlueprintId, string Label,
    bool IsProtected = false);

internal sealed record StudioUx2NavEOverrideDraft(string Key, string Value, bool IsProtected = false);

internal sealed record StudioUx2NavELocatorDraft(
    Guid LocatorId,
    bool IsNew,
    GenericLocator? Original,
    string Name,
    Vector3Value Position,
    LocatorPurpose Purpose,
    IReadOnlyList<string> Tags,
    string AssetId,
    BlueprintId? BlueprintId,
    bool BlueprintIsProtected,
    IReadOnlyList<StudioUx2NavEOverrideDraft> ExplicitOverrides,
    bool RequiredAtRuntime);

internal sealed record StudioUx2NavECollectionDraft(
    Guid CollectionId,
    bool IsNew,
    AnchorCollection? Original,
    string Name,
    string Query,
    IReadOnlyList<Guid> LocatorIds,
    LocatorPurpose? Purpose,
    IReadOnlyList<string> RequiredTags,
    LocatorSelectionMode Selection,
    bool HasOrigin,
    Vector3Value Origin,
    int Seed,
    bool RequiredAtRuntime);

internal sealed record StudioUx2NavEVisualizationDraft(
    RouteVisualizationProfile Original,
    bool ShowSegments,
    bool ShowDirection,
    bool ShowCosts,
    bool ShowActions,
    bool ShowBrokenLinks,
    bool ShowNavigationProjection,
    bool ShowTiming,
    double CostPerMeter,
    bool RequiredAtRuntime);

internal sealed record StudioUx2NavEDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    int AreaWidth,
    int AreaDepth,
    StudioUx2NavELocatorDraft? Locator,
    StudioUx2NavECollectionDraft? Collection,
    StudioUx2NavEVisualizationDraft Visualization);

internal sealed record StudioUx2NavEDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2NavEResolvedLocator(string Label, LocatorPurpose Purpose,
    Vector3Value Position);

internal sealed record StudioUx2NavERouteDiagnostic(
    string RouteLabel,
    int FromOrder,
    int ToOrder,
    bool Reverse,
    double Distance,
    double Cost,
    double TravelSeconds,
    string Action,
    bool Broken,
    bool NavigationProjected);

internal sealed record StudioUx2NavEPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx2NavEDiagnostic> Diagnostics,
    StudioNavigationOverlaySnapshot Overlay,
    IReadOnlyList<StudioUx2NavEResolvedLocator> ResolvedLocators,
    IReadOnlyList<StudioUx2NavERouteDiagnostic> RouteDiagnostics,
    bool ProjectionAvailable,
    int ProjectedPointCount);

internal sealed record StudioUx2NavEActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2NavEApprovalSnapshot(
    StudioUx2NavEDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2NavEEvidenceSourceSnapshot(
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
    StudioUx2NavDApprovalSnapshot PatrolApproval,
    StudioUx2NavEApprovalSnapshot LocatorApproval);

internal sealed record StudioUx2NavESmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool HumanLabels,
    bool LocatorCreation,
    bool LocatorValidation,
    bool BlueprintAndOverrides,
    bool ArrivalPreserved,
    bool CollectionCreation,
    bool StableMemberOrder,
    bool CollectionModes,
    bool CollectionValidation,
    bool RouteVisualization,
    bool RealProjection,
    bool DryRun,
    bool StaleRevisionBlocked,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool WaypointNeighborsPreserved,
    bool LocatorNeighborsPreserved,
    bool CollectionNeighborsPreserved,
    bool NavigationNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool SourceUntouched,
    bool HumanText);
