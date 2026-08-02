namespace Forge.Domain.Blueprints;

public sealed record BlueprintId
{
    public Guid Value { get; }

    private BlueprintId(Guid value)
    {
        Value = value;
    }

    public static BlueprintId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A blueprint ID cannot be empty.", nameof(value));
        }

        return new BlueprintId(value);
    }

    public static BlueprintId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid blueprint ID.");
    }

    public override string ToString() => Value.ToString("D");
}
