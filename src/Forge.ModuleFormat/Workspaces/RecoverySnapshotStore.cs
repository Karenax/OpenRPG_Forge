using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Workspaces;

public sealed record RecoverySnapshotInfo(
    string Id,
    DateTimeOffset CreatedAt,
    long Revision,
    string ProjectSha256,
    string SnapshotPath);

public sealed class RecoverySnapshotStore
{
    public const int CurrentSchemaVersion = 1;
    public const string RelativeDirectory = ".forge/recovery";
    private const string JournalFileName = "journal.json";
    private readonly int _retention;

    public RecoverySnapshotStore(int retention = 12)
    {
        if (retention is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(retention));
        _retention = retention;
    }

    public RecoverySnapshotInfo Capture(
        string workspaceRoot,
        ProjectDefinition project,
        long revision,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(project);
        var directory = ResolveDirectory(workspaceRoot);
        Directory.CreateDirectory(directory);
        var json = ProjectJsonSerializer.Serialize(project);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        var id = $"{createdAt.UtcDateTime:yyyyMMddTHHmmssfffZ}-r{revision:D8}-{hash[..8]}";
        var path = Path.Combine(directory, $"{id}.json");
        WriteAtomic(path, json);
        var info = new RecoverySnapshotInfo(id, createdAt.ToUniversalTime(), revision, hash, path);
        var journal = List(workspaceRoot).Prepend(info).DistinctBy(item => item.Id)
            .OrderByDescending(item => item.CreatedAt).Take(_retention).ToArray();
        WriteJournal(directory, journal);
        DeleteUnretained(directory, journal);
        return info;
    }

    public IReadOnlyList<RecoverySnapshotInfo> List(string workspaceRoot)
    {
        var directory = ResolveDirectory(workspaceRoot);
        var journalPath = Path.Combine(directory, JournalFileName);
        if (!File.Exists(journalPath)) return [];
        var journal = JsonSerializer.Deserialize<RecoveryJournal>(File.ReadAllText(journalPath), Options)
            ?? throw new JsonException("Recovery journal is empty.");
        if (journal.SchemaVersion != CurrentSchemaVersion) throw new NotSupportedException("Recovery journal schema is unsupported.");
        return journal.Snapshots.Where(item => IsContained(directory, item.SnapshotPath) && File.Exists(item.SnapshotPath))
            .OrderByDescending(item => item.CreatedAt).ToArray();
    }

    public ProjectDefinition Restore(RecoverySnapshotInfo snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var json = File.ReadAllText(snapshot.SnapshotPath);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        if (!string.Equals(hash, snapshot.ProjectSha256, StringComparison.Ordinal))
            throw new InvalidDataException("Recovery snapshot hash does not match its journal entry.");
        return ProjectJsonSerializer.Deserialize(json);
    }

    public void Clear(string workspaceRoot)
    {
        var directory = ResolveDirectory(workspaceRoot);
        if (!Directory.Exists(directory)) return;
        foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)) File.Delete(file);
    }

    private static string ResolveDirectory(string root) => Path.Combine(Path.GetFullPath(root), RelativeDirectory);
    private static bool IsContained(string directory, string path) => Path.GetFullPath(path).StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static void WriteJournal(string directory, IReadOnlyList<RecoverySnapshotInfo> snapshots) =>
        WriteAtomic(Path.Combine(directory, JournalFileName), JsonSerializer.Serialize(new RecoveryJournal(CurrentSchemaVersion, snapshots), Options));
    private static void WriteAtomic(string path, string content) { var temp = $"{path}.{Guid.NewGuid():N}.tmp"; File.WriteAllText(temp, content, new UTF8Encoding(false)); File.Move(temp, path, true); }
    private static void DeleteUnretained(string directory, IReadOnlyList<RecoverySnapshotInfo> retained)
    {
        var paths = retained.Select(item => Path.GetFullPath(item.SnapshotPath)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Where(path => !path.EndsWith(JournalFileName, StringComparison.OrdinalIgnoreCase) && !paths.Contains(Path.GetFullPath(path)))) File.Delete(path);
    }

    private sealed record RecoveryJournal(int SchemaVersion, IReadOnlyList<RecoverySnapshotInfo> Snapshots);
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
}
