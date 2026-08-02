namespace Forge.Domain.Quests;

public sealed record QuestAction
{
    public QuestActionKind Kind { get; }

    public string Key { get; }

    public string Value { get; }

    public QuestAction(QuestActionKind kind, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        Kind = kind;
        Key = key.Trim();
        Value = value.Trim();
    }
}
