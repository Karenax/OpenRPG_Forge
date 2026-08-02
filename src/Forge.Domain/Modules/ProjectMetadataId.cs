namespace Forge.Domain.Modules;

public sealed record ProjectMetadataId
{
    public Guid Value { get; }

    private ProjectMetadataId(Guid value)
    {
        Value = value;
    }

    public static ProjectMetadataId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A project metadata ID cannot be empty.", nameof(value));
        }

        return new ProjectMetadataId(value);
    }

    public static ProjectMetadataId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid project metadata ID.");
    }

    public override string ToString() => Value.ToString("D");
}
