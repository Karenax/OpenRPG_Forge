namespace Forge.Domain.Combat;

public sealed record CombatActionResult(
    AbilityDefinition Ability,
    int Damage,
    int Healing,
    int ResourceRestored,
    bool TargetDied,
    IReadOnlyList<LootDrop> Loot,
    AbilityVisualCue VisualCue);
