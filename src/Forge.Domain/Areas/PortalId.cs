namespace Forge.Domain.Areas;

public sealed record PortalId
{
    public Guid Value { get; }

    private PortalId(Guid value)
    {
        Value = value;
    }

    public static PortalId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A portal ID cannot be empty.", nameof(value));
        }

        return new PortalId(value);
    }

    public static PortalId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid portal ID.");
    }

    public override string ToString() => Value.ToString("D");
}
