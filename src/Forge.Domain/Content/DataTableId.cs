namespace Forge.Domain.Content;

public sealed record DataTableId
{
    public Guid Value { get; }

    private DataTableId(Guid value) => Value = value;

    public static DataTableId From(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException("A data table ID cannot be empty.", nameof(value))
            : new DataTableId(value);

    public static DataTableId Parse(string value) =>
        Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid data table ID.");

    public override string ToString() => Value.ToString("D");
}
