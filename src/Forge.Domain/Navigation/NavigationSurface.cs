using Forge.Domain.Areas;
using Forge.Domain.Spatial;

namespace Forge.Domain.Navigation;

public sealed record NavigationSurface
{
    public const int CurrentSchemaVersion = 1;

    private readonly HashSet<NavigationCell> _walkableSet;

    public NavigationSurfaceId Id { get; }

    public int SchemaVersion { get; }

    public AreaId AreaId { get; }

    public NavigationAgentProfileId AgentProfileId { get; }

    public NavigationDoorState DoorState { get; }

    public int WidthCells { get; }

    public int DepthCells { get; }

    public float CellSize { get; }

    public float MinimumX { get; }

    public float MinimumZ { get; }

    public IReadOnlyList<NavigationCell> WalkableCells { get; }

    public IReadOnlyList<NavigationEdge> OffMeshEdges { get; }

    public string SourceFingerprint { get; }

    public string Provenance { get; }

    public NavigationSurface(
        NavigationSurfaceId id,
        AreaId areaId,
        NavigationAgentProfileId agentProfileId,
        NavigationDoorState doorState,
        int widthCells,
        int depthCells,
        float cellSize,
        float minimumX,
        float minimumZ,
        IReadOnlyList<NavigationCell> walkableCells,
        IReadOnlyList<NavigationEdge>? offMeshEdges,
        string sourceFingerprint,
        string provenance,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(areaId);
        ArgumentNullException.ThrowIfNull(agentProfileId);
        ArgumentNullException.ThrowIfNull(walkableCells);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion),
                $"Navigation surface schema '{schemaVersion}' is not supported.");
        }

        if (widthCells <= 0 || depthCells <= 0 || !float.IsFinite(cellSize) || cellSize <= 0 ||
            !float.IsFinite(minimumX) || !float.IsFinite(minimumZ))
        {
            throw new ArgumentOutOfRangeException(nameof(widthCells), "Navigation surface dimensions are invalid.");
        }

        var cells = walkableCells.Distinct().Order().ToArray();
        if (cells.Any(cell => cell.X < 0 || cell.X >= widthCells || cell.Z < 0 || cell.Z >= depthCells))
        {
            throw new ArgumentException("Navigation surface contains an out-of-bounds cell.", nameof(walkableCells));
        }

        var edges = (offMeshEdges ?? []).
            OrderBy(edge => edge.From).
            ThenBy(edge => edge.To).
            ThenBy(edge => edge.LinkId.ToString(), StringComparer.Ordinal).
            ToArray();
        if (edges.Any(edge => !cells.Contains(edge.From) || !cells.Contains(edge.To)))
        {
            throw new ArgumentException("Off-mesh edges must connect walkable cells.", nameof(offMeshEdges));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        AreaId = areaId;
        AgentProfileId = agentProfileId;
        DoorState = doorState;
        WidthCells = widthCells;
        DepthCells = depthCells;
        CellSize = cellSize;
        MinimumX = minimumX;
        MinimumZ = minimumZ;
        WalkableCells = cells;
        OffMeshEdges = edges;
        SourceFingerprint = sourceFingerprint.Trim().ToLowerInvariant();
        Provenance = provenance.Trim();
        _walkableSet = cells.ToHashSet();
    }

    public bool IsWalkable(NavigationCell cell) => _walkableSet.Contains(cell);

    public NavigationCell CellAt(Vector3Value position) => new(
        (int)MathF.Floor((position.X - MinimumX) / CellSize),
        (int)MathF.Floor((position.Z - MinimumZ) / CellSize));

    public Vector3Value CenterOf(NavigationCell cell) => new(
        MinimumX + ((cell.X + 0.5f) * CellSize),
        0.04f,
        MinimumZ + ((cell.Z + 0.5f) * CellSize));
}
