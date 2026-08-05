using Forge.Domain.Spatial;

namespace Forge.Domain.RuntimePolicies;

public sealed record AreaRestPolicy
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public RestPermissionMode Mode { get; }
    public string DenialReason { get; }
    public string Condition { get; }
    public int CooldownSeconds { get; }
    public float RequiredSecureRadius { get; }
    public IReadOnlyList<RestPolicyVolume> Volumes { get; }
    public string Provenance { get; }

    public AreaRestPolicy(Guid id, int schemaVersion, RestPermissionMode mode, string denialReason,
        string condition, int cooldownSeconds, float requiredSecureRadius, IReadOnlyList<RestPolicyVolume> volumes,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(denialReason);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(volumes);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        var values = volumes.OrderByDescending(item => item.Priority).ThenBy(item => item.Id).ToArray();
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion || cooldownSeconds is < 0 or > 86_400 ||
            !float.IsFinite(requiredSecureRadius) || requiredSecureRadius is < 0 or > 512 ||
            mode != RestPermissionMode.Allowed && string.IsNullOrWhiteSpace(denialReason) ||
            mode == RestPermissionMode.Conditional && string.IsNullOrWhiteSpace(condition) ||
            values.Select(item => item.Id).Distinct().Count() != values.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Area rest policy is invalid.");
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Mode = mode;
        DenialReason = denialReason.Trim();
        Condition = condition.Trim();
        CooldownSeconds = cooldownSeconds;
        RequiredSecureRadius = requiredSecureRadius;
        Volumes = values;
        Provenance = provenance.Trim();
    }

    public AreaRestPolicy ResolveAt(Vector3Value position)
    {
        var volume = Volumes.FirstOrDefault(item => item.Contains(position));
        return volume is null
            ? this
            : new AreaRestPolicy(Id, SchemaVersion, volume.Mode, volume.DenialReason, volume.Condition,
                volume.CooldownSeconds, volume.RequiredSecureRadius, [], volume.Provenance);
    }
}
