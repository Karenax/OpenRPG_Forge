using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Sequences;
using Forge.Studio.Core.Navigation;

namespace Forge.Studio.Session;

internal sealed record StudioUx2NavMAreaChoice(
    AreaId AreaId,
    string Label,
    int AnchorCount,
    bool IsEvidence);

internal sealed record StudioUx2NavMAnchorChoice(
    Guid AnchorId,
    string Label,
    bool IsEvidence);

internal sealed record StudioUx2NavMDraft(
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    Guid AnchorId,
    bool IsNew,
    string Provenance,
    string Name,
    double PositionX,
    double PositionY,
    double PositionZ,
    double RotationX,
    double RotationY,
    double RotationZ,
    double RotationW,
    double ScaleX,
    double ScaleY,
    double ScaleZ,
    double LookAtX,
    double LookAtY,
    double LookAtZ,
    CameraFraming Framing,
    double FieldOfViewDegrees,
    int DefaultDurationMilliseconds,
    double SafeAreaMargin,
    IReadOnlyList<string> Tags,
    AreaCinematicProfile OriginalAreaProfile,
    CameraAnchorDefinition? OriginalAnchor);

internal sealed record StudioUx2NavMDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2NavMDiff(string Section, string Before, string After, bool Changed);

internal sealed record StudioUx2NavMFramingPreview(
    string CameraPose,
    string LookAt,
    string Framing,
    string Lens,
    string Timing,
    string SafeArea,
    string RuntimeConsumption,
    string Boundary,
    StudioNavigationOverlaySnapshot Overlay);

internal sealed record StudioUx2NavMPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string CanonicalPatch,
    IReadOnlyList<StudioUx2NavMDiff> Diff,
    IReadOnlyList<StudioUx2NavMDiagnostic> Diagnostics,
    StudioUx2NavMFramingPreview? Framing,
    bool DryRunPure);

internal sealed record StudioUx2NavMActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2NavMApprovalSnapshot(
    StudioUx2NavMDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx2NavMEvidenceSourceSnapshot(
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
    StudioUx2NavMApprovalSnapshot Approval);

internal sealed record StudioUx2NavMSmokeResult(
    bool Succeeded,
    bool ExplicitAreaAndAnchorSelection,
    bool CreationSurface,
    bool HumanLabels,
    bool TransformValidation,
    bool BoundsValidation,
    bool LensTimingSafeAreaValidation,
    bool DuplicateNameValidation,
    bool OverlayPreview,
    bool RuntimePreview,
    bool DryRunPure,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool ModuleCinematicsPreserved,
    bool TargetAreaNeighborsPreserved,
    bool OtherAnchorsPreserved,
    bool OtherAreasPreserved,
    bool ProjectNeighborsPreserved,
    bool StructuredDiagnostics,
    bool SourceUntouched,
    bool HumanText);
