using Forge.Application.Commands;

namespace Forge.Application.Assistant;

public sealed record AssistedAreaPlanCommandResult(
    bool Succeeded,
    AssistedAreaPlanState State,
    AssistedAreaPlanRevision BaseRevision,
    AssistedAreaPlanRevision? NewRevision,
    Guid TransactionId,
    Guid PlanId,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    ProjectPatch? Patch,
    ProjectPatch? Inverse,
    AuditEntry Audit);
