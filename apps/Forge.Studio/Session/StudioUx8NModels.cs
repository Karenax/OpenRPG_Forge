using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Content;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx8NCuratedDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string Channel,
    string Compatibility,
    IReadOnlyList<string> AllowedLicenses,
    bool RequireTrustedSignature,
    bool AllowOfflineFallback,
    bool Approved);

internal sealed record StudioUx8NResourceChoice(
    Guid PackageId,
    Guid ResourceId,
    bool IsAsset,
    string PackageLabel,
    string ResourceLabel,
    string Address);

internal sealed record StudioUx8NResourceDraft(
    StudioUx8GPackageDraft Catalog,
    Guid PackageId,
    Guid ResourceId,
    bool IsAsset,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> References,
    bool Approved);

internal sealed record StudioUx8NMigrationDraft(
    StudioUx8GPackageDraft Catalog,
    Guid PackageId,
    string TargetVersion,
    bool Approved);

internal sealed record StudioUx8NUninstallDraft(
    StudioUx8GPackageDraft Catalog,
    Guid PackageId,
    Guid? ReplacementPackageId,
    bool RemoveFromAssetVault,
    bool Approved);

internal sealed record StudioUx8NDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx8NPreview(
    bool Succeeded,
    bool Changed,
    bool CanonicalDryRun,
    bool CanApply,
    string Message,
    string Patch,
    IReadOnlyList<string> Lines,
    IReadOnlyList<StudioUx8NDiagnostic> Diagnostics);

internal sealed record StudioUx8NActionResult(bool Succeeded, string Message);

internal sealed record StudioUx8NApprovalSnapshot(
    StudioUx8NCuratedDraft? CuratedDraft,
    ProjectRevision? CuratedRevision,
    string? CuratedFingerprint,
    StudioUx8NMigrationDraft? MigrationDraft,
    string? MigrationFingerprint,
    StudioUx8NUninstallDraft? UninstallDraft,
    string? UninstallFingerprint,
    StudioUx8GApprovalSnapshot PackageApproval);

internal sealed record StudioUx8NEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool Dirty,
    bool CanUndo,
    bool CanRedo,
    StudioUx8NApprovalSnapshot Approval);

internal sealed record StudioUx8NSmokeResult(
    bool Succeeded,
    bool FourRoutes,
    bool CuratedGovernance,
    bool ResourceNamespaces,
    bool MigrationLifecycle,
    bool SafeUninstall,
    bool CanonicalDryRuns,
    bool CanonicalCommands,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool ResourcesReleased,
    bool SourceUnchanged,
    string Message);
