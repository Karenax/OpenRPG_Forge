namespace Forge.ModuleFormat.Build;

public static class WorkspaceFileMaterializer
{
    public static DeterministicBuildBatch<WorkspaceFileContent> Materialize(
        string workspaceRoot,
        IEnumerable<string> files,
        int maximumParallelism = DeterministicBuildTaskScheduler.DefaultMaximumParallelism)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        ArgumentNullException.ThrowIfNull(files);
        var root = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var paths = files.Select(Path.GetFullPath).ToArray();
        if (paths.Any(path => !path.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Build materialization cannot read outside the workspace.");
        }

        return DeterministicBuildTaskScheduler.Execute(paths, maximumParallelism, path =>
            new WorkspaceFileContent(
                path,
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                File.ReadAllBytes(path)));
    }
}
