namespace Forge.Domain.Quests;

public sealed record QuestOperationsRuntimeContext(
    DateTimeOffset Now,
    DateTimeOffset? LastCompletion,
    int CompletionCount,
    int PartyMemberCount,
    int OptedInMembers,
    bool ActorIsLeader,
    bool ServerAuthorized,
    IReadOnlySet<QuestTransitionId> ChosenTransitions);

public sealed record QuestRepeatDecision(bool Allowed, string Code, DateTimeOffset? NextEligibleAt, double RewardMultiplier, int Seed);
public sealed record QuestShareDecision(bool Accepted, string Code, int RequiredMembers, QuestSeparationPolicy Separation);
public sealed record QuestExclusiveDecision(bool Allowed, bool RequiresConfirmation, IReadOnlyList<QuestTransitionId> LockedTransitions, string ConsequenceKey);
public sealed record QuestNotificationDecision(bool Visible, bool Cinematic, string GroupKey, string TitleKey, string BodyKey, string AccessibleText);

public static class QuestOperationsRuntimeResolver
{
    public static QuestRepeatDecision ResolveRepeat(QuestDefinition quest, QuestOperationsRuntimeContext context)
    {
        var profile = RequireValid(quest);
        var repeat = profile.Repeat;
        if (!repeat.Enabled)
            return new(context.CompletionCount == 0, context.CompletionCount == 0 ? "quest.repeat.first_run" : "quest.repeat.disabled", null, 1, repeat.SeedOffset);
        if (repeat.MaximumCompletions > 0 && context.CompletionCount >= repeat.MaximumCompletions)
            return new(false, "quest.repeat.maximum_reached", null, 0, repeat.SeedOffset + context.CompletionCount);
        var next = context.LastCompletion?.AddSeconds(repeat.CooldownSeconds);
        if (next > context.Now)
            return new(false, "quest.repeat.cooldown", next, 0, repeat.SeedOffset + context.CompletionCount);
        return new(true, "quest.repeat.allowed", next, Math.Pow(repeat.RewardDecay, context.CompletionCount),
            repeat.SeedOffset + context.CompletionCount);
    }

    public static QuestShareDecision ResolveShare(QuestDefinition quest, QuestOperationsRuntimeContext context)
    {
        var share = RequireValid(quest).Share;
        if (share.RequireServerAuthority && !context.ServerAuthorized)
            return new(false, "quest.share.server_authority_required", 0, share.Separation);
        var required = share.Mode switch
        {
            QuestShareMode.Unanimous => context.PartyMemberCount,
            QuestShareMode.OptIn => 1,
            QuestShareMode.Leader => 1,
            _ => 0
        };
        var accepted = share.Mode switch
        {
            QuestShareMode.Individual or QuestShareMode.PartyShared => true,
            QuestShareMode.OptIn => context.OptedInMembers > 0,
            QuestShareMode.Leader => context.ActorIsLeader,
            QuestShareMode.Unanimous => context.PartyMemberCount > 0 && context.OptedInMembers == context.PartyMemberCount,
            _ => false
        };
        return new(accepted, accepted ? "quest.share.accepted" : "quest.share.consent_missing", required, share.Separation);
    }

    public static QuestExclusiveDecision ResolveExclusive(
        QuestDefinition quest,
        QuestTransitionId requestedTransition,
        QuestOperationsRuntimeContext context)
    {
        var profile = RequireValid(quest);
        var policy = profile.ExclusiveChoices.SingleOrDefault(item => item.TransitionId == requestedTransition);
        var blockedByInboundConflict = profile.ExclusiveChoices.Any(item =>
            context.ChosenTransitions.Contains(item.TransitionId)
            && item.IncompatibleTransitionIds.Contains(requestedTransition));
        if (policy is null)
            return new(!blockedByInboundConflict, false, [], string.Empty);
        var sameGroup = profile.ExclusiveChoices.Where(item => string.Equals(item.MutexGroup, policy.MutexGroup, StringComparison.Ordinal)).ToArray();
        var blockedByPriorChoice = sameGroup.Any(item => item.TransitionId != requestedTransition && context.ChosenTransitions.Contains(item.TransitionId));
        var explicitConflict = policy.IncompatibleTransitionIds.Any(context.ChosenTransitions.Contains);
        return new(!blockedByPriorChoice && !explicitConflict && !blockedByInboundConflict, policy.Irreversible,
            sameGroup.Where(item => item.TransitionId != requestedTransition).Select(item => item.TransitionId)
                .Concat(policy.IncompatibleTransitionIds).Distinct().OrderBy(item => item.Value).ToArray(),
            policy.Consequence.Key);
    }

    public static QuestNotificationDecision ResolveNotification(QuestDefinition quest)
    {
        var notification = RequireValid(quest).Notification;
        return new(notification.Mode != QuestNotificationMode.Silent,
            notification.Mode == QuestNotificationMode.Cinematic,
            notification.GroupKey,
            notification.Title.Key,
            notification.Body.Key,
            notification.AccessibleText);
    }

    public static IReadOnlyList<QuestDefinition> SortJournal(IEnumerable<QuestDefinition> quests) => quests
        .OrderByDescending(item => item.Operations?.Journal.DefaultPinned ?? false)
        .ThenByDescending(item => item.Operations?.Journal.Sorting == QuestJournalSortingPolicy.ImportanceThenUpdated
            ? item.Operations.Journal.NarrativeImportance
            : item.Operations?.Journal.Priority ?? 0)
        .ThenBy(item => item.DisplayName, StringComparer.Ordinal)
        .ThenBy(item => item.Id.Value)
        .ToArray();

    private static QuestOperationsProfile RequireValid(QuestDefinition quest)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (quest.Operations is not { } profile)
            throw new InvalidOperationException("quest.operations.missing");
        var diagnostics = QuestOperationsValidator.Validate(quest);
        if (diagnostics.Count > 0)
            throw new InvalidOperationException($"{diagnostics[0].Code}: {diagnostics[0].Path}");
        return profile;
    }
}
