using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Entities;

namespace Forge.Application.Areas;

public sealed record EditAreaGeometryCommand(CommandId CommandId, ProjectRevision BaseRevision, AreaId AreaId,
    AreaDimensions Dimensions, IReadOnlyList<EntityInstance> Entities, string Reason, bool DryRun,
    AreaFoundationProfile? Foundation = null);
