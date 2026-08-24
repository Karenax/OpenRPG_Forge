using Forge.Application.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx8LSyncDraft(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    bool Resumable,
    bool DeduplicateByHash,
    bool VerifyContent,
    bool RequireTrustedSigner,
    string StorageDirectory,
    long MaximumCacheBytes,
    IReadOnlyList<string> AllowedLicenses,
    IReadOnlyList<string> TrustedSignerFingerprints,
    string Channel,
    string Compatibility,
    bool RequireSignature,
    bool RequireChangeNotes,
    string RepositoryRelativePath,
    string ResourceGlob,
    bool Consent);

internal sealed record StudioUx8LSyncPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool ManifestValid,
    bool SyncPlanValid,
    bool ScopeConfined,
    string Message,
    string ManifestFingerprint,
    long TotalBytes,
    long ReusedBytes,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Resources,
    IReadOnlyList<string> Diagnostics);

internal sealed record StudioUx8LActionResult(bool Succeeded, string Message);

internal sealed record StudioUx8LApprovalSnapshot(
    StudioUx8LSyncDraft? Draft,
    string? DraftFingerprint);

internal sealed record StudioUx8LSmokeResult(
    bool Succeeded,
    bool TwoRoutes,
    bool ExplicitSelection,
    bool ConsentRequired,
    bool ScopeConfined,
    bool ManifestValidated,
    bool TrustAndLicenseBlocked,
    bool TamperBlocked,
    bool ResumeAndReuse,
    bool CanonicalDryRun,
    bool CanonicalCommand,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
