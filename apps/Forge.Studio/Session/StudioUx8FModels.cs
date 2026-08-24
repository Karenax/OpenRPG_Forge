using Forge.Application.Projects;
using Forge.Domain.Modules;

namespace Forge.Studio.Session;

internal sealed record StudioUx8FSelection(
    Guid ProjectId,
    string ModuleName,
    string WorkspaceName,
    bool GovernanceFileAvailable);

internal sealed record StudioUx8FStage(
    string Route,
    string Title,
    bool Evaluated,
    bool Ready,
    string Verdict,
    string Summary,
    IReadOnlyList<string> Details,
    IReadOnlyList<string> Log);

internal sealed record StudioUx8FReport(
    bool Succeeded,
    bool Cancelled,
    bool ResourcesReleased,
    string ModuleName,
    string WorkspaceName,
    IReadOnlyList<StudioUx8FStage> Stages,
    string Message);

internal sealed record StudioUx8FDependencyChoice(
    Guid Id,
    string Label);

internal sealed record StudioUx8FDependencyDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    Guid Id,
    bool IsNew,
    string Name,
    string Version,
    string LicenseSpdx,
    string Owner,
    string Rationale,
    string UpdateCadence,
    string RemovalPlan,
    string SourceReference,
    IReadOnlyList<ModuleTargetKind> RequiredForTargets,
    DependencyApprovalState ApprovalState,
    DependencyRiskLevel RiskLevel,
    IReadOnlyList<string> ApproverRoles,
    int RequiredApprovals,
    string SecurityAuditReference);

internal sealed record StudioUx8FDistributionDiagnostic(
    string Code,
    string Message);

internal sealed record StudioUx8FDistributionPreview(
    bool Succeeded,
    bool Changed,
    bool CanonicalDryRun,
    bool CanApply,
    string CandidateFingerprint,
    string NoticeSha256,
    string Patch,
    string Inverse,
    IReadOnlyList<StudioUx8FDistributionDiagnostic> Diagnostics,
    string Message);

internal sealed record StudioUx8FDistributionApprovalSnapshot(
    StudioUx8FDependencyDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioUx8FSmokeResult(
    bool Succeeded,
    bool FourRoutes,
    bool DependencyApproval,
    bool ThirdPartyNotices,
    bool CreditsGeneration,
    bool IpBoundary,
    bool Cancellation,
    bool LogsOnDemand,
    bool CanonicalDryRun,
    bool CanonicalCommand,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool ResourcesReleased,
    bool SourceUnchanged,
    string Message);
