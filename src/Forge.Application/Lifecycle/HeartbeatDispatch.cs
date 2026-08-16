using Forge.Domain.Areas;
using Forge.Domain.RuntimePolicies;

namespace Forge.Application.Lifecycle;

public sealed record HeartbeatDispatch(Guid ScheduleId, string Name, HeartbeatScope Scope, AreaId? AreaId,
    long DueAtMilliseconds, int BudgetMilliseconds, string Handler, string? ScriptPath = null,
    bool RequiredAtRuntime = false);
