using Forge.Domain.Entities;

namespace Forge.Domain.Areas;

public sealed record AreaDefinition
{
    public const int CurrentSchemaVersion = 1;

    public AreaId Id { get; }

    public string Slug { get; }

    public string DisplayName { get; }

    public int SchemaVersion { get; }

    public AreaKind Kind { get; }

    public AreaDimensions Dimensions { get; }

    public IReadOnlyList<EntityInstance> Entities { get; }

    public IReadOnlyList<PortalLink> Portals { get; }

    public AreaDefinition(
        AreaId id,
        string slug,
        string displayName,
        IReadOnlyList<EntityInstance> entities,
        AreaKind kind = AreaKind.Interior,
        AreaDimensions? dimensions = null,
        IReadOnlyList<PortalLink>? portals = null,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(entities);
        if (schemaVersion is not 0 and not CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                $"Area schema version '{schemaVersion}' is not supported.");
        }

        var materializedEntities = entities.ToArray();
        var materializedPortals = (portals ?? []).ToArray();
        if (materializedEntities.Select(entity => entity.Id).Distinct().Count() != materializedEntities.Length)
        {
            throw new ArgumentException("An area cannot contain duplicate entity IDs.", nameof(entities));
        }

        if (materializedPortals.Select(portal => portal.Id).Distinct().Count() != materializedPortals.Length)
        {
            throw new ArgumentException("An area cannot contain duplicate portal IDs.", nameof(portals));
        }

        Id = id;
        Slug = slug.Trim();
        DisplayName = displayName.Trim();
        SchemaVersion = CurrentSchemaVersion;
        Kind = kind;
        Dimensions = dimensions ?? AreaDimensions.Default;
        Entities = materializedEntities;
        Portals = materializedPortals;
    }

    public AreaDefinition WithEntities(IReadOnlyList<EntityInstance> entities) =>
        new(Id, Slug, DisplayName, entities, Kind, Dimensions, Portals, SchemaVersion);

    public AreaDefinition WithPortals(IReadOnlyList<PortalLink> portals) =>
        new(Id, Slug, DisplayName, Entities, Kind, Dimensions, portals, SchemaVersion);
}
