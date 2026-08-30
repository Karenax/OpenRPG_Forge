namespace Forge.Application.Assistant;

public sealed record TerrainPlacementCandidate(
    float X,
    float Z,
    float GroundHeight,
    float SurfaceVariationMeters,
    int NearbyEnvironmentInstances,
    int FootprintEnvironmentInstances,
    float DistanceFromPreferredMeters,
    float NearbyEnvironmentDensityPerHectare)
{
    public float StructureAnchorHeight => GroundHeight;
    public float PlayerSpawnAnchorHeight =>
        GroundHeight + TerrainPlacementGrounding.PlayerSpawnCapsuleCenterOffsetMeters;
}
