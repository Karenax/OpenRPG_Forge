using Forge.Domain.Rules;

namespace Forge.Application.Rules;

public static class RulesRuntimeService
{
    public static ProgressionResolution ResolveProgression(ProgressionPolicy policy, double baseAmount,
        string sourceId, int partySize, double currentExperience, double mutatorMultiplier = 1)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        if (!double.IsFinite(baseAmount) || baseAmount < 0 || partySize <= 0 ||
            !double.IsFinite(currentExperience) || currentExperience < 0 ||
            !double.IsFinite(mutatorMultiplier) || mutatorMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(baseAmount), "Progression inputs are invalid.");

        var source = policy.SourceMultipliers.GetValueOrDefault(sourceId, 1);
        var group = policy.ShareWithParty ? policy.GroupMultiplier / partySize : policy.GroupMultiplier;
        var awarded = baseAmount * source * group * policy.DifficultyMultiplier * mutatorMultiplier;
        var total = currentExperience + awarded;
        var level = policy.Curve.LastOrDefault(point => point.RequiredExperience <= total)?.Level ?? 1;
        var next = policy.Curve.FirstOrDefault(point => point.Level > level)?.RequiredExperience;
        return new ProgressionResolution(awarded, total, level, next, sourceId,
            $"base:{baseAmount:0.###}|source:{source:0.###}|group:{group:0.###}|difficulty:{policy.DifficultyMultiplier:0.###}|mutator:{mutatorMultiplier:0.###}");
    }

    public static ResolvedDifficulty ResolveDifficulty(ModuleRulesProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var active = profile.ActiveDifficulty;
        double Scalar(string key) => profile.Mutators.Mutators.Where(item => item.Enabled)
            .Select(item => item.ScalarOverrides.GetValueOrDefault(key, 1))
            .Aggregate(1d, (left, right) => left * right);

        return new ResolvedDifficulty(active.Key, active.DisplayName,
            active.PlayerDamageMultiplier * Scalar("playerDamage"),
            active.IncomingDamageMultiplier * Scalar("incomingDamage"),
            active.AiIntensityMultiplier * Scalar("aiIntensity"),
            active.EconomyMultiplier * Scalar("economy"),
            active.RestRecoveryMultiplier * Scalar("restRecovery"), Scalar("progression"),
            active.SaveRestrictions, active.FriendlyFire, active.Accessibility,
            profile.Mutators.Mutators.Where(item => item.Enabled).Select(item => item.Key).ToArray());
    }

    public static PerceptionResolution ResolvePerception(EnvironmentPerceptionProfile profile, string senseId,
        double baseRange, double baseConfidence, IReadOnlySet<string> environmentTags)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(senseId);
        ArgumentNullException.ThrowIfNull(environmentTags);
        if (!double.IsFinite(baseRange) || baseRange < 0 || !double.IsFinite(baseConfidence))
            throw new ArgumentOutOfRangeException(nameof(baseRange), "Perception inputs are invalid.");

        var applicable = profile.Modifiers.Where(item => item.SenseId == senseId &&
            (string.IsNullOrEmpty(item.ConditionTag) || environmentTags.Contains(item.ConditionTag))).ToArray();
        var multiplier = applicable.Aggregate(1d, (value, item) => value * item.Multiplier);
        var additive = applicable.Sum(item => item.AdditiveModifier);
        return new PerceptionResolution(Math.Max(0, baseRange * multiplier),
            Math.Clamp(baseConfidence * multiplier + additive, 0, 1), applicable.Select(item => item.Id).ToArray());
    }
}

public sealed record ProgressionResolution(double AwardedExperience, double TotalExperience, int Level,
    double? NextLevelExperience, string SourceId, string Trace);

public sealed record ResolvedDifficulty(string Key, string DisplayName, double PlayerDamageMultiplier,
    double IncomingDamageMultiplier, double AiIntensityMultiplier, double EconomyMultiplier,
    double RestRecoveryMultiplier, double ProgressionMultiplier, SaveRestrictionPolicy SaveRestrictions,
    FriendlyFireMode FriendlyFire, DifficultyAccessibilityOptions Accessibility,
    IReadOnlyList<string> EnabledMutators);

public sealed record PerceptionResolution(double Range, double Confidence, IReadOnlyList<Guid> AppliedModifierIds);
