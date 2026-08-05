using Forge.Domain.Spatial;

namespace Forge.Domain.RuntimePolicies;

public sealed record RestPolicyVolume
{
    public Guid Id { get; }
    public string Name { get; }
    public Vector3Value Center { get; }
    public Vector3Value Size { get; }
    public RestPermissionMode Mode { get; }
    public string DenialReason { get; }
    public string Condition { get; }
    public int CooldownSeconds { get; }
    public float RequiredSecureRadius { get; }
    public int Priority { get; }
    public string Provenance { get; }

    public RestPolicyVolume(Guid id, string name, Vector3Value center, Vector3Value size, RestPermissionMode mode,
        string denialReason, string condition, int cooldownSeconds, float requiredSecureRadius, int priority,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(denialReason);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || size.X <= 0 || size.Y <= 0 || size.Z <= 0 ||
            cooldownSeconds is < 0 or > 86_400 || !float.IsFinite(requiredSecureRadius) ||
            requiredSecureRadius is < 0 or > 512 || priority is < -10_000 or > 10_000 ||
            mode != RestPermissionMode.Allowed && string.IsNullOrWhiteSpace(denialReason) ||
            mode == RestPermissionMode.Conditional && string.IsNullOrWhiteSpace(condition))
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Rest policy volume is invalid.");
        }

        Id = id;
        Name = name.Trim();
        Center = center;
        Size = size;
        Mode = mode;
        DenialReason = denialReason.Trim();
        Condition = condition.Trim();
        CooldownSeconds = cooldownSeconds;
        RequiredSecureRadius = requiredSecureRadius;
        Priority = priority;
        Provenance = provenance.Trim();
    }

    public bool Contains(Vector3Value position) =>
        Math.Abs(position.X - Center.X) <= Size.X / 2f &&
        Math.Abs(position.Y - Center.Y) <= Size.Y / 2f &&
        Math.Abs(position.Z - Center.Z) <= Size.Z / 2f;
}
