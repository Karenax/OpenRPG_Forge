using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.RuntimePolicies;

namespace Forge.Application.Areas;

public sealed record SetAreaRuntimePoliciesCommand(CommandId CommandId, ProjectRevision BaseRevision, AreaId AreaId,
    AreaRuntimePolicyProfile Profile, bool DryRun);
