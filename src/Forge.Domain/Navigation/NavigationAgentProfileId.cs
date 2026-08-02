namespace Forge.Domain.Navigation;

public sealed record NavigationAgentProfileId
{
    public Guid Value { get; }

    private NavigationAgentProfileId(Guid value)
    {
        Value = value;
    }

    public static NavigationAgentProfileId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A navigation agent profile ID cannot be empty.", nameof(value));
        }

        return new NavigationAgentProfileId(value);
    }

    public static NavigationAgentProfileId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid navigation agent profile ID.");
    }

    public override string ToString() => Value.ToString("D");
}
