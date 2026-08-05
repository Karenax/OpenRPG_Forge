using Forge.Domain.Spatial;

namespace Forge.Domain.Areas;

public sealed record FlightRouteNode
{
    public Guid Id { get; }
    public string Name { get; }
    public Vector3Value Position { get; }
    public float Speed { get; }
    public float BankDegrees { get; }

    public FlightRouteNode(Guid id, string name, Vector3Value position, float speed, float bankDegrees)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (id == Guid.Empty || !float.IsFinite(speed) || speed is <= 0 or > 500 ||
            !float.IsFinite(bankDegrees) || bankDegrees is < -89 or > 89)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Flight route node is invalid.");
        }

        Id = id;
        Name = name.Trim();
        Position = position;
        Speed = speed;
        BankDegrees = bankDegrees;
    }
}
