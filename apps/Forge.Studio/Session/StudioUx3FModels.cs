using Forge.Application.Assets;
using Forge.Application.Content;
using Forge.Domain.Assets;

namespace Forge.Studio.Session;

internal sealed record StudioUx3FAssetChoice(
    UnifiedContentBrowserItem Resource,
    AssetCatalogItem? Asset,
    string Label,
    string KindLabel,
    string AvailabilityLabel,
    bool HasLocalSource,
    bool HasManifest,
    bool HasProcessingProfile,
    bool HasExternalEdit);

internal sealed record StudioUx3FBrowserResult(
    bool Succeeded,
    string Message,
    int CatalogCount,
    int UnifiedCount,
    IReadOnlyList<StudioUx3FAssetChoice> Choices);

internal sealed record StudioUx3FProcessingDraft(
    AssetProcessingProfileRevision BaseRevision,
    string SourceFingerprint,
    AssetCatalogItemId AssetId,
    AssetProcessingKind Kind,
    string SourceFormat,
    string CanonicalFormat,
    string LicenseSpdx,
    string ContentSha256,
    string Provenance,
    long MaximumOutputBytes,
    TextureColorSpace TextureColorSpace,
    TextureCompressionProfile TextureCompression,
    int TextureMaximumDimension,
    decimal TextureTexelDensity,
    IReadOnlyDictionary<PbrTextureChannel, string> TextureChannels,
    bool ModelImportRig,
    bool ModelImportAnimations,
    bool ModelGenerateCollisions,
    IReadOnlyList<string> ModelSockets,
    IReadOnlyList<decimal> ModelLodRatios,
    bool Approved);

internal sealed record StudioUx3FExternalDraft(
    AssetExternalEditRevision BaseRevision,
    string SourceFingerprint,
    AssetExternalEditId Id,
    AssetCatalogItemId AssetId,
    AssetPackageManifestId ManifestId,
    string ModificationProvenance,
    string LicenseSpdx,
    string SourceContentSha256,
    string SourceRelativePath,
    string OutputRelativePath,
    AssetExternalTool Tool,
    IReadOnlyList<AssetExternalOperation> Operations,
    bool WatchForChanges,
    IReadOnlyList<AssetTargetPlatform> TargetPlatforms,
    bool Approved);

internal sealed record StudioUx3FDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx3FProcessingPreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool ImportPlanReady,
    bool RuntimeClaimsBounded,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx3FDiagnostic> Diagnostics,
    IReadOnlyList<string> Tasks);

internal sealed record StudioUx3FExternalPreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool WorkerRequestValidated,
    bool WatchSourceResolved,
    bool RuntimeClaimsBounded,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx3FDiagnostic> Diagnostics,
    IReadOnlyList<string> Operations);

internal sealed record StudioUx3FActionResult(bool Succeeded, string Message);

internal sealed record StudioUx3FApprovalSnapshot(
    StudioUx3FProcessingDraft? ProcessingDraft,
    AssetProcessingProfileRevision? ProcessingRevision,
    string? ProcessingSourceFingerprint,
    string? ProcessingDraftFingerprint,
    StudioUx3FExternalDraft? ExternalDraft,
    AssetExternalEditRevision? ExternalRevision,
    string? ExternalSourceFingerprint,
    string? ExternalDraftFingerprint);

internal sealed record StudioUx3FEvidencePlan(
    Forge.Domain.Projects.ProjectDefinition Project,
    string RepositoryRoot,
    string VaultRoot,
    AssetCatalogItem Asset,
    AssetPackageManifest Manifest,
    AssetProcessingProfile Profile,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioUx3FSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool CatalogBrowsed,
    bool TextureAndModelFacets,
    bool UsesRealModuleEvidence,
    bool UsesCanonicalFallbackEvidence,
    bool ProcessingDryRun,
    bool ImportPlan,
    bool ExternalDryRun,
    bool WorkerRequest,
    bool WatchPlan,
    bool StaleProcessingBlocked,
    bool StaleExternalBlocked,
    bool SeparateApprovals,
    bool CanonicalCommands,
    bool UndoRedo,
    bool Persistence,
    bool ProfileNeighborsPreserved,
    bool ExternalNeighborsPreserved,
    bool ProjectSourcePreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
