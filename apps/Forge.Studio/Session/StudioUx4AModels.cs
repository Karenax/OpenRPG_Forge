using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Factions;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal enum StudioFactionOperationUx4A
{
    Edit,
    Archive,
    Remove,
}

internal sealed record StudioFactionChoiceUx4A(
    Guid FactionId,
    string Slug,
    string Label,
    bool Archived,
    int ParentCount);

internal sealed record StudioFactionRelationshipDraftUx4A(
    Guid SourceId,
    Guid TargetId,
    string Score,
    string Preset,
    string Explanation);

internal sealed record StudioDispositionBandDraftUx4A(
    string Id,
    string Minimum,
    string Maximum,
    string Priority);

internal sealed record StudioFactionFoundationDraftUx4A(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    FactionCatalog SourceCatalog,
    Guid WorkingFactionId,
    bool IsNew,
    StudioFactionOperationUx4A Operation,
    string Slug,
    string DisplayName,
    bool Archived,
    IReadOnlyList<Guid> ParentIds,
    bool InheritTags,
    bool InheritPolicies,
    bool InheritRelationships,
    Guid AdvancedProfileId,
    Guid InheritancePolicyId,
    Guid? MigrationTargetId,
    IReadOnlyList<StudioFactionRelationshipDraftUx4A> Relationships,
    IReadOnlyList<StudioDispositionBandDraftUx4A> Bands);

internal sealed record StudioFactionImpactUx4A(
    int ChildParents,
    int Relationships,
    int LegacyMembers,
    int PersonalReputation,
    int InheritancePolicies,
    int WitnessPolicies,
    int AdvancedMemberships,
    int DynamicFactions,
    int DiplomacyStates,
    int ReputationTransactions,
    int HeatmapRelationships)
{
    internal int Total => ChildParents + Relationships + LegacyMembers + PersonalReputation +
                          InheritancePolicies + WitnessPolicies + AdvancedMemberships + DynamicFactions +
                          DiplomacyStates + ReputationTransactions + HeatmapRelationships;
}

internal sealed record StudioFactionDiagnosticUx4A(
    string Code,
    string Path,
    string HumanMessage);

internal sealed record StudioFactionPreviewUx4A(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    StudioFactionImpactUx4A Impact,
    IReadOnlyList<StudioFactionDiagnosticUx4A> Diagnostics,
    string DirectionalSummary,
    string BandSummary);

internal sealed record StudioFactionActionResultUx4A(bool Succeeded, string Message);

internal sealed record StudioFactionApprovalSnapshotUx4A(
    StudioFactionFoundationDraftUx4A? Draft,
    FactionCatalog? Candidate,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioFactionEvidenceSourceSnapshotUx4A(
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
    StudioFactionApprovalSnapshotUx4A Approval);

internal sealed record StudioFactionEvidenceProjectUx4A(
    ProjectDefinition Project,
    bool UsesRealCryptSource,
    bool UsesCanonicalFallback);

internal sealed record StudioUx4ASmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool Creation,
    bool Hierarchy,
    bool DirectionalMatrix,
    bool Bands,
    bool InvalidConfigurationBlocked,
    bool DryRun,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool TypedDraftFingerprint,
    bool CanonicalCommand,
    bool NullPersonalPreserved,
    bool Archive,
    bool SafeRemoval,
    bool ExternalReferenceBlocked,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved);
