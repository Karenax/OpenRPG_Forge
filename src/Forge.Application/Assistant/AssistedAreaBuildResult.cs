using Forge.Application.Commands;
using Forge.Domain.Entities;

namespace Forge.Application.Assistant;

public sealed record AssistedAreaBuildResult(
    bool Succeeded,
    CommandResult Command,
    Guid TransactionId,
    Guid ExecutionPlanId,
    Guid AssistedAreaPlanId,
    IReadOnlyList<EntityId> EntityIds,
    string SemanticDiff,
    int NavigationDiagnosticsBefore,
    int NavigationDiagnosticsAfter,
    bool LightingPass,
    IReadOnlyList<CommandDiagnostic> Diagnostics);
