namespace Forge.Domain.Quests;

public sealed record QuestJournalEntry
{
    public string LocalizationKey { get; }

    public string Fallback { get; }

    public QuestJournalEntry(string localizationKey, string fallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localizationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallback);
        LocalizationKey = localizationKey.Trim();
        Fallback = fallback.Trim();
    }
}
