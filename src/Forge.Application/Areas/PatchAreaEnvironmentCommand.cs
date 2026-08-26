using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed record PatchAreaEnvironmentCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AreaId AreaId,
    AreaEnvironmentPatch? Patch,
    bool DryRun)
{
    public AreaEnvironmentProfile? Candidate { get; init; }

    public PatchAreaEnvironmentCommand(CommandId commandId, ProjectRevision baseRevision, AreaId areaId,
        AreaEnvironmentProfile candidate, bool dryRun)
        : this(commandId, baseRevision, areaId, (AreaEnvironmentPatch?)null, dryRun)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        Candidate = candidate;
    }
}
