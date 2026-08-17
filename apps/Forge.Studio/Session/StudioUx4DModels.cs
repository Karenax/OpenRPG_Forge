using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioCreatureChoiceUx4D(
    BlueprintId BlueprintId,
    string Slug,
    string Label,
    bool HasFoundation,
    bool HasAuthoringComposition,
    bool IsReadOnly);

internal sealed record StudioCreatureFactionChoiceUx4D(string Value, string Label, bool IsProtected);

internal sealed record StudioCreatureFoundationDraftUx4D(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    BlueprintId BlueprintId,
    BlueprintDefinition SourceBlueprint,
    CreatureFoundationProfile? SourceFoundation,
    bool RequiredAtRuntime,
    string FrenchName,
    string EnglishName,
    string Aliases,
    string Pronouns,
    string FrenchBiography,
    string EnglishBiography,
    string AuthorNotes,
    string NarrativeTags,
    string AssetId,
    string ArchetypeId,
    string RulesetId,
    string RigId,
    string FactionId,
    CreatureAuthoringEncounterRole EncounterRole,
    string ThreatRating,
    string VisualKitId,
    string CombatTags,
    string SpeciesId,
    string ParentSpeciesIds,
    string TraitSets,
    string Variants,
    string RequiredSkeletonId);

internal sealed record StudioCreatureFoundationDiagnosticUx4D(
    string Code,
    string Path,
    string HumanMessage);

internal sealed record StudioCreatureFoundationPreviewUx4D(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioCreatureFoundationDiagnosticUx4D> Diagnostics,
    string IdentitySummary,
    string WizardSummary,
    string SpeciesSummary,
    string RuntimeSummary);

internal sealed record StudioCreatureFoundationActionResultUx4D(bool Succeeded, string Message);

internal sealed record StudioCreatureFoundationApprovalSnapshotUx4D(
    StudioCreatureFoundationDraftUx4D? Draft,
    CreatureFoundationProfile? Candidate,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioCreatureFoundationEvidenceSourceSnapshotUx4D(
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
    StudioCreatureFoundationApprovalSnapshotUx4D Approval);

internal sealed record StudioCreatureFoundationEvidenceProjectUx4D(
    ProjectDefinition Project,
    BlueprintId BlueprintId,
    bool UsesRealCryptSource,
    bool UsesCanonicalFallback);

internal sealed record StudioUx4DSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool FoundationInitialization,
    bool LocalizedIdentity,
    bool Wizard,
    bool RoleAndThreat,
    bool Species,
    bool InvalidConfigurationBlocked,
    bool DryRun,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
