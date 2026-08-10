using Forge.Application.Commands;
using Forge.Application.Creator;
using Forge.Application.Projects;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Application.Entities;

public sealed record PasteEntitiesCommand(
    CommandId CommandId,
    ProjectRevision BaseRevision,
    AreaId TargetAreaId,
    EntityClipboardPayload Payload,
    IReadOnlyDictionary<EntityId, EntityId> EntityIdMap,
    Vector3Value PositionOffset,
    bool DryRun,
    SemanticPasteMode Mode = SemanticPasteMode.Instance,
    IReadOnlyDictionary<BlueprintId, BlueprintId>? BlueprintIdMap = null);
