namespace Forge.Domain.Assistant;

public sealed record AssistedAreaPlanLink(
    Guid Id,
    int SchemaVersion,
    Guid FromNodeId,
    Guid ToNodeId,
    string Traversal,
    bool Required);
