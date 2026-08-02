using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.ModuleFormat.Playtesting;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Tests.Playtesting;

public sealed class PlaytestSnapshotStoreTests : IDisposable
{
    private readonly string _temporaryRoot = Path.Combine(
        Path.GetTempPath(),
        "openrpg-forge-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Create_WritesACanonicalSnapshotForTheSelectedSpawn()
    {
        var project = LoadSampleProject();
        var spawnBlueprintIds = project.Blueprints
            .Where(blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn)
            .Select(blueprint => blueprint.Id)
            .ToHashSet();
        var spawn = project.Areas
            .SelectMany(area => area.Entities)
            .Single(entity => spawnBlueprintIds.Contains(entity.BlueprintId));
        var store = new PlaytestSnapshotStore(_temporaryRoot);

        var snapshot = store.Create(project, spawn.Id);

        Assert.Equal(spawn.Id, snapshot.SpawnEntityId);
        Assert.Equal(PlaytestSnapshotStore.SnapshotFileName, Path.GetFileName(snapshot.ProjectFilePath));
        Assert.True(File.Exists(snapshot.ProjectFilePath));
        var restored = ProjectJsonSerializer.Deserialize(File.ReadAllText(snapshot.ProjectFilePath));
        Assert.Equal(ProjectJsonSerializer.Serialize(project), ProjectJsonSerializer.Serialize(restored));
        Assert.Empty(Directory.EnumerateFiles(snapshot.DirectoryPath, "*.tmp"));
    }

    [Fact]
    public void Create_RejectsAnEntityThatIsNotAPlayerSpawn()
    {
        var project = LoadSampleProject();
        var nonSpawnBlueprintIds = project.Blueprints
            .Where(blueprint => blueprint.Kind != BlueprintKind.PlayerSpawn)
            .Select(blueprint => blueprint.Id)
            .ToHashSet();
        var entity = project.Areas
            .SelectMany(area => area.Entities)
            .First(candidate => nonSpawnBlueprintIds.Contains(candidate.BlueprintId));
        var store = new PlaytestSnapshotStore(_temporaryRoot);

        var exception = Assert.Throws<ArgumentException>(() => store.Create(project, entity.Id));

        Assert.Contains("not a player spawn", exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(_temporaryRoot));
    }

    [Fact]
    public void Delete_RemovesTheCreatedSnapshotDirectory()
    {
        var project = LoadSampleProject();
        var spawnBlueprintIds = project.Blueprints
            .Where(blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn)
            .Select(blueprint => blueprint.Id)
            .ToHashSet();
        var spawn = project.Areas
            .SelectMany(area => area.Entities)
            .Single(entity => spawnBlueprintIds.Contains(entity.BlueprintId));
        var store = new PlaytestSnapshotStore(_temporaryRoot);
        var snapshot = store.Create(project, spawn.Id);

        store.Delete(snapshot);

        Assert.False(Directory.Exists(snapshot.DirectoryPath));
    }

    [Fact]
    public void Delete_RejectsTheConfiguredRootDirectory()
    {
        Directory.CreateDirectory(_temporaryRoot);
        var store = new PlaytestSnapshotStore(_temporaryRoot);
        var unsafeSnapshot = new PlaytestSnapshot(
            _temporaryRoot,
            Path.Combine(_temporaryRoot, PlaytestSnapshotStore.SnapshotFileName),
            EntityId.From(Guid.NewGuid()));

        Assert.Throws<ArgumentException>(() => store.Delete(unsafeSnapshot));
        Assert.True(Directory.Exists(_temporaryRoot));
    }

    public void Dispose()
    {
        if (Directory.Exists(_temporaryRoot))
        {
            Directory.Delete(_temporaryRoot, recursive: true);
        }
    }

    private static Forge.Domain.Projects.ProjectDefinition LoadSampleProject()
    {
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "crypt-of-ashes");
        return ProjectSession.Open(fixtureDirectory).Session?.Project
            ?? throw new InvalidOperationException("The crypt-of-ashes fixture did not open.");
    }
}
