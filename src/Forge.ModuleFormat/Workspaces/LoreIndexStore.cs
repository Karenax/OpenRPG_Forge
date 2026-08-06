using System.Text;
using System.Text.Json;
using Forge.Domain.Assistant;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Workspaces;

public static class LoreIndexStore
{
    public const string RelativePath = "assistant/lore-index.json";
    public const string ReadFailedCode = "assistant.lore.read_failed";
    public const string WriteFailedCode = "assistant.lore.write_failed";

    public static LoreIndexLoadResult Load(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var path = ResolvePath(workspaceRoot);
        if (!File.Exists(path))
        {
            return new LoreIndexLoadResult(LoreIndexDefinition.CreateDefault(), true, []);
        }
        try
        {
            return new LoreIndexLoadResult(
                LoreIndexJsonSerializer.Deserialize(File.ReadAllText(path)),
                false,
                []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return new LoreIndexLoadResult(
                LoreIndexDefinition.CreateDefault(),
                false,
                [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, path)]);
        }
    }

    public static ProjectSessionSaveResult Save(string workspaceRoot, LoreIndexDefinition index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(index);
        var path = ResolvePath(workspaceRoot);
        try
        {
            var directory = Path.GetDirectoryName(path)
                ?? throw new IOException("The lore index path has no parent directory.");
            Directory.CreateDirectory(directory);
            WriteAtomically(path, LoreIndexJsonSerializer.Serialize(index));
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
        Path.Combine(Path.GetFullPath(workspaceRoot), "assistant", "lore-index.json");

    private static void WriteAtomically(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}

public sealed record LoreIndexLoadResult(
    LoreIndexDefinition Index,
    bool FileMissing,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
