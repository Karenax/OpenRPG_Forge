using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;

namespace Forge.Application.Areas;

public sealed record EditTerrainBrushCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AreaId AreaId,
    TerrainBrushStroke Stroke,
    bool DryRun);
