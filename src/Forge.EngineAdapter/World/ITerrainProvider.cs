using Forge.Domain.Spatial;

namespace Forge.EngineAdapter.World;

public interface ITerrainProvider
{
    float SampleHeight(int x, int z);
    bool IsHoleCell(int x, int z);
    Vector3Value PositionOf(int x, int z);
}
