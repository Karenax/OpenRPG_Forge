namespace Forge.Domain.Content;

public sealed record DataRowDefinition
{
    public DataRowId Id { get; }

    public IReadOnlyDictionary<string, string> Values { get; }

    public string Provenance { get; }

    public DataRowDefinition(
        DataRowId id,
        IReadOnlyDictionary<string, string> values,
        string provenance = "project-author")
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            ArgumentNullException.ThrowIfNull(pair.Value);
            if (!normalized.TryAdd(pair.Key.Trim(), pair.Value.Trim()))
            {
                throw new ArgumentException($"Duplicate data cell '{pair.Key}'.", nameof(values));
            }
        }

        Id = id;
        Values = normalized;
        Provenance = provenance.Trim();
    }
}
