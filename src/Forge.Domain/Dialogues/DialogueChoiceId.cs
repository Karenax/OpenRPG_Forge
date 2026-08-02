namespace Forge.Domain.Dialogues;

public sealed record DialogueChoiceId
{
    public Guid Value { get; }

    private DialogueChoiceId(Guid value) => Value = value;

    public static DialogueChoiceId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A dialogue choice ID cannot be empty.", nameof(value));
        }

        return new DialogueChoiceId(value);
    }

    public static DialogueChoiceId Parse(string value) => From(Guid.Parse(value));

    public override string ToString() => Value.ToString("D");
}
