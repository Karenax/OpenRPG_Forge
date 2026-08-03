using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Application.Entities;

public sealed record ScaleEntitiesCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AreaId AreaId,
    IReadOnlyList<EntityId> EntityIds,
    Vector3Value Scale,
    bool Multiply,
    bool DryRun);
