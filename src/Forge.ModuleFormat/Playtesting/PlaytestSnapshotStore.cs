using System.Text;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Playtesting;

public sealed class PlaytestSnapshotStore
{
    public const string SnapshotFileName = "project.json";

    private readonly string _rootDirectory;

    public PlaytestSnapshotStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = Path.GetFullPath(rootDirectory);
    }

    public PlaytestSnapshot Create(ProjectDefinition project, EntityId spawnEntityId)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(spawnEntityId);

        var blueprintKinds = project.Blueprints.ToDictionary(
            blueprint => blueprint.Id,
            blueprint => blueprint.Kind);
        var spawn = project.Areas
            .SelectMany(area => area.Entities)
            .SingleOrDefault(entity => entity.Id == spawnEntityId)
            ?? throw new ArgumentException(
                "The selected playtest spawn does not exist in the project.",
                nameof(spawnEntityId));

        if (!blueprintKinds.TryGetValue(spawn.BlueprintId, out var kind) ||
            kind != BlueprintKind.PlayerSpawn)
        {
            throw new ArgumentException(
                "The selected playtest entity is not a player spawn.",
                nameof(spawnEntityId));
        }

        var serializedProject = ProjectJsonSerializer.Serialize(project);
        Directory.CreateDirectory(_rootDirectory);
        var sessionDirectory = Path.Combine(_rootDirectory, Guid.NewGuid().ToString("N"));
        var snapshotPath = Path.Combine(sessionDirectory, SnapshotFileName);
        var temporaryPath = $"{snapshotPath}.{Path.GetRandomFileName()}.tmp";

        try
        {
            Directory.CreateDirectory(sessionDirectory);
            File.WriteAllText(
                temporaryPath,
                serializedProject,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, snapshotPath);
            return new PlaytestSnapshot(sessionDirectory, snapshotPath, spawnEntityId);
        }
        catch
        {
            if (Directory.Exists(sessionDirectory))
            {
                Directory.Delete(sessionDirectory, recursive: true);
            }

            throw;
        }
    }

    public void Delete(PlaytestSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var snapshotDirectory = Path.GetFullPath(snapshot.DirectoryPath);
        var relativePath = Path.GetRelativePath(_rootDirectory, snapshotDirectory);
        if (relativePath.Equals(".", StringComparison.Ordinal) ||
            relativePath.Equals("..", StringComparison.Ordinal) ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException(
                "The playtest snapshot is outside the configured temporary root.",
                nameof(snapshot));
        }

        if (Directory.Exists(snapshotDirectory))
        {
            Directory.Delete(snapshotDirectory, recursive: true);
        }
    }
}
