namespace Forge.Domain.Combat;

public sealed record CombatStats
{
    public int MaximumHealth { get; }

    public int MaximumResource { get; }

    public int Offense { get; }

    public int Defense { get; }

    public string Faction { get; }

    public string Team { get; }

    public CombatStats(
        int maximumHealth,
        int maximumResource,
        int offense,
        int defense,
        string faction,
        string team)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumHealth, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumResource);
        ArgumentOutOfRangeException.ThrowIfNegative(offense);
        ArgumentOutOfRangeException.ThrowIfNegative(defense);
        ArgumentException.ThrowIfNullOrWhiteSpace(faction);
        ArgumentException.ThrowIfNullOrWhiteSpace(team);

        MaximumHealth = maximumHealth;
        MaximumResource = maximumResource;
        Offense = offense;
        Defense = defense;
        Faction = faction.Trim();
        Team = team.Trim();
    }
}
