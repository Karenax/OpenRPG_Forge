using Forge.Application.Areas;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Areas;

public sealed class AreaHlodGenerationPlannerTests
{
    [Fact]
    public void ProducesDeterministicConfinedGenerationHooksForPopulatedClusters()
    {
        var areaId = AreaId.From(Guid.Parse("e1150000-0000-4000-8000-000000000001"));
        var blueprintId = BlueprintId.From(Guid.Parse("e1150000-0000-4000-8000-000000000002"));
        EntityInstance Entity(string id, float x, float z) => new(
            EntityId.From(Guid.Parse(id)), blueprintId,
            new(new(x, 0, z), QuaternionValue.Identity, Vector3Value.One));
        var area = new AreaDefinition(areaId, "r115-hlod", "R115 HLOD",
        [
            Entity("e1150000-0000-4000-8000-000000000010", 1, 1),
            Entity("e1150000-0000-4000-8000-000000000011", 4, 3),
            Entity("e1150000-0000-4000-8000-000000000012", 70, 2),
        ]);

        var first = AreaHlodGenerationPlanner.Plan(area);
        var second = AreaHlodGenerationPlanner.Plan(area);

        var cluster = Assert.Single(first.Clusters);
        Assert.True(first.CanGenerate);
        Assert.True(first.DryRun);
        Assert.Equal(AreaHlodGenerationPlanner.GenerateOperation, cluster.Operation);
        Assert.Equal(2, cluster.SourceEntityIds.Count);
        Assert.StartsWith($".forge/generated/hlod/{areaId}/", cluster.OutputRelativePath);
        Assert.EndsWith(".glb", cluster.OutputRelativePath);
        var repeated = Assert.Single(second.Clusters);
        Assert.Equal(cluster.Id, repeated.Id);
        Assert.Equal(cluster.OutputRelativePath, repeated.OutputRelativePath);
        Assert.Equal(cluster.SourceEntityIds, repeated.SourceEntityIds);
    }

    [Fact]
    public void RefusesGenerationWhenPopulatedClustersExceedAreaBudget()
    {
        var areaId = AreaId.From(Guid.Parse("e1150000-0000-4000-8000-000000000101"));
        var blueprintId = BlueprintId.From(Guid.Parse("e1150000-0000-4000-8000-000000000102"));
        EntityInstance Entity(string id, float x) => new(
            EntityId.From(Guid.Parse(id)), blueprintId,
            new(new(x, 0, 1), QuaternionValue.Identity, Vector3Value.One));
        var basis = AreaAuthoringProfile.CreateDefault(areaId);
        var experience = new AreaExperienceAuthoringProfile(
            basis.Experience.Id,
            basis.Experience.SchemaVersion,
            "test:r115-budget",
            basis.Experience.AudioSnapshots,
            basis.Experience.Visibility,
            basis.Experience.Lod with { MaximumHlodClusters = 1 },
            true);
        var authoring = new AreaAuthoringProfile(
            basis.SchemaVersion, basis.UserEvents, basis.Map, basis.Layers, experience);
        var area = new AreaDefinition(areaId, "r115-budget", "R115 Budget",
        [
            Entity("e1150000-0000-4000-8000-000000000110", 1),
            Entity("e1150000-0000-4000-8000-000000000111", 2),
            Entity("e1150000-0000-4000-8000-000000000112", 65),
            Entity("e1150000-0000-4000-8000-000000000113", 66),
        ], authoring: authoring);

        var result = AreaHlodGenerationPlanner.Plan(area, dryRun: false);

        Assert.False(result.CanGenerate);
        Assert.False(result.DryRun);
        Assert.Empty(result.Clusters);
        Assert.Equal(AreaHlodGenerationPlanner.ClusterBudgetCode, Assert.Single(result.Diagnostics).Code);
    }
}
