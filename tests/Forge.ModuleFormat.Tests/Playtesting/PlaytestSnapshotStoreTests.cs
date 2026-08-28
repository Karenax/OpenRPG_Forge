using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Quests;
using Forge.Domain.Spatial;
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
    public void Create_CopiesGovernedRuntimeResourcesFromTheSourceWorkspace()
    {
        var project = LoadSampleProject();
        var spawnBlueprintIds = project.Blueprints
            .Where(blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn)
            .Select(blueprint => blueprint.Id)
            .ToHashSet();
        var spawn = project.Areas.SelectMany(area => area.Entities)
            .Single(entity => spawnBlueprintIds.Contains(entity.BlueprintId));
        var source = Path.Combine(_temporaryRoot, "source");
        Directory.CreateDirectory(Path.Combine(source, "assets", "models", "hero"));
        Directory.CreateDirectory(Path.Combine(source, "scripts"));
        Directory.CreateDirectory(Path.Combine(source, "art-kits"));
        Directory.CreateDirectory(Path.Combine(source, ".forge"));
        Directory.CreateDirectory(Path.Combine(source, "artifacts"));
        File.WriteAllText(Path.Combine(source, "assets", "models", "hero", "manifest.json"), "{}");
        File.WriteAllText(Path.Combine(source, "scripts", "startup.forge"), "return true");
        File.WriteAllText(Path.Combine(source, "art-kits", "dungeon.layout.json"), "{}");
        File.WriteAllText(Path.Combine(source, ".forge", "project-session.json"), "{}");
        File.WriteAllText(Path.Combine(source, "artifacts", "build-report.json"), "{}");
        File.WriteAllText(Path.Combine(source, "README.md"), "# Playtest module");

        var snapshot = new PlaytestSnapshotStore(Path.Combine(_temporaryRoot, "snapshots"))
            .Create(project, spawn.Id, source);

        Assert.True(File.Exists(Path.Combine(snapshot.DirectoryPath, "assets", "models", "hero", "manifest.json")));
        Assert.True(File.Exists(Path.Combine(snapshot.DirectoryPath, "scripts", "startup.forge")));
        Assert.True(File.Exists(Path.Combine(snapshot.DirectoryPath, "art-kits", "dungeon.layout.json")));
        Assert.True(File.Exists(Path.Combine(snapshot.DirectoryPath, "README.md")));
        Assert.False(Directory.Exists(Path.Combine(snapshot.DirectoryPath, ".forge")));
        Assert.False(Directory.Exists(Path.Combine(snapshot.DirectoryPath, "artifacts")));
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
    public void Create_FromCameraWritesAManifestAndDoesNotMutateTheSourceProject()
    {
        var project = LoadSampleProject();
        var area = project.Areas.Single();
        var player = project.Blueprints.Single(blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn);
        var quest = project.Quests.Single();
        var state = quest.States[^1];
        var before = ProjectJsonSerializer.Serialize(project);
        var defaults = AreaPlaytestProfile.CreateDefault(area.Id);
        var profile = new AreaPlaytestProfile(defaults.Id, 1, "author:r029-test", true, false,
            PlayFromHereStartMode.Camera, null,
            new TransformValue(new Vector3Value(7, 2, -4), QuaternionValue.Identity, Vector3Value.One),
            null, player.Id, "combat-debug",
            [new PlaytestQuestInjection(Guid.Parse("a0293000-0000-4000-8000-000000000001"), quest.Id,
                state.Id, new Dictionary<string, string> { ["boss_phase"] = "2" })]);
        var store = new PlaytestSnapshotStore(_temporaryRoot);

        var snapshot = store.Create(project, area.Id, profile);
        var restored = ProjectJsonSerializer.Deserialize(File.ReadAllText(snapshot.ProjectFilePath));
        var manifest = PlaytestLaunchManifestStore.Load(snapshot.LaunchManifestFilePath!);

        Assert.Equal(before, ProjectJsonSerializer.Serialize(project));
        var synthetic = restored.Areas.Single().Entities.Single(entity => entity.Id == snapshot.SpawnEntityId);
        Assert.Equal(profile.CameraTransform, synthetic.Transform);
        Assert.Equal("Camera", manifest.StartMode);
        Assert.Equal("combat-debug", manifest.TestProfile);
        Assert.Equal("2", Assert.Single(manifest.QuestStates).Variables["boss_phase"]);
        Assert.Empty(Directory.EnumerateFiles(snapshot.DirectoryPath, "*.tmp"));
    }

    [Fact]
    public void Create_TargetedQuestHeadlessPersistsExplicitLaunchContract()
    {
        var project = LoadSampleProject();
        var area = project.Areas.Single();
        var player = project.Blueprints.Single(blueprint => blueprint.Kind == BlueprintKind.PlayerSpawn);
        var quest = project.Quests.Single();
        var state = quest.States[^1];
        var defaults = AreaPlaytestProfile.CreateDefault(area.Id);
        var profile = new AreaPlaytestProfile(defaults.Id, 1, "test:r034-target", true, false,
            PlayFromHereStartMode.Camera, null, TransformValue.Identity, null, player.Id, "quest-focus",
            [new PlaytestQuestInjection(Guid.NewGuid(), quest.Id, state.Id, new Dictionary<string, string>())],
            PlaytestTargetKind.SelectedQuest, quest.Id.Value, headless: true);

        var snapshot = new PlaytestSnapshotStore(_temporaryRoot).Create(project, area.Id, profile);
        var manifest = PlaytestLaunchManifestStore.Load(snapshot.LaunchManifestFilePath!);

        Assert.Equal("SelectedQuest", manifest.TargetKind);
        Assert.Equal(quest.Id.ToString(), manifest.TargetId);
        Assert.True(manifest.Headless);
        Assert.Equal(state.Id.ToString(), Assert.Single(manifest.QuestStates).StateId);
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
