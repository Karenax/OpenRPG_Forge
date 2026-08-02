using System.IO.Compression;
using System.Text;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Packages;

public static class ModulePackager
{
    private static readonly DateTimeOffset DeterministicTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static ModulePackageExportResult Export(
        string workspacePath,
        string destinationPath,
        string moduleVersion = "0.1.0",
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

        var projectPath = Path.Combine(workspaceRoot, "project.json");
        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException("Module workspace does not contain project.json.", projectPath);
        }

        var project = ProjectJsonSerializer.Deserialize(File.ReadAllText(projectPath));
        var entries = new List<(string Path, byte[] Content)>
        {
            ("content/project.json", Encoding.UTF8.GetBytes(ProjectJsonSerializer.Serialize(project))),
        };
        foreach (var file in Directory.EnumerateFiles(workspaceRoot, "*", SearchOption.AllDirectories)
                     .Select(Path.GetFullPath)
                     .Where(file => !string.Equals(file, projectPath, StringComparison.OrdinalIgnoreCase))
                     .Where(file => !string.Equals(file, destination, StringComparison.OrdinalIgnoreCase))
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
            moduleVersion,
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

    private static void WriteEntry(ZipArchive archive, string path, byte[] content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = DeterministicTimestamp;
        using var stream = entry.Open();
        stream.Write(content);
    }
}
