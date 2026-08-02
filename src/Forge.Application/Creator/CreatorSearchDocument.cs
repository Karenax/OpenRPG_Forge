namespace Forge.Application.Creator;

public sealed record CreatorSearchDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }

    public CreatorContentKind Kind { get; }

    public string Id { get; }

    public string DisplayName { get; }

    public string DocumentPath { get; }

    public string SearchText { get; }

    public IReadOnlyList<string> Tags { get; }

    public string Provenance { get; }

    public CreatorSelection? Selection { get; }

    public CreatorSearchDocument(
        CreatorContentKind kind,
        string id,
        string displayName,
        string documentPath,
        string searchText,
        IReadOnlyList<string>? tags = null,
        string provenance = "project-canonical",
        CreatorSelection? selection = null,
        int schemaVersion = CurrentSchemaVersion)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                $"Search document schema '{schemaVersion}' is not supported.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentNullException.ThrowIfNull(searchText);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);

        SchemaVersion = schemaVersion;
        Kind = kind;
        Id = id.Trim();
        DisplayName = displayName.Trim();
        DocumentPath = documentPath.Trim();
        SearchText = searchText.Trim();
        Tags = (tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Provenance = provenance.Trim();
        Selection = selection;
    }
}

public sealed record CreatorSearchQuery(
    string Text,
    CreatorContentKind? Kind = null,
    string? Tag = null,
    int Limit = 100);

public sealed record CreatorSearchResult(CreatorSearchDocument Document, int Score);
