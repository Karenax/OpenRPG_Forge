using Forge.Application.Commands;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;

namespace Forge.Application.Areas;

public sealed record CreateAreaCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AreaId AreaId,
    string DisplayName,
    AreaKind Kind,
    AreaDimensions Dimensions,
    AreaTemplateKind Template,
    BlueprintId? SpawnBlueprintId,
    EntityId? SpawnEntityId,
    bool DryRun,
    AreaFoundationProfile? Foundation = null);
