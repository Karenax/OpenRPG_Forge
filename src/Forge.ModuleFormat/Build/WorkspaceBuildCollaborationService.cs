using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Forge.Domain.Build;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Build;

public sealed record BuildCollaborationDiagnostic(string Code, string Message, string Path);

public sealed record DisposableCacheEntry(string RelativePath, long Bytes, string Sha256);

public sealed record DisposableCacheReport(
    bool Succeeded,
    bool DryRun,
    string CacheDirectory,
    string IndexPath,
    IReadOnlyList<DisposableCacheEntry> Entries,
    IReadOnlyList<BuildCollaborationDiagnostic> Diagnostics);

public sealed record NormalizedDocumentChange(string RelativePath, string BeforeSha256, string AfterSha256);

public sealed record DocumentNormalizationReport(
    bool Succeeded,
    bool DryRun,
    IReadOnlyList<NormalizedDocumentChange> Changes,
    IReadOnlyList<BuildCollaborationDiagnostic> Diagnostics);

public sealed record WorkspaceMigrationReport(
    bool Succeeded,
    bool DryRun,
    string FromVersion,
    string ToVersion,
    bool Applied,
    bool Idempotent,
    bool DowngradeSupported,
    string? BackupDirectory,
    IReadOnlyList<string> ChangedDocuments,
    IReadOnlyList<BuildCollaborationDiagnostic> Diagnostics);

public sealed record WorkspaceReferenceNode(
    string Id,
    string Path,
    BuildResourceKind Kind,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Dependents);

public sealed record WorkspaceReferenceIndex(
    string GraphId,
    IReadOnlyList<WorkspaceReferenceNode> Nodes,
    IReadOnlyList<BuildCollaborationDiagnostic> Diagnostics);

public enum WorkspaceSemanticChangeKind
{
    Added,
    Removed,
    Modified,
}

public sealed record WorkspaceSemanticResource(string Id, string Domain, string DisplayName, string Sha256);

public sealed record WorkspaceSemanticChange(
    WorkspaceSemanticChangeKind Kind,
    string Id,
    string Domain,
    string DisplayName,
    string? BeforeSha256,
    string? AfterSha256);

public sealed record WorkspaceSemanticDiffReport(
    bool Succeeded,
    string Text,
    IReadOnlyList<WorkspaceSemanticChange> Changes,
    IReadOnlyList<BuildCollaborationDiagnostic> Diagnostics);

