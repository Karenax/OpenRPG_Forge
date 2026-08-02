namespace Forge.Domain.Modules;

public sealed record ProjectVariableId
{
    public Guid Value { get; }

    private ProjectVariableId(Guid value)
    {
        Value = value;
    }

    public static ProjectVariableId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A project variable ID cannot be empty.", nameof(value));
        }

        return new ProjectVariableId(value);
    }

    public static ProjectVariableId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid project variable ID.");
    }

    public override string ToString() => Value.ToString("D");
}
