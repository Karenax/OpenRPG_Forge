using Forge.Application.Creator;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.Domain.Authoring;

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

    [Theory]
    [InlineData(EditorExplorerGrouping.Type)]
    [InlineData(EditorExplorerGrouping.Folder)]
    [InlineData(EditorExplorerGrouping.Area)]
    [InlineData(EditorExplorerGrouping.Tag)]
    [InlineData(EditorExplorerGrouping.Dependency)]
    [InlineData(EditorExplorerGrouping.ValidationStatus)]
    public void ExplorerSupportsEveryVirtualGroupingWithStableUniqueTargets(EditorExplorerGrouping grouping)
    {
        var root = CreatorProjectExplorer.Build(CreateProject(), grouping);
        var nodes = Flatten(root);

        Assert.Equal($"project:{ProjectId}", root.Key);
        Assert.NotEmpty(root.Children);
        Assert.Equal(nodes.Count, nodes.Select(node => node.Key).Distinct(StringComparer.Ordinal).Count());
        if (grouping == EditorExplorerGrouping.Tag)
            Assert.Contains(nodes, node => node.Key == "tag:light" && node.DocumentPath == "virtual/tags/light");
        if (grouping == EditorExplorerGrouping.Dependency)
            Assert.Contains(nodes, node => node.Key == $"dependency:blueprint:{BlueprintId}");
        if (grouping == EditorExplorerGrouping.ValidationStatus)
            Assert.Contains(nodes, node => node.Key == "validation:valid");
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
    public void SelectionCanBeFocusedFramedAndInspectedForCommonEditableProperties()
    {
        var project = CreateProject();
        var secondId = EntityId.From(Guid.Parse("40000000-0000-4000-8000-000000000002"));
        var first = project.Areas[0].Entities[0];
        var second = new EntityInstance(secondId, BlueprintId,
            new TransformValue(new Vector3Value(2, 0, 3), QuaternionValue.Identity, Vector3Value.One), ["light"]);
        project = new ProjectDefinition(project.Id, project.Title, project.Blueprints,
            [project.Areas[0].WithEntities([first, second])]);
        var selections = new[]
        {
            new CreatorSelection(CreatorSelectionKind.Entity, EntityId.ToString(), "Premier", $"areas/{AreaId}/entities/{EntityId}"),
            new CreatorSelection(CreatorSelectionKind.Entity, secondId.ToString(), "Second", $"areas/{AreaId}/entities/{secondId}"),
        };
        var context = new CreatorSelectionContext();

        Assert.True(context.Replace(selections));
        Assert.True(context.Focus(selections[1], "test-focus"));
        Assert.True(context.Frame("test-frame"));
        var inspection = CreatorSelectionInspector.Inspect(project, context.Current, "test-inspector");

        Assert.Equal(selections[1], context.Focused);
        Assert.True(context.IsFramed);
        Assert.Equal(1, inspection.SchemaVersion);
        Assert.Contains(inspection.CommonProperties, property => property.Key == "position.x" && property.Mixed && property.Editable);
        Assert.Contains(inspection.CommonProperties, property => property.Key == "scale.x" && !property.Mixed && property.Editable);
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
        Assert.Equal(CreatorDiagnosticCategory.Reference, first.Category);
        Assert.Equal("revision:7", first.CorrelationId);
        Assert.Equal(CreatorSelectionKind.Entity, first.Target!.Kind);
        Assert.Equal(EntityId.ToString(), first.Target.Id);
    }

    [Fact]
    public void DiagnosticConsoleFiltersByCategorySeverityTextAndCorrelationDeterministically()
    {
        var diagnostics = new[]
        {
            CreatorDiagnostic.Create(CreatorDiagnosticSeverity.Warning, "area.navigation.portal_missing",
                "Portail introuvable", $"areas/{AreaId}/portals/0", "operation:12"),
            CreatorDiagnostic.Create(CreatorDiagnosticSeverity.Error, "project.reference.blueprint_missing",
                "Blueprint introuvable", $"areas/{AreaId}/entities/{EntityId}", "operation:12"),
            CreatorDiagnostic.Create(CreatorDiagnosticSeverity.Information, "runtime.playtest.ready",
                "Playtest prêt", "runtime/playtest", "operation:13"),
        };

        var references = CreatorDiagnosticConsole.Filter(diagnostics, new(
            Text: "blueprint",
            Category: CreatorDiagnosticCategory.Reference,
            MinimumSeverity: CreatorDiagnosticSeverity.Warning,
            CorrelationId: "operation:12"));
        var operation = CreatorDiagnosticConsole.Filter(diagnostics, new(
            MinimumSeverity: CreatorDiagnosticSeverity.Warning,
            CorrelationId: "operation:12"));

        Assert.Equal("project.reference.blueprint_missing", Assert.Single(references).Code);
        Assert.Equal(
            ["project.reference.blueprint_missing", "area.navigation.portal_missing"],
            operation.Select(item => item.Code));
    }

    private static ProjectDefinition CreateProject(bool includeReferencedBlueprint = true)
    {
        var blueprint = new BlueprintDefinition(BlueprintId, "campfire", "Feu de camp", BlueprintKind.Placeable);
        var entity = new EntityInstance(EntityId, BlueprintId, TransformValue.Identity, ["light"]);
        var area = new AreaDefinition(AreaId, "shore", "Rivage", [entity]);
        return new ProjectDefinition(
            ProjectId,
            "Module de test",
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
