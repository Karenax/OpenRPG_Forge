using Forge.Application.Build;
using Forge.Domain.Assets;

namespace Forge.Studio.Session;

internal sealed record StudioUx8ICompatibilityDraft(
    Guid ProjectId,
    string ProjectLabel,
    WorkspaceBuildProfileRevision BaseRevision,
    string SourceFingerprint,
    Guid CompatibilityId,
    int SchemaVersion,
    string Provenance,
    string MinimumRuntimeVersion,
    string ProjectFormatVersion,
    bool AutomaticMigrations,
    bool RequiredAtRuntime,
    bool Approved);

internal sealed record StudioUx8IPlatformChoice(
    AssetCatalogItemId AssetId,
    string Label,
    string Detail,
    AssetPackageManifest Manifest);

internal sealed record StudioUx8IPlatformReport(
    AssetTargetPlatform Platform,
    bool Succeeded,
    bool BudgetDeclared,
    bool RequiredRolesCovered,
    bool ContentVerified,
    int SelectedChunks,
    long StoredBytes,
    long? BudgetBytes,
    IReadOnlyList<string> Plan,
    IReadOnlyList<string> Diagnostics);

internal sealed record StudioUx8ICompatibilityPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool RuntimeManifestReady,
    bool MigrationDryRunReady,
    string Message,
    string PatchSummary,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> MigrationPlan);

internal sealed record StudioUx8IActionResult(bool Succeeded, string Message);

internal sealed record StudioUx8IApprovalSnapshot(
    StudioUx8ICompatibilityDraft? Draft,
    WorkspaceBuildProfileRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx8ISmokeResult(
    bool Succeeded,
    bool TwoRoutes,
    bool CompatibilityValidated,
    bool RuntimeRequiredGate,
    bool MigrationDryRun,
    bool PlatformExactOrFallback,
    bool PlatformHashes,
    bool CanonicalDryRun,
    bool CanonicalCommand,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