public static class WorkspaceBuildCollaborationService
{
    public const string SemanticSnapshotRelativePath = ".forge/semantic-snapshot.json";
    public const string CacheUnsafeCode = "build.cache.path_unsafe";
    public const string CacheIoFailedCode = "build.cache.io_failed";
    public const string NormalizationInvalidJsonCode = "build.normalize.json_invalid";
    public const string NormalizationTooLargeCode = "build.normalize.document_too_large";
    public const string NormalizationIoFailedCode = "build.normalize.io_failed";
    public const string MigrationUnsupportedCode = "build.migration.unsupported";
    public const string MigrationIoFailedCode = "build.migration.io_failed";
    public const string ReferencesLimitCode = "build.references.limit_exceeded";

    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static DisposableCacheReport RebuildCacheIndex(
        string workspaceRoot,
        WorkspaceBuildProfile profile,
        bool dryRun)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(profile);
        var diagnostics = WorkspaceBuildProfileValidator.Validate(profile)
            .Select(item => new BuildCollaborationDiagnostic(item.Code, item.Message, item.Path)).ToList();
        if (!TryResolveContainedDirectory(workspaceRoot, profile.CachePath, out var cache, out var error))
        {
            diagnostics.Add(new(CacheUnsafeCode, error, profile.CachePath));
            return new(false, dryRun, string.Empty, string.Empty, [], diagnostics);
        }
        var index = Path.Combine(cache, profile.Collaboration.Cache.IndexFileName);
        try
        {
            if (Directory.Exists(cache) && new DirectoryInfo(cache).Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Cache directory cannot be a reparse point.");
            var entries = Directory.Exists(cache)
                ? Directory.EnumerateFiles(cache, "*", SearchOption.AllDirectories)
                    .Where(path => !string.Equals(Path.GetFullPath(path), index, StringComparison.OrdinalIgnoreCase))
                    .Select(path => Entry(cache, path))
                    .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
                    .Take(profile.Collaboration.Cache.MaximumEntries + 1)
                    .ToArray()
                : [];
            if (entries.Length > profile.Collaboration.Cache.MaximumEntries)
            {
                diagnostics.Add(new(CacheIoFailedCode, "Cache entry limit exceeded.", profile.CachePath));
                return new(false, dryRun, cache, index, entries, diagnostics);
            }
            if (!dryRun)
            {
                Directory.CreateDirectory(cache);
                WriteAtomically(index, JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    provenance = "openrpg-forge:r023-cache-index",
                    entries,
                }, IndentedJson) + "\n");
            }
            return new(diagnostics.Count == 0, dryRun, cache, index, entries, diagnostics);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            diagnostics.Add(new(CacheIoFailedCode, exception.Message, profile.CachePath));
            return new(false, dryRun, cache, index, [], diagnostics);
        }
    }

    public static DisposableCacheReport InvalidateCache(
        string workspaceRoot,
        WorkspaceBuildProfile profile,
        bool dryRun)
    {
        var report = RebuildCacheIndex(workspaceRoot, profile, dryRun: true);
        if (!report.Succeeded || dryRun || !Directory.Exists(report.CacheDirectory))
            return report with { DryRun = dryRun };
        try
        {
            foreach (var path in Directory.EnumerateFiles(report.CacheDirectory, "*", SearchOption.AllDirectories)
                         .OrderByDescending(path => path.Length))
                File.Delete(path);
            foreach (var directory in Directory.EnumerateDirectories(report.CacheDirectory, "*", SearchOption.AllDirectories)
                         .OrderByDescending(path => path.Length))
                Directory.Delete(directory, recursive: false);
            return report with { DryRun = false };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return report with
            {
                Succeeded = false,
                DryRun = false,
                Diagnostics = [.. report.Diagnostics,
                    new BuildCollaborationDiagnostic(CacheIoFailedCode, exception.Message, profile.CachePath)],
            };
        }
    }

    public static DocumentNormalizationReport NormalizeDocuments(
        string workspaceRoot,
        WorkspaceBuildProfile profile,
        bool dryRun)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(profile);
        var root = Path.GetFullPath(workspaceRoot);
        var diagnostics = new List<BuildCollaborationDiagnostic>();
        var changes = new List<(NormalizedDocumentChange Change, string Path, string Content)>();
        foreach (var path in EnumerateCanonicalJson(root).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            try
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length > profile.Collaboration.Normalization.MaximumDocumentBytes)
                {
                    diagnostics.Add(new(NormalizationTooLargeCode,
                        "JSON document exceeds the configured normalization limit.", relative));
                    continue;
                }
                var after = NormalizeJson(bytes);
                var beforeHash = Sha256(bytes);
                var afterBytes = Encoding.UTF8.GetBytes(after);
                var afterHash = Sha256(afterBytes);
                if (!string.Equals(beforeHash, afterHash, StringComparison.Ordinal))
                    changes.Add((new(relative, beforeHash, afterHash), path, after));
            }
            catch (JsonException exception)
            {
                diagnostics.Add(new(NormalizationInvalidJsonCode, exception.Message, relative));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new(NormalizationIoFailedCode, exception.Message, relative));
            }
        }
        if (!dryRun && diagnostics.Count == 0)
        {
            var originals = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var written = new List<string>();
            try
            {
                foreach (var change in changes) originals[change.Path] = File.ReadAllBytes(change.Path);
                foreach (var change in changes)
                {
                    WriteAtomically(change.Path, change.Content);
                    written.Add(change.Path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                foreach (var path in written.AsEnumerable().Reverse())
                    try
                    {
                        WriteAtomically(path, originals[path]);
                    }
                    catch (Exception rollback) when (rollback is IOException or UnauthorizedAccessException)
                    {
                        diagnostics.Add(new(NormalizationIoFailedCode,
                            $"Rollback failed after normalization error: {rollback.Message}",
                            Path.GetRelativePath(root, path).Replace('\\', '/')));
                    }
                diagnostics.Add(new(NormalizationIoFailedCode,
                    $"No partial normalization was retained: {exception.Message}", "workspace"));
            }
        }
        return new(diagnostics.Count == 0, dryRun, changes.Select(item => item.Change).ToArray(), diagnostics);
    }

    public static WorkspaceMigrationReport MigrateWorkspace(
        string workspaceRoot,
        WorkspaceBuildProfile profile,
        bool dryRun,
        string? targetVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(profile);
        var root = Path.GetFullPath(workspaceRoot);
        var projectPath = Path.Combine(root, ProjectSession.DefaultProjectFileName);
        try
        {
            using var parsed = JsonDocument.Parse(File.ReadAllText(projectPath));
            var from = parsed.RootElement.GetProperty("formatVersion").GetString() ?? string.Empty;
            var to = string.IsNullOrWhiteSpace(targetVersion) ? ProjectSession.CurrentProjectFormatVersion : targetVersion;
            if (!string.Equals(to, ProjectSession.CurrentProjectFormatVersion, StringComparison.Ordinal))
                return new(false, dryRun, from, to, false, true, false, null, [],
                    [new(MigrationUnsupportedCode,
                        "Downgrade is not available for this project schema; no source was modified.", "project.json/formatVersion")]);
            var opened = ProjectSession.Open(root);
            if (!opened.Succeeded || opened.Session is null)
                return new(false, dryRun, from, to, false, false, false, null, [],
                    opened.Diagnostics.Select(item => new BuildCollaborationDiagnostic(
                        item.Code, item.Message, item.Path ?? "project.json")).ToArray());
            var bundle = WorkspaceProjectJsonSerializer.Serialize(opened.Session.Project);
            var candidates = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["project.json"] = bundle.ProjectJson,
            };
            foreach (var item in bundle.AreaDocuments) candidates[item.Key] = item.Value;
            var changed = candidates.Where(item =>
                    !File.Exists(Path.Combine(root, item.Key.Replace('/', Path.DirectorySeparatorChar))) ||
                    !string.Equals(File.ReadAllText(Path.Combine(root, item.Key.Replace('/', Path.DirectorySeparatorChar))),
                        item.Value, StringComparison.Ordinal))
                .Select(item => item.Key).Order(StringComparer.Ordinal).ToArray();
            if (dryRun || changed.Length == 0)
                return new(true, dryRun, from, to, false, true, true, null, changed, []);
            var backupRelative = $"{profile.Collaboration.Migrations.BackupDirectory}/{from}-to-{to}";
            if (!TryResolveContainedDirectory(root, backupRelative, out var backup, out var error))
                return new(false, false, from, to, false, false, false, null, changed,
                    [new(MigrationIoFailedCode, error, backupRelative)]);
            if (!TryResolveContainedDirectory(root, profile.Collaboration.Migrations.BackupDirectory,
                    out var backupRoot, out error))
                return new(false, false, from, to, false, false, false, null, changed,
                    [new(MigrationIoFailedCode, error, profile.Collaboration.Migrations.BackupDirectory)]);
            var existingBackups = Directory.Exists(backupRoot)
                ? Directory.EnumerateDirectories(backupRoot, "*", SearchOption.TopDirectoryOnly).Count()
                : 0;
            if (!Directory.Exists(backup) && existingBackups >= profile.Collaboration.Migrations.MaximumBackups)
                return new(false, false, from, to, false, false, true, null, changed,
                    [new(MigrationIoFailedCode,
                        "Migration backup limit reached; remove an obsolete backup explicitly before applying.",
                        profile.Collaboration.Migrations.BackupDirectory)]);
            try
            {
                Directory.CreateDirectory(backup);
                foreach (var relative in changed)
                {
                    var source = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(source)) continue;
                    var destination = Path.Combine(backup, relative.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    File.Copy(source, destination, overwrite: true);
                }
                var saved = opened.Session.Save(opened.Session.Project);
                if (!saved.Succeeded)
                {
                    var restoreDiagnostics = RestoreMigration(root, backup, changed);
                    return new(false, false, from, to, false, false, true, backupRelative, changed,
                        [.. saved.Diagnostics.Select(item => new BuildCollaborationDiagnostic(
                            item.Code, item.Message, item.Path ?? "project.json")), .. restoreDiagnostics]);
                }
                var idempotence = ProjectSession.Open(root);
                var stable = idempotence.Succeeded && idempotence.Session is not null &&
                             MigrationDocumentsAreStable(root,
                                 WorkspaceProjectJsonSerializer.Serialize(idempotence.Session.Project));
                if (stable)
                    return new(true, false, from, to, true, true, true, backupRelative, changed, []);
                var idempotenceDiagnostics = RestoreMigration(root, backup, changed);
                return new(false, false, from, to, false, false, true, backupRelative, changed,
                    [new(MigrationIoFailedCode,
                            "Migration was not idempotent; the backup was restored.", "project.json"),
                        .. idempotenceDiagnostics]);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                var restoreDiagnostics = RestoreMigration(root, backup, changed);
                return new(false, false, from, to, false, false, true, backupRelative, changed,
                    [new(MigrationIoFailedCode,
                            $"Migration failed; the backup was restored: {exception.Message}", "project.json"),
                        .. restoreDiagnostics]);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or
                                           ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return new(false, dryRun, string.Empty, targetVersion ?? ProjectSession.CurrentProjectFormatVersion,
                false, false, false, null, [], [new(MigrationIoFailedCode, exception.Message, "project.json")]);
        }
    }

    public static WorkspaceReferenceIndex BuildReferenceIndex(
        string workspaceRoot,
        ProjectDefinition project,
        string projectPath,
        WorkspaceBuildCollaborationProfile profile)
    {
        var graph = WorkspaceBuildGraphBuilder.Build(workspaceRoot, project, projectPath).Graph;
        if (graph.Nodes.Count > profile.References.MaximumNodes)
            return new(graph.Id.ToString(), [],
                [new(ReferencesLimitCode, "Reference graph exceeds the configured node limit.", "build/references")]);
        var byId = graph.Nodes.ToDictionary(item => item.Id, item => item.Path);
        var reverse = graph.Nodes.SelectMany(node => node.Dependencies.Select(dependency => (dependency, node.Path)))
            .GroupBy(item => item.dependency)
            .ToDictionary(group => group.Key,
                group => (IReadOnlyList<string>)group.Select(item => item.Path).Order(StringComparer.Ordinal).ToArray());
        var nodes = graph.Nodes.Select(node => new WorkspaceReferenceNode(
            node.Id.ToString(), node.Path, node.Kind,
            node.Dependencies.Select(id => byId[id]).Order(StringComparer.Ordinal).ToArray(),
            reverse.GetValueOrDefault(node.Id, []).Order(StringComparer.Ordinal).ToArray())).ToArray();
        return new(graph.Id.ToString(), nodes, []);
    }

    public static IReadOnlyList<WorkspaceSemanticResource> CreateSemanticSnapshot(
        ProjectDefinition project,
        BuildDependencyGraph graph)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(graph);
        var resources = new List<WorkspaceSemanticResource>();
        resources.AddRange(project.Areas.Select(area => Resource($"area:{area.Id}", "area", area.DisplayName, area)));
        resources.AddRange(project.Areas.SelectMany(area => area.Entities)
            .Select(entity => Resource($"entity:{entity.Id}", "entity", entity.Id.ToString(), entity)));
        resources.AddRange(project.Dialogues.Select(dialogue =>
            Resource($"dialogue:{dialogue.Id}", "graph", dialogue.Slug, dialogue)));
        resources.AddRange(project.Quests.Select(quest => Resource($"quest:{quest.Id}", "graph", quest.Slug, quest)));
        resources.AddRange(project.Module.Content.Packages.Packages.Select(package =>
            Resource($"package:{package.Id}", "package", package.PackageNamespace, package)));
        resources.AddRange(graph.Nodes.Where(node => node.Kind == BuildResourceKind.Script).Select(node =>
            new WorkspaceSemanticResource($"script:{node.Id}", "script", node.Path, node.ContentSha256)));
        return resources.OrderBy(item => item.Domain, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
    }

    public static WorkspaceSemanticDiffReport Diff(
        IReadOnlyList<WorkspaceSemanticResource> before,
        IReadOnlyList<WorkspaceSemanticResource> after,
        WorkspaceBuildCollaborationProfile profile)
    {
        var left = before.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var right = after.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var changes = left.Keys.Union(right.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(id =>
        {
            left.TryGetValue(id, out var oldValue);
            right.TryGetValue(id, out var newValue);
            if (oldValue is null) return new WorkspaceSemanticChange(WorkspaceSemanticChangeKind.Added, id,
                newValue!.Domain, newValue.DisplayName, null, newValue.Sha256);
            if (newValue is null) return new WorkspaceSemanticChange(WorkspaceSemanticChangeKind.Removed, id,
                oldValue.Domain, oldValue.DisplayName, oldValue.Sha256, null);
            return string.Equals(oldValue.Sha256, newValue.Sha256, StringComparison.Ordinal)
                ? null
                : new WorkspaceSemanticChange(WorkspaceSemanticChangeKind.Modified, id, newValue.Domain,
                    newValue.DisplayName, oldValue.Sha256, newValue.Sha256);
        }).Where(item => item is not null).Cast<WorkspaceSemanticChange>()
            .Take(profile.SemanticDiff.MaximumEntries + 1).ToArray();
        if (changes.Length > profile.SemanticDiff.MaximumEntries)
            return new(false, string.Empty, [],
                [new("build.semantic_diff.limit_exceeded", "Semantic diff exceeds the configured entry limit.",
                    "build/semanticDiff")]);
        var text = changes.Length == 0 ? "No semantic changes." : string.Join("\n", changes.Select(item =>
            $"{item.Kind.ToString().ToLowerInvariant()} {item.Domain} {item.DisplayName} ({item.Id})"));
        return new(true, text, changes, []);
    }

    public static IReadOnlyList<WorkspaceSemanticResource> LoadSemanticSnapshot(string workspaceRoot)
    {
        var path = Path.Combine(Path.GetFullPath(workspaceRoot),
            SemanticSnapshotRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) return [];
        try
        {
            return (JsonSerializer.Deserialize<WorkspaceSemanticResource[]>(File.ReadAllText(path), IndentedJson) ?? [])
                .OrderBy(item => item.Domain, StringComparer.Ordinal).ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    public static void SaveSemanticSnapshot(
        string workspaceRoot,
        IReadOnlyList<WorkspaceSemanticResource> resources)
    {
        var path = Path.Combine(Path.GetFullPath(workspaceRoot),
            SemanticSnapshotRelativePath.Replace('/', Path.DirectorySeparatorChar));
        WriteAtomically(path, JsonSerializer.Serialize(resources
            .OrderBy(item => item.Domain, StringComparer.Ordinal).ThenBy(item => item.Id, StringComparer.Ordinal),
            IndentedJson) + "\n");
    }

    private static WorkspaceSemanticResource Resource(string id, string domain, string name, object value) =>
        new(id, domain, name, Sha256(JsonSerializer.SerializeToUtf8Bytes(value, IndentedJson)));

    private static DisposableCacheEntry Entry(string root, string path)
    {
        var bytes = File.ReadAllBytes(path);
        return new(Path.GetRelativePath(root, path).Replace('\\', '/'), bytes.LongLength, Sha256(bytes));
    }

    private static IEnumerable<string> EnumerateCanonicalJson(string root) =>
        Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).Where(path =>
        {
            var segments = Path.GetRelativePath(root, path)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries);
            return !segments.Any(segment => segment is ".forge" or ".godot" or "bin" or "obj" or
                "artifacts" or "tmp" or "native");
        });

    private static string NormalizeJson(byte[] json)
    {
        var content = json.AsMemory();
        if (json.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) content = content[3..];
        using var document = JsonDocument.Parse(content);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            WriteCanonical(writer, document.RootElement);
        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.Number when value.TryGetInt64(out var integer): writer.WriteNumberValue(integer); break;
            case JsonValueKind.Number when value.TryGetDecimal(out var number): writer.WriteNumberValue(number); break;
            case JsonValueKind.Number: writer.WriteNumberValue(value.GetDouble()); break;
            case JsonValueKind.String: writer.WriteStringValue(value.GetString()); break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new JsonException("Unsupported JSON token.");
        }
    }

    private static bool TryResolveContainedDirectory(string rootPath, string relative, out string resolved, out string error)
    {
        var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        resolved = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        error = string.Empty;
        if (string.Equals(root, resolved, StringComparison.OrdinalIgnoreCase) ||
            !resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            error = "Directory must be a contained workspace subdirectory.";
            return false;
        }
        return true;
    }

    private static void WriteAtomically(string path, string content)
        => WriteAtomically(path, new UTF8Encoding(false).GetBytes(content));

    private static void WriteAtomically(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, content);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static IReadOnlyList<BuildCollaborationDiagnostic> RestoreMigration(
        string root,
        string backup,
        IReadOnlyList<string> changed)
    {
        var diagnostics = new List<BuildCollaborationDiagnostic>();
        foreach (var relative in changed.Reverse())
        {
            var target = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            var source = Path.Combine(backup, relative.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                if (File.Exists(source)) WriteAtomically(target, File.ReadAllBytes(source));
                else if (File.Exists(target)) File.Delete(target);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new(MigrationIoFailedCode,
                    $"Migration rollback failed: {exception.Message}", relative));
            }
        }
        return diagnostics;
    }

    private static bool MigrationDocumentsAreStable(string root, WorkspaceProjectBundle bundle)
    {
        if (!string.Equals(File.ReadAllText(Path.Combine(root, ProjectSession.DefaultProjectFileName)),
                bundle.ProjectJson, StringComparison.Ordinal)) return false;
        return bundle.AreaDocuments.All(item =>
        {
            var path = Path.Combine(root, item.Key.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) && string.Equals(File.ReadAllText(path), item.Value, StringComparison.Ordinal);
        });
    }

    private static string Sha256(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
}
