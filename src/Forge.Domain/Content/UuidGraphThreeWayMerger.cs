using System.Security.Cryptography;
using System.Text;
using Forge.Domain.Validation;

namespace Forge.Domain.Content;

public sealed record UuidGraphNode(Guid Id, string Kind, IReadOnlyDictionary<string, string> Properties,
    string Provenance);

public sealed record UuidGraphEdge(Guid Id, Guid From, Guid To, string Kind,
    IReadOnlyDictionary<string, string> Properties, string Provenance);

public sealed record UuidGraphDocument(Guid Id, int SchemaVersion, string Provenance,
    IReadOnlyList<UuidGraphNode> Nodes, IReadOnlyList<UuidGraphEdge> Edges);

public sealed record UuidGraphMergeConflict(Guid ConflictId, string ElementKind, Guid ElementId,
    string Summary);

public sealed record UuidGraphMergeResult(UuidGraphDocument Merged,
    IReadOnlyList<UuidGraphMergeConflict> Conflicts);

public static class UuidGraphValidator
{
    public static IReadOnlyList<DomainDiagnostic> Validate(UuidGraphDocument graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var diagnostics = new List<DomainDiagnostic>();
        if (graph.Id == Guid.Empty)
            diagnostics.Add(new("BUILD_GRAPH_UUID_REQUIRED", "Graph UUID is required.", "/build/merge/graph/id"));
        if (graph.SchemaVersion != 1)
            diagnostics.Add(new("BUILD_GRAPH_SCHEMA_UNSUPPORTED", "Only graph schema v1 is supported.", "/build/merge/graph/schemaVersion"));
        if (string.IsNullOrWhiteSpace(graph.Provenance))
            diagnostics.Add(new("BUILD_GRAPH_PROVENANCE_REQUIRED", "Graph provenance is required.", "/build/merge/graph/provenance"));
        var elementIds = graph.Nodes.Select(item => item.Id).Concat(graph.Edges.Select(item => item.Id)).ToArray();
        if (elementIds.Any(id => id == Guid.Empty) || elementIds.Distinct().Count() != elementIds.Length)
            diagnostics.Add(new("BUILD_GRAPH_ELEMENT_UUID_UNIQUE_REQUIRED", "Node and edge UUIDs must be non-empty and unique.", "/build/merge/graph/elements"));
        var nodeIds = graph.Nodes.Select(item => item.Id).ToHashSet();
        foreach (var node in graph.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Kind) || string.IsNullOrWhiteSpace(node.Provenance))
                diagnostics.Add(new("BUILD_GRAPH_NODE_INVALID", "Node kind and provenance are required.", $"/build/merge/graph/nodes/{node.Id}"));
        }
        foreach (var edge in graph.Edges)
        {
            if (!nodeIds.Contains(edge.From) || !nodeIds.Contains(edge.To))
                diagnostics.Add(new("BUILD_GRAPH_EDGE_REFERENCE_UNRESOLVED", "Edge endpoints must resolve to graph nodes.", $"/build/merge/graph/edges/{edge.Id}"));
            if (string.IsNullOrWhiteSpace(edge.Kind) || string.IsNullOrWhiteSpace(edge.Provenance))
                diagnostics.Add(new("BUILD_GRAPH_EDGE_INVALID", "Edge kind and provenance are required.", $"/build/merge/graph/edges/{edge.Id}"));
        }
        return diagnostics;
    }
}

