namespace Forge.Domain.Validation;

public sealed record ValidationGraphNode(Guid Id, string Type, string Path);

public sealed record ValidationGraphEdge(Guid Id, Guid SourceId, Guid TargetId, string Type, string Path);

public sealed record ValidationGraph(
    Guid Id,
    int SchemaVersion,
    string Provenance,
    IReadOnlyList<ValidationGraphNode> Nodes,
    IReadOnlyList<ValidationGraphEdge> Edges,
    IReadOnlyList<Guid> EntryNodeIds,
    bool AllowCycles);

public static class GenericGraphValidator
{
    public const string IdentityInvalidCode = "qa.graph.identity_invalid";
    public const string NodeDuplicateCode = "qa.graph.node_duplicate";
    public const string EdgeDuplicateCode = "qa.graph.edge_duplicate";
    public const string ReferenceInvalidCode = "qa.graph.reference_invalid";
    public const string UnreachableCode = "qa.graph.node_unreachable";
    public const string CycleCode = "qa.graph.cycle_forbidden";

    public static IReadOnlyList<DomainDiagnostic> Validate(ValidationGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var diagnostics = new List<DomainDiagnostic>();
        if (graph.Id == Guid.Empty || graph.SchemaVersion != 1 || string.IsNullOrWhiteSpace(graph.Provenance))
            diagnostics.Add(new(IdentityInvalidCode, "Validation graph requires UUID, schema v1 and provenance.",
                $"graphs/{graph.Id}/identity"));
        var nodeIds = graph.Nodes.Select(node => node.Id).ToArray();
        if (nodeIds.Any(id => id == Guid.Empty) || nodeIds.Distinct().Count() != nodeIds.Length)
            diagnostics.Add(new(NodeDuplicateCode, "Graph node UUIDs must be non-empty and unique.",
                $"graphs/{graph.Id}/nodes"));
        var edgeIds = graph.Edges.Select(edge => edge.Id).ToArray();
        if (edgeIds.Any(id => id == Guid.Empty) || edgeIds.Distinct().Count() != edgeIds.Length)
            diagnostics.Add(new(EdgeDuplicateCode, "Graph edge UUIDs must be non-empty and unique.",
                $"graphs/{graph.Id}/edges"));
        var known = nodeIds.ToHashSet();
        foreach (var edge in graph.Edges.OrderBy(item => item.Path, StringComparer.Ordinal))
            if (!known.Contains(edge.SourceId) || !known.Contains(edge.TargetId) || string.IsNullOrWhiteSpace(edge.Type))
                diagnostics.Add(new(ReferenceInvalidCode, "Graph edge has a missing endpoint or type.", edge.Path));
        foreach (var entry in graph.EntryNodeIds)
            if (!known.Contains(entry))
                diagnostics.Add(new(ReferenceInvalidCode, "Graph entry node does not exist.",
                    $"graphs/{graph.Id}/entries/{entry}"));
        if (diagnostics.Any(item => item.Code is NodeDuplicateCode or ReferenceInvalidCode)) return diagnostics;

        var outgoing = graph.Edges.GroupBy(edge => edge.SourceId)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.TargetId).Distinct().Order().ToArray());
        var reachable = new HashSet<Guid>();
        var queue = new Queue<Guid>(graph.EntryNodeIds.Order());
        while (queue.TryDequeue(out var node))
            if (reachable.Add(node) && outgoing.TryGetValue(node, out var targets))
                foreach (var target in targets) queue.Enqueue(target);
        foreach (var node in graph.Nodes.Where(node => !reachable.Contains(node.Id)).OrderBy(node => node.Path, StringComparer.Ordinal))
            diagnostics.Add(new(UnreachableCode, $"Graph node '{node.Id}' is unreachable.", node.Path));
        if (!graph.AllowCycles && ContainsCycle(graph.Nodes.Select(node => node.Id), outgoing))
            diagnostics.Add(new(CycleCode, "Graph contains a forbidden cycle.", $"graphs/{graph.Id}/edges"));
        return diagnostics;
    }

    private static bool ContainsCycle(IEnumerable<Guid> nodes, IReadOnlyDictionary<Guid, Guid[]> outgoing)
    {
        var visiting = new HashSet<Guid>();
        var visited = new HashSet<Guid>();
        bool Visit(Guid node)
        {
            if (visiting.Contains(node)) return true;
            if (!visited.Add(node)) return false;
            visiting.Add(node);
            if (outgoing.TryGetValue(node, out var targets) && targets.Any(Visit)) return true;
            visiting.Remove(node);
            return false;
        }
        return nodes.Order().Any(Visit);
    }
}
