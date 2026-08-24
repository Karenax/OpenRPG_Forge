using Forge.Application.Projects;
using Forge.Domain.Projects;
using Forge.Domain.RuntimeExtensions;
using Forge.ModuleFormat.Workspaces;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed record StudioUx6QProfileChoice(string Label);

internal sealed record StudioUx6QSourceChoice(string Label, string RelativePath, bool Canonical);

internal sealed record StudioUx6QDiagnostic(string Code, string Path, string Severity, string Message);

internal sealed record StudioUx6QCompilerDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    bool Incremental,
    bool StableDiagnostics,
    bool PerformanceTelemetry,
    int MaxParallelism,
    int MaxSourceBytes,
    IReadOnlyList<string> ConformanceSuites);

internal sealed record StudioUx6QCompilationEvidence(
    bool Succeeded,
    bool InventoryPresent,
    bool DependencyAware,
    bool CacheReused,
    bool DependencyInvalidated,
    bool StableDiagnostics,
    bool Telemetry,
    bool Conformance,
    bool TemporaryCacheReleased,
    int SourceCount,
    int FirstCompiled,
    int SecondReused,
    int ChangedCompiled,
    long ElapsedMicroseconds,
    IReadOnlyList<string> Entries,
    IReadOnlyList<StudioUx6QDiagnostic> Diagnostics,
    string Message);

internal sealed record StudioUx6QCompilerPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    string Message,
    string SemanticDiff,
    IReadOnlyList<string> Changes,
    StudioUx6QCompilationEvidence Compilation,
    IReadOnlyList<StudioUx6QDiagnostic> Diagnostics);

internal sealed record StudioUx6QAssistantDraft(
    ProjectRevision BaseRevision,
    string ProjectFingerprint,
    string DiskFingerprint,
    bool NewSource,
    ForgeScriptSourceDocument Template,
    string RelativePath,
    string EventBinding,
    string Intent,
    string Message,
    string VariableKey,
    string VariableValue,
    IReadOnlyList<string> ContextCitations,
    IReadOnlyList<string> StyleRules,
    string AuthorId,
    int Seed,
    string SourceOverride);

internal sealed record StudioUx6QProposalView(
    bool Succeeded,
    bool CanPreview,
    string Message,
    string Source,
    string SemanticDiff,
    string ApiVersion,
    string VirtualMachineVersion,
    string Provider,
    decimal EstimatedCostUsd,
    string Provenance,
    IReadOnlyList<string> Tests,
    IReadOnlyList<StudioUx6QDiagnostic> Diagnostics);

internal sealed record StudioUx6QAssistantPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool ApprovalBound,
    bool PermissionBound,
    string Message,
    string SemanticDiff,
    IReadOnlyList<string> Tests,
    IReadOnlyList<StudioUx6QDiagnostic> Diagnostics);

internal sealed record StudioUx6QActionResult(
    bool Succeeded,
    string Message,
    bool CanUndo,
    bool CanRedo);

internal sealed record StudioUx6QAssistantApprovalState(
    StudioUx6QAssistantDraft Draft,
    AssistedForgeScriptProposal Proposal,
    ForgeScriptSourceDocument Candidate,
    ProjectRevision Revision,
    string DraftFingerprint,
    Guid TransactionId,
    Guid ExecutionPlanId);

internal sealed record StudioUx6QApprovalSnapshot(
    StudioUx6QCompilerDraft? CompilerDraft,
    ProjectRevision? CompilerRevision,
    string? CompilerFingerprint,
    AssistedForgeScriptProposal? Proposed,
    ForgeScriptSourceDocument? ProposedCandidate,
    string? ProposedDraftFingerprint,
    StudioUx6QAssistantApprovalState? AssistantApproval);

internal sealed record StudioUx6QSourceSnapshot(
    ProjectDefinition Project,
    Forge.Application.Commands.ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<Forge.Application.Commands.CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    Forge.Domain.Areas.AreaId? ActiveAreaId,
    StudioUx6FApprovalSnapshot SourceApproval,
    StudioUx6JApprovalSnapshot ExecutionApproval,
    StudioUx6KApprovalSnapshot PermissionApproval,
    StudioUx6MApprovalSnapshot AutomationApproval,
    StudioUx6OApprovalSnapshot QualityApproval,
    StudioUx6PApprovalSnapshot InteropApproval,
    StudioUx6QApprovalSnapshot CompilerAssistantApproval);

internal sealed record StudioUx6QSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool CanonicalProfile,
    bool InvalidCompilerBlocked,
    bool CompilerDryRun,
    bool DependencyCompilation,
    bool CacheReuse,
    bool DependencyInvalidation,
    bool StableDiagnostics,
    bool Telemetry,
    bool Conformance,
    bool CompilerApplyUndoRedo,
    bool CompilerPersistence,
    bool InvalidAssistantBlocked,
    bool ProposalCompiled,
    bool SandboxTests,
    bool OfflineProvider,
    bool ApprovalRequired,
    bool PermissionRequired,
    bool StaleDraftBlocked,
    bool StaleDiskBlocked,
    bool CanonicalAssistantDryRun,
    bool AssistantApplyUndoRedo,
    bool SourceMetadataRoundTrip,
    bool ProjectNeighborsPreserved,
    bool TemporaryWorkspaceReleased);
