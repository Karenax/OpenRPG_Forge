namespace Forge.Domain.Combat;

public sealed record AbilityEffect
{
    public AbilityEffectKind Kind { get; }

    public int Magnitude { get; }

    public AbilityEffect(AbilityEffectKind kind, int magnitude)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(magnitude, 1);
        Kind = kind;
        Magnitude = magnitude;
    }
}
