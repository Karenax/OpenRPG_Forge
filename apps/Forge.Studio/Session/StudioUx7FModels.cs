namespace Forge.Studio.Session;

internal sealed record StudioUx7FHistorySelection(
    Guid ProjectId,
    string ModuleName,
    string SourceFingerprint,
    int AutosaveIntervalSeconds,
    int RetainedRecoverySnapshots,
    int SaveRollbackSlots,
    string PersistenceScope,
    int CheckpointCount);

internal sealed record StudioUx7FHistoryStage(
    string Route,
    string Key,
    string Title,
    bool Completed,
    bool Passed,
    int Cases,
    string Summary,
    IReadOnlyList<string> Details,
    string Fingerprint);

internal sealed record StudioUx7FHistoryRunReport(
    bool Succeeded,
    bool Cancelled,
    bool Isolated,
    bool ArtifactsConfined,
    bool SourceUntouched,
    bool TemporaryWorkspaceReleased,
    string Fingerprint,
    IReadOnlyList<StudioUx7FHistoryStage> Stages,
    string Message);

internal sealed record StudioUx7FHistorySmokeResult(
    bool Succeeded,
    bool FiveRoutes,
    bool ExplicitSelection,
    bool AutosaveRecovery,
    bool SaveLoad,
    bool SaveScopes,
    bool CheckpointRespawn,
    bool CrashRecovery,
    bool Cancellation,
    bool ResultNavigation,
    bool Deterministic,
    bool Isolated,
    bool ArtifactsConfined,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);
