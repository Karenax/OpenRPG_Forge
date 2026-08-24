using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Dialogues;
using Forge.Domain.Entities;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed record StudioUx2NavCAreaChoice(AreaId AreaId, string Label, bool IsEvidence);

internal sealed record StudioUx2NavCWaypointChoice(Guid WaypointId, string Label, bool HasMarker);

internal sealed record StudioUx2NavCDraft(
    AreaId AreaId,
    Guid WaypointId,
    bool IsNew,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string AreaDisplayName,
    int AreaWidth,
    int AreaDepth,
    WaypointProfile? Original,
    string Name,
    Vector3Value Position,
    string AppearanceIcon,
    string AppearanceColor,
    string AppearanceLayer,
    bool AppearanceVisible,
    double AppearanceScreenSize,
    bool HasMarker,
    string MarkerText,
    string MarkerIcon,
    int MarkerFloor,
    string MarkerCategory,
    double MarkerPrecision,
    MarkerVisibilityPolicy MarkerVisibility,
    MarkerVisibilityScope MarkerScope,
    bool MarkerTracked,
    bool MarkerRevealed,
    string MarkerRevealEvent,
    string MarkerRevealAnimation,
    bool MarkerPersistent);

internal sealed record StudioUx2NavCDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2NavCAppearanceMarker(
    string HumanLabel,
    StudioNavigationPoint Position,
    string Icon,
    string Color,
    string Layer,
    bool Visible,
    double ScreenSize,
    bool Selected);

internal sealed record StudioUx2NavCPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply,
    IReadOnlyList<string> Diff,
    IReadOnlyList<StudioUx2NavCDiagnostic> Diagnostics,
    StudioNavigationOverlaySnapshot Overlay,
    IReadOnlyList<StudioUx2NavCAppearanceMarker> AppearanceMarkers,
    bool MarkerVisibleBeforeReveal,
    bool MarkerVisibleAfterReveal);

internal sealed record StudioUx2NavCActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2NavCApprovalSnapshot(
    StudioUx2NavCDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx2NavCEvidenceSourceSnapshot(
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
    StudioUx2NavCApprovalSnapshot Approval);

internal sealed record StudioUx2NavCSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool HumanLabels,
    bool CreateSupported,
    bool AppearanceValidation,
    bool MarkerValidation,
    bool RevealPolicy,
    bool DryRun,
    bool StaleRevisionBlocked,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool HiddenFieldsPreserved,
    bool WaypointNeighborsPreserved,
    bool NavigationNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool SourceUntouched,
    bool HumanText);
