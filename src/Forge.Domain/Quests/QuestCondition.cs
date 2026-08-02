namespace Forge.Domain.Quests;

public sealed record QuestCondition
{
    public QuestConditionKind Kind { get; }

    public string Key { get; }

    public string ExpectedValue { get; }

    public QuestCondition(QuestConditionKind kind, string key, string expectedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(expectedValue);
        Kind = kind;
        Key = key.Trim();
        ExpectedValue = expectedValue.Trim();
    }
}
