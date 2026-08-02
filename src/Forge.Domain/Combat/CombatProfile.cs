namespace Forge.Domain.Combat;

public sealed record CombatProfile
{
    public CombatStats Stats { get; }

    public IReadOnlyList<AbilityDefinition> Abilities { get; }

    public IReadOnlyList<LootDrop> Loot { get; }

    public CombatProfile(
        CombatStats stats,
        IReadOnlyList<AbilityDefinition>? abilities = null,
        IReadOnlyList<LootDrop>? loot = null)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var materializedAbilities = (abilities ?? []).ToArray();
        if (materializedAbilities.Select(ability => ability.Id).Distinct().Count() != materializedAbilities.Length)
        {
            throw new ArgumentException("A combat profile cannot contain duplicate ability IDs.", nameof(abilities));
        }

        Stats = stats;
        Abilities = materializedAbilities;
        Loot = (loot ?? []).ToArray();
    }
}
