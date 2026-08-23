using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed record StudioUx6JChoice(
    Guid Id,
    string Label,
    string Detail);

internal sealed record StudioUx6JCatalog(
    IReadOnlyList<StudioUx6JChoice> Areas,
    IReadOnlyList<StudioUx6JChoice> Entities,
    IReadOnlyList<StudioUx6JChoice> Assets)
{
    internal bool Complete => Areas.Count > 0 && Entities.Count > 0 && Assets.Count > 0;
}

internal sealed record StudioUx6JHandleDraft(
    Guid AreaId,
    Guid EntityId,
    Guid AssetId);

internal sealed record StudioUx6JPolicyDraft(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string ApiVersion,
    int HandlerInstructionLimit,
    int TickInstructionLimit,
    long ModuleInstructionLimit,
    int TimeoutMilliseconds,
    int RecursionLimit,
    int SpawnLimit,
    int ContextMemoryLimitBytes,
    long ModuleMemoryLimitBytes,
    ulong DeterministicSeed,
    long SimulationTick,
    long SimulationTimeMilliseconds,
    ForgeScriptExecutionAuthority Authority,
    bool RequiredAtRuntime,
    long BaseRevision,
    string SourceFingerprint);

internal sealed record StudioUx6JPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string SemanticDiff,
    string SelectedApiVersion,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Deprecations,
    IReadOnlyList<string> Migrations,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics,
    bool CanonicalDryRun);

internal sealed record StudioUx6JSimulation(
    bool Succeeded,
    string Message,
    bool OpaqueHandles,
    bool WrongTypeBlocked,
    bool StaleHandleBlocked,
    bool GenerationAdvanced,
    bool InstructionQuotaBlocked,
    bool AggregateQuotaBlocked,
    bool MemoryQuotaBlocked,
    bool CancellationBlocked,
    bool RuntimeRecovered,
    bool DeterministicReplay,
    bool SimulationClock,
    bool ApiNegotiation,
    bool CanonicalSourceCompiled,
    long Instructions,
    long PeakMemoryBytes,
    IReadOnlyList<string> Events,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6JActionResult(
    bool Succeeded,
    string Message,
    bool CanUndo,
    bool CanRedo);

internal sealed record StudioUx6JApprovalSnapshot(
    StudioUx6JPolicyDraft? Draft,
    long? Revision,
    string? CandidateFingerprint);

internal sealed record StudioUx6JSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    StudioUx6FApprovalSnapshot ScriptApproval,
    StudioUx6JApprovalSnapshot PolicyApproval,
    bool PolicyFileExisted,
    byte[]? PolicyFileBytes);

internal sealed record StudioUx6JSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool HumanCatalogs,
    bool CanonicalPolicy,
    bool OpaqueHandles,
    bool InstructionBudgets,
    bool MemoryBudgets,
    bool DeterministicReplay,
    bool ApiCompatibility,
    bool CanonicalDryRun,
    bool InvalidPolicyBlocked,
    bool StaleApprovalBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool SourcePreserved,
    bool ProjectNeighborsPreserved,
    bool TemporaryWorkspaceReleased);
