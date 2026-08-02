namespace Forge.Domain.Combat;

public sealed class CombatantState
{
    private readonly Dictionary<AbilityId, double> _cooldownEnds = [];

    public string Id { get; }

    public CombatProfile Profile { get; }

    public int Health { get; private set; }

    public int Resource { get; private set; }

    public bool IsDead => Health == 0;

    public CombatantState(string id, CombatProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(profile);
        Id = id.Trim();
        Profile = profile;
        Health = profile.Stats.MaximumHealth;
        Resource = profile.Stats.MaximumResource;
    }

    public bool IsAbilityReady(AbilityDefinition ability, double elapsedSeconds) =>
        !_cooldownEnds.TryGetValue(ability.Id, out var readyAt) || elapsedSeconds >= readyAt;

    public void Restore(int health, int resource)
    {
        if (health < 0 || health > Profile.Stats.MaximumHealth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(health),
                $"Health must be between 0 and {Profile.Stats.MaximumHealth}.");
        }

        if (resource < 0 || resource > Profile.Stats.MaximumResource)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resource),
                $"Resource must be between 0 and {Profile.Stats.MaximumResource}.");
        }

        Health = health;
        Resource = resource;
        _cooldownEnds.Clear();
    }

    internal void SpendResource(int amount) => Resource -= amount;

    internal void StartCooldown(AbilityDefinition ability, double elapsedSeconds) =>
        _cooldownEnds[ability.Id] = elapsedSeconds + ability.CooldownSeconds;

    internal int Damage(int amount)
    {
        var applied = Math.Min(Health, Math.Max(amount, 0));
        Health -= applied;
        return applied;
    }

    internal int Heal(int amount)
    {
        var applied = Math.Min(Profile.Stats.MaximumHealth - Health, Math.Max(amount, 0));
        Health += applied;
        return applied;
    }

    internal int RestoreResource(int amount)
    {
        var applied = Math.Min(Profile.Stats.MaximumResource - Resource, Math.Max(amount, 0));
        Resource += applied;
        return applied;
    }
}
