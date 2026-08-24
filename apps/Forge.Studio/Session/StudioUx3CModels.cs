using Forge.Application.Blueprints;
using Forge.Application.Projects;
using Forge.Domain.Authoring;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Studio.Session;

internal sealed record StudioUx3CBlueprintChoice(
    Guid Id,
    string Label,
    BlueprintKind Kind,
    BlueprintPaletteOrigin Origin,
    bool IsFavorite,
    bool IsRecent,
    int RecentRank,
    bool IsReadOnly,
    string Kit,
    string License,
    IReadOnlyList<string> Styles,
    IReadOnlyList<string> Compatibility,
    IReadOnlyList<string> Tags,
    string? PreviewRelativePath,
    string? PreviewAbsolutePath,
    bool PreviewFileExists,
    int UsageCount);

internal sealed record StudioUx3CDiscoveryDraft(
    string SourceProjectFingerprint,
    string SourceWorkspaceFingerprint,
    Guid BlueprintId,
    BlueprintPaletteView View,
    string Query,
    IReadOnlyList<BlueprintKind> Kinds,
    IReadOnlyList<string> Kits,
    IReadOnlyList<string> Styles,
    IReadOnlyList<string> Licenses,
    IReadOnlyList<string> Compatibility,
    IReadOnlyList<string> Tags,
    bool Favorite,
    bool Approved);

internal sealed record StudioUx3CDropDraft(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    Guid BlueprintId,
    Guid AreaId,
    Vector3Value Position,
    BlueprintDropTargetKind TargetKind,
    string TargetPath,
    bool Approved);

internal sealed record StudioUx3CDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx3CDiscoveryPreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool RuntimeClaimsBounded,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx3CDiagnostic> Diagnostics,
    EditorWorkspaceProfile? Candidate);

internal sealed record StudioUx3CDropPreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool RuntimeClaimsBounded,
    string Message,
    string Code,
    string Path,
    Vector3Value? CorrectedPosition,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx3CDiagnostic> Diagnostics);

internal sealed record StudioUx3CActionResult(bool Succeeded, string Message);

internal sealed record StudioUx3CApprovalSnapshot(
    StudioUx3CDiscoveryDraft? DiscoveryDraft,
    string? DiscoveryProjectFingerprint,
    string? DiscoveryWorkspaceFingerprint,
    string? DiscoveryDraftFingerprint,
    StudioUx3CDropDraft? DropDraft,
    ProjectRevision? DropRevision,
    string? DropProjectFingerprint,
    string? DropDraftFingerprint);

internal sealed record StudioUx3CEvidenceProject(
    ProjectDefinition Project,
    Guid BlueprintId,
    Guid AreaId,
    Guid ExistingEntityId,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioUx3CSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool UsesRealModuleEvidence,
    bool UsesCanonicalFallbackEvidence,
    bool Favorites,
    bool Recents,
    bool AdvancedSearch,
    bool ThumbnailMetadata,
    bool ThumbnailLoaded,
    bool HonestFallback,
    bool DragPayload,
    bool DropPlan,
    bool InvalidDropBlocked,
    bool DryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool SeparateApprovals,
    bool CanonicalCommands,
    bool UndoRedo,
    bool Persistence,
    bool BlueprintNeighborsPreserved,
    bool OtherAreasPreserved,
    bool ExistingEntitiesPreserved,
    bool TargetAreaTailsPreserved,
    bool AreaNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool WorkspaceNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
