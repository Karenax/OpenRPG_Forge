namespace Forge.Studio.Session;

internal sealed record StudioUx7DPerformanceSelection(
    Guid ProjectId,
    string ModuleName,
    string SourceFingerprint,
    int AreaCount,
    int ScriptCount,
    int ScenarioCount,
    int BudgetCount,
    int ProfilerSurfaceCount);

internal sealed record StudioUx7DPerformanceStage(
    string Route,
    string Key,
    string Title,
    bool Completed,
    bool Passed,
    int Cases,
    int WarningCases,
    int FailedCases,
    string Summary,
    IReadOnlyList<string> Details,
    string Fingerprint);

internal sealed record StudioUx7DRunReport(
    bool Succeeded,
    bool Cancelled,
    bool Isolated,
    bool ArtifactsConfined,
    bool SourceUntouched,
    bool TemporaryWorkspaceReleased,
    string Fingerprint,
    IReadOnlyList<StudioUx7DPerformanceStage> Stages,
    string Message);

internal sealed record StudioUx7DSmokeResult(
    bool Succeeded,
    bool ThreeRoutes,
    bool ExplicitSelection,
    bool Benchmarks,
    bool Budgets,
    bool Profiling,
    bool Cancellation,
    bool ResultNavigation,
    bool Deterministic,
    bool Isolated,
    bool ArtifactsConfined,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
