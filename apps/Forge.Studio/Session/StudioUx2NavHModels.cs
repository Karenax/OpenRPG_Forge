using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioUx2NavHTriggerChoice(
    Guid TriggerId,
    string Label,
    bool CanEdit,
    string Availability);

internal sealed record StudioUx2NavHDestinationChoice(
    NavigationDestinationKind Kind,
    Guid Id,
    string Label);

internal sealed record StudioUx2NavHArrivalChoice(
    string AnchorKey,
    string Label);

internal sealed record StudioUx2NavHDraft(
    Guid TriggerId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    TriggerWorldProfile OriginalProfile,
    bool Enabled,
    NavigationDestinationKind DestinationKind,
    Guid DestinationId,
    string SpawnAnchorKey,
    Vector3Value ExitOrientationDegrees,
    bool Preload,
    IReadOnlyList<string> Permissions);

internal sealed record StudioUx2NavHDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2NavHPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string Summary,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx2NavHDiagnostic> Diagnostics,
    bool DestinationResolved,
    bool TriggerFired,
    bool PermissionPolicyValidated,
    bool ValidationContextSynchronized,
    bool NoExternalRuntimeExecuted);

internal sealed record StudioUx2NavHActionResult(bool Succeeded, string Message, bool Stale = false);

internal sealed record StudioUx2NavHApprovalSnapshot(
    StudioUx2NavHDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx2NavHEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    StudioUx2NavHApprovalSnapshot Approval);

internal sealed record StudioUx2NavHSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool HumanLabels,
    bool FoundationRequired,
    bool TypedDestination,
    bool RuntimeTransition,
    bool PermissionRefusal,
    bool InvalidBlocked,
    bool DryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool Ux1DataPreserved,
    bool ValidationNeighborPreserved,
    bool WorldNeighborsPreserved,
    bool SourceUntouched,
    bool Cleanup);
