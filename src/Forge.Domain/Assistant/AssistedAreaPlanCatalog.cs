namespace Forge.Domain.Assistant;

public sealed record AssistedAreaPlanCatalog
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public IReadOnlyList<AssistedAreaPlanDefinition> Plans { get; }
    public string Provenance { get; }

    public AssistedAreaPlanCatalog(
        Guid id,
        int schemaVersion,
        IReadOnlyList<AssistedAreaPlanDefinition>? plans,
        string provenance)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Plans = (plans ?? []).OrderBy(item => item.Title, StringComparer.Ordinal).ThenBy(item => item.Id).ToArray();
        Provenance = (provenance ?? string.Empty).Trim();
    }

    public static AssistedAreaPlanCatalog CreateDefault() => new(
        Guid.Parse("b1240000-0000-4000-8000-000000000001"),
        CurrentSchemaVersion,
        [],
        "migration-default:g12-c");
}
