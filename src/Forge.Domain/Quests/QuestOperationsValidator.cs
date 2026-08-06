using Forge.Domain.Validation;

namespace Forge.Domain.Quests;

public static class QuestOperationsValidator
{
    public static IReadOnlyList<DomainDiagnostic> Validate(QuestDefinition quest, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (quest.Operations is not { } profile)
            return [];

        var root = path ?? $"quests/{quest.Id}/operations";
        var diagnostics = new List<DomainDiagnostic>();
        if (profile.Id == Guid.Empty || profile.SchemaVersion != QuestOperationsProfile.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(profile.Provenance))
            diagnostics.Add(Diagnostic("quest.operations.identity_invalid", root, "Quest operations identity is invalid."));
        if (profile.Journal.Priority is < 0 or > 100 || profile.Journal.NarrativeImportance is < 0 or > 100 ||
            profile.Journal.DefaultPinned && !profile.Journal.AllowManualPin)
            diagnostics.Add(Diagnostic("quest.operations.journal_invalid", $"{root}/journal", "Journal priority, importance or pin policy is invalid."));
        if (profile.Repeat.CooldownSeconds < 0 || profile.Repeat.MaximumCompletions < 0 ||
            profile.Repeat.RewardDecay is < 0 or > 1 ||
            profile.Repeat.ResetStateIds.Distinct().Count() != profile.Repeat.ResetStateIds.Count ||
            profile.Repeat.ResetStateIds.Any(id => quest.States.All(state => state.Id != id)) ||
            profile.Repeat.ResetMode == QuestRepeatResetMode.SelectedStates && profile.Repeat.ResetStateIds.Count == 0)
            diagnostics.Add(Diagnostic("quest.operations.repeat_invalid", $"{root}/repeat", "Repeat cooldown, count, decay or reset states are invalid."));
        if (profile.Share.Mode == QuestShareMode.Unanimous && !profile.Share.SynchronizeProgress)
            diagnostics.Add(Diagnostic("quest.operations.share_invalid", $"{root}/share", "Unanimous quests must synchronize party progress."));

        var transitionIds = quest.Transitions.Select(item => item.Id).ToHashSet();
        var choiceIds = new HashSet<Guid>();
        foreach (var choice in profile.ExclusiveChoices)
        {
            var choicePath = $"{root}/exclusiveChoices/{choice.Id}";
            if (choice.Id == Guid.Empty || !choiceIds.Add(choice.Id) || !transitionIds.Contains(choice.TransitionId) ||
                string.IsNullOrWhiteSpace(choice.MutexGroup) || string.IsNullOrWhiteSpace(choice.Provenance) ||
                choice.IncompatibleTransitionIds.Contains(choice.TransitionId) ||
                choice.IncompatibleTransitionIds.Distinct().Count() != choice.IncompatibleTransitionIds.Count ||
                choice.IncompatibleTransitionIds.Any(id => !transitionIds.Contains(id)))
                diagnostics.Add(Diagnostic("quest.operations.exclusive_invalid", choicePath, "Exclusive choice identity, group or transition references are invalid."));
            if (choice.Irreversible && (string.IsNullOrWhiteSpace(choice.Confirmation.Key) ||
                                        string.IsNullOrWhiteSpace(choice.Confirmation.Fallback)))
                diagnostics.Add(Diagnostic("quest.operations.confirmation_missing", $"{choicePath}/confirmation", "Irreversible choices require a localized confirmation."));
        }

        var notification = profile.Notification;
        if (notification.CooldownSeconds < 0 || string.IsNullOrWhiteSpace(notification.Title.Key) ||
            string.IsNullOrWhiteSpace(notification.Title.Fallback) || string.IsNullOrWhiteSpace(notification.Body.Key) ||
            string.IsNullOrWhiteSpace(notification.Body.Fallback) || string.IsNullOrWhiteSpace(notification.AccessibleText) ||
            string.IsNullOrWhiteSpace(notification.GroupKey))
            diagnostics.Add(Diagnostic("quest.operations.notification_invalid", $"{root}/notification", "Notification localization, accessibility, group or cooldown is invalid."));
        return diagnostics;
    }

    private static DomainDiagnostic Diagnostic(string code, string path, string message) => new(code, message, path);
}
