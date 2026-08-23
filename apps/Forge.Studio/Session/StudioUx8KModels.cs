using Forge.Application.Projects;
using Forge.Domain.Content;
using Forge.ModuleFormat.Content;

namespace Forge.Studio.Session;

internal sealed record StudioUx8KBulkDraft(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    ContentBatchOperation Operation,
    string SourceRelativePath,
    string DestinationRelativePath,
    string Glob,
    IReadOnlyList<string> Extensions,
    ContentBatchCollisionPolicy CollisionPolicy,
    string Provenance,
    string LicenseSpdx,
    bool Consent);

internal sealed record StudioUx8KBulkPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool ConfigurationValid,
    bool TransferValid,
    bool ScopeConfined,
    string Message,
    long TotalBytes,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Entries,
    IReadOnlyList<string> Diagnostics);

internal sealed record StudioUx8KActionResult(bool Succeeded, string Message);

internal sealed record StudioUx8KApprovalSnapshot(
    StudioUx8KBulkDraft? Draft,
    string? Fingerprint);

internal sealed record StudioUx8KSmokeResult(
    bool Succeeded,
    bool RouteCovered,
    bool ExplicitSelection,
    bool ConsentRequired,
    bool ImportPlanned,
    bool ExportPlanned,
    bool CollisionPreserved,
    bool ScopeConfined,
    bool CanonicalDryRun,
    bool CanonicalCommand,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
