using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioCreatureRoleBehaviorChoiceUx4M(
    BlueprintId BlueprintId,
    string Label,
    bool HasProfile,
    bool IsReadOnly);

internal sealed record StudioCreatureRoleBehaviorDraftUx4M(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    BlueprintId BlueprintId,
    BlueprintDefinition SourceBlueprint,
    CreatureRoleBehaviorProfile? SourceProfile,
    CreatureProgressionProfile? SourceProgression,
    bool Enabled,
    bool RequiredAtRuntime,
    string RulesetTemplateId,
    string LoreFields,
    string RoleTags,
    string AmbientGraphId,
    string IdleSets,
    string Schedules,
    string Interactions,
    bool InterruptOnCombat,
    bool InterruptOnConversation,
    bool InterruptOnMovement,
    AmbientResumeMode ResumeMode,
    string ResumeDelaySeconds,
    string AutoLevelTemplateId,
    string AutoLevelSeed,
    string TargetLevel,
    bool ApplyOnSave,
    bool ManualLock,
    string TrackAllocations,
    string SkillAllocations,
    string TalentUnlocks,
    string Overrides);

internal sealed record StudioCreatureRoleBehaviorCandidateUx4M(CreatureRoleBehaviorProfile? Profile);

internal sealed record StudioCreatureRoleBehaviorDiagnosticUx4M(
    string Code,
    string Path,
    string HumanMessage);

internal sealed record StudioCreatureRoleBehaviorPreviewUx4M(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioCreatureRoleBehaviorDiagnosticUx4M> Diagnostics,
    string RoleSummary,
    string AmbientSummary,
    string AutoLevelSummary,
    string RuntimeSummary);

internal sealed record StudioCreatureRoleBehaviorActionResultUx4M(bool Succeeded, string Message);

internal sealed record StudioCreatureRoleBehaviorApprovalSnapshotUx4M(
    StudioCreatureRoleBehaviorDraftUx4M? Draft,
    StudioCreatureRoleBehaviorCandidateUx4M? Candidate,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioCreatureRoleBehaviorEvidenceProjectUx4M(
    ProjectDefinition Project,
    BlueprintId BlueprintId,
    bool UsesRealProfile,
    bool UsesCanonicalFallback);

internal sealed record StudioCreatureRoleBehaviorEvidenceSourceSnapshotUx4M(
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
    StudioCreatureRoleBehaviorApprovalSnapshotUx4M Approval);

internal sealed record StudioUx4MSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RoleAndLore,
    bool AmbientGraph,
    bool AutoLevel,
    bool OptionalRemoval,
    bool InvalidConfigurationBlocked,
    bool DuplicateBlocked,
    bool DryRun,
    bool RuntimeProjection,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
