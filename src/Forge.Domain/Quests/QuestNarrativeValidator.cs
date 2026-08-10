using Forge.Domain.Validation;

namespace Forge.Domain.Quests;

public static class QuestNarrativeValidator
{
    public const string DeadlockCode = "quest.narrative.deadlock";
    public const string CycleWithoutExitCode = "quest.narrative.cycle_without_exit";
    public const string TransitionConflictCode = "quest.narrative.transition_conflict";
    public const string RepeatedRewardCode = "quest.narrative.repeated_reward";
    public const string ImpossibleObjectiveCode = "quest.narrative.objective_impossible";

    public static IReadOnlyList<DomainDiagnostic> Validate(QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var result = new List<DomainDiagnostic>();
        var states = quest.States.ToDictionary(item => item.Id);
        var outgoing = quest.Transitions.Where(item => states.ContainsKey(item.FromStateId) &&
                                                       states.ContainsKey(item.ToStateId))
            .GroupBy(item => item.FromStateId).ToDictionary(group => group.Key, group => group.ToArray());
        var reachableFromInitial = ComputeReachableFromInitial(quest, outgoing);

        foreach (var state in quest.States.Where(item => reachableFromInitial.Contains(item.Id) &&
                                                         !item.IsTerminal && !outgoing.ContainsKey(item.Id)))
        {
            result.Add(new DomainDiagnostic(DeadlockCode,
                $"Non-terminal quest state '{state.Id}' has no outgoing transition.",
                $"quests/{quest.Id}/states/{state.Id}"));
        }

        var canReachTerminal = ComputeStatesThatReachTerminal(quest, states);
        foreach (var state in quest.States.Where(item => reachableFromInitial.Contains(item.Id) &&
                                                         !item.IsTerminal &&
                                                         !canReachTerminal.Contains(item.Id) &&
                                                         outgoing.ContainsKey(item.Id)))
        {
            result.Add(new DomainDiagnostic(CycleWithoutExitCode,
                $"Quest state '{state.Id}' belongs to a path that cannot reach a terminal state.",
                $"quests/{quest.Id}/states/{state.Id}"));
        }

        foreach (var state in quest.States.Where(item => reachableFromInitial.Contains(item.Id) &&
                                                         item.Journal.Objectives.Count > 0 &&
                                                         !canReachTerminal.Contains(item.Id)))
        {
            result.Add(new DomainDiagnostic(ImpossibleObjectiveCode,
                $"Objectives attached to quest state '{state.Id}' cannot reach a terminal outcome.",
                $"quests/{quest.Id}/states/{state.Id}/journal/objectives"));
        }

        foreach (var conflict in quest.Transitions
                     .GroupBy(item => new
                     {
                         item.FromStateId,
                         Event = item.EventBinding.EventName.ToUpperInvariant(),
                         item.Priority,
                         Conditions = ConditionSignature(item.Conditions),
                     })
                     .Where(group => group.Select(item => item.ToStateId).Distinct().Count() > 1))
        {
            foreach (var transition in conflict)
            {
                result.Add(new DomainDiagnostic(TransitionConflictCode,
                    $"Quest transition '{transition.Id}' competes with another equally ranked transition.",
                    $"quests/{quest.Id}/transitions/{transition.Id}/priority"));
            }
        }

        foreach (var transition in quest.Transitions)
        {
            var duplicateRewards = transition.Actions.Where(item => item.Kind == QuestActionKind.GiveItem)
                .GroupBy(item => item.Key, StringComparer.Ordinal).Where(group => group.Count() > 1)
                .Select(group => group.Key).ToArray();
            var isCycleEdge = CanReach(transition.ToStateId, transition.FromStateId, outgoing);
            var unsafeCycleReward = isCycleEdge && transition.Idempotence == QuestTransitionIdempotence.None &&
                                    transition.Actions.Any(item => item.Kind == QuestActionKind.GiveItem);
            if (duplicateRewards.Length == 0 && !unsafeCycleReward) continue;
            result.Add(new DomainDiagnostic(RepeatedRewardCode,
                $"Quest transition '{transition.Id}' can grant a reward more than once without an idempotence guard.",
                $"quests/{quest.Id}/transitions/{transition.Id}/actions"));
        }

        return result;
    }

    private static HashSet<QuestStateId> ComputeStatesThatReachTerminal(
        QuestDefinition quest,
        IReadOnlyDictionary<QuestStateId, QuestStateDefinition> states)
    {
        var reverse = quest.Transitions.Where(item => states.ContainsKey(item.FromStateId) &&
                                                      states.ContainsKey(item.ToStateId))
            .GroupBy(item => item.ToStateId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.FromStateId).Distinct().ToArray());
        var reachable = quest.States.Where(item => item.IsTerminal).Select(item => item.Id).ToHashSet();
        var pending = new Queue<QuestStateId>(reachable);
        while (pending.TryDequeue(out var current))
        {
            if (!reverse.TryGetValue(current, out var predecessors)) continue;
            foreach (var predecessor in predecessors)
                if (reachable.Add(predecessor)) pending.Enqueue(predecessor);
        }
        return reachable;
    }

    private static HashSet<QuestStateId> ComputeReachableFromInitial(
        QuestDefinition quest,
        IReadOnlyDictionary<QuestStateId, QuestTransition[]> outgoing)
    {
        var reachable = new HashSet<QuestStateId>();
        var pending = new Queue<QuestStateId>();
        pending.Enqueue(quest.InitialStateId);
        while (pending.TryDequeue(out var current))
        {
            if (!reachable.Add(current) || !outgoing.TryGetValue(current, out var transitions)) continue;
            foreach (var transition in transitions) pending.Enqueue(transition.ToStateId);
        }
        return reachable;
    }

    private static bool CanReach(
        QuestStateId from,
        QuestStateId target,
        IReadOnlyDictionary<QuestStateId, QuestTransition[]> outgoing)
    {
        var seen = new HashSet<QuestStateId>();
        var pending = new Queue<QuestStateId>();
        pending.Enqueue(from);
        while (pending.TryDequeue(out var current))
        {
            if (current == target) return true;
            if (!seen.Add(current) || !outgoing.TryGetValue(current, out var transitions)) continue;
            foreach (var transition in transitions) pending.Enqueue(transition.ToStateId);
        }
        return false;
    }

    private static string ConditionSignature(IReadOnlyList<QuestCondition> conditions) => string.Join('|',
        conditions.OrderBy(item => item.Kind).ThenBy(item => item.Key, StringComparer.Ordinal)
            .ThenBy(item => item.ExpectedValue, StringComparer.Ordinal)
            .Select(item => $"{item.Kind}:{item.Key}:{item.ExpectedValue}"));
}
