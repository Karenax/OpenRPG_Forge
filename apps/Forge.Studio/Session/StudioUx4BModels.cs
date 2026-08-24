using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Factions;
using Forge.Domain.Projects;

namespace Forge.Studio.Session;

internal sealed record StudioFactionRuntimeDraftUx4B(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    FactionCatalog SourceCatalog,
    bool PersonalConfigured,
    IReadOnlyList<PersonalReputationOverlay> PersonalReputation,
    bool AdvancedConfigured,
    bool RequireAdvancedRuntime,
    IReadOnlyList<FactionWitnessPolicy> WitnessPolicies,
    IReadOnlyList<FactionMembership> Memberships,
    IReadOnlyList<DynamicFactionDefinition> DynamicFactions,
    bool ReputationConfigured,
    bool RequireReputationRuntime,
    IReadOnlyList<ReputationTransaction> ReputationTransactions);

internal sealed record StudioFactionRuntimeDiagnosticUx4B(
    string Code,
    string Path,
    string HumanMessage);

internal sealed record StudioFactionRuntimePreviewUx4B(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioFactionRuntimeDiagnosticUx4B> Diagnostics,
    string PersonalSummary,
    string ReputationSummary,
    string WitnessSummary,
    string MembershipSummary,
    string DynamicSummary);

internal sealed record StudioFactionRuntimeActionResultUx4B(bool Succeeded, string Message);

internal sealed record StudioFactionRuntimeApprovalSnapshotUx4B(
    StudioFactionRuntimeDraftUx4B? Draft,
    FactionCatalog? Candidate,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioFactionRuntimeEvidenceSourceSnapshotUx4B(
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
    StudioFactionRuntimeApprovalSnapshotUx4B Approval);

internal sealed record StudioUx4BSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool PersonalReputation,
    bool GlobalReputation,
    bool WitnessPropagation,
    bool MultipleMemberships,
    bool DynamicFactions,
    bool InvalidConfigurationBlocked,
    bool DryRun,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
