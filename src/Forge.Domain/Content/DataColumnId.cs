namespace Forge.Domain.Content;

public sealed record DataColumnId
{
    public Guid Value { get; }

    private DataColumnId(Guid value) => Value = value;

    public static DataColumnId From(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException("A data column ID cannot be empty.", nameof(value))
            : new DataColumnId(value);

    public static DataColumnId Parse(string value) =>
        Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid data column ID.");

    public override string ToString() => Value.ToString("D");
}
