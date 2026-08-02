using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;

namespace Forge.Application.Blueprints;

public sealed record CreateBlueprintFromEntityCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AreaId AreaId,
    EntityId EntityId,
    BlueprintId NewBlueprintId,
    string Slug,
    string DisplayName,
    bool DryRun);
