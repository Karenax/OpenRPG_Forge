namespace Forge.Domain.Modules;

public sealed record ThirdPartyDependencyId
{
    public Guid Value { get; }

    private ThirdPartyDependencyId(Guid value)
    {
        Value = value;
    }

    public static ThirdPartyDependencyId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A third-party dependency ID cannot be empty.", nameof(value));
        }

        return new ThirdPartyDependencyId(value);
    }

    public static ThirdPartyDependencyId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid third-party dependency ID.");
    }

    public override string ToString() => Value.ToString("D");
}
