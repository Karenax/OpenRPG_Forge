using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Factions;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioFactionMatrixDraftUx4C(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    FactionCatalog SourceCatalog,
    Guid FocusFactionId,
    IReadOnlyDictionary<Guid, IReadOnlyList<string>> TagsByFaction,
    IReadOnlyList<FactionRelationship> Relationships,
    bool ReputationConfigured,
    bool RequireReputationRuntime,
    IReadOnlyList<FactionHeatmapSnapshot> HeatmapHistory,
    FactionReactionPolicy Reaction,
    string SubjectId,
    Guid ReactionSourceId,
    Guid ReactionTargetId,
    double DisguiseModifier,
    double CrimeModifier,
    double DialogueModifier,
    double? ZoneOverride,
    double? GameMasterOverride);

internal sealed record StudioFactionMatrixDiagnosticUx4C(string Code, string Path, string HumanMessage);

internal sealed record StudioFactionMatrixPreviewUx4C(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioFactionMatrixDiagnosticUx4C> Diagnostics,
    string TagsSummary,
    string MatrixSummary,
    string ReactionSummary,
    string ValidationSummary);

internal sealed record StudioFactionMatrixActionResultUx4C(bool Succeeded, string Message);

internal sealed record StudioFactionMatrixApprovalSnapshotUx4C(
    StudioFactionMatrixDraftUx4C? Draft,
    FactionCatalog? Candidate,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioFactionMatrixEvidenceSourceSnapshotUx4C(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    Forge.Domain.Areas.AreaId? ActiveAreaId,
    IReadOnlyList<Forge.Domain.Entities.EntityId> SelectedEntityIds,
    Forge.Domain.Entities.EntityId? SelectedEntityId,
    StudioFactionMatrixApprovalSnapshotUx4C Approval);

internal sealed record StudioUx4CSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool SemanticTags,
    bool MatrixInterface,
    bool PlayerReaction,
    bool FactionValidation,
    bool ImportExport,
    bool InvalidConfigurationBlocked,
    bool DryRun,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
