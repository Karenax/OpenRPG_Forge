using Forge.Application.Commands;
using Forge.Domain.Areas;
using Forge.Domain.Sequences;

namespace Forge.Application.Projects;

public sealed record SetCinematicProfilesCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    ModuleCinematicProfile ModuleProfile,
    AreaId AreaId,
    AreaCinematicProfile AreaProfile,
    bool DryRun);
