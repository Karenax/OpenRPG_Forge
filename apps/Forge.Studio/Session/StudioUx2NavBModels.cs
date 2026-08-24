using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed record StudioUx2NavBAreaChoice(AreaId AreaId, string Label, bool IsEvidence);

internal sealed record StudioUx2NavBDestinationChoice(
    NavigationDestinationKind Kind,
    Guid Id,
    AreaId? AreaId,
    string Label,
    string? ArrivalAnchorKey,
    bool IsProtectedUnknown = false);

internal sealed record StudioUx2NavBBlueprintChoice(BlueprintId BlueprintId, string Label);

internal sealed record StudioUx2NavBAgentDraft(
    NavigationAgentProfileId Id,
    int SchemaVersion,
    string Provenance,
    string Name,
    float Radius,
    float Height,
    bool CanOpenDoors,
    bool CanUseOffMeshLinks,
    bool CanFly);

internal sealed record StudioUx2NavBOffMeshDraft(
    OffMeshNavigationLinkId Id,
    int SchemaVersion,
    string Provenance,
    string Name,
    NavigationLinkKind Kind,
    Vector3Value Start,
    Vector3Value End,
    bool Bidirectional,
    float MaximumAgentRadius);

internal sealed record StudioUx2NavBAnchorDefinitionDraft(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string Slug,
    LocalizedText OriginalLabel,
    string Label,
    AnchorKind Kind,
    AnchorScope Scope,
    IReadOnlyList<string> Tags,
    BlueprintId? BlueprintId,
    string? AssetId,
    bool RequiredAtRuntime);

internal sealed record StudioUx2NavBSpawnDraft(
    string Profile,
    Vector3Value Clearance,
    int Priority,
    string Team,
    float OrientationDegrees,
    Guid? FallbackInstanceId,
    bool RequireNavigation);

internal sealed record StudioUx2NavBPortalDraft(
    AreaId TargetAreaId,
    Guid TargetAnchorInstanceId,
    PortalOrientationPolicy Orientation,
    float FixedYawDegrees,
    PortalVelocityPolicy Velocity,
    bool Preload);

internal sealed record StudioUx2NavBAnchorInstanceDraft(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    Guid DefinitionId,
    TransformValue OriginalTransform,
    Vector3Value Position,
    int DisplayOrder,
    EntityPropertyOverrides Overrides,
    StudioUx2NavBSpawnDraft? Spawn,
    StudioUx2NavBPortalDraft? Portal);

internal sealed record StudioUx2NavBDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    string AreaSlug,
    int AreaWidth,
    int AreaDepth,
    float CellSize,
    IReadOnlyList<StudioUx2NavBAgentDraft> Agents,
    IReadOnlyList<StudioUx2NavBOffMeshDraft> OffMeshLinks,
    IReadOnlyList<StudioUx2NavBAnchorDefinitionDraft> Definitions,
    IReadOnlyList<StudioUx2NavBAnchorInstanceDraft> Instances,
    NavigationAgentProfileId? SelectedAgentId = null,
    OffMeshNavigationLinkId? SelectedOffMeshId = null,
    Guid? SelectedDefinitionId = null,
    Guid? SelectedInstanceId = null);

internal sealed record StudioUx2NavBDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2NavBPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx2NavBDiagnostic> Diagnostics,
    StudioNavigationOverlaySnapshot Overlay,
    int WalkableCellCount,
    int DisplayedCellCount,
    int RebuiltCellCount,
    int ReusedCellCount,
    bool FullRebuild);

internal sealed record StudioUx2NavBActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2NavBApprovalSnapshot(
    StudioUx2NavBDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2NavBEvidenceSourceSnapshot(
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
    StudioUx2NavBApprovalSnapshot Approval);

internal sealed record StudioUx2NavBSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool HumanDestinations,
    bool SurfaceBuilt,
    bool PartialBakePlanned,
    bool InvalidBoundsBlocked,
    bool InvalidDestinationBlocked,
    bool DryRun,
    bool StaleDraftBlocked,
    bool StaleFingerprintBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NavigationNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool SourceUntouched,
    bool HumanText);
