using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Areas;
using Forge.Domain.Entities;
using Forge.Domain.Validation;

namespace Forge.Application.Areas;

public sealed record AreaHlodClusterGenerationHook(
    Guid Id,
    int CellX,
    int CellZ,
    IReadOnlyList<EntityId> SourceEntityIds,
    string Operation,
    string OutputRelativePath,
    int MaximumTriangles);

public sealed record AreaHlodGenerationPlan(
    bool CanGenerate,
    bool DryRun,
    AreaLodSourceMode SourceMode,
    float ClusterSize,
    bool CrossFade,
    bool MeasurePopping,
    float PoppingTolerance,
    IReadOnlyList<AreaHlodClusterGenerationHook> Clusters,
    IReadOnlyList<DomainDiagnostic> Diagnostics);

public static class AreaHlodGenerationPlanner
{
    public const string ClusterBudgetCode = "area.hlod.cluster_budget_exceeded";
    public const string GenerateOperation = "area.hlod.generate";

    public static AreaHlodGenerationPlan Plan(AreaDefinition area, bool dryRun = true)
    {
        ArgumentNullException.ThrowIfNull(area);
        var profile = area.Authoring.Experience.Lod;
        if (profile.DefaultSourceMode == AreaLodSourceMode.Imported)
        {
            return new(true, dryRun, profile.DefaultSourceMode, profile.HlodClusterSize,
                profile.CrossFade, profile.MeasurePopping, profile.PoppingTolerance, [], []);
        }

        var groups = area.Entities
            .GroupBy(entity => (
                X: (int)MathF.Floor(entity.Transform.Position.X / profile.HlodClusterSize),
                Z: (int)MathF.Floor(entity.Transform.Position.Z / profile.HlodClusterSize)))
            .Where(group => group.Count() >= 2)
            .OrderBy(group => group.Key.X)
            .ThenBy(group => group.Key.Z)
            .ToArray();
        if (groups.Length > profile.MaximumHlodClusters)
        {
            return new(false, dryRun, profile.DefaultSourceMode, profile.HlodClusterSize,
                profile.CrossFade, profile.MeasurePopping, profile.PoppingTolerance, [],
                [new(
                    ClusterBudgetCode,
                    $"HLOD generation requires {groups.Length} clusters; area budget is {profile.MaximumHlodClusters}.",
                    $"areas/{area.Id}/authoring/experience/lod/maximumHlodClusters")]);
        }

        var triangleBudget = Math.Max(1, profile.MaximumVisibleTriangles / Math.Max(1, groups.Length));
        var clusters = groups.Select(group =>
        {
            var sources = group.OrderBy(entity => entity.Id.ToString(), StringComparer.Ordinal)
                .Select(entity => entity.Id)
                .ToArray();
            var id = StableId(area.Id, group.Key.X, group.Key.Z, sources);
            return new AreaHlodClusterGenerationHook(
                id,
                group.Key.X,
                group.Key.Z,
                sources,
                GenerateOperation,
                $".forge/generated/hlod/{area.Id}/{id:D}.glb",
                triangleBudget);
        }).ToArray();
        return new(true, dryRun, profile.DefaultSourceMode, profile.HlodClusterSize,
            profile.CrossFade, profile.MeasurePopping, profile.PoppingTolerance, clusters, []);
    }

    private static Guid StableId(AreaId areaId, int cellX, int cellZ, IReadOnlyList<EntityId> sources)
    {
        var canonical = $"openrpg:hlod:v1:{areaId}:{cellX}:{cellZ}:{string.Join(',', sources)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        Span<byte> id = stackalloc byte[16];
        bytes.AsSpan(0, id.Length).CopyTo(id);
        id[6] = (byte)((id[6] & 0x0f) | 0x40);
        id[8] = (byte)((id[8] & 0x3f) | 0x80);
        return new Guid(id);
    }
}
