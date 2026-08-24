using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;

namespace Forge.Studio.Session;

internal sealed record StudioUx6VAreaChoice(AreaId Id, string Label);

internal sealed record StudioUx6VAgentChoice(NavigationAgentProfileId Id, string Label);

internal sealed record StudioUx6VDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    AreaId AreaId,
    NavigationAgentProfileId AgentId,
    string AgentName,
    float CellSize,
    float AgentRadius,
    float AgentHeight,
    bool CanOpenDoors,
    bool CanUseOffMeshLinks,
    bool CanFly,
    NavigationDoorState DoorState,
    NavigationCell Start,
    NavigationCell Goal,
    bool DynamicObstacleEnabled,
    NavigationCell DynamicObstacle,
    bool CostEnabled,
    NavigationCell CostCell,
    float CostMultiplier,
    bool RecoverBlockedEndpoints,
    int RecoveryRadius,
    int MaximumVisitedCells);

internal sealed record StudioUx6VDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx6VGridSnapshot(
    int Width,
    int Depth,
    IReadOnlyList<NavigationCell> Walkable,
    IReadOnlyList<NavigationCell> DynamicObstacles,
    IReadOnlyList<NavigationCell> CostCells,
    IReadOnlyList<NavigationCell> Path,
    NavigationCell RequestedStart,
    NavigationCell RequestedGoal,
    NavigationCell? ResolvedStart,
    NavigationCell? ResolvedGoal);

internal sealed record StudioUx6VPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    string Message,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx6VDiagnostic> Diagnostics,
    IReadOnlyList<string> Results,
    StudioUx6VGridSnapshot? Grid,
    bool SurfaceBuilt,
    bool EnginePlanBuilt,
    bool DynamicObstacleAvoided,
    bool CostDetour,
    bool RecoveryVerified,
    bool BudgetFailureVerified,
    bool PartialUpdatePlanned,
    bool RuntimeClaimsBounded);

internal sealed record StudioUx6VActionResult(bool Succeeded, string Message);

internal sealed record StudioUx6VApprovalSnapshot(
    StudioUx6VDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx6VSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealModuleReadable,
    bool InvalidConfigurationBlocked,
    bool CanonicalDryRun,
    bool SurfaceBuilt,
    bool EnginePlanBuilt,
    bool DynamicObstacleAvoided,
    bool CostDetour,
    bool RecoveryVerified,
    bool BudgetFailureVerified,
    bool PartialUpdatePlanned,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NavigationNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool HumanText,
    bool RuntimeClaimsBounded,
    bool TemporaryWorkspaceReleased);
