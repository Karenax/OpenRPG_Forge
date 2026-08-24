using Forge.Application.Projects;
using Forge.Domain.Areas;

namespace Forge.Studio.Session;

internal sealed record StudioUx6YAreaChoice(AreaId Id, string Label);

internal sealed record StudioUx6YWeatherStateChoice(Guid Id, string Label, WeatherKind Kind);

internal sealed record StudioUx6YWeatherDraft(
    Guid InitialStateId,
    bool ServerAuthoritative,
    int DeterministicSeed,
    bool PersistState,
    bool ReplicateState);

internal sealed record StudioUx6YLoadingDraft(
    bool AsyncStreaming,
    bool PrefetchDependencies,
    bool CancellationEnabled,
    int CacheBudgetMegabytes,
    int MaximumConcurrentLoads,
    int ChunkRadius,
    int TargetLoadMilliseconds,
    IReadOnlyList<string> RequiredResources);

internal sealed record StudioUx6YDraft(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    AreaId AreaId,
    AreaEnvironmentProfile SourceEnvironment,
    AreaWorldProfile SourceWorld,
    StudioUx6YWeatherDraft Weather,
    StudioUx6YLoadingDraft Loading);

internal sealed record StudioUx6YDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx6YPreview(
    bool Succeeded,
    bool CanApply,
    bool WeatherDryRun,
    bool LoadingDryRun,
    string Message,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx6YDiagnostic> Diagnostics,
    IReadOnlyList<string> WeatherResults,
    IReadOnlyList<string> LoadingResults,
    bool WeatherRuntimeResolved,
    bool StreamingRuntimeResolved,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx6YActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6YApprovalSnapshot(
    StudioUx6YDraft? Draft,
    ProjectRevision? Revision,
    string? CandidateFingerprint);

internal sealed record StudioUx6YSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealModuleReadable,
    bool InvalidConfigurationBlocked,
    bool WeatherDryRun,
    bool LoadingDryRun,
    bool WeatherRuntimeResolved,
    bool TypedWeatherEvent,
    bool AuthorityPolicyResolved,
    bool StreamingRuntimeResolved,
    bool CacheAndProgressResolved,
    bool BenchmarkResolved,
    bool CancellationPolicyResolved,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommands,
    bool UndoRedo,
    bool Persistence,
    bool EnvironmentNeighborsPreserved,
    bool WorldNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
