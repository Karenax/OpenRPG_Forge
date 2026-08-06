using System.Text;
using System.Text.Json;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Workspaces;

public static class ExternalWorkerSecurityPolicyStore
{
    public const string RelativePath = "assets/worker-security.json";
    public const string ReadFailedCode = "asset.worker_security.read_failed";
    public const string WriteFailedCode = "asset.worker_security.write_failed";

    public static ExternalWorkerSecurityPolicyLoadResult Load(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var path = ResolvePath(workspaceRoot);
        if (!File.Exists(path))
            return new(ExternalWorkerSecurityPolicy.CreateDefault(), true, []);
        try
        {
            return new(ExternalWorkerSecurityPolicyJsonSerializer.Deserialize(File.ReadAllText(path)), false, []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return new(ExternalWorkerSecurityPolicy.CreateDefault(), false,
                [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, path)]);
        }
    }

    public static ProjectSessionSaveResult Save(string workspaceRoot, ExternalWorkerSecurityPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(policy);
        var path = ResolvePath(workspaceRoot);
        try
        {
            var directory = Path.GetDirectoryName(path) ?? throw new IOException("Worker security path has no parent.");
            Directory.CreateDirectory(directory);
            WriteAtomically(path, ExternalWorkerSecurityPolicyJsonSerializer.Serialize(policy));
            return new(true, []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return new(false, [new ProjectSessionDiagnostic(WriteFailedCode, exception.Message, path)]);
        }
    }

    private static string ResolvePath(string workspaceRoot) =>
        Path.Combine(Path.GetFullPath(workspaceRoot), "assets", "worker-security.json");

    private static void WriteAtomically(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
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

public sealed record ExternalWorkerSecurityPolicyLoadResult(
    ExternalWorkerSecurityPolicy Policy,
    bool FileMissing,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
