namespace Forge.Domain.Localization;

public sealed record LocalizationCatalogId
{
    public Guid Value { get; }

    private LocalizationCatalogId(Guid value) => Value = value;

    public static LocalizationCatalogId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("A localization catalog ID cannot be empty.", nameof(value))
        : new LocalizationCatalogId(value);

    public static LocalizationCatalogId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid localization catalog ID.");
    }

    public override string ToString() => Value.ToString("D");
}
