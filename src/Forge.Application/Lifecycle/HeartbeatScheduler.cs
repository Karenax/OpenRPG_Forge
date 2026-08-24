using Forge.Domain.Areas;
using Forge.Domain.RuntimePolicies;
using Forge.Domain.Validation;

namespace Forge.Application.Lifecycle;

public sealed class HeartbeatScheduler
{
    public const string AggressiveIntervalCode = "runtime.heartbeat.interval_aggressive";
    public const string BacklogTruncatedCode = "runtime.heartbeat.backlog_truncated";
    public const int MaximumCatchUpDispatches = 4;

    private readonly Dictionary<Guid, long> _accumulatedMilliseconds = [];
    private long _elapsedMilliseconds;

    public HeartbeatAdvanceResult Advance(IReadOnlyList<HeartbeatScheduleDefinition> schedules,
        long deltaMilliseconds, bool scopeActive, AreaId? areaId = null)
    {
        ArgumentNullException.ThrowIfNull(schedules);
        if (deltaMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaMilliseconds));
        }

        _elapsedMilliseconds = checked(_elapsedMilliseconds + deltaMilliseconds);
        var dispatches = new List<HeartbeatDispatch>();
        var diagnostics = new List<DomainDiagnostic>();
        foreach (var schedule in schedules.Where(item => item.Enabled).OrderBy(item => item.Id))
        {
            if (!scopeActive && schedule.SuspendWhenInactive)
            {
                continue;
            }

            if (schedule.IntervalMilliseconds < 1_000)
            {
                diagnostics.Add(new DomainDiagnostic(AggressiveIntervalCode,
                    $"Heartbeat '{schedule.Name}' runs faster than once per second.",
                    $"runtimePolicies/heartbeats/{schedule.Id}/intervalMilliseconds"));
            }

            var accumulated = _accumulatedMilliseconds.GetValueOrDefault(schedule.Id) + deltaMilliseconds;
            var due = accumulated / schedule.IntervalMilliseconds;
            var emitted = Math.Min(due, MaximumCatchUpDispatches);
            for (var index = 0; index < emitted; index++)
            {
                var dueAt = _elapsedMilliseconds - accumulated + ((index + 1) * schedule.IntervalMilliseconds);
                dispatches.Add(new HeartbeatDispatch(schedule.Id, schedule.Name, schedule.Scope,
                    schedule.Scope == HeartbeatScope.Area ? areaId : null, dueAt, schedule.BudgetMilliseconds,
                    schedule.Handler, schedule.ScriptPath, schedule.RequiredAtRuntime));
            }

            if (due > MaximumCatchUpDispatches)
            {
                diagnostics.Add(new DomainDiagnostic(BacklogTruncatedCode,
                    $"Heartbeat '{schedule.Name}' dropped {due - MaximumCatchUpDispatches} overdue dispatch(es).",
                    $"runtimePolicies/heartbeats/{schedule.Id}"));
                accumulated %= schedule.IntervalMilliseconds;
            }
            else
            {
                accumulated -= emitted * schedule.IntervalMilliseconds;
            }

            _accumulatedMilliseconds[schedule.Id] = accumulated;
        }

        return new HeartbeatAdvanceResult(dispatches, diagnostics, _elapsedMilliseconds);
    }
}
