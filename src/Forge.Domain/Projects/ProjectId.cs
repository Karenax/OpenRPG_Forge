namespace Forge.Domain.Projects;

public sealed record ProjectId
{
    public Guid Value { get; }

    private ProjectId(Guid value)
    {
        Value = value;
    }

    public static ProjectId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A project ID cannot be empty.", nameof(value));
        }

        return new ProjectId(value);
    }

    public static ProjectId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid project ID.");
    }

    public override string ToString() => Value.ToString("D");
}
