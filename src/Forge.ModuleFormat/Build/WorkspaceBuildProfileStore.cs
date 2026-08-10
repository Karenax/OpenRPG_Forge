using System.Text;
using System.Text.Json;
using Forge.Domain.Build;
using Forge.ModuleFormat.Workspaces;

namespace Forge.ModuleFormat.Build;

public static class WorkspaceBuildProfileStore
{
    public const string RelativePath = "build.profile.json";
    public const string ReadFailedCode = "build.profile.read_failed";
    public const string WriteFailedCode = "build.profile.write_failed";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
    };

    public static WorkspaceBuildProfileLoadResult Load(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var path = ResolvePath(workspaceRoot);
        if (!File.Exists(path))
            return new(WorkspaceBuildProfile.CreateDefault(), true, []);
        try
        {
            var profile = JsonSerializer.Deserialize<WorkspaceBuildProfile>(File.ReadAllText(path), Options)
                ?? throw new JsonException("Build profile document is empty.");
            var diagnostics = WorkspaceBuildProfileValidator.Validate(profile);
            return diagnostics.Count == 0
                ? new(profile, false, [])
                : new(profile, false, diagnostics.Select(item =>
                    new ProjectSessionDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return new(WorkspaceBuildProfile.CreateDefault(), false,
                [new ProjectSessionDiagnostic(ReadFailedCode, exception.Message, path)]);
        }
    }

    public static ProjectSessionSaveResult Save(string workspaceRoot, WorkspaceBuildProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(profile);
        var path = ResolvePath(workspaceRoot);
        var diagnostics = WorkspaceBuildProfileValidator.Validate(profile);
        if (diagnostics.Count > 0)
            return new(false, diagnostics.Select(item =>
                new ProjectSessionDiagnostic(item.Code, item.Message, item.Path)).ToArray());
        try
        {
            var directory = Path.GetDirectoryName(path)
                ?? throw new IOException("Build profile path has no parent directory.");
            Directory.CreateDirectory(directory);
            var content = JsonSerializer.Serialize(profile, Options) + Environment.NewLine;
            var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Path.GetRandomFileName()}.tmp");
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                           FileShare.None, 4096, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
                {
                    writer.Write(content);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            return new(true, []);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or ArgumentException)
        {
            return new(false, [new ProjectSessionDiagnostic(WriteFailedCode, exception.Message, path)]);
        }
    }

    private static string ResolvePath(string workspaceRoot) =>
        Path.Combine(Path.GetFullPath(workspaceRoot), RelativePath);
}

public sealed record WorkspaceBuildProfileLoadResult(
    WorkspaceBuildProfile Profile,
    bool FileMissing,
    IReadOnlyList<ProjectSessionDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}
