using Forge.Application.Commands;
using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed record SetAssistedAreaPlansCommand(
    CommandId CommandId,
    Guid TransactionId,
    Guid PlanId,
    AssistedAreaPlanRevision BaseRevision,
    AssistedAreaPlanCatalog Catalog,
    AssistantPermissionLevel Permission,
    bool DryRun);
