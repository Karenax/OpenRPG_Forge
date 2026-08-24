using Forge.Domain.Areas;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.WorldFeatures;

namespace Forge.Domain.Tests.WorldFeatures;

public sealed class VehicleRouteProfileTests
{
    private static readonly AreaId AreaId =
        AreaId.From(Guid.Parse("8b000000-0000-4000-8000-000000000001"));

    [Fact]
    public void Canonical_route_validates_and_samples_without_mutating_the_profile()
    {
        var route = Route();
        var project = Project(route);

        Assert.DoesNotContain(WorldFeatureCatalogValidator.Validate(project), diagnostic =>
            diagnostic.Path.Contains("vehicleRoutes", StringComparison.Ordinal));

        var sample = WorldFeatureRuntimeResolver.SampleVehicleRoute(route, 1);
        Assert.Equal(20, sample.Position.X);
        Assert.Equal(8, sample.SpeedLimit);
        Assert.True(sample.AtStop);
        Assert.Equal(5, sample.WaitSeconds);
        Assert.Equal("server-snapshots", route.NetworkSyncMode);
    }

    [Fact]
    public void Validator_rejects_non_finite_duplicate_and_unsupported_route_values_with_localized_codes()
    {
        var source = Route();
        var duplicatePoint = source.Points[0];
        var invalid = source with
        {
            Points =
            [
                duplicatePoint with { TangentDegrees = double.NaN, Width = double.PositiveInfinity },
                duplicatePoint,
            ],
            Lanes =
            [
                new("primary", double.NaN, double.PositiveInfinity, "forward"),
                new("primary", 0, 8, ""),
            ],
            Stops =
            [
                new(Guid.Empty, Guid.NewGuid(), double.NaN, ""),
                new(Guid.Empty, Guid.NewGuid(), -1, "service"),
            ],
            DefaultSpeedLimit = double.NaN,
            NetworkSyncMode = "peer-authoritative",
        };

        var diagnostics = WorldFeatureCatalogValidator.Validate(Project(invalid));
        var codes = diagnostics.Select(item => item.Code).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("world.vehicle_route.points_invalid", codes);
        Assert.Contains("world.vehicle_route.lanes_invalid", codes);
        Assert.Contains("world.vehicle_route.speed_invalid", codes);
        Assert.Contains("world.vehicle_route.network_sync_invalid", codes);
        Assert.Contains("world.vehicle_route.stop_invalid", codes);
        Assert.All(diagnostics.Where(item => item.Code.StartsWith("world.vehicle_route", StringComparison.Ordinal)),
            item => Assert.Contains("module/worldFeatures/vehicleRoutes/", item.Path, StringComparison.Ordinal));
    }

    private static VehicleRouteProfile Route()
    {
        var routeId = Guid.Parse("8b000000-0000-4000-8000-000000000002");
        var start = Guid.Parse("8b000000-0000-4000-8000-000000000003");
        var end = Guid.Parse("8b000000-0000-4000-8000-000000000004");
        return new(routeId, 1, "test:g13-v", AreaId,
            [new(start, Vector3Value.Zero, 0, 3), new(end, new(20, 0, 0), 0, 3)],
            [new("primary", 0, 8, "forward")],
            [new(Guid.Parse("8b000000-0000-4000-8000-000000000005"), end, 5, "caravan-stop")],
            10, false, "server-snapshots");
    }

    private static ProjectDefinition Project(VehicleRouteProfile route)
    {
        var area = new AreaDefinition(AreaId, "vehicle-route-area", "Vehicle route area", []);
        var project = new ProjectDefinition(
            ProjectId.From(Guid.Parse("8b000000-0000-4000-8000-000000000006")),
            "Vehicle route validation", [], [area]);
        var catalog = new WorldFeatureCatalog(project.Module.Id.Value, 1, "test:g13-v", vehicleRoutes: [route]);
        return new ProjectDefinition(project.Id, project.Title, project.Module.WithWorldFeatures(catalog),
            project.Blueprints, project.Areas);
    }
}
