using Forge.Application.Projects;
using Forge.Domain.Content;

namespace Forge.Studio.Session;

internal sealed record StudioUx8GPackageDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    Guid CatalogId,
    int CatalogSchemaVersion,
    string CatalogProvenance,
    ContentConflictPolicy ConflictPolicy,
    IReadOnlyList<ContentPackageDefinition> Packages,
    bool Approved);

internal sealed record StudioUx8GDiagnostic(
    string Code,
    string Path,
    string Message);

internal sealed record StudioUx8GPackagePlan(
    string PackageLabel,
    string Version,
    int Priority,
    int DependencyCount,
    int SourceCount,
    int AssetCount);

internal sealed record StudioUx8GPackagePreview(
    bool Succeeded,
    bool Changed,
    bool CanonicalDryRun,
    bool CompilationReady,
    bool CanApply,
    string CandidateFingerprint,
    string Patch,
    string Inverse,
    IReadOnlyList<StudioUx8GPackagePlan> Packages,
    IReadOnlyList<string> DependencyPlan,
    IReadOnlyList<string> LockPlan,
    IReadOnlyList<string> ResourcePlan,
    IReadOnlyList<string> ArtifactPlan,
    IReadOnlyList<StudioUx8GDiagnostic> Diagnostics,
    string Message);

internal sealed record StudioUx8GApprovalSnapshot(
    StudioUx8GPackageDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioUx8GActionResult(
    bool Succeeded,
    string Message);

internal sealed record StudioUx8GSmokeResult(
    bool Succeeded,
    bool FiveRoutes,
    bool VersionedPackages,
    bool PriorityResolution,
    bool Dependencies,
    bool Compilation,
    bool CanonicalDryRun,
    bool CanonicalCommand,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool ResourcesReleased,
    bool SourceUnchanged,
    string Message);
