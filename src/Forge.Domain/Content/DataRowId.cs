namespace Forge.Domain.Content;

public sealed record DataRowId
{
    public Guid Value { get; }

    private DataRowId(Guid value) => Value = value;

    public static DataRowId From(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException("A data row ID cannot be empty.", nameof(value))
            : new DataRowId(value);

    public static DataRowId Parse(string value) =>
        Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid data row ID.");

    public override string ToString() => Value.ToString("D");
}
