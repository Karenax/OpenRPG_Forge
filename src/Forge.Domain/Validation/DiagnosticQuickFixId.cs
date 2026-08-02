namespace Forge.Domain.Validation;

public sealed record DiagnosticQuickFixId
{
    public Guid Value { get; }

    private DiagnosticQuickFixId(Guid value)
    {
        Value = value;
    }

    public static DiagnosticQuickFixId From(Guid value) => value == Guid.Empty
        ? throw new ArgumentException("A diagnostic quick-fix ID cannot be empty.", nameof(value))
        : new DiagnosticQuickFixId(value);

    public static DiagnosticQuickFixId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Guid.TryParse(value, out var parsed)
            ? From(parsed)
            : throw new FormatException($"'{value}' is not a valid diagnostic quick-fix ID.");
    }

    public override string ToString() => Value.ToString("D");
}
