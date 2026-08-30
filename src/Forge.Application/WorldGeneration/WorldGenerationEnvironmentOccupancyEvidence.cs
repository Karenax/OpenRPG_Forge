using Forge.Domain.Areas;

namespace Forge.Application.WorldGeneration;

public sealed record WorldGenerationEnvironmentOccupancyEvidence(
    int SchemaVersion,
    int Width,
    int Depth,
    float CellSizeMeters,
    IReadOnlyList<AreaEnvironmentOccupancyCell> Cells,
    string Fingerprint)
{
    public const int CurrentSchemaVersion = 1;
}
