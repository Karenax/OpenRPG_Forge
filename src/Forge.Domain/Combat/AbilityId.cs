namespace Forge.Domain.Combat;

public sealed record AbilityId
{
    public Guid Value { get; }

    private AbilityId(Guid value) => Value = value;

    public static AbilityId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An ability ID cannot be empty.", nameof(value));
        }

        return new AbilityId(value);
    }

    public static AbilityId Parse(string value) => From(Guid.Parse(value));

    public override string ToString() => Value.ToString("D");
}
