namespace Forge.Domain.Modules;

public sealed record ModuleDistributionConfigurationId
{
    public Guid Value { get; }

    private ModuleDistributionConfigurationId(Guid value)
    {
        Value = value;
    }

    public static ModuleDistributionConfigurationId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A module distribution configuration ID cannot be empty.", nameof(value));
        }

        return new ModuleDistributionConfigurationId(value);
    }

    public static ModuleDistributionConfigurationId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid module distribution configuration ID.");
    }

    public override string ToString() => Value.ToString("D");
}
