namespace Forge.Domain.Blueprints;

public sealed record BlueprintProvenance
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; }

    public string Source { get; }

    public string Author { get; }

    public string LicenseSpdx { get; }

    public string ContentHash { get; }

    public bool IsAiGenerated { get; }

    public IReadOnlyList<string> Modifications { get; }

    public BlueprintProvenance(
        int schemaVersion,
        string source,
        string author,
        string licenseSpdx,
        string contentHash,
        bool isAiGenerated = false,
        IReadOnlyList<string>? modifications = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(schemaVersion, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(author);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseSpdx);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        SchemaVersion = schemaVersion;
        Source = source.Trim();
        Author = author.Trim();
        LicenseSpdx = licenseSpdx.Trim();
        ContentHash = contentHash.Trim().ToLowerInvariant();
        IsAiGenerated = isAiGenerated;
        Modifications = (modifications ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    public static BlueprintProvenance CreateProjectDefault(BlueprintId id, string slug) =>
        new(
            CurrentSchemaVersion,
            $"project://blueprints/{id}/{slug}",
            "Project author",
            "UNLICENSED",
            "unverified");
}
