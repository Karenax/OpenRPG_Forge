using Forge.Domain.Areas;

namespace Forge.Application.WorldGeneration;

public static class WorldGenerationSpatialEvidenceValidator
{
    public static IReadOnlyList<string> Validate(
        WorldGenerationTerrainSurfaceEvidence? terrain,
        WorldGenerationEnvironmentOccupancyEvidence? environment)
    {
        var diagnostics = new List<string>();
        if (terrain is null)
        {
            diagnostics.Add("Validation report has no canonical terrain-placement surface.");
        }
        else
        {
            try
            {
                _ = new TerrainPlacementSurfaceDefinition(Guid.Parse("d1580000-0000-4000-8000-000000000010"),
                    terrain.SchemaVersion, terrain.Width, terrain.Depth, terrain.SamplesX, terrain.SamplesZ,
                    terrain.HeightScale, terrain.Heights, terrain.CellDiagonal, terrain.Fingerprint,
                    "validation:world-generation-surface");
                var fingerprint = WorldGenerationSpatialEvidenceFingerprint.ComputeTerrain(
                    terrain.Width, terrain.Depth, terrain.SamplesX, terrain.SamplesZ, terrain.HeightScale,
                    terrain.Heights, terrain.CellDiagonal);
                if (!string.Equals(fingerprint, terrain.Fingerprint, StringComparison.Ordinal))
                    diagnostics.Add("Terrain-placement surface fingerprint does not match its samples.");
            }
            catch (ArgumentException exception)
            {
                diagnostics.Add($"Terrain-placement surface is invalid: {exception.Message}");
            }
        }

        if (environment is null)
        {
            diagnostics.Add("Validation report has no canonical environment-occupancy evidence.");
        }
        else
        {
            try
            {
                _ = new AreaEnvironmentOccupancyProfile(Guid.Parse("d1580000-0000-4000-8000-000000000011"),
                    environment.SchemaVersion, environment.Width, environment.Depth,
                    environment.CellSizeMeters, environment.Cells, environment.Fingerprint,
                    "validation:world-generation-environment");
                var fingerprint = WorldGenerationSpatialEvidenceFingerprint.ComputeEnvironment(
                    environment.Width, environment.Depth, environment.CellSizeMeters, environment.Cells);
                if (!string.Equals(fingerprint, environment.Fingerprint, StringComparison.Ordinal))
                    diagnostics.Add("Environment-occupancy fingerprint does not match its cells.");
            }
            catch (ArgumentException exception)
            {
                diagnostics.Add($"Environment-occupancy evidence is invalid: {exception.Message}");
            }
        }

        if (terrain is not null && environment is not null &&
            (terrain.Width != environment.Width || terrain.Depth != environment.Depth))
            diagnostics.Add("Terrain-placement and environment-occupancy evidence cover different bounds.");
        return diagnostics;
    }
}
