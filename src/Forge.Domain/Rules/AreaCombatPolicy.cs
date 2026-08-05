using Forge.Domain.Areas;
using Forge.Domain.RuntimePolicies;

namespace Forge.Domain.Rules;

public enum AreaPvpMode { Disabled, DuelsOnly, HostileFactions, Open }

public sealed record AreaCombatPolicy
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public AreaPvpMode PvpMode { get; }
    public FriendlyFireMode FriendlyFire { get; }
    public bool CrimeEnabled { get; }
    public bool AggroEnabled { get; }
    public bool EnvironmentalDamageEnabled { get; }
    public bool ServerAuthoritative { get; }
    public string Provenance { get; }

    public AreaCombatPolicy(Guid id, int schemaVersion, AreaPvpMode pvpMode, FriendlyFireMode friendlyFire,
        bool crimeEnabled, bool aggroEnabled, bool environmentalDamageEnabled, bool serverAuthoritative,
        string provenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(id), "Area combat policy is invalid.");
        Id = id;
        SchemaVersion = schemaVersion;
        PvpMode = pvpMode;
        FriendlyFire = friendlyFire;
        CrimeEnabled = crimeEnabled;
        AggroEnabled = aggroEnabled;
        EnvironmentalDamageEnabled = environmentalDamageEnabled;
        ServerAuthoritative = serverAuthoritative;
        Provenance = provenance.Trim();
    }

    internal static AreaCombatPolicy CreateDefault(AreaId areaId) => new(
        RuntimePolicyIdFactory.Create(areaId.Value, "rules:combat"), 1, AreaPvpMode.Disabled,
        FriendlyFireMode.Disabled, true, true, true, true, "migration-default");
}
