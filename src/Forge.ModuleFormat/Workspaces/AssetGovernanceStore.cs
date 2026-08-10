using System.Text;
using System.Text.Json;
using Forge.Domain.Assets;
using Forge.ModuleFormat.Serialization;

namespace Forge.ModuleFormat.Workspaces;

public static class AssetGovernanceStore
{
    public const string RelativePath = ".forge/assets/asset-governance.json";
    public const string ReadFailedCode = "asset.governance.read_failed";
    public const string WriteFailedCode = "asset.governance.write_failed";

    public static AssetGovernanceLoadResult Load(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var path = ResolvePath(workspaceRoot);
        if (!File.Exists(path)) return new([], true, []);
        try
        {
            return new(AssetGovernanceJsonSerializer.Deserialize(File.ReadAllText(path)), false, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            JsonException or NotSupportedException or ArgumentException)
        {
            return new([], false, [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, path)]);
        }
    }

    public static ProjectSessionSaveResult Save(string workspaceRoot, IReadOnlyList<AssetGovernanceRecord> records)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(records);
        var path = ResolvePath(workspaceRoot);
        try
        {
            var directory = Path.GetDirectoryName(path) ?? throw new IOException("Governance path has no parent.");
            Directory.CreateDirectory(directory);
            var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Path.GetRandomFileName()}.tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                           4096, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
                {
                    writer.Write(AssetGovernanceJsonSerializer.Serialize(records));
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            return new(true, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            JsonException or NotSupportedException or ArgumentException)
        {
            return new(false, [new ProjectSessionDiagnostic(WriteFailedCode, exception.Message, path)]);
        }
    }

    private static string ResolvePath(string workspaceRoot) =>
        Path.Combine(Path.GetFullPath(workspaceRoot), ".forge", "assets", "asset-governance.json");
}

public sealed record AssetGovernanceLoadResult(
    IReadOnlyList<AssetGovernanceRecord> Records,
    bool FileMissing,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
