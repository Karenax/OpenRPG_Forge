namespace Forge.Domain.Areas;

public sealed record TerrainMaterialPaintCell
{
    public int X { get; }
    public int Z { get; }
    public Guid MaterialLayerId { get; }

    public TerrainMaterialPaintCell(int x, int z, Guid materialLayerId)
    {
        if (x < 0 || z < 0 || materialLayerId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Terrain material paint cell is invalid.");
        }

        X = x;
        Z = z;
        MaterialLayerId = materialLayerId;
    }
}
