using Forge.Domain.Projects;

namespace Forge.Application.Projects;

public sealed record ProjectState
{
    public ProjectDefinition Project { get; }

    public ProjectRevision Revision { get; }

    public ProjectState(ProjectDefinition project, ProjectRevision revision)
    {
        ArgumentNullException.ThrowIfNull(project);
        Project = project;
        Revision = revision;
    }
}
