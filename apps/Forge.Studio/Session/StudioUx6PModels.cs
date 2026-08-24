using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed record StudioUx6PInteropDraft(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string ApiVersion,
    string EventBinding,
    bool RequiredAtRuntime,
    int MaximumSources,
    int MaximumSourceBytes,
    int TestInstructionLimit,
    int TestTimeoutMilliseconds,
    int TestRecursionLimit,
    int TestSpawnLimit,
    string TargetLabel,
    string EntityId,
    string ComponentId,
    string EventName,
    string Handler,
    string PayloadType,
    int Priority,
    string? EnableCondition,
    ForgeScriptEventAuthority Authority,
    long BaseRevision,
    string ProfileSourceFingerprint,
    Guid ScriptSourceId,
    string ScriptRelativePath,
    string ScriptSourceFingerprint);

internal sealed record StudioUx6PPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool SourcePlan,
    bool BindingPlan,
    string Message,
    string SemanticDiff,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6PSimulation(
    bool Succeeded,
    string Message,
    bool Api,
    bool SourceTransactions,
    bool Compilation,
    bool SandboxTests,
    bool BindingTransactions,
    bool SnapshotRoundTrip,
    bool RuntimeAdapter,
    bool NativeSandbox,
    bool QuotaRecovery,
    IReadOnlyList<string> Results,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6PActionResult(
    bool Succeeded,
    string Message,
    bool CanUndo,
    bool CanRedo);

internal sealed record StudioUx6PApprovalSnapshot(
    StudioUx6PInteropDraft? Draft,
    long? Revision,
    string? CandidateFingerprint,
    string? ScriptFingerprint);

internal sealed record StudioUx6PSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    StudioUx6FApprovalSnapshot ScriptApproval,
    StudioUx6JApprovalSnapshot ExecutionPolicyApproval,
    StudioUx6KApprovalSnapshot PermissionApproval,
    StudioUx6MApprovalSnapshot AutomationApproval,
    StudioUx6OApprovalSnapshot QualityApproval,
    StudioUx6PApprovalSnapshot InteropApproval,
    bool InteropFileExisted,
    byte[]? InteropFileBytes);

internal sealed record StudioUx6PSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool CanonicalProfile,
    bool Api,
    bool SourceTransactions,
    bool Compilation,
    bool SandboxTests,
    bool BindingTransactions,
    bool SnapshotRoundTrip,
    bool RuntimeAdapter,
    bool NativeSandbox,
    bool QuotaRecovery,
    bool InvalidProfilesBlocked,
    bool InvalidSourceBlocked,
    bool InvalidBindingBlocked,
    bool CanonicalDryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool SourcePreserved,
    bool ProjectNeighborsPreserved,
    bool TemporaryWorkspaceReleased);
