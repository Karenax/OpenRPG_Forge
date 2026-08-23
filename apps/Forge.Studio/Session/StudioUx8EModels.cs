namespace Forge.Studio.Session;

internal sealed record StudioUx8ESelection(
    Guid ProjectId,
    string ModuleName,
    string RepositoryName,
    bool RepositoryAvailable);

internal sealed record StudioUx8EStage(
    string Route,
    string Title,
    bool Evaluated,
    bool Ready,
    string Verdict,
    string Summary,
    IReadOnlyList<string> Details,
    IReadOnlyList<string> Log);

internal sealed record StudioUx8EReport(
    bool Succeeded,
    bool Cancelled,
    bool TemporaryWorkspaceReleased,
    string ModuleName,
    string RepositoryName,
    IReadOnlyList<StudioUx8EStage> Stages,
    string Message);

internal sealed record StudioUx8ESmokeResult(
    bool Succeeded,
    bool FourRoutes,
    bool DocumentationCheck,
    bool TaskManifests,
    bool ReleaseChannels,
    bool CrashArtifacts,
    bool Cancellation,
    bool LogsOnDemand,
    bool TemporaryWorkspaceReleased,
    bool SourceUnchanged,
    string Message);
