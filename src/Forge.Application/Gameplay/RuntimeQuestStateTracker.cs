using Forge.Domain.Quests;

namespace Forge.Application.Gameplay;

public sealed class RuntimeQuestStateTracker
{
    private readonly IReadOnlyList<QuestDefinition> _quests;
    private readonly IReadOnlyDictionary<QuestId, QuestStateMachine> _machines;
    private readonly HashSet<QuestId> _acceptedQuestIds = [];

    public QuestId? TrackedQuestId { get; private set; }

    public RuntimeQuestStateTracker(IReadOnlyList<QuestDefinition> quests)
    {
        ArgumentNullException.ThrowIfNull(quests);
        _quests = quests.ToArray();
        if (_quests.Select(quest => quest.Id).Distinct().Count() != _quests.Count)
            throw new ArgumentException("Runtime quest definitions must have unique IDs.", nameof(quests));
        _machines = _quests.ToDictionary(quest => quest.Id, quest => new QuestStateMachine(quest));
    }

    public IReadOnlyList<RuntimeQuestStateSnapshot> Snapshot() => _quests
        .Select(quest => new RuntimeQuestStateSnapshot(
            quest.Id,
            _machines[quest.Id].CurrentState.Id,
            _acceptedQuestIds.Contains(quest.Id)))
        .ToArray();

    public QuestDefinition? ResolveQuest(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        var normalized = reference.Trim();
        var matches = _quests.Where(quest =>
                string.Equals(quest.Id.ToString(), normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(quest.Slug, normalized, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"Quest reference '{normalized}' resolves to multiple definitions."),
        };
    }

    public QuestStateDefinition? GetCurrentState(QuestId questId)
    {
        ArgumentNullException.ThrowIfNull(questId);
        return _machines.TryGetValue(questId, out var machine) ? machine.CurrentState : null;
    }

    public bool IsAccepted(QuestId questId)
    {
        ArgumentNullException.ThrowIfNull(questId);
        return _acceptedQuestIds.Contains(questId);
    }

    public bool Accept(string questReference, bool track) =>
        Accept(questReference, track, out _);

    public bool Accept(string questReference, bool track, out bool newlyAccepted)
    {
        var quest = ResolveQuest(questReference);
        if (quest is null)
        {
            newlyAccepted = false;
            return false;
        }
        newlyAccepted = _acceptedQuestIds.Add(quest.Id);
        if (track && !_machines[quest.Id].CurrentState.IsTerminal)
            TrackedQuestId = quest.Id;
        return true;
    }

    public bool Track(QuestId? questId)
    {
        if (questId is null)
        {
            TrackedQuestId = null;
            return true;
        }

        if (!_machines.TryGetValue(questId, out var machine) ||
            !_acceptedQuestIds.Contains(questId) || machine.CurrentState.IsTerminal)
            return false;
        TrackedQuestId = questId;
        return true;
    }

    public IReadOnlyList<QuestTransition> GetAvailableTransitions(
        QuestId questId,
        IQuestConditionEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(questId);
        ArgumentNullException.ThrowIfNull(evaluator);
        return _machines.TryGetValue(questId, out var machine) && _acceptedQuestIds.Contains(questId)
            ? machine.GetAvailableTransitions(evaluator)
            : [];
    }

    public RuntimeQuestAdvance? AdvanceByEvent(
        string questReference,
        QuestEventKind kind,
        string eventName,
        string? sourceReference,
        IQuestConditionEvaluator evaluator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(evaluator);
        var quest = ResolveQuest(questReference);
        if (quest is null || !_acceptedQuestIds.Contains(quest.Id))
            return null;
        var matches = _machines[quest.Id].GetAvailableTransitions(evaluator)
            .Where(transition => transition.EventBinding.Kind == kind &&
                string.Equals(transition.EventBinding.EventName, eventName, StringComparison.Ordinal) &&
                (transition.EventBinding.SourceReference is null ||
                 string.Equals(transition.EventBinding.SourceReference, sourceReference, StringComparison.Ordinal)))
            .ToArray();
        if (matches.Length == 0)
            return null;
        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                $"Quest event '{kind}:{eventName}' resolves to multiple transitions in quest '{quest.Id}'.");
        }

        return Advance(quest, matches[0], evaluator);
    }

    public RuntimeQuestAdvance Advance(
        QuestDefinition quest,
        QuestTransition transition,
        IQuestConditionEvaluator evaluator)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(evaluator);
        if (!_machines.TryGetValue(quest.Id, out var machine) || !_acceptedQuestIds.Contains(quest.Id))
            throw new InvalidOperationException($"Quest '{quest.Id}' is not accepted in this runtime session.");
        var result = machine.Advance(transition.Id, evaluator);
        if (result.Completed && TrackedQuestId == quest.Id)
            TrackedQuestId = null;
        return new RuntimeQuestAdvance(quest, transition, result);
    }

    public void Restore(
        IReadOnlyList<RuntimeQuestStateSnapshot> snapshots,
        QuestId? trackedQuestId)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        if (snapshots.Select(snapshot => snapshot.QuestId).Distinct().Count() != snapshots.Count)
            throw new InvalidOperationException("A quest can only have one restored runtime state.");

        _acceptedQuestIds.Clear();
        TrackedQuestId = null;
        foreach (var quest in _quests)
            _machines[quest.Id].Restore(quest.InitialStateId);
        foreach (var snapshot in snapshots)
        {
            if (!_machines.TryGetValue(snapshot.QuestId, out var machine))
                throw new InvalidOperationException($"Restored quest '{snapshot.QuestId}' is not defined by the project.");
            machine.Restore(snapshot.StateId);
            if (snapshot.IsAccepted)
                _acceptedQuestIds.Add(snapshot.QuestId);
        }

        if (trackedQuestId is not null &&
            _machines.TryGetValue(trackedQuestId, out var trackedMachine) &&
            _acceptedQuestIds.Contains(trackedQuestId) && !trackedMachine.CurrentState.IsTerminal)
            TrackedQuestId = trackedQuestId;
    }
}
