using Forge.Application.Commands;
using Forge.Domain.Assets;
using Forge.Domain.Assistant;

namespace Forge.Application.Assets;

public sealed record SetExternalWorkerSecurityPolicyCommand(
    CommandId CommandId,
    Guid PlanId,
    ExternalWorkerSecurityPolicyRevision BaseRevision,
    ExternalWorkerSecurityPolicy Policy,
    AssistantPermissionLevel Permission,
    bool DryRun);
