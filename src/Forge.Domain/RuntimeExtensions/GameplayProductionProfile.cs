using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.RuntimeExtensions;

public sealed record GameplayProductionProfile
{
    public const int CurrentSchemaVersion = 1;

    public sealed record ZeroGravitySettings(Guid Id, bool Enabled, float ThrusterAcceleration,
        float MaximumSpeed, float LinearDrag, bool OrientationAssist, bool PreserveMomentum);

    public sealed record VehicleSettings(Guid Id, bool Enabled, int MaximumSeats, string MotionMode,
        int MaximumIntegrity, bool AllowPossession, bool ReplicateAuthoritatively);

    public sealed record MountSettings(Guid Id, bool Enabled, bool AllowCombat, bool AllowInventory,
        bool PersistRiderState, string DismountPolicy);

    public sealed record AchievementDefinition(Guid Id, string Key, string EventName, int Threshold,
        string? PlatformKey, bool PrivacySafeTelemetry);

    public sealed record CuratedPackSettings(Guid Id, string Channel, string Compatibility,
        IReadOnlyList<string> AllowedLicenses, bool RequireTrustedSignature, bool AllowOfflineFallback);

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public ZeroGravitySettings ZeroGravity { get; }
    public VehicleSettings Vehicles { get; }
    public MountSettings Mounts { get; }
    public IReadOnlyList<AchievementDefinition> Achievements { get; }
    public CuratedPackSettings CuratedPacks { get; }
    public string Provenance { get; }

    public GameplayProductionProfile(Guid id, int schemaVersion, ZeroGravitySettings zeroGravity,
        VehicleSettings vehicles, MountSettings mounts, IReadOnlyList<AchievementDefinition> achievements,
        CuratedPackSettings curatedPacks, string provenance)
    {
        ArgumentNullException.ThrowIfNull(zeroGravity);
        ArgumentNullException.ThrowIfNull(vehicles);
        ArgumentNullException.ThrowIfNull(mounts);
        ArgumentNullException.ThrowIfNull(achievements);
        ArgumentNullException.ThrowIfNull(curatedPacks);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion ||
            zeroGravity.Id == Guid.Empty || zeroGravity.ThrusterAcceleration is < .1f or > 500f ||
            zeroGravity.MaximumSpeed is < .1f or > 2_000f || zeroGravity.LinearDrag is < 0f or > 20f)
            throw new ArgumentOutOfRangeException(nameof(id), "Gameplay production settings require schema v1 and bounded zero-gravity motion.");
        if (vehicles.Id == Guid.Empty || vehicles.MaximumSeats is < 1 or > 64 ||
            vehicles.MaximumIntegrity is < 1 or > 1_000_000 || vehicles.MotionMode is not ("kinematic" or "physics"))
            throw new ArgumentException("Vehicles require seats, integrity and a supported motion mode.", nameof(vehicles));
        if (mounts.Id == Guid.Empty || mounts.DismountPolicy is not ("safe-ground" or "nearest-valid" or "explicit-only"))
            throw new ArgumentException("Mounts require a supported dismount policy.", nameof(mounts));
        var orderedAchievements = achievements.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        if (orderedAchievements.Any(item => item.Id == Guid.Empty || item.Threshold is < 1 or > 1_000_000 ||
                string.IsNullOrWhiteSpace(item.EventName)) ||
            orderedAchievements.Select(item => item.Id).Distinct().Count() != orderedAchievements.Length ||
            orderedAchievements.Select(item => Normalize(item.Key)).Distinct(StringComparer.Ordinal).Count() != orderedAchievements.Length)
            throw new ArgumentException("Achievements require unique IDs and keys, events and bounded thresholds.", nameof(achievements));
        if (curatedPacks.Id == Guid.Empty || string.IsNullOrWhiteSpace(curatedPacks.Channel) ||
            string.IsNullOrWhiteSpace(curatedPacks.Compatibility) || curatedPacks.AllowedLicenses.Count == 0 ||
            curatedPacks.AllowedLicenses.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Curated packs require a channel, compatibility and an explicit license allowlist.", nameof(curatedPacks));
        var ids = orderedAchievements.Select(item => item.Id).Append(id).Append(zeroGravity.Id).Append(vehicles.Id)
            .Append(mounts.Id).Append(curatedPacks.Id).ToArray();
        if (ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("Gameplay production UUIDs must be unique.");

        Id = id;
        SchemaVersion = schemaVersion;
        ZeroGravity = zeroGravity;
        Vehicles = vehicles with { MotionMode = vehicles.MotionMode.Trim().ToLowerInvariant() };
        Mounts = mounts with { DismountPolicy = mounts.DismountPolicy.Trim().ToLowerInvariant() };
        Achievements = orderedAchievements.Select(item => item with
        {
            Key = Normalize(item.Key),
            EventName = item.EventName.Trim(),
            PlatformKey = string.IsNullOrWhiteSpace(item.PlatformKey) ? null : item.PlatformKey.Trim(),
        }).ToArray();
        CuratedPacks = curatedPacks with
        {
            Channel = Normalize(curatedPacks.Channel),
            Compatibility = curatedPacks.Compatibility.Trim(),
            AllowedLicenses = curatedPacks.AllowedLicenses.Select(item => item.Trim().ToUpperInvariant())
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
        };
        Provenance = RequireText(provenance, nameof(provenance));
    }

    public static GameplayProductionProfile CreateDefault(Guid ownerId)
    {
        Guid Stable(string key) => RuntimePolicyIdFactory.Create(ownerId, $"post-launch:gameplay:{key}");
        return new GameplayProductionProfile(Stable("root"), 1,
            new ZeroGravitySettings(Stable("zero-gravity"), false, 12, 24, .15f, true, true),
            new VehicleSettings(Stable("vehicles"), false, 4, "kinematic", 1_000, true, true),
            new MountSettings(Stable("mounts"), false, false, true, true, "safe-ground"),
            [], new CuratedPackSettings(Stable("curated-packs"), "stable", "1.x", ["CC0-1.0"], true, true),
            "migration-default");
    }

    public IReadOnlyList<Guid> AllIds() => Achievements.Select(item => item.Id).Append(Id).Append(ZeroGravity.Id)
        .Append(Vehicles.Id).Append(Mounts.Id).Append(CuratedPacks.Id).ToArray();

    private static string Normalize(string value) => DataDrivenRuleSchema.NormalizeKey(value, nameof(value));
    private static string RequireText(string value, string parameter) => DataDrivenRuleSchema.RequireText(value, parameter);
}
