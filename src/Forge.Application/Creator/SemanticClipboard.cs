using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Projects;

namespace Forge.Application.Creator;

public enum SemanticClipboardKind
{
    Entities,
    Area,
}

public abstract record SemanticClipboardPayload(int SchemaVersion, SemanticClipboardKind Kind)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record EntityClipboardPayload : SemanticClipboardPayload
{
    public AreaId SourceAreaId { get; }

    public IReadOnlyList<EntityInstance> Entities { get; }

    public EntityClipboardPayload(AreaId sourceAreaId, IReadOnlyList<EntityInstance> entities)
        : base(CurrentSchemaVersion, SemanticClipboardKind.Entities)
    {
        ArgumentNullException.ThrowIfNull(sourceAreaId);
        ArgumentNullException.ThrowIfNull(entities);
        var materialized = entities.ToArray();
        if (materialized.Length == 0)
        {
            throw new ArgumentException("An entity clipboard payload cannot be empty.", nameof(entities));
        }

        if (materialized.Select(entity => entity.Id).Distinct().Count() != materialized.Length)
        {
            throw new ArgumentException("An entity clipboard payload cannot contain duplicate IDs.", nameof(entities));
        }

        SourceAreaId = sourceAreaId;
        Entities = materialized;
    }
}

public sealed record AreaClipboardPayload : SemanticClipboardPayload
{
    public AreaDefinition Area { get; }

    public AreaClipboardPayload(AreaDefinition area)
        : base(CurrentSchemaVersion, SemanticClipboardKind.Area)
    {
        Area = area ?? throw new ArgumentNullException(nameof(area));
    }
}

public sealed class SemanticClipboard
{
    public SemanticClipboardPayload? Payload { get; private set; }

    public bool HasContent => Payload is not null;

    public EntityClipboardPayload CopyEntities(
        ProjectDefinition project,
        AreaId sourceAreaId,
        IReadOnlyCollection<EntityId> entityIds)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(sourceAreaId);
        ArgumentNullException.ThrowIfNull(entityIds);
        var requestedIds = entityIds.ToHashSet();
        if (requestedIds.Count != entityIds.Count || requestedIds.Count == 0)
        {
            throw new ArgumentException("The copied entity IDs must be unique and non-empty.", nameof(entityIds));
        }

        var area = project.Areas.FirstOrDefault(candidate => candidate.Id == sourceAreaId)
            ?? throw new ArgumentException($"Area '{sourceAreaId}' does not exist.", nameof(sourceAreaId));
        var entities = area.Entities.Where(entity => requestedIds.Contains(entity.Id)).ToArray();
        if (entities.Length != requestedIds.Count)
        {
            throw new ArgumentException("At least one copied entity does not exist in the source area.", nameof(entityIds));
        }

        var payload = new EntityClipboardPayload(sourceAreaId, entities);
        Payload = payload;
        return payload;
    }

    public AreaClipboardPayload CopyArea(ProjectDefinition project, AreaId sourceAreaId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(sourceAreaId);
        var area = project.Areas.FirstOrDefault(candidate => candidate.Id == sourceAreaId)
            ?? throw new ArgumentException($"Area '{sourceAreaId}' does not exist.", nameof(sourceAreaId));
        var payload = new AreaClipboardPayload(area);
        Payload = payload;
        return payload;
    }

    public void Clear() => Payload = null;
}
