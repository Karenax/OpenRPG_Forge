using Forge.Domain.Spatial;

namespace Forge.Domain.Areas;

public sealed record NavigationVolumeDefinition
{
    public Guid Id { get; }
    public string Name { get; }
    public Vector3Value Center { get; }
    public Vector3Value Size { get; }
    public float CostMultiplier { get; }
    public string Provenance { get; }

    public NavigationVolumeDefinition(Guid id, string name, Vector3Value center, Vector3Value size,
        float costMultiplier, string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || size.X <= 0 || size.Y <= 0 || size.Z <= 0 ||
            !float.IsFinite(costMultiplier) || costMultiplier is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Navigation volume is invalid.");
        }

        Id = id;
        Name = name.Trim();
        Center = center;
        Size = size;
        CostMultiplier = costMultiplier;
        Provenance = provenance.Trim();
    }
}
