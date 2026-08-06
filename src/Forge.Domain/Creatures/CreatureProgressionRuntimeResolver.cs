namespace Forge.Domain.Creatures;

public sealed record CreatureProgressionRuntimeSnapshot(
    bool Succeeded,
    bool IgnoredOptionalProfile,
    int TotalLevel,
    IReadOnlyDictionary<string, int> SkillRanks,
    IReadOnlyList<string> PreparedAbilityIds,
    IReadOnlyList<CreatureProgressionDiagnostic> Diagnostics);

public static class CreatureProgressionRuntimeResolver
{
    public static CreatureProgressionRuntimeSnapshot Resolve(
        CreatureProgressionProfile? profile,
        IReadOnlySet<string> availableAbilityIds,
        bool required = false)
    {
        ArgumentNullException.ThrowIfNull(availableAbilityIds);
        if (profile is null)
        {
            var diagnostics = required
                ? new[] { new CreatureProgressionDiagnostic("creature.progression.required_missing",
                    "creatureProgression", "The runtime requires a creature progression profile.") }
                : [];
            return new(!required, !required, 0, new Dictionary<string, int>(), [], diagnostics);
        }

        var validation = CreatureProgressionValidator.Validate(profile, "creatureProgression", availableAbilityIds);
        if (validation.Count > 0)
        {
            return new(false, false, 0, new Dictionary<string, int>(), [], validation);
        }

        return new(true, false, profile.Tracks.Sum(item => item.Level),
            profile.Skills.ToDictionary(item => item.SkillId, item => item.Rank, StringComparer.Ordinal),
            profile.Loadout.PreparedAbilities.Select(item => item.AbilityId).ToArray(), []);
    }

    public static double ResolveSkillCheck(
        CreatureProgressionRuntimeSnapshot snapshot,
        string skillId,
        double attributeModifier,
        double situationalModifier)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.Succeeded || !double.IsFinite(attributeModifier) || !double.IsFinite(situationalModifier))
        {
            throw new InvalidOperationException("CREATURE_PROGRESSION_SKILL_CHECK_INVALID");
        }

        return snapshot.SkillRanks.GetValueOrDefault(CreaturePresentationProfile.NormalizeIdentifier(skillId)) +
               attributeModifier + situationalModifier;
    }
}
