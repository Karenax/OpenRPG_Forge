namespace Forge.Domain.Areas;

public sealed record AreaId
{
    public Guid Value { get; }

    private AreaId(Guid value)
    {
        Value = value;
    }

    public static AreaId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An area ID cannot be empty.", nameof(value));
        }

        return new AreaId(value);
    }

    public static AreaId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid area ID.");
    }

    public override string ToString() => Value.ToString("D");
}
