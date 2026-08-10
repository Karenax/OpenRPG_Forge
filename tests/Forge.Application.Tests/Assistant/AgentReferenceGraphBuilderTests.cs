using Forge.Application.Assistant;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Assistant;

public sealed class AgentReferenceGraphBuilderTests
{
    [Fact]
    public void GraphIsDeterministicTypedAndSupportsBoundedReverseImpact()
    {
        var blueprintId = BlueprintId.From(Guid.Parse("a2010000-0000-4000-8000-000000000001"));
        var entityId = EntityId.From(Guid.Parse("a2010000-0000-4000-8000-000000000002"));
        var area = new AreaDefinition(AreaId.From(Guid.Parse("a2010000-0000-4000-8000-000000000003")),
            "forge", "Forge", [new EntityInstance(entityId, blueprintId, TransformValue.Identity)]);
        var project = new ProjectDefinition(ProjectId.From(Guid.Parse("a2010000-0000-4000-8000-000000000004")),
            "Agent graph", [new BlueprintDefinition(blueprintId, "warden", "Warden", BlueprintKind.Creature)], [area]);

        var first = AgentReferenceGraphBuilder.Build(project, blueprintId.Value, 2, 50);
        var second = AgentReferenceGraphBuilder.Build(project, blueprintId.Value, 2, 50);

        var edge = Assert.Single(first.Edges, item => item.SourceId == entityId.Value);
        Assert.Equal(blueprintId.Value, edge.TargetId);
        Assert.Contains(entityId.Value, first.ImpactedObjectIds);
        Assert.Equal(first.Nodes, second.Nodes);
        Assert.Equal(first.Edges, second.Edges);
        Assert.Equal(first.ImpactedObjectIds, second.ImpactedObjectIds);
        Assert.NotEqual(Guid.Empty, edge.Id);
    }
}
