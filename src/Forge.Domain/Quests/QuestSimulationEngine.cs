using Forge.Domain.Validation;

namespace Forge.Domain.Quests;

public enum QuestSimulationStatus
{
    Completed,
    AwaitingEvent,
    Deadlocked,
    StepLimitReached,
}

public sealed record QuestSimulationStep(
    Guid Id,
    QuestSimulationEvent Event,
    QuestStateId BeforeStateId,
    QuestStateId AfterStateId,
    QuestTransitionId? TransitionId,
    IReadOnlyList<QuestAction> Effects,
    IReadOnlyList<DomainDiagnostic> Diagnostics,
    string? IdempotenceKey);

public sealed record QuestSimulationResult(
    Guid ScenarioId,
    QuestSimulationStatus Status,
    QuestInstance Instance,
    IReadOnlyList<QuestSimulationStep> Steps,
    IReadOnlyList<QuestTransitionAuditEntry> Audit,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class QuestSimulationEngine
{
    public const string EventUnhandledCode = "quest.simulation.event_unhandled";
    public const string EventIdempotentCode = "quest.simulation.event_idempotent";
    public const string StepLimitCode = "quest.simulation.step_limit";
    public const string DeadlockCode = "quest.simulation.deadlock";

    public static QuestSimulationResult Run(
        QuestDefinition quest,
        QuestProgressScope scope,
        string scopeOwnerId,
        DateTimeOffset startedAt,
        bool authorityGranted = true,
        Guid? instanceId = null)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var validation = QuestValidator.Validate(quest);
        if (validation.Count > 0) throw new ArgumentException(validation[0].Code, nameof(quest));
        var profile = quest.Simulation;
        var instance = QuestInstanceService.Create(quest, scope, scopeOwnerId, startedAt, authorityGranted,
            instanceId);
        if (profile.InitialVariables.Count > 0)
            instance = CopyInstance(instance, profile.InitialVariables, instance.UpdatedAt);

        var steps = new List<QuestSimulationStep>();
        var audit = new List<QuestTransitionAuditEntry>();
        var diagnostics = new List<DomainDiagnostic>();
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        var orderedEvents = profile.Events.OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).ToArray();
        var count = Math.Min(profile.MaximumSteps, orderedEvents.Length);
        for (var index = 0; index < count; index++)
        {
            var simulationEvent = orderedEvents[index];
            var before = instance.CurrentStateId;
            var variables = Merge(instance.Variables, simulationEvent.Variables);
            instance = CopyInstance(instance, variables, simulationEvent.OccurredAt);
            var evaluator = new VariableEvaluator(variables);
            var candidates = quest.Transitions.Where(item => item.FromStateId == before &&
                                                              string.Equals(item.EventBinding.EventName,
                                                                  simulationEvent.EventName,
                                                                  StringComparison.OrdinalIgnoreCase) &&
                                                              item.Conditions.All(evaluator.IsSatisfied))
                .OrderByDescending(item => item.Priority).ThenBy(item => item.DisplayOrder)
                .ThenBy(item => item.Id.Value).ToArray();
            var selected = candidates.FirstOrDefault(item => !IsConsumed(item, simulationEvent, instance, consumed));
            if (selected is null)
            {
                var code = candidates.Length == 0 ? EventUnhandledCode : EventIdempotentCode;
                var diagnostic = new DomainDiagnostic(code,
                    $"Simulation event '{simulationEvent.EventName}' did not advance quest state '{before}'.",
                    $"quests/{quest.Id}/simulation/events/{simulationEvent.Id}");
                diagnostics.Add(diagnostic);
                steps.Add(new QuestSimulationStep(StableId($"{profile.Id}:{simulationEvent.Id}:none"),
                    simulationEvent, before, before, null, [], [diagnostic], null));
                continue;
            }

            var key = ResolveIdempotenceKey(selected, simulationEvent, instance);
            if (key.Length > 0) consumed.Add(key);
            var actionUpdates = ApplyActions(selected.Actions, variables);
            instance = QuestInstanceService.Advance(quest, instance, selected.Id, evaluator,
                simulationEvent.OccurredAt, simulationEvent.ActorId, actionUpdates, authorityGranted);
            var auditEntry = new QuestTransitionAuditEntry(
                StableId($"{profile.Id}:{simulationEvent.Id}:{selected.Id}"), selected.Id, simulationEvent.Id,
                before, instance.CurrentStateId, simulationEvent.OccurredAt, simulationEvent.ActorId, key,
                selected.Actions, selected.Provenance);
            audit.Add(auditEntry);
            steps.Add(new QuestSimulationStep(auditEntry.Id, simulationEvent, before, instance.CurrentStateId,
                selected.Id, selected.Actions, [], key));
        }

        QuestSimulationStatus status;
        var current = quest.States.Single(item => item.Id == instance.CurrentStateId);
        if (current.IsTerminal)
            status = QuestSimulationStatus.Completed;
        else if (orderedEvents.Length > profile.MaximumSteps)
        {
            status = QuestSimulationStatus.StepLimitReached;
            diagnostics.Add(new DomainDiagnostic(StepLimitCode,
                $"Simulation exceeded its {profile.MaximumSteps} step limit.",
                $"quests/{quest.Id}/simulation/maximumSteps"));
        }
        else if (!quest.Transitions.Any(item => item.FromStateId == current.Id))
        {
            status = QuestSimulationStatus.Deadlocked;
            diagnostics.Add(new DomainDiagnostic(DeadlockCode,
                $"Simulation reached non-terminal state '{current.Id}' without an outgoing transition.",
                $"quests/{quest.Id}/states/{current.Id}"));
        }
        else
            status = QuestSimulationStatus.AwaitingEvent;

        return new QuestSimulationResult(profile.Id, status, instance, steps, audit, diagnostics);
    }

    private static bool IsConsumed(QuestTransition transition, QuestSimulationEvent simulationEvent,
        QuestInstance instance, ISet<string> consumed)
    {
        var key = ResolveIdempotenceKey(transition, simulationEvent, instance);
        return key.Length > 0 && consumed.Contains(key);
    }

    private static string ResolveIdempotenceKey(QuestTransition transition, QuestSimulationEvent simulationEvent,
        QuestInstance instance)
    {
        var prefix = transition.IdempotenceKey.Length == 0 ? transition.Id.ToString() : transition.IdempotenceKey;
        return transition.Idempotence switch
        {
            QuestTransitionIdempotence.None => string.Empty,
            QuestTransitionIdempotence.PerInstance => $"{prefix}:instance:{instance.Id}",
            QuestTransitionIdempotence.PerActor => $"{prefix}:actor:{simulationEvent.ActorId}",
            QuestTransitionIdempotence.PerEvent => $"{prefix}:event:{simulationEvent.Id}",
            _ => throw new ArgumentOutOfRangeException(nameof(transition)),
        };
    }

    private static IReadOnlyDictionary<string, string> ApplyActions(
        IReadOnlyList<QuestAction> actions,
        IReadOnlyDictionary<string, string> variables)
    {
        var updates = new SortedDictionary<string, string>(
            variables.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
        foreach (var action in actions)
        {
            switch (action.Kind)
            {
                case QuestActionKind.SetVariable:
                    updates[action.Key] = action.Value;
                    break;
                case QuestActionKind.GiveItem:
                    updates[$"item:{action.Key}"] = action.Value;
                    break;
                case QuestActionKind.UnlockEntity:
                    updates[$"entity:{action.Key}"] = "unlocked";
                    break;
                default:
                    throw new InvalidOperationException($"quest.simulation.action_kind_unsupported:{action.Kind}");
            }
        }
        return updates;
    }

    private static SortedDictionary<string, string> Merge(
        IReadOnlyDictionary<string, string> current,
        IReadOnlyDictionary<string, string> incoming)
    {
        var result = new SortedDictionary<string, string>(
            current.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            StringComparer.Ordinal);
        foreach (var item in incoming) result[item.Key] = item.Value;
        return result;
    }

    private static QuestInstance CopyInstance(QuestInstance source, IReadOnlyDictionary<string, string> variables,
        DateTimeOffset updatedAt) => new(source.Id, source.SchemaVersion, source.Provenance, source.QuestId,
        source.Scope, source.ScopeOwnerId, source.CurrentStateId, source.History, variables, source.CreatedAt,
        updatedAt, updatedAt, source.Revision);

    private static Guid StableId(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        Span<byte> id = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(id);
        id[6] = (byte)((id[6] & 0x0f) | 0x40);
        id[8] = (byte)((id[8] & 0x3f) | 0x80);
        return new Guid(id);
    }

    private sealed class VariableEvaluator(IReadOnlyDictionary<string, string> variables) : IQuestConditionEvaluator
    {
        public bool IsSatisfied(QuestCondition condition) => condition.Kind switch
        {
            QuestConditionKind.VariableEquals => variables.TryGetValue(condition.Key, out var value) &&
                                                  string.Equals(value, condition.ExpectedValue,
                                                      StringComparison.Ordinal),
            QuestConditionKind.HasItem => variables.TryGetValue($"item:{condition.Key}", out var value) &&
                                          !string.Equals(value, "0", StringComparison.Ordinal) &&
                                          !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }
}
