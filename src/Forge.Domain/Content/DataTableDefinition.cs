using System.Globalization;

namespace Forge.Domain.Content;

public sealed record DataTableDefinition
{
    public const int CurrentSchemaVersion = 1;

    public DataTableId Id { get; }

    public int SchemaVersion { get; }

    public string Slug { get; }

    public string DisplayName { get; }

    public IReadOnlyList<DataColumnDefinition> Columns { get; }

    public IReadOnlyList<DataRowDefinition> Rows { get; }

    public DataTableId? OverridesTableId { get; }

    public string Provenance { get; }

    public string LicenseSpdx { get; }

    public string ContentHash { get; }

    public DataTableDefinition(
        DataTableId id,
        string slug,
        string displayName,
        IReadOnlyList<DataColumnDefinition> columns,
        IReadOnlyList<DataRowDefinition> rows,
        DataTableId? overridesTableId = null,
        string provenance = "project-author",
        string licenseSpdx = "UNLICENSED",
        string contentHash = "unverified",
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseSpdx);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), $"Data table schema '{schemaVersion}' is not supported.");
        }

        var normalizedSlug = slug.Trim();
        if (normalizedSlug.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        {
            throw new ArgumentException("A data table slug may only contain ASCII letters, digits, '-' and '_'.", nameof(slug));
        }

        var materializedColumns = columns.OrderBy(column => column.Key, StringComparer.Ordinal).ToArray();
        var materializedRows = rows.OrderBy(row => row.Id.Value).ToArray();
        if (materializedColumns.Length == 0)
        {
            throw new ArgumentException("A data table must declare at least one column.", nameof(columns));
        }

        if (materializedColumns.Select(column => column.Id).Distinct().Count() != materializedColumns.Length ||
            materializedColumns.Select(column => column.Key).Distinct(StringComparer.Ordinal).Count() != materializedColumns.Length)
        {
            throw new ArgumentException("Data table column IDs and keys must be unique.", nameof(columns));
        }

        if (materializedRows.Select(row => row.Id).Distinct().Count() != materializedRows.Length)
        {
            throw new ArgumentException("Data table row IDs must be unique.", nameof(rows));
        }

        foreach (var row in materializedRows)
        {
            ValidateRow(materializedColumns, row);
        }

        var normalizedHash = contentHash.Trim().ToLowerInvariant();
        if (!string.Equals(normalizedHash, "unverified", StringComparison.Ordinal) &&
            (normalizedHash.Length != 64 || normalizedHash.Any(character => !Uri.IsHexDigit(character))))
        {
            throw new ArgumentException("A data table content hash must be 'unverified' or a SHA-256 hex digest.", nameof(contentHash));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Slug = normalizedSlug;
        DisplayName = displayName.Trim();
        Columns = materializedColumns;
        Rows = materializedRows;
        OverridesTableId = overridesTableId;
        Provenance = provenance.Trim();
        LicenseSpdx = licenseSpdx.Trim();
        ContentHash = normalizedHash;
    }

    private static void ValidateRow(IReadOnlyList<DataColumnDefinition> columns, DataRowDefinition row)
    {
        var knownKeys = columns.Select(column => column.Key).ToHashSet(StringComparer.Ordinal);
        var unknownKey = row.Values.Keys.FirstOrDefault(key => !knownKeys.Contains(key));
        if (unknownKey is not null)
        {
            throw new ArgumentException($"Row '{row.Id}' contains unknown column '{unknownKey}'.", nameof(row));
        }

        foreach (var column in columns)
        {
            var hasValue = row.Values.TryGetValue(column.Key, out var value) && !string.IsNullOrWhiteSpace(value);
            if (column.Required && !hasValue)
            {
                throw new ArgumentException($"Row '{row.Id}' requires column '{column.Key}'.", nameof(row));
            }

            if (!hasValue)
            {
                continue;
            }

            var valid = column.Kind switch
            {
                DataColumnKind.Text => true,
                DataColumnKind.Integer => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
                DataColumnKind.Decimal => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
                DataColumnKind.Boolean => bool.TryParse(value, out _),
                DataColumnKind.Reference => Guid.TryParse(value, out var parsed) && parsed != Guid.Empty,
                _ => false,
            };
            if (!valid)
            {
                throw new ArgumentException($"Row '{row.Id}' value for '{column.Key}' is not a valid {column.Kind}.", nameof(row));
            }
        }
    }
}
