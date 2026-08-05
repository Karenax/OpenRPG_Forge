namespace Forge.Domain.Areas;

public sealed record TerrainHoleDefinition
{
    public Guid Id { get; }
    public string Name { get; }
    public int StartX { get; }
    public int StartZ { get; }
    public int Width { get; }
    public int Depth { get; }
    public string Provenance { get; }

    public TerrainHoleDefinition(Guid id, string name, int startX, int startZ, int width, int depth, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || startX < 0 || startZ < 0 || width <= 0 || depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Terrain hole values are invalid.");
        }

        Id = id;
        Name = name.Trim();
        StartX = startX;
        StartZ = startZ;
        Width = width;
        Depth = depth;
        Provenance = provenance.Trim();
    }
}
