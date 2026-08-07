namespace Forge.ArchitectureGraph;

public sealed class GraphBuilder
{
    private readonly Dictionary<string, GraphNode> _nodes = new(StringComparer.Ordinal);
    private readonly Dictionary<EdgeKey, GraphEdge> _edges = [];
    private readonly Dictionary<DiagnosticKey, GraphDiagnostic> _diagnostics = [];

    public IReadOnlyDictionary<string, GraphNode> Nodes => _nodes;

    public void AddNode(GraphNode node)
    {
        if (!_nodes.TryAdd(node.Id, node) && _nodes[node.Id] != node)
        {
            AddDiagnostic(new GraphDiagnostic(
                node.Path ?? string.Empty,
                node.Line ?? 1,
                "warning",
                $"Conflicting node identifier ignored: {node.Id}"));
        }
    }

    public void AddEdge(
        string source,
        string target,
        string kind,
        string confidence,
        string evidencePath,
        int evidenceLine)
    {
        if (source == target || !_nodes.ContainsKey(source) || !_nodes.ContainsKey(target))
        {
            return;
        }

        var key = new EdgeKey(source, target, kind, evidencePath);
        var edge = new GraphEdge(
            source,
            target,
            kind,
            confidence,
            new GraphEvidence(evidencePath, evidenceLine));

        if (!_edges.TryGetValue(key, out var existing) || evidenceLine < existing.Evidence.Line)
        {
            _edges[key] = edge;
        }
    }

    public void AddDiagnostic(GraphDiagnostic diagnostic)
    {
        _diagnostics.TryAdd(
            new DiagnosticKey(
                diagnostic.Path,
                diagnostic.Line,
                diagnostic.Severity,
                diagnostic.Message),
            diagnostic);
    }

    public GraphDocument Build(GraphMetadata metadata)
    {
        return new GraphDocument(
            metadata,
            _nodes.Values.OrderBy(node => node.Id, StringComparer.Ordinal).ToArray(),
            _edges.Values
                .OrderBy(edge => edge.Source, StringComparer.Ordinal)
                .ThenBy(edge => edge.Target, StringComparer.Ordinal)
                .ThenBy(edge => edge.Kind, StringComparer.Ordinal)
                .ThenBy(edge => edge.Evidence.Path, StringComparer.Ordinal)
                .ThenBy(edge => edge.Evidence.Line)
                .ToArray(),
            _diagnostics.Values
                .OrderBy(item => item.Path, StringComparer.Ordinal)
                .ThenBy(item => item.Line)
                .ThenBy(item => item.Severity, StringComparer.Ordinal)
                .ThenBy(item => item.Message, StringComparer.Ordinal)
                .ToArray());
    }

    private sealed record EdgeKey(
        string Source,
        string Target,
        string Kind,
        string EvidencePath);

    private sealed record DiagnosticKey(
        string Path,
        int Line,
        string Severity,
        string Message);
}
