using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioUx2LwFEncounterChoice(
    Guid? ProfileId,
    string Label,
    bool IsNew,
    bool IsComplete,
    bool IsEvidence);

internal sealed record StudioUx2LwFReferenceChoice<T>(
    T Id,
    string Label,
    string Provenance);

internal sealed record StudioUx2LwFCandidateDraft(
    Guid Id,
    BlueprintId BlueprintId,
    EncounterWorldProfile.CandidateRole Role,
    double Weight,
    double Cost,
    IReadOnlyList<string> Conditions,
    IReadOnlyList<string> Variants,
    int MaximumAlive);

internal sealed record StudioUx2LwFGroupDraft(
    Guid Id,
    string Slug,
    int Minimum,
    int Maximum,
    double Budget,
    IReadOnlyList<Guid> CandidateIds);

internal sealed record StudioUx2LwFDraft(
    Guid ProfileId,
    bool IsNew,
    AreaId AreaId,
    ProjectRevision BaseRevision,
    string SourceFingerprint,
    string Slug,
    string DisplayName,
    string Category,
    string Theme,
    EncounterWorldProfile.CandidateRole PrimaryRole,
    EncounterWorldProfile.VolumeShape Shape,
    IReadOnlyList<Vector3Value> Points,
    Vector3Value Center,
    double Height,
    double Radius,
    IReadOnlyList<string> RequiredEntityTags,
    IReadOnlyList<string> AllowedFactions,
    bool RequireLineOfSight,
    double ThreatRadius,
    string? ActivationScriptId,
    bool VisualizeInEditor,
    IReadOnlyList<StudioUx2LwFCandidateDraft> Candidates,
    double SpawnBudget,
    IReadOnlyList<string> SpawnAnchorIds,
    IReadOnlyList<string> RewardIds,
    int NetworkActorCap,
    string RulesetId,
    string PartyAssessmentProviderId,
    EncounterDifficultyBand DifficultyBand,
    int ExpectedPartySize,
    double ExpectedPartyPower,
    double BaseBudget,
    double PartySizeMultiplier,
    double DifficultyMultiplier,
    IReadOnlyList<StudioUx2LwFGroupDraft> Groups,
    EncounterCompositionProfile.SelectionPolicy Selection,
    EncounterCompositionProfile.ActivationPolicy Activation,
    EncounterWorldProfile? OriginalProfile);

internal sealed record StudioUx2LwFDiagnostic(string Code, string Path, string Message);

internal sealed record StudioUx2LwFDiff(string Section, string Before, string After, bool Changed);

internal sealed record StudioUx2LwFRuntimePreview(
    string Volume,
    string Pool,
    string Difficulty,
    string Count,
    string Boundary);

internal sealed record StudioUx2LwFPreview(
    bool Succeeded,
    bool CanApply,
    string Message,
    string CanonicalPatch,
    IReadOnlyList<StudioUx2LwFDiff> Diff,
    IReadOnlyList<StudioUx2LwFDiagnostic> Diagnostics,
    StudioUx2LwFRuntimePreview Runtime,
    bool DryRunPure);

internal sealed record StudioUx2LwFActionResult(bool Succeeded, string Message);

internal sealed record StudioUx2LwFApprovalSnapshot(
    StudioUx2LwFDraft? Draft,
    ProjectRevision? Revision,
    string? SourceFingerprint,
    string? DraftFingerprint);

internal sealed record StudioUx2LwFEvidenceSourceSnapshot(
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
    StudioUx2LwFApprovalSnapshot Approval);

internal sealed record StudioUx2LwFSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool NewAssistant,
    bool VolumeValidation,
    bool CandidateValidation,
    bool DifficultyValidation,
    bool CountValidation,
    bool RealScriptValidation,
    bool DeterministicPreview,
    bool DryRunPure,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool TargetNeighborsPreserved,
    bool ProjectNeighborsPreserved,
    bool CollectionPreserved,
    bool StructuredDiagnostics,
    bool HumanText);
