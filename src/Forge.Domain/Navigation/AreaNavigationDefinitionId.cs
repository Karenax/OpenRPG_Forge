namespace Forge.Domain.Navigation;

public sealed record AreaNavigationDefinitionId
{
    public Guid Value { get; }

    private AreaNavigationDefinitionId(Guid value)
    {
        Value = value;
    }

    public static AreaNavigationDefinitionId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An area navigation definition ID cannot be empty.", nameof(value));
        }

        return new AreaNavigationDefinitionId(value);
    }

    public static AreaNavigationDefinitionId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid area navigation definition ID.");
    }

    public override string ToString() => Value.ToString("D");
}
