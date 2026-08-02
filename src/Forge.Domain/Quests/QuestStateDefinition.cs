namespace Forge.Domain.Quests;

public sealed record QuestStateDefinition
{
    public QuestStateId Id { get; }

    public string DisplayName { get; }

    public QuestJournalEntry Journal { get; }

    public bool IsTerminal { get; }

    public QuestStateDefinition(
        QuestStateId id,
        string displayName,
        QuestJournalEntry journal,
        bool isTerminal = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(journal);
        Id = id;
        DisplayName = displayName.Trim();
        Journal = journal;
        IsTerminal = isTerminal;
    }
}
