using Forge.Application.Commands;

namespace Forge.Application.Assistant;

public sealed record LoreIndexCommandResult(
    bool Succeeded,
    LoreIndexState State,
    LoreIndexRevision BaseRevision,
    LoreIndexRevision? NewRevision,
    Guid TransactionId,
    Guid PlanId,
    IReadOnlyList<CommandDiagnostic> Diagnostics,
    ProjectPatch? Patch,
    ProjectPatch? Inverse,
    AuditEntry Audit);
