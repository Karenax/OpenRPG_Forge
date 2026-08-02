using System.Text;
using System.Text.Json;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Workspaces;

public sealed class ProjectSession
{
    public const string ManifestFileName = "forge.project.json";
    public const string CurrentManifestVersion = "0.1.0";
    public const string DirectoryMissingCode = "workspace.directory_missing";
    public const string ManifestMissingCode = "workspace.manifest_missing";
    public const string ManifestInvalidCode = "workspace.manifest_invalid";
    public const string ManifestVersionUnsupportedCode = "workspace.manifest_version_unsupported";
    public const string ProjectPathUnsafeCode = "workspace.project_path_unsafe";
    public const string ProjectMissingCode = "workspace.project_missing";
    public const string ProjectInvalidCode = "workspace.project_invalid";
    public const string AreaPathUnsafeCode = "workspace.area_path_unsafe";
    public const string AreaMissingCode = "workspace.area_missing";
    public const string SaveFailedCode = "workspace.save_failed";
    public const string DirectoryNotEmptyCode = "workspace.directory_not_empty";
    public const string CreateFailedCode = "workspace.create_failed";
    public const string DefaultProjectFileName = "project.json";
    public const string CurrentProjectFormatVersion = WorkspaceProjectJsonSerializer.CurrentFormatVersion;

    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public string RootDirectory { get; }

    public string ManifestPath { get; }

    public string ProjectFilePath { get; }

    public ProjectDefinition Project { get; private set; }

    private ProjectSession(
        string rootDirectory,
        string manifestPath,
        string projectFilePath,
        ProjectDefinition project)
    {
        RootDirectory = rootDirectory;
        ManifestPath = manifestPath;
        ProjectFilePath = projectFilePath;
        Project = project;
    }

    public static ProjectSessionOpenResult Open(string workspaceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);

        string rootDirectory;
        try
        {
            rootDirectory = Path.GetFullPath(workspaceDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Failure(DirectoryMissingCode, exception.Message, workspaceDirectory);
        }

        if (!Directory.Exists(rootDirectory))
        {
            return Failure(
                DirectoryMissingCode,
                "The Forge project directory does not exist.",
                rootDirectory);
        }

        var manifestPath = Path.Combine(rootDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return Failure(
                ManifestMissingCode,
                $"The Forge project does not contain '{ManifestFileName}'.",
                manifestPath);
        }

        ProjectWorkspaceManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ProjectWorkspaceManifest>(
                File.ReadAllText(manifestPath),
                ManifestOptions);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return Failure(ManifestInvalidCode, exception.Message, manifestPath);
        }

        if (manifest is null || string.IsNullOrWhiteSpace(manifest.ProjectFile))
        {
            return Failure(
                ManifestInvalidCode,
                "The Forge project manifest must declare a projectFile.",
                manifestPath);
        }

        if (!string.Equals(
                manifest.FormatVersion,
                CurrentManifestVersion,
                StringComparison.Ordinal))
        {
            return Failure(
                ManifestVersionUnsupportedCode,
                $"Workspace format '{manifest.FormatVersion}' is not supported; expected '{CurrentManifestVersion}'.",
                manifestPath);
        }

        if (!TryResolveProjectPath(rootDirectory, manifest.ProjectFile, out var projectFilePath))
        {
            return Failure(
                ProjectPathUnsafeCode,
                "The projectFile must be a relative JSON path contained in the Forge project directory.",
                manifestPath);
        }

        if (!File.Exists(projectFilePath))
        {
            return Failure(
                ProjectMissingCode,
                "The project data file declared by the manifest does not exist.",
                projectFilePath);
        }

        try
        {
            var projectDirectory = Path.GetDirectoryName(projectFilePath)
                ?? throw new IOException("The project index does not have a parent directory.");
            var project = WorkspaceProjectJsonSerializer.Deserialize(
                File.ReadAllText(projectFilePath),
                relativePath => ReadAreaDocument(projectDirectory, relativePath));
            var session = new ProjectSession(
                rootDirectory,
                manifestPath,
                projectFilePath,
                project);
            return new ProjectSessionOpenResult(session, []);
        }
        catch (AreaDocumentReadException exception)
        {
            return Failure(exception.Code, exception.Message, exception.Path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or
            NotSupportedException or ProjectValidationException)
        {
            return Failure(ProjectInvalidCode, exception.Message, projectFilePath);
        }
    }

