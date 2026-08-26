namespace Forge.ModuleFormat.Workspaces;

internal static class WorldGenerationWorkspacePath
{
    internal static string Resolve(string workspaceRoot, params string[] relativeSegments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(relativeSegments);
        if (relativeSegments.Length == 0 || relativeSegments.Any(segment =>
                string.IsNullOrWhiteSpace(segment) || segment is "." or ".." ||
                segment.IndexOf('\0') >= 0 || Path.IsPathRooted(segment) ||
                segment.Contains(Path.DirectorySeparatorChar) || segment.Contains(Path.AltDirectorySeparatorChar)))
            throw new ArgumentException("World-generation workspace path segments are invalid.",
                nameof(relativeSegments));

        var workspace = new DirectoryInfo(Path.GetFullPath(workspaceRoot));
        if (!workspace.Exists)
            throw new DirectoryNotFoundException($"World-generation workspace '{workspace.FullName}' does not exist.");
        var resolvedWorkspace = workspace.LinkTarget is null
            ? workspace.FullName
            : workspace.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
        if (string.IsNullOrWhiteSpace(resolvedWorkspace))
            throw new IOException("World-generation workspace link could not be resolved.");
        var root = Path.GetFullPath(resolvedWorkspace);
        var current = root;
        foreach (var segment in relativeSegments)
        {
            current = Path.GetFullPath(Path.Combine(current, segment));
            if (!IsContained(root, current))
                throw new ArgumentException("World-generation path escapes the workspace.", nameof(relativeSegments));
            if ((File.Exists(current) || Directory.Exists(current)) &&
                File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("World-generation paths cannot traverse symbolic links or junctions.");
        }
        return current;
    }

    private static bool IsContained(string root, string candidate)
    {
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                     Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
