namespace Forge.Domain.Quests;

public sealed class QuestStateMachine
{
    private readonly QuestDefinition _quest;

    public QuestStateDefinition CurrentState { get; private set; }

    public bool Completed => CurrentState.IsTerminal;

    public QuestStateMachine(QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        var diagnostics = QuestValidator.Validate(quest);
        if (diagnostics.Count > 0)
        {
            throw new ArgumentException(
                $"Quest is invalid: {string.Join("; ", diagnostics.Select(diagnostic => diagnostic.Message))}",
                nameof(quest));
        }

        _quest = quest;
        CurrentState = quest.States.Single(state => state.Id == quest.InitialStateId);
    }

    public IReadOnlyList<QuestTransition> GetAvailableTransitions(IQuestConditionEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        return _quest.Transitions
            .Where(transition => transition.FromStateId == CurrentState.Id &&
                                 transition.Conditions.All(evaluator.IsSatisfied))
            .ToArray();
    }

    public void Restore(QuestStateId stateId)
    {
        ArgumentNullException.ThrowIfNull(stateId);
        CurrentState = _quest.States.SingleOrDefault(state => state.Id == stateId)
            ?? throw new InvalidOperationException(
                $"Quest state '{stateId}' does not exist in quest '{_quest.Id}'.");
    }

    public QuestTransitionResult Advance(QuestTransitionId transitionId, IQuestConditionEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(transitionId);
        ArgumentNullException.ThrowIfNull(evaluator);
        var transition = _quest.Transitions.SingleOrDefault(candidate => candidate.Id == transitionId);
        if (transition is null || transition.FromStateId != CurrentState.Id)
        {
            throw new InvalidOperationException(
                $"Transition '{transitionId}' is not valid from quest state '{CurrentState.Id}'.");
        }

        if (!transition.Conditions.All(evaluator.IsSatisfied))
        {
            throw new InvalidOperationException($"Transition '{transitionId}' does not satisfy its conditions.");
        }

        CurrentState = _quest.States.Single(state => state.Id == transition.ToStateId);
        return new QuestTransitionResult(CurrentState, transition.Actions, CurrentState.IsTerminal);
    }
}
