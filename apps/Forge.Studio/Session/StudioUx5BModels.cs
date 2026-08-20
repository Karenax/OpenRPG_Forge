using Forge.Application.Assistant;
using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Assistant;
using Forge.Domain.Authoring;
using Forge.Domain.Dialogues;
using Forge.Domain.Localization;
using Forge.Domain.Projects;
using Forge.Domain.Quests;
using Forge.Domain.Sequences;

namespace Forge.Studio.Session;

internal enum StudioNarrativeSectionUx5B
{
    Dialogue,
    Quest,
    Localization,
    Lore,
    Cinematic,
}

internal sealed record StudioNarrativeChoiceUx5B(Guid Id, string Label, bool Complete);

internal sealed record StudioNarrativeDiagnosticUx5B(string Code, string Path, string Message);

internal sealed record StudioNarrativePreviewUx5B(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    IReadOnlyList<StudioNarrativeDiagnosticUx5B> Diagnostics,
    IReadOnlyList<string> Metrics,
    string RuntimeBoundary);

internal sealed record StudioNarrativeActionResultUx5B(bool Succeeded, string Message);

internal sealed record StudioDialogueProductionDraftUx5B(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    DialogueDefinition Source,
    DialogueDefinition Candidate);

internal sealed record StudioQuestProductionDraftUx5B(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    QuestDefinition Source,
    QuestDefinition Candidate);

internal sealed record StudioLocalizationProductionDraftUx5B(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    LocalizationCatalog Source,
    LocalizationCatalog Candidate,
    EditorWorkspaceProfile WorkspaceSource,
    EditorWorkspaceProfile WorkspaceCandidate);

internal sealed record StudioLoreProductionDraftUx5B(
    LoreIndexRevision BaseRevision,
    string SourceProjectFingerprint,
    LoreIndexDefinition Source,
    LoreIndexDefinition Candidate,
    Guid TransactionId,
    Guid PlanId);

internal sealed record StudioCinematicProductionDraftUx5B(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    AreaId AreaId,
    ModuleCinematicProfile ModuleSource,
    AreaCinematicProfile AreaSource,
    ModuleCinematicProfile ModuleCandidate,
    AreaCinematicProfile AreaCandidate);

internal sealed record StudioNarrativeApprovalSnapshotUx5B(
    StudioDialogueProductionDraftUx5B? Dialogue,
    string? DialogueCandidateFingerprint,
    StudioQuestProductionDraftUx5B? Quest,
    string? QuestCandidateFingerprint,
    StudioLocalizationProductionDraftUx5B? Localization,
    string? LocalizationCandidateFingerprint,
    StudioLoreProductionDraftUx5B? Lore,
    string? LoreCandidateFingerprint,
    StudioCinematicProductionDraftUx5B? Cinematic,
    string? CinematicCandidateFingerprint);

internal sealed record StudioUx5BEvidenceProject(
    ProjectDefinition Project,
    DialogueId DialogueId,
    QuestId QuestId,
    AreaId AreaId,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioUx5BSmokeResult(
    bool Succeeded,
    bool DialogueAuthoring,
    bool DialoguePresentation,
    bool DialogueExperience,
    bool DialogueProduction,
    bool DialogueSimulation,
    bool QuestFoundation,
    bool QuestAuthoring,
    bool QuestExperience,
    bool QuestQuality,
    bool QuestSimulation,
    bool LocalizationCatalog,
    bool AccessibleTypography,
    bool LoreIndex,
    bool LoreReferenceGraph,
    bool CinematicStartup,
    bool CinematicTimeline,
    bool ControlLocks,
    bool DryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommands,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);

internal sealed record StudioNarrativeEvidenceSourceSnapshotUx5B(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool Dirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    StudioNarrativeApprovalSnapshotUx5B Approval);
