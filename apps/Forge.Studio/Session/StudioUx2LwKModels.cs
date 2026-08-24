using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioUx2LwKEncounterChoice(
    Guid ProfileId,
    string Label,
    bool IsComplete,
    bool HasStatistics,
    bool IsEvidence);

internal sealed record StudioUx2LwKDraft(
    Guid ProfileId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    Guid StatisticsId,
    int StatisticsSchemaVersion,
    string StatisticsProvenance,
    int Runs,
    int BaseSeed,
    string DurationEstimatorId,
    double SecondsPerActor,
    string RewardEstimatorId,
    double RewardPerCost,
    IReadOnlyList<double> Percentiles,
    bool IncludeRunDetails,
    IReadOnlyDictionary<string, string> Overrides,
    bool RequiredAtRuntime,
    EncounterWorldProfile OriginalProfile);

internal sealed record StudioUx2LwKDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2LwKDiff(string Section, string Before, string After, bool Changed);

internal sealed record StudioUx2LwKPercentile(
    string Label,
    int SpawnedCount,
    double Cost,
    double DurationSeconds,
    double RewardValue);

internal sealed record StudioUx2LwKComposition(
    string Creature,
    int TotalAcrossRuns,
    double MeanPerRun);

internal sealed record StudioUx2LwKRun(
    int Ordinal,
    int Seed,
    int SpawnedCount,
    double Cost,
    double DurationSeconds,
    double RewardValue,
    string Composition);

internal sealed record StudioUx2LwKSimulation(
    int Runs,
    int FirstSeed,
    int LastSeed,
    double MeanCount,
    double MeanCost,
    double MeanDurationSeconds,
    double MeanRewardValue,
    double CountVariance,
    double CostVariance,
    double DurationVariance,
    double RewardVariance,
    IReadOnlyList<StudioUx2LwKPercentile> Percentiles,
    IReadOnlyList<StudioUx2LwKComposition> Composition,
    IReadOnlyList<StudioUx2LwKRun> DisplayedRuns,
    int HiddenRunCount,
    string CanonicalSummary,
    string Boundary);

internal sealed record StudioUx2LwKPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string CanonicalPatch,
    IReadOnlyList<StudioUx2LwKDiff> Diff,
    IReadOnlyList<StudioUx2LwKDiagnostic> Diagnostics,
    StudioUx2LwKSimulation? Simulation,
    bool DryRunPure);

internal sealed record StudioUx2LwKActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2LwKApprovalSnapshot(
    StudioUx2LwKDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx2LwKEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    IReadOnlyList<EntityId> SelectedEntityIds,
    EntityId? SelectedEntityId,
    StudioUx2LwKApprovalSnapshot Approval);

internal sealed record StudioUx2LwKSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool DependenciesRequired,
    bool OptionalAndRequiredResolution,
    bool ConfigurationValidation,
    bool PercentileValidation,
    bool OverrideValidation,
    bool SeedRangeValidation,
    bool DeterministicSimulation,
    bool CompleteMetrics,
    bool DetailsPolicy,
    bool DryRunPure,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool EncounterTailsPreserved,
    bool EncounterNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool CollectionsPreserved,
    bool StructuredDiagnostics,
    bool HumanText,
    bool OfflineBoundary);
