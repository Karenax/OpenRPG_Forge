using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx3DBlueprintChoice(
    Guid Id,
    string Label,
    bool IsReadOnly,
    bool IsUsed,
    IReadOnlyList<string> Dependencies,
    long PlatformByteBudget,
    string Source,
    string Author,
    string LicenseSpdx,
    string ContentHash,
    string PermissionEvidence,
    bool IsAiGenerated,
    IReadOnlyList<string> Modifications);

internal sealed record StudioUx3DDependencyDraft(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    Guid BlueprintId,
    IReadOnlyList<string> Dependencies,
    long PlatformByteBudget,
    bool Approved);

internal sealed record StudioUx3DProvenanceDraft(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    Guid BlueprintId,
    string Source,
    string Author,
    string LicenseSpdx,
    string ContentHash,
    string PermissionEvidence,
    bool IsAiGenerated,
    IReadOnlyList<string> Modifications,
    bool Approved);

internal sealed record StudioUx3DDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx3DDependencyPreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool BuildGraphResolved,
    bool RuntimeClaimsBounded,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx3DDiagnostic> Diagnostics,
    int GraphNodeCount,
    IReadOnlyList<string> AutomaticPackages);

internal sealed record StudioUx3DProvenancePreview(
    bool Succeeded,
    bool CanApply,
    bool DryRunSucceeded,
    bool DistributionReady,
    bool RuntimeClaimsBounded,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx3DDiagnostic> Diagnostics);

internal sealed record StudioUx3DActionResult(bool Succeeded, string Message);

internal sealed record StudioUx3DApprovalSnapshot(
    StudioUx3DDependencyDraft? DependencyDraft,
    ProjectRevision? DependencyRevision,
    string? DependencyProjectFingerprint,
    string? DependencyDraftFingerprint,
    StudioUx3DProvenanceDraft? ProvenanceDraft,
    ProjectRevision? ProvenanceRevision,
    string? ProvenanceProjectFingerprint,
    string? ProvenanceDraftFingerprint);

internal sealed record StudioUx3DEvidenceProject(
    ProjectDefinition Project,
    Guid EditableBlueprintId,
    Guid SupportBlueprintId,
    string SupportManifestPath,
    string AssetDependencyPath,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioUx3DSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool UsesRealModuleEvidence,
    bool UsesCanonicalFallbackEvidence,
    bool MissingDependencyBlocked,
    bool TransitiveClosure,
    bool BuildGraphResolved,
    bool InvalidProvenanceBlocked,
    bool DistributionReady,
    bool DryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool SeparateApprovals,
    bool CanonicalCommands,
    bool UndoRedo,
    bool Persistence,
    bool AuthoringTailsPreserved,
    bool BlueprintTailsPreserved,
    bool BlueprintNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
