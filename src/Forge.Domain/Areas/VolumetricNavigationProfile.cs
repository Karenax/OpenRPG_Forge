using Forge.Domain.Spatial;

namespace Forge.Domain.Areas;

public sealed record VolumetricNavigationProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public bool Enabled { get; }
    public VolumetricNavigationMode Modes { get; }
    public float CellSize { get; }
    public Vector3Value Center { get; }
    public Vector3Value Size { get; }
    public float AvoidanceRadius { get; }
    public IReadOnlyList<NavigationVolumeDefinition> Obstacles { get; }
    public IReadOnlyList<NavigationVolumeDefinition> CostVolumes { get; }
    public bool DebugVisible { get; }
    public string Provenance { get; }

    public VolumetricNavigationProfile(Guid id, int schemaVersion, bool enabled, VolumetricNavigationMode modes,
        float cellSize, Vector3Value center, Vector3Value size, float avoidanceRadius,
        IReadOnlyList<NavigationVolumeDefinition> obstacles, IReadOnlyList<NavigationVolumeDefinition> costVolumes,
        bool debugVisible, string provenance)
    {
        ArgumentNullException.ThrowIfNull(obstacles);
        ArgumentNullException.ThrowIfNull(costVolumes);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || !float.IsFinite(cellSize) ||
            cellSize is < 0.5f or > 64 || size.X <= 0 || size.Y <= 0 || size.Z <= 0 ||
            !float.IsFinite(avoidanceRadius) || avoidanceRadius is < 0 or > 64 ||
            enabled && modes == VolumetricNavigationMode.None)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Volumetric navigation profile is invalid.");
        }

        var obstacleValues = obstacles.OrderBy(volume => volume.Id).ToArray();
        var costValues = costVolumes.OrderBy(volume => volume.Id).ToArray();
        if (obstacleValues.Concat(costValues).Select(volume => volume.Id).Distinct().Count() != obstacleValues.Length + costValues.Length)
        {
            throw new ArgumentException("Navigation volume IDs must be unique.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Enabled = enabled;
        Modes = modes;
        CellSize = cellSize;
        Center = center;
        Size = size;
        AvoidanceRadius = avoidanceRadius;
        Obstacles = obstacleValues;
        CostVolumes = costValues;
        DebugVisible = debugVisible;
        Provenance = provenance.Trim();
    }
}
