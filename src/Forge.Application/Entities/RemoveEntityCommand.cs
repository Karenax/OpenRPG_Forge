using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;

namespace Forge.Application.Entities;

public sealed record RemoveEntityCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AreaId AreaId,
    EntityId EntityId,
    bool DryRun);
