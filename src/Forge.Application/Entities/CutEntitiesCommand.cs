using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;

namespace Forge.Application.Entities;

public sealed record CutEntitiesCommand(
    CommandId CommandId,
    int SchemaVersion,
    string Provenance,
    ProjectRevision BaseRevision,
    AreaId AreaId,
    IReadOnlyList<EntityId> EntityIds,
    bool DryRun);

public sealed record CutEntitiesResult(CommandResult Command, EntityClipboardPayload? Payload)
{
    public bool Succeeded => Command.Succeeded;
}
