using Forge.Domain.Spatial;

namespace Forge.Domain.Navigation;

public sealed class VolumetricNavigationGraph
{
    private readonly HashSet<VolumetricCell> _blocked;
    private readonly IReadOnlyDictionary<VolumetricCell, float> _costs;

    public Vector3Value Minimum { get; }
    public float CellSize { get; }
    public int WidthCells { get; }
    public int HeightCells { get; }
    public int DepthCells { get; }
    public IReadOnlySet<VolumetricCell> Blocked => _blocked;
    public IReadOnlyDictionary<VolumetricCell, float> Costs => _costs;

    public VolumetricNavigationGraph(Vector3Value minimum, float cellSize, int widthCells, int heightCells,
        int depthCells, IEnumerable<VolumetricCell> blocked, IReadOnlyDictionary<VolumetricCell, float> costs)
    {
        ArgumentNullException.ThrowIfNull(blocked);
        ArgumentNullException.ThrowIfNull(costs);
        if (cellSize <= 0 || widthCells <= 0 || heightCells <= 0 || depthCells <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize));
        }

        Minimum = minimum;
        CellSize = cellSize;
        WidthCells = widthCells;
        HeightCells = heightCells;
        DepthCells = depthCells;
        _blocked = blocked.ToHashSet();
        _costs = new Dictionary<VolumetricCell, float>(costs);
    }

    public bool Contains(VolumetricCell cell) => cell.X >= 0 && cell.X < WidthCells &&
        cell.Y >= 0 && cell.Y < HeightCells && cell.Z >= 0 && cell.Z < DepthCells;

    public bool IsPassable(VolumetricCell cell) => Contains(cell) && !_blocked.Contains(cell);

    public float CostAt(VolumetricCell cell) => _costs.TryGetValue(cell, out var value) ? value : 1;

    public VolumetricCell CellAt(Vector3Value point) => new(
        (int)MathF.Floor((point.X - Minimum.X) / CellSize),
        (int)MathF.Floor((point.Y - Minimum.Y) / CellSize),
        (int)MathF.Floor((point.Z - Minimum.Z) / CellSize));

    public Vector3Value CenterOf(VolumetricCell cell) => new(
        Minimum.X + ((cell.X + .5f) * CellSize),
        Minimum.Y + ((cell.Y + .5f) * CellSize),
        Minimum.Z + ((cell.Z + .5f) * CellSize));
}
