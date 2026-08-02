using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;

namespace Forge.Application.Areas;

public sealed record EntityIdRemap(EntityId SourceId, EntityId DestinationId);

public sealed record PortalIdRemap(PortalId SourceId, PortalId DestinationId);

public sealed record DuplicateAreaCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AreaId SourceAreaId,
    AreaId DestinationAreaId,
    string DisplayName,
    AreaCloneMode Mode,
    ExternalPortalPolicy ExternalPortalPolicy,
    IReadOnlyList<EntityIdRemap> EntityIdMap,
    IReadOnlyList<PortalIdRemap> PortalIdMap,
    bool DryRun);
