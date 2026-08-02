namespace Forge.Domain.Quests;

public sealed record QuestStateId
{
    public Guid Value { get; }

    private QuestStateId(Guid value) => Value = value;

    public static QuestStateId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A quest state ID cannot be empty.", nameof(value));
        }

        return new QuestStateId(value);
    }

    public static QuestStateId Parse(string value) => From(Guid.Parse(value));

    public override string ToString() => Value.ToString("D");
}
