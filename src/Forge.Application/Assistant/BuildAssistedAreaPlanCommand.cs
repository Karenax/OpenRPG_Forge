using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed record BuildAssistedAreaPlanCommand(
    CommandId CommandId,
    Guid TransactionId,
    Guid ExecutionPlanId,
    ProjectRevision BaseRevision,
    Guid AssistedAreaPlanId,
    AssistantPermissionLevel Permission,
    bool DryRun);
