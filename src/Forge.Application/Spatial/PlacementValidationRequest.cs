using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Application.Spatial;

public sealed record PlacementValidationRequest(
    AreaId AreaId,
    BlueprintId BlueprintId,
    Vector3Value Position,
    float GridSize,
    EntityId? IgnoredEntityId = null,
    string? RequiredSocket = null,
    IReadOnlyList<string>? AvailableSockets = null,
    long? AvailableBudgetBytes = null,
    long? BlueprintEstimatedBytes = null,
    bool AutoCorrect = true);
