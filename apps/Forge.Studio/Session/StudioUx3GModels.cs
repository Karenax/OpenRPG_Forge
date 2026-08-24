using Forge.Application.Assets;
using Forge.Domain.Assets;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx3GManifestChoice(
    AssetCatalogItemId AssetId,
    string Label,
    string SourceLabel,
    AssetPackageManifest Manifest,
    bool HasCollision,
    bool HasNavigation,
    bool HasAnimation,
    bool HasVfx,
    bool HasLod);

internal sealed record StudioUx3GManifestDraft(
    AssetPackageManifestRevision BaseRevision,
    string SourceFingerprint,
    int SchemaVersion,
    AssetPackageManifestId Id,
    AssetCatalogItemId AssetId,
    string ModificationProvenance,
    string LicenseSpdx,
    string SourceContentSha256,
    IReadOnlyDictionary<AssetTargetPlatform, long> PlatformBudgets,
    IReadOnlyList<AssetManifestChunk> Chunks,
    AssetCollisionManifest? Collision,
    AssetNavigationManifest? Navigation,
    AssetAnimationManifest? Animation,
    AssetVfxManifest? Vfx,
    AssetLodManifest? Lod,
    AssetModelMetadata? ModelMetadata,
    AssetFontManifest? Font,
    bool Approved);

internal sealed record StudioUx3GDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx3GManifestPreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool PlatformResolutionReady,
    bool VfxPlanReady,
    bool RuntimeClaimsBounded,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx3GDiagnostic> Diagnostics,
    IReadOnlyList<string> ChunkPlan,
    IReadOnlyList<string> AnimationPlan,
    IReadOnlyList<string> VfxPlan);

internal sealed record StudioUx3GActionResult(bool Succeeded, string Message);

internal sealed record StudioUx3GApprovalSnapshot(
    StudioUx3GManifestDraft? Draft,
    AssetPackageManifestRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx3GEvidencePlan(
    ProjectDefinition Project,
    string SourceWorkspaceRoot,
    AssetPackageManifest Manifest,
    AssetPackageManifest Neighbor,
    string AssetLabel,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioUx3GSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealManifestLoaded,
    bool CollisionAndNavigation,
    bool AnimationLibrary,
    bool VfxAndLod,
    bool GovernanceValidated,
    bool PlatformResolution,
    bool VfxPlan,
    bool DryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool HiddenTailsPreserved,
    bool NeighborPreserved,
    bool ProjectSourcePreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
