namespace Forge.Domain.Dialogues;

public sealed record DialogueId
{
    public Guid Value { get; }

    private DialogueId(Guid value) => Value = value;

    public static DialogueId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A dialogue ID cannot be empty.", nameof(value));
        }

        return new DialogueId(value);
    }

    public static DialogueId Parse(string value) => From(Guid.Parse(value));

    public override string ToString() => Value.ToString("D");
}
