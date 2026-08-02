namespace Forge.Domain.Dialogues;

public sealed record DialogueCondition
{
    public DialogueConditionKind Kind { get; }

    public string Key { get; }

    public string ExpectedValue { get; }

    public DialogueCondition(DialogueConditionKind kind, string key, string expectedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(expectedValue);
        Kind = kind;
        Key = key.Trim();
        ExpectedValue = expectedValue.Trim();
    }
}
