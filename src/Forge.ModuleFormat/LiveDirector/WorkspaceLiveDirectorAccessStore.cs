using System.Text;
using System.Text.Json;
using Forge.LiveDirector.Authoring;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.LiveDirector;

public static class WorkspaceLiveDirectorAccessStore
{
    public const string RelativePath = ".forge/live-director/access.json";
    public const string ReadFailedCode = "live_director.access.read_failed";
    public const string WriteFailedCode = "live_director.access.write_failed";

    public static WorkspaceLiveDirectorAccessLoadResult Load(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var path = ResolvePath(workspaceRoot);
        if (!File.Exists(path))
            return new(LiveDirectorAccessProfile.CreateDefault(
                "workspace:live-director-access"), true, []);
        try
        {
            var profile = new LiveDirectorAccessStore().Deserialize(
                File.ReadAllText(path, Encoding.UTF8));
            return new(profile, false, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               InvalidDataException or JsonException or
                                               NotSupportedException or ArgumentException)
        {
            return new(LiveDirectorAccessProfile.CreateDefault(
                    "workspace:live-director-access"), false,
                [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, path)]);
        }
    }

    public static ProjectSessionSaveResult Save(string workspaceRoot,
        LiveDirectorAccessProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(profile);
        var path = ResolvePath(workspaceRoot);
        try
        {
            var directory = Path.GetDirectoryName(path)
                ?? throw new IOException("Live Director access path has no parent directory.");
            Directory.CreateDirectory(directory);
            WriteAtomically(path, new LiveDirectorAccessStore().Serialize(profile));
            return new(true, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               InvalidDataException or JsonException or
                                               NotSupportedException or ArgumentException)
        {
            return new(false,
                [new ProjectSessionDiagnostic(WriteFailedCode, exception.Message, path)]);
        }
    }

    private static string ResolvePath(string workspaceRoot) => Path.GetFullPath(Path.Combine(
        Path.GetFullPath(workspaceRoot),
        RelativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static void WriteAtomically(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var temporary = Path.Combine(directory,
            $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096,
                       leaveOpen: true))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}

public sealed record WorkspaceLiveDirectorAccessLoadResult(
    LiveDirectorAccessProfile Profile,
    bool FileMissing,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
