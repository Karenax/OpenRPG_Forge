using Forge.Application.Commands;
using Forge.Application.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioUx9MProductionDraft(
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    bool ForcedRestEnabled,
    int MaximumRestTargets,
    bool LevelAdjustmentEnabled,
    int MaximumLevelDelta,
    bool RequireRollbackSnapshot,
    bool AppearanceEnabled,
    double MinimumScale,
    double MaximumScale,
    bool HasSavedGroup,
    string GroupKey,
    string GroupQuery,
    int GroupHotkey,
    int MaximumGroupMembers,
    bool RecordingEnabled,
    string ExportFormat,
    int MaximumRecordingMinutes,
    bool RedactPrivateData);

internal sealed record StudioUx9MProductionProbe(
    string Status,
    string RestCode,
    bool RestPolicyRespected,
    string LevelCode,
    bool LevelPolicyRespected,
    bool RollbackSnapshot,
    string AppearanceCode,
    bool AppearancePolicyRespected,
    string RecordingCode,
    bool RecordingPolicyRespected,
    int RecordedMembers,
    string ExportFormat,
    bool PrivateDataRedacted,
    string PermissionCode,
    string ConfirmationCode,
    string ReasonCode,
    bool ResourcesReleased);

internal sealed record StudioUx9MProductionPreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool RuntimeReady,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics,
    StudioUx9MProductionProbe Probe);

internal sealed record StudioUx9MActionResult(bool Succeeded, string Message);

internal sealed record StudioUx9MApprovalSnapshot(
    StudioUx9MProductionDraft? Draft,
    ProjectRevision? Revision,
    string? Fingerprint);

internal sealed record StudioUx9MSmokeResult(
    bool Succeeded,
    bool FiveRoutes,
    bool ExplicitSelection,
    bool InvalidBlocked,
    bool PermissionBlocked,
    bool ConfirmationRequired,
    bool CanonicalDryRun,
    bool CanonicalCommand,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool UndoRedo,
    bool Persistence,
    bool ForcedRest,
    bool LevelAdjustment,
    bool Appearance,
    bool SavedGroup,
    bool Recording,
    bool Privacy,
    bool ProjectNeighborsPreserved,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);

internal sealed record StudioUx9MSourceSnapshot(
    Forge.Domain.Projects.ProjectDefinition? Project,
    ProjectCommandHistory? History,
    ProjectRevision? Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool Dirty,
    bool CanUndo,
    bool CanRedo,
    Forge.Domain.Areas.AreaId? Area,
    IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
    Forge.Domain.Entities.EntityId? Selected,
    StudioUx9MApprovalSnapshot Approval);
