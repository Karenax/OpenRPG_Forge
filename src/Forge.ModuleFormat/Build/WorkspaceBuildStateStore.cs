using System.Text.Json;
using System.Text.Json.Serialization;
using Forge.Domain.Build;

namespace Forge.ModuleFormat.Build;

public static class WorkspaceBuildStateStore
{
    public const string RelativePath = ".forge/build-graph.json";

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static BuildDependencyGraph? TryLoad(string workspacePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        var path = Path.Combine(Path.GetFullPath(workspacePath), RelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<BuildGraphDocument>(File.ReadAllText(path), Options);
            if (document is null)
            {
                return null;
            }

            var nodes = document.Nodes.Select(node => new BuildDependencyNode(
                BuildResourceId.Parse(node.Id),
                node.Path,
                node.Kind,
                node.ContentSha256,
                node.LicenseSpdx,
                node.Provenance,
                node.Dependencies.Select(BuildResourceId.Parse).ToArray())).ToArray();
            return new BuildDependencyGraph(
                BuildResourceId.Parse(document.Id),
                document.SchemaVersion,
                document.Provenance,
                nodes);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or
            ArgumentException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    public static void Save(string workspacePath, BuildDependencyGraph graph)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(graph);
        var path = Path.Combine(Path.GetFullPath(workspacePath), RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The build state path has no parent directory.");
        Directory.CreateDirectory(directory);
        var document = new BuildGraphDocument(
            graph.Id.ToString(),
            graph.SchemaVersion,
            graph.Provenance,
            graph.Nodes.Select(node => new BuildNodeDocument(
                node.Id.ToString(),
                node.Path,
                node.Kind,
                node.ContentSha256,
                node.LicenseSpdx,
                node.Provenance,
                node.Dependencies.Select(dependency => dependency.ToString()).ToArray())).ToArray());
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(document, Options));
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private sealed record BuildGraphDocument(
        string Id,
        int SchemaVersion,
        string Provenance,
        IReadOnlyList<BuildNodeDocument> Nodes);

    private sealed record BuildNodeDocument(
        string Id,
        string Path,
        BuildResourceKind Kind,
        string ContentSha256,
        string LicenseSpdx,
        string Provenance,
        IReadOnlyList<string> Dependencies);
}
