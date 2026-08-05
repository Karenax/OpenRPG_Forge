using Forge.Application.Commands;
using Forge.Domain.Areas;
using Forge.Domain.RuntimePolicies;

namespace Forge.Application.Projects;

public sealed record SetRuntimePoliciesCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    ModuleRuntimePolicyProfile ModuleProfile,
    AreaId AreaId,
    AreaRuntimePolicyProfile AreaProfile,
    bool DryRun);
