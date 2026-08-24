namespace Forge.Studio.Session;

internal sealed record StudioUx8BMaintenanceSelection(
    Guid ProjectId,
    string ModuleName,
    string SourceFingerprint,
    bool ProfileReady,
    string CachePath,
    int CacheMaximumEntries,
    int ReferenceMaximumNodes,
    int DiffMaximumEntries,
    string CommandPreview);

internal sealed record StudioUx8BMaintenanceStage(
    string Route,
    string Key,
    string Title,
    bool Passed,
    string Summary,
    IReadOnlyList<string> Details,
    string Log);

internal sealed record StudioUx8BMaintenanceReport(
    bool Succeeded,
    bool Cancelled,
    bool Isolated,
    bool SourceUntouched,
    bool TemporaryWorkspaceReleased,
    bool WorkspaceRetained,
    string WorkspaceDirectory,
    IReadOnlyList<StudioUx8BMaintenanceStage> Stages,
    string Message);

internal sealed record StudioUx8BMaintenanceSmokeResult(
    bool Succeeded,
    bool FiveRoutes,
    bool ExplicitSelection,
    bool Cache,
    bool Normalization,
    bool Migration,
    bool ReverseReferences,
    bool SemanticDiff,
    bool Cancellation,
    bool Isolated,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased,
    string Message);
