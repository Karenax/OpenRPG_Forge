namespace Forge.Domain.Assistant;

public sealed record AssistedAreaPlanNode
{
    public Guid Id { get; }
    public int SchemaVersion { get; }
    public AssistedAreaPlanNodeKind Kind { get; }
    public string Label { get; }
    public float X { get; }
    public float Z { get; }
    public float Width { get; }
    public float Depth { get; }
    public IReadOnlyList<Guid> ContentBlueprintIds { get; }
    public IReadOnlyList<string> Tags { get; }

    public AssistedAreaPlanNode(
        Guid id,
        int schemaVersion,
        AssistedAreaPlanNodeKind kind,
        string label,
        float x,
        float z,
        float width,
        float depth,
        IReadOnlyList<Guid>? contentBlueprintIds,
        IReadOnlyList<string>? tags)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Kind = kind;
        Label = (label ?? string.Empty).Trim();
        X = x;
        Z = z;
        Width = width;
        Depth = depth;
        ContentBlueprintIds = (contentBlueprintIds ?? []).Distinct().Order().ToArray();
        Tags = (tags ?? []).Select(item => item.Trim().ToLowerInvariant())
            .Where(item => item.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }
}
