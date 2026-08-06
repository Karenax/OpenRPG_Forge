namespace Forge.Domain.Assistant;

public sealed record LoreIndexDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public bool Enabled { get; }
    public int MaximumContextCharacters { get; }
    public IReadOnlyList<LoreEntryDefinition> Entries { get; }
    public string Provenance { get; }

    public LoreIndexDefinition(
        Guid id,
        int schemaVersion,
        bool enabled,
        int maximumContextCharacters,
        IReadOnlyList<LoreEntryDefinition>? entries,
        string provenance)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Enabled = enabled;
        MaximumContextCharacters = maximumContextCharacters;
        Entries = (entries ?? [])
            .OrderBy(entry => entry.Kind)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .ThenBy(entry => entry.Id)
            .ToArray();
        Provenance = (provenance ?? string.Empty).Trim();
    }

    public static LoreIndexDefinition CreateDefault() =>
        new(
            Guid.Parse("b1230000-0000-4000-8000-000000000001"),
            CurrentSchemaVersion,
            enabled: true,
            maximumContextCharacters: 12_000,
            entries: [],
            provenance: "migration-default:g12-b");
}
