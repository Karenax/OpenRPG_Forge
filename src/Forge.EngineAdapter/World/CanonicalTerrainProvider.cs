using Forge.Domain.Areas;
using Forge.Domain.Spatial;

namespace Forge.EngineAdapter.World;

public sealed class CanonicalTerrainProvider : ITerrainProvider
{
    private readonly TerrainDefinition _terrain;

    public CanonicalTerrainProvider(TerrainDefinition terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        _terrain = terrain;
    }

    public float SampleHeight(int x, int z) => _terrain.SampleHeight(x, z);

    public bool IsHoleCell(int x, int z) => _terrain.IsHoleCell(x, z);

    public Vector3Value PositionOf(int x, int z) => new(
        (-_terrain.Width / 2f) + (x * (_terrain.Width / (float)(_terrain.SamplesX - 1))),
        SampleHeight(x, z),
        (-_terrain.Depth / 2f) + (z * (_terrain.Depth / (float)(_terrain.SamplesZ - 1))));
}
