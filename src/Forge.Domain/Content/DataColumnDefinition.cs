namespace Forge.Domain.Content;

public sealed record DataColumnDefinition
{
    public DataColumnId Id { get; }

    public string Key { get; }

    public string DisplayName { get; }

    public DataColumnKind Kind { get; }

    public bool Required { get; }

    public DataTableId? ReferenceTableId { get; }

    public DataColumnDefinition(
        DataColumnId id,
        string key,
        string displayName,
        DataColumnKind kind,
        bool required = false,
        DataTableId? referenceTableId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var normalizedKey = key.Trim();
        if (normalizedKey.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '_' or '-')))
        {
            throw new ArgumentException("A data column key may only contain ASCII letters, digits, '_' and '-'.", nameof(key));
        }

        if (kind == DataColumnKind.Reference && referenceTableId is null)
        {
            throw new ArgumentException("A reference column must identify its target table.", nameof(referenceTableId));
        }

        if (kind != DataColumnKind.Reference && referenceTableId is not null)
        {
            throw new ArgumentException("Only reference columns may identify a target table.", nameof(referenceTableId));
        }

        Id = id;
        Key = normalizedKey;
        DisplayName = displayName.Trim();
        Kind = kind;
        Required = required;
        ReferenceTableId = referenceTableId;
    }
}
