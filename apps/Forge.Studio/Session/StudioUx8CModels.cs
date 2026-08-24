using Forge.Application.Build;
using Forge.Domain.Build;
using Forge.Domain.Content;

namespace Forge.Studio.Session;

internal sealed record StudioUx8CGovernanceDraft(
    Guid ProjectId,
    string ModuleName,
    WorkspaceBuildProfileRevision BaseRevision,
    string SourceFingerprint,
    string BranchPrefix,
    string OwnershipManifestPath,
    IReadOnlyList<string> IntegrationChecks,
    string WorkflowPath,
    bool ValidateSchemas,
    bool BuildManagedCode,
    bool BuildNativeCode,
    bool CompileForgeScript,
    bool RunTests,
    bool PackReferenceModule,
    bool VerifyLicenses,
    string FixtureCatalogPath,
    IReadOnlyList<ReferenceFixtureDefinition> Fixtures);

internal sealed record StudioUx8CGovernancePreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string Patch,
    IReadOnlyList<string> Diagnostics,
    bool WorktreeReady,
    bool CiReady,
    bool FixturesReady,
    bool RepositoryEvidenceAvailable,
    string EvidenceSummary);

internal sealed record StudioUx8CMergePreview(
    bool Succeeded,
    ContentMergePolicy Policy,
    string ModuleName,
    string ElementName,
    int TableConflicts,
    int GraphConflicts,
    int MergedRows,
    int MergedNodes,
    int MergedEdges,
    bool PreservedBoth,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> ResultLines,
    string Message);

internal sealed record StudioUx8CActionResult(bool Succeeded, string Message);

internal sealed record StudioUx8CApprovalSnapshot(
    StudioUx8CGovernanceDraft? Draft,
    WorkspaceBuildProfileRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx8CSmokeResult(
    bool Succeeded,
    bool FourRoutes,
    bool MergeConflict,
    bool PreserveBoth,
    bool WorktreePolicy,
    bool CiPolicy,
    bool FixtureCatalog,
    bool DryRun,
    bool StaleGuard,
    bool ApplyUndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased,
    string Message);
