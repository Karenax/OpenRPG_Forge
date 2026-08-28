using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
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
    public const string RevisionConflictCode = "workspace.revision_conflict";
    public const string DirectoryNotEmptyCode = "workspace.directory_not_empty";
    public const string CreateFailedCode = "workspace.create_failed";
    public const string DefaultProjectFileName = "project.json";
    public const string CurrentProjectFormatVersion = WorkspaceProjectJsonSerializer.CurrentFormatVersion;
    public const string RevisionStateRelativePath = ".forge/project-session.json";

    private const int CurrentRevisionStateSchemaVersion = 1;
    private const string RevisionLockRelativePath = ".forge/project-session.lock";

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

    public long Revision { get; private set; }

    private string _observedFingerprint;
    private IReadOnlySet<string>? _allowedMigrationDiagnosticCodes;

    private ProjectSession(
        string rootDirectory,
        string manifestPath,
        string projectFilePath,
        ProjectDefinition project,
        long revision,
        string observedFingerprint,
        IReadOnlySet<string>? allowedMigrationDiagnosticCodes)
    {
        RootDirectory = rootDirectory;
        ManifestPath = manifestPath;
        ProjectFilePath = projectFilePath;
        Project = project;
        Revision = revision;
        _observedFingerprint = observedFingerprint;
        _allowedMigrationDiagnosticCodes = allowedMigrationDiagnosticCodes is null
            ? null
            : new HashSet<string>(allowedMigrationDiagnosticCodes, StringComparer.Ordinal);
    }

    public static ProjectSessionOpenResult Open(string workspaceDirectory) =>
        OpenCore(workspaceDirectory, null);

    public static ProjectSessionOpenResult OpenForMigration(
        string workspaceDirectory,
        IReadOnlySet<string> allowedProjectDiagnosticCodes)
    {
        ArgumentNullException.ThrowIfNull(allowedProjectDiagnosticCodes);
        if (allowedProjectDiagnosticCodes.Count == 0 ||
            allowedProjectDiagnosticCodes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException(
                "Migration opening requires an explicit non-empty diagnostic allowlist.",
                nameof(allowedProjectDiagnosticCodes));
        return OpenCore(workspaceDirectory, allowedProjectDiagnosticCodes);
    }

    private static ProjectSessionOpenResult OpenCore(
        string workspaceDirectory,
        IReadOnlySet<string>? allowedProjectDiagnosticCodes)
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

        FileStream? revisionLock = null;
        try
        {
            revisionLock = AcquireRevisionLock(rootDirectory);
            ProjectWorkspaceTransaction.RecoverPending(rootDirectory);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            revisionLock?.Dispose();
            return Failure(ProjectInvalidCode, exception.Message, rootDirectory);
        }
        using var heldRevisionLock = revisionLock;

        var manifestPath = Path.Combine(rootDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return Failure(
                ManifestMissingCode,
                $"The Forge project does not contain '{ManifestFileName}'.",
                manifestPath);
        }

        ProjectWorkspaceManifest? manifest;
        string manifestJson;
        try
        {
            manifestJson = File.ReadAllText(manifestPath);
            manifest = JsonSerializer.Deserialize<ProjectWorkspaceManifest>(
                manifestJson,
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
            var projectJson = File.ReadAllText(projectFilePath);
            var areaDocuments = new Dictionary<string, string>(StringComparer.Ordinal);
            var project = WorkspaceProjectJsonSerializer.Deserialize(
                projectJson,
                relativePath =>
                {
                    var document = ReadAreaDocument(projectDirectory, relativePath);
                    areaDocuments[relativePath] = document;
                    return document;
                },
                allowedProjectDiagnosticCodes);
            var fingerprint = ComputeWorkspaceFingerprint(
                manifestJson,
                Path.GetRelativePath(rootDirectory, projectFilePath),
                projectJson,
                areaDocuments);
            var revision = ReadEffectiveRevision(rootDirectory, fingerprint);
            var session = new ProjectSession(
                rootDirectory,
                manifestPath,
                projectFilePath,
                project,
                revision,
                fingerprint,
                allowedProjectDiagnosticCodes);
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

    public ProjectSessionSaveResult Save(ProjectDefinition project) =>
        Save(project, targetRevision: null);

    public ProjectSessionSaveResult Save(ProjectDefinition project, long targetRevision) =>
        Save(project, (long?)targetRevision);

    private ProjectSessionSaveResult Save(ProjectDefinition project, long? targetRevision)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (targetRevision is < 0)
        {
            return SaveFailure(ProjectInvalidCode, "A workspace revision cannot be negative.", ProjectFilePath);
        }

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
            using var revisionLock = AcquireRevisionLock(RootDirectory);
            var currentFingerprint = ComputeWorkspaceFingerprintFromDisk(
                RootDirectory,
                ManifestPath,
                ProjectFilePath,
                _allowedMigrationDiagnosticCodes);
            if (!string.Equals(currentFingerprint, _observedFingerprint, StringComparison.Ordinal))
            {
                return SaveFailure(
                    RevisionConflictCode,
                    "The Forge workspace changed on disk after this session was opened. Reload and reapply the pending command.",
                    ProjectFilePath);
            }

            var nextRevision = Math.Max(targetRevision ?? checked(Revision + 1), checked(Revision + 1));
            var manifestJson = File.ReadAllText(ManifestPath);
            var savedFingerprint = ComputeWorkspaceFingerprint(
                manifestJson,
                Path.GetRelativePath(RootDirectory, ProjectFilePath),
                bundle.ProjectJson,
                bundle.AreaDocuments);
            var revisionStateJson = SerializeRevisionState(nextRevision, savedFingerprint);
            ProjectWorkspaceTransaction.Commit(
                RootDirectory,
                BuildWorkspaceTransactionDocuments(bundle, revisionStateJson));
            Project = project;
            Revision = nextRevision;
            _observedFingerprint = savedFingerprint;
            _allowedMigrationDiagnosticCodes = null;
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

    private IReadOnlyDictionary<string, string> BuildWorkspaceTransactionDocuments(
        WorkspaceProjectBundle bundle,
        string revisionStateJson)
    {
        var documents = new Dictionary<string, string>(StringComparer.Ordinal);
        var projectDirectory = Path.GetDirectoryName(ProjectFilePath)
            ?? throw new IOException("The project index does not have a parent directory.");
        foreach (var areaDocument in bundle.AreaDocuments)
        {
            if (!TryResolveProjectPath(projectDirectory, areaDocument.Key, out var areaPath))
            {
                throw new IOException($"Area document path '{areaDocument.Key}' is unsafe.");
            }

            documents.Add(
                Path.GetRelativePath(RootDirectory, areaPath).Replace('\\', '/'),
                areaDocument.Value);
        }

        documents.Add(
            Path.GetRelativePath(RootDirectory, ProjectFilePath).Replace('\\', '/'),
            bundle.ProjectJson);
        documents.Add(RevisionStateRelativePath, revisionStateJson);
        return documents;
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

    private static FileStream AcquireRevisionLock(string rootDirectory)
    {
        var lockPath = Path.Combine(
            rootDirectory,
            RevisionLockRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        IOException? lastError = null;
        do
        {
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.WriteThrough);
            }
            catch (IOException exception)
            {
                lastError = exception;
                Thread.Sleep(25);
            }
        } while (DateTime.UtcNow < deadline);

        throw new IOException("The Forge workspace is currently being saved by another process.", lastError);
    }

    private static string ComputeWorkspaceFingerprintFromDisk(
        string rootDirectory,
        string manifestPath,
        string projectFilePath,
        IReadOnlySet<string>? allowedMigrationDiagnosticCodes)
    {
        var manifestJson = File.ReadAllText(manifestPath);
        var projectJson = File.ReadAllText(projectFilePath);
        var projectDirectory = Path.GetDirectoryName(projectFilePath)
            ?? throw new IOException("The project index does not have a parent directory.");
        var areaDocuments = new Dictionary<string, string>(StringComparer.Ordinal);
        _ = WorkspaceProjectJsonSerializer.Deserialize(
            projectJson,
            relativePath =>
            {
                var document = ReadAreaDocument(projectDirectory, relativePath);
                areaDocuments[relativePath] = document;
                return document;
            },
            allowedMigrationDiagnosticCodes);
        return ComputeWorkspaceFingerprint(
            manifestJson,
            Path.GetRelativePath(rootDirectory, projectFilePath),
            projectJson,
            areaDocuments);
    }

    private static string ComputeWorkspaceFingerprint(
        string manifestJson,
        string relativeProjectPath,
        string projectJson,
        IReadOnlyDictionary<string, string> areaDocuments)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendFingerprintComponent(hash, ProjectSession.ManifestFileName, manifestJson);
        AppendFingerprintComponent(
            hash,
            relativeProjectPath.Replace('\\', '/'),
            projectJson);
        foreach (var areaDocument in areaDocuments.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            AppendFingerprintComponent(hash, areaDocument.Key.Replace('\\', '/'), areaDocument.Value);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendFingerprintComponent(IncrementalHash hash, string path, string content)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(path));
        hash.AppendData([0]);
        hash.AppendData(Encoding.UTF8.GetBytes(content));
        hash.AppendData([byte.MaxValue]);
    }

    private static long ReadEffectiveRevision(string rootDirectory, string currentFingerprint)
    {
        var statePath = Path.Combine(
            rootDirectory,
            RevisionStateRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(statePath)) return 0;

        var state = JsonSerializer.Deserialize<ProjectSessionRevisionState>(
            File.ReadAllText(statePath),
            ManifestOptions);
        if (state is null || state.SchemaVersion != CurrentRevisionStateSchemaVersion || state.Revision < 0 ||
            string.IsNullOrWhiteSpace(state.Fingerprint))
        {
            throw new JsonException("The Forge workspace revision state is invalid.");
        }

        return string.Equals(state.Fingerprint, currentFingerprint, StringComparison.Ordinal)
            ? state.Revision
            : checked(state.Revision + 1);
    }

    private static string SerializeRevisionState(long revision, string fingerprint) =>
        JsonSerializer.Serialize(
            new ProjectSessionRevisionState(CurrentRevisionStateSchemaVersion, revision, fingerprint),
            ManifestOptions);

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

    private sealed record ProjectSessionRevisionState(
        int SchemaVersion,
        long Revision,
        string Fingerprint);
}
