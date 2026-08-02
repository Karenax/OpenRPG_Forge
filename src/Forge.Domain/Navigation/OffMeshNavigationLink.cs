using Forge.Domain.Spatial;

namespace Forge.Domain.Navigation;

public sealed record OffMeshNavigationLink
{
    public const int CurrentSchemaVersion = 1;

    public OffMeshNavigationLinkId Id { get; }

    public int SchemaVersion { get; }

    public string Name { get; }

    public NavigationLinkKind Kind { get; }

    public Vector3Value Start { get; }

    public Vector3Value End { get; }

    public bool Bidirectional { get; }

    public float MaximumAgentRadius { get; }

    public string Provenance { get; }

    public OffMeshNavigationLink(
        OffMeshNavigationLinkId id,
        string name,
        NavigationLinkKind kind,
        Vector3Value start,
        Vector3Value end,
        bool bidirectional,
        float maximumAgentRadius,
        string provenance,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion),
                $"Off-mesh navigation link schema '{schemaVersion}' is not supported.");
        }

        if (!float.IsFinite(maximumAgentRadius) || maximumAgentRadius is < 0.1f or > 8f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumAgentRadius),
                "Maximum link agent radius must be between 0.1 and 8 metres.");
        }

        if (start == end)
        {
            throw new ArgumentException("An off-mesh navigation link requires distinct endpoints.", nameof(end));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Name = name.Trim();
        Kind = kind;
        Start = start;
        End = end;
        Bidirectional = bidirectional;
        MaximumAgentRadius = maximumAgentRadius;
        Provenance = provenance.Trim();
    }
}
