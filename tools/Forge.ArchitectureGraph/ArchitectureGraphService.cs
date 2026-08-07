using System.Diagnostics;

namespace Forge.ArchitectureGraph;

public sealed class ArchitectureGraphService
{
    public const string DefaultOutput = "docs/architecture/graph.json";
    private readonly SourceScanner _scanner = new();
    private readonly RoslynGraphAnalyzer _analyzer = new();

    public (GraphDocument Graph, TimeSpan Elapsed) Generate(string root, string output)
    {
        var timer = Stopwatch.StartNew();
        var paths = ResolveOutputPaths(root, output);
        var sources = _scanner.Discover(root, [paths.GraphPath, paths.OverviewPath]);
        var graph = _analyzer.Analyze(root, sources);
        GraphStore.WriteAtomically(
            (paths.GraphPath, GraphStore.Serialize(graph)),
            (paths.OverviewPath, GraphStore.BuildOverview(graph)));
        timer.Stop();
        return (graph, timer.Elapsed);
    }

    public FreshnessResult Check(string root, string output, bool full)
    {
        var paths = ResolveOutputPaths(root, output);
        if (!File.Exists(paths.GraphPath) || !File.Exists(paths.OverviewPath))
        {
            var missing = new List<string>();
            if (!File.Exists(paths.GraphPath)) missing.Add($"Missing artifact: {DisplayPath(root, paths.GraphPath)}");
            if (!File.Exists(paths.OverviewPath)) missing.Add($"Missing artifact: {DisplayPath(root, paths.OverviewPath)}");
            missing.Add(GenerateInstruction(output));
            return new FreshnessResult(false, missing);
        }

        GraphDocument stored;
        try
        {
            stored = GraphStore.Load(paths.GraphPath);
        }
        catch (Exception exception) when (exception is IOException or System.Text.Json.JsonException)
        {
            return new FreshnessResult(false, [$"Unreadable graph: {exception.Message}", GenerateInstruction(output)]);
        }

        if (stored.Metadata.SchemaVersion != 2 || stored.Metadata.Engine != "roslyn")
        {
            return new FreshnessResult(false, [
                "The stored graph was not produced by the Roslyn schema v2 engine.",
                GenerateInstruction(output),
            ]);
        }

        var sources = _scanner.Discover(root, [paths.GraphPath, paths.OverviewPath]);
        var manifest = SourceScanner.CreateManifest(sources);
        var digest = SourceScanner.ComputeManifestDigest(manifest);
        var messages = CompareManifests(stored.Metadata.Sources, manifest).ToList();
        if (!string.Equals(digest, stored.Metadata.SourceDigest, StringComparison.Ordinal))
        {
            messages.Insert(0, "Source manifest changed.");
        }

        var expectedOverview = GraphStore.BuildOverview(stored);
        if (!File.ReadAllBytes(paths.OverviewPath).AsSpan().SequenceEqual(expectedOverview))
        {
            messages.Add($"Stale artifact: {DisplayPath(root, paths.OverviewPath)}");
        }

        if (messages.Count == 0 && full)
        {
            var rebuilt = _analyzer.Analyze(root, sources);
            if (!File.ReadAllBytes(paths.GraphPath).AsSpan().SequenceEqual(GraphStore.Serialize(rebuilt)))
            {
                messages.Add($"Stale artifact: {DisplayPath(root, paths.GraphPath)}");
            }
        }

        if (messages.Count > 0)
        {
            messages.Add(GenerateInstruction(output));
            return new FreshnessResult(false, messages);
        }

        return new FreshnessResult(
            true,
            [full
                ? "Roslyn architecture graph is fresh and fully deterministic."
                : "Roslyn architecture graph manifest is fresh."]);
    }

    public GraphDocument Load(string root, string output)
    {
        var paths = ResolveOutputPaths(root, output);
        return GraphStore.Load(paths.GraphPath);
    }

    public static (string GraphPath, string OverviewPath) ResolveOutputPaths(
        string root,
        string output)
    {
        var graphPath = Path.IsPathRooted(output)
            ? Path.GetFullPath(output)
            : Path.GetFullPath(Path.Combine(root, output));
        var overviewPath = Path.Combine(
            Path.GetDirectoryName(graphPath) ?? root,
            "overview.mmd");
        return (graphPath, overviewPath);
    }

    private static IEnumerable<string> CompareManifests(
        IReadOnlyList<SourceManifestEntry> previous,
        IReadOnlyList<SourceManifestEntry> current)
    {
        var previousByPath = previous.ToDictionary(item => item.Path, StringComparer.Ordinal);
        var currentByPath = current.ToDictionary(item => item.Path, StringComparer.Ordinal);
        foreach (var path in previousByPath.Keys.Except(currentByPath.Keys, StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal).Take(20))
        {
            yield return $"Removed indexed source: {path}";
        }

        foreach (var path in currentByPath.Keys.Except(previousByPath.Keys, StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal).Take(20))
        {
            yield return $"Added indexed source: {path}";
        }

        foreach (var path in previousByPath.Keys.Intersect(currentByPath.Keys, StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            if (previousByPath[path] != currentByPath[path])
            {
                yield return $"Changed indexed source: {path}";
            }
        }
    }

    private static string GenerateInstruction(string output) =>
        $"Run: dotnet run --project tools/Forge.ArchitectureGraph -- generate --output \"{output}\"";

    private static string DisplayPath(string root, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(path));
        return relative.StartsWith("..", StringComparison.Ordinal)
            ? Path.GetFullPath(path)
            : SourceScanner.NormalizePath(relative);
    }
}
