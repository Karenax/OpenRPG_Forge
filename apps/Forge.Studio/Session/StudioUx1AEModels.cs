using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.WorldFeatures;

namespace Forge.Studio.Session;

internal sealed record StudioTriggerDiscoveryAccessChoice(
    Guid ProfileId,
    BlueprintId BlueprintId,
    string Label,
    bool HasFoundation,
    bool HasDiscovery,
    bool HasInteraction,
    bool HasAccess,
    bool IsEvidence);

internal sealed record StudioTriggerRevealTargetChoice(Guid TargetId, string Label, bool Resolved);

internal sealed record StudioTriggerScriptChoice(
    string ScriptId,
    string Label,
    bool AllowedByValidation);

internal sealed record StudioTriggerCredentialChoice(string CredentialId, string Label);

internal sealed record StudioTriggerDiscoveryAccessDraft(
    Guid TriggerProfileId,
    BlueprintId BlueprintId,
    AreaId AreaId,
    ProjectRevision BaseRevision,
    bool DiscoveryEnabled,
    IReadOnlyList<string> Senses,
    int Difficulty,
    Guid RevealTargetId,
    string FeedbackId,
    bool Permanent,
    TriggerWorldProfile.DiscoveryScope Scope,
    bool InteractionEnabled,
    string Verb,
    string Icon,
    string Outline,
    string PromptKey,
    string ScriptId,
    bool Visible,
    string Permission,
    bool AccessEnabled,
    string CredentialId,
    bool ConsumeCredential,
    string DeniedFeedbackKey,
    TriggerWorldProfile OriginalProfile);

internal sealed record StudioTriggerDiscoveryAccessPreview(
    bool Succeeded,
    string Message,
    string Summary,
    bool HiddenBlocked,
    bool CredentialBlocked,
    bool PermissionBlocked,
    bool Allowed,
    bool CanApply);

internal sealed record StudioUx1AEActionResult(bool Succeeded, string Message);

internal sealed record StudioUx1AEEvidenceSourceSnapshot(
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

internal sealed record StudioUx1AESmokeResult(
    bool Succeeded,
    bool SelectionExplicit,
    bool FoundationRequired,
    bool Discovery,
    bool Interaction,
    bool Access,
    bool RuntimePreview,
    bool DryRun,
    bool InvalidDiscoveryBlocked,
    bool InvalidInteractionBlocked,
    bool InvalidAccessBlocked,
    bool ValidationScriptBlocked,
    bool StaleDraftBlocked,
    bool StaleApprovalBlocked,
    bool CanonicalCommand,
    bool UndoRedo,
    bool Persistence,
    bool FoundationPreserved,
    bool PortalPreserved,
    bool ValidationPreserved,
    bool AdvancedPreserved,
    bool Ux2NeighborsPreserved,
    bool NeighborPreserved,
    bool HumanDiagnostics);
