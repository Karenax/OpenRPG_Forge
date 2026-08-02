using Forge.Domain.Areas;
using Forge.Domain.Projects;

namespace Forge.Application.Creator;

public sealed class ActiveAreaContext
{
    public AreaId? Current { get; private set; }

    public event Action? Changed;

    public bool Reset(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var next = project.Areas.Any(area => area.Id == project.Module.EntryAreaId)
            ? project.Module.EntryAreaId
            : project.Areas.FirstOrDefault()?.Id;
        return Set(next);
    }

    public bool Activate(ProjectDefinition project, AreaId areaId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(areaId);
        if (!project.Areas.Any(area => area.Id == areaId))
        {
            return false;
        }

        return Set(areaId);
    }

    public bool EnsureValid(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return Current is not null && project.Areas.Any(area => area.Id == Current)
            ? false
            : Reset(project);
    }

    public bool Clear() => Set(null);

    private bool Set(AreaId? areaId)
    {
        if (Current == areaId)
        {
            return false;
        }

        Current = areaId;
        Changed?.Invoke();
        return true;
    }
}
