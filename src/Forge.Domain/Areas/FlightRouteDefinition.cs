namespace Forge.Domain.Areas;

public sealed record FlightRouteDefinition
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public string Name { get; }
    public IReadOnlyList<FlightRouteNode> Nodes { get; }
    public FlightRouteCurve Curve { get; }
    public FlightObstaclePolicy ObstaclePolicy { get; }
    public bool Closed { get; }
    public bool DebugVisible { get; }
    public string Provenance { get; }

    public FlightRouteDefinition(Guid id, int schemaVersion, string name, IReadOnlyList<FlightRouteNode> nodes,
        FlightRouteCurve curve, FlightObstaclePolicy obstaclePolicy, bool closed, bool debugVisible, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || nodes.Count < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Flight route is invalid.");
        }

        var nodeValues = nodes.ToArray();
        if (nodeValues.Select(node => node.Id).Distinct().Count() != nodeValues.Length)
        {
            throw new ArgumentException("Flight route node IDs must be unique.", nameof(nodes));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Name = name.Trim();
        Nodes = nodeValues;
        Curve = curve;
        ObstaclePolicy = obstaclePolicy;
        Closed = closed;
        DebugVisible = debugVisible;
        Provenance = provenance.Trim();
    }
}
