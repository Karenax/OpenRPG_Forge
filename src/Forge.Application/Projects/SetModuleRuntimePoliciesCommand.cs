using Forge.Application.Commands;
using Forge.Domain.RuntimePolicies;

namespace Forge.Application.Projects;

public sealed record SetModuleRuntimePoliciesCommand(CommandId CommandId, ProjectRevision BaseRevision,
    ModuleRuntimePolicyProfile Profile, bool DryRun);
