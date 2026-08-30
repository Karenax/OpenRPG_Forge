using Forge.Application.Assistant;

namespace Forge.Mcp.Projects;

public sealed partial class ForgeProjectAgentService
{
    public TerrainPlacementCandidateResource FindTerrainPlacementCandidates(
        TerrainPlacementCandidateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (request.SchemaVersion != 1 || !Guid.TryParse(request.AreaId, out var areaId))
                return Failure("assistant.terrain_candidates.request_invalid",
                    "Terrain-candidate request schema or area identity is invalid.");
            var area = _history.State.Project.Areas.SingleOrDefault(item => item.Id.Value == areaId);
            if (area is null)
                return Failure("assistant.terrain_candidates.area_missing",
                    $"Area '{request.AreaId}' does not exist.");
            if (area.World.PlacementSurface is null && !area.World.Terrain.Enabled)
                return Failure("assistant.terrain_candidates.surface_missing",
                    "Area has no canonical terrain-placement surface.");
            if ((request.MinimumNearbyEnvironmentInstances > 0 ||
                 request.MinimumEnvironmentDensityPerHectare > 0) && area.World.EnvironmentOccupancy is null)
                return Failure("assistant.terrain_candidates.environment_missing",
                    "Area has no canonical environment occupancy for the requested density gate.");
            if (string.Equals(request.EnvironmentCategory, "vegetation", StringComparison.OrdinalIgnoreCase) &&
                request.MinimumNearbyEnvironmentInstances > 0 &&
                request.MinimumEnvironmentDensityPerHectare <
                Forge.Domain.Assistant.AssistedAreaEnvironmentIntent.MinimumRequiredVegetationDensityPerHectare)
                return Failure("assistant.terrain_candidates.vegetation_density_missing",
                    "Vegetation-aware placement requires an explicit density of at least 6 instances/ha.");

            try
            {
                var candidates = TerrainPlacementCandidateFinder.Find(
                    area,
                    request.FootprintWidth,
                    request.FootprintDepth,
                    request.PreferredX,
                    request.PreferredZ,
                    request.SearchRadiusMeters,
                    request.StepMeters,
                    request.MaximumSurfaceVariationMeters,
                    request.EnvironmentCategory,
                    request.MinimumNearbyEnvironmentInstances,
                    request.EnvironmentRadiusMeters,
                    request.MaximumFootprintEnvironmentInstances,
                    request.Limit,
                    request.MinimumEnvironmentDensityPerHectare);
                var diagnostics = candidates.Count > 0
                    ? Array.Empty<AgentDiagnostic>()
                    : [new AgentDiagnostic("assistant.terrain_candidates.none",
                        "No candidate satisfies the approved terrain and environment constraints.",
                        $"areas/{area.Id}/world")];
                return new(candidates.Count > 0, _history.State.Revision.Value, area.Id.ToString(),
                    area.World.PlacementSurface?.SourceFingerprint,
                    area.World.EnvironmentOccupancy?.SourceFingerprint,
                    candidates,
                    diagnostics);
            }
            catch (ArgumentException exception)
            {
                return Failure("assistant.terrain_candidates.request_invalid", exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return Failure("assistant.terrain_candidates.search_invalid", exception.Message);
            }

            TerrainPlacementCandidateResource Failure(string code, string message) => new(
                false,
                _history.State.Revision.Value,
                request.AreaId,
                null,
                null,
                [],
                [new AgentDiagnostic(code, message, "assistant/terrain-candidates")]);
        }
    }
}
