using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx2NavFAreaChoice(AreaId AreaId, string Label, bool HasPortals);

internal sealed record StudioUx2NavFPortalChoice(PortalId PortalId, string Label);

internal sealed record StudioUx2NavFEntityChoice(EntityId EntityId, string Label);

internal sealed record StudioUx2NavFDestinationChoice(
    AreaId AreaId,
    EntityId EntityId,
    string Label);

internal sealed record StudioUx2NavFDraft(
    AreaId SourceAreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    PortalId PortalId,
    PortalLink? Original,
    bool IsNew,
    bool Remove,
    string DisplayName,
    EntityId SourceEntityId,
    AreaId DestinationAreaId,
    EntityId DestinationEntityId,
    bool Bidirectional,
    bool PreloadDestination,
    PortalOrientationPolicy Orientation,
    double FixedYawDegrees,
    PortalVelocityPolicy Velocity,
    string BeforeTransitionScript,
    string AfterTransitionScript,
    bool HooksRequiredAtRuntime);

internal sealed record StudioUx2NavFDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2NavFPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string Summary,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx2NavFDiagnostic> Diagnostics,
    bool ForwardResolved,
    bool ReverseResolved,
    bool NoRuntimeExecuted);

internal sealed record StudioUx2NavFActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2NavFApprovalSnapshot(
    StudioUx2NavFDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2NavFEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    IReadOnlyList<EntityId> SelectedEntityIds,
    EntityId? SelectedEntityId,
    StudioUx2NavBApprovalSnapshot NavigationApproval,
    StudioUx2NavCApprovalSnapshot WaypointApproval,
    StudioUx2NavDApprovalSnapshot PatrolApproval,
    StudioUx2NavFApprovalSnapshot PortalApproval);

internal sealed record StudioUx2NavFSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool HumanLabels,
    bool CreateUpdateRemove,
    bool InvalidReferencesBlocked,
    bool HookValidation,
    bool RuntimeResolution,
    bool DryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool AreaNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool SourceUntouched,
    bool HumanText,
    bool Cleanup);
