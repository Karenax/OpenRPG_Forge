using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Application.Entities;

public sealed record PlaceEntityCommand
{
    public CommandId CommandId { get; }

    public ProjectRevision BaseRevision { get; }

    public AreaId AreaId { get; }

    public EntityId EntityId { get; }

    public BlueprintId BlueprintId { get; }

    public TransformValue Transform { get; }

    public bool DryRun { get; }

    public PlaceEntityCommand(
        CommandId commandId,
        ProjectRevision baseRevision,
        AreaId areaId,
        EntityId entityId,
        BlueprintId blueprintId,
        TransformValue transform,
        bool dryRun)
    {
        ArgumentNullException.ThrowIfNull(commandId);
        ArgumentNullException.ThrowIfNull(areaId);
        ArgumentNullException.ThrowIfNull(entityId);
        ArgumentNullException.ThrowIfNull(blueprintId);
        ArgumentNullException.ThrowIfNull(transform);

        CommandId = commandId;
        BaseRevision = baseRevision;
        AreaId = areaId;
        EntityId = entityId;
        BlueprintId = blueprintId;
        Transform = transform;
        DryRun = dryRun;
    }
}
