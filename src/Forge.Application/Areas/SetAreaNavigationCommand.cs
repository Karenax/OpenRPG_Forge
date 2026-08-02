using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;

namespace Forge.Application.Areas;

public sealed record SetAreaNavigationCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AreaId AreaId,
    AreaNavigationDefinition Navigation,
    bool DryRun);
