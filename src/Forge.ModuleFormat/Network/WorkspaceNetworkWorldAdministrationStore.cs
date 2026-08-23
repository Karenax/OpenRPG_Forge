using System.Text;
using System.Text.Json;
using Forge.ModuleFormat.Workspaces;
using Forge.Network.Authoring;

namespace Forge.ModuleFormat.Network;

public static class WorkspaceNetworkWorldAdministrationStore
{
    public const string RelativePath = ".forge/network/world-administration.json";
    public const string ReadFailedCode = "network.world_administration.read_failed";
    public const string WriteFailedCode = "network.world_administration.write_failed";

    public static WorkspaceNetworkWorldAdministrationLoadResult Load(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var path = ResolvePath(workspaceRoot);
        if (!File.Exists(path))
            return new(NetworkWorldAdministrationProfile.CreateDefault("workspace:network-world-administration"),
                true, []);
        try
        {
            var profile = new NetworkWorldAdministrationStore().Deserialize(
                File.ReadAllText(path, Encoding.UTF8));
            return new(profile, false, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               InvalidDataException or JsonException or NotSupportedException or
                                               ArgumentException)
        {
            return new(NetworkWorldAdministrationProfile.CreateDefault("workspace:network-world-administration"),
                false, [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, path)]);
        }
    }

    public static ProjectSessionSaveResult Save(string workspaceRoot,
        NetworkWorldAdministrationProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(profile);
        var path = ResolvePath(workspaceRoot);
        try
        {
            var directory = Path.GetDirectoryName(path)
                ?? throw new IOException("Network world administration path has no parent directory.");
            Directory.CreateDirectory(directory);
            WriteAtomically(path, new NetworkWorldAdministrationStore().Serialize(profile));
            return new(true, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                               InvalidDataException or JsonException or NotSupportedException or
                                               ArgumentException)
        {
            return new(false, [new ProjectSessionDiagnostic(WriteFailedCode, exception.Message, path)]);
        }
    }

    private static string ResolvePath(string workspaceRoot)
    {
        var root = Path.GetFullPath(workspaceRoot);
        return Path.GetFullPath(Path.Combine(root,
            RelativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static void WriteAtomically(string targetPath, string content)
    {
        var directory = Path.GetDirectoryName(targetPath)!;
        var temporary = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Path.GetRandomFileName()}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
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

public sealed record WorkspaceNetworkWorldAdministrationLoadResult(
    NetworkWorldAdministrationProfile Profile,
    bool FileMissing,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
