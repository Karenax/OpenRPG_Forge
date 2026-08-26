using Forge.Domain.Areas;
using Forge.Domain.Validation;

namespace Forge.EngineAdapter.Rendering;

public sealed record AreaMapFloorRenderPlan(
    int Floor,
    int BoundarySegments,
    int PinCount,
    int ExplorationCellCount,
    bool Generate2D,
    bool Generate3D,
    bool FogOfWar);

public sealed record AreaExperienceRenderPlan(
    AreaId AreaId,
    IReadOnlyList<AreaMapFloorRenderPlan> MapFloors,
    IReadOnlyList<AreaVisibilityRoomProfile> Rooms,
    IReadOnlyList<AreaVisibilityPortalProfile> VisibilityPortals,
    IReadOnlyList<AreaOccluderProfile> Occluders,
    IReadOnlyList<float> LodDistanceThresholds,
    IReadOnlyList<AreaLodAssetOverride> LodOverrides,
    int MaximumVisibleTriangles,
    float PoppingTolerance,
    bool CellVisualizer,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class AreaExperienceRenderPlanBuilder
{
    public static AreaExperienceRenderPlan Build(AreaDefinition area)
    {
        ArgumentNullException.ThrowIfNull(area);
        var authoring = area.Authoring;
        var visibility = authoring.Experience.Visibility;
        var lod = authoring.Experience.Lod;
        var grid = new AreaMapExplorationGrid(area.Dimensions, authoring.Map.FloorCount,
            authoring.Map.ExplorationCellSize);
        var floors = Enumerable.Range(0, authoring.Map.FloorCount).Select(floor =>
            new AreaMapFloorRenderPlan(floor, 4,
                authoring.Map.Pins.Count(pin => pin.Floor == floor), grid.CellColumns * grid.CellRows,
                authoring.Map.Generate2D, authoring.Map.Generate3D, authoring.Map.FogOfWar)).ToArray();
        return new(area.Id, floors, visibility.Rooms, visibility.Portals, visibility.Occluders,
            lod.DistanceThresholds, lod.AssetOverrides, lod.MaximumVisibleTriangles,
            lod.PoppingTolerance, visibility.CellVisualizer,
            AreaExperienceAuthoringValidator.Validate(area));
    }
}
