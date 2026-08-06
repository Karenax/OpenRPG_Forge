using Forge.Domain.Assistant;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Workspaces;

public sealed class AssistedAreaPlanStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openrpg-forge-area-plans", Guid.NewGuid().ToString("N"));

    [Fact]
    public void MissingFileUsesSafeEmptyCatalog()
    {
        var loaded = AssistedAreaPlanStore.Load(_root);

        Assert.True(loaded.Succeeded);
        Assert.True(loaded.FileMissing);
        Assert.Empty(loaded.Catalog.Plans);
    }

    [Fact]
    public void SaveAndLoadRoundTripCanonicalPlan()
    {
        var first = new AssistedAreaPlanNode(Guid.NewGuid(), 1, AssistedAreaPlanNodeKind.Entry,
            "Entrée", 0, 0, 8, 8, [], ["entry"]);
        var second = new AssistedAreaPlanNode(Guid.NewGuid(), 1, AssistedAreaPlanNodeKind.Exit,
            "Sortie", 12, 0, 8, 8, [], ["exit"]);
        var plan = new AssistedAreaPlanDefinition(
            Guid.NewGuid(), 1, Guid.NewGuid(), "Crypte assistée", AssistedAreaArchetype.Crypt, 42, 32, 32,
            [Guid.NewGuid()], ["Keep paths navigable."], ["Reach the exit."], [first, second],
            [new AssistedAreaPlanLink(Guid.NewGuid(), 1, first.Id, second.Id, "door", true)],
            ["manual:test-lore"], false, 0, "offline-ci", "test:g12-c");
        var catalog = new AssistedAreaPlanCatalog(Guid.NewGuid(), 1, [plan], "test:g12-c");

        var saved = AssistedAreaPlanStore.Save(_root, catalog);
        var loaded = AssistedAreaPlanStore.Load(_root);

        Assert.True(saved.Succeeded);
        Assert.True(loaded.Succeeded);
        var restored = Assert.Single(loaded.Catalog.Plans);
        Assert.Equal(plan.Id, restored.Id);
        Assert.Equal(plan.Nodes.Select(item => (item.Id, item.Kind, item.Label, item.X, item.Z)),
            restored.Nodes.Select(item => (item.Id, item.Kind, item.Label, item.X, item.Z)));
        Assert.Equal(plan.Links, restored.Links);
        Assert.DoesNotContain('\uFEFF', File.ReadAllText(Path.Combine(_root, AssistedAreaPlanStore.RelativePath)));
    }

    [Fact]
    public void InvalidFileReturnsStableDiagnostic()
    {
        var path = Path.Combine(_root, AssistedAreaPlanStore.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"formatVersion\":\"99.0.0\",\"catalog\":null}");

        var loaded = AssistedAreaPlanStore.Load(_root);

        Assert.False(loaded.Succeeded);
        Assert.Equal(AssistedAreaPlanStore.ReadFailedCode, loaded.Diagnostics[0].Code);
        Assert.Empty(loaded.Catalog.Plans);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
