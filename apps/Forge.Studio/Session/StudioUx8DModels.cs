namespace Forge.Studio.Session;

internal sealed record StudioUx8DSelection(
    Guid ProjectId,
    string ModuleName,
    string RepositoryName,
    bool RepositoryAvailable,
    string ProfileProvenance);

internal sealed record StudioUx8DStage(
    string Route,
    string Title,
    bool Configured,
    bool RepositoryEvidenceAvailable,
    string Verdict,
    string Summary,
    IReadOnlyList<string> Details,
    IReadOnlyList<string> Log);

internal sealed record StudioUx8DReport(
    bool Succeeded,
    bool Cancelled,
    string ModuleName,
    string RepositoryName,
    IReadOnlyList<StudioUx8DStage> Stages,
    string Message);

internal sealed record StudioUx8DSmokeResult(
    bool Succeeded,
    bool FourRoutes,
    bool CanonicalRepository,
    bool StandardLayout,
    bool SemanticVersioning,
    bool ArchitectureDecisions,
    bool Cancellation,
    bool LogsOnDemand,
    bool SourceUnchanged,
    string Message);
