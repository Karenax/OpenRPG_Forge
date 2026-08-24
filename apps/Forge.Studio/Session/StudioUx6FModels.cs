using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal enum StudioUx6FSourceKind
{
    Canonical,
    LegacyProtected,
    InvalidCanonical,
}

internal sealed record StudioUx6FSourceChoice(
    string Label,
    string RelativePath,
    StudioUx6FSourceKind Kind,
    string Detail);

internal sealed record StudioUx6FBindingDraft(
    Guid Id,
    string ComponentId,
    string EventName,
    string Handler,
    string PayloadType,
    int Priority,
    string? EnableCondition,
    ForgeScriptEventAuthority Authority,
    bool Enabled);

internal sealed record StudioUx6FDraft(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string RelativePath,
    string Source,
    string ApiVersion,
    ForgeScriptSourceDistributionPolicy Distribution,
    bool RequiredAtRuntime,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<StudioUx6FBindingDraft> Bindings,
    IReadOnlySet<string>? RequiredCapabilities,
    long BaseRevision,
    string DiskFingerprint,
    bool NewSource);

internal sealed record StudioUx6FDiagnostic(
    string Code,
    string Path,
    string Severity,
    string Message,
    int Line = 0,
    int Column = 0);

internal sealed record StudioUx6FPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string SemanticDiff,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics,
    int SourceBytes,
    string Language,
    string DialectVersion,
    string ApiVersion,
    bool CanonicalDryRun,
    bool LegacySourcesProtected);

internal sealed record StudioUx6FCompileResult(
    bool Succeeded,
    string Message,
    string VirtualMachineVersion,
    string ApiVersion,
    int BytecodeBytes,
    string CacheKey,
    bool CacheHit,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6FSandboxResult(
    bool Succeeded,
    string Message,
    ForgeScriptExecutionStatus Status,
    long Instructions,
    long PeakMemoryBytes,
    IReadOnlyList<string> Events,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6FActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6FApprovalSnapshot(
    StudioUx6FDraft? Draft,
    long? Revision,
    string? CandidateFingerprint);

internal sealed record StudioUx6FEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    StudioUx6FApprovalSnapshot Approval);

internal sealed record StudioUx6FSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool CanonicalSource,
    bool LegacyProtected,
    bool TypedBindings,
    bool EntryPoint,
    bool InvalidPathBlocked,
    bool InvalidHandlerBlocked,
    bool CanonicalDryRun,
    bool Compilation,
    bool BytecodeCache,
    bool SandboxExecution,
    bool QuotaAbuseBlocked,
    bool StaleDiskBlocked,
    bool StaleApprovalBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool ProjectNeighborsPreserved,
    bool TemporaryWorkspaceReleased);
