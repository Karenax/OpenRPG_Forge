using Forge.Domain.Validation;

namespace Forge.Domain.Quests;

public static class QuestValidator
{
    public const string MissingInitialStateCode = "quest.reference.initial_state_missing";
    public const string MissingTransitionStateCode = "quest.reference.transition_state_missing";
    public const string UnreachableStateCode = "quest.graph.state_unreachable";
    public const string TerminalStateTransitionCode = "quest.graph.terminal_state_has_transition";

    public static IReadOnlyList<DomainDiagnostic> Validate(QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var diagnostics = new List<DomainDiagnostic>();
        var states = quest.States.ToDictionary(state => state.Id);
        if (!states.ContainsKey(quest.InitialStateId))
        {
            diagnostics.Add(new DomainDiagnostic(
                MissingInitialStateCode,
                $"Quest '{quest.Id}' references missing initial state '{quest.InitialStateId}'.",
                $"quests/{quest.Id}/initialStateId"));
            return diagnostics;
        }

        foreach (var transition in quest.Transitions)
        {
            if (!states.ContainsKey(transition.FromStateId) || !states.ContainsKey(transition.ToStateId))
            {
                diagnostics.Add(new DomainDiagnostic(
                    MissingTransitionStateCode,
                    $"Quest transition '{transition.Id}' references a missing state.",
                    $"quests/{quest.Id}/transitions/{transition.Id}"));
            }

            if (states.TryGetValue(transition.FromStateId, out var fromState) && fromState.IsTerminal)
            {
                diagnostics.Add(new DomainDiagnostic(
                    TerminalStateTransitionCode,
                    $"Terminal quest state '{fromState.Id}' cannot have outgoing transitions.",
                    $"quests/{quest.Id}/transitions/{transition.Id}/fromStateId"));
            }
        }

        var reachable = new HashSet<QuestStateId>();
        var pending = new Queue<QuestStateId>();
        pending.Enqueue(quest.InitialStateId);
        while (pending.TryDequeue(out var stateId) && reachable.Add(stateId))
        {
            foreach (var target in quest.Transitions
                         .Where(transition => transition.FromStateId == stateId && states.ContainsKey(transition.ToStateId))
                         .Select(transition => transition.ToStateId))
            {
                pending.Enqueue(target);
            }
        }

        foreach (var state in quest.States.Where(state => !reachable.Contains(state.Id)))
        {
            diagnostics.Add(new DomainDiagnostic(
                UnreachableStateCode,
                $"Quest state '{state.Id}' cannot be reached from the initial state.",
                $"quests/{quest.Id}/states/{state.Id}"));
        }

        diagnostics.AddRange(QuestOperationsValidator.Validate(quest));

        return diagnostics;
    }
}
