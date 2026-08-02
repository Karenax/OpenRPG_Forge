namespace Forge.Domain.Combat;

public sealed record AbilityDefinition
{
    public AbilityId Id { get; }

    public string Slug { get; }

    public string DisplayName { get; }

    public int ResourceCost { get; }

    public double CooldownSeconds { get; }

    public AbilityTargeting Targeting { get; }

    public IReadOnlyList<AbilityEffect> Effects { get; }

    public AbilityVisualCue VisualCue { get; }

    public AbilityDefinition(
        AbilityId id,
        string slug,
        string displayName,
        int resourceCost,
        double cooldownSeconds,
        AbilityTargeting targeting,
        IReadOnlyList<AbilityEffect> effects,
        AbilityVisualCue visualCue)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentOutOfRangeException.ThrowIfNegative(resourceCost);
        ArgumentOutOfRangeException.ThrowIfNegative(cooldownSeconds);
        if (!double.IsFinite(cooldownSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(cooldownSeconds), "Cooldown must be finite.");
        }

        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(visualCue);
        var materializedEffects = effects.ToArray();
        if (materializedEffects.Length == 0)
        {
            throw new ArgumentException("An ability must contain at least one effect.", nameof(effects));
        }

        Id = id;
        Slug = slug.Trim();
        DisplayName = displayName.Trim();
        ResourceCost = resourceCost;
        CooldownSeconds = cooldownSeconds;
        Targeting = targeting;
        Effects = materializedEffects;
        VisualCue = visualCue;
    }
}
