using Forge.Application.Quality;
using Forge.Domain.Quality;

namespace Forge.Studio.Session;

internal sealed record StudioUx7BProfileDraft(
    QualityFoundationRevision Revision,
    Guid Id,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<string> DomainAreas,
    IReadOnlyList<string> HeadlessScenarios,
    string ReportRelativePath,
    bool SaveRoundTrip,
    bool SaveOldVersions,
    bool SavePartialFailure,
    bool SaveMissingPackage,
    bool SaveRollback,
    bool SaveDeterministicState,
    StructuredLogSeverity MinimumSeverity,
    IReadOnlyList<string> LogCategories,
    bool IncludeObjectIds,
    bool IncludeSourceLines,
    bool IncludeTransactions,
    PlayerPrivacyMode PlayerPrivacy,
    int RetentionDays,
    int MaximumEntries,
    string SourceFingerprint);

internal sealed record StudioUx7BDiagnostic(
    string Code,
    string Path,
    string Severity,
    string Message);

internal sealed record StudioUx7BPreview(
    bool Succeeded,
    bool Changed,
    bool CanonicalDryRun,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<StudioUx7BDiagnostic> Diagnostics);

internal sealed record StudioUx7BRunStage(
    string Key,
    string Route,
    string Label,
    bool Passed,
    string Summary,
    IReadOnlyList<string> Details,
    long DurationMilliseconds,
    bool Navigable);

internal sealed record StudioUx7BRunReport(
    bool Succeeded,
    bool Cancelled,
    bool Isolated,
    bool SourceUntouched,
    bool TemporaryWorkspaceReleased,
    string Fingerprint,
    IReadOnlyList<StudioUx7BRunStage> Stages,
    string Message);

internal sealed record StudioUx7BActionResult(
    bool Succeeded,
    string Message,
    bool CanUndo,
    bool CanRedo);

internal sealed record StudioUx7BApprovalSnapshot(
    StudioUx7BProfileDraft? Draft,
    QualityFoundationRevision? Revision,
    string? CandidateFingerprint,
    string? SourceFingerprint);

internal sealed record StudioUx7BSmokeResult(
    bool Succeeded,
    bool FourRoutes,
    bool ExplicitSelection,
    bool DomainHarness,
    bool HeadlessRuntime,
    bool SaveCoverage,
    bool StructuredLogs,
    bool Cancellation,
    bool Navigation,
    bool CanonicalDryRun,
    bool InvalidProfileBlocked,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool LicenseTailPreserved,
    bool ValidationTailPreserved,
    bool SourcePreserved,
    bool TemporaryWorkspaceReleased);
