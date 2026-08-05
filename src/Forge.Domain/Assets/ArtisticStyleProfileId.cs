namespace Forge.Domain.Assets;

public sealed record ArtisticStyleProfileId
{
    public Guid Value { get; }

    private ArtisticStyleProfileId(Guid value) => Value = value;

    public static ArtisticStyleProfileId From(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException("An artistic style profile ID cannot be empty.", nameof(value))
            : new ArtisticStyleProfileId(value);

    public static ArtisticStyleProfileId Parse(string value) =>
        Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid artistic style profile ID.");

    public override string ToString() => Value.ToString("D");
}
