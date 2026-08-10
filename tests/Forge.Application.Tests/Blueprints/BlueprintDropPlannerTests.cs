using Forge.Application.Blueprints;
using Forge.Application.Tests.Entities;
using Forge.Domain.Blueprints;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Blueprints;

public sealed class BlueprintDropPlannerTests
{
    [Fact]
    public void PlannerProducesTypedPlansForEverySupportedContext()
    {
        var context = new EntityMutationTestContext();
        var project = AddBlueprint(context.State.Project, CreateBlueprint(BlueprintKind.Item));
        var item = project.Blueprints.Single(blueprint => blueprint.Kind == BlueprintKind.Item);
        var planner = new BlueprintDropPlanner();

        var viewport = planner.Plan(project, Request(
            context.BlueprintId, BlueprintDropTargetKind.Viewport,
            $"areas/{context.AreaId}/entities", context.AreaId, new Vector3Value(3.6f, 0, 2.6f)));
        var graph = planner.Plan(project, Request(
            context.BlueprintId, BlueprintDropTargetKind.Graph, "dialogues/sample/nodes/node-1/speakerBlueprintId"));
        var inventory = planner.Plan(project, Request(
            item.Id, BlueprintDropTargetKind.Inventory, "areas/sample/entities/actor/inventory/items"));
        var property = planner.Plan(project, Request(
            context.BlueprintId, BlueprintDropTargetKind.Property, "areas/sample/entities/actor/properties/appearance"));
        var package = planner.Plan(project, Request(
            item.Id, BlueprintDropTargetKind.Package, "module/content/packages/selection"));

        Assert.Equal(BlueprintDropOperationKind.PlaceEntity, viewport.Operation);
        Assert.Equal(new Vector3Value(4, 0, 3), viewport.CorrectedPosition);
        Assert.Equal(BlueprintDropOperationKind.CreateGraphReference, graph.Operation);
        Assert.Equal(BlueprintDropOperationKind.AddInventoryItem, inventory.Operation);
        Assert.Equal(BlueprintDropOperationKind.SetBlueprintReference, property.Operation);
        Assert.Equal(BlueprintDropOperationKind.IncludePackageDependency, package.Operation);
        Assert.All([viewport, graph, inventory, property, package], plan =>
        {
            Assert.True(plan.IsValid, plan.Message);
            Assert.Single(plan.Changes);
            Assert.NotEqual(Guid.Empty, plan.RequestId);
        });
    }

    [Fact]
    public void PlannerRejectsWrongInventoryKindAndUngovernedPackageWithLocalizedCodes()
    {
        var context = new EntityMutationTestContext();
        var planner = new BlueprintDropPlanner();

        var inventory = planner.Plan(context.State.Project, Request(
            context.BlueprintId, BlueprintDropTargetKind.Inventory, "inventories/player/items"));
        var package = planner.Plan(context.State.Project, Request(
            context.BlueprintId, BlueprintDropTargetKind.Package, "module/content/packages/selection"));

        Assert.Equal(BlueprintDropPlanner.InventoryKindCode, inventory.Code);
        Assert.Equal(BlueprintDropPlanner.PackageGovernanceCode, package.Code);
        Assert.Equal("inventories/player/items", inventory.Path);
        Assert.Equal("module/content/packages/selection", package.Path);
    }

    [Fact]
    public void PlannerRejectsInvalidSchemaProvenanceAndTraversalBeforeMutation()
    {
        var context = new EntityMutationTestContext();
        var planner = new BlueprintDropPlanner();
        var invalidSchema = Request(context.BlueprintId, BlueprintDropTargetKind.Property, "module/property") with
        {
            SchemaVersion = 99,
        };
        var invalidProvenance = Request(context.BlueprintId, BlueprintDropTargetKind.Property, "module/property") with
        {
            Provenance = "unverified",
        };
        var traversal = Request(context.BlueprintId, BlueprintDropTargetKind.Property, "../project.json");

        Assert.Equal(BlueprintDropPlanner.UnsupportedSchemaCode, planner.Plan(context.State.Project, invalidSchema).Code);
        Assert.Equal(BlueprintDropPlanner.MissingProvenanceCode, planner.Plan(context.State.Project, invalidProvenance).Code);
        Assert.Equal(BlueprintDropPlanner.InvalidTargetPathCode, planner.Plan(context.State.Project, traversal).Code);
    }

    private static BlueprintDropRequest Request(
        BlueprintId blueprintId,
        BlueprintDropTargetKind target,
        string path,
        Forge.Domain.Areas.AreaId? areaId = null,
        Vector3Value? position = null) => new(
            Guid.NewGuid(),
            BlueprintDropPlanner.CurrentSchemaVersion,
            "tests:r031",
            blueprintId,
            target,
            path,
            areaId,
            position);

    private static BlueprintDefinition CreateBlueprint(BlueprintKind kind)
    {
        var id = BlueprintId.From(Guid.Parse("b0310000-0000-4000-8000-000000000016"));
        return new BlueprintDefinition(
            id,
            "governed-item",
            "Governed item",
            kind,
            provenance: new BlueprintProvenance(
                BlueprintProvenance.CurrentSchemaVersion,
                "project://blueprints/governed-item",
                "Forge team",
                "CC0-1.0",
                new string('a', 64),
                permissionEvidence: "spdx:CC0-1.0"));
    }

    private static ProjectDefinition AddBlueprint(ProjectDefinition project, BlueprintDefinition blueprint) => new(
        project.Id,
        project.Title,
        project.Module,
        [.. project.Blueprints, blueprint],
        project.Areas,
        project.Dialogues,
        project.Quests);
}
