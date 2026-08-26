using Forge.Domain.Spatial;

namespace Forge.EngineAdapter.World;

public sealed record TerrainBuildVertex(
    Vector3Value Position,
    Vector3Value Normal,
    float U,
    float V,
    string AlbedoColor);
