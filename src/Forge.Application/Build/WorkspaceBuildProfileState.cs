using Forge.Domain.Build;

namespace Forge.Application.Build;

public sealed record WorkspaceBuildProfileState(
    WorkspaceBuildProfile Profile,
    WorkspaceBuildProfileRevision Revision)
{
    public WorkspaceBuildProfileState(WorkspaceBuildProfile? profile = null)
        : this(profile ?? WorkspaceBuildProfile.CreateDefault(), new WorkspaceBuildProfileRevision(0)) { }
}
