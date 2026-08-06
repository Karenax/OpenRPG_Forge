namespace Forge.Domain.Assistant;

public sealed record LoreEntryDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public LoreEntryKind Kind { get; }
    public string Key { get; }
    public string Title { get; }
    public string Content { get; }
    public IReadOnlyList<string> SourceIds { get; }
    public IReadOnlyList<Guid> RelatedEntryIds { get; }
    public IReadOnlyList<string> Tags { get; }
    public string Provenance { get; }

    public LoreEntryDefinition(
        Guid id,
        int schemaVersion,
        LoreEntryKind kind,
        string key,
        string title,
        string content,
        IReadOnlyList<string> sourceIds,
        IReadOnlyList<Guid>? relatedEntryIds,
        IReadOnlyList<string>? tags,
        string provenance)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Kind = kind;
        Key = (key ?? string.Empty).Trim().ToLowerInvariant();
        Title = (title ?? string.Empty).Trim();
        Content = (content ?? string.Empty).Trim();
        SourceIds = (sourceIds ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        RelatedEntryIds = (relatedEntryIds ?? [])
            .Distinct()
            .Order()
            .ToArray();
        Tags = (tags ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Provenance = (provenance ?? string.Empty).Trim();
    }
}
