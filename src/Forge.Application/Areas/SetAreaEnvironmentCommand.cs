using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed record SetAreaEnvironmentCommand(CommandId CommandId, ProjectRevision BaseRevision, AreaId AreaId,
    AreaEnvironmentProfile Environment, bool DryRun);
