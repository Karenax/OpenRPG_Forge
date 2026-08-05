namespace Forge.Application.Areas;

public sealed record TerrainBrushStroke(
    TerrainBrushOperation Operation,
    float WorldX,
    float WorldZ,
    float Radius,
    float Strength,
    float TargetHeight = 0,
    Guid MaterialLayerId = default);

public sealed record TerrainBrushEditResult(
    Forge.Domain.Areas.TerrainDefinition Terrain,
    int ChangedSamples,
    int ChangedHoleCells,
    int ChangedMaterialCells);
