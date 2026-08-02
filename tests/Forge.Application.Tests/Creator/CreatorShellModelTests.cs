using Forge.Application.Creator;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;

namespace Forge.Application.Tests.Creator;

public sealed class CreatorShellModelTests
{
    private static readonly ProjectId ProjectId = ProjectId.From(Guid.Parse("10000000-0000-4000-8000-000000000001"));
    private static readonly AreaId AreaId = AreaId.From(Guid.Parse("20000000-0000-4000-8000-000000000001"));
    private static readonly BlueprintId BlueprintId = BlueprintId.From(Guid.Parse("30000000-0000-4000-8000-000000000001"));
    private static readonly EntityId EntityId = EntityId.From(Guid.Parse("40000000-0000-4000-8000-000000000001"));

    [Fact]
    public void ExplorerBuildsStableTypedHierarchy()
    {
        var root = CreatorProjectExplorer.Build(CreateProject());

        Assert.Equal($"project:{ProjectId}", root.Key);
        Assert.Equal(CreatorExplorerNodeKind.Project, root.Kind);
        Assert.Equal(["folder:areas", "folder:blueprints", "folder:dialogues", "folder:quests"], root.Children.Select(node => node.Key));
        var entity = Flatten(root).Single(node => node.Key == $"entity:{EntityId}");
        Assert.Equal(CreatorExplorerNodeKind.Entity, entity.Kind);
        Assert.Equal($"areas/{AreaId}/entities/{EntityId}", entity.DocumentPath);
        Assert.Equal(CreatorSelectionKind.Entity, entity.ToSelection()!.Kind);
        Assert.Equal(Flatten(root).Count, Flatten(root).Select(node => node.Key).Distinct().Count());
    }

    [Fact]
    public void SelectionContextSharesMultipleSelectionsAndHonorsLock()
    {
        var context = new CreatorSelectionContext();
        var project = new CreatorSelection(CreatorSelectionKind.Project, ProjectId.ToString(), "Projet", "project.json");
        var entity = new CreatorSelection(CreatorSelectionKind.Entity, EntityId.ToString(), "Entité", $"areas/{AreaId}/entities/{EntityId}");
        var changeCount = 0;
        context.Changed += () => changeCount++;

        Assert.True(context.Replace([project, entity, entity]));
        Assert.Equal(2, context.Current.Count);
        context.SetLocked(true);
        Assert.False(context.Replace(project));
        Assert.Equal(2, context.Current.Count);
        context.SetLocked(false);
        Assert.True(context.Replace(entity));
        Assert.Equal(4, changeCount);
    }

    [Fact]
    public void ProjectDiagnosticsHaveStableIdsAndClickableEntityTargets()
    {
        var project = CreateProject(includeReferencedBlueprint: false);

        var first = Assert.Single(CreatorDiagnostics.FromProject(project, "revision:7"));
        var second = Assert.Single(CreatorDiagnostics.FromProject(project, "revision:7"));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("project.reference.blueprint_missing", first.Code);
        Assert.Equal(CreatorDiagnosticSeverity.Error, first.Severity);
        Assert.Equal("revision:7", first.CorrelationId);
        Assert.Equal(CreatorSelectionKind.Entity, first.Target!.Kind);
        Assert.Equal(EntityId.ToString(), first.Target.Id);
    }

    private static ProjectDefinition CreateProject(bool includeReferencedBlueprint = true)
    {
        var blueprint = new BlueprintDefinition(BlueprintId, "campfire", "Feu de camp", BlueprintKind.Placeable);
        var entity = new EntityInstance(EntityId, BlueprintId, TransformValue.Identity, ["light"]);
        var area = new AreaDefinition(AreaId, "shore", "Rivage", [entity]);
        return new ProjectDefinition(
            ProjectId,
            "Les Îles Brisées",
            includeReferencedBlueprint ? [blueprint] : [],
            [area]);
    }

    private static IReadOnlyList<CreatorExplorerNode> Flatten(CreatorExplorerNode root)
    {
        var result = new List<CreatorExplorerNode>();
        var pending = new Stack<CreatorExplorerNode>();
        pending.Push(root);
        while (pending.TryPop(out var node))
        {
            result.Add(node);
            foreach (var child in node.Children.Reverse())
            {
                pending.Push(child);
            }
        }

        return result;
    }
}
