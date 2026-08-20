using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.RpgKit.Application.Bosses;

namespace Forge.Studio.Session;

internal sealed record StudioBossAreaChoiceUx4R(AreaId AreaId, string Label);
internal sealed record StudioBossBlueprintChoiceUx4R(string Slug, string Label, bool Creature, bool Item);
internal sealed record StudioBossScriptChoiceUx4R(string RelativePath, string Label);

internal sealed record StudioBossDraftUx4R(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    Guid BossId,
    Guid BlueprintId,
    AreaId AreaId,
    string Slug,
    string DisplayName,
    string Synopsis,
    int MaximumHealth,
    int Offense,
    int Defense,
    double ThreatRating,
    IReadOnlyList<string> AbilityNames,
    IReadOnlyList<string> PhaseScripts,
    IReadOnlyList<string> SummonBlueprintSlugs,
    IReadOnlyList<string> RewardItemSlugs,
    string DeathScript,
    IReadOnlyList<string> ContextCitations,
    IReadOnlyList<string> StyleRules,
    string AuthorId,
    int Seed,
    bool Approved);

internal sealed record StudioBossDiagnosticUx4R(string Code, string Path, string HumanMessage);

internal sealed record StudioBossPreviewUx4R(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioBossDiagnosticUx4R> Diagnostics,
    string PackageSummary,
    string BalanceSummary,
    AssistedBossProposal? Proposal);

internal sealed record StudioBossActionResultUx4R(bool Succeeded, string Message);

internal sealed record StudioBossApprovalSnapshotUx4R(
    StudioBossDraftUx4R? Draft,
    AssistedBossProposal? Proposal,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? DraftFingerprint,
    Guid TransactionId,
    Guid ExecutionPlanId);

internal sealed record StudioBossEvidenceProjectUx4R(
    ProjectDefinition Project,
    AreaId AreaId,
    string SummonSlug,
    string RewardSlug,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioBossEvidenceSourceSnapshotUx4R(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool Dirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    IReadOnlyList<EntityId> Selection,
    EntityId? ActiveEntity,
    StudioBossApprovalSnapshotUx4R Approval);

internal sealed record StudioUx4RSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool RealScripts,
    bool Abilities,
    bool Phases,
    bool Balance,
    bool ApprovalRequired,
    bool InvalidScriptBlocked,
    bool DryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);
