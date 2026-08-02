using Forge.Domain.Entities;

namespace Forge.Domain.Areas;

public sealed record AreaDefinition
{
    public AreaId Id { get; }

    public string Slug { get; }

    public string DisplayName { get; }

    public IReadOnlyList<EntityInstance> Entities { get; }

    public AreaDefinition(
        AreaId id,
        string slug,
        string displayName,
        IReadOnlyList<EntityInstance> entities)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(entities);

        var materializedEntities = entities.ToArray();
        if (materializedEntities.Select(entity => entity.Id).Distinct().Count() != materializedEntities.Length)
        {
            throw new ArgumentException("An area cannot contain duplicate entity IDs.", nameof(entities));
        }

        Id = id;
        Slug = slug;
        DisplayName = displayName;
        Entities = materializedEntities;
    }
}
