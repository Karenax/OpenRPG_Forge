namespace Forge.Studio.Session;

internal sealed record StudioUx7GPlaytestSelection(
    Guid ProjectId,
    string ModuleName,
    string SourceFingerprint,
    string EntryAreaName,
    int AreaCount,
    int DialogueCount,
    int QuestCount,
    int CombatBlueprintCount);

internal sealed record StudioUx7GPlaytestStage(
    string Route,
    string Key,
    string Title,
    bool Completed,
    bool Passed,
    int Cases,
    string Summary,
    IReadOnlyList<string> Details,
    string Fingerprint);

internal sealed record StudioUx7GPlaytestRunReport(
    bool Succeeded,
    bool Cancelled,
    bool Isolated,
    bool ArtifactsConfined,
    bool SourceUntouched,
    bool TemporaryWorkspaceReleased,
    string Fingerprint,
    IReadOnlyList<StudioUx7GPlaytestStage> Stages,
    string Message);

internal sealed record StudioUx7GPlaytestSmokeResult(
    bool Succeeded,
    bool FiveRoutes,
    bool ExplicitSelection,
    bool PreviewMode,
    bool PlayFromHere,
    bool DeterministicReplay,
    bool TestAgent,
    bool CombatSimulation,
    bool Cancellation,
    bool ResultNavigation,
    bool Deterministic,
    bool Isolated,
    bool ArtifactsConfined,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);

internal sealed record StudioUx7GRuntimeLaunchResult(
    bool Succeeded,
    bool Running,
    int? ProcessId,
    bool Isolated,
    bool SourceUntouched,
    string Message);

internal sealed record StudioUx7GRuntimeStatus(
    bool Running,
    bool Completed,
    int? ExitCode,
    bool TemporaryWorkspaceReleased,
    string Message);
