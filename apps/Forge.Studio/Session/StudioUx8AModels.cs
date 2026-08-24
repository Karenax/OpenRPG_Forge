namespace Forge.Studio.Session;

internal sealed record StudioUx8ABuildSelection(
    Guid ProjectId,
    string ModuleName,
    string SourceFingerprint,
    string GraphFingerprint,
    int ResourceCount,
    int DependencyCount,
    int MaterializationTasks,
    int MaximumParallelism,
    int PeakParallelism,
    string OrderFingerprint,
    int PlannedRebuild,
    int PlannedReuse,
    bool ProfileReady,
    bool CliReady,
    string CommandPreview);

internal sealed record StudioUx8ABuildStage(
    string Route,
    string Key,
    string Title,
    bool Completed,
    bool Passed,
    int Rebuilt,
    int Reused,
    string Summary,
    IReadOnlyList<string> Details,
    string Log);

internal sealed record StudioUx8ABuildRunReport(
    bool Succeeded,
    bool Cancelled,
    bool Isolated,
    bool CliProcess,
    bool ArtifactsConfined,
    bool SourceUntouched,
    bool TemporaryWorkspaceReleased,
    bool ArtifactsRetained,
    string OutputDirectory,
    string Fingerprint,
    IReadOnlyList<StudioUx8ABuildStage> Stages,
    string Message);

internal sealed record StudioUx8ABuildSmokeResult(
    bool Succeeded,
    bool FiveRoutes,
    bool ExplicitSelection,
    bool CliProcess,
    bool ModulePackage,
    bool OptimizedBuild,
    bool CliBuild,
    bool Deterministic,
    bool Incremental,
    bool Cancellation,
    bool ResultNavigation,
    bool Isolated,
    bool ArtifactsConfined,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased,
    string RunMessage);
