using Forge.Domain.Areas;
using Forge.Domain.Spatial;

namespace Forge.Domain.WorldFeatures;

public sealed record VehicleRouteProfile(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    AreaId AreaId,
    IReadOnlyList<VehicleRouteProfile.RoutePoint> Points,
    IReadOnlyList<VehicleRouteProfile.Lane> Lanes,
    IReadOnlyList<VehicleRouteProfile.Stop> Stops,
    double DefaultSpeedLimit,
    bool AllowOvertaking,
    string NetworkSyncMode)
{
    public sealed record RoutePoint(Guid Id, Vector3Value Position, double TangentDegrees, double Width);
    public sealed record Lane(string Id, double LateralOffset, double SpeedLimit, string Direction);
    public sealed record Stop(Guid Id, Guid RoutePointId, double WaitSeconds, string ServiceId);
}
