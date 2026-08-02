using System.IO.Compression;
using System.Text;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Packages;

public static class ModulePackager
{
    private static readonly DateTimeOffset DeterministicTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static ModulePackageExportResult Export(
        string workspacePath,
        string destinationPath,
        string? moduleVersion = null,
        string runtimeMinimumVersion = "0.1.0",
        string forgeScriptApiVersion = "1.0.0")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var workspaceRoot = Path.GetFullPath(workspacePath);
        var destination = Path.GetFullPath(destinationPath);
        if (!Directory.Exists(workspaceRoot))
        {
            throw new DirectoryNotFoundException($"Module workspace '{workspaceRoot}' does not exist.");
        }

        if (!string.Equals(Path.GetExtension(destination), ".orpmod", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Module destination must use the .orpmod extension.", nameof(destinationPath));
        }

        ProjectDefinition project;
        string projectPath;
        if (File.Exists(Path.Combine(workspaceRoot, ProjectSession.ManifestFileName)))
        {
            var opened = ProjectSession.Open(workspaceRoot);
            if (!opened.Succeeded || opened.Session is null)
            {
                throw new InvalidDataException(
                    $"Module workspace is invalid: {string.Join("; ", opened.Diagnostics.Select(item => item.Message))}");
            }

            project = opened.Session.Project;
            projectPath = opened.Session.ProjectFilePath;
        }
        else
        {
            projectPath = Path.Combine(workspaceRoot, ProjectSession.DefaultProjectFileName);
            if (!File.Exists(projectPath))
            {
                throw new FileNotFoundException("Module workspace does not contain project.json.", projectPath);
            }

            var bareProjectDirectory = Path.GetDirectoryName(projectPath)
                ?? throw new InvalidDataException("The workspace project document has no parent directory.");
            project = WorkspaceProjectJsonSerializer.Deserialize(
                File.ReadAllText(projectPath),
                relativePath => ReadBareAreaDocument(bareProjectDirectory, relativePath));
        }

        var bundle = WorkspaceProjectJsonSerializer.Serialize(project);
        var entries = new List<(string Path, byte[] Content)>
        {
            ("content/project.json", Encoding.UTF8.GetBytes(bundle.ProjectJson)),
        };
        entries.AddRange(bundle.AreaDocuments.Select(item =>
            ($"content/{item.Key}", Encoding.UTF8.GetBytes(item.Value))));
        var projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidDataException("The workspace project index has no parent directory.");
        foreach (var file in Directory.EnumerateFiles(workspaceRoot, "*", SearchOption.AllDirectories)
                     .Select(Path.GetFullPath)
                     .Where(file => !string.Equals(file, projectPath, StringComparison.OrdinalIgnoreCase))
                     .Where(file => !string.Equals(file, destination, StringComparison.OrdinalIgnoreCase))
                     .Where(file => !IsWorkspaceAreaDocument(projectDirectory, file))
                     .Where(file => !IsIgnoredWorkspaceFile(workspaceRoot, file))
                     .OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(workspaceRoot, file).Replace('\\', '/');
            var packagePath = $"content/{relative}";
            if (!ModulePackageValidator.IsSafeArchivePath(packagePath))
            {
                throw new InvalidOperationException($"Workspace file '{relative}' does not map to a safe package path.");
            }

            entries.Add((packagePath, File.ReadAllBytes(file)));
        }

        var hash = ModuleContentHash.Compute(entries);
        var manifest = ModuleManifest.Create(
            project,
            moduleVersion ?? project.Module.Metadata.Version,
            runtimeMinimumVersion,
            forgeScriptApiVersion,
            hash);
        var manifestDiagnostics = ModuleManifestValidator.Validate(manifest);
        if (manifestDiagnostics.Count > 0)
        {
            throw new InvalidOperationException(
                $"Module manifest is invalid: {string.Join("; ", manifestDiagnostics.Select(d => d.Message))}");
        }

        var destinationDirectory = Path.GetDirectoryName(destination)
            ?? throw new InvalidOperationException("Module destination has no parent directory.");
        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "manifest.json", Encoding.UTF8.GetBytes(ModuleManifestJson.Serialize(manifest)));
                foreach (var entry in entries.OrderBy(entry => entry.Path, StringComparer.Ordinal))
                {
                    WriteEntry(archive, entry.Path, entry.Content);
                }
            }

            File.Move(temporaryPath, destination, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        var validation = ModulePackageValidator.Validate(destination);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Exported module failed validation: {string.Join("; ", validation.Diagnostics.Select(d => d.Message))}");
        }

        return new ModulePackageExportResult(
            destination,
            manifest,
            entries.Count + 1,
            new FileInfo(destination).Length);
    }

    private static bool IsIgnoredWorkspaceFile(string workspaceRoot, string file)
    {
        var segments = Path.GetRelativePath(workspaceRoot, file)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment => segment is ".godot" or "bin" or "obj" or "artifacts" or "tmp" or "native") ||
               string.Equals(Path.GetFileName(file), "forge.project.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorkspaceAreaDocument(string projectDirectory, string file)
    {
        var relative = Path.GetRelativePath(projectDirectory, file).Replace('\\', '/');
        return relative.StartsWith("areas/", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetExtension(relative), ".json", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadBareAreaDocument(string projectDirectory, string relativePath)
    {
        if (!WorkspaceProjectJsonSerializer.IsSafeAreaDocumentPath(relativePath))
        {
            throw new InvalidDataException($"Area document path '{relativePath}' is unsafe.");
        }

        var path = Path.GetFullPath(Path.Combine(
            projectDirectory,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var containedRoot = Path.GetFullPath(projectDirectory).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!path.StartsWith(containedRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            throw new FileNotFoundException($"Area document '{relativePath}' is missing.", path);
        }

        return File.ReadAllText(path);
    }

    private static void WriteEntry(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = DeterministicTimestamp;
        using var stream = entry.Open();
        stream.Write(content);
    }
}
