using System.Text;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
using Forge.ModuleFormat.Build;
using Forge.ModuleFormat.Packages;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Build;

public sealed class WorkspaceBuildGraphTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        "openrpg-forge-build-graph-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void GraphDerivesScriptAreaAndPackageDependenciesFromWorkspaceUsage()
    {
        var workspace = CreateFixtureWorkspace();
        var session = Assert.IsType<ProjectSession>(ProjectSession.Open(workspace).Session);

        var build = WorkspaceBuildGraphBuilder.Build(workspace, session.Project, session.ProjectFilePath);
        var script = build.Graph.Nodes.Single(node => node.Path == "scripts/ash_warden_death.forge");
        var area = build.Graph.Nodes.Single(node => node.Kind == BuildResourceKind.AreaDocument);
        var package = build.Graph.Nodes.Single(node => node.Kind == BuildResourceKind.ModulePackage);

        Assert.Contains(script.Id, area.Dependencies);
        Assert.Contains(area.Id, package.Dependencies);
        Assert.Equal(
            [script.Path, area.Path, package.Path],
            build.Graph.AffectedBy([script.Path]).Select(node => node.Path));
    }

    [Fact]
    public void PlannerReusesUnchangedResourcesAndRebuildsOnlyTransitiveImpact()
    {
        var workspace = CreateFixtureWorkspace();
        var session = Assert.IsType<ProjectSession>(ProjectSession.Open(workspace).Session);
        var first = WorkspaceBuildGraphBuilder.Build(workspace, session.Project, session.ProjectFilePath).Graph;
        WorkspaceBuildStateStore.Save(workspace, first);
        var restored = Assert.IsType<BuildDependencyGraph>(WorkspaceBuildStateStore.TryLoad(workspace));
        var unchanged = WorkspaceIncrementalBuildPlanner.Plan(restored, first);

        File.WriteAllText(
            Path.Combine(workspace, "scripts", "ash_warden_death.forge"),
            "void main() { say(\"Changed\"); }",
            new UTF8Encoding(false));
        var changedGraph = WorkspaceBuildGraphBuilder.Build(workspace, session.Project, session.ProjectFilePath).Graph;
        var changed = WorkspaceIncrementalBuildPlanner.Plan(restored, changedGraph);

        Assert.Empty(unchanged.RebuiltResources);
        Assert.Equal(first.Nodes.Count(node => node.Kind != BuildResourceKind.ExternalPackage), unchanged.ReusedResources.Count);
        Assert.Equal(
            ["scripts/ash_warden_death.forge", changedGraph.Nodes.Single(node => node.Kind == BuildResourceKind.AreaDocument).Path,
                "package/crypt-of-ashes.orpmod"],
            changed.RebuiltResources.Select(node => node.Path));
        Assert.DoesNotContain(changed.RebuiltResources, node => node.Kind == BuildResourceKind.ProjectDocument);
    }

    [Fact]
    public void InstalledPaletteUsageProducesMinimalAutomaticPackageDependency()
    {
        var project = CreateInstalledPackProject();
        var workspace = Path.Combine(_temporaryRoot, "installed-pack");
        var creation = ProjectSession.Create(workspace, project);
        var session = Assert.IsType<ProjectSession>(creation.Session);

        var build = WorkspaceBuildGraphBuilder.Build(workspace, session.Project, session.ProjectFilePath);

        Assert.Equal(["forge-core@1.2.0"], build.AutomaticDependencies);
        var external = build.Graph.Nodes.Single(node => node.Kind == BuildResourceKind.ExternalPackage);
        Assert.Equal("pack://forge-core/1.2.0", external.Path);
        Assert.Equal("CC0-1.0", external.LicenseSpdx);
        Assert.Contains(
            external.Id,
            build.Graph.Nodes.Single(node => node.Kind == BuildResourceKind.ProjectDocument).Dependencies);
    }

    [Fact]
    public void PackageExportPersistsDisposableGraphAndBecomesIncremental()
    {
        var workspace = CreateFixtureWorkspace();
        var package = Path.Combine(_temporaryRoot, "crypt.orpmod");

        var first = ModulePackager.Export(workspace, package);
        var second = ModulePackager.Export(workspace, package);

        Assert.NotEmpty(first.BuildPlan.RebuiltResources);
        Assert.Empty(second.BuildPlan.RebuiltResources);
        Assert.NotEmpty(second.BuildPlan.ReusedResources);
        Assert.True(File.Exists(Path.Combine(workspace, ".forge", "build-graph.json")));
        Assert.DoesNotContain(second.Manifest.Dependencies, dependency => string.IsNullOrWhiteSpace(dependency));
    }

    [Fact]
    public void CorruptDisposableBuildStateFallsBackToAFullPlan()
    {
        var workspace = CreateFixtureWorkspace();
        Directory.CreateDirectory(Path.Combine(workspace, ".forge"));
        File.WriteAllText(Path.Combine(workspace, ".forge", "build-graph.json"), "{\"id\":\"invalid\"}");
        var session = Assert.IsType<ProjectSession>(ProjectSession.Open(workspace).Session);
        var current = WorkspaceBuildGraphBuilder.Build(workspace, session.Project, session.ProjectFilePath).Graph;

        var restored = WorkspaceBuildStateStore.TryLoad(workspace);
        var plan = WorkspaceIncrementalBuildPlanner.Plan(restored, current);

        Assert.Null(restored);
        Assert.Equal(
            current.Nodes.Count(node => node.Kind != BuildResourceKind.ExternalPackage),
            plan.RebuiltResources.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, true);
        }
    }

    private string CreateFixtureWorkspace()
    {
        var workspace = Path.Combine(_temporaryRoot, $"workspace-{Guid.NewGuid():N}");
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "crypt-of-ashes");
        Directory.CreateDirectory(Path.Combine(workspace, "scripts"));
        Directory.CreateDirectory(Path.Combine(workspace, "areas"));
        File.Copy(Path.Combine(fixture, "forge.project.json"), Path.Combine(workspace, "forge.project.json"));
        File.Copy(Path.Combine(fixture, "project.json"), Path.Combine(workspace, "project.json"));
        foreach (var areaDocument in Directory.EnumerateFiles(Path.Combine(fixture, "areas"), "*.json"))
        {
            File.Copy(areaDocument, Path.Combine(workspace, "areas", Path.GetFileName(areaDocument)));
        }

        File.WriteAllText(
            Path.Combine(workspace, "scripts", "ash_warden_death.forge"),
            "void main() { say(\"Braise\"); }",
            new UTF8Encoding(false));
        return workspace;
    }

    private static ProjectDefinition CreateInstalledPackProject()
    {
        var blueprintId = BlueprintId.From(Guid.Parse("e1000000-0000-4000-8000-000000000001"));
        var blueprint = new BlueprintDefinition(
            blueprintId,
            "pack-floor",
            "Pack floor",
            BlueprintKind.Placeable,
            paletteOrigin: BlueprintPaletteOrigin.InstalledPack,
            provenance: new BlueprintProvenance(
                BlueprintProvenance.CurrentSchemaVersion,
                "pack://forge-core/1.2.0/floor",
                "OpenRPG Forge",
                "CC0-1.0",
                new string('b', 64)));
        var area = new AreaDefinition(
            AreaId.From(Guid.Parse("e2000000-0000-4000-8000-000000000001")),
            "entry",
            "Entry",
            [new EntityInstance(
                EntityId.From(Guid.Parse("e3000000-0000-4000-8000-000000000001")),
                blueprintId,
                TransformValue.Identity)]);
        return new ProjectDefinition(
            ProjectId.From(Guid.Parse("e4000000-0000-4000-8000-000000000001")),
            "Pack project",
            [blueprint],
            [area]);
    }
}
