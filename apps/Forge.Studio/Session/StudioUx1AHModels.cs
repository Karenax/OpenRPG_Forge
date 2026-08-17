using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioTriggerAutomationDirectorChoice(
    Guid ProfileId,
    BlueprintId BlueprintId,
    string Label,
    bool HasFoundation,
    bool HasScheduler,
    bool HasCustomEvent,
    bool HasDirector,
    bool IsEvidence);

internal sealed record StudioTriggerAutomationDirectorDraft(
    Guid TriggerProfileId,
    BlueprintId BlueprintId,
    AreaId AreaId,
    ProjectRevision BaseRevision,
    bool SchedulerEnabled,
    double IntervalSeconds,
    int BudgetPerMinute,
    bool WhileOccupiedOnly,
    bool AggregateOccupants,
    string CustomEventNamespace,
    bool DirectorEnabled,
    bool AllowManualEnter,
    bool AllowManualExit,
    bool AllowManualFire,
    bool RequireTarget,
    bool Audit,
    TriggerWorldProfile OriginalProfile);

internal sealed record StudioTriggerAutomationDirectorPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool OccupancyPolicyHonored,
    bool BudgetBlocked,
    bool ScheduledAllowed,
    bool NamespaceValid,
    bool ManualFirePolicyHonored,
    bool AuditRecorded,
    bool LiveDispatchAttempted,
    bool CanApply);

internal sealed record StudioUx1AHActionResult(bool Succeeded, string Message);

internal sealed record StudioUx1AHEvidenceSourceSnapshot(
    ProjectDefinition Project,
    ProjectCommandHistory History,
    ProjectRevision Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool IsDirty,
    bool CanUndo,
    bool CanRedo,
    AreaId? ActiveAreaId,
    IReadOnlyList<EntityId> SelectedEntityIds,
    EntityId? SelectedEntityId);

internal sealed record StudioUx1AHSmokeResult(
    bool Succeeded,
    bool SelectionExplicit,
    bool FoundationRequired,
    bool Scheduler,
    bool CustomEvent,
    bool Director,
    bool RuntimePolicy,
    bool NoLiveDispatch,
    bool DryRun,
    bool InvalidIntervalBlocked,
    bool InvalidBudgetBlocked,
    bool InvalidNamespaceBlocked,
    bool UnsafeDirectorBlocked,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool DiscoveryPreserved,
    bool InteractionPreserved,
    bool AccessPreserved,
    bool FoundationPreserved,
    bool PortalPreserved,
    bool ValidationPreserved,
    bool AdvancedPreserved,
    bool Ux2NeighborsPreserved,
    bool NeighborPreserved,
    bool HumanDiagnostics);
