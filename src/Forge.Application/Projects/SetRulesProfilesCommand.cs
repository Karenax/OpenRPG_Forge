using Forge.Application.Commands;
using Forge.Domain.Areas;
using Forge.Domain.Rules;

namespace Forge.Application.Projects;

public sealed record SetRulesProfilesCommand(CommandId CommandId, ProjectRevision BaseRevision,
    ModuleRulesProfile ModuleProfile, AreaId AreaId, AreaRulesProfile AreaProfile, bool DryRun);
