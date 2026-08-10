using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed record SetAreaPlaytestCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AreaId AreaId,
    AreaPlaytestProfile Profile,
    bool DryRun);
