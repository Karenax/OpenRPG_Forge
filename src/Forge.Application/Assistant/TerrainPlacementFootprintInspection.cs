namespace Forge.Application.Assistant;

public sealed record TerrainPlacementFootprintInspection(
    bool SurfaceAvailable,
    bool InBounds,
    float CenterHeight,
    float MinimumHeight,
    float MaximumHeight,
    float SurfaceVariationMeters,
    int SampleCount);
