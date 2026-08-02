namespace Forge.Domain.Navigation;

public sealed record OffMeshNavigationLinkId
{
    public Guid Value { get; }

    private OffMeshNavigationLinkId(Guid value)
    {
        Value = value;
    }

    public static OffMeshNavigationLinkId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An off-mesh navigation link ID cannot be empty.", nameof(value));
        }

        return new OffMeshNavigationLinkId(value);
    }

    public static OffMeshNavigationLinkId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid off-mesh navigation link ID.");
    }

    public override string ToString() => Value.ToString("D");
}
