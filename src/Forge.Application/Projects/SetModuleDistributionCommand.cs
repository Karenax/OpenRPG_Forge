using Forge.Application.Commands;
using Forge.Domain.Modules;

namespace Forge.Application.Projects;

public sealed record SetModuleDistributionCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    ModuleDistributionConfiguration Distribution,
    bool DryRun);
