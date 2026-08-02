using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed record UpsertPortalLinkCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AreaId SourceAreaId,
    PortalLink Portal,
    bool DryRun);
