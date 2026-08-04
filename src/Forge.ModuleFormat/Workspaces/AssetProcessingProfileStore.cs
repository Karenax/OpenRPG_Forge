using System.Text;
using System.Text.Json;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Workspaces;

public static class AssetProcessingProfileStore
{
    public const string RelativePath = "assets/import-profiles.json";
    public const string ReadFailedCode = "asset.profile.read_failed";
    public const string WriteFailedCode = "asset.profile.write_failed";

    public static AssetProcessingProfileLoadResult Load(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var path = ResolvePath(workspaceRoot);
        if (!File.Exists(path))
        {
            return new AssetProcessingProfileLoadResult([], true, []);
        }

        try
        {
            return new AssetProcessingProfileLoadResult(
                AssetProcessingProfileJsonSerializer.Deserialize(File.ReadAllText(path)),
                false,
                []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return new AssetProcessingProfileLoadResult(
                [],
                false,
                [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, path)]);
        }
    }

    public static ProjectSessionSaveResult Save(
        string workspaceRoot,
        IReadOnlyList<AssetProcessingProfile> profiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(profiles);
        var path = ResolvePath(workspaceRoot);
        try
        {
            var directory = Path.GetDirectoryName(path)
                ?? throw new IOException("The asset processing profile path has no parent directory.");
            Directory.CreateDirectory(directory);
            WriteAtomically(path, AssetProcessingProfileJsonSerializer.Serialize(profiles));
            return new ProjectSessionSaveResult(true, []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return new ProjectSessionSaveResult(
                false,
                [new ProjectSessionDiagnostic(WriteFailedCode, exception.Message, path)]);
        }
    }

    private static string ResolvePath(string workspaceRoot) =>
        Path.Combine(Path.GetFullPath(workspaceRoot), "assets", "import-profiles.json");

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

public sealed record AssetProcessingProfileLoadResult(
    IReadOnlyList<AssetProcessingProfile> Profiles,
    bool FileMissing,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
