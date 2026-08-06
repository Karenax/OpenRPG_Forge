using Forge.Application.Commands;
using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed record SetAssistantWorkspaceProfileCommand(
    CommandId CommandId,
    Guid PlanId,
    AssistantWorkspaceProfileRevision BaseRevision,
    AssistantWorkspaceProfile Profile,
    AssistantPermissionLevel Permission,
    bool DryRun);