    public static ProjectSessionOpenResult Create(
        string workspaceDirectory,
        ProjectDefinition project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        ArgumentNullException.ThrowIfNull(project);

        WorkspaceProjectBundle bundle;
        try
        {
            bundle = WorkspaceProjectJsonSerializer.Serialize(project);
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or ProjectValidationException)
        {
            return Failure(ProjectInvalidCode, exception.Message, workspaceDirectory);
        }

        string rootDirectory;
        try
        {
            rootDirectory = Path.GetFullPath(workspaceDirectory);
            if (Directory.Exists(rootDirectory) && Directory.EnumerateFileSystemEntries(rootDirectory).Any())
            {
                return Failure(
                    DirectoryNotEmptyCode,
                    "A new Forge project requires an empty target directory.",
                    rootDirectory);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return Failure(CreateFailedCode, exception.Message, workspaceDirectory);
        }

        var manifestPath = Path.Combine(rootDirectory, ManifestFileName);
        var projectFilePath = Path.Combine(rootDirectory, DefaultProjectFileName);
        try
        {
            Directory.CreateDirectory(rootDirectory);
            WriteWorkspaceBundle(projectFilePath, bundle);
            var manifest = new ProjectWorkspaceManifest(
                CurrentManifestVersion,
                DefaultProjectFileName);
            WriteAllTextAtomically(
                manifestPath,
                JsonSerializer.Serialize(manifest, ManifestOptions));
            return Open(rootDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DeleteCreatedFile(manifestPath);
            DeleteCreatedFile(projectFilePath);
            foreach (var relativePath in bundle.AreaDocuments.Keys)
            {
                if (TryResolveProjectPath(rootDirectory, relativePath, out var areaPath))
                {
                    DeleteCreatedFile(areaPath);
                }
            }
            return Failure(CreateFailedCode, exception.Message, rootDirectory);
        }
    }

    public ProjectSessionSaveResult Save(ProjectDefinition project)
    {
        ArgumentNullException.ThrowIfNull(project);

        WorkspaceProjectBundle bundle;
        try
        {
            bundle = WorkspaceProjectJsonSerializer.Serialize(project);
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or ProjectValidationException)
        {
            return SaveFailure(ProjectInvalidCode, exception.Message, ProjectFilePath);
        }

        try
        {
            WriteWorkspaceBundle(ProjectFilePath, bundle);
            Project = project;
            return new ProjectSessionSaveResult(true, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return SaveFailure(SaveFailedCode, exception.Message, ProjectFilePath);
        }
    }

    private static bool TryResolveProjectPath(
        string rootDirectory,
        string relativeProjectPath,
        out string projectFilePath)
    {
        projectFilePath = string.Empty;
        if (Path.IsPathRooted(relativeProjectPath) ||
            !string.Equals(Path.GetExtension(relativeProjectPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var candidate = Path.GetFullPath(Path.Combine(rootDirectory, relativeProjectPath));
            var relative = Path.GetRelativePath(rootDirectory, candidate);
            if (relative.Equals("..", StringComparison.Ordinal) ||
                relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                Path.IsPathRooted(relative))
            {
                return false;
            }

            projectFilePath = candidate;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static string ReadAreaDocument(string projectDirectory, string relativePath)
    {
        if (!WorkspaceProjectJsonSerializer.IsSafeAreaDocumentPath(relativePath) ||
            !TryResolveProjectPath(projectDirectory, relativePath, out var areaPath))
        {
            throw new AreaDocumentReadException(
                AreaPathUnsafeCode,
                $"Area document path '{relativePath}' is unsafe.",
                relativePath);
        }

        if (!File.Exists(areaPath))
        {
            throw new AreaDocumentReadException(
                AreaMissingCode,
                $"Area document '{relativePath}' does not exist.",
                areaPath);
        }

        try
        {
            return File.ReadAllText(areaPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AreaDocumentReadException(ProjectInvalidCode, exception.Message, areaPath);
        }
    }

    private static void WriteWorkspaceBundle(string projectFilePath, WorkspaceProjectBundle bundle)
    {
        var projectDirectory = Path.GetDirectoryName(projectFilePath)
            ?? throw new IOException("The project index does not have a parent directory.");
        foreach (var areaDocument in bundle.AreaDocuments)
        {
            if (!TryResolveProjectPath(projectDirectory, areaDocument.Key, out var areaPath))
            {
                throw new IOException($"Area document path '{areaDocument.Key}' is unsafe.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(areaPath)!);
            WriteAllTextAtomically(areaPath, areaDocument.Value);
        }

        WriteAllTextAtomically(projectFilePath, bundle.ProjectJson);
    }

    private static void WriteAllTextAtomically(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new IOException("The project data path does not have a parent directory.");
        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(
                       stream,
                       new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                       bufferSize: 4096,
                       leaveOpen: true))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void DeleteCreatedFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static ProjectSessionOpenResult Failure(string code, string message, string? path) =>
        new(null, [new ProjectSessionDiagnostic(code, message, path)]);

    private static ProjectSessionSaveResult SaveFailure(string code, string message, string? path) =>
        new(false, [new ProjectSessionDiagnostic(code, message, path)]);

    private sealed class AreaDocumentReadException : Exception
    {
        internal string Code { get; }

        internal string Path { get; }

        internal AreaDocumentReadException(string code, string message, string path)
            : base(message)
        {
            Code = code;
            Path = path;
        }
    }
}
