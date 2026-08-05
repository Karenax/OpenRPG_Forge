namespace Forge.Domain.Gameplay;

public sealed record ClimbingProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public bool Enabled { get; }
    public bool Required { get; }
    public float ClimbSpeed { get; }
    public float StaminaCapacity { get; }
    public float StaminaDrainPerSecond { get; }
    public float LedgeReach { get; }
    public bool InverseKinematics { get; }
    public bool RequireAuthoredLinks { get; }
    public bool PreventUnsafeDrop { get; }
    public IReadOnlyList<string> SurfaceTags { get; }
    public string ClimbAnimation { get; }
    public string LedgeAnimation { get; }
    public string Provenance { get; }

    public ClimbingProfile(Guid id, int schemaVersion, bool enabled, bool required, float climbSpeed,
        float staminaCapacity, float staminaDrainPerSecond, float ledgeReach, bool inverseKinematics,
        bool requireAuthoredLinks, bool preventUnsafeDrop, IReadOnlyList<string> surfaceTags,
        string climbAnimation, string ledgeAnimation, string provenance)
    {
        ArgumentNullException.ThrowIfNull(surfaceTags);
        ArgumentException.ThrowIfNullOrWhiteSpace(climbAnimation);
        ArgumentException.ThrowIfNullOrWhiteSpace(ledgeAnimation);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var tags = surfaceTags.Select(Normalize).Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || climbSpeed <= 0 ||
            staminaCapacity <= 0 || staminaDrainPerSecond <= 0 || ledgeReach is <= 0 or > 5 ||
            (required && !enabled) || (enabled && requireAuthoredLinks && tags.Length == 0))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Climbing profile is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Enabled = enabled;
        Required = required;
        ClimbSpeed = climbSpeed;
        StaminaCapacity = staminaCapacity;
        StaminaDrainPerSecond = staminaDrainPerSecond;
        LedgeReach = ledgeReach;
        InverseKinematics = inverseKinematics;
        RequireAuthoredLinks = requireAuthoredLinks;
        PreventUnsafeDrop = preventUnsafeDrop;
        SurfaceTags = tags;
        ClimbAnimation = climbAnimation.Trim();
        LedgeAnimation = ledgeAnimation.Trim();
        Provenance = provenance.Trim();
    }

    private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}
