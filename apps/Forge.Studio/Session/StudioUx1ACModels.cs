using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioTriggerAdvancedChoice(
    Guid ProfileId,
    BlueprintId BlueprintId,
    string Label,
    bool HasFoundation,
    bool HasAdvanced);

internal sealed record StudioTriggerFactionChoice(Guid FactionId, string Label);

internal sealed record StudioTriggerAdvancedDraft(
    Guid TriggerProfileId,
    BlueprintId BlueprintId,
    AreaId AreaId,
    ProjectRevision BaseRevision,
    Guid AdvancedId,
    string ForgeScriptApiVersion,
    bool RequireRuntime,
    TriggerTrapProfile Trap,
    TriggerEntityQueryProfile TargetFilter,
    IReadOnlyList<TriggerGameplayVolumeProfile> GameplayVolumes,
    TriggerOccupancyDebugProfile OccupancyDebug,
    TriggerWorldProfile OriginalProfile,
    TriggerAdvancedProfile? OriginalAdvanced);

internal sealed record StudioTriggerAdvancedPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool CanApply);

internal sealed record StudioUx1ACActionResult(bool Succeeded, string Message);

internal sealed record StudioUx1ACEvidenceSourceSnapshot(
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

internal sealed record StudioUx1ACSmokeResult(
    bool Succeeded,
    bool SelectionExplicit,
    bool FoundationRequired,
    bool AdvancedCreation,
    bool Trap,
    bool TargetFilter,
    bool GameplayVolumes,
    bool OccupancyDebug,
    bool DryRun,
    bool InvalidTrapBlocked,
    bool InvalidFilterBlocked,
    bool InvalidVolumeBlocked,
    bool InvalidDebugBlocked,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool RuntimeAdapter,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool FoundationPreserved,
    bool PortalPreserved,
    bool ValidationPreserved,
    bool NeighborPreserved,
    bool HumanDiagnostics);