public static class UuidGraphThreeWayMerger
{
    public static UuidGraphMergeResult Merge(UuidGraphDocument @base, UuidGraphDocument ours,
        UuidGraphDocument theirs, ContentMergePolicy policy)
    {
        foreach (var graph in new[] { @base, ours, theirs })
        {
            var diagnostics = UuidGraphValidator.Validate(graph);
            if (diagnostics.Count != 0) throw new ArgumentException($"{diagnostics[0].Code}:{diagnostics[0].Path}");
        }
        if (@base.Id != ours.Id || @base.Id != theirs.Id)
            throw new ArgumentException("BUILD_GRAPH_ID_MISMATCH:/build/merge/graph/id");

        var conflicts = new List<UuidGraphMergeConflict>();
        var theirNodeRemap = new Dictionary<Guid, Guid>();
        var nodes = MergeElements(@base.Nodes, ours.Nodes, theirs.Nodes, item => item.Id,
            (item, id) => item with { Id = id, Provenance = $"preserved-variant:{item.Provenance}" },
            "node", policy, conflicts, theirNodeRemap);
        var edges = MergeElements(@base.Edges, ours.Edges, theirs.Edges, item => item.Id,
            (item, id) => item with { Id = id, Provenance = $"preserved-variant:{item.Provenance}" },
            "edge", policy, conflicts, null);

        if (policy == ContentMergePolicy.PreserveBoth && theirNodeRemap.Count != 0)
        {
            var existingEdges = edges.Select(item => item.Id).ToHashSet();
            foreach (var edge in theirs.Edges.Where(item => theirNodeRemap.ContainsKey(item.From) ||
                         theirNodeRemap.ContainsKey(item.To)))
            {
                var cloneId = DeterministicGuid($"{ours.Id}:edge:{edge.Id}:theirs-remapped");
                if (!existingEdges.Add(cloneId)) continue;
                edges.Add(edge with
                {
                    Id = cloneId,
                    From = theirNodeRemap.GetValueOrDefault(edge.From, edge.From),
                    To = theirNodeRemap.GetValueOrDefault(edge.To, edge.To),
                    Provenance = $"preserved-variant:{edge.Provenance}"
                });
            }
        }

        var merged = new UuidGraphDocument(ours.Id, 1, $"three-way-merge:{policy}",
            nodes.OrderBy(item => item.Id).ToArray(), edges.OrderBy(item => item.Id).ToArray());
        var mergedDiagnostics = UuidGraphValidator.Validate(merged);
        if (mergedDiagnostics.Count != 0)
            throw new InvalidOperationException($"{mergedDiagnostics[0].Code}:{mergedDiagnostics[0].Path}");
        return new(merged, conflicts);
    }

    private static List<T> MergeElements<T>(IReadOnlyList<T> baseItems, IReadOnlyList<T> ourItems,
        IReadOnlyList<T> theirItems, Func<T, Guid> id, Func<T, Guid, T> clone, string kind,
        ContentMergePolicy policy, ICollection<UuidGraphMergeConflict> conflicts,
        IDictionary<Guid, Guid>? theirRemap) where T : class
    {
        var bases = baseItems.ToDictionary(id);
        var ours = ourItems.ToDictionary(id);
        var theirs = theirItems.ToDictionary(id);
        var merged = new List<T>();
        foreach (var elementId in bases.Keys.Concat(ours.Keys).Concat(theirs.Keys).Distinct().Order())
        {
            bases.TryGetValue(elementId, out var baseItem);
            ours.TryGetValue(elementId, out var ourItem);
            theirs.TryGetValue(elementId, out var theirItem);
            var oursChanged = !Equivalent(baseItem, ourItem);
            var theirsChanged = !Equivalent(baseItem, theirItem);
            if (!oursChanged && !theirsChanged) Add(merged, baseItem);
            else if (oursChanged && !theirsChanged) Add(merged, ourItem);
            else if (!oursChanged && theirsChanged) Add(merged, theirItem);
            else if (Equivalent(ourItem, theirItem)) Add(merged, ourItem);
            else
            {
                conflicts.Add(new(DeterministicGuid($"{kind}:{elementId}:conflict"), kind, elementId,
                    $"{kind} '{elementId}' changed differently in both variants."));
                if (policy == ContentMergePolicy.PreferTheirs) Add(merged, theirItem);
                else
                {
                    Add(merged, ourItem);
                    if (policy == ContentMergePolicy.PreserveBoth && theirItem is not null)
                    {
                        var cloneId = DeterministicGuid($"{kind}:{elementId}:theirs");
                        merged.Add(clone(theirItem, cloneId));
                        theirRemap?.Add(elementId, cloneId);
                    }
                }
            }
        }
        return merged;
    }

    private static void Add<T>(ICollection<T> items, T? item) where T : class
    {
        if (item is not null) items.Add(item);
    }

    private static bool Equivalent<T>(T? left, T? right) where T : class
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return (left, right) switch
        {
            (UuidGraphNode a, UuidGraphNode b) => a.Id == b.Id && a.Kind == b.Kind &&
                a.Provenance == b.Provenance && PropertiesEqual(a.Properties, b.Properties),
            (UuidGraphEdge a, UuidGraphEdge b) => a.Id == b.Id && a.From == b.From && a.To == b.To &&
                a.Kind == b.Kind && a.Provenance == b.Provenance && PropertiesEqual(a.Properties, b.Properties),
            _ => Equals(left, right)
        };
    }

    private static bool PropertiesEqual(IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right) => left.Count == right.Count &&
        left.All(pair => right.TryGetValue(pair.Key, out var value) &&
            string.Equals(pair.Value, value, StringComparison.Ordinal));

    private static Guid DeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guid = stackalloc byte[16];
        bytes.AsSpan(0, guid.Length).CopyTo(guid);
        guid[6] = (byte)((guid[6] & 0x0f) | 0x40);
        guid[8] = (byte)((guid[8] & 0x3f) | 0x80);
        return new Guid(guid);
    }
}
