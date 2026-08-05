using Forge.Domain.Rules;
using Forge.Domain.Spatial;

namespace Forge.RpgKit.Domain.Creatures;

public static class EnvironmentPerceptionService
{
    public static IReadOnlyList<PerceptionResult> Detect(
        CreaturePerceptionProfile creature,
        EnvironmentPerceptionProfile environment,
        IReadOnlySet<string> environmentTags,
        Vector3Value origin,
        IReadOnlyList<Stimulus> stimuli,
        double nowSeconds)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(environmentTags);
        ArgumentNullException.ThrowIfNull(stimuli);

        var modifiers = creature.Senses.ToDictionary(
            sense => sense.Kind,
            sense => Resolve(environment, sense.Kind, environmentTags));
        var adjusted = new CreaturePerceptionProfile(
            creature.Senses.Select(sense => sense with
            {
                Range = Math.Max(0.001, sense.Range * modifiers[sense.Kind].Multiplier),
            }).ToArray(),
            creature.MemorySeconds);

        return CreatureRulesService.Detect(adjusted, origin, stimuli, nowSeconds)
            .Select(result => result with
            {
                Confidence = Math.Clamp(result.Confidence + modifiers[result.Sense].Additive, 0, 1),
            })
            .OrderByDescending(result => result.Confidence)
            .ThenBy(result => result.StimulusId, StringComparer.Ordinal)
            .ToArray();
    }

    private static (double Multiplier, double Additive) Resolve(
        EnvironmentPerceptionProfile environment,
        SenseKind sense,
        IReadOnlySet<string> environmentTags)
    {
        var senseId = sense.ToString().ToLowerInvariant();
        var applicable = environment.Modifiers.Where(item =>
            string.Equals(item.SenseId, senseId, StringComparison.Ordinal) &&
            (string.IsNullOrEmpty(item.ConditionTag) || environmentTags.Contains(item.ConditionTag)));
        return applicable.Aggregate((Multiplier: 1d, Additive: 0d),
            (value, item) => (value.Multiplier * item.Multiplier, value.Additive + item.AdditiveModifier));
    }
}
