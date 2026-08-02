namespace Forge.Domain.Quests;

public sealed record QuestId
{
    public Guid Value { get; }

    private QuestId(Guid value) => Value = value;

    public static QuestId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A quest ID cannot be empty.", nameof(value));
        }

        return new QuestId(value);
    }

    public static QuestId Parse(string value) => From(Guid.Parse(value));

    public override string ToString() => Value.ToString("D");
}
