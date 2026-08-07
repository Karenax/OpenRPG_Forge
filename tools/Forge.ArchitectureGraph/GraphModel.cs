using System.Text.Json.Serialization;

namespace Forge.ArchitectureGraph;

public sealed record SourceManifestEntry(
    string Path,
    string Sha256,
    long Size);

public sealed record GraphMetadata(
    int SchemaVersion,
    string Engine,
    string EngineVersion,
    string SourceDigest,
    int SourceFileCount,
    IReadOnlyList<SourceManifestEntry> Sources);

public sealed record GraphNode(
    string Id,
    string Kind,
    string Name,
    string? Path = null,
    int? Line = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record GraphEvidence(
    string Path,
    int Line);

public sealed record GraphEdge(
    string Source,
    string Target,
    string Kind,
    string Confidence,
    GraphEvidence Evidence);

public sealed record GraphDiagnostic(
    string Path,
    int Line,
    string Severity,
    string Message);

public sealed record GraphDocument(
    GraphMetadata Metadata,
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges,
    IReadOnlyList<GraphDiagnostic> Diagnostics);

public sealed record SourceDocument(
    string Path,
    byte[] Bytes,
    string Text,
    string Sha256,
    bool HasInvalidUtf8)
{
    public string Suffix => System.IO.Path.GetExtension(Path).ToLowerInvariant();
}

public enum QueryMode
{
    Impact,
    Dependencies,
    Dependents,
    Tests,
}

public enum QueryFormat
{
    Text,
    Paths,
    Json,
    Mermaid,
    Context,
}

public sealed record QueryResult(
    string Query,
    QueryMode Mode,
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges);

public sealed record FreshnessResult(
    bool IsFresh,
    IReadOnlyList<string> Messages);
