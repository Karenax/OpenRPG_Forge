namespace Forge.Domain.Dialogues;

public sealed record DialogueNodeId
{
    public Guid Value { get; }

    private DialogueNodeId(Guid value) => Value = value;

    public static DialogueNodeId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A dialogue node ID cannot be empty.", nameof(value));
        }

        return new DialogueNodeId(value);
    }

    public static DialogueNodeId Parse(string value) => From(Guid.Parse(value));

    public override string ToString() => Value.ToString("D");
}
