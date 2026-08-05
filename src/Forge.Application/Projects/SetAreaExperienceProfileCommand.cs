using Forge.Application.Commands;
using Forge.Domain.Areas;
using Forge.Domain.RuntimePolicies;

namespace Forge.Application.Projects;

public sealed record SetAreaExperienceProfileCommand(CommandId CommandId, ProjectRevision BaseRevision,
    AreaId AreaId, AreaExperienceProfile Profile, bool DryRun);
