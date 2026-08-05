using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed record SetAreaWorldProfileCommand(CommandId CommandId, ProjectRevision BaseRevision, AreaId AreaId,
    AreaWorldProfile World, bool DryRun);
