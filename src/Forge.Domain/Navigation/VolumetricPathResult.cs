using Forge.Domain.Spatial;

namespace Forge.Domain.Navigation;

public sealed record VolumetricPathResult(bool Succeeded, IReadOnlyList<Vector3Value> Points,
    int VisitedCells, float TotalCost);
