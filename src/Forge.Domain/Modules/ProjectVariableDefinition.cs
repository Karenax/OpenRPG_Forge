using System.Globalization;

namespace Forge.Domain.Modules;

public sealed record ProjectVariableDefinition
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumKeyLength = 96;
    public const int MaximumValueLength = 4096;

    public ProjectVariableId Id { get; }

    public int SchemaVersion { get; }

    public string Key { get; }

    public ProjectVariableValueKind Kind { get; }

    public ProjectVariableScope Scope { get; }

    public string DefaultValue { get; }

    public string Description { get; }

    public string Provenance { get; }

    public ProjectVariableDefinition(
        ProjectVariableId id,
        string key,
        ProjectVariableValueKind kind,
        ProjectVariableScope scope,
        string defaultValue,
        string description,
        string provenance,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(defaultValue);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                $"Project variable schema '{schemaVersion}' is not supported.");
        }

        var normalizedKey = key.Trim();
        if (normalizedKey.Length > MaximumKeyLength ||
            !normalizedKey.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_'))
        {
            throw new ArgumentException(
                $"Variable key '{key}' contains unsupported characters or exceeds {MaximumKeyLength} characters.",
                nameof(key));
        }

        if (defaultValue.Length > MaximumValueLength)
        {
            throw new ArgumentException(
                $"Variable value must be at most {MaximumValueLength} characters.",
                nameof(defaultValue));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Key = normalizedKey;
        Kind = kind;
        Scope = scope;
        DefaultValue = NormalizeValue(kind, defaultValue);
        Description = description.Trim();
        Provenance = provenance.Trim();
    }

    public string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return NormalizeValue(Kind, value);
    }

    private static string NormalizeValue(ProjectVariableValueKind kind, string value) => kind switch
    {
        ProjectVariableValueKind.String => value,
        ProjectVariableValueKind.Boolean when bool.TryParse(value, out var parsedBoolean) =>
            parsedBoolean ? "true" : "false",
        ProjectVariableValueKind.Integer when long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsedInteger) => parsedInteger.ToString(CultureInfo.InvariantCulture),
        ProjectVariableValueKind.Number when double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsedNumber) && double.IsFinite(parsedNumber) =>
            parsedNumber.ToString("G17", CultureInfo.InvariantCulture),
        ProjectVariableValueKind.Boolean => throw new ArgumentException($"'{value}' is not a boolean value."),
        ProjectVariableValueKind.Integer => throw new ArgumentException($"'{value}' is not an integer value."),
        ProjectVariableValueKind.Number => throw new ArgumentException($"'{value}' is not a finite number."),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown variable value kind."),
    };
}
