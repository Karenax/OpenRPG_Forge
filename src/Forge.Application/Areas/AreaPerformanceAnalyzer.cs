using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;

namespace Forge.Application.Areas;

public sealed record AreaPerformanceBudget(int MaximumEntities, int MaximumUniqueAssets, int MaximumEstimatedDrawUnits)
{
    public static AreaPerformanceBudget EditorDefault { get; } = new(2_000, 500, 4_000);
}

public sealed record AreaPerformanceReport(
    AreaId AreaId,
    int EntityCount,
    int UniqueAssetCount,
    int EstimatedDrawUnits,
    int OccupiedOcclusionSectors,
    int NearLodCount,
    int MidLodCount,
    int FarLodCount,
    bool IsWithinBudget,
    IReadOnlyList<string> Diagnostics);

public static class AreaPerformanceAnalyzer
{
    public static AreaPerformanceReport Analyze(ProjectDefinition project, AreaId areaId,
        AreaPerformanceBudget? budget = null, float sectorSize = 8f)
    {
        ArgumentNullException.ThrowIfNull(project);
        var area = project.Areas.SingleOrDefault(candidate => candidate.Id == areaId)
            ?? throw new ArgumentException("Area does not exist.", nameof(areaId));
        budget ??= AreaPerformanceBudget.EditorDefault;
        if (sectorSize <= 0 || !float.IsFinite(sectorSize)) throw new ArgumentOutOfRangeException(nameof(sectorSize));
        var blueprints = project.Blueprints.ToDictionary(item => item.Id);
        var unique = area.Entities.Select(item => item.BlueprintId).Distinct().Count();
        var draw = area.Entities.Sum(entity => blueprints.GetValueOrDefault(entity.BlueprintId)?.Kind switch
        {
            BlueprintKind.Creature => 4,
            BlueprintKind.Placeable or BlueprintKind.Door => 2,
            _ => 1,
        });
        var sectors = area.Entities.Select(entity => (
            X: (int)MathF.Floor(entity.Transform.Position.X / sectorSize),
            Z: (int)MathF.Floor(entity.Transform.Position.Z / sectorSize))).Distinct().Count();
        var distances = area.Entities.Select(entity => MathF.Sqrt(
            entity.Transform.Position.X * entity.Transform.Position.X + entity.Transform.Position.Z * entity.Transform.Position.Z)).ToArray();
        var diagnostics = new List<string>();
        if (area.Entities.Count > budget.MaximumEntities) diagnostics.Add($"entities:{area.Entities.Count}>{budget.MaximumEntities}");
        if (unique > budget.MaximumUniqueAssets) diagnostics.Add($"assets:{unique}>{budget.MaximumUniqueAssets}");
        if (draw > budget.MaximumEstimatedDrawUnits) diagnostics.Add($"draw-units:{draw}>{budget.MaximumEstimatedDrawUnits}");
        return new(areaId, area.Entities.Count, unique, draw, sectors,
            distances.Count(value => value < 20), distances.Count(value => value is >= 20 and < 50),
            distances.Count(value => value >= 50), diagnostics.Count == 0, diagnostics);
    }
}
