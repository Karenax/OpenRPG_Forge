namespace Forge.Studio.Session;

internal sealed record StudioUx7HVisualSelection(
    Guid ProjectId,
    string ModuleName,
    string SourceFingerprint,
    string PlanFingerprint,
    int CaseCount,
    int ResolutionCount,
    int CameraCount,
    int QualityTierCount,
    double PixelTolerance,
    bool OfficialEngineReady,
    string EngineVersion,
    bool RuntimeReady);

internal sealed record StudioUx7HVisualStage(
    string Route,
    string Key,
    string Title,
    bool Completed,
    bool Passed,
    int Cases,
    int ArtifactCount,
    string Summary,
    IReadOnlyList<string> Details,
    string Fingerprint);

internal sealed record StudioUx7HVisualRunReport(
    bool Succeeded,
    bool Cancelled,
    bool Isolated,
    bool OfficialEngine,
    bool RuntimeProcess,
    bool ArtifactsConfined,
    bool SourceUntouched,
    bool TemporaryWorkspaceReleased,
    string Fingerprint,
    IReadOnlyList<StudioUx7HVisualStage> Stages,
    string Message);

internal sealed record StudioUx7HVisualSmokeResult(
    bool Succeeded,
    bool VisualRoute,
    bool ExplicitSelection,
    bool OfficialEngine,
    bool RuntimeProcess,
    bool EightCases,
    bool ArtifactSet,
    bool Cancellation,
    bool ResultNavigation,
    bool DeterministicPlan,
    bool Isolated,
    bool ArtifactsConfined,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased,
    bool TracyDeferred,
    bool RunSucceeded,
    bool RunCancelled,
    string RunMessage);
