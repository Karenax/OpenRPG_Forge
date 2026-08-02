namespace Forge.Domain.Entities;

public sealed record EntityId
{
    public Guid Value { get; }

    private EntityId(Guid value)
    {
        Value = value;
    }

    public static EntityId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An entity ID cannot be empty.", nameof(value));
        }

        return new EntityId(value);
    }

    public static EntityId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!Guid.TryParse(value, out var parsed))
        {
            throw new FormatException($"'{value}' is not a valid entity ID.");
        }

        return From(parsed);
    }

    public override string ToString() => Value.ToString("D");
}
