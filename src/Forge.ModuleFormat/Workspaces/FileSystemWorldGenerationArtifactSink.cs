using Forge.Application.WorldGeneration;

namespace Forge.ModuleFormat.Workspaces;

public sealed class FileSystemWorldGenerationArtifactSink : IWorldGenerationArtifactSink
{
    public const string RelativeRoot = "world/generation/artifacts";
    private readonly string _root;

    public FileSystemWorldGenerationArtifactSink(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        _root = Path.GetFullPath(Path.Combine(workspaceRoot, "world", "generation", "artifacts"));
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
        if (Path.IsPathRooted(relativePath))
            throw new ArgumentException("World-generation artifact path must be relative.", nameof(relativePath));
        var resolved = Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("World-generation artifact path escapes the workspace artifact root.", nameof(relativePath));
        return resolved;
    }
}
