using Forge.Domain.Assets;

namespace Forge.Application.Assets;

public sealed record ExternalWorkerSecurityPolicyState(
    ExternalWorkerSecurityPolicy Policy,
    ExternalWorkerSecurityPolicyRevision Revision)
{
    public ExternalWorkerSecurityPolicyState(ExternalWorkerSecurityPolicy? policy = null)
        : this(policy ?? ExternalWorkerSecurityPolicy.CreateDefault(), new ExternalWorkerSecurityPolicyRevision(0))
    {
    }
}
