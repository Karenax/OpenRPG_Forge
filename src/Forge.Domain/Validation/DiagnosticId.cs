namespace Forge.Domain.Validation;

public sealed record DiagnosticId
{
    public Guid Value { get; }

    private DiagnosticId(Guid value)
    {
        Value = value;
    }

    public static DiagnosticId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("A diagnostic ID cannot be empty.", nameof(value))
        : new DiagnosticId(value);

    public static DiagnosticId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid diagnostic ID.");
    }

    public override string ToString() => Value.ToString("D");
}
