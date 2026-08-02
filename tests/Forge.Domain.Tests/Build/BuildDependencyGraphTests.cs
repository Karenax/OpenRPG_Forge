using Forge.Domain.Build;

namespace Forge.Domain.Tests.Build;

public sealed class BuildDependencyGraphTests
{
    private static readonly string Hash = new('a', 64);

    [Fact]
    public void ResourceIdsAreStableAcrossPathSeparatorsAndCase()
    {
        Assert.Equal(
            BuildResourceId.ForPath("Scripts\\Boss.forge"),
            BuildResourceId.ForPath("scripts/boss.forge"));
    }

    [Fact]
    public void AffectedByReturnsOnlyTransitiveDependentsInDependencyOrder()
    {
        var script = Node("scripts/boss.forge", BuildResourceKind.Script);
        var area = Node("areas/crypt.json", BuildResourceKind.AreaDocument, script.Id);
        var unrelated = Node("assets/music.ogg", BuildResourceKind.Asset);
        var package = Node("package/module.orpmod", BuildResourceKind.ModulePackage, area.Id, unrelated.Id);
        var graph = Graph(script, area, unrelated, package);

        var affected = graph.AffectedBy([script.Path]);

        Assert.Equal([script.Path, area.Path, package.Path], affected.Select(node => node.Path));
    }

    [Fact]
    public void ConstructorRejectsMissingDependenciesAndCycles()
    {
        var missing = BuildResourceId.ForPath("missing");
        Assert.Throws<ArgumentException>(() => Graph(Node("project.json", BuildResourceKind.ProjectDocument, missing)));

        var firstId = BuildResourceId.ForPath("first");
        var secondId = BuildResourceId.ForPath("second");
        var first = new BuildDependencyNode(firstId, "first", BuildResourceKind.Asset, Hash, "CC0-1.0", "test", [secondId]);
        var second = new BuildDependencyNode(secondId, "second", BuildResourceKind.Asset, Hash, "CC0-1.0", "test", [firstId]);
        Assert.Throws<ArgumentException>(() => Graph(first, second));
    }

    [Fact]
    public void NodeRequiresCanonicalHashAndRejectsSelfDependency()
    {
        var id = BuildResourceId.ForPath("asset.glb");
        Assert.Throws<ArgumentException>(() =>
            new BuildDependencyNode(id, "asset.glb", BuildResourceKind.Asset, "unverified", "CC0-1.0", "test"));
        Assert.Throws<ArgumentException>(() =>
            new BuildDependencyNode(id, "asset.glb", BuildResourceKind.Asset, Hash, "CC0-1.0", "test", [id]));
    }

    private static BuildDependencyNode Node(
        string path,
        BuildResourceKind kind,
        params BuildResourceId[] dependencies) =>
        new(BuildResourceId.ForPath(path), path, kind, Hash, "CC0-1.0", "test", dependencies);

    private static BuildDependencyGraph Graph(params BuildDependencyNode[] nodes) =>
        new(BuildResourceId.ForPath("graph/project"), BuildDependencyGraph.CurrentSchemaVersion, "test", nodes);
}
