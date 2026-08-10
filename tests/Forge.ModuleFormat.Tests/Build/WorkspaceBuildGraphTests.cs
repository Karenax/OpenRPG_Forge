using System.Text;
using Forge.Domain.Areas;
using Forge.Domain.Blueprints;
using Forge.Domain.Build;
using Forge.Domain.Content;
using Forge.Domain.Entities;
using Forge.Domain.Modules;
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
        Assert.True(build.Materialization.TaskCount >= 1);
        Assert.Equal(DeterministicBuildTaskScheduler.DefaultMaximumParallelism,
            build.Materialization.MaximumParallelism);
        Assert.Equal(
            [script.Path, area.Path, package.Path],
            build.Graph.AffectedBy([script.Path]).Select(node => node.Path));
    }

    [Fact]
    public void CanonicalAngelScriptSourcesAreScriptNodesAndMetadataRemainsCanonicalInput()
    {
        var workspace = CreateFixtureWorkspace();
        File.WriteAllText(Path.Combine(workspace, "scripts", "events.as"), "void main() {}",
            new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(workspace, "scripts", "events.as.meta.json"),
            "{\"schemaVersion\":1}", new UTF8Encoding(false));
        var session = Assert.IsType<ProjectSession>(ProjectSession.Open(workspace).Session);

        var build = WorkspaceBuildGraphBuilder.Build(workspace, session.Project, session.ProjectFilePath);

        Assert.Equal(BuildResourceKind.Script,
            build.Graph.Nodes.Single(node => node.Path == "scripts/events.as").Kind);
        Assert.Contains(build.Graph.Nodes, node => node.Path == "scripts/events.as.meta.json");
        Assert.Contains(build.Graph.Nodes.Single(node => node.Kind == BuildResourceKind.ModulePackage).Dependencies,
            dependency => dependency == BuildResourceId.ForPath("scripts/events.as"));
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
    public void UsedBlueprintDependenciesCreateTransitiveScriptAndAssetEdges()
    {
        var workspace = CreateFixtureWorkspace();
        Directory.CreateDirectory(Path.Combine(workspace, "assets"));
        File.WriteAllBytes(Path.Combine(workspace, "assets", "ash-pillar.glb"), [1, 2, 3, 4]);
        var session = Assert.IsType<ProjectSession>(ProjectSession.Open(workspace).Session);
        var usedBlueprintId = session.Project.Areas.SelectMany(area => area.Entities).First().BlueprintId;
        var original = session.Project.Blueprints.Single(item => item.Id == usedBlueprintId);
        var authoring = new BlueprintAuthoringMetadata(
            BlueprintAuthoringMetadata.CurrentSchemaVersion,
            original.Authoring.Version,
            original.Authoring.PackageNamespace,
            original.Authoring.ManifestPath,
            original.Authoring.ExposedParameters,
            ["assets/ash-pillar.glb", "scripts/ash_warden_death.forge"],
            original.Authoring.PreviewPath,
            original.Authoring.PlatformByteBudget);
        var blueprint = new BlueprintDefinition(
            original.Id, original.Slug, original.DisplayName, original.Kind, original.Combat,
            original.PaletteOrigin, original.IsFavorite, original.Provenance,
            original.CreaturePresentation, original.CreatureProgression, original.CreatureLoot,
            original.CreatureRoleBehavior, original.CreatureEvents, original.ItemAdvanced, authoring);
        var project = new ProjectDefinition(
            session.Project.Id, session.Project.Title, session.Project.Module,
            session.Project.Blueprints.Select(item => item.Id == blueprint.Id ? blueprint : item).ToArray(), session.Project.Areas,
            session.Project.Dialogues, session.Project.Quests);

        var build = WorkspaceBuildGraphBuilder.Build(workspace, project, session.ProjectFilePath);
        var area = build.Graph.Nodes.Single(node => node.Kind == BuildResourceKind.AreaDocument);

        Assert.Contains(BuildResourceId.ForPath("scripts/ash_warden_death.forge"), area.Dependencies);
        Assert.Contains(BuildResourceId.ForPath("assets/ash-pillar.glb"), area.Dependencies);
        Assert.Equal(
            ["assets/ash-pillar.glb", area.Path, $"package/{project.Module.Slug}.orpmod", "project.json"],
            build.Graph.AffectedBy(["assets/ash-pillar.glb"]).Select(node => node.Path));
    }

    [Fact]
    public void RequiredContentPackagesAreClosedTransitivelyWithoutUnusedPackages()
    {
        var project = WithPackageCatalog(CreateInstalledPackProject());
        var workspace = Path.Combine(_temporaryRoot, "transitive-installed-pack");
        var creation = ProjectSession.Create(workspace, project);
        var session = Assert.IsType<ProjectSession>(creation.Session);

        var build = WorkspaceBuildGraphBuilder.Build(workspace, session.Project, session.ProjectFilePath);

        Assert.Equal(["forge-core@1.2.0", "forge.effects@2.0.0"], build.AutomaticDependencies);
        Assert.DoesNotContain(build.AutomaticDependencies, item => item.StartsWith("forge.unused@", StringComparison.Ordinal));
        Assert.Equal(2, build.Graph.Nodes.Count(node => node.Kind == BuildResourceKind.ExternalPackage));
    }

    [Fact]
    public void PackageExportPersistsDisposableGraphAndBecomesIncremental()
    {
        var workspace = CreateFixtureWorkspace();
        var package = Path.Combine(_temporaryRoot, "crypt.orpmod");

        var first = ModulePackager.Export(workspace, package);
        var firstBytes = File.ReadAllBytes(package);
        var firstWrite = File.GetLastWriteTimeUtc(package);
        Thread.Sleep(20);
        var second = ModulePackager.Export(workspace, package);

        Assert.NotEmpty(first.BuildPlan.RebuiltResources);
        Assert.True(first.PackagingMaterialization.TaskCount >= 1);
        Assert.Equal(DeterministicBuildTaskScheduler.DefaultMaximumParallelism,
            first.PackagingMaterialization.MaximumParallelism);
        Assert.Empty(second.BuildPlan.RebuiltResources);
        Assert.NotEmpty(second.BuildPlan.ReusedResources);
        Assert.Equal(firstBytes, File.ReadAllBytes(package));
        Assert.Equal(firstWrite, File.GetLastWriteTimeUtc(package));
        Assert.True(File.Exists(Path.Combine(workspace, ".forge", "build-graph.json")));
        Assert.DoesNotContain(second.Manifest.Dependencies, dependency => string.IsNullOrWhiteSpace(dependency));
    }

    [Fact]
    public void TwoCleanBuildsAreByteIdenticalWithNormalizedArchiveMetadata()
    {
        var workspace = CreateFixtureWorkspace();
        var firstPath = Path.Combine(_temporaryRoot, "first.orpmod");
        var secondPath = Path.Combine(_temporaryRoot, "second.orpmod");

        ModulePackager.Export(workspace, firstPath);
        Directory.Delete(Path.Combine(workspace, ".forge"), true);
        ModulePackager.Export(workspace, secondPath);

        Assert.Equal(File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath));
        Assert.Equal(
            ModulePackageValidator.Validate(firstPath).Manifest?.ContentSha256,
            ModulePackageValidator.Validate(secondPath).Manifest?.ContentSha256);
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

    private static ProjectDefinition WithPackageCatalog(ProjectDefinition project)
    {
        var module = project.Module;
        ContentPackageDefinition Package(
            string packageNamespace,
            string version,
            char hash,
            IReadOnlyList<ContentPackageDependency>? dependencies = null) => new(
                Guid.Parse(packageNamespace switch
                {
                    "forge-core" => "b0310000-0000-4000-8000-000000000101",
                    "forge.effects" => "b0310000-0000-4000-8000-000000000102",
                    _ => "b0310000-0000-4000-8000-000000000103",
                }),
                ContentPackageDefinition.CurrentSchemaVersion,
                "tests:r031-package-catalog",
                packageNamespace,
                version,
                "CC0-1.0",
                new string(hash, 64),
                0,
                [],
                dependencies,
                []);
        var effectsDependency = new ContentPackageDependency(
            Guid.Parse("b0310000-0000-4000-8000-000000000104"),
            ContentPackageDefinition.CurrentSchemaVersion,
            "tests:r031-package-catalog",
            "forge.effects",
            "^2.0.0",
            ContentDependencyKind.Required);
        var catalog = new ContentPackageCatalog(
            module.Id.Value,
            ContentPackageCatalog.CurrentSchemaVersion,
            "tests:r031-package-catalog",
            ContentConflictPolicy.HighestPriority,
            [
                Package("forge-core", "1.2.0", 'b', [effectsDependency]),
                Package("forge.effects", "2.0.0", 'c'),
                Package("forge.unused", "9.0.0", 'd'),
            ]);
        var content = new ContentWorkspaceConfiguration(
            module.Content.Id,
            module.Content.Batch,
            module.Content.DataTables,
            module.Content.MergePolicy,
            module.Content.Provenance,
            module.Content.SchemaVersion,
            catalog);
        var configuredModule = new ModuleDefinition(
            module.Id, module.Slug, module.DisplayName, module.Description, module.EntryAreaId,
            module.Metadata, module.Variables, module.Distribution, module.Localization, content,
            module.ArtisticStyle, module.RuntimePolicies, module.Factions, module.WorldFeatures);
        return new ProjectDefinition(
            project.Id, project.Title, configuredModule, project.Blueprints, project.Areas,
            project.Dialogues, project.Quests);
    }
}
