using Forge.Domain.Areas;

namespace Forge.Domain.Rules;

public sealed record AreaRulesProfile
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; }
    public int SchemaVersion { get; }
    public EnvironmentPerceptionProfile Perception { get; }
    public AreaCombatPolicy Combat { get; }
    public string Provenance { get; }

    public AreaRulesProfile(Guid id, int schemaVersion, EnvironmentPerceptionProfile perception,
        AreaCombatPolicy combat, string provenance)
    {
        ArgumentNullException.ThrowIfNull(perception);
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance);
        if (id == Guid.Empty || schemaVersion != CurrentSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(id), "Area rules profile is invalid.");
        Id = id;
        SchemaVersion = schemaVersion;
        Perception = perception;
        Combat = combat;
        Provenance = provenance.Trim();
    }

    public static AreaRulesProfile CreateDefault(AreaId areaId)
    {
        ArgumentNullException.ThrowIfNull(areaId);
        return new AreaRulesProfile(areaId.Value, 1, EnvironmentPerceptionProfile.CreateDefault(areaId),
            AreaCombatPolicy.CreateDefault(areaId), "migration-default");
    }
}
