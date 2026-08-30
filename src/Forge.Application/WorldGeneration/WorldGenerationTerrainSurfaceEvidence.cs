using Forge.Domain.Areas;

namespace Forge.Application.WorldGeneration;

public sealed record WorldGenerationTerrainSurfaceEvidence(
    int SchemaVersion,
    int Width,
    int Depth,
    int SamplesX,
    int SamplesZ,
    float HeightScale,
    IReadOnlyList<float> Heights,
    TerrainCellDiagonal CellDiagonal,
    string Fingerprint)
{
    public const int CurrentSchemaVersion = 1;
}
