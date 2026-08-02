namespace Forge.Domain.Dialogues;

public sealed record DialogueAction
{
    public DialogueActionKind Kind { get; }

    public string Key { get; }

    public string Value { get; }

    public DialogueAction(DialogueActionKind kind, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        Kind = kind;
        Key = key.Trim();
        Value = value.Trim();
    }
}
