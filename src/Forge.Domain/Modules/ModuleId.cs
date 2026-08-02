namespace Forge.Domain.Modules;

public sealed record ModuleId
{
    public Guid Value { get; }

    private ModuleId(Guid value)
    {
        Value = value;
    }

    public static ModuleId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A module ID cannot be empty.", nameof(value));
        }

        return new ModuleId(value);
    }

    public static ModuleId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid module ID.");
    }

    public override string ToString() => Value.ToString("D");
}
