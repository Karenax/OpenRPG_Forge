using Forge.Application.Commands;
using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed record SetLoreIndexCommand(
    CommandId CommandId,
    Guid TransactionId,
    Guid PlanId,
    LoreIndexRevision BaseRevision,
    LoreIndexDefinition Index,
    AssistantPermissionLevel Permission,
    bool DryRun);
