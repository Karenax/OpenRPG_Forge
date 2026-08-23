using Forge.Application.Assistant;
using Forge.Domain.Assistant;
using Forge.Application.Commands;
using Forge.Application.Projects;

namespace Forge.Studio.Session;

internal enum StudioUx10Mode
{
    Context,
    Plans,
    Safety,
    Creative,
}

internal sealed record StudioUx10Route(
    string Id,
    string Title,
    string Summary);

internal sealed record StudioUx10Draft(
    Guid ProjectId,
    string ModuleName,
    AssistantWorkspaceProfileRevision ProfileRevision,
    string ProfileFingerprint,
    StudioUx10Mode Mode,
    AssistantProviderKind ProviderKind,
    int MaximumContextItems,
    int ReferenceDepth,
    int ReferenceNodes,
    AssistantPermissionLevel MaximumPermission,
    bool NetworkAllowed,
    bool ShareProjectContent,
    AgentProjectClassification Classification,
    bool CloudProjectContentOptIn,
    string VariableKey,
    string VariableValue,
    string UntrustedText,
    string AssetSlug,
    string AssetPrompt,
    bool Consent);

internal sealed record StudioUx10ProfilePreview(
    bool Succeeded,
    bool CanApply,
    bool CanonicalDryRun,
    bool ProviderReady,
    bool ContextReady,
    bool GovernanceReady,
    bool AuditReady,
    string Message,
    IReadOnlyList<string> SemanticDiff,
    IReadOnlyList<string> Diagnostics);

internal sealed record StudioUx10RunReport(
    bool Succeeded,
    StudioUx10Mode Mode,
    IReadOnlyList<StudioUx10Route> Routes,
    bool ExplicitSelection,
    bool CanonicalDryRun,
    bool ApprovalRequired,
    bool TestsPassed,
    bool Reverted,
    bool Audited,
    bool Offline,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased,
    string Summary,
    IReadOnlyList<string> Details,
    IReadOnlyList<string> Diagnostics);

internal sealed record StudioUx10ApprovalSnapshot(
    StudioUx10Draft? Draft,
    string? DraftFingerprint,
    Guid? PlanId);

internal sealed record StudioUx10SmokeResult(
    bool Succeeded,
    bool SeventeenRoutes,
    bool Context,
    bool Plans,
    bool Safety,
    bool Creative,
    bool ExplicitSelection,
    bool CanonicalDryRun,
    bool ApprovalRequired,
    bool TestsPassed,
    bool Reverted,
    bool Audited,
    bool Offline,
    bool SourceUnchanged,
    bool TemporaryWorkspaceReleased);

internal sealed record StudioUx10SourceSnapshot(
    Forge.Domain.Projects.ProjectDefinition? Project,
    ProjectCommandHistory? History,
    ProjectRevision? Revision,
    IReadOnlyList<CommandHistoryJournalEntry> Journal,
    bool Dirty,
    bool CanUndo,
    bool CanRedo,
    Forge.Domain.Areas.AreaId? Area,
    IReadOnlyList<Forge.Domain.Entities.EntityId> Selection,
    Forge.Domain.Entities.EntityId? Selected,
    StudioUx10ApprovalSnapshot Approval);
