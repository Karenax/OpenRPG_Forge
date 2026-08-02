namespace Forge.Domain.Modules;

public sealed record ModuleTargetProfileId
{
    public Guid Value { get; }

    private ModuleTargetProfileId(Guid value)
    {
        Value = value;
    }

    public static ModuleTargetProfileId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A module target profile ID cannot be empty.", nameof(value));
        }

        return new ModuleTargetProfileId(value);
    }

    public static ModuleTargetProfileId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid module target profile ID.");
    }

    public override string ToString() => Value.ToString("D");
}
