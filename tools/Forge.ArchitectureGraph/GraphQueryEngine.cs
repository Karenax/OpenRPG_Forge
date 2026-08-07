using System.Text;
using System.Text.Json;

namespace Forge.ArchitectureGraph;

public sealed class GraphQueryEngine
{
    public QueryResult Query(
        GraphDocument graph,
        string query,
        QueryMode mode,
        int depth,
        int maxNodes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(depth, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxNodes, 1);

        var tokens = query
            .Split([' ', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.ToUpperInvariant())
            .ToArray();
        var scored = graph.Nodes
            .Select(node => (Node: node, Score: Score(node, query, tokens)))
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Node.Id, StringComparer.Ordinal)
            .Take(maxNodes)
            .ToArray();
        if (scored.Length == 0)
        {
            return new QueryResult(query, mode, [], []);
        }

        var outgoing = graph.Edges
            .GroupBy(edge => edge.Source, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(EdgePriority).ThenBy(edge => edge.Target, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var incoming = graph.Edges
            .GroupBy(edge => edge.Target, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(EdgePriority).ThenBy(edge => edge.Source, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        var selected = scored.Select(item => item.Node.Id).ToHashSet(StringComparer.Ordinal);
        var queue = new Queue<(string NodeId, int Depth)>();
        foreach (var seed in selected.Order(StringComparer.Ordinal)) queue.Enqueue((seed, 0));
        while (queue.Count > 0 && selected.Count < maxNodes)
        {
            var (nodeId, currentDepth) = queue.Dequeue();
            if (currentDepth >= depth) continue;
            foreach (var edge in RelevantEdges(nodeId, mode, outgoing, incoming))
            {
                var neighbor = edge.Source == nodeId ? edge.Target : edge.Source;
                if (selected.Add(neighbor))
                {
                    queue.Enqueue((neighbor, currentDepth + 1));
                    if (selected.Count >= maxNodes) break;
                }
            }
        }

        var nodes = graph.Nodes.Where(node => selected.Contains(node.Id))
            .OrderBy(node => node.Id, StringComparer.Ordinal).ToArray();
        var edges = graph.Edges
            .Where(edge => selected.Contains(edge.Source) && selected.Contains(edge.Target))
            .OrderBy(edge => edge.Source, StringComparer.Ordinal)
            .ThenBy(edge => edge.Target, StringComparer.Ordinal)
            .ThenBy(edge => edge.Kind, StringComparer.Ordinal)
            .ToArray();
        return new QueryResult(query, mode, nodes, edges);
    }

    public string Format(QueryResult result, QueryFormat format)
    {
        return format switch
        {
            QueryFormat.Paths => FormatPaths(result),
            QueryFormat.Json => JsonSerializer.Serialize(result, GraphStore.JsonOptions),
            QueryFormat.Mermaid => FormatMermaid(result),
            QueryFormat.Context => FormatContext(result),
            _ => FormatText(result),
        };
    }

    private static IEnumerable<GraphEdge> RelevantEdges(
        string nodeId,
        QueryMode mode,
        IReadOnlyDictionary<string, GraphEdge[]> outgoing,
        IReadOnlyDictionary<string, GraphEdge[]> incoming)
    {
        var dependencies = (outgoing.GetValueOrDefault(nodeId) ?? [])
            .Where(IsContextRelation)
            .ToArray();
        var dependents = (incoming.GetValueOrDefault(nodeId) ?? [])
            .Where(IsContextRelation)
            .ToArray();
        return mode switch
        {
            QueryMode.Dependencies => dependencies,
            QueryMode.Dependents => dependents,
            QueryMode.Tests => dependencies.Concat(dependents)
                .Where(edge => edge.Kind == "tests"),
            _ => dependencies.Concat(dependents)
                .OrderBy(EdgePriority)
                .ThenBy(edge => edge.Source, StringComparer.Ordinal)
                .ThenBy(edge => edge.Target, StringComparer.Ordinal),
        };
    }

    private static bool IsContextRelation(GraphEdge edge) =>
        edge.Kind is not "belongs_to" and not "contains" and not "defined_in";

    private static int Score(GraphNode node, string query, IReadOnlyList<string> tokens)
    {
        var upperQuery = query.ToUpperInvariant();
        var fields = string.Join(' ',
            node.Id,
            node.Name,
            node.Path ?? string.Empty,
            node.Metadata is null
                ? string.Empty
                : string.Join(' ', node.Metadata.Select(pair => $"{pair.Key} {pair.Value}")));
        var haystack = fields.ToUpperInvariant();
        if (tokens.Count == 0 || tokens.Any(token => !haystack.Contains(token, StringComparison.Ordinal)))
        {
            return 0;
        }

        var score = 10;
        if (node.Id.Equals(query, StringComparison.OrdinalIgnoreCase)) score += 120;
        if (node.Name.Equals(query, StringComparison.OrdinalIgnoreCase)) score += 100;
        if (node.Name.Contains(upperQuery, StringComparison.OrdinalIgnoreCase)) score += 40;
        if (node.Path?.Contains(upperQuery, StringComparison.OrdinalIgnoreCase) == true) score += 20;
        return score;
    }

    private static int EdgePriority(GraphEdge edge) => edge.Kind switch
    {
        "tests" => 0,
        "implements" or "inherits" => 1,
        "calls_route" or "handled_by" => 2,
        "uses_service" or "uses_repository" or "accesses_model" => 3,
        "imports" => 4,
        "references" => 8,
        _ => 5,
    };

    private static string FormatPaths(QueryResult result)
    {
        return string.Join('\n', result.Nodes
            .Select(node => node.Path)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Concat(result.Edges.Select(edge => edge.Evidence.Path))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal));
    }

    private static string FormatText(QueryResult result)
    {
        if (result.Nodes.Count == 0)
        {
            return $"No result for '{result.Query}'.\nNodes: 0; relations: 0.";
        }

        var lines = new List<string>
        {
            $"Query: {result.Query}",
            $"Mode: {result.Mode}",
            $"Nodes: {result.Nodes.Count}; relations: {result.Edges.Count}",
        };
        lines.AddRange(result.Nodes.Select(node =>
            $"[{node.Kind}] {node.Name}"
            + (node.Path is null ? string.Empty : $" — {node.Path}:{node.Line ?? 1}")));
        lines.AddRange(result.Edges.Select(edge =>
            $"{edge.Source} -{edge.Kind}/{edge.Confidence}-> {edge.Target} "
            + $"({edge.Evidence.Path}:{edge.Evidence.Line})"));
        return string.Join('\n', lines);
    }

    private static string FormatContext(QueryResult result)
    {
        if (result.Nodes.Count == 0)
        {
            return $"FOCUS: {result.Query}\nSTATUS: no matching architecture evidence";
        }

        var nodeById = result.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var paths = result.Nodes
            .Where(node => node.Path is not null)
            .GroupBy(node => node.Path!, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);
        var builder = new StringBuilder();
        builder.AppendLine($"FOCUS: {result.Query}");
        builder.AppendLine($"MODE: {result.Mode}");
        builder.AppendLine("FILES:");
        foreach (var path in paths)
        {
            builder.Append("- ").Append(path.Key).Append(": ")
                .AppendLine(string.Join(", ", path.Select(node => $"{node.Kind}:{node.Name}")));
        }

        builder.AppendLine("RELATIONS:");
        foreach (var edge in result.Edges)
        {
            var source = nodeById.GetValueOrDefault(edge.Source)?.Name ?? edge.Source;
            var target = nodeById.GetValueOrDefault(edge.Target)?.Name ?? edge.Target;
            builder.Append("- ").Append(source).Append(" -> ").Append(target)
                .Append(" [").Append(edge.Kind).Append(", ").Append(edge.Confidence).Append("] ")
                .Append(edge.Evidence.Path).Append(':').AppendLine(edge.Evidence.Line.ToString());
        }

        var tests = result.Nodes.Where(node => node.Kind == "test").ToArray();
        builder.AppendLine("TESTS:");
        if (tests.Length == 0)
        {
            builder.AppendLine("- none found in bounded subgraph");
        }
        else
        {
            foreach (var test in tests)
            {
                builder.Append("- ").Append(test.Name).Append(" — ").AppendLine(test.Path);
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatMermaid(QueryResult result)
    {
        var lines = new List<string> { "flowchart LR" };
        if (result.Nodes.Count == 0)
        {
            lines.Add("  empty[\"No result\"]");
            return string.Join('\n', lines);
        }

        foreach (var node in result.Nodes)
        {
            lines.Add($"  {GraphStore.MermaidId(node.Id)}[\"{GraphStore.EscapeMermaid($"{node.Kind}: {node.Name}")}\"]");
        }

        foreach (var edge in result.Edges)
        {
            lines.Add(
                $"  {GraphStore.MermaidId(edge.Source)} -->|\"{GraphStore.EscapeMermaid(edge.Kind)}\"| "
                + GraphStore.MermaidId(edge.Target));
        }

        return string.Join('\n', lines);
    }
}
