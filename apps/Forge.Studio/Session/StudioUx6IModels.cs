namespace Forge.Studio.Session;

internal sealed record StudioUx6IChoice(
    string Key,
    string Label,
    string Detail);

internal sealed record StudioUx6IAreaChoice(
    Guid Id,
    string Slug,
    string Label,
    IReadOnlyList<StudioUx6IChoice> Tags,
    int EntityCount);

internal sealed record StudioUx6ICatalog(
    IReadOnlyList<StudioUx6IAreaChoice> Areas,
    IReadOnlyList<StudioUx6IChoice> Blueprints)
{
    internal bool Complete => Areas.Any(area => area.Tags.Count > 1) && Blueprints.Count > 0;
}

internal sealed record StudioUx6IWorldDraft(
    Guid AreaId,
    string SubjectTag,
    string TargetTag,
    string SpawnBlueprint,
    string MovementMode,
    double DestinationX,
    double DestinationY,
    double DestinationZ,
    double VelocityX,
    double VelocityY,
    double VelocityZ,
    double SpawnX,
    double SpawnY,
    double SpawnZ,
    double FollowDistance,
    long LocalInteger,
    double LocalNumber,
    string LocalText,
    string RegexValue,
    string RegexPattern,
    int FloatPrecision);

internal sealed record StudioUx6ISourcePlan(
    bool Succeeded,
    string Message,
    string ReviewedSource,
    string CandidateSource,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6IRuntimeResult(
    bool Succeeded,
    string Message,
    bool ManifestReady,
    bool ForbiddenCapabilityBlocked,
    bool QuotaInterrupted,
    bool RuntimeRecovered,
    bool NativeCompleted,
    bool AreaAndQueries,
    bool SpawnAndDestroy,
    bool MovementTasks,
    bool TeleportAndVelocity,
    bool MovementCancelled,
    bool UtilitiesDeterministic,
    bool UnicodeAndRegex,
    bool TypedLocals,
    bool CanonicalDryRun,
    bool CommandApplyUndoRedo,
    bool InvalidIntentBlocked,
    bool StaleRevisionBlocked,
    string SemanticDiff,
    IReadOnlyList<string> Events,
    IReadOnlyList<StudioUx6FDiagnostic> Diagnostics);

internal sealed record StudioUx6ISmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool SharedSourceAuthority,
    bool HumanCatalogs,
    bool ReviewedSource,
    bool WorldApi,
    bool MovementApi,
    bool UtilityApi,
    bool LocalVariables,
    bool SandboxBoundary,
    bool CanonicalDryRun,
    bool Compilation,
    bool SandboxExecution,
    bool StaleApprovalBlocked,
    bool ApplyUndoRedo,
    bool Persistence,
    bool ProjectNeighborsPreserved,
    bool TemporaryWorkspaceReleased);
