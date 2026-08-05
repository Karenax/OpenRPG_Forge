namespace Forge.Domain.Gameplay;

public sealed record SwimmingProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public bool Enabled { get; }
    public bool Required { get; }
    public float Buoyancy { get; }
    public float SurfaceSnapDistance { get; }
    public float SurfaceSpeed { get; }
    public float DiveSpeed { get; }
    public float CurrentInfluence { get; }
    public float BreathSeconds { get; }
    public IReadOnlyList<string> WaterVolumeTags { get; }
    public string SurfaceAnimation { get; }
    public string DiveAnimation { get; }
    public string IdleAnimation { get; }
    public string Provenance { get; }

    public SwimmingProfile(Guid id, int schemaVersion, bool enabled, bool required, float buoyancy,
        float surfaceSnapDistance, float surfaceSpeed, float diveSpeed, float currentInfluence,
        float breathSeconds, IReadOnlyList<string> waterVolumeTags, string surfaceAnimation,
        string diveAnimation, string idleAnimation, string provenance)
    {
        ArgumentNullException.ThrowIfNull(waterVolumeTags);
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceAnimation);
        ArgumentException.ThrowIfNullOrWhiteSpace(diveAnimation);
        ArgumentException.ThrowIfNullOrWhiteSpace(idleAnimation);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var tags = waterVolumeTags.Select(Normalize).Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || buoyancy is < 0 or > 50 ||
            surfaceSnapDistance is < 0 or > 10 || surfaceSpeed <= 0 || diveSpeed <= 0 ||
            currentInfluence is < 0 or > 5 || breathSeconds <= 0 || (required && !enabled))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Swimming profile is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Enabled = enabled;
        Required = required;
        Buoyancy = buoyancy;
        SurfaceSnapDistance = surfaceSnapDistance;
        SurfaceSpeed = surfaceSpeed;
        DiveSpeed = diveSpeed;
        CurrentInfluence = currentInfluence;
        BreathSeconds = breathSeconds;
        WaterVolumeTags = tags;
        SurfaceAnimation = surfaceAnimation.Trim();
        DiveAnimation = diveAnimation.Trim();
        IdleAnimation = idleAnimation.Trim();
        Provenance = provenance.Trim();
    }

    private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}
