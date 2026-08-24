using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioUx2NavGDoorChoice(
    Guid DoorId,
    string Label,
    bool CanEdit,
    string Availability);

internal sealed record StudioUx2NavGDestinationChoice(
    NavigationDestinationKind Kind,
    Guid Id,
    string Label);

internal sealed record StudioUx2NavGArrivalChoice(
    Guid Id,
    string AnchorKey,
    string Label);

internal sealed record StudioUx2NavGDraft(
    Guid DoorId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    DoorWorldProfile OriginalProfile,
    NavigationDestinationKind DestinationKind,
    Guid DestinationId,
    string SpawnAnchorKey,
    double ExitYawDegrees,
    bool Preload,
    IReadOnlyList<string> Permissions,
    bool Bidirectional,
    string ReturnNavigationLabel,
    Guid LinkPolicyId,
    Guid ReverseLinkPolicyId,
    Guid? OriginalTargetDoorId,
    Guid? OriginalLinkPolicyId,
    Guid? OriginalReverseLinkPolicyId,
    string PortalUseForgeScriptApiVersion,
    string ObstacleId,
    string NavigationLinkId,
    IReadOnlyList<string> BlockingStateIds,
    IReadOnlyList<string> TraversableStateIds,
    double OpeningPassableAt,
    double ClosingBlockedAt,
    bool VisualizeInEditor);

internal sealed record StudioUx2NavGDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2NavGPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string Summary,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx2NavGDiagnostic> Diagnostics,
    bool DestinationResolved,
    bool PortalUseAccepted,
    bool PortalUseVetoed,
    bool DirectionValidated,
    bool DynamicNavigationValidated,
    bool NoRuntimeExecuted);

internal sealed record StudioUx2NavGActionResult(bool Succeeded, string Message, bool Stale = false);

internal sealed record StudioUx2NavGApprovalSnapshot(
    StudioUx2NavGDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2NavGEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    StudioUx2NavGApprovalSnapshot Approval);

internal sealed record StudioUx2NavGSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool HumanLabels,
    bool TypedDestination,
    bool AtomicDirection,
    bool PortalUseInPlace,
    bool PortalUseRuntime,
    bool DynamicNavigationRuntime,
    bool InvalidBlocked,
    bool DryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool Ux1DataPreserved,
    bool NeighborLinksPreserved,
    bool SourceUntouched,
    bool Cleanup);
