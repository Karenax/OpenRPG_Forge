namespace Forge.Domain.Navigation;

public sealed record NavigationSurfaceId
{
    public Guid Value { get; }

    private NavigationSurfaceId(Guid value)
    {
        Value = value;
    }

    public static NavigationSurfaceId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A navigation surface ID cannot be empty.", nameof(value));
        }

        return new NavigationSurfaceId(value);
    }

    public static NavigationSurfaceId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid navigation surface ID.");
    }

    public override string ToString() => Value.ToString("D");
}
