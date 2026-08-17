using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioCreatureProgressionChoiceUx4G(
    BlueprintId BlueprintId,
    string Label,
    bool HasProgression,
    bool HasCombatAbilities,
    bool IsReadOnly);

internal sealed record StudioCreatureAbilityChoiceUx4G(string AbilityId, string Label);

internal sealed record StudioCreatureProgressionDraftUx4G(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    BlueprintId BlueprintId,
    BlueprintDefinition SourceBlueprint,
    CreatureProgressionProfile SourceProgression,
    string RulesetId,
    bool AllowMultipleTracks,
    bool RequiredAtRuntime,
    IReadOnlyList<CreatureProgressionProfile.ProgressionTrack> Tracks,
    IReadOnlyList<CreatureProgressionProfile.DispositionAxis> DispositionAxes,
    IReadOnlyList<CreatureProgressionProfile.SkillProgression> Skills,
    IReadOnlyList<CreatureProgressionProfile.TalentProgression> Talents,
    string LoadoutProviderId,
    AbilityLoadoutMode LoadoutMode,
    int LoadoutCapacity,
    IReadOnlyList<string> LearnedAbilityIds,
    IReadOnlyList<CreatureProgressionProfile.PreparedAbility> PreparedAbilities);

internal sealed record StudioCreatureProgressionDiagnosticUx4G(string Code, string Path, string HumanMessage);

internal sealed record StudioCreatureProgressionPreviewUx4G(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioCreatureProgressionDiagnosticUx4G> Diagnostics,
    string TrackSummary,
    string DispositionSummary,
    string SkillSummary,
    string TalentSummary,
    string LoadoutSummary,
    string RuntimeSummary);

internal sealed record StudioCreatureProgressionActionResultUx4G(bool Succeeded, string Message);

internal sealed record StudioCreatureProgressionApprovalSnapshotUx4G(
    StudioCreatureProgressionDraftUx4G? Draft,
    CreatureProgressionProfile? Candidate,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioCreatureProgressionEvidenceSourceSnapshotUx4G(
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
    StudioCreatureProgressionApprovalSnapshotUx4G Approval);

internal sealed record StudioCreatureProgressionEvidenceProjectUx4G(
    ProjectDefinition Project,
    BlueprintId BlueprintId,
    bool UsesRealProfile,
    bool UsesCanonicalFallback);

internal sealed record StudioUx4GSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool Tracks,
    bool Disposition,
    bool Skills,
    bool Talents,
    bool Loadout,
    bool InvalidConfigurationBlocked,
    bool DryRun,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
