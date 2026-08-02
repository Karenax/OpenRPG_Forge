namespace Forge.Domain.Quests;

public sealed record QuestDefinition
{
    public QuestId Id { get; }

    public string Slug { get; }

    public string DisplayName { get; }

    public QuestStateId InitialStateId { get; }

    public IReadOnlyList<QuestStateDefinition> States { get; }

    public IReadOnlyList<QuestTransition> Transitions { get; }

    public QuestDefinition(
        QuestId id,
        string slug,
        string displayName,
        QuestStateId initialStateId,
        IReadOnlyList<QuestStateDefinition> states,
        IReadOnlyList<QuestTransition>? transitions = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(initialStateId);
        ArgumentNullException.ThrowIfNull(states);
        var materializedStates = states.ToArray();
        var materializedTransitions = (transitions ?? []).ToArray();
        if (materializedStates.Select(state => state.Id).Distinct().Count() != materializedStates.Length)
        {
            throw new ArgumentException("A quest cannot contain duplicate state IDs.", nameof(states));
        }

        if (materializedTransitions.Select(transition => transition.Id).Distinct().Count() !=
            materializedTransitions.Length)
        {
            throw new ArgumentException("A quest cannot contain duplicate transition IDs.", nameof(transitions));
        }

        Id = id;
        Slug = slug.Trim();
        DisplayName = displayName.Trim();
        InitialStateId = initialStateId;
        States = materializedStates;
        Transitions = materializedTransitions;
    }
}
