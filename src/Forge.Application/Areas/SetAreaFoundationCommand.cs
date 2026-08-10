using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed record SetAreaFoundationCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AreaId AreaId,
    AreaFoundationProfile Foundation,
    bool DryRun);
