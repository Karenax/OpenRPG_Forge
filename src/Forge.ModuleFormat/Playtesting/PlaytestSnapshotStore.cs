using System.Text;
using Forge.Domain.Blueprints;
using Forge.Domain.Entities;
using Forge.Domain.Areas;
using Forge.Domain.Navigation;
using Forge.Domain.Projects;
using Forge.Domain.Spatial;
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

    public PlaytestSnapshot Create(
        ProjectDefinition project,
        EntityId spawnEntityId,
        string? sourceDirectory = null)
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

        return Write(project, spawnEntityId, null, sourceDirectory);
    }

    public PlaytestSnapshot Create(
        ProjectDefinition project,
        AreaId areaId,
        AreaPlaytestProfile profile,
        string? sourceDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(areaId);
        ArgumentNullException.ThrowIfNull(profile);
        var area = project.Areas.SingleOrDefault(item => item.Id == areaId)
            ?? throw new ArgumentException("The selected playtest area does not exist.", nameof(areaId));
        var diagnostics = AreaPlaytestProfileValidator.Validate(project, area, profile);
        if (diagnostics.Count > 0)
            throw new ArgumentException($"Play From Here is invalid: {diagnostics[0].Code} {diagnostics[0].Path}",
                nameof(profile));

        var transform = profile.StartMode switch
        {
            PlayFromHereStartMode.PlayerSpawn => area.Entities.Single(item => item.Id == profile.SpawnEntityId).Transform,
            PlayFromHereStartMode.Camera => profile.CameraTransform,
            PlayFromHereStartMode.Waypoint => area.Navigation.Anchors.Instances
                .Single(item => item.Id == profile.WaypointInstanceId).Transform,
            _ => throw new ArgumentOutOfRangeException(nameof(profile)),
        };
        var spawnId = profile.StartMode == PlayFromHereStartMode.PlayerSpawn
            ? profile.SpawnEntityId!
            : EntityId.From(Stable(profile.Id, $"spawn:{profile.StartMode}"));
        var snapshotProject = project;
        if (profile.StartMode != PlayFromHereStartMode.PlayerSpawn)
        {
            var spawn = new EntityInstance(spawnId, profile.PlayerBlueprintId!, transform,
                ["playtest", "play-from-here", profile.StartMode.ToString().ToLowerInvariant()]);
            var snapshotArea = area.WithEntities(area.Entities.Append(spawn).ToArray());
            snapshotProject = new ProjectDefinition(project.Id, project.Title, project.Module, project.Blueprints,
                project.Areas.Select(item => item.Id == area.Id ? snapshotArea : item).ToArray(),
                project.Dialogues, project.Quests);
        }

        var manifest = new PlaytestLaunchManifest(profile.Id, PlaytestLaunchManifest.CurrentSchemaVersion,
            profile.Provenance, area.Id.ToString(), spawnId.ToString(), profile.StartMode.ToString(),
            profile.TestProfile, profile.QuestInjections.Select(item => new PlaytestQuestLaunchState(
                item.QuestId.ToString(), item.StateId.ToString(), item.Variables)).ToArray(),
            profile.TargetKind.ToString(), profile.TargetId?.ToString(), profile.Headless);
        return Write(snapshotProject, spawnId, manifest, sourceDirectory);
    }

    private PlaytestSnapshot Write(ProjectDefinition project, EntityId spawnEntityId,
        PlaytestLaunchManifest? manifest, string? sourceDirectory)
    {
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
            string? manifestPath = null;
            if (manifest is not null)
            {
                manifestPath = Path.Combine(sessionDirectory, PlaytestLaunchManifestStore.FileName);
                var temporaryManifest = $"{manifestPath}.{Path.GetRandomFileName()}.tmp";
                File.WriteAllText(temporaryManifest, PlaytestLaunchManifestStore.Serialize(manifest),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(temporaryManifest, manifestPath);
            }
            CopySupplementalDirectories(sourceDirectory, sessionDirectory);
            return new PlaytestSnapshot(sessionDirectory, snapshotPath, spawnEntityId, manifestPath);
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

    private static void CopySupplementalDirectories(string? sourceDirectory, string sessionDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory)) return;
        var sourceRoot = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(sourceRoot))
            throw new DirectoryNotFoundException($"The playtest source directory does not exist: '{sourceRoot}'.");

        foreach (var name in new[] { "assets", "scripts" })
        {
            var source = Path.Combine(sourceRoot, name);
            if (!Directory.Exists(source)) continue;
            var destination = Path.Combine(sessionDirectory, name);
            foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(destination, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: false);
            }
        }
    }

    private static Guid Stable(Guid source, string discriminator)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes($"openrpg-forge:{source:D}:{discriminator}"));
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes[..16]);
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
