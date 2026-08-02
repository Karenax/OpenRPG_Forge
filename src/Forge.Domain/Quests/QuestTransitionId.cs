namespace Forge.Domain.Quests;

public sealed record QuestTransitionId
{
    public Guid Value { get; }

    private QuestTransitionId(Guid value) => Value = value;

    public static QuestTransitionId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A quest transition ID cannot be empty.", nameof(value));
        }

        return new QuestTransitionId(value);
    }

    public static QuestTransitionId Parse(string value) => From(Guid.Parse(value));

    public override string ToString() => Value.ToString("D");
}
