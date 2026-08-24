using Forge.Application.Assets;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Assets;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Assets;

namespace Forge.Studio.Session;

internal sealed record StudioUx3IAssetChoice(
    AssetCatalogItemId AssetId,
    string Label,
    string SourceRoot,
    string SourceRelativePath,
    string MediaType,
    string ContentSha256,
    long Bytes,
    string LicenseSpdx,
    string Author,
    string SourceUrl,
    string PermissionEvidence,
    string Provenance,
    bool UsesRealModuleSource);

internal sealed record StudioUx3IGovernanceDraft(
    AssetGovernanceRevision BaseRevision,
    string SourceFingerprint,
    StudioUx3IAssetChoice Asset,
    string LicenseSpdx,
    string Author,
    string SourceUrl,
    string PermissionEvidence,
    string Modifications,
    bool AiGenerated,
    string AiModel,
    string AiPromptPolicySha256,
    long PlatformBudgetBytes,
    AssetOriginKind Origin,
    bool RedistributionAllowed,
    bool CompatibilityIsolated,
    string LegalReviewReference,
    bool ApproveImport,
    string Reviewer,
    bool Approved);

internal sealed record StudioUx3IMediaDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    ModuleRuntimeExtensionProfile SourceExtensions,
    bool Enabled,
    IReadOnlyList<string> MediaKinds,
    IReadOnlyList<string> Providers,
    int MaximumJobs,
    long MaximumBytesPerJob,
    bool ModuleIndependentFallback,
    string JobKind,
    string JobProvider,
    string JobLicenseSpdx,
    string JobRightsProvenance,
    long JobExpectedBytes,
    bool JobProviderAvailable,
    bool Approved);

internal sealed record StudioUx3IDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx3IGovernancePreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool QuarantineDryRunSucceeded,
    bool DistributionReady,
    bool RuntimeClaimsBounded,
    string Message,
    string Patch,
    string Inverse,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx3IDiagnostic> Diagnostics,
    string ScannerLabel,
    string QuarantinePlan);

internal sealed record StudioUx3IMediaPreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool JobPlanSucceeded,
    bool RuntimeClaimsBounded,
    string Message,
    string Patch,
    string Inverse,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx3IDiagnostic> Diagnostics,
    IReadOnlyList<string> Plan);

internal sealed record StudioUx3IActionResult(bool Succeeded, string Message);

internal sealed record StudioUx3IGovernanceApprovalSnapshot(
    StudioUx3IGovernanceDraft? Draft,
    AssetGovernanceRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx3IMediaApprovalSnapshot(
    StudioUx3IMediaDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx3IQuarantineReceipt(
    AssetQuarantineStageRequest Request,
    string WorkspaceRoot,
    string? QuarantineRelativePath,
    bool Approved,
    bool Undone);

internal sealed record StudioUx3IEvidencePlan(
    ProjectDefinition Project,
    StudioUx3IAssetChoice Asset,
    AssetGovernanceRecord Neighbor,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioUx3ISmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool Provenance,
    bool Quarantine,
    bool HumanDecision,
    bool MediaPolicy,
    bool MediaPlan,
    bool CanonicalGovernanceCommand,
    bool CanonicalProjectCommand,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool UndoRedo,
    bool Persistence,
    bool NeighborPreserved,
    bool ProjectPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryReleased);
