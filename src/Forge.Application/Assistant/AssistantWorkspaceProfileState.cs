using Forge.Domain.Assistant;

namespace Forge.Application.Assistant;

public sealed record AssistantWorkspaceProfileState(
    AssistantWorkspaceProfile Profile,
    AssistantWorkspaceProfileRevision Revision)
{
    public AssistantWorkspaceProfileState(AssistantWorkspaceProfile? profile = null)
        : this(profile ?? AssistantWorkspaceProfile.CreateDefault(), new AssistantWorkspaceProfileRevision(0))
    {
    }
}
