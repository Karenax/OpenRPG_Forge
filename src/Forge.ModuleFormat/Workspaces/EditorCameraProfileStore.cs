using System.Text;
using System.Text.Json;
using Forge.Domain.Authoring;
using Forge.Domain.Projects;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Workspaces;

public static class EditorCameraProfileStore
{
    public const string RelativePath = ".forge/editor-camera.json";
    public const string ReadFailedCode = "editor.camera.profile_read_failed";
    public const string WriteFailedCode = "editor.camera.profile_write_failed";

    public static EditorCameraProfileLoadResult LoadOrCreate(string workspaceRoot, ProjectId projectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(projectId);
        var path = ResolvePath(workspaceRoot);
        if (!File.Exists(path))
        {
            return new EditorCameraProfileLoadResult(
                EditorCameraProfile.CreateDefault(EditorCameraProfileId.From(projectId.Value)),
                true,
                []);
        }

        try
        {
            return new EditorCameraProfileLoadResult(
                EditorCameraProfileJsonSerializer.Deserialize(File.ReadAllText(path)),
                false,
                []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return new EditorCameraProfileLoadResult(
                EditorCameraProfile.CreateDefault(EditorCameraProfileId.From(projectId.Value)),
                true,
                [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, path)]);
        }
    }

    public static ProjectSessionSaveResult Save(string workspaceRoot, EditorCameraProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(profile);
        var path = ResolvePath(workspaceRoot);
        try
        {
            var directory = Path.GetDirectoryName(path)
                ?? throw new IOException("The camera profile path has no parent directory.");
            Directory.CreateDirectory(directory);
            WriteAtomically(path, EditorCameraProfileJsonSerializer.Serialize(profile));
            return new ProjectSessionSaveResult(true, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ProjectSessionSaveResult(
                false,
                [new ProjectSessionDiagnostic(WriteFailedCode, exception.Message, path)]);
        }
    }

    private static string ResolvePath(string workspaceRoot)
    {
        var root = Path.GetFullPath(workspaceRoot);
        return Path.Combine(root, ".forge", "editor-camera.json");
    }

    private static void WriteAtomically(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
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
                       4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(
                       stream,
                       new UTF8Encoding(false),
                       4096,
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
}

public sealed record EditorCameraProfileLoadResult(
    EditorCameraProfile Profile,
    bool UsedDefault,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics);
