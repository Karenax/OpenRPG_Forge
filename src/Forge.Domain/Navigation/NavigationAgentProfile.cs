namespace Forge.Domain.Navigation;

public sealed record NavigationAgentProfile
{
    public const int CurrentSchemaVersion = 1;

    public NavigationAgentProfileId Id { get; }

    public int SchemaVersion { get; }

    public string Name { get; }

    public float Radius { get; }

    public float Height { get; }

    public bool CanOpenDoors { get; }

    public bool CanUseOffMeshLinks { get; }

    public bool CanFly { get; }

    public string Provenance { get; }

    public NavigationAgentProfile(
        NavigationAgentProfileId id,
        string name,
        float radius,
        float height,
        bool canOpenDoors,
        bool canUseOffMeshLinks,
        bool canFly,
        string provenance,
        int schemaVersion = CurrentSchemaVersion)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion),
                $"Navigation agent schema '{schemaVersion}' is not supported.");
        }

        if (!float.IsFinite(radius) || radius is < 0.1f or > 8f)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "Agent radius must be between 0.1 and 8 metres.");
        }

        if (!float.IsFinite(height) || height is < 0.2f or > 16f)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Agent height must be between 0.2 and 16 metres.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Name = name.Trim();
        Radius = radius;
        Height = height;
        CanOpenDoors = canOpenDoors;
        CanUseOffMeshLinks = canUseOffMeshLinks;
        CanFly = canFly;
        Provenance = provenance.Trim();
    }
}
