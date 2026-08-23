using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Scripting;

namespace Forge.Studio.Session;

internal sealed record StudioUx6OQualityDraft(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    string ApiVersion,
    string EventBinding,
    bool RequiredAtRuntime,
    Guid UnitTestsId,
    int DeterministicSeed,
    int MaximumCases,
    Guid ProfilerId,
    long InstructionThreshold,
    long AllocationThresholdBytes,
    int CpuThresholdMilliseconds,
    Guid DocumentationId,
    string DocumentationPath,
    IReadOnlySet<string> DocumentationAvailability,
    Guid StaticAnalysisId,
    IReadOnlySet<string> EnabledRules,
    bool TreatWarningsAsErrors,
    Guid StateId,
    int EnvelopeSchemaVersion,
    int MaximumStateBytes,
    IReadOnlyDictionary<string, int> RegisteredComponents,
    long BaseRevision,
    string ProfileSourceFingerprint,
    Guid ScriptSourceId,
    string ScriptRelativePath,
    string ScriptSourceFingerprint);

internal sealed record StudioUx6OPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    string Message,
    string SemanticDiff,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6OSimulation(
    bool Succeeded,
    string Message,
    bool UnitTests,
    bool DeterministicReport,
    bool Profiler,
    bool Documentation,
    bool StaticAnalysis,
    bool StateRoundTrip,
    bool Adapter,
    bool NativeSandbox,
    IReadOnlyList<string> Results,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6OActionResult(
    bool Succeeded,
    string Message,
    bool CanUndo,
    bool CanRedo);

internal sealed record StudioUx6OApprovalSnapshot(
    StudioUx6OQualityDraft? Draft,
    long? Revision,
    string? CandidateFingerprint,
    string? ScriptFingerprint);

internal sealed record StudioUx6OSourceSnapshot(
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
    bool QualityFileExisted,
    byte[]? QualityFileBytes);

internal sealed record StudioUx6OSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool CanonicalProfile,
    bool UnitTests,
    bool Profiler,
    bool Documentation,
    bool StaticAnalysis,
    bool StateSerialization,
    bool RuntimeAdapter,
    bool NativeSandbox,
    bool InvalidProfilesBlocked,
    bool CanonicalDryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool SourcePreserved,
    bool ProjectNeighborsPreserved,
    bool TemporaryWorkspaceReleased);
