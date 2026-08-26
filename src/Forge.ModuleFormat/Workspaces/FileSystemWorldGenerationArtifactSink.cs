using Forge.Application.WorldGeneration;

namespace Forge.ModuleFormat.Workspaces;

public sealed class FileSystemWorldGenerationArtifactSink : IWorldGenerationArtifactSink
{
    public const string RelativeRoot = "world/generation/artifacts";
    private readonly string _root;

    public FileSystemWorldGenerationArtifactSink(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _root = WorldGenerationWorkspacePath.Resolve(workspaceRoot, "world", "generation", "artifacts");
    }

    public ValueTask WriteTextAsync(
        string relativePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        var path = Resolve(relativePath);
        AtomicWorkspaceFile.Write(path, content);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var path = Resolve(relativePath);
        if (File.Exists(path)) File.Delete(path);
        return ValueTask.CompletedTask;
    }

    private string Resolve(string relativePath)
    {
        if (Path.IsPathRooted(relativePath) || relativePath.IndexOf('\0') >= 0)
            throw new ArgumentException("World-generation artifact path must be relative.", nameof(relativePath));
        var segments = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
            throw new ArgumentException("World-generation artifact path contains traversal.", nameof(relativePath));
        var resolved = Path.GetFullPath(Path.Combine(_root, Path.Combine(segments)));
        var prefix = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("World-generation artifact path escapes the workspace artifact root.", nameof(relativePath));
        var current = _root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current)) continue;
            if (File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new ArgumentException(
                    "World-generation artifact paths cannot traverse symbolic links or junctions.",
                    nameof(relativePath));
        }
        return resolved;
    }
}
