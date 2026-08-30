namespace Forge.Domain.Assistant;

public sealed record AssistedAreaPlanDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public Guid AreaId { get; }
    public string Title { get; }
    public AssistedAreaArchetype Archetype { get; }
    public int Seed { get; }
    public float WidthMeters { get; }
    public float DepthMeters { get; }
    public IReadOnlyList<Guid> KitBlueprintIds { get; }
    public IReadOnlyList<string> Constraints { get; }
    public IReadOnlyList<string> Objectives { get; }
    public IReadOnlyList<AssistedAreaPlanNode> Nodes { get; }
    public IReadOnlyList<AssistedAreaPlanLink> Links { get; }
    public IReadOnlyList<string> LoreSourceIds { get; }
    public bool ApprovedForConstruction { get; }
    public decimal EstimatedCostUsd { get; }
    public string ProviderKey { get; }
    public string Provenance { get; }
    public AssistedAreaEnvironmentIntent EnvironmentIntent { get; }

    public AssistedAreaPlanDefinition(
        Guid id,
        int schemaVersion,
        Guid areaId,
        string title,
        AssistedAreaArchetype archetype,
        int seed,
        float widthMeters,
        float depthMeters,
        IReadOnlyList<Guid>? kitBlueprintIds,
        IReadOnlyList<string>? constraints,
        IReadOnlyList<string>? objectives,
        IReadOnlyList<AssistedAreaPlanNode>? nodes,
        IReadOnlyList<AssistedAreaPlanLink>? links,
        IReadOnlyList<string>? loreSourceIds,
        bool approvedForConstruction,
        decimal estimatedCostUsd,
        string providerKey,
        string provenance,
        AssistedAreaEnvironmentIntent? environmentIntent = null)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        AreaId = areaId;
        Title = (title ?? string.Empty).Trim();
        Archetype = archetype;
        Seed = seed;
        WidthMeters = widthMeters;
        DepthMeters = depthMeters;
        KitBlueprintIds = (kitBlueprintIds ?? []).Distinct().Order().ToArray();
        Constraints = Normalize(constraints);
        Objectives = Normalize(objectives);
        Nodes = (nodes ?? []).OrderBy(item => item.Id).ToArray();
        Links = (links ?? []).OrderBy(item => item.Id).ToArray();
        LoreSourceIds = Normalize(loreSourceIds, lower: true);
        ApprovedForConstruction = approvedForConstruction;
        EstimatedCostUsd = estimatedCostUsd;
        ProviderKey = (providerKey ?? string.Empty).Trim().ToLowerInvariant();
        Provenance = (provenance ?? string.Empty).Trim();
        EnvironmentIntent = environmentIntent ?? AssistedAreaEnvironmentIntent.Default;
    }

    public AssistedAreaPlanDefinition WithApproval(bool approved, string provenance) =>
        new(Id, SchemaVersion, AreaId, Title, Archetype, Seed, WidthMeters, DepthMeters, KitBlueprintIds,
            Constraints, Objectives, Nodes, Links, LoreSourceIds, approved, EstimatedCostUsd, ProviderKey,
            provenance, EnvironmentIntent);

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? values, bool lower = false) =>
        (values ?? []).Select(item => (item ?? string.Empty).Trim())
            .Where(item => item.Length > 0)
            .Select(item => lower ? item.ToLowerInvariant() : item)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
}
