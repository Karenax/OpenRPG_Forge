using Forge.Domain.Areas;
using Forge.Domain.Validation;

namespace Forge.EngineAdapter.World;

public sealed record TerrainBuildPlan(
    bool Enabled,
    IReadOnlyList<TerrainBuildVertex> Vertices,
    IReadOnlyList<int> Indices,
    int ChunkCount,
    IReadOnlyList<TerrainFeatureDefinition> Features,
    VolumetricNavigationProfile VolumetricNavigation,
    IReadOnlyList<FlightRouteDefinition> FlightRoutes,
    IReadOnlyList<DomainDiagnostic> Diagnostics)
{
    public int TriangleCount => Indices.Count / 3;
}
