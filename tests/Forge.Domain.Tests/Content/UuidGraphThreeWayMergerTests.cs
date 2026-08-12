using Forge.Domain.Content;

namespace Forge.Domain.Tests.Content;

public sealed class UuidGraphThreeWayMergerTests
{
    private static readonly Guid GraphId = Guid.Parse("a1060000-0000-4000-8000-000000000001");
    private static readonly Guid NodeId = Guid.Parse("a1060000-0000-4000-8000-000000000002");
    private static readonly Guid AnchorId = Guid.Parse("a1060000-0000-4000-8000-000000000003");
    private static readonly Guid EdgeId = Guid.Parse("a1060000-0000-4000-8000-000000000004");

    [Fact]
    public void Preserve_both_keeps_conflicting_graph_variants_and_remaps_their_edges()
    {
        var first = UuidGraphThreeWayMerger.Merge(Graph("base"), Graph("ours"), Graph("theirs"),
            ContentMergePolicy.PreserveBoth);
        var second = UuidGraphThreeWayMerger.Merge(Graph("base"), Graph("ours"), Graph("theirs"),
            ContentMergePolicy.PreserveBoth);

        Assert.Single(first.Conflicts);
        Assert.Equal(3, first.Merged.Nodes.Count);
        Assert.Equal(2, first.Merged.Edges.Count);
        Assert.Contains(first.Merged.Nodes, node => node.Properties["name"] == "ours");
        var preserved = Assert.Single(first.Merged.Nodes, node => node.Properties["name"] == "theirs");
        Assert.Contains(first.Merged.Edges, edge => edge.From == preserved.Id);
        Assert.Equal(first.Merged.Nodes.Select(node => node.Id), second.Merged.Nodes.Select(node => node.Id));
        Assert.Equal(first.Merged.Edges.Select(edge => edge.Id), second.Merged.Edges.Select(edge => edge.Id));
    }

    [Fact]
    public void Validator_reports_stable_localized_diagnostics_for_unresolved_edges()
    {
        var graph = Graph("base") with
        {
            Edges = [new(EdgeId, Guid.NewGuid(), AnchorId, "next", new Dictionary<string, string>(), "test")]
        };

        var diagnostic = Assert.Single(UuidGraphValidator.Validate(graph));

        Assert.Equal("BUILD_GRAPH_EDGE_REFERENCE_UNRESOLVED", diagnostic.Code);
        Assert.Equal($"/build/merge/graph/edges/{EdgeId}", diagnostic.Path);
    }

    private static UuidGraphDocument Graph(string value) => new(GraphId, 1, $"test:{value}",
        [
            new(NodeId, "dialogue-node", new Dictionary<string, string> { ["name"] = value }, value),
            new(AnchorId, "dialogue-node", new Dictionary<string, string> { ["name"] = "anchor" }, "base")
        ],
        [new(EdgeId, NodeId, AnchorId, "next", new Dictionary<string, string>(), "base")]);
}
