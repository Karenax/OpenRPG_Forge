using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Creatures;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioCreatureRulesChoiceUx4F(
    BlueprintId BlueprintId,
    string Label,
    bool HasFoundation,
    bool HasAuthoringComposition,
    bool HasRulesComposition,
    bool IsReadOnly);

internal sealed record StudioCreatureRulesDraftUx4F(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    BlueprintId BlueprintId,
    BlueprintDefinition SourceBlueprint,
    CreatureFoundationProfile SourceFoundation,
    string RetargetProfileId,
    IReadOnlyList<CreatureAnimationState> AnimationStates,
    IReadOnlyList<CreatureAnimationTransition> AnimationTransitions,
    CreatureRulesInitialLifeState InitialLifeState,
    string? InitialAnimationLoopAssetId,
    string? InitialPoseId,
    bool InitiallyInvisible,
    IReadOnlyList<CreatureFoundationResourcePool> Resources,
    string RulesetId,
    IReadOnlyDictionary<string, double> BaseAttributes,
    IReadOnlyList<CreatureRulesAttributeModifier> AttributeModifiers,
    IReadOnlyList<CreatureRulesDerivedAttribute> DerivedAttributes,
    double Avoidance,
    double Armor,
    IReadOnlyDictionary<string, double> Resistances,
    IReadOnlyDictionary<string, double> Saves,
    IReadOnlyList<string> Immunities,
    double FlatMitigation);

internal sealed record StudioCreatureRulesDiagnosticUx4F(string Code, string Path, string HumanMessage);

internal sealed record StudioCreatureRulesPreviewUx4F(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioCreatureRulesDiagnosticUx4F> Diagnostics,
    string AnimationSummary,
    string InitialStateSummary,
    string ResourceSummary,
    string AttributeSummary,
    string DefenseSummary,
    string RuntimeSummary);

internal sealed record StudioCreatureRulesActionResultUx4F(bool Succeeded, string Message);

internal sealed record StudioCreatureRulesApprovalSnapshotUx4F(
    StudioCreatureRulesDraftUx4F? Draft,
    CreatureFoundationProfile? Candidate,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioCreatureRulesEvidenceSourceSnapshotUx4F(
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
    StudioCreatureRulesApprovalSnapshotUx4F Approval);

internal sealed record StudioCreatureRulesEvidenceProjectUx4F(
    ProjectDefinition Project,
    BlueprintId BlueprintId,
    bool UsesRealProfile,
    bool UsesCanonicalFallback);

internal sealed record StudioUx4FSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool AnimationGraph,
    bool InitialState,
    bool Resources,
    bool Attributes,
    bool Defenses,
    bool InvalidConfigurationBlocked,
    bool DryRun,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
