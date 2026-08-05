namespace Forge.Domain.Modules;

public sealed record ModuleReleaseConfigurationId
{
    public Guid Value { get; }

    private ModuleReleaseConfigurationId(Guid value)
    {
        Value = value;
    }

    public static ModuleReleaseConfigurationId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A module release configuration ID cannot be empty.", nameof(value));
        }

        return new ModuleReleaseConfigurationId(value);
    }

    public static ModuleReleaseConfigurationId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid module release configuration ID.");
    }

    public override string ToString() => Value.ToString("D");
}
