using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioMerchantChoiceUx4T(Guid MerchantId, string Label, bool Complete);

internal sealed record StudioMerchantBlueprintChoiceUx4T(BlueprintId BlueprintId, string Label);

internal sealed record StudioMerchantDialogueChoiceUx4T(Guid DialogueId, string Label);

internal sealed record StudioMerchantDraftUx4T(
    ProjectRevision BaseRevision,
    string SourceProjectFingerprint,
    Guid MerchantId,
    MerchantWorldProfile? Original,
    MerchantWorldProfile Profile);

internal sealed record StudioMerchantDiagnosticUx4T(string Code, string Path, string HumanMessage);

internal sealed record StudioMerchantPreviewUx4T(
    bool Succeeded,
    bool CanApply,
    string Message,
    string PatchSummary,
    string InverseSummary,
    IReadOnlyList<StudioMerchantDiagnosticUx4T> Diagnostics,
    string StoreSummary,
    string EconomySummary,
    string TransactionSummary,
    string ExperienceSummary,
    string RuntimeBoundary);

internal sealed record StudioMerchantActionResultUx4T(bool Succeeded, string Message);

internal sealed record StudioMerchantApprovalSnapshotUx4T(
    StudioMerchantDraftUx4T? Draft,
    ProjectRevision? Revision,
    string? ProjectFingerprint,
    string? CandidateFingerprint);

internal sealed record StudioMerchantEvidenceProjectUx4T(
    ProjectDefinition Project,
    Guid MerchantId,
    bool UsesRealModule,
    bool UsesCanonicalFallback);

internal sealed record StudioUx4TSmokeResult(
    bool Succeeded,
    bool ExplicitSelection,
    bool StoreWizard,
    bool StockPricing,
    bool TreasuryPurchase,
    bool StolenRestrictions,
    bool RestockEvents,
    bool AtomicTransactions,
    bool CurrenciesNegotiationMarket,
    bool ServicesInterfaceAudit,
    bool Validation,
    bool DryRun,
    bool StaleSourceBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool NeighborsPreserved,
    bool TemporaryWorkspaceReleased);

internal sealed record StudioMerchantEvidenceSourceSnapshotUx4T(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool Dirty,
    bool CanUndo,
    bool CanRedo,
    Forge.Domain.Areas.AreaId? ActiveAreaId,
    IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
    Forge.Domain.Entities.EntityId? ActiveEntity,
    StudioMerchantApprovalSnapshotUx4T Approval);
