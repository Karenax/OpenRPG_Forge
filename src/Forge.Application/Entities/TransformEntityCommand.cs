using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Application.Entities;

public sealed record TransformEntityCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AreaId AreaId,
    EntityId EntityId,
    TransformValue Transform,
    bool DryRun);
