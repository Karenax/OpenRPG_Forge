using System.Globalization;

namespace Forge.Domain.Entities;

public sealed record EntityPropertyOverride
{
    public const int MaximumKeyLength = 96;
    public const int MaximumValueLength = 4096;

    public string Key { get; }

    public EntityPropertyValueKind Kind { get; }

    public string Value { get; }

    public EntityPropertyOverride(string key, EntityPropertyValueKind kind, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        if (key.Length > MaximumKeyLength || !IsValidKey(key))
        {
            throw new ArgumentException(
                $"Property key '{key}' must contain only letters, digits, '.', '-' or '_' and be at most {MaximumKeyLength} characters.",
                nameof(key));
        }

        if (value.Length > MaximumValueLength)
        {
            throw new ArgumentException(
                $"Property value must be at most {MaximumValueLength} characters.",
                nameof(value));
        }

        Key = key;
        Kind = kind;
        Value = NormalizeValue(kind, value);
    }

    private static bool IsValidKey(string key) =>
        key.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_');

    private static string NormalizeValue(EntityPropertyValueKind kind, string value) => kind switch
    {
        EntityPropertyValueKind.String => value,
        EntityPropertyValueKind.Boolean when bool.TryParse(value, out var parsedBoolean) =>
            parsedBoolean ? "true" : "false",
        EntityPropertyValueKind.Integer when long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedInteger) => parsedInteger.ToString(CultureInfo.InvariantCulture),
        EntityPropertyValueKind.Number when double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsedNumber) && double.IsFinite(parsedNumber) =>
            parsedNumber.ToString("G17", CultureInfo.InvariantCulture),
        EntityPropertyValueKind.Boolean => throw new ArgumentException(
            $"'{value}' is not a boolean value.",
            nameof(value)),
        EntityPropertyValueKind.Integer => throw new ArgumentException(
            $"'{value}' is not an integer value.",
            nameof(value)),
        EntityPropertyValueKind.Number => throw new ArgumentException(
            $"'{value}' is not a finite number.",
            nameof(value)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown property value kind."),
    };
}
