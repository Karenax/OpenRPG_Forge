using Forge.Domain.Combat;

namespace Forge.Application.Combat;

public enum CombatFeedbackAmountKind
{
    Damage,
    Healing,
    ResourceRestored,
}

public sealed record CombatFeedbackAmount(
    CombatFeedbackAmountKind Kind,
    int Value);

public sealed record CombatFeedbackPresentation(
    Guid TransactionId,
    string SourceCombatantId,
    string TargetCombatantId,
    AbilityId AbilityId,
    string AbilitySlug,
    string AbilityDisplayName,
    string VisualCueKey,
    IReadOnlyList<CombatFeedbackAmount> Amounts,
    int TargetHealth,
    int TargetMaximumHealth,
    int SourceResource,
    int SourceMaximumResource,
    double CooldownRemainingSeconds,
    double CooldownDurationSeconds,
    bool TargetDied);

public static class CombatFeedbackPresentationService
{
    public static CombatFeedbackPresentation Create(
        Guid transactionId,
        CombatantState source,
        CombatantState target,
        CombatActionResult result,
        double nowSeconds)
    {
        if (transactionId == Guid.Empty)
        {
            throw new ArgumentException("A combat feedback transaction ID is required.", nameof(transactionId));
        }

        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(result);

        var amounts = new List<CombatFeedbackAmount>(3);
        AddPositive(amounts, CombatFeedbackAmountKind.Damage, result.Damage);
        AddPositive(amounts, CombatFeedbackAmountKind.Healing, result.Healing);
        AddPositive(amounts, CombatFeedbackAmountKind.ResourceRestored, result.ResourceRestored);

        return new CombatFeedbackPresentation(
            transactionId,
            source.Id,
            target.Id,
            result.Ability.Id,
            result.Ability.Slug,
            result.Ability.DisplayName,
            result.VisualCue.CueKey,
            amounts.AsReadOnly(),
            target.Health,
            target.Profile.Stats.MaximumHealth,
            source.Resource,
            source.Profile.Stats.MaximumResource,
            source.CooldownRemaining(result.Ability, nowSeconds),
            result.Ability.CooldownSeconds,
            result.TargetDied);
    }

    private static void AddPositive(
        ICollection<CombatFeedbackAmount> amounts,
        CombatFeedbackAmountKind kind,
        int value)
    {
        if (value > 0)
        {
            amounts.Add(new CombatFeedbackAmount(kind, value));
        }
    }
}
